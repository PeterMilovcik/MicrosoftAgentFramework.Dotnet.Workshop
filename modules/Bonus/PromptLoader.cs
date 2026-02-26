namespace RPGGameMaster;

/// <summary>
/// Loads prompt markdown files from assets/prompts/ with a simple in-memory cache.
/// Replaces the <c>Func&lt;string, string&gt; loadPrompt</c> closure that was
/// threaded through multiple method layers in GameMasterWorkflow.
/// </summary>
internal static class PromptLoader
{
    private static readonly Dictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);
    private static string _baseDir = AppContext.BaseDirectory;

    /// <summary>Override the base directory (useful for tests).</summary>
    public static void SetBaseDir(string baseDir) => _baseDir = baseDir;

    /// <summary>
    /// Load a prompt file by name (without extension).
    /// Returns the file contents, or a generic fallback if the file does not exist.
    /// Results are cached for the process lifetime.
    /// </summary>
    public static string Load(string name)
    {
        if (_cache.TryGetValue(name, out var cached))
            return cached;

        var path = Path.Combine(_baseDir, "assets", "prompts", $"{name}.md");
        var content = File.Exists(path)
            ? File.ReadAllText(path)
            : $"You are the {name} agent.";

        _cache[name] = content;
        return content;
    }
}
