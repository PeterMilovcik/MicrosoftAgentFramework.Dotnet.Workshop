using System.Text.Json.Serialization;

namespace RPGGameMaster.Domain;

/// <summary>
/// Full game state — serializable for save/load.
/// </summary>
internal sealed class GameState
{
    /// <summary>Unique save identifier (8-char hex), generated at new game creation.</summary>
    [JsonPropertyName("save_id")]
    public EntityId SaveId { get; set; } = EntityId.New();

    /// <summary>UTC timestamp of the last save.</summary>
    [JsonPropertyName("last_saved_at")]
    public DateTime LastSavedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("player")]
    public PlayerCharacter Player { get; set; } = new();

    [JsonPropertyName("current_location_id")]
    public EntityId CurrentLocationId { get; set; }

    [JsonPropertyName("world_theme")]
    public string WorldTheme { get; set; } = "";

    /// <summary>Language for all LLM-generated content (e.g. "English", "German", "Slovak"). Persisted with saves.</summary>
    [JsonPropertyName("language")]
    public string Language { get; set; } = "English";

    [JsonPropertyName("turn_count")]
    public int TurnCount { get; set; }

    [JsonPropertyName("locations")]
    public Dictionary<string, Location> Locations { get; set; } = new();

    [JsonPropertyName("npcs")]
    public Dictionary<string, NPC> NPCs { get; set; } = new();

    [JsonPropertyName("creatures")]
    public Dictionary<string, Creature> Creatures { get; set; } = new();

    /// <summary>Rolling game log — kept trimmed to manage token budgets.</summary>
    [JsonPropertyName("game_log")]
    public List<string> GameLog { get; set; } = [];

    public void AddLog(string entry)
        => GameLog.AddCapped(entry, GameConstants.MaxLogEntries);

    public Location? CurrentLocation
        => Locations.TryGetValue(CurrentLocationId, out var loc) ? loc : null;

    // ── Entity registration ──

    /// <summary>
    /// Register a location in the world. Sets <see cref="CurrentLocationId"/>
    /// if this is the first location.
    /// </summary>
    public void RegisterLocation(Location loc)
    {
        loc.Visited = true;
        Locations[loc.Id] = loc;
        if (CurrentLocationId.IsEmpty)
            CurrentLocationId = loc.Id;
        AddLog($"Discovered new location: {loc.Name}");
    }

    /// <summary>
    /// Register an NPC in the world and link it to the given location (if any).
    /// Duplicates are ignored.
    /// </summary>
    public void RegisterNPC(NPC npc, Location? location = null)
    {
        NPCs[npc.Id] = npc;
        if (location is not null && !location.NPCIds.Contains(npc.Id))
        {
            npc.LocationId = location.Id;
            location.NPCIds.Add(npc.Id);
        }
        AddLog($"Met {npc.Name}.");
    }

    /// <summary>
    /// Register a creature in the world and link it to the given location (if any).
    /// Duplicates are ignored.
    /// </summary>
    public void RegisterCreature(Creature creature, Location? location = null)
    {
        Creatures[creature.Id] = creature;
        if (location is not null && !location.CreatureIds.Contains(creature.Id))
        {
            creature.LocationId = location.Id;
            location.CreatureIds.Add(creature.Id);
        }
        AddLog($"A {creature.Name} lurks nearby.");
    }

    /// <summary>Generates the save filename based on sanitized player name + SaveId.</summary>
    public string GetSaveFileName()
    {
        var safeName = string.Concat(Player.Name.Where(c => char.IsLetterOrDigit(c) || c == '-'))
            .ToLowerInvariant();
        if (string.IsNullOrEmpty(safeName)) safeName = "hero";
        return $"save_{safeName}_{SaveId}.json";
    }
}
