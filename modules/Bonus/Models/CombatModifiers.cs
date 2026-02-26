namespace RPGGameMaster.Models;

/// <summary>
/// Clamped combat modifiers extracted from an LLM-generated <see cref="CombatMove"/>.
/// All values are bounded to safe ranges on construction via <see cref="FromMove"/>.
/// </summary>
internal readonly record struct CombatModifiers(int Attack, int Damage, int Defense, int SelfDamage)
{
    // Allowed ranges for each modifier
    private const int MinAttackBonus = -3;
    private const int MaxAttackBonus = 5;
    private const int MinDamageBonus = -2;
    private const int MaxDamageBonus = 8;
    private const int MinDefenseBonus = 0;
    private const int MaxDefenseBonus = 5;
    private const int MinSelfDamage = 0;
    private const int MaxSelfDamage = 10;

    /// <summary>Extracts and clamps modifiers from a <see cref="CombatMove"/>.</summary>
    public static CombatModifiers FromMove(CombatMove move) => new(
        Attack: Math.Clamp(move.AttackBonus, MinAttackBonus, MaxAttackBonus),
        Damage: Math.Clamp(move.DamageBonus, MinDamageBonus, MaxDamageBonus),
        Defense: Math.Clamp(move.DefenseBonus, MinDefenseBonus, MaxDefenseBonus),
        SelfDamage: Math.Clamp(move.SelfDamage, MinSelfDamage, MaxSelfDamage));

    /// <summary>Neutral modifiers (all zeros).</summary>
    public static CombatModifiers None => default;
}
