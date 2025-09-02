using Xunit;
using CaseGen.Functions.Models;

namespace CaseGen.Functions.Tests;

public class CaseGenerationStepsTests
{
    [Fact]
    public void AllSteps_ShouldContainRenderImagesStep()
    {
        // Arrange & Act
        var steps = CaseGenerationSteps.AllSteps;

        // Assert
        Assert.Contains(CaseGenerationSteps.RenderImages, steps);
        Assert.Equal(12, steps.Length); // Plan, Expand, Design, GenDocs, GenMedia, RenderDocs, RenderImages, Normalize, Index, RuleValidate, RedTeam, Package
    }

    [Fact]
    public void RenderImagesStep_ShouldHaveCorrectValue()
    {
        // Arrange & Act & Assert
        Assert.Equal("RenderImages", CaseGenerationSteps.RenderImages);
    }

    [Fact]
    public void AllSteps_ShouldBeInCorrectOrder()
    {
        // Arrange & Act
        var steps = CaseGenerationSteps.AllSteps;

        // Assert - Verify the order is logical for the pipeline
        Assert.Equal(CaseGenerationSteps.Plan, steps[0]);
        Assert.Equal(CaseGenerationSteps.Expand, steps[1]);
        Assert.Equal(CaseGenerationSteps.Design, steps[2]);
        Assert.Equal(CaseGenerationSteps.GenDocs, steps[3]);
        Assert.Equal(CaseGenerationSteps.GenMedia, steps[4]);
        Assert.Equal(CaseGenerationSteps.RenderDocs, steps[5]);
        Assert.Equal(CaseGenerationSteps.RenderImages, steps[6]); // New step after RenderDocs
        Assert.Equal(CaseGenerationSteps.Normalize, steps[7]);
        Assert.Equal(CaseGenerationSteps.Index, steps[8]);
        Assert.Equal(CaseGenerationSteps.RuleValidate, steps[9]);
        Assert.Equal(CaseGenerationSteps.RedTeam, steps[10]);
        Assert.Equal(CaseGenerationSteps.Package, steps[11]);
    }
}