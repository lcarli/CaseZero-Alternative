using CaseZeroApi.Services;
using CaseZeroApi.Data;
using CaseZeroApi.Models;
using Microsoft.EntityFrameworkCore;

namespace CaseZeroApi.Services
{
    public interface ICaseProcessingService
    {
        Task ProcessNewCasesAsync();
        Task ProcessCaseAsync(string caseId);
        Task<bool> IsCaseAlreadyProcessedAsync(string caseId);
    }

    public class CaseProcessingService : ICaseProcessingService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICaseObjectService _caseObjectService;
        private readonly ILogger<CaseProcessingService> _logger;
        private readonly IConfiguration _configuration;

        public CaseProcessingService(
            ApplicationDbContext context,
            ICaseObjectService caseObjectService,
            ILogger<CaseProcessingService> logger,
            IConfiguration configuration)
        {
            _context = context;
            _caseObjectService = caseObjectService;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task ProcessNewCasesAsync()
        {
            try
            {
                _logger.LogInformation("Starting case processing scan...");

                // Get source: local folder or blob storage
                var useBlob = _configuration.GetValue<bool>("CaseProcessing:UseBlobStorage", false);
                var blobUrl = _configuration.GetValue<string>("CaseProcessing:BlobStorageUrl");

                List<string> availableCases;
                
                if (useBlob && !string.IsNullOrEmpty(blobUrl))
                {
                    // TODO: Implement blob storage case scanning
                    _logger.LogInformation("Blob storage case processing not yet implemented. Using local folder.");
                    availableCases = await _caseObjectService.GetAvailableCasesAsync();
                }
                else
                {
                    // Scan local cases folder
                    availableCases = await _caseObjectService.GetAvailableCasesAsync();
                }

                var processedCount = 0;
                var skippedCount = 0;

                foreach (var caseId in availableCases)
                {
                    try
                    {
                        if (await IsCaseAlreadyProcessedAsync(caseId))
                        {
                            _logger.LogDebug("Case {CaseId} already processed, skipping", caseId);
                            skippedCount++;
                            continue;
                        }

                        await ProcessCaseAsync(caseId);
                        processedCount++;
                        _logger.LogInformation("Successfully processed new case: {CaseId}", caseId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process case {CaseId}", caseId);
                    }
                }

                _logger.LogInformation("Case processing completed. Processed: {ProcessedCount}, Skipped: {SkippedCount}, Total: {TotalCount}", 
                    processedCount, skippedCount, availableCases.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during case processing scan");
            }
        }

        public async Task ProcessCaseAsync(string caseId)
        {
            try
            {
                // Load case object from filesystem
                var caseObject = await _caseObjectService.LoadCaseObjectAsync(caseId);
                if (caseObject?.Metadata == null)
                {
                    throw new InvalidOperationException($"Could not load case object for {caseId}");
                }

                // Check if case already exists
                var existingCase = await _context.Cases.FindAsync(caseId);
                if (existingCase != null)
                {
                    _logger.LogInformation("Case {CaseId} already exists in database, skipping", caseId);
                    return;
                }

                // Create case entity
                var newCase = new Case
                {
                    Id = caseId,
                    Title = caseObject.Metadata.Title,
                    Description = caseObject.Metadata.Description,
                    Status = CaseStatus.Open,
                    Priority = GetPriorityFromDifficulty(caseObject.Metadata.Difficulty),
                    CreatedAt = DateTime.UtcNow,
                    Type = CaseType.Investigation,
                    MinimumRankRequired = ParseRankFromString(caseObject.Metadata.MinRankRequired),
                    Location = caseObject.Metadata.Location,
                    IncidentDate = caseObject.Metadata.IncidentDateTime,
                    BriefingText = caseObject.Metadata.Briefing,
                    VictimInfo = System.Text.Json.JsonSerializer.Serialize(caseObject.Metadata.VictimInfo),
                    HasMultipleSuspects = caseObject.Suspects?.Count > 1,
                    EstimatedDifficultyLevel = caseObject.Metadata.Difficulty,
                    CorrectSuspectName = caseObject.Solution?.Culprit ?? "Unknown",
                    MaxScore = 100.0
                };

                _context.Cases.Add(newCase);

                // Process evidences
                if (caseObject.Evidences != null)
                {
                    foreach (var evidence in caseObject.Evidences)
                    {
                        var evidenceEntity = new Evidence
                        {
                            CaseId = caseId,
                            Name = evidence.Name,
                            Type = evidence.Type,
                            Description = evidence.Description,
                            FilePath = $"{evidence.Location}/{evidence.FileName}",
                            IsUnlocked = evidence.IsUnlocked,
                            RequiresAnalysis = evidence.RequiresAnalysis,
                            Category = ParseEvidenceCategory(evidence.Category),
                            Priority = ParseEvidencePriority(evidence.Priority),
                            DependsOnEvidenceIds = System.Text.Json.JsonSerializer.Serialize(evidence.DependsOn),
                            AnalysisStatus = evidence.IsUnlocked ? EvidenceStatus.Available : EvidenceStatus.Available
                        };
                        
                        _context.Evidences.Add(evidenceEntity);
                    }
                }

                // Process suspects
                if (caseObject.Suspects != null)
                {
                    foreach (var suspect in caseObject.Suspects)
                    {
                        var suspectEntity = new Suspect
                        {
                            CaseId = caseId,
                            Name = suspect.Name,
                            Alias = suspect.Alias,
                            Age = suspect.Age,
                            Description = suspect.Description,
                            Motive = suspect.Motive,
                            Alibi = suspect.Alibi,
                            HasAlibiVerified = suspect.AlibiVerified,
                            Status = SuspectStatus.Active
                        };

                        _context.Suspects.Add(suspectEntity);
                    }
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully processed and stored case {CaseId} in database", caseId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing case {CaseId}", caseId);
                throw;
            }
        }

        public async Task<bool> IsCaseAlreadyProcessedAsync(string caseId)
        {
            try
            {
                return await _context.Cases.AnyAsync(c => c.Id == caseId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if case {CaseId} is already processed", caseId);
                return false;
            }
        }

        private static CasePriority GetPriorityFromDifficulty(int difficulty)
        {
            return difficulty switch
            {
                >= 8 => CasePriority.Critical,
                >= 6 => CasePriority.High,
                >= 4 => CasePriority.Medium,
                _ => CasePriority.Low
            };
        }

        private static DetectiveRank ParseRankFromString(string rankString)
        {
            if (Enum.TryParse<DetectiveRank>(rankString, true, out var rank))
            {
                return rank;
            }
            return DetectiveRank.Rook; // Default to entry level
        }

        private static EvidenceCategory ParseEvidenceCategory(string categoryString)
        {
            if (Enum.TryParse<EvidenceCategory>(categoryString, true, out var category))
            {
                return category;
            }
            return EvidenceCategory.Physical; // Default
        }

        private static EvidencePriority ParseEvidencePriority(string priorityString)
        {
            if (Enum.TryParse<EvidencePriority>(priorityString, true, out var priority))
            {
                return priority;
            }
            return EvidencePriority.Medium; // Default
        }
    }
}

/// <summary>
/// Background service to periodically scan for new cases
/// </summary>
public class CaseProcessingBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CaseProcessingBackgroundService> _logger;
    private readonly IConfiguration _configuration;

    public CaseProcessingBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<CaseProcessingBackgroundService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var scanInterval = _configuration.GetValue<int>("CaseProcessing:ScanIntervalMinutes", 30);
        var enabled = _configuration.GetValue<bool>("CaseProcessing:Enabled", true);

        if (!enabled)
        {
            _logger.LogInformation("Case processing background service is disabled");
            return;
        }

        _logger.LogInformation("Case processing background service started. Scan interval: {ScanInterval} minutes", scanInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var caseProcessingService = scope.ServiceProvider.GetRequiredService<ICaseProcessingService>();
                
                await caseProcessingService.ProcessNewCasesAsync();
                
                await Task.Delay(TimeSpan.FromMinutes(scanInterval), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in case processing background service");
                // Continue running even if there's an error
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Short delay before retry
            }
        }

        _logger.LogInformation("Case processing background service stopped");
    }
}