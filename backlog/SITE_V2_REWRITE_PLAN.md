# Plano de Reescrita do Site — Contrato `case.json` v2

> **Status:** Aguardando execução. Este documento é o **único** plano canônico para a reescrita do *site* (frontend + `CaseZeroApi`) em torno de um único contrato `case.json` v2.
>
> **Branch:** `feat/site-contract-v2` (já criada a partir de `V3-2026-newAI`).
>
> **Escopo:** apenas o site. **`functions/CaseGen.Functions*` NÃO é tocada** nesta entrega — continua em .NET 9 e será alinhada em um trabalho futuro (ver seção *Continuidade*).
>
> **Modo de execução:** as fases têm dependências entre si. Dentro de cada fase, os itens são paralelizáveis em arquivos diferentes. Cada item abaixo está descrito com nível de detalhe suficiente para um LLM executar **sem precisar adivinhar** decisões; quando houver alternativa, a decisão preferida está marcada com **(decisão).**

---

## 0. Contexto e diagnóstico (resumo)

Hoje convivem três especificações conflitantes de "caso":

- **v0 legacy** — `frontend/src/types/case.ts`, `frontend/src/engine/CaseEngine.ts`, `docs/OBJETO_CASO.md`, `GDD-BR/09-ESQUEMA-DE-DADOS.md`. Usa `evidences[]`, `unlockLogic`, `solution`, `forensicAnalyses`, `temporalEvents`, `timeline`.
- **v1 atual** — `schemas/case.schema.json`, `cases/case_001/case.json`, `backend/CaseZeroApi/Models/CaseV1/CaseV1.cs`, `docs/CASE_JSON_V1_SPEC.md`. Usa `assets[]`, `emails[]`, `suspects[]`, `rules[].trigger.actions[]`, `forensicsDefaults`. **Não tem `solution`.**
- **GDD v3.0** — `GDD-BR/04-ESTRUTURA-DE-CASO.md`. Usa ainda outro formato (`evidence[]`, `documents[]`, `forensicReports[]`, `unlock:{trigger:onEvidenceCollected,...}`).

Bugs estruturais descobertos:

1. `RulesEngineService.EvaluateForensicRuleAsync` **nunca é invocado** pelo `ForensicsBackgroundService`. Mesmo se fosse, o DTO interno espera `{ rules: { forensics:[{inputAssetId,analysisType,action,emailId}] } }` mas o `case_001/case.json` traz `{ rules: [{ ruleId, trigger:{type,inputAssetId,analysisType}, actions:[{type,emailId}] }] }`. Os dois não casam.
2. `frontend/src/services/caseDataService.ts` converte v1→v0 em runtime e **descarta** `rules`/`solution`/`forensicAnalyses`/`temporalEvents`/`timeline`.
3. `Desktop.tsx` chama `assetsApi/emailsApi/forensicsApi` direto — o `CaseEngine` na prática é código morto.
4. `SubmitCase.tsx` é um formulário genérico (breaking-entering/theft/murder/disappearance) totalmente desconectado do caso atual. Não há endpoint backend que avalie a submissão contra a verdade.
5. README e `DesktopPage.tsx` apontam para `CASE-2024-001/002/003`, que **não existem** no repo.
6. `difficulty` é inconsistente: schema declara enum string, `case_001/case.json` traz `5` (number), `types/caseV1.ts` tipa como `number`.

## Decisões já tomadas com o usuário

1. **(A) Hard cut.** Remover o legado v0 por completo do site, sem janela de coexistência. Migrar `case_001` para v2 manualmente.
2. **(B) Solução estruturada e rica.** O `case.json` inclui `solution{}` com culprit, evidências obrigatórias, análises obrigatórias, perguntas estruturadas com pontuação parcial e nº máximo de tentativas.
3. **(C) Difficulty como enum string** alinhada à hierarquia de ranks: `Rookie | Detective | Detective2 | Sergeant | Lieutenant | Captain | Commander`. Quando `requiredRank == Rookie`, o caso herda `unlockMode = "all_initial"` automaticamente.

## Restrições do projeto

- **i18n obrigatório:** qualquer string nova de UI deve ser traduzida nos 4 idiomas (`en-US`, `pt-BR`, `es-ES`, `fr-FR`) em `frontend/src/locales/`.
- **`CaseGen.Functions` e `CaseGen.Functions.Tests`** permanecem em **.NET SDK 9** e fora do escopo desta entrega.
- **Segurança:** `solution`, `rules`, `forensicOutcomes`, `gameMetadata.generation` e qualquer asset/email/suspeito com `visibility="hidden"` ainda não revelado **NUNCA** podem ir ao cliente. Tudo passa pelo sanitizer e tem teste.

---

## Convenções para o executor

- **Onde implementar:** sempre na branch `feat/site-contract-v2`.
- **Padrão de commits:** um commit por fase (ou subfase grande), prefixo Conventional Commits, trailer `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`. Mensagens em inglês.
- **Verificações antes de marcar um item como done:**
  - Compila (`dotnet build`, `npm run build`).
  - Não introduz novos warnings que não existissem no baseline.
  - Quando o item tem teste, o teste passa.
- **Quando um arquivo for renomeado** (ex.: `CaseV1.cs` → `CaseV2.cs`), atualizar **todos** os imports/usings/refs no projeto inteiro (não deixar definição duplicada).
- **Não usar shims/adapters v1↔v2** no site. É hard cut.
- **Não criar migrations EF** a menos que estritamente necessário; se um campo novo de `CaseSubmission` for preciso, criar **uma única** migration consolidada na Fase 3.

---

## Estrutura de fases

```
Fase 0  Setup & baseline                 → pré-requisito de tudo
Fase 1  Contrato v2 (spec, schema, caso) → pré-requisito do código novo
Fase 2  Hard cut                         → depende da Fase 1 (sabemos o que fica)
Fase 3  Backend v2                       → depende da Fase 2
Fase 4  Frontend v2-nativo               → depende da Fase 3
Fase 5  Documentação                     → paralela à Fase 4 (depende da Fase 1)
Fase 6  Testes                           → depende das fases 3 e 4
Fase 7  Validação final & commits        → última
```

---

## Fase 0 — Setup & baseline

