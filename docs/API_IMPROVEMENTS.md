# API Improvements Documentation

> **⚠️ DEPRECATED - Esta documentação foi consolidada**
> 
> Este arquivo foi substituído pela documentação completa e atualizada. Por favor, consulte:
> **[API_COMPLETE.md](./API_COMPLETE.md)** - Documentação completa da API com todas as funcionalidades e melhorias

## Redirecionamento

Esta documentação de melhorias foi integrada à documentação principal da API. O novo documento consolida:

- Todas as melhorias de segurança
- Sistema de controle de acesso baseado em rank
- Gerenciamento de visibilidade de evidências
- Processamento automatizado de casos
- Sistema de tradução em 4 idiomas

## Overview (Histórico)

This document describes the API improvements implemented for the CaseZero Alternative system, including security enhancements, rank-based access control, evidence visibility management, and case processing automation.

## New Security Features

### Rate Limiting

The API now includes comprehensive rate limiting to protect against abuse and attacks:

- **General API**: 60 requests per minute
- **Authentication endpoints**: 5 attempts per 15 minutes  
- **Login endpoint**: 3 attempts per 5 minutes

Configuration can be modified in `appsettings.json`:

```json
{
  "IpRateLimitOptions": {
    "GeneralRules": [
      {
        "Endpoint": "*",
        "Period": "1m",
        "Limit": 60
      },
      {
        "Endpoint": "*/api/auth/*",
        "Period": "15m", 
        "Limit": 5
      }
    ]
  }
}
```

### Security Headers

The following security headers are automatically added to all responses:

- `X-Frame-Options: DENY` - Prevents clickjacking attacks
- `X-Content-Type-Options: nosniff` - Prevents MIME type sniffing
- `Referrer-Policy: strict-origin-when-cross-origin` - Controls referrer information
- `X-XSS-Protection: 1; mode=block` - Enables XSS protection
- `Content-Security-Policy` - Defines content sources
- `Strict-Transport-Security` - Forces HTTPS connections

### HTTPS Enforcement

- HTTPS is enforced in production environments
- HTTP requests are automatically redirected to HTTPS
- HSTS headers ensure future connections use HTTPS

## Rank-Based Access Control

### Overview

The system now filters available cases based on the user's detective rank. Users can only access cases that match or are below their current rank level.

### Rank Hierarchy

```
Rook (0) → Detective (1) → Detective2 (2) → Sergeant (3) → Lieutenant (4) → Captain (5) → Commander (6)
```

### API Endpoints

#### Get Available Cases for User
```http
GET /api/cases
Authorization: Bearer {token}
```

Returns only cases accessible to the user's current rank.

**Response:**
```json
[
  {
    "id": "CASE-2024-001",
    "title": "Roubo no Banco Central",
    "description": "Investigação de roubo milionário no Banco Central",
    "minimumRankRequired": "Detective2",
    "difficulty": 7,
    "priority": "High"
  }
]
```

## Evidence Visibility Management

### Overview

New system allows dynamic management of evidence visibility for individual users, preparing for user-specific case instances.

### API Endpoints

#### Update Evidence Visibility
```http
POST /api/evidencevisibility/{caseId}/evidence/{evidenceId}/visibility
Authorization: Bearer {token}
Content-Type: application/json

{
  "isVisible": true,
  "reason": "Evidence unlocked after analysis completion"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Evidence visibility updated successfully",
  "caseId": "CASE-2024-001",
  "evidenceId": "EVD001",
  "isVisible": true
}
```

#### Get Visible Evidences
```http
GET /api/evidencevisibility/{caseId}/visible-evidences
Authorization: Bearer {token}
```

**Response:**
```json
{
  "caseId": "CASE-2024-001",
  "evidences": [
    {
      "id": "EVD001",
      "name": "Crime Scene Photos",
      "type": "image",
      "category": "Digital",
      "priority": "Critical",
      "isUnlocked": true
    }
  ],
  "totalCount": 5
}
```

#### Start Case Instance
```http
POST /api/evidencevisibility/{caseId}/start
Authorization: Bearer {token}
```

Creates a user-specific case instance (prepares for document database implementation).

## Enhanced Forensic Analysis

### Overview

Improved forensic analysis system with time-based result delivery and evidence type compatibility checks.

### API Endpoints

#### Get Visible Evidences for Forensics
```http
GET /api/forensic/case/{caseId}/visible-evidences
Authorization: Bearer {token}
```

Returns only evidences visible to the user for forensic analysis.

**Response:**
```json
{
  "caseId": "CASE-2024-001",
  "evidences": [
    {
      "id": "EVD003",
      "name": "Weapon Evidence",
      "type": "physical",
      "category": "Physical",
      "canAnalyze": true,
      "supportedAnalyses": ["DNA", "Fingerprint", "Trace"]
    }
  ],
  "analysableCount": 3
}
```

