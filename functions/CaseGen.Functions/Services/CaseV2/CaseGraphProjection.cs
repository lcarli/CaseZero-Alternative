using System.Text.RegularExpressions;

namespace CaseGen.Functions.Services.CaseV2;

/// <summary>
/// Transitional adapter for the existing generator. It may create or refresh only a
/// TransitionalClueLadder graph and never overwrites a hand-authored CaseGraph.
/// </summary>
public static partial class CaseGraphProjection
{
    public static CaseGraph ProjectTransitionalClueLadder(CaseDraft draft)
    {
        if (!draft.CaseGraph.IsEmpty
            && draft.CaseGraph.Origin is CaseGraphOrigin.HandAuthored or CaseGraphOrigin.Generated)
            return draft.CaseGraph;

        var graph = new CaseGraph { Origin = CaseGraphOrigin.TransitionalClueLadder };
        var suspectIds = draft.SuspectStubs.Select(suspect => suspect.Id)
            .Concat(draft.SuspectFull.Select(suspect => suspect.Id))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (!string.IsNullOrWhiteSpace(draft.CulpritId) && !suspectIds.Contains(draft.CulpritId, StringComparer.Ordinal))
            suspectIds.Add(draft.CulpritId);

        foreach (var suspectId in suspectIds.Order(StringComparer.Ordinal))
        {
            var suffix = IdSuffix(suspectId);
            var personId = $"person.{suffix}";
            graph.Entities.Add(new CaseEntity
            {
                Id = personId,
                Kind = EntityKind.Person,
                DisplayName = draft.SuspectFull.FirstOrDefault(suspect => suspect.Id == suspectId)?.Name
                              ?? draft.SuspectStubs.FirstOrDefault(suspect => suspect.Id == suspectId)?.Name
                              ?? suspectId
            });
            graph.SuspectReferences.Add(new SuspectEntityReference
            {
                SuspectId = suspectId,
                PersonEntityId = personId
            });
        }

        graph.Entities.Add(new CaseEntity
        {
            Id = "organization.case_context",
            Kind = EntityKind.Organization,
            DisplayName = string.IsNullOrWhiteSpace(draft.Metadata.Title) ? "Case context" : draft.Metadata.Title
        });

        AddSources(draft, graph);
        var claims = draft.EvidenceGraph.Claims
            .GroupBy(claim => claim.ClueId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var culpritFactBySuspect = new Dictionary<string, string>(StringComparer.Ordinal);
        var observationIdsByClue = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var clue in draft.Blueprint.ClueLadder.OrderBy(clue => clue.Id, StringComparer.Ordinal))
        {
            var clueSuffix = IdSuffix(clue.Id);
            var subjectId = ResolvePersonId(graph, clue.SupportsSuspectId) ?? "organization.case_context";
            var factId = $"fact.clue_{clueSuffix}";
            graph.Facts.Add(new CanonicalFact
            {
                Id = factId,
                Predicate = $"{(string.IsNullOrWhiteSpace(clue.Role) ? "supportsCase" : clue.Role)}_{clueSuffix}",
                SubjectId = subjectId,
                LiteralValue = clue.Discovery,
                LiteralType = LiteralValueType.String,
                Visibility = FactVisibility.Public
            });

            var assetIds = claims.GetValueOrDefault(clue.Id)?.AssetIds
                .Where(id => graph.Sources.Any(source => source.Id == id))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray() ?? Array.Empty<string>();
            foreach (var assetId in assetIds)
            {
                var observationId = $"observation.{clueSuffix}_{IdSuffix(assetId)}";
                graph.Observations.Add(new EvidenceObservation
                {
                    Id = observationId,
                    FactId = factId,
                    SourceAssetId = assetId,
                    Fidelity = clue.SourceType.Equals("witness", StringComparison.OrdinalIgnoreCase)
                        ? ObservationFidelity.Testimony
                        : clue.SourceType.Equals("circumstantial", StringComparison.OrdinalIgnoreCase)
                            ? ObservationFidelity.Circumstantial
                            : ObservationFidelity.DirectRecord,
                    Reliability = ObservationReliability.Verified,
                    Visibility = FactVisibility.Public
                });
                var source = graph.Sources.First(source => source.Id == assetId);
                source.ObservationIds.Add(observationId);
                source.ClaimedFactIds.Add(factId);
                if (!observationIdsByClue.TryGetValue(clue.Id, out var clueObservations))
                    observationIdsByClue[clue.Id] = clueObservations = new List<string>();
                clueObservations.Add(observationId);
            }
        }

        AddCulpritDerivations(draft, graph, observationIdsByClue, culpritFactBySuspect);
        AddDecoyArcs(draft, graph);
        AddAssetSpecs(draft, graph);
        AddForensicActions(draft, graph);
        graph.RequiredSourceIds = draft.RequiredEvidenceIds
            .Where(id => graph.Sources.Any(source => source.Id == id))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        graph.RequiredAnalysisIds = draft.RequiredAnalysisIds
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        draft.CaseGraph = graph;
        return graph;
    }

