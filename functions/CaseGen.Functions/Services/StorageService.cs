using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;

namespace CaseGen.Functions.Services;

public class StorageService : IStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<StorageService> _logger;

    public StorageService(IConfiguration configuration, ILogger<StorageService> logger)
    {
        // Prefer specific CaseGenerator setting; fall back to AzureWebJobsStorage (local.settings / App Settings)
        var connectionString = configuration["CaseGeneratorStorage:ConnectionString"]
            ?? configuration["AzureWebJobsStorage"]
            ?? Environment.GetEnvironmentVariable("AzureWebJobsStorage")
            ?? throw new InvalidOperationException("Storage connection string not configured. Set CaseGeneratorStorage:ConnectionString or AzureWebJobsStorage.");

        _blobServiceClient = new BlobServiceClient(connectionString);
        _logger = logger;
    }

    public async Task<string> SaveFileAsync(string containerName, string fileName, string content, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = await GetContainerClientAsync(containerName, cancellationToken);
            var blobClient = containerClient.GetBlobClient(fileName);

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            await blobClient.UploadAsync(stream, overwrite: true, cancellationToken);

            _logger.LogInformation("Saved file {FileName} to container {ContainerName}", fileName, containerName);
            return blobClient.Uri.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save file {FileName} to container {ContainerName}", fileName, containerName);
            throw;
        }
    }

    public async Task<string> SaveFileAsync(string containerName, string fileName, byte[] content, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = await GetContainerClientAsync(containerName, cancellationToken);
            var blobClient = containerClient.GetBlobClient(fileName);

            using var stream = new MemoryStream(content);
            await blobClient.UploadAsync(stream, overwrite: true, cancellationToken);

            _logger.LogInformation("Saved binary file {FileName} to container {ContainerName}", fileName, containerName);
            return blobClient.Uri.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save binary file {FileName} to container {ContainerName}", fileName, containerName);
            throw;
        }
    }

    public async Task<string> GetFileAsync(string containerName, string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = await GetContainerClientAsync(containerName, cancellationToken);
            var blobClient = containerClient.GetBlobClient(fileName);

            var response = await blobClient.DownloadContentAsync(cancellationToken);
            return response.Value.Content.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get file {FileName} from container {ContainerName}", fileName, containerName);
            throw;
        }
    }

    public async Task<byte[]?> GetFileBytesAsync(string containerName, string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = await GetContainerClientAsync(containerName, cancellationToken);
            var blobClient = containerClient.GetBlobClient(fileName);

            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                _logger.LogWarning("File {FileName} does not exist in container {ContainerName}", fileName, containerName);
                return null;
            }

            var response = await blobClient.DownloadContentAsync(cancellationToken);
            return response.Value.Content.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get file bytes {FileName} from container {ContainerName}", fileName, containerName);
            return null;
        }
    }

    public async Task SaveFileBytesAsync(string containerName, string fileName, byte[] content, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = await GetContainerClientAsync(containerName, cancellationToken);
            var blobClient = containerClient.GetBlobClient(fileName);

            using var stream = new MemoryStream(content);
            await blobClient.UploadAsync(stream, overwrite: true, cancellationToken);

            _logger.LogInformation("Successfully saved {Size} bytes to {FileName} in container {ContainerName}", 
                content.Length, fileName, containerName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save file bytes {FileName} to container {ContainerName}", fileName, containerName);
            throw;
        }
    }

    public async Task<bool> FileExistsAsync(string containerName, string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = await GetContainerClientAsync(containerName, cancellationToken);
            var blobClient = containerClient.GetBlobClient(fileName);

            var response = await blobClient.ExistsAsync(cancellationToken);
            return response.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if file {FileName} exists in container {ContainerName}", fileName, containerName);
            return false;
        }
    }

    public async Task DeleteFileAsync(string containerName, string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = await GetContainerClientAsync(containerName, cancellationToken);
            var blobClient = containerClient.GetBlobClient(fileName);

            await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            _logger.LogInformation("Deleted file {FileName} from container {ContainerName}", fileName, containerName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file {FileName} from container {ContainerName}", fileName, containerName);
            throw;
        }
    }

    public async Task<IEnumerable<string>> ListFilesAsync(string containerName, string prefix = "", CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = await GetContainerClientAsync(containerName, cancellationToken);
            var blobs = new List<string>();

            await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
            {
                blobs.Add(blobItem.Name);
            }

            return blobs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list files in container {ContainerName} with prefix {Prefix}", containerName, prefix);
            throw;
        }
    }

    private async Task<BlobContainerClient> GetContainerClientAsync(string containerName, CancellationToken cancellationToken = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        return containerClient;
    }
}