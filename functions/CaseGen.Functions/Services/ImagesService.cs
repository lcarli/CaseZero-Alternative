using CaseGen.Functions.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CaseGen.Functions.Services;

public class ImagesService : IImagesService
{
    private readonly IStorageService _storageService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ImagesService> _logger;
    private readonly ILLMService _llmService;

    public ImagesService(
        IStorageService storageService,
        IConfiguration configuration,
        ILogger<ImagesService> logger,
        ILLMService llmService)
    {
        _storageService = storageService;
        _configuration = configuration;
        _logger = logger;
        _llmService = llmService;
    }

    public async Task<string> GenerateAsync(string caseId, MediaSpec spec, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating real image for evidence {EvidenceId} of kind {Kind}", spec.EvidenceId, spec.Kind);

        // Check if this is an image type that should be generated
        if (!IsImageType(spec.Kind))
        {
            _logger.LogInformation("Skipping non-image media type {Kind} for evidence {EvidenceId}", spec.Kind, spec.EvidenceId);
            return await CreateDeferredResult(caseId, spec, "Non-image type deferred");
        }

        try
        {            
            if (string.IsNullOrWhiteSpace(spec.Prompt))
            {
                _logger.LogWarning("No generation prompt available for evidence {EvidenceId}", spec.EvidenceId);
                return await CreateDeferredResult(caseId, spec, "No generation prompt available");
            }

            // Generate image using LLM based on detailed prompt and constraints
            var imageBytes = await GenerateImageAsync(caseId, spec, cancellationToken);
            
            // Save the image file to blob storage within the bundle
            var imageUrl = await SaveImageToBundle(caseId, spec.EvidenceId, imageBytes, cancellationToken);
            
            // Create log entry
            await CreateImageLog(caseId, spec, imageUrl, imageBytes.Length, cancellationToken);
            
            _logger.LogInformation("Successfully generated image for evidence {EvidenceId}: {Url}", spec.EvidenceId, imageUrl);
            
            return imageUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate image for evidence {EvidenceId}", spec.EvidenceId);
            
            // Create error file in bundle
            await CreateErrorFile(caseId, spec.EvidenceId, ex.Message, cancellationToken);
            
            return await CreateDeferredResult(caseId, spec, $"Generation failed: {ex.Message}");
        }
    }

    private bool IsImageType(string kind)
    {
        return kind switch
        {
            MediaTypes.Photo => true,
            MediaTypes.DocumentScan => true,
            MediaTypes.Diagram => true,
            _ => kind.Contains("photo") || kind.Contains("image") || kind.Contains("scan") || kind.Contains("diagram")
        };
    }

    private async Task<byte[]> GenerateImageAsync(string caseId, MediaSpec spec, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Generating image for evidence {EvidenceId} using LLM", spec.EvidenceId);

        // Check if this spec has visual reference IDs
        if (spec.VisualReferenceIds != null && spec.VisualReferenceIds.Any())
        {
            _logger.LogInformation("MediaSpec has visual references: {References}", string.Join(", ", spec.VisualReferenceIds));
            
            // Try to load visual references and generate with them
            var referenceImage = await TryLoadVisualReference(caseId, spec.VisualReferenceIds[0], cancellationToken);
            
            if (referenceImage != null)
            {
                return await GenerateImageWithReferenceAsync(caseId, spec, referenceImage, cancellationToken);
            }
            else
            {
                _logger.LogWarning("Could not load visual reference {ReferenceId}, falling back to text-only generation", 
                    spec.VisualReferenceIds[0]);
            }
        }

        // Fallback to text-only generation
        return await GenerateImageFromTextAsync(caseId, spec, cancellationToken);
    }

    private async Task<byte[]?> TryLoadVisualReference(string caseId, string referenceId, CancellationToken cancellationToken)
    {
        try
        {
            var contextContainer = _configuration["CaseGeneratorStorage:ContextContainer"] ?? "case-context";
            var referencePath = $"{caseId}/references/{referenceId}.png";
            
            _logger.LogInformation("Attempting to load visual reference from {Path}", referencePath);
            
            var referenceBytes = await _storageService.GetFileBytesAsync(contextContainer, referencePath, cancellationToken);
            
            if (referenceBytes != null && referenceBytes.Length > 0)
            {
                _logger.LogInformation("Loaded visual reference {ReferenceId}, size: {Size} bytes", referenceId, referenceBytes.Length);
                return referenceBytes;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load visual reference {ReferenceId}", referenceId);
        }
        
        return null;
    }

    private async Task<byte[]> GenerateImageWithReferenceAsync(string caseId, MediaSpec spec, byte[] referenceImage, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Generating image for {EvidenceId} using visual reference ({ReferenceSize} bytes)", 
            spec.EvidenceId, referenceImage.Length);

        var constraintsText = spec.Constraints != null && spec.Constraints.Any()
            ? string.Join("\n", spec.Constraints.Select(kvp => $"- {kvp.Key}: {kvp.Value}"))
            : "No specific constraints";

        var imagePrompt = $"""
            Generate a forensic image based on these specifications:

            EVIDENCE: {spec.EvidenceId}
            TYPE: {spec.Kind}
            TITLE: {spec.Title}
            
            SCENE DESCRIPTION:
            {spec.Prompt}
            
            VISUAL CONSISTENCY REQUIREMENT:
            The reference image shows the EXACT appearance of a key element in this scene.
            You MUST maintain ALL visual characteristics from the reference image:
            - Colors, textures, and materials
            - Size, proportions, and distinctive features
            - Any visible wear, marks, or unique details
            
            Integrate this element naturally into the scene described above.
            DO NOT alter the element's visual appearance - only its placement, context, lighting, and angle.
            
            TECHNICAL REQUIREMENTS AND CONSTRAINTS:
            {constraintsText}

            Generate a high-quality PNG/JPG image that strictly satisfies all specifications for forensic/police use.
            """;

        var imageBytes = await _llmService.GenerateImageWithReferenceAsync(caseId, imagePrompt, referenceImage, null, cancellationToken);
        
        return imageBytes;
    }

