using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using RPGGameMaster.Models;

namespace RPGGameMaster.Workflow;

/// <summary>
/// Combat sub-loop: player vs creature, round by round.
/// Uses a Combat Strategist (LLM) to generate cinematic moves,
/// CombatResolver (pure C#) to roll dice and calculate damage,
/// and a Combat Narrator (LLM) to narrate the outcome.
/// <para>NOTE: Static — see GameMasterWorkflow for DI migration notes.</para>
/// </summary>
internal static class CombatWorkflow
{

    public static async Task<CombatResult> RunAsync(
        GameState state, Creature creature, AgentConfig config,
        AIAgent strategist, AIAgent narrator,
        CancellationToken ct)
    {
        Console.WriteLine();
        GameConsoleUI.WriteLine($"  {UIStrings.Format(state.Language, "combat_header", creature.Name)}", ConsoleColor.Red);
        GameConsoleUI.WriteLine($"  {creature.Description}", ConsoleColor.DarkGray);
        GameConsoleUI.WriteLine($"  {UIStrings.Format(state.Language, "combat_stats", creature.Health.Current, creature.Health.Max, creature.Attack, creature.Defense, creature.Difficulty)}", ConsoleColor.DarkGray);

        var round = 0;
        var combatLog = new List<string>();
        var locationName = state.CurrentLocation?.Name ?? "the battlefield";
        var locationAtmosphere = state.CurrentLocation?.Atmosphere ?? "";

        while (creature.Health.IsAlive && state.Player.Health.IsAlive)
        {
            round++;

            // ── Show combat status ──
            Console.WriteLine();
            GameConsoleUI.WriteLine($"{UIStrings.Format(state.Language, "combat_round", round)}", ConsoleColor.White);
            PrintCombatStatus(state.Player, creature, state.Language);

            // ── Generate cinematic moves via Combat Strategist ──
            var movesPrompt = BuildStrategistPrompt(state.Player, creature, round, combatLog, locationName, locationAtmosphere, state.Language);
            string movesResponse;
            await using (ConsoleSpinner.Start("[Strategist] Planning moves..."))
            {
                movesResponse = await AgentHelper.RunAgent(strategist, movesPrompt, ct,
                    "[]");
            }

            var moves = ParseMoves(movesResponse, state.Player, creature, state.Language);

            // ── Display moves ──
            PrintMoves(moves);

            // ── Player picks a move ──
            var chosen = GetMoveChoice(moves, state.Language);
            if (chosen is null) continue;

            // ── Find potion if item move ──
            Item? potionToUse = null;
            if (chosen.Type == MoveType.Item)
            {
                potionToUse = state.Player.Inventory.FirstOrDefault(i => i.Type == ItemType.Potion);
                if (potionToUse is null)
                {
                    GameConsoleUI.WriteLine($"  {UIStrings.Get(state.Language, "combat_no_potions")}", ConsoleColor.DarkYellow);
                    chosen = new CombatMove { Number = 1, Name = "Quick Strike", Type = MoveType.Attack, Icon = "⚔️" };
                }
            }

            // ── Resolve combat (pure C# — code-authoritative) ──
            var result = CombatResolver.Resolve(state.Player, creature, chosen, potionToUse);

            // ── Apply results to game state ──
            creature.Health = creature.Health.TakeDamage(result.TotalDamageToCreature);
            state.Player.Health = state.Player.Health.TakeDamage(result.TotalDamageToPlayer);

            // Apply healing
            if (result.HealAmount > 0 && potionToUse is not null)
            {
                state.Player.Health = state.Player.Health.Heal(result.HealAmount);
                state.Player.Inventory.Remove(potionToUse);
            }

            // ── Display dice results ──
            PrintDiceResults(result, creature.Name, state.Language);

            // ── Narrate the outcome via Combat Narrator (LLM) ──
            var narratorPrompt = BuildNarratorPrompt(result, state.Player, creature, round, locationName, locationAtmosphere, state.Language);
            string narrativeResponse;
            await using (ConsoleSpinner.Start("[Narrator] Describing battle..."))
            {
                narrativeResponse = await AgentHelper.RunAgent(narrator, narratorPrompt, ct,
                    "{\"narrative\": \"The clash of combat continues.\"}");
            }
            var narrative = ParseNarrative(narrativeResponse);

            GameConsoleUI.WriteLine($"\n  {narrative}", ConsoleColor.Cyan);

            // ── Update combat log ──
            combatLog.Add($"Round {round}: {chosen.Name} ({chosen.Type}) → dealt {result.TotalDamageToCreature} dmg, took {result.TotalDamageToPlayer} dmg.");

            // ── Check flee ──
            if (result.FledSuccessfully)
            {
                GameConsoleUI.WriteLine($"\n  {UIStrings.Get(state.Language, "combat_fled")}", ConsoleColor.Yellow);
                state.AddLog($"Fled from {creature.Name}.");
                return CombatResult.PlayerFled;
            }

            // ── Check creature defeated ──
            if (creature.Health.IsDead)
            {
                creature.IsDefeated = true;

                GameConsoleUI.WriteLine($"\n  {UIStrings.Format(state.Language, "combat_defeated", creature.Name)}", ConsoleColor.Green);

                // Award loot
                if (creature.Loot.Count > 0)
                {
                    GameConsoleUI.WriteLine($"  {UIStrings.Get(state.Language, "combat_loot")}", ConsoleColor.Yellow);
                    foreach (var loot in creature.Loot)
                    {
                        state.Player.Inventory.Add(loot);
                        GameConsoleUI.WriteLine($"    🎁 {loot.Name} — {loot.Description}", ConsoleColor.Yellow);
                    }
                }

                // Award XP
                state.Player.AddXP(creature.XPReward);
                GameConsoleUI.WriteLine($"  {UIStrings.Format(state.Language, "combat_xp", creature.XPReward, state.Player.XP, state.Player.XPToNextLevel)}", ConsoleColor.Cyan);

                state.AddLog($"Defeated {creature.Name} (+{creature.XPReward} XP).");

                return CombatResult.CreatureDefeated;
            }

            // ── Check player defeated ──
            if (state.Player.Health.IsDead)
            {
                GameConsoleUI.WriteLine($"\n  {UIStrings.Format(state.Language, "combat_slain", creature.Name)}", ConsoleColor.Red);
                state.AddLog($"Slain by {creature.Name}.");
                return CombatResult.PlayerDefeated;
            }
        }

        return creature.Health.IsDead ? CombatResult.CreatureDefeated : CombatResult.PlayerDefeated;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Prompt builders
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private static string BuildStrategistPrompt(
        PlayerCharacter player, Creature creature, int round,
        List<string> combatLog, string locationName, string locationAtmosphere, string language)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Generate 3-4 combat moves for this round.\n");
        LanguageHint.AppendTo(sb, language, "all move names and descriptions MUST be in this language. JSON keys and type values stay English.");

        // Player state
        var weapon = player.Inventory.FirstOrDefault(i => i.Type == ItemType.Weapon);
        var weaponDesc = weapon is not null ? $"{weapon.Name} (+{weapon.EffectValue} atk)" : "bare hands";
        var armor = player.Inventory.FirstOrDefault(i => i.Type == ItemType.Armor);
        var armorDesc = armor is not null ? $"{armor.Name} (+{armor.EffectValue} def)" : "no armor";
        var hpPct = player.Health.Percentage;

        sb.AppendLine($"Player: {player.Name} | HP: {player.Health} ({hpPct}%) | " +
            $"EffAtk: {player.EffectiveAttack} | EffDef: {player.EffectiveDefense}");
        sb.AppendLine($"Weapon: {weaponDesc} | Armor: {armorDesc}");

        // Creature state
        var creatureHpPct = creature.Health.Percentage;
        sb.AppendLine($"\nCreature: {creature.Name} | HP: {creature.Health} ({creatureHpPct}%) | " +
            $"Atk: {creature.Attack} | Def: {creature.Defense} | Difficulty: {creature.Difficulty}");
        sb.AppendLine($"Description: {creature.Description}");
        if (!string.IsNullOrWhiteSpace(creature.Behavior))
            sb.AppendLine($"Combat behavior: {creature.Behavior}");

        sb.AppendLine($"\nLocation: {locationName}");
        if (!string.IsNullOrWhiteSpace(locationAtmosphere))
            sb.AppendLine($"Location atmosphere: {locationAtmosphere}");
        sb.AppendLine($"Round: {round}");

        // Potions check
        var potions = player.Inventory.Where(i => i.Type == ItemType.Potion).ToList();
        if (potions.Count > 0 && player.Health.Current < player.Health.Max)
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
        CombatRoundResult result, PlayerCharacter player, Creature creature, int round,
        string locationName, string locationAtmosphere, string language)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Narrate this combat round result dramatically in 2-4 sentences.\n");
        LanguageHint.AppendTo(sb, language, "narration MUST be in this language.");

