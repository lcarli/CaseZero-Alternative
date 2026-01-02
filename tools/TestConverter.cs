#!/usr/bin/env dotnet script
#r "nuget: Azure.Storage.Blobs, 12.19.1"
#r "nuget: Microsoft.Extensions.Logging.Console, 9.0.0"

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

// Simple test to validate the converter logic without running full Azure Functions

Console.WriteLine("🧪 Testing CaseFormatConverter with existing cases...\n");

var testCases = new[]
{
    "CASE-20260102-e6dcc658",
    "CASE-20251027-b442dc10"
};

foreach (var caseId in testCases)
{
    var casePath = $"../test-output/bundles/{caseId}";
    
    if (!Directory.Exists(casePath))
    {
        Console.WriteLine($"⏭️  Skipping {caseId} - not found");
        continue;
    }

    Console.WriteLine($"📁 Testing case: {caseId}");
    
    // Load normalized_case.json
    var normalizedPath = Path.Combine(casePath, "normalized_case.json");
    if (!File.Exists(normalizedPath))
    {
        Console.WriteLine($"  ❌ normalized_case.json not found");
        continue;
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
    
    Console.WriteLine($"  ✅ v2-hierarchical loaded:");
    Console.WriteLine($"     - normalized_case.json: {normalizedJson.Length} bytes");
    Console.WriteLine($"     - documents/: {docCount} files");
    Console.WriteLine($"     - media/: {mediaCount} files");
    
    // Analyze suspects from interviews
    var suspectIds = new List<string>();
    if (Directory.Exists(docsPath))
    {
        foreach (var docFile in Directory.GetFiles(docsPath, "doc_interview_*.json"))
        {
            var docJson = await File.ReadAllTextAsync(docFile);
            var doc = JsonDocument.Parse(docJson);
            
            var docId = doc.RootElement.GetProperty("docId").GetString();
            var title = doc.RootElement.GetProperty("title").GetString();
            
            // Extract suspect ID: doc_interview_s001_001 -> S001
            var match = System.Text.RegularExpressions.Regex.Match(docId, @"_s(\d{3})_");
            if (match.Success)
            {
                var suspectId = $"S{match.Groups[1].Value}";
                if (!suspectIds.Contains(suspectId))
                {
                    suspectIds.Add(suspectId);
                    
                    // Extract name from title
                    var nameMatch = System.Text.RegularExpressions.Regex.Match(title, @"with\s+([^(]+)\s*\(");
                    var name = nameMatch.Success ? nameMatch.Groups[1].Value.Trim() : "Unknown";
                    
                    Console.WriteLine($"     - Suspect {suspectId}: {name}");
                }
            }
        }
    }
    
    // Expected output structure
    Console.WriteLine($"\n  📊 Expected case.json v1.0:");
    Console.WriteLine($"     - version: \"1.0\"");
    Console.WriteLine($"     - caseId: \"{caseId.ToLowerInvariant().Replace("-", "_")}\"");
    Console.WriteLine($"     - assets[]: ~{docCount + mediaCount} items");
    Console.WriteLine($"     - suspects[]: {suspectIds.Count} items");
    Console.WriteLine($"     - emails[]: 1+ items (welcome email)");
    Console.WriteLine($"     - rules[]: forensic rules");
    
    Console.WriteLine();
}

Console.WriteLine("✅ Analysis complete!");
Console.WriteLine("\n💡 To test the full converter:");
Console.WriteLine("   1. Start Azure Functions");
Console.WriteLine("   2. Generate a new case");
Console.WriteLine("   3. Check bundles/{caseId}/case.json");
