using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using CaseGen.Functions.Models.CaseV2;
using static CaseGen.Functions.Services.CaseV2.TaskContractException;

namespace CaseGen.Functions.Services.CaseV2;

public static class CaseBibleValidator
{
    public static void Plan(CaseDraft draft, CaseBible bible)
    {
        StructuredJson.Validate(JsonSerializer.Serialize(bible, CaseDraft.CanonicalJsonOptions),
            StructuredJson.ParseSchema(MicrotaskSchemas.CaseBible));
        MicrotaskValidation.Unique(bible.Entities.Select(e => e.Id), "entities");
        MicrotaskValidation.Unique(bible.Facts.Select(f => f.Id), "facts");
        MicrotaskValidation.Unique(bible.Claims.Select(c => c.Id), "claims");
        MicrotaskValidation.Unique(bible.Sources.Select(s => s.Id), "sources");
        MicrotaskValidation.Unique(bible.AnalysisTypes.Select(t => t.Type), "analysisTypes");
        MicrotaskValidation.Unique(bible.Outcomes.Select(o => $"{o.InputAssetId}:{o.AnalysisType}"), "outcomes");
        MicrotaskValidation.Unique(bible.Alibis.Select(a => a.SuspectId), "alibis");
        MicrotaskValidation.Unique(bible.ProofSets.Select(p => p.Id), "proofSets");
        MicrotaskValidation.Unique(bible.Questions.Select(q => q.Id), "questions");
        var entities = bible.Entities.ToDictionary(e => e.Id, StringComparer.Ordinal);
        var facts = bible.Facts.ToDictionary(f => f.Id, StringComparer.Ordinal);
        var claims = bible.Claims.ToDictionary(c => c.Id, StringComparer.Ordinal);
        var sources = bible.Sources.ToDictionary(s => s.Id, StringComparer.Ordinal);
        var opened = Date(draft.Metadata.OpenedAt);
        foreach (var stub in draft.SuspectStubs)
            Require(entities.TryGetValue(stub.Id, out var entity) && entity.Name == stub.Name && entity.Kind == "person",
                $"Canonical suspect '{stub.Id}' must preserve its name and identity.");
        Require(bible.Alibis.Select(a => a.SuspectId).ToHashSet(StringComparer.Ordinal).SetEquals(draft.SuspectStubs.Select(s => s.Id)),
            "Every suspect requires one alibi plan.");
        Require(facts.TryGetValue(bible.CrimeFactId, out var crime) && crime.IsPrivate && crime.Kind == "event" &&
            crime.EntityIds.Contains(draft.CulpritId) && crime.Time is not null,
            "crimeFactId must identify the private, timed criminal act involving the selected culprit.");
        Require(bible.Entities.Any(e => e.Kind == "place" && e.Name == draft.Metadata.Location && crime!.EntityIds.Contains(e.Id)),
            "The private criminal act must reference the canonical case location.");
        if (draft.Metadata.Victim is { } victim)
            Require(bible.Entities.Any(e => e.Kind == "person" && e.Name == victim.Name && crime!.EntityIds.Contains(e.Id)),
                "The private criminal act must preserve the victim identity.");
        foreach (var entity in bible.Entities) References(entity.KnownFactIds, facts.Keys, $"{entity.Id}.knownFactIds");
        foreach (var fact in bible.Facts)
        {
            References(fact.EntityIds, entities.Keys, $"{fact.Id}.entityIds");
            Time(fact.Time, fact.Id);
            Require(!fact.Decisive || fact.Kind is not ("no_findings" or "similarity"),
                $"'{fact.Id}': no findings or visual similarity cannot be decisive proof.");
        }
        Require(Date(crime!.Time!.Latest) <= opened, "The planned crime cannot occur after case opening.");
        foreach (var claim in bible.Claims)
        {
            Require(entities.ContainsKey(claim.SpeakerEntityId), $"Claim '{claim.Id}' has an unknown speaker.");
            References(claim.RelatedFactIds, facts.Keys, $"{claim.Id}.relatedFactIds");
            Time(claim.Time, claim.Id);
            if (claim.TruthStatus == "accurate" && claim.Time is { } range)
                foreach (var fact in claim.RelatedFactIds.Select(id => facts[id]).Where(f => f.Time is not null))
                    Require(Date(range.Earliest) <= Date(fact.Time!.Latest) && Date(range.Latest) >= Date(fact.Time.Earliest),
                        $"Accurate claim '{claim.Id}' contradicts the canonical time of '{fact.Id}'.");
        }
        Require(sources.TryGetValue("email.briefing", out var briefing) && briefing.Kind == "briefing" &&
            briefing.Visibility == "initial" && briefing.RecordedAt == draft.Metadata.OpenedAt,
            "An initial briefing at case opening is required.");
        Require(bible.Sources.Count(s => s.Kind == "briefing") == 1 && bible.Sources.Count(s => s.Kind == "email") <= 3,
            "Plan one briefing and at most three initial follow-up emails.");
        Require(bible.Sources.Where(s => s.Kind == "profile").Select(s => s.Id).ToHashSet(StringComparer.Ordinal)
            .SetEquals(draft.SuspectStubs.Select(s => s.Id)), "Each suspect requires a planned public profile.");
        Require(bible.Sources.Count(s => s.Kind == "asset") is >= 4 and <= 10, "Plan 4-10 investigation assets.");
        foreach (var source in bible.Sources)
        {
            Require(entities.TryGetValue(source.AuthorEntityId, out var author), $"Source '{source.Id}' has an unknown author.");
            References(source.FactIds, facts.Keys, $"{source.Id}.factIds");
            References(source.ClaimIds, claims.Keys, $"{source.Id}.claimIds");
            Require(source.FactIds.All(id => !facts[id].IsPrivate && author!.KnownFactIds.Contains(id)),
                $"Source '{source.Id}' asserts private facts or exceeds its author's knowledge.");
            Require(source.Kind switch
            {
                "asset" or "result_asset" => source.Type is "photo" or "pdf" or "document" or "digital" && source.Id.StartsWith("asset."),
                "email" or "result_email" or "briefing" => source.Type == "email" && source.Id.StartsWith("email.") && !string.IsNullOrWhiteSpace(source.From),
                "profile" => source.Type == "profile" && source.Id.StartsWith("suspect.") && source.Visibility == "initial",
                _ => false
            }, $"Source '{source.Id}' kind/type/identity is inconsistent.");
            Require((source.Visibility == "hidden") == (source.Discovery is not null),
                $"Source '{source.Id}' needs exactly one discovery condition when hidden.");
            Require(source.Kind is not ("email" or "briefing") || source.Visibility == "initial",
                "Follow-up emails are initial; gated lab emails are result_email.");
            var recorded = Date(source.RecordedAt);
            Require(source.Visibility != "initial" && draft.Metadata.RequiredRank != "Rookie" || recorded <= opened,
                $"Source '{source.Id}' is delivered at opening but was not yet recorded.");
            foreach (var fact in source.FactIds.Select(id => facts[id]).Where(f => f.Time is not null))
                Require(recorded >= Date(fact.Time!.Latest), $"Source '{source.Id}' predates its observation '{fact.Id}'.");
            Require(source.Kind != "profile" || recorded <= opened, "Public profiles cannot be recorded after case opening.");
            References(source.Attachments, sources.Values.Where(s => s.Type is "photo" or "pdf" or "document" or "digital").Select(s => s.Id),
                $"{source.Id}.attachments");
            Require(source.Type == "email" || source.Attachments.Count == 0, "Only email sources may declare attachments.");
        }
        foreach (var outcome in bible.Outcomes)
        {
            Require(sources.TryGetValue(outcome.InputAssetId, out var input) && input.Type is "photo" or "pdf" or "document" or "digital",
                $"Unknown forensic input '{outcome.InputAssetId}'.");
            Require(bible.AnalysisTypes.Any(t => t.Type == outcome.AnalysisType && t.AvailableFor.Contains(input!.Type)),
                $"Incompatible canonical analysis '{outcome.InputAssetId}:{outcome.AnalysisType}'.");
            References(outcome.FactIds, facts.Keys, "outcome.factIds");
            Require(outcome.FactIds.All(id => !facts[id].IsPrivate), "A lab conclusion cannot disclose private generator facts.");
            Require(outcome.MatchedSuspectId is null || outcome.Findings && outcome.MatchBasis == "individualizing" &&
                draft.SuspectStubs.Any(s => s.Id == outcome.MatchedSuspectId) &&
                outcome.FactIds.Any(id => facts[id].Kind == "identification" && facts[id].EntityIds.Contains(outcome.MatchedSuspectId)),
                "A suspect match requires findings and an individualizing identification; similarity is not identification.");
            Require(outcome.MatchBasis switch
            {
                "individualizing" => outcome.FactIds.Any(id => facts[id].Kind == "identification"),
                "similarity" => outcome.FactIds.All(id => !facts[id].Decisive) && outcome.FactIds.Any(id => facts[id].Kind == "similarity"),
                "exclusion" => outcome.FactIds.Any(id => facts[id].Kind == "exclusion"),
                _ => true
            }, "Forensic match basis is unsupported by its canonical facts.");
            if (!outcome.Findings)
                Require(outcome.ResultAssetId is null && outcome.ResultEmailId is null && outcome.MatchedSuspectId is null &&
                    outcome.MatchBasis == "none" && outcome.FactIds.All(id => facts[id].Kind == "no_findings"),
                    "No-findings outcomes cannot identify or exonerate a suspect or fabricate result material.");
            else
            {
                Require(outcome.ResultAssetId is not null && outcome.ResultEmailId is not null &&
                    outcome.ResultAssetId.StartsWith("asset.report_", StringComparison.Ordinal) &&
                    outcome.ResultEmailId.StartsWith("email.lab_", StringComparison.Ordinal) &&
                    sources.TryGetValue(outcome.ResultAssetId, out var resultReport) && resultReport.Kind == "result_asset" &&
                    resultReport.Type is "pdf" or "document" &&
                    sources.TryGetValue(outcome.ResultEmailId, out var email) && email.Kind == "result_email",
                    "Findings require planned report/email sources.");
                Require(sources[outcome.ResultEmailId!].Attachments.Contains(outcome.ResultAssetId!) &&
                    Date(sources[outcome.ResultEmailId!].RecordedAt) >= Date(sources[outcome.ResultAssetId!].RecordedAt),
                    "A result email must attach its existing planned report.");
                foreach (var id in new[] { outcome.ResultAssetId!, outcome.ResultEmailId! })
                {
                    var source = sources[id];
                    Require(outcome.FactIds.All(source.FactIds.Contains), $"Result '{id}' omits its planned forensic facts.");
                    Require(source.Discovery is { Type: "forensics_complete" } trigger &&
                        trigger.InputAssetId == outcome.InputAssetId && trigger.AnalysisType == outcome.AnalysisType,
                        $"Result '{id}' must be discovered by its configured analysis.");
                }
            }
        }
        foreach (var source in bible.Sources.Where(s => s.Kind is "result_asset" or "result_email"))
            Require(bible.Outcomes.Count(o => o.ResultAssetId == source.Id || o.ResultEmailId == source.Id) == 1,
                $"Forensic source '{source.Id}' must have exactly one producing analysis.");
        foreach (var alibi in bible.Alibis)
        {
            References(alibi.VerifiedFactIds, facts.Keys, $"{alibi.SuspectId}.alibi facts");
            References(alibi.SourceIds, sources.Keys, $"{alibi.SuspectId}.alibi sources");
            Require((alibi.VerifiedFactIds.Count == 0) == (alibi.SourceIds.Count == 0), "Alibi verification needs facts and sources together.");
            Require(alibi.VerifiedFactIds.All(id => facts[id].Kind == "exclusion" && !facts[id].IsPrivate &&
                facts[id].EntityIds.Contains(alibi.SuspectId)),
                "An alibi needs positive independent exclusion facts, never no-findings or private knowledge.");
            Coverage(alibi.VerifiedFactIds, alibi.SourceIds, sources, alibi.SuspectId);
            Require(alibi.SourceIds.All(id => sources[id].AuthorEntityId != alibi.SuspectId && sources[id].Type is not ("photo" or "profile")),
                "An alibi needs independently authored readable sources, not the suspect's statement or an image prompt.");
        }
        foreach (var proof in bible.ProofSets)
        {
            Require(proof.ConclusionFactId == bible.CrimeFactId, "Proof sets must support the planned criminal act, not pick another culprit.");
            References(proof.FactIds, facts.Keys, $"{proof.Id}.factIds");
            References(proof.SourceIds, sources.Keys, $"{proof.Id}.sourceIds");
            Require(proof.FactIds.All(id => !facts[id].IsPrivate && facts[id].Kind is not ("no_findings" or "similarity")),
                "Proof sets require discoverable positive facts; absence/similarity do not establish responsibility.");
            Require(proof.FactIds.Any(id => facts[id].EntityIds.Contains(draft.CulpritId) &&
                facts[id].Kind is "event" or "observation" or "motive" or "identification"),
                "A sufficient proof set needs positive supported facts about the selected suspect's responsibility.");
            Require(proof.SourceIds.All(id => sources[id].Type is not ("photo" or "profile")), "Decisive proof cannot rest on image prompts or suspect summaries.");
            Require(proof.SourceIds.Count(id => sources[id].Type is "pdf" or "document" or "digital") is >= 1 and <= 3,
                "The v2 primary evidence set needs 1-3 readable asset sources.");
            Coverage(proof.FactIds, proof.SourceIds, sources, proof.Id);
            References(proof.AnalysisIds, bible.Outcomes.Where(o => o.Findings).Select(o => $"{o.InputAssetId}:{o.AnalysisType}"), "proof.analysisIds");
            foreach (var result in proof.SourceIds.Select(id => sources[id]).Where(s => s.Kind is "result_asset" or "result_email"))
                Require(proof.AnalysisIds.Any(pair => bible.Outcomes.Any(o => $"{o.InputAssetId}:{o.AnalysisType}" == pair &&
                    (o.ResultAssetId == result.Id || o.ResultEmailId == result.Id))), "A required lab source needs its producing analysis.");
        }
        Require(bible.ProofSets.Any(p => p.Id == bible.PrimaryProofSetId), "primaryProofSetId is unknown.");
        foreach (var question in bible.Questions)
        {
            References(question.FactIds, facts.Keys, $"{question.Id}.facts");
            References(question.SourceIds, sources.Keys, $"{question.Id}.sources");
            Require(question.FactIds.All(id => !facts[id].IsPrivate && facts[id].Kind is not ("no_findings" or "similarity")),
                "Question answers cannot depend on private facts or unsupported technical inferences.");
            Require(question.SourceIds.All(id => sources[id].Type is not ("photo" or "profile")),
                "Question answers need readable evidence, not image prompts or suspect summaries.");
            Coverage(question.FactIds, question.SourceIds, sources, question.Id);
        }
        References(bible.ExplanationFactIds, facts.Keys, "explanationFactIds");
        Require(bible.ExplanationFactIds.All(id => !facts[id].IsPrivate && facts[id].Kind is not ("no_findings" or "similarity")),
            "Explanation assertions need positive, player-visible support.");
        Coverage(bible.ExplanationFactIds, sources.Values.Where(s => s.Type is not ("photo" or "profile")).Select(s => s.Id), sources, "explanation");
        Require(bible.ProofSets.Single(p => p.Id == bible.PrimaryProofSetId).FactIds.All(bible.ExplanationFactIds.Contains),
            "The explanation must account for the sufficient primary proof set.");
        foreach (var item in bible.Chronology)
        {
            Require(sources.TryGetValue(item.SourceId, out var source) && source.Visibility == "initial",
                "Public chronology cannot expose a hidden source.");
            var fact = facts.GetValueOrDefault(item.ReferenceId);
            var claim = claims.GetValueOrDefault(item.ReferenceId);
            var time = fact?.Time ?? claim?.Time;
            Require(time is not null && Date(time.Latest) <= opened && (fact is not null
                ? source!.FactIds.Contains(fact.Id) : claim is not null && source!.ClaimIds.Contains(claim.Id)),
                $"Chronology reference '{item.ReferenceId}' lacks an initial, timed supporting source.");
        }
        var projection = CaseBibleAuthorship.Projection(draft, bible);
        MicrotaskValidation.Rules(projection.Rules, new CaseDraft
        {
            AssetFull = projection.AssetFull, ResultAssets = projection.ResultAssets,
            FollowUpEmails = projection.FollowUpEmails, ResultEmails = projection.ResultEmails,
            SuspectStubs = projection.SuspectStubs, AnalysisTypes = projection.AnalysisTypes
        });
        var report = new ConsistencyReport();
        var reachable = CaseProgressionValidator.Reachable(projection, report);
        Require(!report.HasErrors, string.Join("; ", report.Errors));
        bool Accessible(string id) => sources[id].Type switch
        {
            "email" => reachable.Emails.Contains(id), "profile" => true, _ => reachable.Assets.Contains(id)
        };
        var needed = bible.ProofSets.SelectMany(p => p.SourceIds).Concat(bible.Questions.SelectMany(q => q.SourceIds))
            .Concat(bible.Alibis.SelectMany(a => a.SourceIds));
        Require(needed.All(Accessible), "Required proof/question/alibi sources are unreachable or depend on a discovery cycle.");
        foreach (var fact in bible.Facts.Where(f => f.Decisive && !f.IsPrivate).Concat(
                     bible.ExplanationFactIds.Select(id => facts[id])).DistinctBy(f => f.Id))
            Require(bible.Sources.Any(s => s.FactIds.Contains(fact.Id) && s.Type is not ("photo" or "profile") && Accessible(s.Id)),
                $"Decisive fact '{fact.Id}' lacks reachable readable evidence.");
    }

