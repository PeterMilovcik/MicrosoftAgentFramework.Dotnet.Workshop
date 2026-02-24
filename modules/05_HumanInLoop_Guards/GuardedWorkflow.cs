using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace HumanInLoopGuards;

/// <summary>
/// A workflow with human approval gates at key decision points.
/// Gate 1: User approves the plan before evidence gathering begins.
/// Gate 2: ReadFile tool requires explicit approval each time.
/// </summary>
internal static class GuardedWorkflow
{
    public static async Task RunAsync(AIAgent agent, string userQuery, CancellationToken ct = default)
    {
        // ---- Step 1: Plan (with approval gate) ----
        string planResult;
        while (true)
        {
            PrintHeader("PLAN", "Generating analysis plan...");
            var planSession = await agent.CreateSessionAsync(ct);
            var planPrompt = $"""
                Produce a SHORT plan (3-5 bullet points) to analyze:
                {userQuery}

                Include: what information to gather, which tools to use, potential risks.
                """;

            planResult = await RunAndCollectAsync(agent, planPrompt, planSession, "PLAN", ct);

            // Human approval gate
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("━━━ 🔐 Human Approval Gate ━━━");
            Console.ResetColor();
            Console.WriteLine("Options: [approve] | [revise <feedback>] | [abort]");
            Console.Write("Your decision: ");

            var decision = Console.ReadLine()?.Trim() ?? "";

            if (decision.Equals("approve", StringComparison.OrdinalIgnoreCase))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✅ Plan approved. Proceeding to evidence gathering.");
                Console.ResetColor();
                break;
            }

            if (decision.Equals("abort", StringComparison.OrdinalIgnoreCase))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("🛑 Workflow aborted by user.");
                Console.ResetColor();
                return;
            }

            if (decision.StartsWith("revise", StringComparison.OrdinalIgnoreCase))
            {
                var feedback = decision.Length > 6 ? decision[6..].Trim() : "";
                if (string.IsNullOrWhiteSpace(feedback))
                {
                    Console.Write("Enter your revision feedback: ");
                    feedback = Console.ReadLine()?.Trim() ?? "";
                }
                userQuery = $"{userQuery}\n\nUser feedback on plan: {feedback}";
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("🔄 Regenerating plan with your feedback...");
                Console.ResetColor();
                continue; // loop back to regenerate plan
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("❌ Unknown option. Please type: approve | revise <feedback> | abort");
            Console.ResetColor();
        }

        // ---- Step 2: Gather Evidence (with ReadFile gating) ----
        PrintHeader("EVIDENCE", "Gathering evidence (ReadFile requires approval)...");
        var evidenceSession = await agent.CreateSessionAsync(ct);
        var evidencePrompt = $"""
            Based on this plan:
            {planResult}

            Gather evidence for: {userQuery}
            Use ReadFile and SearchKb as appropriate. List each evidence with its source.
            """;
        var evidenceResult = await RunAndCollectAsync(agent, evidencePrompt, evidenceSession, "EVIDENCE", ct);

        // ---- Step 3: Critique ----
        PrintHeader("CRITIQUE", "Evaluating evidence quality...");
        var critiqueSession = await agent.CreateSessionAsync(ct);
        var critiquePrompt = $"""
            Critique the following evidence for: {userQuery}
            Evidence: {evidenceResult}
            What is missing? Any unsupported claims?
            """;
        var critiqueResult = await RunAndCollectAsync(agent, critiquePrompt, critiqueSession, "CRITIQUE", ct);

        // ---- Step 4: Final ----
        PrintHeader("FINAL", "Producing final structured output...");
        var finalSession = await agent.CreateSessionAsync(ct);
        var finalPrompt = $$"""
            Produce a final JSON-only analysis for: {{userQuery}}
            Plan: {{planResult}}
            Evidence: {{evidenceResult}}
            Critique: {{critiqueResult}}

            Respond ONLY with valid JSON (no markdown fences):
            {
              "summary": "...",
              "evidence": ["...", "..."],
              "recommendations": ["...", "..."],
              "confidence": 0.85
            }
            """;
        var finalResult = await RunAndCollectAsync(agent, finalPrompt, finalSession, "FINAL", ct);

        // Print formatted JSON
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("══════════════════════════════════════════");
        Console.WriteLine(" STRUCTURED OUTPUT");
        Console.WriteLine("══════════════════════════════════════════");
        Console.ResetColor();

        var start = finalResult.IndexOf('{');
        var end = finalResult.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            try
            {
                var doc = JsonDocument.Parse(finalResult[start..(end + 1)]);
                Console.WriteLine(JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { Console.WriteLine(finalResult); }
        }
        else
        {
            Console.WriteLine(finalResult);
        }

        Console.WriteLine();
    }

    private static async Task<string> RunAndCollectAsync(
        AIAgent agent, string prompt, AgentSession session, string label, CancellationToken ct)
    {
        var sb = new StringBuilder();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"[{label}] ");
        Console.ResetColor();

        await foreach (var update in agent.RunStreamingAsync(prompt, session).WithCancellation(ct))
        {
            Console.Write(update.Text);
            sb.Append(update.Text);
        }
        Console.WriteLine();
        Console.WriteLine();
        return sb.ToString();
    }

    private static void PrintHeader(string step, string desc)
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"━━━ Step: {step} ━━━");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  {desc}");
        Console.ResetColor();
    }
}
