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
/// <para>
/// NOTE: Static by design (AIFunctionFactory.Create requires static methods for tools).
/// TODO: When the framework supports instance-based tool registration, convert to
///       an instance class with constructor-injected dependencies (AgentConfig, IDiceRoller, etc.).
/// </para>
/// </summary>
internal static class GameMasterWorkflow
{

    public static async Task RunAsync(AgentConfig config, GameState state, CancellationToken ct = default)
    {
        // ── Create persistent agents ──
        var gmAgent = config.CreateAgent(PromptLoader.Load(AgentNames.GameMasterPrompt));
        var worldArchitect = config.CreateAgent(PromptLoader.Load(AgentNames.WorldArchitectPrompt), tools: LocationTools.GetTools());
        var npcWeaver = config.CreateAgent(PromptLoader.Load(AgentNames.NPCWeaverPrompt), tools: NPCTools.GetTools());
        var creatureForger = config.CreateAgent(PromptLoader.Load(AgentNames.CreatureForgerPrompt), tools: CreatureTools.GetTools());
        var combatNarrator = config.CreateAgent(PromptLoader.Load(AgentNames.CombatNarratorPrompt));
        var itemSage = config.CreateAgent(PromptLoader.Load(AgentNames.ItemSagePrompt), tools: ItemTools.GetTools());

        var agentMap = new Dictionary<string, AIAgent>(StringComparer.OrdinalIgnoreCase)
        {
            [AgentNames.WorldArchitect] = worldArchitect,
            [AgentNames.NPCWeaver] = npcWeaver,
            [AgentNames.CreatureForger] = creatureForger,
            [AgentNames.CombatNarrator] = combatNarrator,
            [AgentNames.ItemSage] = itemSage,
        };

        // Set game state reference for tool classes
        GameStateAccessor.Set(state);

        // ── First turn: generate starting location ──
        if (state.Locations.Count == 0)
        {
            AgentHelper.PrintStatus("Generating your world...");
            await GenerateLocationForEntry(config, state, null, null, agentMap, ct);
        }

        // ── Main game loop ──
        var playerAction = "The adventure begins. You have just arrived.";

        while (true)
        {
            state.TurnCount++;

            // Build game state summary for the GM
            var stateSummary = ContextBuilder.GameStateSummary(state);

            // ── Inner routing loop ──
            var turnContext = new List<string>
            {
                $"GAME STATE:\n{stateSummary}",
                $"PLAYER ACTION: {playerAction}",
            };

            PlayerPresentation? presentation = null;
            var innerIter = 0;

            while (innerIter < GameConstants.MaxInnerIterations)
            {
                innerIter++;

                var contextText = string.Join("\n\n---\n\n", turnContext);
                var gmPrompt = "Review the current game state and player's action. Decide what sub-agent work is needed, " +
                    "or output PRESENT_TO_PLAYER if ready.\n\n" +
                    $"{contextText}\n\n" +
                    "Respond with a JSON InnerDecision: {\"next_agent\": \"...\", \"reason\": \"...\", \"task\": \"...\"}. " +
                    "Use PRESENT_TO_PLAYER when all generation is done and you are ready to show the player their options.";

                if (innerIter >= GameConstants.MaxInnerIterations)
                    gmPrompt += "\n\nIMPORTANT: This is your last inner iteration. You MUST output PRESENT_TO_PLAYER now.";

                var decisionText = await AgentHelper.RunAgent(gmAgent, gmPrompt, ct,
                    "{\"next_agent\": \"PRESENT_TO_PLAYER\", \"reason\": \"Error occurred\", \"task\": \"Present current state\"}");
                var decision = ParseDecision(decisionText);

                if (decision.NextAgent.Equals(AgentNames.PresentToPlayer, StringComparison.OrdinalIgnoreCase))
                    break;

                // Route to sub-agent
                if (!agentMap.TryGetValue(decision.NextAgent, out var subAgent))
                {
                    AgentHelper.PrintWarning($"Unknown agent '{decision.NextAgent}', skipping.");
                    turnContext.Add($"SYSTEM: Unknown agent '{decision.NextAgent}' — skipped.");
                    continue;
                }

                AgentHelper.PrintSubAgentWork(decision.NextAgent, decision.Task);

                var langHint = LanguageHint.For(state.Language);
                var subPrompt = $"World theme: {state.WorldTheme}\n{langHint}\n" +
                    $"Current game context:\n{string.Join("\n\n---\n\n", turnContext)}\n\n" +
                    $"Your task:\n{decision.Task}";

                // Inject rich generation context for NPC and creature generation agents
                if (decision.NextAgent.Equals(AgentNames.NPCWeaver, StringComparison.OrdinalIgnoreCase) && state.CurrentLocation is not null)
                {
                    subPrompt = ContextBuilder.NPCGeneration(state, state.CurrentLocation) + "\n\n" +
                        $"Your task:\n{decision.Task}\n\n" +
                        "Output ONLY the raw NPC JSON object, no markdown fences, no explanation.";
                }
                else if (decision.NextAgent.Equals(AgentNames.CreatureForger, StringComparison.OrdinalIgnoreCase) && state.CurrentLocation is not null)
                {
                    var diff = EnumExtensions.FromPlayerLevel(state.Player.Level);
                    subPrompt = ContextBuilder.CreatureGeneration(state, state.CurrentLocation, diff) + "\n\n" +
                        $"Your task:\n{decision.Task}\n\n" +
                        "Output ONLY the raw Creature JSON object, no markdown fences, no explanation.";
                }
                else if (decision.NextAgent.Equals(AgentNames.WorldArchitect, StringComparison.OrdinalIgnoreCase))
                {
                    subPrompt = ContextBuilder.LocationGeneration(state, state.CurrentLocationId, null) + "\n\n" +
                        $"Your task:\n{decision.Task}\n\n" +
                        "Output ONLY the raw Location JSON object, no markdown fences, no explanation.";
                }

                var subResponse = await AgentHelper.RunAgent(subAgent, subPrompt, ct);
                turnContext.Add($"{decision.NextAgent.ToUpper()} OUTPUT:\n{subResponse}");

                // Process sub-agent output — integrate into game state
                AgentOutputProcessor.Apply(decision.NextAgent, subResponse, state);
            }

            // ── Present to player ──
            var presentLangHint = state.Language != "English" ? $" The narrative and option descriptions MUST be in {state.Language}." : "";
            var presentPrompt = "Now present the current situation to the player. " +
                $"Output a PlayerPresentation JSON with a vivid narrative and 3-6 numbered options.{presentLangHint}\n\n" +
                $"Game state:\n{ContextBuilder.GameStateSummary(state)}\n\n" +
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
            GameConsoleUI.PrintNarrative(presentation.Narrative);
            GameConsoleUI.PrintOptions(presentation.Options, state.Language);

            // Get player choice
            var choice = GetPlayerChoice(presentation.Options, state);
            if (choice is null) continue;

            state.AddLog($"Turn {state.TurnCount}: Player chose — {choice.Description}");

            // ── Handle player action ──
            playerAction = await HandlePlayerAction(choice, state, config, agentMap, ct);

            if (playerAction == "__QUIT__") break;

            // Check level up
            if (state.Player.TryLevelUp())
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n{UIStrings.Format(state.Language, "level_up", state.Player.Level)}");
                Console.WriteLine($"   {UIStrings.Format(state.Language, "level_stats", state.Player.Health.Current, state.Player.Health.Max, state.Player.Attack, state.Player.Defense)}");
                Console.ResetColor();
                state.AddLog($"Player leveled up to {state.Player.Level}!");
            }

            // Check quest completion
            CheckQuestCompletion(state);

            // Auto-save after every turn
            SaveManager.AutoSave(state);
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Player action handling
    // TODO: Consider strategy/handler pattern — each ActionType
    //       could be a pluggable IActionHandler once DI is introduced.
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private static async Task<string> HandlePlayerAction(
        GameOption choice, GameState state, AgentConfig config,
        Dictionary<string, AIAgent> agentMap,
        CancellationToken ct)
    {
        switch (choice.ActionType)
        {
            case ActionType.Move:
                return await HandleMove(choice, state, config, agentMap, ct);

            case ActionType.Talk:
                return await HandleTalk(choice, state, config, ct);

            case ActionType.Fight:
                var fightResult = await HandleFight(choice, state, config, ct);
                if (fightResult == "__GAME_OVER__")
                    return HandleDeath(state);
                return fightResult;

            case ActionType.Pickup:
                return HandlePickup(choice, state);

            case ActionType.Use_Item:
                return await HandleUseItem(choice, state, agentMap, ct);

            case ActionType.Rest:
                return HandleRest(state);

            case ActionType.Look_Around:
                return $"Player looks around the current location: {state.CurrentLocation?.Name ?? "unknown"}.";

            case ActionType.Examine:
                return await HandleExamine(choice, state, agentMap, ct);

            case ActionType.Check_Quests:
                GameConsoleUI.PrintQuests(state);
                return "Player reviewed their active quests.";

            case ActionType.Inventory:
                return HandleInventory(state);

            case ActionType.Map:
                return HandleMap(state);

            case ActionType.Trade:
                return await TradeWorkflow.HandleTrade(choice, state, config, ct);

            case ActionType.Save_Game:
                SaveManager.Save(state);
                return "Player saved the game.";

            case ActionType.Quit:
                SaveManager.Save(state);
                return "__QUIT__";

            default:
                return $"Player chose: {choice.Description}";
        }
    }

    private static async Task<string> HandleMove(
        GameOption choice, GameState state, AgentConfig config,
        Dictionary<string, AIAgent> agentMap,
        CancellationToken ct)
    {
        var currentLoc = state.CurrentLocation;
        if (currentLoc is null) return "You're lost in the void.";

        var exit = currentLoc.Exits.FirstOrDefault(e =>
            e.Direction.Equals(choice.Target, StringComparison.OrdinalIgnoreCase));

        if (exit is null)
            return $"There is no exit in direction '{choice.Target}'.";

        if (exit.TargetLocationId.IsEmpty)
        {
            // Unexplored — generate new location
            AgentHelper.PrintStatus($"Discovering what lies {exit.Direction}...");
            var newLoc = await GenerateLocationForEntry(
                config, state, currentLoc.Id, exit, agentMap, ct);

            if (newLoc is not null)
            {
                exit.TargetLocationId = newLoc.Id;
            }
        }

        if (!exit.TargetLocationId.IsEmpty && state.Locations.TryGetValue(exit.TargetLocationId, out var targetLoc))
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
            npc = FuzzyFinder.ByNameOrId(state.NPCs.Values, npcId, n => n.Name, n => n.Id.Value);

            // Also try matching NPCs at current location
            if (npc is null && state.CurrentLocation is not null)
            {
                npc = FuzzyFinder.ByName(
                    state.CurrentLocation.NPCIds
                        .Where(id => state.NPCs.ContainsKey(id))
                        .Select(id => state.NPCs[id]),
                    npcId, n => n.Name);
            }

            if (npc is null)
                return $"NPC '{npcId}' not found.";
        }

        await DialogueWorkflow.RunAsync(config, state, npc, ct);
        return $"Player spoke with {npc.Name}.";
    }

