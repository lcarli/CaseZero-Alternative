# Capítulo 08 - Arquitetura Técnica

**Documento de Design de Jogo - CaseZero v3.0**  
**Última atualização:** 13 de novembro de 2025  
**Status:** ✅ Completo

---

## 8.1 Visão Geral

Este capítulo define a **arquitetura técnica, o stack de tecnologia e a abordagem de implementação** para CaseZero v3.0. O sistema é projetado para confiabilidade, escalabilidade e fácil manutenção, apoiando as mecânicas centrais de jogo.

**Conceitos-chave:**

- Frontend em React (TypeScript)
- Backend em C# ASP.NET Core
- Infraestrutura em nuvem Azure
- Banco de dados PostgreSQL
- Blob Storage para assets
- Autenticação via JWT
- Perícias em tempo real com jobs em background

---

## 8.2 Filosofia de Arquitetura

### Princípios Centrais

#### 1. Separação de Responsabilidades

- Frontend cuida da apresentação e interação
- Backend concentra a lógica de negócio e dados
- Banco armazena estado persistente
- Storage guarda arquivos binários

#### 2. Backend Stateless

- Servidor não mantém estado de sessão
- Todo estado vive no banco ou no token JWT
- Possibilita escalonamento horizontal
- Resistente a reinicializações

#### 3. Frontend Offline-First

- Dados de caso em cache local
- Leituras funcionam offline
- Escritas entram em fila para sincronização
- Service worker para modo PWA

#### 4. Segurança por Design

- Autenticação obrigatória para todas as ações
- Autorização por acesso ao caso
- Validação de entrada em todas as camadas
- Nenhum segredo guardado no cliente

---

## 8.3 Diagrama da Arquitetura do Sistema

