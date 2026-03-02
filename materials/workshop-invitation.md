# Workshop Invitation — Building AI Agents with Microsoft Agent Framework

> **Use this template to invite participants.** Replace all `[PLACEHOLDER]` values before sending.

---

**Subject:** You're Invited: Building AI Agents with Microsoft Agent Framework (.NET) — [DATE]

---

Hi [TEAM / NAME],

You are invited to a **hands-on workshop** where you will learn to build AI agents in **C# and .NET** using the **Microsoft Agent Framework** and **Azure OpenAI**.

Over the course of the session, you will go from a simple "Hello Agent" all the way to **multi-agent orchestration** — building a realistic **software triage assistant** that reads build logs, searches a knowledge base, and produces structured incident cards.

| | |
|---|---|
| **Date** | [DATE] |
| **Time** | [START TIME] – [END TIME] |
| **Location** | [ROOM / TEAMS LINK] |
| **Duration** | ~3 hours (core modules) — with optional advanced extension (~4+ hours total) |
| **Level** | Intermediate .NET developers |

### What You Will Learn

- Creating and configuring AI agents with system prompts
- Registering C# methods as tools for LLM function calling
- Managing conversation state and session persistence
- Building multi-step LLM workflows (Plan → Evidence → Critique → Final)
- Implementing human-in-the-loop approval gates and guardrails
- Multi-agent orchestration patterns: Group Chat, Handoff routing, and Magentic-One

### What You Will Build

A **Triage Assistant** that autonomously analyzes build failures, gathers evidence from a knowledge base, and produces structured incident cards — a pattern directly applicable to DevOps and SRE workflows.

---

## Preparation Instructions

> **Please complete ALL steps below before the workshop day.** Module 00 (Connectivity Check) must pass. If you get stuck, contact [ORGANIZER NAME / EMAIL] for help.

### 1. Install Required Software

| Tool | Required Version | Verify With |
|------|-----------------|-------------|
| **.NET SDK** | **10.0.100** or later | `dotnet --version` |
| **Editor** | VS Code with C# Dev Kit, Visual Studio 2022+, or JetBrains Rider | — |
| **Git** | Any recent version | `git --version` |

### 2. Clone the Repository

```powershell
git clone https://github.com/PeterMilovcik/MicrosoftAgentFramework.Dotnet.Workshop
cd MicrosoftAgentFramework.Dotnet.Workshop
```

### 3. Restore and Build

Run this from the repository root to verify your SDK and packages are working:

```powershell
dotnet restore AgentFrameworkWorkshop.slnx
dotnet build AgentFrameworkWorkshop.slnx
```

> **Corporate networks:** If NuGet restore fails, see the [Troubleshooting guide](https://github.com/PeterMilovcik/MicrosoftAgentFramework.Dotnet.Workshop/blob/main/README.md#troubleshooting) and confirm that `nuget.org` is an enabled package source:
>
> ```powershell
> dotnet nuget list source
> ```

### 4. Azure OpenAI Credentials

You will need access to an **Azure OpenAI** resource with a deployed chat-completion model (e.g., `gpt-4o`). Credentials will either be provided by the organizer on the day, or you can bring your own. Detailed setup instructions are in the [repository README](https://github.com/PeterMilovcik/MicrosoftAgentFramework.Dotnet.Workshop/blob/main/README.md).

### Pre-Workshop Checklist

- [ ] .NET 10 SDK installed (`dotnet --version` shows `10.0.x`)
- [ ] Repository cloned and `dotnet build` succeeds with no errors
- [ ] Editor of choice ready with C# support

---

### What to Bring

- Your laptop (and charger!) with the setup above completed
- Curiosity about AI agents!

### Questions?

Contact [ORGANIZER NAME] at [ORGANIZER EMAIL] if you run into any issues with setup.

We look forward to seeing you there!

Best regards,
[ORGANIZER NAME / TEAM]
