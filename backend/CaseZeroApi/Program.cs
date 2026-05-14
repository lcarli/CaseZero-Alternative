using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using AspNetCoreRateLimit;
using CaseZeroApi.Data;
using CaseZeroApi.Models;
using CaseZeroApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // 🔒 SECURITY: Omitir propriedades null para não expor "rules":null
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// Configure Entity Framework - Always use Azure SQL Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Skip validation in Testing environment (used by integration tests)
var isTestingEnvironment = builder.Environment.EnvironmentName == "Testing";

if (string.IsNullOrEmpty(connectionString) && !isTestingEnvironment)
{
    throw new InvalidOperationException(
        "Database connection string 'DefaultConnection' is not configured. " +
        "Please set it in appsettings.json or appsettings.Development.json with your Azure SQL Database connection string.");
}

// Validate that it's not the placeholder value (skip in Testing environment)
if (!isTestingEnvironment && !string.IsNullOrEmpty(connectionString) && 
    (connectionString.Contains("your-server") || connectionString.Contains("your-username")))
{
    throw new InvalidOperationException(
        "Database connection string contains placeholder values. " +
        "Please update appsettings.json or appsettings.Development.json with your actual Azure SQL Database credentials.");
}

// Configure DbContext (tests will override this configuration)
if (!isTestingEnvironment)
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
            sqlOptions.CommandTimeout(60);
        });
    });
}

// Configure Identity
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

// Configure JWT
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var key = Encoding.ASCII.GetBytes(jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured"));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
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

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173") // Vite dev server
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Configure SignalR
builder.Services.AddSignalR();

// Register Background Services
builder.Services.AddHostedService<CaseZeroApi.Services.ForensicsBackgroundService>();

// Configure Rate Limiting
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.EnableEndpointRateLimiting = true;
    options.StackBlockedRequests = false;
    options.HttpStatusCode = 429;
    options.RealIpHeader = "X-Real-IP";
    options.ClientIdHeader = "X-ClientId";
    options.GeneralRules = new List<RateLimitRule>
    {
        new RateLimitRule
        {
            Endpoint = "*",
            Period = "1m",
            Limit = 60, // 60 requests per minute
        },
        new RateLimitRule
        {
            Endpoint = "*/api/auth/*",
            Period = "15m", 
            Limit = 50, // 50 authentication attempts per 15 minutes (development)
        },
        new RateLimitRule
        {
            Endpoint = "POST:*/api/auth/login",
            Period = "5m",
            Limit = 50, // 50 login attempts per 5 minutes (development)
        },
        // P84: Rate limiting anti-brute-force específico
        new RateLimitRule
        {
            Endpoint = "POST:*/api/forensicrequest*",
            Period = "1h",
            Limit = 10, // 10 forensics submissions per hour
        },
        new RateLimitRule
        {
            Endpoint = "POST:*/api/cases/*/emails/*/open",
            Period = "1h",
            Limit = 100, // 100 email opens per hour
        },
        new RateLimitRule
        {
            Endpoint = "GET:*/api/cases/*/assets/*/download",
            Period = "1h",
            Limit = 50, // 50 asset downloads per hour
        },
        new RateLimitRule
        {
            Endpoint = "POST:*/api/cases/*/submit",
            Period = "24h",
            Limit = 3, // 3 solution submissions per case per day
        }
    };
});

builder.Services.Configure<IpRateLimitPolicies>(options =>
{
    options.IpRules = new List<IpRateLimitPolicy>();
});

builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

// Register services
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<DataSeedingService>();

// Case storage (blob manifest + v2 case loading)
builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();
builder.Services.AddScoped<ICaseV1SanitizerService, CaseV1SanitizerService>();
builder.Services.AddScoped<ICaseV1StorageService, CaseV1StorageService>();
builder.Services.AddScoped<IVisibilityService, VisibilityService>();
builder.Services.AddScoped<IRulesEngineService, RulesEngineService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>(); // P86: Audit Log
builder.Services.AddSingleton<IForensicQueueService, ForensicQueueService>();

// Register background services
// OBSOLETE: Removed CaseProcessingBackgroundService (uses obsolete CaseProcessingService from Services/OBSOLETE/)

