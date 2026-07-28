# Game Time Engine

## Overview

The current game-time system is split across three frontend pieces:
- `TimeProvider` in `frontend/src/contexts/TimeContext.tsx` owns the accelerated clock.
- `TimeSync` in `frontend/src/components/TimeSync.tsx` forwards elapsed game minutes into the case backend.
- `DesktopPage` in `frontend/src/pages/DesktopPage.tsx` loads/saves session time when a player enters or leaves a case.

There is **no** dedicated `GameTimeEngine` class in `frontend/src/engine/`. The only file there is `CaseEngine.ts`, and it does not store the clock.

---

## Actual Time Scale

`TimeContext.tsx` defines:

- `TIME_MULTIPLIER = 60`
- ticker interval: `1000 ms`
- calculation: `gameTimeDelta = realTimeDelta * TIME_MULTIPLIER`

That means the implemented behavior is:
- **1 real second = 1 game minute**
- **1 real minute = 1 game hour**
- **1 real hour = 60 game hours**

The in-game default start time is **08:00:00** on the current day when a case starts without a saved timestamp.

---

## Frontend Entry Points

### `TimeProvider` (`frontend/src/contexts/TimeContext.tsx`)

Real context API:
- `gameTime: Date`
- `startTime: Date`
- `isRunning: boolean`
- `setStartTime(startTime: Date)`
- `pauseTime()`
- `resumeTime()`
- `addTimeEntry(entry: TimeEntry)`
- `timeEntries: TimeEntry[]`
- `getFormattedTime()`
- `getFormattedDate()`

Initialization behavior:
- if `initialGameTime` is passed, both `gameTime` and `startTime` begin there
- if `caseId` exists and `initialGameTime` is absent, the provider resets to today at 08:00 and appends an initial log entry
- if `initialGameTime` exists, it appends a resume log entry

There is no `useGameTime()` hook in the repo. The active hook is `useTimeContext()` from `frontend/src/hooks/useTimeContext.ts`.

### `TimeSync` (`frontend/src/components/TimeSync.tsx`)

`TimeSync` is a renderless bridge:
- reads `gameTime` from `useTimeContext()`
- reads `updateGameTime` from `useCase()`
- calls `updateGameTime(gameTime)` whenever the clock changes

### `Clock` (`frontend/src/components/Clock.tsx`)

`Clock` currently displays:
- formatted time
- formatted date
- running/paused indicator
- elapsed time since `startTime`
- fixed label `60x tempo real`

The older forensic badge behavior is not present anymore; the file explicitly notes that it was removed.

### `Logs` (`frontend/src/components/apps/Logs.tsx`)

`Logs` renders `TimeContext.timeEntries`. Today that mostly means:
- the automatic case-start / resume entries created by `TimeProvider`
- a sample "Detective conectado ao sistema" entry inserted by `Logs` itself on first mount

---

## How Time Reaches the Backend

`CaseEngine` is **not** the clock source. Its only time-related method is:

- `postGameTime(gameTimeMinutes: number)`

The flow is:

1. `TimeProvider` advances `gameTime`
2. `TimeSync` calls `useCase().updateGameTime(gameTime)`
3. `CaseContext.updateGameTime(newTime)` stores the first received `Date` as `sessionStartRef`
4. later ticks are converted to whole elapsed minutes since that first timestamp
5. `CaseEngine.postGameTime(minutes)` calls `casesV2Api.postGameTime(...)`
6. `casesV2Api.postGameTime(...)` sends `POST /api/cases/{caseId}/time`

So the backend receives **elapsed game minutes**, not the absolute ISO timestamp used for session persistence.

---

## Backend Time Handling

There is no dedicated `GameTimeService` under `backend/CaseZeroApi/Services`.

The current backend split is:

### Session persistence (`backend/CaseZeroApi/Controllers/CaseSessionController.cs`)

Persisted fields live on `backend/CaseZeroApi/Models/CaseSession.cs`:
- `GameTimeAtStart: string?`
- `GameTimeAtEnd: string?`
- `FiredTemporalEventIds: string?`
- other session-state JSON fields used by rules/visibility

Exposed routes already wrapped by the frontend:
- `POST /api/casesession/start`
- `POST /api/casesession/end/{caseId}`
- `GET /api/casesession/last/{caseId}`
- `GET /api/casesession/{caseId}`
- `DELETE /api/casesession/reset-visibility/{caseId}`

Additional backend routes exist but are **not** wrapped in `frontend/src/services/api.ts`:
- `GET /api/cases/{caseId}/session`
- `POST /api/cases/{caseId}/resume`

### Time-driven rules (`backend/CaseZeroApi/Controllers/CaseTriggersController.cs`)

`POST /api/cases/{caseId}/time` accepts:

```csharp
public record TimeRequest(int GameTimeMinutes);
```

