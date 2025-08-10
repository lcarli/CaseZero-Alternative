using System.Text.Json;
using System.Text.Json.Serialization;

namespace CaseZeroApi.Services
{
    /// <summary>
    /// Service for generating detective cases using Azure Foundry AI
    /// </summary>
    public sealed class CaseGenerationService : ICaseGenerationService
    {
        private readonly LlmClient _llm;
        private readonly JsonSerializerOptions _json;
        private readonly ILogger<CaseGenerationService> _logger;
        private readonly string _casesBasePath;

        public CaseGenerationService(LlmClient llm, ILogger<CaseGenerationService> logger, IConfiguration configuration)
        {
            _llm = llm;
            _logger = logger;
            _casesBasePath = configuration.GetValue<string>("CasesBasePath") 
                ?? Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "cases");
            
            _json = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        #region Public API
        public async Task<CasePackage> GenerateCaseAsync(CaseSeed seed, GenerationOptions? options = null, CancellationToken ct = default)
        {
            options ??= GenerationOptions.Default;

            _logger.LogInformation("Starting case generation for {Title}", seed.Title);

            try
            {
                // 1) case.json
                var caseJson = await GenerateCaseJsonAsync(seed, ct);
                var ctx = new CaseContext(seed, caseJson);

                _logger.LogInformation("Generated case.json for case {CaseId}", ctx.CaseId);

                // 2) interrogatórios (um por suspeito; mínimo 1)
                var ints = await GenerateInterrogatoriosAsync(ctx, ct);

                // 3) relatórios investigativos
                var reports = await GenerateRelatoriosAsync(ctx, ct);

                // 4) laudos periciais
                var laudos = await GenerateLaudosAsync(ctx, ct);

                // 5) manifest de evidências + cadeia de custódia
                var manifest = await GenerateEvidenceManifestAsync(ctx, ct);

                // 6) prompts de imagem (rich captions por evidência/ambiente)
                var imagePrompts = await GenerateImagePromptsAsync(ctx, ct);

                var package = new CasePackage
                {
                    CaseId = ctx.CaseId,
                    CaseJson = caseJson,
                    Interrogatorios = ints,
                    Relatorios = reports,
                    Laudos = laudos,
                    EvidenceManifest = manifest,
                    ImagePrompts = imagePrompts
                };

                // Save the case to disk
                await SaveCasePackageAsync(package, ct);

                _logger.LogInformation("Successfully generated and saved case {CaseId}", ctx.CaseId);
                return package;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate case for {Title}", seed.Title);
                throw;
            }
        }

        public async Task<string> GenerateCaseJsonAsync(CaseSeed seed, CancellationToken ct = default)
        {
            var sys = PromptLibrary.SystemArchitect(seed);
            var user = PromptLibrary.UserArchitect(seed);

            var result = await _llm.ChatAsync(new[]
            {
                ChatMsg.System(sys),
                ChatMsg.User(user)
            }, new ChatParams
            {
                MaxTokens = 4000,
                ResponseFormat = ChatResponseFormat.JsonObject // tentamos JSON estrito; o cliente faz fallback se não suportado
            }, ct);

            // Opcional: aqui poderíamos validar o JSON contra um schema (ver TODO abaixo)
            return result;
        }

        public async Task<List<GeneratedDoc>> GenerateInterrogatoriosAsync(CaseContext ctx, CancellationToken ct = default)
        {
            var docs = new List<GeneratedDoc>();
            var suspects = CaseUtils.ExtractIds(ctx.CaseJson, "suspects");
            if (suspects.Count == 0) suspects.Add("SUS-001");

            foreach (var sid in suspects)
            {
                var sys = PromptLibrary.SystemForense();
                var user = PromptLibrary.UserInterrogatorio(ctx, sid);
                var content = await _llm.ChatAsync(new[] { ChatMsg.System(sys), ChatMsg.User(user) }, new ChatParams
                {
                    MaxTokens = 8000
                }, ct);

                docs.Add(new GeneratedDoc
                {
                    Id = $"DOC-INT-{sid}",
                    FileName = $"03_interrogatorios/INT-{sid}-sessao-01.md",
                    Content = content,
                    Kind = DocumentKind.Interrogatorio
                });
            }
            return docs;
        }

