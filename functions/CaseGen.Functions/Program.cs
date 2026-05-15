using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Functions.Worker.Builder;
using CaseGen.Functions.Services;
using CaseGen.Functions.Services.CaseGeneration;
using CaseGen.Functions.Services.CaseV2;
using CaseGen.Functions.Services.CaseV2.Templates;
using CaseGen.Functions.Data;
using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;


var builder = FunctionsApplication.CreateBuilder(args);

// Ensure configuration is loaded from local.settings.json
builder.Configuration.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

builder.ConfigureFunctionsWebApplication();

// Configure SQL Database (for forensic processing - Task 38)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrEmpty(connectionString))
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

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights()
    // Main case generation orchestrator
    .AddScoped<ICaseGenerationService, CaseGenerationService>()
    // Specialized case generation phase services
    .AddScoped<PlanGenerationService>()
    .AddScoped<ExpandService>()
    .AddScoped<DesignService>()
    .AddScoped<DocumentGenerationService>()
    .AddScoped<MediaGenerationService>()
    .AddScoped<ValidationService>()
    // Core infrastructure services
    .AddScoped<IStorageService, StorageService>()
    .AddScoped<ICaseLoggingService, CaseLoggingService>()
    .AddScoped<ISchemaValidationService, SchemaValidationService>()
    .AddScoped<INormalizerService, NormalizerService>()
    .AddScoped<ICaseFormatConverterService, CaseFormatConverterService>()
    .AddScoped<IPdfRenderingService, PdfRenderingService>()
    .AddScoped<IImagesService, ImagesService>()
    .AddSingleton<IJsonSchemaProvider, FileJsonSchemaProvider>()
    .AddScoped<IPrecisionEditor, PrecisionEditor>()
    .AddScoped<ILLMService, LLMService>()
    .AddSingleton<IRedTeamCacheService, RedTeamCacheService>()
    // v2 case generator (micro-task pipeline + PDF/image rendering)
    .AddScoped<ICaseV2GeneratorService, CaseV2GeneratorService>()
    .AddScoped<IAssetRenderingService, AssetRenderingService>()
    .AddScoped<IEvidenceDocumentRenderer, EvidenceDocumentRenderer>()
    .AddScoped<MechanicalRulesBuilder>()
    .AddScoped<IConsistencyValidator, ConsistencyValidator>()
    .AddScoped<ICaseV2BlobPublisher, CaseV2BlobPublisher>()
    .AddSingleton<IJobPhaseReporterFactory, JobPhaseReporterFactory>()
    // Digital evidence templates — each enriches a specific bodyDoc.layout before render
    .AddScoped<IEvidenceTemplate, CallLogTemplate>()
    .AddScoped<IEvidenceTemplate, PosExportTemplate>()
    .AddScoped<IEvidenceTemplate, PhoneDumpTemplate>()
    .AddScoped<IEvidenceTemplate, SensorLogTemplate>()
    .AddScoped<IEvidenceTemplate, BrowserHistoryTemplate>()
    .AddScoped<IEvidenceTemplate, BankStatementTemplate>()
    .AddScoped<IEvidenceTemplate, GpsTrackTemplate>()
    .AddScoped<IEvidenceTemplate, FileListingTemplate>()
    .AddScoped<IEvidenceTemplate, ChatExportTemplate>()
    .AddScoped<IEvidenceTemplate, EmailExportTemplate>()
    .AddScoped<IEvidenceTemplate, AccessLogTemplate>()
    .AddScoped<IEvidenceTemplateRegistry, EvidenceTemplateRegistry>()
    // Configure Context Manager for granular context storage
    .AddSingleton<IContextManager>(serviceProvider =>
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var logger = serviceProvider.GetRequiredService<ILogger<ContextManager>>();

        var blobServiceClient = BlobServiceClientFactory.Create(configuration);

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