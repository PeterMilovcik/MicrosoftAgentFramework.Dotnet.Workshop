using ToolsFunctionCalling;
using Microsoft.Agents.AI;

Console.WriteLine("===========================================");
Console.WriteLine(" Module 02 – Tools and Function Calling");
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
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.Write("You> ");
    Console.ResetColor();

    var input = Console.ReadLine();
    if (input is null) break;
    input = input.Trim();
    if (string.IsNullOrEmpty(input)) continue;

    if (input.Equals("/exit", StringComparison.OrdinalIgnoreCase)) { Console.WriteLine("Goodbye!"); break; }

    if (input.Equals("/help", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Commands: /reset | /exit");
        Console.WriteLine("Tools available: GetTime, ReadFile(<path>), SearchKb(<query>)");
        continue;
    }

    if (input.Equals("/reset", StringComparison.OrdinalIgnoreCase))
    {
        session = await agent.CreateSessionAsync();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("🔄 Conversation history cleared.");
        Console.ResetColor();
        continue;
    }

    try
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("Agent> ");
        Console.ResetColor();

        await foreach (var update in agent.RunStreamingAsync(input, session))
        {
            Console.Write(update.Text);
        }
        Console.WriteLine();
        Console.WriteLine();
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"❌ Error: {ex.Message}");
        Console.ResetColor();
    }
}

static string LoadPromptFile(string path)
{
    if (!File.Exists(path))
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"⚠️  Prompt file not found: {path}");
        Console.ResetColor();
        return "";
    }
    return File.ReadAllText(path);
}
