# Developer Guide - CaseZero System

## Overview

Este guia reflete o estado atual do repositório `CaseZero-Alternative`.
Ele cobre o fluxo real de desenvolvimento para os três componentes ativos:

- `backend\CaseZeroApi` - ASP.NET Core Web API (`net8.0`)
- `functions\CaseGen.Functions` - Azure Functions isolated worker (`net9.0`)
- `frontend` - React 19 + TypeScript 5.8 + Vite 7

## Stack e pré-requisitos

| Componente | Versão atual | Evidência |
|---|---|---|
| Backend API | .NET 8 (`net8.0`) | `backend\CaseZeroApi\CaseZeroApi.csproj` |
| Functions | .NET 9 (`net9.0`) | `functions\CaseGen.Functions\CaseGen.Functions.csproj` |
| Functions Tests | .NET 9 (`net9.0`) | `functions\CaseGen.Functions.Tests\CaseGen.Functions.Tests.csproj` |
| Frontend | React 19.1 | `frontend\package.json` |
| Frontend build | TypeScript `~5.8.3`, Vite `^7.0.4` | `frontend\package.json` |
| CI Node.js | Node 20 | `.github\workflows\cd-dev.yml` |

### Ferramentas recomendadas

- Git 2.x+
- Node.js 20+
- npm 10+
- .NET SDK 8 e .NET SDK 9 instalados lado a lado
- Azure Functions Core Tools v4 e Azurite para trabalho local com Functions
  - `scripts\run-functions.ps1` instala ambos automaticamente se necessário

> Importante: `functions\CaseGen.Functions` e `functions\CaseGen.Functions.Tests` devem continuar em `.NET SDK 9`.

## Estrutura atual do repositório

```text
CaseZero-Alternative\
├── backend\
│   ├── CaseZeroApi\
│   ├── CaseZeroApi.Tests\
│   └── CaseZeroApi.IntegrationTests\
├── frontend\
├── functions\
│   ├── CaseGen.Functions\
│   └── CaseGen.Functions.Tests\
├── cases\
├── schemas\
├── scripts\
└── docs\
```

### Observação sobre a solution

A solution raiz `CaseZero-Alternative.sln` inclui:

- `backend\CaseZeroApi`
- `backend\CaseZeroApi.Tests`
- `backend\CaseZeroApi.IntegrationTests`
- `functions\CaseGen.Functions.Tests`

O projeto `functions\CaseGen.Functions` não aparece diretamente na solution, mas é compilado de forma transitiva pelos testes de Functions.

## Setup inicial

### 1. Restore e dependências

```powershell
git clone https://github.com/lcarli/CaseZero-Alternative.git
Set-Location F:\repos\CaseZero-Alternative

dotnet restore .\CaseZero-Alternative.sln
dotnet restore .\functions\CaseGen.Functions\CaseGen.Functions.csproj

Set-Location .\frontend
npm ci
Set-Location ..
```

### 2. Configuração do backend

O backend sempre carrega `appsettings.json` e também aceita um override local opcional em:

- `backend\CaseZeroApi\appsettings.Local.json`

Use esse arquivo local para não alterar o `appsettings.json` compartilhado.

Exemplo mínimo com placeholders seguros:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=tcp:<YOUR_SERVER>.database.windows.net,1433;Database=<YOUR_DATABASE>;User ID=<YOUR_USER>;Password=<YOUR_PASSWORD>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  },
  "JwtSettings": {
    "SecretKey": "<32+_CHAR_SECRET>",
    "Issuer": "CaseZeroApi",
    "Audience": "CaseZeroFrontend"
  },
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:5173",
      "https://localhost:5173"
    ]
  },
  "CaseGenerator": {
    "FunctionBaseUrl": "http://localhost:7071",
    "FunctionKey": "<OPTIONAL_FUNCTION_KEY>"
  },
  "CaseGeneratorStorage": {
    "ConnectionString": "UseDevelopmentStorage=true",
    "BundlesContainer": "bundles"
  }
}
```

### 3. SQLite local opcional

Embora o `appsettings.json` esteja orientado a Azure SQL, o `Program.cs` atual aceita SQLite local quando:

- `UseSqlite` = `true`, ou
- `ConnectionStrings:DefaultConnection` começa com `Data Source=`

Exemplo:

```json
{
  "UseSqlite": true,
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=casezero-dev.db"
  }
}
```

### 4. Observação importante sobre EF Core CLI

O factory de design-time (`Data\ApplicationDbContextFactory.cs`) lê `ConnectionStrings__DefaultConnection` do ambiente e, se ela não existir, cai para LocalDB.

Se você for executar migrations com `dotnet ef`, defina explicitamente a variável antes do comando. Exemplo:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Server=tcp:<YOUR_SERVER>.database.windows.net,1433;Database=<YOUR_DATABASE>;User ID=<YOUR_USER>;Password=<YOUR_PASSWORD>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
dotnet ef database update --project .\backend\CaseZeroApi\CaseZeroApi.csproj
```

