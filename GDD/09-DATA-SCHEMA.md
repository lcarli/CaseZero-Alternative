# Chapter 09 - Data Schema & Models

**Game Design Document - CaseZero v3.0**  
**Last Updated:** November 14, 2025  
**Status:** ✅ Complete

---

## 9.1 Overview

This chapter defines the **complete data schema, models, and validation rules** for CaseZero v3.0. It covers the case.json structure, C# backend models, TypeScript frontend interfaces, and database tables.

**Key Concepts:**
- case.json as single source of truth
- Strong typing across frontend/backend
- Validation at multiple layers
- Extensibility for future features

---

## 9.2 Schema Philosophy

### Design Principles

**1. Single Source of Truth**
- case.json contains all static case data
- Database stores runtime/session data
- No duplication between sources

**2. Type Safety**
- TypeScript interfaces for frontend
- C# models for backend
- JSON Schema for validation

**3. Forward Compatibility**
- Optional fields for future features
- Version field for migrations
- Backward-compatible changes only

**4. Human-Readable**
- Clear property names
- ISO-8601 dates
- Minimal nesting

---

## 9.3 case.json Complete Schema

### Root Object

```json
{
  "schemaVersion": "3.0",
  "metadata": { /* CaseMetadata */ },
  "victim": { /* Victim */ },
  "crime": { /* Crime */ },
  "suspects": [ /* Suspect[] */ ],
  "evidence": [ /* Evidence[] */ ],
  "documents": [ /* Document[] */ ],
  "forensicAnalyses": [ /* ForensicAnalysis[] */ ],
  "timeline": [ /* TimelineEvent[] */ ],
  "solution": { /* Solution */ }
}
```

### CaseMetadata

```json
{
  "caseId": "CASE-2024-001",
  "title": "The Downtown Office Murder",
  "difficulty": "Medium",
  "estimatedTimeHours": 4.5,
  "tags": ["homicide", "financial", "workplace"],
  "createdDate": "2024-11-01",
  "lastModified": "2024-11-10",
  "author": "CaseZero Writing Team",
  "status": "Published"
}
```

**Field Descriptions:**
- `caseId` (string, required): Unique identifier, format `CASE-YYYY-###`
- `title` (string, required): Case display name
- `difficulty` (enum, required): `Easy` | `Medium` | `Hard` | `Expert`
- `estimatedTimeHours` (number, required): Expected solve time (1.0-12.0 hours)
- `tags` (string[], optional): Search/filter tags
- `createdDate` (string, required): ISO-8601 date `YYYY-MM-DD`
- `lastModified` (string, required): ISO-8601 date `YYYY-MM-DD`
- `author` (string, optional): Content creator attribution
- `status` (enum, required): `Draft` | `Published` | `Archived`

### Victim

```json
{
  "name": "Marcus Coleman",
  "age": 32,
  "occupation": "Software Engineer",
  "background": "Recently promoted to senior engineer at TechCorp. Married with one child. Known for being meticulous and professional.",
  "photoUrl": "assets/CASE-2024-001/victim-photo.jpg",
  "lastSeenDateTime": "2024-10-15T22:30:00Z",
  "lastSeenLocation": "TechCorp Office, 5th Floor"
}
```

**Field Descriptions:**
- `name` (string, required): Full legal name
- `age` (number, required): Age at time of crime
- `occupation` (string, required): Job/profession
- `background` (string, required): 2-3 sentences, personal context
- `photoUrl` (string, optional): Relative path from case root
- `lastSeenDateTime` (string, optional): ISO-8601 datetime with timezone
- `lastSeenLocation` (string, optional): Last known location

### Crime

```json
{
  "type": "Homicide",
  "subtype": "Firearm",
  "location": {
    "address": "1247 Tech Plaza Drive, Suite 500",
    "city": "Riverside",
    "state": "CA",
    "zipCode": "92501",
    "description": "Downtown high-rise office building, 5th floor corner office"
  },
  "dateTime": "2024-10-16T01:15:00Z",
  "discoveryDateTime": "2024-10-16T08:00:00Z",
  "description": "Victim found deceased in office with single gunshot wound to chest. Office ransacked, papers scattered. No forced entry detected.",
  "sceneDiagram": "assets/CASE-2024-001/scene-diagram.jpg"
}
```

**Field Descriptions:**
- `type` (enum, required): `Homicide` | `Assault` | `Theft` | `Fraud` | `Arson` | `Other`
- `subtype` (string, optional): Specific method (e.g., "Firearm", "Blunt Force", "Poison")
- `location.address` (string, required): Street address
- `location.city` (string, required): City name
- `location.state` (string, required): State/province code
- `location.zipCode` (string, optional): Postal code
- `location.description` (string, required): Contextual details
- `dateTime` (string, required): ISO-8601 datetime, estimated time of crime
- `discoveryDateTime` (string, required): ISO-8601 datetime, when crime discovered
- `description` (string, required): 3-5 sentences, initial scene overview
- `sceneDiagram` (string, optional): Path to floor plan/diagram

### Suspect

```json
{
  "suspectId": "SUSP-001",
  "name": "David Reynolds",
  "age": 45,
  "occupation": "Business Partner",
  "relationship": "Business partner at TechCorp startup division",
  "photoUrl": "assets/CASE-2024-001/suspects/reynolds.jpg",
  "background": "Co-founded startup with victim 3 years ago. Recently disagreed over company direction. Financial difficulties.",
  "alibi": "Claims he was home alone the night of the murder, watching TV. No witnesses.",
  "motive": "Financial disputes over company ownership. Recent audit revealed discrepancies.",
  "suspicionLevel": "High"
}
```

**Field Descriptions:**
- `suspectId` (string, required): Unique ID, format `SUSP-###`
- `name` (string, required): Full name
- `age` (number, required): Current age
- `occupation` (string, required): Job/role
- `relationship` (string, required): Connection to victim
- `photoUrl` (string, optional): Headshot photo path
- `background` (string, required): 2-3 sentences, context
- `alibi` (string, required): Claimed whereabouts during crime
- `motive` (string, required): Potential reason for crime
- `suspicionLevel` (enum, optional): `Low` | `Medium` | `High` (for internal use)

