using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using CaseGen.Functions.Models.CaseV2;
using static CaseGen.Functions.Services.CaseV2.TaskContractException;

namespace CaseGen.Functions.Services.CaseV2;

public static class CaseBibleAuthorship
{
    public const string Instructions = """
        The supplied source observations are binding and are the author's entire permitted knowledge.
        Do not infer guilt, invent identification/exoneration, or add unseen facts.
        Preserve each observation's full statement verbatim, including attribution, disagreement,
        estimates and uncertainty. Never replace an observed value with a supposed canonical truth.
        Return support[] with referenceId=observation ID and an exact excerpt of the readable content
        for every assigned observation. An image prompt, description or invisible metadata is NOT evidence.
        No findings do not establish innocence; visual, handwriting or linguistic similarity is not identity.
        Echo assigned IDs, source times, sender, title, visibility, layout and attachments exactly.
        """;

    public sealed record EmailPlan(string Id, string From, string Subject, string SentAt,
        IReadOnlyList<string> Attachments, IReadOnlyList<CaseBibleObservation> Observations);

    public static string Context(CaseDraft draft, params string[] componentIds) =>
        ObservationContext(draft, componentIds.SelectMany(id => AssignedObservations(draft, id)));

    public static string ObservationContext(CaseDraft draft, IEnumerable<CaseBibleObservation> assigned)
    {
        var bible = Bible(draft);
        var observations = assigned.DistinctBy(o => o.Id).ToList();
        Require(observations.All(o => o.Visibility == FactVisibility.Public),
            "A source author cannot receive private observations.");
        var sources = observations.Select(o => bible.Sources.Single(s => s.Id == o.SourceId)).DistinctBy(s => s.Id).ToList();
        foreach (var observation in observations)
            Require(KnownAtSourceTime(bible, observation), $"Observation '{observation.Id}' occurs after its source was recorded.");
        object? Identity(string? personId) => personId is null ? null : bible.People.Where(p => p.Id == personId)
            .Select(p => new { p.SuspectId, p.Name }).Single();
        string? PublicObservedValue(string? value)
        {
            if (value is null) return null;
            foreach (var person in bible.People.OrderByDescending(p => p.Id.Length))
                value = value.Replace(person.Id, person.SuspectId ?? person.Name, StringComparison.Ordinal);
            return value;
        }
        return JsonSerializer.Serialize(new
        {
            caseId = draft.CaseId, language = ForensicMethodCatalog.NormalizeLanguage(draft.Request.Language),
            metadata = new { draft.Metadata.Title, Location = bible.World.LocationDisplayName, OpenedAt = bible.Incident.OpenedAt },
            people = bible.People.Select(p => new { p.SuspectId, p.Name, p.Age, p.Occupation, p.PublicRole, p.RelationshipToVictim }),
            sources = sources.Select(s => new { s.Id, s.Name, s.Kind, author = Identity(s.AuthorPersonId),
                custodian = Identity(s.CustodianPersonId),
                institution = s.CustodianInstitutionId is null ? null : bible.Institutions.Single(i => i.Id == s.CustodianInstitutionId).Name,
                s.RecordedAt }),
            observations = observations.Select(o => new
            {
                o.Id, o.SourceId, statement = Statement(draft, o), observedValue = PublicObservedValue(o.ObservedValue), o.ObservedAt,
                speaker = Identity(o.SpeakerPersonId)
            })
        }, CaseDraft.CanonicalJsonOptions);
    }

    public static string ProfileContext(CaseDraft draft, string suspectId)
    {
        var person = CaseBibleNormalizer.FindSuspect(draft, suspectId)
            ?? throw new TaskContractException($"Unknown canonical suspect '{suspectId}'.");
        var observations = AssignedObservations(draft, suspectId);
        Require(string.IsNullOrWhiteSpace(person.StatedAlibi) || observations.Any(o =>
            o.Statement.Contains(person.StatedAlibi, StringComparison.Ordinal)),
            "A profile cannot receive an alibi claim not recorded in its public opening-time testimony.");
        return JsonSerializer.Serialize(new
        {
            profile = new { id = suspectId, person.Name, person.Age, person.Occupation,
                relationship = person.RelationshipToVictim, person.PublicRole, person.StatedAlibi },
            sourceContext = JsonSerializer.Deserialize<JsonElement>(ObservationContext(draft, observations))
        }, CaseDraft.CanonicalJsonOptions);
    }

