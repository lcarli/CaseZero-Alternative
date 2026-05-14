> **Canonical v2.** Anything in older docs that contradicts this document is stale and was removed. The case format itself is specified in [`CASE_JSON_V2_SPEC.md`](./CASE_JSON_V2_SPEC.md).

# Backend Architecture — CaseZero v2

**Project:** `backend/CaseZeroApi` · .NET 8 · ASP.NET Core · SQLite (EF Core)  
**Generation pipeline** lives in `functions/CaseGen.Functions/` (.NET 9) and is **not** part of this document.  
The site reads cases from Azure Blob Storage (production) or `cases/<id>/case.json` (dev/Azurite).

---

## Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 8 / ASP.NET Core |
| ORM | Entity Framework Core (SQLite) |
| Auth | JWT Bearer + ASP.NET Identity |
| Realtime | SignalR (`/hubs/forensics`) |
| Storage | Azure Blob Storage via `Azure.Storage.Blobs` |
| Queue | Azure Storage Queue via `ForensicQueueService` |
| Rate Limit | `AspNetCoreRateLimit` (IP-based, in-memory) |
| ID validation | `IdValidationMiddleware` (request pipeline) |

---

## EF Models (`Models/`)

### Site entities (database)

| Class | Table | Notes |
|-------|-------|-------|
| `User` | Users | ASP.NET Identity user |
| `Case` | Cases | Case registry |
| `UserCase` | UserCases | User ↔ Case join |
| `Evidence` | Evidences | Uploaded evidence files |
| `ForensicAnalysis` | ForensicAnalyses | Completed analyses |
| `ForensicRequest` | ForensicRequests | Pending/completed queue entries |
| `CaseSession` | CaseSessions | Active/paused/completed sessions; 8 JSON columns (see below) |
| `CaseSessionVisibleAsset` | CaseSessionVisibleAssets | Per-user revealed assets |
| `CaseSessionVisibleEmail` | CaseSessionVisibleEmails | Per-user revealed emails |
| `CaseSessionEmailState` | CaseSessionEmailStates | Read/unread state per email |
| `EmailAttachmentDownloaded` | EmailAttachmentDownloaded | Idempotency for attachment downloads |
| `Note` | Notes | Investigator notes |
| `AuditLog` | AuditLogs | Audit trail |
| `CaseSubmission` | CaseSubmissions | Solution attempts; v2 adds `RequestPayloadJson` (JSON) + `AttemptNumber` |

#### `CaseSession` JSON columns (v2 session state)

All eight columns are stored as serialised JSON strings:

| Column | Type | Contents |
|--------|------|----------|
| `FiredRuleIds` | `string?` | `List<string>` — rules already fired (idempotency) |
| `RevealedSuspectIds` | `string?` | `List<string>` |
| `EmailAttachmentOverrides` | `string?` | `Dict<emailId, List<assetId>>` |
| `SuspectStatusOverrides` | `string?` | `Dict<suspectId, string>` |
| `SuspectAlibiVerified` | `string?` | `List<string>` |
| `Notifications` | `string?` | `List<SessionNotification>` |
| `SyntheticEmails` | `string?` | `List<CaseV2Email>` (no-findings emails) |
| `FiredTemporalEventIds` | `string?` | `List<string>` |

### Case v2 POCO model (`Models/CaseV2/`)

Server-side in-memory representation of `case.json v2`. Never written to the database.

