# Capítulo 04 - Estrutura de Caso

**Documento de Design de Jogo - CaseZero v3.0**  
**Última atualização:** 13 de novembro de 2025  
**Status:** ✅ Completo

---

## 4.1 Visão Geral

Este capítulo define a **anatomia completa de um caso** – os componentes, formatos e relações que compõem uma investigação íntegra. Todo caso em CaseZero segue esta estrutura para garantir consistência, completude e investigabilidade.

**Conceitos-chave:**

- `case.json` como fonte única da verdade
- Componentes obrigatórios vs. opcionais
- Tipos e categorização de evidências
- Modelos e formatos de documentos
- Tipos de análises forenses
- Fórmulas de balanceamento de dificuldade
- Checklist de requisitos de assets

---

## 4.2 Anatomia do Caso

### Caso Mínimo Viável

**Todo caso DEVE conter:**

```text
CASE-AAAA-NNN/
├── case.json                 # Arquivo mestre de dados
├── README.md                 # Resumo legível por humanos
├── evidence/                 # Fotos das evidências físicas
│   ├── ev001-weapon.jpg
│   └── ev002-blood.jpg
├── documents/                # Documentos da investigação
│   ├── police-report.pdf
│   ├── witness-statement-1.pdf
│   └── suspect-interview-1.pdf
├── forensics/                # Laudos forenses (gerados)
│   ├── ballistics-report.pdf
│   └── dna-report.pdf
└── suspects/                 # Fotos dos suspeitos
    ├── suspect-1.jpg
    └── suspect-2.jpg
```

### Estrutura Completa do Caso

**Estrutura completa com elementos opcionais:**

```text
CASE-AAAA-NNN/
├── case.json                 # OBRIGATÓRIO
├── README.md                 # OBRIGATÓRIO
├── evidence/                 # OBRIGATÓRIO (mínimo 3 itens)
│   ├── photos/
│   ├── documents/
│   └── metadata/
├── documents/                # OBRIGATÓRIO (mínimo 5)
│   ├── police/
│   ├── witnesses/
│   ├── suspects/
│   ├── forensics/
│   └── personal/
├── suspects/                 # OBRIGATÓRIO (mínimo 2)
│   ├── photos/
│   └── profiles/
├── forensics/                # OBRIGATÓRIO (mínimo 1 tipo)
│   ├── dna/
│   ├── ballistics/
│   ├── fingerprints/
│   └── toxicology/
├── witnesses/                # OPCIONAL
│   └── photos/
├── victim/                   # OBRIGATÓRIO
│   ├── photo.jpg
│   └── background.pdf
├── memos/                    # OPCIONAL (futuro)
│   └── case-notes.txt
└── timeline/                 # OPCIONAL (gerado automaticamente)
    └── timeline.json
```json

---

## 4.3 Esquema do `case.json`

Arquivo mestre que define todo o caso.

### Esquema completo

