> **Canonical v2.** Anything in older docs that contradicts this document is stale. The case payload format is specified in [`CASE_JSON_V2_SPEC.md`](./CASE_JSON_V2_SPEC.md).

# Frontend Architecture — CaseZero v2

**Project:** `frontend/` · React 19.1 · TypeScript 5.8 · Vite 7 · styled-components

---

## Stack

| Technology | Version in repo | Role |
|------------|-----------------|------|
| React | `^19.1.0` | UI runtime |
| React DOM | `^19.1.0` | DOM renderer |
| TypeScript | `~5.8.3` | Type safety |
| Vite | `^7.0.4` | Dev server + build |
| React Router DOM | `^7.7.1` | Routing |
| styled-components | `^6.1.19` | Styling |
| @microsoft/signalr | `^10.0.0` | Realtime client |
| Vitest | `^2.0.5` | Unit/integration tests |
| ESLint | `^9.30.1` | Linting |

---

## App Shell (`src/App.tsx`)

Provider order in the real app shell:

```tsx
<LanguageProvider>
  <ErrorBoundary>
    <AuthProvider>
      <WindowProvider>
        <Router>
          <OfflineStatus />
          <Routes>...</Routes>
        </Router>
      </WindowProvider>
    </AuthProvider>
  </ErrorBoundary>
</LanguageProvider>
```

`OfflineStatus` is global and mounted for every route. `WindowProvider` only owns desktop-style window state; case data is mounted later inside `DesktopPage`.

---

## Routing (`src/App.tsx`)

| Path | Component | Guard |
|------|-----------|-------|
| `/` | `HomePage` | public |
| `/login` | `LoginPage` | public |
| `/register` | `RegisterPage` | public |
| `/verify-email` | `EmailVerificationPage` | public |
| `/dashboard` | `DashboardPage` | `ProtectedRoute` |
| `/desktop/:caseId?` | `DesktopPage` | `ProtectedRoute` |
| `/case-generation` | `CaseGenerationPage` | `ProtectedRoute requiredRole="ADMIN"` |
| `/profile` | `ProfilePage` | `ProtectedRoute` |

Current router facts:
- `DesktopPage` redirects to `/dashboard` when `caseId` is missing.
- The active admin route is `/case-generation`, not `/generate-case` or `/case-generator-ai`.
- `ProfilePage` is now part of the routed surface.

---

## Source Layout (`src/`)

| Folder | Current purpose |
|--------|-----------------|
| `components/` | Shared UI shell pieces such as `Navigation`, `Desktop`, `Dock`, `Clock`, `InboxPanel`, `LanguageSelector`, `ProtectedRoute`, `TimeSync` |
| `components/apps/` | Desktop windows/apps: `FileViewer`, `EmailApp`, `Notebook`, `Logs`, `Pinboard`, `SubmitCase`, `ForensicModule`, document viewers |
| `components/ui/` | Cross-cutting UI helpers such as `ErrorBoundary`, `LoadingComponents`, `OfflineStatus` |
| `contexts/` | `AuthContext`, `CaseContext`, `LanguageContext`, `TimeContext`, `WindowContext` |
| `hooks/` | Thin wrappers around contexts plus UI helpers (`useDualFileViewer`, `useFavorites`, `useKeyboardNavigation`) |
| `locales/` | Translation registry and locale dictionaries |
| `pages/` | Route entry points (`HomePage`, `DashboardPage`, `DesktopPage`, `CaseGenerationPage`, etc.) |
| `services/` | API clients plus forensic helpers/SignalR helpers |
| `engine/` | `CaseEngine.ts` external store backing the case workspace |
| `types/` | `caseV2`, `i18n`, `profile`, `ranks` |
| `test/` | Vitest setup and frontend tests |
| `utils/` | Shared utilities such as error handling |

---

## State Management

### Contexts

| Context | File | Notes |
|---------|------|-------|
| `AuthContext` | `src/contexts/AuthContext.tsx` | Authenticated user, `login`, `logout`, startup auth check |
| `LanguageContext` | `src/contexts/LanguageContext.tsx` | Active language, translation lookup `t`, persistence in `localStorage` |
| `WindowContext` | `src/contexts/WindowContext.tsx` | Desktop window registry, z-order, resize/maximize/minimize |
| `CaseContext` | `src/contexts/CaseContext.tsx` | Case workspace state backed by `CaseEngine` + `useSyncExternalStore` |
| `TimeContext` | `src/contexts/TimeContext.tsx` | Accelerated in-game clock and timeline log entries |

### `CaseContext` + `CaseEngine`

