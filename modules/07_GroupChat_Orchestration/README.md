# Module 07 - Group Chat Orchestration

**Duration:** ~20 minutes  
**Goal:** Teach the **Group Chat** multi-agent pattern using Microsoft Agent Framework's built-in Group Chat orchestration.

> **Advanced / Bonus Module** — requires all core modules (00-06) completed.

---

## What's New in This Module

Moving from single-agent to **multi-agent collaboration**:
- **4 specialized agents** share a conversation context
- **`RoundRobinGroupChatManager`** — built-in rotation coordinator
- **`AgentWorkflowBuilder`** — declarative workflow construction
- **`InProcessExecution.Lockstep`** — event-driven streaming execution
- **Tool enforcement** — only the investigator agent gets tools

---

## Prerequisites

- Module 06 (capstone) concepts — triage workflow, TriageCard
- Module 02 concepts (tools)
- Understanding of `AIAgent` creation and streaming from earlier modules
- Input is simplified with preset scenarios to let you focus on the orchestration pattern

---

## Purpose

Multiple specialized agents collaborate in a shared conversation coordinated by a `RoundRobinGroupChatManager`:

- **Planner** — analyzes the failure and assigns subtasks
- **Investigator** — uses tools (`SearchKb`, `ReadFile`) to gather evidence
- **Critic** — challenges assumptions and finds gaps
- **Scribe** — produces the final structured JSON Triage Card

Key constraints enforced at runtime:
- Only the **Investigator** may call tools
- Maximum **8 turns** total (2 full rounds for 4 agents)

---

## Key Concepts

| Concept | Description |
|---------|-------------|
| Group Chat | Multiple agents share a conversation context |
| `RoundRobinGroupChatManager` | Selects agents in fixed rotation |
| `AgentWorkflowBuilder.CreateGroupChatBuilderWith` | Factory for group chat orchestration |
| `InProcessExecution.Lockstep.RunStreamingAsync` | Runs the workflow in lockstep mode |
| `AgentResponseUpdateEvent` | Event fired for streaming response updates |
| Tool enforcement | Only the investigator agent is created with tools |

---

## Key Code

**Building a group chat workflow:**

```csharp
// Create named agents — only investigator gets tools
var plannerAgent = config.CreateNamedAgent(
    plannerPrompt, name: "planner", description: "Creates the triage plan");
var investigatorAgent = config.CreateNamedAgent(
    investigatorPrompt, name: "investigator", description: "Gathers evidence",
    tools: WorkshopTools.GetInvestigatorTools());  // only this agent gets tools
var criticAgent = config.CreateNamedAgent(criticPrompt, name: "critic", description: "Challenges assumptions");
var scribeAgent = config.CreateNamedAgent(scribePrompt, name: "scribe", description: "Produces JSON triage card");

// Build the group chat with a round-robin manager
var groupChatBuilder = AgentWorkflowBuilder.CreateGroupChatBuilderWith(
    agents => new RoundRobinGroupChatManager(agents) { MaximumIterationCount = 8 });

groupChatBuilder.AddParticipants([plannerAgent, investigatorAgent, criticAgent, scribeAgent]);
var workflow = groupChatBuilder.Build();
```

**Running with streaming events:**

```csharp
List<ChatMessage> messages = [new(ChatRole.User, context)];
await using var streamingRun = await InProcessExecution.Lockstep.RunStreamingAsync(workflow, messages);
await streamingRun.TrySendMessageAsync(new TurnToken(emitEvents: true));

await foreach (var evt in streamingRun.WatchStreamAsync())
{
    if (evt is AgentResponseUpdateEvent agentEvt)
    {
        Console.Write(agentEvt.Update.Text);  // stream each agent's response
    }
}
```

---

## How to Run

```bash
./scripts/run.sh 07
# or
dotnet run --project modules/07_GroupChat_Orchestration
```

---

## Expected Output

```
Select a scenario:
  [1] AuthService — DB connection pool failure  (build-log-01.txt)
  [2] PaymentGateway — retry + coverage failure (build-log-02.txt)
  [3] Custom — enter your own failure report
Choice [1-3]: 1

🚀 Starting group chat triage...

━━━ GROUP CHAT ━━━
  Starting group chat with 4 agents (max 8 turns)...

[PLANNER] Analysis plan:
• Category: likely infra (connection refused on port 5432)
• Subtasks: INVESTIGATOR read build-log-01.txt and search "database connection"
...

[INVESTIGATOR] Evidence gathered:
• [build-log-01.txt] Error: Connection refused 127.0.0.1:5432
...

[CRITIC] Evidence assessment:
• Strong: connection error clearly identified
• Gap: no confirmation of docker-compose config
...

[SCRIBE]
{ "summary": "...", "category": "infra", ... }
```

---

## Agent Prompt Files

| File | Agent | Role |
|------|-------|------|
| `assets/prompts/agents/planner.md` | Planner | Creates the plan |
| `assets/prompts/agents/investigator.md` | Investigator | Uses tools |
| `assets/prompts/agents/critic.md` | Critic | Challenges assumptions |
| `assets/prompts/agents/scribe.md` | Scribe | Produces JSON card |

---

## Exercises

1. ✏️ **Observe role division**: Run with scenario 1 and note how each agent contributes differently. *(~5 min)*

2. ✏️ **Tune max turns**: Change `MaxIterations` in `GroupChatWorkflow.cs` from 8 to 4. Does quality suffer? Try 12 — does it improve? *(~5 min)*

3. ✏️ **Modify critic.md**: Make the Critic stricter by requiring it to always list at least 3 gaps. Compare outputs before and after. *(~5 min)*

4. ✏️ **Custom scenario**: Select scenario 3, write a custom failure report, and observe how the group handles it. *(~5 min)*

💡HINT: Prompt for GitHub Copilot:

```
In #file:GroupChatWorkflow.cs for module 07, change MaxIterations from 8 to 12 and add a console log at each iteration showing the current turn number and which agent is responding.
```

---

## Key Takeaways

- **Group chat = shared context** — all agents see the full conversation history
- **Round-robin rotation** is simple but effective for structured analysis
- **Tool enforcement** is done at agent creation — only pass tools to agents that should have them
- **`AgentWorkflowBuilder`** provides a declarative API for multi-agent orchestration
- **`InProcessExecution.Lockstep`** gives you event-driven control over execution flow

---

## Further Reading

- [Multi-agent design patterns](https://learn.microsoft.com/azure/ai-services/openai/concepts/advanced-prompt-engineering)
- [Microsoft Agent Framework Workflows (GitHub)](https://github.com/microsoft/Agents)
