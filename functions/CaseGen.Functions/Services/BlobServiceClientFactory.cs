using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;

namespace CaseGen.Functions.Services;

/// <summary>
/// Centralised factory for <see cref="BlobServiceClient"/>.
/// Prefers managed identity (CaseGeneratorStorage:AccountName) and falls back
/// to connection strings (CaseGeneratorStorage:ConnectionString, AzureWebJobsStorage)
/// to keep local dev with Azurite working without code changes.
/// </summary>
public static class BlobServiceClientFactory
{
    private const string AzuriteConnectionString =
        "AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;DefaultEndpointsProtocol=http;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";

    public static BlobServiceClient Create(IConfiguration configuration)
    {
        var accountName = configuration["CaseGeneratorStorage:AccountName"]
            ?? configuration["CaseGeneratorStorage__AccountName"];

        if (!string.IsNullOrWhiteSpace(accountName))
        {
            var uri = new Uri($"https://{accountName}.blob.core.windows.net");
            return new BlobServiceClient(uri, new DefaultAzureCredential());
        }

        var connectionString = configuration["CaseGeneratorStorage:ConnectionString"]
            ?? configuration["CaseGeneratorStorage__ConnectionString"]
            ?? configuration["AzureWebJobsStorage"]
            ?? Environment.GetEnvironmentVariable("AzureWebJobsStorage");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Storage not configured. Set CaseGeneratorStorage:AccountName (managed identity, " +
                "preferred for Azure) or CaseGeneratorStorage:ConnectionString / AzureWebJobsStorage " +
                "(connection string, used for Azurite/local dev).");
        }

        if (connectionString.Equals("UseDevelopmentStorage=true", StringComparison.OrdinalIgnoreCase))
        {
            connectionString = AzuriteConnectionString;
        }

        return new BlobServiceClient(connectionString);
    }
}
