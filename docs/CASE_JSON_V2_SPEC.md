# Case JSON v2 — Specification (Canonical)

> **Canonical v2.** This is the **only** canonical case format used by the CaseZero site. Versions v0 and v1 have been removed from the codebase. Any document that references `evidences[]`, `unlockLogic`, `documents[]`, `forensicReports[]`, or `version` other than `"2.0"` is stale and must be ignored.

This document is the source of truth for the `case.json` shape consumed by the website (`CaseZeroApi` + frontend). The case generator (`functions/CaseGen.Functions`) must emit files conforming to this spec.

---

## 1. Root shape

```jsonc
{
  "version": "2.0",
  "caseId": "case_001",
  "metadata":   { /* see §2 */ },
  "assets":     [ /* §3 */ ],
  "emails":     [ /* §4 */ ],
  "suspects":   [ /* §5 */ ],
  "timeline":   [ /* §6 */ ],
  "temporalEvents":   [ /* §7  — server-side only */ ],
  "rules":            [ /* §8  — server-side only */ ],
  "forensicsDefaults":{ /* §9 */ },
  "forensicOutcomes": [ /* §10 — server-side only */ ],
  "solution":         { /* §11 — server-side only */ },
  "gameMetadata":     { /* §12 */ }
}
```

`version` MUST be the literal string `"2.0"`. `caseId` MUST match the folder name in storage.

---

## 2. `metadata`

```jsonc
{
  "title": "string",                     // required
  "description": "string",               // required
  "location": "string",                  // required
  "incidentDate": "ISO-8601",            // required
  "openedAt": "ISO-8601",                // required — case opening time
  "difficulty": "Rookie | Detective | Detective2 | Sergeant | Lieutenant | Captain | Commander",
  "requiredRank": "Rookie | Detective | Detective2 | Sergeant | Lieutenant | Captain | Commander",
  "unlockMode": "gated | all_initial",   // optional; defaults to "gated"
  "estimatedDurationMinutes": 60,        // optional
  "category": "string",                  // optional — Missing Person, Homicide, etc.
  "briefing": "markdown string",         // optional — short pitch shown in dashboard
  "tags": ["string"]                     // optional
}
```

**Auto rule:** if `requiredRank == "Rookie"`, the backend forces `unlockMode = "all_initial"` regardless of the JSON value.

`difficulty` and `requiredRank` are **enum strings**. Numeric difficulty (e.g. `5`) from v1 is rejected.

---

## 3. `assets[]`

```jsonc
{
  "id": "asset.<slug>",                  // required; pattern ^asset\.[a-z0-9_]+$
  "type": "photo | pdf | audio | video | document | image | digital",
  "title": "string",                     // required — display name
  "description": "string",               // optional
  "uri": "case://<caseId>/<file>",       // required — case://… for local, blob://… for cloud
  "checksum": "sha256:<hex>",            // optional
  "visibility": "initial | hidden",      // required
  "category": "string",                  // optional — Document, Digital, Physical
  "tags": ["string"],                    // optional
  "metadata": { /* free-form, server-side hints (pages, resolution, etc.) */ }
}
```

---

## 4. `emails[]`

```jsonc
{
  "id": "email.<slug>",                  // required
  "from": "string",                      // required — "Name <addr@host>"
  "to": ["string"],                      // required
  "subject": "string",                   // required
  "body": "markdown string",             // required
  "sentAt": "ISO-8601",                  // required
  "priority": "normal | high | urgent",  // optional, default "normal"
  "attachments": ["asset.<slug>"],       // optional
  "visibility": "initial | hidden",      // required
  "metadata": { /* free-form */ }
}
```

The first email with `visibility: "initial"` (or `id == "email.briefing"`) is the case-opening email from the chief.

---

## 5. `suspects[]`

