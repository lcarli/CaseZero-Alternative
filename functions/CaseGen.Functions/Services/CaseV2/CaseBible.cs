using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CaseGen.Functions.Models.CaseV2;

namespace CaseGen.Functions.Services.CaseV2;

public sealed class CaseBible
{
    [JsonPropertyName("version")] public string Version { get; set; } = "1.0";
    [JsonPropertyName("world")] public CaseBibleWorld World { get; set; } = new();
    [JsonPropertyName("institutions")] public List<CaseBibleInstitution> Institutions { get; set; } = new();
    [JsonPropertyName("locations")] public List<CaseBibleLocation> Locations { get; set; } = new();
    [JsonPropertyName("travelRelationships")] public List<CaseBibleTravelRelationship> TravelRelationships { get; set; } = new();
    [JsonPropertyName("people")] public List<CaseBiblePerson> People { get; set; } = new();
    [JsonPropertyName("relationships")] public List<CaseBibleRelationship> Relationships { get; set; } = new();
    [JsonPropertyName("employments")] public List<CaseBibleEmployment> Employments { get; set; } = new();
    [JsonPropertyName("workSchedules")] public List<CaseBibleWorkSchedule> WorkSchedules { get; set; } = new();
    [JsonPropertyName("devices")] public List<CaseBibleDevice> Devices { get; set; } = new();
    [JsonPropertyName("accounts")] public List<CaseBibleAccount> Accounts { get; set; } = new();
    [JsonPropertyName("vehicles")] public List<CaseBibleVehicle> Vehicles { get; set; } = new();
    [JsonPropertyName("incident")] public CaseBibleIncident Incident { get; set; } = new();
    [JsonPropertyName("truthTimeline")] public List<CaseBibleTruthBeat> TruthTimeline { get; set; } = new();
    [JsonPropertyName("sources")] public List<CaseBibleSource> Sources { get; set; } = new();
    [JsonPropertyName("facts")] public List<CaseBibleFact> Facts { get; set; } = new();
    [JsonPropertyName("observations")] public List<CaseBibleObservation> Observations { get; set; } = new();
    [JsonPropertyName("difficultyIntent")] public CaseBibleDifficultyIntent DifficultyIntent { get; set; } = new();
    [JsonPropertyName("investigationConstraints")] public CaseBibleInvestigationConstraints InvestigationConstraints { get; set; } = new();
}

public sealed class CaseBibleWorld
{
    [JsonPropertyName("language")] public string Language { get; set; } = "en-US";
    [JsonPropertyName("locationDisplayName")] public string LocationDisplayName { get; set; } = string.Empty;
    [JsonPropertyName("city")] public string City { get; set; } = string.Empty;
    [JsonPropertyName("region")] public string Region { get; set; } = string.Empty;
    [JsonPropertyName("country")] public string Country { get; set; } = string.Empty;
    [JsonPropertyName("jurisdiction")] public string Jurisdiction { get; set; } = string.Empty;
    [JsonPropertyName("timeZoneId")] public string TimeZoneId { get; set; } = "UTC";
    [JsonPropertyName("utcOffset")] public string UtcOffset { get; set; } = "+00:00";
    [JsonPropertyName("policeAgencyId")] public string PoliceAgencyId { get; set; } = string.Empty;
    [JsonPropertyName("investigatorPersonId")] public string InvestigatorPersonId { get; set; } = string.Empty;
}

public sealed class CaseBibleInstitution
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("kind")] public string Kind { get; set; } = string.Empty;
    [JsonPropertyName("locationId")] public string? LocationId { get; set; }
    [JsonPropertyName("emailDomain")] public string? EmailDomain { get; set; }
}

public sealed class CaseBibleLocation
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("kind")] public string Kind { get; set; } = string.Empty;
    [JsonPropertyName("address")] public CaseBibleAddress Address { get; set; } = new();
    [JsonPropertyName("institutionId")] public string? InstitutionId { get; set; }
}

