Aqui está o **MD completo** com o TO-DO no topo e os épicos reordenados como você pediu (A e B marcados como feitos). Já inclui as **novas epics C, F, G, H, I** e o **remapeamento** de letras (E→J, F→K e assim por diante).

> Baseado e atualizado a partir do seu TASKS.md anterior.&#x20;

---

# Roadmap & Kanban — CaseGen

## TO-DO (épicos)

| Épico | Título                                                                        | Status | Notas rápidas                              |
| :---: | ----------------------------------------------------------------------------- | :----: | ------------------------------------------ |
| **A** | Design estruturado (JSON `documentSpecs`/`mediaSpecs`)                        | ✅ DONE | Entregue e validando por schema            |
| **B** | Fan-out/Fan-in (docs & mídias)                                                | ✅ DONE | Orchestrator paraleliza por item           |
| **C** | **Schemas de Plan & Expand em arquivos + versionamento de todos os schemas**  | ✅ DONE | Novo — igual ao Design (file-based)        |
| **D** | GenerateDocumentFromSpec (seções, tamanho, “Cadeia de Custódia”)              | ⏳ TODO | Era C, virou D                             |
| **F** | **Geração REAL de imagens (image API) + Fan-out/Fan-in**                      | ⏳ TODO | Novo — executa prompts de mídia            |
| **G** | **Enriquecer prompts de documentos (GenerateDocumentsAsync)**                 | ⏳ TODO | Novo — prompts por tipo mais ricos         |
| **H** | **Padrão de dificuldade (nível→comportamento do prompt e limites)**           | ⏳ TODO | Novo — perfis L1…L5 no prompt e validações |
| **I** | **Revisão do Normalize (clarificar objetivos, saídas, e onde entra o Index)** | ⏳ TODO | Novo — separar Normalize≠Index             |
| **J** | GenerateMediaFromSpec (design de prompts e metadados de mídia)                | ⏳ TODO | Era E, virou J                             |
| **K** | Validation & Rules (timeline, supports, gating, custódia)                     | ⏳ TODO | Era F, virou K                             |
| **L** | Normalize + Index (Azure AI Search)                                           | ⏳ TODO | Era G, virou L                             |
| **M** | i18n & Naming Policy                                                          | ⏳ TODO | Era H, virou M                             |
| **N** | Observability & Logging                                                       | ⏳ TODO | Era I, virou N                             |
| **O** | PDFs em paralelo (RenderPdfActivity fan-out)                                  | ⏳ TODO | Era J, virou O                             |
| **P** | Red Team & Tuning (loop leve)                                                 | ⏳ TODO | Era K, virou P                             |

> Observação sobre letras: **E** foi remapeado para **J** e **F** para **K**; por isso a sequência pula o “E”.

---

## Épico D — GenerateDocumentFromSpec (seções, tamanho, custódia)

**User Story** – Como jogador, quero documentos enxutos e fiéis ao tipo.

**DoD**

* `GenerateDocumentFromSpecAsync` usa prompts por **tipo** (police\_report, interview, memo\_admin, forensics\_report, witness\_statement, evidence\_log).
* Respeita `sections` e `lengthTarget`; laudos contêm **“Cadeia de Custódia”**.
* Fan-out já habilitado no Orchestrator (feito no B).

**Tasks**

1. Prompts por tipo (system) + payload com `spec` e contexto.
2. Activity unitária já criada (B); garantir logs por `docId`.
3. Render PDF opcional por activity (ligado ao O).

---

## Épico F — **Geração REAL de Imagens** (image API) + Fan-out/Fan-in

**User Story** – Como produtor, quero que cada `mediaSpec` gere **arquivo de imagem** real (quando suportado), paralelamente.

**DoD**

* `ImagesService.GenerateAsync(spec)` chamando **image API** (ex.: OpenAI `images/generations`).
* Suporta `photo`, `document_scan`, `diagram`; demais `kind` aceitos com `deferred=true`.
* Activity `GenerateMediaItemActivity` grava `/bundles/<caseId>/media/<evidenceId>.jpg|.png`.
* Orchestrator: fan-out por mídia (já pronto) + fan-in; erros geram `*.error.txt`.

**Tasks**

1. **Service**

   * `ImagesService`: método `GenerateAsync(caseId, spec)` com `size`, `seed`, `safety` (opcional).
   * Montar `genPrompt` = `spec.Prompt` + `constraints` (bullet points).

2. **Activity & Orchestrator**

   * `GenerateMediaItemActivity` usa `ImagesService`.
   * Orchestrator já cria as N tasks; só persista os paths e retorne no fan-in.

3. **Config & Logs**

   * Settings de modelo e size (`OPENAI_MODEL_IMAGE`, `IMAGE_SIZE`).
   * Log por `evidenceId`, tempo, bytes salvos.

---

## Épico G — **Enriquecer prompts** do `GenerateDocumentsAsync`

**User Story** – Como designer narrativo, quero prompts mais ricos, controlados por **tipo** e **nível**.

**DoD**

* Switch por tipo troca prompts simples por **blocos detalhados** (tom, estrutura, estilo).
* Injeção de **constraints** por nível (ver Épico H).
* Evita “solução do caso” e reforça “cadeia de custódia” em laudos.

**Tasks**

