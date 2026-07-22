using CaseGen.Functions.Models.CaseV2;

namespace CaseGen.Functions.Services.CaseV2;

public enum PlayerActionKind
{
    InspectAsset,
    CompareRecords,
    RequestAnalysis,
    ReviewResult,
    AccuseSuspect,
    AnswerQuestion
}

public sealed class PlayerActionRequest
{
    public PlayerActionKind Kind { get; set; }
    public string ActionId { get; set; } = string.Empty;
    public string? TargetId { get; set; }
    public string? AnalysisId { get; set; }
    public string? SuspectId { get; set; }
    public string? QuestionId { get; set; }
    public string? OptionId { get; set; }
    public List<string> ObservationIds { get; set; } = new();
    public string? Rationale { get; set; }
}

public sealed record PlayerActionTraceEntry(
    int Sequence,
    PlayerActionKind Kind,
    string ActionId,
    string? TargetId,
    bool Accepted,
    string Code,
    string Message,
    IReadOnlyList<string> RevealedSourceIds,
    IReadOnlyList<string> CitedObservationIds);

public sealed class SolverDiagnosticTrace
{
    public List<PlayerActionTraceEntry> Entries { get; set; } = new();
    public List<string> FinalReachableSourceIds { get; set; } = new();
    public List<string> FinalReachableObservationIds { get; set; } = new();
    public bool LogicalProofPassed { get; set; }
    public bool NarrativeSimulationPassed { get; set; }
}

public sealed record PlayerAvailableAction(
    string Id,
    PlayerActionKind Kind,
    string Label,
    string? TargetId,
    string? AnalysisId);

public sealed record PlayerVisibleAsset(
    string Id,
    string Type,
    string Title,
    string? Description,
    string? Body,
    EvidenceDocument? BodyDoc,
    IReadOnlyList<string> ObservationIds);

public sealed record PlayerVisibleEmail(
    string Id,
    string From,
    string Subject,
    string Body,
    string SentAt,
    IReadOnlyList<string> AttachmentIds);

public sealed record PlayerVisibleSuspect(
    string Id,
    string Name,
    int Age,
    string Occupation,
    string Relationship,
    string Motive,
    string Alibi,
    bool AlibiVerified,
    string Background);

public sealed record PlayerVisibleQuestion(
    string Id,
    string Prompt,
    IReadOnlyList<SolutionOption> Options);

public sealed class PlayerVisibleCaseProjection
{
    public object Metadata { get; init; } = new();
    public IReadOnlyList<PlayerVisibleAsset> Assets { get; init; } = Array.Empty<PlayerVisibleAsset>();
    public IReadOnlyList<PlayerVisibleEmail> Emails { get; init; } = Array.Empty<PlayerVisibleEmail>();
    public IReadOnlyList<PlayerVisibleSuspect> Suspects { get; init; } = Array.Empty<PlayerVisibleSuspect>();
    public IReadOnlyList<EvidenceTimelineEntry> Timeline { get; init; } = Array.Empty<EvidenceTimelineEntry>();
    public IReadOnlyList<PlayerAvailableAction> AvailableActions { get; init; } = Array.Empty<PlayerAvailableAction>();
    public IReadOnlyList<PlayerVisibleQuestion> Questions { get; init; } = Array.Empty<PlayerVisibleQuestion>();
}

public sealed class SequentialPlayerSimulator
{
    private readonly CaseDraft _draft;
    private readonly Dictionary<string, EvidenceSourceNode> _sources;
    private readonly Dictionary<string, InvestigationAction> _actions;
    private readonly Dictionary<string, EvidenceObservation> _observations;
    private readonly HashSet<string> _reachableSources = new(StringComparer.Ordinal);
    private readonly HashSet<string> _reachableObservations = new(StringComparer.Ordinal);
    private readonly HashSet<string> _executedActions = new(StringComparer.Ordinal);
    private readonly HashSet<string> _inspectedSources = new(StringComparer.Ordinal);
    private readonly HashSet<string> _answeredQuestions = new(StringComparer.Ordinal);
    private readonly List<PlayerActionTraceEntry> _trace = new();