```json
{
  "caseId": "CASE-2024-001",
  "version": "3.0",
  "metadata": {
    "title": "The Downtown Office Murder",
    "shortDescription": "Business partner found dead in locked office",
    "createdAt": "2024-01-15T00:00:00Z",
    "author": "CaseZero Team",
    "difficulty": "Medium",
    "estimatedTimeHours": 4.5,
    "tags": ["homicide", "financial-motive", "locked-room"],
    "status": "Published"
  },
  "crime": {
    "type": "Homicide",
    "date": "2023-03-15T23:30:00Z",
    "location": {
      "name": "TechCorp Office Building",
      "address": "450 Market Street, Floor 15",
      "city": "San Francisco",
      "state": "CA",
      "coordinates": {
        "lat": 37.7749,
        "lng": -122.4194
      }
    },
    "description": "Victim found shot once in the chest in his private office. Door was locked from inside. No signs of forced entry.",
    "weaponUsed": "Firearm (.38 caliber revolver)",
    "causeOfDeath": "Single gunshot wound to chest"
  },
  "victim": {
    "id": "VICTIM-001",
    "name": "Robert Chen",
    "age": 42,
    "gender": "Male",
    "occupation": "CEO, TechCorp Industries",
    "photo": "victim/robert-chen.jpg",
    "background": "victim/background.pdf",
    "personalityTraits": ["ambitious", "detail-oriented", "demanding"],
    "relationships": [
      {
        "personId": "SUSP-001",
        "relationship": "Business Partner",
        "nature": "Strained - financial disputes"
      },
      {
        "personId": "SUSP-002",
        "relationship": "Wife",
        "nature": "Troubled marriage"
      }
    ],
    "relevantHistory": [
      "Founded TechCorp in 2018",
      "Recent financial success but partner conflicts",
      "Life insurance policy for $2M"
    ]
  },
  "suspects": [
    {
      "id": "SUSP-001",
      "name": "Michael Torres",
      "age": 38,
      "gender": "Male",
      "occupation": "COO, TechCorp Industries",
      "photo": "suspects/michael-torres.jpg",
      "background": "Co-founded TechCorp with victim. Minority shareholder (30%). MBA from State University.",
      "motive": "Financial dispute - owed victim $500,000. Threatened buyout of shares.",
      "alibi": "Claims home alone watching TV, 9 PM - midnight. No witnesses.",
      "criminalRecord": "None",
      "personalityTraits": ["calculating", "resentful", "intelligent"],
      "interview": "documents/suspect-interview-torres.pdf",
      "relatedEvidence": ["EV-001", "EV-004", "EV-007"],
      "isGuilty": true,
      "guiltEvidence": [
        "DNA at crime scene",
        "Weapon registered to him",
        "Financial motive confirmed",
        "Alibi cannot be verified",
        "Security log places him at building"
      ]
    },
    {
      "id": "SUSP-002",
      "name": "Linda Chen",
      "age": 40,
      "gender": "Female",
      "occupation": "Marketing Director",
      "photo": "suspects/linda-chen.jpg",
      "background": "Married to victim for 12 years. Recent marital problems. Beneficiary of life insurance.",
      "motive": "Life insurance payout ($2M). Marital issues documented.",
      "alibi": "Home at time of murder. CCTV confirms she never left residence.",
      "criminalRecord": "None",
      "personalityTraits": ["composed", "grieving", "private"],
      "interview": "documents/suspect-interview-chen.pdf",
      "relatedEvidence": ["EV-008", "EV-012"],
      "isGuilty": false,
      "exoneratingEvidence": [
        "CCTV alibi confirmed",
        "DNA not at scene",
        "No gunshot residue",
        "Timeline doesn't match"
      ]
    },
    {
      "id": "SUSP-003",
      "name": "David Park",
      "age": 29,
      "gender": "Male",
      "occupation": "Former Employee",
      "photo": "suspects/david-park.jpg",
      "background": "Fired by victim 6 months prior. Software engineer. Had building access card.",
      "motive": "Revenge - fired unfairly, threatened legal action.",
      "alibi": "At bar with friends, 8 PM - 1 AM. Multiple witnesses.",
      "criminalRecord": "Assault charge (2019) - dismissed",
      "personalityTraits": ["volatile", "brilliant", "vindictive"],
      "interview": "documents/suspect-interview-park.pdf",
      "relatedEvidence": ["EV-003"],
      "isGuilty": false,
      "exoneratingEvidence": [
        "Multiple witnesses confirm alibi",
        "Access card deactivated 6 months ago",
        "No forensic connection"
      ]
    }
  ],
  "evidence": [
    {
      "id": "EV-001",
      "name": "Firearm - .38 Caliber Revolver",
      "type": "Physical",
      "category": "Weapon",
      "description": "Smith & Wesson .38 Special revolver. Serial number registered to Michael Torres.",
      "photos": [
        "evidence/ev001-overview.jpg",
        "evidence/ev001-closeup.jpg",
        "evidence/ev001-serial.jpg"
      ],
      "collectedFrom": "Crime scene, 3 feet from victim",
      "collectedBy": "CSI Team Alpha",
      "collectedAt": "2023-03-16T02:00:00Z",
      "tags": ["weapon", "critical", "firearm"],
      "forensicAnalysisAvailable": [
        {
          "type": "Ballistics",
          "duration": 12,
          "durationUnit": "hours",
          "reportTemplate": "forensics/ballistics-report-template.pdf"
        },
        {
          "type": "Fingerprints",
          "duration": 8,
          "durationUnit": "hours",
          "reportTemplate": "forensics/fingerprint-report-template.pdf"
        }
      ],
      "forensicResults": {
        "Ballistics": {
          "finding": "Bullet recovered from victim matches rifling pattern. High confidence match.",
          "significance": "Critical - confirms this weapon fired fatal shot"
        },
        "Fingerprints": {
          "finding": "Partial print on grip matches Michael Torres (right thumb, 85% confidence)",
          "significance": "Strong - places Torres in contact with weapon"
        }
      },
      "importance": "Critical"
    },
    {
      "id": "EV-004",
      "name": "Blood Sample - Crime Scene",
      "type": "Biological",
      "category": "Blood",
      "description": "Blood droplets found near door. Distinct from victim's blood.",
      "photos": [
        "evidence/ev004-scene.jpg",
        "evidence/ev004-sample.jpg"
      ],
      "collectedFrom": "Office entrance, near door handle",
      "collectedBy": "CSI Team Alpha",
      "collectedAt": "2023-03-16T03:30:00Z",
      "tags": ["biological", "critical", "blood"],
      "forensicAnalysisAvailable": [
        {
          "type": "DNA",
          "duration": 24,
          "durationUnit": "hours",
          "reportTemplate": "forensics/dna-report-template.pdf"
        }
      ],
      "forensicResults": {
        "DNA": {
          "finding": "DNA profile matches Michael Torres (99.7% confidence). Not victim's blood.",
          "significance": "Critical - places Torres at scene, suggests he was injured/cut"
        }
      },
      "importance": "Critical"
    },
    {
      "id": "EV-007",
      "name": "Security Access Log",
      "type": "Document",
      "category": "Records",
      "description": "Digital log of building access card swipes on night of murder.",
      "photos": [
        "evidence/ev007-log.pdf"
      ],
      "collectedFrom": "Building security office",
      "collectedBy": "Detective Sarah Martinez",
      "collectedAt": "2023-03-16T10:00:00Z",
      "tags": ["document", "timeline", "critical"],
      "forensicAnalysisAvailable": [],
      "forensicResults": {},
      "keyEntries": [
        "23:15 - Michael Torres - Floor 15 entry",
        "23:45 - Michael Torres - Floor 15 exit"
      ],
      "importance": "Critical"
    }
  ],
  "documents": [
    {
      "id": "DOC-001",
      "type": "PoliceReport",
      "title": "Initial Incident Report #2023-0315",
      "fileName": "documents/police-report-2023-0315.pdf",
      "author": "Officer Sarah Martinez",
      "dateCreated": "2023-03-16T08:00:00Z",
      "pageCount": 3,
      "description": "Official police report documenting crime scene, initial findings, and witness accounts.",
      "availableAt": "start",
      "tags": ["official", "initial", "scene"],
      "keyInformation": [
        "Body discovered at 12:30 AM by security guard",
        "Single gunshot wound, estimated TOD 11:30 PM",
        "Door locked from inside",
        "Weapon found at scene"
      ],
      "relatedEvidence": ["EV-001", "EV-002"],
      "relatedPeople": ["VICTIM-001"],
      "importance": "Critical"
    },
    {
      "id": "DOC-003",
      "type": "WitnessStatement",
      "title": "Statement - John Silva, Night Security Guard",
      "fileName": "documents/witness-silva.pdf",
      "author": "John Silva",
      "dateCreated": "2023-03-16T04:00:00Z",
      "pageCount": 2,
      "description": "Statement from security guard who discovered body.",
      "availableAt": "start",
      "tags": ["witness", "discovery"],
      "keyInformation": [
        "Discovered body during routine 12:30 AM check",
        "Heard no gunshot (on different floor)",
        "Saw Torres enter building at 11:15 PM",
        "Saw Torres exit at 11:45 PM"
      ],
      "relatedEvidence": ["EV-007"],
      "relatedPeople": ["SUSP-001"],
      "importance": "High"
    },
    {
      "id": "DOC-004",
      "type": "SuspectInterview",
      "title": "Interview Transcript - Michael Torres",
      "fileName": "documents/suspect-interview-torres.pdf",
      "author": "Detective Lisa Wong",
      "dateCreated": "2023-03-17T14:00:00Z",
      "pageCount": 4,
      "description": "Formal interview with primary suspect Michael Torres.",
      "availableAt": "start",
      "tags": ["suspect", "interview", "torres"],
      "keyInformation": [
        "Claims he was home alone",
        "Admits financial dispute with victim",
        "Cannot explain building access log",
        "Nervous, evasive answers about timeline"
      ],
      "relatedEvidence": ["EV-007"],
      "relatedPeople": ["SUSP-001"],
      "contradictions": ["DOC-003"],
      "importance": "Critical"
    },
    {
      "id": "DOC-009",
      "type": "FinancialRecord",
      "title": "Bank Statements - Torres & Chen",
      "fileName": "documents/financial-records.pdf",
      "author": "First National Bank",
      "dateCreated": "2023-03-17T00:00:00Z",
      "pageCount": 2,
      "description": "Financial records showing transactions between victim and Torres.",
      "availableAt": "start",
      "tags": ["financial", "motive"],
      "keyInformation": [
        "$500,000 loan from Chen to Torres (2022)",
        "Payment due: March 1, 2023",
        "Torres account shows insufficient funds",
        "Email mentions buyout threat"
      ],
      "relatedPeople": ["VICTIM-001", "SUSP-001"],
      "importance": "High"
    }
  ],
  "forensicReports": [
    {
      "id": "FOR-001",
      "type": "Ballistics",
      "evidenceId": "EV-001",
      "title": "Ballistics Analysis Report - EV-001",
      "fileName": "forensics/ballistics-ev001.pdf",
      "analyst": "Dr. James Chen, PhD",
      "completionTime": 12,
      "completionUnit": "hours",
      "templatePath": "forensics/templates/ballistics-template.pdf",
      "findings": {
        "summary": "Weapon #EV-001 fired fatal bullet",
        "details": [
          "Bullet recovered from victim matches rifling pattern",
          "Gunshot residue present on weapon grip",
          "Weapon recently fired (within 24 hours of collection)",
          "High confidence match (99.2%)"
        ]
      },
      "significance": "Critical",
      "relatedEvidence": ["EV-001", "EV-002"]
    },
    {
      "id": "FOR-002",
      "type": "DNA",
      "evidenceId": "EV-004",
      "title": "DNA Analysis Report - EV-004",
      "fileName": "forensics/dna-ev004.pdf",
      "analyst": "Dr. Sarah Kim, PhD",
      "completionTime": 24,
      "completionUnit": "hours",
      "templatePath": "forensics/templates/dna-template.pdf",
      "findings": {
        "summary": "Blood sample matches Michael Torres",
        "details": [
          "DNA profile: [technical details]",
          "Match: Michael Torres (99.7% confidence)",
          "Blood type: O positive (matches Torres)",
          "Conclusion: Torres was present at scene"
        ]
      },
      "significance": "Critical",
      "relatedEvidence": ["EV-004"],
      "relatedPeople": ["SUSP-001"]
    }
  ],
  "timeline": [
    {
      "time": "2023-03-15T22:00:00Z",
      "event": "Victim enters office building",
      "source": "Security CCTV",
      "sourceDocument": "DOC-001",
      "verified": true,
      "importance": "Medium"
    },
    {
      "time": "2023-03-15T22:30:00Z",
      "event": "Victim last seen alive (video call)",
      "source": "Wife testimony",
      "sourceDocument": "DOC-005",
      "verified": true,
      "importance": "High"
    },
    {
      "time": "2023-03-15T23:15:00Z",
      "event": "Michael Torres enters building",
      "source": "Security access log + CCTV",
      "sourceDocument": "DOC-003",
      "verified": true,
      "importance": "Critical"
    },
    {
      "time": "2023-03-15T23:30:00Z",
      "event": "Estimated time of death",
      "source": "Medical examiner",
      "sourceDocument": "FOR-003",
      "verified": true,
      "importance": "Critical"
    },
    {
      "time": "2023-03-15T23:45:00Z",
      "event": "Michael Torres exits building",
      "source": "Security access log + witness",
      "sourceDocument": "DOC-003",
      "verified": true,
      "importance": "Critical"
    },
    {
      "time": "2023-03-16T00:30:00Z",
      "event": "Body discovered by security guard",
      "source": "Witness statement",
      "sourceDocument": "DOC-003",
      "verified": true,
      "importance": "High"
    }
  ],
  "solution": {
    "culprit": "SUSP-001",
    "motive": "Financial desperation. Torres owed victim $500,000 and was facing buyout of his shares. Saw murder as only way to eliminate debt and keep company stake.",
    "method": "Torres used his building access to enter office late at night. Confronted victim about debt. Argument escalated. Shot victim with his own registered firearm. Locked door from inside to buy time, exited through emergency stairwell.",
    "keyEvidence": [
      "EV-001 - Weapon registered to Torres, his prints found",
      "EV-004 - Torres' blood at scene (cut hand during struggle)",
      "EV-007 - Security log places Torres at building during murder window",
      "DOC-004 - Torres' weak alibi and evasive answers",
      "DOC-009 - Financial motive confirmed"
    ],
    "explanation": {
      "what": "Michael Torres shot Robert Chen once in the chest with a .38 caliber revolver",
      "why": "Financial desperation - owed $500k, facing buyout, saw no other escape",
      "how": "Used building access at 11:15 PM, confronted victim, shot him during argument, staged scene, exited 11:45 PM",
      "proves": "DNA at scene, weapon prints, access log timeline, financial records, failed alibi"
    },
    "alternativeTheories": [
      {
        "suspect": "SUSP-002",
        "plausibility": "Low",
        "why": "Strong motive (life insurance) but CCTV alibi is ironclad"
      },
      {
        "suspect": "SUSP-003",
        "plausibility": "Very Low",
        "why": "Motive exists but multiple witness alibi and no forensic connection"
      }
    ]
  },
  "difficulty": {
    "rating": "Medium",
    "factors": {
      "suspectCount": 3,
      "documentCount": 12,
      "evidenceCount": 8,
      "redHerrings": 2,
      "forensicComplexity": "Moderate",
      "timelineComplexity": "Simple",
      "motiveClarify": "Clear"
    },
    "estimatedTime": {
      "easy": "N/A",
      "medium": 4.5,
      "hard": "N/A",
      "expert": "N/A"
    },
    "successRate": {
      "firstAttempt": 35,
      "secondAttempt": 60,
      "thirdAttempt": 80
    }
  },
  "metadata_technical": {
    "version": "3.0",
    "schemaVersion": "1.0",
    "createdAt": "2024-01-15T00:00:00Z",
    "lastModified": "2024-01-20T00:00:00Z",
    "status": "Published",
    "tags": ["homicide", "financial-motive", "locked-room", "business-crime"],
    "contentWarnings": ["violence", "murder"],
    "minRankRequired": "Detective III",
    "language": "en-US",
    "localizationAvailable": ["fr-FR", "pt-BR", "es-ES"]
  }
}
```

