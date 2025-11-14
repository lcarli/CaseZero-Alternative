# Chapter 04 - Case Structure

**Game Design Document - CaseZero v3.0**  
**Last Updated:** November 13, 2025  
**Status:** ✅ Complete

---

## 4.1 Overview

This chapter defines the **anatomical structure of a case** - the components, formats, and relationships that make up a complete investigation. Every case in CaseZero follows this structure to ensure consistency, completeness, and investigability.

**Key Concepts:**
- **case.json** as single source of truth
- Required vs. optional components
- Evidence types and categorization
- Document templates and formats
- Forensic analysis types
- Difficulty balancing formulas
- Asset requirements checklist

---

## 4.2 Case Anatomy

### Minimum Viable Case

**Every case MUST have:**

```
CASE-YYYY-NNN/
├── case.json                 # Master data file
├── README.md                 # Human-readable case summary
├── evidence/                 # Physical evidence photos
│   ├── ev001-weapon.jpg
│   └── ev002-blood.jpg
├── documents/                # Investigation documents
│   ├── police-report.pdf
│   ├── witness-statement-1.pdf
│   └── suspect-interview-1.pdf
├── forensics/                # Forensic reports (generated)
│   ├── ballistics-report.pdf
│   └── dna-report.pdf
└── suspects/                 # Suspect photos
    ├── suspect-1.jpg
    └── suspect-2.jpg
```

### Complete Case Structure

**Full structure with optional elements:**

```
CASE-YYYY-NNN/
├── case.json                 # REQUIRED
├── README.md                 # REQUIRED
├── evidence/                 # REQUIRED (min 3 items)
│   ├── photos/
│   ├── documents/
│   └── metadata/
├── documents/                # REQUIRED (min 5)
│   ├── police/
│   ├── witnesses/
│   ├── suspects/
│   ├── forensics/
│   └── personal/
├── suspects/                 # REQUIRED (min 2)
│   ├── photos/
│   └── profiles/
├── forensics/                # REQUIRED (min 1 type)
│   ├── dna/
│   ├── ballistics/
│   ├── fingerprints/
│   └── toxicology/
├── witnesses/                # OPTIONAL
│   └── photos/
├── victim/                   # REQUIRED
│   ├── photo.jpg
│   └── background.pdf
├── memos/                    # OPTIONAL (future)
│   └── case-notes.txt
└── timeline/                 # OPTIONAL (auto-generated)
    └── timeline.json
```

---

## 4.3 case.json Schema

The master data file defining the entire case.

### Complete Schema

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

### Schema Field Requirements

**Required Top-Level Fields:**
```json
{
  "caseId": "string",              // CASE-YYYY-NNN format
  "version": "string",             // "3.0"
  "metadata": {},                  // Case metadata
  "crime": {},                     // Crime details
  "victim": {},                    // Victim information
  "suspects": [],                  // Array (min 2)
  "evidence": [],                  // Array (min 3)
  "documents": [],                 // Array (min 5)
  "forensicReports": [],           // Array (min 1)
  "timeline": [],                  // Array (min 3 events)
  "solution": {},                  // Correct solution
  "difficulty": {}                 // Difficulty metrics
}
```

---

## 4.4 Evidence Types & Categories

### Physical Evidence

**Weapons:**
- Firearms (guns, rifles)
- Bladed weapons (knives, swords)
- Blunt objects (bats, hammers)
- Improvised weapons

**Personal Items:**
- Wallets, purses
- Phones, electronics
- Jewelry
- Clothing
- Documents

**Tools:**
- Used in crime commission
- Breaking & entering tools
- Restraints

### Biological Evidence

**Samples:**
- Blood
- Hair/fibers
- Saliva
- Tissue
- Bodily fluids

**Analysis Types:**
- DNA profiling
- Blood typing
- Hair comparison
- Fiber analysis

### Trace Evidence

**Types:**
- Fingerprints (latent, patent)
- Footprints/shoe prints
- Tire tracks
- Paint chips
- Glass fragments
- Soil samples

### Documentary Evidence

**Types:**
- Handwritten notes
- Typed documents
- Forged papers
- Threatening letters
- Business records
- Personal correspondence

**Analysis:**
- Handwriting comparison
- Forgery detection
- Ink analysis
- Paper dating

### Digital Evidence (Future)

