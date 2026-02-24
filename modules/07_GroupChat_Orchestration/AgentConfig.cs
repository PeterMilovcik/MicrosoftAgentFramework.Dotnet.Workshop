using System.ClientModel;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace GroupChatOrchestration;

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
        return azureClient.GetChatClient(Deployment).AsIChatClient();
    }

    public AIAgent CreateAgent(string instructions, IList<AITool>? tools = null)
        => CreateChatClient().AsAIAgent(instructions, tools: tools);

    public AIAgent CreateNamedAgent(string instructions, string name, string description = "", IList<AITool>? tools = null)
        => CreateChatClient().AsAIAgent(instructions, name: name, description: description, tools: tools);
}
