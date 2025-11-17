using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services;

public class FileJsonSchemaProvider : IJsonSchemaProvider
{
    private readonly string _basePath;
    public FileJsonSchemaProvider(IConfiguration cfg, IHostEnvironment env)
    {
        var root = cfg["SCHEMAS_BASE_PATH"];
        _basePath = string.IsNullOrWhiteSpace(root) ? Path.Combine(env.ContentRootPath, "Schemas/v1")
                                                    : (Path.IsPathRooted(root) ? root : Path.Combine(env.ContentRootPath, root));
    }

    public string GetSchema(string name)
    {
        var file = name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? name : $"{name}.schema.json";
        var path = Path.Combine(_basePath, file);
        if (!File.Exists(path)) throw new FileNotFoundException($"Schema not found: {path}");
        return File.ReadAllText(path);
    }
}