**Types:**
- Phone records
- Text messages
- Emails
- Computer files
- Photos/videos
- GPS data
- Social media

---

## 4.5 Document Types & Templates

### Police Report Template

**Structure:**
```
┌─────────────────────────────────────────────┐
│ METROPOLITAN POLICE DEPARTMENT              │
│ INCIDENT REPORT                             │
├─────────────────────────────────────────────┤
│ Case Number: 2023-0315                      │
│ Incident Type: Homicide                     │
│ Date/Time: March 15, 2023, 11:30 PM (est.) │
│ Location: 450 Market St, Floor 15           │
│ Reporting Officer: Martinez, Sarah (Badge)  │
│ Date Filed: March 16, 2023, 08:00 AM        │
├─────────────────────────────────────────────┤
│                                             │
│ SUMMARY:                                    │
│ At approximately 00:30 hours on 3/16/2023, │
│ this officer responded to report of...     │
│                                             │
│ SCENE DESCRIPTION:                          │
│ Victim found in private office, door       │
│ locked from inside...                       │
│                                             │
│ EVIDENCE COLLECTED:                         │
│ - Evidence #001: Firearm (.38 caliber)     │
│ - Evidence #002: Bullet (recovered)        │
│ ...                                         │
│                                             │
│ WITNESS INFORMATION:                        │
│ - Silva, John (Security Guard)             │
│ ...                                         │
│                                             │
│ INITIAL FINDINGS:                           │
│ Single gunshot wound to chest. Estimated   │
│ time of death 23:30. No forced entry...    │
│                                             │
│ [Officer Signature]                         │
└─────────────────────────────────────────────┘
```

**Key Elements:**
- Official header
- Case number
- Officer information
- Chronological narrative
- Scene description
- Evidence log
- Witness list
- Initial conclusions

### Witness Statement Template

**Structure:**
```
┌─────────────────────────────────────────────┐
│ METROPOLITAN POLICE DEPARTMENT              │
│ WITNESS STATEMENT                           │
├─────────────────────────────────────────────┤
│ Case Number: 2023-0315                      │
│ Witness Name: John Silva                    │
│ Date of Statement: March 16, 2023           │
│ Interviewing Officer: Martinez, S.          │
├─────────────────────────────────────────────┤
│                                             │
│ STATEMENT:                                  │
│                                             │
│ "I was making my rounds on the 12th floor  │
│ when I noticed the light still on in Mr.   │
│ Chen's office on 15. That was unusual      │
│ for past midnight. I took the elevator     │
│ up to check..."                             │
│                                             │
│ [Continues for 1-3 pages]                   │
│                                             │
│ I hereby declare that the above statement  │
│ is true and correct to the best of my      │
│ knowledge.                                  │
│                                             │
│ [Witness Signature]                         │
│ [Date]                                      │
└─────────────────────────────────────────────┘
```

### Suspect Interview Template

**Structure (Q&A Format):**
```
┌─────────────────────────────────────────────┐
│ METROPOLITAN POLICE DEPARTMENT              │
│ INTERVIEW TRANSCRIPT                        │
├─────────────────────────────────────────────┤
│ Case Number: 2023-0315                      │
│ Interviewee: Michael Torres                │
│ Date: March 17, 2023, 14:00                 │
│ Location: Police Station, Room 3            │
│ Interviewer: Detective Lisa Wong            │
│ Also Present: Attorney David Miller         │
├─────────────────────────────────────────────┤
│                                             │
│ DET. WONG: Mr. Torres, where were you on   │
│ the night of March 15th between 10 PM      │
│ and midnight?                               │
│                                             │
│ TORRES: I was home. Watching TV.            │
│                                             │
│ DET. WONG: Can anyone verify that?         │
│                                             │
│ TORRES: No, I live alone. My girlfriend    │
│ was out of town.                            │
│                                             │
│ DET. WONG: We have security logs showing   │
│ your access card used at the building at   │
│ 11:15 PM that night.                        │
│                                             │
│ TORRES: [Pauses] I... I don't remember     │
│ that.                                       │
│                                             │
│ [Continues for 3-4 pages]                   │
│                                             │
│ Interview concluded at 15:45.               │
│ [Signatures]                                │
└─────────────────────────────────────────────┘
```

