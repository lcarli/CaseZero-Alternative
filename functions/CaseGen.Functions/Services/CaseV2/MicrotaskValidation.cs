using CaseGen.Functions.Models.CaseV2;
using static CaseGen.Functions.Services.CaseV2.TaskContractException;

namespace CaseGen.Functions.Services.CaseV2;

public static class MicrotaskValidation
{
    public static void Unique(IEnumerable<string> ids, string field)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids) Require(seen.Add(id), $"{field} contains duplicate ID '{id}'.");
    }

    public static void Equal(string actual, string expected, string field) =>
        Require(actual == expected, $"{field} must echo '{expected}', not '{actual}'.");

    public static HashSet<string> AssetIds(CaseDraft draft) => draft.AssetStubs.Select(a => a.Id)
        .Concat(draft.AssetFull.Select(a => a.Id)).Concat(draft.ResultAssets.Select(a => a.Id)).ToHashSet(StringComparer.Ordinal);

    public static HashSet<string> EmailIds(CaseDraft draft) => draft.FollowUpEmails.Select(e => e.Id)
        .Concat(draft.ResultEmails.Select(e => e.Id)).Append("email.briefing").ToHashSet(StringComparer.Ordinal);

    public static void Asset(EvidenceAsset asset)
    {
        if (asset.Type is "pdf" or "document" or "digital")
        {
            Require(asset.BodyDoc is not null || !string.IsNullOrWhiteSpace(asset.Body), "A document asset must contain bodyDoc or a nonblank body.");
        }
        else
        {
            Require(!string.IsNullOrWhiteSpace(asset.Body), "Photo/audio assets must contain a nonblank body.");
            Require(asset.BodyDoc is null, "Photo/audio assets must not contain bodyDoc.");
        }
        if (asset.BodyDoc is null) return;
        Require(asset.BodyDoc.Sections.Count > 0, "A bodyDoc must contain substantive readable sections.");
        foreach (var section in asset.BodyDoc.Sections)
            Require(section.Kind switch
            {
                "table" => section.Table is { Columns.Count: > 0, Rows.Count: > 0 },
                "transcript" => section.Transcript is { Count: > 0 } && section.Transcript.All(t => !string.IsNullOrWhiteSpace(t.Text)),
                "keyValue" => section.Items is { Count: > 0 } && section.Items.All(i => !string.IsNullOrWhiteSpace(i.Value)),
                "code" => !string.IsNullOrWhiteSpace(section.Code?.Content),
                "narrative" or "callout" => !string.IsNullOrWhiteSpace(section.Text),
                _ => false
            }, $"bodyDoc section '{section.Kind}' is empty or unsupported.");
        foreach (var section in asset.BodyDoc.Sections.Where(s => s.Kind == "table"))
        {
            var table = section.Table!;
            Require(table.Rows.All(row => row.Count == table.Columns.Count),
                "bodyDoc table rows must have exactly the same number of cells as columns.");
        }
    }

    public static void Question(SolutionQuestion question, QuestionTopic topic)
    {
        Equal(question.Id, topic.Id, "question.id");
        Require(question.Weight == topic.Weight, "Question weight must echo its assigned topic.");
        Unique(question.Options.Select(o => o.Id), "question.options");
        Unique(question.Options.Select(o => o.Label), "question option labels");
        Require(question.Options.Count is >= 3 and <= 4 && question.Options.Any(o => o.Id == question.CorrectOptionId),
            "Question correctOptionId must identify one of its unique options.");
    }

    public static void Email(EvidenceEmail email, CaseDraft draft)
    {
        var assets = AssetIds(draft);
        foreach (var id in email.Attachments)
            Require(assets.Contains(id), $"Email attachment '{id}' does not exist.");
    }

    public static void Rules(IEnumerable<RulesRule> rules, CaseDraft draft)
    {
        var ruleList = rules.ToList();
        Unique(ruleList.Select(r => r.RuleId).Concat(draft.Rules.Select(r => r.RuleId)), "rules");
        var assets = AssetIds(draft);
        var emails = EmailIds(draft);
        var suspects = draft.SuspectStubs.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var rule in ruleList)
        {
            Trigger(rule.Trigger, draft, assets, emails, suspects, 1);
            foreach (var action in rule.Actions)
            {
                CheckReference(action.AssetId, assets, "action.assetId");
                CheckReference(action.EmailId, emails, "action.emailId");
                CheckReference(action.SuspectId, suspects, "action.suspectId");
                Require(action.Type switch
                {
                    "reveal_email" => action.EmailId is not null,
                    "reveal_asset" => action.AssetId is not null,
                    "reveal_suspect" => action.SuspectId is not null,
                    "add_email_attachment" => action.EmailId is not null && action.AssetId is not null,
                    "send_notification" => !string.IsNullOrWhiteSpace(action.Message) && action.Level is not null,
                    "update_suspect_status" => action.SuspectId is not null && action.Status is not null,
                    "mark_alibi_verified" => action.SuspectId is not null && action.Verified.HasValue,
                    _ => false
                }, $"Action '{action.Type}' is missing its required fields.");
            }
        }
    }

    private static void Trigger(RulesTrigger trigger, CaseDraft draft, HashSet<string> assets,
        HashSet<string> emails, HashSet<string> suspects, int depth)
    {
        Require(depth <= 3, "multiple_conditions nesting exceeds three trigger levels.");
        CheckReference(trigger.InputAssetId, assets, "trigger.inputAssetId");
        CheckReference(trigger.AssetId, assets, "trigger.assetId");
        CheckReference(trigger.EmailId, emails, "trigger.emailId");
        CheckReference(trigger.SuspectId, suspects, "trigger.suspectId");
        Require(trigger.Type switch
        {
            "forensics_complete" => trigger.InputAssetId is not null && trigger.AnalysisType is not null,
            "attachment_download" => trigger.EmailId is not null && trigger.AssetId is not null,
            "asset_viewed" => trigger.AssetId is not null,
            "email_opened" => trigger.EmailId is not null,
            "time_elapsed" => trigger.AtMinutes.HasValue,
            "suspect_viewed" => trigger.SuspectId is not null,
            "multiple_conditions" => trigger.Operator is not null && trigger.Conditions is { Count: >= 2 },
            _ => false
        }, $"Trigger '{trigger.Type}' is missing its required fields.");
        if (trigger.Type == "forensics_complete")
        {
            var asset = draft.AssetFull.Concat(draft.ResultAssets).Single(a => a.Id == trigger.InputAssetId);
            Require(draft.AnalysisTypes.Any(t => t.Type == trigger.AnalysisType && t.AvailableFor.Contains(asset.Type)),
                $"Trigger analysis '{trigger.AnalysisType}' is not available for '{trigger.InputAssetId}'.");
        }
        if (trigger.Type == "multiple_conditions")
        {
            foreach (var child in trigger.Conditions!) Trigger(child, draft, assets, emails, suspects, depth + 1);
        }
        else
            Require(trigger.Conditions is null && trigger.Operator is null,
                "Only multiple_conditions may contain conditions/operator.");
    }

    private static void CheckReference(string? id, HashSet<string> known, string field)
    {
        if (id is not null) Require(known.Contains(id), $"{field} references unknown ID '{id}'.");
    }
}
