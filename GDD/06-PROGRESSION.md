# Chapter 06 - Progression & Advancement

**Game Design Document - CaseZero v3.0**  
**Last Updated:** November 13, 2025  
**Status:** ✅ Complete

---

## 6.1 Overview

This chapter defines the **detective rank progression system** - how players advance through their career, unlock new cases, and track their investigative mastery. The system is designed to feel like genuine career progression rather than gamified leveling.

**Key Concepts:**
- 8 detective ranks (Rookie → Master Detective)
- XP earned from solving cases
- Rank-based case unlocking
- Performance tracking and statistics
- No mechanical advantages (only content access)
- Long-term mastery goals

---

## 6.2 Philosophy of Progression

### What Progression IS

**Career Advancement:**
- Reflects growing expertise
- Unlocks more complex cases
- Tracks investigative achievements
- Shows mastery over time

**Recognition System:**
- Acknowledges player skill
- Provides sense of growth
- Creates long-term goals
- Encourages thorough investigation

### What Progression is NOT

**No Power Creep:**
- ❌ No faster forensics at higher ranks
- ❌ No extra submission attempts
- ❌ No hints or assistance unlocked
- ❌ No easier evidence discovery

**No Manipulation:**
- ❌ No daily login bonuses
- ❌ No "energy" systems
- ❌ No time-gated progression
- ❌ No pressure to advance quickly

**Core Principle:** Ranks measure skill, not time investment or payment.

---

## 6.3 Detective Rank Structure

### The 8 Ranks

```
8. MASTER DETECTIVE      [18,000+ XP]     █████████████████████
                                          Legendary (Top 1%)

7. VETERAN DETECTIVE     [12,000-18,000]  ████████████████░░░░░
                                          Elite

6. LEAD DETECTIVE        [8,000-12,000]   ████████████░░░░░░░░░
                                          Expert

5. SENIOR DETECTIVE      [5,000-8,000]    ████████░░░░░░░░░░░░░
                                          Highly Skilled

4. DETECTIVE I           [3,000-5,000]    ██████░░░░░░░░░░░░░░░
                                          Experienced

3. DETECTIVE II          [1,500-3,000]    ████░░░░░░░░░░░░░░░░░
                                          Competent

2. DETECTIVE III         [500-1,500]      ██░░░░░░░░░░░░░░░░░░░
                                          Developing

1. ROOKIE                [0-500]          █░░░░░░░░░░░░░░░░░░░░
                                          Beginner
```

### Rank Descriptions

#### 1. Rookie (0-500 XP)

**Description:** Newly assigned to the Cold Case Division. Still learning the fundamentals of investigation.

**Access:**
- Tutorial case (training)
- Easy cases (2-3 suspects)
- 3-5 total cases available

**Profile Badge:** 🔰 Rookie Detective  
**Estimated Time to Rank Up:** 2-3 hours (1-2 easy cases)

**Player at This Stage:**
- Learning investigation mechanics
- Understanding document reading
- First exposure to forensics
- Building confidence

---

#### 2. Detective III (500-1,500 XP)

**Description:** Completed first real case. Shows basic competence in investigation fundamentals.

**Access:**
- All Easy cases unlocked
- Medium cases unlocked (4-5 suspects)
- 8-12 total cases available

**Profile Badge:** 👮 Detective III  
**Estimated Time to Rank Up:** 8-12 hours (2-3 medium cases)

**Player at This Stage:**
- Comfortable with mechanics
- Can solve straightforward cases
- Starting to handle complexity
- Developing analytical skills

---

#### 3. Detective II (1,500-3,000 XP)

**Description:** Proven investigator with multiple solved cases. Handles medium-complexity investigations reliably.

**Access:**
- All Easy & Medium cases
- 15-20 total cases available

**Profile Badge:** 👮‍♂️ Detective II  
**Estimated Time to Rank Up:** 15-20 hours (3-4 medium cases, maybe 1 hard)

**Player at This Stage:**
- Confident investigator
- Rarely fails easy cases
- 50%+ first-attempt success on medium
- Ready for harder challenges

---

#### 4. Detective I (3,000-5,000 XP)

**Description:** Experienced detective. Tackles complex cases with strong analytical skills.

**Access:**
- All Easy, Medium cases
- Hard cases unlocked (6-7 suspects)
- 25-30 total cases available

