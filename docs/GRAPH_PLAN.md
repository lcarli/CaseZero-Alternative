# CaseGraph Reliability Plan

## Purpose

This document tracks the redesign of the private case-generation model so
Detective through Commander cases can be generated with reproducible logical
quality.

The central change is to replace prose-only canonical truth with a typed
`CaseGraph`. LLM agents will remain responsible for creativity and document
writing, but deterministic code will decide whether:

- the culprit is uniquely derivable;
- the evidence is independent and player-reachable;
- every decoy can be fairly evaluated;
- forensic work is scientifically compatible with its input;
- chronology is coherent;
- documents expose only the facts they are allowed to expose.

This is an internal architecture change. The public `case.json` v2 contract must
remain compatible with the game.

## Permanent constraints

- [ ] Preserve the public `case.json` v2 contract.
- [ ] Keep `CaseGen.Functions` on .NET SDK 9.
- [ ] Keep `CaseGen.Functions.Tests` on .NET SDK 9.
- [ ] Localize all player-visible generated text and deterministic labels for
      `pt-BR`, `en-US`, `es-ES`, and `fr-FR`.
- [ ] Rookie must continue to contain no forensic workflow and must expose all
      solving evidence initially.
- [ ] A case must never be persisted or published when any blocking quality gate
      fails.

## Current baseline

### Completed

- [x] Private investigation blueprint and CaseBible.
- [x] Typed clue ownership through `EvidenceGraph`.
- [x] Difficulty-aware evidence portfolios.
- [x] Explicit forensic clue ownership.
- [x] Explicit forensic input asset ownership.
- [x] Deterministic public timeline compilation.
- [x] Red-herring verification separated from private resolution.
- [x] Owner-stage repair coordinator.
- [x] Best-snapshot recovery and repair plateau detection.
- [x] Deterministic graph-witness solver and advisory specialist reviews.
- [x] Validated Rookie reference case.

### Known limitations

- [ ] Canonical entities and relationships are still represented primarily as
      prose.
- [ ] Validators generally prove that text is present, not that the evidence
      logically entails a conclusion.
- [ ] The solver reads titles and descriptions instead of the complete rendered
      documents.
- [ ] The solver receives reveal information too broadly and does not simulate
      player actions sequentially.
- [ ] One broad red-team agent evaluates too many unrelated quality dimensions.
- [ ] Some deterministic fallbacks hide semantic generation failures instead of
      rejecting them.
- [ ] Repairs operate at stage granularity and can regenerate unrelated valid
      content.
- [ ] Higher difficulty is mostly represented by larger counts instead of deeper
      reasoning topology.
- [ ] No Detective case has yet received a final red-team verdict of `ok`.

## Target architecture

```text
Creative premise
      |
      v
Typed CaseGraph proposal
      |
      v
Deterministic graph validation
      |
      v
Evidence and forensic delivery plan
      |
      v
Deterministic reachability and proof validation
      |
      v
Localized document generation
      |
      v
Document-to-fact fidelity validation
      |
      v
Interactive player simulation
      |
      v
Specialized narrative reviews
      |
      v
case.json compiler
```

The `CaseGraph` becomes the only canonical source of truth. Documents, timeline,
forensic results, questions, solution, and public `case.json` are projections of
that graph.

---

# Phase 1 - CaseGraph kernel

**Goal:** represent the case as atomic entities, facts, events, observations, and
proof derivations before generating prose.

## GRAPH-001 - Define entity contracts

**Status:** [x] Complete

Create a private `CaseEntity` model.

Suggested fields:

```csharp
public sealed class CaseEntity
{
    public string Id { get; set; }
    public EntityKind Kind { get; set; }
    public string DisplayName { get; set; }
    public Dictionary<string, string> Attributes { get; set; }
}
```

Initial entity kinds:

- Person
- Organization
- Location
- Device
- Account
- Credential
- Session
- Document
- PhysicalObject
- Vehicle
- Communication
- Transaction

**Acceptance criteria**

- [x] IDs use stable typed prefixes.
- [x] Every suspect references a Person entity.
- [x] Accounts, badges, devices, sessions, and files are first-class entities.
- [x] Entity attributes cannot silently redefine another canonical fact.
- [x] Serialization and clone/snapshot tests exist.

## GRAPH-002 - Define atomic fact contracts

**Status:** [x] Complete

