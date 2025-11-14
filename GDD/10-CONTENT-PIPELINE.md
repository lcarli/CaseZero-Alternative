# Chapter 10 - Content Pipeline & Production

**Game Design Document - CaseZero v3.0**  
**Last Updated:** November 14, 2025  
**Status:** ✅ Complete

---

## 10.1 Overview

This chapter defines the **case creation workflow, content management systems, and production processes** for CaseZero v3.0. It covers the end-to-end pipeline from concept to published case.

**Key Concepts:**
- Structured case creation workflow
- Quality assurance checkpoints
- Asset management pipeline
- Content versioning and approval
- Localization strategy

---

## 10.2 Content Team Roles

### Role Definitions

**Case Writer**
- **Responsibilities:**
  - Develop case concepts
  - Write case narratives
  - Create documents (police reports, statements, interviews)
  - Draft solution and key evidence list
- **Skills Required:**
  - Creative writing
  - Mystery/crime genre knowledge
  - Attention to detail
  - Research capabilities

**Evidence Designer**
- **Responsibilities:**
  - Design physical evidence items
  - Create evidence descriptions
  - Coordinate with forensic writer
  - Specify evidence photography requirements
- **Skills Required:**
  - Forensic knowledge (basic)
  - Research skills
  - Logical thinking
  - Detail orientation

**Forensic Writer**
- **Responsibilities:**
  - Write forensic analysis reports
  - Ensure scientific accuracy
  - Define forensic analysis types per evidence
  - Review forensic content consistency
- **Skills Required:**
  - Scientific writing
  - Forensic science knowledge
  - Research capabilities
  - Technical accuracy

**Document Designer**
- **Responsibilities:**
  - Format documents as PDFs
  - Create letterheads, forms, templates
  - Ensure visual consistency
  - Apply branding guidelines
- **Skills Required:**
  - Graphic design
  - Adobe InDesign/Illustrator
  - Typography knowledge
  - Template creation

**QA Tester**
- **Responsibilities:**
  - Solve cases blind
  - Verify clues are present
  - Check for contradictions
  - Test difficulty appropriateness
  - Report issues
- **Skills Required:**
  - Analytical thinking
  - Attention to detail
  - Critical reading
  - Documentation

**Content Manager**
- **Responsibilities:**
  - Assign cases to writers
  - Track production status
  - Review submissions
  - Approve final cases
  - Coordinate team
- **Skills Required:**
  - Project management
  - Editorial judgment
  - Communication
  - Organization

---

## 10.3 Case Creation Workflow

### Phase 1: Concept Development (2-3 days)

**Step 1.1: Initial Concept**
- Writer proposes case idea (1 paragraph pitch)
- Define crime type, setting, basic premise
- Identify unique hook or interesting element
- **Deliverable:** Concept document (200-300 words)

**Step 1.2: Concept Review**
- Content Manager reviews for originality
- Check against existing cases (avoid repetition)
- Verify difficulty tier feasibility
- **Decision:** Approve, Revise, or Reject

**Step 1.3: Case Outline**
- Writer expands to full outline
- Define victim, 4-6 suspects, solution
- Identify 10-15 key evidence items
- Sketch timeline of events
- **Deliverable:** Case outline (1000-1500 words)

**Step 1.4: Outline Approval**
- Content Manager + Lead Writer review
- Check for logical consistency
- Verify solvability
- Ensure no plot holes
- **Decision:** Approve to proceed or request revisions

---

### Phase 2: Research & Planning (1-2 days)

**Step 2.1: Research**
- Writer researches relevant topics:
  - Crime scene procedures
  - Forensic capabilities
  - Legal processes
  - Setting details (if specific location)
  - Historical accuracy (if period piece)
- **Deliverable:** Research notes document

**Step 2.2: Evidence Planning**
- Evidence Designer collaborates with Writer
- Finalize evidence list (8-25 items)
- Determine forensic analyses available (3-8)
- Map evidence to clues
- **Deliverable:** Evidence manifest spreadsheet

**Step 2.3: Document Planning**
- List all required documents (8-25)
- Assign document types:
  - Police Reports (2-4)
  - Witness Statements (3-6)
  - Suspect Interviews (2-4)
  - Forensic Reports (3-8)
  - Financial Records (0-3)
  - Letters/Misc (0-3)
