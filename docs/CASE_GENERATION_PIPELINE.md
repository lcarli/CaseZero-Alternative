# Case Generation Pipeline

The CaseZero case generator is an Azure Functions app (`CaseGen.Functions`, .NET 9
isolated worker) that produces detective case bundles on demand. It is
driven by a Durable Functions orchestration and a fine-grained **micro-task**
LLM pipeline. Each case is a `case.json` v2 document plus a folder of rendered
assets (PDFs and images) with an integrity manifest. Approved bundles can be published to the same blob
container the website reads from. The current reviewer and diagnostic solver
do not yet certify full narrative coherence or runtime playability.

## Entry points

- **`POST /api/cases/v2/generate`** — kicks off generation, returns `202 Accepted + jobId`.
  Body fields (all optional; `{}` or an empty body uses the documented defaults):
  ```json
  {
    "caseId": "case_002",
    "title": "The River Drop",
    "theme": "Body found at a dockyard under the morning fog",
    "location": "Seattle, Washington",
    "difficulty": "Detective",
    "requiredRank": "Detective",
    "language": "en-US",
    "seed": 42,
    "writeToDisk": true
  }
  ```
- **`GET /api/cases/v2/jobs/{jobId}`** — poll status. Composes Durable runtime
  status with a per-phase blob status published by `JobPhaseReporter`. While
  running, returns `{ status: "running", currentPhase: "<stage>", ... }`. On
  completion returns the result summary, including quality issues, actual
  persistence/publication flags, validation counts, render counts, and refine
  telemetry. A Durable `Completed` job with logical blockers is reported as
  `status: "failed"` with its result and error preserved.

A best-effort singleton check rejects concurrent runs with `409 Conflict` and
the `jobId` of the in-flight job.

Malformed JSON, duplicate/unknown properties, invalid types, unsupported ranks
or languages, blank explicit hints, and invalid case IDs return **400** with
`error: "invalid_request"` and an `errors` array. Bodies above **16 KiB** return
**413**, `error: "request_too_large"`. Neither path schedules a Durable job or
calls a model. Cancellation propagates instead of becoming a default request.
Valid property names remain case-insensitive, but enum values are exact.

