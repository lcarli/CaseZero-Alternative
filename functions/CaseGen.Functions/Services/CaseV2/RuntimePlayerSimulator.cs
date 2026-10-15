using CaseGen.Functions.Models.CaseV2;
using CaseZero.Progression;

namespace CaseGen.Functions.Services.CaseV2;

public sealed class RuntimePlayerSimulator
{
    private readonly CaseDraft _draft;
    private readonly IReadOnlyDictionary<string, string> _readableTexts;
    private readonly Dictionary<string, EvidenceAsset> _assets;
    private readonly Dictionary<string, EvidenceEmail> _emails;
    private readonly HashSet<string> _visibleAssets = new(StringComparer.Ordinal);
    private readonly HashSet<string> _visibleEmails = new(StringComparer.Ordinal);
    private readonly HashSet<string> _readAssets = new(StringComparer.Ordinal);
    private readonly HashSet<string> _readEmails = new(StringComparer.Ordinal);
    private readonly HashSet<string> _viewedSuspects = new(StringComparer.Ordinal);
    private readonly HashSet<string> _downloaded = new(StringComparer.Ordinal);
    private readonly HashSet<string> _firedRules = new(StringComparer.Ordinal);
    private readonly HashSet<string> _firedEvents = new(StringComparer.Ordinal);
    private readonly HashSet<string> _verifiedAlibis = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _suspectStatuses = new(StringComparer.Ordinal);
    private readonly List<(string Level, string Message)> _notifications = [];
    private readonly Dictionary<string, int> _pending = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _analysisRequestIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _completed = new(StringComparer.Ordinal);
    private readonly List<PlayerActionTraceEntry> _trace = [];
    public int GameMinutes { get; private set; }
    public IReadOnlySet<string> ReadAssets => _readAssets;
    public IReadOnlySet<string> ReadEmails => _readEmails;
    public IReadOnlySet<string> CompletedAnalyses => _completed;
    public IReadOnlyList<PlayerActionTraceEntry> Trace => _trace;

    public RuntimePlayerSimulator(CaseDraft draft, IReadOnlyDictionary<string, string> readableTexts)
    {
        _draft = draft;
        _readableTexts = readableTexts;
        _assets = draft.AssetFull.Concat(draft.ResultAssets).ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        _emails = draft.FollowUpEmails.Concat(draft.ResultEmails).Select(email => new EvidenceEmail
        {
            Id = email.Id, From = email.From, Subject = email.Subject, Body = email.Body,
            SentAt = email.SentAt, Visibility = email.Visibility, Attachments = email.Attachments.ToList()
        }).ToDictionary(email => email.Id, StringComparer.Ordinal);
        _emails.Add("email.briefing", new EvidenceEmail
        {
            Id = "email.briefing", From = draft.Briefing.From, Subject = draft.Briefing.Subject,
            Body = draft.Briefing.Body, SentAt = draft.Metadata.OpenedAt, Visibility = "initial",
            Attachments = CaseEmailProjection.BriefingAttachments(draft)
        });
        _verifiedAlibis.UnionWith(draft.SuspectFull.Where(suspect => suspect.AlibiVerified).Select(suspect => suspect.Id));
        foreach (var rule in draft.Rules)
            foreach (var trigger in RuleConditions.Leaves(rule.Trigger))
                ValidateLeaf(trigger);
        var allInitial = string.Equals(draft.Metadata.RequiredRank, "Rookie", StringComparison.OrdinalIgnoreCase);
        _visibleAssets.UnionWith(_assets.Values.Where(asset => allInitial || asset.Visibility == "initial").Select(asset => asset.Id));
        _visibleEmails.UnionWith(_emails.Values.Where(email => allInitial || email.Visibility == "initial").Select(email => email.Id));
        ApplyConsequences();
    }

