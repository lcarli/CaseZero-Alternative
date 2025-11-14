# Chapter 08 - Technical Architecture

**Game Design Document - CaseZero v3.0**  
**Last Updated:** November 13, 2025  
**Status:** ✅ Complete

---

## 8.1 Overview

This chapter defines the **technical architecture, technology stack, and implementation approach** for CaseZero v3.0. The system is designed for reliability, scalability, and maintainability while supporting the core gameplay mechanics.

**Key Concepts:**
- React frontend (TypeScript)
- C# ASP.NET Core backend
- Azure cloud infrastructure
- PostgreSQL database
- Blob storage for assets
- JWT authentication
- Real-time forensics via background jobs

---

## 8.2 Architecture Philosophy

### Core Principles

**1. Separation of Concerns**
- Frontend handles presentation and user interaction
- Backend handles business logic and data
- Database stores persistent state
- Storage handles binary assets

**2. Stateless Backend**
- Server doesn't maintain session state
- All state in database or JWT token
- Horizontal scaling possible
- Restart-safe

**3. Offline-First Frontend**
- Case data cached locally
- Read operations work offline
- Write operations queue for sync
- Service worker for PWA

**4. Security by Design**
- Authentication required for all operations
- Authorization per case access
- Input validation at all layers
- No client-side secrets

---

## 8.3 System Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                        USER'S BROWSER                        │
│  ┌────────────────────────────────────────────────────────┐ │
│  │            React SPA (TypeScript)                      │ │
│  │  - Desktop UI Components                               │ │
│  │  - State Management (Redux)                            │ │
│  │  - PDF.js for document viewing                         │ │
│  │  - Service Worker (offline support)                    │ │
│  └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                              │
                         HTTPS/REST API
                              │