        sb.AppendLine($"Round: {round}");
        sb.AppendLine($"Player: {player.Name} (HP: {player.Health})");
        sb.AppendLine($"Creature: {creature.Name} (HP: {creature.Health})");
        if (!string.IsNullOrWhiteSpace(creature.Behavior))
            sb.AppendLine($"Creature behavior: {creature.Behavior}");
        if (!string.IsNullOrWhiteSpace(locationName))
            sb.AppendLine($"Location: {locationName}");
        if (!string.IsNullOrWhiteSpace(locationAtmosphere))
            sb.AppendLine($"Atmosphere: {locationAtmosphere}");
        sb.AppendLine($"Move used: {result.MoveName} (type: {result.MoveType.ToString().ToLowerInvariant()})");

        switch (result.MoveType)
        {
            case MoveType.Attack:
            case MoveType.Heavy:
                sb.AppendLine($"Attack roll: {result.PlayerAttackRoll} (total: {result.PlayerAttackTotal}) — {(result.PlayerHit ? "HIT" : "MISS")}{(result.PlayerCrit ? " — CRITICAL!" : "")}");
                if (result.PlayerHit)
                    sb.AppendLine($"Player dealt {result.PlayerDamageDealt} damage to {creature.Name}.");
                if (result.SelfDamage > 0)
                    sb.AppendLine($"The reckless attack cost the player {result.SelfDamage} self-damage.");
                break;

            case MoveType.Defensive:
                sb.AppendLine($"Player took a defensive stance, halving incoming damage.");
                if (result.CounterHit)
                    sb.AppendLine($"Counter-attack connected for {result.CounterDamage} damage!");
                else
                    sb.AppendLine("No counter-attack connected.");
                break;

            case MoveType.Flee:
                sb.AppendLine($"Flee roll: {result.FleeRoll} — {(result.FledSuccessfully ? "ESCAPED!" : "FAILED!")}");
                break;

            case MoveType.Item:
                if (result.ItemUsed is not null)
                    sb.AppendLine($"Used {result.ItemUsed}, healing {result.HealAmount} HP.");
                break;
        }