Ranks are `Rookie`, `Detective`, `Detective2`, `Sergeant`, `Lieutenant`, `Captain`,
and `Commander`. Languages are `pt-BR`, `en-US`, `es-ES`, and `fr-FR`.
`difficulty` defaults to `Detective`, `requiredRank` to the difficulty,
`language` to `en-US`, and `writeToDisk` to `true`. `seed` is an optional
32-bit integer and is still metadata, not a reproducibility guarantee.
Explicit `caseId`, `title`, `theme`, and `location` are limited to 128, 200,
2000, and 300 characters respectively. A case ID may contain letters, digits,
underscores, or hyphens; existing lowercase/prefix/hyphen normalization is
preserved. The generation UI omits empty optional hints rather than sending
an invalid explicit value.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│  POST /api/cases/v2/generate                                    │
│        ↓                                                         │
│  StartCaseV2GenerationFunction                                   │
│        ↓ schedule                                                │
│  CaseV2GenerationOrchestrator (durable, instanceId = jobId)     │
│        ↓                                                         │
│  CaseV2GenerateActivity                                          │
│        ↓                                                         │
│  CaseV2GeneratorService.GenerateAsync(request, reporter)         │
│        ↓ micro-tasks → quality gate → isolated rendering          │
│  verified manifest → immutable assets → conditional JSON commit  │
└─────────────────────────────────────────────────────────────────┘
```

The website (`CaseZeroApi` → `CaseV2StorageService`) reads from the same
`bundles` container in production. A successfully published bundle becomes
visible when the dashboard cache refreshes. A preview or local-only run does
not claim production publication.

## The phases

`CaseV2GeneratorService` runs these stages in order. The phase name shown is
what `JobPhaseReporter` publishes during execution so the GET status endpoint
can show progress. Per-stage latencies are reported on completion.

| # | Phase | Role |
|---|-------|------|
| 1 | `plotOutline` | LLM establishes setting, victim, incident, suspect identities and private culprit choice |
| 2 | `caseBible` | LLM plans canonical entities/facts/claims, author knowledge, source inventory, time windows, discovery, forensic methods, sufficient proof and supported answers; deterministic validation blocks invalid plans |
| 3 | `suspectCards` | Parallel scoped authors write recorded profiles/statements; opening alibi verification follows independent supported evidence |
| 4 | `assetsAndTimelineAndBriefing` | Parallel scoped asset/briefing authors; chronology is compiled from the Bible, preserving uncertainty and attribution |
| 5 | `outcomesAndInitialEmails` | Parallel planned forensic conclusions/reports/emails and separately scoped initial email authors; no new source IDs or scientific matches |
| 6 | `mechanicalRules` | **Deterministic** compilation of canonical hidden-source discovery, including non-forensic/nested conditions |
| 7 | `rulesAndSolutionSkeleton` | Historical phase ID: the solution is already planned; LLM may add neutral notifications, not new discovery or verification |
| 8 | `questionsAndExplanation` | Parallel supported questions and post-mortem explanation, with internal content excerpts |
| 9 | `consistency` | **Deterministic** checks of authored support, canonical bindings, IDs, time precision, discovery and playable reachability |
| 10 | Compilation *(no separate progress phase)* | Compile unchanged public `case.json` v2, excluding private Bible/support fields; validate public schema and canonical contracts |
| 11 | `redTeamAndSolver` | Parallel full-body/Bible adversarial comparison and diagnostic-only player-visible solver |
| 12 | `refineCase` *(conditional)* | One typed repair of supported prose/components, never of the locked facts/source graph |
| 13 | `revalidateRepair` *(candidate only)* | Recheck support, preparation, consistency/compilation/contracts, then obtain fresh review/diagnostic solver |
| 14 | `qualityGate` | Schema/consistency errors, blocking or unavailable review, revision mismatch, and unsuccessful repair block output |
| 15 | `renderAssets` *(approved, non-preview only)* | Render reviewed documents without factual enrichment; report actual filenames/MIME and reject unsupported media |
| 16 | `verifyBundle` *(non-preview only)* | Check bytes, size, MIME, SHA-256 and exact coverage; seal the manifest, materialize actual URIs, and revalidate public JSON |
| 17 | `preparePublication` *(non-preview only)* | Acquire a local case-ID lock, copy only the new immutable bundle, and verify the destination before any remote commit |
| 18 | `publishToBlob` *(configured, non-preview only)* | Create immutable assets/manifest, verify remote bytes, then conditionally commit `case.json`; conflicts/failures block the job |
| 19 | `persistCase` *(non-preview only)* | Reverify the bundle and unchanged local JSON, then atomically replace only `case.json`; retain all prior assets |

Conditional stages with no work are skipped. `assetPlan`, `forensicsPlan` and
`autoFixSchema` remain recognized historical UI phases, not current generation
calls. Independent replanning and JSON-only auto-fixing are no longer used.
The UI uses the recorded stage timings rather than marking all terminal stages
as completed. Stages without records are labeled accordingly.

## Private CaseBible and supported authorship

The Bible is planned and validated **before** parallel prose expansion. It
records the private timed criminal act, canonical location/victim/entities,
positive facts, attributed claims and their intentional disagreements. Every
source declares its author, permitted facts/claims, issue time, stable identity,
visibility, attachments and exact discovery condition. Authors receive only
their source's facts/claims and relevant identity metadata, not the private
culprit choice, crime fact, claim truth classifications or internal source role.

Plans require 4-10 investigation assets, one profile per suspect and an opening
briefing; result reports/emails are bound to their producing forensic pair.
The unchanged v2 schema requires at least one available analysis method, but
neither a positive lab result nor a required analysis is mandatory. Complementary
documentary proof can suffice without a forensic suspect match. Individualizing
identification must concern the matched entity; similarity or no findings cannot
be decisive proof or independent alibi verification.

Every authored source supplies internal `support: [{referenceId, excerpt}]`.
The exact references must cover its approved facts/claims, and each excerpt
must occur in actual delivered document sections or email/profile text, not
merely a listing description. Questions and explanation similarly bind their
supported answers/assertions. Verified alibis need independently authored,
readable exclusion evidence, not a generated image prompt or a suspect's own
assertion. Proof/question/alibi/explanation sources must be reachable in the
planned **and** authored graph. Canonical requirements are not silently dropped.

Anchoring is **not semantic entailment**: a present quotation can still be
misleading. The blocking red-team compares full material, excerpts and canonical
facts, author knowledge, time precision and technical limitations, including
unsupported decisive prose. This remains an LLM-based review, not a scientific
or full runtime-playability guarantee. Image prompts cannot independently
support decisive proof, question answers or verified alibis.

Chronology is deterministic, retains estimated/interval bounds in the narrative
and labels claims as attributed/unverified. Source creation cannot precede its
asserted observation; opening material must already exist. Rookie/all-initial
plans also require result sources to exist by case opening. Source identity,
sender, timestamps, attachments, findings, sufficient proof and answer text
remain bound throughout expansion and repair. Bible-bound documents bypass
templates that could invent observations; legacy unbound assets retain their
existing one-time template behavior.

The Bible and support fields are internal draft/review contracts. They are
never added to public `case.json`, asset manifests or player APIs; its version
and strict schema remain unchanged.

## Microtask contracts and bounded calls

`MicrotaskSchemas` defines closed, typed schemas for every generation task.
`TaskRunner` checks JSON syntax, duplicate properties, the full local schema,
and task-specific invariants **before** deserializing/returning a draft update.
Raw responses are capped at 256 KiB. Missing fields and unsupported enums do
not become C# defaults; model output is not extracted from prose, converted
to `{}`, or silently repaired by the provider.

`bodyDoc.sections` is discriminated: prose/callouts need text, tables need
columns and rows, key/value sections need items, transcripts need entries,
and code sections need content. Arrays and text are bounded, and every table
row must have the same number of cells as its columns. Initial PDF/document/
digital cards require `bodyDoc`; forensic reports may still use validated
nonblank markdown for renderer compatibility.
New plans support `photo`, `pdf`, `document`, and `digital`. Speech evidence is
an explicitly authored document transcript (`InterviewTranscript`), not an
advertised recording. Audio/video/unsupported types never emit placeholders;
the public v2 schema and readers still support existing legacy media.

Suspect/asset cards and questions must echo their upstream identity. IDs are
rejected rather than renamed without reconciling references. Culprit flags,
forensic pairs and supported asset types, result assets/emails, reachable
solution evidence, required analyses, rule references (including nested
conditions), option IDs, and the selected answer are checked before updating
the draft. Invalid choices are never replaced with the first option; a
missing culprit match is never converted into fabricated forensic evidence.
This is contract enforcement, not full narrative or scientific review.

Critical microtasks and the red-team get at most **one retry** for a malformed
contract or a transient provider failure (408, 429, 5xx/network/timeout).
Contract retries include the specific error and preserve the supplied facts/
IDs; permanent provider errors and cancellation do not retry. The diagnostic
solver and the single refine pass have **no retries**. SDK retries are disabled
so the task budget is not multiplied by hidden transport attempts. Parallel
tasks are all awaited before leaving a phase.

Set `AzureFoundry:UseStructuredOutputs=true` (or the environment variable
`AzureFoundry__UseStructuredOutputs=true`) only for a deployment supporting
native JSON Schema output. The default is `false`. Native mode sets the SDK's
`ResponseFormat` with `strict: true`, using a separate projection that removes
unsupported native constraints and requires all fields. The original local
schema, including those constraints, remains mandatory. Open schemas or
schemas exceeding the compatible 100-property/five-object-level limits use
the prompt path with an explicit warning; a case-sized repair schema may exceed
these limits. An API rejection does not trigger an automatic weaker-mode
fallback. Empty, refused, filtered, and token-truncated completions fail
explicitly. No paid generation is needed for the synthetic regression suite.

The public schema has a single source: `schemas/case.schema.json`. MSBuild
links it into build **and publish** output as `Schemas/case.v2.schema.json`;
do not maintain a Functions copy or overwrite source files in startup scripts.

Local validation uses **JsonSchema.Net 7.4.0**, whose NuGet package has the MIT
license, instead of the hourly-limited `Newtonsoft.Json.Schema`. `StructuredJson`
shares the mechanism across microtasks, canonical repairs, and public-case
validation: Draft 2020-12, mandatory format assertions, strict JSON parsing,
duplicate rejection, and bounded diagnostics with JSON Pointer locations.
The shared date-time format also checks actual calendar validity and explicit
timezone syntax; the package's default regex alone accepts impossible dates.
Historical timeline entries after case opening are deterministic blockers
(preserving the existing one-minute tolerance); later gameplay events belong
in `temporalEvents`, not the historical timeline.
Regular `Newtonsoft.Json` remains only for schema construction/projection.
The synthetic suite exercises more than 1,000 validations and concurrent reuse
without weakening enforcement or splitting execution to bypass a quota.

The version pin is deliberate: newer JsonSchema.Net binary packages can carry
additional maintenance-fee terms. Review package **and transitive** license
terms before upgrading; do not assume the source MIT license is the only term
governing a published binary. See the
[adopted NuGet package](https://www.nuget.org/packages/JsonSchema.Net/7.4.0).

## Quality gate and output modes

Playable progression is checked deterministically before approval. The fixed
point starts from visible assets, initial emails (including the briefing) and
suspects, then follows compatible forensic results, temporal payloads, email
attachments and recursively evaluated rules. The generator and runtime share
the `AND`/`OR` condition evaluator. A required item with only a circular reveal
path is a blocker; validation no longer promotes hidden required evidence to
`initial`. `Rookie`/`all_initial` visibility remains an intentional beginner
mode, also honored by backend visibility and sanitization.

The runtime uses a server-owned, persisted 60x game clock for analysis durations
and temporal events. Pauses preserve deadlines and accumulated events; completed
analyses reveal their actual case documents/emails. The localized forensic UI
uses visible v2 assets, case-configured analyses and backend requests, without
simulated evidence/results. These operational regressions do not turn the
diagnostic solver into a blind gameplay acceptance gate; that remains separate
work.

The strict policy blocks `needs_review` and `reject`, any medium/high review
finding (even alongside `verdict: "ok"`), and invalid/unavailable review.
Only `ok` with no blocking findings can proceed. Low findings remain visible
diagnostics. Reviewer JSON is validated locally before deserialization; omitted
fields, invalid types/enums, duplicate properties, and fabricated success flags
do not turn into approval.

`result.Quality` is an additive job-result contract, not a change to `case.json`
v2. It contains:

| Field | Meaning |
|-------|---------|
| `Approved` | This execution passed the applicable quality/output checks |
| `Revision` / `ReviewedRevision` | Canonical content revision / application-stamped revision analyzed by the final reviewer |
| `Preview` | `writeToDisk: false`: review the draft without rendering or storing files |
| `PersistenceRequested` / `Persisted` | Whether local persistence was requested / actually completed |
| `PublicationRequested` / `Published` | Whether storage publication was configured and requested / actually completed |
| `SolverMode` | Always `diagnostic`; the solver score is not a publication prerequisite |
| `Issues` | Stable `Code`, `Severity`, `Area`, optional `Reference`, original `Message`, and `Blocking` |

Before artifact verification, files exist only in
`<CasesBasePath>/.casegen-staging/<unique-workspace>/<caseId>/`.
Local discovery checks direct child directories for `case.json`, so it cannot
list these workspaces. Rejection and render failure leave an existing case
unchanged and do not call the publisher. Workspaces are cleaned after use;
local preparation copies only the new immutable bundle, never moves the prior
case directory. Until the final JSON replacement, readers see the previous
case or no case at all. An uncommitted local copy is cleaned only if current
`case.json` does not reference it; prior files and hidden lock files are retained.

The additive `result.Bundle` summary reports `BundleId`, `Revision`, `Verified`,
`AssetsVerified`, `TotalBytes` (assets + manifest + JSON), and
`ManifestChecksum`. Previews and historical results without a bundle report
do not imply that file verification ran. Invalid/unverified or wrong-revision
reports cannot override job failure with a legacy success flag.

`writeToDisk: true` with an unconfigured publisher is an explicit local-only
success. A configured publisher returning zero or failing is not a local-only
success. Storage publication runs before local promotion, so a subsequent local
failure can honestly report `Published: true`, `Persisted: false`, and a failed
job. There is still no cross-storage transaction; each store has its own JSON
commit point. Remote commit uncertainty is a failure, not an invented success,
and cannot trigger deletion of potentially referenced artifacts.

The GET endpoint checks the completed activity result instead of equating
Durable `Completed` with success. It preserves the original result and issue
references when returning `failed`. Missing/malformed completed results fail
explicitly. Historical results without `Quality` retain their legacy
validation/error outcome; the UI marks approval/publication flags unavailable
rather than inferring them from counters.

The generation UI is localized in `pt-BR`, `en-US`, `es-ES`, and `fr-FR`,
including forms, phases, modes, issue labels, and severity. Original model and
provider diagnostics remain available under the localized technical-details
label; generated content is not silently translated.

## Artifact manifest and safe publication

Every approved file-output run has a random immutable bundle ID, independent
of the canonical content revision. A new generation can start at revision 0
without colliding with a previous bundle. Final evidence URIs are
`case://<caseId>/assets/<bundleId>/<actualFilename>`; unrendered previews use
provisional extensionless references and never claim media exists.

