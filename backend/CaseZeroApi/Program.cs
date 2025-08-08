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
builder.Services.AddControllers();

// Configure Entity Framework
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

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
            Limit = 5, // 5 authentication attempts per 15 minutes
        },
        new RateLimitRule
        {
            Endpoint = "POST:*/api/auth/login",
            Period = "5m",
            Limit = 3, // 3 login attempts per 5 minutes
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
builder.Services.AddScoped<ICaseObjectService, CaseObjectService>();
builder.Services.AddScoped<ICaseAccessService, CaseAccessService>();
builder.Services.AddScoped<ICaseProcessingService, CaseProcessingService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// Register background services
builder.Services.AddHostedService<CaseProcessingBackgroundService>();

// Configure Email Settings
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

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

// Security Headers Middleware
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Content-Security-Policy"] = 
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'";
    
    if (context.Request.IsHttps || app.Environment.IsDevelopment())
    {
        context.Response.Headers["Strict-Transport-Security"] = 
            "max-age=31536000; includeSubDomains";
    }
    
    await next();
});

// Force HTTPS in production
if (app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
    app.UseHsts();
}

// Rate limiting middleware
app.UseIpRateLimiting();

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.EnsureCreated();
    
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
}

app.Run();

// Make the implicit Program class public for testing
public partial class Program { }
