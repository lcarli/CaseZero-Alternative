using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CaseZeroApi.Services;
using CaseZeroApi.Models;

namespace CaseZeroApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CaseFilesController : ControllerBase
{
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<CaseFilesController> _logger;

    public CaseFilesController(
        IBlobStorageService blobStorageService,
        ILogger<CaseFilesController> logger)
    {
        _blobStorageService = blobStorageService;
        _logger = logger;
    }

    /// <summary>
    /// Get all files for a specific case
    /// </summary>
    /// <param name="caseId">The case ID (e.g., CASE-20251027-b442dc10)</param>
    /// <returns>List of files available in the case</returns>
    [HttpGet("{caseId}")]
    [ProducesResponseType(typeof(CaseFilesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CaseFilesResponse>> GetCaseFiles(
        string caseId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting files for case: {CaseId}", caseId);

            var response = await _blobStorageService.GetCaseFilesAsync(caseId, cancellationToken);

            if (response.TotalFiles == 0)
            {
                _logger.LogWarning("No files found for case: {CaseId}", caseId);
                return NotFound(new { message = $"Case {caseId} not found or has no files" });
            }

            _logger.LogInformation("Retrieved {Count} files for case {CaseId}", response.TotalFiles, caseId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting files for case {CaseId}", caseId);
            return StatusCode(500, new { message = "An error occurred while retrieving case files" });
        }
    }

    /// <summary>
    /// Get a specific document from a case
    /// </summary>
    /// <param name="caseId">The case ID</param>
    /// <param name="documentId">The document ID (e.g., doc_evidence_log_001)</param>
    /// <returns>The document content</returns>
    [HttpGet("{caseId}/documents/{documentId}")]
    [ProducesResponseType(typeof(CaseDocument), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<string>> GetCaseDocument(
        string caseId,
        string documentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting document {DocumentId} for case {CaseId}", documentId, caseId);

            var documentJson = await _blobStorageService.GetCaseDocumentAsync(caseId, documentId, cancellationToken);

            if (documentJson == null)
            {
                _logger.LogWarning("Document not found: {CaseId}/{DocumentId}", caseId, documentId);
                return NotFound(new { message = $"Document {documentId} not found in case {caseId}" });
            }

            return Ok(documentJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document {DocumentId} for case {CaseId}", documentId, caseId);
            return StatusCode(500, new { message = "An error occurred while retrieving the document" });
        }
    }

    /// <summary>
    /// Get the normalized case bundle
    /// </summary>
    /// <param name="caseId">The case ID</param>
    /// <returns>The normalized case bundle</returns>
    [HttpGet("{caseId}/bundle")]
    [ProducesResponseType(typeof(NormalizedCaseBundle), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<NormalizedCaseBundle>> GetCaseBundle(
        string caseId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting bundle for case: {CaseId}", caseId);

            var bundle = await _blobStorageService.GetCaseBundleAsync(caseId, cancellationToken);

            if (bundle == null)
            {
                _logger.LogWarning("Bundle not found for case: {CaseId}", caseId);
                return NotFound(new { message = $"Case bundle {caseId} not found" });
            }

            return Ok(bundle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bundle for case {CaseId}", caseId);
            return StatusCode(500, new { message = "An error occurred while retrieving the case bundle" });
        }
    }
}