```jsonc
{
  "id": "suspect.<slug>",                // required
  "name": "string",                      // required
  "alias": "string",                     // optional
  "age": 35,                             // optional
  "occupation": "string",                // optional
  "relationship": "string",              // optional — to victim
  "description": "string",               // optional
  "motive": "string",                    // optional
  "alibi": "string",                     // optional
  "alibiVerified": false,                // optional, default false
  "status": "suspect | cleared | confirmed_culprit",  // optional, default "suspect"
  "background": "markdown string",       // optional
  "relatedAssets": ["asset.<slug>"],     // optional
  "photo": "asset.<slug>",               // optional
  "visibility": "initial | hidden"       // required
}
```

`status` and `alibiVerified` may be mutated server-side via `update_suspect_status` and `mark_alibi_verified` actions.

---

## 6. `timeline[]`

Static, narrative timeline of known events. Cliente vê.

```jsonc
{
  "time": "ISO-8601",                    // required
  "event": "string",                     // required
  "source": "investigation | witness | sensor | forensic",
  "sourceAssetId": "asset.<slug>",       // optional
  "verified": true,                      // optional, default false
  "importance": "low | medium | high | critical"
}
```

---

## 7. `temporalEvents[]` (server-side)

Time-driven scripted events keyed off **game time** (minutes since `metadata.openedAt`).

```jsonc
{
  "id": "tevt.<slug>",                   // required
  "triggerAtMinutes": 30,                // required
  "type": "memo | witness | alert | email",
  "payload": {
    "emailId": "email.<slug>",           // when type == "email"
    "assetId": "asset.<slug>",           // when type == "memo" or "alert"
    "message": "string"                  // free-text fallback
  }
}
```

Idempotent: backend marks each `tevt.*` as fired in session state and never replays.

---

## 8. `rules[]` (server-side)

Canonical rule shape. **Any other shape that appeared in older code (e.g. `rules.forensics`) is invalid.**

```jsonc
{
  "ruleId": "rule.<slug>",
  "description": "string",
  "trigger": {
    "type": "forensics_complete | attachment_download | asset_viewed | email_opened | time_elapsed | suspect_viewed | multiple_conditions",
    "inputAssetId": "asset.<slug>",   // forensics_complete, attachment_download, asset_viewed
    "analysisType": "string",         // forensics_complete
    "emailId":   "email.<slug>",      // email_opened, attachment_download
    "suspectId": "suspect.<slug>",    // suspect_viewed
    "assetId":   "asset.<slug>",      // attachment_download, asset_viewed
    "atMinutes": 45,                  // time_elapsed
    "operator":  "AND | OR",          // multiple_conditions
    "conditions": [ /* nested trigger objects */ ]
  },
  "actions": [
    { "type": "reveal_email",          "emailId":   "email.<slug>" },
    { "type": "reveal_asset",          "assetId":   "asset.<slug>" },
    { "type": "reveal_suspect",        "suspectId": "suspect.<slug>" },
    { "type": "add_email_attachment",  "emailId":   "email.<slug>", "assetId": "asset.<slug>" },
    { "type": "send_notification",     "level": "info | warn | critical", "message": "string" },
    { "type": "update_suspect_status", "suspectId": "suspect.<slug>", "status": "suspect | cleared | confirmed_culprit" },
    { "type": "mark_alibi_verified",   "suspectId": "suspect.<slug>", "verified": true }
  ]
}
```

Each rule fires **at most once per session** (idempotent per `(ruleId, sessionId)`).

---

## 9. `forensicsDefaults`

```jsonc
{
  "analysisTypes": [
    { "type": "Fingerprint",      "durationMinutes": 150, "availableFor": ["print","surface","object","photo","document"] },
    { "type": "DNA",              "durationMinutes": 300, "availableFor": ["blood","hair","saliva","tissue","photo"] },
    { "type": "DigitalForensics", "durationMinutes": 360, "availableFor": ["phone","computer","hard_drive","usb","digital"] },
    { "type": "Ballistics",       "durationMinutes": 240, "availableFor": ["bullet","casing","firearm"] }
  ],
  "noFindingsEmail": {
    "template": "markdown with {{analysisType}} and {{assetName}} placeholders",
    "from": "Forensics Lab <lab@citypolice.gov>",
    "subject": "Analysis Results - {{analysisType}} - No Findings"
  }
}
```

