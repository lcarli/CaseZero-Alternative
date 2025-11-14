# Chapter 07 - User Interface & Experience

**Game Design Document - CaseZero v3.0**  
**Last Updated:** November 13, 2025  
**Status:** ✅ Complete

---

## 7.1 Overview

This chapter defines the **visual interface, interaction patterns, and user experience** of CaseZero. The UI is built around a **Desktop OS metaphor** - players navigate a simulated detective's workstation with familiar windows, icons, and applications.

**Key Concepts:**
- Desktop OS metaphor (Windows/macOS inspired)
- Application-based organization (Email, Case Files, Forensics Lab)
- Window management system
- Minimalist, professional aesthetic
- Accessibility-first design
- No unnecessary gamification UI

---

## 7.2 Design Philosophy

### Core Principles

**1. Familiarity Through Metaphor**
- Use desktop OS conventions (windows, taskbar, icons)
- Players already understand file systems and apps
- Reduces learning curve
- Feels like "real" detective workspace

**2. Content First, UI Second**
- Interface serves the content (documents, evidence)
- No flashy animations or effects
- Clean, professional presentation
- UI should be invisible when working well

**3. Professional Aesthetic**
- Dark theme (reduces eye strain for long reading)
- High contrast for readability
- Clean typography
- No cartoonish elements

**4. Accessibility Built-In**
- Keyboard navigation throughout
- Screen reader support
- Adjustable text sizes
- High contrast mode
- No time pressure anywhere

---

## 7.3 Desktop Metaphor Structure

### Desktop Layout

```
┌──────────────────────────────────────────────────────────────┐
│ CaseZero Detective Workspace                    [_][□][X]    │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│                                                              │
│        [📧]                [📁]              [🧪]            │
│        Email              Case Files      Forensics Lab      │
│                                                              │
│                                                              │
│                         [📋]                                 │
│                    Submit Solution                           │
│                                                              │
│                                                              │
│                                                              │
├──────────────────────────────────────────────────────────────┤
│ [🏠] [📧] [📁] [🧪] [📋]                    🕐 11:47 AM     │
└──────────────────────────────────────────────────────────────┘
```

**Desktop Components:**

