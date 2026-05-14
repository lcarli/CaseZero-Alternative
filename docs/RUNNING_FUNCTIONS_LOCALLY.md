# Running CaseGen.Functions locally (Mac + Windows)

This guide covers running the **CaseZero case generator** locally to produce a `case.json` v2 that the site can consume. The full Plan→Expand→Design→Documents→Media→Normalize pipeline is preserved, but the canonical flow for now is the **5-stage v2 pipeline** exposed at `POST /api/cases/v2/generate`.

> Functions stay on **.NET 9** (per project policy). The site stays on .NET 8.

## 1. Prereqs

Same on Mac and Windows:

- **Node 20+** — for Azurite + Azure Functions Core Tools.
- **.NET 9 SDK** — https://dotnet.microsoft.com/download
- An **Azure Foundry / Azure OpenAI** endpoint + key (the LLM provider). Default reads from `functions/CaseGen.Functions/local.settings.json`.

If anything is missing the run scripts install Azurite + Functions Core Tools automatically via `npm`.

## 2. One-command bring-up

**Mac / Linux:**
```bash
./scripts/run-functions.sh
```

**Windows (PowerShell):**
```powershell
./scripts/run-functions.ps1
```

The script:
1. Verifies `dotnet`, `node`.
2. Installs Azurite and `azure-functions-core-tools@4` globally if missing.
3. Spins up Azurite in the background (logs in `AzuriteConfig/azurite.log`, PID in `AzuriteConfig/azurite.pid`).
4. Copies the canonical schema to `functions/CaseGen.Functions/Schemas/case.v2.schema.json`.
5. Builds + starts the Functions host on `http://localhost:7071`.

Stop the host with `Ctrl+C`. Azurite keeps running — kill it with `kill $(cat AzuriteConfig/azurite.pid)` or `Stop-Process` on Windows.

## 3. Generate a case (v2)

```bash
curl -sS -X POST http://localhost:7071/api/cases/v2/generate \
  -H "Content-Type: application/json" \
  -d '{
    "caseId": "case_002",
    "title": "The River Drop",
    "theme": "Body found at a dockyard under the morning fog",
    "location": "Seattle, Washington",
    "difficulty": "Detective",
    "requiredRank": "Detective",
    "language": "en-US",
    "seed": 42,
    "writeToDisk": true
  }' | jq
```

Response (truncated):

```json
{
  "caseId": "case_002",
  "outputPath": "/Users/.../cases/case_002/case.json",
  "stageLatencyMs": { "plot": 4321, "evidence": 6532, "forensics": 7011, "rules": 3120, "solution": 4002 },
  "validationErrors": [],
  "preview": {
    "title": "The River Drop",
    "location": "Seattle, Washington",
    "difficulty": "Detective",
    "suspectCount": 4,
    "assetCount": 7,
    "emailCount": 4,
    "ruleCount": 5
  }
}
```

The file is written to `cases/<caseId>/case.json` (idempotent — re-running with the same `caseId` overwrites).

### Body fields (all optional)

| Field | Default | Notes |
|-------|---------|-------|
| `caseId` | `case_yyyymmdd_hhmmss` | Forced to lower-case and prefixed `case_` if missing |
| `title` | model picks | Overrides whatever the plot stage produced |
| `theme` | model picks | A short hint to anchor the plot |
| `location` | model picks | City, state |
| `difficulty` | `Detective` | One of: `Rookie · Detective · Detective2 · Sergeant · Lieutenant · Captain · Commander` |
| `requiredRank` | `difficulty` | Same enum |
| `language` | `en-US` | Future use — story strings still come out in EN by default |
| `seed` | none | Forwarded into `gameMetadata.generation.seed` |
| `writeToDisk` | `true` | Set to `false` if you only want the JSON in the response |

## 4. Pipeline overview (micro-tasks, parallel where possible)