```text
┌─────────────────────────────────────────────────────────────┐
│                        NAVEGADOR DO USUÁRIO                 │
│  ┌────────────────────────────────────────────────────────┐ │
│  │            React SPA (TypeScript)                      │ │
│  │  - Componentes de UI estilo desktop                    │ │
│  │  - Gerenciamento de estado (Redux)                     │ │
│  │  - PDF.js para leitura de documentos                   │ │
│  │  - Service Worker (suporte offline)                    │ │
│  └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                              │
                         HTTPS/REST API
                              │
┌─────────────────────────────────────────────────────────────┐
│                    PLATAFORMA AZURE CLOUD                    │
│                                                              │
│  ┌────────────────────────────────────────────────────────┐ │
│  │        Azure App Service (Linux)                       │ │
│  │  ┌──────────────────────────────────────────────────┐  │ │
│  │  │   ASP.NET Core 9.0 Web API (C#)                  │  │ │
│  │  │   - Controllers REST                             │  │ │
│  │  │   - Autenticação (JWT)                           │  │ │
│  │  │   - Serviços de negócio                         │  │ │
│  │  │   - Entity Framework Core                       │  │ │
│  │  └──────────────────────────────────────────────────┘  │ │
│  └────────────────────────────────────────────────────────┘ │
│                              │                               │
│              ┌───────────────┴───────────────┐              │
│              │                               │              │
│  ┌───────────▼────────────┐    ┌────────────▼──────────┐  │
│  │ Azure SQL Database      │    │ Azure Blob Storage    │  │
│  │ (PostgreSQL)            │    │ - Documentos do caso  │  │
│  │ - Contas de usuário     │    │ - Fotos de evidências │  │
│  │ - Sessões de caso       │    │ - Laudos forenses     │  │
│  │ - Solicitações de perícia│   │ - Fotos de suspeitos  │  │
│  │ - Submissões            │    │                       │  │
│  │ - Dados de progressão   │    │                       │  │
│  └────────────────────────┘    └───────────────────────┘  │
│                                                              │
│  ┌────────────────────────────────────────────────────────┐ │
│  │        Azure Functions (Serverless)                    │ │
│  │  - Worker do temporizador de perícia                   │ │
│  │  - Serviço de notificações por email                   │ │
│  │  - Agregação analítica                                 │ │
│  └────────────────────────────────────────────────────────┘ │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

---

## 8.4 Stack de Tecnologia

### Frontend

#### Framework principal do frontend

- **React 18+** (TypeScript)
  - UI baseada em componentes
  - Hooks para estado
  - Virtual DOM para performance

#### Gerenciamento de estado

- **Redux Toolkit**
  - Estado centralizado
  - Atualizações previsíveis
  - Debug time-travel

#### Roteamento

- **React Router v6**
  - Navegação client-side
  - Lazy loading para code splitting
  - Rotas protegidas

#### Visualização de documentos

- **PDF.js** (Mozilla)
  - Renderização nativa de PDF
  - Sem conversão no servidor
  - Seleção de texto e busca

#### Cliente HTTP

- **Axios**
  - Requisições baseadas em Promise
  - Interceptadores para auth
  - Cancelamento de requisições

#### Estilização

- **CSS Modules** + **Tailwind CSS**
  - Estilos isolados
  - Utilitários CSS
  - Design responsivo

#### Ferramenta de build

- **Vite**
  - Dev server rápido
  - Hot module replacement
  - Builds otimizados

#### Testes do frontend

- **Vitest** (unitários)
- **React Testing Library** (componentes)
- **Playwright** (E2E)

---

### Backend

#### Framework principal do backend

- **ASP.NET Core 9.0** (C#)
  - Cross-platform (Linux/Windows)
  - Alta performance
  - Injeção de dependência nativa

#### API

- **REST API** (JSON)
  - Métodos HTTP convencionais
  - Endpoints por recurso
  - Versionamento via URL

#### Autenticação

- **JWT (JSON Web Tokens)**
  - Autenticação stateless
  - Autorização baseada em claims
  - Suporte a refresh token

#### ORM

- **Entity Framework Core 9.0**
  - Migrations code-first
  - Queries LINQ
  - Change tracking

#### Jobs em background

- **Azure Functions** (Timer Triggers)
  - Conclusão de perícias
  - Disparo de notificações
  - Agregação de analytics

#### Logging

- **Serilog**
  - Logging estruturado
  - Integração com Azure Application Insights
  - Níveis e filtros configuráveis

#### Testes do backend

- **xUnit** (unitários)
- **Moq** (mocks)
- **WebApplicationFactory** (integração)

---

### Banco de Dados

#### Banco primário

- **PostgreSQL 15+** (Azure Database for PostgreSQL)
  - Dados relacionais
  - Transações ACID
  - Suporte a JSON para armazenar case.json

#### Por que PostgreSQL

- Open source amplamente suportado
- Ótima performance
- JSON/JSONB para dados flexíveis
- Integração sólida com Azure

---

### Infraestrutura em Nuvem

#### Hospedagem

- **Azure App Service** (Web API)
  - Plataforma gerenciada
  - Auto scaling
  - Slots de deployment (staging/prod)

#### Serverless

- **Azure Functions**
  - Execução orientada a eventos
  - Pagamento por uso
  - Timer triggers para perícias

#### Storage

- **Azure Blob Storage**
  - CDN para assets estáticos
  - Tier quente para casos ativos
  - Tier frio para casos arquivados

#### CDN

- **Azure CDN**
  - Distribuição global
  - Menor latência
  - HTTPS por padrão

#### Monitoramento

- **Azure Application Insights**
  - Telemetria de performance
  - Rastreamento de erros
  - Analytics de uso

#### DevOps

- **GitHub Actions**
  - Pipelines CI/CD
  - Testes automatizados
  - Deploy automatizado

---

## 8.5 Esquema de Banco de Dados

### Tabelas centrais

#### Users

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

#### UserProgress

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

#### Cases (metadados)

```sql
CREATE TABLE Cases (
    CaseId VARCHAR(50) PRIMARY KEY,
    Title VARCHAR(255) NOT NULL,
    Difficulty VARCHAR(20) NOT NULL CHECK (Difficulty IN ('Easy', 'Medium', 'Hard', 'Expert')),
    EstimatedTimeHours DECIMAL(4,1) NOT NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'Published',
    CaseDataJson JSONB NOT NULL, -- case.json completo aqui
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW(),
    UpdatedAt TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_cases_difficulty ON Cases(Difficulty);
CREATE INDEX idx_cases_status ON Cases(Status);
```

#### CaseSessions (progresso do jogador por caso)

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

#### ForensicRequests (controle do timer de perícia)

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
    ResultBlobPath VARCHAR(500) -- Caminho do laudo no blob
);

CREATE INDEX idx_forensics_session ON ForensicRequests(SessionId);
CREATE INDEX idx_forensics_status ON ForensicRequests(Status);
CREATE INDEX idx_forensics_completes_at ON ForensicRequests(CompletesAt) WHERE Status = 'Pending';
```

#### CaseSubmissions (tentativas de solução)

```sql
CREATE TABLE CaseSubmissions (
    SubmissionId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    SessionId UUID NOT NULL REFERENCES CaseSessions(SessionId),
    SubmittedAt TIMESTAMP NOT NULL DEFAULT NOW(),
    CulpritSelected VARCHAR(50) NOT NULL,
    MotiveExplanation TEXT NOT NULL,
    MethodExplanation TEXT NOT NULL,
    EvidenceSelected JSONB NOT NULL, -- Array de IDs de evidência
    IsCorrect BOOLEAN NOT NULL,
    XPAwarded INTEGER NOT NULL,
    FeedbackJson JSONB -- Feedback detalhado
);

CREATE INDEX idx_submissions_session ON CaseSubmissions(SessionId);
CREATE INDEX idx_submissions_submitted_at ON CaseSubmissions(SubmittedAt);
```

#### Achievements (opcional)

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

## 8.6 Endpoints da API

### Autenticação (API)

#### POST /api/auth/register

Request (JSON):

```json
{
  "username": "alex_detective",
  "email": "alex@example.com",
  "password": "SecurePass123!"
}
```

Response 201 Created (JSON):

```json
{
  "userId": "uuid",
  "username": "alex_detective",
  "token": "jwt-token",
  "refreshToken": "refresh-token"
}
```

#### POST /api/auth/login

Request (JSON):

```json
{
  "email": "alex@example.com",
  "password": "SecurePass123!"
}
```

Response 200 OK (JSON):

```json
{
  "userId": "uuid",
  "username": "alex_detective",
  "rank": "Detective I",
  "xp": 3250,
  "token": "jwt-token",
  "refreshToken": "refresh-token"
}
```

#### POST /api/auth/refresh

Request (JSON):

```json
{
  "refreshToken": "refresh-token"
}
```

Response 200 OK (JSON):

```json
{
  "token": "new-jwt-token",
  "refreshToken": "new-refresh-token"
}
```

---

### Casos

#### GET /api/cases

Query params:

```text
difficulty: Easy|Medium|Hard|Expert (opcional)
status: Published|Active|Solved (opcional)
page: int (padrão 1)
pageSize: int (padrão 20)
```

Response 200 OK (JSON):

```json
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
    }
  ],
  "totalCount": 45,
  "page": 1,
  "pageSize": 20
}
```

#### GET /api/cases/{caseId}

Response 200 OK (JSON):

```json
{
  "caseId": "CASE-2024-001",
  "title": "The Downtown Office Murder",
  "difficulty": "Medium",
  "caseData": {},
  "userSession": {
    "sessionId": "uuid",
    "startedAt": "2025-11-13T10:00:00Z",
    "totalTimeMinutes": 145,
    "submissionAttempts": 0,
    "status": "Active"
  }
}
```

#### POST /api/cases/{caseId}/start

Response 201 Created (JSON):

```json
{
  "sessionId": "uuid",
  "caseId": "CASE-2024-001",
  "startedAt": "2025-11-13T10:00:00Z"
}
```

---

### Documentos & Evidências

#### GET /api/cases/{caseId}/documents/{documentId}

Response 200 OK (JSON):

```json
{
  "documentId": "DOC-001",
  "type": "PoliceReport",
  "title": "Initial Incident Report",
  "blobUrl": "https://cdn.casezero.com/cases/2024-001/docs/police-report.pdf",
  "pageCount": 3
}
```

#### GET /api/cases/{caseId}/evidence/{evidenceId}

Response 200 OK (JSON):

```json
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

### Perícias

#### POST /api/forensics/request

Request payload JSON:

```json
{
  "sessionId": "uuid",
  "evidenceId": "EV-001",
  "analysisType": "Ballistics"
}
```

Response 201 Created (JSON):

```json
{
  "requestId": "uuid",
  "evidenceId": "EV-001",
  "analysisType": "Ballistics",
  "requestedAt": "2025-11-13T10:00:00Z",
  "completesAt": "2025-11-13T22:00:00Z",
  "status": "Pending"
}
```

#### GET /api/forensics/session/{sessionId}

Response 200 OK (JSON):

```json
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

#### GET /api/forensics/{requestId}

Response 200 OK (JSON):

```json
{
  "requestId": "uuid",
  "status": "Completed",
  "reportUrl": "https://cdn.casezero.com/cases/2024-001/forensics/ballistics-ev001.pdf"
}
```

---

### Submissão da solução

#### POST /api/cases/{caseId}/submit

Request payload JSON:

```json
{
  "sessionId": "uuid",
  "culprit": "SUSP-001",
  "motive": "Financial desperation. Torres owed $500k...",
  "method": "Used building access to enter office...",
  "evidenceSelected": ["EV-001", "EV-004", "EV-007", "DOC-009"]
}
```

Resposta 200 OK (JSON):

```json
{
  "submissionId": "uuid",
  "isCorrect": true,
  "xpAwarded": 450,
  "feedback": {
    "summary": "Excelente trabalho, Detetive!",
    "culpritCorrect": true,
    "keyEvidenceCited": true,
    "explanationQuality": "thorough"
  },
  "newRank": "Detective I",
  "rankUp": false,
  "progressToNextRank": 79
}
```

Resposta incorreta 200 OK (JSON):

```json
{
  "submissionId": "uuid",
  "isCorrect": false,
  "xpAwarded": 0,
  "attemptsRemaining": 2,
  "feedback": {
    "summary": "Sua conclusão não corresponde às evidências.",
    "hints": [
      "O suspeito indicado tem álibi sólido.",
      "Reveja o laudo de DNA.",
      "A linha do tempo apresenta discrepâncias."
    ]
  }
}
```

---

### Perfil do usuário & progressão

#### GET /api/users/profile

Response 200 OK (JSON):

```json
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

#### GET /api/users/stats

Response 200 OK (JSON):

```json
{
  "overall": {},
  "byDifficulty": {
    "Easy": { "solved": 4, "failed": 0, "successRate": 100 },
    "Medium": { "solved": 5, "failed": 1, "successRate": 83.3 },
    "Hard": { "solved": 3, "failed": 1, "successRate": 75 }
  }
}
```

---

### Notas

#### PUT /api/cases/{caseId}/notes

Request payload JSON:

```json
{
  "sessionId": "uuid",
  "notesText": "SUSPECTS:\n- Torres: Financial motive..."
}
```

Response 200 OK (JSON):

```json
{
  "saved": true,
  "lastSavedAt": "2025-11-13T10:15:00Z"
}
```

#### GET /api/cases/{caseId}/notes

Response 200 OK (JSON):

```json
{
  "notesText": "SUSPECTS:\n- Torres: Financial motive...",
  "lastSavedAt": "2025-11-13T10:15:00Z"
}
```

---

## 8.7 Autenticação & Autorização

### Estrutura do token JWT

#### Payload

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

#### Tempo de vida

- Access token: 1 hora
- Refresh token: 30 dias
- Expiração deslizante ao usar

### Regras de autorização

#### Acesso ao caso

- Usuário deve ter iniciado sessão no caso
- OU caso disponível para sua patente

#### Regras para perícias

- Precisa possuir a sessão do caso
- Evidência deve pertencer ao caso

#### Submissão

- Sessão deve pertencer ao usuário
- Caso precisa estar ativo (não falhou)
- Deve haver tentativas restantes

---

## 8.8 Implementação das Perícias em Tempo Real

### Azure Function Timer Worker

**Função:** `ForensicTimerWorker`  
**Trigger:** Timer (a cada 5 minutos)

#### Lógica

```csharp
[Function("ForensicTimerWorker")]
public async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo timer)
{
    // 1. Buscar perícias pendentes que já deveriam finalizar
    var completedRequests = await _db.ForensicRequests
        .Where(r => r.Status == "Pending" && r.CompletesAt <= DateTime.UtcNow)
        .ToListAsync();
    
    foreach (var request in completedRequests)
    {
        // 2. Gerar laudo (template + dados)
        var report = await _forensicService.GenerateReport(request);
        
        // 3. Enviar PDF ao blob storage
        var blobPath = await _blobService.UploadReportAsync(report);
        
        // 4. Atualizar banco
        request.Status = "Completed";
        request.CompletedAt = DateTime.UtcNow;
        request.ResultBlobPath = blobPath;
        
        // 5. Notificar usuário por email
        await _notificationService.NotifyForensicComplete(request);
    }
    
    await _db.SaveChangesAsync();
}
```

#### Por que funciona

- Roda independente da Web API
- Escala automaticamente
- Tolerante a falhas (retries)
- Evita polling do cliente

---

## 8.9 Gestão de Estado no Frontend

### Estrutura da store Redux

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

### Ações principais

```typescript
// Auth
authActions.login(credentials)
authActions.logout()
authActions.refreshToken()

