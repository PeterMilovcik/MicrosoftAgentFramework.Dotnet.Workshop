using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.AI;
using RPGGameMaster.Models;
using RPGGameMaster.Workflow;

namespace RPGGameMaster.Tools;

/// <summary>
/// General-purpose game tools: dice rolling, save/load game, player stats.
/// Assigned to the Combat Narrator agent (dice + stats) and available to the Game Master workflow (save/load).
/// </summary>
internal static class GameTools
{

    // ── Dice ──

    [Description("Rolls dice and returns results. Example: count=2, sides=6 rolls 2d6.")]
    public static string RollDice(
        [Description("Number of dice to roll")] int count,
        [Description("Number of sides per die (e.g. 6, 20)")] int sides)
    {
        if (count < 1 || count > 10) return "ERROR: count must be 1-10.";
        if (sides < 2 || sides > 100) return "ERROR: sides must be 2-100.";

        var rolls = Dice.RollEach(count, sides);
        var total = rolls.Sum();
        return $"Rolled {count}d{sides}: [{string.Join(", ", rolls)}] = {total}";
    }

    // ── Player stats ──

    [Description("Returns the player's current stats as JSON.")]
    public static string GetPlayerStats()
    {
        if (!GameStateAccessor.IsLoaded) return "ERROR: No game state loaded.";
        return JsonSerializer.Serialize(GameStateAccessor.Current.Player, AgentHelper.JsonOpts);
    }

    [Description("Updates the player's HP and/or gold. Input JSON with optional fields: hp, gold, xp.")]
    public static string UpdatePlayerStats([Description("JSON with fields to update: {hp, gold, xp}")] string updateJson)
    {
        if (!GameStateAccessor.IsLoaded) return "ERROR: No game state loaded.";
        var player = GameStateAccessor.Current.Player;
        try
        {
            using var doc = JsonDocument.Parse(updateJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("hp", out var hpProp))
                player.Health = new HitPoints(hpProp.GetInt32(), player.Health.Max);
            if (root.TryGetProperty("gold", out var goldProp))
                player.Gold = new Gold(goldProp.GetInt32());
            if (root.TryGetProperty("xp", out var xpProp))
                player.XP = Math.Max(0, xpProp.GetInt32());

            return $"OK: Player stats updated. HP={player.Health}, Gold={player.Gold}, XP={player.XP}";
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    // ── Load Game (removed — multi-save managed by workflow) ──

    public static IList<AITool> GetCombatTools() =>
    [
        AIFunctionFactory.Create(RollDice),
        AIFunctionFactory.Create(GetPlayerStats),
        AIFunctionFactory.Create(UpdatePlayerStats),
    ];
}
