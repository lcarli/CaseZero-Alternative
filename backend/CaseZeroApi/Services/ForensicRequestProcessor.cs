using System.Text.Json;
using CaseZeroApi.Data;
using CaseZeroApi.Models;
using CaseZeroApi.Models.CaseV2;
using Microsoft.EntityFrameworkCore;

namespace CaseZeroApi.Services;

public sealed class ForensicRequestProcessor(ApplicationDbContext context, ICaseV2StorageService storage,
    IRulesEngineService rulesEngine, CaseGameClock clock, ILogger<ForensicRequestProcessor> logger)
{
    public async Task<bool> ProcessAsync(int requestId, CancellationToken ct = default)
    {
        var request = await context.ForensicRequests.SingleOrDefaultAsync(r => r.Id == requestId, ct);
        if (request is null || request.Status is not ("pending" or "in-progress")) return false;
        try
        {
            var caseData = await storage.GetRawAsync(request.CaseId, ct)
                ?? throw new InvalidDataException("The forensic request's case is missing.");
            var session = await context.CaseSessions
                .Where(s => s.UserId == request.UserId && s.CaseId == request.CaseId)
                .OrderByDescending(s => s.SessionStart).ThenByDescending(s => s.Id)
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidDataException("The forensic request's session is missing.");
            var inputs = caseData.Assets.Where(a => a.Id == request.InputAssetId).Take(2).ToList();
            if (inputs.Count != 1)
                throw new InvalidDataException("The forensic input asset is missing or ambiguous.");
            var input = inputs[0];
            var analyses = caseData.ForensicsDefaults?.AnalysisTypes.Where(t =>
                string.Equals(t.Type, request.AnalysisType, StringComparison.OrdinalIgnoreCase)).Take(2).ToList();
            if (analyses?.Count > 1)
                throw new InvalidDataException("The forensic analysis configuration is ambiguous.");
            var analysis = analyses?.SingleOrDefault();
            if (analysis is null || analysis.DurationMinutes <= 0 || !analysis.Supports(input))
                throw new InvalidDataException("The forensic request is incompatible with the case configuration.");
            clock.Initialize(session, CaseGameClock.Parse(caseData.Metadata.OpenedAt));
            var snapshot = clock.Capture(session);
            var now = snapshot.GameTime;
            if (now < request.EstimatedCompletionTime) return false;
            var outcomes = caseData.ForensicOutcomes?.Where(o => o.InputAssetId == request.InputAssetId &&
                string.Equals(o.AnalysisType, request.AnalysisType, StringComparison.OrdinalIgnoreCase)).Take(2).ToList();
            if (outcomes?.Count > 1)
                throw new InvalidDataException("The forensic outcome configuration is ambiguous.");
            var outcome = outcomes?.SingleOrDefault();
            if (outcome is { Findings: true })
            {
                if (outcome.ResultAssetId is null && outcome.ResultEmailId is null)
                    throw new InvalidDataException("An actionable forensic outcome has no result.");
                if (outcome.ResultAssetId is not null)
                {
                    if (!caseData.Assets.Any(a => a.Id == outcome.ResultAssetId))
                        throw new InvalidDataException("The forensic result asset is missing.");
                    await using var report = await storage.GetAssetStreamAsync(request.CaseId, outcome.ResultAssetId, ct)
                        ?? throw new InvalidDataException("The forensic result file is unavailable.");
                    if (await report.ReadAsync(new byte[1], ct) == 0)
                        throw new InvalidDataException("The forensic result file is empty.");
                    if (!await context.CaseSessionVisibleAssets.AnyAsync(v => v.UserId == request.UserId &&
                        v.CaseId == request.CaseId && v.AssetId == outcome.ResultAssetId, ct))
                        context.CaseSessionVisibleAssets.Add(new()
                        {
                            UserId = request.UserId, CaseId = request.CaseId, AssetId = outcome.ResultAssetId,
                            UnlockedAt = snapshot.AsOfUtc
                        });
                    request.ResultDocumentId = outcome.ResultAssetId;
                }
                if (outcome.ResultEmailId is not null)
                {
                    if (!caseData.Emails.Any(e => e.Id == outcome.ResultEmailId))
                        throw new InvalidDataException("The forensic result email is missing.");
                    if (!await context.CaseSessionVisibleEmails.AnyAsync(v => v.UserId == request.UserId &&
                        v.CaseId == request.CaseId && v.EmailId == outcome.ResultEmailId, ct))
                        context.CaseSessionVisibleEmails.Add(new()
                        {
                            UserId = request.UserId, CaseId = request.CaseId, EmailId = outcome.ResultEmailId,
                            UnlockedAt = snapshot.AsOfUtc
                        });
                    request.ResultEmailId = outcome.ResultEmailId;
                }
            }
            else if (caseData.ForensicsDefaults?.NoFindingsEmail is { } template)
            {
                var emails = string.IsNullOrWhiteSpace(session.SyntheticEmails) ? new List<CaseV2Email>() :
                    JsonSerializer.Deserialize<List<CaseV2Email>>(session.SyntheticEmails)
                        ?? throw new InvalidDataException("Synthetic emails state is null.");
                var id = $"forensic-{request.Id}-no-findings";
                if (!emails.Any(e => e.Id == id))
                    emails.Add(new()
                    {
                        Id = id, From = template.From, Visibility = "initial", SentAt = now.ToString("o"),
                        Subject = Fill(template.Subject, request), Body = Fill(template.Template, request)
                    });
                session.SyntheticEmails = JsonSerializer.Serialize(emails);
                request.ResultEmailId = id;
                if (!await context.CaseSessionVisibleEmails.AnyAsync(v => v.UserId == request.UserId &&
                    v.CaseId == request.CaseId && v.EmailId == id, ct))
                    context.CaseSessionVisibleEmails.Add(new()
                    {
                        UserId = request.UserId, CaseId = request.CaseId, EmailId = id, UnlockedAt = snapshot.AsOfUtc
                    });
            }
            request.Status = "completed";
            request.CompletedAt = now;
            request.FailureCode = null;
            await rulesEngine.EvaluateAndApplyAsync(request.CaseId, request.UserId,
                new ForensicsCompleteTrigger(request.InputAssetId, request.AnalysisType), ct);
            await context.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogInformation(ex, "Forensic request {RequestId} changed concurrently; retry will use persisted state", requestId);
            context.ChangeTracker.Clear();
            return false;
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException)
        {
            logger.LogError(ex, "Invalid configuration/state for forensic request {RequestId}", requestId);
            context.ChangeTracker.Clear();
            var failed = await context.ForensicRequests.SingleAsync(r => r.Id == requestId, ct);
            if (failed.Status is "pending" or "in-progress")
            {
                failed.Status = "failed";
                failed.FailureCode = "forensic_configuration_invalid";
                await context.SaveChangesAsync(ct);
            }
            return false;
        }
    }

    private static string Fill(string template, ForensicRequest request) =>
        template.Replace("{{analysisType}}", request.AnalysisType).Replace("{{assetName}}", request.InputAssetName);
}
