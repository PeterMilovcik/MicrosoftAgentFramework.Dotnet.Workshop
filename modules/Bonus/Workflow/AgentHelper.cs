using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;

namespace RPGGameMaster.Workflow;

/// <summary>
/// Shared utilities for agent execution, JSON parsing, and console formatting
/// used across GameMasterWorkflow, DialogueWorkflow, and CombatWorkflow.
/// </summary>
internal static class AgentHelper
{
    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Agent execution
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    /// <summary>
    /// Run an agent with the given prompt and return the full text response.
    /// Handles content-filter and transient errors gracefully.
    /// </summary>
    public static async Task<string> RunAgent(
        AIAgent agent, string prompt, CancellationToken ct, string? fallbackJson = null)
    {
        var session = await agent.CreateSessionAsync(ct);
        var sb = new StringBuilder();
        try
        {
            await foreach (var update in agent.RunStreamingAsync(prompt, session).WithCancellation(ct))
            {
                sb.Append(update.Text);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (sb.Length > 0) return sb.ToString().Trim();

            // Log the error so transient API failures are visible
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine($"  ⚡ Agent error: {ex.Message.Split('\n')[0]}");
            Console.ResetColor();

            return fallbackJson ?? $"[Error: {ex.Message.Split('\n')[0]}]";
        }
        return sb.ToString().Trim();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // JSON helpers
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    /// <summary>Extract the first top-level JSON object from text that may contain prose or markdown fences.</summary>
    public static string? ExtractJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        // Strip markdown code fences if present
        var cleaned = text;
        if (cleaned.Contains("```"))
        {
            var fenceStart = cleaned.IndexOf("```", StringComparison.Ordinal);
            var afterFence = cleaned.IndexOf('\n', fenceStart);
            if (afterFence > 0)
            {
                var fenceEnd = cleaned.IndexOf("```", afterFence, StringComparison.Ordinal);
                if (fenceEnd > 0)
                    cleaned = cleaned[(afterFence + 1)..fenceEnd];
            }
        }

        var start = cleaned.IndexOf('{');
        if (start < 0) return null;

        // Find the matching closing brace (handle nested braces)
        var depth = 0;
        for (var i = start; i < cleaned.Length; i++)
        {
            if (cleaned[i] == '{') depth++;
            else if (cleaned[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return cleaned[start..(i + 1)];
            }
        }

        // Fallback: use last closing brace
        var end = cleaned.LastIndexOf('}');
        if (end > start) return cleaned[start..(end + 1)];
        return null;
    }

    /// <summary>Parse a JSON string into a typed object, returning null on failure.</summary>
    public static T? ParseJson<T>(string text) where T : class
    {
        var json = ExtractJson(text);
        if (json is null) return null;
        try { return JsonSerializer.Deserialize<T>(json, JsonOpts); }
        catch { return null; }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Console formatting helpers
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public static void PrintStatus(string msg)
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"\n🌍 {msg}");
        Console.ResetColor();
    }

    public static void PrintWarning(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  ⚠️ {msg}");
        Console.ResetColor();
    }

    public static void PrintSubAgentWork(string agent, string task)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  ⚙ [{agent}] {task}");
        Console.ResetColor();
    }
}

/// <summary>A numbered dialogue option for NPC conversation.</summary>
internal sealed class DialogueOption
{
    public int Number { get; set; }
    public string Text { get; set; } = "";
}
