# Chapter 09 - Data Schema & Models

**Game Design Document - CaseZero v3.0**  
**Last updated:** November 19, 2025  
**Status:** ✅ In production

---

## 9.1 Overview

This chapter reflects the storage-first architecture now in place. Every narrative artifact lives inside `case.json`, versioned in Azure Blob Storage, converted in the backend, and delivered to the client through the `CaseData` interface. The relational database only stores dynamic state (access control, progress, sessions, emails) and never duplicates the canonical story content.

---

## 9.2 Data Sources & Flow

### 9.2.1 Case bundle in Blob Storage

Each hosted case follows the same layout produced by `CaseGen.Functions`:

| Item | Description | Consumed by |
| --- | --- | --- |
| `case.json` | Game-ready format (`CaseObject` / `CaseData`) | `CaseObjectService` |
| `normalized_case.json` | `NormalizedCaseBundle` with entity graphs and gating info | `CaseFormatService` |
| `manifest.json` | Generation summary with counts (`documents`, `media`, `suspects`) and textual `difficulty` | Dashboard (`CasesController.GetDashboard`) |
| Asset folders (`evidence/`, `forensics/`, `witnesses/`, …) | Binary files referenced via `fileName` or `resultFile` | Download endpoints |

### 9.2.2 NormalizedCaseBundle → CaseObject

`backend/CaseZeroApi/Models/NormalizedCaseBundle.cs` models the rich payload emitted by CaseGen. `CaseFormatService.ConvertToGameFormatAsync` turns that bundle into `CaseObject` (`backend/CaseZeroApi/Models/CaseObject.cs`). The result is persisted as `case.json` inside the blob so the frontend can consume a deterministic schema.

### 9.2.3 Access services

- **`CaseObjectService`**: resolves the case path (`CasesBasePath`), reads `case.json`, validates required folders (`evidence/`, `suspects/`, `forensics/`), and exposes `LoadCaseObjectAsync` (locale aware).
- **`BlobStorageService`**: serves bundles from Azure. Used by `GET /api/cases/{id}/data` whenever the id starts with `CASE-`.
- **`CaseFormatService`**: validates (`ValidateForGameFormatAsync`) and converts a `NormalizedCaseBundle` into the game format.
- **`CasesController`**: sanitizes payloads before sending them to players (solution fields removed).

### 9.2.4 SQL database

| Table | Content | Source |
| --- | --- | --- |
| `Cases` | Operational metadata (status, minimum rank, estimated difficulty, `MaxScore`) | Backoffice |
| `UserCases`, `CaseAccessRules` | Agent assignments and entitlements | Access service |
| `CaseProgresses`, `CaseSessions` | Telemetry and per-user state | Live gameplay |
| `Evidences`, `Suspects` (SQL) | Only unlocked items shown in admin UI | Manually synced |
| `CaseSubmissions`, `Emails` | Final guesses and asynchronous narrative | Web dashboards |

SQL never stores `timeline`, `unlockLogic`, `temporalEvents`, or `gameMetadata`. Those remain in Blob-only files.

---

## 9.3 `case.json` structure

Root object consumed by `CaseObject` / `CaseData`:

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

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `title` | string | ✔ | Display name on the hub |
| `description` | string | ✔ | Story summary |
| `startDateTime` | ISO-8601 string | ✔ | Moment the investigation begins |
| `location` | string | ✔ | Main city or district |
| `incidentDateTime` | ISO-8601 string | ✔ | Time of the crime |
| `victimInfo` | object | optional | `VictimInfo` holds `name`, `age`, `occupation`, `causeOfDeath?` |
| `briefing` | string | ✔ | Commander narration |
| `difficulty` | integer | ✔ | 1-10 scale used in matchmaking |
| `estimatedDuration` | string | ✔ | e.g. `"45min"` |
| `minRankRequired` | string | ✔ | e.g. `"Detective"` |

