using System.Text.Json;
using Microsoft.Agents.AI;
using RPGGameMaster.Models;

namespace RPGGameMaster.Workflow;

/// <summary>
/// NPC dialogue sub-loop: creates a dynamic agent from the NPC's stored AgentInstructions,
/// then lets the player converse via GM-formatted options.
/// </summary>
internal static class DialogueWorkflow
{

    public static async Task RunAsync(
        AgentConfig config, GameState state, NPC npc, CancellationToken ct)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"  💬 Conversation with {npc.Name}");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  {npc.Description}");
        Console.ResetColor();

        // Create dynamic NPC agent from stored instructions
        var npcBaseInstructions = !string.IsNullOrWhiteSpace(npc.AgentInstructions)
            ? npc.AgentInstructions
            : $"You are {npc.Name}, a {npc.Occupation}. {npc.Personality}. Keep responses to 2-3 sentences. Stay in character.";

        // Augment NPC instructions to also produce dialogue options in structured JSON
        var npcInstructions = npcBaseInstructions + "\n\n" +
            "IMPORTANT OUTPUT FORMAT: You MUST respond with ONLY a JSON object, no extra text. Format:\n" +
            "{\n" +
            "  \"speech\": \"Your in-character dialogue here (2-4 sentences).\",\n" +
            "  \"options\": [\n" +
            "    {\"number\": 1, \"text\": \"A contextual player response option\"},\n" +
            "    {\"number\": 2, \"text\": \"Another option with a different tone\"},\n" +
            "    {\"number\": 3, \"text\": \"A third option\"},\n" +
            "    {\"number\": 4, \"text\": \"End conversation\"}\n" +
            "  ]\n" +
            "}\n" +
            "Rules for options:\n" +
            "- Generate 3-5 options that are SPECIFIC to what was just discussed\n" +
            "- Vary the tone: curious, friendly, skeptical, direct, etc.\n" +
            "- Reference specific details from the conversation\n" +
            "- The LAST option must always be to end or leave the conversation\n" +
            "- NEVER use generic options like 'Tell me more' — be specific!";

        var npcAgent = config.CreateAgent(npcInstructions);

        var dialogueHistory = new List<string>();
        var openingPrompt = npc.HasMet
            ? $"The adventurer {state.Player.Name} approaches you again. Greet them as someone you've met before."
            : $"An adventurer named {state.Player.Name} approaches you for the first time. Greet them in character.";

        // Mark as met (after constructing openingPrompt so first-meeting greeting fires)
        npc.HasMet = true;

        // Dialogue loop
        var maxRounds = 10;
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
                    "Work this naturally into your speech.";
            }

            // NPC speaks (returns JSON with speech + options)
            var npcResponse = await AgentHelper.RunAgent(npcAgent, npcPrompt, ct,
                "{\"speech\": \"The NPC seems lost in thought.\", \"options\": []}");

            // Parse the structured response
            var (speech, options) = ParseNpcResponse(npcResponse);

            // Display NPC speech
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"  {npc.Name}: ");
            Console.ResetColor();
            Console.WriteLine(speech);

            dialogueHistory.Add($"{npc.Name}: {speech}");

            // Fallback options if parsing failed
            if (options.Count == 0)
            {
                options =
                [
                    new DialogueOption { Number = 1, Text = $"Ask {npc.Name} to elaborate on what they just said." },
                    new DialogueOption { Number = 2, Text = $"Ask about {npc.Name}'s role in this place." },
                    new DialogueOption { Number = 3, Text = "End conversation." },
                ];
            }

            // Display options
            Console.WriteLine();
            foreach (var opt in options)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"  [{opt.Number}] ");
                Console.ResetColor();
                Console.WriteLine(opt.Text);
            }
            Console.WriteLine();

            // Get player choice
            var choice = GetDialogueChoice(options);
            if (choice is null) continue;

            // Check for end conversation: last option is always exit, or text hints at leaving
            if (choice.Number == options[^1].Number ||
                 (choice.Text.Contains("end", StringComparison.OrdinalIgnoreCase) &&
                  choice.Text.Contains("conversation", StringComparison.OrdinalIgnoreCase)) ||
                 choice.Text.Contains("goodbye", StringComparison.OrdinalIgnoreCase) ||
                 choice.Text.Contains("farewell", StringComparison.OrdinalIgnoreCase) ||
                 choice.Text.Contains("leave", StringComparison.OrdinalIgnoreCase))
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"\n  You bid farewell to {npc.Name}.");
                Console.ResetColor();
                break;
            }

            // Check for quest acceptance — only if the NPC explicitly mentioned a quest
            // and the player's chosen option contains clear acceptance language
            if (unofferedQuests.Count > 0 && round >= 3 &&
                (choice.Text.Contains("accept", StringComparison.OrdinalIgnoreCase) ||
                 (choice.Text.Contains("quest", StringComparison.OrdinalIgnoreCase) &&
                  (choice.Text.Contains("yes", StringComparison.OrdinalIgnoreCase) ||
                   choice.Text.Contains("sure", StringComparison.OrdinalIgnoreCase) ||
                   choice.Text.Contains("agree", StringComparison.OrdinalIgnoreCase) ||
                   choice.Text.Contains("help", StringComparison.OrdinalIgnoreCase)))))
            {
                var quest = unofferedQuests[0];
                state.Player.ActiveQuests.Add(quest);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n  📜 Quest accepted: {quest.Title}!");
                Console.WriteLine($"     {quest.Description}");
                Console.ResetColor();
                state.AddLog($"Accepted quest '{quest.Title}' from {npc.Name}.");
            }

            dialogueHistory.Add($"Player: {choice.Text}");
        }

        // Save NPC state
        state.AddLog($"Spoke with {npc.Name}.");
    }

    private static (string speech, List<DialogueOption> options) ParseNpcResponse(string text)
    {
        var json = AgentHelper.ExtractJson(text);
        if (json is null) return (text, []);  // No JSON found — treat entire response as speech
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Extract speech
            var speech = root.TryGetProperty("speech", out var s) ? s.GetString() ?? text : text;

            // Extract options
            var options = new List<DialogueOption>();
            if (root.TryGetProperty("options", out var arr))
            {
                foreach (var el in arr.EnumerateArray())
                {
                    options.Add(new DialogueOption
                    {
                        Number = el.TryGetProperty("number", out var n) ? n.GetInt32() : options.Count + 1,
                        Text = el.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "",
                    });
                }
            }
            return (speech, options);
        }
        catch
        {
            return (text, []);
        }
    }

    private static DialogueOption? GetDialogueChoice(List<DialogueOption> options)
    {
        while (true)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("Say > ");
            Console.ResetColor();
            var input = Console.ReadLine();

            if (input is null)
            {
                // End of input stream — auto-leave dialogue
                Console.WriteLine();
                return options.FirstOrDefault(o => o.Text.Contains("end", StringComparison.OrdinalIgnoreCase)
                    && o.Text.Contains("conversation", StringComparison.OrdinalIgnoreCase))
                    ?? options.FirstOrDefault(o => o.Text.Contains("leave", StringComparison.OrdinalIgnoreCase)
                        || o.Text.Contains("goodbye", StringComparison.OrdinalIgnoreCase)
                        || o.Text.Contains("farewell", StringComparison.OrdinalIgnoreCase))
                    ?? new DialogueOption { Number = 0, Text = "End conversation" };
            }

            input = input.Trim();
            if (string.IsNullOrEmpty(input)) continue;

            if (int.TryParse(input, out var num))
            {
                var match = options.FirstOrDefault(o => o.Number == num);
                if (match is not null) return match;
            }

            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"Enter 1-{options.Count}.");
            Console.ResetColor();
        }
    }
}
