using System.Text.Json.Serialization;
using CaseGen.Functions.Models.CaseV2;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.CaseV2.Tasks;

/// <summary>Stage 3 — Asset Plan. Tiny output: list of asset stubs (id+type+title+role+visibility).</summary>
public class AssetPlanTask
{
    private readonly ILLMProvider _llm;
    private readonly ILogger _logger;
    public AssetPlanTask(ILLMProvider llm, ILogger logger) { _llm = llm; _logger = logger; }

    private class Output
    {
        [JsonPropertyName("assets")] public List<AssetStub> Assets { get; set; } = new();
    }

    private const string Schema = """
    {
      "type":"object","required":["assets"],
      "properties":{
        "assets":{"type":"array","minItems":3,"maxItems":10,
          "items":{"type":"object","required":["archetypeId","id","type","title","role","visibility","layoutHint","supportsClueIds","forensicInputClueIds","introducesRedHerringSuspectIds","resolvesRedHerringSuspectIds","containedObjectIds"],
            "properties":{
              "archetypeId":{"type":"string","pattern":"^[a-z0-9_]+$"},
              "id":{"type":"string","pattern":"^asset\\.[a-z0-9_]+$"},
              "type":{"type":"string","enum":["photo","pdf","document","digital"]},
              "visibility":{"type":"string","enum":["initial","hidden"]},
              "layoutHint":{"type":"string","enum":["Photo","PoliceReport","WitnessStatement","InterviewTranscript","AudioTranscript","MedicalReport","EvidenceLog","Memo","CustodyForm","NewspaperClipping","PersonalLetter","Receipt","SearchWarrant","DispatchLog","CaseMap","Calendar","CallLog","PosExport","PhoneDump","SensorLog","BrowserHistory","BankStatement","GpsTrack","FileListing","ChatExport","EmailExport","AccessLog"]},
              "supportsClueIds":{"type":"array","minItems":1,"maxItems":3,
                "items":{"type":"string","pattern":"^clue\\.[a-z0-9_]+$"}},
              "forensicInputClueIds":{"type":"array","items":{"type":"string","pattern":"^clue\\.[a-z0-9_]+$"}},
              "introducesRedHerringSuspectIds":{"type":"array","items":{"type":"string","pattern":"^suspect\\.[a-z0-9_]+$"}},
              "resolvesRedHerringSuspectIds":{"type":"array","items":{"type":"string","pattern":"^suspect\\.[a-z0-9_]+$"}}
              ,"containedObjectIds":{"type":"array","minItems":1,"items":{"type":"string","pattern":"^(document|object|device|account|session|communication|transaction)\\.[a-z0-9_]+$"}}
            }}}
      }
    }
    """;

