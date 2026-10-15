# PDF Document Templates - Police Case Files

## Overview

This file now reflects the **current** CaseZero codebase.

The repository has **two QuestPDF-based rendering paths**:

1. **Preferred v2 path** — structured `EvidenceDocument` payloads rendered by `AssetRenderingService` → `EvidenceTemplateRegistry` → `EvidenceDocumentRenderer` → `IDocumentLayout`.
2. **Legacy fallback path** — markdown/body-only assets rendered by `PdfRenderingService` and the older `Services\Pdf\Templates\*` classes.

There is **no PdfSharp implementation** in the current codebase.

## Current implementation snapshot

- **PDF library:** `QuestPDF` `2025.7.1` (`functions\CaseGen.Functions\CaseGen.Functions.csproj`)
- **Preferred model:** `functions\CaseGen.Functions\Models\CaseV2\EvidenceDocumentModels.cs`
- **Preferred renderer:** `functions\CaseGen.Functions\Services\CaseV2\EvidenceDocumentRenderer.cs`
- **Layout registry:** `functions\CaseGen.Functions\Services\CaseV2\Rendering\DocumentLayoutRegistry.cs`
- **Embedded resources:** `brasao.png`, `Lora`, `Source Serif 4`, `Inter`, `JetBrains Mono`
- **Supported renderer languages:** `en-US`, `pt-BR`, `es-ES`, `fr-FR`

## Rendering architecture

```text
AssetRenderingService
├─ asset.type = pdf | document | digital
├─ if asset.bodyDoc exists
│  ├─ EvidenceTemplateRegistry.Apply()
│  └─ EvidenceDocumentRenderer.Render()
│     └─ IDocumentLayoutRegistry.Resolve(doc.Layout)
│        └─ concrete QuestPDF layout
└─ else
   └─ PdfRenderingService.GenerateTestPdfAsync()
      └─ legacy doc-type routing / generic fallback
```

### Structured document contract used by the v2 renderer

`EvidenceDocument` supports:

- `layout` — visual family selector (`PoliceReport`, `EvidenceLog`, `Memo`, etc.)
- `language` — chrome localization for all 4 supported UI languages
- `title`, `subtitle`, `classification`
- `header[]` — key/value metadata rows
- `sections[]` — `narrative`, `table`, `keyValue`, `transcript`, `code`, `callout`
- `signature` — optional signed footer block

Unknown layout ids fall back to `GeneralReport` instead of throwing.

## Registered v2 layouts (current authoritative list)

| Layout | Primary use | Current behavior |
|---|---|---|
| `GeneralReport` | Generic narrative documents | Default fallback for unknown layouts |
| `PoliceReport` | Initial / supplemental police reports | Brasão header, confidential band, split metadata block, numbered sections, officer signature |
| `WitnessStatement` | Sworn witness statements | `STATEMENT OF` headline, oath block, witness + officer signatures |
| `InterviewTranscript` | Interview / questioning transcripts | Navy transcript header, transcript-friendly section rendering |
| `AudioTranscript` | Dispatch, voicemail, surveillance or recorded-audio transcripts | Separate audio-specific chrome from interview transcripts |
| `ForensicReport` | Lab / examination reports | Lab letterhead, integrity banner, findings/conclusions styling |
| `MedicalReport` | Medical examiner / urgent-care style records | Distinct sepia palette and medical header |
| `EvidenceLog` | Chain-of-custody / evidence logs | **Landscape A4** layout for wide custody tables |
| `Memo` | Internal memoranda | TO/FROM/DATE/RE extraction from header labels/synonyms |
| `CustodyForm` | Briefing sheets / sign-out / accountability forms | FORM stamp, underlined fields, multi-signature footer |
| `NewspaperClipping` | Press clippings | Newsprint palette and centered headline treatment |
| `PersonalLetter` | Letters / handwritten-style correspondence | Serif body and signed closing |
| `Receipt` | Receipts / small slips | A5 mono-styled receipt layout |
| `SearchWarrant` | Warrant / court-style paperwork | Framed legal header and signature block |
| `DispatchLog` | CAD / dispatch-style logs | Monospace operational log treatment |
| `CaseMap` | Visual map / relationship boards | Landscape case-map canvas styling |
| `Calendar` | Calendar / schedule artifacts | Calendar-specific chrome and accent styling |

