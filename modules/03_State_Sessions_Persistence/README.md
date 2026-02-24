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

- `WorkshopSession` model: `SessionId`, `Label`, `CreatedAt`, `Messages`
- JSON serialization with `System.Text.Json`
- In-memory history tracking and replay
- Session commands: `/new`, `/list`, `/load`, `/delete`, `/status`
- Auto-save after each conversation turn

---

## Key Code

**Session model with serializable message history:**

```csharp
internal class WorkshopSession
{
    public Guid SessionId { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Label { get; set; } = "";
    public List<SessionMessage> Messages { get; set; } = [];
}
```

**Saving after each turn:**

```csharp
// After getting the agent's response, add to history and save
workshopSession.Messages.Add(new("user", input));
workshopSession.Messages.Add(new("assistant", responseText));
SessionStore.Save(workshopSession);  // writes to .sessions/{id}.json
```

**Loading a session by ID prefix:**

```csharp
var session = SessionStore.Load(sessionId);
// Replay history into a fresh AgentSession to restore context
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
  "Messages": [
    { "Role": "user", "Content": "What are the flaky test rules?" },
    { "Role": "assistant", "Content": "According to our policy..." }
  ]
}
```

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
  ad5dcad1-2797-4570-959f-31fa92edb111 | 2026-02-24 07:50:50Z | "testing-discussion" | 2 messages ◄ active
[testing-discussion] You> /exit
Goodbye!

# Restart the app, then:

You> /list
Saved sessions (1):
  ad5dcad1-2797-4570-959f-31fa92edb111 | 2026-02-24 07:50:50Z | "testing-discussion" | 2 messages
You> /load ad5
✅ Loaded session: ad5dcad1-2797-4570-959f-31fa92edb111 ("testing-discussion") - 2 messages in history
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
- **History replay** feeds past messages into a fresh `AgentSession` to restore context
- This pattern is foundational for production agent applications (e.g., chat history databases)

---

**[Next: Module 04 — Workflows & Multi-Step Pipelines →](../04_Workflows_MultiStep/README.md)**
