namespace RPGGameMaster;

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
}
