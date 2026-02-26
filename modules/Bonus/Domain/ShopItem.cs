namespace RPGGameMaster.Domain;

/// <summary>
/// DTO for shop items with price — used during NPC trade interactions.
/// </summary>
internal sealed class ShopItem
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public ItemType Type { get; init; } = ItemType.Misc;
    public int EffectValue { get; init; }
    public int Price { get; init; }
}
