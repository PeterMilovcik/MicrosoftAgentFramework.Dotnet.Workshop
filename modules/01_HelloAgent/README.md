# Module 01 - Hello Agent

**Duration:** ~25 minutes  
**Goal:** Build a basic interactive agent with a system prompt and persistent conversation history.

---

## Purpose

Learn the fundamentals of the Microsoft Agent Framework:

- What an `AIAgent` is and how to create one
- How `AgentSession` maintains in-memory conversation history
- How to run a streaming REPL (Read-Eval-Print Loop)
- How to load prompts from editable Markdown files

---

## Concepts Covered

- `IChatClient.AsAIAgent(instructions)` - wraps a chat model as an agent
- `agent.CreateSessionAsync()` - creates a stateful session
- `agent.RunStreamingAsync(input, session)` - streams the response token by token
- Loading `system-base.md` and `system-safety.md` from `assets/prompts/`
- REPL commands: `/help`, `/reset`, `/sys`, `/exit`

---

## How to Run

```bash
./scripts/run.sh 01       # Linux/macOS
./scripts/run.ps1 01      # Windows PowerShell
dotnet run --project modules/01_HelloAgent
```

---

## Expected Output

```
===========================================
 Module 01 - Hello Agent (REPL Loop)
===========================================

✅ Agent ready. System prompt loaded.

Commands:
  /help   Show this help message
  /reset  Clear conversation history
  /sys    Print active system prompt
  /exit   Exit the application

Type any message to chat with the agent.

You> Hello! What can you do?
Agent> Hello! I'm designed to help with a variety of tasks, including:

- **Answering questions**: I can provide information or explanations on a wide range of topics.
- **Reading files**: If you provide me with a file (such as `.txt` or `.md` within a specific folder), I can analyze or summarize its content.
- **Analyzing logs**: I can help pinpoint issues by reviewing log files and offering interpretations.
- **Knowledge base search**: If a relevant keyword or topic exists in a predefined knowledge base, I can search it for useful information.
- **Providing recommendations**: Based on the information you provide (e.g., logs or data), I can suggest next steps or possible solutions.

Let me know how I can assist you today!

You> /reset
🔄 Conversation history cleared.

You> /exit
Goodbye!
```

---

## Exercises

1. ✏️ **Shorten responses**: Edit `assets/prompts/system-base.md` and add: "Always respond in 1-2 sentences maximum." Run again and observe shorter answers.

2. ✏️ **Bullet mode**: Add the following line to `system-base.md`:
   ```
   When listing items, always use bullet points (•), never numbered lists.
   ```
   Ask "What are your capabilities?" and compare the formatting.

3. ✏️ **Test conversation memory**: Ask "My name is Alice." Then ask "What is my name?" Verify the agent remembers. Then use `/reset` and ask again - it should not remember.

4. ✏️ **Explore `/sys`**: Use the `/sys` command to see the active system prompt. Observe how both `system-base.md` and `system-safety.md` are combined.
