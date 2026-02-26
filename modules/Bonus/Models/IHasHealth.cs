namespace RPGGameMaster.Models;

/// <summary>
/// Shared combat stats surface for any entity that can take/deal damage.
/// Implemented by <see cref="PlayerCharacter"/> and <see cref="Creature"/>.
/// </summary>
internal interface IHasHealth
{
    HitPoints Health { get; set; }
    int Attack { get; set; }
    int Defense { get; set; }
}
