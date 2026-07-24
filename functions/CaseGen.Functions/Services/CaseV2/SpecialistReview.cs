using System.Text.Json;
using System.Text.Json.Serialization;
using CaseGen.Functions.Services.CaseV2.Tasks;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.CaseV2;

public enum SpecialistReviewKind
{
    DocumentFidelity,
    ScientificRealism,
    NarrativeHumanBehavior,
    LocaleProceduralRealism,
    PlayerExperience
}

public enum ReviewSeverity
{
    Low,
    Medium,
    High
}

public sealed record SpecialistFinding(
    SpecialistReviewKind Reviewer,
    string ConstraintId,
    string OwningNodeId,
    ReviewSeverity Severity,
    string Message,
    string? SuggestedRepair,
    bool Deterministic);

public sealed class SpecialistReport
{
    public SpecialistReviewKind Reviewer { get; init; }
    public List<SpecialistFinding> Findings { get; } = new();
    public string Verdict => Findings.Any(finding => finding.Severity == ReviewSeverity.High)
        ? "reject"
        : Findings.Any(finding => finding.Severity == ReviewSeverity.Medium)
            ? "needs_review"
            : "ok";
}

public static class SpecialistSeverityPolicy
{
    public static ReviewSeverity For(SpecialistReviewKind reviewer, string constraintId) =>
        (reviewer, constraintId.ToLowerInvariant()) switch
        {
            (_, var id) when id.Contains("private") || id.Contains("unreachable") || id.Contains("unsolvable") =>
                ReviewSeverity.High,
            (SpecialistReviewKind.DocumentFidelity, var id) when id.Contains("missing") || id.Contains("contradiction") =>
                ReviewSeverity.High,
            (SpecialistReviewKind.ScientificRealism, var id) when id.Contains("fabricat") || id.Contains("overclaim") || id.Contains("incompatible") =>
                ReviewSeverity.High,
            (SpecialistReviewKind.PlayerExperience, var id) when id.Contains("blocked") || id.Contains("answerable") =>
                ReviewSeverity.High,
            (SpecialistReviewKind.NarrativeHumanBehavior, _) => ReviewSeverity.Medium,
            (SpecialistReviewKind.LocaleProceduralRealism, _) => ReviewSeverity.Medium,
            _ => ReviewSeverity.Medium
        };
}