┌─────────────────────────────────────────────────────────────┐
│                    AZURE CLOUD PLATFORM                      │
│                                                              │
│  ┌────────────────────────────────────────────────────────┐ │
│  │        Azure App Service (Linux)                       │ │
│  │  ┌──────────────────────────────────────────────────┐  │ │
│  │  │   ASP.NET Core 9.0 Web API (C#)                  │  │ │
│  │  │   - REST Controllers                              │  │ │
│  │  │   - Authentication (JWT)                          │  │ │
│  │  │   - Business Logic Services                       │  │ │
│  │  │   - Entity Framework Core                         │  │ │
│  │  └──────────────────────────────────────────────────┘  │ │
│  └────────────────────────────────────────────────────────┘ │
│                              │                               │
│              ┌───────────────┴───────────────┐              │
│              │                               │              │
│  ┌───────────▼────────────┐    ┌────────────▼──────────┐  │
│  │ Azure SQL Database      │    │ Azure Blob Storage    │  │
│  │ (PostgreSQL)            │    │ - Case documents      │  │
│  │ - User accounts         │    │ - Evidence photos     │  │
│  │ - Case sessions         │    │ - Forensic reports    │  │
│  │ - Forensic requests     │    │ - Suspect photos      │  │
│  │ - Submissions           │    │                       │  │
│  │ - Progression data      │    │                       │  │
│  └────────────────────────┘    └───────────────────────┘  │
│                                                              │
│  ┌────────────────────────────────────────────────────────┐ │
│  │        Azure Functions (Serverless)                    │ │
│  │  - Forensic Timer Worker                               │ │
│  │  - Email Notification Service                          │ │
│  │  - Analytics Aggregation                               │ │
│  └────────────────────────────────────────────────────────┘ │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

---

## 8.4 Technology Stack

### Frontend

**Core Framework:**
- **React 18+** (TypeScript)
  - Component-based UI
  - Hooks for state management
  - Virtual DOM for performance

**State Management:**
- **Redux Toolkit**
  - Centralized application state
  - Predictable state updates
  - Time-travel debugging

**Routing:**
- **React Router v6**
  - Client-side navigation
  - Lazy loading for code splitting
  - Protected routes

**Document Viewing:**
- **PDF.js** (Mozilla)
  - Native PDF rendering
  - No server-side conversion
  - Text selection, search

**HTTP Client:**
- **Axios**
  - Promise-based requests
  - Interceptors for auth
  - Request cancellation

**Styling:**
- **CSS Modules** + **Tailwind CSS**
  - Scoped styles
  - Utility-first CSS
  - Responsive design

**Build Tool:**
- **Vite**
  - Fast development server
  - Hot module replacement
  - Optimized production builds

**Testing:**
- **Vitest** (unit tests)
- **React Testing Library** (component tests)
- **Playwright** (E2E tests)

---

### Backend

**Core Framework:**
- **ASP.NET Core 9.0** (C#)
  - Cross-platform (Linux, Windows)
  - High performance
  - Dependency injection built-in

**API:**
- **REST API** (JSON)
  - Conventional HTTP methods
  - Clear resource endpoints
  - Versioning via URL

**Authentication:**
- **JWT (JSON Web Tokens)**
  - Stateless authentication
  - Claims-based authorization
  - Refresh token support

**Database ORM:**
- **Entity Framework Core 9.0**
  - Code-first migrations
  - LINQ queries
  - Change tracking

**Background Jobs:**
- **Azure Functions** (Timer Triggers)
  - Forensic timer completion
  - Notification dispatch
  - Analytics aggregation

**Logging:**
- **Serilog**
  - Structured logging
  - Azure Application Insights integration
  - Log levels, filters

**Testing:**
- **xUnit** (unit tests)
- **Moq** (mocking)
- **WebApplicationFactory** (integration tests)

---

### Database

**Primary Database:**
- **PostgreSQL 15+** (via Azure Database for PostgreSQL)
  - Relational data
  - ACID transactions
  - JSON support for case.json storage

**Why PostgreSQL:**
- Open source, widely supported
- Excellent performance
- JSON/JSONB for flexible case data
- Strong Azure integration

---

### Cloud Infrastructure

**Hosting:**
- **Azure App Service** (Web API)
  - Managed platform
  - Auto-scaling
  - Deployment slots (staging/prod)

**Serverless:**
- **Azure Functions**
  - Event-driven execution
  - Pay-per-use
  - Timer triggers for forensics

**Storage:**
- **Azure Blob Storage**
  - CDN for static assets
  - Hot tier for active cases
  - Cool tier for archived cases

**CDN:**
- **Azure CDN**
  - Global asset delivery
  - Reduced latency
  - HTTPS by default

**Monitoring:**
- **Azure Application Insights**
  - Performance monitoring
  - Error tracking
  - Usage analytics

**DevOps:**
- **GitHub Actions**
  - CI/CD pipelines
  - Automated testing
  - Deployment automation

---

## 8.5 Database Schema

### Core Tables

**Users**
```sql
CREATE TABLE Users (
    UserId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    Username VARCHAR(50) UNIQUE NOT NULL,
    Email VARCHAR(255) UNIQUE NOT NULL,
    PasswordHash VARCHAR(255) NOT NULL,
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW(),
    LastLoginAt TIMESTAMP,
    IsActive BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE INDEX idx_users_email ON Users(Email);
CREATE INDEX idx_users_username ON Users(Username);
```

**UserProgress**
```sql
CREATE TABLE UserProgress (
    UserId UUID PRIMARY KEY REFERENCES Users(UserId),
    CurrentRank VARCHAR(50) NOT NULL DEFAULT 'Rookie',
    TotalXP INTEGER NOT NULL DEFAULT 0,
    CasesSolved INTEGER NOT NULL DEFAULT 0,
    CasesFailed INTEGER NOT NULL DEFAULT 0,
    FirstAttemptSuccesses INTEGER NOT NULL DEFAULT 0,
    TotalInvestigationMinutes INTEGER NOT NULL DEFAULT 0,
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW(),
    UpdatedAt TIMESTAMP NOT NULL DEFAULT NOW()
);
```

**Cases** (Metadata)
```sql
CREATE TABLE Cases (
    CaseId VARCHAR(50) PRIMARY KEY,
    Title VARCHAR(255) NOT NULL,
    Difficulty VARCHAR(20) NOT NULL CHECK (Difficulty IN ('Easy', 'Medium', 'Hard', 'Expert')),
    EstimatedTimeHours DECIMAL(4,1) NOT NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'Published',
    CaseDataJson JSONB NOT NULL, -- Full case.json stored here
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW(),
    UpdatedAt TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_cases_difficulty ON Cases(Difficulty);
CREATE INDEX idx_cases_status ON Cases(Status);
```

**CaseSessions** (Player progress per case)
```sql
CREATE TABLE CaseSessions (
    SessionId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    UserId UUID NOT NULL REFERENCES Users(UserId),
    CaseId VARCHAR(50) NOT NULL REFERENCES Cases(CaseId),
    Status VARCHAR(20) NOT NULL DEFAULT 'Active' CHECK (Status IN ('Active', 'Solved', 'Failed')),
    StartedAt TIMESTAMP NOT NULL DEFAULT NOW(),
    CompletedAt TIMESTAMP,
    TotalTimeMinutes INTEGER NOT NULL DEFAULT 0,
    SubmissionAttempts INTEGER NOT NULL DEFAULT 0,
    NotesText TEXT,
    UNIQUE(UserId, CaseId)
);

CREATE INDEX idx_sessions_user ON CaseSessions(UserId);
CREATE INDEX idx_sessions_status ON CaseSessions(Status);
```

**ForensicRequests** (Real-time timer tracking)
```sql
CREATE TABLE ForensicRequests (
    RequestId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    SessionId UUID NOT NULL REFERENCES CaseSessions(SessionId),
    EvidenceId VARCHAR(50) NOT NULL,
    AnalysisType VARCHAR(50) NOT NULL,
    RequestedAt TIMESTAMP NOT NULL DEFAULT NOW(),
    CompletesAt TIMESTAMP NOT NULL,
    CompletedAt TIMESTAMP,
    Status VARCHAR(20) NOT NULL DEFAULT 'Pending' CHECK (Status IN ('Pending', 'Completed', 'Cancelled')),
    ResultBlobPath VARCHAR(500) -- Path to forensic report PDF
);

CREATE INDEX idx_forensics_session ON ForensicRequests(SessionId);
CREATE INDEX idx_forensics_status ON ForensicRequests(Status);
CREATE INDEX idx_forensics_completes_at ON ForensicRequests(CompletesAt) WHERE Status = 'Pending';
```

**CaseSubmissions** (Solution attempts)
```sql
CREATE TABLE CaseSubmissions (
    SubmissionId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    SessionId UUID NOT NULL REFERENCES CaseSessions(SessionId),
    SubmittedAt TIMESTAMP NOT NULL DEFAULT NOW(),
    CulpritSelected VARCHAR(50) NOT NULL,
    MotiveExplanation TEXT NOT NULL,
    MethodExplanation TEXT NOT NULL,
    EvidenceSelected JSONB NOT NULL, -- Array of evidence IDs
    IsCorrect BOOLEAN NOT NULL,
    XPAwarded INTEGER NOT NULL,
    FeedbackJson JSONB -- Detailed feedback
);

CREATE INDEX idx_submissions_session ON CaseSubmissions(SessionId);
CREATE INDEX idx_submissions_submitted_at ON CaseSubmissions(SubmittedAt);
```

**Achievements** (Optional)
```sql
CREATE TABLE Achievements (
    AchievementId VARCHAR(50) PRIMARY KEY,
    Name VARCHAR(100) NOT NULL,
    Description TEXT NOT NULL,
    BadgeIcon VARCHAR(100)
);

CREATE TABLE UserAchievements (
    UserId UUID NOT NULL REFERENCES Users(UserId),
    AchievementId VARCHAR(50) NOT NULL REFERENCES Achievements(AchievementId),
    UnlockedAt TIMESTAMP NOT NULL DEFAULT NOW(),
    PRIMARY KEY (UserId, AchievementId)
);
```

---

## 8.6 API Endpoints

### Authentication

**POST /api/auth/register**
```json
Request:
{
  "username": "alex_detective",
  "email": "alex@example.com",
  "password": "SecurePass123!"
}

Response: 201 Created
{
  "userId": "uuid",
  "username": "alex_detective",
  "token": "jwt-token",
  "refreshToken": "refresh-token"
}
```

**POST /api/auth/login**
```json
Request:
{
  "email": "alex@example.com",
  "password": "SecurePass123!"
}

Response: 200 OK
{
  "userId": "uuid",
  "username": "alex_detective",
  "rank": "Detective I",
  "xp": 3250,
  "token": "jwt-token",
  "refreshToken": "refresh-token"
}
```

**POST /api/auth/refresh**
```json
Request:
{
  "refreshToken": "refresh-token"
}

Response: 200 OK
{
  "token": "new-jwt-token",
  "refreshToken": "new-refresh-token"
}
```

---

### Cases

**GET /api/cases**
```
Query params:
- difficulty: Easy|Medium|Hard|Expert (optional)
- status: Published|Active|Solved (optional)
- page: int (default 1)
- pageSize: int (default 20)

Response: 200 OK
{
  "cases": [
    {
      "caseId": "CASE-2024-001",
      "title": "The Downtown Office Murder",
      "difficulty": "Medium",
      "estimatedTimeHours": 4.5,
      "suspectCount": 3,
      "documentCount": 12,
      "evidenceCount": 8,
      "userStatus": "NotStarted|Active|Solved|Failed"
    },
    ...
  ],
  "totalCount": 45,
  "page": 1,
  "pageSize": 20
}
```

**GET /api/cases/{caseId}**
```
Response: 200 OK
{
  "caseId": "CASE-2024-001",
  "title": "The Downtown Office Murder",
  "difficulty": "Medium",
  "caseData": { /* Full case.json */ },
  "userSession": {
    "sessionId": "uuid",
    "startedAt": "2025-11-13T10:00:00Z",
    "totalTimeMinutes": 145,
    "submissionAttempts": 0,
    "status": "Active"
  }
}
```

**POST /api/cases/{caseId}/start**
```
Response: 201 Created
{
  "sessionId": "uuid",
  "caseId": "CASE-2024-001",
  "startedAt": "2025-11-13T10:00:00Z"
}
```

---

### Documents & Evidence

**GET /api/cases/{caseId}/documents/{documentId}**
```
Response: 200 OK
{
  "documentId": "DOC-001",
  "type": "PoliceReport",
  "title": "Initial Incident Report",
  "blobUrl": "https://cdn.casezero.com/cases/2024-001/docs/police-report.pdf",
  "pageCount": 3
}
```

**GET /api/cases/{caseId}/evidence/{evidenceId}**
```
Response: 200 OK
{
  "evidenceId": "EV-001",
  "name": "Firearm - .38 Caliber",
  "type": "Physical",
  "photos": [
    "https://cdn.casezero.com/cases/2024-001/evidence/ev001-1.jpg",
    "https://cdn.casezero.com/cases/2024-001/evidence/ev001-2.jpg"
  ],
  "forensicAnalysisAvailable": [
    {
      "type": "Ballistics",
      "duration": 12,
      "durationUnit": "hours"
    }
  ]
}
```

---

### Forensics

**POST /api/forensics/request**
```json
Request:
{
  "sessionId": "uuid",
  "evidenceId": "EV-001",
  "analysisType": "Ballistics"
}

Response: 201 Created
{
  "requestId": "uuid",
  "evidenceId": "EV-001",
  "analysisType": "Ballistics",
  "requestedAt": "2025-11-13T10:00:00Z",
  "completesAt": "2025-11-13T22:00:00Z",
  "status": "Pending"
}
```

**GET /api/forensics/session/{sessionId}**
```
Response: 200 OK
{
  "pending": [
    {
      "requestId": "uuid",
      "evidenceId": "EV-001",
      "analysisType": "Ballistics",
      "completesAt": "2025-11-13T22:00:00Z",
      "hoursRemaining": 10.5
    }
  ],
  "completed": [
    {
      "requestId": "uuid",
      "evidenceId": "EV-004",
      "analysisType": "DNA",
      "completedAt": "2025-11-13T08:00:00Z",
      "reportUrl": "https://cdn.casezero.com/cases/2024-001/forensics/dna-ev004.pdf"
    }
  ]
}
```

**GET /api/forensics/{requestId}**
```
Response: 200 OK
{
  "requestId": "uuid",
  "status": "Completed",
  "reportUrl": "https://cdn.casezero.com/cases/2024-001/forensics/ballistics-ev001.pdf"
}
```

---

### Solution Submission

**POST /api/cases/{caseId}/submit**
```json
Request:
{
  "sessionId": "uuid",
  "culprit": "SUSP-001",
  "motive": "Financial desperation. Torres owed $500k...",
  "method": "Used building access to enter office...",
  "evidenceSelected": ["EV-001", "EV-004", "EV-007", "DOC-009"]
}

Response: 200 OK
{
  "submissionId": "uuid",
  "isCorrect": true,
  "xpAwarded": 450,
  "feedback": {
    "summary": "Excellent work, Detective!",
    "culpritCorrect": true,
    "keyEvidenceCited": true,
    "explanationQuality": "thorough"
  },
  "newRank": "Detective I",
  "rankUp": false,
  "progressToNextRank": 79
}
```

**Response (Incorrect):**
```json
{
  "submissionId": "uuid",
  "isCorrect": false,
  "xpAwarded": 0,
  "attemptsRemaining": 2,
  "feedback": {
    "summary": "Your conclusion does not match the evidence.",
    "hints": [
      "The suspect you identified has a solid alibi.",
      "Re-examine the forensic DNA report.",
      "Timeline shows discrepancies."
    ]
  }
}
```

---

### User Profile & Progress

**GET /api/users/profile**
```
Response: 200 OK
{
  "userId": "uuid",
  "username": "alex_detective",
  "rank": "Detective I",
  "xp": 3250,
  "xpToNextRank": 1750,
  "stats": {
    "casesSolved": 12,
    "casesFailed": 2,
    "successRate": 85.7,
    "firstAttemptSuccessRate": 41.7,
    "totalInvestigationHours": 45.2,
    "avgTimePerCase": 3.8
  },
  "recentCases": [
    {
      "caseId": "CASE-2024-012",
      "title": "The Warehouse Fire",
      "status": "Solved",
      "attempts": 1,
      "xpEarned": 900
    }
  ]
}
```

**GET /api/users/stats**
```
Response: 200 OK
{
  "overall": { /* stats */ },
  "byDifficulty": {
    "Easy": { "solved": 4, "failed": 0, "successRate": 100 },
    "Medium": { "solved": 5, "failed": 1, "successRate": 83.3 },
    "Hard": { "solved": 3, "failed": 1, "successRate": 75 }
  }
}
```

---

### Notes

**PUT /api/cases/{caseId}/notes**
```json
Request:
{
  "sessionId": "uuid",
  "notesText": "SUSPECTS:\n- Torres: Financial motive..."
}

Response: 200 OK
{
  "saved": true,
  "lastSavedAt": "2025-11-13T10:15:00Z"
}
```

**GET /api/cases/{caseId}/notes**
```
Response: 200 OK
{
  "notesText": "SUSPECTS:\n- Torres: Financial motive...",
  "lastSavedAt": "2025-11-13T10:15:00Z"
}
```

---

## 8.7 Authentication & Authorization

### JWT Token Structure

**Token Payload:**
```json
{
  "sub": "user-uuid",
  "username": "alex_detective",
  "rank": "Detective I",
  "email": "alex@example.com",
  "iat": 1699876800,
  "exp": 1699880400
}
```

**Token Lifetime:**
- Access Token: 1 hour
- Refresh Token: 30 days
- Sliding expiration on use

### Authorization Rules

**Case Access:**
- User must have started session for case
- OR case is available for their rank

**Forensic Requests:**
- Must own the case session
- Evidence must be part of case

**Submission:**
- Must own the case session
- Case must be active (not failed)
- Must have attempts remaining

---

## 8.8 Real-Time Forensics Implementation

### Azure Function Timer Worker

**Function:** `ForensicTimerWorker`  
**Trigger:** Timer (runs every 5 minutes)

**Logic:**
```csharp
[Function("ForensicTimerWorker")]
public async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo timer)
{
    // 1. Query for pending forensics that should complete
    var completedRequests = await _db.ForensicRequests
        .Where(r => r.Status == "Pending" && r.CompletesAt <= DateTime.UtcNow)
        .ToListAsync();
    
    foreach (var request in completedRequests)
    {
        // 2. Generate forensic report (template + data)
        var report = await _forensicService.GenerateReport(request);
        
        // 3. Upload PDF to blob storage
        var blobPath = await _blobService.UploadReportAsync(report);
        
        // 4. Update database
        request.Status = "Completed";
        request.CompletedAt = DateTime.UtcNow;
        request.ResultBlobPath = blobPath;
        
        // 5. Send notification (email to user)
        await _notificationService.NotifyForensicComplete(request);
    }
    
    await _db.SaveChangesAsync();
}
```

**Why This Works:**
- Runs independently of web API
- Scales automatically
- Fault-tolerant (retries)
- No polling from client needed

---

## 8.9 Frontend State Management

### Redux Store Structure

```typescript
{
  auth: {
    isAuthenticated: boolean,
    user: User | null,
    token: string | null
  },
  cases: {
    available: Case[],
    active: Case | null,
    loading: boolean,
    error: string | null
  },
  documents: {
    [documentId: string]: Document
  },
  evidence: {
    [evidenceId: string]: Evidence
  },
  forensics: {
    pending: ForensicRequest[],
    completed: ForensicRequest[]
  },
  notes: {
    text: string,
    lastSaved: Date | null,
    isDirty: boolean
  },
  ui: {
    openWindows: string[],
    activeWindow: string | null,
    settings: UserSettings
  }
}
```

### Key Actions

```typescript
// Auth
authActions.login(credentials)
authActions.logout()
authActions.refreshToken()

// Cases
caseActions.fetchAvailableCases()
caseActions.loadCase(caseId)
caseActions.startCase(caseId)

// Documents
documentActions.fetchDocument(caseId, documentId)

// Evidence
evidenceActions.fetchEvidence(caseId, evidenceId)

// Forensics
forensicActions.requestAnalysis(evidenceId, type)
forensicActions.fetchPendingForensics(sessionId)

// Submission
submissionActions.submitSolution(solution)

// Notes
notesActions.updateNotes(text)
notesActions.saveNotes()
```

---

## 8.10 Performance Optimization

### Frontend Optimization

**Code Splitting:**
```typescript
// Lazy load case components
const CaseFiles = lazy(() => import('./apps/CaseFiles'));
const ForensicsLab = lazy(() => import('./apps/ForensicsLab'));
```

**Asset Optimization:**
- PDF.js lazy loads pages
- Images use responsive srcset
- Evidence photos lazy load on scroll
- CDN for all static assets

**Caching Strategy:**
```typescript
// Service Worker caches:
// - Case metadata (24 hours)
// - Documents (indefinite, versioned)
// - Evidence photos (indefinite)
// - API responses (5 minutes)
```

### Backend Optimization

**Database Queries:**
```csharp
// Use projections to avoid loading full case.json
var cases = await _db.Cases
    .Select(c => new CaseListDto {
        CaseId = c.CaseId,
        Title = c.Title,
        Difficulty = c.Difficulty
        // Don't load CaseDataJson
    })
    .ToListAsync();
```

**Caching:**
- Redis for frequently accessed cases
- CDN for static assets
- In-memory cache for case metadata

**Connection Pooling:**
- Database connection pool (max 100)
- HTTP client reuse (Singleton)

---

## 8.11 Security Measures

### Input Validation

**API Layer:**
```csharp
[HttpPost("submit")]
public async Task<IActionResult> SubmitSolution(
    [FromBody, Required] SubmissionDto submission)
{
    // Model validation
    if (!ModelState.IsValid)
        return BadRequest(ModelState);
    
    // Business validation
    if (submission.Motive.Length < 50)
        return BadRequest("Motive explanation too short");
    
    // Authorization
    var session = await GetUserSession(submission.SessionId);
    if (session.UserId != User.GetUserId())
        return Unauthorized();
    
    // Process...
}
```

### SQL Injection Prevention

**Always Use Parameterized Queries:**
```csharp
// Entity Framework automatically parameterizes
var user = await _db.Users
    .FirstOrDefaultAsync(u => u.Email == email);
```

### XSS Prevention

**Content Security Policy:**
```
Content-Security-Policy: 
  default-src 'self';
  script-src 'self' 'unsafe-inline' 'unsafe-eval';
  style-src 'self' 'unsafe-inline';
  img-src 'self' data: https://cdn.casezero.com;
  font-src 'self' data:;
```

**Sanitize User Input:**
```typescript
// Sanitize notes before display
import DOMPurify from 'dompurify';
const cleanNotes = DOMPurify.sanitize(notes);
```

### CORS Configuration

```csharp
services.AddCors(options => {
    options.AddPolicy("Production", builder => {
        builder
            .WithOrigins("https://casezero.com")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});
```

---

## 8.12 Deployment Strategy

### Environments

**Development:**
- Local machines
- Docker Compose for dependencies
- Hot reload enabled

**Staging:**
- Azure App Service (staging slot)
- Separate database
- Production-like configuration
- Automated deployment from `develop` branch

**Production:**
- Azure App Service (production slot)
- Production database (replicated)
- CDN for assets
- Deployment via GitHub Actions (manual trigger)

### CI/CD Pipeline (GitHub Actions)

**On Pull Request:**
```yaml
- Run linters (ESLint, Prettier)
- Run unit tests (frontend + backend)
- Run integration tests
- Build Docker image
- Deploy to staging
- Run E2E tests against staging
```

**On Merge to Main:**
```yaml
- All PR checks must pass
- Build production Docker image
- Tag with version
- Push to Azure Container Registry
- Manual approval gate
- Deploy to production (blue-green)
- Smoke tests
- Monitor for 30 minutes
```

### Rollback Strategy

**Blue-Green Deployment:**
1. Deploy to inactive slot (green)
2. Run smoke tests on green
3. Swap slots (green → production)
4. Monitor metrics for 30 minutes
5. If issues, swap back immediately

---

## 8.13 Monitoring & Observability

### Application Insights

**Tracked Metrics:**
- Request latency (p50, p95, p99)
- Error rates
- Active users
- Case completion rates
- Forensic request times

**Custom Events:**
```csharp
_telemetry.TrackEvent("CaseSolved", new Dictionary<string, string> {
    { "CaseId", caseId },
    { "Difficulty", difficulty },
    { "Attempts", attempts.ToString() },
    { "TimeMinutes", timeMinutes.ToString() }
});
```

### Logging

**Log Levels:**
- **Trace:** Detailed diagnostics
- **Debug:** Developer information
- **Information:** General flow
- **Warning:** Unexpected but handled
- **Error:** Failures requiring attention
- **Critical:** Application crash

**Structured Logging:**
```csharp
_logger.LogInformation(
    "User {UserId} submitted solution for case {CaseId}. Result: {IsCorrect}",
    userId, caseId, isCorrect
);
```

### Alerts

**Critical Alerts (Immediate):**
- Error rate > 5%
- Database connection failures
- API response time > 5s (p95)

**Warning Alerts (30 min):**
- Error rate > 1%
- High memory usage (>80%)
- Disk space low (<20%)

---

## 8.14 Scalability Plan

### Horizontal Scaling

**Web API:**
- Azure App Service auto-scale rules
- Scale out at 70% CPU
- Scale in at 30% CPU
- Min: 2 instances, Max: 10 instances

**Database:**
- Read replicas for queries
- Write operations to primary
- Connection pooling

**Blob Storage:**
- Auto-scales (Azure managed)
- CDN reduces load

### Vertical Scaling

**When to Upgrade:**
- Sustained high CPU (>80%)
- Memory pressure
- Database query performance

**Tiers:**
- Start: B1 (1 core, 1.75 GB RAM)
- Growth: S1 (1 core, 1.75 GB RAM, better SLA)
- Scale: P1v2 (1 core, 3.5 GB RAM, premium)

---

## 8.15 Disaster Recovery

### Backup Strategy

**Database:**
- Automated daily backups (Azure)
- Retention: 30 days
- Point-in-time restore available
- Geo-redundant backups

**Blob Storage:**
- Zone-redundant storage (ZRS)
- Soft delete enabled (7 days)
- Versioning enabled

**Recovery Time Objective (RTO):** 1 hour  
**Recovery Point Objective (RPO):** 24 hours

### Failure Scenarios

**Database Failure:**
1. Azure auto-fails to replica
2. DNS updated automatically
3. Minimal downtime (<5 minutes)

**Region Outage:**
1. Traffic manager redirects to secondary region
2. Manual failover of database
3. Degraded service during recovery

**Data Corruption:**
1. Restore from backup
2. Replay transactions if possible
3. Notify affected users

---

## 8.16 Development Workflow

### Local Development

**Prerequisites:**
- Node.js 18+
- .NET 9 SDK
- Docker Desktop
- PostgreSQL (via Docker)

**Setup:**
```bash
# Frontend
cd frontend
npm install
npm run dev

# Backend
cd backend
dotnet restore
dotnet run

# Database (Docker)
docker-compose up -d postgres
dotnet ef database update
```

**Environment Variables:**
```
# Frontend (.env.local)
VITE_API_URL=http://localhost:5000
VITE_CDN_URL=http://localhost:5000/assets

# Backend (appsettings.Development.json)
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=casezero_dev;..."
  },
  "JwtSettings": {
    "Secret": "dev-secret-key-32-chars-min",
    "ExpirationHours": 1
  }
}
```

### Code Quality

**Linting:**
- ESLint for TypeScript
- Prettier for formatting
- StyleCop for C#

**Pre-commit Hooks:**
```bash
# Install husky
npm install -D husky lint-staged

# .husky/pre-commit
npm run lint
npm run test
dotnet format --verify-no-changes
dotnet test
```

---

## 8.17 Third-Party Dependencies

### Frontend

**Production:**
- react, react-dom
- react-router-dom
- redux, @reduxjs/toolkit
- axios
- pdfjs-dist
- tailwindcss

**Development:**
- vite
- vitest
- @testing-library/react
- eslint, prettier
- typescript

### Backend

**Production:**
- Microsoft.AspNetCore.App
- Microsoft.EntityFrameworkCore
- Npgsql.EntityFrameworkCore.PostgreSQL
- Microsoft.AspNetCore.Authentication.JwtBearer
- Serilog
- Azure.Storage.Blobs

**Development:**
- xUnit
- Moq
- FluentAssertions
- Microsoft.AspNetCore.Mvc.Testing

---

## 8.18 Summary

**Architecture:**
- **Frontend:** React SPA (TypeScript) with Redux
- **Backend:** ASP.NET Core REST API (C#)
- **Database:** PostgreSQL (Azure)
- **Storage:** Azure Blob Storage + CDN
- **Serverless:** Azure Functions for forensic timers

**Key Technologies:**
- JWT authentication
- Entity Framework Core ORM
- PDF.js for documents
- Service Worker for offline
- GitHub Actions for CI/CD

**Performance:**
- Code splitting and lazy loading
- CDN for assets
- Database query optimization
- Caching (Redis, in-memory)

**Security:**
- Input validation at all layers
- Parameterized queries (EF Core)
- CSP headers
- HTTPS enforced
- JWT with refresh tokens

**Deployment:**
- Blue-green deployment
- Automated staging deployment
- Manual production approval
- Rollback capability

**Monitoring:**
- Azure Application Insights
- Structured logging (Serilog)
- Custom metrics and alerts
- Error tracking

---

**Next Chapter:** [09-DATA-SCHEMA.md](09-DATA-SCHEMA.md) - Detailed data models and formats

**Related Documents:**
- [04-CASE-STRUCTURE.md](04-CASE-STRUCTURE.md) - Case data structure
- [03-MECHANICS.md](03-MECHANICS.md) - Mechanical implementation
- [11-TESTING.md](11-TESTING.md) - Testing strategy

---

**Revision History:**

| Date | Version | Changes | Author |
|------|---------|---------|--------|
| 2025-11-13 | 1.0 | Initial complete draft | AI Assistant |
