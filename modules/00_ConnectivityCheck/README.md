# Module 00 - Connectivity Check

**Duration:** ~10 minutes  
**Goal:** Verify your environment is correctly configured and Azure OpenAI is reachable.

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

1. ✏️ Change the test message from `"Say OK"` to `"Say HELLO WORKSHOP"` and observe the response.
2. ✏️ Print the full `AgentResponse` object to explore its properties (hint: use `response.Messages`).
3. ✏️ Add a second agent run with a different message in the same session and verify the response.
