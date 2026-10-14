using System.Text.Json;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using CaseGen.Functions.Models.CaseV2;
using CaseGen.Functions.Services;
using CaseGen.Functions.Services.CaseV2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace CaseGen.Functions.Tests.CaseV2;

internal sealed class CaseGenerationTestFixture : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "casegen-tests", Guid.NewGuid().ToString("N"));
    public TestCaseLlmProvider Llm { get; } = new();
    public TestAssetRenderer Renderer { get; } = new();
    public TestCasePublisher Publisher { get; } = new();
    public Action<CaseDraft>? BeforeConsistency { get; set; }
    public string CasePath => Path.Combine(Root, "case_quality", "case.json");

    public CaseGenerationTestFixture() => Directory.CreateDirectory(Root);

    public Task<GenerateCaseV2Response> GenerateAsync(bool writeToDisk = true, CancellationToken ct = default,
        GenerateCaseV2Request? request = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["CaseGenV2:CasesBasePath"] = Root
        }).Build();
        var generator = new CaseV2GeneratorService(
            Llm, config, Renderer, Publisher,
            new MechanicalRulesBuilder(NullLogger<MechanicalRulesBuilder>.Instance),
            new FixtureConsistencyValidator(BeforeConsistency),
            NullLogger<CaseV2GeneratorService>.Instance);
        return generator.GenerateAsync(request ?? new GenerateCaseV2Request
        {
            CaseId = "case_quality", WriteToDisk = writeToDisk
        }, ct);
    }

    public IEnumerable<string> DiscoverableCases() =>
        Directory.EnumerateDirectories(Root).Where(dir => File.Exists(Path.Combine(dir, "case.json")));

    public void SeedExistingCase()
    {
        var assets = Path.Combine(Root, "case_quality", "assets");
        Directory.CreateDirectory(assets);
        File.WriteAllText(CasePath, "original case");
        File.WriteAllText(Path.Combine(assets, "scene.pdf"), "original asset");
        File.WriteAllText(Path.Combine(assets, "extra.txt"), "preserved extra");
    }

    public void Dispose() => Directory.Delete(Root, recursive: true);

    private sealed class FixtureConsistencyValidator(Action<CaseDraft>? before) : IConsistencyValidator
    {
        private bool _injected;
        public ConsistencyReport Validate(CaseDraft draft)
        {
            if (!_injected)
            {
                _injected = true;
                before?.Invoke(draft);
            }
            return new ConsistencyValidator(NullLogger<ConsistencyValidator>.Instance).Validate(draft);
        }
    }
}

internal record TestLlmCall(string System, string User, string Schema);

internal sealed class TestCaseLlmProvider : ILLMProvider
{
    public ConcurrentQueue<TestLlmCall> Calls { get; } = new();
    public Func<string, string, JsonNode, JsonNode>? TransformResponse { get; set; }
    public string ReviewContent { get; set; } = """{"verdict":"ok","findings":[]}""";
    public Exception? ReviewException { get; set; }
    public Action? OnReview { get; set; }
    public Func<string, string>? ReviewResponse { get; set; }
    public Func<JsonObject, JsonObject>? RepairResponse { get; set; }
    public Action? OnImage { get; set; }
    public bool InvalidDifficulty { get; set; }
    public bool OrphanForensicInput { get; set; }
    public bool InvalidRefine { get; set; }
    public bool PhotoAsset { get; set; }
    public byte[] ImageBytes { get; set; } = [0xFF, 0xD8, 0xFF, 0x00];

    private static readonly string[] AssetIds = { "asset.scene", "asset.receipt", "asset.access_log", "asset.statement" };
    private static readonly string[] SuspectIds = { "suspect.jordan", "suspect.riley", "suspect.sam" };
    private CaseBible _bible = CaseBibleTestData.Create();

