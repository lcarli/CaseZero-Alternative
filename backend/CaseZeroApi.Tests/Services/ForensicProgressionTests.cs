using System.Security.Claims;
using System.Text.Json;
using CaseZeroApi.Controllers;
using CaseZeroApi.Data;
using CaseZeroApi.DTOs;
using CaseZeroApi.Models;
using CaseZeroApi.Models.CaseV2;
using CaseZeroApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CaseZeroApi.Tests.Services;

public sealed class ForensicProgressionTests : IDisposable
{
    private const string UserId = "forensic-player";
    private const string CaseId = "case_forensic_progression";
    private readonly ApplicationDbContext db;
    private readonly Mock<ICaseV2StorageService> storage = new();
    private readonly TestGameTimeProvider time = new();
    private readonly CaseGameClock clock;
    private readonly RulesEngineService rules;
    private readonly CaseV2SanitizerService sanitizer;
    private readonly CaseV2 caseData;

    public ForensicProgressionTests()
    {
        db = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        clock = new(time);
        caseData = BuildCase();
        storage.Setup(s => s.GetRawAsync(CaseId, It.IsAny<CancellationToken>())).ReturnsAsync(caseData);
        storage.Setup(s => s.GetAssetStreamAsync(CaseId, "asset.report", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream("%PDF-1.7\nsynthetic test\n%%EOF"u8.ToArray()));
        rules = new(db, storage.Object, NullLogger<RulesEngineService>.Instance);
        sanitizer = new(db, NullLogger<CaseV2SanitizerService>.Instance);
    }

    private async Task<CaseSession> StartAsync()
    {
        var session = new CaseSession { CaseId = CaseId, UserId = UserId, SessionStart = time.GetUtcNow().UtcDateTime };
        clock.Initialize(session, CaseGameClock.Parse(caseData.Metadata.OpenedAt));
        db.CaseSessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    private ForensicRequestController Controller()
    {
        var audit = new Mock<IAuditLogService>();
        var controller = new ForensicRequestController(db, NullLogger<ForensicRequestController>.Instance,
            audit.Object, storage.Object, sanitizer, clock);
        Authenticate(controller);
        return controller;
    }

    private static void Authenticate(ControllerBase controller) =>
        controller.ControllerContext = new() { HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, UserId) }, "test"))
        }};

    private ForensicRequestProcessor Processor() =>
        new(db, storage.Object, rules, clock, NullLogger<ForensicRequestProcessor>.Instance);

    private CaseSessionController SessionController()
    {
        var controller = new CaseSessionController(db, new Mock<IVisibilityService>().Object,
            NullLogger<CaseSessionController>.Instance, storage.Object, clock);
        Authenticate(controller);
        return controller;
    }

    private EmailsController EmailController()
    {
        var controller = new EmailsController(db, NullLogger<EmailsController>.Instance, storage.Object,
            new Mock<IAuditLogService>().Object, rules, sanitizer);
        Authenticate(controller);
        return controller;
    }

    private static ForensicRequestController.CreateRequest Input(string asset = "asset.phone", string analysis = "Digital") =>
        new() { CaseId = CaseId, InputAssetId = asset, AnalysisType = analysis };

    private static ForensicRequest Created(ActionResult<ForensicRequest> result) =>
        Assert.IsType<ForensicRequest>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);

    [Fact]
    public async Task GatedInvestigation_ProducesRealEmailPdfAndCumulativeReveal()
    {
        await StartAsync();
        var before = await sanitizer.SanitizeForUserAsync(caseData, UserId, CaseId);
        Assert.DoesNotContain(before.Assets, a => a.Id == "asset.report");
        var request = Created(await Controller().CreateForensicRequest(Input()));
        Assert.Equal(TimeSpan.FromMinutes(15), request.EstimatedCompletionTime - request.RequestedAt);
        Assert.False(await Processor().ProcessAsync(request.Id));
        time.Advance(TimeSpan.FromSeconds(15));
        Assert.True(await Processor().ProcessAsync(request.Id));
        Assert.Equal("completed", request.Status);
        Assert.Equal("asset.report", request.ResultDocumentId);
        Assert.Equal("email.lab", request.ResultEmailId);
        var after = await sanitizer.SanitizeForUserAsync(caseData, UserId, CaseId);
        Assert.Contains(after.Assets, a => a.Id == "asset.report");
        Assert.Contains(after.Emails, e => e.Id == "email.lab");
        Assert.DoesNotContain(after.Assets, a => a.Id == "asset.final");
        await rules.EvaluateAndApplyAsync(CaseId, UserId, new AssetViewedTrigger("asset.report"));
        Assert.Contains((await sanitizer.SanitizeForUserAsync(caseData, UserId, CaseId)).Assets, a => a.Id == "asset.final");
        Assert.False(await Processor().ProcessAsync(request.Id));
        await rules.EvaluateAndApplyAsync(CaseId, UserId, new AssetViewedTrigger("asset.report"));
        Assert.Equal(1, await db.CaseSessionVisibleAssets.CountAsync(v => v.AssetId == "asset.final"));
    }

    [Theory]
    [InlineData("asset.report", "Digital")]
    [InlineData("asset.phone", "DNA")]
    [InlineData("asset.phone", "UnknownType")]
    public async Task HiddenIncompatibleOrUnknownRequests_AreRejected(string asset, string analysis)
    {
        await StartAsync();
        Assert.IsType<BadRequestObjectResult>((await Controller().CreateForensicRequest(Input(asset, analysis))).Result);
        Assert.Empty(db.ForensicRequests);
    }

    [Fact]
    public async Task DuplicateRequest_ReusesPersistedIdAndDoesNotRecalculateDeadline()
    {
        await StartAsync();
        var first = Created(await Controller().CreateForensicRequest(Input()));
        time.Advance(TimeSpan.FromSeconds(3));
        var repeated = await Controller().CreateForensicRequest(Input(analysis: "digital"));
        Assert.Equal(first.Id, Assert.IsType<ForensicRequest>(Assert.IsType<OkObjectResult>(repeated.Result).Value).Id);
        Assert.Equal(1, await db.ForensicRequests.CountAsync());
    }

    [Fact]
    public async Task PauseStopsDeadline_ResumeKeepsRequestsAndResultsDoNotDuplicate()
    {
        var session = await StartAsync();
        var request = Created(await Controller().CreateForensicRequest(Input()));
        time.Advance(TimeSpan.FromSeconds(5));
        clock.Capture(session);
        session.Status = SessionStatus.Paused;
        await db.SaveChangesAsync();
        time.Advance(TimeSpan.FromDays(2));
        Assert.False(await Processor().ProcessAsync(request.Id));
        Assert.IsType<ConflictObjectResult>((await Controller().CreateForensicRequest(Input(analysis: "Toxicology"))).Result);
        clock.Resume(session);
        await db.SaveChangesAsync();
        time.Advance(TimeSpan.FromSeconds(10));
        Assert.True(await Processor().ProcessAsync(request.Id));
        Assert.False(await Processor().ProcessAsync(request.Id));
        Assert.Equal(1, await db.CaseSessionVisibleEmails.CountAsync(v => v.EmailId == "email.lab"));
    }

    [Fact]
    public async Task NoFindings_IsPersistedAsOneSyntheticEmailAndCanBeRead()
    {
        await StartAsync();
        var request = Created(await Controller().CreateForensicRequest(Input(analysis: "Toxicology")));
        time.Advance(TimeSpan.FromSeconds(30));
        Assert.True(await Processor().ProcessAsync(request.Id));
        Assert.False(await Processor().ProcessAsync(request.Id));
        var visible = await sanitizer.SanitizeForUserAsync(caseData, UserId, CaseId);
        var email = Assert.Single(visible.Emails.Where(e => e.Id == request.ResultEmailId));
        Assert.Contains("Toxicology", email.Body);
        Assert.Contains("Phone", email.Body);
        Assert.True(await db.CaseSessionVisibleEmails.AnyAsync(e => e.EmailId == email.Id));
        Assert.Null(request.ResultDocumentId);
        Assert.IsType<OkObjectResult>(await EmailController().OpenEmail(CaseId, email.Id));
        var details = Assert.IsType<OkObjectResult>(await EmailController().GetEmailDetails(CaseId, email.Id));
        Assert.Contains("Toxicology", JsonSerializer.Serialize(details.Value));
        Assert.Single((await sanitizer.SanitizeForUserAsync(caseData, UserId, CaseId)).EmailStates);
    }

    [Fact]
    public async Task MissingResultArtifact_FailsExplicitlyWithoutPartialReveal()
    {
        await StartAsync();
        var request = Created(await Controller().CreateForensicRequest(Input()));
        caseData.Assets.RemoveAll(a => a.Id == "asset.report");
        time.Advance(TimeSpan.FromSeconds(15));
        Assert.False(await Processor().ProcessAsync(request.Id));
        var failed = await db.ForensicRequests.SingleAsync();
        Assert.Equal("failed", failed.Status);
        Assert.Equal("forensic_configuration_invalid", failed.FailureCode);
        Assert.Empty(db.CaseSessionVisibleEmails);
        Assert.Empty(db.CaseSessionVisibleAssets);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("empty")]
    [InlineData("non-seekable-empty")]
    public async Task UnavailablePhysicalReport_FailsWithoutClaimingACompletedResult(string kind)
    {
        await StartAsync();
        var request = Created(await Controller().CreateForensicRequest(Input()));
        Stream? report = kind == "empty" ? new MemoryStream() : null;
        if (kind == "non-seekable-empty")
        {
            var stream = new Mock<Stream>();
            stream.Setup(s => s.ReadAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
            report = stream.Object;
        }
        storage.Setup(s => s.GetAssetStreamAsync(CaseId, "asset.report", It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        time.Advance(TimeSpan.FromSeconds(15));
        Assert.False(await Processor().ProcessAsync(request.Id));
        Assert.Equal("failed", (await db.ForensicRequests.SingleAsync()).Status);
        Assert.Empty(db.CaseSessionVisibleAssets);
        Assert.Empty(db.CaseSessionVisibleEmails);
    }

    [Theory]
    [InlineData("input")]
    [InlineData("analysis")]
    [InlineData("outcome")]
    public async Task AmbiguousConfiguration_FailsWithoutRetryingForeverOrRevealingResults(string kind)
    {
        await StartAsync();
        var request = Created(await Controller().CreateForensicRequest(Input()));
        switch (kind)
        {
            case "input": caseData.Assets.Add(caseData.Assets[0]); break;
            case "analysis": caseData.ForensicsDefaults!.AnalysisTypes.Add(caseData.ForensicsDefaults.AnalysisTypes[0]); break;
            case "outcome": caseData.ForensicOutcomes!.Add(caseData.ForensicOutcomes[0]); break;
        }
        time.Advance(TimeSpan.FromSeconds(15));
        Assert.False(await Processor().ProcessAsync(request.Id));
        Assert.Equal("failed", (await db.ForensicRequests.SingleAsync()).Status);
        Assert.False(await Processor().ProcessAsync(request.Id));
        Assert.Empty(db.CaseSessionVisibleAssets);
        Assert.Empty(db.CaseSessionVisibleEmails);
    }

    [Fact]
    public async Task AmbiguousAnalysisType_IsRejectedBeforeCreatingARequest()
    {
        await StartAsync();
        caseData.ForensicsDefaults!.AnalysisTypes.Add(caseData.ForensicsDefaults.AnalysisTypes[0]);
        var result = Assert.IsType<ObjectResult>((await Controller().CreateForensicRequest(Input())).Result);
        Assert.Equal(500, result.StatusCode);
        Assert.Empty(db.ForensicRequests);
    }

    [Fact]
    public async Task ClientCannotCompleteOrReplaceResultFields()
    {
        await StartAsync();
        var request = Created(await Controller().CreateForensicRequest(Input()));
        var forged = new ForensicRequest
        {
            Id = request.Id, CaseId = CaseId, Status = "completed", CompletedAt = time.GetUtcNow().UtcDateTime,
            ResultDocumentId = "asset.report"
        };
        Assert.IsType<BadRequestObjectResult>(await Controller().UpdateForensicRequest(CaseId, request.Id, forged));
        Assert.Equal("pending", request.Status);
    }

    [Fact]
    public async Task CancellationDoesNotDeleteHistoryOrReleaseResults()
    {
        await StartAsync();
        var request = Created(await Controller().CreateForensicRequest(Input()));
        Assert.IsType<NoContentResult>(await Controller().DeleteForensicRequest(CaseId, request.Id));
        time.Advance(TimeSpan.FromDays(1));
        Assert.False(await Processor().ProcessAsync(request.Id));
        Assert.Equal("cancelled", (await db.ForensicRequests.SingleAsync()).Status);
        Assert.Empty(db.CaseSessionVisibleAssets);
    }

    [Fact]
    public async Task TimeEndpointUsesServerClock_NotClientSuppliedElapsedTime()
    {
        await StartAsync();
        caseData.TemporalEvents = new() { new()
        {
            Id = "event.future", TriggerAtMinutes = 5, Type = "reveal",
            Payload = new() { AssetId = "asset.final", EmailId = "email.lab", Message = "Timed message" }
        }};
        var controller = new CaseTriggersController(db, rules, storage.Object, NullLogger<CaseTriggersController>.Instance, clock, sanitizer);
        Authenticate(controller);
        await controller.AdvanceTime(CaseId, new(int.MaxValue), default);
        Assert.Empty(db.CaseSessionVisibleAssets);
        time.Advance(TimeSpan.FromSeconds(5));
        await controller.AdvanceTime(CaseId, new(0), default);
        await controller.AdvanceTime(CaseId, new(0), default);
        Assert.Equal(1, await db.CaseSessionVisibleAssets.CountAsync(v => v.AssetId == "asset.final"));
        Assert.Equal(1, await db.CaseSessionVisibleEmails.CountAsync(v => v.EmailId == "email.lab"));
        var session = await db.CaseSessions.SingleAsync();
        var notifications = JsonSerializer.Deserialize<List<RulesEngineService.SessionNotification>>(session.Notifications!);
        Assert.Equal("Timed message", Assert.Single(notifications!).Message);
    }

    [Fact]
    public async Task StartSessionAgain_RetainsClockAndRuleHistory()
    {
        var session = await StartAsync();
        await rules.EvaluateAndApplyAsync(CaseId, UserId, new EmailOpenedTrigger("email.briefing"));
        time.Advance(TimeSpan.FromSeconds(7));
        var result = Assert.IsType<OkObjectResult>(await SessionController().StartSession(new() { CaseId = CaseId }));
        var dto = Assert.IsType<CaseSessionDto>(result.Value);
        Assert.Equal(session.Id, dto.Id);
        Assert.Equal(7, dto.GameClock!.ElapsedMinutes);
        Assert.Contains("email.briefing", session.ObservedTriggers);
        Assert.Equal(1, await db.CaseSessions.CountAsync());
    }

    [Fact]
    public async Task SessionEndpoints_PauseAndResumeIdempotentlyWithoutDiscardingMinutes()
    {
        var session = await StartAsync();
        time.Advance(TimeSpan.FromSeconds(7));
        var ended = Assert.IsType<OkObjectResult>(await SessionController().EndSession(CaseId,
            new() { GameTimeAtEnd = "2099-01-01T00:00:00Z" }));
        Assert.Equal(7, Assert.IsType<CaseSessionDto>(ended.Value).GameClock!.ElapsedMinutes);
        time.Advance(TimeSpan.FromDays(2));
        await SessionController().ResumeSession(CaseId);
        time.Advance(TimeSpan.FromSeconds(3));
        var repeated = Assert.IsType<OkObjectResult>(await SessionController().ResumeSession(CaseId));
        var dto = Assert.IsType<CaseSessionDto>(repeated.Value);
        Assert.Equal(session.Id, dto.Id);
        Assert.Equal(10, dto.GameClock!.ElapsedMinutes);
        var last = Assert.IsType<OkObjectResult>(await SessionController().GetLastSession(CaseId));
        Assert.Equal(10, Assert.IsType<CaseSessionDto>(last.Value).GameClock!.ElapsedMinutes);
        Assert.Equal(1, await db.CaseSessions.CountAsync());
    }

    [Fact]
    public async Task LatestCompletedSession_DoesNotResumeAnOlderPausedSessionOrReopenOnPause()
    {
        var older = await StartAsync();
        older.Status = SessionStatus.Paused;
        var latest = new CaseSession
        {
            CaseId = CaseId, UserId = UserId, SessionStart = older.SessionStart.AddMinutes(1),
            Status = SessionStatus.Completed, IsActive = false
        };
        clock.Initialize(latest, CaseGameClock.Parse(caseData.Metadata.OpenedAt));
        db.CaseSessions.Add(latest);
        await db.SaveChangesAsync();
        Assert.IsType<ConflictObjectResult>(await SessionController().ResumeSession(CaseId));
        Assert.Equal(SessionStatus.Paused, older.Status);
        var paused = Assert.IsType<OkObjectResult>(await SessionController().PauseClock(CaseId));
        var dto = Assert.IsType<CaseSessionDto>(paused.Value);
        Assert.Equal(latest.Id, dto.Id);
        Assert.Equal(SessionStatus.Completed, dto.Status);
        Assert.False(dto.GameClock!.IsRunning);
    }

    [Fact]
    public async Task MalformedPersistedNotifications_AreNotSilentlyDropped()
    {
        var session = await StartAsync();
        session.Notifications = "{broken";
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<JsonException>(() => sanitizer.SanitizeForUserAsync(caseData, UserId, CaseId));
    }

    [Fact]
    public async Task CompletedInvestigation_CanSubmitRealEvidenceAndPerformedAnalysis()
    {
        await StartAsync();
        caseData.Solution = new()
        {
            CulpritId = "suspect.one", RequiredEvidenceIds = new() { "asset.report" },
            RequiredAnalysisIds = new() { "asset.phone:Digital" }, MinimumScore = 1,
            PartialCreditRules = new() { CulpritWeight = 0.4, EvidenceWeight = 0.3, AnalysisWeight = 0.3, QuestionsWeight = 0 }
        };
        var solution = new SolutionService(db, storage.Object,
            new PromotionService(db, NullLogger<PromotionService>.Instance), NullLogger<SolutionService>.Instance);
        var submission = new SubmitCaseRequest("suspect.one", new[] { "asset.report" }, new[] { "asset.phone:Digital" }, Array.Empty<AnswerDto>());
        var premature = await solution.SubmitAsync(CaseId, UserId, submission);
        Assert.False(premature.Correct);
        Assert.Equal(0, premature.Breakdown.EvidenceScore);
        Assert.Equal(0, premature.Breakdown.AnalysisScore);
        var request = Created(await Controller().CreateForensicRequest(Input()));
        time.Advance(TimeSpan.FromSeconds(15));
        Assert.True(await Processor().ProcessAsync(request.Id));
        var completed = await solution.SubmitAsync(CaseId, UserId, submission);
        Assert.True(completed.Correct);
        Assert.Equal(1, completed.Score);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NestedAndOr_AccumulatesAcrossCallsAndReopenedContext(bool reverse)
    {
        await StartAsync();
        caseData.Rules = new() { new()
        {
            RuleId = "rule.nested", Trigger = new()
            {
                Type = "multiple_conditions", Operator = "AND", Conditions = new()
                {
                    new() { Type = "asset_viewed", AssetId = "asset.phone" },
                    new() { Type = "multiple_conditions", Operator = "OR", Conditions = new()
                    {
                        new() { Type = "email_opened", EmailId = "email.briefing" },
                        new() { Type = "suspect_viewed", SuspectId = "suspect.other" }
                    }}
                }
            },
            Actions = new()
            {
                new() { Type = "reveal_asset", AssetId = "asset.final" },
                new() { Type = "send_notification", Message = "Matched both events" }
            }
        }};
        RuleTrigger first = reverse ? new EmailOpenedTrigger("email.briefing") : new AssetViewedTrigger("asset.phone");
        RuleTrigger second = reverse ? new AssetViewedTrigger("asset.phone") : new EmailOpenedTrigger("email.briefing");
        await rules.EvaluateAndApplyAsync(CaseId, UserId, first);
        Assert.Empty(db.CaseSessionVisibleAssets);
        db.ChangeTracker.Clear();
        var reopened = new RulesEngineService(db, storage.Object, NullLogger<RulesEngineService>.Instance);
        await reopened.EvaluateAndApplyAsync(CaseId, UserId, second);
        await reopened.EvaluateAndApplyAsync(CaseId, UserId, second);
        Assert.Single(db.CaseSessionVisibleAssets);
        var session = await db.CaseSessions.SingleAsync();
        Assert.Single(JsonSerializer.Deserialize<List<RulesEngineService.SessionNotification>>(session.Notifications!)!);
    }

    [Fact]
    public async Task AttachmentOverride_UsesRealStorageAndPersistsDownloadTriggerWithoutDuplicates()
    {
        var session = await StartAsync();
        session.EmailAttachmentOverrides = """{"email.briefing":["asset.report"]}""";
        db.CaseSessionVisibleEmails.Add(new() { UserId = UserId, CaseId = CaseId, EmailId = "email.briefing" });
        await db.SaveChangesAsync();
        storage.Setup(s => s.GetAssetStreamAsync(CaseId, "asset.report", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream("%PDF-1.7\nsynthetic test\n%%EOF"u8.ToArray()));
        storage.Setup(s => s.GetAssetContentTypeAsync(CaseId, "asset.report", It.IsAny<CancellationToken>()))
            .ReturnsAsync("application/pdf");
        caseData.Assets.Single(a => a.Id == "asset.report").Uri = $"case://{CaseId}/assets/bundle/report.pdf";
        caseData.Rules = new() { new()
        {
            RuleId = "rule.download", Trigger = new()
            {
                Type = "multiple_conditions", Operator = "AND", Conditions = new()
                {
                    new() { Type = "attachment_download", EmailId = "email.briefing", AssetId = "asset.report" },
                    new() { Type = "asset_viewed", AssetId = "asset.phone" }
                }
            },
            Actions = new() { new() { Type = "reveal_asset", AssetId = "asset.final" } }
        }};
        var first = Assert.IsType<FileStreamResult>(await EmailController().DownloadEmailAttachment(CaseId, "email.briefing", "asset.report"));
        Assert.Equal("report.pdf", first.FileDownloadName);
        Assert.Equal("application/pdf", first.ContentType);
        await first.FileStream.DisposeAsync();
        Assert.True(await db.CaseSessionVisibleAssets.AnyAsync(a => a.AssetId == "asset.report"));
        await rules.EvaluateAndApplyAsync(CaseId, UserId, new AssetViewedTrigger("asset.phone"));
        var second = Assert.IsType<FileStreamResult>(await EmailController().DownloadEmailAttachment(CaseId, "email.briefing", "asset.report"));
        await second.FileStream.DisposeAsync();
        Assert.Equal(1, await db.EmailAttachmentsDownloaded.CountAsync());
        Assert.Equal(1, await db.CaseSessionVisibleAssets.CountAsync(a => a.AssetId == "asset.final"));
    }

    [Fact]
    public async Task InvisibleAssetView_IsRejectedBeforeRecordingACondition()
    {
        var session = await StartAsync();
        var controller = new CaseTriggersController(db, rules, storage.Object,
            NullLogger<CaseTriggersController>.Instance, clock, sanitizer);
        Authenticate(controller);
        var result = Assert.IsType<ObjectResult>(await controller.ViewAsset(CaseId, "asset.report", default));
        Assert.Equal(403, result.StatusCode);
        Assert.Null(session.ObservedTriggers);
    }

    [Fact]
    public async Task PhysicalTypeCompatibility_IsPreservedForExistingCaseFiles()
    {
        await StartAsync();
        caseData.Assets.Single(a => a.Id == "asset.phone").Metadata = new()
        {
            ["physicalType"] = JsonSerializer.SerializeToElement("hair")
        };
        caseData.ForensicsDefaults!.AnalysisTypes.Add(new()
        {
            Type = "DNA", DurationMinutes = 25, AvailableFor = new() { "hair" }
        });
        var request = Created(await Controller().CreateForensicRequest(Input(analysis: "DNA")));
        time.Advance(TimeSpan.FromSeconds(25));
        Assert.True(await Processor().ProcessAsync(request.Id));
        Assert.Equal("completed", request.Status);
    }

    [Fact]
    public async Task AllInitialMode_ReturnsHiddenFlaggedEntitiesAsAuthorizedWithoutChangingTheirFlags()
    {
        await StartAsync();
        caseData.Metadata.UnlockMode = "all_initial";
        var visible = await sanitizer.SanitizeForUserAsync(caseData, UserId, CaseId);
        Assert.Contains(visible.Assets, a => a.Id == "asset.report" && a.Visibility == "hidden");
        Assert.Contains(visible.Emails, e => e.Id == "email.lab");
    }

    private static CaseV2 BuildCase() => new()
    {
        CaseId = CaseId,
        Metadata = new() { Title = "Clock test", OpenedAt = "2024-01-01T08:00:00Z", UnlockMode = "gated" },
        Assets = new()
        {
            new() { Id = "asset.phone", Title = "Phone", Type = "digital", Visibility = "initial" },
            new() { Id = "asset.report", Title = "Report", Type = "pdf", Visibility = "hidden" },
            new() { Id = "asset.final", Title = "Final clue", Type = "document", Visibility = "hidden" }
        },
        Emails = new()
        {
            new() { Id = "email.briefing", Visibility = "initial" },
            new() { Id = "email.lab", Visibility = "hidden", Attachments = new() { "asset.report" } }
        },
        ForensicsDefaults = new()
        {
            AnalysisTypes = new()
            {
                new() { Type = "Digital", DurationMinutes = 15, AvailableFor = new() { "digital" } },
                new() { Type = "Toxicology", DurationMinutes = 30, AvailableFor = new() { "digital" } }
            },
            NoFindingsEmail = new() { From = "Lab", Subject = "No findings", Template = "{{analysisType}} on {{assetName}}: no findings" }
        },
        ForensicOutcomes = new() { new()
        {
            InputAssetId = "asset.phone", AnalysisType = "Digital", Findings = true,
            ResultAssetId = "asset.report", ResultEmailId = "email.lab"
        }},
        Rules = new() { new()
        {
            RuleId = "rule.after_report", Trigger = new()
            {
                Type = "multiple_conditions", Operator = "AND", Conditions = new()
                {
                    new() { Type = "forensics_complete", InputAssetId = "asset.phone", AnalysisType = "Digital" },
                    new() { Type = "asset_viewed", AssetId = "asset.report" }
                }
            },
            Actions = new() { new() { Type = "reveal_asset", AssetId = "asset.final" } }
        }}
    };

    public void Dispose() => db.Dispose();
}
