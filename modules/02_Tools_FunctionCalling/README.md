# Module 02 – Tools and Function Calling

**Duration:** ~35 minutes  
**Goal:** Expose tools to the agent and handle function calling with security guardrails.

---

## Purpose

Teach the agent to use tools to answer questions grounded in real data:

- Register tools using `AIFunctionFactory.Create()`
- Pass tools to `IChatClient.AsAIAgent(instructions, tools: tools)`
- Implement path-traversal protection and extension allowlists
- Observe the agent autonomously deciding when and which tools to call

---

## Concepts Covered

- `AIFunctionFactory.Create(MethodDelegate)` – wraps a static method as an AI tool
- `[Description("...")]` attribute – provides tool/parameter descriptions to the model
- Tool allowlist pattern: restrict `ReadFile` to `assets/sample-data/` only
- Security guardrails: no `..` traversal, extension allowlist, max file size

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

### Definition:
- A **flaky test** is one that fails and passes inconsistently without any code changes.

### Policy:
1. **Classification**:
   - A test is considered flaky if it fails 2 or more times i
...

You> Show me build-log-01.txt
Agent> Here are the contents of build-log-01.txt:
       [log contents...]
```

---

## Exercises

1. ✏️ **Flaky test policy**: Ask: _"What are our guidelines for flaky tests?"_
   Observe the agent calling `SearchKb` and citing the KB file.

2. ✏️ **Release notes summary**: Ask: _"Summarize the release notes and list all action items."_
   The agent should call `SearchKb` and/or `ReadFile` for `kb/release-notes.md`.

3. ✏️ **Security test**: Ask the agent to read `../../Directory.Build.props`.
   It should refuse with a `⛔ I cannot fulfill this request...` message.

4. ✏️ **Add a tool**: Implement a new tool `ListFiles()` that lists files in `assets/sample-data/`.
   Register it in `WorkshopTools.GetTools()` and test it.

HINT: Prompt for GitHub Copilot:

```
Implement a new tool `ListFiles()` in #file:WorkshopTools.cs in module `02_Tools_FunctionCalling` that lists files in `assets/sample-data/`. Register it in `WorkshopTools.GetTools()`.
```
