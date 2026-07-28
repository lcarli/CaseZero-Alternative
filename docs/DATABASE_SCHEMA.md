# Schema do Banco de Dados - Sistema CaseZero

## Overview

O `ApplicationDbContext` do backend (`backend\CaseZeroApi`) herda de `IdentityDbContext<User>` e combina tabelas do ASP.NET Identity com tabelas próprias do domínio investigativo.

**Fonte de verdade deste documento:**
- Model snapshot atual: `backend\CaseZeroApi\Migrations\ApplicationDbContextModelSnapshot.cs`
- Mapeamentos EF Core: `backend\CaseZeroApi\Data\ApplicationDbContext.cs`
- Modelos: `backend\CaseZeroApi\Models\*.cs`

> Observação importante: o projeto da API roda em **.NET 8.0** (`CaseZeroApi.csproj`), mas o snapshot/migrations atuais foram gerados com **EF Core 9.x** para **SQL Server**.

---

## Database Engine

| Característica | Valor atual |
|----------------|-------------|
| **API target** | .NET 8.0 |
| **ORM** | Entity Framework Core (packages 9.0.x no projeto) |
| **Provider padrão em runtime** | SQL Server / Azure SQL |
| **Provider opcional para dev local** | SQLite |
| **Migrations** | Code-First com snapshot SQL Server |
| **Inicialização local com SQLite** | `EnsureCreated()` (não aplica migrations SQL Server) |
| **Connection string** | `ConnectionStrings:DefaultConnection` |
| **Fallback SQLite** | `Data Source=casezero-dev.db` quando `UseSqlite=true` e não houver connection string |

### Comportamento por ambiente

- `Program.cs` usa **SQL Server por padrão**.
- Se `UseSqlite=true` **ou** a connection string começar com `Data Source=`, o app usa **SQLite**.
- O `ApplicationDbContextFactory` de design-time usa **sempre SQL Server**, por isso o snapshot e as migrations atuais refletem tipos SQL Server.

---

## Diagrama de Relacionamentos (ERD)

```text
AspNetUsers
 ├─< AuditLogs
 ├─< CaseProgresses >─ Cases
 ├─< CaseSessions
 │    ├─< CaseSessionVisibleAssets   (FK opcional por shadow column CaseSessionId)
 │    ├─< CaseSessionVisibleEmails   (FK opcional por shadow column CaseSessionId)
 │    └─< CaseSessionEmailStates     (FK opcional por shadow column CaseSessionId)
 ├─< CaseSubmissions (SubmittedByUserId)
 ├─< CaseSubmissions (EvaluatedByUserId, opcional)
 ├─< Emails (ToUserId, delete restrict)
 ├─< Emails (FromUserId, delete restrict)
 ├─< EmailAttachmentsDownloaded
 ├─< ForensicAnalyses (RequestedByUserId)
 ├─< ForensicRequests
 ├─< Notes
 ├─< UserCases >─ Cases
 └─< UserRankHistories

Cases
 ├─< Evidences ─< ForensicAnalyses
 ├─< Suspects
 ├─< CaseProgresses
 ├─< CaseSubmissions
 └─< UserCases
```

### Observações estruturais importantes

- `CaseSessions.CaseId` é apenas `string` obrigatória; **não existe FK para `Cases`** no snapshot atual.
- `ForensicRequests.CaseId`, `Notes.CaseId`, `CaseSessionVisible*.CaseId`, `CaseSessionEmailStates.CaseId` e `EmailAttachmentsDownloaded.CaseId` também são strings sem FK para `Cases`.
- `ForensicAnalyses` se relaciona com **`Evidences`**, não com `CaseSessions`.
- `CaseSubmissions` se relaciona com **`Cases`** e usuários; **não** com `CaseSessions`.

---

## Tabelas Principais

### 1. AspNetUsers (Users)

Tabela principal de usuários do Identity, estendida pela entidade `User`.

