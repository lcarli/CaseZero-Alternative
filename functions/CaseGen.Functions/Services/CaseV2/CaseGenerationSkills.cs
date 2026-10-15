using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CaseGen.Functions.Services.CaseV2;

public sealed class CaseGenerationSkill
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("version")] public string Version { get; set; } = string.Empty;
    [JsonPropertyName("instructions")] public List<string> Instructions { get; set; } = [];
    [JsonPropertyName("contract")] public List<string> Contract { get; set; } = [];
    [JsonPropertyName("positiveExample")] public string PositiveExample { get; set; } = string.Empty;
    [JsonPropertyName("negativeExample")] public string NegativeExample { get; set; } = string.Empty;
    [JsonPropertyName("criteria")] public List<string> Criteria { get; set; } = [];
    [JsonIgnore] public string Hash { get; set; } = string.Empty;

    public string ToPrompt() => $"""
        Runtime skill: {Id}@{Version}
        {string.Join("\n", Instructions)}
        Contract:
        {string.Join("\n", Contract)}
        Positive example: {PositiveExample}
        Invalid example: {NegativeExample}
        Acceptance:
        {string.Join("\n", Criteria)}
        """;
}

public sealed class CaseGenerationSkillRegistry
{
    public static readonly IReadOnlyList<string> RequiredIds =
    [
        "case-architect", "timeline-alibi", "evidence-designer",
        "forensic-reviewer", "artifact-author", "blind-solver"
    ];
    private readonly IReadOnlyDictionary<string, CaseGenerationSkill> _skills;

    public CaseGenerationSkillRegistry(string directory)
    {
        var skills = new Dictionary<string, CaseGenerationSkill>(StringComparer.Ordinal);
        var options = new JsonSerializerOptions
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        foreach (var id in RequiredIds)
        {
            var path = Path.Combine(directory, $"{id}.json");
            if (!File.Exists(path))
                throw new TaskContractException($"Required runtime skill is missing: {id}.");
            var content = File.ReadAllText(path);
            using var document = StructuredJson.ParseObject(content);
            var skill = JsonSerializer.Deserialize<CaseGenerationSkill>(content, options)
                ?? throw new TaskContractException($"Invalid runtime skill: {id}.");
            if (skill.Id != id || !System.Text.RegularExpressions.Regex.IsMatch(skill.Version, @"\A\d+\.\d+\.\d+\z")
                || skill.Instructions.Count == 0 || skill.Contract.Count == 0 || skill.Criteria.Count == 0
                || skill.Instructions.Concat(skill.Contract).Concat(skill.Criteria).Any(string.IsNullOrWhiteSpace)
                || string.IsNullOrWhiteSpace(skill.PositiveExample) || string.IsNullOrWhiteSpace(skill.NegativeExample))
                throw new TaskContractException($"Runtime skill {id} has an invalid version or incomplete contract.");
            skill.Hash = Hash(content);
            skills.Add(id, skill);
        }
        _skills = skills;
    }

    public IReadOnlyDictionary<string, string> Fingerprints =>
        _skills.ToDictionary(pair => $"{pair.Key}@{pair.Value.Version}", pair => pair.Value.Hash, StringComparer.Ordinal);

    public IReadOnlyList<CaseGenerationSkill> ForTask(string taskName)
    {
        var name = taskName.ToLowerInvariant();
        string[] ids = name switch
        {
            var value when value.Contains("blind") => ["blind-solver"],
            var value when value.Contains("forensic") || value.Contains("outcome") => ["forensic-reviewer", "artifact-author"],
            var value when value.Contains("timeline") || value.Contains("suspect") => ["timeline-alibi", "artifact-author"],
            var value when value.Contains("bible") || value.Contains("plot") => ["case-architect", "timeline-alibi"],
            var value when value.Contains("specialist") || value.Contains("redteam") =>
                ["case-architect", "timeline-alibi", "evidence-designer", "forensic-reviewer", "artifact-author"],
            _ => ["evidence-designer", "artifact-author"]
        };
        return ids.Select(id => _skills[id]).ToArray();
    }

    public static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}
