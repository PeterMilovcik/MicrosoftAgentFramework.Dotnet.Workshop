using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace HumanInLoopGuards;

internal static class WorkshopTools
{
    private static readonly string AllowedRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "assets", "sample-data"));

    private static readonly string[] AllowedExtensions = [".txt", ".md"];

    /// <summary>
    /// Checks the tool policy and requests user approval when required.
    /// Returns null if allowed, or an error message if denied.
    /// </summary>
    private static string? EnforcePolicy(string toolName, string args)
    {
        var policy = ToolPolicy.GetPolicy(toolName);
        return policy switch
        {
            ToolApprovalPolicy.Deny => $"⛔ Tool '{toolName}' is denied by policy.",
            ToolApprovalPolicy.RequireApproval when !ToolPolicy.RequestApproval(toolName, args)
                => $"⛔ Tool '{toolName}' was denied by the user.",
            _ => null, // AlwaysAllow or user approved
        };
    }

    [Description("Returns the current UTC date and time in ISO-8601 format.")]
    public static string GetTime()
    {
        var denied = EnforcePolicy("GetTime", "");
        if (denied is not null) return denied;
        return DateTime.UtcNow.ToString("O");
    }

    [Description("Reads a file from sample-data. Path is relative (e.g. 'build-log-01.txt').")]
    public static string ReadFile(
        [Description("Relative path within sample-data.")] string path)
    {
        // Validate path before prompting so invalid paths are blocked without an approval dialog
        if (path.Contains("..") || Path.IsPathRooted(path))
            return "⛔ Access denied: no path traversal allowed.";
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return $"⛔ Only {string.Join(", ", AllowedExtensions)} files allowed.";
        var fullPath = Path.GetFullPath(Path.Combine(AllowedRoot, path));
        if (!fullPath.StartsWith(AllowedRoot, StringComparison.OrdinalIgnoreCase))
            return "⛔ Access denied: outside allowed directory.";

        // Enforce tool policy (may prompt the user for approval)
        var denied = EnforcePolicy("ReadFile", path);
        if (denied is not null) return denied;

        return global::Workshop.Common.WorkshopTools.ReadFile(path);
    }

    [Description("Searches kb/*.md files. Returns a full filename match or up to 3 ranked sections.")]
    public static string SearchKb(
        [Description("Keywords to search for.")] string query)
    {
        var denied = EnforcePolicy("SearchKb", query);
        if (denied is not null) return denied;

        return global::Workshop.Common.WorkshopTools.SearchKb(query);
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
