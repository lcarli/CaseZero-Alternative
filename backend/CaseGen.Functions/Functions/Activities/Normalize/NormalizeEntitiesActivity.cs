using System.Text.Json;
using CaseGen.Functions.Models;
using CaseGen.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Functions.Activities.Normalize;

/// <summary>
/// Phase 6 Task 6.1: Normalizes suspects, evidence, and witnesses from expand stage
/// into final entity format and saves to context/entities/
/// </summary>
public class NormalizeEntitiesActivity
{
    private readonly ILogger<NormalizeEntitiesActivity> _logger;
    private readonly IContextManager _contextManager;
    private readonly IStorageService _storageService;

    public NormalizeEntitiesActivity(
        ILogger<NormalizeEntitiesActivity> logger,
        IContextManager contextManager,
        IStorageService storageService)
    {
        _logger = logger;
        _contextManager = contextManager;
        _storageService = storageService;
    }

    [Function("NormalizeEntitiesActivity")]
    public async Task<string> Run([ActivityTrigger] NormalizeEntitiesActivityModel model)
    {
        var caseId = model.CaseId;
        _logger.LogInformation("Normalizing entities for case {CaseId}", caseId);

        var normalizedCount = new
        {
            Suspects = 0,
            Evidence = 0,
            Witnesses = 0
        };

        try
        {
            // Load all suspects from expand/suspects/ using QueryContextAsync
            var suspectsQuery = await _contextManager.QueryContextAsync<object>(
                caseId, 
                "expand/suspects/*");
            
            var suspectsList = suspectsQuery.ToList();

            // Load all evidence from expand/evidence/ using QueryContextAsync
            var evidenceQuery = await _contextManager.QueryContextAsync<object>(
                caseId, 
                "expand/evidence/*");
            
            var evidenceList = evidenceQuery.ToList();

            // Normalize and save suspects
            foreach (var suspectResult in suspectsList)
            {
                try
                {
                    // Convert object back to JSON
                    var suspectJson = JsonSerializer.Serialize(suspectResult.Data);
                    var suspectElement = JsonDocument.Parse(suspectJson).RootElement;
                    
                    var suspectId = suspectElement.GetProperty("suspectId").GetString();
                    if (string.IsNullOrEmpty(suspectId))
                        continue;

                    // Save to entities/suspects/{id}.json
                    var normalizedJson = JsonSerializer.Serialize(suspectElement, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                    
                    await _contextManager.SaveContextAsync(caseId, $"entities/suspects/{suspectId}.json", normalizedJson);
                    normalizedCount = normalizedCount with { Suspects = normalizedCount.Suspects + 1 };
                    
                    _logger.LogInformation("Normalized suspect {SuspectId}", suspectId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to normalize suspect");
                }
            }

            // Normalize and save evidence
            foreach (var evidenceResult in evidenceList)
            {
                try
                {
                    // Convert object back to JSON
                    var evidenceJson = JsonSerializer.Serialize(evidenceResult.Data);
                    var evidenceElement = JsonDocument.Parse(evidenceJson).RootElement;
                    
                    var evidenceId = evidenceElement.GetProperty("evidenceId").GetString();
                    if (string.IsNullOrEmpty(evidenceId))
                        continue;

                    // Save to entities/evidence/{id}.json
                    var normalizedJson = JsonSerializer.Serialize(evidenceElement, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                    
                    await _contextManager.SaveContextAsync(caseId, $"entities/evidence/{evidenceId}.json", normalizedJson);
                    normalizedCount = normalizedCount with { Evidence = normalizedCount.Evidence + 1 };
                    
                    _logger.LogInformation("Normalized evidence {EvidenceId}", evidenceId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to normalize evidence");
                }
            }

            // TODO: Extract witnesses if they exist in suspect data
            // For now, witnesses are part of suspects/evidence relationships

            _logger.LogInformation(
                "Normalized entities for case {CaseId}: {Suspects} suspects, {Evidence} evidence, {Witnesses} witnesses",
                caseId, normalizedCount.Suspects, normalizedCount.Evidence, normalizedCount.Witnesses);

            return JsonSerializer.Serialize(normalizedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to normalize entities for case {CaseId}", caseId);
            throw;
        }
    }
}
