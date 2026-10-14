# Running CaseGen.Functions locally (Mac + Windows)

This guide covers running the **CaseZero case generator** locally to produce a `case.json` v2 bundle. The current flow is the micro-task pipeline exposed at `POST /api/cases/v2/generate`, with strict quality checks before isolated rendering and publication. See [Case Generation Pipeline](CASE_GENERATION_PIPELINE.md) for the exact stages and current limitations.

> Functions stay on **.NET 9** (per project policy). The site stays on .NET 8.

## 1. Prereqs

Same on Mac and Windows:

- **Node 20+** — for Azurite + Azure Functions Core Tools.
- **.NET 9 SDK** — https://dotnet.microsoft.com/download
- An **Azure Foundry / Azure OpenAI** endpoint + key (the LLM provider). Default reads from `functions/CaseGen.Functions/local.settings.json`.

If anything is missing the run scripts install Azurite + Functions Core Tools automatically via `npm`.

### Isolated real-model pilot without the Functions host

For the controlled **pt-BR Rookie** pilot, use the .NET 9 console runner:

```bash
dotnet run --project tools/CaseGen.Pilot/CaseGen.Pilot.csproj -- . ../casezero-pilot-rookie-001
```

Run from the repository root and choose a **new** output directory outside the
repository. This explicitly calls the real configured model and can incur costs.
It uses the production generator/renderers, writes local diagnostics, and
replaces cloud publication with a publisher that refuses every upload. No
Functions host, Azurite, database, portal login or Azure CLI session is needed
when the resource permits the configured API key. The current provider is
key-based; `HTTP 403 AuthenticationTypeDisabled` means the resource rejects this
authentication mode, not that a local Functions host is missing.

See [the handoff](CASE_GENERATION_HANDOFF.md) for credentials, the known blocker,
artifact interpretation and the required analysis before moving to Detective.

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
4. Builds with the canonical `schemas/case.schema.json` linked automatically into build/publish output as `Schemas/case.v2.schema.json`.
5. Builds + starts the Functions host on `http://localhost:7071`.

Stop the host with `Ctrl+C`. Azurite keeps running — kill it with `kill $(cat AzuriteConfig/azurite.pid)` or `Stop-Process` on Windows.

## 3. Generate a case (v2 — async job)

Since v0.9 the v2 endpoint is **asynchronous**: `POST /api/cases/v2/generate` returns `202 Accepted + jobId` immediately, and you poll `GET /api/cases/v2/jobs/{jobId}` until `status == "done"` (or `"failed"`). Generation takes 8–12 minutes — well past the 230s HTTP limit, which is why we run it as a Durable Functions orchestration.

### Start the job

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

Response:

```json
{
  "jobId": "casev2-20260515081234-a1b2c3",
  "status": "queued",
  "statusUri": "/api/cases/v2/jobs/casev2-20260515081234-a1b2c3"
}
```

> **Single-instance**: if another v2 generation is already running, the endpoint returns **409 Conflict** with the running `jobId`. Wait for it, or query its status first.

### Poll the job

```bash
curl -sS http://localhost:7071/api/cases/v2/jobs/casev2-20260515081234-a1b2c3 | jq
```

While running:

```json
{
  "jobId": "casev2-20260515081234-a1b2c3",
  "status": "running",
  "currentPhase": "outcomesAndInitialEmails",
  "runtimeStatus": "Running",
  "createdAt": "2026-05-15T08:12:34Z",
  "lastUpdatedAt": "2026-05-15T08:18:11Z",
  "result": null,
  "error": null
}
```

When done:

```json
{
  "jobId": "casev2-20260515081234-a1b2c3",
  "status": "done",
  "currentPhase": null,
  "runtimeStatus": "Completed",
  "result": {
    "JobId": "casev2-20260515081234-a1b2c3",
    "CaseId": "case_002",
    "OutputPath": "/path/to/cases/case_002/case.json",
    "ValidationErrorsCount": 0,
    "AssetsRenderedPdfs": 6,
    "AssetsRenderedImages": 2,
    "BlobsPublished": 10,
    "HasErrors": false,
    "ErrorMessage": null,
    "StageLatencyMs": { "plotOutline": 4321, "suspectCards": 6532 },
    "Bundle": {
      "BundleId": "0123456789abcdef0123456789abcdef",
      "Revision": 0,
      "Verified": true,
      "AssetsVerified": 8,
      "TotalBytes": 245760,
      "ManifestChecksum": "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
    },
    "Quality": {
      "Approved": true,
      "Preview": false,
      "PersistenceRequested": true,
      "Persisted": true,
      "PublicationRequested": true,
      "Published": true,
      "SolverMode": "diagnostic",
      "Issues": []
    }
  },
  "error": null
}
```