### 0.1 Confirmar branch
- Branch `feat/site-contract-v2` **já está criada** a partir de `V3-2026-newAI` (commit `07bdb09`).
- Antes de começar, rodar `git status` e garantir árvore limpa. Se houver mudanças locais não relacionadas, *stashear*.

### 0.2 Capturar baseline
Rodar e **salvar a saída em comentário do primeiro commit** (ou em um arquivo `backlog/BASELINE.txt` temporário, que é apagado na Fase 7):

```bash
# Backend
cd backend && dotnet build CaseZeroApi/CaseZeroApi.csproj 2>&1 | tee /tmp/baseline-backend-build.txt
dotnet test CaseZeroApi.Tests/CaseZeroApi.Tests.csproj 2>&1 | tee /tmp/baseline-backend-test.txt
cd ..

# Frontend
cd frontend && npm install
npm run build 2>&1 | tee /tmp/baseline-front-build.txt
npm run lint 2>&1 | tee /tmp/baseline-front-lint.txt
npm run test:run 2>&1 | tee /tmp/baseline-front-test.txt
cd ..
```

A função disso é poder distinguir, ao fim da Fase 7, o que é **regressão** do que já estava quebrado.

---

## Fase 1 — Contrato `case.json` v2

### 1.1 Escrever `docs/CASE_JSON_V2_SPEC.md`

Substitui `docs/CASE_JSON_V1_SPEC.md` (que será apagado na Fase 5). Deve conter:

#### 1.1.1 Cabeçalho de canon
```md
# Case JSON v2 — Specification (Canonical)

This is the **only** canonical case format used by the site. Versions v0 and v1 are deprecated and removed from the codebase. Any document that references `evidences[]`, `unlockLogic`, `documents[]`, `forensicReports[]`, or schema version other than `"2.0"` is stale and must be ignored.
```

#### 1.1.2 Estrutura raiz
```jsonc
{
  "version": "2.0",
  "caseId": "case_001",
  "metadata": { ... },
  "assets":   [ ... ],
  "emails":   [ ... ],
  "suspects": [ ... ],
  "timeline": [ ... ],
  "temporalEvents": [ ... ],
  "rules":   [ ... ],
  "forensicsDefaults": { ... },
  "forensicOutcomes": [ ... ],   // server-side only
  "solution": { ... },           // server-side only
  "gameMetadata": { ... }
}
```

#### 1.1.3 `metadata`
```jsonc
{
  "title": "string",
  "description": "string",
  "location": "string",
  "incidentDate": "ISO-8601",
  "openedAt": "ISO-8601",
  "difficulty": "Rookie | Detective | Detective2 | Sergeant | Lieutenant | Captain | Commander",
  "requiredRank": "Rookie | Detective | Detective2 | Sergeant | Lieutenant | Captain | Commander",
  "unlockMode": "gated | all_initial",   // optional; defaults to "gated" except when requiredRank == "Rookie" → forced "all_initial"
  "estimatedDurationMinutes": 60,
  "tags": ["string"]
}
```
- **Regra de validação:** se `requiredRank == "Rookie"`, o validador deve forçar `unlockMode = "all_initial"`. O backend aplica essa regra mesmo que o JSON não tenha o campo.

#### 1.1.4 `assets[]`
```jsonc
{
  "id": "asset.<slug>",
  "type": "photo | pdf | audio | video | document",
  "title": "string",
  "description": "string",
  "uri": "blob://<container>/<path>" | "case://<caseId>/<file>",
  "checksum": "sha256:..." ,    // optional
  "visibility": "initial | hidden",
  "tags": ["string"]
}
```

#### 1.1.5 `emails[]`
```jsonc
{
  "id": "email.<slug>",
  "from": "string",
  "to": ["string"],
  "subject": "string",
  "body": "markdown string",
  "sentAt": "ISO-8601",
  "attachments": ["asset.<slug>"],
  "visibility": "initial | hidden"
}
```
- O email com `id == "email.briefing"` (ou primeiro `visibility: initial`) é o de abertura do chefe da polícia.

#### 1.1.6 `suspects[]`
```jsonc
{
  "id": "suspect.<slug>",
  "name": "string",
  "alias": "string",
  "description": "string",
  "alibi": "string",
  "alibiVerified": false,
  "status": "suspect | cleared | confirmed_culprit",
  "relatedAssets": ["asset.<slug>"],
  "photo": "asset.<slug>",
  "visibility": "initial | hidden"
}
```

#### 1.1.7 `timeline[]`
```jsonc
{
  "time": "ISO-8601",
  "event": "string",
  "source": "investigation | witness | sensor | forensic",
  "sourceAssetId": "asset.<slug>",   // optional
  "verified": true,
  "importance": "low | medium | high | critical"
}
```

#### 1.1.8 `temporalEvents[]`
Eventos disparados em função do tempo de jogo (em minutos desde abertura do caso).
```jsonc
{
  "id": "tevt.<slug>",
  "triggerAtMinutes": 30,
  "type": "memo | witness | alert | email",
  "payload": {
    "emailId": "email.<slug>",        // when type == "email"
    "assetId": "asset.<slug>",        // when type == "memo" or "alert"
    "message": "string"               // free-text fallback
  }
}
```
- Idempotência: backend marca cada `tevt.*` como disparado em `CaseSessionState`.

#### 1.1.9 `rules[]`
**Forma canônica única** (substitui qualquer outra variante que tenha aparecido em código):
```jsonc
{
  "ruleId": "rule.<slug>",
  "description": "string",
  "trigger": {
    "type": "forensics_complete | attachment_download | asset_viewed | email_opened | time_elapsed | suspect_viewed | multiple_conditions",
    "inputAssetId": "asset.<slug>",      // for forensics_complete, attachment_download, asset_viewed
    "analysisType": "string",            // for forensics_complete
    "emailId": "email.<slug>",           // for email_opened, attachment_download
    "suspectId": "suspect.<slug>",       // for suspect_viewed
    "atMinutes": 45,                     // for time_elapsed
    "conditions": [ { /* trigger */ } ], // for multiple_conditions
    "operator": "AND | OR"               // for multiple_conditions
  },
  "actions": [
    { "type": "reveal_email",          "emailId": "email.<slug>" },
    { "type": "reveal_asset",          "assetId": "asset.<slug>" },
    { "type": "reveal_suspect",        "suspectId": "suspect.<slug>" },
    { "type": "add_email_attachment",  "emailId": "email.<slug>", "assetId": "asset.<slug>" },
    { "type": "send_notification",     "level": "info | warn | critical", "message": "string" },
    { "type": "update_suspect_status", "suspectId": "suspect.<slug>", "status": "cleared | confirmed_culprit" },
    { "type": "mark_alibi_verified",   "suspectId": "suspect.<slug>", "verified": true }
  ]
}
```
- Avaliação: server-side. Idempotente por `(ruleId, sessionId)`. Cada `rule` só dispara uma vez por sessão.

