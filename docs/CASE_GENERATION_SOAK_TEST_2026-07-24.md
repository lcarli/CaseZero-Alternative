# Case Generation Soak Test - 2026-07-24

This report records the first multi-difficulty soak test of the Case v2 graph-mode generator after the Case Bible and CaseGraph stabilization work.

The detailed generator architecture is documented in [`CASE_GENERATION_PIPELINE.md`](./CASE_GENERATION_PIPELINE.md).

## Executive conclusion

The generated cases that passed the complete pipeline were structurally strong and solvable. Their average deterministic solver score was `0.986`, with a minimum score of `0.90`.

The generator is not yet reliable enough for unattended production generation:

- 14 of 20 completed cases passed after all retries: **70%**;
- only 8 of 20 passed on the first attempt: **40%**;
- 6 successful cases required a second or third attempt;
- 6 cases still failed after three attempts;
- the remaining Commander case was cancelled at the user's request.

The final validation gate behaved correctly: invalid cases were blocked rather than accepted or published. The current risk is generation reliability, latency, and token cost, not invalid cases bypassing validation.

No Azure OpenAI rate-limit response was detected during the run.

## Scope and configuration

The soak harness was:

```text
scripts/run-casev2-soak.ps1
```

Configuration:

| Setting | Value |
|---|---:|
| Difficulties | Rookie through Commander |
| Planned cases per difficulty | 3 |
| Planned cases | 21 |
| Completed cases | 20 |
| Cancelled cases | 1 |
| Language | `en-US` |
| Concurrency | 1 |
| Cooldown between cases | 60 seconds |
| Maximum attempts per seed | 3 |
| Rate-limit backoff | Exponential, starting at 300 seconds |
| Other retry backoff | Exponential, starting at 60 seconds |
| Blob publication | Disabled |
| CaseGraph | Enabled |

The batch ran sequentially for approximately six hours. It wrote an incremental resumable report after each attempt. Successful generated case directories were removed after their metrics were recorded.

The harness used three themes:

- museum archive theft;
- waterfront cold case;
- corporate financial fraud.

## Aggregate results

| Metric | Result |
|---|---:|
| Completed | 20 |
| Passed | 14 |
| Failed after three attempts | 6 |
| Cancelled | 1 |
| Completed-case pass rate | 70% |
| First-attempt pass rate | 40% |
| Cases recovered by retry | 6 |
| Attempts across completed cases | 41 |
| Rate-limited attempts | 0 |
| Average solver score for passed cases | 0.986 |
| Minimum solver score for passed cases | 0.90 |
| Cumulative completed-case generation time | 6.01 hours |
| Average passed-case duration | 14 minutes |

Token accounting was only persisted for successful runs. It must not be interpreted as the complete cost of the soak test because failed attempts can terminate before the harness receives a generation result.

| Recorded successful-run tokens | Result |
|---|---:|
| Input | 11,543,086 |
| Output | 617,416 |
| Average input per successful case | 824,506 |
| Average output per successful case | 44,101 |

## Results by difficulty

Commander seed `7103` was cancelled and is excluded from the completed-case pass rate.

| Difficulty | Passed | Failed | Cancelled | Completed pass rate |
|---|---:|---:|---:|---:|
| Rookie | 2 | 1 | 0 | 66.7% |
| Detective | 2 | 1 | 0 | 66.7% |
| Detective2 | 2 | 1 | 0 | 66.7% |
| Sergeant | 3 | 0 | 0 | 100% |
| Lieutenant | 2 | 1 | 0 | 66.7% |
| Captain | 2 | 1 | 0 | 66.7% |
| Commander | 1 | 1 | 1 | 50% |

Successful cases preserved the expected progression:

| Difficulty | Successful seeds | Solver scores | Suspects | Assets | Images | Forensic outcomes | Decoys |
|---|---|---|---|---|---|---|---|
| Rookie | 1101, 1103 | 1.00, 1.00 | 3 | 10-12 | 5 | 0 | 2 |
| Detective | 2101, 2103 | 1.00, 1.00 | 3 | 15-17 | 4-5 | 1 | 2 |
| Detective2 | 3102, 3103 | 1.00, 0.90 | 4 | 17-18 | 5-6 | 2 | 3 |
| Sergeant | 4101, 4102, 4103 | 1.00, 1.00, 1.00 | 4 | 18-20 | 7-8 | 2-3 | 3 |
| Lieutenant | 5101, 5103 | 1.00, 0.96 | 4 | 19-21 | 6-7 | 3 | 3 |
| Captain | 6101, 6102 | 1.00, 0.95 | 5 | 20-21 | 7-8 | 3 | 4 |
| Commander | 7101 | 1.00 | 5 | 21 | 7 | 4 | 4 |

## Failed seeds

### Rookie 1102

The three attempts failed for different reasons:

1. incident timestamp did not use the canonical UTC offset;
2. an observation ID did not use the typed `observation.` prefix;
3. the investigator email used a rejected non-neutral placeholder domain.

The final failure was:

```text
locale.investigatorEmail uses a non-neutral placeholder domain
```

### Detective 2102

The attempts exposed:

1. non-canonical offsets and undeclared observation conflicts;
2. insufficient non-forensic proof-origin diversity;
3. another rejected investigator email placeholder domain.

The final failure was the locale email validation.

### Detective2 3101

The attempts exposed:

1. malformed JSON;
2. an invalid typed source ID and a broken source reference;
3. an unsupported `FactTruthStatus` value.

The model did not converge to the structured Case Bible contract within three attempts.

### Lieutenant 5102

The attempts exposed:

1. multiple observation IDs without typed prefixes;
2. an unsupported `ObservationFidelity` value;
3. only two independently originated culprit paths when Lieutenant requires three.

The final solver score was zero because the logical topology gate correctly rejected the case.

### Captain 6103

The attempts exposed:

1. an observation ID without a typed prefix;
2. a broken fact reference;
3. nested fact IDs such as `fact.access.*` and references to accounts absent from the canonical world model.

The Case Bible failed before downstream asset generation.

### Commander 7102

The attempts exposed:

1. non-canonical offsets and missing decoy verification observations;
2. only two independent culprit paths and two reliability levels instead of three;
3. a work schedule unrelated to the incident window, a missing public timeline description, and another offset mismatch.

The second attempt reached final graph validation but failed the Commander topology contract. The other attempts failed earlier in Case Bible validation.

## Failure clusters

### 1. Structured Case Bible contract instability

The largest cluster includes:

- malformed JSON;
- unsupported enum values;
- missing typed prefixes;
- nested typed IDs;
- broken references;
- missing canonical entities;
- missing public descriptions.

These failures occur before the stronger downstream repair system can help. Retrying the whole Case Bible can replace one defect with a different defect.

### 2. Temporal and locale normalization gaps

Repeated failures involved:

- timestamps using a different offset from the canonical location;
- truth timeline beats retaining the wrong offset;
- investigator email domains rejected only during final locale validation.

These values should be normalized deterministically before semantic or final validation whenever the correction is unambiguous.

### 3. Proof-path topology

Lieutenant and Commander exposed cases where the declared proof design collapsed to only two independently originated graph paths.

The validator and solver correctly blocked these cases. The construction step must become more deterministic so that proof-path independence does not depend only on the model following prompt instructions.

### 4. Retry behavior

Retries recovered six cases, but the failed seeds demonstrate that each attempt can generate an entirely different defect. A retry is currently closer to rerolling the complete canonical world than repairing the rejected nodes.

This increases:

- latency;
- token consumption;
- variance;
- the probability that a corrected issue is replaced by another issue.

## Production-readiness assessment

The pipeline is safe in the following limited sense:

- approved cases passed graph, artifact, solver, and final validation;
- rejected cases did not pass the persistence/publication gate;
- successful cases had high solver scores and appropriate dossier depth.

It is not yet ready for unattended bulk production because:

- 30% of completed cases failed after all retries;
- 60% did not pass on the first attempt;
- advanced topology still fails stochastically;
- the monolithic Case Bible response remains a high-variance and high-cost step.

Until the reliability work below is complete, production generation should expose failure explicitly and require an operator to retry or review the job. A generic success response must never be returned for a blocked case.

## Recommended implementation order

### Priority 0 - Deterministic normalization

Before requesting another full Case Bible:

1. canonicalize accepted typed-ID shapes and flatten nested IDs;
2. normalize safe enum aliases to supported values;
3. rewrite all incident-related timestamps to the canonical UTC offset while preserving the instant;
4. canonicalize generated investigator email domains to an accepted neutral domain;
5. redirect references when an ID was deterministically renamed;
6. reject, rather than invent, references to canonical entities that do not exist.

Regression tests should cover every failure shape listed in this report.

### Priority 1 - Deterministic proof-path construction

Proof paths should be assembled from explicit canonical origins. Each branch must retain its own non-forensic ancestry, and forensic observations must not be attached globally in a way that collapses independent branches.

Lieutenant and Commander require dedicated regression tests proving three independently originated culprit paths.

### Priority 1 - Split Case Bible generation

Replace the single large generation request with:

```text
Truth Core
  -> World Expansion
  -> Proof Design
  -> Assembled and normalized Case Bible
```

Each stage should have its own schema, validator, retry budget, and persisted snapshot. This reduces structured-output size and allows a failed proof design to be regenerated without replacing an already valid world.

### Priority 2 - Targeted semantic retries

When validation fails:

- preserve valid nodes;
- send only rejected nodes and their dependencies to the responsible stage;
- revalidate references after the patch;
- avoid regenerating the complete case for a local schema or topology defect.

