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

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

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
        var combatNarrator = config.CreateAgent(LoadPrompt("combat-narrator"), tools: GameTools.GetCombatTools());

        var agentMap = new Dictionary<string, AIAgent>(StringComparer.OrdinalIgnoreCase)
        {
            ["world_architect"] = worldArchitect,
            ["npc_weaver"] = npcWeaver,
            ["creature_forger"] = creatureForger,
            ["combat_narrator"] = combatNarrator,
        };

        // Set game state reference for GameTools
        GameTools.SetGameState(state);

        // ── First turn: generate starting location ──
        if (state.Locations.Count == 0)
        {
            PrintStatus("Generating your world...");
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

                var decisionText = await RunAgent(gmAgent, gmPrompt, ct);
                var decision = ParseDecision(decisionText);

                if (decision.NextAgent.Equals("PRESENT_TO_PLAYER", StringComparison.OrdinalIgnoreCase))
                    break;

                // Route to sub-agent
                if (!agentMap.TryGetValue(decision.NextAgent, out var subAgent))
                {
                    PrintWarning($"Unknown agent '{decision.NextAgent}', skipping.");
                    turnContext.Add($"SYSTEM: Unknown agent '{decision.NextAgent}' — skipped.");
                    continue;
                }

                PrintSubAgentWork(decision.NextAgent, decision.Task);

                var subPrompt = $"World theme: {state.WorldTheme}\n\n" +
                    $"Current game context:\n{string.Join("\n\n---\n\n", turnContext)}\n\n" +
                    $"Your task:\n{decision.Task}";

                var subResponse = await RunAgent(subAgent, subPrompt, ct);
                turnContext.Add($"{decision.NextAgent.ToUpper()} OUTPUT:\n{subResponse}");

                // Process sub-agent output — integrate into game state
                await ProcessSubAgentOutput(decision.NextAgent, subResponse, state, ct);
            }

            // ── Present to player ──
            var presentPrompt = "Now present the current situation to the player. " +
                "Output a PlayerPresentation JSON with a vivid narrative and 3-6 numbered options.\n\n" +
                $"Game state:\n{BuildGameStateSummary(state)}\n\n" +
                $"Turn context:\n{string.Join("\n\n---\n\n", turnContext)}\n\n" +
                "Remember: output ONLY the JSON {\"narrative\": \"...\", \"options\": [...]}";

            var presentText = await RunAgent(gmAgent, presentPrompt, ct);
            presentation = ParsePresentation(presentText);

            if (presentation is null || presentation.Options.Count == 0)
            {
                // Fallback: simple presentation
                presentation = new PlayerPresentation
                {
                    Narrative = state.CurrentLocation?.Description ?? "You look around.",
                    Options =
                    [
                        new() { Number = 1, Description = "Look around", ActionType = "look_around" },
                        new() { Number = 2, Description = "Check quests", ActionType = "check_quests" },
                        new() { Number = 3, Description = "Save game", ActionType = "save_game" },
                        new() { Number = 4, Description = "Quit", ActionType = "quit" },
                    ],
                };
            }

            // Display to player
            PrintNarrative(presentation.Narrative);
            PrintOptions(presentation.Options);

            // Get player choice
            var choice = GetPlayerChoice(presentation.Options);
            if (choice is null) continue;

            state.AddLog($"Turn {state.TurnCount}: Player chose — {choice.Description}");

            // ── Handle player action ──
            playerAction = await HandlePlayerAction(choice, state, config, agentMap, LoadPrompt, combatNarrator, ct);

            if (playerAction == "__QUIT__") break;
            if (playerAction == "__GAME_OVER__")
            {
                PrintGameOver(state);
                break;
            }

            // Check level up
            if (state.Player.TryLevelUp())
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n⬆️  LEVEL UP! You are now level {state.Player.Level}!");
                Console.WriteLine($"   HP: {state.Player.MaxHP} | Attack: {state.Player.Attack} | Defense: {state.Player.Defense}");
                Console.ResetColor();
                state.AddLog($"Player leveled up to {state.Player.Level}!");
            }
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Player action handling
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private static async Task<string> HandlePlayerAction(
        GameOption choice, GameState state, AgentConfig config,
        Dictionary<string, AIAgent> agentMap,
        Func<string, string> loadPrompt,
        AIAgent combatNarrator,
        CancellationToken ct)
    {
        switch (choice.ActionType.ToLowerInvariant())
        {
            case "move":
                return await HandleMove(choice, state, config, agentMap, loadPrompt, ct);

            case "talk":
                return await HandleTalk(choice, state, config, ct);

            case "fight":
                return await HandleFight(choice, state, combatNarrator, ct);

            case "pickup":
                return HandlePickup(choice, state);

            case "use_item":
                return HandleUseItem(choice, state);

            case "rest":
                return HandleRest(state);

            case "look_around":
                return $"Player looks around the current location: {state.CurrentLocation?.Name ?? "unknown"}.";

            case "check_quests":
                PrintQuests(state);
                return "Player reviewed their active quests.";

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
            PrintStatus($"Discovering what lies {exit.Direction}...");
            var newLoc = await GenerateLocationForEntry(
                config, state, currentLoc.Id, exit, agentMap, loadPrompt, ct);

            if (newLoc is not null)
            {
                exit.TargetLocationId = newLoc.Id;
                // Save updated current location with the new exit link
                var locJson = JsonSerializer.Serialize(currentLoc, JsonOpts);
                LocationTools.SaveLocation(locJson);
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
        GameOption choice, GameState state, AIAgent combatNarrator, CancellationToken ct)
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

        var result = await CombatWorkflow.RunAsync(state, creature, combatNarrator, ct);

        if (result == CombatResult.PlayerDefeated)
            return "__GAME_OVER__";

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
        Console.WriteLine($"\n🎒 Picked up: {item.Name} — {item.Description}");
        Console.ResetColor();

        state.AddLog($"Picked up {item.Name}.");
        return $"Player picked up {item.Name}.";
    }

    private static string HandleUseItem(GameOption choice, GameState state)
    {
        var item = state.Player.Inventory.FirstOrDefault(i =>
            i.Name.Equals(choice.Target, StringComparison.OrdinalIgnoreCase));

        if (item is null)
            return $"You don't have '{choice.Target}' in your inventory.";

        if (item.Type == "potion")
        {
            var healed = Math.Min(item.EffectValue, state.Player.MaxHP - state.Player.HP);
            state.Player.HP += healed;
            state.Player.Inventory.Remove(item);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n🧪 Used {item.Name}: healed {healed} HP (now {state.Player.HP}/{state.Player.MaxHP})");
            Console.ResetColor();

            state.AddLog($"Used {item.Name}, healed {healed} HP.");
            return $"Player used {item.Name}, healed {healed} HP.";
        }

        return $"You can't use {item.Name} right now.";
    }

    private static string HandleRest(GameState state)
    {
        var healAmount = state.Player.MaxHP / 4;
        var healed = Math.Min(healAmount, state.Player.MaxHP - state.Player.HP);
        state.Player.HP += healed;

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n💤 You rest and recover {healed} HP (now {state.Player.HP}/{state.Player.MaxHP})");
        Console.ResetColor();

        state.AddLog($"Rested, healed {healed} HP.");
        return $"Player rested, healed {healed} HP.";
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

        var backRef = fromLocationId is not null
            ? $"Include an exit with direction 'Back' and target_location_id '{fromLocationId}' (the way the player came from)."
            : "This is the starting location — no back exit needed.";

        var exitHint = entryExit is not null
            ? $"The player entered via an exit described as: \"{entryExit.Description}\". The new location should match that description."
            : "Generate a suitable starting location for this adventure.";

        var prompt = $"World theme: {state.WorldTheme}\n\n" +
            $"{exitHint}\n{backRef}\n\n" +
            "Generate a new location. Output ONLY the raw Location JSON object, no markdown fences, no explanation.";

        PrintSubAgentWork("world_architect", "Generating location...");
        var locResponse = await RunAgent(architect, prompt, ct);
        var newLoc = ParseJson<Location>(locResponse);

        if (newLoc is null || string.IsNullOrWhiteSpace(newLoc.Id))
        {
            PrintWarning("Failed to generate location.");
            return null;
        }

        newLoc.Visited = true;
        state.Locations[newLoc.Id] = newLoc;
        state.CurrentLocationId = newLoc.Id;
        state.AddLog($"Discovered new location: {newLoc.Name}");

        // Generate NPCs (~60% chance, at least 1 for starting location)
        var shouldGenNPC = fromLocationId is null || Random.Shared.NextDouble() < 0.6;
        if (shouldGenNPC)
        {
            // Use a tool-free NPC agent for clean JSON
            var npcWeaverPrompt = loadPrompt("npc-weaver");
            var npcWeaver = config.CreateAgent(npcWeaverPrompt);
            var npcCount = fromLocationId is null ? 2 : (Random.Shared.NextDouble() < 0.4 ? 2 : 1);

            for (var i = 0; i < npcCount; i++)
            {
                PrintSubAgentWork("npc_weaver", $"Creating NPC {i + 1}...");
                var npcPrompt = $"World theme: {state.WorldTheme}\n" +
                    $"Location: {newLoc.Name} — {newLoc.Description}\n" +
                    $"Location id: {newLoc.Id}\n\n" +
                    "Generate a unique NPC for this location. Output ONLY the raw NPC JSON object, no markdown fences, no explanation.";

                var npcResponse = await RunAgent(npcWeaver, npcPrompt, ct);
                var npc = ParseJson<NPC>(npcResponse);

                if (npc is not null && !string.IsNullOrWhiteSpace(npc.Id))
                {
                    npc.LocationId = newLoc.Id;
                    state.NPCs[npc.Id] = npc;
                    newLoc.NPCIds.Add(npc.Id);
                    state.AddLog($"Met {npc.Name} at {newLoc.Name}.");
                }
            }
        }

        // Generate creatures (~40% chance, never for starting location)
        if (fromLocationId is not null && Random.Shared.NextDouble() < 0.4)
        {
            var creaturePromptText = loadPrompt("creature-forger");
            var forge = config.CreateAgent(creaturePromptText);
            PrintSubAgentWork("creature_forger", "Spawning creature...");

            var difficulty = state.Player.Level <= 2 ? "easy" : state.Player.Level <= 4 ? "medium" : "hard";
            var creaturePrompt = $"World theme: {state.WorldTheme}\n" +
                $"Location: {newLoc.Name} — {newLoc.Description}\n" +
                $"Location id: {newLoc.Id}\n" +
                $"Difficulty: {difficulty}\n\n" +
                "Generate a creature for this location. Output ONLY the raw Creature JSON object, no markdown fences, no explanation.";

            var creatureResponse = await RunAgent(forge, creaturePrompt, ct);
            var creature = ParseJson<Creature>(creatureResponse);

            if (creature is not null && !string.IsNullOrWhiteSpace(creature.Id))
            {
                creature.LocationId = newLoc.Id;
                state.Creatures[creature.Id] = creature;
                newLoc.CreatureIds.Add(creature.Id);
                state.AddLog($"A {creature.Name} lurks at {newLoc.Name}.");
            }
        }

        // Persist the updated location with NPC/creature ids
        var updatedLocJson = JsonSerializer.Serialize(newLoc, JsonOpts);
        LocationTools.SaveLocation(updatedLocJson);

        return newLoc;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Agent execution helpers
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

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
            PrintWarning($"LLM error: {ex.Message.Split('\n')[0]}");
            if (sb.Length > 0) return sb.ToString().Trim();
            return "{\"next_agent\": \"PRESENT_TO_PLAYER\", \"reason\": \"Error occurred\", \"task\": \"Present current state\"}";
        }
        return sb.ToString().Trim();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Parsing helpers
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private static GameMasterDecision ParseDecision(string text)
    {
        var json = ExtractJson(text);
        if (json is null) return new GameMasterDecision { NextAgent = "PRESENT_TO_PLAYER", Task = text };
        try
        {
            return JsonSerializer.Deserialize<GameMasterDecision>(json, JsonOpts)
                ?? new GameMasterDecision { NextAgent = "PRESENT_TO_PLAYER", Task = text };
        }
        catch
        {
            return new GameMasterDecision { NextAgent = "PRESENT_TO_PLAYER", Task = text };
        }
    }

    private static PlayerPresentation? ParsePresentation(string text)
    {
        var json = ExtractJson(text);
        if (json is null) return null;
        try { return JsonSerializer.Deserialize<PlayerPresentation>(json, JsonOpts); }
        catch { return null; }
    }

    private static T? ParseJson<T>(string text) where T : class
    {
        var json = ExtractJson(text);
        if (json is null) return null;
        try { return JsonSerializer.Deserialize<T>(json, JsonOpts); }
        catch { return null; }
    }

    private static string? ExtractJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return null;
        return text[start..(end + 1)];
    }

    private static Task ProcessSubAgentOutput(string agentName, string response, GameState state, CancellationToken ct)
    {
        try
        {
            switch (agentName.ToLowerInvariant())
            {
                case "world_architect":
                    var loc = ParseJson<Location>(response);
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
                    var npc = ParseJson<NPC>(response);
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
                    var creature = ParseJson<Creature>(response);
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
            sb.AppendLine($"Description: {loc.Description}");
            sb.AppendLine($"Exits: {string.Join(", ", loc.Exits.Select(e => $"{e.Direction} ({(string.IsNullOrEmpty(e.TargetLocationId) ? "unexplored" : "visited")})"))}");

            var npcsHere = loc.NPCIds
                .Where(id => state.NPCs.ContainsKey(id))
                .Select(id => state.NPCs[id])
                .ToList();
            if (npcsHere.Count > 0)
                sb.AppendLine($"NPCs here: {string.Join(", ", npcsHere.Select(n => $"{n.Name} (id: {n.Id}, {n.Occupation})"))}");

            var creaturesHere = loc.CreatureIds
                .Where(id => state.Creatures.ContainsKey(id))
                .Select(id => state.Creatures[id])
                .Where(c => !c.IsDefeated)
                .ToList();
            if (creaturesHere.Count > 0)
                sb.AppendLine($"Creatures here: {string.Join(", ", creaturesHere.Select(c => $"{c.Name} (id: {c.Id}, {c.Difficulty})"))}");

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

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Save game
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public static void SaveGameToDisk(GameState state)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "game-data", "saves");
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(state, JsonOpts);
        File.WriteAllText(Path.Combine(dir, "save.json"), json);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n💾 Game saved!");
        Console.ResetColor();
    }

    public static GameState? LoadGameFromDisk()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "game-data", "saves", "save.json");
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<GameState>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { return null; }
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

    private static void PrintOptions(List<GameOption> options)
    {
        Console.WriteLine();
        foreach (var opt in options)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"  [{opt.Number}] ");
            Console.ResetColor();
            Console.WriteLine(opt.Description);
        }
        Console.WriteLine();
    }

    private static GameOption? GetPlayerChoice(List<GameOption> options)
    {
        while (true)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("Your choice > ");
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

            if (int.TryParse(input, out var num))
            {
                var match = options.FirstOrDefault(o => o.Number == num);
                if (match is not null) return match;
            }

            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"Please enter a number between {options.Min(o => o.Number)} and {options.Max(o => o.Number)}.");
            Console.ResetColor();
        }
    }

    private static void PrintQuests(GameState state)
    {
        var active = state.Player.ActiveQuests.Where(q => !q.IsComplete).ToList();
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("📜 Active Quests:");
        Console.ResetColor();

        if (active.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("   No active quests.");
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

    private static void PrintSubAgentWork(string agent, string task)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"  ⚙ [{agent}] ");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(task);
        Console.ResetColor();
    }

    private static void PrintStatus(string msg)
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"\n🌍 {msg}");
        Console.ResetColor();
    }

    private static void PrintWarning(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  ⚠️ {msg}");
        Console.ResetColor();
    }

    private static void PrintGameOver(GameState state)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("╔══════════════════════════════════════════╗");
        Console.WriteLine("║           ☠  GAME  OVER  ☠              ║");
        Console.WriteLine("╚══════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine($"  {state.Player.Name} fell after {state.TurnCount} turns.");
        Console.WriteLine($"  Level: {state.Player.Level} | Locations discovered: {state.Locations.Count}");
        Console.WriteLine($"  Creatures defeated: {state.Creatures.Values.Count(c => c.IsDefeated)}");
    }
}