#### 1.1.10 `forensicsDefaults`
```jsonc
{
  "analysisTypes": [
    { "type": "fingerprint",  "durationMinutes": 10, "availableFor": ["photo","document"] },
    { "type": "dna",          "durationMinutes": 30, "availableFor": ["photo"] },
    { "type": "phone_dump",   "durationMinutes": 20, "availableFor": ["audio","document"] }
  ],
  "noFindingsEmail": {
    "template": "forensics.no_findings",
    "from": "lab@police.gov",
    "subject": "Forensic analysis — no relevant findings"
  }
}
```

#### 1.1.11 `forensicOutcomes[]` (server-only)
Mapeia o resultado **canônico** de cada análise sobre cada asset elegível.
```jsonc
{
  "inputAssetId": "asset.<slug>",
  "analysisType": "fingerprint",
  "findings": true,
  "matchedSuspectId": "suspect.<slug>",   // optional
  "matchedAssetId": "asset.<slug>",       // optional
  "conclusionText": "Fingerprints on the knife match suspect.sarah_chen.",
  "resultAssetId": "asset.lab_report_001",
  "resultEmailId": "email.lab_response_001"
}
```
- Se `findings == false`, o backend envia o `noFindingsEmail` ao usuário.
- Em paralelo, qualquer `rule` com trigger `forensics_complete` continua sendo avaliada.

#### 1.1.12 `solution{}` (server-only)
```jsonc
{
  "culpritId": "suspect.<slug>",
  "requiredEvidenceIds": ["asset.<slug>"],
  "requiredAnalysisIds": ["analysisId(=`{inputAssetId}:{analysisType}`)"],
  "questions": [
    {
      "id": "q.motive",
      "prompt": "What was the motive?",
      "options": [
        { "id": "opt.greed",     "label": "Financial gain" },
        { "id": "opt.revenge",   "label": "Revenge" },
        { "id": "opt.jealousy",  "label": "Jealousy" }
      ],
      "correctOptionId": "opt.greed",
      "weight": 0.2
    }
  ],
  "explanation": "markdown string shown only after the case is resolved",
  "minimumScore": 0.7,
  "maxAttempts": 3,
  "partialCreditRules": {
    "culpritWeight": 0.4,
    "evidenceWeight": 0.2,
    "analysisWeight": 0.2,
    "questionsWeight": 0.2
  }
}
```
- O sanitizador remove **`correctOptionId`**, **`explanation`** e **`partialCreditRules`** antes de enviar ao cliente. O cliente recebe apenas `culpritId` *sanitizado* (omitido) + perguntas + opções + `minimumScore` + `maxAttempts`.
- `partialCreditRules` deve somar 1.0.

#### 1.1.13 `gameMetadata{}`
```jsonc
{
  "schemaVersion": "2.0",
  "createdAt": "ISO-8601",
  "tags": ["string"],
  "contentWarnings": ["violence","drug-use"],
  "localizations": ["en-US","pt-BR"],
  "generation": {                  // server-side only
    "pipelineVersion": "string",
    "model": "string",
    "seed": 12345,
    "bundleChecksum": "sha256:..."
  }
}
```

#### 1.1.14 Checklist de validação
A spec termina com tabela:
| Campo | Obrigatório | Cliente vê? |
|-------|-------------|-------------|
| `metadata.*` | sim | sim |
| `assets[]` (visibility=initial) | sim | sim |
| `assets[]` (visibility=hidden) | sim | só se revelado |
| `solution` | sim | **não** |
| `rules` | sim | **não** |
| `forensicOutcomes` | sim | **não** |
| `gameMetadata.generation` | não | **não** |

### 1.2 Reescrever `schemas/case.schema.json`
- JSON Schema Draft 2020-12.
- Reflete 1:1 a spec acima, incluindo enums.
- Validar localmente com `ajv` (`npx ajv-cli validate -s schemas/case.schema.json -d cases/case_001/case.json`).

### 1.3 Migrar `cases/case_001/case.json` para v2
- Manter os campos existentes onde aplicável.
- **Adicionar** `solution`, `timeline`, `temporalEvents`, `forensicOutcomes`, `gameMetadata`, `unlockMode`.
- Trocar `metadata.difficulty: 5` por `metadata.difficulty: "Detective"` (escolha coerente com o caso — não Rookie, pois case_001 não deve auto-desbloquear tudo; documentar a escolha no commit).
- Garantir que existe ao menos: um `email.briefing` initial, um asset initial, uma rule `forensics_complete → reveal_email`, um `forensicOutcome`, um `temporalEvent` simples.
- `cases/samples/case.sample.json` é **apagado** (Fase 2) — sample passa a ser o próprio `case_001`.

---

## Fase 2 — Hard cut (apagar o que ficou obsoleto)

> **Ordem:** após a Fase 1. Não apagar antes da spec estar fechada — a spec define o que sobra.

### 2.1 Backend — apagar
Verificar referências antes de cada delete (`grep -r "ClassName" backend/`):

