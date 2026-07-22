using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace CaseGen.Functions.Services.CaseV2;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EntityKind
{
    Person,
    Organization,
    Location,
    Device,
    Account,
    Credential,
    Session,
    Document,
    PhysicalObject,
    Vehicle,
    Communication,
    Transaction
}

public sealed class CaseEntity
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public EntityKind Kind { get; set; }

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("attributes")]
    public Dictionary<string, string> Attributes { get; set; } = new(StringComparer.Ordinal);

    public CaseEntity DeepClone() => new()
    {
        Id = Id,
        Kind = Kind,
        DisplayName = DisplayName,
        Attributes = new Dictionary<string, string>(Attributes, StringComparer.Ordinal)
    };
}

public sealed class SuspectEntityReference
{
    [JsonPropertyName("suspectId")]
    public string SuspectId { get; set; } = string.Empty;

    [JsonPropertyName("personEntityId")]
    public string PersonEntityId { get; set; } = string.Empty;
}

public static partial class CaseEntityContract
{
    private static readonly IReadOnlyDictionary<EntityKind, string> Prefixes =
        new Dictionary<EntityKind, string>
        {
            [EntityKind.Person] = "person",
            [EntityKind.Organization] = "organization",
            [EntityKind.Location] = "location",
            [EntityKind.Device] = "device",
            [EntityKind.Account] = "account",
            [EntityKind.Credential] = "credential",
            [EntityKind.Session] = "session",
            [EntityKind.Document] = "document",
            [EntityKind.PhysicalObject] = "object",
            [EntityKind.Vehicle] = "vehicle",
            [EntityKind.Communication] = "communication",
            [EntityKind.Transaction] = "transaction"
        };

    private static readonly HashSet<string> CanonicalFactAttributeNames = new(
        new[]
        {
            "assignedTo",
            "ownedBy",
            "originatedFrom",
            "paidTo",
            "locatedAt",
            "containedIn",
            "createdBy",
            "accessedBy",
            "subjectId",
            "objectId",
            "eventId",
            "factId",
            "literalValue",
            "visibility",
            "truthStatus"
        },
        StringComparer.OrdinalIgnoreCase);

    public static string PrefixFor(EntityKind kind) => Prefixes[kind];

    public static bool HasValidId(CaseEntity entity) =>
        HasTypedPrefix(entity.Id, PrefixFor(entity.Kind));

    public static StageValidationReport Validate(
        IEnumerable<CaseEntity> entities,
        IEnumerable<SuspectEntityReference>? suspectReferences = null)
    {
        var report = new StageValidationReport();
        var entityList = entities.ToList();
        var entitiesById = new Dictionary<string, CaseEntity>(StringComparer.Ordinal);

        foreach (var entity in entityList)
        {
            if (!HasValidId(entity))
            {
                report.Errors.Add(
                    $"entity '{entity.Id}' must use prefix '{PrefixFor(entity.Kind)}.' for kind '{entity.Kind}'");
            }

            if (string.IsNullOrWhiteSpace(entity.DisplayName))
                report.Errors.Add($"entity '{entity.Id}' has no display name");

            if (!entitiesById.TryAdd(entity.Id, entity))
                report.Errors.Add($"duplicate entity id '{entity.Id}'");

            foreach (var attributeName in entity.Attributes.Keys)
            {
                if (CanonicalFactAttributeNames.Contains(attributeName))
                {
                    report.Errors.Add(
                        $"entity '{entity.Id}' attribute '{attributeName}' must be represented as a canonical fact");
                }
            }
        }

        foreach (var suspectReference in suspectReferences ?? Array.Empty<SuspectEntityReference>())
        {
            if (!HasTypedPrefix(suspectReference.SuspectId, "suspect"))
                report.Errors.Add($"suspect reference '{suspectReference.SuspectId}' must use prefix 'suspect.'");

            if (!entitiesById.TryGetValue(suspectReference.PersonEntityId, out var person))
            {
                report.Errors.Add(
                    $"suspect '{suspectReference.SuspectId}' references missing person entity '{suspectReference.PersonEntityId}'");
            }
            else if (person.Kind != EntityKind.Person)
            {
                report.Errors.Add(
                    $"suspect '{suspectReference.SuspectId}' must reference a Person entity, not '{person.Kind}'");
            }
        }

        return report;
    }

    private static bool HasTypedPrefix(string id, string prefix) =>
        !string.IsNullOrWhiteSpace(id)
        && TypedIdRegex().IsMatch(id)
        && id.StartsWith(prefix + ".", StringComparison.Ordinal);

    [GeneratedRegex("^[a-z][a-z0-9_]*\\.[a-z0-9_]+$", RegexOptions.CultureInvariant)]
    private static partial Regex TypedIdRegex();
}
