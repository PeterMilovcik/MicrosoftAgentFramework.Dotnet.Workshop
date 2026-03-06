using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.AI;

namespace Workshop.Common;

/// <summary>
/// Read-only, sandbox-safe tools exposed to the agent via function calling.
/// All file access is restricted to the assets/sample-data/ subtree.
/// </summary>
public static class WorkshopTools
{
    // Compute the allowed root directory at startup
    private static readonly string AllowedRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "assets", "sample-data"));

    private const long MaxFileSizeBytes = 100 * 1024; // 100 KB
    private static readonly string[] AllowedExtensions = [".txt", ".md"];

    /// <summary>Returns the current UTC time as an ISO-8601 string.</summary>
    [Description("Returns the current UTC date and time in ISO-8601 format.")]
    public static string GetTime()
        => DateTime.UtcNow.ToString("O");

    /// <summary>
    /// Reads a text file from the sample-data directory.
    /// Path must be relative (e.g., "build-log-01.txt" or "kb/testing-guidelines.md").
    /// </summary>
    [Description("Reads a file from the workshop sample data directory. " +
                 "Path must be a relative path like 'build-log-01.txt' or 'kb/testing-guidelines.md'. " +
                 "Only .txt and .md files under assets/sample-data/ are accessible.")]
    public static string ReadFile(
        [Description("Relative path to the file within the sample-data directory.")] string path)
    {
        // Security: reject path traversal attempts
        if (path.Contains("..") || Path.IsPathRooted(path))
            return "⛔ Access denied: path must be relative and must not contain '..' segments.";

        // Check extension
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return $"⛔ Access denied: only {string.Join(", ", AllowedExtensions)} files are allowed.";

        // Build and validate the absolute path
        var fullPath = Path.GetFullPath(Path.Combine(AllowedRoot, path));
        if (!fullPath.StartsWith(AllowedRoot, StringComparison.OrdinalIgnoreCase))
            return "⛔ Access denied: path is outside the allowed directory.";

        if (!File.Exists(fullPath))
            return $"⚠️ File not found: {path}";

        // Check file size
        var info = new FileInfo(fullPath);
        if (info.Length > MaxFileSizeBytes)
            return $"⚠️ File too large ({info.Length / 1024}KB). Maximum allowed size is {MaxFileSizeBytes / 1024}KB.";

        return System.IO.File.ReadAllText(fullPath);
    }

    /// <summary>
    /// Performs a naive keyword search across all knowledge-base Markdown files.
    /// Returns up to 5 matching line snippets with file names.
    /// </summary>
    [Description("Searches the knowledge base (kb/*.md files) for lines matching the query keywords. " +
                 "Returns file name and matching line snippets (up to 5 results).")]
    public static string SearchKb(
        [Description("Keywords to search for in the knowledge base files.")] string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return "⚠️ Query must not be empty.";

        var kbDir = Path.Combine(AllowedRoot, "kb");
        if (!Directory.Exists(kbDir))
            return "⚠️ Knowledge base directory not found.";

        var keywords = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var results = new List<string>();

        foreach (var file in Directory.GetFiles(kbDir, "*.md"))
        {
            var fileName = Path.GetFileName(file);
            var lines = System.IO.File.ReadAllLines(file);
            foreach (var line in lines)
            {
                if (keywords.Any(kw => line.Contains(kw, StringComparison.OrdinalIgnoreCase)))
                {
                    results.Add($"[{fileName}] {line.Trim()}");
                    if (results.Count >= 5) break;
                }
            }
            if (results.Count >= 5) break;
        }

        if (results.Count == 0)
            return $"No results found for: {query}";

        var sb = new StringBuilder();
        sb.AppendLine($"Found {results.Count} result(s) for '{query}':");
        foreach (var r in results)
            sb.AppendLine($"  • {r}");
        return sb.ToString();
    }

    /// <summary>
    /// Returns the list of <see cref="AIFunction"/> tool wrappers to register with the agent.
    /// </summary>
    public static IList<AITool> GetTools()
    {
        return
        [
            AIFunctionFactory.Create(GetTime),
            AIFunctionFactory.Create(ReadFile),
            AIFunctionFactory.Create(SearchKb),
        ];
    }
}