### Evidence

```json
{
  "evidenceId": "EV-001",
  "name": "9mm Handgun",
  "type": "Physical",
  "subtype": "Weapon",
  "description": "Semi-automatic 9mm pistol, Glock 19. Serial number filed off. Found in dumpster 2 blocks from scene.",
  "collectionLocation": "Dumpster, 1300 block of Tech Plaza Drive",
  "collectionDateTime": "2024-10-16T10:30:00Z",
  "photos": [
    "assets/CASE-2024-001/evidence/ev001-1.jpg",
    "assets/CASE-2024-001/evidence/ev001-2.jpg",
    "assets/CASE-2024-001/evidence/ev001-3.jpg"
  ],
  "forensicAnalysisAvailable": ["Ballistics", "Fingerprints"]
}
```

**Field Descriptions:**
- `evidenceId` (string, required): Unique ID, format `EV-###`
- `name` (string, required): Short descriptive name
- `type` (enum, required): `Physical` | `Biological` | `Trace` | `Documentary` | `Digital`
- `subtype` (string, optional): Specific category (e.g., "Weapon", "Clothing", "Blood")
- `description` (string, required): Detailed description, 2-4 sentences
- `collectionLocation` (string, required): Where evidence was found
- `collectionDateTime` (string, required): ISO-8601 datetime of collection
- `photos` (string[], optional): Array of photo paths (multiple angles)
- `forensicAnalysisAvailable` (string[], required): Analysis types applicable to this evidence

### Document

```json
{
  "documentId": "DOC-001",
  "type": "PoliceReport",
  "title": "Initial Incident Report",
  "description": "First responding officer's report of crime scene",
  "author": "Officer Sarah Martinez, Badge #2847",
  "dateCreated": "2024-10-16",
  "pageCount": 3,
  "filePath": "assets/CASE-2024-001/documents/police-report-001.pdf",
  "relatedEvidence": ["EV-001", "EV-004", "EV-007"],
  "keyClues": ["Victim found at desk", "No forced entry", "Office ransacked"]
}
```

**Field Descriptions:**
- `documentId` (string, required): Unique ID, format `DOC-###`
- `type` (enum, required): `PoliceReport` | `WitnessStatement` | `SuspectInterview` | `ForensicReport` | `FinancialRecord` | `Letter` | `Other`
- `title` (string, required): Document display name
- `description` (string, required): 1-2 sentence summary
- `author` (string, required): Who created the document
- `dateCreated` (string, required): ISO-8601 date `YYYY-MM-DD`
- `pageCount` (number, required): Number of pages (1-10 typical)
- `filePath` (string, required): Path to PDF file
- `relatedEvidence` (string[], optional): Evidence IDs mentioned in document
- `keyClues` (string[], optional): Important facts for solving (internal use)

### ForensicAnalysis

```json
{
  "analysisId": "FA-001",
  "type": "Ballistics",
  "evidenceId": "EV-001",
  "durationHours": 12,
  "description": "Ballistics analysis of firearm and bullet recovered from victim",
  "resultDocument": {
    "documentId": "DOC-015",
    "title": "Ballistics Report - EV-001",
    "filePath": "assets/CASE-2024-001/forensics/ballistics-ev001.pdf"
  },
  "keyFindings": [
    "Bullet matches 9mm Glock rifling pattern",
    "Weapon recently fired (gunpowder residue present)",
    "Serial number removed via filing"
  ]
}
```

**Field Descriptions:**
- `analysisId` (string, required): Unique ID, format `FA-###`
- `type` (enum, required): `DNA` | `Ballistics` | `Fingerprints` | `Toxicology` | `Trace` | `Handwriting`
- `evidenceId` (string, required): Evidence item being analyzed
- `durationHours` (number, required): Real-time hours to complete (8-36)
- `description` (string, required): What the analysis examines
- `resultDocument.documentId` (string, required): Document ID of report
- `resultDocument.title` (string, required): Report title
- `resultDocument.filePath` (string, required): Path to report PDF
- `keyFindings` (string[], required): 2-5 critical facts from analysis

### TimelineEvent

```json
{
  "eventId": "TL-001",
  "dateTime": "2024-10-15T22:30:00Z",
  "title": "Victim Last Seen Alive",
  "description": "Marcus Coleman seen leaving TechCorp office by night security guard",
  "location": "TechCorp Office Building Lobby",
  "source": "Witness Statement - Security Guard",
  "participants": ["Marcus Coleman", "Security Guard Johnson"],
  "significance": "High"
}
```

**Field Descriptions:**
- `eventId` (string, required): Unique ID, format `TL-###`
- `dateTime` (string, required): ISO-8601 datetime with timezone
- `title` (string, required): Short event name
- `description` (string, required): 1-2 sentences, what happened
- `location` (string, required): Where event occurred
- `source` (string, required): How we know this (document, witness, forensics)
- `participants` (string[], optional): People involved
- `significance` (enum, optional): `Low` | `Medium` | `High` (for internal use)

### Solution

```json
{
  "culprit": "SUSP-001",
  "motive": "David Reynolds embezzled $500,000 from company. Marcus discovered discrepancy during audit review. Reynolds feared exposure would lead to prison.",
  "method": "Reynolds used stolen building keycard to enter office after hours. Shot victim once in chest with 9mm Glock. Ransacked office to stage robbery.",
  "timeOfCrime": "2024-10-16T01:15:00Z",
  "keyEvidence": [
    "EV-001", // 9mm Glock with Reynolds' fingerprints
    "EV-004", // Gunshot residue on Reynolds' jacket
    "EV-007", // Financial records showing embezzlement
    "DOC-009", // Bank statements proving motive
    "FA-002"   // DNA evidence placing Reynolds at scene
  ],
  "timeline": [
    "2024-10-15T23:45:00Z - Reynolds enters building with stolen keycard",
    "2024-10-16T01:00:00Z - Reynolds confronts victim in office",
    "2024-10-16T01:15:00Z - Shooting occurs",
    "2024-10-16T01:30:00Z - Reynolds stages robbery, ransacks office",
    "2024-10-16T01:45:00Z - Reynolds disposes of weapon in dumpster"
  ],
  "alternativeExplanations": [
    "SUSP-002 had motive (ex-girlfriend jealousy) but alibi confirmed by traffic cameras",
    "SUSP-003 had opportunity (office neighbor) but no forensic evidence linking to crime"
  ]
}
```

