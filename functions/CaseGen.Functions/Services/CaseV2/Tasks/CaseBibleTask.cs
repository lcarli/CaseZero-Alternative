using CaseGen.Functions.Models.CaseV2;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.CaseV2.Tasks;

public sealed class CaseBibleTask(ILLMProvider llm, ILogger logger)
{
    public async Task RunAsync(CaseDraft draft, CancellationToken ct)
    {
        var system = """
            You are planning the **canonical CaseBible** for ONE fictional investigation BEFORE any material is authored.
            Preserve the supplied plot, suspect IDs/names and culprit. Plan the criminal act (who/what/where/when)
            as a PRIVATE timed event fact. Other private facts describe generator-only truth, never public profiles.
            Its entityIds include the culprit, a place whose name exactly matches metadata.location,
            and the victim person matching metadata.victim when a victim is supplied.
            Use entity.<slug> for locations, objects, organizations and other people; suspects keep suspect.<slug>.
            Each entity lists knownFactIds. Every public source lists ONLY non-private facts its author knows.
            Distinguish fact.<slug> ground truth from claim.<slug> attributed witness/suspect assertions.
            Claims have a speaker, related canonical facts and accurate/unverified/mistaken/deceptive truthStatus.
            Intentional disagreement must be traceable to its underlying facts, not an accidental contradiction.
            Time windows use exact (equal bounds), interval or estimate (different bounds), always ISO-8601 with timezone.

            Plan 4-10 asset sources of type photo/pdf/document/digital, one initial profile per suspect,
            an initial email.briefing recorded at openedAt, and 0-3 initial follow-up emails.
            Use the supported source kinds/types, authored titles, sender/from, recordedAt and exact attachments.
            Record creation cannot predate an asserted observation. Public chronology uses only initial sources
            and references fact/claim time windows no later than openedAt. Uncertain witness time stays uncertain.
            source.discovery is null when initial; when hidden use the runtime's nested trigger contract.
            Source profiles contain recorded background, attributed motives/alibis, never private guilt/innocence.
            Speech evidence is a document transcript (InterviewTranscript), never an audio/video placeholder.
            All initial sources must be recorded by openedAt. Rookie delivers every source at opening,
            so its lab reports/emails must already exist by openedAt as well.

            The unchanged public v2 contract requires 1-4 compatible available analysisTypes; plan 0-5 outcomes.
            A documentary case can have no required analysis or positive outcome, but still exposes available methods.
            Positive outcomes have a result_asset PDF/document
            and a result_email, both discovered by the exact forensic input/type pair.
            Plan stable asset.report_<slug> and email.lab_<slug> IDs. A result email attaches its planned report.
            List supported factIds and explicit method limitations. A matchedSuspectId is allowed ONLY for
            an individualizing identification fact, not a similarity. A comparison can have findings=true with
            matchedSuspectId=null and still contribute to a sufficient collective proof set.
            No findings produce no result material or suspect match and NEVER independently prove innocence.
            Alibi verification requires positive exclusion facts about that suspect from independent readable sources;
            never an image prompt or the suspect's own statement. Otherwise use empty lists.

            Define 1-3 proof.<slug> sets with at least two complementary facts and source documents/emails.
            conclusionFactId is the private crime fact; factIds are its discoverable positive supporting facts.
            No single mandatory magic forensic match. Primary set has 1-3 readable asset IDs, plus producing
            analysisIds where lab results are required. Every proof/question/alibi source must be reachable;
            reject closed discovery cycles rather than making evidence initial as a shortcut.
            Plan 2-4 questions: ID, topic, weight, the exact supported answer text, factIds and sourceIds.
            ExplanationFactIds cover the primary proof and only assertions backed by readable player evidence.
            Questions use readable source documents/emails, not just profiles or image prompts.
            Image prompts and suspect summaries cannot be the sole decisive proof or a positive identification.
            Additional sources can provide texture; decisive sources must be substantive, not just summaries.
            Emit the complete canonical plan in the requested language, JSON only.
            """;
        var bible = await TaskRunner.RunStructuredAsync<CaseBible>(llm, logger, "CaseBible", system,
            $"PLOT SEED (read-only):\n{draft.ToSummaryJson()}", MicrotaskSchemas.CaseBible, ct,
            validate: b => CaseBibleValidator.Plan(draft, b));
        draft.Bible = bible;
        draft.AssetStubs = bible.Sources.Where(s => s.Kind == "asset").Select(s => new AssetStub
        {
            Id = s.Id, Type = s.Type, Title = s.Title, Role = s.Role, Visibility = s.Visibility
        }).ToList();
        draft.AnalysisTypes = bible.AnalysisTypes;
        draft.ForensicStubs = bible.Outcomes.Select(o => new ForensicOutcomeStub
        {
            InputAssetId = o.InputAssetId, AnalysisType = o.AnalysisType, Findings = o.Findings,
            MatchedSuspectId = o.MatchedSuspectId, Role = o.Limitations
        }).ToList();
        var proof = bible.ProofSets.Single(p => p.Id == bible.PrimaryProofSetId);
        draft.RequiredEvidenceIds = proof.SourceIds.Where(id => id.StartsWith("asset.", StringComparison.Ordinal)).ToList();
        draft.RequiredAnalysisIds = proof.AnalysisIds.ToList();
        draft.QuestionTopics = bible.Questions.Select(q => new QuestionTopic { Id = q.Id, Topic = q.Topic, Weight = q.Weight }).ToList();
    }
}
