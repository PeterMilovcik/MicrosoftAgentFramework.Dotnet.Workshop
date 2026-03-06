using Workshop.Common;

namespace HumanInLoopGuards;

/// <summary>
/// Defines the approval policy for each tool.
/// </summary>
internal enum ToolApprovalPolicy
{
    /// <summary>Tool is always allowed without user approval.</summary>
    AlwaysAllow,
    /// <summary>Tool requires explicit user approval before execution.</summary>
    RequireApproval,
    /// <summary>Tool is never allowed.</summary>
    Deny,
}

/// <summary>
/// Enforces a per-tool approval policy by intercepting tool calls
/// and prompting the user for approval when required.
/// </summary>
internal static class ToolPolicy
{
    // Define policy per tool name
    private static readonly Dictionary<string, ToolApprovalPolicy> Policies = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GetTime"] = ToolApprovalPolicy.AlwaysAllow,
        ["ReadFile"] = ToolApprovalPolicy.RequireApproval,
        ["SearchKb"] = ToolApprovalPolicy.AlwaysAllow,
    };

    /// <summary>
    /// Returns the approval policy for a tool by name.
    /// Unknown tools default to RequireApproval (safe default).
    /// </summary>
    public static ToolApprovalPolicy GetPolicy(string toolName)
        => Policies.TryGetValue(toolName, out var policy) ? policy : ToolApprovalPolicy.RequireApproval;

    /// <summary>
    /// Prompts the user to approve or deny a tool call.
    /// Returns true if approved, false if denied.
    /// </summary>
    public static bool RequestApproval(string toolName, string args)
    {
        Console.WriteLine();
        Console.WriteLineColorful($"⚠️  Tool Approval Required: {toolName}", ConsoleColor.Yellow);
        Console.WriteLineColorful($"   Arguments: {args}", ConsoleColor.Yellow);
        Console.Write("   Approve this tool call? [y/N]: ");
        var answer = Console.ReadLine()?.Trim().ToLowerInvariant();
        var approved = answer is "y" or "yes";
        Console.WriteLineColorful(approved ? "   ✅ Approved." : "   ❌ Denied.", approved ? ConsoleColor.Green : ConsoleColor.Red);
        return approved;
    }
}
