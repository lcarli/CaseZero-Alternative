# Status & próximas tarefas — `main` (post v2 launch)

> Snapshot tirado em **2026-05-15**. `feat/site-contract-v2` mergeada via PRs **#119–#123** e o workflow `cd-dev` ficou **verde** na run `25915855197`. Stack rodando em dev (`casezero-api-dev`, `casegen-func-dev`, `casezero-web-dev` SWA, `casezero-sql-dev`). Sem ambiente de produção.

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
- **`CaseV2BlobPublisher`** (PR #123): após geração, espelha `case.json` + `assets/*` pro container `bundles` do storage que o site lê. Usa `BlobServiceClientFactory` — MI em Azure, conn string em Azurite, no-op quando nenhum dos dois está configurado.

### CI/CD — workflows GitHub Actions (estado atual)
- **`cd-prod.yml` foi DELETADO** (não existe ambiente PROD). Único workflow é **`cd-dev.yml`**, que dispara em push para `develop` OU `main` e deploya nos RGs `casezero-*-dev-rg`.
- Removido step de **EF migrations** — `Program.cs:329` (`context.Database.Migrate()`) aplica no startup do webapp via MI.
- Functions deploy via **`Azure/functions-action@v1` com RBAC** (sem publish profile). Compatível com Flex Consumption + identity-based deployment storage.
- `actions/upload-artifact@v4` configurado com `include-hidden-files: true` — pasta `.azurefunctions/` precisa estar no zip pro Flex Consumption.
- Substituído `actions/create-release@v1` (deprecated) por `softprops/action-gh-release@v2`.
- Static Web App deploy via `Azure/static-web-apps-deploy@v1` com `app_location: ./artifacts/frontend`.
- `npm audit` e `dotnet list package --vulnerable` em `continue-on-error: true`.
- `paths-ignore` para `**.md`, `docs/**`, `GDD*/**`, `backlog/**`.
- Node 18 → 20.
- `IntegrationTests` em `continue-on-error: true` (legacy v1 dependencies pendentes de portar — ver TASK B).

### Migração para Managed Identity end-to-end (esta sessão)
- **Function App** `casegen-func-dev` migrada de Consumption Y1 para **Flex Consumption (FC1)**:
  - System-assigned MI com Storage Blob Data Owner/Contributor + Queue/Table Contributor no `stcadevcabtlmvw4g`.
  - Deployment storage com `--deployment-storage-auth-type SystemAssignedIdentity` (sem `WEBSITE_CONTENTAZUREFILECONNECTIONSTRING`).
  - Key Vault Secrets User no `kv-ca-dev-oeq4agkmf6k4k` (modo RBAC).
  - `BlobServiceClientFactory.cs` centraliza `BlobServiceClient`: prefere `CaseGeneratorStorage:AccountName` + `DefaultAzureCredential`, cai pra conn string só em Azurite.
  - `StorageService`, `CaseLoggingService` e `CaseV2BlobPublisher` usam o factory.
- **Backend** `casezero-api-dev`:
  - System-assigned MI (`4f9fbbba-...`) adicionado como **`db_owner`** em `casezero-db`.
  - Connection string trocada pra `Authentication=Active Directory Default` (sem senha).
  - SQL Server permanece **AAD-only** (sem mudar do estado anterior).
- **SQL** `casezero-sql-dev`: `publicNetworkAccess=Enabled` + firewall rule `AllowAzureServices` (tráfego pela backbone Azure). Sem private endpoint — F1 Free não suporta VNet Integration e Canada Central não tem quota pra B1+.
- **Storage** `stcadevcabtlmvw4g`: `publicNetworkAccess=Enabled`, `defaultAction=Allow`, `bypass=AzureServices`, `allowSharedKeyAccess=false`. ⚠️ **Fechar a rede pública desse storage quebra o deploy do Functions** — o Kudu Legion precisa fazer upload pelo endpoint público.

### Conteúdo entregue
- `cases/case_001/case.json` — The Missing Heir (Detective), migrado pra v2.
- `cases/case_rookie_hardened/case.json` — The Diner Break-In (Rookie), gerado pelo pipeline reforçado. 9 assets reais (5 PDFs com templates + 3 fotos via gpt-image-1.5 + 1 audio sidecar). Solver score 1.0.

---

## 📋 Tarefas pendentes

### TASK A — Configurar GitHub Environments + Secrets ✅ **CONCLUÍDO (dev)**

- `AZURE_CREDENTIALS_DEV` rotacionado pro SP `casezero-ci-dev-v2b` (`625a2fab-37d6-4811-b044-dadb7971cdab`), Contributor na subscription + Storage Blob Data Contributor no `stcadevcabtlmvw4g` (pra Flex Consumption deploy).
- `AZURE_STATIC_WEB_APPS_API_TOKEN_DEV` configurado.
- `SQL_ADMIN_PASSWORD` mantido — **não usado pelo workflow** (auth migrada pra MI), mas pode ser útil pra acesso emergencial via firewall rule + IP temporário.
- Environments criados: `development` (em uso), `production-approval` (idle — sem cd-prod).
- ❌ Secrets de PROD (`AZURE_CREDENTIALS_PROD`, `AZURE_STATIC_WEB_APPS_API_TOKEN_PROD`, `SQL_ADMIN_PASSWORD_PROD`) **não criados** — sem ambiente PROD provisionado. Quando criar, regenerar e adicionar `cd-prod.yml` baseado no `cd-dev.yml` atual.

### TASK B — Portar `CaseZeroApi.IntegrationTests` para v2

`cd-dev.yml` está com esses testes em `continue-on-error: true`. Eles referenciam o legacy `IRulesEngineService.EvaluateForensicRuleAsync`/`ApplyRevealEmailActionAsync`/etc — esses métodos ainda existem mas a forma do `case.json` v1 esperada no input não bate mais com o v2 que está em `cases/`. Substituir os fixtures + reescrever os 3-4 testes que dependem dessa superfície (estão em `AuditLogTests.cs`, `SecurityIntegrationTests.cs`).

### TASK C — Job assíncrono para `POST /api/cases/v2/generate` ✅ **CONCLUÍDO**

Implementado na branch `feat/task-c-async-generate` (PR pendente):
- `POST /api/cases/v2/generate` retorna `202 Accepted + jobId` (Durable Functions orchestration `CaseV2GenerationOrchestrator`).
- `GET /api/cases/v2/jobs/{jobId}` devolve status (queued | running com `currentPhase` | done | failed) + payload final, compondo runtime status do Durable com per-phase blob status do `JobPhaseReporter`.
- Singleton best-effort: rejeita 409 se outra geração estiver Pending/Running (query por `InstanceIdPrefix` + nome do orquestrador).
- Smoke local: gerou `case_smoke_v2` em ~4 min, 0 erros de validação, 11 blobs publicados em `bundles/` (Azurite).

### TASK D — `CaseV1*` cleanup definitivo

Remover `CaseV1StorageService`, `CaseV1SanitizerService`, `CasesV1Controller` (`/api/cases/v1`) e os métodos legados em `IRulesEngineService` quando a Task B liberar (integration tests não dependerão mais deles). O frontend `FileViewer` ainda usa `casesV1Api.getAssetUrl` — substituir pela rota v2 nova quando o endpoint de asset stream estiver mapeado em `/api/cases/{id}/assets/{aid}` no `AssetsController` (já existe `download`, só falta apontar lá).

### TASK E — Refine loop quando red-team retorna `reject`

Quando `RedTeamTask` emite verdict `reject` com finding `high`, dispara um task novo (`RefineTask`) que recebe o caso + os findings e re-gera **só** as partes problemáticas (rules / solution). Hoje o reject já é reportado mas só serve de aviso — não corrige.

### TASK F — `functions/CaseGen.Functions` precisa emitir v2 nativo

Hoje só o **novo** endpoint `POST /api/cases/v2/generate` gera v2. O pipeline durável antigo (`PlanStep` / `ExpandStep` / `DesignStep` / etc) ainda emite v1. Quando estiver estável, substituir a saída do pipeline durável para usar `CaseV2GeneratorService` ou aposentá-lo.

### TASK G — Hardening de infra dev (não-bloqueante)

Trade-offs aceitos hoje em dev pra manter custo baixo / contornar quota:
- **F1 (Free) no plano do backend** — 60 min CPU/dia, sem VNet Integration. Sem quota pra B1+ em Canada Central.
- **SQL com `publicNetworkAccess=Enabled` + AllowAzureServices** — tráfego pela backbone Azure mas tecnicamente público.
- **Storage com `publicNetworkAccess=Enabled` + defaultAction=Allow** — ⚠️ necessário pro deploy do Functions funcionar via Kudu Legion. Fechar quebra o deploy.
- **Identity Functions antiga (`85ad3c95-...`) ainda tem role assignments orfãs** no `stcadevcabtlmvw4g` (limpar é cosmético).

Quando virar produção:
1. Solicitar quota App Service B1+ em CC (ou outra região onde toda a stack se mova junta).
2. Subir plano pra B1/P1v3, criar VNet + 2 subnets (webapp delegada / PE).
3. Criar Private Endpoint pro SQL + Private DNS Zone `privatelink.database.windows.net`.
4. Criar Private Endpoint pro storage (`blob`, `queue`, `table` + DNS zones).
5. Configurar GitHub runner self-hosted no VNet (ou usar Azure Container Apps Jobs como CI) pra o deploy alcançar os PEs.
6. Fechar `publicNetworkAccess` em SQL e storage.

### TASK H — Limpar artefatos cosméticos

- `revert-99-copilot/fix-98` no remote — branch órfã do passado.
- 53 branches `copilot/*` já deletadas nesta sessão.
- Eventualmente arquivar `feat/site-contract-v2` (já mergeada via #119/#120/#121/#122/#123).

---

## 🧭 Estado atual / como tocar daqui

1. **Trabalho regular**: branch feature → PR pra `main` → merge dispara `cd-dev.yml` → deploy automático nos RGs dev.
2. **Deploy manual**: `gh workflow run "Deploy to DEV Environment" --ref main`.
3. **Migrations**: já automáticas no startup do `casezero-api-dev` via `Database.Migrate()` (auth MI).
4. **Gerador local**: `./scripts/run-functions.ps1` (Windows) ou `.sh` (Mac/Linux) — Azurite + func host local.
5. **Smoke pós-deploy**: `https://casezero-api-dev.azurewebsites.net/swagger` (200) e a SWA em `https://gentle-ground-03dca4110.3.azurestaticapps.net/`.

> Próximo foco recomendado: **TASK E** (refine loop) — primeira tentativa do smoke da TASK C falhou validação porque o LLM emitiu IDs `asset_xxx` em vez de `asset.xxx`; o refine loop converteria isso de "abortar pipeline" em "regenerar só a parte problemática".
