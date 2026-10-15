using System.Text.Json;
using CaseGen.Functions.Services.CaseV2;

namespace CaseGen.Functions.Tests.CaseV2;

internal static partial class CaseBibleTestData
{
    public static CaseBible Create(bool photo = false, bool orphanInput = false, bool rookie = false)
    {
        var json = JsonSerializer.Serialize(rookie ? ValidRookieBible() : ValidDetectiveBible())
            .Replace("suspect.alex", "suspect.jordan", StringComparison.Ordinal)
            .Replace("suspect.blair", "suspect.riley", StringComparison.Ordinal)
            .Replace("suspect.casey", "suspect.sam", StringComparison.Ordinal)
            .Replace("Alex Mercer", "jordan", StringComparison.Ordinal)
            .Replace("Alex", "jordan", StringComparison.Ordinal)
            .Replace("Blair Chen", "riley", StringComparison.Ordinal)
            .Replace("Blair", "riley", StringComparison.Ordinal)
            .Replace("Casey Ortiz", "sam", StringComparison.Ordinal)
            .Replace("Casey", "sam", StringComparison.Ordinal)
            .Replace("_alex", "_jordan", StringComparison.Ordinal)
            .Replace("_blair", "_riley", StringComparison.Ordinal)
            .Replace("_casey", "_sam", StringComparison.Ordinal)
            .Replace("call", "connection", StringComparison.OrdinalIgnoreCase)
            .Replace("message", "notice", StringComparison.OrdinalIgnoreCase);
        var bible = JsonSerializer.Deserialize<CaseBible>(json)!;
        return bible;
    }

    private static CaseBible WithInitialAlibis(CaseBible bible)
    {
        foreach (var person in bible.People.Where(p => p.SuspectId is not null && !string.IsNullOrWhiteSpace(p.StatedAlibi)))
        {
            var suffix = person.SuspectId!["suspect.".Length..];
            person.Email = $"{suffix}@casezero.local";
            var sourceId = $"source.statement_{suffix}";
            var factId = $"fact.stated_alibi_{suffix}";
            bible.Sources.Add(new CaseBibleSource
            {
                Id = sourceId, Kind = CaseBibleSourceKind.Testimony, Name = $"Recorded statement — {person.Name}",
                Description = "An attributed account, not independently verified.",
                AuthorPersonId = person.Id, CustodianPersonId = "person.investigator", RecordedAt = bible.Incident.OpenedAt
            });
            bible.Facts.Add(new CaseBibleFact
            {
                Id = factId, Predicate = "statedAlibi", SubjectId = person.Id, LiteralValue = person.StatedAlibi,
                LiteralType = LiteralValueType.String, Visibility = FactVisibility.Public, TruthStatus = FactTruthStatus.Disputed
            });
            bible.Observations.Add(new CaseBibleObservation
            {
                Id = $"observation.stated_alibi_{suffix}", FactId = factId, SourceId = sourceId,
                Statement = person.StatedAlibi!, SpeakerPersonId = person.Id, ObservedAt = bible.Incident.OpenedAt,
                Fidelity = ObservationFidelity.Testimony, Reliability = ObservationReliability.Unverified
            });
        }
        return bible;
    }
}
