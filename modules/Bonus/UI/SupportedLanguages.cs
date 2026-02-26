namespace RPGGameMaster.UI;

/// <summary>
/// Defines the set of languages selectable during character creation.
/// Display + value are co-located so the selection menu and mapping
/// are driven from a single list — no duplicate switch statements.
/// </summary>
internal static class SupportedLanguages
{
    public record LanguageOption(string Label, string Value);

    public static readonly IReadOnlyList<LanguageOption> All =
    [
        new("🇬🇧 English",              "English"),
        new("🇩🇪 Deutsch (German)",      "German"),
        new("🇫🇷 Français (French)",     "French"),
        new("🇪🇸 Español (Spanish)",     "Spanish"),
        new("🇮🇹 Italiano (Italian)",    "Italian"),
        new("🇵🇹 Português (Portuguese)","Portuguese"),
        new("🇳🇱 Nederlands (Dutch)",    "Dutch"),
        new("🇵🇱 Polski (Polish)",       "Polish"),
        new("🇨🇿 Čeština (Czech)",       "Czech"),
        new("🇸🇰 Slovenčina (Slovak)",   "Slovak"),
        new("🇺🇦 Українська (Ukrainian)","Ukrainian"),
        new("🇯🇵 日本語 (Japanese)",      "Japanese"),
        new("🇰🇷 한국어 (Korean)",        "Korean"),
    ];

    /// <summary>Default language when input is invalid or empty.</summary>
    public const string Default = "English";

    /// <summary>
    /// Resolve a 1-based selection number to a language value.
    /// Returns <see cref="Default"/> for invalid input.
    /// </summary>
    public static string FromSelection(string? input)
    {
        if (int.TryParse(input, out var idx) && idx >= 1 && idx <= All.Count)
            return All[idx - 1].Value;
        return Default;
    }
}
