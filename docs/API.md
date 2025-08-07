# API Documentation - CaseZero System

## Overview

CaseZero API é uma REST API construída em .NET 8 Core que fornece todos os endpoints necessários para o sistema de investigação detetivesca. A API utiliza autenticação JWT e oferece funcionalidades completas para gerenciamento de usuários, casos, evidências e análises forenses.

## Base URL

```
http://localhost:5000/api
```

## Autenticação

A API utiliza JWT (JSON Web Tokens) para autenticação. Todos os endpoints protegidos requerem o header:

```
Authorization: Bearer <token>
```

---

## Endpoints de Autenticação

### POST /api/auth/register
**Descrição:** Registra um novo usuário no sistema com verificação por email

**Body:**
```json
{
  "firstName": "João",
  "lastName": "Silva",
  "personalEmail": "joao.silva@gmail.com",
  "password": "Password123!"
}
```

**Response (200):**
```json
{
  "message": "Registro realizado com sucesso! Verifique seu email pessoal para ativar a conta.",
  "policeEmail": "joao.silva@fic-police.gov",
  "personalEmail": "joao.silva@gmail.com"
}
```

### POST /api/auth/verify-email
**Descrição:** Verifica o email do usuário através do token enviado

**Body:**
```json
{
  "token": "verification-token-here"
}
```

**Response (200):**
```json
{
  "message": "Email verificado com sucesso! Sua conta está ativa."
}
```

### POST /api/auth/resend-verification
**Descrição:** Reenvia email de verificação

**Body:**
```json
{
  "email": "joao.silva@fic-police.gov"
}
```

**Response (200):**
```json
{
  "message": "Novo email de verificação enviado."
}
```

### POST /api/auth/login
**Descrição:** Autentica um usuário e retorna JWT token

**Body:**
```json
{
  "email": "joao.silva@fic-police.gov",
  "password": "Password123!"
}
```