    private static void AddCulpritDerivations(
        CaseDraft draft,
        CaseGraph graph,
        IReadOnlyDictionary<string, List<string>> observationIdsByClue,
        IDictionary<string, string> culpritFactBySuspect)
    {
        if (string.IsNullOrWhiteSpace(draft.CulpritId))
            return;
        var culpritFactId = GetOrAddCulpritFact(graph, culpritFactBySuspect, draft.CulpritId);
        var culpritClues = draft.Blueprint.ClueLadder
            .Where(clue => clue.SupportsSuspectId == draft.CulpritId)
            .ToList();
        if (DifficultyProfileCatalog.Get(draft).AllEvidenceInitial)
        {
            foreach (var clue in culpritClues)
            foreach (var observationId in observationIdsByClue.GetValueOrDefault(clue.Id) ?? [])
            {
                graph.Derivations.Add(new ProofDerivation
                {
                    Id = $"derivation.{IdSuffix(clue.Id)}_{IdSuffix(observationId)}",
                    PremiseIds = { observationId },
                    Rule = RuleFor(clue),
                    ConclusionFactId = culpritFactId,
                    SupportsSuspectId = draft.CulpritId,
                    IsCulpritConclusion = true
                });
            }
            return;
        }

        var bridgePremises = culpritClues
            .Where(clue => clue.Role is "identity" or "action")
            .SelectMany(clue => (observationIdsByClue.GetValueOrDefault(clue.Id) ?? [])
                .Select(observationId => (Clue: clue, ObservationId: observationId)))
            .DistinctBy(item => item.ObservationId, StringComparer.Ordinal)
            .OrderBy(item => item.ObservationId, StringComparer.Ordinal)
            .ToArray();
        foreach (var clue in culpritClues.Where(clue =>
                     clue.SourceType.Equals("forensic", StringComparison.OrdinalIgnoreCase)))
        foreach (var forensicObservationId in observationIdsByClue.GetValueOrDefault(clue.Id) ?? [])
        foreach (var bridge in bridgePremises)
        {
            var branchSuffix = $"{IdSuffix(bridge.Clue.Id)}_{IdSuffix(bridge.ObservationId)}_{IdSuffix(forensicObservationId)}";
            var bridgeFactId = $"fact.proof_bridge_{branchSuffix}";
            var joinedFactId = $"fact.proof_join_{branchSuffix}";
            var bridgeDerivationId = $"derivation.proof_bridge_{branchSuffix}";
            var joinDerivationId = $"derivation.proof_join_{branchSuffix}";
            graph.Facts.Add(new CanonicalFact
            {
                Id = bridgeFactId,
                Predicate = $"proofBridge_{branchSuffix}",
                SubjectId = ResolvePersonId(graph, draft.CulpritId) ?? draft.CulpritId,
                LiteralValue = "true",
                LiteralType = LiteralValueType.Boolean
            });
            graph.Facts.Add(new CanonicalFact
            {
                Id = joinedFactId,
                Predicate = $"forensicJoin_{branchSuffix}",
                SubjectId = ResolvePersonId(graph, draft.CulpritId) ?? draft.CulpritId,
                LiteralValue = "true",
                LiteralType = LiteralValueType.Boolean
            });
            graph.Derivations.Add(new ProofDerivation
            {
                Id = bridgeDerivationId,
                PremiseIds = { bridge.ObservationId },
                Rule = RuleFor(bridge.Clue),
                ConclusionFactId = bridgeFactId
            });
            graph.Derivations.Add(new ProofDerivation
            {
                Id = joinDerivationId,
                PremiseIds = { bridgeDerivationId, forensicObservationId },
                Rule = DerivationRule.CrossSourceCorroboration,
                ConclusionFactId = joinedFactId
            });
            graph.Derivations.Add(new ProofDerivation
            {
                Id = $"derivation.culprit_{branchSuffix}",
                PremiseIds = { joinDerivationId },
                Rule = DerivationRule.ActionAttribution,
                ConclusionFactId = culpritFactId,
                SupportsSuspectId = draft.CulpritId,
                IsCulpritConclusion = true
            });
        }
    }

