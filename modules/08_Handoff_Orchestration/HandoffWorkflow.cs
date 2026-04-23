using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Workshop.Common;

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
        var expertTools = WorkshopTools.GetTools();

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
        var lastScribeText = "";

        PrintHeader("HANDOFF WORKFLOW", "Starting handoff-based triage...");
        Console.WriteLineColorful("  frontdesk → [infra-expert | product-expert | test-expert] → scribe", ConsoleColor.DarkGray);
        Console.WriteLine();

        // Run the workflow using Lockstep mode with chat messages as input
        List<ChatMessage> messages = [new(ChatRole.User, context)];
        await using var streamingRun = await InProcessExecution.Lockstep.RunStreamingAsync(workflow, messages);

        // Start execution with event emission enabled
        await streamingRun.TrySendMessageAsync(new TurnToken(emitEvents: true));

        string? lastExecutorId = null;
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
                    if (executorId != lastExecutorId)
                    {
                        if (lastExecutorId is not null && currentAgentText.Length > 0)
                        {
                            var prevRole = ResolveAgentRole(lastExecutorId);
                            if (prevRole == "scribe")
                                lastScribeText = currentAgentText.ToString();

                            // Print handoff transition
                            PrintHandoff(prevRole, agentRole);
                        }
                        currentAgentText.Clear();

                        // Print agent header
                        Console.WriteLine();
                        var (color, prefix) = agentRole switch
                        {
                            "frontdesk" => (ConsoleColor.White, "[FRONTDESK]"),
                            "infra-expert" => (ConsoleColor.Cyan, "[INFRA-EXPERT]"),
                            "product-expert" => (ConsoleColor.Blue, "[PRODUCT-EXPERT]"),
                            "test-expert" => (ConsoleColor.Magenta, "[TEST-EXPERT]"),
                            "scribe" => (ConsoleColor.Green, "[SCRIBE]"),
                            _ => (ConsoleColor.Gray, $"[{agentRole.ToUpper()}]"),
                        };
                        Console.WriteColorful($"{prefix} ", color);
                        lastExecutorId = executorId;
                    }

                    Console.Write(text);
                    currentAgentText.Append(text);
                    break;
                }
                case ExecutorInvokedEvent invoked:
                    Console.WriteLineColorful($"  [Event] Executor invoked: {invoked.ExecutorId}", ConsoleColor.DarkGray);
                    break;
                case ExecutorCompletedEvent completed:
                    Console.WriteLineColorful($"  [Event] Executor completed: {completed.ExecutorId}", ConsoleColor.DarkGray);
                    break;
                case ExecutorFailedEvent failed:
                    Console.WriteLineColorful($"  [Event] Executor FAILED: {failed.ExecutorId} — {failed}", ConsoleColor.Red);
                    break;
                case WorkflowErrorEvent workflowErr:
                    Console.WriteLineColorful($"  [Event] Workflow ERROR: {workflowErr}", ConsoleColor.Red);
                    break;
                case WorkflowWarningEvent workflowWarn:
                    Console.WriteLineColorful($"  [Event] Workflow WARNING: {workflowWarn}", ConsoleColor.DarkYellow);
                    break;
                default:
                    Console.WriteLineColorful($"  [Event] {evt.GetType().Name}", ConsoleColor.DarkGray);
                    break;
            }
        }

        // Flush the last agent's output
        if (lastExecutorId is not null && currentAgentText.Length > 0)
        {
            var lastRole = ResolveAgentRole(lastExecutorId);
            if (lastRole == "scribe")
                lastScribeText = currentAgentText.ToString();
        }

        Console.WriteLine();
        Console.WriteLine();

        Console.WriteLineColorful($"  [Debug] Last executor: {lastExecutorId ?? "(none)"}, resolved role: {(lastExecutorId is not null ? ResolveAgentRole(lastExecutorId) : "N/A")}", ConsoleColor.DarkGray);
        Console.WriteLineColorful($"  [Debug] Scribe text length: {lastScribeText.Length}", ConsoleColor.DarkGray);
        if (lastScribeText.Length > 0)
            Console.WriteLineColorful($"  [Debug] Scribe text preview: {lastScribeText[..Math.Min(200, lastScribeText.Length)]}", ConsoleColor.DarkGray);

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
        if (string.IsNullOrWhiteSpace(text))
        {
            Console.WriteLineColorful("  [ParseTriageCard] Scribe output is empty or whitespace.", ConsoleColor.DarkYellow);
            return null;
        }

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');

        if (start < 0 || end <= start)
        {
            Console.WriteLineColorful($"  [ParseTriageCard] No JSON object found in scribe output (start={start}, end={end}). Text length={text.Length}.", ConsoleColor.DarkYellow);
            return null;
        }

        var jsonFragment = text[start..(end + 1)];
        try
        {
            var card = JsonSerializer.Deserialize<TriageCard>(jsonFragment,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (card is null)
            {
                Console.WriteLineColorful("  [ParseTriageCard] Deserialization returned null.", ConsoleColor.DarkYellow);
                return null;
            }

            // Warn if key fields are missing
            if (string.IsNullOrWhiteSpace(card.Summary) && string.IsNullOrWhiteSpace(card.Category))
            {
                Console.WriteLineColorful("  [ParseTriageCard] Parsed card has empty summary and category — may be wrong JSON object.", ConsoleColor.DarkYellow);
                Console.WriteLineColorful($"  [ParseTriageCard] JSON fragment: {jsonFragment[..Math.Min(200, jsonFragment.Length)]}...", ConsoleColor.DarkYellow);
            }

            return card;
        }
        catch (JsonException ex)
        {
            Console.WriteLineColorful($"  [ParseTriageCard] JSON deserialization failed: {ex.Message}", ConsoleColor.DarkYellow);
            Console.WriteLineColorful($"  [ParseTriageCard] JSON fragment ({jsonFragment.Length} chars): {jsonFragment[..Math.Min(300, jsonFragment.Length)]}...", ConsoleColor.DarkYellow);
            return null;
        }
    }

    private static void PrintHandoff(string from, string to)
    {
        Console.WriteLineColorful($"  ⟶ Handoff: {from} → {to}", ConsoleColor.DarkYellow);
        Console.WriteLine();
    }


    /// <summary>
    /// Extracts the agent role name from an ExecutorId that may contain a GUID suffix.
    /// The framework converts hyphens to underscores in ExecutorIds
    /// (e.g., "INFRA_EXPERT_34F80B7F00FC435C91613D6D2C81CA47" → "infra-expert").
    /// </summary>
    private static string ResolveAgentRole(string executorId)
    {
        var id = executorId.ToLowerInvariant();
        foreach (var role in (ReadOnlySpan<string>)["frontdesk", "infra-expert", "product-expert", "test-expert", "scribe"])
        {
            // Check both original name and underscore variant since the framework
            // may convert hyphens to underscores in ExecutorIds
            if (id.StartsWith(role, StringComparison.Ordinal) ||
                id.StartsWith(role.Replace('-', '_'), StringComparison.Ordinal))
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