    public static IReadOnlyList<CaseBibleObservation> AssignedObservations(CaseDraft draft, string componentId)
    {
        var bible = Bible(draft);
        if (componentId == "email.briefing" || componentId == "briefing")
        {
            var opening = bible.Observations.Where(o => bible.InvestigationConstraints.OpeningObservationIds.Contains(o.Id, StringComparer.Ordinal)).ToList();
            Require(opening.All(o => KnownAtOpening(bible, o)), "Briefing cannot receive source observations recorded after case opening.");
            return opening;
        }
        if (componentId.StartsWith("suspect.", StringComparison.Ordinal))
        {
            var person = CaseBibleNormalizer.FindSuspect(draft, componentId)
                ?? throw new TaskContractException($"Unknown canonical suspect '{componentId}'.");
            return bible.Observations.Where(o => o.Visibility == FactVisibility.Public
                && (o.SpeakerPersonId == person.Id || bible.Sources.Any(s => s.Id == o.SourceId
                    && s.Kind == CaseBibleSourceKind.Testimony && (s.AuthorPersonId ?? s.CustodianPersonId) == person.Id))
                && (o.ObservedAt is null || AtOrBefore(o.ObservedAt, bible.Incident.OpenedAt))
                && bible.Sources.Any(s => s.Id == o.SourceId && AtOrBefore(s.RecordedAt, bible.Incident.OpenedAt))).ToList();
        }
        var email = InitialEmailPlans(draft).SingleOrDefault(p => p.Id == componentId);
        if (email is not null) return email.Observations;
        var result = draft.ForensicStubs.SingleOrDefault(s =>
            ResultIds(s).AssetId == componentId || ResultIds(s).EmailId == componentId);
        if (result is not null) return OutcomeObservations(draft, result);
        var graphObservations = draft.CaseGraph.Observations.Where(o => o.SourceAssetId == componentId).ToList();
        var statements = draft.AssetStubs.SingleOrDefault(s => s.Id == componentId && s.LayoutHint == "InterviewTranscript")
            ?.SubjectSuspectId is { } subject ? AssignedObservations(draft, subject) : Array.Empty<CaseBibleObservation>();
        var assigned = graphObservations.Select(o => ResolveObservation(draft, o)).Concat(statements).DistinctBy(o => o.Id).ToList();
        if (draft.AssetStubs.Any(s => s.Id == componentId && s.Visibility == "initial"))
            Require(assigned.All(o => KnownAtOpening(bible, o)),
                $"Initial material '{componentId}' cannot receive observations recorded after case opening.");
        return assigned;
    }

    public static CaseBibleObservation ResolveObservation(CaseDraft draft, EvidenceObservation observation)
    {
        var candidates = Bible(draft).Observations.Where(o => o.FactId == observation.FactId
            && (observation.EvidentiaryOriginId is null || o.SourceId == observation.EvidentiaryOriginId)).ToList();
        var exact = candidates.Where(o => observation.Id == o.Id
            || observation.Id == $"{o.Id}_{Suffix(observation.SourceAssetId)}"
            || observation.Id.Contains($"_{Suffix(o.Id)}_", StringComparison.Ordinal)).ToList();
        if (exact.Count == 1) return exact[0];
        Require(candidates.Count == 1, $"Graph observation '{observation.Id}' must identify one explicit canonical source observation.");
        return candidates[0];
    }

    public static IReadOnlyList<CaseBibleObservation> OutcomeObservations(CaseDraft draft, ForensicOutcomeStub stub)
    {
        var bible = Bible(draft);
        var ids = bible.DifficultyIntent.Clues.Where(c => stub.SupportsClueIds.Contains(c.Id, StringComparer.Ordinal))
            .SelectMany(c => c.ObservationIds).ToHashSet(StringComparer.Ordinal);
        return bible.Observations.Where(o => ids.Contains(o.Id)
            && bible.Sources.Any(s => s.Id == o.SourceId && s.Kind == CaseBibleSourceKind.ForensicResult)).ToList();
    }

    public static (string AssetId, string EmailId) ResultIds(ForensicOutcomeStub stub)
    {
        var suffix = $"{Suffix(stub.InputAssetId)}_{stub.AnalysisType.ToLowerInvariant()}";
        return ($"asset.report_{suffix}", $"email.lab_{suffix}");
    }