- **Deliverable:** Document list with types and authors

---

### Phase 3: Writing Phase (5-7 days)

**Step 3.1: Character Development**
- Write detailed suspect profiles
- Define victim background
- Create character motivations
- Draft dialogue styles for each character
- **Deliverable:** Character dossiers

**Step 3.2: Timeline Construction**
- Build detailed timeline (10-30 events)
- Place crime at central point
- Add suspect alibis
- Include witness observations
- Ensure chronological consistency
- **Deliverable:** Master timeline spreadsheet

**Step 3.3: Document Writing - Round 1**
- Write police reports (initial scene, follow-up)
- Draft witness statements (3-6 witnesses)
- Create suspect interview transcripts (2-4)
- **Deliverable:** First batch of documents (8-12)

**Step 3.4: Evidence Descriptions**
- Write detailed descriptions for all evidence
- Include collection details (location, time, officer)
- Note visible characteristics
- **Deliverable:** Evidence descriptions document

**Step 3.5: Document Writing - Round 2**
- Write forensic reports (collaborate with Forensic Writer)
- Create financial records if applicable
- Draft letters, emails, other documents
- **Deliverable:** Second batch of documents (5-10)

**Step 3.6: Clue Placement Review**
- Map all clues to solution
- Verify critical clues present (3-5)
- Ensure important clues clear (5-8)
- Place supporting clues (8-12)
- Add red herrings (5-10)
- **Deliverable:** Clue matrix spreadsheet

---

### Phase 4: Asset Production (3-4 days)

**Step 4.1: Document Design**
- Document Designer formats all documents
- Apply templates (police reports, statements, etc.)
- Add headers, logos, signatures
- Create realistic artifacts (coffee stains, handwritten notes optional)
- **Deliverable:** PDF files for all documents (8-25)

**Step 4.2: Evidence Photography**
- Identify evidence requiring photos (10-40)
- Coordinate photoshoots or source stock images
- Capture multiple angles (2-4 per item)
- Ensure high resolution (1920×1080 minimum)
- **Deliverable:** Evidence photo library

**Step 4.3: Photo Editing**
- Edit photos for consistency
- Add evidence tags/labels
- Adjust lighting, contrast
- Export in optimized formats (JPEG/PNG)
- **Deliverable:** Edited evidence photos

**Step 4.4: Forensic Report Design**
- Format forensic reports as PDFs
- Use lab report templates
- Include evidence photos in reports
- Add scientific diagrams if applicable
- **Deliverable:** Forensic report PDFs (3-8)

**Step 4.5: Supplementary Assets**
- Create scene diagrams (floor plans, maps)
- Design suspect profile photos (if original characters)
- Generate victim photo
- **Deliverable:** Supplementary visual assets

---

### Phase 5: case.json Assembly (1 day)

**Step 5.1: Data Entry**
- Compile all text content into case.json structure
- Enter metadata (ID, title, difficulty, etc.)
- Input victim, crime, suspects data
- Add evidence, documents, forensics arrays
- Build timeline array
- Define solution object
- **Deliverable:** Draft case.json file

**Step 5.2: Validation**
- Run JSON schema validator
- Check referential integrity
- Verify cardinality (2-8 suspects, 8-25 evidence, etc.)
- Test file paths to assets
- **Deliverable:** Validated case.json

**Step 5.3: Asset Organization**
```
cases/CASE-YYYY-###/
├── case.json
├── documents/
│   ├── police-report-001.pdf
│   ├── witness-statement-001.pdf
│   ├── suspect-interview-001.pdf
│   └── ... (8-25 total)
├── evidence/
│   ├── ev001-1.jpg
│   ├── ev001-2.jpg
│   ├── ev002-1.jpg
│   └── ... (10-40 total)
├── forensics/
│   ├── dna-ev004.pdf
│   ├── ballistics-ev001.pdf
│   └── ... (3-8 total)
├── suspects/
│   ├── susp001.jpg
│   ├── susp002.jpg
│   └── ... (2-8 total)
└── scene-diagram.jpg (optional)
```

---

### Phase 6: Quality Assurance (2-3 days)