- `backend/CaseZeroApi/Controllers/CaseObjectController.cs`
- `backend/CaseZeroApi/Services/CaseObjectService.cs`
- `backend/CaseZeroApi/Services/ICaseObjectService.cs` (se existir)
- `backend/CaseZeroApi/Models/CaseObject.cs`
- `backend/CaseZeroApi/Models/CaseDocument.cs`
- `backend/CaseZeroApi/Models/TemporalEvent.cs` (legacy — o v2 tem o seu próprio dentro de `CaseV2`)
- `backend/CaseZeroApi/Models/NormalizedCaseBundle.cs` (verificar — se só serve para v0, apagar)
- `backend/CaseZeroApi/Models/Evidence.cs` — **CUIDADO**: pode ser referenciada por EF/migrations. Se for, manter classe vazia/EF-only ou criar migration que dropa a tabela. **(decisão)**: se a tabela for usada por `CaseSubmission`, manter a entity mínima sem semântica de gameplay (sem `UnlockLogic`); caso contrário, dropar com migration consolidada.
- `backend/CaseZeroApi/Controllers/CaseGenerationController.cs`
- `backend/CaseZeroApi/Services/CaseGenerationService.cs`
- `backend/CaseZeroApi/Services/ICaseGenerationService.cs`
- `backend/CaseZeroApi/Services/CaseGenerationModels.cs`
- `backend/CaseZeroApi/Services/LlmClient.cs`
- `backend/CaseZeroApi/Services/PromptLibrary.cs`
- `backend/CaseZeroApi/README_AI_GENERATION.md`
- Remover registros em `Program.cs` (DI) e quaisquer `using` órfãos.
- Remover dependências NuGet órfãs (ex.: cliente HTTP de LLM) no Fase 7.

### 2.2 Backend tests — apagar
- `backend/CaseZeroApi.Tests/Controllers/CaseObjectControllerTests.cs`
- `backend/CaseZeroApi.Tests/Services/CaseGenerationServiceTests.cs`
- `backend/CaseZeroApi.Tests/Services/CaseGenerationIntegrationTests.cs`
- `backend/CaseZeroApi.Tests/Services/CaseUtilsTests.cs` **somente se** todas as utilities testadas pertencerem ao caminho v0. Verificar antes.

### 2.3 Frontend — apagar
- `frontend/src/types/case.ts` (v0)
- `frontend/src/types/caseV1.ts` (será substituído por `caseV2.ts` na Fase 4)
- `frontend/src/engine/CaseEngine.ts` (será reescrito na Fase 4)
- `frontend/src/services/caseDataService.ts` (conversão v1→v0 some)
- `frontend/src/pages/CaseGeneratorAIPage.tsx`
- `frontend/src/pages/GenerateCasePage.tsx`
- Remover rotas dessas páginas em `frontend/src/App.tsx`.
- Remover imports órfãos e ajustar `index.ts`/barrels.

### 2.4 Docs — apagar
- `docs/OBJETO_CASO.md` (v0, contradiz a spec nova)
- `docs/CASE_JSON_V1_SPEC.md` (substituído por `CASE_JSON_V2_SPEC.md`)
- `cases/samples/case.sample.json` (case_001 é o sample agora; atualizar links que apontavam para o sample)

### 2.5 Limpar referências a casos inexistentes
- `README.md`: trocar `CASE-2024-001/002/003` por `case_001`.
- `frontend/src/pages/DesktopPage.tsx`: remover fallback `CASE-2024-001` (correção definitiva na Fase 4).
- `frontend/src/services/caseDataService.ts` foi apagado, mas se algum outro arquivo expôr `getAvailableCases()` com lista hardcoded, remover.

---

## Fase 3 — Backend v2

### 3.1 Modelo: `Models/CaseV2/CaseV2.cs`
- Mover/renomear o conteúdo de `Models/CaseV1/CaseV1.cs` para `Models/CaseV2/CaseV2.cs`.
- Expandir com `Timeline`, `TemporalEvent`, `ForensicOutcome`, `Solution`, `Question`, `QuestionOption`, `PartialCreditRules`, `GameMetadata`, `GenerationMetadata`.
- Usar `record` quando a entidade é puramente imutável e DTO-like; `class` se precisar de serialização EF.
- Polimorfismo de `trigger`/`action`: usar `System.Text.Json.Serialization.JsonPolymorphic`/`JsonDerivedType` com discriminador `type`. Alternativa: deserializar em `JsonElement` e fazer dispatch manual. **(decisão)** polimorfismo nativo do `System.Text.Json` 8.x.
- Atualizar **todos** os usings/refs no projeto (`grep -rl "CaseV1" backend/`).
- Apagar o namespace `Models/CaseV1/`.

### 3.2 Storage: `Services/CaseV2StorageService.cs`
- Renomear/atualizar `Services/CaseV1StorageService.cs`.
- Manter cache em memória por `caseId`.
- Expor dois métodos:
  - `Task<CaseV2> GetRawAsync(string caseId)` — uso server-side.
  - `Task<CaseV2Sanitized> GetForUserAsync(string caseId, string userId)` — chama o sanitizer.
- Suportar carregamento via Blob (produção) e filesystem local (dev). Manter o fallback que já existe.

### 3.3 Sanitizer: `Services/CaseV2SanitizerService.cs`
- Renomear/atualizar `Services/CaseV1SanitizerService.cs`.
- Remove de forma garantida:
  - `solution` inteiro **exceto** `questions[].id/prompt/options[].id/label` e `maxAttempts`.
  - `solution.questions[].correctOptionId`, `solution.explanation`, `solution.partialCreditRules`, `solution.culpritId`, `solution.requiredEvidenceIds`, `solution.requiredAnalysisIds`.
  - `rules` inteiro.
  - `forensicOutcomes` inteiro.
  - `gameMetadata.generation`.
- Filtra `assets`, `emails`, `suspects` por `visibility != "hidden" OR revelado em CaseSessionState`.
- Filtra `temporalEvents` por já disparados no session state, mas **não** envia o JSON deles ao cliente (são server-only).
- Tem testes para **cada** campo sensível.

### 3.4 VisibilityService — `unlockMode`
- Estender `VisibilityService` para:
  - Se `metadata.unlockMode == "all_initial"` (forçado quando `requiredRank == "Rookie"`): tratar todos os assets/emails/suspects como revelados na primeira sessão.
  - Caso contrário (`gated`): comportamento existente.
- Adicionar teste para cada cenário.

### 3.5 RulesEngineService — reescrita completa
Arquivo: `Services/RulesEngineService.cs` (+ interface).

- Apagar o DTO antigo (`{ rules: { forensics:[...] } }`).
- Novo método público:
  ```csharp
  Task EvaluateAndApplyAsync(string caseId, string userId, RuleTrigger trigger, CancellationToken ct);
  ```
