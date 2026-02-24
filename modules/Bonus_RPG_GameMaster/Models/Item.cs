using System.Text.Json.Serialization;

namespace RPGGameMaster.Models;

/// <summary>
/// An item that can be found, looted, bought, or used by the player.
/// </summary>
internal sealed class Item
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    /// <summary>weapon, armor, potion, key, misc</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "misc";

    /// <summary>
    /// Positive number: damage bonus (weapon), armor bonus (armor), heal amount (potion).
    /// Zero for key/misc.
    /// </summary>
    [JsonPropertyName("effect_value")]
    public int EffectValue { get; set; }
}