**Step 6.1: Writer Self-Test**
- Writer solves own case "blind"
- Verify all clues discoverable
- Check for unintended solutions
- Test forensic timing (if applicable)
- **Deliverable:** Writer QA report

**Step 6.2: Peer Review**
- Another writer reviews case
- Read all documents
- Check consistency
- Verify writing quality
- Test solvability
- **Deliverable:** Peer review notes

**Step 6.3: QA Tester Play-Through**
- QA Tester attempts case completely blind
- Document solving process
- Note time taken
- Identify confusing elements
- Check for contradictions
- **Deliverable:** QA test report with issues

**Step 6.4: Revisions**
- Writer addresses QA issues
- Fix contradictions
- Clarify ambiguous clues
- Adjust difficulty if needed
- Update assets if required
- **Deliverable:** Revised case files

**Step 6.5: Final Validation**
- Re-run JSON validator
- Verify all revisions applied
- Check file integrity
- Test asset loading
- **Deliverable:** Final validation report

---

### Phase 7: Approval & Publishing (1 day)

**Step 7.1: Content Manager Review**
- Review full case package
- Check production quality
- Verify difficulty rating
- Ensure brand consistency
- **Decision:** Approve, Request Minor Revisions, or Reject

**Step 7.2: Final Approval**
- Lead Designer final sign-off
- Mark case as "Published" in system
- Assign official case number
- Update case library

**Step 7.3: Deployment**
- Upload assets to Azure Blob Storage
- Insert case.json into database
- Generate CDN URLs
- Update frontend case list
- **Status:** Case live in production

---

## 10.4 Production Timeline Summary

| Phase | Duration | Key Deliverables |
|-------|----------|------------------|
| 1. Concept Development | 2-3 days | Concept, Outline |
| 2. Research & Planning | 1-2 days | Research Notes, Evidence Manifest, Document List |
| 3. Writing Phase | 5-7 days | Character Dossiers, Timeline, Documents, Evidence Descriptions |
| 4. Asset Production | 3-4 days | PDFs, Photos, Forensic Reports |
| 5. case.json Assembly | 1 day | case.json, Asset Organization |
| 6. Quality Assurance | 2-3 days | QA Reports, Revisions |
| 7. Approval & Publishing | 1 day | Final Approval, Deployment |
| **TOTAL** | **15-21 days** | Complete Published Case |

**Velocity:** 1-2 cases per month per writer

---

## 10.5 Content Management System

### Case Status Workflow

```
Draft → In Review → Approved → In Production → QA Testing → Final Review → Published → Archived
```

**Status Definitions:**
- **Draft:** Writer actively working
- **In Review:** Submitted for content review
- **Approved:** Concept/outline approved, proceed to production
- **In Production:** Assets being created
- **QA Testing:** QA tester solving case
- **Final Review:** Content Manager final approval
- **Published:** Live in production
- **Archived:** Removed from active library (but preserved)

### Case Tracking Database

**Fields:**
- Case ID
- Title
- Status
- Difficulty
- Assigned Writer
- Start Date
- Target Completion Date
- Current Phase
- Blockers (if any)
- QA Status
- Approval Date

### Version Control

**Git Repository Structure:**
```
casezero-content/
├── cases/
│   ├── CASE-2024-001/
│   ├── CASE-2024-002/
│   └── ...
├── templates/
│   ├── police-report-template.indd
│   ├── witness-statement-template.indd
│   └── ...
├── guidelines/
│   ├── writing-guidelines.md
│   ├── forensic-accuracy.md
│   └── ...
└── tools/
    ├── case-validator.js
    ├── clue-checker.py
    └── ...
```

**Git Workflow:**
- Each case in feature branch: `case/CASE-2024-XXX`
- Pull request for QA review
- Merge to `main` after approval
- Tag releases: `v3.0-CASE-2024-001`

---

## 10.6 Quality Assurance Checklist

### Content QA

**✅ Story & Logic**
- [ ] Crime is solvable with available evidence
- [ ] Solution is logical and provable
- [ ] No contradictions in timeline
- [ ] Suspect alibis are consistent
- [ ] Red herrings are plausible but disprovable

**✅ Clues & Evidence**
- [ ] 3-5 critical clues present
- [ ] Critical clues are discoverable
- [ ] Evidence chain is complete
- [ ] Forensic results make sense
- [ ] No accidental red herrings (unintended misleads)

