using System.ClientModel;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace RPGGameMaster;

internal sealed class AgentConfig
{
    public string Endpoint { get; }
    public string ApiKey { get; }
    public string Deployment { get; }
    public string ApiVersion { get; }

    private AgentConfig(string endpoint, string apiKey, string deployment, string apiVersion)
    {
        Endpoint = endpoint; ApiKey = apiKey; Deployment = deployment; ApiVersion = apiVersion;
    }

    public static AgentConfig? Load()
    {
        var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        var apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
        var deployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT");
        var apiVersion = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_VERSION") ?? "2025-01-01-preview";

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(endpoint)) missing.Add("AZURE_OPENAI_ENDPOINT");
        if (string.IsNullOrWhiteSpace(apiKey)) missing.Add("AZURE_OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(deployment)) missing.Add("AZURE_OPENAI_DEPLOYMENT");

        if (missing.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("❌ Missing required environment variables:");
            foreach (var v in missing) Console.Error.WriteLine($"   - {v}");
            Console.ResetColor();
            return null;
        }

        return new AgentConfig(endpoint!, apiKey!, deployment!, apiVersion);
    }

    public IChatClient CreateChatClient()
    {
        var azureClient = new AzureOpenAIClient(new Uri(Endpoint), new ApiKeyCredential(ApiKey));
        return new TokenTrackingClient(azureClient.GetChatClient(Deployment).AsIChatClient());
    }

    public AIAgent CreateAgent(string instructions, IList<AITool>? tools = null)
        => CreateChatClient().AsAIAgent(instructions, tools: tools);

    // ---- Token Usage Tracking ----

    private static long _totalInputTokens;
    private static long _totalOutputTokens;
    private static long _llmRequests;

    private static void TrackUsage(UsageDetails? usage)
    {
        if (usage is null) return;
        Interlocked.Add(ref _totalInputTokens, usage.InputTokenCount ?? 0);
        Interlocked.Add(ref _totalOutputTokens, usage.OutputTokenCount ?? 0);
        Interlocked.Increment(ref _llmRequests);
    }

    public static void PrintTokenSummary()
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