**Key Elements:**
- Formal header
- All parties present
- Q&A format
- Exact quotes
- Note pauses/reactions
- Detective observations

### Forensic Report Template

**Structure:**
```
┌─────────────────────────────────────────────┐
│ METROPOLITAN FORENSICS LABORATORY           │
│ FORENSIC ANALYSIS REPORT                    │
├─────────────────────────────────────────────┤
│ Report Type: DNA Analysis                   │
│ Case Number: 2023-0315                      │
│ Evidence Number: EV-004                     │
│ Analyst: Dr. Sarah Kim, PhD                 │
│ Date Received: March 16, 2023               │
│ Date Analyzed: March 17, 2023               │
│ Report Date: March 17, 2023                 │
├─────────────────────────────────────────────┤
│                                             │
│ EVIDENCE DESCRIPTION:                       │
│ Blood sample collected from crime scene    │
│ near office door handle.                    │
│                                             │
│ ANALYSIS PERFORMED:                         │
│ DNA extraction and profiling using STR     │
│ analysis (16 loci).                         │
│                                             │
│ METHODOLOGY:                                │
│ Standard forensic DNA extraction protocol. │
│ PCR amplification. Capillary               │
│ electrophoresis. Profile comparison.        │
│                                             │
│ FINDINGS:                                   │
│ DNA profile obtained: [technical details]  │
│ Comparison run against known samples.      │
│                                             │
│ CONCLUSIONS:                                │
│ DNA profile matches Michael Torres         │
│ (reference sample) with 99.7% statistical  │
│ confidence. Probability of random match:   │
│ 1 in 5 billion.                             │
│                                             │
│ [Analyst Signature]                         │
│ [Lab Director Signature]                    │
└─────────────────────────────────────────────┘
```

### Financial Records Template

**Structure:**
```
┌─────────────────────────────────────────────┐
│ FIRST NATIONAL BANK                         │
│ ACCOUNT STATEMENT                           │
├─────────────────────────────────────────────┤
│ Account Holder: Michael Torres             │
│ Account Number: ****1234                    │
│ Statement Period: Jan 1 - Mar 15, 2023      │
├─────────────────────────────────────────────┤
│                                             │
│ Date       Description           Amount    │
│ ────────────────────────────────────────   │
│ 01/05      Opening Balance      $125,450   │
│ 01/15      Payroll Deposit      $12,500    │
│ 02/01      Wire Transfer OUT    -$50,000   │
│ 02/15      Payroll Deposit      $12,500    │
│ 02/28      Payment Due Notice   $500,000   │
│            (Loan #LN-4421)                  │
│ 03/01      INSUFFICIENT FUNDS               │
│ 03/10      Email - Buyout threat           │
│            from R. Chen                     │
│ 03/15      Closing Balance      $42,180    │
│                                             │
│ NOTES:                                      │
│ Outstanding loan: $500,000 (past due)      │
└─────────────────────────────────────────────┘
```

---

## 4.6 Forensic Analysis Types

### DNA Analysis

**Duration:** 24 hours  
**Applicable To:** Blood, hair, saliva, tissue, bodily fluids  
**Results Provided:**
- DNA profile (STR loci)
- Match to known suspects (% confidence)
- Database comparison (if no match)
- Statistical significance

**Report Sections:**
- Evidence description
- Methodology
- DNA profile data
- Comparison results
- Conclusions

**Use Cases:**
- Identify perpetrator from biological evidence
- Confirm suspect presence at scene
- Exclude innocent suspects
- Establish relationships

### Ballistics Analysis

**Duration:** 12 hours  
**Applicable To:** Firearms, bullets, casings  
**Results Provided:**
- Weapon identification
- Bullet matching (fired from specific weapon)
- Trajectory analysis
- Distance estimation
- Gunshot residue presence

**Report Sections:**
- Weapon specifications
- Bullet/casing examination
- Comparison microscopy results
- Trajectory findings
- Conclusions

### Fingerprint Analysis

**Duration:** 8 hours  
**Applicable To:** Latent prints on surfaces, objects, weapons  
**Results Provided:**
- Print quality assessment
- Pattern classification (arch, loop, whorl)
- Match to known suspects
- Points of comparison
- Confidence level

**Report Sections:**
- Print quality and location
- Enhancement methods used
- Comparison results
- Match confidence
- Conclusions

