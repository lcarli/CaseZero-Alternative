using System.Text.Json;
using CaseGen.Functions.Models;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services;

/// <summary>
/// Converts v2-hierarchical format (normalized_case.json + individual files) to case.json v1.0 format
/// </summary>
public class CaseFormatConverterService : ICaseFormatConverterService
{
    private readonly ILogger<CaseFormatConverterService> _logger;
    private readonly IStorageService _storageService;

    public CaseFormatConverterService(
        ILogger<CaseFormatConverterService> logger,
        IStorageService storageService)
    {
        _logger = logger;
        _storageService = storageService;
    }

    /// <summary>
    /// Converts v2-hierarchical case to case.json v1.0 format by loading individual document/media files
    /// </summary>
    public async Task<string> ConvertCaseToV1Async(string caseId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Converting case {CaseId} from v2-hierarchical to case.json v1.0", caseId);

            // Load normalized_case.json
            var normalizedCaseJson = await _storageService.GetFileAsync("bundles", $"{caseId}/normalized_case.json", cancellationToken);
            var normalizedCase = JsonSerializer.Deserialize<JsonElement>(normalizedCaseJson);

            // Load all document files
            var documents = new List<JsonElement>();
            if (normalizedCase.TryGetProperty("documents", out var docsObj) &&
                docsObj.TryGetProperty("items", out var docsArray))
            {
                foreach (var docRef in docsArray.EnumerateArray())
                {
                    var docPath = docRef.GetString()?.Replace("@documents/", "");
                    if (!string.IsNullOrEmpty(docPath))
                    {
                        try
                        {
                            var docJson = await _storageService.GetFileAsync("bundles", $"{caseId}/documents/{docPath}.json", cancellationToken);
                            var doc = JsonSerializer.Deserialize<JsonElement>(docJson);
                            documents.Add(doc);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Could not load document {DocPath} for case {CaseId}", docPath, caseId);
                        }
                    }
                }
            }

            // Load all media files
            var mediaItems = new List<JsonElement>();
            var mediaFiles = await _storageService.ListFilesAsync("bundles", $"{caseId}/media/", cancellationToken);
            foreach (var mediaFile in mediaFiles.Where(f => f.EndsWith(".json")))
            {
                try
                {
                    var mediaJson = await _storageService.GetFileAsync("bundles", mediaFile, cancellationToken);
                    var media = JsonSerializer.Deserialize<JsonElement>(mediaJson);
                    mediaItems.Add(media);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not load media {MediaFile} for case {CaseId}", mediaFile, caseId);
                }
            }

            // Build case.json v1.0
            var caseV1 = BuildCaseV1(caseId, normalizedCase, documents, mediaItems);

            var json = JsonSerializer.Serialize(caseV1, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            // Count arrays for logging (cast to avoid dynamic dispatch issues)
            var assetsCount = ((object[])caseV1.assets).Length;
            var emailsCount = ((object[])caseV1.emails).Length;
            var suspectsCount = ((object[])caseV1.suspects).Length;
            var rulesCount = ((object[])caseV1.rules).Length;
            
            _logger.LogInformation("Successfully converted case {CaseId} to v1.0: {AssetCount} assets, {EmailCount} emails, {SuspectCount} suspects, {RuleCount} rules",
                caseId, assetsCount, emailsCount, suspectsCount, rulesCount);

            return json;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert case {CaseId} to case.json v1.0", caseId);
            throw;
        }
    }

    /// <summary>
    /// Builds case.json v1.0 structure from v2-hierarchical data
    /// </summary>
    private dynamic BuildCaseV1(string caseId, JsonElement normalizedCase, List<JsonElement> documents, List<JsonElement> mediaItems)
    {
        // Extract metadata
        var metadata = ExtractMetadata(normalizedCase, documents);
        
        // Convert to assets
        var assets = BuildAssets(documents, mediaItems);
        
        // Extract suspects
        var suspects = ExtractSuspects(documents, mediaItems);
        
        // Generate emails
        var emails = GenerateEmails();
        
        // Generate rules
        var rules = GenerateRules(documents, suspects);
        
        return new
        {
            version = "1.0",
            caseId = ConvertCaseId(caseId), // CASE-XXX → case_xxx
            metadata,
            assets,
            emails,
            suspects,
            rules,
            forensicsDefaults = new
            {
                analysisTypes = new[] { "DNA", "Fingerprint", "DigitalForensics", "Ballistics", "Toxicology" },
                noFindingsEmail = new
                {
                    emailId = "email.no_findings",
                    from = "lab@forensics.gov",
                    to = "detective@police.gov",
                    subject = "Análise Forense - Sem Resultados",
                    sentAt = DateTime.UtcNow.AddHours(2).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    priority = "normal",
                    visibility = "hidden",
                    content = "A análise forense solicitada não apresentou resultados conclusivos.",
                    attachments = Array.Empty<string>(),
                    metadata = new { }
                }
            }
        };
    }

