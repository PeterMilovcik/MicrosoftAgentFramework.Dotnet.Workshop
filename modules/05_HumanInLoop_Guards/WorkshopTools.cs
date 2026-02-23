using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.AI;

namespace HumanInLoopGuards;

internal static class WorkshopTools
{
    private static readonly string AllowedRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "assets", "sample-data"));

    private const long MaxFileSizeBytes = 100 * 1024;
    private static readonly string[] AllowedExtensions = [".txt", ".md"];

    [Description("Returns the current UTC date and time in ISO-8601 format.")]
    public static string GetTime() => DateTime.UtcNow.ToString("O");

    [Description("Reads a file from sample-data. Path is relative (e.g. 'build-log-01.txt').")]
    public static string ReadFile(
        [Description("Relative path within sample-data.")] string path)
    {
        if (path.Contains("..") || Path.IsPathRooted(path))
            return "⛔ Access denied: no path traversal allowed.";
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return $"⛔ Only {string.Join(", ", AllowedExtensions)} files allowed.";
        var fullPath = Path.GetFullPath(Path.Combine(AllowedRoot, path));
        if (!fullPath.StartsWith(AllowedRoot, StringComparison.OrdinalIgnoreCase))
            return "⛔ Access denied: outside allowed directory.";
        if (!File.Exists(fullPath)) return $"⚠️ File not found: {path}";
        var info = new FileInfo(fullPath);
        if (info.Length > MaxFileSizeBytes) return $"⚠️ File too large.";
        return File.ReadAllText(fullPath);
    }

    [Description("Searches kb/*.md files for matching keywords. Returns up to 5 snippets.")]
    public static string SearchKb(
        [Description("Keywords to search for.")] string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return "⚠️ Query must not be empty.";
        var kbDir = Path.Combine(AllowedRoot, "kb");
        if (!Directory.Exists(kbDir)) return "⚠️ KB directory not found.";
        var keywords = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var results = new List<string>();
        foreach (var file in Directory.GetFiles(kbDir, "*.md"))
        {
            var fileName = Path.GetFileName(file);
            foreach (var line in File.ReadAllLines(file))
            {
                if (keywords.Any(kw => line.Contains(kw, StringComparison.OrdinalIgnoreCase)))
                {
                    results.Add($"[{fileName}] {line.Trim()}");
                    if (results.Count >= 5) break;
                }
            }
            if (results.Count >= 5) break;
        }
        if (results.Count == 0) return $"No results for: {query}";
        var sb = new StringBuilder();
        sb.AppendLine($"Found {results.Count} result(s) for '{query}':");
        foreach (var r in results) sb.AppendLine($"  • {r}");
        return sb.ToString();
    }

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
