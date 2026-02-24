using System.Text.Json.Serialization;

namespace RPGGameMaster.Models;

/// <summary>
/// A quest offered by an NPC.
/// </summary>
internal sealed class Quest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("giver_npc_id")]
    public string GiverNPCId { get; set; } = "";

    /// <summary>fetch, defeat, explore</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "fetch";

    /// <summary>The target item, creature, or location id.</summary>
    [JsonPropertyName("target_id")]
    public string TargetId { get; set; } = "";

    [JsonPropertyName("reward_gold")]
    public int RewardGold { get; set; }

    [JsonPropertyName("reward_xp")]
    public int RewardXP { get; set; }

    [JsonPropertyName("reward_item")]
    public Item? RewardItem { get; set; }

    [JsonPropertyName("is_complete")]
    public bool IsComplete { get; set; }
}