public sealed class CaseBibleAddress
{
    [JsonPropertyName("line1")] public string Line1 { get; set; } = string.Empty;
    [JsonPropertyName("line2")] public string? Line2 { get; set; }
    [JsonPropertyName("city")] public string City { get; set; } = string.Empty;
    [JsonPropertyName("region")] public string? Region { get; set; }
    [JsonPropertyName("postalCode")] public string? PostalCode { get; set; }
    [JsonPropertyName("country")] public string Country { get; set; } = string.Empty;
}

public sealed class CaseBibleTravelRelationship
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("fromLocationId")] public string FromLocationId { get; set; } = string.Empty;
    [JsonPropertyName("toLocationId")] public string ToLocationId { get; set; } = string.Empty;
    [JsonPropertyName("mode")] public string Mode { get; set; } = "drive";
    [JsonPropertyName("typicalMinutes")] public int TypicalMinutes { get; set; }
    [JsonPropertyName("minimumMinutes")] public int MinimumMinutes { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CaseBiblePersonRole { Suspect, Victim, Witness, Investigator, Other }

public sealed class CaseBiblePerson
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("suspectId")] public string? SuspectId { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("age")] public int Age { get; set; }
    [JsonPropertyName("roles")] public List<CaseBiblePersonRole> Roles { get; set; } = new();
    [JsonPropertyName("occupation")] public string Occupation { get; set; } = string.Empty;
    [JsonPropertyName("publicRole")] public string PublicRole { get; set; } = string.Empty;
    [JsonPropertyName("relationshipToVictim")] public string RelationshipToVictim { get; set; } = string.Empty;
    [JsonPropertyName("background")] public string Background { get; set; } = string.Empty;
    [JsonPropertyName("privateMotive")] public string? PrivateMotive { get; set; }
    [JsonPropertyName("statedAlibi")] public string? StatedAlibi { get; set; }
    [JsonPropertyName("alibiVerified")] public bool AlibiVerified { get; set; }
    [JsonPropertyName("homeLocationId")] public string? HomeLocationId { get; set; }
    [JsonPropertyName("phone")] public string? Phone { get; set; }
    [JsonPropertyName("email")] public string? Email { get; set; }
}

public sealed class CaseBibleRelationship
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("fromPersonId")] public string FromPersonId { get; set; } = string.Empty;
    [JsonPropertyName("toPersonId")] public string ToPersonId { get; set; } = string.Empty;
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
}

public sealed class CaseBibleEmployment
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("personId")] public string PersonId { get; set; } = string.Empty;
    [JsonPropertyName("institutionId")] public string InstitutionId { get; set; } = string.Empty;
    [JsonPropertyName("locationId")] public string LocationId { get; set; } = string.Empty;
    [JsonPropertyName("jobTitle")] public string JobTitle { get; set; } = string.Empty;
    [JsonPropertyName("startDate")] public string? StartDate { get; set; }
    [JsonPropertyName("endDate")] public string? EndDate { get; set; }
}

public sealed class CaseBibleWorkSchedule
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("personId")] public string PersonId { get; set; } = string.Empty;
    [JsonPropertyName("employmentId")] public string? EmploymentId { get; set; }
    [JsonPropertyName("locationId")] public string LocationId { get; set; } = string.Empty;
    [JsonPropertyName("start")] public string Start { get; set; } = string.Empty;
    [JsonPropertyName("end")] public string End { get; set; } = string.Empty;
    [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
}

public sealed class CaseBibleDevice
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("manufacturer")] public string? Manufacturer { get; set; }
    [JsonPropertyName("model")] public string? Model { get; set; }
    [JsonPropertyName("identifier")] public string Identifier { get; set; } = string.Empty;
    [JsonPropertyName("ownerPersonId")] public string? OwnerPersonId { get; set; }
    [JsonPropertyName("assignedPersonId")] public string? AssignedPersonId { get; set; }
    [JsonPropertyName("usualLocationId")] public string? UsualLocationId { get; set; }
    [JsonPropertyName("accountIds")] public List<string> AccountIds { get; set; } = new();
}

