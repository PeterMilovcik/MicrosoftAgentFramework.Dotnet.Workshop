using HelloAgent;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

Console.WriteLine("===========================================");
Console.WriteLine(" Module 01 – Hello Agent (REPL Loop)");
Console.WriteLine("===========================================");
Console.WriteLine();

// Load configuration
var config = AgentConfig.Load();
if (config is null) { Environment.Exit(1); return; }

// Load system prompts from files (participants can edit these without touching code)
var baseDir = AppContext.BaseDirectory;
var promptsDir = Path.GetFullPath(Path.Combine(baseDir, "assets", "prompts"));
var systemBaseFile = Path.Combine(promptsDir, "system-base.md");
var systemSafetyFile = Path.Combine(promptsDir, "system-safety.md");

var systemPrompt = LoadPromptFile(systemBaseFile, "# You are a helpful AI assistant.");
var safetyPrompt = LoadPromptFile(systemSafetyFile, "");

// Combine prompts
var combinedInstructions = string.IsNullOrWhiteSpace(safetyPrompt)
    ? systemPrompt
    : $"{systemPrompt}\n\n---\n\n{safetyPrompt}";

Console.WriteLine("✅ Agent ready. System prompt loaded.");
Console.WriteLine();
PrintHelp();
Console.WriteLine();

// Create agent and session
var agent = config.CreateAgent(combinedInstructions);
var session = await agent.CreateSessionAsync();

// REPL loop
while (true)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.Write("You> ");
    Console.ResetColor();

    var input = Console.ReadLine();
    if (input is null) break;
    input = input.Trim();
    if (string.IsNullOrEmpty(input)) continue;

    // Handle commands
    if (input.Equals("/exit", StringComparison.OrdinalIgnoreCase) ||
        input.Equals("/quit", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Goodbye!");
        break;
    }

    if (input.Equals("/help", StringComparison.OrdinalIgnoreCase))
    {
        PrintHelp();
        continue;
    }

    if (input.Equals("/reset", StringComparison.OrdinalIgnoreCase))
    {
        // Create a fresh session to clear history
        session = await agent.CreateSessionAsync();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("🔄 Conversation history cleared.");
        Console.ResetColor();
        continue;
    }

    if (input.Equals("/sys", StringComparison.OrdinalIgnoreCase))
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("--- Active System Prompt ---");
        Console.WriteLine(combinedInstructions);
        Console.WriteLine("----------------------------");
        Console.ResetColor();
        continue;
    }

    // Send message to agent and print response
    try
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("Agent> ");
        Console.ResetColor();

        // Stream the response for a better UX
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

static void PrintHelp()
{
    Console.WriteLine("Commands:");
    Console.WriteLine("  /help   Show this help message");
    Console.WriteLine("  /reset  Clear conversation history");
    Console.WriteLine("  /sys    Print active system prompt");
    Console.WriteLine("  /exit   Exit the application");
    Console.WriteLine();
    Console.WriteLine("Type any message to chat with the agent.");
}

static string LoadPromptFile(string path, string fallback)
{
    if (!File.Exists(path))
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"⚠️  Prompt file not found: {path} – using default.");
        Console.ResetColor();
        return fallback;
    }
    return File.ReadAllText(path);
}
