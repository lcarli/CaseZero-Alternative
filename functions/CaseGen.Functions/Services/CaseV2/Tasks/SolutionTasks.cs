using System.Text.Json.Serialization;
using CaseGen.Functions.Models.CaseV2;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.CaseV2.Tasks;

/// <summary>Stage 11 — Solution Skeleton. Tiny output: required ids + list of question topics.</summary>
public class SolutionSkeletonTask
{
    private readonly ILLMProvider _llm;
    private readonly ILogger _logger;
    public SolutionSkeletonTask(ILLMProvider llm, ILogger logger) { _llm = llm; _logger = logger; }

    private class Output
    {
        [JsonPropertyName("requiredEvidenceIds")] public List<string> RequiredEvidenceIds { get; set; } = new();
        [JsonPropertyName("requiredAnalysisIds")] public List<string> RequiredAnalysisIds { get; set; } = new();
        [JsonPropertyName("questionTopics")] public List<QuestionTopic> QuestionTopics { get; set; } = new();
    }

    private static readonly string Schema = MicrotaskSchemas.SolutionSkeleton;

    public async Task RunAsync(CaseDraft draft, CancellationToken ct)
    {
        // Compose a richer context: tell the model which assets are already accessible
        // (initial OR revealed by a pre-built rule). Prefer those in requiredEvidenceIds
        // so the player always has a path to the solution.
        var revealedByRules = draft.Rules
            .SelectMany(r => r.Actions)
            .Where(a => a.Type == "reveal_asset" && !string.IsNullOrEmpty(a.AssetId))
            .Select(a => a.AssetId!)
            .ToHashSet(StringComparer.Ordinal);
        var initialIds = draft.AssetFull
            .Where(a => string.Equals(a.Visibility, "initial", StringComparison.OrdinalIgnoreCase))
            .Select(a => a.Id)
            .Concat(draft.ResultAssets
                .Where(a => string.Equals(a.Visibility, "initial", StringComparison.OrdinalIgnoreCase))
                .Select(a => a.Id))
            .ToHashSet(StringComparer.Ordinal);
        var reachableAssets = initialIds.Union(revealedByRules).ToHashSet(StringComparer.Ordinal);

        var ctxJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            language = draft.Request.Language ?? "en-US",
            culpritId = draft.CulpritId,
            assets = draft.AssetStubs
                .Concat(draft.ResultAssets.Select(a => new AssetStub { Id = a.Id, Type = a.Type, Title = a.Title, Role = "lab result" }))
                .Select(a => new { a.Id, a.Type, a.Title, role = a.Role, reachable = reachableAssets.Contains(a.Id) }),
            forensics = draft.ForensicFull.Where(f => f.Findings).Select(f => new { id = $"{f.InputAssetId}:{f.AnalysisType}", f.MatchedSuspectId }),
            canonical = draft.GetComponents()
        });

        var system = @"You are writing the **skeleton** of the case solution.

CRITICAL CONSTRAINT: every id you put in `requiredEvidenceIds` MUST be marked `reachable: true` in the supplied context.
`reachable` means the asset is either visibility=initial or already gets revealed by a pre-built rule. Picking an
unreachable asset would make the case unsolvable — DO NOT do it.

Pick:
- `requiredEvidenceIds` (1-3 reachable assets that are the strongest probative items, including result PDFs when reachable);
- `requiredAnalysisIds` (1-2 entries in the `<assetId>:<analysisType>` form, only for analyses that had `findings: true`);
- `questionTopics`: 2-4 entries. Each has `id` (`q.<slug>`), `topic` (short label like ""motive"", ""method"", ""location"", ""contactChannel"", ""accomplice""), and a `weight` (1.0 for primary topics, 0.5 for secondary). No options yet — those come per-question.";
        var user = $@"CONTEXT:
{ctxJson}

Emit JSON only.";

        var o = await TaskRunner.RunStructuredAsync<Output>(_llm, _logger, "SolutionSkeleton", system, user, Schema, ct,
            validate: output =>
            {
                TaskContractException.Require(output.RequiredEvidenceIds.All(reachableAssets.Contains),
                    "requiredEvidenceIds must refer only to existing reachable assets.");
                var analyses = draft.ForensicFull.Where(f => f.Findings)
                    .Select(f => $"{f.InputAssetId}:{f.AnalysisType}").ToHashSet(StringComparer.Ordinal);
                TaskContractException.Require(output.RequiredAnalysisIds.All(analyses.Contains),
                    "requiredAnalysisIds must refer only to existing outcomes with findings.");
                MicrotaskValidation.Unique(output.QuestionTopics.Select(q => q.Id), "questionTopics");
            });
        draft.RequiredEvidenceIds = o.RequiredEvidenceIds;
        draft.RequiredAnalysisIds = o.RequiredAnalysisIds;
        draft.QuestionTopics = o.QuestionTopics;
    }
}

/// <summary>Stage 12 — Solution Question. One call per topic.</summary>
public class QuestionTask
{
    private readonly ILLMProvider _llm;
    private readonly ILogger _logger;
    public QuestionTask(ILLMProvider llm, ILogger logger) { _llm = llm; _logger = logger; }

    private static readonly string Schema = MicrotaskSchemas.Question;

