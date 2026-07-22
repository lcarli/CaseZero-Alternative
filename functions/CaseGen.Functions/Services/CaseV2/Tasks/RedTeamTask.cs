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
        [JsonPropertyName("affectedPaths")] public List<string> AffectedPaths { get; set; } = new();
        [JsonPropertyName("factRefs")] public List<string> FactRefs { get; set; } = new();
        [JsonPropertyName("expected")] public string? Expected { get; set; }
        [JsonPropertyName("ownerStage")] public string? OwnerStage { get; set; }
        [JsonPropertyName("repairAction")] public string? RepairAction { get; set; }
        [JsonPropertyName("invalidateDownstream")] public List<string> InvalidateDownstream { get; set; } = new();
        [JsonPropertyName("nodeId")] public string? NodeId { get; set; }
        [JsonPropertyName("constraintId")] public string? ConstraintId { get; set; }
        [JsonIgnore] public bool Blocking { get; set; }
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
        "findings":{"type":"array","items":{"type":"object","required":["severity","area","issue","affectedPaths","ownerStage","repairAction"],
          "properties":{
            "severity":{"type":"string","enum":["low","medium","high"]},
            "area":{"type":"string"},
            "issue":{"type":"string"},
            "suggestion":{"type":["string","null"]},
            "affectedPaths":{"type":"array","items":{"type":"string"}},
            "factRefs":{"type":"array","items":{"type":"string"}},
            "expected":{"type":["string","null"]},
            "ownerStage":{"type":["string","null"],"enum":["plotOutline","suspectCards","assetPlan","assetCards","timeline","forensics","emails","rules","solution",null]},
            "repairAction":{"type":["string","null"]},
            "invalidateDownstream":{"type":"array","items":{"type":"string"}}
          }}},
        "notes":{"type":["string","null"]}
      }
    }
    """;

    public async Task<Report> RunAsync(CaseDraft draft, string assembledJson, CancellationToken ct)
    {
        return await RunSpecialistsAsync(draft, ct);
    }

    /// <summary>
    /// Re-runs the red-team audit against an already-assembled case.json string
    /// (typically produced by RefineCaseTask). Used after refine to verify that
    /// the refined JSON actually addressed the original findings.
    /// </summary>
    public async Task<Report> RunOnAssembledAsync(string assembledJson, CaseDraft draft, CancellationToken ct)
    {
        return await RunSpecialistsAsync(draft, ct);
    }

    private async Task<Report> RunSpecialistsAsync(CaseDraft draft, CancellationToken ct)
    {
        var reports = await new SpecialistReviewCoordinator(_llm, _logger).RunAsync(draft, ct);
        var findings = reports.SelectMany(report => report.Findings)
            .Select(finding => new Finding
            {
                Severity = finding.Severity.ToString().ToLowerInvariant(),
                Area = finding.Reviewer.ToString(),
                Issue = finding.Message,
                Suggestion = finding.SuggestedRepair,
                AffectedPaths = { finding.OwningNodeId },
                FactRefs = { finding.OwningNodeId },
                Expected = finding.ConstraintId,
                OwnerStage = OwnerStageFor(finding.OwningNodeId),
                RepairAction = finding.ConstraintId,
                NodeId = finding.OwningNodeId,
                ConstraintId = finding.ConstraintId,
                Blocking = finding.Deterministic && finding.Severity == ReviewSeverity.High
            })
            .ToList();
        var verdict = findings.Any(finding => finding.Blocking)
            ? "reject"
            : findings.Any(finding => finding.Severity == "medium")
                ? "needs_review"
                : "ok";
        return new Report
        {
            Verdict = verdict,
            Findings = findings,
            Notes = string.Join(", ", reports.Select(report => $"{report.Reviewer}:{report.Verdict}"))
        };
    }

    private static string OwnerStageFor(string nodeId) =>
        nodeId switch
        {
            var id when id.StartsWith("asset.", StringComparison.Ordinal) => "assetCards",
            var id when id.StartsWith("observation.", StringComparison.Ordinal) => "assetCards",
            var id when id.StartsWith("forensic.", StringComparison.Ordinal) => "forensics",
            var id when id.StartsWith("question.", StringComparison.Ordinal) => "solution",
            var id when id.StartsWith("event.", StringComparison.Ordinal) => "timeline",
            var id when id.StartsWith("locale.", StringComparison.Ordinal) => "plotOutline",
            var id when id.StartsWith("suspect.", StringComparison.Ordinal) => "suspectCards",
            _ => "plotOutline"
        };

    private static string? TryExtractDifficulty(string assembledJson)
    {
        try
        {
            var node = JsonNode.Parse(assembledJson);
            return node?["metadata"]?["difficulty"]?.GetValue<string>();
        }
        catch
        {
            return null;
        }
    }

    private static string BuildCompactContextFromJson(string assembledJson, CaseDraft? draft = null)
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
            var forensicsDefaults = node["forensicsDefaults"];
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
            var draftAssets = draft?.AssetFull.Concat(draft.ResultAssets)
                .GroupBy(asset => asset.Id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            if (assetsArr is not null)
            {
                foreach (var a in assetsArr)
                {
                    if (a is null) continue;
                    EvidenceAsset? draftAsset = null;
                    draftAssets?.TryGetValue(a["id"]?.GetValue<string>() ?? string.Empty, out draftAsset);
                    var renderedContent = string.Join("\n", new[]
                    {
                        draftAsset?.Body,
                        draftAsset?.BodyDoc is null ? null : JsonSerializer.Serialize(draftAsset.BodyDoc),
                        draftAsset?.Description
                    }.Where(value => !string.IsNullOrWhiteSpace(value)));
                    compactAssets.Add(new JsonObject
                    {
                        ["id"] = a["id"]?.GetValue<string>(),
                        ["type"] = a["type"]?.GetValue<string>(),
                        ["title"] = a["title"]?.GetValue<string>(),
                        ["description"] = a["description"]?.GetValue<string>(),
                        ["renderedContent"] = renderedContent,
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
                    compactRules.Add(new JsonObject
                    {
                        ["ruleId"] = r["ruleId"]?.GetValue<string>(),
                        ["description"] = r["description"]?.GetValue<string>(),
                        ["trigger"] = r["trigger"]?.DeepClone(),
                        ["actions"] = r["actions"]?.DeepClone()
                    });
                }
            }

            var compactEmails = new JsonArray();
            if (emailsArr is not null)
            {
                foreach (var e in emailsArr)
                {
                    if (e is null) continue;
                    compactEmails.Add(new JsonObject
                    {
                        ["id"] = e["id"]?.GetValue<string>(),
                        ["from"] = e["from"]?.GetValue<string>(),
                        ["subject"] = e["subject"]?.GetValue<string>(),
                        ["body"] = e["body"]?.GetValue<string>(),
                        ["attachments"] = e["attachments"]?.DeepClone(),
                        ["visibility"] = e["visibility"]?.GetValue<string>()
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
                ["emails"] = compactEmails,
                ["timeline"] = timeline?.DeepClone(),
                ["temporalEvents"] = temporal?.DeepClone(),
                ["forensics"] = compactForensics,
                ["forensicsDefaults"] = forensicsDefaults?.DeepClone(),
                ["rules"] = compactRules,
                ["solution"] = solution?.DeepClone()
            };

            return compact.ToJsonString();
        }
        catch
        {
            // If anything goes sideways, fall back to passing the raw JSON.
            return assembledJson;
        }
    }

    private async Task<Report> ExecuteAsync(string ctxJson, string source, string? difficulty, CancellationToken ct)
    {
        var isRookie = string.Equals(difficulty, "Rookie", StringComparison.OrdinalIgnoreCase);

        var rookieGuidance = @"

This is a **Rookie** case — by design it must be SIMPLE, SHORT, and FAIR for novice players.
Apply the rubric with that in mind:
- Compact motives (1-2 sentences) are appropriate — do NOT flag brevity or lack of backstory depth.
- 2-3 decoys (not 4+) is fine; do NOT flag ""too few suspects"" or ""decoys lack richness"".
- Rookie MUST have zero forensic analysis types, zero forensic outcomes, zero result assets/emails, and zero requiredAnalysisIds.
- Initial digital records such as chat exports, access logs, call logs, e-mails, browser history, or transaction exports are ordinary evidence and are fully allowed in Rookie. Do NOT call them a forensic dependency when no later analysis/result is required.
- Police evidence logs, seizure forms, custody records, scene photographs, and faithful transcripts of a source record are also ordinary initial evidence. They are not a forensic workflow unless the player must request a later analysis or wait for a result.
- All evidence needed to solve the case must be initial and understandable without a lab workflow.
- A short timeline with a direct corroboration chain is the norm — do NOT request extra complexity.
- Two or more consistent initial records that jointly identify the culprit are intentional Rookie design, not truth leakage. Flag only a single asset that explicitly declares guilt/private truth, a contradiction, or reliance on absent evidence.
- A consolidated evidence log, dispatch record, or access register is valid proof when it contains the exact underlying timestamped entries; do not demand a separate asset merely because several checks share one well-structured document.
- A faithful player-visible transcription or excerpt with provenance is answerable evidence; do not require the original standalone file unless the case asks the player to inspect visual/physical properties absent from the transcription.
- `metadata.incidentDate` is an estimated anchor for uncertain-window crimes. It need not equal a discrete timeline event. Flag it only when it falls outside the player-supported opportunity window or is presented elsewhere as an exact observed time.
- Procedural language about preserving evidence is not a forensic dependency unless the solution, question, required IDs, or reveal graph actually requires a later result.
- Use `high` for any forensic dependency, private-truth leakage, contradictory chronology, or question requiring unavailable facts.";

        var system = @"You are the **red-team reviewer** for an interactive detective case. Audit the supplied case package for semantic flaws that would make the case unsolvable, unfair, or implausible. Be ruthless and concise.

Look specifically for:
1. Evidence-chain strength — do at least two independent player-visible sources support the culprit? For non-Rookie cases, forensic outcomes may participate but must state limitations.
2. Decoy quality — do the other suspects have plausible motives AND verifiable-enough alibis so the player can confidently rule them out using the evidence available?
3. Timeline coherence — compare every repeated timestamp across metadata, timeline, assets, emails, questions, and outcomes. Flag hour shifts, future claims, timezone mismatch, contradictions, or impossible travel.
4. Motive plausibility — culprit's motive must be specific (not ""anger"" or ""greed""), proportional to the crime, and consistent with the briefing.
5. Reveal chain — every result email/asset linked from a forensic outcome should be reachable via at least one rule when unlockMode is `gated`. (Skip this check for Rookie / all_initial cases.)
   The runtime itself lets the player request any analysis declared in `forensicsDefaults.analysisTypes`; a matching `forensics_complete` rule is the valid trigger that reveals its result. Do not demand a separate `run_forensics` rule.
   A hidden required result is intentionally gated. Do NOT flag it merely because the player must choose the declared analysis; flag only when the input/type is unavailable, scientifically incompatible, or the matching reveal rule/result is missing.
6. Question fairness — every question must be answerable strictly from initial assets + the result emails the rules can reveal. No reliance on hidden information.
7. Required evidence — requiredEvidenceIds should be revealable; requiredAnalysisIds must be among the forensic outcomes with findings=true.
8. Tone / content warnings — flag anything gratuitously dark for a Rookie case.
9. Truth leakage — the public timeline, suspect profiles, briefing, asset descriptions, and notifications must not directly identify the culprit, narrate the hidden crime, or explicitly clear every decoy.
   A gated forensic result may objectively name a matched account, device, fingerprint, or author when the method supports it; that is evidence, not leakage. It must not declare the matched person guilty.
10. Locale realism — names, agency, investigator identity, timezone, email domains, and terminology must fit the requested location/language.
11. Runtime completeness — every temporal event needs a useful payload and every required item must exist in the player-reachable graph.

For each issue emit a Finding with severity, area, concise issue, optional suggestion, exact `affectedPaths`,
relevant `factRefs`, the expected state, `ownerStage`, a machine-oriented `repairAction`, and
`invalidateDownstream` listing stages that must rerun after repair.
`ownerStage` MUST be the earliest stage that owns the defective fact:
- `plotOutline` for crime mechanism, motive/objective, canonical timestamps, locale, clue wording, or red-herring truth;
- `suspectCards`, `assetPlan`, `assetCards`, `timeline`, `forensics`, `emails`, `rules`, or `solution` for defects introduced there.
Do not route canonical-fact defects to a downstream document merely because that is where they became visible.

Verdict:
- `ok`           — at most low-severity findings.
- `needs_review` — at least one medium-severity finding.
- `reject`       — any high-severity finding that prevents the player from solving fairly." + (isRookie ? rookieGuidance : string.Empty);

        var user = $@"CASE PACKAGE:
{ctxJson}

Emit JSON only.";

        try
        {
            var raw = await _llm.GenerateStructuredResponseAsync(system, user, Schema, ct);
            var report = JsonSerializer.Deserialize<Report>(raw.Content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                         ?? new Report();
            _logger.LogInformation("RedTeam verdict={Verdict} findings={Count} source={Source} difficulty={Difficulty}",
                report.Verdict, report.Findings.Count, source, difficulty ?? "(unknown)");
            // Surface each finding (area + severity + truncated issue) so future incident
            // analysis doesn't require rerunning the audit.
            for (int i = 0; i < report.Findings.Count; i++)
            {
                var f = report.Findings[i];
                var issue = f.Issue ?? string.Empty;
                if (issue.Length > 240) issue = issue.Substring(0, 240) + "…";
                _logger.LogInformation("RedTeam finding #{Index} severity={Severity} area={Area} issue={Issue}",
                    i + 1, f.Severity, f.Area, issue);
            }
            return report;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RedTeam failed (source={Source}) — returning empty report", source);
            return new Report { Verdict = "needs_review", Notes = "RedTeam stage failed: " + ex.Message };
        }
    }
}
