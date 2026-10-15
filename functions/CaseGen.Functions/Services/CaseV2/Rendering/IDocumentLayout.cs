using CaseGen.Functions.Models.CaseV2;
using QuestPDF.Infrastructure;

namespace CaseGen.Functions.Services.CaseV2.Rendering;

/// <summary>
/// A document layout owns the visual chrome (page setup, header, footer,
/// section heading style) for a family of evidence documents. The shared
/// section payload renderers (narrative/table/keyValue/transcript/code/
/// callout) live in <see cref="DocumentBlocks"/> so they are NOT duplicated
/// per layout — layouts only customize how the document is framed.
/// </summary>
public interface IDocumentLayout
{
    /// <summary>The <c>EvidenceDocument.Layout</c> id this implementation handles.</summary>
    string Layout { get; }

    /// <summary>Compose the document into the QuestPDF container.</summary>
    void Compose(IDocumentContainer container, EvidenceDocument doc);
}
