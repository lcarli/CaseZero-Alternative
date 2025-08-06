using CaseZeroApi.Models;
using System.Text.Json;

namespace CaseZeroApi.Services
{
    public interface ICaseObjectService
    {
        Task<CaseObject?> LoadCaseObjectAsync(string caseId);
        Task<List<string>> GetAvailableCasesAsync();
        Task<bool> ValidateCaseStructureAsync(string caseId);
        string GetCaseFilePath(string caseId, string fileName);
        Task<Stream?> GetCaseFileAsync(string caseId, string fileName);
    }

    public class CaseObjectService : ICaseObjectService
    {
        private readonly ILogger<CaseObjectService> _logger;
        private readonly string _casesBasePath;
        private readonly JsonSerializerOptions _jsonOptions;

        public CaseObjectService(ILogger<CaseObjectService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _casesBasePath = configuration.GetValue<string>("CasesBasePath") 
                ?? Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "cases");
            
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };
        }

        public async Task<CaseObject?> LoadCaseObjectAsync(string caseId)
        {
            try
            {
                var casePath = Path.Combine(_casesBasePath, caseId);
                var caseJsonPath = Path.Combine(casePath, "case.json");

                if (!File.Exists(caseJsonPath))
                {
                    _logger.LogWarning("Case file not found for case {CaseId} at {Path}", caseId, caseJsonPath);
                    return null;
                }

                var jsonContent = await File.ReadAllTextAsync(caseJsonPath);
                var caseObject = JsonSerializer.Deserialize<CaseObject>(jsonContent, _jsonOptions);

                if (caseObject != null)
                {
                    _logger.LogInformation("Successfully loaded case object for {CaseId}", caseId);
                }

                return caseObject;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse case.json for case {CaseId}", caseId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading case object for {CaseId}", caseId);
                return null;
            }
        }

        public Task<List<string>> GetAvailableCasesAsync()
        {
            try
            {
                if (!Directory.Exists(_casesBasePath))
                {
                    _logger.LogWarning("Cases directory not found at {Path}", _casesBasePath);
                    return Task.FromResult(new List<string>());
                }

                var caseDirs = Directory.GetDirectories(_casesBasePath)
                    .Where(dir => File.Exists(Path.Combine(dir, "case.json")))
                    .Select(dir => Path.GetFileName(dir))
                    .Where(name => !string.IsNullOrEmpty(name))
                    .ToList();

                _logger.LogInformation("Found {Count} available cases", caseDirs.Count);
                return Task.FromResult(caseDirs!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving available cases");
                return Task.FromResult(new List<string>());
            }
        }

        public async Task<bool> ValidateCaseStructureAsync(string caseId)
        {
            try
            {
                var caseObject = await LoadCaseObjectAsync(caseId);
                if (caseObject == null) return false;

                var casePath = Path.Combine(_casesBasePath, caseId);

                // Validate basic structure
                var requiredDirs = new[] { "evidence", "suspects", "forensics" };
                foreach (var dir in requiredDirs)
                {
                    var dirPath = Path.Combine(casePath, dir);
                    if (!Directory.Exists(dirPath))
                    {
                        _logger.LogWarning("Missing required directory {Dir} for case {CaseId}", dir, caseId);
                        return false;
                    }
                }

                // Validate evidence files exist
                foreach (var evidence in caseObject.Evidences)
                {
                    var filePath = Path.Combine(casePath, evidence.Location, evidence.FileName);
                    if (!File.Exists(filePath))
                    {
                        _logger.LogWarning("Missing evidence file {File} for case {CaseId}", 
                            evidence.FileName, caseId);
                        return false;
                    }
                }

                // Validate forensic result files exist (if any)
                foreach (var analysis in caseObject.ForensicAnalyses)
                {
                    var filePath = Path.Combine(casePath, analysis.ResultFile);
                    if (!File.Exists(filePath))
                    {
                        _logger.LogWarning("Missing forensic result file {File} for case {CaseId}", 
                            analysis.ResultFile, caseId);
                        return false;
                    }
                }

                // Validate temporal event files exist
                foreach (var timeEvent in caseObject.TemporalEvents)
                {
                    if (!string.IsNullOrEmpty(timeEvent.FileName))
                    {
                        var filePath = Path.Combine(casePath, timeEvent.FileName);
                        if (!File.Exists(filePath))
                        {
                            _logger.LogWarning("Missing temporal event file {File} for case {CaseId}", 
                                timeEvent.FileName, caseId);
                            return false;
                        }
                    }
                }

                _logger.LogInformation("Case structure validation passed for {CaseId}", caseId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating case structure for {CaseId}", caseId);
                return false;
            }
        }

        public string GetCaseFilePath(string caseId, string fileName)
        {
            return Path.Combine(_casesBasePath, caseId, fileName);
        }

        public Task<Stream?> GetCaseFileAsync(string caseId, string fileName)
        {
            try
            {
                var filePath = GetCaseFilePath(caseId, fileName);
                
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("Case file not found: {FilePath}", filePath);
                    return Task.FromResult<Stream?>(null);
                }

                // Security check: ensure the file is within the case directory
                var casePath = Path.Combine(_casesBasePath, caseId);
                var fullPath = Path.GetFullPath(filePath);
                var fullCasePath = Path.GetFullPath(casePath);

                if (!fullPath.StartsWith(fullCasePath))
                {
                    _logger.LogWarning("Security violation: attempted to access file outside case directory: {FilePath}", filePath);
                    return Task.FromResult<Stream?>(null);
                }

                return Task.FromResult<Stream?>(File.OpenRead(filePath));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving case file {FileName} for case {CaseId}", fileName, caseId);
                return Task.FromResult<Stream?>(null);
            }
        }
    }
}