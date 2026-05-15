using CaseGen.Functions.Models.CaseV2;
using static CaseGen.Functions.Services.CaseV2.Templates.TemplateHelpers;

namespace CaseGen.Functions.Services.CaseV2.Templates;

/// <summary>Generic IoT / sensor log with timestamped events.</summary>
public class SensorLogTemplate : IEvidenceTemplate
{
    public string Layout => "SensorLog";
    public EvidenceDocument Enrich(EvidenceDocument doc)
    {
        EnsureHeader(doc, "Document Type", "Sensor / IoT Event Log");
        var table = FindTable(doc, "timestamp", "event", "sensor")?.Table;
        if (table is not null) SortTableByFirstColumn(table);
        return doc;
    }
}

/// <summary>Browser history dump (URL × timestamp × duration).</summary>
public class BrowserHistoryTemplate : IEvidenceTemplate
{
    public string Layout => "BrowserHistory";
    public EvidenceDocument Enrich(EvidenceDocument doc)
    {
        EnsureHeader(doc, "Document Type", "Browser History Export");
        var table = FindTable(doc, "url", "timestamp", "visited")?.Table;
        if (table is not null) SortTableByFirstColumn(table);
        return doc;
    }
}

/// <summary>Bank / card statement.</summary>
public class BankStatementTemplate : IEvidenceTemplate
{
    public string Layout => "BankStatement";
    public EvidenceDocument Enrich(EvidenceDocument doc)
    {
        EnsureHeader(doc, "Document Type", "Bank / Card Statement");
        var t = FindTable(doc, "date", "description", "amount")?.Table;
        if (t is null) return doc;
        SortTableByFirstColumn(t);

        // Sum debit/credit columns when present
        var debitCol = t.Columns.FindIndex(c => c.Contains("Debit", StringComparison.OrdinalIgnoreCase));
        var creditCol = t.Columns.FindIndex(c => c.Contains("Credit", StringComparison.OrdinalIgnoreCase));
        if (debitCol >= 0 || creditCol >= 0)
        {
            decimal debits = 0, credits = 0;
            foreach (var row in t.Rows)
            {
                if (debitCol >= 0 && row.Count > debitCol)
                    debits += ParseMoney(row[debitCol]);
                if (creditCol >= 0 && row.Count > creditCol)
                    credits += ParseMoney(row[creditCol]);
            }
            AppendSection(doc, KeyValueSection("Statement Totals",
                ("Total debits", debits.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)),
                ("Total credits", credits.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)),
                ("Net", (credits - debits).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))));
        }
        return doc;
    }

    private static decimal ParseMoney(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        var raw = new string(s.Where(ch => char.IsDigit(ch) || ch == '.' || ch == '-').ToArray());
        return decimal.TryParse(raw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
    }
}

/// <summary>GPS track / location history.</summary>
public class GpsTrackTemplate : IEvidenceTemplate
{
    public string Layout => "GpsTrack";
    public EvidenceDocument Enrich(EvidenceDocument doc)
    {
        EnsureHeader(doc, "Document Type", "GPS Location History");
        EnsureHeader(doc, "Datum", doc.Header.FirstOrDefault(h => h.Label == "Datum")?.Value ?? "WGS-84");
        var t = FindTable(doc, "timestamp", "lat", "lng", "longitude")?.Table;
        if (t is not null)
        {
            SortTableByFirstColumn(t);
            t.Footnote ??= "Coordinates are decimal degrees (WGS-84). Accuracy in metres.";
        }
        return doc;
    }
}

/// <summary>File listing / directory tree dump.</summary>
public class FileListingTemplate : IEvidenceTemplate
{
    public string Layout => "FileListing";
    public EvidenceDocument Enrich(EvidenceDocument doc)
    {
        EnsureHeader(doc, "Document Type", "File Listing Export");
        var t = FindTable(doc, "path", "size", "modified")?.Table;
        if (t is null) return doc;
        SortTableByFirstColumn(t);
        AppendSection(doc, KeyValueSection("Listing Summary",
            ("Total entries", t.Rows.Count.ToString())));
        return doc;
    }
}

/// <summary>Chat / messaging app export (WhatsApp / iMessage / Signal style).</summary>
public class ChatExportTemplate : IEvidenceTemplate
{
    public string Layout => "ChatExport";
    public EvidenceDocument Enrich(EvidenceDocument doc)
    {
        EnsureHeader(doc, "Document Type", "Messaging Export");
        EnsureHeader(doc, "Platform", doc.Header.FirstOrDefault(h => h.Label == "Platform")?.Value ?? "Mobile Messaging App");

        // If the model put messages in a table, repackage as a transcript so the renderer formats them nicely.
        var tableSection = FindTable(doc, "timestamp", "sender", "message");
        if (tableSection?.Table is not null &&
            !doc.Sections.Any(s => string.Equals(s.Kind, "transcript", StringComparison.OrdinalIgnoreCase)))
        {
            var entries = new List<EvidenceTranscriptEntry>();
            foreach (var row in tableSection.Table.Rows)
            {
                if (row.Count < 3) continue;
                entries.Add(new EvidenceTranscriptEntry
                {
                    Timestamp = row[0],
                    Speaker = row[1],
                    Text = row[2]
                });
            }
            if (entries.Count > 0)
            {
                tableSection.Kind = "transcript";
                tableSection.Transcript = entries;
                tableSection.Table = null;
            }
        }
        return doc;
    }
}

/// <summary>Email export (single thread or mailbox slice).</summary>
public class EmailExportTemplate : IEvidenceTemplate
{
    public string Layout => "EmailExport";
    public EvidenceDocument Enrich(EvidenceDocument doc)
    {
        EnsureHeader(doc, "Document Type", "Email Export");
        return doc;
    }
}

/// <summary>Server / firewall access log.</summary>
public class AccessLogTemplate : IEvidenceTemplate
{
    public string Layout => "AccessLog";
    public EvidenceDocument Enrich(EvidenceDocument doc)
    {
        EnsureHeader(doc, "Document Type", "Network / Server Access Log");
        var t = FindTable(doc, "timestamp", "ip", "status")?.Table;
        if (t is not null) SortTableByFirstColumn(t);
        return doc;
    }
}
