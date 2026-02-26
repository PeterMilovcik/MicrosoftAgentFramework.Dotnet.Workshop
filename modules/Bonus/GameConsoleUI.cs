using RPGGameMaster.Models;

namespace RPGGameMaster;

/// <summary>
/// Pure console-output helpers extracted from GameMasterWorkflow.
/// Every method here only writes to stdout — no game-state mutation, no input.
/// </summary>
internal static class GameConsoleUI
{
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Color helper — eliminates ad-hoc ForegroundColor/ResetColor pairs
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    /// <summary>Write a line in the specified color, resetting afterwards.</summary>
    internal static void WriteLine(string text, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    /// <summary>Write text (no newline) in the specified color, resetting afterwards.</summary>
    internal static void Write(string text, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ResetColor();
    }

    // ── Narrative box ──

    internal static void PrintNarrative(string narrative)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(narrative);
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.ResetColor();
    }

    // ── Option list ──

    internal static void PrintOptions(List<GameOption> options, string language)
    {
        Console.WriteLine();
        foreach (var opt in options)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"  [{opt.Number}] ");
            Console.ResetColor();
            Console.WriteLine(opt.Description);
        }
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  {UIStrings.Get(language, "input_help_hint")}");
        Console.ResetColor();
        Console.WriteLine();
    }

    // ── Player stats block ──

    internal static void PrintStats(GameState state)
    {
        var p = state.Player;
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(UIStrings.Get(state.Language, "stats_header"));
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"   {UIStrings.Format(state.Language, "stat_name", p.Name)}");
        Console.WriteLine($"   {UIStrings.Format(state.Language, "stat_level", p.Level)}");
        Console.Write($"   ");
        Console.ForegroundColor = p.Health.Percentage <= 25 ? ConsoleColor.Red
            : p.Health.Percentage <= 50 ? ConsoleColor.Yellow : ConsoleColor.Green;
        Console.WriteLine(UIStrings.Format(state.Language, "stat_hp", p.Health.Current, p.Health.Max));
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"   {UIStrings.Format(state.Language, "stat_attack", p.EffectiveAttack, p.Attack)}");
        Console.WriteLine($"   {UIStrings.Format(state.Language, "stat_defense", p.EffectiveDefense, p.Defense)}");
        Console.WriteLine($"   {UIStrings.Format(state.Language, "stat_xp", p.XP, p.XPToNextLevel)}");
        Console.WriteLine($"   {UIStrings.Format(state.Language, "stat_gold", p.Gold)}");
        Console.ResetColor();
    }

    // ── Help screen ──

    internal static void PrintHelp(string language)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(UIStrings.Get(language, "help_header"));
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("   map        "); Console.ForegroundColor = ConsoleColor.DarkGray; Console.WriteLine(UIStrings.Get(language, "help_map"));
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("   inv        "); Console.ForegroundColor = ConsoleColor.DarkGray; Console.WriteLine(UIStrings.Get(language, "help_inv"));
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("   quests     "); Console.ForegroundColor = ConsoleColor.DarkGray; Console.WriteLine(UIStrings.Get(language, "help_quests"));
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("   stats      "); Console.ForegroundColor = ConsoleColor.DarkGray; Console.WriteLine(UIStrings.Get(language, "help_stats"));
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("   ?          "); Console.ForegroundColor = ConsoleColor.DarkGray; Console.WriteLine(UIStrings.Get(language, "help_help"));
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("   exit       "); Console.ForegroundColor = ConsoleColor.DarkGray; Console.WriteLine(UIStrings.Get(language, "help_exit"));
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"\n   {UIStrings.Get(language, "help_number")}");
        Console.ResetColor();
    }

    // ── Quest journal ──

    internal static void PrintQuests(GameState state)
    {
        var active = state.Player.ActiveQuests.Where(q => !q.IsComplete).ToList();
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(UIStrings.Get(state.Language, "quests_header"));
        Console.ResetColor();

        if (active.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"   {UIStrings.Get(state.Language, "quests_none")}");
            Console.ResetColor();
        }
        else
        {
            foreach (var q in active)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"   • {q.Title}");
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($" — {q.Description}");
                Console.ResetColor();
            }
        }
    }

    // ── Map ──

    internal static void PrintMap(GameState state)
    {
        Console.WriteLine();
        WriteLine(UIStrings.Get(state.Language, "map_header"), ConsoleColor.Yellow);

        foreach (var loc in state.Locations.Values)
        {
            var isCurrent = loc.Id == state.CurrentLocationId;
            Write($"   {(isCurrent ? "➤ " : "  ")}{loc.Name}", isCurrent ? ConsoleColor.Cyan : ConsoleColor.White);

            var tags = new List<string>();
            if (!string.IsNullOrWhiteSpace(loc.Type))
                tags.Add(loc.Type);
            tags.Add(loc.DangerLevel.ToString().ToLowerInvariant());
            var tagStr = tags.Count > 0 ? $" [{string.Join(", ", tags)}]" : "";

            var exitNames = loc.Exits.Select(e =>
            {
                var explored = !e.TargetLocationId.IsEmpty ? "✓" : "?";
                return $"{e.Direction}[{explored}]";
            });
            WriteLine($"{tagStr} — exits: {string.Join(", ", exitNames)}", ConsoleColor.DarkGray);
        }

        WriteLine($"\n   {UIStrings.Format(state.Language, "map_total", state.Locations.Count)}", ConsoleColor.DarkGray);
    }

    // ── Inventory display ──

    internal static void PrintInventory(GameState state)
    {
        var player = state.Player;
        Console.WriteLine();
        WriteLine(UIStrings.Get(state.Language, "inventory_header"), ConsoleColor.Yellow);

        if (player.Inventory.Count == 0)
        {
            WriteLine($"   {UIStrings.Get(state.Language, "inventory_empty")}", ConsoleColor.DarkGray);
            return;
        }

        for (var i = 0; i < player.Inventory.Count; i++)
        {
            var item = player.Inventory[i];
            var bonus = item.Type switch
            {
                ItemType.Weapon => UIStrings.Format(state.Language, "inv_atk_bonus", item.EffectValue),
                ItemType.Armor => UIStrings.Format(state.Language, "inv_def_bonus", item.EffectValue),
                ItemType.Potion => UIStrings.Format(state.Language, "inv_heals", item.EffectValue),
                _ => "",
            };
            Write($"   [{i + 1}] {item.Name}", ConsoleColor.White);
            WriteLine($" — {item.Type}{bonus}", ConsoleColor.DarkGray);
        }
    }

    // ── Death banner ──

    internal static void PrintDeathBanner(GameState state, int goldLost, int xpLost, string? respawnName)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("╔══════════════════════════════════════════╗");
        Console.WriteLine($"║   {UIStrings.Get(state.Language, "death_header"),36}   ║");
        Console.WriteLine("╚══════════════════════════════════════════╝");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine($"\n  {UIStrings.Format(state.Language, "death_respawn", respawnName ?? "the starting area")}");
        Console.WriteLine($"  {UIStrings.Format(state.Language, "death_penalty", goldLost, xpLost)}");
        Console.WriteLine($"  {UIStrings.Format(state.Language, "death_restored", state.Player.Health.Current, state.Player.Health.Max)}");
        Console.ResetColor();
    }
}
