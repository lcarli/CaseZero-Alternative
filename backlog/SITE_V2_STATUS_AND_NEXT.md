# Status & próximas tarefas — branch `feat/site-contract-v2`

> Snapshot tirado em **2026-05-14**. Branch acumula **45 commits** acima de `V3-2026-newAI`. Working tree limpo, builds verdes, smoke ao vivo passou (site + gerador + caso jogável).

---

## ✅ Concluído nesta branch

### Fase 1-7 do plano `SITE_V2_REWRITE_PLAN.md` — site v2 completo
- **Contrato canônico v2** (`docs/CASE_JSON_V2_SPEC.md`, `schemas/case.schema.json`).
- **Hard cut** de v0/v1: removidos `CaseObject*`, `CaseGenerationService` no site, types/engine/converter no frontend, docs antigos.
- **Backend v2**: `Models/CaseV2/*`, `CaseV2StorageService` + filesystem fallback, `CaseV2SanitizerService`, `RulesEngineService.EvaluateAndApplyAsync` (7 trigger types × 7 action types), `SolutionService` + `POST /api/cases/{id}/submit`, `ForensicsBackgroundService` integrado com `forensicOutcomes` + synthetic emails de noFindings, `CaseTriggersController`. Migration consolidada `PhaseThreeV2Schema` (8 colunas em `CaseSession` + 2 em `CaseSubmission`).
- **Frontend v2-nativo**: `types/caseV2.ts`, engine novo, `CaseContext` reescrito, `services/api.ts` em `/api/cases`, `Desktop` integrado, `SubmitCase` estruturado, `DashboardPage` com stats + recent activity + cases-by-rank, `DesktopPage` default route corrigido.
- **i18n em 4 idiomas** (`en-US`, `pt-BR`, `es-ES`, `fr-FR`) para todas as strings novas.
- **Docs** atualizados: `GDD-BR/04+09`, `GDD/04+09`, `BACKEND_ARCHITECTURE.md`, `FRONTEND_ARCHITECTURE.md`, `API_COMPLETE.md`, `README.md`.
- **Tests**: `CaseV2SanitizerServiceTests`, `RulesEngineServiceTests`, `VisibilityServiceTests`, `SolutionServiceTests` (xUnit), `SubmitCase.test.tsx` (Vitest), `docs/MANUAL_SMOKE_v2.md`. Backend **60/60** · Frontend **25/25**.

### Bugs reais corrigidos durante o smoke ao vivo
- DI faltando para `ICaseV2StorageService` / `ICaseV2SanitizerService` / `ISolutionService`.
- Suporte a SQLite local (`appsettings.Local.json`, `EnsureCreated`) — não exige Azure SQL em dev.
- Dashboard estava devolvendo `id` em vez de `caseId`.
- `CasesController.GetCase` agora seeda `CaseSessionVisible{Assets,Emails}` + `CaseSession` na primeira chamada (honra `unlockMode`).
- Rota duplicada de `email open` entre `CaseTriggersController` e `EmailsController` removida; `EmailsController.OpenEmail` agora chama `RulesEngineService.EvaluateAndApplyAsync`.
- `FileViewer` agora trata `asset.type ∈ {photo, pdf, document, digital}` direto; fallback usa `uri`.
- `CaseV1StorageService.GetAssetAsync` ganhou filesystem fallback que lê `cases/<id>/assets/<slug>.*` direto do disco com content-type sniffado.
- `AssetRenderingService.RenderImageAsync` agora detecta magic-bytes (PNG/JPEG/GIF/WEBP) e salva com a extensão certa.
- Dashboard rank filter: `john.doe` (Rank=Rook) não vê mais casos de Detective.
- Listagem do dashboard reduzida de **23s → 0.12s** (skipa Azurite blob quando `CaseGenV2:UseBlobStorage=false`).
- Dashboard restaurado com stats grid + recent activity + cases-by-rank (perdido na simplificação inicial).

