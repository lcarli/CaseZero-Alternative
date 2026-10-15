using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CaseGen.Functions.Services.CaseV2.Tasks;

namespace CaseGen.Functions.Services.CaseV2;

public enum RepairOperationKind
{
    ReplaceFact,
    ReplaceEvent,
    ReplaceObservation,
    ReassignObservation,
    RewriteAsset,
    ReplaceForensicTransform,
    RewriteQuestion,
    RewriteExplanation
}

public sealed class RepairOperation
{
    public string TargetNodeId { get; set; } = string.Empty;
    public string ConstraintId { get; set; } = string.Empty;
    public RepairOperationKind Operation { get; set; }
    public List<string> InvalidatedNodeIds { get; set; } = new();
}

public sealed record NodeGenerationRecord(
    string NodeId,
    string OwningStage,
    IReadOnlyList<string> InputNodeIds,
    IReadOnlyList<string> OutputNodeIds,
    IReadOnlyList<string> DownstreamDependentIds,
    string ContentHash);

public sealed class NodeDependencyIndex
{
    private readonly IReadOnlyDictionary<string, NodeGenerationRecord> _records;

    private NodeDependencyIndex(IReadOnlyDictionary<string, NodeGenerationRecord> records)
    {
        _records = records;
    }

    public IReadOnlyCollection<NodeGenerationRecord> Records => _records.Values.ToArray();

    public NodeGenerationRecord? Find(string nodeId) => _records.GetValueOrDefault(nodeId);