**Colunas de domínio adicionadas ao Identity:**
- `FirstName` `nvarchar(max)` NOT NULL
- `LastName` `nvarchar(max)` NOT NULL
- `PersonalEmail` `nvarchar(max)` NOT NULL
- `Department` `nvarchar(max)` NULL
- `Position` `nvarchar(max)` NULL
- `BadgeNumber` `nvarchar(max)` NULL
- `CreatedAt` `datetime2` NOT NULL
- `LastLoginAt` `datetime2` NULL
- `EmailVerified` `bit` NOT NULL
- `EmailVerificationToken` `nvarchar(max)` NULL
- `EmailVerificationSentAt` `datetime2` NULL
- `Rank` `int` NOT NULL
- `ExperiencePoints` `int` NOT NULL
- `CasesResolved` `int` NOT NULL
- `CasesFailed` `int` NOT NULL
- `SuccessRate` `float` NOT NULL
- `AverageScore` `float` NOT NULL
- `LastPromotionDate` `datetime2` NULL
- `Specializations` `nvarchar(max)` NULL
- `CanAccessHighPriorityCases` `bit` NOT NULL

**Índices reais:**
- `EmailIndex` em `NormalizedEmail`
- `UserNameIndex` único em `NormalizedUserName` (`IS NOT NULL`)

> Não há índice/constraint único no banco para `Email` ou `BadgeNumber`.

### 2. Cases

Tabela de casos investigativos.

| Coluna | Tipo SQL | Null |
|--------|----------|------|
| `Id` | `nvarchar(450)` | Não |
| `Title` | `nvarchar(max)` | Não |
| `Description` | `nvarchar(max)` | Sim |
| `Status` | `int` | Não |
| `Priority` | `int` | Não |
| `CreatedAt` | `datetime2` | Não |
| `ClosedAt` | `datetime2` | Sim |
| `ClosedByUserId` | `nvarchar(max)` | Sim |
| `Type` | `int` | Não |
| `MinimumRankRequired` | `int` | Não |
| `Location` | `nvarchar(max)` | Sim |
| `IncidentDate` | `datetime2` | Sim |
| `BriefingText` | `nvarchar(max)` | Sim |
| `VictimInfo` | `nvarchar(max)` | Sim |
| `HasMultipleSuspects` | `bit` | Não |
| `EstimatedDifficultyLevel` | `int` | Não |
| `CorrectSuspectName` | `nvarchar(max)` | Sim |
| `CorrectEvidenceIds` | `nvarchar(max)` | Sim |
| `MaxScore` | `float` | Não |
| `CaseNotes` | `nvarchar(max)` | Sim |

**Relacionamentos:**
- 1:N com `UserCases`
- 1:N com `CaseProgresses`
- 1:N com `Evidences`
- 1:N com `Suspects`
- 1:N com `CaseSubmissions`

### 3. UserCases

Tabela de associação usuário-caso.

| Coluna | Tipo SQL | Null |
|--------|----------|------|
| `UserId` | `nvarchar(450)` | Não |
| `CaseId` | `nvarchar(450)` | Não |
| `AssignedAt` | `datetime2` | Não |
| `Role` | `int` | Não |

**Chaves/índices:**
- PK composta: (`UserId`, `CaseId`)
- Índice adicional: `CaseId`

### 4. CaseProgresses

Progresso do usuário por caso.

| Coluna | Tipo SQL | Null |
|--------|----------|------|
| `Id` | `int identity` | Não |
| `UserId` | `nvarchar(450)` | Não |
| `CaseId` | `nvarchar(450)` | Não |
| `EvidencesCollected` | `int` | Não |
| `InterviewsCompleted` | `int` | Não |
| `ReportsSubmitted` | `int` | Não |
| `LastActivity` | `datetime2` | Não |
| `CompletionPercentage` | `float` | Não |

**FKs:**
- `UserId -> AspNetUsers.Id`
- `CaseId -> Cases.Id`

