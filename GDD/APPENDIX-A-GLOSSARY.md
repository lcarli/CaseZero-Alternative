# Chapter 13: Glossary

## Overview

This glossary provides comprehensive definitions for all technical terms, game-specific terminology, acronyms, and specialized vocabulary used throughout the CaseZero v3.0 Game Design Document. Terms are organized alphabetically with cross-references to relevant chapters where they are discussed in detail.

---

## A

### Achievement
A milestone or accomplishment that awards the player with recognition and potentially bonus XP. Examples include "First Case Solved," "Perfect Detective" (solved case with no hints), or "Forensics Expert" (used all forensic types). See **[Chapter 06 - Player Progression]** for achievement system details.

### API (Application Programming Interface)
A set of HTTP endpoints that allow the frontend to communicate with the backend server. CaseZero's REST API handles authentication, case data retrieval, forensic requests, and solution submission. See **[Chapter 08 - Technical Architecture]** for endpoint specifications.

### ASP.NET Core
An open-source, cross-platform web framework by Microsoft used to build the CaseZero backend API. Version 9.0 is specified for the project. See **[Chapter 08 - Technical Architecture]** for technology stack rationale.

### Authentication
The process of verifying a user's identity through username/email and password. CaseZero uses JWT (JSON Web Tokens) for stateless authentication. See **[Chapter 08 - Technical Architecture]** for authentication flow.

### Authorization
The process of determining what actions an authenticated user is permitted to perform. CaseZero uses role-based authorization (currently "Player" role, with future "Admin" and "ContentCreator" roles planned).

### Azure App Service
Microsoft's cloud platform-as-a-service (PaaS) for hosting web applications. CaseZero's backend API runs on Azure App Service Linux. See **[Chapter 08 - Technical Architecture]** for deployment strategy.

### Azure Blob Storage
Microsoft's cloud object storage service for unstructured data. CaseZero stores PDF documents, evidence photos, and forensic reports in blob storage. Uses Hot tier for active cases, Cool tier for archive. See **[Chapter 08 - Technical Architecture]**.

### Azure CDN (Content Delivery Network)
A distributed network of servers that delivers static content to users from geographically nearby locations. CaseZero uses Azure CDN to serve case assets (PDFs, images) with low latency globally. See **[Chapter 08 - Technical Architecture]**.

### Azure Functions
Microsoft's serverless compute service for running event-driven code. CaseZero uses Azure Functions with Timer Triggers to process forensic analysis completions every 5 minutes. See **[Chapter 08 - Technical Architecture]** for real-time forensics implementation.

---

## B

### Ballistics
A forensic analysis type that examines firearms, bullets, and gunshot residue to determine weapon type, trajectory, and shooter position. Available as a forensic request in the Forensics Lab. See **[Chapter 03 - Core Mechanics]** for forensics system.

### Bicep
A domain-specific language for deploying Azure resources using Infrastructure as Code (IaC). CaseZero's infrastructure templates are written in Bicep. See **[Chapter 08 - Technical Architecture]** for deployment.

### Blue-Green Deployment
A deployment strategy that maintains two identical production environments ("blue" active, "green" idle). New versions are deployed to the green environment, tested, then traffic is switched over. Enables zero-downtime deployments and quick rollback. See **[Chapter 08 - Technical Architecture]**.

---

## C

### Case
A complete murder mystery scenario that the player must solve. Each case includes victim information, crime details, suspects, evidence, documents, forensic analyses, timeline, and solution. Identified by unique case ID (e.g., CASE-2024-001). See **[Chapter 04 - Case Structure]** for complete case anatomy.

### Case Files
The in-game application/interface where players view documents and evidence. Named metaphorically as a "detective's case file folder." Includes Document Viewer (PDF.js), Evidence Gallery, and Case Files List. See **[Chapter 07 - User Interface]**.

