using RPGGameMaster.Models;

namespace RPGGameMaster;

/// <summary>
/// Pure console-output helpers extracted from GameMasterWorkflow.
/// Every method here only writes to stdout — no game-state mutation, no input.
/// </summary>
internal static class GameConsoleUI
{
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
}
