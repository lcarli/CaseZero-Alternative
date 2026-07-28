# API Reference — CaseZero

This document is the canonical HTTP API inventory for the current backend and CaseGen Function App. The case bundle contract is documented separately in [`CASE_JSON_V2_SPEC.md`](./CASE_JSON_V2_SPEC.md), and generation internals in [`CASE_GENERATION_PIPELINE.md`](./CASE_GENERATION_PIPELINE.md).

## Conventions

- Frontend API base URL: `VITE_API_URL`, normally `http://localhost:5001/api`.
- Protected endpoints require `Authorization: Bearer <jwt>`.
- Registration grants the `PLAYER` role. Case-generation proxy endpoints require `ADMIN`.
- Case content is returned only in the language selected when the case is generated; the API does not translate a generated case.
- Error bodies are not yet fully standardized. Depending on the controller, they may use `error`, `message`, `Message`, ASP.NET validation details, or plain text.

## Authentication

Base route: `/api/auth`. Except for `me`, these endpoints are anonymous.

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/auth/register` | Creates a player account, institutional email, badge, and initial rank history. Accounts are currently auto-verified because outbound verification email is disabled. |
| `POST` | `/api/auth/login` | Authenticates with the institutional email and returns `{ token, user }`. |
| `GET` | `/api/auth/me` | Returns the current authenticated user and roles. |
| `POST` | `/api/auth/verify-email` | Validates a verification token. Retained for compatibility while email delivery is disabled. |
| `POST` | `/api/auth/resend-verification` | Rotates the verification token subject to a five-minute cooldown. Email delivery is currently disabled. |

Registration body:

```json
{
  "firstName": "Jane",
  "lastName": "Doe",
  "personalEmail": "jane@example.com",
  "password": "P@ssw0rd!"
}
```

Successful registration currently returns `200`:

```json
{
  "message": "Registro realizado com sucesso! Conta ativada automaticamente.",
  "policeEmail": "jane.doe@fic-police.gov",
  "personalEmail": "jane@example.com"
}
```

Login body:

```json
{
  "email": "jane.doe@fic-police.gov",
  "password": "P@ssw0rd!"
}
```

## Cases

Base route: `/api/cases`. All endpoints require authentication.

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/cases/dashboard` | Rank-filtered case catalog, player statistics, difficulty progress, promotion progress, and recent activity. |
| `GET` | `/api/cases` | Lists lightweight metadata for every stored case. |
| `GET` | `/api/cases/{caseId}` | Starts/seeds the session when necessary and returns the sanitized case using current visibility state. |
| `HEAD` | `/api/cases/{caseId}` | Returns `200` when the case exists and `404` otherwise. |
| `GET` | `/api/cases/{caseId}/raw` | Returns the complete unsanitized case. Requires `ADMIN`. |
| `POST` | `/api/cases/{caseId}/submit` | Grades a solution attempt and persists submission/progression data. |

`GET /api/cases/{caseId}` seeds initially visible assets, emails, and suspects. Rookie cases using `unlockMode: all_initial` expose the complete initial dossier.

Submission body:

```json
{
  "suspectId": "suspect.sarah_chen",
  "evidenceIds": ["asset.access_log"],
  "analysisIds": ["asset.device:MetadataAnalysis"],
  "answers": [
    { "questionId": "q.culprit", "optionId": "opt.sarah" }
  ]
}
```

The response is the current `SubmitCaseResult`, including correctness, score, partial-credit breakdown, attempt information, feedback, and explanation when allowed.

## Assets

Base route: `/api/cases/{caseId}/assets`. Authentication and an active case session are required.

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/cases/{caseId}/assets` | Lists only assets visible to the current player session. |
| `GET` | `/api/cases/{caseId}/assets/{assetId}/download` | Streams a visible asset from local case storage or Blob Storage and validates its declared checksum when present. |

Downloading an unrevealed asset returns `403`. Missing active sessions return `404`.

## Case emails

Base route: `/api/cases/{caseId}/emails`. Authentication and case visibility checks apply.

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/cases/{caseId}/emails` | Lists visible case emails with read/open state. |
| `GET` | `/api/cases/{caseId}/emails/{emailId}` | Returns full content and attachments for a visible email. |
| `POST` | `/api/cases/{caseId}/emails/{emailId}/open` | Marks an email as read, increments its open count, audits the action, and fires `email_opened` rules. |
| `POST` | `/api/cases/{caseId}/emails/{emailId}/attachments/{assetId}/download` | Records a valid attachment download, applies visibility/reveal hooks, and fires attachment rules. |

