# Role

Write the detailed player-visible card for one canonical evidence asset in `{{language}}`.

# Fixed asset contract

- Echo the supplied ID and type exactly.
- Use the exact planned layout `{{layout_hint}}`.
- The asset type is `{{asset_type}}`; structured `bodyDoc` is required: `{{must_have_body_doc}}`.
- Visibility is always `initial`.
- Preserve evidence role, subject suspect, image purpose, and all canonical identifiers.
- The short description must accurately summarize the rendered content but cannot substitute for it.

# Structured document layouts

For `pdf` and `document`, use the planned layout:

- `PoliceReport`: first-responder or supplemental police report.
- `WitnessStatement`: sworn statement with oath and signatures.
- `InterviewTranscript`: police interview/interrogation transcript.
- `AudioTranscript`: timestamped transcript of 911, dispatch, voicemail, intercom, or surveillance audio.
- `MedicalReport`: medical examiner or medical record.
- `EvidenceLog`: chain-of-custody table.
- `Memo`: TO/FROM/DATE/RE memorandum.
- `CustodyForm`: key control, cash count, evidence custody, or similar form.
- `NewspaperClipping`, `PersonalLetter`, `Receipt`, `SearchWarrant`, `DispatchLog`, `CaseMap`, or `Calendar` as planned.

For `digital`, use the planned specialized export layout: `CallLog`, `PosExport`, `PhoneDump`, `SensorLog`, `BrowserHistory`, `BankStatement`, `GpsTrack`, `FileListing`, `ChatExport`, `EmailExport`, or `AccessLog`.

An EvidenceDocument contains title, subtitle, classification, header key/value fields, and substantive sections. Use narrative, table, keyValue, transcript, code, and callout sections according to the planned layout. Anchor all dates and times to canonical chronology.

# Image assets

- `suspect_portrait`: neutral head-and-shoulders identification portrait, plain background, ordinary clothing, no accusation or mugshot board.
- `scene`: non-graphic documentary scene with useful spatial detail.
- `object`: neutral evidence-object photograph.
- `surveillance`: bounded camera angle, realistic timestamp context, occlusion, and resolution limits.

For photos, write a renderable image prompt in `body` and leave `bodyDoc` null.

# Fact materialization

- Every assigned clue must appear as a concrete player-readable observation with its exact canonical values.
- Materialize the observation statement, never the private inference.
- For non-Rookie identity shielding, identity clue A must not mention the action; action clue B must not identify the suspect or endpoint A. Only the later forensic result may map B to A.
- Materialize assigned decoy suspicion and verification observations neutrally. Never label anything a decoy, red herring, resolution, or cleared suspect.
- Do not invent a second name, timestamp, address, phone, account, hostname, amount, schedule, relationship, or identifier.
- Do not cite an attachment, screenshot, CCTV clip, appendix, database export, report, or message unless it is contained here or exists as another supplied asset.
- Do not promise future evidence or unsupported lab work.
