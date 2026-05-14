using System.Text.Json.Serialization;
using CaseGen.Functions.Models.CaseV2;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.CaseV2.Tasks;

/// <summary>
/// Stage 1 — Plot Outline. Tiny output: metadata + victim + suspect stubs (id+name+role) + culprit pick.
/// Each suspect is one-line; full profiles come in <see cref="SuspectCardTask"/>.
/// </summary>
public class PlotOutlineTask
{
    private readonly ILLMProvider _llm;
    private readonly ILogger _logger;

    public PlotOutlineTask(ILLMProvider llm, ILogger logger) { _llm = llm; _logger = logger; }

    private class Output
    {
        [JsonPropertyName("metadata")] public PlotMetadata Metadata { get; set; } = new();
        [JsonPropertyName("suspects")] public List<SuspectStub> Suspects { get; set; } = new();
        [JsonPropertyName("culpritId")] public string CulpritId { get; set; } = string.Empty;
    }

    private const string Schema = """
    {
      "type":"object","required":["metadata","suspects","culpritId"],
      "properties":{
        "metadata":{"type":"object","required":["title","description","location","incidentDate","openedAt","difficulty","requiredRank","category","briefing","tags"]},
        "suspects":{"type":"array","minItems":3,"maxItems":6,
          "items":{"type":"object","required":["id","name","role","isCulprit"],
            "properties":{
              "id":{"type":"string","pattern":"^suspect\\.[a-z0-9_]+$"},
              "name":{"type":"string"},
              "role":{"type":"string"},
              "isCulprit":{"type":"boolean"}}}},
        "culpritId":{"type":"string","pattern":"^suspect\\.[a-z0-9_]+$"}
      }
    }
    """;

    public async Task RunAsync(CaseDraft draft, CancellationToken ct)
    {
        var r = draft.Request;
        var system = @"You are designing the **outline** of a single self-contained detective case.
Pick the title, location, incident date, victim, category and 3-6 candidate suspects (one MUST be the culprit).
For each suspect emit ONLY: id (pattern `suspect.<snake>`), name, a ONE-LINE role/relationship hint, and `isCulprit`.
Do NOT write motives, alibis or backgrounds — those come in a later step.
`metadata.difficulty` and `metadata.requiredRank` MUST be in: Rookie | Detective | Detective2 | Sergeant | Lieutenant | Captain | Commander.
`incidentDate` and `openedAt` are ISO-8601; `openedAt` is later than `incidentDate`. Briefing in metadata is a 1-2 sentence pitch (the long email comes later).";
        var user = $@"caseId: {draft.CaseId}
language: {r.Language ?? "en-US"}
theme hint: {r.Theme ?? "(model picks)"}
title hint: {r.Title ?? "(model picks)"}
location hint: {r.Location ?? "(model picks)"}
difficulty: {r.Difficulty ?? "Detective"}
requiredRank: {r.RequiredRank ?? r.Difficulty ?? "Detective"}

Emit JSON only.";
        var output = await TaskRunner.RunStructuredAsync<Output>(_llm, _logger, "PlotOutline", system, user, Schema, ct);

        // Coerce inputs that the operator pinned.
        if (!string.IsNullOrEmpty(r.Title)) output.Metadata.Title = r.Title!;
        if (!string.IsNullOrEmpty(r.Location)) output.Metadata.Location = r.Location!;
        if (!string.IsNullOrEmpty(r.Difficulty)) output.Metadata.Difficulty = r.Difficulty!;
        if (!string.IsNullOrEmpty(r.RequiredRank)) output.Metadata.RequiredRank = r.RequiredRank!;

        if (!output.Suspects.Any(s => s.Id == output.CulpritId) && output.Suspects.Count > 0)
            output.CulpritId = output.Suspects.FirstOrDefault(s => s.IsCulprit)?.Id ?? output.Suspects[0].Id;

        // Reconcile the IsCulprit flag.
        foreach (var s in output.Suspects) s.IsCulprit = s.Id == output.CulpritId;

        draft.Metadata = output.Metadata;
        draft.CulpritId = output.CulpritId;
        draft.SuspectStubs = output.Suspects;
    }
}

/// <summary>
/// Stage 2 — Suspect Card. One call per suspect. Receives the slim CaseDraft + this suspect's stub.
/// </summary>
public class SuspectCardTask
{
    private readonly ILLMProvider _llm;
    private readonly ILogger _logger;
    public SuspectCardTask(ILLMProvider llm, ILogger logger) { _llm = llm; _logger = logger; }

    private const string Schema = """
    {
      "type":"object",
      "required":["id","name","age","occupation","relationship","motive","alibi","alibiVerified","background"],
      "properties":{
        "id":{"type":"string","pattern":"^suspect\\.[a-z0-9_]+$"},
        "alibiVerified":{"type":"boolean"}
      }
    }
    """;

    public async Task<PlotSuspect> RunAsync(CaseDraft draft, SuspectStub stub, CancellationToken ct)
    {
        var system = $@"You are writing the **full profile** of ONE suspect in a detective case.
Stay consistent with the supplied case draft (victim, location, culprit). If this suspect IS the culprit, the alibi
should be partly verifiable but have a crack; if NOT the culprit, the alibi should be solid OR cleanly verifiable.

Output a single JSON object with EXACTLY this suspect's data:
- id (echo the supplied `{stub.Id}`)
- name (echo the supplied `{stub.Name}`)
- age (int)
- occupation (short)
- relationship (to the victim, short)
- motive (1-2 sentences)
- alibi (1-2 sentences)
- alibiVerified (boolean — true only if independently verified)
- background (markdown, 2-4 sentences)

The culprit for this case is `{draft.CulpritId}`. This suspect is `{(stub.IsCulprit ? "the culprit" : "not the culprit")}`.";

        var user = $@"CASE DRAFT (read-only):
{draft.ToSummaryJson()}

SUSPECT TO DESIGN:
id={stub.Id}
name={stub.Name}
role={stub.Role}
isCulprit={stub.IsCulprit}

Emit JSON only.";

        var s = await TaskRunner.RunStructuredAsync<PlotSuspect>(_llm, _logger, $"SuspectCard:{stub.Id}", system, user, Schema, ct);
        s.Id = stub.Id; // never let the model drift the id
        s.Name = string.IsNullOrEmpty(s.Name) ? stub.Name : s.Name;
        return s;
    }
}
