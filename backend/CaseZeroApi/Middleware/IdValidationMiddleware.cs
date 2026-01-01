using System.Text.RegularExpressions;

namespace CaseZeroApi.Middleware;

/// <summary>
/// P85: Middleware para validar integridade de IDs e prevenir injection
/// Valida formato: asset.xxx, email.xxx, suspect.xxx, case_xxx
/// Rejeita IDs malformados com 400 Bad Request
/// </summary>
public class IdValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<IdValidationMiddleware> _logger;

    // Padrões válidos para IDs
    private static readonly Regex AssetIdPattern = new Regex(@"^asset\.[a-zA-Z0-9_\-\.]+$", RegexOptions.Compiled);
    private static readonly Regex EmailIdPattern = new Regex(@"^email\.[a-zA-Z0-9_\-\.]+$", RegexOptions.Compiled);
    private static readonly Regex SuspectIdPattern = new Regex(@"^suspect\.[a-zA-Z0-9_\-\.]+$", RegexOptions.Compiled);
    private static readonly Regex CaseIdPattern = new Regex(@"^case_[a-zA-Z0-9_\-]+$", RegexOptions.Compiled);
    private static readonly Regex NoFindingsEmailIdPattern = new Regex(@"^no-findings-[a-f0-9\-]+$", RegexOptions.Compiled);
    private static readonly Regex AttachmentIdPattern = new Regex(@"^attachment\.[a-zA-Z0-9_\-\.]+$", RegexOptions.Compiled);

    public IdValidationMiddleware(RequestDelegate next, ILogger<IdValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Extrair IDs dos route parameters
        var routeValues = context.Request.RouteValues;
        
        // Validar assetId
        if (routeValues.TryGetValue("assetId", out var assetIdObj) && assetIdObj != null)
        {
            var assetId = assetIdObj.ToString();
            if (!IsValidAssetId(assetId))
            {
                _logger.LogWarning("🚨 Invalid assetId format blocked: {AssetId}", assetId);
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "Invalid assetId format" });
                return;
            }
        }

        // Validar emailId
        if (routeValues.TryGetValue("emailId", out var emailIdObj) && emailIdObj != null)
        {
            var emailId = emailIdObj.ToString();
            if (!IsValidEmailId(emailId))
            {
                _logger.LogWarning("🚨 Invalid emailId format blocked: {EmailId}", emailId);
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "Invalid emailId format" });
                return;
            }
        }

        // Validar suspectId
        if (routeValues.TryGetValue("suspectId", out var suspectIdObj) && suspectIdObj != null)
        {
            var suspectId = suspectIdObj.ToString();
            if (!IsValidSuspectId(suspectId))
            {
                _logger.LogWarning("🚨 Invalid suspectId format blocked: {SuspectId}", suspectId);
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "Invalid suspectId format" });
                return;
            }
        }

        // Validar caseId
        if (routeValues.TryGetValue("caseId", out var caseIdObj) && caseIdObj != null)
        {
            var caseId = caseIdObj.ToString();
            if (!IsValidCaseId(caseId))
            {
                _logger.LogWarning("🚨 Invalid caseId format blocked: {CaseId}", caseId);
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "Invalid caseId format" });
                return;
            }
        }

        // Validar attachmentId
        if (routeValues.TryGetValue("attachmentId", out var attachmentIdObj) && attachmentIdObj != null)
        {
            var attachmentId = attachmentIdObj.ToString();
            if (!IsValidAttachmentId(attachmentId))
            {
                _logger.LogWarning("🚨 Invalid attachmentId format blocked: {AttachmentId}", attachmentId);
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "Invalid attachmentId format" });
                return;
            }
        }

        // Todos IDs válidos, continuar pipeline
        await _next(context);
    }

    private static bool IsValidAssetId(string? assetId)
    {
        if (string.IsNullOrWhiteSpace(assetId)) return false;
        return AssetIdPattern.IsMatch(assetId);
    }

    private static bool IsValidEmailId(string? emailId)
    {
        if (string.IsNullOrWhiteSpace(emailId)) return false;
        // Aceitar tanto email.xxx quanto no-findings-xxx
        return EmailIdPattern.IsMatch(emailId) || NoFindingsEmailIdPattern.IsMatch(emailId);
    }

    private static bool IsValidSuspectId(string? suspectId)
    {
        if (string.IsNullOrWhiteSpace(suspectId)) return false;
        return SuspectIdPattern.IsMatch(suspectId);
    }

    private static bool IsValidCaseId(string? caseId)
    {
        if (string.IsNullOrWhiteSpace(caseId)) return false;
        return CaseIdPattern.IsMatch(caseId);
    }

    private static bool IsValidAttachmentId(string? attachmentId)
    {
        if (string.IsNullOrWhiteSpace(attachmentId)) return false;
        return AttachmentIdPattern.IsMatch(attachmentId);
    }
}
