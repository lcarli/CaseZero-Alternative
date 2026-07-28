using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CaseGen.Functions.Services.CaseV2;

public enum AgentPromptRole
{
    System,
    User
}

public sealed partial class AgentPromptCatalog
{
    private static readonly Lazy<AgentPromptCatalog> LazyDefault = new(() => new(ResolveDefaultRoot()));
    private readonly ConcurrentDictionary<string, string> _templates = new(StringComparer.OrdinalIgnoreCase);

    public AgentPromptCatalog(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
            throw new ArgumentException("Prompt root directory is required.", nameof(rootDirectory));
        RootDirectory = Path.GetFullPath(rootDirectory);
        if (!Directory.Exists(RootDirectory))
            throw new DirectoryNotFoundException($"CaseV2 prompt directory was not found: {RootDirectory}");
    }

    public static AgentPromptCatalog Default => LazyDefault.Value;
    public string RootDirectory { get; }

    public string RenderSystem(string agentName, IReadOnlyDictionary<string, object?> variables) =>
        Render(agentName, AgentPromptRole.System, variables);

    public string RenderUser(string agentName, IReadOnlyDictionary<string, object?> variables) =>
        Render(agentName, AgentPromptRole.User, variables);

    public string Render(
        string agentName,
        AgentPromptRole role,
        IReadOnlyDictionary<string, object?> variables)
    {
        ValidateAgentName(agentName);
        ArgumentNullException.ThrowIfNull(variables);
        var template = Load(agentName, role);
        var missing = PlaceholderRegex().Matches(template)
            .Select(match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .Where(name => !variables.ContainsKey(name) || variables[name] is null)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"Prompt template '{agentName}.{RoleName(role)}.md' is missing variables: {string.Join(", ", missing)}");
        }

        return PlaceholderRegex().Replace(template, match =>
        {
            var name = match.Groups["name"].Value;
            var value = variables[name];
            return value switch
            {
                string text => text,
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value?.ToString() ?? string.Empty
            };
        });
    }

    public string GetTemplatePath(string agentName, AgentPromptRole role)
    {
        ValidateAgentName(agentName);
        return Path.Combine(RootDirectory, $"{agentName}.{RoleName(role)}.md");
    }

    public static string ResolveDefaultRoot()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "agents", "case-v2"),
            Path.Combine(Directory.GetCurrentDirectory(), "agents", "case-v2"),
            Path.Combine(Directory.GetCurrentDirectory(), "functions", "CaseGen.Functions", "agents", "case-v2")
        };
        return candidates.FirstOrDefault(Directory.Exists)
               ?? throw new DirectoryNotFoundException(
                   "Could not locate CaseV2 agent prompts. Checked: " + string.Join("; ", candidates));
    }

    private string Load(string agentName, AgentPromptRole role)
    {
        var path = GetTemplatePath(agentName, role);
        return _templates.GetOrAdd(path, static value =>
        {
            if (!File.Exists(value))
                throw new FileNotFoundException($"CaseV2 prompt template was not found: {value}", value);
            return File.ReadAllText(value);
        });
    }

    private static void ValidateAgentName(string agentName)
    {
        if (string.IsNullOrWhiteSpace(agentName)
            || !string.Equals(agentName, Path.GetFileName(agentName), StringComparison.Ordinal)
            || agentName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("Agent name must be a simple file name.", nameof(agentName));
        }
    }

    private static string RoleName(AgentPromptRole role) =>
        role == AgentPromptRole.System ? "system" : "user";

    [GeneratedRegex("\\{\\{(?<name>[A-Za-z][A-Za-z0-9_.-]*)\\}\\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderRegex();
}
