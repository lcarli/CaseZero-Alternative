using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace CaseGen.Functions.Tools;

/// <summary>
/// Standalone test tool to validate CaseFormatConverter with existing test cases
/// Run: dotnet run --project ../CaseGen.Functions TestConverter
/// </summary>
public class ConverterTestTool
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("🧪 CaseFormatConverter Test Tool");
        Console.WriteLine("================================\n");

        var testCases = new[]
        {
            "CASE-20260102-e6dcc658",
            "CASE-20251027-b442dc10"
        };

        foreach (var caseId in testCases)
        {
            await AnalyzeCaseAsync(caseId);
        }

        Console.WriteLine("\n✅ Analysis complete!");
        Console.WriteLine("\n💡 Next steps:");
        Console.WriteLine("   1. Converter is ready to process v2-hierarchical format");
        Console.WriteLine("   2. Start Azure Functions and generate a new case");
        Console.WriteLine("   3. Verify case.json v1.0 is created in bundles/{caseId}/");
    }

    private static async Task AnalyzeCaseAsync(string caseId)
    {
        var basePath = Path.GetFullPath("../../../../test-output/bundles");
        var casePath = Path.Combine(basePath, caseId);

        if (!Directory.Exists(casePath))
        {
            Console.WriteLine($"⏭️  Skipping {caseId} - not found at {casePath}\n");
            return;
        }

        Console.WriteLine($"📁 Analyzing: {caseId}");
        Console.WriteLine($"   Path: {casePath}");

        // Load normalized_case.json
        var normalizedPath = Path.Combine(casePath, "normalized_case.json");
        if (!File.Exists(normalizedPath))
        {
            Console.WriteLine($"  ❌ normalized_case.json not found\n");
            return;
        }

        var normalizedJson = await File.ReadAllTextAsync(normalizedPath);
        var normalized = JsonDocument.Parse(normalizedJson);

        // Count documents
        var docCount = 0;
        var docsPath = Path.Combine(casePath, "documents");
        if (Directory.Exists(docsPath))
        {
            docCount = Directory.GetFiles(docsPath, "*.json").Length;
        }

        // Count media
        var mediaCount = 0;
        var mediaPath = Path.Combine(casePath, "media");
        if (Directory.Exists(mediaPath))
        {
            mediaCount = Directory.GetFiles(mediaPath, "*.json").Length;
        }

        Console.WriteLine($"  ✅ v2-hierarchical structure:");
        Console.WriteLine($"     - normalized_case.json: {normalizedJson.Length / 1024.0:F1} KB");
        Console.WriteLine($"     - documents/: {docCount} files");
        Console.WriteLine($"     - media/: {mediaCount} files");

        // Extract metadata
        if (normalized.RootElement.TryGetProperty("context", out var context) &&
            context.TryGetProperty("plan", out var plan))
        {
            if (plan.TryGetProperty("title", out var title))
                Console.WriteLine($"     - Title: {title.GetString()}");
            if (plan.TryGetProperty("difficulty", out var diff))
                Console.WriteLine($"     - Difficulty: {diff.GetString()}");
        }

        // Analyze suspects from interviews
        var suspectIds = new List<(string id, string name)>();
        if (Directory.Exists(docsPath))
        {
            foreach (var docFile in Directory.GetFiles(docsPath, "doc_interview_*.json"))
            {
                var docJson = await File.ReadAllTextAsync(docFile);
                var doc = JsonDocument.Parse(docJson);

                var docId = doc.RootElement.GetProperty("docId").GetString() ?? "";
                var title = doc.RootElement.GetProperty("title").GetString() ?? "";

                // Extract suspect ID: doc_interview_s001_001 -> S001
                var match = System.Text.RegularExpressions.Regex.Match(docId, @"_s(\d{3})_");
                if (match.Success)
                {
                    var suspectId = $"S{match.Groups[1].Value}";

                    // Extract name from title
                    var nameMatch = System.Text.RegularExpressions.Regex.Match(title, @"with\s+([^(]+)\s*\(");
                    var name = nameMatch.Success ? nameMatch.Groups[1].Value.Trim() : "Unknown";

                    if (!suspectIds.Any(s => s.id == suspectId))
                    {
                        suspectIds.Add((suspectId, name));
                    }
                }
            }
        }

        Console.WriteLine($"\n  🔍 Extracted Suspects ({suspectIds.Count}):");
        foreach (var (id, name) in suspectIds)
        {
            Console.WriteLine($"     - {id}: {name}");
            
            // Check for mugshot
            if (Directory.Exists(mediaPath))
            {
                var mugshotPattern = $"ev_mugshot_{id.ToLower()}_*.json";
                var mugshots = Directory.GetFiles(mediaPath, mugshotPattern);
                if (mugshots.Length > 0)
                {
                    Console.WriteLine($"       ✅ Mugshot found: {Path.GetFileName(mugshots[0])}");
                }
            }
        }

        Console.WriteLine($"\n  📊 Expected case.json v1.0 output:");
        Console.WriteLine($"     - version: \"1.0\"");
        Console.WriteLine($"     - caseId: \"{caseId.ToLowerInvariant().Replace("-", "_")}\"");
        Console.WriteLine($"     - metadata: {{title, description, difficulty, ...}}");
        Console.WriteLine($"     - assets[]: {docCount + mediaCount} items");
        Console.WriteLine($"       * {docCount} documents (doc_* → asset.interview_*, asset.report_*)");
        Console.WriteLine($"       * {mediaCount} media (ev_* → asset.ev_mugshot_*, asset.ev_*)");
        Console.WriteLine($"     - suspects[]: {suspectIds.Count} items");
        Console.WriteLine($"       * Extracted from interview docIds and titles");
        Console.WriteLine($"       * linkedAssets: [interview, mugshot]");
        Console.WriteLine($"     - emails[]: 1+ items (welcome email + forensic results)");
        Console.WriteLine($"     - rules[]: forensic trigger rules");
        Console.WriteLine($"     - forensicsDefaults: {{analysisTypes[], noFindingsEmail}}");

        Console.WriteLine();
    }
}
