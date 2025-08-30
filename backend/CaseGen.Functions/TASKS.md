# Visão Kanban

**Colunas sugeridas:**
Backlog → Ready → In Progress → Code Review → QA/Playtest → Done

**WIP (limites):**
In Progress ≤ 3 cartões por dev • Code Review ≤ 5 • QA ≤ 5

**Labels úteis:** `orchestrator`, `activities`, `prompts`, `schemas`, `pdf`, `search`, `i18n`, `validation`, `media`, `observability`

---

## Épico A — Saída estruturada no **Design** (desbloqueia fan-out)

**User Story A1** – Como orquestrador, quero que **Design** retorne **JSON** com `documentSpecs[]` e `mediaSpecs[]` para disparar geração paralela.
**DoD:** `Design` retorna JSON válido; contém `i18nKey`, `gated/gatingRule`, `lengthTarget`; validado por schema.

**Tasks**

1. **Criar Schema `DocumentAndMediaSpecs`**

   * Criar arquivo `Schemas/DocumentAndMediaSpecs.schema.json`.
   * Campos obrigatórios: `documentSpecs[].{docId,type,title,i18nKey,sections[],lengthTarget[],gated,gatingRule?}`, `mediaSpecs[].{evidenceId,kind,title,i18nKey,prompt,constraints?}`.
   * **Aceite:** valida via JSON Schema (lint/validador local).

2. **Endurecer Prompt de Design (JSON-only)**

   * System: “Saída **APENAS JSON** no schema X; **NÃO** explique; use `i18nKey` e `gated`.”
   * User: contexto do caso + `levelProfile` + exemplos curtos.
   * **Aceite:** output sem texto extra; parse sem try/catch “perdoador”.

3. **Service: `DesignCaseAsync` → `GenerateStructuredAsync`**

   * Trocar chamada LLM para usar `response_format: json_schema`.
   * **Aceite:** método rejeita respostas fora do schema; retorna `JObject`.

---

## Épico B — **Fan-out/Fan-in** na geração (docs/mídias/PDF)

**User Story B1** – Como orquestrador, quero criar **1 activity por documento** para paralelizar geração.
**DoD:** Orchestrator faz `fan-out` N docs + M mídias; `fan-in` antes de Normalize/Index.

**Tasks**

1. **Activities unitárias**

   * `GenerateDocumentActivity(spec)` → `markdownPath` (ou payload com `docId`, `markdown`).
   * `GenerateMediaItemActivity(spec)` → salva imagem (quando ativar) e retorna `evidenceId`.
   * **Aceite:** rodar várias instâncias em paralelo sem colisão de arquivos.

2. **(Opcional) `RenderPdfActivity(docId)`**

   * Recebe `markdown` ou caminho; usa Playwright para PDF.
   * **Aceite:** um PDF por doc, sem sobrescrever.

3. **Refatorar Orchestrator**

   * Após `Design`, parsear specs e:

     * `var docTasks = specs.Documents.Select(d => CallActivityAsync("GenerateDocumentActivity", d));`
     * `var mediaTasks = specs.Media.Select(m => CallActivityAsync("GenerateMediaItemActivity", m));`
     * `await Task.WhenAll(docTasks.Concat(mediaTasks));`
   * **Aceite:** tempo total reduzido com múltiplos docs (visível no log).

---

## Épico C — **GenerateDocumentFromSpec** (seções, tamanho, laudo com “Cadeia de Custódia”)

**User Story C1** – Como jogador, quero documentos coerentes e curtos no nível Iniciante.
**DoD:** docs seguem `sections` e `lengthTarget`; laudos têm seção “Cadeia de custódia”.

**Tasks**

1. **Prompt por tipo**

   * `police_report_system.md`, `interview_system.md`, `memo_admin_system.md`, `forensics_report_system.md`.
   * Cada prompt: “Saída APENAS Markdown”, “respeite `sections`, `lengthTarget`”.
   * **Aceite:** doc gerado contém todos os cabeçalhos; tamanho dentro do intervalo.

2. **Service: `GenerateDocumentFromSpecAsync(spec, ctx)`**

   * Constrói payload com `spec` + contexto (`skeleton`,`entities`).
   * **Aceite:** retorna `markdown` puro (sem YAML extra).

3. **Salvar & PDF**

   * Salvar `.md` em `bundles/<caseId>/documents/`.
   * Converter para PDF via `RealPdfRenderer`.
   * **Aceite:** `<docId>.md` e `<docId>.pdf` existem.

---

## Épico D — **GenerateMediaFromSpec** (fotos)

**User Story D1** – Como produtor, quero **prompts** por evidência com metadados (ângulo, iluminação).
**DoD:** gera e salva `{evidenceId}.jpg` OU stub `.prompt.txt` se gerador desativado.

**Tasks**

1. **Prompt base de foto**

   * System: “foto forense realista; **sem pessoas identificáveis**; output = imagem.”
   * **Aceite:** prompt final concatena `constraints`.

2. **Service: `GenerateMediaFromSpecAsync(spec)`**

   * Se `generateImages=false` → cria `evidenceId.prompt.txt`.
   * **Aceite:** arquivo existe com prompt montado.

3. **Activity unitária**

   * `GenerateMediaItemActivity` chama service acima.
   * **Aceite:** paraleliza N itens.

---

## Épico E — **Validation & Rules**

**User Story E1** – Como designer, quero validar **timeline**, **golden facts** e **gating**.
**DoD:** `RuleValidate` produz `{passed, violations[]}`.

**Tasks**

