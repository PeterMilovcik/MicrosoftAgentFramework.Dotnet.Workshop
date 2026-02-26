namespace RPGGameMaster.Domain;

/// <summary>
/// A numbered dialogue option for NPC conversation.
/// </summary>
internal sealed class DialogueOption : INumberedOption
{
    public int Number { get; init; }
    public string Text { get; init; } = "";
    /// <summary>Structured farewell signal — true when this option ends the conversation.</summary>
    public bool IsFarewell { get; init; }
}
