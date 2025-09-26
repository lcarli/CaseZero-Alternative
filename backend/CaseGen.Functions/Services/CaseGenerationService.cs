using CaseGen.Functions.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text;
using System;

namespace CaseGen.Functions.Services;

public class CaseGenerationService : ICaseGenerationService
{
    private readonly ILLMService _llmService;
    private readonly IStorageService _storageService;
    private readonly ISchemaValidationService _schemaValidationService;
    private readonly IConfiguration _configuration;
    private readonly IJsonSchemaProvider _schemaProvider;
    private readonly ICaseLoggingService _caseLogging;
    private readonly INormalizerService _normalizerService;
    private readonly IPdfRenderingService _pdfRenderingService;
    private readonly IImagesService _imagesService;
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

            TIME/LANGUAGE:
            - All timestamps MUST be ISO-8601 with offset (target timezone: {request.Timezone}).

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
            - timeline[] canonical events with ISO-8601 + offset (use {request.Timezone})
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

            STRICT CONSISTENCY:
            - Reuse names/roles introduced in Plan verbatim; DO NOT invent new real-world brands/addresses.
            - All timestamps MUST be ISO-8601 with offset.
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
            - Use ISO-8601 timestamps with offset whenever citing times.
            - If type == forensics_report, include the ""Chain of Custody"" section.
            - Whenever citing evidence, reference existing evidenceId/docId (do not invent).
            - Do not mention gating in the content (gating is game metadata).
            - No real PII, brands, or real addresses.

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

    public async Task<string> IndexCaseAsync(string normalizedJson, string caseId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Indexing case content");

        var systemPrompt = """
            You are a specialist in indexing educational investigative training content. Create structured indices and metadata
            to facilitate search, retrieval, filtering, and organization of this police investigative case.
            """;

        var userPrompt = $"""
            Generate indices and metadata for this normalized case:

            {normalizedJson}

            Include (concise and relevant):
            - tags (general thematic descriptors)
            - categories (higher‑level grouping: e.g. crime type, procedural phase)
            - keywords (investigative and forensic focus terms)
            - difficulty (normalized if possible)
            - estimatedDuration (minutes if derivable)
            - learningObjectives (refined / deduplicated)
            - summary (1–2 sentence abstract)
            - entities (people, locations, evidence IDs) — list with type classification
            Output only JSON (no commentary).
            """;

        return await _llmService.GenerateAsync(caseId, systemPrompt, userPrompt, cancellationToken);
    }

    public async Task<string> ValidateRulesAsync(string indexedJson, string caseId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Validating case rules");

        var systemPrompt = """
            You are a specialist in validating educational police investigative training content. 
            Verify that the case complies with all established pedagogical, realism, and quality standards.
            """;

        var userPrompt = $"""
            Validate this indexed case against the defined quality rules:
            
            {indexedJson}
            
            Check: narrative consistency, pedagogical suitability, completeness of information, realism, 
            and adherence to investigative training quality standards. Do NOT reveal any solution or culprit.
            """;

        return await _llmService.GenerateAsync(caseId, systemPrompt, userPrompt, cancellationToken);
    }

    public async Task<string> RedTeamCaseAsync(string validatedJson, string caseId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Red teaming case for quality assurance");

        var systemPrompt = """
            You are a red team specialist for police investigative training content. Perform a critical analysis
            of the case, identifying weaknesses, inconsistencies, and opportunities for improvement.
            """;

        var userPrompt = $"""
            Perform a red team analysis of this validated case:
            
            {validatedJson}
            
            Identify: logical issues, inconsistencies, narrative weaknesses, potential improvements, and quality risks.
            """;

        return await _llmService.GenerateAsync(caseId, systemPrompt, userPrompt, cancellationToken);
    }

    public async Task<CaseGenerationOutput> PackageCaseAsync(string finalJson, string caseId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Packaging final case: {CaseId}", caseId);

        try
        {
            var casesContainer = _configuration["CaseGeneratorStorage:CasesContainer"] ?? "cases";
            var bundlesContainer = _configuration["CaseGeneratorStorage:BundlesContainer"] ?? "bundles";

            var files = new List<GeneratedFile>();

            // Save main case file
            var caseFileName = $"{caseId}/case.json";
            await _storageService.SaveFileAsync(casesContainer, caseFileName, finalJson, cancellationToken);
            files.Add(new GeneratedFile
            {
                Path = caseFileName,
                Type = "json",
                Size = System.Text.Encoding.UTF8.GetByteCount(finalJson),
                CreatedAt = DateTime.UtcNow
            });

            // Save bundle metadata
            var bundlePath = $"{caseId}";
            var metadata = new CaseMetadata
            {
                Title = caseId,
                Difficulty = "Rookie",
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
}