    private static void AddSources(CaseDraft draft, CaseGraph graph)
    {
        foreach (var asset in draft.AssetStubs.OrderBy(asset => asset.Id, StringComparer.Ordinal))
        {
            AddSource(
                graph,
                asset.Id,
                EvidenceSourceKind.Asset,
                asset.Visibility.Equals("initial", StringComparison.OrdinalIgnoreCase)
                    ? ReachabilityState.Initial
                    : ReachabilityState.AfterAction);
        }

        foreach (var asset in draft.AssetFull.OrderBy(asset => asset.Id, StringComparer.Ordinal))
        {
            AddSource(
                graph,
                asset.Id,
                EvidenceSourceKind.Asset,
                asset.Visibility.Equals("initial", StringComparison.OrdinalIgnoreCase)
                    ? ReachabilityState.Initial
                    : ReachabilityState.AfterAction);
        }

        foreach (var asset in draft.ResultAssets.OrderBy(asset => asset.Id, StringComparer.Ordinal))
            AddSource(graph, asset.Id, EvidenceSourceKind.Asset, ReachabilityState.AfterForensic);
        foreach (var email in draft.FollowUpEmails.OrderBy(email => email.Id, StringComparer.Ordinal))
        {
            AddSource(
                graph,
                email.Id,
                EvidenceSourceKind.Email,
                email.Visibility.Equals("initial", StringComparison.OrdinalIgnoreCase)
                    ? ReachabilityState.Initial
                    : ReachabilityState.AfterAction);
        }
        foreach (var email in draft.ResultEmails.OrderBy(email => email.Id, StringComparer.Ordinal))
            AddSource(graph, email.Id, EvidenceSourceKind.Email, ReachabilityState.AfterForensic);
    }

    private static void AddSource(
        CaseGraph graph,
        string id,
        EvidenceSourceKind kind,
        ReachabilityState availableAt)
    {
        if (string.IsNullOrWhiteSpace(id) || graph.Sources.Any(source => source.Id == id))
            return;
        graph.Sources.Add(new EvidenceSourceNode
        {
            Id = id,
            Kind = kind,
            AvailableAt = availableAt,
            EvidentiaryOriginId = id
        });
    }

    private static string GetOrAddCulpritFact(
        CaseGraph graph,
        IDictionary<string, string> factsBySuspect,
        string suspectId)
    {
        if (factsBySuspect.TryGetValue(suspectId, out var existing))
            return existing;
        var factId = $"fact.culprit_{IdSuffix(suspectId)}";
        graph.Facts.Add(new CanonicalFact
        {
            Id = factId,
            Predicate = "isCulprit",
            SubjectId = ResolvePersonId(graph, suspectId) ?? suspectId,
            LiteralValue = "true",
            LiteralType = LiteralValueType.Boolean,
            Visibility = FactVisibility.Public
        });
        factsBySuspect[suspectId] = factId;
        return factId;
    }