// Casos
caseActions.fetchAvailableCases()
caseActions.loadCase(caseId)
caseActions.startCase(caseId)

// Documentos
documentActions.fetchDocument(caseId, documentId)

// Evidências
evidenceActions.fetchEvidence(caseId, evidenceId)

// Perícias
forensicActions.requestAnalysis(evidenceId, type)
forensicActions.fetchPendingForensics(sessionId)

// Submissão
submissionActions.submitSolution(solution)

// Notas
notesActions.updateNotes(text)
notesActions.saveNotes()
```

---

## 8.10 Otimização de Performance

### Frontend (Performance)

#### Code splitting

```typescript
// Lazy load dos apps de caso
const CaseFiles = lazy(() => import('./apps/CaseFiles'));
const ForensicsLab = lazy(() => import('./apps/ForensicsLab'));
```

#### Otimização de assets

- PDF.js carrega páginas sob demanda
- Imagens usam srcset responsivo
- Fotos de evidência carregam ao rolar
- CDN distribui todos os arquivos estáticos

#### Estratégia de cache

```typescript
// Service Worker cacheia:
// - Metadados de casos (24 horas)
// - Documentos (indefinido, versionados)
// - Fotos de evidência (indefinido)
// - Respostas de API (5 minutos)
```

### Backend (Performance)

#### Queries otimizadas

```csharp
// Projeções evitam carregar case.json inteiro
var cases = await _db.Cases
    .Select(c => new CaseListDto {
        CaseId = c.CaseId,
        Title = c.Title,
        Difficulty = c.Difficulty
        // Sem carregar CaseDataJson
    })
    .ToListAsync();