    public SequentialPlayerSimulator(CaseDraft draft)
    {
        _draft = draft;
        _sources = draft.CaseGraph.Sources.ToDictionary(source => source.Id, StringComparer.Ordinal);
        _actions = draft.CaseGraph.Actions.ToDictionary(action => action.Id, StringComparer.Ordinal);
        _observations = draft.CaseGraph.Observations.ToDictionary(observation => observation.Id, StringComparer.Ordinal);
        AddEligibleInitialSources();
    }

    public IReadOnlySet<string> ReachableSourceIds => _reachableSources;
    public IReadOnlySet<string> ReachableObservationIds => _reachableObservations;
    public IReadOnlySet<string> InspectedSourceIds => _inspectedSources;
    public IReadOnlyList<PlayerActionTraceEntry> Trace => _trace;
    public string? AccusedSuspectId { get; private set; }
    public IReadOnlyDictionary<string, string> Answers => _answers;
    private readonly Dictionary<string, string> _answers = new(StringComparer.Ordinal);

    public IReadOnlyList<PlayerAvailableAction> AvailableActions()
    {
        var language = _draft.Request.Language;
        var available = _reachableSources
            .Where(id => !_inspectedSources.Contains(id))
            .Order(StringComparer.Ordinal)
            .Select(id => new PlayerAvailableAction(
                $"inspect:{id}",
                PlayerActionKind.InspectAsset,
                LocaleProfileCatalog.ActionLabel(language, PlayerActionKind.InspectAsset),
                id,
                null))
            .ToList();

        available.AddRange(_actions.Values
            .Where(IsActionAvailable)
            .OrderBy(action => action.Id, StringComparer.Ordinal)
            .Select(action => new PlayerAvailableAction(
                action.Id,
                Map(action.Kind),
                LocaleProfileCatalog.ActionLabel(language, Map(action.Kind)),
                action.PrerequisiteSourceIds.FirstOrDefault(),
                action.AnalysisId)));

        if (_inspectedSources.Count > 0 && AccusedSuspectId is null)
        {
            available.AddRange(_draft.SuspectFull.OrderBy(suspect => suspect.Id, StringComparer.Ordinal)
                .Select(suspect => new PlayerAvailableAction(
                    $"accuse:{suspect.Id}",
                    PlayerActionKind.AccuseSuspect,
                    LocaleProfileCatalog.ActionLabel(language, PlayerActionKind.AccuseSuspect),
                    suspect.Id,
                    null)));
        }

        available.AddRange(_draft.Questions
            .Where(question => !_answeredQuestions.Contains(question.Id))
            .OrderBy(question => question.Id, StringComparer.Ordinal)
            .Select(question => new PlayerAvailableAction(
                $"answer:{question.Id}",
                PlayerActionKind.AnswerQuestion,
                LocaleProfileCatalog.ActionLabel(language, PlayerActionKind.AnswerQuestion),
                question.Id,
                null)));
        return available;
    }

    public PlayerActionTraceEntry Apply(PlayerActionRequest request)
    {
        var before = _reachableSources.ToHashSet(StringComparer.Ordinal);
        var code = "accepted";
        var message = PlayerMessageCatalog.Get(_draft.Request.Language, "accepted");
        var accepted = request.Kind switch
        {
            PlayerActionKind.InspectAsset => Inspect(request, ref code, ref message),
            PlayerActionKind.CompareRecords or PlayerActionKind.RequestAnalysis or PlayerActionKind.ReviewResult =>
                ExecuteGraphAction(request, ref code, ref message),
            PlayerActionKind.AccuseSuspect => Accuse(request, ref code, ref message),
            PlayerActionKind.AnswerQuestion => Answer(request, ref code, ref message),
            _ => Reject("unknown_action", PlayerMessageCatalog.Get(_draft.Request.Language, "unknownAction"), ref code, ref message)
        };
        var revealed = _reachableSources.Except(before).Order(StringComparer.Ordinal).ToArray();
        var entry = new PlayerActionTraceEntry(
            _trace.Count + 1,
            request.Kind,
            request.ActionId,
            request.TargetId,
            accepted,
            code,
            message,
            revealed,
            request.ObservationIds.Order(StringComparer.Ordinal).ToArray());
        _trace.Add(entry);
        return entry;
    }

