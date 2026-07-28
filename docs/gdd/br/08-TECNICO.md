# Capítulo 08 - Arquitetura Técnica

**Documento de Design de Jogo - CaseZero v3.0**  
**Última atualização:** 13 de novembro de 2025  
**Status:** ✅ Completo

---

## 8.1 Visão Geral

Este capítulo define a **arquitetura técnica, o stack de tecnologia e a abordagem de implementação** para CaseZero v3.0. O sistema é projetado para confiabilidade, escalabilidade e fácil manutenção, apoiando as mecânicas centrais de jogo.

**Conceitos-chave:**

- Frontend em React (TypeScript)
- Backend Web API em ASP.NET Core (.NET 8)
- Azure Functions isolated worker para geração de casos (.NET 9)
- Azure SQL Database para identidade e estado de sessão
- Azure Blob Storage + Table Storage para bundles publicados e estado de jobs
- Autenticação com JWT + ASP.NET Identity
- Geração durável de casos e perícias assíncronas

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
│  │  - 4 locales de UI: en-US, pt-BR, es-ES, fr-FR        │ │
│  │  - Fluxo admin em /case-generation                    │ │
│  │  - UI de polling de fases/progresso                   │ │
│  │  - PDF.js para leitura de documentos                  │ │
│  └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                              │
                         HTTPS/REST API
                              │
┌─────────────────────────────────────────────────────────────┐
│                    PLATAFORMA AZURE CLOUD                    │
│                                                              │
│  ┌────────────────────────────────────────────────────────┐ │
│  │      ASP.NET Core 8.0 Web API (.NET 8)                 │ │
│  │   - JWT + ASP.NET Identity                             │ │
│  │   - EF Core + estado de sessão em SQL                  │ │
│  │   - Lê bundles publicados de case.json v2              │ │
│  └────────────────────────────────────────────────────────┘ │
│                              │                               │
│              ┌───────────────┴───────────────┐              │
│              │                               │              │
│  ┌───────────▼────────────┐    ┌────────────▼──────────┐  │
│  │ Azure SQL Database      │    │ Azure Blob Storage    │  │
│  │ - Identidade + perfis   │    │ - Assets para player  │  │
│  │ - Sessões e progresso   │    │ - Bundles gerados     │  │
│  │ - Submissões/perícias   │    │ - case.json commit mk │  │
│  └────────────────────────┘    └───────────────────────┘  │
│                              │                               │
│                     ┌────────▼────────┐                     │
│                     │ Azure Table Stor│                     │
│                     │ - Jobs/progresso│                     │
│                     │ - Estado Durable│                     │
│                     └─────────────────┘                     │
│                                                              │
│  ┌────────────────────────────────────────────────────────┐ │
│  │ Azure Functions Isolated Worker (.NET 9)               │ │
│  │  - Orquestrador Durable de geração de casos            │ │
│  │  - Tasks de fase + prompts markdown externos           │ │
│  │  - Refresh de CaseGraph + validação + solver gate      │ │
│  │  - Processamento forense assíncrono/com temporizador   │ │
│  └────────────────────────────────────────────────────────┘ │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

---

## 8.4 Stack de Tecnologia

### Frontend

#### Framework principal do frontend

- **React 19** (TypeScript)
  - UI baseada em componentes
  - Hooks para estado
  - Virtual DOM para performance

#### Gerenciamento de estado

- **React Context + hooks**
  - Estado de autenticação, idioma, tema, notificações e sessão de jogo
  - Estado local de componentes quando coordenação global não é necessária

#### Roteamento

- **React Router v7**
  - Navegação client-side
  - Rotas protegidas

#### Visualização de documentos

- Renderização nativa do navegador para PDFs e mídias geradas
- Visualizadores da aplicação para emails, evidências, relatórios e outros assets do caso

#### Cliente HTTP

- **Fetch API** nativa
  - Requisições autenticadas por JWT
  - Respostas streaming e Blob quando necessário

#### Estilização