```

#### Caching

- Redis para casos acessados frequentemente
- CDN para arquivos estáticos
- Cache em memória para metadados

#### Pool de conexões

- Pool do banco (máximo 100)
- HttpClient reutilizado (singleton)

---

## 8.11 Medidas de Segurança

### Validação de entrada

#### Camada de API

```csharp
[HttpPost("submit")]
public async Task<IActionResult> SubmitSolution(
  [FromBody, Required] SubmissionDto submission)
{
  // Validação de modelo
  if (!ModelState.IsValid)
    return BadRequest(ModelState);
    
  // Validação de negócio
  if (submission.Motive.Length < 50)
    return BadRequest("Motive explanation too short");
    
  // Autorização
  var session = await GetUserSession(submission.SessionId);
  if (session.UserId != User.GetUserId())
    return Unauthorized();
    
  // Processamento...
}
```

### Prevenção de SQL Injection

#### Queries parametrizadas

```csharp
// Entity Framework parametriza automaticamente
var user = await _db.Users
  .FirstOrDefaultAsync(u => u.Email == email);
```

### Prevenção de XSS

#### Content Security Policy

```http
Content-Security-Policy:
  default-src 'self';
  script-src 'self' 'unsafe-inline' 'unsafe-eval';
  style-src 'self' 'unsafe-inline';
  img-src 'self' data: https://cdn.casezero.com;
  font-src 'self' data:;
