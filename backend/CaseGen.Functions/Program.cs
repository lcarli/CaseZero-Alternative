using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker.Builder;
using CaseGen.Functions.Services;


var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights()
    .AddScoped<ICaseGenerationService, CaseGenerationService>()
    .AddScoped<IStorageService, StorageService>()
    .AddScoped<ILLMService, LLMService>()
    .AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddApplicationInsights();
        });

builder.Build().Run();