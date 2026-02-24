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
/// Uses a Combat Strategist (LLM) to generate cinematic moves,
/// CombatResolver (pure C#) to roll dice and calculate damage,
/// and a Combat Narrator (LLM) to narrate the outcome.
/// </summary>
internal static class CombatWorkflow
{

    public static async Task<CombatResult> RunAsync(
        GameState state, Creature creature, AgentConfig config,
        Func<string, string> loadPrompt, CancellationToken ct)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  ⚔  COMBAT: {creature.Name}");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  {creature.Description}");
        Console.WriteLine($"  HP: {creature.HP}/{creature.MaxHP} | Atk: {creature.Attack} | Def: {creature.Defense} | Difficulty: {creature.Difficulty}");
        Console.ResetColor();

        // Create combat agents (tool-free — no UpdatePlayerStats double-mutation)
        var strategist = config.CreateAgent(loadPrompt("combat-strategist"));
        var narrator = config.CreateAgent(loadPrompt("combat-narrator"));

        var round = 0;
        var combatLog = new List<string>();
        var locationName = state.CurrentLocation?.Name ?? "the battlefield";

        while (creature.HP > 0 && state.Player.HP > 0)
        {
            round++;

            // ── Show combat status ──
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"── Round {round} ──");
            Console.ResetColor();
            PrintCombatStatus(state.Player, creature);

            // ── Generate cinematic moves via Combat Strategist ──
            var movesPrompt = BuildStrategistPrompt(state.Player, creature, round, combatLog, locationName);
            var movesResponse = await AgentHelper.RunAgent(strategist, movesPrompt, ct,
                "[]");

            var moves = ParseMoves(movesResponse, state.Player, creature);

            // ── Display moves ──
            PrintMoves(moves);

            // ── Player picks a move ──
            var chosen = GetMoveChoice(moves);
            if (chosen is null) continue;

            // ── Find potion if item move ──
            Item? potionToUse = null;
            if (chosen.Type.Equals("item", StringComparison.OrdinalIgnoreCase))
            {
                potionToUse = state.Player.Inventory.FirstOrDefault(i => i.Type == "potion");
                if (potionToUse is null)
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine("  No potions available! Using a basic attack instead.");
                    Console.ResetColor();
                    chosen = new CombatMove { Number = 1, Name = "Quick Strike", Type = "attack", Icon = "⚔️" };
                }
            }

            // ── Resolve combat (pure C# — code-authoritative) ──
            var result = CombatResolver.Resolve(state.Player, creature, chosen, potionToUse);

            // ── Apply results to game state ──
            creature.HP = Math.Max(0, creature.HP - result.TotalDamageToCreature);
            state.Player.HP = Math.Clamp(state.Player.HP - result.TotalDamageToPlayer, 0, state.Player.MaxHP);

            // Apply healing
            if (result.HealAmount > 0 && potionToUse is not null)
            {
                state.Player.HP = Math.Min(state.Player.HP + result.HealAmount, state.Player.MaxHP);
                state.Player.Inventory.Remove(potionToUse);
            }

            // ── Display dice results ──
            PrintDiceResults(result, creature.Name);

            // ── Narrate the outcome via Combat Narrator (LLM) ──
            var narratorPrompt = BuildNarratorPrompt(result, state.Player, creature, round);
            var narrativeResponse = await AgentHelper.RunAgent(narrator, narratorPrompt, ct,
                "{\"narrative\": \"The clash of combat continues.\"}");
            var narrative = ParseNarrative(narrativeResponse);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n  {narrative}");
            Console.ResetColor();

            // ── Update combat log ──
            combatLog.Add($"Round {round}: {chosen.Name} ({chosen.Type}) → dealt {result.TotalDamageToCreature} dmg, took {result.TotalDamageToPlayer} dmg.");

            // ── Check flee ──
            if (result.FledSuccessfully)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n  🏃 You fled from combat!");
                Console.ResetColor();
                state.AddLog($"Fled from {creature.Name}.");
                return CombatResult.PlayerFled;
            }

            // ── Check creature defeated ──
            if (creature.HP <= 0)
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
                var creatureJson = JsonSerializer.Serialize(creature, AgentHelper.JsonOpts);
                CreatureTools.SaveCreature(creatureJson);

                return CombatResult.CreatureDefeated;
            }

            // ── Check player defeated ──
            if (state.Player.HP <= 0)
            {
                state.Player.HP = 0;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n  ☠ You have been slain by {creature.Name}...");
                Console.ResetColor();
                state.AddLog($"Slain by {creature.Name}.");
                return CombatResult.PlayerDefeated;
            }
        }

        return creature.HP <= 0 ? CombatResult.CreatureDefeated : CombatResult.PlayerDefeated;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Prompt builders
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private static string BuildStrategistPrompt(
        PlayerCharacter player, Creature creature, int round,
        List<string> combatLog, string locationName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Generate 3-4 combat moves for this round.\n");

        // Player state
        var weapon = player.Inventory.FirstOrDefault(i => i.Type == "weapon");
        var weaponDesc = weapon is not null ? $"{weapon.Name} (+{weapon.EffectValue} atk)" : "bare hands";
        var armor = player.Inventory.FirstOrDefault(i => i.Type == "armor");
        var armorDesc = armor is not null ? $"{armor.Name} (+{armor.EffectValue} def)" : "no armor";
        var hpPct = (int)(100.0 * player.HP / player.MaxHP);

        sb.AppendLine($"Player: {player.Name} | HP: {player.HP}/{player.MaxHP} ({hpPct}%) | " +
            $"EffAtk: {player.EffectiveAttack} | EffDef: {player.EffectiveDefense}");
        sb.AppendLine($"Weapon: {weaponDesc} | Armor: {armorDesc}");

        // Creature state
        var creatureHpPct = (int)(100.0 * creature.HP / creature.MaxHP);
        sb.AppendLine($"\nCreature: {creature.Name} | HP: {creature.HP}/{creature.MaxHP} ({creatureHpPct}%) | " +
            $"Atk: {creature.Attack} | Def: {creature.Defense} | Difficulty: {creature.Difficulty}");
        sb.AppendLine($"Description: {creature.Description}");

        sb.AppendLine($"\nLocation: {locationName}");
        sb.AppendLine($"Round: {round}");

        // Potions check
        var potions = player.Inventory.Where(i => i.Type == "potion").ToList();
        if (potions.Count > 0 && player.HP < player.MaxHP)
        {
            sb.AppendLine($"\nPOTION AVAILABLE: {potions[0].Name} (heals {potions[0].EffectValue}) — " +
                "include an 'item' type move.");
        }

        // Context hints
        if (hpPct <= 30)
            sb.AppendLine("\n⚠ Player HP is LOW — include a defensive or flee option!");
        if (creatureHpPct <= 25)
            sb.AppendLine("\n💀 Creature is nearly dead — include a finishing move!");

        // Combat history
        if (combatLog.Count > 0)
        {
            sb.AppendLine("\nPrevious rounds:");
            foreach (var log in combatLog.TakeLast(4))
                sb.AppendLine($"  {log}");
        }

        sb.AppendLine("\nOutput ONLY the raw JSON array of CombatMove objects.");
        return sb.ToString();
    }

    private static string BuildNarratorPrompt(
        CombatRoundResult result, PlayerCharacter player, Creature creature, int round)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Narrate this combat round result dramatically in 2-4 sentences.\n");

        sb.AppendLine($"Round: {round}");
        sb.AppendLine($"Player: {player.Name} (HP: {player.HP}/{player.MaxHP})");
        sb.AppendLine($"Creature: {creature.Name} (HP: {creature.HP}/{creature.MaxHP})");
        sb.AppendLine($"Move used: {result.MoveName} (type: {result.MoveType})");

        switch (result.MoveType)
        {
            case "attack":
            case "heavy":
                sb.AppendLine($"Attack roll: {result.PlayerAttackRoll} (total: {result.PlayerAttackTotal}) — {(result.PlayerHit ? "HIT" : "MISS")}{(result.PlayerCrit ? " — CRITICAL!" : "")}");
                if (result.PlayerHit)
                    sb.AppendLine($"Player dealt {result.PlayerDamageDealt} damage to {creature.Name}.");
                if (result.SelfDamage > 0)
                    sb.AppendLine($"The reckless attack cost the player {result.SelfDamage} self-damage.");
                break;

            case "defensive":
                sb.AppendLine($"Player took a defensive stance, halving incoming damage.");
                if (result.CounterHit)
                    sb.AppendLine($"Counter-attack connected for {result.CounterDamage} damage!");
                else
                    sb.AppendLine("No counter-attack connected.");
                break;

            case "flee":
                sb.AppendLine($"Flee roll: {result.FleeRoll} — {(result.FledSuccessfully ? "ESCAPED!" : "FAILED!")}");
                break;

            case "item":
                if (result.ItemUsed is not null)
                    sb.AppendLine($"Used {result.ItemUsed}, healing {result.HealAmount} HP.");
                break;
        }

        // Creature's counter-attack (for non-defensive, non-fled rounds)
        if (result.MoveType is not "defensive" && !result.FledSuccessfully)
        {
            sb.AppendLine($"{creature.Name}'s attack roll: {result.CreatureAttackRoll} — {(result.CreatureHit ? "HIT" : "MISS")}{(result.CreatureCrit ? " — CRITICAL!" : "")}");
            if (result.CreatureHit)
                sb.AppendLine($"{creature.Name} dealt {result.CreatureDamageTaken} damage to the player.");
        }

        sb.AppendLine("\nOutput ONLY the JSON {\"narrative\": \"...\"}.");
        return sb.ToString();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Parsing
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private static List<CombatMove> ParseMoves(string text, PlayerCharacter player, Creature creature)
    {
        // Try to parse as JSON array
        var json = ExtractJsonArray(text);
        if (json is not null)
        {
            try
            {
                var moves = JsonSerializer.Deserialize<List<CombatMove>>(json, AgentHelper.JsonOpts);
                if (moves is not null && moves.Count >= 2)
                {
                    // Re-number sequentially
                    for (var i = 0; i < moves.Count; i++)
                        moves[i].Number = i + 1;
                    return moves;
                }
            }
            catch { /* fall through to defaults */ }
        }

        // Fallback: generate contextual default moves
        return BuildFallbackMoves(player, creature);
    }

    private static List<CombatMove> BuildFallbackMoves(PlayerCharacter player, Creature creature)
    {
        var moves = new List<CombatMove>
        {
            new()
            {
                Number = 1, Name = "Quick Strike", Icon = "⚔️", Type = "attack",
                Description = $"A reliable strike aimed at {creature.Name}",
                AttackBonus = 1, DamageBonus = 0,
            },
            new()
            {
                Number = 2, Name = "Defensive Stance", Icon = "🛡️", Type = "defensive",
                Description = "Brace for impact and look for an opening to counter",
                DefenseBonus = 2, AttackBonus = 0,
            },
            new()
            {
                Number = 3, Name = "Disengage", Icon = "🏃", Type = "flee",
                Description = "Attempt to break away from combat",
            },
        };

        if (player.Inventory.Any(i => i.Type == "potion") && player.HP < player.MaxHP)
        {
            moves.Add(new CombatMove
            {
                Number = 4, Name = "Drink Potion", Icon = "🧪", Type = "item",
                Description = "Quickly down a healing potion",
            });
        }

        return moves;
    }

    private static string? ExtractJsonArray(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        // Strip markdown fences
        var cleaned = text;
        if (cleaned.Contains("```"))
        {
            var fenceStart = cleaned.IndexOf("```", StringComparison.Ordinal);
            var afterFence = cleaned.IndexOf('\n', fenceStart);
            if (afterFence > 0)
            {
                var fenceEnd = cleaned.IndexOf("```", afterFence, StringComparison.Ordinal);
                if (fenceEnd > 0)
                    cleaned = cleaned[(afterFence + 1)..fenceEnd];
            }
        }

        var start = cleaned.IndexOf('[');
        if (start < 0) return null;

        // Find matching closing bracket with depth tracking
        var depth = 0;
        for (var i = start; i < cleaned.Length; i++)
        {
            if (cleaned[i] == '[') depth++;
            else if (cleaned[i] == ']')
            {
                depth--;
                if (depth == 0)
                    return cleaned[start..(i + 1)];
            }
        }

        return null;
    }

    private static string ParseNarrative(string text)
    {
        var json = AgentHelper.ExtractJson(text);
        if (json is null) return text.Trim(); // treat entire response as narrative

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("narrative", out var n))
                return n.GetString() ?? text.Trim();
        }
        catch { /* fall through */ }
        return text.Trim();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Console UI
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

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

    private static void PrintMoves(List<CombatMove> moves)
    {
        Console.WriteLine();
        foreach (var move in moves)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"  [{move.Number}] {move.Icon} {move.Name}");
            Console.ResetColor();

            // Show modifier hints
            var hints = new List<string>();
            if (move.AttackBonus != 0) hints.Add($"{(move.AttackBonus > 0 ? "+" : "")}{move.AttackBonus} hit");
            if (move.DamageBonus != 0) hints.Add($"{(move.DamageBonus > 0 ? "+" : "")}{move.DamageBonus} dmg");
            if (move.DefenseBonus != 0) hints.Add($"+{move.DefenseBonus} def");
            if (move.SelfDamage > 0) hints.Add($"{move.SelfDamage} self-dmg");

            if (hints.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($" ({string.Join(", ", hints)})");
                Console.ResetColor();
            }
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"      {move.Description}");
            Console.ResetColor();
        }
        Console.WriteLine();
    }

    private static void PrintDiceResults(CombatRoundResult result, string creatureName)
    {
        Console.WriteLine();

        switch (result.MoveType)
        {
            case "attack":
            case "heavy":
                Console.ForegroundColor = result.PlayerHit ? ConsoleColor.Green : ConsoleColor.DarkGray;
                Console.Write($"  🎲 Attack: {result.PlayerAttackRoll}");
                if (result.PlayerAttackTotal != result.PlayerAttackRoll)
                    Console.Write($" ({(result.PlayerAttackTotal > result.PlayerAttackRoll ? "+" : "")}{result.PlayerAttackTotal - result.PlayerAttackRoll} = {result.PlayerAttackTotal})");
                Console.Write(result.PlayerHit ? " — HIT!" : " — MISS");
                if (result.PlayerCrit) Console.Write(" CRITICAL!");
                Console.WriteLine();
                if (result.PlayerHit)
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"     Damage dealt: {result.PlayerDamageDealt}");
                }
                if (result.SelfDamage > 0)
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine($"     Self-damage: {result.SelfDamage}");
                }
                Console.ResetColor();
                break;

            case "defensive":
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("  🎲 Defensive stance — incoming damage halved");
                Console.WriteLine();
                if (result.CounterHit)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"     Counter-attack: {result.CounterDamage} damage!");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"     Counter roll: {result.CounterRoll} — no opening");
                }
                Console.ResetColor();
                break;

            case "flee":
                Console.ForegroundColor = result.FledSuccessfully ? ConsoleColor.Green : ConsoleColor.Red;
                Console.WriteLine($"  🎲 Flee: {result.FleeRoll} — {(result.FledSuccessfully ? "ESCAPED!" : "BLOCKED!")}");
                Console.ResetColor();
                break;

            case "item":
                if (result.HealAmount > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"  🧪 {result.ItemUsed}: +{result.HealAmount} HP");
                    Console.ResetColor();
                }
                break;
        }

        // Creature attack result (unless fled or defensive — defensive printed its own way)
        if (result.MoveType is not "flee" || !result.FledSuccessfully)
        {
            if (result.MoveType is not "defensive") // defensive already shows reduced damage
            {
                Console.ForegroundColor = result.CreatureHit ? ConsoleColor.Red : ConsoleColor.DarkGray;
                Console.Write($"  🎲 {creatureName}: {result.CreatureAttackRoll}");
                Console.Write(result.CreatureHit ? " — HIT!" : " — MISS");
                if (result.CreatureCrit) Console.Write(" CRITICAL!");
                Console.WriteLine();
                if (result.CreatureHit)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"     Damage taken: {result.CreatureDamageTaken}");
                }
                Console.ResetColor();
            }
            else
            {
                // Show creature attack against defensive stance
                Console.ForegroundColor = result.CreatureHit ? ConsoleColor.DarkYellow : ConsoleColor.DarkGray;
                Console.Write($"  🎲 {creatureName}: {result.CreatureAttackRoll}");
                Console.Write(result.CreatureHit ? $" — blocked (took {result.CreatureDamageTaken})" : " — MISS");
                Console.WriteLine();
                Console.ResetColor();
            }
        }
    }

    private static CombatMove? GetMoveChoice(List<CombatMove> moves)
    {
        while (true)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("Combat > ");
            Console.ResetColor();
            var input = Console.ReadLine();

            if (input is null)
            {
                Console.WriteLine();
                return moves.FirstOrDefault(m => m.Type.Equals("flee", StringComparison.OrdinalIgnoreCase))
                    ?? moves[0];
            }

            input = input.Trim();
            if (string.IsNullOrEmpty(input)) continue;

            if (int.TryParse(input, out var num))
            {
                var match = moves.FirstOrDefault(m => m.Number == num);
                if (match is not null) return match;
            }

            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"Enter 1-{moves.Count}.");
            Console.ResetColor();
        }
    }
}

