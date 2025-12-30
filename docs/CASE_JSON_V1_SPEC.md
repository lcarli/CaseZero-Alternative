# 📋 Case JSON v1.0 - Especificação Completa

> **Versão:** 1.0  
> **Data:** Dezembro 2025  
> **Status:** Draft  
> **Branch:** V3-2026

## 🎯 Visão Geral

O **case.json v1.0** é o novo modelo de dados para casos investigativos no CaseZero. Esta versão introduz:
- Sistema de **visibilidade** (initial/hidden) substituindo unlock logic complexo
- **Emails dinâmicos** gerados por IA integrados ao fluxo
- **Rules engine** server-side para progressão narrativa
- **Forensics defaults** para análises padrão

---

## 📐 Estrutura Raiz

```json
{
  "version": "1.0",
  "caseId": "case_001",
  "metadata": { ... },
  "assets": [ ... ],
  "emails": [ ... ],
  "suspects": [ ... ],
  "rules": [ ... ],
  "forensicsDefaults": { ... }
}
```

### Campos Raiz

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `version` | string | ✅ | Versão do schema (sempre "1.0") |
| `caseId` | string | ✅ | ID único do caso (formato: `case_nnn`) |
| `metadata` | object | ✅ | Metadados do caso (título, dificuldade, etc.) |
| `assets` | array | ✅ | Lista de assets (evidências, documentos, mídia) |
| `emails` | array | ✅ | Lista de emails (briefings, updates, resultados) |
| `suspects` | array | ✅ | Lista de suspeitos |
| `rules` | array | ✅ | Regras de progressão (server-side APENAS) |
| `forensicsDefaults` | object | ✅ | Configurações padrão de análises forenses |

---

## 🏷️ Metadata

```json
{
  "metadata": {
    "title": "The Missing Heir",
    "description": "Investigate the disappearance of a wealthy businessman's son",
    "difficulty": "Medium",
    "estimatedTimeMinutes": 90,
    "requiredRank": "Detective",
    "location": "Portland, Oregon",
    "incidentDate": "2025-12-15T14:30:00Z",
    "category": "Missing Person",
    "briefing": "You've been assigned to investigate the sudden disappearance of...",
    "victim": {
      "name": "Thomas Blackwood Jr.",
      "age": 28,
      "occupation": "Software Engineer",
      "lastSeen": "2025-12-15T14:30:00Z"
    }
  }
}
```

### Campos de Metadata

| Campo | Tipo | Obrigatório | Valores | Descrição |
|-------|------|-------------|---------|-----------|
| `title` | string | ✅ | - | Título do caso |
| `description` | string | ✅ | - | Descrição curta (1-2 frases) |
| `difficulty` | number | ✅ | 1-10 | Difficulty level (1=easiest, 10=hardest) |
| `estimatedTimeMinutes` | number | ✅ | 30-240 | Tempo estimado em minutos reais |
| `requiredRank` | string | ✅ | Cadet, Junior Detective, Detective, Senior Detective, Lead Detective, Chief Inspector, Legendary Detective | Rank mínimo necessário (GDD) |
| `location` | string | ✅ | - | Cidade/estado do caso |
| `incidentDate` | string | ✅ | ISO 8601 | Data/hora do incidente |
| `category` | string | ✅ | Murder, Theft, Missing Person, Fraud, Assault | Categoria do crime |
| `briefing` | string | ✅ | - | Texto do briefing inicial (200-500 palavras) |
| `victim` | object | ⚠️ | - | Informações da vítima (obrigatório para Murder/Missing Person) |

---

## 📦 Assets

Assets substituem "evidences" do modelo antigo. Incluem evidências físicas, documentos, mídia e qualquer arquivo acessível.

