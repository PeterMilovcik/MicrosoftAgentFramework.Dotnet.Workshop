using StateSessionsPersistence;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

Console.WriteLine("===========================================");
Console.WriteLine(" Module 03 - State, Sessions & Persistence");
Console.WriteLine("===========================================");
Console.WriteLine();

var config = AgentConfig.Load();
if (config is null) { Environment.Exit(1); return; }

var baseDir = AppContext.BaseDirectory;
var systemPrompt = LoadPromptFile(Path.Combine(baseDir, "assets", "prompts", "system-base.md"));
var agent = config.CreateAgent(systemPrompt);

SessionStore.EnsureDirectory();

// Current in-memory state
WorkshopSession? currentWorkshopSession = null;
AgentSession? agentSession = null;
var inMemoryMessages = new List<ChatMessage>();

Console.WriteLine("Commands:");
Console.WriteLine("  /new              Create a new session");
Console.WriteLine("  /list             List saved sessions");
Console.WriteLine("  /load <id>        Load a session by ID (or ID prefix)");
Console.WriteLine("  /delete <id>      Delete a session");
Console.WriteLine("  /status           Show current session info");
Console.WriteLine("  /exit             Exit");
Console.WriteLine();
Console.WriteLine("Start by typing /new to create your first session.");
Console.WriteLine();

while (true)
{
    var prompt = currentWorkshopSession is not null
        ? $"[{currentWorkshopSession.Label}] You> "
        : "You> ";

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.Write(prompt);
    Console.ResetColor();

    var input = Console.ReadLine();
    if (input is null) break;
    input = input.Trim();
    if (string.IsNullOrEmpty(input)) continue;

    // ---- Commands ----

    if (input.Equals("/exit", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Goodbye!");
        break;
    }

    if (input.StartsWith("/new", StringComparison.OrdinalIgnoreCase))
    {
        Console.Write("Session label (optional, press Enter to skip): ");
        var label = Console.ReadLine()?.Trim() ?? "";

        // Save current session if active
        if (currentWorkshopSession is not null && inMemoryMessages.Count > 0)
        {
            currentWorkshopSession.Messages = SessionStore.ToSerializable(inMemoryMessages);
            SessionStore.Save(currentWorkshopSession);
        }

        currentWorkshopSession = new WorkshopSession { Label = label };
        agentSession = await agent.CreateSessionAsync();
        inMemoryMessages = [];

        SessionStore.Save(currentWorkshopSession);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✅ New session created: {currentWorkshopSession.SessionId} (\"{label}\")");
        Console.ResetColor();
        continue;
    }

    if (input.Equals("/list", StringComparison.OrdinalIgnoreCase))
    {
        var sessions = SessionStore.ListAll();
        if (sessions.Count == 0)
        {
            Console.WriteLine("No saved sessions. Type /new to create one.");
        }
        else
        {
            Console.WriteLine($"Saved sessions ({sessions.Count}):");
            foreach (var s in sessions)
            {
                var active = currentWorkshopSession?.SessionId == s.SessionId ? " ◄ active" : "";
                Console.WriteLine($"  {s.SessionId} | {s.CreatedAt:u} | \"{s.Label}\" | {s.Messages.Count} messages{active}");
            }
        }
        continue;
    }

    if (input.StartsWith("/load ", StringComparison.OrdinalIgnoreCase))
    {
        var idArg = input[6..].Trim();
        var sessions = SessionStore.ListAll();
        var target = sessions.FirstOrDefault(s =>
            s.SessionId.ToString().StartsWith(idArg, StringComparison.OrdinalIgnoreCase));

        if (target is null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ No session found matching: {idArg}");
            Console.ResetColor();
            continue;
        }

        // Save current session first
        if (currentWorkshopSession is not null && inMemoryMessages.Count > 0)
        {
            currentWorkshopSession.Messages = SessionStore.ToSerializable(inMemoryMessages);
            SessionStore.Save(currentWorkshopSession);
        }

        currentWorkshopSession = target;
        inMemoryMessages = SessionStore.FromSerializable(target.Messages);

        // Recreate agent session and replay history
        agentSession = await agent.CreateSessionAsync();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✅ Loaded session: {target.SessionId} (\"{target.Label}\") - {inMemoryMessages.Count} messages in history");
        Console.ResetColor();
        continue;
    }

    if (input.StartsWith("/delete ", StringComparison.OrdinalIgnoreCase))
    {
        var idArg = input[8..].Trim();
        var sessions = SessionStore.ListAll();
        var target = sessions.FirstOrDefault(s =>
            s.SessionId.ToString().StartsWith(idArg, StringComparison.OrdinalIgnoreCase));

        if (target is null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ No session found matching: {idArg}");
            Console.ResetColor();
            continue;
        }

        SessionStore.Delete(target.SessionId);
        if (currentWorkshopSession?.SessionId == target.SessionId)
        {
            currentWorkshopSession = null;
            agentSession = null;
            inMemoryMessages = [];
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"🗑️  Deleted session: {target.SessionId}");
        Console.ResetColor();
        continue;
    }

    if (input.Equals("/status", StringComparison.OrdinalIgnoreCase))
    {
        if (currentWorkshopSession is null)
        {
            Console.WriteLine("No active session. Type /new to create one.");
        }
        else
        {
            Console.WriteLine($"Active session: {currentWorkshopSession.SessionId}");
            Console.WriteLine($"  Label:    \"{currentWorkshopSession.Label}\"");
            Console.WriteLine($"  Created:  {currentWorkshopSession.CreatedAt:u}");
            Console.WriteLine($"  Messages: {inMemoryMessages.Count}");
        }
        continue;
    }

    // ---- Chat ----

    if (currentWorkshopSession is null || agentSession is null)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("⚠️  No active session. Type /new to create one first.");
        Console.ResetColor();
        continue;
    }

    try
    {
        // Include the full in-memory history in the run
        var allMessages = new List<ChatMessage>(inMemoryMessages)
        {
            new(ChatRole.User, input)
        };

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("Agent> ");
        Console.ResetColor();

        var responseText = new System.Text.StringBuilder();
        await foreach (var update in agent.RunStreamingAsync(allMessages, agentSession))
        {
            Console.Write(update.Text);
            responseText.Append(update.Text);
        }
        Console.WriteLine();
        Console.WriteLine();

        // Store messages in local history
        inMemoryMessages.Add(new ChatMessage(ChatRole.User, input));
        inMemoryMessages.Add(new ChatMessage(ChatRole.Assistant, responseText.ToString()));

        // Auto-save session after each turn
        currentWorkshopSession.Messages = SessionStore.ToSerializable(inMemoryMessages);
        SessionStore.Save(currentWorkshopSession);
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
    if (!File.Exists(path)) return "You are a helpful AI assistant.";
    return File.ReadAllText(path);
}
