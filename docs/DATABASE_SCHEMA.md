# Database Schema - CaseZero System

## Overview

O CaseZero utiliza SQLite como banco de dados com Entity Framework Core para ORM. O schema é otimizado para suportar autenticação de usuários, gerenciamento de casos investigativos e sessões de jogo.

## Database Engine

| Característica | Valor |
|----------------|-------|
| **Engine** | SQLite 3.x |
| **ORM** | Entity Framework Core 8.0 |
| **Migrations** | Code-First com EF Migrations |
| **Location** | `casezero.db` (raiz do projeto) |
| **Charset** | UTF-8 |

---

## Diagrama de Relacionamentos (ERD)

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│      Users      │    │      Cases      │    │   CaseSessions  │
├─────────────────┤    ├─────────────────┤    ├─────────────────┤
│ Id (PK)         │    │ Id (PK)         │    │ Id (PK)         │
│ UserName        │    │ CaseId (UNIQUE) │    │ SessionId       │
│ Email           │    │ Title           │    │ UserId (FK)     │
│ FirstName       │    │ Description     │    │ CaseId (FK)     │
│ LastName        │    │ Category        │    │ StartTime       │
│ Department      │    │ Difficulty      │    │ EndTime         │
│ Position        │    │ EstimatedTime   │    │ Status          │
│ BadgeNumber     │    │ CreatedDate     │    │ UnlockedContent │
│ Rank            │    │ LastModified    │    │ ProgressPercent │
│ IsApproved      │    │ IsActive        │    │ Score           │
│ CreatedAt       │    └─────────────────┘    └─────────────────┘
│ LastLoginAt     │             │                       │
└─────────────────┘             │                       │
         │                      │                       │
         │                      └───────────────────────┘
         │
         │     ┌─────────────────┐    ┌─────────────────┐
         │     │    Evidence     │    │    Suspects     │
         │     ├─────────────────┤    ├─────────────────┤
         │     │ Id (PK)         │    │ Id (PK)         │
         │     │ EvidenceId      │    │ SuspectId       │
         │     │ CaseId (FK)     │    │ CaseId (FK)     │
         │     │ Name            │    │ Name            │
         │     │ Type            │    │ Age             │
         │     │ Description     │    │ Occupation      │
         │     │ FilePath        │    │ Description     │
         │     │ UnlockReqs      │    │ FilePath        │
         │     │ Metadata        │    │ Alibi           │
         │     │ CollectedAt     │    │ Motive          │
         │     │ IsAvailable     │    │ UnlockReqs      │
         │     └─────────────────┘    └─────────────────┘
         │
         │     ┌─────────────────┐    ┌─────────────────┐
         └─────│  ForensicAnalysis│    │ CaseSubmissions │
               ├─────────────────┤    ├─────────────────┤
               │ Id (PK)         │    │ Id (PK)         │
               │ AnalysisId      │    │ SessionId (FK)  │
               │ CaseSessionId   │    │ PrimarySuspect  │
               │ EvidenceId      │    │ Motive          │
               │ AnalysisType    │    │ EvidenceChain   │
               │ Status          │    │ Timeline        │
               │ RequestedAt     │    │ Conclusion      │
               │ CompletedAt     │    │ SubmittedAt     │
               │ DurationMinutes │    │ Score           │
               │ ResultPath      │    │ Feedback        │
               │ Notes           │    └─────────────────┘
               └─────────────────┘