    public Task<LLMResponse> GenerateStructuredResponseAsync(string systemPrompt, string userPrompt, string jsonSchema, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls.Enqueue(new TestLlmCall(systemPrompt, userPrompt, jsonSchema));
        if (systemPrompt.Contains("red-team reviewer"))
        {
            OnReview?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            if (ReviewException is not null) throw ReviewException;
            return Task.FromResult(new LLMResponse { Content = ReviewResponse?.Invoke(userPrompt) ?? ReviewContent });
        }

        object response;
        if (systemPrompt.Contains("**outline**"))
            response = new
            {
                metadata = new
                {
                    title = "Quality fixture", description = "A controlled fictional investigation for regression tests.",
                    location = "Test city", incidentDate = "2025-01-01T08:00:00Z", openedAt = "2025-01-01T09:00:00Z",
                    difficulty = InvalidDifficulty ? "InvalidRank" : "Detective", requiredRank = "Detective",
                    category = "fraud", briefing = "Investigate an altered access record.", tags = new[] { "test" }, victim = (object?)null
                },
                suspects = SuspectIds.Select(id => new { id, name = id["suspect.".Length..], role = "employee", isCulprit = id == SuspectIds[0] }),
                culpritId = SuspectIds[0]
            };
        else if (systemPrompt.Contains("**canonical CaseBible**"))
            response = CaseBibleTestData.Create(PhotoAsset, OrphanForensicInput,
                Context(userPrompt)["metadata"]!["requiredRank"]!.GetValue<string>() == "Rookie");
        else if (systemPrompt.Contains("**full profile**"))
        {
            var id = Line(userPrompt, "id");
            var source = Source(id);
            response = new
            {
                id, name = id["suspect.".Length..], age = 30, occupation = "employee",
                relationship = "colleague", motive = "Conceal an unauthorized transfer.",
                alibi = Content(source), alibiVerified = Line(userPrompt, "alibiVerified") == "true",
                background = "A fictional employee with access to the shared records.", support = Support(source)
            };
        }
        else if (systemPrompt.Contains("**initial evidence assets**"))
            response = new
            {
                assets = AssetIds.Select(id => new
                {
                    id, type = PhotoAsset && id == AssetIds[0] ? "photo" : "pdf",
                    title = id["asset.".Length..], role = "Documented case fact", visibility = "initial"
                })
            };
        else if (systemPrompt.Contains("**detail card**"))
        {
            var source = Source(Line(userPrompt, "id"));
            response = new
            {
                id = Line(userPrompt, "id"), type = Line(userPrompt, "type"), title = Line(userPrompt, "title"),
                description = "An independently recorded event.", visibility = source.Visibility, category = "Document",
                body = Line(userPrompt, "type") == "photo" ? "A fictional test scene." : "",
                bodyDoc = Line(userPrompt, "type") == "photo" ? null : new
                {
                    layout = "GeneralReport", title = Line(userPrompt, "title"),
                    subtitle = (string?)null, classification = "INTERNAL", header = Array.Empty<object>(),
                    sections = new[] { new { kind = "narrative", heading = (string?)null, text = Content(source) } },
                    signature = (object?)null
                },
                support = Support(source)
            };
        }
        else if (systemPrompt.Contains("**narrative timeline**"))
            response = new
            {
                timeline = new[]
                {
                    new { time = "2025-01-01T08:15:00Z", @event = "Access record saved.", source = "sensor", verified = true, importance = "high" },
                    new { time = "2025-01-01T08:45:00Z", @event = "Access discrepancy reported.", source = "investigation", verified = true, importance = "medium" }
                },
                temporalEvents = Array.Empty<object>()
            };
        else if (systemPrompt.Contains("**forensics layer**"))
            response = new
            {
                analysisTypes = new[]
                {
                    new { type = "DigitalForensics", durationMinutes = 15, availableFor = new[] { "pdf", "digital" } },
                    new { type = "DocumentAuthenticity", durationMinutes = 30, availableFor = new[] { "pdf", "document" } }
                },
                outcomes = new[]
                {
                    new
                    {
                        inputAssetId = OrphanForensicInput ? "asset.unknown" : "asset.access_log",
                        analysisType = "DigitalForensics", findings = true, matchedSuspectId = SuspectIds[0],
                        role = "Compare the access record with its signed backup."
                    }
                }
            };
        else if (systemPrompt.Contains("**detailed forensic outcome**"))
        {
            var outcome = _bible.Outcomes.FirstOrDefault(o => o.InputAssetId == Line(userPrompt, "inputAssetId") &&
                o.AnalysisType == Line(userPrompt, "analysisType")) ?? _bible.Outcomes[0];
            var conclusion = string.Join(" ", outcome.FactIds.Select(id => _bible.Facts.Single(f => f.Id == id).Statement));
            var report = outcome.ResultAssetId is null ? null : Source(outcome.ResultAssetId);
            var email = outcome.ResultEmailId is null ? null : Source(outcome.ResultEmailId);
            response = new
            {
                conclusionText = conclusion, support = outcome.FactIds.Select(id => new CaseContentSupport
                {
                    ReferenceId = id, Excerpt = _bible.Facts.Single(f => f.Id == id).Statement
                }),
                resultAsset = report is null ? null : new
                {
                    id = report.Id, type = report.Type, title = report.Title,
                    description = "A lab comparison of the access records.", body = Content(report),
                    visibility = report.Visibility, category = "Document", bodyDoc = (object?)null, support = Support(report)
                },
                resultEmail = email is null ? null : new
                {
                    id = email.Id, from = email.From, subject = email.Title,
                    body = Content(email), sentAt = email.RecordedAt,
                    priority = (string?)null, attachments = email.Attachments, visibility = email.Visibility, support = Support(email)
                }
            };
        }
        else if (systemPrompt.Contains("ONLY the briefing email"))
        {
            var source = Source("email.briefing");
            response = new { from = source.From, subject = source.Title, body = Content(source), support = Support(source) };
        }
        else if (systemPrompt.Contains("0-3 OPTIONAL"))
        {
            var source = _bible.Sources.FirstOrDefault(s => s.Kind == "email" && s.Id == Context(userPrompt)["sources"]![0]!["id"]!.GetValue<string>());
            response = new { emails = source is null ? [] : new[]
            {
                new EvidenceEmail
                {
                    Id = source.Id, From = source.From!, Subject = source.Title, Body = Content(source),
                    SentAt = source.RecordedAt, Attachments = source.Attachments, Visibility = source.Visibility, Support = Support(source)
                }
            }};
        }
        else if (systemPrompt.Contains("ONLY the narrative"))
            response = new { rules = Array.Empty<object>() };
        else if (systemPrompt.Contains("**skeleton**"))
            response = new
            {
                requiredEvidenceIds = new[] { "asset.access_log", "asset.report_access" },
                requiredAnalysisIds = new[] { "asset.access_log:DigitalForensics" },
                questionTopics = new[]
                {
                    new { id = "q.motive", topic = "motive", weight = 1.0 },
                    new { id = "q.method", topic = "method", weight = 1.0 }
                }
            };
        else if (systemPrompt.Contains("ONE multiple-choice question"))
        {
            var id = Regex.Match(systemPrompt, "Echo id `([^`]+)`").Groups[1].Value;
            var plan = _bible.Questions.Single(q => q.Id == id);
            response = new
            {
                id, prompt = "What does the evidence show?",
                options = new[]
                {
                    new { id = $"opt.{id[2..]}_primary", label = plan.Answer },
                    new { id = $"opt.{id[2..]}_second", label = "An authorized update" },
                    new { id = $"opt.{id[2..]}_third", label = "A clock discrepancy" }
                },
                correctOptionId = $"opt.{id[2..]}_primary", weight = plan.Weight,
                support = plan.FactIds.Select(referenceId => new CaseContentSupport { ReferenceId = referenceId, Excerpt = plan.Answer })
            };
        }
        else if (systemPrompt.Contains("post-mortem explanation"))
            response = new
            {
                explanation = string.Join(" ", _bible.ExplanationFactIds.Select(id => _bible.Facts.Single(f => f.Id == id).Statement)),
                support = _bible.ExplanationFactIds.Select(id => new CaseContentSupport
                {
                    ReferenceId = id, Excerpt = _bible.Facts.Single(f => f.Id == id).Statement
                })
            };
        else if (systemPrompt.Contains("AS THE PLAYER"))
            response = new { suspectId = "suspect.unknown", evidenceIds = Array.Empty<string>(), analysisIds = Array.Empty<string>(), answers = Array.Empty<object>(), rationale = (string?)null };
        else if (systemPrompt.Contains("**refine agent**"))
        {
            if (InvalidRefine) return Task.FromResult(new LLMResponse { Content = "{}" });
            var current = userPrompt.Split("=== CANONICAL DRAFT ===\n")[1].Split("\n\n=== VALIDATION ERRORS ===")[0];
            var snapshot = JsonNode.Parse(current)!.AsObject();
            JsonObject repair;
            if (RepairResponse is not null) repair = RepairResponse(snapshot);
            else
            {
                var metadata = snapshot["components"]!["metadata"]!.DeepClone();
                metadata["difficulty"] = "Detective";
                repair = new JsonObject
                {
                    ["baseRevision"] = snapshot["revision"]!.GetValue<int>(),
                    ["changes"] = new JsonArray(new JsonObject
                    {
                        ["target"] = "metadata", ["reason"] = "Restore requested difficulty.", ["value"] = metadata
                    })
                };
            }
            return Task.FromResult(new LLMResponse { Content = repair.ToJsonString() });
        }
        else
            throw new InvalidOperationException($"Unrecognized fixture task: {systemPrompt}");
        var node = JsonSerializer.SerializeToNode(response)!;
        if (TransformResponse is not null) node = TransformResponse(systemPrompt, userPrompt, node);
        if (systemPrompt.Contains("**canonical CaseBible**")) _bible = node.Deserialize<CaseBible>(CaseDraft.CanonicalJsonOptions)!;
        return Task.FromResult(new LLMResponse { Content = node.ToJsonString() });
    }

