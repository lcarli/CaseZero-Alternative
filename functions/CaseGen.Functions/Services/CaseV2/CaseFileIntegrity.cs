using System.Buffers.Binary;
using System.Security.Cryptography;

namespace CaseGen.Functions.Services.CaseV2;

public record CaseFileInspection(string MimeType, long SizeBytes, string Sha256);

public class CaseBundleValidationException(string code, string message, string? reference = null)
    : IOException(message)
{
    public string Code { get; } = code;
    public string? Reference { get; } = reference;
}

public static class CaseFileIntegrity
{
    public static (string Extension, string MimeType) DetectImage(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return ("png", "image/png");
        if (bytes.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }))
            return ("jpg", "image/jpeg");
        if (bytes.StartsWith("GIF87a"u8) || bytes.StartsWith("GIF89a"u8))
            return ("gif", "image/gif");
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8))
            return ("webp", "image/webp");
        throw new CaseBundleValidationException("asset_integrity_failed", "Image bytes do not have a supported image signature.");
    }

    public static async Task<CaseFileInspection> InspectAsync(string path, string assetId, CancellationToken ct = default)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length == 0)
            throw new CaseBundleValidationException("asset_file_missing", $"Rendered asset is missing or empty: {info.Name}", assetId);
        Require((info.Attributes & FileAttributes.ReparsePoint) == 0, "Evidence files cannot be symbolic links.");

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var length = stream.Length;
        var prefix = new byte[12];
        var read = await stream.ReadAtLeastAsync(prefix, prefix.Length, throwOnEndOfStream: false, ct);
        string mimeType;
        string extension;
        if (prefix.AsSpan(0, read).StartsWith("%PDF-"u8))
        {
            mimeType = "application/pdf";
            extension = "pdf";
        }
        else
        {
            try { (extension, mimeType) = DetectImage(prefix.AsSpan(0, read)); }
            catch (CaseBundleValidationException ex)
            {
                throw new CaseBundleValidationException(ex.Code, $"Unsupported evidence bytes in {info.Name}.", assetId);
            }
        }
        var actualExtension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        Require(actualExtension == extension || (extension == "jpg" && actualExtension == "jpeg"),
            $"File extension does not match {mimeType}: {info.Name}");

        var tailSize = (int)Math.Min(length, mimeType == "application/pdf" ? 1024 : 12);
        var tail = new byte[tailSize];
        stream.Position = length - tailSize;
        await stream.ReadExactlyAsync(tail, ct);
        var complete = mimeType switch
        {
            "application/pdf" => length >= 16 && tail.AsSpan().IndexOf("%%EOF"u8) >= 0,
            "image/png" => length >= 45 && tail.Length == 12 && tail.AsSpan(0, 8).SequenceEqual(new byte[] { 0, 0, 0, 0, 0x49, 0x45, 0x4E, 0x44 }),
            "image/jpeg" => length >= 4 && tail[^2] == 0xFF && tail[^1] == 0xD9,
            "image/gif" => length >= 14 && tail[^1] == 0x3B,
            "image/webp" => length >= 20 && BinaryPrimitives.ReadUInt32LittleEndian(prefix.AsSpan(4, 4)) == length - 8,
            _ => false
        };
        Require(complete, $"Evidence file is truncated or lacks its format terminator: {info.Name}");
        stream.Position = 0;
        var checksum = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).ToLowerInvariant();
        Require(stream.Length == length, $"Evidence file changed during verification: {info.Name}");
        return new CaseFileInspection(mimeType, length, checksum);

        void Require(bool condition, string message)
        {
            if (!condition) throw new CaseBundleValidationException("asset_integrity_failed", message, assetId);
        }
    }
}
