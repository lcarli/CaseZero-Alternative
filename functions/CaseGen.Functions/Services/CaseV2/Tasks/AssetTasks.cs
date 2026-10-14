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

    private static readonly string Schema = MicrotaskSchemas.AssetPlan;

    public async Task RunAsync(CaseDraft draft, CancellationToken ct)
    {
        var system = @"You are planning the **initial evidence assets** a detective collects on day one of an investigation.
List 4-10 distinct assets. Each has:
- id (pattern `asset.<snake>`)
- type — one of these EXACT values (do NOT use anything else, especially not `video` or `image`):
    * `photo`    — a single still image of a scene / object / location
    * `pdf`      — a PDF report or evidence document (will be rendered with QuestPDF)
    * `document` — same renderer as `pdf`; includes written transcripts of interviews / calls
    * `digital`  — a digital-forensics export (call logs, POS data, sensor log, file listing). Will be rendered as a PDF report.
- a short title
- a ONE-LINE role explaining how it fits the case
- `visibility` = `initial` (all assets here are initial; result PDFs from forensics come later).

Do NOT write descriptions or metadata — that comes in a later step. Pick assets that make sense given the case draft.
Audio and video files are not supported. Plan any speech evidence as a document transcript, never as an audio asset or placeholder.";
        var user = $@"CASE DRAFT (read-only):
{draft.ToSummaryJson()}

Emit JSON only.";
        var o = await TaskRunner.RunStructuredAsync<Output>(_llm, _logger, "AssetPlan", system, user, Schema, ct,
            validate: output => MicrotaskValidation.Unique(output.Assets.Select(a => a.Id), "assets"));
        draft.AssetStubs = o.Assets;
    }
}

/// <summary>Stage 4 — Asset Card. One call per asset.</summary>
public class AssetCardTask
{
    private readonly ILLMProvider _llm;
    private readonly ILogger _logger;
    public AssetCardTask(ILLMProvider llm, ILogger logger) { _llm = llm; _logger = logger; }

    private static readonly string Schema = MicrotaskSchemas.AssetCard;

    public async Task<EvidenceAsset> RunAsync(CaseDraft draft, AssetStub stub, CancellationToken ct)
    {
        var (bodyGuidance, mustHaveBodyDoc) = stub.Type switch
        {
            "pdf" or "document" =>
                ("Set `bodyDoc` to a structured EvidenceDocument (see schema in this prompt). Pick a `layout` that fits — `PoliceReport`, `InterviewTranscript`, `WitnessStatement`, `Memo`, `ForensicReport`, `EvidenceLog` or `GeneralReport`. Speech evidence requires `InterviewTranscript` with transcript entries; it is a written document, not a playable recording. Use `narrative` for prose, `transcript` for Q/A interrogations, `keyValue` for header-style facts (date, location, officer), `table` for any tabulated data, `callout` for highlighted notes. Anchor every date/time to the case timeline (incidentDate / openedAt).", true),
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

Produce:
- `description`: 1-3 sentence summary visible in the case-file listing.
- `category`: short label (Document / Digital / Physical / Biological / Communication).
- `visibility`: echo `{stub.Visibility}` from the source plan.
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

Keep continuity with the authorized source facts and time precision. Use only supplied identities.
{CaseBibleAuthorship.Instructions}";

        var context = draft.Bible is null ? draft.ToSummaryJson() : CaseBibleAuthorship.Context(draft, stub.Id);
        var user = $@"AUTHOR'S READ-ONLY CONTEXT:
{context}

ASSET TO DESIGN:
id={stub.Id}
type={stub.Type}
title={stub.Title}
visibility={stub.Visibility}

Emit JSON only.";
        return await TaskRunner.RunStructuredAsync<EvidenceAsset>(_llm, _logger, $"AssetCard:{stub.Id}", system, user, Schema, ct,
            validate: a =>
            {
                MicrotaskValidation.Equal(a.Id, stub.Id, "asset.id");
                MicrotaskValidation.Equal(a.Type, stub.Type, "asset.type");
                MicrotaskValidation.Equal(a.Title, stub.Title, "asset.title");
                MicrotaskValidation.Equal(a.Visibility, stub.Visibility, "asset.visibility");
                TaskContractException.Require(!mustHaveBodyDoc || a.BodyDoc is not null,
                    "This asset type requires bodyDoc; markdown is not a fallback for an initial document.");
                MicrotaskValidation.Asset(a);
                CaseBibleAuthorship.Asset(draft, a);
            });
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

    private static readonly string Schema = MicrotaskSchemas.Timeline;

    public async Task RunAsync(CaseDraft draft, CancellationToken ct)
    {
        if (draft.Bible is not null)
        {
            ct.ThrowIfCancellationRequested();
            draft.Timeline = CaseBibleAuthorship.Timeline(draft);
            draft.TemporalEvents = new();
            return;
        }
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
        var o = await TaskRunner.RunStructuredAsync<Output>(_llm, _logger, "Timeline", system, user, Schema, ct,
            validate: output =>
            {
                MicrotaskValidation.Unique(output.TemporalEvents.Select(t => t.Id), "temporalEvents");
                var assets = MicrotaskValidation.AssetIds(draft);
                foreach (var item in output.TemporalEvents)
                {
                    var payload = item.Payload!;
                    TaskContractException.Require(payload.EmailId is null || payload.EmailId == "email.briefing",
                        "temporalEvents may reference only an email already defined in the draft.");
                    TaskContractException.Require(payload.AssetId is null || assets.Contains(payload.AssetId),
                        $"temporalEvent references unknown asset '{payload.AssetId}'.");
                    TaskContractException.Require(payload.EmailId is not null || payload.AssetId is not null ||
                        !string.IsNullOrWhiteSpace(payload.Message), "temporalEvent payload must contain content.");
                    TaskContractException.Require(item.Type != "email" || payload.EmailId is not null,
                        "An email temporalEvent requires payload.emailId.");
                }
            });
        draft.Timeline = o.Timeline;
        draft.TemporalEvents = o.TemporalEvents;
    }
}
