# Capítulo 09 - Esquema de Dados & Modelos

**Documento de Design de Jogo - CaseZero v3.0**  
**Última atualização:** 19 de novembro de 2025  
**Status:** ✅ Em produção

---

## 9.1 Visão Geral

Este capítulo descreve com precisão o fluxo de dados vigente após a migração para o modelo **storage-first**. Todo conteúdo narrativo é versionado em `case.json`, armazenado no Azure Blob Storage, convertido no backend e entregue ao frontend por meio da interface `CaseData`. O banco de dados relacional guarda somente estado dinâmico (acesso, progresso, sessões, e-mails) e nunca duplica o conteúdo narrativo.

---

## 9.2 Fontes e Fluxo de Dados

### 9.2.1 Bundle no Blob Storage

Cada caso hospedado em Blob segue a mesma convenção que o gerador (`CaseGen.Functions`) produz:

| Item | Descrição | Consumido por |
| --- | --- | --- |
| `case.json` | Formato pronto para o jogo (`CaseObject` / `CaseData`) | `CaseObjectService` |
| `normalized_case.json` | `NormalizedCaseBundle` com entidades, documentos e grafo de gating | `CaseFormatService` |
| `manifest.json` | Resumo com contagens (`documents`, `media`, `suspects`) e `difficulty` textual | Dashboard (`CasesController.GetDashboard`) |
| Pastas (`evidence/`, `forensics/`, `witnesses/`, etc.) | Binários referenciados por `fileName` ou `resultFile` | APIs de download |

### 9.2.2 NormalizedCaseBundle → CaseObject

`backend/CaseZeroApi/Models/NormalizedCaseBundle.cs` define a estrutura completa que sai do pipeline CaseGen. `CaseFormatService.ConvertToGameFormatAsync` transforma esse bundle em `CaseObject` (`backend/CaseZeroApi/Models/CaseObject.cs`). O resultado é persistido como `case.json` no Blob para servir o frontend de forma determinística.

### 9.2.3 Serviços de acesso

- **`CaseObjectService`**: resolve o caminho do caso (`CasesBasePath`), lê `case.json`, valida diretórios mínimos (`evidence/`, `suspects/`, `forensics/`) e expõe `LoadCaseObjectAsync` (com suporte a `locale`).
- **`BlobStorageService`**: entrega bundles hospedados no Azure. Utilizado nas rotas `GET /api/cases/{id}/data` quando o id começa com `CASE-`.
- **`CaseFormatService`**: valida (`ValidateForGameFormatAsync`) e converte `NormalizedCaseBundle` para o formato do jogo.
- **`CasesController`**: aplica sanitização antes de enviar dados ao cliente (remove solução e campos internos).

### 9.2.4 Banco de dados SQL

| Tabela | Conteúdo | Origem |
| --- | --- | --- |
| `Cases` | Status operacional, rank mínimo, dificuldade estimada, `MaxScore` | Cadastro interno |
| `UserCases`, `CaseAccessRules` | Relação de agentes e permissões | Serviços de acesso |
| `CaseProgresses`, `CaseSessions` | Telemetria e andamento do jogador | Gameplay em tempo real |
| `Evidences`, `Suspects` (SQL) | Apenas itens desbloqueados/exibidos em UI administrativa | Sincronizados manualmente |
| `CaseSubmissions`, `Emails` | Submissões finais e narrativa assíncrona | Painel web |

O SQL não guarda `timeline`, `unlockLogic`, `temporalEvents` nem `gameMetadata`. Esses dados permanecem exclusivamente no Blob.

---

## 9.3 Estrutura do `case.json`

Objeto raiz consumido por `CaseObject`/`CaseData`:

```jsonc
{
  "caseId": "CASE-2024-001",
  "metadata": { /* CaseMetadata */ },
  "evidences": [ /* CaseEvidence[] */ ],
  "suspects": [ /* CaseSuspect[] */ ],
  "forensicAnalyses": [ /* CaseForensicAnalysis[] */ ],
  "temporalEvents": [ /* CaseTemporalEvent[] */ ],
  "timeline": [ /* CaseTimelineEvent[] */ ],
  "solution": { /* CaseSolution */ },
  "unlockLogic": { /* CaseUnlockLogic */ },
  "gameMetadata": { /* CaseGameMetadata */ }
}
```

### 9.3.1 `CaseMetadata`

