using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CaseGen.Functions.Models.CaseV2;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.CaseV2.Tasks;

/// <summary>
/// Stage 14 — Red-Team. After the case is fully assembled and schema-valid,
/// an adversarial agent looks for semantic flaws (weak smoking gun, fragile
/// culprit alibi, decoys too obvious, timeline drift, motive vagueness, etc.).
/// Returns an audit report; the orchestrator records it in
/// gameMetadata.generation.redTeam and surfaces severity in the HTTP response.
/// </summary>
public class RedTeamTask
{
    private readonly ILLMProvider _llm;
    private readonly ILogger _logger;
    public RedTeamTask(ILLMProvider llm, ILogger logger) { _llm = llm; _logger = logger; }

    public class Finding
    {
        [JsonPropertyName("severity")] public string Severity { get; set; } = "low"; // low | medium | high
        [JsonPropertyName("area")] public string Area { get; set; } = string.Empty;
        [JsonPropertyName("issue")] public string Issue { get; set; } = string.Empty;
        [JsonPropertyName("suggestion")] public string? Suggestion { get; set; }
    }

    public class Report
    {
        [JsonPropertyName("verdict")] public string Verdict { get; set; } = "needs_review"; // ok | needs_review | reject
        [JsonPropertyName("findings")] public List<Finding> Findings { get; set; } = new();
        [JsonPropertyName("notes")] public string? Notes { get; set; }
    }

    private const string Schema = """
    {
      "type":"object","required":["verdict","findings"],
      "properties":{
        "verdict":{"type":"string","enum":["ok","needs_review","reject"]},
        "findings":{"type":"array","items":{"type":"object","required":["severity","area","issue"],
          "properties":{"severity":{"type":"string","enum":["low","medium","high"]}}}},
        "notes":{"type":["string","null"]}
      }
    }
    """;

    public async Task<Report> RunAsync(CaseDraft draft, string assembledJson, CancellationToken ct)
    {
        // Compact view of the case (without re-pasting bodies): the LLM still needs to see
        // suspects with their motive/alibi/background, the timeline, and the forensic outcomes.
        var culprit = draft.SuspectFull.FirstOrDefault(s => s.Id == draft.CulpritId);
        var ctxJson = JsonSerializer.Serialize(new
        {
            metadata = new
            {
                draft.Metadata.Title, draft.Metadata.Location,
                draft.Metadata.IncidentDate, draft.Metadata.OpenedAt,
                draft.Metadata.Difficulty, draft.Metadata.RequiredRank
            },
            culprit = culprit is null ? null : new { culprit.Id, culprit.Name, culprit.Motive, culprit.Alibi, culprit.AlibiVerified },
            decoys = draft.SuspectFull.Where(s => s.Id != draft.CulpritId)
                                       .Select(s => new { s.Id, s.Name, s.Motive, s.Alibi, s.AlibiVerified }),
            assets = draft.AssetFull.Concat(draft.ResultAssets).Select(a => new { a.Id, a.Type, a.Title, a.Description, a.Visibility }),
            timeline = draft.Timeline,
            forensics = draft.ForensicFull.Select(f => new { f.InputAssetId, f.AnalysisType, f.Findings, f.MatchedSuspectId, f.ConclusionText }),
            rules = draft.Rules.Select(r => new { r.RuleId, trigger = r.Trigger.Type, actionCount = r.Actions.Count }),
            solution = new
            {
                draft.CulpritId,
                draft.RequiredEvidenceIds,
                draft.RequiredAnalysisIds,
                questions = draft.Questions.Select(q => new { q.Id, q.Prompt, options = q.Options.Select(o => o.Label), correct = q.Options.FirstOrDefault(o => o.Id == q.CorrectOptionId)?.Label })
            }
        });

        return await ExecuteAsync(ctxJson, "draft", ct);
    }

    /// <summary>
    /// Re-runs the red-team audit against an already-assembled case.json string
    /// (typically produced by RefineCaseTask). Used after refine to verify that
    /// the refined JSON actually addressed the original findings.
    /// </summary>
    public async Task<Report> RunOnAssembledAsync(string assembledJson, CancellationToken ct)
    {
        var ctxJson = BuildCompactContextFromJson(assembledJson);
        return await ExecuteAsync(ctxJson, "refined-json", ct);
    }

