using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Text.Json;

namespace CaseZeroApi.Services;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BlobStorageService> _logger;
    private readonly string _bundlesContainer;
    private readonly string _storageAccountUrl;

    public BlobStorageService(
        IConfiguration configuration,
        ILogger<BlobStorageService> logger)
    {
        // Try multiple sources for connection string, with Azurite as fallback for local development
        var connectionString = configuration["CaseGeneratorStorage:ConnectionString"]
            ?? configuration["AzureWebJobsStorage"]
            ?? Environment.GetEnvironmentVariable("AzureWebJobsStorage")
            ?? "UseDevelopmentStorage=true"; // Azurite default connection string

        _blobServiceClient = new BlobServiceClient(connectionString);
        _configuration = configuration;
        _logger = logger;
        _bundlesContainer = configuration["CaseGeneratorStorage:BundlesContainer"] ?? "bundles";
        
        _logger.LogInformation("BlobStorageService initialized with container: {Container}", _bundlesContainer);
        
        // Extract storage account URL from connection string for generating media URLs
        _storageAccountUrl = ExtractStorageAccountUrl(connectionString);
    }

    public async Task<List<CaseManifest>> ListCasesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_bundlesContainer);
            var cases = new List<CaseManifest>();

            // List all case folders (they follow pattern: CASE-YYYYMMDD-xxxxxxxx/)
            await foreach (var item in containerClient.GetBlobsByHierarchyAsync(
                delimiter: "/",
                cancellationToken: cancellationToken))
            {
                if (item.IsPrefix && item.Prefix.StartsWith("CASE-"))
                {
                    var caseId = item.Prefix.TrimEnd('/');
                    var manifestPath = $"{caseId}/{caseId}.json";
                    
                    try
                    {
                        var manifestBlob = containerClient.GetBlobClient(manifestPath);
                        var response = await manifestBlob.DownloadContentAsync(cancellationToken);
                        var manifest = JsonSerializer.Deserialize<CaseManifest>(
                            response.Value.Content.ToString(),
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        
                        if (manifest != null)
                        {
                            cases.Add(manifest);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load manifest for case {CaseId}", caseId);
                    }
                }
            }

            // Sort by generated date, newest first
            return cases.OrderByDescending(c => c.GeneratedAt).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list cases from blob storage");
            throw;
        }
    }

    public async Task<CaseManifest?> GetCaseManifestAsync(
        string caseId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_bundlesContainer);
            var manifestPath = $"{caseId}/{caseId}.json";
            var blobClient = containerClient.GetBlobClient(manifestPath);

            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                _logger.LogWarning("Case manifest not found: {CaseId}", caseId);
                return null;
            }

            var response = await blobClient.DownloadContentAsync(cancellationToken);
            var manifest = JsonSerializer.Deserialize<CaseManifest>(
                response.Value.Content.ToString(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return manifest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get case manifest for {CaseId}", caseId);
            throw;
        }
    }

    public async Task<Models.NormalizedCaseBundle?> GetCaseBundleAsync(
        string caseId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_bundlesContainer);
            var bundlePath = $"{caseId}/normalized_case.json";
            var blobClient = containerClient.GetBlobClient(bundlePath);

            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                _logger.LogWarning("Case bundle not found: {CaseId}", caseId);
                return null;
            }

            var response = await blobClient.DownloadContentAsync(cancellationToken);
            var bundle = JsonSerializer.Deserialize<Models.NormalizedCaseBundle>(
                response.Value.Content.ToString(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return bundle;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get case bundle for {CaseId}", caseId);
            throw;
        }
    }

    public async Task<string?> GetCaseDocumentAsync(
        string caseId,
        string documentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_bundlesContainer);
            var documentPath = $"{caseId}/documents/{documentId}.json";
            var blobClient = containerClient.GetBlobClient(documentPath);

            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                _logger.LogWarning("Document not found: {CaseId}/{DocumentId}", caseId, documentId);
                return null;
            }

            var response = await blobClient.DownloadContentAsync(cancellationToken);
            return response.Value.Content.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get document {DocumentId} for case {CaseId}", documentId, caseId);
            throw;
        }
    }

    public async Task<Models.CaseFilesResponse> GetCaseFilesAsync(
        string caseId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get the normalized case bundle to find all document references
            var bundle = await GetCaseBundleAsync(caseId, cancellationToken);
            if (bundle == null || bundle.Documents == null)
            {
                _logger.LogWarning("Case bundle not found or has no documents: {CaseId}", caseId);
                return new Models.CaseFilesResponse { CaseId = caseId };
            }

            var files = new List<Models.FileViewerItem>();
            var containerClient = _blobServiceClient.GetBlobContainerClient(_bundlesContainer);

            // Process each document reference
            foreach (var docRef in bundle.Documents.Items)
            {
                // Extract document ID from reference (e.g., "@documents/doc_evidence_log_001" -> "doc_evidence_log_001")
                var docId = docRef.Replace("@documents/", "");
                var documentPath = $"{caseId}/documents/{docId}.json";
                
                try
                {
                    var blobClient = containerClient.GetBlobClient(documentPath);
                    if (!await blobClient.ExistsAsync(cancellationToken))
                    {
                        _logger.LogWarning("Document not found: {DocumentPath}", documentPath);
                        continue;
                    }

                    var response = await blobClient.DownloadContentAsync(cancellationToken);
                    var document = JsonSerializer.Deserialize<Models.CaseDocument>(
                        response.Value.Content.ToString(),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (document != null)
                    {
                        // Convert document to FileViewerItem
                        var file = ConvertDocumentToFileItem(document, bundle.GeneratedAt);
                        files.Add(file);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load document {DocId} for case {CaseId}", docId, caseId);
                }
            }

            // Process media files - include only evidence PNG images (ev_*.png)
            // Exclude JSON metadata files and reference images (ref_*.png)
            var mediaPrefix = $"{caseId}/media/";
            await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: mediaPrefix, cancellationToken: cancellationToken))
            {
                var fileName = blobItem.Name.Replace(mediaPrefix, "");
                
                // Filter: Only include PNG files that start with "ev_" (evidence images)
                // Exclude: JSON files (ev_*.json) and reference images (ref_*.png)
                if (fileName.StartsWith("ev_") && 
                    fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
                    !fileName.Contains(".json", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        // Extract evidence ID (e.g., "ev_cash_count_annotation_005.generated-image.png" -> "ev_cash_count_annotation_005")
                        var evidenceId = fileName.Replace(".generated-image.png", "").Replace(".png", "");
                        var sizeBytes = blobItem.Properties.ContentLength ?? 0;
                        
                        var mediaItem = new Models.FileViewerItem
                        {
                            Id = evidenceId,
                            Name = fileName,
                            Title = FormatMediaTitle(evidenceId),
                            Category = "evidence",
                            Icon = "📷",
                            Type = "image/png",
                            Timestamp = blobItem.Properties.CreatedOn?.DateTime ?? bundle.GeneratedAt,
                            Modified = blobItem.Properties.CreatedOn?.DateTime ?? bundle.GeneratedAt,
                            Size = $"{sizeBytes} bytes",
                            SizeBytes = sizeBytes,
                            Content = "", // Images don't have text content
                            Author = "System Generated",
                            EvidenceId = evidenceId.ToUpperInvariant(),
                            MediaUrl = GetMediaUrl(caseId, fileName)
                        };
                        
                        files.Add(mediaItem);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to process media file {FileName} for case {CaseId}", fileName, caseId);
                    }
                }
            }

            // Group files by category
            var filesByCategory = files
                .GroupBy(f => f.Category)
                .ToDictionary(g => g.Key, g => g.Count());

            return new Models.CaseFilesResponse
            {
                CaseId = caseId,
                Files = files,
                TotalFiles = files.Count,
                FilesByCategory = filesByCategory
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get case files for {CaseId}", caseId);
            throw;
        }
    }

    private Models.FileViewerItem ConvertDocumentToFileItem(Models.CaseDocument document, DateTime generatedAt)
    {
        // Concatenate all sections into content
        var content = string.Join("\n\n", document.Sections.Select(s => 
            $"## {s.Title}\n\n{s.Content}"));

        // Determine category and icon from document type
        var (category, icon) = GetCategoryAndIcon(document.Type);

        // Extract evidence ID if present (e.g., "doc_evidence_log_001" might reference "EV001")
        string? evidenceId = null;
        if (document.Type.Contains("evidence"))
        {
            // Try to extract evidence ID from document content
            var match = System.Text.RegularExpressions.Regex.Match(content, @"EV\d{3}");
            evidenceId = match.Success ? match.Value : null;
        }

        return new Models.FileViewerItem
        {
            Id = document.DocId,
            Name = document.Title,
            Title = document.Title,
            Type = GetFileType(document.Type),
            Icon = icon,
            Category = category,
            Size = $"{document.Words * 5} bytes", // Rough estimate: avg 5 bytes per word
            SizeBytes = document.Words * 5,
            Modified = generatedAt,
            Timestamp = generatedAt,
            Content = content,
            EvidenceId = evidenceId,
            Author = "Case Generator",
            IsUnlocked = true // For now, all files are unlocked. Add unlock logic later
        };
    }

    private (string category, string icon) GetCategoryAndIcon(string docType)
    {
        // All documents go under "documents" category, but keep different icons
        return docType.ToLower() switch
        {
            "evidence_log" => ("documents", "📋"),
            "forensics" => ("documents", "🔬"),
            "interview" => ("documents", "👤"),
            "witness" => ("documents", "👤"),
            "memo" => ("documents", "📝"),
            "police" => ("documents", "👮"),
            "suspect_profile" => ("documents", "🕵️"),
            _ => ("documents", "📄")
        };
    }

    private string GetFileType(string docType)
    {
        return docType.ToLower() switch
        {
            "evidence_log" => "pdf",
            "forensics" => "pdf",
            "interview" => "text",
            "witness" => "pdf",
            "memo" => "text",
            "police" => "pdf",
            "suspect_profile" => "pdf",
            _ => "text"
        };
    }

    private string FormatMediaTitle(string evidenceId)
    {
        // Convert "ev_cash_count_annotation_005" to "Cash Count Annotation 005"
        var title = evidenceId
            .Replace("ev_", "")
            .Replace("_", " ")
            .Replace(".generated-image", "")
            .Replace(".png", "");
        
        // Capitalize each word
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(title);
    }

    public string GetMediaUrl(string caseId, string fileName)
    {
        // Generate URL for media file in format: {storageUrl}/bundles/{caseId}/media/{fileName}
        return $"{_storageAccountUrl}/{_bundlesContainer}/{caseId}/media/{fileName}";
    }

    private string ExtractStorageAccountUrl(string connectionString)
    {
        // For development (Azurite), use http://127.0.0.1:10000/devstoreaccount1
        if (connectionString.Contains("UseDevelopmentStorage=true") || 
            connectionString.Contains("127.0.0.1:10000"))
        {
            return "http://127.0.0.1:10000/devstoreaccount1";
        }

        // For Azure Storage, extract from connection string
        var parts = connectionString.Split(';');
        var accountName = parts.FirstOrDefault(p => p.StartsWith("AccountName="))?.Split('=')[1];
        
        if (!string.IsNullOrEmpty(accountName))
        {
            return $"https://{accountName}.blob.core.windows.net";
        }

        _logger.LogWarning("Could not extract storage account URL from connection string");
        return string.Empty;
    }
}
