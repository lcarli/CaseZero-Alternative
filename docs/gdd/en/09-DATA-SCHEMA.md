# 09 — Data Schema

> **Canonical v2.** This page is a **design summary**. The authoritative technical spec is [`docs/CASE_JSON_V2_SPEC.md`](../../CASE_JSON_V2_SPEC.md) (accompanied by the JSON Schema in `schemas/case.schema.json`).

## 9.1 Principles

- **One schema.** v0 and v1 no longer live in the site code. Any doc/file still referencing them is stale.
- **`case.json` is the source of truth.** The backend does not duplicate case logic into SQL.
- **SQL holds session state.** Which emails were opened, which rules already fired, which analyses were requested, what the submission result was.

## 9.2 `case.json` v2 at a glance

```jsonc
{
  "version": "2.0",
  "caseId": "case_001",
  "metadata":     { /* difficulty, rank, unlockMode, ... */ },
  "assets":       [ /* visible to the detective (FileViewer) */ ],
  "emails":       [ /* inbox */ ],
  "suspects":     [ /* profiles */ ],
  "timeline":     [ /* verified narrative events */ ],
  "temporalEvents":   [ /* fires by game time (server-only) */ ],
  "rules":            [ /* trigger → actions (server-only) */ ],
  "forensicsDefaults":{ /* analysis types + "no findings" email */ },
  "forensicOutcomes": [ /* canonical result of each analysis (server-only) */ ],
  "solution":         { /* culprit + questions + scoring (server-only) */ },
  "gameMetadata":     { /* tags, content warnings, generation info */ }
}
```

Exact shape of each block lives in `docs/CASE_JSON_V2_SPEC.md` §§ 2-12.

## 9.3 Sensitivity matrix

| Field | Client sees? |
|-------|--------------|
| `metadata.*` | yes |
| Visible `assets`/`emails`/`suspects` | yes |
| Hidden `assets`/`emails`/`suspects` | only after revealed by rules/forensics |
| `timeline` | yes |
| `temporalEvents` | only the effect (memo/email) |
| `rules` | **no** |
| `forensicsDefaults.analysisTypes` | yes |
| `forensicOutcomes` | **no** |
| `solution.culpritId / required* / correctOptionId / explanation / partialCreditRules` | **no** |
| `solution.questions[].{id,prompt,options[].{id,label},weight}` | yes |
| `gameMetadata.generation` | **no** |
| Other `gameMetadata.*` | yes |

Sanitisation is performed by `CaseV2SanitizerService` (backend). Unit tests cover each row.

## 9.4 Session state (SQL)

Main tables (EF entities under `backend/CaseZeroApi/Models/`):

| Table | Role |
|-------|------|
| `CaseSession` | A detective's playthrough of a case (start time, status) |
| `CaseSessionVisibleAsset` | Which assets have been revealed in the session |
| `CaseSessionVisibleEmail` | Same for emails |
| `CaseSessionEmailState` | Email read/unread markers |
| `EmailAttachmentDownloaded` | Attachment downloads (anti-replay & audit) |
| `ForensicRequest` | Pending forensic requests (with timer, status) |
| `ForensicAnalysis` | Materialised result |
| `CaseSubmission` | Detective's solution attempts |
| `Note` | Detective's private notes |
| `AuditLog` | General audit trail |

For more detail, see `backend/CaseZeroApi/Data/ApplicationDbContext.cs` — that file is the live snapshot.

## 9.5 Validation

Every `case.json` must:

1. Pass the JSON Schema (`schemas/case.schema.json`).
2. Have `version == "2.0"` and `caseId` matching the folder name.
3. Have at least one email with `visibility: "initial"`.
4. Reference existing IDs in every `rules`, `forensicOutcomes`, `solution`.
5. Have `solution.partialCreditRules` weights summing to 1.0.
6. Have each `solution.questions[].correctOptionId` belong to that question's `options`.
7. Have every `solution.requiredAnalysisIds` entry in the form `<assetId>:<analysisType>`.

The backend rejects (HTTP 422) cases that fail validation on upload.
