# 📖 Game Design Document - CaseZero v3.0
## Cold Case Detective Experience

**Project:** CaseZero  
**Version:** 3.0 - Complete Redesign  
**Date:** November 13, 2025  
**Genre:** Detective / Investigation / Document Analysis  
**Platform:** Web (Desktop/Tablet)  
**Target Audience:** Adults 18+ interested in true crime and investigation

---

## 📑 Document Structure

### **Core Design**
1. [**01-CONCEPT.md**](01-CONCEPT.md) - High Concept & Vision
2. [**02-GAMEPLAY.md**](02-GAMEPLAY.md) - Core Gameplay Loop
3. [**03-MECHANICS.md**](03-MECHANICS.md) - Game Mechanics & Systems

### **Content & World**
4. [**04-CASE-STRUCTURE.md**](04-CASE-STRUCTURE.md) - Case Design & Schema
5. [**05-NARRATIVE.md**](05-NARRATIVE.md) - Storytelling & Writing Guidelines
6. [**06-PROGRESSION.md**](06-PROGRESSION.md) - Player Progression & Ranking

### **Technical Design**
7. [**07-USER-INTERFACE.md**](07-USER-INTERFACE.md) - UI/UX Design
8. [**08-TECHNICAL.md**](08-TECHNICAL.md) - Technical Architecture
9. [**09-DATA-SCHEMA.md**](09-DATA-SCHEMA.md) - Data Structures & Formats

### **Production**
10. [**10-CONTENT-PIPELINE.md**](10-CONTENT-PIPELINE.md) - Asset Creation Workflow
11. [**11-TESTING.md**](11-TESTING.md) - QA & Playtesting
12. [**12-ROADMAP.md**](12-ROADMAP.md) - Development Roadmap & Milestones

### **Appendices**
13. [**APPENDIX-A-GLOSSARY.md**](APPENDIX-A-GLOSSARY.md) - Terminology
14. [**APPENDIX-B-REFERENCES.md**](APPENDIX-B-REFERENCES.md) - Inspirations & Research
15. [**APPENDIX-C-DECISIONS.md**](APPENDIX-C-DECISIONS.md) - Design Decisions Log

---

## 🎯 Quick Reference

### **Core Pillars**
1. **Authenticity** - Realistic police work, no fantasy elements
2. **Autonomy** - Player-driven investigation, minimal hand-holding
3. **Analysis** - Deep document reading and evidence correlation
4. **Patience** - Real-time forensics, deliberate pacing

### **Key Features**
- ✅ Static document investigation (PDFs, photos)
- ✅ Real-time forensic analysis system
- ✅ Multiple suspects with complex motives
- ✅ Solution submission with limited attempts
- ✅ Detective ranking progression
- ❌ NO mini-games or action sequences
- ❌ NO dialogue trees or interrogations
- ❌ NO timer pressure or arcade elements
- ❌ NO fantasy/supernatural elements

### **Target Experience**
> "You're a cold case detective. You have documents, photos, forensic reports, and time. No shortcuts, no magic clues. Just your brain and determination."

---

## 📊 Document Status

| Chapter | Status | Last Updated | Author |
|---------|--------|--------------|--------|
| 01-CONCEPT | ✅ Complete | 2025-11-13 | AI Assistant |
| 02-GAMEPLAY | ✅ Complete | 2025-11-13 | AI Assistant |
| 03-MECHANICS | ✅ Complete | 2025-11-13 | AI Assistant |
| 04-CASE-STRUCTURE | ✅ Complete | 2025-11-13 | AI Assistant |
| 05-NARRATIVE | ✅ Complete | 2025-11-13 | AI Assistant |
| 06-PROGRESSION | ✅ Complete | 2025-11-13 | AI Assistant |
| 07-USER-INTERFACE | ✅ Complete | 2025-11-13 | AI Assistant |
| 08-TECHNICAL | ✅ Complete | 2025-11-13 | AI Assistant |
| 09-DATA-SCHEMA | ✅ Complete | 2025-11-14 | AI Assistant |
| 10-CONTENT-PIPELINE | ✅ Complete | 2025-11-14 | AI Assistant |
| 11-TESTING | ✅ Complete | 2025-11-14 | AI Assistant |
| 12-ROADMAP | ✅ Complete | 2025-11-14 | AI Assistant |

