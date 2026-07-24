# Role

Plan the complete initial evidence dossier for a `{{difficulty}}` detective case.

# Portfolio contract

- Create exactly {{portfolio_count}} assets from the supplied required dossier slots.
- Total dossier budget: {{min_assets}}-{{max_assets}}.
- Investigative asset budget: {{min_investigative_assets}}-{{max_investigative_assets}}.
- Write all player-visible titles and roles only in `{{language}}`.
- Use every required slot exactly once and echo its exact slot ID, archetype, type, evidence role, subject suspect, image purpose, and layout.
- Preserve multi-level roles: `primary`, `corroborative`, and `contextual`.
- Contextual assets complete the dossier but own no clues, decoys, or forensic inputs.
- Preserve mandatory suspect portraits, suspect interviews, scene images, and the existing difficulty-based asset budget.

# Canonical evidence ownership

- The Case Bible and investigation blueprint are immutable.
- Cover every non-forensic canonical clue through `supportsClueIds`.
- Assign every forensic clue to exactly one suitable initial asset through `forensicInputClueIds`.
- The analyzable object must actually be contained in that asset, not merely mentioned, pictured, linked, or attached elsewhere.
- `containedObjectIds` lists stable typed IDs for objects physically or digitally contained in the asset.
- Every decoy needs a concrete suspicion owner and a player-visible verification owner.
- Reuse canonical names, times, addresses, identifiers, account handles, amounts, and relationships exactly. Never create substitutes.

# Types and layouts

Allowed asset types:

- `photo`: still image rendered by the image pipeline.
- `pdf` or `document`: structured evidence document. Audio is always a transcript document.
- `digital`: structured digital export rendered as a PDF.

Allowed layouts:

- Photo: `Photo`.
- Documents: `PoliceReport`, `WitnessStatement`, `InterviewTranscript`, `AudioTranscript`, `MedicalReport`, `EvidenceLog`, `Memo`, `CustodyForm`, `NewspaperClipping`, `PersonalLetter`, `Receipt`, `SearchWarrant`, `DispatchLog`, `CaseMap`, `Calendar`.
- Digital: `CallLog`, `PosExport`, `PhoneDump`, `SensorLog`, `BrowserHistory`, `BankStatement`, `GpsTrack`, `FileListing`, `ChatExport`, `EmailExport`, `AccessLog`.

Include human-source, operational/institutional, and digital evidence when canonically plausible. Apart from mandatory per-suspect interviews and portraits, avoid using one layout more than twice. Do not plan audio or video media.
