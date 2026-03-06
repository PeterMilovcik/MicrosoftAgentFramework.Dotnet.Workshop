using System.ClientModel;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Workshop.Common;

/// <summary>
/// Reads Azure OpenAI configuration from environment variables and creates
/// a configured <see cref="AIAgent"/> backed by Azure OpenAI Chat Completions.
/// </summary>
public sealed class AgentConfig
{
    public string Endpoint { get; }
    public string Deployment { get; }
    public string ApiVersion { get; }

    private readonly string _apiKey;

    private AgentConfig(string endpoint, string apiKey, string deployment, string apiVersion)
    {
        Endpoint = endpoint;
        _apiKey = apiKey;
        Deployment = deployment;
        ApiVersion = apiVersion;
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
        var apiVersion = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_VERSION")
                         ?? "2025-01-01-preview";

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

        return new AgentConfig(endpoint!, apiKey!, deployment!, apiVersion);
    }

    /// <summary>
    /// Creates an <see cref="IChatClient"/> connected to Azure OpenAI, wrapped with token tracking.
    /// </summary>
    private IChatClient CreateChatClient()
    {
        var azureClient = new AzureOpenAIClient(
            new Uri(Endpoint),
            new ApiKeyCredential(_apiKey));

        return TokenTracker.Wrap(azureClient.GetChatClient(Deployment).AsIChatClient());
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
