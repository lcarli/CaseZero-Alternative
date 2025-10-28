# 📄 PDF Case File Improvement Roadmap

> **Goal:** Transform case file PDFs into realistic police/forensic documents inspired by authentic US cold case files (FBI, NCMEC, local PDs).

---

## 📍 Current State Analysis

### Existing Implementation
**File:** `backend/CaseGen.Functions/Services/PdfRenderingService.cs`

**Technology Stack:**
- **QuestPDF** (Community License) for PDF generation
- **Markdown** as intermediate format (JSON → Markdown → PDF)
- **Document Types Supported:**
  - `police_report` - Police incident reports
  - `forensics_report` - Lab analysis reports
  - `interview` - Witness/suspect interviews
  - `evidence_log` - Evidence inventory
  - `general` - Fallback generic format

**Current Features:**
- ✅ Basic letterhead with classification band
- ✅ Document type labels
- ✅ Watermarks (CONFIDENTIAL)
- ✅ Page numbering with classification footer
- ✅ Multiple document type renderers
- ✅ Table rendering from Markdown
- ✅ Header/footer with case metadata

**Current Limitations:**
- ❌ Generic institutional letterhead (not police-specific)
- ❌ No agency seal/logo
- ❌ Simple typography (lacks police report authenticity)
- ❌ Minimal administrative elements
- ❌ No evidence photo integration
- ❌ No chain of custody forms
- ❌ No security markings beyond watermark
- ❌ No document stamps/signatures
- ❌ Limited multi-language support

---

## 🎯 Phase 1: Research & Foundation (1-2 days)

### Task 1.1: Analyze Current Code Structure
**Files to Review:**
- `backend/CaseGen.Functions/Services/PdfRenderingService.cs` (lines 1-520)
- `backend/CaseGen.Functions/Services/IServices.cs` (IPdfRenderingService interface)
- `backend/CaseGen.Functions/Functions/TestPdfFunction.cs` (test endpoint)

**Analysis Checklist:**
- [ ] Document QuestPDF API usage patterns
- [ ] Map current rendering methods:
  - [ ] `GenerateRealisticPdf()` (main entry point)
  - [ ] `BuildLetterhead()` (header)
  - [ ] `BuildClassificationBand()` (security banner)
  - [ ] `RenderByType()` (dispatcher)
  - [ ] `RenderPoliceReport()`, `RenderForensicsReport()`, etc.
  - [ ] `RenderMarkdownContent()` (content parser)
  - [ ] `RenderTable()` (table formatting)
- [ ] Identify font usage (current: default QuestPDF fonts)
- [ ] Document color scheme (Colors.Grey.Darken2, etc.)
- [ ] Map storage paths (bundles container structure)

**Deliverable:** `docs/PDF_CURRENT_ARCHITECTURE.md` with:
- Architecture diagram (JSON → Service → QuestPDF → PDF)
- Method call hierarchy
- Document type flow chart
- Storage structure

---