`assets/<bundleId>/manifest.json` is an internal file contract:
`{version, caseId, bundleId, revision, files:[{assetId, relativePath, mimeType,
sizeBytes, sha256}]}`. Exactly one rendered file must cover each canonical/
public asset. Duplicate IDs/names, extra files/directories, unsafe filenames,
symlinks, missing/empty files, unsupported MIME/type combinations, extension
mismatches, and changed bytes/metadata block output. Checks examine supported
format signatures and basic terminators, not full PDF/image decoding or visual
content. JPEG output produces a JPEG URI, never an assumed PNG URI.

Public `assets[].checksum` and `metadata.mimeType/sizeBytes` come from the
verified bytes. `gameMetadata.generation.bundleChecksum` is `sha256:<hash of
the exact manifest bytes>`; `case.json` has a separately verified SHA-256,
avoiding a circular manifest/JSON checksum. The manifest contains file
descriptors, not authored bodies, hidden solution answers, or a new player API.

Blob publication uses create-only `If-None-Match: *` for all immutable files,
SDK transfer checksums, and remote metadata/length/MIME checks followed by a
streamed SHA-256 read-back. Existing names are reused only if their bytes and
bundle/revision metadata match. `case.json` is uploaded last with the ETag from
the bundle's first publication attempt (`If-Match`, or `If-None-Match: *` for a
new case). That snapshot stays with the in-memory bundle during retries;
a stale retry cannot roll back a newer commit. A lost successful commit
response can be reconciled by verifying the same bundle's committed JSON.
`BlobsPublished` counts published bundle objects, including verified objects
reused on retry; it is not a count of new upload requests.