### Campos obrigatórios do esquema

**Campos de topo obrigatórios:**

```json
{
  "caseId": "string",              // Formato CASE-AAAA-NNN
  "version": "string",              // "3.0"
  "metadata": {},                   // Metadados do caso
  "crime": {},                      // Detalhes do crime
  "victim": {},                     // Informações da vítima
  "suspects": [],                   // Array (mínimo 2)
  "evidence": [],                   // Array (mínimo 3)
  "documents": [],                  // Array (mínimo 5)
  "forensicReports": [],            // Array (mínimo 1)
  "timeline": [],                   // Array (mínimo 3 eventos)
  "solution": {},                   // Solução correta
  "difficulty": {}                  // Métricas de dificuldade
}
```

---

## 4.4 Tipos e Categorias de Evidência

### Evidência física

**Armas:**

- Armas de fogo (pistolas, rifles)
- Armas brancas (facas, espadas)
- Objetos contundentes (tacos, martelos)
- Armas improvisadas

**Itens pessoais:**

- Carteiras, bolsas
- Celulares, eletrônicos
- Joias
- Roupas
- Documentos

**Ferramentas:**

- Utilizadas na execução do crime
- Ferramentas de arrombamento
- Instrumentos de restrição

### Evidência biológica

**Amostras:**

