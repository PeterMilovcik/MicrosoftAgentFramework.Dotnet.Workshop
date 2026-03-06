using System.Text.Json;
using MagenticOrchestration;
using Workshop.Common;

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
    // Quick scenario picker — keeps focus on the orchestration pattern
    Console.WriteLineColorful("Select a scenario (or type /exit to quit):", ConsoleColor.Cyan);
    Console.WriteLine("  [1] AuthService — DB connection pool failure  (build-log-01.txt)");
    Console.WriteLine("  [2] PaymentGateway — retry + coverage failure (build-log-02.txt)");
    Console.WriteLine("  [3] Custom — enter your own failure report");
    Console.Write("Choice [1-3]: ");

    var scenarioChoice = Console.ReadLine()?.Trim();
    if (scenarioChoice?.Equals("/exit", StringComparison.OrdinalIgnoreCase) == true)
    {
        break;
    }

    string failureReport;
    string? logFileName;
    string? kbQuery = null;

    switch (scenarioChoice)
    {
        case "1":
            failureReport = "AuthService integration tests are failing with 503 Service Unavailable. " +
                            "Inner exception: Connection refused on 127.0.0.1:5432. " +
                            "The DB connection pool may not be initializing in the CI environment.";
            logFileName = "build-log-01.txt";
            kbQuery = "database connection";
            break;
        case "2":
            failureReport = "PaymentGateway nightly build failed. RetryPolicyTest.ShouldRetryOnTransientFailure " +
                            "flaked twice then failed — expected retry_count=3 but got 2. " +
                            "Code coverage also dropped below the 80% threshold.";
            logFileName = "build-log-02.txt";
            kbQuery = "retry transient";
            break;
        case "3":
            // Full custom input (same as Module 06)
            Console.WriteLineColorful("Paste your failure report (end with a line containing only 'END'):", ConsoleColor.Cyan);

            var reportLines = new List<string>();
            while (true)
            {
                var line = Console.ReadLine();
                if (line is null || line.Trim().Equals("END", StringComparison.OrdinalIgnoreCase)) break;
                reportLines.Add(line);
            }
            failureReport = string.Join("\n", reportLines).Trim();
            if (string.IsNullOrWhiteSpace(failureReport))
            {
                Console.WriteLineColorful("⚠️  Empty report — skipping.", ConsoleColor.Yellow);
                continue;
            }

            Console.Write("Log file? [1] build-log-01  [2] build-log-02  [0] None: ");
            logFileName = Console.ReadLine()?.Trim() switch
            {
                "1" => "build-log-01.txt",
                "2" => "build-log-02.txt",
                _ => null,
            };

            Console.Write("KB query (Enter to skip): ");
            kbQuery = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(kbQuery)) kbQuery = null;
            break;
        default:
            Console.WriteLine("Invalid choice. Please enter 1, 2, or 3.");
            continue;
    }

    // Human-in-the-loop plan approval
    Console.WriteLine();
    Console.WriteLineColorful("━━━ 🔐 Human-in-the-Loop Checkpoint ━━━", ConsoleColor.Yellow);
    Console.WriteLine("The manager will run autonomously within the configured limits.");
    Console.WriteLine("Options: [approve] to start | [abort] to cancel");
    Console.Write("Decision: ");

    var decision = Console.ReadLine()?.Trim() ?? "";
    if (decision.Equals("abort", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLineColorful("🛑 Triage aborted.", ConsoleColor.Red);
        Console.WriteLine();
        Console.Write("Run another triage? [y/N]: ");
        var again2 = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (again2 is not "y" and not "yes") break;
        Console.WriteLine();
        continue;
    }

    Console.WriteLineColorful("✅ Starting Magentic triage...", ConsoleColor.Green);
    Console.WriteLine();

    try
    {
        var (finalText, card) = await MagenticWorkflow.RunAsync(config, failureReport, logFileName, kbQuery);

        Console.WriteLine();
        if (card is null)
        {
            Console.WriteLineColorful("⚠️  Could not parse structured Triage Card. Raw scribe output:", ConsoleColor.Yellow);
            Console.WriteLineColorful(finalText, ConsoleColor.Yellow);
        }
        else
        {
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

AgentConfig.PrintTokenSummary();
Console.WriteLine("Goodbye!");