public sealed class CaseBibleAccount
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("provider")] public string Provider { get; set; } = string.Empty;
    [JsonPropertyName("handle")] public string Handle { get; set; } = string.Empty;
    [JsonPropertyName("identifier")] public string Identifier { get; set; } = string.Empty;
    [JsonPropertyName("ownerPersonId")] public string? OwnerPersonId { get; set; }
    [JsonPropertyName("deviceIds")] public List<string> DeviceIds { get; set; } = new();
}

public sealed class CaseBibleVehicle
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("ownerPersonId")] public string? OwnerPersonId { get; set; }
    [JsonPropertyName("plate")] public string Plate { get; set; } = string.Empty;
    [JsonPropertyName("make")] public string Make { get; set; } = string.Empty;
    [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
    [JsonPropertyName("color")] public string Color { get; set; } = string.Empty;
}

public sealed class CaseBibleIncident
{
    [JsonPropertyName("crimeType")] public string CrimeType { get; set; } = string.Empty;
    [JsonPropertyName("culpritPersonId")] public string CulpritPersonId { get; set; } = string.Empty;
    [JsonPropertyName("victimPersonId")] public string VictimPersonId { get; set; } = string.Empty;
    [JsonPropertyName("primaryLocationId")] public string PrimaryLocationId { get; set; } = string.Empty;
    [JsonPropertyName("occurredAt")] public string OccurredAt { get; set; } = string.Empty;
    [JsonPropertyName("discoveredAt")] public string DiscoveredAt { get; set; } = string.Empty;
    [JsonPropertyName("reportedAt")] public string ReportedAt { get; set; } = string.Empty;
    [JsonPropertyName("openedAt")] public string OpenedAt { get; set; } = string.Empty;
    [JsonPropertyName("crimeMechanism")] public string CrimeMechanism { get; set; } = string.Empty;
    [JsonPropertyName("culpritObjective")] public string CulpritObjective { get; set; } = string.Empty;
    [JsonPropertyName("concealment")] public string Concealment { get; set; } = string.Empty;
    [JsonPropertyName("caseHook")] public string CaseHook { get; set; } = string.Empty;
    [JsonPropertyName("privateSummary")] public string PrivateSummary { get; set; } = string.Empty;
}

public sealed class CaseBibleTruthBeat
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("kind")] public EventKind Kind { get; set; }
    [JsonPropertyName("time")] public string Time { get; set; } = string.Empty;
    [JsonPropertyName("endTime")] public string? EndTime { get; set; }
    [JsonPropertyName("locationId")] public string LocationId { get; set; } = string.Empty;
    [JsonPropertyName("participantIds")] public List<string> ParticipantIds { get; set; } = new();
    [JsonPropertyName("deviceIds")] public List<string> DeviceIds { get; set; } = new();
    [JsonPropertyName("accountIds")] public List<string> AccountIds { get; set; } = new();
    [JsonPropertyName("vehicleIds")] public List<string> VehicleIds { get; set; } = new();
    [JsonPropertyName("factIds")] public List<string> FactIds { get; set; } = new();
    [JsonPropertyName("event")] public string Event { get; set; } = string.Empty;
    [JsonPropertyName("publicDescription")] public string? PublicDescription { get; set; }
    [JsonPropertyName("visibility")] public FactVisibility Visibility { get; set; } = FactVisibility.Private;
    [JsonPropertyName("source")] public string Source { get; set; } = "investigation";
    [JsonPropertyName("verified")] public bool Verified { get; set; }
    [JsonPropertyName("importance")] public string Importance { get; set; } = "medium";
    [JsonPropertyName("causalRole")] public string CausalRole { get; set; } = string.Empty;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CaseBibleSourceKind { Document, DigitalRecord, Photograph, Testimony, PhysicalObject, InstitutionalRecord, ForensicResult }

