namespace RPGGameMaster;

/// <summary>
/// Lightweight localization for hardcoded UI strings (menus, labels, status messages).
/// LLM-generated content is localized via prompt instructions — this covers only the C# chrome.
/// English is the fallback for any missing key or language.
/// </summary>
internal static class UIStrings
{
    /// <summary>Get a translated string for the given language and key. Falls back to English.</summary>
    public static string Get(string language, string key)
    {
        if (Translations.TryGetValue(language, out var langDict) && langDict.TryGetValue(key, out var val))
            return val;
        if (Translations["English"].TryGetValue(key, out var en))
            return en;
        return key; // ultimate fallback: return the key itself
    }

    /// <summary>Format a translated string with positional arguments.</summary>
    public static string Format(string language, string key, params object[] args)
        => string.Format(Get(language, key), args);

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Translation dictionaries
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private static readonly Dictionary<string, Dictionary<string, string>> Translations = new()
    {
        ["English"] = new()
        {
            // Starting items
            ["item_rusty_sword"] = "Rusty Sword",
            ["item_rusty_sword_desc"] = "A well-worn blade, still sharp enough",
            ["item_healing_potion"] = "Minor Healing Potion",
            ["item_healing_potion_desc"] = "A small vial of red liquid",

            // Combat UI
            ["combat_header"] = "⚔  COMBAT: {0}",
            ["combat_stats"] = "HP: {0}/{1} | Atk: {2} | Def: {3} | Difficulty: {4}",
            ["combat_round"] = "── Round {0} ──",
            ["combat_defeated"] = "🎉 {0} has been defeated!",
            ["combat_slain"] = "☠ You have been slain by {0}...",
            ["combat_fled"] = "🏃 You fled from combat!",
            ["combat_loot"] = "Loot gained:",
            ["combat_xp"] = "+{0} XP (total: {1}/{2})",
            ["combat_no_potions"] = "No potions available! Using a basic attack instead.",
            ["combat_prompt"] = "Combat > ",
            ["combat_enter_num"] = "Enter 1-{0}.",
            ["combat_you_hp"] = "❤ You: {0}/{1} HP",
            ["combat_creature_hp"] = "💀 {0}: {1}/{2} HP",

            // Combat dice
            ["dice_attack"] = "🎲 Attack: {0}",
            ["dice_hit"] = " — HIT!",
            ["dice_miss"] = " — MISS",
            ["dice_crit"] = " CRITICAL!",
            ["dice_damage_dealt"] = "     Damage dealt: {0}",
            ["dice_self_damage"] = "     Self-damage: {0}",
            ["dice_defend"] = "🎲 Defensive stance — incoming damage halved",
            ["dice_counter"] = "     Counter-attack: {0} damage!",
            ["dice_counter_miss"] = "     Counter roll: {0} — no opening",
            ["dice_flee"] = "🎲 Flee: {0}",
            ["dice_escaped"] = " — ESCAPED!",
            ["dice_blocked"] = " — BLOCKED!",
            ["dice_potion"] = "🧪 {0}: +{1} HP",
            ["dice_creature_atk"] = "🎲 {0}: {1}",
            ["dice_damage_taken"] = "     Damage taken: {0}",
            ["dice_damage_blocked"] = " — blocked (took {0})",

            // Fallback combat moves
            ["move_quick_strike"] = "Quick Strike",
            ["move_quick_strike_desc"] = "A reliable strike aimed at {0}",
            ["move_defensive"] = "Defensive Stance",
            ["move_defensive_desc"] = "Brace for impact and look for an opening to counter",
            ["move_disengage"] = "Disengage",
            ["move_disengage_desc"] = "Attempt to break away from combat",
            ["move_drink_potion"] = "Drink Potion",
            ["move_drink_potion_desc"] = "Quickly down a healing potion",

            // Dialogue
            ["dialogue_header"] = "💬 Conversation with {0}",
            ["dialogue_quest_accepted"] = "📜 Quest accepted: {0}!",
            ["dialogue_farewell"] = "You bid farewell to {0}.",
            ["dialogue_leave_quest"] = "You take your leave from {0}, ready to pursue the quest.",
            ["dialogue_prompt"] = "Say > ",
            ["dialogue_enter_num"] = "Enter 1-{0}.",
            ["dialogue_ask_elaborate"] = "Ask {0} to elaborate on what they just said.",
            ["dialogue_ask_role"] = "Ask about {0}'s role in this place.",
            ["dialogue_end"] = "End conversation.",

            // Game state / HUD
            ["level_up"] = "⬆️  LEVEL UP! You are now level {0}!",
            ["level_stats"] = "HP: {0}/{1} | Attack: {2} | Defense: {3}",
            ["picked_up"] = "🎒 Picked up: {0} — {1}",
            ["used_item"] = "🧪 Used {0}: healed {1} HP (now {2}/{3})",
            ["item_equipped"] = "{0} is equipped passively — it's already boosting your stats.",
            ["inventory_header"] = "🎒 Inventory:",
            ["inventory_empty"] = "(empty)",
            ["inventory_use_prompt"] = "Use a potion? Enter item number or 0 to close > ",
            ["inventory_close"] = "Press Enter to close inventory > ",
            ["map_header"] = "🗺️  Discovered Locations:",
            ["map_total"] = "Total: {0} locations discovered.",
            ["quest_complete"] = "✅ Quest Complete: {0}!",
            ["quest_gold"] = "+{0} gold",
            ["quest_xp"] = "+{0} XP",
            ["quest_item"] = "+{0}",
            ["quests_header"] = "📜 Active Quests:",
            ["quests_none"] = "No active quests.",
            ["stats_header"] = "📊 Player Stats:",
            ["save_confirmed"] = "💾 Game saved!",
            ["death_header"] = "☠  YOU HAVE FALLEN  ☠",
            ["death_respawn"] = "You awaken back at {0}...",
            ["death_penalty"] = "Lost: {0} gold, {1} XP",
            ["death_restored"] = "HP fully restored to {0}/{1}",
            ["help_header"] = "📖 Available Commands:",
            ["examine_header"] = "🔍 {0}",
            ["rest_healed"] = "💤 You rest and recover {0} HP (now {1}/{2})",
            ["trade_header"] = "🪙 Trading with {0}",
            ["trade_no_items"] = "(No items available for trade.)",
            ["trade_gold"] = "Your gold: {0}",
            ["trade_sell"] = "Sell an item from your inventory",
            ["trade_leave"] = "Leave shop",
            ["trade_choice"] = "Your choice > ",
            ["trade_sell_prompt"] = "Sell which? > ",
            ["trade_sold"] = "Sold {0} for {1}g (now {2}g)",
            ["trade_bought"] = "Bought {0} for {1}g (remaining: {2}g)",
            ["trade_no_gold"] = "Not enough gold! Need {0}g, have {1}g.",
            ["trade_welcome"] = "Welcome, adventurer!",
            ["trade_sells_for"] = " — sells for {0}g",

            // Fallback presentation
            ["opt_look_around"] = "Look around",
            ["opt_inventory"] = "Open inventory",
            ["opt_quests"] = "Check quests",
            ["opt_map"] = "View map",
            ["opt_save"] = "Save game",
            ["opt_quit"] = "Quit",
            ["input_prompt"] = "Choose > ",
            ["input_help_hint"] = "(type map, inv, quests, stats, exit, or ? for help)",
            ["input_enter_num"] = "Enter a number ({0}-{1}) or a command (type ? for help).",

            // Help descriptions
            ["help_map"] = "Show discovered locations",
            ["help_inv"] = "Open inventory (also: inventory, i)",
            ["help_quests"] = "Show active quests (also: q)",
            ["help_stats"] = "Show player stats (also: s)",
            ["help_help"] = "Show this help (also: help)",
            ["help_exit"] = "Save and quit the game (also: quit)",
            ["help_number"] = "Or enter a number to pick an option.",

            // Stats display
            ["stat_name"] = "Name: {0}",
            ["stat_level"] = "Level: {0}",
            ["stat_hp"] = "HP: {0}/{1}",
            ["stat_attack"] = "Attack: {0} (base: {1})",
            ["stat_defense"] = "Defense: {0} (base: {1})",
            ["stat_xp"] = "XP: {0}/{1}",
            ["stat_gold"] = "Gold: {0}",

            // Inventory type suffixes
            ["inv_atk_bonus"] = " (+{0} atk)",
            ["inv_def_bonus"] = " (+{0} def)",
            ["inv_heals"] = " (heals {0})",
        },

        ["German"] = new()
        {
            ["item_rusty_sword"] = "Rostiges Schwert",
            ["item_rusty_sword_desc"] = "Eine abgenutzte Klinge, aber noch scharf genug",
            ["item_healing_potion"] = "Kleiner Heiltrank",
            ["item_healing_potion_desc"] = "Ein kleines Fläschchen mit roter Flüssigkeit",
            ["combat_header"] = "⚔  KAMPF: {0}",
            ["combat_round"] = "── Runde {0} ──",
            ["combat_defeated"] = "🎉 {0} wurde besiegt!",
            ["combat_slain"] = "☠ Du wurdest von {0} erschlagen...",
            ["combat_fled"] = "🏃 Du bist aus dem Kampf geflohen!",
            ["combat_loot"] = "Beute erhalten:",
            ["combat_no_potions"] = "Keine Tränke verfügbar! Greife stattdessen an.",
            ["combat_prompt"] = "Kampf > ",
            ["move_quick_strike"] = "Schneller Hieb",
            ["move_quick_strike_desc"] = "Ein zuverlässiger Angriff auf {0}",
            ["move_defensive"] = "Verteidigungshaltung",
            ["move_defensive_desc"] = "Bereite dich auf den Aufprall vor und suche eine Öffnung",
            ["move_disengage"] = "Rückzug",
            ["move_disengage_desc"] = "Versuche, aus dem Kampf auszubrechen",
            ["move_drink_potion"] = "Trank trinken",
            ["move_drink_potion_desc"] = "Trinke schnell einen Heiltrank",
            ["dialogue_header"] = "💬 Gespräch mit {0}",
            ["dialogue_quest_accepted"] = "📜 Quest angenommen: {0}!",
            ["dialogue_farewell"] = "Du verabschiedest dich von {0}.",
            ["dialogue_leave_quest"] = "Du verlässt {0}, bereit das Abenteuer zu verfolgen.",
            ["dialogue_prompt"] = "Sag > ",
            ["dialogue_end"] = "Gespräch beenden.",
            ["level_up"] = "⬆️  AUFSTIEG! Du bist jetzt Stufe {0}!",
            ["picked_up"] = "🎒 Aufgehoben: {0} — {1}",
            ["inventory_header"] = "🎒 Inventar:",
            ["inventory_empty"] = "(leer)",
            ["map_header"] = "🗺️  Entdeckte Orte:",
            ["quest_complete"] = "✅ Quest abgeschlossen: {0}!",
            ["quests_header"] = "📜 Aktive Quests:",
            ["quests_none"] = "Keine aktiven Quests.",
            ["save_confirmed"] = "💾 Spiel gespeichert!",
            ["death_header"] = "☠  DU BIST GEFALLEN  ☠",
            ["opt_look_around"] = "Umschauen",
            ["opt_inventory"] = "Inventar öffnen",
            ["opt_quests"] = "Quests prüfen",
            ["opt_map"] = "Karte anzeigen",
            ["opt_save"] = "Spiel speichern",
            ["opt_quit"] = "Beenden",
            ["trade_header"] = "🪙 Handel mit {0}",
            ["trade_leave"] = "Laden verlassen",
            ["rest_healed"] = "💤 Du ruhst dich aus und erholst {0} HP (jetzt {1}/{2})",
        },

        ["French"] = new()
        {
            ["item_rusty_sword"] = "Épée rouillée",
            ["item_rusty_sword_desc"] = "Une lame usée, encore assez tranchante",
            ["item_healing_potion"] = "Petite potion de soin",
            ["item_healing_potion_desc"] = "Un petit flacon de liquide rouge",
            ["combat_header"] = "⚔  COMBAT : {0}",
            ["combat_defeated"] = "🎉 {0} a été vaincu !",
            ["combat_slain"] = "☠ Vous avez été tué par {0}...",
            ["combat_fled"] = "🏃 Vous avez fui le combat !",
            ["dialogue_header"] = "💬 Conversation avec {0}",
            ["dialogue_farewell"] = "Vous faites vos adieux à {0}.",
            ["dialogue_end"] = "Mettre fin à la conversation.",
            ["opt_look_around"] = "Regarder autour",
            ["opt_inventory"] = "Ouvrir l'inventaire",
            ["opt_quit"] = "Quitter",
            ["save_confirmed"] = "💾 Partie sauvegardée !",
        },

        ["Spanish"] = new()
        {
            ["item_rusty_sword"] = "Espada oxidada",
            ["item_rusty_sword_desc"] = "Una hoja desgastada, pero aún afilada",
            ["item_healing_potion"] = "Poción de curación menor",
            ["item_healing_potion_desc"] = "Un pequeño frasco de líquido rojo",
            ["combat_header"] = "⚔  COMBATE: {0}",
            ["combat_defeated"] = "🎉 ¡{0} ha sido derrotado!",
            ["combat_slain"] = "☠ Has sido derrotado por {0}...",
            ["combat_fled"] = "🏃 ¡Has huido del combate!",
            ["dialogue_header"] = "💬 Conversación con {0}",
            ["dialogue_farewell"] = "Te despides de {0}.",
            ["dialogue_end"] = "Terminar conversación.",
            ["opt_look_around"] = "Mirar alrededor",
            ["opt_quit"] = "Salir",
            ["save_confirmed"] = "💾 ¡Partida guardada!",
        },

        ["Italian"] = new()
        {
            ["item_rusty_sword"] = "Spada arrugginita",
            ["item_rusty_sword_desc"] = "Una lama consumata, ma ancora affilata",
            ["item_healing_potion"] = "Pozione curativa minore",
            ["item_healing_potion_desc"] = "Una piccola fiala di liquido rosso",
            ["combat_header"] = "⚔  COMBATTIMENTO: {0}",
            ["combat_defeated"] = "🎉 {0} è stato sconfitto!",
            ["dialogue_header"] = "💬 Conversazione con {0}",
            ["dialogue_end"] = "Termina conversazione.",
            ["opt_quit"] = "Esci",
            ["save_confirmed"] = "💾 Partita salvata!",
        },

        ["Portuguese"] = new()
        {
            ["item_rusty_sword"] = "Espada enferrujada",
            ["item_rusty_sword_desc"] = "Uma lâmina gasta, mas ainda afiada",
            ["item_healing_potion"] = "Poção de cura menor",
            ["item_healing_potion_desc"] = "Um pequeno frasco de líquido vermelho",
            ["combat_header"] = "⚔  COMBATE: {0}",
            ["combat_defeated"] = "🎉 {0} foi derrotado!",
            ["dialogue_header"] = "💬 Conversa com {0}",
            ["dialogue_end"] = "Encerrar conversa.",
            ["save_confirmed"] = "💾 Jogo salvo!",
        },

        ["Dutch"] = new()
        {
            ["item_rusty_sword"] = "Roestig zwaard",
            ["item_rusty_sword_desc"] = "Een versleten kling, maar nog scherp genoeg",
            ["item_healing_potion"] = "Kleine heeldrank",
            ["item_healing_potion_desc"] = "Een klein flesje rode vloeistof",
            ["combat_header"] = "⚔  GEVECHT: {0}",
            ["combat_defeated"] = "🎉 {0} is verslagen!",
            ["dialogue_header"] = "💬 Gesprek met {0}",
            ["dialogue_end"] = "Gesprek beëindigen.",
            ["save_confirmed"] = "💾 Spel opgeslagen!",
        },

        ["Polish"] = new()
        {
            ["item_rusty_sword"] = "Zardzewiały miecz",
            ["item_rusty_sword_desc"] = "Zużyte ostrze, wciąż wystarczająco ostre",
            ["item_healing_potion"] = "Mniejsza mikstura lecznicza",
            ["item_healing_potion_desc"] = "Mała fiolka czerwonego płynu",
            ["combat_header"] = "⚔  WALKA: {0}",
            ["combat_defeated"] = "🎉 {0} został pokonany!",
            ["combat_slain"] = "☠ Zostałeś pokonany przez {0}...",
            ["dialogue_header"] = "💬 Rozmowa z {0}",
            ["dialogue_farewell"] = "Żegnasz się z {0}.",
            ["dialogue_end"] = "Zakończ rozmowę.",
            ["opt_look_around"] = "Rozejrzyj się",
            ["save_confirmed"] = "💾 Gra zapisana!",
        },

        ["Czech"] = new()
        {
            ["item_rusty_sword"] = "Rezavý meč",
            ["item_rusty_sword_desc"] = "Opotřebovaná čepel, stále dost ostrá",
            ["item_healing_potion"] = "Malý léčivý lektvar",
            ["item_healing_potion_desc"] = "Malá lahvička s červenou tekutinou",
            ["combat_header"] = "⚔  BOJ: {0}",
            ["combat_defeated"] = "🎉 {0} byl poražen!",
            ["dialogue_header"] = "💬 Rozhovor s {0}",
            ["dialogue_end"] = "Ukončit rozhovor.",
            ["save_confirmed"] = "💾 Hra uložena!",
        },

        ["Slovak"] = new()
        {
            ["item_rusty_sword"] = "Hrdzavý meč",
            ["item_rusty_sword_desc"] = "Opotrebovaná čepeľ, stále dosť ostrá",
            ["item_healing_potion"] = "Malý liečivý elixír",
            ["item_healing_potion_desc"] = "Malá fľaštička s červenou tekutinou",
            ["combat_header"] = "⚔  BOJ: {0}",
            ["combat_round"] = "── Kolo {0} ──",
            ["combat_defeated"] = "🎉 {0} bol porazený!",
            ["combat_slain"] = "☠ Bol si zabitý {0}...",
            ["combat_fled"] = "🏃 Utiekol si z boja!",
            ["combat_loot"] = "Získaná korisť:",
            ["combat_no_potions"] = "Žiadne elixíry! Útočíš namiesto toho.",
            ["combat_prompt"] = "Boj > ",
            ["move_quick_strike"] = "Rýchly úder",
            ["move_quick_strike_desc"] = "Spoľahlivý útok na {0}",
            ["move_defensive"] = "Obranný postoj",
            ["move_defensive_desc"] = "Priprav sa na útok a hľadaj otvor",
            ["move_disengage"] = "Ústup",
            ["move_disengage_desc"] = "Pokús sa uniknúť z boja",
            ["move_drink_potion"] = "Vypiť elixír",
            ["move_drink_potion_desc"] = "Rýchlo vypi liečivý elixír",
            ["dialogue_header"] = "💬 Rozhovor s {0}",
            ["dialogue_quest_accepted"] = "📜 Úloha prijatá: {0}!",
            ["dialogue_farewell"] = "Rozlúčiš sa s {0}.",
            ["dialogue_leave_quest"] = "Odchádzaš od {0}, pripravený na dobrodružstvo.",
            ["dialogue_prompt"] = "Povedz > ",
            ["dialogue_end"] = "Ukončiť rozhovor.",
            ["level_up"] = "⬆️  POSTUP! Si teraz úroveň {0}!",
            ["picked_up"] = "🎒 Zdvihnuté: {0} — {1}",
            ["inventory_header"] = "🎒 Inventár:",
            ["inventory_empty"] = "(prázdny)",
            ["map_header"] = "🗺️  Objavené lokácie:",
            ["quest_complete"] = "✅ Úloha splnená: {0}!",
            ["quests_header"] = "📜 Aktívne úlohy:",
            ["quests_none"] = "Žiadne aktívne úlohy.",
            ["save_confirmed"] = "💾 Hra uložená!",
            ["death_header"] = "☠  PADOL SI  ☠",
            ["death_respawn"] = "Prebúdzaš sa späť v {0}...",
            ["opt_look_around"] = "Rozhliadnuť sa",
            ["opt_inventory"] = "Otvoriť inventár",
            ["opt_quests"] = "Skontrolovať úlohy",
            ["opt_map"] = "Zobraziť mapu",
            ["opt_save"] = "Uložiť hru",
            ["opt_quit"] = "Ukončiť",
            ["trade_header"] = "🪙 Obchod s {0}",
            ["trade_leave"] = "Odísť z obchodu",
            ["rest_healed"] = "💤 Odpočívaš a obnovíš {0} HP (teraz {1}/{2})",
        },

        ["Ukrainian"] = new()
        {
            ["item_rusty_sword"] = "Іржавий меч",
            ["item_rusty_sword_desc"] = "Зношений клинок, але ще досить гострий",
            ["item_healing_potion"] = "Мале зілля зцілення",
            ["item_healing_potion_desc"] = "Маленька пляшечка з червоною рідиною",
            ["combat_header"] = "⚔  БІЙ: {0}",
            ["combat_defeated"] = "🎉 {0} переможений!",
            ["combat_slain"] = "☠ Тебе вбив {0}...",
            ["dialogue_header"] = "💬 Розмова з {0}",
            ["dialogue_end"] = "Завершити розмову.",
            ["save_confirmed"] = "💾 Гру збережено!",
        },

        ["Japanese"] = new()
        {
            ["item_rusty_sword"] = "錆びた剣",
            ["item_rusty_sword_desc"] = "使い古された刃、まだ十分に鋭い",
            ["item_healing_potion"] = "小回復薬",
            ["item_healing_potion_desc"] = "赤い液体の小瓶",
            ["combat_header"] = "⚔  戦闘: {0}",
            ["combat_defeated"] = "🎉 {0}を倒した！",
            ["combat_slain"] = "☠ {0}に倒された...",
            ["dialogue_header"] = "💬 {0}との会話",
            ["dialogue_end"] = "会話を終了する。",
            ["save_confirmed"] = "💾 ゲームを保存しました！",
        },

        ["Korean"] = new()
        {
            ["item_rusty_sword"] = "녹슨 검",
            ["item_rusty_sword_desc"] = "닳은 칼날이지만 아직 충분히 날카롭다",
            ["item_healing_potion"] = "소형 치유 물약",
            ["item_healing_potion_desc"] = "빨간 액체가 담긴 작은 병",
            ["combat_header"] = "⚔  전투: {0}",
            ["combat_defeated"] = "🎉 {0}을(를) 물리쳤다!",
            ["combat_slain"] = "☠ {0}에게 쓰러졌다...",
            ["dialogue_header"] = "💬 {0}와의 대화",
            ["dialogue_end"] = "대화 종료.",
            ["save_confirmed"] = "💾 게임이 저장되었습니다!",
        },
    };
}
