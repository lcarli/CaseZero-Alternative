using CaseZeroApi.Models;
using System.Text.Json;

namespace CaseZeroApi.Services
{
    public interface ICaseObjectService
    {
        Task<CaseObject?> LoadCaseObjectAsync(string caseId);
        Task<CaseObject?> LoadCaseObjectAsync(string caseId, string locale);
        Task<List<string>> GetAvailableCasesAsync();
        Task<bool> ValidateCaseStructureAsync(string caseId);
        Task<bool> ValidateCaseStructureAsync(string caseId, string locale);
        string GetCaseFilePath(string caseId, string fileName);
        Task<Stream?> GetCaseFileAsync(string caseId, string fileName);
        Task<Stream?> GetCaseFileAsync(string caseId, string fileName, string locale);
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

        public async Task<CaseObject?> LoadCaseObjectAsync(string caseId, string locale)
        {
            try
            {
                // TODO: When the new structure is implemented, use: /cases/CASE-ID/locale/case.json
                // For now, fallback to the existing structure since all cases are in Portuguese
                
                // Try to load language-specific case (future structure)
                var languageSpecificPath = Path.Combine(_casesBasePath, caseId, locale);
                var languageSpecificJsonPath = Path.Combine(languageSpecificPath, "case.json");
                
                if (File.Exists(languageSpecificJsonPath))
                {
                    var jsonContent = await File.ReadAllTextAsync(languageSpecificJsonPath);
                    var caseObject = JsonSerializer.Deserialize<CaseObject>(jsonContent, _jsonOptions);
                    
                    if (caseObject != null)
                    {
                        _logger.LogInformation("Successfully loaded case object for {CaseId} with locale {Locale}", caseId, locale);
                        return caseObject;
                    }
                }
                
                // Fallback to default case (current structure) - all existing cases are in Portuguese
                _logger.LogInformation("Language-specific case not found for {CaseId} with locale {Locale}, falling back to default", caseId, locale);
                return await LoadCaseObjectAsync(caseId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading case object for {CaseId} with locale {Locale}", caseId, locale);
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

        public async Task<bool> ValidateCaseStructureAsync(string caseId, string locale)
        {
            try
            {
                var caseObject = await LoadCaseObjectAsync(caseId, locale);
                if (caseObject == null) return false;

                // TODO: When the new structure is implemented, validate language-specific paths
                // For now, fallback to the existing structure validation
                
                // Try language-specific path first
                var languageSpecificPath = Path.Combine(_casesBasePath, caseId, locale);
                if (Directory.Exists(languageSpecificPath))
                {
                    // Validate language-specific structure (future implementation)
                    // For now, just check if case.json exists
                    var caseJsonPath = Path.Combine(languageSpecificPath, "case.json");
                    if (File.Exists(caseJsonPath))
                    {
                        _logger.LogInformation("Language-specific case structure validation passed for {CaseId} with locale {Locale}", caseId, locale);
                        return true;
                    }
                }
                
                // Fallback to default validation
                _logger.LogInformation("Language-specific structure not found for {CaseId} with locale {Locale}, falling back to default validation", caseId, locale);
                return await ValidateCaseStructureAsync(caseId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating case structure for {CaseId} with locale {Locale}", caseId, locale);
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

        public Task<Stream?> GetCaseFileAsync(string caseId, string fileName, string locale)
        {
            try
            {
                // TODO: When the new structure is implemented, use: /cases/CASE-ID/locale/fileName
                // For now, fallback to the existing structure since all cases are in Portuguese
                
                // Try language-specific file first (future structure)
                var languageSpecificPath = Path.Combine(_casesBasePath, caseId, locale, fileName);
                
                if (File.Exists(languageSpecificPath))
                {
                    // Security check: ensure the file is within the case/locale directory
                    var languageCasePath = Path.Combine(_casesBasePath, caseId, locale);
                    var fullPath = Path.GetFullPath(languageSpecificPath);
                    var fullLanguageCasePath = Path.GetFullPath(languageCasePath);

                    if (fullPath.StartsWith(fullLanguageCasePath))
                    {
                        _logger.LogInformation("Loading language-specific file {FileName} for case {CaseId} with locale {Locale}", fileName, caseId, locale);
                        return Task.FromResult<Stream?>(File.OpenRead(languageSpecificPath));
                    }
                    else
                    {
                        _logger.LogWarning("Security violation: attempted to access file outside case/locale directory: {FilePath}", languageSpecificPath);
                        return Task.FromResult<Stream?>(null);
                    }
                }
                
                // Fallback to default file (current structure)
                _logger.LogInformation("Language-specific file not found for {CaseId} with locale {Locale}, falling back to default", caseId, locale);
                return GetCaseFileAsync(caseId, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving case file {FileName} for case {CaseId} with locale {Locale}", fileName, caseId, locale);
                return Task.FromResult<Stream?>(null);
            }
        }
    }
}