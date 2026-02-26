namespace RPGGameMaster.Shared;

/// <summary>
/// Shared game constants — magic numbers used across multiple classes.
/// </summary>
internal static class GameConstants
{
    /// <summary>Maximum entries kept in rolling logs (game log, dialogue history).</summary>
    public const int MaxLogEntries = 20;

    /// <summary>Maximum inner routing iterations per turn in the GM loop.</summary>
    public const int MaxInnerIterations = 6;

    /// <summary>Maximum dialogue rounds per NPC conversation.</summary>
    public const int MaxDialogueRounds = 10;

    /// <summary>Gold penalty fraction on death (50%).</summary>
    public const double DeathGoldPenaltyFraction = 0.5;

    /// <summary>XP penalty fraction on death (25%).</summary>
    public const double DeathXPPenaltyFraction = 0.25;

    /// <summary>HP heal fraction when resting (25% of max).</summary>
    public const double RestHealFraction = 0.25;

    /// <summary>Disposition improvement per completed conversation.</summary>
    public const int ConversationDispositionBoost = 5;

    /// <summary>Disposition improvement for nearby NPCs after defeating a creature.</summary>
    public const int CombatDispositionBoost = 10;

    /// <summary>Disposition improvement for quest giver upon quest completion.</summary>
    public const int QuestDispositionBoost = 20;

    /// <summary>HP increase per level up.</summary>
    public const int LevelUpHPBonus = 10;

    /// <summary>Attack increase per level up.</summary>
    public const int LevelUpAttackBonus = 2;

    /// <summary>Defense increase per level up.</summary>
    public const int LevelUpDefenseBonus = 1;

    /// <summary>NPC count when generating the starting location.</summary>
    public const int StartingNPCCount = 2;

    /// <summary>Probability of spawning a second NPC at non-starting locations.</summary>
    public const double ExtraNPCChance = 0.4;

    /// <summary>Probability of spawning a weak creature at the starting location (early combat tutorial).</summary>
    public const double StartingCreatureChance = 0.5;

    /// <summary>Probability of spawning a second creature at dangerous/deadly locations.</summary>
    public const double ExtraCreatureChance = 0.25;

    // ── Reward scaling (used in PromptContextFactory prompt hints) ──

    /// <summary>Base gold reward per quest (additive with level multiplier).</summary>
    public const int RewardGoldBase = 10;

    /// <summary>Gold reward multiplier per player level.</summary>
    public const int RewardGoldPerLevel = 10;

    /// <summary>Upper-bound gold reward base.</summary>
    public const int RewardGoldMaxBase = 20;

    /// <summary>Upper-bound gold reward multiplier per player level.</summary>
    public const int RewardGoldMaxPerLevel = 20;

    /// <summary>Base XP reward per quest.</summary>
    public const int RewardXPBase = 20;

    /// <summary>XP reward multiplier per player level.</summary>
    public const int RewardXPPerLevel = 15;

    /// <summary>Upper-bound XP reward base.</summary>
    public const int RewardXPMaxBase = 40;

    /// <summary>Upper-bound XP reward multiplier per player level.</summary>
    public const int RewardXPMaxPerLevel = 25;

    // ── Agent call resilience ──

    /// <summary>Maximum time (seconds) for a single LLM streaming call before cancelling.</summary>
    public const int AgentCallTimeoutSeconds = 45;

    /// <summary>Maximum retry attempts after a timeout or transient error (total attempts = 1 + retries).</summary>
    public const int AgentMaxRetries = 2;

    /// <summary>Initial backoff delay (ms) between retries. Doubles on each subsequent retry.</summary>
    public const int AgentRetryBaseDelayMs = 2000;
}