## Case triggers and game time

Base route: `/api/cases/{caseId}`. All endpoints require authentication.

| Method | Path | Trigger/behavior |
|---|---|---|
| `POST` | `/api/cases/{caseId}/assets/{assetId}/view` | Fires `asset_viewed`. |
| `POST` | `/api/cases/{caseId}/suspects/{suspectId}/view` | Fires `suspect_viewed`. |
| `POST` | `/api/cases/{caseId}/time` | Fires `time_elapsed` and reveals temporal-event payloads due by the submitted game time. |

Time body:

```json
{ "gameTimeMinutes": 45 }
```

Time response:

```json
{
  "caseId": "case_example",
  "gameTimeMinutes": 45,
  "firedTemporalEventIds": ["te.witness_arrives"]
}
```

Rule and temporal-event application is idempotent through persisted fired IDs.

## Case sessions

Controller base route: `/api/casesession`. All endpoints require authentication.

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/casesession/start` | Pauses an existing active session, creates a new active session, and applies initial visibility. |
| `POST` | `/api/casesession/end/{caseId}` | Pauses the active session and records duration/game time. |
| `GET` | `/api/casesession/last/{caseId}` | Returns the latest session for the player and case. |
| `GET` | `/api/casesession/{caseId}` | Returns the player's session history for the case. |
| `GET` | `/api/cases/{caseId}/session` | Returns the latest session plus visible asset IDs, visible email IDs, and email state. |
| `POST` | `/api/cases/{caseId}/resume` | Reactivates the most recent paused session. |
| `DELETE` | `/api/casesession/reset-visibility/{caseId}` | Testing helper that clears player visibility and attachment-download state for the case. |

Start body:

```json
{
  "caseId": "case_example",
  "gameTimeAtStart": 0
}
```

End body:

```json
{ "gameTimeAtEnd": 75 }
```

## Notes

Base route: `/api/notes`. Notes are always scoped to the authenticated user.

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/notes/case/{caseId}` | Lists the player's notes for a case. |
| `GET` | `/api/notes/{id}` | Returns one owned note. |
| `POST` | `/api/notes` | Creates `{ caseId, title, content }`; returns `201`. |
| `PUT` | `/api/notes/{id}` | Updates `{ title, content }` on an owned note. |
| `DELETE` | `/api/notes/{id}` | Deletes an owned note; returns `204`. |

## Forensic requests

Base route: `/api/forensicrequest`. All endpoints require authentication and are scoped to the current user.

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/forensicrequest/{caseId}` | Lists all requests for the case. |
| `GET` | `/api/forensicrequest/{caseId}/pending` | Lists requests with `pending` or `in-progress` status. |
| `GET` | `/api/forensicrequest/{caseId}/{id}` | Returns one request. |
| `POST` | `/api/forensicrequest` | Creates a pending request, audits it, and enqueues asynchronous processing. |
| `PUT` | `/api/forensicrequest/{caseId}/{id}` | Updates status, completion, result document, and notes. |
| `DELETE` | `/api/forensicrequest/{caseId}/{id}` | Deletes an owned request. |

The create request must include the case, input asset, and analysis type expected by the case's forensic contract. Completion is pushed through SignalR.

## Global inbox

Base route: `/api/inbox`. This inbox contains user-level messages not tied to a case, such as promotion notices.

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/inbox` | Lists global inbox entries newest first. |
| `GET` | `/api/inbox/unread-count` | Returns `{ unread }`. |
| `POST` | `/api/inbox/{id}/read` | Marks an owned inbox entry as read; returns `204`. |

