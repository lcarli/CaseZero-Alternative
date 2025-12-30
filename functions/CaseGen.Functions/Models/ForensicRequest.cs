namespace CaseGen.Functions.Models;

public class ForensicRequest
{
    public int Id { get; set; }
    public string CaseId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string InputAssetId { get; set; } = string.Empty;
    public string InputAssetName { get; set; } = string.Empty;
    public string AnalysisType { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime EstimatedCompletionTime { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "pending";
    public string? ResultDocumentId { get; set; }
    public string? ResultEmailId { get; set; }
    public string? Notes { get; set; }
}
