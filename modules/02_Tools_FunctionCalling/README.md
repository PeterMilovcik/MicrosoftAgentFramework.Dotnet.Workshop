# Module 02 - Tools and Function Calling

**Duration:** ~35 minutes  
**Goal:** Expose tools to the agent and handle function calling with security guardrails.

---

## What's New in This Module

Building on Module 01's REPL, you'll now give the agent **tools** it can call autonomously:
- **Function calling** — the model decides when to invoke tools
- **Security guardrails** — path traversal protection, extension allowlists
- **Knowledge base search** — grounding answers in real data

---

## Prerequisites

- Module 01 concepts (REPL, agent, session, streaming)
- Understanding of how `AIAgent` is created

---

## Purpose

Teach the agent to use tools to answer questions grounded in real data:

- Register tools using `AIFunctionFactory.Create()`
- Pass tools to `IChatClient.AsAIAgent(instructions, tools: tools)`
- Implement path-traversal protection and extension allowlists
- Observe the agent autonomously deciding when and which tools to call

---

## Concepts Covered

- `AIFunctionFactory.Create(MethodDelegate)` - wraps a static method as an AI tool
- `[Description("...")]` attribute - provides tool/parameter descriptions to the model
- Tool allowlist pattern: restrict `ReadFile` to `assets/sample-data/` only
- Security guardrails: no `..` traversal, extension allowlist, max file size

---

## Key Code

**Registering tools with `AIFunctionFactory`:**

```csharp
public static IList<AITool> GetTools() =>
[
    AIFunctionFactory.Create(GetTime),
    AIFunctionFactory.Create(ReadFile),
    AIFunctionFactory.Create(SearchKb),
];
```

**Annotating tool functions for the model:**

```csharp
[Description("Reads a file from sample-data. Path is relative (e.g. 'build-log-01.txt').")]
public static string ReadFile(
    [Description("Relative path within sample-data.")] string path)
{
    // Security: block path traversal
    if (path.Contains("..") || Path.IsPathRooted(path))
        return "⛔ Access denied: no path traversal allowed.";
    // ...read and return file contents
}
```

**Passing tools to the agent:**

```csharp
var tools = WorkshopTools.GetTools();
var agent = config.CreateAgent(instructions, tools);
//  → IChatClient.AsAIAgent(instructions, tools: tools)
```

---

## Tools Implemented

| Tool | Description | Restrictions |
|------|-------------|--------------|
| `GetTime()` | Returns current UTC time | None |
| `ReadFile(path)` | Reads a file | `.txt`/`.md` only, within `assets/sample-data/`, max 100 KB |
| `SearchKb(query)` | Keyword search across `kb/*.md` | Top 5 results |

---

## How to Run

```bash
./scripts/run.sh 02
dotnet run --project modules/02_Tools_FunctionCalling
```

---

## Expected Interaction

```
You> What time is it?
Agent> The current time is 2026-02-23T15:42:31.0000000Z.

You> What are our guidelines for flaky tests?
Agent> Our guidelines for handling flaky tests are as follows:
...

You> Show me build-log-01.txt
Agent> Here are the contents of build-log-01.txt:
       [log contents...]
```

---

## Exercises

1. ✏️ **Flaky test policy**: Ask: _"What are our guidelines for flaky tests?"_
   Observe the agent calling `SearchKb` and citing the KB file. *(~3 min)*

2. ✏️ **Release notes summary**: Ask: _"Summarize the release notes and list all action items."_
   The agent should call `SearchKb` and/or `ReadFile` for `kb/release-notes.md`. *(~3 min)*

3. ✏️ **Security test**: Ask the agent to read `../../Directory.Build.props`.
   It should refuse with a `⛔ I cannot fulfill this request...` message. *(~2 min)*

4. ✏️ **Add a tool**: Implement a new tool `ListFiles()` that lists files in `assets/sample-data/`.
   Register it in `WorkshopTools.GetTools()` and test it. *(~10 min)*

💡HINT: Prompt for GitHub Copilot:

```
Implement a new tool `ListFiles()` in #file:WorkshopTools.cs in module `02_Tools_FunctionCalling` that lists files in `assets/sample-data/`. Register it in `WorkshopTools.GetTools()`.
```

---

## Key Takeaways

- **Tools extend the agent's capabilities** — the model decides when to call them
- **`AIFunctionFactory.Create()`** wraps any static method as a callable tool
- **`[Description]`** attributes are critical — they tell the model what each tool/parameter does
- **Security is your responsibility** — the model will try anything your tools allow
- **Grounding** via tools (ReadFile, SearchKb) produces more accurate answers than pure generation

---

**[Next: Module 03 — State, Sessions & Persistence →](../03_State_Sessions_Persistence/README.md)**
