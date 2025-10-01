using CaseGen.Functions.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text;
using System;
using System.Text.RegularExpressions;

namespace CaseGen.Functions.Services;

public class CaseGenerationService : ICaseGenerationService
{
    // RedTeam chunked processing constants
    private const int DefaultMaxBytesPerCall = 60_000;
    private const int MaxParallelCalls = 3;
    
    private readonly ILLMService _llmService;
    private readonly IStorageService _storageService;
    private readonly ISchemaValidationService _schemaValidationService;
    private readonly IConfiguration _configuration;
    private readonly IJsonSchemaProvider _schemaProvider;
    private readonly ICaseLoggingService _caseLogging;
    private readonly INormalizerService _normalizerService;
    private readonly IPdfRenderingService _pdfRenderingService;
    private readonly IImagesService _imagesService;
    private readonly IPrecisionEditor _precisionEditor;
    private readonly IRedTeamCacheService _redTeamCache;
    private readonly ILogger<CaseGenerationService> _logger;

    public CaseGenerationService(
        ILLMService llmService,
        IStorageService storageService,
        ISchemaValidationService schemaValidationService,
        IJsonSchemaProvider schemaProvider,
        ICaseLoggingService caseLogging,
        INormalizerService normalizerService,
        IPdfRenderingService pdfRenderingService,
        IImagesService imagesService,
        IPrecisionEditor precisionEditor,
        IRedTeamCacheService redTeamCache,
        IConfiguration configuration,
        ILogger<CaseGenerationService> logger)
    {
        _llmService = llmService;
        _schemaProvider = schemaProvider;
        _storageService = storageService;
        _schemaValidationService = schemaValidationService;
        _caseLogging = caseLogging;
        _normalizerService = normalizerService;
        _pdfRenderingService = pdfRenderingService;
        _imagesService = imagesService;
        _precisionEditor = precisionEditor;
        _redTeamCache = redTeamCache;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> PlanCaseAsync(CaseGenerationRequest request, string caseId, CancellationToken cancellationToken = default)
    {
        // Resolve difficulty/profile com fallback robusto
        var requestedDiff = request.Difficulty;
        var difficultyProfile = DifficultyLevels.GetProfile(requestedDiff);
        var actualDifficulty = request.Difficulty ?? DifficultyLevels.AllLevels[Random.Shared.Next(DifficultyLevels.AllLevels.Length)];

        _logger.LogInformation("PLAN: Auto-generating case with difficulty={Difficulty}, timezone={Timezone}, images={GenerateImages}", actualDifficulty, request.Timezone, request.GenerateImages);

        var systemPrompt = $@"
            You are a master architect of investigative cold cases. Produce a FULLY AUTOMATED INITIAL PLAN
            strictly aligned with the DIFFICULTY PROFILE and designed to be expanded without contradictions.

            DIFFICULTY PROFILE: {actualDifficulty}
            Description: {difficultyProfile?.Description}

            COMPLEXITY GUIDELINES (HARD CONSTRAINTS):
            - Suspects: {difficultyProfile?.Suspects.Min}-{difficultyProfile?.Suspects.Max}
            - Documents (later): {difficultyProfile?.Documents.Min}-{difficultyProfile?.Documents.Max}
            - Evidence items (later): {difficultyProfile?.Evidences.Min}-{difficultyProfile?.Evidences.Max}
            - False leads: {difficultyProfile?.RedHerrings}
            - Gated documents (later): {difficultyProfile?.GatedDocuments}
            - Forensics complexity: {difficultyProfile?.ForensicsComplexity}
            - Complexity factors: {string.Join(", ", difficultyProfile?.ComplexityFactors ?? Array.Empty<string>())}
            - Estimated duration: {difficultyProfile?.EstimatedDurationMinutes.Min}-{difficultyProfile?.EstimatedDurationMinutes.Max} minutes

            GEOGRAPHY/NAMING POLICY:
            - No real street names, numbers, coordinates, or real brands/companies.
            - If a real city is needed, limit to City/State only; prefer abstract locations (e.g., ""neighborhood bakery"").
            - All names must be plausible and fictitious.

            TEMPORAL CONSISTENCY (CRITICAL):
            - ALL timestamps MUST use ISO-8601 with timezone offset: {request.Timezone}
            - Establish a clear timeline baseline that will be consistently used across ALL documents and evidence
            - All timeline events must be chronologically ordered and logically spaced
            - NO conflicting or overlapping timestamps - each event must have a unique, consistent time
            - Document creation times, evidence collection times, and incident timestamps must align logically

            EXPANSION READINESS (NON-NEGOTIABLE):
            - mainElements[] must already anticipate the minimum entities that will exist later (e.g., ""witness statement"", ""key control log"", ""CCTV collage"", ""receipt"", etc.).
            - goldenTruth.facts must be verifiable by at least two heterogeneous later items (documents/media), without revealing any culprit.
            - learningObjectives must be concrete, observable, and testable during gameplay.

            OUTPUT: ONLY valid JSON conforming to the Plan schema. No comments or extra text.";


        var userPrompt = $@"
            Generate a FULLY AUTOMATED PLAN for a new investigative case.

            INPUT:
            - Difficulty: {actualDifficulty}
            - Generate images (metadata only): {request.GenerateImages}
            - Timezone for timestamps: {request.Timezone}

            MANDATORY CONTENT (Plan schema only):
            - Unique caseId, strong specific title, plausible abstract location, incidentType coherent with difficulty
            - overview that sets clear investigative scope (no solution)
            - learningObjectives[] directly tied to actions the player will perform
            - mainElements[] that pre-declare the core sources to be expanded (witness statements, logs, receipts, cctv snapshot collage, forensics inspection, etc.)
            - estimatedDuration aligned with difficulty
            - timeline[] canonical events with ISO-8601 + offset (use {request.Timezone}) - MUST be chronologically ordered with NO overlaps or conflicts
            - minDetectiveRank = {requestedDiff}
            - profileApplied with numeric ranges actually used
            - goldenTruth.facts with minSupports ≥ 2 and heterogeneous sources (do not reveal culprit/solution)

            CONSISTENCY RULES:
            - Names/roles introduced here must remain verbatim in later stages (Expand/Design).
            - Do not include any information that would later require real addresses/brands.
            - Avoid over-specifying details that would conflict with the difficulty (e.g., no advanced forensics on Rookie).

            OUTPUT FORMAT:
            - ONLY JSON valid by the **Plan** schema (no comments).";

        var jsonSchema = _schemaProvider.GetSchema("Plan");
        return await _llmService.GenerateStructuredAsync(caseId, systemPrompt, userPrompt, jsonSchema, cancellationToken);
    }

    public async Task<string> ExpandCaseAsync(string planJson, string caseId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("EXPAND: Building detailed case from plan");

        // Parse the plan to extract difficulty level
        var planData = JsonDocument.Parse(planJson);
        var difficulty = planData.RootElement.GetProperty("difficulty").GetString();
        var difficultyProfile = DifficultyLevels.GetProfile(difficulty);

        _logger.LogInformation("EXPAND: Building detailed case from plan with difficulty={Difficulty}", difficulty);

        var systemPrompt = $@"
            You are a specialist in developing investigative COLD CASES. Expand the initial plan
            into a coherent, contradiction-free structure strictly bound to the DIFFICULTY PROFILE.

            DIFFICULTY PROFILE: {difficulty}
            Description: {difficultyProfile.Description}

            EXPANSION HARD LIMITS:
            - Suspects: {difficultyProfile.Suspects.Min}-{difficultyProfile.Suspects.Max}
            - Evidence items: {difficultyProfile.Evidences.Min}-{difficultyProfile.Evidences.Max} (physical/digital/testimonial)
            - False leads: {difficultyProfile.RedHerrings} (subtle and plausible)
            - Forensics complexity: {difficultyProfile.ForensicsComplexity}
            - Complexity factors: {string.Join(", ", difficultyProfile.ComplexityFactors)}

            LEVEL GUIDANCE:
            - Rookie/Detective: linear chronology, direct indicators, modest ambiguity
            - Detective2/Sergeant+: cross-correlations, some gating prerequisites
            - Lieutenant/Captain/Commander: specialized analyses, layered dependencies

            TEMPORAL CONSISTENCY (MANDATORY):
            - PRESERVE all timeline events from Plan with exact same timestamps
            - ALL new timestamps MUST use the same timezone offset as established in Plan
            - Evidence collection times MUST logically follow incident times
            - Witness interview times MUST be chronologically consistent
            - Document creation dates MUST align with when they would realistically be created
            - NO timestamp conflicts - each event gets a unique, logical time slot
            
            STRICT CONSISTENCY:
            - Reuse names/roles introduced in Plan verbatim; DO NOT invent new real-world brands/addresses.
            - Every evidence/witness/suspect introduced here must be usable later in Design (docs or media).";

        var userPrompt = $@"
            Expand the following plan into detailed content honoring the difficulty and remaining expansion-ready:

            {planJson}

            YOU MUST DELIVER (within the Expand schema only):
            1) SUSPECTS:
            - Distinct backgrounds/motives/alibis proportional to difficulty
            - Keep names/roles stable (verbatim from Plan if present)
            2) EVIDENCE:
            - Physical, digital, and testimonial items
            - Each item must be precisely described so it can later become a document or media item (no vague items)
            3) TIMELINE:
            - Coherent, ISO-8601 with offset; link key events to suspects/witnesses/evidence when applicable
            4) WITNESSES:
            - Add statements with specificity and reliability ratings aligned to difficulty
            5) LOCATIONS:
            - Abstract or City/State only (no real addresses/brands)

            CROSS-STAGE GUARANTEES (NO EXCEPTIONS):
            - Every suspect OR witness must be representable later by at least one document (interview/witness_statement/memo).
            - Every evidence item here will later appear either as a mediaSpec (photo/document_scan/diagram) or be cited in at least one generated document.
            - Do NOT introduce content that cannot be legally/realistically depicted later.

            OUTPUT: ONLY valid JSON conforming to the Expand schema.";

        var jsonSchema = _schemaProvider.GetSchema("Expand");

        return await _llmService.GenerateStructuredAsync(caseId, systemPrompt, userPrompt, jsonSchema, cancellationToken);
    }

    public async Task<string> DesignCaseAsync(string planJson, string expandedJson, string caseId, string? difficulty = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Designing case structure");

        using var planDoc = JsonDocument.Parse(planJson);
        var planDifficulty = planDoc.RootElement.GetProperty("difficulty").GetString() ?? "Rookie";

        // Get the actual difficulty profile for dynamic validation
        var difficultyProfile = DifficultyLevels.GetProfile(difficulty ?? planDifficulty);

        (int minDocs, int maxDocs) = difficultyProfile.Documents;
        (int minEvid, int maxEvid) = difficultyProfile.Evidences;

        // Override with plan values if available (for backward compatibility)
        if (planDoc.RootElement.TryGetProperty("profileApplied", out var prof))
        {
            if (prof.TryGetProperty("documents", out var docsArr) && docsArr.ValueKind == JsonValueKind.Array && docsArr.GetArrayLength() == 2)
            { minDocs = docsArr[0].GetInt32(); maxDocs = docsArr[1].GetInt32(); }

            if (prof.TryGetProperty("evidences", out var evidArr) && evidArr.ValueKind == JsonValueKind.Array && evidArr.GetArrayLength() == 2)
            { minEvid = evidArr[0].GetInt32(); maxEvid = evidArr[1].GetInt32(); }
        }

        var gatedDocsCount = difficultyProfile.GatedDocuments;
        var minMedia = Math.Max(2, minEvid);
        var maxMedia = Math.Max(minMedia, maxEvid);

        var systemPrompt = @"
            You are an investigative case designer. Convert the plan and the expansion
            into structured specifications for parallel generation of documents and media.

            IMPORTANT (JSON ONLY conforming to the DocumentAndMediaSpecs schema):
            - All text in english
            - No explanations or comments outside the JSON.
            - Mark sensitive documents with ""gated"": true and include ""gatingRule"" { action, evidenceId?, notes? }.
            - Forensic reports MUST contain the ""Cadeia de Custódia"" (Chain of Custody) section.
            - Do not invent real addresses or real brands/companies (use abstract locations or City/State).
            - Allowed document types: police_report, interview, memo_admin, forensics_report, evidence_log, witness_statement
            - Allowed media types: photo, document_scan, diagram (audio/video => deferred=true)

            TEMPORAL SPECIFICATIONS (CRITICAL):
            - ALL document dateCreated and evidence collectedAt timestamps MUST be consistent with Expand timeline
            - Use the EXACT same timezone offset established in Expand for ALL timestamps
            - Document creation dates must logically follow the incident timeline (e.g., police reports after incident, interviews after initial report)
            - Evidence collection times must be realistic (not before incident, not weeks later without justification)
            - Interview timestamps must be chronologically ordered and consistent with case progression
            - NO overlapping or conflicting timestamps - each document/evidence gets a unique time slot

            MANDATORY CONSISTENCY:
            - Names (suspects/witnesses) and evidence must match 1:1 with Expand (same text/semantics).
            - Every suspect from Expand must have at least 1 document (interview, witness_statement or memo_admin).
            - Every witness from Expand must have at least 1 witness_statement.
            - Every evidence item from Expand must appear in mediaSpecs (scan/photo/diagram) OR be referenced by ID in at least one document.
            - IDs (docId/evidenceId) must be unique, stable, and mutually coherent.";

        var userPrompt = $@"
            Transform this case into structured specifications.

            Difficulty: {difficulty ?? planDifficulty}

            EXPANDED CONTEXT:
            {expandedJson}

            QUANTITY RULES (from difficulty profile):
            - Documents: {minDocs}-{maxDocs}
            - Media items: {minMedia}-{maxMedia}
            - Gated documents: exactly {gatedDocsCount} {(gatedDocsCount > 0 ? "(forensics_report required)" : "(gated usage forbidden)")}

            MINIMUM COVERAGE (NON-NEGOTIABLE):
            - Include at least 1 evidence_log and 1 police_report.
            - For EACH suspect from Expand: >=1 document (interview, witness_statement OR memo_admin).
            - For EACH witness from Expand: >=1 witness_statement.
            - For EACH evidence from Expand: create a corresponding mediaSpec OR explicitly reference the evidence in document(s).

            SPECIFIC RULES:
            {(gatedDocsCount > 0 ? @"- Exactly the indicated number of forensics_report with ""gated"": true.
            - Each ""gatingRule"" must be {{ ""action"": ""requires_evidence"" | ""submit_evidence"", ""evidenceId"": ""<id-existing-in-mediaSpecs>"" }}.
            - Forensic reports (including gated) must contain the ""Chain of Custody"" section." :
            @"- It is forbidden to use ""gated"": true at this level; do not include ""gatingRule"".
            - Forensic reports must still contain the ""Chain of Custody"" section.")}

            DOCUMENT REQUIREMENTS:
            - For each document: docId (""doc_<slug>_<nnn>""), type, title, sections[], lengthTarget[min,max], gated (bool)
            - If type == forensics_report, include ""Chain of Custody"" in sections
            - Reference evidence by evidenceId when relevant; do not reveal the solution

            MEDIA REQUIREMENTS:
            - For each media item: evidenceId (""ev_<slug>_<nnn>""), kind (photo/document_scan/diagram), title, prompt, constraints (OBJECT), deferred (bool)
            - audio/video: deferred=true
            - constraints must be a simple object (e.g. ""lighting"": ""raking"", ""scale"": true)

            SCHEMA COMPLIANCE:
            - Output ONLY valid JSON conforming to **DocumentAndMediaSpecs**.
            - Do not add fields outside the schema; unique IDs; names/evidence faithful to Expand.";

        var jsonSchema = _schemaProvider.GetSchema("DocumentAndMediaSpecs");
        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var response = await _llmService.GenerateStructuredAsync(caseId, systemPrompt, userPrompt, jsonSchema, cancellationToken);
                var validationResult = await _schemaValidationService.ParseAndValidateAsync(response, difficulty ?? planDifficulty);
                if (validationResult != null) return response;
                if (attempt == maxRetries) throw new InvalidOperationException("Design specs failed validation after retries");
            }
            catch when (attempt < maxRetries) { /* retry */ }
        }
        throw new InvalidOperationException("Failed to generate design specs");
    }

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

        // Type-specific directives (augmented with minimum referencing rules)
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
        var planCtx = planJson ?? "{}";
        var expandCtx = expandJson ?? "{}";

        var systemPrompt = @"
            You are a police / forensic technical writer. Generate ONLY JSON containing the document body.

            GENERAL RULES (MANDATORY):
            - Write all text in english.
            - Never reveal the solution or culprit.
            - Maintain consistency with Plan / Expand / Design.
            - **Use exactly the provided 'sections' titles and in the exact order. Do NOT add or rename sections.**
            - Follow exactly the word count range (lengthTarget).
            - If type == forensics_report, include the ""Chain of Custody"" section.
            - Whenever citing evidence, reference existing evidenceId/docId (do not invent).
            - Do not mention gating in the content (gating is game metadata).
            - No real PII, brands, or real addresses.

            TEMPORAL CONSISTENCY (CRITICAL):
            - ALL timestamps MUST use ISO-8601 format with the same timezone offset from Plan/Design
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
            - lengthTarget: {spec.LengthTarget[0]}–{spec.LengthTarget[1]} words
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

    public async Task<string> GenerateMediaFromSpecAsync(
        MediaSpec spec,
        string designJson,
        string caseId,
        string? planJson = null,
        string? expandJson = null,
        string? difficultyOverride = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Gen Media[{EvidenceId}] kind={Kind} title={Title}", spec.EvidenceId, spec.Kind, spec.Title);

        var systemPrompt = @"
            You are a generator of FORENSIC specifications for a single static media asset.
            Output: ONLY valid JSON with { evidenceId, kind, title, prompt, constraints }.

            SCOPE & SAFETY
            - One image only (a collage is acceptable if composed into a single bitmap); never a multi-image sequence.
            - Style: documentary photograph/screenshot, neutral, non-artistic.
            - Content 100% fictitious. Forbid real names, brands/logos, faces/biometrics, real license plates, official badges.
            - No graphic/violent content. Avoid people when the scene allows.
            - Do NOT write the evidence name or caseId inside the image.

            TEMPORAL CONSISTENCY (MANDATORY):
            - ALL timestamps in media MUST use the same timezone offset established in Plan/Design
            - CCTV timestamps MUST match the collectedAt time specified in Design for this evidence
            - Document scan dates MUST be consistent with when the document would realistically exist in the case timeline
            - NO conflicting timestamps - each piece of evidence has exactly one collection time that must be respected

            STRICT CONSISTENCY (NON-NEGOTIABLE)
            - Reflect ONLY the narrative elements already present in Plan/Expand/Design; do not invent new entities, locations, vehicles, or timestamps.
            - If the evidence exists in Expand/Design with specific attributes (time, camera label, object condition, location), MIRROR those details.
            - Use only existing labels already implied by the case (e.g., 'CAM-03', zone ids, door ids). No new camera IDs or zones.
            - Honor any provided initial_constraints (unless they violate safety), mapping them into the final constraints.

            TECHNICAL STANDARDIZATION
            - Always quantify: angle in degrees, camera height OR subject distance in meters, lens in mm, aperture f/, shutter 1/x s, ISO, white balance (K).
            - Lighting: diffuse/even; avoid harsh shadows; prefer color-neutral rendering.
            - For object-on-surface evidence: prefer 90° top-down when applicable.

            MANDATORY FIELD FORMAT
            - prompt: fixed sections in this exact order:
            1) Function (1 sentence)
            2) Scene / Composition (3–5 sentences; include subject percentage in frame)
            3) Angle & Distance (strict numeric values)
            4) Optics & Technique (lens mm, f/, 1/x s, ISO, WB K, focus, DOF)
            5) Mandatory elements (labels/markers/timestamps already present in the case)
            6) Negatives (objective list of what MUST NOT appear)
            7) Acceptance checklist (objective, verifiable bullets)
            - constraints: JSON object containing:
            - angle_deg (number)
            - camera_height_m OR distance_m (number; include exactly one of these keys)
            - aspect_ratio (string, e.g., ""16:9"", ""4:3"", ""A4"")
            - resolution_px (string, e.g., ""1920x1080"")
            - seed (7-digit integer as string)
            - deferred=false

            TYPE-SPECIFIC RULES
            - kind=cftv_frame:
            - Overlay timestamp ""YYYY-MM-DD HH:MM:SS"" (top-right, monospaced, white with 1–2px outline).
            - Camera label (e.g., ""CAM-03"") on frame.
            - Camera height 2.5–3.0 m; lens 2.8–4 mm equiv.; shutter 1/60 s; mild H.264 artifacts; slight coherent motion blur.
            - kind=document_scan / receipt:
            - Perspective corrected; 300–600 DPI equivalent; visible margins; no fingers, no moiré; crop clean edges.
            - kind=scene_topdown:
            - 90° orthographic top-down; keep edges parallel to frame; include simple scale reference ONLY if already implied by case (no rulers).

            DO NOT
            - Do not repeat the rulebook in prose; translate rules into concrete parameters.
            - Do not return Markdown, comments, or extra fields.

            PRE-SUBMISSION SELF-CHECK (must be satisfied before answering)
            - Are all referenced labels/timestamps present in the supplied case context?
            - Are prompt sections exactly in the required order (1–7) and fully populated?
            - Does constraints include angle_deg and exactly one of camera_height_m or distance_m, plus aspect_ratio, resolution_px, seed (7 digits), deferred=false?
            - Is this a single image (no sequence)?";

        var userPrompt = $@"
            CONTEXT (only the minimal, relevant parts — do NOT restate everything)
            {designJson}

            EVIDENCE SPECIFICATION
            - evidenceId: {spec.EvidenceId}
            - kind: {spec.Kind}
            - title: {spec.Title}
            - difficulty: {(difficultyOverride ?? "Detective")}
            - initial_constraints: {(spec.Constraints != null && spec.Constraints.Any() ? string.Join(", ", spec.Constraints.Select(kv => $"{kv.Key}: {kv.Value}")) : "n/a")}
            - language: en-US

            LEVEL OF DETAIL BY DIFFICULTY (apply WITHOUT creating a sequence):
            - Rookie: simple composition; prefer top-down when applicable.
            - Detective/Detective2: add one contextual detail within the same frame.
            - Sergeant+: wider context PLUS a relevant local detail in the same frame (no multi-image montage); subtle false-positive ONLY if consistent with the case.
            - Captain/Commander: include plausible control objects (e.g., neutral color/gray card) only if consistent with operational reality for that scene.

            HARD REQUIREMENTS FOR THIS OUTPUT
            - Return ONLY valid JSON with: evidenceId, kind, title, prompt, constraints.
            - The prompt MUST follow the 7 fixed sections in the exact order and include measurable values.
            - constraints MUST include: angle_deg, camera_height_m OR distance_m (exactly one), aspect_ratio, resolution_px, seed (7 digits), deferred=false.
            - If kind=cftv_frame: put timestamp overlay ""YYYY-MM-DD HH:MM:SS"" (top-right) and a camera label like ""CAM-03"".
            - Respect initial_constraints when provided (unless unsafe/contradictory).
            - Do NOT invent new entities, labels, brands, or times.
            ";

        var json = await _llmService.GenerateAsync(caseId, systemPrompt, userPrompt, cancellationToken);

        try
        {
            await _caseLogging.LogStepResponseAsync(caseId, $"media/{spec.EvidenceId}", json, cancellationToken);

            var bundlesContainer = _configuration["CaseGeneratorStorage:BundlesContainer"] ?? "bundles";
            var docBundlePath = $"{caseId}/media/{spec.EvidenceId}.json";
            await _storageService.SaveFileAsync(bundlesContainer, docBundlePath, json, cancellationToken);

            _logger.LogInformation("BUNDLE: Saved media to bundle: {Path} (case={CaseId},  kind={Kind})",
                docBundlePath, caseId, spec.Kind);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist generated media {DocId} for case {CaseId}",
                spec.EvidenceId, caseId);
        }

        return json;
    }


    public async Task<string> RenderDocumentFromJsonAsync(string docId, string documentJson, string caseId, CancellationToken cancellationToken = default)
    {
        return await _pdfRenderingService.RenderDocumentFromJsonAsync(docId, documentJson, caseId, cancellationToken);
    }

    public async Task<string> RenderMediaFromJsonAsync(MediaSpec spec, string caseId, CancellationToken cancellationToken = default)
    {
        return await _imagesService.GenerateAsync(caseId, spec);
    }

    public async Task<NormalizationResult> NormalizeCaseDeterministicAsync(NormalizationInput input, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Normalizing case content deterministically for case {CaseId}", input.CaseId);
        return await _normalizerService.NormalizeCaseAsync(input, cancellationToken);
    }

    public async Task<string> ValidateRulesAsync(string normalizedJson, string caseId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Validating case rules");

        var systemPrompt = """
            You are a specialist in validating detective game cases. 
            Verify that the case complies with gameplay, narrative consistency, and quality standards.
            
            Pay special attention to TEMPORAL CONSISTENCY:
            - All timestamps must use consistent timezone offset throughout the case
            - Document creation dates must logically follow incident timeline  
            - Evidence collection times must be realistic and chronologically sound
            - Interview timestamps must be properly sequenced
            - No overlapping or conflicting timestamps between documents/evidence
            - Chain of custody timestamps must be chronologically ordered
            """;

        var userPrompt = $"""
            Validate this normalized case against the defined quality rules:
            
            {normalizedJson}
            
            Check: 
            1. TEMPORAL CONSISTENCY: Verify all timestamps use consistent timezone, are chronologically logical, and have no conflicts
            2. Narrative consistency and logical flow
            3. Gameplay balance and challenge level
            4. Completeness of clues and evidence
            5. Realism and authenticity
            6. Overall case quality and solvability
            
            Flag any timestamp inconsistencies, timezone mismatches, or chronological errors as critical issues.
            """;

        return await _llmService.GenerateAsync(caseId, systemPrompt, userPrompt, cancellationToken);
    }

    [Obsolete("Use RedTeamGlobalAnalysisAsync and RedTeamFocusedAnalysisAsync for hierarchical analysis instead")]
    public async Task<string> RedTeamCaseAsync(string validatedJson, string caseId, CancellationToken cancellationToken = default)
    {
        return await RedTeamCaseChunkedAsync(validatedJson, caseId, cancellationToken);
    }

    public async Task<string> RedTeamGlobalAnalysisAsync(string validatedJson, string caseId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("REDTEAM GLOBAL: Starting macro analysis for case {CaseId}", caseId);
        
        if (string.IsNullOrWhiteSpace(validatedJson))
        {
            _logger.LogError("REDTEAM GLOBAL: Empty or null validatedJson provided for case {CaseId}", caseId);
            return CreateFallbackGlobalAnalysis("Global analysis failed - empty input JSON provided.");
        }

        // Check cache first
        var contentHash = _redTeamCache.ComputeContentHash(validatedJson);
        var cachedAnalysis = await _redTeamCache.GetCachedAnalysisAsync(contentHash, "Global", null, cancellationToken);
        
        if (cachedAnalysis != null)
        {
            _logger.LogInformation("REDTEAM GLOBAL: Using cached analysis for case {CaseId} (hash: {Hash})", 
                caseId, contentHash[..8]);
            return cachedAnalysis;
        }

        // Log input size for monitoring
        _logger.LogInformation("REDTEAM GLOBAL: Input JSON size: {Size} bytes for case {CaseId}", 
            validatedJson.Length, caseId);

        var systemPrompt = """
            You are a senior forensic case analyst conducting a high-level strategic assessment of a complete forensic case.
            Your goal is to identify MACRO-LEVEL issues that affect the case's overall integrity and coherence.

            Focus on:
            1. CROSS-DOCUMENT INCONSISTENCIES: Contradictions between different documents
            2. CHRONOLOGICAL PROBLEMS: Timeline gaps, impossible sequences, temporal contradictions
            3. NARRATIVE COHERENCE: Story elements that don't align across the case
            4. REFERENCE INTEGRITY: Missing or broken cross-references between documents
            5. STRUCTURAL COMPLETENESS: Missing critical elements or documents

            Do NOT focus on:
            - Minor formatting issues
            - Small textual errors
            - Document-specific content problems
            - Individual timestamp corrections

            Provide a strategic assessment that identifies which areas need detailed focused analysis.
            """;

        var jsonStructure = """
            {
                "MacroIssues": [
                    {
                        "Type": "CrossDocumentInconsistency|ChronologicalGap|NarrativeContradiction|ReferenceIntegrity|StructuralCompleteness",
                        "Severity": "Critical|Major|Minor",
                        "AffectedDocuments": ["doc_id_1", "doc_id_2"],
                        "Description": "Clear description of the macro issue",
                        "RequiredFocusAreas": ["specific_section_1", "specific_field_2"]
                    }
                ],
                "CriticalDocuments": ["doc_ids_needing_detailed_analysis"],
                "FocusAreas": ["specific_areas_to_examine_in_detail"],
                "OverallAssessment": "Strategic assessment of case quality",
                "RequiresDetailedAnalysis": true
            }
            """;

        var userPrompt = $"""
            Analyze this complete forensic case for macro-level issues. Return your analysis as valid JSON:

            {validatedJson}

            Required JSON structure:
            {jsonStructure}
            """;

        try
        {
            var result = await _llmService.GenerateAsync(caseId, systemPrompt, userPrompt, cancellationToken);
            
            if (string.IsNullOrWhiteSpace(result))
            {
                _logger.LogWarning("REDTEAM GLOBAL: Empty response received for case {CaseId}", caseId);
                return CreateFallbackGlobalAnalysis("Global analysis failed - empty response received from LLM.");
            }

            // Clean and validate JSON
            var cleanedResult = CleanRedTeamJsonResponse(result);
            
            // Try to parse to validate structure
            try
            {
                var analysis = JsonSerializer.Deserialize<GlobalRedTeamAnalysis>(cleanedResult);
                if (analysis != null)
                {
                    _logger.LogInformation("REDTEAM GLOBAL: Completed - {MacroIssueCount} macro issues found", 
                        analysis.MacroIssues.Count);
                    
                    // Cache the successful analysis
                    await _redTeamCache.CacheAnalysisAsync(contentHash, cleanedResult, "Global", null, cancellationToken);
                    
                    return cleanedResult;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "REDTEAM GLOBAL: Invalid JSON structure returned for case {CaseId}", caseId);
            }

            // Cache even if parsing failed - let downstream handle it, but avoid re-processing
            await _redTeamCache.CacheAnalysisAsync(contentHash, cleanedResult, "Global", null, cancellationToken);
            
            return cleanedResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "REDTEAM GLOBAL: Exception during analysis for case {CaseId}", caseId);
            return CreateFallbackGlobalAnalysis($"Global analysis failed with exception: {ex.Message}");
        }
    }

    public async Task<string> RedTeamFocusedAnalysisAsync(string validatedJson, string caseId, string globalAnalysis, string[] focusAreas, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("REDTEAM FOCUSED: Starting detailed analysis for case {CaseId} on {FocusAreaCount} areas", 
            caseId, focusAreas.Length);
        
        if (string.IsNullOrWhiteSpace(validatedJson))
        {
            _logger.LogError("REDTEAM FOCUSED: Empty or null validatedJson provided for case {CaseId}", caseId);
            return CreateFallbackStructuredAnalysis("Focused analysis failed - empty input JSON provided.");
        }

        // Check cache first - for focused analysis, we include focus areas in the cache key
        var contentHash = _redTeamCache.ComputeContentHash(validatedJson + globalAnalysis);
        var cachedAnalysis = await _redTeamCache.GetCachedAnalysisAsync(contentHash, "Focused", focusAreas, cancellationToken);
        
        if (cachedAnalysis != null)
        {
            _logger.LogInformation("REDTEAM FOCUSED: Using cached analysis for case {CaseId} (hash: {Hash})", 
                caseId, contentHash[..8]);
            return cachedAnalysis;
        }

        // Use existing chunked analysis but with focused prompting
        var result = await RedTeamCaseChunkedAsync(validatedJson, caseId, cancellationToken, globalAnalysis, focusAreas);
        
        // Cache the result if successful
        if (!string.IsNullOrWhiteSpace(result))
        {
            await _redTeamCache.CacheAnalysisAsync(contentHash, result, "Focused", focusAreas, cancellationToken);
        }
        
        return result;
    }

    public async Task<string> RedTeamCaseChunkedAsync(string validatedJson, string caseId, CancellationToken cancellationToken = default)
    {
        return await RedTeamCaseChunkedAsync(validatedJson, caseId, cancellationToken, null, null);
    }

    public async Task<string> RedTeamCaseChunkedAsync(string validatedJson, string caseId, CancellationToken cancellationToken = default, string? globalAnalysis = null, string[]? focusAreas = null)
    {
        var analysisType = globalAnalysis != null ? "FOCUSED" : "STANDARD";
        _logger.LogInformation("PRECISION REDTEAM CHUNKED: Starting structured {AnalysisType} analysis for case {CaseId}", analysisType, caseId);
        
        // Input validation
        if (string.IsNullOrWhiteSpace(validatedJson))
        {
            _logger.LogError("PRECISION REDTEAM CHUNKED: Empty or null validatedJson provided for case {CaseId}", caseId);
            return CreateFallbackStructuredAnalysis("RedTeam analysis failed - empty input JSON provided.");
        }

        var inputSize = System.Text.Encoding.UTF8.GetByteCount(validatedJson);
        _logger.LogInformation("PRECISION REDTEAM CHUNKED: Input JSON size: {InputSize} bytes for case {CaseId}", inputSize, caseId);

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(validatedJson);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "PRECISION REDTEAM CHUNKED: Invalid input JSON for case {CaseId}", caseId);
            return CreateFallbackStructuredAnalysis($"RedTeam analysis failed - invalid input JSON: {ex.Message}");
        }

        using (doc)
        {
            // Step 1: Build skeleton and indexes
            var (skeletonJson, docMap, mediaMap) = BuildSkeletonAndIndexes(doc.RootElement);
            _logger.LogInformation("PRECISION REDTEAM CHUNKED: Built skeleton and indexes - {DocCount} docs, {MediaCount} media", 
                docMap.Count, mediaMap.Count);

            // Step 2: Read configuration
            var maxBytesPerCall = _configuration.GetValue("CaseGenerator:RedTeam:MaxBytesPerCall", DefaultMaxBytesPerCall);
            _logger.LogInformation("PRECISION REDTEAM CHUNKED: Using maxBytesPerCall={MaxBytes}", maxBytesPerCall);

            // Step 3: Plan chunks
            var chunks = PlanChunks(docMap, mediaMap, maxBytesPerCall, skeletonJson);
            _logger.LogInformation("PRECISION REDTEAM CHUNKED: Planned {ChunkCount} chunks", chunks.Count);

            // Step 4: Process chunks with limited parallelism
            var semaphore = new SemaphoreSlim(MaxParallelCalls, MaxParallelCalls);
            var tasks = chunks.Select(async chunk =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    return await RunRedTeamOnChunkAsync(chunk, skeletonJson, docMap, mediaMap, caseId, cancellationToken, globalAnalysis, focusAreas);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var analyses = await Task.WhenAll(tasks);
            _logger.LogInformation("PRECISION REDTEAM CHUNKED: Processed all {ChunkCount} chunks", chunks.Count);

            // Step 5: Merge analyses
            var mergedAnalysis = MergeAnalyses(analyses);
            _logger.LogInformation("PRECISION REDTEAM CHUNKED: Merged analysis - {IssueCount} total issues", 
                mergedAnalysis.Issues.Count);

            // Step 6: Serialize and return
            var result = JsonSerializer.Serialize(mergedAnalysis, new JsonSerializerOptions { WriteIndented = true });
            _logger.LogInformation("PRECISION REDTEAM CHUNKED: Completed analysis for case {CaseId}: {ResultLength} chars", 
                caseId, result.Length);
            
            return result;
        }
    }

    #region RedTeam Chunked Processing Helpers

    private (string skeletonJson, Dictionary<string, JsonElement> docMap, Dictionary<string, JsonElement> mediaMap) 
        BuildSkeletonAndIndexes(JsonElement root)
    {
        var docMap = new Dictionary<string, JsonElement>();
        var mediaMap = new Dictionary<string, JsonElement>();
        var timestamps = new HashSet<string>();

        // Extract timezone and difficulty
        var timezone = root.TryGetProperty("timezone", out var tzProp) ? tzProp.GetString() : "UTC";
        var difficulty = root.TryGetProperty("difficulty", out var diffProp) ? diffProp.GetString() : "Rookie";

        // Build document map and collect timestamps
        if (root.TryGetProperty("documents", out var docsArray))
        {
            foreach (var doc in docsArray.EnumerateArray())
            {
                if (doc.TryGetProperty("docId", out var docIdProp))
                {
                    var docId = docIdProp.GetString();
                    if (!string.IsNullOrEmpty(docId))
                    {
                        docMap[docId] = doc;
                        
                        // Collect timestamps from document
                        if (doc.TryGetProperty("createdAt", out var createdProp))
                        {
                            var created = createdProp.GetString();
                            if (IsTimestamp(created)) timestamps.Add(created!);
                        }
                        if (doc.TryGetProperty("modifiedAt", out var modifiedProp))
                        {
                            var modified = modifiedProp.GetString();
                            if (IsTimestamp(modified)) timestamps.Add(modified!);
                        }
                    }
                }
            }
        }

        // Build media map and collect timestamps
        if (root.TryGetProperty("media", out var mediaArray))
        {
            foreach (var media in mediaArray.EnumerateArray())
            {
                if (media.TryGetProperty("evidenceId", out var evidenceIdProp))
                {
                    var evidenceId = evidenceIdProp.GetString();
                    if (!string.IsNullOrEmpty(evidenceId))
                    {
                        mediaMap[evidenceId] = media;
                        
                        // Collect timestamps from media
                        if (media.TryGetProperty("collectedAt", out var collectedProp))
                        {
                            var collected = collectedProp.GetString();
                            if (IsTimestamp(collected)) timestamps.Add(collected!);
                        }
                    }
                }
            }
        }

        // Build skeleton
        var skeleton = new
        {
            timezone = timezone,
            difficulty = difficulty,
            indexes = new
            {
                docIds = docMap.Keys.ToArray(),
                evidenceIds = mediaMap.Keys.ToArray()
            },
            temporalLedger = timestamps.Take(50).ToArray() // Limit to prevent growth
        };

        var skeletonJson = JsonSerializer.Serialize(skeleton, new JsonSerializerOptions { WriteIndented = true });
        return (skeletonJson, docMap, mediaMap);
    }

    private bool IsTimestamp(string? s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        
        // Simple ISO-8601 validation with timezone
        var isoPattern = @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})$";
        return Regex.IsMatch(s, isoPattern);
    }

    private List<RedTeamChunkSpec> PlanChunks(
        Dictionary<string, JsonElement> docMap, 
        Dictionary<string, JsonElement> mediaMap, 
        int maxBytesPerCall, 
        string skeletonJson)
    {
        var chunks = new List<RedTeamChunkSpec>();
        var skeletonBytes = Encoding.UTF8.GetByteCount(skeletonJson);
        var availableBytes = maxBytesPerCall - skeletonBytes - 1000; // Reserve for prompt overhead

        if (availableBytes <= 0)
        {
            _logger.LogWarning("CHUNKING: Skeleton too large, using minimal chunks");
            availableBytes = 5000; // Fallback
        }

        var currentDocIds = new List<string>();
        var currentEvidenceIds = new List<string>();
        var currentBytes = 0;

        // Add documents to chunks
        foreach (var (docId, docElement) in docMap)
        {
            var docBytes = Encoding.UTF8.GetByteCount(docElement.GetRawText());
            
            if (currentBytes + docBytes > availableBytes && currentDocIds.Count > 0)
            {
                // Create chunk and reset
                chunks.Add(new RedTeamChunkSpec
                {
                    DocIds = currentDocIds.ToArray(),
                    EvidenceIds = currentEvidenceIds.ToArray(),
                    EstimatedBytes = currentBytes + skeletonBytes
                });
                
                currentDocIds.Clear();
                currentEvidenceIds.Clear();
                currentBytes = 0;
            }
            
            currentDocIds.Add(docId);
            currentBytes += docBytes;
        }

        // Add media to current or new chunks
        foreach (var (evidenceId, mediaElement) in mediaMap)
        {
            var mediaBytes = Encoding.UTF8.GetByteCount(mediaElement.GetRawText());
            
            if (currentBytes + mediaBytes > availableBytes && (currentDocIds.Count > 0 || currentEvidenceIds.Count > 0))
            {
                // Create chunk and reset
                chunks.Add(new RedTeamChunkSpec
                {
                    DocIds = currentDocIds.ToArray(),
                    EvidenceIds = currentEvidenceIds.ToArray(),
                    EstimatedBytes = currentBytes + skeletonBytes
                });
                
                currentDocIds.Clear();
                currentEvidenceIds.Clear();
                currentBytes = 0;
            }
            
            currentEvidenceIds.Add(evidenceId);
            currentBytes += mediaBytes;
        }

        // Add final chunk if needed
        if (currentDocIds.Count > 0 || currentEvidenceIds.Count > 0)
        {
            chunks.Add(new RedTeamChunkSpec
            {
                DocIds = currentDocIds.ToArray(),
                EvidenceIds = currentEvidenceIds.ToArray(),
                EstimatedBytes = currentBytes + skeletonBytes
            });
        }

        // Ensure at least one chunk exists
        if (chunks.Count == 0)
        {
            chunks.Add(new RedTeamChunkSpec
            {
                DocIds = Array.Empty<string>(),
                EvidenceIds = Array.Empty<string>(),
                EstimatedBytes = skeletonBytes
            });
        }

        return chunks;
    }

    private async Task<StructuredRedTeamAnalysis> RunRedTeamOnChunkAsync(
        RedTeamChunkSpec chunk,
        string skeletonJson,
        Dictionary<string, JsonElement> docMap,
        Dictionary<string, JsonElement> mediaMap,
        string caseId,
        CancellationToken cancellationToken,
        string? globalAnalysis = null,
        string[]? focusAreas = null)
    {
        _logger.LogInformation("REDTEAM CHUNK: Processing {DocCount} docs, {MediaCount} media ({EstimatedBytes} bytes)",
            chunk.DocIds.Length, chunk.EvidenceIds.Length, chunk.EstimatedBytes);

        // Build scoped JSON with skeleton + chunk data
        var scopedData = new
        {
            skeleton = JsonSerializer.Deserialize<object>(skeletonJson),
            documents = chunk.DocIds.Select(id => docMap.TryGetValue(id, out var doc) ? doc : (JsonElement?)null)
                                   .Where(d => d.HasValue)
                                   .Select(d => JsonSerializer.Deserialize<object>(d!.Value.GetRawText()))
                                   .ToArray(),
            media = chunk.EvidenceIds.Select(id => mediaMap.TryGetValue(id, out var media) ? media : (JsonElement?)null)
                                     .Where(m => m.HasValue)
                                     .Select(m => JsonSerializer.Deserialize<object>(m!.Value.GetRawText()))
                                     .ToArray()
        };

        var scopedJson = JsonSerializer.Serialize(scopedData, new JsonSerializerOptions { WriteIndented = true });

        // Build context-aware system prompt
        var contextualPrompt = globalAnalysis != null 
            ? $"""
            You are a precision red team specialist for police investigative training content.
            Analyze ONLY the documents and media provided in this specific chunk scope.
            
            GLOBAL CONTEXT PROVIDED:
            You have access to macro-level analysis that identified these key issues:
            {globalAnalysis}
            
            FOCUSED ANALYSIS AREAS:
            {(focusAreas?.Length > 0 ? string.Join(", ", focusAreas) : "Standard analysis")}
            
            CRITICAL MISSION:
            - Use the global context to inform your detailed analysis
            - Focus especially on areas identified in the global analysis
            - Identify problems with SURGICAL PRECISION within the provided scope only
            - Specify EXACT document IDs, field paths, and problematic values
            - Provide SPECIFIC fix instructions for each issue
            - Prioritize issues that relate to the macro-level problems identified
            """
            : """
            You are a precision red team specialist for police investigative training content. 
            Analyze ONLY the documents and media provided in this specific chunk scope.
            
            CRITICAL MISSION:
            - Identify problems with SURGICAL PRECISION within the provided scope only
            - Specify EXACT document IDs, field paths, and problematic values
            - Provide SPECIFIC fix instructions for each issue
            - Focus on temporal inconsistencies as highest priority
            """;

        var systemPrompt = $$$"""
            {{{contextualPrompt}}}
            
            CRITICAL JSON FORMAT REQUIREMENTS:
            - ALL string fields must be valid JSON strings (no arrays in string fields)
            - CurrentValue must be a single string, not an array
            - If you need to reference multiple values, use comma-separated strings
            - Do not use arrays where strings are expected
            
            OUTPUT FORMAT: Return ONLY valid JSON matching this EXACT structure:
            {{
                "Issues": [
                    {{
                        "Priority": "High|Medium|Low",
                        "Type": "TimestampConflict|PostCreationReference|ChronologicalGap|etc",
                        "Problem": "Clear description of the problem",
                        "Location": {{
                            "DocId": "document_id",
                            "Field": "field_path",
                            "Section": "section_name",
                            "LinePattern": "text_pattern_to_find",
                            "CurrentValue": "current_problematic_value"
                        }},
                        "Fix": {{
                            "Action": "UpdateTimestamp|ReplaceText|MoveToAddendum|RemoveReference|AddMediaAttachment|GenerateMissingDocument",
                            "NewValue": "new_value_to_set",
                            "OldText": "text_to_replace",
                            "NewText": "replacement_text", 
                            "Reason": "explanation_why_fix_needed"
                        }}
                    }}
                ],
                "Summary": "Brief summary of all issues found",
                "HighPriorityCount": 0,
                "MediumPriorityCount": 0,
                "LowPriorityCount": 0
            }}
            
            PRIORITY GUIDELINES:
            - High: Timestamp conflicts, chronological impossibilities
            - Medium: Timeline gaps, missing context
            - Low: Minor wording, style improvements
            """;

        var userPrompt = $"""
            Analyze this chunk scope and identify specific problems with exact locations:
            
            CHUNK SCOPE JSON:
            {scopedJson}
            
            FIND AND SPECIFY (within this scope only):
            1. EXACT document IDs where problems occur
            2. SPECIFIC fields or text patterns that are problematic  
            3. CURRENT values that need to be changed
            4. PRECISE fix instructions (new timestamps, replacement text, etc.)
            
            Focus especially on:
            - Evidence compilation times vs collection times
            - Report creation times vs referenced events
            - Chronological order conflicts
            - Timeline consistency within this chunk
            
            Return structured JSON analysis with surgical precision.
            """;

        string? result = null;
        try
        {
            result = await _llmService.GenerateAsync(caseId, systemPrompt, userPrompt, cancellationToken);
            
            if (string.IsNullOrWhiteSpace(result))
            {
                _logger.LogWarning("REDTEAM CHUNK: Empty response for chunk, returning empty analysis");
                return new StructuredRedTeamAnalysis
                {
                    Issues = new List<PreciseIssue>(),
                    Summary = "Empty response for chunk",
                    HighPriorityCount = 0,
                    MediumPriorityCount = 0,
                    LowPriorityCount = 0
                };
            }

            // Try to clean and fix the JSON before deserializing
            var cleanedResult = CleanRedTeamJsonResponse(result);
            
            var analysis = JsonSerializer.Deserialize<StructuredRedTeamAnalysis>(cleanedResult);
            if (analysis != null)
            {
                _logger.LogInformation("REDTEAM CHUNK: Completed - {IssueCount} issues found", analysis.Issues.Count);
                return analysis;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "REDTEAM CHUNK: Invalid JSON response, returning empty analysis. Response preview: {Preview}", 
                result?.Length > 500 ? result[..500] + "..." : result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "REDTEAM CHUNK: Exception during processing, returning empty analysis");
        }

        return new StructuredRedTeamAnalysis
        {
            Issues = new List<PreciseIssue>(),
            Summary = "Chunk processing failed",
            HighPriorityCount = 0,
            MediumPriorityCount = 0,
            LowPriorityCount = 0
        };
    }

    private StructuredRedTeamAnalysis MergeAnalyses(IEnumerable<StructuredRedTeamAnalysis> analyses)
    {
        var allIssues = new List<PreciseIssue>();
        var summaries = new List<string>();
        int totalHigh = 0, totalMedium = 0, totalLow = 0;

        foreach (var analysis in analyses)
        {
            allIssues.AddRange(analysis.Issues);
            if (!string.IsNullOrWhiteSpace(analysis.Summary))
            {
                summaries.Add(analysis.Summary);
            }
            totalHigh += analysis.HighPriorityCount;
            totalMedium += analysis.MediumPriorityCount;
            totalLow += analysis.LowPriorityCount;
        }

        // Dedupe issues by key combination
        var deduped = allIssues
            .GroupBy(issue => new 
            { 
                issue.Location.DocId, 
                issue.Location.Field, 
                issue.Location.Section, 
                issue.Location.LinePattern 
            })
            .Select(g => g.First())
            .ToList();

        // Recalculate counts from deduped issues
        var actualHigh = deduped.Count(i => i.Priority == "High");
        var actualMedium = deduped.Count(i => i.Priority == "Medium");
        var actualLow = deduped.Count(i => i.Priority == "Low");

        var mergedSummary = summaries.Count > 0 
            ? $"Merged analysis from {summaries.Count} chunks: {string.Join("; ", summaries.Take(3))}"
            : "No issues found in any chunks";

        return new StructuredRedTeamAnalysis
        {
            Issues = deduped,
            Summary = mergedSummary,
            HighPriorityCount = actualHigh,
            MediumPriorityCount = actualMedium,
            LowPriorityCount = actualLow
        };
    }

    private string CleanRedTeamJsonResponse(string jsonResponse)
    {
        try
        {
            // Parse as JsonDocument to inspect and fix structural issues
            using var doc = JsonDocument.Parse(jsonResponse);
            
            // Check if we have the expected structure
            if (!doc.RootElement.TryGetProperty("Issues", out var issuesArray))
            {
                _logger.LogWarning("REDTEAM CHUNK: Response missing 'Issues' property");
                return jsonResponse; // Return as-is if basic structure is missing
            }

            // Create a mutable structure to fix issues
            var mutableResponse = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonResponse)!;
            
            if (mutableResponse.TryGetValue("Issues", out var issuesObj) && issuesObj is JsonElement issuesElement)
            {
                var fixedIssues = new List<object>();
                
                foreach (var issue in issuesElement.EnumerateArray())
                {
                    var fixedIssue = FixIssueStructure(issue);
                    if (fixedIssue != null)
                    {
                        fixedIssues.Add(fixedIssue);
                    }
                }
                
                mutableResponse["Issues"] = fixedIssues;
            }

            // Serialize back to clean JSON
            var cleanedJson = JsonSerializer.Serialize(mutableResponse, new JsonSerializerOptions { WriteIndented = false });
            return cleanedJson;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "REDTEAM CHUNK: Failed to clean JSON response, returning original");
            return jsonResponse;
        }
    }

    private object? FixIssueStructure(JsonElement issueElement)
    {
        try
        {
            var issueDict = new Dictionary<string, object>();
            
            // Copy basic properties
            if (issueElement.TryGetProperty("Priority", out var priority))
                issueDict["Priority"] = priority.GetString() ?? "Low";
            
            if (issueElement.TryGetProperty("Type", out var type))
                issueDict["Type"] = type.GetString() ?? "Unknown";
            
            if (issueElement.TryGetProperty("Problem", out var problem))
                issueDict["Problem"] = problem.GetString() ?? "Unknown issue";

            // Fix Location structure
            if (issueElement.TryGetProperty("Location", out var location))
            {
                var locationDict = new Dictionary<string, object?>();
                
                if (location.TryGetProperty("DocId", out var docId))
                    locationDict["DocId"] = docId.GetString() ?? "";
                
                if (location.TryGetProperty("Field", out var field))
                    locationDict["Field"] = field.GetString();
                
                if (location.TryGetProperty("Section", out var section))
                    locationDict["Section"] = section.GetString();
                
                if (location.TryGetProperty("LinePattern", out var linePattern))
                    locationDict["LinePattern"] = linePattern.GetString();
                
                // Fix CurrentValue - convert arrays to strings
                if (location.TryGetProperty("CurrentValue", out var currentValue))
                {
                    if (currentValue.ValueKind == JsonValueKind.Array)
                    {
                        // Convert array to comma-separated string
                        var arrayValues = new List<string>();
                        foreach (var item in currentValue.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String)
                                arrayValues.Add(item.GetString() ?? "");
                        }
                        locationDict["CurrentValue"] = string.Join(", ", arrayValues);
                    }
                    else if (currentValue.ValueKind == JsonValueKind.String)
                    {
                        locationDict["CurrentValue"] = currentValue.GetString();
                    }
                    else
                    {
                        locationDict["CurrentValue"] = currentValue.GetRawText();
                    }
                }
                
                issueDict["Location"] = locationDict;
            }

            // Fix Fix structure
            if (issueElement.TryGetProperty("Fix", out var fix))
            {
                var fixDict = new Dictionary<string, object?>();
                
                if (fix.TryGetProperty("Action", out var action))
                    fixDict["Action"] = action.GetString() ?? "ReplaceText";
                
                if (fix.TryGetProperty("NewValue", out var newValue))
                    fixDict["NewValue"] = newValue.GetString();
                
                if (fix.TryGetProperty("OldText", out var oldText))
                    fixDict["OldText"] = oldText.GetString();
                
                if (fix.TryGetProperty("NewText", out var newText))
                    fixDict["NewText"] = newText.GetString();
                
                if (fix.TryGetProperty("Reason", out var reason))
                    fixDict["Reason"] = reason.GetString();
                
                issueDict["Fix"] = fixDict;
            }

            return issueDict;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "REDTEAM CHUNK: Failed to fix issue structure, skipping issue");
            return null;
        }
    }

    #endregion

    private string CreateFallbackStructuredAnalysis(string errorMessage)
    {
        _logger.LogWarning("PRECISION REDTEAM: Creating fallback analysis due to: {ErrorMessage}", errorMessage);
        
        var fallback = new StructuredRedTeamAnalysis
        {
            Issues = new List<PreciseIssue>(),
            Summary = $"FALLBACK ANALYSIS: {errorMessage} - No specific issues identified due to service failure.",
            HighPriorityCount = 0,
            MediumPriorityCount = 0,
            LowPriorityCount = 0
        };
        
        var serializedFallback = JsonSerializer.Serialize(fallback, new JsonSerializerOptions { WriteIndented = true });
        _logger.LogInformation("PRECISION REDTEAM: Generated fallback analysis: {FallbackLength} characters", serializedFallback.Length);
        
        return serializedFallback;
    }

    private string CreateFallbackGlobalAnalysis(string errorMessage)
    {
        _logger.LogWarning("REDTEAM GLOBAL: Creating fallback global analysis due to: {ErrorMessage}", errorMessage);
        
        var fallback = new GlobalRedTeamAnalysis
        {
            MacroIssues = new List<MacroIssue>(),
            CriticalDocuments = Array.Empty<string>(),
            FocusAreas = Array.Empty<string>(),
            OverallAssessment = $"FALLBACK GLOBAL ANALYSIS: {errorMessage} - Unable to perform macro-level assessment due to service failure.",
            RequiresDetailedAnalysis = false // Safe default when analysis fails
        };
        
        var serializedFallback = JsonSerializer.Serialize(fallback, new JsonSerializerOptions { WriteIndented = true });
        _logger.LogInformation("REDTEAM GLOBAL: Generated fallback global analysis: {FallbackLength} characters", serializedFallback.Length);
        
        return serializedFallback;
    }

    public async Task<string> FixCaseAsync(string redTeamAnalysis, string currentJson, string caseId, int iterationNumber = 1, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SURGICAL FIX: Starting precision corrections - iteration {Iteration}", iterationNumber);
        _logger.LogInformation("SURGICAL FIX: Current JSON length: {JsonLength} chars", currentJson?.Length ?? 0);

        // Validate input JSON
        if (string.IsNullOrWhiteSpace(currentJson))
        {
            throw new ArgumentException("Current case JSON cannot be null or empty", nameof(currentJson));
        }

        // Validate JSON structure
        try
        {
            using var doc = JsonDocument.Parse(currentJson);
            _logger.LogInformation("SURGICAL FIX: JSON validation successful for case {CaseId}", caseId);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "SURGICAL FIX: Invalid JSON provided to FixCaseAsync for case {CaseId}", caseId);
            throw new ArgumentException($"Invalid JSON structure: {ex.Message}", nameof(currentJson));
        }

        // Parse structured RedTeam analysis
        StructuredRedTeamAnalysis analysis;
        try
        {
            analysis = JsonSerializer.Deserialize<StructuredRedTeamAnalysis>(redTeamAnalysis)!;
            _logger.LogInformation("SURGICAL FIX: Parsed {IssueCount} issues ({HighPriority} high, {MediumPriority} medium, {LowPriority} low)", 
                analysis.Issues.Count, analysis.HighPriorityCount, analysis.MediumPriorityCount, analysis.LowPriorityCount);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "SURGICAL FIX: Failed to parse structured RedTeam analysis, falling back to LLM approach");
            return await FallbackToLLMFix(redTeamAnalysis, currentJson, caseId, iterationNumber, cancellationToken);
        }

        // If no issues found, return original
        if (analysis.Issues.Count == 0)
        {
            _logger.LogInformation("SURGICAL FIX: No issues to fix for case {CaseId}", caseId);
            return currentJson;
        }

        // Apply precision corrections using injected PrecisionEditor
        var correctedJson = await _precisionEditor.ApplyPreciseFixesAsync(currentJson, analysis, caseId, cancellationToken);

        // Validate final result
        try
        {
            using var finalDoc = JsonDocument.Parse(correctedJson);
            _logger.LogInformation("SURGICAL FIX: Completed for case {CaseId}, iteration {Iteration}: {ResultLength} chars", 
                caseId, iterationNumber, correctedJson.Length);
            return correctedJson;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "SURGICAL FIX: Final validation failed for case {CaseId}, returning original", caseId);
            return currentJson;
        }
    }

    private async Task<string> FallbackToLLMFix(string redTeamAnalysis, string currentJson, string caseId, int iterationNumber, CancellationToken cancellationToken)
    {
        _logger.LogInformation("FALLBACK FIX: Using LLM approach for case {CaseId}", caseId);

        var systemPrompt = """
            You are an expert case correction agent. Apply specific corrections mentioned in the analysis.
            Return complete corrected JSON only (no markdown, no explanations).
            Make minimal, surgical changes based solely on the analysis.
            """;

        var userPrompt = $"""
            Apply corrections from this analysis:
            
            {redTeamAnalysis}
            
            To this case JSON:
            {currentJson}
            
            Return corrected complete JSON (iteration #{iterationNumber}).
            """;

        var result = await _llmService.GenerateAsync(caseId, systemPrompt, userPrompt, cancellationToken);
        
        if (string.IsNullOrWhiteSpace(result))
        {
            _logger.LogError("FALLBACK FIX: Empty response for case {CaseId}", caseId);
            return currentJson;
        }
        
        try
        {
            using var doc = JsonDocument.Parse(result);
            _logger.LogInformation("FALLBACK FIX: Successful for case {CaseId}: {ResultLength} chars", caseId, result.Length);
            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "FALLBACK FIX: Invalid JSON for case {CaseId}, returning original", caseId);
            return currentJson;
        }
    }

    public async Task<bool> IsCaseCleanAsync(string redTeamAnalysis, string caseId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Analyzing if case is clean and ready for packaging");

        var systemPrompt = """
            You are a quality assessment agent. Analyze red team feedback to determine if a case is ready for final packaging.
            
            Respond with exactly "CLEAN" if the case has no critical issues, or "NEEDS_FIX" if critical issues remain.
            
            Consider critical issues:
            - Logical inconsistencies that break the case narrative
            - Missing or contradictory evidence
            - Timeline errors that affect investigation flow
            - Character inconsistencies that confuse the story
            - Serious formatting or structural problems
            
            Minor issues (not critical):
            - Small wording improvements
            - Minor character details
            - Stylistic suggestions
            - Performance optimizations
            """;

        var userPrompt = $"""
            Analyze this red team feedback and determine if the case is CLEAN (ready for packaging) or NEEDS_FIX (has critical issues):
            
            RED TEAM ANALYSIS:
            {redTeamAnalysis}
            
            Respond with exactly one word: "CLEAN" or "NEEDS_FIX"
            """;

        var result = await _llmService.GenerateAsync(caseId, systemPrompt, userPrompt, cancellationToken);
        
        // Validate the result is not empty
        if (string.IsNullOrWhiteSpace(result))
        {
            _logger.LogError("Quality assessment returned empty response for case {CaseId} - defaulting to NEEDS_FIX for safety", caseId);
            return false; // Default to needs fix if we can't assess
        }
        
        // Parse the LLM response
        var cleanResult = result.Trim().ToUpperInvariant();
        var isClean = cleanResult.Contains("CLEAN") && !cleanResult.Contains("NEEDS_FIX");
        
        // Additional safety check - if response is too short or unclear, default to needs fix
        if (cleanResult.Length < 4)
        {
            _logger.LogWarning("Quality assessment response too short ({Length} chars): '{Response}' - defaulting to NEEDS_FIX", cleanResult.Length, result);
            return false;
        }
        
        _logger.LogInformation("Case quality assessment: '{Result}' (isClean: {IsClean})", cleanResult, isClean);
        return isClean;
    }

    public async Task<CaseGenerationOutput> PackageCaseAsync(string finalJson, string caseId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Packaging final case: {CaseId}", caseId);

        try
        {
            var bundlesContainer = _configuration["CaseGeneratorStorage:BundlesContainer"] ?? "bundles";
            var files = new List<GeneratedFile>();

            // Parse the validated case to extract information
            JsonElement validatedCase;
            try 
            {
                validatedCase = JsonSerializer.Deserialize<JsonElement>(finalJson);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse finalJson as valid JSON for case {CaseId}. JSON starts with: {JsonStart}", 
                    caseId, finalJson.Length > 100 ? finalJson[..100] : finalJson);
                throw new InvalidOperationException($"Invalid JSON provided to PackageCaseAsync for case {caseId}: {ex.Message}", ex);
            }
            
            // Extract case details from validated JSON
            var timezone = validatedCase.TryGetProperty("timezone", out var tzProp) ? tzProp.GetString() : "UTC";
            var difficulty = validatedCase.TryGetProperty("difficulty", out var diffProp) ? diffProp.GetString() : "Rookie";
            
            // Count documents and media
            var documentsCount = 0;
            var mediaCount = 0;
            var suspectsCount = 0;
            
            if (validatedCase.TryGetProperty("documents", out var docsArray))
                documentsCount = docsArray.GetArrayLength();
            if (validatedCase.TryGetProperty("media", out var mediaArray))
                mediaCount = mediaArray.GetArrayLength();
            if (validatedCase.TryGetProperty("suspects", out var suspectsArray))
                suspectsCount = suspectsArray.GetArrayLength();

            // Create comprehensive case manifest
            var caseManifest = new CaseBundle
            {
                CaseId = caseId,
                GeneratedAt = DateTime.UtcNow,
                Timezone = timezone ?? "UTC",
                Difficulty = difficulty ?? "Rookie",
                Counts = new CaseCounts
                {
                    Documents = documentsCount,
                    Media = mediaCount,
                    Suspects = suspectsCount
                },
                ValidationResults = new CaseValidationSummary
                {
                    TimestampsNormalized = "PASS",
                    AllIdsResolved = "PASS", 
                    MediaIntegrity = "PASS"
                },
                Manifest = new List<FileManifestEntry>(),
                RedTeamAnalysis = "RedTeamAnalysis.txt"
            };

            // Save the canonical normalized case file to BUNDLE
            var normalizedCaseFileName = $"{caseId}/normalized_case.json";
            await _storageService.SaveFileAsync(bundlesContainer, normalizedCaseFileName, finalJson, cancellationToken);
            
            var normalizedCaseHash = ComputeSHA256Hash(finalJson);
            caseManifest.Manifest.Add(new FileManifestEntry
            {
                Filename = "normalized_case.json",
                RelativePath = normalizedCaseFileName,
                Sha256 = normalizedCaseHash,
                MimeType = "application/json"
            });
            
            files.Add(new GeneratedFile
            {
                Path = normalizedCaseFileName,
                Type = "json",
                Size = System.Text.Encoding.UTF8.GetByteCount(finalJson),
                CreatedAt = DateTime.UtcNow
            });

            // Add individual documents to manifest (already saved during generation)
            if (validatedCase.TryGetProperty("documents", out var documentsArray))
            {
                foreach (var doc in documentsArray.EnumerateArray())
                {
                    if (doc.TryGetProperty("docId", out var docIdProp))
                    {
                        var docId = docIdProp.GetString();
                        var docFileName = $"{caseId}/documents/{docId}.json";
                        
                        try
                        {
                            var docContent = await _storageService.GetFileAsync(bundlesContainer, docFileName, cancellationToken);
                            var docHash = ComputeSHA256Hash(docContent);
                            
                            caseManifest.Manifest.Add(new FileManifestEntry
                            {
                                Filename = $"{docId}.json",
                                RelativePath = docFileName,
                                Sha256 = docHash,
                                MimeType = "application/json"
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Could not add document {DocId} to manifest", docId);
                        }
                    }
                }
            }

            // Add individual media to manifest (already saved during generation)
            if (validatedCase.TryGetProperty("media", out var mediaManifestArray))
            {
                foreach (var media in mediaManifestArray.EnumerateArray())
                {
                    if (media.TryGetProperty("evidenceId", out var evidenceIdProp))
                    {
                        var evidenceId = evidenceIdProp.GetString();
                        var mediaFileName = $"{caseId}/media/{evidenceId}.json";
                        
                        try
                        {
                            var mediaContent = await _storageService.GetFileAsync(bundlesContainer, mediaFileName, cancellationToken);
                            var mediaHash = ComputeSHA256Hash(mediaContent);
                            
                            caseManifest.Manifest.Add(new FileManifestEntry
                            {
                                Filename = $"{evidenceId}.json",
                                RelativePath = mediaFileName,
                                Sha256 = mediaHash,
                                MimeType = "application/json"
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Could not add media {EvidenceId} to manifest", evidenceId);
                        }
                    }
                }
            }

            // Save the main case manifest file
            var caseManifestJson = JsonSerializer.Serialize(caseManifest, new JsonSerializerOptions { WriteIndented = true });
            var caseManifestFileName = $"{caseId}/{caseId}.json";
            await _storageService.SaveFileAsync(bundlesContainer, caseManifestFileName, caseManifestJson, cancellationToken);
            
            var manifestHash = ComputeSHA256Hash(caseManifestJson);
            files.Add(new GeneratedFile
            {
                Path = caseManifestFileName,
                Type = "json",
                Size = System.Text.Encoding.UTF8.GetByteCount(caseManifestJson),
                CreatedAt = DateTime.UtcNow
            });

            // Save legacy metadata for compatibility
            var bundlePath = $"{caseId}";
            var metadata = new CaseMetadata
            {
                Title = caseId,
                Difficulty = difficulty ?? "Rookie",
                EstimatedDuration = 60,
                Categories = new[] { "Investigation", "Training" },
                Tags = new[] { "generated", "ai" },
                GeneratedAt = DateTime.UtcNow
            };

            var metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            var metadataFileName = $"{caseId}/metadata.json";
            await _storageService.SaveFileAsync(bundlesContainer, metadataFileName, metadataJson, cancellationToken);
            files.Add(new GeneratedFile
            {
                Path = metadataFileName,
                Type = "json",
                Size = System.Text.Encoding.UTF8.GetByteCount(metadataJson),
                CreatedAt = DateTime.UtcNow
            });

            return new CaseGenerationOutput
            {
                BundlePath = bundlePath,
                CaseId = caseId,
                Files = files.ToArray(),
                Metadata = metadata
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to package case: {CaseId}", caseId);
            throw;
        }
    }

    // Public method for testing PDF generation
    public async Task<byte[]> GenerateTestPdfAsync(string title, string markdownContent, string documentType = "general", CancellationToken cancellationToken = default)
    {
        return await _pdfRenderingService.GenerateTestPdfAsync(title, markdownContent, documentType, cancellationToken);
    }

    // Public method for testing image generation
    public async Task<string> GenerateTestImageAsync(MediaSpec spec, string caseId, CancellationToken cancellationToken = default)
    {
        return await _imagesService.GenerateAsync(caseId, spec);
    }

    // Save RedTeam analysis to logs container
    public async Task SaveRedTeamAnalysisAsync(string caseId, string redTeamAnalysis, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Saving RedTeam analysis for case {CaseId}", caseId);
        
        var logsContainer = "logs";
        var fileName = $"{caseId}/RedTeamAnalysis.txt";
        await _storageService.SaveFileAsync(logsContainer, fileName, redTeamAnalysis, cancellationToken);
        
        _logger.LogInformation("RedTeam analysis saved successfully to {Container}/{FileName}", logsContainer, fileName);
    }

    private static string ComputeSHA256Hash(string input)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hashBytes = sha256.ComputeHash(inputBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}