namespace CaseGen.Functions.Services.CaseV2;

public static class EvidenceRoles
{
    public const string Primary = "primary";
    public const string Corroborative = "corroborative";
    public const string Contextual = "contextual";

    public static bool IsInvestigative(string? role) =>
        string.Equals(role, Primary, StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, Corroborative, StringComparison.OrdinalIgnoreCase);
}

public static class ImagePurposes
{
    public const string Scene = "scene";
    public const string SuspectPortrait = "suspect_portrait";
    public const string Object = "object";
    public const string Surveillance = "surveillance";
}

public sealed record EvidenceArchetype(
    string Id,
    string Family,
    string Type,
    string Layout,
    string DisplayName,
    string Purpose,
    int Rarity);

public sealed record EvidenceDossierSlot(
    string InstanceId,
    EvidenceArchetype Archetype,
    string EvidenceRole,
    string? SubjectSuspectId = null,
    string? ImagePurpose = null,
    string? SuggestedTitle = null)
{
    public string Id => Archetype.Id;
    public string Family => Archetype.Family;
    public string Type => Archetype.Type;
    public string Layout => Archetype.Layout;
    public string DisplayName => Archetype.DisplayName;
    public string Purpose => Archetype.Purpose;
    public int Rarity => Archetype.Rarity;
}

public sealed record EvidencePortfolioBudget(
    string Difficulty,
    int MinAssets,
    int MaxAssets,
    int MinInvestigativeAssets,
    int MaxInvestigativeAssets,
    int MinScenePhotos,
    int MaxScenePhotos,
    int MinFamilies,
    int MaxRarity);

