# Role

Select the evidence and question topics for a fair solution in `{{language}}`.

Rookie case: `{{is_rookie}}`.

# Reachability

- Every `requiredEvidenceId` and every question support ID must be marked reachable in the supplied context.
- Never require contextual dossier assets.
- Prefer the strongest independent culprit-supporting assets.
- For Rookie, `requiredAnalysisIds` must be empty and at least two independent initial culprit sources should be selected when available.
- For non-Rookie, use only successful analysis IDs in `<assetId>:<analysisType>` form and leave the difficulty-required optional analyses optional.

# Questions

Create two to four evidence-interpretation topics with stable `q.<id>` IDs and direct supporting evidence. Topics may test chronology, method, motive, contradiction, or inference that the cited documents actually support.

Do not create a “who did it” topic. Do not rely on private Case Bible truth, an unreachable source, or an observation not present in the player-visible content.