public sealed class CaseBibleSource
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("kind")] public CaseBibleSourceKind Kind { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("custodianPersonId")] public string? CustodianPersonId { get; set; }
    [JsonPropertyName("custodianInstitutionId")] public string? CustodianInstitutionId { get; set; }
    [JsonPropertyName("deviceId")] public string? DeviceId { get; set; }
    [JsonPropertyName("accountId")] public string? AccountId { get; set; }
    [JsonPropertyName("authorPersonId")] public string? AuthorPersonId { get; set; }
    [JsonPropertyName("recordedAt")] public string? RecordedAt { get; set; }
}

public sealed class CaseBibleFact
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("predicate")] public string Predicate { get; set; } = string.Empty;
    [JsonPropertyName("subjectId")] public string SubjectId { get; set; } = string.Empty;
    [JsonPropertyName("objectId")] public string? ObjectId { get; set; }
    [JsonPropertyName("literalValue")] public string? LiteralValue { get; set; }
    [JsonPropertyName("literalType")] public LiteralValueType? LiteralType { get; set; }
    [JsonPropertyName("literalUnit")] public string? LiteralUnit { get; set; }
    [JsonPropertyName("eventId")] public string? EventId { get; set; }
    [JsonPropertyName("visibility")] public FactVisibility Visibility { get; set; } = FactVisibility.Private;
    [JsonPropertyName("truthStatus")] public FactTruthStatus TruthStatus { get; set; } = FactTruthStatus.Confirmed;
    [JsonPropertyName("intentionalConflictId")] public string? IntentionalConflictId { get; set; }
    [JsonIgnore] public string CanonicalValue => ObjectId ?? LiteralValue ?? string.Empty;

    public CanonicalFact ToCanonicalFact() => new()
    {
        Id = Id, Predicate = Predicate, SubjectId = SubjectId, ObjectId = ObjectId,
        LiteralValue = LiteralValue, LiteralType = LiteralType, LiteralUnit = LiteralUnit,
        EventId = EventId, Visibility = Visibility, TruthStatus = TruthStatus,
        IntentionalConflictId = IntentionalConflictId
    };
}

public sealed class CaseBibleObservation
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("factId")] public string FactId { get; set; } = string.Empty;
    [JsonPropertyName("sourceId")] public string SourceId { get; set; } = string.Empty;
    [JsonPropertyName("statement")] public string Statement { get; set; } = string.Empty;
    [JsonPropertyName("observedValue")] public string? ObservedValue { get; set; }
    [JsonPropertyName("observedAt")] public string? ObservedAt { get; set; }
    [JsonPropertyName("fidelity")] public ObservationFidelity Fidelity { get; set; } = ObservationFidelity.DirectRecord;
    [JsonPropertyName("reliability")] public ObservationReliability Reliability { get; set; } = ObservationReliability.Verified;
    [JsonPropertyName("visibility")] public FactVisibility Visibility { get; set; } = FactVisibility.Public;
    [JsonPropertyName("intentionalConflictId")] public string? IntentionalConflictId { get; set; }
    [JsonPropertyName("speakerPersonId")] public string? SpeakerPersonId { get; set; }
}

public sealed class CaseBibleDifficultyIntent
{
    [JsonPropertyName("difficulty")] public string Difficulty { get; set; } = "Detective";
    [JsonPropertyName("clues")] public List<CaseBibleClueIntent> Clues { get; set; } = new();
    [JsonPropertyName("proofPaths")] public List<CaseBibleProofPath> ProofPaths { get; set; } = new();
    [JsonPropertyName("decoyArcs")] public List<CaseBibleDecoyArc> DecoyArcs { get; set; } = new();
    [JsonPropertyName("forensicOpportunities")] public List<CaseBibleForensicOpportunity> ForensicOpportunities { get; set; } = new();
    [JsonPropertyName("intentionalConflicts")] public List<CaseBibleIntentionalConflict> IntentionalConflicts { get; set; } = new();
    [JsonPropertyName("ambiguityNotes")] public List<string> AmbiguityNotes { get; set; } = new();
}

