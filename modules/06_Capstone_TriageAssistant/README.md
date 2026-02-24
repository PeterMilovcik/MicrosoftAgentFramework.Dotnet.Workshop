# Module 06 - Capstone: Triage Assistant

**Duration:** ~20 minutes  
**Goal:** Combine all previous concepts into a realistic software triage application.

---

## Purpose

Apply everything learned in one cohesive application:

- Collect a failure report from the user (multi-line input)
- Select a sample log file to analyze
- Optionally query the knowledge base
- Run the full guarded workflow with human approval
- Output both a human-readable summary and a structured JSON Triage Card

---

## Triage Card Schema

```json
{
  "summary": "Plain-language failure summary for non-technical stakeholders",
  "category": "infra | product | test",
  "suspected_areas": ["AuthService", "PostgreSQL"],
  "next_steps": [
    "Step 1: Verify PostgreSQL is running in the test environment",
    "Step 2: Check docker-compose.yml for missing service definition"
  ],
  "suggested_owner_role": "ops",
  "confidence": 0.87
}
```

---

## Prompts Used

| Prompt File | Purpose |
|-------------|---------|
| `system-base.md` | Core assistant behavior |
| `system-safety.md` | Tool-use constraints |
| `triage-rubric.md` | Triage Card schema and quality criteria |

---

## How to Run

```bash
./scripts/run.sh 06
dotnet run --project modules/06_Capstone_TriageAssistant
```

---

## Example Session

```
Step 1: Paste your failure report (end with a line containing only 'END'):
Three integration tests failed in the AuthService build pipeline.
All failures show "Connection refused (127.0.0.1:5432)".
END

Step 2: Select a sample log file to analyze:
  [0] None
  [1] build-log-01.txt  (AuthService - DB connection failure)
  [2] build-log-02.txt  (PaymentGateway - retry + coverage failure)
Choice [0-2]: 1
Selected: build-log-01.txt

Step 3: Enter an optional KB search query (press Enter to skip):
KB query: flaky tests database

🚀 Starting triage workflow...

━━━ Step: PLAN ━━━
[PLAN] Analysis plan: ...

━━━ 🔐 Human Approval Gate ━━━
Decision: approve

[... evidence, critique, final ...]

══════════════════════════════════════════
 HUMAN SUMMARY
══════════════════════════════════════════
Three integration tests in AuthService failed because the PostgreSQL database
was not reachable at 127.0.0.1:5432 during the test run.

══════════════════════════════════════════
 TRIAGE CARD (JSON)
══════════════════════════════════════════
{
  "summary": "Three integration tests failed...",
  "category": "infra",
  "suspected_areas": ["PostgreSQL", "Test environment setup"],
  "next_steps": ["Step 1: Start PostgreSQL...", "Step 2: ..."],
  "suggested_owner_role": "ops",
  "confidence": 0.91
}
```

---

## Exercises

1. ✏️ **Compare logs**: Run the capstone with `build-log-01.txt` (DB failure), then again with `build-log-02.txt` (retry + coverage). Compare the `category` and `suggested_owner_role` in the two Triage Cards.

2. ✏️ **Custom failure report**: Write your own failure report (a recent real incident or a fictional one) and run it through the triage. Evaluate the quality of the output.

3. ✏️ **Refine the rubric**: Edit `assets/prompts/triage-rubric.md` to add a new field `severity` (critical/high/medium/low). Update `TriageCard.cs` to include it and verify the agent produces the new field.

4. ✏️ **Save triage results**: Add functionality to save each Triage Card as a JSON file in a `.triage/` directory, similar to the session store in Module 03.