## Layout enrichment templates applied before rendering

These templates **do not render PDFs themselves**; they normalize section data before the generic renderer runs.

| Template | Purpose |
|---|---|
| `CallLogTemplate` | Telecom CDR ordering + totals |
| `PosExportTemplate` | POS transaction ordering + raw totals |
| `PhoneDumpTemplate` | Mobile extraction metadata + extraction-summary callout |
| `SensorLogTemplate` | IoT / sensor event ordering |
| `BrowserHistoryTemplate` | Browser-history normalization |
| `BankStatementTemplate` | Debit / credit totals |
| `GpsTrackTemplate` | GPS metadata + WGS-84 footnote |
| `FileListingTemplate` | Listing summary |
| `ChatExportTemplate` | Converts message tables into transcript sections |
| `EmailExportTemplate` | Email-export metadata |
| `AccessLogTemplate` | Access-log ordering |
| `AudioTranscriptTemplate` | Audio-transcript enrichment |

## Legacy fallback routing still present in `PdfRenderingService`

When an asset has no `bodyDoc`, the older renderer still recognizes these document-type aliases:

| Legacy document type(s) | Legacy template |
|---|---|
| `suspect_profile`, `witness_profile` | `SuspectProfileTemplate` |
| `evidence_log`, `evidence_catalog` | `EvidenceLogTemplate` |
| `forensics_report`, `lab_report` | `ForensicsReportTemplate` |
| `interview`, `interview_transcript` | `InterviewTranscriptTemplate` |
| `memo`, `memo_admin`, `internal_memo` | `MemoTemplate` |
| `witness_statement`, `statement` | `WitnessStatementTemplate` |
| `police_report`, `incident_report` | `PoliceReportTemplate` |
| anything else | generic letterhead + watermark fallback |

Important: the **v2 generation pipeline prefers `layout` + `bodyDoc`**, not these older `documentType` ids.

## Shared resources actually used by the renderer

Embedded from `functions\CaseGen.Functions\Resources\*`:

- `Images\brasao.png`
- `Fonts\Lora-Regular.ttf`
- `Fonts\Lora-Italic.ttf`
- `Fonts\SourceSerif4-Regular.ttf`
- `Fonts\SourceSerif4-Italic.ttf`
- `Fonts\Inter-Regular.ttf`
- `Fonts\JetBrainsMono-Regular.ttf`

`DocumentResources.EnsureInitialized()` registers the QuestPDF community license and these embedded fonts before rendering.

## Public schema note

The public case schema (`functions\CaseGen.Functions\Schemas\case.v2.schema.json`) validates final asset metadata such as `type`, `title`, `uri`, `visibility`, `category`, `evidenceRole`, `subjectSuspectId`, and `imagePurpose`.

The internal `bodyDoc` / layout-shaping model is currently defined in C# (`EvidenceDocumentModels.cs`) rather than exposed in the public case schema.

## Test coverage tied to this rendering stack

- `DocumentLayoutSmokeTests` — validates all registered layouts and all 4 languages
- `AssetRenderingTests` — validates image retries, file extensions, and `bodyDoc` fallback behavior

## Status summary

The current codebase no longer has a 7-of-12 roadmap. The authoritative state is:

- **17 registered v2 layouts** in the main renderer
- **12 enrichment templates** for digital/document evidence shaping
- **legacy QuestPDF fallback templates** still available for body-only assets
- **4 fully localized renderer chrome dictionaries** shared across PDF output