Status values: `queued | running | done | failed`. While `running`, the optional `currentPhase` field shows the current pipeline stage (best-effort, written to `jobs/{jobId}/status.json` in blob storage).

A Durable `runtimeStatus: "Completed"` can still mean `status: "failed"`:
`needs_review`, `reject`, medium/high findings, invalid/unavailable review,
render failures, missing files, and configured publication failures are blockers.
The failed job retains its result and `Quality.Issues`; inspect those codes,
references, and original messages instead of relying on validation counts alone.

### What's persisted

- `cases/<caseId>/case.json` + `cases/<caseId>/assets/<bundleId>/*` — verified immutable files/manifest plus an atomic JSON-only commit with `writeToDisk=true`.
- `bundles/<caseId>/case.json` + `bundles/<caseId>/assets/<bundleId>/*` — published when storage is configured; immutable files are verified remotely before the conditional JSON commit.
- `jobs/<jobId>/status.json` — per-job phase doc (read by the GET endpoint).

Use `Quality.Persisted` and `Quality.Published` for actual outcomes. Unconfigured
publication is local-only mode; configured but unavailable Azurite is a failure.
`CaseGenV2:CasesBasePath` can set the local destination and must be writable.
Re-running an approved generation with the same `caseId` changes only its final
JSON pointer/content, not files used by previous readers. Conflicting local
locks or remote ETags fail explicitly. There is no transaction across Blob and
disk; confirmed remote success can coexist with a local failure. Old and
uncertain remote bundles are retained, not automatically garbage-collected.
Use a new ID for experimental runs; `seed` is not replay/checkpoint support.

### Body fields (all optional)

| Field | Default | Notes |
|-------|---------|-------|
| `caseId` | `case_yyyymmdd_hhmmss` | Up to 128 characters; letters/digits/underscores/hyphens; lowercased, hyphens replaced with underscores, prefixed `case_` if needed |
| `title` | model picks | Nonblank if supplied, up to 200 characters; overrides the plot title |
| `theme` | model picks | Nonblank if supplied, up to 2000 characters |
| `location` | model picks | Nonblank if supplied, up to 300 characters |
| `difficulty` | `Detective` | One of: `Rookie · Detective · Detective2 · Sergeant · Lieutenant · Captain · Commander` |
| `requiredRank` | `difficulty` | Same enum |
| `language` | `en-US` | `pt-BR`, `en-US`, `es-ES`, or `fr-FR`; included in draft/task context, but no semantic language certification yet |
| `seed` | none | Signed 32-bit integer, forwarded into `gameMetadata.generation.seed`; not true reproducibility |
| `writeToDisk` | `true` | `false` checks the draft without rendering, saving, or publishing; the polling result is a summary, not the full case JSON |

An empty body or `{}` preserves defaults. Invalid JSON/UTF-8, duplicates,
unknown fields, invalid ranks/languages/types, and blank explicit hints return
400 (`invalid_request`) instead of starting with defaults. Bodies above 16 KiB
return 413 (`request_too_large`). These responses include `errors` and do not
schedule a job. Omit an optional hint to let the model choose it; the UI does
this automatically for cleared fields.