public static class ObjectiveQualityValidator
{
    public static IReadOnlyList<SpecialistFinding> Validate(CaseDraft draft)
    {
        var findings = new List<SpecialistFinding>();
        Add(
            SpecialistReviewKind.DocumentFidelity,
            PipelineStageValidator.ValidateCaseBible(
                draft,
                required: draft.CaseGraph.Origin == CaseGraphOrigin.Generated),
            "caseBible");
        Add(SpecialistReviewKind.LocaleProceduralRealism, LocaleProfileCatalog.Validate(draft), "locale");
        AddGraph(CaseGraphValidator.Validate(draft.CaseGraph));
        Add(SpecialistReviewKind.DocumentFidelity, EvidenceContractValidator.Validate(draft), "evidence");
        Add(SpecialistReviewKind.ScientificRealism, ForensicContractValidator.Validate(draft), "forensics");
        Add(SpecialistReviewKind.PlayerExperience, DifficultyTopologyValidator.Validate(draft), "difficulty");

        var reachability = ReachabilityGate.Validate(draft.CaseGraph, draft.CulpritId);
        foreach (var failure in reachability.Failures)
            AddFinding(SpecialistReviewKind.PlayerExperience, failure.Code, failure.NodeId, failure.Message);

        var independence = IndependentProofPathGate.Validate(
            draft.CaseGraph,
            draft.CulpritId,
            DifficultyProfileCatalog.Get(draft).Name);
        foreach (var failure in independence.Failures)
            AddFinding(SpecialistReviewKind.PlayerExperience, failure.Code, failure.NodeId, failure.Message);

        var sources = draft.CaseGraph.Sources.Select(source => source.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var evidenceId in draft.RequiredEvidenceIds.Where(id => !sources.Contains(id)))
            AddFinding(SpecialistReviewKind.PlayerExperience, "required_evidence_missing", evidenceId, $"required evidence '{evidenceId}' does not exist");

        var reachableObservations = ReachabilityClosureEngine.Calculate(draft.CaseGraph)
            .FinalObservationIds.ToHashSet(StringComparer.Ordinal);
        foreach (var topic in draft.QuestionTopics)
        {
            if (topic.SupportingEvidenceIds.Count == 0)
                AddFinding(SpecialistReviewKind.PlayerExperience, "question_provenance_missing", topic.Id, $"question topic '{topic.Id}' has no evidence provenance");
            foreach (var evidenceId in topic.SupportingEvidenceIds.Where(id =>
                         !sources.Contains(id)
                         && !reachableObservations.Contains(id)))
            {
                AddFinding(SpecialistReviewKind.PlayerExperience, "question_provenance_unreachable", topic.Id, $"question topic '{topic.Id}' references unreachable evidence '{evidenceId}'");
            }
        }

        return findings
            .OrderBy(finding => finding.Reviewer)
            .ThenBy(finding => finding.OwningNodeId, StringComparer.Ordinal)
            .ThenBy(finding => finding.ConstraintId, StringComparer.Ordinal)
            .ToArray();

        void Add(SpecialistReviewKind reviewer, StageValidationReport report, string prefix)
        {
            foreach (var error in report.Errors)
            {
                var nodeId = ExtractNodeId(error) ?? $"{prefix}.root";
                AddFinding(reviewer, $"{prefix}.{Slug(error)}", nodeId, error);
            }
        }

        void AddGraph(GraphValidationReport report)
        {
            foreach (var error in report.Errors)
            {
                var reviewer = error.OwningStage switch
                {
                    "timeline" => SpecialistReviewKind.LocaleProceduralRealism,
                    "proof" or "rules" => SpecialistReviewKind.PlayerExperience,
                    _ => SpecialistReviewKind.DocumentFidelity
                };
                AddFinding(reviewer, error.Code, error.NodeId, error.Message);
            }
        }

        void AddFinding(SpecialistReviewKind reviewer, string constraint, string node, string message)
        {
            findings.Add(new SpecialistFinding(
                reviewer,
                constraint,
                node,
                SpecialistSeverityPolicy.For(reviewer, constraint),
                message,
                null,
                true));
        }
    }

    private static string? ExtractNodeId(string message)
    {
        var start = message.IndexOf('\'');
        var end = start < 0 ? -1 : message.IndexOf('\'', start + 1);
        return start >= 0 && end > start ? message[(start + 1)..end] : null;
    }

    private static string Slug(string message)
    {
        var chars = message.ToLowerInvariant().Select(character =>
            char.IsLetterOrDigit(character) ? character : '_').ToArray();
        return new string(chars).Trim('_')[..Math.Min(64, new string(chars).Trim('_').Length)];
    }
}

public sealed class SpecialistReviewCoordinator
{
    private readonly ILLMProvider _llm;
    private readonly ILogger _logger;