- **styled-components** + CSS da aplicação
  - Estilos orientados a componentes
  - Design responsivo

#### Ferramenta de build

- **Vite**
  - Dev server rápido
  - Hot module replacement
  - Builds otimizados

#### Testes do frontend

- **Vitest** (unitários)
- **React Testing Library** (componentes)

---

### Backend

#### Framework principal do backend

- **ASP.NET Core 8.0** (C#) para `backend/CaseZeroApi`
  - Cross-platform (Linux/Windows)
  - Alta performance
  - Injeção de dependência nativa

#### API

- **REST API** (JSON)
  - Métodos HTTP convencionais
  - Autenticação com JWT + ASP.NET Identity
  - Persistência SQL via EF Core

#### Serviço de geração de casos

- **Azure Functions isolated worker (.NET 9)** para `functions/CaseGen.Functions`
  - Orquestração com Durable Functions
  - Geração guiada por prompts no Azure OpenAI
  - Publicação em Blob/Table Storage + rastreamento de jobs

#### Processamento em background

- **Perícias assíncronas com temporizador**
  - Requisições com duração de espera
  - Conclusão processada fora da Web API

#### ORM

- **Entity Framework Core**
  - Provider SQL Server / Azure SQL
  - Migrations code-first
  - Queries LINQ

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

- **Azure SQL Database (SQL Server 2022+)**
  - Dados relacionais totalmente gerenciados
  - Transações ACID + replicação automática
  - Compatível nativamente com ASP.NET Identity e EF Core
  - Colunas NVARCHAR usadas para armazenar blocks JSON (evidências, feedbacks)

#### Por que Azure SQL Database

- Serviço PaaS oficial do Azure, com backup, HA e failover automático
- Integração direta com Azure AD, Key Vault e alertas nativos
- Provider `Microsoft.EntityFrameworkCore.SqlServer` já é padrão do projeto
- Mantém o mesmo dialeto em dev/prod (sem precisar container de banco)
- Flexível para armazenar JSON leve (NVARCHAR) sem precisar tipos especiais

---

### Infraestrutura em Nuvem

#### Hospedagem

- **Implementação verificada no repositório:** Web API em ASP.NET Core (.NET 8) + Azure Functions isolated worker (.NET 9)
  - A topologia exata de produção depende do ambiente
  - App Service / deployment slots seguem como opções operacionais de design, não como a única forma verificada de hospedagem

#### Serverless

- **Orquestração com Durable Functions**
  - Pipeline de geração de casos já implementado
  - Retries com exponential backoff entre fases
  - Conclusão forense assíncrona baseada em timer

#### Storage

- **Azure Blob Storage + Azure Table Storage**
  - Blob Storage guarda bundles publicados e assets
  - Table Storage guarda estado de jobs / progresso Durable
  - `case.json` é escrito por último como commit marker do bundle

#### Serviços de IA

- **Azure OpenAI (GPT-4o)**
  - Prompts externos em markdown vivem em `functions/CaseGen.Functions/agents/case-v2/`
  - Cada fase de geração usa um arquivo de prompt dedicado carregado via `AgentPromptCatalog`

#### Monitoramento / DevOps

- **Application Insights / GitHub Actions**
  - Alvos operacionais documentados
  - Trate como configuração dependente do ambiente, salvo quando houver provisionamento explícito

---

## 8.5 Esquema de Banco de Dados

CaseZero adota um modelo **storage-first**. O pipeline durável implementado em `functions/CaseGen.Functions` gera nativamente bundles no formato canônico **case.json v2** (veja a [CASE JSON v2 spec](../../CASE_JSON_V2_SPEC.md) e o [JSON Schema](../../../schemas/case.schema.json)). O worker isolated em .NET 9 publica os assets voltados ao jogador no **Azure Blob Storage** e escreve o `case.json` **por último**, como commit marker do bundle. A Web API em .NET 8 lê os bundles publicados no storage, enquanto o **Azure SQL Database** mantém apenas estado relacional de jogador, autenticação, sessão e telemetria.

### Divisão de responsabilidades

- **Azure Blob Storage**: bundles publicados de `case.json` v2, PDFs, imagens, áudio e demais assets voltados ao jogador. Um bundle só se torna descobrível/jogável depois que o `case.json` existe.
- **Azure Table Storage**: estado durável de jobs/orquestração e rastreamento de progresso da geração.
- **Azure SQL Database (SQL Server)**: tabelas do ASP.NET Identity mais estado do jogador (sessões, progresso, desbloqueios, submissões, requisições forenses, emails, conquistas). Nenhum conteúdo narrativo canônico do caso é duplicado aqui.
- **Cache em memória** (Redis opcional): guarda payloads de `case.json` acessados recentemente, versionados por `CaseId:CaseVersion`, para reduzir fetches do blob.

### Fluxo de geração e consumo

1. **CaseV2GenerationOrchestrator** inicia um job de geração de caso com Durable Functions.
2. **CaseBibleTask** cria a fonte de verdade privada e tipada (vítima, suspeitos, linha do tempo, solução) antes de qualquer conteúdo voltado ao jogador.
3. Cada fase carrega um prompt markdown dedicado de `functions/CaseGen.Functions/agents/case-v2/` via `AgentPromptCatalog`; o **CaseGraph** é atualizado conforme as fases terminam, e o orquestrador faz retry com exponential backoff quando uma fase falha.
4. **JobPhaseReporter** / `GenerationProgressCatalog` / `JobPhaseStatus` publicam progresso granular que a UI admin de `/case-generation` consulta e exibe.
5. **SolverTask** tenta resolver o caso usando apenas pistas visíveis ao jogador, e o **CaseV2FinalValidation** rejeita a execução se a confiança do solver ficar abaixo de **0,90** ou se falharem os gates de schema / consistência / locale / parity.
6. **CaseV2BlobPublisher** faz upload primeiro dos assets que não são `case.json` e grava o `case.json` por último. Essa escrita final é o commit marker em nível de bundle que torna o caso descobrível para a API.

### Catálogo leve de casos (opcional)

Mesmo sem armazenar a narrativa no banco, é útil manter um catálogo que aponta para o blob correspondente. O schema a seguir contém apenas metadados mínimos e evita divergência com a Storage.

```sql
CREATE TABLE CaseCatalog (
  CaseId NVARCHAR(64) NOT NULL PRIMARY KEY,
  BlobContainer NVARCHAR(100) NOT NULL,
  BlobPath NVARCHAR(200) NOT NULL,
  Version NVARCHAR(32) NOT NULL,
  Checksum NVARCHAR(128) NULL,
  Locale NVARCHAR(10) NOT NULL DEFAULT 'en-US',
  PublishedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
  IsActive BIT NOT NULL DEFAULT 1
);
```

> Se quisermos eliminar a dependência no SQL, basta remover essa tabela e deixar os serviços descobrirem casos diretamente listando blobs; os demais relacionamentos continuam válidos porque `CaseId` permanece sendo apenas uma string.

### Tabelas focadas no jogador

As tabelas de domínio abaixo fazem referência a `CaseId`, mas nunca armazenam o conteúdo do caso. Cada linha guarda somente o que aconteceu com um usuário em relação àquele caso.

#### UserCases (atribuições)

```sql
CREATE TABLE UserCases (
  UserId NVARCHAR(450) NOT NULL,
  CaseId NVARCHAR(64) NOT NULL,
  CaseVersion NVARCHAR(32) NOT NULL,
  AssignedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
  Role INT NOT NULL,
  Notes NVARCHAR(MAX) NULL,
  CONSTRAINT PK_UserCases PRIMARY KEY (UserId, CaseId),
  CONSTRAINT FK_UserCases_User FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
  CONSTRAINT FK_UserCases_Case FOREIGN KEY (CaseId) REFERENCES CaseCatalog(CaseId)
);
```

#### CaseSessions

```sql
CREATE TABLE CaseSessions (
  Id INT IDENTITY(1,1) PRIMARY KEY,
  SessionId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
  UserId NVARCHAR(450) NOT NULL REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
  CaseId NVARCHAR(64) NOT NULL,
  CaseVersion NVARCHAR(32) NOT NULL,
  Status INT NOT NULL,
  StartedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
  CompletedAt DATETIME2 NULL,
  TotalTimeMinutes INT NOT NULL DEFAULT 0,
  SubmissionAttempts INT NOT NULL DEFAULT 0,
  LastCheckpoint NVARCHAR(128) NULL,
  Notes NVARCHAR(MAX) NULL
);

CREATE UNIQUE INDEX IX_CaseSessions_UserCase ON CaseSessions(UserId, CaseId) WHERE Status = 0; -- garante 1 sessão ativa
```

#### CaseProgresses (snapshot por sessão)

```sql
CREATE TABLE CaseProgresses (
  Id INT IDENTITY(1,1) PRIMARY KEY,
  SessionId INT NOT NULL REFERENCES CaseSessions(Id) ON DELETE CASCADE,
  CaseId NVARCHAR(64) NOT NULL,
  UserId NVARCHAR(450) NOT NULL,
  UnlockedContentJson NVARCHAR(MAX) NOT NULL,
  EvidenceCount INT NOT NULL DEFAULT 0,
  InterviewsCompleted INT NOT NULL DEFAULT 0,
  Notes NVARCHAR(MAX) NULL,
  LastActivity DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

CREATE INDEX IX_CaseProgresses_Session ON CaseProgresses(SessionId);
```

#### EvidenceUnlocks (telemetria de desbloqueio)

```sql
CREATE TABLE EvidenceUnlocks (
  Id INT IDENTITY(1,1) PRIMARY KEY,
  SessionId INT NOT NULL REFERENCES CaseSessions(Id) ON DELETE CASCADE,
  CaseId NVARCHAR(64) NOT NULL,
  EvidenceId NVARCHAR(64) NOT NULL,
  UnlockedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
  Source NVARCHAR(50) NOT NULL, -- documento, entrevista, puzzle...
  PayloadJson NVARCHAR(MAX) NULL
);

CREATE INDEX IX_EvidenceUnlocks_Session ON EvidenceUnlocks(SessionId);
```

#### ForensicAnalyses

```sql
CREATE TABLE ForensicAnalyses (
  Id INT IDENTITY(1,1) PRIMARY KEY,
  SessionId INT NOT NULL REFERENCES CaseSessions(Id) ON DELETE CASCADE,
  CaseId NVARCHAR(64) NOT NULL,
  EvidenceId NVARCHAR(64) NOT NULL,
  RequestedByUserId NVARCHAR(450) NOT NULL REFERENCES AspNetUsers(Id),
  AnalysisType NVARCHAR(50) NOT NULL,
  Status INT NOT NULL,
  RequestedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
  CompletesAt DATETIME2 NOT NULL,
  CompletedAt DATETIME2 NULL,
  ResultBlobPath NVARCHAR(500) NULL,
  ResultSummary NVARCHAR(MAX) NULL
);

CREATE INDEX IX_Forensics_Status ON ForensicAnalyses(Status);
```

#### CaseSubmissions

```sql
CREATE TABLE CaseSubmissions (
  Id INT IDENTITY(1,1) PRIMARY KEY,
  SessionId INT NOT NULL REFERENCES CaseSessions(Id) ON DELETE CASCADE,
  CaseId NVARCHAR(64) NOT NULL,
  SubmittedByUserId NVARCHAR(450) NOT NULL REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
  SuspectExternalId NVARCHAR(64) NOT NULL,
  Motive NVARCHAR(MAX) NOT NULL,
  Method NVARCHAR(MAX) NOT NULL,
  EvidenceSelectedJson NVARCHAR(MAX) NOT NULL,
  CaseVersion NVARCHAR(32) NOT NULL,
  SubmittedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
  Status INT NOT NULL,
  Score FLOAT NOT NULL DEFAULT 0,
  FeedbackJson NVARCHAR(MAX) NULL,
  EvaluatedAt DATETIME2 NULL,
  EvaluatedByUserId NVARCHAR(450) NULL REFERENCES AspNetUsers(Id)
);

CREATE INDEX IX_CaseSubmissions_Session ON CaseSubmissions(SessionId, SubmittedAt);
```

#### Emails

```sql
CREATE TABLE Emails (
  Id INT IDENTITY(1,1) PRIMARY KEY,
  CaseId NVARCHAR(64) NULL,
  CaseVersion NVARCHAR(32) NULL,
  ToUserId NVARCHAR(450) NOT NULL REFERENCES AspNetUsers(Id),
  FromUserId NVARCHAR(450) NOT NULL REFERENCES AspNetUsers(Id),
  Subject NVARCHAR(200) NOT NULL,
  Content NVARCHAR(MAX) NOT NULL,
  Preview NVARCHAR(280) NULL,
  SentAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
  IsRead BIT NOT NULL DEFAULT 0,
  Type INT NOT NULL,
  AttachmentsJson NVARCHAR(MAX) NULL,
  IsSystemGenerated BIT NOT NULL DEFAULT 0
);

CREATE INDEX IX_Emails_User ON Emails(ToUserId, IsRead);
```

#### Achievements (opcional)

```sql
CREATE TABLE Achievements (
  AchievementId NVARCHAR(50) PRIMARY KEY,
  Name NVARCHAR(100) NOT NULL,
  Description NVARCHAR(400) NOT NULL,
  BadgeIcon NVARCHAR(200) NULL
);

CREATE TABLE UserAchievements (
  UserId NVARCHAR(450) NOT NULL REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
  AchievementId NVARCHAR(50) NOT NULL REFERENCES Achievements(AchievementId) ON DELETE CASCADE,
  CaseId NVARCHAR(64) NULL,
  UnlockedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
  PRIMARY KEY (UserId, AchievementId)
);
```

> O campo `CaseId` em `UserAchievements` é apenas referencial, permitindo analytics por caso, mas não implica em cópia de dados narrativos.

---

---

## 8.6 Endpoints da API

> **Nota de implementação:** as rotas e payloads desta seção são documentação ilustrativa, não um contrato congelado. O backend ativo é a Web API em .NET 8, e a semântica atual de gameplay usa o enum compartilhado de sete níveis (`Rookie`, `Detective`, `Detective2`, `Sergeant`, `Lieutenant`, `Captain`, `Commander`) junto com regras de promoção por casos corretos avaliados cumulativos, em vez de thresholds baseados em XP.

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
  "rank": "Detective",
  "gradedCorrectCases": 4,
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
difficulty: Rookie|Detective|Detective2|Sergeant|Lieutenant|Captain|Commander (opcional)
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
      "difficulty": "Sergeant",
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
  "difficulty": "Sergeant",
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
  "gradedCorrectResolveAwarded": 1,
  "feedback": {
    "summary": "Excelente trabalho, Detetive!",
    "culpritCorrect": true,
    "keyEvidenceCited": true,
    "explanationQuality": "thorough"
  },
  "newRank": "Sergeant",
  "rankUp": false,
  "gradedCorrectCases": 18,
  "nextRankThreshold": 28
}
```

Resposta incorreta 200 OK (JSON):

```json
{
  "submissionId": "uuid",
  "isCorrect": false,
  "gradedCorrectResolveAwarded": 0,
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
  "rank": "Lieutenant",
  "gradedCorrectCases": 28,
  "nextRankThreshold": 44,
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
      "gradedCorrectResolve": 1
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
    "Rookie": { "solved": 2, "failed": 0, "successRate": 100 },
    "Detective": { "solved": 4, "failed": 1, "successRate": 80 },
    "Sergeant": { "solved": 3, "failed": 1, "successRate": 75 }
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
  "rank": "Detective",
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

## 8.8 Implementação de Perícias Assíncronas

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
// Projeções evitam carregar coleções pesadas (Evidences, Suspects)
var cases = await _db.Cases
  .Select(c => new CaseListDto {
    CaseId = c.Id,
    Title = c.Title,
    EstimatedDifficulty = c.EstimatedDifficultyLevel,
    Status = c.Status
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

> **Implementação atual:** o repositório possui um workflow de deploy para
> desenvolvimento (`.github/workflows/cd-dev.yml`) e um workflow de infraestrutura
> acionado manualmente (`.github/workflows/infrastructure-3tier.yml`). Slots de
> staging, fluxo blue-green de produção, rollback automático e aprovação manual
> de produção descritos abaixo são arquitetura-alvo, não automações implementadas.

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

- Node.js 20+
- **.NET 8 SDK** para `backend/CaseZeroApi`
- **.NET 9 SDK** para `functions/CaseGen.Functions` e `functions/CaseGen.Functions.Tests` (devem permanecer em .NET 9)
- Docker Desktop (opcional)
- Acesso a Azure SQL Database **ou** SQL Server local (Azure SQL Edge / container mssql)

#### Setup

```bash
# Frontend
cd frontend
npm install
npm run dev

# Backend API (.NET 8)
cd backend/CaseZeroApi
dotnet restore
dotnet run

# Functions de geração (.NET 9)
cd ../../functions/CaseGen.Functions
dotnet restore

# Banco (opcional via Docker)
docker run -e 'ACCEPT_EULA=Y' -e 'SA_PASSWORD=YourStrong!Pass123' \
  -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest

# Aplicar migrações na instância configurada
cd ../../backend/CaseZeroApi
dotnet ef database update
```

#### Variáveis de ambiente

```dotenv
# Frontend (.env.local)
VITE_API_URL=http://localhost:5000
VITE_CDN_URL=http://localhost:5000/assets
```

```jsonc
// backend/CaseZeroApi/appsettings.Development.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=CaseZeroDev;User ID=sa;Password=YourStrong!Pass123;Encrypt=False;TrustServerCertificate=True"
  },
  "JwtSettings": {
    "Secret": "dev-secret-key-32-chars-min",
    "ExpirationHours": 1
  },
  "CasesBasePath": "../../cases"
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

- **Web API:** Microsoft.AspNetCore.App, ASP.NET Identity, Microsoft.EntityFrameworkCore.SqlServer, Microsoft.AspNetCore.Authentication.JwtBearer
- **Functions de geração:** Microsoft.Azure.Functions.Worker, extensão Durable Functions, Azure.Storage.Blobs, Azure.Data.Tables, SDK cliente do Azure OpenAI
- **Serviços compartilhados:** Serilog

#### Desenvolvimento (Backend)

- xUnit
- Moq
- FluentAssertions
- Microsoft.AspNetCore.Mvc.Testing

---

## 8.18 Resumo

### Arquitetura (Resumo)

- **Frontend:** React SPA (TypeScript) com quatro locales de UI implementados e experiência admin em `/case-generation`
- **Backend:** ASP.NET Core 8 REST API (`backend/CaseZeroApi`)
- **Geração de casos:** Azure Functions isolated worker em .NET 9 (`functions/CaseGen.Functions`)
- **Banco:** Azure SQL Database para auth/estado de sessão/estado do jogador
- **Storage:** Azure Blob Storage + Table Storage, com `case.json` gravado por último como marcador de publicação

### Tecnologias-chave

- Autenticação com JWT + ASP.NET Identity
- Orquestração com Durable Functions e retries com exponential backoff
- Azure OpenAI (GPT-4o) com prompts markdown externos
- PDF.js para documentos
- Relato de progresso de geração fase a fase

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

- Deploy automatizado de desenvolvimento via `cd-dev.yml`
- Operações manuais de infraestrutura dev/prod via `infrastructure-3tier.yml`
- Deploy blue-green em produção e rollback automático permanecem no roadmap

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
