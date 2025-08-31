using CaseGen.Functions.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CaseGen.Functions.Services;

public class CaseGenerationService : ICaseGenerationService
{
    private readonly ILLMService _llmService;
    private readonly IStorageService _storageService;
    private readonly ISchemaValidationService _schemaValidationService;
    private readonly IConfiguration _configuration;
    private readonly IJsonSchemaProvider _schemaProvider;
    private readonly ILogger<CaseGenerationService> _logger;

    public CaseGenerationService(
        ILLMService llmService,
        IStorageService storageService,
        ISchemaValidationService schemaValidationService,
        IJsonSchemaProvider schemaProvider,
        IConfiguration configuration,
        ILogger<CaseGenerationService> logger)
    {
        _llmService = llmService;
        _schemaProvider = schemaProvider;
        _storageService = storageService;
        _schemaValidationService = schemaValidationService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> PlanCaseAsync(CaseGenerationRequest request, CancellationToken cancellationToken = default)
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
        return await _llmService.GenerateStructuredAsync(systemPrompt, userPrompt, jsonSchema, cancellationToken);
    }

    public async Task<string> ExpandCaseAsync(string planJson, CancellationToken cancellationToken = default)
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

        return await _llmService.GenerateStructuredAsync(systemPrompt, userPrompt, jsonSchema, cancellationToken);
    }

    public async Task<string> DesignCaseAsync(string expandedJson, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Designing case structure with structured output");

        var systemPrompt = """
            Você é um designer de casos investigativos. Sua tarefa é transformar os detalhes expandidos 
            do caso em especificações estruturadas para geração paralela de documentos e mídias.
            
            IMPORTANTE:
            - Saída APENAS JSON válido no schema DocumentAndMediaSpecs
            - NÃO adicione explicações, comentários ou texto extra
            - Use i18nKey para todos os títulos (formato: categoria.identificador)
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
            - Todos com i18nKey apropriadas
            - LengthTarget adequado ao nível (documentos curtos: 150-400 palavras)
            
            Exemplos de i18nKey:
            - documents.police_report_001
            - documents.interview_suspect_main
            - documents.forensics_ballistics
            - media.crime_scene_photo_001
            - media.weapon_evidence_photo
            """;

        var jsonSchema = _schemaProvider.GetSchema("DocumentAndMediaSpecs");

        // Generate structured response with retry logic for validation
        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var response = await _llmService.GenerateStructuredAsync(systemPrompt, userPrompt, jsonSchema, cancellationToken);

                // Validate the response against schema
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

    public async Task<string[]> GenerateDocumentsAsync(string designJson, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating case documents from structured specs");

        var specs = await _schemaValidationService.ParseAndValidateAsync(designJson);
        if (specs is null) throw new InvalidOperationException("Invalid design specs");

        var tasks = specs.DocumentSpecs.Select(async spec =>
        {
            // escolhe prompt base por tipo
            var systemPrompt = spec.Type switch
            {
                DocumentTypes.PoliceReport => """
                Você é um especialista em documentação policial. 
                Gere APENAS Markdown estruturado seguindo as seções.
                """,
                DocumentTypes.Interview => """
                Você é um entrevistador forense. 
                Gere APENAS Markdown, com perguntas/respostas e marcações de tempo quando natural.
                """,
                DocumentTypes.MemoAdmin => """
                Você é um administrador. 
                Gere APENAS Markdown, tom burocrático conciso.
                """,
                DocumentTypes.ForensicsReport => """
                Você é um perito. 
                Gere APENAS Markdown, incluindo seção obrigatória 'Cadeia de Custódia'.
                """,
                DocumentTypes.EvidenceLog => """
                Você é responsável pelo registro de evidências. 
                Gere APENAS Markdown com tabela/lista padronizada.
                """,
                DocumentTypes.WitnessStatement => """
                Você é um escrivão. 
                Gere APENAS Markdown com declaração coesa da testemunha.
                """,
                _ => "Gere APENAS Markdown."
            };

            var userPrompt = $"""
            Gere um documento do tipo: {spec.Type}
            Título: {spec.Title} (i18nKey: {spec.I18nKey})

            Seções (na ordem):
            {string.Join("\n", spec.Sections.Select(s => "- " + s))}

            Tamanho alvo (palavras): {spec.LengthTarget[0]}–{spec.LengthTarget[1]}

            Regras:
            - Não resolva o caso.
            - Seja realista e consistente com práticas policiais/periciais.
            - Se for laudo, inclua a seção 'Cadeia de Custódia'.
            """;

            var markdown = await _llmService.GenerateAsync(systemPrompt, userPrompt, cancellationToken);
            return markdown;
        });

        return await Task.WhenAll(tasks);
    }


    public async Task<string[]> GenerateMediaAsync(string designJson, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating media prompts from structured specs");

        var specs = await _schemaValidationService.ParseAndValidateAsync(designJson);
        if (specs is null) throw new InvalidOperationException("Invalid design specs");

        var tasks = specs.MediaSpecs
            .Where(m => !m.Deferred) // ignore o que não é suportado agora
            .Select(async m =>
        {
            // monte um prompt final combinando prompt + constraints em bullet points
            var constraints = (m.Constraints is { Count: > 0 })
                ? "\n\nConstraints:\n" + string.Join("\n", m.Constraints.Select(kv => $"- {kv.Key}: {kv.Value}"))
                : string.Empty;

            var systemPrompt = """
            Você é um especialista em criação de prompts para geração de imagens realistas (estilo documentação forense).
            Gere APENAS o texto do prompt final.
            """;

            var userPrompt = $"""
            Tipo de mídia: {m.Kind}
            Título: {m.Title} (i18nKey: {m.I18nKey})

            Prompt base:
            {m.Prompt}
            {constraints}
            """;

            var finalPrompt = await _llmService.GenerateAsync(systemPrompt, userPrompt, cancellationToken);
            return finalPrompt;
        });

        return await Task.WhenAll(tasks);
    }

    public async Task<string> NormalizeCaseAsync(string[] documents, string[] media, CancellationToken cancellationToken = default)
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

        return await _llmService.GenerateAsync(systemPrompt, userPrompt, cancellationToken);
    }

    public async Task<string> IndexCaseAsync(string normalizedJson, CancellationToken cancellationToken = default)
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

        return await _llmService.GenerateAsync(systemPrompt, userPrompt, cancellationToken);
    }

    public async Task<string> ValidateRulesAsync(string indexedJson, CancellationToken cancellationToken = default)
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

        return await _llmService.GenerateAsync(systemPrompt, userPrompt, cancellationToken);
    }

    public async Task<string> RedTeamCaseAsync(string validatedJson, CancellationToken cancellationToken = default)
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

        return await _llmService.GenerateAsync(systemPrompt, userPrompt, cancellationToken);
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

    public async Task<string> GenerateDocumentFromSpecAsync(DocumentSpec spec, string designJson, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Gen Doc[{DocId}] type={Type} title={Title}", spec.DocId, spec.Type, spec.Title);

        var systemPrompt = """
            Você é um escritor técnico policial. Gere um documento verossímil seguindo o tipo e seções requeridas.
            Requisitos:
            - Texto objetivo, com tom documental
            - Respeite as seções exigidas na ordem
            - Respeite o alvo de tamanho (min..max palavras)
            - Se gated=true, inclua a nota de acesso no rodapé
            - Saída **APENAS** JSON: { docId, type, title, i18nKey, sections: [{title, content}], words }
            """;

        var userPrompt = $"""
            CONTEXTO DO DESIGN (resumo estruturado):
            {designJson}

            ESPECIFICAÇÃO DO DOCUMENTO:
            docId: {spec.DocId}
            type: {spec.Type}
            title: {spec.Title}
            i18nKey: {spec.I18nKey}
            sections: {string.Join(", ", spec.Sections)}
            lengthTarget: [{spec.LengthTarget[0]}, {spec.LengthTarget[1]}]
            gated: {spec.Gated}
            gatingRule: {(spec.GatingRule != null ? $"{spec.GatingRule.Action} - {spec.GatingRule.Notes ?? "n/a"}" : "n/a")}

            Gere o JSON final do documento conforme instruções.
            """;

        return await _llmService.GenerateAsync(systemPrompt, userPrompt, cancellationToken);
    }

    public async Task<string> GenerateMediaFromSpecAsync(MediaSpec spec, string designJson, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Gen Media[{EvidenceId}] kind={Kind} title={Title}", spec.EvidenceId, spec.Kind, spec.Title);

        var systemPrompt = """
            Você é um engenheiro de evidências de mídia. Gere uma especificação JSON para criação de mídia.
            Requisitos:
            - Saída **APENAS** JSON: { evidenceId, kind, title, i18nKey, genPrompt, constraints }
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
            i18nKey: {spec.I18nKey}
            constraints: {(spec.Constraints != null && spec.Constraints.Any() ? string.Join(", ", spec.Constraints.Select(kv => $"{kv.Key}: {kv.Value}")) : "n/a")}

            Gere o JSON final da mídia conforme instruções (campo genPrompt obrigatório).
            """;

        return await _llmService.GenerateAsync(systemPrompt, userPrompt, cancellationToken);
    }

}