- `RuleTrigger` é um *sealed record* polimórfico com subtipos: `ForensicsCompleteTrigger`, `AttachmentDownloadTrigger`, `AssetViewedTrigger`, `EmailOpenedTrigger`, `TimeElapsedTrigger`, `SuspectViewedTrigger`.
- Para `multiple_conditions`: avalia recursivamente as condições com operador AND/OR. A rule guarda estado de "condições já satisfeitas neste session" (em `CaseSessionState.MultiConditionProgress`).
- Para cada `rule` cujo trigger casa, dispara **todas** as actions, em ordem. Idempotência: marca `rule.id` como disparada em `CaseSessionState.FiredRuleIds`. Não dispara duas vezes.
- Implementação das actions:
  - `reveal_email`/`reveal_asset`/`reveal_suspect`: marca em `CaseSessionState.RevealedEmailIds`/`RevealedAssetIds`/`RevealedSuspectIds` e emite SignalR `case.entity.revealed`.
  - `add_email_attachment`: persiste em `CaseSessionState.EmailAttachmentOverrides[emailId] += assetId` e emite SignalR.
  - `send_notification`: persiste em `CaseSessionState.Notifications` e emite SignalR `case.notification`.
  - `update_suspect_status`: persiste em `CaseSessionState.SuspectStatusOverrides[suspectId] = status`.
  - `mark_alibi_verified`: idem `SuspectAlibiVerified[suspectId] = true`.
- Logs estruturados com `caseId`, `ruleId`, `userId`.

### 3.6 ForensicsBackgroundService — plugar regras
Arquivo: `Services/ForensicsBackgroundService.cs`.

Após marcar request `completed`:
1. Buscar `forensicOutcome` correspondente a `(inputAssetId, analysisType)` no caso raw.
2. Se `findings == true`:
   - Revelar `resultAssetId` (se houver) e `resultEmailId` (se houver) via `CaseSessionState`.
3. Se `findings == false` ou ausente:
   - Criar email dinâmico baseado em `forensicsDefaults.noFindingsEmail.template` (texto: "Análise concluída sem achados relevantes.") e adicionar à caixa do usuário.
4. **Em qualquer caso**, chamar:
   ```csharp
   await rulesEngine.EvaluateAndApplyAsync(caseId, userId,
       new ForensicsCompleteTrigger(inputAssetId, analysisType), ct);
   ```
5. Persistir tudo em uma única transação (`DbContext.SaveChangesAsync`) e só depois emitir SignalR.

### 3.7 Triggers UI → backend (novos endpoints idempotentes)

- `POST /api/cases/{caseId}/emails/{emailId}/open` — marca email como lido; dispara `email_opened` (uma vez).
- `POST /api/cases/{caseId}/emails/{emailId}/attachments/{assetId}/download` — autoriza download e dispara `attachment_download`.
- `POST /api/cases/{caseId}/assets/{assetId}/view` — dispara `asset_viewed`.
- `POST /api/cases/{caseId}/suspects/{suspectId}/view` — dispara `suspect_viewed`.

Todos verificam autenticação e que o asset/email/suspect está visível ao usuário (senão 403).

### 3.8 `time_elapsed` e `temporalEvents`
- O frontend já tem um `TimeContext` enviando "game time" (verificar). Padronizar para enviar a cada 30s do game-time um `POST /api/cases/{caseId}/time` com `{ gameTimeMinutes }`.
- O endpoint:
  1. Avalia todos `temporalEvents` com `triggerAtMinutes <= gameTimeMinutes` ainda não disparados em session state.
  2. Dispara cada um (criando email/notificação conforme `type/payload`) e marca como disparado.
  3. Dispara `EvaluateAndApplyAsync(... TimeElapsedTrigger(gameTimeMinutes))` para regras `time_elapsed`.

### 3.9 SolutionService + POST /submit

Arquivo: `Services/SolutionService.cs`.

- `Task<SubmitResult> EvaluateAsync(string caseId, string userId, SubmitRequest req, CancellationToken ct)`
- `SubmitRequest`:
  ```csharp
  public record SubmitRequest(
      string SuspectId,
      string[] EvidenceIds,
      string[] AnalysisIds,        // formato "{inputAssetId}:{analysisType}"
      Answer[] Answers
  );
  public record Answer(string QuestionId, string OptionId);
  ```
- Avaliação:
  1. `culpritScore` = `partialCreditRules.culpritWeight` se `req.SuspectId == solution.culpritId`, senão 0.
  2. `evidenceScore` = `partialCreditRules.evidenceWeight * |intersect(req.EvidenceIds, solution.requiredEvidenceIds)| / |solution.requiredEvidenceIds|`.
  3. `analysisScore` análogo.
  4. `questionsScore` = `partialCreditRules.questionsWeight * Σ(weight_i * correct_i) / Σ(weight_i)`.
  5. `total = culpritScore + evidenceScore + analysisScore + questionsScore`.
  6. `correct = total >= solution.minimumScore`.
- Persistir em `CaseSubmission` (verificar entidade; se faltar campo `AnswersJson` ou `BreakdownJson`, **uma única** migration consolidada `Migrations/<timestamp>_CaseSubmissionV2.cs`).
- Aplicar `maxAttempts`: se `attemptsRemaining == 0`, retornar 409.
- `SubmitResult`:
  ```csharp
  public record SubmitResult(
      bool Correct,
      double Score,
      ScoreBreakdown Breakdown,
      int AttemptsRemaining,
      string FeedbackText,
      string? ExplanationMarkdown   // só se Correct == true ou attemptsRemaining == 0
  );
  ```
- Endpoint: `POST /api/cases/{caseId}/submit` no `CasesController` (novo). Auth required.

### 3.10 Renomear rotas para `/api/cases`
- `Controllers/CasesV1Controller.cs` → `Controllers/CasesController.cs`, rota `[Route("api/cases")]`.
- Manter as ações: `GET /api/cases/dashboard`, `GET /api/cases/{id}`, `GET /api/cases/{id}/emails`, etc.
- Atualizar **todos** os clientes frontend na Fase 4.

### 3.11 DevCasesController
- Continua restrito a ambiente `Development`.
- Lista cases do filesystem em `cases/` e serve o `case.json` v2 para teste local sem Blob.

---

## Fase 4 — Frontend v2-nativo

