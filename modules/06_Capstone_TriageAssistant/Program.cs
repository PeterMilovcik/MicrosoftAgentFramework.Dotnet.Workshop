using System.Text.Json;
using CapstoneTriageAssistant;
using Workshop.Common;

Console.WriteLine("===========================================");
Console.WriteLine(" Module 06 - Capstone: Triage Assistant");
Console.WriteLine("===========================================");
Console.WriteLine();

var config = AgentConfig.Load();
if (config is null) { Environment.Exit(1); return; }

var baseDir = AppContext.BaseDirectory;
var systemPrompt = LoadPromptFile(Path.Combine(baseDir, "assets", "prompts", "system-base.md"));
var safetyPrompt = LoadPromptFile(Path.Combine(baseDir, "assets", "prompts", "system-safety.md"));
var triageRubric = LoadPromptFile(Path.Combine(baseDir, "assets", "prompts", "triage-rubric.md"));
var instructions = $"{systemPrompt}\n\n---\n\n{safetyPrompt}\n\n---\n\n{triageRubric}";

var tools = WorkshopTools.GetTools();
var agent = config.CreateAgent(instructions, tools);

Console.WriteLine("Welcome to the Triage Assistant!");
Console.WriteLine("This tool analyzes failure reports and produces structured Triage Cards.");
Console.WriteLine();

while (true)
{
    // Step 1: Get failure report
    Console.WriteLineColorful("Step 1: Paste your failure report (end with a line containing only 'END'):", ConsoleColor.Cyan);

    var reportLines = new List<string>();
    while (true)
    {
        var line = Console.ReadLine();
        if (line is null || line.Trim().Equals("END", StringComparison.OrdinalIgnoreCase)) break;
        if (line.Trim().Equals("/exit", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Goodbye!");
            return;
        }
        reportLines.Add(line);
    }

    var failureReport = string.Join("\n", reportLines).Trim();
    if (string.IsNullOrWhiteSpace(failureReport))
    {
        Console.WriteLineColorful("⚠️  Empty report. Please try again. (type /exit to quit)", ConsoleColor.Yellow);
        continue;
    }

    // Step 2: Select log file
    Console.WriteLine();
    Console.WriteLineColorful("Step 2: Select a sample log file to analyze:", ConsoleColor.Cyan);
    Console.WriteLine("  [0] None");
    Console.WriteLine("  [1] build-log-01.txt  (AuthService - DB connection failure)");
    Console.WriteLine("  [2] build-log-02.txt  (PaymentGateway - retry + coverage failure)");
    Console.Write("Choice [0-2]: ");

    string? logFileName = null;
    var logChoice = Console.ReadLine()?.Trim();
    logFileName = logChoice switch
    {
        "1" => "build-log-01.txt",
        "2" => "build-log-02.txt",
        _ => null,
    };
    Console.WriteLine(logFileName is null ? "No log file selected." : $"Selected: {logFileName}");

    // Step 3: KB query
    Console.WriteLine();
    Console.WriteLineColorful("Step 3: Enter an optional KB search query (press Enter to skip):", ConsoleColor.Cyan);
    Console.Write("KB query: ");
    var kbQuery = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(kbQuery)) kbQuery = null;

    Console.WriteLine();
    Console.WriteLine("🚀 Starting triage workflow...");
    Console.WriteLine();

    try
    {
        var (humanSummary, card) = await TriageWorkflow.RunAsync(agent, failureReport, logFileName, kbQuery);

        if (card is null)
        {
            Console.WriteLineColorful("⚠️  Could not parse structured Triage Card. Raw response:", ConsoleColor.Yellow);
            Console.WriteLineColorful(humanSummary, ConsoleColor.Yellow);
        }
        else
        {
            // Human-readable summary
            Console.WriteLineColorful("══════════════════════════════════════════", ConsoleColor.Green);
            Console.WriteLineColorful(" HUMAN SUMMARY", ConsoleColor.Green);
            Console.WriteLineColorful("══════════════════════════════════════════", ConsoleColor.Green);
            Console.WriteLine(card.Summary);
            Console.WriteLine();

            // Triage Card JSON
            Console.WriteLineColorful("══════════════════════════════════════════", ConsoleColor.Green);
            Console.WriteLineColorful(" TRIAGE CARD (JSON)", ConsoleColor.Green);
            Console.WriteLineColorful("══════════════════════════════════════════", ConsoleColor.Green);
            var json = JsonSerializer.Serialize(card, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(json);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLineError($"❌ Error: {ex.Message}");
    }

    Console.WriteLine();
    Console.Write("Run another triage? [y/N]: ");
    var again = Console.ReadLine()?.Trim().ToLowerInvariant();
    if (again is not "y" and not "yes") break;
    Console.WriteLine();
}

TokenTracker.PrintSummary();
Console.WriteLine("Goodbye!");

static string LoadPromptFile(string path)
{
    if (!File.Exists(path)) return "";
    return File.ReadAllText(path);
}