**Field Descriptions:**
- `culprit` (string, required): Suspect ID of guilty party
- `motive` (string, required): 3-5 sentences, why they committed the crime
- `method` (string, required): 3-5 sentences, how they committed the crime
- `timeOfCrime` (string, required): ISO-8601 datetime, precise time if known
- `keyEvidence` (string[], required): Evidence/document IDs that prove guilt (5-10 items)
- `timeline` (string[], required): Chronological sequence of culprit's actions
- `alternativeExplanations` (string[], optional): Why other suspects are innocent

---

## 9.4 TypeScript Interfaces (Frontend)

### Core Types

```typescript
// enums.ts
export enum Difficulty {
  Easy = 'Easy',
  Medium = 'Medium',
  Hard = 'Hard',
  Expert = 'Expert'
}

export enum CrimeType {
  Homicide = 'Homicide',
  Assault = 'Assault',
  Theft = 'Theft',
  Fraud = 'Fraud',
  Arson = 'Arson',
  Other = 'Other'
}

export enum EvidenceType {
  Physical = 'Physical',
  Biological = 'Biological',
  Trace = 'Trace',
  Documentary = 'Documentary',
  Digital = 'Digital'
}

export enum DocumentType {
  PoliceReport = 'PoliceReport',
  WitnessStatement = 'WitnessStatement',
  SuspectInterview = 'SuspectInterview',
  ForensicReport = 'ForensicReport',
  FinancialRecord = 'FinancialRecord',
  Letter = 'Letter',
  Other = 'Other'
}

export enum ForensicAnalysisType {
  DNA = 'DNA',
  Ballistics = 'Ballistics',
  Fingerprints = 'Fingerprints',
  Toxicology = 'Toxicology',
  Trace = 'Trace',
  Handwriting = 'Handwriting'
}

export enum CaseStatus {
  Draft = 'Draft',
  Published = 'Published',
  Archived = 'Archived'
}

export enum SessionStatus {
  Active = 'Active',
  Solved = 'Solved',
  Failed = 'Failed'
}
```

### Case Structure

```typescript
// case.types.ts
export interface Case {
  schemaVersion: string;
  metadata: CaseMetadata;
  victim: Victim;
  crime: Crime;
  suspects: Suspect[];
  evidence: Evidence[];
  documents: Document[];
  forensicAnalyses: ForensicAnalysis[];
  timeline: TimelineEvent[];
  solution: Solution;
}

export interface CaseMetadata {
  caseId: string;
  title: string;
  difficulty: Difficulty;
  estimatedTimeHours: number;
  tags?: string[];
  createdDate: string; // ISO-8601 date
  lastModified: string; // ISO-8601 date
  author?: string;
  status: CaseStatus;
}

export interface Victim {
  name: string;
  age: number;
  occupation: string;
  background: string;
  photoUrl?: string;
  lastSeenDateTime?: string; // ISO-8601 datetime
  lastSeenLocation?: string;
}

export interface Crime {
  type: CrimeType;
  subtype?: string;
  location: Location;
  dateTime: string; // ISO-8601 datetime
  discoveryDateTime: string; // ISO-8601 datetime
  description: string;
  sceneDiagram?: string;
}

export interface Location {
  address: string;
  city: string;
  state: string;
  zipCode?: string;
  description: string;
}

export interface Suspect {
  suspectId: string;
  name: string;
  age: number;
  occupation: string;
  relationship: string;
  photoUrl?: string;
  background: string;
  alibi: string;
  motive: string;
  suspicionLevel?: 'Low' | 'Medium' | 'High';
}

export interface Evidence {
  evidenceId: string;
  name: string;
  type: EvidenceType;
  subtype?: string;
  description: string;
  collectionLocation: string;
  collectionDateTime: string; // ISO-8601 datetime
  photos?: string[];
  forensicAnalysisAvailable: string[];
}

export interface Document {
  documentId: string;
  type: DocumentType;
  title: string;
  description: string;
  author: string;
  dateCreated: string; // ISO-8601 date
  pageCount: number;
  filePath: string;
  relatedEvidence?: string[];
  keyClues?: string[];
}

export interface ForensicAnalysis {
  analysisId: string;
  type: ForensicAnalysisType;
  evidenceId: string;
  durationHours: number;
  description: string;
  resultDocument: {
    documentId: string;
    title: string;
    filePath: string;
  };
  keyFindings: string[];
}

export interface TimelineEvent {
  eventId: string;
  dateTime: string; // ISO-8601 datetime
  title: string;
  description: string;
  location: string;
  source: string;
  participants?: string[];
  significance?: 'Low' | 'Medium' | 'High';
}

export interface Solution {
  culprit: string; // Suspect ID
  motive: string;
  method: string;
  timeOfCrime: string; // ISO-8601 datetime
  keyEvidence: string[]; // Evidence/Document IDs
  timeline: string[];
  alternativeExplanations?: string[];
}
```

### Session & Runtime Data

```typescript
// session.types.ts
export interface CaseSession {
  sessionId: string;
  userId: string;
  caseId: string;
  status: SessionStatus;
  startedAt: string; // ISO-8601 datetime
  completedAt?: string; // ISO-8601 datetime
  totalTimeMinutes: number;
  submissionAttempts: number;
  notesText?: string;
}

export interface ForensicRequest {
  requestId: string;
  sessionId: string;
  evidenceId: string;
  analysisType: ForensicAnalysisType;
  requestedAt: string; // ISO-8601 datetime
  completesAt: string; // ISO-8601 datetime
  completedAt?: string; // ISO-8601 datetime
  status: 'Pending' | 'Completed' | 'Cancelled';
  resultBlobPath?: string;
}

export interface CaseSubmission {
  submissionId: string;
  sessionId: string;
  submittedAt: string; // ISO-8601 datetime
  culpritSelected: string;
  motiveExplanation: string;
  methodExplanation: string;
  evidenceSelected: string[];
  isCorrect: boolean;
  xpAwarded: number;
  feedbackJson: SubmissionFeedback;
}

export interface SubmissionFeedback {
  summary: string;
  culpritCorrect: boolean;
  motiveQuality?: 'poor' | 'adequate' | 'good' | 'thorough';
  keyEvidenceCited: boolean;
  hints?: string[];
}
```

