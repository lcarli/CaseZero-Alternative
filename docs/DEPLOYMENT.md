# Guia de Deploy - Sistema CaseZero

## Visão geral

Este documento foi atualizado para refletir o **estado atual real** do repositório, da infraestrutura em Bicep e dos workflows do GitHub Actions.

**Fontes de verdade:**
- Infraestrutura: [`../infrastructure/main.bicep`](../infrastructure/main.bicep)
- Parâmetros: [`../infrastructure/parameters.dev.json`](../infrastructure/parameters.dev.json) e [`../infrastructure/parameters.prod.json`](../infrastructure/parameters.prod.json)
- Deploy de infraestrutura: [`../.github/workflows/infrastructure-3tier.yml`](../.github/workflows/infrastructure-3tier.yml)
- Build/test/deploy DEV da aplicação: [`../.github/workflows/cd-dev.yml`](../.github/workflows/cd-dev.yml)

> Hoje o repositório tem **um workflow de deploy de infraestrutura para `dev` e `prod`** e **um workflow de deploy da aplicação somente para DEV**. Não existe, neste momento, um workflow separado de deploy de código da aplicação para produção.

## Pré-requisitos

### Ferramentas locais

| Componente | Versão atual / mínima prática | Uso |
|---|---|---|
| Azure CLI + Bicep | Azure CLI com `az bicep` habilitado | Validar e aplicar `infrastructure/main.bicep` |
| Node.js | 20.x | Build do frontend e testes no workflow |
| .NET SDK | 8.0.x e 9.0.x | API em .NET 8; Functions e testes em .NET 9 |
| GitHub Actions secrets | configurados no repositório/ambiente | Autenticação Azure e build/deploy |

### Secrets usados pelos workflows (nomes apenas)

**`infrastructure-3tier.yml`**
- `AZURE_CREDENTIALS_DEV`
- `AZURE_CREDENTIALS_PROD`
- `SQL_ADMIN_LOGIN`
- `SQL_ADMIN_PASSWORD`

**`cd-dev.yml`**
- `AZURE_CREDENTIALS_DEV`
- `AZURE_STATIC_WEB_APPS_API_TOKEN_DEV`
- `VITE_API_URL`
- `GITHUB_TOKEN`

## Topologia Azure atualmente provisionada

## Resource groups

O orquestrador (`infrastructure/main.bicep`) cria **4 resource groups** por ambiente:

- `casezero-shared-<env>-rg`
- `casezero-api-<env>-rg`
- `casezero-func-<env>-rg`
- `casezero-web-<env>-rg`

## Shared layer

Provisionada por [`../infrastructure/shared/main.bicep`](../infrastructure/shared/main.bicep):

- **Key Vault**: `kv-ca-<env>-<uniqueString>`
- **Log Analytics Workspace**: `casezero-logs-<env>`
- **Application Insights**: `casezero-insights-<env>`
- **Azure SQL Server**: `casezero-sql-<env>` *(opcional)*
- **Azure SQL Database**: `casezero-db` *(opcional)*

SQL **não é obrigatório**:
- `dev`: `enableSqlDatabase = true` em `parameters.dev.json`
- `prod`: `enableSqlDatabase = false` em `parameters.prod.json`

## API layer (.NET 8)

Provisionada por [`../infrastructure/api/main.bicep`](../infrastructure/api/main.bicep):

- **App Service Plan (Linux)**: `casezero-api-plan-<env>`
  - `dev`: **B1**
  - `prod`: **P1v3** com capacidade 2
- **Web App**: `casezero-api-<env>`
- Runtime: `DOTNETCORE|8.0`
- Health check: `/health`
- CORS em Azure App Service fica vazio; a origem é controlada pela aplicação via `Cors__AllowedOrigins__N`

