using System.Text.Json.Serialization;

namespace RPGGameMaster.Models;

/// <summary>
/// A creature that can be encountered and fought.
/// </summary>
internal sealed class Creature
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("location_id")]
    public string LocationId { get; set; } = "";

    [JsonPropertyName("hp")]
    public int HP { get; set; } = 20;

    [JsonPropertyName("max_hp")]
    public int MaxHP { get; set; } = 20;

    [JsonPropertyName("attack")]
    public int Attack { get; set; } = 4;

    [JsonPropertyName("defense")]
    public int Defense { get; set; } = 2;

    /// <summary>easy, medium, hard, boss</summary>
    [JsonPropertyName("difficulty")]
    public string Difficulty { get; set; } = "easy";

    [JsonPropertyName("loot")]
    public List<Item> Loot { get; set; } = [];

    [JsonPropertyName("xp_reward")]
    public int XPReward { get; set; } = 25;

    [JsonPropertyName("is_defeated")]
    public bool IsDefeated { get; set; }
}