```

#### Sanitização de input

```typescript
// Sanitiza notas antes de renderizar
import DOMPurify from 'dompurify';
const cleanNotes = DOMPurify.sanitize(notes);
```

### Configuração de CORS

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

## 8.12 Estratégia de Deploy

### Ambientes

#### Desenvolvimento

- Máquinas locais
- Docker Compose para dependências
- Hot reload habilitado

#### Staging

- Azure App Service (slot de staging)
- Banco separado
- Configuração espelhando produção
- Deploy automático do branch `develop`

#### Produção

- Azure App Service (slot de produção)
- Banco produtivo (replicado)
- CDN para assets
- Deploy via GitHub Actions (gatilho manual)

### Pipeline CI/CD (GitHub Actions)

#### Em Pull Request

```yaml
- Rodar linters (ESLint, Prettier)
- Executar testes unitários (frontend + backend)
- Executar testes de integração
- Build da imagem Docker
- Deploy para staging
- Rodar E2E contra staging
```

#### Ao fazer merge em main

```yaml
- Todos os checks do PR aprovados
- Build da imagem Docker de produção
- Tag com versão
- Push para Azure Container Registry
- Aprovação manual
- Deploy para produção (blue-green)
- Smoke tests
- Monitoramento por 30 minutos
```

### Estratégia de rollback

#### Blue-Green Deployment

1. Deploy no slot inativo (green)
2. Executar smoke tests no green
3. Swap de slots (green → produção)
4. Monitorar métricas por 30 minutos
5. Em caso de problema, swap imediato de volta

---

## 8.13 Monitoramento & Observabilidade

### Application Insights

#### Métricas rastreadas

- Latência das requisições (p50, p95, p99)
- Taxa de erros
- Usuários ativos
- Taxa de conclusão de casos
- Tempo das solicitações de perícia

#### Eventos customizados

```csharp
_telemetry.TrackEvent("CaseSolved", new Dictionary<string, string> {
    { "CaseId", caseId },
    { "Difficulty", difficulty },
    { "Attempts", attempts.ToString() },
    { "TimeMinutes", timeMinutes.ToString() }
});
```

### Logging (Observabilidade)

#### Níveis de log

- **Trace:** Diagnóstico detalhado
- **Debug:** Informações de desenvolvimento
- **Information:** Fluxo geral
- **Warning:** Situações inesperadas porém tratadas
- **Error:** Falhas que exigem ação
- **Critical:** Crash da aplicação

#### Logging estruturado

```csharp
_logger.LogInformation(
    "User {UserId} submitted solution for case {CaseId}. Result: {IsCorrect}",
    userId, caseId, isCorrect
);
```

### Alertas

#### Críticos (imediatos)

- Taxa de erro > 5%
- Falhas de conexão com banco
- Resposta da API > 5 s (p95)

#### Avisos (janela de 30 min)

- Taxa de erro > 1%
- Uso de memória > 80%
- Espaço em disco < 20%

---

## 8.14 Plano de Escalabilidade

### Escala horizontal

#### Web API

- Auto-scale no Azure App Service
- Scale out com CPU > 70%
- Scale in com CPU < 30%
- Mínimo 2 instâncias, máximo 10

#### Banco (Backup)

- Réplicas de leitura para consultas
- Escritas no primário
- Pool de conexões

#### Blob Storage (Backup)

- Escala automática (Azure gerenciado)
- CDN reduz carga

### Escala vertical

#### Quando subir de tier

- CPU alta sustentada (>80%)
- Pressão de memória
- Performance de queries degradando

#### Tiers

- Inicial: B1 (1 core, 1,75 GB RAM)
- Crescimento: S1 (1 core, 1,75 GB, SLA melhor)
- Escala: P1v2 (1 core, 3,5 GB, premium)

---

## 8.15 Recuperação de Desastre

### Estratégia de backup

#### Banco

- Backups automáticos diários (Azure)
- Retenção: 30 dias
- Restore point-in-time disponível
- Backups geo-redundantes

#### Blob Storage

- Zone-redundant (ZRS)
- Soft delete (7 dias)
- Versionamento ativo

**RTO (Recovery Time Objective):** 1 hora  
**RPO (Recovery Point Objective):** 24 horas

### Cenários de falha

#### Falha no banco

1. Azure faz failover para réplica
2. DNS atualizado automaticamente
3. Downtime mínimo (<5 minutos)

#### Outage regional

1. Traffic Manager redireciona para região secundária
2. Failover manual do banco
3. Serviço degradado durante recuperação

#### Corrupção de dados

1. Restaurar backup
2. Reexecutar transações se possível
3. Notificar usuários impactados

---

## 8.16 Fluxo de Desenvolvimento

### Ambiente local

#### Pré-requisitos

- Node.js 18+
- .NET 9 SDK
- Docker Desktop
- PostgreSQL (via Docker)

#### Setup

```bash
# Frontend
cd frontend
npm install
npm run dev

