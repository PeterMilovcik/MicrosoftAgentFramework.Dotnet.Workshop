using System.Text;

namespace RPGGameMaster.Prompts;

/// <summary>
/// Builds prompt context for the Creature Forger agent when spawning new creatures.
/// </summary>
internal static class CreaturePromptFactory
{
    internal static string Build(GameState state, Location location, Difficulty difficulty)
    {
        var sb = new StringBuilder();
        PromptParts.AppendGenerationHeader(sb);
        PromptParts.AppendWorldHeader(sb, state, includeCombatStats: true);
        PromptParts.AppendLocationDetail(sb, location, difficulty);

        // Existing creatures (global dedup)
        var existingCreatures = state.Creatures.Values.ToList();
        if (existingCreatures.Count > 0)
        {
            var active = existingCreatures.Where(c => !c.IsDefeated).Select(c => c.Name).Distinct().ToList();
            var defeated = existingCreatures.Where(c => c.IsDefeated).Select(c => c.Name).Distinct().ToList();
            if (active.Count > 0)
                sb.AppendLine($"Active creatures in world (avoid duplicating): {string.Join(", ", active)}");
            if (defeated.Count > 0)
                sb.AppendLine($"Recent kills (new creature can be related — a pack member, scavenger, or escalation): {string.Join(", ", defeated)}");
            sb.AppendLine();
        }

        PromptParts.AppendNPCsAtLocation(sb, state, location,
            "NPCs at this location (creature lore can reference their warnings or stories):");

        // Active defeat quests (opportunity to spawn quest target)
        var defeatQuests = state.Player.ActiveQuests
            .Where(q => !q.IsComplete && q.Type == QuestType.Defeat)
            .ToList();
        if (defeatQuests.Count > 0)
        {
            sb.AppendLine("Active DEFEAT quests (you MAY generate the target creature if it fits this location):");
            foreach (var q in defeatQuests)
                sb.AppendLine($"  - Quest: {q.Title} — target: {q.TargetId}");
            sb.AppendLine();
        }

        PromptParts.AppendRecentEvents(sb, state, 5);

        return sb.ToString();
    }
}
