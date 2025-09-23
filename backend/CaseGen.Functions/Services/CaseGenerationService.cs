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

        var systemPrompt = $"""
        You are a master architect of investigative cold cases. Your task is to create a FULLY AUTOMATED INITIAL PLAN
        for a detective-style case based on the specified difficulty profile. These are cold cases requiring a meticulous and detailed approach.

        DIFFICULTY PROFILE: {actualDifficulty}
        Description: {difficultyProfile?.Description}

        COMPLEXITY GUIDELINES:
        - Suspects: {difficultyProfile?.Suspects.Min}-{difficultyProfile?.Suspects.Max}
        - Documents: {difficultyProfile?.Documents.Min}-{difficultyProfile?.Documents.Max}
        - Evidence items: {difficultyProfile?.Evidences.Min}-{difficultyProfile?.Evidences.Max}
        - False leads (red herrings): {difficultyProfile?.RedHerrings}
        - Gated documents: {difficultyProfile?.GatedDocuments}
        - Forensics complexity: {difficultyProfile?.ForensicsComplexity}
        - Complexity factors: {string.Join(", ", difficultyProfile?.ComplexityFactors ?? Array.Empty<string>())}
        - Estimated duration: {difficultyProfile?.EstimatedDurationMinutes.Min}-{difficultyProfile?.EstimatedDurationMinutes.Max} minutes

        GEOGRAPHY AND NAMING POLICY (plausibility without exposing sensitive real data):
        - DO NOT use real street names, address numbers, or coordinates.
        - Real cities ONLY if necessary and ONLY at city/state level (no neighborhoods / real streets).
        - It is acceptable NOT to specify a city/state: use generic descriptions such as "residential street", "logistics warehouse", "neighborhood store", "shopping center".
        - Never use real brands/companies; create plausible fictional names when needed.

        TIME AND LANGUAGE STANDARDS:
        - Timestamps MUST ALWAYS be in ISO-8601 with offset (target timezone: {request.Timezone}).

        FULL AUTOMATION:
        - Automatically generate: unique title, plausible location (following the GEOGRAPHY POLICY), and crime type appropriate to difficulty.
        - The title must be specific and compelling (e.g.: "Disappearance at the Shopping Center", "Fraud Inside a Fintech Startup").
        - The location MAY be ABSTRACT (without city/state) when it makes sense; if using a real city, restrict to city/state name only (no addresses).
        - Crime type must match the level (Rookie: simple theft; Commander: complex organized crimes).
        - Align ALL elements to the difficulty profile.

        IMPORTANT: Vary crime types, locations and contexts across generations—avoid repetition.
        """;

        var userPrompt = $"""
        Generate a FULLY AUTOMATED PLAN for a new investigative case.

        MINIMAL INPUT:
        - Difficulty level: {actualDifficulty}
        - Generate images (metadata only, DO NOT generate now): {request.GenerateImages}
        - Preferred timezone for timestamps: {request.Timezone}

        LOCATION & NAMING POLICY:
        - DO NOT use real street names, address numbers, coordinates, or real brands/companies.
        - Location may be ABSTRACT (e.g.: "residential street", "neighborhood store", "logistics warehouse", "shopping center").
        - If absolutely necessary to cite a city, restrict to city/state only (no neighborhood/street details).

        AUTOMATICALLY PRODUCE (follow the Plan schema):
        - Unique caseId
        - Specific and compelling title (Portuguese)
        - Plausible location (may be abstract; if city used, only city/state)
        - incidentType appropriate to the difficulty
        - Engaging overview (Portuguese)
        - learningObjectives[] aligned with the difficulty level
        - mainElements[] that will drive later development
        - estimatedDuration (based on difficulty profile)
        - timeline[] with canonical events in **ISO-8601 with offset** (target timezone: {request.Timezone})
        - minDetectiveRank = {requestedDiff}
        - profileApplied: numeric ranges actually applied (suspects, documents, evidences, gatingSteps, falseLeads, interpretiveComplexity)
        - goldenTruth.facts with **minSupports ≥ 2** (DO NOT reveal culprit or solution)

        VARIATION:
        - Vary among crime types (theft, fraud, disappearance, homicide, kidnapping, cybercrime, etc.) according to difficulty.
        - ALL elements must adapt to the profile (quantity of suspects, documents, evidences, gating chains, false leads, interpretive complexity).

        OUTPUT FORMAT:
        - **ONLY JSON** valid according to the **Plan schema** (no comments, no extra text).
        """;

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

        var systemPrompt = $"""
            You are a specialist in developing investigative COLD CASES. Expand the initial plan
            by creating fully detailed structures based on the specific DIFFICULTY PROFILE.
            
            DIFFICULTY PROFILE: {difficulty}
            Description: {difficultyProfile.Description}
            
            EXPANSION GUIDELINES:
            - Suspects: {difficultyProfile.Suspects.Min}-{difficultyProfile.Suspects.Max} (vary profiles, motives, alibis)
            - Evidence items: {difficultyProfile.Evidences.Min}-{difficultyProfile.Evidences.Max} (physical, digital, testimonial)
            - False leads: {difficultyProfile.RedHerrings} (subtle red herrings appropriate to the level)
            - Forensics complexity: {difficultyProfile.ForensicsComplexity}
            - Complexity factors: {string.Join(", ", difficultyProfile.ComplexityFactors)}
            
            COMPLEXITY BY LEVEL:
            - Rookie / Detective: Direct evidence, clear motives, linear chronology
            - Detective2 / Sergeant: Correlations across sources, cross-analysis, some gated elements
            - Lieutenant / Captain: Specialized analyses, dependencies across evidences, deeper inferences
            - Commander: Global connections, serial patterns, high‑tech evidence
            """;

        var userPrompt = $"""
            Expand this case plan into full detailed content appropriate to the difficulty level:
            
            {planJson}
            
            DETAILED EXPANSION MUST INCLUDE:
            
            1. SUSPECTS (driven by difficulty profile):
            - Varied profiles with detailed backgrounds
            - Convincing motives proportional to the level
            - Alibis whose solidity varies with difficulty
            - Connections/relationships between suspects (for higher levels)
            
            2. EVIDENCE (quantity and complexity by level):
            - Primary and secondary physical evidence
            - Digital evidence (increases with difficulty)
            - Testimonial evidence with varying reliability
            - For higher levels: items requiring specialized analysis
            
            3. DETAILED TIMELINE:
            - Linear for lower levels
            - Multiple layered threads for mid/high levels
            - Events with precise ISO-8601 timestamps
            - Temporal connections linking evidence pieces
            
            4. WITNESSES:
            - Count appropriate to level
            - Variable reliability
            - Statements with specific, concrete details
            - For higher levels: experts / specialists
            
            5. SPECIFIC LOCATIONS:
            - Relevant forensic details
            - Evidence collection points
            - Routes and access paths (important for higher levels)
            
            Adapt narrative, technical and investigative complexity to the specified profile.
            """;

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

        var systemPrompt = """
            Você é um designer de casos investigativos. Converta o plano e a expansão
            em especificações estruturadas para geração paralela de documentos e mídias.

            IMPORTANTE:
            - Saída APENAS JSON válido no schema DocumentAndMediaSpecs.
            - NÃO adicione explicações, comentários ou texto extra.
            - Marque documentos sensíveis com "gated": true e inclua "gatingRule" como objeto { action, evidenceId?, notes? }.
            - Para laudos periciais, inclua seção "Cadeia de Custódia".

            POLÍTICA DE LOCALIZAÇÃO/NOMES:
            - Não invente endereços reais; mantenha locais abstratos ou cidade/UF apenas.
            - Não use marcas/empresas reais; use nomes fictícios plausíveis.

            Tipos de documento permitidos:
            - police_report, interview, memo_admin, forensics_report, evidence_log, witness_statement

            Tipos de mídia permitidos:
            - photo, document_scan, diagram (suportados agora)
            - audio, video (DEVEM ter deferred=true - não suportados ainda)
            """;

        var userPrompt = $"""
            Transforme este caso em especificações estruturadas.

            Dificuldade: {difficulty ?? planDifficulty}

            CONTEXTO DO PLANO:
            {planJson}

            CONTEXTO EXPANDIDO:
            {expandedJson}

            REGRAS DE QUANTIDADE (dinâmicas por perfil do plano):
            - Documentos: {minDocs}-{maxDocs}
            - Itens de mídia (evidence): {minMedia}-{maxMedia}
            - Documentos gated: exatamente {gatedDocsCount} (se > 0, DEVEM ser do tipo forensics_report)

            POLÍTICA DE LOCALIZAÇÃO/NOMES:
            - Não usar endereços reais (rua/número/coordenadas) ou marcas/empresas reais.
            - Locais podem ser abstratos ou, se necessário, apenas cidade/UF.

            REGRAS ESPECÍFICAS PARA GATED:
            {(gatedDocsCount > 0 ? $@"- Exatamente {gatedDocsCount} documento(s) do tipo forensics_report com ""gated"": true.
            - Cada documento gated DEVE conter ""gatingRule"" como objeto: {{ ""action"": ""requires_evidence"" ou ""submit_evidence"", ""evidenceId"": ""<id>"" }}.
            - O ""evidenceId"" referenciado em cada gatingRule DEVE existir em mediaSpecs[*].evidenceId.
            - Cada laudo gated deve incluir a seção ""Cadeia de Custódia"" nas sections."
            : @"- PROIBIDO usar ""gated"": true para QUALQUER documento neste nível.
            - TODOS os documentos DEVEM ter ""gated"": false.
            - NÃO inclua o campo ""gatingRule"" em NENHUM documento.")}

            REQUISITOS DE DOCUMENTOS:
            - Para cada documento, gerar: docId único (ex.: ""doc_<slug>_<nnn>""), type, title, sections[], lengthTarget[min,max], gated (bool).
            - Incluir pelo menos 1 evidence_log e 1 police_report.
            - Tipos permitidos: police_report, interview, memo_admin, forensics_report, evidence_log, witness_statement.
            - Se type == forensics_report, incluir a seção ""Cadeia de Custódia"" nas sections (mesmo quando não-gated).

            REQUISITOS DE MÍDIA:
            - mediaSpecs: evidenceId único (ex.: ""ev_<slug>_<nnn>""), kind (photo/document_scan/diagram/audio/video), title, prompt, constraints (OBJETO), deferred (bool).
            - audio e video DEVEM ter deferred=true (a geração desses tipos ainda não é suportada).
            - constraints deve ser um OBJETO com chaves/valores simples (ex.: ""iluminacao"": ""raking"", ""escala"": true).

            CONFORMIDADE DE SCHEMA (OBRIGATÓRIO):
            - Saída APENAS em JSON válido conforme **DocumentAndMediaSpecs**.
            - NÃO incluir campos fora do schema (sem i18nKey, sem extras).
            - IDs (docId/evidenceId) devem ser únicos e consistentes.
            - Quando gated=false, NÃO incluir o campo ""gatingRule"".

            Saída: **APENAS JSON** válido conforme **DocumentAndMediaSpecs** (sem comentários nem texto extra).
            """;

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

        // Deriva dificuldade (Plan > override > Rookie)
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

        string typeDirectives = spec.Type.ToLowerInvariant() switch
        {
            "police_report" => """
        FORMATO (Markdown dentro de 'content'):
        - Cabeçalho: Número do B.O., Data/Hora (ISO-8601 com offset), Unidade/Agente responsável.
        - Resumo do incidente (objetivo).
        - Seções solicitadas (use títulos H2: ##).
        - Listas em bullet quando apropriado.
        """,
            "interview" => """
        FORMATO (Markdown dentro de 'content'):
        - Transcrição limpa da entrevista (sem observações/juízo do entrevistador).
        - Rotule falas: **Entrevistador:** / **Entrevistado(a):**.
        - Timestamps opcionais em colchetes quando natural (ex.: [00:05]).
        - Perguntas e respostas objetivas; sem inferir culpabilidade.
        """,
            "memo_admin" => """
        FORMATO (Markdown dentro de 'content'):
        - Cabeçalho: Para / De / Assunto / Data.
        - Tom burocrático conciso; bullets para ações.
        - Referencie documentos/evidências por ID quando existir.
        """,
            "forensics_report" => """
        FORMATO (Markdown dentro de 'content'):
        - Cabeçalho: Laboratório/Perito/Data/Hora (ISO-8601 com offset).
        - Metodologia (procedimentos), Resultados, Interpretação/Limitações.
        - **Cadeia de Custódia** (obrigatória) com eventos em ordem temporal.
        - Referencie evidenceId/docId pertinentes (sem revelar solução).
        """,
            "evidence_log" => """
        FORMATO (Markdown dentro de 'content'):
        - Tabela: ItemId | Coleta em | Coletado por | Descrição | Armazenamento | Transferências.
        - Observações breves por item.
        """,
            "witness_statement" => """
        FORMATO (Markdown dentro de 'content'):
        - Declaração em 1ª pessoa, objetiva, sem especular culpado(a).
        - Data/Hora (ISO-8601 com offset) e identificação fictícia resumida.
        """,
            _ => "FORMATO: use as seções solicitadas; texto objetivo, documental."
        };

        // Diretrizes por dificuldade (corrigido "Cruze")
        string difficultyDirectives = difficulty switch
        {
            "Rookie" or "Iniciante" => """
        PERFIL DE DIFICULDADE:
        - Vocabulário simples e direto; pouca ambiguidade.
        - Cronologia linear e relações explícitas.
        - Mire próximo do mínimo em lengthTarget.
        """,
            "Detective" or "Detective2" => """
        PERFIL DE DIFICULDADE:
        - Vocabulário moderado; algum jargão com contexto.
        - Introduza ambiguidades plausíveis e checagens cruzadas leves.
        - Mire o meio da faixa de lengthTarget.
        """,
            "Sergeant" or "Lieutenant" => """
        PERFIL DE DIFICULDADE:
        - Tom técnico quando pertinente; correlações entre fontes.
        - Ambiguidades reais (sem revelar solução); cite horários e IDs.
        - Mire o topo da faixa de lengthTarget.
        """,
            _ /* Captain/Commander */ => """
        PERFIL DE DIFICULDADE:
        - Linguagem técnica; inferências especializadas.
        - Ambiguidade controlada, hipóteses concorrentes.
        - Cruze múltiplas fontes; mencione limitações de método.
        - Use perto do máximo do lengthTarget.
        """
        };

        var designCtx = designJson ?? "{}";
        var planCtx = planJson ?? "{}";
        var expandCtx = expandJson ?? "{}";

        var systemPrompt = """
            Você é um redator técnico policial/pericial. Gere **APENAS JSON** com o corpo do documento.
            Regras gerais:
            - Jamais revele a solução/culpado(a).
            - Mantenha consistência com o contexto do caso (Plan/Expand/Design).
            - Cumpra exatamente as seções e o intervalo de palavras solicitado (lengthTarget).
            - Se o documento for laudo pericial, inclua "Cadeia de Custódia" (obrigatória).
            - Use timestamps ISO-8601 com offset quando citar horários.
            - Nada fora de JSON.

            OUTPUT JSON (obrigatório):
            {
            "docId": "string",
            "type": "string",
            "title": "string",
            "words": number,
            "sections": [
                { "title": "string", "content": "markdown" }
            ]
            }
            """;

        var userPrompt = $"""
            CONTEXTO — DESIGN (resumo):
            {designCtx}

            CONTEXTO — PLAN (resumo):
            {planCtx}

            CONTEXTO — EXPAND (resumo):
            {expandCtx}

            DOCUMENTO A GERAR:
            - docId: {spec.DocId}
            - type: {spec.Type}
            - title: {spec.Title}
            - sections (ordem): {string.Join(", ", spec.Sections)}
            - lengthTarget: {spec.LengthTarget[0]}–{spec.LengthTarget[1]} palavras
            - gated: {spec.Gated}

            DIRETIVAS POR TIPO:
            {typeDirectives}

            DIRETIVAS POR DIFICULDADE ({difficulty}):
            {difficultyDirectives}

            Restrições adicionais:
            - Se citar pessoas, locais, evidências: use somente as definidas em Expand/Design.
            - **Não mencione gating/autorização no conteúdo** (o bloqueio é metadado do jogo).
            - Evite PII real, marcas e endereços reais (use nomes fictícios/locais abstratos).

            Saída: **APENAS o JSON** na estrutura especificada, sem comentários ou texto extra.
            """;

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

        var systemPrompt = """
            You are a generator of FORENSIC specifications for static media.
            Output: ONLY valid JSON with { evidenceId, kind, title, prompt, constraints }.

            DUTIES
            - Create an operational and measurable prompt for ONE image (not a sequence).
            - Style: documentary photograph/screenshot, neutral, non‑artistic.
            - Content 100% fictitious. Forbid real names, brands/logos, faces/biometrics, real license plates, official badges.
            - Nothing graphic or violent. No children. Avoid images of people (when the scene allows).
            - Standardize: angle in degrees, height/distance in meters, lens in mm, aperture f/, shutter 1/x s, ISO, WB K.
            - For forensic item photo (object on a surface): use 90° top‑down when applicable.
            - Lighting: diffuse / even; no harsh shadows.

            MANDATORY FIELD FORMAT
            - prompt: text in fixed, concise sections, in this order:
              1) Function (1 sentence)
              2) Scene / Composition (3–5 sentences; include % of subject in frame)
              3) Angle & Distance (numeric values)
              4) Optics & Technique (lens mm, f/, 1/x s, ISO, WB K, focus, DOF)
              5) Mandatory elements (marker A/B/C, timestamp, “CAM-03”, etc.)
              6) Negatives (objective list of what MUST NOT appear)
              7) Acceptance checklist (actionable verification list)
            - constraints: JSON object containing: angle_deg, camera_height_m or distance_m, aspect_ratio, resolution_px, seed, deferred=false.

            SPECIFIC RULES BY TYPE
            - kind=cftv_frame: provide timestamp overlay (monospaced, white with outline), camera label (“CAM-03”), height 2.5–3.0 m, wide‑angle lens 2.8–4 mm, light H.264 compression (moderate artifacts), shutter 1/60 s, slight coherent motion blur.
            - kind=document_scan/receipt: no moiré, no fingers, perspective corrected, 300–600 DPI equivalent, visible margins.
            - kind=scene_topdown: 90° top‑down, orthogonal plane.

            DO NOT
            - Do not write the evidence name on the image.
            - Do not repeat the rules as generic text; convert them into parameters.
            - Do not return Markdown, comments, or extra fields.

            Mentally validate the checklist before responding. Output: JSON only.
            """;

        var userPrompt = $"""
            CONTEXT (structured summary of design / relevant documents, do not describe everything: only what is necessary)
            {designJson}

            EVIDENCE SPECIFICATION
            evidenceId: {spec.EvidenceId}
            kind: {spec.Kind}
            title: {spec.Title}
            difficulty: {(difficultyOverride ?? "Detective")}
            initial_constraints: {(spec.Constraints != null && spec.Constraints.Any() ? string.Join(", ", spec.Constraints.Select(kv => $"{kv.Key}: {kv.Value}")) : "n/a")}
            language: en-US

            LEVEL OF DETAIL BY DIFFICULTY (apply WITHOUT creating a photo sequence):
            - Rookie: simple composition; top-down when applicable.
            - Detective/Detective2: +1 contextual detail (still a single image).
            - Sergeant+: wide context + relevant detail within the SAME image (no collage), subtle false-positive ONLY if it makes sense.
            - Captain/Commander: include control objects (color/gray card) in the SAME scene when plausible.

            GENERATE:
            - evidenceId, kind, title
            - prompt: fixed sections (Function; Scene / Composition; Angle & Distance; Optics & Technique; Mandatory Elements; Negatives; Acceptance Checklist)
            - constraints: JSON object with angle_deg, camera_height_m / distance_m, aspect_ratio, resolution_px, seed (7 digits), deferred=false

            IMPORTANT:
            - If kind=cftv_frame: timestamp overlay “YYYY-MM-DD HH:MM:SS” in top-right corner; label “CAM-03”.
            - Do not use measurement ruler.
            - 100% fictitious content. Forbid real names, brands/logos, faces/biometrics, real license plates, official badges.
            - Return ONLY valid JSON.
            """;

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