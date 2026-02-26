namespace RPGGameMaster.Domain;

/// <summary>
/// Marker interface for menu items that carry a numeric selector.
/// Implemented by <see cref="GameOption"/>, <see cref="CombatMove"/>,
/// and <see cref="DialogueOption"/>, enabling a generic
/// <see cref="GameConsoleUI"/> prompt method.
/// </summary>
internal interface INumberedOption
{
    int Number { get; }
}
