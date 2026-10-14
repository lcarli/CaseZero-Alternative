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

    private static readonly string Schema = MicrotaskSchemas.PlotOutline;

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
        var output = await TaskRunner.RunStructuredAsync<Output>(_llm, _logger, "PlotOutline", system, user, Schema, ct,
            validate: o =>
            {
                MicrotaskValidation.Unique(o.Suspects.Select(s => s.Id), "suspects");
                MicrotaskValidation.Unique(o.Suspects.Select(s => s.Name), "suspect names");
                TaskContractException.Require(o.Suspects.Count(s => s.IsCulprit) == 1 &&
                    o.Suspects.Any(s => s.Id == o.CulpritId && s.IsCulprit),
                    "culpritId must identify the single suspect marked isCulprit.");
                MicrotaskValidation.Equal(o.Metadata.Difficulty, r.Difficulty ?? "Detective", "metadata.difficulty");
                MicrotaskValidation.Equal(o.Metadata.RequiredRank, r.RequiredRank ?? r.Difficulty ?? "Detective", "metadata.requiredRank");
                TaskContractException.Require(DateTimeOffset.Parse(o.Metadata.OpenedAt) > DateTimeOffset.Parse(o.Metadata.IncidentDate),
                    "metadata.openedAt must be later than incidentDate.");
            });

        // Coerce inputs that the operator pinned.
        if (!string.IsNullOrEmpty(r.Title)) output.Metadata.Title = r.Title!;
        if (!string.IsNullOrEmpty(r.Location)) output.Metadata.Location = r.Location!;
        if (!string.IsNullOrEmpty(r.Difficulty)) output.Metadata.Difficulty = r.Difficulty!;
        if (!string.IsNullOrEmpty(r.RequiredRank)) output.Metadata.RequiredRank = r.RequiredRank!;

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

    private static readonly string Schema = MicrotaskSchemas.SuspectCard;

    public async Task<PlotSuspect> RunAsync(CaseDraft draft, SuspectStub stub, CancellationToken ct)
    {
        var system = $@"You are writing the **full profile** of ONE suspect in a detective case.
Write recorded background and attributed statements, not the generator's private verdict.
Do not assert that a suspect is innocent/guilty, fabricate verification, or make motives secretly omniscient.

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
- support: exact excerpts for the planned source references.
{CaseBibleAuthorship.Instructions}";

        var context = draft.Bible is null ? draft.ToSummaryJson() : CaseBibleAuthorship.Context(draft, stub.Id);
        var verified = draft.Bible is not null && CaseBibleAuthorship.AlibiVerifiedAtOpening(draft, stub.Id);
        var user = $@"AUTHOR'S READ-ONLY CONTEXT:
{context}

SUSPECT TO DESIGN:
id={stub.Id}
name={stub.Name}
alibiVerified={verified.ToString().ToLowerInvariant()}

Emit JSON only.";

        return await TaskRunner.RunStructuredAsync<PlotSuspect>(_llm, _logger, $"SuspectCard:{stub.Id}", system, user, Schema, ct,
            validate: s =>
            {
                MicrotaskValidation.Equal(s.Id, stub.Id, "suspect.id");
                MicrotaskValidation.Equal(s.Name, stub.Name, "suspect.name");
                if (draft.Bible is not null)
                {
                    TaskContractException.Require(s.AlibiVerified == verified, "Alibi verification must follow independent canonical support.");
                    CaseBibleAuthorship.Source(draft, stub.Id, s.Support, CaseBibleAuthorship.Text(s));
                }
            });
    }
}
