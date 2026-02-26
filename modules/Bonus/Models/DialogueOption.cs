namespace RPGGameMaster.Models;

/// <summary>
/// A numbered dialogue option for NPC conversation.
/// </summary>
internal sealed class DialogueOption
{
    public int Number { get; set; }
    public string Text { get; set; } = "";
    /// <summary>Structured farewell signal — true when this option ends the conversation.</summary>
    public bool IsFarewell { get; set; }
}
