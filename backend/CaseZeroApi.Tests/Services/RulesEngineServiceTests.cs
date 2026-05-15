using System.Text.Json;
using CaseZeroApi.Data;
using CaseZeroApi.Models;
using CaseZeroApi.Models.CaseV2;
using CaseZeroApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CaseZeroApi.Tests.Services;

public class RulesEngineServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ICaseV2StorageService> _v2StorageMock;
    private readonly RulesEngineService _sut;

    private const string UserId = "user-rules-test";
    private const string CaseId = "case-rules-test";

    public RulesEngineServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _v2StorageMock = new Mock<ICaseV2StorageService>();

        var v1StorageMock = new Mock<ICaseV1StorageService>();
        _sut = new RulesEngineService(
            _context,
            v1StorageMock.Object,
            _v2StorageMock.Object,
            NullLogger<RulesEngineService>.Instance);
    }

    // ──────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────

    private void SetupCase(CaseV2 caseData) =>
        _v2StorageMock.Setup(s => s.GetRawAsync(CaseId, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(caseData);

    private async Task<CaseSession> CreateSessionAsync(string? firedRuleIds = null)
    {
        var s = new CaseSession
        {
            UserId = UserId,
            CaseId = CaseId,
            FiredRuleIds = firedRuleIds
        };
        _context.CaseSessions.Add(s);
        await _context.SaveChangesAsync();
        return s;
    }

    private static CaseV2 BuildCase(params CaseV2Rule[] rules) => new()
    {
        CaseId = CaseId,
        Metadata = new CaseV2Metadata { Title = "T" },
        Rules = rules.ToList()
    };

    // ──────────────────────────────────────────────────────────────
    // forensics_complete → reveal_email
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ForensicsComplete_RevealEmail_PersistsToVisibleEmails()
    {
        SetupCase(BuildCase(new CaseV2Rule
        {
            RuleId = "r-fe-1",
            Trigger = new CaseV2Trigger { Type = "forensics_complete", InputAssetId = "asset.phone", AnalysisType = "Digital" },
            Actions = new List<CaseV2Action> { new() { Type = "reveal_email", EmailId = "email.lab_report" } }
        }));
        await CreateSessionAsync();

        await _sut.EvaluateAndApplyAsync(CaseId, UserId,
            new ForensicsCompleteTrigger("asset.phone", "Digital"));

        var visible = await _context.CaseSessionVisibleEmails
            .AnyAsync(v => v.UserId == UserId && v.CaseId == CaseId && v.EmailId == "email.lab_report");
        Assert.True(visible);
    }

    // ──────────────────────────────────────────────────────────────
    // asset_viewed → reveal_suspect
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task AssetViewed_RevealSuspect_WritesToRevealedSuspectIds()
    {
        SetupCase(BuildCase(new CaseV2Rule
        {
            RuleId = "r-av-1",
            Trigger = new CaseV2Trigger { Type = "asset_viewed", AssetId = "asset.wallet" },
            Actions = new List<CaseV2Action> { new() { Type = "reveal_suspect", SuspectId = "suspect.marcus" } }
        }));
        await CreateSessionAsync();

        await _sut.EvaluateAndApplyAsync(CaseId, UserId, new AssetViewedTrigger("asset.wallet"));

        var session = await _context.CaseSessions
            .Where(s => s.UserId == UserId && s.CaseId == CaseId)
            .FirstAsync();
        var revealed = JsonSerializer.Deserialize<List<string>>(session.RevealedSuspectIds ?? "[]");
        Assert.Contains("suspect.marcus", revealed);
    }

    // ──────────────────────────────────────────────────────────────
    // email_opened → send_notification
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task EmailOpened_SendNotification_WritesToSessionNotifications()
    {
        SetupCase(BuildCase(new CaseV2Rule
        {
            RuleId = "r-eo-1",
            Trigger = new CaseV2Trigger { Type = "email_opened", EmailId = "email.assignment" },
            Actions = new List<CaseV2Action>
            {
                new() { Type = "send_notification", Level = "info", Message = "New clue found!" }
            }
        }));
        await CreateSessionAsync();

        await _sut.EvaluateAndApplyAsync(CaseId, UserId, new EmailOpenedTrigger("email.assignment"));

        var session = await _context.CaseSessions.Where(s => s.UserId == UserId && s.CaseId == CaseId).FirstAsync();
        var notifications = JsonSerializer.Deserialize<List<JsonElement>>(session.Notifications ?? "[]");
        Assert.NotNull(notifications);
        Assert.NotEmpty(notifications);
        Assert.Contains(notifications, n => n.GetProperty("Message").GetString() == "New clue found!");
    }

    // ──────────────────────────────────────────────────────────────
    // attachment_download → add_email_attachment
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task AttachmentDownload_AddEmailAttachment_UpdatesSessionOverrides()
    {
        SetupCase(BuildCase(new CaseV2Rule
        {
            RuleId = "r-ad-1",
            Trigger = new CaseV2Trigger { Type = "attachment_download", EmailId = "email.intro", AssetId = "asset.map" },
            Actions = new List<CaseV2Action>
            {
                new() { Type = "add_email_attachment", EmailId = "email.lab_report", AssetId = "asset.forensics_pdf" }
            }
        }));
        await CreateSessionAsync();

        await _sut.EvaluateAndApplyAsync(CaseId, UserId,
            new AttachmentDownloadTrigger("email.intro", "asset.map"));

        var session = await _context.CaseSessions.Where(s => s.UserId == UserId && s.CaseId == CaseId).FirstAsync();
        var overrides = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(session.EmailAttachmentOverrides ?? "{}");
        Assert.NotNull(overrides);
        Assert.True(overrides.ContainsKey("email.lab_report"));
        Assert.Contains("asset.forensics_pdf", overrides["email.lab_report"]);
    }

    // ──────────────────────────────────────────────────────────────
    // update_suspect_status
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ForensicsComplete_UpdateSuspectStatus_WritesToSuspectStatusOverrides()
    {
        SetupCase(BuildCase(new CaseV2Rule
        {
            RuleId = "r-uss-1",
            Trigger = new CaseV2Trigger { Type = "forensics_complete", InputAssetId = "asset.dna", AnalysisType = "DNA" },
            Actions = new List<CaseV2Action>
            {
                new() { Type = "update_suspect_status", SuspectId = "suspect.alice", Status = "confirmed_culprit" }
            }
        }));
        await CreateSessionAsync();

        await _sut.EvaluateAndApplyAsync(CaseId, UserId, new ForensicsCompleteTrigger("asset.dna", "DNA"));

        var session = await _context.CaseSessions.Where(s => s.UserId == UserId && s.CaseId == CaseId).FirstAsync();
        var statusDict = JsonSerializer.Deserialize<Dictionary<string, string>>(session.SuspectStatusOverrides ?? "{}");
        Assert.NotNull(statusDict);
        Assert.Equal("confirmed_culprit", statusDict["suspect.alice"]);
    }

    // ──────────────────────────────────────────────────────────────
    // mark_alibi_verified
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task AssetViewed_MarkAlibiVerified_WritesToSuspectAlibiVerified()
    {
        SetupCase(BuildCase(new CaseV2Rule
        {
            RuleId = "r-mav-1",
            Trigger = new CaseV2Trigger { Type = "asset_viewed", AssetId = "asset.receipt" },
            Actions = new List<CaseV2Action>
            {
                new() { Type = "mark_alibi_verified", SuspectId = "suspect.bob", Verified = true }
            }
        }));
        await CreateSessionAsync();

        await _sut.EvaluateAndApplyAsync(CaseId, UserId, new AssetViewedTrigger("asset.receipt"));

        var session = await _context.CaseSessions.Where(s => s.UserId == UserId && s.CaseId == CaseId).FirstAsync();
        var alibiDict = JsonSerializer.Deserialize<Dictionary<string, bool>>(session.SuspectAlibiVerified ?? "{}");
        Assert.NotNull(alibiDict);
        Assert.True(alibiDict["suspect.bob"]);
    }

    // ──────────────────────────────────────────────────────────────
    // time_elapsed: only fires when gameTimeMinutes >= atMinutes
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task TimeElapsed_BelowThreshold_DoesNotFire()
    {
        SetupCase(BuildCase(new CaseV2Rule
        {
            RuleId = "r-te-1",
            Trigger = new CaseV2Trigger { Type = "time_elapsed", AtMinutes = 360 },
            Actions = new List<CaseV2Action> { new() { Type = "reveal_email", EmailId = "email.late" } }
        }));
        await CreateSessionAsync();

        await _sut.EvaluateAndApplyAsync(CaseId, UserId, new TimeElapsedTrigger(100));

        var visible = await _context.CaseSessionVisibleEmails
            .AnyAsync(v => v.UserId == UserId && v.CaseId == CaseId && v.EmailId == "email.late");
        Assert.False(visible);
    }

    [Fact]
    public async Task TimeElapsed_AtOrAboveThreshold_Fires()
    {
        SetupCase(BuildCase(new CaseV2Rule
        {
            RuleId = "r-te-2",
            Trigger = new CaseV2Trigger { Type = "time_elapsed", AtMinutes = 360 },
            Actions = new List<CaseV2Action> { new() { Type = "reveal_email", EmailId = "email.late" } }
        }));
        await CreateSessionAsync();

        await _sut.EvaluateAndApplyAsync(CaseId, UserId, new TimeElapsedTrigger(360));

        var visible = await _context.CaseSessionVisibleEmails
            .AnyAsync(v => v.UserId == UserId && v.CaseId == CaseId && v.EmailId == "email.late");
        Assert.True(visible);
    }

    // ──────────────────────────────────────────────────────────────
    // multiple_conditions — AND semantics
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task MultipleConditions_AND_AllMatch_Fires()
    {
        SetupCase(BuildCase(new CaseV2Rule
        {
            RuleId = "r-mc-and-1",
            Trigger = new CaseV2Trigger
            {
                Type = "multiple_conditions",
                Operator = "AND",
                Conditions = new List<CaseV2Trigger>
                {
                    new() { Type = "asset_viewed", AssetId = "asset.phone" },
                    new() { Type = "asset_viewed", AssetId = "asset.phone" } // same trigger → both match same incoming
                }
            },
            Actions = new List<CaseV2Action> { new() { Type = "reveal_email", EmailId = "email.bonus" } }
        }));
        await CreateSessionAsync();

        await _sut.EvaluateAndApplyAsync(CaseId, UserId, new AssetViewedTrigger("asset.phone"));

        var visible = await _context.CaseSessionVisibleEmails
            .AnyAsync(v => v.UserId == UserId && v.CaseId == CaseId && v.EmailId == "email.bonus");
        Assert.True(visible);
    }

    [Fact]
    public async Task MultipleConditions_OR_AnyMatch_Fires()
    {
        SetupCase(BuildCase(new CaseV2Rule
        {
            RuleId = "r-mc-or-1",
            Trigger = new CaseV2Trigger
            {
                Type = "multiple_conditions",
                Operator = "OR",
                Conditions = new List<CaseV2Trigger>
                {
                    new() { Type = "asset_viewed", AssetId = "asset.phone" },
                    new() { Type = "asset_viewed", AssetId = "asset.wallet" }
                }
            },
            Actions = new List<CaseV2Action> { new() { Type = "reveal_email", EmailId = "email.or_result" } }
        }));
        await CreateSessionAsync();

        // Only asset.wallet is fired — matches second condition under OR
        await _sut.EvaluateAndApplyAsync(CaseId, UserId, new AssetViewedTrigger("asset.wallet"));

        var visible = await _context.CaseSessionVisibleEmails
            .AnyAsync(v => v.UserId == UserId && v.CaseId == CaseId && v.EmailId == "email.or_result");
        Assert.True(visible);
    }

    // ──────────────────────────────────────────────────────────────
    // Idempotency — same trigger fired twice → applied only once
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Idempotency_SameTriggerFiredTwice_ActionAppliedOnce()
    {
        SetupCase(BuildCase(new CaseV2Rule
        {
            RuleId = "r-idem-1",
            Trigger = new CaseV2Trigger { Type = "asset_viewed", AssetId = "asset.phone" },
            Actions = new List<CaseV2Action> { new() { Type = "reveal_email", EmailId = "email.idem" } }
        }));
        await CreateSessionAsync();

        await _sut.EvaluateAndApplyAsync(CaseId, UserId, new AssetViewedTrigger("asset.phone"));
        await _sut.EvaluateAndApplyAsync(CaseId, UserId, new AssetViewedTrigger("asset.phone"));

        var count = await _context.CaseSessionVisibleEmails
            .CountAsync(v => v.UserId == UserId && v.CaseId == CaseId && v.EmailId == "email.idem");
        Assert.Equal(1, count);
    }

    // ──────────────────────────────────────────────────────────────
    // No active session → no-op
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task NoSession_EvaluateAndApply_DoesNotThrow()
    {
        SetupCase(BuildCase(new CaseV2Rule
        {
            RuleId = "r-nosession-1",
            Trigger = new CaseV2Trigger { Type = "asset_viewed", AssetId = "asset.phone" },
            Actions = new List<CaseV2Action> { new() { Type = "reveal_email", EmailId = "email.x" } }
        }));
        // No session in DB for this user/case

        var ex = await Record.ExceptionAsync(() =>
            _sut.EvaluateAndApplyAsync(CaseId, UserId, new AssetViewedTrigger("asset.phone")));

        Assert.Null(ex);
    }

    public void Dispose() => _context.Dispose();
}
