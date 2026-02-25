using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.AI;
using RPGGameMaster.Models;
using RPGGameMaster.Workflow;

namespace RPGGameMaster.Tools;

/// <summary>
/// Tools for examining, using, and querying items.
/// Assigned to the Item Sage agent.
/// </summary>
internal static class ItemTools
{

    private static GameState? _gameState;

    public static void SetGameState(GameState state) => _gameState = state;

    [Description("Returns the full JSON of an item from the player's inventory by name. Case-insensitive fuzzy match.")]
    public static string GetItemDetails([Description("The item name to look up")] string itemName)
    {
        if (_gameState is null) return "ERROR: No game state loaded.";

        var item = _gameState.Player.Inventory.FirstOrDefault(i =>
            i.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase));

        // Fuzzy fallback
        item ??= _gameState.Player.Inventory.FirstOrDefault(i =>
            i.Name.Contains(itemName, StringComparison.OrdinalIgnoreCase) ||
            itemName.Contains(i.Name, StringComparison.OrdinalIgnoreCase));

        if (item is null) return $"ERROR: Item '{itemName}' not found in inventory.";

        return JsonSerializer.Serialize(item, AgentHelper.JsonOpts);
    }

    [Description("Saves a rich lore description to an item in the player's inventory (caches it for future examine).")]
    public static string SetItemLore(
        [Description("The item name")] string itemName,
        [Description("The rich lore description to store")] string lore)
    {
        if (_gameState is null) return "ERROR: No game state loaded.";

        var item = _gameState.Player.Inventory.FirstOrDefault(i =>
            i.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase));

        item ??= _gameState.Player.Inventory.FirstOrDefault(i =>
            i.Name.Contains(itemName, StringComparison.OrdinalIgnoreCase) ||
            itemName.Contains(i.Name, StringComparison.OrdinalIgnoreCase));

        if (item is null) return $"ERROR: Item '{itemName}' not found in inventory.";

        item.Lore = lore;
        return $"OK: Lore saved for '{item.Name}'.";
    }

    [Description("Applies an item effect to the game state. Input: JSON with type, value, target, item_name fields.")]
    public static string ApplyItemEffect([Description("JSON: {\"type\": \"heal|unlock|narrative_only\", \"value\": 0, \"target\": \"\", \"item_name\": \"\"}")] string effectJson)
    {
        if (_gameState is null) return "ERROR: No game state loaded.";

        try
        {
            using var doc = JsonDocument.Parse(effectJson);
            var root = doc.RootElement;

            var effectType = root.TryGetProperty("type", out var tp) ? tp.GetString() ?? "" : "";
            var value = root.TryGetProperty("value", out var vp) ? vp.GetInt32() : 0;
            var target = root.TryGetProperty("target", out var tgt) ? tgt.GetString() ?? "" : "";
            var itemName = root.TryGetProperty("item_name", out var inp) ? inp.GetString() ?? "" : "";

            // Find the item
            var item = !string.IsNullOrEmpty(itemName)
                ? _gameState.Player.Inventory.FirstOrDefault(i =>
                    i.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase))
                  ?? _gameState.Player.Inventory.FirstOrDefault(i =>
                    i.Name.Contains(itemName, StringComparison.OrdinalIgnoreCase))
                : null;

            switch (effectType.ToLowerInvariant())
            {
                case "heal":
                    _gameState.Player.Health = _gameState.Player.Health.Heal(Math.Max(0, value), out var healAmt);
                    // Remove consumable
                    if (item is { IsConsumable: true })
                        _gameState.Player.Inventory.Remove(item);
                    return $"OK: Healed {healAmt} HP (now {_gameState.Player.Health}).{(item?.IsConsumable == true ? $" {item.Name} consumed." : "")}";

                case "unlock":
                    // Log the unlock — actual gate/door logic is handled by the GM narrative
                    _gameState.AddLog($"Used {itemName} to unlock {target}.");
                    // Keys are typically not consumed
                    if (item is { IsConsumable: true })
                        _gameState.Player.Inventory.Remove(item);
                    return $"OK: Unlocked '{target}'.{(item?.IsConsumable == true ? $" {item.Name} consumed." : "")}";

                case "narrative_only":
                    // No mechanical effect — just flavor
                    if (item is { IsConsumable: true })
                        _gameState.Player.Inventory.Remove(item);
                    return $"OK: Item used (narrative effect only).{(item?.IsConsumable == true ? $" {item.Name} consumed." : "")}";

                default:
                    return $"ERROR: Unknown effect type '{effectType}'. Use heal, unlock, or narrative_only.";
            }
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    public static IList<AITool> GetTools() =>
    [
        AIFunctionFactory.Create(GetItemDetails),
        AIFunctionFactory.Create(SetItemLore),
        AIFunctionFactory.Create(ApplyItemEffect),
    ];
}