### 4.1 `frontend/src/types/caseV2.ts`
- Espelha **apenas** a forma sanitizada de `CaseV2` que chega ao cliente.
- Não declara `solution.correctOptionId`, `rules`, `forensicOutcomes`, `gameMetadata.generation`.
- Tipos: `Asset`, `Email`, `Suspect`, `TimelineEntry`, `Question` (sem `correctOptionId`), `SanitizedSolution`, etc.

### 4.2 `frontend/src/engine/CaseEngine.ts` (novo, v2-native)
- Sem `Map`-based legacy. Estado:
  ```ts
  interface CaseState {
    caseId: string;
    metadata: CaseMetadata;
    visibleAssets: Asset[];
    visibleEmails: Email[];
    visibleSuspects: Suspect[];
    timeline: TimelineEntry[];
    notifications: Notification[];
    submission: {
      attemptsUsed: number;
      maxAttempts: number;
      lastResult?: SubmitResult;
    };
  }
  ```
- Pub/sub simples (`subscribe(cb)`) ou `useSyncExternalStore`.
- Integração:
  - `loadCase(caseId)` → `GET /api/cases/{id}` (sanitized).
  - SignalR connection escuta `case.entity.revealed`, `case.notification`, `case.email.attached` e atualiza estado.
  - `viewAsset(assetId)` → `POST /api/cases/{id}/assets/{assetId}/view`.
  - `openEmail(emailId)` → `POST /.../emails/{id}/open`.
  - `viewSuspect(suspectId)` → `POST /.../suspects/{id}/view`.
  - `requestForensic(assetId, type)` → `POST /api/forensics`.
  - `submit(payload)` → `POST /api/cases/{id}/submit` + atualiza `submission`.

### 4.3 `frontend/src/contexts/CaseContext.tsx`
- Reescrito em cima do engine novo.
- Expor: `useCase()`, `useEmails()`, `useAssets()`, `useSuspects()`, `useSubmission()`.
- Conecta SignalR em `useEffect`, desconecta em cleanup.

### 4.4 `frontend/src/services/api.ts`
- Atualizar URLs para `/api/cases` (sem `/v1`).
- Adicionar:
  - `viewAsset(caseId, assetId)`
  - `viewSuspect(caseId, suspectId)`
  - `openEmail(caseId, emailId)`
  - `downloadAttachment(caseId, emailId, assetId)`
  - `submitCase(caseId, payload): Promise<SubmitResult>`
  - `postGameTime(caseId, gameTimeMinutes)`

### 4.5 `Desktop.tsx`
- Remover fetches diretos duplicados (assetsApi/emailsApi/forensicsApi).
- Consumir tudo via `useCase`/`useEmails`/`useAssets`.
- Disparar `viewAsset`/`viewSuspect`/`openEmail` nos lugares certos:
  - `EmailApp`: ao abrir um email, chamar `openEmail` se não foi aberto antes.
  - `FileViewer`: ao abrir asset, chamar `viewAsset` uma vez por sessão.
  - `InterrogationModule` (ou tela de suspeito): chamar `viewSuspect`.

### 4.6 `components/apps/SubmitCase.tsx` — reescrita
- Estado controlado:
  - `suspectId` (select de `case.visibleSuspects`).
  - `evidenceIds` (checkboxes dos `case.visibleAssets`).
  - `analysisIds` (checkboxes das análises feitas — vir de `useSubmission` ou `useForensics`).
  - `answers` (radio por pergunta de `case.solution.questions`).
- Botão "Submit" → `submitCase(...)` → exibe resultado (correct/score/breakdown/feedback).
- Renderiza `attemptsRemaining`. Quando == 0, desabilita botão e mostra `explanation` (se vier).
- Todas as strings via i18n.

### 4.7 `DashboardPage.tsx`
- Consome `GET /api/cases/dashboard`.
- Remove qualquer lista hardcoded.
- Renderiza `metadata.title`, `location`, `difficulty`, `requiredRank`, `tags`.

### 4.8 `DesktopPage.tsx`
- Remover fallback `CASE-2024-001`.
- Se não houver caso na URL/contexto, redirecionar para `/dashboard`.
- Se caso não existir/não autorizado, redirecionar para `/dashboard` com toast de erro.

### 4.9 i18n — strings novas (4 idiomas)
Strings mínimas a adicionar em `frontend/src/locales/{en-US,pt-BR,es-ES,fr-FR}.ts`:

- `submitCase.title`
- `submitCase.suspectLabel`
- `submitCase.evidenceLabel`
- `submitCase.analysisLabel`
- `submitCase.questionsHeader`
- `submitCase.submitButton`
- `submitCase.attemptsRemaining` (com placeholder `{n}`)
- `submitCase.attemptsExhausted`
- `submitCase.result.correct`
- `submitCase.result.incorrect`
- `submitCase.score` (placeholder `{score}`)
- `submitCase.breakdown.culprit`
- `submitCase.breakdown.evidence`
- `submitCase.breakdown.analysis`
- `submitCase.breakdown.questions`
- `submitCase.explanationHeader`
- `dashboard.requiredRank`
- `dashboard.difficulty`
- `notifications.entityRevealed.email`
- `notifications.entityRevealed.asset`
- `notifications.entityRevealed.suspect`

Tradução: usar formulação natural em cada idioma; não traduzir literalmente termos técnicos (ex.: manter "Detective" como rank). Se em dúvida, manter em inglês com comentário `// TODO i18n review`.

---

## Fase 5 — Documentação

Pode rodar em paralelo com a Fase 4 a partir do momento em que a Fase 1 acaba.

### 5.1 Apagar
- `docs/OBJETO_CASO.md`
- `docs/CASE_JSON_V1_SPEC.md`
- Qualquer `README` interno (ex.: `backend/CaseZeroApi/README_AI_GENERATION.md`) que verse sobre geração no backend.