    public static IReadOnlyList<EmailPlan> InitialEmailPlans(CaseDraft draft)
    {
        var bible = Bible(draft);
        return bible.Sources.Where(s => s.Kind == CaseBibleSourceKind.Testimony
                && AtOrBefore(s.RecordedAt, bible.Incident.OpenedAt))
            .OrderBy(s => s.Id, StringComparer.Ordinal)
            .Select(s => new { Source = s, Person = bible.People.FirstOrDefault(p => p.Id == (s.AuthorPersonId ?? s.CustodianPersonId)),
                Observations = bible.Observations.Where(o => o.SourceId == s.Id && o.Visibility == FactVisibility.Public
                    && AtOrBefore(o.ObservedAt, s.RecordedAt)).ToList() })
            .Where(item => item.Person?.Email is not null && item.Observations.Count > 0)
            .Take(3).Select(item => new EmailPlan($"email.followup_{Suffix(item.Source.Id)}",
                item.Person!.Email!, item.Source.Name, item.Source.RecordedAt!, [],
                item.Observations)).ToList();
    }

    public static IReadOnlyList<string> BriefingAttachmentIds(CaseDraft draft)
    {
        var opening = Bible(draft).InvestigationConstraints.OpeningObservationIds.ToHashSet(StringComparer.Ordinal);
        return draft.AssetStubs.Where(a => a.Visibility == "initial"
                && AssignedObservations(draft, a.Id).Any(o => opening.Contains(o.Id)))
            .Select(a => a.Id).Distinct(StringComparer.Ordinal).ToList();
    }

    public static void Source(CaseDraft draft, string componentId, IEnumerable<CaseContentSupport> support, string content) =>
        ObservationSupport(draft, AssignedObservations(draft, componentId), support, content, componentId);

    public static void ObservationSupport(CaseDraft draft, IEnumerable<CaseBibleObservation> assigned,
        IEnumerable<CaseContentSupport> support, string content, string componentId)
    {
        var observations = assigned.DistinctBy(o => o.Id).ToList();
        var entries = support.ToList();
        Support(entries, observations.Select(o => o.Id), content, componentId);
        foreach (var observation in observations)
        {
            var excerpt = entries.Single(e => e.ReferenceId == observation.Id).Excerpt;
            Require(Normalize(excerpt).Contains(Normalize(Statement(draft, observation)), StringComparison.Ordinal),
                $"{componentId} must preserve the complete attributed observation '{observation.Id}', including uncertainty.");
        }
    }

    public static void Support(IEnumerable<CaseContentSupport> support, IEnumerable<string> expected, string content, string component)
    {
        var entries = support.ToList();
        MicrotaskValidation.Unique(entries.Select(s => s.ReferenceId), $"{component}.support");
        Require(entries.Select(s => s.ReferenceId).ToHashSet(StringComparer.Ordinal).SetEquals(expected),
            $"{component} support must cover exactly its assigned observations.");
        foreach (var entry in entries)
            Require(!string.IsNullOrWhiteSpace(entry.Excerpt)
                && Normalize(content).Contains(Normalize(entry.Excerpt), StringComparison.Ordinal),
                $"{component} support excerpt for '{entry.ReferenceId}' is absent from actual readable content.");
    }

    public static string Text(EvidenceAsset asset)
    {
        if (asset.Type == "photo" || asset.BodyDoc is not { } document) return string.Empty;
        var parts = new List<string> { document.Title, document.Subtitle ?? "" };
        parts.AddRange(document.Header.Select(h => $"{h.Label} {h.Value}"));
        foreach (var section in document.Sections)
        {
            parts.Add(section.Heading ?? "");
            if (section.Text is not null) parts.Add(section.Text);
            if (section.Table is { } table)
            {
                parts.AddRange(table.Columns);
                parts.AddRange(table.Rows.Select(r => string.Join(" ", r)));
                parts.Add(table.Footnote ?? "");
            }
            if (section.Items is { } items) parts.AddRange(items.Select(i => $"{i.Label} {i.Value}"));
            if (section.Transcript is { } transcript) parts.AddRange(transcript.Select(t => $"{t.Timestamp} {t.Speaker} {t.Text}"));
            if (section.Code is { } code) parts.Add(code.Content);
        }
        if (document.Signature is { } signature)
            parts.Add($"{signature.Name} {signature.Title} {signature.Badge} {signature.SignedAt}");
        return string.Join("\n", parts);
    }

    public static string Text(PlotSuspect suspect) =>
        string.Join("\n", suspect.Occupation, suspect.Relationship, suspect.Motive, suspect.Alibi, suspect.Background);

