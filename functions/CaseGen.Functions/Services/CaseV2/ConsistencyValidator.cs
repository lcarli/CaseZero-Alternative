using System.Globalization;
using System.Text.Json.Nodes;
using CaseGen.Functions.Models.CaseV2;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.CaseV2;

public interface IConsistencyValidator
{
    /// <summary>
    /// Deterministic post-assemble pass: catches structural / semantic problems the
    /// JSON schema can't (orphan refs, hidden required evidence without a reveal,
    /// weights ≠ 1.0, missing rules for forensic reveals, timeline drift, etc.).
    /// Returns a list of <see cref="ConsistencyFinding"/> describing what was fixed
    /// and what is left as a hard error.
    /// </summary>
    ConsistencyReport Validate(CaseDraft draft);
}

public class ConsistencyReport
{
    public List<ConsistencyFinding> Findings { get; } = new();
    public List<string> Errors => Findings.Where(f => f.Severity == "error").Select(f => f.Message).ToList();
    public List<string> AutoFixes => Findings.Where(f => f.Severity == "fixed").Select(f => f.Message).ToList();
    public List<string> Warnings => Findings.Where(f => f.Severity == "warning").Select(f => f.Message).ToList();
    public bool HasErrors => Findings.Any(f => f.Severity == "error");
}