Current behavior:
- fires `TimeElapsedTrigger(gameTimeMinutes)` through `IRulesEngineService`
- evaluates raw case `TemporalEvents`
- persists fired event ids on `CaseSession.FiredTemporalEventIds`
- reveals email/asset payloads when trigger thresholds are reached

This is the real server-side mirror of the frontend clock today.

---

## Session Load / Save Flow in the UI

`frontend/src/pages/DesktopPage.tsx` is the orchestration point.

On entry:
1. read `caseId` from the route
2. redirect to `/dashboard` if it is missing
3. call `caseSessionApi.getLastSession(caseId)`
4. if `lastSession.gameTimeAtEnd` exists, convert it to `Date` and pass it to `TimeProvider` as `initialGameTime`
5. call `caseSessionApi.startSession({ caseId, gameTimeAtStart })`
6. render:
   - `<CaseProvider caseId={caseId}>`
   - `<TimeProvider caseId={caseId} initialGameTime={initialGameTime}>`
   - `<TimeSync>`
   - `<Desktop />`

On explicit disconnect:
1. `Desktop.tsx` reads `gameTime` from `useTimeContext()`
2. calls `caseSessionApi.endSession(currentCase, { gameTimeAtEnd: gameTime.toISOString() })`
3. navigates back to `/dashboard`

Important current-state note:
- save-on-exit is implemented on the dock disconnect action
- there is no dedicated browser-close/unload persistence path in these files

---

## Forensics and Game Time: Current Reality

Older versions of this document described a fully integrated forensic timing engine. That is **not** the current frontend reality.

### What exists

- `frontend/src/services/forensicsService.ts`
  - computes forensic durations
  - exposes `requestForensicAnalysis`, `getForensicRequests`, `getPendingForensicRequests`, `checkCompletedRequests`, etc.
- `frontend/src/components/apps/ForensicsQueue.tsx`
  - renders backend forensic requests and time remaining based on `gameTime`
- `backend/CaseZeroApi/Controllers/ForensicRequestController.cs`
  - CRUD/list APIs under `/api/forensicrequest/...`
- `backend/CaseZeroApi/Services/ForensicQueueService.cs`
  - enqueues requests to Azure Queue Storage
- `backend/CaseZeroApi/Services/ForensicsBackgroundService.cs`
  - completes ready requests every 30 seconds and emits `ForensicCompleted`

### What is actually wired into the desktop

- `Dock.tsx` opens `ForensicModule.tsx`, not `ForensicsQueue.tsx`
- `ForensicModule.tsx` is a local, in-memory simulator with its own hard-coded analysis catalog and timers
- `ForensicModule.tsx` does **not** call `forensicRequestApi`, `forensicsService`, or `forensicsSignalR`
- `SubmitCase.tsx` *does* query `forensicRequestApi.getForensicRequests(caseId)` and lets players include completed backend forensic requests in a submission

### SignalR mismatch to be aware of

- backend `ForensicsBackgroundService` emits `ForensicCompleted`
- legacy `forensicsSignalR.ts` listens for `ForensicCompleted`
- active `CaseContext` SignalR wiring listens to `case.entity.revealed`, `case.notification`, and `case.email.attached`

So the backend forensic-completion event path exists, but it is not currently the main desktop integration path.

---

## Removed / Non-Current References

These names do **not** match the current codebase and should not be used in new docs:
- `useGameTime`
- `GameTimeEngine` class in `frontend/src/engine/`
- `CaseEngine.getCurrentGameTime()`
- `CaseEngine.updateGameTime()`
- `Clock` forensic badge / queue launcher as an active feature
- `GET /api/casesession/{caseId}/active`

Current replacements:
- `useTimeContext()`
- `TimeProvider` + `TimeSync`
- `CaseContext.updateGameTime(newTime: Date)` wrapper
- `GET /api/casesession/last/{caseId}`
- `POST /api/cases/{caseId}/time`

---

## References

### Frontend
- `frontend/src/contexts/TimeContext.tsx`
- `frontend/src/hooks/useTimeContext.ts`
- `frontend/src/components/TimeSync.tsx`
- `frontend/src/components/Clock.tsx`
- `frontend/src/components/apps/Logs.tsx`
- `frontend/src/pages/DesktopPage.tsx`
- `frontend/src/contexts/CaseContext.tsx`
- `frontend/src/engine/CaseEngine.ts`
- `frontend/src/services/api.ts`
- `frontend/src/services/forensicsService.ts`
- `frontend/src/components/apps/ForensicModule.tsx`
- `frontend/src/components/apps/ForensicsQueue.tsx`

### Backend
- `backend/CaseZeroApi/Models/CaseSession.cs`
- `backend/CaseZeroApi/Controllers/CaseSessionController.cs`
- `backend/CaseZeroApi/Controllers/CaseTriggersController.cs`
- `backend/CaseZeroApi/Controllers/ForensicRequestController.cs`
- `backend/CaseZeroApi/Services/ForensicQueueService.cs`
- `backend/CaseZeroApi/Services/ForensicsBackgroundService.cs`