### Priority 2 - Rerun strategy

After the fixes:

1. rerun the six failed seeds: `1102`, `2102`, `3101`, `5102`, `6103`, and `7102`;
2. require all six to pass;
3. run a new three-seed matrix across all seven difficulties;
4. require at least 90% first-attempt success and 100% success within the configured retry budget before unattended production generation.

## Reproducing the soak test

Run the complete matrix:

```powershell
scripts\run-casev2-soak.ps1 `
  -CasesPerDifficulty 3 `
  -CooldownSeconds 60 `
  -MaxAttempts 3 `
  -Language en-US `
  -ReportPath casev2-soak-report.json
```

The harness:

- runs one generation at a time;
- persists progress after every attempt;
- skips cases already marked as passed when resumed;
- applies longer backoff when logs indicate a `429` or rate-limit response;
- records solver, dossier, token, latency, and retry metrics;
- removes successful generated directories unless `-KeepCases` is supplied.

Use `-KeepCases` only when the generated artifacts are needed for manual inspection.

---

## Addendum - Post-remediation status (2026-07-28)

*This addendum is appended after the original report and does not alter any measurement above. It records what changed in the codebase since 2026-07-24 in response to the findings, based on commit `e1a4bf9` ("feat(casegen): stabilize retry-aware case generation (#162)", merged 2026-07-27).*

### What shipped against the recommended implementation order

- **Priority 0 - Deterministic normalization**: `CaseBibleNormalizer` (`functions/CaseGen.Functions/Services/CaseV2/CaseBible.cs`) now runs before Case Bible validation and canonicalizes the exact defect classes seen in this soak test:
  - `NormalizeUtcOffset` / `NormalizeCanonicalTimestamps` rewrite incident, truth-timeline, and schedule timestamps to the world's canonical UTC offset (addresses the Rookie 1102, Detective 2102, Lieutenant 5102, and Commander 7102 offset failures).
  - `NormalizeInvestigatorEmail` rewrites the investigator's email domain to the neutral `casezero.local` domain (directly addresses the `locale.investigatorEmail uses a non-neutral placeholder domain` failures that ended the Rookie 1102 and Detective 2102 runs).
  - `CaseBibleValidator` (`functions/CaseGen.Functions/Services/CaseV2/CaseBibleValidator.cs`) enforces typed-ID prefixes, canonical-entity references, and offset/enum consistency across people, institutions, schedules, and the truth timeline.
- **Priority 1 - Split/staged Case Bible generation with semantic retry**: `CaseBibleTask` (`functions/CaseGen.Functions/Services/CaseV2/Tasks/CaseBibleTask.cs`) generates the Case Bible, applies `CaseBibleNormalizer.Normalize`, then validates; on failure it retries the semantic step (up to `MaxSemanticAttempts = 3`) rather than discarding the whole run immediately.
- **Priority 2 - Targeted repair instead of full regeneration**: `RepairCoordinator` (`functions/CaseGen.Functions/Services/CaseV2/RepairCoordinator.cs`) re-runs only the flagged sections (e.g. emails, forensics, assets) identified by validation instead of regenerating the entire case.
- **Regression coverage**: `CaseBibleArchitectureTests.cs` and `AssetRenderingTests.cs` were added under `functions/CaseGen.Functions.Tests/CaseV2/` to cover Case Bible structural/typed-ID/enum defects and asset rendering, and `GenerationProgressTests.cs` covers phase reporting.

### Post-remediation real-generation results

All six seeds that exhausted the original three-attempt budget later produced valid cases within the new five-attempt budget:

| Seed | Successful attempt |
|---:|---:|
| 1102 | 1 |
| 2102 | 2 |
| 3101 | 2 |
| 5102 | 4 |
| 6103 | 2 |
| 7102 | 3 |

A separate one-seed-per-difficulty matrix also passed:

| Difficulty | Seed | Successful attempt | Solver score |
|---|---:|---:|---:|
| Rookie | 1101 | 1 | 1.00 |
| Detective | 2101 | 1 | 1.00 |
| Detective2 | 3101 | 1 | 1.00 |
| Sergeant | 4101 | 2 | 0.95 |
| Lieutenant | 5101 | 2 | 1.00 |
| Captain | 6101 | 5 | 0.95 |
| Commander | 7101 | 2 | 1.00 |

No rate limits occurred, and every attempt emitted phase progress events.

### Remaining validation boundary

The original recommendation called for a new three-seed matrix across every difficulty with at least 90% first-attempt success. The post-remediation matrix above used one seed per difficulty and therefore does not establish that first-attempt reliability target or unattended bulk-generation readiness.

**Conclusion:** the remediated generator satisfies the MVP objective validated here: retries can recover the previously failing seeds while the publication gates accept only valid, solvable cases with solver scores of at least `0.90`. A larger soak is still required before claiming unattended high-volume reliability.