        // Creature's counter-attack (for non-defensive, non-fled rounds)
        if (result.MoveType is not MoveType.Defensive && !result.FledSuccessfully)
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

    private static List<CombatMove> ParseMoves(string text, PlayerCharacter player, Creature creature, string language)
    {
        // Try to parse as JSON array
        var json = AgentHelper.ExtractJsonArray(text);
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
        return BuildFallbackMoves(player, creature, language);
    }

    private static List<CombatMove> BuildFallbackMoves(PlayerCharacter player, Creature creature, string language)
    {
        var moves = new List<CombatMove>
        {
            new()
            {
                Number = 1, Name = UIStrings.Get(language, "move_quick_strike"), Icon = "⚔️", Type = MoveType.Attack,
                Description = UIStrings.Format(language, "move_quick_strike_desc", creature.Name),
                AttackBonus = 1, DamageBonus = 0,
            },
            new()
            {
                Number = 2, Name = UIStrings.Get(language, "move_defensive"), Icon = "🛡️", Type = MoveType.Defensive,
                Description = UIStrings.Get(language, "move_defensive_desc"),
                DefenseBonus = 2, AttackBonus = 0,
            },
            new()
            {
                Number = 3, Name = UIStrings.Get(language, "move_disengage"), Icon = "🏃", Type = MoveType.Flee,
                Description = UIStrings.Get(language, "move_disengage_desc"),
            },
        };

        if (player.Inventory.Any(i => i.Type == ItemType.Potion) && player.Health.Current < player.Health.Max)
        {
            moves.Add(new CombatMove
            {
                Number = 4, Name = UIStrings.Get(language, "move_drink_potion"), Icon = "🧪", Type = MoveType.Item,
                Description = UIStrings.Get(language, "move_drink_potion_desc"),
            });
        }

        return moves;
    }

