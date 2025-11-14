# Capítulo 09 - Esquema de Dados & Modelos

**Documento de Design de Jogo - CaseZero v3.0**  
**Última atualização:** 14 de novembro de 2025  
**Status:** ✅ Completo

---

## 9.1 Visão Geral

Este capítulo define o **esquema completo de dados, os modelos e as regras de validação** para CaseZero v3.0. Abrange a estrutura do arquivo case.json, os modelos C# do backend, as interfaces TypeScript do frontend e as tabelas de banco de dados.

**Conceitos-chave:**

- case.json como fonte única da verdade
- Tipagem forte entre frontend e backend
- Validação em múltiplas camadas
- Extensibilidade para recursos futuros

---

## 9.2 Filosofia do Esquema

### Princípios de Design

#### 1. Fonte Única da Verdade

- case.json contém todos os dados estáticos do caso
- O banco de dados armazena dados de runtime/sessão
- Sem duplicação entre fontes

#### 2. Segurança de Tipos

- Interfaces TypeScript para o frontend
- Modelos C# para o backend
- JSON Schema para validação

#### 3. Compatibilidade Futura

- Campos opcionais para recursos futuros
- Campo de versão para migrações
- Alterações sempre retrocompatíveis

#### 4. Legibilidade Humana

- Nomes de propriedades claros
- Datas em ISO-8601
- Mínimo de aninhamento

---

## 9.3 Estrutura Completa do case.json

### Objeto Raiz

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

**Descrição dos campos:**

- `caseId` (string, obrigatório): Identificador único, formato `CASE-YYYY-###`
- `title` (string, obrigatório): Nome exibido do caso
- `difficulty` (enum, obrigatório): `Easy` | `Medium` | `Hard` | `Expert`
- `estimatedTimeHours` (number, obrigatório): Tempo estimado de resolução (1,0-12,0 horas)
- `tags` (string[], opcional): Tags para busca/filtragem
- `createdDate` (string, obrigatório): Data ISO-8601 `YYYY-MM-DD`
- `lastModified` (string, obrigatório): Data ISO-8601 `YYYY-MM-DD`
- `author` (string, opcional): Atribuição ao criador de conteúdo
- `status` (enum, obrigatório): `Draft` | `Published` | `Archived`

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

**Descrição dos campos:**

- `name` (string, obrigatório): Nome completo legal
- `age` (number, obrigatório): Idade na data do crime
- `occupation` (string, obrigatório): Profissão ou cargo
- `background` (string, obrigatório): 2-3 frases de contexto pessoal
- `photoUrl` (string, opcional): Caminho relativo a partir da raiz do caso
- `lastSeenDateTime` (string, opcional): Data/hora ISO-8601 com fuso horário
- `lastSeenLocation` (string, opcional): Última localização conhecida

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

**Descrição dos campos:**

- `type` (enum, obrigatório): `Homicide` | `Assault` | `Theft` | `Fraud` | `Arson` | `Other`
- `subtype` (string, opcional): Método específico (ex.: "Firearm", "Blunt Force", "Poison")
- `location.address` (string, obrigatório): Endereço completo
- `location.city` (string, obrigatório): Nome da cidade
- `location.state` (string, obrigatório): Estado/província
- `location.zipCode` (string, opcional): Código postal
- `location.description` (string, obrigatório): Detalhes contextuais
- `dateTime` (string, obrigatório): Data/hora ISO-8601 estimada do crime
- `discoveryDateTime` (string, obrigatório): Data/hora ISO-8601 da descoberta
- `description` (string, obrigatório): 3-5 frases com visão inicial da cena
- `sceneDiagram` (string, opcional): Caminho do mapa/planta baixa

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

**Descrição dos campos:**