```json
{
  "assets": [
    {
      "assetId": "asset.briefing_doc",
      "name": "Initial Case Briefing",
      "type": "document",
      "category": "Document",
      "description": "Official case assignment from Chief of Police",
      "filePath": "/assets/briefing.pdf",
      "visibility": "initial",
      "metadata": {
        "pages": 2,
        "fileSize": "245KB",
        "format": "PDF"
      }
    },
    {
      "assetId": "asset.cctv_footage",
      "name": "Security Camera Footage",
      "type": "video",
      "category": "Digital",
      "description": "Lobby camera recording from incident time",
      "filePath": "/assets/cctv_lobby_20251215.mp4",
      "visibility": "hidden",
      "metadata": {
        "duration": "00:45:30",
        "resolution": "1080p",
        "timestamp": "2025-12-15T14:00:00Z"
      }
    }
  ]
}
```

### Campos de Asset

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `assetId` | string | ✅ | ID único (formato: `asset.nome_descritivo`) |
| `name` | string | ✅ | Nome amigável do asset |
| `type` | string | ✅ | document, image, video, audio, physical, digital |
| `category` | string | ✅ | Document, Digital, Physical, Biological, Communication, Technical |
| `description` | string | ✅ | Descrição detalhada (1-2 frases) |
| `filePath` | string | ✅ | Caminho relativo no Blob Storage |
| `visibility` | string | ✅ | **initial** (visível desde o início) ou **hidden** (revelado por regras) |
| `metadata` | object | ❌ | Metadados específicos do tipo (páginas, duração, etc.) |

### Convenção de IDs de Assets

```
asset.briefing_doc          ✅ Bom (descritivo, snake_case)
asset.cctv_001              ✅ Bom (numeração clara)
asset.witness_statement_1   ✅ Bom (contexto + número)
asset.e001                  ❌ Evitar (não descritivo)
asset.Evidence-001          ❌ Evitar (kebab-case, maiúscula)
```

---

## 📧 Emails

Emails são a principal forma de comunicação narrativa. Podem ser iniciais (briefing do chefe) ou revelados dinamicamente (resultados forenses, updates).

```json
{
  "emails": [
    {
      "emailId": "email.briefing_001",
      "from": "Chief Sarah Mitchell <chief@citypolice.gov>",
      "to": "Detective Alex Morgan <detective@citypolice.gov>",
      "subject": "URGENT: Missing Person Case Assignment",
      "sentAt": "2025-12-15T08:00:00Z",
      "priority": "high",
      "visibility": "initial",
      "content": "Detective Morgan,\n\nYou've been assigned to investigate...",
      "attachments": [
        "asset.briefing_doc",
        "asset.victim_photo"
      ],
      "metadata": {
        "thread": "case_assignment",
        "importance": "urgent"
      }
    },
    {
      "emailId": "email.forensics_dna_result",
      "from": "Forensics Lab <lab@citypolice.gov>",
      "to": "Detective Alex Morgan <detective@citypolice.gov>",
      "subject": "DNA Analysis Results - Case #2025-1215",
      "sentAt": "2025-12-15T16:30:00Z",
      "priority": "normal",
      "visibility": "hidden",
      "content": "Detective,\n\nWe have completed the DNA analysis...",
      "attachments": [
        "asset.dna_report"
      ],
      "metadata": {
        "thread": "forensics_results",
        "analysisType": "DNA"
      }
    }
  ]
}
```

### Campos de Email

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `emailId` | string | ✅ | ID único (formato: `email.nome_descritivo`) |
| `from` | string | ✅ | Remetente (formato: "Nome <email@domain>") |
| `to` | string | ✅ | Destinatário (formato: "Nome <email@domain>") |
| `subject` | string | ✅ | Assunto do email |
| `sentAt` | string | ✅ | Timestamp ISO 8601 (quando foi "enviado" no jogo) |
| `priority` | string | ✅ | normal, high, urgent |
| `visibility` | string | ✅ | **initial** ou **hidden** |
| `content` | string | ✅ | Corpo do email (texto plano ou markdown) |
| `attachments` | array | ❌ | Array de `assetId`s anexados |
| `metadata` | object | ❌ | Metadados adicionais (thread, tags, etc.) |

### Convenção de IDs de Emails

