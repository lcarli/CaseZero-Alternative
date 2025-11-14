# Appendix C: Design Decisions Log

## Overview

This document records all major design decisions made during the development of CaseZero v3.0, including the reasoning behind each choice, alternatives considered, and the context that informed the decision. This log serves as institutional knowledge for the team and helps explain "why we did it this way" for future developers, designers, and stakeholders.

---

## Decision Log Format

Each decision entry follows this structure:
- **Decision ID**: Unique identifier (DEC-YYYY-NNN)
- **Date**: When the decision was made
- **Category**: Area of the game affected (Gameplay, Technical, Content, UI/UX, Business)
- **Status**: Approved, Implemented, Reconsidering, Deprecated
- **Decision**: What was decided
- **Context**: The situation and constraints that led to this decision
- **Rationale**: Why this decision was made
- **Alternatives Considered**: Other options that were discussed
- **Trade-offs**: Known disadvantages or limitations of this choice
- **Impact**: What parts of the game this affects
- **Related Decisions**: Links to other relevant decisions

---

## 1. Core Gameplay Decisions

### DEC-2024-001: No Interrogation or Dialogue Trees

**Date**: November 2024  
**Category**: Gameplay  
**Status**: Approved  
**Decision**: CaseZero will NOT include interrogation mechanics, dialogue trees, or real-time interactions with NPCs.

**Context**: Early prototypes (v1.0, v2.0) included interrogation mechanics where players could question suspects. User feedback indicated these felt "gamey" and detracted from the realistic document-analysis experience.

**Rationale**:
- Interrogations require complex dialogue writing and branching logic, increasing content production time by 40-60%
- Players expressed more enjoyment from reading written statements and documents than navigating dialogue menus
- Authentic detective work relies heavily on document analysis, not real-time questioning
- Removing interrogations allows focus on core strength: document investigation

**Alternatives Considered**:
1. **Simple dialogue trees**: 5-10 questions per suspect. Rejected due to increased scope and "game-like" feel.
2. **Pre-recorded video interrogations**: Similar to Her Story. Rejected due to high production costs and casting challenges.
3. **Text-based interview transcripts**: Considered viable but ultimately merged into document system (witness statements, interview transcripts are documents).

**Trade-offs**:
- Less "action" or player agency in investigation
- Some players may expect traditional adventure game mechanics
- Limits certain storytelling techniques (character reveals, dynamic reactions)

**Impact**: 
- Chapter 03 (Mechanics) - No interrogation system
- Chapter 04 (Case Structure) - Interview transcripts are static documents
- Chapter 10 (Content Pipeline) - No voice actor casting, no dialogue recording
- Chapter 12 (Roadmap) - Interrogation moved to Year 2+ "Advanced Mechanics" (optional)

**Related Decisions**: DEC-2024-002, DEC-2024-003

---

### DEC-2024-002: Real-Time Forensics (Not Instant)

**Date**: November 2024  
**Category**: Gameplay  
**Status**: Approved  
**Decision**: Forensic analysis requests will take real-world time to complete (2-24 hours) rather than being instant.

**Context**: Balancing realism with player engagement. Need to differentiate evidence examination (instant) from specialized forensic analysis (requires lab time).

**Rationale**:
- Enhances realism: Real forensic labs take days/weeks for results
- Creates strategic decision-making: Players must prioritize which forensics to request first
- Encourages parallel investigation: Players can examine other evidence while waiting
- Reduces impulse requesting: Players must think carefully before submitting forensic requests
- Supports live-service model: Players return to the game to check results, increasing engagement

**Alternatives Considered**:
1. **Instant results**: Rejected as too "game-like" and reduces strategic depth.
2. **In-game time acceleration**: Rejected as it complicates session tracking and feels artificial.
3. **Skippable timers with cost**: Rejected to avoid pay-to-win perception.
4. **No forensics at all**: Rejected as forensics are core to modern detective work.

**Trade-offs**:
- Players cannot complete cases in single session
- Risk of player drop-off during wait times
- Requires notification system for completion alerts
- Backend complexity (Azure Functions timer triggers)

**Impact**:
- Chapter 03 (Mechanics) - Forensics Lab system with countdown timers
- Chapter 04 (Case Structure) - Forensic analysis types and base durations defined
- Chapter 07 (UI) - Pending requests widget, notification system
- Chapter 08 (Technical) - Azure Functions background worker implementation
- Chapter 11 (Testing) - E2E tests with accelerated time for validation

**Related Decisions**: DEC-2024-003, DEC-2024-011

---

### DEC-2024-003: Limited Submission Attempts (3 Maximum)

**Date**: November 2024  
**Category**: Gameplay  
**Status**: Approved  
**Decision**: Players have a maximum of 3 submission attempts per case, with decreasing XP rewards for each attempt.

**Context**: Need to create meaningful stakes without frustrating players or requiring case restart from scratch.

**Rationale**:
- Creates tension and encourages thorough investigation before submitting
- Prevents random guessing or brute-force approaches
- Rewards careful analysis and note-taking
- Allows players to learn from feedback without permanent failure
- Aligns with detective reality: you can't accuse the wrong person infinite times

**Alternatives Considered**:
1. **Single attempt only**: Rejected as too punishing, would frustrate players.
2. **Unlimited attempts**: Rejected as it removes stakes and encourages guessing.
3. **Time-based cooldown** (wait 24h between attempts): Rejected as annoying without adding value.
4. **Hints system with penalty**: Considered but moved to Year 2 roadmap; would undermine "no hand-holding" pillar.

**Trade-offs**:
- Players who struggle may feel blocked from progressing
- Requires clear feedback system to help players understand mistakes
- May need difficulty balancing to ensure 3 attempts is fair across all cases

