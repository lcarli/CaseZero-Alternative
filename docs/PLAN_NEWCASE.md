# New Case Generation MVP Plan

## Objective

Deliver case generation that may require retries but only publishes cases that are valid, high-quality, solvable, and playable.

First-attempt success is not an MVP requirement. The non-negotiable requirement is that an invalid or incomplete case never reaches the game.

## Product decisions

- A generation job may perform up to five complete attempts by default.
- The retry limit must be configurable.
- All attempts remain under the same `jobId`.
- Validation rejection and classified transient failures may trigger another attempt.
- Permanent configuration, authentication, cancellation, and non-retryable infrastructure failures must fail explicitly.
- Exhausting the retry budget is an expected failed-job outcome, not a successful generation.
- The Case Bible may continue to be generated as one unit for the MVP.
- Splitting the Case Bible and implementing fully targeted stage regeneration are post-MVP optimizations.

## Publication contract

A case can be persisted or published only when all of the following are true:

1. The Case Bible is semantically and referentially valid.
2. The public `case.json` passes its schema.
3. The CaseGraph passes graph, reachability, and difficulty-topology validation.
4. Public and canonical graph projections have no blocking parity issue.
5. The deterministic solver succeeds with a score of at least `0.90`.
6. The specialist review has no blocking finding.
7. Final validation has no blocking issue.
8. Every mandatory visual asset renders successfully.

Rejected attempts must retain diagnostics but must not publish a bundle or leave a success-shaped response.

## Real-time progress contract

Phase reporting is part of the generation feature, not optional test telemetry.

Every generation entry point and test harness must report enough information for the website to render the job in real time:

- current job attempt and configured maximum;
- attempt start, completion, failure, and retry backoff;
- current internal phase and stable public stage;
- stage status, timestamps, duration, and attempt count;
- validation or retry reason without exposing private case truth;
- completed-attempt history;
- terminal success or explicit terminal failure.

The existing public stages remain:

1. `caseDesign`
2. `graphConstruction`
3. `evidenceProduction`
4. `forensicWorkflow`
5. `solutionDesign`
6. `deterministicValidation`
7. `solutionWitness`
8. `advisoryReview`
9. `targetedRepair`
10. `finalValidation`
11. `finalization`

The status endpoint remains pollable while generation runs. A new complete attempt must not erase progress from previous attempts.

## Implementation plan

### Phase 1 - Acceptance and deterministic corrections

- Centralize the MVP publication contract.
- Normalize safe typed-ID corrections and update every affected reference.
- Flatten nested IDs such as `fact.access.device` to one typed suffix.
- Normalize accepted enum aliases before structured deserialization rejects them.
- Rewrite incident-related timestamps to the canonical UTC offset while preserving the instant.
- Replace rejected investigator placeholder domains with an accepted neutral domain.
- Never invent a missing canonical entity to repair an unresolved reference.
- Add regression tests for every deterministic correction.

### Phase 2 - Retry-aware progress

- Extend the progress document with `currentAttempt`, `maxAttempts`, retry state, and attempt history.
- Preserve completed and failed stage history between full attempts.
- Report semantic Case Bible retries and targeted-repair iterations.
- Keep progress monotonic within an attempt while allowing a new attempt to restart its own stage sequence.
- Version the progress contract while retaining compatibility fields for existing clients.

### Phase 3 - Automatic complete retries

- Move the complete-attempt loop into the Durable orchestration.
- Use one activity execution per complete generation attempt.
- Classify failures as retryable generation rejection, retryable transient infrastructure failure, or permanent failure.
- Apply configurable exponential backoff.
- Prevent failed attempts from publishing and clean their partial artifacts.
- Return the first fully accepted result.

### Phase 4 - API and website

- Return attempt history, backoff, phase, stage, and failure summaries from the Function status endpoint.
- Preserve the backend proxy pass-through behavior.
- Update frontend API types.
- Show `attempt N of M`, current phase, stage history, retry countdown, durations, and final failure.
- Add every new UI string to `en-US`, `pt-BR`, `es-ES`, and `fr-FR`.

### Phase 5 - Test instrumentation and generation validation

- Run real-generation tests with a recording phase reporter.
- Persist phase and attempt events in the soak report.
- Fail tests when a long-running phase or retry is not observable.
- Cover progress history, retry transitions, explicit failure, and publication gating.
- Re-run seeds `1102`, `2102`, `3101`, `5102`, `6103`, and `7102`.
- Run a small matrix across all seven difficulties after the failed-seed rerun.

## MVP acceptance criteria

- No rejected case is persisted or published.
- Every published case satisfies the publication contract.
- A job can recover through complete retries without changing its `jobId`.
- The website can display every attempt and phase while generation is running.
- Retry exhaustion returns a clear failed state and diagnostics.
- New UI text exists in all four supported languages.
- `CaseGen.Functions` and `CaseGen.Functions.Tests` remain on .NET SDK 9.

## Deferred work

- Splitting Case Bible generation into Truth Core, World Expansion, and Proof Design.
- Resuming an activity from an internal phase after a host crash.
- Regenerating only rejected graph nodes and their dependencies.
- Optimizing primarily for first-attempt success, latency, or token cost.

## Execution status

- [x] MVP acceptance, publication, retry, and progress contracts documented.
- [x] Initial deterministic normalization implemented for typed fact, observation, source, and clue IDs with reference redirection.
- [x] Canonical UTC-offset rewriting implemented for incident, timeline, schedule, observation, and fact timestamps.
- [x] Rejected investigator placeholder domains are rewritten to `casezero.local`.
- [x] Regression coverage added for the initial normalization work.
- [x] Safe enum-alias normalization.
- [x] Retry-aware progress history.
- [x] Automatic complete retries.
- [x] API and four-language UI updates.
- [x] Real-generation harness instrumented with phase-event persistence.
- [x] Failed-seed rerun: all six previously failing seeds passed within five attempts with phase events recorded.
- [x] Seven-difficulty real-generation validation: all seven passed within five attempts with phase events recorded.

### Real-generation validation results

Previously failing seeds:

| Difficulty | Seed | Successful attempt |
|---|---:|---:|
| Rookie | 1102 | 1 |
| Detective | 2102 | 2 |
| Detective2 | 3101 | 2 |
| Lieutenant | 5102 | 4 |
| Captain | 6103 | 2 |
| Commander | 7102 | 3 |

Seven-difficulty MVP matrix:

| Difficulty | Seed | Successful attempt | Solver score |
|---|---:|---:|---:|
| Rookie | 1101 | 1 | 1.00 |
| Detective | 2101 | 1 | 1.00 |
| Detective2 | 3101 | 1 | 1.00 |
| Sergeant | 4101 | 2 | 0.95 |
| Lieutenant | 5101 | 2 | 1.00 |
| Captain | 6101 | 5 | 0.95 |
| Commander | 7101 | 2 | 1.00 |

No rate-limit response was detected. Every recorded attempt emitted phase events. No rejected attempt was counted as a published success.