**Legend:**
- ⏳ Pending - Not started
- 🚧 In Progress - Being written
- ✅ Complete - Ready for review
- 🔄 Review - Under revision
- 📌 Approved - Final version

---

## 🔄 Version History

| Version | Date | Changes | Author |
|---------|------|---------|--------|
| 0.1 | 2025-11-13 | Initial structure created | AI Assistant |
| | | | |
| | | | |

---

## 📝 How to Use This GDD

### **For Developers**
- Start with 01-CONCEPT to understand the vision
- Read 02-GAMEPLAY and 03-MECHANICS for implementation details
- Refer to 08-TECHNICAL and 09-DATA-SCHEMA for architecture
- Use 04-CASE-STRUCTURE when creating content

### **For Designers**
- Review 01-CONCEPT and 02-GAMEPLAY for design philosophy
- Focus on 04-CASE-STRUCTURE and 05-NARRATIVE for content creation
- Consult 07-USER-INTERFACE for UX guidelines
- Check 10-CONTENT-PIPELINE for workflow

### **For Project Managers**
- Monitor progress using Document Status table
- Reference 12-ROADMAP for milestones and timelines
- Review APPENDIX-C-DECISIONS for context on choices

### **For QA/Testers**
- Study 11-TESTING for test scenarios
- Understand 02-GAMEPLAY and 03-MECHANICS for expected behavior
- Use 04-CASE-STRUCTURE to validate case quality

---

## 🎮 Current Build Status

**Version:** 3.0 (in active development)
**Branch:** main
**Status:** Design complete; core systems implemented and evolving

> **⚠️ Note:** This GDD was originally written during the design phase. Since then, substantial parts of the game have been **implemented** and now differ in specifics from the original design chapters (most notably progression/ranks and the case-generation pipeline). Each chapter has been reviewed and annotated where its content is aspirational/roadmap rather than current behavior. For a quick, verified snapshot of what's actually live today, see below.

**Implemented today:**
- ✅ Canonical **`case.json` v2** format (`docs/CASE_JSON_V2_SPEC.md`, `schemas/case.schema.json`) — server-only fields sanitized before reaching the client
- ✅ ASP.NET Core Web API backend (**.NET 8**) with JWT + ASP.NET Identity, EF Core (SQL Server / SQLite)
- ✅ Automated **Case v2 generation pipeline** on Azure Durable Functions (**CaseGen.Functions, .NET 9**): Case Bible → external LLM agent prompts → CaseGraph consistency checks → retries with backoff → phase progress reporting → solver gate (score ≥ 0.90) → multi-stage validation → ordered Blob publication (assets first, `case.json` written last as the commit marker)
- ✅ 7-level `Rookie → Detective → Detective2 → Sergeant → Lieutenant → Captain → Commander` difficulty/rank system, with promotion based on cumulative graded-correct case resolves (see Chapter 06 for exact thresholds)
- ✅ React/TypeScript frontend with an admin-only case-generation UI (`/case-generation`) and a 4-locale UI (`en-US`, `pt-BR`, `es-ES`, `fr-FR`)

**In progress / design-only (see individual chapters for details):** the original 8-rank/XP progression model, most of the long-term product roadmap (Chapter 12), and several UI/mechanics ideas remain aspirational.

**Completed:**
- ✅ Initial concept and vision defined
- ✅ PLAN-V3 documentation created
- ✅ BRAINSTORM initial analysis

**Next Steps:**
1. Keep GDD chapters in sync with implementation as new systems ship
2. Continue expanding case content and locales
3. Revisit aspirational chapters (progression, roadmap) as those systems evolve

---

## 📧 Document Feedback

For questions, suggestions, or clarifications about this GDD:
- Open an issue in the repository
- Tag sections that need clarification
- Propose changes via pull request
- Add comments to APPENDIX-C-DECISIONS

---

**Last Updated:** November 13, 2025  
**Next Review:** TBD