```

---

## Tabelas Principais

### 1. AspNetUsers (Users)

Tabela de usuários baseada no ASP.NET Identity.

```sql
CREATE TABLE "AspNetUsers" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AspNetUsers" PRIMARY KEY,
    "UserName" TEXT(256),
    "NormalizedUserName" TEXT(256),
    "Email" TEXT(256),
    "NormalizedEmail" TEXT(256),
    "EmailConfirmed" INTEGER NOT NULL,
    "PasswordHash" TEXT,
    "SecurityStamp" TEXT,
    "ConcurrencyStamp" TEXT,
    "PhoneNumber" TEXT,
    "PhoneNumberConfirmed" INTEGER NOT NULL,
    "TwoFactorEnabled" INTEGER NOT NULL,
    "LockoutEnd" TEXT,
    "LockoutEnabled" INTEGER NOT NULL,
    "AccessFailedCount" INTEGER NOT NULL,
    "FirstName" TEXT(100) NOT NULL,
    "LastName" TEXT(100) NOT NULL,
    "Department" TEXT(100),
    "Position" TEXT(100),
    "BadgeNumber" TEXT(20),
    "Rank" TEXT(50) NOT NULL DEFAULT 'Rookie',
    "IsApproved" INTEGER NOT NULL DEFAULT 0,
    "CreatedAt" TEXT NOT NULL,
    "LastLoginAt" TEXT
);
```

**Índices:**
```sql
CREATE UNIQUE INDEX "UserNameIndex" ON "AspNetUsers" ("NormalizedUserName");
CREATE INDEX "EmailIndex" ON "AspNetUsers" ("NormalizedEmail");
CREATE INDEX "IX_Users_BadgeNumber" ON "AspNetUsers" ("BadgeNumber");
```

**Campos Principais:**
- `Id`: Identificador único (GUID)
- `Email`: Email único do usuário
- `FirstName`, `LastName`: Nome completo
- `Department`: Departamento policial
- `Position`: Cargo/função
- `BadgeNumber`: Número da placa
- `Rank`: Classificação/rank do detective
- `IsApproved`: Status de aprovação pelo admin

### 2. Cases

Tabela de casos investigativos.

```sql
CREATE TABLE "Cases" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Cases" PRIMARY KEY AUTOINCREMENT,
    "CaseId" TEXT(50) NOT NULL,
    "Title" TEXT(200) NOT NULL,
    "Description" TEXT NOT NULL,
    "Category" TEXT(50) NOT NULL,
    "Difficulty" TEXT(20) NOT NULL,
    "EstimatedTimeMinutes" INTEGER NOT NULL,
    "CreatedDate" TEXT NOT NULL,
    "LastModified" TEXT,
    "IsActive" INTEGER NOT NULL DEFAULT 1
);
```

**Índices:**
```sql
CREATE UNIQUE INDEX "IX_Cases_CaseId" ON "Cases" ("CaseId");
CREATE INDEX "IX_Cases_Category" ON "Cases" ("Category");
CREATE INDEX "IX_Cases_Difficulty" ON "Cases" ("Difficulty");
```

**Campos Principais:**
- `CaseId`: Identificador único do caso (ex: "CASE-2024-001")
- `Title`: Título do caso
- `Category`: Categoria (Homicide, Theft, etc.)
- `Difficulty`: Nível de dificuldade (Easy, Medium, Hard, Expert)
- `EstimatedTimeMinutes`: Tempo estimado para resolução

### 3. CaseSessions

Tabela de sessões de investigação ativas.

```sql
CREATE TABLE "CaseSessions" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_CaseSessions" PRIMARY KEY AUTOINCREMENT,
    "SessionId" TEXT(50) NOT NULL,
    "UserId" TEXT NOT NULL,
    "CaseId" TEXT NOT NULL,
    "StartTime" TEXT NOT NULL,
    "EndTime" TEXT,
    "Status" TEXT(20) NOT NULL DEFAULT 'Active',
    "UnlockedContent" TEXT NOT NULL DEFAULT '[]',
    "ProgressPercent" INTEGER NOT NULL DEFAULT 0,
    "Score" INTEGER,
    CONSTRAINT "FK_CaseSessions_AspNetUsers_UserId" 
        FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_CaseSessions_Cases_CaseId" 
        FOREIGN KEY ("CaseId") REFERENCES "Cases" ("CaseId") ON DELETE CASCADE
);
```

**Índices:**
```sql
CREATE UNIQUE INDEX "IX_CaseSessions_SessionId" ON "CaseSessions" ("SessionId");
CREATE INDEX "IX_CaseSessions_UserId" ON "CaseSessions" ("UserId");
CREATE INDEX "IX_CaseSessions_Status" ON "CaseSessions" ("Status");
```

**Campos Principais:**
- `SessionId`: Identificador único da sessão (GUID)
- `UserId`: Referência ao usuário
- `CaseId`: Referência ao caso
- `Status`: Estado da sessão (Active, Completed, Abandoned)
- `UnlockedContent`: JSON array com conteúdo desbloqueado
- `ProgressPercent`: Percentual de progresso (0-100)

### 4. Evidence

Tabela de evidências dos casos.

```sql
CREATE TABLE "Evidence" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Evidence" PRIMARY KEY AUTOINCREMENT,
    "EvidenceId" TEXT(50) NOT NULL,
    "CaseId" TEXT NOT NULL,
    "Name" TEXT(200) NOT NULL,
    "Type" TEXT(50) NOT NULL,
    "Description" TEXT NOT NULL,
    "FilePath" TEXT(500) NOT NULL,
    "UnlockRequirements" TEXT NOT NULL DEFAULT '[]',
    "Metadata" TEXT NOT NULL DEFAULT '{}',
    "CollectedAt" TEXT NOT NULL,
    "IsAvailable" INTEGER NOT NULL DEFAULT 1,
    CONSTRAINT "FK_Evidence_Cases_CaseId" 
        FOREIGN KEY ("CaseId") REFERENCES "Cases" ("CaseId") ON DELETE CASCADE
);
```

**Índices:**
```sql
CREATE INDEX "IX_Evidence_CaseId" ON "Evidence" ("CaseId");
CREATE INDEX "IX_Evidence_Type" ON "Evidence" ("Type");
CREATE INDEX "IX_Evidence_EvidenceId" ON "Evidence" ("EvidenceId");
```

**Campos Principais:**
- `EvidenceId`: Identificador único da evidência
- `Type`: Tipo de evidência (document, photo, video, audio, digital, physical)
- `FilePath`: Caminho para o arquivo da evidência
- `UnlockRequirements`: JSON array com requisitos para desbloqueio
- `Metadata`: JSON object com metadados específicos

### 5. Suspects

Tabela de suspeitos dos casos.

```sql
CREATE TABLE "Suspects" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Suspects" PRIMARY KEY AUTOINCREMENT,
    "SuspectId" TEXT(50) NOT NULL,
    "CaseId" TEXT NOT NULL,
    "Name" TEXT(200) NOT NULL,
    "Age" INTEGER,
    "Occupation" TEXT(200),
    "Description" TEXT NOT NULL,
    "FilePath" TEXT(500),
    "Alibi" TEXT,
    "Motive" TEXT,
    "UnlockRequirements" TEXT NOT NULL DEFAULT '[]',
    CONSTRAINT "FK_Suspects_Cases_CaseId" 
        FOREIGN KEY ("CaseId") REFERENCES "Cases" ("CaseId") ON DELETE CASCADE
);
```

**Índices:**
```sql
CREATE INDEX "IX_Suspects_CaseId" ON "Suspects" ("CaseId");
CREATE INDEX "IX_Suspects_SuspectId" ON "Suspects" ("SuspectId");
```

### 6. ForensicAnalyses

Tabela de análises forenses solicitadas.

```sql
CREATE TABLE "ForensicAnalyses" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ForensicAnalyses" PRIMARY KEY AUTOINCREMENT,
    "AnalysisId" TEXT(50) NOT NULL,
    "CaseSessionId" INTEGER NOT NULL,
    "EvidenceId" TEXT(50) NOT NULL,
    "AnalysisType" TEXT(50) NOT NULL,
    "Status" TEXT(20) NOT NULL DEFAULT 'Pending',
    "RequestedAt" TEXT NOT NULL,
    "CompletedAt" TEXT,
    "DurationMinutes" INTEGER NOT NULL,
    "ResultPath" TEXT(500),
    "Notes" TEXT,
    CONSTRAINT "FK_ForensicAnalyses_CaseSessions_CaseSessionId" 
        FOREIGN KEY ("CaseSessionId") REFERENCES "CaseSessions" ("Id") ON DELETE CASCADE
);
```

**Índices:**
```sql
CREATE INDEX "IX_ForensicAnalyses_CaseSessionId" ON "ForensicAnalyses" ("CaseSessionId");
CREATE INDEX "IX_ForensicAnalyses_Status" ON "ForensicAnalyses" ("Status");
CREATE INDEX "IX_ForensicAnalyses_AnalysisType" ON "ForensicAnalyses" ("AnalysisType");
```

**Campos Principais:**
- `AnalysisType`: Tipo de análise (dna, fingerprint, digital, ballistic)
- `Status`: Estado da análise (Pending, InProgress, Completed, Failed)
- `DurationMinutes`: Tempo necessário para completar a análise
- `ResultPath`: Caminho para o arquivo de resultado

### 7. CaseSubmissions

Tabela de submissões/soluções de casos.

```sql
CREATE TABLE "CaseSubmissions" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_CaseSubmissions" PRIMARY KEY AUTOINCREMENT,
    "CaseSessionId" INTEGER NOT NULL,
    "PrimarySuspect" TEXT(50) NOT NULL,
    "Motive" TEXT NOT NULL,
    "EvidenceChain" TEXT NOT NULL,
    "Timeline" TEXT NOT NULL,
    "Conclusion" TEXT NOT NULL,
    "SubmittedAt" TEXT NOT NULL,
    "Score" INTEGER,
    "Feedback" TEXT,
    CONSTRAINT "FK_CaseSubmissions_CaseSessions_CaseSessionId" 
        FOREIGN KEY ("CaseSessionId") REFERENCES "CaseSessions" ("Id") ON DELETE CASCADE
);
```

**Índices:**
```sql
CREATE INDEX "IX_CaseSubmissions_CaseSessionId" ON "CaseSubmissions" ("CaseSessionId");
CREATE INDEX "IX_CaseSubmissions_Score" ON "CaseSubmissions" ("Score");
```

---

## Tabelas de Sistema (ASP.NET Identity)

### AspNetRoles
```sql
CREATE TABLE "AspNetRoles" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AspNetRoles" PRIMARY KEY,
    "Name" TEXT(256),
    "NormalizedName" TEXT(256),
    "ConcurrencyStamp" TEXT
);
```

### AspNetUserRoles
```sql
CREATE TABLE "AspNetUserRoles" (
    "UserId" TEXT NOT NULL,
    "RoleId" TEXT NOT NULL,
    CONSTRAINT "PK_AspNetUserRoles" PRIMARY KEY ("UserId", "RoleId"),
    CONSTRAINT "FK_AspNetUserRoles_AspNetRoles_RoleId" 
        FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_AspNetUserRoles_AspNetUsers_UserId" 
        FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);