Create `CanonicalFact`.

Suggested fields:

```csharp
public sealed class CanonicalFact
{
    public string Id { get; set; }
    public string Predicate { get; set; }
    public string SubjectId { get; set; }
    public string? ObjectId { get; set; }
    public string? LiteralValue { get; set; }
    public string? EventId { get; set; }
    public FactVisibility Visibility { get; set; }
    public FactTruthStatus TruthStatus { get; set; }
}
```

Example facts:

```text
device.nb_4471 assignedTo suspect.felipe
session.s_91af uploaded document.receipt_18473
session.s_91af originatedFrom device.nb_4471
transaction.exp_447 paidTo account.fm_8821
```

**Acceptance criteria**

- [x] A fact has exactly one canonical value.
- [x] Subject and object references must exist.
- [x] Literal facts specify value type and unit where applicable.
- [x] Private facts cannot be assigned directly to initial evidence.
- [x] Conflicting facts are deterministically detected.

## GRAPH-003 - Define canonical event contracts

**Status:** [x] Complete

Replace ambiguous timestamp prose with typed events.

Required event types should initially include:

- Created
- Modified
- Scanned
- Uploaded
- Submitted
- Approved
- Paid
- Accessed
- Entered
- Exited
- Called
- Messaged
- Discovered
- Reported
- Collected
- AnalysisRequested
- AnalysisCompleted

Suggested fields:

```csharp
public sealed class CanonicalEvent
{
    public string Id { get; set; }
    public EventKind Kind { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public TemporalPrecision Precision { get; set; }
    public List<string> ParticipantIds { get; set; }
    public List<string> FactIds { get; set; }
    public FactVisibility Visibility { get; set; }
}
```

**Acceptance criteria**

- [x] File creation, modification, upload, submission, and payment are distinct
      events.
- [x] All repeated timestamps reference the same event ID.
- [x] Precision distinguishes exact timestamps from estimated windows.
- [x] The player timeline is compiled only from public events.
- [x] Impossible ordering and travel windows are rejected.

## GRAPH-004 - Define observations and provenance

**Status:** [x] Complete

Create `EvidenceObservation` to represent exactly what an evidence asset states.

Suggested fields:

```csharp
public sealed class EvidenceObservation
{
    public string Id { get; set; }
    public string FactId { get; set; }
    public string SourceAssetId { get; set; }
    public ObservationFidelity Fidelity { get; set; }
    public ObservationReliability Reliability { get; set; }
    public FactVisibility Visibility { get; set; }
}
```

**Acceptance criteria**

- [x] Every player-visible factual statement maps to an observation.
- [x] Observations distinguish direct records, testimony, hearsay, and
      circumstantial evidence.
- [x] Every observation identifies one owning asset.
- [x] Documents may not claim facts outside their assigned observations.
- [x] Duplicate observations do not count as independent evidence.

## GRAPH-005 - Define claims and derivations

**Status:** [x] Complete

Create typed inference rules.

Suggested model:

```csharp
public sealed class ProofDerivation
{
    public string Id { get; set; }
    public List<string> PremiseIds { get; set; }
    public DerivationRule Rule { get; set; }
    public string ConclusionFactId { get; set; }
    public string? SupportsSuspectId { get; set; }
}
```

Initial derivation rules:

- IdentityMatch
- AccountOwnership
- DeviceAssignment
- SessionOrigin
- AccessOpportunity
- ActionAttribution
- BenefitAttribution
- TimelineCorrelation
- AlibiSupport
- AlibiContradiction
- Exclusion
- CrossSourceCorroboration

**Acceptance criteria**

- [x] Every culprit conclusion has explicit premises.
- [x] Every premise is a fact, observation, or prior derivation.
- [x] Circular derivations are rejected.
- [x] Private truth cannot be used by the player proof engine.
- [x] Derivation depth is measurable.

## GRAPH-006 - Integrate CaseGraph into CaseDraft

**Status:** [x] Complete

Add the graph to the private draft without changing public assembly.

**Acceptance criteria**

- [x] `CaseDraft` snapshots include the complete graph.
- [x] Existing Rookie generation can temporarily project its clue ladder into the
      graph.
- [x] `case.json` output remains schema-compatible.
- [x] The old clue ladder is marked transitional and cannot become a second
      source of truth.

**Dependencies:** GRAPH-001 through GRAPH-005.