    public SpecialistReviewCoordinator(ILLMProvider llm, ILogger logger)
    {
        _llm = llm;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SpecialistReport>> RunAsync(CaseDraft draft, CancellationToken ct)
    {
        var deterministic = ObjectiveQualityValidator.Validate(draft);
        var tasks = Enum.GetValues<SpecialistReviewKind>()
            .Select(kind => RunSpecialistAsync(kind, draft, deterministic.Where(finding => finding.Reviewer == kind), ct));
        return await Task.WhenAll(tasks);
    }

    private async Task<SpecialistReport> RunSpecialistAsync(
        SpecialistReviewKind kind,
        CaseDraft draft,
        IEnumerable<SpecialistFinding> deterministicFindings,
        CancellationToken ct)
    {
        var report = new SpecialistReport { Reviewer = kind };
        report.Findings.AddRange(deterministicFindings);
        try
        {
            var input = BuildNarrowInput(kind, draft);
            var prompts = AgentPromptCatalog.Default;
            var system = prompts.RenderSystem("SpecialistReview", new Dictionary<string, object?>
            {
                ["specialist_kind"] = kind
            });
            var user = prompts.RenderUser("SpecialistReview", new Dictionary<string, object?>
            {
                ["input_json"] = JsonSerializer.Serialize(input)
            });
            var raw = await _llm.GenerateStructuredResponseAsync(system, user, Schema, ct);
            var parsed = JsonSerializer.Deserialize<DraftReport>(
                raw.Content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new DraftReport();
            foreach (var finding in parsed.Findings)
            {
                var nodeId = string.IsNullOrWhiteSpace(finding.OwningNodeId)
                    ? $"review.{kind}"
                    : finding.OwningNodeId;
                var constraint = string.IsNullOrWhiteSpace(finding.ConstraintId)
                    ? $"{kind}.semantic"
                    : finding.ConstraintId;
                report.Findings.Add(new SpecialistFinding(
                    kind,
                    constraint,
                    nodeId,
                    ReviewSeverity.Medium,
                    finding.Message,
                    finding.SuggestedRepair,
                    false));
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Specialist reviewer {Reviewer} failed", kind);
            report.Findings.Add(new SpecialistFinding(
                kind,
                $"{kind}.reviewer_unavailable",
                $"review.{kind}",
                ReviewSeverity.Medium,
                $"Specialist reviewer {kind} was unavailable.",
                "retry the specialist review",
                false));
        }
        return report;
    }

    private static object BuildNarrowInput(SpecialistReviewKind kind, CaseDraft draft) => new
    {
        caseBible = draft.CaseBible,
        reviewInput = BuildReviewInput(kind, draft)
    };

    private static object BuildReviewInput(SpecialistReviewKind kind, CaseDraft draft) =>
        kind switch
        {
            SpecialistReviewKind.DocumentFidelity => new
            {
                assets = draft.AssetFull.Concat(draft.ResultAssets).Select(asset => new
                {
                    asset.Id, asset.Title, asset.Description,
                    content = EvidenceContentText.Extract(asset),
                    observations = draft.CaseGraph.Observations
                        .Where(observation => observation.SourceAssetId == asset.Id)
                        .Select(observation => observation.Id)
                })
            },
            SpecialistReviewKind.ScientificRealism => new
            {
                transforms = draft.CaseGraph.ForensicTransforms,
                methods = draft.CaseGraph.ForensicTransforms.Select(transform => ForensicMethodCatalog.Find(transform.MethodId)),
                reports = draft.ResultAssets.Select(asset => new { asset.Id, content = EvidenceContentText.Extract(asset) })
            },
            SpecialistReviewKind.NarrativeHumanBehavior => new
            {
                suspects = draft.SuspectFull.Select(suspect => new
                {
                    suspect.Id, suspect.Name, suspect.Occupation, suspect.Relationship,
                    suspect.Motive, suspect.Alibi, suspect.Background
                }),
                decoyArcs = draft.CaseGraph.DecoyArcs,
                draft.Blueprint.CrimeMechanism,
                draft.Blueprint.CulpritObjective
            },
            SpecialistReviewKind.LocaleProceduralRealism => new
            {
                language = ForensicMethodCatalog.NormalizeLanguage(draft.Request.Language),
                profile = LocaleProfileCatalog.Get(draft.Request.Language),
                draft.Metadata.Location,
                draft.Metadata.IncidentDate,
                draft.Metadata.OpenedAt,
                draft.Blueprint.Locale,
                headings = draft.AssetFull.Concat(draft.ResultAssets)
                    .SelectMany(asset => asset.BodyDoc?.Sections.Select(section => section.Heading) ?? [])
            },
            _ => new
            {
                initialPlayerView = PlayerVisibleProjectionBuilder.Build(draft, new SequentialPlayerSimulator(draft)),
                topology = DifficultyProfileCatalog.Get(draft).Topology
            }
        };

    private sealed class DraftReport
    {
        [JsonPropertyName("findings")]
        public List<DraftFinding> Findings { get; set; } = new();
    }

    private sealed class DraftFinding
    {
        [JsonPropertyName("constraintId")] public string ConstraintId { get; set; } = string.Empty;
        [JsonPropertyName("owningNodeId")] public string OwningNodeId { get; set; } = string.Empty;
        [JsonPropertyName("message")] public string Message { get; set; } = string.Empty;
        [JsonPropertyName("suggestedRepair")] public string? SuggestedRepair { get; set; }
    }

    private const string Schema = """
    {"type":"object","required":["findings"],"properties":{"findings":{"type":"array","items":{
      "type":"object","required":["constraintId","owningNodeId","message"],
      "properties":{
        "constraintId":{"type":"string"},
        "owningNodeId":{"type":"string"},
        "message":{"type":"string"},
        "suggestedRepair":{"type":["string","null"]}
      }}}}}
    """;
}
