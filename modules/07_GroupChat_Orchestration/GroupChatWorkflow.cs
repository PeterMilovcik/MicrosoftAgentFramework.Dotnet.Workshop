using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Workshop.Common;

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
            tools: WorkshopTools.GetTools());

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

        // Run the workflow using Lockstep mode with chat messages as input
        List<ChatMessage> messages = [new(ChatRole.User, context)];
        await using var streamingRun = await InProcessExecution.Lockstep.RunStreamingAsync(workflow, messages);

        // Start execution with event emission enabled
        await streamingRun.TrySendMessageAsync(new TurnToken(emitEvents: true));

        string? lastAgentName = null;
        var currentAgentText = new StringBuilder();

        await foreach (var evt in streamingRun.WatchStreamAsync())
        {
            switch (evt)
            {
                case AgentResponseUpdateEvent agentEvt:
                {
                    var executorId = agentEvt.ExecutorId ?? "unknown";
                    var agentRole = ResolveAgentRole(executorId);
                    var text = agentEvt.Update.Text ?? "";

                    // When agent changes, flush previous agent's output
                    if (executorId != lastAgentName)
                    {
                        if (lastAgentName is not null && currentAgentText.Length > 0)
                        {
                            var prevRole = ResolveAgentRole(lastAgentName);
                            agentOutputs[prevRole] = currentAgentText.ToString();
                            if (prevRole == "scribe")
                                lastScribeText = currentAgentText.ToString();
                        }
                        currentAgentText.Clear();

                        // Print agent header
                        Console.WriteLine();
                        var (color, prefix) = agentRole switch
                        {
                            "planner" => (ConsoleColor.Blue, "[PLANNER]"),
                            "investigator" => (ConsoleColor.Cyan, "[INVESTIGATOR]"),
                            "critic" => (ConsoleColor.Yellow, "[CRITIC]"),
                            "scribe" => (ConsoleColor.Green, "[SCRIBE]"),
                            _ => (ConsoleColor.White, $"[{agentRole.ToUpper()}]"),
                        };
                        Console.WriteColorful($"{prefix} ", color);
                        lastAgentName = executorId;
                    }

                    Console.Write(text);
                    currentAgentText.Append(text);
                    break;
                }
            }
        }

        // Flush the last agent's output
        if (lastAgentName is not null && currentAgentText.Length > 0)
        {
            var lastRole = ResolveAgentRole(lastAgentName);
            agentOutputs[lastRole] = currentAgentText.ToString();
            if (lastRole == "scribe")
                lastScribeText = currentAgentText.ToString();
        }

        Console.WriteLine();
        Console.WriteLine();

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

    /// <summary>
    /// Extracts the agent role name from an ExecutorId that may contain a GUID suffix
    /// (e.g., "PLANNER_4F7606FF25CE41C2BAD54BDE277359D6" → "planner").
    /// </summary>
    private static string ResolveAgentRole(string executorId)
    {
        var id = executorId.ToLowerInvariant();
        foreach (var role in (ReadOnlySpan<string>)["planner", "investigator", "critic", "scribe"])
        {
            if (id.StartsWith(role, StringComparison.Ordinal))
                return role;
        }
        return id;
    }

    private static void PrintHeader(string step, string desc)
    {
        Console.WriteLineColorful($"━━━ {step} ━━━", ConsoleColor.Magenta);
        Console.WriteLineColorful($"  {desc}", ConsoleColor.DarkGray);
    }
}
