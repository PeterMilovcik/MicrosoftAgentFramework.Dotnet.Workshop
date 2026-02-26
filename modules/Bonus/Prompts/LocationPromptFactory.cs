using System.Text;

namespace RPGGameMaster.Prompts;

/// <summary>
/// Builds prompt context for the World Architect agent when generating new locations.
/// </summary>
internal static class LocationPromptFactory
{
    internal static string Build(
        GameState state, string? fromLocationId, Exit? entryExit)
    {
        var sb = new StringBuilder();
        PromptParts.AppendGenerationHeader(sb);
        PromptParts.AppendWorldHeader(sb, state);

        // Entry hint
        if (entryExit is not null)
            sb.AppendLine($"The player entered via an exit described as: \"{entryExit.Description}\". The new location should match that description.");
        else
            sb.AppendLine("Generate a suitable starting location for this adventure.");

        // Back reference
        if (fromLocationId is not null)
            sb.AppendLine($"Include an exit with direction 'Back' and target_location_id '{fromLocationId}' (the way the player came from).");
        else
            sb.AppendLine("This is the starting location — no back exit needed.");
        sb.AppendLine();

        // Source location context (what the player is leaving)
        if (fromLocationId is not null && state.Locations.TryGetValue(fromLocationId, out var fromLoc))
        {
            sb.AppendLine("Leaving from:");
            sb.AppendLine($"  Name: {fromLoc.Name}");
            if (!string.IsNullOrWhiteSpace(fromLoc.Type))
                sb.AppendLine($"  Type: {fromLoc.Type}");
            if (!string.IsNullOrWhiteSpace(fromLoc.Atmosphere))
                sb.AppendLine($"  Atmosphere: {fromLoc.Atmosphere}");
            sb.AppendLine($"  Danger: {fromLoc.DangerLevel}");
            sb.AppendLine();
        }

        // All existing locations (dedup + world awareness)
        if (state.Locations.Count > 0)
        {
            sb.AppendLine("Existing locations in the world (DO NOT duplicate names or repeat the same type too often):");
            foreach (var loc in state.Locations.Values)
            {
                var typeTag = !string.IsNullOrWhiteSpace(loc.Type) ? $" [{loc.Type}]" : "";
                var atmoTag = !string.IsNullOrWhiteSpace(loc.Atmosphere) ? $" ({loc.Atmosphere})" : "";
                sb.AppendLine($"  - {loc.Name}{typeTag}{atmoTag}");
            }

            // Flat type dedup signal
            var usedTypes = state.Locations.Values
                .Select(l => l.Type)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .ToList();
            if (usedTypes.Count > 0)
                sb.AppendLine($"Types already used (prefer variety): {string.Join(", ", usedTypes)}");
            sb.AppendLine();
        }

        // Active quests (quest-aware placement)
        var activeQuests = state.Player.ActiveQuests.Where(q => !q.IsComplete).ToList();
        if (activeQuests.Count > 0)
        {
            sb.AppendLine("Player's active quests (new location MAY serve as a quest destination if it fits naturally):");
            foreach (var q in activeQuests)
                sb.AppendLine($"  - {q.Title} ({q.Type})");
            sb.AppendLine();
        }

        // Danger progression guidance
        sb.AppendLine($"Danger level guidance for Level {state.Player.Level}:");
        if (state.Player.Level <= 2)
            sb.AppendLine("  Early game — prefer safe or moderate. Dangerous locations should be rare.");
        else if (state.Player.Level <= 4)
            sb.AppendLine("  Mid game — mix of moderate and dangerous. Occasional safe havens.");
        else
            sb.AppendLine("  Late game — dangerous and deadly become common. Safe locations are rare refuges.");
        sb.AppendLine();

        PromptParts.AppendRecentEvents(sb, state, 6);

        sb.AppendLine("\nGenerate a new location. Output ONLY the raw Location JSON object, no markdown fences, no explanation.");
        return sb.ToString();
    }
}