## Profile statistics

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/profile/stats` | Returns agent identity, promotion snapshot, case history, score by difficulty, complete submission timeline, rank history, and category breakdown. |

The difficulty ladder is `Rookie`, `Detective`, `Detective2`, `Sergeant`, `Lieutenant`, `Captain`, and `Commander`.

## Case generation

### Authenticated backend proxy

Base route: `/api/casegeneration`. Both endpoints require `ADMIN`.

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/casegeneration/generate` | Proxies a generation request to the Function App and normally returns `202`. |
| `GET` | `/api/casegeneration/jobs/{jobId}` | Proxies generation status, progress, stage timing, result, or failure. |

Generation request:

```json
{
  "caseId": "case_generated_example",
  "title": "Optional title hint",
  "theme": "Harbor warehouse homicide",
  "location": "Optional location override",
  "difficulty": "Detective",
  "requiredRank": "Detective",
  "language": "en-US",
  "seed": 423,
  "writeToDisk": true
}
```

Supported difficulty values are the complete seven-level ladder listed above. `language` selects the single language used for the generated case.

The first canonical generation phase is `caseBible`, reported under the public `caseDesign` stage. It establishes the fictional world, people, schedules, locations, identifiers, incident truth, facts, observations, proof paths, decoys, forensic opportunities, and intentional conflicts before plot, dossier, graph, forensic, solution, and rendering phases consume them.

Accepted response:

```json
{
  "jobId": "casev2-20260723215700-abc123",
  "status": "queued",
  "statusUri": "/api/cases/v2/jobs/casev2-20260723215700-abc123"
}
```

Job status includes:

```json
{
  "jobId": "...",
  "status": "queued | running | done | failed",
  "currentPhase": "caseBible",
  "currentStageId": "caseDesign",
  "pipelineVersion": "casegraph-v2",
  "progressPercent": 5,
  "currentAttempt": 2,
  "maxAttempts": 5,
  "nextRetryAt": null,
  "retryReason": null,
  "stages": [],
  "attempts": [],
  "runtimeStatus": "Running",
  "createdAt": "...",
  "lastUpdatedAt": "...",
  "result": null,
  "error": null
}
```

Only one active generation is allowed best-effort. A concurrent request returns `409` with the running job ID.

### Function App routes

The backend proxy calls these Function routes:

| Method | Path | Function authorization |
|---|---|---|
| `POST` | `/api/cases/v2/generate` | `Anonymous`; deployment/network configuration must protect direct access. |
| `GET` | `/api/cases/v2/jobs/{jobId}` | `Anonymous`; deployment/network configuration must protect direct access. |

The Function key can be forwarded through `CaseGenerator:FunctionKey` if authorization is changed to Function level.

## SignalR

- Hub: `/hubs/forensics`
- Authentication: JWT access token.
- Authenticated connections join `user-{userId}`.
- Server event: `ForensicCompleted`.

Example payload:

```json
{
  "id": 42,
  "caseId": "case_example",
  "evidenceId": "asset.device",
  "analysisType": "MetadataAnalysis",
  "completedAt": "2026-07-23T20:00:00Z"
}
```

## Route ID validation

`IdValidationMiddleware` validates recognized route parameters before controllers run.

| Parameter | Accepted format |
|---|---|
| `assetId` | `asset.` followed by letters, numbers, `_`, `-`, or `.` |
| `emailId` | `email.` followed by letters, numbers, `_`, `-`, or `.`; generated `no-findings-<uuid>` is also accepted |
| `suspectId` | `suspect.` followed by letters, numbers, `_`, `-`, or `.` |
| `caseId` | `case_<letters/numbers/_/->` or `CASE-YYYYMMDD-<8 lowercase hex>` |
| attachment ID | `attachment.` followed by letters, numbers, `_`, `-`, or `.` |

Malformed recognized IDs return `400`.

## Common HTTP statuses

| Status | Typical meaning |
|---|---|
| `200` | Successful read, update, action, or submission |
| `201` | Note or forensic request created |
| `202` | Case-generation job queued |
| `204` | Successful delete or mark-read action |
| `400` | Invalid body, ID, or operation |
| `401` | Missing or invalid JWT |
| `403` | Role failure, inaccessible case content, or unrevealed entity |
| `404` | Case, session, note, email, asset, request, or job not found |
| `409` | Another case-generation job is active |
| `429` | IP rate limit exceeded |
| `500` | Unhandled error or integrity failure |
| `502` | Case generator unreachable through the backend proxy |
| `503` | Case generator is not configured |
