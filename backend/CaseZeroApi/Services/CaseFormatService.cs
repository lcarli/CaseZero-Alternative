using System.Text.Json;
using System.Text.RegularExpressions;
using CaseZeroApi.Models;
using Microsoft.Extensions.Logging;

namespace CaseZeroApi.Services
{
    /// <summary>
    /// Service for converting between different case formats
    /// Converts from CaseGen's NormalizedCaseBundle to game's CaseObject format
    /// </summary>
    public class CaseFormatService : ICaseFormatService
    {
        private readonly ILogger<CaseFormatService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public CaseFormatService(ILogger<CaseFormatService> logger)
        {
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public async Task<CaseObject> ConvertToGameFormatAsync(string normalizedCaseJson, CancellationToken cancellationToken = default)
        {
            try
            {
                var normalizedBundle = JsonSerializer.Deserialize<NormalizedCaseBundle>(normalizedCaseJson, _jsonOptions)
                    ?? throw new ArgumentException("Failed to deserialize normalized case bundle");

                return await ConvertToGameFormatAsync(normalizedBundle, cancellationToken);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse normalized case JSON");
                throw new ArgumentException("Invalid JSON format for normalized case bundle", ex);
            }
        }

        public async Task<CaseObject> ConvertToGameFormatAsync(object normalizedBundle, CancellationToken cancellationToken = default)
        {
            if (normalizedBundle is not NormalizedCaseBundle bundle)
            {
                // Try to serialize/deserialize if it's not the right type
                var json = JsonSerializer.Serialize(normalizedBundle, _jsonOptions);
                bundle = JsonSerializer.Deserialize<NormalizedCaseBundle>(json, _jsonOptions)
                    ?? throw new ArgumentException("Cannot convert object to NormalizedCaseBundle");
            }

            return await Task.FromResult(ConvertBundleToCaseObject(bundle));
        }

        public async Task<FormatValidationResult> ValidateForGameFormatAsync(string normalizedCaseJson, CancellationToken cancellationToken = default)
        {
            var result = new FormatValidationResult();

            try
            {
                var bundle = JsonSerializer.Deserialize<NormalizedCaseBundle>(normalizedCaseJson, _jsonOptions);
                if (bundle == null)
                {
                    result.Issues.Add("Failed to deserialize normalized case bundle");
                    return result;
                }

                // Validate required fields
                if (string.IsNullOrEmpty(bundle.CaseId))
                    result.Issues.Add("Missing caseId");

                // Check version and validate accordingly
                if (bundle.Version == "v2-hierarchical")
                {
                    result.Warnings.Add("v2-hierarchical format requires loading individual documents");
                    if (bundle.Documents == null || bundle.Documents.Items.Count == 0)
                        result.Issues.Add("No documents found in bundle");
                }
                else
                {
                    result.Issues.Add("Unsupported bundle format version - CaseFormatService needs update for v2-hierarchical");
                }

                result.IsValid = result.Issues.Count == 0;
            }
            catch (Exception ex)
            {
                result.Issues.Add($"Validation error: {ex.Message}");
            }

            return await Task.FromResult(result);
        }

        private CaseObject ConvertBundleToCaseObject(NormalizedCaseBundle bundle)
        {
            // TODO: Update this method to support v2-hierarchical format
            // For now, throw NotImplementedException for v2 format
            if (bundle.Version == "v2-hierarchical")
            {
                throw new NotImplementedException(
                    "v2-hierarchical format conversion not yet implemented. " +
                    "This format uses document references instead of embedded documents. " +
                    "Dashboard can display these cases, but full game format conversion requires loading individual documents.");
            }

            // Legacy code for old format (kept for backward compatibility)
            throw new NotImplementedException("Legacy format conversion also needs updating after model changes");
        }

        /*
        // TEMPORARILY DISABLED: These methods need to be rewritten for v2-hierarchical format
        // The v2 format uses document references instead of embedded documents
        // To re-enable, need to:
        // 1. Load individual documents from blob storage
        // 2. Convert DocumentsCollection.Items references to actual document objects
        // 3. Update all LINQ queries to work with loaded documents

        private CaseMetadata ConvertMetadata(NormalizedCaseBundle bundle)
        {
            // Extract metadata from documents (particularly police reports)
            var policeReport = bundle.Documents.FirstOrDefault(d => d.Type == "police_report");
            var title = ExtractTitleFromDocument(policeReport) ?? bundle.CaseId;
            var description = ExtractDescriptionFromDocument(policeReport) ?? "AI Generated Investigation Case";

            return new CaseMetadata
            {
                Title = title,
                Description = description,
                StartDateTime = DateTime.UtcNow,
                Location = ExtractLocationFromDocuments(bundle.Documents),
                IncidentDateTime = bundle.CreatedAt,
                VictimInfo = ExtractVictimInfo(bundle.Documents),
                Briefing = ExtractBriefingFromDocuments(bundle.Documents),
                Difficulty = ConvertDifficultyToInt(bundle.Difficulty),
                EstimatedDuration = "60 minutes",
                MinRankRequired = ConvertDifficultyToRank(bundle.Difficulty)
            };
        }

        private List<CaseEvidence> ConvertToEvidences(NormalizedCaseBundle bundle)
        {
            var evidences = new List<CaseEvidence>();

            // Convert documents to evidences (especially evidence logs and reports)
            foreach (var doc in bundle.Documents)
            {
                if (ShouldConvertDocumentToEvidence(doc))
                {
                    evidences.Add(new CaseEvidence
                    {
                        Id = doc.DocId,
                        Name = doc.Title,
                        Type = MapDocumentTypeToEvidenceType(doc.Type),
                        FileName = $"{doc.DocId}.pdf",
                        Category = "document",
                        Priority = doc.Gated ? "high" : "medium",
                        Description = ExtractDescriptionFromContent(doc.Content),
                        Location = ExtractLocationFromContent(doc.Content),
                        IsUnlocked = !doc.Gated,
                        RequiresAnalysis = doc.Type == "forensics_report",
                        DependsOn = GetDocumentDependencies(doc, bundle.GatingGraph),
                        LinkedSuspects = ExtractLinkedSuspectsFromContent(doc.Content),
                        AnalysisRequired = GetRequiredAnalysisTypes(doc),
                        UnlockConditions = ConvertGatingRuleToUnlockConditions(doc.GatingRule)
                    });
                }
            }

            // Convert media to evidences
            foreach (var media in bundle.Media)
            {
                evidences.Add(new CaseEvidence
                {
                    Id = media.EvidenceId,
                    Name = media.Title,
                    Type = MapMediaKindToEvidenceType(media.Kind),
                    FileName = $"{media.EvidenceId}.{GetFileExtensionForMediaKind(media.Kind)}",
                    Category = "physical",
                    Priority = "medium",
                    Description = media.Prompt,
                    Location = ExtractLocationFromMetadata(media.Metadata),
                    IsUnlocked = !media.Deferred,
                    RequiresAnalysis = MediaRequiresAnalysis(media.Kind),
                    DependsOn = GetMediaDependencies(media, bundle.GatingGraph),
                    LinkedSuspects = new List<string>(),
                    AnalysisRequired = GetRequiredAnalysisForMedia(media.Kind),
                    UnlockConditions = new CaseUnlockConditions { Immediate = !media.Deferred }
                });
            }

            return evidences;
        }

        private List<CaseSuspect> ConvertToSuspects(NormalizedCaseBundle bundle)
        {
            var suspects = new List<CaseSuspect>();
            
            // Extract suspects from interview documents
            var interviewDocs = bundle.Documents.Where(d => d.Type == "interview").ToList();
            
            foreach (var interview in interviewDocs)
            {
                var suspect = ExtractSuspectFromInterview(interview);
                if (suspect != null)
                {
                    suspects.Add(suspect);
                }
            }

            // If no interviews found, create generic suspects based on other documents
            if (suspects.Count == 0)
            {
                suspects.AddRange(GenerateGenericSuspects(bundle.Documents));
            }

            return suspects;
        }

        private List<CaseForensicAnalysis> ConvertToForensicAnalyses(NormalizedCaseBundle bundle)
        {
            var analyses = new List<CaseForensicAnalysis>();
            
            // Convert forensics reports to analyses
            var forensicsReports = bundle.Documents.Where(d => d.Type == "forensics_report").ToList();
            
            foreach (var report in forensicsReports)
            {
                analyses.Add(new CaseForensicAnalysis
                {
                    Id = $"analysis_{report.DocId}",
                    EvidenceId = ExtractEvidenceIdFromForensicsReport(report),
                    AnalysisType = ExtractAnalysisTypeFromReport(report),
                    ResponseTime = 30, // Default 30 minutes
                    ResultFile = $"{report.DocId}.pdf",
                    Description = ExtractDescriptionFromContent(report.Content)
                });
            }

            return analyses;
        }

        private List<CaseTemporalEvent> ConvertToTemporalEvents(NormalizedCaseBundle bundle)
        {
            var events = new List<CaseTemporalEvent>();
            
            // Extract temporal events from document content
            foreach (var doc in bundle.Documents)
            {
                var extractedEvents = ExtractTemporalEventsFromContent(doc.Content, doc.DocId);
                events.AddRange(extractedEvents);
            }

            return events;
        }

        private List<CaseTimelineEvent> ConvertToTimeline(NormalizedCaseBundle bundle)
        {
            var timeline = new List<CaseTimelineEvent>();
            
            // Create timeline from temporal events and document timestamps
            foreach (var doc in bundle.Documents)
            {
                if (doc.CreatedAt.HasValue)
                {
                    timeline.Add(new CaseTimelineEvent
                    {
                        Time = doc.CreatedAt.Value,
                        Event = $"Document created: {doc.Title}",
                        Source = doc.Type
                    });
                }
            }

            return timeline.OrderBy(t => t.Time).ToList();
        }

        private CaseSolution GenerateSolution(NormalizedCaseBundle bundle)
        {
            // This is complex - for now, generate a basic solution structure
            var interviewDocs = bundle.Documents.Where(d => d.Type == "interview").ToList();
            var culprit = interviewDocs.FirstOrDefault()?.DocId ?? "unknown";

            var keyEvidenceList = bundle.Documents.Where(d => d.Gated).Select(d => d.DocId).Take(3).ToList();
            
            return new CaseSolution
            {
                Culprit = culprit,
                KeyEvidence = keyEvidenceList.FirstOrDefault() ?? "unknown",
                Explanation = "Complete the investigation by examining all evidence and interviewing suspects.",
                RequiredEvidence = keyEvidenceList,
                MinimumScore = 75
            };
        }

        private CaseUnlockLogic ConvertToUnlockLogic(NormalizedCaseBundle bundle)
        {
            var progressionRules = new List<CaseProgressionRule>();
            var analysisRules = new List<CaseAnalysisRule>();

            // Convert gating graph to progression rules
            foreach (var node in bundle.GatingGraph.Nodes)
            {
                if (node.Gated && node.RequiredIds.Any())
                {
                    progressionRules.Add(new CaseProgressionRule
                    {
                        Condition = node.Type == "evidence" ? "evidence_examined" : "document_reviewed",
                        Target = node.Id,
                        Unlocks = node.RequiredIds.ToList(),
                        Delay = 0
                    });
                }
            }

            // Create analysis rules for forensic reports
            var forensicsReports = bundle.Documents.Where(d => d.Type == "forensics_report").ToList();
            foreach (var report in forensicsReports)
            {
                var evidenceId = ExtractEvidenceIdFromForensicsReport(report);
                if (!string.IsNullOrEmpty(evidenceId))
                {
                    analysisRules.Add(new CaseAnalysisRule
                    {
                        EvidenceId = evidenceId,
                        AnalysisType = ExtractAnalysisTypeFromReport(report),
                        Unlocks = new List<string>(),
                        Critical = false
                    });
                }
            }

            return new CaseUnlockLogic
            {
                ProgressionRules = progressionRules,
                AnalysisRules = analysisRules
            };
        }

        private CaseGameMetadata GenerateGameMetadata(NormalizedCaseBundle bundle)
        {
            return new CaseGameMetadata
            {
                Version = "1.0",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "CaseFormatService",
                EstimatedPlayTime = "60 minutes",
                Tags = new List<string> { "ai-generated", bundle.Difficulty?.ToLowerInvariant() ?? "medium" },
                Difficulty = bundle.Difficulty ?? "medium"
            };
        }

        #region Helper Methods

        private bool ShouldConvertDocumentToEvidence(NormalizedDocument doc)
        {
            return doc.Type is "evidence_log" or "forensics_report" or "witness_statement";
        }

        private string MapDocumentTypeToEvidenceType(string docType)
        {
            return docType switch
            {
                "evidence_log" => "physical",
                "forensics_report" => "forensic",
                "witness_statement" => "testimony",
                "police_report" => "report",
                _ => "document"
            };
        }

        private string MapMediaKindToEvidenceType(string mediaKind)
        {
            return mediaKind switch
            {
                "photo" => "photo",
                "document_scan" => "document",
                "diagram" => "diagram",
                "audio" => "audio",
                "video" => "video",
                _ => "unknown"
            };
        }

        private string GetFileExtensionForMediaKind(string mediaKind)
        {
            return mediaKind switch
            {
                "photo" => "jpg",
                "document_scan" => "pdf",
                "diagram" => "png",
                "audio" => "mp3",
                "video" => "mp4",
                _ => "bin"
            };
        }

        private int ConvertDifficultyToInt(string? difficulty)
        {
            return difficulty?.ToLowerInvariant() switch
            {
                "rookie" => 1,
                "detective" => 2,
                "detective2" => 3,
                "sergeant" => 4,
                "lieutenant" => 5,
                "captain" => 6,
                "commander" => 7,
                _ => 2
            };
        }

        private string ConvertDifficultyToRank(string? difficulty)
        {
            return difficulty?.ToLowerInvariant() switch
            {
                "rookie" => "Rookie",
                "detective" => "Detective",
                "detective2" => "Detective II",
                "sergeant" => "Sergeant",
                "lieutenant" => "Lieutenant",
                "captain" => "Captain",
                "commander" => "Commander",
                _ => "Detective"
            };
        }

        private string? ExtractTitleFromDocument(NormalizedDocument? doc)
        {
            return doc?.Title;
        }

        private string? ExtractDescriptionFromDocument(NormalizedDocument? doc)
        {
            if (doc?.Content == null) return null;
            
            // Extract first paragraph or sentence as description
            var lines = doc.Content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            return lines.FirstOrDefault(l => !l.StartsWith('#') && !string.IsNullOrWhiteSpace(l));
        }

        private string ExtractLocationFromDocuments(List<NormalizedDocument> documents)
        {
            foreach (var doc in documents)
            {
                var location = ExtractLocationFromContent(doc.Content);
                if (!string.IsNullOrEmpty(location))
                    return location;
            }
            return "Unknown Location";
        }

        private string ExtractLocationFromContent(string content)
        {
            // Simple regex to find location patterns
            var locationPattern = @"(?i)(?:location|address|at|near):\s*([^\n\r]+)";
            var match = Regex.Match(content, locationPattern);
            return match.Success ? match.Groups[1].Value.Trim() : "";
        }

        private string ExtractLocationFromMetadata(Dictionary<string, object> metadata)
        {
            if (metadata.TryGetValue("location", out var location))
                return location.ToString() ?? "";
            return "";
        }

        private CaseVictimInfo ExtractVictimInfo(List<NormalizedDocument> documents)
        {
            // Extract victim information from police reports and other documents
            foreach (var doc in documents.Where(d => d.Type == "police_report"))
            {
                var victimInfo = ExtractVictimFromContent(doc.Content);
                if (victimInfo != null)
                    return victimInfo;
            }

            return new CaseVictimInfo
            {
                Name = "Unknown Victim",
                Age = 0,
                Occupation = "Unknown",
                CauseOfDeath = "Under Investigation"
            };
        }

        private CaseVictimInfo? ExtractVictimFromContent(string content)
        {
            // Simple pattern matching for victim information
            var nameMatch = Regex.Match(content, @"(?i)victim[:\s]+([^\n,]+)");
            var ageMatch = Regex.Match(content, @"(?i)age[:\s]+(\d+)");
            var occupationMatch = Regex.Match(content, @"(?i)occupation[:\s]+([^\n,]+)");

            if (nameMatch.Success)
            {
                return new CaseVictimInfo
                {
                    Name = nameMatch.Groups[1].Value.Trim(),
                    Age = ageMatch.Success ? int.Parse(ageMatch.Groups[1].Value) : 0,
                    Occupation = occupationMatch.Success ? occupationMatch.Groups[1].Value.Trim() : "Unknown",
                    CauseOfDeath = "Under Investigation"
                };
            }

            return null;
        }

        private string ExtractBriefingFromDocuments(List<NormalizedDocument> documents)
        {
            var policeReport = documents.FirstOrDefault(d => d.Type == "police_report");
            if (policeReport != null)
            {
                var lines = policeReport.Content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var briefingLines = lines.Take(3).Where(l => !l.StartsWith('#'));
                return string.Join(" ", briefingLines);
            }

            return "Investigate the case by examining evidence and interviewing suspects.";
        }

        private string ExtractDescriptionFromContent(string content)
        {
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            return lines.FirstOrDefault(l => !l.StartsWith('#') && !string.IsNullOrWhiteSpace(l)) ?? "";
        }

        private List<string> ExtractLinkedSuspectsFromContent(string content)
        {
            var suspects = new List<string>();
            var suspectPattern = @"(?i)suspect[:\s]+([^\n,]+)";
            var matches = Regex.Matches(content, suspectPattern);
            
            foreach (Match match in matches)
            {
                suspects.Add(match.Groups[1].Value.Trim());
            }

            return suspects;
        }

        private List<string> GetDocumentDependencies(NormalizedDocument doc, GatingGraph gatingGraph)
        {
            var node = gatingGraph.Nodes.FirstOrDefault(n => n.Id == doc.DocId);
            return node?.RequiredIds ?? new List<string>();
        }

        private List<string> GetMediaDependencies(NormalizedMedia media, GatingGraph gatingGraph)
        {
            var node = gatingGraph.Nodes.FirstOrDefault(n => n.Id == media.EvidenceId);
            return node?.RequiredIds ?? new List<string>();
        }

        private List<string> GetRequiredAnalysisTypes(NormalizedDocument doc)
        {
            return doc.Type == "forensics_report" ? new List<string> { "forensic" } : new List<string>();
        }

        private bool MediaRequiresAnalysis(string mediaKind)
        {
            return mediaKind is "photo" or "document_scan" or "audio" or "video";
        }

        private List<string> GetRequiredAnalysisForMedia(string mediaKind)
        {
            return mediaKind switch
            {
                "photo" => new List<string> { "photo_analysis" },
                "document_scan" => new List<string> { "document_analysis" },
                "audio" => new List<string> { "audio_analysis" },
                "video" => new List<string> { "video_analysis" },
                _ => new List<string>()
            };
        }

        private CaseUnlockConditions ConvertGatingRuleToUnlockConditions(GatingRule? gatingRule)
        {
            if (gatingRule == null)
                return new CaseUnlockConditions { Immediate = true };

            return new CaseUnlockConditions
            {
                Immediate = false,
                RequiredEvidence = !string.IsNullOrEmpty(gatingRule.EvidenceId) 
                    ? new List<string> { gatingRule.EvidenceId } 
                    : new List<string>(),
                RequiredAnalysis = new List<string>()
            };
        }

        private CaseSuspect? ExtractSuspectFromInterview(NormalizedDocument interview)
        {
            var suspectName = ExtractSuspectNameFromInterview(interview.Content);
            if (string.IsNullOrEmpty(suspectName))
                return null;

            return new CaseSuspect
            {
                Id = $"suspect_{interview.DocId}",
                Name = suspectName,
                Age = ExtractAgeFromContent(interview.Content),
                Occupation = ExtractOccupationFromContent(interview.Content),
                Description = ExtractDescriptionFromContent(interview.Content),
                Relationship = ExtractRelationshipFromContent(interview.Content),
                Motive = ExtractMotiveFromContent(interview.Content),
                Alibi = ExtractAlibiFromContent(interview.Content),
                AlibiVerified = false,
                Behavior = ExtractBehaviorFromContent(interview.Content),
                BackgroundInfo = ExtractBackgroundFromContent(interview.Content),
                LinkedEvidence = new List<string>(),
                Comments = "",
                IsActualCulprit = false, // This would need more sophisticated analysis
                Status = "Person of Interest",

                UnlockConditions = ConvertGatingRuleToUnlockConditions(interview.GatingRule)
            };
        }

        private List<CaseSuspect> GenerateGenericSuspects(List<NormalizedDocument> documents)
        {
            // Generate basic suspects based on document analysis
            var suspects = new List<CaseSuspect>();
            
            // This is a fallback - in a real implementation, you'd use more sophisticated NLP
            suspects.Add(new CaseSuspect
            {
                Id = "suspect_generic_1",
                Name = "Person of Interest #1",
                Age = 35,
                Occupation = "Unknown",
                Description = "Individual identified during investigation",
                Relationship = "Unknown",
                Motive = "Under investigation",
                Alibi = "To be verified",
                AlibiVerified = false,
                Behavior = "Cooperative",
                BackgroundInfo = "Background check pending",
                LinkedEvidence = new List<string>(),
                Comments = "Generated suspect based on case documents",
                IsActualCulprit = false,
                Status = "Person of Interest",

                UnlockConditions = new CaseUnlockConditions { Immediate = true }
            });

            return suspects;
        }

        private string ExtractSuspectNameFromInterview(string content)
        {
            var namePattern = @"(?i)(?:interview with|interviewee|suspect)[:\s]+([^\n,]+)";
            var match = Regex.Match(content, namePattern);
            return match.Success ? match.Groups[1].Value.Trim() : "";
        }

        private int ExtractAgeFromContent(string content)
        {
            var agePattern = @"(?i)age[:\s]+(\d+)";
            var match = Regex.Match(content, agePattern);
            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
        }

        private string ExtractOccupationFromContent(string content)
        {
            var occupationPattern = @"(?i)occupation[:\s]+([^\n,]+)";
            var match = Regex.Match(content, occupationPattern);
            return match.Success ? match.Groups[1].Value.Trim() : "Unknown";
        }

        private string ExtractRelationshipFromContent(string content)
        {
            var relationshipPattern = @"(?i)relationship[:\s]+([^\n,]+)";
            var match = Regex.Match(content, relationshipPattern);
            return match.Success ? match.Groups[1].Value.Trim() : "Unknown";
        }

        private string ExtractMotiveFromContent(string content)
        {
            var motivePattern = @"(?i)motive[:\s]+([^\n,]+)";
            var match = Regex.Match(content, motivePattern);
            return match.Success ? match.Groups[1].Value.Trim() : "Unknown";
        }

        private string ExtractAlibiFromContent(string content)
        {
            var alibiPattern = @"(?i)alibi[:\s]+([^\n,]+)";
            var match = Regex.Match(content, alibiPattern);
            return match.Success ? match.Groups[1].Value.Trim() : "No alibi provided";
        }

        private string ExtractBehaviorFromContent(string content)
        {
            var behaviorPattern = @"(?i)behavior[:\s]+([^\n,]+)";
            var match = Regex.Match(content, behaviorPattern);
            return match.Success ? match.Groups[1].Value.Trim() : "Normal";
        }

        private string ExtractBackgroundFromContent(string content)
        {
            var backgroundPattern = @"(?i)background[:\s]+([^\n,]+)";
            var match = Regex.Match(content, backgroundPattern);
            return match.Success ? match.Groups[1].Value.Trim() : "No background information available";
        }

        private string ExtractEvidenceIdFromForensicsReport(NormalizedDocument report)
        {
            // Look for patterns like "Evidence ID: evidence_001" or "Evidence: evidence_001"
            var evidencePattern = @"(?i)evidence\s+(?:id\s*[:]\s*|[:]\s*)([^\n\s,]+)";
            var match = Regex.Match(report.Content, evidencePattern);
            return match.Success ? match.Groups[1].Value.Trim() : $"evidence_{report.DocId}";
        }

        #endregion
        */
    }
}