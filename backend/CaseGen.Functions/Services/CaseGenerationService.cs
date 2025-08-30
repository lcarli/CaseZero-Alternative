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
        _logger.LogInformation("Planning case: {Title}", request.Title);

        var systemPrompt = $"""
            Você é um arquiteto de casos investigativos experiente. Sua tarefa é criar um plano inicial 
            para um caso detetivesco baseado nos parâmetros fornecidos.
            
            Nível de dificuldade: {request.Difficulty}
            Duração estimada: {request.TargetDurationMinutes} minutos
            
            Crie um plano estruturado que seja adequado para o nível de dificuldade especificado.
            """;

        var userPrompt = $"""
            Crie um plano para o seguinte caso:
            
            Título: {request.Title}
            Local: {request.Location}
            Duração alvo: {request.TargetDurationMinutes} minutos
            Dificuldade: {request.Difficulty}
            Gerar imagens: {request.GenerateImages}
            Restrições: {string.Join(", ", request.Constraints)}
            
            O plano deve incluir: tipo de crime, visão geral, objetivos de aprendizado, 
            estrutura básica do caso, e elementos principais a serem desenvolvidos.
            """;

        var jsonSchema = """
            {
              "type": "object",
              "properties": {
                "caseId": {"type": "string"},
                "title": {"type": "string"},
                "location": {"type": "string"},
                "incidentType": {"type": "string"},
                "difficulty": {"type": "string"},
                "estimatedDuration": {"type": "number"},
                "overview": {"type": "string"},
                "learningObjectives": {"type": "array", "items": {"type": "string"}},
                "mainElements": {"type": "array", "items": {"type": "string"}}
              },
              "required": ["caseId", "title", "location", "incidentType", "difficulty", "estimatedDuration", "overview"]
            }
            """;

        return await _llmService.GenerateStructuredAsync(systemPrompt, userPrompt, jsonSchema, cancellationToken);
    }

    public async Task<string> ExpandCaseAsync(string planJson, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Expanding case from plan");

        var systemPrompt = """
            Você é um especialista em desenvolvimento de casos investigativos. Expanda o plano inicial 
            criando detalhes completos para suspeitos, evidências, cronologia e testemunhas.
            """;

        var userPrompt = $"""
            Expanda este plano de caso em detalhes completos:
            
            {planJson}
            
            Inclua:
            - Lista detalhada de suspeitos com perfis e motivos
            - Evidências principais e secundárias
            - Cronologia detalhada dos eventos
            - Testemunhas e seus depoimentos
            - Localizações específicas
            """;

        var jsonSchema = """
            {
              "type": "object",
              "properties": {
                "suspects": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "properties": {
                      "name": {"type": "string"},
                      "role": {"type": "string"},
                      "motive": {"type": "string"},
                      "alibi": {"type": "string"},
                      "background": {"type": "string"}
                    }
                  }
                },
                "evidence": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "properties": {
                      "type": {"type": "string"},
                      "description": {"type": "string"},
                      "location": {"type": "string"},
                      "significance": {"type": "string"}
                    }
                  }
                },
                "timeline": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "properties": {
                      "time": {"type": "string"},
                      "event": {"type": "string"},
                      "location": {"type": "string"},
                      "witness": {"type": "string"}
                    }
                  }
                },
                "witnesses": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "properties": {
                      "name": {"type": "string"},
                      "role": {"type": "string"},
                      "testimony": {"type": "string"},
                      "reliability": {"type": "string"}
                    }
                  }
                }
              }
            }
            """;

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