> **Canonical v2.** Anything in older docs that contradicts this document is stale and was removed. The case format itself is specified in [`CASE_JSON_V2_SPEC.md`](./CASE_JSON_V2_SPEC.md).

# Frontend Architecture — CaseZero v2

**Project:** `frontend/` · React 19 · TypeScript · Vite · styled-components

---

## Stack

| Technology | Version | Role |
|------------|---------|------|
| React | 19 | UI library |
| TypeScript | 5.x | Type safety |
| Vite | 6.x | Build tool + dev server |
| styled-components | 6.x | CSS-in-JS styling |
| React Router DOM | 6.x | Client-side routing |
| @microsoft/signalr | 8.x | SignalR client |

---

## Routing (`App.tsx`)

| Path | Component | Auth Required |
|------|-----------|--------------|
| `/` | `HomePage` | No |
| `/login` | `LoginPage` | No |
| `/register` | `RegisterPage` | No |
| `/verify-email` | `EmailVerificationPage` | No |
| `/dashboard` | `DashboardPage` | Yes |
| `/desktop/:caseId?` | `DesktopPage` | Yes |

> **Removed:** `/generate-case` and `/case-generator-ai` routes were removed in Phase 2. The files `CaseGeneratorAIPage.tsx` and `GenerateCasePage.tsx` remain on disk but are **not registered** in the router.

`DesktopPage` redirects to `/dashboard` when `caseId` is absent in the URL or when the session fails to start. There is no `CASE-2024-001` fallback.

---

## State Management

### Contexts (`contexts/`)

| Context | File | Provides |
|---------|------|---------|
| `LanguageContext` | `LanguageContext.tsx` | Active locale + `setLanguage` |
| `AuthContext` | `AuthContext.tsx` | JWT token, user info, login/logout |
| `CaseContext` | `CaseContext.tsx` | Full case state via `CaseEngine` (see below) |
| `TimeContext` | `TimeContext.tsx` | In-game clock |
| `WindowContext` | `WindowContext.tsx` | Desktop window manager state |

### CaseContext & CaseEngine

`CaseContext.tsx` wraps an instance of `engine/CaseEngine.ts` and exposes it via React's  
`useSyncExternalStore` (external store pattern — no reducer, no Redux).

```
CaseEngine
  ├── state: EngineState
  │     ├── case: CaseV2Sanitized | null
  │     ├── visibleAssets: Asset[]
  │     ├── visibleEmails: Email[]
  │     ├── visibleSuspects: Suspect[]
  │     ├── notifications: Notification[]
  │     └── submission: { attemptsUsed, maxAttempts, lastResult? }
  ├── subscribe(listener) → unsubscribe    ← useSyncExternalStore hook
  ├── getSnapshot() → EngineState
  └── methods: loadCase, viewAsset, openEmail, viewSuspect, submitCase, postGameTime
```

`CaseEngine` depends on two injected clients:
- `ApiClient` (backed by `casesV2Api` from `services/api.ts`)
- `SignalRClient` (backed by inline SignalR builder in `CaseContext.tsx`)

### Hooks exported from `CaseContext.tsx`

| Hook | Returns |
|------|---------|
| `useCase()` | Full `CaseContextValue` |
| `useEmails()` | `state.visibleEmails` |
| `useAssets()` | `state.visibleAssets` |
| `useSuspects()` | `state.visibleSuspects` |
| `useSubmission()` | `state.submission` |
| `useNotifications()` | `state.notifications` |

---

## Types (`types/`)

### `types/caseV2.ts`

Mirrors the **sanitised** server payload — no `solution.culpritId`, no `rules`, no `forensicOutcomes`,  
no `gameMetadata.generation`. Key types:

