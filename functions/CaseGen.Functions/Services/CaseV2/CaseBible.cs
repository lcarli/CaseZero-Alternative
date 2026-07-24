using System.Globalization;
using System.Text.Json.Serialization;
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
public enum CaseBiblePersonRole
{
    Suspect,
    Victim,
    Witness,
    Investigator,
    Other
}

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
public enum CaseBibleSourceKind
{
    Document,
    DigitalRecord,
    Photograph,
    Testimony,
    PhysicalObject,
    InstitutionalRecord,
    ForensicResult
}

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

    [JsonIgnore]
    public string CanonicalValue => ObjectId ?? LiteralValue ?? string.Empty;

    public CanonicalFact ToCanonicalFact() => new()
    {
        Id = Id,
        Predicate = Predicate,
        SubjectId = SubjectId,
        ObjectId = ObjectId,
        LiteralValue = LiteralValue,
        LiteralType = LiteralType,
        LiteralUnit = LiteralUnit,
        EventId = EventId,
        Visibility = Visibility,
        TruthStatus = TruthStatus,
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
    public static void ApplyRequestOverrides(CaseBible bible, GenerateCaseV2Request request)
    {
        bible.World.Language = ForensicMethodCatalog.NormalizeLanguage(request.Language);
        bible.DifficultyIntent.Difficulty = DifficultyProfileCatalog.Get(request.Difficulty ?? request.RequiredRank).Name;
        if (!string.IsNullOrWhiteSpace(request.Location))
            bible.World.LocationDisplayName = request.Location!;
    }

    public static void Normalize(CaseBible bible)
    {
        if (bible.World.UtcOffset.StartsWith("UTC", StringComparison.OrdinalIgnoreCase))
            bible.World.UtcOffset = bible.World.UtcOffset[3..].Trim();
        foreach (var schedule in bible.WorkSchedules)
            schedule.Id = NormalizeTypedId(schedule.Id);
        var clueIdMap = bible.DifficultyIntent.Clues
            .Where(clue => !string.IsNullOrWhiteSpace(clue.Id))
            .ToDictionary(
                clue => clue.Id,
                clue => FlattenTypedId(clue.Id, "clue"),
                StringComparer.Ordinal);
        foreach (var clue in bible.DifficultyIntent.Clues)
            clue.Id = clueIdMap.GetValueOrDefault(clue.Id) ?? clue.Id;
        foreach (var proofPath in bible.DifficultyIntent.ProofPaths)
        {
            proofPath.ClueIds = proofPath.ClueIds
                .Select(id => clueIdMap.GetValueOrDefault(id) ?? id)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
        NormalizeClueSourceTypes(bible);
        foreach (var person in bible.People.Where(person =>
                     !person.Roles.Contains(CaseBiblePersonRole.Suspect)))
        {
            person.SuspectId = null;
        }
        var primaryLocation = bible.Locations.FirstOrDefault(location =>
            location.Id == bible.Incident.PrimaryLocationId);
        if (primaryLocation is not null && !string.IsNullOrWhiteSpace(bible.World.City))
            primaryLocation.Address.City = bible.World.City;

        var devicesById = bible.Devices
            .Where(device => !string.IsNullOrWhiteSpace(device.Id))
            .GroupBy(device => device.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var accountsById = bible.Accounts
            .Where(account => !string.IsNullOrWhiteSpace(account.Id))
            .GroupBy(account => account.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (var device in bible.Devices)
        {
            foreach (var accountId in device.AccountIds.Where(accountsById.ContainsKey))
            {
                var account = accountsById[accountId];
                if (!account.DeviceIds.Contains(device.Id, StringComparer.Ordinal))
                    account.DeviceIds.Add(device.Id);
            }
        }
        foreach (var account in bible.Accounts)
        {
            foreach (var deviceId in account.DeviceIds.Where(devicesById.ContainsKey))
            {
                var device = devicesById[deviceId];
                if (!device.AccountIds.Contains(account.Id, StringComparer.Ordinal))
                    device.AccountIds.Add(account.Id);
            }
        }

        var factsById = bible.Facts
            .Where(fact => !string.IsNullOrWhiteSpace(fact.Id))
            .GroupBy(fact => fact.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var openingObservationIds = bible.InvestigationConstraints.OpeningObservationIds
            .ToHashSet(StringComparer.Ordinal);
        var playerObservationIds = bible.DifficultyIntent.Clues
            .SelectMany(clue => clue.ObservationIds)
            .Concat(bible.DifficultyIntent.DecoyArcs.SelectMany(decoy =>
                decoy.SuspicionObservationIds.Concat(decoy.VerificationObservationIds)))
            .Concat(bible.DifficultyIntent.ForensicOpportunities.Select(opportunity =>
                opportunity.ResultObservationId))
            .Concat(openingObservationIds)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var observation in bible.Observations)
        {
            if (playerObservationIds.Contains(observation.Id))
                observation.Visibility = FactVisibility.Public;
            if (observation.Visibility == FactVisibility.Public
                && factsById.TryGetValue(observation.FactId, out var fact))
            {
                fact.Visibility = FactVisibility.Public;
            }
        }
        var sourcesById = bible.Sources
            .Where(source => !string.IsNullOrWhiteSpace(source.Id))
            .GroupBy(source => source.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var eventIds = bible.TruthTimeline.Select(beat => beat.Id).ToHashSet(StringComparer.Ordinal);
        var needsCaseContext = bible.Facts.Any(fact =>
            fact.SubjectId is "incident" or "case" or "crime" or "organization.case_context"
            || sourcesById.ContainsKey(fact.SubjectId)
            || eventIds.Contains(fact.SubjectId)
            || factsById.ContainsKey(fact.SubjectId)
            || (!string.IsNullOrWhiteSpace(fact.ObjectId) && sourcesById.ContainsKey(fact.ObjectId)))
            || bible.DifficultyIntent.ForensicOpportunities.Any(opportunity =>
                sourcesById.ContainsKey(opportunity.InputEntityId));
        if (needsCaseContext)
        {
            const string caseContextId = "organization.case_context";
            if (!bible.Institutions.Any(institution => institution.Id == caseContextId))
            {
                bible.Institutions.Add(new CaseBibleInstitution
                {
                    Id = caseContextId,
                    Name = "Case context",
                    Kind = "case"
                });
            }
            foreach (var fact in bible.Facts.Where(fact => fact.SubjectId is "incident" or "case" or "crime"))
                fact.SubjectId = caseContextId;
            foreach (var fact in bible.Facts.Where(fact => eventIds.Contains(fact.SubjectId)))
            {
                fact.EventId ??= fact.SubjectId;
                fact.SubjectId = caseContextId;
            }
            foreach (var fact in bible.Facts.Where(fact => factsById.ContainsKey(fact.SubjectId)))
            {
                var referencedFact = factsById[fact.SubjectId];
                fact.SubjectId = referencedFact.SubjectId == fact.Id
                    ? caseContextId
                    : referencedFact.SubjectId;
                fact.EventId ??= referencedFact.EventId;
            }
            foreach (var fact in bible.Facts.Where(fact => sourcesById.ContainsKey(fact.SubjectId)))
            {
                var source = sourcesById[fact.SubjectId];
                fact.SubjectId = SourceEntityId(source, caseContextId);
            }
            foreach (var fact in bible.Facts.Where(fact =>
                         !string.IsNullOrWhiteSpace(fact.ObjectId)
                         && sourcesById.ContainsKey(fact.ObjectId)))
            {
                fact.ObjectId = SourceEntityId(sourcesById[fact.ObjectId!], caseContextId);
            }
            foreach (var opportunity in bible.DifficultyIntent.ForensicOpportunities
                         .Where(opportunity => sourcesById.ContainsKey(opportunity.InputEntityId)))
            {
                opportunity.InputEntityId = SourceEntityId(
                    sourcesById[opportunity.InputEntityId],
                    caseContextId);
            }
        }
        foreach (var fact in bible.Facts)
        {
            if (!string.IsNullOrWhiteSpace(fact.ObjectId) && !string.IsNullOrWhiteSpace(fact.LiteralValue))
            {
                fact.LiteralValue = null;
                fact.LiteralType = null;
                fact.LiteralUnit = null;
            }
            else if (string.IsNullOrWhiteSpace(fact.ObjectId) && string.IsNullOrWhiteSpace(fact.LiteralValue))
            {
                fact.LiteralValue = "true";
                fact.LiteralType = LiteralValueType.Boolean;
            }
        }
        var duplicateFactIds = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var group in bible.Facts.GroupBy(
                     fact => string.Join(
                         "\u001f",
                         fact.SubjectId,
                         fact.Predicate,
                         fact.ObjectId,
                         fact.LiteralValue,
                         fact.LiteralType,
                         fact.LiteralUnit,
                        fact.EventId),
                     StringComparer.Ordinal))
        {
            var ordered = group.OrderBy(fact => fact.Id, StringComparer.Ordinal).ToList();
            var canonical = ordered[0];
            canonical.IntentionalConflictId ??= ordered
                .Select(fact => fact.IntentionalConflictId)
                .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));
            if (ordered.Any(fact => fact.Visibility == FactVisibility.Public))
                canonical.Visibility = FactVisibility.Public;
            if (ordered.Any(fact => fact.TruthStatus == FactTruthStatus.Confirmed))
                canonical.TruthStatus = FactTruthStatus.Confirmed;
            foreach (var duplicate in ordered.Skip(1))
                duplicateFactIds[duplicate.Id] = canonical.Id;
        }
        if (duplicateFactIds.Count > 0)
        {
            string CanonicalFactId(string id) => duplicateFactIds.GetValueOrDefault(id) ?? id;
            bible.Facts = bible.Facts.Where(fact => !duplicateFactIds.ContainsKey(fact.Id)).ToList();
            foreach (var observation in bible.Observations)
                observation.FactId = CanonicalFactId(observation.FactId);
            foreach (var beat in bible.TruthTimeline)
                beat.FactIds = beat.FactIds.Select(CanonicalFactId).Distinct(StringComparer.Ordinal).ToList();
            foreach (var proofPath in bible.DifficultyIntent.ProofPaths)
                proofPath.ConclusionFactId = CanonicalFactId(proofPath.ConclusionFactId);
            foreach (var conflict in bible.DifficultyIntent.IntentionalConflicts)
                conflict.FactIds = conflict.FactIds.Select(CanonicalFactId).Distinct(StringComparer.Ordinal).ToList();
            bible.InvestigationConstraints.MustPreserveFactIds = bible.InvestigationConstraints.MustPreserveFactIds
                .Select(CanonicalFactId)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
        AddResolvableObservationConflicts(bible);
        AddMissingForensicClues(bible);
        EnsureReliabilityLevels(bible);

        bible.Institutions = bible.Institutions.OrderBy(value => value.Id, StringComparer.Ordinal).ToList();
        bible.Locations = bible.Locations.OrderBy(value => value.Id, StringComparer.Ordinal).ToList();
        bible.TravelRelationships = bible.TravelRelationships.OrderBy(value => value.Id, StringComparer.Ordinal).ToList();
        bible.People = bible.People.OrderBy(value => value.Id, StringComparer.Ordinal).ToList();
        bible.Relationships = bible.Relationships.OrderBy(value => value.Id, StringComparer.Ordinal).ToList();
        bible.Employments = bible.Employments.OrderBy(value => value.Id, StringComparer.Ordinal).ToList();
        bible.WorkSchedules = bible.WorkSchedules
            .OrderBy(value => ParseOrMax(value.Start))
            .ThenBy(value => value.Id, StringComparer.Ordinal)
            .ToList();
        bible.Devices = bible.Devices.OrderBy(value => value.Id, StringComparer.Ordinal).ToList();
        bible.Accounts = bible.Accounts.OrderBy(value => value.Id, StringComparer.Ordinal).ToList();
        bible.Vehicles = bible.Vehicles.OrderBy(value => value.Id, StringComparer.Ordinal).ToList();
        bible.TruthTimeline = bible.TruthTimeline
            .OrderBy(value => ParseOrMax(value.Time))
            .ThenBy(value => value.Id, StringComparer.Ordinal)
            .ToList();
        bible.Sources = bible.Sources.OrderBy(value => value.Id, StringComparer.Ordinal).ToList();
        bible.Facts = bible.Facts.OrderBy(value => value.Id, StringComparer.Ordinal).ToList();
        bible.Observations = bible.Observations.OrderBy(value => value.Id, StringComparer.Ordinal).ToList();
        bible.DifficultyIntent.Clues = bible.DifficultyIntent.Clues.OrderBy(value => value.Id, StringComparer.Ordinal).ToList();
        bible.DifficultyIntent.ProofPaths = bible.DifficultyIntent.ProofPaths.OrderBy(value => value.Id, StringComparer.Ordinal).ToList();
        bible.DifficultyIntent.DecoyArcs = bible.DifficultyIntent.DecoyArcs.OrderBy(value => value.Id, StringComparer.Ordinal).ToList();
        bible.DifficultyIntent.ForensicOpportunities = bible.DifficultyIntent.ForensicOpportunities.OrderBy(value => value.Id, StringComparer.Ordinal).ToList();
        bible.DifficultyIntent.IntentionalConflicts = bible.DifficultyIntent.IntentionalConflicts.OrderBy(value => value.Id, StringComparer.Ordinal).ToList();

        static string SourceEntityId(CaseBibleSource source, string fallback) =>
            source.DeviceId
            ?? source.AccountId
            ?? source.CustodianPersonId
            ?? source.CustodianInstitutionId
            ?? fallback;

        static void AddResolvableObservationConflicts(CaseBible bible)
        {
            var facts = bible.Facts.ToDictionary(fact => fact.Id, StringComparer.Ordinal);
            var usedConflictIds = bible.DifficultyIntent.IntentionalConflicts
                .Select(conflict => conflict.Id)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var observations in bible.Observations
                         .Where(observation => !string.IsNullOrWhiteSpace(observation.ObservedValue))
                         .GroupBy(observation => observation.FactId, StringComparer.Ordinal)
                         .Where(group => group
                             .Select(observation => observation.ObservedValue)
                             .Distinct(StringComparer.Ordinal)
                             .Count() > 1))
            {
                var members = observations.ToArray();
                if (members.Any(observation => !string.IsNullOrWhiteSpace(observation.IntentionalConflictId)))
                    continue;

                var resolution = ResolveObservationConflict(members, facts.GetValueOrDefault(observations.Key));
                if (resolution.Count == 0)
                    continue;

                var baseId = $"conflict.observation_{NormalizeTypedId(observations.Key).Replace('.', '_')}";
                var conflictId = baseId;
                for (var suffix = 2; !usedConflictIds.Add(conflictId); suffix++)
                    conflictId = $"{baseId}_{suffix}";
                foreach (var observation in members)
                    observation.IntentionalConflictId = conflictId;
                if (facts.TryGetValue(observations.Key, out var fact))
                    fact.IntentionalConflictId = conflictId;
                bible.DifficultyIntent.IntentionalConflicts.Add(new CaseBibleIntentionalConflict
                {
                    Id = conflictId,
                    FactIds = facts.ContainsKey(observations.Key) ? [observations.Key] : [],
                    ObservationIds = members.Select(observation => observation.Id).ToList(),
                    ResolutionObservationIds = resolution.Select(observation => observation.Id).ToList(),
                    Purpose = bible.World.Language.ToLowerInvariant() switch
                    {
                        "pt-br" => "Resolver observações conflitantes usando a fonte canônica mais confiável.",
                        "es-es" => "Resolver observaciones contradictorias mediante la fuente canónica más fiable.",
                        "fr-fr" => "Résoudre les observations contradictoires à l’aide de la source canonique la plus fiable.",
                        _ => "Resolve conflicting observations through the most reliable canonical source."
                    }
                });
            }
        }

        static IReadOnlyList<CaseBibleObservation> ResolveObservationConflict(
            IReadOnlyList<CaseBibleObservation> observations,
            CaseBibleFact? fact)
        {
            if (fact is not null && !string.IsNullOrWhiteSpace(fact.CanonicalValue))
            {
                var canonicalMatches = observations
                    .Where(observation => ObservationMatchesFact(observation, fact))
                    .ToArray();
                if (canonicalMatches.Length > 0)
                    return canonicalMatches;
            }

            var bestRank = observations.Max(observation => ReliabilityRank(observation.Reliability));
            var best = observations
                .Where(observation => ReliabilityRank(observation.Reliability) == bestRank)
                .ToArray();
            return best.Select(observation => observation.ObservedValue)
                .Distinct(StringComparer.Ordinal)
                .Count() == 1
                ? best
                : [];
        }

        static bool ObservationMatchesFact(CaseBibleObservation observation, CaseBibleFact fact)
        {
            var observed = observation.ObservedValue;
            var canonical = fact.CanonicalValue;
            if (string.IsNullOrWhiteSpace(observed) || string.IsNullOrWhiteSpace(canonical))
                return false;
            if (string.Equals(observed, canonical, StringComparison.OrdinalIgnoreCase))
                return true;
            if (fact.LiteralType == LiteralValueType.DateTime
                && DateTimeOffset.TryParse(
                    observed,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces,
                    out var observedAt)
                && DateTimeOffset.TryParse(
                    canonical,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces,
                    out var canonicalAt))
            {
                return observedAt.Equals(canonicalAt);
            }
            return fact.LiteralType != LiteralValueType.Boolean
                   && canonical.Length >= 4
                   && (observed.Contains(canonical, StringComparison.OrdinalIgnoreCase)
                       || observed.Length >= 4
                       && canonical.Contains(observed, StringComparison.OrdinalIgnoreCase));
        }

        static int ReliabilityRank(ObservationReliability reliability) =>
            reliability switch
            {
                ObservationReliability.Corroborated => 4,
                ObservationReliability.Verified => 3,
                ObservationReliability.Unverified => 2,
                _ => 1
            };

        static void NormalizeClueSourceTypes(CaseBible bible)
        {
            var observations = bible.Observations.ToDictionary(observation => observation.Id, StringComparer.Ordinal);
            var sources = bible.Sources.ToDictionary(source => source.Id, StringComparer.Ordinal);
            foreach (var clue in bible.DifficultyIntent.Clues)
            {
                var kinds = clue.ObservationIds
                    .Where(observations.ContainsKey)
                    .Select(id => observations[id].SourceId)
                    .Where(sources.ContainsKey)
                    .Select(id => sources[id].Kind)
                    .Distinct()
                    .ToArray();
                if (kinds.Contains(CaseBibleSourceKind.ForensicResult))
                {
                    clue.SourceType = "forensic";
                    continue;
                }
                if (kinds.Length != 1)
                    continue;
                clue.SourceType = kinds[0] switch
                {
                    CaseBibleSourceKind.DigitalRecord => "digital",
                    CaseBibleSourceKind.Photograph or CaseBibleSourceKind.PhysicalObject => "photo",
                    CaseBibleSourceKind.Testimony => "witness",
                    _ => "document"
                };
            }
        }

        static void EnsureReliabilityLevels(CaseBible bible)
        {
            var required = DifficultyProfileCatalog.Get(bible.DifficultyIntent.Difficulty)
                .Topology.MinReliabilityLevels;
            if (required <= 1)
                return;
            var observations = bible.Observations.ToDictionary(observation => observation.Id, StringComparer.Ordinal);
            foreach (var conflict in bible.DifficultyIntent.IntentionalConflicts)
            {
                foreach (var observationId in conflict.ObservationIds.Where(observations.ContainsKey))
                {
                    observations[observationId].Reliability =
                        conflict.ResolutionObservationIds.Contains(observationId, StringComparer.Ordinal)
                            ? ObservationReliability.Corroborated
                            : ObservationReliability.Disputed;
                }
            }

            if (bible.Observations.Select(observation => observation.Reliability).Distinct().Count() >= required)
                return;
            var decoySuspicionIds = bible.DifficultyIntent.DecoyArcs
                .SelectMany(decoy => decoy.SuspicionObservationIds)
                .ToHashSet(StringComparer.Ordinal);
            var decoyVerificationIds = bible.DifficultyIntent.DecoyArcs
                .SelectMany(decoy => decoy.VerificationObservationIds)
                .ToHashSet(StringComparer.Ordinal);
            var assignments = new[]
            {
                (Ids: decoySuspicionIds, Reliability: ObservationReliability.Unverified),
                (Ids: decoyVerificationIds, Reliability: ObservationReliability.Corroborated)
            };
            foreach (var assignment in assignments)
            {
                var candidate = bible.Observations.FirstOrDefault(observation =>
                    assignment.Ids.Contains(observation.Id));
                if (candidate is not null)
                    candidate.Reliability = assignment.Reliability;
                if (bible.Observations.Select(observation => observation.Reliability).Distinct().Count() >= required)
                    break;
            }
        }

        static void AddMissingForensicClues(CaseBible bible)
        {
            var profile = DifficultyProfileCatalog.Get(bible.DifficultyIntent.Difficulty);
            if (profile.AllEvidenceInitial)
                return;
            var culpritId = bible.Incident.CulpritPersonId;
            var forensicClues = bible.DifficultyIntent.Clues
                .Where(clue => clue.SourceType == "forensic" && clue.SupportsPersonId == culpritId)
                .ToList();
            var culpritPaths = bible.DifficultyIntent.ProofPaths
                .Where(path => path.SupportsPersonId == culpritId)
                .OrderBy(path => path.Id, StringComparer.Ordinal)
                .ToArray();
            var pathIndex = 0;
            foreach (var clue in forensicClues.Where(clue =>
                         culpritPaths.All(path => !path.ClueIds.Contains(clue.Id, StringComparer.Ordinal))))
            {
                if (culpritPaths.Length == 0)
                    break;
                culpritPaths[pathIndex % culpritPaths.Length].ClueIds.Add(clue.Id);
                pathIndex++;
            }
            if (forensicClues.Count >= profile.Topology.MinForensicHops)
                return;

            var usedObservationIds = forensicClues
                .SelectMany(clue => clue.ObservationIds)
                .ToHashSet(StringComparer.Ordinal);
            var clueIds = bible.DifficultyIntent.Clues
                .Select(clue => clue.Id)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var opportunity in bible.DifficultyIntent.ForensicOpportunities
                         .Where(opportunity => !usedObservationIds.Contains(opportunity.ResultObservationId))
                         .OrderBy(opportunity => opportunity.Id, StringComparer.Ordinal))
            {
                var baseId = FlattenTypedId($"clue.forensic_{opportunity.Id}", "clue");
                var clueId = baseId;
                for (var suffix = 2; !clueIds.Add(clueId); suffix++)
                    clueId = $"{baseId}_{suffix}";
                bible.DifficultyIntent.Clues.Add(new CaseBibleClueIntent
                {
                    Id = clueId,
                    ObservationIds = { opportunity.ResultObservationId },
                    SupportsPersonId = culpritId,
                    Inference = opportunity.Purpose,
                    Strength = "supporting",
                    SourceType = "forensic",
                    Role = "forensicAttribution"
                });
                usedObservationIds.Add(opportunity.ResultObservationId);
                if (culpritPaths.Length > 0)
                {
                    culpritPaths[pathIndex % culpritPaths.Length].ClueIds.Add(clueId);
                    pathIndex++;
                }
                forensicClues.Add(bible.DifficultyIntent.Clues[^1]);
                if (forensicClues.Count >= profile.Topology.MinForensicHops)
                    break;
            }
        }

        static string NormalizeTypedId(string id)
        {
            var normalized = new string(id
                .Trim()
                .ToLowerInvariant()
                .Select(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_'
                    ? character
                    : '_')
                .ToArray());
            return string.Join(
                '.',
                normalized.Split('.', StringSplitOptions.RemoveEmptyEntries)
                    .Select(segment => segment.Trim('_'))
                    .Where(segment => segment.Length > 0));
        }

        static string FlattenTypedId(string id, string expectedPrefix)
        {
            var normalized = NormalizeTypedId(id);
            var prefix = $"{expectedPrefix}.";
            if (!normalized.StartsWith(prefix, StringComparison.Ordinal))
                return normalized;
            var suffix = normalized[prefix.Length..].Replace('.', '_');
            return $"{prefix}{suffix}";
        }
    }

    public static void ProjectIntoDraft(
        CaseDraft draft,
        IReadOnlyList<string>? clueOrderIds = null,
        IReadOnlyList<string>? decoyOrderIds = null)
    {
        var bible = draft.CaseBible
                    ?? throw new InvalidOperationException("A Case Bible is required before projecting the plot.");
        Normalize(bible);

        var profile = DifficultyProfileCatalog.Get(draft.Request.Difficulty ?? draft.Request.RequiredRank);
        var culprit = bible.People.Single(person => person.Id == bible.Incident.CulpritPersonId);
        var victim = bible.People.Single(person => person.Id == bible.Incident.VictimPersonId);
        var suspects = bible.People
            .Where(person => person.Roles.Contains(CaseBiblePersonRole.Suspect))
            .OrderBy(person => person.SuspectId, StringComparer.Ordinal)
            .ToList();
        var suspectIdByPersonId = suspects.ToDictionary(
            person => person.Id,
            person => person.SuspectId!,
            StringComparer.Ordinal);
        var policeAgency = bible.Institutions.Single(institution => institution.Id == bible.World.PoliceAgencyId);
        var investigator = bible.People.Single(person => person.Id == bible.World.InvestigatorPersonId);

        draft.CulpritId = suspectIdByPersonId[culprit.Id];
        draft.SuspectStubs = suspects.Select(person => new SuspectStub
        {
            Id = person.SuspectId!,
            Name = person.Name,
            Role = person.PublicRole,
            IsCulprit = person.Id == culprit.Id
        }).ToList();

        draft.Metadata.Location = string.IsNullOrWhiteSpace(bible.World.LocationDisplayName)
            ? string.Join(", ", new[] { bible.World.City, bible.World.Region, bible.World.Country }
                .Where(value => !string.IsNullOrWhiteSpace(value)))
            : bible.World.LocationDisplayName;
        draft.Metadata.IncidentDate = bible.Incident.OccurredAt;
        draft.Metadata.OpenedAt = bible.Incident.OpenedAt;
        draft.Metadata.Difficulty = profile.Name;
        draft.Metadata.RequiredRank = profile.Name;
        draft.Metadata.Victim = new PlotVictim
        {
            Name = victim.Name,
            Age = victim.Age,
            Occupation = victim.Occupation
        };

        var observations = bible.Observations.ToDictionary(value => value.Id, StringComparer.Ordinal);
        var clues = Ordered(
                bible.DifficultyIntent.Clues,
                clueOrderIds,
                clue => clue.Id)
            .Select(clue => new CanonicalClue
            {
                Id = clue.Id,
                Discovery = string.Join(" ", clue.ObservationIds
                    .Where(observations.ContainsKey)
                    .Select(id => observations[id].Statement)),
                Inference = clue.Inference,
                SupportsSuspectId = clue.SupportsPersonId is not null
                    && suspectIdByPersonId.TryGetValue(clue.SupportsPersonId, out var suspectId)
                        ? suspectId
                        : null,
                Strength = clue.Strength,
                SourceType = clue.SourceType,
                Role = clue.Role,
                ObservationIds = clue.ObservationIds.Distinct(StringComparer.Ordinal).ToList()
            })
            .ToList();
        var decoyArcs = Ordered(
                bible.DifficultyIntent.DecoyArcs,
                decoyOrderIds,
                decoy => decoy.Id)
            .Select(decoy => new CanonicalRedHerring
            {
                Id = decoy.Id,
                SuspectId = suspectIdByPersonId[decoy.SuspectPersonId],
                Suspicion = decoy.Suspicion,
                Verification = string.Join(" ", decoy.VerificationObservationIds
                    .Where(observations.ContainsKey)
                    .Select(id => observations[id].Statement)),
                Resolution = decoy.Resolution,
                SuspicionObservationIds = decoy.SuspicionObservationIds.Distinct(StringComparer.Ordinal).ToList(),
                VerificationObservationIds = decoy.VerificationObservationIds.Distinct(StringComparer.Ordinal).ToList()
            })
            .ToList();

        draft.Blueprint = new InvestigationBlueprint
        {
            CrimeMechanism = bible.Incident.CrimeMechanism,
            CulpritObjective = bible.Incident.CulpritObjective,
            CaseHook = bible.Incident.CaseHook,
            Locale = new CaseLocaleContext
            {
                TimeZoneId = bible.World.TimeZoneId,
                UtcOffset = bible.World.UtcOffset,
                PoliceAgency = policeAgency.Name,
                InvestigatorName = investigator.Name,
                InvestigatorEmail = investigator.Email ?? string.Empty
            },
            IncidentSequence = bible.TruthTimeline.Select(beat => new CanonicalIncidentBeat
            {
                Id = beat.Id,
                Time = beat.Time,
                Event = beat.Event,
                PublicDescription = beat.PublicDescription,
                Visibility = beat.Visibility == FactVisibility.Public ? "public" : "private",
                Source = beat.Source,
                Verified = beat.Verified,
                Importance = beat.Importance,
                CausalRole = beat.CausalRole
            }).ToList(),
            ClueLadder = clues,
            RedHerrings = decoyArcs
        };
    }

    public static CaseBiblePerson? FindSuspect(CaseDraft draft, string suspectId) =>
        draft.CaseBible?.People.FirstOrDefault(person =>
            string.Equals(person.SuspectId, suspectId, StringComparison.Ordinal));

    private static IReadOnlyList<T> Ordered<T>(
        IReadOnlyList<T> values,
        IReadOnlyList<string>? requestedOrder,
        Func<T, string> idSelector)
    {
        if (requestedOrder is null || requestedOrder.Count == 0)
            return values.OrderBy(idSelector, StringComparer.Ordinal).ToList();
        var byId = values.ToDictionary(idSelector, StringComparer.Ordinal);
        var ordered = requestedOrder
            .Where(byId.ContainsKey)
            .Distinct(StringComparer.Ordinal)
            .Select(id => byId[id])
            .ToList();
        ordered.AddRange(values
            .Where(value => !requestedOrder.Contains(idSelector(value), StringComparer.Ordinal))
            .OrderBy(idSelector, StringComparer.Ordinal));
        return ordered;
    }

    private static DateTimeOffset ParseOrMax(string value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : DateTimeOffset.MaxValue;
}
