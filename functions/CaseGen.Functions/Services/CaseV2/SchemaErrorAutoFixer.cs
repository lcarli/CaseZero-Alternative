using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace CaseGen.Functions.Services.CaseV2;

/// <summary>
/// Deterministic post-generation fixer that rewrites the most common LLM ID-format slips
/// without going back to the model. Today the only known recurring slip is a missing dot
/// between the prefix and the slug (LLM emits <c>asset_eli_phone_location</c> instead of
/// the schema-required <c>asset.eli_phone_location</c>). Handling it here avoids spending
/// a 30-90s refine round-trip on a 1-character bug.
///
/// The fixer is intentionally narrow: it only touches values whose ENCLOSING property name
/// implies one of the schema's known ID-prefix patterns. It never invents IDs or moves data.
/// </summary>
public interface ISchemaErrorAutoFixer
{
    /// <summary>
    /// Walks <paramref name="root"/> in-place and rewrites recognised ID-prefix typos.
    /// Returns a list of human-readable descriptions of each substitution made.
    /// </summary>
    IReadOnlyList<string> Fix(JsonNode root);
}

public class SchemaErrorAutoFixer : ISchemaErrorAutoFixer
{
    // Schema known prefixes (must mirror schemas/case.schema.json patterns ^prefix\.[a-z0-9_]+$).
    // Order doesn't matter — we apply by exact prefix match.
    private static readonly string[] Prefixes = { "asset", "email", "opt", "q", "rule", "suspect", "tevt" };

    // Properties whose values are KNOWN to be ID-typed. Maps property name → expected prefix.
    // When a value carries a wrong-prefix typo (e.g. `asset_xxx`), we rewrite it to `prefix.xxx`.
    // Strings outside this map are left alone.
    private static readonly Dictionary<string, string> IdPropertyToPrefix = new(StringComparer.Ordinal)
    {
        // Asset references
        { "assetId",                 "asset" },
        { "inputAssetId",            "asset" },
        // Suspect references
        { "suspectId",               "suspect" },
        { "matchedSuspectId",        "suspect" },
        { "culprit",                 "suspect" },
        // Rule references
        { "ruleId",                  "rule" },
        // Email references
        { "emailId",                 "email" },
        // Temporal-event references
        { "temporalEventId",         "tevt" },
        // Option references
        { "correctOptionId",         "opt" }
    };

    // Object schemas where the `id` field has a known prefix. We detect the prefix from a
    // sibling field rather than from the path (e.g. inside `assets[]` the id must be
    // `asset.xxx`, inside `temporalEvents[]` it must be `tevt.xxx`).
    private static readonly Dictionary<string, string> ParentArrayToIdPrefix = new(StringComparer.Ordinal)
    {
        { "assets",         "asset" },
        { "resultAssets",   "asset" },
        { "suspects",       "suspect" },
        { "rules",          "rule" },
        { "emails",         "email" },
        { "initialEmails",  "email" },
        { "resultEmails",   "email" },
        { "temporalEvents", "tevt" },
        { "questions",      "q" }
    };

    private readonly ILogger<SchemaErrorAutoFixer> _logger;

