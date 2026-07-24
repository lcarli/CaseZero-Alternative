namespace CaseGen.Functions.Services.CaseV2;

public sealed record GraphRepairTarget(
    string OwningStage,
    string NodeId,
    string Property,
    string SuggestedOperation);

public sealed record GraphValidationError(
    string Code,
    string NodeId,
    string OwningStage,
    string Message,
    GraphRepairTarget RepairTarget);

public sealed class GraphValidationReport
{
    public List<GraphValidationError> Errors { get; } = new();
    public bool IsValid => Errors.Count == 0;
}

public static class CaseGraphValidator
{
    public static GraphValidationReport Validate(CaseGraph graph)
    {
        var errors = new List<GraphValidationError>();
        var entityIds = graph.Entities.Select(entity => entity.Id).ToHashSet(StringComparer.Ordinal);
        var suspectIds = graph.SuspectReferences.Select(reference => reference.SuspectId).ToHashSet(StringComparer.Ordinal);
        var factIds = graph.Facts.Select(fact => fact.Id).ToHashSet(StringComparer.Ordinal);
        var eventIds = graph.Events.Select(graphEvent => graphEvent.Id).ToHashSet(StringComparer.Ordinal);
        var sourceIds = graph.Sources.Select(source => source.Id).ToHashSet(StringComparer.Ordinal);
        var observationIds = graph.Observations.Select(observation => observation.Id).ToHashSet(StringComparer.Ordinal);
        var derivationIds = graph.Derivations.Select(derivation => derivation.Id).ToHashSet(StringComparer.Ordinal);
        var actionIds = graph.Actions.Select(action => action.Id).ToHashSet(StringComparer.Ordinal);

        AddEntityErrors(graph, errors);
        AddDuplicateIdErrors(graph, errors);
        AddTypedNodeIdErrors(graph, errors);
        AddFactErrors(graph, entityIds, suspectIds, eventIds, errors);
        AddEventErrors(graph, entityIds, suspectIds, factIds, errors);
        AddObservationErrors(graph, factIds, sourceIds, errors);
        AddSourceErrors(graph, observationIds, sourceIds, actionIds, errors);
        AddActionErrors(graph, sourceIds, actionIds, errors);
        AddDerivationErrors(graph, factIds, observationIds, derivationIds, errors);
        AddTemporalConstraintErrors(graph, eventIds, errors);
        AddDerivationCycleErrors(graph, errors);
        AddRevealCycleErrors(graph, errors);

        var report = new GraphValidationReport();
        report.Errors.AddRange(errors
            .OrderBy(error => error.OwningStage, StringComparer.Ordinal)
            .ThenBy(error => error.NodeId, StringComparer.Ordinal)
            .ThenBy(error => error.Code, StringComparer.Ordinal)
            .ThenBy(error => error.Message, StringComparer.Ordinal));
        return report;
    }

    private static void AddEntityErrors(CaseGraph graph, List<GraphValidationError> errors)
    {
        var entityReport = CaseEntityContract.Validate(graph.Entities, graph.SuspectReferences);
        foreach (var message in entityReport.Errors.OrderBy(value => value, StringComparer.Ordinal))
        {
            var nodeId = ExtractQuotedNodeId(message) ?? "entities";
            Add(errors, "invalid_entity", nodeId, "blueprint", message, "entities", "repair entity contract");
        }
    }