App settings importantes definidas pela infraestrutura:
- `CaseGenerator__FunctionBaseUrl`
- `CaseGeneratorStorage__AccountName`
- `CaseGeneratorStorage__BundlesContainer=bundles`
- `CaseGeneratorStorage__CasesContainer=cases`
- `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, etc.
- `JwtSettings__*`
- `APPLICATIONINSIGHTS_CONNECTION_STRING`

RBAC atual da API sobre o storage das Functions:
- **Storage Blob Data Reader**
- **Storage Queue Data Message Sender**

Isso existe para a API:
- ler bundles publicados pelas Functions
- enviar mensagens para a fila `forensic-requests`

## Functions layer (.NET 9 isolated)

Provisionada por [`../infrastructure/functions/main.bicep`](../infrastructure/functions/main.bicep):

- **Function App Plan (Linux)**: `casegen-funcplan-<env>`
  - `dev`: **Y1** (Consumption/Dynamic)
  - `prod`: **EP1** (Elastic Premium)
- **Function App**: `casegen-func-<env>`
- Runtime: `DOTNET-ISOLATED|9.0`
- Health check path: `/api/health`
- **Storage account**: `stca<env><unique>`
  - `dev`: `Standard_LRS`
  - `prod`: `Standard_GRS`

### Storage topology atual das Functions

**Blob containers criados pela infraestrutura:**
- `cases`
- `bundles`
- `case-context`
- `logs`

**Fila criada pela infraestrutura:**
- `forensic-requests`

**Uso atual no código:**
- `bundles/<caseId>/case.json` + `bundles/<caseId>/assets/*` → bundle que o backend/site consome
- `jobs/<jobId>/status.json` → progresso do job v2
- `cases/<caseId>/case.json` + `cases/<caseId>/assets/*` → artefatos locais/espelhados
- `forensic-requests` → fila para solicitações forenses emitidas pela API

> Observação: a infraestrutura cria private endpoints para **blob, queue e table** do storage account, mas hoje este conjunto de docs/código usa explicitamente blob e queue. Não há documentação de uso funcional de Table Storage nestes dois arquivos.

## Frontend layer

Provisionada por [`../infrastructure/frontend/main.bicep`](../infrastructure/frontend/main.bicep):

- **Azure Static Web App**: `casezero-web-<env>`
  - `dev`: **Free**
  - `prod`: **Standard**

App settings de SWA definidos no Bicep:
- `VITE_API_BASE_URL=${backendApiUrl}/api`
- `VITE_APP_TITLE=CaseZero`
- `VITE_ENV=<env>`
- `NODE_ENV=development|production`

> Importante: o frontend atual lê `import.meta.env.VITE_API_URL` no código. Por isso o workflow `cd-dev.yml` continua precisando do secret **`VITE_API_URL`** no build, mesmo que a SWA também receba `VITE_API_BASE_URL` via infraestrutura.

## Networking privado

Com `enablePrivateNetworking=true` por padrão em `infrastructure/main.bicep`, a topologia atual inclui:

- VNet **canadacentral** para storage/private endpoints/functions
- VNet **canadaeast** para integração regional da API
- Peering entre as duas VNets
- Private DNS zones para:
  - `privatelink.blob.core.windows.net`
  - `privatelink.queue.core.windows.net`
  - `privatelink.table.core.windows.net`
  - `privatelink.database.windows.net`
  - `privatelink.vaultcore.azure.net`
- Private endpoints para:
  - Storage account (`blob`, `queue`, `table`)
  - Key Vault
  - SQL Server (somente quando SQL está habilitado)

## Ambientes e parâmetros reais

## DEV (`parameters.dev.json`)

- `environment = dev`
- `location = canadacentral`
- `apiLocation = canadaeast`
- `namePrefix = casezero`
- `enableSqlDatabase = true` *(arquivo de parâmetros; o workflow de deploy pode sobrescrever isso com `deploy_sql_database`)*
- `enableMonitoring = true`
- `repositoryUrl = https://github.com/lcarli/CaseZero-Alternative`
- `branchName = develop`

## PROD (`parameters.prod.json`)

- `environment = prod`
- `location = canadaeast`
- `apiLocation` não é sobrescrito, então segue `location`
- `namePrefix = casezero`
- `enableSqlDatabase = false` *(arquivo de parâmetros; o workflow de deploy pode sobrescrever isso com `deploy_sql_database`)*
- `enableMonitoring = true`
- `repositoryUrl = https://github.com/lcarli/CaseZero-Alternative`
- `branchName = main`

## Workflows reais

## 1. Infraestrutura (`infrastructure-3tier.yml`)

Trigger: **`workflow_dispatch` apenas**.

Inputs reais:
- `environment`: `dev | prod`
- `action`: `validate | deploy | destroy`
- `deploy_sql_database`: boolean (default `false`)
- `confirm_destroy`: obrigatório com valor `CONFIRM` para destroy

Fluxo real:
1. valida `confirm_destroy` quando `action=destroy`
2. faz `az bicep build --file infrastructure/main.bicep`
3. roda `az deployment sub validate`
4. roda `az deployment sub what-if` quando `action != validate`
5. roda `az deployment sub create` quando `action = deploy`, passando `enableSqlDatabase=${deploy_sql_database}`
6. salva `deployment-output.json`
7. extrai URLs de saída:
   - `staticWebAppUrl`
   - `apiAppServiceUrl`
   - `functionAppUrl`
8. quando `action = destroy`, deleta resource groups com:
   - tag `Environment == <env>`
   - nome iniciado por `casezero`

## 2. Build/Test/Deploy DEV (`cd-dev.yml`)

Triggers reais:
- `pull_request` para `main`
- `push` para `develop` e `main`
- `workflow_dispatch`

### O que esse workflow faz hoje

**Build/Test job**
- instala **.NET 8 + .NET 9**
- instala **Node 20**
- `npm ci` no frontend
- `npm run build` no frontend com `VITE_API_URL`
- `dotnet restore ./CaseZero-Alternative.sln`
- `dotnet build ./CaseZero-Alternative.sln --configuration Release`
- testes:
  - `backend/CaseZeroApi.Tests`
  - `backend/CaseZeroApi.IntegrationTests`
  - `functions/CaseGen.Functions.Tests`
  - `frontend` (`npm run test:run`)
- valida todos os `cases/*/case.json` com `ajv`
- publica artefatos:
  - backend
  - frontend
  - functions

**Deploy job** *(somente fora de pull request)*
- deploy do backend para `casezero-api-dev`
- deploy das Functions para `casegen-func-dev`
- deploy do frontend para a Static Web App usando `AZURE_STATIC_WEB_APPS_API_TOKEN_DEV`
- environment do GitHub Actions: `development`

> `pull_request` faz build/test, mas **não** publica em Azure.

## Comandos manuais equivalentes à infraestrutura

Não há script de deploy Azure em `scripts\` hoje. Se precisar rodar manualmente, use os mesmos comandos do workflow. Para ficar idêntico ao workflow, trate `enableSqlDatabase` como override explícito e não apenas como valor do arquivo de parâmetros.

### Validar DEV

```powershell
az deployment sub validate `
  --location canadacentral `
  --template-file infrastructure/main.bicep `
  --parameters @infrastructure/parameters.dev.json `
  --parameters sqlAdminLogin='<SQL_ADMIN_LOGIN>' `
  --parameters sqlAdminPassword='<SQL_ADMIN_PASSWORD>'
```

