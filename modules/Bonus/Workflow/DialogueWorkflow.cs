using System.Text.Json;
using Microsoft.Agents.AI;

namespace RPGGameMaster.Workflow;

/// <summary>
/// NPC dialogue sub-loop: creates a dynamic agent from the NPC's stored AgentInstructions,
/// then lets the player converse via GM-formatted options.
/// <para>NOTE: Static — see GameMasterWorkflow for DI migration notes.</para>
/// </summary>
internal static class DialogueWorkflow
{

    public static async Task RunAsync(
        AgentConfig config, GameState state, NPC npc, CancellationToken ct)
    {
        Console.WriteLine();
        GameConsoleUI.WriteLine($"  {UIStrings.Format(state.Language, "dialogue_header", npc.Name)}", ConsoleColor.Magenta);
        GameConsoleUI.WriteLine($"  {npc.Description}", ConsoleColor.DarkGray);

        // Create dynamic NPC agent from stored/generated instructions
        var npcInstructions = NPCPromptFactory.BuildDialogueInstructions(npc, state.Language);

        var npcAgent = config.CreateAgent(npcInstructions);

        // Seed local history from persisted dialogue so the NPC "remembers" past conversations
        var dialogueHistory = new List<string>(npc.DialogueHistory);

        var openingPrompt = npc.HasMet
            ? $"The adventurer {state.Player.Name} approaches you again. Greet them as someone you've met before."
            : $"An adventurer named {state.Player.Name} approaches you for the first time. Greet them in character.";

        // If there's prior history, include a summary so the NPC can reference it
        if (npc.HasMet && npc.DialogueHistory.Count > 0)
        {
            openingPrompt += $"\n\nPrevious conversations (for context — reference these naturally if relevant):\n" +
                string.Join("\n", npc.DialogueHistory.TakeLast(10));
        }

        // Mark as met (after constructing openingPrompt so first-meeting greeting fires)
        npc.HasMet = true;

        // Dialogue loop
        var maxRounds = GameConstants.MaxDialogueRounds;
        var round = 0;

        while (round < maxRounds)
        {
            round++;

            // Build NPC prompt with conversation context
            var npcPrompt = round == 1
                ? openingPrompt
                : $"Conversation so far:\n{string.Join("\n", dialogueHistory.TakeLast(6))}";

            // Check if NPC has quests to offer
            var unofferedQuests = npc.Quests
                .Where(q => !q.IsComplete && !state.Player.ActiveQuests.Any(aq => aq.Id == q.Id))
                .ToList();

            if (unofferedQuests.Count > 0 && round >= 2)
            {
                npcPrompt += $"\n\nYou also want to hint at asking for help with: {unofferedQuests[0].Title} — {unofferedQuests[0].Description}. " +
                    "Work this naturally into your speech. " +
                    "If the player's last message agreed/committed to help with this task, set \"quest_accepted\": true in your response.";
            }

            // NPC speaks (returns JSON with speech + options) — retry once on failure
            string npcResponse;
            await using (ConsoleSpinner.Start($"[{npc.Name}] Thinking..."))
            {
                npcResponse = await AgentRunner.RunAgent(npcAgent, npcPrompt, ct);
            }

            // If the response is empty or obviously a failure, retry once
            if (string.IsNullOrWhiteSpace(npcResponse) || npcResponse.StartsWith("[Error"))
            {
                await Task.Delay(1000, ct);  // Brief pause before retry
                npcResponse = await AgentRunner.RunAgent(npcAgent, npcPrompt, ct);
            }

            // Final fallback if still empty
            if (string.IsNullOrWhiteSpace(npcResponse) || npcResponse.StartsWith("[Error"))
                npcResponse = $"{{\"speech\": \"{npc.Name} pauses, collecting their thoughts.\", \"quest_accepted\": false, \"options\": []}}";

            // Parse the structured response
            var (speech, options, questAccepted) = ParseNpcResponse(npcResponse);

            // Check for quest acceptance signaled by the NPC agent
            if (questAccepted && unofferedQuests.Count > 0 && round >= 2)
            {
                var quest = unofferedQuests[0];
                if (!state.Player.ActiveQuests.Any(aq => aq.Id == quest.Id))
                {
                    state.Player.ActiveQuests.Add(quest);
                    GameConsoleUI.WriteLine($"\n  {UIStrings.Format(state.Language, "dialogue_quest_accepted", quest.Title)}", ConsoleColor.Green);
                    GameConsoleUI.WriteLine($"     {quest.Description}", ConsoleColor.Green);
                    state.AddLog($"Accepted quest '{quest.Title}' from {npc.Name}.");
                }
            }

            // Display NPC speech
            Console.WriteLine();
            GameConsoleUI.Write($"  {npc.Name}: ", ConsoleColor.Cyan);
            Console.WriteLine(speech);

            dialogueHistory.Add($"{npc.Name}: {speech}");

            // Auto-end conversation after quest acceptance:
            // Show the NPC's farewell speech, then exit the dialogue loop
            if (questAccepted)
            {
                GameConsoleUI.WriteLine($"\n  {UIStrings.Format(state.Language, "dialogue_leave_quest", npc.Name)}", ConsoleColor.DarkGray);
                break;
            }

            // Fallback options if parsing failed
            if (options.Count == 0)
            {
                options =
                [
                    new DialogueOption { Number = 1, Text = UIStrings.Format(state.Language, "dialogue_ask_elaborate", npc.Name) },
                    new DialogueOption { Number = 2, Text = UIStrings.Format(state.Language, "dialogue_ask_role", npc.Name) },
                    new DialogueOption { Number = 3, Text = UIStrings.Get(state.Language, "dialogue_end"), IsFarewell = true },
                ];
            }

            // Display options
            Console.WriteLine();
            foreach (var opt in options)
            {
                GameConsoleUI.Write($"  [{opt.Number}] ", ConsoleColor.Yellow);
                Console.WriteLine(opt.Text);
            }
            Console.WriteLine();

            // Get player choice
            var choice = GetDialogueChoice(options, state.Language);
            if (choice is null) continue;

            // Check for end conversation: use structured is_farewell signal (language-agnostic)
            if (choice.IsFarewell || choice.Number == options[^1].Number)
            {
                GameConsoleUI.WriteLine($"\n  {UIStrings.Format(state.Language, "dialogue_farewell", npc.Name)}", ConsoleColor.DarkGray);
                break;
            }

            dialogueHistory.Add($"Player: {choice.Text}");
        }

        // Persist dialogue history back to NPC model (trimmed to cap)
        foreach (var entry in dialogueHistory.Skip(npc.DialogueHistory.Count))
            npc.AddDialogue(entry);

        // Small disposition bump for having a conversation (capped at 100)
        npc.DispositionTowardPlayer = npc.DispositionTowardPlayer.Improve(GameConstants.ConversationDispositionBoost);

        state.AddLog($"Spoke with {npc.Name}.");
    }

