using System.ClientModel;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace ConnectivityCheck;

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
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("❌ Missing required environment variables:");
            foreach (var v in missing)
                Console.Error.WriteLine($"   - {v}");
            Console.ResetColor();
            Console.Error.WriteLine();
            Console.Error.WriteLine("Set them before running:");
            Console.Error.WriteLine("  export AZURE_OPENAI_ENDPOINT='https://<resource>.openai.azure.com/'");
            Console.Error.WriteLine("  export AZURE_OPENAI_API_KEY='<your-key>'");
            Console.Error.WriteLine("  export AZURE_OPENAI_DEPLOYMENT='<deployment-name>'");
            Console.Error.WriteLine("  export AZURE_OPENAI_API_VERSION='2025-01-01-preview'  # optional");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Or run: ./scripts/set-env.sh  (Linux/macOS)");
            Console.Error.WriteLine("        ./scripts/set-env.ps1 (Windows PowerShell)");
            return null;
        }

        return new AgentConfig(endpoint!, apiKey!, deployment!, apiVersion);
    }

    /// <summary>
    /// Creates an <see cref="IChatClient"/> connected to Azure OpenAI.
    /// </summary>
    public IChatClient CreateChatClient()
    {
        var azureClient = new AzureOpenAIClient(
            new Uri(Endpoint),
            new ApiKeyCredential(ApiKey));

        return azureClient.GetChatClient(Deployment).AsIChatClient();
    }

    /// <summary>
    /// Creates an <see cref="AIAgent"/> with the given system instructions.
    /// </summary>
    public AIAgent CreateAgent(string instructions)
    {
        return CreateChatClient().AsAIAgent(instructions);
    }
}
