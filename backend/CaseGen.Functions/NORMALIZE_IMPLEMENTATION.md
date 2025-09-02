# Deterministic Normalize Stage Implementation

## Overview

This implementation replaces the LLM-based normalization with a deterministic, rule-based approach that validates case integrity and creates standardized outputs.

## Key Components

### 1. Service Interface & Implementation
- **INormalizerService**: Interface defining the normalize operation
- **NormalizerService**: Deterministic implementation with comprehensive validation

### 2. Input/Output Models
- **NormalizationInput**: Extended input including caseId, difficulty, timezone, planJson, expandedJson, designJson, documents[], media[]
- **NormalizationResult**: Output containing normalized bundle, manifest, and detailed logs
- **NormalizedCaseBundle**: Canonical case format with i18n support
- **CaseManifest**: File inventory with hashes and visibility rules

### 3. Validation Rules Implemented

#### ID Integrity
- Unique document IDs across the case
- Unique evidence IDs across the case
- Cross-reference validation for gating rules

#### Difficulty Compliance
- Document count validation per difficulty profile
- Evidence count validation per difficulty profile
- Gated document count validation
- Forensics reports must include "Cadeia de Custódia" section

#### Gating Graph Validation
- Cycle detection using depth-first search
- Reference integrity validation (gatingRule.evidenceId must exist)
- Node and edge generation for game engine

#### Timestamp & Timezone
- ISO-8601 timestamp normalization
- Timezone consistency validation
- CreatedAt/ModifiedAt timestamp assignment

### 4. Internationalization (i18n)
- I18nText structure supports 4 languages: pt-BR, en, es, fr
- All user-facing text fields use I18nText structure
- Ready for translation service integration

### 5. Logging & Monitoring
- Detailed step logging with timestamps
- Validation result tracking (PASS/FAIL/WARN)
- Error messages with context
- Performance metrics (duration, counts)

## Pipeline Integration

### Updated Orchestrator
The orchestrator now passes additional context to the normalize stage:
- planJson, expandedJson, designJson for rich validation
- difficulty for profile-based validation
- timezone for timestamp normalization

### Activity Model Updates
- **NormalizeActivityModel**: Extended with new required inputs
- **NormalizeActivity**: Now uses deterministic service instead of LLM

## File Structure

```
backend/CaseGen.Functions/
├── Services/
│   ├── INormalizerService.cs       # Service interface
│   ├── NormalizerService.cs        # Deterministic implementation
│   └── IServices.cs                # Updated with new interface
├── Models/
│   └── CaseGenerationModels.cs     # Extended with normalization models
├── Schemas/v1/
│   ├── NormalizedCaseBundle.schema.json  # Output bundle schema
│   └── CaseManifest.schema.json          # Manifest schema
└── Functions/
    ├── CaseGeneratorOrchestrator.cs       # Updated to pass more inputs
    └── CaseGeneratorActivities.cs         # Updated to use new service
```

## Validation Rules Summary

| Rule | Description | Status |
|------|-------------|--------|
| UNIQUE_IDS | Document and evidence IDs must be unique | ✅ Implemented |
| GATING_REFERENCE_INTEGRITY | Gating rules must reference existing IDs | ✅ Implemented |
| DIFFICULTY_VALIDATION | Document/evidence counts per difficulty | ✅ Implemented |
| GATING_GRAPH_CYCLES | No cycles in unlock dependencies | ✅ Implemented |
| FORENSICS_CUSTODY_CHAIN | Forensics reports include custody section | ✅ Implemented |
| ISO8601_TIMESTAMPS | Valid timestamp format validation | ✅ Implemented |
| TIMEZONE_CONSISTENCY | Timezone format validation | ✅ Implemented |

## Benefits

1. **Deterministic**: No LLM variability, consistent outputs
2. **Fast**: No API calls, immediate processing
3. **Validated**: Comprehensive integrity checking
4. **Traceable**: Detailed logging and validation results
5. **Scalable**: No rate limits or token costs
6. **Maintainable**: Clear validation rules and error messages

## Testing

- Integration tests verify model structure and serialization
- Unit tests validate specific validation rules
- End-to-end compatibility with existing pipeline

## Backward Compatibility

- Existing ICaseGenerationService.NormalizeCaseAsync() maintained for compatibility
- New NormalizeCaseDeterministicAsync() method available
- Pipeline updated to use new deterministic approach by default