    public async Task RunAsync(CaseDraft draft, CancellationToken ct, string? repairGuidance = null)
    {
        var portfolio = EvidenceArchetypeCatalog.BuildPortfolio(draft);
        var budget = EvidenceArchetypeCatalog.GetBudget(draft);
        var portfolioJson = System.Text.Json.JsonSerializer.Serialize(portfolio.Select(a => new
        {
            archetypeId = a.Id,
            a.Family,
            a.Type,
            layoutHint = a.Layout,
            a.DisplayName,
            a.Purpose
        }));
        var system = $@"You are planning the **initial evidence assets** a detective collects on day one of an investigation.
Create exactly {portfolio.Count} distinct initial assets for this {budget.Difficulty} case. The allowed range for this difficulty is {budget.MinAssets}-{budget.MaxAssets}; the deterministic planner selected {portfolio.Count} for this seed and clue load.
Treat `investigationBlueprint` as canonical and cover every non-forensic clue in its `clueLadder`. Clues with `sourceType=forensic` belong to the later forensic outcome stage and MUST NOT be written into an initial asset. Each asset has:
- `archetypeId`: one of the recommended archetype IDs supplied below
- id (pattern `asset.<snake>`)
- type — one of these EXACT values (do NOT use anything else, especially not `video`, `image`, or `audio`):
    * `photo`    — a single still image of a scene / object / location
    * `pdf`      — a PDF report or evidence document (will be rendered with QuestPDF)
    * `document` — same renderer as `pdf` (alias for narrative purposes). USE THIS for transcribed audio (dispatch tapes, 911 calls, voicemails, surveillance audio) — set layout = `AudioTranscript` in the later step.
    * `digital`  — a digital-forensics export (call logs, POS data, sensor log, file listing). Will be rendered as a PDF report.
- a short title
- a ONE-LINE role explaining how it fits the case
- `layoutHint`: the exact document layout that the next agent MUST render
- `supportsClueIds`: 1-3 existing blueprint clue IDs that this asset can actually establish
- `forensicInputClueIds`: forensic clue IDs for which this asset contains the actual object to be scientifically analyzed. Assign every forensic clue to exactly one initial asset. The object must be present in this asset itself, not merely mentioned, linked, emailed, or described by another record.
- `containedObjectIds`: stable typed IDs for every actual object contained in the asset. Include the document/export/image itself; do not include objects merely mentioned or pictured.
- `introducesRedHerringSuspectIds`: decoy suspects for whom this asset establishes the concrete suspicious observation.
- `resolvesRedHerringSuspectIds`: decoy suspect IDs whose red-herring resolution this document can materially establish
- `visibility` = `initial` (all assets here are initial; result PDFs from forensics come later).

Do NOT plan raw audio or video media — there is no audio/video player. Anything an investigator would normally listen to (911 call, dispatch tape, voicemail, intercom audio) is delivered as a TRANSCRIBED document (`document` type, AudioTranscript layout).
Do NOT write descriptions or metadata — that comes in a later step. Use at least three different source types across the plan when the blueprint allows it. Never create an asset whose only purpose is exposition; every asset must establish, challenge, or resolve an investigative inference.

DOCUMENT PORTFOLIO:
- Include at least one human-source artifact: `InterviewTranscript`, `WitnessStatement`, or `AudioTranscript`. Use `InterviewTranscript` for suspect/witness questioning and actual interrogation records.
- Include at least one operational or institutional record: `CustodyForm`, `EvidenceLog`, `Memo`, `PoliceReport`, or `MedicalReport`.
- Include at least one digital record when plausible: `CallLog`, `PosExport`, `PhoneDump`, `SensorLog`, `BrowserHistory`, `BankStatement`, `GpsTrack`, `FileListing`, `ChatExport`, `EmailExport`, or `AccessLog`.
- Do not use the same layout more than twice. Avoid defaulting everything to PoliceReport, GeneralReport, or ForensicReport.
- Use EVERY recommended archetype exactly once. Do not add extras beyond the selected count. Adapt its title and content to the requested language, location, era, and case facts.
- Every blueprint red herring must be assigned to at least one asset through `resolvesRedHerringSuspectIds`; prefer consolidating several alibi checks into an operational log, timeline, dispatch record, or interview rather than adding one document per suspect.
- Difficulty controls volume and cognitive load, not document length alone. Rookie portfolios are smaller and use common, direct materials. Senior ranks may use more families, rarer formats, ambiguity, and cross-document reasoning.

TYPE/LAYOUT COMPATIBILITY:
- `photo` → `Photo`
- `pdf` or `document` → PoliceReport, WitnessStatement, InterviewTranscript, AudioTranscript, MedicalReport, EvidenceLog, Memo, CustodyForm, NewspaperClipping, PersonalLetter, Receipt, SearchWarrant, DispatchLog, CaseMap, Calendar
- `digital` → CallLog, PosExport, PhoneDump, SensorLog, BrowserHistory, BankStatement, GpsTrack, FileListing, ChatExport, EmailExport, AccessLog";
        var user = $@"CASE DRAFT (read-only):
{draft.ToSummaryJson()}

RECOMMENDED EVIDENCE ARCHETYPES:
{portfolioJson}
{(string.IsNullOrWhiteSpace(repairGuidance) ? string.Empty : $"\nREPAIR REQUIREMENTS FROM VALIDATION:\n{repairGuidance}\nRebuild the evidence ownership graph so every requirement is assigned to a suitable independent source.\n")}

Emit JSON only.";
        var o = await TaskRunner.RunStructuredAsync<Output>(_llm, _logger, "AssetPlan", system, user, Schema, ct);
        // Force all initial-stage assets to be visibility=initial
        foreach (var a in o.Assets) a.Visibility = "initial";
        NormalizePortfolio(o.Assets, portfolio);
        NormalizeDeclaredOwnership(draft, o.Assets);
        draft.AssetStubs = o.Assets;
    }