    public IReadOnlyList<PlayerAvailableAction> AvailableActions()
    {
        var result = _visibleAssets.Where(id => !_readAssets.Contains(id)).Order(StringComparer.Ordinal)
            .Select(id => new PlayerAvailableAction($"inspect:{id}", PlayerActionKind.InspectAsset,
                LocaleProfileCatalog.ActionLabel(_draft.Request.Language, PlayerActionKind.InspectAsset), id, null))
            .ToList();
        result.AddRange(_visibleEmails.Where(id => !_readEmails.Contains(id)).Order(StringComparer.Ordinal)
            .Select(id => new PlayerAvailableAction($"email:{id}", PlayerActionKind.ReadEmail,
                LocaleProfileCatalog.ActionLabel(_draft.Request.Language, PlayerActionKind.ReadEmail), id, null)));
        foreach (var email in _emails.Values.Where(email => _readEmails.Contains(email.Id)).OrderBy(email => email.Id, StringComparer.Ordinal))
            foreach (var assetId in email.Attachments.Order(StringComparer.Ordinal))
                if (!_downloaded.Contains($"{email.Id}:{assetId}"))
                    result.Add(new($"download:{email.Id}:{assetId}", PlayerActionKind.DownloadAttachment,
                        LocaleProfileCatalog.ActionLabel(_draft.Request.Language, PlayerActionKind.DownloadAttachment), assetId, email.Id));
        result.AddRange(_draft.SuspectFull.Where(suspect => !_viewedSuspects.Contains(suspect.Id))
            .OrderBy(suspect => suspect.Id, StringComparer.Ordinal)
            .Select(suspect => new PlayerAvailableAction($"suspect:{suspect.Id}", PlayerActionKind.InspectSuspect,
                LocaleProfileCatalog.ActionLabel(_draft.Request.Language, PlayerActionKind.InspectSuspect), suspect.Id, null)));
        foreach (var asset in _assets.Values.Where(asset => _visibleAssets.Contains(asset.Id)).OrderBy(asset => asset.Id, StringComparer.Ordinal))
            foreach (var analysis in _draft.AnalysisTypes.Where(analysis => analysis.AvailableFor.Contains(asset.Type)))
            {
                var id = $"{asset.Id}:{analysis.Type}";
                if (!_pending.ContainsKey(id) && !_completed.Contains(id))
                    result.Add(new($"analyze:{id}", PlayerActionKind.RequestAnalysis,
                        LocaleProfileCatalog.ActionLabel(_draft.Request.Language, PlayerActionKind.RequestAnalysis), asset.Id, analysis.Type));
            }
        var nextTime = _pending.Values.Concat(_draft.TemporalEvents
            .Where(item => item.TriggerAtMinutes > GameMinutes).Select(item => item.TriggerAtMinutes))
            .Concat(_draft.Rules.SelectMany(rule => RuleConditions.Leaves(rule.Trigger))
                .Where(trigger => trigger.Type == "time_elapsed" && trigger.AtMinutes > GameMinutes)
                .Select(trigger => trigger.AtMinutes!.Value))
            .Where(minutes => minutes > GameMinutes).DefaultIfEmpty(-1).Min();
        if (nextTime >= 0)
            result.Add(new($"wait:{nextTime - GameMinutes}", PlayerActionKind.Wait,
                LocaleProfileCatalog.ActionLabel(_draft.Request.Language, PlayerActionKind.Wait), nextTime.ToString(System.Globalization.CultureInfo.InvariantCulture), null));
        return result;
    }

    public object Projection() => new
    {
        metadata = new
        {
            _draft.Metadata.Title, _draft.Metadata.Description, _draft.Metadata.Location,
            _draft.Metadata.IncidentDate, _draft.Metadata.OpenedAt, _draft.Metadata.Difficulty,
            language = _draft.Request.Language
        },
        gameMinutes = GameMinutes,
        assets = _assets.Values.Where(asset => _visibleAssets.Contains(asset.Id)).OrderBy(asset => asset.Id, StringComparer.Ordinal)
            .Select(asset => new
            {
                asset.Id, asset.Type, asset.Title, asset.Description,
                inspected = _readAssets.Contains(asset.Id),
                text = _readAssets.Contains(asset.Id) ? _readableTexts.GetValueOrDefault(asset.Id) : null,
                unverifiedVisual = asset.Type is "photo" or "image"
            }),
        emails = _emails.Values.Where(email => _visibleEmails.Contains(email.Id)).OrderBy(email => email.Id, StringComparer.Ordinal)
            .Select(email => new
            {
                email.Id, email.From, email.Subject, email.SentAt,
                opened = _readEmails.Contains(email.Id),
                body = _readEmails.Contains(email.Id) ? email.Body : null,
                attachments = _readEmails.Contains(email.Id) ? email.Attachments : []
            }),
        suspects = _draft.SuspectFull.OrderBy(suspect => suspect.Id, StringComparer.Ordinal).Select(suspect => new
        {
            suspect.Id, suspect.Name,
            profile = _viewedSuspects.Contains(suspect.Id) ? new
            {
                suspect.Age, suspect.Occupation, suspect.Relationship, suspect.Motive,
                suspect.Alibi, AlibiVerified = _verifiedAlibis.Contains(suspect.Id), suspect.Background,
                status = _suspectStatuses.GetValueOrDefault(suspect.Id, "suspect")
            } : null
        }),
        timeline = _draft.Timeline,
        questions = _draft.Questions.Select(question => new { question.Id, question.Prompt, question.Options }),
        pendingAnalyses = _pending.Select(pair => new { id = pair.Key, completionAtMinutes = pair.Value }),
        completedAnalyses = _completed.Order(StringComparer.Ordinal),
        history = _trace.Select(entry => new { entry.Sequence, entry.Kind, entry.ActionId, entry.TargetId, entry.RevealedSourceIds }),
        notifications = _notifications.Select(notification => new { level = notification.Level, message = notification.Message }),
        actions = AvailableActions()
    };