Local writers hold an exclusive `.casegen-locks/<caseId>.lock`, verify the new
copied bundle and original JSON checksum, and rename only the staged JSON over
the final path on the same filesystem. Old bundles/legacy assets remain valid
for cached readers. A conflict produces `publication_conflict` rather than
overwriting another writer. Other stable artifact codes are `bundle_invalid`,
`asset_integrity_failed`, and `asset_file_missing`.

There is no automatic retention/garbage collection of older or uncommitted
remote bundles yet. On failed/uncertain publication, the owned prefix is
logged and retained rather than risking deletion of a committed bundle.
Do not delete historical assets merely because the newest JSON no longer
references them. Persistent checkpoint recovery, full rendered-content
inspection, and runtime playability remain separate deliveries.

## Canonical repair (one bounded pass)

`CaseDraft` is the single source for compilation, review, and rendering.
Legacy templates prepare unbound documents before review, once per replaced
document; Bible-bound documents preserve already authored canonical content.
Red-team sees complete structured bodies, emails, temporal events, nested
rules, questions, and explanation alongside the compiled `case.json`.
Source authors receive restricted canonical contexts. The reviewer sees the
private Bible and full material; the solver reads player-visible bodies but
remains diagnostic.

Structural errors, `needs_review`/`reject`, or medium/high findings can trigger
one repair after a **successful** review. Invalid/unavailable review blocks
the execution without attempting to rewrite content. The repair contract is
closed and case-specific:
`{baseRevision, changes:[{target, reason, value}]}`. `value` replaces one entire
existing component. Duplicate/unknown targets, stale revisions, no-op repairs,
invalid document sections/tables, and broken references are rejected.