**Profile Badge:** 🕵️ Detective I  
**Estimated Time to Rank Up:** 20-30 hours (4-5 hard cases)

**Player at This Stage:**
- Masters medium cases
- Comfortable with complexity
- Developing expertise
- Can handle ambiguity

---

#### 5. Senior Detective (5,000-8,000 XP)

**Description:** Highly skilled investigator. Reputation for solving difficult cases.

**Access:**
- All Easy, Medium, Hard cases
- 35-40 total cases available

**Profile Badge:** 🎖️ Senior Detective  
**Estimated Time to Rank Up:** 30-40 hours (5-7 hard cases)

**Player at This Stage:**
- Consistent performance
- High success rates
- Rarely stumped
- Developing mastery

---

#### 6. Lead Detective (8,000-12,000 XP)

**Description:** Expert investigator. Handles the most complex cold cases with excellence.

**Access:**
- All Easy, Medium, Hard cases
- Expert cases unlocked (8+ suspects)
- 45-50 total cases available

**Profile Badge:** ⭐ Lead Detective  
**Estimated Time to Rank Up:** 40-60 hours (6-8 expert cases)

**Player at This Stage:**
- Expert-level skills
- Solves most cases first try
- Tackles expert difficulty
- Top tier investigator

---

#### 7. Veteran Detective (12,000-18,000 XP)

**Description:** Elite investigator. Among the best in the division.

**Access:**
- All cases unlocked
- Rare/special cases (if available)
- 55-60+ total cases available

**Profile Badge:** 🏅 Veteran Detective  
**Estimated Time to Rank Up:** 60-90 hours (8-12 expert cases)

**Player at This Stage:**
- Elite performance
- High first-attempt success even on expert
- Deep mastery of investigation
- Respected by community

---

#### 8. Master Detective (18,000+ XP)

**Description:** Legendary status. The highest rank achievable. Fewer than 1% of players reach this level.

**Access:**
- All content unlocked
- Exclusive "Master" badge
- Profile showcase

**Profile Badge:** 👑 Master Detective  
**Achievement:** Permanent (no higher rank)

**Player at This Stage:**
- Complete mastery
- Likely 80%+ first-attempt success
- Can solve any case
- Top 1% of players

---

## 6.4 Experience Points (XP) System

### XP Award Formula

**Base XP by Difficulty:**

```javascript
const baseXP = {
  Easy: 150,
  Medium: 300,
  Hard: 600,
  Expert: 1200
};
```

**Modifiers:**

```javascript
function calculateXP(difficulty, attempt, bonuses) {
  let xp = baseXP[difficulty];
  
  // Attempt penalty
  if (attempt === 2) {
    xp *= 0.75; // -25%
  } else if (attempt === 3) {
    xp *= 0.50; // -50%
  } else if (attempt > 3) {
    xp = 0; // Failed case
  }
  
  // Bonuses
  if (bonuses.firstAttempt) {
    xp *= 1.5; // +50% for first-try solve
  }
  
  if (bonuses.noForensics) {
    xp *= 1.25; // +25% for solving without forensics (rare)
  }
  
  if (bonuses.quickSolve) {
    xp *= 1.1; // +10% for solving in under 2 hours
  }
  
  if (bonuses.thoroughExplanation) {
    xp *= 1.1; // +10% for detailed solution explanation
  }
  
  return Math.floor(xp);
}
```

### XP Examples

**Easy Case (150 base XP):**
- First attempt, quick: 150 × 1.5 × 1.1 = **248 XP**
- Second attempt: 150 × 0.75 = **113 XP**
- Third attempt: 150 × 0.5 = **75 XP**

**Medium Case (300 base XP):**
- First attempt: 300 × 1.5 = **450 XP**
- First attempt + thorough explanation: 300 × 1.5 × 1.1 = **495 XP**
- Second attempt: 300 × 0.75 = **225 XP**
- Third attempt: 300 × 0.5 = **150 XP**

**Hard Case (600 base XP):**
- First attempt: 600 × 1.5 = **900 XP**
- First attempt + all bonuses: 600 × 1.5 × 1.1 × 1.1 = **1089 XP**
- Second attempt: 600 × 0.75 = **450 XP**
- Third attempt: 600 × 0.5 = **300 XP**

