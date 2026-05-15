# Case Generation Pipeline

The CaseZero case generator is an Azure Functions app (`CaseGen.Functions`, .NET 9
isolated worker) that produces fully-playable detective cases on demand. It is
driven by a Durable Functions orchestration and a fine-grained **micro-task**
LLM pipeline. Each case is a `case.json` v2 document plus a folder of rendered
assets (PDFs, images, audio sidecars), all published to the same blob container
the website reads from.

## Entry points

- **`POST /api/cases/v2/generate`** — kicks off generation, returns `202 Accepted + jobId`.
  Body fields (all optional except `difficulty`):
  ```json
  {
    "caseId": "case_002",
    "title": "The River Drop",
    "theme": "Body found at a dockyard under the morning fog",
    "location": "Seattle, Washington",
    "difficulty": "Detective",
    "requiredRank": "Detective",
    "language": "en-US",
    "seed": 42,
    "writeToDisk": true
  }
  ```
- **`GET /api/cases/v2/jobs/{jobId}`** — poll status. Composes Durable runtime
  status with a per-phase blob status published by `JobPhaseReporter`. While
  running, returns `{ status: "running", currentPhase: "<stage>", ... }`. On
  completion returns the full result summary (validation errors, blobs
  published, assets rendered, refine telemetry, red-team verdict, etc).

A best-effort singleton check rejects concurrent runs with `409 Conflict` and
the `jobId` of the in-flight job.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│  POST /api/cases/v2/generate                                    │
│        ↓                                                         │
│  StartCaseV2GenerationFunction                                   │
│        ↓ schedule                                                │
│  CaseV2GenerationOrchestrator (durable, instanceId = jobId)     │
│        ↓                                                         │
│  CaseV2GenerateActivity                                          │
│        ↓                                                         │
│  CaseV2GeneratorService.GenerateAsync(request, reporter)         │
│        ↓ (13 phases, see below)                                  │
│  ✅ case.json + assets/* on disk and in blob `bundles/`          │
└─────────────────────────────────────────────────────────────────┘
```

The website (`CaseZeroApi` → `CaseV2StorageService`) reads from the same
`bundles` container in production, so a successful generation is immediately
visible to players the next time the dashboard cache refreshes.

## The phases

`CaseV2GeneratorService` runs these stages in order. The phase name shown is
what `JobPhaseReporter` publishes during execution so the GET status endpoint
can show progress. Per-stage latencies are reported on completion.

| # | Phase | Role |
|---|-------|------|
| 1 | `plotOutline` | LLM produces the seed: setting, victim, incident, briefing email, suspect stubs, culprit choice |
| 2 | `suspectCards` | One LLM call per suspect, in parallel — full background, motive, alibi, alibi-verified flag |
| 3 | `assetPlan` | LLM decides which evidence assets exist (initial vs gated, file types, descriptions) |
| 4 | `assetsAndTimelineAndBriefing` | Parallel: full asset bodies, briefing email body, timeline events |
| 5 | `forensicsPlan` | LLM picks which assets get a forensic outcome and the analysis types |
| 6 | `outcomesAndInitialEmails` | Parallel: forensic outcome bodies + the additional initial emails |
| 7 | `mechanicalRules` | **Deterministic** — `MechanicalRulesBuilder` emits the `rules[]` array from the forensic outcomes (no LLM) |
| 8 | `rulesAndSolutionSkeleton` | LLM enriches rules where needed + fills the solution skeleton |
| 9 | `questionsAndExplanation` | Parallel: solution questions + the case explanation |
| 10 | `consistency` | **Deterministic** — `ConsistencyValidator` cross-checks IDs, timeline ordering, rule referents |
| 11 | `autoFixSchema` *(only if errors)* | **Deterministic** — `SchemaErrorAutoFixer` rewrites common LLM ID-format slips (`asset_x` → `asset.x`, etc.) |
| 12 | `redTeamAndSolver` | Parallel: red-team adversarial review + solver verifies the case is fairly solvable |
| 13 | `refineCase` *(conditional)* | LLM refines the case when schema errors persist OR red-team rejects with high-severity findings |
| 14 | `renderAssets` | `AssetRenderingService` materialises PDFs (QuestPDF) and images (gpt-image-1) in parallel |
| 15 | `publishToBlob` | `CaseV2BlobPublisher` mirrors `case.json` + every asset to the configured blob container |

Stages that have nothing to do (no errors → no auto-fix, no schema errors and
red-team OK → no refine, blob publisher not configured locally) are skipped
silently.

## Refine loop (defense in depth)

When `autoFixSchema` can't clear all schema errors deterministically, OR
when `redTeamAndSolver` returns `verdict: "reject"` with at least one finding of
severity `high`, the orchestrator runs **one** `refineCase` pass: it sends the
full JSON, the schema errors, and the high-severity findings to the LLM and
asks for a corrected document. The refined JSON is only accepted if it
**strictly reduces** the error count vs. the pre-refine document, otherwise the
pre-refine version is kept (no regressions allowed).

The response surfaces `AutoFixesApplied`, `RefineAttempted`,
`RefineErrorsBefore`, `RefineErrorsAfter`, and `RedTeamVerdict` so operators
can tell what salvage path ran.

## Local development

The function reads from `local.settings.json` for the Azure OpenAI / Foundry
keys and the storage connection string. Azurite stands in for blob in local
dev. See [`RUNNING_FUNCTIONS_LOCALLY.md`](RUNNING_FUNCTIONS_LOCALLY.md) for the
one-command bring-up and a worked example.

## Code map

- `Functions/CaseV2/CaseV2GenerationOrchestrator.cs` — Durable orchestrator,
  starter HTTP function, GET status function.
- `Services/CaseV2/CaseV2GeneratorService.cs` — the multi-phase orchestrator
  proper (the function above just bridges Durable).
- `Services/CaseV2/Tasks/` — one file per LLM task (`PlotOutlineTask`,
  `SuspectCardTask`, `AssetPlanTask`, `RedTeamTask`, `SolverTask`,
  `RefineCaseTask`, etc).
- `Services/CaseV2/MechanicalRulesBuilder.cs` — deterministic rule emission.
- `Services/CaseV2/ConsistencyValidator.cs` — deterministic cross-ref check.
- `Services/CaseV2/SchemaErrorAutoFixer.cs` — deterministic ID-format
  normaliser. Unit-tested in `CaseGen.Functions.Tests`.
- `Services/CaseV2/AssetRenderingService.cs` — PDF/image materialisation.
- `Services/CaseV2/CaseV2BlobPublisher.cs` — final mirror to blob.
- `Services/CaseV2/JobPhaseReporter.cs` — per-phase status JSON in blob, read
  by the GET status endpoint.
