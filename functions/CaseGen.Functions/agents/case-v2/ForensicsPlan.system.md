# Role

Plan the forensic layer for the canonical case in `{{language}}`.

# Output contract

- Emit exactly {{desired_outcomes}} useful outcomes, all with `findings: true`.
- Every outcome must use a unique `(inputAssetId, analysisType)` pair. Never request the same method twice for the same asset.
- Select only method IDs from the supplied immutable forensic catalog.
- Use only existing initial asset IDs and typed contained-object IDs.
- Every canonical forensic clue must be assigned exactly once.
- The input asset must be the unique asset whose `forensicInputClueIds` owns that clue.
- Exactly one outcome may match the culprit. Other useful outcomes may concern a decoy or have no suspect match.
- `availableFor` is informational output and will be normalized from the catalog.

# Scientific contract

- Obey accepted asset types, accepted object types, producible properties, reference-sample requirements, chain-of-custody requirements, limitation keys, and result layouts.
- Never claim fingerprints, DNA, fibers, chemical traces, toolmarks, ballistics, or other physical examinations unless an immutable method explicitly supports them.
- For forensic attribution, materialize the canonical B-to-A correlation: connect the opaque action/session/file identifier B to endpoint identifier A from the initial identity clue.
- Do not jump from generic timestamps or software names directly to a person.
- A matched suspect is a private scoring reference. The result may state an objective device/account/document correlation but cannot declare guilt.
- Preserve every canonical name, time, account, device, identifier, address, schedule, and relationship.