**✅ Writing Quality**
- [ ] Documents are realistic
- [ ] Tone is consistent per document type
- [ ] No anachronisms (if period piece)
- [ ] Grammar and spelling correct
- [ ] Character voices are distinct

**✅ Difficulty Rating**
- [ ] Matches intended tier (Easy/Medium/Hard/Expert)
- [ ] Clues are appropriately hidden/obvious
- [ ] Requires correct level of inference
- [ ] Estimated time is accurate

### Technical QA

**✅ case.json Validation**
- [ ] JSON syntax valid
- [ ] Schema version correct
- [ ] All required fields present
- [ ] IDs are unique and follow pattern
- [ ] References are valid (culprit → suspect, etc.)
- [ ] Cardinality rules met (2-8 suspects, 8-25 evidence, etc.)

**✅ Asset QA**
- [ ] All file paths in case.json are valid
- [ ] PDFs open correctly (not corrupted)
- [ ] Images load and display properly
- [ ] Image resolution meets minimum (1920×1080)
- [ ] File sizes are reasonable (<5MB per PDF, <2MB per image)
- [ ] File naming convention followed

**✅ Metadata**
- [ ] Case ID follows format CASE-YYYY-###
- [ ] Title is unique and descriptive
- [ ] Difficulty matches score calculation
- [ ] Estimated time is reasonable (1-12 hours)
- [ ] Tags are relevant

---

## 10.7 Templates & Tools

### Document Templates

**Police Report Template**
- Letterhead with department logo
- Case number field
- Date/time fields
- Officer name/badge number
- Structured sections: Summary, Details, Evidence, Signature
- Font: Arial 11pt, 1" margins

**Witness Statement Template**
- Header with "Witness Statement" title
- Witness information section
- Narrative statement area
- Signature line with date
- Font: Times New Roman 12pt

**Interview Transcript Template**
- "Interview Transcript" header
- Subject information
- Date/time/location
- Q&A format with speaker labels
- Footer with page numbers
- Font: Courier New 11pt (typewriter style)

**Forensic Report Template**
- Lab letterhead
- Report ID and evidence ID
- Analysis type prominently displayed
- Sections: Methodology, Findings, Conclusions
- Analyst signature
- Font: Arial 10pt, technical style

### Software Tools

**Content Creation:**
- **Adobe InDesign:** Document layout
- **Adobe Photoshop:** Photo editing
- **Adobe Illustrator:** Diagrams, icons
- **Microsoft Word:** Text drafting
- **Google Sheets:** Timeline/evidence tracking

**Validation:**
- **JSON Validator:** Online/CLI tool for syntax
- **Custom Case Validator:** checks referential integrity
- **Clue Checker Script:** verifies solution provable

**Asset Management:**
- **Git:** Version control
- **Azure Blob Storage:** Production storage
- **CDN:** Asset delivery

---

## 10.8 Localization Strategy

### Phase 1: English Primary (Launch)

**Launch Content:**
- All cases in English
- UI in English
- No localization infrastructure yet

### Phase 2: Localization Infrastructure (Post-Launch +6 months)

**Technical Setup:**
- Implement i18n framework (react-i18next)
- Extract UI strings to JSON files
- Create translation pipeline

**case.json Localization Strategy:**
```json
{
  "schemaVersion": "3.0",
  "locale": "en-US",
  "metadata": { ... },
  "translationKey": "CASE-2024-001-en"
}
```

**Separate Localized Files:**
```
cases/CASE-2024-001/
├── case.en.json (English)
├── case.fr.json (French)
├── case.es.json (Spanish)
├── case.pt.json (Portuguese)
├── documents/
│   ├── en/
│   │   ├── police-report-001.pdf
│   │   └── ...
│   ├── fr/
│   │   ├── police-report-001.pdf
│   │   └── ...
│   └── ...
```

### Phase 3: First Translations (Post-Launch +12 months)

**Target Languages (Priority Order):**
1. **French (fr-FR):** European market
2. **Spanish (es-ES):** Spain + Latin America
3. **Portuguese (pt-BR):** Brazil market
4. **German (de-DE):** European market

