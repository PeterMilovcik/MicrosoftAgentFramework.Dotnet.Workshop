using System.Text;

namespace RPGGameMaster.Prompts;

/// <summary>
/// Builds a full game-state summary prompt for the GM presenter agent.
/// </summary>
internal static class GameStatePromptFactory
{
    internal static string Build(GameState state)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"World Theme: {state.WorldTheme}");
        sb.AppendLine($"Turn: {state.TurnCount}");
        sb.AppendLine($"Player: {state.Player.Name} | HP: {state.Player.Health} | " +
            $"Atk: {state.Player.EffectiveAttack} | Def: {state.Player.EffectiveDefense} | " +
            $"Level: {state.Player.Level} | Gold: {state.Player.Gold} | XP: {state.Player.XP}/{state.Player.XPToNextLevel}");

        if (state.Player.Inventory.Count > 0)
            sb.AppendLine($"Inventory: {string.Join(", ", state.Player.Inventory.Select(i => $"{i.Name} ({i.Type})"))}");

        if (state.Player.ActiveQuests.Count > 0)
            sb.AppendLine($"Active Quests: {string.Join(", ", state.Player.ActiveQuests.Where(q => !q.IsComplete).Select(q => q.Title))}");

        var loc = state.CurrentLocation;
        if (loc is not null)
        {
            sb.AppendLine($"\nCurrent Location: {loc.Name}");
            if (!string.IsNullOrWhiteSpace(loc.Type))
                sb.AppendLine($"Type: {loc.Type}");
            if (!string.IsNullOrWhiteSpace(loc.Atmosphere))
                sb.AppendLine($"Atmosphere: {loc.Atmosphere}");
            sb.AppendLine($"Danger level: {loc.DangerLevel}");
            sb.AppendLine($"Description: {loc.Description}");
            if (!string.IsNullOrWhiteSpace(loc.Lore))
                sb.AppendLine($"Lore: {loc.Lore}");
            if (loc.PointsOfInterest.Count > 0)
                sb.AppendLine($"Points of interest: {string.Join("; ", loc.PointsOfInterest)}");
            sb.AppendLine($"Exits: {string.Join(", ", loc.Exits.Select(e => $"{e.Direction} ({(e.TargetLocationId.IsEmpty ? "unexplored" : "visited")})"))}");

            var npcsHere = loc.NPCIds
                .Where(id => state.NPCs.ContainsKey(id))
                .Select(id => state.NPCs[id])
                .ToList();
            if (npcsHere.Count > 0)
                sb.AppendLine($"NPCs here: {string.Join(", ", npcsHere.Select(n => $"{n.Name} (id: {n.Id}, {n.Occupation}, {n.SpeakingStyle}, mood: {n.Mood}, disposition: {n.DispositionTowardPlayer})"))}");

            var creaturesHere = loc.CreatureIds
                .Where(id => state.Creatures.ContainsKey(id))
                .Select(id => state.Creatures[id])
                .Where(c => !c.IsDefeated)
                .ToList();
            if (creaturesHere.Count > 0)
                sb.AppendLine($"Creatures here: {string.Join(", ", creaturesHere.Select(c => $"{c.Name} (id: {c.Id}, {c.Difficulty}, {c.Behavior})"))}");

            if (loc.Items.Count > 0)
                sb.AppendLine($"Items on ground: {string.Join(", ", loc.Items.Select(i => i.Name))}");
        }

        PromptParts.AppendRecentEvents(sb, state, 10, "\nRecent events:");

        return sb.ToString();
    }
}
