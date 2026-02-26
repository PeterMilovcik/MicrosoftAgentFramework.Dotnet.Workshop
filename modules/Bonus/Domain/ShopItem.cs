namespace RPGGameMaster.Domain;

/// <summary>
/// An item offered for sale by a merchant, with a price tag.
/// Wraps <see cref="Item"/> to avoid field duplication.
/// </summary>
internal sealed record ShopItem(Item Item, int Price)
{
    public string Name => Item.Name;
    public string Description => Item.Description;
    public ItemType Type => Item.Type;
    public int EffectValue => Item.EffectValue;

    /// <summary>Create from deserialized JSON fields (used when parsing LLM shop output).</summary>
    public static ShopItem FromRaw(string name, string description, ItemType type, int effectValue, int price)
        => new(new Item { Name = name, Description = description, Type = type, EffectValue = effectValue }, price);
}
