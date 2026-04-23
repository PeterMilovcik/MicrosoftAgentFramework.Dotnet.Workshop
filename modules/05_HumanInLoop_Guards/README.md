# Module 05 - Human-in-the-Loop & Guards

**Duration:** ~25 minutes  
**Goal:** Add human approval gates and per-tool access policies to the workflow.

---

## What's New in This Module

Building on Module 04's multi-step workflow, you'll add **safety controls**:
- **Human approval gate** — user must approve/revise/abort the plan before proceeding
- **Per-tool access policies** — `AlwaysAllow`, `RequireApproval`, or `Deny` per tool
- **Iterative revision** — refine the plan with feedback before committing
- All enforcement happens at the **application layer**, not the model

---

## Prerequisites

- Module 04 concepts (multi-step workflow, step composition)
- Module 02 concepts (tools and function calling)

---

## Purpose

Understand how to keep humans in control of agent decisions:

- Human must approve (or revise/abort) the plan before proceeding
- Individual tools can require explicit approval for each call
- The "revise" flow allows iterative plan refinement with feedback
- Policy enforcement happens at the application layer (not the model)

---

## Concepts Covered

- Approval gate: `approve` / `revise <feedback>` / `abort` loop
- `ToolApprovalPolicy` enum: `AlwaysAllow` / `RequireApproval` / `Deny`
- Per-tool policy in `ToolPolicy.cs`
- Iterative plan refinement with user feedback appended to prompt
- Why human-in-the-loop matters for high-risk tool calls

---

## Key Code

**ToolPolicy wiring — how policies connect to tools:**

The `ToolPolicy` class defines a policy dictionary and an approval prompt:

```csharp
// ToolPolicy.cs — defines per-tool access rules
private static readonly Dictionary<string, ToolApprovalPolicy> Policies = new()
{
    ["GetTime"]  = ToolApprovalPolicy.AlwaysAllow,
    ["ReadFile"] = ToolApprovalPolicy.RequireApproval,
    ["SearchKb"] = ToolApprovalPolicy.AlwaysAllow,
};

public static ToolApprovalPolicy GetPolicy(string toolName)
    => Policies.TryGetValue(toolName, out var policy) ? policy : ToolApprovalPolicy.RequireApproval;
```

Each tool function calls `EnforcePolicy()` before executing, which checks the policy and prompts the user if needed:

```csharp
// WorkshopTools.cs — enforcement wired inside each tool
private static string? EnforcePolicy(string toolName, string args)
{
    var policy = ToolPolicy.GetPolicy(toolName);
    return policy switch
    {
        ToolApprovalPolicy.Deny => $"⛔ Tool '{toolName}' is denied by policy.",
        ToolApprovalPolicy.RequireApproval when !ToolPolicy.RequestApproval(toolName, args)
            => $"⛔ Tool '{toolName}' was denied by the user.",
        _ => null, // AlwaysAllow or user approved
    };
}

[Description("Reads a file from sample-data.")]
public static string ReadFile([Description("Relative path")] string path)
{
    // Policy check BEFORE any file I/O
    var denied = EnforcePolicy("ReadFile", path);
    if (denied is not null) return denied;
    // ...proceed with reading
}
```

> **Key insight:** The policy is enforced at the tool level, not the model level.
> The model doesn't know about policies — it calls tools normally and gets back either
> results or denial messages. This keeps the security boundary in application code.

**Human approval gate (plan step):**

```csharp
Console.WriteLine("Options: [approve] | [revise <feedback>] | [abort]");
var decision = Console.ReadLine()?.Trim() ?? "";

if (decision.StartsWith("revise", StringComparison.OrdinalIgnoreCase))
{
    var feedback = decision[6..].Trim();
    userQuery = $"{userQuery}\n\nUser feedback on plan: {feedback}";
    // Loop back to regenerate plan with feedback incorporated
}
```

---

## Tool Policy

| Tool | Policy | Reason |
|------|--------|--------|
| `GetTime` | Always Allow | Read-only, no risk |
| `ReadFile` | Require Approval | Accesses file system content |
| `SearchKb` | Always Allow | Keyword search, no risk |

---

## How to Run

```bash
./scripts/run.sh 05       # Linux/macOS
./scripts/run.ps1 05      # Windows PowerShell
dotnet run --project modules/05_HumanInLoop_Guards
```

---

## Example Interaction

```
Query> Analyze build-log-01.txt to identify root cause

━━━ Step: PLAN ━━━
[PLAN] **Plan to Analyze `build-log-01.txt` for Root Cause:** ...

━━━ 🔐 Human Approval Gate ━━━
Options: [approve] | [revise <feedback>] | [abort]
Your decision: revise make the plan shorter and focus only on errors
🔄 Regenerating plan with your feedback...

━━━ Step: PLAN ━━━
[PLAN] ### Shortened Plan ...

━━━ 🔐 Human Approval Gate ━━━
Your decision: approve
✅ Plan approved. Proceeding to evidence gathering.

━━━ Step: EVIDENCE ━━━
⚠️  Tool Approval Required: ReadFile
   Arguments: build-log-01.txt
   Approve this tool call? [y/N]: y
   ✅ Approved.
[EVIDENCE] ### Evidence Collected ...

━━━ Step: CRITIQUE ━━━
[CRITIQUE] Critique of the evidence ...

══════════════════════════════════════════
 STRUCTURED OUTPUT
══════════════════════════════════════════
{ "summary": "...", "confidence": 0.95 }
```

---

## Exercises

1. ✏️ **Security test**: Ask the agent to read `../../Directory.Build.props`. Confirm the path is blocked before the approval gate even triggers. *(~2 min)*

2. ✏️ **Revise flow**: Start an analysis, then use `revise make it shorter and skip infrastructure details` to refine the plan. *(~3 min)*

3. ✏️ **Abort test**: Start a workflow and type `abort` at the approval gate. Verify the workflow stops cleanly. *(~2 min)*

4. ✏️ **Change tool policy**: In `ToolPolicy.cs`, change `SearchKb` to `RequireApproval`. Run again and observe the new approval prompts. *(~3 min)*

5. ✏️ **Add a Deny test**: In `ToolPolicy.cs`, set `ReadFile` to `Deny`. Ask the agent to analyze a build log — see what happens when the tool is completely blocked. *(~3 min)*

💡HINT: Prompt for GitHub Copilot:

```
In #file:ToolPolicy.cs for module 05_HumanInLoop_Guards, change the policy for ReadFile from RequireApproval to Deny. What happens when the agent tries to use it?
```

---

## Key Takeaways

- **Human-in-the-loop** is not just a checkbox — it's a design pattern for responsible AI
- **Policies live in application code**, not in the model — the model just sees allow/deny results
- **Unknown tools default to `RequireApproval`** — fail-safe by design
- **Iterative revision** lets the user refine plans without starting over
- **Separate security from capability** — validate paths BEFORE showing the approval prompt

---

**[Next: Module 06 — Capstone Triage Assistant →](../06_Capstone_TriageAssistant/README.md)**
