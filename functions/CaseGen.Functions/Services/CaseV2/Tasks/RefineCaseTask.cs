using System.Text.Json;
using CaseGen.Functions.Models.CaseV2;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.CaseV2.Tasks;

/// <summary>
/// Stage 12 — Refine. When schema validation still has errors after the deterministic
/// auto-fixer ran, OR when the red-team agent verdict was <c>reject</c> with at least one
/// high-severity finding, this task asks the LLM to emit a corrected JSON document.
///
/// Strategy: send the full case.json (which is small — ~30-40 KB), the error list, and
/// any high-severity findings. The model must return JSON that satisfies the v2 schema
/// AND preserves the case content as much as possible. We only call this once per
/// generation — repeated refines are expensive and rarely converge.
/// </summary>
public class RefineCaseTask
{
    private readonly ILLMProvider _llm;
    private readonly ILogger _logger;

    public RefineCaseTask(ILLMProvider llm, ILogger logger)
    {
        _llm = llm;
        _logger = logger;
    }

    public class Result
    {
        public string? RefinedJson { get; set; }
        public bool Succeeded => !string.IsNullOrEmpty(RefinedJson);
        public string? Error { get; set; }
    }

    public async Task<Result> RunAsync(
        string currentJson,
        IReadOnlyList<string> validationErrors,
        IReadOnlyList<RedTeamTask.Finding> findings,
        string schemaJson,
        CancellationToken ct,
        string? difficulty = null,
        CaseBible? caseBible = null)
    {
        var errorBlock = validationErrors.Count == 0
            ? "(no schema errors)"
            : string.Join("\n", validationErrors.Select(e => "- " + e));

        var highFindings = findings.Where(f =>
            string.Equals(f.Severity, "high", StringComparison.OrdinalIgnoreCase)).ToList();
        var mediumFindings = findings.Where(f =>
            string.Equals(f.Severity, "medium", StringComparison.OrdinalIgnoreCase)).ToList();

        var highBlock = highFindings.Count == 0
            ? "(no high-severity red-team findings)"
            : string.Join("\n", highFindings.Select(f =>
                $"- area={f.Area} severity={f.Severity} issue={f.Issue}" +
                (string.IsNullOrEmpty(f.Suggestion) ? "" : $" suggestion={f.Suggestion}") +
                (string.IsNullOrEmpty(f.OwnerStage) ? "" : $" ownerStage={f.OwnerStage}") +
                (string.IsNullOrEmpty(f.RepairAction) ? "" : $" repairAction={f.RepairAction}") +
                (f.AffectedPaths.Count == 0 ? "" : $" affectedPaths={string.Join(",", f.AffectedPaths)}")));

        var mediumBlock = mediumFindings.Count == 0
            ? "(no medium-severity red-team findings)"
            : string.Join("\n", mediumFindings.Select(f =>
                $"- area={f.Area} severity={f.Severity} issue={f.Issue}" +
                (string.IsNullOrEmpty(f.Suggestion) ? "" : $" suggestion={f.Suggestion}") +
                (string.IsNullOrEmpty(f.OwnerStage) ? "" : $" ownerStage={f.OwnerStage}") +
                (string.IsNullOrEmpty(f.RepairAction) ? "" : $" repairAction={f.RepairAction}") +
                (f.AffectedPaths.Count == 0 ? "" : $" affectedPaths={string.Join(",", f.AffectedPaths)}")));

        var flaggedAreas = findings
            .Where(finding => !string.Equals(finding.Severity, "low", StringComparison.OrdinalIgnoreCase))
            .Select(finding => NormalizeArea(finding.Area))
            .Where(area => !string.IsNullOrWhiteSpace(area))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        var isRookie = string.Equals(difficulty, "Rookie", StringComparison.OrdinalIgnoreCase);
        var prompts = AgentPromptCatalog.Default;
        var system = prompts.RenderSystem("RefineCase", new Dictionary<string, object?>
        {
            ["is_rookie"] = isRookie.ToString().ToLowerInvariant(),
            ["flagged_areas"] = flaggedAreas.Count == 0 ? "(none)" : string.Join(", ", flaggedAreas)
        });
        var user = prompts.RenderUser("RefineCase", new Dictionary<string, object?>
        {
            ["current_json"] = currentJson,
            ["case_bible_json"] = caseBible is null ? "(not supplied)" : JsonSerializer.Serialize(caseBible),
            ["validation_errors"] = errorBlock,
            ["high_findings"] = highBlock,
            ["medium_findings"] = mediumBlock
        });

        try
        {
            var raw = await _llm.GenerateStructuredResponseAsync(system, user, schemaJson, ct);
            // Trust the structured-response API to return JSON; do a parse round-trip as
            // a sanity check before handing it back up the pipeline.
            using var doc = JsonDocument.Parse(raw.Content);
            _logger.LogInformation("RefineCaseTask emitted {Bytes} bytes of refined JSON", raw.Content.Length);
            return new Result { RefinedJson = raw.Content };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RefineCaseTask failed");
            return new Result { Error = ex.Message };
        }
    }

    private static string NormalizeArea(string? area)
    {
        if (string.IsNullOrWhiteSpace(area)) return string.Empty;
        // Red-team uses both "Smoking-gun"/"SmokingGun" and "Required-evidence"/"RequiredEvidence".
        return new string(area.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
    }
}
