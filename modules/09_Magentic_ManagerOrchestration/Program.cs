using System.Text.Json;
using MagenticOrchestration;

Console.WriteLine("===========================================");
Console.WriteLine(" Module 09 - Magentic Manager Orchestration");
Console.WriteLine("===========================================");
Console.WriteLine();

var config = AgentConfig.Load();
if (config is null) { Environment.Exit(1); return; }

Console.WriteLine("Welcome to the Magentic Triage!");
Console.WriteLine("A manager LLM dynamically selects which agent acts next at each step.");
Console.WriteLine("Team: researcher (tools), diagnostician, critic, scribe");
Console.WriteLine($"Limits: max 8 iterations, max 5 tool calls, stops at confidence >= 0.75");
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

    // Step 4: Human-in-the-loop plan approval
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("━━━ 🔐 Human-in-the-Loop Checkpoint ━━━");
    Console.ResetColor();
    Console.WriteLine("The manager will run autonomously within the configured limits.");
    Console.WriteLine("Options: [approve] to start | [abort] to cancel");
    Console.Write("Decision: ");

    var decision = Console.ReadLine()?.Trim() ?? "";
    if (decision.Equals("abort", StringComparison.OrdinalIgnoreCase))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("🛑 Triage aborted.");
        Console.ResetColor();
        Console.WriteLine();
        Console.Write("Run another triage? [y/N]: ");
        var again2 = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (again2 is not "y" and not "yes") break;
        Console.WriteLine();
        continue;
    }

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("✅ Starting Magentic triage...");
    Console.ResetColor();
    Console.WriteLine();

    try
    {
        var (finalText, card) = await MagenticWorkflow.RunAsync(config, failureReport, logFileName, kbQuery);

        Console.WriteLine();
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