    public async Task<SolutionQuestion> RunAsync(CaseDraft draft, QuestionTopic topic, CancellationToken ct)
    {
        var system = $@"You are writing ONE multiple-choice question for the case solution.
Topic: ""{topic.Topic}"". Echo id `{topic.Id}` and `weight` {topic.Weight}.
3-4 options. Each option `id` MUST match the regex `^opt\.[a-z0-9_]+$` — start with the literal prefix `opt.`
(four characters: o, p, t, dot) followed by lowercase letters/digits/underscores. NEVER use `opt_` or just `opt-`.
All options must be plausible (no obvious distractors). One is the correct answer (`correctOptionId`), and it
MUST be one of the `option.id` values you emit.
Keep the prompt under 25 words. Cite planned facts with support excerpts from the question prompt or CORRECT option.
Never use a distractor as support. Preserve the exact canonical answer text; do not guess using private culprit information.";
        var plan = draft.Bible?.Questions.Single(q => q.Id == topic.Id);
        var context = plan is null ? draft.ToReviewJson() : CaseBibleAuthorship.Context(draft, plan.SourceIds.ToArray());
        var user = $@"SUPPORTED READ-ONLY CONTEXT:
{context}

SUPPORTED ANSWER: {plan?.Answer ?? "(use the supplied evidence)"}
REQUIRED FACT REFERENCES: {string.Join(", ", plan?.FactIds ?? new())}
ACTUAL AUTHORED SOURCES:
{(plan is null ? "(see draft)" : CaseBibleAuthorship.Materials(draft, plan.SourceIds))}

Emit JSON only.";
        return await TaskRunner.RunStructuredAsync<SolutionQuestion>(_llm, _logger, $"Question:{topic.Id}", system, user, Schema, ct,
            validate: q =>
            {
                MicrotaskValidation.Equal(q.Id, topic.Id, "question.id");
                TaskContractException.Require(q.Weight == topic.Weight, "question.weight must echo the topic weight.");
                MicrotaskValidation.Unique(q.Options.Select(o => o.Id), "options");
                MicrotaskValidation.Unique(q.Options.Select(o => o.Label), "option labels");
                TaskContractException.Require(q.Options.Any(o => o.Id == q.CorrectOptionId),
                    $"correctOptionId '{q.CorrectOptionId}' is not present in options; no answer will be substituted.");
                if (plan is not null)
                {
                    var answer = q.Options.Single(o => o.Id == q.CorrectOptionId);
                    MicrotaskValidation.Equal(answer.Label, plan.Answer, "question.answer");
                    CaseBibleAuthorship.Support(q.Support, plan.FactIds, $"{q.Prompt}\n{answer.Label}", q.Id);
                }
            });
    }
}

/// <summary>Stage 13 — Solution Explanation. 2-4 sentence markdown post-mortem.</summary>
public class ExplanationTask
{
    private readonly ILLMProvider _llm;
    private readonly ILogger _logger;
    public ExplanationTask(ILLMProvider llm, ILogger logger) { _llm = llm; _logger = logger; }

    private class Output : CaseAuthoredContent { [JsonPropertyName("explanation")] public string Explanation { get; set; } = string.Empty; }

    private static readonly string Schema = MicrotaskSchemas.Explanation;

    public async Task RunAsync(CaseDraft draft, CancellationToken ct)
    {
        var culprit = draft.SuspectFull.FirstOrDefault(s => s.Id == draft.CulpritId);
        var sourceIds = draft.Bible?.Sources.Where(s => s.Type is not ("photo" or "profile") &&
            s.FactIds.Any(draft.Bible.ExplanationFactIds.Contains)).Select(s => s.Id).ToArray();
        var context = sourceIds is null ? draft.ToReviewJson() : CaseBibleAuthorship.Context(draft, sourceIds);
        var system = @"You are writing the post-mortem explanation shown to the player when they solve the case (or run out of attempts).
Concise markdown. Explain how the complementary evidence supports the conclusion, with explicit source references.
Do NOT clear every decoy by fiat: exclusion requires positive independent facts; no findings or similarity are insufficient.
Return support[] excerpts covering exactly explanationFactIds when a CaseBible is provided.";
        var user = $@"SUPPORTED READ-ONLY CONTEXT:
{context}

SUPPORTED RESPONSIBILITY CONCLUSION: {culprit?.Name}
KEY EVIDENCE: {string.Join(", ", draft.RequiredEvidenceIds)}
KEY ANALYSES: {string.Join(", ", draft.RequiredAnalysisIds)}
REQUIRED FACT REFERENCES: {string.Join(", ", draft.Bible?.ExplanationFactIds ?? new())}
ACTUAL AUTHORED SOURCES:
{(sourceIds is null ? "(see draft)" : CaseBibleAuthorship.Materials(draft, sourceIds))}

Emit JSON only.";
        var o = await TaskRunner.RunStructuredAsync<Output>(_llm, _logger, "Explanation", system, user, Schema, ct,
            validate: result =>
            {
                if (draft.Bible is not null)
                    CaseBibleAuthorship.Support(result.Support, draft.Bible.ExplanationFactIds, result.Explanation, "explanation");
            });
        draft.Explanation = o.Explanation;
        draft.ExplanationSupport = o.Support;
    }
}
