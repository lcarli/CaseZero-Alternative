using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CaseGen.Functions.Services.Pdf.Models;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.Pdf.Parsing;

public static class EvidenceLogParser
{
    public static EvidenceLogData? TryParse(JsonElement root, ILogger? logger = null)
    {
        try
        {
            var data = new EvidenceLogData
            {
                DocumentId = root.TryGetProperty("docId", out var docIdProp) ? docIdProp.GetString() : null,
                DocumentTitle = root.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : null,
                DocumentType = root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null
            };

            if (!root.TryGetProperty("sections", out var sectionsProp) || sectionsProp.ValueKind != JsonValueKind.Array)
            {
                return data;
            }

            var sections = sectionsProp.EnumerateArray()
                .Select(section => new Section(
                    section.TryGetProperty("title", out var title) ? title.GetString() ?? string.Empty : string.Empty,
                    section.TryGetProperty("content", out var content) ? content.GetString() ?? string.Empty : string.Empty))
                .ToList();

            data.Summary = ExtractSectionText(sections, "Intake Overview");
            data.Items = ParseItemTable(sections);
            data.LabelingAndStorage = ExtractSectionText(sections, "Labeling and Storage");
            data.CustodyEntries = ParseCustodyEntries(sections);
            data.Remarks = ExtractSectionText(sections, "Remarks");

            return data;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to parse evidence log document structure: {Message}", ex.Message);
            return null;
        }
    }

    private static IReadOnlyList<EvidenceItemEntry> ParseItemTable(IReadOnlyList<Section> sections)
    {
        var tableSection = sections.FirstOrDefault(section => section.Title.StartsWith("Item Entries", StringComparison.OrdinalIgnoreCase));
        if (tableSection == null)
        {
            return Array.Empty<EvidenceItemEntry>();
        }

        var lines = tableSection.Content.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !line.TrimStart().StartsWith("|---", StringComparison.Ordinal))
            .ToList();

        // Skip header line
        var dataLines = lines.Skip(1);
        var items = new List<EvidenceItemEntry>();

        foreach (var line in dataLines)
        {
            var cells = line.Split('|', StringSplitOptions.TrimEntries);
            if (cells.Length < 7)
            {
                continue;
            }

            items.Add(new EvidenceItemEntry
            {
                ItemId = cells[1],
                CollectedAt = cells[2],
                CollectedBy = cells[3],
                Description = cells[4],
                Storage = cells[5],
                Transfers = cells[6]
            });
        }

        return items;
    }

    private static IReadOnlyList<CustodyEntry> ParseCustodyEntries(IReadOnlyList<Section> sections)
    {
        var custodySection = sections.FirstOrDefault(section => section.Title.StartsWith("Custody", StringComparison.OrdinalIgnoreCase));
        if (custodySection == null)
        {
            return Array.Empty<CustodyEntry>();
        }

        var entries = new List<CustodyEntry>();
        foreach (var line in custodySection.Content.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("-"))
            {
                continue;
            }

            var withoutMarker = trimmed.TrimStart('-').Trim();
            var delimiterIndex = withoutMarker.IndexOf(':');
            if (delimiterIndex <= 0)
            {
                continue;
            }

            var itemId = withoutMarker.Substring(0, delimiterIndex).Trim();
            var notes = withoutMarker.Substring(delimiterIndex + 1).Trim();

            entries.Add(new CustodyEntry
            {
                ItemId = itemId,
                Notes = notes
            });
        }

        return entries;
    }

    private static string? ExtractSectionText(IReadOnlyList<Section> sections, string title)
    {
        var section = sections.FirstOrDefault(s => s.Title.Contains(title, StringComparison.OrdinalIgnoreCase));
        return section?.Content;
    }

    private record Section(string Title, string Content);
}
