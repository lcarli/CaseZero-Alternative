using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Functions.Worker.Builder;
using CaseGen.Functions.Services;


var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights()
    .AddScoped<ICaseGenerationService, CaseGenerationService>()
    .AddScoped<IStorageService, StorageService>()
    .AddScoped<ICaseLoggingService, CaseLoggingService>()
    .AddScoped<ISchemaValidationService, SchemaValidationService>()
    .AddScoped<INormalizerService, NormalizerService>()
    .AddSingleton<IJsonSchemaProvider, FileJsonSchemaProvider>()
    .AddScoped<ILLMService, LLMService>()
    // Configure LLM Provider - using Mock for now, Azure Foundry when stable
    .AddScoped<ILLMProvider>(serviceProvider =>
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        
        var useAzureFoundry = configuration.GetValue<bool>("LLM:UseAzureFoundry", false);
        
        if (useAzureFoundry)
        {
            logger.LogInformation("Using Azure Foundry LLM Provider");
            var foundryLogger = serviceProvider.GetRequiredService<ILogger<AzureFoundryLLMProvider>>();
            return new AzureFoundryLLMProvider(configuration, foundryLogger);
        }
        else
        {
            logger.LogInformation("Using Mock LLM Provider");
            var mockLogger = serviceProvider.GetRequiredService<ILogger<MockLLMProvider>>();
            return new MockLLMProvider(mockLogger);
        }
    })
    .AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddApplicationInsights();
        });

builder.Build().Run();