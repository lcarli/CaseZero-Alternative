using System.Text.Json;
using System.Text.RegularExpressions;
using CaseGen.Functions.Models.CaseV2;
using static CaseGen.Functions.Services.CaseV2.TaskContractException;

namespace CaseGen.Functions.Services.CaseV2;

public static class CaseBibleAuthorship
{
    public const string Instructions = """
        The supplied source plan is binding. Write only what its author can know.
        Facts are observations, not permission to announce guilt. Claims are attributed statements,
        not verified truth; retain their speaker, uncertainty and intentional disagreement.
        Do not invent decisive facts, exact times, identification or exoneration.
        No findings do not prove innocence; visual similarity is not positive identification.
        Return support[] with referenceId and an exact, nonblank excerpt from the authored content
        for EVERY permitted fact and claim. Do not cite a description or an image prompt as a lab finding.
        Preserve planned IDs, title, sender, visibility, issue time and attachments.
        The reviewer compares each excerpt against its canonical fact/claim and the full material.
        """;

    public static string Context(CaseDraft draft, params string[] sourceIds)
    {
        var bible = draft.Bible ?? throw new TaskContractException("A canonical source plan is required.");
        var sources = sourceIds.Select(id => bible.Sources.Single(s => s.Id == id)).ToList();
        var facts = sources.SelectMany(s => s.FactIds).ToHashSet(StringComparer.Ordinal);
        var claims = sources.SelectMany(s => s.ClaimIds).ToHashSet(StringComparer.Ordinal);
        var entities = bible.Facts.Where(f => facts.Contains(f.Id)).SelectMany(f => f.EntityIds)
            .Concat(bible.Claims.Where(c => claims.Contains(c.Id)).Select(c => c.SpeakerEntityId))
            .Concat(sources.Select(s => s.AuthorEntityId)).Concat(draft.SuspectStubs.Select(s => s.Id)).ToHashSet(StringComparer.Ordinal);
        return JsonSerializer.Serialize(new
        {
            caseId = draft.CaseId, language = draft.Request.Language ?? "en-US",
            metadata = new { draft.Metadata.Title, draft.Metadata.Location, draft.Metadata.IncidentDate, draft.Metadata.OpenedAt, draft.Metadata.Victim },
            entities = bible.Entities.Where(e => entities.Contains(e.Id)).Select(e => new { e.Id, e.Kind, e.Name }),
            sources = sources.Select(s => new
            {
                s.Id, s.Kind, s.Type, s.Title, s.AuthorEntityId, s.From, s.RecordedAt,
                s.Visibility, s.FactIds, s.ClaimIds, s.Attachments
            }),
            facts = bible.Facts.Where(f => facts.Contains(f.Id)).Select(f => new { f.Id, f.Statement, f.Kind, f.EntityIds, f.Time }),
            claims = bible.Claims.Where(c => claims.Contains(c.Id)).Select(c => new { c.Id, c.Statement, c.SpeakerEntityId, c.Time })
        }, CaseDraft.CanonicalJsonOptions);
    }

    public static void Source(CaseDraft draft, string sourceId, IEnumerable<CaseContentSupport> support, string content)
    {
        var plan = draft.Bible!.Sources.Single(s => s.Id == sourceId);
        Support(support, plan.FactIds.Concat(plan.ClaimIds), content, sourceId);
    }

    public static string Materials(CaseDraft draft, IEnumerable<string> sourceIds)
    {
        var components = draft.GetComponents();
        var selected = sourceIds.Distinct(StringComparer.Ordinal).ToDictionary(id => id,
            id => components[id == "email.briefing" ? "briefing" : id]
                ?? throw new TaskContractException($"Authored source '{id}' is missing."), StringComparer.Ordinal);
        return JsonSerializer.Serialize(selected, CaseDraft.CanonicalJsonOptions);
    }

    public static void Support(IEnumerable<CaseContentSupport> support, IEnumerable<string> expected, string content, string component)
    {
        var entries = support.ToList();
        MicrotaskValidation.Unique(entries.Select(s => s.ReferenceId), $"{component}.support");
        Require(entries.Select(s => s.ReferenceId).ToHashSet(StringComparer.Ordinal).SetEquals(expected),
            $"{component} support must cover exactly its planned facts/claims.");
        var text = Normalize(content);
        foreach (var entry in entries)
            Require(!string.IsNullOrWhiteSpace(entry.Excerpt) && text.Contains(Normalize(entry.Excerpt), StringComparison.Ordinal),
                $"{component} support excerpt for '{entry.ReferenceId}' is not present in the actual content.");
    }

    public static string Text(EvidenceAsset asset)
    {
        if (asset.BodyDoc is not { } document) return asset.Body ?? "";
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
        return string.Join("\n", parts);
    }

    public static string Text(PlotSuspect suspect) =>
        string.Join("\n", suspect.Occupation, suspect.Relationship, suspect.Motive, suspect.Alibi, suspect.Background);

    public static void Asset(CaseDraft draft, EvidenceAsset asset)
    {
        if (draft.Bible is null) return;
        var plan = draft.Bible.Sources.Single(s => s.Id == asset.Id);
        MicrotaskValidation.Equal(asset.Title, plan.Title, "asset.title");
        MicrotaskValidation.Equal(asset.Type, plan.Type, "asset.type");
        var visibility = IsAllInitial(draft) ? "initial" : plan.Visibility;
        Require(asset.Visibility == plan.Visibility || asset.Visibility == visibility, "Asset visibility differs from the canonical plan.");
        Source(draft, asset.Id, asset.Support, Text(asset));
        if (asset.BodyDoc?.Signature?.SignedAt is { } signedAt)
            MicrotaskValidation.Equal(signedAt, plan.RecordedAt, "document.signature.signedAt");
        if (asset.BodyDoc?.Signature is { } signature)
            MicrotaskValidation.Equal(signature.Name, draft.Bible.Entities.Single(e => e.Id == plan.AuthorEntityId).Name,
                "document.signature.name");
        asset.CanonicalContent = true;
    }