- Sangue
- Cabelos/fibras
- Saliva
- Tecidos
- Fluidos corporais

**Tipos de análise:**

- Perfil de DNA
- Tipagem sanguínea
- Comparação de cabelos
- Análise de fibras

### Evidência de vestígios

**Tipos:**

- Impressões digitais (latentes, patentes)
- Pegadas/solados
- Marcas de pneus
- Lascas de tinta
- Fragmentos de vidro
- Amostras de solo

### Evidência documental

**Tipos:**

- Bilhetes manuscritos
- Documentos datilografados
- Papéis falsificados
- Cartas ameaçadoras
- Registros corporativos
- Correspondências pessoais

**Análises:**

- Comparação de caligrafia
- Detecção de falsificação
- Análise de tinta
- Datação de papel

### Evidência digital (futuro)

**Tipos:**

- Registros telefônicos
- Mensagens de texto
- E-mails
- Arquivos de computador
- Fotos/vídeos
- Dados de GPS
- Redes sociais

---

## 4.5 Tipos de Documento e Modelos

### Modelo de relatório policial

**Estrutura:**

```text
┌─────────────────────────────────────────────┐
│ DEPARTAMENTO DE POLÍCIA METROPOLITANO       │
│ RELATÓRIO DE INCIDENTE                      │
├─────────────────────────────────────────────┤
│ Nº do Caso: 2023-0315                       │
│ Tipo de Incidente: Homicídio                │
│ Data/Horário: 15 de março de 2023, 23h30 (est.)│
│ Local: 450 Market St, 15º andar             │
│ Oficial Responsável: Martinez, Sarah (badge)│
│ Data de Registro: 16 de março de 2023, 08h00 │
├─────────────────────────────────────────────┤
│                                             │
│ RESUMO:                                     │
│ Por volta de 00h30 de 16/03/2023, esta      │
│ oficial respondeu a um chamado...           │
│                                             │
│ DESCRIÇÃO DA CENA:                          │
│ Vítima encontrada em escritório privado,    │
│ porta trancada pelo lado interno...         │
│                                             │
│ EVIDÊNCIAS COLETADAS:                       │
│ - Evidência #001: Arma de fogo (.38)        │
│ - Evidência #002: Projétil recuperado       │
│ ...                                         │
│                                             │
│ INFORMAÇÕES DE TESTEMUNHAS:                 │
│ - Silva, John (Vigia de segurança)          │
│ ...                                         │
│                                             │
│ ACHADOS INICIAIS:                           │
│ Único disparo no tórax. Hora estimada da    │
│ morte: 23h30. Sem sinais de arrombamento... │
│                                             │
│ [Assinatura do Oficial]                     │
└─────────────────────────────────────────────┘
```