**Impact**:
- Chapter 03 (Mechanics) - Submission system with attempt tracking
- Chapter 06 (Progression) - XP penalty calculations (1st: 100%, 2nd: 70%, 3rd: 50%)
- Chapter 07 (UI) - Attempt counter display, feedback messaging
- Chapter 09 (Data Schema) - CaseSubmission entity tracks attempt number
- Chapter 11 (Testing) - Test cases for attempt limits and XP calculations

**Related Decisions**: DEC-2024-004, DEC-2024-013

---

### DEC-2024-004: Explain Your Answer (Written Submission)

**Date**: November 2024  
**Category**: Gameplay  
**Status**: Approved  
**Decision**: Players must write explanations for motive (100-500 words) and method (50-300 words), not just select multiple-choice answers.

**Context**: Need to validate player understanding and prevent lucky guessing. Inspired by Papers, Please's attention-to-detail philosophy.

**Rationale**:
- Forces players to demonstrate comprehension, not just pattern matching
- Reveals player reasoning, enabling better feedback
- Increases investment in solution (players care more about answers they wrote)
- Discourages external guides/walkthroughs (harder to copy-paste than select answers)
- Validates that player truly understands the case, not just memorized clues

**Alternatives Considered**:
1. **Multiple choice only**: Rejected as too easy to guess, doesn't validate understanding.
2. **No written explanation**: Rejected for same reasons.
3. **AI-evaluated responses**: Considered for future (Year 2+), but requires NLP complexity and potential false positives/negatives.
4. **Peer review system**: Interesting for community features but not for core gameplay.

**Trade-offs**:
- Requires manual evaluation or sophisticated AI (initially manual keyword matching)
- May deter players who dislike writing
- Accessibility challenge for non-native English speakers (mitigated by localization)
- Validation complexity (need to accept various phrasings of correct answer)

**Impact**:
- Chapter 03 (Mechanics) - Submit Solution multi-page form design
- Chapter 04 (Case Structure) - Solution includes reference motive/method text for validation
- Chapter 07 (UI) - Text area inputs with word counters, validation feedback
- Chapter 09 (Data Schema) - Submission DTO includes motive/method strings
- Chapter 11 (Testing) - Validation logic tests for keyword matching, length requirements

**Related Decisions**: DEC-2024-003, DEC-2024-005

---

### DEC-2024-005: No Time Pressure or Timers (Except Forensics)

**Date**: November 2024  
**Category**: Gameplay  
**Status**: Approved  
**Decision**: Players can investigate cases at their own pace with no countdown timers, score multipliers for speed, or time-based penalties (except for real-time forensic completion).

**Context**: Defining what "patience" pillar means in practice. Balancing against modern game trends that reward speed.

**Rationale**:
- Aligns with "Patience" core pillar: investigation should be thoughtful, not rushed
- Reduces anxiety and makes game accessible to players who need breaks
- Encourages thorough reading and note-taking rather than skimming
- Differentiates from action/arcade detective games (L.A. Noire quick-time events)
- Real detectives don't solve cases on a timer

**Alternatives Considered**:
1. **Optional time trial mode**: Considered for Year 2+ as separate leaderboard challenge.
2. **Speed bonus XP**: Rejected as it contradicts "patience" pillar and encourages skimming.
3. **Case "goes cold" after X days**: Rejected as artificial pressure that frustrates rather than motivates.

**Trade-offs**:
- Less urgency may reduce engagement for some players
- No leaderboards or competitive elements at launch
- May feel "slower" compared to modern games

**Impact**:
- Chapter 01 (Concept) - "Patience" pillar explicitly defined
- Chapter 03 (Mechanics) - No countdown timers in investigation phase
- Chapter 06 (Progression) - XP calculation does NOT include time factor (except optional future "Efficiency Bonus")
- Chapter 07 (UI) - No timer UI elements except forensics countdown
- Chapter 12 (Roadmap) - Time trial mode moved to Year 2+ optional features

**Related Decisions**: DEC-2024-002, DEC-2024-006

---

## 2. Technical Architecture Decisions

### DEC-2024-006: Web-First Platform (Not Native Desktop/Mobile)

**Date**: November 2024  
**Category**: Technical  
**Status**: Approved  
**Decision**: CaseZero will be developed as a web application (React + ASP.NET Core) targeting desktop/tablet browsers, not as a native desktop or mobile app.

**Context**: Choosing primary platform for MVP launch. Must consider development resources, deployment complexity, and target audience reach.

**Rationale**:
- Cross-platform by default (Windows, macOS, Linux) without separate builds
- Faster iteration and deployment (no app store approval delays)
- Lower development cost (single codebase)
- Easier updates and bug fixes (server-side deployment)
- Web technologies (React, TypeScript) align with team expertise
- PWA capabilities enable offline support and "app-like" experience

**Alternatives Considered**:
1. **Electron desktop app**: Rejected due to large bundle size (100+ MB) and resource usage.
2. **Native mobile apps** (Swift/Kotlin): Rejected due to small screen constraints and document reading difficulty.
3. **Unity/Unreal Engine**: Rejected as overkill for document-based game, adds complexity.
4. **React Native**: Considered for future mobile port, not for MVP.

**Trade-offs**:
- Browser limitations (file system access, offline capabilities)
- Requires internet connection (mitigated by PWA Service Worker)
- Less "premium" feel than native app
- Performance constraints compared to native