**Expert Case (1200 base XP):**
- First attempt: 1200 × 1.5 = **1800 XP**
- First attempt + bonuses: 1200 × 1.5 × 1.1 × 1.1 = **2178 XP**
- Second attempt: 1200 × 0.75 = **900 XP**
- Third attempt: 1200 × 0.5 = **600 XP**

### XP Requirements Per Rank

| Rank | XP Required | Cumulative XP | Easy Cases Equivalent | Expert Cases Equivalent |
|------|-------------|---------------|----------------------|------------------------|
| Rookie → Detective III | 500 | 500 | 2-3 cases | 1 case |
| Detective III → Detective II | 1,000 | 1,500 | 4-5 cases | 1-2 cases |
| Detective II → Detective I | 1,500 | 3,000 | 6-7 cases | 2-3 cases |
| Detective I → Senior | 2,000 | 5,000 | 8-10 cases | 3-4 cases |
| Senior → Lead | 3,000 | 8,000 | 12-15 cases | 5-6 cases |
| Lead → Veteran | 4,000 | 12,000 | 18-20 cases | 7-9 cases |
| Veteran → Master | 6,000 | 18,000 | 25-30 cases | 10-12 cases |

**Total to Master:** 18,000 XP (~60-120 hours gameplay, 40-80 cases)

---

## 6.5 Case Unlocking System

### Unlock Rules

**Difficulty Gating:**

```javascript
const caseUnlockRequirements = {
  Easy: "Rookie", // Rank 1+
  Medium: "Detective III", // Rank 2+
  Hard: "Detective I", // Rank 4+
  Expert: "Lead Detective" // Rank 6+
};
```

**Progressive Unlock:**
- All Easy cases available at Rookie
- Medium unlocks at Detective III (after first case solved)
- Hard unlocks at Detective I (after proving competence)
- Expert unlocks at Lead Detective (elite content)

### Why Gate Difficulty?

**Prevents Frustration:**
- New players won't face expert cases immediately
- Ensures skill development before harder content
- Creates sense of progression

**Maintains Challenge:**
- Players always have appropriate difficulty available
- Harder cases feel like earned achievements
- Prevents burnout from too-hard content

**Doesn't Block Content:**
- Can still play easier cases at high ranks
- No "out-leveling" content
- All cases remain relevant

---

## 6.6 Progression Rewards

### What You Get from Ranking Up

**1. Content Access (Primary Reward)**
- New difficulty tier unlocked
- More cases available
- Rare/special cases (at highest ranks)

**2. Profile Badge**
- Visual rank indicator
- Shown in profile
- Community recognition

**3. Title**
- "Rookie Detective"
- "Detective III/II/I"
- "Senior Detective"
- "Lead Detective"
- "Veteran Detective"
- "Master Detective"

**4. Statistics Unlock**
- More detailed stats visible
- Case history
- Success rates
- Comparison to rank averages

### What You DON'T Get

**No Mechanical Advantages:**
- ❌ Forensics don't complete faster
- ❌ No extra submission attempts
- ❌ No hints or clue highlighting
- ❌ No "easier" versions of cases

**No Cosmetics (Kept Simple):**
- ❌ No avatar customization
- ❌ No office decoration
- ❌ No profile themes

**Philosophy:** Progression is about skill recognition, not power or vanity.

---

## 6.7 Statistics & Performance Tracking

### Core Statistics

**Overall Stats:**
```
┌─────────────────────────────────────────────┐
│ DETECTIVE PROFILE                           │
├─────────────────────────────────────────────┤
│ Rank: Lead Detective ⭐                     │
│ XP: 9,450 / 12,000                          │
│                                             │
│ CAREER STATISTICS:                          │
│ Cases Solved: 28                            │
│ Cases Failed: 3                             │
│ Cases Active: 2                             │
│ Success Rate: 90.3%                         │
│                                             │
│ First-Attempt Success: 42.9%                │
│ Average Attempts: 1.6                       │
│ Total Investigation Time: 87.5 hours        │
│ Average Time Per Case: 3.1 hours            │
│                                             │
│ BY DIFFICULTY:                              │
│ Easy:   12 solved, 100% success             │
│ Medium: 10 solved, 90% success              │
│ Hard:   5 solved, 80% success               │
│ Expert: 1 solved, 50% success               │
└─────────────────────────────────────────────┘
```

