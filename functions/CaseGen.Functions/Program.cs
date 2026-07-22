using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Functions.Worker.Builder;
using CaseGen.Functions.Services;


var builder = FunctionsApplication.CreateBuilder(args);

// Ensure configuration is loaded from local.settings.json
builder.Configuration.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);
builder.Configuration.AddCaseGenLocalSettings(Directory.GetCurrentDirectory());
builder.Configuration.AddEnvironmentVariables();

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();
builder.Services.AddCaseGenApplicationServices(builder.Configuration);

builder.Build().Run();