### Toxicology Analysis

**Duration:** 36 hours  
**Applicable To:** Blood, tissue, stomach contents  
**Results Provided:**
- Drug detection and identification
- Poison detection
- Alcohol concentration
- Prescription medications
- Time since ingestion (estimate)

**Report Sections:**
- Sample description
- Analytical methods (GC-MS, etc.)
- Substances detected
- Concentrations
- Interpretation and significance

### Trace Evidence Analysis

**Duration:** 16 hours  
**Applicable To:** Fibers, hair, paint, glass, soil  
**Results Provided:**
- Material identification
- Source comparison
- Transfer evidence confirmation
- Manufacturing origin (sometimes)

**Report Sections:**
- Evidence description
- Microscopic examination
- Chemical analysis
- Comparison results
- Conclusions

### Handwriting Analysis

**Duration:** 10 hours  
**Applicable To:** Handwritten documents, signatures  
**Results Provided:**
- Author identification
- Forgery detection
- Alteration detection
- Comparison to known samples

**Report Sections:**
- Document description
- Characteristics examined
- Known samples comparison
- Conclusions (definite/probable/inconclusive)

---

## 4.7 Difficulty Balancing

### Difficulty Factors

**Variables That Increase Difficulty:**

1. **Suspect Count**
   - Easy: 2-3 suspects
   - Medium: 4-5 suspects
   - Hard: 6-7 suspects
   - Expert: 8+ suspects

2. **Document Count**
   - Easy: 8-12 documents
   - Medium: 12-18 documents
   - Hard: 18-25 documents
   - Expert: 25+ documents

3. **Evidence Count**
   - Easy: 5-8 pieces
   - Medium: 8-12 pieces
   - Hard: 12-18 pieces
   - Expert: 18+ pieces

4. **Red Herring Strength**
   - Easy: Obvious red herrings, easy to eliminate
   - Medium: Plausible but evidence exonerates
   - Hard: Very plausible, requires careful analysis
   - Expert: Nearly as convincing as real culprit

5. **Motive Clarity**
   - Easy: Obvious and stated directly
   - Medium: Requires connecting documents
   - Hard: Hidden, requires inference
   - Expert: Multiple layers, misdirection

6. **Timeline Complexity**
   - Easy: Simple, clear sequence
   - Medium: Some gaps, requires reconstruction
   - Hard: Conflicting accounts, requires reconciliation
   - Expert: Multiple timelines, deliberate contradictions

7. **Forensic Complexity**
   - Easy: 1-2 forensic types, clear results
   - Medium: 3-4 types, some ambiguity
   - Hard: 5+ types, requires correlation
   - Expert: Complex interdependencies

### Difficulty Formula

```javascript
function calculateDifficulty(case) {
  let score = 0;
  
  // Suspect count (0-40 points)
  score += (case.suspects.length - 2) * 5;
  
  // Document count (0-30 points)
  score += Math.floor((case.documents.length - 8) / 2);
  
  // Evidence count (0-20 points)
  score += (case.evidence.length - 5) * 2;
  
  // Red herring strength (0-30 points)
  const redHerringScore = case.suspects
    .filter(s => !s.isGuilty && s.motive)
    .reduce((sum, s) => sum + s.plausibilityRating, 0);
  score += redHerringScore;
  
  // Motive clarity (0-20 points)
  // 0 = stated directly, 20 = deeply hidden
  score += case.solution.motiveObfuscation;
  
  // Timeline complexity (0-20 points)
  const timelineGaps = case.timeline.filter(e => !e.verified).length;
  score += timelineGaps * 2;
  
  // Forensic complexity (0-20 points)
  const forensicTypes = new Set(case.evidence
    .flatMap(e => e.forensicAnalysisAvailable.map(f => f.type))
  ).size;
  score += forensicTypes * 3;
  
  // Total: 0-180 points
  return score;
}

function getDifficultyRating(score) {
  if (score < 40) return "Easy";
  if (score < 80) return "Medium";
  if (score < 120) return "Hard";
  return "Expert";
}
```

### Difficulty Target Ranges

**Easy (0-40 points):**
- Time: 2-4 hours
- First-attempt success: 50-60%
- Rank required: Rookie

**Medium (40-80 points):**
- Time: 4-6 hours
- First-attempt success: 30-40%
- Rank required: Detective III

