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
Sistema de geração automática de casos com Azure Functions e AI:

```
backend/CaseGen.Functions/
├── Functions/            # Azure Durable Functions
├── Services/            # LLM, Storage, Logging
├── Models/              # Case Generation Models
├── Schemas/             # JSON Schemas para AI
└── Program.cs           # Configuração do pipeline
```

**🔗 Documentação Detalhada:** Para entender completamente o pipeline de geração de casos com AI, consulte [CASE_GENERATION_PIPELINE.md](./CASE_GENERATION_PIPELINE.md).

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