`CaseContext` is the main v2 case-state boundary. It creates a `CaseEngine`, subscribes with `useSyncExternalStore`, and exposes both the engine snapshot and a legacy-compatible API surface.

`EngineState` currently contains:
- `case: CaseV2Sanitized | null`
- `visibleAssets: Asset[]`
- `visibleEmails: Email[]`
- `visibleSuspects: Suspect[]`
- `notifications: Notification[]`
- `submission: { attemptsUsed, maxAttempts, lastResult? }`

`CaseEngine` currently exposes these real methods:
- `loadCase(caseId)`
- `refreshCase(caseId)`
- `viewAsset(assetId)`
- `openEmail(emailId)`
- `viewSuspect(suspectId)`
- `submitCase(payload)`
- `postGameTime(gameTimeMinutes)`
- `applyReveal(entityType, entityId)`
- `pushNotification(notification)`
- `clearNotifications()`
- `reset()`

Important corrections vs older docs:
- `CaseEngine` does **not** own the game clock.
- There is no `getCurrentGameTime()` method on `CaseEngine`.
- `updateGameTime(newTime: Date)` lives on `CaseContext` as a compatibility wrapper for `TimeSync`, not on the engine class itself.

### Hooks in use

| Hook | Source | Returns |
|------|--------|---------|
| `useCase()` | `contexts/CaseContext.tsx` / `hooks/useCaseContext.ts` | Full case context |
| `useAssets()` | `contexts/CaseContext.tsx` | `state.visibleAssets` |
| `useEmails()` | `contexts/CaseContext.tsx` | `state.visibleEmails` |
| `useSuspects()` | `contexts/CaseContext.tsx` | `state.visibleSuspects` |
| `useSubmission()` | `contexts/CaseContext.tsx` | Submission state |
| `useNotifications()` | `contexts/CaseContext.tsx` | Notification list |
| `useTimeContext()` | `hooks/useTimeContext.ts` | `TimeContext` value |
| `useAuth()` | `hooks/useAuthContext.ts` | `AuthContext` value |
| `useLanguage()` | `contexts/LanguageContext.tsx` / `hooks/useLanguageContext.ts` | Active locale + translations |
| `useWindowContext()` | `hooks/useWindowContext.ts` | Desktop window controls |

---

## Types (`src/types/`)

### `types/caseV2.ts`

The frontend case contract is the sanitized v2 payload:
- `CaseV2Sanitized`
- `Asset`
- `Email`
- `Suspect`
- `TimelineEntry`
- `ForensicsDefaults`
- `SanitizedSolution`
- `SubmitCaseRequest`
- `SubmitCaseResult`
- dashboard/profile helper types (`CaseDashboardItem`, `DashboardActivity`, `PromotionProgress`)

Notable payload details reflected in current code:
- `Asset.visibility` and `Email.visibility` are `'initial' | 'hidden'`.
- `CaseMetadata.requiredRank` and `difficulty` use the same rank ladder (`Rookie` → `Commander`).
- Submit responses use `feedbackCode` and optional `promotion`, not legacy free-form feedback fields.

### `types/i18n.ts`

Defines:
- `Translations` interface
- `SUPPORTED_LANGUAGES`
- `DEFAULT_LANGUAGE = 'pt-BR'`

### `types/ranks.ts`

Holds rank helpers such as `rankI18nKey(...)`, used by dashboard/profile/submit flows.

---

## Services and Backend Route Usage (`src/services/api.ts`)

All HTTP calls are centralized in `api.ts` through `apiFetch(...)`.

### Base URL and auth

- Base URL: `import.meta.env.VITE_API_URL || 'http://localhost:5001/api'`
- JWT is read from `tokenStorage.get()` and attached to `Authorization`
- `tokenStorage` exposes `get`, `set`, `remove`
- `userStorage` mirrors the authenticated user in `localStorage`

### Main client groups

| Client | Real usage |
|--------|------------|
| `authApi` | `/auth/login`, `/auth/me`, `/auth/register`, `/auth/verify-email`, `/auth/resend-verification` |
| `casesV2Api` | dashboard, case load, case triggers, submit, attachment download, time-posting |
| `caseSessionApi` | `/casesession/start`, `/casesession/end/{caseId}`, `/casesession/last/{caseId}`, `/casesession/{caseId}`, reset visibility |
| `caseGenerationApi` | `/casegeneration/generate`, `/casegeneration/jobs/{jobId}` |
| `profileApi` | `/profile/stats` |
| `inboxApi` | `/inbox`, `/inbox/unread-count`, `/inbox/{id}/read` |
| `notesApi` | `/notes/...` CRUD |
| `forensicRequestApi` | `/forensicrequest/...` CRUD/listing |
| `casesApi` / `assetsApi` / `emailsApi` / `caseObjectApi` | Legacy/auxiliary access used by current desktop viewers |

