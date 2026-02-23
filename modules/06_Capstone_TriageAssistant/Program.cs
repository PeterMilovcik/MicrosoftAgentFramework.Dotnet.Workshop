using System.Text.Json;
using CapstoneTriageAssistant;

Console.WriteLine("===========================================");
Console.WriteLine(" Module 06 – Capstone: Triage Assistant");
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
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("Step 1: Paste your failure report (end with a line containing only 'END'):");
    Console.ResetColor();

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
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("⚠️  Empty report. Please try again. (type /exit to quit)");
        Console.ResetColor();
        continue;
    }

    // Step 2: Select log file
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("Step 2: Select a sample log file to analyze:");
    Console.ResetColor();
    Console.WriteLine("  [0] None");
    Console.WriteLine("  [1] build-log-01.txt  (AuthService – DB connection failure)");
    Console.WriteLine("  [2] build-log-02.txt  (PaymentGateway – retry + coverage failure)");
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
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("Step 3: Enter an optional KB search query (press Enter to skip):");
    Console.ResetColor();
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
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("⚠️  Could not parse structured Triage Card. Raw response:");
            Console.WriteLine(humanSummary);
            Console.ResetColor();
        }
        else
        {
            // Human-readable summary
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("══════════════════════════════════════════");
            Console.WriteLine(" HUMAN SUMMARY");
            Console.WriteLine("══════════════════════════════════════════");
            Console.ResetColor();
            Console.WriteLine(card.Summary);
            Console.WriteLine();

            // Triage Card JSON
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("══════════════════════════════════════════");
            Console.WriteLine(" TRIAGE CARD (JSON)");
            Console.WriteLine("══════════════════════════════════════════");
            Console.ResetColor();
            var json = JsonSerializer.Serialize(card, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(json);
        }
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"❌ Error: {ex.Message}");
        Console.ResetColor();
    }

    Console.WriteLine();
    Console.Write("Run another triage? [y/N]: ");
    var again = Console.ReadLine()?.Trim().ToLowerInvariant();
    if (again is not "y" and not "yes") break;
    Console.WriteLine();
}

Console.WriteLine("Goodbye!");

static string LoadPromptFile(string path)
{
    if (!File.Exists(path)) return "";
    return File.ReadAllText(path);
}