### User & Progression

```typescript
// user.types.ts
export enum DetectiveRank {
  Rookie = 'Rookie',
  DetectiveIII = 'Detective III',
  DetectiveII = 'Detective II',
  DetectiveI = 'Detective I',
  LeadDetective = 'Lead Detective',
  SeniorDetective = 'Senior Detective',
  ChiefDetective = 'Chief Detective',
  MasterDetective = 'Master Detective'
}

export interface User {
  userId: string;
  username: string;
  email: string;
  createdAt: string;
  lastLoginAt?: string;
}

export interface UserProgress {
  userId: string;
  currentRank: DetectiveRank;
  totalXP: number;
  casesSolved: number;
  casesFailed: number;
  firstAttemptSuccesses: number;
  totalInvestigationMinutes: number;
}

export interface UserStats {
  overall: {
    casesSolved: number;
    casesFailed: number;
    successRate: number;
    totalTimeHours: number;
    avgTimePerCase: number;
  };
  byDifficulty: {
    [key in Difficulty]: {
      solved: number;
      failed: number;
      successRate: number;
      firstAttemptSuccessRate: number;
      avgTimeHours: number;
    };
  };
  forensics: {
    percentUsed: number;
    avgAnalysesPerCase: number;
  };
}
```

---

## 9.5 C# Models (Backend)

### Case Structure

```csharp
// Models/Case/CaseData.cs
namespace CaseZero.Models.Case
{
    public class CaseData
    {
        public string SchemaVersion { get; set; } = "3.0";
        public CaseMetadata Metadata { get; set; }
        public Victim Victim { get; set; }
        public Crime Crime { get; set; }
        public List<Suspect> Suspects { get; set; } = new();
        public List<Evidence> Evidence { get; set; } = new();
        public List<Document> Documents { get; set; } = new();
        public List<ForensicAnalysis> ForensicAnalyses { get; set; } = new();
        public List<TimelineEvent> Timeline { get; set; } = new();
        public Solution Solution { get; set; }
    }

    public class CaseMetadata
    {
        [Required, MaxLength(50)]
        public string CaseId { get; set; }
        
        [Required, MaxLength(255)]
        public string Title { get; set; }
        
        [Required]
        public Difficulty Difficulty { get; set; }
        
        [Range(1.0, 12.0)]
        public decimal EstimatedTimeHours { get; set; }
        
        public List<string>? Tags { get; set; }
        
        [Required]
        public DateOnly CreatedDate { get; set; }
        
        [Required]
        public DateOnly LastModified { get; set; }
        
        public string? Author { get; set; }
        
        [Required]
        public CaseStatus Status { get; set; }
    }

    public class Victim
    {
        [Required, MaxLength(255)]
        public string Name { get; set; }
        
        [Range(1, 120)]
        public int Age { get; set; }
        
        [Required, MaxLength(255)]
        public string Occupation { get; set; }
        
        [Required, MaxLength(1000)]
        public string Background { get; set; }
        
        public string? PhotoUrl { get; set; }
        
        public DateTime? LastSeenDateTime { get; set; }
        
        public string? LastSeenLocation { get; set; }
    }

    public class Crime
    {
        [Required]
        public CrimeType Type { get; set; }
        
        public string? Subtype { get; set; }
        
        [Required]
        public Location Location { get; set; }
        
        [Required]
        public DateTime DateTime { get; set; }
        
        [Required]
        public DateTime DiscoveryDateTime { get; set; }
        
        [Required, MaxLength(2000)]
        public string Description { get; set; }
        
        public string? SceneDiagram { get; set; }
    }

    public class Location
    {
        [Required, MaxLength(255)]
        public string Address { get; set; }
        
        [Required, MaxLength(100)]
        public string City { get; set; }
        
        [Required, MaxLength(50)]
        public string State { get; set; }
        
        public string? ZipCode { get; set; }
        
        [Required, MaxLength(500)]
        public string Description { get; set; }
    }

    public class Suspect
    {
        [Required, MaxLength(50)]
        public string SuspectId { get; set; }
        
        [Required, MaxLength(255)]
        public string Name { get; set; }
        
        [Range(1, 120)]
        public int Age { get; set; }
        
        [Required, MaxLength(255)]
        public string Occupation { get; set; }
        
        [Required, MaxLength(500)]
        public string Relationship { get; set; }
        
        public string? PhotoUrl { get; set; }
        
        [Required, MaxLength(1000)]
        public string Background { get; set; }
        
        [Required, MaxLength(1000)]
        public string Alibi { get; set; }
        
        [Required, MaxLength(1000)]
        public string Motive { get; set; }
        
        public SuspicionLevel? SuspicionLevel { get; set; }
    }

    public class Evidence
    {
        [Required, MaxLength(50)]
        public string EvidenceId { get; set; }
        
        [Required, MaxLength(255)]
        public string Name { get; set; }
        
        [Required]
        public EvidenceType Type { get; set; }
        
        public string? Subtype { get; set; }
        
        [Required, MaxLength(2000)]
        public string Description { get; set; }
        
        [Required, MaxLength(500)]
        public string CollectionLocation { get; set; }
        
        [Required]
        public DateTime CollectionDateTime { get; set; }
        
        public List<string>? Photos { get; set; }
        
        [Required]
        public List<string> ForensicAnalysisAvailable { get; set; } = new();
    }

    public class Document
    {
        [Required, MaxLength(50)]
        public string DocumentId { get; set; }
        
        [Required]
        public DocumentType Type { get; set; }
        
        [Required, MaxLength(255)]
        public string Title { get; set; }
        
        [Required, MaxLength(1000)]
        public string Description { get; set; }
        
        [Required, MaxLength(255)]
        public string Author { get; set; }
        
        [Required]
        public DateOnly DateCreated { get; set; }
        
        [Range(1, 50)]
        public int PageCount { get; set; }
        
        [Required, MaxLength(500)]
        public string FilePath { get; set; }
        
        public List<string>? RelatedEvidence { get; set; }
        
        public List<string>? KeyClues { get; set; }
    }

    public class ForensicAnalysis
    {
        [Required, MaxLength(50)]
        public string AnalysisId { get; set; }
        
        [Required]
        public ForensicAnalysisType Type { get; set; }
        
        [Required, MaxLength(50)]
        public string EvidenceId { get; set; }
        
        [Range(1, 72)]
        public int DurationHours { get; set; }
        
        [Required, MaxLength(1000)]
        public string Description { get; set; }
        
        [Required]
        public ForensicResultDocument ResultDocument { get; set; }
        
        [Required, MinLength(1)]
        public List<string> KeyFindings { get; set; } = new();
    }

    public class ForensicResultDocument
    {
        [Required, MaxLength(50)]
        public string DocumentId { get; set; }
        
        [Required, MaxLength(255)]
        public string Title { get; set; }
        
        [Required, MaxLength(500)]
        public string FilePath { get; set; }
    }

    public class TimelineEvent
    {
        [Required, MaxLength(50)]
        public string EventId { get; set; }
        
        [Required]
        public DateTime DateTime { get; set; }
        
        [Required, MaxLength(255)]
        public string Title { get; set; }
        
        [Required, MaxLength(1000)]
        public string Description { get; set; }
        
        [Required, MaxLength(500)]
        public string Location { get; set; }
        
        [Required, MaxLength(500)]
        public string Source { get; set; }
        
        public List<string>? Participants { get; set; }
        
        public Significance? Significance { get; set; }
    }

    public class Solution
    {
        [Required, MaxLength(50)]
        public string Culprit { get; set; }
        
        [Required, MaxLength(2000)]
        public string Motive { get; set; }
        
        [Required, MaxLength(2000)]
        public string Method { get; set; }
        
        [Required]
        public DateTime TimeOfCrime { get; set; }
        
        [Required, MinLength(3)]
        public List<string> KeyEvidence { get; set; } = new();
        
        [Required, MinLength(3)]
        public List<string> Timeline { get; set; } = new();
        
        public List<string>? AlternativeExplanations { get; set; }
    }
}
```

