using System.ClientModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using RPGGameMaster.Infrastructure;

namespace RPGGameMaster;

internal sealed class AgentConfig
{
    public string Endpoint { get; }
    public string ApiKey { get; }
    public string Deployment { get; }

    private AgentConfig(string endpoint, string apiKey, string deployment)
    {
        Endpoint = endpoint; ApiKey = apiKey; Deployment = deployment;
    }

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
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("❌ Missing required environment variables:");
            foreach (var v in missing) Console.Error.WriteLine($"   - {v}");
            Console.ResetColor();
            return null;
        }

        return new AgentConfig(endpoint!, apiKey!, deployment!);
    }

    public IChatClient CreateChatClient()
    {
        var options = new OpenAIClientOptions
        {
            Endpoint = CreateV1Endpoint(Endpoint),
            Transport = AzureOpenAIStreamingCompatibility.Transport,
        };
        var chatClient = new ChatClient(Deployment, new ApiKeyCredential(ApiKey), options);
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

    public AIAgent CreateAgent(string instructions, IList<AITool>? tools = null)
        => CreateChatClient().AsAIAgent(instructions, tools: tools);
}
