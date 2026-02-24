using HumanInLoopGuards;
using Microsoft.Agents.AI;

Console.WriteLine("===========================================");
Console.WriteLine(" Module 05 - Human-in-the-Loop & Guards");
Console.WriteLine("===========================================");
Console.WriteLine();

var config = AgentConfig.Load();
if (config is null) { Environment.Exit(1); return; }

var baseDir = AppContext.BaseDirectory;
var systemPrompt = LoadPromptFile(Path.Combine(baseDir, "assets", "prompts", "system-base.md"));
var safetyPrompt = LoadPromptFile(Path.Combine(baseDir, "assets", "prompts", "system-safety.md"));
var instructions = $"{systemPrompt}\n\n---\n\n{safetyPrompt}";

// Tool policy info
Console.WriteLine("Tool Policy:");
Console.WriteLine("  GetTime  → Always Allowed (no approval needed)");
Console.WriteLine("  ReadFile → Requires Approval");
Console.WriteLine("  SearchKb → Always Allowed (no approval needed)");
Console.WriteLine();
Console.WriteLine("Workflow:");
Console.WriteLine("  1. Agent produces a PLAN");
Console.WriteLine("  2. YOU approve, revise, or abort the plan");
Console.WriteLine("  3. Evidence gathering (ReadFile requires approval each call)");
Console.WriteLine("  4. Critique → Final structured output");
Console.WriteLine();
Console.WriteLine("Commands: /exit");
Console.WriteLine("Example: Analyze build-log-01.txt and identify root cause");
Console.WriteLine();

var tools = WorkshopTools.GetTools();
var agent = config.CreateAgent(instructions, tools);

while (true)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.Write("Query> ");
    Console.ResetColor();

    var input = Console.ReadLine();
    if (input is null) break;
    input = input.Trim();
    if (string.IsNullOrEmpty(input)) continue;

    if (input.Equals("/exit", StringComparison.OrdinalIgnoreCase)) { Console.WriteLine("Goodbye!"); break; }

    Console.WriteLine();
    try
    {
        await GuardedWorkflow.RunAsync(agent, input);
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
    if (!File.Exists(path)) return "";
    return File.ReadAllText(path);
}
