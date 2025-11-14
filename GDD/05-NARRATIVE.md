# Chapter 05 - Narrative & Writing

**Game Design Document - CaseZero v3.0**  
**Last Updated:** November 13, 2025  
**Status:** ✅ Complete

---

## 5.1 Overview

This chapter establishes **writing guidelines, tone, and narrative principles** for all case content in CaseZero. The quality of writing directly impacts player immersion, so every document must feel authentic, professionally written, and consistent with real investigative materials.

**Key Concepts:**
- Professional, realistic tone (not dramatic or pulpy)
- Show, don't tell (let players discover)
- Respect player intelligence
- Authentic police/forensic language
- Subtle clue placement
- Character depth without exposition

---

## 5.2 Core Writing Principles

### Principle 1: Authenticity Over Drama

**DO:**
- Write like real police reports (factual, dry, precise)
- Use authentic procedural language
- Include bureaucratic details (case numbers, badge numbers, timestamps)
- Keep tone professional and detached

**DON'T:**
- Write like a crime thriller novel
- Add unnecessary dramatic flair
- Include poetic descriptions
- Make suspects monologue their guilt

**Example - Police Report:**

✅ **GOOD (Authentic):**
```
At approximately 00:30 hours, this officer responded to a 
report of a deceased individual at 450 Market Street, 
Floor 15. Upon arrival, victim was found unresponsive in 
private office with apparent gunshot wound to chest. 
Scene secured at 00:45. CSI notified.
```

❌ **BAD (Dramatic):**
```
The night was cold and quiet when Officer Martinez 
arrived at the scene of what would become the most 
puzzling case of her career. The victim lay in a pool 
of blood, his lifeless eyes staring at the ceiling, 
as if accusing his killer from beyond the grave...
```

### Principle 2: Show, Don't Tell

**DO:**
- Present evidence objectively
- Let players draw conclusions
- Include contradictions without flagging them
- Trust player intelligence

**DON'T:**
- Point out clues ("This is important!")
- Explain significance of evidence
- Tell players what to think
- Make answers obvious

**Example - Witness Statement:**

✅ **GOOD (Showing):**
```
Q: What time did you see Mr. Torres leave the building?
A: Around 11:45, maybe 11:50. I remember because I was 
   about to start my midnight rounds.

Q: Did you notice anything unusual about him?
A: He was walking fast, kept his head down. And he was 
   carrying something under his jacket, but I didn't 
   get a good look.
```

❌ **BAD (Telling):**
```
Q: What time did you see the suspect leave?
A: 11:45 PM exactly. He was clearly nervous and hiding 
   something - probably the murder weapon! He looked 
   guilty to me. I think he did it.
```

### Principle 3: Respect Player Intelligence

**DO:**
- Use proper terminology (no dumbing down)
- Include technical details
- Present complex information clearly
- Allow multiple interpretations

**DON'T:**
- Over-explain everything
- Simplify to the point of unrealism
- Hold player's hand
- Provide obvious answers

### Principle 4: Subtlety in Clue Placement

**DO:**
- Bury important details in paragraphs
- Include relevant info casually
- Mix critical and mundane details
- Make clues feel natural

**DON'T:**
- Isolate clues in separate sentences
- Use formatting to highlight clues
- Repeat important information excessively
- Make clues stand out artificially

**Example - Financial Document:**

✅ **GOOD (Subtle):**
```
Account Statement - February 2023
01/15  Payroll Deposit        $12,500
02/01  Wire Transfer OUT      -$50,000
02/15  Payroll Deposit        $12,500
02/20  ATM Withdrawal         -$200
02/28  Payment Due Notice     $500,000 (Loan #LN-4421)
03/01  INSUFFICIENT FUNDS - Auto-pay declined
03/15  Closing Balance        $42,180
```
*Player must notice the insufficient funds for the loan payment and connect to motive*

❌ **BAD (Obvious):**
```
**IMPORTANT:** Torres owes victim $500,000!
**CRITICAL CLUE:** Payment was due March 1st!
**NOTE:** He doesn't have the money!
```

---

## 5.3 Tone & Voice Guidelines

### Police Reports

**Tone:** Formal, objective, bureaucratic  
**Voice:** Third-person, passive where appropriate  
**Structure:** Header, summary, chronological narrative, evidence list, conclusions  
**Language:** Professional law enforcement terminology