public sealed class CaseBibleClueIntent
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("observationIds")] public List<string> ObservationIds { get; set; } = new();
    [JsonPropertyName("supportsPersonId")] public string? SupportsPersonId { get; set; }
    [JsonPropertyName("inference")] public string Inference { get; set; } = string.Empty;
    [JsonPropertyName("strength")] public string Strength { get; set; } = "supporting";
    [JsonPropertyName("sourceType")] public string SourceType { get; set; } = "document";
    [JsonPropertyName("role")] public string Role { get; set; } = "context";
}

public sealed class CaseBibleProofPath
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("supportsPersonId")] public string SupportsPersonId { get; set; } = string.Empty;
    [JsonPropertyName("clueIds")] public List<string> ClueIds { get; set; } = new();
    [JsonPropertyName("conclusionFactId")] public string ConclusionFactId { get; set; } = string.Empty;
    [JsonPropertyName("independenceKey")] public string IndependenceKey { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
}

public sealed class CaseBibleDecoyArc
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("suspectPersonId")] public string SuspectPersonId { get; set; } = string.Empty;
    [JsonPropertyName("suspicion")] public string Suspicion { get; set; } = string.Empty;
    [JsonPropertyName("suspicionObservationIds")] public List<string> SuspicionObservationIds { get; set; } = new();
    [JsonPropertyName("verificationObservationIds")] public List<string> VerificationObservationIds { get; set; } = new();
    [JsonPropertyName("resolution")] public string Resolution { get; set; } = string.Empty;
}

public sealed class CaseBibleForensicOpportunity
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("inputEntityId")] public string InputEntityId { get; set; } = string.Empty;
    [JsonPropertyName("inputSourceId")] public string InputSourceId { get; set; } = string.Empty;
    [JsonPropertyName("resultObservationId")] public string ResultObservationId { get; set; } = string.Empty;
    [JsonPropertyName("methodHint")] public string MethodHint { get; set; } = string.Empty;
    [JsonPropertyName("purpose")] public string Purpose { get; set; } = string.Empty;
}

public sealed class CaseBibleIntentionalConflict
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("factIds")] public List<string> FactIds { get; set; } = new();
    [JsonPropertyName("observationIds")] public List<string> ObservationIds { get; set; } = new();
    [JsonPropertyName("resolutionObservationIds")] public List<string> ResolutionObservationIds { get; set; } = new();
    [JsonPropertyName("purpose")] public string Purpose { get; set; } = string.Empty;
}

public sealed class CaseBibleInvestigationConstraints
{
    [JsonPropertyName("openingObservationIds")] public List<string> OpeningObservationIds { get; set; } = new();
    [JsonPropertyName("requiredProcedureNotes")] public List<string> RequiredProcedureNotes { get; set; } = new();
    [JsonPropertyName("prohibitedAssumptions")] public List<string> ProhibitedAssumptions { get; set; } = new();
    [JsonPropertyName("allowedForensicMethodIds")] public List<string> AllowedForensicMethodIds { get; set; } = new();
    [JsonPropertyName("mustPreserveFactIds")] public List<string> MustPreserveFactIds { get; set; } = new();
}