    private async Task<byte[]> GenerateImageFromTextAsync(string caseId, MediaSpec spec, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Generating image for evidence {EvidenceId} from text prompt only", spec.EvidenceId);

        var constraintsText = spec.Constraints != null && spec.Constraints.Any()
            ? string.Join("\n", spec.Constraints.Select(kvp => $"- {kvp.Key}: {kvp.Value}"))
            : "No specific constraints";

        var imagePrompt = $"""
            Generate a forensic image based on these specifications:

            EVIDENCE: {spec.EvidenceId}
            TYPE: {spec.Kind}
            TITLE: {spec.Title}
            
            DETAILED IMAGE SPECIFICATION:
            {spec.Prompt}
            
            TECHNICAL REQUIREMENTS AND CONSTRAINTS:
            {constraintsText}

            Generate a high-quality PNG/JPG image that strictly satisfies all the above specifications for forensic/police use.
            """;

        var imageBytes = await _llmService.GenerateImageAsync(caseId, imagePrompt, cancellationToken);
        
        return imageBytes;
    }

    private async Task<string> SaveImageToBundle(string caseId, string evidenceId, byte[] imageBytes, CancellationToken cancellationToken)
    {
        var bundlesContainer = _configuration["CaseGeneratorStorage:BundlesContainer"] ?? "bundles";
        // Use .png extension for actual image file generated by LLM
        var imageFileName = $"{caseId}/media/{evidenceId}.generated-image.png";
        
        var imageUrl = await _storageService.SaveFileAsync(bundlesContainer, imageFileName, imageBytes, cancellationToken);
        
        _logger.LogInformation("BUNDLE: Saved LLM-generated image file to bundle: {Path} (case={CaseId}, evidence={EvidenceId}, size={Size} bytes)",
            imageFileName, caseId, evidenceId, imageBytes.Length);
            
        return imageUrl;
    }

    private async Task CreateImageLog(string caseId, MediaSpec spec, string imageUrl, int sizeBytes, CancellationToken cancellationToken)
    {
        var logEntry = new
        {
            evidenceId = spec.EvidenceId,
            kind = spec.Kind,
            title = spec.Title,
            imageUrl = imageUrl,
            sizeBytes = sizeBytes,
            generatedAt = DateTime.UtcNow,
            status = "IMAGE_FILE_GENERATED",
            type = "image_file",
            format = "png",
            note = "Real image file generated by LLM based on detailed forensic specifications"
        };

        var logJson = JsonSerializer.Serialize(logEntry, new JsonSerializerOptions { WriteIndented = true });
        
        var bundlesContainer = _configuration["CaseGeneratorStorage:BundlesContainer"] ?? "bundles";
        var logFileName = $"{caseId}/logs/image-{spec.EvidenceId}.log.json";
        
        await _storageService.SaveFileAsync(bundlesContainer, logFileName, logJson, cancellationToken);
        
        _logger.LogInformation("Created image generation log for evidence {EvidenceId}", spec.EvidenceId);
    }

    private async Task CreateErrorFile(string caseId, string evidenceId, string error, CancellationToken cancellationToken)
    {
        var bundlesContainer = _configuration["CaseGeneratorStorage:BundlesContainer"] ?? "bundles";
        var errorFileName = $"{caseId}/media/{evidenceId}.error.txt";
        
        var errorContent = $"Image generation failed at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\nError: {error}";
        
        await _storageService.SaveFileAsync(bundlesContainer, errorFileName, errorContent, cancellationToken);
        
        _logger.LogInformation("Created error file for evidence {EvidenceId}: {FileName}", evidenceId, errorFileName);
    }

    private async Task<string> CreateDeferredResult(string caseId, MediaSpec spec, string reason, CancellationToken cancellationToken = default)
    {
        var deferredEntry = new
        {
            evidenceId = spec.EvidenceId,
            kind = spec.Kind,
            title = spec.Title,
            status = "DEFERRED",
            reason = reason,
            deferredAt = DateTime.UtcNow
        };

        var logJson = JsonSerializer.Serialize(deferredEntry, new JsonSerializerOptions { WriteIndented = true });
        
        var bundlesContainer = _configuration["CaseGeneratorStorage:BundlesContainer"] ?? "bundles";
        var logFileName = $"{caseId}/logs/image-{spec.EvidenceId}.deferred.json";
        
        await _storageService.SaveFileAsync(bundlesContainer, logFileName, logJson, cancellationToken);
        
        return $"DEFERRED: {reason}";
    }
}