using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.CaseV2;

/// <summary>
/// Validates each micro-task before it can update the draft, with at most one repair retry.
/// </summary>
public static class TaskRunner
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
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
        int maxRetries = 1,
        Action<T>? validate = null)
    {
        if (maxRetries is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(maxRetries), "Only zero or one retry is allowed.");
        ct.ThrowIfCancellationRequested();
        var schema = StructuredJson.ParseSchema(jsonSchema);
        var prompt = userPrompt;
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var raw = await llm.GenerateStructuredResponseAsync(
                    systemPrompt + "\nWrite case content in the language specified by the request/draft. Keep identifiers and enum values unchanged.",
                    prompt, jsonSchema, ct);
                ct.ThrowIfCancellationRequested();
                StructuredJson.Validate(raw.Content, schema);
                var parsed = JsonSerializer.Deserialize<T>(raw.Content, JsonOpts);
                if (parsed is null) throw new TaskContractException("Response deserialized to null.");
                validate?.Invoke(parsed);
                ct.ThrowIfCancellationRequested();
                return parsed;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (IsRetryable(ex))
            {
                ct.ThrowIfCancellationRequested();
                if (attempt == maxRetries)
                {
                    logger.LogError(ex, "{Task} failed after {Attempts} attempt(s)", taskName, attempt + 1);
                    if (ex is TaskContractException or JsonException or Newtonsoft.Json.JsonException)
                        throw new TaskContractException($"{taskName}: {ex.Message}");
                    throw;
                }
                logger.LogWarning(ex, "{Task} attempt {Attempt} failed; retrying once", taskName, attempt + 1);
                if (ex is TaskContractException or JsonException or Newtonsoft.Json.JsonException)
                {
                    var error = ex.Message.Length > 4000 ? ex.Message[..4000] : ex.Message;
                    prompt = $"{userPrompt}\n\nThe previous attempt failed validation: {error}\n" +
                        "Correct this specific error. Preserve the supplied IDs, facts, and intended answer. Emit the complete JSON object only.";
                }
            }
        }
        throw new InvalidOperationException($"{taskName} exhausted its bounded attempts.");
    }

    private static bool IsRetryable(Exception exception) => exception switch
    {
        TaskContractException or JsonException or Newtonsoft.Json.JsonException => true,
        Azure.RequestFailedException ex => IsTransientStatus(ex.Status),
        System.ClientModel.ClientResultException ex => IsTransientStatus(ex.Status),
        HttpRequestException ex => ex.StatusCode is null || IsTransientStatus((int)ex.StatusCode),
        TimeoutException => true,
        _ => false
    };

    private static bool IsTransientStatus(int status) => status is 408 or 429 || status >= 500;
}
