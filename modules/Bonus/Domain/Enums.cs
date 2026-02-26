using System.Text.Json.Serialization;

namespace RPGGameMaster.Domain;

/// <summary>weapon, armor, potion, scroll, food, key, misc</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ItemType>))]
internal enum ItemType
{
    Weapon,
    Armor,
    Potion,
    Scroll,
    Food,
    Key,
    Misc,
}

/// <summary>common, uncommon, rare, legendary</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ItemRarity>))]
internal enum ItemRarity
{
    Common,
    Uncommon,
    Rare,
    Legendary,
}

/// <summary>attack, heavy, defensive, flee, item</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MoveType>))]
internal enum MoveType
{
    Attack,
    Heavy,
    Defensive,
    Flee,
    Item,
}

/// <summary>safe, moderate, dangerous, deadly</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DangerLevel>))]
internal enum DangerLevel
{
    Safe,
    Moderate,
    Dangerous,
    Deadly,
}

/// <summary>easy, medium, hard, boss</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Difficulty>))]
internal enum Difficulty
{
    Easy,
    Medium,
    Hard,
    Boss,
}

/// <summary>fetch, defeat, explore</summary>
[JsonConverter(typeof(JsonStringEnumConverter<QuestType>))]
internal enum QuestType
{
    Fetch,
    Defeat,
    Explore,
}

/// <summary>move, talk, fight, pickup, use_item, examine, rest, look_around, check_quests, inventory, map, trade, save_game, quit</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ActionType>))]
internal enum ActionType
{
    Move,
    Talk,
    Fight,
    Pickup,
    [JsonStringEnumMemberName("use_item")]
    UseItem,
    Examine,
    Rest,
    [JsonStringEnumMemberName("look_around")]
    LookAround,
    [JsonStringEnumMemberName("check_quests")]
    CheckQuests,
    Inventory,
    Map,
    Trade,
    [JsonStringEnumMemberName("save_game")]
    SaveGame,
    Quit,
}

/// <summary>creature_defeated, player_fled, player_defeated</summary>
internal enum CombatResult
{
    CreatureDefeated,
    PlayerFled,
    PlayerDefeated,
}

/// <summary>
/// Extension methods for domain enums that encapsulate associated behavior.
/// </summary>
internal static class EnumExtensions
{
    // ── ItemType ──

    public static bool IsEquippable(this ItemType type) => type is ItemType.Weapon or ItemType.Armor;
    public static bool IsConsumable(this ItemType type) => type is ItemType.Potion or ItemType.Scroll or ItemType.Food;
    public static bool IsUsable(this ItemType type) => type is ItemType.Potion or ItemType.Scroll or ItemType.Food or ItemType.Key;
    public static int SellPriceMultiplier(this ItemType type) => type.IsEquippable() ? 3 : 1;

    /// <summary>Compute sell price: equipment uses 3x EffectValue, others use 1x, minimum 1.</summary>
    public static int SellPrice(this ItemType type, int effectValue)
        => Math.Max(1, effectValue * type.SellPriceMultiplier());

    // ── DangerLevel ──

    /// <summary>NPC spawn probability by danger level (independent roll — both NPC and creature can spawn or neither).</summary>
    public static double NpcSpawnChance(this DangerLevel level) => level switch
    {
        DangerLevel.Safe => 0.80,
        DangerLevel.Moderate => 0.55,
        DangerLevel.Dangerous => 0.30,
        DangerLevel.Deadly => 0.15,
        _ => 0.55,
    };

    /// <summary>Creature spawn probability by danger level (independent roll — both NPC and creature can spawn or neither).</summary>
    public static double CreatureSpawnChance(this DangerLevel level) => level switch
    {
        DangerLevel.Safe => 0.20,
        DangerLevel.Moderate => 0.55,
        DangerLevel.Dangerous => 0.75,
        DangerLevel.Deadly => 0.90,
        _ => 0.55,
    };

    // ── Difficulty ──

    /// <summary>Map player level to appropriate creature difficulty.</summary>
    public static Difficulty FromPlayerLevel(int level) => level switch
    {
        <= 2 => Difficulty.Easy,
        <= 4 => Difficulty.Medium,
        _ => Difficulty.Hard,
    };
}