    private static void AddDecoyArcs(CaseDraft draft, CaseGraph graph)
    {
        foreach (var redHerring in draft.Blueprint.RedHerrings.OrderBy(item => item.SuspectId, StringComparer.Ordinal))
        {
            var personId = ResolvePersonId(graph, redHerring.SuspectId);
            if (personId is null)
                continue;
            var suspectSuffix = IdSuffix(redHerring.SuspectId);
            var suspicionFactId = $"fact.decoy_suspicion_{suspectSuffix}";
            graph.Facts.Add(new CanonicalFact
            {
                Id = suspicionFactId,
                Predicate = "decoySuspicion",
                SubjectId = personId,
                LiteralValue = redHerring.Suspicion,
                LiteralType = LiteralValueType.String
            });
            var suspicionObservationIds = new List<string>();
            foreach (var assetId in draft.AssetStubs
                         .Where(asset => asset.IntroducesRedHerringSuspectIds.Contains(redHerring.SuspectId, StringComparer.Ordinal))
                         .Select(asset => asset.Id)
                         .Where(id => graph.Sources.Any(source => source.Id == id))
                         .Order(StringComparer.Ordinal))
            {
                var suffix = $"{suspectSuffix}_{IdSuffix(assetId)}";
                var observationId = $"observation.decoy_suspicion_{suffix}";
                graph.Observations.Add(new EvidenceObservation
                {
                    Id = observationId,
                    FactId = suspicionFactId,
                    SourceAssetId = assetId,
                    Fidelity = ObservationFidelity.Circumstantial
                });
                AddObservationOwnership(graph, assetId, observationId, suspicionFactId);
                suspicionObservationIds.Add(observationId);
            }

            var verificationFactId = $"fact.decoy_verification_{suspectSuffix}";
            graph.Facts.Add(new CanonicalFact
            {
                Id = verificationFactId,
                Predicate = "decoyVerification",
                SubjectId = personId,
                LiteralValue = redHerring.Verification,
                LiteralType = LiteralValueType.String
            });
            var verificationObservationIds = new List<string>();
            foreach (var assetId in draft.AssetStubs
                         .Where(asset => asset.ResolvesRedHerringSuspectIds.Contains(redHerring.SuspectId, StringComparer.Ordinal))
                         .Select(asset => asset.Id)
                         .Where(id => graph.Sources.Any(source => source.Id == id))
                         .Order(StringComparer.Ordinal))
            {
                var suffix = $"{suspectSuffix}_{IdSuffix(assetId)}";
                var observationId = $"observation.decoy_verification_{suffix}";
                graph.Observations.Add(new EvidenceObservation
                {
                    Id = observationId,
                    FactId = verificationFactId,
                    SourceAssetId = assetId
                });
                AddObservationOwnership(graph, assetId, observationId, verificationFactId);
                verificationObservationIds.Add(observationId);
            }

            var conclusionFactId = $"fact.excluded_{IdSuffix(redHerring.SuspectId)}";
            var derivationId = $"derivation.exclude_{IdSuffix(redHerring.SuspectId)}";
            graph.Facts.Add(new CanonicalFact
            {
                Id = conclusionFactId,
                Predicate = "excludedFromCrime",
                SubjectId = personId,
                LiteralValue = "true",
                LiteralType = LiteralValueType.Boolean
            });
            graph.Derivations.Add(new ProofDerivation
            {
                Id = derivationId,
                PremiseIds = verificationObservationIds.ToList(),
                Rule = DerivationRule.Exclusion,
                ConclusionFactId = conclusionFactId,
                SupportsSuspectId = redHerring.SuspectId
            });
            graph.DecoyArcs.Add(new DecoyArc
            {
                SuspectId = redHerring.SuspectId,
                SuspicionObservationIds = suspicionObservationIds,
                VerificationObservationIds = verificationObservationIds,
                ResolutionDerivationId = derivationId,
                MustBePlayerReachable = true
            });
        }
    }

    private static void AddAssetSpecs(CaseDraft draft, CaseGraph graph)
    {
        foreach (var stub in draft.AssetStubs.OrderBy(asset => asset.Id, StringComparer.Ordinal))
        {
            foreach (var objectId in stub.ContainedObjectIds)
            {
                if (graph.Entities.Any(entity => entity.Id == objectId))
                    continue;
                graph.Entities.Add(new CaseEntity
                {
                    Id = objectId,
                    Kind = EntityKindFor(objectId),
                    DisplayName = $"{stub.Title} — {objectId}"
                });
            }
            var objectType = EvidenceAssetContract.InferContainedObjectType(stub.Type, stub.LayoutHint);
            graph.AssetSpecs.Add(new EvidenceAssetSpec
            {
                Id = stub.Id,
                ArchetypeId = stub.ArchetypeId,
                AssetType = stub.Type,
                ObservationIds = graph.Observations
                    .Where(observation => observation.SourceAssetId == stub.Id)
                    .Select(observation => observation.Id)
                    .Order(StringComparer.Ordinal)
                    .ToList(),
                ContainedObjectIds = stub.ContainedObjectIds.Distinct(StringComparer.Ordinal).ToList(),
                ContainedObjectTypes = stub.ContainedObjectIds
                    .Distinct(StringComparer.Ordinal)
                    .ToDictionary(id => id, _ => objectType, StringComparer.Ordinal),
                Visibility = stub.Visibility.Equals("initial", StringComparison.OrdinalIgnoreCase)
                    ? VisibilityGate.Initial
                    : VisibilityGate.AfterAction,
                LayoutId = stub.LayoutHint
            });
        }
    }