### Enums

```csharp
// Models/Enums.cs
namespace CaseZero.Models
{
    public enum Difficulty
    {
        Easy,
        Medium,
        Hard,
        Expert
    }

    public enum CaseStatus
    {
        Draft,
        Published,
        Archived
    }

    public enum CrimeType
    {
        Homicide,
        Assault,
        Theft,
        Fraud,
        Arson,
        Other
    }

    public enum EvidenceType
    {
        Physical,
        Biological,
        Trace,
        Documentary,
        Digital
    }

    public enum DocumentType
    {
        PoliceReport,
        WitnessStatement,
        SuspectInterview,
        ForensicReport,
        FinancialRecord,
        Letter,
        Other
    }

    public enum ForensicAnalysisType
    {
        DNA,
        Ballistics,
        Fingerprints,
        Toxicology,
        Trace,
        Handwriting
    }

    public enum SuspicionLevel
    {
        Low,
        Medium,
        High
    }

    public enum Significance
    {
        Low,
        Medium,
        High
    }

    public enum SessionStatus
    {
        Active,
        Solved,
        Failed
    }

    public enum ForensicRequestStatus
    {
        Pending,
        Completed,
        Cancelled
    }

    public enum DetectiveRank
    {
        Rookie,
        DetectiveIII,
        DetectiveII,
        DetectiveI,
        LeadDetective,
        SeniorDetective,
        ChiefDetective,
        MasterDetective
    }
}
```

### Database Entities