```
email.briefing_001           ✅ Bom
email.forensics_dna_result   ✅ Bom
email.chief_update_002       ✅ Bom
email.e001                   ❌ Evitar
```

---

## 👤 Suspects

Suspeitos mantêm estrutura similar ao modelo antigo, mas com simplificações.

```json
{
  "suspects": [
    {
      "suspectId": "suspect.thomas_blackwood_sr",
      "name": "Thomas Blackwood Sr.",
      "age": 58,
      "occupation": "CEO, Blackwood Industries",
      "relationship": "Father of victim",
      "motive": "Financial disputes over company succession",
      "alibi": "Claims to have been at company board meeting",
      "alibiVerified": false,
      "background": "Wealthy businessman, known for aggressive business tactics...",
      "linkedAssets": [
        "asset.financial_records",
        "asset.security_footage"
      ],
      "visibility": "initial"
    }
  ]
}
```

### Campos de Suspect

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `suspectId` | string | ✅ | ID único (formato: `suspect.nome_completo`) |
| `name` | string | ✅ | Nome completo |
| `age` | number | ✅ | Idade |
| `occupation` | string | ✅ | Profissão/ocupação |
| `relationship` | string | ✅ | Relação com vítima/caso |
| `motive` | string | ✅ | Possível motivo |
| `alibi` | string | ✅ | Álibi alegado |
| `alibiVerified` | boolean | ✅ | Se o álibi foi verificado |
| `background` | string | ✅ | Background detalhado (100-300 palavras) |
| `linkedAssets` | array | ❌ | Array de `assetId`s relacionados |
| `visibility` | string | ✅ | **initial** ou **hidden** |

---

## ⚙️ Rules (Server-Side APENAS)

🔒 **CRÍTICO DE SEGURANÇA**: Rules NUNCA devem ser expostas ao cliente. Processamento 100% server-side.

```json
{
  "rules": [
    {
      "ruleId": "rule.forensics_dna_reveals_email",
      "trigger": {
        "type": "forensics_complete",
        "inputAssetId": "asset.blood_sample",
        "analysisType": "DNA"
      },
      "actions": [
        {
          "type": "reveal_email",
          "emailId": "email.forensics_dna_result"
        },
        {
          "type": "reveal_asset",
          "assetId": "asset.dna_report"
        }
      ]
    },
    {
      "ruleId": "rule.download_cctv_reveals_suspect",
      "trigger": {
        "type": "attachment_download",
        "emailId": "email.security_alert",
        "assetId": "asset.cctv_footage"
      },
      "actions": [
        {
          "type": "reveal_suspect",
          "suspectId": "suspect.john_doe"
        }
      ]
    }
  ]
}
```

### Tipos de Triggers

| Tipo | Campos | Descrição |
|------|--------|-----------|
| `forensics_complete` | inputAssetId, analysisType | Quando análise forense completa |
| `attachment_download` | emailId, assetId | Quando attachment de email é baixado |
| `asset_viewed` | assetId | Quando asset é visualizado |
| `email_opened` | emailId | Quando email é aberto |
| `time_elapsed` | gameTimeMinutes | Após X minutos de jogo |

### Tipos de Actions

| Tipo | Campos | Descrição |
|------|--------|-----------|
| `reveal_email` | emailId | Torna email visível |
| `reveal_asset` | assetId | Torna asset visível |
| `reveal_suspect` | suspectId | Torna suspeito visível |
| `send_notification` | message | Envia notificação ao jogador |

---

## 🔬 Forensics Defaults

Configurações padrão para análises forenses quando não há regra específica.

```json
{
  "forensicsDefaults": {
    "analysisTypes": [
      {
        "type": "DNA",
        "durationMinutes": 300,
        "availableFor": ["blood", "hair", "saliva", "tissue"]
      },
      {
        "type": "Fingerprint",
        "durationMinutes": 150,
        "availableFor": ["print", "surface", "object"]
      },
      {
        "type": "DigitalForensics",
        "durationMinutes": 480,
        "availableFor": ["phone", "computer", "hard_drive", "usb"]
      },
      {
        "type": "Ballistics",
        "durationMinutes": 240,
        "availableFor": ["bullet", "casing", "firearm"]
      }
    ],
    "noFindingsEmail": {
      "template": "No significant findings were detected in the analysis of {{assetName}}.",
      "from": "Forensics Lab <lab@citypolice.gov>",
      "subject": "Analysis Results - No Findings"
    }
  }
}
```

