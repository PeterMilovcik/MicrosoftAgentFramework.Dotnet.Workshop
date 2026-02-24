# Module 01 - Hello Agent

**Duration:** ~25 minutes  
**Goal:** Build a basic interactive agent with a system prompt and persistent conversation history.

---

## What's New in This Module

Building on Module 00's single API call, you'll now create a **full interactive REPL** with:
- **Streaming** responses (token-by-token output)
- **Conversation memory** within a session
- **Editable system prompts** loaded from Markdown files

---

## Prerequisites

- Module 00 passing (environment validated)
- Basic understanding of `AIAgent` and `AgentSession` from Module 00

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

## Key Code

**Streaming responses token-by-token:**

```csharp
// RunStreamingAsync returns an IAsyncEnumerable<AgentResponseUpdate>
await foreach (var update in agent.RunStreamingAsync(input, session))
{
    Console.Write(update.Text);  // print each token as it arrives
}
```

**Loading and combining prompts from files:**

```csharp
var systemPrompt = File.ReadAllText("assets/prompts/system-base.md");
var safetyPrompt = File.ReadAllText("assets/prompts/system-safety.md");
var instructions = $"{systemPrompt}\n\n---\n\n{safetyPrompt}";

var agent = config.CreateAgent(instructions);
```

**Resetting conversation history:**

```csharp
// Create a fresh session to clear all history
session = await agent.CreateSessionAsync();
```

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
...

You> /reset
🔄 Conversation history cleared.

You> /exit
Goodbye!
```

---

## Exercises

1. ✏️ **Shorten responses**: Edit `assets/prompts/system-base.md` and add: "Always respond in 1-2 sentences maximum." Run again and observe shorter answers. *(~3 min)*

2. ✏️ **Bullet mode**: Add the following line to `system-base.md`:
   ```
   When listing items, always use bullet points (•), never numbered lists.
   ```
   Ask "What are your capabilities?" and compare the formatting. *(~3 min)*

3. ✏️ **Test conversation memory**: Ask "My name is Alice." Then ask "What is my name?" Verify the agent remembers. Then use `/reset` and ask again — it should not remember. *(~3 min)*

4. ✏️ **Explore `/sys`**: Use the `/sys` command to see the active system prompt. Observe how both `system-base.md` and `system-safety.md` are combined. *(~2 min)*

5. ✏️ **Add a `/tokens` command** that displays the current token usage summary using `AgentConfig.PrintTokenSummary()`. *(~5 min)*

💡HINT: Prompt for GitHub Copilot:

```
In #file:Program.cs for module 01_HelloAgent, add a `/tokens` command to the REPL that calls AgentConfig.PrintTokenSummary() to display current token usage.
```

---

## Key Takeaways

- **Streaming** (`RunStreamingAsync`) gives a responsive UX — tokens appear as they're generated
- **Sessions** maintain conversation history automatically — the agent remembers previous turns
- **`/reset` creates a new session** — this is how you clear memory without restarting
- **Prompts are just Markdown files** — easy to edit, version, and experiment with
- Separating system prompt from safety prompt keeps concerns independent

---

## Further Reading

- [System prompt best practices (Azure OpenAI)](https://learn.microsoft.com/azure/ai-services/openai/concepts/system-message)
- [Microsoft.Extensions.AI — IChatClient](https://learn.microsoft.com/dotnet/ai/ai-extensions)

---

**[Next: Module 02 — Tools & Function Calling →](../02_Tools_FunctionCalling/README.md)**