// OBSOLETE: EmailSettings removed (was part of old email system)
// builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// P90: Security Headers Middleware - Proteção contra ataques comuns
app.Use(async (context, next) =>
{
    // Previne clickjacking: não permite embedding em iframes
    context.Response.Headers["X-Frame-Options"] = "DENY";
    
    // Previne MIME-type sniffing
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    
    // Controla informações de referrer em navegação
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    
    // Proteção XSS legacy (browsers antigos)
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    
    // P90: Content Security Policy - Controle granular de recursos
    // Restringe origens de scripts, estilos, imagens, conexões
    context.Response.Headers["Content-Security-Policy"] = 
        "default-src 'self'; " +                           // Recursos padrão apenas do mesmo domínio
        "script-src 'self'; " +                            // Scripts apenas do mesmo domínio (sem inline ou eval)
        "style-src 'self' 'unsafe-inline'; " +             // Estilos do domínio + inline (para componentes)
        "img-src 'self' data: blob:; " +                   // Imagens do domínio + data URLs + blob (para uploads)
        "font-src 'self' data:; " +                        // Fontes do domínio + data URLs
        "connect-src 'self'; " +                           // Conexões API apenas para o mesmo domínio
        "media-src 'self' blob:; " +                       // Áudio/vídeo do domínio + blob
        "object-src 'none'; " +                            // Bloqueia <object>, <embed>, <applet>
        "frame-ancestors 'none'; " +                       // Previne embedding (complementa X-Frame-Options)
        "base-uri 'self'; " +                              // Restringe <base> tag ao mesmo domínio
        "form-action 'self'";                              // Formulários só podem submeter para o mesmo domínio
    
    // P90: Permissions Policy - Controle de features do navegador
    context.Response.Headers["Permissions-Policy"] = 
        "geolocation=(), " +                               // Bloqueia acesso à localização
        "microphone=(), " +                                // Bloqueia acesso ao microfone
        "camera=(), " +                                    // Bloqueia acesso à câmera
        "payment=(), " +                                   // Bloqueia Payment Request API
        "usb=(), " +                                       // Bloqueia WebUSB
        "magnetometer=(), " +                              // Bloqueia magnetômetro
        "gyroscope=(), " +                                 // Bloqueia giroscópio
        "accelerometer=()";                                // Bloqueia acelerômetro
    
    // HSTS: Force HTTPS em produção (inclui subdomínios, 1 ano)
    if (context.Request.IsHttps || app.Environment.IsDevelopment())
    {
        context.Response.Headers["Strict-Transport-Security"] = 
            "max-age=31536000; includeSubDomains; preload";
    }
    
    await next();
});

// Force HTTPS in production
if (app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
    app.UseHsts();
}

// CORS must be before rate limiting and other middlewares
app.UseCors("AllowFrontend");

// Rate limiting middleware
app.UseIpRateLimiting();

// P85: ID validation middleware - previne injection e valida formato
app.UseMiddleware<CaseZeroApi.Middleware.IdValidationMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<CaseZeroApi.Hubs.ForensicsHub>("/hubs/forensics");

// Check if --seed-only argument is provided
var seedOnly = args.Contains("--seed-only");

// Initialize database (skip in Testing environment - tests manage their own database)
var environment = app.Services.GetRequiredService<IHostEnvironment>();
if (environment.EnvironmentName != "Testing")
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        // Always use migrations for SQL Server
        logger.LogInformation("🗄️ Applying SQL Server migrations...");
        context.Database.Migrate();
        
        // Seed test users if none exist
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        if (!userManager.Users.Any())
        {
            // Primary test user following new pattern
            var testUser1 = new User
            {
                UserName = "john.doe@fic-police.gov",
                Email = "john.doe@fic-police.gov",
                FirstName = "John",
                LastName = "Doe",
            PersonalEmail = "john.doe.personal@example.com",
            Department = "ColdCase",
            Position = "rook",
            BadgeNumber = "4729",
            EmailVerified = true,
            Rank = DetectiveRank.Rook
        };
        
        await userManager.CreateAsync(testUser1, "Password123!");
        
        // Secondary test user following new pattern
        var testUser2 = new User
        {
            UserName = "sarah.connor@fic-police.gov",
            Email = "sarah.connor@fic-police.gov", 
            FirstName = "Sarah",
            LastName = "Connor",
            PersonalEmail = "sarah.connor.personal@example.com",
            Department = "ColdCase",
            Position = "detective",
            BadgeNumber = "1984",
            EmailVerified = true,
            Rank = DetectiveRank.Detective
        };
        
        await userManager.CreateAsync(testUser2, "Inspector456!");
        
        // Assign both users to all seeded cases
        var cases = context.Cases.ToList();
        var users = new[] { testUser1, testUser2 };
        
        foreach (var user in users)
        {
            foreach (var case_ in cases)
            {
                context.UserCases.Add(new UserCase
                {
                    UserId = user.Id,
                    CaseId = case_.Id,
                    Role = UserCaseRole.Detective
                });
                
                // Add some mock progress data
                context.CaseProgresses.Add(new CaseProgress
                {
                    UserId = user.Id,
                    CaseId = case_.Id,
                    EvidencesCollected = Random.Shared.Next(5, 15),
                    InterviewsCompleted = Random.Shared.Next(1, 5),
                    ReportsSubmitted = Random.Shared.Next(1, 3),
                    CompletionPercentage = case_.Status == CaseStatus.Resolved ? 100.0 : Random.Shared.Next(20, 80)
                });
            }
        }
        
        await context.SaveChangesAsync();
    }
    
    // Seed GDD-specific data
    var seedingService = scope.ServiceProvider.GetRequiredService<DataSeedingService>();
    await seedingService.SeedGDDDataAsync();
    
    // If --seed-only flag is provided, exit after seeding
    if (seedOnly)
    {
        logger.LogInformation("✅ Database seeding completed. Exiting (--seed-only mode).");
        Environment.Exit(0);
    }
    }
}
else
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("🧪 Testing environment - skipping database initialization");
}

app.Run();

// Make the implicit Program class public for testing
public partial class Program { }