```csharp
// Data/Entities/CaseEntity.cs
namespace CaseZero.Data.Entities
{
    public class CaseEntity
    {
        [Key, MaxLength(50)]
        public string CaseId { get; set; }
        
        [Required, MaxLength(255)]
        public string Title { get; set; }
        
        [Required]
        public Difficulty Difficulty { get; set; }
        
        public decimal EstimatedTimeHours { get; set; }
        
        [Required]
        public CaseStatus Status { get; set; }
        
        [Required]
        [Column(TypeName = "jsonb")]
        public string CaseDataJson { get; set; } // Full case.json as JSONB
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public ICollection<CaseSessionEntity> Sessions { get; set; }
    }

    public class UserEntity
    {
        [Key]
        public Guid UserId { get; set; } = Guid.NewGuid();
        
        [Required, MaxLength(50)]
        public string Username { get; set; }
        
        [Required, MaxLength(255)]
        public string Email { get; set; }
        
        [Required, MaxLength(255)]
        public string PasswordHash { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? LastLoginAt { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        // Navigation properties
        public UserProgressEntity Progress { get; set; }
        public ICollection<CaseSessionEntity> Sessions { get; set; }
    }

    public class UserProgressEntity
    {
        [Key, ForeignKey("User")]
        public Guid UserId { get; set; }
        
        [Required]
        public DetectiveRank CurrentRank { get; set; } = DetectiveRank.Rookie;
        
        public int TotalXP { get; set; } = 0;
        
        public int CasesSolved { get; set; } = 0;
        
        public int CasesFailed { get; set; } = 0;
        
        public int FirstAttemptSuccesses { get; set; } = 0;
        
        public int TotalInvestigationMinutes { get; set; } = 0;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation property
        public UserEntity User { get; set; }
    }

    public class CaseSessionEntity
    {
        [Key]
        public Guid SessionId { get; set; } = Guid.NewGuid();
        
        [Required, ForeignKey("User")]
        public Guid UserId { get; set; }
        
        [Required, ForeignKey("Case"), MaxLength(50)]
        public string CaseId { get; set; }
        
        [Required]
        public SessionStatus Status { get; set; } = SessionStatus.Active;
        
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? CompletedAt { get; set; }
        
        public int TotalTimeMinutes { get; set; } = 0;
        
        public int SubmissionAttempts { get; set; } = 0;
        
        [Column(TypeName = "text")]
        public string? NotesText { get; set; }
        
        // Navigation properties
        public UserEntity User { get; set; }
        public CaseEntity Case { get; set; }
        public ICollection<ForensicRequestEntity> ForensicRequests { get; set; }
        public ICollection<CaseSubmissionEntity> Submissions { get; set; }
    }

    public class ForensicRequestEntity
    {
        [Key]
        public Guid RequestId { get; set; } = Guid.NewGuid();
        
        [Required, ForeignKey("Session")]
        public Guid SessionId { get; set; }
        
        [Required, MaxLength(50)]
        public string EvidenceId { get; set; }
        
        [Required]
        public ForensicAnalysisType AnalysisType { get; set; }
        
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        
        [Required]
        public DateTime CompletesAt { get; set; }
        
        public DateTime? CompletedAt { get; set; }
        
        [Required]
        public ForensicRequestStatus Status { get; set; } = ForensicRequestStatus.Pending;
        
        [MaxLength(500)]
        public string? ResultBlobPath { get; set; }
        
        // Navigation property
        public CaseSessionEntity Session { get; set; }
    }

    public class CaseSubmissionEntity
    {
        [Key]
        public Guid SubmissionId { get; set; } = Guid.NewGuid();
        
        [Required, ForeignKey("Session")]
        public Guid SessionId { get; set; }
        
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
        
        [Required, MaxLength(50)]
        public string CulpritSelected { get; set; }
        
        [Required, Column(TypeName = "text")]
        public string MotiveExplanation { get; set; }
        
        [Required, Column(TypeName = "text")]
        public string MethodExplanation { get; set; }
        
        [Required, Column(TypeName = "jsonb")]
        public string EvidenceSelectedJson { get; set; } // Array as JSON
        
        public bool IsCorrect { get; set; }
        
        public int XPAwarded { get; set; }
        
        [Column(TypeName = "jsonb")]
        public string? FeedbackJson { get; set; }
        
        // Navigation property
        public CaseSessionEntity Session { get; set; }
    }
}
```

---

## 9.6 Validation Rules

### case.json Validation

**Required Fields:**
- All marked with `required` in schema must be present
- Empty strings not allowed for required string fields
- Arrays must have minimum lengths where specified

**Data Constraints:**
- `caseId`: Must match pattern `CASE-\d{4}-\d{3}`
- `suspectId`: Must match pattern `SUSP-\d{3}`
- `evidenceId`: Must match pattern `EV-\d{3}`
- `documentId`: Must match pattern `DOC-\d{3}`
- `analysisId`: Must match pattern `FA-\d{3}`
- `eventId`: Must match pattern `TL-\d{3}`

**Referential Integrity:**
```typescript
// Validation function
function validateCase(caseData: Case): ValidationResult {
  const errors: string[] = [];
  
  // 1. Solution.culprit must reference valid Suspect
  const culpritExists = caseData.suspects.some(
    s => s.suspectId === caseData.solution.culprit
  );
  if (!culpritExists) {
    errors.push(`Solution culprit ${caseData.solution.culprit} not found in suspects`);
  }
  
  // 2. Solution.keyEvidence must reference valid Evidence/Documents
  caseData.solution.keyEvidence.forEach(id => {
    const isEvidence = caseData.evidence.some(e => e.evidenceId === id);
    const isDocument = caseData.documents.some(d => d.documentId === id);
    if (!isEvidence && !isDocument) {
      errors.push(`Solution references unknown evidence/document: ${id}`);
    }
  });
  
  // 3. ForensicAnalysis.evidenceId must reference valid Evidence
  caseData.forensicAnalyses.forEach(fa => {
    const evidenceExists = caseData.evidence.some(e => e.evidenceId === fa.evidenceId);
    if (!evidenceExists) {
      errors.push(`ForensicAnalysis ${fa.analysisId} references unknown evidence: ${fa.evidenceId}`);
    }
  });
  
  // 4. Document.relatedEvidence must reference valid Evidence
  caseData.documents.forEach(doc => {
    doc.relatedEvidence?.forEach(id => {
      const exists = caseData.evidence.some(e => e.evidenceId === id);
      if (!exists) {
        errors.push(`Document ${doc.documentId} references unknown evidence: ${id}`);
      }
    });
  });
  
  // 5. Timeline events must be chronological
  const timelineDates = caseData.timeline.map(e => new Date(e.dateTime));
  for (let i = 1; i < timelineDates.length; i++) {
    if (timelineDates[i] < timelineDates[i - 1]) {
      errors.push(`Timeline event ${caseData.timeline[i].eventId} out of order`);
    }
  }
  
  return { valid: errors.length === 0, errors };
}
```

### Cardinality Validation

```typescript
function validateCardinality(caseData: Case): ValidationResult {
  const errors: string[] = [];
  
  // Suspects: 2-8
  if (caseData.suspects.length < 2 || caseData.suspects.length > 8) {
    errors.push(`Must have 2-8 suspects, found ${caseData.suspects.length}`);
  }
  
  // Evidence: 8-25
  if (caseData.evidence.length < 8 || caseData.evidence.length > 25) {
    errors.push(`Must have 8-25 evidence items, found ${caseData.evidence.length}`);
  }
  
  // Documents: 8-25
  if (caseData.documents.length < 8 || caseData.documents.length > 25) {
    errors.push(`Must have 8-25 documents, found ${caseData.documents.length}`);
  }
  
  // Forensic Analyses: 3-8
  if (caseData.forensicAnalyses.length < 3 || caseData.forensicAnalyses.length > 8) {
    errors.push(`Must have 3-8 forensic analyses, found ${caseData.forensicAnalyses.length}`);
  }
  
  // Timeline: 10-30 events
  if (caseData.timeline.length < 10 || caseData.timeline.length > 30) {
    errors.push(`Must have 10-30 timeline events, found ${caseData.timeline.length}`);
  }
  
  // Solution.keyEvidence: 3-10
  if (caseData.solution.keyEvidence.length < 3 || caseData.solution.keyEvidence.length > 10) {
    errors.push(`Solution must cite 3-10 key evidence items, found ${caseData.solution.keyEvidence.length}`);
  }
  
  return { valid: errors.length === 0, errors };
}
```