```

### AspNetUserClaims
```sql
CREATE TABLE "AspNetUserClaims" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_AspNetUserClaims" PRIMARY KEY AUTOINCREMENT,
    "UserId" TEXT NOT NULL,
    "ClaimType" TEXT,
    "ClaimValue" TEXT,
    CONSTRAINT "FK_AspNetUserClaims_AspNetUsers_UserId" 
        FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);
```

---

## Views e Consultas Comuns

### 1. View: Active User Sessions
```sql
CREATE VIEW "ActiveUserSessions" AS
SELECT 
    cs.SessionId,
    cs.StartTime,
    u.FirstName + ' ' + u.LastName AS UserName,
    u.BadgeNumber,
    c.Title AS CaseTitle,
    cs.ProgressPercent,
    cs.Status
FROM CaseSessions cs
JOIN AspNetUsers u ON cs.UserId = u.Id
JOIN Cases c ON cs.CaseId = c.CaseId
WHERE cs.Status = 'Active';
```

### 2. View: Case Statistics
```sql
CREATE VIEW "CaseStatistics" AS
SELECT 
    c.CaseId,
    c.Title,
    COUNT(DISTINCT cs.UserId) AS TotalPlayers,
    AVG(CAST(cs.ProgressPercent AS FLOAT)) AS AvgProgress,
    AVG(CAST(cs.Score AS FLOAT)) AS AvgScore,
    COUNT(CASE WHEN cs.Status = 'Completed' THEN 1 END) AS CompletedSessions
