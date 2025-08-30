using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services;

public class FileJsonSchemaProvider : IJsonSchemaProvider
{
    private readonly string _basePath;
    private readonly ILogger<FileJsonSchemaProvider> _logger;

    public FileJsonSchemaProvider(IConfiguration cfg, IHostEnvironment env, ILogger<FileJsonSchemaProvider> logger)
    {
        _logger = logger;
        // raiz padrão = ContentRootPath/Schemas (ou override por SCHEMAS_BASE_PATH)
        var configured = cfg["SCHEMAS_BASE_PATH"];
        var root = string.IsNullOrWhiteSpace(configured) ? "Schemas" : configured;

        _basePath = Path.IsPathRooted(root) ? root : Path.Combine(env.ContentRootPath, root);
        _logger.LogInformation("JsonSchemaProvider basePath: {BasePath}", _basePath);
    }

    public string GetSchema(string name)
    {
        var file = name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? name
            : (name.EndsWith(".schema", StringComparison.OrdinalIgnoreCase) ? $"{name}.json" : $"{name}.schema.json");

        var path = Path.Combine(_basePath, file);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"JSON schema file not found: {path}");
        }

        return File.ReadAllText(path);
    }
}
