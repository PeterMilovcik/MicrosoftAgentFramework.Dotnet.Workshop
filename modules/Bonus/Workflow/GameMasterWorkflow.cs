using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;

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

        // Tool-free variants reused for clean JSON generation (no function-calling overhead)
        var architectGen = config.CreateAgent(PromptLoader.Load(AgentNames.WorldArchitectPrompt));
        var npcWeaverGen = config.CreateAgent(PromptLoader.Load(AgentNames.NPCWeaverPrompt));
        var creatureForgerGen = config.CreateAgent(PromptLoader.Load(AgentNames.CreatureForgerPrompt));
        var combatStrategist = config.CreateAgent(PromptLoader.Load("combat-strategist"));
        var combatNarratorGen = config.CreateAgent(PromptLoader.Load("combat-narrator"));

        var agentMap = new Dictionary<string, AIAgent>(StringComparer.OrdinalIgnoreCase)
        {
            [AgentNames.WorldArchitect] = worldArchitect,
            [AgentNames.NPCWeaver] = npcWeaver,
            [AgentNames.CreatureForger] = creatureForger,
            [AgentNames.CombatNarrator] = combatNarrator,
            [AgentNames.ItemSage] = itemSage,
            // Tool-free gen variants (clean JSON output)
            ["architect-gen"] = architectGen,
            ["npc-gen"] = npcWeaverGen,
            ["creature-gen"] = creatureForgerGen,
            ["combat-strategist"] = combatStrategist,
            ["combat-narrator"] = combatNarratorGen,
        };

        // Set game state reference for tool classes
        GameStateAccessor.Set(state);

        // ── First turn: generate starting location ──
        if (state.Locations.Count == 0)
        {
            await GenerateLocationForEntry(config, state, null, null, agentMap, ct);
        }

        // ── Main game loop ──
        var playerAction = "The adventure begins. You have just arrived.";

        while (true)
        {
            state.TurnCount++;

            // Build game state summary for the GM
            var stateSummary = GameStatePromptFactory.Build(state);

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

                string decisionText;
                await using (ConsoleSpinner.Start("GM deciding next action..."))
                {
                    decisionText = await AgentRunner.RunAgent(gmAgent, gmPrompt, ct,
                        "{\"next_agent\": \"PRESENT_TO_PLAYER\", \"reason\": \"Error occurred\", \"task\": \"Present current state\"}");
                }
                var decision = ParseDecision(decisionText);

                if (decision.NextAgent.Equals(AgentNames.PresentToPlayer, StringComparison.OrdinalIgnoreCase))
                    break;

                // Route to sub-agent
                if (!agentMap.TryGetValue(decision.NextAgent, out var subAgent))
                {
                    GameConsoleUI.PrintWarning($"Unknown agent '{decision.NextAgent}', skipping.");
                    turnContext.Add($"SYSTEM: Unknown agent '{decision.NextAgent}' — skipped.");
                    continue;
                }

                var langHint = LanguageHint.For(state.Language);
                var subPrompt = $"World theme: {state.WorldTheme}\n{langHint}\n" +
                    $"Current game context:\n{string.Join("\n\n---\n\n", turnContext)}\n\n" +
                    $"Your task:\n{decision.Task}";

                // Inject rich generation context for NPC and creature generation agents
                if (decision.NextAgent.Equals(AgentNames.NPCWeaver, StringComparison.OrdinalIgnoreCase) && state.CurrentLocation is not null)
                {
                    subPrompt = NPCPromptFactory.Build(state, state.CurrentLocation) + "\n\n" +
                        $"Your task:\n{decision.Task}\n\n" +
                        "Output ONLY the raw NPC JSON object, no markdown fences, no explanation.";
                }
                else if (decision.NextAgent.Equals(AgentNames.CreatureForger, StringComparison.OrdinalIgnoreCase) && state.CurrentLocation is not null)
                {
                    var diff = EnumExtensions.FromPlayerLevel(state.Player.Level);
                    subPrompt = CreaturePromptFactory.Build(state, state.CurrentLocation, diff) + "\n\n" +
                        $"Your task:\n{decision.Task}\n\n" +
                        "Output ONLY the raw Creature JSON object, no markdown fences, no explanation.";
                }
                else if (decision.NextAgent.Equals(AgentNames.WorldArchitect, StringComparison.OrdinalIgnoreCase))
                {
                    subPrompt = LocationPromptFactory.Build(state, state.CurrentLocationId, null) + "\n\n" +
                        $"Your task:\n{decision.Task}\n\n" +
                        "Output ONLY the raw Location JSON object, no markdown fences, no explanation.";
                }

                string subResponse;
                await using (ConsoleSpinner.Start($"[{decision.NextAgent}] {decision.Task}"))
                {
                    subResponse = await AgentRunner.RunAgent(subAgent, subPrompt, ct);
                }
                turnContext.Add($"{decision.NextAgent.ToUpper()} OUTPUT:\n{subResponse}");

                // Process sub-agent output — integrate into game state
                AgentOutputProcessor.Apply(decision.NextAgent, subResponse, state);
            }

            // ── Present to player ──
            var presentLangHint = state.Language != "English" ? $" The narrative and option descriptions MUST be in {state.Language}." : "";
            var presentPrompt = "Now present the current situation to the player. " +
                $"Output a PlayerPresentation JSON with a vivid narrative and 3-6 numbered options.{presentLangHint}\n\n" +
                $"Game state:\n{GameStatePromptFactory.Build(state)}\n\n" +
                $"Turn context:\n{string.Join("\n\n---\n\n", turnContext)}\n\n" +
                "Remember: output ONLY the JSON {\"narrative\": \"...\", \"options\": [...]}";

            string presentText;
            await using (ConsoleSpinner.Start("Narrating..."))
            {
                presentText = await AgentRunner.RunAgent(gmAgent, presentPrompt, ct);
            }
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
                GameConsoleUI.WriteLine($"\n{UIStrings.Format(state.Language, "level_up", state.Player.Level)}", ConsoleColor.Yellow);
                GameConsoleUI.WriteLine($"   {UIStrings.Format(state.Language, "level_stats", state.Player.Health.Current, state.Player.Health.Max, state.Player.Attack, state.Player.Defense)}", ConsoleColor.Yellow);
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
                var fightResult = await HandleFight(choice, state, config, agentMap, ct);
                if (fightResult == "__GAME_OVER__")
                    return HandleDeath(state);
                return fightResult;

            case ActionType.Pickup:
                return HandlePickup(choice, state);

            case ActionType.UseItem:
                return await HandleUseItem(choice, state, agentMap, ct);

            case ActionType.Rest:
                return HandleRest(state);

            case ActionType.LookAround:
                return $"Player looks around the current location: {state.CurrentLocation?.Name ?? "unknown"}.";

            case ActionType.Examine:
                return await HandleExamine(choice, state, agentMap, ct);

            case ActionType.CheckQuests:
                GameConsoleUI.PrintQuests(state);
                return "Player reviewed their active quests.";

            case ActionType.Inventory:
                return HandleInventory(state);

            case ActionType.Map:
                return HandleMap(state);

            case ActionType.Trade:
                return await TradeWorkflow.HandleTrade(choice, state, config, ct);

            case ActionType.SaveGame:
                SaveManager.Save(state);
                GameConsoleUI.WriteLine($"\n{UIStrings.Get(state.Language, "save_confirmed")}", ConsoleColor.Green);
                return "Player saved the game.";

            case ActionType.Quit:
                SaveManager.Save(state);
                GameConsoleUI.WriteLine($"\n{UIStrings.Get(state.Language, "save_confirmed")}", ConsoleColor.Green);
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
        Dictionary<string, AIAgent> agentMap,
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

        var result = await CombatWorkflow.RunAsync(state, creature, config,
            agentMap["combat-strategist"], agentMap["combat-narrator"], ct);

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

        GameConsoleUI.WriteLine($"\n{UIStrings.Format(state.Language, "picked_up", item.Name, item.Description)}", ConsoleColor.Green);

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
            GameConsoleUI.WriteLine($"\n  {UIStrings.Format(state.Language, "item_equipped", item.Name)}", ConsoleColor.DarkGray);
            return $"{item.Name} is equipped passively.";
        }

        // Route to Item Sage for scrolls, food, keys, misc, and other usable items
        if (agentMap.TryGetValue(AgentNames.ItemSage, out var itemSage))
        {
            var usePrompt = $"The player wants to USE this item.\n\n" +
                $"Item: {JsonSerializer.Serialize(item, LlmJsonParser.JsonOpts)}\n" +
                $"Player HP: {state.Player.Health}\n" +
                $"World theme: {state.WorldTheme}\n" +
                $"Location: {state.CurrentLocation?.Name ?? "unknown"}\n\n" +
                "Determine the effect and narrate what happens. Call ApplyItemEffect with the result.";

            string response;
            await using (ConsoleSpinner.Start($"[ItemSage] Using {item.Name}..."))
            {
                response = await AgentRunner.RunAgent(itemSage, usePrompt, ct,
                    "{\"action\": \"use\", \"narrative\": \"Nothing happens.\", \"effect\": {\"type\": \"narrative_only\"}}");
            }

            var narrative = ParseJsonProperty(response, "narrative");

            GameConsoleUI.WriteLine($"\n  {narrative}", ConsoleColor.Cyan);

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
            GameConsoleUI.WriteLine($"\n  {UIStrings.Format(state.Language, "examine_header", item.Name)}", ConsoleColor.White);
            GameConsoleUI.WriteLine($"  {item.Lore}", ConsoleColor.Cyan);
            return $"Player examined {item.Name} (cached lore).";
        }

        // Route to Item Sage for lore generation
        if (agentMap.TryGetValue(AgentNames.ItemSage, out var itemSage))
        {
            var examinePrompt = $"The player wants to EXAMINE this item. Generate a rich lore description.\n\n" +
                $"Item: {JsonSerializer.Serialize(item, LlmJsonParser.JsonOpts)}\n" +
                $"World theme: {state.WorldTheme}\n" +
                $"Location: {state.CurrentLocation?.Name ?? "unknown"}\n\n" +
                "Generate lore and call SetItemLore to cache it.";

            string response;
            await using (ConsoleSpinner.Start($"[ItemSage] Examining {item.Name}..."))
            {
                response = await AgentRunner.RunAgent(itemSage, examinePrompt, ct,
                    "{\"action\": \"examine\", \"lore\": \"An unremarkable item.\"}");
            }

            var lore = ParseJsonProperty(response, "lore");

            // Cache it ourselves as fallback if the agent didn't call SetItemLore
            if (string.IsNullOrWhiteSpace(item.Lore))
                item.Lore = lore;

            GameConsoleUI.WriteLine($"\n  {UIStrings.Format(state.Language, "examine_header", item.Name)}", ConsoleColor.White);
            GameConsoleUI.WriteLine($"  {lore}", ConsoleColor.Cyan);

            state.AddLog($"Examined {item.Name}.");
            return $"Player examined {item.Name}.";
        }

        // Fallback: show the basic description
        GameConsoleUI.WriteLine($"\n  {item.Description}", ConsoleColor.DarkGray);
        return $"Player examined {item.Name}.";
    }

    /// <summary>Extract a single named property from an agent's JSON response, falling back to the raw text.</summary>
    private static string ParseJsonProperty(string text, string propertyName)
    {
        var json = LlmJsonParser.ExtractJson(text);
        if (json is null) return text.Trim();
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.StrOrNull(propertyName) ?? text.Trim();
        }
        catch { /* fall through */ }
        return text.Trim();
    }

    private static string HandleRest(GameState state)
    {
        var healAmount = (int)(state.Player.Health.Max * GameConstants.RestHealFraction);
        state.Player.Health = state.Player.Health.Heal(healAmount, out var healed);

        GameConsoleUI.WriteLine($"\n{UIStrings.Format(state.Language, "rest_healed", healed, state.Player.Health.Current, state.Player.Health.Max)}", ConsoleColor.Green);

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

        GameConsoleUI.WriteLine($"{consolePrefix}{UIStrings.Format(state.Language, "used_item", potion.Name, healed, state.Player.Health.Current, state.Player.Health.Max)}", ConsoleColor.Green);

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
        // Use pre-created tool-free agents for clean JSON output
        var architect = agentMap["architect-gen"];

        var prompt = LocationPromptFactory.Build(state, fromLocationId, entryExit);

        string locResponse;
        await using (ConsoleSpinner.Start($"[{AgentNames.WorldArchitect}] Generating location..."))
        {
            locResponse = await AgentRunner.RunAgent(architect, prompt, ct);
        }
        var newLoc = LlmJsonParser.ParseJson<Location>(locResponse);

        if (newLoc is null || newLoc.Id.IsEmpty)
        {
            GameConsoleUI.PrintWarning("Failed to generate location.");
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
            var npcCount = fromLocationId is null
                ? GameConstants.StartingNPCCount
                : (Random.Shared.NextDouble() < GameConstants.ExtraNPCChance ? 2 : 1);

            await GenerateNPCsForLocation(state, newLoc, npcCount, agentMap["npc-gen"], ct);
        }

        // Generate creatures (probability driven by danger level; never for starting location)
        if (fromLocationId is not null && Random.Shared.NextDouble() < creatureChance)
        {
            await GenerateCreatureForLocation(state, newLoc, agentMap["creature-gen"], ct);
        }

        return newLoc;
    }

    private static async Task GenerateNPCsForLocation(
        GameState state, Location location, int count, AIAgent npcWeaver, CancellationToken ct)
    {
        for (var i = 0; i < count; i++)
        {
            var npcContext = NPCPromptFactory.Build(state, location);
            var npcPrompt = npcContext + "\n\n" +
                "Generate a unique NPC for this location. Output ONLY the raw NPC JSON object, no markdown fences, no explanation.";

            string npcResponse;
            await using (ConsoleSpinner.Start($"[{AgentNames.NPCWeaver}] Creating NPC {i + 1}..."))
            {
                npcResponse = await AgentRunner.RunAgent(npcWeaver, npcPrompt, ct);
            }
            var npc = LlmJsonParser.ParseJson<NPC>(npcResponse);

            if (npc is not null && !npc.Id.IsEmpty)
            {
                npc.LocationId = location.Id;
                state.NPCs[npc.Id] = npc;
                location.NPCIds.Add(npc.Id);
                state.AddLog($"Met {npc.Name} at {location.Name}.");
            }
        }
    }

    private static async Task GenerateCreatureForLocation(
        GameState state, Location location, AIAgent forge, CancellationToken ct)
    {
        var difficulty = EnumExtensions.FromPlayerLevel(state.Player.Level);
        var creatureContext = CreaturePromptFactory.Build(state, location, difficulty);
        var creaturePrompt = creatureContext + "\n\n" +
            "Generate a creature for this location. Output ONLY the raw Creature JSON object, no markdown fences, no explanation.";

        string creatureResponse;
        await using (ConsoleSpinner.Start($"[{AgentNames.CreatureForger}] Spawning creature..."))
        {
            creatureResponse = await AgentRunner.RunAgent(forge, creaturePrompt, ct);
        }
        var creature = LlmJsonParser.ParseJson<Creature>(creatureResponse);

        if (creature is not null && !creature.Id.IsEmpty)
        {
            creature.LocationId = location.Id;
            state.Creatures[creature.Id] = creature;
            location.CreatureIds.Add(creature.Id);
            state.AddLog($"A {creature.Name} lurks at {location.Name}.");
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Parsing helpers
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private static GameMasterDecision ParseDecision(string text)
    {
        var fallback = new GameMasterDecision { NextAgent = AgentNames.PresentToPlayer, Task = text };
        return LlmJsonParser.ParseJson(text, fallback);
    }

    private static PlayerPresentation? ParsePresentation(string text)
        => LlmJsonParser.ParseJson<PlayerPresentation>(text);

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
            GameConsoleUI.WriteLine($"  {UIStrings.Format(state.Language, "quest_complete", quest.Title)}", ConsoleColor.Green);
            if (quest.RewardGold > 0) GameConsoleUI.WriteLine($"     {UIStrings.Format(state.Language, "quest_gold", quest.RewardGold)}", ConsoleColor.Yellow);
            if (quest.RewardXP > 0) GameConsoleUI.WriteLine($"     {UIStrings.Format(state.Language, "quest_xp", quest.RewardXP)}", ConsoleColor.Yellow);
            if (quest.RewardItem is not null) GameConsoleUI.WriteLine($"     {UIStrings.Format(state.Language, "quest_item", quest.RewardItem.Name)}", ConsoleColor.Yellow);

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

        options.Add(new() { Number = n++, Description = UIStrings.Get(state.Language, "opt_look_around"), ActionType = ActionType.LookAround });
        if (state.Player.Inventory.Count > 0)
            options.Add(new() { Number = n++, Description = UIStrings.Get(state.Language, "opt_inventory"), ActionType = ActionType.Inventory });
        options.Add(new() { Number = n++, Description = UIStrings.Get(state.Language, "opt_quests"), ActionType = ActionType.CheckQuests });
        options.Add(new() { Number = n++, Description = UIStrings.Get(state.Language, "opt_map"), ActionType = ActionType.Map });
        options.Add(new() { Number = n++, Description = UIStrings.Get(state.Language, "opt_save"), ActionType = ActionType.SaveGame });
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
            GameConsoleUI.Write(UIStrings.Get(state.Language, "input_prompt"), ConsoleColor.DarkGray);
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

            GameConsoleUI.WriteLine(UIStrings.Format(state.Language, "input_enter_num", options.Min(o => o.Number), options.Max(o => o.Number)), ConsoleColor.DarkYellow);
        }
    }

}
