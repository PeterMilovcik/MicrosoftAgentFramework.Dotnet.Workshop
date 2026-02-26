using System.Text.Json;
using RPGGameMaster.Models;

namespace RPGGameMaster.Workflow;

/// <summary>
/// Handles the buy/sell trade loop when the player interacts with a merchant NPC.
/// Extracted from GameMasterWorkflow to keep trading logic self-contained.
/// NOTE: Static — see GameMasterWorkflow for DI migration notes.
/// </summary>
internal static class TradeWorkflow
{
    internal static async Task<string> HandleTrade(
        GameOption choice, GameState state, AgentConfig config, CancellationToken ct)
    {
        var npcId = choice.Target;
        NPC? npc = null;

        if (state.NPCs.TryGetValue(npcId, out npc)) { }
        else
        {
            npc = FuzzyFinder.ByName(state.NPCs.Values, npcId, n => n.Name);
        }

        if (npc is null)
            return $"Merchant '{npcId}' not found.";

        Console.WriteLine();
        GameConsoleUI.WriteLine($"  {UIStrings.Format(state.Language, "trade_header", npc.Name)}", ConsoleColor.Yellow);

        // Create a merchant agent
        var merchantPrompt = $"You are {npc.Name}, a merchant. " +
            $"The player has {state.Player.Gold} gold. " +
            "Generate a shop inventory of 3-5 items the player can buy, priced fairly for their level.\n\n" +
            $"Player level: {state.Player.Level}\n" +
            $"World theme: {state.WorldTheme}\n\n" +
            "Output ONLY a JSON object:\n" +
            "{\"greeting\": \"...\", \"items\": [{\"name\": \"...\", \"description\": \"...\", \"type\": \"weapon|armor|potion|misc\", \"effect_value\": 0, \"price\": 0}, ...]}";

        var merchantAgent = config.CreateAgent($"You are a merchant NPC named {npc.Name}. {npc.Personality}");
        string shopResponse;
        await using (ConsoleSpinner.Start($"[{npc.Name}] Preparing wares..."))
        {
            shopResponse = await AgentHelper.RunAgent(merchantAgent, merchantPrompt, ct,
                "{\"greeting\": \"Welcome!\", \"items\": []}");
        }

        var shopJson = AgentHelper.ExtractJson(shopResponse);
        List<ShopItem> shopItems = [];
        string greeting = "Welcome, adventurer!";

        if (shopJson is not null)
        {
            try
            {
                using var doc = JsonDocument.Parse(shopJson);
                var root = doc.RootElement;
                greeting = root.Str("greeting", greeting);
                if (root.TryGetProperty("items", out var arr))
                {
                    foreach (var el in arr.EnumerateArray())
                    {
                        shopItems.Add(new ShopItem
                        {
                            Name = el.Str("name"),
                            Description = el.Str("description"),
                            Type = ParseItemType(el.StrOrNull("type")),
                            EffectValue = el.Int("effect_value"),
                            Price = el.Int("price", 10),
                        });
                    }
                }
            }
            catch { /* use defaults */ }
        }

        GameConsoleUI.WriteLine($"\n  {npc.Name}: \"{greeting}\"", ConsoleColor.Cyan);

        if (shopItems.Count == 0)
        {
            GameConsoleUI.WriteLine($"  {UIStrings.Get(state.Language, "trade_no_items")}", ConsoleColor.DarkGray);
            return $"Attempted to trade with {npc.Name} but no items were available.";
        }

        // Show shop
        GameConsoleUI.WriteLine($"  {UIStrings.Format(state.Language, "trade_gold", state.Player.Gold)}", ConsoleColor.DarkGray);
        Console.WriteLine();

        for (var i = 0; i < shopItems.Count; i++)
        {
            var item = shopItems[i];
            GameConsoleUI.Write($"  [{i + 1}] {item.Name}", ConsoleColor.Yellow);
            GameConsoleUI.Write($" — {item.Description} ({item.Type})", ConsoleColor.DarkGray);
            GameConsoleUI.WriteLine($"  {item.Price}g", ConsoleColor.White);
        }

        // Sell option
        GameConsoleUI.Write($"  [S] ", ConsoleColor.Yellow);
        GameConsoleUI.WriteLine(UIStrings.Get(state.Language, "trade_sell"), ConsoleColor.DarkGray);
        GameConsoleUI.Write($"  [0] ", ConsoleColor.Yellow);
        GameConsoleUI.WriteLine(UIStrings.Get(state.Language, "trade_leave"), ConsoleColor.DarkGray);

        GameConsoleUI.Write($"\n  {UIStrings.Get(state.Language, "trade_choice")}", ConsoleColor.DarkGray);
        var input = Console.ReadLine()?.Trim();

        if (string.Equals(input, "s", StringComparison.OrdinalIgnoreCase) && state.Player.Inventory.Count > 0)
        {
            // Sell
            Console.WriteLine();
            for (var i = 0; i < state.Player.Inventory.Count; i++)
            {
                var inv = state.Player.Inventory[i];
                var sellPrice = inv.SellPrice;
                GameConsoleUI.Write($"  [{i + 1}] {inv.Name}", ConsoleColor.White);
                GameConsoleUI.WriteLine($"{UIStrings.Format(state.Language, "trade_sells_for", sellPrice)}", ConsoleColor.DarkGray);
            }
            GameConsoleUI.Write($"  {UIStrings.Get(state.Language, "trade_sell_prompt")}", ConsoleColor.DarkGray);
            var sellInput = Console.ReadLine()?.Trim();
            if (int.TryParse(sellInput, out var si) && si >= 1 && si <= state.Player.Inventory.Count)
            {
                var sold = state.Player.Inventory[si - 1];
                var price = sold.SellPrice;
                state.Player.Inventory.RemoveAt(si - 1);
                state.Player.Gold += price;
                GameConsoleUI.WriteLine($"  {UIStrings.Format(state.Language, "trade_sold", sold.Name, price, state.Player.Gold)}", ConsoleColor.Green);
                state.AddLog($"Sold {sold.Name} for {price}g.");
                return $"Player sold {sold.Name} for {price} gold.";
            }
        }
        else if (int.TryParse(input, out var buyIdx) && buyIdx >= 1 && buyIdx <= shopItems.Count)
        {
            var toBuy = shopItems[buyIdx - 1];
            if (state.Player.Gold.TrySpend(toBuy.Price, out var remaining))
            {
                state.Player.Gold = remaining;
                state.Player.Inventory.Add(new Item
                {
                    Name = toBuy.Name,
                    Description = toBuy.Description,
                    Type = toBuy.Type,
                    EffectValue = toBuy.EffectValue,
                });
                GameConsoleUI.WriteLine($"  {UIStrings.Format(state.Language, "trade_bought", toBuy.Name, toBuy.Price, state.Player.Gold)}", ConsoleColor.Green);
                state.AddLog($"Bought {toBuy.Name} for {toBuy.Price}g.");
                return $"Player bought {toBuy.Name} for {toBuy.Price} gold.";
            }
            else
            {
                GameConsoleUI.WriteLine($"  {UIStrings.Format(state.Language, "trade_no_gold", toBuy.Price, state.Player.Gold)}", ConsoleColor.Red);
            }
        }

        return $"Player browsed {npc.Name}'s shop.";
    }

    internal static ItemType ParseItemType(string? raw)
        => Enum.TryParse<ItemType>(raw, ignoreCase: true, out var result) ? result : ItemType.Misc;
}
