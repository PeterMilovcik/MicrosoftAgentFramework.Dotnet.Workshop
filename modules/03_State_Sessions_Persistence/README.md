# Module 03 – State, Sessions & Persistence

**Duration:** ~25 minutes  
**Goal:** Persist conversation sessions as JSON files and reload them across application restarts.

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
> /new
Session label: testing-discussion
✅ New session created: 3f8a1c2d-... ("testing-discussion")

[testing-discussion] You> What are the flaky test guidelines?
Agent> According to testing-guidelines.md...

> /list
Saved sessions (1):
  3f8a1c2d-... | 2026-02-23T15:42:00Z | "testing-discussion" | 2 messages ◄ active

> /exit

# Restart the app, then:
> /list
> /load 3f8a  # use ID prefix
✅ Loaded session: 3f8a1c2d-... – 2 messages in history
```

---

## Exercises

1. ✏️ **Persona sessions**: Create two sessions with different "personas" by starting each with a different instruction:
   - Session A: "For this session, I am a Python developer"
   - Session B: "For this session, I am a DevOps engineer"
   Switch between sessions and observe how context differs.

2. ✏️ **Persistence test**: Create a session, have a conversation, then `/exit`. Restart the app, run `/list`, load the session, and ask a follow-up question. Verify history is preserved.

3. ✏️ **Delete test**: Create 3 sessions, list them, delete the middle one, and verify the list is correct.

4. ✏️ **Extend the model**: Add a `LastAccessedAt` field to `WorkshopSession` that updates on each `/load` command.