**Vocabulary:**
- "Deceased individual" not "dead body"
- "Unresponsive" not "dead"
- "Apparent gunshot wound" not "shot"
- "Scene secured" not "locked down"
- "Witness reported" not "witness said"

**Avoid:**
- First-person perspective
- Emotional language
- Speculation presented as fact
- Informal contractions

**Example Opening:**
```
INCIDENT REPORT #2023-0315

Classification: Suspicious Death - Homicide Investigation
Date/Time: March 16, 2023, 00:30 hours
Location: 450 Market Street, Suite 1504
Reporting Officer: Martinez, Sarah (Badge #4521)

SUMMARY:
On the above date and time, this officer responded to 
a report of a deceased individual at the listed location. 
Initial investigation revealed apparent homicide. Scene 
was secured and Homicide Division notified.
```

### Witness Statements

**Tone:** Conversational but formal (official statement)  
**Voice:** First-person (witness speaking)  
**Structure:** Header, statement narrative, signature  
**Language:** Natural speech patterns, with proper grammar

**Characteristics:**
- Reflects witness personality/education
- Includes uncertainty ("I think", "maybe", "around")
- May contain errors in recall
- Personal observations and feelings allowed

**Example:**
```
I was making my rounds on the 12th floor when I noticed 
the light still on in Mr. Chen's office up on 15. That 
was unusual for past midnight. I took the elevator up 
to check, but his door was locked. I knocked a few times, 
no answer. I figured he just left it on by mistake, so 
I made a note to check again in an hour.

When I came back around 12:30, the light was still on. 
This time I used my master key to check. That's when I 
found him. I immediately called 911 and secured the area.
```

### Suspect Interviews

**Tone:** Tense, sometimes evasive, varies by personality  
**Voice:** Q&A format (detective and suspect)  
**Structure:** Header, participants, transcript, conclusion  
**Language:** Natural dialogue, interruptions, pauses

**Characteristics:**
- Guilty suspects show tells (pauses, evasion, contradictions)
- Innocent suspects can be nervous too (red herring)
- Dialogue reflects personality and stress level
- Include non-verbal cues in brackets

**Example (Guilty Suspect):**
```
DET. WONG: Where were you on March 15th between 10 PM 
and midnight?

TORRES: I was home. Watching TV.

DET. WONG: Can anyone verify that?

TORRES: [Pauses] No, I live alone. My girlfriend was 
out of town that week.

DET. WONG: We have security logs showing your access 
card was used at the TechCorp building at 11:15 PM.

TORRES: [Long pause] I... I don't remember that. Maybe 
I stopped by to grab something? I don't know, it was 
late.

DET. WONG: You don't remember going to the building 
where your business partner was murdered?

TORRES: [Agitated] I want to speak to my lawyer.
```

### Forensic Reports

**Tone:** Scientific, precise, technical  
**Voice:** Third-person, objective  
**Structure:** Header, evidence description, methodology, findings, conclusions  
**Language:** Scientific terminology, measurements, statistics

**Characteristics:**
- Technical accuracy (use proper terms)
- Quantitative data (percentages, measurements)
- Methodology transparency
- Cautious conclusions ("consistent with", "indicates", "suggests")

**Example:**
```
METHODOLOGY:
DNA extraction was performed using standard Chelex 
protocol. PCR amplification targeted 16 STR loci plus 
amelogenin for sex determination. Products were analyzed 
via capillary electrophoresis on ABI 3500 Genetic Analyzer.

FINDINGS:
DNA profile obtained from evidence sample EV-004. Profile 
shows male contributor. Comparison against known reference 
sample from Michael Torres (SUSP-001) shows match at all 
16 loci.

STATISTICAL ANALYSIS:
Random Match Probability: 1 in 5.2 billion
Likelihood Ratio: 5.2 x 10^9

CONCLUSIONS:
The DNA profile from evidence EV-004 is consistent with 
Michael Torres with a statistical confidence of 99.7%. 
This result indicates Torres was the source of the 
biological material recovered from the crime scene.
```

### Personal Documents

**Tone:** Varies (informal, emotional, personal)  
**Voice:** First-person (author speaking)  
**Structure:** Varies by document type (email, letter, diary)  
**Language:** Natural, personal, may contain errors

**Characteristics:**
- Reflects character personality and education
- May reveal thoughts, feelings, secrets
- Can contain spelling/grammar errors (realistic)
- More emotional than official documents

