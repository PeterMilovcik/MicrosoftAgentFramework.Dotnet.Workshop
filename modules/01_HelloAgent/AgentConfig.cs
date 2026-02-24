using System.ClientModel;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace HelloAgent;

/// <summary>
/// Reads Azure OpenAI configuration from environment variables and creates
/// a configured <see cref="AIAgent"/> backed by Azure OpenAI Chat Completions.
/// </summary>
internal sealed class AgentConfig
{
    public string Endpoint { get; }
    public string ApiKey { get; }
    public string Deployment { get; }
    public string ApiVersion { get; }

    private AgentConfig(string endpoint, string apiKey, string deployment, string apiVersion)
    {
        Endpoint = endpoint;
        ApiKey = apiKey;
        Deployment = deployment;
        ApiVersion = apiVersion;
    }

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
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("❌ Missing required environment variables:");
            foreach (var v in missing)
                Console.Error.WriteLine($"   - {v}");
            Console.ResetColor();
            Console.Error.WriteLine();
            Console.Error.WriteLine("Set them before running. See scripts/set-env.sh or scripts/set-env.ps1");
            return null;
        }

        return new AgentConfig(endpoint!, apiKey!, deployment!, apiVersion);
    }

    public IChatClient CreateChatClient()
    {
        var azureClient = new AzureOpenAIClient(
            new Uri(Endpoint),
            new ApiKeyCredential(ApiKey));
        return azureClient.GetChatClient(Deployment).AsIChatClient();
    }

    public AIAgent CreateAgent(string instructions)
        => CreateChatClient().AsAIAgent(instructions);
}
