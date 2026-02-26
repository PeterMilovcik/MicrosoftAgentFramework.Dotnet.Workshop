namespace RPGGameMaster.Models;

/// <summary>
/// DTO for shop items with price — used during NPC trade interactions.
/// </summary>
internal sealed class ShopItem
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public ItemType Type { get; set; } = ItemType.Misc;
    public int EffectValue { get; set; }
    public int Price { get; set; }
}