**Translation Process:**
1. Export case.json text content
2. Professional translation service
3. Cultural adaptation (names, locations if needed)
4. Recreate documents in target language
5. QA testing by native speaker
6. Publish localized version

**Translation Costs:**
- ~$0.10-0.15 per word
- Average case: 10,000-15,000 words
- Cost per case per language: $1,000-2,250

---

## 10.9 Content Library Management

### Case Categorization

**By Difficulty:**
- Easy (0-45 points)
- Medium (46-90 points)
- Hard (91-135 points)
- Expert (136-180 points)

**By Crime Type:**
- Homicide
- Assault
- Theft
- Fraud
- Arson
- Other

**By Tags:**
- Financial (embezzlement, fraud)
- Workplace (office crime)
- Domestic (family/relationships)
- Organized Crime (gang, mafia)
- Historical (period cases)
- Serial (multiple victims)

### Case Retirement Policy

**When to Archive:**
- Case has been available for 3+ years
- Player completion rate is very high (>90%)
- Case is replaced by improved version
- Content no longer meets quality standards

**Archive Process:**
- Change status to "Archived"
- Remove from active case list
- Preserve files in archive folder
- Users who started can still complete
- No new players can start

---

## 10.10 Content Production Tools

### Case Validator Tool

```javascript
// case-validator.js
const Ajv = require('ajv');
const fs = require('fs');

function validateCase(caseJsonPath) {
  const caseData = JSON.parse(fs.readFileSync(caseJsonPath));
  const schema = JSON.parse(fs.readFileSync('./case-schema.json'));
  
  const ajv = new Ajv();
  const validate = ajv.compile(schema);
  const valid = validate(caseData);
  
  if (!valid) {
    console.error('Schema validation failed:');
    console.error(validate.errors);
    return false;
  }
  
  // Referential integrity checks
  const errors = [];
  
  // Check solution culprit exists
  const culpritExists = caseData.suspects.some(s => s.suspectId === caseData.solution.culprit);
  if (!culpritExists) {
    errors.push(`Solution culprit ${caseData.solution.culprit} not found in suspects`);
  }
  
  // Check solution evidence references
  caseData.solution.keyEvidence.forEach(id => {
    const isEvidence = caseData.evidence.some(e => e.evidenceId === id);
    const isDocument = caseData.documents.some(d => d.documentId === id);
    if (!isEvidence && !isDocument) {
      errors.push(`Solution references unknown item: ${id}`);
    }
  });
  
  // Check forensic evidence references
  caseData.forensicAnalyses.forEach(fa => {
    const evidenceExists = caseData.evidence.some(e => e.evidenceId === fa.evidenceId);
    if (!evidenceExists) {
      errors.push(`Forensic analysis ${fa.analysisId} references unknown evidence: ${fa.evidenceId}`);
    }
  });
  
  // Check file paths exist
  const basePath = caseJsonPath.replace('/case.json', '');
  caseData.documents.forEach(doc => {
    const fullPath = `${basePath}/${doc.filePath}`;
    if (!fs.existsSync(fullPath)) {
      errors.push(`Document file not found: ${doc.filePath}`);
    }
  });
  
  if (errors.length > 0) {
    console.error('Validation errors:');
    errors.forEach(err => console.error(`  - ${err}`));
    return false;
  }
  
  console.log('✅ Case validation passed!');
  return true;
}

// Usage: node case-validator.js cases/CASE-2024-001/case.json
validateCase(process.argv[2]);
```

### Clue Checker Tool