    private static string ParseNarrative(string text)
    {
        var json = AgentHelper.ExtractJson(text);
        if (json is null) return text.Trim(); // treat entire response as narrative

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.StrOrNull("narrative") ?? text.Trim();
        }
        catch { /* fall through */ }
        return text.Trim();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Console UI
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private static void PrintCombatStatus(PlayerCharacter player, Creature creature, string language)
    {
        GameConsoleUI.Write($"  {UIStrings.Format(language, "combat_you_hp", player.Health.Current, player.Health.Max)}", ConsoleColor.Green);
        Console.Write("  |  ");
        GameConsoleUI.WriteLine($"{UIStrings.Format(language, "combat_creature_hp", creature.Name, creature.Health.Current, creature.Health.Max)}", ConsoleColor.Red);
    }

    private static void PrintMoves(List<CombatMove> moves)
    {
        Console.WriteLine();
        foreach (var move in moves)
        {
            GameConsoleUI.Write($"  [{move.Number}] {move.Icon} {move.Name}", ConsoleColor.Yellow);

            // Show modifier hints
            var hints = new List<string>();
            if (move.AttackBonus != 0) hints.Add($"{(move.AttackBonus > 0 ? "+" : "")}{move.AttackBonus} hit");
            if (move.DamageBonus != 0) hints.Add($"{(move.DamageBonus > 0 ? "+" : "")}{move.DamageBonus} dmg");
            if (move.DefenseBonus != 0) hints.Add($"+{move.DefenseBonus} def");
            if (move.SelfDamage > 0) hints.Add($"{move.SelfDamage} self-dmg");

            if (hints.Count > 0)
                GameConsoleUI.Write($" ({string.Join(", ", hints)})", ConsoleColor.DarkGray);
            Console.WriteLine();

            GameConsoleUI.WriteLine($"      {move.Description}", ConsoleColor.DarkGray);
        }
        Console.WriteLine();
    }

