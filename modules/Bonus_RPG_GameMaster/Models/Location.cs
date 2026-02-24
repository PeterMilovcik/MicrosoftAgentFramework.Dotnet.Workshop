using System.Text.Json.Serialization;

namespace RPGGameMaster.Models;

/// <summary>
/// An exit/passage from one location to another.
/// </summary>
internal sealed class Exit
{
    /// <summary>Compass or descriptive direction, e.g. "North", "Down the stairs".</summary>
    [JsonPropertyName("direction")]
    public string Direction { get; set; } = "";

    /// <summary>Short description of where this exit seems to lead.</summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    /// <summary>
    /// The id of the target location, or null/empty if unexplored (will be generated on demand).
    /// </summary>
    [JsonPropertyName("target_location_id")]
    public string? TargetLocationId { get; set; }
}

/// <summary>
/// A world location — a room, clearing, street, cavern, etc.
/// </summary>
internal sealed class Location
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    /// <summary>Matches the world theme or a sub-theme.</summary>
    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "";

    [JsonPropertyName("exits")]
    public List<Exit> Exits { get; set; } = [];

    [JsonPropertyName("npc_ids")]
    public List<string> NPCIds { get; set; } = [];

    [JsonPropertyName("creature_ids")]
    public List<string> CreatureIds { get; set; } = [];

    [JsonPropertyName("items")]
    public List<Item> Items { get; set; } = [];

    [JsonPropertyName("visited")]
    public bool Visited { get; set; }
}