### Backend Validation

```csharp
// Validators/CaseDataValidator.cs
public class CaseDataValidator : AbstractValidator<CaseData>
{
    public CaseDataValidator()
    {
        RuleFor(c => c.Metadata.CaseId)
            .Matches(@"^CASE-\d{4}-\d{3}$")
            .WithMessage("CaseId must match pattern CASE-YYYY-###");
        
        RuleFor(c => c.Suspects)
            .Must(s => s.Count >= 2 && s.Count <= 8)
            .WithMessage("Must have 2-8 suspects");
        
        RuleFor(c => c.Evidence)
            .Must(e => e.Count >= 8 && e.Count <= 25)
            .WithMessage("Must have 8-25 evidence items");
        
        RuleFor(c => c.Documents)
            .Must(d => d.Count >= 8 && d.Count <= 25)
            .WithMessage("Must have 8-25 documents");
        
        RuleFor(c => c.ForensicAnalyses)
            .Must(f => f.Count >= 3 && f.Count <= 8)
            .WithMessage("Must have 3-8 forensic analyses");
        
        RuleFor(c => c.Solution.Culprit)
            .Must((caseData, culprit) => caseData.Suspects.Any(s => s.SuspectId == culprit))
            .WithMessage("Solution culprit must reference valid suspect");
        
        // Additional rules...
    }
}
```

---

## 9.7 API DTOs (Data Transfer Objects)

### Request DTOs

```csharp
// DTOs/Requests/
public record RegisterRequest(
    string Username,
    string Email,
    string Password
);

public record LoginRequest(
    string Email,
    string Password
);

public record ForensicRequestDto(
    Guid SessionId,
    string EvidenceId,
    ForensicAnalysisType AnalysisType
);

public record SubmitSolutionRequest(
    Guid SessionId,
    string Culprit,
    string MotiveExplanation,
    string MethodExplanation,
    List<string> EvidenceSelected
);

public record UpdateNotesRequest(
    Guid SessionId,
    string NotesText
);
```

### Response DTOs

```csharp
// DTOs/Responses/
public record CaseListItemDto(
    string CaseId,
    string Title,
    Difficulty Difficulty,
    decimal EstimatedTimeHours,
    int SuspectCount,
    int DocumentCount,
    int EvidenceCount,
    string? UserStatus // "NotStarted" | "Active" | "Solved" | "Failed"
);

public record CaseDetailDto(
    string CaseId,
    string Title,
    Difficulty Difficulty,
    CaseData CaseData,
    CaseSessionDto? UserSession
);

public record CaseSessionDto(
    Guid SessionId,
    DateTime StartedAt,
    int TotalTimeMinutes,
    int SubmissionAttempts,
    SessionStatus Status
);

public record ForensicRequestDto(
    Guid RequestId,
    string EvidenceId,
    ForensicAnalysisType AnalysisType,
    DateTime RequestedAt,
    DateTime CompletesAt,
    DateTime? CompletedAt,
    ForensicRequestStatus Status,
    string? ReportUrl
);

public record SubmissionResultDto(
    Guid SubmissionId,
    bool IsCorrect,
    int XPAwarded,
    int? AttemptsRemaining,
    SubmissionFeedback Feedback,
    DetectiveRank? NewRank,
    bool RankUp,
    decimal? ProgressToNextRank
);

public record UserProfileDto(
    Guid UserId,
    string Username,
    DetectiveRank Rank,
    int TotalXP,
    int XPToNextRank,
    UserStatsDto Stats
);

public record UserStatsDto(
    OverallStatsDto Overall,
    Dictionary<Difficulty, DifficultyStatsDto> ByDifficulty
);

public record OverallStatsDto(
    int CasesSolved,
    int CasesFailed,
    decimal SuccessRate,
    decimal TotalInvestigationHours,
    decimal AvgTimePerCase
);

public record DifficultyStatsDto(
    int Solved,
    int Failed,
    decimal SuccessRate,
    decimal FirstAttemptSuccessRate,
    decimal AvgTimeHours
);
```

---

## 9.8 Database Indexes

### Performance Optimization

```sql
-- Users
CREATE INDEX idx_users_email ON Users(Email);
CREATE INDEX idx_users_username ON Users(Username);

-- Cases
CREATE INDEX idx_cases_difficulty ON Cases(Difficulty);
CREATE INDEX idx_cases_status ON Cases(Status);
CREATE INDEX idx_cases_updated_at ON Cases(UpdatedAt DESC);

-- CaseSessions
CREATE INDEX idx_sessions_user ON CaseSessions(UserId);
CREATE INDEX idx_sessions_case ON CaseSessions(CaseId);
CREATE INDEX idx_sessions_status ON CaseSessions(Status);
CREATE INDEX idx_sessions_user_case ON CaseSessions(UserId, CaseId); -- Composite for lookup

-- ForensicRequests
CREATE INDEX idx_forensics_session ON ForensicRequests(SessionId);
CREATE INDEX idx_forensics_status ON ForensicRequests(Status);
CREATE INDEX idx_forensics_completes_at ON ForensicRequests(CompletesAt) WHERE Status = 'Pending'; -- Partial index for timer worker

-- CaseSubmissions
CREATE INDEX idx_submissions_session ON CaseSubmissions(SessionId);
CREATE INDEX idx_submissions_submitted_at ON CaseSubmissions(SubmittedAt DESC);

-- UserProgress
-- Primary key on UserId is sufficient (one-to-one with Users)
```

---

## 9.9 JSON Schema Validation (Optional)

