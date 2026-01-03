# Forensic Systems Log Review Report: Printer and Access/Patrol Timestamp Correlation

## Laboratory and Case Information

**Laboratory:** Digital Forensics Unit (DFU)

**Case Reference:** Internal incident review (systems log correlation)

**Report Date Created:** 2026-01-03T11:05:00-05:00

**Examination Window:** Log entries relevant to incident timeline events (see timeline references E002, E003, E005, E006, E007)

**Purpose:** Review and correlate printer activity logs with access control and patrol timestamp records to determine whether recorded system events align in time, and to identify any inconsistencies requiring follow-up. Evidence reviewed includes EV002, EV003, EV006, EV007, and EV008.

## Examiner Details

**Examiner:** DFU Analyst (initials on file)

**Role:** Digital log review and timestamp correlation

**Tools/Environment:** Read-only review workstation; hashing utility; log parsing/viewing utilities (standard unit tools).

**Handling Notes:** All evidence was reviewed in a read-only manner. No changes were made to source data.

## Evidence Description

- **EV002:** Printer-related system logs/spool records for the relevant device(s), including job submission and completion timestamps.
- **EV003:** Access control system log exports (badge/door events) for relevant doors during the incident window.
- **EV006:** Patrol or guard-tour timestamp records used to document rounds/observations.
- **EV007:** System time configuration references (e.g., device clock settings, NTP status, or time source indicators) used to evaluate timestamp reliability.
- **EV008:** Supplemental system logs (e.g., host/event logs) used to confirm event timing and sequence.

**Related document reference:** This report is **doc_forensics_002**. No other document content was modified or relied upon as a source record.

## Methodology

1. **Intake verification:** Confirmed evidence identifiers and ensured datasets were opened in read-only mode (EV002, EV003, EV006, EV007, EV008).
2. **Time format normalization:** Converted displayed times to a common ISO-8601 representation with offset **-05:00** for comparison. Where logs stored UTC or local time, the source format was noted and then normalized.
3. **Clock reliability check:** Reviewed EV007 for indicators of time synchronization status and any manual time changes.
4. **Event extraction:** Pulled relevant entries from EV002 (print job submit/start/end), EV003 (access events), and EV006 (patrol checkpoints or round timestamps). Extracted adjacent context from EV008 to confirm system activity near those times.
5. **Correlation:** Built a simple chronological list grouped by source (printer/access/patrol) and then aligned events by nearest timestamps.
6. **Consistency review:** Looked for gaps, overlaps, duplicate entries, or sequences that were not plausible given typical system behavior (e.g., print completion preceding submission).

## Results

- **General timestamp alignment:** Across EV002, EV003, and EV006, the majority of compared entries fell into a consistent chronological sequence once normalized to **-05:00**.
- **Printer log sequencing:** EV002 contained print-job records with clear ordering (submission → processing → completion). No internal sequencing errors were observed in the extracted set.
- **Access log continuity:** EV003 presented door/access events in chronological order with no obvious missing blocks in the exported range reviewed.
- **Patrol record continuity:** EV006 showed patrol timestamps consistent with a routine sequence of checks. No duplicated timestamp blocks were noted in the segment reviewed.
- **Time-source considerations:** EV007 indicated time configuration information sufficient to support basic correlation. No definitive, recorded evidence of a manual time rollback/fast-forward was identified in the reviewed segment.
- **Cross-source correlation:** When aligned against timeline reference points (E002, E003, E005, E006, E007), the printer activity in EV002 and supporting events in EV008 were generally time-consistent with access/patrol activity in EV003/EV006. Any small offsets observed were within what can occur between separate systems that do not share perfectly synchronized clocks.

## Interpretation

The reviewed log sources (EV002, EV003, EV006, with time context from EV007 and EV008) support a **coherent time-ordered record** for the examined window. Correlation suggests that printer events can be compared to access and patrol events with reasonable confidence after time normalization.

This examination **does not identify a single definitive narrative** on its own. Instead, it provides a structured basis to:
- Confirm whether specific printed outputs could have occurred near specific access or patrol timestamps, and
- Flag any later-discovered time discrepancies for deeper validation (e.g., additional time server logs or original raw exports).

No conclusion is offered here regarding responsibility or intent. The results are limited to timestamp consistency and cross-system correlation.

## Chain of Custody

- **2026-01-03T11:10:00-05:00:** EV002, EV003, EV006, EV007, and EV008 received by DFU evidence intake from secure storage; seals/containers verified intact.
- **2026-01-03T11:18:00-05:00:** Evidence transferred to DFU Analyst for examination on read-only workstation; intake log updated.
- **2026-01-03T12:05:00-05:00:** Examination completed; working notes saved to case file under **doc_forensics_002**; no changes made to source evidence.
- **2026-01-03T12:12:00-05:00:** EV002, EV003, EV006, EV007, and EV008 returned to secure storage; custody record updated and closed for this handling cycle.

## Limitations

- This review is limited to the provided evidence sets (EV002, EV003, EV006, EV007, EV008). If source systems generated additional logs not provided, those could affect correlation.
- Separate systems may have **minor clock drift**. Normalization reduces but may not eliminate small offsets.
- Exported logs may omit fields present in original systems. This can limit the ability to confirm user/session context beyond timestamp and event type.
- This report addresses **timing and sequence only**; it does not determine who initiated events or why.

