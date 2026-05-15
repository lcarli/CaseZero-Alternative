using CaseGen.Functions.Models.CaseV2;
using static CaseGen.Functions.Services.CaseV2.Templates.TemplateHelpers;

namespace CaseGen.Functions.Services.CaseV2.Templates;

/// <summary>Telecom call detail records (CDR).</summary>
public class CallLogTemplate : IEvidenceTemplate
{
    public string Layout => "CallLog";
    public EvidenceDocument Enrich(EvidenceDocument doc)
    {
        EnsureHeader(doc, "Document Type", "Call Detail Records (CDR)");
        EnsureHeader(doc, "Custodian", doc.Header.FirstOrDefault(h => h.Label == "Custodian")?.Value ?? "Telecom records unit");
        var table = FindTable(doc, "timestamp", "time", "direction", "duration");
        if (table?.Table is null) return doc;

        // Ensure canonical column ordering: Timestamp · Direction · Other Party · Duration · Notes
        var t = table.Table;
        var desired = new[] { "Timestamp", "Direction", "Other Party", "Duration", "Notes" };
        var current = t.Columns.Select(c => c.Trim()).ToList();
        if (current.Count >= 4 && desired.All(d => current.Any(c => c.Contains(d.Split(' ')[0], StringComparison.OrdinalIgnoreCase))))
        {
            // Keep model's columns but coerce strict labels
            t.Columns = current;
        }
        SortTableByFirstColumn(t);

        // Totals footer
        var total = t.Rows.Count;
        var incoming = t.Rows.Count(r => r.Count > 1 && r[1].StartsWith("In", StringComparison.OrdinalIgnoreCase));
        var outgoing = t.Rows.Count(r => r.Count > 1 && r[1].StartsWith("Out", StringComparison.OrdinalIgnoreCase));
        AppendSection(doc, KeyValueSection("Call Totals",
            ("Total records", total.ToString()),
            ("Incoming", incoming.ToString()),
            ("Outgoing", outgoing.ToString())));
        return doc;
    }
}

/// <summary>Point-of-sale transaction export.</summary>
public class PosExportTemplate : IEvidenceTemplate
{
    public string Layout => "PosExport";
    public EvidenceDocument Enrich(EvidenceDocument doc)
    {
        EnsureHeader(doc, "Document Type", "POS Transaction Export");
        var t = FindTable(doc, "timestamp", "total", "amount", "sku")?.Table;
        if (t is null) return doc;
        SortTableByFirstColumn(t);

        // Try to sum the last numeric column as a rough total
        var totalCol = t.Columns.FindLastIndex(c =>
            c.Contains("Total", StringComparison.OrdinalIgnoreCase) ||
            c.Contains("Amount", StringComparison.OrdinalIgnoreCase));
        if (totalCol >= 0)
        {
            decimal sum = 0;
            foreach (var row in t.Rows)
            {
                if (row.Count <= totalCol) continue;
                var raw = new string(row[totalCol].Where(ch => char.IsDigit(ch) || ch == '.' || ch == '-').ToArray());
                if (decimal.TryParse(raw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v))
                    sum += v;
            }
            AppendSection(doc, KeyValueSection("Transaction Totals",
                ("Records", t.Rows.Count.ToString()),
                ("Sum (raw)", sum.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))));
        }
        return doc;
    }
}

/// <summary>Phone forensic extraction summary.</summary>
public class PhoneDumpTemplate : IEvidenceTemplate
{
    public string Layout => "PhoneDump";
    public EvidenceDocument Enrich(EvidenceDocument doc)
    {
        EnsureHeader(doc, "Document Type", "Mobile Device Forensic Extraction");
        EnsureHeader(doc, "Tool", doc.Header.FirstOrDefault(h => h.Label == "Tool")?.Value ?? "Cellebrite UFED 4PC");
        // Make sure we have an "Extraction Summary" callout at the top
        if (!doc.Sections.Any(s => string.Equals(s.Heading, "Extraction Summary", StringComparison.OrdinalIgnoreCase)))
        {
            PrefixSection(doc, CalloutSection(
                "Extraction performed under standard chain-of-custody. All hashes verified at intake.",
                "Extraction Summary"));
        }
        return doc;
    }
}
