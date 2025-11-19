using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using CaseGen.Functions.Services.Pdf.Models;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.Pdf.Parsing;

public static class ForensicsReportParser
{
    private static readonly string[] MethodologyLabels =
    {
        "Procedures:",
        "Results:",
        "Interpretation:"
    };

    public static ForensicsReportData? TryParse(JsonElement root, ILogger? logger = null)
    {
        try
        {
            var data = new ForensicsReportData
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

            ParseObjective(sections, data);
            ParseMethodology(sections, data);
            ParseObservations(sections, data);
            ParsePhotographicEvidence(sections, data);
            ParseChainOfCustody(sections, data);
            ParseNarratives(sections, data);

            return data;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to parse forensics report structure: {Message}", ex.Message);
            return null;
        }
    }

    private static void ParseObjective(IReadOnlyList<Section> sections, ForensicsReportData data)
    {
        var objective = sections.FirstOrDefault(s => s.Title.Equals("Objective", StringComparison.OrdinalIgnoreCase));
        if (objective == null)
        {
            return;
        }

        var summaryLines = new List<string>();
        foreach (var rawLine in SplitLines(objective.Content))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("Laboratory:", StringComparison.OrdinalIgnoreCase))
            {
                data.LabName = ExtractValueAfterColon(line);
                continue;
            }

            if (line.StartsWith("Examiner:", StringComparison.OrdinalIgnoreCase))
            {
                var value = ExtractValueAfterColon(line);
                data.ExaminerName = value;
                continue;
            }

            if (line.StartsWith("Date:", StringComparison.OrdinalIgnoreCase))
            {
                if (DateTimeOffset.TryParse(ExtractValueAfterColon(line), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto))
                {
                    data.ExaminationDate = dto;
                }
                continue;
            }

            if (line.StartsWith("Time:", StringComparison.OrdinalIgnoreCase))
            {
                data.ExaminationTime = ExtractValueAfterColon(line);
                continue;
            }

            summaryLines.Add(line);
        }

        data.ObjectiveSummary = summaryLines.Count > 0 ? string.Join(" ", summaryLines) : null;
    }

    private static void ParseMethodology(IReadOnlyList<Section> sections, ForensicsReportData data)
    {
        var methodology = sections.FirstOrDefault(s => s.Title.Equals("Methodology", StringComparison.OrdinalIgnoreCase));
        if (methodology == null)
        {
            return;
        }

        data.Procedures = ExtractLabeledBlock(methodology.Content, "Procedures:");
        data.Results = ExtractLabeledBlock(methodology.Content, "Results:");
        data.Interpretation = ExtractLabeledBlock(methodology.Content, "Interpretation:");
    }

    private static void ParseObservations(IReadOnlyList<Section> sections, ForensicsReportData data)
    {
        var observations = sections.FirstOrDefault(s => s.Title.StartsWith("Observations", StringComparison.OrdinalIgnoreCase));
        if (observations == null)
        {
            return;
        }

        data.ObservationsSummary = NormalizeWhitespace(observations.Content);
    }

    private static void ParsePhotographicEvidence(IReadOnlyList<Section> sections, ForensicsReportData data)
    {
        var photoSection = sections.FirstOrDefault(s => s.Title.StartsWith("Photographic Evidence", StringComparison.OrdinalIgnoreCase));
        if (photoSection == null)
        {
            return;
        }

        var colonIndex = photoSection.Title.IndexOf(':');
        if (colonIndex > 0 && colonIndex < photoSection.Title.Length - 1)
        {
            data.PhotoReferenceId = photoSection.Title[(colonIndex + 1)..].Trim();
        }

        data.PhotoDescription = NormalizeWhitespace(photoSection.Content);
    }

    private static void ParseChainOfCustody(IReadOnlyList<Section> sections, ForensicsReportData data)
    {
        var chainSection = sections.FirstOrDefault(s => s.Title.Contains("Chain of Custody", StringComparison.OrdinalIgnoreCase));
        if (chainSection == null)
        {
            return;
        }

        var entries = new List<ForensicsChainEntry>();
        foreach (var rawLine in SplitLines(chainSection.Content))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith("-"))
            {
                continue;
            }

            var withoutMarker = line.TrimStart('-').Trim();
            if (string.IsNullOrEmpty(withoutMarker))
            {
                continue;
            }

            DateTimeOffset? timestamp = null;
            string description = withoutMarker;
            var colonIndex = withoutMarker.IndexOf(':');
            if (colonIndex > 0)
            {
                var timestampText = withoutMarker[..colonIndex].Trim();
                var remainder = withoutMarker[(colonIndex + 1)..].Trim();
                description = remainder;
                if (DateTimeOffset.TryParse(timestampText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto))
                {
                    timestamp = dto;
                }
            }

            entries.Add(new ForensicsChainEntry
            {
                Timestamp = timestamp,
                Description = description
            });
        }

        if (entries.Count > 0)
        {
            data.ChainOfCustody = entries;
        }
    }

    private static void ParseNarratives(IReadOnlyList<Section> sections, ForensicsReportData data)
    {
        var findings = sections.FirstOrDefault(s => s.Title.Equals("Findings", StringComparison.OrdinalIgnoreCase));
        if (findings != null)
        {
            data.Findings = NormalizeWhitespace(findings.Content);
        }

        var limitations = sections.FirstOrDefault(s => s.Title.Equals("Limitations", StringComparison.OrdinalIgnoreCase));
        if (limitations != null)
        {
            data.Limitations = NormalizeWhitespace(limitations.Content);
        }
    }

    private static string? ExtractLabeledBlock(string content, string label)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var lines = SplitLines(content).ToList();
        var builder = new StringBuilder();
        var capturing = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            var trimmed = line.Trim();
            if (!capturing)
            {
                if (trimmed.StartsWith(label, StringComparison.OrdinalIgnoreCase))
                {
                    capturing = true;
                    var afterLabel = trimmed[label.Length..].Trim();
                    if (!string.IsNullOrEmpty(afterLabel))
                    {
                        builder.AppendLine(afterLabel);
                    }
                }
                continue;
            }

            if (MethodologyLabels.Any(l => !l.Equals(label, StringComparison.OrdinalIgnoreCase) && trimmed.StartsWith(l, StringComparison.OrdinalIgnoreCase)))
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                builder.AppendLine(string.Empty);
                continue;
            }

            builder.AppendLine(trimmed);
        }

        var result = builder.ToString().Trim();
        return string.IsNullOrEmpty(result) ? null : result;
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

        return line[(index + 1)..].Trim();
    }

    private static string? NormalizeWhitespace(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var lines = SplitLines(content)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l));
        return string.Join(" ", lines);
    }

    private record Section(string Title, string Content);
}
