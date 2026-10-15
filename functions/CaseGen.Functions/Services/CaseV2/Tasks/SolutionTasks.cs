using System.Text.Json;
using System.Text.Json.Serialization;
using CaseGen.Functions.Models.CaseV2;
using Microsoft.Extensions.Logging;
using static CaseGen.Functions.Services.CaseV2.TaskContractException;

namespace CaseGen.Functions.Services.CaseV2.Tasks;

public class SolutionSkeletonTask(ILLMProvider llm, ILogger logger)
{
    private sealed class Output
    {
        [JsonPropertyName("requiredEvidenceIds")] public List<string> RequiredEvidenceIds { get; set; } = new();
        [JsonPropertyName("requiredAnalysisIds")] public List<string> RequiredAnalysisIds { get; set; } = new();
        [JsonPropertyName("questionTopics")] public List<QuestionTopic> QuestionTopics { get; set; } = new();
    }

    public async Task RunAsync(CaseDraft draft, CancellationToken ct)
    {
        var profile = DifficultyProfileCatalog.Get(draft);
        var materials = CaseBibleAuthorship.ReadableMaterials(draft)
            .Where(m => m.Id.StartsWith("asset.", StringComparison.Ordinal) && m.Support.Count > 0
                && !draft.AssetStubs.Any(s => s.Id == m.Id && s.EvidenceRole == EvidenceRoles.Contextual)).ToList();
        var analysisIds = draft.ForensicFull.Where(f => f.Findings)
            .Select(f => $"{f.InputAssetId}:{f.AnalysisType}").ToHashSet(StringComparer.Ordinal);
        var prompts = AgentPromptCatalog.Default;
        var system = prompts.RenderSystem("SolutionSkeleton", new Dictionary<string, object?>
        {
            ["language"] = ForensicMethodCatalog.NormalizeLanguage(draft.Request.Language),
            ["is_rookie"] = profile.AllEvidenceInitial.ToString().ToLowerInvariant()
        });
        var user = prompts.RenderUser("SolutionSkeleton", new Dictionary<string, object?>
        {
            ["context_json"] = JsonSerializer.Serialize(new
            {
                materials, analysisIds, maximumRequiredAnalyses = Math.Max(0, analysisIds.Count - profile.Topology.MinOptionalAnalyses)
            }, CaseDraft.CanonicalJsonOptions)
        });
        var output = await TaskRunner.RunStructuredAsync<Output>(llm, logger, "SolutionSkeleton", system, user,
            MicrotaskSchemas.SolutionSkeleton, ct, validate: result =>
            {
                var readableIds = materials.Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
                MicrotaskValidation.Unique(result.RequiredEvidenceIds, "required evidence");
                MicrotaskValidation.Unique(result.RequiredAnalysisIds, "required analyses");
                MicrotaskValidation.Unique(result.QuestionTopics.Select(t => t.Id), "question topics");
                Require(result.RequiredEvidenceIds.Count > 0 && result.RequiredEvidenceIds.All(readableIds.Contains),
                    "Required evidence must exist, be reachable and have readable observation support; image prompts and descriptions are not proof.");
                Require(result.RequiredAnalysisIds.All(analysisIds.Contains)
                    && result.RequiredAnalysisIds.Count <= Math.Max(0, analysisIds.Count - profile.Topology.MinOptionalAnalyses),
                    "Required analyses must have real findings and preserve the difficulty's optional-analysis budget.");
                Require(!profile.AllEvidenceInitial || result.RequiredAnalysisIds.Count == 0, "Rookie cannot require interactive analysis.");
                foreach (var topic in result.QuestionTopics)
                    Require(topic.SupportingEvidenceIds.Count > 0 && topic.SupportingEvidenceIds.All(readableIds.Contains),
                        $"Question topic '{topic.Id}' requires real reachable readable source materials.");
            });
        draft.RequiredEvidenceIds = output.RequiredEvidenceIds;
        draft.RequiredAnalysisIds = output.RequiredAnalysisIds;
        draft.QuestionTopics = output.QuestionTopics;
    }
}

