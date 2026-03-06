# Common — Shared Workshop Library

Shared classes referenced by all workshop modules via `ProjectReference`.

## Contents

| File | Purpose |
|------|---------|
| `AgentConfig.cs` | Azure OpenAI configuration loading, `IChatClient` / `AIAgent` creation, token usage tracking |
| `ConsoleExtensions.cs` | C# 14 extension methods on `Console` for colored output (`WriteColorful`, `WriteLineColorful`, `WriteError`, `WriteLineError`) |
| `TriageCard.cs` | Structured triage card DTO with JSON serialization attributes (used by modules 06–09) |
| `WorkshopTools.cs` | Sandbox-safe AI tools — `GetTime()`, `ReadFile()`, `SearchKb()` — exposed via `GetTools()` |

## Usage

All modules include:

```xml
<ProjectReference Include="..\Common\Common.csproj" />
```

Types are available under the `Workshop.Common` namespace:

```csharp
using Workshop.Common;
```

## Note

Module 05 (`HumanInLoop_Guards`) maintains its own `WorkshopTools.cs` with tool-policy enforcement wrapping. It references the local version explicitly via `HumanInLoopGuards.WorkshopTools.GetTools()`.