### 5. CaseSessions

Sessões de investigação do jogador.

| Coluna | Tipo SQL | Null |
|--------|----------|------|
| `Id` | `int identity` | Não |
| `UserId` | `nvarchar(450)` | Não |
| `CaseId` | `nvarchar(max)` | Não |
| `SessionStart` | `datetime2` | Não |
| `SessionEnd` | `datetime2` | Sim |
| `SessionDurationMinutes` | `int` | Não |
| `GameTimeAtStart` | `nvarchar(max)` | Sim |
| `GameTimeAtEnd` | `nvarchar(max)` | Sim |
| `IsActive` | `bit` | Não |
| `Status` | `int` | Não |
| `FiredRuleIds` | `nvarchar(max)` | Sim |
| `RevealedSuspectIds` | `nvarchar(max)` | Sim |
| `EmailAttachmentOverrides` | `nvarchar(max)` | Sim |
| `SuspectStatusOverrides` | `nvarchar(max)` | Sim |
| `SuspectAlibiVerified` | `nvarchar(max)` | Sim |
| `Notifications` | `nvarchar(max)` | Sim |
| `SyntheticEmails` | `nvarchar(max)` | Sim |
| `FiredTemporalEventIds` | `nvarchar(max)` | Sim |

**FKs/índices:**
- FK: `UserId -> AspNetUsers.Id`
- Índice: `UserId`
- **Sem FK para `Cases`**

### 6. Evidences

Evidências do caso.

| Coluna | Tipo SQL | Null |
|--------|----------|------|
| `Id` | `int identity` | Não |
| `CaseId` | `nvarchar(450)` | Não |
| `Name` | `nvarchar(max)` | Não |
| `Type` | `nvarchar(max)` | Não |
| `Description` | `nvarchar(max)` | Sim |
| `FilePath` | `nvarchar(max)` | Sim |
| `CollectedByUserId` | `nvarchar(450)` | Sim |
| `CollectedAt` | `datetime2` | Não |
| `IsUnlocked` | `bit` | Não |
| `RequiresAnalysis` | `bit` | Não |
| `AnalysisStatus` | `int` | Não |
| `AnalysisResult` | `nvarchar(max)` | Sim |
| `AnalysisRequestedAt` | `datetime2` | Sim |
| `AnalysisCompletedAt` | `datetime2` | Sim |
| `DependsOnEvidenceIds` | `nvarchar(max)` | Sim |
| `Category` | `int` | Não |
| `Priority` | `int` | Não |
| `Metadata` | `nvarchar(max)` | Sim |

**FKs/índices:**
- FK: `CaseId -> Cases.Id`
- FK opcional: `CollectedByUserId -> AspNetUsers.Id`
- Índices: `CaseId`, `CollectedByUserId`

### 7. Suspects

Suspeitos do caso.

| Coluna | Tipo SQL | Null |
|--------|----------|------|
| `Id` | `int identity` | Não |
| `CaseId` | `nvarchar(450)` | Não |
| `Name` | `nvarchar(max)` | Não |
| `Alias` | `nvarchar(max)` | Sim |
| `Age` | `int` | Sim |
| `Description` | `nvarchar(max)` | Sim |
| `BackgroundInfo` | `nvarchar(max)` | Sim |
| `Motive` | `nvarchar(max)` | Sim |
| `Alibi` | `nvarchar(max)` | Sim |
| `HasAlibiVerified` | `bit` | Não |
| `Status` | `int` | Não |
| `AddedAt` | `datetime2` | Não |
| `AddedByUserId` | `nvarchar(450)` | Sim |
| `IsActualCulprit` | `bit` | Não |
| `PhotoPath` | `nvarchar(max)` | Sim |
| `ContactInfo` | `nvarchar(max)` | Sim |
| `LastKnownLocation` | `nvarchar(max)` | Sim |

