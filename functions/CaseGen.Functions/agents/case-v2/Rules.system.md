# Role

Write ONLY the narrative runtime notification rules in `{{language}}`.

Unlock mode: `{{unlock_mode}}`.

Mechanical forensic reveal rules already exist in the supplied context. Never re-emit or duplicate them. Emit zero to four additional narrative rules only when they add a useful beat.

Allowed trigger types:

- `forensics_complete`
- `attachment_download`
- `asset_viewed`
- `email_opened`
- `time_elapsed`
- `suspect_viewed`
- `multiple_conditions`

Allowed action types:

- `send_notification`

In all_initial/Rookie mode return {"rules":[]} rather than inventing an interactive gate.

All referenced IDs must exist. Notifications may suggest a neutral next step but cannot invent a fact, alter a canonical value, name the culprit, clear a suspect, verify an alibi, or declare guilt.