### 9.3.2 `evidences[]`

| Field | Type | Description |
| --- | --- | --- |
| `id`, `name`, `type`, `category`, `priority` | string | Display metadata |
| `fileName` | string | Relative path inside the bundle |
| `description`, `location` | string | Narrative copy |
| `isUnlocked`, `requiresAnalysis` | bool | Control availability and lab flow |
| `dependsOn`, `linkedSuspects`, `analysisRequired` | string[] | Cross references |
| `unlockConditions` | `CaseUnlockConditions` | See §9.3.6 |

### 9.3.3 `suspects[]`

| Field | Type | Description |
| --- | --- | --- |
| `id`, `name`, `alias` | string | Identity |
| `age`, `occupation` | number/string | Public data |
| `description`, `relationship`, `motive`, `alibi`, `behavior`, `backgroundInfo`, `comments` | string | Detailed profile |
| `alibiVerified`, `isActualCulprit` | bool | `isActualCulprit` never leaves the server |
| `linkedEvidence` | string[] | Evidence IDs |
| `status` | enum | `PersonOfInterest` \| `Suspect` \| `Cleared` |
| `unlockConditions` | object | Same format as evidences |

### 9.3.4 `forensicAnalyses[]`

| Field | Type | Description |
| --- | --- | --- |
| `id` | string | Unique ID (`FA-02`, …) |
| `evidenceId` | string | Reference to an evidence |
| `analysisType` | enum | `DNA`, `Fingerprint`, `DigitalForensics`, `Ballistics` |
| `responseTime` | integer | Minutes the lab needs |
| `resultFile` | string | Relative path to the PDF/image |
| `description` | string | Shown on the forensic tab |

### 9.3.5 `temporalEvents[]` and `timeline[]`

- **Temporal events**: `triggerTime` counts minutes from `metadata.startDateTime`. `type` accepts `memo`, `witness`, `alert`. `fileName` resolves inside the bundle.
- **Timeline**: `CaseTimelineEvent` uses `time` (DateTime on the server, shown as localized string on the client), plus `event` and `source`.

### 9.3.6 `unlockLogic` and `CaseUnlockConditions`

```jsonc
{
  "progressionRules": [ { "condition": "evidenceExamined", "target": "EV-02", "unlocks": ["EV-05"], "delay": 5 } ],
  "analysisRules": [ { "evidenceId": "EV-03", "analysisType": "DNA", "unlocks": ["memo-lab"], "critical": true } ]
}
```

`CaseUnlockConditions` (embedded in evidences/suspects):

| Field | Type | Description |
| --- | --- | --- |
| `immediate` | bool | Available at minute zero |
| `timeDelay` | integer? | Minutes until auto unlock |
| `triggerEvent` | enum | `evidenceExamined` \| `analysisComplete` \| `interviewComplete` |
| `requiredEvidence`, `requiredAnalysis` | string[] | Dependencies |

### 9.3.7 `solution` and `gameMetadata`

- `solution` keeps `culprit`, `keyEvidence[]`, `explanation`, `requiredEvidence[]`, `minimumScore`. The backend reads it to validate submissions, but public endpoints strip it out.
- `gameMetadata` records lineage (`version`, `createdBy`, `createdAt`, `tags[]`, `difficulty`, `estimatedPlayTime`) for auditing and filters.

---

## 9.4 TypeScript interfaces (frontend)

`frontend/src/types/case.ts` is the single source of truth for the client. Key excerpt:

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

Highlights:

- `Evidence` and `Suspect` share `UnlockConditions`.
- `TemporalEvent.triggerTime` remains an integer (minutes) to match `CaseTemporalEvent.TriggerTime`.
- `GameMetadata` mirrors the backend and powers the UI filters.
- Any new field requires corresponding UI copy in all four supported languages plus validation updates.

---

## 9.5 C# models (backend)