    private static string BuildCompactContextFromJson(string assembledJson)
    {
        try
        {
            var node = JsonNode.Parse(assembledJson);
            if (node is null) return assembledJson;

            var metadata = node["metadata"];
            var suspectsArr = node["suspects"] as JsonArray;
            var assetsArr = node["assets"] as JsonArray;
            var emailsArr = node["emails"] as JsonArray;
            var timeline = node["timeline"];
            var temporal = node["temporalEvents"];
            var rulesArr = node["rules"] as JsonArray;
            var outcomesArr = node["forensicOutcomes"] as JsonArray;
            var solution = node["solution"];

            string? culpritId = solution?["culpritId"]?.GetValue<string>();
            JsonNode? culprit = null;
            var decoys = new JsonArray();
            if (suspectsArr is not null)
            {
                foreach (var s in suspectsArr)
                {
                    if (s is null) continue;
                    var compactSuspect = new JsonObject
                    {
                        ["id"] = s["id"]?.GetValue<string>(),
                        ["name"] = s["name"]?.GetValue<string>(),
                        ["motive"] = s["motive"]?.GetValue<string>(),
                        ["alibi"] = s["alibi"]?.GetValue<string>(),
                        ["alibiVerified"] = s["alibiVerified"]?.GetValue<bool>() ?? false
                    };
                    if (culpritId != null && s["id"]?.GetValue<string>() == culpritId)
                        culprit = compactSuspect;
                    else
                        decoys.Add(compactSuspect);
                }
            }

            var compactAssets = new JsonArray();
            if (assetsArr is not null)
            {
                foreach (var a in assetsArr)
                {
                    if (a is null) continue;
                    compactAssets.Add(new JsonObject
                    {
                        ["id"] = a["id"]?.GetValue<string>(),
                        ["type"] = a["type"]?.GetValue<string>(),
                        ["title"] = a["title"]?.GetValue<string>(),
                        ["description"] = a["description"]?.GetValue<string>(),
                        ["visibility"] = a["visibility"]?.GetValue<string>()
                    });
                }
            }

            var compactRules = new JsonArray();
            if (rulesArr is not null)
            {
                foreach (var r in rulesArr)
                {
                    if (r is null) continue;
                    var actions = r["actions"] as JsonArray;
                    compactRules.Add(new JsonObject
                    {
                        ["ruleId"] = r["ruleId"]?.GetValue<string>(),
                        ["trigger"] = r["trigger"]?["type"]?.GetValue<string>(),
                        ["actionCount"] = actions?.Count ?? 0
                    });
                }
            }

            var compactForensics = new JsonArray();
            if (outcomesArr is not null)
            {
                foreach (var f in outcomesArr)
                {
                    if (f is null) continue;
                    compactForensics.Add(new JsonObject
                    {
                        ["inputAssetId"] = f["inputAssetId"]?.GetValue<string>(),
                        ["analysisType"] = f["analysisType"]?.GetValue<string>(),
                        ["findings"] = f["findings"]?.GetValue<bool>() ?? false,
                        ["matchedSuspectId"] = f["matchedSuspectId"]?.GetValue<string>(),
                        ["conclusionText"] = f["conclusionText"]?.GetValue<string>()
                    });
                }
            }

            var compact = new JsonObject
            {
                ["metadata"] = metadata?.DeepClone(),
                ["culprit"] = culprit,
                ["decoys"] = decoys,
                ["assets"] = compactAssets,
                ["timeline"] = timeline?.DeepClone(),
                ["temporalEvents"] = temporal?.DeepClone(),
                ["forensics"] = compactForensics,
                ["rules"] = compactRules,
                ["solution"] = solution?.DeepClone(),
                ["emailCount"] = emailsArr?.Count ?? 0
            };

            return compact.ToJsonString();
        }
        catch
        {
            // If anything goes sideways, fall back to passing the raw JSON.
            return assembledJson;
        }
    }

    private async Task<Report> ExecuteAsync(string ctxJson, string source, CancellationToken ct)
    {
        var system = @"You are the **red-team reviewer** for an interactive detective case. Audit the supplied case package for semantic flaws that would make the case unsolvable, unfair, or implausible. Be ruthless and concise.

Look specifically for:
1. Smoking gun strength — does at least one forensic outcome with findings=true credibly tie the culprit beyond reasonable doubt?
2. Decoy quality — do the other suspects have plausible motives AND verifiable-enough alibis so the player can confidently rule them out using the evidence available?
3. Timeline coherence — every timeline / temporalEvent / forensic conclusion must sit between incidentDate and openedAt (or within the game-time window after openedAt). Spot drift, contradictions, or impossible travel.
4. Motive plausibility — culprit's motive must be specific (not ""anger"" or ""greed""), proportional to the crime, and consistent with the briefing.
5. Reveal chain — every result email/asset linked from a forensic outcome should be reachable via at least one rule when unlockMode is `gated`. (Skip this check for Rookie / all_initial cases.)
6. Question fairness — every question must be answerable strictly from initial assets + the result emails the rules can reveal. No reliance on hidden information.
7. Required evidence — requiredEvidenceIds should be revealable; requiredAnalysisIds must be among the forensic outcomes with findings=true.
8. Tone / content warnings — flag anything gratuitously dark for a Rookie case.

For each issue emit a Finding with severity (low|medium|high), area (1-2 words), a concise issue statement, and an optional suggestion.

Verdict:
- `ok`           — at most low-severity findings.
- `needs_review` — at least one medium-severity finding.
- `reject`       — any high-severity finding that prevents the player from solving fairly.";

        var user = $@"CASE PACKAGE:
{ctxJson}

Emit JSON only.";

        try
        {
            var raw = await _llm.GenerateStructuredResponseAsync(system, user, Schema, ct);
            var report = JsonSerializer.Deserialize<Report>(raw.Content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                         ?? new Report();
            _logger.LogInformation("RedTeam verdict={Verdict} findings={Count} source={Source}", report.Verdict, report.Findings.Count, source);
            return report;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RedTeam failed (source={Source}) — returning empty report", source);
            return new Report { Verdict = "needs_review", Notes = "RedTeam stage failed: " + ex.Message };
        }
    }
}
