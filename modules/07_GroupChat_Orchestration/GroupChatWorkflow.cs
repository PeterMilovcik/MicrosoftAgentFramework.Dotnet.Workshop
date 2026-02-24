using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace GroupChatOrchestration;

/// <summary>
/// Group Chat workflow: 4 agents (planner, investigator, critic, scribe) collaborate
/// in a shared conversation coordinated by a RoundRobinGroupChatManager.
/// Only the investigator may call tools.
/// </summary>
internal static class GroupChatWorkflow
{
    private const int MaxIterations = 8;

    public static async Task<(string FinalText, TriageCard? Card)> RunAsync(
        AgentConfig config,
        string failureReport,
        string? logFileName,
        string? kbQuery,
        CancellationToken ct = default)
    {
        var baseDir = AppContext.BaseDirectory;

        // Load agent prompts
        var plannerPrompt = LoadPrompt(baseDir, "planner");
        var investigatorPrompt = LoadPrompt(baseDir, "investigator");
        var criticPrompt = LoadPrompt(baseDir, "critic");
        var scribePrompt = LoadPrompt(baseDir, "scribe");

        // Build shared context that all agents receive as part of the initial message
        var context = BuildContext(failureReport, logFileName, kbQuery);

        // Create agents — only investigator gets tools
        var plannerAgent = config.CreateNamedAgent(
            plannerPrompt, name: "planner", description: "Creates the triage plan and assigns subtasks");

        var investigatorAgent = config.CreateNamedAgent(
            investigatorPrompt, name: "investigator", description: "Gathers evidence using ReadFile and SearchKb tools",
            tools: WorkshopTools.GetInvestigatorTools());

        var criticAgent = config.CreateNamedAgent(
            criticPrompt, name: "critic", description: "Challenges assumptions and identifies evidence gaps");

        var scribeAgent = config.CreateNamedAgent(
            scribePrompt, name: "scribe", description: "Produces the final JSON triage card");

        // Build group chat workflow with round-robin manager
        var participants = new AIAgent[] { plannerAgent, investigatorAgent, criticAgent, scribeAgent };

        var groupChatBuilder = AgentWorkflowBuilder.CreateGroupChatBuilderWith(
            agents => new RoundRobinGroupChatManager(agents) { MaximumIterationCount = MaxIterations });

        groupChatBuilder.AddParticipants(participants);
        var workflow = groupChatBuilder.Build();

        // Collect agent outputs
        var agentOutputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lastScribeText = "";

        PrintHeader("GROUP CHAT", $"Starting group chat with {participants.Length} agents (max {MaxIterations} turns)...");

        // Run the workflow
        await using var streamingRun = await InProcessExecution.RunStreamingAsync(workflow, context, Guid.NewGuid().ToString(), ct);

        await foreach (var evt in streamingRun.WatchStreamAsync(ct))
        {
            if (evt is AgentResponseEvent agentEvt)
            {
                var agentName = agentEvt.ExecutorId ?? "unknown";
                var text = agentEvt.Response?.Text ?? "";
                if (string.IsNullOrWhiteSpace(text)) continue;

                // Print with colored prefix
                PrintAgentTurn(agentName, text);

                agentOutputs[agentName] = text;

                if (agentName.Equals("scribe", StringComparison.OrdinalIgnoreCase))
                {
                    lastScribeText = text;
                }
            }
        }

        // Parse triage card from scribe output
        var card = ParseTriageCard(lastScribeText);
        return (lastScribeText, card);
    }

    private static string BuildContext(string failureReport, string? logFileName, string? kbQuery)
    {
        var sb = new StringBuilder();
        sb.AppendLine("== Failure Report ==");
        sb.AppendLine(failureReport);

        if (!string.IsNullOrWhiteSpace(logFileName))
            sb.AppendLine($"\n== Log File Available: {logFileName} ==\n(INVESTIGATOR: use ReadFile to load it)");

        if (!string.IsNullOrWhiteSpace(kbQuery))
            sb.AppendLine($"\n== KB Query Hint: {kbQuery} ==\n(INVESTIGATOR: use SearchKb with this query)");

        sb.AppendLine("\n== Instructions ==");
        sb.AppendLine("Team: analyze this failure and produce a structured triage.");
        sb.AppendLine("PLANNER: create the plan first.");
        sb.AppendLine("INVESTIGATOR: gather evidence using tools.");
        sb.AppendLine("CRITIC: identify gaps in the evidence.");
        sb.AppendLine("SCRIBE: produce the final JSON triage card.");

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

    private static string LoadPrompt(string baseDir, string agentName)
    {
        var path = Path.Combine(baseDir, "assets", "prompts", "agents", $"{agentName}.md");
        return File.Exists(path) ? File.ReadAllText(path) : $"You are the {agentName} agent.";
    }

    private static void PrintAgentTurn(string agentName, string text)
    {
        var (color, prefix) = agentName.ToLowerInvariant() switch
        {
            "planner" => (ConsoleColor.Blue, "[PLANNER]"),
            "investigator" => (ConsoleColor.Cyan, "[INVESTIGATOR]"),
            "critic" => (ConsoleColor.Yellow, "[CRITIC]"),
            "scribe" => (ConsoleColor.Green, "[SCRIBE]"),
            _ => (ConsoleColor.White, $"[{agentName.ToUpper()}]"),
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
