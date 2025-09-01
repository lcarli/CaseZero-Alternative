using CaseGen.Functions.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text;
using System;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CaseGen.Functions.Services;

public class CaseGenerationService : ICaseGenerationService
{
    private readonly ILLMService _llmService;
    private readonly IStorageService _storageService;
    private readonly ISchemaValidationService _schemaValidationService;
    private readonly IConfiguration _configuration;
    private readonly IJsonSchemaProvider _schemaProvider;
    private readonly ICaseLoggingService _caseLogging;
    private readonly ILogger<CaseGenerationService> _logger;

    public CaseGenerationService(
        ILLMService llmService,
        IStorageService storageService,
        ISchemaValidationService schemaValidationService,
        IJsonSchemaProvider schemaProvider,
        ICaseLoggingService caseLogging,
        IConfiguration configuration,
        ILogger<CaseGenerationService> logger)
    {
        _llmService = llmService;
        _schemaProvider = schemaProvider;
        _storageService = storageService;
        _schemaValidationService = schemaValidationService;
        _caseLogging = caseLogging;
        _configuration = configuration;
        _logger = logger;

        // Configure QuestPDF for realistic document generation
        QuestPDF.Settings.License = LicenseType.Community;
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
        _logger.LogInformation("Rendering document [{DocId}] from JSON to MD and PDF", docId);

        try
        {
            using var doc = JsonDocument.Parse(documentJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("docId", out var docIdProp) ||
                !root.TryGetProperty("title", out var titleProp) ||
                !root.TryGetProperty("sections", out var sectionsProp))
            {
                throw new InvalidOperationException($"Invalid document JSON structure for {docId}");
            }

            var title = titleProp.GetString() ?? "Untitled Document";
            var sections = sectionsProp.EnumerateArray();

            // Generate Markdown content
            var markdownBuilder = new StringBuilder();
            markdownBuilder.AppendLine($"# {title}");
            markdownBuilder.AppendLine();

            foreach (var section in sections)
            {
                if (section.TryGetProperty("title", out var sectionTitle) &&
                    section.TryGetProperty("content", out var sectionContent))
                {
                    markdownBuilder.AppendLine($"## {sectionTitle.GetString()}");
                    markdownBuilder.AppendLine();
                    markdownBuilder.AppendLine(sectionContent.GetString());
                    markdownBuilder.AppendLine();
                }
            }

            var markdownContent = markdownBuilder.ToString();

            // Generate simple PDF content (text-based since we don't have PDF libraries)
            var pdfContent = GenerateSimplePdfContent(title, markdownContent);

            // Save files to bundles container
            var bundlesContainer = _configuration["CaseGeneratorStorage:BundlesContainer"] ?? "bundles";
            var mdPath = $"{caseId}/documents/{docId}.md";
            var pdfPath = $"{caseId}/documents/{docId}.pdf";

            await _storageService.SaveFileAsync(bundlesContainer, mdPath, markdownContent, cancellationToken);
            await _storageService.SaveFileAsync(bundlesContainer, pdfPath, pdfContent, cancellationToken);

            // Log the rendering step
            await _caseLogging.LogStepResponseAsync(caseId, $"render/{docId}",
                JsonSerializer.Serialize(new
                {
                    docId,
                    markdownPath = mdPath,
                    pdfPath = pdfPath,
                    wordCount = markdownContent.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length
                }, new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken);

            _logger.LogInformation("RENDER: Generated MD and PDF for doc {DocId} (case={CaseId})", docId, caseId);

            // Return a summary of the rendering operation
            return JsonSerializer.Serialize(new
            {
                docId,
                status = "rendered",
                files = new { markdown = mdPath, pdf = pdfPath }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render document {DocId} for case {CaseId}", docId, caseId);
            throw;
        }
    }

    private byte[] GenerateRealisticPdf(string title, string markdownContent, string documentType = "general", string? caseId = null, string? docId = null)
    {
        try
        {
            var classification = "CONFIDENCIAL • USO INTERNO";
            var docTypeLabel = GetDocumentTypeLabel(documentType);
            var (bandBg, bandText) = GetThemeColors(documentType);

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(36);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10.5f).LineHeight(1.35f));

                    // Marca d'água suave
                    page.Background().Element(e => AddWatermark(e, classification));

                    // Letterhead institucional
                    page.Header().Column(headerCol =>
                    {
                        headerCol.Item().Element(h => BuildLetterhead(h, docTypeLabel, title, caseId, docId));
                        headerCol.Item().Element(h => BuildClassificationBand(h, classification, bandBg, bandText));
                    });

                    // Conteúdo
                    page.Content().PaddingTop(8).Column(col =>
                    {
                        RenderByType(col, documentType, markdownContent, caseId, docId);
                    });

                    // Rodapé com paginação e sigilo
                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span(classification).FontSize(9).FontColor(Colors.Grey.Darken2);
                        t.Span("   •   ");
                        t.CurrentPageNumber().FontSize(9);
                        t.Span(" / ");
                        t.TotalPages().FontSize(9);
                    });
                });
            }).GeneratePdf();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GenerateRealisticPdf");
            throw;
        }
    }

    private static string GetDocumentTypeLabel(string documentType)
    {
        return documentType.ToLower() switch
        {
            "police_report" => "RELATÓRIO DE OCORRÊNCIA",
            "forensics_report" => "LAUDO PERICIAL",
            "interview" => "TRANSCRIÇÃO DE ENTREVISTA",
            "evidence_log" => "CATÁLOGO & CADEIA DE CUSTÓDIA",
            "memo" or "memo_admin" => "MEMORANDO INVESTIGATIVO",
            "witness_statement" => "DECLARAÇÃO DE TESTEMUNHA",
            _ => "DOCUMENTO INVESTIGATIVO"
        };
    }

    private void AddWatermark(QuestPDF.Infrastructure.IContainer c, string text)
    {
        c.AlignCenter()
         .AlignMiddle()
         .Rotate(315)
         .Text(text)
            .FontSize(64)
            .Bold()
            .FontColor(QuestPDF.Helpers.Colors.Grey.Lighten3);
    }

    private void BuildLetterhead(QuestPDF.Infrastructure.IContainer c, string docTypeLabel, string title, string? caseId, string? docId)
    {
        c.Column(mainCol =>
        {
            mainCol.Item().PaddingBottom(6).Row(row =>
        {
            // “Brasão” genérico (sem Radius)
            row.RelativeItem(1).Column(col =>
            {
                col.Item().Height(36).Width(36).Background(QuestPDF.Helpers.Colors.Grey.Darken2);
                col.Item().Text("DEPARTAMENTO DE POLÍCIA MUNICIPAL")
                          .Bold().FontSize(11.5f);
            });

            row.RelativeItem(1).AlignRight().Column(col =>
            {
                col.Item().Text(docTypeLabel).Bold().FontSize(13);
                if (!string.IsNullOrWhiteSpace(docId))
                    col.Item().Text($"DocId: {docId}").FontSize(9.5f).FontColor(QuestPDF.Helpers.Colors.Grey.Darken2);
                if (!string.IsNullOrWhiteSpace(caseId))
                    col.Item().Text($"CaseId: {caseId}").FontSize(9.5f).FontColor(QuestPDF.Helpers.Colors.Grey.Darken2);
                col.Item().Text($"Emitido em: {DateTimeOffset.Now:yyyy-MM-dd HH:mm (zzz)}")
                          .FontSize(9.5f).FontColor(QuestPDF.Helpers.Colors.Grey.Darken2);
            });
        });

            // Título
            mainCol.Item().PaddingTop(2).Text(title).FontSize(14).Bold();
        });
    }

    private void BuildClassificationBand(IContainer c, string classification)
    {
        c.PaddingTop(4).BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Row(r =>
        {
            r.RelativeItem().Text(classification).FontSize(9.5f).FontColor(Colors.Grey.Darken2);
        });
    }

    private void BuildClassificationBand(IContainer c, string classification, string bandBg, string textColor)
    {
        c.PaddingTop(4)
         .Background(bandBg)
         .BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
         .Padding(4)
         .Row(r => { r.RelativeItem().Text(classification).FontSize(9.5f).FontColor(textColor).SemiBold(); });
    }

    private (string BandBg, string BandText) GetThemeColors(string documentType)
    {
        return documentType.ToLower() switch
        {
            "police_report" => (Colors.Blue.Lighten5, Colors.Blue.Darken2),
            "forensics_report" => (Colors.Indigo.Lighten5, Colors.Indigo.Darken2),
            "interview" => (Colors.Amber.Lighten5, Colors.Amber.Darken3),
            "evidence_log" => (Colors.Teal.Lighten5, Colors.Teal.Darken2),
            "memo" or "memo_admin" => (Colors.Grey.Lighten4, Colors.Grey.Darken2),
            "witness_statement" => (Colors.DeepOrange.Lighten5, Colors.DeepOrange.Darken2),
            _ => (Colors.Grey.Lighten4, Colors.Grey.Darken2)
        };
    }

    private string GetDocumentHeader(string documentType, string title)
    {
        return documentType.ToLower() switch
        {
            "police_report" => "POLICE INCIDENT REPORT",
            "forensics_report" => "FORENSIC ANALYSIS REPORT",
            "interview" => "INVESTIGATION INTERVIEW TRANSCRIPT",
            "evidence_log" => "EVIDENCE CATALOG & CHAIN OF CUSTODY",
            "memo" => "INVESTIGATIVE MEMORANDUM",
            "witness_statement" => "WITNESS STATEMENT FORM",
            _ => "INVESTIGATIVE DOCUMENT"
        };
    }

    private string FormatMarkdownContent(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return "No content available.";

        // Basic markdown to text conversion (for non-table content)
        return markdown
            .Replace("**", "")  // Remove bold markers
            .Replace("*", "")   // Remove italic markers
            .Replace("#", "")   // Remove headers
            .Replace("- ", "• ") // Convert bullets
            .Trim();
    }

    private void RenderMarkdownContent(QuestPDF.Fluent.ColumnDescriptor column, string markdownContent)
    {
        if (string.IsNullOrWhiteSpace(markdownContent))
        {
            column.Item().Text("Sem conteúdo.");
            return;
        }

        var lines = markdownContent.Replace("\r\n", "\n").Split('\n');
        var i = 0;
        bool skippedFirstH1 = false;

        while (i < lines.Length)
        {
            var raw = lines[i];
            var line = raw.Trim();

            if (line.Length == 0)
            {
                column.Item().PaddingVertical(2);
                i++;
                continue;
            }

            // Ignore primeiro H1 (já está no letterhead)
            if (line.StartsWith("# ") && !skippedFirstH1)
            {
                skippedFirstH1 = true;
                i++;
                continue;
            }

            // Tabela markdown
            if (IsTableLine(line) && i + 1 < lines.Length && IsTableSeparatorLine(lines[i + 1]))
            {
                var tableLines = new List<string> { line, lines[i + 1] };
                i += 2;
                while (i < lines.Length && IsTableLine(lines[i])) { tableLines.Add(lines[i]); i++; }
                RenderTable(column, tableLines);
                continue;
            }

            // Blockquote
            if (line.StartsWith("> "))
            {
                var quote = line.Substring(2);
                column.Item().Background(QuestPDF.Helpers.Colors.Grey.Lighten4)
                             .BorderLeft(3).BorderColor(QuestPDF.Helpers.Colors.Grey.Medium)
                             .Padding(6)
                             .Text(quote).FontSize(10).FontColor(QuestPDF.Helpers.Colors.Grey.Darken2);
                i++;
                continue;
            }

            // Cabeçalhos
            if (line.StartsWith("## "))
            {
                var h = line.Substring(3);
                column.Item().PaddingTop(8).Text(h).FontSize(12).Bold();
                i++;
                continue;
            }
            if (line.StartsWith("# "))
            {
                var h = line.Substring(2);
                column.Item().PaddingTop(10).Text(h).FontSize(13).Bold();
                i++;
                continue;
            }

            // Lista numerada (render simples)
            if (System.Text.RegularExpressions.Regex.IsMatch(line, @"^\d+\.\s+"))
            {
                var text = System.Text.RegularExpressions.Regex.Replace(line, @"^\d+\.\s+", "");
                column.Item().Row(r =>
                {
                    r.ConstantItem(12).Text("•");
                    r.RelativeItem().Text(text);
                });
                i++;
                continue;
            }

            // Bullets
            if (line.StartsWith("- ") || line.StartsWith("* "))
            {
                var text = line.Substring(2);
                column.Item().Row(r =>
                {
                    r.ConstantItem(12).Text("•");
                    r.RelativeItem().Text(text);
                });
                i++;
                continue;
            }

            // Timestamps -> monoespaçado
            if (System.Text.RegularExpressions.Regex.IsMatch(line, @"\d{4}-\d{2}-\d{2}T"))
            {
                column.Item().Text(line).FontFamily("Courier New").FontSize(10);
                i++;
                continue;
            }

            // Texto normal
            column.Item().Text(line);
            i++;
        }
    }

    private void RenderByType(ColumnDescriptor col, string documentType, string markdownContent, string? caseId, string? docId)
    {
        switch (documentType.ToLower())
        {
            case "police_report":
                RenderPoliceReport(col, markdownContent, caseId, docId);
                break;
            case "forensics_report":
                RenderForensicsReport(col, markdownContent);
                break;
            case "interview":
                RenderInterview(col, markdownContent);
                break;
            case "evidence_log":
                RenderEvidenceLog(col, markdownContent);
                break;
            default:
                RenderGeneric(col, markdownContent);
                break;
        }
    }

    private void RenderPoliceReport(ColumnDescriptor col, string md, string? caseId, string? docId)
    {
        col.Item().PaddingBottom(6).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Row(r =>
        {
            r.RelativeItem().Column(c =>
            {
                c.Item().Text("Unidade/Agente: __________________").FontSize(9.5f).FontColor(Colors.Grey.Darken2);
                c.Item().Text("Contato: ________________________").FontSize(9.5f).FontColor(Colors.Grey.Darken2);
            });
            r.RelativeItem().Column(c =>
            {
                c.Item().Text($"Nº B.O.: {(docId ?? "________")}").FontSize(9.5f).FontColor(Colors.Grey.Darken2);
                c.Item().Text($"Data/Hora: {DateTimeOffset.Now:yyyy-MM-dd HH:mm (zzz)}").FontSize(9.5f).FontColor(Colors.Grey.Darken2);
            });
            r.RelativeItem().AlignRight().Column(c =>
            {
                c.Item().Text($"CaseId: {(caseId ?? "________")}").FontSize(9.5f).FontColor(Colors.Grey.Darken2);
                c.Item().Text("Classificação: Confidencial").FontSize(9.5f).FontColor(Colors.Grey.Darken2);
            });
        });

        RenderMarkdownContent(col, md);
    }

    private void RenderForensicsReport(ColumnDescriptor col, string md)
    {
        col.Item().Background(Colors.Indigo.Lighten5).Padding(6)
           .Text(t =>
           {
               t.DefaultTextStyle(TextStyle.Default.FontSize(9.5f).FontColor(Colors.Indigo.Darken2));
               t.Span("Este laudo segue protocolos periciais. ");
               t.Span("Sempre registrar cadeia de custódia ao final.").SemiBold();
           });

        RenderMarkdownContent(col, md);

        col.Item().PaddingTop(6).BorderTop(1).BorderColor(Colors.Indigo.Lighten2)
           .Text("— Fim do Laudo / Cadeia de Custódia acima —").FontSize(9).FontColor(Colors.Grey.Darken1);
    }

    private void RenderInterview(ColumnDescriptor col, string md)
    {
        col.Item().Background(Colors.Amber.Lighten5).Padding(6)
           .Text(t =>
           {
               t.DefaultTextStyle(TextStyle.Default.FontSize(9.5f).FontColor(Colors.Amber.Darken3));
               t.Span("Transcrição integral, sem comentários do entrevistador. ");
               t.Span("Rotulagem: ").FontColor(Colors.Amber.Darken3);
               t.Span("**Entrevistador:** / **Entrevistado(a):**").SemiBold();
           });

        RenderMarkdownContent(col, md);
    }

    private void RenderEvidenceLog(ColumnDescriptor col, string md)
    {
        col.Item().Background(Colors.Teal.Lighten5).Padding(6)
           .Text(t =>
           {
               t.DefaultTextStyle(TextStyle.Default.FontColor(Colors.Teal.Darken2));
               t.Span("Catálogo de itens e cadeia de custódia. ");
               t.Span("Campos: ItemId, Coleta em, Coletado por, Descrição, Armazenamento, Transferências.")
                .FontSize(9.5f);
           });

        RenderMarkdownContent(col, md);
    }

    // --- fallback: genérico ---
    private void RenderGeneric(ColumnDescriptor col, string md) => RenderMarkdownContent(col, md);

    private bool IsTableLine(string line)
    {
        return line.Contains("|") && line.Split('|').Length > 2;
    }

    private bool IsTableSeparatorLine(string line)
    {
        return line.Contains("|") && line.Contains("-");
    }

    private void RenderTable(ColumnDescriptor column, List<string> tableLines)
    {
        if (tableLines.Count < 3) return;

        var headerLine = tableLines[0];
        var dataLines = tableLines.Skip(2).ToList();

        var headers = headerLine.Split('|')
                                .Skip(1)
                                .TakeWhile(_ => true)
                                .ToArray();

        // Limpa bordas vazias
        headers = headers.Take(headers.Length - 1).Select(h => h.Trim()).ToArray();
        var colCount = headers.Length;
        if (colCount == 0) return;

        column.Item().PaddingVertical(4);

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                for (int i = 0; i < colCount; i++)
                    cols.RelativeColumn();
            });

            // Cabeçalho
            table.Header(h =>
            {
                for (int i = 0; i < colCount; i++)
                {
                    h.Cell().Background(Colors.Grey.Lighten3).Padding(6)
                        .BorderBottom(1).BorderColor(Colors.Grey.Medium)
                        .Text(headers[i]).FontSize(10).Bold();
                }
            });

            // Linhas
            var zebra = false;
            foreach (var line in dataLines)
            {
                var rawCells = line.Split('|').Skip(1).ToArray();
                rawCells = rawCells.Take(colCount).ToArray();

                for (int i = 0; i < colCount; i++)
                {
                    var text = i < rawCells.Length ? rawCells[i].Trim() : "";
                    var cell = table.Cell().Background(zebra ? Colors.Grey.Lighten5 : Colors.White)
                                        .BorderBottom(0.5f)
                                        .BorderColor(Colors.Grey.Lighten2)
                                        .Padding(5);
                    cell.Text(text).FontSize(9.5f);
                }
                zebra = !zebra;
            }
        });

        column.Item().PaddingBottom(4);
    }

    // Updated method that calls the new realistic PDF generator
    private string GenerateSimplePdfContent(string title, string markdownContent)
    {
        try
        {
            // Determine document type from title
            string documentType = DetermineDocumentType(title);

            // Generate realistic PDF using QuestPDF
            var pdfBytes = GenerateRealisticPdf(title, markdownContent, documentType);

            // Convert to base64 string for storage/transmission
            return Convert.ToBase64String(pdfBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating PDF content for title: {Title}", title);

            // Fallback to simple text content
            return $"PDF_CONTENT_ERROR: {title}\n\n{markdownContent}";
        }
    }

    private string DetermineDocumentType(string title)
    {
        var titleLower = title.ToLower();

        if (titleLower.Contains("police") || titleLower.Contains("incident") || titleLower.Contains("report"))
            return "police_report";
        if (titleLower.Contains("forensic") || titleLower.Contains("lab") || titleLower.Contains("analysis"))
            return "forensics_report";
        if (titleLower.Contains("interview") || titleLower.Contains("interrogation"))
            return "interview";
        if (titleLower.Contains("evidence") || titleLower.Contains("log") || titleLower.Contains("inventory"))
            return "evidence_log";
        if (titleLower.Contains("memo") || titleLower.Contains("memorandum"))
            return "memo";
        if (titleLower.Contains("witness") || titleLower.Contains("statement"))
            return "witness_statement";

        return "general";
    }

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
        var actualDocumentType = DetermineDocumentType(title);
        if (!string.IsNullOrEmpty(documentType) && documentType != "general")
        {
            actualDocumentType = documentType;
        }

        return GenerateRealisticPdf(title, markdownContent, actualDocumentType);
    }
}