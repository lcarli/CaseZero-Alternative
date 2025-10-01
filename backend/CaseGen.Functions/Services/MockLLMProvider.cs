using Microsoft.Extensions.Logging;
using System.Text.Json;
using CaseGen.Functions.Models;

namespace CaseGen.Functions.Services;

public class MockLLMProvider : ILLMProvider
{
    private readonly ILogger<MockLLMProvider> _logger;

    public MockLLMProvider(ILogger<MockLLMProvider> logger)
    {
        _logger = logger;
    }

    public async Task<LLMResponse> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        // Simulate processing time
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        
        var content = GenerateMockResponse(systemPrompt, userPrompt);
        
        // Mock token usage for testing
        var mockUsage = new LLMUsage
        {
            PromptTokens = (systemPrompt.Length + userPrompt.Length) / 4, // Rough approximation
            CompletionTokens = content.Length / 4,
            TotalTokens = ((systemPrompt.Length + userPrompt.Length) / 4) + (content.Length / 4)
        };
        
        return new LLMResponse
        {
            Content = content,
            Usage = mockUsage
        };
    }

    public async Task<LLMResponse> GenerateStructuredResponseAsync(string systemPrompt, string userPrompt, string jsonSchema, CancellationToken cancellationToken = default)
    {
        // Simulate processing time
        await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        
        var content = GenerateMockStructuredResponse(systemPrompt, userPrompt, jsonSchema);
        
        // Mock token usage for testing
        var mockUsage = new LLMUsage
        {
            PromptTokens = (systemPrompt.Length + userPrompt.Length + jsonSchema.Length) / 4,
            CompletionTokens = content.Length / 4,
            TotalTokens = ((systemPrompt.Length + userPrompt.Length + jsonSchema.Length) / 4) + (content.Length / 4)
        };
        
        return new LLMResponse
        {
            Content = content,
            Usage = mockUsage
        };
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

    public Task<byte[]> GenerateImageAsync(string prompt, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating image with prompt: {Prompt}", prompt);

            var placeholderImageBytes = CreatePlaceholderImage(prompt);
            
            _logger.LogInformation("Image generated successfully with size: {Size} bytes", placeholderImageBytes.Length);
            
            return Task.FromResult(placeholderImageBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Foundry image generation failed for prompt: {Prompt}", prompt);
            throw;
        }
    }

    private byte[] CreatePlaceholderImage(string prompt)
    {
        // Create a simple PNG placeholder image
        // This is a minimal PNG file (1x1 pixel, transparent)
        var pngBytes = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
            0x00, 0x00, 0x00, 0x0D, // IHDR chunk length
            0x49, 0x48, 0x44, 0x52, // IHDR
            0x00, 0x00, 0x00, 0x01, // Width: 1
            0x00, 0x00, 0x00, 0x01, // Height: 1
            0x08, 0x06, 0x00, 0x00, 0x00, // Bit depth: 8, Color type: 6 (RGBA), Compression: 0, Filter: 0, Interlace: 0
            0x1F, 0x15, 0xC4, 0x89, // CRC
            0x00, 0x00, 0x00, 0x0B, // IDAT chunk length
            0x49, 0x44, 0x41, 0x54, // IDAT
            0x78, 0x9C, 0x62, 0x00, 0x02, 0x00, 0x00, 0x05, 0x00, 0x01, 0x0D, // Compressed image data
            0x0A, 0x2D, 0xB4, // CRC
            0x00, 0x00, 0x00, 0x00, // IEND chunk length
            0x49, 0x45, 0x4E, 0x44, // IEND
            0xAE, 0x42, 0x60, 0x82  // CRC
        };

        return pngBytes;
    }
}
