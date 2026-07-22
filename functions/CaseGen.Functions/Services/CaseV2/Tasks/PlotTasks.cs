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
        [JsonPropertyName("blueprint")] public InvestigationBlueprint Blueprint { get; set; } = new();
    }

    private const string Schema = """
    {
      "type":"object","required":["metadata","suspects","culpritId","blueprint"],
      "properties":{
        "metadata":{"type":"object","required":["title","description","location","incidentDate","openedAt","difficulty","requiredRank","category","briefing","tags"]},
        "suspects":{"type":"array","minItems":3,"maxItems":6,
          "items":{"type":"object","required":["id","name","role","isCulprit"],
            "properties":{
              "id":{"type":"string","pattern":"^suspect\\.[a-z0-9_]+$"},
              "name":{"type":"string"},
              "role":{"type":"string"},
              "isCulprit":{"type":"boolean"}}}},
        "culpritId":{"type":"string","pattern":"^suspect\\.[a-z0-9_]+$"},
        "blueprint":{"type":"object","required":["crimeMechanism","culpritObjective","caseHook","locale","incidentSequence","clueLadder","redHerrings"],
          "properties":{
            "crimeMechanism":{"type":"string","minLength":20},
            "culpritObjective":{"type":"string","minLength":20},
            "caseHook":{"type":"string","minLength":20},
            "locale":{"type":"object","required":["timeZoneId","utcOffset","policeAgency","investigatorName","investigatorEmail"]},
            "incidentSequence":{"type":"array","minItems":3,"maxItems":7,
              "items":{"type":"object","required":["id","time","event","publicDescription","visibility","source","verified","importance","causalRole"],
                "properties":{
                  "id":{"type":"string","pattern":"^event\\.[a-z0-9_]+$"},
                  "publicDescription":{"type":["string","null"]},
                  "visibility":{"type":"string","enum":["private","public"]},
                  "source":{"type":"string","enum":["investigation","witness","sensor"]},
                  "verified":{"type":"boolean"},
                  "importance":{"type":"string","enum":["low","medium","high","critical"]}}}},
            "clueLadder":{"type":"array","minItems":4,"maxItems":8,
              "items":{"type":"object","required":["id","discovery","inference","supportsSuspectId","strength","sourceType","role"],
                "properties":{
                  "id":{"type":"string","pattern":"^clue\\.[a-z0-9_]+$"},
                  "supportsSuspectId":{"type":["string","null"],"pattern":"^suspect\\.[a-z0-9_]+$"},
                  "strength":{"type":"string","enum":["context","supporting","decisive"]},
                  "sourceType":{"type":"string","enum":["document","digital","photo","witness","forensic"]},
                  "role":{"type":"string","enum":["context","identity","action","benefit","alibi","forensicAttribution"]}}}},
            "redHerrings":{"type":"array","minItems":2,"maxItems":5,
              "items":{"type":"object","required":["suspectId","suspicion","verification","resolution"],
                "properties":{"suspectId":{"type":"string","pattern":"^suspect\\.[a-z0-9_]+$"}}}}
          }}
      }
    }
    """;

    public async Task RunAsync(CaseDraft draft, CancellationToken ct, string? repairGuidance = null)
    {
        var r = draft.Request;
        var profile = DifficultyProfileCatalog.Get(r.Difficulty ?? r.RequiredRank);
        var system = $@"You are the lead narrative architect for a grounded, self-contained detective case.
Pick the title, location, incident date, victim, category and 3-6 candidate suspects (one MUST be the culprit).
For this {profile.Name} case, create {profile.MinSuspects}-{profile.MaxSuspects} suspects.
For each suspect emit ONLY: id (pattern `suspect.<snake>`), name, a ONE-LINE role/relationship hint, and `isCulprit`.
Do NOT write motives, alibis or backgrounds — those come in a later step.
`metadata.difficulty` and `metadata.requiredRank` MUST be in: Rookie | Detective | Detective2 | Sergeant | Lieutenant | Captain | Commander.
`incidentDate` and `openedAt` are ISO-8601; `openedAt` is later than `incidentDate`. Briefing in metadata is a 1-2 sentence pitch (the long email comes later).

Also create a private `blueprint`, the single source of truth every later agent will follow:
- `crimeMechanism`: concrete means, access, opportunity, and concealment. Prefer ordinary, believable systems and human mistakes over cinematic twists.
- `culpritObjective`: a specific, proportional objective and why the incident happened now.
- `caseHook`: the unusual but plausible detail that makes this case memorable.
- `locale`: IANA timezone, correct UTC offset for the incident date, a plausible local police agency, and a localized investigator name/email.
- `incidentSequence`: 3-7 ISO-timestamped causal beats. Each beat has:
  - `event`: private canonical truth, which may describe the actual crime;
  - `visibility`: `private` or `public`;
  - `publicDescription`: only what the player can legitimately know at case opening, or null for private beats;
  - source, verification state, importance, and causal role.
  At least two beats must be public. A public description MUST NOT name the culprit, describe the hidden method, or assert facts not established by an initial clue.
- `metadata.incidentDate` is the best-supported estimated time of the critical incident, not the later discovery/report time. Never describe the victim as found at `incidentDate` unless discovery truly occurred then.
- `clueLadder`: 4-8 discoveries that progress from context → supporting → decisive. At least TWO independent clues must support the culprit; exactly ONE is decisive. The culprit-supporting clues must use distinct source types and distinct facts: one should establish access/opportunity and another should establish action, possession, communication, or benefit. A forensic clue may strengthen another clue but cannot merely revalidate the same document. A clue's discovery is the observable fact, while inference is what a careful detective may conclude.
  Set `role` to describe the clue's job: context, identity, action, benefit, alibi, or forensicAttribution.
  Every `discovery` must be one neutral observable sentence. Never write evaluative phrases such as ""decisive proof"", ""points to [suspect]"", ""confirms authorship"", ""clears"", or ""not the culprit""; those belong only in the private `inference`.
  A discovery must be independently verifiable from the rendered document: include the exact timestamp, identifier, phone/account suffix, username, amount, quoted phrase, or bounded observation needed by the player. Never write only ""the numbers match"", ""the record confirms"", or ""the same identifier appears""; put each raw value in its own source so the player can compare them.
- `redHerrings`: one per major decoy where possible. `suspicion` is the plausible concern; `verification` is the exact neutral player-visible record that tests it (include concrete times, values, participants, and limitations); `resolution` is the private inference explaining how that verification weakens the suspicion. Never put words such as ""clears"", ""decoy"", ""red herring"", ""innocent"", or ""not the culprit"" in `verification`, and never reference a record whose actual entry is not quoted in `verification`.
{(profile.AllEvidenceInitial
    ? "- Rookie: every clue must use an initial non-forensic source type."
    : "- Non-Rookie culprit chain MUST contain at least three distinct clues forming a three-hop join: (1) one initial `role=identity` clue mapping endpoint identifier A (device, workstation, badge, account owner) to the suspect without mentioning the crime; (2) one initial `role=action` clue showing a DIFFERENT opaque identifier B (session, token, file author tag, transaction trace) performing the relevant action, without naming the suspect or identifier A; and (3) the exactly-one decisive `role=forensicAttribution` clue with `sourceType=forensic` that objectively maps B to A. Initial evidence alone must not contain the B→A link. Never combine identity + criminal action + benefit in one initial observation.")}
{(profile.AllEvidenceInitial
    ? "- Rookie: every clue must use an initial non-forensic source type."
    : "- Non-Rookie: the exactly-one decisive culprit clue MUST use `sourceType=forensic` and describe an objective result obtainable by analyzing one planned initial asset. Initial clues may expose separate identifiers, actions, access, or discrepancies, but no single initial discovery may directly combine the culprit identity with the complete criminal act or financial benefit.")}

QUALITY RULES:
- Ground names, institutions, procedures, travel times, occupations, records, and communications in the requested location and language.
- Avoid serial-killer clichés, secret twins, genius masterminds, miraculous CCTV enhancement, perfect DNA, and coincidence-only solutions unless explicitly requested by the theme.
- Do not make the culprit obvious from motive alone. The player should need corroboration across different evidence sources.
- Scale complexity to difficulty: Rookie is direct and compact; higher ranks may add ambiguity and multi-step inference, never arbitrary obscurity.";
        var user = $@"caseId: {draft.CaseId}
language: {r.Language ?? "en-US"}
theme hint: {r.Theme ?? "(model picks)"}
title hint: {r.Title ?? "(model picks)"}
location hint: {r.Location ?? "(model picks)"}
difficulty: {r.Difficulty ?? "Detective"}
requiredRank: {r.RequiredRank ?? r.Difficulty ?? "Detective"}
{FormatRepairGuidance(repairGuidance)}

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
        if (output.Suspects.Count > profile.MaxSuspects)
        {
            output.Suspects = output.Suspects
                .OrderByDescending(suspect => suspect.Id == output.CulpritId)
                .Take(profile.MaxSuspects)
                .ToList();
        }
        var declaredDecoyIds = output.Blueprint.RedHerrings
            .Select(redHerring => redHerring.SuspectId)
            .Where(id => id != output.CulpritId)
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);
        var graphReadySuspects = output.Suspects
            .Where(suspect => suspect.Id == output.CulpritId || declaredDecoyIds.Contains(suspect.Id))
            .OrderByDescending(suspect => suspect.Id == output.CulpritId)
            .ThenBy(suspect => suspect.Id, StringComparer.Ordinal)
            .Take(profile.MaxSuspects)
            .ToList();
        if (graphReadySuspects.Count >= profile.MinSuspects)
            output.Suspects = graphReadySuspects;

        var retainedSuspectIds = output.Suspects.Select(suspect => suspect.Id).ToHashSet(StringComparer.Ordinal);
        output.Blueprint.RedHerrings = output.Blueprint.RedHerrings
            .Where(redHerring => retainedSuspectIds.Contains(redHerring.SuspectId))
            .GroupBy(redHerring => redHerring.SuspectId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
        foreach (var clue in output.Blueprint.ClueLadder.Where(clue =>
                     clue.SupportsSuspectId is not null
                     && !retainedSuspectIds.Contains(clue.SupportsSuspectId)))
        {
            clue.SupportsSuspectId = null;
        }
        NormalizeDifficultyClueDelivery(output.Blueprint, output.CulpritId, profile);

        draft.Metadata = output.Metadata;
        draft.CulpritId = output.CulpritId;
        draft.SuspectStubs = output.Suspects;
        draft.Blueprint = output.Blueprint;
    }

    private static void NormalizeDifficultyClueDelivery(
        InvestigationBlueprint blueprint,
        string culpritId,
        DifficultyProfile profile)
    {
        if (profile.AllEvidenceInitial) return;

        var culpritClues = blueprint.ClueLadder
            .Where(clue => clue.SupportsSuspectId == culpritId
                && clue.Strength is "supporting" or "decisive")
            .ToList();
        if (culpritClues.Count == 0) return;

        var decisive = culpritClues.FirstOrDefault(clue => clue.Strength == "decisive")
            ?? culpritClues[^1];
        foreach (var clue in culpritClues)
            if (!ReferenceEquals(clue, decisive) && clue.Strength == "decisive")
                clue.Strength = "supporting";
        decisive.Strength = "decisive";
        decisive.SourceType = "forensic";
        decisive.Role = "forensicAttribution";

        var supporting = culpritClues.Where(clue => !ReferenceEquals(clue, decisive)).ToList();
        if (supporting.Count > 0)
        {
            supporting[0].Role = "identity";
            if (string.Equals(supporting[0].SourceType, "forensic", StringComparison.OrdinalIgnoreCase))
                supporting[0].SourceType = "document";
        }
        if (supporting.Count > 1)
        {
            supporting[1].Role = "action";
            if (string.Equals(supporting[1].SourceType, "forensic", StringComparison.OrdinalIgnoreCase))
                supporting[1].SourceType = "digital";
        }
    }

    private static string FormatRepairGuidance(string? repairGuidance) =>
        string.IsNullOrWhiteSpace(repairGuidance)
            ? string.Empty
            : $"\nREPAIR REQUIREMENTS FROM VALIDATION:\n{repairGuidance}\nFix the root canonical facts; do not merely rephrase the same defect.";
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

    public async Task<PlotSuspect> RunAsync(
        CaseDraft draft,
        SuspectStub stub,
        CancellationToken ct,
        string? repairGuidance = null)
    {
        var system = $@"You are writing the **full profile** of ONE suspect in a detective case.
Stay consistent with the supplied case draft (victim, location, culprit). If this suspect IS the culprit, the alibi
should be partly verifiable but have a crack; if NOT the culprit, the alibi should be solid OR cleanly verifiable.
Treat `investigationBlueprint` as canonical truth. Do not invent a different method, time, access path, or motive.
For a decoy, align the apparent suspicion and eventual clearing path with its blueprint red herring.

Output a single JSON object with EXACTLY this suspect's data:
- id (echo the supplied `{stub.Id}`)
- name (echo the supplied `{stub.Name}`)
- age (int)
- occupation (short)
- relationship (to the victim, short)
- motive (1-2 specific, human-scale sentences; avoid generic greed/jealousy/anger)
- alibi (1-2 sentences with who/what can independently verify it and one realistic limitation)
- alibiVerified (boolean — true only if independently verified)
- background (markdown, 2-4 concise sentences containing only facts relevant to the investigation)

PLAYER-VISIBLE NEUTRALITY:
- This profile is visible to the player. Never say that evidence ""points to"", ""does not connect to"", ""clears"", or ""implicates"" this suspect.
- Do not mention private incident beats, the true method, or the culprit designation.
- Present only biography, plausible motive, stated alibi, and independently observable access/opportunity.
- Decoys must remain plausible until their evidence is examined; the culprit must not be obvious from wording or detail density.

The culprit for this case is `{draft.CulpritId}`. This suspect is `{(stub.IsCulprit ? "the culprit" : "not the culprit")}`.";

        var user = $@"CASE DRAFT (read-only):
{draft.ToSummaryJson()}

SUSPECT TO DESIGN:
id={stub.Id}
name={stub.Name}
role={stub.Role}
isCulprit={stub.IsCulprit}
{(string.IsNullOrWhiteSpace(repairGuidance) ? string.Empty : $"\nREPAIR REQUIREMENTS:\n{repairGuidance}\n")}

Emit JSON only.";

        var s = await TaskRunner.RunStructuredAsync<PlotSuspect>(_llm, _logger, $"SuspectCard:{stub.Id}", system, user, Schema, ct);
        s.Id = stub.Id; // never let the model drift the id
        s.Name = string.IsNullOrEmpty(s.Name) ? stub.Name : s.Name;
        s.AlibiVerified = false; // verification must come from player-visible evidence, not profile metadata
        return s;
    }
}