    public PlayerActionTraceEntry Apply(string actionId)
    {
        var action = AvailableActions().SingleOrDefault(action => action.Id == actionId)
            ?? throw new TaskContractException($"Player action {actionId} is not currently available.");
        var before = _visibleAssets.Concat(_visibleEmails).ToHashSet(StringComparer.Ordinal);
        switch (action.Kind)
        {
            case PlayerActionKind.InspectAsset: _readAssets.Add(action.TargetId!); break;
            case PlayerActionKind.ReadEmail: _readEmails.Add(action.TargetId!); break;
            case PlayerActionKind.InspectSuspect: _viewedSuspects.Add(action.TargetId!); break;
            case PlayerActionKind.DownloadAttachment:
                _downloaded.Add($"{action.AnalysisId}:{action.TargetId}");
                RevealAsset(action.TargetId!);
                break;
            case PlayerActionKind.RequestAnalysis:
                var type = _draft.AnalysisTypes.Single(type => type.Type == action.AnalysisId);
                if (type.DurationMinutes <= 0)
                    throw new TaskContractException("Analysis duration must be positive.");
                var analysisId = $"{action.TargetId}:{action.AnalysisId}";
                _analysisRequestIds.Add(analysisId, checked(_analysisRequestIds.Count + 1));
                _pending.Add(analysisId, checked(GameMinutes + type.DurationMinutes));
                break;
            case PlayerActionKind.Wait:
                GameMinutes = int.Parse(action.TargetId!, System.Globalization.CultureInfo.InvariantCulture);
                break;
            default: throw new TaskContractException($"Unsupported live player action {action.Kind}.");
        }
        ApplyConsequences();
        var entry = new PlayerActionTraceEntry(_trace.Count + 1, action.Kind, action.Id, action.TargetId,
            true, "accepted", PlayerMessageCatalog.Get(_draft.Request.Language, "accepted"),
            _visibleAssets.Concat(_visibleEmails).Except(before).Order(StringComparer.Ordinal).ToArray(), []);
        _trace.Add(entry);
        return entry;
    }

    public string? ReadableSource(string id) => _readAssets.Contains(id) ? _readableTexts.GetValueOrDefault(id)
        : _readEmails.Contains(id) ? _emails[id].Body : null;

    private void ApplyConsequences()
    {
        foreach (var analysis in _pending.Where(pair => pair.Value <= GameMinutes).ToList())
        {
            _pending.Remove(analysis.Key);
            _completed.Add(analysis.Key);
            var outcome = _draft.ForensicFull.FirstOrDefault(outcome => $"{outcome.InputAssetId}:{outcome.AnalysisType}" == analysis.Key);
            if (outcome is { Findings: true })
            {
                if (outcome.ResultAssetId is { } assetId) RevealAsset(assetId);
                if (outcome.ResultEmailId is { } emailId) RevealEmail(emailId);
            }
            else
            {
                var asset = _assets.Values.Single(asset => analysis.Key.StartsWith($"{asset.Id}:", StringComparison.Ordinal));
                var email = CaseEmailProjection.NoFindings(_draft, _analysisRequestIds[analysis.Key],
                    analysis.Key[(asset.Id.Length + 1)..], asset.Title, GameMinutes);
                _emails.Add(email.Id, email);
                RevealEmail(email.Id);
            }
        }
        bool changed;
        do
        {
            changed = false;
            foreach (var temporal in _draft.TemporalEvents.Where(item => item.TriggerAtMinutes <= GameMinutes && !_firedEvents.Contains(item.Id)))
            {
                _firedEvents.Add(temporal.Id);
                if (temporal.Payload?.AssetId is { } assetId) changed |= RevealAsset(assetId);
                if (temporal.Payload?.EmailId is { } emailId) changed |= RevealEmail(emailId);
                if (!string.IsNullOrWhiteSpace(temporal.Payload?.Message)) _notifications.Add(("info", temporal.Payload.Message));
            }
            foreach (var rule in _draft.Rules.Where(rule => !_firedRules.Contains(rule.RuleId)))
            {
                if (!RuleConditions.Satisfied(rule.Trigger, Leaf)) continue;
                _firedRules.Add(rule.RuleId);
                foreach (var action in rule.Actions)
                {
                    switch (action.Type)
                    {
                        case "reveal_asset": changed |= RevealAsset(action.AssetId!); break;
                        case "reveal_email": changed |= RevealEmail(action.EmailId!); break;
                        case "reveal_suspect":
                            if (!_draft.SuspectFull.Any(suspect => suspect.Id == action.SuspectId))
                                throw new TaskContractException($"Rule references undeclared suspect {action.SuspectId}.");
                            break;
                        case "add_email_attachment":
                            if (!_emails.TryGetValue(action.EmailId!, out var email) || !_assets.ContainsKey(action.AssetId!))
                                throw new TaskContractException("Rule attachment references an undeclared source.");
                            if (!email.Attachments.Contains(action.AssetId!))
                            {
                                email.Attachments.Add(action.AssetId!);
                                changed = true;
                            }
                            break;
                        case "send_notification":
                            if (string.IsNullOrWhiteSpace(action.Message) || action.Level is not (null or "info" or "warn" or "critical"))
                                throw new TaskContractException("Rule notification has an invalid message or level.");
                            _notifications.Add((action.Level ?? "info", action.Message));
                            break;
                        case "update_suspect_status":
                            RequireSuspect(action.SuspectId);
                            if (action.Status is not ("suspect" or "cleared" or "confirmed_culprit"))
                                throw new TaskContractException("Rule has an invalid suspect status.");
                            _suspectStatuses[action.SuspectId!] = action.Status;
                            break;
                        case "mark_alibi_verified":
                            RequireSuspect(action.SuspectId);
                            if (action.Verified == false) _verifiedAlibis.Remove(action.SuspectId!);
                            else _verifiedAlibis.Add(action.SuspectId!);
                            break;
                        default: throw new TaskContractException($"Unsupported public rule action {action.Type}.");
                    }
                }
            }
        } while (changed);
    }