    private object ExtractMetadata(JsonElement normalizedCase, List<JsonElement> documents)
    {
        // Try to extract from context.plan if available
        string title = "Caso de Investigação";
        string description = "Investigue o caso usando as evidências e documentos disponíveis.";
        string difficulty = "Rookie";
        string location = "Desconhecido";
        string incidentDate = DateTime.UtcNow.AddDays(-7).ToString("yyyy-MM-dd");
        string category = "Investigation";
        
        if (normalizedCase.TryGetProperty("context", out var context))
        {
            if (context.TryGetProperty("plan", out var plan))
            {
                if (plan.TryGetProperty("title", out var titleProp))
                    title = titleProp.GetString() ?? title;
                if (plan.TryGetProperty("description", out var descProp))
                    description = descProp.GetString() ?? description;
                if (plan.TryGetProperty("difficulty", out var diffProp))
                    difficulty = diffProp.GetString() ?? difficulty;
            }
        }

        return new
        {
            title,
            description,
            difficulty,
            estimatedTimeMinutes = GetEstimatedTimeFromDifficulty(difficulty),
            requiredRank = GetRequiredRankFromDifficulty(difficulty),
            location,
            incidentDate,
            category,
            briefing = description,
            victim = new { } // Empty for now
        };
    }

    private object[] BuildAssets(List<JsonElement> documents, List<JsonElement> mediaItems)
    {
        var assets = new List<object>();

        // Convert documents to assets
        foreach (var doc in documents)
        {
            if (!doc.TryGetProperty("docId", out var docIdProp)) continue;
            var docId = docIdProp.GetString();
            if (string.IsNullOrEmpty(docId)) continue;

            var title = doc.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : docId;
            var type = doc.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : "document";
            var words = doc.TryGetProperty("words", out var wordsProp) ? wordsProp.GetInt32() : 0;

            // Determine visibility: initial interviews visible, forensics hidden
            var visibility = DetermineDocumentVisibility(type, docId);

            assets.Add(new
            {
                assetId = ConvertDocIdToAssetId(docId),
                name = title,
                type = "document",
                category = "document",
                description = title,
                filePath = $"/documents/{docId}.pdf",
                visibility,
                checksum = ComputeChecksumFromDoc(doc),
                metadata = new
                {
                    docId,
                    docType = type,
                    words,
                    createdAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                }
            });
        }

        // Convert media to assets
        foreach (var media in mediaItems)
        {
            if (!media.TryGetProperty("evidenceId", out var evidenceIdProp)) continue;
            var evidenceId = evidenceIdProp.GetString();
            if (string.IsNullOrEmpty(evidenceId)) continue;

            var title = media.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : evidenceId;
            var kind = media.TryGetProperty("kind", out var kindProp) ? kindProp.GetString() : "photo";

            // Mugshots visible, other evidence may be hidden
            var visibility = kind == "photo" && evidenceId.Contains("mugshot") ? "initial" : "initial";

            assets.Add(new
            {
                assetId = ConvertEvidenceIdToAssetId(evidenceId),
                name = title,
                type = GetAssetTypeFromMediaKind(kind),
                category = "evidence",
                description = title,
                filePath = $"/media/{evidenceId}.png",
                visibility,
                checksum = "",
                metadata = new
                {
                    evidenceId,
                    kind,
                    createdAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                }
            });
        }

        return assets.ToArray();
    }

    private object[] ExtractSuspects(List<JsonElement> documents, List<JsonElement> mediaItems)
    {
        var suspects = new List<object>();
        var suspectIdMap = new Dictionary<string, object>();