public class ConsistencyFinding
{
    public string Severity { get; set; } = "error"; // error | warning | fixed
    public string Area { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// All-the-rules deterministic validator. Mutates <see cref="CaseDraft"/> in place
/// when auto-fixing is safe (eg. add a forensics_complete reveal rule the LLM forgot)
/// and reports each fix. Returns errors only for problems that need re-generation.
/// </summary>
public class ConsistencyValidator : IConsistencyValidator
{
    private readonly ILogger<ConsistencyValidator> _logger;
    public ConsistencyValidator(ILogger<ConsistencyValidator> logger) { _logger = logger; }

    public ConsistencyReport Validate(CaseDraft draft)
    {
        var report = new ConsistencyReport();
        var assetIds = draft.AssetFull.Select(a => a.Id)
            .Concat(draft.ResultAssets.Select(a => a.Id)).ToHashSet(StringComparer.Ordinal);
        var emailIds = draft.FollowUpEmails.Select(e => e.Id)
            .Concat(draft.ResultEmails.Select(e => e.Id))
            .Append("email.briefing").ToHashSet(StringComparer.Ordinal);
        var suspectIds = draft.SuspectFull.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);

        ValidateCulpritExists(draft, suspectIds, report);
        ValidateEvidencePortfolio(draft, report);
        ValidateDifficultyContract(draft, report);
        ValidatePublicTimelineLeakage(draft, report);
        ValidateRequiredEvidence(draft, assetIds, report);
        ValidateRequiredAnalyses(draft, assetIds, report);
        ValidateForensicOutcomes(draft, assetIds, emailIds, suspectIds, report);
        AutoFixMissingForensicsCompleteRules(draft, report);
        DropEmptyRules(draft, report);
        ValidateRulesReferences(draft, assetIds, emailIds, suspectIds, report);
        EnsureRequiredEvidenceRevealable(draft, report);
        ValidateTimeline(draft, report);
        ValidateTemporalEvents(draft, report);
        ValidateQuestions(draft, report);
        // partialCreditRules normalisation happens in the assembler already.

        return report;
    }

    // -----------------------------------------------------------------
    // Checks
    // -----------------------------------------------------------------

    private void ValidateCulpritExists(CaseDraft d, HashSet<string> suspectIds, ConsistencyReport r)
    {
        if (string.IsNullOrEmpty(d.CulpritId) || !suspectIds.Contains(d.CulpritId))
            r.Findings.Add(new() { Severity = "error", Area = "solution", Message = $"culpritId '{d.CulpritId}' not in suspects" });
    }

    private void ValidateEvidencePortfolio(CaseDraft d, ConsistencyReport r)
    {
        var budget = EvidenceArchetypeCatalog.GetBudget(d);
        var knownArchetypes = d.AssetStubs
            .Select(a => EvidenceArchetypeCatalog.Find(a.ArchetypeId))
            .Where(a => a is not null)
            .Cast<EvidenceArchetype>()
            .ToList();

        var familyCount = knownArchetypes.Select(a => a.Family).Distinct(StringComparer.Ordinal).Count();
        if (familyCount < budget.MinFamilies)
            r.Findings.Add(new()
            {
                Severity = "warning",
                Area = "evidence",
                Message = $"evidence portfolio uses only {familyCount} archetype families; {budget.Difficulty} recommends at least {budget.MinFamilies}"
            });

        if (d.AssetStubs.Count < budget.MinAssets || d.AssetStubs.Count > budget.MaxAssets)
            r.Findings.Add(new()
            {
                Severity = "warning",
                Area = "evidence",
                Message = $"evidence portfolio has {d.AssetStubs.Count} initial assets; {budget.Difficulty} expects {budget.MinAssets}-{budget.MaxAssets}"
            });

        var layoutCount = d.AssetStubs.Select(a => a.LayoutHint)
            .Where(layout => !string.IsNullOrWhiteSpace(layout))
            .Distinct(StringComparer.Ordinal)
            .Count();
        var minimumLayouts = Math.Min(budget.MinFamilies, 4);
        if (layoutCount < minimumLayouts)
            r.Findings.Add(new()
            {
                Severity = "warning",
                Area = "evidence",
                Message = $"evidence portfolio uses only {layoutCount} distinct layouts; at least {minimumLayouts} are recommended"
            });

        foreach (var repeated in d.AssetStubs
                     .Where(a => !string.IsNullOrWhiteSpace(a.LayoutHint))
                     .GroupBy(a => a.LayoutHint, StringComparer.Ordinal)
                     .Where(g => g.Count() > 2))
        {
            r.Findings.Add(new()
            {
                Severity = "warning",
                Area = "evidence",
                Message = $"layout '{repeated.Key}' is repeated {repeated.Count()} times"
            });
        }
    }

    private void ValidateDifficultyContract(CaseDraft d, ConsistencyReport r)
    {
        var isRookie = string.Equals(d.Metadata.Difficulty, "Rookie", StringComparison.OrdinalIgnoreCase)
            || string.Equals(d.Metadata.RequiredRank, "Rookie", StringComparison.OrdinalIgnoreCase);
        if (!isRookie) return;

        if (d.AnalysisTypes.Count > 0 || d.ForensicStubs.Count > 0 || d.ForensicFull.Count > 0)
            r.Findings.Add(new() { Severity = "error", Area = "difficulty", Message = "Rookie case contains forensic analysis configuration or outcomes" });
        if (d.ResultAssets.Count > 0 || d.ResultEmails.Count > 0)
            r.Findings.Add(new() { Severity = "error", Area = "difficulty", Message = "Rookie case contains forensic result assets or emails" });
        if (d.RequiredAnalysisIds.Count > 0)
            r.Findings.Add(new() { Severity = "error", Area = "difficulty", Message = "Rookie solution contains requiredAnalysisIds" });
        if (d.Rules.Any(rule => string.Equals(rule.Trigger.Type, "forensics_complete", StringComparison.OrdinalIgnoreCase)))
            r.Findings.Add(new() { Severity = "error", Area = "difficulty", Message = "Rookie case contains forensics_complete rules" });
    }

    private void ValidatePublicTimelineLeakage(CaseDraft d, ConsistencyReport r)
    {
        var culprit = d.SuspectFull.FirstOrDefault(s => s.Id == d.CulpritId);
        if (culprit is null || string.IsNullOrWhiteSpace(culprit.Name)) return;

        var forms = culprit.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Append(culprit.Name)
            .Where(value => value.Length >= 4)
            .ToList();
        foreach (var entry in d.Timeline)
        {
            if (forms.Any(form => entry.Event.Contains(form, StringComparison.OrdinalIgnoreCase)))
            {
                r.Findings.Add(new()
                {
                    Severity = "error",
                    Area = "leakage",
                    Message = $"public timeline event at '{entry.Time}' names the culprit"
                });
            }
        }
    }

    private void ValidateRequiredEvidence(CaseDraft d, HashSet<string> assetIds, ConsistencyReport r)
    {
        for (int i = d.RequiredEvidenceIds.Count - 1; i >= 0; i--)
        {
            var id = d.RequiredEvidenceIds[i];
            if (!assetIds.Contains(id))
            {
                d.RequiredEvidenceIds.RemoveAt(i);
                r.Findings.Add(new() { Severity = "fixed", Area = "solution",
                    Message = $"dropped orphan requiredEvidenceId '{id}' (no matching asset)" });
            }
        }
        if (d.RequiredEvidenceIds.Count == 0)
            r.Findings.Add(new() { Severity = "error", Area = "solution",
                Message = "requiredEvidenceIds is empty after consistency check" });
    }

    private void ValidateRequiredAnalyses(CaseDraft d, HashSet<string> assetIds, ConsistencyReport r)
    {
        var validOutcomes = d.ForensicFull
            .Where(f => f.Findings)
            .Select(f => $"{f.InputAssetId}:{f.AnalysisType}")
            .ToHashSet(StringComparer.Ordinal);
        for (int i = d.RequiredAnalysisIds.Count - 1; i >= 0; i--)
        {
            var id = d.RequiredAnalysisIds[i];
            var parts = id.Split(':', 2);
            if (parts.Length != 2 || !assetIds.Contains(parts[0]) || !validOutcomes.Contains(id))
            {
                d.RequiredAnalysisIds.RemoveAt(i);
                r.Findings.Add(new() { Severity = "fixed", Area = "solution",
                    Message = $"dropped requiredAnalysisId '{id}' (no matching forensic outcome with findings=true)" });
            }
        }
    }

    private void ValidateForensicOutcomes(CaseDraft d, HashSet<string> assets, HashSet<string> emails, HashSet<string> suspects, ConsistencyReport r)
    {
        foreach (var f in d.ForensicFull)
        {
            if (!assets.Contains(f.InputAssetId))
                r.Findings.Add(new() { Severity = "error", Area = "forensics",
                    Message = $"forensic outcome inputAssetId '{f.InputAssetId}' not in assets" });
            if (f.MatchedSuspectId is not null && !suspects.Contains(f.MatchedSuspectId))
                r.Findings.Add(new() { Severity = "warning", Area = "forensics",
                    Message = $"forensic outcome matchedSuspectId '{f.MatchedSuspectId}' not in suspects" });
            if (f.Findings && f.ResultAssetId is null && f.ResultEmailId is null)
                r.Findings.Add(new() { Severity = "warning", Area = "forensics",
                    Message = $"forensic outcome ({f.InputAssetId}:{f.AnalysisType}) has findings=true but no result asset/email" });
            if (f.ResultAssetId is not null && !assets.Contains(f.ResultAssetId))
                r.Findings.Add(new() { Severity = "error", Area = "forensics",
                    Message = $"forensic outcome resultAssetId '{f.ResultAssetId}' not in assets" });
            if (f.ResultEmailId is not null && !emails.Contains(f.ResultEmailId))
                r.Findings.Add(new() { Severity = "error", Area = "forensics",
                    Message = $"forensic outcome resultEmailId '{f.ResultEmailId}' not in emails" });
        }
    }

    /// <summary>
    /// For every <c>forensicOutcome</c> with findings=true and result email/asset,
    /// ensure there is at least one <c>rule</c> with trigger <c>forensics_complete</c>
    /// for that (inputAssetId, analysisType) which reveals the result(s). If missing,
    /// the validator generates one — this is the most common LLM omission.
    /// </summary>
    private void AutoFixMissingForensicsCompleteRules(CaseDraft d, ConsistencyReport r)
    {
        foreach (var f in d.ForensicFull.Where(x => x.Findings && (x.ResultEmailId is not null || x.ResultAssetId is not null)))
        {
            var covered = d.Rules.Any(rule =>
                string.Equals(rule.Trigger.Type, "forensics_complete", StringComparison.OrdinalIgnoreCase) &&
                rule.Trigger.InputAssetId == f.InputAssetId &&
                string.Equals(rule.Trigger.AnalysisType, f.AnalysisType, StringComparison.OrdinalIgnoreCase) &&
                rule.Actions.Any(a =>
                    (a.Type == "reveal_email" && a.EmailId == f.ResultEmailId && f.ResultEmailId is not null) ||
                    (a.Type == "reveal_asset" && a.AssetId == f.ResultAssetId && f.ResultAssetId is not null)));
            if (covered) continue;

            var slug = Slug($"{f.InputAssetId}_{f.AnalysisType}");
            var newRule = new RulesRule
            {
                RuleId = $"rule.auto_reveal_{slug}",
                Description = $"Auto-generated: reveal forensic outcome of {f.InputAssetId}:{f.AnalysisType}",
                Trigger = new RulesTrigger
                {
                    Type = "forensics_complete",
                    InputAssetId = f.InputAssetId,
                    AnalysisType = f.AnalysisType
                },
                Actions = new List<RulesAction>()
            };
            if (f.ResultEmailId is not null)
                newRule.Actions.Add(new RulesAction { Type = "reveal_email", EmailId = f.ResultEmailId });
            if (f.ResultAssetId is not null)
                newRule.Actions.Add(new RulesAction { Type = "reveal_asset", AssetId = f.ResultAssetId });

            d.Rules.Add(newRule);
            r.Findings.Add(new() { Severity = "fixed", Area = "rules",
                Message = $"generated missing rule '{newRule.RuleId}' to reveal forensic outcome of {f.InputAssetId}:{f.AnalysisType}" });
        }
    }

    private void DropEmptyRules(CaseDraft d, ConsistencyReport r)
    {
        for (int i = d.Rules.Count - 1; i >= 0; i--)
        {
            var rule = d.Rules[i];
            // Drop rules whose actions are empty OR whose every action lacks the required field for its type.
            var bogus = rule.Actions.Count == 0 || rule.Actions.All(a => ActionIsIncomplete(a));
            if (bogus)
            {
                d.Rules.RemoveAt(i);
                r.Findings.Add(new() { Severity = "fixed", Area = "rules",
                    Message = $"dropped rule '{rule.RuleId}' (empty or malformed actions)" });
            }
        }
    }

    private static bool ActionIsIncomplete(RulesAction a) => a.Type switch
    {
        "reveal_email" => string.IsNullOrEmpty(a.EmailId),
        "reveal_asset" => string.IsNullOrEmpty(a.AssetId),
        "reveal_suspect" => string.IsNullOrEmpty(a.SuspectId),
        "add_email_attachment" => string.IsNullOrEmpty(a.EmailId) || string.IsNullOrEmpty(a.AssetId),
        "send_notification" => string.IsNullOrEmpty(a.Message),
        "update_suspect_status" => string.IsNullOrEmpty(a.SuspectId) || string.IsNullOrEmpty(a.Status),
        "mark_alibi_verified" => string.IsNullOrEmpty(a.SuspectId),
        _ => true
    };

    private void ValidateRulesReferences(CaseDraft d, HashSet<string> assets, HashSet<string> emails, HashSet<string> suspects, ConsistencyReport r)
    {
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in d.Rules.ToList())
        {
            if (!seenIds.Add(rule.RuleId))
                r.Findings.Add(new() { Severity = "warning", Area = "rules", Message = $"duplicate ruleId '{rule.RuleId}'" });

            var t = rule.Trigger;
            if (t.InputAssetId is not null && !assets.Contains(t.InputAssetId))
                r.Findings.Add(new() { Severity = "error", Area = "rules", Message = $"rule {rule.RuleId} trigger.inputAssetId '{t.InputAssetId}' orphan" });
            if (t.AssetId is not null && !assets.Contains(t.AssetId))
                r.Findings.Add(new() { Severity = "error", Area = "rules", Message = $"rule {rule.RuleId} trigger.assetId '{t.AssetId}' orphan" });
            if (t.EmailId is not null && !emails.Contains(t.EmailId))
                r.Findings.Add(new() { Severity = "error", Area = "rules", Message = $"rule {rule.RuleId} trigger.emailId '{t.EmailId}' orphan" });
            if (t.SuspectId is not null && !suspects.Contains(t.SuspectId))
                r.Findings.Add(new() { Severity = "error", Area = "rules", Message = $"rule {rule.RuleId} trigger.suspectId '{t.SuspectId}' orphan" });

            foreach (var a in rule.Actions)
            {
                if (a.EmailId is not null && !emails.Contains(a.EmailId))
                    r.Findings.Add(new() { Severity = "error", Area = "rules", Message = $"rule {rule.RuleId} action.emailId '{a.EmailId}' orphan" });
                if (a.AssetId is not null && !assets.Contains(a.AssetId))
                    r.Findings.Add(new() { Severity = "error", Area = "rules", Message = $"rule {rule.RuleId} action.assetId '{a.AssetId}' orphan" });
                if (a.SuspectId is not null && !suspects.Contains(a.SuspectId))
                    r.Findings.Add(new() { Severity = "error", Area = "rules", Message = $"rule {rule.RuleId} action.suspectId '{a.SuspectId}' orphan" });
            }
        }
    }

