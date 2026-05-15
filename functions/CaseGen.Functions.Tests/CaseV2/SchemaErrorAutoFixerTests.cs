using System.Text.Json.Nodes;
using CaseGen.Functions.Services.CaseV2;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaseGen.Functions.Tests.CaseV2;

/// <summary>
/// Unit tests for <see cref="SchemaErrorAutoFixer"/>. The fixer's job is to rewrite
/// LLM ID-format slips deterministically so the v2 generation pipeline doesn't have
/// to spend a 30-90 s LLM round-trip on a 1-character bug.
/// </summary>
public class SchemaErrorAutoFixerTests
{
    private readonly SchemaErrorAutoFixer _fixer = new(NullLogger<SchemaErrorAutoFixer>.Instance);

    /// <summary>
    /// The fixture from the TASK C smoke that triggered TASK E in the first place:
    /// LLM emitted <c>asset_eli_phone_location_calllog_export</c> instead of
    /// <c>asset.eli_phone_location_calllog_export</c>, and the wrong id propagated
    /// to <c>rules[].trigger.assetId</c>, <c>forensicOutcomes[].inputAssetId</c>, and
    /// <c>solution.requiredEvidenceIds</c>. After Fix(), the document must be a clean
    /// 0-error case and every cross-reference must be updated consistently.
    /// </summary>
    [Fact]
    public void Fix_TaskC_FiveErrorFixture_ResolvesAllReferences()
    {
        var json = JsonNode.Parse("""
        {
          "version": "2.0",
          "caseId": "case_smoke_taskc",
          "assets": [
            { "id": "asset.briefing_doc", "type": "document" },
            { "id": "asset_eli_phone_location_calllog_export", "type": "document" }
          ],
          "temporalEvents": [
            { "id": "tevt.entry_window", "label": "x" },
            { "id": "tevt_eli_alibi_gap_ping", "label": "y" }
          ],
          "rules": [
            { "ruleId": "rule.r1", "trigger": { "type": "view", "assetId": "asset_eli_phone_location_calllog_export" } }
          ],
          "forensicOutcomes": [
            { "id": "frx.f1", "inputAssetId": "asset_eli_phone_location_calllog_export" }
          ],
          "solution": {
            "culprit": "suspect.x",
            "requiredEvidenceIds": ["asset.briefing_doc", "asset_eli_phone_location_calllog_export"]
          }
        }
        """)!;

        var fixes = _fixer.Fix(json);

        // 5 substitutions expected, matching the original smoke's 5 schema errors
        Assert.Equal(5, fixes.Count);

        Assert.Equal("asset.eli_phone_location_calllog_export", (string?)json["assets"]![1]!["id"]);
        Assert.Equal("tevt.eli_alibi_gap_ping",                 (string?)json["temporalEvents"]![1]!["id"]);
        Assert.Equal("asset.eli_phone_location_calllog_export", (string?)json["rules"]![0]!["trigger"]!["assetId"]);
        Assert.Equal("asset.eli_phone_location_calllog_export", (string?)json["forensicOutcomes"]![0]!["inputAssetId"]);
        Assert.Equal("asset.eli_phone_location_calllog_export", (string?)json["solution"]!["requiredEvidenceIds"]![1]);
    }

    [Fact]
    public void Fix_AlreadyCorrectValues_AreNotTouched()
    {
        var json = JsonNode.Parse("""
        {
          "assets": [{ "id": "asset.briefing_doc", "type": "document" }],
          "rules":  [{ "ruleId": "rule.r1", "trigger": { "assetId": "asset.briefing_doc" } }]
        }
        """)!;

        var fixes = _fixer.Fix(json);

        Assert.Empty(fixes);
        Assert.Equal("asset.briefing_doc", (string?)json["assets"]![0]!["id"]);
        Assert.Equal("asset.briefing_doc", (string?)json["rules"]![0]!["trigger"]!["assetId"]);
    }

    /// <summary>
    /// If a value already carries a DIFFERENT known prefix (e.g. <c>suspect.foo</c>
    /// where the property expects <c>asset.x</c>), we must not second-guess it.
    /// The right answer is unclear; the schema validator will surface it as an
    /// error and a refine task can pick it up.
    /// </summary>
    [Fact]
    public void Fix_AmbiguousWrongPrefix_IsNotTouched()
    {
        var json = JsonNode.Parse("""
        {
          "rules": [{ "ruleId": "rule.r1", "trigger": { "assetId": "suspect.foo" } }]
        }
        """)!;

        var fixes = _fixer.Fix(json);

        Assert.Empty(fixes);
        Assert.Equal("suspect.foo", (string?)json["rules"]![0]!["trigger"]!["assetId"]);
    }

    [Fact]
    public void Fix_HyphenSeparator_IsAlsoNormalised()
    {
        var json = JsonNode.Parse("""
        {
          "rules": [{ "ruleId": "rule-r1", "trigger": { "assetId": "asset-briefing-doc" } }]
        }
        """)!;

        var fixes = _fixer.Fix(json);

        Assert.Equal(2, fixes.Count);
        Assert.Equal("rule.r1",            (string?)json["rules"]![0]!["ruleId"]);
        Assert.Equal("asset.briefing_doc", (string?)json["rules"]![0]!["trigger"]!["assetId"]);
    }

    /// <summary>
    /// Bare slug (no prefix) gets the expected one prepended. This is the LLM
    /// occasionally writing just <c>briefing_doc</c> in a rule trigger.
    /// </summary>
    [Fact]
    public void Fix_BareSlug_GainsExpectedPrefix()
    {
        var json = JsonNode.Parse("""
        {
          "rules": [{ "ruleId": "rule.r1", "trigger": { "assetId": "briefing_doc" } }]
        }
        """)!;

        var fixes = _fixer.Fix(json);

        Assert.Single(fixes);
        Assert.Equal("asset.briefing_doc", (string?)json["rules"]![0]!["trigger"]!["assetId"]);
    }

    /// <summary>
    /// Values that don't look like slugs (free-text, spaces, mixed case) are left
    /// alone — we can't safely guess a prefix for them.
    /// </summary>
    [Fact]
    public void Fix_FreeTextValue_IsNotTouched()
    {
        var json = JsonNode.Parse("""
        {
          "rules": [{ "ruleId": "rule.r1", "trigger": { "assetId": "Some Random Text" } }]
        }
        """)!;

        var fixes = _fixer.Fix(json);

        Assert.Empty(fixes);
    }

    [Fact]
    public void Fix_QuestionOptionsIds_AreNormalised()
    {
        var json = JsonNode.Parse("""
        {
          "questions": [
            {
              "id": "q.who_did_it",
              "options": [
                { "id": "opt_a", "label": "A" },
                { "id": "opt.b", "label": "B" },
                { "id": "opt-c", "label": "C" }
              ],
              "correctOptionId": "opt_b"
            }
          ]
        }
        """)!;

        var fixes = _fixer.Fix(json);

        Assert.Contains(fixes, f => f.Contains("opt.a"));
        Assert.Contains(fixes, f => f.Contains("opt.c"));
        Assert.Contains(fixes, f => f.Contains("opt.b"));
        Assert.Equal("opt.a", (string?)json["questions"]![0]!["options"]![0]!["id"]);
        Assert.Equal("opt.b", (string?)json["questions"]![0]!["options"]![1]!["id"]);
        Assert.Equal("opt.c", (string?)json["questions"]![0]!["options"]![2]!["id"]);
        Assert.Equal("opt.b", (string?)json["questions"]![0]!["correctOptionId"]);
    }
}