### What-if DEV

```powershell
az deployment sub what-if `
  --location canadacentral `
  --template-file infrastructure/main.bicep `
  --parameters @infrastructure/parameters.dev.json `
  --parameters sqlAdminLogin='<SQL_ADMIN_LOGIN>' `
  --parameters sqlAdminPassword='<SQL_ADMIN_PASSWORD>' `
  --result-format FullResourcePayloads
```

### Deploy DEV

```powershell
$deploymentName = "casezero-dev-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
az deployment sub create `
  --name $deploymentName `
  --location canadacentral `
  --template-file infrastructure/main.bicep `
  --parameters @infrastructure/parameters.dev.json `
  --parameters enableSqlDatabase=<true|false> `
  --parameters sqlAdminLogin='<SQL_ADMIN_LOGIN>' `
  --parameters sqlAdminPassword='<SQL_ADMIN_PASSWORD>'
```

### Deploy PROD

```powershell
$deploymentName = "casezero-prod-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
az deployment sub create `
  --name $deploymentName `
  --location canadacentral `
  --template-file infrastructure/main.bicep `
  --parameters @infrastructure/parameters.prod.json `
  --parameters enableSqlDatabase=<true|false> `
  --parameters sqlAdminLogin='<SQL_ADMIN_LOGIN>' `
  --parameters sqlAdminPassword='<SQL_ADMIN_PASSWORD>'
```

> O `--location canadacentral` acima é a **location do deployment de subscription**, exatamente como no workflow; os recursos seguem `location`/`apiLocation` definidos nos arquivos de parâmetros.

## Verificação pós-deploy

## Infraestrutura

```powershell
az group list --query "[?starts_with(name, 'casezero')].name" -o table
```

## URLs esperadas

Após `az deployment sub create`, confira os outputs:
- `staticWebAppUrl`
- `apiAppServiceUrl`
- `functionAppUrl`

## Pontos de atenção atuais

- O workflow de deploy da aplicação é **DEV-only** neste repositório hoje.
- `docs/**` está em `paths-ignore` no `cd-dev.yml`; alterar documentação **não dispara** deploy DEV.
- `backend/CaseZeroApi` continua em **.NET 8**.
- `functions/CaseGen.Functions` e `functions/CaseGen.Functions.Tests` continuam em **.NET 9**.
- `parameters.prod.json` atualmente deixa **SQL desabilitado**; não assuma SQL em produção sem alterar parâmetros/execução.
- O frontend atual depende de **`VITE_API_URL` no build**.