---

# Phase 2 - Deterministic proof and fairness engine

**Goal:** prove solvability, uniqueness, evidence independence, and decoy
resolution without relying on an LLM verdict.

## PROOF-001 - Implement graph well-formedness validator

**Status:** [x] Complete

Validate:

- orphan entity, event, fact, observation, and derivation IDs;
- duplicate canonical values;
- invalid visibility transitions;
- circular derivations;
- observations assigned to missing assets;
- private facts materialized by initial assets.

**Acceptance criteria**

- [x] All failures contain node IDs and owning stages.
- [x] Failures can be converted into targeted repair operations.
- [x] Unit tests cover every invalid graph shape.

## PROOF-002 - Implement derivation closure

**Status:** [x] Complete

Calculate all conclusions obtainable from a supplied set of observations.

**Acceptance criteria**

- [x] Results are deterministic.
- [x] The engine returns the derivation path for every conclusion.
- [x] The engine can run against full knowledge or player-reachable knowledge.
- [x] Cycles do not cause non-termination.

## PROOF-003 - Implement culprit uniqueness gate

**Status:** [x] Complete

The gate must verify:

- the culprit can be derived;
- no other suspect can be derived as culprit;
- the conclusion does not depend on private facts;
- the required proof depth matches the difficulty profile.

**Acceptance criteria**

- [x] A case with two logically valid culprits is rejected.
- [x] A case solvable only from private truth is rejected.
- [x] A case where motive alone identifies the culprit is rejected.

## PROOF-004 - Implement independent proof-path gate

**Status:** [x] Complete

Calculate edge-disjoint or observation-disjoint proof paths.

**Acceptance criteria**

- [x] Repeated excerpts of the same source count as one source.
- [x] A forensic report and its summary email count as one evidentiary origin.
- [x] Detective requires at least two meaningful corroborating paths.
- [x] Higher difficulties can require additional or deeper paths.

## PROOF-005 - Implement reachability closure

**Status:** [x] Complete

Model the player state:

```text
Initial
AfterAction
AfterForensic
```

Calculate which assets, emails, observations, and actions become available after
each valid player action.

**Acceptance criteria**

- [x] Hidden evidence is not considered until its trigger is satisfied.
- [x] Every required asset and analysis is reachable.
- [x] Circular reveal dependencies are rejected.
- [x] The final reachable observation set is sufficient to derive the culprit.

## PROOF-006 - Implement premature-solution leakage gate

**Status:** [x] Complete

Run the proof engine using only initial observations.

**Acceptance criteria**

- [x] Rookie must be solvable from initial observations.
- [x] Detective and higher must not expose their gated decisive join initially.
- [x] The gate reports the exact initial derivation that leaked the answer.

---

# Phase 3 - Forensic contract

**Goal:** make forensic generation scientifically constrained and fully
traceable.

## FORENSICS-001 - Create immutable forensic method catalog

**Status:** [x] Complete

Each method must define:

- accepted input object types;
- accepted asset types;
- observations it may produce;
- limitations;
- whether a reference sample is required;
- whether chain of custody is required;
- valid result layouts.

**Acceptance criteria**

- [x] The LLM cannot extend `availableFor`.
- [x] Incompatible input and analysis combinations fail immediately.
- [x] Method names and descriptions support all four languages.

## FORENSICS-002 - Replace forced smoking-gun fallback

**Status:** [x] Complete

Remove logic that assigns the culprit to the first useful outcome when the
forensics planner fails to produce a valid match.

**Acceptance criteria**

- [x] Missing decisive forensic attribution is a validation failure.
- [x] The owning graph node is repaired or the case is rejected.
- [x] No suspect match is fabricated deterministically.

## FORENSICS-003 - Validate forensic input subject

**Status:** [x] Complete

Ensure the report analyzes the exact object contained in the input asset.

**Acceptance criteria**

- [x] A report cannot analyze a PDF merely mentioned in an email.
- [x] The Items Submitted section references the canonical input object.
- [x] Findings can only mention properties producible by the selected method.

## FORENSICS-004 - Validate result limitations

**Status:** [x] Complete

Examples:

- metadata may correlate a file with a device but not prove who operated it;
- account attribution may identify an account holder but not prove intent;
- image analysis cannot invent unreadable details;
- timestamps with uncertain provenance must expose that limitation.

