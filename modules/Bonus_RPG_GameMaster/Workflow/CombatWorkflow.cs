using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using RPGGameMaster.Models;
using RPGGameMaster.Tools;

namespace RPGGameMaster.Workflow;

internal enum CombatResult
{
    CreatureDefeated,
    PlayerFled,
    PlayerDefeated,
}

/// <summary>
/// Combat sub-loop: player vs creature, round by round.
/// The Combat Narrator agent uses dice tools to determine outcomes.
/// </summary>
internal static class CombatWorkflow
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<CombatResult> RunAsync(
        GameState state, Creature creature, AIAgent combatNarrator, CancellationToken ct)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  ⚔  COMBAT: {creature.Name}");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  {creature.Description}");
        Console.WriteLine($"  HP: {creature.HP}/{creature.MaxHP} | Atk: {creature.Attack} | Def: {creature.Defense} | Difficulty: {creature.Difficulty}");
        Console.ResetColor();

        var round = 0;
        var combatLog = new List<string>();

        while (creature.HP > 0 && state.Player.HP > 0)
        {
            round++;

            // Show combat status
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"── Round {round} ──");
            Console.ResetColor();
            PrintCombatStatus(state.Player, creature);

            // Show combat options
            var options = BuildCombatOptions(state.Player);
            PrintCombatOptions(options);

            // Get player choice
            var choice = GetCombatChoice(options);
            if (choice is null) continue;

            // Build prompt for Combat Narrator
            var prompt = BuildCombatPrompt(state.Player, creature, choice, combatLog);

            // Run combat narrator
            var response = await RunAgent(combatNarrator, prompt, ct);
            var result = ParseCombatRound(response);

            if (result is not null)
            {
                // Apply creature HP change
                creature.HP = Math.Max(0, creature.HP - result.PlayerDamageDealt);

                // Apply player HP from the narrator's update (already applied via tool)
                // but also sync from game state in case the tool updated it
                if (result.PlayerHP > 0)
                    state.Player.HP = Math.Clamp(result.PlayerHP, 0, state.Player.MaxHP);

                // Print combat narrative
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\n  {result.Narrative}");
                Console.ResetColor();

                combatLog.Add($"Round {round}: Player {choice} → dealt {result.PlayerDamageDealt} dmg, took {result.PlayerDamageTaken} dmg.");

                // Check flee
                if (result.PlayerFled)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("\n  🏃 You fled from combat!");
                    Console.ResetColor();
                    state.AddLog($"Fled from {creature.Name}.");
                    return CombatResult.PlayerFled;
                }

                // Check creature defeated
                if (creature.HP <= 0 || result.CreatureDefeated)
                {
                    creature.HP = 0;
                    creature.IsDefeated = true;

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"\n  🎉 {creature.Name} has been defeated!");
                    Console.ResetColor();

                    // Award loot
                    if (creature.Loot.Count > 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("  Loot gained:");
                        foreach (var loot in creature.Loot)
                        {
                            state.Player.Inventory.Add(loot);
                            Console.WriteLine($"    🎁 {loot.Name} — {loot.Description}");
                        }
                        Console.ResetColor();
                    }

                    // Award XP
                    state.Player.XP += creature.XPReward;
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"  +{creature.XPReward} XP (total: {state.Player.XP}/{state.Player.XPToNextLevel})");
                    Console.ResetColor();

                    state.AddLog($"Defeated {creature.Name} (+{creature.XPReward} XP).");

                    // Save creature state
                    var creatureJson = JsonSerializer.Serialize(creature, JsonOpts);
                    CreatureTools.SaveCreature(creatureJson);

                    return CombatResult.CreatureDefeated;
                }

                // Check player defeated
                if (state.Player.HP <= 0 || result.PlayerDefeated)
                {
                    state.Player.HP = 0;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n  ☠ You have been slain by {creature.Name}...");
                    Console.ResetColor();
                    state.AddLog($"Slain by {creature.Name}.");
                    return CombatResult.PlayerDefeated;
                }
            }
            else
            {
                // Fallback if parsing fails — simple auto-resolution
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("  [Combat round processing...]");
                Console.ResetColor();

                // Simple fallback: both deal 1-3 damage
                var playerDmg = Math.Max(1, state.Player.EffectiveAttack - creature.Defense + Random.Shared.Next(-1, 2));
                var creatureDmg = Math.Max(1, creature.Attack - state.Player.EffectiveDefense + Random.Shared.Next(-1, 2));

                if (choice == "defend") creatureDmg /= 2;
                if (choice == "flee" && Random.Shared.Next(1, 21) >= 10)
                {
                    state.AddLog($"Fled from {creature.Name}.");
                    return CombatResult.PlayerFled;
                }

                creature.HP = Math.Max(0, creature.HP - (choice == "defend" ? 0 : playerDmg));
                state.Player.HP = Math.Max(0, state.Player.HP - creatureDmg);
            }
        }

        // Shouldn't reach here, but handle gracefully
        return creature.HP <= 0 ? CombatResult.CreatureDefeated : CombatResult.PlayerDefeated;
    }

    private static string BuildCombatPrompt(
        PlayerCharacter player, Creature creature, string action, List<string> combatLog)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Player: {player.Name} | HP: {player.HP}/{player.MaxHP} | EffAtk: {player.EffectiveAttack} | EffDef: {player.EffectiveDefense}");
        sb.AppendLine($"Creature: {creature.Name} | HP: {creature.HP}/{creature.MaxHP} | Atk: {creature.Attack} | Def: {creature.Defense}");
        sb.AppendLine($"Creature description: {creature.Description}");
        sb.AppendLine();

        if (combatLog.Count > 0)
        {
            sb.AppendLine("Previous rounds:");
            foreach (var log in combatLog.TakeLast(5))
                sb.AppendLine($"  {log}");
            sb.AppendLine();
        }

        sb.AppendLine($"Player action this round: {action}");

        if (action == "use_item")
        {
            var potion = player.Inventory.FirstOrDefault(i => i.Type == "potion");
            if (potion is not null)
                sb.AppendLine($"Using: {potion.Name} (heals {potion.EffectValue} HP)");
        }

        sb.AppendLine();
        sb.AppendLine("Use RollDice, GetPlayerStats, UpdatePlayerStats tools. Then output the CombatRound JSON.");
        return sb.ToString();
    }

    private static List<string> BuildCombatOptions(PlayerCharacter player)
    {
        var options = new List<string> { "attack", "defend", "flee" };
        if (player.Inventory.Any(i => i.Type == "potion"))
            options.Add("use_item");
        return options;
    }

    private static void PrintCombatStatus(PlayerCharacter player, Creature creature)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write($"  ❤ You: {player.HP}/{player.MaxHP} HP");
        Console.ResetColor();
        Console.Write("  |  ");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"💀 {creature.Name}: {creature.HP}/{creature.MaxHP} HP");
        Console.ResetColor();
    }

    private static void PrintCombatOptions(List<string> options)
    {
        Console.WriteLine();
        for (var i = 0; i < options.Count; i++)
        {
            var icon = options[i] switch
            {
                "attack" => "⚔️  Attack",
                "defend" => "🛡️  Defend",
                "flee" => "🏃 Flee",
                "use_item" => "🧪 Use Potion",
                _ => options[i],
            };

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"  [{i + 1}] ");
            Console.ResetColor();
            Console.WriteLine(icon);
        }
        Console.WriteLine();
    }

    private static string? GetCombatChoice(List<string> options)
    {
        while (true)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("Combat > ");
            Console.ResetColor();
            var input = Console.ReadLine();

            if (input is null)
            {
                // End of input stream — auto-flee
                Console.WriteLine();
                return options.Contains("Flee") ? "Flee" : options[0];
            }

            input = input.Trim();
            if (string.IsNullOrEmpty(input)) continue;

            if (int.TryParse(input, out var num) && num >= 1 && num <= options.Count)
                return options[num - 1];

            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"Enter 1-{options.Count}.");
            Console.ResetColor();
        }
    }

    private static async Task<string> RunAgent(AIAgent agent, string prompt, CancellationToken ct)
    {
        var session = await agent.CreateSessionAsync(ct);
        var sb = new StringBuilder();
        try
        {
            await foreach (var update in agent.RunStreamingAsync(prompt, session).WithCancellation(ct))
            {
                sb.Append(update.Text);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (sb.Length > 0) return sb.ToString().Trim();
            return "{\"narrative\": \"The combat magic fizzles momentarily.\", \"player_damage_dealt\": 0, \"player_damage_taken\": 0}";
        }
        return sb.ToString().Trim();
    }

    private static CombatRound? ParseCombatRound(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return null;
        try
        {
            return JsonSerializer.Deserialize<CombatRound>(text[start..(end + 1)], JsonOpts);
        }
        catch { return null; }
    }

    private sealed class CombatRound
    {
        [System.Text.Json.Serialization.JsonPropertyName("narrative")]
        public string Narrative { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("player_damage_dealt")]
        public int PlayerDamageDealt { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("player_damage_taken")]
        public int PlayerDamageTaken { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("player_hp")]
        public int PlayerHP { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("creature_hp")]
        public int CreatureHP { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("creature_defeated")]
        public bool CreatureDefeated { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("player_fled")]
        public bool PlayerFled { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("player_defeated")]
        public bool PlayerDefeated { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("loot_gained")]
        public List<Item> LootGained { get; set; } = [];
    }
}
