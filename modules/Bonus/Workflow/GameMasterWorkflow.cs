using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using RPGGameMaster.Models;
using RPGGameMaster.Tools;

namespace RPGGameMaster.Workflow;

/// <summary>
/// Core game loop: the Game Master LLM routes to sub-agents behind the scenes,
/// then presents the player with narrative + numbered options each turn.
/// Directly adapted from Module 09's Magentic-One pattern.
/// </summary>
internal static class GameMasterWorkflow
{
    private const int MaxInnerIterations = 6;

    public static async Task RunAsync(AgentConfig config, GameState state, CancellationToken ct = default)
    {
        var baseDir = AppContext.BaseDirectory;

        string LoadPrompt(string name)
        {
            var path = Path.Combine(baseDir, "assets", "prompts", $"{name}.md");
            return File.Exists(path) ? File.ReadAllText(path) : $"You are the {name} agent.";
        }

        // ── Create persistent agents ──
        var gmAgent = config.CreateAgent(LoadPrompt("game-master"));
        var worldArchitect = config.CreateAgent(LoadPrompt("world-architect"), tools: LocationTools.GetTools());
        var npcWeaver = config.CreateAgent(LoadPrompt("npc-weaver"), tools: NPCTools.GetTools());
        var creatureForger = config.CreateAgent(LoadPrompt("creature-forger"), tools: CreatureTools.GetTools());
        var combatNarrator = config.CreateAgent(LoadPrompt("combat-narrator"));
        var itemSage = config.CreateAgent(LoadPrompt("item-sage"), tools: ItemTools.GetTools());

        var agentMap = new Dictionary<string, AIAgent>(StringComparer.OrdinalIgnoreCase)
        {
            ["world_architect"] = worldArchitect,
            ["npc_weaver"] = npcWeaver,
            ["creature_forger"] = creatureForger,
            ["combat_narrator"] = combatNarrator,
            ["item_sage"] = itemSage,
        };

        // Set game state references for tool classes
        GameTools.SetGameState(state);
        ItemTools.SetGameState(state);

        // ── First turn: generate starting location ──
        if (state.Locations.Count == 0)
        {
            AgentHelper.PrintStatus("Generating your world...");
            await GenerateLocationForEntry(config, state, null, null, agentMap, LoadPrompt, ct);
        }

        // ── Main game loop ──
        var playerAction = "The adventure begins. You have just arrived.";

        while (true)
        {
            state.TurnCount++;

            // Build game state summary for the GM
            var stateSummary = BuildGameStateSummary(state);

            // ── Inner routing loop ──
            var turnContext = new List<string>
            {
                $"GAME STATE:\n{stateSummary}",
                $"PLAYER ACTION: {playerAction}",
            };

            PlayerPresentation? presentation = null;
            var innerIter = 0;

            while (innerIter < MaxInnerIterations)
            {
                innerIter++;

                var contextText = string.Join("\n\n---\n\n", turnContext);
                var gmPrompt = "Review the current game state and player's action. Decide what sub-agent work is needed, " +
                    "or output PRESENT_TO_PLAYER if ready.\n\n" +
                    $"{contextText}\n\n" +
                    "Respond with a JSON InnerDecision: {\"next_agent\": \"...\", \"reason\": \"...\", \"task\": \"...\"}. " +
                    "Use PRESENT_TO_PLAYER when all generation is done and you are ready to show the player their options.";

                if (innerIter >= MaxInnerIterations)
                    gmPrompt += "\n\nIMPORTANT: This is your last inner iteration. You MUST output PRESENT_TO_PLAYER now.";

                var decisionText = await AgentHelper.RunAgent(gmAgent, gmPrompt, ct,
                    "{\"next_agent\": \"PRESENT_TO_PLAYER\", \"reason\": \"Error occurred\", \"task\": \"Present current state\"}");
                var decision = ParseDecision(decisionText);

                if (decision.NextAgent.Equals("PRESENT_TO_PLAYER", StringComparison.OrdinalIgnoreCase))
                    break;

                // Route to sub-agent
                if (!agentMap.TryGetValue(decision.NextAgent, out var subAgent))
                {
                    AgentHelper.PrintWarning($"Unknown agent '{decision.NextAgent}', skipping.");
                    turnContext.Add($"SYSTEM: Unknown agent '{decision.NextAgent}' — skipped.");
                    continue;
                }

                AgentHelper.PrintSubAgentWork(decision.NextAgent, decision.Task);

                var langHint = state.Language != "English" ? $"Language: {state.Language} — all player-facing text MUST be in this language. JSON keys stay English.\n" : "";
                var subPrompt = $"World theme: {state.WorldTheme}\n{langHint}\n" +
                    $"Current game context:\n{string.Join("\n\n---\n\n", turnContext)}\n\n" +
                    $"Your task:\n{decision.Task}";

                // Inject rich generation context for NPC and creature generation agents
                if (decision.NextAgent.Equals("npc_weaver", StringComparison.OrdinalIgnoreCase) && state.CurrentLocation is not null)
                {
                    subPrompt = BuildNPCGenerationContext(state, state.CurrentLocation) + "\n\n" +
                        $"Your task:\n{decision.Task}\n\n" +
                        "Output ONLY the raw NPC JSON object, no markdown fences, no explanation.";
                }
                else if (decision.NextAgent.Equals("creature_forger", StringComparison.OrdinalIgnoreCase) && state.CurrentLocation is not null)
                {
                    var diff = state.Player.Level <= 2 ? "easy" : state.Player.Level <= 4 ? "medium" : "hard";
                    subPrompt = BuildCreatureGenerationContext(state, state.CurrentLocation, diff) + "\n\n" +
                        $"Your task:\n{decision.Task}\n\n" +
                        "Output ONLY the raw Creature JSON object, no markdown fences, no explanation.";
                }
                else if (decision.NextAgent.Equals("world_architect", StringComparison.OrdinalIgnoreCase))
                {
                    subPrompt = BuildLocationGenerationContext(state, state.CurrentLocationId, null) + "\n\n" +
                        $"Your task:\n{decision.Task}\n\n" +
                        "Output ONLY the raw Location JSON object, no markdown fences, no explanation.";
                }

                var subResponse = await AgentHelper.RunAgent(subAgent, subPrompt, ct);
                turnContext.Add($"{decision.NextAgent.ToUpper()} OUTPUT:\n{subResponse}");

                // Process sub-agent output — integrate into game state
                await ProcessSubAgentOutput(decision.NextAgent, subResponse, state, ct);
            }

            // ── Present to player ──
            var presentLangHint = state.Language != "English" ? $" The narrative and option descriptions MUST be in {state.Language}." : "";
            var presentPrompt = "Now present the current situation to the player. " +
                $"Output a PlayerPresentation JSON with a vivid narrative and 3-6 numbered options.{presentLangHint}\n\n" +
                $"Game state:\n{BuildGameStateSummary(state)}\n\n" +
                $"Turn context:\n{string.Join("\n\n---\n\n", turnContext)}\n\n" +
                "Remember: output ONLY the JSON {\"narrative\": \"...\", \"options\": [...]}";

            var presentText = await AgentHelper.RunAgent(gmAgent, presentPrompt, ct);
            presentation = ParsePresentation(presentText);

            if (presentation is null || presentation.Options.Count == 0)
            {
                // Fallback: build contextual options from current game state
                presentation = BuildFallbackPresentation(state);
            }

            // Display to player
            PrintNarrative(presentation.Narrative);
            PrintOptions(presentation.Options, state.Language);

            // Get player choice
            var choice = GetPlayerChoice(presentation.Options, state);
            if (choice is null) continue;

            state.AddLog($"Turn {state.TurnCount}: Player chose — {choice.Description}");

            // ── Handle player action ──
            playerAction = await HandlePlayerAction(choice, state, config, agentMap, LoadPrompt, ct);

            if (playerAction == "__QUIT__") break;

            // Check level up
            if (state.Player.TryLevelUp())
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n{UIStrings.Format(state.Language, "level_up", state.Player.Level)}");
                Console.WriteLine($"   {UIStrings.Format(state.Language, "level_stats", state.Player.HP, state.Player.MaxHP, state.Player.Attack, state.Player.Defense)}");
                Console.ResetColor();
                state.AddLog($"Player leveled up to {state.Player.Level}!");
            }

            // Check quest completion
            CheckQuestCompletion(state);

            // Auto-save after every turn
            AutoSave(state);
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Player action handling
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private static async Task<string> HandlePlayerAction(
        GameOption choice, GameState state, AgentConfig config,
        Dictionary<string, AIAgent> agentMap,
        Func<string, string> loadPrompt,
        CancellationToken ct)
    {
        switch (choice.ActionType.ToLowerInvariant())
        {
            case "move":
                return await HandleMove(choice, state, config, agentMap, loadPrompt, ct);

            case "talk":
                return await HandleTalk(choice, state, config, ct);

            case "fight":
                var fightResult = await HandleFight(choice, state, config, loadPrompt, ct);
                if (fightResult == "__GAME_OVER__")
                    return HandleDeath(state);
                return fightResult;

            case "pickup":
                return HandlePickup(choice, state);

            case "use_item":
                return await HandleUseItem(choice, state, agentMap, ct);

            case "rest":
                return HandleRest(state);

            case "look_around":
                return $"Player looks around the current location: {state.CurrentLocation?.Name ?? "unknown"}.";

            case "examine":
                return await HandleExamine(choice, state, agentMap, ct);

            case "check_quests":
                PrintQuests(state);
                return "Player reviewed their active quests.";

            case "inventory":
                return HandleInventory(state);

            case "map":
                return HandleMap(state);

            case "trade":
                return await HandleTrade(choice, state, config, ct);

            case "save_game":
                SaveGameToDisk(state);
                return "Player saved the game.";

            case "quit":
                SaveGameToDisk(state);
                return "__QUIT__";

            default:
                return $"Player chose: {choice.Description}";
        }
    }

    private static async Task<string> HandleMove(
        GameOption choice, GameState state, AgentConfig config,
        Dictionary<string, AIAgent> agentMap,
        Func<string, string> loadPrompt,
        CancellationToken ct)
    {
        var currentLoc = state.CurrentLocation;
        if (currentLoc is null) return "You're lost in the void.";

        var exit = currentLoc.Exits.FirstOrDefault(e =>
            e.Direction.Equals(choice.Target, StringComparison.OrdinalIgnoreCase));

        if (exit is null)
            return $"There is no exit in direction '{choice.Target}'.";

        if (string.IsNullOrWhiteSpace(exit.TargetLocationId))
        {
            // Unexplored — generate new location
            AgentHelper.PrintStatus($"Discovering what lies {exit.Direction}...");
            var newLoc = await GenerateLocationForEntry(
                config, state, currentLoc.Id, exit, agentMap, loadPrompt, ct);

            if (newLoc is not null)
            {
                exit.TargetLocationId = newLoc.Id;
            }
        }

        if (!string.IsNullOrWhiteSpace(exit.TargetLocationId) && state.Locations.TryGetValue(exit.TargetLocationId, out var targetLoc))
        {
            state.CurrentLocationId = targetLoc.Id;
            targetLoc.Visited = true;
            state.AddLog($"Moved to {targetLoc.Name} ({exit.Direction}).");
            return $"Player moved {exit.Direction} to {targetLoc.Name}.";
        }

        return $"Player tried to move {exit.Direction} but the path was blocked.";
    }

    private static async Task<string> HandleTalk(
        GameOption choice, GameState state, AgentConfig config, CancellationToken ct)
    {
        var npcId = choice.Target;
        if (!state.NPCs.TryGetValue(npcId, out var npc))
        {
            // Fuzzy match: try matching by name (GM may output name instead of ID)
            npc = state.NPCs.Values.FirstOrDefault(n =>
                n.Name.Contains(npcId, StringComparison.OrdinalIgnoreCase) ||
                npcId.Contains(n.Name, StringComparison.OrdinalIgnoreCase) ||
                n.Id.Contains(npcId, StringComparison.OrdinalIgnoreCase));

            // Also try matching NPCs at current location
            if (npc is null && state.CurrentLocation is not null)
            {
                npc = state.CurrentLocation.NPCIds
                    .Where(id => state.NPCs.ContainsKey(id))
                    .Select(id => state.NPCs[id])
                    .FirstOrDefault(n =>
                        n.Name.Contains(npcId, StringComparison.OrdinalIgnoreCase) ||
                        npcId.Contains(n.Name, StringComparison.OrdinalIgnoreCase));
            }

            if (npc is null)
                return $"NPC '{npcId}' not found.";
        }

        await DialogueWorkflow.RunAsync(config, state, npc, ct);
        return $"Player spoke with {npc.Name}.";
    }

    private static async Task<string> HandleFight(
        GameOption choice, GameState state, AgentConfig config,
        Func<string, string> loadPrompt, CancellationToken ct)
    {
        var creatureId = choice.Target;
        if (!state.Creatures.TryGetValue(creatureId, out var creature))
        {
            // Fuzzy match: try matching by name
            creature = state.Creatures.Values.FirstOrDefault(c =>
                c.Name.Contains(creatureId, StringComparison.OrdinalIgnoreCase) ||
                creatureId.Contains(c.Name, StringComparison.OrdinalIgnoreCase) ||
                c.Id.Contains(creatureId, StringComparison.OrdinalIgnoreCase));

            if (creature is null)
                return $"Creature '{creatureId}' not found.";
        }

        if (creature.IsDefeated)
            return $"{creature.Name} has already been defeated.";

        var result = await CombatWorkflow.RunAsync(state, creature, config, loadPrompt, ct);

        if (result == CombatResult.PlayerDefeated)
            return "__GAME_OVER__";

        // Boost disposition and mood for nearby NPCs after defeating a creature
        if (result == CombatResult.CreatureDefeated && state.CurrentLocation is not null)
        {
            foreach (var npcId in state.CurrentLocation.NPCIds)
            {
                if (state.NPCs.TryGetValue(npcId, out var nearbyNpc))
                {
                    nearbyNpc.DispositionTowardPlayer = Math.Min(100, nearbyNpc.DispositionTowardPlayer + 10);
                    if (nearbyNpc.Mood is "anxious" or "fearful" or "wary" or "suspicious")
                        nearbyNpc.Mood = "relieved";
                }
            }
        }

        return $"Combat ended: {result}";
    }

    private static string HandlePickup(GameOption choice, GameState state)
    {
        var loc = state.CurrentLocation;
        if (loc is null) return "Nothing to pick up.";

        var item = loc.Items.FirstOrDefault(i =>
            i.Name.Equals(choice.Target, StringComparison.OrdinalIgnoreCase));

        if (item is null)
            return $"Item '{choice.Target}' not found here.";

        loc.Items.Remove(item);
        state.Player.Inventory.Add(item);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n{UIStrings.Format(state.Language, "picked_up", item.Name, item.Description)}");
        Console.ResetColor();

        state.AddLog($"Picked up {item.Name}.");
        return $"Player picked up {item.Name}.";
    }

    private static async Task<string> HandleUseItem(
        GameOption choice, GameState state,
        Dictionary<string, AIAgent> agentMap,
        CancellationToken ct)
    {
        var item = state.Player.Inventory.FirstOrDefault(i =>
            i.Name.Equals(choice.Target, StringComparison.OrdinalIgnoreCase));

        // Fuzzy fallback
        item ??= state.Player.Inventory.FirstOrDefault(i =>
            i.Name.Contains(choice.Target, StringComparison.OrdinalIgnoreCase) ||
            choice.Target.Contains(i.Name, StringComparison.OrdinalIgnoreCase));

        if (item is null)
            return $"You don't have '{choice.Target}' in your inventory.";

        // Potions: direct handling (no LLM call needed for simple heal math)
        if (item.Type == "potion")
        {
            var healed = Math.Min(item.EffectValue, state.Player.MaxHP - state.Player.HP);
            state.Player.HP += healed;
            state.Player.Inventory.Remove(item);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n{UIStrings.Format(state.Language, "used_item", item.Name, healed, state.Player.HP, state.Player.MaxHP)}");
            Console.ResetColor();

            state.AddLog($"Used {item.Name}, healed {healed} HP.");
            return $"Player used {item.Name}, healed {healed} HP.";
        }

        // Non-usable items (weapons, armor without IsUsable flag)
        if (!item.IsUsable && item.Type is "weapon" or "armor")
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"\n  {UIStrings.Format(state.Language, "item_equipped", item.Name)}");
            Console.ResetColor();
            return $"{item.Name} is equipped passively.";
        }

