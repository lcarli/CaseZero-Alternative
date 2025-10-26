using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Functions.Worker.Builder;
using CaseGen.Functions.Services;
using Azure.Storage.Blobs;


var builder = FunctionsApplication.CreateBuilder(args);

// Ensure configuration is loaded from local.settings.json
builder.Configuration.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights()
    .AddScoped<ICaseGenerationService, CaseGenerationService>()
    .AddScoped<IStorageService, StorageService>()
    .AddScoped<ICaseLoggingService, CaseLoggingService>()
    .AddScoped<ISchemaValidationService, SchemaValidationService>()
    .AddScoped<INormalizerService, NormalizerService>()
    .AddScoped<IPdfRenderingService, PdfRenderingService>()
    .AddScoped<IImagesService, ImagesService>()
    .AddSingleton<IJsonSchemaProvider, FileJsonSchemaProvider>()
    .AddScoped<IPrecisionEditor, PrecisionEditor>()
    .AddScoped<ILLMService, LLMService>()
    .AddSingleton<IRedTeamCacheService, RedTeamCacheService>()
    // Configure Context Manager for granular context storage
    .AddSingleton<IContextManager>(serviceProvider =>
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var logger = serviceProvider.GetRequiredService<ILogger<ContextManager>>();
        
        // Read connection string - handle both formats
        var connectionString = configuration["CaseGeneratorStorage__ConnectionString"];
        
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Fallback to AzureWebJobsStorage if not found
            connectionString = configuration["AzureWebJobsStorage"];
        }
        
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "CaseGeneratorStorage__ConnectionString is not configured. " +
                "Please set it in local.settings.json or environment variables.");
        }
        
        logger.LogInformation("Initializing ContextManager with connection string format: {Format}", 
            connectionString.StartsWith("UseDevelopmentStorage") ? "Development Storage" : "Custom");
        
        // Convert "UseDevelopmentStorage=true" to full connection string for modern SDK
        if (connectionString.Equals("UseDevelopmentStorage=true", StringComparison.OrdinalIgnoreCase))
        {
            connectionString = "AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;DefaultEndpointsProtocol=http;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";
            logger.LogInformation("Converted to Azurite connection string");
        }
        
        var blobServiceClient = new BlobServiceClient(connectionString);
        
        // Use a dedicated container for context storage
        return new ContextManager(blobServiceClient, logger, containerName: "case-context");
    })
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