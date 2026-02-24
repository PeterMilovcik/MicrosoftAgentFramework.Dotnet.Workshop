# Module 04 – Workflows: Multi-Step Analysis

**Duration:** ~40 minutes  
**Goal:** Implement an explicit multi-step workflow with 4 sequential LLM calls and tool usage.

---

## Purpose

Move beyond single-turn chat to orchestrated pipelines:

- Each workflow step is a separate LLM call with its own session
- Steps are labelled and streamed to the console for observability
- The final step produces structured JSON output
- Tools (ReadFile, SearchKb) are used in the evidence-gathering step

---

## Concepts Covered

- 4-step pipeline: **Plan → Evidence → Critique → Final**
- Passing context between steps (plan feeds into evidence, etc.)
- Structured JSON output parsing with `System.Text.Json`
- Multiple `AgentSession` objects in a single workflow run
- Streaming with labels for observability

---

## Workflow Steps

| Step | Label | Description |
|------|-------|-------------|
| 1 | `[PLAN]` | Produce a plan with risk notes (3-5 bullets) |
| 2 | `[EVIDENCE]` | Use tools to gather facts from logs and KB |
| 3 | `[CRITIQUE]` | Identify gaps and potential hallucinations |
| 4 | `[FINAL]` | Produce final JSON with summary, evidence, recommendations, confidence |

---

## How to Run

```bash
./scripts/run.sh 04
dotnet run --project modules/04_Workflows_MultiStep
```

---

## Example Interaction

```
Query> Analyze build-log-01.txt and identify the likely root cause

🚀 Starting workflow...

━━━ Step: PLAN ━━━
  Producing a concise plan and risk notes...
[PLAN] ### Analysis Plan:
...

━━━ Step: EVIDENCE ━━━
  Gathering facts using available tools...
[EVIDENCE] ### Analysis Results:
...

━━━ Step: CRITIQUE ━━━
  Identifying gaps and evaluating evidence quality...
[CRITIQUE] ### Critique of the Analysis
...

━━━ Step: FINAL ━━━
  Producing final answer and structured JSON output...
[FINAL] {
   ...
}

══════════════════════════════════════════
 STRUCTURED OUTPUT (JSON)
══════════════════════════════════════════
{
  "Summary": "The build failed due to integration tests experiencing connection issues to a service believed to be a local PostgreSQL database on 127.0.0.1:5432. This was likely caused by the database service not being available during test execution, misconfigurations in the environment, or CI pipeline isolation issues.",
  "Evidence": [
    "Error in log: \u0027HttpRequestException: Connection refused (127.0.0.1:5432)\u0027 during three integration tests.",
    "344 tests passed, 3 failed affecting critical integration flow functionalities."
  ],
  "Recommendations": [
    "Verify that the PostgreSQL database is running on 127.0.0.1:5432 during test execution.",
    "Inspect and validate test environment configuration, including database connection settings and initialization scripts.",
    "Enhance the CI pipeline with pre-checks for essential services like databases before running tests.",
    "Investigate if isolated or containerized testing environments restrict communication with localhost."
  ],
  "Confidence": 0.85
}
```

---

## Exercises

1. ✏️ **Build log 01**: Query: _"Analyze build-log-01.txt and identify likely root causes"_

2. ✏️ **Build log 02**: Query: _"Analyze build-log-02.txt for test failures and coverage issues"_
   Compare the JSON output for both logs – observe different categories.

3. ✏️ **KB analysis**: Query: _"What does the KB say about handling flaky tests?"_
   The evidence step should call SearchKb.

4. ✏️ **Add a step**: Extend the workflow with a **5th step "Impact"** that estimates business impact.
   Add it between Critique and Final in `AnalysisWorkflow.cs`.