**Example - Email:**
```
From: robert.chen@techcorp.com
To: michael.torres@techcorp.com
Date: March 10, 2023, 3:42 PM
Subject: Re: Loan Payment

Mike,

I've been more than patient. The $500k was due last week. 
I don't want to do this, but if you can't pay by end of 
month, I'll have to proceed with the buyout clause in our 
partnership agreement. You'll lose your shares.

This isn't personal. It's business.

Robert
```

---

## 5.4 Character Development

### Creating Believable Victims

**DO:**
- Give victims full backstories (even if not all revealed)
- Make them flawed humans, not perfect martyrs
- Show their relationships (good and bad)
- Include personal details that humanize

**DON'T:**
- Make victims one-dimensional
- Present only positive traits
- Make them too sympathetic (manipulative)
- Leave them as blank slates

**Example - Victim Background:**
```
Robert Chen, 42, founded TechCorp in 2018 with partner 
Michael Torres. Ambitious and driven, Chen was known for 
his demanding management style. Employees described him as 
"brilliant but difficult." His marriage to Linda was 
reportedly strained, with couples counseling records 
dating back two years.

Chen was meticulous about finances and held partners to 
strict accountability. Friends noted he had become 
increasingly stressed in recent months, likely due to 
business pressures and personal conflicts.
```

### Creating Believable Suspects

**Guilty Suspect:**
- Strong motive (financial, emotional, revenge)
- Opportunity (access, timing)
- Weak or unverifiable alibi
- Nervous or evasive in interview
- Forensic evidence connects them
- But: Should seem plausible, not cartoonishly guilty

