namespace RPGGameMaster.Domain;

/// <summary>
/// Common contract for game-world entities that have a unique <see cref="EntityId"/>
/// and a human-readable name. Implemented by <see cref="Location"/>,
/// <see cref="NPC"/>, and <see cref="Creature"/>.
/// </summary>
internal interface IEntity
{
    EntityId Id { get; }
    string Name { get; }
}
