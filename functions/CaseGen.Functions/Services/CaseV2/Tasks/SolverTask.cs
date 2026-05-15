using System.Text.Json;
using System.Text.Json.Serialization;
using CaseGen.Functions.Models.CaseV2;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.CaseV2.Tasks;

/// <summary>
/// Stage 15 — Solver. Plays the case from the perspective of a detective who
/// sees ONLY what the sanitised payload exposes (no rules, no forensicOutcomes,
/// no solution internals). The solver picks a culprit, required evidence,
/// required analyses, and answers each question. The orchestrator then runs
/// the same scoring formula <see cref="Services.SolutionService"/> would run
/// on the live site and reports the resulting score + breakdown.
///
/// This lets us catch "the case isn't actually solvable from the player's POV"
/// flaws that schema validation can't see.
/// </summary>
public class SolverTask
{
    private readonly ILLMProvider _llm;
    private readonly ILogger _logger;
    public SolverTask(ILLMProvider llm, ILogger logger) { _llm = llm; _logger = logger; }

    public class Attempt
    {
        [JsonPropertyName("suspectId")] public string SuspectId { get; set; } = string.Empty;
        [JsonPropertyName("evidenceIds")] public List<string> EvidenceIds { get; set; } = new();
        [JsonPropertyName("analysisIds")] public List<string> AnalysisIds { get; set; } = new();
        [JsonPropertyName("answers")] public List<SolverAnswer> Answers { get; set; } = new();
        [JsonPropertyName("rationale")] public string? Rationale { get; set; }
    }

    public class SolverAnswer
    {
        [JsonPropertyName("questionId")] public string QuestionId { get; set; } = string.Empty;
        [JsonPropertyName("optionId")] public string OptionId { get; set; } = string.Empty;
    }

    public class SolverResult
    {
        public bool Correct { get; set; }
        public double Score { get; set; }
        public double CulpritScore { get; set; }
        public double EvidenceScore { get; set; }
        public double AnalysisScore { get; set; }
        public double QuestionsScore { get; set; }
        public Attempt Attempt { get; set; } = new();
        public string? Notes { get; set; }
    }

    private const string Schema = """
    {"type":"object","required":["suspectId","evidenceIds","analysisIds","answers"],
     "properties":{
       "suspectId":{"type":"string","pattern":"^suspect\\.[a-z0-9_]+$"},
       "evidenceIds":{"type":"array","items":{"type":"string","pattern":"^asset\\.[a-z0-9_]+$"}},
       "analysisIds":{"type":"array","items":{"type":"string","pattern":"^asset\\.[a-z0-9_]+:[A-Za-z0-9_]+$"}},
       "answers":{"type":"array","items":{"type":"object","required":["questionId","optionId"]}}
     }}
    """;

