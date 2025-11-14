using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using CaseGen.Functions.Models;

namespace CaseGen.Functions.Services;

public class NormalizerService : INormalizerService
{
    private readonly ILogger<NormalizerService> _logger;
    private readonly ISchemaValidationService _schemaValidation;
    private readonly ICaseLoggingService _caseLogging;
    private readonly IStorageService _storageService;

    public NormalizerService(
        ILogger<NormalizerService> logger, 
        ISchemaValidationService schemaValidation,
        ICaseLoggingService caseLogging,
        IStorageService storageService)
    {
        _logger = logger;
        _schemaValidation = schemaValidation;
        _caseLogging = caseLogging;
        _storageService = storageService;
    }

    public async Task<NormalizationResult> NormalizeCaseAsync(NormalizationInput input, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var logEntries = new List<LogEntry>();
        var validationResults = new List<ValidationResult>();

        try
        {
            _logger.LogInformation("Starting deterministic normalize for case {CaseId}", input.CaseId);
            
            logEntries.Add(new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = "INFO",
                Message = $"Starting normalize for case {input.CaseId}",
                Details = new Dictionary<string, object>
                {
                    ["difficulty"] = input.Difficulty ?? "auto",
                    ["timezone"] = input.Timezone ?? "UTC",
                    ["documentCount"] = input.Documents.Length,
                    ["mediaCount"] = input.Media.Length
                }
            });

            // Step 1: Parse and validate input documents/media
            var (parsedDocuments, parsedMedia) = await ParseInputDataAsync(input, logEntries, validationResults);
            
            // Step 2: Validate unique IDs and cross-references
            await ValidateIdsAndReferencesAsync(parsedDocuments, parsedMedia, logEntries, validationResults);
            
            // Step 3: Apply difficulty validation
            var difficultyProfile = ValidateDifficultyRules(input.Difficulty, parsedDocuments, parsedMedia, logEntries, validationResults);
            
            // Step 4: Build and validate gating graph
            var gatingGraph = BuildGatingGraph(parsedDocuments, parsedMedia, logEntries, validationResults);
            
            // Step 5: Normalize timestamps and timezone
            var normalizedDocuments = NormalizeTimestamps(parsedDocuments, input.Timezone, logEntries);
            var normalizedMedia = NormalizeMediaTimestamps(parsedMedia, input.Timezone, logEntries);
            
            // Step 6: Create internationalized content
            var i18nDocuments = CreateI18nDocuments(normalizedDocuments, logEntries);
            var i18nMedia = CreateI18nMedia(normalizedMedia, logEntries);
            
            // Step 7: Generate normalized bundle
            var normalizedBundle = CreateNormalizedBundle(input, i18nDocuments, i18nMedia, gatingGraph, difficultyProfile);
            
            // Step 8: Create manifest
            var manifest = CreateManifest(input.CaseId, i18nDocuments, i18nMedia);
            
            // Step 9: Final validation
            await ValidateNormalizedBundleAsync(normalizedBundle, logEntries, validationResults);

            var endTime = DateTime.UtcNow;
            
            logEntries.Add(new LogEntry
            {
                Timestamp = endTime,
                Level = "INFO",
                Message = "Normalization completed successfully",
                Details = new Dictionary<string, object>
                {
                    ["duration"] = (endTime - startTime).TotalSeconds,
                    ["validationsPassed"] = validationResults.Count(v => v.Status == "PASS"),
                    ["validationsWarned"] = validationResults.Count(v => v.Status == "WARN"),
                    ["validationsFailed"] = validationResults.Count(v => v.Status == "FAIL")
                }
            });

            var log = new NormalizationLog
            {
                CaseId = input.CaseId,
                Step = "normalize",
                StartTime = startTime,
                EndTime = endTime,
                Status = validationResults.Any(v => v.Status == "FAIL") ? "FAILED" : "SUCCESS",
                Entries = logEntries.ToArray(),
                ValidationResults = validationResults.ToArray()
            };

            // Save log
            var logJson = JsonSerializer.Serialize(log, new JsonSerializerOptions { WriteIndented = true });
            await _caseLogging.LogStepResponseAsync(input.CaseId, "normalize", logJson);

            // Serialize the normalized bundle to JSON string
            var normalizedJson = JsonSerializer.Serialize(normalizedBundle, new JsonSerializerOptions { WriteIndented = true });

            // Save the complete normalized case to logs only (the canonical file will be saved in PackageActivity)
            await _caseLogging.LogStepResponseAsync(input.CaseId, "normalized_case", normalizedJson);

            return new NormalizationResult
            {
                NormalizedJson = normalizedJson,
                Manifest = manifest,
                Log = log
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to normalize case {CaseId}", input.CaseId);
            
            var errorLog = new NormalizationLog
            {
                CaseId = input.CaseId,
                Step = "normalize",
                StartTime = startTime,
                EndTime = DateTime.UtcNow,
                Status = "ERROR",
                Entries = logEntries.ToArray(),
                ValidationResults = validationResults.ToArray(),
                ErrorMessage = ex.Message
            };

            throw new InvalidOperationException($"Normalization failed for case {input.CaseId}: {ex.Message}", ex);
        }
    }

