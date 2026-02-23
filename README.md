# Microsoft Agent Framework – .NET 10 Workshop

A **3-hour hands-on workshop** for building AI agents with the **Microsoft Agent Framework** in **.NET 10** using console applications.

No Docker. No web UI. Just C#, NuGet, and Azure OpenAI.

---

## Prerequisites

| Requirement | Details |
|-------------|---------|
| .NET SDK | 10.0.102+ (pinned via `global.json`) |
| Azure OpenAI | An Azure OpenAI resource with a chat model deployment (e.g., `gpt-4o`) |
| Shell | Bash (Linux/macOS) or PowerShell (Windows) |
| Editor | Visual Studio 2022+, VS Code with C# Dev Kit, or Rider |

---

## Environment Variables

All modules use the same four environment variables:

| Variable | Required | Example |
|----------|----------|---------|
| `AZURE_OPENAI_ENDPOINT` | ✅ Yes | `https://myresource.openai.azure.com/` |
| `AZURE_OPENAI_API_KEY` | ✅ Yes | `sk-...` |
| `AZURE_OPENAI_DEPLOYMENT` | ✅ Yes | `gpt-4o` |
| `AZURE_OPENAI_API_VERSION` | ⬜ Optional | `2025-01-01-preview` (default if unset) |

### Setting environment variables

**Linux / macOS:**
```bash
export AZURE_OPENAI_ENDPOINT="https://myresource.openai.azure.com/"
export AZURE_OPENAI_API_KEY="your-api-key"
export AZURE_OPENAI_DEPLOYMENT="gpt-4o"

# Check / diagnose
./scripts/set-env.sh
```

**Windows PowerShell:**
```powershell
$env:AZURE_OPENAI_ENDPOINT = "https://myresource.openai.azure.com/"
$env:AZURE_OPENAI_API_KEY = "your-api-key"
$env:AZURE_OPENAI_DEPLOYMENT = "gpt-4o"

# Check / diagnose
./scripts/set-env.ps1
```

---

## Quickstart

```bash
# 1. Clone the repository
git clone https://github.com/PeterMilovcik/MicrosoftAgentFramework.Dotnet.Workshop
cd MicrosoftAgentFramework.Dotnet.Workshop

# 2. Set environment variables (see above)

# 3. Build everything
dotnet build AgentFrameworkWorkshop.slnx

# 4. Run the connectivity check
./scripts/run.sh 00        # Linux/macOS
./scripts/run.ps1 00       # Windows PowerShell

# 5. Start the workshop – module 01
./scripts/run.sh 01
```

Or run directly:
```bash
dotnet run --project modules/00_ConnectivityCheck
dotnet run --project modules/01_HelloAgent
```

---

## Repository Structure

```
agent-framework-dotnet-workshop/
  README.md                   ← You are here
  AgentFrameworkWorkshop.slnx ← Solution file (.NET 10 format)
  Directory.Packages.props    ← Centralized NuGet version management
  Directory.Build.props       ← Shared build properties (nullable, lang version)
  .editorconfig               ← Coding style
  global.json                 ← Pins .NET SDK version
  assets/
    prompts/                  ← Editable system prompt files
      system-base.md
      system-safety.md
      triage-rubric.md
    sample-data/              ← Realistic data for exercises
      build-log-01.txt
      build-log-02.txt
      kb/
        testing-guidelines.md
        release-notes.md
  scripts/
    set-env.sh / set-env.ps1  ← Diagnose missing env vars
    run.sh / run.ps1          ← Run a module by number
  modules/
    00_ConnectivityCheck/     ← Azure OpenAI connectivity check
    01_HelloAgent/            ← Basic agent + REPL loop
    02_Tools_FunctionCalling/ ← Tools: GetTime, ReadFile, SearchKb
    03_State_Sessions_Persistence/ ← Sessions + JSON persistence
    04_Workflows_MultiStep/   ← 4-step analysis pipeline
    05_HumanInLoop_Guards/    ← Human approval gates + tool policy
    06_Capstone_TriageAssistant/ ← Full triage assistant
```

---

## NuGet Packages Used

| Package | Version | Purpose |
|---------|---------|---------|
| `Microsoft.Agents.AI` | 1.0.0-rc1 | Core Agent Framework |
| `Microsoft.Agents.AI.OpenAI` | 1.0.0-rc1 | OpenAI/Azure OpenAI provider |
| `Microsoft.Extensions.AI` | 10.3.0 | Unified AI abstractions (`IChatClient`) |
| `Microsoft.Extensions.AI.OpenAI` | 10.3.0 | `AsIChatClient()` extension |
| `Azure.AI.OpenAI` | 2.1.0 | Azure OpenAI SDK |

---

## 3-Hour Workshop Timeline

| Time | Module | Topic |
|------|--------|-------|
| 0:00 | **00** | Connectivity check – verify env vars & Azure OpenAI |
| 0:10 | **01** | Hello Agent – system prompt + conversation REPL |
| 0:35 | **02** | Tools & Function Calling – GetTime, ReadFile, SearchKb |
| 1:10 | **03** | State, Sessions & Persistence – JSON session store |
| 1:35 | **04** | Workflows – 4-step analysis pipeline |
| 2:15 | **05** | Human-in-the-Loop – approval gates + tool policy |
| 2:40 | **06** | Capstone – full Triage Assistant |
| 3:00 | 🎉 | Done! |

---

## Editing Prompts

All agent prompts live in `assets/prompts/`. Edit them without touching any code:

- **`system-base.md`** – Core assistant behavior
- **`system-safety.md`** – Tool-use constraints and guardrails
- **`triage-rubric.md`** – Structured output schema for the capstone

Changes take effect immediately on next `dotnet run` (files are copied to build output).

---

## License

MIT – see [LICENSE](LICENSE).