    public async Task<SolverResult> RunAsync(CaseDraft draft, CancellationToken ct)
    {
        // Sanitised view: visible assets/emails/suspects + the question prompts WITHOUT
        // correctOptionId; we feed the solver only what a player would see on the site.
        var visibleAssets = draft.AssetFull
            .Where(a => string.Equals(a.Visibility, "initial", StringComparison.OrdinalIgnoreCase))
            .Select(a => new { a.Id, a.Type, a.Title, a.Description });
        var visibleEmails = draft.FollowUpEmails
            .Concat(new[]
            {
                new EvidenceEmail
                {
                    Id = "email.briefing",
                    From = draft.Briefing.From,
                    Subject = draft.Briefing.Subject,
                    Body = draft.Briefing.Body,
                    Visibility = "initial"
                }
            })
            .Where(e => string.Equals(e.Visibility, "initial", StringComparison.OrdinalIgnoreCase))
            .Select(e => new { e.Id, e.From, e.Subject, e.Body });
        var visibleSuspects = draft.SuspectFull
            // PlotSuspect doesn't carry a Visibility field — every suspect produced
            // by Phase 2 is initial-visible by construction.
            .Select(s => new
            {
                s.Id, s.Name, s.Occupation, s.Relationship, s.Motive, s.Alibi, s.AlibiVerified, s.Background
            });

        // For a Rookie / all_initial case the solver also sees the hidden lab assets/emails
        // because the runtime auto-reveals them. We approximate by including ResultEmails/Assets too.
        var rookie = string.Equals(draft.Metadata.RequiredRank, "Rookie", StringComparison.OrdinalIgnoreCase);
        var availableAssets = rookie
            ? visibleAssets.Concat(draft.ResultAssets.Select(a => new { a.Id, a.Type, a.Title, a.Description }))
            : visibleAssets;
        var availableEmails = rookie
            ? visibleEmails.Concat(draft.ResultEmails.Select(e => new { e.Id, e.From, e.Subject, e.Body }))
            : visibleEmails;
        var availableAnalyses = rookie
            ? draft.ForensicFull
                .Where(f => f.Findings)
                .Select(f => $"{f.InputAssetId}:{f.AnalysisType}")
                .ToList()
            : new List<string>();

        var ctxJson = JsonSerializer.Serialize(new
        {
            metadata = new { draft.Metadata.Title, draft.Metadata.Location, draft.Metadata.IncidentDate, draft.Metadata.OpenedAt },
            assets = availableAssets,
            emails = availableEmails,
            suspects = visibleSuspects,
            timeline = draft.Timeline,
            availableAnalyses,
            questions = draft.Questions.Select(q => new
            {
                q.Id, q.Prompt,
                options = q.Options.Select(o => new { o.Id, o.Label }) // NO correctOptionId
            })
        });

        var system = @"You are playing this detective case AS THE PLAYER. You DO NOT see the solution.
Pick:
  - suspectId — the person you believe is the culprit, from `suspects`.
  - evidenceIds — ALL the assets that support your accusation (pick 1-4 strongest).
  - analysisIds — the forensic analyses (`<assetId>:<analysisType>`) that were decisive.
  - answers — one option per question.
Also write a short `rationale` (≤2 sentences) explaining the logic.
Be honest — pick the suspect the evidence actually points to.";

        var user = $@"CASE (player-visible state):
{ctxJson}

Emit JSON only.";

        Attempt attempt;
        try
        {
            var raw = await _llm.GenerateStructuredResponseAsync(system, user, Schema, ct);
            attempt = JsonSerializer.Deserialize<Attempt>(raw.Content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                      ?? new Attempt();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Solver failed — returning unscored");
            return new SolverResult { Notes = "Solver failed: " + ex.Message };
        }

        return Grade(draft, attempt);
    }

    /// <summary>Apply the exact same scoring formula used by the live runtime
    /// (mirrors <c>backend/CaseZeroApi/Services/SolutionService.cs</c>) so the solver
    /// score is an honest preview of what a player would get.</summary>
    private SolverResult Grade(CaseDraft d, Attempt attempt)
    {
        var pcr = new SolutionPartialCredit(); // defaults 0.4/0.2/0.2/0.2

        var culpritCorrect = attempt.SuspectId == d.CulpritId;
        var culpritScore = culpritCorrect ? pcr.CulpritWeight : 0;

        double evidenceScore;
        if (d.RequiredEvidenceIds.Count == 0) evidenceScore = pcr.EvidenceWeight;
        else
        {
            var hit = attempt.EvidenceIds.Intersect(d.RequiredEvidenceIds, StringComparer.Ordinal).Count();
            evidenceScore = pcr.EvidenceWeight * ((double)hit / d.RequiredEvidenceIds.Count);
        }

        double analysisScore;
        if (d.RequiredAnalysisIds.Count == 0) analysisScore = pcr.AnalysisWeight;
        else
        {
            var hit = attempt.AnalysisIds.Intersect(d.RequiredAnalysisIds, StringComparer.Ordinal).Count();
            analysisScore = pcr.AnalysisWeight * ((double)hit / d.RequiredAnalysisIds.Count);
        }

        double questionsScore = 0;
        if (d.Questions.Count > 0)
        {
            double tw = 0, gained = 0;
            foreach (var q in d.Questions)
            {
                var w = q.Weight; tw += w;
                var ans = attempt.Answers.FirstOrDefault(a => a.QuestionId == q.Id);
                if (ans is not null && ans.OptionId == q.CorrectOptionId) gained += w;
            }
            if (tw > 0) questionsScore = pcr.QuestionsWeight * (gained / tw);
        }

        var total = culpritScore + evidenceScore + analysisScore + questionsScore;
        return new SolverResult
        {
            Attempt = attempt,
            Score = Math.Round(total, 4),
            CulpritScore = Math.Round(culpritScore, 4),
            EvidenceScore = Math.Round(evidenceScore, 4),
            AnalysisScore = Math.Round(analysisScore, 4),
            QuestionsScore = Math.Round(questionsScore, 4),
            Correct = total >= 0.7 // mirror default minimumScore
        };
    }
}