### Gerador de casos (`functions/CaseGen.Functions`, .NET 9)
- **Pipeline em 13 fases** com micro-tarefas paralelas:
  1. PlotOutline · 2. SuspectCards × N · 3. AssetPlan · 4. AssetCards + Timeline + Briefing (parallel) · 5. ForensicsPlan · 6. Outcomes + InitialEmails (parallel) · **7. MechanicalRules (det. C#)** · 8. SolutionSkeleton → Rules (sequential, com `reachable` flag) · 9. Questions + Explanation (parallel) · **10. ConsistencyValidator (det. C#)** · 11. Assemble + JSON-Schema · **12. RedTeam + Solver (parallel)** · 13. RenderAssets.
- **Continuidade narrativa** via `CaseDraft.ToSummaryJson()` que expõe ao próximo task todo o conteúdo já criado (motive/alibi/background, descrições, conclusões forenses, timeline ancorada em `incidentDate`/`openedAt`).
- **EvidenceDocumentRenderer** + **11 templates** (`CallLog`, `PosExport`, `PhoneDump`, `SensorLog`, `BrowserHistory`, `BankStatement`, `GpsTrack`, `FileListing`, `ChatExport`, `EmailExport`, `AccessLog`) que enriquecem o documento antes do QuestPDF renderizar.
- **Image generation** via `gpt-image-1.5` no projeto Foundry `agenttestlucas`.
- **Endpoint HTTP** `POST /api/cases/v2/generate` + scripts cross-platform (`scripts/run-functions.sh`/`.ps1`) + doc `docs/RUNNING_FUNCTIONS_LOCALLY.md`.

### CI/CD — workflows GitHub Actions
- `cd-prod.yml` (push `main`) e `cd-dev.yml` (push `develop`) revisados:
  - Removido step `validate_case.sh` (script inexistente) → substituído por `ajv-cli` validando todos os `cases/*/case.json` contra o schema v2.
  - Adicionado deploy das **Functions** (.NET 9) — setup-dotnet side-by-side, publish, `az functionapp deployment source config-zip`.
  - Adicionado step de **EF migrations** (`dotnet ef migrations script --idempotent` + `sqlcmd`).
  - **Health check ANTES do swap** (testa staging slot, swap só roda se passa).
  - Substituído `actions/create-release@v1` (deprecated) por `softprops/action-gh-release@v2`.
  - Static Web App deploy migrado pra `Azure/static-web-apps-deploy@v1` com `app_location: ./artifacts/frontend`.
  - `npm audit` e `dotnet list package --vulnerable` agora `continue-on-error: true`.
  - `paths-ignore` para `**.md`, `docs/**`, `GDD*/**`, `backlog/**`.
  - Node 18 → 20.
  - `await-approval` usa `environment: production-approval` (precisa setup no GitHub).
  - `IntegrationTests` em `continue-on-error: true` (legacy v1 dependencies pendentes de portar).

### Conteúdo entregue
- `cases/case_001/case.json` — The Missing Heir (Detective), migrado pra v2.
- `cases/case_rookie_hardened/case.json` — The Diner Break-In (Rookie), gerado pelo pipeline reforçado. 9 assets reais (5 PDFs com templates + 3 fotos via gpt-image-1.5 + 1 audio sidecar). Solver score 1.0.

---

## 📋 Tarefas pendentes

### TASK A — Configurar GitHub Environments + Secrets (alta prioridade)

**Onde:** outro computador com `az` logado e permissão de Owner no repo + Contributor nos resources Azure.

**O que fazer:**

1. **GitHub → Settings → Environments**
   - Criar environment `production-approval` com **Required reviewers** = Lucas (você mesmo). Salvar.
   - Criar environment `development` (sem required reviewer, pode deixar default).

2. **GitHub → Settings → Secrets and variables → Actions → New repository secret**

   Lista completa (a maioria pode já existir do setup anterior — checar e adicionar só os ausentes):

   | Secret | Como pegar |
   |--------|------------|
   | `AZURE_CREDENTIALS_DEV` | `az ad sp create-for-rbac --name casezero-ci-dev --role contributor --scopes /subscriptions/$(az account show --query id -o tsv) --sdk-auth` — JSON inteiro |
   | `AZURE_CREDENTIALS_PROD` | mesmo comando, nome `casezero-ci-prod` |
   | `AZURE_STATIC_WEB_APPS_API_TOKEN_DEV` | `az staticwebapp secrets list --name casezero-web-dev --resource-group casezero-web-dev-rg --query "properties.apiKey" -o tsv` |
   | `AZURE_STATIC_WEB_APPS_API_TOKEN_PROD` | `az staticwebapp secrets list --name casezero-web-prod --resource-group casezero-web-prod-rg --query "properties.apiKey" -o tsv` |
   | `SQL_ADMIN_PASSWORD` | senha atual do `casezero_admin` em `casezero-sql-dev` (ou reseta com `az sql server update --name casezero-sql-dev --resource-group casezero-db-dev-rg --admin-password 'NovaSenha!2026'`) |
   | `SQL_ADMIN_PASSWORD_PROD` | idem para `casezero-sql-prod` |

3. **Validar:** criar uma branch `develop` (se ainda não existir) a partir de `feat/site-contract-v2`, fazer push pra disparar o workflow `cd-dev.yml`. Quando passar, abrir PR → `main` e validar o `cd-prod.yml` (vai pausar no approval — você aprova).

4. **Verificar infraestrutura Azure existente:** se os Function Apps `casezero-functions-dev` / `casezero-functions-prod` ainda não existem, criar (Bicep em `infrastructure/Functions/` provavelmente cobre, ou rodar `infrastructure-3tier.yml`).

**Critérios de aceitação:**
- Push em `develop` → cd-dev verde, dev environment atualizado.
- Push em `main` → cd-prod pausa no approval, após aprovação completa o deploy de backend + functions + frontend + migrations.

### TASK B — Portar `CaseZeroApi.IntegrationTests` para v2

`cd-prod.yml` está com esses testes em `continue-on-error: true`. Eles referenciam o legacy `IRulesEngineService.EvaluateForensicRuleAsync`/`ApplyRevealEmailActionAsync`/etc — esses métodos ainda existem mas a forma do `case.json` v1 esperada no input não bate mais com o v2 que está em `cases/`. Substituir os fixtures + reescrever os 3-4 testes que dependem dessa superfície (estão em `AuditLogTests.cs`, `SecurityIntegrationTests.cs`).

### TASK C — Job assíncrono para `POST /api/cases/v2/generate`

A geração leva 8-12 minutos (LLM image + texto). HTTP timeout vira problema. Refatorar pra:
- `POST /api/cases/v2/generate` retornar `202 Accepted + jobId` imediatamente.
- Background worker (Functions queue) processa.
- `GET /api/cases/v2/jobs/{jobId}` devolve status (queued | running:phase | done | failed) + payload final.

### TASK D — `CaseV1*` cleanup definitivo

Remover `CaseV1StorageService`, `CaseV1SanitizerService`, `CasesV1Controller` (`/api/cases/v1`) e os métodos legados em `IRulesEngineService` quando a Task B liberar (integration tests não dependerão mais deles). O frontend `FileViewer` ainda usa `casesV1Api.getAssetUrl` — substituir pela rota v2 nova quando o endpoint de asset stream estiver mapeado em `/api/cases/{id}/assets/{aid}` no `AssetsController` (já existe `download`, só falta apontar lá).

### TASK E — Refine loop quando red-team retorna `reject`

Quando `RedTeamTask` emite verdict `reject` com finding `high`, dispara um task novo (`RefineTask`) que recebe o caso + os findings e re-gera **só** as partes problemáticas (rules / solution). Hoje o reject já é reportado mas só serve de aviso — não corrige.

### TASK F — `functions/CaseGen.Functions` precisa emitir v2 nativo

Hoje só o **novo** endpoint `POST /api/cases/v2/generate` gera v2. O pipeline durável antigo (`PlanStep` / `ExpandStep` / `DesignStep` / etc) ainda emite v1. Quando estiver estável, substituir a saída do pipeline durável para usar `CaseV2GeneratorService` ou aposentá-lo.

---

## 🧭 Para o próximo computador

1. `git clone` o repo, `git checkout feat/site-contract-v2`.
2. `./scripts/run-functions.sh` (Mac/Linux) ou `./scripts/run-functions.ps1` (Windows) — sobe Azurite + func host local pra geração.
3. **Executar TASK A** (acima) primeiro: GitHub Environments + Secrets.
4. Quando os secrets estiverem prontos, abrir um PR `feat/site-contract-v2` → `main` (depois de `develop`) pra validar o pipeline ponta a ponta.
5. Seguir as TASKs B-F na ordem de prioridade.

> **Não merge antes de TASK A**. O cd-prod vai falhar sem os secrets novos (`AZURE_STATIC_WEB_APPS_API_TOKEN_PROD`, `SQL_ADMIN_PASSWORD_PROD`).
