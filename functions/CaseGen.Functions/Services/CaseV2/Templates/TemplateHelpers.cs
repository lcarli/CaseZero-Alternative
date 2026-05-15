using CaseGen.Functions.Models.CaseV2;

namespace CaseGen.Functions.Services.CaseV2.Templates;

/// <summary>Shared helpers used by concrete <see cref="IEvidenceTemplate"/> implementations.</summary>
internal static class TemplateHelpers
{
    public static EvidenceSection? FindTable(EvidenceDocument doc, params string[] hintColumns)
    {
        foreach (var s in doc.Sections)
        {
            if (!string.Equals(s.Kind, "table", StringComparison.OrdinalIgnoreCase)) continue;
            if (s.Table is null || s.Table.Columns.Count == 0) continue;
            if (hintColumns.Length == 0) return s;
            var cols = s.Table.Columns.Select(c => c.Trim().ToLowerInvariant()).ToHashSet();
            if (hintColumns.Any(h => cols.Contains(h.ToLowerInvariant()))) return s;
        }
        return null;
    }

    public static void EnsureHeader(EvidenceDocument doc, string label, string value)
    {
        if (doc.Header.Any(h => string.Equals(h.Label, label, StringComparison.OrdinalIgnoreCase))) return;
        doc.Header.Add(new EvidenceKeyValue { Label = label, Value = value });
    }

    public static void PrefixSection(EvidenceDocument doc, EvidenceSection section)
        => doc.Sections.Insert(0, section);

    public static void AppendSection(EvidenceDocument doc, EvidenceSection section)
        => doc.Sections.Add(section);

    public static void SortTableByFirstColumn(EvidenceTable table)
    {
        if (table.Rows.Count <= 1) return;
        try { table.Rows.Sort((a, b) => string.CompareOrdinal(a.FirstOrDefault() ?? string.Empty, b.FirstOrDefault() ?? string.Empty)); }
        catch { /* ignore */ }
    }

    public static EvidenceSection NarrativeSection(string heading, string text)
        => new() { Kind = "narrative", Heading = heading, Text = text };

    public static EvidenceSection CalloutSection(string text, string? heading = null)
        => new() { Kind = "callout", Heading = heading, Text = text };

    public static EvidenceSection KeyValueSection(string heading, params (string Label, string Value)[] items)
        => new()
        {
            Kind = "keyValue",
            Heading = heading,
            Items = items.Select(i => new EvidenceKeyValue { Label = i.Label, Value = i.Value }).ToList()
        };
}
