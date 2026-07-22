using System.Text.Json;
using CaseGen.Functions.Services.CaseV2;
using Xunit;

namespace CaseGen.Functions.Tests.CaseV2;

public class CaseEntityTests
{
    [Theory]
    [InlineData(EntityKind.Person, "person.felipe")]
    [InlineData(EntityKind.Organization, "organization.freight_market")]
    [InlineData(EntityKind.Location, "location.loading_bay")]
    [InlineData(EntityKind.Device, "device.nb_4471")]
    [InlineData(EntityKind.Account, "account.fm_8821")]
    [InlineData(EntityKind.Credential, "credential.badge_41")]
    [InlineData(EntityKind.Session, "session.s_91af")]
    [InlineData(EntityKind.Document, "document.receipt_18473")]
    [InlineData(EntityKind.PhysicalObject, "object.shipping_crate")]
    [InlineData(EntityKind.Vehicle, "vehicle.van_12")]
    [InlineData(EntityKind.Communication, "communication.call_91")]
    [InlineData(EntityKind.Transaction, "transaction.exp_447")]
    public void Validate_AcceptsStableTypedPrefixes(EntityKind kind, string id)
    {
        var entity = Entity(kind, id);

        var report = CaseEntityContract.Validate(new[] { entity });

        Assert.True(report.IsValid);
    }

    [Fact]
    public void Validate_RejectsWrongPrefixForEntityKind()
    {
        var report = CaseEntityContract.Validate(new[]
        {
            Entity(EntityKind.Device, "account.nb_4471")
        });

        Assert.Contains(report.Errors, error => error.Contains("prefix 'device.'", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RequiresEverySuspectToReferenceAPersonEntity()
    {
        var entities = new[]
        {
            Entity(EntityKind.Person, "person.felipe"),
            Entity(EntityKind.Device, "device.nb_4471")
        };

        var valid = CaseEntityContract.Validate(entities, new[]
        {
            new SuspectEntityReference
            {
                SuspectId = "suspect.felipe",
                PersonEntityId = "person.felipe"
            }
        });
        var invalid = CaseEntityContract.Validate(entities, new[]
        {
            new SuspectEntityReference
            {
                SuspectId = "suspect.device_owner",
                PersonEntityId = "device.nb_4471"
            },
            new SuspectEntityReference
            {
                SuspectId = "suspect.missing",
                PersonEntityId = "person.missing"
            }
        });

        Assert.True(valid.IsValid);
        Assert.Contains(invalid.Errors, error => error.Contains("must reference a Person entity", StringComparison.Ordinal));
        Assert.Contains(invalid.Errors, error => error.Contains("missing person entity", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsAttributesThatRedefineCanonicalFacts()
    {
        var entity = Entity(EntityKind.Device, "device.nb_4471");
        entity.Attributes["model"] = "ThinkBook";
        entity.Attributes["assignedTo"] = "person.felipe";

        var report = CaseEntityContract.Validate(new[] { entity });

        Assert.Single(report.Errors);
        Assert.Contains("must be represented as a canonical fact", report.Errors[0]);
    }

    [Fact]
    public void Serialization_RoundTripsEntityContract()
    {
        var entity = Entity(EntityKind.Account, "account.fm_8821");
        entity.Attributes["provider"] = "Freight Market";

        var json = JsonSerializer.Serialize(entity);
        var restored = JsonSerializer.Deserialize<CaseEntity>(json);

        Assert.NotNull(restored);
        Assert.Equal(entity.Id, restored.Id);
        Assert.Equal(EntityKind.Account, restored.Kind);
        Assert.Equal("Freight Market", restored.Attributes["provider"]);
        Assert.Contains("\"kind\":\"Account\"", json);
    }

    [Fact]
    public void DeepClone_CreatesIndependentSnapshot()
    {
        var entity = Entity(EntityKind.Document, "document.receipt_18473");
        entity.Attributes["format"] = "pdf";

        var snapshot = entity.DeepClone();
        entity.DisplayName = "Changed";
        entity.Attributes["format"] = "image";

        Assert.Equal("Test entity", snapshot.DisplayName);
        Assert.Equal("pdf", snapshot.Attributes["format"]);
    }

    private static CaseEntity Entity(EntityKind kind, string id) => new()
    {
        Id = id,
        Kind = kind,
        DisplayName = "Test entity"
    };
}
