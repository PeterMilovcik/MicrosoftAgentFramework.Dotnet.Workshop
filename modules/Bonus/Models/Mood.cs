namespace RPGGameMaster.Models;

/// <summary>
/// Well-known mood values used in game logic. NPC mood remains a <c>string</c>
/// because the LLM can generate arbitrary mood descriptors, but code that checks
/// specific moods should reference these constants.
/// </summary>
internal static class Mood
{
    public const string Neutral = "neutral";
    public const string Anxious = "anxious";
    public const string Fearful = "fearful";
    public const string Wary = "wary";
    public const string Suspicious = "suspicious";
    public const string Relieved = "relieved";
    public const string Grateful = "grateful";

    /// <summary>Returns <c>true</c> if the mood indicates tension or worry.</summary>
    public static bool IsTense(string mood)
        => mood is Anxious or Fearful or Wary or Suspicious;
}
