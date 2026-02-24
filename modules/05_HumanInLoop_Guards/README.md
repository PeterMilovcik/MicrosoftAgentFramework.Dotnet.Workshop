# Module 05 - Human-in-the-Loop & Guards

**Duration:** ~25 minutes  
**Goal:** Add human approval gates and per-tool access policies to the workflow.

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

## Tool Policy

| Tool | Policy | Reason |
|------|--------|--------|
| `GetTime` | Always Allow | Read-only, no risk |
| `ReadFile` | Require Approval | Accesses file system content |
| `SearchKb` | Always Allow | Keyword search, no risk |

---

## How to Run

```bash
./scripts/run.sh 05
dotnet run --project modules/05_HumanInLoop_Guards
```

---

## Example Interaction

```
Query> Analyze build-log-01.txt to identify root cause

━━━ Step: PLAN ━━━
[PLAN] • Read build-log-01.txt to find error patterns
       • Search KB for database connection issues
       • Assess confidence level

━━━ 🔐 Human Approval Gate ━━━
Options: [approve] | [revise <feedback>] | [abort]
Decision: revise make the plan shorter and focus only on errors

🔄 Regenerating plan with feedback...

━━━ Step: PLAN ━━━  (iteration 2)
[PLAN] • Read build-log-01.txt
       • Focus on ERROR lines only
       • Identify root cause

━━━ 🔐 Human Approval Gate ━━━
Decision: approve
✅ Plan approved. Proceeding to evidence gathering.

━━━ Step: EVIDENCE ━━━

⚠️  Tool Approval Required: ReadFile
   Arguments: build-log-01.txt
   Approve this tool call? [y/N]: y
   ✅ Approved.

[EVIDENCE] Reading build-log-01.txt...
```

---

## Exercises

1. ✏️ **Security test**: Ask the agent to read `../../Directory.Build.props`. Confirm the path is blocked before the approval gate even triggers.

2. ✏️ **Revise flow**: Start an analysis, then use `revise make it shorter and skip infrastructure details` to refine the plan.

3. ✏️ **Abort test**: Start a workflow and type `abort` at the approval gate. Verify the workflow stops cleanly.

4. ✏️ **Change tool policy**: In `ToolPolicy.cs`, change `SearchKb` to `RequireApproval`. Run again and observe the new approval prompts.