Se preferir SQLite, ajuste a connection string correspondente antes de usar `dotnet ef`.

## Variáveis de ambiente e arquivos de configuração

### Backend

Chaves relevantes usadas hoje:

- `ConnectionStrings:DefaultConnection`
- `UseSqlite`
- `JwtSettings:SecretKey`
- `JwtSettings:Issuer`
- `JwtSettings:Audience`
- `Cors:AllowedOrigins`
- `CaseGenerator:FunctionBaseUrl`
- `CaseGenerator:FunctionKey`
- `CaseGeneratorStorage:AccountName`
- `CaseGeneratorStorage:ConnectionString`
- `CaseGeneratorStorage:BundlesContainer`
- `SeedUsers:LucasAdminEmail`
- `SeedUsers:LucasAdminPassword`

`SeedUsers:LucasAdminPassword` só é obrigatório ao iniciar a API com `--provision-admin-only`.

### Frontend

Arquivos atuais:

- `frontend\.env.development`
- `frontend\.env.production`

Variável usada pelo frontend:

```env
VITE_API_URL=http://localhost:5001/api
```

Notas:

- Inclua o sufixo `/api` em `VITE_API_URL`.
- Não confie nos fallbacks hardcoded do frontend; mantenha o `.env.development` correto.
- O backend aceita CORS local para `http://localhost:5173` e `https://localhost:5173` por padrão.

### Functions

O projeto `functions\CaseGen.Functions` usa `local.settings.json` e `EnvironmentVariables`.

Chaves locais usadas hoje (nomes apenas):

- `AzureWebJobsStorage`
- `FUNCTIONS_WORKER_RUNTIME`
- `CaseGeneratorStorage__AccountName`
- `CaseGeneratorStorage__ConnectionString`
- `CaseGeneratorStorage__BundlesContainer`
- `LLM__UseAzureFoundry`
- `AzureFoundry__Endpoint`
- `AzureFoundry__ModelName`
- `AzureFoundry__ImageDeploymentName`
- `AzureFoundry__ApiKey`
- `ASPNETCORE_ENVIRONMENT`

Exemplo seguro:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "CaseGeneratorStorage__ConnectionString": "UseDevelopmentStorage=true",
    "CaseGeneratorStorage__BundlesContainer": "bundles",
    "LLM__UseAzureFoundry": "true",
    "AzureFoundry__Endpoint": "https://<YOUR_RESOURCE>.openai.azure.com/openai/v1",
    "AzureFoundry__ModelName": "<YOUR_TEXT_MODEL>",
    "AzureFoundry__ImageDeploymentName": "<YOUR_IMAGE_MODEL>",
    "AzureFoundry__ApiKey": "<YOUR_API_KEY>",
    "ASPNETCORE_ENVIRONMENT": "Development"
  }
}
```

## Como subir o ambiente local

### Opção recomendada: 3 terminais

#### Terminal 1 - Backend API

```powershell
Set-Location F:\repos\CaseZero-Alternative
dotnet run --project .\backend\CaseZeroApi\CaseZeroApi.csproj --urls http://localhost:5001
```

- API base esperada pelo frontend: `http://localhost:5001/api`
- Swagger disponível apenas em `Development`: `http://localhost:5001/swagger`
- SignalR hub: `http://localhost:5001/hubs/forensics`

#### Terminal 2 - Frontend

```powershell
Set-Location F:\repos\CaseZero-Alternative\frontend
npm run dev
```

