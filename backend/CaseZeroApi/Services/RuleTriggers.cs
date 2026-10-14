namespace CaseZeroApi.Services;

public abstract record RuleTrigger;
public sealed record ForensicsCompleteTrigger(string InputAssetId, string AnalysisType) : RuleTrigger;
public sealed record AttachmentDownloadTrigger(string EmailId, string AssetId) : RuleTrigger;
public sealed record AssetViewedTrigger(string AssetId) : RuleTrigger;
public sealed record EmailOpenedTrigger(string EmailId) : RuleTrigger;
public sealed record SuspectViewedTrigger(string SuspectId) : RuleTrigger;
public sealed record TimeElapsedTrigger(int GameTimeMinutes) : RuleTrigger;

public sealed record ObservedCaseTrigger(string Type, string? AssetId = null, string? EmailId = null,
    string? SuspectId = null, string? AnalysisType = null)
{
    public static ObservedCaseTrigger? From(RuleTrigger trigger) => trigger switch
    {
        ForensicsCompleteTrigger f => new("forensics_complete", f.InputAssetId, AnalysisType: f.AnalysisType.ToUpperInvariant()),
        AttachmentDownloadTrigger a => new("attachment_download", a.AssetId, a.EmailId),
        AssetViewedTrigger a => new("asset_viewed", a.AssetId),
        EmailOpenedTrigger e => new("email_opened", EmailId: e.EmailId),
        SuspectViewedTrigger s => new("suspect_viewed", SuspectId: s.SuspectId),
        TimeElapsedTrigger => null,
        _ => throw new InvalidDataException("Unsupported progression trigger.")
    };
}
