# Case JSON v2 — Specification (Canonical)

> **Canonical raw contract:** this document describes the stored `case.json` payload produced by the v2 generator and validated in-repo. The repo-level schema is `schemas/case.schema.json`. The generator mirror is `functions/CaseGen.Functions/Schemas/case.v2.schema.json`.

## 1. Source-of-truth files

There are two active schema files for v2:

- `schemas/case.schema.json` — public repo schema.

- `functions/CaseGen.Functions/Schemas/case.v2.schema.json` — generator/runtime mirror used by final validation.

They are currently identical **except** that the generator mirror also allows three optional asset classification fields:

- `assets[].evidenceRole`

- `assets[].subjectSuspectId`

- `assets[].imagePurpose`

The generator can emit those fields in raw `case.json` (`CaseV2GeneratorService.BuildAssetNode`), but the backend `CaseV2Asset` model does not surface them to clients.

---

## 2. Root shape

```jsonc
{
  "version": "2.0",
  "caseId": "case_001",
  "metadata": { /* §3 */ },
  "assets": [ /* §4 */ ],
  "emails": [ /* §5 */ ],
  "suspects": [ /* §6 */ ],
  "timeline": [ /* §7 */ ],             // optional in schema
  "temporalEvents": [ /* §8 */ ],       // optional in schema; server-side
  "rules": [ /* §9 */ ],                // required at root; may be []
  "forensicsDefaults": { /* §10 */ },   // required at root
  "forensicOutcomes": [ /* §11 */ ],    // required at root; may be []
  "solution": { /* §12 */ },            // required at root
  "gameMetadata": { /* §13 */ }         // required at root
}
```

### Root requirements

- `version` is a literal `"2.0"`.

- `caseId` must match `^case_[a-z0-9_]+$`.

- Required root properties: `version`, `caseId`, `metadata`, `assets`, `emails`, `suspects`, `rules`, `forensicsDefaults`, `forensicOutcomes`, `solution`, `gameMetadata`.

- `timeline` and `temporalEvents` are optional in schema, although the generator normally writes arrays for both.

- Root `additionalProperties` is `false`.

---

## 3. `metadata`

```jsonc
{
  "title": "string",                  // required, minLength 3
  "description": "string",            // required, minLength 10
  "location": "string",               // required
  "incidentDate": "ISO-8601",         // required, date-time
  "openedAt": "ISO-8601",             // required, date-time
  "difficulty": "Rookie | Detective | Detective2 | Sergeant | Lieutenant | Captain | Commander",
  "requiredRank": "Rookie | Detective | Detective2 | Sergeant | Lieutenant | Captain | Commander",
  "unlockMode": "gated | all_initial",    // optional
  "estimatedDurationMinutes": 90,           // optional, integer 5..600
  "category": "string",                    // optional
  "briefing": "string",                    // optional
  "tags": ["string"],                      // optional
  "victim": {                               // optional
    "name": "string",
    "age": 28,
    "occupation": "string",
    "lastSeen": "ISO-8601"
  }
}
```

Notes:

- `victim` is allowed by both schemas and by the backend model.

- `victim` permits extra properties (`additionalProperties: true`).

- The generator forces `unlockMode = "all_initial"` when `requiredRank == "Rookie"`; otherwise it defaults missing values to `"gated"`.

- Backend listing/availability gates cases by `requiredRank`, not by `difficulty`.

---

## 4. `assets[]`

Canonical schema shape:

```jsonc
{
  "id": "asset.<slug>",               // required, ^asset\.[a-z0-9_]+$
  "type": "photo | pdf | audio | video | document | image | digital | physical",
  "title": "string",                  // required
  "description": "string",            // optional
  "uri": "string",                    // required
  "checksum": "sha256:<hex> | <hex>", // optional, 64 lowercase hex chars with optional prefix
  "visibility": "initial | hidden",   // required
  "category": "string",               // optional
  "tags": ["string"],                 // optional
  "metadata": { /* free-form */ }      // optional, additionalProperties true
}
```