```python
# clue-checker.py
import json
import sys

def check_clues(case_json_path):
    with open(case_json_path, 'r') as f:
        case = json.load(f)
    
    solution = case['solution']
    culprit = solution['culprit']
    key_evidence = solution['keyEvidence']
    
    print(f"\n🔍 Checking case: {case['metadata']['title']}\n")
    
    # Find all mentions of culprit
    culprit_mentions = []
    for doc in case['documents']:
        if culprit in str(doc):
            culprit_mentions.append(doc['documentId'])
    
    print(f"Culprit ({culprit}) mentioned in documents: {culprit_mentions}")
    
    # Check if key evidence is discoverable
    discoverable_evidence = []
    for ev_id in key_evidence:
        # Check if evidence is in evidence list
        evidence_item = next((e for e in case['evidence'] if e['evidenceId'] == ev_id), None)
        if evidence_item:
            discoverable_evidence.append(ev_id)
        # Check if it's a document
        document_item = next((d for d in case['documents'] if d['documentId'] == ev_id), None)
        if document_item:
            discoverable_evidence.append(ev_id)
    
    print(f"Key evidence discoverable: {len(discoverable_evidence)}/{len(key_evidence)}")
    
    # Check forensic analyses
    forensic_available = [fa['analysisId'] for fa in case['forensicAnalyses']]
    forensic_in_solution = [ev for ev in key_evidence if ev.startswith('FA-')]
    
    print(f"Forensic analyses in solution: {forensic_in_solution}")
    
    # Timeline consistency check
    timeline_dates = [event['dateTime'] for event in case['timeline']]
    is_chronological = all(timeline_dates[i] <= timeline_dates[i+1] for i in range(len(timeline_dates)-1))
    
    print(f"Timeline is chronological: {is_chronological}")
    
    print("\n✅ Clue check complete!\n")

# Usage: python clue-checker.py cases/CASE-2024-001/case.json
check_clues(sys.argv[1])
```

---

## 10.11 Writer Guidelines Document

### Writing Principles (Summary)

Refer to [05-NARRATIVE.md](05-NARRATIVE.md) for complete guidelines.

**Key Points:**
- Authenticity over drama
- Show, don't tell
- Respect player intelligence
- Subtle clues, not obvious
- Realistic police procedures
- No anachronisms
- Consistent character voices

### Common Mistakes to Avoid

**❌ Over-Explaining:**
"The suspect lied about his alibi because he was actually at the crime scene killing the victim."
- Too direct, removes mystery

**✅ Show Evidence:**
"Suspect claims he was home at 11pm. Security footage shows him leaving building at 10:45pm."
- Player makes connection

**❌ Contradictory Timeline:**
"Victim last seen at 10pm. Crime occurred at 9pm."
- Impossible, breaks logic

**✅ Consistent Timeline:**
"Victim last seen at 10pm. Crime occurred between 10:30pm-11:00pm per forensics."
- Logical, allows investigation

**❌ Unearned Red Herrings:**
"Suspect B has motive and opportunity but didn't do it. No reason why."
- Frustrating, feels like cheat

**✅ Disprovable Red Herrings:**
"Suspect B has motive, but DNA evidence proves he wasn't at scene."
- Player discovers truth via investigation

---

## 10.12 Difficulty Calibration

### Difficulty Scoring Formula

(From [04-CASE-STRUCTURE.md](04-CASE-STRUCTURE.md))

```
Score = (suspects × 5) + (evidence × 3) + (documents × 4) + (forensics × 5) + (red_herrings × 2) + (timeline_complexity × 10)
```

**Tiers:**
- Easy: 0-45
- Medium: 46-90
- Hard: 91-135
- Expert: 136-180

### Calibration Testing

**Process:**
1. Calculate initial difficulty score
2. QA tester solves case, records time
3. Compare actual time to estimated time
4. Adjust complexity if mismatch:
   - Too easy? Add red herrings, hide clues deeper
   - Too hard? Clarify critical clues, reduce misleads
5. Re-test with second QA tester
6. Final difficulty rating assigned

**Time Targets:**
- Easy: 1.5-3 hours
- Medium: 3-6 hours
- Hard: 6-9 hours
- Expert: 9-12 hours

---

## 10.13 Post-Launch Content Pipeline

### Launch Content (MVP)

**Initial Library:**
- 3 Easy cases
- 3 Medium cases
- 2 Hard cases
- 1 Expert case
- **Total:** 9 cases

**Production Schedule Pre-Launch:**
- 6 months before launch
- 1-2 writers active
- 1 case completed every 3 weeks
- Buffer time for polish

### Post-Launch Cadence

**Months 1-3 (Stabilization):**
- Focus on bug fixes
- No new cases (use backlog if needed)
- 1 case in production (release Month 4)

**Months 4-6 (Steady State):**
- 1 new case per month
- 2 cases in production pipeline
- Hire additional writer if demand high

**Months 7-12 (Expansion):**
- 1-2 new cases per month
- Introduce seasonal/themed cases
- Begin localization of top 3 cases

