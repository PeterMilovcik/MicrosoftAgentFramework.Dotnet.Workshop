using System.Text.Json.Serialization;

namespace RPGGameMaster.Models;

/// <summary>
/// The player's character — stats, inventory, active quests.
/// </summary>
internal sealed class PlayerCharacter
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "Hero";

    [JsonPropertyName("hp")]
    public int HP { get; set; } = 100;

    [JsonPropertyName("max_hp")]
    public int MaxHP { get; set; } = 100;

    [JsonPropertyName("attack")]
    public int Attack { get; set; } = 5;

    [JsonPropertyName("defense")]
    public int Defense { get; set; } = 3;

    [JsonPropertyName("level")]
    public int Level { get; set; } = 1;

    [JsonPropertyName("xp")]
    public int XP { get; set; }

    [JsonPropertyName("xp_to_next_level")]
    public int XPToNextLevel { get; set; } = 100;

    [JsonPropertyName("gold")]
    public int Gold { get; set; }

    [JsonPropertyName("inventory")]
    public List<Item> Inventory { get; set; } = [];

    [JsonPropertyName("active_quests")]
    public List<Quest> ActiveQuests { get; set; } = [];

    /// <summary>Check if player has enough XP and level up if so.</summary>
    public bool TryLevelUp()
    {
        if (XP < XPToNextLevel) return false;
        XP -= XPToNextLevel;
        Level++;
        MaxHP += 10;
        HP = MaxHP;
        Attack += 2;
        Defense += 1;
        XPToNextLevel = Level * 100;
        return true;
    }

    /// <summary>Compute effective attack (base + best weapon bonus).</summary>
    public int EffectiveAttack
        => Attack + Inventory.Where(i => i.Type == "weapon").Select(i => i.EffectValue).DefaultIfEmpty(0).Max();

    /// <summary>Compute effective defense (base + best armor bonus).</summary>
    public int EffectiveDefense
        => Defense + Inventory.Where(i => i.Type == "armor").Select(i => i.EffectValue).DefaultIfEmpty(0).Max();
}
