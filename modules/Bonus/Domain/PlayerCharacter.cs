using System.Text.Json.Serialization;

namespace RPGGameMaster.Domain;

/// <summary>
/// The player's character — stats, inventory, active quests.
/// </summary>
internal sealed class PlayerCharacter : IHasHealth
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "Hero";

    [JsonPropertyName("hp")]
    public int HP { get; set; } = 100;

    [JsonPropertyName("max_hp")]
    public int MaxHP { get; set; } = 100;

    /// <summary>Value-object accessor for HP/MaxHP with clamped domain arithmetic.</summary>
    [JsonIgnore]
    public HitPoints Health
    {
        get => new(HP, MaxHP);
        set { HP = value.Current; MaxHP = value.Max; }
    }

    [JsonPropertyName("attack")]
    public int Attack { get; set; } = 5;

    [JsonPropertyName("defense")]
    public int Defense { get; set; } = 3;

    [JsonPropertyName("level")]
    public int Level { get; set; } = 1;

    [JsonPropertyName("xp")]
    public Experience XP { get; set; }

    [JsonPropertyName("xp_to_next_level")]
    public int XPToNextLevel { get; set; } = 100;

    [JsonPropertyName("gold")]
    public Gold Gold { get; set; }

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
        Health = Health.IncreaseMax(GameConstants.LevelUpHPBonus, restoreToMax: true);
        Attack += GameConstants.LevelUpAttackBonus;
        Defense += GameConstants.LevelUpDefenseBonus;
        XPToNextLevel = Level * 100;
        return true;
    }

    /// <summary>Awards experience points.</summary>
    public void AddXP(int amount) => XP = XP.Add(amount);

    /// <summary>
    /// Removes a fraction of current XP as a penalty (e.g. death).
    /// Returns the amount lost.
    /// </summary>
    public int LoseXP(double fraction)
    {
        XP = XP.ApplyPenalty(fraction, out var lost);
        return lost;
    }

    /// <summary>Compute effective attack (base + best weapon bonus).</summary>
    public int EffectiveAttack
        => Attack + Inventory.Where(i => i.Type == ItemType.Weapon).Select(i => i.EffectValue).DefaultIfEmpty(0).Max();

    /// <summary>Compute effective defense (base + best armor bonus).</summary>
    public int EffectiveDefense
        => Defense + Inventory.Where(i => i.Type == ItemType.Armor).Select(i => i.EffectValue).DefaultIfEmpty(0).Max();
}
