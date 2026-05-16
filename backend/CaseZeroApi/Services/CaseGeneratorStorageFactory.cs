using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;

namespace CaseZeroApi.Services;

/// <summary>
/// Centralised factory for Azure Storage clients used by the API to read
/// case bundles published by the Function App and to enqueue forensic work.
///
/// Prefers managed identity (when <c>CaseGeneratorStorage:AccountName</c>
/// is configured) and falls back to connection strings for local dev where
/// Azurite or the legacy AzureWebJobsStorage key is still in play.
///
/// Pattern mirrors <c>BlobServiceClientFactory</c> in the CaseGen.Functions
/// project so the two halves of the system stay consistent.
/// </summary>
public static class CaseGeneratorStorageFactory
{
    private const string AzuriteConnectionString = "UseDevelopmentStorage=true";

    public static BlobServiceClient CreateBlobServiceClient(
        IConfiguration configuration,
        TokenCredential credential)
    {
        var accountName = ResolveAccountName(configuration);
        if (!string.IsNullOrWhiteSpace(accountName))
        {
            var uri = new Uri($"https://{accountName}.blob.core.windows.net");
            return new BlobServiceClient(uri, credential);
        }

        return new BlobServiceClient(ResolveConnectionString(configuration));
    }

    public static QueueClient CreateQueueClient(
        IConfiguration configuration,
        TokenCredential credential,
        string queueName)
    {
        var accountName = ResolveAccountName(configuration);
        if (!string.IsNullOrWhiteSpace(accountName))
        {
            var uri = new Uri($"https://{accountName}.queue.core.windows.net/{queueName}");
            return new QueueClient(uri, credential);
        }

        return new QueueClient(ResolveConnectionString(configuration), queueName);
    }

    public static bool UsesManagedIdentity(IConfiguration configuration)
        => !string.IsNullOrWhiteSpace(ResolveAccountName(configuration));

    private static string? ResolveAccountName(IConfiguration configuration)
        => configuration["CaseGeneratorStorage:AccountName"];

    private static string ResolveConnectionString(IConfiguration configuration)
        => configuration["CaseGeneratorStorage:ConnectionString"]
            ?? configuration["AzureWebJobsStorage"]
            ?? Environment.GetEnvironmentVariable("AzureWebJobsStorage")
            ?? AzuriteConnectionString;
}