1. **Validators**

   * `CheckTimelineMonotonic`: ISO-8601 ordenado.
   * `CheckGoldenSupportsThreshold`: `minSupports >= supportsPerFact`.
   * `CheckGatingConsistency`: `gated=true` exige `gatingRule`.
   * `CheckCustodyHeaderInPericial`: string obrigatória.
   * **Aceite:** violações legíveis; 0 erros para happy path.

2. **Activity `RuleValidate`**

   * Lê `case.skeleton.json`, `case.specs.json`, e MDs.
   * **Aceite:** JSON com status; log detalhado.

---

## Épico F — **Normalize + Index (Azure AI Search)**

**User Story F1** – Como engine de busca, quero documentos indexados com embeddings.
**DoD:** `NormalizeAndIndex` conclui com sucesso; índice presente.

**Tasks**

1. **Normalize**

   * (stub) limpeza/NER futura; hoje só garante codificação e front-matter mínimo opcional.
   * **Aceite:** sem alteração semântica.

2. **Index**

   * `SearchIndexService.IndexDocumentsAsync(dir, caseId)` (embeddings + upsert).
   * **Aceite:** POST bem-sucedido; logging de quantos docs.

---

## Épico G — **i18n & Naming Policy**

**User Story G1** – Como UX lead, quero títulos com `i18nKey` e sem nomes proibidos.
**DoD:** nenhum doc usa nome da blacklist; todos têm `i18nKey`.

**Tasks**

1. **Blacklist de nomes**

   * Adicionar no **Plan** e **Expand**: “evite \[‘Lucas’, …]”.
   * **Aceite:** verificação simples pós-geração.

2. **`i18nKey` obrigatório**

   * Validar em `Design` e recusar spec sem chave.
   * **Aceite:** build falha do card até ajustar prompt.

---

## Épico H — **Observabilidade & Logging**

**User Story H1** – Como operador, quero saber tempo e custo por etapa.
**DoD:** logs com início/fim por activity, tokens aproximados e custo estimado.

**Tasks**

1. **Timers & métricas simples**

   * Stopwatch por activity, contagem de itens em fan-out.
   * **Aceite:** logs em nível `Info` agregados no final.

2. **Playtest hook**

   * Logar top-N trechos dos docs para QA.
   * **Aceite:** arquivo `preview.log` por caso.

---

## Épico I — **Orquestração de PDFs em paralelo (opcional agora)**

**User Story I1** – Como produtor, quero renderizar PDFs fora da geração de texto.
**DoD:** `RenderPdfActivity` independente; **fan-out** por doc.

**Tasks**

1. **Extrair render**

   * Activity `RenderPdfActivity(docRef)` usa `RealPdfRenderer`.
   * **Aceite:** PDFs iguais aos atuais.

2. **Fan-in antes de Normalize**

   * Espera todos PDF tasks.
   * **Aceite:** Normalize só após todos prontos.

---

## Épico J — **Red Team & Tuning (loop leve)**

**User Story J1** – Como designer, quero detectar casos triviais e endurecer.
**DoD:** gera `redteam.json` com `{steps,coverage,trivial}` e opcional `tuning.delta.json`.

**Tasks**

1. **Prompt Red Team (JSON-only)**

   * Saída `{steps,coverage,trivial,notes[]}`.
   * **Aceite:** parse 100% confiável.

2. **Tune (opcional)**

   * `TuningDelta.actions[]` = `add_noise`, `add_red_herring`, `split_doc`, `gate_doc`.
   * **Aceite:** arquivo salvo; aplicação automática futura.

---

# Backlog (ordenado)

1. **A1.1–A1.3** (schema + prompt de Design + service structured)
2. **B1.1–B1.3** (activities unitárias + orquestrador fan-out/fan-in)
3. **C1.1–C1.3** (prompts por tipo + service doc + salvar + PDF)
4. **D1.1–D1.3** (media prompt + service + activity)
5. **E1.1–E1.2** (validators + activity RuleValidate)
6. **F1.1–F1.2** (normalize + index)
7. **G1.1–G1.2** (blacklist + i18nKey obrigatório)
8. **H1.1–H1.2** (observabilidade)
9. **I1.1–I1.2** (PDF fan-out dedicado)
10. **J1.1–J1.2** (redteam + tuning)

> **Dependências-chave:**
> A → B (Design estruturado antecede fan-out) • C/D dependem de B (ou rodam com wrapper) • F depende de C • E depende de C (e parte de D) • I depende de C.

---

## Critérios de Aceite (globais)

* **Nível Iniciante**: 2–3 suspeitos; 8–14 docs; **ISO-8601** em timeline; 1–2 laudos **gated**; `supportsPerFact=2`.
* **Fan-out/Fan-in**: docs e mídias executam **paralelo**; Orchestrator só segue após **Task.WhenAll**.
* **i18n**: todas as `documentSpecs[].title` e `mediaSpecs[].title` têm `i18nKey`.
* **Laudos**: contêm “**Cadeia de custódia**”.
* **Index**: documentos presentes no índice; busca retorna itens com `caseId` e `docId`.

---

## Definition of Done (DOD)

* Código revisado (CR aprovado).
* Pipelines locais rodam sem erro com um **case.request Iniciante**.
* Bundle gerado em `bundles/<caseId>/` com `.md`, `.pdf`, mídias e `manifest.json`.
* `RuleValidate.passed = true`.
* `RedTeam.trivial = false` (ou `true` + `tuning.delta.json` salvo).
* Documentação/README atualizada.

---