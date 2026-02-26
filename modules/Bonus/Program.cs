using RPGGameMaster;
using RPGGameMaster.Models;
using RPGGameMaster.Workflow;

Console.OutputEncoding = System.Text.Encoding.UTF8;

Console.ForegroundColor = ConsoleColor.DarkYellow;
Console.WriteLine("""

                  />
     (           //------------------------------------------------------\
    (*)OXOXOXOXO(*>        ~ * ~ A I  G A M E  M A S T E R ~ * ~          \
     (           \\--------------------------------------------------------\
                  \>

    """);
Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine("""
    ╔═══════════════════════════════════════════════════════════╗
    ║    Bonus Module — AI-Driven RPG with Dynamic Agents       ║
    ║    Powered by Microsoft Agent Framework + Azure OpenAI    ║
    ╚═══════════════════════════════════════════════════════════╝
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
        TokenTracker.PrintSummary();
        return;
    }

    if (menuInput == "2")
    {
        var saves = SaveManager.ListSaves();
        if (saves.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("  No saved games found. Start a new game first.");
            Console.ResetColor();
            Console.WriteLine();
            continue;
        }

        // Display saved games
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  📂 Saved Games:");
        Console.ResetColor();
        Console.WriteLine();

        for (var i = 0; i < saves.Count; i++)
        {
            var s = saves[i].State;
            var themeShort = s.WorldTheme.Contains('—')
                ? s.WorldTheme[..s.WorldTheme.IndexOf('—')].Trim()
                : (s.WorldTheme.Length > 25 ? s.WorldTheme[..25] + "…" : s.WorldTheme);
            var ago = TimeFormatHelper.FormatTimeAgo(s.LastSavedAt);

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"  [{i + 1}] ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"{s.Player.Name}");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($" — {themeShort}");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($" (Lvl {s.Player.Level}, Turn {s.TurnCount}");
            Console.ForegroundColor = s.Player.Health.Percentage <= 25 ? ConsoleColor.Red
                : s.Player.Health.Percentage <= 50 ? ConsoleColor.Yellow : ConsoleColor.Green;
            Console.Write($", HP {s.Player.Health}");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($", {ago})");
            Console.ResetColor();
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  [D] 🗑️  Delete a save");
        Console.WriteLine("  [B] ← Back to menu");
        Console.ResetColor();
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("  Choose > ");
        Console.ResetColor();
        var loadInput = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(loadInput) ||
            loadInput.Equals("B", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine();
            continue;
        }

        // Delete flow
        if (loadInput.Equals("D", StringComparison.OrdinalIgnoreCase))
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("  Enter save number to delete > ");
            Console.ResetColor();
            var delInput = Console.ReadLine()?.Trim();
            if (int.TryParse(delInput, out var delNum) && delNum >= 1 && delNum <= saves.Count)
            {
                var target = saves[delNum - 1];
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write($"  Delete {target.State.Player.Name}'s save? (y/n) > ");
                Console.ResetColor();
                var confirm = Console.ReadLine()?.Trim();
                if (confirm?.Equals("y", StringComparison.OrdinalIgnoreCase) == true)
                {
                    SaveManager.Delete(target.Path);
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("  Save deleted.");
                    Console.ResetColor();
                }
            }
            Console.WriteLine();
            continue;
        }

        // Load selected save
        if (int.TryParse(loadInput, out var idx) && idx >= 1 && idx <= saves.Count)
        {
            var (savePath, loadedState) = saves[idx - 1];
            state = loadedState;

            // Migrate legacy saves (no SaveId) to new naming
            if (state.SaveId.IsEmpty)
            {
                SaveManager.MigrateLegacy(state, savePath);
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n  ✅ Game loaded! {state.Player.Name} — Level {state.Player.Level} — Turn {state.TurnCount}");
            Console.ResetColor();
            break;
        }

        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("  Invalid selection.");
        Console.ResetColor();
        Console.WriteLine();
        continue;
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
    TokenTracker.PrintSummary();
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
    for (var i = 0; i < WorldThemes.Themes.Count; i++)
    {
        Console.WriteLine($"  [{i + 1}] {WorldThemes.Themes[i].Label}");
    }
    Console.WriteLine($"  [{WorldThemes.Themes.Count + 1}] ✏️  Custom — Describe your own world");
    Console.ResetColor();
    Console.WriteLine();

    Console.Write("  Theme > ");
    var themeInput = Console.ReadLine()?.Trim();
    string? theme = null;

    if (int.TryParse(themeInput, out var themeIdx))
        theme = WorldThemes.GetDescription(themeIdx);

    if (theme is null)
    {
        Console.Write("  Describe your world theme: ");
        theme = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(theme))
            theme = WorldThemes.DefaultDescription;
    }

    // Language selection
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("  Choose a language for the game world:");
    for (var i = 0; i < SupportedLanguages.All.Count; i++)
        Console.WriteLine($"  [{i + 1,2}] {SupportedLanguages.All[i].Label}");
    Console.ResetColor();
    Console.WriteLine();
    Console.Write("  Language > ");
    var langInput = Console.ReadLine()?.Trim();
    var language = SupportedLanguages.FromSelection(langInput);

    var state = new GameState
    {
        SaveId = EntityId.New(),
        LastSavedAt = DateTime.UtcNow,
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
                new Item { Name = UIStrings.Get(language, "item_rusty_sword"), Description = UIStrings.Get(language, "item_rusty_sword_desc"), Type = ItemType.Weapon, EffectValue = 2 },
                new Item { Name = UIStrings.Get(language, "item_healing_potion"), Description = UIStrings.Get(language, "item_healing_potion_desc"), Type = ItemType.Potion, EffectValue = 20 },
            ],
        },
        WorldTheme = theme,
        Language = language,
    };

    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"  ✅ {name} is ready for adventure!");
    Console.ResetColor();
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"  HP: {state.Player.Health} | Atk: {state.Player.Attack} | Def: {state.Player.Defense} | Gold: {state.Player.Gold}");
    Console.WriteLine($"  Inventory: {string.Join(", ", state.Player.Inventory.Select(i => i.Name))}");
    Console.WriteLine($"  World: {theme}");
    Console.ResetColor();
    Console.WriteLine();

    return state;
}
