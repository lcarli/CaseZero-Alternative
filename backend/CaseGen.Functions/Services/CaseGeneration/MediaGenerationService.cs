using CaseGen.Functions.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CaseGen.Functions.Services.CaseGeneration;

/// <summary>
/// Service responsible for generating media content from specifications (Phase 5).
/// Handles media generation for evidence items (photos, scans, diagrams).
/// </summary>
public class MediaGenerationService
{
    private readonly ILLMService _llmService;
    private readonly IStorageService _storageService;
    private readonly ICaseLoggingService _caseLogging;
    private readonly IImagesService _imagesService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MediaGenerationService> _logger;

    public MediaGenerationService(
        ILLMService llmService,
        IStorageService storageService,
        ICaseLoggingService caseLogging,
        IImagesService imagesService,
        IConfiguration configuration,
        ILogger<MediaGenerationService> logger)
    {
        _llmService = llmService;
        _storageService = storageService;
        _caseLogging = caseLogging;
        _imagesService = imagesService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Generates media specification and content from MediaSpec.
    /// </summary>
    public async Task<string> GenerateMediaFromSpecAsync(
        MediaSpec spec,
        string designJson,
        string caseId,
        string? planJson = null,
        string? expandJson = null,
        string? difficultyOverride = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Gen Media[{EvidenceId}] kind={Kind} title={Title}", spec.EvidenceId, spec.Kind, spec.Title);

        var systemPrompt = @"
            You are a generator of FORENSIC specifications for a single static media asset.
            Output: ONLY valid JSON with { evidenceId, kind, title, prompt, constraints }.

            SCOPE & SAFETY
            - One image only (a collage is acceptable if composed into a single bitmap); never a multi-image sequence.
            - Style: documentary photograph/screenshot, neutral, non-artistic.
            - Content 100% fictitious. Forbid real names, brands/logos, faces/biometrics, real license plates, official badges.
            - No graphic/violent content. Avoid people when the scene allows.
            - Do NOT write the evidence name or caseId inside the image.

            TEMPORAL CONSISTENCY (MANDATORY):
            - ALL timestamps in media MUST use the same timezone offset established in Plan/Design
            - CCTV timestamps MUST match the collectedAt time specified in Design for this evidence
            - Document scan dates MUST be consistent with when the document would realistically exist in the case timeline
            - NO conflicting timestamps - each piece of evidence has exactly one collection time that must be respected

            STRICT CONSISTENCY (NON-NEGOTIABLE)
            - Reflect ONLY the narrative elements already present in Plan/Expand/Design; do not invent new entities, locations, vehicles, or timestamps.
            - If the evidence exists in Expand/Design with specific attributes (time, camera label, object condition, location), MIRROR those details.
            - Use only existing labels already implied by the case (e.g., 'CAM-03', zone ids, door ids). No new camera IDs or zones.
            - Honor any provided initial_constraints (unless they violate safety), mapping them into the final constraints.

            TECHNICAL STANDARDIZATION
            - Always quantify: angle in degrees, camera height OR subject distance in meters, lens in mm, aperture f/, shutter 1/x s, ISO, white balance (K).
            - Lighting: diffuse/even; avoid harsh shadows; prefer color-neutral rendering.
            - For object-on-surface evidence: prefer 90° top-down when applicable.

            MANDATORY FIELD FORMAT
            - prompt: fixed sections in this exact order:
            1) Function (1 sentence)
            2) Scene / Composition (3–5 sentences; include subject percentage in frame)
            3) Angle & Distance (strict numeric values)
            4) Optics & Technique (lens mm, f/, 1/x s, ISO, WB K, focus, DOF)
            5) Mandatory elements (labels/markers/timestamps already present in the case)
            6) Negatives (objective list of what MUST NOT appear)
            7) Acceptance checklist (objective, verifiable bullets)
            - constraints: JSON object containing:
            - angle_deg (number)
            - camera_height_m OR distance_m (number; include exactly one of these keys)
            - aspect_ratio (string, e.g., ""16:9"", ""4:3"", ""A4"")
            - resolution_px (string, e.g., ""1920x1080"")
            - seed (7-digit integer as string)
            - deferred=false

            TYPE-SPECIFIC RULES
            - kind=cftv_frame:
            - Overlay timestamp ""YYYY-MM-DD HH:MM:SS"" (top-right, monospaced, white with 1–2px outline).
            - Camera label (e.g., ""CAM-03"") on frame.
            - Camera height 2.5–3.0 m; lens 2.8–4 mm equiv.; shutter 1/60 s; mild H.264 artifacts; slight coherent motion blur.
            - kind=document_scan / receipt:
            - Perspective corrected; 300–600 DPI equivalent; visible margins; no fingers, no moiré; crop clean edges.
            - kind=scene_topdown:
            - 90° orthographic top-down; keep edges parallel to frame; include simple scale reference ONLY if already implied by case (no rulers).

            DO NOT
            - Do not repeat the rulebook in prose; translate rules into concrete parameters.
            - Do not return Markdown, comments, or extra fields.

            PRE-SUBMISSION SELF-CHECK (must be satisfied before answering)
            - Are all referenced labels/timestamps present in the supplied case context?
            - Are prompt sections exactly in the required order (1–7) and fully populated?
            - Does constraints include angle_deg and exactly one of camera_height_m or distance_m, plus aspect_ratio, resolution_px, seed (7 digits), deferred=false?
            - Is this a single image (no sequence)?";

        var userPrompt = $@"
            CONTEXT (only the minimal, relevant parts — do NOT restate everything)
            {designJson ?? "{}"}

            EVIDENCE SPECIFICATION
            - evidenceId: {spec.EvidenceId}
            - kind: {spec.Kind}
            - title: {spec.Title}
            - difficulty: {(difficultyOverride ?? "Detective")}
            - initial_constraints: {(spec.Constraints != null && spec.Constraints.Any() ? string.Join(", ", spec.Constraints.Select(kv => $"{kv.Key}: {kv.Value}")) : "n/a")}
            - language: en-US

            LEVEL OF DETAIL BY DIFFICULTY (apply WITHOUT creating a sequence):
            - Rookie: simple composition; prefer top-down when applicable.
            - Detective/Detective2: add one contextual detail within the same frame.
            - Sergeant+: wider context PLUS a relevant local detail in the same frame (no multi-image montage); subtle false-positive ONLY if consistent with the case.
            - Captain/Commander: include plausible control objects (e.g., neutral color/gray card) only if consistent with operational reality for that scene.

            HARD REQUIREMENTS FOR THIS OUTPUT
            - Return ONLY valid JSON with: evidenceId, kind, title, prompt, constraints.
            - The prompt MUST follow the 7 fixed sections in the exact order and include measurable values.
            - constraints MUST include: angle_deg, camera_height_m OR distance_m (exactly one), aspect_ratio, resolution_px, seed (7 digits), deferred=false.
            - If kind=cftv_frame: put timestamp overlay ""YYYY-MM-DD HH:MM:SS"" (top-right) and a camera label like ""CAM-03"".
            - Respect initial_constraints when provided (unless unsafe/contradictory).
            - Do NOT invent new entities, labels, brands, or times.
            ";

        var json = await _llmService.GenerateAsync(caseId, systemPrompt, userPrompt, cancellationToken);

        try
        {
            await _caseLogging.LogStepResponseAsync(caseId, $"media/{spec.EvidenceId}", json, cancellationToken);

            var bundlesContainer = _configuration["CaseGeneratorStorage:BundlesContainer"] ?? "bundles";
            var docBundlePath = $"{caseId}/media/{spec.EvidenceId}.json";
            await _storageService.SaveFileAsync(bundlesContainer, docBundlePath, json, cancellationToken);

            _logger.LogInformation("BUNDLE: Saved media to bundle: {Path} (case={CaseId}, kind={Kind})",
                docBundlePath, caseId, spec.Kind);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist generated media {EvidenceId} for case {CaseId}",
                spec.EvidenceId, caseId);
        }

        return json;
    }

    /// <summary>
    /// Renders media from JSON specification using ImagesService.
    /// </summary>
    public async Task<string> RenderMediaFromJsonAsync(MediaSpec spec, string caseId, CancellationToken cancellationToken = default)
    {
        return await _imagesService.GenerateAsync(caseId, spec);
    }
}
