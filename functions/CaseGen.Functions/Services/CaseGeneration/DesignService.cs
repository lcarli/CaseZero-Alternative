using CaseGen.Functions.Models;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace CaseGen.Functions.Services.CaseGeneration;

/// <summary>
/// Service responsible for designing case specifications (Phase 4).
/// Handles document/media design, visual consistency, and legacy monolithic design.
/// </summary>
public class DesignService
{
    private readonly ILLMService _llmService;
    private readonly IJsonSchemaProvider _schemaProvider;
    private readonly ISchemaValidationService _schemaValidationService;
    private readonly IContextManager _contextManager;
    private readonly IStorageService _storageService;
    private readonly IImagesService _imagesService;
    private readonly ILogger<DesignService> _logger;

    public DesignService(
        ILLMService llmService,
        IJsonSchemaProvider schemaProvider,
        ISchemaValidationService schemaValidationService,
        IContextManager contextManager,
        IStorageService storageService,
        IImagesService imagesService,
        ILogger<DesignService> logger)
    {
        _llmService = llmService;
        _schemaProvider = schemaProvider;
        _schemaValidationService = schemaValidationService;
        _contextManager = contextManager;
        _storageService = storageService;
        _imagesService = imagesService;
        _logger = logger;
    }

    /// <summary>
    /// Creates visual consistency registry for case elements (Phase 4.3).
    /// </summary>
    public async Task<string> DesignVisualConsistencyRegistryAsync(string caseId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("DESIGN-VISUAL-REGISTRY: Starting visual consistency analysis for caseId={CaseId}", caseId);

        // Load available contexts
        var contexts = new Dictionary<string, string>();
        
        try
        {
            contexts["plan/core"] = await LoadContextAsync(caseId, "plan/core", cancellationToken);
        }
        catch
        {
            throw new InvalidOperationException($"Failed to load core context for case {caseId}");
        }

        // Optional contexts
        foreach (var path in new[] { "plan/evidence", "plan/suspects", "expand/evidence", "expand/suspects" })
        {
            try
            {
                contexts[path] = await LoadContextAsync(caseId, path, cancellationToken);
            }
            catch
            {
                _logger.LogDebug("Context {Path} not available", path);
            }
        }

        // Build context for analysis
        var contextBuilder = new StringBuilder();
        contextBuilder.AppendLine("=== CASE CONTEXT FOR VISUAL CONSISTENCY ANALYSIS ===\n");
        
        foreach (var (path, content) in contexts)
        {
            contextBuilder.AppendLine($"--- {path.ToUpper()} ---");
            contextBuilder.AppendLine(content);
            contextBuilder.AppendLine();
        }

        var systemPrompt = @"
You are a forensic visual consistency specialist. Create a Visual Consistency Registry for ALL elements requiring consistent visual representation.

FOR EACH ELEMENT, PROVIDE:
- referenceId: Unique identifier (e.g., ""evidence_backpack"", ""suspect_001"")
- category: ""physical_evidence"" | ""suspect"" | ""location""
- detailedDescription: Comprehensive physical description (dimensions, materials, colors, wear, marks, features)
- colorPalette: Array of 3-5 hex colors
- distinctiveFeatures: Array of 3-5 unique identifiers

OUTPUT: ONLY valid JSON conforming to VisualConsistencyRegistry schema.
";

        var userPrompt = $@"
Analyze this case and create a Visual Consistency Registry:

{contextBuilder}

Include: evidence items appearing 2+ times, ALL suspects, key locations.
Provide EXHAUSTIVE physical details for generating reference images.
";

        var jsonSchema = _schemaProvider.GetSchema("VisualConsistencyRegistry");
        
        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var response = await _llmService.GenerateStructuredAsync(
                    caseId, systemPrompt, userPrompt, jsonSchema, cancellationToken);

                using var doc = JsonDocument.Parse(response);
                if (!doc.RootElement.TryGetProperty("references", out _))
                    throw new InvalidOperationException("Response missing required properties");

                var registryObject = JsonSerializer.Deserialize<object>(response);
                await _contextManager.SaveContextAsync(caseId, "visual-registry", registryObject, cancellationToken);

                _logger.LogInformation("DESIGN-VISUAL-REGISTRY-COMPLETE: saved to visual-registry");
                return response;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                _logger.LogWarning("DESIGN-VISUAL-REGISTRY-RETRY: attempt={Attempt} error={Error}", attempt, ex.Message);
            }
        }

        throw new InvalidOperationException($"Failed to design visual consistency registry after {maxRetries} attempts");
    }

    /// <summary>
    /// Generates master reference images for visual consistency (Phase 4.4).
    /// </summary>
    public async Task<int> GenerateMasterReferencesAsync(string caseId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GENERATE-MASTER-REFS: Starting for caseId={CaseId}", caseId);

        string registryJson;
        try
        {
            registryJson = await LoadContextAsync(caseId, "visual-registry", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GENERATE-MASTER-REFS-ERROR: Failed to load visual registry");
            throw new InvalidOperationException("Visual consistency registry not found. Run DesignVisualConsistencyRegistryAsync first.", ex);
        }

        using var registryDoc = JsonDocument.Parse(registryJson);
        var referencesElement = registryDoc.RootElement.GetProperty("references");

        var referencesList = new List<(string referenceId, string category, string description, string[] colorPalette)>();
        
        foreach (var refProperty in referencesElement.EnumerateObject())
        {
            var refId = refProperty.Name;
            var refObj = refProperty.Value;
            
            var category = refObj.GetProperty("category").GetString() ?? "physical_evidence";
            var description = refObj.GetProperty("detailedDescription").GetString() ?? "";
            var colorPaletteArray = refObj.GetProperty("colorPalette")
                .EnumerateArray()
                .Select(c => c.GetString() ?? "")
                .ToArray();
            
            referencesList.Add((refId, category, description, colorPaletteArray));
        }

        _logger.LogInformation("GENERATE-MASTER-REFS: Found {Count} references to generate", referencesList.Count);

        var generatedCount = 0;
        var updatedReferences = new Dictionary<string, JsonElement>();

        foreach (var (referenceId, category, description, colorPalette) in referencesList)
        {
            try
            {
                var prompt = BuildMasterReferencePrompt(referenceId, category, description, colorPalette);

                var imageUrl = await _imagesService.GenerateAsync(
                    caseId,
                    new MediaSpec 
                    { 
                        EvidenceId = $"ref_{referenceId}",
                        Title = $"Master Reference - {referenceId}",
                        Prompt = prompt,
                        Kind = "photo"
                    },
                    cancellationToken);

                var blobPath = imageUrl.Split(new[] { "/bundles/" }, StringSplitOptions.None).Last();
                var imageBytes = await _storageService.GetFileBytesAsync("bundles", blobPath, cancellationToken);
                
                if (imageBytes == null || imageBytes.Length == 0)
                    throw new InvalidOperationException($"Failed to load generated image for reference {referenceId}");

                var referenceFileName = $"{referenceId}.png";
                await _storageService.SaveFileBytesAsync(
                    "case-context",
                    $"{caseId}/references/{referenceFileName}",
                    imageBytes,
                    cancellationToken);

                var referencePath = $"case-context/{caseId}/references/{referenceFileName}";
                var originalRef = referencesElement.GetProperty(referenceId);
                var updatedRef = UpdateReferenceWithImageUrl(originalRef, referencePath);
                updatedReferences[referenceId] = updatedRef;

                generatedCount++;
                _logger.LogInformation("GENERATE-MASTER-REF-COMPLETE: {ReferenceId}, size={Size}bytes", 
                    referenceId, imageBytes.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GENERATE-MASTER-REF-ERROR: Failed for {ReferenceId}", referenceId);
                updatedReferences[referenceId] = referencesElement.GetProperty(referenceId);
            }
        }

        var updatedRegistry = UpdateRegistryWithImageUrls(registryDoc.RootElement, updatedReferences);
        await _contextManager.SaveContextAsync(caseId, "visual-registry", updatedRegistry, cancellationToken);

        _logger.LogInformation("GENERATE-MASTER-REFS-COMPLETE: Generated {GeneratedCount}/{TotalCount}",
            generatedCount, referencesList.Count);

        return generatedCount;
    }

    /// <summary>
    /// LEGACY: Original monolithic design method (Phase 1).
    /// Generates complete document and media specs in one LLM call.
    /// </summary>
    public async Task<string> DesignCaseAsync(string planJson, string expandedJson, string caseId, string? difficulty = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Designing case structure");

        using var planDoc = JsonDocument.Parse(planJson);
        var planDifficulty = planDoc.RootElement.GetProperty("difficulty").GetString() ?? "Rookie";
        var difficultyProfile = DifficultyLevels.GetProfile(difficulty ?? planDifficulty);

        (int minDocs, int maxDocs) = difficultyProfile.Documents;
        (int minEvid, int maxEvid) = difficultyProfile.Evidences;

        if (planDoc.RootElement.TryGetProperty("profileApplied", out var prof))
        {
            if (prof.TryGetProperty("documents", out var docsArr) && docsArr.ValueKind == JsonValueKind.Array && docsArr.GetArrayLength() == 2)
            { minDocs = docsArr[0].GetInt32(); maxDocs = docsArr[1].GetInt32(); }

            if (prof.TryGetProperty("evidences", out var evidArr) && evidArr.ValueKind == JsonValueKind.Array && evidArr.GetArrayLength() == 2)
            { minEvid = evidArr[0].GetInt32(); maxEvid = evidArr[1].GetInt32(); }
        }

        var gatedDocsCount = difficultyProfile.GatedDocuments;
        var minMedia = Math.Max(2, minEvid);
        var maxMedia = Math.Max(minMedia, maxEvid);

        var systemPrompt = @"
            You are an investigative case designer. Convert plan/expansion into document and media specifications.

            IMPORTANT (JSON conforming to DocumentAndMediaSpecs schema):
            - All text in english
            - Mark sensitive documents with ""gated"": true and ""gatingRule""
            - Forensic reports MUST contain ""Cadeia de Custódia"" (Chain of Custody) section
            - No real addresses/brands
            - Allowed doc types: police_report, interview, memo_admin, forensics_report, evidence_log, witness_statement
            - Allowed media types: photo, document_scan, diagram

            TEMPORAL CONSISTENCY (CRITICAL):
            - ALL timestamps MUST use consistent timezone offset from Expand
            - Document dateCreated must follow incident timeline logically
            - Evidence collectedAt must be realistic and chronologically sound
            - NO overlapping or conflicting timestamps

            MANDATORY CONSISTENCY:
            - Names/evidence must match 1:1 with Expand
            - Every suspect must have ≥1 document
            - Every witness must have ≥1 witness_statement
            - Every evidence item must appear in mediaSpecs OR be referenced in documents
            ";

        var userPrompt = $@"
            Transform this case into structured specifications.

            Difficulty: {difficulty ?? planDifficulty}

            EXPANDED CONTEXT:
            {expandedJson}

            QUANTITY RULES:
            - Documents: {minDocs}-{maxDocs}
            - Media items: {minMedia}-{maxMedia}
            - Gated documents: {gatedDocsCount}

            MINIMUM COVERAGE:
            - ≥1 evidence_log and ≥1 police_report
            - For EACH suspect: ≥1 document (interview/witness_statement/memo)
            - For EACH witness: ≥1 witness_statement
            - For EACH evidence: mediaSpec OR document reference

            OUTPUT: ONLY JSON valid by DocumentAndMediaSpecs schema.";

        var jsonSchema = _schemaProvider.GetSchema("DocumentAndMediaSpecs");
        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var response = await _llmService.GenerateStructuredAsync(caseId, systemPrompt, userPrompt, jsonSchema, cancellationToken);
                var validationResult = await _schemaValidationService.ParseAndValidateAsync(response, difficulty ?? planDifficulty);
                if (validationResult != null) return response;
                if (attempt == maxRetries) throw new InvalidOperationException("Design specs failed validation after retries");
            }
            catch when (attempt < maxRetries) { /* retry */ }
        }
        throw new InvalidOperationException("Failed to generate design specs");
    }

    // Helper methods
    private async Task<string> LoadContextAsync(string caseId, string path, CancellationToken cancellationToken)
    {
        var snapshot = await _contextManager.BuildSnapshotAsync(caseId, new[] { $"@{path}" }, cancellationToken);
        
        if (!snapshot.Items.ContainsKey(path))
            throw new InvalidOperationException($"Context {path} not found");
        
        var item = snapshot.Items[path];
        if (item == null) throw new InvalidOperationException($"Context {path} is null");
        
        if (item is string str) return str;
        if (item is JsonElement jsonElement) return jsonElement.GetRawText();
        
        return JsonSerializer.Serialize(item);
    }

    private string BuildMasterReferencePrompt(string referenceId, string category, string description, string[] colorPalette)
    {
        var promptBuilder = new StringBuilder();
        promptBuilder.AppendLine("MASTER REFERENCE IMAGE - ISOLATED STUDIO PHOTOGRAPHY");
        promptBuilder.AppendLine($"Subject ID: {referenceId}");
        promptBuilder.AppendLine($"Category: {category}");
        promptBuilder.AppendLine();

        switch (category)
        {
            case "physical_evidence":
                promptBuilder.AppendLine("SETUP: Clean white background, soft lighting, centered, forensic scale ruler, maximum detail");
                break;
            case "suspect":
                promptBuilder.AppendLine("SETUP: Neutral gray background, even frontal lighting, mugshot framing, neutral expression");
                break;
            case "location":
                promptBuilder.AppendLine("SETUP: Wide-angle, even lighting, empty scene, focus on structural elements");
                break;
        }

        promptBuilder.AppendLine();
        promptBuilder.AppendLine("SUBJECT DESCRIPTION:");
        promptBuilder.AppendLine(description);
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("COLOR PALETTE:");
        foreach (var color in colorPalette) promptBuilder.AppendLine($"  - {color}");

        return promptBuilder.ToString();
    }

    private JsonElement UpdateReferenceWithImageUrl(JsonElement originalRef, string imageUrl)
    {
        var refDict = new Dictionary<string, object?>();
        
        foreach (var prop in originalRef.EnumerateObject())
        {
            refDict[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString(),
                JsonValueKind.Number => prop.Value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Array => prop.Value.EnumerateArray().Select(e => e.GetString()).ToArray(),
                _ => null
            };
        }

        refDict["imageUrl"] = imageUrl;
        var json = JsonSerializer.Serialize(refDict);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private string UpdateRegistryWithImageUrls(JsonElement originalRegistry, Dictionary<string, JsonElement> updatedReferences)
    {
        var registryDict = new Dictionary<string, object?>();

        foreach (var prop in originalRegistry.EnumerateObject())
        {
            if (prop.Name == "references")
            {
                var refsDict = new Dictionary<string, JsonElement>();
                foreach (var (key, value) in updatedReferences)
                {
                    refsDict[key] = value;
                }
                registryDict["references"] = refsDict;
            }
            else
            {
                registryDict[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => prop.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => null
                };
            }
        }

        return JsonSerializer.Serialize(registryDict, new JsonSerializerOptions { WriteIndented = true });
    }
}