FROM Cases c
LEFT JOIN CaseSessions cs ON c.CaseId = cs.CaseId
GROUP BY c.CaseId, c.Title;
```

---

## Consultas de Performance

### 1. Casos mais jogados
```sql
SELECT 
    c.Title,
    COUNT(*) as Sessions,
    AVG(CAST(cs.Score AS FLOAT)) as AvgScore
FROM CaseSessions cs
JOIN Cases c ON cs.CaseId = c.CaseId
WHERE cs.Status = 'Completed'
GROUP BY c.CaseId, c.Title
ORDER BY Sessions DESC;
```

### 2. Usuários mais ativos
```sql
SELECT 
    u.FirstName + ' ' + u.LastName AS UserName,
    u.BadgeNumber,
    COUNT(*) as TotalSessions,
    AVG(CAST(cs.Score AS FLOAT)) as AvgScore,
    u.Rank
FROM CaseSessions cs
JOIN AspNetUsers u ON cs.UserId = u.Id
GROUP BY u.Id, u.FirstName, u.LastName, u.BadgeNumber, u.Rank
ORDER BY TotalSessions DESC;
```

### 3. Análises forenses pendentes
```sql
SELECT 
    fa.AnalysisId,
    fa.AnalysisType,
    fa.RequestedAt,
    u.FirstName + ' ' + u.LastName AS RequestedBy,
    c.Title AS CaseTitle