public static class EvidenceArchetypeCatalog
{
    public static readonly IReadOnlyList<EvidenceArchetype> All =
    [
        new("incident_report", "official", "document", "PoliceReport", "Incident report", "Establish the initial official account and its limitations.", 1),
        new("search_warrant", "official", "document", "SearchWarrant", "Search warrant", "Define the legal scope, probable cause, place searched, and items sought.", 2),
        new("dispatch_log", "official", "document", "DispatchLog", "Dispatch log", "Anchor calls, unit movements, response times, and changing information.", 2),
        new("evidence_log", "official", "document", "EvidenceLog", "Evidence inventory", "Track collection details and expose custody or timing discrepancies.", 1),
        new("custody_form", "official", "document", "CustodyForm", "Custody record", "Show access, sign-out history, transfers, or controlled-item handling.", 2),
        new("medical_report", "official", "document", "MedicalReport", "Medical report", "Provide bounded medical observations relevant to timing or mechanism.", 2),

        new("suspect_interview", "testimonial", "document", "InterviewTranscript", "Suspect interview", "Capture an alibi, evasions, corrections, and testable claims in formal questioning.", 1),
        new("witness_statement", "testimonial", "document", "WitnessStatement", "Witness statement", "Provide a first-person account with realistic uncertainty and limited perspective.", 1),
        new("recorded_call", "testimonial", "document", "AudioTranscript", "Recorded call transcript", "Preserve spoken timing, tone, background sounds, and partial recollection.", 2),
        new("anonymous_tip", "testimonial", "document", "PersonalLetter", "Anonymous tip", "Introduce a claim whose credibility must be tested against independent evidence.", 3),

        new("newspaper_clipping", "journalistic", "document", "NewspaperClipping", "Newspaper clipping", "Separate the public narrative from facts known only to investigators.", 1),
        new("obituary", "journalistic", "document", "NewspaperClipping", "Obituary", "Reveal relationships, survivors, public reputation, and omitted conflicts.", 2),
        new("press_release", "journalistic", "document", "NewspaperClipping", "Press release clipping", "Show what authorities publicly disclosed and what they deliberately withheld.", 3),

        new("personal_letter", "personal", "document", "PersonalLetter", "Personal letter", "Expose private intentions, strained relationships, or a dated commitment.", 1),
        new("receipt", "personal", "document", "Receipt", "Receipt or invoice", "Corroborate a purchase, location, amount, or narrow timestamp.", 1),
        new("calendar", "personal", "document", "Calendar", "Calendar or appointment page", "Establish routines, meetings, deadlines, and suspicious changes.", 2),
        new("travel_ticket", "personal", "document", "Receipt", "Travel ticket", "Test travel time, presence, route, or an alibi.", 2),
        new("insurance_or_will", "personal", "document", "PersonalLetter", "Insurance or will extract", "Establish a concrete financial interest without making motive alone decisive.", 3),

        new("scene_photo", "visual", "photo", "Photo", "Scene photograph", "Show spatial details, condition, omissions, or an object in context.", 1),
        new("suspect_portrait", "visual", "photo", "Photo", "Suspect portrait", "Provide a neutral identification portrait for the case dossier.", 1),
        new("case_map", "visual", "document", "CaseMap", "Case map or floor plan", "Support route, access, sightline, distance, and opportunity reasoning.", 1),
        new("surveillance_still", "visual", "photo", "Photo", "Surveillance still", "Provide a bounded visual observation without impossible enhancement.", 2),

        new("call_log", "digital", "digital", "CallLog", "Call detail record", "Correlate contacts and timestamps without exposing message content.", 1),
        new("pos_export", "digital", "digital", "PosExport", "Point-of-sale export", "Confirm a transaction, operator, item, and time.", 2),
        new("phone_dump", "digital", "digital", "PhoneDump", "Phone extraction", "Combine device metadata with selected messages and application artifacts.", 2),
        new("sensor_log", "digital", "digital", "SensorLog", "Sensor log", "Provide machine-timestamped access or environmental events.", 1),
        new("browser_history", "digital", "digital", "BrowserHistory", "Browser history", "Reveal research, preparation, or a misleading shared-device trail.", 2),
        new("bank_statement", "digital", "digital", "BankStatement", "Bank statement", "Show specific transfers, debts, or unusual financial timing.", 1),
        new("gps_track", "digital", "digital", "GpsTrack", "Location track", "Test movement while preserving accuracy limitations and gaps.", 2),
        new("chat_export", "digital", "digital", "ChatExport", "Chat export", "Reveal relationship dynamics, plans, edits, or missing context.", 1),
        new("email_export", "digital", "digital", "EmailExport", "Email export", "Establish correspondence, headers, attachments, and chronology.", 2),
        new("access_log", "digital", "digital", "AccessLog", "Access-control log", "Test authorized entry, credential use, and system-side timestamps.", 1)
    ];

