using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CaseGen.Functions.Models.CaseV2;
using CaseGen.Functions.Services;
using CaseGen.Functions.Services.CaseV2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace CaseGen.Functions.Tests.CaseV2;

internal sealed class CaseGenerationTestFixture : IDisposable
{
    public string Root { get; } = Path.Combine(AppContext.BaseDirectory, "canonical-fixtures", Guid.NewGuid().ToString("N"));
    public TestCaseLlmProvider Llm { get; } = new();
    public TestAssetRenderer Renderer { get; } = new();
    public TestCasePublisher Publisher { get; } = new();
    public Action<CaseDraft>? BeforeConsistency { get; set; }
    public int? RepairMaxIterations { get; set; }
    public string CasePath => Path.Combine(Root, "case_quality", "case.json");

    public CaseGenerationTestFixture() => Directory.CreateDirectory(Root);

    public Task<GenerateCaseV2Response> GenerateAsync(bool writeToDisk = true, CancellationToken ct = default,
        GenerateCaseV2Request? request = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["CaseGenV2:CasesBasePath"] = Root
        };
        if (RepairMaxIterations is int repairMaxIterations)
            settings["CaseGenV2:RepairMaxIterations"] = repairMaxIterations.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
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

    public void Dispose()
    {
        if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
    }

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
    public async Task<LLMResponse> GenerateStructuredResponseAsync(string systemPrompt, string userPrompt, string jsonSchema,
        int maximumOutputTokens, CancellationToken cancellationToken = default)
    {
        var response = await GenerateStructuredResponseAsync(systemPrompt, userPrompt, jsonSchema, cancellationToken);
        if (response.Usage?.CompletionTokens > maximumOutputTokens)
            throw new InvalidOperationException("Synthetic response exceeds its output-token allowance.");
        return response;
    }

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
    public bool ReturnTokenUsage { get; set; } = true;
    public byte[] ImageBytes { get; set; } = [0xFF, 0xD8, 0xFF, 0x00];

    private CaseBible _bible = CaseBibleTestData.Create();
    private readonly ConcurrentDictionary<string, EvidenceAsset> _assets = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _answers = new(StringComparer.Ordinal);
    private readonly HashSet<string> _blindActions = new(StringComparer.Ordinal);
    private List<string> _requiredEvidenceIds = new();
    private List<string> _requiredAnalysisIds = new();

