# 📋 CaseZero — Backlog

> Backlog ativo. Tarefas concluídas saem daqui (histórico vive nos commits e PRs).

---

## 🔥 Em aberto

### TASK 4 — Persistir configuração da Function App no Bicep + corrigir fallback de disco

Durante o TASK 3 (validar geração ponta-a-ponta em Azure dev) aplicamos uma
série de mudanças direto no Azure via `az` que **não** estão no IaC do repo.
Se alguém recriar o ambiente do zero hoje (`azd up` / `az deployment`), nada
funciona. Além disso, o código tem um fallback de caminho que é uma armadilha
para Flex Consumption Linux.

**Drift atual entre o que existe em Azure dev e o que está no Bicep:**

`infrastructure/functions/main.bicep` (Function App `casegen-func-dev`):
- Linhas 205/209/213 ainda declaram as chaves antigas erradas
  `AzureOpenAI__Endpoint/ApiKey/DeploymentName` — o código NÃO lê essas chaves.
- Faltando: `LLM__UseAzureFoundry=true`
- Faltando: `AzureFoundry__Endpoint`, `AzureFoundry__ApiKey`,
  `AzureFoundry__ModelName`, `AzureFoundry__ImageDeploymentName` (Key Vault refs)
- Faltando: `CaseGenV2__CasesBasePath=/tmp` (sem isso, geração falha com
  `Access to /home/site/wwwroot/cases is denied` em Flex Consumption Linux)
- Faltando: role assignment `Key Vault Secrets User` do MI da Function App
  sobre o Key Vault `kv-ca-dev-oeq4agkmf6k4k`

`infrastructure/api/main.bicep` (Web App `casezero-api-dev`):
- Faltando: `CaseGenerator__FunctionBaseUrl=https://<func-app>.azurewebsites.net`
- Faltando: `CaseGeneratorStorage__ConnectionString` apontando para a storage
  account `stcadevcabtlmvw4g`. Sem isso, `CaseV2StorageService` cai pra
  `UseDevelopmentStorage=true` (Azurite) e a `/api/cases` lista vazio
  silenciosamente — geração funciona, blobs ficam em `bundles/<caseId>/`,
  mas o dashboard nunca vê o caso novo.
- Faltando: `CaseGeneratorStorage__BundlesContainer=bundles` e
  `CaseGeneratorStorage__CasesContainer=cases` (defaults do código já são
  `bundles`/`cases` mas vale declarar explicitamente para consistência com a FA).

`infrastructure/README.md`:
- Ainda documenta `AzureOpenAI__*` em vez de `AzureFoundry__*`.

**Bug latente no código:**

`functions/CaseGen.Functions/Services/CaseV2/CaseV2GeneratorService.cs:564`
em `ResolveCasesBasePath()` o fallback final é
`Path.Combine(AppContext.BaseDirectory, "cases")`, que em Flex Consumption
Linux resolve para `/home/site/wwwroot/cases` (mount read-only do pacote de
deploy) → `UnauthorizedAccessException`. Trocar fallback para
`Path.Combine(Path.GetTempPath(), "casegen")` funciona em Linux Function App,
Windows local e macOS local sem precisar de app setting em prod.

**Tarefas:**

1. `infrastructure/functions/main.bicep`:
   - Remover entradas `AzureOpenAI__*`.
   - Adicionar entradas `LLM__UseAzureFoundry`, `AzureFoundry__Endpoint`,
     `AzureFoundry__ApiKey`, `AzureFoundry__ModelName`,
     `AzureFoundry__ImageDeploymentName` (apontando para os 4 secrets
     `azure-foundry-*` que já criamos no Key Vault).
   - Adicionar `CaseGenV2__CasesBasePath=/tmp` (até o item 4 ser feito; depois
     vira opcional).
   - Adicionar role assignment `Key Vault Secrets User` do MI da FA no escopo
     do KV `kv-ca-dev-oeq4agkmf6k4k` (módulo `keyvault-rbac.bicep` já existe
     para a API, replicar para a Function).

2. `infrastructure/api/main.bicep`:
   - Adicionar app setting `CaseGenerator__FunctionBaseUrl` derivado do
     nome/host da Function App do mesmo deploy (parametrizar ou ler da
     output do módulo de functions).
   - Adicionar `CaseGeneratorStorage__ConnectionString`,
     `CaseGeneratorStorage__BundlesContainer=bundles` e
     `CaseGeneratorStorage__CasesContainer=cases`. Em dev pode ser connection
     string da storage account `stcadevcabtlmvw4g` (que já foi aplicada via
     `az` nesta sessão como workaround). Em prod **prefira o item 5 abaixo**
     (migrar para Managed Identity).