    public SolverDiagnosticTrace Snapshot(bool logicalPassed, bool narrativePassed) => new()
    {
        Entries = _trace.ToList(),
        FinalReachableSourceIds = _reachableSources.Order(StringComparer.Ordinal).ToList(),
        FinalReachableObservationIds = _reachableObservations.Order(StringComparer.Ordinal).ToList(),
        LogicalProofPassed = logicalPassed,
        NarrativeSimulationPassed = narrativePassed
    };

    private bool Inspect(PlayerActionRequest request, ref string code, ref string message)
    {
        var sourceId = request.TargetId
            ?? (request.ActionId.StartsWith("inspect:", StringComparison.Ordinal)
                ? request.ActionId["inspect:".Length..]
                : string.Empty);
        if (!_reachableSources.Contains(sourceId))
            return Reject("source_unavailable", PlayerMessageCatalog.Get(_draft.Request.Language, "sourceUnavailable", sourceId), ref code, ref message);
        _inspectedSources.Add(sourceId);
        return true;
    }

    private bool ExecuteGraphAction(PlayerActionRequest request, ref string code, ref string message)
    {
        if (!_actions.TryGetValue(request.ActionId, out var action))
            return Reject("action_unavailable", PlayerMessageCatalog.Get(_draft.Request.Language, "actionUnavailable", request.ActionId), ref code, ref message);
        if (Map(action.Kind) != request.Kind || !IsActionAvailable(action))
            return Reject("action_prerequisite_missing", PlayerMessageCatalog.Get(_draft.Request.Language, "prerequisiteMissing", request.ActionId), ref code, ref message);
        if (request.Kind == PlayerActionKind.RequestAnalysis
            && (!string.Equals(request.AnalysisId, action.AnalysisId, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(action.AnalysisId)))
        {
            return Reject("analysis_unavailable", PlayerMessageCatalog.Get(_draft.Request.Language, "analysisUnavailable", request.AnalysisId), ref code, ref message);
        }
        _executedActions.Add(action.Id);
        foreach (var sourceId in action.RevealsSourceIds)
            AddSource(sourceId);
        AddEligibleSources();
        return true;
    }

    private bool Accuse(PlayerActionRequest request, ref string code, ref string message)
    {
        var suspectId = request.SuspectId ?? request.TargetId;
        if (!_draft.SuspectFull.Any(suspect => suspect.Id == suspectId))
            return Reject("suspect_unavailable", PlayerMessageCatalog.Get(_draft.Request.Language, "suspectUnavailable", suspectId), ref code, ref message);
        foreach (var observationId in request.ObservationIds)
        {
            if (!_reachableObservations.Contains(observationId)
                || !_observations.TryGetValue(observationId, out var observation)
                || !_inspectedSources.Contains(observation.SourceAssetId))
            {
                return Reject("citation_unavailable", PlayerMessageCatalog.Get(_draft.Request.Language, "citationUnavailable", observationId), ref code, ref message);
            }
        }
        if (request.ObservationIds.Count == 0)
            return Reject("citation_required", PlayerMessageCatalog.Get(_draft.Request.Language, "citationRequired"), ref code, ref message);
        AccusedSuspectId = suspectId;
        return true;
    }

    private bool Answer(PlayerActionRequest request, ref string code, ref string message)
    {
        var question = _draft.Questions.FirstOrDefault(item => item.Id == request.QuestionId);
        if (question is null || !question.Options.Any(option => option.Id == request.OptionId))
            return Reject("answer_unavailable", PlayerMessageCatalog.Get(_draft.Request.Language, "answerUnavailable", $"{request.QuestionId}:{request.OptionId}"), ref code, ref message);
        _answeredQuestions.Add(question.Id);
        _answers[question.Id] = request.OptionId!;
        return true;
    }

    private bool IsActionAvailable(InvestigationAction action) =>
        !_executedActions.Contains(action.Id)
        && action.PrerequisiteSourceIds.All(_reachableSources.Contains)
        && action.PrerequisiteActionIds.All(_executedActions.Contains);

    private void AddEligibleInitialSources()
    {
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var source in _sources.Values
                         .Where(source => source.AvailableAt == ReachabilityState.Initial)
                         .OrderBy(source => source.Id, StringComparer.Ordinal))
            {
                if (_reachableSources.Contains(source.Id)
                    || source.RevealPrerequisiteIds.Any(id =>
                        !_reachableSources.Contains(id) && !_executedActions.Contains(id)))
                    continue;
                AddSource(source.Id);
                changed = true;
            }
        }
    }

    private void AddEligibleSources()
    {
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var source in _sources.Values.OrderBy(source => source.Id, StringComparer.Ordinal))
            {
                if (_reachableSources.Contains(source.Id)
                    || source.RevealPrerequisiteIds.Any(id =>
                        !_reachableSources.Contains(id) && !_executedActions.Contains(id)))
                    continue;
                if (source.AvailableAt == ReachabilityState.Initial
                    || source.RevealPrerequisiteIds.Any(_executedActions.Contains))
                {
                    AddSource(source.Id);
                    changed = true;
                }
            }
        }
    }

    private void AddSource(string sourceId)
    {
        if (!_sources.TryGetValue(sourceId, out var source) || !_reachableSources.Add(sourceId))
            return;
        foreach (var observationId in source.ObservationIds)
        {
            if (_observations.TryGetValue(observationId, out var observation)
                && observation.Visibility == FactVisibility.Public)
                _reachableObservations.Add(observationId);
        }
    }

    private static PlayerActionKind Map(InvestigationActionKind kind) =>
        kind switch
        {
            InvestigationActionKind.InspectAsset => PlayerActionKind.InspectAsset,
            InvestigationActionKind.CompareRecords => PlayerActionKind.CompareRecords,
            InvestigationActionKind.RequestAnalysis => PlayerActionKind.RequestAnalysis,
            InvestigationActionKind.ReviewResult => PlayerActionKind.ReviewResult,
            _ => PlayerActionKind.InspectAsset
        };

    private static bool Reject(string errorCode, string errorMessage, ref string code, ref string message)
    {
        code = errorCode;
        message = errorMessage;
        return false;
    }
}

