using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.CaseV2;

public class CasePublicationConflictException(string message, Exception? inner = null) : IOException(message, inner);

public sealed class LocalCasePublication : IDisposable
{
    private readonly CaseBundle _bundle;
    private readonly FileStream _lock;
    private readonly ILogger _logger;
    private readonly string _caseDirectory;
    private readonly string _bundleDirectory;
    private readonly string? _previousChecksum;
    private bool _ownsBundle;
    private bool _committed;

    private LocalCasePublication(CaseBundle bundle, FileStream publicationLock, string caseDirectory, string? previousChecksum, ILogger logger)
    {
        _bundle = bundle;
        _lock = publicationLock;
        _caseDirectory = caseDirectory;
        _bundleDirectory = Path.Combine(caseDirectory, "assets", bundle.Manifest.BundleId);
        _previousChecksum = previousChecksum;
        _logger = logger;
    }

    public static async Task<LocalCasePublication> PrepareAsync(CaseBundle bundle, string casesBasePath, ILogger logger, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var lockDirectory = Path.Combine(casesBasePath, ".casegen-locks");
        Directory.CreateDirectory(lockDirectory);
        var lockPath = Path.Combine(lockDirectory, $"{bundle.Manifest.CaseId}.lock");
        FileStream publicationLock;
        try { publicationLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException ex) { throw new CasePublicationConflictException($"Cannot acquire the publication lock for {bundle.Manifest.CaseId}: {ex.Message}", ex); }
        LocalCasePublication? publication = null;
        var prepared = false;
        try
        {
            var caseDirectory = Path.Combine(casesBasePath, bundle.Manifest.CaseId);
            Directory.CreateDirectory(caseDirectory);
            var assetsDirectory = Path.Combine(caseDirectory, "assets");
            Directory.CreateDirectory(assetsDirectory);
            if ((File.GetAttributes(caseDirectory) & FileAttributes.ReparsePoint) != 0 ||
                (File.GetAttributes(assetsDirectory) & FileAttributes.ReparsePoint) != 0)
                throw new CaseBundleValidationException("bundle_invalid", "Local case directories cannot be symbolic links.");
            var jsonPath = Path.Combine(caseDirectory, "case.json");
            var previous = File.Exists(jsonPath) ? await CaseBundle.HashFileAsync(jsonPath, ct) : null;
            publication = new LocalCasePublication(bundle, publicationLock, caseDirectory, previous, logger);
            if (Directory.Exists(publication._bundleDirectory))
                throw new CaseBundleValidationException("bundle_invalid", "The new immutable local bundle already exists.");
            Directory.CreateDirectory(publication._bundleDirectory);
            publication._ownsBundle = true;
            foreach (var source in Directory.EnumerateFiles(bundle.BundleDirectory))
            {
                ct.ThrowIfCancellationRequested();
                File.Copy(source, Path.Combine(publication._bundleDirectory, Path.GetFileName(source)), overwrite: false);
            }
            await bundle.VerifyAssetsAsync(caseDirectory, ct);
            prepared = true;
            return publication;
        }
        finally
        {
            if (!prepared)
            {
                if (publication is not null) publication.Dispose();
                else publicationLock.Dispose();
            }
        }
    }

    public async Task<string> CommitAsync(CancellationToken ct = default)
    {
        await _bundle.VerifyAsync(ct);
        await _bundle.VerifyAssetsAsync(_caseDirectory, ct);
        var jsonPath = Path.Combine(_caseDirectory, "case.json");
        var current = File.Exists(jsonPath) ? await CaseBundle.HashFileAsync(jsonPath, ct) : null;
        if (current != _previousChecksum)
            throw new CasePublicationConflictException("Local case.json changed while the new bundle was being published.");
        ct.ThrowIfCancellationRequested();
        // Staging shares the case filesystem; replacing only this file keeps old readers and assets intact.
        File.Move(_bundle.CaseJsonPath, jsonPath, overwrite: true);
        _committed = true;
        return jsonPath;
    }

    public void Dispose()
    {
        try
        {
            if (!_committed && _ownsBundle && Directory.Exists(_bundleDirectory))
            {
                var jsonPath = Path.Combine(_caseDirectory, "case.json");
                var referenced = File.Exists(jsonPath) && File.ReadAllText(jsonPath).Contains($"/assets/{_bundle.Manifest.BundleId}/", StringComparison.Ordinal);
                if (!referenced)
                    Directory.Delete(_bundleDirectory, recursive: true);
                else
                    _logger.LogWarning("Retaining bundle {BundleId}: current local case.json references it despite a failed commit", _bundle.Manifest.BundleId);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not clean uncommitted local bundle {BundleId}", _bundle.Manifest.BundleId);
        }
        finally { _lock.Dispose(); }
    }
}