3. `infrastructure/README.md`:
   - Atualizar a seção de app settings da Function App para listar
     `AzureFoundry__*` em vez de `AzureOpenAI__*` e mencionar
     `CaseGenV2__CasesBasePath` + `LLM__UseAzureFoundry`.
   - Adicionar seção de app settings do Web App com `CaseGenerator__FunctionBaseUrl`
     e `CaseGeneratorStorage__*`.

4. Código — `CaseV2GeneratorService.ResolveCasesBasePath`:
   - Trocar fallback final de `Path.Combine(AppContext.BaseDirectory, "cases")`
     para `Path.Combine(Path.GetTempPath(), "casegen")`.
   - Manter walk-up para repo-root só quando rodando local (preservar DX local).
   - Depois disso, `CaseGenV2__CasesBasePath=/tmp` pode sair do Bicep.

5. Código — `CaseV2StorageService` migrar para Managed Identity (igual à FA):
   - Hoje só lê `CaseGeneratorStorage:ConnectionString` (connection string com
     account key). A FA já usa `AzureWebJobsStorage__accountName` +
     `DefaultAzureCredential`.
   - Adicionar suporte a `CaseGeneratorStorage:AccountName` no construtor:
     se setado, usar `new BlobServiceClient(new Uri($"https://{accountName}.blob.core.windows.net"), new DefaultAzureCredential())`.
     Senão, manter fallback atual pra connection string (preserva DX local com Azurite).
   - Aplicar o mesmo padrão em `AssetsController`, `EmailsController` e
     `ForensicQueueService` (todos buscam `CaseGeneratorStorage:ConnectionString`
     direto hoje).
   - Habilitar System-Assigned MI no Web App via Bicep e dar role
     `Storage Blob Data Reader` (ou `Contributor` se houver write) no escopo
     da storage account.
   - Depois disso, `CaseGeneratorStorage__ConnectionString` pode sair do Bicep
     do Web App; só fica `CaseGeneratorStorage__AccountName`.

**Notas:**

- Os 4 secrets `azure-foundry-endpoint`, `azure-foundry-api-key`,
  `azure-foundry-model-name`, `azure-foundry-image-deployment-name` já estão
  populados no Key Vault dev — Bicep só precisa referenciá-los.
- Os secrets antigos `azure-openai-endpoint/api-key/deployment-name` referenciados
  pelo Bicep antigo **não existem** no KV (sempre falharam — só ninguém percebeu
  porque o código defaultava para Mock provider).
- Public Network Access do `kv-ca-dev-oeq4agkmf6k4k` foi habilitado nesta
  sessão. Pra prod isso vira `Disabled` + private endpoint, mas é fora do
  escopo dessa task.

**Critério de aceitação:**

- Rodar `az deployment ... what-if` ou `azd provision` na branch e ver que
  as 5 mudanças acima ficaram aplicadas sem drift.
- Geração ponta-a-ponta continua funcionando após o deploy do Bicep (sem
  precisar de `az ... appsettings set` manual).
- Dashboard do Web App lista o caso recém-gerado **sem** depender de
  connection string com account key (se item 5 foi feito).
- Em outra sub/ambiente novo, `azd up` deixa tudo funcionando do zero
  (assumindo que os 4 secrets do AzureFoundry sejam pré-populados no KV ou
  criados pelo próprio Bicep).

### TASK 3 — Configurar Function App URL no Web App de Azure dev

A página `/case-generation` está retornando **HTTP 503: "Case generator
not configured"** em Azure dev. O `CaseGenerationController` exige o app
setting `CaseGenerator__FunctionBaseUrl` apontando pra Function App, e o
Web App `casezero-api-dev` não tem ele.

**Passos (rodar do computador com `az login` válido para a subscription do dev):**

1. Descobrir o nome e URL da Function App de dev:
   ```bash
   az functionapp list \
     --query "[?contains(name, 'cgad') || contains(name, 'casegen') || contains(name, 'func')].{name:name, rg:resourceGroup, url:defaultHostName}" \
     -o table
   ```

2. Descobrir o resource group do Web App:
   ```bash
   az webapp list \
     --query "[?name=='casezero-api-dev'].{name:name, rg:resourceGroup}" \
     -o table
   ```