**Impact**:
- Chapter 08 (Technical) - React + TypeScript frontend, ASP.NET Core backend
- Chapter 07 (UI) - Desktop OS metaphor optimized for web viewport
- Chapter 11 (Testing) - Browser compatibility testing (Chrome, Firefox, Safari, Edge)
- Chapter 12 (Roadmap) - Mobile optimization moved to Year 2 Q3

**Related Decisions**: DEC-2024-007, DEC-2024-008

---

### DEC-2024-007: PostgreSQL with JSONB (Not NoSQL)

**Date**: November 2024  
**Category**: Technical  
**Status**: Approved  
**Decision**: Use PostgreSQL with JSONB column for case.json storage, not a pure NoSQL database (MongoDB, CosmosDB).

**Context**: Need to store structured relational data (users, sessions) AND flexible case.json documents efficiently.

**Rationale**:
- PostgreSQL JSONB provides best of both worlds: relational integrity + flexible JSON storage
- Strong ACID guarantees for user data, sessions, submissions
- Efficient JSONB indexing and querying (GIN indexes)
- Lower cost than CosmosDB at small-medium scale
- Team familiarity with SQL
- Avoids multi-database complexity (RDBMS + NoSQL)

**Alternatives Considered**:
1. **MongoDB**: Rejected due to lack of relational constraints for user/session data, weaker consistency.
2. **Azure CosmosDB**: Rejected due to cost ($$$) and overkill for MVP scale.
3. **SQL Server with JSON**: Considered, but PostgreSQL has superior JSONB performance and is open-source.
4. **Separate SQL + Blob Storage**: Rejected as it complicates queries requiring case.json data filtering.

**Trade-offs**:
- JSONB less flexible than pure document DB for schema evolution
- Requires Azure Database for PostgreSQL (cost, not free tier like CosmosDB free tier)
- Team must learn PostgreSQL-specific JSONB features

**Impact**:
- Chapter 08 (Technical) - PostgreSQL 15+ with JSONB columns
- Chapter 09 (Data Schema) - Hybrid model: relational tables + JSONB case_data column
- Chapter 11 (Testing) - SQL integration tests, JSONB query validation
- Infrastructure - Azure Database for PostgreSQL (Basic/General Purpose tier)

**Related Decisions**: DEC-2024-008, DEC-2024-009

---

### DEC-2024-008: Azure Functions for Forensics (Not WebJobs/Background Service)

**Date**: November 2024  
**Category**: Technical  
**Status**: Approved  
**Decision**: Use Azure Functions with Timer Triggers to process forensic request completions, rather than WebJobs or in-process background services.

**Context**: Need a reliable, scalable way to check for completed forensic requests every 5 minutes and generate reports.

**Rationale**:
- Serverless: No need to maintain always-running background process
- Automatic scaling: Azure manages compute resources
- Cost-effective: Pay only for execution time (every 5 min = ~8,640 invocations/month = minimal cost)
- Decoupled: Forensics worker runs independently from API, improving reliability
- Azure-native: Seamless integration with Azure App Service, Blob Storage, Application Insights

**Alternatives Considered**:
1. **ASP.NET Core HostedService**: Rejected because it runs in web app process, complicating scaling and restart scenarios.
2. **Azure WebJobs**: Viable alternative but Functions are more modern, better tooling, easier local development.
3. **Azure Logic Apps**: Rejected as overkill for simple timer logic, less flexible for custom code.
4. **Client-side polling**: Rejected due to inefficiency (unnecessary API calls) and unreliability (requires player to be online).

**Trade-offs**:
- Cold start delays (mitigated by running every 5 min)
- Separate deployment artifact
- Debugging complexity (distributed system)

**Impact**:
- Chapter 08 (Technical) - Azure Functions architecture with Timer Trigger
- Chapter 08 (Technical) - Forensic completion workflow diagram
- Chapter 11 (Testing) - Azure Functions integration tests, timer simulation
- Infrastructure - Azure Functions Consumption Plan

**Related Decisions**: DEC-2024-002, DEC-2024-009

---

### DEC-2024-009: Azure Blob Storage for Assets (Not CDN-Only or Database)

**Date**: November 2024  
**Category**: Technical  
**Status**: Approved  
**Decision**: Store all case assets (PDFs, images, forensic reports) in Azure Blob Storage with Azure CDN in front, not directly in database or CDN-only solutions.

**Context**: Need scalable, cost-effective storage for binary files (documents, photos) that supports global distribution and offline caching.