**Acceptance criteria**

- [x] Every forensic transform has at least one limitation statement.
- [x] The solution cannot claim more than the forensic observation supports.

---

# Phase 4 - Evidence and decoy delivery contracts

## EVIDENCE-001 - Remove automatic semantic clue assignment

**Status:** [x] Complete

Stop assigning uncovered clues to arbitrary source-compatible assets.

**Acceptance criteria**

- [x] Missing ownership produces a repair issue.
- [x] Repair targets the evidence plan rather than silently selecting an asset.
- [x] A case is rejected after repeated ownership failure.

## EVIDENCE-002 - Define typed asset specification

**Status:** [x] Complete

Suggested fields:

```csharp
public sealed class EvidenceAssetSpec
{
    public string Id { get; set; }
    public string ArchetypeId { get; set; }
    public List<string> ObservationIds { get; set; }
    public List<string> ContainedObjectIds { get; set; }
    public VisibilityGate Visibility { get; set; }
    public string LayoutId { get; set; }
}
```

**Acceptance criteria**

- [x] Assets own observations rather than generic clue IDs.
- [x] An asset declares which physical/digital objects it contains.
- [x] Empty placeholder assets are rejected.
- [x] Layout and source-type compatibility remains deterministic.

## EVIDENCE-003 - Define typed decoy arcs

**Status:** [x] Complete

```csharp
public sealed class DecoyArc
{
    public string SuspectId { get; set; }
    public List<string> SuspicionObservationIds { get; set; }
    public List<string> VerificationObservationIds { get; set; }
    public string ResolutionDerivationId { get; set; }
    public bool MustBePlayerReachable { get; set; }
}
```

**Acceptance criteria**

- [x] Every non-culprit suspect has a plausible suspicion path.
- [x] Every major decoy has a player-reachable verification path.
- [x] Decoys are not excluded merely by absence of evidence.
- [x] Decoy evidence does not explicitly label a suspect innocent.

## EVIDENCE-004 - Validate document-to-observation fidelity

**Status:** [x] Complete

Use deterministic checks for exact canonical values and a narrow LLM reviewer for
semantic overstatement.

**Acceptance criteria**

- [x] Every assigned observation is present.
- [x] Unassigned private facts are absent.
- [x] Descriptions cannot contain stronger conclusions than document bodies.
- [x] Empty tables, transcripts, exports, and placeholder records are rejected.

---

# Phase 5 - Solver redesign

## SOLVER-001 - Build player-visible document projection

**Status:** [x] Complete

The solver must receive complete player-visible content:

- `body`;
- all `bodyDoc` sections;
- visible emails and attachments;
- current suspects;
- current timeline;
- currently available actions.

It must not receive:

- solution fields;
- hidden observations;
- future forensic results;
- the full reveal graph;
- private CaseGraph facts.

## SOLVER-002 - Implement sequential player simulation

**Status:** [x] Complete

The solver should respond with one action at a time:

```text
inspect asset
compare records
request analysis
review result
accuse suspect
answer questions
```

**Acceptance criteria**

- [x] Results are revealed only after valid actions.
- [x] The solver cannot select an unavailable analysis.
- [x] The complete action trace is stored for diagnosis.
- [x] A successful solve cites exact evidence observations.

## SOLVER-003 - Separate deterministic guarantees from narrative review

**Status:** [x] Complete

- Deterministic proof engine: proves that a solution exists.
- Deterministic graph witness: replays the exact public actions, observations,
  analyses, accusation citations, and answers that solve the case.
- LLM specialists: assess prose quality and human plausibility as advisory
  findings.

Only reproducible deterministic failures block persistence. Probabilistic
reviewer findings cannot trigger regeneration loops; they remain available for
quality telemetry and manual review.

---

# Phase 6 - Specialized quality reviewers

## REVIEW-001 - Replace broad red team with specialist reports

**Status:** [x] Complete

Create focused reviewers:

1. Document Fidelity Reviewer
2. Scientific Realism Reviewer
3. Narrative and Human Behavior Reviewer
4. Locale and Procedural Realism Reviewer
5. Player Experience Reviewer

Each reviewer must have:

- a narrow input;
- a typed finding schema;
- explicit owning node IDs;
- deterministic severity rules where possible.

## REVIEW-002 - Move objective checks out of LLM review

**Status:** [x] Complete