public static class CaseBibleNormalizer
{
    // Run only before validation/freeze. Formatting never creates truth, evidence, conflicts or reliability.
    public static void Normalize(CaseBible bible)
    {
        var offsetText = bible.World.UtcOffset.Trim();
        if (offsetText.StartsWith("UTC", StringComparison.OrdinalIgnoreCase))
            offsetText = offsetText[3..].Trim();
        if (offsetText is "Z" or "z") offsetText = "+00:00";
        if (TimeSpan.TryParse(offsetText, CultureInfo.InvariantCulture, out var offset)
            && offset >= TimeSpan.FromHours(-14) && offset <= TimeSpan.FromHours(14)
            && offset.Ticks % TimeSpan.TicksPerMinute == 0)
        {
            var absolute = offset.Duration();
            bible.World.UtcOffset = $"{(offset < TimeSpan.Zero ? '-' : '+')}{absolute.Hours:00}:{absolute.Minutes:00}";
            string Timestamp(string value) =>
                Regex.IsMatch(value.Trim(), @"(?:[zZ]|[+-]\d{2}:\d{2})$")
                && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
                && parsed.UtcTicks + offset.Ticks >= DateTime.MinValue.Ticks
                && parsed.UtcTicks + offset.Ticks <= DateTime.MaxValue.Ticks
                    ? parsed.ToOffset(offset).ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz", CultureInfo.InvariantCulture)
                    : value;
            bible.Incident.OccurredAt = Timestamp(bible.Incident.OccurredAt);
            bible.Incident.DiscoveredAt = Timestamp(bible.Incident.DiscoveredAt);
            bible.Incident.ReportedAt = Timestamp(bible.Incident.ReportedAt);
            bible.Incident.OpenedAt = Timestamp(bible.Incident.OpenedAt);
            foreach (var beat in bible.TruthTimeline)
            {
                beat.Time = Timestamp(beat.Time);
                if (beat.EndTime is not null) beat.EndTime = Timestamp(beat.EndTime);
            }
            foreach (var schedule in bible.WorkSchedules)
            {
                schedule.Start = Timestamp(schedule.Start);
                schedule.End = Timestamp(schedule.End);
            }
            foreach (var source in bible.Sources)
                if (source.RecordedAt is not null) source.RecordedAt = Timestamp(source.RecordedAt);
            foreach (var observation in bible.Observations)
                if (observation.ObservedAt is not null) observation.ObservedAt = Timestamp(observation.ObservedAt);
            foreach (var fact in bible.Facts.Where(f => f.LiteralType == LiteralValueType.DateTime && f.LiteralValue is not null))
                fact.LiteralValue = Timestamp(fact.LiteralValue!);
        }
    }