    public static IReadOnlyList<string> BuildPreferredLayoutPortfolio(CaseDraft draft)
        => EvidenceArchetypeCatalog.BuildPortfolio(draft).Select(a => a.Layout).ToList();

    private void NormalizePortfolio(
        List<AssetStub> assets,
        IReadOnlyList<EvidenceArchetype> portfolio)
    {
        var allowedArchetypes = portfolio.ToDictionary(a => a.Id, StringComparer.Ordinal);
        var canonicalIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var asset in assets)
        {
            if (!allowedArchetypes.TryGetValue(asset.ArchetypeId, out var archetype))
            {
                archetype = portfolio.FirstOrDefault(a => a.Layout == asset.LayoutHint);
                if (archetype is not null) asset.ArchetypeId = archetype.Id;
            }

            if (archetype is null) continue;
            asset.ArchetypeId = archetype.Id;
            asset.Type = archetype.Type;
            asset.LayoutHint = archetype.Layout;
            asset.Id = BuildCanonicalAssetId(archetype.Id, canonicalIds);
        }

    }

    private static string BuildCanonicalAssetId(string archetypeId, HashSet<string> usedIds)
    {
        var preferred = $"asset.{archetypeId}";
        if (usedIds.Add(preferred)) return preferred;

        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{preferred}_{suffix}";
            if (usedIds.Add(candidate)) return candidate;
        }
    }

    private static void NormalizeDeclaredOwnership(CaseDraft draft, List<AssetStub> assets)
    {
        var validInitialClueIds = draft.Blueprint.ClueLadder
            .Where(clue => !string.Equals(clue.SourceType, "forensic", StringComparison.OrdinalIgnoreCase))
            .Select(clue => clue.Id)
            .ToHashSet(StringComparer.Ordinal);
        var validForensicClueIds = draft.Blueprint.ClueLadder
            .Where(clue => string.Equals(clue.SourceType, "forensic", StringComparison.OrdinalIgnoreCase))
            .Select(clue => clue.Id)
            .ToHashSet(StringComparer.Ordinal);
        var validDecoyIds = draft.Blueprint.RedHerrings
            .Select(redHerring => redHerring.SuspectId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var asset in assets)
        {
            asset.SupportsClueIds = asset.SupportsClueIds
                .Where(validInitialClueIds.Contains)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            asset.ForensicInputClueIds = asset.ForensicInputClueIds
                .Where(validForensicClueIds.Contains)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            asset.IntroducesRedHerringSuspectIds = asset.IntroducesRedHerringSuspectIds
                .Where(validDecoyIds.Contains)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            asset.ResolvesRedHerringSuspectIds = asset.ResolvesRedHerringSuspectIds
                .Where(validDecoyIds.Contains)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            asset.ContainedObjectIds = asset.ContainedObjectIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        AssignMissingClues(
            validInitialClueIds,
            clueId => assets.Where(asset => SupportsClueSource(
                asset,
                draft.Blueprint.ClueLadder.First(clue => clue.Id == clueId).SourceType)));
        AssignMissingClues(validForensicClueIds, _ => assets.Where(asset => asset.ContainedObjectIds.Count > 0));

        foreach (var redHerring in draft.Blueprint.RedHerrings)
        {
            if (!assets.Any(asset => asset.IntroducesRedHerringSuspectIds.Contains(redHerring.SuspectId, StringComparer.Ordinal)))
                LeastLoaded(assets).IntroducesRedHerringSuspectIds.Add(redHerring.SuspectId);
            if (!assets.Any(asset => asset.ResolvesRedHerringSuspectIds.Contains(redHerring.SuspectId, StringComparer.Ordinal)))
                LeastLoaded(assets).ResolvesRedHerringSuspectIds.Add(redHerring.SuspectId);
        }

        void AssignMissingClues(
            IEnumerable<string> clueIds,
            Func<string, IEnumerable<AssetStub>> candidates)
        {
            foreach (var clueId in clueIds.Order(StringComparer.Ordinal))
            {
                var forensic = validForensicClueIds.Contains(clueId);
                if (assets.Any(asset => (forensic ? asset.ForensicInputClueIds : asset.SupportsClueIds)
                        .Contains(clueId, StringComparer.Ordinal)))
                    continue;
                var target = LeastLoaded(candidates(clueId).DefaultIfEmpty(LeastLoaded(assets)));
                (forensic ? target.ForensicInputClueIds : target.SupportsClueIds).Add(clueId);
            }
        }

        static AssetStub LeastLoaded(IEnumerable<AssetStub> candidates) =>
            candidates.OrderBy(asset => asset.SupportsClueIds.Count
                                        + asset.ForensicInputClueIds.Count
                                        + asset.IntroducesRedHerringSuspectIds.Count
                                        + asset.ResolvesRedHerringSuspectIds.Count)
                .ThenBy(asset => asset.Id, StringComparer.Ordinal)
                .First();

        static bool SupportsClueSource(AssetStub asset, string sourceType) =>
            sourceType.ToLowerInvariant() switch
            {
                "photo" => asset.Type.Equals("photo", StringComparison.OrdinalIgnoreCase),
                "digital" => asset.Type.Equals("digital", StringComparison.OrdinalIgnoreCase),
                "witness" => asset.LayoutHint.Equals("InterviewTranscript", StringComparison.OrdinalIgnoreCase)
                             || asset.Type is "document" or "pdf",
                _ => asset.Type is "document" or "pdf" or "digital"
            };
    }
}