Move these checks to deterministic validators:

- timezone validity;
- timestamp equality and ordering;
- reveal reachability;
- required evidence existence;
- forensic compatibility;
- duplicate sources;
- question provenance;
- private-fact leakage;
- decoy verification coverage.

## REVIEW-003 - Add locale profiles

**Status:** [x] Complete

Create locale data for the four supported languages and common target regions.

The profile may define:

- timezone identifiers;
- date/time presentation;
- police and administrative terminology;
- neutral placeholder domains;
- report headings;
- acceptable agency naming patterns.

This should prevent repeated subjective findings such as incorrectly rejecting a
valid IANA timezone.

---

# Phase 7 - Granular repair architecture

## REPAIR-001 - Replace stage booleans with repair operations

**Status:** [x] Complete

```csharp
public sealed class RepairOperation
{
    public string TargetNodeId { get; set; }
    public string ConstraintId { get; set; }
    public RepairOperationKind Operation { get; set; }
    public List<string> InvalidatedNodeIds { get; set; }
}
```

Supported operations should initially include:

- ReplaceFact
- ReplaceEvent
- ReplaceObservation
- ReassignObservation
- RewriteAsset
- ReplaceForensicTransform
- RewriteQuestion
- RewriteExplanation

## REPAIR-002 - Add node ownership and dependency index

**Status:** [x] Complete

Every generated unit records:

- owning stage/agent;
- input node IDs;
- output node IDs;
- downstream dependents;
- content hash.

**Acceptance criteria**

- [x] Repairing one asset does not regenerate unrelated assets.
- [x] Unchanged inputs produce byte-stable outputs.
- [x] Invalidated dependents are calculated, not guessed by the red team.

## REPAIR-003 - Add per-node repair budgets

**Status:** [x] Complete

**Acceptance criteria**

- [x] Repeated failure of the same constraint stops after a configured limit.
- [x] The pipeline reports the exact node and constraint that plateaued.
- [x] Best valid nodes remain frozen while the failing node is retried.

---

# Phase 8 - Difficulty topology

## DIFFICULTY-001 - Replace count-only profiles

**Status:** [x] Complete

Extend or replace `DifficultyProfile` with:

```csharp
public sealed class DifficultyTopology
{
    public int MinDerivationDepth { get; set; }
    public int MaxDerivationDepth { get; set; }
    public int MinIndependentProofPaths { get; set; }
    public int RequiredDecoyArcs { get; set; }
    public int MinForensicHops { get; set; }
    public int RedHerringResolutionDepth { get; set; }
    public int RequiredCrossSourceCorrelations { get; set; }
}
```

Asset, suspect, and analysis counts remain useful as bounds but no longer define
difficulty.

## DIFFICULTY-002 - Define Rookie topology

**Status:** [x] Complete

- all evidence initial;
- zero forensic hops;
- short direct derivations;
- clear but non-spoiling corroboration;
- every decoy directly verifiable.

## DIFFICULTY-003 - Define Detective topology

**Status:** [x] Complete

- at least one three-hop attribution chain;
- one gated forensic join;
- at least two independent proof paths;
- all major decoys verifiable;
- initial evidence insufficient for final attribution.

## DIFFICULTY-004 - Define Detective2 and Sergeant topology

**Status:** [x] Complete

- deeper derivations;
- partially overlapping evidence paths;
- at least one conflicting but explainable observation;
- stronger decoy arcs;
- one or more optional analyses that help but are not fake filler.

## DIFFICULTY-005 - Define Lieutenant through Commander topology

**Status:** [x] Complete

- multiple plausible hypotheses;
- multiple forensic or institutional correlations;
- different source reliability levels;
- meaningful investigation order;
- no dependence on arbitrary obscurity or unavailable facts.

---

# Phase 9 - Testing and validation corpus

## TEST-001 - Unit tests for every contract

**Status:** [x] Complete

Cover:

- entity and fact references;
- event ordering;
- derivation closure;
- culprit uniqueness;
- independent proof paths;
- reachability;
- forensic admissibility;
- decoy resolution;
- private-fact leakage;
- question provenance.

## TEST-002 - Mutation tests

**Status:** [x] Complete

Starting from a valid case, automatically mutate it:

- remove the decisive observation;
- replace a timestamp;
- change an account owner;
- expose a private fact;
- select an incompatible analysis;
- remove a reveal rule;
- remove a decoy verification;
- duplicate the same evidence under another asset;
- make a second suspect satisfy the culprit derivation.

Every mutation must fail the expected gate.

## TEST-003 - Golden cases

**Status:** [x] Complete

Maintain approved reference cases for:

- Rookie;
- Detective;
- Detective2;
- Sergeant;
- Lieutenant;
- Captain;
- Commander.

Golden cases should store:

- private CaseGraph;
- public `case.json`;
- rendered-text projection;
- expected derivations;
- expected reachability trace;
- expected player-solver trace.

## TEST-004 - Batch generation harness

**Status:** [x] Complete

Generate multiple seeds and themes per difficulty and report:

- graph validation pass rate;
- first-pass success rate;
- average repair operations;
- repair plateau rate;
- solver success rate;
- red-team findings by category;
- latency and token use;
- evidence-layout diversity.

## TEST-005 - Difficulty approval gates

**Status:** [x] Complete

Suggested initial approval requirements:

- zero schema or deterministic graph errors;
- 100% culprit uniqueness;
- 100% required evidence reachability;
- zero forensic compatibility violations;
- zero private-fact leakage;
- successful sequential solver;
- no blocking specialist-review findings;
- successful generation across a representative multi-seed batch.

---

# Phase 10 - Integration and migration

## MIGRATION-001 - Add graph generation behind a feature flag

**Status:** [x] Complete

Run the existing and graph pipelines side by side during development, but never
allow both to act as canonical truth.

## MIGRATION-002 - Compile existing public contract from CaseGraph

**Status:** [x] Complete

Verify parity for:

- metadata;
- suspects;
- assets;
- emails;
- timeline;
- temporal events;
- forensics defaults and outcomes;
- rules;
- solution;
- generation metadata.

## MIGRATION-003 - Validate Rookie regression

**Status:** [x] Complete

The approved Rookie reference must retain:

- zero analyses;
- zero forensic outcomes;
- zero required analysis IDs;
- all solving evidence initially reachable;
- equivalent scoring behavior.

## MIGRATION-004 - Approve Detective

**Status:** [x] Complete

Detective approval requires:

- [x] CaseGraph and proof gates implemented.
- [x] No semantic fallbacks masking failures.
- [x] Sequential solver implemented.
- [x] At least one persisted golden Detective case.
- [x] Multi-seed Detective batch passes the agreed quality threshold.

Approval evidence:

- `case_graph_detective_203`, `case_graph_detective_204`, and
  `case_graph_detective_205` were generated from distinct seeds and themes.
- Every private validation report has `Issues: []` and `IsValid: true`.
- Every deterministic graph-witness trace has score `1`, exact observation
  citations, required forensic analysis execution, and accepted accusation.
- `case_graph_rookie_103` independently preserves the Rookie zero-forensics
  contract and also passes with `Issues: []` and score `1`.

## MIGRATION-005 - Enable higher ranks incrementally

**Status:** [ ] Blocked

Do not approve Sergeant or higher based solely on one successful sample.
Enable one topology at a time after the previous level has stable batch results.

---

# Recommended implementation order

1. GRAPH-001 through GRAPH-006
2. PROOF-001 through PROOF-006
3. FORENSICS-001 through FORENSICS-004
4. EVIDENCE-001 through EVIDENCE-004
5. SOLVER-001 through SOLVER-003
6. REPAIR-001 through REPAIR-003
7. REVIEW-001 through REVIEW-003
8. DIFFICULTY-001 through DIFFICULTY-005
9. TEST-001 through TEST-005
10. MIGRATION-001 through MIGRATION-005

## First implementation milestone

The first meaningful milestone is not a new generated case. It is:

> Given a small hand-authored Detective CaseGraph, deterministic code can prove
> that exactly one suspect is derivable through two independent, player-reachable
> evidence paths and that every decoy has a reachable refutation.

Once this works, LLM agents can safely generate content against that contract.

## Definition of done

The redesign is complete when:

- the private CaseGraph is the sole canonical truth;
- public `case.json` is compiled from that graph;
- deterministic gates prove logical solvability and reachability;
- LLM reviewers evaluate only prose and realism;
- repairs modify the smallest owning node;
- Detective through Commander difficulty is expressed as reasoning topology;
- every supported difficulty has a golden case and stable multi-seed results.
