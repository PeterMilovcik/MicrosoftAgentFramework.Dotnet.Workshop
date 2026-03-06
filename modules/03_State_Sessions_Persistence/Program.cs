using StateSessionsPersistence;
using Microsoft.Agents.AI;
using Workshop.Common;

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
int turnCount = 0;

PrintCommands();

while (true)
{
    var prompt = currentWorkshopSession is not null
        ? $"[{currentWorkshopSession.Label}] You> "
        : "You> ";

    Console.WriteColorful(prompt, ConsoleColor.Cyan);

    var input = Console.ReadLine();
    if (input is null) break;
    input = input.Trim();
    if (string.IsNullOrEmpty(input)) continue;

    // ---- Commands ----

    if (input.Equals("/exit", StringComparison.OrdinalIgnoreCase))
    {
        await SaveActiveSessionAsync();
        AgentConfig.PrintTokenSummary();
        Console.WriteLine("Goodbye!");
        break;
    }

    if (input.StartsWith("/new", StringComparison.OrdinalIgnoreCase))
    {
        await HandleNewSessionAsync();
        continue;
    }

    if (input.Equals("/list", StringComparison.OrdinalIgnoreCase))
    {
        HandleListSessions();
        continue;
    }

    if (input.StartsWith("/load ", StringComparison.OrdinalIgnoreCase))
    {
        await HandleLoadSessionAsync(input[6..].Trim());
        continue;
    }

    if (input.StartsWith("/delete ", StringComparison.OrdinalIgnoreCase))
    {
        HandleDeleteSession(input[8..].Trim());
        continue;
    }

    if (input.Equals("/status", StringComparison.OrdinalIgnoreCase))
    {
        HandleStatus();
        continue;
    }

    // ---- Chat ----

    await HandleChatAsync(input);
}

// ---- Helper Methods ----

void PrintCommands()
{
    Console.WriteLine("Commands:");
    Console.WriteLine("  /new              Create a new session");
    Console.WriteLine("  /list             List saved sessions");
    Console.WriteLine("  /load <id>        Load a session by ID (or ID prefix)");
    Console.WriteLine("  /delete <id>      Delete a session by ID (or ID prefix)");
    Console.WriteLine("  /status           Show current session info");
    Console.WriteLine("  /exit             Exit");
    Console.WriteLine();
    Console.WriteLine("Start by typing /new to create your first session.");
    Console.WriteLine();
}

async Task HandleNewSessionAsync()
{
    Console.Write("Session label (optional, press Enter to skip): ");
    var label = Console.ReadLine()?.Trim() ?? "";

    await SaveActiveSessionAsync();

    currentWorkshopSession = new WorkshopSession { Label = label };
    agentSession = await agent.CreateSessionAsync();
    turnCount = 0;

    SessionStore.Save(currentWorkshopSession);
    Console.WriteLineColorful($"✅ New session created: {currentWorkshopSession.SessionId} (\"{label}\")", ConsoleColor.Green);
}

void HandleListSessions()
{
    var sessions = SessionStore.ListAll();
    if (sessions.Count == 0)
    {
        Console.WriteLine("No saved sessions. Type /new to create one.");
        return;
    }

    Console.WriteLine($"Saved sessions ({sessions.Count}):");
    foreach (var s in sessions)
    {
        var active = currentWorkshopSession?.SessionId == s.SessionId ? " ◄ active" : "";
        Console.WriteLine($"  {s.SessionId} | {s.CreatedAt:u} | \"{s.Label}\" | {s.TurnCount} turns{active}");
    }
}

async Task HandleLoadSessionAsync(string sessionIdPrefix)
{
    var target = FindSessionByPrefix(sessionIdPrefix);
    if (target is null) return;

    await SaveActiveSessionAsync();

    var loaded = SessionStore.Load(target.SessionId);
    if (loaded?.AgentSessionState is null)
    {
        Console.WriteLineColorful($"⚠️  Session {target.SessionId} has no saved conversation state. Starting fresh.", ConsoleColor.Yellow);
        currentWorkshopSession = loaded ?? target;
        agentSession = await agent.CreateSessionAsync();
        turnCount = 0;
        return;
    }

    currentWorkshopSession = loaded;
    agentSession = await agent.DeserializeSessionAsync(loaded.AgentSessionState.Value);
    turnCount = loaded.TurnCount;

    Console.WriteLineColorful($"✅ Loaded session: {loaded.SessionId} (\"{loaded.Label}\") - {loaded.TurnCount} turns restored", ConsoleColor.Green);
}

void HandleDeleteSession(string sessionIdPrefix)
{
    var target = FindSessionByPrefix(sessionIdPrefix);
    if (target is null) return;

    SessionStore.Delete(target.SessionId);
    if (currentWorkshopSession?.SessionId == target.SessionId)
    {
        currentWorkshopSession = null;
        agentSession = null;
        turnCount = 0;
    }

    Console.WriteLineColorful($"🗑️  Deleted session: {target.SessionId}", ConsoleColor.Yellow);
}

void HandleStatus()
{
    if (currentWorkshopSession is null)
    {
        Console.WriteLine("No active session. Type /new to create one.");
        return;
    }

    Console.WriteLine($"Active session: {currentWorkshopSession.SessionId}");
    Console.WriteLine($"  Label:    \"{currentWorkshopSession.Label}\"");
    Console.WriteLine($"  Created:  {currentWorkshopSession.CreatedAt:u}");
    Console.WriteLine($"  Turns:    {turnCount}");
}

async Task HandleChatAsync(string input)
{
    if (currentWorkshopSession is null || agentSession is null)
    {
        Console.WriteLineColorful("⚠️  No active session. Type /new to create one first.", ConsoleColor.Yellow);
        return;
    }

    try
    {
        Console.WriteColorful("Agent> ", ConsoleColor.DarkGray);

        await foreach (var update in agent.RunStreamingAsync(input, agentSession))
        {
            Console.Write(update.Text);
        }
        Console.WriteLine();
        Console.WriteLine();

        turnCount++;
        await SaveActiveSessionAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLineError($"❌ Error: {ex.Message}");
    }
}

WorkshopSession? FindSessionByPrefix(string sessionIdPrefix)
{
    var sessions = SessionStore.ListAll();
    var target = sessions.FirstOrDefault(s =>
        s.SessionId.ToString().StartsWith(sessionIdPrefix, StringComparison.OrdinalIgnoreCase));

    if (target is null)
    {
        Console.WriteLineColorful($"❌ No session found matching: {sessionIdPrefix}", ConsoleColor.Red);
    }

    return target;
}

async Task SaveActiveSessionAsync()
{
    if (currentWorkshopSession is null || agentSession is null) return;

    var serializedState = await agent.SerializeSessionAsync(agentSession);
    currentWorkshopSession.AgentSessionState = serializedState;
    currentWorkshopSession.TurnCount = turnCount;
    SessionStore.Save(currentWorkshopSession);
}

static string LoadPromptFile(string path)
{
    if (!File.Exists(path)) return "You are a helpful AI assistant.";
    return File.ReadAllText(path);
}