**Response (200):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "user": {
    "id": "guid-here",
    "email": "joao.silva@fic-police.gov",
    "personalEmail": "joao.silva@gmail.com",
    "firstName": "João",
    "lastName": "Silva",
    "department": "ColdCase",
    "position": "rook",
    "badgeNumber": "1234",
    "emailVerified": true
  },
  "expiresAt": "2024-01-15T10:30:00Z"
}
```

### POST /api/auth/logout
**Descrição:** Realiza logout do usuário
**Headers:** Authorization Bearer Token Required

**Response (200):**
```json
{
  "message": "Logged out successfully"
}
```

---

## Endpoints de Casos (CaseObject System)

### GET /api/caseobject
**Descrição:** Lista todos os casos disponíveis
**Headers:** Authorization Bearer Token Required

**Response (200):**
```json
[
  {
    "caseId": "CASE-2024-001",
    "title": "Homicídio no Edifício Corporativo",
    "description": "Investigação de assassinato em ambiente corporativo",
    "difficulty": "Medium",
    "estimatedTime": "120 minutes",
    "category": "Homicide",
    "createdDate": "2024-01-01T00:00:00Z"
  }
]
```

### GET /api/caseobject/{caseId}
**Descrição:** Carrega dados completos de um caso específico
**Headers:** Authorization Bearer Token Required

**Parameters:**
- `caseId` (string): ID do caso (ex: "CASE-2024-001")

**Response (200):**
```json
{
  "caseId": "CASE-2024-001",
  "metadata": {
    "title": "Homicídio no Edifício Corporativo",
    "description": "Investigação completa de assassinato",
    "difficulty": "Medium",
    "estimatedTime": "120 minutes",
    "category": "Homicide",
    "tags": ["murder", "corporate", "investigation"]
  },
  "evidences": [
    {
      "id": "evidence_001",
      "name": "Câmera de Segurança - Entrada Principal",
      "type": "video",
      "description": "Gravação do horário do crime",
      "filePath": "evidence/camera_entrance.mp4",
      "unlockRequirements": [],
      "metadata": {
        "timestamp": "2024-01-10T14:30:00Z",
        "location": "Entrance Hall",
        "quality": "HD"
      }
    }
  ],
  "suspects": [
    {
      "id": "suspect_001",
      "name": "Marina Silva",
      "age": 34,
      "occupation": "Gerente de Vendas",
      "description": "Funcionária há 5 anos, conhecida por conflitos com a vítima",
      "filePath": "suspects/marina_silva.txt",
      "alibi": "Estava em reunião durante o horário do crime",
      "motive": "Conflitos profissionais recentes",
      "unlockRequirements": []
    }
  ],
  "forensicAnalyses": [
    {
      "id": "forensic_001",
      "name": "Análise de DNA",
      "type": "dna",
      "description": "Análise de material genético encontrado na cena",
      "duration": 180,
      "unlockRequirements": ["evidence_003"],
      "resultPath": "forensics/dna_resultado.pdf"
    }
  ],
  "timeline": [
    {
      "time": "14:00",
      "event": "Vítima chega ao escritório",
      "evidence": ["evidence_001"],
      "verified": true
    }
  ]
}
```

### GET /api/caseobject/{caseId}/validate
**Descrição:** Valida a estrutura e integridade de um caso
**Headers:** Authorization Bearer Token Required

**Response (200):**
```json
{
  "isValid": true,
  "validationErrors": [],
  "warnings": [],
  "summary": {
    "evidencesCount": 6,
    "suspectsCount": 3,
    "forensicAnalysesCount": 4,
    "timelineEventsCount": 8
  }
}
```

---

## Endpoints de Sessão de Caso

### POST /api/casesession/start
**Descrição:** Inicia uma nova sessão de investigação
**Headers:** Authorization Bearer Token Required

**Body:**
```json
{
  "caseId": "CASE-2024-001"
}
```

**Response (201):**
```json
{
  "sessionId": "session-guid-here",
  "caseId": "CASE-2024-001",
  "startTime": "2024-01-15T10:00:00Z",
  "status": "Active",
  "progress": {
    "evidencesUnlocked": 1,
    "totalEvidences": 6,
    "suspectsInterviewed": 0,
    "forensicAnalysesCompleted": 0
  }
}
```

### GET /api/casesession/{sessionId}
**Descrição:** Obtém status atual da sessão
**Headers:** Authorization Bearer Token Required

**Response (200):**
```json
{
  "sessionId": "session-guid-here",
  "caseId": "CASE-2024-001",
  "startTime": "2024-01-15T10:00:00Z",
  "currentTime": "2024-01-15T10:45:00Z",
  "status": "Active",
  "progress": {
    "evidencesUnlocked": 3,
    "totalEvidences": 6,
    "suspectsInterviewed": 1,
    "forensicAnalysesCompleted": 2,
    "timelineProgress": 60
  },
  "unlockedContent": {
    "evidences": ["evidence_001", "evidence_002", "evidence_003"],
    "suspects": ["suspect_001"],
    "forensics": ["forensic_001", "forensic_002"]
  }
}
```

---

## Endpoints de Evidências

### GET /api/evidence/{sessionId}/{evidenceId}
**Descrição:** Obtém arquivo de evidência específica
**Headers:** Authorization Bearer Token Required

**Response (200):** Retorna o arquivo da evidência (PDF, imagem, vídeo, etc.)

### POST /api/evidence/{sessionId}/{evidenceId}/analyze
**Descrição:** Solicita análise forense de uma evidência
**Headers:** Authorization Bearer Token Required

**Response (202):**
```json
{
  "analysisId": "analysis-guid-here",
  "estimatedCompletionTime": "2024-01-15T13:00:00Z",
  "status": "InProgress"
}
```

---

## Endpoints de Análises Forenses

### GET /api/forensic/{sessionId}
**Descrição:** Lista todas as análises forenses da sessão
**Headers:** Authorization Bearer Token Required

**Response (200):**
```json
[
  {
    "id": "forensic_001",
    "name": "Análise de DNA",
    "type": "dna",
    "status": "Completed",
    "requestedAt": "2024-01-15T10:15:00Z",
    "completedAt": "2024-01-15T13:15:00Z",
    "resultAvailable": true
  }
]
```

### GET /api/forensic/{sessionId}/{forensicId}
**Descrição:** Obtém resultado de análise forense específica
**Headers:** Authorization Bearer Token Required

**Response (200):** Retorna o arquivo de resultado da análise

---

## Endpoints de Submissão de Caso

### POST /api/casesubmission/{sessionId}
**Descrição:** Submete solução final do caso
**Headers:** Authorization Bearer Token Required

**Body:**
```json
{
  "primarySuspect": "suspect_002",
  "motive": "Vingança por demissão",
  "evidenceChain": [
    "evidence_003",
    "forensic_002",
    "evidence_005"
  ],
  "timeline": [
    {
      "time": "14:30",
      "event": "Suspeito entra no edifício",
      "evidence": "evidence_001"
    }
  ],
  "conclusion": "Baseado nas evidências coletadas..."
}
```

**Response (200):**
```json
{
  "submissionId": "submission-guid-here",
  "score": 85,
  "correctSuspect": true,
  "correctMotive": true,
  "evidenceScore": 80,
  "timelineAccuracy": 90,
  "feedback": {
    "strengths": ["Identificação correta do suspeito", "Uso eficiente das evidências"],
    "improvements": ["Poderia ter explorado mais o motivo financeiro"]
  },
  "rank": "Detective First Class",
  "timeToSolve": "02:15:30"
}
```

---

## Endpoints de Email

### GET /api/email/{sessionId}
**Descrição:** Lista emails/mensagens da sessão
**Headers:** Authorization Bearer Token Required

**Response (200):**
```json
[
  {
    "id": "email_001",
    "from": "chief@police.gov",
    "subject": "Novo caso atribuído",
    "receivedAt": "2024-01-15T10:00:00Z",
    "isRead": false,
    "priority": "High"
  }
]
```

### GET /api/email/{sessionId}/{emailId}
**Descrição:** Obtém conteúdo completo de um email
**Headers:** Authorization Bearer Token Required

**Response (200):**
```json
{
  "id": "email_001",
  "from": "chief@police.gov",
  "subject": "Novo caso atribuído",
  "content": "Detective, você foi designado para investigar...",
  "receivedAt": "2024-01-15T10:00:00Z",
  "attachments": [
    {
      "name": "initial_report.pdf",
      "size": "245KB",
      "downloadUrl": "/api/email/session-id/email_001/attachment/initial_report.pdf"
    }
  ]
}
```

---

## Códigos de Status HTTP

| Código | Descrição |
|--------|-----------|
| 200 | OK - Requisição bem-sucedida |
| 201 | Created - Recurso criado com sucesso |
| 202 | Accepted - Requisição aceita para processamento |
| 400 | Bad Request - Dados de entrada inválidos |
| 401 | Unauthorized - Token de autenticação inválido ou ausente |
| 403 | Forbidden - Usuário não tem permissão |
| 404 | Not Found - Recurso não encontrado |
| 409 | Conflict - Conflito de dados (ex: email já existe) |
| 500 | Internal Server Error - Erro interno do servidor |

---

## Modelos de Dados

### User
```json
{
  "id": "string (guid)",
  "email": "string",
  "firstName": "string",
  "lastName": "string",
  "phoneNumber": "string",
  "department": "string",
  "position": "string",
  "badgeNumber": "string",
  "rank": "string",
  "isApproved": "boolean",
  "createdAt": "datetime",
  "lastLoginAt": "datetime"
}
```

### Case Metadata
```json
{
  "caseId": "string",
  "title": "string",
  "description": "string",
  "difficulty": "Easy|Medium|Hard|Expert",
  "estimatedTime": "string",
  "category": "string",
  "tags": ["string"],
  "createdDate": "datetime",
  "lastModified": "datetime"
}
```

### Evidence
```json
{
  "id": "string",
  "name": "string",
  "type": "document|photo|video|audio|digital|physical",
  "description": "string",
  "filePath": "string",
  "unlockRequirements": ["string"],
  "metadata": "object"
}
```

---

## Rate Limiting

A API implementa rate limiting para prevenir abuso:

- **Autenticação:** 5 tentativas por minuto por IP
- **Endpoints gerais:** 100 requisições por minuto por usuário
- **Upload de arquivos:** 10 uploads por minuto por usuário

---

## Versionamento

A API utiliza versionamento via URL path:
- Versão atual: `/api/v1/`
- Versão de desenvolvimento: `/api/v2/` (quando aplicável)

---

## WebSockets (Eventos em Tempo Real)

Para atualizações em tempo real durante investigações:

**Endpoint:** `ws://localhost:5000/casehub`

**Eventos enviados:**
- `evidence_unlocked`
- `forensic_completed`
- `new_message`
- `time_update`

**Formato do evento:**
```json
{
  "type": "evidence_unlocked",
  "sessionId": "session-guid",
  "data": {
    "evidenceId": "evidence_003",
    "name": "Documento Financeiro"
  },
  "timestamp": "2024-01-15T10:30:00Z"
}
```