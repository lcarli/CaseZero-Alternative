using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
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

// Register services
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<DataSeedingService>();

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

app.UseHttpsRedirection();

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.EnsureCreated();
    
    // Seed a test user if none exists
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
    if (!userManager.Users.Any())
    {
        var testUser = new User
        {
            UserName = "detective@police.gov",
            Email = "detective@police.gov",
            FirstName = "John",
            LastName = "Doe",
            Department = "Investigation Division",
            Position = "Detective",
            BadgeNumber = "4729",
            IsApproved = true
        };
        
        await userManager.CreateAsync(testUser, "Password123!");
        
        // Assign the user to all seeded cases
        var cases = context.Cases.ToList();
        foreach (var case_ in cases)
        {
            context.UserCases.Add(new UserCase
            {
                UserId = testUser.Id,
                CaseId = case_.Id,
                Role = UserCaseRole.Detective
            });
            
            // Add some mock progress data
            context.CaseProgresses.Add(new CaseProgress
            {
                UserId = testUser.Id,
                CaseId = case_.Id,
                EvidencesCollected = Random.Shared.Next(5, 15),
                InterviewsCompleted = Random.Shared.Next(1, 5),
                ReportsSubmitted = Random.Shared.Next(1, 3),
                CompletionPercentage = case_.Status == CaseStatus.Resolved ? 100.0 : Random.Shared.Next(20, 80)
            });
        }
        
        await context.SaveChangesAsync();
    }
    
    // Seed GDD-specific data
    var seedingService = scope.ServiceProvider.GetRequiredService<DataSeedingService>();
    await seedingService.SeedGDDDataAsync();
}

app.Run();
