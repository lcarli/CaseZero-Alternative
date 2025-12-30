using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using CaseZeroApi.Data;
using CaseZeroApi.Services;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Xunit;

namespace CaseZeroApi.IntegrationTests
{
    public class CustomWebApplicationFactory<TStartup> : WebApplicationFactory<TStartup> where TStartup : class
    {
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
                    options.UseInMemoryDatabase("InMemoryDbForTesting")
                           .EnableSensitiveDataLogging();
                });

                // Replace IJwtService with mock implementation
                var jwtServiceDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IJwtService));
                if (jwtServiceDescriptor != null)
                {
                    services.Remove(jwtServiceDescriptor);
                }
                services.AddScoped<IJwtService, MockJwtService>();

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
                // Override configuration for testing
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "InMemory",
                    ["ASPNETCORE_ENVIRONMENT"] = "Testing"
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
    }
}