using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using CaseZeroApi.Data;
using CaseZeroApi.Services;
using CaseZeroApi.Models;
using Moq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Xunit;

namespace CaseZeroApi.IntegrationTests
{
    public class CustomWebApplicationFactory<TStartup> : WebApplicationFactory<TStartup> where TStartup : class
    {
        private readonly string _databaseName = $"TestDb_{Guid.NewGuid()}";

        /// <summary>
        /// Walks up from the test bin folder to find the repo root (the directory containing
        /// the `cases/` fixture folder with `case_001/case.json`). Used as ContentRoot so the
        /// storage services' `ResolveLocalCasesRoot` finds real fixture data instead of
        /// blob-falling-back to an unreachable Azurite.
        /// </summary>
        private static string ResolveRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int i = 0; i < 8 && dir is not null; i++)
            {
                var candidate = Path.Combine(dir.FullName, "cases", "case_001", "case.json");
                if (File.Exists(candidate)) return dir.FullName;
                dir = dir.Parent;
            }
            // Fall back to current dir — tests that don't need fixtures still pass.
            return Directory.GetCurrentDirectory();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                // Remove ALL existing DbContext registrations (both options and context itself)
                var descriptorsToRemove = services
                    .Where(d => d.ServiceType == typeof(ApplicationDbContext) ||
                               d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                               (d.ServiceType.IsGenericType &&
                                d.ServiceType.GetGenericTypeDefinition() == typeof(DbContextOptions<>)))
                    .ToList();

                foreach (var descriptor in descriptorsToRemove)
                {
                    services.Remove(descriptor);
                }

