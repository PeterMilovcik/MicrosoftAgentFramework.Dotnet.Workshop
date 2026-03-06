# Module 03 - State, Sessions & Persistence

**Duration:** ~25 minutes  
**Goal:** Persist conversation sessions as JSON files and reload them across application restarts.

---

## What's New in This Module

Building on Module 01's in-memory sessions, you'll now add **persistence**:
- **Save sessions to disk** as JSON files
- **Reload sessions** after restarting the application
- **Multiple concurrent sessions** with switching support
- Session management commands (`/new`, `/list`, `/load`, `/delete`)

---

## Prerequisites

- Module 01 concepts (REPL, agent, session, streaming)
- Basic understanding of JSON serialization in .NET

---

## Purpose

Understand how to manage agent state beyond a single run:

- Sessions have unique IDs, labels, timestamps, and message history
- Conversations are saved automatically as JSON after each turn
- Sessions survive application restarts and can be reloaded
- Multiple sessions can coexist and be switched between

---

## Concepts Covered

- `WorkshopSession` model: `SessionId`, `Label`, `CreatedAt`, `TurnCount`, `AgentSessionState`
- Framework-native session serialization via `agent.SerializeSessionAsync()` / `agent.DeserializeSessionAsync()`
- Letting `AgentSession` manage conversation history internally
- Session commands: `/new`, `/list`, `/load`, `/delete`, `/status`
- Auto-save after each conversation turn

---

## Key Code

**Session model with framework-serialized state:**

```csharp
internal class WorkshopSession
{
    public Guid SessionId { get; init; } = Guid.NewGuid();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string Label { get; set; } = "";
    public int TurnCount { get; set; }
    public JsonElement? AgentSessionState { get; set; }
}
```

**Saving after each turn using framework serialization:**

```csharp
// Serialize the AgentSession (includes full conversation history)
var serializedState = await agent.SerializeSessionAsync(agentSession);
workshopSession.AgentSessionState = serializedState;
workshopSession.TurnCount = turnCount;
SessionStore.Save(workshopSession);  // writes to .sessions/{id}.json
```

**Loading a session — deserialize directly into a working AgentSession:**

```csharp
var loaded = SessionStore.Load(sessionId);
agentSession = await agent.DeserializeSessionAsync(loaded.AgentSessionState.Value);
// AgentSession has full history restored — ready for follow-up conversation
```

---

## Session Storage

Sessions are stored in `.sessions/<SessionId>.json` in the current directory.

Example session file:
```json
{
  "SessionId": "3f8a1c2d-...",
  "CreatedAt": "2026-02-23T15:42:00Z",
  "Label": "testing-discussion",
  "TurnCount": 1,
  "AgentSessionState": { ... }
}
```

The `AgentSessionState` field contains the framework-serialized `AgentSession`, which includes the full conversation history and any internal state managed by the agent.

---

## How to Run

```bash
./scripts/run.sh 03
dotnet run --project modules/03_State_Sessions_Persistence
```

---

## Expected Interaction

```
You> /new
Session label (optional, press Enter to skip): testing-discussion
✅ New session created: ad5dcad1-2797-4570-959f-31fa92edb111 ("testing-discussion")

[testing-discussion] You> What are flaky tests guidelines?
Agent> Flaky tests are tests that pass and fail intermittently without any changes ...

[testing-discussion] You> /list
Saved sessions (1):
  ad5dcad1-2797-4570-959f-31fa92edb111 | 2026-02-24 07:50:50Z | "testing-discussion" | 1 turns ◄ active
[testing-discussion] You> /exit
Goodbye!

# Restart the app, then:

You> /list
Saved sessions (1):
  ad5dcad1-2797-4570-959f-31fa92edb111 | 2026-02-24 07:50:50Z | "testing-discussion" | 1 turns
You> /load ad5
✅ Loaded session: ad5dcad1-2797-4570-959f-31fa92edb111 ("testing-discussion") - 1 turns restored
```

---

## Exercises

1. ✏️ **Persona sessions**: Create two sessions with different "personas" by starting each with a different instruction:
   - Session A: "For this session, I am a Python developer"
   - Session B: "For this session, I am a DevOps engineer"
   Switch between sessions and observe how context differs. *(~5 min)*

2. ✏️ **Persistence test**: Create a session, have a conversation, then `/exit`. Restart the app, run `/list`, load the session, and ask a follow-up question. Verify history is preserved. *(~3 min)*

3. ✏️ **Delete test**: Create 3 sessions, list them, delete the middle one, and verify the list is correct. *(~3 min)*

4. ✏️ **Extend the model**: Add a `LastAccessedAt` field to `WorkshopSession` that updates on each `/load` command. *(~5 min)*

💡HINT: Prompt for GitHub Copilot:

```
Module `03_State_Sessions_Persistence`: #file:SessionStore.cs extend #sym:WorkshopSession model by adding a `LastAccessedAt` field that updates on each `/load` command. See #sym:Load(Guid) : WorkshopSession? Update #file:Program.cs to show `LastAccessedAt` in `/status` command.
```

---

## Key Takeaways

- **Sessions decouple state from the process** — restart the app without losing context
- **Auto-save** after each turn keeps conversations durable without user action
- **Session labels** make it easy to find and manage multiple conversations
- **Framework-native serialization** (`SerializeSessionAsync` / `DeserializeSessionAsync`) lets you persist and restore full `AgentSession` state without manually tracking messages
- Let the `AgentSession` manage conversation history — use `RunStreamingAsync(string, session)` instead of manually building message lists
- This pattern is foundational for production agent applications (e.g., chat history databases)

---

**[Next: Module 04 — Workflows & Multi-Step Pipelines →](../04_Workflows_MultiStep/README.md)**
