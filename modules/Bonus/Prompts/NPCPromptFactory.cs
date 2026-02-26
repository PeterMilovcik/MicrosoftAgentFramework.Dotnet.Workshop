using System.Text;

namespace RPGGameMaster.Prompts;

/// <summary>
/// Builds prompt context for the NPC Weaver agent (generation)
/// and dynamic NPC dialogue agents (conversation system-prompt).
/// </summary>
internal static class NPCPromptFactory
{
    // ── NPC generation context ──

    internal static string Build(GameState state, Location location)
    {
        var sb = new StringBuilder();
        PromptParts.AppendGenerationHeader(sb);
        PromptParts.AppendWorldHeader(sb, state);
        PromptParts.AppendLocationDetail(sb, location);

        // Existing NPCs at this location (dedup signal)
        var npcsHere = location.NPCIds
            .Where(id => state.NPCs.ContainsKey(id))
            .Select(id => state.NPCs[id])
            .ToList();
        if (npcsHere.Count > 0)
        {
            sb.AppendLine("NPCs already at this location (DO NOT duplicate these archetypes):");
            foreach (var n in npcsHere)
                sb.AppendLine($"  - {n.Name} ({n.Occupation}, mood: {n.Mood})");
            sb.AppendLine();
        }

        // Global occupation dedup
        var allOccupations = state.NPCs.Values.Select(n => n.Occupation).Where(o => !string.IsNullOrWhiteSpace(o)).Distinct().ToList();
        if (allOccupations.Count > 0)
            sb.AppendLine($"Occupations already used in this world (pick a DIFFERENT one): {string.Join(", ", allOccupations)}");

        // Global name dedup
        var allNames = state.NPCs.Values.Select(n => n.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
        if (allNames.Count > 0)
            sb.AppendLine($"Names already used (pick a DIFFERENT one): {string.Join(", ", allNames)}");
        sb.AppendLine();

        // Creatures at this location (NPC can reference/warn)
        var creaturesHere = location.CreatureIds
            .Where(id => state.Creatures.ContainsKey(id))
            .Select(id => state.Creatures[id])
            .Where(c => !c.IsDefeated)
            .ToList();
        if (creaturesHere.Count > 0)
        {
            sb.AppendLine("Creatures at this location (NPC should be aware of these):");
            foreach (var c in creaturesHere)
                sb.AppendLine($"  - {c.Name} ({c.Difficulty}) — {c.Lore}");
            sb.AppendLine();
        }

        // Active & completed quests
        var activeQuests = state.Player.ActiveQuests.Where(q => !q.IsComplete).ToList();
        var completedQuests = state.Player.ActiveQuests.Where(q => q.IsComplete).ToList();
        if (activeQuests.Count > 0)
            sb.AppendLine($"Player's active quests: {string.Join(", ", activeQuests.Select(q => $"{q.Title} ({q.Type})"))}");
        if (completedQuests.Count > 0)
            sb.AppendLine($"Completed quests: {completedQuests.Count}");

        PromptParts.AppendRecentEvents(sb, state, 8, "\nRecent events in the world:");

        // Reward scaling guidance
        var lvl = state.Player.Level;
        sb.AppendLine($"\nQuest reward guidance for Level {lvl}:");
        sb.AppendLine($"  Gold: {GameConstants.RewardGoldBase + lvl * GameConstants.RewardGoldPerLevel}-{GameConstants.RewardGoldMaxBase + lvl * GameConstants.RewardGoldMaxPerLevel}");
        sb.AppendLine($"  XP: {GameConstants.RewardXPBase + lvl * GameConstants.RewardXPPerLevel}-{GameConstants.RewardXPMaxBase + lvl * GameConstants.RewardXPMaxPerLevel}");

        return sb.ToString();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // NPC dialogue agent prompt
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    /// <summary>
    /// Builds the full system-prompt for a dynamically created NPC conversation agent.
    /// Combines the NPC's stored <see cref="NPC.AgentInstructions"/>, current mood/disposition,
    /// language override, and structured-JSON output rules.
    /// </summary>
    public static string BuildDialogueInstructions(NPC npc, string language)
    {
        var baseInstructions = !string.IsNullOrWhiteSpace(npc.AgentInstructions)
            ? npc.AgentInstructions
            : $"You are {npc.Name}, a {npc.Occupation}. {npc.Personality}. Keep responses to 2-3 sentences. Stay in character.";

        var sb = new StringBuilder(baseInstructions);

        // Dynamic mood / disposition (not baked into AgentInstructions — changes between conversations)
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("CURRENT STATE (override any conflicting instructions):");
        sb.AppendLine($"Your current mood is: {npc.Mood}.");
        sb.AppendLine($"Your attitude toward this player is: {npc.DispositionTowardPlayer.Label} (disposition score: {npc.DispositionTowardPlayer}).");
        sb.AppendLine("Let your mood and attitude color your tone, word choice, and willingness to share information.");

        // Language override
        if (language != "English")
        {
            sb.AppendLine();
            sb.AppendLine($"IMPORTANT: You MUST speak and generate ALL text (speech, option text) in {language}. JSON keys stay in English.");
        }

        // Structured output format
        sb.AppendLine();
        sb.AppendLine("IMPORTANT OUTPUT FORMAT: You MUST respond with ONLY a JSON object, no extra text. Format:");
        sb.AppendLine("{");
        sb.AppendLine("  \"speech\": \"Your in-character dialogue here (2-4 sentences).\",");
        sb.AppendLine("  \"quest_accepted\": false,");
        sb.AppendLine("  \"is_farewell\": false,");
        sb.AppendLine("  \"options\": [");
        sb.AppendLine("    {\"number\": 1, \"text\": \"A contextual player response option\", \"is_farewell\": false},");
        sb.AppendLine("    {\"number\": 2, \"text\": \"Another option with a different tone\", \"is_farewell\": false},");
        sb.AppendLine("    {\"number\": 3, \"text\": \"A third option\", \"is_farewell\": false},");
        sb.AppendLine("    {\"number\": 4, \"text\": \"End conversation\", \"is_farewell\": true}");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine("Rules for quest_accepted:");
        sb.AppendLine("- Set to true ONLY when the player's PREVIOUS message clearly agreed, committed, or volunteered to help with a task/quest you offered");
        sb.AppendLine("- Examples of acceptance: 'I'll do it', 'Count me in', 'I'll help', 'I'll take the risk', 'I'll proceed', 'Deal', 'Where do I start?'");
        sb.AppendLine("- Set to false if the player is merely asking questions, hesitating, or hasn't been offered a task yet");
        sb.AppendLine("Rules for is_farewell:");
        sb.AppendLine("- Set to true when the option is a goodbye, farewell, 'end conversation', 'leave', 'walk away', or similar.");
        sb.AppendLine("- Set to false for ALL other options.");
        sb.AppendLine("- ONLY the last option in the list should ever be a farewell option.");
        sb.AppendLine("Rules for options:");
        sb.AppendLine("- Generate 3-5 options that are SPECIFIC to what was just discussed");
        sb.AppendLine("- Vary the tone: curious, friendly, skeptical, direct, etc.");
        sb.AppendLine("- Reference specific details from the conversation");
        sb.AppendLine("- The LAST option must always be to end or leave the conversation");
        sb.AppendLine("- NEVER use generic options like 'Tell me more' — be specific!");
        sb.AppendLine("CRITICAL DIALOGUE BOUNDARIES:");
        sb.AppendLine("- You are having a CONVERSATION. All options must be things the player can SAY or ASK.");
        sb.AppendLine("- NEVER generate action/exploration options like 'Enter the room', 'Pick up item', 'Search the area', 'Head to X'. Those belong to the game, not to dialogue.");
        sb.AppendLine("- NEVER pretend the player has completed a task, found an item, or traveled somewhere during this conversation.");
        sb.AppendLine("- NEVER skip ahead in time. Everything happens in the present moment of this conversation.");
        sb.AppendLine("- If the player has accepted a task, give a brief farewell and wish them luck — do NOT roleplay them doing the task.");

        return sb.ToString();
    }
}