- Vite roda em `http://localhost:5173` por padrão.

#### Terminal 3 - Functions

```powershell
Set-Location F:\repos\CaseZero-Alternative
.\scripts\run-functions.ps1
```

Esse script:

- verifica `.NET 9`
- exige `Node 20+`
- instala `azure-functions-core-tools@4` e `azurite` se faltarem
- sobe o Azurite localmente
- copia `schemas\case.schema.json` para `functions\CaseGen.Functions\Schemas\case.v2.schema.json`
- executa `dotnet build` no projeto Functions
- inicia o host em `http://localhost:7071`

### Fluxo entre os serviços

- O frontend fala com a API em `VITE_API_URL`
- A API expõe autenticação, casos, inbox, notas, perícia, sessão e proxy de geração
- O SignalR usa `/hubs/forensics`
- A API usa `CaseGenerator:FunctionBaseUrl` para chamar as Functions
- As Functions expõem os endpoints reais de geração v2 em `http://localhost:7071/api/cases/v2/*`

Se `CaseGenerator:FunctionBaseUrl` não estiver configurado, `api/casegeneration/*` retorna `503`.

## Endpoints e rotas relevantes

### Backend API

Rotas principais confirmadas em `backend\CaseZeroApi\Controllers`:

- `POST /api/auth/register`
- `POST /api/auth/login`
- `GET /api/auth/me`
- `POST /api/auth/verify-email`
- `POST /api/auth/resend-verification`
- `GET /api/profile/stats`
- `GET /api/inbox`
- `GET /api/inbox/unread-count`
- `POST /api/inbox/{id}/read`
- `GET /api/cases/dashboard`
- `GET /api/cases`
- `GET /api/cases/{caseId}`
- `GET /api/cases/{caseId}/raw` (Admin)
- `POST /api/cases/{caseId}/submit`
- `GET /api/cases/{caseId}/assets`
- `GET /api/cases/{caseId}/assets/{assetId}/download`
- `GET /api/cases/{caseId}/emails`
- `POST /api/cases/{caseId}/emails/{emailId}/open`
- `GET /api/cases/{caseId}/emails/{emailId}`
- `POST /api/cases/{caseId}/emails/{emailId}/attachments/{assetId}/download`
- `POST /api/cases/{caseId}/assets/{assetId}/view`
- `POST /api/cases/{caseId}/suspects/{suspectId}/view`
- `POST /api/cases/{caseId}/time`
- `POST /api/casesession/start`
- `POST /api/casesession/end/{caseId}`
- `GET /api/cases/{caseId}/session`
- `POST /api/cases/{caseId}/resume`
- `DELETE /api/casesession/reset-visibility/{caseId}`
- `GET /api/forensicrequest/{caseId}`
- `POST /api/forensicrequest`
- `GET /api/notes/case/{caseId}`
- `POST /api/notes`
- `POST /api/casegeneration/generate` (Admin; proxy para Functions)
- `GET /api/casegeneration/jobs/{jobId}` (Admin; proxy para Functions)

### Functions

Rotas HTTP confirmadas em `functions\\CaseGen.Functions\\Functions\\CaseV2\\CaseV2GenerationOrchestrator.cs`:

- `POST /api/cases/v2/generate`
- `GET /api/cases/v2/jobs/{jobId}`

Características do host:

- Azure Functions isolated worker
- Durable Functions habilitado
- `hubName`: `CaseGenHub`
- timeout configurado: `01:00:00`

## Build, testes e validação

### Comandos alinhados ao CI

#### Build local

```powershell
Set-Location F:\repos\CaseZero-Alternative

dotnet restore .\CaseZero-Alternative.sln
dotnet build .\CaseZero-Alternative.sln --no-restore --configuration Release

Set-Location .\frontend
npm ci
npm run build
Set-Location ..

# Publish local quando necessário
# dotnet publish .\backend\CaseZeroApi\CaseZeroApi.csproj -c Release -o .\backend\CaseZeroApi\publish
# dotnet publish .\functions\CaseGen.Functions\CaseGen.Functions.csproj -c Release -o .\functions\CaseGen.Functions\publish
```

#### Testes backend