    private bool Leaf(RulesTrigger trigger) => trigger.Type switch
    {
        "asset_viewed" => _readAssets.Contains(trigger.AssetId ?? trigger.InputAssetId ?? string.Empty),
        "email_opened" => _readEmails.Contains(trigger.EmailId ?? string.Empty),
        "suspect_viewed" => _viewedSuspects.Contains(trigger.SuspectId ?? string.Empty),
        "forensics_complete" => _completed.Contains($"{trigger.InputAssetId}:{trigger.AnalysisType}"),
        "time_elapsed" => trigger.AtMinutes is >= 0 && GameMinutes >= trigger.AtMinutes.Value,
        "attachment_download" => _downloaded.Any(pair => _emails.Values.Any(email =>
            (trigger.EmailId is null || trigger.EmailId == email.Id) && email.Attachments.Any(asset =>
                (trigger.AssetId is null || trigger.AssetId == asset) && pair == $"{email.Id}:{asset}"))),
        _ => throw new TaskContractException($"Unsupported public rule trigger {trigger.Type}.")
    };

    private void ValidateLeaf(RulesTrigger trigger)
    {
        switch (trigger.Type)
        {
            case "asset_viewed":
                if (!_assets.ContainsKey(trigger.AssetId ?? trigger.InputAssetId ?? string.Empty))
                    throw new TaskContractException("Rule references undeclared viewed evidence.");
                break;
            case "email_opened":
                if (!_emails.ContainsKey(trigger.EmailId ?? string.Empty))
                    throw new TaskContractException("Rule references an undeclared email.");
                break;
            case "suspect_viewed": RequireSuspect(trigger.SuspectId); break;
            case "forensics_complete":
                if (!_assets.TryGetValue(trigger.InputAssetId ?? string.Empty, out var input)
                    || !_draft.AnalysisTypes.Any(type => type.Type == trigger.AnalysisType && type.AvailableFor.Contains(input.Type)))
                    throw new TaskContractException("Rule references an unavailable forensic analysis.");
                break;
            case "time_elapsed":
                if (trigger.AtMinutes is not >= 0) throw new TaskContractException("Rule has an invalid game-time threshold.");
                break;
            case "attachment_download":
                if ((trigger.EmailId is not null && !_emails.ContainsKey(trigger.EmailId))
                    || (trigger.AssetId is not null && !_assets.ContainsKey(trigger.AssetId)))
                    throw new TaskContractException("Rule download references an undeclared source.");
                break;
            default: throw new TaskContractException($"Unsupported public rule trigger {trigger.Type}.");
        }
    }

    private void RequireSuspect(string? id)
    {
        if (!_draft.SuspectFull.Any(suspect => suspect.Id == id))
            throw new TaskContractException($"Rule references undeclared suspect {id}.");
    }

    private bool RevealAsset(string id) => _assets.ContainsKey(id) ? _visibleAssets.Add(id)
        : throw new TaskContractException($"Investigation reveals undeclared evidence {id}.");
    private bool RevealEmail(string id) => _emails.ContainsKey(id) ? _visibleEmails.Add(id)
        : throw new TaskContractException($"Investigation reveals undeclared email {id}.");
}
