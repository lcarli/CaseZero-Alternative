using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using CaseZeroApi.Data;
using CaseZeroApi.Hubs;
using CaseZeroApi.Models;
using CaseZeroApi.Models.CaseV2;

namespace CaseZeroApi.Services
{
    public class ForensicsBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ForensicsBackgroundService> _logger;
        private readonly IHubContext<ForensicsHub> _hubContext;

        public ForensicsBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<ForensicsBackgroundService> logger,
            IHubContext<ForensicsHub> hubContext)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _hubContext = hubContext;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Forensics Background Service started");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndCompleteForensicRequests(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Forensics Background Service");
                }
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        private async Task CheckAndCompleteForensicRequests(CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var storage = scope.ServiceProvider.GetRequiredService<ICaseV2StorageService>();
            var rulesEngine = scope.ServiceProvider.GetRequiredService<IRulesEngineService>();

            var now = DateTime.UtcNow;

            var completedRequests = await context.ForensicRequests
                .Where(fr => (fr.Status == "pending" || fr.Status == "in-progress") &&
                            fr.EstimatedCompletionTime <= now)
                .ToListAsync(ct);

            foreach (var request in completedRequests)
            {
                request.Status = "completed";
                request.CompletedAt = now;

                try
                {
                    var caseData = await storage.GetRawAsync(request.CaseId, ct);
                    var outcome = caseData?.ForensicOutcomes?
                        .FirstOrDefault(o =>
                            o.InputAssetId == request.InputAssetId &&
                            string.Equals(o.AnalysisType, request.AnalysisType, StringComparison.OrdinalIgnoreCase));

                    if (outcome is not null && outcome.Findings)
                    {
                        if (!string.IsNullOrEmpty(outcome.ResultAssetId))
                            await UpsertVisibleAsset(context, request.UserId, request.CaseId, outcome.ResultAssetId, ct);
                        if (!string.IsNullOrEmpty(outcome.ResultEmailId))
                            await UpsertVisibleEmail(context, request.UserId, request.CaseId, outcome.ResultEmailId, ct);
                        request.ResultDocumentId = outcome.ResultAssetId;
                        request.ResultEmailId = outcome.ResultEmailId;
                    }
                    else
                    {
                        var template = caseData?.ForensicsDefaults?.NoFindingsEmail;
                        if (template is not null)
                        {
                            var assetName = caseData?.Assets?.FirstOrDefault(a => a.Id == request.InputAssetId)?.Title
                                ?? request.InputAssetName;
                            var synthEmail = new CaseV2Email
                            {
                                Id = $"synth-{Guid.NewGuid():N}",
                                From = template.From,
                                Subject = template.Subject
                                    .Replace("{{analysisType}}", request.AnalysisType)
                                    .Replace("{{assetName}}", assetName),
                                Body = template.Template
                                    .Replace("{{analysisType}}", request.AnalysisType)
                                    .Replace("{{assetName}}", assetName),
                                SentAt = now.ToString("o"),
                                Visibility = "initial"
                            };
                            await AppendSyntheticEmail(context, request.UserId, request.CaseId, synthEmail, ct);
                            request.ResultEmailId = synthEmail.Id;
                        }
                    }

                    await context.SaveChangesAsync(ct);

                    await rulesEngine.EvaluateAndApplyAsync(
                        request.CaseId, request.UserId,
                        new ForensicsCompleteTrigger(request.InputAssetId, request.AnalysisType), ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to apply forensic outcome for request {RequestId}", request.Id);
                }

                await _hubContext.Clients
                    .Group($"user-{request.UserId}")
                    .SendAsync("ForensicCompleted", new
                    {
                        id = request.Id,
                        caseId = request.CaseId,
                        evidenceId = request.InputAssetId,
                        analysisType = request.AnalysisType,
                        completedAt = request.CompletedAt
                    }, ct);

                _logger.LogInformation("Forensic request {RequestId} completed for user {UserId}", request.Id, request.UserId);
            }

            if (completedRequests.Any())
            {
                await context.SaveChangesAsync(ct);
            }
        }

        private static async Task UpsertVisibleAsset(ApplicationDbContext ctx, string userId, string caseId, string assetId, CancellationToken ct)
        {
            var exists = await ctx.CaseSessionVisibleAssets.AnyAsync(v => v.UserId == userId && v.CaseId == caseId && v.AssetId == assetId, ct);
            if (!exists)
                ctx.CaseSessionVisibleAssets.Add(new CaseSessionVisibleAsset { UserId = userId, CaseId = caseId, AssetId = assetId, UnlockedAt = DateTime.UtcNow });
        }

        private static async Task UpsertVisibleEmail(ApplicationDbContext ctx, string userId, string caseId, string emailId, CancellationToken ct)
        {
            var exists = await ctx.CaseSessionVisibleEmails.AnyAsync(v => v.UserId == userId && v.CaseId == caseId && v.EmailId == emailId, ct);
            if (!exists)
                ctx.CaseSessionVisibleEmails.Add(new CaseSessionVisibleEmail { UserId = userId, CaseId = caseId, EmailId = emailId, UnlockedAt = DateTime.UtcNow });
        }

        private static async Task AppendSyntheticEmail(ApplicationDbContext ctx, string userId, string caseId, CaseV2Email email, CancellationToken ct)
        {
            var session = await ctx.CaseSessions
                .Where(cs => cs.UserId == userId && cs.CaseId == caseId)
                .OrderByDescending(cs => cs.SessionStart)
                .FirstOrDefaultAsync(ct);
            if (session is null) return;

            List<CaseV2Email> list;
            if (string.IsNullOrWhiteSpace(session.SyntheticEmails))
            {
                list = new();
            }
            else
            {
                try { list = JsonSerializer.Deserialize<List<CaseV2Email>>(session.SyntheticEmails) ?? new(); }
                catch { list = new(); }
            }
            list.Add(email);
            session.SyntheticEmails = JsonSerializer.Serialize(list);
        }
    }
}
