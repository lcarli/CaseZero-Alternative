using System.Globalization;
using System.Text.RegularExpressions;

namespace CaseGen.Functions.Services.CaseV2;

public sealed record CaseBibleValidationIssue(string Code, string NodeId, string Message);

public sealed class CaseBibleValidationReport
{
    public List<CaseBibleValidationIssue> Issues { get; } = new();
    public bool IsValid => Issues.Count == 0;
    public IEnumerable<string> Errors => Issues.Select(issue => $"{issue.Code} [{issue.NodeId}]: {issue.Message}");
}

public sealed class CaseBibleValidationException : InvalidOperationException
{
    public CaseBibleValidationException(CaseBibleValidationReport report)
        : base("Case Bible validation failed: " + string.Join(" | ", report.Errors))
    {
        Report = report;
    }

    public CaseBibleValidationReport Report { get; }
}

public static partial class CaseBibleValidator
{
    public static CaseBibleValidationReport Validate(CaseDraft draft, bool required = false)
    {
        var report = Validate(
            draft.CaseBible,
            draft.Request.Difficulty ?? draft.Request.RequiredRank ?? draft.Metadata.Difficulty,
            draft.Request.Language,
            required);
        if (draft.CaseBible is null)
            return report;

        var bible = draft.CaseBible;
        var expectedLocation = string.IsNullOrWhiteSpace(bible.World.LocationDisplayName)
            ? string.Join(", ", new[] { bible.World.City, bible.World.Region, bible.World.Country }
                .Where(value => !string.IsNullOrWhiteSpace(value)))
            : bible.World.LocationDisplayName;
        Alignment(draft.Metadata.Location, expectedLocation, "metadata.location");
        Alignment(draft.Metadata.IncidentDate, bible.Incident.OccurredAt, "metadata.incidentDate");
        Alignment(draft.Metadata.OpenedAt, bible.Incident.OpenedAt, "metadata.openedAt");
        Alignment(draft.Metadata.Difficulty, bible.DifficultyIntent.Difficulty, "metadata.difficulty");
        Alignment(draft.Metadata.RequiredRank, bible.DifficultyIntent.Difficulty, "metadata.requiredRank");
        var canonicalVictim = bible.People.FirstOrDefault(value => value.Id == bible.Incident.VictimPersonId);
        Alignment(draft.Metadata.Victim?.Name, canonicalVictim?.Name, "metadata.victim.name");
        if (draft.Metadata.Victim is not null && canonicalVictim is not null
            && draft.Metadata.Victim.Age != canonicalVictim.Age)
        {
            report.Issues.Add(new CaseBibleValidationIssue(
                "projection_value_mismatch",
                "metadata.victim.age",
                $"downstream age '{draft.Metadata.Victim.Age}' does not match canonical age '{canonicalVictim.Age}'"));
        }
        Alignment(draft.Metadata.Victim?.Occupation, canonicalVictim?.Occupation, "metadata.victim.occupation");
        Alignment(draft.Blueprint.CrimeMechanism, bible.Incident.CrimeMechanism, "blueprint.crimeMechanism");
        Alignment(draft.Blueprint.CulpritObjective, bible.Incident.CulpritObjective, "blueprint.culpritObjective");
        Alignment(draft.Blueprint.CaseHook, bible.Incident.CaseHook, "blueprint.caseHook");
        Alignment(draft.Blueprint.Locale.TimeZoneId, bible.World.TimeZoneId, "blueprint.locale.timeZoneId");
        Alignment(draft.Blueprint.Locale.UtcOffset, bible.World.UtcOffset, "blueprint.locale.utcOffset");
        var policeAgency = bible.Institutions.FirstOrDefault(value => value.Id == bible.World.PoliceAgencyId)?.Name;
        var investigator = bible.People.FirstOrDefault(value => value.Id == bible.World.InvestigatorPersonId);
        Alignment(draft.Blueprint.Locale.PoliceAgency, policeAgency, "blueprint.locale.policeAgency");
        Alignment(draft.Blueprint.Locale.InvestigatorName, investigator?.Name, "blueprint.locale.investigatorName");
        Alignment(draft.Blueprint.Locale.InvestigatorEmail, investigator?.Email, "blueprint.locale.investigatorEmail");
        var culpritSuspectId = bible.People
            .FirstOrDefault(value => value.Id == bible.Incident.CulpritPersonId)?.SuspectId;
        Alignment(draft.CulpritId, culpritSuspectId, "culpritId");

        var bibleSuspects = bible.People
            .Where(person => person.Roles.Contains(CaseBiblePersonRole.Suspect))
            .Where(person => !string.IsNullOrWhiteSpace(person.SuspectId))
            .GroupBy(person => person.SuspectId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (var suspect in draft.SuspectStubs)
        {
            if (!bibleSuspects.TryGetValue(suspect.Id, out var person))
            {
                report.Issues.Add(new CaseBibleValidationIssue(
                    "projection_suspect_unknown",
                    suspect.Id,
                    "downstream suspect does not exist in the canonical Case Bible"));
                continue;
            }
            Alignment(suspect.Name, person.Name, suspect.Id + ".name");
        }
        if (draft.SuspectStubs.Select(value => value.Id).ToHashSet(StringComparer.Ordinal)
            .SetEquals(bibleSuspects.Keys) is false)
        {
            report.Issues.Add(new CaseBibleValidationIssue(
                "projection_suspect_set_mismatch",
                "suspects",
                "downstream suspect set differs from the canonical Case Bible"));
        }
        foreach (var suspect in draft.SuspectFull)
        {
            if (!bibleSuspects.TryGetValue(suspect.Id, out var person))
                continue;
            Alignment(suspect.Name, person.Name, suspect.Id + ".name");
            if (suspect.Age.HasValue && suspect.Age.Value != person.Age)
            {
                report.Issues.Add(new CaseBibleValidationIssue(
                    "projection_value_mismatch",
                    suspect.Id + ".age",
                    $"downstream age '{suspect.Age}' does not match canonical age '{person.Age}'"));
            }
            Alignment(suspect.Occupation, person.Occupation, suspect.Id + ".occupation");
            Alignment(suspect.Relationship, person.RelationshipToVictim, suspect.Id + ".relationship");
        }

        var canonicalBeats = bible.TruthTimeline
            .GroupBy(value => value.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (var beat in draft.Blueprint.IncidentSequence)
        {
            if (!canonicalBeats.TryGetValue(beat.Id, out var canonical))
            {
                report.Issues.Add(new CaseBibleValidationIssue(
                    "projection_event_unknown",
                    beat.Id,
                    "downstream incident beat does not exist in the canonical Case Bible"));
                continue;
            }
            Alignment(beat.Time, canonical.Time, beat.Id + ".time");
            Alignment(beat.Event, canonical.Event, beat.Id + ".event");
        }
        if (!draft.Blueprint.IncidentSequence.Select(value => value.Id).ToHashSet(StringComparer.Ordinal)
                .SetEquals(canonicalBeats.Keys))
        {
            report.Issues.Add(new CaseBibleValidationIssue(
                "projection_event_set_mismatch",
                "blueprint.incidentSequence",
                "downstream incident beat set differs from the canonical Case Bible"));
        }

        var observations = bible.Observations
            .GroupBy(value => value.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var clueIntents = bible.DifficultyIntent.Clues
            .GroupBy(value => value.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (var clue in draft.Blueprint.ClueLadder)
        {
            if (!clueIntents.TryGetValue(clue.Id, out var intent))
            {
                report.Issues.Add(new CaseBibleValidationIssue(
                    "projection_clue_unknown",
                    clue.Id,
                    "downstream clue does not exist in the canonical Case Bible"));
                continue;
            }
            if (!clue.ObservationIds.ToHashSet(StringComparer.Ordinal)
                    .SetEquals(intent.ObservationIds))
            {
                report.Issues.Add(new CaseBibleValidationIssue(
                    "projection_clue_observation_set_mismatch",
                    clue.Id,
                    "downstream clue observation IDs differ from the canonical Case Bible"));
            }
            var expectedDiscovery = string.Join(" ", intent.ObservationIds
                .Where(observations.ContainsKey)
                .Select(id => observations[id].Statement));
            if (clue.ObservationIds.Count == 0)
            {
                report.Issues.Add(new CaseBibleValidationIssue(
                    "projection_clue_observations_missing",
                    clue.Id,
                    "downstream clue is not linked to canonical observations"));
            }
            else
            {
                Alignment(clue.Discovery, expectedDiscovery, clue.Id + ".discovery");
            }
            Alignment(clue.Inference, intent.Inference, clue.Id + ".inference");
            Alignment(clue.SourceType, intent.SourceType, clue.Id + ".sourceType");
            Alignment(clue.Role, intent.Role, clue.Id + ".role");
            Alignment(clue.Strength, intent.Strength, clue.Id + ".strength");
        }
        if (!draft.Blueprint.ClueLadder.Select(value => value.Id).ToHashSet(StringComparer.Ordinal)
                .SetEquals(clueIntents.Keys))
        {
            report.Issues.Add(new CaseBibleValidationIssue(
                "projection_clue_set_mismatch",
                "blueprint.clueLadder",
                "downstream clue set differs from the canonical Case Bible"));
        }
        var decoyIntents = bible.DifficultyIntent.DecoyArcs
            .GroupBy(value => value.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (var redHerring in draft.Blueprint.RedHerrings)
        {
            if (!decoyIntents.TryGetValue(redHerring.Id, out var intent))
            {
                report.Issues.Add(new CaseBibleValidationIssue(
                    "projection_decoy_unknown",
                    redHerring.Id,
                    "downstream decoy does not exist in the canonical Case Bible"));
                continue;
            }
            var expectedSuspectId = bible.People.FirstOrDefault(person => person.Id == intent.SuspectPersonId)?.SuspectId;
            Alignment(redHerring.SuspectId, expectedSuspectId, redHerring.Id + ".suspectId");
            Alignment(redHerring.Suspicion, intent.Suspicion, redHerring.Id + ".suspicion");
            Alignment(redHerring.Resolution, intent.Resolution, redHerring.Id + ".resolution");
            Alignment(
                redHerring.Verification,
                string.Join(" ", intent.VerificationObservationIds
                    .Where(observations.ContainsKey)
                    .Select(id => observations[id].Statement)),
                redHerring.Id + ".verification");
            if (!redHerring.SuspicionObservationIds.ToHashSet(StringComparer.Ordinal)
                    .SetEquals(intent.SuspicionObservationIds)
                || !redHerring.VerificationObservationIds.ToHashSet(StringComparer.Ordinal)
                    .SetEquals(intent.VerificationObservationIds))
            {
                report.Issues.Add(new CaseBibleValidationIssue(
                    "projection_decoy_observation_set_mismatch",
                    redHerring.Id,
                    "downstream decoy observation IDs differ from the canonical Case Bible"));
            }
        }
        if (!draft.Blueprint.RedHerrings.Select(value => value.Id).ToHashSet(StringComparer.Ordinal)
                    .SetEquals(decoyIntents.Keys))
        {
            report.Issues.Add(new CaseBibleValidationIssue(
                    "projection_decoy_set_mismatch",
                    "blueprint.redHerrings",
                    "downstream decoy set differs from the canonical Case Bible"));
        }
        return report;

        void Alignment(string? actual, string? expected, string nodeId)
        {
            if (!string.Equals(actual ?? string.Empty, expected ?? string.Empty, StringComparison.Ordinal))
            {
                report.Issues.Add(new CaseBibleValidationIssue(
                    "projection_value_mismatch",
                    nodeId,
                    $"downstream value '{actual}' does not match canonical value '{expected}'"));
            }
        }
    }

    public static CaseBibleValidationReport Validate(
        CaseBible? bible,
        string? difficulty,
        string? language,
        bool required = false)
    {
        var report = new CaseBibleValidationReport();
        if (bible is null)
        {
            if (required)
                Add("case_bible_missing", "caseBible", "a canonical Case Bible is required for generated graph-mode cases");
            return report;
        }

        var allIds = new Dictionary<string, string>(StringComparer.Ordinal);
        var personIds = Ids(bible.People, person => person.Id, "person", "people");
        var institutionIds = Ids(bible.Institutions, value => value.Id, "organization", "institutions");
        var locationIds = Ids(bible.Locations, value => value.Id, "location", "locations");
        var travelIds = Ids(bible.TravelRelationships, value => value.Id, "travel", "travelRelationships");
        var relationshipIds = Ids(bible.Relationships, value => value.Id, "relationship", "relationships");
        var employmentIds = Ids(bible.Employments, value => value.Id, "employment", "employments");
        var scheduleIds = Ids(bible.WorkSchedules, value => value.Id, "schedule", "workSchedules");
        var deviceIds = Ids(bible.Devices, value => value.Id, "device", "devices");
        var accountIds = Ids(bible.Accounts, value => value.Id, "account", "accounts");
        var vehicleIds = Ids(bible.Vehicles, value => value.Id, "vehicle", "vehicles");
        var eventIds = Ids(bible.TruthTimeline, value => value.Id, "event", "truthTimeline");
        var sourceIds = Ids(bible.Sources, value => value.Id, "source", "sources");
        var factIds = Ids(bible.Facts, value => value.Id, "fact", "facts");
        var observationIds = Ids(bible.Observations, value => value.Id, "observation", "observations");
        var clueIds = Ids(bible.DifficultyIntent.Clues, value => value.Id, "clue", "difficultyIntent.clues");
        var proofIds = Ids(bible.DifficultyIntent.ProofPaths, value => value.Id, "proof", "difficultyIntent.proofPaths");
        var decoyIds = Ids(bible.DifficultyIntent.DecoyArcs, value => value.Id, "decoy", "difficultyIntent.decoyArcs");
        var forensicIds = Ids(bible.DifficultyIntent.ForensicOpportunities, value => value.Id, "forensic", "difficultyIntent.forensicOpportunities");
        var conflictIds = Ids(bible.DifficultyIntent.IntentionalConflicts, value => value.Id, "conflict", "difficultyIntent.intentionalConflicts");
        _ = travelIds;
        _ = relationshipIds;
        _ = scheduleIds;
        _ = proofIds;
        _ = decoyIds;
        _ = forensicIds;

        var suspectIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var person in bible.People)
        {
            if (string.IsNullOrWhiteSpace(person.Name))
                Add("person_name_missing", person.Id, "person name is required");
            if (person.Age is < 1 or > 110)
                Add("person_age_invalid", person.Id, $"age {person.Age} is outside the plausible range 1-110");
            if (person.Roles.Count == 0)
                Add("person_role_missing", person.Id, "at least one person role is required");
            if (person.Roles.Contains(CaseBiblePersonRole.Suspect))
            {
                if (!HasPrefix(person.SuspectId, "suspect"))
                    Add("suspect_id_invalid", person.Id, "suspect people require a stable 'suspect.<id>' suspectId");
                else if (!suspectIds.Add(person.SuspectId!))
                    Add("duplicate_suspect_id", person.Id, $"suspectId '{person.SuspectId}' is duplicated");
            }
            else if (!string.IsNullOrWhiteSpace(person.SuspectId))
            {
                Add("unexpected_suspect_id", person.Id, "only people with the Suspect role may declare suspectId");
            }
            Reference(person.HomeLocationId, locationIds, person.Id, "homeLocationId");
        }

        Required(bible.World.City, "world.city");
        Required(bible.World.Country, "world.country");
        Required(bible.World.Jurisdiction, "world.jurisdiction");
        Required(bible.World.LocationDisplayName, "world.locationDisplayName");
        var expectedLanguage = ForensicMethodCatalog.NormalizeLanguage(language);
        if (!string.Equals(bible.World.Language, expectedLanguage, StringComparison.Ordinal))
            Add("language_mismatch", "world.language", $"expected selected request language '{expectedLanguage}'");
        RequiredReference(bible.World.PoliceAgencyId, institutionIds, "world", "policeAgencyId");
        RequiredReference(bible.World.InvestigatorPersonId, personIds, "world", "investigatorPersonId");
        if (!OffsetRegex().IsMatch(bible.World.UtcOffset))
            Add("utc_offset_invalid", "world.utcOffset", $"'{bible.World.UtcOffset}' is not a valid UTC offset");
        var locale = LocaleProfileCatalog.Get(expectedLanguage);
        if (!locale.TimeZoneIds.Contains(bible.World.TimeZoneId))
            Add("timezone_invalid", "world.timeZoneId", $"'{bible.World.TimeZoneId}' is not valid for {locale.Language}");
        var canonicalAgency = bible.Institutions.FirstOrDefault(institution => institution.Id == bible.World.PoliceAgencyId);
        if (canonicalAgency is not null
            && !Regex.IsMatch(
                canonicalAgency.Name,
                locale.AgencyNamePattern,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            Add("police_agency_invalid", canonicalAgency.Id, $"agency name does not match {locale.Language} locale conventions");
        }
        var canonicalInvestigator = bible.People.FirstOrDefault(person => person.Id == bible.World.InvestigatorPersonId);
        if (canonicalInvestigator is not null
            && !canonicalInvestigator.Roles.Contains(CaseBiblePersonRole.Investigator))
        {
            Add("investigator_role_missing", "world.investigatorPersonId", "canonical investigator lacks the Investigator role");
        }
        if (canonicalInvestigator is not null && string.IsNullOrWhiteSpace(canonicalInvestigator.Email))
            Add("investigator_email_missing", canonicalInvestigator.Id, "canonical investigator requires an email address");

        foreach (var institution in bible.Institutions)
        {
            Required(institution.Name, institution.Id);
            Reference(institution.LocationId, locationIds, institution.Id, "locationId");
        }
        foreach (var location in bible.Locations)
        {
            Required(location.Name, location.Id);
            Required(location.Address.Line1, $"{location.Id}.address.line1");
            Required(location.Address.City, $"{location.Id}.address.city");
            Required(location.Address.Country, $"{location.Id}.address.country");
            Reference(location.InstitutionId, institutionIds, location.Id, "institutionId");
        }
        foreach (var travel in bible.TravelRelationships)
        {
            RequiredReference(travel.FromLocationId, locationIds, travel.Id, "fromLocationId");
            RequiredReference(travel.ToLocationId, locationIds, travel.Id, "toLocationId");
            if (travel.MinimumMinutes <= 0 || travel.TypicalMinutes < travel.MinimumMinutes || travel.TypicalMinutes > 240)
                Add("travel_range_invalid", travel.Id, "travel minutes must be positive, typical >= minimum, and typical <= 240");
        }
        foreach (var relationship in bible.Relationships)
        {
            RequiredReference(relationship.FromPersonId, personIds, relationship.Id, "fromPersonId");
            RequiredReference(relationship.ToPersonId, personIds, relationship.Id, "toPersonId");
            Required(relationship.Type, relationship.Id);
            Required(relationship.Description, relationship.Id);
        }
        foreach (var employment in bible.Employments)
        {
            RequiredReference(employment.PersonId, personIds, employment.Id, "personId");
            RequiredReference(employment.InstitutionId, institutionIds, employment.Id, "institutionId");
            RequiredReference(employment.LocationId, locationIds, employment.Id, "locationId");
            Required(employment.JobTitle, employment.Id);
            ValidateDate(employment.StartDate, employment.Id, "startDate");
            ValidateDate(employment.EndDate, employment.Id, "endDate");
            if (DateOnly.TryParse(employment.StartDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var employmentStart)
                && DateOnly.TryParse(employment.EndDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var employmentEnd)
                && employmentEnd < employmentStart)
            {
                Add("employment_range_invalid", employment.Id, "employment endDate cannot precede startDate");
            }
        }
        foreach (var schedule in bible.WorkSchedules)
        {
            RequiredReference(schedule.PersonId, personIds, schedule.Id, "personId");
            Reference(schedule.EmploymentId, employmentIds, schedule.Id, "employmentId");
            RequiredReference(schedule.LocationId, locationIds, schedule.Id, "locationId");
            var start = ParseTimestamp(schedule.Start, schedule.Id, "start");
            var end = ParseTimestamp(schedule.End, schedule.Id, "end");
            if (start.HasValue && end.HasValue
                && (end <= start || end - start > TimeSpan.FromHours(16)))
            {
                Add("schedule_range_invalid", schedule.Id, "work schedule must end after it starts and cannot exceed 16 hours");
            }
            if (TryParseOffset(bible.World.UtcOffset, out var scheduleOffset))
            {
                if (start.HasValue && start.Value.Offset != scheduleOffset)
                    Add("schedule_offset_mismatch", schedule.Id, $"start does not use canonical offset '{bible.World.UtcOffset}'");
                if (end.HasValue && end.Value.Offset != scheduleOffset)
                    Add("schedule_offset_mismatch", schedule.Id, $"end does not use canonical offset '{bible.World.UtcOffset}'");
            }
            if (DateTimeOffset.TryParse(
                    bible.Incident.OccurredAt,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var incidentAnchor)
                && start.HasValue
                && Math.Abs((start.Value - incidentAnchor).TotalDays) > 14)
            {
                Add("schedule_not_incident_relevant", schedule.Id, "work schedule is more than 14 days from the incident");
            }
        }
        foreach (var device in bible.Devices)
        {
            Required(device.Identifier, device.Id);
            Reference(device.OwnerPersonId, personIds, device.Id, "ownerPersonId");
            Reference(device.AssignedPersonId, personIds, device.Id, "assignedPersonId");
            Reference(device.UsualLocationId, locationIds, device.Id, "usualLocationId");
            References(device.AccountIds, accountIds, device.Id, "accountIds");
            foreach (var accountId in device.AccountIds)
            {
                var account = bible.Accounts.FirstOrDefault(value => value.Id == accountId);
                if (account is not null && !account.DeviceIds.Contains(device.Id, StringComparer.Ordinal))
                    Add("device_account_link_not_reciprocal", device.Id, $"account '{accountId}' does not reference this device");
            }
        }
        foreach (var account in bible.Accounts)
        {
            Required(account.Provider, account.Id);
            Required(account.Identifier, account.Id);
            Reference(account.OwnerPersonId, personIds, account.Id, "ownerPersonId");
            References(account.DeviceIds, deviceIds, account.Id, "deviceIds");
            foreach (var deviceId in account.DeviceIds)
            {
                var device = bible.Devices.FirstOrDefault(value => value.Id == deviceId);
                if (device is not null && !device.AccountIds.Contains(account.Id, StringComparer.Ordinal))
                    Add("account_device_link_not_reciprocal", account.Id, $"device '{deviceId}' does not reference this account");
            }
        }
        foreach (var vehicle in bible.Vehicles)
        {
            Required(vehicle.Plate, vehicle.Id);
            Reference(vehicle.OwnerPersonId, personIds, vehicle.Id, "ownerPersonId");
        }

        RequiredReference(bible.Incident.CulpritPersonId, personIds, "incident", "culpritPersonId");
        RequiredReference(bible.Incident.VictimPersonId, personIds, "incident", "victimPersonId");
        RequiredReference(bible.Incident.PrimaryLocationId, locationIds, "incident", "primaryLocationId");
        var primaryLocation = bible.Locations.FirstOrDefault(location => location.Id == bible.Incident.PrimaryLocationId);
        if (primaryLocation is not null
            && !string.Equals(primaryLocation.Address.City, bible.World.City, StringComparison.OrdinalIgnoreCase))
        {
            Add("primary_location_city_mismatch", primaryLocation.Id, "primary incident address city differs from canonical world city");
        }
        Required(bible.Incident.CrimeMechanism, "incident.crimeMechanism");
        Required(bible.Incident.CulpritObjective, "incident.culpritObjective");
        Required(bible.Incident.PrivateSummary, "incident.privateSummary");
        var occurredAt = ParseTimestamp(bible.Incident.OccurredAt, "incident", "occurredAt");
        var discoveredAt = ParseTimestamp(bible.Incident.DiscoveredAt, "incident", "discoveredAt");
        var reportedAt = ParseTimestamp(bible.Incident.ReportedAt, "incident", "reportedAt");
        var openedAt = ParseTimestamp(bible.Incident.OpenedAt, "incident", "openedAt");
        ValidateOrder(occurredAt, discoveredAt, "incident.discoveredAt", "discovery cannot precede the incident");
        ValidateOrder(discoveredAt, reportedAt, "incident.reportedAt", "reporting cannot precede discovery");
        ValidateOrder(reportedAt, openedAt, "incident.openedAt", "case opening cannot precede reporting");
        foreach (var timestamp in new[] { occurredAt, discoveredAt, reportedAt, openedAt }.Where(value => value.HasValue))
        {
            if (TryParseOffset(bible.World.UtcOffset, out var offset) && timestamp!.Value.Offset != offset)
                Add("timestamp_offset_mismatch", "incident", $"timestamp '{timestamp.Value:O}' does not use canonical offset '{bible.World.UtcOffset}'");
        }

        var culprit = bible.People.FirstOrDefault(person => person.Id == bible.Incident.CulpritPersonId);
        if (culprit is not null && !culprit.Roles.Contains(CaseBiblePersonRole.Suspect))
            Add("culprit_not_suspect", "incident.culpritPersonId", "culprit person is not in the suspect set");
        var victim = bible.People.FirstOrDefault(person => person.Id == bible.Incident.VictimPersonId);
        if (victim is not null && !victim.Roles.Contains(CaseBiblePersonRole.Victim))
            Add("victim_role_missing", "incident.victimPersonId", "victim person does not have the Victim role");

        var previousBeat = (DateTimeOffset?)null;
        foreach (var beat in bible.TruthTimeline)
        {
            var time = ParseTimestamp(beat.Time, beat.Id, "time");
            var endTime = string.IsNullOrWhiteSpace(beat.EndTime)
                ? null
                : ParseTimestamp(beat.EndTime!, beat.Id, "endTime");
            if (time.HasValue && previousBeat.HasValue && time < previousBeat)
                Add("truth_timeline_nonchronological", beat.Id, "truth timeline beats must be emitted in chronological order");
            if (time.HasValue)
                previousBeat = time;
            if (time.HasValue && endTime.HasValue && endTime < time)
                Add("truth_beat_window_invalid", beat.Id, "endTime cannot precede time");
            if (TryParseOffset(bible.World.UtcOffset, out var beatOffset))
            {
                if (time.HasValue && time.Value.Offset != beatOffset)
                    Add("truth_beat_offset_mismatch", beat.Id, $"time does not use canonical offset '{bible.World.UtcOffset}'");
                if (endTime.HasValue && endTime.Value.Offset != beatOffset)
                    Add("truth_beat_offset_mismatch", beat.Id, $"endTime does not use canonical offset '{bible.World.UtcOffset}'");
            }
            RequiredReference(beat.LocationId, locationIds, beat.Id, "locationId");
            References(beat.ParticipantIds, personIds, beat.Id, "participantIds");
            References(beat.DeviceIds, deviceIds, beat.Id, "deviceIds");
            References(beat.AccountIds, accountIds, beat.Id, "accountIds");
            References(beat.VehicleIds, vehicleIds, beat.Id, "vehicleIds");
            References(beat.FactIds, factIds, beat.Id, "factIds");
            Required(beat.Event, beat.Id);
            if (beat.Visibility == FactVisibility.Public && string.IsNullOrWhiteSpace(beat.PublicDescription))
                Add("public_description_missing", beat.Id, "public timeline beats require a publicDescription");
        }
        if (bible.TruthTimeline.Count(beat => beat.Visibility == FactVisibility.Public) < 2)
            Add("public_timeline_insufficient", "truthTimeline", "at least two canonical truth beats must be player-visible");
        if (occurredAt.HasValue
            && !bible.TruthTimeline.Any(beat =>
                DateTimeOffset.TryParse(
                    beat.Time,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var beatTime)
                && beatTime == occurredAt
                && beat.LocationId == bible.Incident.PrimaryLocationId
                && beat.ParticipantIds.Contains(bible.Incident.CulpritPersonId, StringComparer.Ordinal)))
        {
            Add(
                "incident_beat_missing",
                "truthTimeline",
                "truth timeline must contain the exact incident beat with culprit and primary location");
        }

        foreach (var source in bible.Sources)
        {
            Required(source.Name, source.Id);
            Reference(source.CustodianPersonId, personIds, source.Id, "custodianPersonId");
            Reference(source.CustodianInstitutionId, institutionIds, source.Id, "custodianInstitutionId");
            Reference(source.DeviceId, deviceIds, source.Id, "deviceId");
            Reference(source.AccountId, accountIds, source.Id, "accountId");
        }

        var referencableIds = personIds
            .Concat(institutionIds)
            .Concat(locationIds)
            .Concat(deviceIds)
            .Concat(accountIds)
            .Concat(vehicleIds)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var fact in bible.Facts)
        {
            RequiredReference(fact.SubjectId, referencableIds, fact.Id, "subjectId");
            Reference(fact.ObjectId, referencableIds, fact.Id, "objectId");
            Reference(fact.EventId, eventIds, fact.Id, "eventId");
            if (string.IsNullOrWhiteSpace(fact.ObjectId) == string.IsNullOrWhiteSpace(fact.LiteralValue))
                Add("fact_value_invalid", fact.Id, "exactly one of objectId or literalValue is required");
            if (!string.IsNullOrWhiteSpace(fact.LiteralValue) && fact.LiteralType is null)
                Add("fact_literal_type_missing", fact.Id, "literal facts require literalType");
        }
        foreach (var observation in bible.Observations)
        {
            RequiredReference(observation.FactId, factIds, observation.Id, "factId");
            RequiredReference(observation.SourceId, sourceIds, observation.Id, "sourceId");
            Required(observation.Statement, observation.Id);
            if (!string.IsNullOrWhiteSpace(observation.ObservedAt))
                ParseTimestamp(observation.ObservedAt!, observation.Id, "observedAt");
            var observedFact = bible.Facts.FirstOrDefault(fact => fact.Id == observation.FactId);
            if (observation.Visibility == FactVisibility.Public
                && observedFact?.Visibility == FactVisibility.Private)
            {
                Add(
                    "private_fact_public_observation",
                    observation.Id,
                    $"public observation cannot directly expose private fact '{observation.FactId}'");
            }
        }

        var profile = DifficultyProfileCatalog.Get(difficulty ?? bible.DifficultyIntent.Difficulty);
        var suspectCount = bible.People.Count(person => person.Roles.Contains(CaseBiblePersonRole.Suspect));
        if (suspectCount < profile.MinSuspects || suspectCount > profile.MaxSuspects)
            Add("suspect_count_invalid", "difficultyIntent", $"{profile.Name} requires {profile.MinSuspects}-{profile.MaxSuspects} suspects");
        if (!string.Equals(bible.DifficultyIntent.Difficulty, profile.Name, StringComparison.Ordinal))
            Add("difficulty_mismatch", "difficultyIntent.difficulty", $"expected canonical difficulty '{profile.Name}'");

        var allowedSourceTypes = new HashSet<string>(
            ["document", "digital", "photo", "witness", "forensic"],
            StringComparer.Ordinal);
        var culpritClues = new List<CaseBibleClueIntent>();
        foreach (var clue in bible.DifficultyIntent.Clues)
        {
            References(clue.ObservationIds, observationIds, clue.Id, "observationIds");
            Reference(clue.SupportsPersonId, personIds, clue.Id, "supportsPersonId");
            if (clue.ObservationIds.Count == 0)
                Add("clue_observation_missing", clue.Id, "canonical clue intent requires at least one observation");
            if (!allowedSourceTypes.Contains(clue.SourceType))
                Add("clue_source_type_invalid", clue.Id, $"sourceType '{clue.SourceType}' is unsupported");
            if (clue.SupportsPersonId == bible.Incident.CulpritPersonId
                && clue.Strength is "supporting" or "decisive")
                culpritClues.Add(clue);
        }
        if (culpritClues.Count < profile.MinIndependentCulpritSources)
            Add("proof_intent_insufficient", "difficultyIntent.clues", $"{profile.Name} requires at least {profile.MinIndependentCulpritSources} culprit-supporting clues");
        if (culpritClues.Select(clue => clue.SourceType).Distinct(StringComparer.Ordinal).Count()
            < profile.MinIndependentCulpritSources)
            Add("proof_source_diversity_insufficient", "difficultyIntent.clues", "culprit proof intent does not use enough independent source types");
        var culpritBridgeSourceCount = culpritClues
            .Where(clue => clue.SourceType != "forensic")
            .SelectMany(clue => clue.ObservationIds)
            .Where(observationIds.Contains)
            .Select(id => bible.Observations.First(observation => observation.Id == id).SourceId)
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (!profile.AllEvidenceInitial
            && culpritBridgeSourceCount < profile.MinIndependentCulpritSources)
        {
            Add(
                "proof_origin_diversity_insufficient",
                "difficultyIntent.clues",
                $"{profile.Name} requires {profile.MinIndependentCulpritSources} distinct non-forensic evidentiary origins");
        }
        if (culpritClues.Count(clue => clue.Strength == "decisive") != 1)
            Add("decisive_clue_count_invalid", "difficultyIntent.clues", "exactly one culprit clue must be decisive");
        if (profile.AllEvidenceInitial && culpritClues.Any(clue => clue.SourceType == "forensic"))
            Add("rookie_forensic_intent_forbidden", "difficultyIntent.clues", "Rookie cases cannot contain forensic clue intent");
        if (!profile.AllEvidenceInitial
            && !culpritClues.Any(clue => clue.Strength == "decisive" && clue.SourceType == "forensic"))
            Add("forensic_decisive_intent_missing", "difficultyIntent.clues", "non-Rookie cases require a decisive forensic culprit clue");
        if (!profile.AllEvidenceInitial
            && !culpritClues.Any(clue => string.Equals(clue.Role, "identity", StringComparison.Ordinal)))
            Add("identity_bridge_intent_missing", "difficultyIntent.clues", "non-Rookie cases require an initial identity clue");
        if (!profile.AllEvidenceInitial
            && !culpritClues.Any(clue => string.Equals(clue.Role, "action", StringComparison.Ordinal)))
            Add("action_bridge_intent_missing", "difficultyIntent.clues", "non-Rookie cases require an independent action clue");
        if (!profile.AllEvidenceInitial
            && !culpritClues.Any(clue =>
                string.Equals(clue.Role, "forensicAttribution", StringComparison.Ordinal)
                && clue.Strength == "decisive"))
            Add("forensic_attribution_intent_missing", "difficultyIntent.clues", "non-Rookie cases require a decisive forensic-attribution clue");
        if (!profile.AllEvidenceInitial
            && culpritClues.Count(clue => clue.SourceType == "forensic") < profile.Topology.MinForensicHops)
            Add("forensic_clue_intent_insufficient", "difficultyIntent.clues", $"{profile.Name} requires at least {profile.Topology.MinForensicHops} forensic clue hops");

        var independenceKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in bible.DifficultyIntent.ProofPaths)
        {
            RequiredReference(path.SupportsPersonId, personIds, path.Id, "supportsPersonId");
            RequiredReference(path.ConclusionFactId, factIds, path.Id, "conclusionFactId");
            References(path.ClueIds, clueIds, path.Id, "clueIds");
            if (path.ClueIds.Count == 0)
                Add("proof_path_clues_missing", path.Id, "proof path requires at least one canonical clue");
            if (path.SupportsPersonId != bible.Incident.CulpritPersonId)
                Add("proof_path_target_invalid", path.Id, "canonical proof paths must support the culprit");
            if (string.IsNullOrWhiteSpace(path.IndependenceKey) || !independenceKeys.Add(path.IndependenceKey))
                Add("proof_path_not_independent", path.Id, "proof paths require distinct non-empty independenceKey values");
        }
        if (bible.DifficultyIntent.ProofPaths.Count < profile.Topology.MinIndependentProofPaths)
            Add("proof_path_count_insufficient", "difficultyIntent.proofPaths", $"{profile.Name} requires at least {profile.Topology.MinIndependentProofPaths} independent proof paths");

        var decoyPeople = new HashSet<string>(StringComparer.Ordinal);
        foreach (var decoy in bible.DifficultyIntent.DecoyArcs)
        {
            RequiredReference(decoy.SuspectPersonId, personIds, decoy.Id, "suspectPersonId");
            References(decoy.SuspicionObservationIds, observationIds, decoy.Id, "suspicionObservationIds");
            References(decoy.VerificationObservationIds, observationIds, decoy.Id, "verificationObservationIds");
            if (decoy.SuspectPersonId == bible.Incident.CulpritPersonId)
                Add("decoy_targets_culprit", decoy.Id, "a decoy arc cannot target the culprit");
            var decoyPerson = bible.People.FirstOrDefault(person => person.Id == decoy.SuspectPersonId);
            if (decoyPerson is not null && !decoyPerson.Roles.Contains(CaseBiblePersonRole.Suspect))
                Add("decoy_target_not_suspect", decoy.Id, "decoy arc target is not in the suspect set");
            if (!decoyPeople.Add(decoy.SuspectPersonId))
                Add("duplicate_decoy_target", decoy.Id, "each decoy arc must target a different suspect");
            if (decoy.SuspicionObservationIds.Count == 0 || decoy.VerificationObservationIds.Count == 0)
                Add("decoy_evidence_missing", decoy.Id, "decoy arcs require both suspicion and verification observations");
        }
        if (bible.DifficultyIntent.DecoyArcs.Count < profile.Topology.RequiredDecoyArcs)
            Add("decoy_intent_insufficient", "difficultyIntent.decoyArcs", $"{profile.Name} requires at least {profile.Topology.RequiredDecoyArcs} decoy arcs");

        foreach (var opportunity in bible.DifficultyIntent.ForensicOpportunities)
        {
            RequiredReference(opportunity.InputEntityId, referencableIds, opportunity.Id, "inputEntityId");
            RequiredReference(opportunity.InputSourceId, sourceIds, opportunity.Id, "inputSourceId");
            RequiredReference(opportunity.ResultObservationId, observationIds, opportunity.Id, "resultObservationId");
            if (!string.IsNullOrWhiteSpace(opportunity.MethodHint)
                && ForensicMethodCatalog.Find(opportunity.MethodHint) is null)
                Add("forensic_method_unknown", opportunity.Id, $"methodHint '{opportunity.MethodHint}' is not in the immutable forensic catalog");
        }
        if (profile.AllEvidenceInitial && bible.DifficultyIntent.ForensicOpportunities.Count > 0)
            Add("rookie_forensic_opportunity_forbidden", "difficultyIntent.forensicOpportunities", "Rookie cases cannot contain forensic opportunities");
        if (!profile.AllEvidenceInitial
            && bible.DifficultyIntent.ForensicOpportunities.Count < profile.Topology.MinForensicHops)
            Add("forensic_intent_insufficient", "difficultyIntent.forensicOpportunities", $"{profile.Name} requires at least {profile.Topology.MinForensicHops} forensic opportunities");
        if (profile.Topology.RequiresConflictingObservation
            && bible.DifficultyIntent.IntentionalConflicts.Count == 0)
            Add("intentional_conflict_intent_missing", "difficultyIntent.intentionalConflicts", $"{profile.Name} requires an explicitly marked intentional conflict");

        var conflicts = bible.DifficultyIntent.IntentionalConflicts
            .GroupBy(value => value.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (var conflict in bible.DifficultyIntent.IntentionalConflicts)
        {
            References(conflict.FactIds, factIds, conflict.Id, "factIds");
            References(conflict.ObservationIds, observationIds, conflict.Id, "observationIds");
            References(conflict.ResolutionObservationIds, observationIds, conflict.Id, "resolutionObservationIds");
            if (conflict.FactIds.Count + conflict.ObservationIds.Count < 2)
                Add("intentional_conflict_too_small", conflict.Id, "an intentional conflict must identify at least two conflicting facts or observations");
            Required(conflict.Purpose, conflict.Id);
        }
        foreach (var fact in bible.Facts.Where(value => !string.IsNullOrWhiteSpace(value.IntentionalConflictId)))
            ValidateConflictMembership(fact.IntentionalConflictId!, fact.Id, true);
        foreach (var observation in bible.Observations.Where(value => !string.IsNullOrWhiteSpace(value.IntentionalConflictId)))
            ValidateConflictMembership(observation.IntentionalConflictId!, observation.Id, false);

        foreach (var group in bible.Facts.GroupBy(
                     fact => $"{fact.SubjectId}\u001f{fact.Predicate}\u001f{fact.EventId}",
                     StringComparer.Ordinal))
        {
            var members = group.ToList();
            if (members.Select(fact => fact.CanonicalValue).Distinct(StringComparer.Ordinal).Count() > 1
                && !IsPotentiallyMultiValuedPredicate(members[0].Predicate)
                && !HasDeclaredConflict(members.Select(fact => fact.IntentionalConflictId), members.Select(fact => fact.Id), true))
            {
                Add("unmarked_fact_contradiction", members[0].Id, $"conflicting canonical values for predicate '{members[0].Predicate}' are not marked as one intentional conflict");
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
        foreach (var group in bible.Observations
                     .Where(observation => !string.IsNullOrWhiteSpace(observation.ObservedValue))
                     .GroupBy(observation => observation.FactId, StringComparer.Ordinal))
        {
            var members = group.ToList();
            if (members.Select(observation => observation.ObservedValue).Distinct(StringComparer.Ordinal).Count() > 1
                && !HasDeclaredConflict(members.Select(observation => observation.IntentionalConflictId), members.Select(observation => observation.Id), false))
            {
                Add("unmarked_observation_contradiction", members[0].Id, $"conflicting observations of fact '{group.Key}' are not marked as one intentional conflict");
            }
        }

        References(bible.InvestigationConstraints.OpeningObservationIds, observationIds, "investigationConstraints", "openingObservationIds");
        foreach (var observationId in bible.InvestigationConstraints.OpeningObservationIds)
        {
            var observation = bible.Observations.FirstOrDefault(value => value.Id == observationId);
            if (observation is not null && observation.Visibility != FactVisibility.Public)
                Add("opening_observation_private", observationId, "opening observations must be player-visible");
        }
        References(bible.InvestigationConstraints.MustPreserveFactIds, factIds, "investigationConstraints", "mustPreserveFactIds");
        foreach (var methodId in bible.InvestigationConstraints.AllowedForensicMethodIds)
        {
            if (ForensicMethodCatalog.Find(methodId) is null)
                Add("constraint_forensic_method_unknown", "investigationConstraints.allowedForensicMethodIds", $"method '{methodId}' is not in the immutable forensic catalog");
        }

        return report;

        HashSet<string> Ids<T>(
            IEnumerable<T> values,
            Func<T, string> idSelector,
            string prefix,
            string collection)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                var id = idSelector(value);
                if (!HasPrefix(id, prefix))
                    Add("typed_id_invalid", string.IsNullOrWhiteSpace(id) ? collection : id, $"ID must use the '{prefix}.' typed prefix");
                if (!ids.Add(id))
                    Add("duplicate_id", id, $"ID '{id}' is duplicated in {collection}");
                if (!string.IsNullOrWhiteSpace(id))
                {
                    if (allIds.TryGetValue(id, out var existing))
                        Add("duplicate_global_id", id, $"ID '{id}' is already used by {existing}");
                    else
                        allIds[id] = collection;
                }
            }
            return ids;
        }

        void Required(string? value, string nodeId)
        {
            if (string.IsNullOrWhiteSpace(value))
                Add("required_value_missing", nodeId, "required value is missing");
        }

        void Reference(string? value, IReadOnlySet<string> valid, string nodeId, string field)
        {
            if (!string.IsNullOrWhiteSpace(value) && !valid.Contains(value))
                Add("broken_reference", nodeId, $"{field} references unknown ID '{value}'");
        }

        void RequiredReference(string? value, IReadOnlySet<string> valid, string nodeId, string field)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                Add("missing_reference", nodeId, $"{field} requires a typed ID");
                return;
            }
            Reference(value, valid, nodeId, field);
        }

        void References(IEnumerable<string> values, IReadOnlySet<string> valid, string nodeId, string field)
        {
            foreach (var value in values.Distinct(StringComparer.Ordinal))
                RequiredReference(value, valid, nodeId, field);
        }

        DateTimeOffset? ParseTimestamp(string value, string nodeId, string field)
        {
            if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
                return parsed;
            Add("timestamp_invalid", nodeId, $"{field} value '{value}' is not an ISO-8601 timestamp with offset");
            return null;
        }

        void ValidateDate(string? value, string nodeId, string field)
        {
            if (!string.IsNullOrWhiteSpace(value)
                && !DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                Add("date_invalid", nodeId, $"{field} value '{value}' is not an ISO date");
        }

        void ValidateOrder(DateTimeOffset? before, DateTimeOffset? after, string nodeId, string message)
        {
            if (before.HasValue && after.HasValue && after < before)
                Add("incident_chronology_invalid", nodeId, message);
        }

        void ValidateConflictMembership(string conflictId, string memberId, bool fact)
        {
            if (!conflicts.TryGetValue(conflictId, out var conflict))
            {
                Add("intentional_conflict_missing", memberId, $"intentionalConflictId '{conflictId}' does not exist");
                return;
            }
            var members = fact ? conflict.FactIds : conflict.ObservationIds;
            if (!members.Contains(memberId, StringComparer.Ordinal))
                Add("intentional_conflict_membership_missing", memberId, $"conflict '{conflictId}' does not list this member");
        }

        bool HasDeclaredConflict(
            IEnumerable<string?> declaredIds,
            IEnumerable<string> memberIds,
            bool facts)
        {
            var declared = declaredIds
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (declared.Count != 1 || !conflicts.TryGetValue(declared[0]!, out var conflict))
                return false;
            var members = facts ? conflict.FactIds : conflict.ObservationIds;
            return memberIds.All(id => members.Contains(id, StringComparer.Ordinal));
        }

        void Add(string code, string nodeId, string message) =>
            report.Issues.Add(new CaseBibleValidationIssue(code, nodeId, message));
    }

    private static bool HasPrefix(string? id, string prefix) =>
        !string.IsNullOrWhiteSpace(id)
        && TypedIdRegex().IsMatch(id)
        && id.StartsWith(prefix + ".", StringComparison.Ordinal);

    private static bool TryParseOffset(string value, out TimeSpan offset)
    {
        offset = default;
        if (!OffsetRegex().IsMatch(value))
            return false;
        var sign = value[0] == '-' ? -1 : 1;
        offset = TimeSpan.FromMinutes(sign * (int.Parse(value.AsSpan(1, 2), CultureInfo.InvariantCulture) * 60
                                              + int.Parse(value.AsSpan(4, 2), CultureInfo.InvariantCulture)));
        return true;
    }

    [GeneratedRegex("^[a-z][a-z0-9_]*\\.[a-z0-9_]+$", RegexOptions.CultureInvariant)]
    private static partial Regex TypedIdRegex();

    [GeneratedRegex("^[+-](0\\d|1[0-4]):[0-5]\\d$", RegexOptions.CultureInvariant)]
    private static partial Regex OffsetRegex();
}
