namespace RPGGameMaster.Domain.Combat;

/// <summary>
/// Pure C# deterministic combat math. Code-authoritative: all dice rolls happen here,
/// all damage is calculated here. No LLM involvement — this is the source of truth.
/// </summary>
internal static class CombatResolver
{
    // ── Base thresholds (d20) ──
    private const int HitThreshold = 8;       // d20 >= 8 to hit
    private const int HeavyHitThreshold = 12; // heavy attacks are harder to land
    private const int CritThreshold = 18;     // d20 >= 18 is critical
    private const int FleeThreshold = 10;     // d20 >= 10 to flee
    private const int CounterThreshold = 10;  // d20 >= 10 for defensive counter

    /// <summary>
    /// Resolve a single combat round. Does NOT mutate player or creature —
    /// the caller applies the result.
    /// </summary>
    public static CombatRoundResult Resolve(
        PlayerCharacter player, Creature creature, CombatMove move, Item? potionToUse = null)
    {
        var mods = CombatModifiers.FromMove(move);

        var result = new CombatRoundResult
        {
            MoveType = move.Type,
            MoveName = move.Name,
        };

        switch (move.Type)
        {
            case MoveType.Attack:
                ResolvePlayerAttack(result, player, creature, mods, HitThreshold);
                ResolveCreatureAttack(result, creature, player, mods);
                result.SelfDamage = 0;
                break;

            case MoveType.Heavy:
                ResolvePlayerAttack(result, player, creature, mods, HeavyHitThreshold, doubleDamage: true);
                ResolveCreatureAttack(result, creature, player, mods);
                result.SelfDamage = mods.SelfDamage;
                break;

            case MoveType.Defensive:
                ResolveDefensiveCounter(result, player, creature, mods);
                ResolveCreatureAttack(result, creature, player, mods, halfDamage: true);
                result.SelfDamage = 0;
                break;

            case MoveType.Flee:
                ResolveFlee(result);
                if (!result.FledSuccessfully)
                    ResolveCreatureAttack(result, creature, player, CombatModifiers.None);
                result.SelfDamage = 0;
                break;

            case MoveType.Item:
                ResolveItemUse(result, player, potionToUse);
                ResolveCreatureAttack(result, creature, player, CombatModifiers.None);
                result.SelfDamage = 0;
                break;

            default:
                // Unknown type — treat as basic attack
                ResolvePlayerAttack(result, player, creature, CombatModifiers.None, HitThreshold);
                ResolveCreatureAttack(result, creature, player, CombatModifiers.None);
                result.SelfDamage = 0;
                break;
        }

        // Sum totals
        result.TotalDamageToCreature = result.PlayerDamageDealt + result.CounterDamage;
        result.TotalDamageToPlayer = result.CreatureDamageTaken + result.SelfDamage;

        return result;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Resolution helpers
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private static void ResolvePlayerAttack(
        CombatRoundResult result,
        PlayerCharacter player, Creature creature,
        CombatModifiers mods, int hitThreshold,
        bool doubleDamage = false)
    {
        result.PlayerAttackRoll = Dice.Roll(1, 20);
        result.PlayerAttackTotal = result.PlayerAttackRoll + mods.Attack;
        result.PlayerHit = result.PlayerAttackTotal >= hitThreshold;
        result.PlayerCrit = result.PlayerAttackRoll >= CritThreshold; // crit based on raw roll

        if (result.PlayerHit)
        {
            result.PlayerDamageRoll = Dice.Roll(doubleDamage ? 2 : 1, 6);
            var rawDmg = result.PlayerDamageRoll + player.EffectiveAttack + mods.Damage - creature.Defense;
            rawDmg = Math.Max(1, rawDmg); // minimum 1 on a hit
            if (result.PlayerCrit) rawDmg *= 2;
            result.PlayerDamageDealt = rawDmg;
        }
    }

    private static void ResolveCreatureAttack(
        CombatRoundResult result,
        Creature creature, PlayerCharacter player,
        CombatModifiers mods, bool halfDamage = false)
    {
        result.CreatureAttackRoll = Dice.Roll(1, 20);
        result.CreatureAttackTotal = result.CreatureAttackRoll;
        result.CreatureHit = result.CreatureAttackRoll >= HitThreshold;
        result.CreatureCrit = result.CreatureAttackRoll >= CritThreshold;

        if (result.CreatureHit)
        {
            result.CreatureDamageRoll = Dice.Roll(1, 6);
            var rawDmg = result.CreatureDamageRoll + creature.Attack - player.EffectiveDefense - mods.Defense;
            rawDmg = Math.Max(1, rawDmg); // minimum 1 on a hit
            if (result.CreatureCrit) rawDmg *= 2;
            if (halfDamage) rawDmg /= 2; // defensive stance
            result.CreatureDamageTaken = Math.Max(0, rawDmg);
        }
    }

    private static void ResolveDefensiveCounter(
        CombatRoundResult result,
        PlayerCharacter player, Creature creature,
        CombatModifiers mods)
    {
        result.CounterRoll = Dice.Roll(1, 20);
        result.CounterHit = (result.CounterRoll + mods.Attack) >= CounterThreshold;

        if (result.CounterHit)
        {
            var counterDmg = Dice.Roll(1, 4) + (player.EffectiveAttack / 2) - creature.Defense;
            result.CounterDamage = Math.Max(1, counterDmg);
        }
    }

    private static void ResolveFlee(CombatRoundResult result)
    {
        result.FleeRoll = Dice.Roll(1, 20);
        result.FledSuccessfully = result.FleeRoll >= FleeThreshold;
    }

    private static void ResolveItemUse(
        CombatRoundResult result,
        PlayerCharacter player, Item? potion)
    {
        if (potion is null) return;
        result.ItemUsed = potion.Name;
        result.HealAmount = Math.Min(potion.EffectValue, player.Health.Max - player.Health.Current);
    }

}
