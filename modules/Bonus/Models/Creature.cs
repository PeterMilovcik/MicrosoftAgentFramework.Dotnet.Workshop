using System.Text.Json.Serialization;

namespace RPGGameMaster.Models;

/// <summary>
/// A creature that can be encountered and fought.
/// </summary>
internal sealed class Creature : IHasHealth
{
    [JsonPropertyName("id")]
    public EntityId Id { get; set; } = EntityId.New();

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("location_id")]
    public EntityId LocationId { get; set; }

    [JsonPropertyName("hp")]
    public int HP { get; set; } = 20;

    [JsonPropertyName("max_hp")]
    public int MaxHP { get; set; } = 20;

    /// <summary>Value-object accessor for HP/MaxHP with clamped domain arithmetic.</summary>
    [JsonIgnore]
    public HitPoints Health
    {
        get => new(HP, MaxHP);
        set { HP = value.Current; MaxHP = value.Max; }
    }

    [JsonPropertyName("attack")]
    public int Attack { get; set; } = 4;

    [JsonPropertyName("defense")]
    public int Defense { get; set; } = 2;

    /// <summary>easy, medium, hard, boss</summary>
    [JsonPropertyName("difficulty")]
    public Difficulty Difficulty { get; set; } = Difficulty.Easy;

    /// <summary>Combat personality (e.g., "ambush predator, retreats when wounded"). Fed to Combat Strategist/Narrator.</summary>
    [JsonPropertyName("behavior")]
    public string Behavior { get; set; } = "";

    /// <summary>World-lore snippet explaining the creature's origin or role in the location.</summary>
    [JsonPropertyName("lore")]
    public string Lore { get; set; } = "";

    [JsonPropertyName("loot")]
    public List<Item> Loot { get; set; } = [];

    [JsonPropertyName("xp_reward")]
    public int XPReward { get; set; } = 25;

    [JsonPropertyName("is_defeated")]
    public bool IsDefeated { get; set; }
}
