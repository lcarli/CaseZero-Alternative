using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CaseGen.Functions.Services.Pdf.Models;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.Pdf.Parsing;

public static class PoliceReportParser
{
    public static PoliceReportData? TryParse(JsonElement root, ILogger? logger = null)
    {
        try
        {
            var data = new PoliceReportData
            {
                DocumentId = root.TryGetProperty("docId", out var docIdProp) ? docIdProp.GetString() : null,
                DocumentTitle = root.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : null,
                DocumentType = root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null
            };

            if (root.TryGetProperty("sections", out var sectionsProp) && sectionsProp.ValueKind == JsonValueKind.Array)
            {
                var sections = sectionsProp.EnumerateArray().Select(section => new Section(
                    section.TryGetProperty("title", out var title) ? title.GetString() ?? string.Empty : string.Empty,
                    section.TryGetProperty("content", out var content) ? content.GetString() ?? string.Empty : string.Empty
                )).ToList();

                ParseOverview(sections, data);
                ParseTimeline(sections, data);
                ParsePersons(sections, data);
                ParseEvidence(sections, data);
                ParseNarratives(sections, data);
            }

            return data;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to parse police report document structure: {Message}", ex.Message);
            return null;
        }
    }

    private static void ParseOverview(IReadOnlyList<Section> sections, PoliceReportData data)
    {
        var overviewSection = sections.FirstOrDefault(s => s.Title.Equals("Overview", StringComparison.OrdinalIgnoreCase));
        if (overviewSection == null)
        {
            return;
        }

        var lines = SplitLines(overviewSection.Content);
        bool synopsisStarted = false;
        var synopsisLines = new List<string>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("Report Number", StringComparison.OrdinalIgnoreCase))
            {
                data.ReportNumber = ExtractValueAfterColon(line);
                continue;
            }

            if (line.StartsWith("Date/Time", StringComparison.OrdinalIgnoreCase))
            {
                var value = ExtractValueAfterColon(line);
                if (DateTimeOffset.TryParse(value, out var dto))
                {
                    data.ReportDateTime = dto;
                }
                continue;
            }

            if (line.StartsWith("Unit", StringComparison.OrdinalIgnoreCase))
            {
                var value = ExtractValueAfterColon(line);
                var parts = value.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    data.Unit = parts[0];
                }
                if (parts.Length > 1)
                {
                    data.OfficerName = parts[1];
                }
                continue;
            }

            if (line.StartsWith("Officer", StringComparison.OrdinalIgnoreCase) || line.StartsWith("Badge", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(data.OfficerName))
                {
                    data.OfficerName = ExtractValueAfterColon(line);
                }
                else
                {
                    data.OfficerBadge = ExtractValueAfterColon(line);
                }
                continue;
            }

            if (line.StartsWith("##", StringComparison.Ordinal))
            {
                synopsisStarted = true;
                continue;
            }

            if (synopsisStarted)
            {
                synopsisLines.Add(line);
            }
        }

        if (synopsisLines.Count > 0)
        {
            data.Summary = string.Join(" ", synopsisLines);
        }
    }

    private static void ParseTimeline(IReadOnlyList<Section> sections, PoliceReportData data)
    {
        var timelineSection = sections.FirstOrDefault(s => s.Title.Contains("Incident Timeline", StringComparison.OrdinalIgnoreCase));
        if (timelineSection == null)
        {
            return;
        }

        foreach (var line in SplitLines(timelineSection.Content))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("-"))
            {
                continue;
            }

            var withoutMarker = trimmed.TrimStart('-').Trim();
            var delimiterIndex = withoutMarker.IndexOf(": ");
            if (delimiterIndex <= 0)
            {
                continue;
            }

            var timestampText = withoutMarker.Substring(0, delimiterIndex).Trim();
            var description = withoutMarker.Substring(delimiterIndex + 2).Trim();
            DateTimeOffset? timestamp = null;
            if (DateTimeOffset.TryParse(timestampText, out var dto))
            {
                timestamp = dto;
            }

            data.Timeline.Add(new PoliceReportTimelineEntry
            {
                Timestamp = timestamp,
                Summary = description
            });
        }
    }

    private static void ParsePersons(IReadOnlyList<Section> sections, PoliceReportData data)
    {
        var personsSection = sections.FirstOrDefault(s => s.Title.Contains("Persons Involved", StringComparison.OrdinalIgnoreCase));
        if (personsSection == null)
        {
            return;
        }

        foreach (var line in SplitLines(personsSection.Content))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("-"))
            {
                continue;
            }

            var person = trimmed.TrimStart('-').Trim();
            if (!string.IsNullOrEmpty(person))
            {
                data.Persons.Add(new PoliceReportPerson { Description = person });
            }
        }
    }

    private static void ParseEvidence(IReadOnlyList<Section> sections, PoliceReportData data)
    {
        var evidenceSection = sections.FirstOrDefault(s => s.Title.Contains("Evidence", StringComparison.OrdinalIgnoreCase));
        if (evidenceSection == null)
        {
            return;
        }

        var ids = new List<string>();

        // Attempt to read IDs from the title (e.g., "Evidence Referenced: ID1, ID2")
        var colonIndex = evidenceSection.Title.IndexOf(':');
        if (colonIndex > 0)
        {
            var suffix = evidenceSection.Title.Substring(colonIndex + 1);
            ids.AddRange(suffix.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        }

        // Include IDs from bullet list in the content
        foreach (var line in SplitLines(evidenceSection.Content))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("-"))
            {
                var id = trimmed.TrimStart('-').Trim();
                if (!string.IsNullOrEmpty(id))
                {
                    ids.Add(id);
                }
            }
        }

        data.EvidenceIds = ids.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void ParseNarratives(IReadOnlyList<Section> sections, PoliceReportData data)
    {
        var sceneSection = sections.FirstOrDefault(s => s.Title.Contains("Scene Description", StringComparison.OrdinalIgnoreCase));
        if (sceneSection != null)
        {
            data.SceneDescription = StripMarkdownHeaders(sceneSection.Content);
        }

        var assessmentSection = sections.FirstOrDefault(s => s.Title.Contains("Assessment", StringComparison.OrdinalIgnoreCase));
        if (assessmentSection != null)
        {
            data.Assessment = StripMarkdownHeaders(assessmentSection.Content);
        }

        var nextActionsSection = sections.FirstOrDefault(s => s.Title.Contains("Next Actions", StringComparison.OrdinalIgnoreCase));
        if (nextActionsSection != null)
        {
            foreach (var line in SplitLines(nextActionsSection.Content))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("-"))
                {
                    var action = trimmed.TrimStart('-').Trim();
                    if (!string.IsNullOrEmpty(action))
                    {
                        data.NextSteps.Add(action);
                    }
                }
            }
        }
    }

    private static IEnumerable<string> SplitLines(string content)
    {
        return (content ?? string.Empty).Split('\n');
    }

    private static string ExtractValueAfterColon(string line)
    {
        var index = line.IndexOf(':');
        if (index < 0 || index >= line.Length - 1)
        {
            return string.Empty;
        }

        return line.Substring(index + 1).Trim();
    }

    private static string StripMarkdownHeaders(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        var lines = SplitLines(content).Select(line => line.StartsWith("##") ? line.Substring(2).Trim() : line);
        return string.Join(' ', lines.Where(l => !string.IsNullOrWhiteSpace(l)));
    }

    private record Section(string Title, string Content);
}
