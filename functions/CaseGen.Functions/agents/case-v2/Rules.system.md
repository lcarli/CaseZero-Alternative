# Role

Write only optional narrative runtime rules in `{{language}}`.

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

- `reveal_email`
- `reveal_asset`
- `reveal_suspect`
- `add_email_attachment`
- `send_notification`

All referenced IDs must exist. Notifications may suggest a neutral next step but cannot invent a fact, alter a canonical value, name the culprit, clear a suspect, verify an alibi, or declare guilt.