**Per-Case Stats:**
```
CASE-2024-001: The Downtown Office Murder
Status: ✓ Solved (First Attempt)
Time Spent: 4.5 hours
Attempts: 1/3
XP Earned: 450 (+50% bonus)
Solved: March 18, 2025
```

### Tracked Metrics

**Performance:**
- Total cases solved
- Cases failed (exhausted attempts)
- Success rate (%)
- First-attempt success rate (%)
- Average attempts per case
- Average time per case

**By Difficulty:**
- Cases solved per difficulty
- Success rate per difficulty
- XP earned per difficulty

**Special Achievements (Hidden):**
- Solved without forensics
- Solved in under 2 hours
- Perfect explanation (high quality)
- Solved expert case first attempt

### What's NOT Tracked

**Privacy Respecting:**
- ❌ Individual document reading time (too invasive)
- ❌ Mouse movement patterns
- ❌ Number of times evidence viewed
- ❌ Note content (private)

**Philosophy:** Track outcomes, not behaviors.

---

## 6.8 Rank Progression Pacing

### Time Investment Per Rank

**Estimated Hours (Skilled Player):**

| Rank | Hours to Achieve | Cumulative Hours | Cases Required |
|------|------------------|------------------|----------------|
| Rookie | 0 | 0 | 0 (starting rank) |
| Detective III | 2-4 | 2-4 | 2-3 easy |
| Detective II | 8-12 | 10-16 | +3-4 medium |
| Detective I | 15-20 | 25-36 | +4-5 medium/hard |
| Senior Detective | 20-30 | 45-66 | +5-7 hard |
| Lead Detective | 30-40 | 75-106 | +6-8 hard |
| Veteran Detective | 40-60 | 115-166 | +8-12 expert |
| Master Detective | 60-90 | 175-256 | +12-18 expert |

**Total to Master:** 175-256 hours (varies by skill and difficulty chosen)

### Progression Curve Design

**Early Ranks (1-3): Fast**
- Quick wins build confidence
- 2-4 hours per rank
- Feels rewarding
- Keeps new players engaged

**Mid Ranks (4-6): Moderate**
- 20-40 hours per rank
- Meaningful progression
- Matches growing skill
- Sustainable pace

**Late Ranks (7-8): Slow**
- 40-90 hours per rank
- Elite achievement
- Long-term goals
- Top 1% players only

**Philosophy:** Fast start, steady middle, long tail for mastery.

---

## 6.9 Failed Cases & Retry System

### Failed Case Rules

**When You Fail:**
- Exhausted 3 submission attempts
- Case marked as "Unsolved (Reviewed)"
- Can view correct solution
- Earn 0 XP
- Case remains accessible

**Retry Requirements:**
- Must solve 2 other cases before retry
- Can retry unlimited times (after solving 2 others)
- Fresh attempts (resets to 3)
- Can earn full XP on retry

### Why This System?

**Encourages Moving On:**
- Don't get stuck on one case
- Experience other content
- Learn from variety
- Prevents frustration lock

**Allows Learning:**
- See solution after failure
- Understand what you missed
- Apply lessons to other cases
- Return with fresh perspective

**No Permanent Penalty:**
- Can eventually solve everything
- No "lost" content
- XP catch-up possible
- Completionist-friendly

### Example Flow:

```
1. Try CASE-2024-001 (Hard)
   → Fail after 3 attempts (0 XP)
   → View solution
   
2. Solve CASE-2024-002 (Medium)
   → Success! (+300 XP)
   
3. Solve CASE-2024-003 (Medium)
   → Success! (+300 XP)
   
4. Retry CASE-2024-001 (Hard)
   → Success on first retry! (+900 XP)
   → Full XP awarded
```

---

## 6.10 Comparative Performance

### Rank Averages (Benchmarks)

**Detective III (Rank 2):**
- Success Rate: 60-70%
- First-Attempt: 30-40%
- Avg Time: 4-5 hours/case

**Detective I (Rank 4):**
- Success Rate: 75-85%
- First-Attempt: 40-50%
- Avg Time: 3-4 hours/case

**Lead Detective (Rank 6):**
- Success Rate: 85-95%
- First-Attempt: 50-65%
- Avg Time: 3-4 hours/case

**Master Detective (Rank 8):**
- Success Rate: 90-98%
- First-Attempt: 65-80%
- Avg Time: 2.5-3.5 hours/case

