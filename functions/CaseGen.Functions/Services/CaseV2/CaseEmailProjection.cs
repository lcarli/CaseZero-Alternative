using System.Globalization;
using System.Text.Json.Serialization;
using CaseGen.Functions.Models.CaseV2;

namespace CaseGen.Functions.Services.CaseV2;

public sealed record CaseNoFindingsTemplate(
    [property: JsonPropertyName("from")] string From,
    [property: JsonPropertyName("subject")] string Subject,
    [property: JsonPropertyName("template")] string Template);

public static class CaseEmailProjection
{
    public static List<string> BriefingAttachments(CaseDraft draft) =>
        draft.AssetFull.Where(asset => asset.Visibility == "initial").Take(2).Select(asset => asset.Id).ToList();

    public static CaseNoFindingsTemplate NoFindingsTemplate(CaseDraft draft)
    {
        var agency = draft.Blueprint.Locale.PoliceAgency;
        var investigator = draft.Blueprint.Locale.InvestigatorName;
        return draft.Request.Language?.ToLowerInvariant() switch
        {
            "pt-br" => new(agency, "Resultado da análise — {{analysisType}} — Sem achados",
                $"{investigator},\n\nA análise {{{{analysisType}}}} de {{{{assetName}}}} não revelou elementos úteis para a investigação.\n\n{agency}"),
            "es-es" => new(agency, "Resultado del análisis — {{analysisType}} — Sin hallazgos",
                $"{investigator},\n\nEl análisis {{{{analysisType}}}} de {{{{assetName}}}} no reveló hallazgos útiles para la investigación.\n\n{agency}"),
            "fr-fr" => new(agency, "Résultat de l'analyse — {{analysisType}} — Aucun résultat",
                $"{investigator},\n\nL'analyse {{{{analysisType}}}} de {{{{assetName}}}} n'a révélé aucun élément utile à l'enquête.\n\n{agency}"),
            _ => new(agency, "Analysis result — {{analysisType}} — No findings",
                $"{investigator},\n\nThe {{{{analysisType}}}} analysis of {{{{assetName}}}} did not reveal any actionable findings.\n\n{agency}")
        };
    }

    public static EvidenceEmail NoFindings(CaseDraft draft, int requestId, string analysisType, string assetName, int gameMinutes)
    {
        TaskContractException.Require(requestId > 0, "A simulated forensic request requires a positive request ID.");
        if (!DateTimeOffset.TryParse(draft.Metadata.OpenedAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var openedAt))
            throw new TaskContractException("A simulated forensic completion requires a valid public game-clock origin.");
        var template = NoFindingsTemplate(draft);
        string Expand(string value) => value.Replace("{{analysisType}}", analysisType, StringComparison.Ordinal)
            .Replace("{{assetName}}", assetName, StringComparison.Ordinal);
        return new()
        {
            Id = $"forensic-{requestId}-no-findings",
            From = template.From, Subject = Expand(template.Subject), Body = Expand(template.Template),
            SentAt = openedAt.AddMinutes(gameMinutes).ToString("o", CultureInfo.InvariantCulture), Visibility = "initial"
        };
    }
}