**Year 2+:**
- 12-18 cases per year
- Expand to 3-4 writers
- Full localization pipeline
- Consider UGC case editor (future)

---

## 10.14 Content Metrics & Analytics

### Success Metrics

**Per Case:**
- **Completion Rate:** % of players who start and complete
- **Success Rate:** % of completed cases solved correctly
- **Average Time:** Mean time to completion
- **First Attempt Success:** % solved on first try
- **Forensic Usage:** % using forensics, avg # requests
- **Difficulty Rating:** Player-reported difficulty vs intended

**Content Health:**
- **Case Availability:** Cases per difficulty tier
- **Backlog:** Cases in production
- **Velocity:** Cases completed per month
- **Quality Score:** Avg QA rating (1-10 scale)

### Data Collection

**From Gameplay:**
- Session start/end times
- Submission attempts
- Forensic requests made
- Documents viewed (optional, privacy-aware)
- Solution correctness

**From Surveys:**
- Post-case difficulty rating (1-5 stars)
- Enjoyment rating (1-5 stars)
- Open-ended feedback

### Iteration Based on Data

**If Completion Rate < 50%:**
- Case too hard or too long
- Review for clarity
- Consider revisions or retirement

**If First Attempt Success > 80%:**
- Case too easy
- Add complexity or re-tier

**If Forensic Usage < 30%:**
- Forensics not compelling or unnecessary
- Review forensic integration

---

## 10.15 Budget & Resources

### Production Costs (Per Case)

**Labor:**
- Writer: 80-100 hours @ $40-60/hr = $3,200-6,000
- Document Designer: 24-32 hours @ $30-50/hr = $720-1,600
- QA Tester: 16-24 hours @ $25-40/hr = $400-960
- Content Manager: 8-12 hours @ $50-75/hr = $400-900
- **Total Labor:** $4,720-9,460 per case

**Assets:**
- Stock photos (if needed): $0-500
- Custom photography: $500-2,000 (if required)
- Graphic assets: $200-500
- **Total Assets:** $700-3,000 per case

**Total Cost Per Case:** $5,420-12,460

**Annual Content Budget (18 cases/year):**
- Low: $97,560
- High: $224,280
- **Average:** ~$160,000/year

### Team Size

**Launch Team:**
- 2 Case Writers (full-time)
- 1 Document Designer (full-time)
- 1 Forensic Writer (part-time/consultant)
- 1 QA Tester (full-time)
- 1 Content Manager (full-time)
- **Total:** 5-6 people

**Year 2 Team:**
- 3-4 Case Writers
- 1-2 Document Designers
- 1-2 QA Testers
- 1 Content Manager
- 1 Localization Coordinator (new)
- **Total:** 7-10 people

---

## 10.16 Summary

**Content Pipeline:**
- 7-phase workflow from concept to publication
- 15-21 days per case
- Velocity: 1-2 cases per month per writer

**Quality Assurance:**
- Writer self-test
- Peer review
- QA tester blind play-through
- Content Manager approval
- Technical validation

**Tools & Systems:**
- Git for version control
- Case validator for integrity
- Clue checker for solvability
- Document templates for consistency
- CMS for tracking

**Production:**
- Launch: 9 cases (3 Easy, 3 Medium, 2 Hard, 1 Expert)
- Post-launch: 1-2 cases per month
- Year 2: 12-18 cases per year
- Localization starts Month 12

**Budget:**
- $5,420-12,460 per case
- ~$160,000/year for 18 cases
- Team: 5-6 people at launch, 7-10 in Year 2

**Content Health:**
- Track completion rate, success rate, time
- Iterate based on player data
- Retire cases after 3+ years
- Maintain 2-4 weeks backlog

---

**Next Chapter:** [11-TESTING.md](11-TESTING.md) - Testing strategy and QA processes

**Related Documents:**
- [04-CASE-STRUCTURE.md](04-CASE-STRUCTURE.md) - Case requirements
- [05-NARRATIVE.md](05-NARRATIVE.md) - Writing guidelines
- [09-DATA-SCHEMA.md](09-DATA-SCHEMA.md) - Data validation

---

**Revision History:**

| Date | Version | Changes | Author |
|------|---------|---------|--------|
| 2025-11-14 | 1.0 | Initial complete draft | AI Assistant |