- `suspectId` (string, obrigatório): ID único, formato `SUSP-###`
- `name` (string, obrigatório): Nome completo
- `age` (number, obrigatório): Idade atual
- `occupation` (string, obrigatório): Posição ou função
- `relationship` (string, obrigatório): Conexão com a vítima
- `photoUrl` (string, opcional): Caminho da foto de perfil
- `background` (string, obrigatório): 2-3 frases de contexto
- `alibi` (string, obrigatório): Local onde afirma estar durante o crime
- `motive` (string, obrigatório): Motivo potencial
- `suspicionLevel` (enum, opcional): `Low` | `Medium` | `High` (uso interno)

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

**Descrição dos campos:**

- `evidenceId` (string, obrigatório): ID único, formato `EV-###`
- `name` (string, obrigatório): Nome descritivo curto
- `type` (enum, obrigatório): `Physical` | `Biological` | `Trace` | `Documentary` | `Digital`
- `subtype` (string, opcional): Categoria específica (ex.: "Weapon", "Clothing", "Blood")
- `description` (string, obrigatório): Descrição detalhada em 2-4 frases
- `collectionLocation` (string, obrigatório): Local onde a evidência foi encontrada
- `collectionDateTime` (string, obrigatório): Data/hora ISO-8601 da coleta
- `photos` (string[], opcional): Lista de caminhos para fotos (vários ângulos)
- `forensicAnalysisAvailable` (string[], obrigatório): Tipos de perícia aplicáveis

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

**Descrição dos campos:**

- `documentId` (string, obrigatório): ID único, formato `DOC-###`
- `type` (enum, obrigatório): `PoliceReport` | `WitnessStatement` | `SuspectInterview` | `ForensicReport` | `FinancialRecord` | `Letter` | `Other`
- `title` (string, obrigatório): Nome exibido do documento
- `description` (string, obrigatório): Resumo de 1-2 frases
- `author` (string, obrigatório): Responsável pela criação do documento
- `dateCreated` (string, obrigatório): Data ISO-8601 `YYYY-MM-DD`
- `pageCount` (number, obrigatório): Quantidade de páginas (1-10 típico)
- `filePath` (string, obrigatório): Caminho para o arquivo PDF
- `relatedEvidence` (string[], opcional): IDs de evidências mencionadas no documento
- `keyClues` (string[], opcional): Fatos importantes para a solução (uso interno)

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

**Descrição dos campos:**

- `analysisId` (string, obrigatório): ID único, formato `FA-###`
- `type` (enum, obrigatório): `DNA` | `Ballistics` | `Fingerprints` | `Toxicology` | `Trace` | `Handwriting`
- `evidenceId` (string, obrigatório): ID da evidência analisada
- `durationHours` (number, obrigatório): Horas em tempo real para concluir (8-36)
- `description` (string, obrigatório): O que a perícia examina
- `resultDocument.documentId` (string, obrigatório): ID do documento do laudo
- `resultDocument.title` (string, obrigatório): Título do laudo
- `resultDocument.filePath` (string, obrigatório): Caminho do PDF do laudo
- `keyFindings` (string[], obrigatório): 2-5 fatos críticos da análise

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

**Descrição dos campos:**

- `eventId` (string, obrigatório): ID único, formato `TL-###`
- `dateTime` (string, obrigatório): Data/hora ISO-8601 com fuso
- `title` (string, obrigatório): Nome curto do evento
- `description` (string, obrigatório): 1-2 frases explicando o ocorrido
- `location` (string, obrigatório): Local do evento
- `source` (string, obrigatório): Fonte da informação (documento, testemunha, perícia)
- `participants` (string[], opcional): Pessoas envolvidas
- `significance` (enum, opcional): `Low` | `Medium` | `High` (uso interno)

### Solution