| Class | Purpose |
|-------|---------|
| `CaseV2` | Root object: metadata, assets, emails, suspects, timeline, temporalEvents, rules, forensicsDefaults, forensicOutcomes, solution, gameMetadata |
| `CaseV2Metadata` | title, description, location, difficulty, requiredRank, unlockMode, victim, tags… |
| `CaseV2Asset` | id, type, title, uri, visibility, tags, metadata |
| `CaseV2Email` | id, from, to[], subject, body, sentAt, attachments[], visibility |
| `CaseV2Suspect` | id, name, alias, occupation, motive, alibi, visibility |
| `CaseV2Rule` / `CaseV2Trigger` / `CaseV2Action` | Rules engine DSL (server-only, never sent to client) |
| `CaseV2ForensicsDefaults` | analysisTypes[], defaultDurationMinutes |
| `CaseV2ForensicOutcome` | inputAssetId, analysisType, findings, resultEmailId, resultAssetId |
| `CaseV2Solution` | culpritId, requiredEvidenceIds, requiredAnalysisIds, questions, partialCreditRules, maxAttempts, minimumScore |
| `CaseV2GameMetadata` | generation metadata (stripped on sanitise) |
| `CaseV2Sanitized` | Client-safe DTO: no `solution.culpritId`, no `rules`, no `forensicOutcomes`, no `gameMetadata.generation` |

---

## Services (`Services/`)

### Active v2 services

| Service | Interface | Responsibility |
|---------|-----------|---------------|
| `CaseV2StorageService` | `ICaseV2StorageService` | Loads/caches raw `CaseV2` from blob or local `cases/<id>/case.json`; exposes `ListCasesAsync`, `GetRawAsync`, `GetForUserAsync` (sanitised), `CaseExistsAsync` |
| `CaseV2SanitizerService` | `ICaseV2SanitizerService` | Strips server-only fields; merges `CaseSessionVisibleAssets/Emails`, `RevealedSuspectIds`, synthetic emails, and attachment overrides from session state |
| `VisibilityService` | `IVisibilityService` | Applies initial visibility on session start; honours `metadata.unlockMode == all_initial` (or `Rookie` rank) to reveal all entities immediately |
| `RulesEngineService` | `IRulesEngineService` | **v2 method:** `EvaluateAndApplyAsync(caseId, userId, RuleTrigger, ct)` — matches trigger type, applies actions, persists effects idempotently via `CaseSession.FiredRuleIds` |
| `SolutionService` | `ISolutionService` | `POST /api/cases/{id}/submit` — scores against `solution{}` server-side using `partialCreditRules`; enforces `maxAttempts`; persists to `CaseSubmission` |
| `ForensicsBackgroundService` | `IHostedService` | Polls pending `ForensicRequests`; simulates lab delay; generates synthetic no-findings emails when no `forensicOutcome` matches; calls `EvaluateAndApplyAsync(ForensicsCompleteTrigger)`; pushes `ForensicCompleted` via SignalR |
| `BlobStorageService` | `IBlobStorageService` | Lightweight: `ListCases`, `GetCaseManifest`, `GetMediaUrl` — no longer handles case content |
| `ForensicQueueService` | `IForensicQueueService` | Enqueues forensic requests to Azure Storage Queue |
| `JwtService` | — | Token generation/validation |
| `AuditLogService` | `IAuditLogService` | Writes audit rows |
| `DataSeedingService` | — | Seeds dev data on startup |

### Rule trigger types (`RuleTriggers.cs`)

```csharp
ForensicsCompleteTrigger(string InputAssetId, string AnalysisType)
AttachmentDownloadTrigger(string EmailId, string AssetId)
AssetViewedTrigger(string AssetId)
EmailOpenedTrigger(string EmailId)
SuspectViewedTrigger(string SuspectId)
TimeElapsedTrigger(int GameTimeMinutes)
// multiple_conditions evaluated inside EvaluateAndApplyAsync using AND/OR combinator
```

### Legacy v1 services (kept for integration tests — removal in roadmap)

`CaseV1StorageService`, `CaseV1SanitizerService`, and the legacy methods on `IRulesEngineService`  
(`EvaluateForensicRuleAsync`, `ApplyReveal*ActionAsync`, `GenerateNoFindingsEmailAsync`) are still  
present. They are **not** called by any v2 controller.

---

## Controllers (`Controllers/`)

All controllers require `[Authorize]` unless noted.

