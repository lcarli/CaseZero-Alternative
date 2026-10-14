using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CaseGen.Functions.Models.CaseV2;

namespace CaseGen.Functions.Services.CaseV2;

public sealed class GenerationRequestException : ArgumentException
{
    public IReadOnlyList<string> Errors { get; }
    public bool TooLarge { get; }

    public GenerationRequestException(IEnumerable<string> errors, bool tooLarge = false)
        : base(string.Join("; ", errors))
    {
        Errors = errors.ToArray();
        TooLarge = tooLarge;
    }
}

public static class GenerationRequestValidator
{
    public const int MaxBodyBytes = 16 * 1024;
    public static IReadOnlyList<string> Ranks { get; } =
        new[] { "Rookie", "Detective", "Detective2", "Sergeant", "Lieutenant", "Captain", "Commander" };
    public static IReadOnlyList<string> Languages { get; } = new[] { "pt-BR", "en-US", "es-ES", "fr-FR" };

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static async Task<GenerateCaseV2Request> ReadAsync(Stream body, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        int read;
        while ((read = await body.ReadAsync(chunk, ct)) != 0)
        {
            if (buffer.Length + read > MaxBodyBytes)
                throw new GenerationRequestException(new[] { $"Request exceeds {MaxBodyBytes} bytes." }, tooLarge: true);
            buffer.Write(chunk, 0, read);
        }
        ct.ThrowIfCancellationRequested();
        if (buffer.Length == 0) return new GenerateCaseV2Request();
        try
        {
            var json = new System.Text.UTF8Encoding(false, true).GetString(buffer.ToArray());
            using var document = StructuredJson.ParseObject(json, StringComparer.OrdinalIgnoreCase);
            var request = JsonSerializer.Deserialize<GenerateCaseV2Request>(json, JsonOpts)
                ?? throw new JsonException("Request must be a JSON object.");
            Validate(request);
            return request;
        }
        catch (Exception ex) when (ex is JsonException or System.Text.DecoderFallbackException)
        {
            throw new GenerationRequestException(new[] { $"Invalid request JSON: {ex.Message}" });
        }
    }

    public static void Validate(GenerateCaseV2Request request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new List<string>();
        CheckText(request.CaseId, "caseId", 128, errors);
        CheckText(request.Title, "title", 200, errors);
        CheckText(request.Theme, "theme", 2000, errors);
        CheckText(request.Location, "location", 300, errors);
        if (request.CaseId is not null &&
            !Regex.IsMatch(request.CaseId.Trim(), @"^[A-Za-z0-9][A-Za-z0-9_-]*$", RegexOptions.CultureInvariant))
            errors.Add("caseId must contain only letters, digits, underscores, or hyphens.");
        if (request.CaseId is not null &&
            !Regex.IsMatch(NormalizeCaseId(request.CaseId), @"^case_[a-z0-9_]+$", RegexOptions.CultureInvariant))
            errors.Add("caseId must have a nonempty suffix after case_.");
        if (request.Difficulty is not null && !Ranks.Contains(request.Difficulty, StringComparer.Ordinal))
            errors.Add($"difficulty must be one of: {string.Join(", ", Ranks)}.");
        if (request.RequiredRank is not null && !Ranks.Contains(request.RequiredRank, StringComparer.Ordinal))
            errors.Add($"requiredRank must be one of: {string.Join(", ", Ranks)}.");
        if (request.Language is not null && !Languages.Contains(request.Language, StringComparer.Ordinal))
            errors.Add($"language must be one of: {string.Join(", ", Languages)}.");
        if (errors.Count > 0) throw new GenerationRequestException(errors);
    }

    public static string NormalizeCaseId(string? caseId)
    {
        if (caseId is null) return $"case_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        var id = caseId.Trim().ToLowerInvariant().Replace('-', '_');
        return id.StartsWith("case_", StringComparison.Ordinal) ? id : "case_" + id;
    }

    private static void CheckText(string? value, string field, int maximum, List<string> errors)
    {
        if (value is not null && (string.IsNullOrWhiteSpace(value) || value.Length > maximum))
            errors.Add($"{field} must be nonblank and at most {maximum} characters.");
    }
}