**FKs/índices:**
- FK: `CaseId -> Cases.Id`
- FK opcional: `AddedByUserId -> AspNetUsers.Id`
- Índices: `CaseId`, `AddedByUserId`

### 8. ForensicAnalyses

Análises forenses vinculadas a uma evidência.

| Coluna | Tipo SQL | Null |
|--------|----------|------|
| `Id` | `int identity` | Não |
| `EvidenceId` | `int` | Não |
| `RequestedByUserId` | `nvarchar(450)` | Não |
| `AnalysisType` | `nvarchar(max)` | Não |
| `Status` | `int` | Não |
| `RequestedAt` | `datetime2` | Não |
| `CompletedAt` | `datetime2` | Sim |
| `Results` | `nvarchar(max)` | Sim |
| `TechnicianNotes` | `nvarchar(max)` | Sim |
| `ConfidenceLevel` | `float` | Sim |
| `IsMatch` | `bit` | Não |
| `ComparedAgainst` | `nvarchar(max)` | Sim |

**FKs/índices:**
- FK: `EvidenceId -> Evidences.Id`
- FK: `RequestedByUserId -> AspNetUsers.Id`
- Índices: `EvidenceId`, `RequestedByUserId`

> `AnalysisType` é string livre persistida no banco; o enum `ForensicAnalysisType` existe no código, mas não é o tipo mapeado da coluna.

### 9. ForensicRequests

Fila/registro de solicitações de perícia.

| Coluna | Tipo SQL | Null |
|--------|----------|------|
| `Id` | `int identity` | Não |
| `CaseId` | `nvarchar(max)` | Não |
| `UserId` | `nvarchar(450)` | Não |
| `InputAssetId` | `nvarchar(max)` | Não |
| `InputAssetName` | `nvarchar(max)` | Não |
| `AnalysisType` | `nvarchar(max)` | Não |
| `RequestedAt` | `datetime2` | Não |
| `EstimatedCompletionTime` | `datetime2` | Não |
| `CompletedAt` | `datetime2` | Sim |
| `Status` | `nvarchar(max)` | Não |
| `ResultDocumentId` | `nvarchar(max)` | Sim |
| `ResultEmailId` | `nvarchar(max)` | Sim |
| `Notes` | `nvarchar(max)` | Sim |

**FKs/índices:**
- FK: `UserId -> AspNetUsers.Id`
- Índice: `UserId`
- **Sem FK para `Cases`**

### 10. CaseSubmissions

Submissões/soluções do jogador para um caso.

| Coluna | Tipo SQL | Null |
|--------|----------|------|
| `Id` | `int identity` | Não |
| `CaseId` | `nvarchar(450)` | Não |
| `SubmittedByUserId` | `nvarchar(450)` | Não |
| `SuspectName` | `nvarchar(max)` | Não |
| `SuspectId` | `nvarchar(max)` | Sim |
| `KeyEvidenceDescription` | `nvarchar(max)` | Não |
| `SupportingEvidenceIds` | `nvarchar(max)` | Sim |
| `Reasoning` | `nvarchar(max)` | Não |
| `SubmittedAt` | `datetime2` | Não |
| `Status` | `int` | Não |
| `IsCorrectSuspect` | `bit` | Não |
| `IsValidEvidence` | `bit` | Não |
| `Score` | `float` | Não |
| `Feedback` | `nvarchar(max)` | Sim |
| `EvaluatedAt` | `datetime2` | Sim |
| `EvaluatedByUserId` | `nvarchar(450)` | Sim |
| `RequestPayloadJson` | `nvarchar(max)` | Sim |
| `AttemptNumber` | `int` | Não |
| `Graded` | `bit` | Não |
| `CulpritScore` | `float` | Sim |
| `EvidenceScore` | `float` | Sim |
| `AnalysisScore` | `float` | Sim |
| `QuestionsScore` | `float` | Sim |