| Campo | Tipo | Obrigatório | Observações |
| --- | --- | --- | --- |
| `title` | string | ✔ | Texto exibido no hub |
| `description` | string | ✔ | Resumo apresentado ao agente |
| `startDateTime` | string ISO-8601 | ✔ | Início in-mundo |
| `location` | string | ✔ | Cidade/bairro principal |
| `incidentDateTime` | string ISO-8601 | ✔ | Momento exato do crime |
| `victimInfo` | objeto | opcional | `VictimInfo` possui `name`, `age`, `occupation`, `causeOfDeath?` |
| `briefing` | string | ✔ | Mensagem do comandante |
| `difficulty` | inteiro | ✔ | Escala 1-10 usada nas rotas e no dashboard |
| `estimatedDuration` | string | ✔ | Ex.: `"45min"` |
| `minRankRequired` | string | ✔ | Ex.: `"Detective"` |

### 9.3.2 `evidences[]`

| Campo | Tipo | Descrição |
| --- | --- | --- |
| `id`, `name`, `type`, `category`, `priority` | string | Identificadores e metadados exibidos |
| `fileName` | string | Caminho relativo dentro do bundle |
| `description`, `location` | string | Texto narrativo |
| `isUnlocked`, `requiresAnalysis` | bool | Controlam disponibilidade e fluxo com laboratório |
| `dependsOn`, `linkedSuspects`, `analysisRequired` | string[] | Referências cruzadas |
| `unlockConditions` | `CaseUnlockConditions` | Ver seção 9.3.6 |

### 9.3.3 `suspects[]`

| Campo | Tipo | Descrição |
| --- | --- | --- |
| `id`, `name`, `alias` | string | Identidade |
| `age`, `occupation` | number/string | Dados públicos |
| `description`, `relationship`, `motive`, `alibi`, `behavior`, `backgroundInfo`, `comments` | string | Perfil detalhado |
| `alibiVerified`, `isActualCulprit` | bool | `isActualCulprit` fica retido no servidor |
| `linkedEvidence` | string[] | IDs de evidências relacionadas |
| `status` | enum | `PersonOfInterest` \| `Suspect` \| `Cleared` |
| `unlockConditions` | objeto | Mesmo formato de evidências |

### 9.3.4 `forensicAnalyses[]`

| Campo | Tipo | Descrição |
| --- | --- | --- |
| `id` | string | Identificador único (ex.: `FA-02`) |
| `evidenceId` | string | Referência obrigatória a uma evidência |
| `analysisType` | enum | `DNA`, `Fingerprint`, `DigitalForensics`, `Ballistics` |
| `responseTime` | inteiro | Minutos para o laboratório responder |
| `resultFile` | string | Caminho relativo (mesmo container) |
| `description` | string | Resumo exibido na aba forense |

### 9.3.5 `temporalEvents[]` e `timeline[]`

- `temporalEvents`: `triggerTime` é contado em minutos desde `startDateTime`. `type` aceita `memo`, `witness` ou `alert`. `fileName` aponta para o asset exibido quando o evento dispara.
- `timeline`: `CaseTimelineEvent` guarda `time` (DateTime no backend), `event` e `source`. O frontend converte para string local.

### 9.3.6 `unlockLogic` e `CaseUnlockConditions`

```jsonc
{
  "progressionRules": [
    { "condition": "evidenceExamined", "target": "EV-02", "unlocks": ["EV-05"], "delay": 5 }
  ],
  "analysisRules": [
    { "evidenceId": "EV-03", "analysisType": "DNA", "unlocks": ["memo-lab"], "critical": true }
  ]
}
```

`CaseUnlockConditions` usado por evidências/suspeitos inclui:

| Campo | Tipo | Descrição |
| --- | --- | --- |
| `immediate` | bool | Disponível no minuto zero |
| `timeDelay` | inteiro? | Minutos até desbloquear automaticamente |
| `triggerEvent` | enum | `evidenceExamined` \| `analysisComplete` \| `interviewComplete` |
| `requiredEvidence`, `requiredAnalysis` | string[] | IDs necessários |

### 9.3.7 `solution` e `gameMetadata`

- `solution` contém `culprit`, `keyEvidence[]`, `explanation`, `requiredEvidence[]`, `minimumScore`. O backend carrega esses dados para validar submissões, mas a rota pública nunca os envia.
- `gameMetadata` descreve genealogia do caso (`version`, `createdBy`, `createdAt`, `tags[]`, `difficulty`, `estimatedPlayTime`). É usado para auditoria e filtros no UI.

---

## 9.4 Interfaces TypeScript (frontend)

`frontend/src/types/case.ts` é a fonte única para o cliente. Os trechos principais:

```ts
export interface CaseData {
  caseId: string
  metadata: CaseMetadata
  evidences: Evidence[]
  suspects: Suspect[]
  forensicAnalyses: ForensicAnalysis[]
  temporalEvents: TemporalEvent[]
  timeline: TimelineEvent[]
  solution: CaseSolution
  unlockLogic: UnlockLogic
  gameMetadata: GameMetadata
}
```

Outros pontos relevantes:

