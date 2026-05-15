# 04 — Case Structure

> **Canonical v2.** The technical shape of `case.json` v2 is specified in [`docs/CASE_JSON_V2_SPEC.md`](../docs/CASE_JSON_V2_SPEC.md). This chapter only covers the **game-design** rationale behind that shape. Any older document referencing `evidences[]`, `unlockLogic`, `documents[]` or `forensicReports[]` is stale and should be ignored.

## 4.1 Overview

Each case is a single `case.json` (v2) plus binary assets (PDFs, photos, audio) under `cases/<caseId>/assets/`. All **case logic** lives in the JSON: what is visible initially, what unlocks when, who the culprit is, what questions to ask the player, how much each correct answer is worth.

Principles:

- **Self-contained.** A case runs on the site without any auxiliary code. The backend has no hard-coded case logic.
- **Server-only secrets.** `rules`, `forensicOutcomes`, `solution`, `temporalEvents` and `gameMetadata.generation` never reach the client.
- **Idempotency.** Every rule and temporal event fires at most once per session.
- **Reproducibility.** Two detectives on the same case see the same beats in the same order (modulo their own choices).

## 4.2 Case building blocks

| Block | Role | Client sees? |
|-------|------|--------------|
| `metadata` | Pitch, difficulty, required rank, unlock mode | yes |
| `assets[]` | Anything openable in the FileViewer (PDF, photo, audio) | only the visible ones |
| `emails[]` | Detective's inbox — briefing, lab, witnesses | only the visible ones |
| `suspects[]` | Profiles with motive, alibi, status | only the visible ones |
| `timeline[]` | Narrative timeline shown to the player | yes |
| `temporalEvents[]` | Memos/alerts that fire by game time | effects yes, definition no |
| `rules[]` | "When X happens, do Y" | **no** |
| `forensicsDefaults` | Available analysis types and durations | partial |
| `forensicOutcomes[]` | Canonical result of each analysis over each asset | **no** |
| `solution{}` | Culprit, required evidence, questions, scoring | only questions (no answer key) |
| `gameMetadata` | Tags, content warnings, generation metadata | yes (no `generation`) |

## 4.3 Unlock mode (`unlockMode`)

- **`gated`** (default) — only items with `visibility: "initial"` show up at the start; the rest is unlocked by `rules[]`.
- **`all_initial`** — everything is visible from the start. Used in Rookie cases and in playtests.
- **Auto-rule:** when `metadata.requiredRank == "Rookie"` the backend forces `unlockMode = "all_initial"` regardless of the JSON.

## 4.4 Triggers and Actions

Triggers the server recognises:

| Trigger | Fires when |
|---------|------------|
| `forensics_complete` | A forensic analysis `(inputAssetId, analysisType)` finishes |
| `email_opened` | Detective opens an email for the first time |
| `attachment_download` | Detective downloads an attachment |
| `asset_viewed` | Detective opens an asset in the FileViewer |
| `suspect_viewed` | Detective opens a suspect profile |
| `time_elapsed` | Game time reaches `atMinutes` |
| `multiple_conditions` | AND/OR composition of other triggers |

Actions a rule may apply:

| Action | Effect |
|--------|--------|
| `reveal_email` / `reveal_asset` / `reveal_suspect` | Make entity visible |
| `add_email_attachment` | Attach to an already-visible email |
| `send_notification` | On-screen notification (info/warn/critical) |
| `update_suspect_status` | `suspect → cleared` or `confirmed_culprit` |
| `mark_alibi_verified` | Mark alibi as verified |

Every rule is **idempotent** per `(ruleId, sessionId)`.

## 4.5 Forensics (`forensicsDefaults` + `forensicOutcomes`)

When the detective requests an analysis:

1. Backend verifies that the `analysisType` is compatible with the `Asset.type` via `forensicsDefaults.analysisTypes[*].availableFor`.
2. Creates a `ForensicRequest` and waits for the configured duration.
3. On completion:
   - Looks up `forensicOutcomes[]` for `(inputAssetId, analysisType)`.
   - If found with `findings == true`, reveals `resultEmailId`/`resultAssetId`.
   - Otherwise, sends `forensicsDefaults.noFindingsEmail`.
4. In every case the `forensics_complete` trigger fires — extra rules may react.

## 4.6 Solution (`solution`)

The solution is graded server-side and has four scorable components:

| Component | Default weight | Scoring |
|-----------|----------------|---------|
| `culpritId` | 0.4 | All or nothing |
| `requiredEvidenceIds` | 0.2 | Proportional to intersection |
| `requiredAnalysisIds` | 0.2 | Same |
| `questions[]` | 0.2 | Weighted sum of `weight × correct` |

`partialCreditRules` defines the weights (must sum to 1.0). `minimumScore` is the bar for `correct = true`. `maxAttempts` caps tries per `(user, case)`. After winning or exhausting attempts the player receives `explanation` in markdown.

The client gets `solution.questions[]` **without `correctOptionId`** and without any of the required lists — only the form.

## 4.7 Temporal events (`temporalEvents`)

Each `tevt.*` fires at `triggerAtMinutes` of game time since `openedAt`. The server tracks which have fired per session so each happens exactly once. Payloads become dynamic emails, desktop memos or notifications.

## 4.8 Design heuristics

1. **The first email should pave the way to the first analysis.** Nothing should be locked at the start without an in-fiction explanation.
2. **Every rule must have a narrative "why".** Don't use `reveal_*` as a shortcut — it's the consequence of a discovery.
3. **3-5 suspects per case.** Fewer is obvious; more is muddled.
4. **The solution should require at least one forensic and one piece of evidence.** Otherwise the game becomes "guess the culprit".
5. **Use `partialCreditRules` to reward partial reasoning.** Getting the culprit right but the evidence wrong still deserves points.

## 4.9 Generating a case

The generation pipeline lives in `functions/CaseGen.Functions/` (Azure Functions, .NET 9). It must emit a `case.json` that validates against `schemas/case.schema.json`. Migrating the pipeline to emit v2 is on the roadmap (chapter 12).
