using CaseZeroApi.Data;
using CaseZeroApi.Models;
using CaseZeroApi.Services;
using CaseV1Models = CaseZeroApi.Models.CaseV1;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CaseZeroApi.Tests.Services;

/// <summary>
/// Tests for VisibilityService.
///
/// VisibilityService applies v1 initial-visibility rules using ICaseV1StorageService.
/// The analogous v2 "gated" vs "all_initial" behaviour (filtering by unlockMode) is
/// implemented in CaseV2SanitizerService (tested separately); here we verify the
/// VisibilityService primitive operations that underpin both modes:
///
///  - "all_initial" semantics  = ApplyInitialRulesAsync unlocks every entity whose
///    visibility == "initial"; after that call every such entity is visible.
///  - "Rookie" / no unlockMode = same as all_initial for the initial set (no restriction).
///  - "gated" semantics        = after ApplyInitialRulesAsync only "initial" entities
///    are visible; hidden ones are NOT, unless individually unlocked via UnlockAssetAsync /
///    UnlockEmailAsync.
/// </summary>
public class VisibilityServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ICaseV1StorageService> _caseStorageMock;
    private readonly VisibilityService _sut;

    private const string UserId = "user-vis-test";
    private const string CaseId = "case-vis-test";

    public VisibilityServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _caseStorageMock = new Mock<ICaseV1StorageService>();
        _sut = new VisibilityService(_context, _caseStorageMock.Object, NullLogger<VisibilityService>.Instance);
    }

    // ──────────────────────────────────────────────────────────────
    // Helper builders
    // ──────────────────────────────────────────────────────────────

    private static CaseV1Models.CaseV1 BuildV1Case(
        string requiredRank = "Rookie",
        string? unlockMode = null,
        int assetInitialCount = 2,
        int assetHiddenCount = 1,
        int emailInitialCount = 1,
        int emailHiddenCount = 1)
    {
        var assets = Enumerable.Range(1, assetInitialCount)
            .Select(i => new CaseV1Models.Asset { AssetId = $"asset.initial_{i}", Name = $"Init Asset {i}", Visibility = "initial" })
            .Concat(Enumerable.Range(1, assetHiddenCount)
            .Select(i => new CaseV1Models.Asset { AssetId = $"asset.hidden_{i}", Name = $"Hidden Asset {i}", Visibility = "hidden" }))
            .ToList();

        var emails = Enumerable.Range(1, emailInitialCount)
            .Select(i => new CaseV1Models.Email { EmailId = $"email.initial_{i}", From = "a@b.com", Subject = $"S{i}", Content = $"B{i}", SentAt = "2024-01-01", Visibility = "initial" })
            .Concat(Enumerable.Range(1, emailHiddenCount)
            .Select(i => new CaseV1Models.Email { EmailId = $"email.hidden_{i}", From = "x@y.com", Subject = $"H{i}", Content = $"B{i}", SentAt = "2024-01-02", Visibility = "hidden" }))
            .ToList();

        return new CaseV1Models.CaseV1
        {
            CaseId = CaseId,
            Metadata = new CaseV1Models.CaseMetadata { Title = "Vis Test", RequiredRank = requiredRank },
            Assets = assets,
            Emails = emails,
            Suspects = new List<CaseV1Models.Suspect>()
        };
    }

    private void SetupCase(CaseV1Models.CaseV1 caseData) =>
        _caseStorageMock.Setup(s => s.GetCaseAsync(CaseId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(caseData);

    // ──────────────────────────────────────────────────────────────
    // "all_initial" / "Rookie" behaviour
    // ApplyInitialRulesAsync unlocks all visibility=="initial" entities
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApplyInitialRules_Rookie_UnlocksAllInitialAssets()
    {
        SetupCase(BuildV1Case(requiredRank: "Rookie", assetInitialCount: 2, assetHiddenCount: 1));

        var (assets, _) = await _sut.ApplyInitialRulesAsync(UserId, CaseId);

        Assert.Equal(2, assets);
    }

    [Fact]
    public async Task ApplyInitialRules_Rookie_UnlocksAllInitialEmails()
    {
        SetupCase(BuildV1Case(requiredRank: "Rookie", emailInitialCount: 3, emailHiddenCount: 1));

        var (_, emails) = await _sut.ApplyInitialRulesAsync(UserId, CaseId);

        Assert.Equal(3, emails);
    }

    [Fact]
    public async Task ApplyInitialRules_DoesNotUnlockHiddenAssets()
    {
        SetupCase(BuildV1Case(assetInitialCount: 1, assetHiddenCount: 2));
        await _sut.ApplyInitialRulesAsync(UserId, CaseId);

        var isHiddenVisible = await _sut.IsAssetVisibleAsync(UserId, CaseId, "asset.hidden_1");
        Assert.False(isHiddenVisible);
    }

    [Fact]
    public async Task ApplyInitialRules_DoesNotUnlockHiddenEmails()
    {
        SetupCase(BuildV1Case(emailInitialCount: 1, emailHiddenCount: 2));
        await _sut.ApplyInitialRulesAsync(UserId, CaseId);

        var isHiddenVisible = await _sut.IsEmailVisibleAsync(UserId, CaseId, "email.hidden_1");
        Assert.False(isHiddenVisible);
    }

    // ──────────────────────────────────────────────────────────────
    // Idempotency — calling ApplyInitialRulesAsync twice doesn't duplicate
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApplyInitialRules_CalledTwice_IsIdempotent()
    {
        SetupCase(BuildV1Case(assetInitialCount: 2));

        await _sut.ApplyInitialRulesAsync(UserId, CaseId);
        var (assets2, _) = await _sut.ApplyInitialRulesAsync(UserId, CaseId);

        Assert.Equal(0, assets2); // already unlocked → 0 newly unlocked

        var total = await _context.CaseSessionVisibleAssets
            .CountAsync(v => v.UserId == UserId && v.CaseId == CaseId);
        Assert.Equal(2, total);
    }

    // ──────────────────────────────────────────────────────────────
    // "gated" behaviour — hidden entities require explicit unlock
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Gated_HiddenAsset_NotVisible_UntilUnlocked()
    {
        SetupCase(BuildV1Case(assetInitialCount: 1, assetHiddenCount: 1));
        await _sut.ApplyInitialRulesAsync(UserId, CaseId);

        var beforeUnlock = await _sut.IsAssetVisibleAsync(UserId, CaseId, "asset.hidden_1");
        Assert.False(beforeUnlock);

        await _sut.UnlockAssetAsync(UserId, CaseId, "asset.hidden_1");

        var afterUnlock = await _sut.IsAssetVisibleAsync(UserId, CaseId, "asset.hidden_1");
        Assert.True(afterUnlock);
    }

    [Fact]
    public async Task Gated_HiddenEmail_NotVisible_UntilUnlocked()
    {
        SetupCase(BuildV1Case(emailInitialCount: 1, emailHiddenCount: 1));
        await _sut.ApplyInitialRulesAsync(UserId, CaseId);

        var beforeUnlock = await _sut.IsEmailVisibleAsync(UserId, CaseId, "email.hidden_1");
        Assert.False(beforeUnlock);

        await _sut.UnlockEmailAsync(UserId, CaseId, "email.hidden_1");

        var afterUnlock = await _sut.IsEmailVisibleAsync(UserId, CaseId, "email.hidden_1");
        Assert.True(afterUnlock);
    }

    // ──────────────────────────────────────────────────────────────
    // Unlock operations are also idempotent
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task UnlockAsset_CalledTwice_NoDuplicates()
    {
        await _sut.UnlockAssetAsync(UserId, CaseId, "asset.extra_1");
        await _sut.UnlockAssetAsync(UserId, CaseId, "asset.extra_1");

        var count = await _context.CaseSessionVisibleAssets
            .CountAsync(v => v.UserId == UserId && v.CaseId == CaseId && v.AssetId == "asset.extra_1");
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task UnlockEmail_CalledTwice_NoDuplicates()
    {
        await _sut.UnlockEmailAsync(UserId, CaseId, "email.extra_1");
        await _sut.UnlockEmailAsync(UserId, CaseId, "email.extra_1");

        var count = await _context.CaseSessionVisibleEmails
            .CountAsync(v => v.UserId == UserId && v.CaseId == CaseId && v.EmailId == "email.extra_1");
        Assert.Equal(1, count);
    }

    // ──────────────────────────────────────────────────────────────
    // IsAssetVisibleAsync / IsEmailVisibleAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task IsAssetVisible_ReturnsFalse_WhenNotUnlocked()
    {
        var visible = await _sut.IsAssetVisibleAsync(UserId, CaseId, "asset.nonexistent");
        Assert.False(visible);
    }

    [Fact]
    public async Task IsEmailVisible_ReturnsFalse_WhenNotUnlocked()
    {
        var visible = await _sut.IsEmailVisibleAsync(UserId, CaseId, "email.nonexistent");
        Assert.False(visible);
    }

    [Fact]
    public async Task IsAssetVisible_ReturnsTrue_AfterUnlock()
    {
        await _sut.UnlockAssetAsync(UserId, CaseId, "asset.z");
        Assert.True(await _sut.IsAssetVisibleAsync(UserId, CaseId, "asset.z"));
    }

    // ──────────────────────────────────────────────────────────────
    // Case not found → returns (0, 0) without crash
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApplyInitialRules_CaseNotFound_ReturnsZeroZero()
    {
        _caseStorageMock.Setup(s => s.GetCaseAsync(CaseId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync((CaseV1Models.CaseV1?)null);

        var (assets, emails) = await _sut.ApplyInitialRulesAsync(UserId, CaseId);

        Assert.Equal(0, assets);
        Assert.Equal(0, emails);
    }

    public void Dispose() => _context.Dispose();
}