    public static void Asset(CaseDraft draft, EvidenceAsset asset)
    {
        var stub = draft.AssetStubs.SingleOrDefault(s => s.Id == asset.Id);
        if (stub is not null)
        {
            MicrotaskValidation.Equal(asset.Title, stub.Title, "asset.title");
            MicrotaskValidation.Equal(asset.Type, stub.Type, "asset.type");
            MicrotaskValidation.Equal(asset.Visibility, stub.Visibility, "asset.visibility");
            Require(asset.EvidenceRole == stub.EvidenceRole && asset.SubjectSuspectId == stub.SubjectSuspectId
                && asset.ImagePurpose == stub.ImagePurpose, "Asset must preserve its portfolio assignment.");
        }
        MicrotaskValidation.Asset(asset);
        if (asset.Type == "photo")
            Require(asset.Support.Count == 0, "An image prompt cannot provide readable observation support.");
        else
        {
            Require(asset.BodyDoc is not null, $"Readable asset '{asset.Id}' requires bodyDoc; descriptions are not evidence.");
            if (stub is not null) MicrotaskValidation.Equal(asset.BodyDoc!.Layout, stub.LayoutHint, "asset.bodyDoc.layout");
            MicrotaskValidation.Equal(asset.BodyDoc!.Language, ForensicMethodCatalog.NormalizeLanguage(draft.Request.Language), "document.language");
            Source(draft, asset.Id, asset.Support, Text(asset));
        }
        if (asset.BodyDoc?.Signature is { } signature)
        {
            var sources = AssignedObservations(draft, asset.Id).Select(o => Bible(draft).Sources.Single(s => s.Id == o.SourceId)).DistinctBy(s => s.Id).ToList();
            if (signature.SignedAt is not null)
                Require(sources.Count > 0 && sources.All(s => s.RecordedAt == signature.SignedAt), "Document signing time is not in its source plan.");
            if (sources.Count == 1 && sources[0].AuthorPersonId is { } personId)
                MicrotaskValidation.Equal(signature.Name, Bible(draft).People.Single(p => p.Id == personId).Name, "document.signature.name");
        }
        asset.CanonicalContent = true;
    }

    public static void Suspect(CaseDraft draft, PlotSuspect suspect)
    {
        var person = CaseBibleNormalizer.FindSuspect(draft, suspect.Id)
            ?? throw new TaskContractException($"Unknown suspect '{suspect.Id}'.");
        MicrotaskValidation.Equal(suspect.Name, person.Name, "suspect.name");
        Require(suspect.Age == person.Age && suspect.Occupation == person.Occupation
            && suspect.Relationship == person.RelationshipToVictim, "Suspect profile changed canonical public identity.");
        Require(suspect.Alibi == person.StatedAlibi, "A suspect's stated alibi cannot be replaced with private truth.");
        Require(!suspect.AlibiVerified || AlibiVerifiedAtOpening(draft, suspect.Id), "An alibi cannot be verified without reachable independent readable support.");
        Source(draft, suspect.Id, suspect.Support, Text(suspect));
    }

    public static void Email(CaseDraft draft, EvidenceEmail email)
    {
        MicrotaskValidation.Email(email, draft);
        if (InitialEmailPlans(draft).SingleOrDefault(p => p.Id == email.Id) is { } plan)
        {
            Require(email.From == plan.From && email.Subject == plan.Subject && email.SentAt == plan.SentAt
                && email.Visibility == "initial" && email.Attachments.SequenceEqual(plan.Attachments),
                "Email must preserve its planned public sender, issue time, title, visibility and attachments.");
        }
        Source(draft, email.Id, email.Support, email.Body);
    }

    public static string Materials(CaseDraft draft, IEnumerable<string> componentIds)
    {
        var selected = componentIds.Distinct(StringComparer.Ordinal).Select(id => ReadableMaterials(draft)
            .SingleOrDefault(m => m.Id == id) ?? throw new TaskContractException($"Readable material '{id}' is missing or unreachable."));
        return JsonSerializer.Serialize(selected, CaseDraft.CanonicalJsonOptions);
    }

    public sealed record ReadableMaterial(string Id, string Title, string Text, IReadOnlyList<CaseContentSupport> Support);

    public static IReadOnlyList<ReadableMaterial> ReadableMaterials(CaseDraft draft)
    {
        var reachable = CaseProgressionValidator.Reachable(draft, new ConsistencyReport());
        return draft.AssetFull.Concat(draft.ResultAssets).Where(a => reachable.Assets.Contains(a.Id) && a.BodyDoc is not null && a.Type != "photo")
            .Select(a => new ReadableMaterial(a.Id, a.Title, Text(a), a.Support))
            .Concat(draft.FollowUpEmails.Concat(draft.ResultEmails).Where(e => reachable.Emails.Contains(e.Id))
                .Select(e => new ReadableMaterial(e.Id, e.Subject, e.Body, e.Support)))
            .Append(new ReadableMaterial("email.briefing", draft.Briefing.Subject, draft.Briefing.Body, draft.Briefing.Support)).ToList();
    }