        // Route to Item Sage for scrolls, food, keys, misc, and other usable items
        if (agentMap.TryGetValue("item_sage", out var itemSage))
        {
            AgentHelper.PrintStatus($"Using {item.Name}...");

            var usePrompt = $"The player wants to USE this item.\n\n" +
                $"Item: {JsonSerializer.Serialize(item, AgentHelper.JsonOpts)}\n" +
                $"Player HP: {state.Player.HP}/{state.Player.MaxHP}\n" +
                $"World theme: {state.WorldTheme}\n" +
                $"Location: {state.CurrentLocation?.Name ?? "unknown"}\n\n" +
                "Determine the effect and narrate what happens. Call ApplyItemEffect with the result.";

            var response = await AgentHelper.RunAgent(itemSage, usePrompt, ct,
                "{\"action\": \"use\", \"narrative\": \"Nothing happens.\", \"effect\": {\"type\": \"narrative_only\"}}");

            var narrative = ParseItemSageNarrative(response);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n  {narrative}");
            Console.ResetColor();

            state.AddLog($"Used {item.Name}.");
            return $"Player used {item.Name}.";
        }

        return $"You can't use {item.Name} right now.";
    }

    // ── Examine ──

    private static async Task<string> HandleExamine(
        GameOption choice, GameState state,
        Dictionary<string, AIAgent> agentMap,
        CancellationToken ct)
    {
        var item = state.Player.Inventory.FirstOrDefault(i =>
            i.Name.Equals(choice.Target, StringComparison.OrdinalIgnoreCase));

        item ??= state.Player.Inventory.FirstOrDefault(i =>
            i.Name.Contains(choice.Target, StringComparison.OrdinalIgnoreCase) ||
            choice.Target.Contains(i.Name, StringComparison.OrdinalIgnoreCase));

        // Also check location items
        if (item is null && state.CurrentLocation is not null)
        {
            item = state.CurrentLocation.Items.FirstOrDefault(i =>
                i.Name.Equals(choice.Target, StringComparison.OrdinalIgnoreCase));
            item ??= state.CurrentLocation.Items.FirstOrDefault(i =>
                i.Name.Contains(choice.Target, StringComparison.OrdinalIgnoreCase) ||
                choice.Target.Contains(i.Name, StringComparison.OrdinalIgnoreCase));
        }

        if (item is null)
            return $"Cannot find '{choice.Target}' to examine.";

        // If lore is already cached, display it directly
        if (!string.IsNullOrWhiteSpace(item.Lore))
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"\n  {UIStrings.Format(state.Language, "examine_header", item.Name)}");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  {item.Lore}");
            Console.ResetColor();
            return $"Player examined {item.Name} (cached lore).";
        }

        // Route to Item Sage for lore generation
        if (agentMap.TryGetValue("item_sage", out var itemSage))
        {
            AgentHelper.PrintStatus($"Examining {item.Name}...");

            var examinePrompt = $"The player wants to EXAMINE this item. Generate a rich lore description.\n\n" +
                $"Item: {JsonSerializer.Serialize(item, AgentHelper.JsonOpts)}\n" +
                $"World theme: {state.WorldTheme}\n" +
                $"Location: {state.CurrentLocation?.Name ?? "unknown"}\n\n" +
                "Generate lore and call SetItemLore to cache it.";

            var response = await AgentHelper.RunAgent(itemSage, examinePrompt, ct,
                "{\"action\": \"examine\", \"lore\": \"An unremarkable item.\"}");

            var lore = ParseItemSageLore(response);

            // Cache it ourselves as fallback if the agent didn't call SetItemLore
            if (string.IsNullOrWhiteSpace(item.Lore))
                item.Lore = lore;

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"\n  {UIStrings.Format(state.Language, "examine_header", item.Name)}");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  {lore}");
            Console.ResetColor();

            state.AddLog($"Examined {item.Name}.");
            return $"Player examined {item.Name}.";
        }

        // Fallback: show the basic description
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"\n  {item.Description}");
        Console.ResetColor();
        return $"Player examined {item.Name}.";
    }

    private static string ParseItemSageNarrative(string text)
    {
        var json = AgentHelper.ExtractJson(text);
        if (json is null) return text.Trim();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("narrative", out var n))
                return n.GetString() ?? text.Trim();
        }
        catch { /* fall through */ }
        return text.Trim();
    }

    private static string ParseItemSageLore(string text)
    {
        var json = AgentHelper.ExtractJson(text);
        if (json is null) return text.Trim();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("lore", out var l))
                return l.GetString() ?? text.Trim();
        }
        catch { /* fall through */ }
        return text.Trim();
    }

    private static string HandleRest(GameState state)
    {
        var healAmount = state.Player.MaxHP / 4;
        var healed = Math.Min(healAmount, state.Player.MaxHP - state.Player.HP);
        state.Player.HP += healed;

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n{UIStrings.Format(state.Language, "rest_healed", healed, state.Player.HP, state.Player.MaxHP)}");
        Console.ResetColor();

        state.AddLog($"Rested, healed {healed} HP.");
        return $"Player rested, healed {healed} HP.";
    }

    // ── Inventory ──

    private static string HandleInventory(GameState state)
    {
        var player = state.Player;
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(UIStrings.Get(state.Language, "inventory_header"));
        Console.ResetColor();

        if (player.Inventory.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"   {UIStrings.Get(state.Language, "inventory_empty")}");
            Console.ResetColor();
            return "Player checked inventory — nothing to do.";
        }

        for (var i = 0; i < player.Inventory.Count; i++)
        {
            var item = player.Inventory[i];
            var bonus = item.Type switch
            {
                "weapon" => UIStrings.Format(state.Language, "inv_atk_bonus", item.EffectValue),
                "armor" => UIStrings.Format(state.Language, "inv_def_bonus", item.EffectValue),
                "potion" => UIStrings.Format(state.Language, "inv_heals", item.EffectValue),
                _ => "",
            };
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"   [{i + 1}] {item.Name}");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($" — {item.Type}{bonus}");
            Console.ResetColor();
        }

        // Offer to use a potion
        var potions = player.Inventory.Where(i => i.Type == "potion").ToList();
        if (potions.Count > 0 && player.HP < player.MaxHP)
        {
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write($"\n   {UIStrings.Get(state.Language, "inventory_use_prompt")}");
            Console.ResetColor();

            var input = Console.ReadLine()?.Trim();
            if (int.TryParse(input, out var idx) && idx >= 1 && idx <= player.Inventory.Count)
            {
                var chosen = player.Inventory[idx - 1];
                if (chosen.Type == "potion")
                {
                    var healed = Math.Min(chosen.EffectValue, player.MaxHP - player.HP);
                    player.HP += healed;
                    player.Inventory.RemoveAt(idx - 1);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"   {UIStrings.Format(state.Language, "used_item", chosen.Name, healed, player.HP, player.MaxHP)}");
                    Console.ResetColor();
                    state.AddLog($"Used {chosen.Name}, healed {healed} HP.");
                    return $"Player used {chosen.Name}, healed {healed} HP.";
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"   {UIStrings.Format(state.Language, "item_equipped", chosen.Name)}");
                    Console.ResetColor();
                }
            }
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"\n   {UIStrings.Get(state.Language, "inventory_close")}");
            Console.ResetColor();
            Console.ReadLine();
        }

        return "Player reviewed their inventory.";
    }

    // ── Map ──

    private static string HandleMap(GameState state)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(UIStrings.Get(state.Language, "map_header"));
        Console.ResetColor();

        foreach (var loc in state.Locations.Values)
        {
            var isCurrent = loc.Id == state.CurrentLocationId;
            Console.ForegroundColor = isCurrent ? ConsoleColor.Cyan : ConsoleColor.White;
            Console.Write($"   {(isCurrent ? "➤ " : "  ")}{loc.Name}");
            Console.ForegroundColor = ConsoleColor.DarkGray;

            var tags = new List<string>();
            if (!string.IsNullOrWhiteSpace(loc.Type))
                tags.Add(loc.Type);
            if (!string.IsNullOrWhiteSpace(loc.DangerLevel))
                tags.Add(loc.DangerLevel);
            var tagStr = tags.Count > 0 ? $" [{string.Join(", ", tags)}]" : "";

            var exitNames = loc.Exits.Select(e =>
            {
                var explored = !string.IsNullOrEmpty(e.TargetLocationId) ? "✓" : "?";
                return $"{e.Direction}[{explored}]";
            });
            Console.WriteLine($"{tagStr} — exits: {string.Join(", ", exitNames)}");
            Console.ResetColor();
        }

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"\n   {UIStrings.Format(state.Language, "map_total", state.Locations.Count)}");
        Console.ResetColor();

        return "Player viewed the map.";
    }

    // ── Death / Respawn ──

    private static string HandleDeath(GameState state)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("╔══════════════════════════════════════════╗");
        Console.WriteLine($"║   {UIStrings.Get(state.Language, "death_header"),36}   ║");
        Console.WriteLine("╚══════════════════════════════════════════╝");
        Console.ResetColor();

        // Penalty: lose 50% gold, 25% XP
        var goldLost = state.Player.Gold / 2;
        var xpLost = state.Player.XP / 4;
        state.Player.Gold -= goldLost;
        state.Player.XP = Math.Max(0, state.Player.XP - xpLost);

        // Full HP restore
        state.Player.HP = state.Player.MaxHP;

        // Teleport to first discovered location (starting area)
        var startLoc = state.Locations.Values.FirstOrDefault();
        if (startLoc is not null)
            state.CurrentLocationId = startLoc.Id;

        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine($"\n  {UIStrings.Format(state.Language, "death_respawn", startLoc?.Name ?? "the starting area")}");
        Console.WriteLine($"  {UIStrings.Format(state.Language, "death_penalty", goldLost, xpLost)}");
        Console.WriteLine($"  {UIStrings.Format(state.Language, "death_restored", state.Player.HP, state.Player.MaxHP)}");
        Console.ResetColor();

        state.AddLog($"Fell in battle. Lost {goldLost}g and {xpLost}xp. Respawned at {startLoc?.Name ?? "start"}.");

        // Auto-save after death so penalties persist
        AutoSave(state);

        return $"Player was defeated but respawned at {startLoc?.Name ?? "the starting area"} with penalties.";
    }

    // ── NPC Trading ──

    private static async Task<string> HandleTrade(
        GameOption choice, GameState state, AgentConfig config, CancellationToken ct)
    {
        var npcId = choice.Target;
        NPC? npc = null;

        if (state.NPCs.TryGetValue(npcId, out npc)) { }
        else
        {
            npc = state.NPCs.Values.FirstOrDefault(n =>
                n.Name.Contains(npcId, StringComparison.OrdinalIgnoreCase) ||
                npcId.Contains(n.Name, StringComparison.OrdinalIgnoreCase));
        }

        if (npc is null)
            return $"Merchant '{npcId}' not found.";

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  {UIStrings.Format(state.Language, "trade_header", npc.Name)}");
        Console.ResetColor();

        // Create a merchant agent
        var merchantPrompt = $"You are {npc.Name}, a merchant. " +
            $"The player has {state.Player.Gold} gold. " +
            "Generate a shop inventory of 3-5 items the player can buy, priced fairly for their level.\n\n" +
            $"Player level: {state.Player.Level}\n" +
            $"World theme: {state.WorldTheme}\n\n" +
            "Output ONLY a JSON object:\n" +
            "{\"greeting\": \"...\", \"items\": [{\"name\": \"...\", \"description\": \"...\", \"type\": \"weapon|armor|potion|misc\", \"effect_value\": 0, \"price\": 0}, ...]}";

        var merchantAgent = config.CreateAgent($"You are a merchant NPC named {npc.Name}. {npc.Personality}");
        var shopResponse = await AgentHelper.RunAgent(merchantAgent, merchantPrompt, ct,
            "{\"greeting\": \"Welcome!\", \"items\": []}");

        var shopJson = AgentHelper.ExtractJson(shopResponse);
        List<ShopItem> shopItems = [];
        string greeting = "Welcome, adventurer!";

        if (shopJson is not null)
        {
            try
            {
                using var doc = JsonDocument.Parse(shopJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("greeting", out var g)) greeting = g.GetString() ?? greeting;
                if (root.TryGetProperty("items", out var arr))
                {
                    foreach (var el in arr.EnumerateArray())
                    {
                        shopItems.Add(new ShopItem
                        {
                            Name = el.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                            Description = el.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                            Type = el.TryGetProperty("type", out var t) ? t.GetString() ?? "misc" : "misc",
                            EffectValue = el.TryGetProperty("effect_value", out var ev) ? ev.GetInt32() : 0,
                            Price = el.TryGetProperty("price", out var p) ? p.GetInt32() : 10,
                        });
                    }
                }
            }
            catch { /* use defaults */ }
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n  {npc.Name}: \"{greeting}\"");
        Console.ResetColor();

        if (shopItems.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  {UIStrings.Get(state.Language, "trade_no_items")}");
            Console.ResetColor();
            return $"Attempted to trade with {npc.Name} but no items were available.";
        }

        // Show shop
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  {UIStrings.Format(state.Language, "trade_gold", state.Player.Gold)}");
        Console.ResetColor();
        Console.WriteLine();

        for (var i = 0; i < shopItems.Count; i++)
        {
            var item = shopItems[i];
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"  [{i + 1}] {item.Name}");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($" — {item.Description} ({item.Type})");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"  {item.Price}g");
            Console.ResetColor();
        }

        // Sell option
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"  [S] ");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(UIStrings.Get(state.Language, "trade_sell"));
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"  [0] ");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(UIStrings.Get(state.Language, "trade_leave"));
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"\n  {UIStrings.Get(state.Language, "trade_choice")}");
        Console.ResetColor();
        var input = Console.ReadLine()?.Trim();

        if (string.Equals(input, "s", StringComparison.OrdinalIgnoreCase) && state.Player.Inventory.Count > 0)
        {
            // Sell
            Console.WriteLine();
            for (var i = 0; i < state.Player.Inventory.Count; i++)
            {
                var inv = state.Player.Inventory[i];
                var sellPrice = Math.Max(1, (inv.Type == "weapon" || inv.Type == "armor" ? inv.EffectValue * 3 : inv.EffectValue));
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"  [{i + 1}] {inv.Name}");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"{UIStrings.Format(state.Language, "trade_sells_for", sellPrice)}");
                Console.ResetColor();
            }
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"  {UIStrings.Get(state.Language, "trade_sell_prompt")}");
            Console.ResetColor();
            var sellInput = Console.ReadLine()?.Trim();
            if (int.TryParse(sellInput, out var si) && si >= 1 && si <= state.Player.Inventory.Count)
            {
                var sold = state.Player.Inventory[si - 1];
                var price = Math.Max(1, (sold.Type == "weapon" || sold.Type == "armor" ? sold.EffectValue * 3 : sold.EffectValue));
                state.Player.Inventory.RemoveAt(si - 1);
                state.Player.Gold += price;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  {UIStrings.Format(state.Language, "trade_sold", sold.Name, price, state.Player.Gold)}");
                Console.ResetColor();
                state.AddLog($"Sold {sold.Name} for {price}g.");
                return $"Player sold {sold.Name} for {price} gold.";
            }
        }
        else if (int.TryParse(input, out var buyIdx) && buyIdx >= 1 && buyIdx <= shopItems.Count)
        {
            var toBuy = shopItems[buyIdx - 1];
            if (state.Player.Gold >= toBuy.Price)
            {
                state.Player.Gold -= toBuy.Price;
                state.Player.Inventory.Add(new Item
                {
                    Name = toBuy.Name,
                    Description = toBuy.Description,
                    Type = toBuy.Type,
                    EffectValue = toBuy.EffectValue,
                });
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  {UIStrings.Format(state.Language, "trade_bought", toBuy.Name, toBuy.Price, state.Player.Gold)}");
                Console.ResetColor();
                state.AddLog($"Bought {toBuy.Name} for {toBuy.Price}g.");
                return $"Player bought {toBuy.Name} for {toBuy.Price} gold.";
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  {UIStrings.Format(state.Language, "trade_no_gold", toBuy.Price, state.Player.Gold)}");
                Console.ResetColor();
            }
        }

        return $"Player browsed {npc.Name}'s shop.";
    }

    /// <summary>Temporary DTO for shop items with price.</summary>
    private sealed class ShopItem
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Type { get; set; } = "misc";
        public int EffectValue { get; set; }
        public int Price { get; set; }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Generation context builders
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    /// <summary>
    /// Builds a rich context block for location generation so the World Architect can
    /// avoid duplicates, match the world progression, and connect to existing lore.
    /// </summary>
    private static string BuildLocationGenerationContext(
        GameState state, string? fromLocationId, Exit? entryExit)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## GENERATION CONTEXT\n");

        // World & player
        sb.AppendLine($"World theme: {state.WorldTheme}");
        if (state.Language != "English")
            sb.AppendLine($"Language: {state.Language} — all player-facing text MUST be in this language. JSON keys stay English.");
        sb.AppendLine($"Player: {state.Player.Name} | Level {state.Player.Level} | HP {state.Player.HP}/{state.Player.MaxHP} | Gold {state.Player.Gold}");
        sb.AppendLine($"Locations explored: {state.Locations.Count}");
        sb.AppendLine();

        // Entry hint
        if (entryExit is not null)
            sb.AppendLine($"The player entered via an exit described as: \"{entryExit.Description}\". The new location should match that description.");
        else
            sb.AppendLine("Generate a suitable starting location for this adventure.");

        // Back reference
        if (fromLocationId is not null)
            sb.AppendLine($"Include an exit with direction 'Back' and target_location_id '{fromLocationId}' (the way the player came from).");
        else
            sb.AppendLine("This is the starting location — no back exit needed.");
        sb.AppendLine();

        // Source location context (what the player is leaving)
        if (fromLocationId is not null && state.Locations.TryGetValue(fromLocationId, out var fromLoc))
        {
            sb.AppendLine("Leaving from:");
            sb.AppendLine($"  Name: {fromLoc.Name}");
            if (!string.IsNullOrWhiteSpace(fromLoc.Type))
                sb.AppendLine($"  Type: {fromLoc.Type}");
            if (!string.IsNullOrWhiteSpace(fromLoc.Atmosphere))
                sb.AppendLine($"  Atmosphere: {fromLoc.Atmosphere}");
            if (!string.IsNullOrWhiteSpace(fromLoc.DangerLevel))
                sb.AppendLine($"  Danger: {fromLoc.DangerLevel}");
            sb.AppendLine();
        }

        // All existing locations (dedup + world awareness)
        if (state.Locations.Count > 0)
        {
            sb.AppendLine("Existing locations in the world (DO NOT duplicate names or repeat the same type too often):");
            foreach (var loc in state.Locations.Values)
            {
                var typeTag = !string.IsNullOrWhiteSpace(loc.Type) ? $" [{loc.Type}]" : "";
                var atmoTag = !string.IsNullOrWhiteSpace(loc.Atmosphere) ? $" ({loc.Atmosphere})" : "";
                sb.AppendLine($"  - {loc.Name}{typeTag}{atmoTag}");
            }

            // Flat type dedup signal
            var usedTypes = state.Locations.Values
                .Select(l => l.Type)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .ToList();
            if (usedTypes.Count > 0)
                sb.AppendLine($"Types already used (prefer variety): {string.Join(", ", usedTypes)}");
            sb.AppendLine();
        }

        // Active quests (quest-aware placement)
        var activeQuests = state.Player.ActiveQuests.Where(q => !q.IsComplete).ToList();
        if (activeQuests.Count > 0)
        {
            sb.AppendLine("Player's active quests (new location MAY serve as a quest destination if it fits naturally):");
            foreach (var q in activeQuests)
                sb.AppendLine($"  - {q.Title} ({q.Type})");
            sb.AppendLine();
        }

        // Danger progression guidance
        sb.AppendLine($"Danger level guidance for Level {state.Player.Level}:");
        if (state.Player.Level <= 2)
            sb.AppendLine("  Early game — prefer safe or moderate. Dangerous locations should be rare.");
        else if (state.Player.Level <= 4)
            sb.AppendLine("  Mid game — mix of moderate and dangerous. Occasional safe havens.");
        else
            sb.AppendLine("  Late game — dangerous and deadly become common. Safe locations are rare refuges.");
        sb.AppendLine();

        // Recent events (tonal continuity)
        if (state.GameLog.Count > 0)
        {
            sb.AppendLine("Recent events:");
            foreach (var entry in state.GameLog.TakeLast(6))
                sb.AppendLine($"  - {entry}");
        }

        sb.AppendLine("\nGenerate a new location. Output ONLY the raw Location JSON object, no markdown fences, no explanation.");
        return sb.ToString();
    }

    /// <summary>
    /// Builds a rich context block for NPC generation so the NPC Weaver can avoid
    /// duplicates, reference the world state, and scale rewards to the player's level.
    /// </summary>
    private static string BuildNPCGenerationContext(GameState state, Location location)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## GENERATION CONTEXT\n");

        // World & player
        sb.AppendLine($"World theme: {state.WorldTheme}");
        if (state.Language != "English")
            sb.AppendLine($"Language: {state.Language} — all player-facing text MUST be in this language. JSON keys stay English.");
        sb.AppendLine($"Player: {state.Player.Name} | Level {state.Player.Level} | HP {state.Player.HP}/{state.Player.MaxHP} | Gold {state.Player.Gold}");
        sb.AppendLine($"Locations explored: {state.Locations.Count}");
        sb.AppendLine();

        // Target location
        sb.AppendLine($"Location: {location.Name}");
        sb.AppendLine($"Description: {location.Description}");
        sb.AppendLine($"Location id: {location.Id}");
        if (!string.IsNullOrWhiteSpace(location.Type))
            sb.AppendLine($"Location type: {location.Type}");
        if (!string.IsNullOrWhiteSpace(location.Atmosphere))
            sb.AppendLine($"Atmosphere: {location.Atmosphere}");
        if (!string.IsNullOrWhiteSpace(location.DangerLevel))
            sb.AppendLine($"Danger level: {location.DangerLevel}");
        if (!string.IsNullOrWhiteSpace(location.Lore))
            sb.AppendLine($"Location lore: {location.Lore}");
        sb.AppendLine();

        // Existing NPCs at this location (dedup signal)
        var npcsHere = location.NPCIds
            .Where(id => state.NPCs.ContainsKey(id))
            .Select(id => state.NPCs[id])
            .ToList();
        if (npcsHere.Count > 0)
        {
            sb.AppendLine("NPCs already at this location (DO NOT duplicate these archetypes):");
            foreach (var n in npcsHere)
                sb.AppendLine($"  - {n.Name} ({n.Occupation}, mood: {n.Mood})");
            sb.AppendLine();
        }

        // Global occupation dedup
        var allOccupations = state.NPCs.Values.Select(n => n.Occupation).Where(o => !string.IsNullOrWhiteSpace(o)).Distinct().ToList();
        if (allOccupations.Count > 0)
            sb.AppendLine($"Occupations already used in this world (pick a DIFFERENT one): {string.Join(", ", allOccupations)}");

        // Global name dedup
        var allNames = state.NPCs.Values.Select(n => n.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
        if (allNames.Count > 0)
            sb.AppendLine($"Names already used (pick a DIFFERENT one): {string.Join(", ", allNames)}");
        sb.AppendLine();

        // Creatures at this location (NPC can reference/warn)
        var creaturesHere = location.CreatureIds
            .Where(id => state.Creatures.ContainsKey(id))
            .Select(id => state.Creatures[id])
            .Where(c => !c.IsDefeated)
            .ToList();
        if (creaturesHere.Count > 0)
        {
            sb.AppendLine("Creatures at this location (NPC should be aware of these):");
            foreach (var c in creaturesHere)
                sb.AppendLine($"  - {c.Name} ({c.Difficulty}) — {c.Lore}");
            sb.AppendLine();
        }

        // Active & completed quests
        var activeQuests = state.Player.ActiveQuests.Where(q => !q.IsComplete).ToList();
        var completedQuests = state.Player.ActiveQuests.Where(q => q.IsComplete).ToList();
        if (activeQuests.Count > 0)
            sb.AppendLine($"Player's active quests: {string.Join(", ", activeQuests.Select(q => $"{q.Title} ({q.Type})"))}");
        if (completedQuests.Count > 0)
            sb.AppendLine($"Completed quests: {completedQuests.Count}");

        // Recent events
        if (state.GameLog.Count > 0)
        {
            sb.AppendLine("\nRecent events in the world:");
            foreach (var entry in state.GameLog.TakeLast(8))
                sb.AppendLine($"  - {entry}");
        }

        // Reward scaling guidance
        sb.AppendLine($"\nQuest reward guidance for Level {state.Player.Level}:");
        sb.AppendLine($"  Gold: {10 + state.Player.Level * 10}-{20 + state.Player.Level * 20}");
        sb.AppendLine($"  XP: {20 + state.Player.Level * 15}-{40 + state.Player.Level * 25}");

        return sb.ToString();
    }

    /// <summary>
    /// Builds a rich context block for creature generation so the Creature Forger can
    /// avoid duplicates, match the location, and scale to the player's actual stats.
    /// </summary>
    private static string BuildCreatureGenerationContext(GameState state, Location location, string difficulty)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## GENERATION CONTEXT\n");

        // World & player stats
        sb.AppendLine($"World theme: {state.WorldTheme}");
        if (state.Language != "English")
            sb.AppendLine($"Language: {state.Language} — all player-facing text MUST be in this language. JSON keys stay English.");
        sb.AppendLine($"Player: {state.Player.Name} | Level {state.Player.Level} | HP {state.Player.HP}/{state.Player.MaxHP} | " +
            $"EffAtk {state.Player.EffectiveAttack} | EffDef {state.Player.EffectiveDefense}");
        sb.AppendLine();

        // Target location
        sb.AppendLine($"Location: {location.Name}");
        sb.AppendLine($"Description: {location.Description}");
        sb.AppendLine($"Location id: {location.Id}");
        sb.AppendLine($"Difficulty: {difficulty}");
        if (!string.IsNullOrWhiteSpace(location.DangerLevel))
            sb.AppendLine($"Location danger level: {location.DangerLevel}");
        if (!string.IsNullOrWhiteSpace(location.Atmosphere))
            sb.AppendLine($"Location atmosphere: {location.Atmosphere}");
        sb.AppendLine();

        // Existing creatures (global dedup)
        var existingCreatures = state.Creatures.Values.ToList();
        if (existingCreatures.Count > 0)
        {
            var active = existingCreatures.Where(c => !c.IsDefeated).Select(c => c.Name).Distinct().ToList();
            var defeated = existingCreatures.Where(c => c.IsDefeated).Select(c => c.Name).Distinct().ToList();
            if (active.Count > 0)
                sb.AppendLine($"Active creatures in world (avoid duplicating): {string.Join(", ", active)}");
            if (defeated.Count > 0)
                sb.AppendLine($"Recent kills (new creature can be related — a pack member, scavenger, or escalation): {string.Join(", ", defeated)}");
            sb.AppendLine();
        }

        // NPCs at this location (creature lore can reference them)
        var npcsHere = location.NPCIds
            .Where(id => state.NPCs.ContainsKey(id))
            .Select(id => state.NPCs[id])
            .ToList();
        if (npcsHere.Count > 0)
        {
            sb.AppendLine("NPCs at this location (creature lore can reference their warnings or stories):");
            foreach (var n in npcsHere)
                sb.AppendLine($"  - {n.Name} ({n.Occupation})");
            sb.AppendLine();
        }

        // Active defeat quests (opportunity to spawn quest target)
        var defeatQuests = state.Player.ActiveQuests
            .Where(q => !q.IsComplete && q.Type.Equals("defeat", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (defeatQuests.Count > 0)
        {
            sb.AppendLine("Active DEFEAT quests (you MAY generate the target creature if it fits this location):");
            foreach (var q in defeatQuests)
                sb.AppendLine($"  - Quest: {q.Title} — target: {q.TargetId}");
            sb.AppendLine();
        }

        // Recent events
        if (state.GameLog.Count > 0)
        {
            sb.AppendLine("Recent events:");
            foreach (var entry in state.GameLog.TakeLast(5))
                sb.AppendLine($"  - {entry}");
        }

        return sb.ToString();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Location generation (triggers NPC/creature gen)
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private static async Task<Location?> GenerateLocationForEntry(
        AgentConfig config, GameState state,
        string? fromLocationId, Exit? entryExit,
        Dictionary<string, AIAgent> agentMap,
        Func<string, string> loadPrompt,
        CancellationToken ct)
    {
        // Use a dedicated agent WITHOUT tools to get clean JSON output
        var architectPrompt = loadPrompt("world-architect");
        var architect = config.CreateAgent(architectPrompt);

        var prompt = BuildLocationGenerationContext(state, fromLocationId, entryExit);

        AgentHelper.PrintSubAgentWork("world_architect", "Generating location...");
        var locResponse = await AgentHelper.RunAgent(architect, prompt, ct);
        var newLoc = AgentHelper.ParseJson<Location>(locResponse);

        if (newLoc is null || string.IsNullOrWhiteSpace(newLoc.Id))
        {
            AgentHelper.PrintWarning("Failed to generate location.");
            return null;
        }

        newLoc.Visited = true;
        state.Locations[newLoc.Id] = newLoc;
        state.CurrentLocationId = newLoc.Id;
        state.AddLog($"Discovered new location: {newLoc.Name}");

        // DangerLevel-driven spawn probabilities
        var (npcChance, creatureChance) = newLoc.DangerLevel.ToLowerInvariant() switch
        {
            "safe" => (0.90, 0.10),
            "moderate" => (0.60, 0.40),
            "dangerous" => (0.30, 0.70),
            "deadly" => (0.15, 0.90),
            _ => (0.60, 0.40),
        };

        // Generate NPCs (probability driven by danger level; always for starting location)
        var shouldGenNPC = fromLocationId is null || Random.Shared.NextDouble() < npcChance;
        if (shouldGenNPC)
        {
            // Use a tool-free NPC agent for clean JSON
            var npcWeaverPrompt = loadPrompt("npc-weaver");
            var npcWeaver = config.CreateAgent(npcWeaverPrompt);
            var npcCount = fromLocationId is null ? 2 : (Random.Shared.NextDouble() < 0.4 ? 2 : 1);

            for (var i = 0; i < npcCount; i++)
            {
                AgentHelper.PrintSubAgentWork("npc_weaver", $"Creating NPC {i + 1}...");
                var npcContext = BuildNPCGenerationContext(state, newLoc);
                var npcPrompt = npcContext + "\n\n" +
                    "Generate a unique NPC for this location. Output ONLY the raw NPC JSON object, no markdown fences, no explanation.";

                var npcResponse = await AgentHelper.RunAgent(npcWeaver, npcPrompt, ct);
                var npc = AgentHelper.ParseJson<NPC>(npcResponse);

                if (npc is not null && !string.IsNullOrWhiteSpace(npc.Id))
                {
                    npc.LocationId = newLoc.Id;
                    state.NPCs[npc.Id] = npc;
                    newLoc.NPCIds.Add(npc.Id);
                    state.AddLog($"Met {npc.Name} at {newLoc.Name}.");
                }
            }
        }

        // Generate creatures (probability driven by danger level; never for starting location)
        if (fromLocationId is not null && Random.Shared.NextDouble() < creatureChance)
        {
            var creaturePromptText = loadPrompt("creature-forger");
            var forge = config.CreateAgent(creaturePromptText);
            AgentHelper.PrintSubAgentWork("creature_forger", "Spawning creature...");

            var difficulty = state.Player.Level <= 2 ? "easy" : state.Player.Level <= 4 ? "medium" : "hard";
            var creatureContext = BuildCreatureGenerationContext(state, newLoc, difficulty);
            var creaturePrompt = creatureContext + "\n\n" +
                "Generate a creature for this location. Output ONLY the raw Creature JSON object, no markdown fences, no explanation.";

            var creatureResponse = await AgentHelper.RunAgent(forge, creaturePrompt, ct);
            var creature = AgentHelper.ParseJson<Creature>(creatureResponse);

            if (creature is not null && !string.IsNullOrWhiteSpace(creature.Id))
            {
                creature.LocationId = newLoc.Id;
                state.Creatures[creature.Id] = creature;
                newLoc.CreatureIds.Add(creature.Id);
                state.AddLog($"A {creature.Name} lurks at {newLoc.Name}.");
            }
        }

        return newLoc;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Parsing helpers
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private static GameMasterDecision ParseDecision(string text)
    {
        var json = AgentHelper.ExtractJson(text);
        if (json is null) return new GameMasterDecision { NextAgent = "PRESENT_TO_PLAYER", Task = text };
        try
        {
            return JsonSerializer.Deserialize<GameMasterDecision>(json, AgentHelper.JsonOpts)
                ?? new GameMasterDecision { NextAgent = "PRESENT_TO_PLAYER", Task = text };
        }
        catch
        {
            return new GameMasterDecision { NextAgent = "PRESENT_TO_PLAYER", Task = text };
        }
    }

    private static PlayerPresentation? ParsePresentation(string text)
    {
        var json = AgentHelper.ExtractJson(text);
        if (json is null) return null;
        try { return JsonSerializer.Deserialize<PlayerPresentation>(json, AgentHelper.JsonOpts); }
        catch { return null; }
    }

    private static Task ProcessSubAgentOutput(string agentName, string response, GameState state, CancellationToken ct)
    {
        try
        {
            switch (agentName.ToLowerInvariant())
            {
                case "world_architect":
                    var loc = AgentHelper.ParseJson<Location>(response);
                    if (loc is not null && !string.IsNullOrWhiteSpace(loc.Id))
                    {
                        loc.Visited = true;
                        state.Locations[loc.Id] = loc;
                        if (string.IsNullOrWhiteSpace(state.CurrentLocationId))
                            state.CurrentLocationId = loc.Id;
                        state.AddLog($"Discovered new location: {loc.Name}");
                    }
                    break;

                case "npc_weaver":
                    var npc = AgentHelper.ParseJson<NPC>(response);
                    if (npc is not null && !string.IsNullOrWhiteSpace(npc.Id))
                    {
                        state.NPCs[npc.Id] = npc;
                        // Add to current location's NPC list if not already there
                        var currentLoc = state.CurrentLocation;
                        if (currentLoc is not null && !currentLoc.NPCIds.Contains(npc.Id))
                        {
                            npc.LocationId = currentLoc.Id;
                            currentLoc.NPCIds.Add(npc.Id);
                        }
                        state.AddLog($"Met {npc.Name}.");
                    }
                    break;

                case "creature_forger":
                    var creature = AgentHelper.ParseJson<Creature>(response);
                    if (creature is not null && !string.IsNullOrWhiteSpace(creature.Id))
                    {
                        state.Creatures[creature.Id] = creature;
                        var curLoc = state.CurrentLocation;
                        if (curLoc is not null && !curLoc.CreatureIds.Contains(creature.Id))
                        {
                            creature.LocationId = curLoc.Id;
                            curLoc.CreatureIds.Add(creature.Id);
                        }
                        state.AddLog($"A {creature.Name} lurks nearby.");
                    }
                    break;
            }
        }
        catch
        {
            // Parsing failure — not critical, game continues
        }

        return Task.CompletedTask;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Quest completion
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private static void CheckQuestCompletion(GameState state)
    {
        foreach (var quest in state.Player.ActiveQuests.Where(q => !q.IsComplete))
        {
            var completed = quest.Type.ToLowerInvariant() switch
            {
                "defeat" => state.Creatures.TryGetValue(quest.TargetId, out var c) && c.IsDefeated,
                "fetch" => state.Player.Inventory.Any(i =>
                    i.Name.Equals(quest.TargetId, StringComparison.OrdinalIgnoreCase)),
                "explore" => state.Locations.TryGetValue(quest.TargetId, out var loc) && loc.Visited,
                _ => false,
            };

            if (!completed) continue;

            quest.IsComplete = true;

            // Award rewards
            state.Player.Gold += quest.RewardGold;
            state.Player.XP += quest.RewardXP;
            if (quest.RewardItem is not null)
                state.Player.Inventory.Add(quest.RewardItem);

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  {UIStrings.Format(state.Language, "quest_complete", quest.Title)}");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Yellow;
            if (quest.RewardGold > 0) Console.WriteLine($"     {UIStrings.Format(state.Language, "quest_gold", quest.RewardGold)}");
            if (quest.RewardXP > 0) Console.WriteLine($"     {UIStrings.Format(state.Language, "quest_xp", quest.RewardXP)}");
            if (quest.RewardItem is not null) Console.WriteLine($"     {UIStrings.Format(state.Language, "quest_item", quest.RewardItem.Name)}");
            Console.ResetColor();

            state.AddLog($"Quest '{quest.Title}' completed! (+{quest.RewardGold}g, +{quest.RewardXP}xp)");

            // Boost quest giver's disposition and mood
            if (!string.IsNullOrWhiteSpace(quest.GiverNPCId) && state.NPCs.TryGetValue(quest.GiverNPCId, out var giver))
            {
                giver.DispositionTowardPlayer = Math.Min(100, giver.DispositionTowardPlayer + 20);
                giver.Mood = "grateful";
            }
        }
    }

    private static string BuildGameStateSummary(GameState state)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"World Theme: {state.WorldTheme}");
        sb.AppendLine($"Turn: {state.TurnCount}");
        sb.AppendLine($"Player: {state.Player.Name} | HP: {state.Player.HP}/{state.Player.MaxHP} | " +
            $"Atk: {state.Player.EffectiveAttack} | Def: {state.Player.EffectiveDefense} | " +
            $"Level: {state.Player.Level} | Gold: {state.Player.Gold} | XP: {state.Player.XP}/{state.Player.XPToNextLevel}");

        if (state.Player.Inventory.Count > 0)
            sb.AppendLine($"Inventory: {string.Join(", ", state.Player.Inventory.Select(i => $"{i.Name} ({i.Type})"))}");

        if (state.Player.ActiveQuests.Count > 0)
            sb.AppendLine($"Active Quests: {string.Join(", ", state.Player.ActiveQuests.Where(q => !q.IsComplete).Select(q => q.Title))}");

        var loc = state.CurrentLocation;
        if (loc is not null)
        {
            sb.AppendLine($"\nCurrent Location: {loc.Name}");
            if (!string.IsNullOrWhiteSpace(loc.Type))
                sb.AppendLine($"Type: {loc.Type}");
            if (!string.IsNullOrWhiteSpace(loc.Atmosphere))
                sb.AppendLine($"Atmosphere: {loc.Atmosphere}");
            if (!string.IsNullOrWhiteSpace(loc.DangerLevel))
                sb.AppendLine($"Danger level: {loc.DangerLevel}");
            sb.AppendLine($"Description: {loc.Description}");
            if (!string.IsNullOrWhiteSpace(loc.Lore))
                sb.AppendLine($"Lore: {loc.Lore}");
            if (loc.PointsOfInterest.Count > 0)
                sb.AppendLine($"Points of interest: {string.Join("; ", loc.PointsOfInterest)}");
            sb.AppendLine($"Exits: {string.Join(", ", loc.Exits.Select(e => $"{e.Direction} ({(string.IsNullOrEmpty(e.TargetLocationId) ? "unexplored" : "visited")})"))}");

            var npcsHere = loc.NPCIds
                .Where(id => state.NPCs.ContainsKey(id))
                .Select(id => state.NPCs[id])
                .ToList();
            if (npcsHere.Count > 0)
                sb.AppendLine($"NPCs here: {string.Join(", ", npcsHere.Select(n => $"{n.Name} (id: {n.Id}, {n.Occupation}, {n.SpeakingStyle}, mood: {n.Mood}, disposition: {n.DispositionTowardPlayer})"))}");

            var creaturesHere = loc.CreatureIds
                .Where(id => state.Creatures.ContainsKey(id))
                .Select(id => state.Creatures[id])
                .Where(c => !c.IsDefeated)
                .ToList();
            if (creaturesHere.Count > 0)
                sb.AppendLine($"Creatures here: {string.Join(", ", creaturesHere.Select(c => $"{c.Name} (id: {c.Id}, {c.Difficulty}, {c.Behavior})"))}");

            if (loc.Items.Count > 0)
                sb.AppendLine($"Items on ground: {string.Join(", ", loc.Items.Select(i => i.Name))}");
        }

        if (state.GameLog.Count > 0)
        {
            sb.AppendLine($"\nRecent events:");
            foreach (var entry in state.GameLog.TakeLast(10))
                sb.AppendLine($"  - {entry}");
        }

        return sb.ToString();
    }

    private static PlayerPresentation BuildFallbackPresentation(GameState state)
    {
        var loc = state.CurrentLocation;
        var options = new List<GameOption>();
        var n = 1;

        // Exits
        if (loc is not null)
        {
            foreach (var exit in loc.Exits)
                options.Add(new() { Number = n++, Description = $"Go {exit.Direction} — {exit.Description}", ActionType = "move", Target = exit.Direction });

            // NPCs
            foreach (var npcId in loc.NPCIds)
            {
                if (state.NPCs.TryGetValue(npcId, out var npc))
                    options.Add(new() { Number = n++, Description = $"Talk to {npc.Name} ({npc.Occupation})", ActionType = "talk", Target = npc.Id });
            }

            // Creatures
            foreach (var cId in loc.CreatureIds)
            {
                if (state.Creatures.TryGetValue(cId, out var creature) && !creature.IsDefeated)
                    options.Add(new() { Number = n++, Description = $"Fight {creature.Name}", ActionType = "fight", Target = creature.Id });
            }

            // Items on ground
            foreach (var item in loc.Items)
                options.Add(new() { Number = n++, Description = $"Pick up {item.Name}", ActionType = "pickup", Target = item.Name });
        }

        options.Add(new() { Number = n++, Description = UIStrings.Get(state.Language, "opt_look_around"), ActionType = "look_around" });
        if (state.Player.Inventory.Count > 0)
            options.Add(new() { Number = n++, Description = UIStrings.Get(state.Language, "opt_inventory"), ActionType = "inventory" });
        options.Add(new() { Number = n++, Description = UIStrings.Get(state.Language, "opt_quests"), ActionType = "check_quests" });
        options.Add(new() { Number = n++, Description = UIStrings.Get(state.Language, "opt_map"), ActionType = "map" });
        options.Add(new() { Number = n++, Description = UIStrings.Get(state.Language, "opt_save"), ActionType = "save_game" });
        options.Add(new() { Number = n++, Description = UIStrings.Get(state.Language, "opt_quit"), ActionType = "quit" });

        return new PlayerPresentation
        {
            Narrative = loc?.Description ?? "You look around, getting your bearings.",
            Options = options,
        };
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Save / Load game — multi-save support
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private static string SavesDir
    {
        get
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "game-data", "saves");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static void SaveGameToDisk(GameState state)
    {
        state.LastSavedAt = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(state, AgentHelper.JsonOpts);
        File.WriteAllText(Path.Combine(SavesDir, state.GetSaveFileName()), json);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n{UIStrings.Get(state.Language, "save_confirmed")}");
        Console.ResetColor();
    }

    /// <summary>
    /// Lists all saved games found on disk.
    /// Returns a list of (filePath, GameState) tuples, sorted by LastSavedAt descending.
    /// </summary>
    public static List<(string Path, GameState State)> ListSaves()
    {
        var results = new List<(string, GameState)>();

        foreach (var file in Directory.GetFiles(SavesDir, "save_*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var state = JsonSerializer.Deserialize<GameState>(json, AgentHelper.JsonOpts);
                if (state is not null)
                    results.Add((file, state));
            }
            catch { /* skip corrupt saves */ }
        }

        // Also load legacy save.json if it exists (migration)
        var legacyPath = Path.Combine(SavesDir, "save.json");
        if (File.Exists(legacyPath) && !results.Any(r => r.Item1 == legacyPath))
        {
            try
            {
                var json = File.ReadAllText(legacyPath);
                var state = JsonSerializer.Deserialize<GameState>(json, AgentHelper.JsonOpts);
                if (state is not null)
                    results.Add((legacyPath, state));
            }
            catch { /* skip */ }
        }

        return results.OrderByDescending(r => r.Item2.LastSavedAt).ToList();
    }

    /// <summary>Loads a specific save file from disk.</summary>
    public static GameState? LoadGameFromFile(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<GameState>(json, AgentHelper.JsonOpts);
        }
        catch { return null; }
    }

    /// <summary>Deletes a save file from disk.</summary>
    public static bool DeleteSave(string path)
    {
        try { File.Delete(path); return true; }
        catch { return false; }
    }

    /// <summary>
    /// Migrates a legacy save (no SaveId) to the new naming scheme.
    /// Assigns a SaveId, re-saves under the new filename, and deletes the old file.
    /// </summary>
    public static void MigrateLegacySave(GameState state, string legacyPath)
    {
        if (!string.IsNullOrEmpty(state.SaveId)) return; // already migrated

        state.SaveId = Guid.NewGuid().ToString("N")[..8];
        state.LastSavedAt = DateTime.UtcNow;

        var json = JsonSerializer.Serialize(state, AgentHelper.JsonOpts);
        var newPath = Path.Combine(SavesDir, state.GetSaveFileName());
        File.WriteAllText(newPath, json);

        // Remove old legacy file if different
        if (Path.GetFullPath(legacyPath) != Path.GetFullPath(newPath))
        {
            try { File.Delete(legacyPath); } catch { /* best-effort */ }
        }
    }

    /// <summary>Silent auto-save — no console output to avoid cluttering gameplay.</summary>
    private static void AutoSave(GameState state)
    {
        try
        {
            state.LastSavedAt = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(state, AgentHelper.JsonOpts);
            File.WriteAllText(Path.Combine(SavesDir, state.GetSaveFileName()), json);
        }
        catch { /* best-effort — don't crash the game loop */ }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Console UI helpers
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private static void PrintNarrative(string narrative)
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

    private static void PrintOptions(List<GameOption> options, string language)
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

    private static GameOption? GetPlayerChoice(List<GameOption> options, GameState state)
    {
        while (true)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(UIStrings.Get(state.Language, "input_prompt"));
            Console.ResetColor();
            var input = Console.ReadLine();

            if (input is null)
            {
                // End of input stream — treat as quit
                Console.WriteLine();
                return options.FirstOrDefault(o => o.ActionType.Equals("quit", StringComparison.OrdinalIgnoreCase))
                    ?? new GameOption { Number = 0, Description = "Quit", ActionType = "quit", Target = "" };
            }

            input = input.Trim();
            if (string.IsNullOrEmpty(input)) continue;

            // Text commands — display info and re-prompt (no turn consumed)
            switch (input.ToLowerInvariant())
            {
                case "map":
                    HandleMap(state);
                    continue;
                case "inv" or "inventory" or "i":
                    HandleInventory(state);
                    continue;
                case "quests" or "q":
                    PrintQuests(state);
                    continue;
                case "stats" or "s":
                    PrintStats(state);
                    continue;
                case "help" or "?":
                    PrintHelp(state.Language);
                    continue;
                case "exit" or "quit":
                    return new GameOption { Number = 0, Description = "Quit", ActionType = "quit", Target = "" };
            }

            if (int.TryParse(input, out var num))
            {
                var match = options.FirstOrDefault(o => o.Number == num);
                if (match is not null) return match;
            }

            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine(UIStrings.Format(state.Language, "input_enter_num", options.Min(o => o.Number), options.Max(o => o.Number)));
            Console.ResetColor();
        }
    }

    private static void PrintStats(GameState state)
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
        Console.ForegroundColor = p.HP <= p.MaxHP / 4 ? ConsoleColor.Red
            : p.HP <= p.MaxHP / 2 ? ConsoleColor.Yellow : ConsoleColor.Green;
        Console.WriteLine(UIStrings.Format(state.Language, "stat_hp", p.HP, p.MaxHP));
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"   {UIStrings.Format(state.Language, "stat_attack", p.EffectiveAttack, p.Attack)}");
        Console.WriteLine($"   {UIStrings.Format(state.Language, "stat_defense", p.EffectiveDefense, p.Defense)}");
        Console.WriteLine($"   {UIStrings.Format(state.Language, "stat_xp", p.XP, p.XPToNextLevel)}");
        Console.WriteLine($"   {UIStrings.Format(state.Language, "stat_gold", p.Gold)}");
        Console.ResetColor();
    }

    private static void PrintHelp(string language)
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

    private static void PrintQuests(GameState state)
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
