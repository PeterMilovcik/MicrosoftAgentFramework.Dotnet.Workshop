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

        var rolls = new int[count];
        for (var i = 0; i < count; i++)
            rolls[i] = Random.Shared.Next(1, sides + 1);

        var total = rolls.Sum();
        return $"Rolled {count}d{sides}: [{string.Join(", ", rolls)}] = {total}";
    }

    // ── Player stats ──

    // A reference to the in-memory game state, set by the workflow before combat.
    private static GameState? _gameState;

    public static void SetGameState(GameState state) => _gameState = state;

    [Description("Returns the player's current stats as JSON.")]
    public static string GetPlayerStats()
    {
        if (_gameState is null) return "ERROR: No game state loaded.";
        return JsonSerializer.Serialize(_gameState.Player, AgentHelper.JsonOpts);
    }

    [Description("Updates the player's HP and/or gold. Input JSON with optional fields: hp, gold, xp.")]
    public static string UpdatePlayerStats([Description("JSON with fields to update: {hp, gold, xp}")] string updateJson)
    {
        if (_gameState is null) return "ERROR: No game state loaded.";
        try
        {
            using var doc = JsonDocument.Parse(updateJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("hp", out var hpProp))
                _gameState.Player.HP = Math.Clamp(hpProp.GetInt32(), 0, _gameState.Player.MaxHP);
            if (root.TryGetProperty("gold", out var goldProp))
                _gameState.Player.Gold = Math.Max(0, goldProp.GetInt32());
            if (root.TryGetProperty("xp", out var xpProp))
                _gameState.Player.XP = Math.Max(0, xpProp.GetInt32());

            return $"OK: Player stats updated. HP={_gameState.Player.HP}/{_gameState.Player.MaxHP}, Gold={_gameState.Player.Gold}, XP={_gameState.Player.XP}";
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
