using System.Text.Json;
using HandoffOrchestration;

Console.WriteLine("===========================================");
Console.WriteLine(" Module 08 - Handoff Orchestration");
Console.WriteLine("===========================================");
Console.WriteLine();

var config = AgentConfig.Load();
if (config is null) { Environment.Exit(1); return; }

Console.WriteLine("Welcome to the Handoff Triage!");
Console.WriteLine("Agents transfer control to specialists based on failure type:");
Console.WriteLine("  frontdesk → [infra-expert | product-expert | test-expert] → scribe");
Console.WriteLine("Only expert agents may call tools.");
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
    Console.WriteLine("  [1] build-log-01.txt  (AuthService - DB connection failure)");
    Console.WriteLine("  [2] build-log-02.txt  (PaymentGateway - retry + coverage failure)");
    Console.Write("Choice [0-2]: ");

    var logChoice = Console.ReadLine()?.Trim();
    string? logFileName = logChoice switch
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
    Console.WriteLine("🚀 Starting handoff triage...");
    Console.WriteLine();

    try
    {
        var (finalText, card) = await HandoffWorkflow.RunAsync(config, failureReport, logFileName, kbQuery);

        if (card is null)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("⚠️  Could not parse structured Triage Card. Raw scribe output:");
            Console.WriteLine(finalText);
            Console.ResetColor();
        }
        else
        {
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