### `casesV2Api` route pattern

Although the frontend treats these as one namespace, they are served by multiple backend controllers under `/api/cases/{caseId}`:

| Frontend call | Backend route |
|--------------|---------------|
| `getCase(caseId)` | `GET /api/cases/{caseId}` |
| `viewAsset(caseId, assetId)` | `POST /api/cases/{caseId}/assets/{assetId}/view` |
| `openEmail(caseId, emailId)` | `POST /api/cases/{caseId}/emails/{emailId}/open` |
| `viewSuspect(caseId, suspectId)` | `POST /api/cases/{caseId}/suspects/{suspectId}/view` |
| `downloadAttachment(caseId, emailId, assetId)` | `POST /api/cases/{caseId}/emails/{emailId}/attachments/{assetId}/download` |
| `submitCase(caseId, payload)` | `POST /api/cases/{caseId}/submit` |
| `postGameTime(caseId, gameTimeMinutes)` | `POST /api/cases/{caseId}/time` |

### SignalR

There are two distinct SignalR client paths on disk:
- `CaseContext` builds an inline connection to `${VITE_API_URL without /api}/hubs/forensics` and listens for `case.entity.revealed`, `case.notification`, and `case.email.attached`.
- `services/forensicsSignalR.ts` is a standalone singleton hard-coded to `http://localhost:5001/hubs/forensics` and listens for `ForensicCompleted`.

The inline `CaseContext` path is the one actually wired into the workspace lifecycle.

---

## Desktop / Workspace Surface

### Actively opened from `Dock.tsx`

| Dock item | Component |
|-----------|-----------|
| File Viewer | `components/apps/FileViewer.tsx` |
| Email | `components/apps/EmailApp.tsx` |
| Forensic Module | `components/apps/ForensicModule.tsx` (hidden for Rookie cases) |
| Logs | `components/apps/Logs.tsx` |
| Notebook | `components/apps/Notebook.tsx` |
| Pinboard | `components/apps/Pinboard.tsx` |
| Submit Case | `components/apps/SubmitCase.tsx` |
| Clock / disconnect controls | `components/Clock.tsx`, dock action |

### Current app-specific notes

- `FileViewer` opens `DocumentViewerWindow` windows and resolves asset media through `casesApi.getAssetUrl(...)`.
- `EmailApp` hydrates read state from `emailsApi.getEmails(caseId)`, opens mail through `emailsApi.openEmail(...)`, and downloads attachments through the backend trigger endpoint.
- `Notebook` is backed by `notesApi`.
- `SubmitCase` reads suspects/assets from `CaseContext`, loads completed forensic requests from `forensicRequestApi`, and submits sanitized answers through `useCase().submitCase(...)`.
- `ForensicModule` is still a local in-memory simulator; it does **not** call `forensicRequestApi` or `forensicsService`.
- `ForensicsQueue.tsx` and `forensicsService.ts` exist on disk, but `ForensicsQueue` is not currently opened from `Dock.tsx`.
- `Clock` shows time/date/status/elapsed time only; the older forensic badge is gone.

---

## i18n (`src/locales/`)

The real locale registry contains exactly four languages:

| File | Code |
|------|------|
| `locales/pt-BR.ts` | `pt-BR` |
| `locales/en-US.ts` | `en-US` |
| `locales/fr-FR.ts` | `fr-FR` |
| `locales/es-ES.ts` | `es-ES` |

`src/locales/index.ts` exports those four dictionaries, and `LanguageContext` persists the selected code in `localStorage` under `casezero-language`.

Important current-state note: the translation framework is four-language capable, but several desktop components still contain hard-coded UI text instead of `t(...)` calls (for example `Clock`, `Logs`, `ForensicModule`, and `ForensicsQueue`).

---

## Build, Test, and Lint

Available scripts from `frontend/package.json`:

```bash
npm run dev
npm run build
npm run lint
npm run preview
npm run test
npm run test:ui
npm run test:run
```

### Vite / Vitest

Both `vite.config.ts` and `vitest.config.ts` configure tests for:
- `environment: 'jsdom'`
- `globals: true`
- `setupFiles: './src/test/setup.ts'` (array form in `vite.config.ts`)

### ESLint

`eslint.config.js`:
- ignores `dist`
- targets `**/*.{ts,tsx}`
- extends `@eslint/js`, `typescript-eslint`, `react-hooks`, and `react-refresh` Vite rules

---

## Local Markdown Links

- [`CASE_JSON_V2_SPEC.md`](./CASE_JSON_V2_SPEC.md) — valid