    public static void SupportFromMaterials(CaseDraft draft, IEnumerable<CaseContentSupport> support,
        IEnumerable<string> materialIds, string component, bool required = true)
    {
        var entries = support.ToList();
        var ids = materialIds.ToHashSet(StringComparer.Ordinal);
        var materials = ReadableMaterials(draft).Where(m => ids.Contains(m.Id)).ToList();
        Require(!required || entries.Count > 0, $"{component} requires readable source support.");
        MicrotaskValidation.Unique(entries.Select(s => s.ReferenceId), $"{component}.support");
        foreach (var entry in entries)
        {
            var observation = Bible(draft).Observations.SingleOrDefault(o => o.Id == entry.ReferenceId);
            Require(observation is not null && !string.IsNullOrWhiteSpace(entry.Excerpt)
                && Normalize(entry.Excerpt).Contains(Normalize(Statement(draft, observation)), StringComparison.Ordinal)
                && materials.Any(m => m.Support.Any(s => s.ReferenceId == entry.ReferenceId
                    && Normalize(s.Excerpt).Contains(Normalize(entry.Excerpt), StringComparison.Ordinal))
                    && Normalize(m.Text).Contains(Normalize(entry.Excerpt), StringComparison.Ordinal)),
                $"{component} support '{entry.ReferenceId}' must quote the complete attributed observation from its reachable material's supported excerpt.");
        }
    }

    public static bool AlibiVerifiedAtOpening(CaseDraft draft, string suspectId)
    {
        var bible = Bible(draft);
        var person = CaseBibleNormalizer.FindSuspect(draft, suspectId);
        var arc = bible.DifficultyIntent.DecoyArcs.SingleOrDefault(d => d.SuspectPersonId == person?.Id);
        if (arc is null || arc.VerificationObservationIds.Count == 0) return false;
        return arc.VerificationObservationIds.All(id => bible.Observations.Any(o => o.Id == id
            && o.Reliability is ObservationReliability.Verified or ObservationReliability.Corroborated
            && o.SpeakerPersonId != person!.Id
            && AtOrBefore(o.ObservedAt, bible.Incident.OpenedAt)
            && bible.Sources.Any(s => s.Id == o.SourceId && s.Kind != CaseBibleSourceKind.ForensicResult
                && (s.AuthorPersonId ?? s.CustodianPersonId) != person!.Id && AtOrBefore(s.RecordedAt, bible.Incident.OpenedAt)))
            && draft.AssetFull.Any(a => a.Visibility == "initial" && a.BodyDoc is not null
                && a.Support.Any(s => s.ReferenceId == id && Normalize(Text(a)).Contains(Normalize(s.Excerpt), StringComparison.Ordinal))));
    }

    public static string Statement(CaseDraft draft, CaseBibleObservation observation) =>
        observation.SpeakerPersonId is { } speaker
            ? $"{Bible(draft).People.Single(p => p.Id == speaker).Name}: {observation.Statement}"
            : observation.Statement;

    private static CaseBible Bible(CaseDraft draft) =>
        draft.CaseBible ?? throw new TaskContractException("A validated canonical CaseBible is required for authorship.");
    private static string Suffix(string id) => id[(id.IndexOf('.') + 1)..];
    private static bool AtOrBefore(string? time, string? cutoff) =>
        DateTimeOffset.TryParse(time, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value)
        && DateTimeOffset.TryParse(cutoff, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var limit) && value <= limit;
    private static bool KnownAtSourceTime(CaseBible bible, CaseBibleObservation observation) =>
        observation.ObservedAt is null || AtOrBefore(observation.ObservedAt, bible.Sources.Single(s => s.Id == observation.SourceId).RecordedAt);
    private static bool KnownAtOpening(CaseBible bible, CaseBibleObservation observation) =>
        KnownAtSourceTime(bible, observation)
        && AtOrBefore(bible.Sources.Single(s => s.Id == observation.SourceId).RecordedAt, bible.Incident.OpenedAt);
    private static string Normalize(string text) => Regex.Replace(text, @"\s+", " ").Trim();
}
