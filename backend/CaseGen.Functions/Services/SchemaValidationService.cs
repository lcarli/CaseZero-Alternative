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
    Task<DocumentAndMediaSpecs?> ParseAndValidateAsync(string jsonOutput);
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

    public async Task<DocumentAndMediaSpecs?> ParseAndValidateAsync(string jsonOutput)
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

            // Additional business validation
            var businessValidationErrors = ValidateBusinessRules(specs);
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

    private string[] ValidateBusinessRules(DocumentAndMediaSpecs? specs)
    {
        var errors = new List<string>();
        if (specs == null) { errors.Add("Specs object is null"); return errors.ToArray(); }

        // 8–14 docs para Iniciante (mantém)
        if (specs.DocumentSpecs.Length < 8 || specs.DocumentSpecs.Length > 14)
            errors.Add($"Document count ({specs.DocumentSpecs.Length}) should be between 8-14 for Iniciante level");

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

        // i18nKey deve bater com o pattern do schema: ^[a-z0-9_]+\.[a-z0-9_]+$
        var reI18n = new Regex("^[a-z0-9_]+\\.[a-z0-9_]+$");
        foreach (var d in specs.DocumentSpecs)
            if (!reI18n.IsMatch(d.I18nKey))
                errors.Add($"Document {d.DocId} has invalid i18nKey format: {d.I18nKey}");

        foreach (var m in specs.MediaSpecs)
            if (!reI18n.IsMatch(m.I18nKey))
                errors.Add($"Media {m.EvidenceId} has invalid i18nKey format: {m.I18nKey}");

        // mídias não suportadas podem existir, mas devem vir marcadas como deferred=true
        var supportedNow = new[] { MediaTypes.Photo, MediaTypes.DocumentScan, MediaTypes.Diagram };
        foreach (var m in specs.MediaSpecs)
        {
            if (!supportedNow.Contains(m.Kind) && !m.Deferred)
                errors.Add($"Media {m.EvidenceId} kind '{m.Kind}' not supported yet; set deferred=true");
        }

        return errors.ToArray();
    }


    private bool IsValidI18nKey(string i18nKey)
    {
        // Format: category.identifier (e.g., documents.police_report_001)
        return !string.IsNullOrWhiteSpace(i18nKey) &&
               i18nKey.Contains('.') &&
               i18nKey.Split('.').Length == 2 &&
               i18nKey.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '_');
    }
}