| Type | Description |
|------|-------------|
| `Asset` | id, type, title, uri, visibility, tags, metadata |
| `Email` | id, from, to, subject, body, sentAt, attachments[], visibility |
| `Suspect` | id, name, alias, occupation, motive, alibi, status, visibility |
| `TimelineEntry` | time, event, source, verified, importance |
| `Notification` | level (`info\|warn\|critical`), message |
| `SanitizedQuestion` | id, prompt, options[] — no `correctOptionId` |
| `SanitizedSolution` | questions[], minimumScore, maxAttempts |
| `SubmitCaseRequest` | suspectId, evidenceIds[], analysisIds[], answers[{questionId, optionId}] |
| `SubmitCaseResult` | correct, score, breakdown, attemptsRemaining, feedbackText, explanation? |
| `CaseV2Sanitized` | Root: metadata, assets, emails, suspects, timeline, forensicsDefaults, solution (sanitised), gameMetadata (client subset) |

### `types/i18n.ts`

`Translations` interface + `SUPPORTED_LANGUAGES` constant.

### `types/ranks.ts`

`CaseDifficulty` / rank enum aligned with backend.

---

## Services (`services/`)

### `services/api.ts`

Base URL: `VITE_API_URL` env var (default `http://localhost:5001/api`).

Key namespaced exports used by v2:

| Export | Methods |
|--------|---------|
| `authApi` | `login`, `register`, `verifyEmail`, `resendVerification` |
| `casesV2Api` | `getCase(caseId)`, `viewAsset(caseId, assetId)`, `openEmail(caseId, emailId)`, `viewSuspect(caseId, suspectId)`, `submitCase(caseId, payload)`, `postGameTime(caseId, gameTimeMinutes)` |
| `forensicRequestApi` | `create`, `getForCase`, `update` |
| `notesApi` | `getForCase`, `getById`, `create`, `update`, `delete` |
| `caseSessionApi` | `start`, `end`, `getLastSession`, `getState`, `resume` |
| `tokenStorage` | `get`, `set`, `clear` |

Legacy exports (`caseGenerationApi`, `caseObjectApi`, `casesV1Api`, `caseFilesApi`) remain in the file  
but are not used by any active component.

### `services/forensicsSignalR.ts`

Standalone singleton class that connects to `/hubs/forensics` and re-emits the `ForensicCompleted`  
event to registered listeners. Used by legacy forensic polling; the `CaseContext`/`CaseEngine` path  
wires its own inline SignalR client for tighter lifecycle management.

---

## Desktop Apps (`components/apps/`)

| App | Description |
|-----|-------------|
| `EmailApp` | Inbox, compose, attachment download; fires `openEmail` + `downloadAttachment` triggers |
| `FileViewer` | Asset viewer for photos/PDFs/audio/video; fires `viewAsset` trigger |
| `ForensicModule` | Submit forensic requests; shows pending/completed results |
| `SubmitCase` | Rewritten v2: culprit dropdown (populated from visible suspects), visible-evidence checkboxes, analysis combobox, dynamic `solution.questions` with per-question option list; submits to `/api/cases/{id}/submit`; renders `score`, `breakdown`, `attemptsRemaining`, `feedback`, `explanation` on result; button disabled when attempts exhausted |

---

## i18n (`locales/`)

Four locale files, each implementing the full `Translations` interface:

| File | Locale |
|------|--------|
| `locales/en-US.ts` | English (US) |
| `locales/pt-BR.ts` | Portuguese (BR) |
| `locales/es-ES.ts` | Spanish (ES) |
| `locales/fr-FR.ts` | French (FR) |

**Rule:** every new UI string added to any component **must** be added to all four locale files.  
`LanguageContext` switches locale at runtime without page reload.

---

## Build & Dev

```bash
npm run dev        # Vite dev server (http://localhost:5173)
npm run build      # Production build → dist/
npm run lint       # ESLint
npm run test:run   # Vitest (single pass)
npm run test       # Vitest watch
```

Environment variables (`.env` / Vite):

| Variable | Default | Purpose |
|----------|---------|---------|
| `VITE_API_URL` | `http://localhost:5001/api` | Backend base URL |
