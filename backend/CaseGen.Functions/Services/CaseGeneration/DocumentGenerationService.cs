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
}
