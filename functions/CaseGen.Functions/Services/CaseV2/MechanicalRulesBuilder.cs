using CaseGen.Functions.Models.CaseV2;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.CaseV2;

/// <summary>
/// Pre-builds the mechanical rules every case MUST have, in C#, before the
/// LLM-driven <see cref="Tasks.RulesTask"/> runs. The LLM is then asked only for
/// narrative rules (notifications, alibi nudges, multi-condition beats), so it
/// can't drop a reveal by accident.
///
/// The rules generated here mirror exactly what <see cref="ConsistencyValidator"/>
/// would auto-fix later — but doing it up-front means the RulesTask, SolverTask
/// and RedTeamTask all see the canonical reveal chain immediately.
/// </summary>
public class MechanicalRulesBuilder
{
    private readonly ILogger<MechanicalRulesBuilder> _logger;

    public MechanicalRulesBuilder(ILogger<MechanicalRulesBuilder> logger) { _logger = logger; }

    public void Build(CaseDraft draft)
    {
        var built = 0;
        foreach (var f in draft.ForensicFull.Where(x => x.Findings && (x.ResultEmailId is not null || x.ResultAssetId is not null)))
        {
            var slug = Slug($"{f.InputAssetId}_{f.AnalysisType}");
            var rule = new RulesRule
            {
                RuleId = $"rule.reveal_{slug}",
                Description = $"Reveal forensic outcome of {f.InputAssetId}:{f.AnalysisType}",
                Trigger = new RulesTrigger
                {
                    Type = "forensics_complete",
                    InputAssetId = f.InputAssetId,
                    AnalysisType = f.AnalysisType
                },
                Actions = new List<RulesAction>()
            };
            if (f.ResultEmailId is not null)
                rule.Actions.Add(new RulesAction { Type = "reveal_email", EmailId = f.ResultEmailId });
            if (f.ResultAssetId is not null)
                rule.Actions.Add(new RulesAction { Type = "reveal_asset", AssetId = f.ResultAssetId });

            draft.Rules.Add(rule);
            built++;
        }
        _logger.LogInformation("MechanicalRulesBuilder pre-populated {N} reveal rule(s) for case {CaseId}", built, draft.CaseId);
    }

    private static string Slug(string raw)
    {
        var chars = raw.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
        return new string(chars).Trim('_');
    }
}