**Elementos essenciais:**

- Cabeçalho oficial
- Número do caso
- Dados do oficial
- Narrativa cronológica
- Descrição da cena
- Registro de evidências
- Lista de testemunhas
- Conclusões iniciais

### Modelo de declaração de testemunha

**Estrutura:**

```text
┌─────────────────────────────────────────────┐
│ DEPARTAMENTO DE POLÍCIA METROPOLITANO       │
│ DECLARAÇÃO DE TESTEMUNHA                    │
├─────────────────────────────────────────────┤
│ Nº do Caso: 2023-0315                       │
│ Nome da Testemunha: John Silva              │
│ Data da Declaração: 16 de março de 2023     │
│ Oficial Entrevistador: Martinez, S.         │
├─────────────────────────────────────────────┤
│                                             │
│ DECLARAÇÃO:                                 │
│                                             │
│ "Eu fazia minha ronda no 12º andar quando  │
│ notei a luz ainda acesa no escritório do Sr.│
│ Chen, no 15º. Isso era incomum depois da    │
│ meia-noite. Peguei o elevador..."           │
│                                             │
│ [Continua por 1-3 páginas]                  │
│                                             │
│ Declaro que a declaração acima é verdadeira │
│ e correta ao melhor do meu conhecimento.    │
│                                             │
│ [Assinatura da Testemunha]                  │
│ [Data]                                      │
└─────────────────────────────────────────────┘
```

### Modelo de entrevista com suspeito

**Estrutura (formato perguntas e respostas):**

```text
┌─────────────────────────────────────────────┐
│ DEPARTAMENTO DE POLÍCIA METROPOLITANO       │
│ TRANSCRIÇÃO DE ENTREVISTA                   │
├─────────────────────────────────────────────┤
│ Nº do Caso: 2023-0315                       │
│ Entrevistado: Michael Torres                │
│ Data: 17 de março de 2023, 14h00            │
│ Local: Delegacia, Sala 3                    │
│ Entrevistador: Detetive Lisa Wong           │
│ Presentes: Advogado David Miller            │
├─────────────────────────────────────────────┤
│                                             │
│ DET. WONG: Sr. Torres, onde o senhor estava │
│ na noite de 15 de março entre 22h e meia-noite?│
│                                             │
│ TORRES: Eu estava em casa. Assistindo TV.   │
│                                             │
│ DET. WONG: Alguém pode confirmar isso?      │
│                                             │
│ TORRES: Não, moro sozinho. Minha namorada   │
│ estava viajando.                            │
│                                             │
│ DET. WONG: Temos registros de acesso        │
│ indicando seu crachá no prédio às 23h15.    │
│                                             │
│ TORRES: [Pausa] Eu... não me lembro disso.  │
│                                             │
│ [Continua por 3-4 páginas]                  │
│                                             │
│ Entrevista encerrada às 15h45.              │
│ [Assinaturas]                               │
└─────────────────────────────────────────────┘
```

**Elementos essenciais:**

- Cabeçalho formal
- Todos os presentes registrados
- Formato Q&A com citações literais
- Observações de pausas/reacões
- Notas do detetive quando relevante

### Modelo de laudo forense

**Estrutura:**

```text
┌─────────────────────────────────────────────┐
│ LABORATÓRIO FORENSE METROPOLITANO           │
│ RELATÓRIO DE ANÁLISE FORENSE                │
├─────────────────────────────────────────────┤
│ Tipo de Laudo: Análise de DNA               │
│ Nº do Caso: 2023-0315                       │
│ Nº da Evidência: EV-004                     │
│ Perito: Dra. Sarah Kim, PhD                 │
│ Data de Recebimento: 16 de março de 2023    │
│ Data da Análise: 17 de março de 2023        │
│ Data do Relatório: 17 de março de 2023      │
├─────────────────────────────────────────────┤
│                                             │
│ DESCRIÇÃO DA EVIDÊNCIA:                     │
│ Amostra de sangue coletada na cena,         │
│ próxima à maçaneta da porta.                │
│                                             │
│ ANÁLISES REALIZADAS:                        │
│ Extração e perfil genético via STR (16 loci).│
│                                             │
│ METODOLOGIA:                                │
│ Protocolo padrão de extração de DNA.        │
│ PCR e eletroforese capilar. Comparação de   │
│ perfis.                                     │
│                                             │
│ ACHADOS:                                    │
│ Perfil de DNA obtido: [detalhes técnicos].  │
│ Comparado com amostras conhecidas.          │
│                                             │
│ CONCLUSÕES:                                 │
│ Perfil coincide com Michael Torres          │
│ (amostra de referência) com 99,7% de        │
│ confiança estatística. Probabilidade de     │
│ coincidência aleatória: 1 em 5 bilhões.     │
│                                             │
│ [Assinatura do Perito]                      │
│ [Assinatura do Diretor do Laboratório]      │
└─────────────────────────────────────────────┘
```

### Modelo de registros financeiros

**Estrutura:**

```text
┌─────────────────────────────────────────────┐
│ FIRST NATIONAL BANK                         │
│ EXTRATO DE CONTA                            │
├─────────────────────────────────────────────┤
│ Titular: Michael Torres                    │
│ Conta nº: ****1234                          │
│ Período: 1º jan – 15 mar 2023              │
├─────────────────────────────────────────────┤
│                                             │
│ Data       Descrição             Valor     │
│ ────────────────────────────────────────   │
│ 05/01      Saldo inicial        US$125.450 │
│ 15/01      Depósito salário     US$12.500  │
│ 01/02      Transferência (saída)US$50.000- │
│ 15/02      Depósito salário     US$12.500  │
│ 28/02      Aviso de pagamento   US$500.000 │
│           (Empréstimo #LN-4421)            │
│ 01/03      FUNDOS INSUFICIENTES            │
│ 10/03      E-mail – Ameaça de buyout       │
│           de R. Chen                       │
│ 15/03      Saldo final          US$42.180  │
│                                             │
│ NOTAS:                                      │
│ Empréstimo em aberto: US$500.000 (vencido) │
└─────────────────────────────────────────────┘
```

