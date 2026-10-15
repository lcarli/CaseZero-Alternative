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
        AddCaseBibleCanonicalCore(draft, graph);
        var suspectIds = draft.SuspectStubs.Select(suspect => suspect.Id)
            .Concat(draft.SuspectFull.Select(suspect => suspect.Id))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (!string.IsNullOrWhiteSpace(draft.CulpritId) && !suspectIds.Contains(draft.CulpritId, StringComparer.Ordinal))
            suspectIds.Add(draft.CulpritId);

        foreach (var suspectId in suspectIds.Order(StringComparer.Ordinal))
        {
            var canonicalPerson = draft.CaseBible?.People.FirstOrDefault(person =>
                string.Equals(person.SuspectId, suspectId, StringComparison.Ordinal));
            var personId = canonicalPerson?.Id ?? $"person.{IdSuffix(suspectId)}";
            if (!graph.Entities.Any(entity => entity.Id == personId))
            {
                graph.Entities.Add(new CaseEntity
                {
                    Id = personId,
                    Kind = EntityKind.Person,
                    DisplayName = canonicalPerson?.Name
                                  ?? draft.SuspectFull.FirstOrDefault(suspect => suspect.Id == suspectId)?.Name
                                  ?? draft.SuspectStubs.FirstOrDefault(suspect => suspect.Id == suspectId)?.Name
                                  ?? suspectId
                });
            }
            if (!graph.SuspectReferences.Any(reference => reference.SuspectId == suspectId))
            {
                graph.SuspectReferences.Add(new SuspectEntityReference
                {
                    SuspectId = suspectId,
                    PersonEntityId = personId
                });
            }
        }

        if (!graph.Entities.Any(entity => entity.Id == "organization.case_context"))
        {
            graph.Entities.Add(new CaseEntity
            {
                Id = "organization.case_context",
                Kind = EntityKind.Organization,
                DisplayName = string.IsNullOrWhiteSpace(draft.Metadata.Title) ? "Case context" : draft.Metadata.Title
            });
        }

        AddSources(draft, graph);
        var claims = draft.EvidenceGraph.Claims
            .GroupBy(claim => claim.ClueId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var culpritFactBySuspect = new Dictionary<string, string>(StringComparer.Ordinal);
        var observationIdsByClue = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var clue in draft.Blueprint.ClueLadder.OrderBy(clue => clue.Id, StringComparer.Ordinal))
        {
            if (AddCanonicalClueObservations(
                    draft,
                    graph,
                    clue,
                    claims.GetValueOrDefault(clue.Id),
                    observationIdsByClue))
            {
                continue;
            }
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

    public static CaseGraph RefreshGeneratedGraph(CaseDraft draft)
    {
        if (draft.CaseGraph.Origin == CaseGraphOrigin.HandAuthored)
            return draft.CaseGraph;

        draft.CaseGraph = new CaseGraph { Origin = CaseGraphOrigin.TransitionalClueLadder };
        var graph = ProjectTransitionalClueLadder(draft);
        SynchronizePhotoDetails(draft, graph);
        return graph;
    }

    private static void SynchronizePhotoDetails(CaseDraft draft, CaseGraph graph)
    {
        var facts = graph.Facts.ToDictionary(fact => fact.Id, StringComparer.Ordinal);
        foreach (var spec in graph.AssetSpecs.Where(spec => spec.AssetType == "photo"))
        {
            var asset = draft.AssetFull.FirstOrDefault(candidate => candidate.Id == spec.Id);
            if (asset is null)
                continue;
            var requiredValues = spec.ObservationIds
                .Select(id => graph.Observations.FirstOrDefault(observation => observation.Id == id))
                .Where(observation => observation is not null && facts.ContainsKey(observation.FactId))
                .Select(observation => facts[observation!.FactId].CanonicalValue)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .Where(value => !EvidenceContentText.Extract(asset).Contains(
                    value,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (requiredValues.Length > 0)
                asset.Body = $"{asset.Body}\nRequired visible details: {string.Join("; ", requiredValues)}";
        }
    }

    private static void AddCaseBibleCanonicalCore(CaseDraft draft, CaseGraph graph)
    {
        var bible = draft.CaseBible;
        if (bible is null)
            return;

        foreach (var person in bible.People)
        {
            graph.Entities.Add(new CaseEntity
            {
                Id = person.Id,
                Kind = EntityKind.Person,
                DisplayName = person.Name,
                Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["age"] = person.Age.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["occupation"] = person.Occupation,
                    ["publicRole"] = person.PublicRole
                }
            });
            if (!string.IsNullOrWhiteSpace(person.SuspectId))
            {
                graph.SuspectReferences.Add(new SuspectEntityReference
                {
                    SuspectId = person.SuspectId,
                    PersonEntityId = person.Id
                });
            }
        }
        foreach (var institution in bible.Institutions)
        {
            graph.Entities.Add(new CaseEntity
            {
                Id = institution.Id,
                Kind = EntityKind.Organization,
                DisplayName = institution.Name,
                Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["kind"] = institution.Kind,
                    ["emailDomain"] = institution.EmailDomain ?? string.Empty
                }
            });
        }
        foreach (var location in bible.Locations)
        {
            graph.Entities.Add(new CaseEntity
            {
                Id = location.Id,
                Kind = EntityKind.Location,
                DisplayName = location.Name,
                Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["kind"] = location.Kind,
                    ["address"] = string.Join(", ", new[]
                    {
                        location.Address.Line1,
                        location.Address.Line2,
                        location.Address.City,
                        location.Address.Region,
                        location.Address.PostalCode,
                        location.Address.Country
                    }.Where(value => !string.IsNullOrWhiteSpace(value)))
                }
            });
        }
        foreach (var device in bible.Devices)
        {
            graph.Entities.Add(new CaseEntity
            {
                Id = device.Id,
                Kind = EntityKind.Device,
                DisplayName = string.Join(" ", new[] { device.Manufacturer, device.Model, device.Identifier }
                    .Where(value => !string.IsNullOrWhiteSpace(value))),
                Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["type"] = device.Type,
                    ["identifier"] = device.Identifier
                }
            });
        }
        foreach (var account in bible.Accounts)
        {
            graph.Entities.Add(new CaseEntity
            {
                Id = account.Id,
                Kind = EntityKind.Account,
                DisplayName = string.IsNullOrWhiteSpace(account.Handle) ? account.Identifier : account.Handle,
                Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["provider"] = account.Provider,
                    ["identifier"] = account.Identifier
                }
            });
        }
        foreach (var vehicle in bible.Vehicles)
        {
            graph.Entities.Add(new CaseEntity
            {
                Id = vehicle.Id,
                Kind = EntityKind.Vehicle,
                DisplayName = string.Join(" ", new[] { vehicle.Color, vehicle.Make, vehicle.Model, vehicle.Plate }
                    .Where(value => !string.IsNullOrWhiteSpace(value))),
                Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["plate"] = vehicle.Plate
                }
            });
        }

        graph.Facts.AddRange(bible.Facts.Select(fact => fact.ToCanonicalFact()));
        foreach (var beat in bible.TruthTimeline)
        {
            if (!DateTimeOffset.TryParse(
                    beat.Time,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var timestamp))
            {
                continue;
            }
            DateTimeOffset? windowEnd = null;
            if (!string.IsNullOrWhiteSpace(beat.EndTime)
                && DateTimeOffset.TryParse(
                    beat.EndTime,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var parsedEnd))
            {
                windowEnd = parsedEnd;
            }
            graph.Events.Add(new CanonicalEvent
            {
                Id = beat.Id,
                Kind = beat.Kind,
                Timestamp = timestamp,
                WindowEnd = windowEnd,
                Precision = windowEnd.HasValue ? TemporalPrecision.Window : TemporalPrecision.Exact,
                ParticipantIds = beat.ParticipantIds
                    .Concat(beat.DeviceIds)
                    .Concat(beat.AccountIds)
                    .Concat(beat.VehicleIds)
                    .Distinct(StringComparer.Ordinal)
                    .ToList(),
                FactIds = beat.FactIds.Distinct(StringComparer.Ordinal).ToList(),
                Visibility = beat.Visibility
            });
        }
    }

    private static bool AddCanonicalClueObservations(
        CaseDraft draft,
        CaseGraph graph,
        CanonicalClue clue,
        EvidenceClaimNode? claim,
        IDictionary<string, List<string>> observationIdsByClue)
    {
        var bible = draft.CaseBible;
        if (bible is null || clue.ObservationIds.Count == 0)
            return false;
        var canonicalObservations = bible.Observations
            .Where(observation => clue.ObservationIds.Contains(observation.Id, StringComparer.Ordinal))
            .OrderBy(observation => observation.Id, StringComparer.Ordinal)
            .ToList();
        if (canonicalObservations.Count == 0)
            return false;

        var assetIds = claim?.AssetIds
            .Where(id => graph.Sources.Any(source => source.Id == id))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<string>();
        foreach (var canonical in canonicalObservations)
        {
            if (!graph.Facts.Any(fact => fact.Id == canonical.FactId))
                continue;
            var assetId = SelectObservationAsset(draft, bible, canonical, assetIds);
            if (assetId is null)
                continue;
            var observationId = $"observation.{IdSuffix(canonical.Id)}_{IdSuffix(assetId)}";
            if (graph.Observations.Any(observation => observation.Id == observationId))
                continue;
            graph.Observations.Add(new EvidenceObservation
            {
                Id = observationId,
                FactId = canonical.FactId,
                SourceAssetId = assetId,
                EvidentiaryOriginId = canonical.SourceId,
                Fidelity = canonical.Fidelity,
                Reliability = canonical.Reliability,
                Visibility = canonical.Visibility
            });
            AddObservationOwnership(graph, assetId, observationId, canonical.FactId);
            if (!observationIdsByClue.TryGetValue(clue.Id, out var clueObservations))
                observationIdsByClue[clue.Id] = clueObservations = new List<string>();
            clueObservations.Add(observationId);
        }
        return true;
    }

    private static string? SelectObservationAsset(
        CaseDraft draft,
        CaseBible bible,
        CaseBibleObservation observation,
        IReadOnlyList<string> assetIds)
    {
        if (assetIds.Count == 0)
            return null;
        var source = bible.Sources.FirstOrDefault(candidate => candidate.Id == observation.SourceId);
        var candidates = assetIds
            .Select(id => draft.AssetStubs.FirstOrDefault(asset => asset.Id == id))
            .Where(asset => asset is not null)
            .Select(asset => asset!)
            .ToList();
        var sourceTokens = new[] { source?.Id, source?.Name }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => value!.Split(
                ['.', '_', '-', ' '],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(token => token.Length >= 4)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var lexicalMatch = candidates
            .Select(asset => new
            {
                Asset = asset,
                Score = sourceTokens.Count(token =>
                    asset.Id.Contains(token, StringComparison.OrdinalIgnoreCase)
                    || asset.ArchetypeId.Contains(token, StringComparison.OrdinalIgnoreCase)
                    || asset.Title.Contains(token, StringComparison.OrdinalIgnoreCase)
                    || asset.LayoutHint.Contains(token, StringComparison.OrdinalIgnoreCase))
            })
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Asset.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (lexicalMatch?.Score > 0)
            return lexicalMatch.Asset.Id;
        var compatible = source?.Kind switch
        {
            CaseBibleSourceKind.DigitalRecord => candidates.Where(asset => asset.Type == "digital"),
            CaseBibleSourceKind.Photograph => candidates.Where(asset => asset.Type == "photo"),
            CaseBibleSourceKind.Testimony => candidates.Where(asset =>
                asset.ArchetypeId.Contains("interview", StringComparison.OrdinalIgnoreCase)
                || asset.LayoutHint.Contains("Statement", StringComparison.OrdinalIgnoreCase)
                || asset.LayoutHint.Contains("Transcript", StringComparison.OrdinalIgnoreCase)),
            CaseBibleSourceKind.PhysicalObject => candidates.Where(asset =>
                asset.Type == "photo" || asset.ImagePurpose == "object"),
            _ => candidates.Where(asset => asset.Type is "pdf" or "document" or "digital")
        };
        var pool = compatible.Select(asset => asset.Id).ToArray();
        if (pool.Length == 0)
            pool = assetIds.ToArray();
        var index = observation.Id.Aggregate(17, (value, character) => unchecked(value * 31 + character)) & int.MaxValue;
        return pool[index % pool.Length];
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
            .Where(clue => !clue.SourceType.Equals("forensic", StringComparison.OrdinalIgnoreCase))
            .SelectMany(clue => (observationIdsByClue.GetValueOrDefault(clue.Id) ?? [])
                .Select(observationId => (Clue: clue, ObservationId: observationId)))
            .DistinctBy(item => item.ObservationId, StringComparer.Ordinal)
            .OrderBy(item => item.ObservationId, StringComparer.Ordinal)
            .ToArray();
        var topology = DifficultyProfileCatalog.Get(draft).Topology;
        var forensicObservationIds = culpritClues
            .Where(clue => clue.SourceType.Equals("forensic", StringComparison.OrdinalIgnoreCase))
            .SelectMany(clue => observationIdsByClue.GetValueOrDefault(clue.Id) ?? [])
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Take(Math.Max(1, topology.MinForensicHops))
            .ToArray();
        if (forensicObservationIds.Length == 0)
            return;

        foreach (var bridge in bridgePremises)
        {
            var forensicSuffix = string.Join("_", forensicObservationIds.Select(IdSuffix));
            var branchSuffix = $"{IdSuffix(bridge.Clue.Id)}_{IdSuffix(bridge.ObservationId)}_{forensicSuffix}";
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
                PremiseIds = new[] { bridgeDerivationId }.Concat(forensicObservationIds).ToList(),
                Rule = DerivationRule.CrossSourceCorroboration,
                ConclusionFactId = joinedFactId
            });
            var finalPremiseId = joinDerivationId;
            for (var depth = 3; depth < topology.MinDerivationDepth; depth++)
            {
                var depthFactId = $"fact.proof_depth_{depth}_{branchSuffix}";
                var depthDerivationId = $"derivation.proof_depth_{depth}_{branchSuffix}";
                graph.Facts.Add(new CanonicalFact
                {
                    Id = depthFactId,
                    Predicate = $"proofDepth{depth}_{branchSuffix}",
                    SubjectId = ResolvePersonId(graph, draft.CulpritId) ?? draft.CulpritId,
                    LiteralValue = "true",
                    LiteralType = LiteralValueType.Boolean
                });
                graph.Derivations.Add(new ProofDerivation
                {
                    Id = depthDerivationId,
                    PremiseIds = { finalPremiseId },
                    Rule = DerivationRule.TimelineCorrelation,
                    ConclusionFactId = depthFactId
                });
                finalPremiseId = depthDerivationId;
            }
            graph.Derivations.Add(new ProofDerivation
            {
                Id = $"derivation.culprit_{branchSuffix}",
                PremiseIds = { finalPremiseId },
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
        var personId = ResolvePersonId(graph, suspectId) ?? suspectId;
        var canonical = graph.Facts.FirstOrDefault(fact =>
            fact.SubjectId == personId
            && fact.Predicate.Equals("isCulprit", StringComparison.OrdinalIgnoreCase)
            && fact.LiteralValue?.Equals("true", StringComparison.OrdinalIgnoreCase) == true);
        if (canonical is not null)
        {
            factsBySuspect[suspectId] = canonical.Id;
            return canonical.Id;
        }
        var factId = $"fact.culprit_{IdSuffix(suspectId)}";
        graph.Facts.Add(new CanonicalFact
        {
            Id = factId,
            Predicate = "isCulprit",
            SubjectId = personId,
            LiteralValue = "true",
            LiteralType = LiteralValueType.Boolean,
            Visibility = FactVisibility.Public
        });
        factsBySuspect[suspectId] = factId;
        return factId;
    }

    private static void AddDecoyArcs(CaseDraft draft, CaseGraph graph)
    {
        var topology = DifficultyProfileCatalog.Get(draft).Topology;
        foreach (var redHerring in draft.Blueprint.RedHerrings.OrderBy(item => item.SuspectId, StringComparer.Ordinal))
        {
            if (AddCanonicalDecoyArc(draft, graph, redHerring, topology))
                continue;
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
                LiteralType = LiteralValueType.String,
                TruthStatus = topology.RequiresConflictingObservation
                    ? FactTruthStatus.Disputed
                    : FactTruthStatus.Confirmed
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
                    Fidelity = ObservationFidelity.Circumstantial,
                    Reliability = topology.RequiresConflictingObservation
                        ? ObservationReliability.Disputed
                        : ObservationReliability.Verified
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
                    SourceAssetId = assetId,
                    Reliability = topology.MinReliabilityLevels >= 3
                        ? ObservationReliability.Corroborated
                        : ObservationReliability.Verified
                });
                AddObservationOwnership(graph, assetId, observationId, verificationFactId);
                verificationObservationIds.Add(observationId);
            }

            var conclusionFactId = $"fact.excluded_{IdSuffix(redHerring.SuspectId)}";
            var derivationId = $"derivation.exclude_{IdSuffix(redHerring.SuspectId)}";
            var resolutionPremiseIds = verificationObservationIds.ToList();
            for (var depth = 1; depth < topology.RedHerringResolutionDepth; depth++)
            {
                var depthFactId = $"fact.decoy_resolution_{depth}_{suspectSuffix}";
                var depthDerivationId = $"derivation.decoy_resolution_{depth}_{suspectSuffix}";
                graph.Facts.Add(new CanonicalFact
                {
                    Id = depthFactId,
                    Predicate = $"decoyResolutionStep{depth}",
                    SubjectId = personId,
                    LiteralValue = "true",
                    LiteralType = LiteralValueType.Boolean
                });
                graph.Derivations.Add(new ProofDerivation
                {
                    Id = depthDerivationId,
                    PremiseIds = resolutionPremiseIds,
                    Rule = DerivationRule.CrossSourceCorroboration,
                    ConclusionFactId = depthFactId,
                    SupportsSuspectId = redHerring.SuspectId
                });
                resolutionPremiseIds = [depthDerivationId];
            }
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
                PremiseIds = resolutionPremiseIds
                    .Concat(suspicionObservationIds)
                    .Distinct(StringComparer.Ordinal)
                    .ToList(),
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

    private static bool AddCanonicalDecoyArc(
        CaseDraft draft,
        CaseGraph graph,
        CanonicalRedHerring redHerring,
        DifficultyTopology topology)
    {
        var bible = draft.CaseBible;
        if (bible is null)
            return false;
        var canonicalDecoy = bible.DifficultyIntent.DecoyArcs.FirstOrDefault(decoy =>
            (!string.IsNullOrWhiteSpace(redHerring.Id) && decoy.Id == redHerring.Id)
            || bible.People.Any(person =>
                person.Id == decoy.SuspectPersonId
                && person.SuspectId == redHerring.SuspectId));
        var personId = ResolvePersonId(graph, redHerring.SuspectId);
        if (canonicalDecoy is null || personId is null)
            return false;

        var suspicionAssets = draft.AssetStubs
            .Where(asset => asset.IntroducesRedHerringSuspectIds.Contains(redHerring.SuspectId, StringComparer.Ordinal))
            .Select(asset => asset.Id)
            .Where(id => graph.Sources.Any(source => source.Id == id))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var verificationAssets = draft.AssetStubs
            .Where(asset => asset.ResolvesRedHerringSuspectIds.Contains(redHerring.SuspectId, StringComparer.Ordinal))
            .Select(asset => asset.Id)
            .Where(id => graph.Sources.Any(source => source.Id == id))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var suspicionObservationIds = AttachCanonicalObservations(
            draft,
            bible,
            graph,
            canonicalDecoy.SuspicionObservationIds,
            suspicionAssets,
            "decoy_suspicion");
        var verificationObservationIds = AttachCanonicalObservations(
            draft,
            bible,
            graph,
            canonicalDecoy.VerificationObservationIds,
            verificationAssets,
            "decoy_verification");

        var suspectSuffix = IdSuffix(redHerring.SuspectId);
        var resolutionPremiseIds = verificationObservationIds.ToList();
        for (var depth = 1; depth < topology.RedHerringResolutionDepth; depth++)
        {
            var depthFactId = $"fact.decoy_resolution_{depth}_{suspectSuffix}";
            var depthDerivationId = $"derivation.decoy_resolution_{depth}_{suspectSuffix}";
            graph.Facts.Add(new CanonicalFact
            {
                Id = depthFactId,
                Predicate = $"decoyResolutionStep{depth}",
                SubjectId = personId,
                LiteralValue = "true",
                LiteralType = LiteralValueType.Boolean
            });
            graph.Derivations.Add(new ProofDerivation
            {
                Id = depthDerivationId,
                PremiseIds = resolutionPremiseIds,
                Rule = DerivationRule.CrossSourceCorroboration,
                ConclusionFactId = depthFactId,
                SupportsSuspectId = redHerring.SuspectId
            });
            resolutionPremiseIds = [depthDerivationId];
        }

        var conclusionFactId = $"fact.excluded_{suspectSuffix}";
        var derivationId = $"derivation.exclude_{suspectSuffix}";
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
            PremiseIds = resolutionPremiseIds
                .Concat(suspicionObservationIds)
                .Distinct(StringComparer.Ordinal)
                .ToList(),
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
        return true;
    }

    private static List<string> AttachCanonicalObservations(
        CaseDraft draft,
        CaseBible bible,
        CaseGraph graph,
        IEnumerable<string> canonicalObservationIds,
        IEnumerable<string> assetIds,
        string purpose)
    {
        var result = new List<string>();
        var availableAssetIds = assetIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        foreach (var canonicalId in canonicalObservationIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var canonical = bible.Observations.FirstOrDefault(observation => observation.Id == canonicalId);
            if (canonical is null || !graph.Facts.Any(fact => fact.Id == canonical.FactId))
                continue;
            var existing = graph.Observations.FirstOrDefault(observation =>
                observation.FactId == canonical.FactId
                && observation.EvidentiaryOriginId == canonical.SourceId);
            if (existing is not null)
            {
                result.Add(existing.Id);
                continue;
            }
            var assetId = SelectObservationAsset(draft, bible, canonical, availableAssetIds);
            if (assetId is null)
                continue;
            var observationId = $"observation.{purpose}_{IdSuffix(canonical.Id)}_{IdSuffix(assetId)}";
            if (!graph.Observations.Any(observation => observation.Id == observationId))
            {
                graph.Observations.Add(new EvidenceObservation
                {
                    Id = observationId,
                    FactId = canonical.FactId,
                    SourceAssetId = assetId,
                    EvidentiaryOriginId = canonical.SourceId,
                    Fidelity = canonical.Fidelity,
                    Reliability = canonical.Reliability,
                    Visibility = canonical.Visibility
                });
                AddObservationOwnership(graph, assetId, observationId, canonical.FactId);
            }
            result.Add(observationId);
        }
        return result.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
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
                EvidenceRole = stub.EvidenceRole ?? EvidenceRoles.Corroborative,
                SubjectSuspectId = stub.SubjectSuspectId,
                ImagePurpose = stub.ImagePurpose,
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
        var actionIds = new HashSet<string>(StringComparer.Ordinal);
        var transformIds = new HashSet<string>(StringComparer.Ordinal);
        var generatedFactIds = graph.Facts.Select(fact => fact.Id).ToHashSet(StringComparer.Ordinal);
        var generatedObservationIds = graph.Observations
            .Select(observation => observation.Id)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var outcome in draft.ForensicFull
                     .Where(outcome => !string.IsNullOrWhiteSpace(outcome.ResultAssetId)
                                       || !string.IsNullOrWhiteSpace(outcome.ResultEmailId))
                     .OrderBy(outcome => outcome.InputAssetId, StringComparer.Ordinal)
                     .ThenBy(outcome => outcome.AnalysisType, StringComparer.Ordinal))
        {
            var idSuffix = $"{IdSuffix(outcome.InputAssetId)}_{IdSuffix(outcome.AnalysisType)}";
            var actionId = UniqueId($"action.analysis_{idSuffix}", actionIds);
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
            if (producedObservationIds.Count == 0)
            {
                var resultSourceId = outcome.ResultAssetId ?? outcome.ResultEmailId;
                if (!string.IsNullOrWhiteSpace(resultSourceId)
                    && graph.Sources.Any(source => source.Id == resultSourceId))
                {
                    var resultSuffix = IdSuffix(resultSourceId);
                    var factId = UniqueId(
                        $"fact.optional_forensic_{idSuffix}_{resultSuffix}",
                        generatedFactIds);
                    var observationId = UniqueId(
                        $"observation.optional_forensic_{idSuffix}_{resultSuffix}",
                        generatedObservationIds);
                    graph.Facts.Add(new CanonicalFact
                    {
                        Id = factId,
                        Predicate = "optionalForensicFinding",
                        SubjectId = stub.InputObjectId,
                        LiteralValue = outcome.ConclusionText ?? stub.Role,
                        LiteralType = LiteralValueType.String,
                        Visibility = FactVisibility.Public
                    });
                    graph.Observations.Add(new EvidenceObservation
                    {
                        Id = observationId,
                        FactId = factId,
                        SourceAssetId = resultSourceId,
                        Reliability = ObservationReliability.Verified,
                        ForensicProperty = stub.ProducedProperties.FirstOrDefault()
                    });
                    AddObservationOwnership(graph, resultSourceId, observationId, factId);
                    producedObservationIds.Add(observationId);
                }
            }
            for (var index = 0; index < producedObservationIds.Count; index++)
            {
                var observation = graph.Observations.First(item => item.Id == producedObservationIds[index]);
                if (stub.ProducedProperties.Count > 0)
                    observation.ForensicProperty = stub.ProducedProperties[Math.Min(index, stub.ProducedProperties.Count - 1)];
            }
            var transformId = UniqueId($"forensic.{idSuffix}", transformIds);
            graph.ForensicTransforms.Add(new ForensicTransform
            {
                Id = transformId,
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

    private static string UniqueId(string baseId, ISet<string> usedIds)
    {
        var id = baseId;
        for (var suffix = 2; !usedIds.Add(id); suffix++)
            id = $"{baseId}_{suffix}";
        return id;
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
