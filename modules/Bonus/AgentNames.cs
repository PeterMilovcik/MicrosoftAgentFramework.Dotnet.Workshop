namespace RPGGameMaster;

/// <summary>
/// Canonical agent and prompt-file names used across the workflow layer.
/// Eliminates magic strings scattered in agentMap, routing switches, and prompt loading.
/// </summary>
internal static class AgentNames
{
    // ── Agent identifiers (used in agentMap keys, routing, and ProcessSubAgentOutput) ──

    public const string WorldArchitect = "world_architect";
    public const string NPCWeaver = "npc_weaver";
    public const string CreatureForger = "creature_forger";
    public const string CombatNarrator = "combat_narrator";
    public const string CombatStrategist = "combat_strategist";
    public const string ItemSage = "item_sage";
    public const string PresentToPlayer = "PRESENT_TO_PLAYER";

    // ── Prompt file names (assets/prompts/{name}.md) ──

    public const string GameMasterPrompt = "game-master";
    public const string WorldArchitectPrompt = "world-architect";
    public const string NPCWeaverPrompt = "npc-weaver";
    public const string CreatureForgerPrompt = "creature-forger";
    public const string CombatNarratorPrompt = "combat-narrator";
    public const string CombatStrategistPrompt = "combat-strategist";
    public const string ItemSagePrompt = "item-sage";
}
