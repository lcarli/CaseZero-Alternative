using System.Reflection;
using QuestPDF;
using QuestPDF.Drawing;
using QuestPDF.Infrastructure;

namespace CaseGen.Functions.Services.CaseV2.Rendering;

/// <summary>
/// Loads and caches font + image bytes shipped as embedded resources.
/// Registers fonts with QuestPDF's FontManager on first access so every
/// layout can refer to them by family name (Lora, Inter, SourceSerif4,
/// JetBrainsMono).
/// </summary>
public static class DocumentResources
{
    private static readonly object _lock = new();
    private static bool _initialized;
    private static byte[]? _brasaoPng;

    public static class FontFamilies
    {
        public const string Serif = "Lora";
        public const string SerifCorporate = "Source Serif 4";
        public const string Sans = "Inter";
        public const string Mono = "JetBrains Mono";
    }

    public static void EnsureInitialized()
    {
        if (_initialized) return;
        lock (_lock)
        {
            if (_initialized) return;

            // QuestPDF refuses to render until a license is declared. Setting
            // it here means every code path that ends up rendering an
            // EvidenceDocument is covered, including unit tests that instantiate
            // EvidenceDocumentRenderer directly (which would otherwise miss the
            // PdfRenderingService constructor that historically set this).
            Settings.License = LicenseType.Community;

            var asm = Assembly.GetExecutingAssembly();
            foreach (var name in asm.GetManifestResourceNames())
            {
                if (name.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
                {
                    using var s = asm.GetManifestResourceStream(name);
                    if (s is null) continue;
                    using var ms = new MemoryStream();
                    s.CopyTo(ms);
                    var bytes = ms.ToArray();
                    using var rs = new MemoryStream(bytes);
                    FontManager.RegisterFontWithCustomName(InferFamilyName(name), rs);
                }
                else if (name.EndsWith("brasao.png", StringComparison.OrdinalIgnoreCase))
                {
                    using var s = asm.GetManifestResourceStream(name);
                    if (s is null) continue;
                    using var ms = new MemoryStream();
                    s.CopyTo(ms);
                    _brasaoPng = ms.ToArray();
                }
            }
            _initialized = true;
        }
    }

    public static byte[] BrasaoPng
    {
        get
        {
            EnsureInitialized();
            return _brasaoPng ?? Array.Empty<byte>();
        }
    }

    private static string InferFamilyName(string resourceName)
    {
        // Resource names look like "CaseGen.Functions.Resources.Fonts.Lora-Regular.ttf"
        var file = Path.GetFileNameWithoutExtension(resourceName);
        if (file.StartsWith("Lora", StringComparison.OrdinalIgnoreCase)) return FontFamilies.Serif;
        if (file.StartsWith("SourceSerif4", StringComparison.OrdinalIgnoreCase)) return FontFamilies.SerifCorporate;
        if (file.StartsWith("Inter", StringComparison.OrdinalIgnoreCase)) return FontFamilies.Sans;
        if (file.StartsWith("JetBrainsMono", StringComparison.OrdinalIgnoreCase)) return FontFamilies.Mono;
        return file;
    }
}