    public static void Authored(CaseDraft draft)
    {
        var bible = draft.Bible!;
        Plan(draft, bible);
        var expectedSources = bible.Sources.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        var actualSources = draft.AssetFull.Concat(draft.ResultAssets).Select(a => a.Id)
            .Concat(draft.FollowUpEmails.Concat(draft.ResultEmails).Select(e => e.Id))
            .Concat(draft.SuspectFull.Select(s => s.Id)).Append("email.briefing").ToHashSet(StringComparer.Ordinal);
        Require(actualSources.SetEquals(expectedSources), "Authored sources differ from the canonical evidence plan.");
        foreach (var asset in draft.AssetFull.Concat(draft.ResultAssets)) CaseBibleAuthorship.Asset(draft, asset);
        foreach (var email in draft.FollowUpEmails.Concat(draft.ResultEmails)) CaseBibleAuthorship.Email(draft, email);
        foreach (var suspect in draft.SuspectFull)
        {
            CaseBibleAuthorship.Source(draft, suspect.Id, suspect.Support, CaseBibleAuthorship.Text(suspect));
            Require(suspect.AlibiVerified == CaseBibleAuthorship.AlibiVerifiedAtOpening(draft, suspect.Id),
                "Public alibi verification differs from its independent source support.");
        }
        var briefing = bible.Sources.Single(s => s.Id == "email.briefing");
        MicrotaskValidation.Equal(draft.Briefing.From, briefing.From!, "briefing.from");
        MicrotaskValidation.Equal(draft.Briefing.Subject, briefing.Title, "briefing.subject");
        CaseBibleAuthorship.Source(draft, "email.briefing", draft.Briefing.Support, draft.Briefing.Body);
        Require(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(draft.Timeline),
            JsonSerializer.SerializeToNode(CaseBibleAuthorship.Timeline(draft))), "Public chronology changed canonical time precision or source statements.");
        Require(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(draft.AnalysisTypes), JsonSerializer.SerializeToNode(bible.AnalysisTypes)),
            "Analysis definitions differ from the approved plan.");
        Require(draft.TemporalEvents.Count == 0, "Canonical source discovery cannot be replaced by unplanned temporal events.");
        var plannedRules = CaseBibleAuthorship.DiscoveryRules(bible);
        foreach (var rule in plannedRules)
            Require(draft.Rules.Count(r => r.RuleId == rule.RuleId) == 1 &&
                JsonNode.DeepEquals(JsonSerializer.SerializeToNode(rule),
                    JsonSerializer.SerializeToNode(draft.Rules.Single(r => r.RuleId == rule.RuleId))),
                $"Discovery rule '{rule.RuleId}' differs from its canonical condition.");
        Require(draft.Rules.Where(r => plannedRules.All(p => p.RuleId != r.RuleId))
            .SelectMany(r => r.Actions).All(a => a.Type == "send_notification"),
            "Narrative rules cannot add unplanned discovery, verification or suspect status changes.");
        var progression = new ConsistencyReport();
        var reachable = CaseProgressionValidator.Reachable(draft, progression);
        Require(!progression.HasErrors, string.Join("; ", progression.Errors));
        var requiredSources = bible.ProofSets.SelectMany(p => p.SourceIds).Concat(bible.Questions.SelectMany(q => q.SourceIds))
            .Concat(bible.Alibis.SelectMany(a => a.SourceIds));
        Require(requiredSources.All(id => id.StartsWith("email.", StringComparison.Ordinal)
            ? reachable.Emails.Contains(id) : id.StartsWith("suspect.", StringComparison.Ordinal) || reachable.Assets.Contains(id)),
            "Authored proof/question/alibi sources are unreachable.");
        Require(draft.ForensicFull.Select(o => $"{o.InputAssetId}:{o.AnalysisType}").ToHashSet(StringComparer.Ordinal)
            .SetEquals(bible.Outcomes.Select(o => $"{o.InputAssetId}:{o.AnalysisType}")), "Forensic pairs differ from the approved plan.");
        foreach (var outcome in draft.ForensicFull)
        {
            var plan = bible.Outcomes.Single(o => o.InputAssetId == outcome.InputAssetId && o.AnalysisType == outcome.AnalysisType);
            Require(outcome.Findings == plan.Findings && outcome.MatchedSuspectId == plan.MatchedSuspectId &&
                outcome.ResultAssetId == plan.ResultAssetId && outcome.ResultEmailId == plan.ResultEmailId,
                "Authored forensic conclusions cannot change findings, identification or output sources.");
            CaseBibleAuthorship.Support(outcome.Support, plan.FactIds, outcome.ConclusionText ?? "", "forensic conclusion");
        }
        var proof = bible.ProofSets.Single(p => p.Id == bible.PrimaryProofSetId);
        Require(draft.RequiredEvidenceIds.ToHashSet(StringComparer.Ordinal).SetEquals(proof.SourceIds.Where(id => id.StartsWith("asset."))) &&
            draft.RequiredAnalysisIds.ToHashSet(StringComparer.Ordinal).SetEquals(proof.AnalysisIds), "Solution requirements differ from the sufficient proof set.");
        Require(draft.Questions.Select(q => q.Id).ToHashSet(StringComparer.Ordinal).SetEquals(bible.Questions.Select(q => q.Id)),
            "Solution questions differ from the canonical plan.");
        foreach (var question in draft.Questions)
        {
            var plan = bible.Questions.Single(q => q.Id == question.Id);
            var correct = question.Options.SingleOrDefault(o => o.Id == question.CorrectOptionId);
            Require(correct is not null && correct.Label == plan.Answer && question.Weight == plan.Weight,
                $"Question '{question.Id}' changed its supported answer or weight.");
            CaseBibleAuthorship.Support(question.Support, plan.FactIds, $"{question.Prompt}\n{correct!.Label}", question.Id);
        }
        CaseBibleAuthorship.Support(draft.ExplanationSupport, bible.ExplanationFactIds, draft.Explanation, "explanation");
    }

    private static void Coverage(IEnumerable<string> factIds, IEnumerable<string> sourceIds,
        Dictionary<string, CaseSourcePlan> sources, string component)
    {
        var provided = sourceIds.SelectMany(id => sources[id].FactIds).ToHashSet(StringComparer.Ordinal);
        Require(factIds.All(provided.Contains), $"'{component}' has assertions without a supporting source.");
    }

    private static void References(IEnumerable<string> ids, IEnumerable<string> known, string component)
    {
        var values = ids.ToList();
        MicrotaskValidation.Unique(values, component);
        var allowed = known.ToHashSet(StringComparer.Ordinal);
        Require(values.All(allowed.Contains), $"'{component}' references an unknown canonical ID.");
    }

    private static void Time(CaseTimeWindow? time, string component)
    {
        if (time is null) return;
        var from = Date(time.Earliest);
        var to = Date(time.Latest);
        Require(from <= to && (time.Precision == "exact" ? from == to : from < to),
            $"'{component}' has an invalid exact/interval/estimate time window.");
    }

    private static DateTimeOffset Date(string value)
    {
        Require(DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date),
            $"Invalid canonical date '{value}'.");
        return date;
    }
}