### case.json
The master data file that defines a complete case. JSON format containing metadata, victim, crime, location, suspects, evidence, documents, forensic analyses, timeline, and solution. Stored in PostgreSQL JSONB column. See **[Chapter 09 - Data Schema & Models]** for complete schema.

### Case Session
A persistent record of a player's progress on a specific case, including session ID, case ID, user ID, start time, completion status, time spent, and submitted solution. Enables players to resume cases across devices. See **[Chapter 09 - Data Schema & Models]**.

### CDN
See **Azure CDN**.

### CI/CD (Continuous Integration / Continuous Deployment)
Automated software development practices. CI automatically tests code changes when pushed to repository. CD automatically deploys passing changes to staging/production environments. CaseZero uses GitHub Actions for CI/CD. See **[Chapter 11 - Testing Strategy]**.

### Clue
A piece of information embedded within documents or evidence that helps identify the culprit, motive, or method. Clues can be explicit (direct statement) or implicit (must be inferred). See **[Chapter 04 - Case Structure]** and **[Chapter 10 - Content Pipeline]** for clue placement matrix.

### CORS (Cross-Origin Resource Sharing)
A security mechanism that allows or restricts web applications from making requests to different domains. CaseZero's API configures CORS to only accept requests from the frontend domain. See **[Chapter 08 - Technical Architecture]** for security configuration.

### Crime Scene
The physical location where the murder occurred. Described with address, type (residential, public, commercial, outdoor), environmental conditions, and physical description. See **[Chapter 04 - Case Structure]**.

### CSP (Content Security Policy)
HTTP response headers that control which resources (scripts, styles, images) the browser is allowed to load. Prevents XSS attacks. CaseZero implements strict CSP. See **[Chapter 08 - Technical Architecture]** for security.

### Culprit
The person who committed the murder. In CaseZero, the culprit is always one of the named suspects. The player's primary objective is to identify the correct culprit. See **[Chapter 04 - Case Structure]** for solution structure.

---

## D

### DTO (Data Transfer Object)
A simple object used to transfer data between application layers, typically between API and frontend. CaseZero uses C# records for DTOs (e.g., `CaseListDto`, `SubmitSolutionDto`). See **[Chapter 09 - Data Schema & Models]**.

### DAU/MAU (Daily Active Users / Monthly Active Users)
Key engagement metrics. DAU measures unique users per day, MAU measures unique users per month. Target ratio for CaseZero is 25-30% (indicates healthy retention). See **[Chapter 12 - Product Roadmap]** for success metrics.

### Detective Rank
A progression tier based on total XP earned. Ranks include Cadet (0 XP), Junior Detective (5,000 XP), Detective (20,000 XP), Senior Detective (50,000 XP), Lead Detective (100,000 XP), Chief Inspector (200,000 XP), Legendary Detective (500,000 XP). See **[Chapter 06 - Player Progression]**.

### Difficulty
A case classification indicating complexity and challenge level. Four tiers: Easy (fewer suspects, clearer clues), Medium (moderate complexity), Hard (multiple red herrings, complex timeline), Expert (highly complex, subtle clues). See **[Chapter 04 - Case Structure]** for difficulty calibration.

### DNA Analysis
A forensic analysis type that examines biological evidence (blood, hair, saliva) to identify individuals through genetic matching. Longest forensic request (24 hours base time). See **[Chapter 03 - Core Mechanics]**.

---

## E

### E2E Testing (End-to-End Testing)
Testing methodology that validates complete user workflows from start to finish in a production-like environment. CaseZero uses Playwright for E2E testing. See **[Chapter 11 - Testing Strategy]** for test flows.

### EF Core (Entity Framework Core)
Microsoft's object-relational mapping (ORM) framework for .NET. Version 9.0 used in CaseZero to interact with PostgreSQL database. See **[Chapter 08 - Technical Architecture]**.

