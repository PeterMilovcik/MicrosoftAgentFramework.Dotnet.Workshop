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
                var sellPrice = inv.SellPrice;
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
                var price = sold.SellPrice;
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

    internal static ItemType ParseItemType(string? raw)
        => Enum.TryParse<ItemType>(raw, ignoreCase: true, out var result) ? result : ItemType.Misc;
}