    /// <summary>
    /// Every requiredEvidenceId must be either visibility=initial OR be revealable
    /// by some rule. If neither, auto-promote the asset to 'initial' visibility —
    /// the cheapest fix and never breaks unlock-mode semantics.
    /// </summary>
    private void EnsureRequiredEvidenceRevealable(CaseDraft d, ConsistencyReport r)
    {
        var revealedAssetIds = d.Rules
            .SelectMany(rule => rule.Actions)
            .Where(a => a.Type == "reveal_asset" && !string.IsNullOrEmpty(a.AssetId))
            .Select(a => a.AssetId!)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var id in d.RequiredEvidenceIds)
        {
            var asset = d.AssetFull.FirstOrDefault(a => a.Id == id) ?? d.ResultAssets.FirstOrDefault(a => a.Id == id);
            if (asset is null) continue;
            var visible = string.Equals(asset.Visibility, "initial", StringComparison.OrdinalIgnoreCase);
            if (visible || revealedAssetIds.Contains(id)) continue;

            asset.Visibility = "initial";
            r.Findings.Add(new() { Severity = "fixed", Area = "visibility",
                Message = $"required evidence '{id}' was hidden with no reveal rule — promoted to visibility=initial" });
        }
    }

    private void ValidateTimeline(CaseDraft d, ConsistencyReport r)
    {
        if (!TryParseUtc(d.Metadata.IncidentDate, out var incident) || !TryParseUtc(d.Metadata.OpenedAt, out var opened))
        {
            r.Findings.Add(new() { Severity = "warning", Area = "timeline", Message = "incidentDate or openedAt unparseable" });
            return;
        }
        if (opened < incident)
            r.Findings.Add(new() { Severity = "error", Area = "timeline", Message = $"openedAt ({d.Metadata.OpenedAt}) is before incidentDate ({d.Metadata.IncidentDate})" });

        foreach (var t in d.Timeline)
        {
            if (!TryParseUtc(t.Time, out var tt)) continue;
            if (tt > opened.AddMinutes(1))
                r.Findings.Add(new() { Severity = "warning", Area = "timeline", Message = $"timeline event '{t.Event}' time {t.Time} is after openedAt" });
            if (tt < incident.AddDays(-7))
                r.Findings.Add(new() { Severity = "warning", Area = "timeline", Message = $"timeline event '{t.Event}' is more than a week before incident" });
        }
    }

    private void ValidateTemporalEvents(CaseDraft d, ConsistencyReport r)
    {
        foreach (var te in d.TemporalEvents)
        {
            if (te.TriggerAtMinutes < 0)
                r.Findings.Add(new() { Severity = "error", Area = "temporalEvents", Message = $"{te.Id} triggerAtMinutes is negative" });
            if (te.TriggerAtMinutes > 24 * 60)
                r.Findings.Add(new() { Severity = "warning", Area = "temporalEvents", Message = $"{te.Id} triggerAtMinutes > 24h, may never fire" });
            if (te.Payload is null ||
                (string.IsNullOrWhiteSpace(te.Payload.Message)
                 && string.IsNullOrWhiteSpace(te.Payload.AssetId)
                 && string.IsNullOrWhiteSpace(te.Payload.EmailId)))
                r.Findings.Add(new() { Severity = "error", Area = "temporalEvents", Message = $"temporal event '{te.Id}' has no actionable payload" });
        }
    }

    private void ValidateQuestions(CaseDraft d, ConsistencyReport r)
    {
        foreach (var q in d.Questions)
        {
            var optIds = q.Options.Select(o => o.Id).ToHashSet();
            if (!optIds.Contains(q.CorrectOptionId))
                r.Findings.Add(new() { Severity = "error", Area = "solution", Message = $"question {q.Id} correctOptionId '{q.CorrectOptionId}' not in options" });
            if (q.Options.Count < 2)
                r.Findings.Add(new() { Severity = "warning", Area = "solution", Message = $"question {q.Id} has fewer than 2 options" });
        }
    }

    // -----------------------------------------------------------------
    // Utilities
    // -----------------------------------------------------------------

    private static bool TryParseUtc(string? s, out DateTime dt)
    {
        dt = default;
        if (string.IsNullOrEmpty(s)) return false;
        return DateTime.TryParse(s, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dt);
    }

    private static string Slug(string raw)
    {
        var chars = raw.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
        return new string(chars).Trim('_');
    }
}
