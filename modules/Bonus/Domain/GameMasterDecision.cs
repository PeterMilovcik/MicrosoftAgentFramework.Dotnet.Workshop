using System.Text.Json.Serialization;

namespace RPGGameMaster.Domain;

// ── Inner routing decision (GM → sub-agent) ──

/// <summary>
/// The Game Master's internal routing decision — which sub-agent to activate and why.
/// Not shown to the player.
/// </summary>
internal sealed class GameMasterDecision
{
    /// <summary>world_architect, npc_weaver, creature_forger, combat_narrator, npc_dialogue, item_sage, PRESENT_TO_PLAYER</summary>
    [JsonPropertyName("next_agent")]
    public string NextAgent { get; init; } = "";

    [JsonPropertyName("reason")]
    public string Reason { get; init; } = "";

    [JsonPropertyName("task")]
    public string Task { get; init; } = "";
}

// ── Player-facing presentation ──

/// <summary>
/// A numbered option presented to the player.
/// </summary>
internal sealed class GameOption : INumberedOption
{
    [JsonPropertyName("number")]
    public int Number { get; init; }

    [JsonPropertyName("description")]
    public string Description { get; init; } = "";

    /// <summary>move, talk, fight, pickup, use_item, examine, rest, look_around, check_quests, inventory, map, trade, save_game, quit</summary>
    [JsonPropertyName("action_type")]
    public ActionType ActionType { get; init; }

    /// <summary>Optional target identifier (location exit direction, NPC id, creature id, item name).</summary>
    [JsonPropertyName("target")]
    public string Target { get; init; } = "";
}

/// <summary>
/// What the Game Master presents to the player each turn: a narrative paragraph + numbered options.
/// </summary>
internal sealed class PlayerPresentation
{
    [JsonPropertyName("narrative")]
    public string Narrative { get; init; } = "";

    [JsonPropertyName("options")]
    public List<GameOption> Options { get; init; } = [];
}
