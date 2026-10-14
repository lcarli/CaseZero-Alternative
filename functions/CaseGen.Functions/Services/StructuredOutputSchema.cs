using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CaseGen.Functions.Services;

public static class StructuredOutputSchema
{
    private static readonly HashSet<string> UnsupportedKeywords = new(StringComparer.Ordinal)
    {
        "$schema", "$id", "title", "default", "examples", "minLength", "maxLength", "pattern", "format",
        "minimum", "maximum", "exclusiveMinimum", "exclusiveMaximum", "multipleOf",
        "minItems", "maxItems", "uniqueItems", "minProperties", "maxProperties"
    };

    public static bool TryCreate(string jsonSchema, out string nativeSchema, out string? reason)
    {
        var root = JObject.Parse(jsonSchema);
        var schemas = EnumerateSchemas(root).ToList();
        if (schemas.Any(s => s["type"]?.Type == JTokenType.String && s["type"]?.Value<string>() == "object" && s["additionalProperties"]?.Type != JTokenType.Boolean
            || s["additionalProperties"]?.Value<bool>() == true))
        {
            nativeSchema = string.Empty;
            reason = "The schema contains an open object.";
            return false;
        }
        if (schemas.Sum(s => (s["properties"] as JObject)?.Count ?? 0) > 100 ||
            ObjectDepth(root, root, new HashSet<string>(StringComparer.Ordinal)) > 5)
        {
            nativeSchema = string.Empty;
            reason = "The schema exceeds the deployment-compatible 100-property/five-object-level limits.";
            return false;
        }
        foreach (var schema in schemas.AsEnumerable().Reverse())
        {
            foreach (var keyword in UnsupportedKeywords) schema.Remove(keyword);
            if (schema.Remove("oneOf", out var alternatives)) schema["anyOf"] = alternatives;
            if (schema.Remove("const", out var constant)) schema["enum"] = new JArray(constant ?? JValue.CreateNull());
            if (schema["properties"] is JObject properties)
            {
                schema["additionalProperties"] = false;
                schema["required"] = new JArray(properties.Properties().Select(p => p.Name));
            }
        }
        nativeSchema = root.ToString(Formatting.None);
        reason = null;
        return true;
    }

    private static IEnumerable<JObject> EnumerateSchemas(JObject schema)
    {
        yield return schema;
        foreach (var keyword in new[] { "properties", "$defs", "definitions" })
        {
            if (schema[keyword] is not JObject children) continue;
            foreach (var child in children.Properties().Select(p => p.Value).OfType<JObject>())
            foreach (var nested in EnumerateSchemas(child)) yield return nested;
        }
        if (schema["items"] is JObject items)
            foreach (var nested in EnumerateSchemas(items)) yield return nested;
        foreach (var keyword in new[] { "oneOf", "anyOf" })
        {
            if (schema[keyword] is not JArray alternatives) continue;
            foreach (var child in alternatives.OfType<JObject>())
            foreach (var nested in EnumerateSchemas(child)) yield return nested;
        }
    }

    private static int ObjectDepth(JObject schema, JObject root, HashSet<string> references)
    {
        if (schema["$ref"]?.Value<string>() is string reference)
        {
            if (!references.Add(reference)) return 0;
            var resolved = reference == "#" ? root : root["$defs"]?[reference["#/$defs/".Length..]] as JObject;
            if (resolved is null) throw new InvalidOperationException($"Unsupported schema reference '{reference}'.");
            var depth = ObjectDepth(resolved, root, references);
            references.Remove(reference);
            return depth;
        }
        var children = new List<JObject>();
        if (schema["properties"] is JObject properties) children.AddRange(properties.Properties().Select(p => p.Value).OfType<JObject>());
        if (schema["items"] is JObject items) children.Add(items);
        foreach (var keyword in new[] { "anyOf", "oneOf" })
            if (schema[keyword] is JArray alternatives) children.AddRange(alternatives.OfType<JObject>());
        var ownDepth = schema["type"]?.Type == JTokenType.String && schema["type"]?.Value<string>() == "object" ? 1 : 0;
        return ownDepth + (children.Count == 0 ? 0 : children.Max(child => ObjectDepth(child, root, references)));
    }
}
