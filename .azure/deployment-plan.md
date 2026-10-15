# Deployment plan: existing DEV CaseGen Function

## 1. Status

**Status:** Draft

The user approved completing the reconciled architecture before publication.
Source reconciliation is being committed for a cross-machine handoff.
Local Functions compilation succeeded, but the complete offline suite has
20 failures (682 passing / 702 total). Architecture safeguards and a
Functions-only workflow are still pending. No deployment has been executed.

## 2. Approved scope

- Update only the existing DEV Function App `casegen-func-dev`.
- Keep the feature branch `lcarli/feat-casegen-quality-gate`; no merge to main.
- The user approved incorporating current main into the feature before deployment.
- Use main's canonical CaseBible/graph architecture, port the feature safeguards
  and complete remaining original-plan requirements before a single final release.
- Do not deploy the API/frontend or apply database migrations.
- Keep Functions and Functions tests on .NET SDK 9.
- Preserve the existing key-based model authentication, as confirmed by the user.
- Do not change Azure roles, tenant policy, networking or resource topology.
- Use GitHub Actions credentials or the user's other, tenant-authorized
  computer. Do not attempt interactive Azure login on this machine.
- Do not generate or publish a case as part of code deployment.

## 3. Azure context

- Existing Function App: `casegen-func-dev`.
- Existing resource group: `casezero-func-dev-rg`, to verify against the workflow.
- Subscription and location: preserve the existing deployment context; verify
  the target resource through the CI identity before deployment.
- No new resources or regions are planned.

## 4. Recipe and artifacts

Recipe selection and the existing GitHub Actions configuration are under review.
Reuse the existing DEV pipeline without running its full-stack deployment.
Required workflow/preflight changes, if any, will be recorded before approval.

## 5. Preparation steps

- [ ] Inspect current branch, deployment workflow and Function configuration.
- [ ] Finalize a Functions-only deployment path with explicit failure behavior.
- [ ] Confirm the final plan before implementation/deployment.
- [ ] Build/test/publish the .NET 9 package without local secrets.
- [ ] Update the versioned handoff.

## 6. Validation and deployment

- [ ] Run local and remote preflight checks.
- [ ] Invoke azure-validate and record actual validation proof.
- [ ] Deploy only after the plan is Validated.
- [ ] Verify the deployed Function and record the endpoint/results.

## 7. Validation Proof

Pending. No validation result is asserted by this initial plan.

## 8. Risks and unresolved items

- Main `ff78eb5` is incorporated on the feature using its richer canonical
  architecture and the feature safeguards. Compilation is not deployment
  approval: 20 complete-suite failures and the pending architecture work in
  `docs/CASE_GENERATION_HANDOFF.md` still block publication.
- The starter still has fail-open best-effort singleton admission. Atomic
  admission, cancellation/publication race checks, real Blob-store tests and
  active-job/orchestrator compatibility preflight are unfinished.
- The model rejected key authentication locally; user confirmed that the
  existing key configuration can be used from the Azure Function.
- Cloud app-setting presence, .NET 9 runtime, deployment permissions and host
  readiness must be verified without printing credentials.
- Avoid interrupting active Durable generation jobs.
- A Functions-only deployment does not release the PR05 API/frontend changes
  or its database migration and does not certify gameplay.
- The Rookie pilot remains separate and requires preserving its isolation
  and the distinction between Function deployment and case publication.
