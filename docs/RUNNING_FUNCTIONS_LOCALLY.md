# Running CaseGen.Functions locally (Mac + Windows)

This guide reflects the **current** `CaseGen.Functions` project and local scripts in this repository.

> `functions/CaseGen.Functions` and `functions/CaseGen.Functions.Tests` stay on **.NET 9**. The backend remains on **.NET 8**.

## 1. What actually runs locally

Current triggers in [`../functions/CaseGen.Functions/Functions/CaseV2/CaseV2GenerationOrchestrator.cs`](../functions/CaseGen.Functions/Functions/CaseV2/CaseV2GenerationOrchestrator.cs):

- `POST /api/cases/v2/generate` — HTTP starter
- `GET /api/cases/v2/jobs/{jobId}` — HTTP status endpoint
- `CaseV2GenerationOrchestrator` — Durable orchestrator
- `CaseV2GenerateActivity` — Durable activity

There are **no timer triggers** and **no queue-triggered functions** in this project today. Azurite is still required because Durable Functions and job-status blobs rely on storage.

## 2. Prerequisites

- **.NET 9 SDK**
- **Node.js 20+**
- **Azure Functions Core Tools v4**
- **Azurite**
- An **Azure Foundry / Azure OpenAI-compatible** endpoint, model names, and API key

The repo scripts install **Functions Core Tools v4** and **Azurite** automatically if missing:
- [`../scripts/run-functions.ps1`](../scripts/run-functions.ps1)
- [`../scripts/run-functions.sh`](../scripts/run-functions.sh)

## 3. Required local settings (keys only)

Crie `functions/CaseGen.Functions/local.settings.json` com estas chaves. Esse arquivo é local, contém segredos e não deve ser versionado:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "CaseGeneratorStorage__ConnectionString": "UseDevelopmentStorage=true",
    "CaseGeneratorStorage__BundlesContainer": "bundles",
    "LLM__UseAzureFoundry": "true",
    "AzureFoundry__Endpoint": "https://<your-foundry-endpoint>",
    "AzureFoundry__ModelName": "<your-text-model-deployment>",
    "AzureFoundry__ImageDeploymentName": "<your-image-model-deployment>",
    "AzureFoundry__ApiKey": "<YOUR_OPENAI_KEY>",
    "CaseGenV2__JobMaxAttempts": "5",
    "ASPNETCORE_ENVIRONMENT": "Development"
  }
}
```

Notes:
- Do **not** commit real keys.
- `Program.cs` loads both `local.settings.json` and environment variables, so env vars are also valid.
- The Azure Foundry provider requires:
  - `AzureFoundry:Endpoint`
  - `AzureFoundry:ModelName`
  - `AzureFoundry:ImageDeploymentName`
  - `AzureFoundry:ApiKey`
- Storage can come from managed identity (`CaseGeneratorStorage:AccountName`) or a connection string, but local dev in this repo is designed around **Azurite**.

## 4. One-command bring-up

### Windows (PowerShell)

```powershell
.\scripts\run-functions.ps1
```

### Mac / Linux

```bash
./scripts/run-functions.sh
```

What the scripts do today:
1. verify `dotnet` and `node`
2. install `azure-functions-core-tools@4` globally if missing
3. install `azurite` globally if missing
4. start Azurite in the background using **`AzuriteConfig`**
5. write Azurite logs to `AzuriteConfig/azurite.log`
6. store the Azurite PID in `AzuriteConfig/azurite.pid`
7. copy `schemas/case.schema.json` to `functions/CaseGen.Functions/Schemas/case.v2.schema.json`
8. build `CaseGen.Functions.csproj`
9. start the Functions host on `http://localhost:7071`

`host.json` currently sets:
- Durable hub name: `CaseGenHub`
- Durable storage provider: `AzureStorage`
- `functionTimeout`: `01:00:00`

## 5. Manual bring-up (if you do not use the scripts)

### Windows

