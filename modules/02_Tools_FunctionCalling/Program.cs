using Microsoft.Agents.AI;
using Workshop.Common;

Console.WriteLine("===========================================");
Console.WriteLine(" Module 02 - Tools and Function Calling");
Console.WriteLine("===========================================");
Console.WriteLine();

var config = AgentConfig.Load();
if (config is null) { Environment.Exit(1); return; }

// Load system prompts
var baseDir = AppContext.BaseDirectory;
var promptsDir = Path.GetFullPath(Path.Combine(baseDir, "assets", "prompts"));
var systemPrompt = LoadPromptFile(Path.Combine(promptsDir, "system-base.md"));
var safetyPrompt = LoadPromptFile(Path.Combine(promptsDir, "system-safety.md"));
var instructions = $"{systemPrompt}\n\n---\n\n{safetyPrompt}";

// Create agent with tools registered
var tools = WorkshopTools.GetTools();
var agent = config.CreateAgent(instructions, tools);
var session = await agent.CreateSessionAsync();

Console.WriteLine("✅ Agent ready with tools: GetTime, ReadFile, SearchKb");
Console.WriteLine();
Console.WriteLine("Try asking:");
Console.WriteLine("  • \"What time is it?\"");
Console.WriteLine("  • \"What are our guidelines for flaky tests?\"");
Console.WriteLine("  • \"Summarize the release notes and list action items.\"");
Console.WriteLine("  • \"Show me the contents of build-log-01.txt\"");
Console.WriteLine();
Console.WriteLine("Commands: /help | /reset | /exit");
Console.WriteLine();

while (true)
{
    Console.WriteColorful("You> ", ConsoleColor.Cyan);

    var input = Console.ReadLine();
    if (input is null) break;
    input = input.Trim();
    if (string.IsNullOrEmpty(input)) continue;

    if (input.Equals("/exit", StringComparison.OrdinalIgnoreCase)) 
    { 
        TokenTracker.PrintSummary(); 
        Console.WriteLine("Goodbye!"); 
        break; 
    }

    if (input.Equals("/help", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Commands: /reset | /exit");
        Console.WriteLine("Tools available: GetTime, ReadFile(<path>), SearchKb(<query>)");
        continue;
    }

    if (input.Equals("/reset", StringComparison.OrdinalIgnoreCase))
    {
        session = await agent.CreateSessionAsync();
        Console.WriteLineColorful("🔄 Conversation history cleared.", ConsoleColor.Yellow);
        continue;
    }

    try
    {
        Console.WriteColorful("Agent> ", ConsoleColor.DarkGray);

        await foreach (var update in agent.RunStreamingAsync(input, session))
        {
            Console.Write(update.Text);
        }
        Console.WriteLine();
        Console.WriteLine();
    }
    catch (Exception ex)
    {
        Console.WriteLineError($"❌ Error: {ex.Message}");
    }
}

static string LoadPromptFile(string path)
{
    if (!File.Exists(path))
    {
        Console.WriteLineColorful($"⚠️  Prompt file not found: {path}", ConsoleColor.Yellow);
        return "";
    }
    return File.ReadAllText(path);
}
