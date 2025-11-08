using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CaseZeroApi.Services;

namespace CaseZeroApi.Controllers;

/// <summary>
/// Controller for accessing generated cases from CaseGen.Functions
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GeneratedCasesController : ControllerBase
{
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<GeneratedCasesController> _logger;

    public GeneratedCasesController(
        IBlobStorageService blobStorageService,
        ILogger<GeneratedCasesController> logger)
    {
        _blobStorageService = blobStorageService;
        _logger = logger;
    }

    /// <summary>
    /// Lists all generated cases from CaseGen.Functions
    /// </summary>
    /// <returns>List of case manifests</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<CaseManifest>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<CaseManifest>>> ListGeneratedCases(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Listing generated cases");
            var cases = await _blobStorageService.ListCasesAsync(cancellationToken);
            _logger.LogInformation("Found {Count} generated cases", cases.Count);
            return Ok(cases);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list generated cases");
            return StatusCode(500, new { error = "Failed to retrieve cases" });
        }
    }

    /// <summary>
    /// Gets a specific generated case bundle by ID
    /// </summary>
    /// <param name="caseId">Case ID (e.g., CASE-20251027-b442dc10)</param>
    [HttpGet("{caseId}")]
    [ProducesResponseType(typeof(Models.NormalizedCaseBundle), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<Models.NormalizedCaseBundle>> GetGeneratedCase(
        string caseId,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting generated case {CaseId}", caseId);
            var caseBundle = await _blobStorageService.GetCaseBundleAsync(caseId, cancellationToken);
            
            if (caseBundle == null)
            {
                _logger.LogWarning("Generated case {CaseId} not found", caseId);
                return NotFound(new { error = $"Case {caseId} not found" });
            }

            _logger.LogInformation("Successfully retrieved generated case {CaseId}", caseId);
            return Ok(caseBundle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get generated case {CaseId}", caseId);
            return StatusCode(500, new { error = "Failed to retrieve case" });
        }
    }

    /// <summary>
    /// Gets a specific document from a generated case
    /// </summary>
    /// <param name="caseId">Case ID</param>
    /// <param name="documentId">Document ID (e.g., doc_police_001)</param>
    [HttpGet("{caseId}/documents/{documentId}")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<string>> GetDocument(
        string caseId,
        string documentId,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting document {DocumentId} from case {CaseId}", documentId, caseId);
            var document = await _blobStorageService.GetCaseDocumentAsync(caseId, documentId, cancellationToken);
            
            if (document == null)
            {
                _logger.LogWarning("Document {DocumentId} not found in case {CaseId}", documentId, caseId);
                return NotFound(new { error = $"Document {documentId} not found in case {caseId}" });
            }

            return Ok(document);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get document {DocumentId} from case {CaseId}", documentId, caseId);
            return StatusCode(500, new { error = "Failed to retrieve document" });
        }
    }

    /// <summary>
    /// Gets the URL for a media file from a generated case
    /// </summary>
    /// <param name="caseId">Case ID</param>
    /// <param name="fileName">Media file name</param>
    [HttpGet("{caseId}/media/{fileName}/url")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<object> GetMediaUrl(string caseId, string fileName)
    {
        try
        {
            _logger.LogInformation("Getting media URL for {FileName} in case {CaseId}", fileName, caseId);
            var url = _blobStorageService.GetMediaUrl(caseId, fileName);
            return Ok(new { url });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get media URL for {FileName} in case {CaseId}", fileName, caseId);
            return StatusCode(500, new { error = "Failed to generate media URL" });
        }
    }
}
