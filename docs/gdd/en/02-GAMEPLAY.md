# Chapter 02 - Gameplay

**Game Design Document - CaseZero v3.0**  
**Last Updated:** November 13, 2025  
**Status:** ✅ Complete

---

## 2.1 Core Gameplay Loop

The fundamental cycle that players repeat throughout their investigation:

```
┌─────────────────────────────────────────────────────────────┐
│                    CORE GAMEPLAY LOOP                        │
└─────────────────────────────────────────────────────────────┘

    ┌──────────────┐
    │ READ BRIEFING│  ← Entry point (5-10 min)
    └──────┬───────┘
           │
           ▼
    ┌──────────────────┐
    │ EXPLORE CASE     │  ← Main investigation phase (60-80% of time)
    │ FILES            │     - Read documents
    │                  │     - Examine evidence photos
    └──────┬───────────┘     - Review suspect profiles
           │                 - Study timeline
           ▼
    ┌──────────────────┐
    │ REQUEST FORENSIC │  ← Strategic decision point (5-10 min)
    │ ANALYSES         │     - Choose which evidence to analyze
    └──────┬───────────┘     - Select analysis type
           │                 - Submit request
           ▼
    ┌──────────────────┐
    │ WAIT FOR RESULTS │  ← Real-time mechanic (hours/days)
    │                  │     - Can continue reading other materials
    └──────┬───────────┘     - Can investigate other cases
           │                 - Return when ready
           ▼
    ┌──────────────────┐
    │ ANALYZE REPORTS  │  ← Integration phase (10-20 min)
    │                  │     - Read forensic findings
    └──────┬───────────┘     - Connect to other evidence
           │                 - Update theory
           │
           ▼
    ┌──────────────────┐
    │ FORM THEORY      │  ← Synthesis (ongoing)
    │                  │     - Who did it?
    └──────┬───────────┘     - Why? (motive)
           │                 - How? (method)
           │                 - What proves it?
           ▼
    ┌──────────────────┐
    │ SUBMIT SOLUTION  │  ← Final decision (5-10 min)
    │                  │     - Select culprit
    └──────┬───────────┘     - Write explanation
           │                 - Submit (limited attempts)
           │
           ▼
    ┌──────────────────┐
    │ GET FEEDBACK     │  ← Resolution (2-5 min)
    │                  │     - Correct/Incorrect
    └──────┬───────────┘     - See actual solution
           │                 - Earn XP and rank
           │
           ▼
    [Next Case or Retry]
```

### Loop Duration
- **Single Pass:** 2-8 hours (spread across multiple sessions)
- **Typical Session:** 30-90 minutes
- **Minimum Session:** 15 minutes (read a document or two)
- **Maximum Session:** 3+ hours (deep investigation dive)

---

## 2.2 Player Verbs (Actions)

What can the player **do** in CaseZero?

### Primary Verbs (Core Gameplay)

**READ**
- Documents (police reports, statements, letters)
- Forensic reports (when available)
- Suspect profiles
- Timeline entries
- Briefing email

**EXAMINE**
- Crime scene photographs
- Evidence photos (multiple angles)
- Victim photo
- Suspect photos
- Location images

**REQUEST**
- Forensic analyses (DNA, ballistics, fingerprints, toxicology)
- Multiple requests in parallel
- View pending request status

**ANALYZE**
- Compare information across sources
- Identify contradictions
- Connect evidence to suspects
- Build timeline of events

**TAKE NOTES**
- Write observations
- Track theories
- List connections
- Create personal investigation log

**SUBMIT**
- Solution (who, why, how, proof)
- Explanation of reasoning
- Final answer (limited attempts)

### Secondary Verbs (Supporting Actions)

**NAVIGATE**
- Switch between applications
- Open/close windows
- Browse file directory
- Search through materials

**ORGANIZE**
- Arrange windows on desktop
- Bookmark important documents
- Flag key evidence

**WAIT**
- For forensic results
- Between investigation sessions

---

## 2.3 Session Flow

How a typical play session unfolds:

### First Session (Tutorial Case)
**Duration:** 20-30 minutes

```
0:00 - Tutorial screen 1: "Welcome to Cold Case Division"
0:01 - Tutorial screen 2: "Here's your desktop - Email, Case Files, Forensics Lab"
0:02 - Player opens Email app
0:03 - Reads tutorial briefing (simplified case)
0:05 - Tutorial prompt: "Open Case Files to see evidence"
0:06 - Player explores documents (2 simple documents)
0:10 - Tutorial prompt: "Submit your theory in the Submission app"
0:12 - Player submits solution
0:13 - Correct! Tutorial complete, first case unlocks
```