**FKs/índices:**
- FK: `CaseId -> Cases.Id`
- FK: `SubmittedByUserId -> AspNetUsers.Id`
- FK opcional: `EvaluatedByUserId -> AspNetUsers.Id`
- Índices: `CaseId`, `SubmittedByUserId`, `EvaluatedByUserId`

### 11. Emails

Mensagens internas do sistema.

| Coluna | Tipo SQL | Null |
|--------|----------|------|
| `Id` | `int identity` | Não |
| `CaseId` | `nvarchar(450)` | Sim |
| `ToUserId` | `nvarchar(450)` | Não |
| `FromUserId` | `nvarchar(450)` | Não |
| `Subject` | `nvarchar(max)` | Não |
| `Content` | `nvarchar(max)` | Não |
| `Preview` | `nvarchar(max)` | Sim |
| `SentAt` | `datetime2` | Não |
| `IsRead` | `bit` | Não |
| `ReadAt` | `datetime2` | Sim |
| `Priority` | `int` | Não |
| `Type` | `int` | Não |
| `Attachments` | `nvarchar(max)` | Sim |
| `IsSystemGenerated` | `bit` | Não |
| `MetadataJson` | `nvarchar(max)` | Sim |

**FKs/índices:**
- FK opcional: `CaseId -> Cases.Id`
- FK: `FromUserId -> AspNetUsers.Id` (`DeleteBehavior.Restrict`)
- FK: `ToUserId -> AspNetUsers.Id` (`DeleteBehavior.Restrict`)
- Índices: `CaseId`, `FromUserId`, `ToUserId`

### 12. Notes

Notas pessoais por usuário/caso.

| Coluna | Tipo SQL | Null |
|--------|----------|------|
| `Id` | `int identity` | Não |
| `UserId` | `nvarchar(450)` | Não |
| `CaseId` | `nvarchar(100)` | Não |
| `Title` | `nvarchar(500)` | Não |
| `Content` | `nvarchar(max)` | Não |
| `CreatedAt` | `datetime2` | Não |
| `UpdatedAt` | `datetime2` | Não |

**FKs/índices:**
- FK: `UserId -> AspNetUsers.Id`
- Índice: `UserId`
- **Sem FK para `Cases`**

### 13. Tabelas de visibilidade por sessão

#### CaseSessionVisibleAssets
- Colunas: `Id` (`int identity`), `UserId` (`nvarchar(450)`), `CaseId` (`nvarchar(max)`), `AssetId` (`nvarchar(max)`), `UnlockedAt` (`datetime2`), `CaseSessionId` (`int`, NULL - shadow FK)
- FKs: `UserId -> AspNetUsers.Id`, `CaseSessionId -> CaseSessions.Id` (opcional)
- Índices: `UserId`, `CaseSessionId`

#### CaseSessionVisibleEmails
- Colunas: `Id` (`int identity`), `UserId` (`nvarchar(450)`), `CaseId` (`nvarchar(max)`), `EmailId` (`nvarchar(max)`), `UnlockedAt` (`datetime2`), `CaseSessionId` (`int`, NULL - shadow FK)
- FKs: `UserId -> AspNetUsers.Id`, `CaseSessionId -> CaseSessions.Id` (opcional)
- Índices: `UserId`, `CaseSessionId`

#### CaseSessionEmailStates
- Colunas: `Id` (`int identity`), `UserId` (`nvarchar(450)`), `CaseId` (`nvarchar(max)`), `EmailId` (`nvarchar(max)`), `ReadAt` (`datetime2`, NULL), `OpenCount` (`int`), `CaseSessionId` (`int`, NULL - shadow FK)
- FKs: `UserId -> AspNetUsers.Id`, `CaseSessionId -> CaseSessions.Id` (opcional)
- Índices: `UserId`, `CaseSessionId`

### 14. EmailAttachmentsDownloaded

Rastreia download de anexos por usuário.

