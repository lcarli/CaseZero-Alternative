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
        "assets":{"type":"array","minItems":4,"maxItems":10,
          "items":{"type":"object","required":["id","type","title","role","visibility"],
            "properties":{
              "id":{"type":"string","pattern":"^asset\\.[a-z0-9_]+$"},
              "type":{"type":"string","enum":["photo","pdf","document","audio","digital"]},
              "visibility":{"type":"string","enum":["initial","hidden"]}
            }}}
      }
    }
    """;

    public async Task RunAsync(CaseDraft draft, CancellationToken ct)
    {
        var system = @"You are planning the **initial evidence assets** a detective collects on day one of an investigation.
List 4-10 distinct assets. Each has:
- id (pattern `asset.<snake>`)
- type — one of these EXACT values (do NOT use anything else, especially not `video` or `image`):
    * `photo`    — a single still image of a scene / object / location
    * `pdf`      — a PDF report or evidence document (will be rendered with QuestPDF)
    * `document` — same renderer as `pdf` (alias for narrative purposes)
    * `audio`    — an audio recording (transcript stored in sidecar, audio TTS deferred)
    * `digital`  — a digital-forensics export (call logs, POS data, sensor log, file listing). Will be rendered as a PDF report.
- a short title
- a ONE-LINE role explaining how it fits the case
- `visibility` = `initial` (all assets here are initial; result PDFs from forensics come later).

Do NOT write descriptions or metadata — that comes in a later step. Pick assets that make sense given the case draft.";
        var user = $@"CASE DRAFT (read-only):
{draft.ToSummaryJson()}

Emit JSON only.";
        var o = await TaskRunner.RunStructuredAsync<Output>(_llm, _logger, "AssetPlan", system, user, Schema, ct);
        // Force all initial-stage assets to be visibility=initial
        foreach (var a in o.Assets) a.Visibility = "initial";
        draft.AssetStubs = o.Assets;
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
      "type":"object","required":["id","type","title","description","visibility","category"],
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

    public async Task<EvidenceAsset> RunAsync(CaseDraft draft, AssetStub stub, CancellationToken ct)
    {
        var (bodyGuidance, mustHaveBodyDoc) = stub.Type switch
        {
            "pdf" or "document" =>
                ("Set `bodyDoc` to a structured EvidenceDocument (see schema in this prompt). PICK a `layout` from the family below based on what the asset represents (be precise — the renderer styles each family differently):\n" +
                 "  · `PoliceReport`        — initial / supplemental police reports, first-responder narratives (brasão letterhead, §1/§2 numbered sections).\n" +
                 "  · `WitnessStatement`    — sworn statements taken from a witness (STATEMENT OF block, oath, double signature).\n" +
                 "  · `InterviewTranscript` — interviews / interrogations. Use a `transcript` section.\n" +
                 "  · `ForensicReport`      — lab examination reports (CCTV correlation, video enhancement, fingerprint analysis, ballistics, DNA, toxicology). Use sections Items Submitted / Methodology / Findings / Conclusions / Limitations.\n" +
                 "  · `MedicalReport`       — preliminary ME reports, autopsy summaries, urgent-care visit records. Sections History / External Examination / Findings / Cause / Disposition.\n" +
                 "  · `EvidenceLog`         — evidence log / chain of custody. ALWAYS include a `table` with columns Item, Description, Collected By, Time, Location, Container, Seal.\n" +
                 "  · `Memo`                — internal memorandums, records preservation holds, tasking memos, admin summaries, compliance briefs. Header[] MUST include TO, FROM, DATE, RE.\n" +
                 "  · `CustodyForm`         — key-control sign-out, cash count, drop-safe audit, case briefing. Header[] has the form fields. Include a signatures section.\n" +
                 "  · `GeneralReport`       — only if NONE of the above apply.\n" +
                 "Use `narrative` for prose, `transcript` for Q/A, `keyValue` for header-style facts (date, location, officer), `table` for tabulated data, `callout` for highlighted notes. Anchor every date/time to the case timeline (incidentDate / openedAt).", true),
            "photo" =>
                ("Set `body` to a vivid image prompt that an image model can render (lighting, composition, framing, mood, visible details). Leave `bodyDoc` null.", false),
            "audio" =>
                ("Set `body` to a transcript + descriptive note (stored as sidecar, TTS deferred). Leave `bodyDoc` null.", false),
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

        var user = $@"CASE DRAFT (read-only):
{draft.ToSummaryJson()}

ASSET TO DESIGN:
id={stub.Id}
type={stub.Type}
title={stub.Title}
role={stub.Role}

Emit JSON only.";
        var a = await TaskRunner.RunStructuredAsync<EvidenceAsset>(_llm, _logger, $"AssetCard:{stub.Id}", system, user, Schema, ct);
        a.Id = stub.Id;
        a.Type = stub.Type;
        a.Title = string.IsNullOrEmpty(a.Title) ? stub.Title : a.Title;
        a.Visibility = "initial";

        // Sanity check: PDF/document/digital MUST have bodyDoc with at least one section.
        if (mustHaveBodyDoc && (a.BodyDoc is null || a.BodyDoc.Sections.Count == 0))
        {
            _logger.LogWarning("AssetCard:{Id} expected a bodyDoc but the LLM omitted it — falling back to body markdown", stub.Id);
        }

        return a;
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
        var system = @"You are writing the **narrative timeline** and optional **temporalEvents** for an in-progress case.
The timeline is the verified chain of events visible to the player; it spans from the incident to the moment the chief opens the case.
`temporalEvents` are scripted memos/witness recall that fire by game time (minutes since case opening, range 5-120).

STRICT enums (use these EXACT strings — do NOT invent new ones):
- timeline[].source: investigation | witness | sensor | forensic
- timeline[].importance: low | medium | high | critical
- temporalEvents[].type: memo | witness | alert | email

Reference asset/email IDs only if they exist in the draft.";
        var user = $@"CASE DRAFT (read-only):
{draft.ToSummaryJson()}

Emit JSON only.";
        var o = await TaskRunner.RunStructuredAsync<Output>(_llm, _logger, "Timeline", system, user, Schema, ct);
        draft.Timeline = o.Timeline;
        draft.TemporalEvents = o.TemporalEvents;
    }
}