The model must preserve the **locked CaseBible**, canonical location/victim/time
windows, source plans/discovery, proof sets, answers, identities, request
constraints, forensic pairs/findings/matches/result references and scoring.
`caseBible` is excluded from editable targets. Prose repairs must retain
canonical reference IDs and update their support/explanation excerpts to the
new actual text; underlying truth cannot be rewritten to fit prose. Changing
prose about suspects, narrative metadata, or timeline requires reconciling
the dependent bodies, emails, conclusions, briefing, and solution in the same
response. Changes in a forensic group require its outcome, report, email, and
solution; attached emails/briefing and selected evidence have explicit
dependencies too. Merely declaring a dependency is insufficient: its complete
replacement is required, even when reconciliation leaves its facts unchanged.

Repairs apply to a deep copy. The candidate repeats preparation, consistency,
canonical contracts, and public-schema compilation. A structurally valid
candidate receives a **fresh** red-team review and diagnostic solver attempt.
Acceptance requires no blocking issues, not a smaller schema-error count;
semantic **0 -> 0** repair can therefore succeed. Review revision mismatch is
blocking. Only acceptance switches the canonical draft, JSON, and reviewer
together. Rejected candidates leave the original content and existing files
unchanged, never render/publish, and do not start another repair pass.

The additive `Repair` job report contains `Attempted`, `Accepted`,
`BaseRevision`, `CandidateRevision`, actual `ChangedComponents`, original
blockers, and explicit errors (`repair_invalid`, `repair_unavailable`,
`repair_rejected`). `RefineAttempted`, before/after validation counts, and
`RedTeamVerdict` remain compatible. `AutoFixesApplied` now reports draft
consistency fixes, never independent changes to compiled JSON.