---

## 4.6 Tipos de Análise Forense

### Análise de DNA

**Duração:** 24 horas  
**Aplicável a:** sangue, cabelo, saliva, tecido, fluidos corporais  
**Resultados fornecidos:**

- Perfil de DNA (loci STR)
- Correspondência com suspeitos conhecidos (% de confiança)
- Busca em banco de dados (se não houver match)
- Significância estatística

**Seções do laudo:**

- Descrição da evidência
- Metodologia
- Dados do perfil de DNA
- Resultados de comparação
- Conclusões

**Uso típico:**

- Identificar perpetrador a partir de vestígios biológicos
- Confirmar presença de suspeito na cena
- Excluir inocentes
- Estabelecer relações

### Análise balística

**Duração:** 12 horas  
**Aplicável a:** armas de fogo, projéteis, cápsulas  
**Resultados fornecidos:**

- Identificação da arma
- Correspondência do projétil (disparado pela arma)
- Análise de trajetória
- Estimativa de distância
- Presença de resíduo de disparo

**Seções do laudo:**

- Especificações da arma
- Exame de projéteis/cápsulas
- Resultados de microscopia comparativa
- Achados de trajetória
- Conclusões

### Análise de impressões digitais

**Duração:** 8 horas  
**Aplicável a:** impressões latentes em superfícies, objetos, armas  
**Resultados fornecidos:**

- Avaliação de qualidade da impressão
- Classificação do padrão (arco, laço, verticilo)
- Correspondência com suspeitos
- Pontos de comparação
- Nível de confiança

**Seções do laudo:**

- Qualidade e localização da impressão
- Métodos de realce utilizados
- Resultados de comparação
- Confiança da correspondência
- Conclusões

### Análise toxicológica

**Duração:** 36 horas  
**Aplicável a:** sangue, tecidos, conteúdo estomacal  
**Resultados fornecidos:**

- Detecção e identificação de drogas
- Identificação de venenos
- Concentração alcoólica
- Medicamentos prescritos
- Estimativa do tempo desde ingestão

**Seções do laudo:**

- Descrição da amostra
- Métodos analíticos (GC-MS etc.)
- Substâncias detectadas
- Concentrações
- Interpretação e relevância

### Análise de vestígios

**Duração:** 16 horas  
**Aplicável a:** fibras, cabelos, tinta, vidro, solo  
**Resultados fornecidos:**

- Identificação do material
- Comparação com possíveis fontes
- Confirmação de transferência
- Origem de fabricação (quando possível)

**Seções do laudo:**

- Descrição da evidência
- Exame microscópico
- Análise química
- Resultados de comparação
- Conclusões

### Grafoscopia

**Duração:** 10 horas  
**Aplicável a:** documentos manuscritos, assinaturas  
**Resultados fornecidos:**

- Identificação do autor
- Detecção de falsificação
- Identificação de alterações
- Comparação com amostras conhecidas

**Seções do laudo:**

- Descrição do documento
- Características analisadas
- Comparação com amostras conhecidas
- Conclusões (definitivo/provável/inconclusivo)

---

## 4.7 Balanceamento de Dificuldade

### Fatores de dificuldade

**Variáveis que aumentam a dificuldade:**

1. **Número de suspeitos**
   - Fácil: 2-3 suspeitos
   - Médio: 4-5 suspeitos
   - Difícil: 6-7 suspeitos
   - Especialista: 8+ suspeitos

2. **Quantidade de documentos**
   - Fácil: 8-12 documentos
   - Médio: 12-18 documentos
   - Difícil: 18-25 documentos
   - Especialista: 25+ documentos

3. **Quantidade de evidências**
   - Fácil: 5-8 itens
   - Médio: 8-12 itens
   - Difícil: 12-18 itens
   - Especialista: 18+ itens

4. **Força das pistas falsas**
   - Fácil: Falsos suspeitos óbvios, fácil eliminar
   - Médio: Plausíveis, mas evidências os inocentam
   - Difícil: Muito plausíveis, exigem análise cuidadosa
   - Especialista: Quase tão convincentes quanto o verdadeiro

5. **Clareza do motivo**
   - Fácil: Óbvio e declarado diretamente
   - Médio: Requer conectar documentos
   - Difícil: Oculto, exige inferência
   - Especialista: Múltiplas camadas, com desinformação

6. **Complexidade da linha do tempo**
   - Fácil: Sequência simples e clara
   - Médio: Algumas lacunas, exige reconstrução
   - Difícil: Relatos conflitantes, requer conciliação
   - Especialista: Múltiplas linhas do tempo, contradições deliberadas

7. **Complexidade forense**
   - Fácil: 1-2 tipos, resultados claros
   - Médio: 3-4 tipos, alguma ambiguidade
   - Difícil: 5+ tipos, exige correlacionar
   - Especialista: Interdependências complexas

### Fórmula de dificuldade