**Hard (80-120 points):**
- Time: 6-8 hours
- First-attempt success: 20-30%
- Rank required: Detective I

**Expert (120-180 points):**
- Time: 8-12 hours
- First-attempt success: 10-20%
- Rank required: Lead Detective

---

## 4.8 Case Asset Checklist

### Required Assets Per Case

**Documents (PDFs):**
- [ ] Police report (3-5 pages)
- [ ] 2+ witness statements (1-3 pages each)
- [ ] 2+ suspect interviews (2-4 pages each)
- [ ] Background documents (variable)
- [ ] Forensic report templates (for generation)

**Evidence Photos (JPGs):**
- [ ] 3+ evidence items (2-3 angles each)
- [ ] High resolution (2000x1500 minimum)
- [ ] Proper evidence tags visible
- [ ] Scale reference (ruler) included

**Character Photos (JPGs):**
- [ ] Victim photo (headshot)
- [ ] 2+ suspect photos (headshots)
- [ ] Witness photos (optional)

**Location Photos (Optional):**
- [ ] Crime scene exterior
- [ ] Crime scene interior
- [ ] Relevant locations

### Asset Quality Standards

**PDFs:**
- Professional formatting
- Realistic headers/footers
- No typos or errors
- 1-5 MB file size
- Searchable text (not scanned images)

**Photos:**
- 2000x1500 minimum resolution
- Clear, well-lit
- Realistic evidence presentation
- No modern watermarks/artifacts
- JPEG format, <5 MB

**Naming Convention:**
```
CASE-YYYY-NNN/
├── documents/
│   ├── police-report-{casenumber}.pdf
│   ├── witness-{lastname}.pdf
│   ├── suspect-interview-{lastname}.pdf
│   └── forensics-{type}-{evidence}.pdf
├── evidence/
│   ├── ev{NNN}-{description}-overview.jpg
│   ├── ev{NNN}-{description}-closeup.jpg
│   └── ev{NNN}-{description}-detail.jpg
└── suspects/
    ├── {lastname}-{firstname}.jpg
    └── ...
```

---

## 4.9 Case Validation Checklist

### Content Validation

**Logical Consistency:**
- [ ] Timeline is internally consistent
- [ ] No contradictions in evidence
- [ ] Forensics match physical evidence
- [ ] Alibi times align with events
- [ ] Geographic locations make sense

**Solvability:**
- [ ] Solution can be deduced from evidence
- [ ] Key evidence is present
- [ ] No information required outside case files
- [ ] Multiple evidence pieces point to culprit
- [ ] Motive is discoverable

**Red Herrings:**
- [ ] Innocent suspects are plausible
- [ ] Evidence eventually exonerates them
- [ ] Not TOO obvious they're innocent
- [ ] Not TOO convincing (frustrating)

**Fairness:**
- [ ] No hidden information required
- [ ] No impossible leaps of logic
- [ ] No obscure knowledge needed
- [ ] Solution follows evidence

### Technical Validation

**case.json:**
- [ ] Valid JSON syntax
- [ ] All required fields present
- [ ] Correct data types
- [ ] IDs are unique and consistent
- [ ] File paths exist

**Assets:**
- [ ] All referenced files exist
- [ ] File paths match case.json
- [ ] Correct file formats
- [ ] Reasonable file sizes
- [ ] No broken references

**Forensics:**
- [ ] Duration values reasonable
- [ ] Results match evidence
- [ ] Report templates exist
- [ ] Findings are discoverable

---

## 4.10 Case Creation Workflow

### Phase 1: Concept (1-2 days)

1. **Choose crime type** (homicide, theft, etc.)
2. **Define victim** (background, why targeted)
3. **Design culprit** (motive, method, opportunity)
4. **Create red herrings** (2-4 innocent suspects)
5. **Outline key evidence** (what proves guilt)

### Phase 2: Structure (2-3 days)

1. **Build timeline** (crime chronology)
2. **Design evidence** (physical, biological, documentary)
3. **Plan forensics** (what analyses reveal what)
4. **Write solution** (complete explanation)
5. **Create case.json skeleton**

### Phase 3: Content Creation (5-7 days)

1. **Write documents:**
   - Police report
   - Witness statements
   - Suspect interviews
   - Background documents
   - Financial/personal docs

