using System.Text.Json;
using System.Text.Json.Serialization;
using CaseGen.Functions.Services.CaseV2.Tasks;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.CaseV2;

public sealed class BlindEvidenceCitation
{
    [JsonPropertyName("sourceId")] public string SourceId { get; set; } = string.Empty;
    [JsonPropertyName("quote")] public string Quote { get; set; } = string.Empty;
}

public sealed class BlindInvestigationAttempt : SolverTask.Attempt
{
    [JsonPropertyName("citations")] public List<BlindEvidenceCitation> Citations { get; set; } = [];
}

public sealed class BlindInvestigationStep
{
    [JsonPropertyName("actionId")] public string ActionId { get; set; } = string.Empty;
    [JsonPropertyName("attempt")] public BlindInvestigationAttempt? Attempt { get; set; }
}

public sealed record BlindInvestigationResult(BlindInvestigationAttempt? Attempt, SolverDiagnosticTrace Trace, string? ErrorCode, string? Error);

public sealed class BlindPlayerInvestigator(ILLMProvider llm, ILogger logger)
{
    public async Task<BlindInvestigationResult> RunAsync(CaseDraft draft,
        IReadOnlyDictionary<string, string> readableTexts, CancellationToken ct)
    {
        var simulator = new RuntimePlayerSimulator(draft, readableTexts);
        var maximumSteps = Math.Max(24, draft.AssetFull.Count * 3 + draft.ResultAssets.Count * 3
            + draft.FollowUpEmails.Count * 2 + draft.ForensicFull.Count * 3 + draft.SuspectFull.Count + 8);
        var system = """
            BlindPlayerInvestigator
            Investigate as a player using ONLY the supplied public projection. Choose one offered actionId.
            The submit action is always allowed, but only submit when actual readable evidence supports a unique suspect.
            Do not guess withheld image content. You have no private culprit or answer key.
            On actions return {"actionId":"offered-id","attempt":null}.
            On submission return {"actionId":"submit","attempt":{"suspectId":"...","evidenceIds":["..."],"analysisIds":["..."],
            "answers":[{"questionId":"...","optionId":"..."}],"citations":[{"sourceId":"...","quote":"exact readable excerpt"}],
            "rationale":"supported reasoning"}}. Never return observationIds.
            Quotes must occur in inspected source text. Cite every evidence item and explain the chosen answers.
            """;
        try
        {
            for (var step = 0; step < maximumSteps; step++)
            {
                ct.ThrowIfCancellationRequested();
                var allowed = simulator.AvailableActions().Select(action => action.Id).Append("submit").ToArray();
                var schema = Schema(allowed);
                var result = await TaskRunner.RunStructuredAsync<BlindInvestigationStep>(
                    llm, logger, "BlindPlayerInvestigator", system, JsonSerializer.Serialize(simulator.Projection()),
                    schema, ct, validate: value => ValidateStep(value, simulator, draft));
                if (result.ActionId != "submit")
                {
                    simulator.Apply(result.ActionId);
                    continue;
                }
                var attempt = result.Attempt!;
                return new(attempt, Snapshot(simulator), null, null);
            }
            return new(null, Snapshot(simulator), "solver_action_budget_exhausted", "Player investigation exhausted its action budget without a supported submission.");
        }
        catch (Exception exception) when (exception is TaskContractException or JsonException
            or Azure.RequestFailedException or System.ClientModel.ClientResultException or HttpRequestException or TimeoutException)
        {
            logger.LogWarning(exception, "Blind player investigation failed for {CaseId}", draft.CaseId);
            return new(null, Snapshot(simulator), exception is TaskContractException ? "solver_invalid_submission" : "solver_unavailable",
                exception.Message);
        }
    }

    private static SolverDiagnosticTrace Snapshot(RuntimePlayerSimulator simulator) => new()
    {
        Entries = simulator.Trace.ToList(),
        FinalReachableSourceIds = simulator.ReadAssets.Concat(simulator.ReadEmails).Order(StringComparer.Ordinal).ToList()
    };

    private static void ValidateStep(BlindInvestigationStep value, RuntimePlayerSimulator simulator, CaseDraft draft)
    {
        if (value.ActionId != "submit")
        {
            if (value.Attempt is not null)
                throw new TaskContractException("An intermediate action cannot include a solution submission.");
            return;
        }
        var attempt = value.Attempt ?? throw new TaskContractException("A submission must include an attempted solution.");
        if (!draft.SuspectFull.Any(suspect => suspect.Id == attempt.SuspectId))
            throw new TaskContractException("Submission references an unknown suspect.");
        if (attempt.EvidenceIds.Count == 0 || attempt.EvidenceIds.Distinct(StringComparer.Ordinal).Count() != attempt.EvidenceIds.Count
            || attempt.EvidenceIds.Any(id => !simulator.ReadAssets.Contains(id)))
            throw new TaskContractException("Every submitted evidence ID must be distinct and inspected.");
        if (attempt.AnalysisIds.Any(id => !simulator.CompletedAnalyses.Contains(id)))
            throw new TaskContractException("Submitted analyses must have actually completed.");
        foreach (var answer in attempt.Answers)
            if (!draft.Questions.Any(question => question.Id == answer.QuestionId && question.Options.Any(option => option.Id == answer.OptionId)))
                throw new TaskContractException("Submission references an unavailable question or option.");
        foreach (var citation in attempt.Citations)
        {
            var text = simulator.ReadableSource(citation.SourceId);
            if (text is null || citation.Quote.Trim().Length < 12
                || !RenderedEvidenceText.Normalize(text).Contains(RenderedEvidenceText.Normalize(citation.Quote), StringComparison.Ordinal))
                throw new TaskContractException($"Citation {citation.SourceId} is not supported by inspected readable text.");
        }
        if (attempt.EvidenceIds.Any(id => !attempt.Citations.Any(citation => citation.SourceId == id)))
            throw new TaskContractException("Every submitted evidence item needs an exact readable citation; image prompts are not evidence.");
    }

    private static string Schema(IEnumerable<string> actions) => """
        {"type":"object","additionalProperties":false,"required":["actionId","attempt"],"properties":{
          "actionId":{"type":"string","enum":"$ACTIONS$"},
          "attempt":{"anyOf":[{"type":"null"},{"type":"object","additionalProperties":false,
            "required":["suspectId","evidenceIds","analysisIds","answers","citations","rationale"],"properties":{
              "suspectId":{"type":"string","minLength":1},
              "evidenceIds":{"type":"array","minItems":1,"uniqueItems":true,"items":{"type":"string"}},
              "analysisIds":{"type":"array","uniqueItems":true,"items":{"type":"string"}},
              "answers":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["questionId","optionId"],"properties":{
                "questionId":{"type":"string"},"optionId":{"type":"string"}}}},
              "citations":{"type":"array","minItems":1,"items":{"type":"object","additionalProperties":false,"required":["sourceId","quote"],"properties":{
                "sourceId":{"type":"string"},"quote":{"type":"string","minLength":12}}}},
              "rationale":{"type":"string","minLength":12}
            }}]}
        }}
        """.Replace("\"$ACTIONS$\"", JsonSerializer.Serialize(actions), StringComparison.Ordinal);
}