### Performance Comparison (Optional Display)

**In Player Profile:**
```
YOUR PERFORMANCE vs. RANK AVERAGE:

First-Attempt Success:
You: 42%  ▓▓▓▓▓▓▓▓░░░░
Avg: 50%  ▓▓▓▓▓▓▓▓▓▓░░

Success Rate:
You: 90%  ▓▓▓▓▓▓▓▓▓░░
Avg: 87%  ▓▓▓▓▓▓▓▓▓░░

You're performing above average for your rank!
```

**Philosophy:** Optional, non-competitive comparison. Helps players gauge their skill.

---

## 6.11 Special Achievements (Hidden)

### Secret Badges

**"Sharp Detective" 🧠**
- Solve 5 cases on first attempt
- Reward: Profile badge

**"Patient Investigator" ⏳**
- Solve a case without accelerating forensics
- Reward: Profile badge

**"Intuitive Mind" 💡**
- Solve a case without requesting any forensics (rare)
- Reward: Profile badge + bonus XP

**"Speed Reader" ⚡**
- Solve a medium case in under 2 hours
- Reward: Profile badge

**"Master Analyst" 🎯**
- Solve an expert case on first attempt
- Reward: Profile badge + respect

**"Cold Case Expert" 📁**
- Solve 50 cases total
- Reward: Title unlock

**"Legendary Detective" 👑**
- Reach Master rank
- Reward: Permanent title + community showcase

**Philosophy:** Hidden achievements reward excellence without pressure.

---

## 6.12 Rank Reset & Prestige (Not Planned)

### Why No Prestige System?

**Arguments Against:**
1. **No mechanical changes:** Since ranks don't give power, prestige is meaningless
2. **Content access loss:** Would lock players out of hard cases they earned
3. **Artificial grind:** Extends playtime artificially, not through content
4. **Premium model:** Players paid for content, shouldn't re-grind

**Philosophy:** Mastery is the end goal, not infinite grinding.

**Alternative:** New case DLC packs extend content without resetting progress.

---

## 6.13 Seasonal Content (Future Consideration)

### Seasonal Cases (Post-Launch)

**Concept:**
- Limited-time special cases (1-2 months)
- Available to all ranks (no gating)
- Unique themes (holidays, historical)
- Earn special badges
- Cases archived but replayable after season

**Example:**
```
WINTER SEASON 2025: "The Holiday Heist"
Duration: Dec 1 - Jan 31
Cases: 3 new cases (Easy, Medium, Hard)
Theme: Holiday-themed crimes
Reward: Seasonal badge
```

