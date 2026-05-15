# Manual Smoke Runbook — CaseZero v2

> **Branch:** `feat/site-contract-v2`  
> **Target spec:** `docs/CASE_JSON_V2_SPEC.md`  
> **Reference case:** `cases/case_001/case.json` — *"The Missing Heir"*

---

## Prerequisites

| Tool | Version |
|------|---------|
| .NET SDK | 9.0+ (backend uses net9.0) |
| Node.js | 20+ |
| Docker | 24+ (for Azurite + SQL Server) |

### Infrastructure (local dev)

```bash
# From repo root — starts Azurite (blob/queue) and SQL Server
docker compose up -d
```

For **production / CI**, set the following environment variables instead of running Docker:

| Variable | Description |
|----------|-------------|
| `ConnectionStrings__DefaultConnection` | Azure SQL connection string |
| `AzureStorageConnectionString` | Azure Blob Storage connection string |

---

## Step 1 — Start the API

```bash
cd backend/CaseZeroApi
dotnet run
```

Verify: open **http://localhost:5001/swagger** — the Swagger UI should load with all v2 endpoints visible.

Expected endpoints include:
- `GET /api/v2/cases/dashboard`
- `GET /api/v2/cases/{caseId}`
- `POST /api/v2/cases/{caseId}/submit`
- `POST /api/v2/cases/{caseId}/forensics`

---

## Step 2 — Start the Frontend

```bash
cd frontend
npm install
npm run dev
```

Open **http://localhost:5173** — the CaseZero desktop UI should render.

---

## Step 3 — Login

On the login screen, enter:

| Field | Value |
|-------|-------|
| Email | `john.doe@fic-police.gov` |
| Password | `Password123!` |

> **Note:** This is the seed user. If data seeding hasn't run, register a new account through the UI first.

---

## Step 4 — Dashboard

After login, the **Dashboard** should list at least one case:

- **ID:** `case_001`
- **Title:** *The Missing Heir*
- **Difficulty:** Detective
- **Estimated duration:** ~120 min

Click **"Open Case"** (or equivalent) to load the case.

---

## Step 5 — Initial Email Visible

Inside the case, open the **Inbox** tab. Verify the email titled:

> **URGENT: Missing Person Case Assignment**

is present and readable. This email has `visibility: "initial"` and should appear immediately.

---

## Step 6 — Asset View Trigger

Open the **FileViewer** (or Assets panel). Click on the asset:

> **`asset.phone_data`** — *Phone Data*

This fires the `asset_viewed` trigger. In the background the rules engine evaluates case rules for this trigger.

**Expected:** no error; the asset detail/viewer opens.

---

## Step 7 — Submit Forensic Analysis

Navigate to the **Forensics** panel (or use the in-case forensics button).  
Submit a forensic analysis:

| Field | Value |
|-------|-------|
| Asset | `asset.phone_data` |
| Analysis Type | `DigitalForensics` |

Click **Submit** / **Queue Analysis**.

**Expected:** The forensic request is accepted. The analysis is queued.

---

## Step 8 — Lab Email Appears After Game Time

The rules engine fires the `forensics_complete` trigger once the analysis completes.  
In real game time this takes ~6 h; for dev testing use the **fast-forward** endpoint if exposed:

```bash
# Fast-forward game clock (if dev endpoint is available)
curl -X POST http://localhost:5001/api/dev/game-time \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"caseId":"case_001","gameTimeMinutes":400}'
```

**Expected:** After the trigger fires, a new email appears in the Inbox:

- Subject contains **"Lab Report"** or **"Forensic Analysis"**
- The asset `asset.phone_forensics_report` is now visible in the Assets panel

---

## Step 9 — Submit Case Solution

Open the **⚖️ Submit Case** panel.

Fill in the form as follows:

| Field | Value |
|-------|-------|
| **Primary Suspect** | `Marcus Reeve` |
| **Evidence** | ✓ `Phone Data`, ✓ `Phone Forensics Report` |
| **Forensic Analyses** | `asset.phone_data:DigitalForensics` |
| **Question — Motive** | `Blackmail` |
| **Question — Location** | `1247 Riverside Drive` |
| **Question — Device** | `burner` (or `Burner Phone`) |

Click **Submit Case**.

---

## Step 10 — Verify Correct Result

**Expected response:**

```json
{
  "correct": true,
  "score": 0.9,
  "breakdown": {
    "culprit":   true,
    "evidence":  true,
    "analysis":  true,
    "questions": true
  },
  "attemptsRemaining": 2,
  "feedbackText": "Case solved. Strong reasoning.",
  "explanationMarkdown": "Marcus Reeve orchestrated the disappearance..."
}
```

Acceptance criteria:

- [ ] `correct == true`
- [ ] `score >= 0.7`
- [ ] Explanation text is rendered in the UI below the result panel

---

## Troubleshooting

| Symptom | Likely Cause | Fix |
|---------|-------------|-----|
| Swagger not loading | API not started / port conflict | Check `dotnet run` output; try port 5000 |
| `401 Unauthorized` on API calls | JWT token expired | Log out and log in again |
| Case not on dashboard | Data seeding didn't run | Call `POST /api/dev/seed` or restart API with `SEED_ON_STARTUP=true` |
| Lab email missing after forensics | Game time not advanced | Use fast-forward endpoint or wait |
| `submit` returns `score < 0.7` | Wrong culprit or missing evidence | Ensure Marcus Reeve + both assets selected |
| Docker containers not starting | Port conflict | `docker compose down` then `docker compose up -d` |