# Backend
cd backend
dotnet restore
dotnet run

# Banco (Docker)
docker-compose up -d postgres
dotnet ef database update
```

#### Variáveis de ambiente

```dotenv
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

```json
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

### Qualidade de código

#### Linting

- ESLint para TypeScript
- Prettier para formatação
- StyleCop para C#

#### Hooks de pre-commit

```bash
# Instalar husky
npm install -D husky lint-staged

# .husky/pre-commit
npm run lint
npm run test
dotnet format --verify-no-changes
dotnet test
```

---

## 8.17 Dependências de Terceiros

### Dependências do Frontend

#### Produção (Frontend)

- react, react-dom
- react-router-dom
- redux, @reduxjs/toolkit
- axios
- pdfjs-dist
- tailwindcss

#### Desenvolvimento (Frontend)

- vite
- vitest
- @testing-library/react
- eslint, prettier
- typescript

### Dependências do Backend

#### Produção (Backend)

- Microsoft.AspNetCore.App
- Microsoft.EntityFrameworkCore
- Npgsql.EntityFrameworkCore.PostgreSQL
- Microsoft.AspNetCore.Authentication.JwtBearer
- Serilog
- Azure.Storage.Blobs

#### Desenvolvimento (Backend)

- xUnit
- Moq
- FluentAssertions
- Microsoft.AspNetCore.Mvc.Testing

---

## 8.18 Resumo

### Arquitetura (Resumo)

- **Frontend:** React SPA (TypeScript) com Redux
- **Backend:** ASP.NET Core REST API (C#)
- **Banco:** PostgreSQL (Azure)
- **Storage:** Azure Blob Storage + CDN
- **Serverless:** Azure Functions para timers forenses

### Tecnologias-chave

- Autenticação JWT
- ORM Entity Framework Core
- PDF.js para documentos
- Service Worker para offline
- GitHub Actions para CI/CD

### Performance (Resumo)

- Code splitting e lazy loading
- CDN para assets
- Otimização de queries
- Cache (Redis e memória)

### Segurança (Resumo)

- Validação em todas as camadas
- Queries parametrizadas (EF Core)
- Headers CSP
- HTTPS obrigatório
- JWT com refresh tokens

### Deploy (Resumo)

- Blue-green deployment
- Staging automatizado
- Aprovação manual em produção
- Rollback imediato

### Monitoramento (Resumo)

- Azure Application Insights
- Logging estruturado (Serilog)
- Métricas e alertas customizados
- Rastreamento de erros

---

**Próximo capítulo:** [09-ESQUEMA-DE-DADOS.md](09-ESQUEMA-DE-DADOS.md) – Modelos de dados e formatos detalhados

### Documentos relacionados

- [04-ESTRUTURA-DE-CASO.md](04-ESTRUTURA-DE-CASO.md) – Estrutura dos casos
- [03-MECANICAS.md](03-MECANICAS.md) – Implementação das mecânicas
- [11-TESTES.md](11-TESTES.md) – Estratégia de testes

---

**Histórico de revisões:**

| Data | Versão | Mudanças | Autor |
|------|--------|----------|-------|
| 13/11/2025 | 1.0 | Tradução completa para PT-BR | Assistente de IA |
