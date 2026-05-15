using System;
using System.Collections.Generic;

namespace CaseGen.Functions.Services.Pdf.Models;

public class PoliceReportData
{
    public string? DocumentId { get; set; }
    public string? DocumentTitle { get; set; }
    public string? DocumentType { get; set; }
    public string? CaseId { get; set; }
    public string? ReportNumber { get; set; }
    public DateTimeOffset? ReportDateTime { get; set; }
    public string? Unit { get; set; }
    public string? OfficerName { get; set; }
    public string? OfficerBadge { get; set; }
    public string? Summary { get; set; }
    public List<PoliceReportTimelineEntry> Timeline { get; set; } = new();
    public List<PoliceReportPerson> Persons { get; set; } = new();
    public List<string> EvidenceIds { get; set; } = new();
    public string? SceneDescription { get; set; }
    public string? Assessment { get; set; }
    public List<string> NextSteps { get; set; } = new();
}

public class PoliceReportTimelineEntry
{
    public DateTimeOffset? Timestamp { get; set; }
    public string Summary { get; set; } = string.Empty;
}

public class PoliceReportPerson
{
    public string Description { get; set; } = string.Empty;
}
