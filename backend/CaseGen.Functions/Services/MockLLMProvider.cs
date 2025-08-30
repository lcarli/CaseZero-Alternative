using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CaseGen.Functions.Services;

public class MockLLMProvider : ILLMProvider
{
    private readonly ILogger<MockLLMProvider> _logger;

    public MockLLMProvider(ILogger<MockLLMProvider> logger)
    {
        _logger = logger;
    }

    public async Task<string> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        // Simulate processing time
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        
        return GenerateMockResponse(systemPrompt, userPrompt);
    }

    public async Task<string> GenerateStructuredResponseAsync(string systemPrompt, string userPrompt, string jsonSchema, CancellationToken cancellationToken = default)
    {
        // Simulate processing time
        await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        
        return GenerateMockStructuredResponse(systemPrompt, userPrompt, jsonSchema);
    }

    private string GenerateMockResponse(string systemPrompt, string userPrompt)
    {
        // Generate context-appropriate mock responses based on the prompts
        if (userPrompt.Contains("Plan", StringComparison.OrdinalIgnoreCase))
        {
            return "Este é um caso de investigação criminal simulado gerado para treinamento. O incidente envolve um roubo em uma empresa de tecnologia durante o final de semana.";
        }
        
        if (userPrompt.Contains("Expand", StringComparison.OrdinalIgnoreCase))
        {
            return "Detalhes expandidos: Local: Escritório corporativo no 15º andar. Hora: Sábado, 23:30. Suspeitos: Funcionário com acesso privilegiado. Evidências: Sistema de segurança desabilitado, computadores removidos.";
        }
        
        if (userPrompt.Contains("Design", StringComparison.OrdinalIgnoreCase))
        {
            return "Design do caso: 3 suspeitos identificados, 5 evidências principais, 2 testemunhas, timeline de 4 horas. Complexidade: Média. Duração estimada: 60 minutos.";
        }
        
        return "Resposta gerada pelo sistema de IA para fins de demonstração.";
    }

    private string GenerateMockStructuredResponse(string systemPrompt, string userPrompt, string jsonSchema)
    {
        // Generate structured JSON responses based on the schema and prompts
        if (userPrompt.Contains("Plan", StringComparison.OrdinalIgnoreCase))
        {
            var planResponse = new
            {
                caseId = $"CASE-{DateTime.UtcNow:yyyyMMdd-HHmmss}",
                title = "Roubo em Empresa de Tecnologia",
                location = "São Paulo, SP - Edifício Corporativo",
                incidentType = "Roubo",
                difficulty = "Iniciante",
                estimatedDuration = 60,
                overview = "Investigação de roubo de equipamentos em empresa de tecnologia durante final de semana"
            };
            return JsonSerializer.Serialize(planResponse, new JsonSerializerOptions { WriteIndented = true });
        }
        
        if (userPrompt.Contains("Expand", StringComparison.OrdinalIgnoreCase))
        {
            var expandResponse = new
            {
                suspects = new[]
                {
                    new { name = "Joao Silva", role = "Funcionario TI", motive = "Problemas financeiros" },
                    new { name = "Maria Santos", role = "Seguranca", motive = "Acesso facilitado" },
                    new { name = "Carlos Lima", role = "Limpeza", motive = "Conhecimento da rotina" }
                },
                evidence = new[]
                {
                    new { type = "Digital", description = "Logs de acesso" },
                    new { type = "Fisico", description = "Pegadas no local" },
                    new { type = "Testemunhal", description = "Depoimento do porteiro" }
                },
                timeline = new[]
                {
                    new { time = "23:30", eventDescription = "Sistema de seguranca desabilitado" },
                    new { time = "23:45", eventDescription = "Entrada no edificio registrada" },
                    new { time = "01:15", eventDescription = "Saida com equipamentos" }
                }
            };
            return JsonSerializer.Serialize(expandResponse, new JsonSerializerOptions { WriteIndented = true });
        }
        
        return JsonSerializer.Serialize(new { status = "generated", message = "Mock structured response" }, new JsonSerializerOptions { WriteIndented = true });
    }
}
