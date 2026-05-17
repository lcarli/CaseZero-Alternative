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
        CancellationToken ct)
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
                (string.IsNullOrEmpty(f.Suggestion) ? "" : $" suggestion={f.Suggestion}")));

        var mediumBlock = mediumFindings.Count == 0
            ? "(no medium-severity red-team findings)"
            : string.Join("\n", mediumFindings.Select(f =>
                $"- area={f.Area} severity={f.Severity} issue={f.Issue}" +
                (string.IsNullOrEmpty(f.Suggestion) ? "" : $" suggestion={f.Suggestion}")));

        var system = @"You are the **refine agent** for an interactive detective case. You receive a case.json document that failed validation or red-team review, plus the list of problems. Your job is to emit a corrected case.json that:

1. Satisfies every JSON-Schema constraint listed in the supplied schema (especially id-pattern constraints like `^asset\.[a-z0-9_]+$`, `^tevt\.[a-z0-9_]+$`, etc.).
2. Addresses every high-severity red-team finding listed.
3. Also addresses medium-severity findings whenever doing so does not require restructuring the case (e.g. sharpening a motive, tightening an alibi, clarifying a forensic conclusion).
4. Preserves the rest of the case as faithfully as possible — DO NOT rewrite plot, suspects, timeline, motive, evidence, or solution unless explicitly required to fix a flagged issue. Keep IDs stable when fixing referential issues; if an ID must change, also update every reference to it.
5. Emit ONLY the corrected JSON document — no commentary, no markdown fences. The top-level must be an object matching the v2 case schema.

Be surgical. If the only complaint is malformed IDs, just fix the IDs (and their references). If a finding says ""smoking gun too weak"", you may add a sentence to the relevant ConclusionText or sharpen a forensic outcome, but don't restructure the case.";

        var user = $@"=== CURRENT case.json ===
{currentJson}

=== VALIDATION ERRORS ===
{errorBlock}

=== HIGH-SEVERITY RED-TEAM FINDINGS ===
{highBlock}

=== MEDIUM-SEVERITY RED-TEAM FINDINGS ===
{mediumBlock}

Emit the corrected case.json as a single JSON object.";

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
}