#### Request Forensic Analysis
```http
POST /api/forensic/case/{caseId}/evidence/{evidenceId}/analyze
Authorization: Bearer {token}
Content-Type: application/json

{
  "analysisType": "DNA",
  "notes": "Priority analysis requested"
}
```

**Successful Analysis Response:**
```json
{
  "success": true,
  "hasResult": true,
  "message": "Análise solicitada com sucesso. Resultado será entregue em breve.",
  "analysisType": "DNA",
  "estimatedDelivery": "2024-01-15T14:30:00Z",
  "responseTimeMinutes": 120,
  "resultFile": "forensics/dna_resultado.pdf"
}
```

**No Analysis Available Response:**
```json
{
  "success": true,
  "hasResult": false,
  "message": "Análise solicitada, mas não foram encontrados resultados relevantes para esta evidência.",
  "estimatedTime": "24 horas",
  "willReceiveEmail": true
}
```

### Supported Analysis Types

The system automatically determines compatible analysis types based on evidence category:

| Evidence Type | Supported Analyses |
|---------------|-------------------|
| `physical` | DNA, Fingerprint, Trace |
| `document` | HandwritingAnalysis, DocumentAuthentication |
| `digital` | DigitalForensics, MetadataAnalysis |
| `audio` | VoiceAnalysis, AudioEnhancement |
| `video` | VideoAnalysis, FacialRecognition |
| `image` | ImageAnalysis, PhotoAuthentication |

## Case Processing Automation

### Overview

Automated system to monitor the cases folder and process new cases into the database.

### Configuration

```json
{
  "CaseProcessing": {
    "Enabled": true,
    "ScanIntervalMinutes": 30,
    "UseBlobStorage": false,
    "BlobStorageUrl": ""
  }
}
```

### API Endpoints

#### Manual Process All Cases
```http
POST /api/caseprocessing/process-all
Authorization: Bearer {token}
```

Manually triggers processing of all new cases from the cases folder.

#### Process Specific Case
```http
POST /api/caseprocessing/process/{caseId}
Authorization: Bearer {token}
```

Processes a specific case by ID.

#### Check Processing Status
```http
GET /api/caseprocessing/status/{caseId}
Authorization: Bearer {token}
```

**Response:**
```json
{
  "caseId": "CASE-2024-001",
  "isProcessed": true,
  "status": "Processed",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

### Background Service

The system includes a background service that:

- Scans for new cases every 30 minutes (configurable)
- Prevents duplicate case insertion
- Logs processing status and errors
- Supports both local folder and blob storage (future)

## Translation System

### Overview

Comprehensive 4-language support system already implemented with new translations for all new features.

### Supported Languages

- **Portuguese (Brazil)** - `pt-BR` 🇧🇷 (Default)
- **English (United States)** - `en-US` 🇺🇸  
- **French (France)** - `fr-FR` 🇫🇷
- **Spanish (Spain)** - `es-ES` 🇪🇸

### New Translation Keys

The following translation keys have been added for the new features:

```typescript
// Evidence visibility and forensics
evidenceVisibility: string;
visibleEvidences: string;
forensicAnalysisTitle: string;
requestAnalysis: string;
analysisType: string;
noAnalysisAvailable: string;

// Analysis types
dnaAnalysis: string;
fingerprintAnalysis: string;
digitalForensics: string;
ballisticsAnalysis: string;

// Case processing
caseProcessing: string;
processingAllCases: string;
caseProcessed: string;

// Rank-based access
accessDenied: string;
insufficientRank: string;
rankRequired: string;
```

### Usage

The translation system uses React Context and can be accessed via the `useLanguage` hook:

```typescript
import { useLanguage } from '../contexts/LanguageContext';

const MyComponent = () => {
  const { t } = useLanguage();
  
  return (
    <button>{t('requestAnalysis')}</button>
  );
};
```

## Error Handling

### Standard Error Responses

All API endpoints follow a consistent error response format:

```json
{
  "success": false,
  "message": "Error description",
  "error": "Detailed error information",
  "statusCode": 400
}
```

### Common HTTP Status Codes

- `200 OK` - Request successful
- `400 Bad Request` - Invalid request parameters
- `401 Unauthorized` - Authentication required
- `403 Forbidden` - Insufficient permissions/rank
- `404 Not Found` - Resource not found
- `429 Too Many Requests` - Rate limit exceeded
- `500 Internal Server Error` - Server error

## Future Enhancements

### Database Migration

The current implementation prepares for migration to a document database (CosmosDB) by:

- Creating service abstractions for case access
- Implementing user-specific case instances concept
- Preparing evidence visibility state management
- Supporting both local and blob storage configurations

### Planned Features

- Complete migration to CosmosDB for non-relational storage
- Real-time evidence visibility updates via SignalR
- Advanced forensic analysis workflows
- Email notifications for analysis completion
- Case collaboration features
- Advanced security audit logging

## Conclusion

These API improvements significantly enhance the security, functionality, and scalability of the CaseZero Alternative system while maintaining backward compatibility and preparing for future architectural enhancements.