    public IReadOnlyList<string> CalculateInvalidation(string targetNodeId)
    {
        var invalidated = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(targetNodeId);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!invalidated.Add(current) || !_records.TryGetValue(current, out var record))
                continue;
            foreach (var dependent in record.DownstreamDependentIds)
                queue.Enqueue(dependent);
        }
        return invalidated.Order(StringComparer.Ordinal).ToArray();
    }

    public static NodeDependencyIndex Build(CaseDraft draft)
    {
        var units = new Dictionary<string, MutableRecord>(StringComparer.Ordinal);
        var canonicalOwner = draft.CaseBible is null ? "plotOutline" : "caseBible";
        foreach (var entity in draft.CaseGraph.Entities)
            Add(entity.Id, canonicalOwner, [], entity);
        foreach (var fact in draft.CaseGraph.Facts)
            Add(fact.Id, canonicalOwner, References(fact.SubjectId, fact.ObjectId, fact.EventId), fact);
        foreach (var graphEvent in draft.CaseGraph.Events)
            Add(
                graphEvent.Id,
                draft.CaseBible is null ? "timeline" : "caseBible",
                graphEvent.ParticipantIds.Concat(graphEvent.FactIds),
                graphEvent);
        foreach (var source in draft.CaseGraph.Sources)
            Add(source.Id, source.AvailableAt == ReachabilityState.AfterForensic ? "forensics" : "assetPlan",
                source.ObservationIds.Concat(source.RevealPrerequisiteIds), source);
        foreach (var observation in draft.CaseGraph.Observations)
            Add(observation.Id, "assetCards", References(observation.FactId, observation.SourceAssetId), observation);
        foreach (var derivation in draft.CaseGraph.Derivations)
            Add(derivation.Id, "solution", derivation.PremiseIds.Append(derivation.ConclusionFactId), derivation);
        foreach (var spec in draft.CaseGraph.AssetSpecs)
            Add(spec.Id, "assetPlan", spec.ObservationIds.Concat(spec.ContainedObjectIds), spec);
        foreach (var transform in draft.CaseGraph.ForensicTransforms)
            Add(transform.Id, "forensics",
                References(transform.InputAssetId, transform.InputObjectId, transform.ReferenceSampleObjectId,
                    transform.ChainOfCustodyObservationId).Concat(transform.ProducedObservationIds), transform);
        foreach (var asset in draft.AssetFull.Concat(draft.ResultAssets))
        {
            IEnumerable<string> inputs =
                draft.CaseGraph.AssetSpecs.FirstOrDefault(spec => spec.Id == asset.Id)?.ObservationIds
                ?? Enumerable.Empty<string>();
            Add(asset.Id, asset.Visibility == "hidden" ? "forensics" : "assetCards", inputs, asset);
        }
        foreach (var question in draft.Questions)
        {
            IEnumerable<string> inputs =
                draft.QuestionTopics.FirstOrDefault(topic => topic.Id == question.Id)?.SupportingEvidenceIds
                ?? Enumerable.Empty<string>();
            Add(question.Id, "solution", inputs, question);
        }
        Add("explanation.solution", "solution", draft.RequiredEvidenceIds.Concat(draft.RequiredAnalysisIds), draft.Explanation);

        foreach (var record in units.Values)
        foreach (var input in record.InputNodeIds.Where(units.ContainsKey))
            units[input].Dependents.Add(record.NodeId);

        var frozen = units.ToDictionary(
            pair => pair.Key,
            pair => new NodeGenerationRecord(
                pair.Value.NodeId,
                pair.Value.OwningStage,
                pair.Value.InputNodeIds.Order(StringComparer.Ordinal).ToArray(),
                [pair.Value.NodeId],
                pair.Value.Dependents.Order(StringComparer.Ordinal).ToArray(),
                pair.Value.ContentHash),
            StringComparer.Ordinal);
        return new NodeDependencyIndex(frozen);

        void Add(string nodeId, string stage, IEnumerable<string> inputs, object content)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
                return;
            var normalizedInputs = inputs.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
            if (units.TryGetValue(nodeId, out var existing))
            {
                existing.InputNodeIds.UnionWith(normalizedInputs);
                existing.OwningStage = stage;
                existing.ContentHash = ContentHasher.Hash(content);
                return;
            }
            units[nodeId] = new MutableRecord(nodeId, stage, normalizedInputs, ContentHasher.Hash(content));
        }
    }

    private static IEnumerable<string> References(params string?[] ids) =>
        ids.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!);

    private sealed class MutableRecord
    {
        public MutableRecord(string nodeId, string stage, IEnumerable<string> inputs, string hash)
        {
            NodeId = nodeId;
            OwningStage = stage;
            InputNodeIds = new HashSet<string>(inputs, StringComparer.Ordinal);
            ContentHash = hash;
        }

        public string NodeId { get; }
        public string OwningStage { get; set; }
        public HashSet<string> InputNodeIds { get; }
        public HashSet<string> Dependents { get; } = new(StringComparer.Ordinal);
        public string ContentHash { get; set; }
    }
}

public static class ContentHasher
{
    public static string Hash(object? content)
    {
        var node = JsonSerializer.SerializeToNode(content);
        var canonical = Canonical(node);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string Canonical(JsonNode? node) =>
        node switch
        {
            null => "null",
            JsonValue value => value.ToJsonString(),
            JsonArray array => "[" + string.Join(",", array.Select(Canonical)) + "]",
            JsonObject obj => "{" + string.Join(",", obj
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => JsonSerializer.Serialize(pair.Key) + ":" + Canonical(pair.Value))) + "}",
            _ => node.ToJsonString()
        };
}

