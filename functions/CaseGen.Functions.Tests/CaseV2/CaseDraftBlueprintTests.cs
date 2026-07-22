using CaseGen.Functions.Services.CaseV2;
using Xunit;

namespace CaseGen.Functions.Tests.CaseV2;

public class CaseDraftBlueprintTests
{
    [Fact]
    public void ToSummaryJson_IncludesCanonicalBlueprint()
    {
        var draft = new CaseDraft
        {
            CaseId = "case_test",
            CulpritId = "suspect.culprit",
            Blueprint = new InvestigationBlueprint
            {
                CrimeMechanism = "Legitimate access was used to alter a delivery handoff.",
                CulpritObjective = "Hide an embezzlement before a scheduled audit.",
                CaseHook = "A loading-bay sensor recorded the handoff out of sequence.",
                ClueLadder =
                {
                    new CanonicalClue
                    {
                        Id = "clue.sensor_gap",
                        Discovery = "A sensor gap",
                        Inference = "The normal handoff sequence was bypassed",
                        SupportsSuspectId = "suspect.culprit",
                        Strength = "decisive",
                        SourceType = "digital"
                    }
                }
            }
        };

        var summary = draft.ToSummaryJson();

        Assert.Contains("\"investigationBlueprint\"", summary);
        Assert.Contains("\"clue.sensor_gap\"", summary);
    }
}
