using System.Text;
using RPGGameMaster.Models;

namespace RPGGameMaster;

/// <summary>
/// Builds prompt-context strings fed to sub-agents so they can generate
/// world-consistent locations, NPCs, creatures, and game-state summaries.
/// <para>
/// Shared blocks (world header, recent events, location detail) are factored
/// into private helpers to keep each public method focused on its unique context.
/// TODO: A fully fluent <c>PromptBuilder</c> could replace these helpers
///       once the prompt library grows beyond 4–5 contexts.
/// </para>
/// </summary>
internal static class ContextBuilder
{
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Shared building blocks
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private static void AppendGenerationHeader(StringBuilder sb)
        => sb.AppendLine("## GENERATION CONTEXT\n");

    private static void AppendWorldHeader(StringBuilder sb, GameState state, bool includeCombatStats = false)
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

    private static void AppendLocationDetail(StringBuilder sb, Location location, Difficulty? difficulty = null)
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

    private static void AppendRecentEvents(StringBuilder sb, GameState state, int count, string header = "Recent events:")
    {
        if (state.GameLog.Count == 0) return;
        sb.AppendLine(header);
        foreach (var entry in state.GameLog.TakeLast(count))
            sb.AppendLine($"  - {entry}");
    }

    private static void AppendNPCsAtLocation(StringBuilder sb, GameState state, Location location, string label)
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

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Public context methods
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    // ── Full game-state summary (for GM presenter) ──

    internal static string GameStateSummary(GameState state)
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

        AppendRecentEvents(sb, state, 10, "\nRecent events:");

        return sb.ToString();
    }

    // ── Location generation context ──

    internal static string LocationGeneration(
        GameState state, string? fromLocationId, Exit? entryExit)
    {
        var sb = new StringBuilder();
        AppendGenerationHeader(sb);
        AppendWorldHeader(sb, state);

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

        AppendRecentEvents(sb, state, 6);

        sb.AppendLine("\nGenerate a new location. Output ONLY the raw Location JSON object, no markdown fences, no explanation.");
        return sb.ToString();
    }

    // ── NPC generation context ──

    internal static string NPCGeneration(GameState state, Location location)
    {
        var sb = new StringBuilder();
        AppendGenerationHeader(sb);
        AppendWorldHeader(sb, state);
        AppendLocationDetail(sb, location);

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

        AppendRecentEvents(sb, state, 8, "\nRecent events in the world:");

        // Reward scaling guidance
        sb.AppendLine($"\nQuest reward guidance for Level {state.Player.Level}:");
        sb.AppendLine($"  Gold: {10 + state.Player.Level * 10}-{20 + state.Player.Level * 20}");
        sb.AppendLine($"  XP: {20 + state.Player.Level * 15}-{40 + state.Player.Level * 25}");

        return sb.ToString();
    }

    // ── Creature generation context ──

    internal static string CreatureGeneration(GameState state, Location location, Difficulty difficulty)
    {
        var sb = new StringBuilder();
        AppendGenerationHeader(sb);
        AppendWorldHeader(sb, state, includeCombatStats: true);
        AppendLocationDetail(sb, location, difficulty);

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

        AppendNPCsAtLocation(sb, state, location,
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

        AppendRecentEvents(sb, state, 5);

        return sb.ToString();
    }
}