FROM ForensicAnalyses fa
JOIN CaseSessions cs ON fa.CaseSessionId = cs.Id
JOIN AspNetUsers u ON cs.UserId = u.Id
JOIN Cases c ON cs.CaseId = c.CaseId
WHERE fa.Status IN ('Pending', 'InProgress')
ORDER BY fa.RequestedAt;
```

---

## Triggers e Procedures

### 1. Trigger: Update Last Login
```sql
CREATE TRIGGER UpdateLastLogin
AFTER UPDATE ON AspNetUsers
WHEN NEW.LastLoginAt != OLD.LastLoginAt
BEGIN
    UPDATE AspNetUsers 
    SET LastLoginAt = datetime('now') 
    WHERE Id = NEW.Id;
END;
```

### 2. Trigger: Auto Complete Forensic Analysis
```sql
CREATE TRIGGER AutoCompleteForensicAnalysis
AFTER UPDATE ON ForensicAnalyses
WHEN NEW.Status = 'InProgress' AND 
     datetime('now') >= datetime(NEW.RequestedAt, '+' || NEW.DurationMinutes || ' minutes')
BEGIN
    UPDATE ForensicAnalyses 
    SET Status = 'Completed',
        CompletedAt = datetime('now')
    WHERE Id = NEW.Id;
END;
```

---

## Índices de Performance

### 1. Índices Compostos
```sql
-- Session lookup por usuário e status
CREATE INDEX "IX_CaseSessions_UserId_Status" 
ON "CaseSessions" ("UserId", "Status");

-- Evidence lookup por caso e tipo
CREATE INDEX "IX_Evidence_CaseId_Type" 
ON "Evidence" ("CaseId", "Type");

-- Forensic analysis por sessão e status
CREATE INDEX "IX_ForensicAnalyses_CaseSessionId_Status" 
ON "ForensicAnalyses" ("CaseSessionId", "Status");
```

### 2. Índices de Data
```sql
-- Sessions por data de início
CREATE INDEX "IX_CaseSessions_StartTime" 
ON "CaseSessions" ("StartTime");

