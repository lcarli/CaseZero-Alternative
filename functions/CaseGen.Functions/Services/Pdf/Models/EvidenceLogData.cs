using System;
using System.Collections.Generic;

namespace CaseGen.Functions.Services.Pdf.Models;

public class EvidenceLogData
{
    public string? DocumentId { get; set; }
    public string? DocumentTitle { get; set; }
    public string? DocumentType { get; set; }
    public string? CaseId { get; set; }
    public string? Summary { get; set; }
    public IReadOnlyList<EvidenceItemEntry> Items { get; set; } = Array.Empty<EvidenceItemEntry>();
    public string? LabelingAndStorage { get; set; }
    public IReadOnlyList<CustodyEntry> CustodyEntries { get; set; } = Array.Empty<CustodyEntry>();
    public string? Remarks { get; set; }
}

public class EvidenceItemEntry
{
    public string ItemId { get; set; } = string.Empty;
    public string CollectedAt { get; set; } = string.Empty;
    public string CollectedBy { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Storage { get; set; } = string.Empty;
    public string Transfers { get; set; } = string.Empty;
}

public class CustodyEntry
{
    public string ItemId { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}