/// <summary>Stage 4 — Asset Card. One call per asset.</summary>
public class AssetCardTask
{
    private readonly ILLMProvider _llm;
    private readonly ILogger _logger;
    public AssetCardTask(ILLMProvider llm, ILogger logger) { _llm = llm; _logger = logger; }

    private const string Schema = """
    {
      "type":"object","required":["id","type","title","description","visibility","category","body","bodyDoc"],
      "properties":{
        "id":{"type":"string","pattern":"^asset\\.[a-z0-9_]+$"},
        "type":{"type":"string"},
        "visibility":{"type":"string","enum":["initial","hidden"]},
        "body":{"type":"string"},
        "bodyDoc":{"type":["object","null"],
          "properties":{
            "layout":{"type":"string"},
            "title":{"type":"string"},
            "subtitle":{"type":["string","null"]},
            "classification":{"type":"string"},
            "header":{"type":"array","items":{"type":"object","required":["label","value"]}},
            "sections":{"type":"array","minItems":1,
              "items":{"type":"object","required":["kind"],
                "properties":{"kind":{"type":"string","enum":["narrative","table","keyValue","transcript","code","callout"]}}}},
            "signature":{"type":["object","null"]}
          }}
      }
    }
    """;

    public async Task<EvidenceAsset> RunAsync(
        CaseDraft draft,
        AssetStub stub,
        CancellationToken ct,
        string? repairGuidance = null)
    {
        var supportedClues = draft.Blueprint.ClueLadder
            .Where(clue => stub.SupportsClueIds.Contains(clue.Id, StringComparer.Ordinal))
            .ToList();
        var supportedCluesJson = System.Text.Json.JsonSerializer.Serialize(supportedClues);
        var resolvedRedHerrings = draft.Blueprint.RedHerrings
            .Where(redHerring => stub.ResolvesRedHerringSuspectIds.Contains(redHerring.SuspectId, StringComparer.Ordinal))
            .ToList();
        var resolvedRedHerringsJson = System.Text.Json.JsonSerializer.Serialize(resolvedRedHerrings);
        var introducedRedHerrings = draft.Blueprint.RedHerrings
            .Where(redHerring => stub.IntroducesRedHerringSuspectIds.Contains(redHerring.SuspectId, StringComparer.Ordinal))
            .ToList();
        var introducedRedHerringsJson = System.Text.Json.JsonSerializer.Serialize(introducedRedHerrings);
        var (bodyGuidance, mustHaveBodyDoc) = stub.Type switch
        {
            "pdf" or "document" =>
                ("Set `bodyDoc` to a structured EvidenceDocument (see schema in this prompt). PICK a `layout` from the family below based on what the asset represents (be precise — the renderer styles each family differently):\n" +
                 "  · `PoliceReport`        — initial / supplemental police reports, first-responder narratives (brasão letterhead, §1/§2 numbered sections).\n" +
                 "  · `WitnessStatement`    — sworn statements taken from a witness (STATEMENT OF block, oath, double signature).\n" +
                 "  · `InterviewTranscript` — interviews / interrogations conducted face-to-face by the police. Use a `transcript` section.\n" +
                 "  · `AudioTranscript`     — transcribed audio recordings (911 / dispatch tapes, voicemails, intercom audio, surveillance recordings). Header should include `Recording Ref`, `Duration`, `Recording Device`, `Transcribed by`. Body is a `transcript` section with {timestamp, speaker, text}.\n" +
                 "  · `ForensicReport`      — lab examination reports (CCTV correlation, video enhancement, fingerprint analysis, ballistics, DNA, toxicology). Use sections Items Submitted / Methodology / Findings / Conclusions / Limitations.\n" +
                 "  · `MedicalReport`       — preliminary ME reports, autopsy summaries, urgent-care visit records. Sections History / External Examination / Findings / Cause / Disposition.\n" +
                 "  · `EvidenceLog`         — evidence log / chain of custody. ALWAYS include a `table` with columns Item, Description, Collected By, Time, Location, Container, Seal.\n" +
                 "  · `Memo`                — internal memorandums, records preservation holds, tasking memos, admin summaries, compliance briefs. Header[] MUST include TO, FROM, DATE, RE.\n" +
                 "  · `CustodyForm`         — key-control sign-out, cash count, drop-safe audit, case briefing. Header[] has the form fields. Include a signatures section.\n" +
                 "  · `NewspaperClipping`    — article, obituary, or archived press clipping. `subtitle` is the publication masthead; header contains date/byline/edition. Use concise journalistic prose and distinguish reported claims from verified facts.\n" +
                 "  · `PersonalLetter`       — letter, anonymous tip, will extract, or private correspondence. Use first-person voice appropriate to the author and date it in header/subtitle.\n" +
                 "  · `Receipt`              — receipt, invoice, ticket, betting slip, or transaction stub. Prefer a compact table and exact amounts/times; do not add narrative filler.\n" +
                 "  · `SearchWarrant`        — warrant or return of service. Sections should cover probable cause, premises/person, items sought, authorization, and execution/return where applicable.\n" +
                 "  · `DispatchLog`          — CAD/dispatch event history. Use a timestamped table or code-style log showing caller information, unit assignment, status changes, and response timing.\n" +
                 "  · `CaseMap`              — floor plan, route map, access diagram, or scene sketch represented through labeled locations, distances, sightlines, and route notes in tables/keyValue sections.\n" +
                 "  · `Calendar`             — appointment book, shift roster, or calendar page. Use a chronological table with date/time, event, location, and annotations.\n" +
                 "  · `GeneralReport`       — only if NONE of the above apply.\n" +
                 "Use `narrative` for prose, `transcript` for Q/A or transcribed audio, `keyValue` for header-style facts (date, location, officer), `table` for tabulated data, `callout` for highlighted notes. Anchor every date/time to the case timeline (incidentDate / openedAt).", true),
            "photo" =>
                ("Set `body` to a vivid image prompt that an image model can render (lighting, composition, framing, mood, visible details). Leave `bodyDoc` null.", false),
            "digital" =>
                ("Set `bodyDoc` to a structured EvidenceDocument. CHOOSE one of these recognised digital layouts so a specialised template can enrich the output:\n" +
                 "  · `CallLog`         — telecom CDRs. Table columns: Timestamp, Direction, Other Party, Duration, Notes.\n" +
                 "  · `PosExport`       — point-of-sale exports. Table columns: Timestamp, Cashier, SKU/Item, Qty, Total.\n" +
                 "  · `PhoneDump`       — forensic phone extraction. Multiple sections: keyValue for device info (IMEI/SIM/Model), table for installed apps, transcript or table for messages.\n" +
                 "  · `SensorLog`       — IoT / door chime / alarm. Table columns: Timestamp, Sensor, Event, Value.\n" +
                 "  · `BrowserHistory`  — table columns: Timestamp, URL, Title, Duration.\n" +
                 "  · `BankStatement`   — table columns: Date, Description, Debit, Credit, Balance.\n" +
                 "  · `GpsTrack`        — table columns: Timestamp, Latitude, Longitude, Accuracy (m), Notes.\n" +
                 "  · `FileListing`     — table columns: Path, Size (bytes), Modified, SHA-256.\n" +
                 "  · `ChatExport`      — messaging app. Table or transcript with columns/fields: Timestamp, Sender, Message.\n" +
                 "  · `EmailExport`     — table columns: Timestamp, From, To, Subject, Snippet.\n" +
                 "  · `AccessLog`       — table columns: Timestamp, Source IP, Method, Path, Status.\n" +
                 "Use suspect names/identities ONLY when they exist in the draft. Anchor timestamps to the case timeline.", true),
            _ => ("Short note describing the asset.", false)
        };

        var system = $@"You are writing the **detail card** for ONE evidence asset.
Echo the supplied id and type exactly.
The planner selected layout `{stub.LayoutHint}`. Use that EXACT `bodyDoc.layout`; do not replace it with a generic report.

Produce:
- `description`: 1-3 sentence summary visible in the case-file listing.
- `category`: short label (Document / Digital / Physical / Biological / Communication).
- `visibility`: `initial`.
- {(mustHaveBodyDoc ? "`bodyDoc`: REQUIRED for this asset type — a structured EvidenceDocument (see below). Leave `body` as the empty string." : "`body`: required — see guidance below.")}

Body guidance: {bodyGuidance}

EvidenceDocument schema (when used):
  layout (PascalCase tag)
  title, subtitle, classification (defaults to ""CONFIDENTIAL · INTERNAL USE ONLY"")
  header[]: {{ label, value }}  ← case ref, exhibit ref, custodian, station, generated by/at
  sections[]: each is one of
    {{ kind:""narrative"", heading?, text }}       ← markdown paragraphs; bullets ""- ...""
    {{ kind:""table"", heading?, table:{{ columns:[..], rows:[[..],..], footnote? }} }}
    {{ kind:""keyValue"", heading?, items:[{{label,value}}] }}
    {{ kind:""transcript"", heading?, transcript:[{{ timestamp?, speaker?, text }}] }}
    {{ kind:""code"", heading?, code:{{ language?, content }} }}        ← raw log dumps
    {{ kind:""callout"", heading?, text }}        ← highlighted notes
  signature?: {{ name, title?, badge?, signedAt? }}

Keep continuity with the supplied case draft (suspects, motives, alibis, location, incidentDate, openedAt). Use suspect names ONLY when they exist in the draft.";
        system += @"

CLUE MATERIALIZATION CONTRACT:
- Every clue assigned to this asset must appear as a concrete, player-readable observation in `bodyDoc`/`body`, including its relevant names, values, timestamps, identifiers, or limitations.
- Put the observable `discovery` in the document. Do not copy the private `inference` as an authoritative conclusion.
- IDENTITY SHIELDING for non-Rookie initial evidence: a `role=identity` clue maps endpoint identifier A to one suspect but must not mention the criminal action. A `role=action` clue shows a different opaque identifier B acting, but must not add the suspect's identity or identifier A. Preserve only what `discovery` states and never expose the B→A link in initial evidence; the gated forensic result closes that correlation.
- Decisive clues need enough exact detail to distinguish the culprit from every decoy; supporting clues must independently corroborate rather than merely repeat another document.
- For every assigned red herring, include the concrete observable record that weakens or resolves it. Do not simply state that an alibi is verified.
- For every introduced red herring, include only the concrete suspicious observation. Never label it a decoy.
- Never label a section, person, or fact as a ""red herring"", ""decoy"", ""resolution"", or ""cleared suspect"". Present the underlying record neutrally and let the player draw the conclusion.
- The short `description` must accurately summarize the rendered content, but cannot replace it.";
        system += @"

FACT DISCIPLINE:
- Treat every canonical name, identifier, account, phone number, hostname, timestamp, amount, and location in the blueprint discoveries as immutable.
- Do not invent a second value for the same fact. If the blueprint does not provide an exact value, describe it without fabricating one.
- Do not cite an appendix, receipt, screenshot, CCTV clip, database export, message, attachment, or external report unless it is fully included inside this asset or exists as another supplied asset.
- Do not mention future investigative requests, lab validation, subpoenas, platform returns, or evidence the player does not receive.";

        var user = $@"CASE DRAFT (read-only):
{draft.ToSummaryJson()}

ASSET TO DESIGN:
archetypeId={stub.ArchetypeId}
id={stub.Id}
type={stub.Type}
title={stub.Title}
role={stub.Role}
layoutHint={stub.LayoutHint}
supportsClueIds={string.Join(", ", stub.SupportsClueIds)}
containedObjectIds={string.Join(", ", stub.ContainedObjectIds)}
introducesRedHerringSuspectIds={string.Join(", ", stub.IntroducesRedHerringSuspectIds)}
resolvesRedHerringSuspectIds={string.Join(", ", stub.ResolvesRedHerringSuspectIds)}

CLUES THIS ASSET MUST MATERIALIZE:
{supportedCluesJson}

RED-HERRING RESOLUTIONS THIS ASSET MUST MATERIALIZE:
{resolvedRedHerringsJson}

RED-HERRING SUSPICIONS THIS ASSET MUST MATERIALIZE:
{introducedRedHerringsJson}
{(string.IsNullOrWhiteSpace(repairGuidance) ? string.Empty : $"\nREPAIR REQUIREMENTS:\n{repairGuidance}\nCorrect only through player-readable observations. Do not state private inferences as established facts.\n")}

Emit JSON only.";
        var a = await TaskRunner.RunStructuredAsync<EvidenceAsset>(_llm, _logger, $"AssetCard:{stub.Id}", system, user, Schema, ct);
        a.Id = stub.Id;
        a.Type = stub.Type;
        a.Title = string.IsNullOrEmpty(a.Title) ? stub.Title : a.Title;
        a.Visibility = "initial";
        if (mustHaveBodyDoc && a.BodyDoc is not null && !string.IsNullOrWhiteSpace(stub.LayoutHint))
            a.BodyDoc.Layout = stub.LayoutHint;

        // Sanity check: PDF/document/digital MUST have bodyDoc with at least one section.
        if (mustHaveBodyDoc && (a.BodyDoc is null || a.BodyDoc.Sections.Count == 0))
        {
            _logger.LogWarning("AssetCard:{Id} expected a bodyDoc but the LLM omitted it — falling back to body markdown", stub.Id);
        }
        if (a.BodyDoc is not null)
            a.BodyDoc.Sections = a.BodyDoc.Sections.Where(IsSubstantiveSection).ToList();

        MaterializeCanonicalFacts(
            a,
            supportedClues,
            introducedRedHerrings,
            resolvedRedHerrings,
            draft.Request.Language);
        if (a.BodyDoc is not null && a.BodyDoc.Sections.Count == 0)
        {
            a.BodyDoc.Sections.Add(new EvidenceSection
            {
                Kind = "narrative",
                Text = string.IsNullOrWhiteSpace(a.Description) ? a.Title : a.Description
            });
        }
        return a;
    }