```json
{
  "culprit": "SUSP-001",
  "motive": "David Reynolds embezzled $500,000 from company. Marcus discovered discrepancy during audit review. Reynolds feared exposure would lead to prison.",
  "method": "Reynolds used stolen building keycard to enter office after hours. Shot victim once in chest with 9mm Glock. Ransacked office to stage robbery.",
  "timeOfCrime": "2024-10-16T01:15:00Z",
  "keyEvidence": [
    "EV-001",
    "EV-004",
    "EV-007",
    "DOC-009",
    "FA-002"
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

**Descrição dos campos:**

- `culprit` (string, obrigatório): ID do suspeito culpado
- `motive` (string, obrigatório): 3-5 frases explicando o motivo
- `method` (string, obrigatório): 3-5 frases descrevendo o método
- `timeOfCrime` (string, obrigatório): Data/hora ISO-8601 precisa, se conhecida
- `keyEvidence` (string[], obrigatório): IDs de evidências/documentos que comprovam a culpa (5-10 itens)
- `timeline` (string[], obrigatório): Sequência cronológica das ações do culpado
- `alternativeExplanations` (string[], opcional): Por que outros suspeitos são inocentes

---

## 9.4 Interfaces TypeScript (Frontend)

### Tipos centrais

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

### Estrutura do caso

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

---

## 9.5 Modelos C# (Backend)

### Estrutura do caso (C#)

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

### Enums (C#)

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

### Entidades de banco de dados

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
    public string CaseDataJson { get; set; }
        
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
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
    public string EvidenceSelectedJson { get; set; }
        
    public bool IsCorrect { get; set; }
        
    public int XPAwarded { get; set; }
        
    [Column(TypeName = "jsonb")]
    public string? FeedbackJson { get; set; }
        
    public CaseSessionEntity Session { get; set; }
  }
}
  ```

### Dados de sessão e runtime

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

### Usuários & progressão

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

## 9.6 Regras de validação

### Validação do case.json

**Campos obrigatórios:**

- Todos os marcados como `required` no schema devem existir
- Strings obrigatórias não podem ser vazias
- Arrays precisam respeitar o tamanho mínimo definido

**Restrições de dados:**

- `caseId`: Deve seguir o padrão `CASE-\d{4}-\d{3}`
- `suspectId`: Deve seguir o padrão `SUSP-\d{3}`
- `evidenceId`: Deve seguir o padrão `EV-\d{3}`
- `documentId`: Deve seguir o padrão `DOC-\d{3}`
- `analysisId`: Deve seguir o padrão `FA-\d{3}`
- `eventId`: Deve seguir o padrão `TL-\d{3}`

**Integridade referencial:**

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

### Validação de cardinalidade

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

### Validação no backend

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

## 9.7 DTOs da API (Objetos de Transferência de Dados)

### DTOs de requisição

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

### DTOs de resposta

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

## 9.8 Índices de banco de dados

### Otimização de performance

```sql
-- Usuários
CREATE INDEX idx_users_email ON Users(Email);
CREATE INDEX idx_users_username ON Users(Username);

-- Casos
CREATE INDEX idx_cases_difficulty ON Cases(Difficulty);
CREATE INDEX idx_cases_status ON Cases(Status);
CREATE INDEX idx_cases_updated_at ON Cases(UpdatedAt DESC);

-- Sessões de caso
CREATE INDEX idx_sessions_user ON CaseSessions(UserId);
CREATE INDEX idx_sessions_case ON CaseSessions(CaseId);
CREATE INDEX idx_sessions_status ON CaseSessions(Status);
CREATE INDEX idx_sessions_user_case ON CaseSessions(UserId, CaseId); -- Índice composto para busca

-- Solicitações forenses
CREATE INDEX idx_forensics_session ON ForensicRequests(SessionId);
CREATE INDEX idx_forensics_status ON ForensicRequests(Status);
CREATE INDEX idx_forensics_completes_at ON ForensicRequests(CompletesAt) WHERE Status = 'Pending'; -- Índice parcial para worker de timers

-- Submissões de caso
CREATE INDEX idx_submissions_session ON CaseSubmissions(SessionId);
CREATE INDEX idx_submissions_submitted_at ON CaseSubmissions(SubmittedAt DESC);

-- Progresso do usuário
-- Chave primária em UserId já garante unicidade (relação 1-1)
```

