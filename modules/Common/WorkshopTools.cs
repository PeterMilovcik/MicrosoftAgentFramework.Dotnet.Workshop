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
    private const int MaxSearchResults = 3;
    private const int MaxSectionCharacters = 4_000;
    private static readonly string[] AllowedExtensions = [".txt", ".md"];
    private static readonly HashSet<string> SearchStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "all", "an", "and", "are", "as", "at", "be", "by", "for", "from",
        "how", "i", "in", "is", "it", "knowledge", "me", "of", "on", "or", "our",
        "please", "search", "show", "that", "the", "this", "to", "use", "what",
        "when", "where", "which", "who", "with", "would", "you", "your",
    };

    /// <summary>Returns the current UTC time as an ISO-8601 string.</summary>
    [Description("Returns the current UTC date and time in ISO-8601 format.")]
    public static string GetTime()
        => DateTime.UtcNow.ToString("O");

    /// <summary>
    /// Reads a text file from the sample-data directory.
    /// Path must be relative (e.g., "build-log-01.txt" or "kb/testing-guidelines.md").
    /// Knowledge-base files may also be read by basename (e.g., "testing-guidelines.md").
    /// </summary>
    [Description("Reads a file from the workshop sample data directory. " +
                 "Path must be a relative path like 'build-log-01.txt' or 'kb/testing-guidelines.md'. " +
                 "A knowledge-base basename like 'release-notes.md' is resolved under the 'kb' directory. " +
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

        if (!File.Exists(fullPath) && string.IsNullOrEmpty(Path.GetDirectoryName(path)))
        {
            fullPath = Path.GetFullPath(Path.Combine(AllowedRoot, "kb", path));
        }

        if (!File.Exists(fullPath))
            return $"⚠️ File not found: {path}";

        // Check file size
        var info = new FileInfo(fullPath);
        if (info.Length > MaxFileSizeBytes)
            return $"⚠️ File too large ({info.Length / 1024}KB). Maximum allowed size is {MaxFileSizeBytes / 1024}KB.";

        return System.IO.File.ReadAllText(fullPath);
    }

    /// <summary>
    /// Searches and ranks top-level sections across all knowledge-base Markdown files.
    /// Returns a full document for a filename match, or up to 3 relevant sections.
    /// </summary>
    [Description("Searches the knowledge base (kb/*.md files) for relevant Markdown sections. " +
                 "Returns the full document when the query identifies a filename, otherwise up to 3 ranked sections. " +
                 "Source paths can be passed directly to ReadFile for more context.")]
    public static string SearchKb(
        [Description("Keywords to search for in the knowledge base files.")] string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return "⚠️ Query must not be empty.";

        var kbDir = Path.Combine(AllowedRoot, "kb");
        if (!Directory.Exists(kbDir))
            return "⚠️ Knowledge base directory not found.";

        var queryTerms = Tokenize(query);
        queryTerms.ExceptWith(SearchStopWords);
        if (queryTerms.Count == 0)
            return $"No searchable terms found for: {query}";

        var files = Directory.GetFiles(kbDir, "*.md")
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var documentMatch = files.FirstOrDefault(file =>
            queryTerms.IsSubsetOf(Tokenize(Path.GetFileNameWithoutExtension(file))));
        if (documentMatch is not null)
        {
            var relativePath = GetRelativeSourcePath(documentMatch);
            return $"Found knowledge-base document for '{query}':{Environment.NewLine}{Environment.NewLine}" +
                   $"[{relativePath}]{Environment.NewLine}{TruncateSection(File.ReadAllText(documentMatch))}";
        }

        var results = files
            .SelectMany(ReadMarkdownSections)
            .Select(section => new
            {
                Section = section,
                Score = ScoreSection(section, queryTerms),
            })
            .Where(result => result.Score > 0)
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Section.FileName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.Section.Order)
            .Take(MaxSearchResults)
            .ToList();

        if (results.Count == 0)
            return $"No results found for: {query}";

        var sb = new StringBuilder();
        sb.AppendLine($"Found {results.Count} relevant section(s) for '{query}':");
        foreach (var result in results)
        {
            sb.AppendLine();
            sb.AppendLine($"[{result.Section.FileName} > {result.Section.Heading}]");
            sb.AppendLine(TruncateSection(result.Section.Content));
        }

        return sb.ToString();
    }

    private static IEnumerable<MarkdownSection> ReadMarkdownSections(string file)
    {
        var sourcePath = GetRelativeSourcePath(file);
        var heading = Path.GetFileNameWithoutExtension(file);
        var content = new List<string>();
        var order = 0;

        foreach (var line in File.ReadLines(file))
        {
            if (TryGetSectionHeading(line, out var nextHeading))
            {
                if (content.Any(line => !string.IsNullOrWhiteSpace(line)))
                {
                    yield return new MarkdownSection(sourcePath, heading, string.Join(Environment.NewLine, content).Trim(), order++);
                }

                heading = nextHeading;
                content.Clear();
                continue;
            }

            content.Add(line);
        }

        if (content.Any(line => !string.IsNullOrWhiteSpace(line)))
        {
            yield return new MarkdownSection(sourcePath, heading, string.Join(Environment.NewLine, content).Trim(), order);
        }
    }

    private static string GetRelativeSourcePath(string file)
        => Path.GetRelativePath(AllowedRoot, file).Replace('\\', '/');

    private static bool TryGetSectionHeading(string line, out string heading)
    {
        var trimmed = line.TrimStart();
        var markerLength = 0;
        while (markerLength < trimmed.Length && trimmed[markerLength] == '#')
        {
            markerLength++;
        }

        if (markerLength is < 1 or > 2 ||
            markerLength >= trimmed.Length ||
            trimmed[markerLength] != ' ')
        {
            heading = "";
            return false;
        }

        heading = trimmed[(markerLength + 1)..].Trim();
        return heading.Length > 0;
    }

    private static int ScoreSection(MarkdownSection section, HashSet<string> queryTerms)
    {
        var headingTerms = Tokenize(section.Heading);
        var fileTerms = Tokenize(Path.GetFileNameWithoutExtension(section.FileName));
        var contentTerms = Tokenize(section.Content);
        var score = 0;

        foreach (var term in queryTerms)
        {
            if (headingTerms.Contains(term))
            {
                score += 6;
            }
            else if (fileTerms.Contains(term))
            {
                score += 3;
            }
            else if (contentTerms.Contains(term))
            {
                score++;
            }
        }

        return score;
    }

    private static HashSet<string> Tokenize(string text)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var token = new StringBuilder();

        foreach (var character in text)
        {
            if (char.IsLetterOrDigit(character))
            {
                token.Append(char.ToLowerInvariant(character));
                continue;
            }

            AddToken();
        }

        AddToken();
        return tokens;

        void AddToken()
        {
            if (token.Length == 0) return;
            tokens.Add(token.ToString());
            token.Clear();
        }
    }

    private static string TruncateSection(string content)
    {
        if (content.Length <= MaxSectionCharacters) return content;
        return $"{content[..MaxSectionCharacters].TrimEnd()}{Environment.NewLine}...";
    }

    private sealed record MarkdownSection(string FileName, string Heading, string Content, int Order);

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

    /// <summary>
    /// Returns evidence-gathering tools restricted to the sources selected by the user.
    /// </summary>
    public static IList<AITool> GetSelectedEvidenceTools(string? selectedFile, string? selectedKbQuery)
    {
        var tools = new List<AITool>();

        if (!string.IsNullOrWhiteSpace(selectedFile))
        {
            Func<string, string> readSelectedFile = path =>
                !string.IsNullOrWhiteSpace(path) &&
                string.Equals(path.Trim(), selectedFile.Trim(), StringComparison.OrdinalIgnoreCase)
                    ? ReadFile(path)
                    : $"⛔ ReadFile denied. The selected file is '{selectedFile}'.";

            tools.Add(AIFunctionFactory.Create(
                readSelectedFile,
                name: "ReadFile",
                description: $"Reads the selected sample-data file: {selectedFile}."));
        }

        if (!string.IsNullOrWhiteSpace(selectedKbQuery))
        {
            Func<string, string> searchSelectedQuery = query =>
                !string.IsNullOrWhiteSpace(query) &&
                string.Equals(query.Trim(), selectedKbQuery.Trim(), StringComparison.OrdinalIgnoreCase)
                    ? SearchKb(query)
                    : $"⛔ SearchKb denied. The selected query is '{selectedKbQuery}'.";

            tools.Add(AIFunctionFactory.Create(
                searchSelectedQuery,
                name: "SearchKb",
                description: $"Searches the knowledge base using the selected query: {selectedKbQuery}."));
        }

        return tools;
    }
}
