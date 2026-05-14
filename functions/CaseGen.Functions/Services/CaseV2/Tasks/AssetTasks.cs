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
              "type":{"type":"string","enum":["photo","pdf","audio","video","document","image","digital"]},
              "visibility":{"type":"string","enum":["initial","hidden"]}
            }}}
      }
    }
    """;

    public async Task RunAsync(CaseDraft draft, CancellationToken ct)
    {
        var system = @"You are planning the **initial evidence assets** a detective collects on day one of an investigation.
List 4-10 distinct assets, each with id (pattern `asset.<snake>`), type (one of photo|pdf|audio|video|document|image|digital),
a short title, a ONE-LINE role explaining how it fits the case, and `visibility` = `initial` (all assets here are initial; result PDFs from forensics come later).
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
        "visibility":{"type":"string","enum":["initial","hidden"]}
      }
    }
    """;

    public async Task<EvidenceAsset> RunAsync(CaseDraft draft, AssetStub stub, CancellationToken ct)
    {
        var system = @"You are writing the **detail card** for ONE evidence asset.
Echo the id and type exactly. Produce: a 1-3 sentence description, a short category label (Document / Digital / Physical / Biological),
and visibility (`initial` for this stage).";
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
        "timeline":{"type":"array","minItems":2,"maxItems":8},
        "temporalEvents":{"type":"array","minItems":0,"maxItems":3,
          "items":{"type":"object","required":["id","triggerAtMinutes","type"],
            "properties":{"id":{"type":"string","pattern":"^tevt\\.[a-z0-9_]+$"}}}}
      }
    }
    """;

    public async Task RunAsync(CaseDraft draft, CancellationToken ct)
    {
        var system = @"You are writing the **narrative timeline** and optional **temporalEvents** for an in-progress case.
The timeline is the verified chain of events visible to the player; it spans from the incident to the moment the chief opens the case.
`temporalEvents` are scripted memos/witness recall that fire by game time (minutes since case opening, range 5-120).
Reference asset/email IDs only if they exist in the draft.";
        var user = $@"CASE DRAFT (read-only):
{draft.ToSummaryJson()}

Emit JSON only.";
        var o = await TaskRunner.RunStructuredAsync<Output>(_llm, _logger, "Timeline", system, user, Schema, ct);
        draft.Timeline = o.Timeline;
        draft.TemporalEvents = o.TemporalEvents;
    }
}
