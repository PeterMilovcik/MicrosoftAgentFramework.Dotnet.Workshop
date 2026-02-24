# Module 00 - Connectivity Check

**Duration:** ~10 minutes  
**Goal:** Verify your environment is correctly configured and Azure OpenAI is reachable.

---

## What's New in This Module

This is the **starting point** — no prior modules required. You'll validate your environment and make your first API call using the Microsoft Agent Framework.

---

## Prerequisites

- .NET 10 SDK installed (`dotnet --version` → `10.0.100` or later)
- Azure OpenAI resource with a deployed model (e.g., `gpt-4o`)
- Environment variables set: `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_API_KEY`, `AZURE_OPENAI_DEPLOYMENT`
- See `scripts/set-env.ps1` (Windows) or `scripts/set-env.sh` (Linux/macOS)

> **Model availability:** This workshop uses `gpt-4o` by default but works with any chat-completion model
> deployed to your Azure OpenAI resource (e.g., `gpt-4o-mini`, `gpt-4.1`). Set `AZURE_OPENAI_DEPLOYMENT`
> to whatever deployment name you created. If your region doesn't have `gpt-4o`, any chat model will do.

---

## Purpose

Before starting the workshop, confirm that:

1. All required environment variables are set.
2. The Azure OpenAI endpoint, API key, and deployment are valid.
3. The SDK can make a successful API call.

This module exits with code `0` on success, `1` on missing config, and `2` on API failure.

---

## Concepts Covered

- Environment variable validation with friendly error messages
- Creating an `AIAgent` via `IChatClient.AsAIAgent()`
- Creating an `AgentSession` and calling `RunAsync()`
- Reading `AgentResponse.Text` and `AgentResponse.Usage`
- Basic diagnostics: elapsed time, correlation ID, token counts

---

## Key Code

**Creating an agent and running a single turn:**

```csharp
// Create the agent with a system prompt
var agent = config.CreateAgent("You are a connectivity test assistant. Keep responses very short.");

// Create a stateful session (maintains conversation history)
var session = await agent.CreateSessionAsync();

// Run a single turn and get the full response
AgentResponse response = await agent.RunAsync("Say OK", session);

// Read the result
Console.WriteLine(response.Text);           // "OK"
Console.WriteLine(response.Usage?.InputTokenCount);  // e.g. 14
Console.WriteLine(response.Usage?.OutputTokenCount); // e.g. 2
```

Under the hood, `CreateAgent` wraps `IChatClient.AsAIAgent(instructions)`:

```csharp
public AIAgent CreateAgent(string instructions)
    => CreateChatClient().AsAIAgent(instructions);
```

---

## How to Run

```bash
# Linux / macOS
./scripts/run.sh 00

# Windows PowerShell
./scripts/run.ps1 00

# Or directly
dotnet run --project modules/00_ConnectivityCheck
```

---

## Expected Output

```
===========================================
 Module 00 - Azure OpenAI Connectivity Check
===========================================

✅ Configuration loaded.
   Endpoint:   https://myresource.openai.azure.com/
   Deployment: gpt-4o
   API Version:2025-01-01-preview

🔗 Correlation ID: 3f8a1c2d-...

📡 Sending test message: "Say OK" ... done (842ms)

📨 Response:
   OK

📊 Diagnostics:
   Deployment used : gpt-4o
   Elapsed time    : 842ms
   Correlation ID  : 3f8a1c2d-...
   Input tokens    : 14
   Output tokens   : 2

✅ Connectivity check PASSED. Azure OpenAI is reachable.
```

---

## If It Fails

**Missing environment variables:**
```
❌ Missing required environment variables:
   - AZURE_OPENAI_API_KEY
Set them before running. See scripts/set-env.sh or scripts/set-env.ps1
```

**API call fails:**
```
❌ Request failed after 30000ms
   Error: HttpRequestException: Connection refused
```
→ Check your endpoint URL (must include trailing `/`), API key validity, and network connectivity.

---

## Exercises

1. ✏️ Change the test message from `"Say OK"` to `"Say HELLO WORKSHOP"` and observe the response. *(~2 min)*
2. ✏️ Print the full `AgentResponse` object to explore its properties (hint: use `response.Messages`). *(~3 min)*
3. ✏️ Add a second agent run with a different message in the same session and verify the response. *(~3 min)*

---

## Key Takeaways

- **`AIAgent`** is the core abstraction — wraps an `IChatClient` with a system prompt
- **`AgentSession`** holds conversation history for multi-turn interactions
- **`RunAsync()`** sends a message and returns the full `AgentResponse`
- **`AgentResponse.Usage`** gives token counts — useful for cost monitoring
- If this module passes, your environment is ready for the rest of the workshop

---

## Further Reading

- [Azure OpenAI Service documentation](https://learn.microsoft.com/azure/ai-services/openai/)
- [Microsoft.Extensions.AI Overview](https://learn.microsoft.com/dotnet/ai/ai-extensions)
- [Microsoft Agent Framework (GitHub)](https://github.com/microsoft/Agents)

---

**[Next: Module 01 — Hello Agent →](../01_HelloAgent/README.md)**