**Rationale**:
- Blob Storage designed for binary files, extremely cost-effective ($0.018/GB/month Hot tier)
- Decouples storage from compute (API doesn't serve files directly)
- Azure CDN provides global edge caching for low-latency access
- Supports tiered storage (Hot for active cases, Cool for archive, saving $$$)
- SAS tokens enable secure, time-limited access without exposing storage keys

**Alternatives Considered**:
1. **Store in database as BLOB/BYTEA**: Rejected due to database bloat, slow queries, expensive backups.
2. **Serve directly from API**: Rejected due to bandwidth costs, API resource contention.
3. **Third-party CDN** (Cloudflare, Fastly): Considered but Azure CDN integrates seamlessly with Blob Storage.
4. **GitHub Releases for static assets**: Rejected as not designed for dynamic content, no access control.

**Trade-offs**:
- Additional service to manage
- SAS token generation adds API complexity
- Cold storage retrieval latency (mitigated by keeping active cases in Hot tier)

**Impact**:
- Chapter 08 (Technical) - Blob Storage architecture, CDN integration, SAS token generation
- Chapter 09 (Data Schema) - Document/Evidence entities store blob URLs, not binary data
- Chapter 10 (Content Pipeline) - Asset upload to Blob Storage in publishing workflow
- Infrastructure - Azure Blob Storage (Hot/Cool), Azure CDN Standard

**Related Decisions**: DEC-2024-007, DEC-2024-010

---

### DEC-2024-010: JWT Authentication (Not Session Cookies)

**Date**: November 2024  
**Category**: Technical  
**Status**: Approved  
**Decision**: Use JWT (JSON Web Tokens) for authentication with refresh tokens, not traditional session cookies.

**Context**: Need stateless authentication for scalable API that works cross-domain (PWA, future mobile apps).

**Rationale**:
- Stateless: No server-side session storage, enables horizontal scaling
- Cross-domain support: Can authenticate across subdomains, future mobile apps
- Self-contained: Token includes user claims (ID, username, role), reducing database lookups
- Industry standard: Well-understood security model, abundant libraries
- Works with PWA: LocalStorage/SessionStorage persistence for offline

**Alternatives Considered**:
1. **Session cookies with Redis**: Rejected due to added complexity (Redis instance), cost, not truly stateless.
2. **OAuth2 with Azure AD B2C**: Considered for enterprise, but overkill for MVP, adds user friction.
3. **API keys**: Rejected as less secure (no expiration), harder to rotate.

**Trade-offs**:
- Cannot revoke tokens before expiration (mitigated by short-lived access tokens + refresh tokens)
- Token size larger than session ID (mitigated by gzip)
- Requires careful XSS prevention (HTTPOnly refresh token, sanitize all inputs)

**Impact**:
- Chapter 08 (Technical) - JWT authentication flow, token structure, refresh token rotation
- Chapter 08 (Technical) - Security considerations (token storage, XSS prevention)
- Chapter 09 (Data Schema) - UserEntity includes refresh tokens, token expiration
- Chapter 11 (Testing) - Authentication tests (login, token refresh, expiration)

**Related Decisions**: DEC-2024-011

---

### DEC-2024-011: React + Redux Toolkit (Not Context API or Zustand)

**Date**: November 2024  
**Category**: Technical  
**Status**: Approved  
**Decision**: Use Redux Toolkit for frontend state management, not React Context API or alternative libraries (Zustand, Jotai, Recoil).

**Context**: Need robust state management for complex application state (cases, documents, evidence, forensics, notes, UI).

**Rationale**:
- Redux Toolkit simplifies Redux boilerplate (createSlice, RTK Query)
- Time-travel debugging: Redux DevTools invaluable for complex state
- Persistence: redux-persist for offline case data caching
- Middleware: Easy to add logging, analytics, error tracking
- Team familiarity: Redux is industry standard with extensive documentation
- RTK Query: Built-in API caching, loading states, optimistic updates

**Alternatives Considered**:
1. **React Context API**: Rejected due to re-render issues with deeply nested state, no DevTools, no middleware.
2. **Zustand**: Lightweight and appealing, but less tooling, smaller community, team less familiar.
3. **Jotai/Recoil**: Interesting atom-based approach, but experimental, less mature ecosystem.
4. **MobX**: Rejected due to learning curve, less explicit state updates (harder to debug).

**Trade-offs**:
- Redux has more boilerplate than simpler solutions (mitigated by RTK)
- Larger bundle size (~40KB) vs. Context API (0KB) or Zustand (~3KB)
- Learning curve for junior developers

**Impact**:
- Chapter 08 (Technical) - Redux Toolkit architecture, slice structure, RTK Query API integration
- Chapter 08 (Technical) - State persistence strategy (redux-persist)
- Chapter 11 (Testing) - Redux slice unit tests, RTK Query mocking

**Related Decisions**: DEC-2024-006, DEC-2024-012

---

## 3. Content & Design Decisions

### DEC-2024-012: 2-8 Suspects Per Case (Not Fixed)

**Date**: November 2024  
**Category**: Content  
**Status**: Approved  
**Decision**: Cases must include 2-8 suspects with variable counts based on difficulty, not a fixed number.

**Context**: Balancing complexity/realism with player cognitive load. Real cases have varying suspect counts.

**Rationale**:
- Easy cases: 2-3 suspects (clear differentiation, straightforward)
- Medium cases: 4-5 suspects (more complexity, multiple viable theories)
- Hard cases: 6-7 suspects (high complexity, many red herrings)
- Expert cases: 7-8 suspects (maximum complexity, intricate relationships)
- Variable counts feel more realistic (not every case has exactly 5 suspects)
- Allows content flexibility for writers

**Alternatives Considered**:
1. **Fixed 5 suspects per case**: Rejected as too formulaic, limits creativity.
2. **No minimum/maximum**: Rejected as 1 suspect trivializes investigation, 10+ overwhelms players.
3. **Unlockable suspects**: Rejected as it contradicts "all information available upfront" principle.

**Trade-offs**:
- Variable UI layouts (suspect gallery must handle 2-8 flexibly)
- Difficulty calibration complexity (must balance across different counts)

**Impact**:
- Chapter 04 (Case Structure) - Suspect count guidelines by difficulty
- Chapter 09 (Data Schema) - Validation rules: 2 ≤ suspects.length ≤ 8
- Chapter 10 (Content Pipeline) - Writer guidelines for suspect count selection
- Chapter 11 (Testing) - Validation tests for min/max suspect counts

**Related Decisions**: DEC-2024-013, DEC-2024-014

---

### DEC-2024-013: Four Difficulty Tiers (Easy, Medium, Hard, Expert)

**Date**: November 2024  
**Category**: Content  
**Status**: Approved  
**Decision**: Implement four difficulty tiers, not three or five.

**Context**: Need clear difficulty progression that accommodates casual players and hardcore enthusiasts without too many or too few options.

**Rationale**:
- **Easy**: Onboarding tier, 2-3 suspects, clear clues (50-60% success rate target)
- **Medium**: Standard tier, 4-5 suspects, moderate complexity (30-40% success rate)
- **Hard**: Challenge tier, 6-7 suspects, red herrings (15-25% success rate)
- **Expert**: Prestige tier, 7-8 suspects, subtle clues (5-15% success rate)
- Four tiers feel natural (beginner, intermediate, advanced, expert)
- Matches common skill-based game progressions (League of Legends, fighting games)

**Alternatives Considered**:
1. **Three tiers** (Easy, Medium, Hard): Rejected as insufficient granularity, no "extreme challenge" tier for experts.
2. **Five tiers** (Tutorial, Easy, Medium, Hard, Expert): Rejected as tutorial is separate onboarding, not difficulty.
3. **Dynamic difficulty adjustment**: Rejected as it conflicts with "authentic case" philosophy (cases don't change based on player skill).

**Trade-offs**:
- Must produce content for all four tiers (production burden)
- Easy tier might feel too simple for experienced players (but necessary for onboarding)
- Expert tier might be too hard for most players (acceptable for niche appeal)

**Impact**:
- Chapter 04 (Case Structure) - Difficulty tier definitions, calibration guidelines
- Chapter 06 (Progression) - XP scaling by difficulty (Easy 150, Medium 300, Hard 600, Expert 1200)
- Chapter 10 (Content Pipeline) - Difficulty calibration checklist, blind playthrough testing
- Chapter 12 (Roadmap) - MVP includes at least 2 cases per tier except Expert (1 case)

**Related Decisions**: DEC-2024-012, DEC-2024-015

---

### DEC-2024-014: case.json as Single Source of Truth

**Date**: November 2024  
**Category**: Content  
**Status**: Approved  
**Decision**: All case content (metadata, victim, crime, suspects, evidence, documents, forensics, timeline, solution) defined in single case.json file, not split across multiple files or database tables.

**Context**: Need clear content structure for writers, designers, and developers. Must support version control, validation, and asset references.

**Rationale**:
- Single file easier to version control (Git diffs, branching, merging)
- Writers can see complete case structure in one place
- JSON Schema validation ensures consistency
- Assets referenced by relative paths, not embedded (keeps JSON manageable size)
- Backend can load entire case in single database query (JSONB column)
- Supports localization (separate case-fr.json, case-es.json)

**Alternatives Considered**:
1. **Separate JSON files per section** (victim.json, suspects.json, etc.): Rejected as fragmented, harder to validate cross-references.
2. **Direct database entry** (no JSON file): Rejected as it couples content to database, no version control, harder to review.
3. **YAML instead of JSON**: Rejected as JSON has better tooling, validation, and JavaScript parsing.
4. **XML**: Rejected as too verbose, less readable.

**Trade-offs**:
- Large case.json files (500-2000 lines) can be unwieldy to edit (mitigated by editor folding, schema hints)
- Must carefully design schema upfront (harder to change later)
- Merge conflicts possible (mitigated by clear section structure)

**Impact**:
- Chapter 04 (Case Structure) - Complete case.json schema documentation
- Chapter 09 (Data Schema) - case.json structure, validation rules, JSON Schema definition
- Chapter 10 (Content Pipeline) - case.json assembly phase, case-validator.js tool
- Chapter 11 (Testing) - case.json validation tests, schema compliance checks

**Related Decisions**: DEC-2024-009, DEC-2024-015

---

### DEC-2024-015: MVP Launch with 9 Cases (Not 5 or 15)

**Date**: November 2024  
**Category**: Business  
**Status**: Approved  
**Decision**: Launch MVP with 9 cases distributed across difficulty tiers: 3 Easy, 3 Medium, 2 Hard, 1 Expert.

**Context**: Balancing content volume for compelling launch against development timeline and budget constraints.

**Rationale**:
- **9 cases = 20-45 hours of content** for average player (2-5 hours per case)
- Competitive with similar games (Her Story: 7 videos, Obra Dinn: 60 deaths but ~12 "cases")
- Sufficient variety to showcase different case types, motives, methods
- Supports progression system (players unlock ranks through 9 cases)
- Production feasible within 12-month timeline (~1 case/month during Phase 3-4)
- Tier distribution ensures onboarding (3 Easy) and aspirational content (1 Expert)

**Alternatives Considered**:
1. **5 cases**: Rejected as insufficient content, poor value perception ($19.99 for 10-15 hours).
2. **15 cases**: Rejected as unrealistic timeline (would delay MVP by 6+ months), budget constraints.
3. **Episodes** (3 cases per episode): Rejected as it splits player base, complicates pricing.

**Trade-offs**:
- 9 cases may feel "short" to hardcore players (mitigated by post-launch content pipeline)
- Uneven tier distribution (3-3-2-1) is asymmetric but necessary for launch timeline

**Impact**:
- Chapter 10 (Content Pipeline) - Production budget ~$50k-110k for MVP cases
- Chapter 12 (Roadmap) - MVP delivers 9 cases by Month 12
- Chapter 12 (Roadmap) - Post-launch: 1-2 new cases/month (Year 1 target: 19 total cases)
- Business - Premium pricing ($19.99-$29.99) justified by 20-45 hours content + post-launch updates

**Related Decisions**: DEC-2024-013, DEC-2024-016

---

## 4. UI/UX Decisions

### DEC-2024-016: Desktop OS Metaphor (Not Traditional Game UI)

**Date**: November 2024  
**Category**: UI/UX  
**Status**: Approved  
**Decision**: Design UI as a desktop operating system metaphor with windowed applications (Case Files, Forensics Lab, Email), not a traditional game menu/HUD interface.

**Context**: Establishing UI identity that reinforces realism and "you are a detective at your desk" fantasy.

**Rationale**:
- Metaphor is immediately familiar to all users (everyone uses computers)
- Supports multi-tasking workflow (view document + take notes simultaneously)
- Windows can be resized, moved, minimized = player agency
- Reinforces "authenticity" pillar (detectives use computers)
- Differentiates from traditional game UIs
- Enables future expansion (new "apps" for features)

**Alternatives Considered**:
1. **Traditional game UI** (fixed panels, HUD overlays): Rejected as too "game-like", limits flexibility.
2. **Physical desk metaphor** (Papers, Please): Rejected as too literal, limits digital affordances (ctrl+F, zooming).
3. **Dashboard/Kanban board**: Rejected as less intuitive than familiar desktop OS.

**Trade-offs**:
- Desktop metaphor on mobile is challenging (moved to Year 2)
- Window management complexity (overlapping, z-index, focus)
- Must implement window chrome (title bar, minimize/maximize/close buttons)

**Impact**:
- Chapter 07 (UI) - Complete desktop OS design system (window manager, dock, applications)
- Chapter 08 (Technical) - Frontend window management state (Redux ui slice)
- Chapter 11 (Testing) - UI interaction tests (window drag, resize, focus)
- Future - Easy to add new "apps" (Settings, Profile, Community) as windows

**Related Decisions**: DEC-2024-017

---

### DEC-2024-017: PDF.js for Document Viewing (Not Custom Renderer)

**Date**: November 2024  
**Category**: UI/UX, Technical  
**Status**: Approved  
**Decision**: Use Mozilla's PDF.js library to render PDF documents in the browser, not a custom renderer or image-based approach.

**Context**: Need reliable, performant way to display PDF evidence documents that supports zoom, search, and accessibility.

**Rationale**:
- PDF.js is industry-standard, maintained by Mozilla, battle-tested
- Supports all PDF features (fonts, images, vector graphics)
- Built-in text layer enables Ctrl+F search, screen readers
- Zoom, pan, page navigation out-of-box
- Renders client-side (no server processing)
- Open-source (MIT license), actively maintained

**Alternatives Considered**:
1. **Convert PDFs to images**: Rejected as it loses text layer (no search, poor accessibility), larger file sizes.
2. **Use iframe with browser PDF viewer**: Rejected as inconsistent cross-browser, no customization, security risks.
3. **Custom canvas renderer**: Rejected as reinventing wheel, high development cost, bugs.
4. **React-PDF wrapper**: Considered, but adds abstraction layer; decided to use PDF.js directly.

**Trade-offs**:
- PDF.js bundle size (~500KB) adds to initial load
- Rendering complex PDFs can be CPU-intensive
- Must style PDF.js viewer chrome to match game aesthetic

**Impact**:
- Chapter 07 (UI) - Document Viewer interface design (integrated with PDF.js controls)
- Chapter 08 (Technical) - PDF.js integration, text layer configuration
- Chapter 10 (Content Pipeline) - Document templates designed for PDF export
- Chapter 11 (Testing) - PDF rendering tests, text search validation

**Related Decisions**: DEC-2024-016, DEC-2024-018

---

### DEC-2024-018: Auto-Save Notes Every 30 Seconds (Not Manual Save)

**Date**: November 2024  
**Category**: UI/UX  
**Status**: Approved  
**Decision**: Automatically save player notes to database every 30 seconds, not requiring manual save button.

**Context**: Preventing data loss while minimizing server requests. Balancing user convenience with backend load.

**Rationale**:
- Prevents frustration from lost notes (browser crash, accidental close)
- 30 seconds balances freshness vs. API request volume (max 120 requests/hour per user)
- Modern users expect auto-save (Google Docs, Notion)
- Removes cognitive burden (no "did I save?" worry)
- Visual indicator ("Last saved 15 seconds ago") provides feedback

**Alternatives Considered**:
1. **Manual save button**: Rejected as old-fashioned, users forget, data loss risk.
2. **Save on every keystroke**: Rejected as excessive API calls, poor performance on slow connections.
3. **Save on blur/unmount only**: Rejected as too infrequent, data loss if browser crashes.
4. **LocalStorage only** (no server sync): Rejected as no cross-device sync, data loss if clear cache.

**Trade-offs**:
- 30-second delay means up to 30 seconds of notes could be lost (acceptable risk)
- Backend must handle concurrent note updates (optimistic concurrency control)

**Impact**:
- Chapter 03 (Mechanics) - Notes system auto-save behavior
- Chapter 07 (UI) - Notes app includes "Last saved" timestamp
- Chapter 08 (Technical) - API endpoint `/api/cases/{id}/notes` (PUT), debounce logic
- Chapter 09 (Data Schema) - CaseSession entity includes notes JSON field, last_modified timestamp
- Chapter 11 (Testing) - Auto-save tests (debounce, concurrent updates)

**Related Decisions**: DEC-2024-011

---

## 5. Business & Monetization Decisions

### DEC-2024-019: Premium Purchase Model (Not Free-to-Play)

**Date**: November 2024  
**Category**: Business  
**Status**: Approved  
**Decision**: CaseZero will use a premium purchase model ($19.99-$29.99 one-time payment) with no ads, no DLC, no microtransactions, not free-to-play with monetization.

**Context**: Defining revenue model that aligns with game philosophy and target audience.

**Rationale**:
- Aligns with target audience (adults 18+ willing to pay for quality content)
- Matches pricing of comparable games (Obra Dinn $19.99, Papers Please $9.99)
- No ads = no distractions, preserves immersion
- No DLC pressure = players get complete experience upfront
- Post-launch content updates are free = builds goodwill, community
- Simpler to implement (no in-app purchases, ad networks, payment processing complexity)
- Indie game ethos: "pay once, own forever"

**Alternatives Considered**:
1. **Free-to-play with ads**: Rejected as ads destroy immersion, feel cheap, alienate target demographic.
2. **Freemium** (3 free cases, pay for rest): Rejected as splits player base, reduces perceived value.
3. **Subscription** ($4.99/month): Rejected as revenue pressure to constantly add content, players prefer ownership.
4. **DLC case packs**: Considered for Year 2+ but risks fragmenting player base.

**Trade-offs**:
- High barrier to entry (players must commit $19.99 upfront)
- No viral free-tier to drive user acquisition
- Revenue concentrated at launch (no recurring)
- Must deliver compelling value upfront (9 cases minimum)

**Impact**:
- Chapter 12 (Roadmap) - Launch revenue model, pricing strategy
- Chapter 12 (Roadmap) - Post-launch content updates are FREE (Year 1)
- Business - Marketing emphasizes "no ads, no DLC, own forever"
- Year 2+ - Re-evaluate if expansion content (20+ cases) warrants DLC or "expansion pack"

**Related Decisions**: DEC-2024-015, DEC-2024-020

---

### DEC-2024-020: English-Only MVP, Localize Year 1

**Date**: November 2024  
**Category**: Business, Content  
**Status**: Approved  
**Decision**: Launch MVP in English only, add French, Spanish, Portuguese, German localizations in Year 1 (Months 7-12).

**Context**: Balancing international reach with MVP timeline and budget. Localization is expensive and time-consuming.

**Rationale**:
- English-first validates product-market fit before investing in localization
- Target audience (US, UK, Canada, Australia) primarily English-speaking
- Localization cost: $10k-15k per language (translation, QA, cultural adaptation)
- Localization timeline: 2-3 months per language
- MVP timeline doesn't accommodate 4+ languages (would delay launch 3-6 months)
- Post-launch metrics inform which languages to prioritize

**Alternatives Considered**:
1. **Multi-language MVP**: Rejected as it doubles/triples timeline and budget.
2. **Machine translation (Google Translate)**: Rejected as low quality, hurts brand, confuses players.
3. **Community translation**: Considered for Year 2+ but requires tools, quality is variable.

**Trade-offs**:
- Excludes non-English speakers from MVP (acceptable for validation)
- Localized competitors may have advantage in international markets
- Case content is text-heavy (high localization cost)

**Impact**:
- Chapter 10 (Content Pipeline) - Localization strategy, timeline, budget
- Chapter 12 (Roadmap) - French Month 7-9, Spanish/Portuguese/German Months 10-12
- Business - MVP targets English-speaking markets, international expansion Year 1
- Infrastructure - i18n support built into frontend (react-i18next), but only English locale at launch

**Related Decisions**: DEC-2024-015, DEC-2024-019

---

## 6. Scope Management Decisions

### DEC-2024-021: No Multiplayer or Co-op at Launch

**Date**: November 2024  
**Category**: Scope  
**Status**: Approved  
**Decision**: CaseZero MVP will be single-player only. Multiplayer/co-op features (shared case solving, collaborative notes) are deferred to Year 3+.

**Context**: Multiplayer adds significant technical complexity (real-time sync, conflict resolution, matchmaking, cheating prevention). Must focus on core single-player experience.

**Rationale**:
- Single-player experience is core: investigation is personal, thoughtful, self-paced
- Multiplayer requires real-time infrastructure (WebSockets, SignalR), state synchronization complexity
- MVP validation doesn't require multiplayer
- Most comparable games are single-player (Papers Please, Obra Dinn, Her Story)
- Co-op is "nice to have", not essential to core pillars

**Alternatives Considered**:
1. **Asynchronous co-op** (players can share notes): Deferred to Year 2 as community features.
2. **Competitive leaderboards**: Considered for Year 1 but conflicts with "no time pressure" pillar.
3. **Spectator mode**: Interesting but very niche, deferred indefinitely.

**Trade-offs**:
- Misses social gaming trend (Among Us, Phasmophobia co-op)
- Limits viral growth (no "play with friends" hook)
- Reduces engagement for players who prefer multiplayer

**Impact**:
- Chapter 01 (Concept) - Single-player focus emphasized
- Chapter 08 (Technical) - No real-time infrastructure, no WebSockets
- Chapter 12 (Roadmap) - Co-op moved to Year 3+ vision (experimental)

**Related Decisions**: DEC-2024-022

---

### DEC-2024-022: No User-Generated Content (UGC) at Launch

**Date**: November 2024  
**Category**: Scope  
**Status**: Approved  
**Decision**: CaseZero MVP will not include case editor or user-generated content (UGC) tools. UGC is deferred to Year 2 Q4.

**Context**: Case editor is complex (GUI builder, validation, publishing workflow). Must validate core game before investing in UGC platform.

**Rationale**:
- Case creation requires significant content knowledge (murder mystery writing, evidence design)
- Editor complexity is comparable to building second application
- Quality control is challenging (must moderate/curate UGC cases)
- MVP validation doesn't require UGC
- Developer-curated cases ensure consistent quality at launch

**Alternatives Considered**:
1. **Simple JSON editor**: Still requires understanding case.json schema, not accessible to most players.
2. **Template-based case creator**: Limits creativity, produces formulaic cases.
3. **Community mod support**: Interesting but risks bypassing validation, cheating.

**Trade-offs**:
- Content production bottleneck remains with developers (1-2 cases/month ceiling)
- Misses opportunity for community engagement, infinite content
- Limits longevity (finite developer-created cases)

**Impact**:
- Chapter 12 (Roadmap) - Case editor moved to Year 2 Q4 (6+ months effort)
- Chapter 12 (Roadmap) - Year 3+ vision includes UGC platform with community voting, revenue share
- Business - Content production remains in-house for Year 1-2

**Related Decisions**: DEC-2024-015, DEC-2024-023

---

### DEC-2024-023: No Mobile App at Launch (Web PWA Only)

**Date**: November 2024  
**Category**: Scope, Technical  
**Status**: Approved  
**Decision**: CaseZero MVP will target desktop/tablet browsers as PWA (Progressive Web App), not native iOS/Android apps. Mobile optimization deferred to Year 2 Q3.

**Context**: Mobile development requires separate codebases (Swift, Kotlin) or React Native. Must validate desktop experience first.

**Rationale**:
- Document reading is better on larger screens (desktop/tablet)
- Small phone screens make PDF viewing, note-taking difficult
- Mobile app stores add friction (approval process, 30% revenue cut, update delays)
- PWA provides "install to home screen" on mobile without app store
- MVP validation targets desktop players first

**Alternatives Considered**:
1. **React Native**: Deferred to Year 2 for true native apps.
2. **Responsive design only**: Included in MVP (tablet support) but phone UX is compromised.
3. **Capacitor/Ionic**: Wrapper around web app, considered for Year 2 interim solution.

**Trade-offs**:
- Misses mobile gaming market (largest gaming segment)
- Players expect native apps (PWA "add to home screen" is less discoverable)
- App Store visibility = marketing channel we don't have

**Impact**:
- Chapter 08 (Technical) - PWA implementation (Service Worker, manifest.json, offline support)
- Chapter 12 (Roadmap) - Mobile optimization (responsive UI, touch gestures) Year 2 Q3
- Chapter 12 (Roadmap) - Native apps consideration Year 3+
- Business - Desktop/tablet market focus for launch

**Related Decisions**: DEC-2024-006, DEC-2024-016

---

## 7. Future Considerations & Revisit Decisions

### DEC-2024-024: Revisit XP Bonuses (Year 1 Metrics)

**Date**: November 2024  
**Category**: Gameplay  
**Status**: Approved - Revisit After Launch  
**Decision**: Launch with XP bonuses for first attempt (+50%), time efficiency (+10-20%), and no hints (+20%). Revisit based on Year 1 player behavior metrics.

**Context**: Balancing extrinsic rewards (XP) with intrinsic motivation (satisfaction of solving). Current bonus structure is educated guess.

**Rationale**:
- Bonuses incentivize "optimal play" without being punitive
- First attempt bonus rewards thorough investigation
- Time efficiency bonus mild enough to not contradict "no pressure" pillar
- No hints bonus reinforces self-reliance

**Revisit Criteria**:
- If >60% of players use all 3 attempts: Consider increasing first attempt bonus or improving feedback
- If <10% of players earn time efficiency bonus: Consider removing (not adding value)
- If players complain bonuses feel like pressure: Remove or make optional (leaderboard-only)

**Impact**:
- Chapter 06 (Progression) - XP bonus structure documented
- Chapter 12 (Roadmap) - Year 1 analytics tracking: attempt distribution, time spent, hint usage
- Post-Launch - Potential XP rebalance patch

**Related Decisions**: DEC-2024-003, DEC-2024-005

---

### DEC-2024-025: Sunset Plan (If Needed)

**Date**: November 2024  
**Category**: Business  
**Status**: Approved  
**Decision**: If CaseZero fails to reach sustainability (<5k users Month 6, <$50k revenue Year 1), execute graceful shutdown with 6-month notice, refunds, DRM-free release, and open-source case editor.

**Context**: Responsible planning for potential failure scenario. Player-first approach to shutdown.

**Rationale**:
- Transparency builds trust even in failure
- Refund policy shows good faith (within 1 year of purchase)
- DRM-free release enables permanent offline play
- Open-source case editor allows community continuation
- 6-month notice allows players to complete existing content

**Shutdown Steps**:
1. Public announcement (blog, email, social media)
2. Refund window opens (automated through payment processor)
3. Release final update with all cases unlocked, offline mode enabled
4. Open-source case editor on GitHub (MIT license)
5. Archive documentation, tutorials, writer guides
6. Disable authentication requirement (all cases playable offline)
7. Community handoff (Discord ownership transfer to community moderators)

**Impact**:
- Chapter 12 (Roadmap) - Sunset plan documented
- Business - Ethical exit strategy protects players
- Community - Honest communication preserves goodwill even in failure

---

## Appendix: Decision Categories

- **Gameplay**: Core mechanics, difficulty, progression, player actions
- **Technical**: Architecture, infrastructure, frameworks, libraries
- **Content**: Cases, narrative, assets, localization
- **UI/UX**: Interface design, interactions, visual style
- **Business**: Monetization, pricing, markets, release strategy
- **Scope**: Features included/excluded, timeline, resources

---

## Appendix: Decision Status

- **Approved**: Decision made, team aligned, ready to implement
- **Implemented**: Decision executed, in production/codebase
- **Reconsidering**: Decision under review due to new information
- **Deprecated**: Decision overturned, replaced by newer decision

---

## Revision History

| Version | Date | Changes | Author |
|---------|------|---------|--------|
| 1.0 | 2024-11-14 | Initial decision log with 25 key decisions | AI Assistant |

---

**Document Status:** Complete  
**Last Updated:** November 14, 2024  
**Next Review:** Monthly during development; quarterly post-launch