Native structured output is optional:
`AzureFoundry__UseStructuredOutputs=true`, only on a compatible model
deployment. Local schema validation runs in both modes. Unsupported native
schema shapes use an explicitly logged prompt path, not a second API attempt
after rejection. See [contract and retry details](CASE_GENERATION_PIPELINE.md#microtask-contracts-and-bounded-calls).

Schema validation uses the MIT-licensed `JsonSchema.Net 7.4.0` package with
mandatory date-time format validation and Draft 2020-12 processing. No
commercial schema-validation license or hourly quota is required. Keep the
package pin unless the replacement binary/dependency licenses have been
reviewed; newer binaries may have additional maintenance-fee terms.

## 4. Pipeline overview (micro-tasks, parallel, with context continuity)

Focused tasks share the canonical draft. Forensic detail and solution-content
tasks receive full relevant bodies, and red-team compares all document/email
bodies and complete rules with the compiled JSON. Templates are applied before
review, not again during rendering. The solver reads player-visible bodies,
but does not execute gameplay or inspect the actual rendered PDFs; it remains
diagnostic only.

After the draft passes the strict gate, **AssetRenderingService** works in
`<CasesBasePath>/.casegen-staging/<workspace>/<caseId>/assets/`:

- **PDF / document / digital** → PDFs rendered with QuestPDF. Initial cards require validated `bodyDoc`; forensic report markdown remains supported.
- **photo / image** → real image generated by the configured image model (`ILLMProvider.GenerateImageAsync(prompt)`). Prompt = `body` if present, otherwise the description.
- **speech evidence** → explicitly planned document transcripts, rendered as PDFs.
- **audio / video / unsupported types** → explicit failure, without a placeholder or fictional media URI.

The renderer reports actual filenames/MIME. The sealed manifest checks
signatures/basic terminators, size, SHA-256 and exact coverage; final URIs use
`assets/<bundleId>/<actualFilename>`, including JPEG/WebP output. Public JSON
and destination bytes are reverified before commit. File integrity is not
certification of extracted PDF text, visual image content, or playability;
those checks and new document families remain separate work.
One canonical repair can replace affected components and their dependent
material. Acceptance requires recompilation, contract/consistency validation,
and a new successful review of that exact revision, including semantic repairs
with zero schema errors before and after. Failed repair explicitly blocks
output; invalid/unavailable initial review never triggers content repair.
`result.Repair` records acceptance, changed components, and original blockers.

The authoritative phase table, output modes, and quality contract are in
[Case Generation Pipeline](CASE_GENERATION_PIPELINE.md). Optional/skipped stages
are not painted as completed in the generation UI.

### Why micro-tasks instead of one big call?

- **Token budget:** focused tasks limit output size; full-body review/repair may require a larger input context. Truncated output is an explicit failure, not an accepted partial case.
- **Quality per scope:** the model focuses on ONE suspect / ONE asset / ONE question.
- **Failure isolation:** a malformed microtask gets at most one retry with its specific contract error; permanent provider errors/cancellation do not retry. Solver/refine have no retries.
- **Parallelism:** independent steps inside a phase run via `Task.WhenAll`.
- **Context continuity:** the orchestrator passes the growing `CaseDraft` (real motives, descriptions, timeline, dates) so a suspect's alibi from Phase 2 is consistently referenced in the Phase 8 question, and the timeline never drifts off `incidentDate`/`openedAt`.

## 5. Feeding the site

The site's `CaseV2StorageService` prefers the local `cases/` directory before falling back to blob, so a freshly generated `case_002` immediately shows up in `GET /api/cases/dashboard` after a backend restart (or `force-reload` of the dashboard if storage cache is empty).

To regenerate the same id with tweaks, POST again with `caseId: "case_002"`.
The new immutable bundle replaces only the committed `case.json`; cached
readers can still load old files. A stale retry cannot revert a newer commit.

## 6. Troubleshooting

- **`AzureFoundry:ApiKey not configured`** — fill in `functions/CaseGen.Functions/local.settings.json` (do not commit secrets) or export `AzureFoundry__ApiKey` in your shell.
- **`Canonical case schema is missing from the build output`** — rebuild/republish the Functions project; MSBuild distributes the canonical schema. Do not create a second versioned source copy.
- **`invalid_request` / `request_too_large`** — correct the reported `errors` before resubmitting; no generation was scheduled.
- **A microtask fails with a contract error** — inspect the named task and field. Missing bodies, invalid tables, drifted IDs, unsupported forensic pairs, and invalid answer keys now fail early rather than being silently substituted.
- **`502 / 504` from the LLM** — inspect the job diagnostics before retrying. Use a new `caseId` for experiments; generation still has no persistent microtask checkpoint recovery.
- **`bundle_invalid` / `asset_integrity_failed` / `asset_file_missing`** — inspect file references and manifest diagnostics. Missing, extra, truncated, mislabeled, or changed files cannot be committed.
- **`publication_conflict`** — another writer changed the case or its local lock is unavailable. Inspect the current committed case before starting a new generation; do not delete prior bundles or blindly retry stale publication data.
- **Validation errors in the response** — the model violated the v2 schema. Re-run; if persistent, narrow the prompt by setting `theme` more explicitly or lowering `difficulty`.
- **Mac M-series + Azurite EACCES on tcp 10000/10001/10002** — kill any stale Azurite process: `kill $(cat AzuriteConfig/azurite.pid)`.
