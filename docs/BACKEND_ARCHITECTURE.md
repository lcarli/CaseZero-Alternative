# Arquitetura Backend - Sistema CaseZero

## Overview

O backend do CaseZero é uma REST API robusta construída em .NET 8 Core com Entity Framework, implementando autenticação JWT, sistema de casos modulares e uma arquitetura limpa baseada em controllers, services e repositories.

## Stack Tecnológico

| Tecnologia | Versão | Propósito |
|------------|--------|-----------|
| .NET Core | 8.0 | Framework principal |
| ASP.NET Core | 8.0 | Web API framework |
| Entity Framework Core | 8.0 | ORM para acesso a dados |
| SQLite | 3.x | Banco de dados |
| ASP.NET Identity | 8.0 | Sistema de autenticação |
| JWT Bearer | 8.0 | Autenticação stateless |
| AutoMapper | 12.x | Mapeamento de DTOs |

## Arquitetura de Componentes

O sistema CaseZero possui **duas arquiteturas principais**:

### 🏛️ **CaseZeroApi - Core System**
API principal para autenticação, gestão de usuários e execução de casos:

```
backend/CaseZeroApi/
├── Controllers/           # Controllers da API
├── Models/               # Entidades do domínio
├── DTOs/                 # Data Transfer Objects
├── Data/                 # DbContext e configurações
├── Services/             # Lógica de negócio
└── Program.cs            # Configuração da aplicação
```

### 🤖 **CaseGen.Functions - AI Pipeline**
Sistema de geração automática de casos com Azure Functions e AI (**.NET 9.0**):

```
functions/CaseGen.Functions/
├── Functions/                 # Azure Functions endpoints
│   ├── GenerateCaseFunction.cs    # Main orchestration
│   ├── PlanFunction.cs            # Planning phase
│   ├── ExpandFunction.cs          # Expansion phase
│   └── RenderFunction.cs          # PDF/Image rendering
├── Services/
│   ├── CaseGeneration/           # 🆕 v2.0 - Specialized Services (1,742 lines)
│   │   ├── PlanGenerationService.cs      (282 lines) - Phase 2: Planning
│   │   ├── ExpandService.cs              (513 lines) - Phase 3: Expansion
│   │   ├── DesignService.cs              (361 lines) - Phase 4: Design
│   │   ├── DocumentGenerationService.cs  (219 lines) - Phase 5: Documents
│   │   ├── MediaGenerationService.cs     (149 lines) - Phase 5: Media
│   │   └── ValidationService.cs          (218 lines) - Phase 6: Validation
│   ├── CaseGenerationService.cs  # ~300 lines - Main coordinator
│   ├── LLMService.cs             # Azure OpenAI GPT-4o integration
│   ├── StorageService.cs         # Azure Blob Storage (Azurite local)
│   ├── PdfRenderingService.cs    # ⭐ PDF generation (~3200 lines)
│   ├── ImagesService.cs          # DALL-E 3 integration
│   ├── PrecisionEditor.cs        # Surgical JSON editing with AI
│   ├── NormalizerService.cs      # Case normalization
│   ├── RedTeamCacheService.cs    # RedTeam analysis caching
│   ├── ContextManager.cs         # Granular context storage
│   └── CaseLoggingService.cs     # Structured logging
├── Models/                       # Case Generation Models
├── Schemas/                      # JSON Schemas for AI validation
└── Program.cs                    # Dependency injection configuration
```

**🎯 Arquitetura v2.0 - Modular Services:**

O sistema foi **refatorado** (outubro 2025) de um monólito (3,938 linhas) para **6 serviços especializados** (1,742 linhas):

| Serviço | Linhas | Fase | Responsabilidade |
|---------|--------|------|------------------|
| **PlanGenerationService** | 282 | 2 | Planejamento hierárquico (Core → Suspects → Timeline → Evidence) |
| **ExpandService** | 513 | 3 | Expansão detalhada de suspeitos, evidências, timeline e relações |
| **DesignService** | 361 | 4 | Visual consistency registry + master reference images |
| **DocumentGenerationService** | 219 | 5 | Geração de conteúdo para PDFs (6 tipos de documentos) |
| **MediaGenerationService** | 149 | 5 | Geração de imagens via DALL-E 3 (CCTV, scans, fotos) |
| **ValidationService** | 218 | 6 | Normalização + RedTeam analysis + surgical fixes |

**Benefícios da Refatoração:**
- ✅ **Separation of Concerns**: Cada serviço tem responsabilidade única
- ✅ **Testabilidade**: Serviços independentes facilitam testes unitários
- ✅ **Manutenibilidade**: 56% redução de complexidade
- ✅ **Escalabilidade**: Fácil adicionar novas fases/serviços

**Core Services:**
- **PdfRenderingService**: Professional multi-page PDF templates usando QuestPDF (7 document types)
- **LLMService**: AI-powered content generation com structured prompts
- **StorageService**: Blob storage para casos, documentos e assets
- **ImagesService**: Geração de imagens via DALL-E 3 com temporal consistency
- **ContextManager**: Gerenciamento granular de contexto em Table Storage

**🔗 Documentação Detalhada:** 
- Pipeline completo: [CASE_GENERATION_PIPELINE.md](./CASE_GENERATION_PIPELINE.md)
- Templates de PDF: [PDF_DOCUMENT_TEMPLATES.md](./PDF_DOCUMENT_TEMPLATES.md)
- Arquitetura backend: [backend/README.md](../backend/README.md)