### Evidence
Physical or digital items related to the crime that players can examine. Types include Physical Evidence (weapon, clothing), Documents (subset of evidence that are readable), Photos, Forensic Samples (analyzed via Forensics Lab), and Digital Evidence (future expansion). See **[Chapter 04 - Case Structure]**.

### Evidence Gallery
The UI component in Case Files that displays all evidence items as a visual grid with thumbnails, names, and descriptions. Players click to enlarge images. See **[Chapter 07 - User Interface]**.

### XP (Experience Points)
Points awarded for solving cases, used to calculate Detective Rank. Base XP varies by difficulty: Easy 150, Medium 300, Hard 600, Expert 1200. Bonuses awarded for first attempt (+50%), time efficiency (+10-20%), and no hints (+20%). See **[Chapter 06 - Player Progression]**.

---

## F

### Fingerprint Analysis
A forensic analysis type that compares fingerprints found at the crime scene with suspect fingerprints to establish presence or exclusion. Medium turnaround time (6 hours base). See **[Chapter 03 - Core Mechanics]**.

### FluentValidation
A .NET library for building strongly-typed validation rules. CaseZero uses FluentValidation to validate case.json data, API request DTOs, and user submissions. See **[Chapter 09 - Data Schema & Models]**.

### Forensic Analysis
A scientific examination of evidence performed by specialists. In CaseZero, players request forensic analyses via the Forensics Lab, wait for real-time completion, then review the generated PDF report. Types: DNA, Fingerprints, Toxicology, Ballistics, Digital Forensics (future). See **[Chapter 03 - Core Mechanics]**.

### Forensic Request
A player-initiated action to analyze evidence. Creates a database record with request ID, case ID, user ID, evidence ID, analysis type, request timestamp, completion timestamp, status (Pending/Completed/Failed), and report URL. See **[Chapter 09 - Data Schema & Models]**.

### Forensics Lab
The in-game interface where players request and track forensic analyses. Displays available evidence, allows selection of analysis type, shows pending requests with countdown timers, and provides access to completed reports. See **[Chapter 07 - User Interface]**.

---

## G

### GitHub Actions
GitHub's built-in CI/CD automation platform. CaseZero uses GitHub Actions workflows to run tests, build artifacts, and deploy to Azure on code push. See **[Chapter 11 - Testing Strategy]** for CI/CD pipeline.

---

## H

### Hot Tier
Azure Blob Storage access tier optimized for frequently accessed data. CaseZero stores active case assets (currently playable cases) in Hot tier. See **[Chapter 08 - Technical Architecture]**.

### HTTP (Hypertext Transfer Protocol)
The application protocol used for communication between frontend and backend. CaseZero's API uses HTTPS (encrypted HTTP) exclusively. See **[Chapter 08 - Technical Architecture]**.

---

## I

### IaC (Infrastructure as Code)
The practice of managing cloud infrastructure through code files (Bicep, Terraform) rather than manual configuration. Enables version control, automated deployment, and reproducibility. See **[Chapter 08 - Technical Architecture]**.

### Integration Testing
Testing methodology that validates interactions between multiple system components (e.g., API + database). CaseZero uses xUnit with WebApplicationFactory for integration testing. See **[Chapter 11 - Testing Strategy]**.

---

## J

### JSON (JavaScript Object Notation)
A lightweight data-interchange format. CaseZero uses JSON for case.json files, API request/response bodies, and Redux state. See **[Chapter 09 - Data Schema & Models]**.

### JSONB
PostgreSQL's binary JSON data type that allows efficient storage and querying of JSON documents. CaseZero stores case.json in JSONB column for flexible schema and fast querying. See **[Chapter 08 - Technical Architecture]**.

### JSON Schema
A vocabulary for validating JSON document structure. CaseZero defines a JSON Schema for case.json to ensure content validity. See **[Chapter 09 - Data Schema & Models]**.

### JWT (JSON Web Token)
A compact, URL-safe token format for representing claims between two parties. CaseZero uses JWT for stateless authentication. Token contains user ID, username, email, role, and expiration. See **[Chapter 08 - Technical Architecture]**.