### 5.2 Reescrever
- `GDD-BR/04-ESTRUTURA-DE-CASO.md` — descrever a forma v2 (assets/emails/suspects/timeline/temporalEvents/rules/forensicOutcomes/solution/gameMetadata). **Espelhar a spec** sem duplicar — pode referenciar `docs/CASE_JSON_V2_SPEC.md` como fonte da verdade.
- `GDD-BR/09-ESQUEMA-DE-DADOS.md` — alinhar 100%. Adicionar exemplos curtos.
- `GDD/04-CASE-STRUCTURE.md` e `GDD/09-DATA-SCHEMA.md` — versão EN equivalente.
- `docs/BACKEND_ARCHITECTURE.md` — refletir: ausência de `CaseObject*`/`CaseGeneration*`; presença de `CaseV2StorageService`, `CaseV2SanitizerService`, `RulesEngineService` refeito, `SolutionService`, `ForensicsBackgroundService` integrado.
- `docs/FRONTEND_ARCHITECTURE.md` — engine v2, context, SignalR, fluxo Desktop, SubmitCase estruturado.
- `docs/API_COMPLETE.md` — endpoints atuais (incluindo os novos triggers + `/submit`).
- `README.md`:
  - Remove `CASE-2024-001/002/003`.
  - Atualiza fluxo: login → dashboard → desktop → email briefing → file viewer → submit.
  - Aponta para `case_001` v2.
  - Remove qualquer menção a geração de casos via backend (geração mora em `functions/`).

### 5.3 Nota de canon
No topo dos arquivos canônicos (`CASE_JSON_V2_SPEC.md`, `GDD-BR/04`, `GDD-BR/09`, `GDD/04`, `GDD/09`, `BACKEND_ARCHITECTURE.md`, `FRONTEND_ARCHITECTURE.md`, `API_COMPLETE.md`), adicionar:

```md
> **Canonical v2.** Anything in the codebase or in older docs that contradicts this document is stale and should be ignored. Older case formats (v0, v1) were removed.
```

---

## Fase 6 — Testes

### 6.1 Backend
- `CaseV2SanitizerServiceTests` — uma asserção por campo sensível (solution removida, rules removidas, forensicOutcomes removidos, gameMetadata.generation removido, assets/emails/suspects hidden filtrados, hidden + revealed passam).
- `RulesEngineServiceTests` — cenário por par trigger×action; idempotência (rule dispara só uma vez); `multiple_conditions` AND/OR; concorrência básica.
- `VisibilityServiceTests` — Rookie/all_initial revela tudo na 1ª sessão; gated revela só initial; revelações via rules são honradas; combinação.
- `SolutionServiceTests` — acerto total, erro total, parcial (várias combinações), esgotamento de `maxAttempts` (4ª tentativa retorna 409), pontuação confere com `partialCreditRules`.
- `ForensicsBackgroundServiceTests` — quando há `forensicOutcome.findings=true`, revela result; quando false, envia noFindingsEmail; chama `EvaluateAndApplyAsync("forensics_complete")` sempre.

### 6.2 Frontend (vitest)
- `SubmitCase.test.tsx`:
  - Renderiza dropdown de suspects vindos do contexto.
  - Renderiza checkboxes de evidências/análises desbloqueadas.
  - Renderiza perguntas com opções dinâmicas.
  - Envia POST correto com payload esperado (mock `submitCase`).
  - Após resposta, mostra score + breakdown + feedback.
  - Quando `attemptsRemaining == 0`, desabilita botão e mostra explanation.
- `DashboardPage.test.tsx`: renderiza lista vinda da API mockada (sem hardcode).

### 6.3 Smoke manual
Criar `docs/MANUAL_SMOKE_v2.md` com roteiro:

1. `dotnet run` no `CaseZeroApi`, `npm run dev` no frontend.
2. Registrar/logar.
3. Dashboard mostra `case_001`.
4. Abrir o caso → desktop monta.
5. Email briefing aparece na inbox.
6. Abrir asset evidente (initial) no FileViewer — dispara `asset_viewed`.
7. Pedir análise forense `fingerprint` no asset chave → após o tempo configurado (ou via flag de dev `forensicsFastMode`), chega email do lab e novo asset.
8. Submeter solução: escolher culprit, evidências, análises, responder perguntas.
9. Receber score + feedback. Repetir até esgotar tentativas.

---

## Fase 7 — Validação final & commits

### 7.1 Build + lint + test
```bash
cd backend && dotnet build CaseZeroApi/CaseZeroApi.csproj && dotnet test CaseZeroApi.Tests/CaseZeroApi.Tests.csproj
cd ../frontend && npm run build && npm run lint && npm run test:run
```
Todos devem passar. Comparar com `BASELINE.txt`.

### 7.2 Limpar dependências
- `cd frontend && npm prune`.
- Revisar `backend/CaseZeroApi/CaseZeroApi.csproj`: remover pacotes só usados pelos serviços apagados (cliente OpenAI/LLM, etc).
- Apagar `backlog/BASELINE.txt` se foi criado.

### 7.3 Commits (separados por fase)
Sugestão (em inglês, com trailer):

1. `feat(site): define case.json v2 spec, schema and migrate case_001`
2. `chore(site): hard-cut legacy v0 from backend and frontend`
3. `feat(backend): v2 models, sanitizer, rules engine, solution service, triggers`
4. `feat(frontend): v2-native engine, context, submit-case and dashboard`
5. `docs: realign GDD, architecture and README to v2`
6. `test: cover v2 sanitizer, rules engine, solution service, submit UI`
7. `chore: dependency cleanup and baseline removal`

Todos com:
```
Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
```

---

## Continuidade (fora desta entrega)

- **`functions/CaseGen.Functions`** (permanece em .NET 9) precisa passar a emitir `case.json` v2 com `solution`, `timeline`, `temporalEvents`, `forensicOutcomes`, `gameMetadata.generation`, `unlockMode`, `difficulty` enum string. Será um plano separado.
- Atualizar templates de PDF/HTML para refletirem o schema v2.
- Tunar prompts para gerar perguntas estruturadas coerentes com a solução real.

## Riscos & mitigações

- **`Models/Evidence.cs` pode estar acoplado a EF.** Antes de apagar, `grep -r "Evidence" backend/` e ler migrations. Se a tabela for usada, manter classe EF-only sem semântica de gameplay.
- **Migrations EF.** Se um campo novo em `CaseSubmission` for necessário, **uma única** migration consolidada `<timestamp>_CaseSubmissionV2.cs`. Não acumular migrations.
- **Mudança de rotas `/api/cases/v1` → `/api/cases`.** Como é hard cut e o frontend é controlado, atualizar todas as chamadas juntas na Fase 4.
- **SignalR e SubmitCase.** Verificar que `SignalRContext` existente já cobre os eventos novos (`case.entity.revealed`, `case.notification`, `case.email.attached`); senão, estender.

