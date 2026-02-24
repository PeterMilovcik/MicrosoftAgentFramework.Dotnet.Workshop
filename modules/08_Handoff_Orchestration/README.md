# Module 08 - Handoff Orchestration

**Duration:** ~20 minutes  
**Goal:** Teach the **Handoff** pattern: agents transfer control to specialists based on failure context.

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
| `AgentWorkflowBuilder.CreateHandoffBuilderWith` | Creates the handoff workflow builder starting with a given agent |
| `HandoffsWorkflowBuilder.WithHandoff` | Adds a directed handoff relationship between two agents |
| `HandoffsWorkflowBuilder.WithHandoffs` | Adds handoff from multiple sources to one target |
| `InProcessExecution.RunStreamingAsync` | Runs the workflow and streams `WorkflowEvent`s |
| `AgentResponseEvent` | Event fired when an agent completes a response |

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
━━━ HANDOFF WORKFLOW ━━━
  Starting handoff-based triage...
  frontdesk → [infra-expert | product-expert | test-expert] → scribe

[FRONTDESK]
This failure mentions "Connection refused on port 5432" — a database connectivity issue.
Routing to infra-expert for investigation.

  ⟶ Handoff: frontdesk → infra-expert

[INFRA-EXPERT]
Evidence from build-log-01.txt: PostgreSQL unreachable at 127.0.0.1:5432...
Root cause hypothesis: missing docker-compose service definition.
Handing off to scribe.

  ⟶ Handoff: infra-expert → scribe

[SCRIBE]
{
  "summary": "...",
  "category": "infra",
  ...
}
```

---

## Agent Prompt Files

| File | Agent | Can Use Tools |
|------|-------|--------------|
| `assets/prompts/agents/frontdesk.md` | Front Desk | ❌ |
| `assets/prompts/agents/infra-expert.md` | Infra Expert | ✅ |
| `assets/prompts/agents/product-expert.md` | Product Expert | ✅ |
| `assets/prompts/agents/test-expert.md` | Test Expert | ✅ |
| `assets/prompts/agents/scribe.md` | Scribe | ❌ |

---

## Exercises

1. ✏️ **Infra routing**: Use this report: _"The CI pipeline timed out after 10 minutes. The agent process never started."_ Verify it routes to `infra-expert`.

2. ✏️ **Product routing**: Use: _"NullReferenceException in PaymentService.ProcessAsync after the last deployment."_ Verify `product-expert` handles it.

3. ✏️ **Test routing**: Use: _"Test `OrderTest.ShouldCalculateTotal` fails intermittently on CI but passes locally."_ Verify `test-expert` handles it.

4. ✏️ **Ambiguous input**: Try: _"The build failed."_ — observe how `frontdesk` makes its decision. Add a confidence heuristic: if the frontdesk response contains "unclear", route to the most conservative expert (`product-expert`).

5. ✏️ **Loop prevention**: Add a counter to `HandoffWorkflow.cs` that stops the workflow if more than 5 agents have responded.
