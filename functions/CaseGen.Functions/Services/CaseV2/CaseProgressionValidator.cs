using CaseGen.Functions.Models.CaseV2;
using CaseZero.Progression;

namespace CaseGen.Functions.Services.CaseV2;

public static class CaseProgressionValidator
{
    public static void Validate(CaseDraft draft, ConsistencyReport report)
    {
        var reachable = Reachable(draft, report);
        foreach (var id in draft.RequiredEvidenceIds.Where(id => !reachable.Assets.Contains(id)))
            report.Findings.Add(new() { Area = "visibility", Message = $"required evidence '{id}' is unreachable from the initial investigation" });
        foreach (var id in draft.RequiredAnalysisIds)
        {
            var parts = id.Split(':', 2);
            if (parts.Length != 2 || !CanAnalyze(draft, reachable.Assets, parts[0], parts[1]))
                report.Findings.Add(new() { Area = "forensics", Message = $"required analysis '{id}' is unreachable or incompatible" });
        }
    }

    public sealed record Reachability(HashSet<string> Assets, HashSet<string> Emails);

    public static Reachability Reachable(CaseDraft draft, ConsistencyReport report)
    {
        var assets = draft.AssetFull.Concat(draft.ResultAssets).ToList();
        var emails = draft.FollowUpEmails.Concat(draft.ResultEmails).ToList();
        var allInitial = string.Equals(draft.Metadata.RequiredRank, "Rookie", StringComparison.OrdinalIgnoreCase);
        var visibleAssets = assets.Where(a => allInitial || a.Visibility == "initial").Select(a => a.Id).ToHashSet();
        var visibleEmails = emails.Where(e => allInitial || e.Visibility == "initial").Select(e => e.Id).Append("email.briefing").ToHashSet();
        var visibleSuspects = draft.SuspectFull.Select(s => s.Id).ToHashSet();
        var attachments = emails.ToDictionary(e => e.Id, e => e.Attachments.ToHashSet());
        var fired = new HashSet<string>();
        bool Analysis(string? assetId, string? type) => CanAnalyze(draft, visibleAssets, assetId, type);
        bool Leaf(RulesTrigger t) => t.Type switch
        {
            "asset_viewed" => visibleAssets.Contains(t.AssetId ?? "") || visibleAssets.Contains(t.InputAssetId ?? ""),
            "email_opened" => visibleEmails.Contains(t.EmailId ?? ""),
            "suspect_viewed" => visibleSuspects.Contains(t.SuspectId ?? ""),
            "forensics_complete" => Analysis(t.InputAssetId, t.AnalysisType),
            "time_elapsed" => t.AtMinutes is >= 0,
            "attachment_download" => attachments.Any(e => visibleEmails.Contains(e.Key) &&
                (t.EmailId is null || t.EmailId == e.Key) && e.Value.Any(a => t.AssetId is null || t.AssetId == a)),
            _ => false
        };
        bool Apply(RulesAction a) => a.Type switch
        {
            "reveal_asset" => a.AssetId is not null && visibleAssets.Add(a.AssetId),
            "reveal_email" => a.EmailId is not null && visibleEmails.Add(a.EmailId),
            "reveal_suspect" => a.SuspectId is not null && visibleSuspects.Add(a.SuspectId),
            "add_email_attachment" => AddAttachment(a),
            _ => false
        };
        bool AddAttachment(RulesAction a)
        {
            if (a.EmailId is null || a.AssetId is null) return false;
            if (!attachments.TryGetValue(a.EmailId, out var list)) attachments[a.EmailId] = list = new();
            return list.Add(a.AssetId);
        }
        bool changed;
        do
        {
            changed = false;
            foreach (var item in draft.TemporalEvents.Where(t => t.TriggerAtMinutes >= 0))
            {
                if (item.Payload?.AssetId is { } asset) changed |= visibleAssets.Add(asset);
                if (item.Payload?.EmailId is { } email) changed |= visibleEmails.Add(email);
            }
            foreach (var email in attachments.Where(e => visibleEmails.Contains(e.Key)))
                foreach (var asset in email.Value) changed |= visibleAssets.Add(asset);
            foreach (var outcome in draft.ForensicFull.Where(o => o.Findings && Analysis(o.InputAssetId, o.AnalysisType)))
            {
                if (outcome.ResultAssetId is { } asset) changed |= visibleAssets.Add(asset);
                if (outcome.ResultEmailId is { } email) changed |= visibleEmails.Add(email);
            }
            foreach (var rule in draft.Rules.Where(r => !fired.Contains(r.RuleId)))
            {
                try
                {
                    if (!RuleConditions.Satisfied(rule.Trigger, Leaf)) continue;
                }
                catch (InvalidDataException ex)
                {
                    report.Findings.Add(new() { Area = "rules", Message = $"rule {rule.RuleId}: {ex.Message}" });
                    fired.Add(rule.RuleId);
                    continue;
                }
                fired.Add(rule.RuleId);
                foreach (var action in rule.Actions) changed |= Apply(action);
            }
        } while (changed);
        return new(visibleAssets, visibleEmails);
    }

    private static bool CanAnalyze(CaseDraft draft, HashSet<string> visible, string? assetId, string? type) =>
        visible.Contains(assetId ?? "") && draft.AssetFull.Concat(draft.ResultAssets).Any(a => a.Id == assetId &&
            draft.AnalysisTypes.Any(t => string.Equals(t.Type, type, StringComparison.OrdinalIgnoreCase) &&
                t.DurationMinutes > 0 && t.AvailableFor.Contains(a.Type)));
}
