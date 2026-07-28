# Manual Smoke Runbook — CaseZero v2

> **Target spec:** [`CASE_JSON_V2_SPEC.md`](CASE_JSON_V2_SPEC.md)
> **Reference case:** [`cases\case_001\case.json`](..\cases\case_001\case.json)

---

## Scope

This runbook is for the **current admin-only v2 case-generation flow**, not the older in-game case playthrough flow.

Current request path:

- Frontend: `POST /api/casegeneration/generate`
- API proxy: `backend\CaseZeroApi\Controllers\CaseGenerationController.cs`
- Function App starter: `POST /api/cases/v2/generate`
- Status endpoint: `GET /api/cases/v2/jobs/{jobId}`

---

## Prerequisites

| Tool | Version / note |
|---|---|
| .NET SDK | **9.x** for `CaseGen.Functions` and `CaseGen.Functions.Tests` |
| .NET SDK / runtime | 8.x for `backend\CaseZeroApi` |
| Node.js | 20+ |
| Azure Functions local setup | Required for the Function App |
| Azure Storage emulator / equivalent | Required locally because the Durable Functions app uses `AzureWebJobsStorage` |

Notes:

- The frontend defaults to `http://localhost:5001/api`.
- The HTTP request files under `tests\http-requests\casegen-functions\` assume the Function App is available on `http://localhost:7071`.
- The **Generate New Case** screen is protected by `ProtectedRoute requiredRole="ADMIN"`.

---

## Step 1 — Start the Function App

Start `functions\CaseGen.Functions` with your normal local Azure Functions workflow and verify the v2 endpoints are exposed.

Minimum endpoints to verify:

- `POST /api/cases/v2/generate`
- `GET /api/cases/v2/jobs/{jobId}`

If local storage/Azurite is not available, the Durable workflow will not run correctly.

---

## Step 2 — Start the API proxy

```powershell
Set-Location F:\repos\CaseZero-Alternative\backend\CaseZeroApi
dotnet run
```

Expected behavior:

- Swagger loads for the API host.
- The proxy controller exposes:
  - `POST /api/casegeneration/generate`
  - `GET /api/casegeneration/jobs/{jobId}`

If generation returns **503 Service Unavailable**, check the API configuration for `CaseGenerator:FunctionBaseUrl`.

---

## Step 3 — Start the frontend

```powershell
Set-Location F:\repos\CaseZero-Alternative\frontend
npm install
npm run dev
```

Open `http://localhost:5173`.

---

## Step 4 — Verify role-gated access

Log in with an account that has the `ADMIN` role.

Expected behavior:

- Non-admin users do **not** get the **Generate New Case** button and should be blocked from `/case-generation`.
- Admin users **do** get the dashboard button and can open `/case-generation`.

---

## Step 5 — Verify the generation form

On `/case-generation`, verify the current form fields match the code:

- Difficulty: `Rookie`, `Detective`, `Detective2`, `Sergeant`, `Lieutenant`, `Captain`, `Commander`
- Language: `en-US`, `pt-BR`, `es-ES`, `fr-FR`
- Optional fields: `title`, `location`, `theme`, `caseId`, `seed`
- Request always includes `writeToDisk: true` from the page state unless changed in code later

---

## Step 6 — Start a generation job

Submit a simple admin run, for example:

- Difficulty: `Rookie`
- Language: `en-US`
- Optional fields left blank

Expected response path:

1. Frontend calls `POST /api/casegeneration/generate`
2. API proxy forwards to `POST /api/cases/v2/generate`
3. Function starter returns `202 Accepted` with:
   - `jobId`
   - `status = "queued"`
   - `statusUri = "/api/cases/v2/jobs/{jobId}"`

If another generation is already running, expect **409 Conflict** with `runningJobId`.

---

## Step 7 — Poll job status and verify public stage order

The status payload should expose `pipelineVersion = "casegraph-v2"` and report these **public** stages in order:

| Order | Stage id | English UI label |
|---|---|---|
| 1 | `caseDesign` | `Case design` |
| 2 | `graphConstruction` | `Case graph construction` |
| 3 | `evidenceProduction` | `Evidence production` |
| 4 | `forensicWorkflow` | `Forensic workflow` |
| 5 | `solutionDesign` | `Solution design` |
| 6 | `deterministicValidation` | `Deterministic validation` |
| 7 | `solutionWitness` | `Solution witness replay` |
| 8 | `advisoryReview` | `Advisory specialist review` |
| 9 | `targetedRepair` | `Targeted repair` |
| 10 | `finalValidation` | `Final validation` |
| 11 | `finalization` | `Finalization and publishing` |

The same labels are localized in:

- `frontend\src\locales\en-US.ts`
- `frontend\src\locales\pt-BR.ts`
- `frontend\src\locales\es-ES.ts`
- `frontend\src\locales\fr-FR.ts`

### Internal phase sequence behind those public stages

The generator currently reports phases in this order:

1. `caseBible`
2. `plotOutline`
3. `suspectCards`
4. `assetPlan`
5. `assetsAndTimelineAndBriefing`
6. `forensicsPlan` + `outcomesAndInitialEmails` **or** `rookieInitialEvidence`
7. `mechanicalRules`
8. `rulesAndSolutionSkeleton`
9. `questionsAndExplanation`
10. `consistency`
11. optional `autoFixSchema`
12. `solutionWitness`
13. `advisoryReview`
14. optional/repeated `targetedRepair`
15. `finalValidation`
16. `renderAssets`
17. optional `publishToBlob`

### Rookie-specific expectation

`Rookie` runs intentionally bypass the true forensic workflow. In the public status model, `forensicWorkflow` can therefore end up marked as **skipped**.

---

## Step 8 — Verify retry behavior

The Durable orchestration wraps a **single long-running activity** and retries whole-job attempts when needed.

Current policy:

- Default `MaxAttempts`: **5**
- Configurable by `CaseGenV2:JobMaxAttempts`
- Hard cap: **10**
- Backoff: **15s → 30s → 60s → 120s → 240s**, capped at **300s**
- Retryable exception families include:
  - `CaseBibleValidationException`
  - `TimeoutException`
  - `HttpRequestException`
  - `Azure.RequestFailedException` with `408`, `429`, or `>= 500`
- Validation-error results can also schedule another attempt until `MaxAttempts` is reached

Status fields to verify when a retry happens:

- `currentAttempt`
- `maxAttempts`
- `nextRetryAt`
- `retryReason`
- `attempts[]`

Important: `targetedRepair` is an **in-run repair loop**, not the same thing as a Durable retry attempt.

---

## Step 9 — Verify successful completion payload

On a successful run, expect:

- top-level status becomes `done`
- `progressPercent = 100`
- `result` includes at least:
  - `CaseId`
  - `OutputPath`
  - `ValidationErrorsCount`
  - `AssetsRenderedPdfs`
  - `AssetsRenderedImages`
  - `BlobsPublished`
  - `GraphValidationPassed`
  - `SolverSucceeded`
  - `AttemptCount`
  - `MaxAttempts`
  - `SolverScore`
  - `SpecialistFindingsByCategory`

Healthy run expectations:

- `ValidationErrorsCount == 0`
- `GraphValidationPassed == true`
- `SolverSucceeded == true`
- `OutputPath` is non-empty when persistence is allowed

---

## Step 10 — Verify generated artifacts

If the generation passes persistence gates, verify artifacts under the case output directory:

- `cases\<caseId>\case.json`
- `cases\<caseId>\assets\*`

Expected rendering behavior:

- `pdf`, `document`, and `digital` assets render through the PDF stack
- `photo` and `image` assets render as images
- mandatory visuals failing to render block publication

---

## Optional targeted regression commands

### Functions progress / rendering checks

```powershell
Set-Location F:\repos\CaseZero-Alternative
dotnet test functions\CaseGen.Functions.Tests\CaseGen.Functions.Tests.csproj --filter "FullyQualifiedName~GenerationProgressTests|FullyQualifiedName~DocumentLayoutSmokeTests|FullyQualifiedName~AssetRenderingTests"
```

### API authorization check for the proxy

```powershell
Set-Location F:\repos\CaseZero-Alternative
dotnet test backend\CaseZeroApi.IntegrationTests\CaseZeroApi.IntegrationTests.csproj --filter "FullyQualifiedName~CaseGenerationAuthorizationTests"
```

---

## Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| `503` from `/api/casegeneration/generate` | API proxy cannot find the Function App base URL | Check `CaseGenerator:FunctionBaseUrl` in API configuration |
| `409 Conflict` on generation start | Another v2 generation job is already running | Poll the returned `runningJobId` or wait for completion |
| Job stuck before meaningful stage updates | Function storage / Durable setup is unavailable | Fix `AzureWebJobsStorage` / local storage emulator first |
| `forensicWorkflow` shows as skipped | You generated a `Rookie` case | This is expected |
| Job reaches `failed` after retries | Validation or retryable infra issue exhausted attempts | Inspect `error`, `retryReason`, and prior `attempts[]` |
| Status is `done` but result still indicates errors | Durable completed but `HasErrors` was true in the output payload | Treat the run as failed and inspect `result.ErrorMessage` |