    private static bool IsSubstantiveSection(EvidenceSection section) =>
        section.Kind switch
        {
            "table" => section.Table?.Rows.Count > 0,
            "transcript" => section.Transcript?.Count > 0,
            "code" => !string.IsNullOrWhiteSpace(section.Code?.Content),
            "keyValue" => section.Items?.Any(item =>
                !string.IsNullOrWhiteSpace(item.Label) || !string.IsNullOrWhiteSpace(item.Value)) == true,
            _ => !string.IsNullOrWhiteSpace(section.Text)
        };

    private static void MaterializeCanonicalFacts(
        EvidenceAsset asset,
        IReadOnlyList<CanonicalClue> supportedClues,
        IReadOnlyList<CanonicalRedHerring> introducedRedHerrings,
        IReadOnlyList<CanonicalRedHerring> resolvedRedHerrings,
        string? language)
    {
        var clueDiscoveries = supportedClues
            .Select(clue => clue.Discovery)
            .Where(discovery => !string.IsNullOrWhiteSpace(discovery))
            .ToList();
        var suspicions = introducedRedHerrings
            .Select(redHerring => redHerring.Suspicion)
            .Where(suspicion => !string.IsNullOrWhiteSpace(suspicion))
            .ToList();
        var resolutions = resolvedRedHerrings
            .Select(redHerring => redHerring.Verification)
            .Where(verification => !string.IsNullOrWhiteSpace(verification))
            .ToList();
        if (clueDiscoveries.Count == 0 && suspicions.Count == 0 && resolutions.Count == 0) return;

        var (clueHeading, suspicionHeading, verificationHeading) = language?.ToLowerInvariant() switch
        {
            "pt-br" => ("Observações documentadas", "Elementos de suspeita", "Verificações complementares"),
            "es-es" => ("Observaciones documentadas", "Elementos de sospecha", "Verificaciones complementarias"),
            "fr-fr" => ("Observations documentées", "Éléments de soupçon", "Vérifications complémentaires"),
            _ => ("Documented observations", "Suspicion indicators", "Supporting checks")
        };

        if (asset.BodyDoc is not null)
        {
            if (clueDiscoveries.Count > 0)
                asset.BodyDoc.Sections.Add(new EvidenceSection
                {
                    Kind = "narrative",
                    Heading = clueHeading,
                    Text = string.Join("\n", clueDiscoveries.Select(discovery => $"- {discovery}"))
                });
            if (suspicions.Count > 0)
                asset.BodyDoc.Sections.Add(new EvidenceSection
                {
                    Kind = "narrative",
                    Heading = suspicionHeading,
                    Text = string.Join("\n", suspicions.Select(suspicion => $"- {suspicion}"))
                });
            if (resolutions.Count > 0)
                asset.BodyDoc.Sections.Add(new EvidenceSection
                {
                    Kind = "narrative",
                    Heading = verificationHeading,
                    Text = string.Join("\n", resolutions.Select(resolution => $"- {resolution}"))
                });
            return;
        }

        var additions = new List<string>();
        if (clueDiscoveries.Count > 0)
            additions.Add($"## {clueHeading}\n{string.Join("\n", clueDiscoveries.Select(discovery => $"- {discovery}"))}");
        if (suspicions.Count > 0)
            additions.Add($"## {suspicionHeading}\n{string.Join("\n", suspicions.Select(suspicion => $"- {suspicion}"))}");
        if (resolutions.Count > 0)
            additions.Add($"## {verificationHeading}\n{string.Join("\n", resolutions.Select(resolution => $"- {resolution}"))}");
        asset.Body = string.Join("\n\n", new[] { asset.Body ?? string.Empty }.Concat(additions)).Trim();
    }
}