---

## 9.9 Validação com JSON Schema (opcional)

### Definição do JSON Schema

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

## 9.10 Estratégia de migração

### Versionamento do schema

**Versão atual:** 3.0

**Campo de versão:** todos os arquivos case.json incluem `"schemaVersion": "3.0"`

**Migrações futuras:**

```csharp
public interface ICaseMigrator
{
  string FromVersion { get; }
  string ToVersion { get; }
  CaseData Migrate(CaseData oldData);
}

// Exemplo: migração futura 3.0 → 3.1
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

### Migrações de banco (Entity Framework)

```bash
# Criar nova migration
dotnet ef migrations add AddForensicRequests

# Atualizar banco
dotnet ef database update

# Reverter para migration anterior
dotnet ef database update PreviousMigrationName
```

---

## 9.11 Serialização de dados

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

## 9.12 Exemplo completo de case.json

Veja o **Apêndice A** em [04-ESTRUTURA-DE-CASO.md](04-ESTRUTURA-DE-CASO.md) para o exemplo completo do CASE-2024-001.

**Resumo:**

- 1 vítima, 1 local do crime
- 4 suspeitos (SUSP-001 até SUSP-004)
- 12 evidências (EV-001 até EV-012)
- 15 documentos (DOC-001 até DOC-015)
- 5 análises forenses (FA-001 até FA-005)
- 18 eventos na linha do tempo (TL-001 até TL-018)
- 1 solução (culpado SUSP-001, 7 evidências principais)

---

## 9.13 Resumo

**Arquitetura de dados:**

- **case.json** = fonte única de verdade para conteúdo estático
- **Interfaces TypeScript** = modelos tipados para o frontend
- **Modelos C#** = modelos de domínio no backend com validação
- **Entidades de banco** = dados de sessão e progressão em runtime

**Estruturas principais:**

- Caso (metadata, vítima, crime, suspeitos, evidências, documentos, perícia, linha do tempo, solução)
- Sessão (progresso do usuário por caso, anotações, solicitações forenses)
- Submissão (tentativas de solução, feedback, XP concedido)
- Progresso do usuário (patente, XP, estatísticas)

**Camadas de validação:**

- JSON Schema (opcional, em tempo de design)
- TypeScript (checagens de tipo em tempo de compilação)
- Data Annotations em C# (runtime, camada de API)
- FluentValidation (runtime, regras complexas)
- Restrições de banco de dados (runtime, integridade)

**Integridade referencial:**

- Solution.culprit → Suspects
- Solution.keyEvidence → Evidence/Documents
- ForensicAnalysis.evidenceId → Evidence
- Document.relatedEvidence → Evidence

**Regras de cardinalidade:**

- Suspeitos: 2-8
- Evidências: 8-25
- Documentos: 8-25
- Análises forenses: 3-8
- Eventos da linha do tempo: 10-30

**Design do banco:**

- PostgreSQL com JSONB para armazenar o case.json
- Tabelas relacionais para sessões, solicitações e submissões
- Índices para otimização de performance
- Migrações via Entity Framework Core

---

**Próximo capítulo:** [10-CONTENT-PIPELINE.md](10-CONTENT-PIPELINE.md) – Workflow de criação de casos e gestão de conteúdo

**Documentos relacionados:**

- [04-ESTRUTURA-DE-CASO.md](04-ESTRUTURA-DE-CASO.md) – Estrutura detalhada do case.json
- [08-TECNICO.md](08-TECNICO.md) – Arquitetura do sistema
- [11-TESTES.md](11-TESTES.md) – Testes e validação

---

**Histórico de revisões:**

| Data | Versão | Mudanças | Autor |
|------|--------|----------|-------|
| 14/11/2025 | 1.0 | Tradução completa para PT-BR | Assistente de IA |

