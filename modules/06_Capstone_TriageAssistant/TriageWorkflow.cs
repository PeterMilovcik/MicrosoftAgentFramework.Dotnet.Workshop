using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Workshop.Common;

namespace CapstoneTriageAssistant;

/// <summary>
/// Full triage workflow with human approval gate and structured output.
/// </summary>
internal static class TriageWorkflow
{
    public static async Task<(string HumanSummary, TriageCard? Card)> RunAsync(
        AIAgent agent,
        string failureReport,
        string? logFileName,
        string? kbQuery,
        CancellationToken ct = default)
    {
        // Build context string
        var contextParts = new StringBuilder();
        contextParts.AppendLine("== Failure Report ==");
        contextParts.AppendLine(failureReport);

        if (!string.IsNullOrWhiteSpace(logFileName))
            contextParts.AppendLine($"\n== Log File Requested: {logFileName} ==\n(Use ReadFile tool to load it)");

        if (!string.IsNullOrWhiteSpace(kbQuery))
            contextParts.AppendLine($"\n== KB Query: {kbQuery} ==\n(Use SearchKb tool to find relevant info)");

        var context = contextParts.ToString();

        // ---- Step 1: Plan (with approval gate) ----
        string planResult;
        var planIteration = 0;
        var augmentedQuery = context;

        while (true)
        {
            planIteration++;
            PrintHeader("PLAN", $"Generating triage plan (iteration {planIteration})...");
            var planSession = await agent.CreateSessionAsync(ct);
            var planPrompt = $"""
                You are a software triage assistant. Analyze this failure report and produce a SHORT triage plan:

                {augmentedQuery}

                Plan should include (3-5 points):
                - What type of failure this appears to be (infra/product/test)
                - What files or KB entries to examine
                - What information is still needed
                - Confidence level (low/medium/high)
                """;

            planResult = await RunAndCollectAsync(agent, planPrompt, planSession, "PLAN", ct);

            // Human approval gate
            Console.WriteLine();
            Console.WriteLineColorful("━━━ 🔐 Human Approval Gate ━━━", ConsoleColor.Yellow);
            Console.WriteLine("Options: [approve] | [revise <feedback>] | [abort]");
            Console.Write("Decision: ");

            var decision = Console.ReadLine()?.Trim() ?? "";

            if (decision.Equals("approve", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLineColorful("✅ Plan approved.", ConsoleColor.Green);
                break;
            }

            if (decision.Equals("abort", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLineColorful("🛑 Triage aborted.", ConsoleColor.Red);
                return ("Aborted", null);
            }

            if (decision.StartsWith("revise", StringComparison.OrdinalIgnoreCase))
            {
                var feedback = decision.Length > 6 ? decision[6..].Trim() : "";
                if (string.IsNullOrWhiteSpace(feedback))
                {
                    Console.Write("Enter revision feedback: ");
                    feedback = Console.ReadLine()?.Trim() ?? "";
                }
                augmentedQuery = $"{context}\n\nUser plan revision feedback: {feedback}";
                Console.WriteLineColorful("🔄 Regenerating plan with feedback...", ConsoleColor.Yellow);
                continue;
            }

            Console.WriteLine("Please type: approve | revise <feedback> | abort");
        }

        // ---- Step 2: Gather Evidence ----
        PrintHeader("EVIDENCE", "Gathering evidence from logs and KB...");
        var evidenceSession = await agent.CreateSessionAsync(ct);
        var evidencePrompt = $"""
            Based on the triage plan:
            {planResult}

            And the failure context:
            {context}

            Use the tools (ReadFile for log files, SearchKb for knowledge base) to gather concrete evidence.
            List each piece of evidence with its source.
            {(logFileName is not null ? $"Make sure to read: {logFileName}" : "")}
            {(kbQuery is not null ? $"Make sure to search KB for: {kbQuery}" : "")}
            """;
        var evidenceResult = await RunAndCollectAsync(agent, evidencePrompt, evidenceSession, "EVIDENCE", ct);

        // ---- Step 3: Critique ----
        PrintHeader("CRITIQUE", "Evaluating evidence and identifying gaps...");
        var critiqueSession = await agent.CreateSessionAsync(ct);
        var critiquePrompt = $"""
            Critique the triage evidence:
            {evidenceResult}

            For the failure: {failureReport}

            Are there gaps? Unsupported claims? What would increase confidence?
            """;
        var critiqueResult = await RunAndCollectAsync(agent, critiquePrompt, critiqueSession, "CRITIQUE", ct);

        // ---- Step 4: Finalize ----
        PrintHeader("FINAL", "Producing Triage Card...");
        var finalSession = await agent.CreateSessionAsync(ct);
        var finalPrompt = $$"""
            Produce a final triage card for this failure report:
            {{failureReport}}

            Using:
            Plan: {{planResult}}
            Evidence: {{evidenceResult}}
            Critique: {{critiqueResult}}

            Respond ONLY with a valid JSON Triage Card (no markdown fences) using this exact schema:
            {
              "summary": "One or two sentence plain-language summary for a non-technical stakeholder",
              "category": "infra | product | test",
              "suspected_areas": ["area1", "area2"],
              "next_steps": ["Step 1: ...", "Step 2: ..."],
              "suggested_owner_role": "dev | ops | qa | arch",
              "confidence": 0.85
            }
            """;
        var finalResult = await RunAndCollectAsync(agent, finalPrompt, finalSession, "FINAL", ct);

        // Parse Triage Card
        TriageCard? card = null;
        var start = finalResult.IndexOf('{');
        var end = finalResult.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            try
            {
                card = JsonSerializer.Deserialize<TriageCard>(finalResult[start..(end + 1)],
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { /* fall through */ }
        }

        return (card?.Summary ?? finalResult, card);
    }

    private static async Task<string> RunAndCollectAsync(
        AIAgent agent, string prompt, AgentSession session, string label, CancellationToken ct)
    {
        var sb = new StringBuilder();
        Console.WriteColorful($"[{label}] ", ConsoleColor.DarkGray);

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
        Console.WriteLineColorful($"━━━ Step: {step} ━━━", ConsoleColor.Magenta);
        Console.WriteLineColorful($"  {desc}", ConsoleColor.DarkGray);
    }
}