## Critérios de aceitação

1. Apenas **um** schema/case documentado (v2) e **um** path no código.
2. `case_001` carrega via dashboard, briefing email aparece, perícia revela email/asset corretamente, submissão estruturada retorna score.
3. Nenhuma referência viva a `evidences[]`, `unlockLogic`, `forensicAnalyses`, `CaseObject*`, `CaseGenerationService` no site.
4. Build/lint/tests passam (backend + frontend).
5. Strings novas presentes em `en-US`, `pt-BR`, `es-ES`, `fr-FR`.
6. Sanitização testada: não vaza `solution`, `rules`, `forensicOutcomes`, `gameMetadata.generation`.
7. README aponta para `case_001` e fluxo correto.

---

## Apêndice A — Lista de arquivos por fase (mapa de operações)

| Operação | Arquivo |
|----------|---------|
| **Criar** | `docs/CASE_JSON_V2_SPEC.md` |
| Criar | `backend/CaseZeroApi/Models/CaseV2/CaseV2.cs` |
| Criar | `backend/CaseZeroApi/Services/SolutionService.cs` (+ interface) |
| Criar | `backend/CaseZeroApi/Controllers/CasesController.cs` (renomeio) |
| Criar | `frontend/src/types/caseV2.ts` |
| Criar | `frontend/src/engine/CaseEngine.ts` (substitui) |
| Criar | `docs/MANUAL_SMOKE_v2.md` |
| **Atualizar** | `schemas/case.schema.json` |
| Atualizar | `cases/case_001/case.json` |
| Atualizar | `backend/CaseZeroApi/Services/RulesEngineService.cs` (reescrita) |
| Atualizar | `backend/CaseZeroApi/Services/ForensicsBackgroundService.cs` |
| Atualizar | `backend/CaseZeroApi/Services/VisibilityService.cs` |
| Atualizar | `backend/CaseZeroApi/Services/CaseV2SanitizerService.cs` (renomeio + extensão) |
| Atualizar | `backend/CaseZeroApi/Services/CaseV2StorageService.cs` (renomeio + extensão) |
| Atualizar | `backend/CaseZeroApi/Program.cs` (DI) |
| Atualizar | `frontend/src/services/api.ts` |
| Atualizar | `frontend/src/contexts/CaseContext.tsx` |
| Atualizar | `frontend/src/pages/Desktop.tsx` (e `DesktopPage.tsx`) |
| Atualizar | `frontend/src/pages/DashboardPage.tsx` |
| Atualizar | `frontend/src/components/apps/SubmitCase.tsx` |
| Atualizar | `frontend/src/locales/{en-US,pt-BR,es-ES,fr-FR}.ts` |
| Atualizar | `README.md` |
| Atualizar | `GDD-BR/04-ESTRUTURA-DE-CASO.md` |
| Atualizar | `GDD-BR/09-ESQUEMA-DE-DADOS.md` |
| Atualizar | `GDD/04-CASE-STRUCTURE.md` |
| Atualizar | `GDD/09-DATA-SCHEMA.md` |
| Atualizar | `docs/BACKEND_ARCHITECTURE.md` |
| Atualizar | `docs/FRONTEND_ARCHITECTURE.md` |
| Atualizar | `docs/API_COMPLETE.md` |
| **Apagar** | `backend/CaseZeroApi/Controllers/CaseObjectController.cs` |
| Apagar | `backend/CaseZeroApi/Services/CaseObjectService.cs` (+ interface) |
| Apagar | `backend/CaseZeroApi/Models/CaseObject.cs` |
| Apagar | `backend/CaseZeroApi/Models/CaseDocument.cs` |
| Apagar | `backend/CaseZeroApi/Models/TemporalEvent.cs` (legacy) |
| Apagar | `backend/CaseZeroApi/Models/NormalizedCaseBundle.cs` (verificar) |
| Apagar | `backend/CaseZeroApi/Models/Evidence.cs` (verificar EF) |
| Apagar | `backend/CaseZeroApi/Controllers/CaseGenerationController.cs` |
| Apagar | `backend/CaseZeroApi/Services/CaseGenerationService.cs` (+ interface) |
| Apagar | `backend/CaseZeroApi/Services/CaseGenerationModels.cs` |
| Apagar | `backend/CaseZeroApi/Services/LlmClient.cs` |
| Apagar | `backend/CaseZeroApi/Services/PromptLibrary.cs` |
| Apagar | `backend/CaseZeroApi/README_AI_GENERATION.md` |
| Apagar | `backend/CaseZeroApi.Tests/Controllers/CaseObjectControllerTests.cs` |
| Apagar | `backend/CaseZeroApi.Tests/Services/CaseGenerationServiceTests.cs` |
| Apagar | `backend/CaseZeroApi.Tests/Services/CaseGenerationIntegrationTests.cs` |
| Apagar | `frontend/src/types/case.ts` |
| Apagar | `frontend/src/types/caseV1.ts` |
| Apagar | `frontend/src/services/caseDataService.ts` |
| Apagar | `frontend/src/pages/CaseGeneratorAIPage.tsx` |
| Apagar | `frontend/src/pages/GenerateCasePage.tsx` |
| Apagar | `cases/samples/case.sample.json` |
| Apagar | `docs/OBJETO_CASO.md` |
| Apagar | `docs/CASE_JSON_V1_SPEC.md` |

## Apêndice B — Glossário rápido

- **asset:** qualquer arquivo de mídia/documento exibido no FileViewer. ID `asset.<slug>`.
- **email:** mensagem na inbox do detetive. ID `email.<slug>`.
- **suspect:** ficha de suspeito. ID `suspect.<slug>`.
- **rule:** regra `trigger → actions[]` avaliada server-side. ID `rule.<slug>`.
- **forensicOutcome:** resultado canônico de uma análise sobre um asset, server-side.
- **session state:** estado por `(userId, caseId)` que guarda revelações, rules disparadas, emails abertos, etc.
- **sanitizer:** serviço que filtra o `CaseV2` antes de enviar ao cliente.

