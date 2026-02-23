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
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"⚠️  Tool Approval Required: {toolName}");
        Console.WriteLine($"   Arguments: {args}");
        Console.ResetColor();
        Console.Write("   Approve this tool call? [y/N]: ");
        var answer = Console.ReadLine()?.Trim().ToLowerInvariant();
        var approved = answer is "y" or "yes";
        Console.ForegroundColor = approved ? ConsoleColor.Green : ConsoleColor.Red;
        Console.WriteLine(approved ? "   ✅ Approved." : "   ❌ Denied.");
        Console.ResetColor();
        return approved;
    }
}
