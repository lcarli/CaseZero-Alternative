using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace CaseGen.Functions.Services.CaseV2;

public sealed class TaskContractException : Exception
{
    public TaskContractException(string message) : base(message) { }

    public static void Require(bool condition, string message)
    {
        if (!condition) throw new TaskContractException(message);
    }
}

public static class StructuredJson
{
    public const int MaxResponseBytes = 256 * 1024;

    static StructuredJson()
    {
        // The library's built-in date-time regex accepts nonexistent calendar dates.
        var dateTime = Formats.DateTime;
        Formats.Register(new PredicateFormat("date-time", node =>
        {
            if (node is not JsonValue value || !value.TryGetValue<string>(out var text)) return true;
            return dateTime.Validate(node, out _) && text == text.Trim() && text.Length > 10 && (text[10] is 'T' or 't') &&
                DateTimeOffset.TryParse(text.ToUpperInvariant(), CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
        }));
    }

    public static JsonDocument ParseObject(string json, StringComparer? propertyComparer = null)
    {
        var parsed = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 });
        try
        {
            if (parsed.RootElement.ValueKind != JsonValueKind.Object)
                throw new JsonException("The response must be a JSON object.");
            CheckDuplicates(parsed.RootElement, "$", propertyComparer ?? StringComparer.Ordinal);
            return parsed;
        }
        catch (JsonException)
        {
            parsed.Dispose();
            throw;
        }
    }

    public static JsonSchema ParseSchema(string json) => JsonSchema.FromText(json);

    public static List<string> SchemaErrors(JsonDocument document, JsonSchema schema)
    {
        var result = schema.Evaluate(JsonSerializer.SerializeToNode(document.RootElement), new EvaluationOptions
        {
            EvaluateAs = SpecVersion.Draft202012,
            RequireFormatValidation = true,
            OutputFormat = OutputFormat.List
        });
        if (result.IsValid) return new();
        var errors = Errors(result).Take(10).ToList();
        if (errors.Count == 0)
            throw new InvalidOperationException("Schema validation failed without diagnostic details.");
        return errors;
    }

    public static void Validate(string json, JsonSchema schema)
    {
        TaskContractException.Require(Encoding.UTF8.GetByteCount(json) <= MaxResponseBytes,
            $"Response exceeds {MaxResponseBytes} bytes.");
        using var document = ParseObject(json);
        var errors = SchemaErrors(document, schema);
        if (errors.Count > 0) throw new TaskContractException(string.Join("; ", errors));
    }

    private static IEnumerable<string> Errors(EvaluationResults result)
    {
        if (result.Errors is not null)
            foreach (var error in result.Errors)
                yield return $"${result.InstanceLocation} [{error.Key}]: {error.Value}";
        foreach (var detail in result.Details ?? [])
            foreach (var error in Errors(detail))
                yield return error;
    }

    private static void CheckDuplicates(JsonElement value, string path, StringComparer comparer)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(comparer);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    throw new JsonException($"Duplicate property at {path}.{property.Name}.");
                CheckDuplicates(property.Value, $"{path}.{property.Name}", comparer);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in value.EnumerateArray())
                CheckDuplicates(item, $"{path}[{index++}]", comparer);
        }
    }
}