        // Extract suspects from interview documents
        foreach (var doc in documents)
        {
            if (!doc.TryGetProperty("type", out var typeProp) || typeProp.GetString() != "interview") continue;
            if (!doc.TryGetProperty("docId", out var docIdProp)) continue;
            if (!doc.TryGetProperty("title", out var titleProp)) continue;

            var docId = docIdProp.GetString();
            var title = titleProp.GetString();
            
            // Extract suspect ID from docId: doc_interview_s001_001 → S001
            var suspectIdMatch = System.Text.RegularExpressions.Regex.Match(docId, @"_s(\d{3})_", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!suspectIdMatch.Success) continue;

            var suspectNumber = suspectIdMatch.Groups[1].Value;
            var suspectId = $"suspect.{suspectNumber}";

            if (suspectIdMap.ContainsKey(suspectId)) continue;

            // Extract name from title: "Interview with Dana Whitcomb (S001)"
            var name = ExtractNameFromTitle(title);
            
            // Find linked assets (interview + mugshot)
            var linkedAssets = new List<string> { ConvertDocIdToAssetId(docId) };
            
            // Look for mugshot
            var mugshot = mediaItems.FirstOrDefault(m => 
            {
                if (!m.TryGetProperty("evidenceId", out var evId)) return false;
                var evIdStr = evId.GetString();
                return evIdStr != null && evIdStr.Contains($"s{suspectNumber}");
            });
            
            if (mugshot.ValueKind != JsonValueKind.Undefined && mugshot.TryGetProperty("evidenceId", out var mugshotId))
            {
                linkedAssets.Add(ConvertEvidenceIdToAssetId(mugshotId.GetString()));
            }

            var suspect = new
            {
                suspectId,
                name,
                age = 0, // Not in v2 format - could extract from content
                occupation = "Unknown", // Could extract from interview content
                relationship = "Unknown",
                motive = "Under investigation",
                alibi = "To be determined",
                alibiVerified = false,
                background = "", // Could extract from interview sections
                linkedAssets = linkedAssets.ToArray(),
                visibility = "initial"
            };

            suspectIdMap[suspectId] = suspect;
        }

        suspects.AddRange(suspectIdMap.Values);

        // If no suspects found, create a default
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
                background = "",
                linkedAssets = Array.Empty<string>(),
                visibility = "initial"
            });
        }

        return suspects.ToArray();
    }

    private object[] GenerateEmails()
    {
        // Generate initial welcome email
        var emails = new List<object>
        {
            new
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
            }
        };

        return emails.ToArray();
    }

    private object[] GenerateRules(List<JsonElement> documents, object[] suspects)
    {
        var rules = new List<object>();

        // Generate forensic rules for forensics_report documents
        foreach (var doc in documents)
        {
            if (!doc.TryGetProperty("type", out var typeProp) || typeProp.GetString() != "forensics_report") continue;
            if (!doc.TryGetProperty("docId", out var docIdProp)) continue;

            var docId = docIdProp.GetString();
            var assetId = ConvertDocIdToAssetId(docId);

            rules.Add(new
            {
                ruleId = $"rule.forensic_{docId.Replace("-", "_").ToLowerInvariant()}",
                trigger = new
                {
                    type = "forensic_complete",
                    inputAssetId = "asset.evidence_001", // Placeholder
                    analysisType = "DigitalForensics"
                },
                actions = new[]
                {
                    new
                    {
                        type = "reveal_asset",
                        assetId
                    }
                }
            });
        }

        return rules.ToArray();
    }

    private string ConvertCaseId(string caseId)
    {
        // CASE-20260102-e6dcc658 → case_20260102_e6dcc658
        return caseId.ToLowerInvariant().Replace("-", "_");
    }

    private string ConvertDocIdToAssetId(string docId)
    {
        // doc_interview_s001_001 → asset.interview_s001_001
        return $"asset.{docId.Replace("doc_", "")}";
    }

    private string ConvertEvidenceIdToAssetId(string evidenceId)
    {
        // ev_mugshot_s001_001 → asset.ev_mugshot_s001_001
        return $"asset.{evidenceId}";
    }

    private string DetermineDocumentVisibility(string type, string docId)
    {
        // Initial interviews visible, forensics and follow-ups hidden
        if (type == "interview" && docId.Contains("_001"))
            return "initial";
        if (type == "forensics_report")
            return "hidden";
        if (type == "police_report" || type == "admin")
            return "initial";
        
        return "hidden";
    }

    private string GetAssetTypeFromMediaKind(string kind)
    {
        return kind?.ToLowerInvariant() switch
        {
            "photo" => "image",
            "video" => "video",
            "audio" => "audio",
            _ => "image"
        };
    }

    private int GetEstimatedTimeFromDifficulty(string difficulty)
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

    private string GetRequiredRankFromDifficulty(string difficulty)
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

    private string ComputeChecksumFromDoc(JsonElement doc)
    {
        // Compute checksum from sections content
        if (!doc.TryGetProperty("sections", out var sections))
            return "";

        var content = JsonSerializer.Serialize(sections);
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private string ExtractNameFromTitle(string title)
    {
        // Extract name from: "Interview with Dana Whitcomb (S001)"
        var match = System.Text.RegularExpressions.Regex.Match(title, @"with\s+([^(]+)\s*\(", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success)
            return match.Groups[1].Value.Trim();

        return "Unknown Suspect";
    }
}