### Typical Early Session (First Real Case)
**Duration:** 45-60 minutes

```
0:00 - Player opens Email, reads case briefing
0:05 - Opens Case Files app, sees 15 documents available
0:07 - Starts with Police Report (most obvious choice)
0:15 - Reads through entire report, takes notes
0:20 - Opens first witness statement
0:28 - Notices contradiction with police report
0:30 - Opens second witness statement
0:35 - Opens Evidence tab, examines crime scene photos
0:40 - Opens Suspect profiles, reads about 3 suspects
0:50 - Thinks they've identified culprit but needs proof
0:52 - Opens Forensics Lab, requests DNA analysis
0:55 - Sees "Results in 24 hours"
0:57 - Makes notes about current theory
1:00 - Closes game, will return tomorrow
```

### Mid-Investigation Session
**Duration:** 30-45 minutes

```
0:00 - Player returns, checks forensics lab
0:01 - DNA results are ready! Opens report
0:05 - Reads findings, connects to suspect
0:10 - Re-reads suspect's alibi statement
0:15 - Opens timeline, checks timing
0:20 - Opens Notebook app, updates theory
0:25 - Requests ballistics analysis (wants more proof)
0:27 - Re-examines evidence photos with new context
0:35 - Confident in solution but will wait for ballistics
0:40 - Makes final notes
0:45 - Closes session
```

### Final Session (Solution Submission)
**Duration:** 30-45 minutes

```
0:00 - Player returns, ballistics results ready
0:05 - Reads report, confirms theory
0:10 - Reviews all key evidence one more time
0:20 - Opens Submission app
0:22 - Selects culprit from dropdown
0:25 - Writes detailed explanation (200-300 words)
0:35 - Proofreads explanation
0:38 - Clicks Submit (nervous!)
0:40 - Feedback screen: "Correct!" + solution walkthrough
0:42 - XP awarded, rank increases
0:45 - Returns to dashboard, sees next case unlocked
```

---

## 2.4 Difficulty Progression

How challenge scales across experience:

### Easy Cases
**Target Audience:** First-time players, casual investigators  
**Characteristics:**
- 2-3 suspects (one obvious culprit)
- 8-12 documents total
- Clear motive and opportunity
- Minimal red herrings
- Straightforward timeline
- 3-5 pieces of evidence
- 2-3 forensic analyses needed
- 2-4 hours to solve

**Example:** "Office Murder" - CFO killed, disgruntled partner is obvious suspect, DNA confirms

### Medium Cases
**Target Audience:** Experienced players (solved 2-3 cases)  
**Characteristics:**
- 4-5 suspects (multiple viable theories)
- 12-18 documents
- Multiple possible motives
- Some red herrings and misleading info
- Complex timeline with gaps
- 6-8 pieces of evidence
- 3-5 forensic analyses needed
- 4-6 hours to solve

**Example:** "Warehouse Heist" - Multiple suspects with alibis, must determine who's lying

### Hard Cases
**Target Audience:** Veterans (solved 5+ cases)  
**Characteristics:**
- 6+ suspects (everyone has secrets)
- 18-25 documents
- Hidden motives (not obvious)
- Significant red herrings
- Timeline reconstruction required
- 10-15 pieces of evidence
- 5-8 forensic analyses needed
- 6-8 hours to solve

**Example:** "Cold Case Reopened" - 10-year-old murder, conflicting witness accounts, new DNA tech reveals truth

### Expert Cases
**Target Audience:** Masters (solved 10+ cases, high success rate)  
**Characteristics:**
- 8+ suspects (multiple conspirators possible)
- 25+ documents
- Extremely subtle clues
- Deliberate misdirection
- Multiple crime scenes
- 15+ pieces of evidence
- 8-12 forensic analyses needed
- 8-12 hours to solve

**Example:** "Serial Killer Pattern" - Connecting 3 seemingly unrelated murders through forensic analysis

---

## 2.5 Win Conditions

How does a player "win" a case?

### Primary Win: Correct Solution
**Requirements:**
1. ✅ Identify correct culprit
2. ✅ Provide coherent explanation
3. ✅ Reference key evidence in explanation
4. ✅ Submit within allowed attempts (typically 3)

**Rewards:**
- Full XP for case (based on difficulty)
- Rank progress toward next tier
- Case marked as "Solved" in history
- Solution walkthrough unlocked
- Next case(s) unlocked

### Partial Success: Wrong Culprit, Good Reasoning
**When:**
- Player selects wrong suspect BUT
- Explanation demonstrates solid investigation process
- Evidence interpretation was logical (even if incorrect)

