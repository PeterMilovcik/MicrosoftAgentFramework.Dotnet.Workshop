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
[PLAN] I'll analyze the build log to identify what failed.
       Plan:
       • Read build-log-01.txt to examine error messages
       • Search KB for relevant guidelines
       ...

━━━ Step: EVIDENCE ━━━
[EVIDENCE] Reading build-log-01.txt...
           Key findings:
           • 3 tests failed with "Connection refused (127.0.0.1:5432)"
           • Suggests PostgreSQL is not running in the test environment
           ...

━━━ Step: CRITIQUE ━━━
[CRITIQUE] Evidence is clear. Missing: confirmation of DB configuration.
           Recommendation: also check docker-compose or test setup scripts.

━━━ Step: FINAL ━━━
[FINAL] { "summary": "Three integration tests failed because..." }

══════════════════════════════════════════
 STRUCTURED OUTPUT (JSON)
══════════════════════════════════════════
{
  "summary": "Three integration tests failed because the PostgreSQL database...",
  "evidence": ["Connection refused on port 5432", "3 test failures"],
  "recommendations": ["Start PostgreSQL before running integration tests", "..."],
  "confidence": 0.92
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
