# Module 09 - Magentic Manager Orchestration

**Duration:** ~25 minutes  
**Goal:** Teach **Magentic-style** orchestration: a manager LLM dynamically selects which agent acts next based on evolving context and progress.

> **Advanced / Bonus Module** — requires all core modules (00-06) completed.

---

## What's New in This Module

Building on Modules 07-08's multi-agent patterns, this module introduces **dynamic LLM-driven orchestration**:
- **Manager LLM decides** which agent acts next each turn (no fixed rotation, no static routes)
- **Rolling progress summary** — the manager tracks what's been accomplished
- **Confidence-based stopping** — stop early when confidence threshold is met
- **`ManagerDecision`** — structured JSON parsed from the manager LLM at each iteration
- **Manual implementation** — demonstrates the Magentic-One pattern directly with `AIAgent` API

---

## Prerequisites

- Module 07 concepts (multi-agent, shared context)
- Module 08 concepts (agent selection, handoff)
- Module 05 concepts (human-in-the-loop checkpoint)

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
| `magentic-manager` | Selects next agent each turn | No |
| `researcher` | Gathers evidence from logs and KB | Yes |
| `diagnostician` | Proposes root cause hypotheses | No |
| `critic` | Challenges assumptions | No |
| `scribe` | Produces final JSON triage card | No |

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

## Key Code

**Manager decision structure:**

```csharp
// ManagerDecision.cs — extracted into its own file
internal sealed class ManagerDecision
{
    [JsonPropertyName("progress_summary")] public string ProgressSummary { get; set; } = "";
    [JsonPropertyName("next_agent")]       public string NextAgent { get; set; } = "";
    [JsonPropertyName("reason")]           public string Reason { get; set; } = "";
    [JsonPropertyName("task")]             public string Task { get; set; } = "";
    [JsonPropertyName("confidence")]       public double Confidence { get; set; }
}
```

**Manager loop — the core orchestration pattern:**

```csharp
while (iteration < MaxIterations)
{
    iteration++;

    // 1. Ask the manager LLM which agent should act next
    var managerSession = await managerAgent.CreateSessionAsync(ct);
    var managerResponse = await managerAgent.RunAsync(managerPrompt, managerSession, ct);
    var decision = JsonSerializer.Deserialize<ManagerDecision>(managerResponse.Text);

    // 2. Check stopping conditions
    if (decision.Confidence >= ConfidenceThreshold && decision.NextAgent == "scribe")
        break;  // confidence threshold met

    // 3. Run the selected agent
    var selectedAgent = agentMap[decision.NextAgent];
    var agentSession = await selectedAgent.CreateSessionAsync(ct);
    var agentResult = await RunAgent(selectedAgent, decision.Task, agentSession, ct);

    // 4. Add result to shared history for next iteration
    sharedHistory.Add($"[{decision.NextAgent}]: {agentResult}");
}
```

**Agent map — lookup by name:**

```csharp
var agentMap = new Dictionary<string, AIAgent>(StringComparer.OrdinalIgnoreCase)
{
    ["researcher"]     = researcherAgent,
    ["diagnostician"]  = diagnosticianAgent,
    ["critic"]         = criticAgent,
    ["scribe"]         = scribeAgent,
};
```

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
Select a scenario:
  [1] AuthService — DB connection pool failure  (build-log-01.txt)
  [2] PaymentGateway — retry + coverage failure (build-log-02.txt)
  [3] Custom — enter your own failure report
Choice [1-3]: 1

━━━ 🔐 Human-in-the-Loop Checkpoint ━━━
Decision: approve
✅ Starting Magentic triage...

━━━ MAGENTIC MANAGER ━━━
  Starting manager-driven triage (max 8 iterations, max 5 tool calls)...

Iteration 1/8 | ToolCalls 0/5
Manager chose: RESEARCHER (reason: needs evidence from log)
  Progress: No evidence gathered yet.
  Confidence: 0.10 | Task: Read build-log-01.txt and search KB

[RESEARCHER] Evidence gathered: ...

Iteration 2/8 | ToolCalls 2/5
Manager chose: DIAGNOSTICIAN (reason: evidence available for hypothesis)
  Progress: Log shows DB connectivity error.
  Confidence: 0.55 | Task: Analyze evidence and propose root cause

[DIAGNOSTICIAN] Hypothesis: PostgreSQL not running in test env...

...

[SCRIBE]
{ "summary": "...", "category": "infra", ... }
```

---

## Exercises

1. ✏️ **Observe manager selection**: Run with scenario 1 and note how the manager switches between researcher → diagnostician → critic → scribe. *(~5 min)*

2. ✏️ **Lower max iterations**: Change `MaxIterations` from 8 to 3. What breaks? Can you improve the manager's prompt (`magentic-manager.md`) to still succeed in fewer turns? *(~5 min)*

3. ✏️ **Confidence threshold**: Change `ConfidenceThreshold` from 0.75 to 0.5. Does the manager stop earlier? Is the triage card quality lower? *(~5 min)*

4. ✏️ **Compare orchestrations**: Run the same scenario through Modules 07, 08, and 09. Compare the triage card quality and token usage. *(~10 min)*

5. ✏️ **Custom stopping rule**: Add a rule: if the same agent is chosen 3 times in a row, skip to scribe. Implement this counter in `MagenticWorkflow.cs`. *(~10 min)*

💡HINT: Prompt for GitHub Copilot:

```
In #file:MagenticWorkflow.cs for module 09, add a counter that tracks consecutive selections of the same agent. If any agent is chosen 3 times in a row, force the next selection to be "scribe" to produce the final output.
```

---

## Key Takeaways

- **Manager-driven orchestration** is the most flexible pattern — the LLM adapts to what it learns
- **`ManagerDecision`** provides structured steering — progress, next agent, reason, confidence
- **Confidence-based stopping** avoids wasting tokens when evidence is already sufficient
- **Shared history** lets each agent build on previous agents' work
- **Compare the three patterns**: Round-robin (07) is simplest, Handoff (08) is most predictable, Magentic (09) is most adaptive
- This module implements the pattern manually — future framework versions may include a built-in `MagenticManager`

---

## Further Reading

- [Magentic-One: A Generalist Multi-Agent System (Microsoft Research)](https://www.microsoft.com/en-us/research/articles/magentic-one-a-generalist-multi-agent-system-for-solving-complex-tasks/)
- [Multi-agent orchestration patterns](https://learn.microsoft.com/azure/ai-services/openai/concepts/advanced-prompt-engineering)
- [Microsoft Agent Framework (GitHub)](https://github.com/microsoft/Agents)

---

🎉 **Full workshop complete!**

**[← Back to Workshop Overview](../../README.md)** · **[☕ Support](../../README.md#-support)**