- Colunas: `Id` (`int identity`), `UserId` (`nvarchar(450)`), `CaseId` (`nvarchar(max)`), `EmailId` (`nvarchar(max)`), `AssetId` (`nvarchar(max)`), `DownloadedAt` (`datetime2`)
- FK: `UserId -> AspNetUsers.Id`
- Índice: `UserId`

### 15. AuditLogs

Auditoria de ações críticas.

- Colunas: `Id` (`int identity`), `UserId` (`nvarchar(450)`), `Action` (`nvarchar(max)`), `Resource` (`nvarchar(max)`), `CaseId` (`nvarchar(max)`, NULL), `Timestamp` (`datetime2`), `Details` (`nvarchar(max)`, NULL), `Result` (`nvarchar(max)`)
- FK: `UserId -> AspNetUsers.Id`
- Índice: `UserId`

### 16. UserRankHistories

Histórico cronológico de promoções/rank.

- Colunas: `Id` (`int identity`), `UserId` (`nvarchar(450)`), `PreviousRank` (`int`, NULL), `NewRank` (`int`), `ChangedAt` (`datetime2`), `Reason` (`nvarchar(max)`, NULL)
- FK: `UserId -> AspNetUsers.Id`
- Índice composto: (`UserId`, `ChangedAt`)

---

## Tabelas de Sistema (ASP.NET Identity)

Além de `AspNetUsers`, o schema atual contém as tabelas padrão do Identity:

- `AspNetRoles`
- `AspNetRoleClaims`
- `AspNetUserClaims`
- `AspNetUserLogins`
- `AspNetUserRoles`
- `AspNetUserTokens`

**Índices relevantes:**
- `RoleNameIndex` único em `AspNetRoles.NormalizedName` (`IS NOT NULL`)
- `EmailIndex` em `AspNetUsers.NormalizedEmail`
- `UserNameIndex` único em `AspNetUsers.NormalizedUserName` (`IS NOT NULL`)

---

## Enums Persistidos

As seguintes propriedades são persistidas como `int` no banco:

- `User.Rank` (`DetectiveRank`): `Rook=0`, `Detective=1`, `Detective2=2`, `Sergeant=3`, `Lieutenant=4`, `Captain=5`, `Commander=6`
- `Case.Status` (`CaseStatus`): `Open`, `InProgress`, `Resolved`, `Closed`, `Archived`, `UnderReview`
- `Case.Priority` (`CasePriority`): `Low`, `Medium`, `High`, `Critical`, `Emergency`
- `Case.Type` (`CaseType`): `Investigation`, `ColdCase`, `EmergencyResponse`, `FollowUp`, `Review`, `Training`
- `UserCase.Role` (`UserCaseRole`): `Detective`, `Lead`, `Assistant`, `Observer`
- `CaseSession.Status` (`SessionStatus`): `Active`, `Paused`, `Completed`
- `Evidence.AnalysisStatus` (`EvidenceStatus`): `Available`, `Submitted`, `InAnalysis`, `Completed`, `Inconclusive`
- `Evidence.Category` (`EvidenceCategory`): `Physical`, `Digital`, `Document`, `Biological`, `Communication`, `Location`, `Witness`, `Technical`
- `Evidence.Priority` (`EvidencePriority`): `Low`, `Medium`, `High`, `Critical`
- `ForensicAnalysis.Status` (`ForensicAnalysisStatus`): `Requested`, `InProgress`, `Completed`, `Inconclusive`, `Failed`, `OnHold`
- `Suspect.Status` (`SuspectStatus`): `PersonOfInterest`, `Active`, `Cleared`, `Charged`, `Convicted`, `Unknown`
- `CaseSubmission.Status` (`SubmissionStatus`): `UnderReview`, `Approved`, `Rejected`, `NeedsRevision`, `Archived`
- `Email.Priority` (`EmailPriority`): `Low`, `Normal`, `High`, `Urgent`
- `Email.Type` (`EmailType`): `General`, `CaseAssignment`, `CaseBriefing`, `ForensicResults`, `EvidenceNotification`, `SystemNotification`, `CaseUpdate`, `PromotionNotice`

