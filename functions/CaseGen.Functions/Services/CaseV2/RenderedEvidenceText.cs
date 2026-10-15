using System.Text;
using System.Text.RegularExpressions;
using CaseGen.Functions.Models.CaseV2;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace CaseGen.Functions.Services.CaseV2;

public static class RenderedEvidenceText
{
    public static string Extract(byte[] bytes)
    {
        using var pdf = PdfDocument.Open(bytes);
        return string.Join("\n", pdf.GetPages().Select(page => ContentOrderTextExtractor.GetText(page)));
    }

    public static string ExtractAndValidate(EvidenceAsset asset, byte[] bytes)
    {
        var text = Extract(bytes);
        var normalized = Normalize(text);
        if (string.IsNullOrWhiteSpace(normalized))
            throw new TaskContractException($"Rendered document {asset.Id} has no extractable readable text.");
        foreach (var value in AuthoredValues(asset))
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            if (!normalized.Contains(Normalize(value), StringComparison.Ordinal))
                throw new TaskContractException($"Rendered document {asset.Id} omits authored content: {value[..Math.Min(100, value.Length)]}");
        }
        foreach (var support in asset.Support)
        {
            if (!normalized.Contains(Normalize(support.Excerpt), StringComparison.Ordinal))
                throw new TaskContractException($"Rendered document {asset.Id} omits support {support.ReferenceId}.");
        }
        return text;
    }

    public static string Normalize(string text) =>
        Regex.Replace(Regex.Replace(text.Normalize(NormalizationForm.FormKC).Replace("•", string.Empty),
                @"(?m)^\s*[-*] ", string.Empty), @"\s+", string.Empty)
            .ToUpperInvariant();

    private static IEnumerable<string?> AuthoredValues(EvidenceAsset asset)
    {
        if (asset.BodyDoc is not { } document)
        {
            yield return asset.Title;
            yield return asset.Body;
            yield break;
        }
        yield return document.Title;
        yield return document.Subtitle;
        foreach (var header in document.Header)
        {
            yield return header.Label;
            yield return header.Value;
        }
        foreach (var section in document.Sections)
        {
            yield return section.Heading;
            yield return section.Text;
            if (section.Table is { } table)
            {
                foreach (var column in table.Columns) yield return column;
                foreach (var cell in table.Rows.SelectMany(row => row)) yield return cell;
                yield return table.Footnote;
            }
            foreach (var item in section.Items ?? [])
            {
                yield return item.Label;
                yield return item.Value;
            }
            foreach (var entry in section.Transcript ?? [])
            {
                yield return entry.Timestamp;
                yield return entry.Speaker;
                yield return entry.Text;
            }
            yield return section.Code?.Content;
        }
        if (document.Signature is { } signature)
        {
            yield return signature.Name;
            yield return signature.Badge;
            yield return signature.Title;
            yield return signature.SignedAt;
        }
    }
}
