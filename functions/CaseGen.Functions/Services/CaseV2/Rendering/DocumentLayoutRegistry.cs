using CaseGen.Functions.Models.CaseV2;
using Microsoft.Extensions.Logging;
using QuestPDF.Infrastructure;

namespace CaseGen.Functions.Services.CaseV2.Rendering;

public interface IDocumentLayoutRegistry
{
    IDocumentLayout Resolve(string layout);
}

/// <summary>
/// Looks up the right <see cref="IDocumentLayout"/> implementation by id.
/// Layouts are DI-registered; lookup is case-insensitive and falls back to
/// <c>GeneralReport</c> when an unknown id is encountered (e.g. older
/// generated cases that pre-date a layout family). The renderer never
/// throws because of an unknown layout id.
/// </summary>
public class DocumentLayoutRegistry : IDocumentLayoutRegistry
{
    private readonly Dictionary<string, IDocumentLayout> _byId;
    private readonly IDocumentLayout _fallback;
    private readonly ILogger<DocumentLayoutRegistry> _logger;

    public DocumentLayoutRegistry(IEnumerable<IDocumentLayout> layouts, ILogger<DocumentLayoutRegistry> logger)
    {
        _logger = logger;
        _byId = layouts.ToDictionary(l => l.Layout, l => l, StringComparer.OrdinalIgnoreCase);
        if (!_byId.TryGetValue("GeneralReport", out _fallback!))
            throw new InvalidOperationException("GeneralReportLayout must be registered as the fallback.");
    }

    public IDocumentLayout Resolve(string layout)
    {
        if (string.IsNullOrWhiteSpace(layout)) return _fallback;
        if (_byId.TryGetValue(layout, out var found)) return found;
        _logger.LogWarning("Unknown layout '{Layout}' — falling back to GeneralReport.", layout);
        return _fallback;
    }
}