**Innocent Suspect (Red Herring):**
- Plausible motive (appears strong initially)
- Apparent opportunity (but actually doesn't match)
- Suspicious behavior (but explainable)
- Eventually exonerated by evidence
- But: Should feel like a real suspect until cleared

**Example - Red Herring Suspect:**
```
Linda Chen appears to benefit significantly from her 
husband's death ($2M life insurance). Marital problems 
are documented. She has the key to his office. On the 
surface, she's the obvious suspect.

However:
- CCTV shows she never left home that night
- Her DNA is not at the crime scene
- She has no gunshot residue
- Phone records show no suspicious calls

The evidence exonerates her completely. The life insurance 
was mutual (he had the same on her). The marriage issues 
were real but not murderous.
```

### Character Personality Through Writing

**Show personality through:**
- Word choice (educated vs. colloquial)
- Sentence structure (simple vs. complex)
- Emotional tone (calm vs. agitated)
- Details mentioned (observant vs. vague)
- Consistency (truthful vs. contradictory)

**Example - Educated Suspect:**
```
TORRES: I find it concerning that you're focusing on 
circumstantial evidence rather than actual proof. The 
access log merely indicates my card was used; it doesn't 
prove I was the one who used it. Corporate espionage and 
card cloning are not uncommon in our industry.
```

**Example - Working-Class Witness:**
```
SILVA: Look, I just do my job, you know? Make the rounds, 
check the doors, keep things secure. I seen him come in 
around 11-ish, maybe 11:15. Didn't think nothing of it - 
people work late sometimes. He looked normal to me, I guess.
```

---

## 5.5 Plot Structure & Pacing

### Case Narrative Arc

**Act 1: Discovery (Documents 1-3)**
- Police report establishes crime
- Initial witness statements
- Basic evidence collection
- Players understand WHAT happened

**Act 2: Investigation (Documents 4-10)**
- Suspect interviews
- Forensic requests and results
- Background documents
- Financial/personal records
- Players understand WHO could have done it

**Act 3: Resolution (Documents 11+, Forensics)**
- Critical forensic results
- Contradictions resolved
- Timeline becomes clear
- Players understand WHY and HOW

### Information Reveal Strategy

**Early Documents (Always Available):**
- Crime scene basics
- Victim information
- Initial suspect list
- Obvious evidence

**Mid-Investigation (Available, requires reading):**
- Motives revealed
- Alibis presented (some false)
- Relationships detailed
- Timeline gaps shown

**Late Investigation (Requires forensics):**
- DNA matches
- Ballistics results
- Forensic confirmations
- Final pieces of puzzle

**Solution Phase (Player deduction):**
- Player correlates all information
- Identifies contradictions in alibis
- Connects forensics to suspects
- Forms complete theory

---

## 5.6 Clue Placement Strategy

### Types of Clues

**Direct Evidence Clues:**
- Forensic matches (DNA, ballistics)
- Physical evidence at scene
- Documented timeline entries
- Verifiable facts

**Indirect Evidence Clues:**
- Contradictions in statements
- Timeline impossibilities
- Motive establishment
- Opportunity confirmation

**Character Behavioral Clues:**
- Evasive answers in interviews
- Inconsistent statements
- Nervous reactions
- Unexplained knowledge

**Background Clues:**
- Financial pressures
- Relationship tensions
- Historical conflicts
- Prior incidents

### Clue Distribution Guidelines

**Every case needs:**
- 3-5 **CRITICAL** clues (solution impossible without them)
- 5-8 **IMPORTANT** clues (strongly support solution)
- 8-12 **SUPPORTING** clues (provide context, confirm theory)
- 5-10 **RED HERRING** clues (point to wrong suspect)

**Critical Clue Placement:**
- ✅ Must be discoverable (in available documents/forensics)
- ✅ Should appear natural, not highlighted
- ✅ Can be spread across multiple documents
- ✅ Ideally requires player to correlate information
- ❌ Never hidden in one obscure sentence
- ❌ Never require external knowledge

**Example - Critical Clue (Spread Across Documents):**

*Document A (Access Log):*
"23:15 - Michael Torres - Floor 15 Entry"

*Document B (Witness Statement):*
"I saw someone matching Torres' description enter the elevator around 11:15 PM"

*Document C (Forensic Report):*
"Estimated time of death: 23:30 (11:30 PM) ±15 minutes"

*Document D (Interview):*
"TORRES: I was home all night. I didn't leave my apartment."

*Player must connect:* Torres was at building → During murder window → Lied about alibi → Strong evidence of guilt

---

## 5.7 Red Herring Design

### Effective Red Herrings

**Characteristics:**
- Plausible motive and opportunity
- Some evidence pointing to them
- Suspicious behavior or statements
- Eventually exonerated by stronger evidence

**How to Create:**

1. **Give them a real motive**
   - Financial benefit
   - Personal grudge
   - Relationship conflict

2. **Make their alibi weak initially**
   - "I was home alone"
   - "I don't remember exactly"
   - Timing seems suspicious

3. **Add suspicious elements**
   - Nervous in interview
   - Benefit from victim's death
   - Had access/opportunity

4. **Provide exoneration**
   - CCTV proves alibi
   - Forensics clear them
   - Timeline makes guilt impossible
   - Third-party verification

**Example - Full Red Herring:**

**Initial Presentation (Looks Guilty):**
```
David Park, 29, former employee fired by victim 6 months 
ago. Made threats on social media. Has assault charge 
from 2019. Motive: revenge. Still had building access 
(old card not deactivated?). Interview shows hostility.
```

**Exoneration (Actually Innocent):**
```
- Access card was deactivated (verified with security)
- Multiple witnesses place him at bar 8 PM - 1 AM
- Receipts, CCTV, bartender all confirm
- No forensic connection to crime scene
- Social media threats were venting, not serious
- Assault charge was bar fight, charges dismissed
```

**Player should think:** "He seemed like the obvious suspect, but the evidence doesn't support it. Time to look elsewhere."

---

## 5.8 Dialogue Writing

### Interview Dialogue Guidelines

**Structure:**
```
DET. [NAME]: [Question]

[SUSPECT]: [Answer, with behavioral cues if relevant]
```

**DO:**
- Write natural speech (contractions, pauses, incomplete sentences)
- Include behavioral cues [in brackets]
- Show stress through dialogue (repetition, evasion)
- Let personality come through

**DON'T:**
- Make suspects confess easily
- Write perfect, formal speech
- Explain too much
- Make dialogue unrealistically expository

**Example - Natural Dialogue:**

✅ **GOOD:**
```
DET. WONG: Where were you Tuesday night?

TORRES: Tuesday? [Pauses] I was... I think I was home.

DET. WONG: You think?

TORRES: No, I was. I was home. Definitely.

DET. WONG: Alone?

TORRES: [Hesitates] Yeah. My girlfriend was traveling.

DET. WONG: So no one can confirm you were there?

TORRES: [Shifts in seat] No, but that doesn't mean—look, 
I didn't do anything wrong.
```

❌ **BAD:**
```
DET. WONG: Where were you on the night of the murder?

TORRES: I was at my apartment located at 123 Main Street 
from approximately 9 PM until the following morning. I 
cannot provide anyone who can verify my whereabouts as 
I was alone. However, I assure you that I am innocent 
and did not commit this crime despite my financial motive.
```

### Witness Dialogue

**Characteristics:**
- More cooperative than suspects
- Natural recall (with uncertainty)
- May be nervous but not evasive
- Provide observational details

**Example:**
```
DET. MARTINEZ: Tell me what you saw that night.

SILVA: Well, I was doing my midnight rounds, right? Start 
on the ground floor, work my way up. When I got to 12, I 
noticed the light on up on 15—Mr. Chen's office. That's 
unusual for that late.

DET. MARTINEZ: What time was this?

SILVA: Maybe 12:20? Could've been 12:30. I check the 
clock when I start rounds, but I don't look every minute, 
you know?

DET. MARTINEZ: Go on.

SILVA: So I went up to check. Door was locked. I knocked, 
no answer. Figured he left it on. But when I came back 
an hour later, still on. That's when I opened it and... 
[trails off] ...found him.
```

---

## 5.9 Document Formatting Standards

### Headers

**Police Report:**
```
METROPOLITAN POLICE DEPARTMENT
INCIDENT REPORT

Case Number: [YYYY-####]
Classification: [Crime Type]
Date/Time: [Full timestamp]
Location: [Address]
Reporting Officer: [Name, Badge #]
Date Filed: [Date]
```

**Witness Statement:**
```
METROPOLITAN POLICE DEPARTMENT
WITNESS STATEMENT

Case Number: [YYYY-####]
Witness Name: [Full Name]
Date of Birth: [DOB]
Address: [Address]
Date of Statement: [Date]
Interviewing Officer: [Name, Badge #]
```

**Suspect Interview:**
```
METROPOLITAN POLICE DEPARTMENT
INTERVIEW TRANSCRIPT

Case Number: [YYYY-####]
Interviewee: [Full Name]
Date: [Date], [Time]
Location: [Police Station, Room #]
Interviewer: [Detective Name]
Also Present: [Attorney, if applicable]
```

**Forensic Report:**
```
METROPOLITAN FORENSICS LABORATORY
[TYPE] ANALYSIS REPORT

Report Number: [LAB-YYYY-####]
Case Number: [CASE-YYYY-####]
Evidence Number: [EV-###]
Analyst: [Name, PhD/Credentials]
Date Received: [Date]
Date Analyzed: [Date]
Report Date: [Date]
```

### Body Text Formatting

**Paragraph Structure:**
- Short paragraphs (3-5 sentences)
- Clear topic sentences
- Logical flow
- White space for readability

**Lists & Enumeration:**
```
EVIDENCE COLLECTED:
- Evidence #001: Firearm (.38 caliber revolver)
- Evidence #002: Bullet (recovered from victim)
- Evidence #003: Blood sample (floor near door)
- Evidence #004: Access log (digital printout)
```

**Signatures & Authentication:**
```
This report is accurate and complete to the best of my 
knowledge and ability.

_________________________
[Signature Line]
[Name]
[Title/Badge/Credentials]
[Date]
```

---

## 5.10 Writing for Different Difficulty Levels

### Easy Cases

**Writing Characteristics:**
- Clear, straightforward language
- Obvious connections between documents
- Motive stated directly
- Fewer contradictions
- Linear narrative

**Example - Easy Case Motive:**
```
Financial records show Torres owes victim $500,000. Email 
from victim dated March 10 states: "If you can't pay by 
end of month, I'll proceed with buyout." Torres had 
everything to lose.
```

### Medium Cases

**Writing Characteristics:**
- Requires connecting multiple documents
- Motive requires inference
- Some contradictions to resolve
- Red herrings more plausible

**Example - Medium Case Motive:**
```
[Document A] Bank statement shows $500k loan payment due.
[Document B] Email mentions "consequences if you don't pay."
[Document C] Partnership agreement: buyout clause exists.
[Document D] Torres interview: evasive about finances.

Player must connect: Debt → Consequences → Desperation → Motive
```

### Hard Cases

**Writing Characteristics:**
- Motive hidden in subtle details
- Multiple plausible suspects
- Strong red herrings
- Requires careful timeline analysis
- Contradictions harder to spot

**Example - Hard Case Motive:**
```
[Document A] Casual mention of "financial restructuring"
[Document B] Torres nervous when finances mentioned
[Document C] Victim's diary: "M is avoiding me"
[Document D] Legal document buried in files: buyout terms
[Forensic Report] Torres' presence at scene

Player must discover: Restructuring = Torres being forced out 
→ Financial ruin → Hidden motive
```

### Expert Cases

**Writing Characteristics:**
- Motive deeply buried
- Multiple layers of deception
- Extremely plausible red herrings
- Requires correlating many sources
- Timeline reconstruction challenging

**Example - Expert Case Motive:**
```
Motive spread across 8+ documents, never stated explicitly. 
Player must discover: Torres embezzled → Victim found out → 
Audit scheduled → Exposure imminent → Desperate act

Clues are subtle mentions in various documents that player 
must recognize and connect over hours of investigation.
```

---

## 5.11 Localization Writing Guidelines

### Writing for Translation

**DO:**
- Use clear, standard English
- Avoid idioms and slang (unless character-appropriate)
- Keep cultural references minimal
- Use universal measurements (with conversions)
- Write complete sentences

**DON'T:**
- Use wordplay that won't translate
- Include culture-specific jokes
- Use ambiguous pronouns excessively
- Write run-on sentences

**Translatable:**
```
Detective Wong entered the office at 2:00 PM. The victim 
had been dead for approximately 14 hours. Evidence 
suggested the murder occurred around 11:30 PM the previous 
night.
```

**Harder to Translate:**
```
Wong strolled in around 2-ish. The stiff had been pushing 
up daisies for half a day, give or take. Looks like 
someone clocked him around when Letterman comes on.
```

### Character Names

**Guidelines:**
- Use names that work in multiple languages
- Consider pronunciation in target languages
- Avoid names that are offensive in other cultures
- Can localize names per region if needed

**Example:**
- English: Michael Torres
- French: Michel Torres (slight adjustment)
- Portuguese: Miguel Torres (localized)
- Spanish: Miguel Torres (natural)

---

## 5.12 Quality Control Checklist

### Before Finalizing Documents

**Content:**
- [ ] Tone is consistent with document type
- [ ] Language is authentic and professional
- [ ] Clues are present but subtle
- [ ] No plot holes or contradictions (unless intentional)
- [ ] Timeline is internally consistent
- [ ] Forensic details are accurate

**Character:**
- [ ] Voice is consistent per character
- [ ] Personality comes through naturally
- [ ] Motivations are clear (to designer, hidden to player)
- [ ] Behaviors match personality

**Technical:**
- [ ] No typos or grammar errors
- [ ] Formatting is correct
- [ ] Headers are complete
- [ ] Page numbers if multi-page
- [ ] Proper signatures/authentication

**Player Experience:**
- [ ] Information is discoverable
- [ ] Nothing requires external knowledge
- [ ] Difficulty appropriate for target level
- [ ] Solution is logical and fair

---

## 5.13 Common Writing Mistakes to Avoid

### 1. Over-Explaining

❌ **BAD:**
```
Torres' fingerprints were found on the murder weapon. 
This is very important because it proves he touched the 
gun. Since the gun was used in the murder, this means 
Torres was likely involved in the crime. This is a 
critical piece of evidence that you should pay attention to.
```

✅ **GOOD:**
```
Partial fingerprint recovered from weapon grip. Comparison 
analysis shows match to right thumb of Michael Torres 
(SUSP-001). Match confidence: 85%.
```

### 2. Making Suspects Too Obvious

❌ **BAD:**
```
Q: Where were you on the night of the murder?
A: I was home. Alone. With no witnesses. And I definitely 
didn't go to the victim's office. Even though I hated him 
and needed him dead. But I didn't do it. I swear.
```

✅ **GOOD:**
```
Q: Where were you on the night of the murder?
A: I was home.
Q: Can anyone confirm that?
A: [Pauses] No. I was alone.
```

### 3. Unrealistic Forensics

❌ **BAD:**
```
The DNA analysis instantly identified the killer with 
100% certainty. The computer matched the DNA to the 
suspect's records in seconds.
```

✅ **GOOD:**
```
DNA profile obtained from evidence sample. Comparison 
against reference sample shows match at 16 STR loci. 
Statistical confidence: 99.7%. Random match probability: 
1 in 5.2 billion.
```

### 4. Breaking Character Voice

❌ **BAD (Forensic Scientist Using Casual Language):**
```
So yeah, we found this blood at the scene and it totally 
matched the suspect. Like, for sure it was his. We're 
pretty confident about it.
```

✅ **GOOD:**
```
DNA analysis of blood sample EV-004 yielded a profile 
consistent with reference sample SUSP-001 (Michael Torres). 
Match probability exceeds 99.7%.
```

### 5. Info-Dumping

❌ **BAD:**
```
Michael Torres, 38, born April 15, 1985 in San Francisco, 
attended Lincoln High School where he was an honor student, 
then went to State University for his MBA which he 
completed in 2009, after which he worked at several 
companies before co-founding TechCorp in 2018 with Robert 
Chen whom he met at a networking event in 2017...
```

✅ **GOOD:**
```
Michael Torres, 38, co-founded TechCorp with victim in 
2018. Holds MBA from State University. Minority 
shareholder (30%). Recent financial disputes with victim.
```

---

## 5.14 Writing Resources & References

### Research Sources for Authentic Writing

**Police Procedures:**
- Real police report templates (public records)
- Law enforcement training manuals
- True crime documentaries (for tone)

**Forensic Science:**
- FBI forensic handbook
- Academic forensic journals
- Crime lab websites (methodology)

**Legal Documents:**
- Court records (public access)
- Legal document templates
- Interview transcripts

**True Crime Cases:**
- Study real case files (for structure)
- Analyze how clues emerged
- Observe how evidence connects

---

## 5.15 Example: Complete Document Set Tone Analysis

### CASE-2024-001 Document Tone Breakdown

**Document 1: Police Report**
- Tone: ✅ Formal, bureaucratic, objective
- Length: ✅ 3 pages (appropriate)
- Clues: ✅ Initial evidence list, scene description
- Quality: ✅ Professional, authentic

**Document 2: Witness Statement (Security Guard)**
- Tone: ✅ Conversational but formal
- Voice: ✅ Working-class, observant
- Clues: ✅ Saw Torres enter, timeline details
- Quality: ✅ Natural, realistic

**Document 3: Suspect Interview (Torres)**
- Tone: ✅ Tense, evasive
- Voice: ✅ Educated, defensive
- Clues: ✅ Weak alibi, contradictions
- Quality: ✅ Shows guilt through behavior

**Document 4: Forensic Report (DNA)**
- Tone: ✅ Scientific, precise
- Language: ✅ Technical, quantitative
- Clues: ✅ Critical match to Torres
- Quality: ✅ Authentic methodology

**Overall:** Documents work together, each with appropriate tone, building case progressively.

---

## 5.16 Summary

**Core Writing Principles:**
1. **Authenticity** over drama (write like real documents)
2. **Show, don't tell** (present evidence objectively)
3. **Respect intelligence** (trust players to deduce)
4. **Subtle clues** (natural placement, not highlighted)

**Tone Guidelines:**
- Police reports: Formal, bureaucratic, objective
- Witness statements: Conversational but official
- Suspect interviews: Tense, showing personality
- Forensic reports: Scientific, technical, precise
- Personal docs: Emotional, informal, revealing

**Character Development:**
- Victims: Flawed humans, not perfect martyrs
- Guilty suspects: Strong motive, weak alibi, forensic connection
- Red herrings: Plausible guilt, eventual exoneration
- Personality through word choice and behavior

**Clue Placement:**
- 3-5 critical (solution impossible without)
- 5-8 important (strongly support solution)
- 8-12 supporting (context and confirmation)
- 5-10 red herrings (misdirection)

**Quality Standards:**
- No typos or errors
- Consistent voice per character
- Authentic procedural language
- Logical and fair to players

---

**Next Chapter:** [06-PROGRESSION.md](06-PROGRESSION.md) - Detective ranks and advancement

**Related Documents:**
- [04-CASE-STRUCTURE.md](04-CASE-STRUCTURE.md) - What documents contain
- [02-GAMEPLAY.md](02-GAMEPLAY.md) - How players interact with writing
- [10-CONTENT-PIPELINE.md](10-CONTENT-PIPELINE.md) - Writing workflow

---

**Revision History:**

| Date | Version | Changes | Author |
|------|---------|---------|--------|
| 2025-11-13 | 1.0 | Initial complete draft | AI Assistant |
