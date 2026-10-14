using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using CaseZeroApi.Data;
using CaseZeroApi.Hubs;

namespace CaseZeroApi.Services;

public sealed class ForensicsBackgroundService(IServiceProvider serviceProvider,
    ILogger<ForensicsBackgroundService> logger, IHubContext<ForensicsHub> hubContext) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessPendingAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Forensic processing failed; pending requests remain retryable"); }
            try { await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    public async Task ProcessPendingAsync(CancellationToken ct = default)
    {
        using var listingScope = serviceProvider.CreateScope();
        var context = listingScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ids = await context.ForensicRequests.AsNoTracking()
            .Where(r => r.Status == "pending" || r.Status == "in-progress")
            .OrderBy(r => r.Id).Select(r => r.Id).ToListAsync(ct);
        foreach (var id in ids)
        {
            using var scope = serviceProvider.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<ForensicRequestProcessor>();
            try
            {
                if (!await processor.ProcessAsync(id, ct)) continue;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Forensic request {RequestId} failed transiently and remains retryable", id);
                continue;
            }
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var request = await db.ForensicRequests.SingleAsync(r => r.Id == id, ct);
            try
            {
                await hubContext.Clients.Group($"user-{request.UserId}").SendAsync("ForensicCompleted", new
                {
                    request.Id, request.CaseId, evidenceId = request.InputAssetId, request.AnalysisType,
                    request.CompletedAt, request.ResultDocumentId, request.ResultEmailId
                }, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) { logger.LogError(ex, "Forensic {RequestId} committed, but realtime notification failed", id); }
        }
    }
}
