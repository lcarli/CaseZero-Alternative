# 09 — Esquema de Dados

> **Canônico v2.** Esta página é um **resumo de design** do esquema. A especificação técnica autoritativa é [`docs/CASE_JSON_V2_SPEC.md`](../../CASE_JSON_V2_SPEC.md) (acompanhada do JSON Schema em `schemas/case.schema.json`).

## 9.1 Princípios

- **Um único esquema.** Não existem v0 nem v1 no código vivo do site. Qualquer doc/arquivo que ainda os mencione é obsoleto.
- **`case.json` é a fonte da verdade.** O backend não armazena cópia da lógica do caso em SQL.
- **SQL guarda estado por sessão.** Quais emails foram abertos, quais regras já dispararam, quais análises foram solicitadas, qual o resultado da submissão.

## 9.2 Visão geral do `case.json` v2

```jsonc
{
  "version": "2.0",
  "caseId": "case_001",
  "metadata":     { /* dificuldade, rank, unlockMode, ... */ },
  "assets":       [ /* visíveis ao detetive (FileViewer) */ ],
  "emails":       [ /* inbox */ ],
  "suspects":     [ /* fichas */ ],
  "timeline":     [ /* eventos narrativos verificados */ ],
  "temporalEvents":   [ /* dispara por tempo de jogo (server-only) */ ],
  "rules":            [ /* trigger → actions (server-only) */ ],
  "forensicsDefaults":{ /* tipos de análise + email "sem achados" */ },
  "forensicOutcomes": [ /* resultado canônico de cada análise (server-only) */ ],
  "solution":         { /* culpado + perguntas + pontuação (server-only) */ },
  "gameMetadata":     { /* tags, content warnings, info de geração */ }
}
```

A forma exata de cada bloco está em `docs/CASE_JSON_V2_SPEC.md` §§ 2-12.

## 9.3 Sensibilidade (matriz)

| Campo | Cliente vê? |
|-------|-------------|
| `metadata.*` | sim |
| `assets`/`emails`/`suspects` visíveis | sim |
| `assets`/`emails`/`suspects` ocultos | só após revelados via regras/perícia |
| `timeline` | sim |
| `temporalEvents` | apenas o efeito (memo/email) |
| `rules` | **não** |
| `forensicsDefaults.analysisTypes` | sim |
| `forensicOutcomes` | **não** |
| `solution.culpritId / required* / correctOptionId / explanation / partialCreditRules` | **não** |
| `solution.questions[].{id,prompt,options[].{id,label},weight}` | sim |
| `gameMetadata.generation` | **não** |
| `gameMetadata.*` (resto) | sim |

A sanitização é feita pelo `CaseV2SanitizerService` (backend). Há testes unitários cobrindo cada item dessa matriz.

## 9.4 Estado por sessão (SQL)

Tabelas principais (entidades EF em `backend/CaseZeroApi/Models/`):

| Tabela | Papel |
|--------|-------|
| `CaseSession` | Sessão de um detetive jogando um caso (start time, status) |
| `CaseSessionVisibleAsset` | Quais assets já foram revelados por sessão |
| `CaseSessionVisibleEmail` | Idem para emails |
| `CaseSessionEmailState` | Email aberto/fechado, marcadores |
| `EmailAttachmentDownloaded` | Downloads de anexo (anti-replay e auditoria) |
| `ForensicRequest` | Pedidos de perícia (com timer, status) |
| `ForensicAnalysis` | Resultado materializado |
| `CaseSubmission` | Tentativas de solução do detetive |
| `Note` | Notas pessoais do detetive |
| `AuditLog` | Trilha de auditoria geral |

Quem precisar de mais detalhe deve consultar `backend/CaseZeroApi/Data/ApplicationDbContext.cs` — esta tabela é o snapshot vivo.

## 9.5 Validação

Qualquer `case.json` deve:

1. Passar no JSON Schema (`schemas/case.schema.json`).
2. Ter `version == "2.0"` e `caseId` casando o nome da pasta.
3. Ter pelo menos um email `visibility: "initial"`.
4. Referenciar IDs existentes em todas as `rules`, `forensicOutcomes`, `solution`.
5. Ter `solution.partialCreditRules` somando 1.0.
6. Ter `solution.questions[].correctOptionId` pertencendo a `options`.
7. Ter `solution.requiredAnalysisIds` em formato `<assetId>:<analysisType>`.

O backend recusa (HTTP 422) cases que falhem na validação ao subir.
