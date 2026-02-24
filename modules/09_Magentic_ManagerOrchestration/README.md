# Module 09 - Magentic Manager Orchestration

**Duration:** ~25 minutes  
**Goal:** Teach **Magentic-style** orchestration: a manager LLM dynamically selects which agent acts next based on evolving context and progress.

---

## Purpose

Instead of a fixed rotation (group chat) or pre-defined routes (handoff), a **Magentic Manager** decides at each step:
- What has been accomplished so far
- Which agent should act next and why
- Whether to stop (confidence reached or evidence sufficient)

This mirrors the **Magentic-One** architecture from Microsoft Research.

> **Note:** The `Microsoft.Agents.AI.Workflows` v1.0.0-rc1 package does not yet include
> a built-in `MagenticManager` type. This module implements the pattern manually using
> the `AIAgent` API directly, demonstrating the core concepts.

---

## Team

| Agent | Role | Tools |
|-------|------|-------|
| `magentic-manager` | Selects next agent each turn | ❌ |
| `researcher` | Gathers evidence from logs and KB | ✅ |
| `diagnostician` | Proposes root cause hypotheses | ❌ |
| `critic` | Challenges assumptions | ❌ |
| `scribe` | Produces final JSON triage card | ❌ |

---

## Guardrails

| Limit | Value |
|-------|-------|
| Max iterations | 8 |
| Max tool calls | 5 |
| Confidence threshold | 0.75 |
| Human checkpoint | Before manager starts |

---

## Key Concepts

| Concept | Description |
|---------|-------------|
| Manager-driven selection | LLM decides next agent each turn |
| Rolling progress summary | Manager maintains context across iterations |
| Confidence-based stopping | Stop early if confidence >= threshold |
| Shared history | All agents receive full conversation context |
| Human checkpoint | User approves/aborts before automation begins |

---

## How to Run

```bash
./scripts/run.sh 09
# or
dotnet run --project modules/09_Magentic_ManagerOrchestration
```

---

## Expected Output

```
━━━ MAGENTIC MANAGER ━━━
  Starting manager-driven triage (max 8 iterations, max 5 tool calls)...

━━━ 🔐 Human-in-the-Loop Checkpoint ━━━
Decision: approve
✅ Starting Magentic triage...

Iteration 1/8 | ToolCalls 0/5
Manager chose: RESEARCHER (reason: needs evidence from log)
  Progress: No evidence gathered yet.
  Confidence: 0.10 | Task: Read build-log-01.txt and search KB for "database connection"

[RESEARCHER] Evidence gathered:
• [build-log-01.txt] Error: Connection refused 127.0.0.1:5432
...

Iteration 2/8 | ToolCalls 2/5
Manager chose: DIAGNOSTICIAN (reason: evidence available for hypothesis formation)
  Progress: Log shows DB connectivity error.
  Confidence: 0.55 | Task: Analyze the evidence and propose root cause hypotheses

[DIAGNOSTICIAN] Hypothesis 1 (confidence 0.8): PostgreSQL not running in test env...

...

[SCRIBE]
{
  "summary": "...",
  "category": "infra",
  ...
}
```

---

## Exercises

1. ✏️ **Observe manager selection**: Run with `build-log-01.txt` and note how the manager switches between researcher → diagnostician → critic → scribe.

2. ✏️ **Lower max iterations**: Change `MaxIterations` from 8 to 3. What breaks? Can you improve the manager's prompt (`magentic-manager.md`) to still succeed in fewer turns?

3. ✏️ **Confidence threshold**: Change `ConfidenceThreshold` from 0.75 to 0.5. Does the manager stop earlier? Is the triage card quality lower?

4. ✏️ **Add plan approval**: Extend the human checkpoint to also show the manager's initial plan before allowing the loop to continue. Use the first manager decision as the "plan" and ask the user to `approve / revise / abort`.

5. ✏️ **Custom stopping rule**: Add a rule: if the same agent is chosen 3 times in a row, skip to scribe. Implement this counter in `MagenticWorkflow.cs`.
