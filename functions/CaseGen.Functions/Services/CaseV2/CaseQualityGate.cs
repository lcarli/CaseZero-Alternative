using CaseGen.Functions.Models.CaseV2;
using CaseGen.Functions.Services.CaseV2.Tasks;

namespace CaseGen.Functions.Services.CaseV2;

public static class CaseQualityGate
{
    public static CaseQualityReport Evaluate(
        GenerateCaseV2Request request,
        bool publisherConfigured,
        IEnumerable<string> validationErrors,
        IEnumerable<string> consistencyErrors,
        RedTeamTask.Report? review,
        int revision = 0)
    {
        var quality = new CaseQualityReport
        {
            Revision = revision,
            ReviewedRevision = review?.Revision,
            Preview = !request.WriteToDisk,
            PersistenceRequested = request.WriteToDisk,
            PublicationRequested = request.WriteToDisk && publisherConfigured
        };
        var consistency = consistencyErrors.ToHashSet(StringComparer.Ordinal);
        foreach (var error in validationErrors.Where(error => !consistency.Contains(error)))
            quality.Block("schema_invalid", "schema", error);
        foreach (var error in consistency)
            quality.Block("consistency_error", "consistency", error);

        if (review is null || !review.Succeeded)
        {
            quality.Block(
                review?.ErrorCode ?? "red_team_unavailable",
                "review",
                review?.ErrorMessage ?? "The red-team review did not complete successfully.");
        }
        else
        {
            if (review.Verdict != "ok")
                quality.Block(
                    review.Verdict == "reject" ? "red_team_rejected" : "red_team_needs_review",
                    "review",
                    $"Red-team verdict: {review.Verdict}.");

            foreach (var finding in review.Findings)
                quality.Issues.Add(new CaseQualityIssue(
                    "red_team_finding",
                    finding.Severity,
                    finding.Area,
                    finding.Reference,
                    finding.Issue,
                    finding.Severity is "medium" or "high"));
        }

        if (review?.Succeeded == true && review.Revision != revision)
            quality.Block(
                "review_revision_mismatch",
                "review",
                "The review does not belong to the canonical draft revision.");

        quality.Approved = !quality.HasBlockingIssues;
        return quality;
    }

    public static void CheckRendering(CaseQualityReport quality, AssetRenderingReport report)
    {
        foreach (var error in report.Errors)
            quality.Block("asset_render_failed", "rendering", error);
        if (report.Skipped > 0)
            quality.Block(
                "assets_skipped",
                "rendering",
                $"{report.Skipped} asset(s) were not rendered as usable evidence files.");
    }

}
