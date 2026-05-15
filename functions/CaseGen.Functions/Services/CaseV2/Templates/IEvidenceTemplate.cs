using CaseGen.Functions.Models.CaseV2;

namespace CaseGen.Functions.Services.CaseV2.Templates;

/// <summary>
/// A template knows how to enrich an LLM-emitted <see cref="EvidenceDocument"/>
/// for a specific layout (CallLog, PosExport, PhoneDump, etc.) before it goes
/// through the generic renderer. Templates do NOT render — they shape data so
/// the generic renderer produces a layout-appropriate result (right columns,
/// totals, branded header, ordered rows).
/// </summary>
public interface IEvidenceTemplate
{
    /// <summary>Layout tag this template handles (matches <see cref="EvidenceDocument.Layout"/>).</summary>
    string Layout { get; }

    /// <summary>Returns the enriched document. Implementations MUST be pure (no IO) and
    /// MUST never throw — fall back to <paramref name="doc"/> unchanged on bad input.</summary>
    EvidenceDocument Enrich(EvidenceDocument doc);
}

public interface IEvidenceTemplateRegistry
{
    EvidenceDocument Apply(EvidenceDocument doc);
}

/// <summary>
/// Dispatches an <see cref="EvidenceDocument"/> to the matching template by
/// <see cref="EvidenceDocument.Layout"/>. Unknown layouts pass through unchanged.
/// </summary>
public class EvidenceTemplateRegistry : IEvidenceTemplateRegistry
{
    private readonly Dictionary<string, IEvidenceTemplate> _templates;

    public EvidenceTemplateRegistry(IEnumerable<IEvidenceTemplate> templates)
    {
        _templates = templates.ToDictionary(
            t => t.Layout,
            t => t,
            StringComparer.OrdinalIgnoreCase);
    }

    public EvidenceDocument Apply(EvidenceDocument doc)
    {
        if (doc is null) return new EvidenceDocument();
        if (string.IsNullOrEmpty(doc.Layout) ||
            !_templates.TryGetValue(doc.Layout, out var template))
            return doc;
        try
        {
            return template.Enrich(doc) ?? doc;
        }
        catch
        {
            return doc; // defensive: never block a render because of a template bug
        }
    }
}