**Why Seasonal?**
- Keeps community engaged
- Fresh content regularly
- Optional (doesn't gate base game)
- Replayable after season ends

**Note:** Only if successful at launch. Not core to MVP.

---

## 6.14 Profile & Identity

### Public Profile Display

**What's Shown:**
```
┌─────────────────────────────────────────────┐
│ Detective Profile                           │
├─────────────────────────────────────────────┤
│ Username: Alex_Martinez                     │
│ Rank: Lead Detective ⭐                     │
│ XP: 9,450 / 12,000                          │
│                                             │
│ Joined: January 15, 2025                    │
│ Cases Solved: 28                            │
│ Success Rate: 90.3%                         │
│                                             │
│ BADGES:                                     │
│ 🧠 Sharp Detective                          │
│ 💡 Intuitive Mind                           │
│ ⚡ Speed Reader                             │
│                                             │
│ RECENT CASES:                               │
│ ✓ CASE-2024-015 (Expert) - 1st attempt     │
│ ✓ CASE-2024-014 (Hard) - 2nd attempt       │
│ ✓ CASE-2024-013 (Hard) - 1st attempt       │
└─────────────────────────────────────────────┘
```

### Privacy Settings

**What Players Can Hide:**
- Success rate
- Total cases solved
- Recent case history
- Badges earned

**What's Always Visible:**
- Username
- Current rank
- Joined date

**Philosophy:** Social features are opt-in, not forced.

---

## 6.15 Onboarding & Tutorial XP

### Tutorial Case

**Training Case:**
- Simple theft case (1 suspect, obvious solution)
- 5 documents, 2 evidence
- Takes 15-20 minutes
- Awards **50 XP** (10% toward Detective III)

**Purpose:**
- Teaches mechanics
- Gives first success
- Builds confidence
- Quick win

### First Real Case Bonus

**First Case Solved:**
- Extra +100 XP bonus (one-time)
- Total: ~250-350 XP (Easy case + bonus)
- Gets player 50-70% toward Detective III
- Encourages continued play

---

## 6.16 Edge Cases & Special Scenarios

### Solving Higher Difficulty Early

**Scenario:** Rookie player solves Hard case

**Handling:**
- ✅ Full XP awarded (900+ for first attempt)
- ✅ Might jump multiple ranks
- ✅ Unlocks appropriate content
- 🎖️ Special recognition ("Overachiever" badge)

**Philosophy:** Reward skill, don't artificially cap it.

### Grinding Lower Difficulty

**Scenario:** Lead Detective repeatedly solves Easy cases

**Handling:**
- ✅ Still awards XP (150 base)
- ✅ No reduction or penalty
- 📊 Stats track difficulty distribution
- 💭 Community might notice (optional leaderboard shows difficulty mix)

**Philosophy:** Players can play however they want, but stats show skill level.

### Perfect Record

**Scenario:** Player solves every case first attempt

**Handling:**
- 🏆 "Perfect Detective" achievement
- 👑 Special badge/title
- 📈 Top leaderboard position (if implemented)
- 🎉 Community recognition

**Philosophy:** Recognize excellence publicly.

---

## 6.17 Progression Transparency

### Clear Communication

**In-Game Display:**
```
┌─────────────────────────────────────────────┐
│ RANK PROGRESS                               │
├─────────────────────────────────────────────┤
│ Current: Lead Detective ⭐                  │
│ Next: Veteran Detective 🏅                  │
│                                             │
│ XP: 9,450 / 12,000                          │
│ Progress: ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓░░░░ 79%          │
│                                             │
│ XP to Next Rank: 2,550                      │
│ ~3-4 more Hard cases, or                    │
│ ~2 Expert cases                             │
│                                             │
│ [View Full Rank Structure]                  │
└─────────────────────────────────────────────┘
```

**After Case:**
```
┌─────────────────────────────────────────────┐
│ CASE SOLVED! ✓                              │
├─────────────────────────────────────────────┤
│ CASE-2024-014: The Harbor Conspiracy        │
│ Difficulty: Hard                            │
│ Attempts: 1/3 (First Try!)                  │
│                                             │
│ XP EARNED:                                  │
│ Base XP:        600                         │
│ First Attempt:  +300 (50% bonus)            │
│ Quick Solve:    +90 (10% bonus)             │
│ ─────────────────                           │
│ Total XP:       990                         │
│                                             │
│ Rank Progress: 9,450 → 10,440 / 12,000     │
│ [+990 XP]  ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓░░░ 87%         │
│                                             │
│ 1,560 XP until Veteran Detective!           │
└─────────────────────────────────────────────┘
```

---

## 6.18 Summary

**Progression System:**
- **8 Ranks:** Rookie → Master Detective
- **XP Earnings:** 150 (Easy) to 1200+ (Expert) per case
- **Total to Max:** 18,000 XP (~175-256 hours, 40-80 cases)
- **Unlocks:** Higher difficulty cases at ranks 2, 4, 6

**Core Principles:**
- 🎯 **Skill-based:** Ranks reflect mastery, not time
- 🚫 **No power creep:** Only content access, no mechanical advantage
- 📈 **Transparent:** Clear XP requirements and progress
- ⏱️ **Respectful:** No manipulation, grinding, or pressure

**Rewards:**
- Content access (new difficulty tiers)
- Profile badges and titles
- Performance statistics
- Community recognition

**Philosophy:** Progression recognizes and celebrates investigative skill while respecting player time and intelligence.

---

**Next Chapter:** [07-USER-INTERFACE.md](07-USER-INTERFACE.md) - Desktop metaphor and UI design

**Related Documents:**
- [02-GAMEPLAY.md](02-GAMEPLAY.md) - How progression integrates with gameplay
- [04-CASE-STRUCTURE.md](04-CASE-STRUCTURE.md) - Case difficulty factors
- [11-TESTING.md](11-TESTING.md) - Progression balance testing

---

**Revision History:**

| Date | Version | Changes | Author |
|------|---------|---------|--------|
| 2025-11-13 | 1.0 | Initial complete draft | AI Assistant |
