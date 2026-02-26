using System.Text;
using Microsoft.Agents.AI;

namespace RPGGameMaster.Workflow;

/// <summary>
/// Runs an <see cref="AIAgent"/> with a prompt and streams the full text response.
/// Handles content-filter and transient API errors gracefully.
/// </summary>
internal static class AgentRunner
{
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
            GameConsoleUI.WriteLine($"  ⚡ Agent error: {ex.Message.Split('\n')[0]}", ConsoleColor.DarkRed);

            return fallbackJson ?? $"[Error: {ex.Message.Split('\n')[0]}]";
        }
        return sb.ToString().Trim();
    }
}