    private static void AddDuplicateIdErrors(CaseGraph graph, List<GraphValidationError> errors)
    {
        var ids = graph.Entities.Select(node => (node.Id, "blueprint"))
            .Concat(graph.Facts.Select(node => (node.Id, "blueprint")))
            .Concat(graph.Events.Select(node => (node.Id, "timeline")))
            .Concat(graph.Sources.Select(node => (node.Id, "evidencePlan")))
            .Concat(graph.Observations.Select(node => (node.Id, "evidenceContent")))
            .Concat(graph.Derivations.Select(node => (node.Id, "proof")))
            .Concat(graph.Actions.Select(node => (node.Id, "rules")));

        foreach (var duplicate in ids
                     .Where(value => !string.IsNullOrWhiteSpace(value.Id))
                     .GroupBy(value => value.Id, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            Add(
                errors,
                "duplicate_node_id",
                duplicate.Key,
                duplicate.First().Item2,
                $"node id '{duplicate.Key}' is used {duplicate.Count()} times",
                "id",
                "assign a unique typed node id");
        }
    }

    private static void AddTypedNodeIdErrors(CaseGraph graph, List<GraphValidationError> errors)
    {
        AddPrefixErrors(graph.Facts.Select(node => node.Id), "fact.", "blueprint");
        AddPrefixErrors(graph.Events.Select(node => node.Id), "event.", "timeline");
        AddPrefixErrors(graph.Observations.Select(node => node.Id), "observation.", "evidenceContent");
        AddPrefixErrors(graph.Derivations.Select(node => node.Id), "derivation.", "proof");
        AddPrefixErrors(graph.Actions.Select(node => node.Id), "action.", "rules");

        void AddPrefixErrors(IEnumerable<string> ids, string prefix, string stage)
        {
            foreach (var id in ids.Where(id => string.IsNullOrWhiteSpace(id)
                                               || !id.StartsWith(prefix, StringComparison.Ordinal)))
            {
                Add(
                    errors,
                    "invalid_typed_node_id",
                    string.IsNullOrWhiteSpace(id) ? "<empty>" : id,
                    stage,
                    $"{stage} node '{id}' must use prefix '{prefix}'",
                    "id",
                    $"assign a stable '{prefix}' id");
            }
        }
    }

    private static void AddFactErrors(
        CaseGraph graph,
        HashSet<string> entityIds,
        HashSet<string> suspectIds,
        HashSet<string> eventIds,
        List<GraphValidationError> errors)
    {
        foreach (var fact in graph.Facts)
        {
            if (string.IsNullOrWhiteSpace(fact.Predicate))
            {
                Add(
                    errors,
                    "missing_fact_predicate",
                    fact.Id,
                    "blueprint",
                    $"fact '{fact.Id}' has no predicate",
                    "predicate",
                    "assign one canonical predicate");
            }

            var hasObject = !string.IsNullOrWhiteSpace(fact.ObjectId);
            var hasLiteral = fact.LiteralValue is not null;
            if (hasObject == hasLiteral)
            {
                Add(
                    errors,
                    "invalid_canonical_value",
                    fact.Id,
                    "blueprint",
                    $"fact '{fact.Id}' must have exactly one object or literal value",
                    "objectId/literalValue",
                    "retain exactly one canonical value");
            }

            if (!ReferenceExists(fact.SubjectId, entityIds, suspectIds))
            {
                Add(
                    errors,
                    "missing_fact_subject",
                    fact.Id,
                    "blueprint",
                    $"fact '{fact.Id}' references missing subject '{fact.SubjectId}'",
                    "subjectId",
                    "create or retarget the subject entity");
            }

            if (hasObject && !ReferenceExists(fact.ObjectId!, entityIds, suspectIds))
            {
                Add(
                    errors,
                    "missing_fact_object",
                    fact.Id,
                    "blueprint",
                    $"fact '{fact.Id}' references missing object '{fact.ObjectId}'",
                    "objectId",
                    "create or retarget the object entity");
            }

            if (hasLiteral && fact.LiteralType is null)
            {
                Add(
                    errors,
                    "missing_literal_type",
                    fact.Id,
                    "blueprint",
                    $"literal fact '{fact.Id}' must specify a value type",
                    "literalType",
                    "assign the canonical literal type");
            }

            if (fact.LiteralType == LiteralValueType.Duration && string.IsNullOrWhiteSpace(fact.LiteralUnit))
            {
                Add(
                    errors,
                    "missing_literal_unit",
                    fact.Id,
                    "blueprint",
                    $"duration fact '{fact.Id}' must specify a unit",
                    "literalUnit",
                    "assign a duration unit");
            }

            if (!string.IsNullOrWhiteSpace(fact.EventId) && !eventIds.Contains(fact.EventId))
            {
                Add(
                    errors,
                    "missing_fact_event",
                    fact.Id,
                    "timeline",
                    $"fact '{fact.Id}' references missing event '{fact.EventId}'",
                    "eventId",
                    "create or retarget the canonical event");
            }
        }

        foreach (var group in graph.Facts.GroupBy(
                     fact => $"{fact.SubjectId}\u001f{fact.Predicate}\u001f{fact.EventId}",
                     StringComparer.Ordinal))
        {
            var ordered = group.OrderBy(fact => fact.Id, StringComparer.Ordinal).ToList();
            var byValue = ordered.GroupBy(
                    fact => $"{fact.ObjectId}\u001f{fact.LiteralValue}\u001f{fact.LiteralType}\u001f{fact.LiteralUnit}",
                    StringComparer.Ordinal)
                .ToList();
            if (byValue.Count == 1 && ordered.Count > 1)
            {
                foreach (var duplicate in ordered.Skip(1))
                {
                    Add(
                        errors,
                        "duplicate_canonical_fact",
                        duplicate.Id,
                        "blueprint",
                        $"fact '{duplicate.Id}' duplicates canonical fact '{ordered[0].Id}'",
                        "id",
                        $"remove the duplicate and reference '{ordered[0].Id}'");
                }
            }
            else if (byValue.Count > 1 && !IsPotentiallyMultiValuedPredicate(ordered[0].Predicate))
            {
                var intentionalConflictIds = ordered
                    .Select(fact => fact.IntentionalConflictId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                if (intentionalConflictIds.Count == 1
                    && ordered.All(fact => fact.IntentionalConflictId == intentionalConflictIds[0]))
                    continue;
                foreach (var conflict in ordered)
                {
                    Add(
                        errors,
                        "conflicting_canonical_fact",
                        conflict.Id,
                        "blueprint",
                        $"fact '{conflict.Id}' conflicts with another value for predicate '{conflict.Predicate}' on '{conflict.SubjectId}'",
                        "objectId/literalValue",
                        "resolve the canonical value conflict");
                }
            }

            static bool IsPotentiallyMultiValuedPredicate(string predicate) =>
                predicate.Contains("event", StringComparison.OrdinalIgnoreCase)
                || predicate.Contains("access", StringComparison.OrdinalIgnoreCase)
                || predicate.Contains("entered", StringComparison.OrdinalIgnoreCase)
                || predicate.Contains("exited", StringComparison.OrdinalIgnoreCase)
                || predicate.Contains("called", StringComparison.OrdinalIgnoreCase)
                || predicate.Contains("messaged", StringComparison.OrdinalIgnoreCase)
                || predicate.Contains("visited", StringComparison.OrdinalIgnoreCase)
                || predicate.Contains("transaction", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void AddEventErrors(
        CaseGraph graph,
        HashSet<string> entityIds,
        HashSet<string> suspectIds,
        HashSet<string> factIds,
        List<GraphValidationError> errors)
    {
        foreach (var graphEvent in graph.Events)
        {
            foreach (var participantId in graphEvent.ParticipantIds.Where(id => !ReferenceExists(id, entityIds, suspectIds)))
            {
                Add(
                    errors,
                    "missing_event_participant",
                    graphEvent.Id,
                    "timeline",
                    $"event '{graphEvent.Id}' references missing participant '{participantId}'",
                    "participantIds",
                    "create or retarget the participant");
            }

            foreach (var factId in graphEvent.FactIds.Where(id => !factIds.Contains(id)))
            {
                Add(
                    errors,
                    "missing_event_fact",
                    graphEvent.Id,
                    "timeline",
                    $"event '{graphEvent.Id}' references missing fact '{factId}'",
                    "factIds",
                    "create or remove the fact reference");
            }

            if (graphEvent.Precision == TemporalPrecision.Window
                && (graphEvent.WindowEnd is null || graphEvent.WindowEnd < graphEvent.Timestamp))
            {
                Add(
                    errors,
                    "invalid_event_window",
                    graphEvent.Id,
                    "timeline",
                    $"window event '{graphEvent.Id}' must end at or after its start",
                    "windowEnd",
                    "repair the estimated event window");
            }
            else if (graphEvent.Precision != TemporalPrecision.Window && graphEvent.WindowEnd is not null)
            {
                Add(
                    errors,
                    "unexpected_event_window",
                    graphEvent.Id,
                    "timeline",
                    $"event '{graphEvent.Id}' has a window end but precision is '{graphEvent.Precision}'",
                    "precision/windowEnd",
                    "use Window precision or remove the window end");
            }
        }

        foreach (var duplicate in graph.Events
                     .GroupBy(
                         graphEvent => $"{graphEvent.Kind}\u001f{graphEvent.Timestamp:O}\u001f{string.Join(",", graphEvent.ParticipantIds.Order(StringComparer.Ordinal))}",
                         StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            var canonical = duplicate.OrderBy(graphEvent => graphEvent.Id, StringComparer.Ordinal).First();
            foreach (var repeated in duplicate.Where(graphEvent => graphEvent != canonical))
            {
                Add(
                    errors,
                    "duplicate_canonical_event",
                    repeated.Id,
                    "timeline",
                    $"event '{repeated.Id}' repeats canonical event '{canonical.Id}'; repeated timestamps must reference one event id",
                    "id",
                    $"reference '{canonical.Id}'");
            }
        }
    }

    private static void AddObservationErrors(
        CaseGraph graph,
        HashSet<string> factIds,
        HashSet<string> sourceIds,
        List<GraphValidationError> errors)
    {
        var facts = graph.Facts
            .GroupBy(fact => fact.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var sources = graph.Sources
            .GroupBy(source => source.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (var observation in graph.Observations)
        {
            if (!factIds.Contains(observation.FactId))
            {
                Add(
                    errors,
                    "missing_observation_fact",
                    observation.Id,
                    "evidenceContent",
                    $"observation '{observation.Id}' references missing fact '{observation.FactId}'",
                    "factId",
                    "create or retarget the observed fact");
            }

            if (!sourceIds.Contains(observation.SourceAssetId))
            {
                Add(
                    errors,
                    "missing_observation_source",
                    observation.Id,
                    "evidencePlan",
                    $"observation '{observation.Id}' references missing source asset '{observation.SourceAssetId}'",
                    "sourceAssetId",
                    "create or retarget the owning evidence source");
            }
            else if (sources.TryGetValue(observation.SourceAssetId, out var owningSource)
                     && !owningSource.ObservationIds.Contains(observation.Id, StringComparer.Ordinal))
            {
                Add(
                    errors,
                    "observation_not_owned_by_source",
                    observation.Id,
                    "evidencePlan",
                    $"observation '{observation.Id}' is not assigned to owning source '{observation.SourceAssetId}'",
                    "observationIds",
                    "assign the observation to its owning evidence source");
            }

            if (facts.TryGetValue(observation.FactId, out var fact)
                && fact.Visibility == FactVisibility.Private
                && observation.Visibility == FactVisibility.Public)
            {
                Add(
                    errors,
                    "invalid_visibility_transition",
                    observation.Id,
                    "evidenceContent",
                    $"public observation '{observation.Id}' cannot materialize private fact '{fact.Id}'",
                    "visibility",
                    "make the fact public or remove it from player evidence");
            }

            if (facts.TryGetValue(observation.FactId, out fact)
                && fact.Visibility == FactVisibility.Private
                && sources.TryGetValue(observation.SourceAssetId, out var source)
                && source.AvailableAt == ReachabilityState.Initial)
            {
                Add(
                    errors,
                    "private_fact_in_initial_evidence",
                    observation.Id,
                    "evidencePlan",
                    $"initial source '{source.Id}' materializes private fact '{fact.Id}' through observation '{observation.Id}'",
                    "sourceAssetId",
                    "remove the private fact or gate the source");
            }
        }
    }

    private static void AddSourceErrors(
        CaseGraph graph,
        HashSet<string> observationIds,
        HashSet<string> sourceIds,
        HashSet<string> actionIds,
        List<GraphValidationError> errors)
    {
        foreach (var source in graph.Sources)
        {
            foreach (var observationId in source.ObservationIds.Where(id => !observationIds.Contains(id)))
            {
                Add(
                    errors,
                    "missing_source_observation",
                    source.Id,
                    "evidencePlan",
                    $"source '{source.Id}' references missing observation '{observationId}'",
                    "observationIds",
                    "create or remove the observation reference");
            }

            var ownedFactIds = graph.Observations
                .Where(observation => observation.SourceAssetId == source.Id)
                .Select(observation => observation.FactId)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var claimedFactId in source.ClaimedFactIds.Where(id => !ownedFactIds.Contains(id)))
            {
                Add(
                    errors,
                    "unobserved_source_claim",
                    source.Id,
                    "evidenceContent",
                    $"source '{source.Id}' claims fact '{claimedFactId}' without an owned observation",
                    "claimedFactIds",
                    "assign an observation or remove the unsupported claim");
            }

            foreach (var prerequisiteId in source.RevealPrerequisiteIds
                         .Where(id => !sourceIds.Contains(id) && !actionIds.Contains(id)))
            {
                Add(
                    errors,
                    "missing_reveal_prerequisite",
                    source.Id,
                    "rules",
                    $"source '{source.Id}' references missing reveal prerequisite '{prerequisiteId}'",
                    "revealPrerequisiteIds",
                    "create or retarget the reveal prerequisite");
            }
        }

        foreach (var requiredId in graph.RequiredSourceIds.Where(id => !sourceIds.Contains(id)))
        {
            Add(
                errors,
                "missing_required_source",
                requiredId,
                "solution",
                $"required source '{requiredId}' does not exist",
                "requiredSourceIds",
                "create or remove the required source");
        }
    }

    private static void AddActionErrors(
        CaseGraph graph,
        HashSet<string> sourceIds,
        HashSet<string> actionIds,
        List<GraphValidationError> errors)
    {
        foreach (var action in graph.Actions)
        {
            foreach (var sourceId in action.PrerequisiteSourceIds.Concat(action.RevealsSourceIds)
                         .Where(id => !sourceIds.Contains(id)))
            {
                Add(
                    errors,
                    "missing_action_source",
                    action.Id,
                    "rules",
                    $"action '{action.Id}' references missing source '{sourceId}'",
                    "prerequisiteSourceIds/revealsSourceIds",
                    "create or retarget the evidence source");
            }

            foreach (var prerequisiteId in action.PrerequisiteActionIds.Where(id => !actionIds.Contains(id)))
            {
                Add(
                    errors,
                    "missing_action_prerequisite",
                    action.Id,
                    "rules",
                    $"action '{action.Id}' references missing action prerequisite '{prerequisiteId}'",
                    "prerequisiteActionIds",
                    "create or retarget the prerequisite action");
            }
        }
    }

    private static void AddDerivationErrors(
        CaseGraph graph,
        HashSet<string> factIds,
        HashSet<string> observationIds,
        HashSet<string> derivationIds,
        List<GraphValidationError> errors)
    {
        foreach (var derivation in graph.Derivations)
        {
            if (derivation.PremiseIds.Count == 0)
            {
                Add(
                    errors,
                    "missing_derivation_premise",
                    derivation.Id,
                    "proof",
                    $"derivation '{derivation.Id}' has no explicit premises",
                    "premiseIds",
                    "add factual or observational premises");
            }

            foreach (var premiseId in derivation.PremiseIds
                         .Where(id => !factIds.Contains(id) && !observationIds.Contains(id) && !derivationIds.Contains(id)))
            {
                Add(
                    errors,
                    "missing_derivation_premise",
                    derivation.Id,
                    "proof",
                    $"derivation '{derivation.Id}' references missing premise '{premiseId}'",
                    "premiseIds",
                    "create or retarget the premise");
            }

            if (!factIds.Contains(derivation.ConclusionFactId))
            {
                Add(
                    errors,
                    "missing_derivation_conclusion",
                    derivation.Id,
                    "proof",
                    $"derivation '{derivation.Id}' references missing conclusion fact '{derivation.ConclusionFactId}'",
                    "conclusionFactId",
                    "create or retarget the conclusion fact");
            }

            if (!string.IsNullOrWhiteSpace(derivation.SupportsSuspectId)
                && !graph.SuspectReferences.Any(reference =>
                    reference.SuspectId == derivation.SupportsSuspectId))
            {
                Add(
                    errors,
                    "missing_derivation_suspect",
                    derivation.Id,
                    "proof",
                    $"derivation '{derivation.Id}' references missing suspect '{derivation.SupportsSuspectId}'",
                    "supportsSuspectId",
                    "create or retarget the suspect reference");
            }
        }
    }

    private static void AddTemporalConstraintErrors(
        CaseGraph graph,
        HashSet<string> eventIds,
        List<GraphValidationError> errors)
    {
        var events = graph.Events
            .GroupBy(graphEvent => graphEvent.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (var constraint in graph.EventOrderConstraints)
        {
            if (!eventIds.Contains(constraint.BeforeEventId) || !eventIds.Contains(constraint.AfterEventId))
            {
                Add(
                    errors,
                    "missing_order_event",
                    constraint.Id,
                    "timeline",
                    $"ordering constraint '{constraint.Id}' references a missing event",
                    "beforeEventId/afterEventId",
                    "create or retarget the constrained event");
                continue;
            }

            var before = events[constraint.BeforeEventId];
            var after = events[constraint.AfterEventId];
            if (Latest(before) + constraint.MinimumGap > Earliest(after))
            {
                Add(
                    errors,
                    "impossible_event_order",
                    constraint.Id,
                    "timeline",
                    $"ordering constraint '{constraint.Id}' is impossible between '{before.Id}' and '{after.Id}'",
                    "minimumGap",
                    "repair the event timestamps or ordering constraint");
            }
        }

        foreach (var constraint in graph.TravelWindows)
        {
            if (!eventIds.Contains(constraint.FromEventId) || !eventIds.Contains(constraint.ToEventId))
            {
                Add(
                    errors,
                    "missing_travel_event",
                    constraint.Id,
                    "timeline",
                    $"travel constraint '{constraint.Id}' references a missing event",
                    "fromEventId/toEventId",
                    "create or retarget the travel event");
                continue;
            }

            var from = events[constraint.FromEventId];
            var to = events[constraint.ToEventId];
            if (Latest(from) + constraint.MinimumTravelTime > Earliest(to))
            {
                Add(
                    errors,
                    "impossible_travel_window",
                    constraint.Id,
                    "timeline",
                    $"travel constraint '{constraint.Id}' allows less than {constraint.MinimumTravelTime} between '{from.Id}' and '{to.Id}'",
                    "minimumTravelTime",
                    "repair timestamps or minimum travel time");
            }
        }
    }

    private static void AddDerivationCycleErrors(CaseGraph graph, List<GraphValidationError> errors)
    {
        var producers = graph.Derivations
            .GroupBy(derivation => derivation.ConclusionFactId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(derivation => derivation.Id).ToArray(),
                StringComparer.Ordinal);
        var dependencies = graph.Derivations.ToDictionary(
            derivation => derivation.Id,
            derivation => derivation.PremiseIds
                .Where(id => graph.Derivations.Any(candidate => candidate.Id == id))
                .Concat(derivation.PremiseIds
                    .Where(producers.ContainsKey)
                    .SelectMany(id => producers[id]))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            StringComparer.Ordinal);

        foreach (var nodeId in FindCycleNodes(dependencies))
        {
            Add(
                errors,
                "circular_derivation",
                nodeId,
                "proof",
                $"derivation '{nodeId}' participates in a circular proof dependency",
                "premiseIds",
                "remove or replace the circular premise");
        }
    }

    private static void AddRevealCycleErrors(CaseGraph graph, List<GraphValidationError> errors)
    {
        var dependencies = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var source in graph.Sources)
        {
            var revealingActions = graph.Actions
                .Where(action => action.RevealsSourceIds.Contains(source.Id, StringComparer.Ordinal))
                .Select(action => action.Id);
            dependencies[source.Id] = source.RevealPrerequisiteIds
                .Concat(revealingActions)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
        }

        foreach (var action in graph.Actions)
        {
            dependencies[action.Id] = action.PrerequisiteActionIds
                .Concat(action.PrerequisiteSourceIds)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
        }

        foreach (var nodeId in FindCycleNodes(dependencies))
        {
            Add(
                errors,
                "circular_reveal_dependency",
                nodeId,
                nodeId.StartsWith("action.", StringComparison.Ordinal) ? "rules" : "evidencePlan",
                $"node '{nodeId}' participates in a circular reveal dependency",
                "prerequisites",
                "remove or replace the circular reveal prerequisite");
        }
    }

    private static IReadOnlyList<string> FindCycleNodes(IReadOnlyDictionary<string, string[]> dependencies)
    {
        var state = new Dictionary<string, int>(StringComparer.Ordinal);
        var stack = new List<string>();
        var cycleNodes = new HashSet<string>(StringComparer.Ordinal);

        void Visit(string nodeId)
        {
            if (state.GetValueOrDefault(nodeId) == 2)
                return;
            if (state.GetValueOrDefault(nodeId) == 1)
            {
                var start = stack.FindIndex(id => id == nodeId);
                foreach (var id in stack.Skip(Math.Max(start, 0)))
                    cycleNodes.Add(id);
                cycleNodes.Add(nodeId);
                return;
            }

            state[nodeId] = 1;
            stack.Add(nodeId);
            if (dependencies.TryGetValue(nodeId, out var nodeDependencies))
            {
                foreach (var dependency in nodeDependencies.Where(dependencies.ContainsKey))
                    Visit(dependency);
            }
            stack.RemoveAt(stack.Count - 1);
            state[nodeId] = 2;
        }

        foreach (var nodeId in dependencies.Keys.Order(StringComparer.Ordinal))
            Visit(nodeId);
        return cycleNodes.Order(StringComparer.Ordinal).ToList();
    }

    private static bool ReferenceExists(string id, HashSet<string> entityIds, HashSet<string> suspectIds) =>
        entityIds.Contains(id) || suspectIds.Contains(id);

    private static DateTimeOffset Earliest(CanonicalEvent graphEvent) => graphEvent.Timestamp;

    private static DateTimeOffset Latest(CanonicalEvent graphEvent) =>
        graphEvent.Precision == TemporalPrecision.Window
            ? graphEvent.WindowEnd ?? graphEvent.Timestamp
            : graphEvent.Timestamp;

    private static string? ExtractQuotedNodeId(string message)
    {
        var start = message.IndexOf('\'');
        if (start < 0)
            return null;
        var end = message.IndexOf('\'', start + 1);
        return end > start ? message[(start + 1)..end] : null;
    }

    private static void Add(
        ICollection<GraphValidationError> errors,
        string code,
        string nodeId,
        string stage,
        string message,
        string property,
        string operation) =>
        errors.Add(new GraphValidationError(
            code,
            nodeId,
            stage,
            message,
            new GraphRepairTarget(stage, nodeId, property, operation)));
}