### Generator-side raw extensions

`functions/CaseGen.Functions/Schemas/case.v2.schema.json` also permits these optional fields, and `CaseV2GeneratorService` writes them when present:

```jsonc
{
  "evidenceRole": "primary | corroborative | contextual",
  "subjectSuspectId": "suspect.<slug>",
  "imagePurpose": "scene | suspect_portrait | object | surveillance"
}
```

These are meaningful to the generator/validators (`EvidenceContractValidator`, `AssetRenderingService`, `SolutionSkeletonTask`) but are not part of the backend `CaseV2Asset` model returned to the client.

---

## 5. `emails[]`

```jsonc
{
  "id": "email.<slug>",               // required
  "from": "string",                   // required
  "to": "string | string[]",          // required by schema; generator writes string[]
  "subject": "string",                // required
  "body": "string",                   // required
  "sentAt": "ISO-8601",               // required
  "priority": "normal | high | urgent", // optional
  "visibility": "initial | hidden",   // required
  "attachments": ["asset.<slug>"],    // optional
  "metadata": { /* free-form */ }       // optional
}
```

Notes:

- The backend deserializer accepts either a single string or an array for `to`.

- The generator always emits `email.briefing` with `visibility: "initial"`, `priority: "urgent"`, and array-form `to`.

---

## 6. `suspects[]`

```jsonc
{
  "id": "suspect.<slug>",
  "name": "string",
  "alias": "string",
  "age": 35,
  "occupation": "string",
  "relationship": "string",
  "description": "string",
  "motive": "string",
  "alibi": "string",
  "alibiVerified": false,
  "status": "suspect | cleared | confirmed_culprit",
  "background": "string",
  "relatedAssets": ["asset.<slug>"],
  "photo": "asset.<slug>",
  "visibility": "initial | hidden"
}
```

Required fields are only `id`, `name`, and `visibility`.

Runtime notes:

- `status` and `alibiVerified` can be overridden per user session by rule actions.

- The backend sanitizer returns initial suspects plus any suspects revealed in session state.

---

## 7. `timeline[]`

```jsonc
{
  "time": "ISO-8601",                 // required
  "event": "string",                  // required
  "source": "investigation | witness | sensor | forensic",
  "sourceAssetId": "asset.<slug>",
  "verified": true,
  "importance": "low | medium | high | critical"
}
```

`timeline` is optional at the root, but each item requires `time` and `event`.

---

## 8. `temporalEvents[]` (server-side)

```jsonc
{
  "id": "tevt.<slug>",
  "triggerAtMinutes": 30,
  "type": "memo | witness | alert | email",
  "payload": {
    "emailId": "email.<slug>",
    "assetId": "asset.<slug>",
    "message": "string"
  }
}
```

Notes:

- `temporalEvents` is optional at the root.

- Backend processing is driven from `CaseTriggersController`; the sanitized client contract does not expose this array.

---

## 9. `rules[]` (server-side)

```jsonc
{
  "ruleId": "rule.<slug>",
  "description": "string",
  "trigger": {
    "type": "forensics_complete | attachment_download | asset_viewed | email_opened | time_elapsed | suspect_viewed | multiple_conditions",
    "inputAssetId": "asset.<slug>",
    "analysisType": "string",
    "emailId": "email.<slug>",
    "assetId": "asset.<slug>",
    "suspectId": "suspect.<slug>",
    "atMinutes": 45,
    "operator": "AND | OR",
    "conditions": [ /* nested trigger objects */ ]
  },
  "actions": [
    { "type": "reveal_email", "emailId": "email.<slug>" },
    { "type": "reveal_asset", "assetId": "asset.<slug>" },
    { "type": "reveal_suspect", "suspectId": "suspect.<slug>" },
    { "type": "add_email_attachment", "emailId": "email.<slug>", "assetId": "asset.<slug>" },
    { "type": "send_notification", "level": "info | warn | critical", "message": "string" },
    { "type": "update_suspect_status", "suspectId": "suspect.<slug>", "status": "suspect | cleared | confirmed_culprit" },
    { "type": "mark_alibi_verified", "suspectId": "suspect.<slug>", "verified": true }
  ]
}
```