    private static async Task<string> HandleFight(
        GameOption choice, GameState state, AgentConfig config,
        CancellationToken ct)
    {
        var creatureId = choice.Target;
        if (!state.Creatures.TryGetValue(creatureId, out var creature))
        {
            // Fuzzy match: try matching by name
            creature = FuzzyFinder.ByNameOrId(state.Creatures.Values, creatureId, c => c.Name, c => c.Id.Value);

            if (creature is null)
                return $"Creature '{creatureId}' not found.";
        }

        if (creature.IsDefeated)
            return $"{creature.Name} has already been defeated.";

        var result = await CombatWorkflow.RunAsync(state, creature, config, ct);

        if (result == CombatResult.PlayerDefeated)
            return "__GAME_OVER__";

        // Boost disposition and mood for nearby NPCs after defeating a creature
        if (result == CombatResult.CreatureDefeated && state.CurrentLocation is not null)
        {
            foreach (var npcId in state.CurrentLocation.NPCIds)
            {
                if (state.NPCs.TryGetValue(npcId, out var nearbyNpc))
                {
                    nearbyNpc.DispositionTowardPlayer = nearbyNpc.DispositionTowardPlayer.Improve(GameConstants.CombatDispositionBoost);
                    if (Mood.IsTense(nearbyNpc.Mood))
                        nearbyNpc.Mood = Mood.Relieved;
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
        var item = FuzzyFinder.ByName(state.Player.Inventory, choice.Target, i => i.Name);

        if (item is null)
            return $"You don't have '{choice.Target}' in your inventory.";

        // Potions: direct handling (no LLM call needed for simple heal math)
        if (item.Type == ItemType.Potion)
            return ApplyPotion(state, item);

        // Non-usable items (weapons, armor without IsUsable flag)
        if (!item.IsUsable && item.Type.IsEquippable())
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"\n  {UIStrings.Format(state.Language, "item_equipped", item.Name)}");
            Console.ResetColor();
            return $"{item.Name} is equipped passively.";
        }

        // Route to Item Sage for scrolls, food, keys, misc, and other usable items
        if (agentMap.TryGetValue(AgentNames.ItemSage, out var itemSage))
        {
            AgentHelper.PrintStatus($"Using {item.Name}...");

            var usePrompt = $"The player wants to USE this item.\n\n" +
                $"Item: {JsonSerializer.Serialize(item, AgentHelper.JsonOpts)}\n" +
                $"Player HP: {state.Player.Health}\n" +
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
        var item = FuzzyFinder.ByName(state.Player.Inventory, choice.Target, i => i.Name);

        // Also check location items
        if (item is null && state.CurrentLocation is not null)
        {
            item = FuzzyFinder.ByName(state.CurrentLocation.Items, choice.Target, i => i.Name);
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
        if (agentMap.TryGetValue(AgentNames.ItemSage, out var itemSage))
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
            return doc.RootElement.StrOrNull("narrative") ?? text.Trim();
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
            return doc.RootElement.StrOrNull("lore") ?? text.Trim();
        }
        catch { /* fall through */ }
        return text.Trim();
    }

    private static string HandleRest(GameState state)
    {
        var healAmount = (int)(state.Player.Health.Max * GameConstants.RestHealFraction);
        state.Player.Health = state.Player.Health.Heal(healAmount, out var healed);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n{UIStrings.Format(state.Language, "rest_healed", healed, state.Player.Health.Current, state.Player.Health.Max)}");
        Console.ResetColor();

        state.AddLog($"Rested, healed {healed} HP.");
        return $"Player rested, healed {healed} HP.";
    }

    /// <summary>
    /// Heals the player with a potion, removes it from inventory, prints feedback, and logs it.
    /// Shared by HandleUse and HandleInventory.
    /// </summary>
    private static string ApplyPotion(GameState state, Item potion, string consolePrefix = "\n")
    {
        state.Player.Health = state.Player.Health.Heal(potion.EffectValue, out var healed);
        state.Player.Inventory.Remove(potion);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"{consolePrefix}{UIStrings.Format(state.Language, "used_item", potion.Name, healed, state.Player.Health.Current, state.Player.Health.Max)}");
        Console.ResetColor();

        state.AddLog($"Used {potion.Name}, healed {healed} HP.");
        return $"Player used {potion.Name}, healed {healed} HP.";
    }

    // ── Inventory ──

    private static string HandleInventory(GameState state)
    {
        var player = state.Player;
        GameConsoleUI.PrintInventory(state);

        if (player.Inventory.Count == 0)
            return "Player checked inventory — nothing to do.";

        // Offer to use a potion
        var potions = player.Inventory.Where(i => i.Type == ItemType.Potion).ToList();
        if (potions.Count > 0 && player.Health.Current < player.Health.Max)
        {
            GameConsoleUI.Write($"\n   {UIStrings.Get(state.Language, "inventory_use_prompt")}", ConsoleColor.DarkCyan);

            var input = Console.ReadLine()?.Trim();
            if (int.TryParse(input, out var idx) && idx >= 1 && idx <= player.Inventory.Count)
            {
                var chosen = player.Inventory[idx - 1];
                if (chosen.Type == ItemType.Potion)
                {
                    return ApplyPotion(state, chosen, "\n   ");
                }
                else
                {
                    GameConsoleUI.WriteLine($"   {UIStrings.Format(state.Language, "item_equipped", chosen.Name)}", ConsoleColor.DarkGray);
                }
            }
        }
        else
        {
            GameConsoleUI.Write($"\n   {UIStrings.Get(state.Language, "inventory_close")}", ConsoleColor.DarkGray);
            Console.ReadLine();
        }

        return "Player reviewed their inventory.";
    }

    // ── Map ──

    private static string HandleMap(GameState state)
    {
        GameConsoleUI.PrintMap(state);
        return "Player viewed the map.";
    }

    // ── Death / Respawn ──

    private static string HandleDeath(GameState state)
    {
        // Penalty: lose gold and XP on death
        state.Player.Gold = state.Player.Gold.ApplyPenalty(GameConstants.DeathGoldPenaltyFraction, out var goldLost);
        var xpLost = state.Player.LoseXP(GameConstants.DeathXPPenaltyFraction);

        // Full HP restore
        state.Player.Health = state.Player.Health.RestoreToMax();

        // Teleport to first discovered location (starting area)
        var startLoc = state.Locations.Values.FirstOrDefault();
        if (startLoc is not null)
            state.CurrentLocationId = startLoc.Id;

        GameConsoleUI.PrintDeathBanner(state, goldLost, xpLost, startLoc?.Name);

        state.AddLog($"Fell in battle. Lost {goldLost}g and {xpLost}xp. Respawned at {startLoc?.Name ?? "start"}.");

        // Auto-save after death so penalties persist
        SaveManager.AutoSave(state);

        return $"Player was defeated but respawned at {startLoc?.Name ?? "the starting area"} with penalties.";
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Location generation (triggers NPC/creature gen)
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private static async Task<Location?> GenerateLocationForEntry(
        AgentConfig config, GameState state,
        string? fromLocationId, Exit? entryExit,
        Dictionary<string, AIAgent> agentMap,
        CancellationToken ct)
    {
        // Use a dedicated agent WITHOUT tools to get clean JSON output
        var architectPrompt = PromptLoader.Load(AgentNames.WorldArchitectPrompt);
        var architect = config.CreateAgent(architectPrompt);

        var prompt = ContextBuilder.LocationGeneration(state, fromLocationId, entryExit);

        AgentHelper.PrintSubAgentWork(AgentNames.WorldArchitect, "Generating location...");
        var locResponse = await AgentHelper.RunAgent(architect, prompt, ct);
        var newLoc = AgentHelper.ParseJson<Location>(locResponse);

        if (newLoc is null || newLoc.Id.IsEmpty)
        {
            AgentHelper.PrintWarning("Failed to generate location.");
            return null;
        }

        newLoc.Visited = true;
        state.Locations[newLoc.Id] = newLoc;
        state.CurrentLocationId = newLoc.Id;
        state.AddLog($"Discovered new location: {newLoc.Name}");

        // DangerLevel-driven spawn probabilities
        var npcChance = newLoc.DangerLevel.NpcSpawnChance();
        var creatureChance = newLoc.DangerLevel.CreatureSpawnChance();

        // Generate NPCs (probability driven by danger level; always for starting location)
        var shouldGenNPC = fromLocationId is null || Random.Shared.NextDouble() < npcChance;
        if (shouldGenNPC)
        {
            // Use a tool-free NPC agent for clean JSON
            var npcWeaverPrompt = PromptLoader.Load(AgentNames.NPCWeaverPrompt);
            var npcWeaver = config.CreateAgent(npcWeaverPrompt);
            var npcCount = fromLocationId is null
                ? GameConstants.StartingNPCCount
                : (Random.Shared.NextDouble() < GameConstants.ExtraNPCChance ? 2 : 1);

            for (var i = 0; i < npcCount; i++)
            {
                AgentHelper.PrintSubAgentWork(AgentNames.NPCWeaver, $"Creating NPC {i + 1}...");
                var npcContext = ContextBuilder.NPCGeneration(state, newLoc);
                var npcPrompt = npcContext + "\n\n" +
                    "Generate a unique NPC for this location. Output ONLY the raw NPC JSON object, no markdown fences, no explanation.";

                var npcResponse = await AgentHelper.RunAgent(npcWeaver, npcPrompt, ct);
                var npc = AgentHelper.ParseJson<NPC>(npcResponse);

                if (npc is not null && !npc.Id.IsEmpty)
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
            var creaturePromptText = PromptLoader.Load(AgentNames.CreatureForgerPrompt);
            var forge = config.CreateAgent(creaturePromptText);
            AgentHelper.PrintSubAgentWork(AgentNames.CreatureForger, "Spawning creature...");

            var difficulty = EnumExtensions.FromPlayerLevel(state.Player.Level);
            var creatureContext = ContextBuilder.CreatureGeneration(state, newLoc, difficulty);
            var creaturePrompt = creatureContext + "\n\n" +
                "Generate a creature for this location. Output ONLY the raw Creature JSON object, no markdown fences, no explanation.";

            var creatureResponse = await AgentHelper.RunAgent(forge, creaturePrompt, ct);
            var creature = AgentHelper.ParseJson<Creature>(creatureResponse);

            if (creature is not null && !creature.Id.IsEmpty)
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
        var fallback = new GameMasterDecision { NextAgent = AgentNames.PresentToPlayer, Task = text };
        return AgentHelper.ParseJson(text, fallback);
    }

    private static PlayerPresentation? ParsePresentation(string text)
        => AgentHelper.ParseJson<PlayerPresentation>(text);

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Quest completion
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private static void CheckQuestCompletion(GameState state)
    {
        foreach (var quest in state.Player.ActiveQuests.Where(q => !q.IsComplete))
        {
            var completed = quest.Type switch
            {
                QuestType.Defeat => state.Creatures.TryGetValue(quest.TargetId, out var c) && c.IsDefeated,
                QuestType.Fetch => state.Player.Inventory.Any(i =>
                    i.Name.Equals(quest.TargetId, StringComparison.OrdinalIgnoreCase)),
                QuestType.Explore => state.Locations.TryGetValue(quest.TargetId, out var loc) && loc.Visited,
                _ => false,
            };

            if (!completed) continue;

            quest.IsComplete = true;

            // Award rewards
            state.Player.Gold += quest.RewardGold;
            state.Player.AddXP(quest.RewardXP);
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
            if (!quest.GiverNPCId.IsEmpty && state.NPCs.TryGetValue(quest.GiverNPCId, out var giver))
            {
                giver.DispositionTowardPlayer = giver.DispositionTowardPlayer.Improve(GameConstants.QuestDispositionBoost);
                giver.Mood = Mood.Grateful;
            }
        }
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
                options.Add(new() { Number = n++, Description = $"Go {exit.Direction} — {exit.Description}", ActionType = ActionType.Move, Target = exit.Direction });

            // NPCs
            foreach (var npcId in loc.NPCIds)
            {
                if (state.NPCs.TryGetValue(npcId, out var npc))
                    options.Add(new() { Number = n++, Description = $"Talk to {npc.Name} ({npc.Occupation})", ActionType = ActionType.Talk, Target = npc.Id });
            }

            // Creatures
            foreach (var cId in loc.CreatureIds)
            {
                if (state.Creatures.TryGetValue(cId, out var creature) && !creature.IsDefeated)
                    options.Add(new() { Number = n++, Description = $"Fight {creature.Name}", ActionType = ActionType.Fight, Target = creature.Id });
            }

            // Items on ground
            foreach (var item in loc.Items)
                options.Add(new() { Number = n++, Description = $"Pick up {item.Name}", ActionType = ActionType.Pickup, Target = item.Name });
        }

        options.Add(new() { Number = n++, Description = UIStrings.Get(state.Language, "opt_look_around"), ActionType = ActionType.Look_Around });
        if (state.Player.Inventory.Count > 0)
            options.Add(new() { Number = n++, Description = UIStrings.Get(state.Language, "opt_inventory"), ActionType = ActionType.Inventory });
        options.Add(new() { Number = n++, Description = UIStrings.Get(state.Language, "opt_quests"), ActionType = ActionType.Check_Quests });
        options.Add(new() { Number = n++, Description = UIStrings.Get(state.Language, "opt_map"), ActionType = ActionType.Map });
        options.Add(new() { Number = n++, Description = UIStrings.Get(state.Language, "opt_save"), ActionType = ActionType.Save_Game });
        options.Add(new() { Number = n++, Description = UIStrings.Get(state.Language, "opt_quit"), ActionType = ActionType.Quit });

        return new PlayerPresentation
        {
            Narrative = loc?.Description ?? "You look around, getting your bearings.",
            Options = options,
        };
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Save / Load game — multi-save support
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Player input
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

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
                return options.FirstOrDefault(o => o.ActionType == ActionType.Quit)
                    ?? new GameOption { Number = 0, Description = "Quit", ActionType = ActionType.Quit, Target = "" };
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
                    GameConsoleUI.PrintQuests(state);
                    continue;
                case "stats" or "s":
                    GameConsoleUI.PrintStats(state);
                    continue;
                case "help" or "?":
                    GameConsoleUI.PrintHelp(state.Language);
                    continue;
                case "exit" or "quit":
                    return new GameOption { Number = 0, Description = "Quit", ActionType = ActionType.Quit, Target = "" };
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

}
