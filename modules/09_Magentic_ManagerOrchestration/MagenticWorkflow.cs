using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Workshop.Common;

namespace MagenticOrchestration;

/// <summary>
/// Magentic-style orchestration: a manager LLM selects which agent acts next at each step.
/// The manager maintains a rolling progress summary and decides when to stop.
///
/// Team: researcher (tools), diagnostician, critic, scribe (final output).
/// Manager: selects the next agent and provides a task instruction each turn.
///
/// Note: The Microsoft.Agents.AI.Workflows v1.0.0-rc1 package does not yet include
/// a built-in MagenticManager type. This module implements the pattern manually using
/// the AIAgent API directly, demonstrating the core Magentic-One concepts.
/// </summary>
internal static class MagenticWorkflow
{
    private const int MaxIterations = 8;
    private const int MaxToolCalls = 5;
    private const double ConfidenceThreshold = 0.75;

    public static async Task<(string FinalText, TriageCard? Card)> RunAsync(
        AgentConfig config,
        string failureReport,
        string? logFileName,
        string? kbQuery,
        CancellationToken ct = default)
    {
        var baseDir = AppContext.BaseDirectory;

        string LoadPrompt(string name)
        {
            var path = Path.Combine(baseDir, "assets", "prompts", "agents", $"{name}.md");
            return File.Exists(path) ? File.ReadAllText(path) : $"You are the {name} agent.";
        }

        // Create agents
        var managerAgent = config.CreateAgent(LoadPrompt("magentic-manager"));
        var researcherAgent = config.CreateAgent(LoadPrompt("researcher"),
            tools: WorkshopTools.GetTools());
        var diagnosticianAgent = config.CreateAgent(LoadPrompt("diagnostician"));
        var criticAgent = config.CreateAgent(LoadPrompt("critic"));
        var scribeAgent = config.CreateAgent(LoadPrompt("scribe"));

        var agentMap = new Dictionary<string, AIAgent>(StringComparer.OrdinalIgnoreCase)
        {
            ["researcher"] = researcherAgent,
            ["diagnostician"] = diagnosticianAgent,
            ["critic"] = criticAgent,
            ["scribe"] = scribeAgent,
        };

        // Build the shared context string for the initial manager prompt
        var contextSb = new StringBuilder();
        contextSb.AppendLine("== Failure Report ==");
        contextSb.AppendLine(failureReport);
        if (!string.IsNullOrWhiteSpace(logFileName))
            contextSb.AppendLine($"\n== Log File Available: {logFileName} ==\n(researcher: use ReadFile to load it)");
        if (!string.IsNullOrWhiteSpace(kbQuery))
            contextSb.AppendLine($"\n== KB Query Hint: {kbQuery} ==\n(researcher: use SearchKb with this query)");
        var context = contextSb.ToString();

        // Rolling conversation history shared across all agents
        var sharedHistory = new List<string>();
        sharedHistory.Add($"TASK: Triage the following failure.\n\n{context}");

        var finalScribeText = "";
        var iteration = 0;
        var toolCallCount = 0;

        PrintHeader("MAGENTIC MANAGER", $"Starting manager-driven triage (max {MaxIterations} iterations, max {MaxToolCalls} tool calls)...");
        Console.WriteLine();

        // ---- Manager loop ----
        while (iteration < MaxIterations)
        {
            iteration++;

            // Build manager prompt with full shared history
            var historyText = string.Join("\n\n---\n\n", sharedHistory);
            var urgencyHint = iteration >= MaxIterations - 1
                ? "\nIMPORTANT: You are running low on iterations. If you have any evidence at all, route to \"scribe\" NOW to produce the final triage card. Do not gather more evidence.\n"
                : "";
            var managerPrompt = "You are the Magentic Manager. Review the team's progress and decide what to do next.\n\n" +
                $"Full conversation history so far:\n{historyText}\n\n" +
                urgencyHint +
                "Respond with a JSON object (no markdown fences):\n" +
                "{\n" +
                "  \"progress_summary\": \"brief summary of progress so far\",\n" +
                "  \"next_agent\": \"researcher | diagnostician | critic | scribe | DONE\",\n" +
                "  \"reason\": \"why you chose this agent\",\n" +
                "  \"task\": \"specific instruction for the chosen agent\",\n" +
                "  \"confidence\": 0.0\n" +
                "}\n\n" +
                "Use \"DONE\" only if scribe has already produced a valid JSON triage card.\n" +
                "Once the diagnostician and critic have both contributed, route to \"scribe\" to produce the final output. Do not keep gathering evidence indefinitely.\n" +
                $"Set confidence >= {ConfidenceThreshold} when you believe the evidence is sufficient.";

            // Get manager decision
            var managerSession = await managerAgent.CreateSessionAsync(ct);
            var managerDecisionText = await RunAgentAndCollect(managerAgent, managerPrompt, managerSession, ct);
            var decision = ParseManagerDecision(managerDecisionText);

            // Print manager decision
            PrintManagerDecision(iteration, MaxIterations, toolCallCount, MaxToolCalls, decision);

            // Check stopping conditions
            if (decision.NextAgent.Equals("DONE", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLineColorful($"  Manager signalled DONE. Confidence: {decision.Confidence:F2}", ConsoleColor.Green);
                break;
            }

            if (decision.Confidence >= ConfidenceThreshold && !string.IsNullOrEmpty(finalScribeText))
            {
                Console.WriteLineColorful($"  Confidence threshold reached ({decision.Confidence:F2} >= {ConfidenceThreshold}). Stopping.", ConsoleColor.Green);
                break;
            }

            // Get the next agent
            var agentName = decision.NextAgent?.Trim().ToLowerInvariant() ?? "";

            // Force scribe on last iteration if not already called
            if (iteration == MaxIterations && agentName != "scribe" && string.IsNullOrEmpty(finalScribeText))
            {
                Console.WriteLineColorful($"  ⚠️ Last iteration reached without scribe. Forcing scribe to produce triage card.", ConsoleColor.Yellow);
                agentName = "scribe";
            }

            if (!agentMap.TryGetValue(agentName, out var nextAgent))
            {
                Console.WriteLineColorful($"  ⚠️ Unknown agent '{decision.NextAgent}'. Defaulting to researcher.", ConsoleColor.Yellow);
                agentName = "researcher";
                nextAgent = researcherAgent;
            }

            // Build agent prompt using shared history + manager task
            var agentPromptSb = new StringBuilder();
            agentPromptSb.AppendLine("== Team conversation so far ==");
            agentPromptSb.AppendLine(historyText);
            agentPromptSb.AppendLine("\n== Your task from the Manager ==");
            agentPromptSb.AppendLine(decision.Task ?? "Proceed with your role.");
            var agentPrompt = agentPromptSb.ToString();

            // Count tool calls for researcher
            var preToolCount = toolCallCount;
            if (agentName == "researcher" && toolCallCount >= MaxToolCalls)
            {
                Console.WriteLineColorful($"  ⚠️ Tool call limit reached ({MaxToolCalls}). Skipping tool usage this turn.", ConsoleColor.Yellow);
                // Override: run researcher without tools this turn
                nextAgent = config.CreateAgent(LoadPrompt("researcher")); // no tools
            }

            // Run the chosen agent
            var agentSession = await nextAgent.CreateSessionAsync(ct);
            var agentResponse = await RunAgentAndCollect(nextAgent, agentPrompt, agentSession, ct);

            // Track tool calls (rough count by checking for tool result markers)
            if (agentName == "researcher")
            {
                toolCallCount += CountToolCalls(agentResponse);
            }

            // Print agent output
            PrintAgentTurn(agentName, agentResponse);

            // Add to shared history
            sharedHistory.Add($"{agentName.ToUpper()}: {agentResponse}");

            // Track scribe output
            if (agentName == "scribe")
            {
                finalScribeText = agentResponse;
            }
        }

        if (iteration >= MaxIterations)
        {
            Console.WriteLineColorful($"  ⚠️ Max iterations ({MaxIterations}) reached.", ConsoleColor.Yellow);
        }

        var card = ParseTriageCard(finalScribeText);
        return (finalScribeText, card);
    }

    private static async Task<string> RunAgentAndCollect(
        AIAgent agent, string prompt, AgentSession session, CancellationToken ct)
    {
        var sb = new StringBuilder();
        await foreach (var update in agent.RunStreamingAsync(prompt, session).WithCancellation(ct))
        {
            sb.Append(update.Text);
        }
        return sb.ToString().Trim();
    }

    private static ManagerDecision ParseManagerDecision(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new ManagerDecision { NextAgent = "researcher", Task = "Gather initial evidence." };

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
            return new ManagerDecision { NextAgent = "researcher", Task = text };

        try
        {
            return JsonSerializer.Deserialize<ManagerDecision>(text[start..(end + 1)],
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new ManagerDecision { NextAgent = "researcher", Task = text };
        }
        catch
        {
            return new ManagerDecision { NextAgent = "researcher", Task = text };
        }
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

    private static int CountToolCalls(string text)
    {
        // Rough heuristic: count occurrences of tool result markers
        var count = 0;
        count += CountOccurrences(text, "[release-notes.md]");
        count += CountOccurrences(text, "[testing-guidelines.md]");
        count += CountOccurrences(text, "Found");
        count += CountOccurrences(text, "build-log-");
        return Math.Min(count, 2); // cap contribution per turn
    }

    private static int CountOccurrences(string text, string pattern)
        => (text.Length - text.Replace(pattern, "").Length) / pattern.Length;

    private static void PrintManagerDecision(int iteration, int maxIter, int toolCalls, int maxTools, ManagerDecision decision)
    {
        Console.WriteColorful($"\nIteration {iteration}/{maxIter} | ToolCalls {toolCalls}/{maxTools}", ConsoleColor.Magenta);
        Console.WriteLine();

        Console.WriteColorful($"Manager chose: {decision.NextAgent?.ToUpper()} ", ConsoleColor.DarkMagenta);
        Console.WriteLineColorful($"(reason: {decision.Reason})", ConsoleColor.DarkGray);

        if (!string.IsNullOrWhiteSpace(decision.ProgressSummary))
        {
            Console.WriteLineColorful($"  Progress: {decision.ProgressSummary}", ConsoleColor.DarkGray);
        }

        Console.WriteLineColorful($"  Confidence: {decision.Confidence:F2} | Task: {decision.Task}", ConsoleColor.DarkGray);
        Console.WriteLine();
    }

    private static void PrintAgentTurn(string agentName, string text)
    {
        var (color, prefix) = agentName.ToLowerInvariant() switch
        {
            "researcher" => (ConsoleColor.Cyan, "[RESEARCHER]"),
            "diagnostician" => (ConsoleColor.Blue, "[DIAGNOSTICIAN]"),
            "critic" => (ConsoleColor.Yellow, "[CRITIC]"),
            "scribe" => (ConsoleColor.Green, "[SCRIBE]"),
            _ => (ConsoleColor.White, $"[{agentName.ToUpper()}]"),
        };

        Console.WriteColorful($"{prefix} ", color);
        Console.WriteLine(text);
        Console.WriteLine();
    }

    private static void PrintHeader(string step, string desc)
    {
        Console.WriteLineColorful($"━━━ {step} ━━━", ConsoleColor.Magenta);
        Console.WriteLineColorful($"  {desc}", ConsoleColor.DarkGray);
    }
}
