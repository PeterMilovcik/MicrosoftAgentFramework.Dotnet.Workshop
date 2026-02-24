using RPGGameMaster;
using RPGGameMaster.Models;
using RPGGameMaster.Workflow;

Console.OutputEncoding = System.Text.Encoding.UTF8;

Console.ForegroundColor = ConsoleColor.DarkYellow;
Console.WriteLine("""

                  />
     (           //------------------------------------------------------\
    (*)OXOXOXOXO(*>      ~ * ~ R P G  G A M E  M A S T E R ~ * ~          \
     (           \\--------------------------------------------------------\
                  \>

    """);
Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine("""
    ╔══════════════════════════════════════════════════════════╗
    ║  Bonus Module — AI-Driven RPG with Dynamic Agents       ║
    ║  Powered by Microsoft Agent Framework + Azure OpenAI    ║
    ╚══════════════════════════════════════════════════════════╝
    """);
Console.ResetColor();

// ── Load configuration ──
var config = AgentConfig.Load();
if (config is null)
{
    Environment.Exit(1);
    return;
}

Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine($"  Endpoint:   {config.Endpoint}");
Console.WriteLine($"  Deployment: {config.Deployment}");
Console.ResetColor();
Console.WriteLine();

// ── Main menu ──
GameState? state = null;

while (true)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("  [1] 🆕 New Game");
    Console.WriteLine("  [2] 📂 Load Game");
    Console.WriteLine("  [3] ❌ Quit");
    Console.ResetColor();
    Console.WriteLine();

    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.Write("  Choose > ");
    Console.ResetColor();
    var menuInput = Console.ReadLine()?.Trim();

    if (menuInput is null || menuInput == "3" || menuInput.Equals("quit", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("\nFarewell, adventurer! 👋");
        AgentConfig.PrintTokenSummary();
        return;
    }

    if (menuInput == "2")
    {
        state = GameMasterWorkflow.LoadGameFromDisk();
        if (state is null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("  No save file found. Start a new game first.");
            Console.ResetColor();
            Console.WriteLine();
            continue;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n  ✅ Game loaded! {state.Player.Name} — Level {state.Player.Level} — Turn {state.TurnCount}");
        Console.ResetColor();
        break;
    }

    if (menuInput == "1")
    {
        state = CreateNewGame();
        break;
    }
}

if (state is null)
{
    Console.WriteLine("No game state. Exiting.");
    return;
}

// ── Run the game ──
try
{
    await GameMasterWorkflow.RunAsync(config, state);
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine($"\n❌ Error: {ex.Message}");
    Console.ResetColor();
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.Error.WriteLine(ex.StackTrace);
    Console.ResetColor();
}
finally
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    Console.ResetColor();
    Console.WriteLine($"  Session completed: {state.TurnCount} turns");
    Console.WriteLine($"  Locations discovered: {state.Locations.Count}");
    Console.WriteLine($"  NPCs met: {state.NPCs.Values.Count(n => n.HasMet)}");
    Console.WriteLine($"  Creatures defeated: {state.Creatures.Values.Count(c => c.IsDefeated)}");
    AgentConfig.PrintTokenSummary();
}

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// New game creation
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

static GameState CreateNewGame()
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("  ── Character Creation ──");
    Console.ResetColor();
    Console.WriteLine();

    // Character name
    Console.Write("  Enter your character's name: ");
    var name = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(name)) name = "Hero";

    // World theme
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("  Choose a world theme:");
    Console.WriteLine("  [1] 🏰 Dark Fantasy — Crumbling castles, ancient curses, and dark forests");
    Console.WriteLine("  [2] 🌲 Enchanted Forest — Mystical groves, fae creatures, and hidden magic");
    Console.WriteLine("  [3] 👻 Haunted Ruins — Forgotten temples, restless spirits, and deadly traps");
    Console.WriteLine("  [4] 🚀 Sci-Fi Station — Abandoned space station, alien tech, and rogue AI");
    Console.WriteLine("  [5] 🏴‍☠️ Pirate Archipelago — Tropical islands, sea monsters, and buried treasure");
    Console.WriteLine("  [6] ✏️  Custom — Describe your own world");
    Console.ResetColor();
    Console.WriteLine();

    Console.Write("  Theme > ");
    var themeInput = Console.ReadLine()?.Trim();
    var theme = themeInput switch
    {
        "1" => "Dark Fantasy — a grim world of crumbling castles, ancient curses, dark forests, and undead horrors",
        "2" => "Enchanted Forest — a mystical realm of ancient groves, fae creatures, hidden magic, and nature spirits",
        "3" => "Haunted Ruins — a desolate landscape of forgotten temples, restless spirits, deadly traps, and lost civilizations",
        "4" => "Sci-Fi Station — an abandoned orbital station with malfunctioning systems, alien technology, rogue AI, and zero-gravity hazards",
        "5" => "Pirate Archipelago — a chain of tropical islands with sea monsters, buried treasure, rival pirates, and cursed shipwrecks",
        _ => null,
    };

    if (theme is null)
    {
        Console.Write("  Describe your world theme: ");
        theme = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(theme))
            theme = "High Fantasy — a classic world of knights, dragons, wizards, and ancient dungeons";
    }

    var state = new GameState
    {
        Player = new PlayerCharacter
        {
            Name = name,
            HP = 100,
            MaxHP = 100,
            Attack = 5,
            Defense = 3,
            Level = 1,
            XP = 0,
            XPToNextLevel = 100,
            Gold = 10,
            Inventory =
            [
                new Item { Name = "Rusty Sword", Description = "A well-worn blade, still sharp enough", Type = "weapon", EffectValue = 2 },
                new Item { Name = "Minor Healing Potion", Description = "A small vial of red liquid", Type = "potion", EffectValue = 20 },
            ],
        },
        WorldTheme = theme,
    };

    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"  ✅ {name} is ready for adventure!");
    Console.ResetColor();
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"  HP: {state.Player.HP} | Atk: {state.Player.Attack} | Def: {state.Player.Defense} | Gold: {state.Player.Gold}");
    Console.WriteLine($"  Inventory: {string.Join(", ", state.Player.Inventory.Select(i => i.Name))}");
    Console.WriteLine($"  World: {theme}");
    Console.ResetColor();
    Console.WriteLine();

    return state;
}
