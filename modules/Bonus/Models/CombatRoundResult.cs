using System.Text.Json.Serialization;

namespace RPGGameMaster.Models;

/// <summary>
/// The authoritative output of CombatResolver — all dice rolls and damage numbers.
/// Produced by pure C# math, no LLM involved.
/// </summary>
internal sealed class CombatRoundResult
{
    // ── Player attack ──
    public int PlayerAttackRoll { get; set; }
    public int PlayerAttackTotal { get; set; }  // roll + bonuses
    public bool PlayerHit { get; set; }
    public bool PlayerCrit { get; set; }
    public int PlayerDamageRoll { get; set; }
    public int PlayerDamageDealt { get; set; }  // final after defense
    public int SelfDamage { get; set; }

    // ── Creature attack ──
    public int CreatureAttackRoll { get; set; }
    public int CreatureAttackTotal { get; set; }
    public bool CreatureHit { get; set; }
    public bool CreatureCrit { get; set; }
    public int CreatureDamageRoll { get; set; }
    public int CreatureDamageTaken { get; set; }  // final damage to player

    // ── Counter attack (for defensive moves) ──
    public int CounterRoll { get; set; }
    public bool CounterHit { get; set; }
    public int CounterDamage { get; set; }

    // ── Flee ──
    public int FleeRoll { get; set; }
    public bool FledSuccessfully { get; set; }

    // ── Item use ──
    public int HealAmount { get; set; }
    public string? ItemUsed { get; set; }

    // ── Summary ──
    public int TotalDamageToCreature { get; set; }
    public int TotalDamageToPlayer { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter<MoveType>))]
    public MoveType MoveType { get; set; }
    public string MoveName { get; set; } = "";
}