The endpoint runs a **fine-grained pipeline** of focused LLM calls. Each call has a tiny scope (the case-wide context is summarised, not pasted whole) and tasks that have no inter-dependency run in parallel via `Task.WhenAll`. The assembler is deterministic C# and runs schema validation against `schemas/case.schema.json`.

### Phases

| # | Phase | LLM calls | Parallel? | Purpose |
|---|-------|-----------|-----------|---------|
| 1 | **Plot Outline** | 1 | — | `metadata` + victim + 3-6 suspect stubs (id + name + 1-line role) + culprit pick |
| 2 | **Suspect Cards** | N (one per suspect) | ✅ | Full profile: motive, alibi, alibiVerified, background |
| 3 | **Asset Plan** | 1 | — | List of 4-10 asset stubs (id + type + title + role) |
| 4 | **Asset Cards · Timeline · Briefing** | M+2 | ✅ all in one batch | Per-asset detail, narrative timeline + 0-3 temporal events, chief-of-police email |
| 5 | **Forensics Plan** | 1 | — | `analysisTypes` + outcome stubs (with smoking-gun pinned to culprit) |
| 6 | **Forensic Outcomes · Initial Emails** | K+1 | ✅ all in one batch | Per-outcome conclusion + (if findings) hidden result PDF + hidden lab email; 0-3 follow-up initial emails |
| 7 | **Rules · Solution Skeleton** | 2 | ✅ in parallel | Rules wiring (`forensics_complete → reveal_*` etc.) + required evidence/analysis IDs + question topic list |
| 8 | **Questions · Explanation** | Q+1 | ✅ all in one batch | One MCQ per topic + final markdown explanation |
| 9 | **Assemble + Validate** | 0 | — | C# deterministic. Re-validates the merged JSON against the v2 schema and writes to disk |

For a typical 4-suspect, 6-asset, 2-outcome, 3-question case that's ~20 LLM calls total, but most of them are tiny (a single suspect / single asset / single question). Wall-clock benefits a lot from the parallel phases.

### Why micro-tasks instead of one big call?

- **Token budget:** large cases used to choke a one-shot prompt. Each micro-task fits comfortably in a few-k-token window.
- **Quality per scope:** the model focuses on ONE suspect / ONE asset / ONE question instead of juggling them all.
- **Failure isolation:** if `SuspectCard:suspect.eva_blackwood` fails, only that one is retried (built-in 1-shot retry inside `TaskRunner`).
- **Parallelism:** independent steps inside a phase run via `Task.WhenAll`.
- **ID coherence:** the orchestrator passes a slim `CaseDraft` (IDs + 1-line roles only) downstream so the model can't drift IDs.

## 5. Feeding the site

The site's `CaseV2StorageService` prefers the local `cases/` directory before falling back to blob, so a freshly generated `case_002` immediately shows up in `GET /api/cases/dashboard` after a backend restart (or `force-reload` of the dashboard if storage cache is empty).

To regenerate the same id with tweaks, just POST again with `caseId: "case_002"` — the file is overwritten.

## 6. Troubleshooting

- **`AzureFoundry:ApiKey not configured`** — fill in `functions/CaseGen.Functions/local.settings.json` (do not commit secrets) or export `AzureFoundry__ApiKey` in your shell.
- **`Could not locate case.v2 schema`** — re-run the script; it copies the schema at start. Or copy `schemas/case.schema.json` → `functions/CaseGen.Functions/Schemas/case.v2.schema.json` manually.
- **`502 / 504` from the LLM** — re-run; transient. The pipeline is idempotent per `caseId`.
- **Validation errors in the response** — the model violated the v2 schema. Re-run; if persistent, narrow the prompt by setting `theme` more explicitly or lowering `difficulty`.
- **Mac M-series + Azurite EACCES on tcp 10000/10001/10002** — kill any stale Azurite process: `kill $(cat AzuriteConfig/azurite.pid)`.