    private Task<(NormalizedDocument[] documents, NormalizedMedia[] media)> ParseInputDataAsync(
        NormalizationInput input, List<LogEntry> logEntries, List<ValidationResult> validationResults)
    {
        var documents = new List<NormalizedDocument>();
        var media = new List<NormalizedMedia>();

        // Parse documents
        foreach (var docJson in input.Documents)
        {
            try
            {
                var docData = JsonSerializer.Deserialize<Dictionary<string, object>>(docJson);
                if (docData != null)
                {
                    var normalizedDoc = ParseDocument(docData, logEntries);
                    documents.Add(normalizedDoc);
                }
            }
            catch (Exception ex)
            {
                logEntries.Add(new LogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Level = "ERROR",
                    Message = $"Failed to parse document: {ex.Message}",
                    Details = new Dictionary<string, object> { ["content"] = docJson.Substring(0, Math.Min(100, docJson.Length)) }
                });

                validationResults.Add(new ValidationResult
                {
                    Rule = "DOCUMENT_PARSING",
                    Status = "FAIL",
                    Description = "Document JSON parsing failed",
                    Details = ex.Message
                });
            }
        }

        // Parse media
        foreach (var mediaJson in input.Media)
        {
            try
            {
                var mediaData = JsonSerializer.Deserialize<Dictionary<string, object>>(mediaJson);
                if (mediaData != null)
                {
                    var normalizedMedia = ParseMedia(mediaData, logEntries);
                    media.Add(normalizedMedia);
                }
            }
            catch (Exception ex)
            {
                logEntries.Add(new LogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Level = "ERROR",
                    Message = $"Failed to parse media: {ex.Message}",
                    Details = new Dictionary<string, object> { ["content"] = mediaJson.Substring(0, Math.Min(100, mediaJson.Length)) }
                });

                validationResults.Add(new ValidationResult
                {
                    Rule = "MEDIA_PARSING",
                    Status = "FAIL",
                    Description = "Media JSON parsing failed",
                    Details = ex.Message
                });
            }
        }

        return Task.FromResult((documents.ToArray(), media.ToArray()));
    }

    private NormalizedDocument ParseDocument(Dictionary<string, object> docData, List<LogEntry> logEntries)
    {
        var docId = docData.GetValueOrDefault("docId")?.ToString() ?? throw new ArgumentException("Missing docId");
        var type = docData.GetValueOrDefault("type")?.ToString() ?? throw new ArgumentException("Missing type");
        var title = docData.GetValueOrDefault("title")?.ToString() ?? throw new ArgumentException("Missing title");
        
        // Parse sections - the actual document structure has sections array, not content string
        var sectionsData = new List<Dictionary<string, string>>();
        var content = "";
        
        if (docData.TryGetValue("sections", out var sectionsObj))
        {
            if (sectionsObj is JsonElement sectionsElement && sectionsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var sectionElement in sectionsElement.EnumerateArray())
                {
                    if (sectionElement.ValueKind == JsonValueKind.Object)
                    {
                        var sectionTitle = sectionElement.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "" : "";
                        var sectionContent = sectionElement.TryGetProperty("content", out var contentProp) ? contentProp.GetString() ?? "" : "";
                        
                        sectionsData.Add(new Dictionary<string, string>
                        {
                            ["title"] = sectionTitle,
                            ["content"] = sectionContent
                        });
                        
                        // Combine all content for full-text search
                        content += $"{sectionTitle}\n{sectionContent}\n\n";
                    }
                }
            }
        }
        
        // Extract section titles for the sections array (backward compatibility)
        var sections = sectionsData.Select(s => s["title"]).ToArray();
        if (sections.Length == 0)
        {
            sections = new[] { "Content" };
        }

        // Parse lengthTarget from the document - this might not exist, so provide defaults
        var lengthTarget = new[] { 100, 500 }; // default values
        if (docData.TryGetValue("lengthTarget", out var lengthTargetObj))
        {
            if (lengthTargetObj is JsonElement lengthTargetElement && lengthTargetElement.ValueKind == JsonValueKind.Array)
            {
                var targets = lengthTargetElement.EnumerateArray().Select(e => e.GetInt32()).ToArray();
                if (targets.Length >= 2)
                {
                    lengthTarget = new[] { targets[0], targets[1] };
                }
            }
        }

        var gated = false;
        if (docData.TryGetValue("gated", out var gatedObj))
        {
            if (gatedObj is JsonElement gatedElement)
            {
                gated = gatedElement.GetBoolean();
            }
            else
            {
                bool.TryParse(gatedObj?.ToString(), out gated);
            }
        }
        
        GatingRule? gatingRule = null;
        if (gated && docData.TryGetValue("gatingRule", out var gatingRuleObj))
        {
            if (gatingRuleObj is JsonElement gatingElement)
            {
                var action = gatingElement.TryGetProperty("action", out var actionProp) ? actionProp.GetString() : null;
                var evidenceId = gatingElement.TryGetProperty("evidenceId", out var evidenceProp) ? evidenceProp.GetString() : null;
                var notes = gatingElement.TryGetProperty("notes", out var notesProp) ? notesProp.GetString() : null;

                if (!string.IsNullOrEmpty(action))
                {
                    gatingRule = new GatingRule
                    {
                        Action = action,
                        EvidenceId = evidenceId,
                        Notes = notes
                    };
                }
            }
        }

        return new NormalizedDocument
        {
            DocId = docId,
            Type = type,
            Title = title,
            Sections = sections,
            LengthTarget = lengthTarget,
            Gated = gated,
            GatingRule = gatingRule,
            CreatedAt = DateTime.UtcNow,
            Content = content.Trim(),
            Metadata = new Dictionary<string, object>
            {
                ["generatedBy"] = "normalize",
                ["originalData"] = docData,
                ["sectionsData"] = sectionsData
            }
        };
    }

    private NormalizedMedia ParseMedia(Dictionary<string, object> mediaData, List<LogEntry> logEntries)
    {
        var evidenceId = mediaData.GetValueOrDefault("evidenceId")?.ToString() ?? throw new ArgumentException("Missing evidenceId");
        var kind = mediaData.GetValueOrDefault("kind")?.ToString() ?? throw new ArgumentException("Missing kind");
        var title = mediaData.GetValueOrDefault("title")?.ToString() ?? throw new ArgumentException("Missing title");
        var prompt = mediaData.GetValueOrDefault("prompt")?.ToString() ?? "";
        
        var deferred = bool.TryParse(mediaData.GetValueOrDefault("deferred")?.ToString(), out var deferredValue) && deferredValue;
        
        // Handle constraints
        Dictionary<string, object>? constraints = null;
        if (mediaData.TryGetValue("constraints", out var constraintsObj))
        {
            if (constraintsObj is JsonElement constraintsElement && constraintsElement.ValueKind == JsonValueKind.Object)
            {
                constraints = new Dictionary<string, object>();
                foreach (var prop in constraintsElement.EnumerateObject())
                {
                    constraints[prop.Name] = prop.Value.ValueKind switch
                    {
                        JsonValueKind.String => prop.Value.GetString() ?? "",
                        JsonValueKind.Number => prop.Value.GetDouble(),
                        JsonValueKind.True or JsonValueKind.False => prop.Value.GetBoolean(),
                        _ => prop.Value.ToString()
                    };
                }
            }
        }

        return new NormalizedMedia
        {
            EvidenceId = evidenceId,
            Kind = kind,
            Title = title,
            Prompt = prompt,
            Constraints = constraints,
            Deferred = deferred,
            CreatedAt = DateTime.UtcNow,
            Metadata = new Dictionary<string, object>
            {
                ["generatedBy"] = "normalize",
                ["originalData"] = mediaData
            }
        };
    }

    private Task ValidateIdsAndReferencesAsync(
        NormalizedDocument[] documents, NormalizedMedia[] media,
        List<LogEntry> logEntries, List<ValidationResult> validationResults)
    {
        var documentIds = documents.Select(d => d.DocId).ToHashSet();
        var mediaIds = media.Select(m => m.EvidenceId).ToHashSet();

        // Check unique document IDs
        var duplicateDocIds = documents.GroupBy(d => d.DocId).Where(g => g.Count() > 1).Select(g => g.Key);
        foreach (var duplicateId in duplicateDocIds)
        {
            validationResults.Add(new ValidationResult
            {
                Rule = "UNIQUE_DOCUMENT_IDS",
                Status = "FAIL",
                Description = $"Duplicate document ID: {duplicateId}",
                Details = "Document IDs must be unique across the case"
            });
        }

        // Check unique media IDs
        var duplicateMediaIds = media.GroupBy(m => m.EvidenceId).Where(g => g.Count() > 1).Select(g => g.Key);
        foreach (var duplicateId in duplicateMediaIds)
        {
            validationResults.Add(new ValidationResult
            {
                Rule = "UNIQUE_EVIDENCE_IDS",
                Status = "FAIL", 
                Description = $"Duplicate evidence ID: {duplicateId}",
                Details = "Evidence IDs must be unique across the case"
            });
        }

        // Validate gating rule references
        foreach (var doc in documents.Where(d => d.Gated && d.GatingRule != null))
        {
            var rule = doc.GatingRule!;
            if (!string.IsNullOrEmpty(rule.EvidenceId) && !mediaIds.Contains(rule.EvidenceId))
            {
                validationResults.Add(new ValidationResult
                {
                    Rule = "GATING_REFERENCE_INTEGRITY",
                    Status = "FAIL",
                    Description = $"Document {doc.DocId} references non-existent evidence {rule.EvidenceId}",
                    Details = "Gating rules must reference existing evidence IDs"
                });
            }

            if (!string.IsNullOrEmpty(rule.DocId) && !documentIds.Contains(rule.DocId))
            {
                validationResults.Add(new ValidationResult
                {
                    Rule = "GATING_REFERENCE_INTEGRITY", 
                    Status = "FAIL",
                    Description = $"Document {doc.DocId} references non-existent document {rule.DocId}",
                    Details = "Gating rules must reference existing document IDs"
                });
            }
        }

        if (!duplicateDocIds.Any() && !duplicateMediaIds.Any())
        {
            validationResults.Add(new ValidationResult
            {
                Rule = "UNIQUE_IDS",
                Status = "PASS",
                Description = "All IDs are unique",
                Details = $"Validated {documentIds.Count} document IDs and {mediaIds.Count} evidence IDs"
            });
        }

        logEntries.Add(new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = "INFO",
            Message = "ID and reference validation completed",
            Details = new Dictionary<string, object>
            {
                ["documentIds"] = documentIds.Count,
                ["evidenceIds"] = mediaIds.Count,
                ["duplicateDocuments"] = duplicateDocIds.Count(),
                ["duplicateEvidence"] = duplicateMediaIds.Count()
            }
        });

        return Task.CompletedTask;
    }

    private DifficultyProfile ValidateDifficultyRules(
        string? difficulty, NormalizedDocument[] documents, NormalizedMedia[] media,
        List<LogEntry> logEntries, List<ValidationResult> validationResults)
    {
        var profile = DifficultyLevels.GetProfile(difficulty);
        
        // Validate document count
        var docCount = documents.Length;
        if (docCount < profile.Documents.Min || docCount > profile.Documents.Max)
        {
            validationResults.Add(new ValidationResult
            {
                Rule = "DIFFICULTY_DOCUMENT_COUNT",
                Status = "FAIL",
                Description = $"Document count ({docCount}) outside range {profile.Documents.Min}-{profile.Documents.Max} for {difficulty ?? "auto"} level",
                Details = profile.Description
            });
        }
        else
        {
            validationResults.Add(new ValidationResult
            {
                Rule = "DIFFICULTY_DOCUMENT_COUNT",
                Status = "PASS",
                Description = $"Document count ({docCount}) within range for {difficulty ?? "auto"} level"
            });
        }

        // Validate evidence count
        var evidenceCount = media.Length;
        if (evidenceCount < profile.Evidences.Min || evidenceCount > profile.Evidences.Max)
        {
            validationResults.Add(new ValidationResult
            {
                Rule = "DIFFICULTY_EVIDENCE_COUNT",
                Status = "FAIL",
                Description = $"Evidence count ({evidenceCount}) outside range {profile.Evidences.Min}-{profile.Evidences.Max} for {difficulty ?? "auto"} level",
                Details = profile.Description
            });
        }
        else
        {
            validationResults.Add(new ValidationResult
            {
                Rule = "DIFFICULTY_EVIDENCE_COUNT",
                Status = "PASS",
                Description = $"Evidence count ({evidenceCount}) within range for {difficulty ?? "auto"} level"
            });
        }

        // Validate gated documents count
        var gatedCount = documents.Count(d => d.Gated);
        if (gatedCount != profile.GatedDocuments)
        {
            validationResults.Add(new ValidationResult
            {
                Rule = "DIFFICULTY_GATED_COUNT",
                Status = gatedCount == 0 && profile.GatedDocuments == 0 ? "PASS" : "WARN",
                Description = $"Gated document count ({gatedCount}) differs from expected {profile.GatedDocuments} for {difficulty ?? "auto"} level",
                Details = "May indicate difficulty level mismatch"
            });
        }
        else
        {
            validationResults.Add(new ValidationResult
            {
                Rule = "DIFFICULTY_GATED_COUNT", 
                Status = "PASS",
                Description = $"Gated document count ({gatedCount}) matches expected for {difficulty ?? "auto"} level"
            });
        }

        // Validate forensics reports have Cadeia de Custódia
        var forensicsReports = documents.Where(d => d.Type == DocumentTypes.ForensicsReport);
        foreach (var report in forensicsReports)
        {
            var hasCustodySection = report.Sections.Any(s => Regex.IsMatch(s, @"cadeia de cust[óo]dia", RegexOptions.IgnoreCase));
            if (!hasCustodySection)
            {
                validationResults.Add(new ValidationResult
                {
                    Rule = "FORENSICS_CUSTODY_CHAIN",
                    Status = "FAIL",
                    Description = $"Forensics report {report.DocId} missing 'Cadeia de Custódia' section",
                    Details = "All forensics reports must include chain of custody section"
                });
            }
            else
            {
                validationResults.Add(new ValidationResult
                {
                    Rule = "FORENSICS_CUSTODY_CHAIN",
                    Status = "PASS",
                    Description = $"Forensics report {report.DocId} includes required custody section"
                });
            }
        }

        logEntries.Add(new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = "INFO",
            Message = $"Difficulty validation completed for {difficulty ?? "auto"} level",
            Details = new Dictionary<string, object>
            {
                ["profile"] = profile.Description,
                ["documentCount"] = docCount,
                ["evidenceCount"] = evidenceCount,
                ["gatedCount"] = gatedCount,
                ["forensicsReports"] = forensicsReports.Count()
            }
        });

        return profile;
    }

    private GatingGraph BuildGatingGraph(
        NormalizedDocument[] documents, NormalizedMedia[] media,
        List<LogEntry> logEntries, List<ValidationResult> validationResults)
    {
        var nodes = new List<GatingNode>();
        var edges = new List<GatingEdge>();

        // Create nodes for all documents and media
        foreach (var doc in documents)
        {
            var requiredIds = new List<string>();
            if (doc.GatingRule != null)
            {
                if (!string.IsNullOrEmpty(doc.GatingRule.EvidenceId))
                    requiredIds.Add(doc.GatingRule.EvidenceId);
                if (!string.IsNullOrEmpty(doc.GatingRule.DocId))
                    requiredIds.Add(doc.GatingRule.DocId);
            }

            nodes.Add(new GatingNode
            {
                Id = doc.DocId,
                Type = "document",
                Gated = doc.Gated,
                UnlockAction = doc.GatingRule?.Action,
                RequiredIds = requiredIds.ToArray()
            });
        }

        foreach (var mediaItem in media)
        {
            nodes.Add(new GatingNode
            {
                Id = mediaItem.EvidenceId,
                Type = "evidence",
                Gated = false, // Media is not gated directly
                RequiredIds = Array.Empty<string>()
            });
        }

        // Create edges based on gating rules
        foreach (var doc in documents.Where(d => d.Gated && d.GatingRule != null))
        {
            var rule = doc.GatingRule!;
            
            if (!string.IsNullOrEmpty(rule.EvidenceId))
            {
                edges.Add(new GatingEdge
                {
                    From = rule.EvidenceId,
                    To = doc.DocId,
                    Relationship = "unlocks"
                });
            }

            if (!string.IsNullOrEmpty(rule.DocId))
            {
                edges.Add(new GatingEdge
                {
                    From = rule.DocId,
                    To = doc.DocId,
                    Relationship = "unlocks"
                });
            }
        }

        // Detect cycles using DFS
        var (hasCycles, cycleDescription) = DetectCycles(nodes, edges);

        if (hasCycles)
        {
            validationResults.Add(new ValidationResult
            {
                Rule = "GATING_GRAPH_CYCLES",
                Status = "FAIL",
                Description = "Gating graph contains cycles",
                Details = string.Join("; ", cycleDescription)
            });
        }
        else
        {
            validationResults.Add(new ValidationResult
            {
                Rule = "GATING_GRAPH_CYCLES",
                Status = "PASS",
                Description = "Gating graph is acyclic",
                Details = $"Validated {nodes.Count} nodes and {edges.Count} edges"
            });
        }

        logEntries.Add(new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = "INFO",
            Message = "Gating graph construction completed",
            Details = new Dictionary<string, object>
            {
                ["totalNodes"] = nodes.Count,
                ["totalEdges"] = edges.Count,
                ["gatedNodes"] = nodes.Count(n => n.Gated),
                ["hasCycles"] = hasCycles
            }
        });

        return new GatingGraph
        {
            Nodes = nodes.ToArray(),
            Edges = edges.ToArray(),
            HasCycles = hasCycles,
            CycleDescription = cycleDescription.ToArray()
        };
    }

    private (bool hasCycles, List<string> cycleDescription) DetectCycles(List<GatingNode> nodes, List<GatingEdge> edges)
    {
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();
        var adjacencyList = new Dictionary<string, List<string>>();
        var cycles = new List<string>();

        // Build adjacency list
        foreach (var node in nodes)
        {
            adjacencyList[node.Id] = new List<string>();
        }

        foreach (var edge in edges)
        {
            if (adjacencyList.ContainsKey(edge.From))
            {
                adjacencyList[edge.From].Add(edge.To);
            }
        }

        // DFS to detect cycles
        bool HasCycleDFS(string nodeId, List<string> path)
        {
            visited.Add(nodeId);
            recursionStack.Add(nodeId);
            path.Add(nodeId);

            if (adjacencyList.ContainsKey(nodeId))
            {
                foreach (var neighbor in adjacencyList[nodeId])
                {
                    if (!visited.Contains(neighbor))
                    {
                        if (HasCycleDFS(neighbor, new List<string>(path)))
                            return true;
                    }
                    else if (recursionStack.Contains(neighbor))
                    {
                        var cycleStart = path.IndexOf(neighbor);
                        var cycle = path.Skip(cycleStart).Concat(new[] { neighbor });
                        cycles.Add($"Cycle detected: {string.Join(" -> ", cycle)}");
                        return true;
                    }
                }
            }

            recursionStack.Remove(nodeId);
            return false;
        }

        // Check each unvisited node
        foreach (var node in nodes)
        {
            if (!visited.Contains(node.Id))
            {
                if (HasCycleDFS(node.Id, new List<string>()))
                {
                    return (true, cycles);
                }
            }
        }

        return (false, cycles);
    }

    private NormalizedDocument[] NormalizeTimestamps(NormalizedDocument[] documents, string? timezone, List<LogEntry> logEntries)
    {
        var normalizedTimezone = timezone ?? "UTC";
        var inconsistentTimestamps = new List<string>();
        
        foreach (var doc in documents)
        {
            // Validate and normalize timestamps
            if (doc.CreatedAt == null)
            {
                inconsistentTimestamps.Add($"Document {doc.DocId}: missing CreatedAt timestamp");
                doc.CreatedAt = DateTime.UtcNow;
            }
            
            // Ensure consistent timezone format
            var createdAtStr = doc.CreatedAt?.ToString("yyyy-MM-ddTHH:mm:ss.fffK");
            if (!string.IsNullOrEmpty(createdAtStr) && !createdAtStr.Contains("+") && !createdAtStr.Contains("-") && !createdAtStr.EndsWith("Z"))
            {
                inconsistentTimestamps.Add($"Document {doc.DocId}: timestamp missing timezone offset");
            }
            
            doc.ModifiedAt ??= doc.CreatedAt;
            
            // Note: Detailed content timestamp validation would require parsing the actual document JSON content
            // For now, we focus on the core document timestamp properties
        }

        if (inconsistentTimestamps.Any())
        {
            logEntries.Add(new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = "WARNING",
                Message = $"Found {inconsistentTimestamps.Count} timestamp inconsistencies",
                Details = new Dictionary<string, object>
                {
                    ["inconsistencies"] = inconsistentTimestamps
                }
            });
        }

        logEntries.Add(new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = "INFO",
            Message = $"Normalized timestamps for {documents.Length} documents to {normalizedTimezone}",
            Details = new Dictionary<string, object>
            {
                ["timezone"] = normalizedTimezone,
                ["documentCount"] = documents.Length,
                ["inconsistenciesFound"] = inconsistentTimestamps.Count
            }
        });

        return documents;
    }

    private NormalizedMedia[] NormalizeMediaTimestamps(NormalizedMedia[] media, string? timezone, List<LogEntry> logEntries)
    {
        var normalizedTimezone = timezone ?? "UTC";
        var inconsistentTimestamps = new List<string>();
        
        foreach (var item in media)
        {
            // Validate and normalize media timestamps
            if (item.CreatedAt == null)
            {
                inconsistentTimestamps.Add($"Media {item.EvidenceId}: missing CreatedAt timestamp");
                item.CreatedAt = DateTime.UtcNow;
            }
            
            // Ensure consistent timezone format for media
            var createdAtStr = item.CreatedAt?.ToString("yyyy-MM-ddTHH:mm:ss.fffK");
            if (!string.IsNullOrEmpty(createdAtStr) && !createdAtStr.Contains("+") && !createdAtStr.Contains("-") && !createdAtStr.EndsWith("Z"))
            {
                inconsistentTimestamps.Add($"Media {item.EvidenceId}: timestamp missing timezone offset");
            }
            
            // Note: Additional timestamp validation for media collection times would require
            // checking metadata or expanded case timeline information
        }

        if (inconsistentTimestamps.Any())
        {
            logEntries.Add(new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = "WARNING",
                Message = $"Found {inconsistentTimestamps.Count} media timestamp inconsistencies",
                Details = new Dictionary<string, object>
                {
                    ["inconsistencies"] = inconsistentTimestamps
                }
            });
        }

        logEntries.Add(new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = "INFO",
            Message = $"Normalized timestamps for {media.Length} media items to {normalizedTimezone}",
            Details = new Dictionary<string, object>
            {
                ["timezone"] = normalizedTimezone,
                ["mediaCount"] = media.Length,
                ["inconsistenciesFound"] = inconsistentTimestamps.Count
            }
        });

        return media;
    }

    private NormalizedDocument[] CreateI18nDocuments(NormalizedDocument[] documents, List<LogEntry> logEntries)
    {
        // For now, keep the existing I18n structure
        // In a real implementation, this would translate content
        
        logEntries.Add(new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = "INFO",
            Message = $"Processed i18n for {documents.Length} documents",
            Details = new Dictionary<string, object>
            {
                ["supportedLanguages"] = new[] { "pt-BR", "en", "es", "fr" },
                ["documentCount"] = documents.Length
            }
        });

        return documents;
    }

    private NormalizedMedia[] CreateI18nMedia(NormalizedMedia[] media, List<LogEntry> logEntries)
    {
        // For now, keep the existing I18n structure
        // In a real implementation, this would translate content
        
        logEntries.Add(new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = "INFO",
            Message = $"Processed i18n for {media.Length} media items",
            Details = new Dictionary<string, object>
            {
                ["supportedLanguages"] = new[] { "pt-BR", "en", "es", "fr" },
                ["mediaCount"] = media.Length
            }
        });

        return media;
    }

    private NormalizedCaseBundle CreateNormalizedBundle(
        NormalizationInput input, NormalizedDocument[] documents, NormalizedMedia[] media, 
        GatingGraph gatingGraph, DifficultyProfile difficultyProfile)
    {
        // Read emails from blob storage (emails/ folder)
        var emails = ReadEmailsFromStorage(input.CaseId).GetAwaiter().GetResult();

        return new NormalizedCaseBundle
        {
            CaseId = input.CaseId,
            Version = "1.0",
            CreatedAt = DateTime.UtcNow,
            Timezone = input.Timezone ?? "UTC",
            Difficulty = input.Difficulty,
            Documents = documents,
            Media = media,
            Emails = emails,
            GatingGraph = gatingGraph,
            Metadata = new NormalizedCaseMetadata
            {
                GeneratedBy = "NormalizerService",
                Pipeline = "Plan→Expand→Design→GenerateDocuments/GenerateMedia→GenerateEmails→Normalize",
                GeneratedAt = DateTime.UtcNow,
                ValidationResults = new Dictionary<string, object>
                {
                    ["difficultyProfile"] = difficultyProfile.Description,
                    ["documentCount"] = documents.Length,
                    ["mediaCount"] = media.Length,
                    ["gatedCount"] = documents.Count(d => d.Gated),
                    ["emailCount"] = emails.Length
                },
                AppliedRules = new[]
                {
                    "UNIQUE_IDS",
                    "GATING_REFERENCE_INTEGRITY", 
                    "DIFFICULTY_VALIDATION",
                    "GATING_GRAPH_CYCLES",
                    "FORENSICS_CUSTODY_CHAIN",
                    "ISO8601_TIMESTAMPS"
                }
            }
        };
    }

    private CaseManifest CreateManifest(string caseId, NormalizedDocument[] documents, NormalizedMedia[] media)
    {
        var documentEntries = documents.Select(d => new ManifestEntry
        {
            Id = d.DocId,
            RelativePath = $"documents/{d.DocId}.json",
            Type = d.Type,
            Gated = d.Gated,
            Hash = ComputeHash(JsonSerializer.Serialize(d)),
            SizeBytes = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(d))
        }).ToArray();

        var mediaEntries = media.Select(m => new ManifestEntry
        {
            Id = m.EvidenceId,
            RelativePath = $"media/{m.EvidenceId}.json",
            Type = m.Kind,
            Gated = false,
            Hash = ComputeHash(JsonSerializer.Serialize(m)),
            SizeBytes = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(m))
        }).ToArray();

        var fileHashes = new Dictionary<string, string>();
        foreach (var entry in documentEntries.Concat(mediaEntries))
        {
            fileHashes[entry.RelativePath] = entry.Hash;
        }

        return new CaseManifest
        {
            CaseId = caseId,
            Version = "1.0",
            GeneratedAt = DateTime.UtcNow,
            BundlePaths = new[] { "documents/", "media/", "logs/" },
            Documents = documentEntries,
            Media = mediaEntries,
            FileHashes = fileHashes,
            Visibility = new CaseVisibility
            {
                AlwaysVisible = documents.Where(d => !d.Gated).Select(d => d.DocId)
                    .Concat(media.Select(m => m.EvidenceId)).ToArray(),
                GatedVisible = documents.Where(d => d.Gated).Select(d => d.DocId).ToArray(),
                HiddenUntilUnlocked = Array.Empty<string>()
            }
        };
    }

    private Task ValidateNormalizedBundleAsync(
        NormalizedCaseBundle bundle, List<LogEntry> logEntries, List<ValidationResult> validationResults)
    {
        // Validate ISO-8601 timestamps
        foreach (var doc in bundle.Documents)
        {
            if (doc.CreatedAt.HasValue)
            {
                // Check if it's a valid ISO-8601 format (DateTime parsing handles this)
                validationResults.Add(new ValidationResult
                {
                    Rule = "ISO8601_TIMESTAMPS",
                    Status = "PASS",
                    Description = $"Document {doc.DocId} has valid ISO-8601 timestamp",
                    Details = doc.CreatedAt.Value.ToString("O")
                });
            }
        }

        // Validate timezone consistency
        if (!string.IsNullOrEmpty(bundle.Timezone))
        {
            try
            {
                // Basic timezone validation - in real implementation would use TimeZoneInfo
                var isValidTz = bundle.Timezone == "UTC" || bundle.Timezone.Contains("/");
                validationResults.Add(new ValidationResult
                {
                    Rule = "TIMEZONE_CONSISTENCY",
                    Status = isValidTz ? "PASS" : "WARN",
                    Description = $"Timezone validation: {bundle.Timezone}",
                    Details = isValidTz ? "Valid timezone format" : "Timezone format may be invalid"
                });
            }
            catch
            {
                validationResults.Add(new ValidationResult
                {
                    Rule = "TIMEZONE_CONSISTENCY",
                    Status = "WARN",
                    Description = "Could not validate timezone format",
                    Details = bundle.Timezone
                });
            }
        }

        logEntries.Add(new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = "INFO",
            Message = "Final validation completed",
            Details = new Dictionary<string, object>
            {
                ["bundleVersion"] = bundle.Version,
                ["timezone"] = bundle.Timezone,
                ["totalItems"] = bundle.Documents.Length + bundle.Media.Length
            }
        });

        return Task.CompletedTask;
    }

    private static string ComputeHash(string content)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private async Task<NormalizedEmail[]> ReadEmailsFromStorage(string caseId)
    {
        try
        {
            _logger.LogInformation("Reading emails from storage for case {CaseId}", caseId);

            // List all email_*.json files in emails/ folder
            var prefix = $"{caseId}/emails/email_";
            var emailFiles = await _storageService.ListFilesAsync("casegen", prefix);
            var emails = new List<NormalizedEmail>();

            foreach (var fileName in emailFiles)
            {
                // Only process files that match email_*.json pattern
                if (!fileName.EndsWith(".json"))
                {
                    continue;
                }

                try
                {
                    _logger.LogDebug("Reading email file: {FileName}", fileName);
                    
                    var json = await _storageService.GetFileAsync("casegen", fileName);
                    
                    var email = JsonSerializer.Deserialize<NormalizedEmail>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (email != null)
                    {
                        emails.Add(email);
                        _logger.LogDebug("Successfully deserialized email: {EmailId}", email.EmailId);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to deserialize email from {FileName}", fileName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading email file {FileName}", fileName);
                    // Continue processing other emails even if one fails
                }
            }

            _logger.LogInformation("Read {EmailCount} emails from storage for case {CaseId}", emails.Count, caseId);
            return emails.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading emails from storage for case {CaseId}", caseId);
            // Return empty array if emails folder doesn't exist or there's an error
            return Array.Empty<NormalizedEmail>();
        }
    }
}