2. **Create/source photos:**
   - Evidence photography
   - Suspect headshots
   - Location photos
   - Victim photo

3. **Generate forensic reports:**
   - DNA analysis
   - Ballistics
   - Fingerprints
   - Other analyses

### Phase 4: Integration (1-2 days)

1. **Complete case.json** (all fields)
2. **Organize file structure**
3. **Verify all file paths**
4. **Create README.md**
5. **Test data integrity**

### Phase 5: Testing (2-3 days)

1. **Internal playtest** (can it be solved?)
2. **Logic check** (contradictions?)
3. **Difficulty assessment** (too easy/hard?)
4. **Polish content** (typos, formatting)
5. **Final validation** (checklist)

**Total Time:** 11-17 days per case

---

## 4.11 Example Case Breakdown

### CASE-2024-001: "The Downtown Office Murder"

**High-Level Summary:**
Business partner murders CEO over financial dispute. Staged as potential locked-room mystery but access logs and DNA place him at scene.

**Why This Works:**

1. **Clear Motive:** Financial desperation (owes $500k)
2. **Strong Evidence:** DNA, ballistics, access logs all point to Torres
3. **Plausible Red Herrings:** 
   - Wife has insurance motive but solid alibi
   - Fired employee has revenge motive but witnesses confirm alibi
4. **Solvable:** Multiple independent evidence pieces converge on Torres
5. **Medium Difficulty:** 3 suspects, clear evidence, some misdirection

**Key Evidence Chain:**
```
Financial Records → Motive (owes $500k)
        +
Access Log → Opportunity (at scene during murder)
        +
DNA at Scene → Physical presence
        +
Ballistics → Weapon registered to him
        +
Failed Alibi → No alternative explanation
        ↓
    GUILT PROVEN
```

**Red Herring Resolution:**
- **Linda Chen:** CCTV proves she never left home
- **David Park:** Multiple bar witnesses confirm alibi

---

## 4.12 Advanced Case Structures (Future)

### Multi-Culprit Cases

**Structure:**
- Two suspects working together
- Different roles (mastermind vs executor)
- Evidence must implicate both
- Solution requires identifying both

**Example:** Wife plans murder, hired killer executes

### Cold Case Evolution

**Structure:**
- Original investigation (20 years ago)
- New evidence discovered (present day)
- Must re-examine old conclusions
- Time passage creates unique challenges

**Example:** DNA technology not available in 1995, now exonerates original suspect

### Innocent Suspect Cases

**Structure:**
- All evidence points to one person
- But they're actually innocent
- Must find real culprit through subtle clues
- Reversal of expectations

**Example:** Framed by actual killer who planted evidence

---

## 4.13 Localization Considerations

### Translatable Elements

**Must Be Translated:**
- UI text
- Case titles
- Case descriptions
- Document text
- Forensic report text
- Character names (optional)

**Cannot Be Translated:**
- Evidence photos (text in images)
- Handwritten documents
- Signatures

**Solution:**
- Create separate document PDFs per language
- Evidence photos use minimal text
- Provide translation guide for embedded text

---

## 4.14 Summary

**Case Structure Essentials:**

- **case.json** = single source of truth
- **Minimum:** 2 suspects, 5 documents, 3 evidence, 1 forensic type
- **Components:** Crime, victim, suspects, evidence, documents, forensics, timeline, solution
- **Difficulty:** Calculated from suspect count, document count, red herring strength, motive clarity, timeline complexity
- **Assets:** PDFs, photos, reports (quality standards)
- **Creation:** 11-17 days per case (concept → testing)

**Core Principle:**
Every case must be **solvable through deduction** using only the provided evidence, with no hidden information or impossible leaps of logic.

---

**Next Chapter:** [05-NARRATIVE.md](05-NARRATIVE.md) - Writing guidelines and tone

**Related Documents:**
- [03-MECHANICS.md](03-MECHANICS.md) - How case components are presented
- [09-DATA-SCHEMA.md](09-DATA-SCHEMA.md) - Technical implementation of case.json
- [10-CONTENT-PIPELINE.md](10-CONTENT-PIPELINE.md) - Case generation workflow

---

**Revision History:**

| Date | Version | Changes | Author |
|------|---------|---------|--------|
| 2025-11-13 | 1.0 | Initial complete draft | AI Assistant |
