using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CaseZeroApi.Models;
using CaseZeroApi.Services;

namespace CaseZeroApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CaseObjectController : ControllerBase
    {
        private readonly ICaseObjectService _caseObjectService;
        private readonly ILogger<CaseObjectController> _logger;

        public CaseObjectController(ICaseObjectService caseObjectService, ILogger<CaseObjectController> logger)
        {
            _caseObjectService = caseObjectService;
            _logger = logger;
        }

        /// <summary>
        /// Get list of available case objects
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<string>>> GetAvailableCases()
        {
            try
            {
                var cases = await _caseObjectService.GetAvailableCasesAsync();
                return Ok(cases);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving available cases");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Load a specific case object
        /// </summary>
        [HttpGet("{caseId}")]
        public async Task<ActionResult<CaseObject>> GetCaseObject(string caseId)
        {
            try
            {
                var caseObject = await _caseObjectService.LoadCaseObjectAsync(caseId);
                
                if (caseObject == null)
                {
                    return NotFound($"Case {caseId} not found");
                }

                return Ok(caseObject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading case object {CaseId}", caseId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Load a specific case object for a specific language
        /// </summary>
        [HttpGet("{caseId}/{locale}")]
        public async Task<ActionResult<CaseObject>> GetCaseObjectWithLanguage(string caseId, string locale)
        {
            try
            {
                var caseObject = await _caseObjectService.LoadCaseObjectAsync(caseId, locale);
                
                if (caseObject == null)
                {
                    return NotFound($"Case {caseId} not found for locale {locale}");
                }

                return Ok(caseObject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading case object {CaseId} for locale {Locale}", caseId, locale);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Validate case structure
        /// </summary>
        [HttpGet("{caseId}/validate")]
        public async Task<ActionResult<bool>> ValidateCaseStructure(string caseId)
        {
            try
            {
                var isValid = await _caseObjectService.ValidateCaseStructureAsync(caseId);
                return Ok(new { caseId, isValid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating case structure for {CaseId}", caseId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Validate case structure for a specific language
        /// </summary>
        [HttpGet("{caseId}/{locale}/validate")]
        public async Task<ActionResult<bool>> ValidateCaseStructureWithLanguage(string caseId, string locale)
        {
            try
            {
                var isValid = await _caseObjectService.ValidateCaseStructureAsync(caseId, locale);
                return Ok(new { caseId, locale, isValid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating case structure for {CaseId} with locale {Locale}", caseId, locale);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get a specific file from a case
        /// </summary>
        [HttpGet("{caseId}/files/{*fileName}")]
        public async Task<ActionResult> GetCaseFile(string caseId, string fileName)
        {
            try
            {
                var fileStream = await _caseObjectService.GetCaseFileAsync(caseId, fileName);
                
                if (fileStream == null)
                {
                    return NotFound($"File {fileName} not found in case {caseId}");
                }

                var contentType = GetContentType(fileName);
                return File(fileStream, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving file {FileName} from case {CaseId}", fileName, caseId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get a specific file from a case for a specific language
        /// </summary>
        [HttpGet("{caseId}/{locale}/files/{*fileName}")]
        public async Task<ActionResult> GetCaseFileWithLanguage(string caseId, string locale, string fileName)
        {
            try
            {
                var fileStream = await _caseObjectService.GetCaseFileAsync(caseId, fileName, locale);
                
                if (fileStream == null)
                {
                    return NotFound($"File {fileName} not found in case {caseId} for locale {locale}");
                }

                var contentType = GetContentType(fileName);
                return File(fileStream, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving file {FileName} from case {CaseId} for locale {Locale}", fileName, caseId, locale);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get case metadata only (without full case object)
        /// </summary>
        [HttpGet("{caseId}/metadata")]
        public async Task<ActionResult<CaseMetadata>> GetCaseMetadata(string caseId)
        {
            try
            {
                var caseObject = await _caseObjectService.LoadCaseObjectAsync(caseId);
                
                if (caseObject == null)
                {
                    return NotFound($"Case {caseId} not found");
                }

                return Ok(caseObject.Metadata);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading case metadata for {CaseId}", caseId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get case metadata only (without full case object) for a specific language
        /// </summary>
        [HttpGet("{caseId}/{locale}/metadata")]
        public async Task<ActionResult<CaseMetadata>> GetCaseMetadataWithLanguage(string caseId, string locale)
        {
            try
            {
                var caseObject = await _caseObjectService.LoadCaseObjectAsync(caseId, locale);
                
                if (caseObject == null)
                {
                    return NotFound($"Case {caseId} not found for locale {locale}");
                }

                return Ok(caseObject.Metadata);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading case metadata for {CaseId} with locale {Locale}", caseId, locale);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get case evidences with current unlock status
        /// </summary>
        [HttpGet("{caseId}/evidences")]
        public async Task<ActionResult<List<CaseEvidence>>> GetCaseEvidences(string caseId)
        {
            try
            {
                var caseObject = await _caseObjectService.LoadCaseObjectAsync(caseId);
                
                if (caseObject == null)
                {
                    return NotFound($"Case {caseId} not found");
                }

                // TODO: In the future, filter based on user progress and unlock status
                return Ok(caseObject.Evidences);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading case evidences for {CaseId}", caseId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get case evidences with current unlock status for a specific language
        /// </summary>
        [HttpGet("{caseId}/{locale}/evidences")]
        public async Task<ActionResult<List<CaseEvidence>>> GetCaseEvidencesWithLanguage(string caseId, string locale)
        {
            try
            {
                var caseObject = await _caseObjectService.LoadCaseObjectAsync(caseId, locale);
                
                if (caseObject == null)
                {
                    return NotFound($"Case {caseId} not found for locale {locale}");
                }

                // TODO: In the future, filter based on user progress and unlock status
                return Ok(caseObject.Evidences);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading case evidences for {CaseId} with locale {Locale}", caseId, locale);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get case suspects with current unlock status
        /// </summary>
        [HttpGet("{caseId}/suspects")]
        public async Task<ActionResult<List<CaseSuspect>>> GetCaseSuspects(string caseId)
        {
            try
            {
                var caseObject = await _caseObjectService.LoadCaseObjectAsync(caseId);
                
                if (caseObject == null)
                {
                    return NotFound($"Case {caseId} not found");
                }

                // TODO: In the future, filter based on user progress and unlock status
                return Ok(caseObject.Suspects);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading case suspects for {CaseId}", caseId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get case suspects with current unlock status for a specific language
        /// </summary>
        [HttpGet("{caseId}/{locale}/suspects")]
        public async Task<ActionResult<List<CaseSuspect>>> GetCaseSupsectsWithLanguage(string caseId, string locale)
        {
            try
            {
                var caseObject = await _caseObjectService.LoadCaseObjectAsync(caseId, locale);
                
                if (caseObject == null)
                {
                    return NotFound($"Case {caseId} not found for locale {locale}");
                }

                // TODO: In the future, filter based on user progress and unlock status
                return Ok(caseObject.Suspects);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading case suspects for {CaseId} with locale {Locale}", caseId, locale);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get case timeline
        /// </summary>
        [HttpGet("{caseId}/timeline")]
        public async Task<ActionResult<List<CaseTimelineEvent>>> GetCaseTimeline(string caseId)
        {
            try
            {
                var caseObject = await _caseObjectService.LoadCaseObjectAsync(caseId);
                
                if (caseObject == null)
                {
                    return NotFound($"Case {caseId} not found");
                }

                return Ok(caseObject.Timeline);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading case timeline for {CaseId}", caseId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get case timeline for a specific language
        /// </summary>
        [HttpGet("{caseId}/{locale}/timeline")]
        public async Task<ActionResult<List<CaseTimelineEvent>>> GetCaseTimelineWithLanguage(string caseId, string locale)
        {
            try
            {
                var caseObject = await _caseObjectService.LoadCaseObjectAsync(caseId, locale);
                
                if (caseObject == null)
                {
                    return NotFound($"Case {caseId} not found for locale {locale}");
                }

                return Ok(caseObject.Timeline);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading case timeline for {CaseId} with locale {Locale}", caseId, locale);
                return StatusCode(500, "Internal server error");
            }
        }

        private static string GetContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".pdf" => "application/pdf",
                ".txt" => "text/plain",
                ".json" => "application/json",
                ".csv" => "text/csv",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".mp4" => "video/mp4",
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _ => "application/octet-stream"
            };
        }
    }
}