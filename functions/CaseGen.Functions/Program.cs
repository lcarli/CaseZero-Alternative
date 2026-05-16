using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Functions.Worker.Builder;
using CaseGen.Functions.Services;
using CaseGen.Functions.Services.CaseV2;
using CaseGen.Functions.Services.CaseV2.Rendering;
using CaseGen.Functions.Services.CaseV2.Rendering.Layouts;
using CaseGen.Functions.Services.CaseV2.Templates;


var builder = FunctionsApplication.CreateBuilder(args);

// Ensure configuration is loaded from local.settings.json
builder.Configuration.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights()
    // Shared infrastructure
    .AddScoped<IPdfRenderingService, PdfRenderingService>()
    // v2 case generator (micro-task pipeline + PDF/image rendering)
    .AddScoped<ICaseV2GeneratorService, CaseV2GeneratorService>()
    .AddScoped<IAssetRenderingService, AssetRenderingService>()
    .AddScoped<IEvidenceDocumentRenderer, EvidenceDocumentRenderer>()
    // Document layouts — one per evidence-document chrome family. Each
    // layout owns its header/footer/section-heading style; shared section
    // payload rendering (narrative/table/keyValue/transcript/code/callout)
    // lives in DocumentBlocks so adding a new family means adding ONE file.
    .AddScoped<IDocumentLayout, GeneralReportLayout>()
    .AddScoped<IDocumentLayout, PoliceReportLayout>()
    .AddScoped<IDocumentLayout, WitnessStatementLayout>()
    .AddScoped<IDocumentLayout, InterviewTranscriptLayout>()
    .AddScoped<IDocumentLayout, ForensicReportLayout>()
    .AddScoped<IDocumentLayout, MedicalReportLayout>()
    .AddScoped<IDocumentLayout, EvidenceLogLayout>()
    .AddScoped<IDocumentLayout, MemoLayout>()
    .AddScoped<IDocumentLayout, CustodyFormLayout>()
    .AddScoped<IDocumentLayoutRegistry, DocumentLayoutRegistry>()
    .AddScoped<MechanicalRulesBuilder>()
    .AddScoped<IConsistencyValidator, ConsistencyValidator>()
    .AddScoped<ICaseV2BlobPublisher, CaseV2BlobPublisher>()
    .AddScoped<ISchemaErrorAutoFixer, SchemaErrorAutoFixer>()
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
    // LLM Provider — Azure OpenAI for production, Mock for local/dev without keys
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
