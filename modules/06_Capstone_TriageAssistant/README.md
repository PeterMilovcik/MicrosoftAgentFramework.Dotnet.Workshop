# Module 06 - Capstone: Triage Assistant

**Duration:** ~20 minutes  
**Goal:** Combine all previous concepts into a realistic software triage application.

---

## What's New in This Module

This is the **capstone** — everything from Modules 01-05 comes together:
- **Multi-line failure report input** — real-world style
- **Prompt composition** — system + safety + triage rubric combined
- **Full guarded workflow** — plan → approval → evidence → critique → triage card
- **Dual output** — human-readable summary AND structured JSON Triage Card

---

## Prerequisites

- All Modules 01-05 concepts
- This module ties together: agents, tools, sessions, workflows, and human-in-the-loop

---

## Purpose

Apply everything learned in one cohesive application:

- Collect a failure report from the user (multi-line input)
- Select a sample log file to analyze
- Optionally query the knowledge base
- Run the full guarded workflow with human approval
- Output both a human-readable summary and a structured JSON Triage Card

---

## Key Code

**Composing multiple prompt files:**

```csharp
var systemPrompt = LoadPromptFile(Path.Combine(baseDir, "assets", "prompts", "system-base.md"));
var safetyPrompt = LoadPromptFile(Path.Combine(baseDir, "assets", "prompts", "system-safety.md"));
var triageRubric = LoadPromptFile(Path.Combine(baseDir, "assets", "prompts", "triage-rubric.md"));

// Combine all three into a single instruction set
var instructions = $"{systemPrompt}\n\n---\n\n{safetyPrompt}\n\n---\n\n{triageRubric}";

var tools = WorkshopTools.GetTools();
var agent = config.CreateAgent(instructions, tools);
```

**Parsing the Triage Card from JSON output:**

```csharp
internal sealed class TriageCard
{
    [JsonPropertyName("summary")]         public string Summary { get; set; } = "";
    [JsonPropertyName("category")]        public string Category { get; set; } = "";  // infra | product | test
    [JsonPropertyName("suspected_areas")] public List<string> SuspectedAreas { get; set; } = [];
    [JsonPropertyName("next_steps")]      public List<string> NextSteps { get; set; } = [];
    [JsonPropertyName("suggested_owner_role")] public string SuggestedOwnerRole { get; set; } = "";
    [JsonPropertyName("confidence")]      public double Confidence { get; set; }
}
```

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
./scripts/run.sh 06       # Linux/macOS
./scripts/run.ps1 06      # Windows PowerShell
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
  [1] build-log-01.txt  (AuthService - DB connection failure)
  [2] build-log-02.txt  (PaymentGateway - retry + coverage failure)
Choice [0-2]: 1

Step 3: Enter an optional KB search query (press Enter to skip):
KB query: flaky tests database

🚀 Starting triage workflow...

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

1. ✏️ **Compare logs**: Run with `build-log-01.txt` (DB failure), then with `build-log-02.txt` (retry + coverage). Compare the `category` and `suggested_owner_role` in the two Triage Cards. *(~5 min)*

2. ✏️ **Custom failure report**: Write your own failure report (a fictional one) and run it through the triage. Evaluate the quality of the output. *(~5 min)*

3. ✏️ **Refine the rubric**: Edit `assets/prompts/triage-rubric.md` to add a new field `severity` (critical/high/medium/low). Update `TriageCard.cs` in the `Common` project to include it and verify the agent produces the new field. *(~10 min)*

4. ✏️ **Save triage results**: Add functionality to save each Triage Card as a JSON file in a `.triage/` directory, similar to the session store in Module 03. *(~10 min)*

💡HINT: Prompt for GitHub Copilot:

```
In #file:TriageCard.cs in the `Common` project, add a `severity` field (critical/high/medium/low). Also update #file:Program.cs to save each Triage Card to a `.triage/` directory as a JSON file with a timestamp-based filename.
```

---

## Key Takeaways

- **Prompt composition** (system + safety + domain rubric) gives the model structured guidance
- **Triage Cards** provide a consistent, parseable output format for downstream systems
- **The rubric drives quality** — edit `triage-rubric.md` to change what the model produces
- **Everything from Modules 01-05 is used here** — this is the integration test for your learning
- Core workshop complete! Modules 07-09 explore advanced multi-agent orchestration patterns

---

🎉 **Core workshop complete!** Continue with the advanced modules:

**[Next: Module 07 — Group Chat Orchestration →](../07_GroupChat_Orchestration/README.md)**