```javascript
function calculateDifficulty(case) {
  let score = 0;
  
  // Quantidade de suspeitos (0-40 pontos)
  score += (case.suspects.length - 2) * 5;
  
  // Quantidade de documentos (0-30 pontos)
  score += Math.floor((case.documents.length - 8) / 2);
  
  // Quantidade de evidências (0-20 pontos)
  score += (case.evidence.length - 5) * 2;
  
  // Força das pistas falsas (0-30 pontos)
  const redHerringScore = case.suspects
    .filter(s => !s.isGuilty && s.motive)
    .reduce((sum, s) => sum + s.plausibilityRating, 0);
  score += redHerringScore;
  
  // Clareza do motivo (0-20 pontos)
  // 0 = explícito, 20 = profundamente oculto
  score += case.solution.motiveObfuscation;
  
  // Complexidade da linha do tempo (0-20 pontos)
  const timelineGaps = case.timeline.filter(e => !e.verified).length;
  score += timelineGaps * 2;
  
  // Complexidade forense (0-20 pontos)
  const forensicTypes = new Set(case.evidence
    .flatMap(e => e.forensicAnalysisAvailable.map(f => f.type))
  ).size;
  score += forensicTypes * 3;
  
  // Total: 0-180 pontos
  return score;
}

function getDifficultyRating(score) {
  if (score < 40) return "Easy";
  if (score < 80) return "Medium";
  if (score < 120) return "Hard";
  return "Expert";
}
```

### Faixas-alvo de dificuldade

**Fácil (0-40 pontos):**

- Tempo: 2-4 horas
- Acerto na primeira tentativa: 50-60%
- Patente mínima: Novato

**Médio (40-80 pontos):**

- Tempo: 4-6 horas
- Acerto na primeira tentativa: 30-40%
- Patente mínima: Detetive III

**Difícil (80-120 pontos):**

- Tempo: 6-8 horas
- Acerto na primeira tentativa: 20-30%
- Patente mínima: Detetive I

**Especialista (120-180 pontos):**

- Tempo: 8-12 horas
- Acerto na primeira tentativa: 10-20%
- Patente mínima: Detetive Líder

---

## 4.8 Checklist de Assets do Caso

### Assets obrigatórios por caso

**Documentos (PDFs):**

- [ ] Relatório policial (3-5 páginas)
- [ ] 2+ depoimentos de testemunha (1-3 páginas cada)
- [ ] 2+ entrevistas de suspeito (2-4 páginas cada)
- [ ] Documentos de contexto (variável)
- [ ] Modelos de laudo forense (para geração)

**Fotos de evidência (JPGs):**

- [ ] 3+ itens de evidência (2-3 ângulos cada)
- [ ] Alta resolução (mínimo 2000x1500)
- [ ] Tags de evidência visíveis
- [ ] Referência de escala (régua) incluída

**Fotos de personagens (JPGs):**

- [ ] Foto da vítima (retrato)
- [ ] 2+ fotos de suspeitos (retratos)
- [ ] Fotos de testemunhas (opcional)

**Fotos de locais (opcional):**

- [ ] Exterior da cena
- [ ] Interior da cena
- [ ] Locais relevantes

### Padrões de qualidade

**PDFs:**

- Formatação profissional
- Cabeçalhos/rodapés realistas
- Sem erros ou typos
- Tamanho entre 1-5 MB
- Texto pesquisável (evitar imagem pura)

**Fotos:**

- Resolução mínima 2000x1500
- Iluminação clara
- Apresentação realista das evidências
- Sem marcas d’água ou artefatos modernos
- Formato JPEG, <5 MB

**Convenção de nomes:**

```text
CASE-AAAA-NNN/
├── documents/
│   ├── police-report-{casenumber}.pdf
│   ├── witness-{sobrenome}.pdf
│   ├── suspect-interview-{sobrenome}.pdf
│   └── forensics-{tipo}-{evidencia}.pdf
├── evidence/
│   ├── ev{NNN}-{descricao}-overview.jpg
│   ├── ev{NNN}-{descricao}-closeup.jpg
│   └── ev{NNN}-{descricao}-detail.jpg
└── suspects/
    ├── {sobrenome}-{nome}.jpg
    └── ...
```

---

## 4.9 Checklist de Validação do Caso

### Validação de conteúdo

**Consistência lógica:**

- [ ] Linha do tempo coerente
- [ ] Sem contradições entre evidências
- [ ] Perícias condizem com os itens físicos
- [ ] Horários de álibi batem com os eventos
- [ ] Localizações geográficas fazem sentido

**Solvabilidade:**

- [ ] Solução dedutível pelas evidências
- [ ] Evidências-chave presentes
- [ ] Nada depende de informação externa
- [ ] Múltiplas provas apontam para o culpado
- [ ] Motivo é descobrível

**Pistas falsas:**

- [ ] Suspeitos inocentes são plausíveis
- [ ] Evidências eventualmentе os inocentam
- [ ] Não são óbvios demais
- [ ] Não são convincentes demais (evitar frustração)

**Justiça:**

- [ ] Sem informações ocultas
- [ ] Sem saltos lógicos impossíveis
- [ ] Sem conhecimento obscuro obrigatório
- [ ] Solução segue as evidências

### Validação técnica

**`case.json`:**

- [ ] JSON válido
- [ ] Todos os campos obrigatórios presentes
- [ ] Tipos de dados corretos
- [ ] IDs únicos e consistentes
- [ ] Caminhos de arquivos existentes

**Assets:**

- [ ] Todos os arquivos referenciados existem
- [ ] Caminhos coincidem com o `case.json`
- [ ] Formatos corretos
- [ ] Tamanhos razoáveis
- [ ] Sem referências quebradas

**Perícias:**

- [ ] Durações plausíveis
- [ ] Resultados alinhados às evidências
- [ ] Modelos de laudos disponíveis
- [ ] Achados descobríveis pelos documentos

---

## 4.10 Fluxo de Criação do Caso

### Fase 1: Conceito (1-2 dias)

1. **Escolher o tipo de crime** (homicídio, furto etc.)
2. **Definir a vítima** (histórico, motivo para ser alvo)
3. **Desenhar o culpado** (motivo, método, oportunidade)
4. **Criar pistas falsas** (2-4 inocentes plausíveis)
5. **Esboçar evidências-chave** (o que prova a culpa)

