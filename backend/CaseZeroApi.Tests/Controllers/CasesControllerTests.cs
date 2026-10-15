using System.Security.Claims;
using CaseZeroApi.Controllers;
using CaseZeroApi.Data;
using CaseZeroApi.Models.CaseV2;
using CaseZeroApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CaseZeroApi.Tests.Controllers;

public class CasesControllerTests
{
    [Theory]
    [InlineData("gated", "Rookie", true)]
    [InlineData("all_initial", "Detective", true)]
    [InlineData("gated", "Detective", false)]
    public async Task GetCase_SeedsDownloadAuthorizationConsistentlyWithSanitizedVisibility(
        string unlockMode, string requiredRank, bool unlockAll)
    {
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var raw = new CaseV2
        {
            CaseId = "case_visibility",
            Metadata = new() { UnlockMode = unlockMode, RequiredRank = requiredRank },
            Assets = [new() { Id = "asset.hidden", Visibility = "hidden" }],
            Emails = [new() { Id = "email.hidden", Visibility = "hidden" }],
            Suspects = [new() { Id = "suspect.hidden", Visibility = "hidden" }]
        };
        var sanitizer = new CaseV2SanitizerService(context, NullLogger<CaseV2SanitizerService>.Instance);
        var storage = new Mock<ICaseV2StorageService>();
        storage.Setup(s => s.GetRawAsync(raw.CaseId, It.IsAny<CancellationToken>())).ReturnsAsync(raw);
        storage.Setup(s => s.GetForUserAsync(raw.CaseId, "player", It.IsAny<CancellationToken>()))
            .Returns(async (string caseId, string userId, CancellationToken ct) =>
                (CaseV2Sanitized?)await sanitizer.SanitizeForUserAsync(raw, userId, caseId, ct));
        var controller = new CasesController(storage.Object, Mock.Of<ISolutionService>(), context,
            NullLogger<CasesController>.Instance)
        {
            ControllerContext = new()
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, "player")], "test"))
                }
            }
        };

        var response = await controller.GetCase(raw.CaseId, default);
        var data = Assert.IsType<CaseV2Sanitized>(Assert.IsType<OkObjectResult>(response.Result).Value);

        Assert.Equal(unlockAll, data.Assets.Any(a => a.Id == "asset.hidden"));
        Assert.Equal(unlockAll, data.Emails.Any(e => e.Id == "email.hidden"));
        Assert.Equal(unlockAll, data.Suspects.Any(s => s.Id == "suspect.hidden"));
        Assert.Equal(unlockAll, await context.CaseSessionVisibleAssets.AnyAsync(a => a.AssetId == "asset.hidden"));
        Assert.Equal(unlockAll, await context.CaseSessionVisibleEmails.AnyAsync(e => e.EmailId == "email.hidden"));
        Assert.Single(context.CaseSessions);
    }
}
