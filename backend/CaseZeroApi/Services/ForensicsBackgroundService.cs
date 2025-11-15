using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using CaseZeroApi.Data;
using CaseZeroApi.Hubs;

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
                    await CheckAndCompleteForensicRequests();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Forensics Background Service");
                }

                // Check every 30 seconds
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        private async Task CheckAndCompleteForensicRequests()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var now = DateTime.UtcNow;

            // Find all pending/in-progress requests that should be completed
            var completedRequests = await context.ForensicRequests
                .Where(fr => (fr.Status == "pending" || fr.Status == "in-progress") &&
                            fr.EstimatedCompletionTime <= now)
                .ToListAsync();

            foreach (var request in completedRequests)
            {
                request.Status = "completed";
                request.CompletedAt = now;

                // Notify the specific user via SignalR
                await _hubContext.Clients
                    .Group($"user-{request.UserId}")
                    .SendAsync("ForensicCompleted", new
                    {
                        id = request.Id,
                        caseId = request.CaseId,
                        evidenceId = request.EvidenceId,
                        analysisType = request.AnalysisType,
                        completedAt = request.CompletedAt
                    });

                _logger.LogInformation(
                    "Forensic request {RequestId} completed for user {UserId}",
                    request.Id,
                    request.UserId);
            }

            if (completedRequests.Any())
            {
                await context.SaveChangesAsync();
                _logger.LogInformation("Completed {Count} forensic requests", completedRequests.Count);
            }
        }
    }
}
