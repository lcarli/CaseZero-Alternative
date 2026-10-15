# Chapter 03 - Mechanics

**Game Design Document - CaseZero v3.0**  
**Last Updated:** November 13, 2025  
**Status:** ✅ Complete

---

## 3.1 Overview

This chapter details the **specific systems and mechanics** that power CaseZero's gameplay. Each mechanic is designed to support the core pillars (Authenticity, Autonomy, Analysis, Patience) while maintaining a realistic investigation experience.

**Core Systems:**
1. Document Viewing System
2. Evidence Examination System
3. Forensic Request System
4. Note-Taking System
5. Timeline System
6. Solution Submission System
7. Case Session Management
8. Detective Progression System

---

## 3.2 Document Viewing System

The primary interaction mechanic - reading investigative documents.

### Document Types

**1. Police Reports**
- **Format:** PDF, 2-5 pages
- **Content:** Official incident report, scene description, initial findings
- **Structure:** Header (date, officer, case#), narrative, evidence log
- **Availability:** Always available from case start
- **Example:** "Downtown Office Building - Homicide Report #2023-0315"

**2. Witness Statements**
- **Format:** PDF, 1-3 pages
- **Content:** Transcribed interview Q&A or written statement
- **Structure:** Header (witness name, date), statement body, signature
- **Availability:** Available from start (most cases)
- **Example:** "Statement of John Silva, Night Security Guard"

**3. Suspect Interviews**
- **Format:** PDF, 2-4 pages
- **Content:** Interview transcript with suspect
- **Structure:** Header, Q&A format, interviewer notes
- **Availability:** Available from start
- **Example:** "Interview Transcript - Michael Torres, 03/16/2023"

**4. Forensic Reports**
- **Format:** PDF, 2-3 pages
- **Content:** Technical analysis results
- **Structure:** Lab header, methodology, findings, conclusions
- **Availability:** Only after requesting and waiting
- **Example:** "DNA Analysis Report - Evidence #EV-004"

**5. Personal Documents**
- **Format:** PDF or image, 1-2 pages
- **Content:** Letters, emails, diary entries, notes
- **Structure:** Varies (personal format)
- **Availability:** Found in victim's effects (available from start)
- **Example:** "Email exchange between victim and suspect"

**6. Financial Records**
- **Format:** PDF, 1-2 pages
- **Content:** Bank statements, transaction logs
- **Structure:** Table format with dates and amounts
- **Availability:** Obtained via investigation (available from start)
- **Example:** "Bank Statement - Robert Chen, January-March 2023"

**7. Background Records**
- **Format:** PDF, 1 page
- **Content:** Criminal history, employment records, medical records
- **Structure:** Database printout style
- **Availability:** Available from start
- **Example:** "Criminal Background Check - Michael Torres"

### Viewing Interface

Document reading happens inside the existing **File Viewer** application on the in-game desktop. We reuse the same window that already handles other file types, so opening a PDF feels identical to double-clicking a file on a real workstation.

**File Viewer layout:**

```text
┌─────────────────────────────────────────────┐
│  File Viewer · Incident Report 2023-0315    │
├─────────────────────────────────────────────┤
│  Page 1 / 3        [Fit] [100%] [150%]      │
├─────────────────────────────────────────────┤
│                                             │
│        RENDERED PDF CONTENT HERE            │
│                                             │
│   (standard vertical scroll, neutral bg)    │
│                                             │
└─────────────────────────────────────────────┘
```

**Available behavior (all inherited from File Viewer):**

- Vertical scroll or Page Up/Down for pagination
- Simple zoom presets: Fit to width, 100%, 150%
- Native PDF rendering with antialiasing and soft gray background
- Copy text or drag images directly into the Notes app
- Light/Dark toggle follows the global desktop setting

**Intentionally not included:**

- Bookmarks, highlights, search, print, or any extra HUD widgets
- Automatic clue markers or summaries
- Overlay hints, guided reading, or extra navigation chrome

> Goal: let the detective open a PDF, read it comfortably, and close it—nothing more elaborate than a clean file viewer.

### Document Metadata

Each document has hidden metadata (not shown to player but affects gameplay):

```json
{
  "id": "DOC-001",
  "type": "PoliceReport",
  "title": "Incident Report #2023-0315",
  "fileName": "police-report-2023-0315.pdf",
  "author": "Officer Sarah Martinez",
  "dateCreated": "2023-03-16T08:00:00Z",
  "tags": ["initial", "official", "scene"],
  "availableAt": "start",
  "relatedEvidence": ["EV-001", "EV-002"],
  "relatedSuspects": ["SUSP-001", "SUSP-002"],
  "contradicts": ["DOC-003"],
  "pageCount": 3,
  "importance": "critical"
}
```

**Note:** Metadata is for design/testing only. Players discover relationships by reading.

---

## 3.3 Evidence Examination System

Viewing and analyzing physical evidence through photographs.

### Evidence Types

**Physical Evidence:**
- Weapons (guns, knives, blunt objects)
- Personal items (wallets, phones, keys)
- Clothing (bloodstained, torn, etc.)
- Tools (used in crime)

**Biological Evidence:**
- Blood samples
- Hair/fiber samples
- Bodily fluids
- Tissue samples

**Trace Evidence:**
- Fingerprints
- Footprints
- Tire tracks
- Paint chips

**Digital Evidence (Future):**
- Phone records
- Computer files
- Security footage screenshots

**Documents as Evidence:**
- Handwritten notes
- Forged documents
- Ransom notes
- Threatening letters

### Evidence Photo Presentation

Each evidence entry now shows **only one primary photo**. No alternate angles, no gallery controls—just the same kind of printed photo you would get in a physical cold-case binder.

```text
┌─────────────────────────────────────────────┐
│ Evidence #EV-001: Firearm (.38 Caliber)     │
├─────────────────────────────────────────────┤
│                                             │
│        PRIMARY PHOTO (with scale)           │
│                                             │
├─────────────────────────────────────────────┤
│ Type: Physical - Weapon                     │
│ Collected: 03/16/2023 02:00 AM             │
│ Location: Crime scene, near victim          │
│ Collected by: CSI Team Alpha                │
│                                             │
│ [🔬 Available Analyses: Ballistics,         │
│     Fingerprints]                           │
│ [📋 View Chain of Custody]                  │
└─────────────────────────────────────────────┘
```

If we need to highlight a detail, it appears inside the supporting documents (e.g., circled in the police report) rather than as separate photos. This keeps the File Viewer simple and focused on reading.

### Evidence Metadata

```json
{
  "id": "EV-001",
  "name": "Firearm - .38 Caliber",
  "type": "Physical",
  "subtype": "Weapon",
  "description": "Smith & Wesson .38 Special revolver, found 3 feet from victim",
  "photos": [
    "evidence/ev001-overview.jpg",
    "evidence/ev001-closeup.jpg",
    "evidence/ev001-serial.jpg"
  ],
  "collectedBy": "CSI Team Alpha",
  "collectedAt": "2023-03-16T02:00:00Z",
  "collectedFrom": "Crime scene, 15th floor office",
  "chainOfCustody": ["CSI Alpha", "Evidence Room", "Forensics Lab"],
  "forensicAnalysisAvailable": [
    {
      "type": "Ballistics",
      "duration": 12,
      "durationUnit": "minutes"
    },
    {
      "type": "Fingerprints",
      "duration": 8,
      "durationUnit": "minutes"
    }
  ],
  "tags": ["weapon", "critical", "firearm"],
  "relatedEvidence": ["EV-002"],
  "availableAt": "start"
}
```

### Evidence Interaction

**What Players Can Do:**
- ✅ View high-resolution photos
- ✅ Zoom and pan the single archived photo
- ✅ Read evidence description
- ✅ See collection metadata
- ✅ Request forensic analysis
- ✅ Cross-reference with documents

**What Players Cannot Do:**
- ❌ Rotate 3D model (it's a photo, not 3D)
- ❌ "Use" evidence on other evidence
- ❌ Collect new evidence
- ❌ Contaminate evidence
- ❌ Re-examine at scene (it's in evidence room)

---

## 3.4 Forensic Request System

The core time-based mechanic that creates pacing and anticipation. In the current backend this is implemented as asynchronous, elapsed-time requests rather than live-streaming lab work.

### Forensic Analysis Types

> **Accelerated timing (new default):** players now see real-time minutes. Every real minute equals one in-world hour, so "12-hour" lab work finishes in 12 real minutes.

**DNA Analysis**
- **Accelerated Duration:** 24 minutes (equals 24 in-world hours)
- **Applied to:** Blood, hair, saliva, tissue
- **Results:** DNA profile, potential matches to suspects/database
- **Cost:** None (unlimited requests)
- **Example Output:** "DNA profile matches Michael Torres (99.7% confidence)"

**Ballistics Analysis**
- **Accelerated Duration:** 12 minutes (12h in-lore)
- **Applied to:** Firearms, bullets, casings
- **Results:** Weapon identification, trajectory, match to bullets
- **Example Output:** "Bullet recovered from victim fired from Evidence #EV-001"

**Fingerprint Analysis**
- **Accelerated Duration:** 8 minutes (8h in-lore)
- **Applied to:** Prints on surfaces, weapons, objects
- **Results:** Print identification, matches to suspects
- **Example Output:** "Partial print on weapon matches right thumb of SUSP-002"

**Toxicology**
- **Accelerated Duration:** 36 minutes (36h in-lore)
- **Applied to:** Blood samples, tissue
- **Results:** Drugs, poisons, alcohol levels
- **Example Output:** "Blood toxicology: 0.08% BAC, traces of sleeping medication"

**Trace Evidence Analysis**
- **Accelerated Duration:** 16 minutes (16h in-lore)
- **Applied to:** Fibers, hair, paint chips
- **Results:** Material identification, potential sources
- **Example Output:** "Fiber matches carpet in suspect's vehicle"

**Handwriting Analysis**
- **Accelerated Duration:** 10 minutes (10h in-lore)
- **Applied to:** Written documents
- **Results:** Author identification, forgery detection
- **Example Output:** "Signature on document likely forged"

**Digital Forensics (Future)**
- **Accelerated Duration:** 48 minutes (48h in-lore)
- **Applied to:** Phones, computers, storage
- **Results:** Deleted files, metadata, communication logs

### Request Flow

**Step 1: Select Evidence**

```text
Forensics Lab > Available Evidence

EV-001: Firearm (.38 Caliber)
  [✓] Ballistics Analysis (12 min)
  [✓] Fingerprint Analysis (8 min)
  [ ] Request Selected Analyses

EV-004: Blood Sample
  [✓] DNA Analysis (24 min)
  [✓] Toxicology (36 min)
  [ ] Request Selected Analyses
```

**Step 2: Confirm Request**

```text
┌─────────────────────────────────────────────┐
│ Confirm Forensic Analysis Request           │
├─────────────────────────────────────────────┤
│ Evidence: EV-001 - Firearm                  │
│ Analysis: Ballistics                        │
│ Duration: 12 minutes (equals 12h in-world)  │
│                                             │
│ Estimated Completion: 03/17/2023 02:00 PM   │
│  (in ~12 real minutes)                      │
│                                             │
│ Note: You can continue investigating while  │
│ waiting for results.                        │
│                                             │
│ [Cancel] [Submit Request]                   │
└─────────────────────────────────────────────┘
```

**Step 3: Wait Period**

```text
Forensics Lab > Pending Requests

EV-001 - Ballistics Analysis
  Requested: 03/17/2023 02:00 AM
  Status: In Progress
  Completion: 03/17/2023 02:00 PM (12 real min / 12h in-world remaining)
  [View Status]

EV-004 - DNA Analysis
  Requested: 03/17/2023 02:05 AM
  Status: In Progress  
  Completion: 03/18/2023 02:05 AM (24 real min / 24h in-world remaining)
  [View Status]
```

**Step 4: Results Ready**

```text
Forensics Lab > Completed Analyses

✓ EV-001 - Ballistics Analysis
  Completed: 03/17/2023 02:00 PM
  [View Report]  ← Opens PDF with results

⏱ EV-004 - DNA Analysis
  In Progress (24 real min / 24h in-world remaining)
```

When a lab report finishes, the player receives a **plain email from the Forensics Lab** (subject line "Report Ready", short body listing the evidence ID plus a "Download report" link). Clicking the link pushes the PDF into the in-game **File Viewer**, so it opens exactly like any other digital dossier page.

### Time Mechanics

**Accelerated Mode (now the default):**
- UI shows real minutes that map 1:1 to in-world hours
- A "12-hour" report finishes in 12 real minutes
- Keeps procedural weight without multi-hour waits
- Can be disabled in settings for players who want the long-form experience

**Real-Time Mode (optional):**
- Restores the original hour-long timers
- Player can close the game and return later
- Progress persists server-side
- Great for long streams or slow-burn roleplay

**Instant Mode (Accessibility):**
- All analyses complete immediately
- For players with limited time
- Clearly labeled as "Story Mode"
- No rank progression in this mode

### Multiple Requests

**Parallel Processing:**
- ✅ Can request multiple analyses simultaneously
- ✅ Each runs on independent timer
- ✅ No queue or slot limits
- ✅ All complete at their scheduled times

**Example Timeline:**

```text
02:00 AM in-world (T+0 real min)  - Request Ballistics (12 min), DNA (24 min), Fingerprints (8 min)
10:00 AM in-world (T+8 real min)  - Fingerprints ready
02:00 PM in-world (T+12 real min) - Ballistics ready
02:00 AM next day in-world (T+24 real min) - DNA ready
```

### Forensic Report Format

**Report Structure (PDF):**
```
┌─────────────────────────────────────────────┐
│ METROPOLITAN FORENSICS LABORATORY            │
│ BALLISTICS ANALYSIS REPORT                  │
│                                             │
│ Case: #2023-0315                            │
│ Evidence: EV-001 - Firearm (.38 Caliber)   │
│ Analyst: Dr. James Chen, PhD                │
│ Date: March 17, 2023                        │
│                                             │
│ ANALYSIS PERFORMED:                         │
│ Firearm examination and bullet comparison   │
│                                             │
│ METHODOLOGY:                                │
│ Comparison microscopy, rifling analysis     │
│                                             │
│ FINDINGS:                                   │
│ 1. Weapon: Smith & Wesson .38 Special      │
│ 2. Serial number: [redacted]                │
│ 3. Bullet recovered from victim (EV-002)   │
│    matches rifling pattern of EV-001        │
│ 4. Gunshot residue present on grip         │
│                                             │
│ CONCLUSIONS:                                │
│ Evidence #EV-001 was the weapon used in    │
│ the fatal shooting. High confidence match.  │
│                                             │
│ [Signature: Dr. James Chen]                │
└─────────────────────────────────────────────┘
```

### Design Rationale

**Why Real-Time Forensics?**
1. **Creates Anticipation:** Something to look forward to
2. **Mimics Reality:** Real forensics takes time
3. **Encourages Multi-Session:** Natural breakpoint
4. **Prevents Spam:** Can't just request everything instantly
5. **Adds Weight:** Makes results feel meaningful

**Why Unlimited Requests?**
1. **No Artificial Scarcity:** Doesn't feel "gamey"
2. **Player Freedom:** Request what you think is important
3. **No Punishment:** Won't be penalized for exploring
4. **Realistic:** Real police can request analyses

---

## 3.5 Note-Taking System

Simple but essential tool for player-driven analysis.

### Notebook Interface

**Simple Text Editor:**
```
┌─────────────────────────────────────────────┐
│ Detective's Notebook                         │
├─────────────────────────────────────────────┤
│ [New Note] [Case Notes] [Theories] [Qs]     │
├─────────────────────────────────────────────┤
│                                             │
│  Case #2023-0315 - My Investigation        │
│  ───────────────────────────────────────   │
│                                             │
│  SUSPECTS:                                  │
│  - Michael Torres: Business partner,       │
│    financial dispute, weak alibi           │
│  - Linda Chen: Wife, life insurance,       │
│    but seems genuinely grieving            │
│  - David Park: Fired employee, access      │
│                                             │
│  TIMELINE:                                  │
│  10:00 PM - Victim enters office (CCTV)    │
│  11:15 PM - Torres seen entering (log)     │
│  11:30 PM - Estimated time of death        │
│                                             │
│  QUESTIONS:                                 │
│  - Torres alibi doesn't check out          │
│  - Why was victim there late?              │
│  - Where's the missing $500k?              │
│                                             │
└─────────────────────────────────────────────┘
```

**Features:**
- ✅ Free-form text entry
- ✅ Auto-save every 30 seconds
- ✅ Persistent across sessions
- ✅ Can have multiple notes
- ✅ Can copy/paste from documents
- ✅ Basic formatting (bold, italic, lists)
- ✅ Export to text file

**No Features:**
- ❌ No auto-population of clues
- ❌ No suggestion system
- ❌ No highlighting/tagging integration
- ❌ No AI summary
- ❌ No connection mapping (too prescriptive)

### Note Organization

**Tab System:**
- **Case Notes:** General observations
- **Suspects:** Notes on each person
- **Evidence:** Evidence observations
- **Timeline:** Chronological notes
- **Questions:** Open questions
- **Theory:** Current working theory

**Or:** Player can organize however they want (free-form)

### Design Philosophy

**"A Blank Page, Not A Template"**
- Give players space to think
- Don't structure their thinking
- Let them develop their own system
- Note-taking is personal

**Why No Advanced Features?**
- Want players to engage mentally, not rely on tool
- Connections should be in player's mind
- Over-systematization can reduce discovery joy
- Simplicity keeps focus on case

---

## 3.6 Timeline System

Visual representation of case events (drawn from documents).

### Timeline View

**Chronological Display:**
```
┌─────────────────────────────────────────────┐
│ Case Timeline - March 15, 2023             │
├─────────────────────────────────────────────┤
│                                             │
│ 10:00 PM ─────┐                            │
│               └─ Victim enters building     │
│                  (CCTV, Security Log)       │
│                                             │
│ 10:30 PM ─────┐                            │
│               └─ Last seen alive            │
│                  (Witness: Night Guard)     │
│                                             │
│ 11:15 PM ─────┐                            │
│               └─ Suspect A enters building  │
│                  (Security Log)             │
│                                             │
│ 11:30 PM ─────┐                            │
│               └─ Estimated time of death    │
│                  (Forensic Report)          │
│                                             │
│ 11:45 PM ─────┐                            │
│               └─ Suspect A exits building   │
│                  (CCTV)                     │
│                                             │
│ 12:30 AM ─────┐                            │
│               └─ Body discovered            │
│                  (Security Guard)           │
│                                             │
└─────────────────────────────────────────────┘
```

### Timeline Data Source

**Automatically Populated From:**
- Document timestamps
- Witness statement times
- Forensic estimates
- Security logs
- CCTV data

**Players Cannot:**
- ❌ Add custom events (data must come from case files)
- ❌ Edit event descriptions
- ❌ Change timestamps

**Players Can:**
- ✅ View events chronologically
- ✅ Filter by source (CCTV, witnesses, etc.)
- ✅ Click event to see source document
- ✅ Identify gaps in timeline

### Design Purpose

**Why Include Timeline:**
- Helps visualize sequence of events
- Makes gaps obvious (where we don't know what happened)
- Useful for identifying alibi conflicts
- Reduces cognitive load of remembering timestamps

**Why Keep It Simple:**
- Not a puzzle to solve
- Just a visual aid
- Data comes from documents (no hidden info)
- Player still does analysis

---

## 3.7 Solution Submission System

The high-stakes culmination of investigation.

### Submission Form

**Multi-Part Submission:**
```
┌─────────────────────────────────────────────┐
│ Submit Case Solution                         │
├─────────────────────────────────────────────┤
│                                             │
│ WHO COMMITTED THE CRIME?                    │
│ [Select Suspect ▼]                          │
│ ├─ Michael Torres                           │
│ ├─ Linda Chen                               │
│ ├─ David Park                               │
│ └─ Other/Unknown                            │
│                                             │
│ WHAT WAS THE MOTIVE?                        │
│ ┌───────────────────────────────────────┐   │
│ │ Financial dispute over $500k debt.    │   │
│ │ Torres needed money and was being     │   │
│ │ pushed out of company...              │   │
│ └───────────────────────────────────────┘   │
│                                             │
│ HOW WAS IT COMMITTED?                       │
│ ┌───────────────────────────────────────┐   │
│ │ Torres used his key to access office │   │
│ │ late at night. Shot victim with .38   │   │
│ │ from evidence. Made it look like...   │   │
│ └───────────────────────────────────────┘   │
│                                             │
│ KEY EVIDENCE:                               │
│ [Select Evidence]                           │
│ ☑ EV-001 - Firearm (ballistics match)      │
│ ☑ EV-004 - DNA (Torres at scene)           │
│ ☑ DOC-007 - Financial records             │
│                                             │
│ Attempts Remaining: 3/3                     │
│                                             │
│ [Cancel] [Submit Solution]                  │
└─────────────────────────────────────────────┘
```

### Validation System

**Automatic Checks:**
1. **Culprit Selected?** (Required)
2. **Motive Explained?** (Minimum 50 words)
3. **Method Described?** (Minimum 50 words)
4. **At Least One Evidence Selected?** (Required)

**If Missing:**
```
⚠️ Incomplete Submission
Please provide:
- Explanation of motive (at least 50 words)
- At least one piece of supporting evidence

[Go Back]
```

### Solution Evaluation

**Server-Side Comparison:**

```typescript
function evaluateSolution(submission, correctSolution) {
  // 1. Check culprit
  const culpritCorrect = submission.culprit === correctSolution.culprit;
  
  // 2. Check key evidence cited
  const keyEvidenceCited = submission.evidence.some(e => 
    correctSolution.keyEvidence.includes(e)
  );
  
  // 3. Analyze explanation quality (future: ML-based)
  const explanationLength = submission.motive.length + submission.method.length;
  const thoughtfulExplanation = explanationLength > 200;
  
  return {
    isCorrect: culpritCorrect,
    hadKeyEvidence: keyEvidenceCited,
    wasThoughtful: thoughtfulExplanation,
    score: calculateScore(culpritCorrect, keyEvidenceCited, thoughtfulExplanation)
  };
}
```

### Feedback Screen

**Correct Solution:**
```
┌─────────────────────────────────────────────┐
│ ✓ CASE SOLVED                               │
├─────────────────────────────────────────────┤
│                                             │
│ Excellent work, Detective!                  │
│                                             │
│ You correctly identified Michael Torres    │
│ as the culprit and demonstrated strong     │
│ analytical skills in connecting the        │
│ evidence.                                   │
│                                             │
│ YOUR ANALYSIS:                              │
│ • Identified correct culprit ✓             │
│ • Cited key evidence ✓                     │
│ • Explained motive thoroughly ✓            │
│                                             │
│ RECORDS (server):                           │
│ • Time logged: 18 min total                │
│ • Cases solved: 5/6 toward Detective I     │
│ • Case Status: SOLVED                      │
│                                             │
│ [View Full Solution] [Next Case]           │
└─────────────────────────────────────────────┘
```

**Incorrect Solution (Attempts Remaining):**
```
┌─────────────────────────────────────────────┐
│ ✗ INCORRECT                                 │
├─────────────────────────────────────────────┤
│                                             │
│ Your conclusion does not match the         │
│ evidence.                                   │
│                                             │
│ FEEDBACK:                                   │
│ • The suspect you identified has a solid   │
│   alibi for the time of the crime.        │
│ • Consider re-examining the forensic       │
│   reports, particularly the DNA analysis.  │
│ • Timeline shows a discrepancy in the      │
│   witness statements - look closer.        │
│                                             │
│ Attempts Remaining: 2/3                     │
│                                             │
│ Take your time. Review the evidence and    │
│ try again when ready.                      │
│                                             │
│ [Return to Investigation]                   │
└─────────────────────────────────────────────┘
```

**Failed (No Attempts Remaining):**
```
┌─────────────────────────────────────────────┐
│ CASE REMAINS UNSOLVED                       │
├─────────────────────────────────────────────┤
│                                             │
│ You've exhausted all submission attempts.  │
│                                             │
│ This case was particularly challenging.    │
│ You can review the solution now to learn   │
│ what you missed, or return to this case    │
│ after solving 2 more cases.                │
│                                             │
│ RECORD:                                     │
│ • Time logged: 24 min (for reference)      │
│ • Case Status: UNSOLVED (Reviewed)        │
│                                             │
│ [View Solution] [Return to Dashboard]      │
└─────────────────────────────────────────────┘
```

### Attempt Limit Rationale

**Why 3 Attempts?**
1. **Prevents Guessing:** Can't just try every suspect
2. **Adds Stakes:** Makes submission meaningful
3. **Encourages Thoroughness:** Players investigate fully before submitting
4. **Realistic:** Real detectives need strong evidence before accusing

**Why Not Infinite?**
- Would encourage trial-and-error over deduction
- Removes tension from submission
- Players wouldn't take it seriously

**Why Not 1 Attempt?**
- Too punishing for honest mistakes
- Doesn't allow learning from error
- Would frustrate players

---

## 3.8 Case Session Management

Behind-the-scenes system managing player progress.

### Session Data

**What's Tracked:**
```json
{
  "sessionId": "uuid-123",
  "userId": "user-456",
  "caseId": "CASE-2024-001",
  "startedAt": "2023-11-13T14:00:00Z",
  "lastAccessAt": "2023-11-13T15:30:00Z",
  "status": "Active",
  "progress": {
    "documentsRead": ["DOC-001", "DOC-002", "DOC-005"],
    "evidenceViewed": ["EV-001", "EV-004"],
    "forensicRequests": [
      {
        "id": "req-1",
        "evidenceId": "EV-001",
        "analysisType": "Ballistics",
        "requestedAt": "2023-11-13T14:30:00Z",
        "completedAt": "2023-11-14T02:30:00Z",
        "status": "Pending"
      }
    ],
    "notebookEntries": ["Suspect analysis", "Timeline notes"],
    "submissionAttempts": 0,
    "timeSpent": 5400
  }
}
```

**What's NOT Tracked:**
- ❌ Which documents were read first (no forced order)
- ❌ How long on each document (no speed metrics)
- ❌ Mouse movements or "interest" heatmaps
- ❌ Number of times evidence viewed (irrelevant)

### Save System

**Auto-Save:**
- Every 30 seconds
- On window focus loss
- On app close
- On navigation away from case

**Manual Save:**
- Not needed (always auto-saving)
- No "save slots" (one save per case per user)

**Resume:**
- Opens exactly where left off
- Forensics timers continue server-side
- Notes preserved
- Window positions saved (optional)

---

## 3.9 Detective Progression System

Long-term advancement through ranks.

### Rank Structure

**Official rank / difficulty ladder (7 tiers):** the live codebase uses the same seven-level ladder for both case difficulty and detective rank. Promotions are based on **cumulative graded-correct case resolves** — not XP, and not per-difficulty quotas.

1. **Rookie**
  - Starting rank
  - Entry-level case difficulty
  - **Promotion threshold:** 0 graded-correct resolves

2. **Detective**
  - First promoted rank
  - **Promotion threshold:** 3 graded-correct resolves

3. **Senior Detective** (`Detective2`)
  - Established investigator
  - **Promotion threshold:** 8 graded-correct resolves

4. **Sergeant**
  - Senior field investigator
  - **Promotion threshold:** 16 graded-correct resolves

5. **Lieutenant**
  - Advanced case access
  - **Promotion threshold:** 28 graded-correct resolves

6. **Captain**
  - High-complexity case access
  - **Promotion threshold:** 44 graded-correct resolves

7. **Commander**
  - Highest implemented rank
  - **Promotion threshold:** 65 graded-correct resolves

### Case Logging & Timekeeping

- No XP, no score multipliers—**progression follows cumulative graded-correct resolves**.
- When the player submits the final report, the backend records the case outcome plus:
  - `caseId`
  - `difficulty`
  - `resolvedAt`
  - `elapsedMinutes` (total time spent on that case)
- Promotions are computed from graded-correct resolves; incorrect or ungraded outcomes do not advance rank.
- `elapsedMinutes` still feeds internal timing comparisons and post-case summaries.
- The server stores one row per resolved case; there is no cumulative XP counter.

### Promotion Flow

1. Player resolves a cold case.
2. System stores the elapsed time and records whether the resolution counted as graded-correct.
3. Promotion service compares the cumulative graded-correct total against the live thresholds (3 / 8 / 16 / 28 / 44 / 65).
4. If a threshold is crossed, the profile rank updates immediately and higher-rank case access can unlock.
5. Only per-case history + timing remain in the save; no XP ledger or per-difficulty quota bookkeeping is persisted.

### Rank Benefits

**What Ranks Unlock:**
- ✅ Access to harder cases (gating)
- ✅ New case categories (when available)
- ✅ Profile badge/title

**What Ranks DON'T Give:**
- ❌ Mechanical advantages
- ❌ Faster forensics
- ❌ Better hints (there are no hints)
- ❌ Extra submission attempts
- ❌ Easier cases

**Philosophy:** Ranks reflect mastery, not power

---

## 3.10 Suspect System

How suspects are presented and investigated.

### Suspect Profile

**Information Provided:**
```
┌─────────────────────────────────────────────┐
│ SUSPECT PROFILE: Michael Torres             │
├─────────────────────────────────────────────┤
│ [Photo: Professional headshot]              │
│                                             │
│ AGE: 38                                     │
│ OCCUPATION: Business Partner, TechCorp      │
│ RELATIONSHIP: Victim's business associate   │
│                                             │
│ BACKGROUND:                                 │
│ Torres co-founded TechCorp with victim in  │
│ 2018. Minority shareholder (30%). Recent   │
│ tensions over company direction. Has MBA   │
│ from State University.                      │
│                                             │
│ MOTIVE:                                     │
│ Financial dispute - owed victim $500k.     │
│ Threatened buyout of his shares.           │
│                                             │
│ ALIBI:                                      │
│ Claims he was home alone watching TV from  │
│ 9 PM to midnight. No witnesses.            │
│                                             │
│ CRIMINAL RECORD:                            │
│ None                                        │
│                                             │
│ INTERVIEW TRANSCRIPT: [View DOC-004]        │
│ RELATED EVIDENCE: [EV-001, EV-004]         │
└─────────────────────────────────────────────┘
```

### Suspect Count Guidelines

Broad design guideline across the current seven-rank difficulty ladder:

- **Rookie:** 2-3 suspects
- **Detective / Senior Detective:** 4-5 suspects
- **Sergeant / Lieutenant:** 6-7 suspects
- **Captain / Commander:** 8+ suspects

### Red Herrings

**Innocent Suspects Must Be:**
- Plausible as culprits
- Have weak alibis or suspicious behavior
- Connected to victim
- Have apparent motives

**But Eventually:**
- Evidence exonerates them
- Alibi checks out on close examination
- Motive is less strong than it appears

**Example:**
- **Linda Chen (Wife):** Benefits from life insurance, but DNA places her at home during murder, CCTV confirms she didn't leave house

---

## 3.11 Tutorial Mechanics

Minimal onboarding that teaches essentials.

### Tutorial Structure (Streamlined)

**Screen 1: Welcome (10 seconds)**
```
Welcome to the Cold Case Division

You're a detective investigating archived cases.
Read documents, examine evidence, solve crimes.

[Continue]
```

**Screen 2: Desktop Tour (20 seconds)**
```
[Desktop shown with 3 apps]

This is your workspace. You have three tools:

📧 EMAIL - Case briefings
📁 CASE FILES - Documents and evidence  
🧪 FORENSICS LAB - Request analyses

Click EMAIL to start.
```

**Screen 3: Training Case Briefing (Read)**
```
[Email app opens automatically]

Read this briefing to learn about your first case.

[Tutorial case briefing displays - simple theft case]
```

**Screen 4: Explore Files (Prompt)**
```
Now open CASE FILES to examine evidence.

[Player opens app, sees 2 simple documents]
```

**Screen 5: Submit Solution (Guided)**
```
You've seen the evidence. Time to submit your conclusion.

Open the SUBMISSION APP and identify the thief.

[Player submits, gets immediate feedback]
```

**Screen 6: Complete (10 seconds)**
```
Training Complete!

You're now ready for real cases.
Remember: Investigate thoroughly before submitting.

[Start First Real Case]
```

**Total Time:** 3-5 minutes

### Post-Tutorial Discovery

**Forensics Discovery:**
- First real case has evidence that clearly needs analysis
- Lab app has "New!" badge
- Clicking evidence shows "Request Analysis" button
- Once clicked, player understands mechanic

**No hand-holding after tutorial:**
- No popup tips
- No objective markers
- No "you should do X now" messages

---

## 3.12 Quality of Life Features

Small but important mechanics that improve experience.

### Bookmarking System
- Mark documents/evidence as "Important"
- Quick access to bookmarked items
- Personal organization tool

### Search Function
- Search across all documents for keywords
- Finds mentions of names, locations, items
- Shows results with context

### Case Dashboard
- See all cases
- Filter by: Status (active, solved, unsolved), Difficulty
- Track overall stats

### Evidence Comparison
- View two pieces of evidence side-by-side
- Useful for finding similarities/differences
- Still requires player analysis

### Document Printing
- "Print" documents to personal folder
- Can reference without opening original
- More like "save for later"

### Window Management
- Minimize/maximize apps
- Arrange windows
- Remember positions across sessions

---

## 3.13 Accessibility Mechanics

Making the game playable for more people.

### Visual Accessibility
- High contrast mode
- Text size adjustment (UI only, not PDFs)
- Color-blind friendly palette option
- Screen reader support for UI

### Time Accessibility
- Forensics speed options (real-time, accelerated, instant)
- Pause forensics timer
- No time pressure anywhere

### Reading Accessibility
- Browser text-to-speech works on documents
- Dyslexia-friendly font option (UI)
- Clear, simple language in UI

### Motor Accessibility
- Full keyboard navigation
- No time-sensitive inputs
- No rapid clicking required
- Large click targets

---

## 3.14 Anti-Patterns (What We Avoid)

Mechanics deliberately NOT included:

### ❌ Hint System
**Why not:** Undermines player autonomy and deduction

### ❌ Objective List
**Why not:** Makes investigation prescriptive instead of exploratory

### ❌ Quest Markers
**Why not:** Removes the "figure it out yourself" challenge

### ❌ Mini-Games
**Why not:** Breaks immersion and authentic investigation feel

### ❌ Skill Checks/Stats
**Why not:** Player's real intelligence is the "stat"

### ❌ Inventory Management
**Why not:** All evidence is in lab, no collecting/carrying

### ❌ Resource Scarcity
**Why not:** Unlimited forensics, no money system, no artificial limits

### ❌ Energy/Stamina
**Why not:** Investigate as long as you want

### ❌ Daily Missions
**Why not:** Manipulative engagement pattern

### ❌ Social Pressure
**Why not:** No "your friend solved this faster"

---

## 3.15 Edge Case Handling

How systems handle unusual player behavior:

### Player Never Requests Forensics
**System Response:**
- ✅ Still allowed to submit solution
- ✅ Can solve without forensics (if smart enough)
- ❌ Won't get pestered to use forensics
- 🎖️ Get "Detective's Intuition" badge if correct

### Player Requests Everything Immediately
**System Response:**
- ✅ All requests accepted
- ⏱️ All timers run in parallel
- No punishment or penalty

### Player Takes 6 Months to Finish Case
**System Response:**
- ✅ All progress saved
- ✅ Forensics completed long ago
- 📝 "Case Summary" button to refresh memory
- No penalties for long breaks

### Player Copies Suspect Name Without Reading
**System Response:**
- ⚠️ Explanation field is required (prevents blind guessing)
- Must provide motive and method
- System detects copy-paste (future: flag suspicious submissions)

### Player Abandons Tutorial
**System Response:**
- ✅ Can skip and go straight to cases
- ✅ Help documentation available
- ❌ No forced re-tutorial

---

## 3.16 Metrics & Telemetry

What we measure (respectfully):

### Tracked Anonymously:
- ✅ Case completion rates
- ✅ Average time to solution
- ✅ Submission attempt distribution
- ✅ Forensics usage rates
- ✅ Document view counts (aggregate)

### NOT Tracked:
- ❌ Individual document reading time
- ❌ Exact player behavior patterns
- ❌ Mouse movements or clicks
- ❌ Personal investigation notes
- ❌ Anything that feels invasive

### Purpose:
- Balance difficulty
- Identify confusing cases
- Improve design
- NOT for engagement manipulation

---

## 3.17 Summary

**Core Mechanics:**

1. **Document Viewing** - Read PDFs, examine photos
2. **Forensics** - Request analyses, wait for results (real-time or accelerated)
3. **Note-Taking** - Free-form personal notes
4. **Timeline** - Visual event chronology
5. **Solution** - Submit culprit + explanation (3 attempts)
6. **Progression** - Ranks advance by solved cases; only total time is stored

**Mechanical Principles:**
- 🎯 Authentic (realistic forensics, no gamification)
- 🧠 Player-Driven (no hand-holding)
- 📚 Analysis-Focused (reading is the game)
- ⏳ Patient (real-time creates anticipation)

**What Makes It Work:**
- Simple mechanics, deep content
- No artificial barriers or manipulation
- Respects player intelligence
- Deduction over action

---

**Next Chapter:** [04-CASE-STRUCTURE.md](04-CASE-STRUCTURE.md) - Detailed case design

**Related Documents:**
- [02-GAMEPLAY.md](02-GAMEPLAY.md) - How mechanics create gameplay
- [07-USER-INTERFACE.md](07-USER-INTERFACE.md) - How mechanics are presented
- [09-DATA-SCHEMA.md](09-DATA-SCHEMA.md) - Mechanical data structures

---

**Revision History:**

| Date | Version | Changes | Author |
|------|---------|---------|--------|
| 2025-11-13 | 1.0 | Initial complete draft | AI Assistant |

