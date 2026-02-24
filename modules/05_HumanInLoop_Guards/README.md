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
  Generating analysis plan...
[PLAN] **Plan to Analyze `build-log-01.txt` for Root Cause:**
...

━━━ 🔐 Human Approval Gate ━━━
Options: [approve] | [revise <feedback>] | [abort]
Your decision: revise make the plan shorter and focus only on errors
🔄 Regenerating plan with your feedback...
━━━ Step: PLAN ━━━
  Generating analysis plan...
[PLAN] ### Shortened Plan to Analyze `build-log-01.txt`
...

━━━ 🔐 Human Approval Gate ━━━
Options: [approve] | [revise <feedback>] | [abort]
Your decision: approve
✅ Plan approved. Proceeding to evidence gathering.
━━━ Step: EVIDENCE ━━━
  Gathering evidence (ReadFile requires approval)...
[EVIDENCE] ### Revised Plan to Analyze `build-log-01.txt`
...

I'll proceed with extracting errors now.
⚠️  Tool Approval Required: ReadFile
   Arguments: build-log-01.txt
   Approve this tool call? [y/N]: y
   ✅ Approved.
### Evidence Collected
...

━━━ Step: CRITIQUE ━━━
  Evaluating evidence quality...
[CRITIQUE] Here is a critique of the provided evidence and plan for analyzing the root cause based on the user's feedback and goal to focus only on errors:
...

══════════════════════════════════════════
 STRUCTURED OUTPUT
══════════════════════════════════════════
{
  "summary": "Build pipeline failed due to unit test failures caused by service connection issues.",
  "evidence": [
    "Three unit tests failed with the error \u0027HttpRequestException: Connection refused (127.0.0.1:5432)\u0027:",
    "1. AuthService.Tests.Integration.UserLoginFlowTest.ShouldIssueTokenOnValidCredentials",
    "2. AuthService.Tests.Integration.UserLoginFlowTest.ShouldRefreshExpiredToken",
    "3. AuthService.Tests.Integration.AdminRoleAssignmentTest.ShouldAssignRoleToNewUser",
    "Error context indicates these tests could not connect to a service (PostgreSQL expected on 127.0.0.1:5432).",
    "Test summary: 344 tests passed, 3 failed. Build concluded with Exit Code 1."
  ],
  "recommendations": [
    "Verify the PostgreSQL database or service is running and accessible locally on port 5432 during the build process.",
    "Check the pipeline configuration or environment variables to ensure the database is properly initialized before tests are executed.",
    "Review any recent changes to the authentication or service configuration that may affect connectivity."
  ],
  "confidence": 0.95
}
```

---

## Exercises

1. ✏️ **Security test**: Ask the agent to read `../../Directory.Build.props`. Confirm the path is blocked before the approval gate even triggers.

2. ✏️ **Revise flow**: Start an analysis, then use `revise make it shorter and skip infrastructure details` to refine the plan.

3. ✏️ **Abort test**: Start a workflow and type `abort` at the approval gate. Verify the workflow stops cleanly.

4. ✏️ **Change tool policy**: In `ToolPolicy.cs`, change `SearchKb` to `RequireApproval`. Run again and observe the new approval prompts.
