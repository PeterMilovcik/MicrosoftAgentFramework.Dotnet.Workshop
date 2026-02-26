using System.Text;
using Microsoft.Agents.AI;

namespace RPGGameMaster.Workflow;

/// <summary>
/// Runs an <see cref="AIAgent"/> with a prompt and streams the full text response.
/// Each call is guarded by a per-call timeout (<see cref="GameConstants.AgentCallTimeoutSeconds"/>)
/// and automatic retries (<see cref="GameConstants.AgentMaxRetries"/>) with exponential backoff.
/// </summary>
internal static class AgentRunner
{
    /// <summary>
    /// Run an agent with the given prompt and return the full text response.
    /// Applies a per-call timeout and retries on timeout or transient errors.
    /// If the caller's <paramref name="ct"/> is cancelled (e.g. Ctrl+C), the exception propagates immediately.
    /// </summary>
    public static async Task<string> RunAgent(
        AIAgent agent, string prompt, CancellationToken ct, string? fallbackJson = null)
    {
        var maxAttempts = 1 + GameConstants.AgentMaxRetries;
        var backoffMs = GameConstants.AgentRetryBaseDelayMs;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            // Create a linked CTS that cancels on timeout OR when the caller cancels
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(GameConstants.AgentCallTimeoutSeconds));
            var linkedCt = timeoutCts.Token;

            var session = await agent.CreateSessionAsync(linkedCt);
            var sb = new StringBuilder();

            try
            {
                await foreach (var update in agent.RunStreamingAsync(prompt, session).WithCancellation(linkedCt))
                {
                    sb.Append(update.Text);
                }

                // Success — return the streamed content
                var result = sb.ToString().Trim();
                if (!string.IsNullOrEmpty(result))
                    return result;

                // Empty response — treat as transient failure, retry if possible
                if (attempt < maxAttempts)
                {
                    GameConsoleUI.WriteLine($"  ⚡ Empty response — retrying ({attempt}/{GameConstants.AgentMaxRetries})...", ConsoleColor.DarkYellow);
                    await Task.Delay(backoffMs, ct);
                    backoffMs *= 2;
                    continue;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // The caller (user / Ctrl+C) cancelled — propagate immediately
                throw;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Our per-call timeout fired — return partial content if any
                if (sb.Length > 0)
                    return sb.ToString().Trim();

                if (attempt < maxAttempts)
                {
                    GameConsoleUI.WriteLine($"  ⚡ Timeout after {GameConstants.AgentCallTimeoutSeconds}s — retrying ({attempt}/{GameConstants.AgentMaxRetries})...", ConsoleColor.DarkYellow);
                    await Task.Delay(backoffMs, ct);
                    backoffMs *= 2;
                    continue;
                }
            }
            catch (Exception ex)
            {
                // Transient API / content-filter error — return partial content if any
                if (sb.Length > 0)
                    return sb.ToString().Trim();

                if (attempt < maxAttempts)
                {
                    GameConsoleUI.WriteLine($"  ⚡ {ex.Message.Split('\n')[0]} — retrying ({attempt}/{GameConstants.AgentMaxRetries})...", ConsoleColor.DarkYellow);
                    await Task.Delay(backoffMs, ct);
                    backoffMs *= 2;
                    continue;
                }

                // Final attempt failed — log and fall through
                GameConsoleUI.WriteLine($"  ⚡ Agent error: {ex.Message.Split('\n')[0]}", ConsoleColor.DarkRed);
            }
        }

        // All attempts exhausted
        GameConsoleUI.PrintWarning("Agent call failed after all retry attempts.");
        return fallbackJson ?? "[Error: Agent call failed after retries]";
    }
}