---

## K

### Key Evidence
The specific pieces of evidence and documents that directly prove the solution (culprit, motive, method). Players must identify key evidence as part of their submission. See **[Chapter 04 - Case Structure]** for solution requirements.

### Keyword Search
A future planned feature allowing players to search document text for specific words or phrases. Not in MVP scope. See **[Chapter 12 - Product Roadmap]** for feature backlog.

---

## L

### Load Testing
Testing methodology that evaluates system performance under expected and peak user loads. CaseZero uses Artillery to simulate concurrent users. See **[Chapter 11 - Testing Strategy]**.

### Localization (L10n)
The process of adapting software for different languages and regions. CaseZero MVP is English-only; French, Spanish, Portuguese, German planned for Year 1 expansion. See **[Chapter 10 - Content Pipeline]** for localization strategy.

---

## M

### Method
How the murder was committed (e.g., stabbing, shooting, poisoning, strangulation). Part of the crime details and player solution. See **[Chapter 04 - Case Structure]**.

### Motive
Why the culprit committed the murder (e.g., financial gain, revenge, jealousy, self-defense). Players must deduce and write the motive as part of their solution submission. See **[Chapter 04 - Case Structure]**.

### MVP (Minimum Viable Product)
The initial release version with core features necessary for launch. CaseZero MVP includes authentication, 9 cases (3 Easy, 3 Medium, 2 Hard, 1 Expert), case viewing, forensics, submission, and basic progression. See **[Chapter 12 - Product Roadmap]**.

---

## N

### NPS (Net Promoter Score)
A customer satisfaction metric measured by asking "How likely are you to recommend this product?" on a 0-10 scale. Target NPS for CaseZero is >40 by end of Year 1. See **[Chapter 12 - Product Roadmap]**.

### Notes System
The in-game tool where players write freeform notes, observations, and theories. Auto-saves to database every 30 seconds. Supports rich text formatting (bold, italic, bullet lists). See **[Chapter 03 - Core Mechanics]** and **[Chapter 07 - User Interface]**.

---

## O

