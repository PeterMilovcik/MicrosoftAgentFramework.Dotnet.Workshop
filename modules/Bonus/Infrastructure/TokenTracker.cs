using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;

namespace RPGGameMaster.Infrastructure;

/// <summary>
/// Tracks cumulative token usage across all LLM calls and provides a summary.
/// Wraps any <see cref="IChatClient"/> via <see cref="Wrap"/>.
/// </summary>
internal static class TokenTracker
{
    private static long _totalInputTokens;
    private static long _totalOutputTokens;
    private static long _llmRequests;
    private static long _activeRequests;
    private static long _peakConcurrentRequests;

    /// <summary>Wraps <paramref name="inner"/> so every request updates the global counters.</summary>
    public static IChatClient Wrap(IChatClient inner) => new TrackingClient(inner);

    /// <summary>Prints a formatted token-usage summary to the console.</summary>
    public static void PrintSummary()
    {
        var total = _totalInputTokens + _totalOutputTokens;
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("📊 Token Usage Summary");
        Console.ResetColor();
        Console.WriteLine($"   Input tokens:  {_totalInputTokens:N0}");
        Console.WriteLine($"   Output tokens: {_totalOutputTokens:N0}");
        Console.WriteLine($"   Total tokens:  {total:N0}");
        Console.WriteLine($"   LLM requests:  {_llmRequests}");
        Console.WriteLine($"   Peak concurrent requests: {_peakConcurrentRequests}");
    }

    private static void Track(UsageDetails? usage)
    {
        if (usage is null) return;
        Interlocked.Add(ref _totalInputTokens, usage.InputTokenCount ?? 0);
        Interlocked.Add(ref _totalOutputTokens, usage.OutputTokenCount ?? 0);
        Interlocked.Increment(ref _llmRequests);
    }

    private static void EnterRequest()
    {
        var active = Interlocked.Increment(ref _activeRequests);
        var observedPeak = Volatile.Read(ref _peakConcurrentRequests);

        while (active > observedPeak)
        {
            var previous = Interlocked.CompareExchange(
                ref _peakConcurrentRequests,
                active,
                observedPeak);

            if (previous == observedPeak)
                break;

            observedPeak = previous;
        }
    }

    private sealed class TrackingClient(IChatClient inner) : DelegatingChatClient(inner)
    {
        public override async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            EnterRequest();
            try
            {
                var result = await base.GetResponseAsync(chatMessages, options, cancellationToken);
                Track(result.Usage);
                return result;
            }
            finally
            {
                Interlocked.Decrement(ref _activeRequests);
            }
        }

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            EnterRequest();
            try
            {
                await foreach (var update in base.GetStreamingResponseAsync(chatMessages, options, cancellationToken))
                {
                    foreach (var content in update.Contents)
                    {
                        if (content is UsageContent usageContent)
                            Track(usageContent.Details);
                    }
                    yield return update;
                }
            }
            finally
            {
                Interlocked.Decrement(ref _activeRequests);
            }
        }
    }
}
