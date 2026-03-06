using Microsoft.Extensions.AI;

namespace Workshop.Common;

/// <summary>
/// Cross-cutting token usage tracking for LLM calls.
/// Wraps any <see cref="IChatClient"/> to transparently capture input/output token counts.
/// </summary>
public static class TokenTracker
{
    private static long _totalInputTokens;
    private static long _totalOutputTokens;
    private static long _llmRequests;

    /// <summary>
    /// Wraps an <see cref="IChatClient"/> with transparent token usage tracking.
    /// </summary>
    public static IChatClient Wrap(IChatClient inner) => new TokenTrackingClient(inner);

    /// <summary>Prints a summary of total token usage across all LLM calls in this session.</summary>
    public static void PrintSummary()
    {
        var total = _totalInputTokens + _totalOutputTokens;
        Console.WriteLine();
        Console.WriteLineColorful("\ud83d\udcca Token Usage Summary", ConsoleColor.Cyan);
        Console.WriteLine($"   Input tokens:  {_totalInputTokens:N0}");
        Console.WriteLine($"   Output tokens: {_totalOutputTokens:N0}");
        Console.WriteLine($"   Total tokens:  {total:N0}");
        Console.WriteLine($"   LLM requests:  {_llmRequests}");
    }

    private static void TrackUsage(UsageDetails? usage)
    {
        if (usage is null) return;
        Interlocked.Add(ref _totalInputTokens, usage.InputTokenCount ?? 0);
        Interlocked.Add(ref _totalOutputTokens, usage.OutputTokenCount ?? 0);
        Interlocked.Increment(ref _llmRequests);
    }

    private sealed class TokenTrackingClient(IChatClient inner) : DelegatingChatClient(inner)
    {
        public override async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var result = await base.GetResponseAsync(chatMessages, options, cancellationToken);
            TrackUsage(result.Usage);
            return result;
        }

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var update in base.GetStreamingResponseAsync(chatMessages, options, cancellationToken))
            {
                foreach (var content in update.Contents)
                {
                    if (content is UsageContent usageContent)
                        TrackUsage(usageContent.Details);
                }
                yield return update;
            }
        }
    }
}
