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
    private readonly ILogger<CaseGenerationService> _logger;

    public CaseGenerationService(
        ILLMService llmService,
        IStorageService storageService,
        ISchemaValidationService schemaValidationService,
        IJsonSchemaProvider schemaProvider,
        ICaseLoggingService caseLogging,
        INormalizerService normalizerService,
        IPdfRenderingService pdfRenderingService,
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
        Você é um arquiteto mestre de casos investigativos. Sua tarefa é criar um plano inicial COMPLETAMENTE AUTOMATIZADO
        para um caso detetivesco baseado no perfil de dificuldade especificado.

        PERFIL DE DIFICULDADE: {actualDifficulty}
        Descrição: {difficultyProfile?.Description}

        DIRETRIZES DE COMPLEXIDADE:
        - Suspeitos: {difficultyProfile?.Suspects.Min}-{difficultyProfile?.Suspects.Max}
        - Documentos: {difficultyProfile?.Documents.Min}-{difficultyProfile?.Documents.Max}
        - Evidências: {difficultyProfile?.Evidences.Min}-{difficultyProfile?.Evidences.Max}
        - Pistas falsas: {difficultyProfile?.RedHerrings}
        - Documentos gated: {difficultyProfile?.GatedDocuments}
        - Complexidade forense: {difficultyProfile?.ForensicsComplexity}
        - Fatores de complexidade: {string.Join(", ", difficultyProfile?.ComplexityFactors ?? Array.Empty<string>())}
        - Duração estimada: {difficultyProfile?.EstimatedDurationMinutes.Min}-{difficultyProfile?.EstimatedDurationMinutes.Max} minutos

        POLÍTICA DE GEOGRAFIA E NOMES (verossimilhança sem dados reais sensíveis):
        - NÃO use nomes reais de ruas, números de endereço ou coordenadas.
        - Cidades reais só se necessário e APENAS no nível cidade/UF (sem bairros/ruas reais).
        - É aceitável NÃO citar cidade/UF: use descrições genéricas como "rua residencial", "galpão logístico", "loja de bairro", "shopping center".
        - Nunca use marcas/empresas reais; crie nomes fictícios plausíveis quando preciso.

        PADRÕES DE TEMPO E LINGUAGEM:
        - Timestamps SEMPRE em ISO-8601 com offset (timezone alvo: {request.Timezone}).

        AUTOMATIZAÇÃO COMPLETA:
        - Gere automaticamente: título único, local verossímil (seguindo a POLÍTICA DE GEOGRAFIA), e tipo de crime adequado ao nível.
        - O título deve ser específico e atraente (ex: "Desaparecimento no Shopping Center", "Fraude na Startup de Fintech")
        - O local pode ser ABSTRATO (sem cidade/UF) quando fizer sentido; se usar cidade real, limite-se ao nome da cidade/UF (sem endereços).
        - Tipo de crime deve ser adequado ao nível (Rookie: roubo simples; Commander: crimes organizados complexos)
        - Adeque TODOS os elementos ao perfil de dificuldade

        IMPORTANTE: Seja criativo e varie os tipos de crime, locais e contextos para cada geração.
        """;

        var userPrompt = $"""
        Crie um PLANO COMPLETAMENTE AUTOMATIZADO para um novo caso investigativo.

        ENTRADA MÍNIMA:
        - Nível de dificuldade: {actualDifficulty}
        - Gerar imagens (metadado, não gerar agora): {request.GenerateImages}
        - Timezone preferencial para timestamps: {request.Timezone}

        POLÍTICA DE LOCALIZAÇÃO E NOMES:
        - NÃO usar nomes reais de ruas, números de endereço, coordenadas ou marcas/empresas reais.
        - O local pode ser ABSTRATO (ex.: "rua residencial", "loja de bairro", "galpão logístico", "shopping center").
        - Se for estritamente necessário citar cidade, limite-se ao nível cidade/UF (sem bairro/rua).

        GERE AUTOMATICAMENTE (siga o schema do Plan):
        - caseId único
        - title específico e atraente (pt-BR)
        - location verossímil (pode ser abstrato; se usar cidade, apenas cidade/UF)
        - incidentType adequado ao nível
        - overview envolvente (pt-BR)
        - learningObjectives[] alinhados ao nível
        - mainElements[] que guiarão o desenvolvimento
        - estimatedDuration (baseada no perfil de dificuldade)
        - timeline[] com eventos canônicos em **ISO-8601 com offset** (timezone alvo: {request.Timezone})
        - minDetectiveRank = {requestedDiff}
        - profileApplied: ranges numéricos efetivamente aplicados (suspects, documents, evidences, gatingSteps, falseLeads, interpretiveComplexity)
        - goldenTruth.facts com **minSupports ≥ 2** (NÃO revelar culpado(a) ou solução)

        VARIAÇÃO:
        - Varie entre tipos de crime (roubo, fraude, desaparecimento, homicídio, sequestro, crimes cibernéticos, etc.) conforme a dificuldade.
        - Adeque **todos** os elementos ao perfil (quantidade de suspeitos, documentos, evidências, encadeamentos/gating, pistas falsas e complexidade interpretativa).

        FORMATO DE SAÍDA:
        - **APENAS JSON** válido conforme o **Plan schema** (sem comentários, sem texto fora do JSON).
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
            Você é um especialista em desenvolvimento de casos investigativos. Expanda o plano inicial 
            criando detalhes completos baseados no PERFIL DE DIFICULDADE específico.
            
            PERFIL DE DIFICULDADE: {difficulty}
            Descrição: {difficultyProfile.Description}
            
            DIRETRIZES DE EXPANSÃO:
            - Suspeitos: {difficultyProfile.Suspects.Min}-{difficultyProfile.Suspects.Max} (varie perfis, motivos, alibis)
            - Evidências: {difficultyProfile.Evidences.Min}-{difficultyProfile.Evidences.Max} (físicas, digitais, testemunhais)
            - Pistas falsas: {difficultyProfile.RedHerrings} (red herrings sutis adequados ao nível)
            - Complexidade forense: {difficultyProfile.ForensicsComplexity}
            - Fatores: {string.Join(", ", difficultyProfile.ComplexityFactors)}
            
            COMPLEXIDADE POR NÍVEL:
            - Rookie/Detective: Evidências diretas, motivos claros, cronologia linear
            - Detective2/Sergeant: Correlações entre fontes, análises cruzadas, alguns elementos gated
            - Lieutenant/Captain: Análises especializadas, dependências entre evidências, inferências profundas
            - Commander: Conexões globais, casos em série, evidências de alta tecnologia
            """;

        var userPrompt = $"""
            Expanda este plano de caso em detalhes completos adequados ao nível de dificuldade:
            
            {planJson}
            
            EXPANSÃO DETALHADA DEVE INCLUIR:
            
            1. SUSPEITOS (baseado no perfil de dificuldade):
            - Perfis variados com backgrounds detalhados
            - Motivos convincentes e proporcionais ao nível
            - Alibis que variam em solidez conforme a dificuldade
            - Conexões entre suspeitos (para níveis altos)
            
            2. EVIDÊNCIAS (quantidade e complexidade por nível):
            - Evidências físicas principais e secundárias
            - Evidências digitais (crescente com a dificuldade)
            - Evidências testemunhais com variação de confiabilidade
            - Para níveis altos: evidências que exigem análise especializada
            
            3. CRONOLOGIA DETALHADA:
            - Linear para níveis baixos
            - Camadas múltiplas para níveis médios/altos
            - Eventos com timestamps precisos
            - Conexões temporais entre evidências
            
            4. TESTEMUNHAS:
            - Número adequado ao nível
            - Confiabilidade variável
            - Depoimentos com detalhes específicos
            - Para níveis altos: especialistas e peritos
            
            5. LOCALIZAÇÕES ESPECÍFICAS:
            - Detalhes forenses relevantes
            - Pontos de coleta de evidências
            - Rotas e acessos (importante para níveis altos)
            
            Adapte a complexidade narrativa, técnica e investigativa ao perfil especificado.
            """;

        var jsonSchema = _schemaProvider.GetSchema("Expand");

        return await _llmService.GenerateStructuredAsync(caseId, systemPrompt, userPrompt, jsonSchema, cancellationToken);
    }

    public async Task<string> DesignCaseAsync(string expandedJson, string caseId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Designing case structure with structured output");

        var systemPrompt = """
            Você é um designer de casos investigativos. Sua tarefa é transformar os detalhes expandidos 
            do caso em especificações estruturadas para geração paralela de documentos e mídias.
            
            IMPORTANTE:
            - Saída APENAS JSON válido no schema DocumentAndMediaSpecs
            - NÃO adicione explicações, comentários ou texto extra
            - Marque documentos sensíveis com "gated": true e inclua "gatingRule" como objeto { action, evidenceId?, notes? }
            - Para documentos gated=true, sempre inclua gatingRule
            - Para laudos periciais, inclua seção "Cadeia de Custódia"
            
            Tipos de documento permitidos:
            - police_report: Boletim de ocorrência
            - interview: Entrevista com suspeito/testemunha  
            - memo_admin: Memorando administrativo
            - forensics_report: Laudo pericial (sempre incluir "Cadeia de Custódia")
            - evidence_log: Log de evidências
            - witness_statement: Depoimento de testemunha
            
            Tipos de mídia permitidos:
            - photo: Fotografia de evidência
            - audio: Gravação de áudio
            - video: Gravação de vídeo
            - document_scan: Digitalização de documento
            - diagram: Diagrama ou esquema
            """;

        var userPrompt = $"""
            Transforme este caso expandido em especificações estruturadas:
            
            {expandedJson}
            
            Gere especificações para:
            - 8-14 documentos (adequados ao nível Iniciante)
            - 2-6 itens de mídia como evidências
            - 1-2 laudos periciais gated=true com gatingRule
            - LengthTarget adequado ao nível (documentos curtos: 150-400 palavras)
            
            """;

        var jsonSchema = _schemaProvider.GetSchema("DocumentAndMediaSpecs");

        // Generate structured response with retry logic for validation
        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var response = await _llmService.GenerateStructuredAsync(caseId, systemPrompt, userPrompt, jsonSchema, cancellationToken);

                // Validate the response against schema (without difficulty context for legacy method)
                var validationResult = await _schemaValidationService.ParseAndValidateAsync(response);
                if (validationResult != null)
                {
                    _logger.LogInformation("Design validation successful on attempt {Attempt}", attempt);
                    return response;
                }

                _logger.LogWarning("Design validation failed on attempt {Attempt}, retrying...", attempt);

                if (attempt == maxRetries)
                {
                    throw new InvalidOperationException("Failed to generate valid design specs after maximum retries");
                }
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                _logger.LogWarning(ex, "Design generation failed on attempt {Attempt}, retrying...", attempt);
            }
        }

        throw new InvalidOperationException("Failed to generate valid design specs");
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
            Você é um engenheiro de evidências de mídia. Gere uma especificação JSON para criação de mídia.
            Requisitos:
            - Saída **APENAS** JSON: { evidenceId, kind, title, genPrompt, constraints }
            - genPrompt deve ser detalhado e reproduzível (iluminação/ângulo/escala/etiquetas etc.)
            - NADA de texto fora do JSON
            """;

        var userPrompt = $"""
            CONTEXTO DO DESIGN (resumo estruturado):
            {designJson}

            ESPECIFICAÇÃO DE MÍDIA:
            evidenceId: {spec.EvidenceId}
            kind: {spec.Kind}
            title: {spec.Title}
            constraints: {(spec.Constraints != null && spec.Constraints.Any() ? string.Join(", ", spec.Constraints.Select(kv => $"{kv.Key}: {kv.Value}")) : "n/a")}

            Gere o JSON final da mídia conforme instruções (campo genPrompt obrigatório).
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

    //Old Normalize using LLM
    public async Task<string> NormalizeCaseAsync(string[] documents, string[] media, string caseId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Normalizing case content");

        var systemPrompt = """
            Você é um especialista em padronização de conteúdo educacional. Normalize e organize 
            todos os elementos do caso em uma estrutura consistente e bem formatada.
            """;

        var userPrompt = $"""
            Normalize e organize estes elementos do caso:
            
            DOCUMENTOS:
            {string.Join("\n---\n", documents)}

            PROMPTS DE MÍDIA:
            {string.Join("\n---\n", media)}

            Crie uma estrutura normalizada com formatação consistente, metadata adequada e organização lógica.
            """;

        return await _llmService.GenerateAsync(caseId, systemPrompt, userPrompt, cancellationToken);
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
            Você é um especialista em indexação de conteúdo educacional. Crie índices e metadados 
            para facilitar a busca e organização do caso.
            """;

        var userPrompt = $"""
            Crie índices e metadados para este caso normalizado:
            
            {normalizedJson}
            
            Inclua: tags, categorias, palavras-chave, dificuldade, duração, objetivos de aprendizado.
            """;

        return await _llmService.GenerateAsync(caseId, systemPrompt, userPrompt, cancellationToken);
    }

    public async Task<string> ValidateRulesAsync(string indexedJson, string caseId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Validating case rules");

        var systemPrompt = """
            Você é um especialista em validação de conteúdo educacional. Verifique se o caso 
            atende a todas as regras pedagógicas e de qualidade estabelecidas.
            """;

        var userPrompt = $"""
            Valide este caso indexado contra as regras de qualidade:
            
            {indexedJson}
            
            Verifique: consistência narrativa, adequação pedagógica, completude das informações, 
            realismo, e aderência aos padrões de qualidade.
            """;

        return await _llmService.GenerateAsync(caseId, systemPrompt, userPrompt, cancellationToken);
    }

    public async Task<string> RedTeamCaseAsync(string validatedJson, string caseId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Red teaming case for quality assurance");

        var systemPrompt = """
            Você é um especialista em red team para conteúdo educacional. Faça uma análise crítica 
            do caso buscando problemas, inconsistências e pontos de melhoria.
            """;

        var userPrompt = $"""
            Faça uma análise de red team deste caso validado:
            
            {validatedJson}
            
            Identifique: problemas lógicos, inconsistências, pontos fracos na narrativa, 
            possíveis melhorias, e riscos de qualidade.
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
                Difficulty = "Iniciante", // Extract from finalJson in real implementation
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
}