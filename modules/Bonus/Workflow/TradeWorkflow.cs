using System.Text.Json;

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
        var npc = FindMerchant(choice.Target, state);
        if (npc is null)
            return $"Merchant '{choice.Target}' not found.";

        Console.WriteLine();
        GameConsoleUI.WriteLine($"  {UIStrings.Format(state.Language, "trade_header", npc.Name)}", ConsoleColor.Yellow);

        var (greeting, shopItems) = await GenerateShopInventory(npc, state, config, ct);

        GameConsoleUI.WriteLine($"\n  {npc.Name}: \"{greeting}\"", ConsoleColor.Cyan);

        if (shopItems.Count == 0)
        {
            GameConsoleUI.WriteLine($"  {UIStrings.Get(state.Language, "trade_no_items")}", ConsoleColor.DarkGray);
            return $"Attempted to trade with {npc.Name} but no items were available.";
        }

        PrintShopMenu(shopItems, state);
        var input = Console.ReadLine()?.Trim();

        if (string.Equals(input, "s", StringComparison.OrdinalIgnoreCase) && state.Player.Inventory.Count > 0)
            return HandleSell(state);

        if (int.TryParse(input, out var buyIdx) && buyIdx >= 1 && buyIdx <= shopItems.Count)
            return HandleBuy(shopItems[buyIdx - 1], state);

        return $"Player browsed {npc.Name}'s shop.";
    }

    // ── Find merchant ──

    private static NPC? FindMerchant(string npcId, GameState state)
    {
        if (state.NPCs.TryGetValue(npcId, out var npc))
            return npc;
        return FuzzyFinder.ByName(state.NPCs.Values, npcId, n => n.Name);
    }

    // ── Generate shop inventory via LLM ──

    private static async Task<(string greeting, List<ShopItem> items)> GenerateShopInventory(
        NPC npc, GameState state, AgentConfig config, CancellationToken ct)
    {
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
            shopResponse = await AgentRunner.RunAgent(merchantAgent, merchantPrompt, ct,
                "{\"greeting\": \"Welcome!\", \"items\": []}");
        }

        return ParseShopResponse(shopResponse);
    }

    private static (string greeting, List<ShopItem> items) ParseShopResponse(string response)
    {
        var greeting = "Welcome, adventurer!";
        List<ShopItem> items = [];

        var json = LlmJsonParser.ExtractJson(response);
        if (json is null) return (greeting, items);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            greeting = root.Str("greeting", greeting);
            if (root.TryGetProperty("items", out var arr))
            {
                foreach (var el in arr.EnumerateArray())
                {
                    items.Add(ShopItem.FromRaw(
                        el.Str("name"),
                        el.Str("description"),
                        ParseItemType(el.StrOrNull("type")),
                        el.Int("effect_value"),
                        el.Int("price", 10)));
                }
            }
        }
        catch { /* use defaults */ }

        return (greeting, items);
    }

    // ── Shop UI ──

    private static void PrintShopMenu(List<ShopItem> shopItems, GameState state)
    {
        GameConsoleUI.WriteLine($"  {UIStrings.Format(state.Language, "trade_gold", state.Player.Gold)}", ConsoleColor.DarkGray);
        Console.WriteLine();

        for (var i = 0; i < shopItems.Count; i++)
        {
            var item = shopItems[i];
            GameConsoleUI.Write($"  [{i + 1}] {item.Name}", ConsoleColor.Yellow);
            GameConsoleUI.Write($" — {item.Description} ({item.Type})", ConsoleColor.DarkGray);
            GameConsoleUI.WriteLine($"  {item.Price}g", ConsoleColor.White);
        }

        GameConsoleUI.Write($"  [S] ", ConsoleColor.Yellow);
        GameConsoleUI.WriteLine(UIStrings.Get(state.Language, "trade_sell"), ConsoleColor.DarkGray);
        GameConsoleUI.Write($"  [0] ", ConsoleColor.Yellow);
        GameConsoleUI.WriteLine(UIStrings.Get(state.Language, "trade_leave"), ConsoleColor.DarkGray);

        GameConsoleUI.Write($"\n  {UIStrings.Get(state.Language, "trade_choice")}", ConsoleColor.DarkGray);
    }

    // ── Buy / Sell ──

    private static string HandleBuy(ShopItem toBuy, GameState state)
    {
        if (state.Player.Gold.TrySpend(toBuy.Price, out var remaining))
        {
            state.Player.Gold = remaining;
            state.Player.Inventory.Add(toBuy.Item);
            GameConsoleUI.WriteLine($"  {UIStrings.Format(state.Language, "trade_bought", toBuy.Name, toBuy.Price, state.Player.Gold)}", ConsoleColor.Green);
            state.AddLog($"Bought {toBuy.Name} for {toBuy.Price}g.");
            return $"Player bought {toBuy.Name} for {toBuy.Price} gold.";
        }

        GameConsoleUI.WriteLine($"  {UIStrings.Format(state.Language, "trade_no_gold", toBuy.Price, state.Player.Gold)}", ConsoleColor.Red);
        return $"Player couldn't afford {toBuy.Name}.";
    }

    private static string HandleSell(GameState state)
    {
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
        return "Player decided not to sell anything.";
    }

    internal static ItemType ParseItemType(string? raw)
        => Enum.TryParse<ItemType>(raw, ignoreCase: true, out var result) ? result : ItemType.Misc;
}