The backend matches `analysisType` against `Asset.type` (or `Asset.metadata.physicalType`) via `availableFor`.

---

## 10. `forensicOutcomes[]` (server-side)

Canonical outcome of `(inputAssetId, analysisType)` pairs.

```jsonc
{
  "inputAssetId": "asset.<slug>",        // required
  "analysisType": "string",              // required — must match a forensicsDefaults.analysisTypes[].type
  "findings": true,                      // required
  "matchedSuspectId": "suspect.<slug>",  // optional
  "matchedAssetId":   "asset.<slug>",    // optional
  "conclusionText": "string",            // optional
  "resultAssetId":  "asset.<slug>",      // optional — asset to reveal on findings
  "resultEmailId":  "email.<slug>"       // optional — email to reveal on findings
}
```

Behaviour:
- If `findings == true` and `resultAssetId`/`resultEmailId` set → reveal them via session state.
- If `findings == false` (or no outcome) → backend sends `forensicsDefaults.noFindingsEmail`.
- In **all** cases, `rules[]` with trigger `forensics_complete` matching `(inputAssetId, analysisType)` are evaluated independently.

---

## 11. `solution{}` (server-side)

```jsonc
{
  "culpritId": "suspect.<slug>",                                     // required
  "requiredEvidenceIds": ["asset.<slug>"],                           // required
  "requiredAnalysisIds": ["<inputAssetId>:<analysisType>"],          // required
  "questions": [
    {
      "id": "q.<slug>",                                              // required
      "prompt": "string",                                            // required
      "options": [
        { "id": "opt.<slug>", "label": "string" }
      ],
      "correctOptionId": "opt.<slug>",                               // required, server-only
      "weight": 0.2                                                  // optional, default 1 — relative
    }
  ],
  "explanation": "markdown",                                         // shown only after resolved or attempts exhausted
  "minimumScore": 0.7,                                               // required, 0..1
  "maxAttempts": 3,                                                  // required
  "partialCreditRules": {
    "culpritWeight":   0.4,
    "evidenceWeight":  0.2,
    "analysisWeight":  0.2,
    "questionsWeight": 0.2                                           // weights MUST sum to 1.0
  }
}
```

Sanitised before reaching the client:
- `culpritId`, `requiredEvidenceIds`, `requiredAnalysisIds` → removed.
- `solution.questions[].correctOptionId` → removed.
- `explanation` → removed (sent back only with the final result if `correct == true` or `attemptsRemaining == 0`).
- `partialCreditRules` → removed.

Client receives only: `questions[] { id, prompt, options[] { id, label }, weight }`, `minimumScore`, `maxAttempts`.

### Scoring

```
culpritScore   = partialCreditRules.culpritWeight   if suspectId == culpritId        else 0
evidenceScore  = partialCreditRules.evidenceWeight  * |evidenceIds ∩ requiredEvidenceIds| / |requiredEvidenceIds|
analysisScore  = partialCreditRules.analysisWeight  * |analysisIds ∩ requiredAnalysisIds| / |requiredAnalysisIds|
questionsScore = partialCreditRules.questionsWeight * Σ(weight_i * correct_i) / Σ(weight_i)
total          = culpritScore + evidenceScore + analysisScore + questionsScore
correct        = total >= minimumScore
```

Empty `requiredEvidenceIds` or `requiredAnalysisIds` → that component scores 1.0 (no requirement).

---

## 12. `gameMetadata{}`

```jsonc
{
  "schemaVersion": "2.0",                // required
  "createdAt": "ISO-8601",               // required
  "tags": ["string"],                    // optional
  "contentWarnings": ["violence"],       // optional
  "localizations": ["en-US","pt-BR"],    // optional
  "generation": {                        // server-side only
    "pipelineVersion": "string",
    "model": "string",
    "seed": 12345,
    "bundleChecksum": "sha256:<hex>"
  }
}
```

---

## 13. Sensitivity matrix