3. Setar o app setting (substituir `<func-app>` e `<rg>` com o que vier acima):
   ```bash
   az webapp config appsettings set \
     --name casezero-api-dev \
     --resource-group <rg-do-webapp> \
     --settings "CaseGenerator__FunctionBaseUrl=https://<func-app>.azurewebsites.net"
   ```

   O **`__` (duplo underscore)** é o separador que o .NET `IConfiguration`
   usa pra mapear `CaseGenerator:FunctionBaseUrl` quando o valor vem de
   variável de ambiente / app setting do App Service.

4. O `az webapp config appsettings set` reinicia o Web App automaticamente.
   Aguardar ~30 s e testar:
   ```bash
   curl -i -X POST https://casezero-api-dev.azurewebsites.net/api/casegeneration/generate \
     -H "Authorization: Bearer <jwt-de-um-usuario-logado>" \
     -H "Content-Type: application/json" \
     -d '{"difficulty":"Rookie"}'
   ```
   Esperado: **202 Accepted** com `{ jobId, status, statusUri }`. Se vier
   **502**, a Function App não está respondendo (ver TASK 4 abaixo se aplicar).
   Se vier **503** de novo, o app setting não pegou — checar com
   `az webapp config appsettings list --name casezero-api-dev --rg <rg>`.

5. Validar pela UI: logar na SWA, ir em `/case-generation`, gerar um caso
   Rookie, acompanhar o progresso, confirmar que o caso novo aparece no
   dashboard depois.

**Notas:**
- CORS na Function App **não** é problema (proxy server-to-server via backend).
- A Function App em dev usa Managed Identity pro Blob Storage; nenhuma key
  é necessária no app setting do backend pra esse cenário (auth da Function
  está como `AuthorizationLevel.Anonymous` nos endpoints v2).
- Se preferir Function-level auth no futuro, adicionar
  `CaseGenerator__FunctionKey=<key>` — o controller já injeta como header
  `x-functions-key`.

**Critério de aceitação:** geração ponta-a-ponta funcionando em Azure dev
(SWA → Web App → Function App → Blob → Web App lê → SWA mostra o caso novo).

### TASK 2 — Página de geração de casos no site

Botão no menu (visível para todos por enquanto) que abre uma nova página onde
o usuário escolhe o nível de dificuldade e dispara a geração de um novo caso.
A página acompanha o progresso em tempo (quase) real mostrando a fase atual da
pipeline e os tempos por estágio.

**Arquitetura:**
- **Backend proxy** (`CaseZeroApi`): novo `CaseGenerationController` em
  `/api/casegeneration` com `POST /generate` e `GET /jobs/{jobId}`. Proxy para
  a Function App. Mantém tudo em mesma origem, JWT auth única, prepara
  hook para audit/billing futuro.
- **Polling** (não SignalR): a Function já expõe `GET /api/cases/v2/jobs/{id}`
  com `currentPhase` + stage latencies. Geração leva 4-8 min, polling a cada
  2.5 s = ~150 hits de <1 KB. SignalR seria over-engineering — exigiria
  re-adicionar o package que foi removido na TASK F + um SignalR Service no
  Azure. Polling resolve hoje e é trivial migrar pra SignalR depois.
- **Config**: novo `CaseGenerator:FunctionBaseUrl` (e opcional
  `CaseGenerator:FunctionKey` para prod). Em dev local: `http://localhost:7071`.

**Frontend:**
- Nova página `pages/CaseGenerationPage.tsx` em `/case-generation`.
- Botão no nav menu (visível para todos).
- Form: difficulty (Rookie / Detective / Detective2 / Sergeant / Lieutenant /
  Captain / Commander) + opcionalmente título / theme / location / language /
  seed.
- Após `POST /generate` → polling do status com progress bar (% baseado no
  índice da fase atual / total), badge de status (queued/running/done/failed),
  fase atual em destaque, lista de stages concluídos com latência, e ao
  completar mostra: blobs publicados, validation errors (se houver),
  red-team verdict, refine telemetry.

**Critérios de aceitação:**
- Local end-to-end: usuário loga no frontend, clica no botão, escolhe Rookie,
  vê o status atualizando em tempo (quase) real, vê a geração concluir e o
  case aparece no dashboard depois do refresh.
- Backend não vaza function-key para o browser.
- Código pronto para teste em Azure dev (config via appsettings já está em
  Program.cs).

**Não-objetivos:**
- Não implementar gating por rank ainda (botão público).
- Não implementar billing / quota.
- Não implementar UI de cancelamento (a Function tem singleton mas não
  cancel).
- Não implementar SignalR; deixar marcado como evolução futura se polling
  ficar pesado.


### TASK 1 — Atualizar toda a documentação para refletir v2