    public Task<LLMResponse> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<byte[]> GenerateImageAsync(string prompt, CancellationToken cancellationToken = default)
    {
        OnImage?.Invoke();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ImageBytes);
    }

    public Task<byte[]> GenerateImageWithReferenceAsync(string prompt, byte[] referenceImage, byte[]? maskImage = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    private static string Line(string user, string key) =>
        user.Split('\n').Single(line => line.StartsWith(key + "=", StringComparison.Ordinal))[(key.Length + 1)..].Trim();

    private CaseSourcePlan Source(string id) => _bible.Sources.Single(s => s.Id == id);
    private List<CaseContentSupport> Support(CaseSourcePlan source) =>
        source.FactIds.Select(id => new CaseContentSupport { ReferenceId = id, Excerpt = _bible.Facts.Single(f => f.Id == id).Statement })
            .Concat(source.ClaimIds.Select(id => new CaseContentSupport
            {
                ReferenceId = id, Excerpt = $"{_bible.Entities.Single(e => e.Id == _bible.Claims.Single(c => c.Id == id).SpeakerEntityId).Name}: {_bible.Claims.Single(c => c.Id == id).Statement}"
            })).ToList();

    private string Content(CaseSourcePlan source) => string.Join("\n", Support(source).Select(s => s.Excerpt));

    private static JsonObject Context(string user)
    {
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(user[user.IndexOf('{')..]));
        return JsonNode.Parse(ref reader)!.AsObject();
    }
}

