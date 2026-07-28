using System.Text.Json;
using System.Text.Json.Serialization;

namespace CaseGen.Functions.Services.CaseV2;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FactVisibility
{
    Public,
    Private
}

[JsonConverter(typeof(CaseV2EnumConverter<FactTruthStatus>))]
public enum FactTruthStatus
{
    Confirmed,
    Disputed,
    False
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LiteralValueType
{
    String,
    Boolean,
    Integer,
    Decimal,
    DateTime,
    Duration
}

public sealed class CanonicalFact
{
    public string Id { get; set; } = string.Empty;
    public string Predicate { get; set; } = string.Empty;
    public string SubjectId { get; set; } = string.Empty;
    public string? ObjectId { get; set; }
    public string? LiteralValue { get; set; }
    public LiteralValueType? LiteralType { get; set; }
    public string? LiteralUnit { get; set; }
    public string? EventId { get; set; }
    public FactVisibility Visibility { get; set; } = FactVisibility.Public;
    public FactTruthStatus TruthStatus { get; set; } = FactTruthStatus.Confirmed;
    public string? IntentionalConflictId { get; set; }

    [JsonIgnore]
    public string CanonicalValue => ObjectId ?? LiteralValue ?? string.Empty;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EventKind
{
    Occurred,
    Created,
    Modified,
    Scanned,
    Uploaded,
    Submitted,
    Approved,
    Paid,
    Accessed,
    Entered,
    Exited,
    Called,
    Messaged,
    Discovered,
    Reported,
    Collected,
    AnalysisRequested,
    AnalysisCompleted
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TemporalPrecision
{
    Exact,
    Estimated,
    Window
}

public sealed class CanonicalEvent
{
    public string Id { get; set; } = string.Empty;
    public EventKind Kind { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public DateTimeOffset? WindowEnd { get; set; }
    public TemporalPrecision Precision { get; set; } = TemporalPrecision.Exact;
    public List<string> ParticipantIds { get; set; } = new();
    public List<string> FactIds { get; set; } = new();
    public FactVisibility Visibility { get; set; } = FactVisibility.Public;
}

[JsonConverter(typeof(CaseV2EnumConverter<ObservationFidelity>))]
public enum ObservationFidelity
{
    DirectRecord,
    Testimony,
    Hearsay,
    Circumstantial
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ObservationReliability
{
    Verified,
    Corroborated,
    Unverified,
    Disputed
}

public sealed class CaseV2EnumConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"{typeof(TEnum).Name} must be a string.");

        var raw = reader.GetString() ?? string.Empty;
        if (Enum.TryParse<TEnum>(raw, ignoreCase: true, out var exact))
            return exact;

        var normalized = Normalize(raw);
        foreach (var name in Enum.GetNames<TEnum>())
        {
            if (Normalize(name) == normalized)
                return Enum.Parse<TEnum>(name);
        }

        if (TryAlias(normalized, out var alias))
            return alias;

        throw new JsonException($"Unsupported {typeof(TEnum).Name} value '{raw}'.");
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());

    private static bool TryAlias(string value, out TEnum result)
    {
        string? canonical = typeof(TEnum) == typeof(FactTruthStatus)
            ? value switch
            {
                "true" or "verified" or "established" => nameof(FactTruthStatus.Confirmed),
                "contested" or "uncertain" => nameof(FactTruthStatus.Disputed),
                "disproven" or "incorrect" => nameof(FactTruthStatus.False),
                _ => null
            }
            : typeof(TEnum) == typeof(ObservationFidelity)
                ? value switch
                {
                    "direct" or "record" or "documentary" or "directevidence" => nameof(ObservationFidelity.DirectRecord),
                    "witness" or "witnessstatement" or "testimonial" => nameof(ObservationFidelity.Testimony),
                    "secondhand" => nameof(ObservationFidelity.Hearsay),
                    "indirect" => nameof(ObservationFidelity.Circumstantial),
                    _ => null
                }
                : null;

        if (canonical is not null && Enum.TryParse(canonical, out result))
            return true;
        result = default;
        return false;
    }

    private static string Normalize(string value) =>
        new(value.Where(char.IsAsciiLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}

public sealed class EvidenceObservation
{
    public string Id { get; set; } = string.Empty;
    public string FactId { get; set; } = string.Empty;
    public string SourceAssetId { get; set; } = string.Empty;
    public string? EvidentiaryOriginId { get; set; }
    public ObservationFidelity Fidelity { get; set; } = ObservationFidelity.DirectRecord;
    public ObservationReliability Reliability { get; set; } = ObservationReliability.Verified;
    public FactVisibility Visibility { get; set; } = FactVisibility.Public;
    public ForensicObservationProperty? ForensicProperty { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DerivationRule
{
    IdentityMatch,
    AccountOwnership,
    DeviceAssignment,
    SessionOrigin,
    AccessOpportunity,
    ActionAttribution,
    BenefitAttribution,
    TimelineCorrelation,
    AlibiSupport,
    AlibiContradiction,
    Exclusion,
    CrossSourceCorroboration
}

public sealed class ProofDerivation
{
    public string Id { get; set; } = string.Empty;
    public List<string> PremiseIds { get; set; } = new();
    public DerivationRule Rule { get; set; }
    public string ConclusionFactId { get; set; } = string.Empty;
    public string? SupportsSuspectId { get; set; }
    public bool IsCulpritConclusion { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReachabilityState
{
    Initial,
    AfterAction,
    AfterForensic
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EvidenceSourceKind
{
    Asset,
    Email
}

public sealed class EvidenceSourceNode
{
    public string Id { get; set; } = string.Empty;
    public EvidenceSourceKind Kind { get; set; } = EvidenceSourceKind.Asset;
    public List<string> ObservationIds { get; set; } = new();
    public List<string> ClaimedFactIds { get; set; } = new();
    public string? EvidentiaryOriginId { get; set; }
    public ReachabilityState AvailableAt { get; set; } = ReachabilityState.Initial;
    public List<string> RevealPrerequisiteIds { get; set; } = new();
    public bool Required { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum VisibilityGate
{
    Initial,
    AfterAction,
    AfterForensic
}

public sealed class EvidenceAssetSpec
{
    public string Id { get; set; } = string.Empty;
    public string ArchetypeId { get; set; } = string.Empty;
    public string AssetType { get; set; } = string.Empty;
    public string EvidenceRole { get; set; } = EvidenceRoles.Corroborative;
    public string? SubjectSuspectId { get; set; }
    public string? ImagePurpose { get; set; }
    public List<string> ObservationIds { get; set; } = new();
    public List<string> ContainedObjectIds { get; set; } = new();
    public Dictionary<string, ForensicInputObjectType> ContainedObjectTypes { get; set; } =
        new(StringComparer.Ordinal);
    public VisibilityGate Visibility { get; set; } = VisibilityGate.Initial;
    public string LayoutId { get; set; } = string.Empty;
}

public sealed class DecoyArc
{
    public string SuspectId { get; set; } = string.Empty;
    public List<string> SuspicionObservationIds { get; set; } = new();
    public List<string> VerificationObservationIds { get; set; } = new();
    public string ResolutionDerivationId { get; set; } = string.Empty;
    public bool MustBePlayerReachable { get; set; } = true;
}

public sealed class ForensicTransform
{
    public string Id { get; set; } = string.Empty;
    public string MethodId { get; set; } = string.Empty;
    public string InputAssetId { get; set; } = string.Empty;
    public string InputObjectId { get; set; } = string.Empty;
    public string? ReferenceSampleObjectId { get; set; }
    public string? ChainOfCustodyObservationId { get; set; }
    public List<string> ProducedObservationIds { get; set; } = new();
    public List<ForensicObservationProperty> ProducedProperties { get; set; } = new();
    public List<string> LimitationKeys { get; set; } = new();
    public string? ResultAssetId { get; set; }
    public string? ResultEmailId { get; set; }
    public string ResultLayoutId { get; set; } = "ForensicReport";
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InvestigationActionKind
{
    InspectAsset,
    CompareRecords,
    RequestAnalysis,
    ReviewResult
}

public sealed class InvestigationAction
{
    public string Id { get; set; } = string.Empty;
    public InvestigationActionKind Kind { get; set; }
    public ReachabilityState CompletesAt { get; set; } = ReachabilityState.AfterAction;
    public List<string> PrerequisiteSourceIds { get; set; } = new();
    public List<string> PrerequisiteActionIds { get; set; } = new();
    public List<string> RevealsSourceIds { get; set; } = new();
    public string? AnalysisId { get; set; }
    public bool Required { get; set; }
}

public sealed class EventOrderConstraint
{
    public string Id { get; set; } = string.Empty;
    public string BeforeEventId { get; set; } = string.Empty;
    public string AfterEventId { get; set; } = string.Empty;
    public TimeSpan MinimumGap { get; set; }
}

public sealed class TravelWindowConstraint
{
    public string Id { get; set; } = string.Empty;
    public string FromEventId { get; set; } = string.Empty;
    public string ToEventId { get; set; } = string.Empty;
    public TimeSpan MinimumTravelTime { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CaseGraphOrigin
{
    HandAuthored,
    TransitionalClueLadder,
    Generated
}

public sealed class CaseGraph
{
    public CaseGraphOrigin Origin { get; set; } = CaseGraphOrigin.HandAuthored;
    public List<CaseEntity> Entities { get; set; } = new();
    public List<SuspectEntityReference> SuspectReferences { get; set; } = new();
    public List<CanonicalFact> Facts { get; set; } = new();
    public List<CanonicalEvent> Events { get; set; } = new();
    public List<EvidenceSourceNode> Sources { get; set; } = new();
    public List<EvidenceAssetSpec> AssetSpecs { get; set; } = new();
    public List<DecoyArc> DecoyArcs { get; set; } = new();
    public List<ForensicTransform> ForensicTransforms { get; set; } = new();
    public List<EvidenceObservation> Observations { get; set; } = new();
    public List<ProofDerivation> Derivations { get; set; } = new();
    public List<EventOrderConstraint> EventOrderConstraints { get; set; } = new();
    public List<TravelWindowConstraint> TravelWindows { get; set; } = new();
    public List<InvestigationAction> Actions { get; set; } = new();
    public List<string> RequiredSourceIds { get; set; } = new();
    public List<string> RequiredAnalysisIds { get; set; } = new();

    [JsonIgnore]
    public bool IsEmpty =>
        Entities.Count == 0
        && Facts.Count == 0
        && Events.Count == 0
        && Sources.Count == 0
        && AssetSpecs.Count == 0
        && DecoyArcs.Count == 0
        && ForensicTransforms.Count == 0
        && Observations.Count == 0
        && Derivations.Count == 0;

    public CaseGraph DeepClone()
    {
        var json = JsonSerializer.Serialize(this);
        return JsonSerializer.Deserialize<CaseGraph>(json)
            ?? throw new InvalidOperationException("Could not clone the case graph.");
    }
}

public static class CaseGraphTimelineCompiler
{
    public static IReadOnlyList<CanonicalEvent> CompilePlayerTimeline(CaseGraph graph) =>
        graph.Events
            .Where(graphEvent => graphEvent.Visibility == FactVisibility.Public)
            .OrderBy(graphEvent => graphEvent.Timestamp)
            .ThenBy(graphEvent => graphEvent.Id, StringComparer.Ordinal)
            .Select(graphEvent => new CanonicalEvent
            {
                Id = graphEvent.Id,
                Kind = graphEvent.Kind,
                Timestamp = graphEvent.Timestamp,
                WindowEnd = graphEvent.WindowEnd,
                Precision = graphEvent.Precision,
                ParticipantIds = graphEvent.ParticipantIds.ToList(),
                FactIds = graphEvent.FactIds.ToList(),
                Visibility = graphEvent.Visibility
            })
            .ToList();
}
