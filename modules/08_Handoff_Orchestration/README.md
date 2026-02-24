# Module 08 - Handoff Orchestration

**Duration:** ~20 minutes  
**Goal:** Teach the **Handoff** pattern: agents transfer control to specialists based on failure context.

> **Advanced / Bonus Module** — requires all core modules (00-06) completed.

---

## What's New in This Module

Building on Module 07's group chat, you'll now see **directed handoffs**:
- **Agents decide who acts next** — no fixed rotation
- **`CreateHandoffBuilderWith`** — declarative handoff graph
- **`WithHandoff` / `WithHandoffs`** — define directed transfer relationships
- **Routing logic** — the frontdesk LLM picks the right expert based on failure type
- **Many-to-one** — all experts hand off to the same scribe

---

## Prerequisites

- Module 07 concepts (multi-agent, `AgentWorkflowBuilder`, `InProcessExecution.Lockstep`)
- Module 06 (TriageCard, triage workflow)

---

## Purpose

A **front-desk** agent triages incoming failure reports and routes them to the right specialist, who then hands off to a scribe for final output. No central planner — each agent decides who acts next.

**Routing logic:**

| Failure Signal | Expert |
|---------------|--------|
| Timeouts, pipelines, network, CI/CD, environment | `infra-expert` |
| Stack traces, null refs, code bugs, regressions | `product-expert` |
| Flaky tests, assertions, test setup, non-determinism | `test-expert` |

Then: expert → `scribe` (always).

Key constraints:
- **Only expert agents** may call tools (`ReadFile`, `SearchKb`)
- `frontdesk` and `scribe` do NOT call tools
- Maximum handoffs capped by prompt design

---

## Key Concepts

| Concept | Description |
|---------|-------------|
| Handoff | An agent transfers control to another agent |
| `AgentWorkflowBuilder.CreateHandoffBuilderWith` | Creates the handoff workflow starting with a given agent |
| `HandoffsWorkflowBuilder.WithHandoff` | Adds a directed handoff between two agents |
| `HandoffsWorkflowBuilder.WithHandoffs` | Adds handoff from multiple sources to one target |
| `InProcessExecution.Lockstep.RunStreamingAsync` | Runs the workflow with streaming events |

---

## Key Code

**Declaring the handoff graph:**

```csharp
// Build handoff workflow — frontdesk starts
var handoffBuilder = AgentWorkflowBuilder.CreateHandoffBuilderWith(frontdesk);

// frontdesk → experts (routing description guides the LLM's decision)
handoffBuilder.WithHandoff(frontdesk, infraExpert,
    "Route here when failure involves infra, CI/CD, pipelines, timeouts, or network issues");
handoffBuilder.WithHandoff(frontdesk, productExpert,
    "Route here when failure involves stack traces, null refs, regressions, or code bugs");
handoffBuilder.WithHandoff(frontdesk, testExpert,
    "Route here when failure involves flaky tests, assertions, test setup, or non-determinism");

// experts → scribe (all experts hand off to scribe after investigation)
handoffBuilder.WithHandoffs([infraExpert, productExpert, testExpert], scribe,
    "Hand off here after investigation is complete to produce the final JSON triage card");

var workflow = handoffBuilder.Build();
```

**Tool enforcement at agent creation:**

```csharp
// Only expert agents receive tools
var infraExpert = config.CreateNamedAgent(LoadPrompt("infra-expert"),
    name: "infra-expert", description: "...", tools: expertTools);

// Frontdesk and scribe are created WITHOUT tools
var frontdesk = config.CreateNamedAgent(LoadPrompt("frontdesk"),
    name: "frontdesk", description: "...");  // no tools parameter
```

---

## How to Run

```bash
./scripts/run.sh 08
# or
dotnet run --project modules/08_Handoff_Orchestration
```

---

## Expected Output

```
Select a scenario:
  [1] AuthService — DB connection pool failure  (build-log-01.txt)
  [2] PaymentGateway — retry + coverage failure (build-log-02.txt)
  [3] Custom — enter your own failure report
Choice [1-3]: 1

🚀 Starting handoff triage...

━━━ HANDOFF WORKFLOW ━━━
  frontdesk → [infra-expert | product-expert | test-expert] → scribe

[FRONTDESK]
This failure mentions "Connection refused on port 5432" — routing to infra-expert.

  ⟶ Handoff: frontdesk → infra-expert

[INFRA-EXPERT]
Evidence from build-log-01.txt: PostgreSQL unreachable at 127.0.0.1:5432...
Handing off to scribe.

  ⟶ Handoff: infra-expert → scribe

[SCRIBE]
{ "summary": "...", "category": "infra", ... }
```

---

## Agent Prompt Files

| File | Agent | Can Use Tools |
|------|-------|--------------|
| `assets/prompts/agents/frontdesk.md` | Front Desk | No |
| `assets/prompts/agents/infra-expert.md` | Infra Expert | Yes |
| `assets/prompts/agents/product-expert.md` | Product Expert | Yes |
| `assets/prompts/agents/test-expert.md` | Test Expert | Yes |
| `assets/prompts/agents/scribe.md` | Scribe | No |

---

## Exercises

1. ✏️ **Infra routing**: Use scenario 1 (AuthService DB failure). Verify it routes to `infra-expert`. *(~3 min)*

2. ✏️ **Product routing**: Select scenario 3 and enter: _"NullReferenceException in PaymentService.ProcessAsync after the last deployment."_ Verify `product-expert` handles it. *(~3 min)*

3. ✏️ **Test routing**: Select scenario 3 and enter: _"Test `OrderTest.ShouldCalculateTotal` fails intermittently on CI but passes locally."_ Verify `test-expert` handles it. *(~3 min)*

4. ✏️ **Ambiguous input**: Select scenario 3 and try: _"The build failed."_ — observe how `frontdesk` makes its routing decision with minimal context. *(~3 min)*

5. ✏️ **Loop prevention**: Add a counter to `HandoffWorkflow.cs` that stops the workflow if more than 5 agents have responded. *(~10 min)*

💡HINT: Prompt for GitHub Copilot:

```
In #file:HandoffWorkflow.cs for module 08, add a counter that tracks how many agent responses have been received. If it exceeds 5, break out of the event loop and use whatever scribe output is available.
```

---

## Key Takeaways

- **Handoff = directed graph** — unlike round-robin, agents choose who acts next
- **`WithHandoff` descriptions guide the LLM** — the routing decision is semantic, not rule-based
- **`WithHandoffs` (plural)** allows many-to-one relationships (all experts → scribe)
- **Frontdesk pattern** — a router agent that doesn't process, only delegates
- Compare with Module 07: handoff trades simplicity (no fixed order) for dynamic routing flexibility

---

## Further Reading

- [Agent handoff patterns in multi-agent systems](https://learn.microsoft.com/azure/ai-services/openai/concepts/advanced-prompt-engineering)
- [Microsoft Agent Framework Workflows (GitHub)](https://github.com/microsoft/Agents)

---

**[Next: Module 09 — Magentic Manager Orchestration →](../09_Magentic_ManagerOrchestration/README.md)**
