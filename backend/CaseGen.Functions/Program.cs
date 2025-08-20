using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CaseGen.Functions.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        
        // Add case generation services
        services.AddScoped<ICaseGenerationService, CaseGenerationService>();
        services.AddScoped<IStorageService, StorageService>();
        services.AddScoped<ILLMService, LLMService>();
        
        // Add logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddApplicationInsights();
        });
    })
    .Build();

host.Run();