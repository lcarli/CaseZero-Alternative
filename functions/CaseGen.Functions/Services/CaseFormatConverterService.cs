using System.Text.Json;
using CaseGen.Functions.Models;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services;

/// <summary>
/// Converts NormalizedCaseBundle (normalized_case.json) to case.json v1.0 format
/// </summary>
public class CaseFormatConverterService : ICaseFormatConverterService
{
    private readonly ILogger<CaseFormatConverterService> _logger;

    public CaseFormatConverterService(ILogger<CaseFormatConverterService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Converts NormalizedCaseBundle to case.json v1.0 format
    /// </summary>
    public async Task<string> ConvertToV1Async(NormalizedCaseBundle bundle, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Converting NormalizedCaseBundle to case.json v1.0 for case {CaseId}", bundle.CaseId);

            // Build case.json v1.0 structure
            var caseV1 = new
            {
                version = "1.0",
                caseId = bundle.CaseId,
                metadata = new
                {
                    title = $"Case {bundle.CaseId}",
                    description = "Generated detective case",
                    difficulty = bundle.Difficulty ?? "Rookie",
                    estimatedTimeMinutes = GetEstimatedTimeFromDifficulty(bundle.Difficulty),
                    requiredRank = GetRequiredRankFromDifficulty(bundle.Difficulty),
                    location = "Unknown", // Not in NormalizedCaseBundle
                    incidentDate = bundle.CreatedAt.ToString("yyyy-MM-dd"),
                    category = "Investigation",
                    briefing = "Investigate the case using available evidence and documents.",
                    victim = new { } // Empty object - not in NormalizedCaseBundle
                },
                assets = ConvertToAssets(bundle.Documents, bundle.Media),
                emails = ConvertToEmails(bundle.Documents),
                suspects = ConvertToSuspects(bundle.Documents),
                rules = ConvertToRules(bundle.GatingGraph, bundle.Documents),
                forensicsDefaults = new
                {
                    analysisTypes = new[]
                    {
                        "DNA",
                        "Fingerprint",
                        "DigitalForensics",
                        "Ballistics",
                        "Toxicology"
                    },
                    noFindingsEmail = new
                    {
                        emailId = "email.no_findings",
                        from = "lab@forensics.gov",
                        to = "detective@police.gov",
                        subject = "Análise Forense - Sem Resultados",
                        sentAt = bundle.CreatedAt.AddHours(2).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                        priority = "normal",
                        visibility = "hidden",
                        content = "A análise forense solicitada não apresentou resultados conclusivos.",
                        attachments = Array.Empty<string>(),
                        metadata = new { }
                    }
                }
            };

            var json = JsonSerializer.Serialize(caseV1, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _logger.LogInformation("Successfully converted to case.json v1.0: {AssetCount} assets, {EmailCount} emails, {SuspectCount} suspects, {RuleCount} rules",
                caseV1.assets.Length, caseV1.emails.Length, caseV1.suspects.Length, caseV1.rules.Length);

            return json;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert NormalizedCaseBundle to case.json v1.0 for case {CaseId}", bundle.CaseId);
            throw;
        }
    }

    private object[] ConvertToAssets(NormalizedDocument[] documents, NormalizedMedia[] media)
    {
        var assets = new List<object>();

        // Convert documents to assets
        foreach (var doc in documents)
        {
            var assetId = ConvertDocIdToAssetId(doc.DocId);
            assets.Add(new
            {
                assetId,
                name = doc.Title,
                type = GetAssetTypeFromDocType(doc.Type),
                category = "document",
                description = $"{doc.Title} - {doc.Type}",
                filePath = $"/documents/{doc.DocId}.pdf",
                visibility = doc.Gated ? "hidden" : "initial",
                checksum = ComputeChecksum(doc.Content),
                metadata = new
                {
                    docId = doc.DocId,
                    docType = doc.Type,
                    sections = doc.Sections,
                    createdAt = doc.CreatedAt?.ToString("yyyy-MM-ddTHH:mm:ssZ") ?? DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                }
            });
        }

        // Convert media to assets
        foreach (var mediaItem in media)
        {
            var assetId = ConvertEvidenceIdToAssetId(mediaItem.EvidenceId);
            assets.Add(new
            {
                assetId,
                name = mediaItem.Title,
                type = GetAssetTypeFromMediaKind(mediaItem.Kind),
                category = "evidence",
                description = mediaItem.Title,
                filePath = $"/media/{mediaItem.EvidenceId}.png",
                visibility = "initial", // Media is not gated in NormalizedCaseBundle
                checksum = "", // No checksum for media in NormalizedCaseBundle
                metadata = new
                {
                    evidenceId = mediaItem.EvidenceId,
                    kind = mediaItem.Kind,
                    prompt = mediaItem.Prompt,
                    constraints = mediaItem.Constraints ?? new Dictionary<string, object>(),
                    createdAt = mediaItem.CreatedAt?.ToString("yyyy-MM-ddTHH:mm:ssZ") ?? DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                }
            });
        }

        return assets.ToArray();
    }

    private object[] ConvertToEmails(NormalizedDocument[] documents)
    {
        // Extract email-like documents or create placeholder emails
        var emails = new List<object>();

        // For now, create a placeholder email system
        // In a real implementation, we'd extract email content from documents or metadata
        emails.Add(new
        {
            emailId = "email.welcome",
            from = "chief@police.gov",
            to = "detective@police.gov",
            subject = "Novo Caso Atribuído",
            sentAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            priority = "high",
            visibility = "initial",
            content = "Um novo caso foi atribuído a você. Revise os documentos e evidências disponíveis.",
            attachments = Array.Empty<string>(),
            metadata = new { }
        });

        return emails.ToArray();
    }

    private object[] ConvertToSuspects(NormalizedDocument[] documents)
    {
        // Extract suspect information from documents
        // For now, create placeholder suspects
        var suspects = new List<object>();

        // Look for interview documents to extract suspect info
        var interviews = documents.Where(d => d.Type == "interview" || d.Type.Contains("interrogat")).ToArray();
        
        for (int i = 0; i < Math.Min(interviews.Length, 3); i++)
        {
            var interview = interviews[i];
            suspects.Add(new
            {
                suspectId = $"suspect.{i + 1:D3}",
                name = ExtractNameFromTitle(interview.Title),
                age = 0, // Not available in NormalizedDocument
                occupation = "Unknown",
                relationship = "Unknown",
                motive = "Under investigation",
                alibi = "To be determined",
                alibiVerified = false,
                background = ExtractBackgroundFromContent(interview.Content),
                linkedAssets = new[] { ConvertDocIdToAssetId(interview.DocId) },
                visibility = interview.Gated ? "hidden" : "initial"
            });
        }

        // If no interviews, create a default suspect
        if (suspects.Count == 0)
        {
            suspects.Add(new
            {
                suspectId = "suspect.001",
                name = "Unknown Suspect",
                age = 0,
                occupation = "Unknown",
                relationship = "Unknown",
                motive = "Under investigation",
                alibi = "To be determined",
                alibiVerified = false,
                background = "No information available",
                linkedAssets = Array.Empty<string>(),
                visibility = "initial"
            });
        }

        return suspects.ToArray();
    }

    private object[] ConvertToRules(GatingGraph gatingGraph, NormalizedDocument[] documents)
    {
        var rules = new List<object>();

        // Convert gating edges to rules
        foreach (var edge in gatingGraph.Edges)
        {
            var targetDoc = documents.FirstOrDefault(d => d.DocId == edge.To);
            if (targetDoc == null) continue;

            var ruleId = $"rule.reveal_{edge.To.ToLowerInvariant().Replace("-", "_")}";
            var sourceAssetId = edge.From.StartsWith("EVD-") || edge.From.StartsWith("MED-") 
                ? ConvertEvidenceIdToAssetId(edge.From)
                : ConvertDocIdToAssetId(edge.From);
            var targetAssetId = ConvertDocIdToAssetId(edge.To);

            rules.Add(new
            {
                ruleId,
                trigger = new
                {
                    type = "asset_viewed",
                    assetId = sourceAssetId
                },
                actions = new[]
                {
                    new
                    {
                        type = "reveal_asset",
                        assetId = targetAssetId
                    }
                }
            });
        }

        // Add forensics rules from gated documents with forensics-related gating
        foreach (var doc in documents.Where(d => d.Gated && d.GatingRule != null))
        {
            if (doc.Type == "forensics_report" || doc.Type.Contains("forensic"))
            {
                var inputAssetId = doc.GatingRule.EvidenceId != null 
                    ? ConvertEvidenceIdToAssetId(doc.GatingRule.EvidenceId)
                    : null;

                if (inputAssetId != null)
                {
                    rules.Add(new
                    {
                        ruleId = $"rule.forensic_{doc.DocId.ToLowerInvariant().Replace("-", "_")}",
                        trigger = new
                        {
                            type = "forensic_complete",
                            inputAssetId,
                            analysisType = "DigitalForensics"
                        },
                        actions = new[]
                        {
                            new
                            {
                                type = "reveal_email",
                                emailId = "email.forensic_results"
                            }
                        }
                    });
                }
            }
        }

        return rules.ToArray();
    }

    private string ConvertDocIdToAssetId(string docId)
    {
        // Convert DOC-XXX-YYY or similar formats to asset.xxx_yyy
        var clean = docId.ToLowerInvariant()
            .Replace("doc-", "")
            .Replace("int-", "interview_")
            .Replace("rel-", "report_")
            .Replace("anl-", "analysis_")
            .Replace("-", "_");
        
        return $"asset.{clean}";
    }

    private string ConvertEvidenceIdToAssetId(string evidenceId)
    {
        // Convert EVD-XXX or MED-XXX to asset.evd_xxx or asset.med_xxx
        var clean = evidenceId.ToLowerInvariant()
            .Replace("evd-", "evd_")
            .Replace("med-", "med_")
            .Replace("-", "_");
        
        return $"asset.{clean}";
    }

    private string GetAssetTypeFromDocType(string docType)
    {
        return docType.ToLowerInvariant() switch
        {
            "interview" or "interrogatorio" => "document",
            "police_report" or "report" or "relatorio" => "document",
            "forensics_report" or "laudo" => "document",
            _ => "document"
        };
    }

    private string GetAssetTypeFromMediaKind(string mediaKind)
    {
        return mediaKind.ToLowerInvariant() switch
        {
            "crime_scene_photo" => "image",
            "mugshot" => "image",
            "evidence_photo" => "image",
            "security_footage" => "video",
            "audio_recording" => "audio",
            _ => "image"
        };
    }

    private int GetEstimatedTimeFromDifficulty(string? difficulty)
    {
        return difficulty?.ToLowerInvariant() switch
        {
            "rookie" => 30,
            "intermediate" => 45,
            "advanced" => 60,
            "expert" => 90,
            _ => 45
        };
    }

    private string GetRequiredRankFromDifficulty(string? difficulty)
    {
        return difficulty?.ToLowerInvariant() switch
        {
            "rookie" => "Novato",
            "intermediate" => "Detetive",
            "advanced" => "Investigador Sênior",
            "expert" => "Detetive Especial",
            _ => "Detetive"
        };
    }

    private string ComputeChecksum(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private string ExtractNameFromTitle(string title)
    {
        // Try to extract name from titles like "Interview with John Doe" or "Interrogatório - Maria Silva"
        var patterns = new[]
        {
            @"(?:Interview with|Interrogatório|Entrevista)\s*[-:]?\s*(.+?)(?:\s*[-]\s*|$)",
            @"(?:Depoimento de|Statement from)\s*(.+?)(?:\s*[-]\s*|$)"
        };

        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(title, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
        }

        return "Unknown Suspect";
    }

    private string ExtractBackgroundFromContent(string content)
    {
        // Extract first 200 characters as background summary
        var cleaned = content.Replace("\n", " ").Replace("\r", "").Trim();
        return cleaned.Length > 200 
            ? cleaned.Substring(0, 200) + "..." 
            : cleaned;
    }
}