                // Add ApplicationDbContext using ONLY in-memory database for testing
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase(_databaseName)
                           .EnableSensitiveDataLogging();
                });

                // Replace IJwtService with mock implementation
                var jwtServiceDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IJwtService));
                if (jwtServiceDescriptor != null)
                {
                    services.Remove(jwtServiceDescriptor);
                }
                services.AddScoped<IJwtService, MockJwtService>();

                // Replace the forensic queue with a no-op for tests so we don't try to talk to
                // Azure Storage Queues. Forensic-flow integration tests assert the request was
                // *recorded*; the actual enqueue side-effect is out of scope for the test host.
                var forensicDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IForensicQueueService));
                if (forensicDescriptor != null) services.Remove(forensicDescriptor);
                var forensicMock = new Mock<IForensicQueueService>();
                forensicMock.Setup(s => s.EnqueueForensicRequestAsync(
                    It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
                services.AddSingleton(forensicMock.Object);

                // Drop the hosted ForensicsBackgroundService so its queue-init doesn't crash
                // the test host with "Failed to initialize forensic queue: forensic-requests".
                var hostedDescriptors = services
                    .Where(d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService))
                    .Where(d => d.ImplementationType?.Name == "ForensicsBackgroundService")
                    .ToList();
                foreach (var d in hostedDescriptors) services.Remove(d);

                // Configure JWT authentication for tests
                services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(MockJwtService.GetTestSecretKey())),
                        ValidateIssuer = true,
                        ValidIssuer = "CaseZeroTestIssuer",
                        ValidateAudience = true,
                        ValidAudience = "CaseZeroTestAudience",
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero
                    };
                });
            });

            builder.ConfigureAppConfiguration((context, config) =>
            {
                // Override configuration for testing — explicitly disable blob storage so
                // CaseV2StorageService / CaseV1StorageService go straight to the filesystem
                // (cases/case_001/...) and never wait for Azurite/Azure.
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "InMemory",
                    ["ASPNETCORE_ENVIRONMENT"] = "Testing",
                    ["CaseGenV2:UseBlobStorage"] = "false",
                    // Force the storage service to read fixture cases from the repo's cases/
                    // folder regardless of where the test host's content root happens to point.
                    ["CaseGenV2:LocalCasesPath"] = Path.Combine(ResolveRepoRoot(), "cases"),
                    ["CaseGeneratorStorage:ConnectionString"] = "UseDevelopmentStorage=true"
                });
            });
        }
    }

    public class IntegrationTestsBase : IClassFixture<CustomWebApplicationFactory<Program>>
    {
        protected readonly HttpClient _client;
        protected readonly CustomWebApplicationFactory<Program> _factory;

        public IntegrationTestsBase(CustomWebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        protected static StringContent CreateJsonContent(object obj)
        {
            var json = JsonSerializer.Serialize(obj);
            return new StringContent(json, Encoding.UTF8, "application/json");
        }

        // ===== Helper Methods for Authentication & Security Tests =====

        protected async Task<string> CreateAuthenticatedUserAndGetToken(string email = "test@fic-police.gov", string? userId = null)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();
            
            var actualUserId = userId ?? email.Split('@')[0];
            
            var existingUser = await context.Users.FirstOrDefaultAsync(u => u.Id == actualUserId);
            if (existingUser != null)
            {
                return jwtService.GenerateToken(existingUser);
            }
            
            var user = new User
            {
                Id = actualUserId,
                UserName = email,
                Email = email,
                FirstName = "Test",
                LastName = "User",
                PersonalEmail = email,
                EmailConfirmed = true,
                Rank = DetectiveRank.Detective
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            return jwtService.GenerateToken(user);
        }

        protected async Task CreateActiveSessionForUser(string userId, string caseId)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var existingUserCase = await context.UserCases
                .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.CaseId == caseId);
            
            if (existingUserCase == null)
            {
                var userCase = new UserCase
                {
                    UserId = userId,
                    CaseId = caseId,
                    AssignedAt = DateTime.UtcNow
                };
                context.UserCases.Add(userCase);
            }
            
            var existingSession = await context.CaseSessions
                .FirstOrDefaultAsync(cs => cs.UserId == userId && cs.CaseId == caseId && cs.Status == SessionStatus.Active);
            
            if (existingSession == null)
            {
                var session = new CaseSession
                {
                    UserId = userId,
                    CaseId = caseId,
                    SessionStart = DateTime.UtcNow,
                    GameTimeAtStart = "00:00:00",
                    Status = SessionStatus.Active
                };
                context.CaseSessions.Add(session);
            }
            
            await context.SaveChangesAsync();
        }

        protected async Task AddVisibleAsset(string userId, string caseId, string assetId)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var existingAsset = await context.CaseSessionVisibleAssets
                .FirstOrDefaultAsync(va => va.UserId == userId && va.CaseId == caseId && va.AssetId == assetId);
            
            if (existingAsset == null)
            {
                var visibleAsset = new CaseSessionVisibleAsset
                {
                    UserId = userId,
                    CaseId = caseId,
                    AssetId = assetId
                };
                context.CaseSessionVisibleAssets.Add(visibleAsset);
                await context.SaveChangesAsync();
            }
        }

        protected async Task GrantUserCaseAccess(string userId, string caseId)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var existingUserCase = await context.UserCases
                .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.CaseId == caseId);
            
            if (existingUserCase == null)
            {
                var userCase = new UserCase
                {
                    UserId = userId,
                    CaseId = caseId,
                    AssignedAt = DateTime.UtcNow
                };
                context.UserCases.Add(userCase);
                await context.SaveChangesAsync();
            }
        }

        protected async Task CreateCaseInDatabase(string caseId, string caseTitle = "Test Case")
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var existingCase = await context.Cases.FirstOrDefaultAsync(c => c.Id == caseId);
            if (existingCase == null)
            {
                var caseEntity = new Case
                {
                    Id = caseId,
                    Title = caseTitle,
                    Description = "Test case description",
                    Status = CaseStatus.Open,
                    Priority = CasePriority.Medium,
                    Type = CaseType.Investigation,
                    MinimumRankRequired = DetectiveRank.Detective,
                    EstimatedDifficultyLevel = 1,
                    MaxScore = 100.0,
                    CreatedAt = DateTime.UtcNow
                };
                context.Cases.Add(caseEntity);
                await context.SaveChangesAsync();
            }
        }
    }
}
