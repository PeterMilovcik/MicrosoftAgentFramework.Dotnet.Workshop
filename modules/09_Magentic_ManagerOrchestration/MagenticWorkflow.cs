using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Specialized.Magentic;
using Microsoft.Extensions.AI;
using Workshop.Common;

namespace MagenticOrchestration;

internal sealed record MagenticRunResult(string FinalText, TriageCard? Card, bool WasAborted = false);

/// <summary>
/// Built-in Magentic orchestration: an LLM manager plans the work, selects participants,
/// tracks progress, detects stalls, replans, and determines when the request is satisfied.
/// </summary>
internal static class MagenticWorkflow
{
    private const int MaxRounds = 8;
    private const int MaxStalls = 3;
    private const int MaxResets = 2;

    public static async Task<MagenticRunResult> RunAsync(
        AgentConfig config,
        string failureReport,
        string? logFileName,
        string? kbQuery,
        CancellationToken ct = default)
    {
        var baseDir = AppContext.BaseDirectory;

        var managerAgent = config.CreateNamedAgent(
            LoadPrompt(baseDir, "magentic-manager"),
            name: "magentic-manager",
            description: "Plans software triage, coordinates specialists, tracks progress, and replans when needed");

        var researcherAgent = config.CreateNamedAgent(
            LoadPrompt(baseDir, "researcher"),
            name: "researcher",
            description: "Gathers factual evidence from logs and the knowledge base using read-only tools",
            tools: WorkshopTools.GetTools());

        var diagnosticianAgent = config.CreateNamedAgent(
            LoadPrompt(baseDir, "diagnostician"),
            name: "diagnostician",
            description: "Analyzes evidence and ranks likely root-cause hypotheses");

        var criticAgent = config.CreateNamedAgent(
            LoadPrompt(baseDir, "critic"),
            name: "critic",
            description: "Challenges unsupported claims and identifies missing evidence or alternative hypotheses");

        var scribeAgent = config.CreateNamedAgent(
            LoadPrompt(baseDir, "scribe"),
            name: "scribe",
            description: "Produces the final evidence-based JSON Triage Card after analysis and critique");

        AIAgent[] participants = [researcherAgent, diagnosticianAgent, criticAgent, scribeAgent];

    #pragma warning disable MAAI001
        Workflow workflow = new MagenticWorkflowBuilder(managerAgent)
            .AddParticipants(participants)
            .WithName("Magentic Software Triage")
            .WithDescription("Coordinates evidence gathering, diagnosis, criticism, and structured triage output")
            .WithPromptOverrides(new MagenticPromptOverrides
            {
                FinalAnswerPrompt = MagenticDefaultPrompts.FinalAnswerPrompt + """

Return only the latest valid JSON Triage Card drafted by the scribe. Do not add
markdown fences, commentary, or fields. The JSON must contain exactly: summary,
category, suspected_areas, next_steps, suggested_owner_role, and confidence.
""",
            })
            .RequirePlanSignoff(true)
            .WithMaxRounds(MaxRounds)
            .WithMaxStalls(MaxStalls)
            .WithMaxResets(MaxResets)
            .Build();
#pragma warning restore MAAI001

        var task = BuildTask(failureReport, logFileName, kbQuery);
        List<ChatMessage> messages = [new(ChatRole.User, task)];

        PrintHeader(
            "MAGENTIC WORKFLOW",
            $"Starting built-in orchestration (rounds {MaxRounds}, stalls {MaxStalls}, resets {MaxResets})...");

        CheckpointManager checkpointManager = CheckpointManager.CreateInMemory();
        var environment = InProcessExecution.Lockstep.WithCheckpointing(checkpointManager);

        ExternalRequest? pendingRequest = null;
        CheckpointInfo? lastCheckpoint = null;
        WorkflowOutputEvent? finalOutput = null;
        Exception? workflowFailure = null;
        string? lastResponseId = null;
        var streamedScribeResponses = new Dictionary<string, StringBuilder>(StringComparer.Ordinal);
        string? streamedCardText = null;
        TriageCard? streamedCard = null;

        async Task DrainAsync(StreamingRun activeRun)
        {
            await foreach (WorkflowEvent evt in activeRun
                .WatchStreamAsync(blockOnPendingRequest: false)
                .WithCancellation(ct))
            {
                switch (evt)
                {
                    case AgentResponseUpdateEvent updateEvent:
                        CaptureScribeCard(
                            updateEvent,
                            streamedScribeResponses,
                            ref streamedCardText,
                            ref streamedCard);
                        WriteStreamingUpdate(updateEvent, ref lastResponseId);
                        break;

                    case MagenticPlanCreatedEvent planCreated:
                        WriteMagenticMessage("Initial Plan", planCreated.FullTaskLedger.Text);
                        break;

                    case MagenticReplannedEvent replanned:
                        WriteMagenticMessage("Replanned", replanned.FullTaskLedger.Text);
                        break;

                    case MagenticProgressLedgerUpdatedEvent progressUpdated:
                        WriteProgressLedger(progressUpdated.ProgressLedger);
                        break;

                    case RequestInfoEvent requestInfo
                        when requestInfo.Request.Data.As<MagenticPlanReviewRequest>() is not null:
                        pendingRequest = requestInfo.Request;
                        break;

                    case SuperStepCompletedEvent stepCompleted:
                        lastCheckpoint = stepCompleted.CompletionInfo?.Checkpoint ?? lastCheckpoint;
                        break;

                    case WorkflowOutputEvent outputEvent when outputEvent.Is<List<ChatMessage>>():
                        finalOutput = outputEvent;
                        break;

                    case WorkflowErrorEvent workflowError:
                        workflowFailure = workflowError.Exception
                            ?? new InvalidOperationException("The Magentic workflow reported an unknown error.");
                        Console.WriteLineError($"\n❌ Workflow error: {workflowFailure.Message}");
                        break;

                    case ExecutorFailedEvent executorFailed:
                        workflowFailure = new InvalidOperationException(
                            $"Executor '{executorFailed.ExecutorId}' failed: " +
                            (executorFailed.Data?.ToString() ?? "unknown error"));
                        Console.WriteLineError($"\n❌ {workflowFailure.Message}");
                        break;
                }
            }
        }

        await using (StreamingRun initialRun = await environment.OpenStreamingAsync(
            workflow,
            cancellationToken: ct))
        {
            await initialRun.TrySendMessageAsync(messages);
            await initialRun.TrySendMessageAsync(new TurnToken(emitEvents: true));
            await DrainAsync(initialRun);
        }

        while (finalOutput is null && pendingRequest is not null && workflowFailure is null)
        {
            var reviewRequest = pendingRequest.Data.As<MagenticPlanReviewRequest>()!;
            var reviewResponse = PromptForPlanReview(reviewRequest);
            if (reviewResponse is null)
            {
                return new MagenticRunResult(string.Empty, null, WasAborted: true);
            }

            if (lastCheckpoint is null)
            {
                throw new InvalidOperationException(
                    "The workflow requested plan review before producing a resumable checkpoint.");
            }

            ExternalResponse response = pendingRequest.CreateResponse(reviewResponse);
            pendingRequest = null;

            await using StreamingRun resumedRun = await environment.ResumeStreamingAsync(workflow, lastCheckpoint, ct);
            await resumedRun.SendResponseAsync(response);
            await DrainAsync(resumedRun);
        }

        if (workflowFailure is not null)
        {
            throw workflowFailure;
        }

        if (finalOutput?.As<List<ChatMessage>>() is not { } transcript)
        {
            throw new InvalidOperationException("The Magentic workflow completed without a conversation transcript.");
        }

        return ExtractTriageResult(transcript, streamedCardText, streamedCard);
    }

