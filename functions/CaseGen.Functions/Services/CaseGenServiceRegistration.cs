using CaseGen.Functions.Services.CaseV2;
using CaseGen.Functions.Services.CaseV2.Rendering;
using CaseGen.Functions.Services.CaseV2.Rendering.Layouts;
using CaseGen.Functions.Services.CaseV2.Templates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services;

public static class CaseGenServiceRegistration
{
    public static IServiceCollection AddCaseGenApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddScoped<IPdfRenderingService, PdfRenderingService>()
            .AddScoped<ICaseV2GeneratorService, CaseV2GeneratorService>()
            .AddScoped<IAssetRenderingService, AssetRenderingService>()
            .AddScoped<IEvidenceDocumentRenderer, EvidenceDocumentRenderer>()
            .AddScoped<IDocumentLayout, GeneralReportLayout>()
            .AddScoped<IDocumentLayout, PoliceReportLayout>()
            .AddScoped<IDocumentLayout, WitnessStatementLayout>()
            .AddScoped<IDocumentLayout, InterviewTranscriptLayout>()
            .AddScoped<IDocumentLayout, AudioTranscriptLayout>()
            .AddScoped<IDocumentLayout, ForensicReportLayout>()
            .AddScoped<IDocumentLayout, MedicalReportLayout>()
            .AddScoped<IDocumentLayout, EvidenceLogLayout>()
            .AddScoped<IDocumentLayout, MemoLayout>()
            .AddScoped<IDocumentLayout, CustodyFormLayout>()
            .AddScoped<IDocumentLayout, NewspaperClippingLayout>()
            .AddScoped<IDocumentLayout, PersonalLetterLayout>()
            .AddScoped<IDocumentLayout, ReceiptLayout>()
            .AddScoped<IDocumentLayout, SearchWarrantLayout>()
            .AddScoped<IDocumentLayout, DispatchLogLayout>()
            .AddScoped<IDocumentLayout, CaseMapLayout>()
            .AddScoped<IDocumentLayout, CalendarLayout>()
            .AddScoped<IDocumentLayoutRegistry, DocumentLayoutRegistry>()
            .AddScoped<MechanicalRulesBuilder>()
            .AddScoped<IConsistencyValidator, ConsistencyValidator>()
            .AddScoped<ICaseV2BlobPublisher, CaseV2BlobPublisher>()
            .AddScoped<ISchemaErrorAutoFixer, SchemaErrorAutoFixer>()
            .AddSingleton<IJobPhaseReporterFactory, JobPhaseReporterFactory>()
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
            .AddScoped<IEvidenceTemplate, AudioTranscriptTemplate>()
            .AddScoped<IEvidenceTemplateRegistry, EvidenceTemplateRegistry>()
            .AddScoped<ILLMProvider>(serviceProvider =>
            {
                var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
                var useAzureFoundry = configuration.GetValue("LLM:UseAzureFoundry", false);
                if (useAzureFoundry)
                {
                    loggerFactory.CreateLogger("CaseGen.LLM")
                        .LogInformation("Using Azure Foundry LLM Provider");
                    return new UsageTrackingLLMProvider(new AzureFoundryLLMProvider(
                        configuration,
                        serviceProvider.GetRequiredService<ILogger<AzureFoundryLLMProvider>>()));
                }

                loggerFactory.CreateLogger("CaseGen.LLM")
                    .LogInformation("Using Mock LLM Provider");
                return new UsageTrackingLLMProvider(new MockLLMProvider(
                    serviceProvider.GetRequiredService<ILogger<MockLLMProvider>>()));
            })
            .AddLogging(logging => logging.AddConsole());
        return services;
    }
}