## Local development

The function reads from `local.settings.json` for the Azure OpenAI / Foundry
keys and the storage connection string. Azurite stands in for blob in local
dev. Configured but unavailable Azurite now fails publication explicitly.
`CaseGenV2:CasesBasePath` must be writable; an explicit configured directory is
created if necessary rather than silently falling back to another destination.
On read-only Function App deployments, set it to a writable location such as
`/tmp/casegen-cases` (local files there are ephemeral; blob publication is the
production persistence mechanism).
See [`RUNNING_FUNCTIONS_LOCALLY.md`](RUNNING_FUNCTIONS_LOCALLY.md) for the
one-command bring-up and a worked example.

## Code map

- `Functions/CaseV2/CaseV2GenerationOrchestrator.cs` — Durable orchestrator,
  starter HTTP function, GET status function.
- `Services/CaseV2/CaseV2GeneratorService.cs` — the multi-phase orchestrator
  proper (the function above just bridges Durable).
- `Services/CaseV2/CaseQualityGate.cs` — strict approval policy and rendering error checks.
- `Services/CaseV2/CaseBundle.cs`, `CaseFileIntegrity.cs` — sealed manifests, actual public URIs/checksums, byte/coverage verification.
- `Services/CaseV2/LocalCasePublication.cs` — case-ID locks, immutable local preparation, atomic JSON-only commits.
- `Services/CaseV2/CaseDraft.cs`, `CaseDraftRepair.cs` — canonical snapshots, typed component repairs, identity/reference/dependency guards.
- `Services/CaseV2/TaskRunner.cs`, `MicrotaskSchemas.cs`, `MicrotaskValidation.cs`,
  `StructuredJson.cs` — validated task contracts, invariants, and bounded retries.
- `Services/CaseV2/GenerationRequestValidator.cs` — HTTP/direct-call input validation.
- `Services/StructuredOutputSchema.cs`, `AzureFoundryLLMProvider.cs` — optional
  native response-format projection and explicit model-response failures.
- `Models/CaseV2/CaseV2GenerationModels.cs` — typed quality issues and output flags.
- `Services/CaseV2/Tasks/` — focused LLM tasks (`PlotOutlineTask`,
  `SuspectCardTask`, `AssetPlanTask`, `RedTeamTask`, `SolverTask`,
  `RefineCaseTask`, etc).
- `Services/CaseV2/MechanicalRulesBuilder.cs` — deterministic rule emission.
- `Services/CaseV2/ConsistencyValidator.cs` — deterministic cross-ref check.
- `Services/CaseV2/SchemaErrorAutoFixer.cs` — standalone legacy ID-format
  normaliser. Unit-tested in `CaseGen.Functions.Tests`.
- `Services/CaseV2/AssetRenderingService.cs` — PDF/image materialisation.
- `Services/CaseV2/CaseV2BlobPublisher.cs` — immutable uploads, remote integrity, idempotent conditional JSON publication.
- `Services/CaseV2/JobPhaseReporter.cs` — per-phase status JSON in blob, read
  by the GET status endpoint.
