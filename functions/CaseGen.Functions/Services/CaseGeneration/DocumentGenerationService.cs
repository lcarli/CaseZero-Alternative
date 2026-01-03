using CaseGen.Functions.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CaseGen.Functions.Services.CaseGeneration;

/// <summary>
/// Service responsible for generating document content from specifications (Phase 5).
/// Handles document generation and PDF rendering.
/// </summary>
public class DocumentGenerationService
{
    private readonly ILLMService _llmService;
    private readonly IStorageService _storageService;
    private readonly ICaseLoggingService _caseLogging;
    private readonly IPdfRenderingService _pdfRenderingService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DocumentGenerationService> _logger;

    public DocumentGenerationService(
        ILLMService llmService,
        IStorageService storageService,
        ICaseLoggingService caseLogging,
        IPdfRenderingService pdfRenderingService,
        IConfiguration configuration,
        ILogger<DocumentGenerationService> logger)
    {
        _llmService = llmService;
        _storageService = storageService;
        _caseLogging = caseLogging;
        _pdfRenderingService = pdfRenderingService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Generates document content from specification.
    /// </summary>
    public async Task<string> GenerateDocumentFromSpecAsync(
        DocumentSpec spec,
        string designJson,
        string caseId,
        string? planJson = null,
        string? expandJson = null,
        string? difficultyOverride = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Gen Doc[{DocId}] type={Type} title={Title}", spec.DocId, spec.Type, spec.Title);

        // Derive difficulty (Plan > override > Rookie)
        string difficulty = "Rookie";
        if (!string.IsNullOrWhiteSpace(difficultyOverride)) difficulty = difficultyOverride;
        if (!string.IsNullOrWhiteSpace(planJson))
        {
            try
            {
                using var p = JsonDocument.Parse(planJson);
                if (p.RootElement.TryGetProperty("difficulty", out var d) && d.ValueKind == JsonValueKind.String)
                    difficulty = d.GetString() ?? difficulty;
            }
            catch { /* ignore */ }
        }

        // Type-specific directives
        string typeDirectives = spec.Type.ToLowerInvariant() switch
        {
            "police_report" => """
                FORMAT (Markdown inside 'content'):
                - Header: Report Number, Date/Time (ISO-8601 with offset), Unit / Responsible Officer.
                - Objective incident summary.
                - Requested sections (use H2 headings: ##).
                - Bullet lists when appropriate.

                MINIMUM ANCHORS (must be real from Design/Expand):
                - Cite ≥2 real IDs (evidenceId and/or docId) and/or concrete timeline timestamps/events.
                - Do not infer guilt or reveal solution.
                """,

            "interview" => """
                FORMAT (Markdown inside 'content'):
                - Clean transcript (no interviewer opinions/judgment).
                - Label lines: **Interviewer:** / **Interviewee:**.
                - Optional timestamps in brackets when natural (e.g., [00:05]).
                - Objective Q&A; do not infer guilt.

                MINIMUM ANCHORS:
                - Reference ≥1 real support: an evidenceId and/or a concrete timeline event/timestamp relevant to statements.
                """,

            "memo_admin" => """
                FORMAT (Markdown inside 'content'):
                - Header: To / From / Subject / Date.
                - Concise bureaucratic tone; use bullets for action items.
                - Reference documents/evidence by ID when available.

                MINIMUM ANCHORS:
                - Cite ≥1 real docId or evidenceId mentioned in Design/Expand when appropriate.
                """,

            "forensics_report" => """
                FORMAT (Markdown inside 'content'):
                - Header: Laboratory / Examiner / Date / Time (ISO-8601 with offset).
                - Methodology (procedures), Results, Interpretation / Limitations.
                - **Chain of Custody** (mandatory) with events in temporal order.
                - Reference relevant evidenceId/docId (do not reveal solution).

                MINIMUM ANCHORS:
                - Cite ≥2 real evidenceId (and docId if applicable).
                - Chain of Custody must include ISO-8601 timestamps and ordered handoffs.
                """,

            "evidence_log" => """
                FORMAT (Markdown inside 'content'):
                - Table: ItemId | Collected At | Collected By | Description | Storage | Transfers.
                - Brief notes per item.

                MINIMUM ANCHORS:
                - Every line must correspond to a real evidenceId present in Design (no new items).
                """,

            "witness_statement" => """
                FORMAT (Markdown inside 'content'):
                - First-person statement, objective, no speculation about perpetrator.
                - Date/Time (ISO-8601 with offset) and brief fictional identification.

                MINIMUM ANCHORS:
                - Reference ≥1 real evidenceId or a concrete timeline event/timestamp corroborating the statement.
                """,

            _ => "FORMAT: use the requested sections; objective, documentary text."
        };

        // Difficulty-specific directives
        string difficultyDirectives = difficulty switch
        {
            "Rookie" or "Iniciante" => """
                DIFFICULTY PROFILE:
                - Simple, direct vocabulary; low ambiguity.
                - Linear chronology and explicit relationships.
                - Aim near the minimum of lengthTarget.
                """,
            "Detective" or "Detective2" => """
                DIFFICULTY PROFILE:
                - Moderate vocabulary; some jargon with context.
                - Introduce plausible ambiguities and light cross-checks.
                - Aim for the middle of the lengthTarget range.
                """,
            "Sergeant" or "Lieutenant" => """
                DIFFICULTY PROFILE:
                - Technical tone when relevant; correlations across sources.
                - Realistic ambiguities (without revealing solution); cite times and IDs.
                - Aim at the top of the lengthTarget range.
                """,
            _ /* Captain/Commander */ => """
                DIFFICULTY PROFILE:
                - Technical language; specialized inferences.
                - Controlled ambiguity, competing hypotheses.
                - Cross multiple sources; mention methodological limitations.
                - Use near the maximum of lengthTarget.
                """
        };

        var designCtx = designJson ?? "{}";
        
        // EPIC 3.2: Extract and filter planned contradictions relevant to this document
        var relevantContradictions = ExtractRelevantContradictions(designJson, spec.DocId);
        var contradictionsPrompt = BuildContradictionsPrompt(relevantContradictions);

        var systemPrompt = @"
            You are a police / forensic technical writer. Generate ONLY JSON containing the document body.

            GENERAL RULES (MANDATORY):
            - Write all text in english.
            - Never reveal the solution or culprit.
            - Maintain consistency with Design specifications.
            - **Use exactly the provided 'sections' titles and in the exact order. Do NOT add or rename sections.**
            - Follow exactly the word count range (lengthTarget).
            - If type == forensics_report, include the ""Chain of Custody"" section.
            - Whenever citing evidence, reference existing evidenceId/docId (do not invent).
            - Do not mention gating in the content (gating is game metadata).
            - No real PII, brands, or real addresses.

            TEMPORAL CONSISTENCY (CRITICAL):
            - ALL timestamps MUST use ISO-8601 format with the same timezone offset from Design
            - Document creation date MUST match the dateCreated specified in Design for this document
            - All referenced times (incident times, collection times, interview times) MUST be consistent with established timeline
            - Chain of Custody timestamps MUST be chronologically ordered and realistic
            - NO conflicting or overlapping timestamps with other documents or evidence

            OUTPUT JSON:
            {
            ""docId"": ""string"",
            ""type"": ""string"",
            ""title"": ""string"",
            ""words"": number,
            ""sections"": [ { ""title"": ""string"", ""content"": ""markdown"" } ]
            }";

        var userPrompt = $@"
            CONTEXT — DESIGN:
            {designCtx}

            DOCUMENT TO GENERATE:
            - docId: {spec.DocId}
            - type: {spec.Type}
            - title: {spec.Title}
            - sections (order): {string.Join(", ", spec.Sections)}
            - lengthTarget: {spec.LengthTarget.Min}–{spec.LengthTarget.Max} words
            - gated: {spec.Gated}

            TYPE DIRECTIVES:
            {typeDirectives}

            DIFFICULTY DIRECTIVES ({difficulty}):
            {difficultyDirectives}

            ANTI-INVENTION (MANDATORY):
            - People/locations/evidence: only those defined in Expand/Design.
            - If mentioning evidence, use existing evidenceId/docId; do not create new items.
            - Do not mention gating in the content (gating is metadata).

            EPIC 3.2 — PLANNED CONTRADICTIONS (CRITICAL):
            {contradictionsPrompt}

            PRE-SUBMISSION CHECK (the model must self-check before writing):
            - Did you verify that every cited evidenceId/docId exists in the supplied Design?
            - Are you using the exact 'sections' titles in the given order, without adding extra sections?
            - Are you within the specified lengthTarget range?
            - Are timestamps (when present) in ISO-8601 with offset?

            Output: **ONLY JSON** in the specified structure (no comments).";

        var json = await _llmService.GenerateAsync(caseId, systemPrompt, userPrompt, cancellationToken);

        try
        {
            await _caseLogging.LogStepResponseAsync(caseId, $"documents/{spec.DocId}", json, cancellationToken);

            var bundlesContainer = _configuration["CaseGeneratorStorage:BundlesContainer"] ?? "bundles";
            var docBundlePath = $"{caseId}/documents/{spec.DocId}.json";
            await _storageService.SaveFileAsync(bundlesContainer, docBundlePath, json, cancellationToken);

            _logger.LogInformation("BUNDLE: Saved doc to bundle: {Path} (case={CaseId}, type={Type})",
                docBundlePath, caseId, spec.Type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist generated document {DocId} for case {CaseId}",
                spec.DocId, caseId);
        }

        return json;
    }

    /// <summary>
    /// Renders document JSON to PDF format.
    /// </summary>
    public async Task<string> RenderDocumentFromJsonAsync(string docId, string documentJson, string caseId, CancellationToken cancellationToken = default)
    {
        return await _pdfRenderingService.RenderDocumentFromJsonAsync(docId, documentJson, caseId, cancellationToken);
    }

    // EPIC 3.2: Helper methods for planned contradictions
    
    /// <summary>
    /// Extracts contradictions from Design JSON that involve the specified document.
    /// </summary>
    private List<PlannedContradiction> ExtractRelevantContradictions(string? designJson, string docId)
    {
        var contradictions = new List<PlannedContradiction>();
        
        if (string.IsNullOrEmpty(designJson))
            return contradictions;

        try
        {
            var designDoc = JsonDocument.Parse(designJson);
            if (!designDoc.RootElement.TryGetProperty("plannedContradictions", out var contradictionsElement) ||
                contradictionsElement.ValueKind != JsonValueKind.Array)
            {
                return contradictions;
            }

            foreach (var contrElement in contradictionsElement.EnumerateArray())
            {
                var involvedDocs = new List<string>();
                if (contrElement.TryGetProperty("involvedDocuments", out var docsArr))
                {
                    foreach (var doc in docsArr.EnumerateArray())
                        involvedDocs.Add(doc.GetString() ?? "");
                }
                
                var resolvingDocs = new List<string>();
                if (contrElement.TryGetProperty("resolution", out var resolutionElement) &&
                    resolutionElement.TryGetProperty("resolvingDocuments", out var resDocsArr))
                {
                    foreach (var doc in resDocsArr.EnumerateArray())
                        resolvingDocs.Add(doc.GetString() ?? "");
                }

                // Include contradiction if this document is involved OR helps resolve it
                if (involvedDocs.Contains(docId) || resolvingDocs.Contains(docId))
                {
                    var involvedEvidences = new List<string>();
                    if (contrElement.TryGetProperty("involvedEvidences", out var evidArr))
                    {
                        foreach (var evid in evidArr.EnumerateArray())
                            involvedEvidences.Add(evid.GetString() ?? "");
                    }
                    
                    var involvedSuspects = new List<string>();
                    if (contrElement.TryGetProperty("involvedSuspects", out var suspArr))
                    {
                        foreach (var susp in suspArr.EnumerateArray())
                            involvedSuspects.Add(susp.GetString() ?? "");
                    }
                    
                    var resolvingEvidences = new List<string>();
                    if (resolutionElement.TryGetProperty("resolvingEvidences", out var resEvidArr))
                    {
                        foreach (var evid in resEvidArr.EnumerateArray())
                            resolvingEvidences.Add(evid.GetString() ?? "");
                    }
                    
                    contradictions.Add(new PlannedContradiction
                    {
                        ContradictionId = contrElement.GetProperty("contradictionId").GetString() ?? "",
                        Type = contrElement.GetProperty("type").GetString() ?? "",
                        Description = contrElement.GetProperty("description").GetString() ?? "",
                        InvolvedDocuments = involvedDocs.ToArray(),
                        InvolvedEvidences = involvedEvidences.ToArray(),
                        InvolvedSuspects = involvedSuspects.ToArray(),
                        Resolution = new ResolutionStrategy
                        {
                            Method = resolutionElement.GetProperty("method").GetString() ?? "",
                            ResolvingDocuments = resolvingDocs.ToArray(),
                            ResolvingEvidences = resolvingEvidences.ToArray(),
                            ExpectedConclusion = resolutionElement.GetProperty("expectedConclusion").GetString() ?? ""
                        },
                        MinimumDifficulty = contrElement.GetProperty("minimumDifficulty").GetString() ?? ""
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "EPIC 3.2: Failed to extract contradictions from Design JSON for document {DocId}", docId);
        }

        return contradictions;
    }

    /// <summary>
    /// Builds prompt section for planned contradictions.
    /// </summary>
    private string BuildContradictionsPrompt(List<PlannedContradiction> contradictions)
    {
        if (!contradictions.Any())
        {
            return "This document does NOT participate in any planned contradictions. Write it normally following all other rules.";
        }

        var prompt = "THIS DOCUMENT PARTICIPATES IN PLANNED CONTRADICTIONS (MANDATORY IMPLEMENTATION):\n\n";
        
        foreach (var contradiction in contradictions)
        {
            var isInvolved = contradiction.InvolvedDocuments.Any();
            var isResolving = contradiction.Resolution.ResolvingDocuments.Any();
            
            prompt += $"## Contradiction {contradiction.ContradictionId} ({contradiction.Type})\n";
            prompt += $"Description: {contradiction.Description}\n\n";
            
            if (isInvolved)
            {
                prompt += "**YOUR ROLE: IMPLEMENT THE CONTRADICTION**\n";
                prompt += "- You MUST include information that CONTRADICTS other documents/evidence\n";
                prompt += "- This contradiction is INTENTIONAL and part of the puzzle design\n";
                prompt += "- DO NOT try to 'fix' or reconcile the contradiction\n";
                prompt += "- Write the contradictory information naturally and believably\n";
                prompt += $"- Involved evidences: {string.Join(", ", contradiction.InvolvedEvidences)}\n";
                prompt += $"- Involved suspects: {string.Join(", ", contradiction.InvolvedSuspects)}\n\n";
            }
            
            if (isResolving)
            {
                prompt += "**YOUR ROLE: PROVIDE RESOLUTION CLUES**\n";
                prompt += "- Include information that HELPS RESOLVE the contradiction described above\n";
                prompt += $"- Resolution method: {contradiction.Resolution.Method}\n";
                prompt += $"- Expected conclusion: {contradiction.Resolution.ExpectedConclusion}\n";
                prompt += $"- Resolving evidences to reference: {string.Join(", ", contradiction.Resolution.ResolvingEvidences)}\n";
                prompt += "- Provide clear, factual information that allows the investigator to determine the truth\n\n";
            }
        }
        
        prompt += "**CRITICAL RULES FOR CONTRADICTIONS:**\n";
        prompt += "1. If you are implementing a contradiction (involved document), write EXACTLY the contradictory information as designed\n";
        prompt += "2. If you are providing resolution clues (resolving document), include clear factual information that resolves the discrepancy\n";
        prompt += "3. DO NOT mention that a contradiction exists in the text - write naturally\n";
        prompt += "4. DO NOT try to reconcile or explain away contradictions you are supposed to implement\n";
        prompt += "5. The contradiction is an intentional puzzle element - implement it faithfully\n";
        
        return prompt;
    }
}