### ORM (Object-Relational Mapping)
A programming technique that converts data between incompatible type systems (e.g., C# objects ↔ database tables). CaseZero uses Entity Framework Core as its ORM. See **[Chapter 08 - Technical Architecture]**.

---

## P

### PDF.js
Mozilla's open-source JavaScript library for rendering PDF documents in the browser without plugins. CaseZero uses PDF.js to display document evidence. See **[Chapter 07 - User Interface]** and **[Chapter 08 - Technical Architecture]**.

### Playwright
A browser automation framework for end-to-end testing. Supports Chrome, Firefox, Safari. CaseZero uses Playwright for E2E test flows. See **[Chapter 11 - Testing Strategy]**.

### PostgreSQL
An open-source relational database management system. Version 15+ used for CaseZero, hosted on Azure Database for PostgreSQL. Chosen for JSONB support and strong ACID guarantees. See **[Chapter 08 - Technical Architecture]**.

### PWA (Progressive Web App)
A web application that uses modern web capabilities to provide app-like experiences (offline support, push notifications, installability). CaseZero implements Service Worker for offline case viewing. See **[Chapter 08 - Technical Architecture]**.

---

## Q

### QA (Quality Assurance)
The process of ensuring software meets quality standards through testing and review. CaseZero QA includes automated testing, content playthroughs, and blind testing by QA testers. See **[Chapter 11 - Testing Strategy]** and **[Chapter 10 - Content Pipeline]**.

### Quality Gate
A checkpoint in the development/release process that must pass specific criteria before proceeding. CaseZero has quality gates for pre-merge (tests pass, coverage ≥80%), pre-deployment (performance benchmarks met), and case publication (QA playthrough). See **[Chapter 11 - Testing Strategy]**.

---

## R

### React
A JavaScript library for building user interfaces developed by Meta. Version 18+ used for CaseZero frontend. See **[Chapter 08 - Technical Architecture]**.

### React Router
A routing library for React applications. Version 6 used for navigation between pages (Dashboard, Case Files, Forensics Lab, Submit Solution, Profile). See **[Chapter 08 - Technical Architecture]**.

### Redux Toolkit
The official, opinionated toolset for efficient Redux development. CaseZero uses Redux Toolkit for frontend state management with slices for auth, cases, documents, evidence, forensics, notes, UI. See **[Chapter 08 - Technical Architecture]**.

### REST API (Representational State Transfer)
An architectural style for web services using HTTP methods (GET, POST, PUT, DELETE) and resource-based URLs. CaseZero backend exposes a RESTful API. See **[Chapter 08 - Technical Architecture]**.

### RTO (Recovery Time Objective)
The maximum acceptable time that a system can be down after a disaster. CaseZero's RTO is 1 hour (critical services restored within 1 hour). See **[Chapter 08 - Technical Architecture]** for disaster recovery.

### RPO (Recovery Point Objective)
The maximum acceptable amount of data loss measured in time. CaseZero's RPO is 24 hours (daily backups, can lose up to 24 hours of data). See **[Chapter 08 - Technical Architecture]** for disaster recovery.

---

## S

### Service Worker
A JavaScript script that runs in the background, separate from the web page, enabling features like offline support and push notifications. CaseZero implements Service Worker to cache case data for offline viewing. See **[Chapter 08 - Technical Architecture]**.

### Session
See **Case Session**.

### Solution
The correct answer to a case, including culprit identity, motive, method, timeline of events, and key evidence. Stored in case.json but never exposed to players until they submit. See **[Chapter 04 - Case Structure]**.

### Submission
The player's answer to a case, submitted through the five-page Submit Solution form. Includes culprit selection, motive explanation (100-500 words), method explanation (50-300 words), key evidence selection (3-8 items), and review/confirmation. See **[Chapter 03 - Core Mechanics]**.

### Suspect
A person of interest in the murder investigation. Each case includes 2-8 suspects with name, age, occupation, relationship to victim, alibi, motive potential, background, and physical description. One suspect is always the culprit. See **[Chapter 04 - Case Structure]**.

---

## T

### Tailwind CSS
A utility-first CSS framework for rapidly building custom user interfaces. CaseZero uses Tailwind CSS for styling with custom theme configuration. See **[Chapter 08 - Technical Architecture]**.

### Timeline
A chronological sequence of events before, during, and after the murder. Each event has timestamp (date/time or relative description), event description, and participants. Players reconstruct the timeline to understand the crime. See **[Chapter 04 - Case Structure]**.

### Timeline View
A UI component that displays case events in chronological order, visually indicating the time of death. Helps players understand the sequence of events. See **[Chapter 07 - User Interface]**.

### Toxicology
A forensic analysis type that tests bodily fluids or tissue samples for presence of drugs, poisons, or other substances. Medium-long turnaround time (12 hours base). See **[Chapter 03 - Core Mechanics]**.

### TypeScript
A strongly-typed superset of JavaScript that compiles to plain JavaScript. CaseZero frontend is written in TypeScript for type safety and better developer experience. See **[Chapter 08 - Technical Architecture]**.

---

## U

### UGC (User-Generated Content)
Content created by players rather than the development team. Year 2+ roadmap includes case editor tools allowing players to create and share custom cases. See **[Chapter 12 - Product Roadmap]**.

### Unit Testing
Testing methodology that validates individual functions/components in isolation. CaseZero uses Vitest (frontend) and xUnit (backend) for unit testing. Target coverage: ≥80%. See **[Chapter 11 - Testing Strategy]**.

### User Progress
A persistent record of a player's overall achievement, including total XP, current rank, cases solved, total time played, hints used, achievements earned, and statistics. See **[Chapter 09 - Data Schema & Models]**.

### UX (User Experience)
The overall experience a person has when interacting with a product. CaseZero prioritizes immersive UX through authentic document design, realistic forensics, and pressure-free investigation. See **[Chapter 01 - Game Concept]** for design philosophy.

---

## V

### Validation
The process of ensuring data meets required format and business rules. CaseZero validates case.json structure, user input (submission form), and API requests. See **[Chapter 09 - Data Schema & Models]** for validation rules.

### Victim
The murder victim. Case includes victim name, age, occupation, personal background, physical description, relationships, and discovered location/condition. See **[Chapter 04 - Case Structure]**.

### Vite
A modern frontend build tool that provides fast development server and optimized production builds. CaseZero uses Vite as its build system. See **[Chapter 08 - Technical Architecture]**.

### Vitest
A Vite-native unit testing framework. CaseZero uses Vitest for frontend unit tests. See **[Chapter 11 - Testing Strategy]**.

---

## W

### WCAG (Web Content Accessibility Guidelines)
International standards for making web content accessible to people with disabilities. CaseZero targets WCAG 2.1 AA compliance (4.5:1 contrast ratio, keyboard navigation, screen reader support, ARIA landmarks). See **[Chapter 11 - Testing Strategy]** for accessibility testing.

### Witness
A person who has information relevant to the case but is not a suspect. May have seen events, heard conversations, or possess knowledge about victim/suspects. Information typically provided through witness statements or interview transcripts. See **[Chapter 04 - Case Structure]**.

---

## X

### XP
See **Experience Points**.

### XSS (Cross-Site Scripting)
A security vulnerability where attackers inject malicious scripts into web pages viewed by other users. CaseZero prevents XSS through CSP headers, input sanitization, and React's automatic escaping. See **[Chapter 08 - Technical Architecture]** for security measures.

---

## Y

### YAML (YAML Ain't Markup Language)
A human-readable data serialization language. CaseZero uses YAML for CI/CD pipeline configuration (GitHub Actions workflows) and configuration files. See **[Chapter 11 - Testing Strategy]**.

---

## Z

### Zero-Downtime Deployment
A deployment strategy that updates production systems without making them unavailable to users. CaseZero achieves this through blue-green deployment. See **[Chapter 08 - Technical Architecture]**.

---

## Cross-Reference Index

### Terms by Chapter

**Chapter 01 - Game Concept:**
UX, Immersive Design, Realism, Investigation

**Chapter 03 - Core Mechanics:**
Forensic Analysis, DNA Analysis, Fingerprint Analysis, Toxicology, Ballistics, Notes System, Submission

**Chapter 04 - Case Structure:**
Case, case.json, Victim, Crime Scene, Suspect, Evidence, Witness, Document, Timeline, Solution, Culprit, Motive, Method, Key Evidence, Clue, Difficulty

**Chapter 06 - Player Progression:**
XP, Detective Rank, Achievement, User Progress

**Chapter 07 - User Interface:**
Case Files, Document Viewer, Evidence Gallery, Timeline View, Forensics Lab

**Chapter 08 - Technical Architecture:**
API, REST API, ASP.NET Core, React, TypeScript, Redux Toolkit, PostgreSQL, JSONB, Azure App Service, Azure Functions, Azure Blob Storage, Azure CDN, JWT, Authentication, Authorization, Service Worker, PWA, PDF.js, Vite, EF Core, Blue-Green Deployment, CI/CD, IaC, Bicep, CORS, CSP, XSS, Hot Tier, RTO, RPO, Zero-Downtime Deployment

**Chapter 09 - Data Schema & Models:**
DTO, Case Session, Forensic Request, User Progress, Validation, FluentValidation, JSON Schema, ORM

**Chapter 10 - Content Pipeline:**
QA, Localization, Clue, Content Creation

**Chapter 11 - Testing Strategy:**
Unit Testing, Integration Testing, E2E Testing, Load Testing, Playwright, Vitest, Quality Gate, WCAG, GitHub Actions, CI/CD

**Chapter 12 - Product Roadmap:**
MVP, UGC, DAU/MAU, NPS

### Terms by Technology Category

**Frontend Technologies:**
React, TypeScript, Redux Toolkit, Vite, Tailwind CSS, React Router, PDF.js, Service Worker, PWA, Vitest

**Backend Technologies:**
ASP.NET Core, C#, EF Core, FluentValidation, JWT, REST API

**Database Technologies:**
PostgreSQL, JSONB, SQL, ORM

**Cloud Technologies:**
Azure App Service, Azure Functions, Azure Blob Storage, Azure CDN, Azure Database for PostgreSQL, IaC, Bicep

**Testing Technologies:**
Vitest, xUnit, Moq, Playwright, WebApplicationFactory, Artillery, axe-playwright

**DevOps Technologies:**
GitHub Actions, CI/CD, Docker, Blue-Green Deployment, YAML

**Security Technologies:**
JWT, CORS, CSP, XSS Prevention, HTTPS

### Terms by Game Concept

**Case Elements:**
Case, case.json, Victim, Suspect, Witness, Crime Scene, Evidence, Document, Timeline, Solution

**Investigation Actions:**
Notes System, Document Viewer, Evidence Gallery, Forensic Request, Submission

**Forensics:**
Forensic Analysis, DNA Analysis, Fingerprint Analysis, Toxicology, Ballistics, Forensics Lab

**Progression:**
XP, Detective Rank, Achievement, User Progress

**Quality Attributes:**
Difficulty, Clue, Key Evidence, Culprit, Motive, Method

---

## Acronyms Quick Reference

- **API** - Application Programming Interface
- **CDN** - Content Delivery Network
- **CI/CD** - Continuous Integration / Continuous Deployment
- **CORS** - Cross-Origin Resource Sharing
- **CSP** - Content Security Policy
- **DAU** - Daily Active Users
- **DNA** - Deoxyribonucleic Acid
- **DTO** - Data Transfer Object
- **E2E** - End-to-End
- **EF Core** - Entity Framework Core
- **HTTP** - Hypertext Transfer Protocol
- **HTTPS** - HTTP Secure
- **IaC** - Infrastructure as Code
- **JSON** - JavaScript Object Notation
- **JSONB** - JSON Binary (PostgreSQL data type)
- **JWT** - JSON Web Token
- **L10n** - Localization
- **MAU** - Monthly Active Users
- **MVP** - Minimum Viable Product
- **NPS** - Net Promoter Score
- **ORM** - Object-Relational Mapping
- **PDF** - Portable Document Format
- **PWA** - Progressive Web App
- **QA** - Quality Assurance
- **REST** - Representational State Transfer
- **RPO** - Recovery Point Objective
- **RTO** - Recovery Time Objective
- **SQL** - Structured Query Language
- **UGC** - User-Generated Content
- **UI** - User Interface
- **UX** - User Experience
- **WCAG** - Web Content Accessibility Guidelines
- **XP** - Experience Points
- **XSS** - Cross-Site Scripting
- **YAML** - YAML Ain't Markup Language

---

## Pronunciation Guide

- **Bicep** - BY-sep (not "by-ceps")
- **case.json** - "case dot jay-son" (file extension pronounced)
- **JSONB** - "jay-son bee" (JSON Binary)
- **PostgreSQL** - "post-gres cue-el" or "post-gress" (informal)
- **Vite** - "veet" (French for "fast")
- **Vitest** - "veet-test"
- **WCAG** - "wuh-cag" or spell out "W-C-A-G"

---

## Revision History

| Version | Date | Changes | Author |
|---------|------|---------|--------|
| 1.0 | 2024-11-14 | Initial glossary creation with 100+ terms | AI Assistant |

---

**Document Status:** Complete  
**Last Updated:** November 14, 2024  
**Next Review:** After major feature additions or terminology changes
