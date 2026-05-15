using System.Net;
using System.Text.Json;
using CaseGen.Functions.Models.CaseV2;
using CaseGen.Functions.Services.CaseV2;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Functions.CaseV2;

/// <summary>
/// HTTP entrypoint for the v2 case generator. Designed for fast local iteration.
/// POST /api/cases/v2/generate body: see <see cref="GenerateCaseV2Request"/>.
/// </summary>
public class GenerateCaseV2Function
{
    private readonly ICaseV2GeneratorService _generator;
    private readonly ILogger<GenerateCaseV2Function> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public GenerateCaseV2Function(ICaseV2GeneratorService generator, ILogger<GenerateCaseV2Function> logger)
    {
        _generator = generator;
        _logger = logger;
    }

    [Function("GenerateCaseV2")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "cases/v2/generate")] HttpRequestData req,
        FunctionContext ctx)
    {
        var ct = ctx.CancellationToken;
        GenerateCaseV2Request request;
        try
        {
            using var body = req.Body;
            if (body.CanSeek && body.Length == 0)
            {
                request = new GenerateCaseV2Request();
            }
            else
            {
                request = await JsonSerializer.DeserializeAsync<GenerateCaseV2Request>(body, JsonOpts, ct)
                          ?? new GenerateCaseV2Request();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse GenerateCaseV2Request body — falling back to defaults");
            request = new GenerateCaseV2Request();
        }

        try
        {
            var result = await _generator.GenerateAsync(request, ct);
            var http = req.CreateResponse(
                result.ValidationErrors.Count == 0 ? HttpStatusCode.OK : HttpStatusCode.UnprocessableEntity);
            await http.WriteAsJsonAsync(new
            {
                caseId = result.CaseId,
                outputPath = result.OutputPath,
                stageLatencyMs = result.StageLatencyMs,
                validationErrors = result.ValidationErrors,
                rendering = new
                {
                    pdfsWritten = result.AssetsRenderedPdfs,
                    imagesWritten = result.AssetsRenderedImages,
                    skipped = result.AssetsSkipped,
                    errors = result.AssetRenderingErrors
                },
                consistency = result.Consistency is null ? null : new
                {
                    autoFixes = result.Consistency.AutoFixes,
                    warnings = result.Consistency.Warnings,
                    errors = result.Consistency.Errors
                },
                redTeam = result.RedTeam,
                solver = result.Solver,
                preview = TryPreview(result.CaseJson)
            }, ct);
            return http;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GenerateCaseV2 failed");
            var http = req.CreateResponse(HttpStatusCode.InternalServerError);
            await http.WriteAsJsonAsync(new { error = ex.Message, type = ex.GetType().Name }, ct);
            return http;
        }
    }

    private static object? TryPreview(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var meta = doc.RootElement.GetProperty("metadata");
            return new
            {
                title = meta.GetProperty("title").GetString(),
                location = meta.GetProperty("location").GetString(),
                difficulty = meta.GetProperty("difficulty").GetString(),
                suspectCount = doc.RootElement.GetProperty("suspects").GetArrayLength(),
                assetCount = doc.RootElement.GetProperty("assets").GetArrayLength(),
                emailCount = doc.RootElement.GetProperty("emails").GetArrayLength(),
                ruleCount = doc.RootElement.GetProperty("rules").GetArrayLength()
            };
        }
        catch
        {
            return null;
        }
    }
}
