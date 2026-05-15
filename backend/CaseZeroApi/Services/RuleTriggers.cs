namespace CaseZeroApi.Services;

public abstract record RuleTrigger;
public sealed record ForensicsCompleteTrigger(string InputAssetId, string AnalysisType) : RuleTrigger;
public sealed record AttachmentDownloadTrigger(string EmailId, string AssetId) : RuleTrigger;
public sealed record AssetViewedTrigger(string AssetId) : RuleTrigger;
public sealed record EmailOpenedTrigger(string EmailId) : RuleTrigger;
public sealed record SuspectViewedTrigger(string SuspectId) : RuleTrigger;
public sealed record TimeElapsedTrigger(int GameTimeMinutes) : RuleTrigger;