    private static void PrintDiceResults(CombatRoundResult result, string creatureName, string language)
    {
        Console.WriteLine();

        switch (result.MoveType)
        {
            case MoveType.Attack:
            case MoveType.Heavy:
                var atkColor = result.PlayerHit ? ConsoleColor.Green : ConsoleColor.DarkGray;
                var atkLine = $"  {UIStrings.Format(language, "dice_attack", result.PlayerAttackRoll)}";
                if (result.PlayerAttackTotal != result.PlayerAttackRoll)
                    atkLine += $" ({(result.PlayerAttackTotal > result.PlayerAttackRoll ? "+" : "")}{result.PlayerAttackTotal - result.PlayerAttackRoll} = {result.PlayerAttackTotal})";
                atkLine += result.PlayerHit ? UIStrings.Get(language, "dice_hit") : UIStrings.Get(language, "dice_miss");
                if (result.PlayerCrit) atkLine += UIStrings.Get(language, "dice_crit");
                GameConsoleUI.WriteLine(atkLine, atkColor);
                if (result.PlayerHit)
                    GameConsoleUI.WriteLine($"  {UIStrings.Format(language, "dice_damage_dealt", result.PlayerDamageDealt)}", ConsoleColor.White);
                if (result.SelfDamage > 0)
                    GameConsoleUI.WriteLine($"  {UIStrings.Format(language, "dice_self_damage", result.SelfDamage)}", ConsoleColor.DarkYellow);
                break;

            case MoveType.Defensive:
                GameConsoleUI.WriteLine($"  {UIStrings.Get(language, "dice_defend")}", ConsoleColor.Cyan);
                if (result.CounterHit)
                    GameConsoleUI.WriteLine($"  {UIStrings.Format(language, "dice_counter", result.CounterDamage)}", ConsoleColor.Green);
                else
                    GameConsoleUI.WriteLine($"  {UIStrings.Format(language, "dice_counter_miss", result.CounterRoll)}", ConsoleColor.DarkGray);
                break;

            case MoveType.Flee:
                var fleeColor = result.FledSuccessfully ? ConsoleColor.Green : ConsoleColor.Red;
                GameConsoleUI.WriteLine($"  {UIStrings.Format(language, "dice_flee", result.FleeRoll)} {(result.FledSuccessfully ? UIStrings.Get(language, "dice_escaped") : UIStrings.Get(language, "dice_blocked"))}", fleeColor);
                break;

            case MoveType.Item:
                if (result.HealAmount > 0)
                    GameConsoleUI.WriteLine($"  {UIStrings.Format(language, "dice_potion", result.ItemUsed ?? "Potion", result.HealAmount)}", ConsoleColor.Green);
                break;
        }

        // Creature attack result (unless fled or defensive — defensive printed its own way)
        if (result.MoveType is not MoveType.Flee || !result.FledSuccessfully)
        {
            if (result.MoveType is not MoveType.Defensive) // defensive already shows reduced damage
            {
                var creatureColor = result.CreatureHit ? ConsoleColor.Red : ConsoleColor.DarkGray;
                var creatureLine = $"  {UIStrings.Format(language, "dice_creature_atk", creatureName, result.CreatureAttackRoll)}";
                creatureLine += result.CreatureHit ? UIStrings.Get(language, "dice_hit") : UIStrings.Get(language, "dice_miss");
                if (result.CreatureCrit) creatureLine += UIStrings.Get(language, "dice_crit");
                GameConsoleUI.WriteLine(creatureLine, creatureColor);
                if (result.CreatureHit)
                    GameConsoleUI.WriteLine($"  {UIStrings.Format(language, "dice_damage_taken", result.CreatureDamageTaken)}", ConsoleColor.Red);
            }
            else
            {
                // Show creature attack against defensive stance
                var defColor = result.CreatureHit ? ConsoleColor.DarkYellow : ConsoleColor.DarkGray;
                var defLine = $"  {UIStrings.Format(language, "dice_creature_atk", creatureName, result.CreatureAttackRoll)}";
                defLine += result.CreatureHit ? UIStrings.Format(language, "dice_damage_blocked", result.CreatureDamageTaken) : UIStrings.Get(language, "dice_miss");
                GameConsoleUI.WriteLine(defLine, defColor);
            }
        }
    }

    private static CombatMove? GetMoveChoice(List<CombatMove> moves, string language)
        => GameConsoleUI.PromptForChoice(
            moves, language,
            "combat_prompt", "combat_enter_num",
            ms => ms.FirstOrDefault(m => m.Type == MoveType.Flee) ?? ms[0],
            moves.Count);
}
