using System;
using System.Collections.Generic;

namespace CaseGen.Functions.Services.Pdf.Models;

public class WitnessStatementData
{
    public string? DocumentId { get; set; }
    public string? DocumentTitle { get; set; }
    public string? DocumentType { get; set; }
    public string? CaseId { get; set; }
    public string? WitnessName { get; set; }
    public string? WitnessRole { get; set; }
    public DateTimeOffset? StatementRecordedAt { get; set; }
    public string? Identification { get; set; }
    public string? Narrative { get; set; }
    public IReadOnlyList<WitnessTimelineEntry> Timeline { get; set; } = Array.Empty<WitnessTimelineEntry>();
    public IReadOnlyList<string> References { get; set; } = Array.Empty<string>();
    public WitnessSignatureInfo Signature { get; set; } = new();
}

public class WitnessTimelineEntry
{
    public DateTimeOffset? Timestamp { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class WitnessSignatureInfo
{
    public string? Name { get; set; }
    public string? Role { get; set; }
    public DateTimeOffset? SignedAt { get; set; }
}