| Field                         | Required | Client sees? |
|-------------------------------|----------|--------------|
| `metadata.*`                  | yes      | yes |
| `assets[]` (visibility=initial) | yes    | yes |
| `assets[]` (visibility=hidden)  | yes    | only after revealed |
| `emails[]` (visibility=initial) | yes    | yes |
| `emails[]` (visibility=hidden)  | yes    | only after revealed |
| `suspects[]` (visibility=initial) | yes  | yes |
| `suspects[]` (visibility=hidden)  | yes  | only after revealed |
| `timeline[]`                  | optional | yes |
| `temporalEvents[]`            | yes      | **no** |
| `rules[]`                     | yes      | **no** |
| `forensicsDefaults`           | yes      | partial (only `analysisTypes`) |
| `forensicOutcomes[]`          | yes      | **no** |
| `solution.questions[].id/prompt/options[].id/label` | yes | yes |
| `solution.culpritId / required* / correctOptionId / explanation / partialCreditRules` | yes | **no** |
| `gameMetadata.generation`     | optional | **no** |
| `gameMetadata.*` (rest)       | yes      | yes |

The backend sanitiser (`CaseV2SanitizerService`) is the single source of truth for this matrix and has dedicated unit tests.

---

## 14. Validation checklist

A valid v2 `case.json` MUST:

- [ ] `version == "2.0"` and `caseId` matches the storage folder.
- [ ] At least one email with `visibility: "initial"`.
- [ ] All `assets[]`, `emails[]`, `suspects[]` IDs unique and use the correct prefix.
- [ ] All `attachments`, `relatedAssets`, `linkedAssets`, `rules.*.trigger.*`, `rules.*.actions.*`, `forensicOutcomes[].*`, `solution.*` reference IDs that exist.
- [ ] `metadata.difficulty` and `metadata.requiredRank` are valid enum strings.
- [ ] `solution.partialCreditRules` weights sum to exactly 1.0.
- [ ] Every `solution.questions[].correctOptionId` belongs to that question's `options`.
- [ ] Every `solution.requiredAnalysisIds` entry has the form `<assetId>:<analysisType>`.

The backend rejects (HTTP 422) cases that fail any of the above when uploaded.

---

## 15. Triggers & actions cheat sheet

| Trigger                | Fired by                                                  | Idempotency |
|------------------------|-----------------------------------------------------------|-------------|
| `forensics_complete`   | `ForensicsBackgroundService` when a request completes     | per `(caseId,inputAssetId,analysisType,sessionId)` |
| `attachment_download`  | `POST /api/cases/{c}/emails/{e}/attachments/{a}/download` | per `(emailId,assetId,sessionId)` |
| `asset_viewed`         | `POST /api/cases/{c}/assets/{a}/view`                     | per `(assetId,sessionId)` |
| `email_opened`         | `POST /api/cases/{c}/emails/{e}/open`                     | per `(emailId,sessionId)` |
| `suspect_viewed`       | `POST /api/cases/{c}/suspects/{s}/view`                   | per `(suspectId,sessionId)` |
| `time_elapsed`         | `POST /api/cases/{c}/time` (gameTimeMinutes)              | per `(ruleId,sessionId)` |
| `multiple_conditions`  | composite; satisfied when sub-conditions satisfied        | per `(ruleId,sessionId)` |

| Action                 | Effect                                              |
|------------------------|-----------------------------------------------------|
| `reveal_email`         | Adds emailId to `CaseSessionState.RevealedEmailIds`, SignalR `case.entity.revealed` |
| `reveal_asset`         | Adds assetId to `RevealedAssetIds`                  |
| `reveal_suspect`       | Adds suspectId to `RevealedSuspectIds`              |
| `add_email_attachment` | `EmailAttachmentOverrides[emailId] += assetId`      |
| `send_notification`    | Pushes a notification (level/message)               |
| `update_suspect_status`| `SuspectStatusOverrides[suspectId] = status`        |
| `mark_alibi_verified`  | `SuspectAlibiVerified[suspectId] = verified`        |