1. **Background:** Solid dark color (#1a1a1a), no distractions
2. **Application Icons:** 4 main apps centered on desktop
3. **Taskbar:** Bottom bar with quick access + system info
4. **Window System:** Apps open as draggable windows

---

## 7.4 Application Design

### App 1: Email (📧)

**Purpose:** Receive case briefings and updates

**Window Layout:**
```
┌────────────────────────────────────────────────┐
│ 📧 Email                          [_][□][X]    │
├────────────────────────────────────────────────┤
│ Inbox                                          │
├────────────────────────────────────────────────┤
│ ● New Case Assignment - CASE-2024-001         │
│   Detective Bureau • March 16, 2025            │
│                                                │
│   Forensic Report Ready - EV-001 Ballistics   │
│   Forensics Lab • March 17, 2025               │
│                                                │
│   Forensic Report Ready - EV-004 DNA          │
│   Forensics Lab • March 18, 2025               │
│                                                │
│                                                │
│                                                │
│                                                │
│                                                │
│                                                │
└────────────────────────────────────────────────┘
```

**Email Content View:**
```
┌────────────────────────────────────────────────┐
│ From: Detective Bureau                         │
│ To: You                                        │
│ Date: March 16, 2025, 9:00 AM                  │
│ Subject: New Case Assignment - CASE-2024-001   │
├────────────────────────────────────────────────┤
│                                                │
│ Detective,                                     │
│                                                │
│ You've been assigned to CASE-2024-001:        │
│ "The Downtown Office Murder"                   │
│                                                │
│ A business executive was found dead in his    │
│ office. Initial investigation suggests        │
│ homicide. Review the case files and report    │
│ your findings.                                 │
│                                                │
│ Case files are available in the Case Files    │
│ application.                                   │
│                                                │
│ Good luck.                                     │
│                                                │
│ - Cold Case Division                           │
│                                                │
│ [Open Case Files]                              │
└────────────────────────────────────────────────┘
```

**Features:**
- Simple inbox list
- Read case briefing
- Notification for forensic results
- Link directly to Case Files app
- No reply functionality (one-way communication)

---

### App 2: Case Files (📁)

**Purpose:** Access all case documents, evidence, and information

**Main Window:**
```
┌────────────────────────────────────────────────┐
│ 📁 Case Files - CASE-2024-001     [_][□][X]   │
├────────────────────────────────────────────────┤
│ ◀ Back to Cases                                │
├────────────────────────────────────────────────┤
│ 📂 Documents                              (12) │
│ 📂 Evidence                               (8)  │
│ 📂 Suspects                               (3)  │
│ 📂 Victim Information                     (2)  │
│ 📂 Forensic Reports                       (2)  │
│ 📂 Timeline                               (1)  │
│ 📓 My Notes                                    │
│                                                │
│                                                │
│                                                │
│                                                │
│                                                │
└────────────────────────────────────────────────┘
```

**Documents Folder:**
```
┌────────────────────────────────────────────────┐
│ 📁 Case Files > Documents          [_][□][X]  │
├────────────────────────────────────────────────┤
│ ◀ Back                                         │
├────────────────────────────────────────────────┤
│ 📄 Police Report - Incident #2023-0315        │
│    3 pages • March 16, 2025                    │
│    [View Document]                             │
│                                                │
│ 📄 Witness Statement - John Silva             │
│    2 pages • March 16, 2025                    │
│    [View Document]                             │
│                                                │
│ 📄 Suspect Interview - Michael Torres         │
│    4 pages • March 17, 2025                    │
│    [View Document]                             │
│                                                │
│ 📄 Financial Records - Torres & Chen          │
│    2 pages • March 17, 2025                    │
│    [View Document]                             │
│                                                │
└────────────────────────────────────────────────┘
```

**Document Viewer (PDF):**
```
┌────────────────────────────────────────────────┐
│ Police Report - Incident #2023-0315 [_][□][X] │
├────────────────────────────────────────────────┤
│ [<] Page 1 of 3 [>]    [⊕][⊖][⚲]     [🔍]    │
├────────────────────────────────────────────────┤
│                                                │
│  METROPOLITAN POLICE DEPARTMENT                │
│  INCIDENT REPORT                               │
│                                                │
│  Case Number: 2023-0315                        │
│  Classification: Homicide                      │
│  Date/Time: March 15, 2023, 11:30 PM (est.)   │
│  Location: 450 Market Street, Floor 15        │
│  Reporting Officer: Martinez, Sarah            │
│  Date Filed: March 16, 2023, 08:00 AM         │
│                                                │
│  SUMMARY:                                      │
│  At approximately 00:30 hours on 3/16/2023,   │
│  this officer responded to report of          │
│  deceased individual at listed location...    │
│                                                │
│                                                │
├────────────────────────────────────────────────┤
│ [📌 Bookmark] [🔍 Search] [📋 Copy Text]       │
└────────────────────────────────────────────────┘
```

**Evidence Folder:**
```
┌────────────────────────────────────────────────┐
│ 📁 Case Files > Evidence           [_][□][X]  │
├────────────────────────────────────────────────┤
│ ◀ Back                                         │
├────────────────────────────────────────────────┤
│ 🔫 EV-001: Firearm - .38 Caliber Revolver     │
│    Type: Physical - Weapon                     │
│    Collected: March 16, 2023, 02:00 AM        │
│    [View Photos] [Request Forensics]           │
│                                                │
│ 🩸 EV-004: Blood Sample - Crime Scene         │
│    Type: Biological - Blood                    │
│    Collected: March 16, 2023, 03:30 AM        │
│    [View Photos] [Request Forensics]           │
│                                                │
│ 📋 EV-007: Security Access Log                │
│    Type: Document - Records                    │
│    Collected: March 16, 2023, 10:00 AM        │
│    [View Document]                             │
│                                                │
│                                                │
└────────────────────────────────────────────────┘
```

**Evidence Photo Viewer:**
```
┌────────────────────────────────────────────────┐
│ EV-001: Firearm - .38 Caliber      [_][□][X]  │
├────────────────────────────────────────────────┤
│                                                │
│                                                │
│          [HIGH RESOLUTION PHOTO]               │
│          Weapon on evidence table              │
│          with ruler for scale                  │
│                                                │
│                                                │
├────────────────────────────────────────────────┤
│ [◀ Previous] 1 of 3 [Next ▶]     [⊕][⊖]       │
│                                                │
│ Type: Physical - Weapon                        │
│ Collected: March 16, 2023, 02:00 AM           │
│ Location: Crime scene, near victim             │
│ Collected by: CSI Team Alpha                   │
│                                                │
│ [🔬 Request Forensics]                         │
└────────────────────────────────────────────────┘
```

**My Notes:**
```
┌────────────────────────────────────────────────┐
│ 📓 My Notes                        [_][□][X]  │
├────────────────────────────────────────────────┤
│ [New Note] [Case Notes] [Suspects] [Timeline]  │
├────────────────────────────────────────────────┤
│ Case #2024-001 - Investigation Notes           │
│ ──────────────────────────────────────────     │
│                                                │
│ SUSPECTS:                                      │
│ - Michael Torres: Business partner, owes      │
│   $500k, weak alibi, access log shows he      │
│   was at building during TOD                   │
│                                                │
│ - Linda Chen: Wife, life insurance $2M,       │
│   but CCTV confirms she was home              │
│                                                │
│ - David Park: Fired employee, revenge         │
│   motive, but bar alibi confirmed by          │
│   multiple witnesses                           │
│                                                │
│ KEY EVIDENCE:                                  │
│ - Weapon (EV-001): Registered to Torres,      │
│   his fingerprints found on grip              │
│ - Blood (EV-004): DNA matches Torres          │
│ - Access log: Torres entered at 11:15 PM      │
│                                                │
│ THEORY:                                        │
│ Torres needed money, confronted victim...     │
│                                                │
│ [Auto-saved 2 minutes ago]                     │
└────────────────────────────────────────────────┘
```

---

### App 3: Forensics Lab (🧪)

**Purpose:** Request forensic analyses and view results

**Main Window:**
```
┌────────────────────────────────────────────────┐
│ 🧪 Forensics Lab                   [_][□][X]  │
├────────────────────────────────────────────────┤
│ [Available Evidence] [Pending] [Completed]     │
├────────────────────────────────────────────────┤
│                                                │
│ AVAILABLE FOR ANALYSIS:                        │
│                                                │
│ EV-001: Firearm - .38 Caliber                 │
│ ☐ Ballistics Analysis (12 hours)              │
│ ☐ Fingerprint Analysis (8 hours)              │
│ [Request Selected]                             │
│                                                │
│ EV-004: Blood Sample                           │
│ ☐ DNA Analysis (24 hours)                     │
│ [Request Selected]                             │
│                                                │
│                                                │
│                                                │
│                                                │
│                                                │
│                                                │
└────────────────────────────────────────────────┘
```

**Pending Tab:**
```
┌────────────────────────────────────────────────┐
│ 🧪 Forensics Lab > Pending         [_][□][X]  │
├────────────────────────────────────────────────┤
│ [Available Evidence] [Pending] [Completed]     │
├────────────────────────────────────────────────┤
│                                                │
│ ANALYSES IN PROGRESS:                          │
│                                                │
│ EV-001 - Ballistics Analysis                   │
│ Requested: March 17, 02:00 AM                  │
│ Status: In Progress ⏱️                         │
│ Completion: March 17, 02:00 PM                 │
│ Time Remaining: 10 hours 23 minutes            │
│ ▓▓▓▓▓▓▓░░░░░░░ 52%                            │
│                                                │
│ EV-004 - DNA Analysis                          │
│ Requested: March 17, 02:05 AM                  │
│ Status: In Progress ⏱️                         │
│ Completion: March 18, 02:05 AM                 │
│ Time Remaining: 22 hours 18 minutes            │
│ ▓▓░░░░░░░░░░░░ 14%                            │
│                                                │
└────────────────────────────────────────────────┘
```

**Completed Tab:**
```
┌────────────────────────────────────────────────┐
│ 🧪 Forensics Lab > Completed       [_][□][X]  │
├────────────────────────────────────────────────┤
│ [Available Evidence] [Pending] [Completed]     │
├────────────────────────────────────────────────┤
│                                                │
│ COMPLETED ANALYSES:                            │
│                                                │
│ ✓ EV-001 - Ballistics Analysis                │
│   Completed: March 17, 02:00 PM                │
│   [View Report]                                │
│                                                │
│ ✓ EV-004 - DNA Analysis                       │
│   Completed: March 18, 02:05 AM                │
│   [View Report]                                │
│                                                │
│                                                │
│                                                │
│                                                │
│                                                │
│                                                │
└────────────────────────────────────────────────┘
```

---

### App 4: Submit Solution (📋)

**Purpose:** Submit final case solution

**Submission Form:**
```
┌────────────────────────────────────────────────┐
│ 📋 Submit Solution - CASE-2024-001 [_][□][X]  │
├────────────────────────────────────────────────┤
│                                                │
│ WHO COMMITTED THE CRIME?                       │
│                                                │
│ [Select Suspect ▼]                             │
│ ├─ Michael Torres                              │
│ ├─ Linda Chen                                  │
│ ├─ David Park                                  │
│ └─ Other/Unknown                               │
│                                                │
│ ────────────────────────────────────────────   │
│                                                │
│ EXPLAIN THE MOTIVE:                            │
│ (Minimum 50 words)                             │
│                                                │
│ ┌────────────────────────────────────────┐    │
│ │ Torres owed victim $500,000 and was    │    │
│ │ facing buyout of his shares in company│    │
│ │ Financial desperation led him to...    │    │
│ │                                        │    │
│ │                                        │    │
│ └────────────────────────────────────────┘    │
│                                                │
│ HOW WAS IT COMMITTED?                          │
│ (Minimum 50 words)                             │
│                                                │
│ ┌────────────────────────────────────────┐    │
│ │ Torres used his building access card   │    │
│ │ to enter at 11:15 PM. He confronted    │    │
│ │ victim in office. During argument...   │    │
│ │                                        │    │
│ └────────────────────────────────────────┘    │
│                                                │
│ [▼ Continue]                                   │
└────────────────────────────────────────────────┘
```

**Evidence Selection (Page 2):**
```
┌────────────────────────────────────────────────┐
│ 📋 Submit Solution - CASE-2024-001 [_][□][X]  │
├────────────────────────────────────────────────┤
│                                                │
│ SELECT KEY EVIDENCE:                           │
│ (Check all that support your conclusion)       │
│                                                │
│ ☑ EV-001 - Firearm (.38 caliber)              │
│   Ballistics match, Torres' fingerprints      │
│                                                │
│ ☑ EV-004 - Blood Sample                       │
│   DNA matches Michael Torres                   │
│                                                │
│ ☑ EV-007 - Security Access Log                │
│   Places Torres at building during murder     │
│                                                │
│ ☐ EV-008 - Victim's Phone Records             │
│                                                │
│ ☑ DOC-009 - Financial Records                 │
│   Shows $500k debt, motive established        │
│                                                │
│ ────────────────────────────────────────────   │
│                                                │
│ Attempts Remaining: 3/3                        │
│                                                │
│ ⚠️ Warning: Incorrect submissions consume an   │
│ attempt. Review your theory carefully.         │
│                                                │
│ [◀ Back] [Cancel] [Submit Solution]            │
└────────────────────────────────────────────────┘
```

---

## 7.5 Window Management

### Window Controls

**Standard Window:**
```
┌────────────────────────────────────────────────┐
│ 📧 Email                          [_][□][X]    │
│                    ▲▲▲                         │
│  Title Bar     Drag to move                    │
└────────────────────────────────────────────────┘

Controls:
[_] = Minimize (collapse to taskbar)
[□] = Maximize (full screen)
[X] = Close window (returns to desktop)
```

**Window States:**

1. **Normal:** Floating window, draggable, resizable
2. **Maximized:** Full screen, covers desktop
3. **Minimized:** Hidden, icon in taskbar

**Multiple Windows:**
- Can open multiple apps simultaneously
- Windows stack (most recent on top)
- Click window to bring to front
- Alt+Tab to switch (keyboard shortcut)

---

## 7.6 Color Scheme & Typography

### Color Palette

**Primary Colors:**
```
Background:      #1a1a1a (Very Dark Gray)
Window:          #2a2a2a (Dark Gray)
Panel:           #333333 (Medium Dark Gray)
Border:          #444444 (Medium Gray)
Text:            #e0e0e0 (Light Gray)
Accent:          #4a9eff (Blue)
Success:         #4caf50 (Green)
Warning:         #ff9800 (Orange)
Error:           #f44336 (Red)
```

**Visual Example:**
```
┌──────────────────────────────────────────────────┐ ← #444444 border
│ #2a2a2a window background                        │
│                                                  │
│ #e0e0e0 text on dark background                 │
│                                                  │
│ [#4a9eff Button]  ← Accent color                │
│                                                  │
│ ✓ Success message (#4caf50)                     │
│ ⚠️ Warning message (#ff9800)                     │
│ ✗ Error message (#f44336)                       │
│                                                  │
└──────────────────────────────────────────────────┘
```

### Typography

**Font Family:**
- UI Text: Inter, -apple-system, system-ui (clean sans-serif)
- Document Text: Georgia, serif (readable for long-form)
- Monospace: "Courier New", Courier, monospace (for logs/data)

**Font Sizes:**
- Heading Large: 24px (bold)
- Heading Medium: 18px (bold)
- Body Text: 16px (normal)
- Small Text: 14px (normal)
- Tiny Text: 12px (metadata, timestamps)

**Readability:**
- Line height: 1.5 (150%)
- Letter spacing: 0.02em
- Max line width: 80 characters (for documents)
- High contrast (WCAG AAA compliant)

---

## 7.7 Responsive Layout

### Desktop (Primary Target)

**Minimum Resolution:** 1280x720  
**Optimal Resolution:** 1920x1080  
**Maximum Window Size:** 1600x900 (internal)

**Layout:**
- 4 app icons centered on desktop
- Windows open at 800x600 (default)
- Resizable up to 1600x900
- Taskbar 60px height

### Tablet/Mobile (Future Consideration)

**Not MVP, but design considerations:**
- Single-app view (no desktop)
- Full-screen apps
- Swipe to switch apps
- Touch-optimized controls

**Note:** Desktop experience is primary. Mobile is post-launch if viable.

---

## 7.8 Accessibility Features

### Keyboard Navigation

**Global Shortcuts:**
- `Alt+1` - Open Email
- `Alt+2` - Open Case Files
- `Alt+3` - Open Forensics Lab
- `Alt+4` - Open Submit Solution
- `Alt+Tab` - Switch between open windows
- `Escape` - Close active window
- `F11` - Toggle fullscreen

**Within Windows:**
- `Tab` - Navigate between elements
- `Enter` - Activate button/link
- `Space` - Toggle checkbox
- `Arrow Keys` - Navigate lists
- `Page Up/Down` - Scroll documents

### Screen Reader Support

**ARIA Labels:**
- All interactive elements labeled
- Document structure (headings, lists)
- Form fields with descriptions
- Status updates announced

**Example:**
```html
<button aria-label="View Police Report - Incident 2023-0315, 3 pages">
  View Document
</button>
```

### Visual Accessibility

**High Contrast Mode:**
- Increased contrast ratios
- 7:1 minimum (WCAG AAA)
- Option in settings

**Text Scaling:**
- UI text: 100%, 125%, 150%, 200%
- Document zoom: Independent control
- Preserve layout at larger sizes

**Color Blindness:**
- Don't rely on color alone
- Use icons + color
- Patterns for differentiation

---

## 7.9 User Flow Examples

### First-Time User Flow

**1. Tutorial Briefing (Email)**
```
Open Email → Read tutorial briefing → Click "Start Training"
```

**2. Training Case**
```
Open Case Files → Read 2 documents → View evidence → 
Submit solution → Success feedback
```

**3. First Real Case**
```
Email notification → Open Case Files → Explore documents →
Request forensics → Wait → Review results → Submit solution
```

### Typical Investigation Flow

**Starting New Case:**
```
Email notification → Read briefing → Open Case Files →
Read police report → Review suspects
```

**Mid-Investigation:**
```
Open Case Files → Read witness statements → View evidence →
Open Forensics Lab → Request DNA analysis → Take notes →
Close and wait
```

**Completing Case:**
```
Email: Forensics ready → Open Forensics Lab → Read report →
Update notes → Open Submit Solution → Fill form → Submit →
View results
```

---

## 7.10 Notifications & Feedback

### System Notifications

**Email Badge:**
```
[📧 ●] ← Red dot indicates unread email
```

**Toast Notification (Bottom-Right):**
```
┌────────────────────────────────────┐
│ ✓ Forensic Report Ready            │
│ EV-001 Ballistics Analysis         │
│ [View Now] [Dismiss]               │
└────────────────────────────────────┘
```

**Types:**
- New case assigned
- Forensic report ready
- Solution submitted
- Rank up achieved

### In-App Feedback

**Loading States:**
```
Submitting solution...
▓▓▓▓▓▓▓▓▓░░░ 75%
```

**Success Messages:**
```
✓ Case Solved!
You correctly identified the culprit.
```

**Error Messages:**
```
✗ Incomplete Submission
Please explain the motive (minimum 50 words)
```

---

## 7.11 Settings & Preferences

### Settings Menu

**Access:** Gear icon in taskbar

**Settings Panel:**
```
┌────────────────────────────────────────────────┐
│ ⚙️ Settings                        [_][□][X]  │
├────────────────────────────────────────────────┤
│ [Display] [Audio] [Forensics] [Accessibility]  │
├────────────────────────────────────────────────┤
│                                                │
│ DISPLAY                                        │
│                                                │
│ Theme:                                         │
│ ○ Dark (Default)                               │
│ ○ Light                                        │
│ ○ High Contrast                                │
│                                                │
│ UI Scale:                                      │
│ ◉ 100%  ○ 125%  ○ 150%  ○ 200%               │
│                                                │
│ Window Animations:                             │
│ [■] Enable (smooth open/close)                 │
│                                                │
│ ────────────────────────────────────────────   │
│                                                │
│ [Apply] [Cancel]                               │
└────────────────────────────────────────────────┘
```

**Forensics Settings:**
```
FORENSICS TIMING

Time Mode:
◉ Real-Time (Default)
  DNA: 24 hours, Ballistics: 12 hours, etc.
  Progress continues when game is closed.

○ Accelerated (1 hour = 1 minute)
  DNA: 24 minutes, Ballistics: 12 minutes
  For faster-paced gameplay.

○ Instant (Story Mode)
  All analyses complete immediately.
  Disables rank progression.
```

**Accessibility Settings:**
```
ACCESSIBILITY

Visual:
[■] High Contrast Mode
[■] Reduce Motion
[■] Screen Reader Support

Input:
[■] Keyboard Navigation Hints
[ ] Sticky Keys Support

Reading:
Font Size: [▼ 16px (Default)]
Font: [▼ Default]
Line Spacing: [▼ 1.5x]
```

---

## 7.12 Loading & Transitions

### Loading Screens

**Game Launch:**
```
┌────────────────────────────────────────────────┐
│                                                │
│                                                │
│                  CaseZero                      │
│              Cold Case Division                │
│                                                │
│             Loading workspace...               │
│             ▓▓▓▓▓▓▓░░░░░░░ 52%                │
│                                                │
│                                                │
└────────────────────────────────────────────────┘
```

**Case Loading:**
```
Loading Case Files...
▓▓▓▓▓▓▓▓▓▓▓▓ 100%

Preparing documents... ✓
Loading evidence... ✓
Checking forensics... ✓
```

### Transitions

**Window Open/Close:**
- Smooth fade + scale (200ms)
- Can disable in settings

**App Switching:**
- Instant (no animation)
- Current window moves to back

**Page Navigation:**
- Smooth scroll (100ms)
- Preserve scroll position when returning

---

## 7.13 Error States & Edge Cases

### Connection Issues

**Offline Mode:**
```
⚠️ Connection Lost

You're currently offline. Some features are unavailable:
- Forensic requests
- Solution submission
- Profile sync

You can still:
- Read case files
- Take notes
- View completed forensics

[Retry Connection] [Continue Offline]
```

### Data Errors

**Missing Case Files:**
```
✗ Error Loading Case

Case files could not be loaded. This might be due to:
- Corrupted save data
- Server issue
- Missing DLC

[Report Issue] [Return to Dashboard] [Retry]
```

### User Errors

**Incomplete Submission:**
```
⚠️ Incomplete Submission

Your solution is missing required information:
- Motive explanation is too short (32 words, need 50)
- At least one piece of evidence must be selected

[Go Back] [Cancel]
```

---

## 7.14 Dashboard & Case Selection

### Main Dashboard

**After Login:**
```
┌──────────────────────────────────────────────────┐
│ CaseZero - Cold Case Division                    │
├──────────────────────────────────────────────────┤
│                                                  │
│ Welcome back, Detective!                         │
│                                                  │
│ Rank: Lead Detective ⭐                          │
│ XP: 9,450 / 12,000  ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓░░░░ 79%    │
│                                                  │
│ ────────────────────────────────────────────────│
│                                                  │
│ ACTIVE CASES (2)                                 │
│                                                  │
│ CASE-2024-015: The Harbor Conspiracy             │
│ Difficulty: Expert • 6.2 hours • 45% complete    │
│ [Continue]                                       │
│                                                  │
│ CASE-2024-014: The Museum Theft                  │
│ Difficulty: Hard • 2.1 hours • 20% complete      │
│ [Continue]                                       │
│                                                  │
│ ────────────────────────────────────────────────│
│                                                  │
│ [Browse New Cases] [View Profile] [Settings]    │
│                                                  │
└──────────────────────────────────────────────────┘
```

### Case Browser

**Browse Available Cases:**
```
┌──────────────────────────────────────────────────┐
│ Browse Cases                                     │
├──────────────────────────────────────────────────┤
│ Filter: [All ▼] [Easy] [Medium] [Hard] [Expert] │
│ Sort: [Newest ▼]                                 │
├──────────────────────────────────────────────────┤
│                                                  │
│ CASE-2024-016: The Poisoned Chalice             │
│ Difficulty: Expert • Est. 10-12 hours            │
│ Suspects: 9 • Documents: 28 • Evidence: 14       │
│ "A wine collector dies mysteriously at a dinner  │
│ party. Was it murder or tragic accident?"        │
│ [Start Case]                                     │
│                                                  │
│ CASE-2024-015: The Harbor Conspiracy             │
│ Difficulty: Expert • Est. 8-10 hours             │
│ Suspects: 8 • Documents: 24 • Evidence: 12       │
│ [Continue] (In Progress)                         │
│                                                  │
│ CASE-2024-014: The Museum Theft                  │
│ Difficulty: Hard • Est. 6-8 hours                │
│ Suspects: 6 • Documents: 18 • Evidence: 10       │
│ [Continue] (In Progress)                         │
│                                                  │
└──────────────────────────────────────────────────┘
```

---

## 7.15 Animations & Polish

### Subtle Animations

**Enabled by Default (Can Disable):**
- Window fade in/out (200ms)
- Button hover highlight
- Smooth scrolling
- Progress bar fill animation
- Notification slide in

**No Animations:**
- Page transitions (instant)
- Loading spinners (static progress bars)
- Decorative effects
- Parallax or motion backgrounds

### Polish Details

**Micro-Interactions:**
- Button click feedback (slight scale)
- Hover states (subtle highlight)
- Active state (border change)
- Focus indicator (blue outline)

**Sound Effects (Optional, Off by Default):**
- Window open/close (soft)
- Button click (minimal)
- Notification (gentle chime)
- Case solved (satisfying tone)

**Volume Control:**
- Master: 0-100%
- UI Sounds: 0-100%
- Option to disable entirely

---

## 7.16 Performance Considerations

### Optimization Targets

**Load Times:**
- App launch: <3 seconds
- Case load: <2 seconds
- Document open: <500ms
- Evidence photo: <1 second

**Responsiveness:**
- UI interactions: <100ms
- Window drag: 60 FPS
- Scroll performance: Smooth 60 FPS
- No jank or stuttering

### Asset Loading

**Lazy Loading:**
- Documents load on open (not all at once)
- Evidence photos load on view
- PDF pages render as needed
- Background loading for next likely documents

**Caching:**
- Recently viewed documents cached
- Evidence photos cached
- Case metadata cached
- Clear cache on case switch

---

## 7.17 Platform-Specific Considerations

### Windows

**Integration:**
- Native window controls
- Taskbar integration
- Windows keyboard shortcuts
- File system access (for export)

### macOS

**Integration:**
- Native window chrome
- Dock integration
- macOS keyboard shortcuts (Cmd instead of Ctrl)
- Touch Bar support (if applicable)

### Linux

**Integration:**
- Standard window decorations
- Desktop environment integration
- Standard keyboard shortcuts

### Web (If Applicable)

**Browser Constraints:**
- Fullscreen API for immersion
- Local storage for saves
- Service worker for offline
- No native window chrome (use custom)

---

## 7.18 Summary

**UI Philosophy:**
- **Desktop OS metaphor** for familiarity
- **Content-first design** (UI serves documents)
- **Professional aesthetic** (dark theme, clean typography)
- **Accessibility built-in** (keyboard nav, screen reader, high contrast)

**Core Applications:**
1. **Email** - Case briefings and notifications
2. **Case Files** - Documents, evidence, notes, timeline
3. **Forensics Lab** - Request analyses, view results
4. **Submit Solution** - Final case submission

**Visual Design:**
- Dark theme (#1a1a1a background, #4a9eff accent)
- Inter font for UI, Georgia for documents
- High contrast (WCAG AAA)
- Minimal animations

**User Experience:**
- Window management (minimize, maximize, close)
- Keyboard shortcuts throughout
- Real-time forensics progress
- Clear feedback and notifications
- Responsive performance

---

**Next Chapter:** [08-TECHNICAL.md](08-TECHNICAL.md) - System architecture and implementation

**Related Documents:**
- [03-MECHANICS.md](03-MECHANICS.md) - Mechanical implementation of UI elements
- [09-DATA-SCHEMA.md](09-DATA-SCHEMA.md) - Data structures behind UI
- [11-TESTING.md](11-TESTING.md) - UI testing and usability

---

**Revision History:**

| Date | Version | Changes | Author |
|------|---------|---------|--------|
| 2025-11-13 | 1.0 | Initial complete draft | AI Assistant |
