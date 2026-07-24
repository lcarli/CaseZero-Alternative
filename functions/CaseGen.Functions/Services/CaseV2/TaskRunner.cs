using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.CaseV2;

/// <summary>
/// Tiny helper that wraps the LLM call + JSON deserialization to keep micro-tasks short.
/// </summary>
public static class TaskRunner
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task<T> RunStructuredAsync<T>(
        ILLMProvider llm,
        ILogger logger,
        string taskName,
        string systemPrompt,
        string userPrompt,
        string jsonSchema,
        CancellationToken ct,
        int maxRetries = 1)
    {
        Exception? last = null;
        var effectiveUserPrompt = userPrompt;
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                var raw = await llm.GenerateStructuredResponseAsync(systemPrompt, effectiveUserPrompt, jsonSchema, ct);
                var parsed = JsonSerializer.Deserialize<T>(raw.Content, JsonOpts);
                if (parsed is null) throw new InvalidOperationException($"{taskName} produced null after deserialization.");
                return parsed;
            }
            catch (JsonException ex) when (attempt < maxRetries)
            {
                logger.LogWarning(ex, "{Task} attempt {Attempt} returned invalid JSON — retrying compactly", taskName, attempt + 1);
                last = ex;
                effectiveUserPrompt = string.Join(
                    Environment.NewLine,
                    userPrompt,
                    string.Empty,
                    "RETRY CORRECTION: The previous JSON was malformed or truncated. "
                    + "Return the complete object as single-line minified JSON using compact values, "
                    + "no redundant prose, and no optional detail beyond the schema.");
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                logger.LogWarning(ex, "{Task} attempt {Attempt} failed — retrying", taskName, attempt + 1);
                last = ex;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{Task} failed permanently", taskName);
                throw;
            }
        }
        throw last ?? new InvalidOperationException($"{taskName} failed with no captured exception");
    }
}