internal sealed class TestAssetRenderer : IAssetRenderingService
{
    public int Calls { get; private set; }
    public bool Fail { get; set; }
    public bool Skip { get; set; }
    public bool MissingFiles { get; set; }
    public bool EmptyFiles { get; set; }
    public bool JpegPhotos { get; set; }
    public bool InvalidBytes { get; set; }
    public Action<string, string>? OnRender { get; set; }
    public List<EvidenceAsset> RenderedAssets { get; private set; } = new();
    public Action<EvidenceAsset>? OnPrepare { get; set; }

    public void PrepareForReview(IEnumerable<EvidenceAsset> assets)
    {
        foreach (var asset in assets)
        {
            if (asset.PreparedForReview) continue;
            OnPrepare?.Invoke(asset);
            asset.PreparedForReview = true;
        }
    }

    public async Task<AssetRenderingReport> RenderAllAsync(string caseId, string casesBasePath, IEnumerable<EvidenceAsset> assets, CancellationToken ct = default)
    {
        Calls++;
        OnRender?.Invoke(caseId, casesBasePath);
        ct.ThrowIfCancellationRequested();
        var items = assets.ToList();
        RenderedAssets = items;
        var directory = Path.Combine(casesBasePath, caseId, "assets");
        Directory.CreateDirectory(directory);
        var files = new List<RenderedAssetFile>();
        foreach (var asset in items)
        {
            var ext = asset.Type == "photo" ? JpegPhotos ? "jpg" : "png" : "pdf";
            var name = $"{asset.Id["asset.".Length..]}.{ext}";
            var mime = ext switch { "jpg" => "image/jpeg", "png" => "image/png", _ => "application/pdf" };
            files.Add(new RenderedAssetFile(asset.Id, name, mime));
            if (MissingFiles) continue;
            var content = Encoding.UTF8.GetBytes($"Rendered {asset.Id}\n{asset.Body}\n{JsonSerializer.Serialize(asset.BodyDoc)}");
            var bytes = ext switch
            {
                "pdf" => Encoding.UTF8.GetBytes($"%PDF-1.7\n{Encoding.UTF8.GetString(content)}\n%%EOF"),
                "jpg" => new byte[] { 0xFF, 0xD8, 0xFF }.Concat(content).Concat(new byte[] { 0xFF, 0xD9 }).ToArray(),
                _ => new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.Concat(content)
                    .Concat(new byte[] { 0, 0, 0, 0, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 }).ToArray()
            };
            await File.WriteAllBytesAsync(Path.Combine(directory, name), EmptyFiles ? [] : InvalidBytes ? content : bytes, ct);
        }
        return new AssetRenderingReport
        {
            PdfsWritten = items.Count(asset => asset.Type != "photo"),
            ImagesWritten = items.Count(asset => asset.Type == "photo"),
            Skipped = Skip ? 1 : 0,
            Files = files,
            Errors = Fail ? new() { "Injected PDF rendering failure" } : new()
        };
    }
}

internal sealed class TestCasePublisher : ICaseV2BlobPublisher
{
    public bool IsConfigured { get; set; } = true;
    public int Calls { get; private set; }
    public bool ReturnZero { get; set; }
    public Exception? Exception { get; set; }
    public Action<string, string>? OnPublish { get; set; }

    public Task<int> PublishAsync(CaseBundle bundle, CancellationToken ct = default)
    {
        Calls++;
        OnPublish?.Invoke(bundle.CaseJsonPath, bundle.BundleDirectory);
        ct.ThrowIfCancellationRequested();
        if (Exception is not null) throw Exception;
        return Task.FromResult(ReturnZero ? 0 : bundle.Manifest.Files.Count + 2);
    }
}
