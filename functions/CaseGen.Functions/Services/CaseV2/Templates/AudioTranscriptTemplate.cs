using CaseGen.Functions.Models.CaseV2;
using static CaseGen.Functions.Services.CaseV2.Templates.TemplateHelpers;

namespace CaseGen.Functions.Services.CaseV2.Templates;

/// <summary>
/// Audio recording transcript. Enriches the header with recording metadata
/// (file ref, duration, device, transcribed by) when the LLM omits them, and
/// ensures the body has at least one usable section. Pairs with
/// <see cref="Rendering.Layouts.AudioTranscriptLayout"/>.
/// </summary>
public class AudioTranscriptTemplate : IEvidenceTemplate
{
    public string Layout => "AudioTranscript";

    public EvidenceDocument Enrich(EvidenceDocument doc)
    {
        EnsureHeader(doc, "Document Type", "Audio Recording Transcript");
        EnsureHeader(doc, "Recording Ref", doc.Header.FirstOrDefault(h =>
            h.Label.Contains("recording", StringComparison.OrdinalIgnoreCase))?.Value
            ?? "REC-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant());
        EnsureHeader(doc, "Duration", doc.Header.FirstOrDefault(h =>
            h.Label.Contains("duration", StringComparison.OrdinalIgnoreCase))?.Value
            ?? "—");

        // If the LLM gave us only narrative prose, the layout still renders it
        // fine; we only warn (via callout) when the section list is completely
        // empty so a downstream reviewer sees the gap.
        if (doc.Sections.Count == 0)
        {
            AppendSection(doc, CalloutSection(
                "No transcript content was produced. Re-run generation for this asset.",
                "Missing Transcript"));
        }
        return doc;
    }
}