    public SchemaErrorAutoFixer(ILogger<SchemaErrorAutoFixer> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<string> Fix(JsonNode root)
    {
        var fixes = new List<string>();
        if (root is null) return fixes;
        Walk(root, parentArray: null, propertyName: null, fixes);
        // requiredEvidenceIds is an array of bare strings — handle separately since the
        // "items" don't carry a name.
        FixSolutionIdArrays(root, fixes);
        // Question options (q.id pattern), nested inside questions[].options[]
        FixQuestionOptionIds(root, fixes);
        if (fixes.Count > 0)
            _logger.LogInformation("SchemaErrorAutoFixer applied {Count} fix(es): {Fixes}", fixes.Count, string.Join("; ", fixes));
        return fixes;
    }

    private static void Walk(JsonNode node, string? parentArray, string? propertyName, List<string> fixes)
    {
        switch (node)
        {
            case JsonObject obj:
                // First, try to rewrite an `id` inside a known parent array.
                if (parentArray is not null && ParentArrayToIdPrefix.TryGetValue(parentArray, out var expectedIdPrefix))
                {
                    TryRewrite(obj, "id", expectedIdPrefix, fixes);
                }
                // Then rewrite any known ID-typed properties at this level.
                foreach (var (propName, prefix) in IdPropertyToPrefix)
                {
                    TryRewrite(obj, propName, prefix, fixes);
                }
                // Recurse, remembering this object's enclosing array (if any) is no longer
                // its descendants' parent — we pass parentArray=null to children and reset
                // when we see a new array below.
                foreach (var kv in obj)
                {
                    if (kv.Value is null) continue;
                    Walk(kv.Value, parentArray: null, propertyName: kv.Key, fixes);
                }
                break;

            case JsonArray arr:
                // The propertyName captured above describes what this array is. Pass it
                // down so element objects know which `id` prefix to expect.
                foreach (var item in arr)
                {
                    if (item is null) continue;
                    Walk(item, parentArray: propertyName, propertyName: null, fixes);
                }
                break;
        }
    }

    private static void TryRewrite(JsonObject obj, string propName, string expectedPrefix, List<string> fixes)
    {
        if (!obj.TryGetPropertyValue(propName, out var node) || node is null) return;
        if (node is not JsonValue val) return;
        if (!val.TryGetValue<string>(out var current) || string.IsNullOrEmpty(current)) return;

        // Already correct.
        if (current.StartsWith(expectedPrefix + ".", StringComparison.Ordinal)) return;

        // Try common typos first: underscore-prefix (`asset_xxx`), hyphen-prefix (`asset-xxx`),
        // or bare slug with no prefix at all.
        var fixedValue = TryNormalisePrefix(current, expectedPrefix);
        if (fixedValue is null || fixedValue == current) return;

        obj[propName] = fixedValue;
        fixes.Add($"{propName}: '{current}' → '{fixedValue}'");
    }

    private static readonly Regex SlugRe = new("^[a-z0-9_]+$", RegexOptions.Compiled);

    private static string? TryNormalisePrefix(string value, string expectedPrefix)
    {
        // Case A: prefix is wrong (some OTHER known prefix). Don't touch — we don't know
        // which one is right.
        foreach (var p in Prefixes)
        {
            if (p == expectedPrefix) continue;
            if (value.StartsWith(p + ".", StringComparison.Ordinal))
            {
                // Property says it should be `expectedPrefix.xxx` but the value already has
                // a different valid prefix → don't second-guess.
                return null;
            }
        }

        // Case B: missing dot — `asset_xxx` → `asset.xxx`. The leading token before the
        // first underscore must match expectedPrefix and the rest must be slug-shaped.
        var us = value.IndexOf('_');
        if (us > 0)
        {
            var head = value[..us];
            var tail = value[(us + 1)..];
            if (head == expectedPrefix && SlugRe.IsMatch(tail))
                return $"{expectedPrefix}.{tail}";
        }

        // Case C: hyphen used as separator — `asset-xxx`. Rare but cheap to handle.
        var hy = value.IndexOf('-');
        if (hy > 0)
        {
            var head = value[..hy];
            var tail = value[(hy + 1)..].Replace('-', '_');
            if (head == expectedPrefix && SlugRe.IsMatch(tail))
                return $"{expectedPrefix}.{tail}";
        }

        // Case D: bare slug, no prefix at all — `eli_phone_location` where it should be
        // `asset.eli_phone_location`. We only do this when value is shaped like a slug.
        if (SlugRe.IsMatch(value))
            return $"{expectedPrefix}.{value}";

        return null;
    }

    /// <summary>
    /// `solution.requiredEvidenceIds` is an array of bare strings that must look like
    /// `asset.xxx`. Same for `requiredAnalysisIds` (also asset-typed).
    /// </summary>
    private static void FixSolutionIdArrays(JsonNode root, List<string> fixes)
    {
        var sol = root["solution"] as JsonObject;
        if (sol is null) return;
        FixStringArray(sol, "requiredEvidenceIds", "asset", fixes);
        FixStringArray(sol, "requiredAnalysisIds", "asset", fixes);
    }

    private static void FixStringArray(JsonObject parent, string propName, string expectedPrefix, List<string> fixes)
    {
        if (!parent.TryGetPropertyValue(propName, out var node) || node is not JsonArray arr) return;
        for (int i = 0; i < arr.Count; i++)
        {
            if (arr[i] is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrEmpty(s))
            {
                var fixedV = TryNormalisePrefix(s, expectedPrefix);
                if (fixedV is not null && fixedV != s)
                {
                    arr[i] = fixedV;
                    fixes.Add($"{propName}[{i}]: '{s}' → '{fixedV}'");
                }
            }
        }
    }

    /// <summary>
    /// `questions[].options[].id` must match `^opt\.[a-z0-9_]+$`. Same for the question's
    /// `id` (`q.xxx`) — already covered by parentArray=questions, but options is one
    /// level deeper so the generic walker doesn't see it.
    /// </summary>
    private static void FixQuestionOptionIds(JsonNode root, List<string> fixes)
    {
        if (root["questions"] is not JsonArray questions) return;
        foreach (var q in questions)
        {
            if (q is not JsonObject qo) continue;
            if (qo["options"] is not JsonArray opts) continue;
            foreach (var opt in opts)
            {
                if (opt is JsonObject optObj)
                    TryRewrite(optObj, "id", "opt", fixes);
            }
        }
    }
}