### JSON Schema Definition

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "CaseZero Case Schema",
  "version": "3.0",
  "type": "object",
  "required": ["schemaVersion", "metadata", "victim", "crime", "suspects", "evidence", "documents", "forensicAnalyses", "timeline", "solution"],
  "properties": {
    "schemaVersion": {
      "type": "string",
      "enum": ["3.0"]
    },
    "metadata": {
      "type": "object",
      "required": ["caseId", "title", "difficulty", "estimatedTimeHours", "createdDate", "lastModified", "status"],
      "properties": {
        "caseId": {
          "type": "string",
          "pattern": "^CASE-\\d{4}-\\d{3}$"
        },
        "title": {
          "type": "string",
          "minLength": 1,
          "maxLength": 255
        },
        "difficulty": {
          "type": "string",
          "enum": ["Easy", "Medium", "Hard", "Expert"]
        },
        "estimatedTimeHours": {
          "type": "number",
          "minimum": 1.0,
          "maximum": 12.0
        },
        "createdDate": {
          "type": "string",
          "format": "date"
        },
        "lastModified": {
          "type": "string",
          "format": "date"
        },
        "status": {
          "type": "string",
          "enum": ["Draft", "Published", "Archived"]
        }
      }
    },
    "suspects": {
      "type": "array",
      "minItems": 2,
      "maxItems": 8
    },
    "evidence": {
      "type": "array",
      "minItems": 8,
      "maxItems": 25
    },
    "documents": {
      "type": "array",
      "minItems": 8,
      "maxItems": 25
    },
    "forensicAnalyses": {
      "type": "array",
      "minItems": 3,
      "maxItems": 8
    },
    "timeline": {
      "type": "array",
      "minItems": 10,
      "maxItems": 30
    }
  }
}
```

---

## 9.10 Migration Strategy

### Schema Versioning

**Current Version:** 3.0

**Version Field:** All case.json files include `"schemaVersion": "3.0"`

**Future Migrations:**
```csharp
public interface ICaseMigrator
{
    string FromVersion { get; }
    string ToVersion { get; }
    CaseData Migrate(CaseData oldData);
}

// Example: Future 3.0 → 3.1 migration
public class Migration_3_0_to_3_1 : ICaseMigrator
{
    public string FromVersion => "3.0";
    public string ToVersion => "3.1";
    
    public CaseData Migrate(CaseData oldData)
    {
        // Add new optional fields
        // Transform existing data if needed
        oldData.SchemaVersion = "3.1";
        return oldData;
    }
}
```

### Database Migrations (Entity Framework)

```bash
# Create new migration
dotnet ef migrations add AddForensicRequests

# Update database
dotnet ef database update

# Rollback to previous migration
dotnet ef database update PreviousMigrationName
```

---

## 9.11 Data Serialization

### Frontend (TypeScript)

```typescript
// Parse case.json
import caseJson from './assets/CASE-2024-001/case.json';
const caseData: Case = caseJson as Case;

// Serialize for API
const submission: SubmitSolutionRequest = {
  sessionId: session.sessionId,
  culprit: 'SUSP-001',
  motiveExplanation: motiveText,
  methodExplanation: methodText,
  evidenceSelected: selectedEvidence
};

const response = await axios.post('/api/cases/submit', submission);
```

### Backend (C#)

```csharp
// Deserialize case.json from database
var caseEntity = await _db.Cases.FindAsync(caseId);
var caseData = JsonSerializer.Deserialize<CaseData>(
    caseEntity.CaseDataJson,
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
);

// Serialize for storage
var caseJson = JsonSerializer.Serialize(caseData, new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
});

caseEntity.CaseDataJson = caseJson;
await _db.SaveChangesAsync();
```

---

## 9.12 Example Complete case.json

See **Appendix A** in [04-CASE-STRUCTURE.md](04-CASE-STRUCTURE.md) for full CASE-2024-001 example.

**Summary:**
- 1 victim, 1 crime location
- 4 suspects (SUSP-001 through SUSP-004)
- 12 evidence items (EV-001 through EV-012)
- 15 documents (DOC-001 through DOC-015)
- 5 forensic analyses (FA-001 through FA-005)
- 18 timeline events (TL-001 through TL-018)
- 1 solution (culprit SUSP-001, 7 key evidence items)

---

## 9.13 Summary

**Data Architecture:**
- **case.json** = Single source of truth for static case content
- **TypeScript interfaces** = Type-safe frontend models
- **C# models** = Backend domain models with validation
- **Database entities** = Runtime session/progression data

**Key Structures:**
- Case (metadata, victim, crime, suspects, evidence, documents, forensics, timeline, solution)
- Session (user progress per case, notes, forensic requests)
- Submission (solution attempts, feedback, XP awards)
- User Progress (rank, XP, statistics)

**Validation Layers:**
- JSON Schema (optional, design-time)
- TypeScript type checking (compile-time)
- C# Data Annotations (runtime, API layer)
- FluentValidation (runtime, complex rules)
- Database constraints (runtime, data integrity)

**Referential Integrity:**
- Solution.culprit → Suspects
- Solution.keyEvidence → Evidence/Documents
- ForensicAnalysis.evidenceId → Evidence
- Document.relatedEvidence → Evidence

**Cardinality Rules:**
- Suspects: 2-8
- Evidence: 8-25
- Documents: 8-25
- Forensic Analyses: 3-8
- Timeline Events: 10-30

**Database Design:**
- PostgreSQL with JSONB for case.json storage
- Relational tables for sessions, requests, submissions
- Indexes for performance optimization
- Entity Framework Core migrations

---

**Next Chapter:** [10-CONTENT-PIPELINE.md](10-CONTENT-PIPELINE.md) - Case creation workflow and content management

**Related Documents:**
- [04-CASE-STRUCTURE.md](04-CASE-STRUCTURE.md) - Detailed case.json structure
- [08-TECHNICAL.md](08-TECHNICAL.md) - System architecture
- [11-TESTING.md](11-TESTING.md) - Validation testing

---

**Revision History:**

| Date | Version | Changes | Author |
|------|---------|---------|--------|
| 2025-11-14 | 1.0 | Initial complete draft | AI Assistant |
