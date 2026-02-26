using System.Text.Json.Serialization;

namespace RPGGameMaster.Models;

/// <summary>
/// A quest offered by an NPC.
/// </summary>
internal sealed class Quest
{
    [JsonPropertyName("id")]
    public EntityId Id { get; set; } = EntityId.New();

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("giver_npc_id")]
    public EntityId GiverNPCId { get; set; }

    /// <summary>fetch, defeat, explore</summary>
    [JsonPropertyName("type")]
    public QuestType Type { get; set; } = QuestType.Fetch;

    /// <summary>The target item, creature, or location id.</summary>
    [JsonPropertyName("target_id")]
    public EntityId TargetId { get; set; }

    [JsonPropertyName("reward_gold")]
    public int RewardGold { get; set; }

    [JsonPropertyName("reward_xp")]
    public int RewardXP { get; set; }

    [JsonPropertyName("reward_item")]
    public Item? RewardItem { get; set; }

    [JsonPropertyName("is_complete")]
    public bool IsComplete { get; set; }
}
