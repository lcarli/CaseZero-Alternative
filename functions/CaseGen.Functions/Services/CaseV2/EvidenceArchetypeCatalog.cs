namespace CaseGen.Functions.Services.CaseV2;

public sealed record EvidenceArchetype(
    string Id,
    string Family,
    string Type,
    string Layout,
    string DisplayName,
    string Purpose,
    int Rarity);

public sealed record EvidencePortfolioBudget(
    string Difficulty,
    int MinAssets,
    int MaxAssets,
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

    public static IReadOnlyList<EvidenceArchetype> BuildPortfolio(CaseDraft draft)
    {
        var key = PositiveKey(draft);
        var budget = GetBudget(draft);
        var targetCount = ResolveTargetCount(draft, budget, key);
        var selected = new List<EvidenceArchetype>();
        var blueprintNeedsDigital = draft.Blueprint.ClueLadder.Any(c =>
            c.SourceType is "digital" or "forensic");

        AddFromFamily(selected, "testimonial", key, budget.MaxRarity);
        AddFromFamily(selected, "official", key / 3, budget.MaxRarity);
        AddSemanticArchetypes(selected, draft, targetCount, budget.MaxRarity);
        if (blueprintNeedsDigital && selected.Count < targetCount)
            AddFromFamily(selected, "digital", key / 5, budget.MaxRarity);

        var allFamilies = new[] { "journalistic", "personal", "visual", "digital" };
        var familyRotation = allFamilies
            .Skip(key % allFamilies.Length)
            .Concat(allFamilies.Take(key % allFamilies.Length))
            .Where(f => !blueprintNeedsDigital || f != "digital")
            .ToList();

        foreach (var family in familyRotation)
        {
            if (selected.Count >= targetCount) break;
            if (selected.Select(a => a.Family).Distinct(StringComparer.Ordinal).Count() >= budget.MinFamilies) break;
            AddFromFamily(selected, family, key / (selected.Count + 2), budget.MaxRarity);
        }

        var eligible = All.Where(a => a.Rarity <= budget.MaxRarity).ToList();
        var cursor = 0;
        while (selected.Count < targetCount && cursor < eligible.Count * 3)
        {
            var candidate = eligible[(key + cursor * 7) % eligible.Count];
            cursor++;
            if (selected.Any(a => a.Id == candidate.Id)) continue;

            var hasUniqueLayoutOption = eligible.Any(a =>
                selected.All(s => s.Id != a.Id && s.Layout != a.Layout));
            if (hasUniqueLayoutOption && selected.Any(a => a.Layout == candidate.Layout)) continue;
            selected.Add(candidate);
        }

        foreach (var candidate in eligible)
        {
            if (selected.Count >= targetCount) break;
            if (selected.Any(item => item.Id == candidate.Id)) continue;
            selected.Add(candidate);
        }

        return selected
            .GroupBy(a => a.Id, StringComparer.Ordinal)
            .Select(g => g.First())
            .Take(targetCount)
            .ToList();
    }

    public static EvidencePortfolioBudget GetBudget(CaseDraft draft)
    {
        var profile = DifficultyProfileCatalog.Get(draft);
        return new(profile.Name, profile.MinAssets, profile.MaxAssets, profile.MinFamilies, profile.MaxRarity);
    }

    public static EvidenceArchetype? Find(string id) =>
        All.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.Ordinal));

    private static int ResolveTargetCount(CaseDraft draft, EvidencePortfolioBudget budget, int key)
    {
        var cluePressure = Math.Max(0, draft.Blueprint.ClueLadder.Count - 4) / 2;
        var minimumForClues = Math.Min(budget.MaxAssets, budget.MinAssets + cluePressure);
        var variableRange = budget.MaxAssets - minimumForClues + 1;
        return minimumForClues + ((key + draft.Blueprint.RedHerrings.Count * 3) % Math.Max(1, variableRange));
    }

    private static void AddFromFamily(List<EvidenceArchetype> selected, string family, int key, int maxRarity)
    {
        var candidates = All.Where(a => a.Family == family && a.Rarity <= maxRarity).ToList();
        if (candidates.Count == 0) return;

        var weighted = candidates
            .SelectMany(a => Enumerable.Repeat(a, Math.Max(1, 4 - a.Rarity)))
            .ToList();
        var pick = weighted[key % weighted.Count];

        if (selected.Any(a => a.Layout == pick.Layout))
        {
            pick = candidates.FirstOrDefault(a => selected.All(s => s.Layout != a.Layout)) ?? pick;
        }
        if (selected.All(a => a.Id != pick.Id)) selected.Add(pick);
    }

    private static void AddSemanticArchetypes(
        List<EvidenceArchetype> selected,
        CaseDraft draft,
        int targetCount,
        int maxRarity)
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
        if (ContainsAny(discoveries, "recibo", "nota fiscal", "invoice", "receipt", "ticket"))
            preferredIds.Add("receipt");
        if (ContainsAny(discoveries, "agenda", "calendário", "calendar", "rendez-vous"))
            preferredIds.Add("calendar");
        if (ContainsAny(discoveries, "cftv", "cctv", "camera", "câmera", "video", "vídeo"))
            preferredIds.Add("surveillance_still");
        if (ContainsAny(discoveries, "erp", "auditoria", "audit", "vpn", "workstation", "estação"))
            preferredIds.Add("access_log");

        foreach (var id in preferredIds.Distinct(StringComparer.Ordinal))
        {
            if (selected.Count >= targetCount) break;
            var archetype = Find(id);
            if (archetype is null || archetype.Rarity > maxRarity || selected.Any(item => item.Id == id))
                continue;
            selected.Add(archetype);
        }
    }

    private static bool ContainsAny(string text, params string[] values) =>
        values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));

    private static int PositiveKey(CaseDraft draft)
    {
        var key = draft.Request.Seed ?? draft.CaseId.Aggregate(17, (value, c) => unchecked(value * 31 + c));
        return key == int.MinValue ? int.MaxValue : Math.Abs(key);
    }
}
