using System.ClientModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

namespace Workshop.Common;

/// <summary>
/// Reads Azure OpenAI configuration from environment variables and creates
/// a configured <see cref="AIAgent"/> backed by Azure OpenAI Chat Completions.
/// </summary>
public sealed class AgentConfig
{
    public string Endpoint { get; }
    public string Deployment { get; }

    private readonly string _apiKey;

    private AgentConfig(string endpoint, string apiKey, string deployment)
    {
        Endpoint = endpoint;
        _apiKey = apiKey;
        Deployment = deployment;
    }

    /// <summary>
    /// Loads configuration from environment variables.
    /// Prints friendly error messages and returns null if any required variable is missing.
    /// </summary>
    public static AgentConfig? Load()
    {
        var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        var apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
        var deployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT");

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(endpoint)) missing.Add("AZURE_OPENAI_ENDPOINT");
        if (string.IsNullOrWhiteSpace(apiKey)) missing.Add("AZURE_OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(deployment)) missing.Add("AZURE_OPENAI_DEPLOYMENT");

        if (missing.Count > 0)
        {
            Console.WriteLineError("❌ Missing required environment variables:");
            foreach (var v in missing) Console.WriteLineError($"   - {v}");
            return null;
        }

        return new AgentConfig(endpoint!, apiKey!, deployment!);
    }

    /// <summary>
    /// Creates an <see cref="IChatClient"/> connected to Azure OpenAI, wrapped with token tracking.
    /// </summary>
    private IChatClient CreateChatClient()
    {
        var options = new OpenAIClientOptions
        {
            Endpoint = CreateV1Endpoint(Endpoint),
            Transport = AzureOpenAIStreamingCompatibility.Transport,
        };
        var chatClient = new ChatClient(Deployment, new ApiKeyCredential(_apiKey), options);

        return TokenTracker.Wrap(chatClient.AsIChatClient());
    }

    private static Uri CreateV1Endpoint(string endpoint)
    {
        var normalized = endpoint.TrimEnd('/');
        if (!normalized.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase))
        {
            normalized += "/openai/v1";
        }

        return new Uri(normalized + "/");
    }

    /// <summary>
    /// Creates an <see cref="AIAgent"/> with the given system instructions and optional tools.
    /// </summary>
    public AIAgent CreateAgent(string instructions, IList<AITool>? tools = null)
        => CreateChatClient().AsAIAgent(instructions, tools: tools);

    /// <summary>
    /// Creates an <see cref="AIAgent"/> with a name, description, instructions and optional tools.
    /// Used in multi-agent scenarios (group chat, handoff).
    /// </summary>
    public AIAgent CreateNamedAgent(string instructions, string name, string description = "", IList<AITool>? tools = null)
        => CreateChatClient().AsAIAgent(instructions, name: name, description: description, tools: tools);
}