    private static MagenticPlanReviewResponse? PromptForPlanReview(MagenticPlanReviewRequest request)
    {
        Console.WriteLine();
        Console.WriteLineColorful("━━━ 🔐 Magentic Plan Review ━━━", ConsoleColor.Yellow);
        Console.WriteLine(request.IsStalled
            ? "The workflow stalled and the manager proposed a revised plan."
            : "The manager proposed an initial plan.");

        if (request.CurrentProgress is { } progress)
        {
            Console.WriteLineColorful(
                $"Current progress: satisfied={progress.IsRequestSatisfied}, " +
                $"inLoop={progress.IsInLoop}, progressing={progress.IsProgressBeingMade}",
                ConsoleColor.DarkGray);
        }

        Console.WriteLine();
        Console.WriteLine(request.Plan.Text);
        Console.WriteLine();

        while (true)
        {
            Console.WriteLine("Options: approve | revise <feedback> | abort");
            Console.Write("Decision: ");
            var decision = Console.ReadLine()?.Trim() ?? string.Empty;

            if (decision.Equals("approve", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLineColorful("✅ Plan approved.", ConsoleColor.Green);
                return request.Approve();
            }

            if (decision.Equals("abort", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (decision.StartsWith("revise", StringComparison.OrdinalIgnoreCase))
            {
                var feedback = decision.Length > "revise".Length
                    ? decision["revise".Length..].Trim()
                    : string.Empty;

                if (string.IsNullOrWhiteSpace(feedback))
                {
                    Console.WriteLineError("❌ Revision feedback is required. Example: revise investigate the retry timing first");
                    continue;
                }

                Console.WriteLineColorful("🔄 Requesting a revised plan...", ConsoleColor.Yellow);
                return request.Revise(feedback);
            }

            Console.WriteLineError("❌ Unknown option. Type: approve | revise <feedback> | abort");
        }
    }

    private static MagenticRunResult ExtractTriageResult(
        List<ChatMessage> transcript,
        string? streamedCardText,
        TriageCard? streamedCard)
    {
        foreach (var message in transcript.AsEnumerable().Reverse())
        {
            if (!string.Equals(message.AuthorName, "scribe", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (TryParseTriageCard(message.Text, out var card))
            {
                return new MagenticRunResult(message.Text, card);
            }
        }

        foreach (var message in transcript.AsEnumerable().Reverse())
        {
            if (TryParseTriageCard(message.Text, out var card))
            {
                return new MagenticRunResult(message.Text, card);
            }
        }

        if (streamedCard is not null && streamedCardText is not null)
        {
            Console.WriteLineColorful(
                "⚠️  The workflow reached a safety bound after producing a valid Triage Card; using the latest valid scribe output.",
                ConsoleColor.Yellow);
            return new MagenticRunResult(streamedCardText, streamedCard);
        }

        var fallback = transcript.LastOrDefault()?.Text ?? string.Empty;
        return new MagenticRunResult(fallback, null);
    }

    private static void CaptureScribeCard(
        AgentResponseUpdateEvent updateEvent,
        Dictionary<string, StringBuilder> responseBuffers,
        ref string? latestCardText,
        ref TriageCard? latestCard)
    {
        var text = updateEvent.Update.Text;
        if (ResolveAgentRole(updateEvent.ExecutorId) != "scribe" || string.IsNullOrEmpty(text))
        {
            return;
        }

        if (TryParseTriageCard(text, out var completeCard))
        {
            latestCardText = text;
            latestCard = completeCard;
        }

        var responseId = updateEvent.Update.ResponseId
            ?? updateEvent.Update.MessageId
            ?? updateEvent.ExecutorId;

        if (!responseBuffers.TryGetValue(responseId, out var buffer))
        {
            buffer = new StringBuilder();
            responseBuffers.Add(responseId, buffer);
        }

        buffer.Append(text);
        var accumulatedText = buffer.ToString();
        if (TryParseTriageCard(accumulatedText, out var accumulatedCard))
        {
            latestCardText = accumulatedText;
            latestCard = accumulatedCard;
        }
    }

    private static bool TryParseTriageCard(string text, out TriageCard? card)
    {
        card = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return false;
        }

        try
        {
            card = JsonSerializer.Deserialize<TriageCard>(
                text[start..(end + 1)],
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return card is not null &&
                !string.IsNullOrWhiteSpace(card.Summary) &&
                !string.IsNullOrWhiteSpace(card.Category);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string BuildTask(string failureReport, string? logFileName, string? kbQuery)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Triage the following software failure using the specialist team.");
        sb.AppendLine();
        sb.AppendLine("== Failure Report ==");
        sb.AppendLine(failureReport);

        if (!string.IsNullOrWhiteSpace(logFileName))
        {
            sb.AppendLine($"\n== Log File Available: {logFileName} ==");
            sb.AppendLine("The researcher should use ReadFile to gather evidence from it.");
        }

        if (!string.IsNullOrWhiteSpace(kbQuery))
        {
            sb.AppendLine($"\n== KB Query Hint: {kbQuery} ==");
            sb.AppendLine("The researcher should use SearchKb with this query.");
        }

        sb.AppendLine("""

== Required Outcome ==
Gather concrete evidence, diagnose likely causes, have the critic review the conclusions,
    and have the scribe draft the JSON Triage Card. Consider the request satisfied as soon
    as the scribe produces valid JSON with exactly these fields:
summary, category, suspected_areas, next_steps, suggested_owner_role, confidence.
    The manager will return that card as the framework's terminal synthesized answer.
""");

        return sb.ToString();
    }

    private static string LoadPrompt(string baseDir, string agentName)
    {
        var path = Path.Combine(baseDir, "assets", "prompts", "agents", $"{agentName}.md");
        return File.Exists(path) ? File.ReadAllText(path) : $"You are the {agentName} agent.";
    }

    private static void WriteStreamingUpdate(AgentResponseUpdateEvent updateEvent, ref string? lastResponseId)
    {
        var responseId = updateEvent.Update.ResponseId
            ?? updateEvent.Update.MessageId
            ?? updateEvent.ExecutorId;

        if (!string.Equals(responseId, lastResponseId, StringComparison.Ordinal))
        {
            if (lastResponseId is not null)
            {
                Console.WriteLine();
                Console.WriteLine();
            }

            Console.WriteColorful($"[{ResolveAgentRole(updateEvent.ExecutorId).ToUpperInvariant()}] ",
                ResolveAgentColor(updateEvent.ExecutorId));
            lastResponseId = responseId;
        }

        if (!string.IsNullOrEmpty(updateEvent.Update.Text))
        {
            Console.Write(updateEvent.Update.Text);
        }
    }

    private static void WriteMagenticMessage(string title, string? content)
    {
        Console.WriteLine();
        Console.WriteLineColorful($"[Magentic {title}]", ConsoleColor.Magenta);
        Console.WriteLine(content);
    }

    private static void WriteProgressLedger(MagenticProgressLedger ledger)
    {
        Console.WriteLine();
        Console.WriteLineColorful("[Magentic Progress Ledger]", ConsoleColor.DarkMagenta);
        Console.WriteLineColorful(
            $"  satisfied={ledger.IsRequestSatisfied}, inLoop={ledger.IsInLoop}, " +
            $"progressing={ledger.IsProgressBeingMade}",
            ConsoleColor.DarkGray);
        Console.WriteLineColorful($"  next={ledger.NextSpeaker}", ConsoleColor.DarkGray);
        Console.WriteLineColorful($"  instruction={ledger.InstructionOrQuestion}", ConsoleColor.DarkGray);
    }

    private static string ResolveAgentRole(string executorId)
    {
        var id = executorId.ToLowerInvariant();
        foreach (var role in (ReadOnlySpan<string>)["magentic-manager", "researcher", "diagnostician", "critic", "scribe"])
        {
            if (id.StartsWith(role, StringComparison.Ordinal) ||
                id.StartsWith(role.Replace('-', '_'), StringComparison.Ordinal))
            {
                return role;
            }
        }

        return id;
    }

    private static ConsoleColor ResolveAgentColor(string executorId) => ResolveAgentRole(executorId) switch
    {
        "magentic-manager" => ConsoleColor.Magenta,
        "researcher" => ConsoleColor.Cyan,
        "diagnostician" => ConsoleColor.Blue,
        "critic" => ConsoleColor.Yellow,
        "scribe" => ConsoleColor.Green,
        _ => ConsoleColor.White,
    };

    private static void PrintHeader(string step, string description)
    {
        Console.WriteLineColorful($"━━━ {step} ━━━", ConsoleColor.Magenta);
        Console.WriteLineColorful($"  {description}", ConsoleColor.DarkGray);
    }
}
