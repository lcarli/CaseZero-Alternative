# 📋 CaseZero — Backlog

> Backlog ativo. Tarefas concluídas saem daqui (histórico vive nos commits e PRs).

---

## 🔥 Em aberto

### TASK 6 — CORS não inclui origin do SWA (SignalR + qualquer request com `credentials: include` quebra)

Descoberto após a TASK 4 (dashboard de cases agora funciona, então a sessão
do jogo chega no ponto de abrir o hub do SignalR). Bug pré-existente
exposto, não introduzido.

**Sintoma no DOM (em `https://gentle-ground-03dca4110.3.azurestaticapps.net`):**

```
Access to fetch at 'https://casezero-api-dev.azurewebsites.net/hubs/forensics/negotiate?negotiateVersion=1'
from origin 'https://gentle-ground-03dca4110.3.azurestaticapps.net' has been blocked by CORS policy:
Response to preflight request doesn't pass access control check:
The value of the 'Access-Control-Allow-Origin' header in the response must not be the wildcard '*'
when the request's credentials mode is 'include'.
```

**Causa:** `backend/CaseZeroApi/Program.cs:121` registra apenas
`policy.WithOrigins("http://localhost:5173")`. Em produção alguém
configurou CORS no portal do App Service com `*`, que é incompatível
com `credentials: include` (regra fundamental do CORS). SignalR usa
`credentials: include` sempre.

**Tarefa:**

1. Em `Program.cs`, ler a lista de origens permitidas de configuração
   (ex.: `Cors:AllowedOrigins` como array no `appsettings.json` + override
   por env var) em vez de hard-coded localhost.
2. Acrescentar a origin do SWA dev (`https://gentle-ground-03dca4110.3.azurestaticapps.net`)
   e a origin de produção quando existir.
3. Adicionar a setting no Bicep (`infrastructure/api/main.bicep`) — algo
   como `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, etc.
4. Limpar o CORS de nível App Service no portal (ou via
   `az resource update --resource-type Microsoft.Web/sites/config
   --name web --set properties.cors.allowedOrigins='[]'`) para que o
   middleware da app seja a única autoridade.
5. Validar SignalR (`/hubs/forensics/negotiate`) + qualquer outro fetch
   com cookies/auth do frontend.

---

### TASK 7 — Geração de caso declara assets que não foram renderizados (PNG/MP3 faltando no blob)

Descoberto na TASK 4. Caso `case_20260515_193838` tem 9 assets declarados
no `case.json` (em `bundles/case_20260515_193838/case.json`), mas o blob
storage só tem 6 PDFs + 1 placeholder `.mp3.txt`. Faltam todos os PNGs
e o MP3 real.

**Sintoma no DOM:**

```
GET /api/cases/case_20260515_193838/assets/asset.archive_room_lock_photo/download
=> 404 Asset file not found
```

(O endpoint retorna 404 corretamente — o `case.json` declara
`uri=case://case_20260515_193838/assets/archive_room_lock_photo.png`
mas o arquivo não está em `bundles/case_20260515_193838/assets/`.)

**Causa provável:** o pipeline de geração de CaseGen.Functions completa
com `validation_errors > 0` e ainda assim publica o `case.json`
incompleto no `bundles/`. O painel de geração já mostrava
`PDFs rendered 0 / Images rendered 0` em uma das execuções — confirma
que o renderer de imagens não está produzindo output em produção.

**Tarefa:**

1. Investigar por que o ImageRendererService não está produzindo PNGs
   em produção (Azure Foundry image deployment? falta de modelo?
   credenciais? quota?). Comparar com o que está nos logs da FA.
2. Fail-fast: se o número de assets renderizados for menor que o
   declarado, a etapa de publicação no `bundles/` deveria abortar
   ou marcar o caso como `incomplete` em vez de publicar parcialmente.
3. Frontend deveria também tratar 404 de asset com graceful fallback
   (mostrar "asset indisponível" em vez de só logar erro silencioso).

---

### TASK 8 — Frontend envia `undefined` como emailId ao abrir email

Descoberto na TASK 4. Bug puro de frontend.

**Sintoma no DOM:**

```
GET /api/cases/case_20260515_193838/emails/undefined/open => 400
```

O `case.json` tem emails com `id=email.briefing`, `id=email.devin_initial_statement`
etc. O frontend está lendo um campo errado do objeto de email (talvez
`email.id` quando o DTO usa `emailId` ou vice-versa após algum refactor)
e mandando a string literal `undefined` na URL.

**Tarefa:**

1. Localizar em `frontend/src/components/Desktop.tsx` (ou similar) o
   chamador de `emailsApi.openEmail(...)`.