Notes:

- `rules` is required at the root, but the array may be empty.

- The generator pre-builds mechanical reveal rules, then may append narrative rules.

- Rookie finalization strips `forensics_complete` rules.

---

## 10. `forensicsDefaults`

```jsonc
{
  "analysisTypes": [
    { "type": "string", "durationMinutes": 60, "availableFor": ["string"] }
  ],
  "noFindingsEmail": {
    "template": "string",
    "from": "string",
    "subject": "string"
  }
}
```

Notes:

- `forensicsDefaults` is required at the root.

- `analysisTypes` may be empty (`minItems: 0`), which is the expected Rookie shape.

- `noFindingsEmail` is required in raw `case.json`, but the backend sanitizer exposes only `analysisTypes` to clients.

---

## 11. `forensicOutcomes[]` (server-side)

```jsonc
{
  "inputAssetId": "asset.<slug>",
  "analysisType": "string",
  "findings": true,
  "matchedSuspectId": "suspect.<slug>",
  "matchedAssetId": "asset.<slug>",
  "conclusionText": "string",
  "resultAssetId": "asset.<slug>",
  "resultEmailId": "email.<slug>"
}
```

Required fields: `inputAssetId`, `analysisType`, `findings`.

Notes:

- `forensicOutcomes` is required at the root, but may be `[]`.

- Rookie finalization forces this array to `[]`.

- Backend forensics processing uses this array to reveal result assets/emails or generate the no-findings email.

---

## 12. `solution`

```jsonc
{
  "culpritId": "suspect.<slug>",
  "requiredEvidenceIds": ["asset.<slug>"],
  "requiredAnalysisIds": ["asset.<slug>:AnalysisType"],
  "questions": [
    {
      "id": "q.<slug>",
      "prompt": "string",
      "options": [
        { "id": "opt.<slug>", "label": "string" }
      ],
      "correctOptionId": "opt.<slug>",
      "weight": 1.0
    }
  ],
  "explanation": "string",
  "minimumScore": 0.7,
  "maxAttempts": 3,
  "partialCreditRules": {
    "culpritWeight": 0.4,
    "evidenceWeight": 0.2,
    "analysisWeight": 0.2,
    "questionsWeight": 0.2
  }
}
```

Schema notes:

- `solution` is required at the root.

- Required fields are `culpritId`, `requiredEvidenceIds`, `requiredAnalysisIds`, `questions`, `minimumScore`, `maxAttempts`, `partialCreditRules`.

- `explanation` is optional in schema, but the generator writes it.

- Each question requires `id`, `prompt`, `options`, `correctOptionId`.

- Each question needs at least 2 options in schema; generator tasks usually emit 3-4.

- `weight` is optional and must be `>= 0` when present.

- `requiredAnalysisIds` must match `^asset\.[a-z0-9_]+:[A-Za-z0-9_]+$`.

- Schema constrains each partial-credit weight to `0..1`, but does **not** enforce “sum to 1”; the generator normalizes the default weights before assembly.

### Runtime scoring (`SolutionService`)

```text
culpritScore   = culpritWeight   if suspectId == culpritId else 0
if requiredEvidenceIds is empty:
  evidenceScore = evidenceWeight
else:
  evidenceScore = evidenceWeight * hits / requiredEvidenceIds.Count
if case is Rookie OR requiredAnalysisIds is empty:
  analysisScore = analysisWeight
else:
  analysisScore = analysisWeight * hits / requiredAnalysisIds.Count
questionsScore = questionsWeight * gainedQuestionWeight / totalQuestionWeight
total   = culpritScore + evidenceScore + analysisScore + questionsScore
correct = total >= minimumScore
```

### Attempts behavior

