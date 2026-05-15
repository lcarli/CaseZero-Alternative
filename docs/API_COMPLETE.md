> **Canonical v2.** Anything in older docs that contradicts this document is stale and was removed. The case format itself is specified in [`CASE_JSON_V2_SPEC.md`](./CASE_JSON_V2_SPEC.md).

# API Reference — CaseZero v2

**Base URL:** `http://localhost:5001/api` (dev) · configured via `VITE_API_URL` in frontend.  
**Auth:** JWT Bearer. Include `Authorization: Bearer <token>` on all protected endpoints.  
All endpoints return JSON. Errors use the standard envelope described at the end of this document.

---

## Authentication — `POST /api/auth/*`

No auth required on any auth endpoint.

### `POST /api/auth/register`
Register a new user. Sends a verification email.

**Body:**
```json
{ "firstName": "Jane", "lastName": "Doe", "personalEmail": "jane@example.com", "password": "P@ssw0rd!" }
```
**Response `201`:** `{ token, user: { id, institutionalEmail, firstName, lastName, rank } }`

---

### `POST /api/auth/login`
**Body:** `{ "email": "jane.doe@fic-police.gov", "password": "P@ssw0rd!" }`  
**Response `200`:** `{ token, user }`  
**Response `401`:** email not verified or wrong credentials.

---

### `POST /api/auth/verify-email`
**Body:** `{ "token": "<verification-token>" }`  
**Response `200`:** `{ message: "Email verified successfully" }`

---

### `POST /api/auth/resend-verification`
**Body:** `{ "email": "jane@example.com" }`  
**Response `200`:** `{ message }`

---

## Cases — `GET/POST /api/cases*`

All case endpoints require auth.

### `GET /api/cases/dashboard`
Returns dashboard stats + list of available cases (from blob or local `cases/`).

**Response `200`:**
```json
{
  "stats": { "casesResolved": 0, "casesActive": 1, "successRate": 0.0, "averageRating": 0.0 },
  "cases": [{ "id", "title", "description", "difficulty", "category", "estimatedDurationMinutes",
               "briefing", "tags", "requiredRank" }],
  "recentActivities": []
}
```

---

### `GET /api/cases`
Returns `CaseV2Metadata2[]` — lightweight list of all known cases.

---

### `GET /api/cases/{caseId}`
Returns sanitised `CaseV2Sanitized` for the authenticated user (merges session-state revealed entities).

**Response `200`:** `CaseV2Sanitized`  
**Response `404`:** `{ error: "Case not found: <caseId>" }`

---

### `HEAD /api/cases/{caseId}`
Existence check. `200` if case exists, `404` otherwise.

---

### `GET /api/cases/{caseId}/raw` _(Admin role required)_
Returns the full unstripped `CaseV2` (includes `rules`, `solution`, `forensicOutcomes`).

---

### `GET /api/cases/{caseId}/assets`
Returns `Asset[]` — visible assets for the user's current session.

---

### `GET /api/cases/{caseId}/emails`
Returns `Email[]` — visible emails for the user's current session.

---

### `POST /api/cases/{caseId}/submit`
Submit a solution attempt.

**Body (`SubmitCaseRequest`):**
```json
{
  "suspectId": "suspect.sarah_chen",
  "evidenceIds": ["asset.blood_report"],
  "analysisIds": ["forensic.dna_001"],
  "answers": [{ "questionId": "q1", "optionId": "opt_b" }]
}
```

**Response `200` (`SubmitCaseResult`):**
```json
{
  "correct": true,
  "score": 87.5,
  "breakdown": { "culprit": 40, "evidence": 25, "analysis": 12.5, "questions": 10 },
  "attemptsRemaining": 2,
  "feedbackText": "Excellent work, Detective.",
  "explanation": "..."
}
```

**Response `409`:** `{ error, maxAttempts }` — attempts exhausted.  
**Response `400`:** invalid request body.

---

## Case Triggers — `POST /api/cases/{caseId}/*`

These endpoints fire the Rules Engine (`EvaluateAndApplyAsync`) for the specified trigger type.  
All are idempotent per session (rules fire at most once per `ruleId`).

### `POST /api/cases/{caseId}/emails/{emailId}/open`
Trigger `email_opened`.  
**Response `200`:** `{ caseId, emailId, trigger: "email_opened" }`

---

### `POST /api/cases/{caseId}/assets/{assetId}/view`
Trigger `asset_viewed`.  
**Response `200`:** `{ caseId, assetId, trigger: "asset_viewed" }`

---

### `POST /api/cases/{caseId}/suspects/{suspectId}/view`
Trigger `suspect_viewed`.  
**Response `200`:** `{ caseId, suspectId, trigger: "suspect_viewed" }`

---

### `POST /api/cases/{caseId}/emails/{emailId}/attachments/{assetId}/download`
Trigger `attachment_download`.  
**Response `200`:** `{ caseId, emailId, assetId, trigger: "attachment_download" }`