public static class GranularRepairPlanner
{
    public static IReadOnlyList<RepairOperation> Build(
        CaseDraft draft,
        IEnumerable<RedTeamTask.Finding> findings,
        IEnumerable<RepairIssue> issues)
    {
        var index = NodeDependencyIndex.Build(draft);
        var requests = findings
            .Where(finding => !string.Equals(finding.Severity, "low", StringComparison.OrdinalIgnoreCase))
            .Select(finding => (
                Node: finding.NodeId ?? finding.FactRefs.FirstOrDefault() ?? StageNode(finding.OwnerStage),
                Constraint: finding.ConstraintId ?? finding.RepairAction ?? "specialist.finding",
                Stage: finding.OwnerStage))
            .Concat(issues.Select(issue => (
                Node: issue.NodeId ?? StageNode(issue.Stage),
                Constraint: issue.ConstraintId ?? Slug(issue.Message),
                Stage: (string?)issue.Stage)))
            .Where(request => !string.IsNullOrWhiteSpace(request.Node))
            .Distinct()
            .OrderBy(request => request.Node, StringComparer.Ordinal)
            .ThenBy(request => request.Constraint, StringComparer.Ordinal);

        return requests.Select(request => new RepairOperation
        {
            TargetNodeId = request.Node!,
            ConstraintId = request.Constraint,
            Operation = OperationFor(request.Node!, request.Stage),
            InvalidatedNodeIds = index.CalculateInvalidation(request.Node!).ToList()
        }).ToArray();
    }

    private static RepairOperationKind OperationFor(string nodeId, string? stage) =>
        nodeId switch
        {
            var id when id.StartsWith("fact.", StringComparison.Ordinal) => RepairOperationKind.ReplaceFact,
            var id when id.StartsWith("event.", StringComparison.Ordinal) => RepairOperationKind.ReplaceEvent,
            var id when id.StartsWith("observation.", StringComparison.Ordinal) => RepairOperationKind.ReplaceObservation,
            var id when id.StartsWith("asset.", StringComparison.Ordinal) => RepairOperationKind.RewriteAsset,
            var id when id.StartsWith("forensic.", StringComparison.Ordinal) => RepairOperationKind.ReplaceForensicTransform,
            var id when id.StartsWith("question.", StringComparison.Ordinal) => RepairOperationKind.RewriteQuestion,
            "explanation.solution" => RepairOperationKind.RewriteExplanation,
            _ when string.Equals(stage, "forensics", StringComparison.OrdinalIgnoreCase) => RepairOperationKind.ReplaceForensicTransform,
            _ when string.Equals(stage, "solution", StringComparison.OrdinalIgnoreCase) => RepairOperationKind.RewriteExplanation,
            _ => RepairOperationKind.ReplaceObservation
        };

    private static string StageNode(string? stage) => $"stage.{stage ?? "unknown"}";

    private static string Slug(string message) =>
        new(message.ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());
}

public sealed record RepairPlateau(
    string NodeId,
    string ConstraintId,
    int Attempts,
    int Limit,
    string? BestContentHash);

public sealed class RepairBudgetTracker
{
    private readonly Dictionary<(string Node, string Constraint), BudgetState> _states = new();
    private readonly HashSet<string> _frozenNodes = new(StringComparer.Ordinal);

    public IReadOnlySet<string> FrozenNodeIds => _frozenNodes;

    public bool TryBegin(
        RepairOperation operation,
        int limit,
        string? currentHash,
        out RepairPlateau? plateau)
    {
        var key = (operation.TargetNodeId, operation.ConstraintId);
        if (!_states.TryGetValue(key, out var state))
            _states[key] = state = new BudgetState();
        if (state.Attempts >= limit)
        {
            plateau = new RepairPlateau(
                operation.TargetNodeId,
                operation.ConstraintId,
                state.Attempts,
                limit,
                state.BestContentHash);
            return false;
        }
        state.Attempts++;
        if (!string.IsNullOrWhiteSpace(currentHash))
            state.BestContentHash ??= currentHash;
        plateau = null;
        return true;
    }

    public void RecordValid(string nodeId, string contentHash)
    {
        _frozenNodes.Add(nodeId);
        foreach (var state in _states.Where(pair => pair.Key.Node == nodeId).Select(pair => pair.Value))
            state.BestContentHash = contentHash;
    }

    private sealed class BudgetState
    {
        public int Attempts { get; set; }
        public string? BestContentHash { get; set; }
    }
}

public sealed class GranularRepairReport
{
    public List<RepairOperation> Operations { get; } = new();
    public List<RepairPlateau> Plateaus { get; } = new();
    public List<string> FrozenNodeIds { get; } = new();
}
