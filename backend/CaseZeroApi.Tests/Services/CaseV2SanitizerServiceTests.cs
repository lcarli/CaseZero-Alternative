using System.Text.Json;
using CaseZeroApi.Data;
using CaseZeroApi.Models;
using CaseZeroApi.Models.CaseV2;
using CaseZeroApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaseZeroApi.Tests.Services;

public class CaseV2SanitizerServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly CaseV2SanitizerService _sut;

    private const string UserId = "user-sanitizer-test";
    private const string CaseId = "case-sanitizer-test";

    public CaseV2SanitizerServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _sut = new CaseV2SanitizerService(_context, NullLogger<CaseV2SanitizerService>.Instance);
    }

    // ──────────────────────────────────────────────────────────────
    // Fixture helpers
    // ──────────────────────────────────────────────────────────────

    private static CaseV2 BuildFixtureCase() => new()
    {
        Version = "2.0",
        CaseId = CaseId,
        Metadata = new CaseV2Metadata { Title = "Test Case", Description = "Desc", Location = "City" },
        Assets = new List<CaseV2Asset>
        {
            new() { Id = "asset.initial_1", Title = "Initial Asset 1", Uri = "file://a1", Visibility = "initial" },
            new() { Id = "asset.hidden_1",  Title = "Hidden Asset 1",  Uri = "file://a2", Visibility = "hidden"  },
        },
        Emails = new List<CaseV2Email>
        {
            new() { Id = "email.initial_1", From = "a@b.com", To = new List<string>{"det@fic.gov"}, Subject = "S1", Body = "B1", SentAt = "2024-01-01", Visibility = "initial" },
            new() { Id = "email.hidden_1",  From = "x@y.com", To = new List<string>{"det@fic.gov"}, Subject = "S2", Body = "B2", SentAt = "2024-01-02", Visibility = "hidden"  },
        },
        Suspects = new List<CaseV2Suspect>
        {
            new() { Id = "suspect.initial_1", Name = "Alice", Visibility = "initial" },
            new() { Id = "suspect.hidden_1",  Name = "Bob",   Visibility = "hidden"  },
        },
        Rules = new List<CaseV2Rule>
        {
            new() { RuleId = "rule-1", Trigger = new CaseV2Trigger { Type = "asset_viewed", AssetId = "asset.hidden_1" }, Actions = new List<CaseV2Action>() }
        },
        ForensicOutcomes = new List<CaseV2ForensicOutcome>
        {
            new() { InputAssetId = "asset.initial_1", AnalysisType = "Digital", Findings = true }
        },
        TemporalEvents = new List<CaseV2TemporalEvent>
        {
            new() { Id = "te-1", TriggerAtMinutes = 60, Type = "reveal_email" }
        },
        Solution = new CaseV2Solution
        {
            CulpritId = "suspect.hidden_1",
            RequiredEvidenceIds = new List<string> { "asset.initial_1" },
            RequiredAnalysisIds = new List<string> { "asset.initial_1:Digital" },
            Questions = new List<CaseV2Question>
            {
                new()
                {
                    Id = "q1", Prompt = "Why?",
                    Options = new List<CaseV2QuestionOption> { new() { Id = "opt-a", Label = "A" } },
                    CorrectOptionId = "opt-a"
                }
            },
            Explanation = "The culprit did it because...",
            PartialCreditRules = new CaseV2PartialCreditRules()
        },
        GameMetadata = new CaseV2GameMetadata
        {
            SchemaVersion = "2.0",
            CreatedAt = "2024-01-01",
            Generation = new CaseV2GenerationMetadata { Model = "gpt-4", Seed = 42 }
        }
    };

    // ──────────────────────────────────────────────────────────────
    // Sanitize() — no session (static sanitization)
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void Sanitize_StripsSolutionSensitiveFields()
    {
        var result = _sut.Sanitize(BuildFixtureCase());

        Assert.NotNull(result.Solution);
        // CulpritId, RequiredEvidenceIds, RequiredAnalysisIds, Explanation, PartialCreditRules
        // are NOT present on SanitizedSolution — only Questions (without correctOptionId), MinimumScore, MaxAttempts
        Assert.Single(result.Solution.Questions);
        Assert.Null(result.Solution.Questions[0].GetType().GetProperty("CorrectOptionId")
            ?.GetValue(result.Solution.Questions[0]));
    }

    [Fact]
    public void Sanitize_StripsRulesAndForensicOutcomes()
    {
        // Sanitize() returns CaseV2Sanitized which has no Rules / ForensicOutcomes / TemporalEvents properties
        var result = _sut.Sanitize(BuildFixtureCase());

        Assert.Null(result.GetType().GetProperty("Rules")?.GetValue(result));
        Assert.Null(result.GetType().GetProperty("ForensicOutcomes")?.GetValue(result));
        Assert.Null(result.GetType().GetProperty("TemporalEvents")?.GetValue(result));
    }

    [Fact]
    public void Sanitize_StripsGenerationFromGameMetadata()
    {
        var result = _sut.Sanitize(BuildFixtureCase());

        Assert.NotNull(result.GameMetadata);
        Assert.Null(result.GameMetadata.GetType().GetProperty("Generation")?.GetValue(result.GameMetadata));
    }

    [Fact]
    public void Sanitize_OnlyExposesInitialVisibilityEntities()
    {
        var result = _sut.Sanitize(BuildFixtureCase());

        Assert.Single(result.Assets);
        Assert.Equal("asset.initial_1", result.Assets[0].Id);

        Assert.Single(result.Emails);
        Assert.Equal("email.initial_1", result.Emails[0].Id);

        Assert.Single(result.Suspects);
        Assert.Equal("suspect.initial_1", result.Suspects[0].Id);
    }

    // ──────────────────────────────────────────────────────────────
    // SanitizeForUserAsync() — session-aware
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SanitizeForUser_HiddenAsset_NotRevealedInSession_IsFiltered()
    {
        var result = await _sut.SanitizeForUserAsync(BuildFixtureCase(), UserId, CaseId);

        Assert.DoesNotContain(result.Assets, a => a.Id == "asset.hidden_1");
    }

    [Fact]
    public async Task SanitizeForUser_HiddenAsset_RevealedInSession_IsIncluded()
    {
        _context.CaseSessionVisibleAssets.Add(new CaseSessionVisibleAsset
        {
            UserId = UserId, CaseId = CaseId, AssetId = "asset.hidden_1", UnlockedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var result = await _sut.SanitizeForUserAsync(BuildFixtureCase(), UserId, CaseId);

        Assert.Contains(result.Assets, a => a.Id == "asset.hidden_1");
    }

    [Fact]
    public async Task SanitizeForUser_HiddenEmail_NotRevealedInSession_IsFiltered()
    {
        var result = await _sut.SanitizeForUserAsync(BuildFixtureCase(), UserId, CaseId);

        Assert.DoesNotContain(result.Emails, e => e.Id == "email.hidden_1");
    }

    [Fact]
    public async Task SanitizeForUser_HiddenEmail_RevealedInSession_IsIncluded()
    {
        _context.CaseSessionVisibleEmails.Add(new CaseSessionVisibleEmail
        {
            UserId = UserId, CaseId = CaseId, EmailId = "email.hidden_1", UnlockedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var result = await _sut.SanitizeForUserAsync(BuildFixtureCase(), UserId, CaseId);

        Assert.Contains(result.Emails, e => e.Id == "email.hidden_1");
    }

    [Fact]
    public async Task SanitizeForUser_HiddenSuspect_RevealedViaSession_IsIncluded()
    {
        var session = new CaseSession
        {
            UserId = UserId, CaseId = CaseId,
            RevealedSuspectIds = JsonSerializer.Serialize(new List<string> { "suspect.hidden_1" })
        };
        _context.CaseSessions.Add(session);
        await _context.SaveChangesAsync();

        var result = await _sut.SanitizeForUserAsync(BuildFixtureCase(), UserId, CaseId);

        Assert.Contains(result.Suspects, s => s.Id == "suspect.hidden_1");
    }

    [Fact]
    public async Task SanitizeForUser_SuspectStatusOverride_AppliesCorrectly()
    {
        var overrides = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["suspect.initial_1"] = "cleared"
        });
        var session = new CaseSession { UserId = UserId, CaseId = CaseId, SuspectStatusOverrides = overrides };
        _context.CaseSessions.Add(session);
        await _context.SaveChangesAsync();

        var result = await _sut.SanitizeForUserAsync(BuildFixtureCase(), UserId, CaseId);

        var suspect = result.Suspects.Single(s => s.Id == "suspect.initial_1");
        Assert.Equal("cleared", suspect.Status);
    }

    [Fact]
    public async Task SanitizeForUser_AlibiVerifiedOverride_AppliesCorrectly()
    {
        var overrides = JsonSerializer.Serialize(new Dictionary<string, bool>
        {
            ["suspect.initial_1"] = true
        });
        var session = new CaseSession { UserId = UserId, CaseId = CaseId, SuspectAlibiVerified = overrides };
        _context.CaseSessions.Add(session);
        await _context.SaveChangesAsync();

        var result = await _sut.SanitizeForUserAsync(BuildFixtureCase(), UserId, CaseId);

        var suspect = result.Suspects.Single(s => s.Id == "suspect.initial_1");
        Assert.True(suspect.AlibiVerified);
    }

    [Fact]
    public async Task SanitizeForUser_EmailAttachmentOverride_AddsAttachmentToEmail()
    {
        var overrides = JsonSerializer.Serialize(new Dictionary<string, List<string>>
        {
            ["email.initial_1"] = new List<string> { "asset.hidden_1" }
        });
        var session = new CaseSession { UserId = UserId, CaseId = CaseId, EmailAttachmentOverrides = overrides };
        _context.CaseSessions.Add(session);
        await _context.SaveChangesAsync();

        var result = await _sut.SanitizeForUserAsync(BuildFixtureCase(), UserId, CaseId);

        var email = result.Emails.Single(e => e.Id == "email.initial_1");
        Assert.NotNull(email.Attachments);
        Assert.Contains("asset.hidden_1", email.Attachments);
    }

    [Fact]
    public async Task SanitizeForUser_SyntheticEmails_AppearInOutput()
    {
        var synthetic = new List<CaseV2Email>
        {
            new()
            {
                Id = "email.synthetic_1", From = "sys@lab.gov", To = new List<string> { "det@fic.gov" },
                Subject = "Lab Report", Body = "Findings...", SentAt = "2024-02-01", Visibility = "hidden"
            }
        };
        var session = new CaseSession
        {
            UserId = UserId, CaseId = CaseId,
            SyntheticEmails = JsonSerializer.Serialize(synthetic)
        };
        _context.CaseSessions.Add(session);
        await _context.SaveChangesAsync();

        var result = await _sut.SanitizeForUserAsync(BuildFixtureCase(), UserId, CaseId);

        Assert.Contains(result.Emails, e => e.Id == "email.synthetic_1");
    }

    [Fact]
    public async Task SanitizeForUser_SolutionStripsCorrectOptionId()
    {
        var result = await _sut.SanitizeForUserAsync(BuildFixtureCase(), UserId, CaseId);

        Assert.NotNull(result.Solution);
        Assert.Single(result.Solution.Questions);
        // SanitizedQuestion has no CorrectOptionId property
        Assert.Null(result.Solution.Questions[0].GetType().GetProperty("CorrectOptionId")
            ?.GetValue(result.Solution.Questions[0]));
    }

    public void Dispose() => _context.Dispose();
}
