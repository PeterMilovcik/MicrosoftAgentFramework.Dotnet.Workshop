# Module 04 - Workflows: Multi-Step Analysis

**Duration:** ~40 minutes  
**Goal:** Implement an explicit multi-step workflow with 4 sequential LLM calls and tool usage.

---

## What's New in This Module

Moving from single-turn chat to **orchestrated pipelines**:
- **4 sequential LLM calls** — each with a distinct role (plan, evidence, critique, final)
- **Inter-step context passing** — output from one step feeds into the next
- **Structured JSON output** — the final step produces parseable JSON
- **Labelled streaming** — each step is clearly marked in the console

---

## Prerequisites

- Module 02 concepts (tools and function calling)
- Module 01 concepts (sessions, streaming)

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

## Key Code

**Each workflow step creates its own session and includes context from previous steps:**

```csharp
// Step 2 receives the plan from Step 1
var evidenceSession = await agent.CreateSessionAsync(ct);
var evidencePrompt = $"""
    Based on this plan:
    {planResult}

    Gather evidence for: {userQuery}
    Use ReadFile and SearchKb as appropriate.
    """;
var evidenceResult = await RunAndCollectAsync(agent, evidencePrompt, evidenceSession, "EVIDENCE", ct);
```

**Streaming with labels for observability:**

```csharp
private static async Task<string> RunAndCollectAsync(
    AIAgent agent, string prompt, AgentSession session, string label, CancellationToken ct)
{
    var sb = new StringBuilder();
    Console.Write($"[{label}] ");

    await foreach (var update in agent.RunStreamingAsync(prompt, session).WithCancellation(ct))
    {
        Console.Write(update.Text);
        sb.Append(update.Text);
    }
    return sb.ToString();
}
```

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
./scripts/run.sh 04       # Linux/macOS
./scripts/run.ps1 04      # Windows PowerShell
dotnet run --project modules/04_Workflows_MultiStep
```

---

## Example Interaction

```
Query> Analyze build-log-01.txt and identify the likely root cause

🚀 Starting workflow...

━━━ Step: PLAN ━━━
[PLAN] ### Analysis Plan: ...

━━━ Step: EVIDENCE ━━━
[EVIDENCE] ### Analysis Results: ...

━━━ Step: CRITIQUE ━━━
[CRITIQUE] ### Critique of the Analysis ...

━━━ Step: FINAL ━━━
[FINAL] { ... }

══════════════════════════════════════════
 STRUCTURED OUTPUT (JSON)
══════════════════════════════════════════
{
  "Summary": "The build failed due to integration tests experiencing connection issues...",
  "Evidence": ["Error in log: 'HttpRequestException: Connection refused (127.0.0.1:5432)'..."],
  "Recommendations": ["Verify PostgreSQL is running...", "Inspect test environment..."],
  "Confidence": 0.85
}
```

---

## Exercises

1. ✏️ **Build log 01**: Query: _"Analyze build-log-01.txt and identify likely root causes"_ *(~5 min)*

2. ✏️ **Build log 02**: Query: _"Analyze build-log-02.txt for test failures and coverage issues"_
   Compare the JSON output for both logs — observe different categories. *(~5 min)*

3. ✏️ **KB analysis**: Query: _"What does the KB say about handling flaky tests?"_
   The evidence step should call SearchKb. *(~3 min)*

4. ✏️ **Add a step**: Extend the workflow with a **5th step "Impact"** that estimates business impact.
   Add it between Critique and Final in `AnalysisWorkflow.cs`. *(~10 min)*

💡HINT: Prompt for GitHub Copilot:
```
Module `04_Workflows_MultiStep`: In #file:AnalysisWorkflow.cs add a new step. Extend the workflow with a 5th step "Impact" that estimates business impact. Add it between Critique and Final in `AnalysisWorkflow.cs`. Update also #file:Program.cs 
```

---

## Key Takeaways

- **Each step = separate session** — prevents one step's context from leaking into another
- **Context chaining** — output from step N is injected into step N+1's prompt
- **Structured output** requires explicit instructions — "Respond ONLY with valid JSON"
- **Streaming with labels** provides observability into what the pipeline is doing
- This pattern scales to N steps — add more steps without reorganizing code

---

**[Next: Module 05 — Human-in-the-Loop & Guards →](../05_HumanInLoop_Guards/README.md)**