    private static (string speech, List<DialogueOption> options, bool questAccepted) ParseNpcResponse(string text)
    {
        var json = LlmJsonParser.ExtractJson(text);
        if (json is null) return (text, [], false);  // No JSON found — treat entire response as speech
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Extract speech
            var speech = root.Str("speech", text);

            // Extract quest_accepted flag
            var questAccepted = root.Bool("quest_accepted");

            // Extract options
            var options = new List<DialogueOption>();
            if (root.TryGetProperty("options", out var arr))
            {
                foreach (var el in arr.EnumerateArray())
                {
                    options.Add(new DialogueOption
                    {
                        Number = el.Int("number", options.Count + 1),
                        Text = el.Str("text"),
                        IsFarewell = el.Bool("is_farewell"),
                    });
                }
            }
            return (speech, options, questAccepted);
        }
        catch
        {
            return (text, [], false);
        }
    }

    private static DialogueOption? GetDialogueChoice(List<DialogueOption> options, string language)
        => GameConsoleUI.PromptForChoice(
            options, language,
            "dialogue_prompt", "dialogue_enter_num",
            opts => opts.FirstOrDefault(o => o.IsFarewell)
                ?? opts.LastOrDefault()
                ?? new DialogueOption { Number = 0, Text = "End conversation", IsFarewell = true },
            options.Count);
}