## Estrutura do CaseZeroApi (Core)

```
backend/CaseZeroApi/
├── Controllers/           # Controllers da API
├── Models/               # Entidades do domínio
├── DTOs/                 # Data Transfer Objects
├── Data/                 # DbContext e configurações
├── Services/             # Lógica de negócio
├── Migrations/           # Migrações do banco
├── appsettings.json      # Configurações
└── Program.cs            # Configuração da aplicação
```

---

## Modelos de Domínio

### 1. User (Usuário)
**Arquivo:** `Models/User.cs`

```csharp
public class User : IdentityUser
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string PersonalEmail { get; set; }
    public string? Department { get; set; }
    public string? Position { get; set; }
    public string? BadgeNumber { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool EmailVerified { get; set; } = false;
    public string? EmailVerificationToken { get; set; }
    public DateTime? EmailVerificationSentAt { get; set; }
    
    // GDD Career Progression
    public DetectiveRank Rank { get; set; } = DetectiveRank.Rook;
    public int ExperiencePoints { get; set; } = 0;
    public int CasesResolved { get; set; } = 0;
    public int CasesFailed { get; set; } = 0;
    public double SuccessRate { get; set; } = 0.0;
    // ... navigation properties
}

public enum DetectiveRank
{
    Rook = 0,           // Nível inicial
    Detective = 1,      // Detetive
    Detective2 = 2,     // Detetive Sênior
    Sergeant = 3,       // Sargento
    Lieutenant = 4,     // Tenente
    Captain = 5,        // Capitão
    Commander = 6       // Comandante
}
```

**Características Principais:**
- **PersonalEmail**: Email pessoal do usuário (usado para verificação)
- **Email**: Email institucional auto-gerado (`{nome}.{sobrenome}@fic-police.gov`)
- **EmailVerified**: Flag de verificação de email
- **EmailVerificationToken**: Token para verificação de email
- **Rank**: Sistema de progressão começando em "Rook"
- **Department**: Automaticamente definido como "ColdCase"
- **Position**: Automaticamente definida como "rook" para novos usuários
    public DateTime? LastLoginAt { get; set; }
    
    // Relacionamentos
    public ICollection<UserCase> UserCases { get; set; } = new List<UserCase>();
    public ICollection<CaseSession> CaseSessions { get; set; } = new List<CaseSession>();
}
```

### 2. Case (Caso)
**Arquivo:** `Models/Case.cs`

```csharp
public class Case
{
    public int Id { get; set; }
    public string CaseId { get; set; } = string.Empty; // CASE-2024-001
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Difficulty { get; set; } = "Medium";
    public int EstimatedTimeMinutes { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastModified { get; set; }
    public bool IsActive { get; set; } = true;
    
    // Relacionamentos
    public ICollection<Evidence> Evidences { get; set; } = new List<Evidence>();
    public ICollection<Suspect> Suspects { get; set; } = new List<Suspect>();
    public ICollection<ForensicAnalysis> ForensicAnalyses { get; set; } = new List<ForensicAnalysis>();
    public ICollection<UserCase> UserCases { get; set; } = new List<UserCase>();
    public ICollection<CaseSession> CaseSessions { get; set; } = new List<CaseSession>();
}
```

### 3. CaseSession (Sessão de Investigação)
**Arquivo:** `Models/CaseSession.cs`

```csharp
public class CaseSession
{
    public int Id { get; set; }
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public int CaseId { get; set; }
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
    public string Status { get; set; } = "Active"; // Active, Completed, Abandoned
    public string UnlockedContent { get; set; } = "[]"; // JSON array
    public int ProgressPercent { get; set; } = 0;
    public int? Score { get; set; }
    
    // Relacionamentos
    public User User { get; set; } = null!;
    public Case Case { get; set; } = null!;
    public ICollection<CaseSubmission> Submissions { get; set; } = new List<CaseSubmission>();
}
```

### 4. Evidence (Evidência)
**Arquivo:** `Models/Evidence.cs`

```csharp
public class Evidence
{
    public int Id { get; set; }
    public string EvidenceId { get; set; } = string.Empty;
    public int CaseId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // document, photo, video, etc.
    public string Description { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string UnlockRequirements { get; set; } = "[]"; // JSON array
    public string Metadata { get; set; } = "{}"; // JSON object
    public DateTime CollectedAt { get; set; } = DateTime.UtcNow;
    public bool IsAvailable { get; set; } = true;
    
    // Relacionamentos
    public Case Case { get; set; } = null!;
}
```

### 5. ForensicAnalysis (Análise Forense)
**Arquivo:** `Models/ForensicAnalysis.cs`

```csharp
public class ForensicAnalysis
{
    public int Id { get; set; }
    public string AnalysisId { get; set; } = string.Empty;
    public int CaseSessionId { get; set; }
    public string EvidenceId { get; set; } = string.Empty;
    public string AnalysisType { get; set; } = string.Empty; // dna, fingerprint, digital
    public string Status { get; set; } = "Pending"; // Pending, InProgress, Completed
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public int DurationMinutes { get; set; }
    public string? ResultPath { get; set; }
    public string? Notes { get; set; }
    
    // Relacionamentos
    public CaseSession CaseSession { get; set; } = null!;
}
```

---

## Data Transfer Objects (DTOs)

### Authentication DTOs
**Arquivo:** `DTOs/AuthDtos.cs`

```csharp
// Login Request
public class LoginRequestDto
{
    [Required]
    [EmailAddress]
    public required string Email { get; set; }
    