---

## 🔐 Convenções de Segurança

### ❌ NUNCA Expor ao Cliente

```json
// Campos que DEVEM ser removidos antes de enviar ao frontend:
{
  "solution": { ... },           // ❌ PROIBIDO
  "culpritId": "suspect.xxx",    // ❌ PROIBIDO
  "rules": [ ... ],              // ❌ PROIBIDO
  "solutionStub": { ... },       // ❌ PROIBIDO
  "correctAnswer": "...",        // ❌ PROIBIDO
  "assets": [
    {
      "visibility": "hidden"     // ❌ Remover se não revelado
    }
  ]
}
```

### ✅ Sanitização Obrigatória

```csharp
public CaseDto SanitizeCaseForClient(Case case, string userId, string caseId) {
    // 1. Remover campos sensíveis
    case.Solution = null;
    case.Rules = null;
    case.CulpritId = null;
    
    // 2. Filtrar assets por visibilidade
    var visibleAssets = GetVisibleAssets(userId, caseId);
    case.Assets = case.Assets
        .Where(a => a.Visibility == "initial" || visibleAssets.Contains(a.AssetId))
        .ToList();
    
    // 3. Filtrar emails por visibilidade
    var visibleEmails = GetVisibleEmails(userId, caseId);
    case.Emails = case.Emails
        .Where(e => e.Visibility == "initial" || visibleEmails.Contains(e.EmailId))
        .ToList();
    
    return case;
}
```

---

## 📝 Exemplo Completo (Mínimo)

```json
{
  "version": "1.0",
  "caseId": "case_001",
  "metadata": {
    "title": "The Missing Heir",
    "description": "Investigate the disappearance of a wealthy businessman's son",
    "difficulty": "Intermediate",
    "estimatedTimeMinutes": 90,
    "requiredRank": "Detective",
    "location": "Portland, Oregon",
    "incidentDate": "2025-12-15T14:30:00Z",
    "category": "Missing Person",
    "briefing": "You've been assigned to investigate the sudden disappearance of Thomas Blackwood Jr., son of prominent businessman Thomas Blackwood Sr. The young software engineer was last seen leaving his apartment building yesterday afternoon.",
    "victim": {
      "name": "Thomas Blackwood Jr.",
      "age": 28,
      "occupation": "Software Engineer",
      "lastSeen": "2025-12-15T14:30:00Z"
    }
  },
  "assets": [
    {
      "assetId": "asset.briefing_doc",
      "name": "Initial Case Briefing",
      "type": "document",
      "category": "Document",
      "description": "Official case assignment from Chief of Police",
      "filePath": "/cases/case_001/assets/briefing.pdf",
      "visibility": "initial",
      "metadata": {
        "pages": 2,
        "format": "PDF"
      }
    },
    {
      "assetId": "asset.victim_photo",
      "name": "Photo of Thomas Blackwood Jr.",
      "type": "image",
      "category": "Document",
      "description": "Recent photo of the missing person",
      "filePath": "/cases/case_001/assets/victim_photo.jpg",
      "visibility": "initial"
    }
  ],
  "emails": [
    {
      "emailId": "email.briefing_001",
      "from": "Chief Sarah Mitchell <chief@citypolice.gov>",
      "to": "Detective Alex Morgan <detective@citypolice.gov>",
      "subject": "URGENT: Missing Person Case Assignment",
      "sentAt": "2025-12-15T08:00:00Z",
      "priority": "high",
      "visibility": "initial",
      "content": "Detective Morgan,\n\nYou've been assigned to investigate the disappearance of Thomas Blackwood Jr. He was last seen yesterday at 2:30 PM leaving his apartment in downtown Portland. His father, Thomas Blackwood Sr., reported him missing this morning when he failed to show up for an important company meeting.\n\nThis is a high-profile case given the Blackwood family's prominence in the community. I need you to handle this with discretion but urgency.\n\nAttached you'll find the initial case briefing and a recent photo of Thomas Jr.\n\nKeep me updated.\n\nChief Mitchell",
      "attachments": [
        "asset.briefing_doc",
        "asset.victim_photo"
      ]
    }
  ],
  "suspects": [
    {
      "suspectId": "suspect.thomas_blackwood_sr",
      "name": "Thomas Blackwood Sr.",
      "age": 58,
      "occupation": "CEO, Blackwood Industries",
      "relationship": "Father of victim",
      "motive": "Recent financial disputes over company succession",
      "alibi": "Claims to have been at company board meeting",
      "alibiVerified": false,
      "background": "Wealthy businessman who built Blackwood Industries into a multi-million dollar corporation. Known for his aggressive business tactics and high expectations. Recent reports suggest tension with his son over the future direction of the company.",
      "linkedAssets": [],
      "visibility": "initial"
    }
  ],
  "rules": [
    {
      "ruleId": "rule.forensics_reveals_clue",
      "trigger": {
        "type": "forensics_complete",
        "inputAssetId": "asset.phone_data",
        "analysisType": "DigitalForensics"
      },
      "actions": [
        {
          "type": "reveal_email",
          "emailId": "email.forensics_result"
        }
      ]
    }
  ],
  "forensicsDefaults": {
    "analysisTypes": [
      {
        "type": "DigitalForensics",
        "durationMinutes": 360,
        "availableFor": ["phone", "computer"]
      }
    ],
    "noFindingsEmail": {
      "template": "The analysis of {{assetName}} did not reveal any actionable leads.",
      "from": "Forensics Lab <lab@citypolice.gov>",
      "subject": "Analysis Results - {{analysisType}}"
    }
  }
}
```

