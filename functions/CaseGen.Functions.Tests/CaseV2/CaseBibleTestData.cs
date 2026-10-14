using CaseGen.Functions.Models.CaseV2;

namespace CaseGen.Functions.Tests.CaseV2;

internal static class CaseBibleTestData
{
    public static CaseBible Create(bool photo = false, bool orphanInput = false, bool rookie = false)
    {
        var record = new CaseFact
        {
            Id = "fact.record", Kind = "observation", Decisive = true,
            Statement = "The access log records jordan's authenticated terminal session saving an altered entry at 08:15.",
            EntityIds = ["suspect.jordan", "entity.records", "entity.location"], Time = Exact("08:15")
        };
        var backup = new CaseFact
        {
            Id = "fact.backup", Kind = "identification", Decisive = true,
            Statement = "The independently signed backup attributes the altered entry to jordan's individual signing key.",
            EntityIds = ["suspect.jordan", "entity.records"], Time = Exact("08:15")
        };
        var context = new CaseFact
        {
            Id = "fact.context", Kind = "event", Statement = "An access discrepancy was reported at 08:45.",
            EntityIds = ["entity.records"], Time = Exact("08:45")
        };
        var facts = new List<CaseFact>
        {
            new()
            {
                Id = "fact.crime", Kind = "event", IsPrivate = true,
                Statement = "jordan deliberately altered the records without authorization.",
                EntityIds = ["suspect.jordan", "entity.records", "entity.location"], Time = Exact("08:15")
            },
            record, backup, context,
            Exclusion("riley"), Exclusion("sam")
        };
        var bible = new CaseBible
        {
            CrimeFactId = "fact.crime", Facts = facts,
            Entities =
            [
                new() { Id = "suspect.jordan", Kind = "person", Name = "jordan", KnownFactIds = ["fact.crime", "fact.record"] },
                new() { Id = "suspect.riley", Kind = "person", Name = "riley", KnownFactIds = ["fact.riley_exclusion"] },
                new() { Id = "suspect.sam", Kind = "person", Name = "sam", KnownFactIds = ["fact.sam_exclusion"] },
                new() { Id = "entity.records", Kind = "object", Name = "Access records" },
                new() { Id = "entity.location", Kind = "place", Name = "Test city" },
                new()
                {
                    Id = "entity.investigator", Kind = "person", Name = "Independent records officer",
                    KnownFactIds = ["fact.record", "fact.context", "fact.riley_exclusion", "fact.sam_exclusion"]
                },
                new() { Id = "entity.lab", Kind = "person", Name = "Records analyst", KnownFactIds = ["fact.backup"] }
            ],
            Claims =
            [
                new()
                {
                    Id = "claim.jordan_alibi", SpeakerEntityId = "suspect.jordan", TruthStatus = "deceptive",
                    Statement = "I did not use the records terminal during the 08:00-08:30 window.",
                    RelatedFactIds = ["fact.record"], Time = Window("08:00", "08:30")
                },
                AlibiClaim("riley"), AlibiClaim("sam")
            ],
            Sources =
            [
                Source("asset.scene", "asset", photo ? "photo" : "pdf", "scene", photo ? [] : ["fact.context"]),
                Source("asset.receipt", "asset", "pdf", "receipt", ["fact.riley_exclusion", "fact.sam_exclusion"]),
                Source("asset.access_log", "asset", "pdf", "access_log", ["fact.record"]),
                Source("asset.statement", "asset", "pdf", "statement", ["fact.context"]),
                Source("email.briefing", "briefing", "email", "Case assignment", ["fact.context"]),
                Profile("jordan"), Profile("riley"), Profile("sam"),
                Source("asset.report_access", "result_asset", "pdf", "Access comparison", ["fact.backup"]),
                Source("email.lab_access", "result_email", "email", "Access comparison results", ["fact.backup"])
            ],
            Chronology =
            [
                new() { ReferenceId = "fact.record", SourceId = "asset.access_log", Source = "sensor", Importance = "high" },
                new() { ReferenceId = "fact.context", SourceId = "asset.statement", Source = "investigation" }
            ],
            AnalysisTypes =
            [
                new() { Type = "DigitalForensics", DurationMinutes = 15, AvailableFor = ["pdf", "digital"] },
                new() { Type = "DocumentAuthenticity", DurationMinutes = 30, AvailableFor = ["pdf", "document"] }
            ],
            Outcomes =
            [
                new()
                {
                    InputAssetId = orphanInput ? "asset.unknown" : "asset.access_log", AnalysisType = "DigitalForensics",
                    Findings = true, MatchedSuspectId = "suspect.jordan", MatchBasis = "individualizing", FactIds = ["fact.backup"],
                    ResultAssetId = "asset.report_access", ResultEmailId = "email.lab_access",
                    Limitations = "The signed comparison identifies the signing key; independent terminal records corroborate use."
                }
            ],
            Alibis =
            [
                new() { SuspectId = "suspect.jordan" },
                new() { SuspectId = "suspect.riley", VerifiedFactIds = ["fact.riley_exclusion"], SourceIds = ["asset.receipt"] },
                new() { SuspectId = "suspect.sam", VerifiedFactIds = ["fact.sam_exclusion"], SourceIds = ["asset.receipt"] }
            ],
            PrimaryProofSetId = "proof.primary",
            ProofSets =
            [
                new()
                {
                    Id = "proof.primary", ConclusionFactId = "fact.crime", FactIds = ["fact.record", "fact.backup"],
                    SourceIds = ["asset.access_log", "asset.report_access"], AnalysisIds = ["asset.access_log:DigitalForensics"]
                }
            ],
            Questions =
            [
                new()
                {
                    Id = "q.motive", Topic = "record attribution", Answer = record.Statement,
                    FactIds = ["fact.record"], SourceIds = ["asset.access_log"]
                },
                new()
                {
                    Id = "q.method", Topic = "independent comparison", Answer = backup.Statement,
                    FactIds = ["fact.backup"], SourceIds = ["asset.report_access"]
                }
            ],
            ExplanationFactIds = ["fact.record", "fact.backup"]
        };
        var briefing = bible.Sources.Single(s => s.Id == "email.briefing");
        briefing.From = "Chief <chief@example.invalid>";
        briefing.RecordedAt = "2025-01-01T09:00:00Z";
        briefing.Attachments = ["asset.access_log"];
        foreach (var source in bible.Sources.Where(s => s.Kind is "result_asset" or "result_email"))
        {
            source.AuthorEntityId = "entity.lab";
            source.RecordedAt = rookie ? "2025-01-01T08:55:00Z" : "2025-01-01T09:30:00Z";
            source.Visibility = "hidden";
            source.Discovery = new() { Type = "forensics_complete", InputAssetId = "asset.access_log", AnalysisType = "DigitalForensics" };
        }
        var email = bible.Sources.Single(s => s.Id == "email.lab_access");
        email.From = "Lab <lab@example.invalid>";
        email.Attachments = ["asset.report_access"];
        return bible;
    }