/// <summary>Stage 5 — Timeline + temporalEvents.</summary>
public class TimelineTask
{
    private readonly ILLMProvider _llm;
    private readonly ILogger _logger;
    public TimelineTask(ILLMProvider llm, ILogger logger) { _llm = llm; _logger = logger; }

    private class Output
    {
        [JsonPropertyName("timeline")] public List<EvidenceTimelineEntry> Timeline { get; set; } = new();
        [JsonPropertyName("temporalEvents")] public List<EvidenceTemporalEvent> TemporalEvents { get; set; } = new();
    }

    private const string Schema = """
    {
      "type":"object","required":["timeline","temporalEvents"],
      "properties":{
        "timeline":{"type":"array","minItems":2,"maxItems":8,
          "items":{"type":"object","required":["time","event"],
            "properties":{
              "source":{"type":"string","enum":["investigation","witness","sensor","forensic"]},
              "importance":{"type":"string","enum":["low","medium","high","critical"]}}}},
        "temporalEvents":{"type":"array","minItems":0,"maxItems":3,
          "items":{"type":"object","required":["id","triggerAtMinutes","type"],
            "properties":{
              "id":{"type":"string","pattern":"^tevt\\.[a-z0-9_]+$"},
              "type":{"type":"string","enum":["memo","witness","alert","email"]}}}}
      }
    }
    """;

    public async Task RunAsync(CaseDraft draft, CancellationToken ct)
    {
        await Task.CompletedTask;
        draft.Timeline = PlayerTimelineCompiler.Compile(draft, _logger);
        // Temporal events remain empty until they can be compiled from explicit,
        // payload-bearing evidence specs. Empty scripted events are worse than none.
        draft.TemporalEvents = new();
    }
}
