using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using CaseZeroApi.Data;
using CaseZeroApi.Models;
using CaseZeroApi.Models.CaseV2;
using CaseZeroApi.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace CaseZeroApi.IntegrationTests;

public class NoFindingsEmailIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public NoFindingsEmailIntegrationTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task ProducedNoFindingsEmail_CanBeOpened_WithoutBypassingAuthorizationOrAttachmentValidation()
    {
        var openedAt = new DateTime(2026, 10, 14, 10, 0, 0, DateTimeKind.Utc);
        var now = new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
        var raw = new CaseV2
        {
            CaseId = $"case_no_findings_{Guid.NewGuid():N}",
            Metadata = new()
            {
                Title = "No findings fixture", OpenedAt = openedAt.ToString("o"),
                Difficulty = "Detective", RequiredRank = "Detective", UnlockMode = "gated"
            },
            Assets = [new() { Id = "asset.initial_report", Title = "Initial report", Type = "document", Visibility = "initial" }],
            Emails =
            [
                new() { Id = "email.briefing", Visibility = "initial" },
                new() { Id = "email.hidden_result", Visibility = "hidden" }
            ],
            ForensicsDefaults = new()
            {
                AnalysisTypes = [new() { Type = "Digital", DurationMinutes = 5, AvailableFor = ["document"] }],
                NoFindingsEmail = new()
                {
                    From = "laboratory@fic-police.gov", Subject = "{{analysisType}}: no findings",
                    Template = "No findings for {{assetName}} using {{analysisType}}."
                }
            },
            ForensicOutcomes =
            [
                new()
                {
                    InputAssetId = "asset.initial_report", AnalysisType = "Digital",
                    Findings = false, ResultEmailId = "email.hidden_result"
                }
            ]
        };
        var storage = new Mock<ICaseV2StorageService>();
        storage.Setup(s => s.GetRawAsync(raw.CaseId, It.IsAny<CancellationToken>())).ReturnsAsync(raw);
        using var host = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<ICaseV2StorageService>();
            services.AddSingleton(storage.Object);
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FrozenTimeProvider(now));
        }));
        using var client = host.CreateClient();
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var owner = UserFixture();
        var otherUser = UserFixture();
        context.Users.AddRange(owner, otherUser);
        context.Cases.Add(new()
        {
            Id = raw.CaseId, Title = raw.Metadata.Title, Description = "", CreatedAt = now.UtcDateTime
        });
        context.CaseSessions.Add(new()
        {
            UserId = owner.Id, CaseId = raw.CaseId, Status = SessionStatus.Active, IsActive = true,
            SessionStart = now.UtcDateTime, GameTimeAtStart = openedAt.ToString("o"),
            GameClockUpdatedAt = now.UtcDateTime, GameTimeMinutes = 5
        });
        var request = new ForensicRequest
        {
            UserId = owner.Id, CaseId = raw.CaseId, InputAssetId = "asset.initial_report",
            InputAssetName = "Initial report", AnalysisType = "Digital",
            RequestedAt = openedAt, EstimatedCompletionTime = openedAt.AddMinutes(5)
        };
        context.ForensicRequests.Add(request);
        await context.SaveChangesAsync();

        Assert.True(await scope.ServiceProvider.GetRequiredService<ForensicRequestProcessor>().ProcessAsync(request.Id));
        Assert.Equal("completed", request.Status);
        var emailId = $"forensic-{request.Id}-no-findings";
        Assert.Equal(emailId, request.ResultEmailId);
        Assert.False(await context.CaseSessionVisibleEmails.AnyAsync(e =>
            e.UserId == owner.Id && e.CaseId == raw.CaseId && e.EmailId == "email.hidden_result"));
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtService>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt.GenerateToken(owner, [UserRoles.Player]));
        var emailUrl = $"/api/cases/{raw.CaseId}/emails/{emailId}";

        using var opened = await client.PostAsync($"{emailUrl}/open", null);
        Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
        using var detail = await client.GetAsync(emailUrl);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        using var content = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        Assert.Equal(emailId, content.RootElement.GetProperty("emailId").GetString());
        Assert.Equal("laboratory@fic-police.gov", content.RootElement.GetProperty("from").GetString());
        Assert.Equal("Digital: no findings", content.RootElement.GetProperty("subject").GetString());
        Assert.Equal("No findings for Initial report using Digital.", content.RootElement.GetProperty("content").GetString());
        Assert.Equal(openedAt.AddMinutes(5), content.RootElement.GetProperty("sentAt").GetDateTime());
        Assert.True(content.RootElement.GetProperty("isRead").GetBoolean());
        Assert.Equal(1, content.RootElement.GetProperty("openCount").GetInt32());

        using var invalidAttachment = await client.PostAsync($"{emailUrl}/attachments/asset.initial_report/download", null);
        Assert.Equal(HttpStatusCode.BadRequest, invalidAttachment.StatusCode);
        using var attachmentError = JsonDocument.Parse(await invalidAttachment.Content.ReadAsStringAsync());
        Assert.Equal("Asset is not an attachment of this email", attachmentError.RootElement.GetProperty("message").GetString());
        using var malformed = await client.PostAsync($"{emailUrl}-extra/open", null);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        using var anonymous = await client.PostAsync($"{emailUrl}/open", null);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt.GenerateToken(otherUser, [UserRoles.Player]));
        using var forbidden = await client.PostAsync($"{emailUrl}/open", null);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.False(await context.CaseSessionEmailStates.AnyAsync(e =>
            e.UserId == otherUser.Id && e.CaseId == raw.CaseId && e.EmailId == emailId));
    }

    private static User UserFixture()
    {
        var email = $"no-findings-{Guid.NewGuid():N}@fic-police.gov";
        return new()
        {
            Id = Guid.NewGuid().ToString("N"), UserName = email, Email = email, PersonalEmail = email,
            FirstName = "No findings", LastName = "Fixture", Rank = DetectiveRank.Detective
        };
    }

    private sealed class FrozenTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