```powershell
dotnet test .\backend\CaseZeroApi.Tests\CaseZeroApi.Tests.csproj --configuration Release --no-build --verbosity normal
dotnet test .\backend\CaseZeroApi.IntegrationTests\CaseZeroApi.IntegrationTests.csproj --configuration Release --no-build --verbosity normal
```

#### Testes Functions

```powershell
dotnet test .\functions\CaseGen.Functions.Tests\CaseGen.Functions.Tests.csproj --configuration Release --no-build --verbosity normal
```

#### Testes frontend

```powershell
Set-Location .\frontend
npm run test:run
Set-Location ..
```

#### Lint frontend

```powershell
Set-Location .\frontend
npm run lint
Set-Location ..
```

> Observação: o workflow atual executa build e testes do frontend, mas não roda `npm run lint`.

### Scripts úteis

#### Validar artefatos v2

```powershell
.\scripts\validate-casev2.ps1 -Mode Goldens
.\scripts\validate-casev2.ps1 -Mode Artifact -CaseDirectory .\cases\<CASE_DIRECTORY>
.\scripts\validate-casev2.ps1 -Mode Generate -Difficulty Rookie -Theme 'office theft' -Seed 1 -Language en-US
.\scripts\validate-casev2.ps1 -Mode Full
```

`validate-casev2.ps1` usa `functions\CaseGen.Functions.Tests\CaseGen.Functions.Tests.csproj` e também pode chamar o host local em `http://localhost:7071`.

#### Soak test v2

```powershell
.\scripts\run-casev2-soak.ps1 -Language en-US -CasesPerDifficulty 3
```

#### Functions em Mac/Linux

```bash
./scripts/run-functions.sh
```

## Regras de desenvolvimento relevantes hoje

### Internacionalização

A UI mantém traduções ativas para quatro idiomas:

- `pt-BR`
- `en-US`
- `fr-FR`
- `es-ES`

Ao adicionar ou alterar texto visível na interface, atualize os quatro arquivos em `frontend\src\locales\`.

### Autenticação e acesso

- Quase todos os controllers da API exigem autenticação JWT.
- O hub SignalR `/hubs/forensics` também exige autenticação.
- `api/casegeneration/*` é restrito a `Admin`.

### Swagger

- Swagger é habilitado apenas em `Development`.
- Se você iniciar a API fora desse ambiente, `/swagger` não aparecerá.

## CI/CD atual

O workflow `.github\workflows\cd-dev.yml` hoje faz:

1. setup de `.NET 8`, `.NET 9` e `Node 20`
2. `npm ci` + `npm run build` no frontend
3. `dotnet restore` e `dotnet build` da solution
4. testes backend unitários e integração
5. testes de Functions
6. `npm run test:run` no frontend
7. validação AJV de `cases\*\case.json` contra `schemas\case.schema.json`
8. publish do backend e das Functions

Ao atualizar este guia, mantenha os comandos locais coerentes com esse workflow.

## Troubleshooting

### API falha na inicialização com erro de connection string

Causa comum:

- `ConnectionStrings:DefaultConnection` ausente, ou
- valores placeholder ainda presentes (`your-server`, `your-username`)

Correção:

- defina uma connection string real em `appsettings.Local.json`, ou
- use SQLite local com `UseSqlite=true` e `Data Source=...`

### `POST /api/casegeneration/generate` retorna 503

Causa comum:

- `CaseGenerator:FunctionBaseUrl` não configurado no backend, ou
- host Functions não está ativo em `http://localhost:7071`

Correção:

- suba `scripts\run-functions.ps1`
- configure `CaseGenerator:FunctionBaseUrl` para `http://localhost:7071`

### Frontend abre mas chamadas falham

Verifique:

- `frontend\.env.development` contém `VITE_API_URL=http://localhost:5001/api`
- a API foi iniciada com `--urls http://localhost:5001`
- o backend permite `http://localhost:5173` em CORS

### Problemas com Functions locais

Verifique:

- `.NET 9 SDK` instalado
- `Node 20+` instalado
- Azurite em execução
- `local.settings.json` com chaves obrigatórias
- `schemas\case.schema.json` copiado para `functions\CaseGen.Functions\Schemas\case.v2.schema.json` (o script já faz isso)

## Links Markdown locais

Este arquivo atualmente não depende de links Markdown relativos para outros documentos do repositório.