- `Evidence` e `Suspect` compartilham `UnlockConditions`.
- `TemporalEvent.triggerTime` é inteiro (minutos) e deve continuar alinhado com `CaseTemporalEvent.TriggerTime` (int).
- `GameMetadata` replicado diretamente do backend para permitir exibição de `createdAt`/`tags` no UI.
- Novos campos exigem traduções em quatro idiomas (ver instrução global) e validações no front.

---

## 9.5 Modelos C# (backend)

`backend/CaseZeroApi/Models/CaseObject.cs` utiliza `[JsonPropertyName]` e espelha cada campo do `case.json`. Pontos-chave:

- `CaseObjectService.LoadCaseObjectAsync(id, locale?)` busca primeiro a versão localizada e, caso não exista, usa o padrão (PT-BR atualmente).
- `ValidateCaseStructureAsync` garante a existência de diretórios e arquivos referenciados (evidence, forensics, temporal events).
- `CaseFormatService.ValidateForGameFormatAsync` retorna `issues`/`warnings` quando um `NormalizedCaseBundle` não pode virar jogo.

Qualquer alteração em `CaseObject` deve acompanhar mudanças em `frontend/src/types/case.ts` e vice-versa.

---

## 9.6 Payloads expostos pela API

`backend/CaseZeroApi/Controllers/CasesController.cs` entrega dados em três níveis:

1. `GET /api/cases` → usa `CaseDto` + manifest, retornando título, dificuldade e progresso agregado.
2. `GET /api/cases/{id}` → retorna a estrutura administrativa do SQL (usuários, progresso, evidências desbloqueadas).
3. `GET /api/cases/{id}/data` → retorna somente o que está em `case.json`, após sanitização.

Exemplo resumido da rota #3:

```json
{
  "caseId": "CASE-2024-001",
  "metadata": { "title": "Operação Riverside", "difficulty": 6, "minRankRequired": "Sergeant" },
  "evidences": [{ "id": "EV-01", "name": "Relatório de Autópsia" }],
  "suspects": [{ "id": "SUSP-02", "name": "Camila Prado", "alibiVerified": false }],
  "forensicAnalyses": [{ "id": "FA-03", "analysisType": "DNA" }],
  "temporalEvents": [{ "id": "memo-chief", "triggerTime": 15 }],
  "timeline": [{ "time": "2024-10-16T01:15:00Z", "event": "Disparo" }],
  "unlockLogic": { "progressionRules": [] },
  "gameMetadata": { "version": "1.2", "createdBy": "CaseGen" }
}
```

Campos omitidos de propósito: `solution`, `isActualCulprit`, `linkedEvidence` sensível, arquivos binários e qualquer dado exclusivamente operacional.

---

## 9.7 Banco de dados relacional

| Entidade | Campos chave | Função |
| --- | --- | --- |
| `Case` | `Id`, `Title`, `Status`, `Priority`, `MinimumRankRequired`, `EstimatedDifficultyLevel` | Catálogo interno |
| `CaseProgress` | `UserId`, `CaseId`, `EvidencesCollected`, `CompletionPercentage`, `LastActivity` | Restaurar sessões |
| `UserCase` | `UserId`, `CaseId`, `Role` | Controle de acesso e matchmaking |
| `CaseSession` | `SessionStart`, `SessionEnd`, `GameTimeAtStart/End`, `IsActive` | Telemetria de tempo |
| `Evidence` / `Suspect` (SQL) | Campos desbloqueados (`IsUnlocked`, `AnalysisStatus`, etc.) | Painéis administrativos |
| `CaseSubmission` | `SuspectName`, `KeyEvidenceDescription`, `Score`, `Status` | Avaliar hipóteses |
| `Email` | `Subject`, `Content`, `Priority`, `Type`, `Attachments` | Narrativa assíncrona |

Nenhuma tabela contém timeline, unlock logic ou solução. Esses dados continuam exclusivamente no Blob.

---

## 9.8 Validação e Garantia

1. **Validação estrutural** – `CaseObjectService.ValidateCaseStructureAsync` garante integridade de diretórios, arquivos e cross references.
2. **Validação semântica** – `CaseFormatService.ValidateForGameFormatAsync` roda sobre `normalized_case.json` e gera relatórios de `issues`/`warnings` para o time de conteúdo.
3. **Sanitização** – `CasesController.GetCaseData` remove dados sensíveis antes de responder ao cliente.
4. **Tipagem compartilhada** – TypeScript (`frontend/src/types/case.ts`) e C# (`CaseObject.cs`) permanecem sincronizados; qualquer alteração exige PR conjunto e atualização das traduções de UI.

Este capítulo mantém o GDD alinhado ao código real e reforça o modelo storage-first definido no Capítulo 08.
