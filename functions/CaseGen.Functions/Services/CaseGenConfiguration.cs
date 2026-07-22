using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace CaseGen.Functions.Services;

public static class CaseGenConfiguration
{
    public static IConfigurationBuilder AddCaseGenLocalSettings(
        this IConfigurationBuilder builder,
        string functionsDirectory,
        string fileName = "local.settings.json")
    {
        var path = Path.Combine(functionsDirectory, fileName);
        if (!File.Exists(path))
            return builder;

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("Values", out var values)
            || values.ValueKind != JsonValueKind.Object)
            return builder;

        var flattened = values.EnumerateObject()
            .Where(property => property.Value.ValueKind == JsonValueKind.String)
            .ToDictionary(
                property => property.Name.Replace("__", ":", StringComparison.Ordinal),
                property => (string?)property.Value.GetString(),
                StringComparer.OrdinalIgnoreCase);
        return builder.AddInMemoryCollection(flattened);
    }
}