```powershell
azurite --silent --location .\AzuriteConfig --debug .\AzuriteConfig\azurite.log
Copy-Item .\schemas\case.schema.json .\functions\CaseGen.Functions\Schemas\case.v2.schema.json -Force
Set-Location .\functions\CaseGen.Functions
dotnet build CaseGen.Functions.csproj
func start --csharp
```

### Mac / Linux

```bash
azurite --silent --location ./AzuriteConfig --debug ./AzuriteConfig/azurite.log
cp ./schemas/case.schema.json ./functions/CaseGen.Functions/Schemas/case.v2.schema.json
cd ./functions/CaseGen.Functions
dotnet build CaseGen.Functions.csproj
func start --csharp
```

## 6. Generate a case locally

The current API is asynchronous.

### Start a job

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
  }'
```

Typical response:

```json
{
  "jobId": "casev2-20260728073000-a1b2c3",
  "status": "queued",
  "statusUri": "/api/cases/v2/jobs/casev2-20260728073000-a1b2c3"
}
```

### Poll the job

```bash
curl -sS http://localhost:7071/api/cases/v2/jobs/casev2-20260728073000-a1b2c3
```

Status values are currently:
- `queued`
- `running`
- `done`
- `failed`

The status document also tracks attempts/stages and is written to:
- `jobs/<jobId>/status.json`

## 7. Supported request fields

| Field | Current behavior |
|---|---|
| `caseId` | Optional; if omitted, the generator creates one |
| `title` | Optional override |
| `theme` | Optional prompt hint |
| `location` | Optional prompt hint |
| `difficulty` | Optional |
| `requiredRank` | Optional |
| `language` | Defaults to `en-US`; repo tooling also recognizes `pt-BR`, `es-ES`, `fr-FR` |
| `seed` | Optional deterministic seed |
| `writeToDisk` | Defaults to `true` |

## 8. What gets written where

### Local filesystem

When `writeToDisk=true`, the generator writes into the repo `cases` directory when it can resolve the repository root:

- `cases/<caseId>/case.json`
- `cases/<caseId>/assets/*`

### Blob storage / Azurite

The current code uses storage for two separate concerns:

- `bundles/<caseId>/case.json` + `bundles/<caseId>/assets/*` — published bundle consumed by the backend/site
- `jobs/<jobId>/status.json` — per-job progress document

Current defaults:
- bundles container: `bundles`
- job-status container: `jobs`

## 9. Useful validation command

The existing helper script can generate and/or validate artifacts:

- [`../scripts/validate-casev2.ps1`](../scripts/validate-casev2.ps1)

Examples:

```powershell
.\scripts\validate-casev2.ps1 -Mode Generate -Difficulty Rookie -Theme "office theft" -Seed 1
.\scripts\validate-casev2.ps1 -Mode Artifact -CaseDirectory .\cases\case_002
```

## 10. Troubleshooting

- **`AzureFoundry:ApiKey not configured` / `AzureFoundry:Endpoint not configured`**

  Fill in the local settings with placeholders replaced by your own values, or export equivalent environment variables.

- **`AzureFoundry:Endpoint must be a valid absolute HTTPS URI`**

  The endpoint must be a full `https://...` URL.

- **`Could not locate case.v2 schema`**

  Re-run the repo script. It copies `schemas/case.schema.json` into the Functions project before startup.

- **Job returns `409 Conflict` on POST**

  The HTTP starter enforces best-effort single-instance execution. Poll the existing `jobId` first.

- **Status never advances / blob progress missing**

  Check Azurite first. The Functions app writes progress to the `jobs` container, and local blob publishing also depends on storage.

- **Cases do not appear in the site locally**

  Confirm `cases/<caseId>/case.json` was written. The backend prefers the local `cases` directory in dev before blob storage.

- **Need to stop Azurite**

  Use the PID in `AzuriteConfig/azurite.pid`, or stop the saved process directly.
