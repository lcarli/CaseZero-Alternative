using CaseGen.Functions.Models.CaseV2;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CaseGen.Functions.Services.CaseV2.Rendering.Layouts;

/// <summary>
/// Audio recording transcript: AUDIO RECORDING TRANSCRIPT banner, header
/// strip with recording reference / duration / device / transcribed-by. Body
/// is typically a <c>transcript</c> section, rendered through the shared
/// <see cref="DocumentBlocks.RenderTranscript"/> helper. Distinct from
/// <see cref="InterviewTranscriptLayout"/> (which is for interviews/oitivas)
/// — this one is for transcribed recordings (dispatch tapes, voicemails,
/// surveillance audio, witness phone calls, etc.).
/// </summary>
public sealed class AudioTranscriptLayout : IDocumentLayout
{
    public string Layout => "AudioTranscript";

    public void Compose(IDocumentContainer container, EvidenceDocument doc)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x.FontFamily(DocumentResources.FontFamilies.Sans).FontSize(10).LineHeight(1.35f));

            page.Header().Element(h => BuildHeader(h, doc));
            page.Content().PaddingTop(10).Column(col =>
            {
                DocumentBlocks.RenderSections(col, doc, (c, heading) =>
                {
                    c.Item().PaddingTop(6).PaddingBottom(3).Text(heading.ToUpperInvariant())
                        .FontSize(9.5f).Bold().FontColor("#1F3864").LetterSpacing(0.05f);
                    c.Item().PaddingBottom(3).LineHorizontal(0.4f).LineColor("#1F3864");
                });
                if (doc.Signature is not null)
                    col.Item().PaddingTop(20).Element(c => DocumentBlocks.RenderSignature(c, doc.Signature, ChromeKey.SignatureOfficer, doc.Language));
            });
            page.Footer().Element(f => DocumentBlocks.StandardFooter(f, doc));
        });
    }

    private static void BuildHeader(IContainer container, EvidenceDocument doc)
    {
        container.Column(col =>
        {
            col.Item().Background("#1A365D").Padding(6).Row(r =>
            {
                r.ConstantItem(36).Height(36).Image(DocumentResources.BrasaoPng).FitArea();
                r.RelativeItem().PaddingLeft(8).Column(c =>
                {
                    c.Item().Text(DocumentChrome.Get(ChromeKey.AudioRecordingTranscript, doc.Language))
                        .FontSize(10).Bold().FontColor(Colors.White).LetterSpacing(0.15f);
                    c.Item().Text(doc.Title).FontSize(13).Bold().FontColor(Colors.White);
                    if (!string.IsNullOrEmpty(doc.Subtitle))
                        c.Item().Text(doc.Subtitle).FontSize(9).FontColor("#C7D2FE").Italic();
                });
            });

            if (doc.Header.Count > 0)
            {
                col.Item().Background("#EEF2FF").Padding(6).Row(r =>
                {
                    foreach (var kv in doc.Header.Take(4))
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text(kv.Label).FontSize(7.5f).Bold().FontColor("#1A365D").LetterSpacing(0.05f);
                            c.Item().Text(kv.Value).FontSize(9);
                        });
                    }
                });
            }

            col.Item().Background("#FBE5E5").Padding(2).AlignCenter()
                .Text(DocumentChrome.Get(ChromeKey.Confidential, doc.Language).ToUpperInvariant())
                .FontSize(7).FontColor("#B71C1C").Bold().LetterSpacing(0.05f);
        });
    }
}
