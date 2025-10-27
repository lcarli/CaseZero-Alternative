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
            // Load all suspects from expand/suspects/ - query as string then parse to JsonElement
            var suspectsQueryRaw = await _contextManager.QueryContextAsync<string>(
                caseId, 
                "expand/suspects/*");
            
            var suspectsList = suspectsQueryRaw
                .Select(s => new 
                {
                    Path = s.Path,
                    Data = JsonDocument.Parse(s.Data).RootElement
                })
                .ToList();
            
            _logger.LogInformation("Found {Count} suspects to normalize", suspectsList.Count);

            // Load all evidence from expand/evidence/ - query as string then parse to JsonElement
            var evidenceQueryRaw = await _contextManager.QueryContextAsync<string>(
                caseId, 
                "expand/evidence/*");
            
            var evidenceList = evidenceQueryRaw
                .Select(e => new 
                {
                    Path = e.Path,
                    Data = JsonDocument.Parse(e.Data).RootElement
                })
                .ToList();
            
            _logger.LogInformation("Found {Count} evidence items to normalize", evidenceList.Count);

            // Normalize and save suspects
            foreach (var suspectResult in suspectsList)
            {
                try
                {
                    // suspectResult.Data is a JsonElement
                    var suspectElement = suspectResult.Data;
                    
                    var suspectId = suspectElement.GetProperty("suspectId").GetString();
                    if (string.IsNullOrEmpty(suspectId))
                        continue;

                    // SaveContextAsync will serialize the JsonElement properly (without double-encoding)
                    // Pass the JsonElement directly - don't serialize to string first
                    await _contextManager.SaveContextAsync(caseId, $"entities/suspects/{suspectId}", suspectElement);
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
                    // evidenceResult.Data is a JsonElement
                    var evidenceElement = evidenceResult.Data;
                    
                    var evidenceId = evidenceElement.GetProperty("evidenceId").GetString();
                    if (string.IsNullOrEmpty(evidenceId))
                        continue;

                    // SaveContextAsync will serialize the JsonElement properly (without double-encoding)
                    // Pass the JsonElement directly - don't serialize to string first
                    await _contextManager.SaveContextAsync(caseId, $"entities/evidence/{evidenceId}", evidenceElement);
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