---

### `POST /api/cases/{caseId}/time`
Advance in-game clock. Fires `time_elapsed` trigger and processes any `temporalEvents` due at or before `gameTimeMinutes`.

**Body:** `{ "gameTimeMinutes": 45 }`  
**Response `200`:** `{ caseId, gameTimeMinutes, firedTemporalEventIds: ["te.witness_arrives"] }`

---

## Assets — `GET /api/cases/{caseId}/assets`

### `GET /api/cases/{caseId}/assets`
List visible assets (same as `CasesController` shorthand above, served by `AssetsController`).

### `GET /api/cases/{caseId}/assets/{assetId}/download`
Stream / redirect to the asset media file from blob storage.

---

## Emails — `GET/POST /api/cases/{caseId}/emails`

### `GET /api/cases/{caseId}/emails`
List visible emails.

### `GET /api/cases/{caseId}/emails/{emailId}`
Single email detail.

### `POST /api/cases/{caseId}/emails/{emailId}/open`
Mark email as opened (also fires `email_opened` trigger via `EmailsController`).

### `POST /api/cases/{caseId}/emails/{emailId}/attachments/{assetId}/download`
Record attachment download (also fires `attachment_download` trigger via `EmailsController`).

---

## Notes — `api/notes`

### `GET /api/notes/case/{caseId}`
All notes for a case by the authenticated user.

### `GET /api/notes/{id}`
Single note.

### `POST /api/notes`
**Body:** `{ "caseId", "title", "content" }`  
**Response `201`:** `NoteDto`

### `PUT /api/notes/{id}`
**Body:** `{ "title", "content" }`

### `DELETE /api/notes/{id}`
**Response `204`**

---

## Forensic Requests — `api/forensicrequest`

### `GET /api/forensicrequest/{caseId}`
All forensic requests for a case.

### `GET /api/forensicrequest/{caseId}/pending`
Only pending requests.

### `GET /api/forensicrequest/{caseId}/{id}`
Single request by numeric ID.

### `POST /api/forensicrequest`
Create a new forensic request. `ForensicsBackgroundService` polls these, runs the lab simulation, then pushes `ForensicCompleted` via SignalR.

**Body:** `ForensicRequest` entity (see model).

### `PUT /api/forensicrequest/{caseId}/{id}`
Update a request.

### `DELETE /api/forensicrequest/{caseId}/{id}`
Delete a request.

---

## Case Sessions — `api/casesession`

### `POST /api/casesession/start`
Start or resume a session.  
**Body:** `{ "caseId": "case_001" }`

### `POST /api/casesession/end/{caseId}`
End the active session.

### `GET /api/casesession/last/{caseId}`
Most recent session record.

### `GET /api/casesession/{caseId}`
All sessions for a case.

### `GET /api/cases/{caseId}/session`
Active session state (current FiredRuleIds, notifications, etc.).

### `POST /api/cases/{caseId}/resume`
Resume a paused session.

### `DELETE /api/casesession/reset-visibility/{caseId}`
Dev helper: reset all revealed entities for a case.

---

## Dev Cases — `api/dev/cases` _(Development environment only)_

Serves cases directly from the local `cases/` directory. Not available in production.

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/dev/cases` | List available local cases |
| `GET` | `/api/dev/cases/{caseId}` | Sanitised case from local filesystem |
| `GET` | `/api/dev/cases/{caseId}/raw` | Raw (unsanitised) case |

---

## SignalR Hub

**Hub URL:** `/hubs/forensics`  
Clients connect with JWT access token factory. Each authenticated connection is added to group  
`user-{userId}` on connect.

| Event | Direction | Payload | Source |
|-------|-----------|---------|--------|
| `ForensicCompleted` | Server → Client | `{ id, caseId, evidenceId, analysisType, completedAt }` | `ForensicsBackgroundService` |

---

## Error Envelope

All non-2xx responses return a JSON error object:

```json
{ "error": "Human-readable message" }
```

Some conflict responses include additional fields:
```json
{ "error": "Maximum attempts reached", "maxAttempts": 3 }
```

Standard HTTP codes:

| Code | Meaning |
|------|---------|
| `400` | Bad request / validation failure |
| `401` | Missing or invalid JWT |
| `403` | Insufficient role/rank |
| `404` | Resource not found |
| `409` | Conflict (e.g. max attempts exceeded) |
| `429` | Rate limit exceeded |
| `500` | Unhandled server error |

---

## ID Format Constraints

Route parameters are validated by `IdValidationMiddleware`:

| Parameter | Pattern |
|-----------|---------|
| `assetId` | `asset.<alphanum>` |
| `emailId` | `email.<alphanum>` |
| `suspectId` | `suspect.<alphanum>` |
| `caseId` | `case_<alphanum>` or `CASE-YYYYMMDD-<hex8>` |

Invalid formats return `400`.