    public static void ProjectIntoDraft(
        CaseDraft draft, IReadOnlyList<string>? clueOrderIds = null, IReadOnlyList<string>? decoyOrderIds = null)
    {
        var bible = draft.CaseBible ?? throw new InvalidOperationException("A Case Bible is required before projecting the plot.");
        var profile = DifficultyProfileCatalog.Get(draft.Request.Difficulty ?? bible.DifficultyIntent.Difficulty);
        var victim = bible.People.Single(p => p.Id == bible.Incident.VictimPersonId);
        var suspects = bible.People.Where(p => p.Roles.Contains(CaseBiblePersonRole.Suspect))
            .OrderBy(p => p.SuspectId, StringComparer.Ordinal).ToList();
        var suspectIds = suspects.ToDictionary(p => p.Id, p => p.SuspectId!, StringComparer.Ordinal);
        var agency = bible.Institutions.Single(i => i.Id == bible.World.PoliceAgencyId);
        var investigator = bible.People.Single(p => p.Id == bible.World.InvestigatorPersonId);
        draft.CulpritId = suspectIds[bible.Incident.CulpritPersonId];
        draft.SuspectStubs = suspects.Select(p => new SuspectStub
        {
            Id = p.SuspectId!, Name = p.Name, Role = p.PublicRole, IsCulprit = p.Id == bible.Incident.CulpritPersonId
        }).ToList();
        draft.Metadata.Location = string.IsNullOrWhiteSpace(bible.World.LocationDisplayName)
            ? string.Join(", ", new[] { bible.World.City, bible.World.Region, bible.World.Country }.Where(v => !string.IsNullOrWhiteSpace(v)))
            : bible.World.LocationDisplayName;
        draft.Metadata.IncidentDate = bible.Incident.OccurredAt;
        draft.Metadata.OpenedAt = bible.Incident.OpenedAt;
        draft.Metadata.Difficulty = profile.Name;
        draft.Metadata.RequiredRank = CanonicalRequiredRank(draft);
        draft.Metadata.Victim = new PlotVictim { Name = victim.Name, Age = victim.Age, Occupation = victim.Occupation };
        var observations = bible.Observations.ToDictionary(o => o.Id, StringComparer.Ordinal);
        draft.Blueprint = new InvestigationBlueprint
        {
            CrimeMechanism = bible.Incident.CrimeMechanism,
            CulpritObjective = bible.Incident.CulpritObjective,
            CaseHook = bible.Incident.CaseHook,
            Locale = new CaseLocaleContext
            {
                TimeZoneId = bible.World.TimeZoneId, UtcOffset = bible.World.UtcOffset, PoliceAgency = agency.Name,
                InvestigatorName = investigator.Name, InvestigatorEmail = investigator.Email ?? string.Empty
            },
            IncidentSequence = bible.TruthTimeline.Select(b => new CanonicalIncidentBeat
            {
                Id = b.Id, Time = b.Time, Event = b.Event, PublicDescription = b.PublicDescription,
                Visibility = b.Visibility == FactVisibility.Public ? "public" : "private", Source = b.Source,
                Verified = b.Verified, Importance = b.Importance, CausalRole = b.CausalRole
            }).ToList(),
            ClueLadder = Ordered(bible.DifficultyIntent.Clues, clueOrderIds, c => c.Id).Select(c => new CanonicalClue
            {
                Id = c.Id, Discovery = string.Join(" ", c.ObservationIds.Select(id => observations[id].Statement)),
                Inference = c.Inference, SupportsSuspectId = c.SupportsPersonId is not null ? suspectIds.GetValueOrDefault(c.SupportsPersonId) : null,
                Strength = c.Strength, SourceType = c.SourceType, Role = c.Role, ObservationIds = c.ObservationIds.ToList()
            }).ToList(),
            RedHerrings = Ordered(bible.DifficultyIntent.DecoyArcs, decoyOrderIds, d => d.Id).Select(d => new CanonicalRedHerring
            {
                Id = d.Id, SuspectId = suspectIds[d.SuspectPersonId], Suspicion = d.Suspicion,
                Verification = string.Join(" ", d.VerificationObservationIds.Select(id => observations[id].Statement)),
                Resolution = d.Resolution, SuspicionObservationIds = d.SuspicionObservationIds.ToList(),
                VerificationObservationIds = d.VerificationObservationIds.ToList()
            }).ToList()
        };
    }

    public static CaseBiblePerson? FindSuspect(CaseDraft draft, string suspectId) =>
        draft.CaseBible?.People.FirstOrDefault(p => p.SuspectId == suspectId);

    public static string CanonicalRequiredRank(CaseDraft draft) => draft.Request.RequiredRank is not { } requested
        ? DifficultyProfileCatalog.Get(draft.Request.Difficulty ?? draft.CaseBible?.DifficultyIntent.Difficulty).Name
        : GenerationRequestValidator.Ranks.SingleOrDefault(rank => rank.Equals(requested.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new TaskContractException($"Unknown requiredRank '{requested}'.");

    private static IReadOnlyList<T> Ordered<T>(IReadOnlyList<T> values, IReadOnlyList<string>? order, Func<T, string> id)
    {
        if (order is null || order.Count == 0) return values.OrderBy(id, StringComparer.Ordinal).ToList();
        var byId = values.ToDictionary(id, StringComparer.Ordinal);
        TaskContractException.Require(order.Count == values.Count && order.Distinct(StringComparer.Ordinal).Count() == values.Count
            && order.All(byId.ContainsKey), "Presentation order must contain each canonical ID exactly once.");
        return order.Select(key => byId[key]).ToList();
    }
}
