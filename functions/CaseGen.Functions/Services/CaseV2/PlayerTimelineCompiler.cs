using CaseGen.Functions.Models.CaseV2;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.CaseV2;

public static class PlayerTimelineCompiler
{
    public static List<EvidenceTimelineEntry> Compile(CaseDraft draft, ILogger logger)
    {
        var culprit = draft.SuspectStubs.FirstOrDefault(s => s.Id == draft.CulpritId);
        var publicEvents = draft.Blueprint.IncidentSequence
            .Where(b => string.Equals(b.Visibility, "public", StringComparison.OrdinalIgnoreCase))
            .Where(b => !string.IsNullOrWhiteSpace(b.PublicDescription))
            .Where(b => !LeaksCulprit(b.PublicDescription!, culprit))
            .Select(b => new EvidenceTimelineEntry
            {
                Time = b.Time,
                Event = b.PublicDescription!,
                Source = NormalizeSource(b.Source),
                Verified = b.Verified,
                Importance = NormalizeImportance(b.Importance)
            })
            .OrderBy(e => e.Time, StringComparer.Ordinal)
            .ToList();

        if (publicEvents.Count < 2)
        {
            logger.LogWarning(
                "Blueprint produced only {Count} safe public timeline event(s); adding neutral case anchors",
                publicEvents.Count);
            publicEvents.Insert(0, new EvidenceTimelineEntry
            {
                Time = draft.Metadata.IncidentDate,
                Event = draft.Metadata.Briefing,
                Source = "investigation",
                Verified = false,
                Importance = "high"
            });
            publicEvents.Add(new EvidenceTimelineEntry
            {
                Time = draft.Metadata.OpenedAt,
                Event = OpeningMessage(draft.Request.Language),
                Source = "investigation",
                Verified = true,
                Importance = "medium"
            });
        }

        return publicEvents
            .Where(e => !string.IsNullOrWhiteSpace(e.Time))
            .DistinctBy(e => new { e.Time, e.Event })
            .OrderBy(e => e.Time, StringComparer.Ordinal)
            .Take(8)
            .ToList();
    }

    private static string OpeningMessage(string? language) => language?.ToLowerInvariant() switch
    {
        "pt-br" => "A investigação foi formalmente aberta após o registro inicial e a preservação das evidências.",
        "es-es" => "La investigación se abrió formalmente tras el informe inicial y la preservación de las pruebas.",
        "fr-fr" => "L'enquête a été officiellement ouverte après le signalement initial et la préservation des preuves.",
        _ => "The investigation was formally opened after the initial report and evidence preservation."
    };

    private static string NormalizeSource(string? source) =>
        source?.Trim().ToLowerInvariant() switch
        {
            "witness" => "witness",
            "sensor" or "digital" or "system" or "log" => "sensor",
            "forensic" => "forensic",
            _ => "investigation"
        };

    private static string NormalizeImportance(string? importance) =>
        importance?.Trim().ToLowerInvariant() switch
        {
            "low" => "low",
            "medium" => "medium",
            "critical" => "critical",
            _ => "high"
        };

    private static bool LeaksCulprit(string text, SuspectStub? culprit)
    {
        if (culprit is null || string.IsNullOrWhiteSpace(culprit.Name)) return false;
        var forms = culprit.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Append(culprit.Name)
            .Where(part => part.Length >= 4);
        return forms.Any(form => text.Contains(form, StringComparison.OrdinalIgnoreCase));
    }
}
