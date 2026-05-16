# 📋 CaseZero — Backlog

> Backlog ativo. Tarefas concluídas saem daqui (histórico vive nos commits e PRs).

---

## 🔥 Em aberto

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

#### Mitigação 2026-05-16 — secret apontado para `gpt-image-1.5`

Secret `azure-foundry-image-deployment-name` no KV `kv-ca-dev-oeq4agkmf6k4k`
trocado de `gpt-image-2` para `gpt-image-1.5` (quota=9, vs 2 do anterior).
Validação:

- Probe local (SDK 2.2.0-beta.4, mesmo `ImageClient` do
  `AzureFoundryLLMProvider`): **OK em 36s, PNG válido 2MB**.
- Geração end-to-end em prod após restart da FA (job
  `casev2-20260516100534-1f3832`, case `case_20260516_100535`):
  `AssetsRenderedImages: 3 / AssetsRenderedPdfs: 7 / BlobsPublished: 12 /
  HasErrors: false`. Bundle no blob com 3 PNGs reais (`archive_room_scene.png`,
  `ledger_shelf_gap.png`, `archive_lock_cylinder_closeup.png` — ~2MB cada).

Isso resolve o sintoma imediato de "imagens não aparecem". As tarefas
estruturais (1) throttle de concurrency, (2) fail-fast no publisher, (3)
graceful 404 no frontend permanecem em aberto — sem PTU folgado isso volta a
quebrar quando a quota baixar ou o caso pedir mais que 9 imagens simultâneas.

#### Investigação 2026-05-16 — diagnóstico raiz

Reproduzi localmente (Windows ARM64) com Functions host + Azurite, bypassando
QuestPDF via `tools/image-probe/` (raw HTTP/SDK contra `gpt-image-2`).
Findings:

1. **Deployment health**: chat (`gpt-5.2`) responde em 2.3s; **endpoint do
   image deployment funciona**, mas a geração de imagem com `quality=auto`
   (default da API) demora **> 90s e frequentemente > 3 min** por request.
   Com `quality=low` o mesmo prompt retorna **PNG válido em 48s**
   (1.8MB, magic bytes corretos).

2. **Saturação de PTU**: quota `OpenAI.GlobalStandard.gpt-image-2 = 2/2 Count`
   na subscription (account `lramo-mf2ik22e-swedencentral`, RG `ai-agents`).
   `gpt-image-1.5` tem 9 disponíveis sem uso; `gpt-image-1`, 3.
   PTU=2 → no máximo ~2 requests simultâneas; o resto enfileira ou rate-limita.

3. **Cliente fire-all-at-once**: `AssetRenderingService.RenderAllAsync`
   (`functions/CaseGen.Functions/Services/CaseV2/AssetRenderingService.cs:79,97`)
   adiciona todas as imagens em `imageTasks` e dispara `Task.WhenAll`.
   Caso de 9 assets → ~5 imagens em paralelo (3 PDFs já feitos) → satura
   PTU + estoura janela útil → maioria falha silenciosamente.

4. **Erros engolidos**: `RenderImageAsync` (linha 159-164) cataloga falhas em
   `report.Errors` e segue. O publisher publica o `case.json` mesmo com
   `ImagesWritten == 0`, gerando o cenário observado em prod
   (`case_20260515_193838`: 6 PDFs + 0 PNGs + 1 sidecar mp3).

5. **SDK 2.2.0-beta.4**: `ImageGenerationOptions.Quality` aceita
   `GeneratedImageQuality.Standard | High` (no `Low` exposto). Para usar
   `quality=low` precisaríamos `BinaryContent` raw ou subir SDK. Hoje o
   código nem seta `Quality` → default `auto` (lento).

6. **NetworkTimeout no SDK** já está em 10 min
   (`AzureFoundryLLMProvider.cs:40`). `functionTimeout=01:00:00` no
   `host.json`. Não há gargalo de timeout do lado do cliente; o problema é
   o tempo + saturação real.

**Fix proposto (próximo PR):**

- `AzureFoundryLLMProvider.GenerateImageAsync`: setar
  `Quality = GeneratedImageQuality.Standard` (ou usar raw HTTP com
  `quality=low` para deployments `gpt-image-*` enquanto SDK não expõe).
  Adicionar retry-with-backoff e tratamento de `ClientResultException` 429
  lendo `Retry-After`.
- `AssetRenderingService`: trocar `Task.WhenAll(imageTasks)` por loop com
  `SemaphoreSlim` (concurrency configurável, default = 2 = quota PTU).
- `CaseV2GeneratorService` + publisher: fail-fast se
  `report.ImagesWritten + report.Skipped < expectedImageAssets`. Marcar job
  como `failed` com lista de assets faltantes em vez de publicar parcial.
- Frontend `FileViewer`/`Desktop`: graceful 404 ("asset indisponível
  temporariamente"), permitir re-tentar download depois.
- (Opcional, infra) Aumentar quota PTU do `gpt-image-2` para 8+ ou
  migrar deployment para `gpt-image-1.5` (quota 9 disponível, sem fila).

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


---

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