`ForensicAnalyses.AnalysisType` e `ForensicRequests.AnalysisType` permanecem `nvarchar(max)`.

---

## Seed Data e Inicialização

### O que o código faz hoje

- Em **SQL Server**, o app aplica `context.Database.Migrate()` no startup.
- Em **SQLite**, o app usa `context.Database.EnsureCreated()` no startup.
- `EnsureUserRolesAsync` garante os papéis `PLAYER` e `ADMIN`.
- Se não existirem usuários e **não** estiver em `--provision-admin-only`, o app cria dois usuários de teste e associa todos os casos existentes em `UserCases` / `CaseProgresses`.
- `DataSeedingService.SeedGDDDataAsync()` povoa suspeitos, evidências e e-mails GDD quando ainda não existem dados correspondentes.
- O projeto **não** usa `HasData` no `OnModelCreating`; a semeadura é feita em runtime.

### Casos cobertos pela semeadura GDD

A semeadura runtime referencia os casos:
- `CASE-2024-001`
- `CASE-2024-002`

Ela adiciona principalmente registros em:
- `Suspects`
- `Evidences`
- `Emails`

---

## Views e Consultas Comuns

### Views reais no banco

O código atual **não define views SQL** nas migrations nem no snapshot.

### Consultas compatíveis com o schema atual

#### 1. Sessões ativas por usuário
```sql
SELECT
    cs.Id,
    cs.UserId,
    cs.CaseId,
    cs.SessionStart,
    cs.SessionEnd,
    cs.Status,
    cs.IsActive
FROM CaseSessions cs
WHERE cs.IsActive = 1;
```

#### 2. Progresso por caso e usuário
```sql
SELECT
    cp.UserId,
    cp.CaseId,
    cp.EvidencesCollected,
    cp.InterviewsCompleted,
    cp.ReportsSubmitted,
    cp.CompletionPercentage,
    cp.LastActivity
FROM CaseProgresses cp;
```

#### 3. Análises forenses pendentes
```sql
SELECT
    fa.Id,
    fa.EvidenceId,
    fa.AnalysisType,
    fa.Status,
    fa.RequestedAt,
    fa.RequestedByUserId
FROM ForensicAnalyses fa
WHERE fa.Status IN (0, 1, 5); -- Requested, InProgress, OnHold
```

---

## Triggers e Procedures

O código atual **não cria triggers, procedures nem jobs SQL** nas migrations versionadas.

Comportamentos como:
- promoção de rank,
- criação de e-mails sistêmicos,
- seeding,
- processamento de perícia,

são implementados na aplicação/serviços, não no banco.

---

## Índices de Performance

### Índices realmente definidos no snapshot

| Tabela | Índice |
|--------|--------|
| `AspNetUsers` | `EmailIndex` em `NormalizedEmail` |
| `AspNetUsers` | `UserNameIndex` único em `NormalizedUserName` |
| `AspNetRoles` | `RoleNameIndex` único em `NormalizedName` |
| `AuditLogs` | `UserId` |
| `CaseProgresses` | `CaseId`, `UserId` |
| `CaseSessions` | `UserId` |
| `CaseSessionEmailStates` | `CaseSessionId`, `UserId` |
| `CaseSessionVisibleAssets` | `CaseSessionId`, `UserId` |
| `CaseSessionVisibleEmails` | `CaseSessionId`, `UserId` |
| `CaseSubmissions` | `CaseId`, `SubmittedByUserId`, `EvaluatedByUserId` |
| `Emails` | `CaseId`, `FromUserId`, `ToUserId` |
| `EmailAttachmentsDownloaded` | `UserId` |
| `Evidences` | `CaseId`, `CollectedByUserId` |
| `ForensicAnalyses` | `EvidenceId`, `RequestedByUserId` |
| `ForensicRequests` | `UserId` |
| `Notes` | `UserId` |
| `Suspects` | `CaseId`, `AddedByUserId` |
| `UserCases` | PK composta (`UserId`, `CaseId`) + índice em `CaseId` |
| `UserRankHistories` | índice composto (`UserId`, `ChangedAt`) |