    public static void Email(CaseDraft draft, EvidenceEmail email)
    {
        if (draft.Bible is null) return;
        var plan = draft.Bible.Sources.Single(s => s.Id == email.Id);
        MicrotaskValidation.Equal(email.From, plan.From!, "email.from");
        MicrotaskValidation.Equal(email.Subject, plan.Title, "email.subject");
        MicrotaskValidation.Equal(email.SentAt, plan.RecordedAt, "email.sentAt");
        Require(email.Visibility == plan.Visibility || IsAllInitial(draft) && email.Visibility == "initial",
            "Email visibility differs from the canonical plan.");
        Require(email.Attachments.ToHashSet(StringComparer.Ordinal).SetEquals(plan.Attachments),
            "Email attachments differ from the canonical plan.");
        Source(draft, email.Id, email.Support, email.Body);
    }

    public static bool AlibiVerifiedAtOpening(CaseDraft draft, string suspectId)
    {
        var bible = draft.Bible!;
        var alibi = bible.Alibis.Single(a => a.SuspectId == suspectId);
        return alibi.VerifiedFactIds.Count > 0 && alibi.SourceIds.All(id =>
        {
            var source = bible.Sources.Single(s => s.Id == id);
            return source.Visibility == "initial" && DateTimeOffset.Parse(source.RecordedAt) <= DateTimeOffset.Parse(draft.Metadata.OpenedAt);
        });
    }

    public static List<EvidenceTimelineEntry> Timeline(CaseDraft draft)
    {
        var bible = draft.Bible!;
        return bible.Chronology.Select(item =>
        {
            var fact = bible.Facts.SingleOrDefault(f => f.Id == item.ReferenceId);
            var claim = bible.Claims.SingleOrDefault(c => c.Id == item.ReferenceId);
            var time = fact?.Time ?? claim!.Time!;
            var text = fact?.Statement ?? claim!.Statement;
            if (claim is not null)
                text = $"{bible.Entities.Single(e => e.Id == claim.SpeakerEntityId).Name}: {text}";
            if (time.Precision != "exact")
            {
                var label = (draft.Request.Language ?? "en-US") switch
                {
                    "pt-BR" => time.Precision == "estimate" ? "Intervalo estimado" : "Intervalo",
                    "es-ES" => time.Precision == "estimate" ? "Intervalo estimado" : "Intervalo",
                    "fr-FR" => time.Precision == "estimate" ? "Plage estim\u00e9e" : "Intervalle",
                    _ => time.Precision == "estimate" ? "Estimated window" : "Time window"
                };
                text = $"{text} ({label}: {time.Earliest} - {time.Latest})";
            }
            return new EvidenceTimelineEntry
            {
                Time = time.Earliest, Event = text, Source = item.Source,
                Verified = fact is not null && time.Precision != "estimate", Importance = item.Importance
            };
        }).ToList();
    }

    public static List<RulesRule> DiscoveryRules(CaseBible bible) => bible.Sources
        .Where(s => s.Visibility == "hidden").Select(s => new RulesRule
        {
            RuleId = $"rule.discover_{s.Id.Replace('.', '_')}", Description = s.Title,
            Trigger = JsonSerializer.Deserialize<RulesTrigger>(JsonSerializer.Serialize(s.Discovery, CaseDraft.CanonicalJsonOptions),
                CaseDraft.CanonicalJsonOptions) ?? throw new TaskContractException("A hidden source has no discovery trigger."),
            Actions = new() { new()
            {
                Type = s.Type == "email" ? "reveal_email" : "reveal_asset",
                AssetId = s.Type == "email" ? null : s.Id, EmailId = s.Type == "email" ? s.Id : null
            }}
        }).ToList();

    public static CaseDraft Projection(CaseDraft draft, CaseBible bible) => new()
    {
        Metadata = draft.Metadata, Bible = bible,
        SuspectStubs = draft.SuspectStubs,
        SuspectFull = draft.SuspectStubs.Select(s => new PlotSuspect { Id = s.Id, Name = s.Name }).ToList(),
        AssetFull = bible.Sources.Where(s => s.Kind == "asset").Select(ToAsset).ToList(),
        ResultAssets = bible.Sources.Where(s => s.Kind == "result_asset").Select(ToAsset).ToList(),
        FollowUpEmails = bible.Sources.Where(s => s.Kind == "email").Select(ToEmail).ToList(),
        ResultEmails = bible.Sources.Where(s => s.Kind == "result_email").Select(ToEmail).ToList(),
        ForensicFull = bible.Outcomes.Select(o => new ForensicsOutcome
        {
            InputAssetId = o.InputAssetId, AnalysisType = o.AnalysisType, Findings = o.Findings,
            MatchedSuspectId = o.MatchedSuspectId, ResultAssetId = o.ResultAssetId, ResultEmailId = o.ResultEmailId
        }).ToList(),
        AnalysisTypes = bible.AnalysisTypes,
        Rules = DiscoveryRules(bible)
    };

    private static EvidenceAsset ToAsset(CaseSourcePlan s) => new() { Id = s.Id, Type = s.Type, Title = s.Title, Visibility = s.Visibility };
    private static EvidenceEmail ToEmail(CaseSourcePlan s) => new() { Id = s.Id, Visibility = s.Visibility, Attachments = s.Attachments };
    private static bool IsAllInitial(CaseDraft d) => d.Metadata.RequiredRank == "Rookie";
    private static string Normalize(string text) => Regex.Replace(text, @"\s+", " ").Trim();
}
