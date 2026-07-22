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
       "requiredAnalysisIds":{"type":"array","items":{"type":"string","pattern":"^asset\\.[a-z0-9_]+:[A-Za-z0-9_]+$"}},
       "questionTopics":{"type":"array","minItems":2,"maxItems":4,
         "items":{"type":"object","required":["id","topic","weight","supportingEvidenceIds"],
           "properties":{
             "id":{"type":"string","pattern":"^q\\.[a-z0-9_]+$"},
             "supportingEvidenceIds":{"type":"array","minItems":1,"maxItems":3,
               "items":{"type":"string","pattern":"^asset\\.[a-z0-9_]+$"}}}}}}}
    """;

    public async Task RunAsync(CaseDraft draft, CancellationToken ct)
    {
        var isRookie = string.Equals(draft.Metadata.Difficulty, "Rookie", StringComparison.OrdinalIgnoreCase)
            || string.Equals(draft.Metadata.RequiredRank, "Rookie", StringComparison.OrdinalIgnoreCase);
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

        var fullAssets = draft.AssetFull.Concat(draft.ResultAssets)
            .GroupBy(asset => asset.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var culpritClueIds = draft.Blueprint.ClueLadder
            .Where(clue => clue.SupportsSuspectId == draft.CulpritId)
            .Select(clue => clue.Id)
            .ToHashSet(StringComparer.Ordinal);
        var ctxJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            culpritId = draft.CulpritId,
            assets = draft.AssetStubs
                .Concat(draft.ResultAssets.Select(a => new AssetStub { Id = a.Id, Type = a.Type, Title = a.Title, Role = "lab result" }))
                .Select(a =>
                {
                    fullAssets.TryGetValue(a.Id, out var full);
                    return new
                    {
                        a.Id,
                        a.Type,
                        a.Title,
                        role = a.Role,
                        reachable = reachableAssets.Contains(a.Id),
                        supportsCulprit = a.SupportsClueIds.Any(culpritClueIds.Contains),
                        clueIds = a.SupportsClueIds,
                        description = full?.Description,
                        content = RenderedEvidence(full)
                    };
                }),
            forensics = draft.ForensicFull.Where(f => f.Findings).Select(f => new { id = $"{f.InputAssetId}:{f.AnalysisType}", f.MatchedSuspectId })
        });

        var system = $@"You are writing the **skeleton** of the case solution.

CRITICAL CONSTRAINT: every id you put in `requiredEvidenceIds` MUST be marked `reachable: true` in the supplied context.
`reachable` means the asset is either visibility=initial or already gets revealed by a pre-built rule. Picking an
unreachable asset would make the case unsolvable — DO NOT do it.

Pick:
- `requiredEvidenceIds` (1-3 reachable assets that are the strongest probative items, including result PDFs when reachable);
- `requiredAnalysisIds`: {(isRookie ? "MUST be an empty array. Rookie cases have no forensic workflow." : "1-2 entries in the `<assetId>:<analysisType>` form, only for analyses that had `findings: true`")};
- `questionTopics`: 2-4 entries. Each has `id` (`q.<slug>`), `topic`, `weight`, and `supportingEvidenceIds`.
  Every supporting evidence id must be reachable and must directly contain the fact needed to answer the future question.
  Do not select a topic such as exact method, hidden location, or private motive unless an initial/reachable asset explicitly states enough to distinguish the correct option.
  Never create a suspect-identity / 'who did it' topic. Culprit identification is scored separately; questions must test evidence interpretation, chronology, method, motive, or contradiction.";
        system += isRookie
            ? "\nFor Rookie, requiredEvidenceIds must prioritize at least two different reachable assets marked `supportsCulprit: true` when available. Do not require background-only opportunity records when stronger culprit-supporting assets exist."
            : string.Empty;
        var user = $@"CONTEXT:
{ctxJson}

Emit JSON only.";

        var o = await TaskRunner.RunStructuredAsync<Output>(_llm, _logger, "SolutionSkeleton", system, user, Schema, ct);

        // Defensive: drop any requiredEvidenceId that ended up unreachable anyway.
        var ids = o.RequiredEvidenceIds.Where(reachableAssets.Contains).ToList();
        if (ids.Count == 0 && o.RequiredEvidenceIds.Count > 0)
        {
            _logger.LogWarning("SolutionSkeleton picked only unreachable evidence — keeping originals so downstream auto-fix can promote them");
            ids = o.RequiredEvidenceIds;
        }
        draft.RequiredEvidenceIds = ids;
        draft.RequiredAnalysisIds = isRookie ? new() : o.RequiredAnalysisIds;
        draft.QuestionTopics = o.QuestionTopics
            .Where(topic => topic.SupportingEvidenceIds.Any(reachableAssets.Contains))
            .ToList();
    }

    private static string? RenderedEvidence(EvidenceAsset? asset)
    {
        if (asset is null) return null;
        return string.Join("\n", new[]
        {
            asset.Body,
            asset.BodyDoc is null ? null : System.Text.Json.JsonSerializer.Serialize(asset.BodyDoc),
            asset.Description
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
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
3-4 options. Each option `id` MUST match the regex `^opt\.[a-z0-9_]+$` — start with the literal prefix `opt.`
(four characters: o, p, t, dot) followed by lowercase letters/digits/underscores. NEVER use `opt_` or just `opt-`.
All options must be plausible (no obvious distractors). One is the correct answer (`correctOptionId`), and it
MUST be one of the `option.id` values you emit.
Keep the prompt under 25 words.
The correct answer must be directly supported by these evidence IDs: {string.Join(", ", topic.SupportingEvidenceIds)}.
If those documents do not support the originally proposed topic, ask a narrower question that they do support.

CRITICAL — NO NAME LEAKAGE:
- This question is about WHAT happened (the topic above), NEVER about WHO did it.
- DO NOT name any suspect, victim, or witness in the prompt or in any option label. Suspect
  identification is captured as a separate field; revealing names here would spoil the case.
- Refer to people generically (in the same language as the prompt): ""the perpetrator"",
  ""the victim"", ""an accomplice"", ""the contact"", ""the witness"". Pick whichever generic role
  fits the topic — never an actual name from the draft.";
        var supportingAssets = draft.AssetFull.Concat(draft.ResultAssets)
            .Where(asset => topic.SupportingEvidenceIds.Contains(asset.Id, StringComparer.Ordinal))
            .Select(asset => new
            {
                asset.Id,
                asset.Title,
                asset.Description,
                content = RenderedEvidence(asset)
            });
        var user = $@"CASE DRAFT (read-only):
{draft.ToSummaryJson()}

CULPRIT INFO: {draft.SuspectFull.FirstOrDefault(s => s.Id == draft.CulpritId)?.Motive ?? "(see culprit suspect's motive in draft)"}

EXACT PLAYER-VISIBLE CONTENT SUPPORTING THIS QUESTION:
{System.Text.Json.JsonSerializer.Serialize(supportingAssets)}

Emit JSON only.";
        var q = await TaskRunner.RunStructuredAsync<SolutionQuestion>(_llm, _logger, $"Question:{topic.Id}", system, user, Schema, ct);
        q.Id = topic.Id;
        q.Weight = topic.Weight;

        var originalCorrectOptionId = q.CorrectOptionId;
        var normalizedOptionIds = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var opt in q.Options)
        {
            var originalId = opt.Id;
            opt.Id = NormalizeOptionId(originalId);
            normalizedOptionIds[originalId] = opt.Id;
        }
        q.CorrectOptionId = normalizedOptionIds.GetValueOrDefault(originalCorrectOptionId)
                            ?? NormalizeOptionId(originalCorrectOptionId);

        // Ensure correctOptionId is one of the options.
        if (!q.Options.Any(o => o.Id == q.CorrectOptionId) && q.Options.Count > 0)
        {
            _logger.LogWarning("Question {Id} correctOptionId {Correct} not in options — defaulting to first", q.Id, q.CorrectOptionId);
            q.CorrectOptionId = q.Options[0].Id;
        }

        // Defensive: if the LLM ignored the no-name-leakage rule, scrub the surface forms.
        // We replace whole-word occurrences of each suspect's full name, given name, family name,
        // and alias with a neutral placeholder so the player never sees a spoiler.
        ScrubSuspectNames(q, draft);

        return q;
    }

    private static string NormalizeOptionId(string? value)
    {
        var suffix = value?.Trim() ?? string.Empty;
        if (suffix.StartsWith("opt.", StringComparison.OrdinalIgnoreCase)
            || suffix.StartsWith("opt_", StringComparison.OrdinalIgnoreCase)
            || suffix.StartsWith("opt-", StringComparison.OrdinalIgnoreCase))
        {
            suffix = suffix[4..];
        }

        var normalized = new string(suffix
            .ToLowerInvariant()
            .Select(character => char.IsAsciiLetterOrDigit(character) ? character : '_')
            .ToArray()).Trim('_');
        return $"opt.{(string.IsNullOrWhiteSpace(normalized) ? "option" : normalized)}";
    }

    private static string RenderedEvidence(EvidenceAsset asset) =>
        string.Join("\n", new[]
        {
            asset.Body,
            asset.BodyDoc is null ? null : System.Text.Json.JsonSerializer.Serialize(asset.BodyDoc),
            asset.Description
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

    /// <summary>
    /// Best-effort scrub of suspect surface forms in a question's prompt and option labels.
    /// Logs a warning when a leak is detected so we can tighten the upstream prompt over time.
    /// </summary>
    private void ScrubSuspectNames(SolutionQuestion q, CaseDraft draft)
    {
        var tokens = BuildSuspectSurfaceForms(draft);
        if (tokens.Count == 0) return;

        var leakedIn = new List<string>();

        var newPrompt = ReplaceSurfaceForms(q.Prompt, tokens, out var promptHits);
        if (promptHits > 0) { q.Prompt = newPrompt; leakedIn.Add("prompt"); }

        for (int i = 0; i < q.Options.Count; i++)
        {
            var label = q.Options[i].Label;
            var replaced = ReplaceSurfaceForms(label, tokens, out var optHits);
            if (optHits > 0)
            {
                q.Options[i].Label = replaced;
                leakedIn.Add($"option[{i}]");
            }
        }

        if (leakedIn.Count > 0)
            _logger.LogWarning("Question {Id} leaked suspect name(s) in {Where} — scrubbed with placeholder", q.Id, string.Join(",", leakedIn));
    }

    private static List<string> BuildSuspectSurfaceForms(CaseDraft draft)
    {
        var forms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in draft.SuspectFull)
        {
            void Add(string? v)
            {
                if (string.IsNullOrWhiteSpace(v)) return;
                var t = v.Trim();
                if (t.Length < 3) return; // skip initials that would over-match
                forms.Add(t);
            }
            Add(s.Name);
            if (!string.IsNullOrWhiteSpace(s.Name))
            {
                var parts = s.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var p in parts) Add(p);
            }
        }
        // Longest first so multi-word matches win over individual tokens.
        return forms.OrderByDescending(f => f.Length).ToList();
    }

    private static string ReplaceSurfaceForms(string text, List<string> tokens, out int hits)
    {
        hits = 0;
        if (string.IsNullOrEmpty(text)) return text;
        var result = text;
        foreach (var token in tokens)
        {
            // Whole-word, case-insensitive replacement. Escape regex specials in the token.
            var pattern = "\\b" + System.Text.RegularExpressions.Regex.Escape(token) + "\\b";
            var matches = System.Text.RegularExpressions.Regex.Matches(result, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (matches.Count == 0) continue;
            hits += matches.Count;
            result = System.Text.RegularExpressions.Regex.Replace(result, pattern, "[redacted]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        return result;
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
Write 4-7 concise sentences. Reconstruct the causal sequence, then triangulate the culprit using at least two independent clues and the decisive analysis. State the limitation of the forensic result where relevant, and explain how the strongest red herrings are resolved. Do not merely list evidence IDs.";
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