2. Verificar que o objeto recebido do `GET /api/cases/{caseId}/emails`
   tem o campo esperado.
3. Corrigir a leitura do id no frontend OU expor o campo correto no DTO
   do backend para alinhar.
4. Smoke test: abrir cada email do caso `case_20260515_193838` no SWA dev.

---

### TASK 5 — Migrar `AzureWebJobsStorage` da Function App para Managed Identity completo

Descoberto durante a TASK 4 (rubber-duck review). O Bicep da Function App em
`infrastructure/functions/main.bicep` ainda usa `listKeys()` + connection
string em `AzureWebJobsStorage` e `WEBSITE_CONTENTAZUREFILECONNECTIONSTRING`.
Hoje funciona em dev porque setamos `AzureWebJobsStorage__accountName` direto
via `az` (drift), mas se alguém re-provisionar com o Bicep atual numa
subscription com a policy `Azure_Security_Baseline` (que bloqueia
`allowSharedKeyAccess`), a Function App não sobe — o `listKeys()` durante o
deploy do storage falha ou as connection strings ficam inertes.

**Bloqueio técnico:** o plano `Y1 (Consumption)` exige
`WEBSITE_CONTENTAZUREFILECONNECTIONSTRING` para o content share, e essa
chave **só** funciona com connection string (não tem MI no Y1). Migrar para
MI completa exige mudar o plano para **Flex Consumption** (`FC1`) ou
**Elastic Premium** (`EP1`).

**Tarefas:**

1. Mudar `infrastructure/functions/main.bicep` para `FC1` (Flex Consumption)
   em dev ou aceitar `EP1` (mais caro).
2. Substituir `AzureWebJobsStorage` (connection string) por
   `AzureWebJobsStorage__accountName=<storage>` + `AzureWebJobsStorage__credential=managedidentity`.
3. Remover `WEBSITE_CONTENTAZUREFILECONNECTIONSTRING` + `WEBSITE_CONTENTSHARE`
   (Flex Consumption não usa content share — usa deployment package via
   `WEBSITE_RUN_FROM_PACKAGE` ou o novo `linuxFxVersion` com source de pacote).
4. Garantir que o MI da FA tem `Storage Blob Data Owner` (ou
   `Contributor`) no storage account para conseguir gerenciar lease/leases
   do host.
5. Validar com `azd up` + smoke da geração ponta-a-ponta numa subscription
   com a policy ativa.

**Critério de aceitação:** Recriar do zero (`azd up`) numa subscription com
`Azure_Security_Baseline` ativo, e a Function App subir, processar uma
geração v2 e publicar o caso no blob — sem nenhum `az ... appsettings set`
manual e sem nenhuma chave compartilhada habilitada.

### TASK 4 — Persistir configuração da Function App no Bicep + corrigir fallback de disco

**Status:** Implementação concluída em `feat/task-4-bicep-mi-fallback` (PR
em aberto). Após o merge + deploy do código via CI, ainda faltam os passos
**manuais** em Azure dev (até o Bicep ser re-aplicado por `azd provision`):

```bash
# 1. Atribuir RBAC do API MI no storage account (cross-RG)
API_MI=$(az webapp identity show -n casezero-api-dev -g casezero-api-dev-rg --query principalId -o tsv)
STORAGE_ID=$(az storage account show -n stcadevcabtlmvw4g -g casezero-func-dev-rg --query id -o tsv)
az role assignment create --assignee-object-id $API_MI --assignee-principal-type ServicePrincipal \
  --role "Storage Blob Data Reader" --scope $STORAGE_ID
az role assignment create --assignee-object-id $API_MI --assignee-principal-type ServicePrincipal \
  --role "Storage Queue Data Message Sender" --scope $STORAGE_ID

# 2. Pré-criar a queue forensic-requests (o Bicep faz no provision; em dev cria manualmente)
az storage queue create --name forensic-requests --account-name stcadevcabtlmvw4g --auth-mode login

# 3. Trocar a connection string pelo AccountName no Web App
az webapp config appsettings set -n casezero-api-dev -g casezero-api-dev-rg \
  --settings CaseGeneratorStorage__AccountName=stcadevcabtlmvw4g
az webapp config appsettings delete -n casezero-api-dev -g casezero-api-dev-rg \
  --setting-names CaseGeneratorStorage__ConnectionString

# 4. Remover o override de CasesBasePath na FA (o código agora usa /tmp/casegen como fallback)
az functionapp config appsettings delete -n casegen-func-dev -g casezero-func-dev-rg \
  --setting-names CaseGenV2__CasesBasePath
```

Aguardar 5–10 min após RBAC para propagação e testar dashboard.

---

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