`backend/CaseZeroApi/Models/CaseObject.cs` mirrors the JSON with `[JsonPropertyName]` attributes.

- `CaseObjectService.LoadCaseObjectAsync(id, locale?)` first tries the locale-specific path (`/cases/<id>/<locale>/case.json`) and falls back to the default Portuguese bundle.
- `ValidateCaseStructureAsync` checks required directories and file references before returning data.
- `CaseFormatService.ValidateForGameFormatAsync` inspects `normalized_case.json` for missing entities and reports `issues` / `warnings`.

Whenever the schema changes, update `CaseObject`, `CaseData`, and their respective tests in the same pull request.

---

## 9.6 API payloads

`backend/CaseZeroApi/Controllers/CasesController.cs` exposes three data levels:

1. `GET /api/cases` → `CaseDto` list (SQL + manifest) for the case selection screen.
2. `GET /api/cases/{id}` → administrative view backed by SQL (assigned users, progress, unlocked evidence records).
3. `GET /api/cases/{id}/data` → sanitized snapshot of `case.json` (Blob for `CASE-*`, filesystem otherwise).

Sample response (#3):

```json
{
  "caseId": "CASE-2024-001",
  "metadata": { "title": "Operation Riverside", "difficulty": 6, "minRankRequired": "Sergeant" },
  "evidences": [{ "id": "EV-01", "name": "Autopsy Report" }],
  "suspects": [{ "id": "SUSP-02", "name": "Camila Prado", "alibiVerified": false }],
  "forensicAnalyses": [{ "id": "FA-03", "analysisType": "DNA" }],
  "temporalEvents": [{ "id": "memo-chief", "triggerTime": 15 }],
  "timeline": [{ "time": "2024-10-16T01:15:00Z", "event": "Gunshot" }],
  "unlockLogic": { "progressionRules": [] },
  "gameMetadata": { "version": "1.2", "createdBy": "CaseGen" }
}
```

Excluded on purpose: `solution`, `isActualCulprit`, sensitive `linkedEvidence`, and any binary content.

---

## 9.7 Relational database

| Entity | Key fields | Purpose |
| --- | --- | --- |
| `Case` | `Id`, `Title`, `Status`, `Priority`, `MinimumRankRequired`, `EstimatedDifficultyLevel` | Catalog management |
| `CaseProgress` | `UserId`, `CaseId`, `EvidencesCollected`, `CompletionPercentage`, `LastActivity` | Resume gameplay |
| `UserCase` | `UserId`, `CaseId`, `Role` | Access and matchmaking |
| `CaseSession` | `SessionStart`, `SessionEnd`, `GameTimeAtStart/End`, `IsActive` | Time-tracking metrics |
| `Evidence` / `Suspect` (SQL) | Only unlocked data (`IsUnlocked`, `AnalysisStatus`, etc.) | Admin dashboards |
| `CaseSubmission` | `SuspectName`, `KeyEvidenceDescription`, `Score`, `Status` | Evaluate player hypotheses |
| `Email` | `Subject`, `Content`, `Priority`, `Type`, `Attachments` | Asynchronous storytelling |

No table stores timeline, unlock logic, or solution data; those stay inside the blob bundle.

---

## 9.8 Validation & safeguards

1. **Structural validation** – `CaseObjectService.ValidateCaseStructureAsync` checks directories, file existence, and cross references.
2. **Semantic validation** – `CaseFormatService.ValidateForGameFormatAsync` analyzes `normalized_case.json` and returns `issues` / `warnings` for the content team.
3. **Sanitization** – `CasesController.GetCaseData` strips spoiler fields before responding.
4. **Shared typing** – TypeScript (`frontend/src/types/case.ts`) and C# (`CaseObject.cs`) must remain in sync; schema changes require synchronized PRs plus localization updates.

Keeping Chapter 09 in sync with the running code base ensures the design doc accurately represents the storage-first architecture defined in Chapter 08.