    [Required]
    public required string Password { get; set; }
}

// Register Request - Simplificado
public class RegisterRequestDto
{
    [Required]
    public required string FirstName { get; set; }
    
    [Required]
    public required string LastName { get; set; }
    
    [Required]
    [EmailAddress]
    public required string PersonalEmail { get; set; }
    
    [Required]
    [MinLength(8)]
    public required string Password { get; set; }
}

// Email Verification
public class VerifyEmailRequestDto
{
    [Required]
    public required string Token { get; set; }
}

// User Response DTO
public class UserDto
{
    public required string Id { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Email { get; set; }
    public required string PersonalEmail { get; set; }
    public string? Department { get; set; }
    public string? Position { get; set; }
    public string? BadgeNumber { get; set; }
    public bool EmailVerified { get; set; }
}
```

// Auth Response
public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public UserDto User { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
}
```

### Case DTOs
**Arquivo:** `DTOs/CaseDtos.cs`

```csharp
// Case Overview
public class CaseOverviewDto
{
    public string CaseId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public int EstimatedTimeMinutes { get; set; }
    public DateTime CreatedDate { get; set; }
}

// Case Details
public class CaseDetailsDto
{
    public string CaseId { get; set; } = string.Empty;
    public CaseMetadataDto Metadata { get; set; } = null!;
    public List<EvidenceDto> Evidences { get; set; } = new();
    public List<SuspectDto> Suspects { get; set; } = new();
    public List<ForensicAnalysisDto> ForensicAnalyses { get; set; } = new();
    public List<TimelineEventDto> Timeline { get; set; } = new();
    public CaseSolutionDto? Solution { get; set; }
}

// Case Submission
public class CaseSubmissionDto
{
    public string PrimarySuspect { get; set; } = string.Empty;
    public string Motive { get; set; } = string.Empty;
    public List<string> EvidenceChain { get; set; } = new();
    public List<TimelineEventDto> Timeline { get; set; } = new();
    public string Conclusion { get; set; } = string.Empty;
}
```

---

## Controladores (Controllers)

### 1. AuthController
**Arquivo:** `Controllers/AuthController.cs`

**Responsabilidades:**
- Autenticação de usuários
- Registro de novos usuários
- Logout e invalidação de tokens
- Renovação de tokens

**Principais Endpoints:**
```csharp
[HttpPost("register")]
public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)

[HttpPost("login")]
public async Task<IActionResult> Login([FromBody] LoginRequestDto request)

[HttpPost("logout")]
[Authorize]
public async Task<IActionResult> Logout()

[HttpPost("refresh")]
public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto request)
```

### 2. CaseObjectController
**Arquivo:** `Controllers/CaseObjectController.cs`

**Responsabilidades:**
- Gerenciamento do sistema de casos modulares
- Carregamento de casos do sistema de arquivos
- Validação de estrutura de casos
- Listagem de casos disponíveis

**Principais Endpoints:**
```csharp
[HttpGet]
public async Task<IActionResult> GetAvailableCases()

[HttpGet("{caseId}")]
public async Task<IActionResult> GetCaseDetails(string caseId)

[HttpGet("{caseId}/validate")]
public async Task<IActionResult> ValidateCase(string caseId)

[HttpPost("{caseId}/load")]
public async Task<IActionResult> LoadCaseForUser(string caseId)
```

### 3. CaseSessionController
**Arquivo:** `Controllers/CaseSessionController.cs`

**Responsabilidades:**
- Gerenciamento de sessões de investigação
- Controle de progresso
- Estado da investigação
- Tempo de jogo

**Principais Endpoints:**
```csharp
[HttpPost("start")]
public async Task<IActionResult> StartCaseSession([FromBody] StartSessionDto request)

[HttpGet("{sessionId}")]
public async Task<IActionResult> GetSessionStatus(string sessionId)

[HttpPut("{sessionId}/progress")]
public async Task<IActionResult> UpdateProgress(string sessionId, [FromBody] ProgressUpdateDto update)

[HttpPost("{sessionId}/end")]
public async Task<IActionResult> EndSession(string sessionId)
```

### 4. EvidenceController
**Arquivo:** `Controllers/EvidenceController.cs`

**Responsabilidades:**
- Acesso a evidências
- Download de arquivos
- Controle de desbloqueio
- Metadados de evidências

### 5. ForensicController
**Arquivo:** `Controllers/ForensicController.cs`

**Responsabilidades:**
- Solicitação de análises forenses
- Acompanhamento de progresso
- Entrega de resultados
- Simulação de tempo de processamento

---

## Serviços (Business Logic)

### 1. JwtService
**Arquivo:** `Services/JwtService.cs`

```csharp
public interface IJwtService
{
    string GenerateToken(User user);
    ClaimsPrincipal? ValidateToken(string token);
    DateTime GetTokenExpiry(string token);
}

public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;
    
    public string GenerateToken(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_configuration["JwtSettings:SecretKey"]);
        
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
                new Claim("badge", user.BadgeNumber),
                new Claim("rank", user.Rank)
            }),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key), 
                SecurityAlgorithms.HmacSha256Signature)
        };
        
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
```

### 2. CaseObjectService
**Arquivo:** `Services/CaseObjectService.cs`

```csharp
public interface ICaseObjectService
{
    Task<List<CaseOverviewDto>> GetAvailableCasesAsync();
    Task<CaseDetailsDto?> GetCaseDetailsAsync(string caseId);
    Task<CaseValidationResult> ValidateCaseAsync(string caseId);
    Task<bool> CaseExistsAsync(string caseId);
}

public class CaseObjectService : ICaseObjectService
{
    private readonly string _casesPath;
    private readonly ILogger<CaseObjectService> _logger;
    
    public async Task<CaseDetailsDto?> GetCaseDetailsAsync(string caseId)
    {
        var casePath = Path.Combine(_casesPath, caseId);
        if (!Directory.Exists(casePath))
            return null;
            
        var caseJsonPath = Path.Combine(casePath, "case.json");
        if (!File.Exists(caseJsonPath))
            return null;
            
        var jsonContent = await File.ReadAllTextAsync(caseJsonPath);
        var caseData = JsonSerializer.Deserialize<CaseDetailsDto>(jsonContent);
        
        return caseData;
    }
    
    public async Task<CaseValidationResult> ValidateCaseAsync(string caseId)
    {
        var result = new CaseValidationResult { CaseId = caseId };
        
        // Validate case structure
        var casePath = Path.Combine(_casesPath, caseId);
        if (!Directory.Exists(casePath))
        {
            result.Errors.Add($"Case directory not found: {casePath}");
            return result;
        }
        
        // Validate case.json
        var caseJsonPath = Path.Combine(casePath, "case.json");
        if (!File.Exists(caseJsonPath))
        {
            result.Errors.Add("case.json file not found");
            return result;
        }
        
        // Validate file references
        var caseData = await GetCaseDetailsAsync(caseId);
        if (caseData != null)
        {
            await ValidateFileReferences(casePath, caseData, result);
        }
        
        result.IsValid = result.Errors.Count == 0;
        return result;
    }
}
```

### 3. CaseSessionService
**Arquivo:** `Services/CaseSessionService.cs`

```csharp
public interface ICaseSessionService
{
    Task<CaseSession> StartSessionAsync(string userId, string caseId);
    Task<CaseSession?> GetSessionAsync(string sessionId);
    Task<CaseSession> UpdateProgressAsync(string sessionId, string[] unlockedContent);
    Task<CaseSession> EndSessionAsync(string sessionId, int? score = null);
}

public class CaseSessionService : ICaseSessionService
{
    private readonly ApplicationDbContext _context;
    private readonly ICaseObjectService _caseObjectService;
    
    public async Task<CaseSession> StartSessionAsync(string userId, string caseId)
    {
        // Verify case exists
        var caseExists = await _caseObjectService.CaseExistsAsync(caseId);
        if (!caseExists)
            throw new ArgumentException($"Case {caseId} not found");
            
        // Create new session
        var session = new CaseSession
        {
            SessionId = Guid.NewGuid().ToString(),
            UserId = userId,
            CaseId = caseId,
            Status = "Active",
            UnlockedContent = "[]",
            StartTime = DateTime.UtcNow
        };
        
        _context.CaseSessions.Add(session);
        await _context.SaveChangesAsync();
        
        return session;
    }
}
```

---

## Data Layer (Entity Framework)

### ApplicationDbContext
**Arquivo:** `Data/ApplicationDbContext.cs`

```csharp
public class ApplicationDbContext : IdentityDbContext<User>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
    
    public DbSet<Case> Cases { get; set; }
    public DbSet<Evidence> Evidences { get; set; }
    public DbSet<Suspect> Suspects { get; set; }
    public DbSet<ForensicAnalysis> ForensicAnalyses { get; set; }
    public DbSet<CaseSession> CaseSessions { get; set; }
    public DbSet<CaseSubmission> CaseSubmissions { get; set; }
    public DbSet<UserCase> UserCases { get; set; }
    public DbSet<Email> Emails { get; set; }
    
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        
        // User configuration
        builder.Entity<User>(entity =>
        {
            entity.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.LastName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Department).HasMaxLength(100);
            entity.Property(e => e.Position).HasMaxLength(100);
            entity.Property(e => e.BadgeNumber).HasMaxLength(20);
            entity.Property(e => e.Rank).HasMaxLength(50).HasDefaultValue("Rookie");
        });
        
        // Case configuration
        builder.Entity<Case>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CaseId).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.CaseId).IsUnique();
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(50);
            entity.Property(e => e.Difficulty).HasMaxLength(20);
        });
        
        // CaseSession configuration
        builder.Entity<CaseSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SessionId).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.SessionId).IsUnique();
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.UnlockedContent).HasColumnType("TEXT");
            
            entity.HasOne(e => e.User)
                  .WithMany(u => u.CaseSessions)
                  .HasForeignKey(e => e.UserId);
                  
            entity.HasOne(e => e.Case)
                  .WithMany(c => c.CaseSessions)
                  .HasForeignKey(e => e.CaseId);
        });
        
        // Evidence configuration
        builder.Entity<Evidence>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EvidenceId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Type).HasMaxLength(50);
            entity.Property(e => e.FilePath).HasMaxLength(500);
            entity.Property(e => e.UnlockRequirements).HasColumnType("TEXT");
            entity.Property(e => e.Metadata).HasColumnType("TEXT");
            
            entity.HasOne(e => e.Case)
                  .WithMany(c => c.Evidences)
                  .HasForeignKey(e => e.CaseId);
        });
        
        // Seed data
        SeedData(builder);
    }
    
    private void SeedData(ModelBuilder builder)
    {
        // Seed default admin user
        var adminUser = new User
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "john.doe@fic-police.gov",
            NormalizedUserName = "JOHN.DOE@FIC-POLICE.GOV",
            Email = "john.doe@fic-police.gov",
            NormalizedEmail = "JOHN.DOE@FIC-POLICE.GOV",
            PersonalEmail = "john.doe.personal@example.com",
            FirstName = "John",
            LastName = "Doe",
            Department = "ColdCase",
            Position = "rook",
            BadgeNumber = "4729",
            Rank = DetectiveRank.Rook,
            EmailVerified = true,
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        
        // Hash password
        var hasher = new PasswordHasher<User>();
        adminUser.PasswordHash = hasher.HashPassword(adminUser, "Password123!");
        
        builder.Entity<User>().HasData(adminUser);
    }
}
```

---

## Configuração da Aplicação

### Program.cs
**Principais Configurações:**

```csharp
var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity
builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 8;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins("http://localhost:5173")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

// Services
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<ICaseObjectService, CaseObjectService>();
builder.Services.AddScoped<ICaseSessionService, CaseSessionService>();

// AutoMapper
builder.Services.AddAutoMapper(typeof(Program));

var app = builder.Build();

// Middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseRouting();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Database initialization
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.EnsureCreated();
}

app.Run();
```

### appsettings.json
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=casezero.db"
  },
  "JwtSettings": {
    "SecretKey": "YourSuperSecretKeyThatShouldBeAtLeast32Characters!",
    "Issuer": "CaseZeroApi",
    "Audience": "CaseZeroFrontend",
    "ExpiryDays": 7
  },
  "CaseSettings": {
    "CasesPath": "../../cases",
    "MaxFileSize": 10485760,
    "AllowedFileTypes": [".pdf", ".jpg", ".jpeg", ".png", ".mp4", ".mp3", ".txt", ".doc", ".docx"]
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

---

## Sistema de Migrações

### Comandos Entity Framework
```bash
# Criar nova migração
dotnet ef migrations add InitialCreate

# Aplicar migrações
dotnet ef database update

# Reverter migração
dotnet ef database update PreviousMigrationName

# Gerar script SQL
dotnet ef migrations script
```

### Exemplo de Migração
```csharp
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Cases",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CaseId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "TEXT", nullable: false),
                Category = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                Difficulty = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                EstimatedTimeMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                LastModified = table.Column<DateTime>(type: "TEXT", nullable: true),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Cases", x => x.Id);
            });
    }
}
```

---

## Middleware Personalizado

### Exception Handling Middleware
```csharp
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        
        var response = new
        {
            error = new
            {
                message = exception.Message,
                details = exception.InnerException?.Message
            }
        };

        switch (exception)
        {
            case UnauthorizedAccessException:
                context.Response.StatusCode = 401;
                break;
            case ArgumentException:
                context.Response.StatusCode = 400;
                break;
            case FileNotFoundException:
                context.Response.StatusCode = 404;
                break;
            default:
                context.Response.StatusCode = 500;
                break;
        }

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
```

---

## Segurança

### Configurações de Segurança

1. **Password Policy**
```csharp
options.Password.RequireDigit = true;
options.Password.RequireLowercase = true;
options.Password.RequireNonAlphanumeric = false;
options.Password.RequireUppercase = true;
options.Password.RequiredLength = 8;
```

2. **JWT Configuration**
```csharp
options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuerSigningKey = true,
    ValidateIssuer = true,
    ValidateAudience = true,
    ValidateLifetime = true,
    ClockSkew = TimeSpan.Zero
};
```

3. **CORS Policy**
```csharp
policy.WithOrigins("http://localhost:5173")
      .AllowAnyHeader()
      .AllowAnyMethod()
      .AllowCredentials();
```

---

## Performance e Otimização

### Database Optimizations
```csharp
// Eager Loading
var casesWithEvidences = await _context.Cases
    .Include(c => c.Evidences)
    .Include(c => c.Suspects)
    .ToListAsync();

// Projection
var caseOverviews = await _context.Cases
    .Select(c => new CaseOverviewDto
    {
        CaseId = c.CaseId,
        Title = c.Title,
        Category = c.Category
    })
    .ToListAsync();

// Pagination
var pagedCases = await _context.Cases
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync();
```

### Caching Strategy
```csharp
// Memory Cache para casos frequentemente acessados
builder.Services.AddMemoryCache();

public class CachedCaseObjectService : ICaseObjectService
{
    private readonly ICaseObjectService _baseService;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(30);

    public async Task<CaseDetailsDto?> GetCaseDetailsAsync(string caseId)
    {
        var cacheKey = $"case_details_{caseId}";
        
        if (_cache.TryGetValue(cacheKey, out CaseDetailsDto cachedCase))
        {
            return cachedCase;
        }

        var caseDetails = await _baseService.GetCaseDetailsAsync(caseId);
        if (caseDetails != null)
        {
            _cache.Set(cacheKey, caseDetails, _cacheExpiry);
        }

        return caseDetails;
    }
}
```

---

## Logging e Monitoramento

### Structured Logging
```csharp
// Program.cs
builder.Services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.AddDebug();
    if (environment.IsProduction())
    {
        builder.AddApplicationInsights();
    }
});

// Controller example
[HttpPost("login")]
public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
{
    _logger.LogInformation("Login attempt for user {Email}", request.Email);
    
    try
    {
        var result = await _authService.LoginAsync(request);
        _logger.LogInformation("Successful login for user {Email}", request.Email);
        return Ok(result);
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed login attempt for user {Email}", request.Email);
        return Unauthorized("Invalid credentials");
    }
}
```

---

## Testing

### Unit Testing Structure
```csharp
[TestClass]
public class CaseObjectServiceTests
{
    private Mock<ILogger<CaseObjectService>> _mockLogger;
    private CaseObjectService _service;

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<CaseObjectService>>();
        _service = new CaseObjectService("./test-cases", _mockLogger.Object);
    }

    [TestMethod]
    public async Task GetCaseDetailsAsync_ValidCaseId_ReturnsCase()
    {
        // Arrange
        var caseId = "TEST-CASE-001";

        // Act
        var result = await _service.GetCaseDetailsAsync(caseId);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(caseId, result.CaseId);
    }
}
```

---

## CaseGen.Functions - Specialized Services (v2.0)

### Arquitetura Modular

A partir de **outubro 2025**, o sistema CaseGen.Functions foi refatorado de um monólito (`CaseGenerationService.cs` com 3,938 linhas) para uma arquitetura modular com **6 serviços especializados** totalizando 1,742 linhas organizadas.

### 1. PlanGenerationService (282 linhas)

**Arquivo:** `functions/CaseGen.Functions/Services/CaseGeneration/PlanGenerationService.cs`

**Fase:** 2 - Planning

**Responsabilidades:**
- Planejamento hierárquico da estrutura do caso
- Geração de plano core (título, overview, learning objectives)
- Criação de lista inicial de suspeitos
- Planejamento de timeline cronológica
- Definição de plano de evidências + Golden Truth

**Métodos Principais:**
```csharp
Task<string> PlanCoreAsync(CaseGenerationRequest request, string caseId, CancellationToken cancellationToken)
Task<string> PlanSuspectsAsync(string caseId, CancellationToken cancellationToken)
Task<string> PlanTimelineAsync(string caseId, CancellationToken cancellationToken)
Task<string> PlanEvidenceAsync(string caseId, CancellationToken cancellationToken)
```

**Dependências:**
- `ILLMService` - Geração de conteúdo via GPT-4o
- `IJsonSchemaProvider` - Validação com schemas JSON
- `IContextManager` - Armazenamento de contexto granular
- `ILogger<PlanGenerationService>`

### 2. ExpandService (513 linhas)

**Arquivo:** `functions/CaseGen.Functions/Services/CaseGeneration/ExpandService.cs`

**Fase:** 3 - Expansion

**Responsabilidades:**
- Expansão detalhada de perfis de suspeitos
- Detalhamento de evidências com chain of custody
- Expansão da timeline com eventos específicos
- Síntese de relações entre elementos do caso

**Métodos Principais:**
```csharp
Task<string> ExpandSuspectAsync(string suspectId, string caseId, CancellationToken cancellationToken)
Task<string> ExpandEvidenceAsync(string evidenceId, string caseId, CancellationToken cancellationToken)
Task<string> ExpandTimelineAsync(string caseId, CancellationToken cancellationToken)
Task<string> SynthesizeRelationsAsync(string caseId, CancellationToken cancellationToken)
```

**Características:**
- Carregamento automático de contexto via `ContextManager`
- Referências cruzadas entre suspeitos/evidências/eventos
- Manutenção de consistência narrativa

### 3. DesignService (361 linhas)

**Arquivo:** `functions/CaseGen.Functions/Services/CaseGeneration/DesignService.cs`

**Fase:** 4 - Design

**Responsabilidades:**
- Criação de Visual Consistency Registry
- Geração de master reference images (suspeitos, evidências, locais)
- Garantia de consistência visual entre documentos

**Métodos Principais:**
```csharp
Task<string> DesignVisualConsistencyRegistryAsync(string caseId, CancellationToken cancellationToken)
Task GenerateMasterReferencesAsync(string caseId, CancellationToken cancellationToken)
```

**Integrações:**
- `IImagesService` - Geração DALL-E 3
- `ISchemaValidationService` - Validação de registry
- `IStorageService` - Armazenamento de imagens

### 4. DocumentGenerationService (219 linhas)

**Arquivo:** `functions/CaseGen.Functions/Services/CaseGeneration/DocumentGenerationService.cs`

**Fase:** 5 - Document Generation

**Responsabilidades:**
- Geração de conteúdo para documentos PDF
- Diretrizes específicas por tipo de documento
- Diretrizes específicas por nível de dificuldade

**Métodos Principais:**
```csharp
Task<DocumentSpec> GenerateDocumentFromSpecAsync(
    DocumentSpec spec, 
    string caseId, 
    string timezone, 
    string? visualRegistry, 
    string? goldenTruth, 
    string? difficulty, 
    CancellationToken cancellationToken)

Task RenderDocumentFromJsonAsync(
    string documentJson, 
    string caseId, 
    string outputPath, 
    CancellationToken cancellationToken)
```

**Tipos de Documentos Suportados:**
- `police_report` - Relatórios policiais
- `interview` - Transcrições de entrevistas
- `memo_admin` - Memorandos administrativos
- `forensics_report` - Laudos forenses
- `evidence_log` - Logs de evidências
- `witness_statement` - Depoimentos de testemunhas

**Integrações:**
- `IPdfRenderingService` - Renderização PDF final

### 5. MediaGenerationService (149 linhas)

**Arquivo:** `functions/CaseGen.Functions/Services/CaseGeneration/MediaGenerationService.cs`

**Fase:** 5 - Media Generation

**Responsabilidades:**
- Geração de especificações para imagens
- Temporal consistency validation (timestamps)
- CCTV frames com overlays de timestamp
- Fotografias forenses e scans de documentos

**Métodos Principais:**
```csharp
Task<MediaSpec> GenerateMediaFromSpecAsync(
    MediaSpec spec, 
    string caseId, 
    string timezone, 
    string? visualRegistry, 
    string? goldenTruth, 
    string? difficulty, 
    CancellationToken cancellationToken)

Task RenderMediaFromJsonAsync(
    MediaSpec spec, 
    string caseId, 
    CancellationToken cancellationToken)
```

**Tipos de Media Suportados:**
- CCTV frames (com timestamp overlays)
- Document scans
- Scene photography
- Forensic photography

**Características:**
- Validação de consistência temporal (timezone enforcement)
- Referência ao visual registry para consistência
- Geração via DALL-E 3

### 6. ValidationService (218 linhas)

**Arquivo:** `functions/CaseGen.Functions/Services/CaseGeneration/ValidationService.cs`

**Fase:** 6 - Validation

**Responsabilidades:**
- Normalização determinística de casos
- Validação de regras de qualidade
- RedTeam global analysis (análise macro)
- Correções cirúrgicas via PrecisionEditor

**Métodos Principais:**
```csharp
Task<NormalizationResult> NormalizeCaseDeterministicAsync(
    NormalizationInput input, 
    CancellationToken cancellationToken)

Task<string> ValidateRulesAsync(
    string normalizedJson, 
    string caseId, 
    CancellationToken cancellationToken)

Task<string> RedTeamGlobalAnalysisAsync(
    string validatedJson, 
    string caseId, 
    CancellationToken cancellationToken)

Task<string> FixCaseAsync(
    StructuredRedTeamAnalysis analysis, 
    string currentJson, 
    string caseId, 
    int iterationNumber, 
    CancellationToken cancellationToken)
```

**Características:**
- **Temporal consistency checks**: Validação de timestamps e timezones
- **RedTeam caching**: Cache de análises para economia de tokens
- **Surgical fixes**: Correções precisas via PrecisionEditor
- **Normalization**: Formatação consistente de todos os documentos

**Integrações:**
- `INormalizerService` - Normalização de formato
- `IRedTeamCacheService` - Caching de análises
- `IPrecisionEditor` - Edições cirúrgicas JSON

### Main Coordinator

**Arquivo:** `functions/CaseGen.Functions/Services/CaseGenerationService.cs` (~300 linhas)

Após a refatoração, o `CaseGenerationService` original tornou-se um **coordinator** que:
- Injeta os 6 serviços especializados via constructor DI
- Delega operações para os serviços apropriados
- Mantém interface pública para compatibilidade
- Orquestra o fluxo entre fases

**Dependency Injection (Program.cs):**
```csharp
builder.Services
    .AddScoped<PlanGenerationService>()
    .AddScoped<ExpandService>()
    .AddScoped<DesignService>()
    .AddScoped<DocumentGenerationService>()
    .AddScoped<MediaGenerationService>()
    .AddScoped<ValidationService>()
    .AddScoped<ICaseGenerationService, CaseGenerationService>();
```

### Métricas da Refatoração

| Métrica | Antes (v1.0) | Depois (v2.0) | Melhoria |
|---------|--------------|---------------|----------|
| **Linhas em CaseGenerationService** | 3,938 | ~300 | 92% ↓ |
| **Serviços especializados** | 0 | 6 | +6 |
| **Linhas organizadas** | 0 | 1,742 | - |
| **Complexidade** | Alta | Baixa | 56% ↓ |
| **Separation of Concerns** | ❌ | ✅ | - |
| **Testabilidade** | Baixa | Alta | ↑ |

---

## CaseGen.Functions - PDF Rendering Service

### PdfRenderingService Overview

O **PdfRenderingService** é o componente responsável por gerar PDFs profissionais de documentos policiais usando a biblioteca **QuestPDF 2025.7.1**.

**Arquivo:** `functions/CaseGen.Functions/Services/PdfRenderingService.cs` (~3200 lines)

### Implemented Templates (7 types)

| Document Type | Type ID | Pages | Key Features |
|--------------|---------|-------|--------------|
| **Police Report** | `police_report` | 1-N | Logo, status badges, checkboxes, officer signature |
| **Suspect/Witness Profile** | `suspect_profile`, `witness_profile` | 3 | Mugshot, criminal history, risk assessment, notes |
| **Evidence Log** | `evidence_log` | 2+ | Cover page, chain of custody, triple signatures |
| **Forensics Report** | `forensics_report`, `lab_report` | 2+ | Lab certification, analysis badges, dual signatures |
| **Interview Transcript** | `interview` | 2+ | Miranda rights, Q&A format, triple signatures |
| **Memo** | `memo`, `memo_admin` | 2+ | Routing slip, priority checkboxes, triple acknowledgment |
| **Witness Statement** | `witness_statement` | 2+ | Witness info, statement body, notary certification |

### Architecture Pattern

Each document type follows a consistent multi-page pattern:

1. **Cover Page**: Large logo (100-120px), title, case info, document-specific metadata
2. **Content Pages**: Small logo header (50px), structured content sections
3. **Signature Section**: Appropriate signatures based on document type (single, dual, or triple)

### Key Methods

```csharp
public class PdfRenderingService
{
    // Main entry point - routes to appropriate template
    public byte[] GenerateRealisticPdf(string title, string content, string type, string caseId, string docId)
    
    // Multi-page generators
    private byte[] GenerateMultiPageSuspectProfile(...)
    private byte[] GenerateMultiPageEvidenceLog(...)
    private byte[] GenerateMultiPageForensicsReport(...)
    private byte[] GenerateMultiPageInterview(...)
    private byte[] GenerateMultiPageMemo(...)
    private byte[] GenerateMultiPageWitnessStatement(...)
    
    // Content renderers
    private void RenderPoliceReport(...)
    private void RenderEvidenceLogContent(...)
    private void RenderForensicsReportContent(...)
    private void RenderInterviewContent(...)
    private void RenderMemoContent(...)
    private void RenderWitnessStatementContent(...)
    
    // Common components
    private void BuildLetterhead(IContainer c, string docType, string title, string caseId, string docId)
    private void AddWatermark(IContainer e, string classification)
}
```

### Assets Management

Logo and visual assets are copied to output directory via `.csproj` configuration:

```xml
<ItemGroup>
  <None Include="..\..\assets\**\*">
    <Link>assets\%(RecursiveDir)%(Filename)%(Extension)</Link>
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

**Logo file:** `assets/LogoMetroPolice_transparent.png`

### Document Type Routing

The service automatically detects document type and routes to the correct template:

```csharp
if (documentType.ToLower() == "suspect_profile" || documentType.ToLower() == "witness_profile")
    return GenerateMultiPageSuspectProfile(...);
    
if (documentType.ToLower() == "evidence_log" || documentType.ToLower() == "evidence_catalog")
    return GenerateMultiPageEvidenceLog(...);

// ... etc for all 7 types
```

### Testing Endpoint

**Function:** `TestPdfFunction.cs`  
**Endpoint:** `GET /api/test/pdf/real?caseId={caseId}&docId={docId}`

Loads real case data from Azure Blob Storage and generates PDF for testing.

**Storage Structure:**
```
bundles/
  └── {caseId}/
      └── documents/
          └── {docId}.json
```

**🔗 Documentação Detalhada:** [PDF_DOCUMENT_TEMPLATES.md](./PDF_DOCUMENT_TEMPLATES.md)

---

## Deployment

### Production Configuration
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=CaseZero;Trusted_Connection=true;"
  },
  "JwtSettings": {
    "SecretKey": "${JWT_SECRET_KEY}",
    "Issuer": "CaseZeroApi",
    "Audience": "CaseZeroFrontend"
  }
}
```

### Docker Configuration
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["CaseZeroApi.csproj", "."]
RUN dotnet restore "./CaseZeroApi.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "CaseZeroApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CaseZeroApi.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CaseZeroApi.dll"]
```

---

## 📝 Histórico de Versões

### v2.0 (Outubro 2025) - Refatoração Modular

**🎯 Objetivo:** Refatorar monólito CaseGenerationService em serviços especializados

**✅ Implementado:**
- **6 Serviços Especializados** criados:
  1. PlanGenerationService (282 linhas) - Planning hierárquico
  2. ExpandService (513 linhas) - Expansion de conteúdo
  3. DesignService (361 linhas) - Design visual
  4. DocumentGenerationService (219 linhas) - Geração de PDFs
  5. MediaGenerationService (149 linhas) - Geração de imagens
  6. ValidationService (218 linhas) - Validação e RedTeam

- **Main Coordinator** refatorado:
  - CaseGenerationService reduzido de 3,938 → ~300 linhas
  - Injeção dos 6 serviços via DI
  - Delegação de operações para serviços especializados

- **Dependency Injection** atualizado:
  - Registro de 6 novos serviços no Program.cs
  - Scoped lifetime para todos os serviços

**📊 Métricas:**
- 92% redução no arquivo principal
- 56% redução de complexidade geral
- 1,742 linhas organizadas em serviços focados
- 0 erros de compilação
- Separation of Concerns implementado

**🔗 Documentação Atualizada:**
- [backend/README.md](../backend/README.md) - Documentação completa
- [PDF_DOCUMENT_TEMPLATES.md](./PDF_DOCUMENT_TEMPLATES.md) - Templates PDF
- [tests/http-requests/README.md](../tests/http-requests/README.md) - Testes HTTP

### v1.0 (Agosto 2025) - Versão Inicial

**✅ Implementado:**
- CaseZeroApi - Web API REST com autenticação JWT
- CaseGen.Functions - Pipeline de geração com Azure Functions
- PdfRenderingService - 7 templates PDF profissionais
- Sistema de geração com 6 fases (Seed → Plan → Expand → Design → Generate → Validate)
- Integração com Azure OpenAI (GPT-4o) e DALL-E 3
- Storage em Azure Blob + Table Storage
- Application Insights para monitoramento

**📚 Documentação:**
- [CASE_GENERATION_PIPELINE.md](./CASE_GENERATION_PIPELINE.md)
- [OBJETO_CASO.md](./OBJETO_CASO.md)

---

## 🔗 Links Relacionados

- **📖 [Pipeline de Geração](./CASE_GENERATION_PIPELINE.md)** - Fluxo completo de geração de casos
- **📄 [Templates PDF](./PDF_DOCUMENT_TEMPLATES.md)** - Documentação dos 7 templates implementados
- **🏗️ [Backend README](../backend/README.md)** - Guia completo do backend
- **🧪 [Testes HTTP](../tests/http-requests/README.md)** - Coleção de testes REST Client
- **📋 [Sistema de Casos](./OBJETO_CASO.md)** - Estrutura de casos investigativos
- **🚀 [Infraestrutura](../infrastructure/)** - IaC com Bicep templates