1. **Biblioteca de prompts (arquivos `.md`)**

   * `Prompts/doc/police_report_system.md`, `…/interview_system.md`, etc.
   * Cada arquivo traz: objetivo, estilo, seções obrigatórias, anti-padrões.

2. **Template de user payload**

   * Sempre inclui: `spec`, `levelProfile`, `caseId`, `contexto do design resumido`.

3. **LLM service**

   * Preferir `GenerateAsync` com **seed** fixo por doc (reprodutibilidade).

---

## Épico H — **Padrão de Dificuldade**

**User Story** – Como orquestrador, quero um **“DifficultyProfile”** que ajuste prompts e validações por nível (L1…L5).

**DoD**

* `DifficultyProfile` (JSON) mapeia: ranges de `lengthTarget`, nº de suspeitos, nº docs, %noise/redHerring, `stepsToThesis`.
* Services e prompts leem `levelProfile` e adaptam instruções (ex.: L1 = curto, menos jargão; L5 = laudos complexos).
* Validators usam `supportsPerFact` do perfil.

**Tasks**

1. **Modelo & Config**

   * `DifficultyProfile.cs` + `difficulty.v1.json` (carregado via config/DI).
2. **Injeção nos prompts**

   * Interpolar `levelProfile` nos system prompts (D e G).
3. **Validação**

   * `SchemaValidationService` reforça limites por nível (quando aplicável).

---

## Épico I — **Revisão do Normalize** (clarificar etapa)

**User Story** – Como dev, quero separar **Normalize** de **Index** para ficar claro o que cada um faz.

**DoD**

* **Normalize**: limpeza, front-matter opcional (YAML), NER/labels, split em chunks (sem indexar).
* **Index**: embeddings + upsert no Azure AI Search (fica no Épico L).
* Saída do Normalize: `/bundles/<caseId>/documents/*.md` normalizados + manifest.

**Tasks**

1. **NormalizeActivity** (claro e pequeno)

   * “O que fazer”: normalizar e salvar; **não** chamar Search.
2. **Manifest & Logs**

   * `manifest.json` com hashes dos arquivos normalizados.
3. **Docs**

   * Atualizar README explicando Normalize≠Index e ordem no Orchestrator.

---

## Épico J — GenerateMediaFromSpec (design de prompts e metadados)

**User Story** – Como designer de mídia, quero `mediaSpecs` coesos (prompts e constraints executáveis).

**DoD**

* `constraints` em objeto; `deferred=true` para não suportados.
* Validação de `i18nKey` e `kind` suportado.

**Tasks**

1. Prompt base por `kind` (foto/docscan/diagram).
2. Regras de redaction (PII) para docscan.
3. Schema & validação (já coberto no Design e validação de mídia).

---

## Épico K — Validation & Rules

**User Story** – Como designer, quero reprovar bundles inconsistentes.

**DoD**

* `RuleValidate` retorna `{passed, violations[]}` cobrindo: timeline ISO-8601 ordenada; supports mínimos; gating consistente; “Cadeia de Custódia” em laudos.

**Tasks**

1. Checkers (timeline/supports/gating/custódia).
2. Activity + logs legíveis.

---

## Épico L — Normalize + Index (Azure AI Search)

**User Story** – Como engine de busca, quero documentos indexados.

**DoD**

* Embeddings com modelo configurável; upsert no índice; logs de quantos documentos.

**Tasks**

1. `SearchIndexService.IndexDocumentsAsync`.
2. Ajustar dimensionamento (detecta `embedding.Length`).

---

## Épico M — i18n & Naming Policy

**User Story** – Como UX, quero `i18nKey` em todos os títulos e blacklist de nomes sensíveis.

**DoD**

* Falha se faltarem `i18nKey`.
* Prompts recebem `nameBlacklist`.

**Tasks**

1. Validações de i18nKey (regex) no SchemaValidation.
2. Injeção de blacklist em Plan/Expand.

---

## Épico N — Observability & Logging

**User Story** – Como operador, quero tempos, custos e previews.

**DoD**

* Stopwatch por activity; custo estimado; preview de N linhas.

**Tasks**

1. Métricas simples (Info).
2. `preview.log` por caso.

---

## Épico O — PDFs em paralelo

**User Story** – Como produtor, quero PDF por doc sem bloquear.

**DoD**

* `RenderPdfActivity` fan-out por doc; fan-in antes de Normalize.

**Tasks**

1. Extract render do fluxo de documento.
2. Orchestrator: `Task.WhenAll(pdfTasks)`.

---

## Épico P — Red Team & Tuning

**User Story** – Como designer, quero sinalizar casos triviais e endurecer.

**DoD**

* `redteam.json {steps,coverage,trivial,notes[]}`.
* `tuning.delta.json` opcional com ações (add\_noise, add\_red\_herring, split\_doc, gate\_doc).

**Tasks**

1. Prompt JSON-only para RedTeam.
2. Tuning (aplicar depois).

---

### Critérios Globais de Aceite

* L1 (Iniciante): 2–3 suspeitos; 8–14 docs; timestamps ISO-8601; 1–2 laudos **gated**; `supportsPerFact=2`.
* Fan-out/Fan-in: docs e mídias executam em paralelo e convergem antes de Normalize/Index.
* i18n: todos os títulos com `i18nKey`.
* Laudos: incluem “**Cadeia de Custódia**”.

---