public static class PlayerVisibleProjectionBuilder
{
    public static PlayerVisibleCaseProjection Build(CaseDraft draft, SequentialPlayerSimulator simulator)
    {
        var reachable = simulator.ReachableSourceIds;
        var observationsBySource = draft.CaseGraph.Observations
            .Where(observation => simulator.ReachableObservationIds.Contains(observation.Id))
            .GroupBy(observation => observation.SourceAssetId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group.Select(item => item.Id).Order(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
        var assets = draft.AssetFull.Concat(draft.ResultAssets)
            .Where(asset => reachable.Contains(asset.Id))
            .GroupBy(asset => asset.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(asset => asset.Id, StringComparer.Ordinal)
            .Select(asset => new PlayerVisibleAsset(
                asset.Id,
                asset.Type,
                asset.Title,
                asset.Description,
                asset.Body,
                asset.BodyDoc,
                observationsBySource.GetValueOrDefault(asset.Id) ?? Array.Empty<string>()))
            .ToArray();
        var reachableAssetIds = assets.Select(asset => asset.Id).ToHashSet(StringComparer.Ordinal);
        var emails = draft.FollowUpEmails.Concat(draft.ResultEmails)
            .Where(email => reachable.Contains(email.Id))
            .Concat(Briefing(draft))
            .GroupBy(email => email.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(email => email.Id, StringComparer.Ordinal)
            .Select(email => new PlayerVisibleEmail(
                email.Id,
                email.From,
                email.Subject,
                email.Body,
                email.SentAt,
                email.Attachments.Where(reachableAssetIds.Contains).Order(StringComparer.Ordinal).ToArray()))
            .ToArray();

        return new PlayerVisibleCaseProjection
        {
            Metadata = new
            {
                draft.Metadata.Title,
                draft.Metadata.Description,
                draft.Metadata.Location,
                draft.Metadata.IncidentDate,
                draft.Metadata.OpenedAt,
                draft.Metadata.Difficulty,
                language = ForensicMethodCatalog.NormalizeLanguage(draft.Request.Language)
            },
            Assets = assets,
            Emails = emails,
            Suspects = draft.SuspectFull.OrderBy(suspect => suspect.Id, StringComparer.Ordinal)
                .Select(suspect => new PlayerVisibleSuspect(
                    suspect.Id, suspect.Name, suspect.Age ?? 0, suspect.Occupation ?? string.Empty, suspect.Relationship ?? string.Empty,
                    suspect.Motive ?? string.Empty, suspect.Alibi ?? string.Empty, suspect.AlibiVerified, suspect.Background ?? string.Empty))
                .ToArray(),
            Timeline = draft.Timeline.ToArray(),
            AvailableActions = simulator.AvailableActions(),
            Questions = draft.Questions.Select(question => new PlayerVisibleQuestion(
                question.Id,
                question.Prompt,
                question.Options.Select(option => new SolutionOption { Id = option.Id, Label = option.Label }).ToArray()))
                .ToArray()
        };
    }

    private static IEnumerable<EvidenceEmail> Briefing(CaseDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.Briefing.Subject) && string.IsNullOrWhiteSpace(draft.Briefing.Body))
            return Array.Empty<EvidenceEmail>();
        return
        [
            new EvidenceEmail
            {
                Id = "email.briefing",
                From = draft.Briefing.From,
                Subject = draft.Briefing.Subject,
                Body = draft.Briefing.Body,
                SentAt = draft.Metadata.OpenedAt,
                Visibility = "initial"
            }
        ];
    }
}

public sealed class LogicalSolvabilityResult
{
    public List<string> Errors { get; } = new();
    public bool IsValid => Errors.Count == 0;
}

public static class LogicalSolvabilityGate
{
    public static LogicalSolvabilityResult Validate(CaseDraft draft)
    {
        var result = new LogicalSolvabilityResult();
        result.Errors.AddRange(CaseGraphValidator.Validate(draft.CaseGraph).Errors.Select(error => error.Message));
        result.Errors.AddRange(CulpritUniquenessGate.Validate(
            draft.CaseGraph,
            draft.CulpritId,
            DifficultyProfileCatalog.Get(draft).Name).Failures.Select(failure => failure.Message));
        result.Errors.AddRange(IndependentProofPathGate.Validate(
            draft.CaseGraph,
            draft.CulpritId,
            DifficultyProfileCatalog.Get(draft).Name).Failures.Select(failure => failure.Message));
        result.Errors.AddRange(ReachabilityGate.Validate(draft.CaseGraph, draft.CulpritId).Failures.Select(failure => failure.Message));
        result.Errors.AddRange(PrematureSolutionLeakageGate.Validate(
            draft.CaseGraph,
            draft.CulpritId,
            DifficultyProfileCatalog.Get(draft).Name).Failures.Select(failure => failure.Message));
        result.Errors.AddRange(DifficultyTopologyValidator.Validate(draft).Errors);
        return result;
    }
}

public static class SolvabilityDecision
{
    public static bool BothPass(bool logicalProofPassed, bool narrativeSimulationPassed, double score) =>
        logicalProofPassed && narrativeSimulationPassed && score >= 0.7;
}