### Fase 2: Estrutura (2-3 dias)

1. **Construir a linha do tempo** (cronologia do crime)
2. **Desenhar evidências** (físicas, biológicas, documentais)
3. **Planejar perícias** (que análises revelam o quê)
4. **Redigir a solução** (explicação completa)
5. **Criar o esqueleto do `case.json`**

### Fase 3: Criação de conteúdo (5-7 dias)

1. **Escrever documentos:**
   - Relatório policial
   - Depoimentos de testemunhas
   - Entrevistas de suspeitos
   - Documentos de contexto
   - Documentos financeiros/pessoais

2. **Produzir/obter fotos:**
   - Fotografia das evidências
   - Retratos de suspeitos
   - Fotos de locais
   - Foto da vítima

3. **Gerar laudos forenses:**
   - DNA
   - Balística
   - Impressões digitais
   - Outras análises

### Fase 4: Integração (1-2 dias)

1. **Completar o `case.json`** (todos os campos)
2. **Organizar a estrutura de pastas**
3. **Verificar todos os caminhos de arquivo**
4. **Criar README.md**
5. **Testar integridade dos dados**

### Fase 5: Testes (2-3 dias)

1. **Playtest interno** (é solucionável?)
2. **Checagem lógica** (há contradições?)
3. **Avaliação de dificuldade** (fácil/difícil demais?)
4. **Polimento** (typos, formatação)
5. **Validação final** (checklist)

**Tempo total:** 11-17 dias por caso

---

## 4.11 Exemplo de Caso

### CASE-2024-001: "Homicídio no Escritório Central"

**Resumo macro:**
Sócio mata o CEO por disputa financeira. Cena parece mistério de sala trancada, mas logs de acesso e DNA colocam o culpado no local.

**Por que funciona:**

1. **Motivo claro:** Desespero financeiro (dívida de US$500 mil)
2. **Evidências fortes:** DNA, balística e registros convergem em Torres
3. **Pistas falsas plausíveis:**
   - Esposa tem motivo (seguro), porém álibi sólido
   - Ex-funcionário tem motivo (vingança), mas testemunhas o cobrem
4. **Solvável:** Evidências independentes apontam para o mesmo culpado
5. **Dificuldade média:** Três suspeitos, provas claras, alguma distração

**Cadeia de evidências-chave:**
```text
Registros financeiros → Motivo (dívida de US$500 mil)
        +
Log de acesso → Oportunidade (na cena na janela do crime)
        +
DNA na cena → Presença física
        +
Balística → Arma registrada em seu nome
        +
Álibi falho → Sem explicação alternativa
        ↓
    CULPA PROVADA
```

**Como as pistas falsas se resolvem:**
- **Linda Chen:** CCTV confirma que não saiu de casa
- **David Park:** Várias testemunhas no bar confirmam o álibi

---

## 4.12 Estruturas Avançadas de Caso (futuro)

### Casos com múltiplos culpados

**Estrutura:**

- Dois suspeitos trabalhando juntos
- Papéis distintos (mentor x executor)
- Evidências precisam implicar ambos
- Solução exige identificar a dupla

**Exemplo:** Esposa planeja, matador de aluguel executa

### Casos reabertos (cold case)

**Estrutura:**

- Investigação original (há 20 anos)
- Novas evidências no presente
- Reavaliar conclusões antigas
- Passagem do tempo cria desafios únicos

**Exemplo:** Tecnologia de DNA inexistente em 1995 agora inocenta o suspeito original

### Casos de suspeito inocente

**Estrutura:**

- Tudo aponta para uma pessoa
- Mas ela é inocente
- É preciso encontrar pistas sutis do verdadeiro culpado
- Subversão deliberada de expectativa

**Exemplo:** Culpado planta evidências para incriminar outro

---

## 4.13 Localização

### Elementos traduzíveis

**Devem ser traduzidos:**

- Texto de UI
- Títulos de casos
- Descrições de casos
- Texto de documentos
- Texto dos laudos forenses
- Nomes de personagens (opcional)

**Não podem ser traduzidos:**

- Fotos de evidência (texto embutido)
- Documentos manuscritos
- Assinaturas

**Solução:**

- Criar PDFs separados por idioma
- Usar o mínimo possível de texto em fotos de evidência
- Fornecer guia de tradução para textos embutidos

---

## 4.14 Resumo

**Essenciais da estrutura:**

- `case.json` = fonte única da verdade
- **Mínimo:** 2 suspeitos, 5 documentos, 3 evidências, 1 tipo de perícia
- **Componentes:** crime, vítima, suspeitos, evidências, documentos, perícias, linha do tempo, solução
- **Dificuldade:** calculada a partir de suspeitos, documentos, força de pistas falsas, clareza do motivo e complexidade da linha do tempo
- **Assets:** PDFs, fotos, laudos (padrões de qualidade)
- **Produção:** 11-17 dias por caso (conceito → testes)

**Princípio central:**
Todo caso deve ser **solucionável por dedução** apenas com as evidências fornecidas, sem informação oculta ou saltos lógicos impossíveis.

---

**Próximo capítulo:** [05-NARRATIVA.md](05-NARRATIVA.md) – Diretrizes de escrita e tom

**Documentos relacionados:**
- [03-MECANICAS.md](03-MECANICAS.md) – Como os componentes do caso aparecem no jogo
- [09-ESQUEMA-DE-DADOS.md](09-ESQUEMA-DE-DADOS.md) – Implementação técnica do `case.json`
- [10-PRODUCAO-DE-CONTEUDO.md](10-PRODUCAO-DE-CONTEUDO.md) – Pipeline de produção de casos

---

**Histórico de revisões:**

| Data | Versão | Mudanças | Autor |
|------|--------|----------|-------|
| 13/11/2025 | 1.0 | Tradução completa para PT-BR | Assistente de IA |
