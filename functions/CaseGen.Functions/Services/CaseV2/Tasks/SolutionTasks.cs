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

    private const string Schema = """
    {"type":"object","required":["requiredEvidenceIds","requiredAnalysisIds","questionTopics"],
     "properties":{
       "requiredEvidenceIds":{"type":"array","minItems":1,"items":{"type":"string","pattern":"^asset\\.[a-z0-9_]+$"}},
       "requiredAnalysisIds":{"type":"array","minItems":1,"items":{"type":"string","pattern":"^asset\\.[a-z0-9_]+:[A-Za-z0-9_]+$"}},
       "questionTopics":{"type":"array","minItems":2,"maxItems":4,
         "items":{"type":"object","required":["id","topic","weight"],
           "properties":{"id":{"type":"string","pattern":"^q\\.[a-z0-9_]+$"}}}}}}
    """;

    public async Task RunAsync(CaseDraft draft, CancellationToken ct)
    {
        var ctxJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            culpritId = draft.CulpritId,
            assets = draft.AssetStubs.Concat(draft.ResultAssets.Select(a => new AssetStub { Id = a.Id, Type = a.Type, Title = a.Title, Role = "lab result" })),
            forensics = draft.ForensicFull.Where(f => f.Findings).Select(f => new { id = $"{f.InputAssetId}:{f.AnalysisType}", f.MatchedSuspectId })
        });

        var system = @"You are writing the **skeleton** of the case solution.
Pick:
- `requiredEvidenceIds` (1-3 strongest probative asset IDs — include result PDFs when they tie the culprit);
- `requiredAnalysisIds` (1-2 entries in the `<assetId>:<analysisType>` form, only for analyses that had `findings: true`);
- `questionTopics`: 2-4 entries. Each has `id` (`q.<slug>`), `topic` (short label like ""motive"", ""method"", ""location"", ""contactChannel"", ""accomplice""), and a `weight` (1.0 for primary topics, 0.5 for secondary). No options yet — those come per-question.";
        var user = $@"CONTEXT:
{ctxJson}

Emit JSON only.";

        var o = await TaskRunner.RunStructuredAsync<Output>(_llm, _logger, "SolutionSkeleton", system, user, Schema, ct);
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

    private const string Schema = """
    {"type":"object","required":["id","prompt","options","correctOptionId","weight"],
     "properties":{
       "id":{"type":"string","pattern":"^q\\.[a-z0-9_]+$"},
       "options":{"type":"array","minItems":3,"maxItems":4,
         "items":{"type":"object","required":["id","label"],
           "properties":{"id":{"type":"string","pattern":"^opt\\.[a-z0-9_]+$"}}}},
       "correctOptionId":{"type":"string","pattern":"^opt\\.[a-z0-9_]+$"}}}
    """;

    public async Task<SolutionQuestion> RunAsync(CaseDraft draft, QuestionTopic topic, CancellationToken ct)
    {
        var system = $@"You are writing ONE multiple-choice question for the case solution.
Topic: ""{topic.Topic}"". Echo id `{topic.Id}` and `weight` {topic.Weight}.
3-4 options (opt.<slug>), all plausible (no obvious distractors). One is the correct answer (`correctOptionId`).
Keep the prompt under 25 words.";
        var user = $@"CASE DRAFT (read-only):
{draft.ToSummaryJson()}

CULPRIT INFO: {draft.SuspectFull.FirstOrDefault(s => s.Id == draft.CulpritId)?.Motive ?? "(see culprit suspect's motive in draft)"}

Emit JSON only.";
        var q = await TaskRunner.RunStructuredAsync<SolutionQuestion>(_llm, _logger, $"Question:{topic.Id}", system, user, Schema, ct);
        q.Id = topic.Id;
        q.Weight = topic.Weight;
        // Ensure correctOptionId is one of the options.
        if (!q.Options.Any(o => o.Id == q.CorrectOptionId) && q.Options.Count > 0)
        {
            _logger.LogWarning("Question {Id} correctOptionId {Correct} not in options — defaulting to first", q.Id, q.CorrectOptionId);
            q.CorrectOptionId = q.Options[0].Id;
        }
        return q;
    }
}

/// <summary>Stage 13 — Solution Explanation. 2-4 sentence markdown post-mortem.</summary>
public class ExplanationTask
{
    private readonly ILLMProvider _llm;
    private readonly ILogger _logger;
    public ExplanationTask(ILLMProvider llm, ILogger logger) { _llm = llm; _logger = logger; }

    private class Output { [JsonPropertyName("explanation")] public string Explanation { get; set; } = string.Empty; }

    private const string Schema = """
    {"type":"object","required":["explanation"],"properties":{"explanation":{"type":"string","minLength":40}}}
    """;

    public async Task RunAsync(CaseDraft draft, CancellationToken ct)
    {
        var culprit = draft.SuspectFull.FirstOrDefault(s => s.Id == draft.CulpritId);
        var system = @"You are writing the post-mortem explanation shown to the player when they solve the case (or run out of attempts).
2-4 sentence markdown. Tie the evidence + forensic analyses to the culprit. State why each decoy is cleared.";
        var user = $@"CASE DRAFT (read-only):
{draft.ToSummaryJson()}

CULPRIT: {culprit?.Name} — motive: {culprit?.Motive}
KEY EVIDENCE: {string.Join(", ", draft.RequiredEvidenceIds)}
KEY ANALYSES: {string.Join(", ", draft.RequiredAnalysisIds)}

Emit JSON only.";
        var o = await TaskRunner.RunStructuredAsync<Output>(_llm, _logger, "Explanation", system, user, Schema, ct);
        draft.Explanation = o.Explanation;
    }
}
