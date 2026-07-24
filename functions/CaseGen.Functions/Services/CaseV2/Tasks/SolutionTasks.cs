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
        var contextualIds = draft.AssetStubs
            .Where(asset => asset.EvidenceRole == EvidenceRoles.Contextual)
            .Select(asset => asset.Id)
            .ToHashSet(StringComparer.Ordinal);
        reachableAssets.ExceptWith(contextualIds);

        var fullAssets = draft.AssetFull.Concat(draft.ResultAssets)
            .GroupBy(asset => asset.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var culpritClueIds = draft.Blueprint.ClueLadder
            .Where(clue => clue.SupportsSuspectId == draft.CulpritId)
            .Select(clue => clue.Id)
            .ToHashSet(StringComparer.Ordinal);
        var ctxJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            caseBible = draft.CaseBible,
            culpritId = draft.CulpritId,
            assets = draft.AssetStubs
                .Concat(draft.ResultAssets.Select(a => new AssetStub { Id = a.Id, Type = a.Type, Title = a.Title, Role = "lab result" }))
                .Where(a => !contextualIds.Contains(a.Id))
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

        var prompts = AgentPromptCatalog.Default;
        var system = prompts.RenderSystem("SolutionSkeleton", new Dictionary<string, object?>
        {
            ["language"] = ForensicMethodCatalog.NormalizeLanguage(draft.Request.Language),
            ["is_rookie"] = isRookie.ToString().ToLowerInvariant()
        });
        var user = prompts.RenderUser("SolutionSkeleton", new Dictionary<string, object?>
        {
            ["context_json"] = ctxJson
        });

        var o = await TaskRunner.RunStructuredAsync<Output>(_llm, _logger, "SolutionSkeleton", system, user, Schema, ct);

        // Defensive: drop any requiredEvidenceId that ended up unreachable anyway.
        var ids = o.RequiredEvidenceIds
            .Where(reachableAssets.Contains)
            .Where(id => !contextualIds.Contains(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (ids.Count == 0 && o.RequiredEvidenceIds.Count > 0)
        {
            _logger.LogWarning("SolutionSkeleton picked only unreachable or contextual evidence — selecting reachable investigative evidence");
            ids = draft.AssetStubs
                .Where(asset => reachableAssets.Contains(asset.Id))
                .OrderByDescending(asset => asset.SupportsClueIds.Any(culpritClueIds.Contains))
                .ThenBy(asset => asset.Id, StringComparer.Ordinal)
                .Take(3)
                .Select(asset => asset.Id)
                .ToList();
        }
        draft.RequiredEvidenceIds = ids;
        draft.RequiredAnalysisIds = isRookie
            ? new()
            : LimitRequiredAnalyses(draft, o.RequiredAnalysisIds);
        draft.QuestionTopics = o.QuestionTopics
            .Select(topic =>
            {
                topic.SupportingEvidenceIds = topic.SupportingEvidenceIds
                    .Where(reachableAssets.Contains)
                    .Where(id => !contextualIds.Contains(id))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                return topic;
            })
            .Where(topic => topic.SupportingEvidenceIds.Count > 0)
            .ToList();
    }

    private static List<string> LimitRequiredAnalyses(CaseDraft draft, IEnumerable<string> proposedIds)
    {
        var availableIds = draft.ForensicFull
            .Where(outcome => outcome.Findings)
            .Select(outcome => $"{outcome.InputAssetId}:{outcome.AnalysisType}")
            .Concat(draft.ForensicStubs
                .Where(outcome => outcome.Findings)
                .Select(outcome => $"{outcome.InputAssetId}:{outcome.AnalysisType}"))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);
        var optionalMinimum = DifficultyProfileCatalog.Get(draft).Topology.MinOptionalAnalyses;
        var maximumRequired = Math.Max(0, availableIds.Count - optionalMinimum);
        return proposedIds
            .Where(availableIds.Contains)
            .Distinct(StringComparer.Ordinal)
            .Take(maximumRequired)
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
        var supportingAssets = draft.AssetFull.Concat(draft.ResultAssets)
            .Where(asset => topic.SupportingEvidenceIds.Contains(asset.Id, StringComparer.Ordinal))
            .Select(asset => new
            {
                asset.Id,
                asset.Title,
                asset.Description,
                content = RenderedEvidence(asset)
            });
        var prompts = AgentPromptCatalog.Default;
        var system = prompts.RenderSystem("Question", new Dictionary<string, object?>
        {
            ["language"] = ForensicMethodCatalog.NormalizeLanguage(draft.Request.Language),
            ["topic"] = topic.Topic,
            ["question_id"] = topic.Id,
            ["weight"] = topic.Weight,
            ["supporting_evidence_ids"] = string.Join(", ", topic.SupportingEvidenceIds)
        });
        var user = prompts.RenderUser("Question", new Dictionary<string, object?>
        {
            ["case_draft_json"] = draft.ToSummaryJson(),
            ["culprit_context"] = draft.SuspectFull.FirstOrDefault(s => s.Id == draft.CulpritId)?.Motive
                                  ?? "(see canonical culprit profile)",
            ["supporting_assets_json"] = System.Text.Json.JsonSerializer.Serialize(supportingAssets)
        });
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
        var prompts = AgentPromptCatalog.Default;
        var system = prompts.RenderSystem("Explanation", new Dictionary<string, object?>
        {
            ["language"] = ForensicMethodCatalog.NormalizeLanguage(draft.Request.Language)
        });
        var user = prompts.RenderUser("Explanation", new Dictionary<string, object?>
        {
            ["case_draft_json"] = draft.ToSummaryJson(),
            ["culprit_context"] = $"{culprit?.Name} — {culprit?.Motive}",
            ["required_evidence_ids"] = string.Join(", ", draft.RequiredEvidenceIds),
            ["required_analysis_ids"] = string.Join(", ", draft.RequiredAnalysisIds)
        });
        var o = await TaskRunner.RunStructuredAsync<Output>(_llm, _logger, "Explanation", system, user, Schema, ct);
        draft.Explanation = o.Explanation;
    }
}