### O que não existe hoje

O schema atual **não** define, via migration/snapshot:
- índices compostos `CaseSessions(UserId, Status)`
- índices `ForensicAnalyses(Status)` ou `ForensicAnalyses(AnalysisType)`
- índices `Cases(Category)` / `Cases(Difficulty)`
- índices `Evidence(Type)` / `Evidence(EvidenceId)`

---

## Backup e Restore

O repositório atual **não versiona scripts de backup/restore** neste documento nem nas migrations.

### Orientação prática por provider

- **SQL Server / Azure SQL:** usar as ferramentas nativas do ambiente (backup administrado, export, BACPAC, etc.).
- **SQLite local:** fazer cópia do arquivo `.db` usado pela connection string (por padrão local, `casezero-dev.db`, quando esse fallback estiver ativo).

---

## Manutenção e Otimização

### 1. Limpeza de dados

Não há rotinas SQL versionadas para limpeza automática de:
- sessões antigas,
- análises antigas,
- auditoria,
- notas.

Se essas políticas forem necessárias, devem ser implementadas explicitamente em migration, job externo ou serviço da aplicação.

### 2. Estatísticas da base

Exemplo simples compatível com SQL Server/SQLite:

```sql
SELECT COUNT(*) AS TotalUsers FROM AspNetUsers;
SELECT COUNT(*) AS TotalCases FROM Cases;
SELECT COUNT(*) AS TotalSessions FROM CaseSessions;
SELECT COUNT(*) AS TotalEvidence FROM Evidences;
```

---

## Constraints e Validações

### Constraints realmente existentes

- PK simples na maioria das tabelas (`Id`)
- PK composta em `UserCases(UserId, CaseId)`
- FKs definidas apenas onde descritas nas seções acima
- `DeleteBehavior.Restrict` apenas nos relacionamentos `Emails.FromUserId` e `Emails.ToUserId`

### Validações que **não** existem como constraint SQL hoje

O schema atual **não** define no banco:
- unique constraint para `AspNetUsers.Email`
- unique constraint para `AspNetUsers.BadgeNumber`
- check constraints para `Rank`, `Difficulty`, `Score` ou enums
- default constraints SQL explícitas para a maioria dos campos de domínio

> A maioria dos “defaults” atuais vem de inicializadores C# nas entidades, não de `DEFAULT` no banco.

### Campos com tamanho máximo explícito no schema

- `AspNetUsers.Email`, `NormalizedEmail`, `UserName`, `NormalizedUserName`: `nvarchar(256)`
- `AspNetRoles.Name`, `NormalizedName`: `nvarchar(256)`
- `Notes.UserId`: `nvarchar(450)`
- `Notes.CaseId`: `nvarchar(100)`
- `Notes.Title`: `nvarchar(500)`

---

## Migration Scripts

### Última migration aplicada no repositório

`20251230165708_UpdateForensicRequestAndAddEmailAttachments`

Essa migration:
- renomeia `ForensicRequests.EvidenceId` -> `InputAssetId`
- renomeia `ForensicRequests.EvidenceName` -> `InputAssetName`
- adiciona `ForensicRequests.ResultEmailId`
- cria a tabela `EmailAttachmentsDownloaded`

### Observação importante

Para o schema atual, o arquivo mais confiável é:
- `backend\CaseZeroApi\Migrations\ApplicationDbContextModelSnapshot.cs`

Ele já inclui também as tabelas mais recentes como:
- `Notes`
- `CaseSessionVisibleAssets`
- `CaseSessionVisibleEmails`
- `CaseSessionEmailStates`
- `EmailAttachmentsDownloaded`
- `AuditLogs`
- `UserRankHistories`