---

## 🔄 Migração do Modelo Antigo

### Mapeamento de Campos

| v0 (antigo) | v1 (novo) | Notas |
|-------------|-----------|-------|
| `evidences` | `assets` | Renomear + ajustar estrutura |
| `unlockLogic` | `rules` | Substituir por sistema de rules |
| `forensicAnalyses` | `forensicsDefaults` | Simplificar + generalizar |
| `temporalEvents` | `emails` (parcialmente) | Converter events em emails |
| `solution` | ❌ Remover | Nunca expor ao cliente |

---

## ✅ Checklist de Validação

Um `case.json` v1.0 válido deve:

- [ ] Ter `version: "1.0"`
- [ ] Ter todos os campos obrigatórios (caseId, metadata, assets, emails, suspects, rules, forensicsDefaults)
- [ ] Ter pelo menos 1 email com `visibility: "initial"` (briefing do chefe)
- [ ] Ter pelo menos 1 asset com `visibility: "initial"`
- [ ] Ter pelo menos 1 suspeito
- [ ] Ter todos os IDs no formato correto (asset.*, email.*, suspect.*, rule.*)
- [ ] Ter filePaths válidos (existem no Blob Storage)
- [ ] Ter attachments válidos (todos os assetIds existem)
- [ ] NÃO ter campos `solution`, `culpritId` ou `solutionStub` (removidos antes de enviar ao cliente)
- [ ] Passar validação do JSON Schema

---

## 📚 Referências

- [MASTER_TASKS.md](../backlog/MASTER_TASKS.md) - Backlog completo
- [case.schema.json](../schemas/case.schema.json) - JSON Schema formal
- [OBJETO_CASO.md](./OBJETO_CASO.md) - Modelo antigo (referência)
- [EMAIL_SYSTEM_IMPLEMENTATION.md](./EMAIL_SYSTEM_IMPLEMENTATION.md) - Sistema de emails

---

**Última Atualização:** 29/12/2025  
**Autor:** CaseZero Dev Team  
**Status:** ✅ Ready for Implementation
