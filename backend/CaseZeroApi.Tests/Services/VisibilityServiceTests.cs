using CaseZeroApi.Data;
using CaseZeroApi.Services;
using CaseV2Models = CaseZeroApi.Models.CaseV2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CaseZeroApi.Tests.Services;

/// <summary>
/// Tests for <see cref="VisibilityService"/> against the v2 storage layer.
///
/// VisibilityService applies initial-visibility rules using
/// <see cref="ICaseV2StorageService"/>. It unlocks every asset/email with
/// <c>visibility == "initial"</c> for a (user, case) tuple, and exposes
/// primitive unlock + check operations that the rules engine uses for
/// gated content.
/// </summary>
public class VisibilityServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ICaseV2StorageService> _caseStorageMock;
    private readonly VisibilityService _sut;

    private const string UserId = "user-vis-test";
    private const string CaseId = "case-vis-test";

    public VisibilityServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _caseStorageMock = new Mock<ICaseV2StorageService>();
        _sut = new VisibilityService(_context, _caseStorageMock.Object, NullLogger<VisibilityService>.Instance);
    }

    private static CaseV2Models.CaseV2 BuildV2Case(
        string requiredRank = "Rookie",
        string? unlockMode = null,
        int assetInitialCount = 2,
        int assetHiddenCount = 1,
        int emailInitialCount = 1,
        int emailHiddenCount = 1)
    {
        var assets = Enumerable.Range(1, assetInitialCount)
            .Select(i => new CaseV2Models.CaseV2Asset { Id = $"asset.initial_{i}", Title = $"Init Asset {i}", Type = "document", Uri = "x", Visibility = "initial" })
            .Concat(Enumerable.Range(1, assetHiddenCount)
            .Select(i => new CaseV2Models.CaseV2Asset { Id = $"asset.hidden_{i}", Title = $"Hidden Asset {i}", Type = "document", Uri = "x", Visibility = "hidden" }))
            .ToList();

        var emails = Enumerable.Range(1, emailInitialCount)
            .Select(i => new CaseV2Models.CaseV2Email { Id = $"email.initial_{i}", From = "a@b.com", Subject = $"S{i}", Body = $"B{i}", SentAt = "2024-01-01", Visibility = "initial" })
            .Concat(Enumerable.Range(1, emailHiddenCount)
            .Select(i => new CaseV2Models.CaseV2Email { Id = $"email.hidden_{i}", From = "x@y.com", Subject = $"H{i}", Body = $"B{i}", SentAt = "2024-01-02", Visibility = "hidden" }))
            .ToList();

        return new CaseV2Models.CaseV2
        {
            CaseId = CaseId,
            Metadata = new CaseV2Models.CaseV2Metadata { Title = "Vis Test", RequiredRank = requiredRank, UnlockMode = unlockMode ?? "all_initial" },
            Assets = assets,
            Emails = emails,
            Suspects = new List<CaseV2Models.CaseV2Suspect>()
        };
    }

    private void SetupCase(CaseV2Models.CaseV2 caseData) =>
        _caseStorageMock.Setup(s => s.GetRawAsync(CaseId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(caseData);

    [Fact]
    public async Task ApplyInitialRules_Rookie_UnlocksAllInitialAssets()
    {
        SetupCase(BuildV2Case(requiredRank: "Rookie", assetInitialCount: 2, assetHiddenCount: 1));
        var (assets, _) = await _sut.ApplyInitialRulesAsync(UserId, CaseId);
        Assert.Equal(2, assets);
    }

    [Fact]
    public async Task ApplyInitialRules_Rookie_UnlocksAllInitialEmails()
    {
        SetupCase(BuildV2Case(requiredRank: "Rookie", emailInitialCount: 3, emailHiddenCount: 1));
        var (_, emails) = await _sut.ApplyInitialRulesAsync(UserId, CaseId);
        Assert.Equal(3, emails);
    }

    [Fact]
    public async Task ApplyInitialRules_DoesNotUnlockHiddenAssets()
    {
        SetupCase(BuildV2Case(assetInitialCount: 1, assetHiddenCount: 2));
        await _sut.ApplyInitialRulesAsync(UserId, CaseId);
        Assert.False(await _sut.IsAssetVisibleAsync(UserId, CaseId, "asset.hidden_1"));
    }

    [Fact]
    public async Task ApplyInitialRules_DoesNotUnlockHiddenEmails()
    {
        SetupCase(BuildV2Case(emailInitialCount: 1, emailHiddenCount: 2));
        await _sut.ApplyInitialRulesAsync(UserId, CaseId);
        Assert.False(await _sut.IsEmailVisibleAsync(UserId, CaseId, "email.hidden_1"));
    }

    [Fact]
    public async Task ApplyInitialRules_CalledTwice_IsIdempotent()
    {
        SetupCase(BuildV2Case(assetInitialCount: 2));
        await _sut.ApplyInitialRulesAsync(UserId, CaseId);
        var (assets2, _) = await _sut.ApplyInitialRulesAsync(UserId, CaseId);
        Assert.Equal(0, assets2);

        var total = await _context.CaseSessionVisibleAssets
            .CountAsync(v => v.UserId == UserId && v.CaseId == CaseId);
        Assert.Equal(2, total);
    }

    [Fact]
    public async Task Gated_HiddenAsset_NotVisible_UntilUnlocked()
    {
        SetupCase(BuildV2Case(assetInitialCount: 1, assetHiddenCount: 1));
        await _sut.ApplyInitialRulesAsync(UserId, CaseId);

        Assert.False(await _sut.IsAssetVisibleAsync(UserId, CaseId, "asset.hidden_1"));
        await _sut.UnlockAssetAsync(UserId, CaseId, "asset.hidden_1");
        Assert.True(await _sut.IsAssetVisibleAsync(UserId, CaseId, "asset.hidden_1"));
    }

    [Fact]
    public async Task Gated_HiddenEmail_NotVisible_UntilUnlocked()
    {
        SetupCase(BuildV2Case(emailInitialCount: 1, emailHiddenCount: 1));
        await _sut.ApplyInitialRulesAsync(UserId, CaseId);

        Assert.False(await _sut.IsEmailVisibleAsync(UserId, CaseId, "email.hidden_1"));
        await _sut.UnlockEmailAsync(UserId, CaseId, "email.hidden_1");
        Assert.True(await _sut.IsEmailVisibleAsync(UserId, CaseId, "email.hidden_1"));
    }

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

    [Fact]
    public async Task IsAssetVisible_ReturnsFalse_WhenNotUnlocked()
    {
        Assert.False(await _sut.IsAssetVisibleAsync(UserId, CaseId, "asset.nonexistent"));
    }

    [Fact]
    public async Task IsEmailVisible_ReturnsFalse_WhenNotUnlocked()
    {
        Assert.False(await _sut.IsEmailVisibleAsync(UserId, CaseId, "email.nonexistent"));
    }

    [Fact]
    public async Task IsAssetVisible_ReturnsTrue_AfterUnlock()
    {
        await _sut.UnlockAssetAsync(UserId, CaseId, "asset.z");
        Assert.True(await _sut.IsAssetVisibleAsync(UserId, CaseId, "asset.z"));
    }

    [Fact]
    public async Task ApplyInitialRules_CaseNotFound_ReturnsZeroZero()
    {
        _caseStorageMock.Setup(s => s.GetRawAsync(CaseId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync((CaseV2Models.CaseV2?)null);

        var (assets, emails) = await _sut.ApplyInitialRulesAsync(UserId, CaseId);
        Assert.Equal(0, assets);
        Assert.Equal(0, emails);
    }

    public void Dispose() => _context.Dispose();
}