    public static IReadOnlyList<EvidenceDossierSlot> BuildPortfolio(CaseDraft draft)
    {
        var key = PositiveKey(draft);
        var budget = GetBudget(draft);
        var suspects = GetSuspects(draft);
        var sceneCount = Pick(budget.MinScenePhotos, budget.MaxScenePhotos, key / 3);
        var mandatoryInvestigativeCount = 1 + suspects.Count + sceneCount;
        var needsDigitalAsset = draft.Blueprint.ClueLadder.Any(clue =>
            string.Equals(clue.SourceType, "digital", StringComparison.OrdinalIgnoreCase));
        var needsSpecializedFiller = needsDigitalAsset || draft.Blueprint.ClueLadder.Any(clue =>
            string.Equals(clue.SourceType, "forensic", StringComparison.OrdinalIgnoreCase));
        if (needsSpecializedFiller
            && mandatoryInvestigativeCount >= budget.MaxInvestigativeAssets
            && sceneCount > budget.MinScenePhotos)
        {
            sceneCount--;
            mandatoryInvestigativeCount--;
        }
        var minimumInvestigative = Math.Max(
            budget.MinInvestigativeAssets,
            mandatoryInvestigativeCount + (needsSpecializedFiller ? 1 : 0));
        minimumInvestigative = Math.Min(minimumInvestigative, budget.MaxInvestigativeAssets);
        var mandatoryTotal = mandatoryInvestigativeCount + suspects.Count;
        var minimumFamilyFillers = Math.Max(0, budget.MinFamilies - 3);
        var minimumTotal = Math.Max(
            budget.MinAssets,
            Math.Max(minimumInvestigative + suspects.Count, mandatoryTotal + minimumFamilyFillers));
        var targetCount = Pick(minimumTotal, budget.MaxAssets, key + draft.Blueprint.ClueLadder.Count);
        var maximumInvestigative = Math.Min(budget.MaxInvestigativeAssets, targetCount - suspects.Count);
        var targetInvestigative = Pick(minimumInvestigative, maximumInvestigative, key / 5);

        var slots = new List<EvidenceDossierSlot>();
        var usedIds = new HashSet<string>(StringComparer.Ordinal);
        AddSlot(slots, usedIds, "asset.initial_report", Required("incident_report"), EvidenceRoles.Primary,
            title: Localized(draft, "Initial incident report", "Relatório inicial da ocorrência", "Informe inicial del incidente", "Rapport initial d’incident"));

        for (var index = 1; index <= sceneCount; index++)
        {
            AddSlot(slots, usedIds, $"asset.scene_photo_{index}", Required("scene_photo"),
                index == 1 ? EvidenceRoles.Primary : EvidenceRoles.Corroborative,
                imagePurpose: ImagePurposes.Scene,
                title: Localized(draft, $"Scene photograph {index}", $"Fotografia da cena {index}", $"Fotografía de la escena {index}", $"Photographie de la scène {index}"));
        }

        foreach (var suspect in suspects)
        {
            var suffix = IdSuffix(suspect.Id);
            AddSlot(slots, usedIds, $"asset.portrait_{suffix}", Required("suspect_portrait"), EvidenceRoles.Contextual,
                suspect.Id, ImagePurposes.SuspectPortrait,
                Localized(draft, $"Portrait — {suspect.Name}", $"Retrato — {suspect.Name}", $"Retrato — {suspect.Name}", $"Portrait — {suspect.Name}"));
            AddSlot(slots, usedIds, $"asset.interview_{suffix}", Required("suspect_interview"),
                suspect.Id == draft.CulpritId ? EvidenceRoles.Primary : EvidenceRoles.Corroborative,
                suspect.Id, null,
                Localized(draft, $"Interview — {suspect.Name}", $"Entrevista — {suspect.Name}", $"Entrevista — {suspect.Name}", $"Entretien — {suspect.Name}"));
        }

        var specializedSlots = 0;
        if (needsDigitalAsset)
        {
            var availableSpecializedSlots = Math.Max(1, targetInvestigative - mandatoryInvestigativeCount);
            var digitalArchetypes = SemanticArchetypeIds(draft)
                .Select(Find)
                .Where(archetype => archetype?.Family == "digital")
                .Select(archetype => archetype!)
                .DistinctBy(archetype => archetype.Id, StringComparer.Ordinal)
                .Take(availableSpecializedSlots)
                .ToList();
            if (digitalArchetypes.Count == 0)
                digitalArchetypes.Add(Required("access_log"));
            foreach (var digitalArchetype in digitalArchetypes)
            {
                AddSlot(
                    slots,
                    usedIds,
                    $"asset.{digitalArchetype.Id}",
                    digitalArchetype,
                    EvidenceRoles.Corroborative,
                    title: digitalArchetype.DisplayName);
                specializedSlots++;
            }
        }

        AddFillers(
            slots,
            usedIds,
            targetInvestigative - mandatoryInvestigativeCount - specializedSlots,
            draft,
            budget,
            EvidenceRoles.Corroborative,
            key);
        AddFillers(
            slots,
            usedIds,
            targetCount - slots.Count,
            draft,
            budget,
            EvidenceRoles.Contextual,
            key / 7);

        return slots;
    }

