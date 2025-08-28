using CaseGen.Functions.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CaseGen.Functions.Services;

public class CaseGenerationService : ICaseGenerationService
{
    private readonly ILLMService _llmService;
    private readonly IStorageService _storageService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CaseGenerationService> _logger;

    public CaseGenerationService(
        ILLMService llmService,
        IStorageService storageService,
        IConfiguration configuration,
        ILogger<CaseGenerationService> logger)
    {
        _llmService = llmService;
        _storageService = storageService;
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
        _logger.LogInformation("Designing case structure");

        var systemPrompt = """
            Você é um designer de experiências educacionais. Transforme os detalhes do caso em uma 
            estrutura de jogo investigativo bem organizada e pedagogicamente eficaz.
            """;

        var userPrompt = $"""
            Projete a estrutura final do caso baseado nestes detalhes expandidos:
            
            {expandedJson}
            
            Organize em:
            - Fluxo de investigação lógico
            - Pontos de decisão do jogador
            - Sistema de pistas e revelações
            - Critérios de avaliação
            - Solução final
            """;

        return await _llmService.GenerateAsync(systemPrompt, userPrompt, cancellationToken);
    }

    public async Task<string[]> GenerateDocumentsAsync(string designJson, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating case documents");

        var documents = new List<string>();

        // Generate multiple document types
        var documentTypes = new[]
        {
            "Relatório Policial Inicial",
            "Depoimentos de Testemunhas",
            "Laudo Pericial",
            "Relatório de Evidências"
        };

        foreach (var docType in documentTypes)
        {
            var systemPrompt = $"""
                Você é um especialista em documentação policial. Crie um {docType} realista e detalhado 
                para o caso investigativo.
                """;

            var userPrompt = $"""
                Baseado neste design de caso, crie um {docType}:
                
                {designJson}
                
                O documento deve ser profissional, realista e conter informações relevantes para a investigação.
                """;

            var document = await _llmService.GenerateAsync(systemPrompt, userPrompt, cancellationToken);
            documents.Add(document);
        }

        return documents.ToArray();
    }

    public async Task<string[]> GenerateMediaAsync(string designJson, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating media prompts for case");

        var mediaPrompts = new List<string>();

        // Generate image prompts for different evidence types
        var imageTypes = new[]
        {
            "Cena do Crime",
            "Evidência Física",
            "Suspeito",
            "Local do Incidente"
        };

        foreach (var imageType in imageTypes)
        {
            var systemPrompt = $"""
                Você é um especialista em criação de prompts para geração de imagens. Crie prompts 
                detalhados para gerar imagens realistas de {imageType} para casos investigativos.
                """;

            var userPrompt = $"""
                Baseado neste design de caso, crie um prompt detalhado para gerar uma imagem de {imageType}:
                
                {designJson}
                
                O prompt deve ser específico, realista e apropriado para um caso de treinamento.
                """;

            var prompt = await _llmService.GenerateAsync(systemPrompt, userPrompt, cancellationToken);
            mediaPrompts.Add(prompt);
        }

        return mediaPrompts.ToArray();
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
}