    public Task<LLMResponse> GenerateStructuredResponseAsync(string systemPrompt, string userPrompt, string jsonSchema,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls.Enqueue(new TestLlmCall(systemPrompt, userPrompt, jsonSchema));
        if (systemPrompt.Contains("red-team reviewer", StringComparison.Ordinal))
        {
            Review(cancellationToken);
            return Task.FromResult(Response(systemPrompt, userPrompt, jsonSchema, ReviewResponse?.Invoke(userPrompt) ?? ReviewContent));
        }

        object response;
        if (systemPrompt.Contains("BlindPlayerInvestigator", StringComparison.Ordinal))
            response = BlindDecision(Context(userPrompt));
        else if (systemPrompt.Contains("specialist for an interactive detective case", StringComparison.Ordinal))
        {
            Review(cancellationToken);
            response = SpecialistResponse(systemPrompt, userPrompt);
        }
        else if (systemPrompt.Contains("**canonical CaseBible**", StringComparison.Ordinal))
        {
            var difficulty = Regex.Match(userPrompt, @"^- Difficulty: `([^`]+)`", RegexOptions.Multiline).Groups[1].Value;
            _bible = CaseBibleTestData.Create(rookie: difficulty == "Rookie");
            response = _bible;
        }
        else if (systemPrompt.Contains("**outline**", StringComparison.Ordinal))
        {
            var title = Regex.Match(userPrompt, @"^title=([^\r\n]+)", RegexOptions.Multiline).Groups[1].Value;
            var rank = Regex.Match(userPrompt, @"^requiredRank=([^\r\n]+)", RegexOptions.Multiline).Groups[1].Value;
            response = new
            {
                metadata = new
                {
                    title = title is "" or "(model chooses)" ? "Institutional records investigation" : title,
                    description = "A controlled fictional investigation into an altered institutional record.",
                    difficulty = InvalidDifficulty ? "InvalidRank" : _bible.DifficultyIntent.Difficulty,
                    requiredRank = rank.Length == 0 ? _bible.DifficultyIntent.Difficulty : rank,
                    category = _bible.Incident.CrimeType, briefing = "Read the independently recorded observations before drawing conclusions.",
                    tags = new[] { "records" }
                },
                clueOrderIds = _bible.DifficultyIntent.Clues.Select(c => c.Id),
                decoyOrderIds = _bible.DifficultyIntent.DecoyArcs.Select(d => d.Id)
            };
        }
        else if (systemPrompt.Contains("**full profile**", StringComparison.Ordinal))
        {
            var context = Context(userPrompt);
            var profile = context["profile"]!;
            var support = Support(context["sourceContext"]!);
            response = new
            {
                id = S(profile, "id"), name = S(profile, "name"), age = V(profile, "age"),
                occupation = S(profile, "occupation"), relationship = S(profile, "relationship"), motive = (string?)null,
                alibi = S(profile, "statedAlibi"), alibiVerified = false, background = Printed(support),
                photo = (string?)null, relatedAssets = Array.Empty<string>(), support
            };
        }
        else if (systemPrompt.Contains("Plan the complete initial evidence dossier", StringComparison.Ordinal))
            response = PortfolioPlan(userPrompt);
        else if (systemPrompt.Contains("**detail card**", StringComparison.Ordinal))
        {
            var source = Block(userPrompt, "# Only permitted source observations and source-time knowledge");
            var slot = Block(userPrompt, "# Binding public asset slot");
            var support = Support(source);
            response = Asset(S(slot, "id"), S(slot, "type"), S(slot, "title"), S(slot, "layoutHint"), support,
                S(slot, "visibility"), S(slot, "evidenceRole"), N(slot, "subjectSuspectId"), N(slot, "imagePurpose"));
        }
        else if (systemPrompt.Contains("**forensics layer**", StringComparison.Ordinal))
            response = ForensicPlan(Context(userPrompt));
        else if (systemPrompt.Contains("**detailed forensic outcome**", StringComparison.Ordinal))
        {
            var source = Block(userPrompt, "# Permitted result-source observations");
            var plan = Block(userPrompt, "# Binding result-material plan");
            var method = Block(userPrompt, "# Immutable scientific method contract");
            var support = Support(source);
            var limitations = V(plan, "limitations")!.AsArray().Select(n => n!.GetValue<string>());
            var labels = V(method, "labels")!;
            var text = $"{S(labels, "itemsSubmitted")}: {S(plan, "inputObjectId")}\n{Printed(support)}\n{S(labels, "limitations")}\n"
                + string.Join("\n", limitations);
            var report = Asset(S(plan, "resultAssetId"), "pdf", S(plan, "title"), "ForensicReport", support, "hidden");
            report.BodyDoc = Document("ForensicReport", text);
            var email = new EvidenceEmail
            {
                Id = S(plan, "resultEmailId"), From = S(plan, "from"), Subject = S(plan, "title"),
                SentAt = S(plan, "sentAt"), Visibility = "hidden", Body = text,
                Attachments = [report.Id], Support = support
            };
            response = new
            {
                conclusionText = text, support, resultAsset = report, resultEmail = email
            };
        }
        else if (systemPrompt.Contains("ONLY the briefing email", StringComparison.Ordinal))
        {
            var support = Support(Block(userPrompt, "# Opening knowledge"));
            var plan = Block(userPrompt, "# Binding email plan");
            response = new { from = S(plan, "from"), subject = S(plan, "subject"), body = Printed(support), support };
        }
        else if (systemPrompt.Contains("0-3 OPTIONAL", StringComparison.Ordinal))
            response = new
            {
                emails = FirstJson(userPrompt).AsArray().Select(plan => new EvidenceEmail
                {
                    Id = S(plan!, "id"), From = S(plan!, "from"), Subject = S(plan!, "subject"), SentAt = S(plan!, "sentAt"),
                    Visibility = "initial", Body = Printed(Support(V(plan!, "sourceContext")!)),
                    Attachments = V(plan!, "attachments")!.AsArray().Select(n => n!.GetValue<string>()).ToList(),
                    Support = Support(V(plan!, "sourceContext")!)
                }).ToArray()
            };
        else if (systemPrompt.Contains("narrative runtime", StringComparison.Ordinal))
            response = new { rules = Array.Empty<object>() };
        else if (systemPrompt.Contains("**skeleton**", StringComparison.Ordinal))
        {
            var context = Context(userPrompt);
            var materials = context["materials"]!.AsArray();
            var selected = materials.Where(m => V(m!, "support")!.AsArray().Any(s =>
                S(s!, "referenceId") is "observation.endpoint_owner" or "observation.action_session" or "observation.forensic_link")).ToArray();
            _requiredEvidenceIds = selected.Select(m => S(m!, "id")).Distinct().Take(3).ToList();
            _requiredAnalysisIds = context["analysisIds"]!.AsArray().Select(n => n!.GetValue<string>()).ToList();
            response = new
            {
                requiredEvidenceIds = _requiredEvidenceIds, requiredAnalysisIds = _requiredAnalysisIds,
                questionTopics = _requiredEvidenceIds.Take(2).Select((id, i) => new
                {
                    id = $"q.record_{i + 1}", topic = "An observation explicitly recorded in the source",
                    supportingEvidenceIds = new[] { id }, weight = 1
                }).ToArray()
            };
        }
        else if (systemPrompt.Contains("ONE multiple-choice question", StringComparison.Ordinal))
        {
            var material = FirstJson(userPrompt).AsArray()[0]!;
            var supportNode = V(material, "support")!.AsArray()[0]!;
            var support = new CaseContentSupport { ReferenceId = S(supportNode, "referenceId"), Excerpt = S(supportNode, "excerpt") };
            var id = Regex.Match(systemPrompt, @"Echo id `([^`]+)`").Groups[1].Value;
            var weight = double.Parse(Regex.Match(systemPrompt, @"weight ([\d.]+)").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            var optionId = $"opt.{id["q.".Length..]}_primary";
            _answers[id] = optionId;
            response = new
            {
                id, prompt = "Which statement is explicitly recorded in the supplied source?",
                options = new[]
                {
                    new { id = optionId, label = support.Excerpt },
                    new { id = $"opt.{id["q.".Length..]}_other", label = "The source establishes a different event." },
                    new { id = $"opt.{id["q.".Length..]}_none", label = "The source records no observations." }
                },
                correctOptionId = optionId, weight, support = new[] { support }
            };
        }
        else if (systemPrompt.Contains("post-mortem explanation", StringComparison.Ordinal))
        {
            var materials = Block(userPrompt, "# Reachable readable materials").AsArray();
            var support = materials.SelectMany(m => V(m!, "support")!.AsArray())
                .Where(n => S(n!, "referenceId") is "observation.endpoint_owner" or "observation.action_session" or "observation.forensic_link")
                .Select(n => new CaseContentSupport { ReferenceId = S(n!, "referenceId"), Excerpt = S(n!, "excerpt") })
                .DistinctBy(s => s.ReferenceId).ToList();
            response = new { explanation = Printed(support), support };
        }
        else if (systemPrompt.Contains("**refine agent**", StringComparison.Ordinal))
        {
            var snapshot = Context(userPrompt);
            if (RepairResponse is not null) response = RepairResponse(snapshot);
            else
            {
                var metadata = snapshot["components"]!["metadata"]!.DeepClone();
                metadata["description"] = "Reviewed presentation; all source observations and canonical identities remain unchanged.";
                response = new
                {
                    baseRevision = InvalidRefine ? -1 : snapshot["revision"]!.GetValue<int>(),
                    changes = new[] { new { target = "metadata", reason = "Clarify presentation without rewriting evidence.", value = metadata } }
                };
            }
        }
        else throw new InvalidOperationException("Unhandled canonical test prompt: " + systemPrompt[..Math.Min(180, systemPrompt.Length)]);

        var node = response is JsonNode json ? json : JsonSerializer.SerializeToNode(response, CaseDraft.CanonicalJsonOptions)!;
        node = TransformResponse?.Invoke(systemPrompt, userPrompt, node) ?? node;
        if (systemPrompt.Contains("**canonical CaseBible**", StringComparison.Ordinal))
            _bible = node.Deserialize<CaseBible>(CaseDraft.CanonicalJsonOptions)!;
        if (systemPrompt.Contains("**detail card**", StringComparison.Ordinal))
        {
            var asset = node.Deserialize<EvidenceAsset>(CaseDraft.CanonicalJsonOptions)!;
            _assets[asset.Id] = asset;
        }
        if (node["resultAsset"] is JsonObject reportNode)
        {
            var report = reportNode.Deserialize<EvidenceAsset>(CaseDraft.CanonicalJsonOptions)!;
            _assets[report.Id] = report;
        }
        return Task.FromResult(Response(systemPrompt, userPrompt, jsonSchema, node.ToJsonString()));
    }

    private LLMResponse Response(string system, string user, string schema, string content)
    {
        // Simulated provider usage keeps fixture budgets independent of the production no-usage fallback.
        var input = (Encoding.UTF8.GetByteCount(system + user + schema) + 3) / 4;
        var output = (Encoding.UTF8.GetByteCount(content) + 3) / 4;
        return new LLMResponse
        {
            Content = content,
            Usage = ReturnTokenUsage ? new LLMUsage
            {
                PromptTokens = input, CompletionTokens = output, TotalTokens = input + output
            } : null
        };
    }

    private void Review(CancellationToken ct)
    {
        OnReview?.Invoke();
        ct.ThrowIfCancellationRequested();
        if (ReviewException is not null) throw ReviewException;
    }

    private object SpecialistResponse(string system, string user)
    {
        var legacy = JsonNode.Parse(ReviewResponse?.Invoke(user) ?? ReviewContent)!;
        if (legacy["findings"] is null) return legacy;
        var verdict = legacy["verdict"]?.DeepClone();
        if (!system.Contains("`DocumentFidelity`", StringComparison.Ordinal))
            return new { verdict, findings = Array.Empty<object>() };
        return new
        {
            verdict,
            findings = legacy["findings"]!.AsArray().Select(f => new
            {
                severity = S(f!, "severity"), constraintId = "document_fidelity", owningNodeId = N(f!, "reference") ?? "asset.initial_report",
                message = S(f!, "issue"), suggestedRepair = "Repair only the supported presentation."
            }).ToArray()
        };
    }

    private object PortfolioPlan(string user)
    {
        var portfolio = FirstJson(user, "# Required dossier slots").AsArray();
        var assets = portfolio.Select(slot => new AssetStub
        {
            Id = S(slot!, "id"), ArchetypeId = S(slot!, "archetypeId"), Type = S(slot!, "type"),
            Title = N(slot!, "suggestedTitle") ?? S(slot!, "displayName"), Visibility = "initial",
            LayoutHint = S(slot!, "layoutHint"), EvidenceRole = S(slot!, "evidenceRole"),
            SubjectSuspectId = N(slot!, "subjectSuspectId"), ImagePurpose = N(slot!, "imagePurpose"),
            Role = S(slot!, "purpose"), ContainedObjectIds = [$"object.{S(slot!, "id")["asset.".Length..]}"]
        }).ToList();
        var report = assets.Single(a => a.Id == "asset.initial_report");
        report.ContainedObjectIds = ["document.endpoint_record"];
        var digital = assets.First(a => a.Type == "digital" && EvidenceRoles.IsInvestigative(a.EvidenceRole));
        foreach (var clue in _bible.DifficultyIntent.Clues)
        {
            if (clue.SourceType == "forensic") report.ForensicInputClueIds.Add(clue.Id);
            else (clue.SourceType == "digital" ? digital : report).SupportsClueIds.Add(clue.Id);
        }
        foreach (var decoy in _bible.DifficultyIntent.DecoyArcs)
        {
            var suspectId = _bible.People.Single(p => p.Id == decoy.SuspectPersonId).SuspectId!;
            var interview = assets.Single(a => a.Id == $"asset.interview_{suspectId["suspect.".Length..]}");
            interview.IntroducesRedHerringSuspectIds.Add(suspectId);
            interview.ResolvesRedHerringSuspectIds.Add(suspectId);
        }
        return new { assets };
    }

    private object ForensicPlan(JsonObject context)
    {
        var assets = context["assets"]!.AsArray();
        var opportunities = context["opportunities"]!.AsArray();
        var outcomes = opportunities.Select(opportunity =>
        {
            var ids = V(opportunity!, "clueIds")!.AsArray().Select(n => n!.GetValue<string>()).ToList();
            var input = assets.Single(a => V(a!, "forensicClueIds")!.AsArray().Any(id => ids.Contains(id!.GetValue<string>())))!;
            var method = ForensicMethodCatalog.Find(S(opportunity!, "methodHint"))!;
            return new ForensicOutcomeStub
            {
                InputAssetId = OrphanForensicInput ? "asset.unknown" : S(input, "id"),
                InputObjectId = V(input, "containedObjectIds")!.AsArray()[0]!.GetValue<string>(),
                AnalysisType = method.Id, Findings = true, SupportsClueIds = ids,
                ProducedProperties = [ForensicObservationProperty.DeviceCorrelation],
                LimitationKeys = method.LimitationKeys.ToList(), ResultLayoutId = "ForensicReport",
                ChainOfCustodyObservationId = V(input, "observationIds")!.AsArray()[0]!.GetValue<string>(),
                Role = "Compare existing recorded metadata without claiming human identity."
            };
        }).ToArray();
        return new
        {
            analysisTypes = outcomes.Select(o => ForensicMethodCatalog.Find(o.AnalysisType)!).Distinct().Select(method => new ForensicsAnalysisType
            {
                Type = method.Id, DurationMinutes = 5,
                AvailableFor = method.AcceptedAssetTypes.ToList()
            }), outcomes
        };
    }

    private object BlindDecision(JsonObject context)
    {
        var available = (Find(context, "actions") ?? Find(context, "availableActions"))?.AsArray() ?? new JsonArray();
        var assets = Find(context, "assets")?.AsArray();
        if (assets is not null && assets.Count > 0 && assets.All(a => V(a!, "inspected")?.GetValue<bool>() == false))
            _blindActions.Clear();
        var next = available.Where(a => !_blindActions.Contains(S(a!, "id")))
            .Where(a => S(a!, "id").StartsWith("inspect:", StringComparison.Ordinal)
                || S(a!, "id").StartsWith("email:", StringComparison.Ordinal)
                || S(a!, "id").StartsWith("suspect:", StringComparison.Ordinal)
                || S(a!, "id").StartsWith("download:", StringComparison.Ordinal)
                || S(a!, "id").StartsWith("wait:", StringComparison.Ordinal)
                || _requiredAnalysisIds.Any(id => S(a!, "id") == $"analyze:{id}"))
            .OrderBy(a => S(a!, "id").StartsWith("inspect:", StringComparison.Ordinal) ? 0
                : S(a!, "id").StartsWith("email:", StringComparison.Ordinal) ? 1
                : S(a!, "id").StartsWith("suspect:", StringComparison.Ordinal) ? 2
                : S(a!, "id").StartsWith("download:", StringComparison.Ordinal) ? 3
                : S(a!, "id").StartsWith("analyze:", StringComparison.Ordinal) ? 4 : 5)
            .FirstOrDefault();
        if (next is not null)
        {
            var id = S(next, "id");
            _blindActions.Add(id);
            return new { actionId = id, attempt = (object?)null };
        }
        var citations = _requiredEvidenceIds.Where(_assets.ContainsKey).SelectMany(id =>
            _assets[id].Support.Select(support => new { sourceId = id, quote = support.Excerpt })).ToArray();
        return new
        {
            actionId = "submit",
            attempt = new
            {
                suspectId = "suspect.jordan", evidenceIds = _requiredEvidenceIds, analysisIds = _requiredAnalysisIds,
                answers = _answers.OrderBy(p => p.Key).Select(p => new { questionId = p.Key, optionId = p.Value }),
                citations, rationale = "The cited independent readable records support the observed attribution and action."
            }
        };
    }

    private static EvidenceAsset Asset(string id, string type, string title, string layout, List<CaseContentSupport> support,
        string visibility, string role = EvidenceRoles.Primary, string? subject = null, string? purpose = null) => new()
    {
        Id = id, Type = type, Title = title, Description = "A source-scoped evidentiary material.",
        Category = "Document", Visibility = visibility, EvidenceRole = role, SubjectSuspectId = subject, ImagePurpose = purpose,
        Body = type == "photo" ? "A contextual photograph; no readable proof is asserted." : null,
        BodyDoc = type == "photo" ? null : Document(layout, Printed(support)),
        Support = type == "photo" ? new() : support
    };

    private static EvidenceDocument Document(string layout, string text) => new()
    {
        Layout = layout, Language = "en-US", Title = "Recorded source material",
        Sections = [new EvidenceSection { Kind = "narrative", Heading = "Recorded observations", Text = text }]
    };

    private static List<CaseContentSupport> Support(JsonNode context) =>
        V(context, "observations")!.AsArray().Select(o => new CaseContentSupport
        {
            ReferenceId = S(o!, "id"), Excerpt = S(o!, "statement")
        }).ToList();

    private static string Printed(IReadOnlyList<CaseContentSupport> support) => support.Count == 0
        ? "Administrative source index. No additional factual observations are asserted."
        : string.Join("\n", support.Select(s => s.Excerpt));
    private static JsonObject Context(string user) => FirstJson(user).AsObject();
    private static JsonNode Block(string user, string heading) => FirstJson(user, heading);
    private static JsonNode FirstJson(string user, string? heading = null)
    {
        var start = heading is null ? 0 : user.IndexOf(heading, StringComparison.Ordinal) + heading.Length;
        var brace = user.IndexOf('{', start);
        var bracket = user.IndexOf('[', start);
        var index = brace < 0 ? bracket : bracket < 0 ? brace : Math.Min(brace, bracket);
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(user[index..]));
        return JsonNode.Parse(ref reader)!;
    }
    private static JsonNode? V(JsonNode node, string key) => node.AsObject()
        .FirstOrDefault(p => p.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value;
    private static string S(JsonNode node, string key) => N(node, key) ?? string.Empty;
    private static string? N(JsonNode node, string key) => V(node, key)?.GetValue<string>();
    private static JsonNode? Find(JsonNode node, string key)
    {
        if (node is JsonObject obj)
        {
            var direct = V(obj, key);
            if (direct is not null) return direct;
            return obj.Select(p => p.Value).Where(n => n is JsonObject or JsonArray).Select(n => Find(n!, key)).FirstOrDefault(n => n is not null);
        }
        if (node is JsonArray array)
            return array.Where(n => n is JsonObject or JsonArray).Select(n => Find(n!, key)).FirstOrDefault(n => n is not null);
        return null;
    }

    public Task<byte[]> GenerateImageAsync(string prompt, CancellationToken cancellationToken = default)
    {
        OnImage?.Invoke();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ImageBytes);
    }
    public Task<byte[]> GenerateImageWithReferenceAsync(string prompt, byte[] referenceImage, byte[]? maskImage = null,
        CancellationToken cancellationToken = default) => GenerateImageAsync(prompt, cancellationToken);
    public Task<LLMResponse> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
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

    public Task<IReadOnlyDictionary<string, string>> ReadPreparedDocumentsAsync(
        IEnumerable<EvidenceAsset> assets, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        IReadOnlyDictionary<string, string> text = assets.Where(a => a.Type != "photo" && a.BodyDoc is not null)
            .ToDictionary(a => a.Id, CaseBibleAuthorship.Text, StringComparer.Ordinal);
        return Task.FromResult(text);
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
            Skipped = Skip ? 1 : 0, Files = files,
            ReadableTexts = items.Where(asset => asset.Type != "photo" && asset.BodyDoc is not null)
                .ToDictionary(asset => asset.Id, CaseBibleAuthorship.Text, StringComparer.Ordinal),
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

    public Task<string?> GetPublicationETagAsync(string caseId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<string?>(null);
    }

    public Task<int> PublishAsync(CaseBundle bundle, CancellationToken ct = default)
    {
        Calls++;
        OnPublish?.Invoke(bundle.CaseJsonPath, bundle.BundleDirectory);
        ct.ThrowIfCancellationRequested();
        if (Exception is not null) throw Exception;
        return Task.FromResult(ReturnZero ? 0 : bundle.Manifest.Files.Count + 2);
    }
}
