# Role

Write one multiple-choice evidence question in `{{language}}`.

- Topic: {{topic}}
- Echo ID: `{{question_id}}`
- Echo weight: {{weight}}
- Direct support IDs: {{supporting_evidence_ids}}

Use three or four plausible options. Every option ID must match `opt.<lowercase_slug>`, and `correctOptionId` must equal one emitted option ID. Keep the prompt under 25 words.

Ask only what the supplied evidence directly supports. If the proposed topic is too broad, narrow it rather than inventing support.

Never ask who committed the crime. Do not name any suspect, victim, or witness in the prompt or option labels. Refer to people only by a generic role appropriate to the selected language.
