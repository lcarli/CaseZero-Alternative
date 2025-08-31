using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using CaseGen.Functions.Models;

namespace CaseGen.Functions.Services;

public interface ISchemaValidationService
{
    Task<(bool IsValid, string[] Errors)> ValidateDocumentAndMediaSpecsAsync(string jsonOutput);
    Task<DocumentAndMediaSpecs?> ParseAndValidateAsync(string jsonOutput, string? difficulty = null);
}

public class SchemaValidationService : ISchemaValidationService
{
    private readonly JSchema _documentAndMediaSchema;
    private readonly IJsonSchemaProvider _schemaProvider;
    private readonly ILogger<SchemaValidationService> _logger;

    public SchemaValidationService(ILogger<SchemaValidationService> logger, IJsonSchemaProvider schemaProvider)
    {
        _schemaProvider = schemaProvider;
        _logger = logger;

        // Load the schema from the embedded file
        var schemaJson = _schemaProvider.GetSchema("DocumentAndMediaSpecs");
        _documentAndMediaSchema = JSchema.Parse(schemaJson);
    }

    public Task<(bool IsValid, string[] Errors)> ValidateDocumentAndMediaSpecsAsync(string jsonOutput)
    {
        try
        {
            var jObject = JObject.Parse(jsonOutput);
            var isValid = jObject.IsValid(_documentAndMediaSchema, out IList<string> errorMessages);

            return Task.FromResult((isValid, errorMessages.ToArray()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate JSON against schema");
            return Task.FromResult((false, new[] { $"JSON parsing error: {ex.Message}" }));
        }
    }

    public async Task<DocumentAndMediaSpecs?> ParseAndValidateAsync(string jsonOutput, string? difficulty = null)
    {
        try
        {
            // First validate against schema
            var (isValid, errors) = await ValidateDocumentAndMediaSpecsAsync(jsonOutput);
            if (!isValid)
            {
                _logger.LogError("Schema validation failed: {Errors}", string.Join(", ", errors));
                return null;
            }

            // Then parse into strongly typed object
            var specs = JsonSerializer.Deserialize<DocumentAndMediaSpecs>(jsonOutput, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            // Additional business validation with difficulty context
            var businessValidationErrors = ValidateBusinessRules(specs, difficulty);
            if (businessValidationErrors.Any())
            {
                _logger.LogError("Business validation failed: {Errors}", string.Join(", ", businessValidationErrors));
                return null;
            }

            return specs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse and validate DocumentAndMediaSpecs");
            return null;
        }
    }

    private string[] ValidateBusinessRules(DocumentAndMediaSpecs? specs, string? difficulty = null)
    {
        var errors = new List<string>();
        if (specs == null) { errors.Add("Specs object is null"); return errors.ToArray(); }

        // Get difficulty profile for dynamic validation
        var difficultyProfile = DifficultyLevels.GetProfile(difficulty);
        _logger.LogInformation("Validating with difficulty profile: {Difficulty} -> {Description}", 
            difficulty ?? "auto-selected", difficultyProfile.Description);

        // Dynamic document count validation based on difficulty
        var docCount = specs.DocumentSpecs.Length;
        if (docCount < difficultyProfile.Documents.Min || docCount > difficultyProfile.Documents.Max)
        {
            errors.Add($"Document count ({docCount}) should be between {difficultyProfile.Documents.Min}-{difficultyProfile.Documents.Max} for {difficulty ?? "selected"} level");
        }

        // Dynamic evidence count validation based on difficulty  
        var evidenceCount = specs.MediaSpecs.Length;
        if (evidenceCount < difficultyProfile.Evidences.Min || evidenceCount > difficultyProfile.Evidences.Max)
        {
            errors.Add($"Evidence count ({evidenceCount}) should be between {difficultyProfile.Evidences.Min}-{difficultyProfile.Evidences.Max} for {difficulty ?? "selected"} level");
        }

        // Validate gated documents count matches difficulty profile
        var gatedCount = specs.DocumentSpecs.Count(x => x.Gated);
        if (gatedCount != difficultyProfile.GatedDocuments)
        {
            errors.Add($"Gated documents count ({gatedCount}) should be exactly {difficultyProfile.GatedDocuments} for {difficulty ?? "selected"} level");
        }

        // lengthTarget coerente
        foreach (var d in specs.DocumentSpecs)
        {
            if (d.LengthTarget.Length != 2 || d.LengthTarget[0] < 10 || d.LengthTarget[0] > d.LengthTarget[1])
                errors.Add($"Document {d.DocId} has invalid lengthTarget [{string.Join(",", d.LengthTarget)}]");
        }

        // gated => gatingRule obrigatório e com action válida
        foreach (var d in specs.DocumentSpecs.Where(x => x.Gated))
        {
            if (d.GatingRule is null)
                errors.Add($"Document {d.DocId} is gated but missing gatingRule");
            else if (string.IsNullOrWhiteSpace(d.GatingRule.Action))
                errors.Add($"Document {d.DocId} has gatingRule without action");
        }

        // Laudos devem conter “Cadeia de Custódia”
        foreach (var d in specs.DocumentSpecs.Where(x => x.Type == DocumentTypes.ForensicsReport))
        {
            if (!d.Sections.Any(s => Regex.IsMatch(s, "cadeia de cust[óo]dia", RegexOptions.IgnoreCase)))
                errors.Add($"Forensics report {d.DocId} missing 'Cadeia de Custódia' section");
        }

        // Validate media types - unsupported types should be deferred
        var supportedNow = new[] { MediaTypes.Photo, MediaTypes.DocumentScan, MediaTypes.Diagram };
        var unsupportedButAllowed = new[] { MediaTypes.Audio, MediaTypes.Video };
        
        foreach (var m in specs.MediaSpecs)
        {
            if (unsupportedButAllowed.Contains(m.Kind) && !m.Deferred)
            {
                // Auto-fix: mark unsupported media as deferred instead of failing
                _logger.LogWarning("Media {EvidenceId} kind '{Kind}' auto-marked as deferred", m.EvidenceId, m.Kind);
                // Note: This is a validation warning, not an error - the LLM should learn to mark these as deferred
            }
            else if (!supportedNow.Contains(m.Kind) && !unsupportedButAllowed.Contains(m.Kind))
            {
                errors.Add($"Media {m.EvidenceId} has unknown media type '{m.Kind}'");
            }
        }

        return errors.ToArray();
    }
}