    private static void AddForensicActions(CaseDraft draft, CaseGraph graph)
    {
        foreach (var outcome in draft.ForensicFull
                     .Where(outcome => !string.IsNullOrWhiteSpace(outcome.ResultAssetId)
                                       || !string.IsNullOrWhiteSpace(outcome.ResultEmailId))
                     .OrderBy(outcome => outcome.InputAssetId, StringComparer.Ordinal)
                     .ThenBy(outcome => outcome.AnalysisType, StringComparer.Ordinal))
        {
            var actionId = $"action.analysis_{IdSuffix(outcome.InputAssetId)}_{IdSuffix(outcome.AnalysisType)}";
            var resultIds = new[] { outcome.ResultAssetId, outcome.ResultEmailId }
                .Where(id => !string.IsNullOrWhiteSpace(id) && graph.Sources.Any(source => source.Id == id))
                .Select(id => id!)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var originId = outcome.ResultAssetId ?? outcome.ResultEmailId ?? actionId;
            graph.Actions.Add(new InvestigationAction
            {
                Id = actionId,
                Kind = InvestigationActionKind.RequestAnalysis,
                CompletesAt = ReachabilityState.AfterForensic,
                PrerequisiteSourceIds = { outcome.InputAssetId },
                RevealsSourceIds = resultIds.ToList(),
                AnalysisId = $"{outcome.InputAssetId}:{outcome.AnalysisType}",
                Required = draft.RequiredAnalysisIds.Contains(
                    $"{outcome.InputAssetId}:{outcome.AnalysisType}",
                    StringComparer.Ordinal)
            });
            foreach (var resultId in resultIds)
            {
                var source = graph.Sources.First(item => item.Id == resultId);
                source.RevealPrerequisiteIds.Add(actionId);
                source.EvidentiaryOriginId = originId;
            }

            var stub = draft.ForensicStubs.FirstOrDefault(candidate =>
                candidate.InputAssetId == outcome.InputAssetId
                && candidate.AnalysisType == outcome.AnalysisType);
            if (stub is null)
                continue;
            var producedObservationIds = graph.Observations
                .Where(observation => observation.SourceAssetId == outcome.ResultAssetId)
                .Select(observation => observation.Id)
                .Order(StringComparer.Ordinal)
                .ToList();
            for (var index = 0; index < producedObservationIds.Count; index++)
            {
                var observation = graph.Observations.First(item => item.Id == producedObservationIds[index]);
                if (stub.ProducedProperties.Count > 0)
                    observation.ForensicProperty = stub.ProducedProperties[Math.Min(index, stub.ProducedProperties.Count - 1)];
            }
            graph.ForensicTransforms.Add(new ForensicTransform
            {
                Id = $"forensic.{IdSuffix(outcome.InputAssetId)}_{IdSuffix(outcome.AnalysisType)}",
                MethodId = outcome.AnalysisType,
                InputAssetId = outcome.InputAssetId,
                InputObjectId = stub.InputObjectId,
                ReferenceSampleObjectId = stub.ReferenceSampleObjectId,
                ChainOfCustodyObservationId = stub.ChainOfCustodyObservationId,
                ProducedObservationIds = producedObservationIds,
                ProducedProperties = stub.ProducedProperties.Distinct().ToList(),
                LimitationKeys = stub.LimitationKeys.Distinct(StringComparer.Ordinal).ToList(),
                ResultAssetId = outcome.ResultAssetId,
                ResultEmailId = outcome.ResultEmailId,
                ResultLayoutId = stub.ResultLayoutId
            });
        }
    }

    private static void AddObservationOwnership(CaseGraph graph, string assetId, string observationId, string factId)
    {
        var source = graph.Sources.First(source => source.Id == assetId);
        source.ObservationIds.Add(observationId);
        source.ClaimedFactIds.Add(factId);
    }

    private static EntityKind EntityKindFor(string id) =>
        id.Split('.', 2)[0] switch
        {
            "device" => EntityKind.Device,
            "account" => EntityKind.Account,
            "session" => EntityKind.Session,
            "communication" => EntityKind.Communication,
            "transaction" => EntityKind.Transaction,
            "object" => EntityKind.PhysicalObject,
            _ => EntityKind.Document
        };

    private static string? ResolvePersonId(CaseGraph graph, string? suspectId) =>
        graph.SuspectReferences.FirstOrDefault(reference => reference.SuspectId == suspectId)?.PersonEntityId;

    private static DerivationRule RuleFor(CanonicalClue clue) =>
        clue.Role.Trim().ToLowerInvariant() switch
        {
            "identity" => DerivationRule.IdentityMatch,
            "action" => DerivationRule.ActionAttribution,
            "forensicattribution" => DerivationRule.CrossSourceCorroboration,
            "motive" => DerivationRule.BenefitAttribution,
            _ => DerivationRule.TimelineCorrelation
        };

    private static string IdSuffix(string value)
    {
        var suffix = value.Contains('.') ? value[(value.IndexOf('.') + 1)..] : value;
        suffix = InvalidIdCharacters().Replace(suffix.ToLowerInvariant(), "_").Trim('_');
        return string.IsNullOrWhiteSpace(suffix) ? "node" : suffix;
    }

    [GeneratedRegex("[^a-z0-9_]+", RegexOptions.CultureInvariant)]
    private static partial Regex InvalidIdCharacters();
}
