using System.Text.Json.Serialization;

namespace RPGGameMaster.Models;

/// <summary>
/// Full game state — serializable for save/load.
/// </summary>
internal sealed class GameState
{
    [JsonPropertyName("player")]
    public PlayerCharacter Player { get; set; } = new();

    [JsonPropertyName("current_location_id")]
    public string CurrentLocationId { get; set; } = "";

    [JsonPropertyName("world_theme")]
    public string WorldTheme { get; set; } = "";

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

    public const int MaxLogEntries = 20;

    public void AddLog(string entry)
    {
        GameLog.Add(entry);
        while (GameLog.Count > MaxLogEntries)
            GameLog.RemoveAt(0);
    }

    public Location? CurrentLocation
        => Locations.TryGetValue(CurrentLocationId, out var loc) ? loc : null;
}
