using System;
using System.Collections.Generic;

namespace CaseGen.Functions.Services.Pdf.Models;

public class ForensicsReportData
{
    public string? DocumentId { get; set; }
    public string? DocumentTitle { get; set; }
    public string? DocumentType { get; set; }
    public string? CaseId { get; set; }
    public string? LabName { get; set; }
    public string? ExaminerName { get; set; }
    public string? ExaminerTitle { get; set; }
    public DateTimeOffset? ExaminationDate { get; set; }
    public string? ExaminationTime { get; set; }
    public string? ObjectiveSummary { get; set; }
    public string? Procedures { get; set; }
    public string? Results { get; set; }
    public string? Interpretation { get; set; }
    public string? ObservationsSummary { get; set; }
    public string? PhotoReferenceId { get; set; }
    public string? PhotoDescription { get; set; }
    public IReadOnlyList<ForensicsChainEntry> ChainOfCustody { get; set; } = Array.Empty<ForensicsChainEntry>();
    public string? Findings { get; set; }
    public string? Limitations { get; set; }
}

public class ForensicsChainEntry
{
    public DateTimeOffset? Timestamp { get; set; }
    public string Description { get; set; } = string.Empty;
}