| Controller | Base Route | Key Actions |
|-----------|-----------|-------------|
| `AuthController` | `POST /api/auth` | `register`, `login`, `verify-email`, `resend-verification` |
| `CasesController` | `GET/POST /api/cases` | `dashboard`, `GET /{id}` (sanitised), `GET /{id}/raw` (Admin), `POST /{id}/submit`, `HEAD /{id}`, list |
| `CaseTriggersController` | `POST /api/cases/{caseId}` | `emails/{e}/open`, `assets/{a}/view`, `suspects/{s}/view`, `emails/{e}/attachments/{a}/download`, `time` |
| `AssetsController` | `GET /api/cases/{caseId}/assets` | list assets, `{assetId}/download` |
| `EmailsController` | `api/cases/{caseId}/emails` | list, `{emailId}`, `{emailId}/open`, `{emailId}/attachments/{assetId}/download` |
| `NotesController` | `api/notes` (controller name) | `GET case/{caseId}`, `GET /{id}`, `POST`, `PUT /{id}`, `DELETE /{id}` |
| `ForensicRequestController` | `api/forensicrequest` (controller name) | CRUD: `GET /{caseId}`, `/{caseId}/pending`, `/{caseId}/{id}`, `POST`, `PUT /{caseId}/{id}`, `DELETE /{caseId}/{id}` |
| `CaseSessionController` | `api/casesession` (controller name) | `POST start`, `POST end/{caseId}`, `GET last/{caseId}`, `GET /{caseId}`, `GET /api/cases/{caseId}/session`, `POST /api/cases/{caseId}/resume`, `DELETE reset-visibility/{caseId}` |
| `DevCasesController` | `api/dev/cases` | Dev environment only: list/serve cases from local filesystem |
| `CasesV1Controller` | `api/cases/v1` | **Deprecated** — kept for integration tests; removal in roadmap |

---

## SignalR Hub

**Address:** `/hubs/forensics`  
**Auth:** `[Authorize]` — clients must pass JWT access token via `accessTokenFactory`.  
Users are auto-joined to a group `user-{userId}` on connect.

| Event (server → client) | Payload | Fired by |
|-------------------------|---------|----------|
| `ForensicCompleted` | `{ id, caseId, evidenceId, analysisType, completedAt }` | `ForensicsBackgroundService` |

> Note: `case.entity.revealed`, `case.notification`, `case.email.attached` events mentioned in earlier drafts were planned but are **not** currently emitted. The engine merges reveals by re-fetching the sanitised case after trigger acknowledgement.

---

## Rate Limiting

`AspNetCoreRateLimit` (IP-based, in-memory):

| Rule | Limit |
|------|-------|
| All endpoints | 60 req / 1 min |
| `*/api/auth/*` | 50 req / 15 min |
| `POST */api/auth/login` | 50 req / 5 min |

Returns HTTP `429` when exceeded.

---

## ID Validation Middleware

`IdValidationMiddleware` validates route parameters against regex patterns:
- `assetId` → `^asset\.[a-zA-Z0-9_\-\.]+$`
- `emailId` → `^email\.[a-zA-Z0-9_\-\.]+$`
- `suspectId` → `^suspect\.[a-zA-Z0-9_\-\.]+$`
- `caseId` → `^(case_[a-zA-Z0-9_\-]+|CASE-[0-9]{8}-[a-f0-9]{8})$`

Invalid IDs → `400 Bad Request`.

---

## Database Migrations

All v2 schema changes are consolidated in:  
`20260101000001_PhaseThreeV2Schema`

---

## Removed (v2 cleanup)

The following were deleted in the Phase 2 cleanup commit and **must not be re-added to the site**:

- `CaseObjectController` / `CaseObjectService`
- `CaseGenerationController` / `CaseGenerationService`
- `LlmClient`, `LlmOptions`, `PromptLibrary`
- `NormalizedCaseBundle`, `CaseDocument`, legacy `TemporalEvent`
- `README_AI_GENERATION.md`

Case generation lives exclusively in `functions/CaseGen.Functions/` (.NET 9).