**Rewards:**
- 50% XP (acknowledging effort)
- Detailed feedback on what was missed
- Can retry with new attempt

### Learning Outcome: Multiple Failed Attempts
**When:**
- Player exhausts all attempts (usually 3)
- Cannot solve case

**Outcome:**
- No XP awarded
- Full solution revealed
- Case marked as "Unsolved - Reviewed"
- Can attempt again later (after solving other cases)
- Still contributes to player knowledge/experience

---

## 2.6 Fail States & Penalties

What CAN'T make you fail:

### No Fail States For:
- ❌ Taking too long (no time limits)
- ❌ Reading documents "out of order"
- ❌ Not requesting forensics
- ❌ Taking notes inefficiently
- ❌ Examining wrong evidence

### Soft Fail: Running Out of Attempts
**What happens:**
- After 3 incorrect submissions, case is "locked"
- Solution is revealed (can't claim XP)
- Must solve 2 other cases before retrying
- Player learns from mistakes

**Why this system:**
- Prevents random guessing
- Encourages thoughtful investigation
- Allows learning from failure
- Doesn't permanently block progress

### No Penalties For:
- ✅ Requesting forensics and not using results
- ✅ Taking long breaks between sessions
- ✅ Abandoning a case (can return anytime)
- ✅ Reading solutions to failed cases

---

## 2.7 Pacing & Rhythm

The intentional tempo of gameplay:

### Investigation Phase: **Slow & Methodical**
- Reading documents takes 5-15 minutes each
- Examining evidence is contemplative
- No rush, no timers
- Player sets their own pace

**Design Goal:** Create meditative investigation mood

### Forensics Phase: **Anticipation**
- Submit request: Quick (30 seconds)
- Wait period: Hours/days (real or accelerated)
- Results: Excitement of new information

**Design Goal:** Mimic real investigation waiting periods

### Solution Phase: **Tension**
- Writing explanation: Thoughtful (10-15 minutes)
- Submission moment: High stakes
- Feedback: Relief or disappointment

**Design Goal:** Make the answer submission feel consequential

### Session Transitions: **Natural Breakpoints**
- After reading several documents
- After submitting forensic requests
- After getting forensic results
- After major revelation

**Design Goal:** Allow guilt-free session endings

---

## 2.8 Player Motivation & Hooks

What keeps players engaged?

### Short-Term Hooks (Per Session)
**"Just one more document..."**
- Cliffhangers in documents (references to unseen evidence)
- Mysteries raised that need answers
- Contradictions that demand resolution
- Forensic results becoming available

### Medium-Term Hooks (Per Case)
**"I need to solve this..."**
- Investment in specific case
- Theory that needs validation
- Wanting to prove hypothesis correct
- Near-complete picture, missing one piece

### Long-Term Hooks (Across Cases)
**"I'm becoming a better detective..."**
- Rank progression (Rookie → Detective → Veteran → Master)
- Growing case library (solved 5, 10, 20 cases)
- Improved success rate over time
- Unlocking harder, more interesting cases

### Meta Hooks
**"This is what I wanted from true crime content..."**
- Actually *doing* investigation, not just watching
- Intellectual satisfaction
- Sharing theories with friends/community
- Status as "good detective" among peers

---

## 2.9 Replayability & Variety

How do we keep it fresh?

### Case Variety
**Crime Types:**
- Homicide (most common)
- Missing persons
- Theft/fraud
- Arson
- Assault

**Settings:**
- Urban (offices, apartments, streets)
- Suburban (homes, parks)
- Rural (farms, isolated locations)
- Public spaces (restaurants, hotels)

**Victim Profiles:**
- Business professional
- Student
- Retiree
- Public figure
- Criminal

**Eras (Future Content):**
- Modern (2020s) - current tech
- 2000s - pre-smartphone
- 1990s - limited digital
- Cold case reopened with new tech

### Evidence Variety
**Document Types:**
- Police reports (standard format)
- Witness statements (interview transcripts)
- Personal letters/emails
- Financial records
- Phone logs
- Diary entries
- Medical records
- Employment files

**Forensic Types:**
- DNA analysis
- Ballistics
- Fingerprints
- Toxicology
- Trace evidence (fiber, hair)
- Digital forensics (future)
- Handwriting analysis
- Autopsy reports

### Suspect Variety
**Archetypes (used carefully to avoid stereotypes):**
- Business rival
- Scorned lover
- Family member
- Employee/coworker
- Stranger with connection
- Wrong place/wrong time (innocent)

---

## 2.10 Social & Community Features

How players interact beyond solo play:

### Solo Experience First
**Core game is single-player:**
- No multiplayer required
- No co-op mechanics (initially)
- Self-contained case experience

### Optional Community Features

**Discussion Forums:**
- Spoiler-tagged threads per case
- Theory sharing (before solution)
- Post-solution discussion
- Case rankings/reviews

**Indirect Sharing:**
- "I solved X cases" status
- Detective rank badge
- Success rate percentage
- Favorite case lists

**NO Direct Competitive Features:**
- ❌ No leaderboards (would encourage speed over depth)
- ❌ No time-based rankings
- ❌ No versus mode
- ❌ No achievement pop-ups

**Why:** Competition undermines the contemplative investigation experience

---

## 2.11 Accessibility & Player Support

How we help without hand-holding:

### Accessibility Features

**Reading Support:**
- Adjustable text size in UI (not in PDF documents)
- High contrast mode
- Dyslexia-friendly font option
- Read-aloud support (browser native)

**Navigation Support:**
- Keyboard shortcuts for all apps
- Tab navigation through documents
- Window management hotkeys
- Bookmark/favorite system for documents

**Time Support:**
- Pause forensics timer (in settings)
- Accelerate time (2x, 5x, 10x options)
- Save progress automatically
- Pick up exactly where you left off

### Optional Aids (Non-Intrusive)

**Detective's Notebook:**
- Blank note-taking space
- Does NOT auto-fill clues
- Does NOT highlight important info
- Pure player tool

**Document Bookmarks:**
- Mark documents as "important"
- Quick navigation to marked items
- Purely organizational

**Timeline View:**
- Visual representation of events
- Drawn from documents (no hidden info)
- Helps visualization only

### What We DON'T Provide

**No Hints System:**
- ❌ No "click here for help"
- ❌ No progressive hint system
- ❌ No AI assistant suggesting next steps
- ❌ No objective markers

**No Simplification:**
- ❌ No "easy mode" that changes case
- ❌ No summary generation
- ❌ No automatic note-taking

**Philosophy:** Tools yes, shortcuts no

---

## 2.12 Tutorial & Onboarding

How new players learn to play:

### Tutorial Case: "First Day"
**Duration:** 15-20 minutes  
**Complexity:** Extremely simple  
**Structure:**

```
Scene: Training exercise for new detective

1. Welcome Screen
   - "Welcome to Cold Case Division"
   - Brief overview (2 sentences)
   - Click "Start Training"

2. Desktop Introduction
   - Shows desktop with 3 apps
   - Email icon pulses
   - Text: "Click Email to receive your first case"

3. Briefing (Simplified)
   - Short email (100 words)
   - Training case: Stolen painting
   - Only 2 documents to review

4. Case Files Introduction
   - Icon pulses
   - Opens to 2 documents
   - Text: "Read both documents"

5. Evidence Introduction
   - Shows 1 photo of evidence
   - Text: "This painting was found in suspect's home"

6. Solution
   - Opens Submission app automatically
   - Only 1 suspect option
   - Simple text box
   - Submit button

7. Completion
   - "Training Complete!"
   - Real case now unlocked
   - No more tutorials
```

### Post-Tutorial Learning

**Discovery-Based:**
- Players figure out forensics by exploring Lab app
- No forced tutorial on every feature
- Tooltips on hover (can be disabled)
- Help button leads to brief manual (optional)

**First Real Case Design:**
- Slightly harder than tutorial (but still Easy)
- Introduces forensics naturally (evidence clearly needs analysis)
- 3 suspects (tutorial had 1)
- More documents (8-10 vs. tutorial's 2)

---

## 2.13 Monetization Impact on Gameplay

How business model affects experience:

### Premium Purchase = No Compromises

**What We CAN Do:**
- ✅ Forensics takes real time (no pressure to monetize speed-ups)
- ✅ Cases are as long as needed (no padding for engagement metrics)
- ✅ Difficulty is authentic (no rubber-banding for retention)
- ✅ No daily missions or login bonuses
- ✅ No energy/stamina systems
- ✅ No ads interrupting investigation

**What Players Get:**
- Pay once, play forever
- All cases in package are included
- No hidden costs
- No psychological manipulation
- Respectful of their time

### DLC Model: More Cases

**Additional Case Packs:**
- Same quality as base game
- Clearly labeled (difficulty, theme)
- Optional purchase (base game is complete)
- No FOMO tactics
- No timed exclusives

---

## 2.14 Edge Cases & Special Situations

Handling unusual scenarios:

### Player Gives Up Early
**Scenario:** Player quits case after 10 minutes

**System Response:**
- Case saved automatically
- Remains available in dashboard
- No penalty
- Can return anytime

**Design Note:** This is okay - some cases won't resonate with every player

### Player Solves Without Forensics
**Scenario:** Player deduces correctly without requesting any analyses

**System Response:**
- ✅ Solution still accepted
- ✅ Full XP awarded
- 🎖️ Bonus: "Detective's Intuition" acknowledgment
- Note: This is rare but should be rewarded

### Player Requests All Forensics Immediately
**Scenario:** Player spam-clicks all forensic options at start

**System Response:**
- ✅ All requests accepted
- ⏱️ All timers start simultaneously
- 💰 No cost limit (they have unlimited requests)

**Design Note:** Not optimal but not punished - player will learn pacing naturally

### Player Takes Months to Finish Case
**Scenario:** Player starts case, doesn't return for 3 months

**System Response:**
- ✅ All progress saved
- ✅ Forensics completed long ago
- 📝 Optional: "Case Summary" button to refresh memory
- Can continue exactly where they left off

---

## 2.15 Success Metrics for Gameplay

How we measure if gameplay is working:

### Engagement Metrics
- **Average session length:** 30-60 minutes (indicates depth)
- **Sessions per case:** 3-5 (indicates proper pacing)
- **Completion rate:** 60%+ start-to-finish (indicates engagement)
- **Time to solution:** 3-6 hours for Easy cases (indicates appropriate difficulty)

### Quality Metrics
- **First-attempt success rate:** 30-40% (indicates challenge, not impossibility)
- **Retry rate after failure:** 70%+ try again (indicates motivation despite failure)
- **Forensics usage:** 60%+ request at least one analysis (indicates understanding mechanic)
- **Note-taking usage:** 40%+ open notebook (indicates depth of engagement)

### Red Flags
- ⚠️ Session length < 15 minutes (too shallow)
- ⚠️ 80%+ quit before finishing first case (too hard/boring)
- ⚠️ 90%+ success on first try (too easy)
- ⚠️ <10% request forensics (mechanic not understood)

---

## 2.16 Gameplay Evolution Roadmap

How gameplay might expand post-launch:

### Phase 1 (Launch): Core Loop
- Single-player investigation
- Document reading
- Forensics requests
- Solution submission

### Phase 2 (Post-Launch): Refinements
- **Timeline Builder:** Visual tool to arrange events
- **Evidence Board:** Cork board to pin connections
- **Compare Documents:** Side-by-side viewer
- **Search Function:** Keyword search across all documents

### Phase 3 (Future): New Mechanics
- **Interview Transcripts:** Read Q&A style documents
- **Security Footage:** Video clips to analyze
- **Digital Forensics:** Email/phone records with metadata
- **Re-interrogation:** New follow-up questions unlock mid-case

### Phase 4 (Long-term): Advanced Features
- **Co-op Mode:** Two detectives share case (async collaboration)
- **Custom Cases:** Community-created cases (curated)
- **Case Generator:** Procedural case creation (very long-term)

**Important:** Core loop remains unchanged. Additions are enhancements, not replacements.

---

## 2.17 Summary

**CaseZero's gameplay loop is: Read → Examine → Request → Wait → Analyze → Theorize → Submit**

**Key Gameplay Principles:**
- 📖 Reading is the primary verb
- 🕰️ Pacing is deliberately slow
- 🧠 Challenge comes from deduction, not mechanics
- ⏰ Real-time forensics creates anticipation
- 🎯 Solution submission has stakes (limited attempts)

**Player Experience:**
- Sessions are 30-90 minutes
- Cases take 3-5 sessions to complete
- Difficulty scales from Easy (2-4h) to Expert (8-12h)
- Autonomy and player-driven investigation throughout

**Success Metrics:**
- 60%+ completion rate
- 30-40% first-attempt success
- 30-60 minute average sessions
- 70%+ retry after failure

---

**Next Chapter:** [03-MECHANICS.md](03-MECHANICS.md) - Detailed system mechanics

**Related Documents:**
- [01-CONCEPT.md](01-CONCEPT.md) - Why these gameplay choices
- [04-CASE-STRUCTURE.md](04-CASE-STRUCTURE.md) - How cases are built to support gameplay
- [07-USER-INTERFACE.md](07-USER-INTERFACE.md) - How gameplay is presented

---

**Revision History:**

| Date | Version | Changes | Author |
|------|---------|---------|--------|
| 2025-11-13 | 1.0 | Initial complete draft | AI Assistant |