A reescrita pra v2 fechou (PRs A–F mergeados): o site, o gerador e os testes
agora só conhecem `case.json` v2. A documentação **não** acompanhou — vários
arquivos ainda descrevem o pipeline durável legacy (Plan/Expand/Design/...), o
contrato v1 do case.json, endpoints v1 (`/api/cases/v1/...`), DBs de
forense rodando na Function App, etc.

**Escopo:**

- `docs/` — todos os arquivos abaixo precisam de uma passada cuidadosa:
  - `README.md` (índice da pasta)
  - `API_COMPLETE.md` — remover toda a seção `/api/cases/v1`, atualizar para os endpoints v2 atuais (`/api/cases`, `/api/cases/{id}`, `/api/cases/{id}/assets`, `/api/cases/{id}/assets/{aid}/download`, `/api/cases/v2/generate`, `/api/cases/v2/jobs/{id}`, etc).
  - `BACKEND_ARCHITECTURE.md` — refletir o fato de que `CaseV1*Service` sumiram, `VisibilityService` agora consome `ICaseV2StorageService`, `IRulesEngineService` foi reduzido a `EvaluateAndApplyAsync`.
  - `CASE_GENERATION_PIPELINE.md` — descrever **só** o pipeline v2 (micro-tasks do `CaseV2GeneratorService` + 13 fases + auto-fix + refine + blob publish). Apagar tudo de PlanStep/ExpandStep/DesignStep/GenerateStep/NormalizeStep.
  - `CASE_GENERATOR_SETUP.md` — reflete a function app atual (sem SignalR/Queue/EF).
  - `CASE_JSON_V2_SPEC.md` — verificar se ainda está atualizado contra `schemas/case.schema.json` e contra o que `CaseV2GeneratorService` realmente emite (incluindo `rules`, `forensicOutcomes`, `solution` etc).
  - `DATABASE_SCHEMA.md` — só backend DB; `ForensicRequest` continua, mas remover qualquer menção a tables da function.
  - `DEPLOYMENT.md` — referenciar os workflows atuais (`cd-dev.yml` + integration/functions tests no pipeline) e o fato de que prod ainda não foi provisionada.
  - `DEVELOPER_GUIDE.md` — atualizar comandos de dev (rodar backend local lê fixture filesystem; rodar functions é só pra gerar caso novo).
  - `RUNNING_FUNCTIONS_LOCALLY.md` — apenas v2; remover qualquer step v1.
  - `FRONTEND_ARCHITECTURE.md` — `casesApi` é o caminho único; `casesV1Api` morreu.
  - `MANUAL_SMOKE_v2.md` — verificar se as credenciais e cenários ainda fazem sentido.
  - `TROUBLESHOOTING.md` — atualizar erros conhecidos que ainda fazem sentido; descartar referências a v1.
  - `PDF_DOCUMENT_TEMPLATES.md`, `DIFFICULTY_PROFILE_SYSTEM.md`, `GAME_TIME_ENGINE.md`, `EMAIL_SYSTEM_IMPLEMENTATION.md` — confirmar se ainda estão alinhados com o código atual; ajustar onde divergir.
  - `FUTURE_FEATURES.md` — limpar features que viraram backlog desta lista ou que já foram entregues.
  - `cicd/` — verificar e atualizar.

- `docs/gdd/en/` (inglês, source-of-truth) — varrer todos os 12 capítulos + 3 apêndices + `TASKS.md`. Especial atenção a:
  - `04-CASE-STRUCTURE.md` — bater contra schema v2 atual
  - `09-DATA-SCHEMA.md` — idem
  - `10-CONTENT-PIPELINE.md` — pipeline v2
  - `08-TECHNICAL.md` — stack atual (sem SignalR/EF na function)
  - `12-ROADMAP.md` — ajustar status / próximos passos

- `docs/gdd/br/` (português) — espelhar exatamente o que ficou em `docs/gdd/en/` em inglês,
  preservando o estilo e exemplos já traduzidos. Não traduzir do zero: usar o
  inglês atualizado como referência e adaptar.

**Critérios de aceitação:**

- `grep -rni "case.*v1\|/v1/\|PlanStep\|ExpandStep\|DesignStep\|CaseV1\|casesV1Api\|ICaseV1\|ForensicProcessor"` no diretório `docs/` retorna **zero** matches (exceto em changelog/history se houver).
- `README.md` (raiz do repo) atualizado com a nova arquitetura.
- Diagramas (se houver no `assets/` ou inline em mermaid) refeitos para refletir só v2.
- GDD inglês e GDD-BR têm conteúdo equivalente.

**Não-objetivos:**

- Não tocar em código (essa task é só documentação).
- Não inventar features novas — só refletir o que existe.