-- Análises por data de solicitação
CREATE INDEX "IX_ForensicAnalyses_RequestedAt" 
ON "ForensicAnalyses" ("RequestedAt");
```

---

## Backup e Restore

### Scripts de Backup
```sql
-- Backup completo
.backup casezero_backup.db

-- Export para SQL
.output casezero_dump.sql
.dump

-- Export de dados específicos
.mode csv
.output users_export.csv
SELECT * FROM AspNetUsers WHERE IsApproved = 1;
```

### Scripts de Restore
```sql
-- Restore do backup
.restore casezero_backup.db

-- Import do SQL dump
.read casezero_dump.sql
```

---

## Manutenção e Otimização

### 1. Limpeza de Dados
```sql
-- Remover sessões abandonadas antigas (> 30 dias)
DELETE FROM CaseSessions 
WHERE Status = 'Abandoned' 
AND StartTime < datetime('now', '-30 days');

-- Remover análises forenses antigas completadas
DELETE FROM ForensicAnalyses 
WHERE Status = 'Completed' 
AND CompletedAt < datetime('now', '-90 days');
```

### 2. Estatísticas da Base
```sql
-- Tamanho das tabelas
SELECT 
    name as TableName,
    COUNT(*) as RowCount
FROM sqlite_master 
WHERE type = 'table' 
AND name NOT LIKE 'sqlite_%';

-- Informações do banco
PRAGMA database_list;
PRAGMA table_info(AspNetUsers);
PRAGMA index_list(CaseSessions);
```

### 3. Otimização
```sql
-- Reindexar todas as tabelas
REINDEX;

-- Analisar estatísticas
ANALYZE;

-- Vacuum para recuperar espaço
VACUUM;
```

---

## Constraints e Validações

### 1. Constraints de Integridade
```sql
-- Email único
ALTER TABLE AspNetUsers ADD CONSTRAINT UK_Users_Email UNIQUE (Email);

-- Badge number único
ALTER TABLE AspNetUsers ADD CONSTRAINT UK_Users_BadgeNumber UNIQUE (BadgeNumber);

-- Case ID único
ALTER TABLE Cases ADD CONSTRAINT UK_Cases_CaseId UNIQUE (CaseId);
```

### 2. Check Constraints
```sql
-- Validação de rank
ALTER TABLE AspNetUsers ADD CONSTRAINT CK_Users_Rank 
CHECK (Rank IN ('Rookie', 'Detective', 'Detective First Class', 'Sergeant', 'Lieutenant'));

-- Validação de difficulty
ALTER TABLE Cases ADD CONSTRAINT CK_Cases_Difficulty 
CHECK (Difficulty IN ('Easy', 'Medium', 'Hard', 'Expert'));

-- Validação de score (0-100)
ALTER TABLE CaseSessions ADD CONSTRAINT CK_CaseSessions_Score 
CHECK (Score IS NULL OR (Score >= 0 AND Score <= 100));
```

---

## Migration Scripts

### Exemplo de Migration
```csharp
public partial class AddCaseSubmissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CaseSubmissions",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CaseSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                PrimarySuspect = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                Motive = table.Column<string>(type: "TEXT", nullable: false),
                EvidenceChain = table.Column<string>(type: "TEXT", nullable: false),
                Timeline = table.Column<string>(type: "TEXT", nullable: false),
                Conclusion = table.Column<string>(type: "TEXT", nullable: false),
                SubmittedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                Score = table.Column<int>(type: "INTEGER", nullable: true),
                Feedback = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CaseSubmissions", x => x.Id);
                table.ForeignKey(
                    name: "FK_CaseSubmissions_CaseSessions_CaseSessionId",
                    column: x => x.CaseSessionId,
                    principalTable: "CaseSessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_CaseSubmissions_CaseSessionId",
            table: "CaseSubmissions",
            column: "CaseSessionId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "CaseSubmissions");
    }
}
```