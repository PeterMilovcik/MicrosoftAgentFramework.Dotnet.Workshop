using System.Text;

namespace RPGGameMaster.Prompts;

/// <summary>
/// Reusable building blocks for prompt-context construction.
/// Each dedicated prompt factory (<see cref="GameStatePromptFactory"/>,
/// <see cref="LocationPromptFactory"/>, <see cref="NPCPromptFactory"/>,
/// <see cref="CreaturePromptFactory"/>) composes from these parts.
/// </summary>
internal static class PromptParts
{
    internal static void AppendGenerationHeader(StringBuilder sb)
        => sb.AppendLine("## GENERATION CONTEXT\n");

    internal static void AppendWorldHeader(StringBuilder sb, GameState state, bool includeCombatStats = false)
    {
        sb.AppendLine($"World theme: {state.WorldTheme}");
        LanguageHint.AppendTo(sb, state.Language);

        if (includeCombatStats)
        {
            sb.AppendLine($"Player: {state.Player.Name} | Level {state.Player.Level} | HP {state.Player.Health} | " +
                $"EffAtk {state.Player.EffectiveAttack} | EffDef {state.Player.EffectiveDefense}");
        }
        else
        {
            sb.AppendLine($"Player: {state.Player.Name} | Level {state.Player.Level} | HP {state.Player.Health} | Gold {state.Player.Gold}");
            sb.AppendLine($"Locations explored: {state.Locations.Count}");
        }

        sb.AppendLine();
    }

    internal static void AppendLocationDetail(StringBuilder sb, Location location, Difficulty? difficulty = null)
    {
        sb.AppendLine($"Location: {location.Name}");
        sb.AppendLine($"Description: {location.Description}");
        sb.AppendLine($"Location id: {location.Id}");
        if (difficulty.HasValue)
            sb.AppendLine($"Difficulty: {difficulty.Value}");
        if (!string.IsNullOrWhiteSpace(location.Type))
            sb.AppendLine($"Location type: {location.Type}");
        if (!string.IsNullOrWhiteSpace(location.Atmosphere))
            sb.AppendLine($"Location atmosphere: {location.Atmosphere}");
        sb.AppendLine($"Location danger level: {location.DangerLevel}");
        if (!string.IsNullOrWhiteSpace(location.Lore))
            sb.AppendLine($"Location lore: {location.Lore}");
        sb.AppendLine();
    }

    internal static void AppendRecentEvents(StringBuilder sb, GameState state, int count, string header = "Recent events:")
    {
        if (state.GameLog.Count == 0) return;
        sb.AppendLine(header);
        foreach (var entry in state.GameLog.TakeLast(count))
            sb.AppendLine($"  - {entry}");
    }

    internal static void AppendNPCsAtLocation(StringBuilder sb, GameState state, Location location, string label)
    {
        var npcsHere = location.NPCIds
            .Where(id => state.NPCs.ContainsKey(id))
            .Select(id => state.NPCs[id])
            .ToList();
        if (npcsHere.Count > 0)
        {
            sb.AppendLine(label);
            foreach (var n in npcsHere)
                sb.AppendLine($"  - {n.Name} ({n.Occupation})");
            sb.AppendLine();
        }
    }
}
