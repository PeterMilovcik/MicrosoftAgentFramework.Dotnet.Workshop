using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace HandoffOrchestration;

/// <summary>
/// Handoff workflow: frontdesk routes to a specialist (infra-expert, product-expert, or test-expert),
/// which then hands off to scribe for the final triage card.
/// Only experts may call tools; scribe and frontdesk may not.
/// Handoff transitions are printed explicitly.
/// </summary>
internal static class HandoffWorkflow
{
    public static async Task<(string FinalText, TriageCard? Card)> RunAsync(
        AgentConfig config,
        string failureReport,
        string? logFileName,
        string? kbQuery,
        CancellationToken ct = default)
    {
        var baseDir = AppContext.BaseDirectory;
        var expertTools = WorkshopTools.GetExpertTools();

        // Load agent prompts
        string LoadPrompt(string name)
        {
            var path = Path.Combine(baseDir, "assets", "prompts", "agents", $"{name}.md");
            return File.Exists(path) ? File.ReadAllText(path) : $"You are the {name} agent.";
        }

        // Build context
        var context = BuildContext(failureReport, logFileName, kbQuery);

        // Create agents
        // Frontdesk: no tools — only routes
        var frontdesk = config.CreateNamedAgent(
            LoadPrompt("frontdesk"),
            name: "frontdesk",
            description: "Triages incoming failure reports and routes to the appropriate expert");

        // Expert agents: have tools; hand off to scribe when done
        var infraExpert = config.CreateNamedAgent(
            LoadPrompt("infra-expert"),
            name: "infra-expert",
            description: "Expert in CI/CD, infrastructure, environments, networking, and deployment failures",
            tools: expertTools);

        var productExpert = config.CreateNamedAgent(
            LoadPrompt("product-expert"),
            name: "product-expert",
            description: "Expert in application code bugs, regressions, null references, and logic defects",
            tools: expertTools);

        var testExpert = config.CreateNamedAgent(
            LoadPrompt("test-expert"),
            name: "test-expert",
            description: "Expert in test flakiness, non-determinism, assertions, and test setup issues",
            tools: expertTools);

        // Scribe: no tools — only produces final output
        var scribe = config.CreateNamedAgent(
            LoadPrompt("scribe"),
            name: "scribe",
            description: "Produces the final structured JSON triage card from all gathered evidence");

        // Build handoff workflow
        // frontdesk starts; can hand off to any of the three experts
        // each expert hands off to scribe
        var handoffBuilder = AgentWorkflowBuilder.CreateHandoffBuilderWith(frontdesk);

        // frontdesk → experts (routing decision made by frontdesk LLM)
        handoffBuilder.WithHandoff(frontdesk, infraExpert,
            "Route here when failure involves infra, CI/CD, pipelines, timeouts, or network issues");
        handoffBuilder.WithHandoff(frontdesk, productExpert,
            "Route here when failure involves stack traces, null refs, regressions, or code bugs");
        handoffBuilder.WithHandoff(frontdesk, testExpert,
            "Route here when failure involves flaky tests, assertions, test setup, or non-determinism");

        // experts → scribe (after investigation, hand off for final output)
        handoffBuilder.WithHandoffs([infraExpert, productExpert, testExpert], scribe,
            "Hand off here after investigation is complete to produce the final JSON triage card");

        var workflow = handoffBuilder.Build();

        // Track agent turns for printing transitions
        var lastAgentId = "";
        var lastScribeText = "";

        PrintHeader("HANDOFF WORKFLOW", "Starting handoff-based triage...");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  frontdesk → [infra-expert | product-expert | test-expert] → scribe");
        Console.ResetColor();
        Console.WriteLine();

        // Run the workflow
        await using var streamingRun = await InProcessExecution.RunStreamingAsync(workflow, context, Guid.NewGuid().ToString(), ct);

        await foreach (var evt in streamingRun.WatchStreamAsync(ct))
        {
            if (evt is AgentResponseEvent agentEvt)
            {
                var agentName = agentEvt.ExecutorId ?? "unknown";
                var text = agentEvt.Response?.Text ?? "";
                if (string.IsNullOrWhiteSpace(text)) continue;

                // Print handoff transition when agent changes
                if (!string.IsNullOrEmpty(lastAgentId) && lastAgentId != agentName)
                {
                    PrintHandoff(lastAgentId, agentName);
                }

                PrintAgentTurn(agentName, text);
                lastAgentId = agentName;

                if (agentName.Equals("scribe", StringComparison.OrdinalIgnoreCase))
                {
                    lastScribeText = text;
                }
            }
        }

        var card = ParseTriageCard(lastScribeText);
        return (lastScribeText, card);
    }

    private static string BuildContext(string failureReport, string? logFileName, string? kbQuery)
    {
        var sb = new StringBuilder();
        sb.AppendLine("== Failure Report ==");
        sb.AppendLine(failureReport);

        if (!string.IsNullOrWhiteSpace(logFileName))
            sb.AppendLine($"\n== Log File Available: {logFileName} ==\n(Expert agents: use ReadFile to load it)");

        if (!string.IsNullOrWhiteSpace(kbQuery))
            sb.AppendLine($"\n== KB Query Hint: {kbQuery} ==\n(Expert agents: use SearchKb with this query)");

        sb.AppendLine("\n== Instructions ==");
        sb.AppendLine("Analyze this failure and route it to the appropriate specialist.");

        return sb.ToString();
    }

    private static TriageCard? ParseTriageCard(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return null;
        try
        {
            return JsonSerializer.Deserialize<TriageCard>(text[start..(end + 1)],
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { return null; }
    }

    private static void PrintHandoff(string from, string to)
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine($"  ⟶ Handoff: {from} → {to}");
        Console.ResetColor();
        Console.WriteLine();
    }

    private static void PrintAgentTurn(string agentName, string text)
    {
        var (color, prefix) = agentName.ToLowerInvariant() switch
        {
            "frontdesk" => (ConsoleColor.White, "[FRONTDESK]"),
            "infra-expert" => (ConsoleColor.Cyan, "[INFRA-EXPERT]"),
            "product-expert" => (ConsoleColor.Blue, "[PRODUCT-EXPERT]"),
            "test-expert" => (ConsoleColor.Magenta, "[TEST-EXPERT]"),
            "scribe" => (ConsoleColor.Green, "[SCRIBE]"),
            _ => (ConsoleColor.Gray, $"[{agentName.ToUpper()}]"),
        };

        Console.ForegroundColor = color;
        Console.Write($"\n{prefix} ");
        Console.ResetColor();
        Console.WriteLine(text);
        Console.WriteLine();
    }

    private static void PrintHeader(string step, string desc)
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"━━━ {step} ━━━");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  {desc}");
        Console.ResetColor();
    }
}
