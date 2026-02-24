using System.Text.Json;
using WorkflowsMultiStep;
using Microsoft.Agents.AI;

Console.WriteLine("===========================================");
Console.WriteLine(" Module 04 - Workflows: Multi-Step Analysis");
Console.WriteLine("===========================================");
Console.WriteLine();

var config = AgentConfig.Load();
if (config is null) { Environment.Exit(1); return; }

var baseDir = AppContext.BaseDirectory;
var systemPrompt = LoadPromptFile(Path.Combine(baseDir, "assets", "prompts", "system-base.md"));
var safetyPrompt = LoadPromptFile(Path.Combine(baseDir, "assets", "prompts", "system-safety.md"));
var instructions = $"{systemPrompt}\n\n---\n\n{safetyPrompt}";

// Create agent with tools (tools used in workflow steps)
var tools = WorkshopTools.GetTools();
var agent = config.CreateAgent(instructions, tools);

Console.WriteLine("This module runs a 4-step analysis workflow:");
Console.WriteLine("  [PLAN] → [EVIDENCE] → [CRITIQUE] → [FINAL]");
Console.WriteLine();
Console.WriteLine("Example prompts to try:");
Console.WriteLine("  • Analyze build-log-01.txt and identify the likely root cause");
Console.WriteLine("  • Analyze build-log-02.txt for test failures and coverage issues");
Console.WriteLine("  • What does the KB say about handling flaky tests?");
Console.WriteLine();

while (true)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.Write("Query> ");
    Console.ResetColor();

    var input = Console.ReadLine();
    if (input is null) break;
    input = input.Trim();
    if (string.IsNullOrEmpty(input)) continue;

    if (input.Equals("/exit", StringComparison.OrdinalIgnoreCase)) { AgentConfig.PrintTokenSummary(); Console.WriteLine("Goodbye!"); break; }

    Console.WriteLine();
    Console.WriteLine("🚀 Starting workflow...");
    Console.WriteLine();

    try
    {
        var output = await AnalysisWorkflow.RunAsync(agent, input);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("══════════════════════════════════════════");
        Console.WriteLine(" STRUCTURED OUTPUT (JSON)");
        Console.WriteLine("══════════════════════════════════════════");
        Console.ResetColor();

        var json = JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(json);
        Console.WriteLine();
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"❌ Workflow error: {ex.Message}");
        Console.ResetColor();
    }
}

static string LoadPromptFile(string path)
{
    if (!File.Exists(path)) return "";
    return File.ReadAllText(path);
}