    public static EvidencePortfolioBudget GetBudget(CaseDraft draft)
    {
        var profile = DifficultyProfileCatalog.Get(draft);
        return new(
            profile.Name,
            profile.MinAssets,
            profile.MaxAssets,
            profile.MinInvestigativeAssets,
            profile.MaxInvestigativeAssets,
            profile.MinScenePhotos,
            profile.MaxScenePhotos,
            profile.MinFamilies,
            profile.MaxRarity);
    }

    public static EvidenceArchetype? Find(string id) =>
        All.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.Ordinal));

    private static void AddFillers(
        List<EvidenceDossierSlot> slots,
        HashSet<string> usedIds,
        int count,
        CaseDraft draft,
        EvidencePortfolioBudget budget,
        string evidenceRole,
        int key)
    {
        if (count <= 0) return;

        var reserved = new HashSet<string>(
            ["incident_report", "scene_photo", "suspect_portrait", "suspect_interview"],
            StringComparer.Ordinal);
        var semantic = SemanticArchetypeIds(draft);
        var eligible = All
            .Where(archetype => archetype.Rarity <= budget.MaxRarity && !reserved.Contains(archetype.Id))
            .ToList();
        if (eligible.Count == 0) return;

        var semanticCandidates = semantic
            .Select(id => eligible.FirstOrDefault(archetype => archetype.Id == id))
            .Where(archetype => archetype is not null)
            .Cast<EvidenceArchetype>()
            .DistinctBy(archetype => archetype.Id)
            .ToList();
        var remaining = eligible
            .Where(archetype => semanticCandidates.All(candidate => candidate.Id != archetype.Id))
            .OrderBy(archetype => archetype.Id, StringComparer.Ordinal)
            .ToList();
        var rotatedRemaining = remaining.Count == 0
            ? []
            : remaining.Skip(key % remaining.Count).Concat(remaining.Take(key % remaining.Count)).ToList();
        var rotated = semanticCandidates.Concat(rotatedRemaining).ToList();
        for (var index = 0; index < count; index++)
        {
            var usedFamilies = slots.Select(slot => slot.Family).ToHashSet(StringComparer.Ordinal);
            var candidate = usedFamilies.Count < budget.MinFamilies
                ? rotated.FirstOrDefault(archetype => !usedFamilies.Contains(archetype.Family))
                : null;
            candidate ??= rotated[index % rotated.Count];
            rotated.Remove(candidate);
            rotated.Add(candidate);

            var imagePurpose = candidate.Id == "surveillance_still" ? ImagePurposes.Surveillance : null;
            AddSlot(
                slots,
                usedIds,
                $"asset.{candidate.Id}",
                candidate,
                evidenceRole,
                imagePurpose: imagePurpose,
                title: Localized(
                    draft,
                    $"{candidate.DisplayName} {index + 1}",
                    $"Registro de evidência {index + 1}",
                    $"Registro de evidencia {index + 1}",
                    $"Pièce du dossier {index + 1}"));
        }
    }

    private static void AddSlot(
        ICollection<EvidenceDossierSlot> slots,
        ISet<string> usedIds,
        string preferredId,
        EvidenceArchetype archetype,
        string evidenceRole,
        string? subjectSuspectId = null,
        string? imagePurpose = null,
        string? title = null)
    {
        var id = preferredId;
        for (var suffix = 2; !usedIds.Add(id); suffix++)
            id = $"{preferredId}_{suffix}";
        slots.Add(new EvidenceDossierSlot(id, archetype, evidenceRole, subjectSuspectId, imagePurpose, title));
    }

    private static List<(string Id, string Name)> GetSuspects(CaseDraft draft)
    {
        var names = draft.SuspectFull
            .Where(suspect => !string.IsNullOrWhiteSpace(suspect.Id))
            .ToDictionary(suspect => suspect.Id, suspect => suspect.Name, StringComparer.Ordinal);
        return draft.SuspectStubs
            .Select(suspect => (suspect.Id, suspect.Name))
            .Concat(draft.SuspectFull.Select(suspect => (suspect.Id, suspect.Name)))
            .Where(suspect => !string.IsNullOrWhiteSpace(suspect.Id))
            .GroupBy(suspect => suspect.Id, StringComparer.Ordinal)
            .Select(group =>
            {
                var id = group.Key;
                var name = names.GetValueOrDefault(id)
                           ?? group.Select(item => item.Name).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                           ?? id;
                return (id, name);
            })
            .OrderBy(suspect => suspect.id, StringComparer.Ordinal)
            .Select(suspect => (suspect.id, suspect.name))
            .ToList();
    }

    private static List<string> SemanticArchetypeIds(CaseDraft draft)
    {
        var discoveries = string.Join(" ",
                draft.Blueprint.ClueLadder.Select(clue => clue.Discovery)
                    .Concat(draft.Blueprint.RedHerrings.Select(redHerring => redHerring.Verification)))
            .ToLowerInvariant();
        var preferredIds = new List<string>();
        if (ContainsAny(discoveries, "whatsapp", "chat", "mensagem", "message", "correo", "courriel"))
            preferredIds.Add("chat_export");
        if (ContainsAny(discoveries, "e-mail", "email", "cabeçalho", "header"))
            preferredIds.Add("email_export");
        if (ContainsAny(discoveries, "banco", "bank", "pix", "ted", "transfer", "conta", "cuenta", "compte"))
            preferredIds.Add("bank_statement");
        if (ContainsAny(discoveries, "ligação", "telefone", "call", "chamada", "appel"))
            preferredIds.Add("call_log");
        if (ContainsAny(discoveries, "crachá", "acesso", "login", "logon", "porta", "badge", "ponto", "presença", "attendance", "catraca"))
            preferredIds.Add("access_log");
        if (ContainsAny(discoveries, "camera", "câmera", "cctv", "nvr", "surveillance", "sensor"))
            preferredIds.Add("sensor_log");
        if (ContainsAny(discoveries, "vehicle", "veículo", "plate", "placa", "lpr", "transit", "route"))
            preferredIds.Add("gps_track");
        if (ContainsAny(discoveries, "recibo", "nota fiscal", "invoice", "receipt", "ticket"))
            preferredIds.Add("receipt");
        if (ContainsAny(discoveries, "agenda", "calendário", "calendar", "rendez-vous"))
            preferredIds.Add("calendar");
        if (ContainsAny(discoveries, "cftv", "cctv", "camera", "câmera", "video", "vídeo"))
            preferredIds.Add("surveillance_still");
        return preferredIds.Distinct(StringComparer.Ordinal).ToList();
    }

    private static EvidenceArchetype Required(string id) =>
        Find(id) ?? throw new InvalidOperationException($"Missing required evidence archetype '{id}'.");

    private static int Pick(int minimum, int maximum, int key)
    {
        if (maximum <= minimum) return minimum;
        return minimum + Math.Abs(key % (maximum - minimum + 1));
    }

    private static bool ContainsAny(string text, params string[] values) =>
        values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));

    private static int PositiveKey(CaseDraft draft)
    {
        var key = draft.Request.Seed ?? draft.CaseId.Aggregate(17, (value, c) => unchecked(value * 31 + c));
        return key == int.MinValue ? int.MaxValue : Math.Abs(key);
    }

    private static string IdSuffix(string id)
    {
        var suffix = id.Contains('.') ? id[(id.LastIndexOf('.') + 1)..] : id;
        var chars = suffix.ToLowerInvariant().Select(character =>
            char.IsAsciiLetterOrDigit(character) ? character : '_').ToArray();
        return new string(chars).Trim('_');
    }

    private static string Localized(
        CaseDraft draft,
        string english,
        string portuguese,
        string spanish,
        string french) =>
        draft.Request.Language?.ToLowerInvariant() switch
        {
            "pt-br" => portuguese,
            "es-es" => spanish,
            "fr-fr" => french,
            _ => english
        };
}
