using System.Text.Json.Serialization;

namespace CaseGen.Functions.Models.CaseV2;

public class CaseAuthoredContent
{
    [JsonPropertyName("support")] public List<CaseContentSupport> Support { get; set; } = new();
}

public sealed class CaseContentSupport
{
    [JsonPropertyName("referenceId")] public string ReferenceId { get; set; } = "";
    [JsonPropertyName("excerpt")] public string Excerpt { get; set; } = "";
}