public class QuestionTask(ILLMProvider llm, ILogger logger)
{
    public async Task<SolutionQuestion> RunAsync(CaseDraft draft, QuestionTopic topic, CancellationToken ct)
    {
        var previous = draft.Questions.SingleOrDefault(q => q.Id == topic.Id);
        var prompts = AgentPromptCatalog.Default;
        var system = prompts.RenderSystem("Question", new Dictionary<string, object?>
        {
            ["language"] = ForensicMethodCatalog.NormalizeLanguage(draft.Request.Language), ["topic"] = topic.Topic,
            ["question_id"] = topic.Id, ["weight"] = topic.Weight,
            ["supporting_evidence_ids"] = string.Join(", ", topic.SupportingEvidenceIds)
        });
        var user = prompts.RenderUser("Question", new Dictionary<string, object?>
        {
            ["supporting_materials_json"] = CaseBibleAuthorship.Materials(draft, topic.SupportingEvidenceIds)
        });
        if (previous is not null)
            user += "\n# Existing public question; preserve its ID, option IDs and labels exactly\n"
                + JsonSerializer.Serialize(new { previous.Id, previous.Prompt, previous.Options }, CaseDraft.CanonicalJsonOptions);
        return await TaskRunner.RunStructuredAsync<SolutionQuestion>(llm, logger, $"Question:{topic.Id}", system, user,
            MicrotaskSchemas.Question, ct, validate: question =>
            {
                MicrotaskValidation.Question(question, topic);
                CaseBibleAuthorship.SupportFromMaterials(draft, question.Support, topic.SupportingEvidenceIds, question.Id);
                var answer = question.Options.Single(o => o.Id == question.CorrectOptionId).Label;
                Require(question.Support.Any(s => s.Excerpt.Contains(answer, StringComparison.OrdinalIgnoreCase)),
                    "Correct option must quote a fact from the actual readable supporting materials. Do not invent or silently replace an answer.");
                if (previous is not null)
                    Require(question.CorrectOptionId == previous.CorrectOptionId
                        && question.Options.Select(o => (o.Id, o.Label)).SequenceEqual(previous.Options.Select(o => (o.Id, o.Label))),
                        "Question repair must preserve every existing option ID, label and the original supported answer.");
            });
    }
}

public class ExplanationTask(ILLMProvider llm, ILogger logger)
{
    private sealed class Output : CaseAuthoredContent
    {
        [JsonPropertyName("explanation")] public string Explanation { get; set; } = string.Empty;
    }

    public async Task RunAsync(CaseDraft draft, CancellationToken ct)
    {
        var materials = CaseBibleAuthorship.ReadableMaterials(draft);
        var prompts = AgentPromptCatalog.Default;
        var system = prompts.RenderSystem("Explanation", new Dictionary<string, object?>
        {
            ["language"] = ForensicMethodCatalog.NormalizeLanguage(draft.Request.Language)
        });
        var user = prompts.RenderUser("Explanation", new Dictionary<string, object?>
        {
            ["materials_json"] = JsonSerializer.Serialize(materials, CaseDraft.CanonicalJsonOptions),
            ["required_evidence_ids"] = string.Join(", ", draft.RequiredEvidenceIds),
            ["required_analysis_ids"] = string.Join(", ", draft.RequiredAnalysisIds)
        });
        var output = await TaskRunner.RunStructuredAsync<Output>(llm, logger, "Explanation", system, user,
            MicrotaskSchemas.Explanation, ct, validate: result =>
            {
                CaseBibleAuthorship.SupportFromMaterials(draft, result.Support, materials.Select(m => m.Id), "explanation");
                foreach (var entry in result.Support)
                    Require(result.Explanation.Contains(entry.Excerpt, StringComparison.Ordinal),
                        "Explanation must actually quote its cited readable support, not merely attach provenance.");
            });
        draft.Explanation = output.Explanation;
        draft.ExplanationSupport = output.Support;
    }
}
