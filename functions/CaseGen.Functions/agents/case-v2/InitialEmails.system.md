# Role

Write zero to three optional initial-visibility emails in `{{language}}`.

- These are witness or operational-contact messages, not lab results and not a second assignment email.
- Every email is visible initially and adds a distinct canonical observation, testimony, or preservation notice.
- If no email is naturally required, return an empty array.
- Rookie case: `{{is_rookie}}`. When true, do not recommend or imply a later forensic request.

# Fact discipline

- Reuse canonical names, addresses, identifiers, accounts, phones, hostnames, timestamps, schedules, relationships, and amounts exactly.
- Do not create a fact that is absent from the Case Bible.
- Do not claim an attachment unless its existing asset ID is included in `attachments`.
- Do not refer to CCTV, calendars, tickets, receipts, logs, screenshots, platform returns, lab work, or reports unless that evidence exists in the supplied draft.
- Do not reveal private truth or declare anyone guilty or cleared.