    private static CaseSourcePlan Source(string id, string kind, string type, string title, List<string> facts) => new()
    {
        Id = id, Kind = kind, Type = type, Title = title, Role = "Documented source observation",
        AuthorEntityId = "entity.investigator", RecordedAt = "2025-01-01T08:45:00Z", FactIds = facts
    };

    private static CaseSourcePlan Profile(string name) => new()
    {
        Id = $"suspect.{name}", Kind = "profile", Type = "profile", Title = name,
        Role = "Recorded suspect statements", AuthorEntityId = $"suspect.{name}",
        RecordedAt = "2025-01-01T09:00:00Z", ClaimIds = [$"claim.{name}_alibi"]
    };

    private static CaseFact Exclusion(string name) => new()
    {
        Id = $"fact.{name}_exclusion", Kind = "exclusion",
        Statement = $"Independent desk staff recorded {name} at an off-site appointment throughout 08:00-08:30.",
        EntityIds = [$"suspect.{name}"], Time = Window("08:00", "08:30")
    };

    private static CaseClaim AlibiClaim(string name) => new()
    {
        Id = $"claim.{name}_alibi", SpeakerEntityId = $"suspect.{name}", TruthStatus = "accurate",
        Statement = "I was at my off-site appointment throughout 08:00-08:30.",
        RelatedFactIds = [$"fact.{name}_exclusion"], Time = Window("08:00", "08:30")
    };

    internal static CaseTimeWindow Exact(string time) => new()
    {
        Precision = "exact", Earliest = $"2025-01-01T{time}:00Z", Latest = $"2025-01-01T{time}:00Z"
    };

    internal static CaseTimeWindow Window(string start, string end) => new()
    {
        Precision = "interval", Earliest = $"2025-01-01T{start}:00Z", Latest = $"2025-01-01T{end}:00Z"
    };
}
