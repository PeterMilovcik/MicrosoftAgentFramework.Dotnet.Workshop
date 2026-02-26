using System.Text;

namespace RPGGameMaster;

/// <summary>
/// Builds the repeated "Language: X — …" instruction that gets prepended to
/// LLM prompts when the player has chosen a non-English language.
/// </summary>
internal static class LanguageHint
{
    private const string DefaultDetail =
        "all player-facing text MUST be in this language. JSON keys stay English.";

    /// <summary>
    /// Returns the hint string, or <c>""</c> for English.
    /// </summary>
    public static string For(string language, string detail = DefaultDetail)
        => language == "English" ? "" : $"Language: {language} — {detail}";

    /// <summary>
    /// Appends a language-hint line to <paramref name="sb"/> (no-op for English).
    /// </summary>
    public static void AppendTo(StringBuilder sb, string language, string detail = DefaultDetail)
    {
        if (language != "English")
            sb.AppendLine(For(language, detail));
    }
}