`maxAttempts` now caps **graded** attempts only. Extra submissions after the limit are still accepted and stored, but they are marked `Graded = false` and do not affect promotion progress.

---

## 13. `gameMetadata`

```jsonc
{
  "schemaVersion": "2.0",             // required const
  "createdAt": "ISO-8601",            // required
  "tags": ["string"],                 // optional
  "contentWarnings": ["string"],      // optional
  "localizations": ["en-US"],         // optional
  "generation": {                      // optional
    "pipelineVersion": "string",
    "model": "string",
    "seed": 123,
    "bundleChecksum": "string"
  }
}
```

Notes:

- `gameMetadata` is required at the root.

- Only `schemaVersion` and `createdAt` are required.

- `generation` allows additional properties and is removed from the sanitized client contract.

---

## 14. Raw `case.json` vs sanitized client response

`GET /api/cases/{caseId}` does **not** return raw `case.json`. It returns `CaseV2Sanitized`, which currently contains:

```jsonc
{
  "version": "2.0",
  "caseId": "case_001",
  "metadata": { ... },
  "assets": [ /* visible only */ ],
  "emails": [ /* visible only */ ],
  "suspects": [ /* initial + revealed only */ ],
  "timeline": [ ... ],
  "forensicsDefaults": {
    "analysisTypes": [ ... ]
  },
  "solution": {
    "questions": [
      { "id": "q...", "prompt": "...", "options": [ ... ], "weight": 1.0 }
    ],
    "minimumScore": 0.7,
    "maxAttempts": 3
  },
  "gameMetadata": {
    "schemaVersion": "2.0",
    "createdAt": "...",
    "tags": [ ... ],
    "contentWarnings": [ ... ],
    "localizations": [ ... ]
  }
}
```

Hidden from the client response:

- `temporalEvents`

- `rules`

- `forensicOutcomes`

- `forensicsDefaults.noFindingsEmail`

- `solution.culpritId`

- `solution.requiredEvidenceIds`

- `solution.requiredAnalysisIds`

- `solution.questions[].correctOptionId`

- `solution.explanation`

- `solution.partialCreditRules`

- `gameMetadata.generation`

Visibility behavior is session-aware:

- assets: `visibility == "initial"` or unlocked in `CaseSessionVisibleAssets`

- emails: `visibility == "initial"` or unlocked in `CaseSessionVisibleEmails`, plus attachment overrides/synthetic emails

- suspects: `visibility == "initial"` or revealed in session state

---

## 15. Validation checklist

A current v2 payload should satisfy the following:

### JSON Schema enforced

- [ ] `version == "2.0"`

- [ ] `caseId` matches `^case_[a-z0-9_]+$`

- [ ] `assets`, `emails`, and `suspects` each have at least 1 item

- [ ] `metadata` contains all required fields

- [ ] `gameMetadata.schemaVersion == "2.0"`

- [ ] `solution.questions[].options` has at least 2 items

- [ ] `requiredAnalysisIds` entries match `<assetId>:<analysisType>` format

### Generator / backend enforced beyond schema

- [ ] `caseId` should also match the storage folder name

- [ ] Rookie cases must have `metadata.unlockMode == "all_initial"`

- [ ] Rookie cases must not contain active forensic workflow content

- [ ] culprit-supporting proof must satisfy the selected difficulty topology

- [ ] result assets/emails referenced by forensic outcomes and rules must exist

- [ ] contextual assets must not become required solution evidence

- [ ] partial-credit weights should sum to `1.0` even though schema alone does not require it

---

## 16. Known schema/runtime nuance

The only current schema-file drift is this:

- `schemas/case.schema.json` does **not** list `assets[].evidenceRole`, `assets[].subjectSuspectId`, or `assets[].imagePurpose`

- `functions/CaseGen.Functions/Schemas/case.v2.schema.json` **does** list them

- generator/runtime code understands them

- backend client models currently ignore them

Treat the repo schema as the canonical public contract, and the generator schema as the runtime superset used during generation/final validation.