        public async Task<List<GeneratedDoc>> GenerateRelatoriosAsync(CaseContext ctx, CancellationToken ct = default)
        {
            var sys = PromptLibrary.SystemForense();
            var user = PromptLibrary.UserRelatorio(ctx);
            var content = await _llm.ChatAsync(new[] { ChatMsg.System(sys), ChatMsg.User(user) }, new ChatParams
            {
                MaxTokens = 6000
            }, ct);

            return new List<GeneratedDoc>
            {
                new GeneratedDoc
                {
                    Id = $"DOC-REL-{ctx.CaseId}",
                    FileName = $"04_relatorios/REL-{ctx.CaseId}.md",
                    Content = content,
                    Kind = DocumentKind.Relatorio
                }
            };
        }

        public async Task<List<GeneratedDoc>> GenerateLaudosAsync(CaseContext ctx, CancellationToken ct = default)
        {
            // Gera 2 laudos padrão (ANL-###) como exemplo: análise de CFTV e análise de logs de acesso
            var outputs = new List<GeneratedDoc>();
            var laudos = new[]
            {
                (id: "ANL-014", tema: "Análise de CFTV e microabertura do cofre"),
                (id: "ANL-005", tema: "Logs de controle de acesso e sincronização NTP")
            };

            foreach (var (id, tema) in laudos)
            {
                var sys = PromptLibrary.SystemPerito();
                var user = PromptLibrary.UserLaudo(ctx, id, tema);
                var content = await _llm.ChatAsync(new[] { ChatMsg.System(sys), ChatMsg.User(user) }, new ChatParams
                {
                    MaxTokens = 5500
                }, ct);

                outputs.Add(new GeneratedDoc
                {
                    Id = id,
                    FileName = $"05_laudos/{id}.md",
                    Content = content,
                    Kind = DocumentKind.Laudo
                });
            }

            return outputs;
        }

        public async Task<GeneratedDoc> GenerateEvidenceManifestAsync(CaseContext ctx, CancellationToken ct = default)
        {
            var sys = PromptLibrary.SystemForense();
            var user = PromptLibrary.UserEvidences(ctx);
            var content = await _llm.ChatAsync(new[] { ChatMsg.System(sys), ChatMsg.User(user) }, new ChatParams
            {
                MaxTokens = 4000
            }, ct);

            return new GeneratedDoc
            {
                Id = $"EVD-MANIFEST-{ctx.CaseId}",
                FileName = $"02_evidencias/EVIDENCIAS-{ctx.CaseId}.md",
                Content = content,
                Kind = DocumentKind.Manifest
            };
        }

        public async Task<List<ImagePrompt>> GenerateImagePromptsAsync(CaseContext ctx, CancellationToken ct = default)
        {
            var sys = PromptLibrary.SystemDiretorArte();
            var user = PromptLibrary.UserImagePrompts(ctx);
            var content = await _llm.ChatAsync(new[] { ChatMsg.System(sys), ChatMsg.User(user) }, new ChatParams
            {
                MaxTokens = 4500
            }, ct);

            // Esperamos JSON com uma lista de prompts
            try
            {
                var list = JsonSerializer.Deserialize<List<ImagePrompt>>(content, _json) ?? new List<ImagePrompt>();
                return list;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize image prompts, returning empty list");
                return new List<ImagePrompt>();
            }
        }
        #endregion

        #region Private Methods
        private async Task SaveCasePackageAsync(CasePackage package, CancellationToken ct = default)
        {
            var caseDir = Path.Combine(_casesBasePath, package.CaseId);
            Directory.CreateDirectory(caseDir);

            // Save case.json
            var caseJsonPath = Path.Combine(caseDir, "case.json");
            await File.WriteAllTextAsync(caseJsonPath, package.CaseJson, ct);

            // Save all generated documents
            var allDocs = new List<GeneratedDoc>();
            allDocs.AddRange(package.Interrogatorios);
            allDocs.AddRange(package.Relatorios);
            allDocs.AddRange(package.Laudos);
            if (package.EvidenceManifest != null)
                allDocs.Add(package.EvidenceManifest);

            foreach (var doc in allDocs)
            {
                var docPath = Path.Combine(caseDir, doc.FileName);
                var docDir = Path.GetDirectoryName(docPath);
                if (!string.IsNullOrEmpty(docDir))
                {
                    Directory.CreateDirectory(docDir);
                }
                await File.WriteAllTextAsync(docPath, doc.Content, ct);
            }

            // Save image prompts as JSON
            if (package.ImagePrompts.Any())
            {
                var imagePromptsPath = Path.Combine(caseDir, "image_prompts.json");
                var imagePromptsJson = JsonSerializer.Serialize(package.ImagePrompts, _json);
                await File.WriteAllTextAsync(imagePromptsPath, imagePromptsJson, ct);
            }

            _logger.LogInformation("Saved case package to {CaseDir}", caseDir);
        }
        #endregion
    }
}