### Task 1.2: Research Real Cold Case File Formats
**Research Sources:**
1. **FBI Case Files:**
   - Freedom of Information Act (FOIA) released documents
   - [FBI Vault](https://vault.fbi.gov/) - declassified cases
   - Look for: UNSUB files, kidnapping cases, homicides

2. **NCMEC (National Center for Missing & Exploited Children):**
   - Missing person poster formats
   - Case summary layouts
   - Photo evidence sheets

3. **Local Police Departments:**
   - Boston Strangler case files (Boston PD)
   - Golden State Killer documents (Sacramento/LA)
   - Zodiac Killer files (San Francisco PD)
   - Public records requests from major PDs

4. **Historical Cold Case Archives:**
   - Texas Ranger Unsolved Homicides archive
   - NamUs (National Missing Persons Database)
   - Cold Case Foundation samples

**Elements to Document:**

#### Header/Cover Page Elements:
- [ ] Agency seal placement and size (typically 1-2 inches, top-left or center)
- [ ] Case number formats:
  - FBI: `###-###-####` (field office - case category - sequential)
  - Local PD: `PD-YYYY-CFS-###` or `CASE-YYYY-###`
- [ ] Classification banners:
  - `CONFIDENTIAL - LAW ENFORCEMENT SENSITIVE`
  - `RESTRICTED - FOR OFFICIAL USE ONLY`
  - `UNCLASSIFIED - PUBLIC RELEASE`
- [ ] Date stamps: `RECEIVED`, `FILE OPENED`, `REVIEWED BY`
- [ ] Investigator assignment fields
- [ ] Distribution lists (carbon copy notation)
- [ ] Barcodes/tracking numbers

#### Typography & Styling:
- [ ] Font families used:
  - **Courier/Courier New** (typewriter effect for old reports)
  - **Arial** (modern reports)
  - **Times New Roman** (formal documents)
  - **OCR-A/OCR-B** (for forms/codes)
- [ ] Font sizes: 10-12pt body, 14-18pt headers
- [ ] Line spacing: 1.0-1.5 (single to one-and-a-half)
- [ ] Margins: 1 inch standard, 0.75 inch for forms
- [ ] Field labels: ALL CAPS, bold, or underlined

#### Evidence Sections:
- [ ] Evidence tag format: `E-001`, `EV-2024-001`, `ITEM #001`
- [ ] Chain of custody table columns:
  - Date/Time | Released By | Received By | Purpose | Location
- [ ] Photo evidence layout: 2x2 or 3x3 grids with captions
- [ ] Scale rulers in photos (ABFO #2 scale, metric/imperial)
- [ ] Evidence description format: Type, Description, Measurements, Condition

#### Suspect/Witness Pages:
- [ ] Mugshot placement: Top-right or centered
- [ ] Personal data table fields:
  - Name (Last, First, Middle)
  - DOB | Age | Sex | Race | Height | Weight
  - Eyes | Hair | Scars/Marks/Tattoos
  - Address | Phone | SSN (redacted)
- [ ] AKA (Also Known As) section
- [ ] Criminal history format
- [ ] Interview log table
- [ ] Fingerprint card placeholder boxes (10-print card)

#### Forms & Reports:
- [ ] Incident Report (IR) format:
  - Header with agency/date/time/location
  - Narrative section (free text)
  - Officer information
  - Supervisor approval signature block
- [ ] Evidence Collection Form:
  - Item number | Description | Collected by | Date/Time
  - Location found | Condition | Photos taken
  - Chain of custody initials
- [ ] Witness Statement Form:
  - Statement header (date/time/location)
  - Narrative (typed or handwritten)
  - Signature line with date
  - Witness contact info

#### Administrative Elements:
- [ ] Page headers: `Case #123-456-7890 | Page 1 of 25 | CONFIDENTIAL`
- [ ] Page footers: Classification + pagination
- [ ] File stamps: Red/blue oval stamps with `RECEIVED JAN 15 2024`
- [ ] Signature blocks: Name/Title/Date lines
- [ ] Revision tracking: Version numbers, edit dates
- [ ] Legal disclaimers: "This document contains confidential information..."

#### Redaction Patterns:
- [ ] Black redaction boxes (solid rectangles)
- [ ] `[REDACTED]` text stamps
- [ ] Exemption codes: `b(6)` (privacy), `b(7)(C)` (law enforcement)
- [ ] Partial redactions: Last 4 digits visible (SSN, phone)
- [ ] Redaction reason footnotes at page bottom

**Deliverable:** `docs/COLD_CASE_REFERENCE_GUIDE.md` with:
- Annotated screenshots/scans (10-15 examples)
- Element specifications (fonts, sizes, spacing)
- Color codes for stamps/classification bands
- Template wireframes for each document type

---

## 🏗️ Phase 2: Core Document Structure (3-4 days)

### Task 2.1: Design Professional Cover Page
**Implementation File:** `PdfRenderingService.cs` - New method `RenderCoverPage()`

**Requirements:**
```csharp
private void RenderCoverPage(PageDescriptor page, CaseMetadata metadata)
{
    // Agency seal/logo (fictional)
    // Classification banner (top: CONFIDENTIAL • LAW ENFORCEMENT SENSITIVE)
    // Case number (large, centered)
    // Case title
    // Date opened/received stamps
    // Investigator assignment table
    // Distribution list
    // Barcode for tracking
    // Classification footer
}
```

**Visual Elements:**
1. **Agency Seal:**
   - Create fictional police department seal using QuestPDF shapes
   - Badge shape with star/eagle motif
   - Text: "METRO POLICE DEPARTMENT" circular border
   - Placement: Top-center, 2 inches diameter

2. **Classification Banner:**
   - Full-width colored band
   - Colors: Red (#C62828) for CONFIDENTIAL, Blue (#1565C0) for RESTRICTED
   - White text, bold, 14pt
   - 8pt padding top/bottom

3. **Case Information Block:**
   ```
   CASE FILE

   Case Number: PD-2024-CFS-001
   Classification: Homicide - Cold Case
   Date Opened: January 15, 2024
   Current Status: Active Investigation

   Lead Investigator: Det. [NAME]
   Badge Number: [####]
   Unit: Major Crimes Division
   Contact: [PHONE] ext. [###]
   ```

4. **Document Stamps:**
   - Red oval stamp: "RECEIVED JAN 15 2024"
   - Blue rectangular stamp: "FILE OPENED"
   - Rotate stamps 5-10 degrees for authenticity

5. **Barcode:**
   - Bottom-right corner
   - Format: Case number encoded
   - Small disclaimer: "For official tracking use only"

**Code Structure:**
```csharp
// In PdfRenderingService.cs, add after line 127

private void RenderCoverPage(PageDescriptor page, string caseId, string title)
{
    page.Margin(36);
    
    // Classification banner
    page.Header().Background(Colors.Red.Darken2).Padding(8).AlignCenter()
        .Text("CONFIDENTIAL • LAW ENFORCEMENT SENSITIVE")
        .FontSize(14).Bold().FontColor(Colors.White);
    
    // Main content
    page.Content().Column(col =>
    {
        // Agency seal
        col.Item().AlignCenter().Element(e => RenderAgencySeal(e));
        
        col.Item().PaddingTop(20).AlignCenter().Text("CASE FILE")
            .FontSize(24).Bold();
        
        // Case info block
        col.Item().PaddingTop(20).Element(e => RenderCaseInfoBlock(e, caseId, title));
        
        // Stamps
        col.Item().PaddingTop(30).Element(e => RenderDocumentStamps(e));
        
        // Barcode
        col.Item().AlignRight().Element(e => RenderBarcode(e, caseId));
    });
    
    // Footer with classification
    page.Footer().AlignCenter().Text("CONFIDENTIAL - DO NOT DISTRIBUTE")
        .FontSize(10).FontColor(Colors.Grey.Darken2);
}

private void RenderAgencySeal(IContainer container)
{
    container.Width(150).Height(150).Canvas((canvas, size) =>
    {
        // Draw badge outline with SVG-like commands
        // Circle + star shape + text
        // This is a simplified seal - use QuestPDF Canvas API
    });
}

private void RenderCaseInfoBlock(IContainer container, string caseId, string title)
{
    container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(15).Column(col =>
    {
        col.Item().Text($"Case Number: {caseId}").FontSize(12).Bold();
        col.Item().Text($"Case Title: {title}").FontSize(12);
        col.Item().Text($"Classification: Cold Case Investigation").FontSize(11);
        col.Item().Text($"Date Opened: {DateTime.Now:MMMM dd, yyyy}").FontSize(11);
        col.Item().PaddingTop(10).Text("Lead Investigator: [REDACTED]").FontSize(11);
        col.Item().Text("Unit: Major Crimes Division").FontSize(11);
    });
}

private void RenderDocumentStamps(IContainer container)
{
    // Red oval "RECEIVED" stamp
    // Blue rectangular "FILE OPENED" stamp
    // Implement using Rotate + Border + Background
}

private void RenderBarcode(IContainer container, string caseId)
{
    // Simple barcode representation
    container.Width(100).Height(50).Border(1).Padding(5).Text($"||| {caseId} |||")
        .FontSize(8).FontFamily("Courier New");
}
```

**Testing:**
```http
### Test Cover Page
POST http://localhost:7071/api/test/pdf
Content-Type: application/json

{
  "title": "Test Cover Page",
  "documentType": "cover_page",
  "sections": [
    {
      "title": "Cover",
      "content": "This is a test of the cover page design"
    }
  ]
}
```

**Checklist:**
- [ ] Agency seal rendering
- [ ] Classification banner (red/blue variants)
- [ ] Case info table with proper formatting
- [ ] Document stamps (received, opened)
- [ ] Barcode/tracking element
- [ ] Footer classification marking
- [ ] Test with different case numbers and titles

---

### Task 2.2: Redesign Case Summary Section
**Implementation:** Enhance `RenderPoliceReport()` method

**Format:**
```
┌─────────────────────────────────────────────────┐
│ CASE SYNOPSIS                                   │
├─────────────────────────────────────────────────┤
│ [3-5 sentence summary of key facts]            │
└─────────────────────────────────────────────────┘

CLASSIFICATION DETAILS
┌─────────────────┬────────────────────────────┐
│ Case Type       │ Homicide                   │
│ Priority Level  │ High                       │
│ Victim Status   │ Deceased                   │
│ Suspect Status  │ Unknown/At Large           │
└─────────────────┴────────────────────────────┘

INCIDENT DETAILS
┌─────────────────┬────────────────────────────┐
│ Incident Date   │ September 15, 2023         │
│ Incident Time   │ 02:30 AM EDT               │
│ Location        │ 123 Main St, City, State   │
│ Weather         │ Clear, 68°F                │
│ Reporting Party │ [NAME] - Badge #1234       │
└─────────────────┴────────────────────────────┘

VICTIM INFORMATION
┌─────────────────┬────────────────────────────┐
│ Name            │ [REDACTED]                 │
│ Age             │ 34                         │
│ Sex/Race        │ Female / White             │
│ Last Known Addr │ [REDACTED]                 │
└─────────────────┴────────────────────────────┘

INITIAL RESPONSE
First Responder: Officer [NAME], Badge #5678
Arrived: 02:45 AM EDT
Scene Secured: 03:00 AM EDT
```

**Code Implementation:**
```csharp
// Replace existing RenderPoliceReport() around line 418

private void RenderPoliceReport(ColumnDescriptor col, string md, string? caseId, string? docId)
{
    // Header box with unit/agent/report info
    col.Item().PaddingBottom(10).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Row(r =>
    {
        r.RelativeItem().Column(c =>
        {
            c.Item().Text("RESPONDING UNIT").FontSize(9).Bold().FontColor(Colors.Grey.Darken3);
            c.Item().Text("Unit: __________________").FontSize(10).FontColor(Colors.Grey.Darken2);
            c.Item().Text("Badge: _________________").FontSize(10).FontColor(Colors.Grey.Darken2);
        });
        r.RelativeItem().Column(c =>
        {
            c.Item().Text("REPORT DETAILS").FontSize(9).Bold().FontColor(Colors.Grey.Darken3);
            c.Item().Text($"Report No.: {(docId ?? "________")}").FontSize(10).FontColor(Colors.Grey.Darken2);
            c.Item().Text($"Date: {DateTimeOffset.Now:MM/dd/yyyy HH:mm}").FontSize(10).FontColor(Colors.Grey.Darken2);
        });
        r.RelativeItem().AlignRight().Column(c =>
        {
            c.Item().Text("CASE REFERENCE").FontSize(9).Bold().FontColor(Colors.Grey.Darken3);
            c.Item().Text($"Case: {(caseId ?? "________")}").FontSize(10).FontColor(Colors.Grey.Darken2);
            c.Item().Text("Classification: CONFIDENTIAL").FontSize(9).FontColor(Colors.Red.Darken2);
        });
    });
    
    // Synopsis box
    col.Item().PaddingTop(10).PaddingBottom(10).Border(2).BorderColor(Colors.Blue.Lighten3)
        .Background(Colors.Blue.Lighten5).Padding(10).Column(c =>
    {
        c.Item().Text("CASE SYNOPSIS").FontSize(11).Bold();
        c.Item().PaddingTop(5).Text(ExtractSynopsis(md)).FontSize(10).LineHeight(1.4f);
    });
    
    // Classification details table
    RenderFormTable(col, "CLASSIFICATION DETAILS", new Dictionary<string, string>
    {
        { "Case Type", "Homicide" },
        { "Priority Level", "High" },
        { "Victim Status", "Deceased" },
        { "Suspect Status", "Unknown" }
    });
    
    // Render remaining markdown content
    col.Item().PaddingTop(15).Column(contentCol =>
    {
        RenderMarkdownContent(contentCol, md);
    });
}

private void RenderFormTable(ColumnDescriptor col, string heading, Dictionary<string, string> fields)
{
    col.Item().PaddingTop(10).Column(c =>
    {
        c.Item().Text(heading).FontSize(11).Bold().FontColor(Colors.Grey.Darken3);
        c.Item().PaddingTop(5).Border(1).BorderColor(Colors.Grey.Lighten2).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1);
                columns.RelativeColumn(2);
            });
            
            foreach (var field in fields)
            {
                table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3)
                    .Padding(5).Text(field.Key).FontSize(9.5f).Bold();
                table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3)
                    .Padding(5).Text(field.Value).FontSize(10);
            }
        });
    });
}

private string ExtractSynopsis(string markdown)
{
    // Extract first paragraph or generate summary
    var lines = markdown.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    var firstPara = lines.FirstOrDefault(l => !l.StartsWith("#") && l.Length > 50);
    return firstPara ?? "Synopsis not available";
}
```

**Testing:**
- [ ] Synopsis box rendering
- [ ] Classification table formatting
- [ ] Incident details table
- [ ] Victim information with redactions
- [ ] Initial response section
- [ ] Typography and spacing

---

### Task 2.3: Enhance Evidence Catalog Section
**Implementation:** Replace `RenderEvidenceLog()` method

**Format:**
```
EVIDENCE INVENTORY
Classification: CONFIDENTIAL • Chain of Custody Required

┌──────┬─────────────┬──────────────┬─────────────────────────┬────────────┐
│ Tag  │ Collected   │ Collected By │ Description             │ Location   │
├──────┼─────────────┼──────────────┼─────────────────────────┼────────────┤
│ E-001│ 09/15/23    │ Det. Smith   │ Black leather wallet,   │ Evidence   │
│      │ 03:15 AM    │ Badge #1234  │ 4.5" x 3", contains ID  │ Room B-12  │
├──────┼─────────────┼──────────────┼─────────────────────────┼────────────┤
│ E-002│ 09/15/23    │ CSI Johnson  │ Fingerprint lift from   │ Lab Queue  │
│      │ 04:00 AM    │ #F-567       │ doorknob, partial print │ Analysis   │
└──────┴─────────────┴──────────────┴─────────────────────────┴────────────┘

CHAIN OF CUSTODY - ITEM E-001
┌─────────────┬──────────────┬──────────────┬─────────────┬────────────┐
│ Date/Time   │ Released By  │ Received By  │ Purpose     │ Location   │
├─────────────┼──────────────┼──────────────┼─────────────┼────────────┤
│ 09/15/23    │ Det. Smith   │ Evidence     │ Initial     │ Evidence   │
│ 03:15       │ #1234        │ Tech Jones   │ Collection  │ Room B-12  │
├─────────────┼──────────────┼──────────────┼─────────────┼────────────┤
│ 09/16/23    │ E. Tech      │ Lab Tech     │ Fingerprint │ Crime Lab  │
│ 08:00       │ Jones        │ Williams     │ Analysis    │ Rm 301     │
└─────────────┴──────────────┴──────────────┴─────────────┴────────────┘

FORENSIC STATUS
E-001: Pending Analysis (DNA swab collected)
E-002: Analysis Complete - No match in database

PHOTO REFERENCES
E-001: Photos #001-004 (4 angles)
E-002: Photo #005 (macro view)
```

**Code Implementation:**
```csharp
private void RenderEvidenceLog(ColumnDescriptor col, string md)
{
    // Header
    col.Item().PaddingBottom(10).Column(c =>
    {
        c.Item().Text("EVIDENCE INVENTORY").FontSize(14).Bold();
        c.Item().Text("Classification: CONFIDENTIAL • Chain of Custody Required")
            .FontSize(9).FontColor(Colors.Red.Darken2);
    });
    
    // Evidence inventory table
    col.Item().PaddingTop(10).Table(table =>
    {
        table.ColumnsDefinition(columns =>
        {
            columns.ConstantColumn(50); // Tag
            columns.RelativeColumn(1);   // Collected
            columns.RelativeColumn(1);   // By
            columns.RelativeColumn(2);   // Description
            columns.RelativeColumn(1);   // Location
        });
        
        // Header row
        table.Header(header =>
        {
            RenderTableHeader(header, "Tag");
            RenderTableHeader(header, "Collected");
            RenderTableHeader(header, "Collected By");
            RenderTableHeader(header, "Description");
            RenderTableHeader(header, "Location");
        });
        
        // Evidence items (parse from markdown)
        var evidenceItems = ParseEvidenceItems(md);
        foreach (var item in evidenceItems)
        {
            // Tag cell with monospace font
            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3)
                .Padding(5).Text(item.Tag).FontFamily("Courier New").FontSize(10).Bold();
            
            // Date/time
            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3)
                .Padding(5).Column(c =>
            {
                c.Item().Text(item.Date).FontSize(9);
                c.Item().Text(item.Time).FontSize(9).FontColor(Colors.Grey.Darken1);
            });
            
            // Collector
            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3)
                .Padding(5).Text(item.CollectedBy).FontSize(9);
            
            // Description
            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3)
                .Padding(5).Text(item.Description).FontSize(9.5f).LineHeight(1.3f);
            
            // Location
            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3)
                .Padding(5).Text(item.Location).FontSize(9);
        }
    });
    
    // Chain of custody for each item
    foreach (var item in evidenceItems)
    {
        col.Item().PaddingTop(15).Column(c =>
        {
            c.Item().Text($"CHAIN OF CUSTODY - ITEM {item.Tag}").FontSize(11).Bold();
            RenderChainOfCustodyTable(c, item.ChainOfCustody);
        });
    }
    
    // Forensic status section
    col.Item().PaddingTop(15).Column(c =>
    {
        c.Item().Text("FORENSIC STATUS").FontSize(11).Bold();
        foreach (var item in evidenceItems)
        {
            c.Item().Text($"{item.Tag}: {item.ForensicStatus}").FontSize(10);
        }
    });
}

private void RenderTableHeader(IContainer cell, string text)
{
    cell.Cell().Border(1).BorderColor(Colors.Grey.Darken1)
        .Background(Colors.Grey.Lighten2).Padding(5)
        .Text(text).FontSize(9).Bold();
}

private void RenderChainOfCustodyTable(ColumnDescriptor col, List<CustodyEntry> entries)
{
    col.Item().PaddingTop(5).Table(table =>
    {
        table.ColumnsDefinition(columns =>
        {
            columns.RelativeColumn(1); // Date/Time
            columns.RelativeColumn(1); // Released By
            columns.RelativeColumn(1); // Received By
            columns.RelativeColumn(1); // Purpose
            columns.RelativeColumn(1); // Location
        });
        
        table.Header(header =>
        {
            RenderTableHeader(header, "Date/Time");
            RenderTableHeader(header, "Released By");
            RenderTableHeader(header, "Received By");
            RenderTableHeader(header, "Purpose");
            RenderTableHeader(header, "Location");
        });
        
        foreach (var entry in entries)
        {
            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3)
                .Padding(5).Column(c =>
            {
                c.Item().Text(entry.Date).FontSize(9);
                c.Item().Text(entry.Time).FontSize(8).FontColor(Colors.Grey.Darken1);
            });
            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3)
                .Padding(5).Text(entry.ReleasedBy).FontSize(9);
            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3)
                .Padding(5).Text(entry.ReceivedBy).FontSize(9);
            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3)
                .Padding(5).Text(entry.Purpose).FontSize(9);
            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3)
                .Padding(5).Text(entry.Location).FontSize(9);
        }
    });
}

// Helper models
private record EvidenceItem(
    string Tag,
    string Date,
    string Time,
    string CollectedBy,
    string Description,
    string Location,
    string ForensicStatus,
    List<CustodyEntry> ChainOfCustody
);

private record CustodyEntry(
    string Date,
    string Time,
    string ReleasedBy,
    string ReceivedBy,
    string Purpose,
    string Location
);

private List<EvidenceItem> ParseEvidenceItems(string markdown)
{
    // Parse evidence items from markdown tables/content
    // This would extract evidence details from the generated content
    // For now, return mock data for demonstration
    return new List<EvidenceItem>
    {
        new EvidenceItem(
            Tag: "E-001",
            Date: "09/15/23",
            Time: "03:15 AM",
            CollectedBy: "Det. Smith #1234",
            Description: "Black leather wallet, 4.5\" x 3\", contains ID",
            Location: "Evidence Room B-12",
            ForensicStatus: "Pending Analysis (DNA swab collected)",
            ChainOfCustody: new List<CustodyEntry>
            {
                new CustodyEntry("09/15/23", "03:15", "Det. Smith #1234", "Evidence Tech Jones", "Initial Collection", "Evidence Room B-12"),
                new CustodyEntry("09/16/23", "08:00", "E. Tech Jones", "Lab Tech Williams", "Fingerprint Analysis", "Crime Lab Rm 301")
            }
        )
    };
}
```

**Testing:**
- [ ] Evidence table formatting
- [ ] Monospace fonts for evidence tags
- [ ] Chain of custody tables
- [ ] Multi-line descriptions
- [ ] Forensic status section
- [ ] Photo reference section

---

### Task 2.4: Redesign Suspect/Witness Profiles
**Implementation:** New method `RenderSuspectProfile()`

**Format:**
```
┌────────────────────────────────────────────────────────────┐
│                    SUSPECT INFORMATION                     │
│                     [MUGSHOT PHOTO]                        │
│                    ID: SUSP-001-2024                       │
└────────────────────────────────────────────────────────────┘

PERSONAL DATA
┌─────────────────┬────────────────────────────────────────┐
│ Name            │ [LAST], [FIRST] [MIDDLE]               │
│ AKA             │ [ALIASES]                              │
│ DOB             │ MM/DD/YYYY (Age: ##)                   │
│ Sex/Race        │ Male / White                           │
│ Height/Weight   │ 6'0" / 180 lbs                         │
│ Eyes/Hair       │ Brown / Brown                          │
│ Marks/Scars     │ Tattoo left forearm (rose)             │
│ Address         │ [REDACTED]                             │
│ SSN             │ ###-##-[LAST 4]                        │
└─────────────────┴────────────────────────────────────────┘

CRIMINAL HISTORY
[Summary of prior arrests/convictions]

INTERVIEW LOG
┌─────────────┬──────────────┬────────────┬───────────────┐
│ Date        │ Interviewer  │ Location   │ Duration      │
├─────────────┼──────────────┼────────────┼───────────────┤
│ 09/15/23    │ Det. Johnson │ Station    │ 45 min        │
│ 10:00 AM    │ #5678        │ Room 2     │               │
└─────────────┴──────────────┴────────────┴───────────────┘

ALIBI VERIFICATION
[Alibi statement and verification status]

RISK ASSESSMENT
☐ Flight Risk    ☑ Violent History    ☐ Armed
```

**Code:**
```csharp
private void RenderSuspectProfile(ColumnDescriptor col, string md)
{
    // Header with mugshot placeholder
    col.Item().Border(2).BorderColor(Colors.Grey.Darken2).Padding(15).Column(c =>
    {
        c.Item().AlignCenter().Text("SUSPECT INFORMATION").FontSize(14).Bold();
        
        // Mugshot placeholder (gray box)
        c.Item().PaddingTop(10).AlignCenter().Width(150).Height(200)
            .Border(1).BorderColor(Colors.Grey.Lighten2)
            .Background(Colors.Grey.Lighten4)
            .AlignMiddle().AlignCenter()
            .Text("[PHOTO]").FontSize(12).FontColor(Colors.Grey.Darken1);
        
        c.Item().PaddingTop(5).AlignCenter()
            .Text("ID: SUSP-001-2024").FontSize(10).FontFamily("Courier New");
    });
    
    // Personal data table
    RenderFormTable(col, "PERSONAL DATA", new Dictionary<string, string>
    {
        { "Name", "[LAST], [FIRST] [MIDDLE]" },
        { "AKA", "[ALIASES]" },
        { "DOB", "MM/DD/YYYY (Age: ##)" },
        { "Sex/Race", "Male / White" },
        { "Height/Weight", "6'0\" / 180 lbs" },
        { "Eyes/Hair", "Brown / Brown" },
        { "Marks/Scars", "Tattoo left forearm (rose)" },
        { "Address", "[REDACTED]" },
        { "SSN", "###-##-[LAST 4]" }
    });
    
    // Criminal history
    col.Item().PaddingTop(15).Column(c =>
    {
        c.Item().Text("CRIMINAL HISTORY").FontSize(11).Bold();
        c.Item().PaddingTop(5).Text("[Summary of prior arrests/convictions]").FontSize(10);
    });
    
    // Interview log table
    col.Item().PaddingTop(15).Column(c =>
    {
        c.Item().Text("INTERVIEW LOG").FontSize(11).Bold();
        c.Item().PaddingTop(5).Table(table =>
        {
            // Table definition...
        });
    });
    
    // Alibi section
    col.Item().PaddingTop(15).Column(c =>
    {
        c.Item().Text("ALIBI VERIFICATION").FontSize(11).Bold();
        c.Item().PaddingTop(5).Text("[Alibi statement and verification status]").FontSize(10);
    });
    
    // Risk assessment checkboxes
    col.Item().PaddingTop(15).Column(c =>
    {
        c.Item().Text("RISK ASSESSMENT").FontSize(11).Bold();
        c.Item().PaddingTop(5).Row(r =>
        {
            r.AutoItem().Text("☐ Flight Risk    ").FontSize(10);
            r.AutoItem().Text("☑ Violent History    ").FontSize(10);
            r.AutoItem().Text("☐ Armed").FontSize(10);
        });
    });
}
```

---

## 🎨 Phase 3: Visual Enhancement (2-3 days)

### Task 3.1: Improve Typography and Visual Hierarchy
**Goal:** Apply authentic police document fonts and styling

**Font Strategy:**
1. **Install/Configure Fonts:**
   ```csharp
   // In PdfRenderingService constructor or static initializer
   QuestPDF.Settings.License = LicenseType.Community;
   
   // Font definitions
   const string FONT_COURIER = "Courier New";
   const string FONT_ARIAL = "Arial";
   const string FONT_TIMES = "Times New Roman";
   
   // Font sizes
   const float FONT_H1 = 18f;
   const float FONT_H2 = 14f;
   const float FONT_H3 = 12f;
   const float FONT_BODY = 10.5f;
   const float FONT_SMALL = 9f;
   const float FONT_TINY = 8f;
   ```

2. **Typography Hierarchy:**
   ```csharp
   // Document title
   .Text(title).FontSize(FONT_H1).Bold().FontFamily(FONT_ARIAL);
   
   // Section headers
   .Text(sectionTitle).FontSize(FONT_H2).Bold().FontFamily(FONT_ARIAL);
   
   // Subsection headers
   .Text(subsection).FontSize(FONT_H3).Bold().FontFamily(FONT_ARIAL);
   
   // Body text
   .Text(content).FontSize(FONT_BODY).FontFamily(FONT_TIMES).LineHeight(1.5f);
   
   // Field labels
   .Text(label).FontSize(FONT_SMALL).Bold().FontFamily(FONT_ARIAL);
   
   // Form fields/evidence tags
   .Text(fieldValue).FontSize(FONT_SMALL).FontFamily(FONT_COURIER);
   ```

3. **Line Spacing and Margins:**
   ```csharp
   // Standard margins
   page.Margin(36); // 0.5 inch = 36 points
   
   // Section spacing
   col.Item().PaddingTop(15).PaddingBottom(10);
   
   // Paragraph spacing
   col.Item().PaddingBottom(8);
   
   // Line height
   .LineHeight(1.5f); // For readability
   ```

4. **Color Scheme:**
   ```csharp
   // Define standard colors
   private static class DocumentColors
   {
       public static string ClassificationRed = Colors.Red.Darken2;
       public static string ClassificationBlue = Colors.Blue.Darken2;
       public static string HeaderGray = Colors.Grey.Darken3;
       public static string BorderGray = Colors.Grey.Lighten2;
       public static string BackgroundLightGray = Colors.Grey.Lighten5;
       public static string TextBlack = Colors.Black;
       public static string TextDarkGray = Colors.Grey.Darken2;
   }
   ```

**Testing:**
- [ ] Font rendering across all document types
- [ ] Hierarchy consistency
- [ ] Line spacing and readability
- [ ] Color contrast (accessibility)
- [ ] PDF file size impact

---

### Task 3.2: Add Document Control Elements
**Implementation:** Enhance page headers and footers

**Header Design:**
```
┌────────────────────────────────────────────────────────────┐
│ Case #PD-2024-CFS-001 │ Page 1 of 25 │ CONFIDENTIAL      │
└────────────────────────────────────────────────────────────┘
```

**Code:**
```csharp
private void RenderPageHeader(IContainer container, string caseId, string classification)
{
    container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
        .PaddingBottom(5).Row(r =>
    {
        r.RelativeItem().Text($"Case #{caseId}").FontSize(FONT_SMALL).FontColor(DocumentColors.TextDarkGray);
        
        r.RelativeItem().AlignCenter().Text(t =>
        {
            t.Span("Page ");
            t.CurrentPageNumber();
            t.Span(" of ");
            t.TotalPages();
        }).FontSize(FONT_SMALL).FontColor(DocumentColors.TextDarkGray);
        
        r.RelativeItem().AlignRight().Text(classification)
            .FontSize(FONT_SMALL).FontColor(DocumentColors.ClassificationRed).Bold();
    });
}

private void RenderPageFooter(IContainer container, string classification)
{
    container.BorderTop(1).BorderColor(Colors.Grey.Lighten2)
        .PaddingTop(5).AlignCenter().Text(classification)
        .FontSize(FONT_TINY).FontColor(DocumentColors.TextDarkGray);
}
```

**Document Stamps:**
```csharp
private void RenderDocumentStamp(IContainer container, string stampText, string color, float rotation = 0)
{
    container.Width(120).Height(40).RotateLeft(rotation).Border(2).BorderColor(color)
        .Padding(8).AlignMiddle().AlignCenter()
        .Text(stampText).FontSize(11).Bold().FontColor(color);
}

// Usage:
col.Item().PaddingTop(50).AlignRight().Element(e => 
    RenderDocumentStamp(e, "RECEIVED\nJAN 15 2024", Colors.Red.Darken2, 10));
```

**Signature Blocks:**
```csharp
private void RenderSignatureBlock(ColumnDescriptor col, string title, string name, string date)
{
    col.Item().PaddingTop(30).Column(c =>
    {
        c.Item().BorderBottom(1).BorderColor(Colors.Black).Width(200);
        c.Item().PaddingTop(3).Text($"{title}: {name}").FontSize(FONT_SMALL);
        c.Item().Text($"Date: {date}").FontSize(FONT_SMALL);
    });
}
```

**Testing:**
- [ ] Header consistency across pages
- [ ] Footer classification marking
- [ ] Page numbering (current/total)
- [ ] Stamp rendering and rotation
- [ ] Signature block alignment

---

### Task 3.3: Add Realistic Redaction and Security Markings
**Implementation:** New redaction helpers

**Black Redaction Boxes:**
```csharp
private void RenderRedaction(IContainer container, float width, float height = 15)
{
    container.Width(width).Height(height).Background(Colors.Black);
}

// Usage in text:
col.Item().Row(r =>
{
    r.AutoItem().Text("SSN: ");
    r.AutoItem().Element(e => RenderRedaction(e, 80, 12));
});
```

**Redacted Text with Stamps:**
```csharp
private string ApplyRedaction(string text, string reason = "b(6)")
{
    return $"[REDACTED - {reason}]";
}

// For partial redactions:
private string PartialRedact(string fullValue, int visibleChars)
{
    if (fullValue.Length <= visibleChars)
        return fullValue;
    
    var visible = fullValue.Substring(fullValue.Length - visibleChars);
    return new string('█', fullValue.Length - visibleChars) + visible;
}

// Usage:
var ssn = "123-45-6789";
var redactedSSN = PartialRedact(ssn, 4); // "███-██-6789"
```

**Exemption Code Footnotes:**
```csharp
private void RenderRedactionFootnote(ColumnDescriptor col)
{
    col.Item().PaddingTop(20).BorderTop(1).BorderColor(Colors.Grey.Lighten3)
        .PaddingTop(5).Column(c =>
    {
        c.Item().Text("REDACTION CODES:").FontSize(FONT_TINY).Bold();
        c.Item().Text("b(6) - Personal privacy exemption (5 U.S.C. § 552(b)(6))")
            .FontSize(FONT_TINY).FontColor(Colors.Grey.Darken1);
        c.Item().Text("b(7)(C) - Law enforcement records exemption (5 U.S.C. § 552(b)(7)(C))")
            .FontSize(FONT_TINY).FontColor(Colors.Grey.Darken1);
    });
}
```

**Testing:**
- [ ] Black box rendering (solid rectangles)
- [ ] [REDACTED] text stamps
- [ ] Partial redactions (last 4 digits visible)
- [ ] Exemption code footnotes
- [ ] Redaction in tables vs. text

---

## 📷 Phase 4: Evidence Photo Integration (2 days)

### Task 4.1: Implement Photo Evidence Sheets
**Goal:** Add photo grids with metadata to evidence sections

**Photo Sheet Layout:**
```
PHOTOGRAPHIC EVIDENCE
Item: E-001 | Photographer: CSI Johnson | Date: 09/15/2023

┌──────────────┬──────────────┐
│   Photo #1   │   Photo #2   │
│  Front view  │  Back view   │
│  03:15 AM    │  03:16 AM    │
└──────────────┴──────────────┘
┌──────────────┬──────────────┐
│   Photo #3   │   Photo #4   │
│  Detail view │  With scale  │
│  03:17 AM    │  03:18 AM    │
└──────────────┴──────────────┘

Scale: ABFO #2 Ruler | Lighting: Ambient + Flash
Notes: Photos taken at scene before collection
```

**Code:**
```csharp
private void RenderPhotoEvidenceSheet(ColumnDescriptor col, string evidenceId, List<string> photoUrls)
{
    col.Item().PaddingTop(20).Column(c =>
    {
        // Header
        c.Item().Text("PHOTOGRAPHIC EVIDENCE").FontSize(12).Bold();
        c.Item().Text($"Item: {evidenceId} | Photographer: CSI Johnson | Date: 09/15/2023")
            .FontSize(FONT_SMALL).FontColor(Colors.Grey.Darken1);
        
        // Photo grid (2x2)
        c.Item().PaddingTop(10).Grid(grid =>
        {
            grid.Columns(2);
            grid.Spacing(10);
            
            for (int i = 0; i < photoUrls.Count && i < 4; i++)
            {
                grid.Item().Column(photoCol =>
                {
                    // Photo placeholder or actual image
                    photoCol.Item().Border(1).BorderColor(Colors.Black)
                        .Width(220).Height(165).Background(Colors.Grey.Lighten4)
                        .AlignMiddle().AlignCenter()
                        .Text($"[PHOTO #{i+1}]").FontSize(12);
                    
                    // Caption
                    photoCol.Item().PaddingTop(3).AlignCenter()
                        .Text($"Photo #{i+1:D3}").FontSize(FONT_SMALL).Bold();
                    
                    photoCol.Item().AlignCenter()
                        .Text($"03:{15+i}:00 AM").FontSize(FONT_TINY).FontColor(Colors.Grey.Darken1);
                });
            }
        });
        
        // Metadata footer
        c.Item().PaddingTop(10).Text("Scale: ABFO #2 Ruler | Lighting: Ambient + Flash")
            .FontSize(FONT_SMALL).FontColor(Colors.Grey.Darken2);
        
        c.Item().Text("Notes: Photos taken at scene before collection")
            .FontSize(FONT_SMALL).Italic().FontColor(Colors.Grey.Darken1);
    });
}
```

**Photo Log Table:**
```csharp
private void RenderPhotoLogTable(ColumnDescriptor col, List<PhotoLogEntry> photos)
{
    col.Item().PaddingTop(15).Column(c =>
    {
        c.Item().Text("PHOTOGRAPH LOG").FontSize(11).Bold();
        
        c.Item().PaddingTop(5).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(60); // Photo #
                columns.RelativeColumn(1);   // Subject
                columns.RelativeColumn(1);   // Taken By
                columns.ConstantColumn(90);  // Date/Time
                columns.RelativeColumn(1);   // Location/Notes
            });
            
            table.Header(header =>
            {
                RenderTableHeader(header, "Photo #");
                RenderTableHeader(header, "Subject");
                RenderTableHeader(header, "Taken By");
                RenderTableHeader(header, "Date/Time");
                RenderTableHeader(header, "Notes");
            });
            
            foreach (var photo in photos)
            {
                table.Cell().Border(1).Padding(3)
                    .Text(photo.PhotoNumber).FontSize(FONT_SMALL).FontFamily(FONT_COURIER);
                table.Cell().Border(1).Padding(3)
                    .Text(photo.Subject).FontSize(FONT_SMALL);
                table.Cell().Border(1).Padding(3)
                    .Text(photo.TakenBy).FontSize(FONT_SMALL);
                table.Cell().Border(1).Padding(3)
                    .Text($"{photo.Date}\n{photo.Time}").FontSize(FONT_TINY);
                table.Cell().Border(1).Padding(3)
                    .Text(photo.Notes).FontSize(FONT_TINY);
            }
        });
    });
}

private record PhotoLogEntry(
    string PhotoNumber,
    string Subject,
    string TakenBy,
    string Date,
    string Time,
    string Notes
);
```

**Testing:**
- [ ] Photo grid layout (2x2, 3x3)
- [ ] Photo placeholders rendering
- [ ] Caption formatting
- [ ] Metadata display
- [ ] Photo log table
- [ ] Page breaks (don't split grids)

---

## 🌍 Phase 5: Multi-Language Support (1-2 days)

### Task 5.1: Implement Translation System
**Goal:** Support EN, PT, ES, FR in all PDF sections

**Translation Keys:**
```csharp
public class PdfTranslations
{
    private static Dictionary<string, Dictionary<string, string>> _translations = new()
    {
        ["en"] = new()
        {
            ["case_file"] = "CASE FILE",
            ["confidential"] = "CONFIDENTIAL",
            ["page"] = "Page",
            ["evidence_inventory"] = "EVIDENCE INVENTORY",
            ["suspect_information"] = "SUSPECT INFORMATION",
            ["chain_of_custody"] = "CHAIN OF CUSTODY",
            // ... add 50+ keys
        },
        ["pt"] = new()
        {
            ["case_file"] = "ARQUIVO DO CASO",
            ["confidential"] = "CONFIDENCIAL",
            ["page"] = "Página",
            ["evidence_inventory"] = "INVENTÁRIO DE EVIDÊNCIAS",
            ["suspect_information"] = "INFORMAÇÕES DO SUSPEITO",
            ["chain_of_custody"] = "CADEIA DE CUSTÓDIA",
            // ...
        },
        ["es"] = new()
        {
            ["case_file"] = "ARCHIVO DEL CASO",
            ["confidential"] = "CONFIDENCIAL",
            ["page"] = "Página",
            // ...
        },
        ["fr"] = new()
        {
            ["case_file"] = "DOSSIER D'AFFAIRE",
            ["confidential"] = "CONFIDENTIEL",
            ["page"] = "Page",
            // ...
        }
    };
    
    public static string Get(string key, string language = "en")
    {
        if (_translations.TryGetValue(language, out var langDict) &&
            langDict.TryGetValue(key, out var translation))
        {
            return translation;
        }
        return _translations["en"][key]; // Fallback to English
    }
}
```

**Usage in Rendering:**
```csharp
// In constructor or method parameter
private string _currentLanguage = "en";

// Replace all hardcoded strings:
.Text("CASE FILE") 
// becomes:
.Text(PdfTranslations.Get("case_file", _currentLanguage))

// In page footer:
.Text($"Page {pageNum} of {totalPages}")
// becomes:
.Text($"{PdfTranslations.Get("page", _currentLanguage)} {pageNum} of {totalPages}")
```

**Language-Specific Formatting:**
```csharp
private string FormatDate(DateTime date, string language)
{
    return language switch
    {
        "pt" => date.ToString("dd/MM/yyyy HH:mm"),
        "es" => date.ToString("dd/MM/yyyy HH:mm"),
        "fr" => date.ToString("dd/MM/yyyy HH:mm"),
        _ => date.ToString("MM/dd/yyyy HH:mm") // en
    };
}
```

**Font Support for Accents:**
```csharp
// Ensure fonts support Unicode
page.DefaultTextStyle(x => x
    .FontSize(FONT_BODY)
    .FontFamily("Arial") // Good Unicode support
);

// Test characters: á é í ó ú ñ ç à ê ô
```

**Testing:**
- [ ] Generate PDFs in all 4 languages
- [ ] Verify all section headers translate
- [ ] Check date/time formatting
- [ ] Test accented characters rendering
- [ ] Validate layout with longer text (German effect)
- [ ] Ensure consistent alignment

---

## 📊 Phase 6: Testing & Validation (2 days)

### Task 6.1: Generate Test Cases for All Difficulty Levels
**Test Suite:**

```http
### Test 1: Rookie Case PDF (Simple)
POST http://localhost:7071/api/generate-case
Content-Type: application/json

{
  "prompt": "Simple theft case at a convenience store",
  "difficulty": "rookie",
  "renderDocs": true,
  "renderMedia": false
}

### Test 2: Intermediate Case PDF (Medium)
POST http://localhost:7071/api/generate-case
Content-Type: application/json

{
  "prompt": "Missing person case with multiple witnesses",
  "difficulty": "intermediate",
  "renderDocs": true,
  "renderMedia": false
}

### Test 3: Expert Case PDF (Complex)
POST http://localhost:7071/api/generate-case
Content-Type: application/json

{
  "prompt": "Complex murder investigation with forensics",
  "difficulty": "expert",
  "renderDocs": true,
  "renderMedia": false
}

### Test 4: All Document Types
POST http://localhost:7071/api/test/pdf
Content-Type: application/json

{
  "title": "Complete Case File Test",
  "documentType": "police_report",
  "sections": [
    { "title": "Synopsis", "content": "..." },
    { "title": "Evidence", "content": "..." },
    { "title": "Suspects", "content": "..." },
    { "title": "Timeline", "content": "..." }
  ]
}
```

**Validation Checklist:**
- [ ] **Rookie Case:**
  - [ ] 5-10 pages total
  - [ ] 2-3 evidence items
  - [ ] 1-2 suspects
  - [ ] Simple timeline
  - [ ] Basic evidence log
  - [ ] No complex forensics
  
- [ ] **Intermediate Case:**
  - [ ] 15-25 pages total
  - [ ] 5-8 evidence items
  - [ ] 3-4 suspects
  - [ ] Multiple interviews
  - [ ] Chain of custody tables
  - [ ] Forensics reports
  
- [ ] **Expert Case:**
  - [ ] 30-50 pages total
  - [ ] 10+ evidence items
  - [ ] 5+ suspects/witnesses
  - [ ] Complex timeline
  - [ ] Multiple forensic analyses
  - [ ] Detailed chain of custody
  - [ ] Photo evidence sheets

**Quality Checks:**
- [ ] Page breaks don't split critical content
- [ ] Tables render correctly
- [ ] Images/photos display properly
- [ ] Headers/footers on every page
- [ ] Page numbering accurate
- [ ] File size reasonable (<5MB for most cases)
- [ ] PDF searchable (text not embedded as images)
- [ ] Bookmarks work (if implemented)

---

### Task 6.2: Cross-Platform PDF Validation
**Test Viewers:**
- [ ] Adobe Acrobat Reader (Windows/Mac)
- [ ] Preview (macOS)
- [ ] Chrome PDF viewer
- [ ] Firefox PDF viewer
- [ ] Microsoft Edge PDF viewer
- [ ] Mobile viewers (iOS Safari, Android Chrome)

**Validation Points:**
- [ ] Fonts render consistently
- [ ] Colors match across viewers
- [ ] Tables don't overflow
- [ ] Images display correctly
- [ ] Bookmarks/navigation works
- [ ] Text is selectable
- [ ] Copy/paste preserves formatting
- [ ] Prints correctly

---

## 📚 Phase 7: Documentation (1 day)

### Task 7.1: Create Comprehensive PDF Documentation
**Document:** `docs/PDF_GENERATION_GUIDE.md`

**Sections:**
1. **Architecture Overview**
   - JSON → Markdown → PDF pipeline
   - Service dependencies
   - Storage structure

2. **Document Types**
   - Police Report
   - Forensics Report
   - Interview Transcript
   - Evidence Log
   - Suspect Profile
   - Cover Page

3. **Customization Guide**
   - How to add new document types
   - Custom layouts
   - Theme/styling modifications

4. **Translation System**
   - Adding new languages
   - Translation key structure
   - Testing translations

5. **Testing Guide**
   - Unit test examples
   - Integration test setup
   - Manual testing checklist

6. **Troubleshooting**
   - Common issues
   - Font problems
   - Layout bugs
   - Performance optimization

**Code Examples:**
```markdown
## Adding a New Document Type

1. Add document type constant:
```csharp
private const string DOC_TYPE_WARRANT = "search_warrant";
```

2. Create rendering method:
```csharp
private void RenderSearchWarrant(ColumnDescriptor col, string md)
{
    // Implementation
}
```

3. Update dispatcher:
```csharp
private void RenderByType(ColumnDescriptor col, string documentType, ...)
{
    switch (documentType.ToLower())
    {
        case DOC_TYPE_WARRANT:
            RenderSearchWarrant(col, markdownContent);
            break;
        // ...
    }
}
```

4. Add translation keys (all languages)
5. Test with sample content
```

---

## 🎯 Success Criteria

### Phase 1-2 (Foundation):
- [ ] Detailed analysis document created
- [ ] 10+ real cold case examples documented
- [ ] Cover page rendering works
- [ ] Case summary enhanced
- [ ] Evidence catalog improved

### Phase 3 (Visual):
- [ ] Typography system implemented
- [ ] Document control elements added
- [ ] Redaction system working
- [ ] Security markings applied

### Phase 4 (Photos):
- [ ] Photo evidence sheets render
- [ ] Photo log tables work
- [ ] Grid layouts functional

### Phase 5 (Languages):
- [ ] All 4 languages supported
- [ ] Translations complete (100+ keys)
- [ ] Date/time formatting correct
- [ ] Unicode fonts working

### Phase 6 (Testing):
- [ ] All difficulty levels tested
- [ ] Cross-platform validation passed
- [ ] Quality checklist completed

### Phase 7 (Docs):
- [ ] Comprehensive guide created
- [ ] Code examples provided
- [ ] Troubleshooting section complete

---

## 📈 Estimated Timeline

| Phase | Tasks | Duration | Dependencies |
|-------|-------|----------|--------------|
| 1 | Research & Analysis | 1-2 days | None |
| 2 | Core Structure | 3-4 days | Phase 1 |
| 3 | Visual Enhancement | 2-3 days | Phase 2 |
| 4 | Photo Integration | 2 days | Phase 3 |
| 5 | Multi-Language | 1-2 days | Phase 2-4 |
| 6 | Testing | 2 days | Phase 2-5 |
| 7 | Documentation | 1 day | All phases |
| **Total** | | **12-16 days** | |

---

## 🚀 Getting Started

**Day 1:**
1. Read this document completely
2. Review `PdfRenderingService.cs` (Task 1.1)
3. Set up research document structure
4. Begin collecting cold case examples (Task 1.2)

**Day 2:**
5. Complete research document
6. Start cover page implementation (Task 2.1)
7. Create test endpoint for cover page

**Day 3-4:**
8. Implement case summary improvements (Task 2.2)
9. Build evidence catalog enhancements (Task 2.3)
10. Test both sections

Continue following the phases sequentially...

---

## 📝 Notes

- **QuestPDF Documentation:** https://www.questpdf.com/documentation/getting-started.html
- **Commit often:** After each sub-task completion
- **Test incrementally:** Don't wait until the end
- **Ask for feedback:** Show progress PDFs to stakeholders
- **Performance:** Watch PDF generation time and file sizes
- **Accessibility:** Ensure PDFs are screen-reader friendly

---

**Ready to start? Begin with Task 1.1!** 🎯
