# 📋 CaseZero — Backlog

> Backlog ativo. Tarefas concluídas saem daqui (histórico vive nos commits e PRs).

---

## 🔥 Em aberto

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

- `GDD/` (inglês, source-of-truth) — varrer todos os 12 capítulos + 3 apêndices + `TASKS.md`. Especial atenção a:
  - `04-CASE-STRUCTURE.md` — bater contra schema v2 atual
  - `09-DATA-SCHEMA.md` — idem
  - `10-CONTENT-PIPELINE.md` — pipeline v2
  - `08-TECHNICAL.md` — stack atual (sem SignalR/EF na function)
  - `12-ROADMAP.md` — ajustar status / próximos passos

- `GDD-BR/` (português) — espelhar exatamente o que ficou em `GDD/` em inglês,
  preservando o estilo e exemplos já traduzidos. Não traduzir do zero: usar o
  inglês atualizado como referência e adaptar.

**Critérios de aceitação:**

- `grep -rni "case.*v1\|/v1/\|PlanStep\|ExpandStep\|DesignStep\|CaseV1\|casesV1Api\|ICaseV1\|ForensicProcessor"` nos diretórios `docs/`, `GDD/`, `GDD-BR/` retorna **zero** matches (exceto em changelog/history se houver).
- `README.md` (raiz do repo) atualizado com a nova arquitetura.
- Diagramas (se houver no `assets/` ou inline em mermaid) refeitos para refletir só v2.
- GDD inglês e GDD-BR têm conteúdo equivalente.

**Não-objetivos:**

- Não tocar em código (essa task é só documentação).
- Não inventar features novas — só refletir o que existe.
