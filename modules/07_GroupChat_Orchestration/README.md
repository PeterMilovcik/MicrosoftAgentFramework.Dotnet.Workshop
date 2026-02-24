# Module 07 - Group Chat Orchestration

**Duration:** ~20 minutes  
**Goal:** Teach the **Group Chat** multi-agent pattern using Microsoft Agent Framework's built-in Group Chat orchestration.

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
| `AgentWorkflowBuilder.CreateGroupChatBuilderWith` | Factory for the group chat orchestration |
| `InProcessExecution.RunStreamingAsync` | Runs the workflow and streams `WorkflowEvent`s |
| `AgentResponseEvent` | Event fired when an agent produces a complete response |
| Tool enforcement | Only the investigator agent is created with tools |

---

## How to Run

```bash
./scripts/run.sh 07
# or
dotnet run --project modules/07_GroupChat_Orchestration
```

Set these environment variables first (see `scripts/set-env.sh`):

```bash
export AZURE_OPENAI_ENDPOINT=...
export AZURE_OPENAI_API_KEY=...
export AZURE_OPENAI_DEPLOYMENT=...
```

---

## Expected Output

```
━━━ GROUP CHAT ━━━
  Starting group chat with 4 agents (max 8 turns)...

[PLANNER] Analysis plan:
• Category: likely infra (connection refused on port 5432)
• Evidence to examine: build-log-01.txt, KB entries on DB connectivity
• Subtasks: INVESTIGATOR read build-log-01.txt and search "database connection"
• Confidence: high

[INVESTIGATOR] Evidence gathered:
• [build-log-01.txt] Error: Connection refused 127.0.0.1:5432
• [release-notes.md] Known issue: PostgreSQL not started in test containers
...

[CRITIC] Evidence assessment:
• Strong: connection error clearly identified
• Gap: no confirmation of docker-compose config
...

[SCRIBE]
{
  "summary": "...",
  "category": "infra",
  ...
}
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

1. ✏️ **Observe role division**: Run with `build-log-01.txt` and note how each agent contributes differently.

2. ✏️ **Tune max turns**: Change `MaxIterations` in `GroupChatWorkflow.cs` from 8 to 4. Does quality suffer? Try 12 — does it improve?

3. ✏️ **Modify critic.md**: Make the Critic stricter by requiring it to always list at least 3 gaps. Compare outputs before and after.

4. ✏️ **Custom manager**: Replace `RoundRobinGroupChatManager` with a custom `GroupChatManager` that calls the planner first, then investigator twice, then critic, then scribe — regardless of round-robin order.
