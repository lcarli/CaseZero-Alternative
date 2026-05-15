using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using CaseGen.Functions.Services.Pdf.Models;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.Pdf.Parsing;

public static class WitnessStatementParser
{
    public static WitnessStatementData? TryParse(JsonElement root, ILogger? logger = null)
    {
        try
        {
            var data = new WitnessStatementData
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

            ParseHeader(sections, data);
            ParseNarrative(sections, data);
            ParseTimeline(sections, data);
            ParseReferences(sections, data);
            ParseSignature(sections, data);

            return data;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to parse witness statement structure: {Message}", ex.Message);
            return null;
        }
    }

    private static void ParseHeader(IReadOnlyList<Section> sections, WitnessStatementData data)
    {
        var header = sections.FirstOrDefault(s => s.Title.Contains("Header", StringComparison.OrdinalIgnoreCase));
        if (header == null)
        {
            return;
        }

        var text = NormalizeWhitespace(header.Content ?? string.Empty);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var identificationIndex = text.IndexOf("Identification:", StringComparison.OrdinalIgnoreCase);
        if (identificationIndex >= 0)
        {
            data.Identification = text[(identificationIndex + "Identification:".Length)..].Trim();
            text = text[..identificationIndex].Trim();
        }

        var providePhrase = "provide this statement recorded on";
        var provideIndex = text.IndexOf(providePhrase, StringComparison.OrdinalIgnoreCase);
        if (provideIndex >= 0)
        {
            var afterPhrase = text[(provideIndex + providePhrase.Length)..].Trim();
            var delimiterIndex = afterPhrase.IndexOf('.');
            var timestampText = delimiterIndex >= 0 ? afterPhrase[..delimiterIndex].Trim() : afterPhrase;
            if (DateTimeOffset.TryParse(timestampText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto))
            {
                data.StatementRecordedAt = dto;
            }

            text = text[..provideIndex].Trim();
        }

        text = TrimLeadingIdentity(text);
        var identityParts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (identityParts.Length > 0)
        {
            data.WitnessName = identityParts[0];
        }
        if (identityParts.Length > 1)
        {
            data.WitnessRole = identityParts[1];
        }
    }

    private static string TrimLeadingIdentity(string value)
    {
        var trimmed = value.Trim().Trim(',' );
        if (trimmed.StartsWith("I", StringComparison.OrdinalIgnoreCase))
        {
            var commaIndex = trimmed.IndexOf(',');
            if (commaIndex >= 0 && commaIndex < trimmed.Length - 1)
            {
                trimmed = trimmed[(commaIndex + 1)..].Trim();
            }
        }
        return trimmed.Trim(',').Trim();
    }

    private static void ParseNarrative(IReadOnlyList<Section> sections, WitnessStatementData data)
    {
        var narrative = sections.FirstOrDefault(s => s.Title.Equals("Narrative", StringComparison.OrdinalIgnoreCase));
        if (narrative != null)
        {
            data.Narrative = NormalizeWhitespace(narrative.Content);
        }
    }

    private static void ParseTimeline(IReadOnlyList<Section> sections, WitnessStatementData data)
    {
        var times = sections.FirstOrDefault(s => s.Title.Contains("Key Times", StringComparison.OrdinalIgnoreCase));
        if (times == null)
        {
            return;
        }

        var entries = new List<WitnessTimelineEntry>();
        foreach (var rawLine in SplitLines(times.Content))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith("-"))
            {
                continue;
            }

            var withoutMarker = line.TrimStart('-').Trim();
            if (string.IsNullOrWhiteSpace(withoutMarker))
            {
                continue;
            }

            var separatorIndex = withoutMarker.IndexOf('–');
            if (separatorIndex < 0)
            {
                separatorIndex = withoutMarker.IndexOf('-');
            }

            string timestampText;
            string description;
            if (separatorIndex > 0)
            {
                timestampText = withoutMarker[..separatorIndex].Trim();
                description = withoutMarker[(separatorIndex + 1)..].Trim();
            }
            else
            {
                timestampText = withoutMarker;
                description = withoutMarker;
            }

            DateTimeOffset? timestamp = null;
            if (DateTimeOffset.TryParse(timestampText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto))
            {
                timestamp = dto;
            }

            entries.Add(new WitnessTimelineEntry
            {
                Timestamp = timestamp,
                Description = description
            });
        }

        if (entries.Count > 0)
        {
            data.Timeline = entries;
        }
    }

    private static void ParseReferences(IReadOnlyList<Section> sections, WitnessStatementData data)
    {
        var referencesSection = sections.FirstOrDefault(s => s.Title.Contains("Attachments", StringComparison.OrdinalIgnoreCase) || s.Title.Contains("References", StringComparison.OrdinalIgnoreCase));
        if (referencesSection == null)
        {
            return;
        }

        var refs = new List<string>();
        foreach (var rawLine in SplitLines(referencesSection.Content))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith("-"))
            {
                continue;
            }

            var value = line.TrimStart('-').Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                refs.Add(value);
            }
        }

        if (refs.Count > 0)
        {
            data.References = refs;
        }
    }

    private static void ParseSignature(IReadOnlyList<Section> sections, WitnessStatementData data)
    {
        var signatureSection = sections.FirstOrDefault(s => s.Title.StartsWith("Signature", StringComparison.OrdinalIgnoreCase));
        if (signatureSection == null)
        {
            return;
        }

        var info = new WitnessSignatureInfo();
        foreach (var rawLine in SplitLines(signatureSection.Content))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("Signed:", StringComparison.OrdinalIgnoreCase))
            {
                var afterSigned = line[("Signed:".Length)..].Trim();
                var parts = afterSigned.Split('.', 2, StringSplitOptions.TrimEntries);
                var identitySegment = parts[0];
                if (parts.Length > 1 && DateTimeOffset.TryParse(parts[1], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var inlineDate))
                {
                    info.SignedAt = inlineDate;
                }

                var identityParts = identitySegment.Split(',', 2, StringSplitOptions.TrimEntries);
                if (identityParts.Length > 0)
                {
                    info.Name = identityParts[0];
                }
                if (identityParts.Length > 1)
                {
                    info.Role = identityParts[1];
                }
                continue;
            }

            if (info.SignedAt == null && DateTimeOffset.TryParse(line, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dateLine))
            {
                info.SignedAt = dateLine;
            }
        }

        data.Signature = info;
    }

    private static IEnumerable<string> SplitLines(string content)
    {
        return (content ?? string.Empty).Split('\n');
    }

    private static string NormalizeWhitespace(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var parts = content.Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line));
        return string.Join(' ', parts);
    }

    private record Section(string Title, string Content);
}
