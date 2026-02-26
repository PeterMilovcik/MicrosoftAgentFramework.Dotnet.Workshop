namespace RPGGameMaster.UI;

/// <summary>
/// Predefined world themes for character creation.
/// Each theme has a menu label (with emoji) and a full description used for LLM generation.
/// </summary>
internal static class WorldThemes
{
    /// <summary>Menu labels displayed during theme selection.</summary>
    public static readonly IReadOnlyList<(string Label, string Description)> Themes =
    [
        ("🏰 Dark Fantasy — Crumbling castles, ancient curses, and dark forests",
         "Dark Fantasy — a grim world of crumbling castles, ancient curses, dark forests, and undead horrors"),

        ("⚔️  High Fantasy — Dragon lairs, dungeon crawls, and warring kingdoms",
         "High Fantasy — a classic realm of dragon lairs, sprawling dungeons, warring kingdoms, tavern quests, and ancient prophecies"),

        ("👻 Haunted Ruins — Forgotten temples, restless spirits, and deadly traps",
         "Haunted Ruins — a desolate landscape of forgotten temples, restless spirits, deadly traps, and lost civilizations"),

        ("🚀 Sci-Fi Station — Abandoned space station, alien tech, and rogue AI",
         "Sci-Fi Station — an abandoned orbital station with malfunctioning systems, alien technology, rogue AI, and zero-gravity hazards"),

        ("🏴‍☠️ Pirate Archipelago — Tropical islands, sea monsters, and buried treasure",
         "Pirate Archipelago — a chain of tropical islands with sea monsters, buried treasure, rival pirates, and cursed shipwrecks"),

        ("⚙️  Steampunk City — Clockwork machines, airship docks, and industrial intrigue",
         "Steampunk City — a Victorian-era metropolis of steam-powered machines, airship docks, clockwork automatons, smog-choked streets, and industrial espionage"),

        ("🏜️  Desert Tombs — Sandswept pyramids, cursed pharaohs, and hidden oases",
         "Desert Tombs — an ancient desert of sandswept pyramids, cursed pharaoh tombs, scorching dunes, hidden oases, and sand elementals"),

        ("🌊 Sunken Kingdom — Submerged ruins, merfolk, and abyssal leviathans",
         "Sunken Kingdom — a drowned kingdom of coral-encrusted palaces, bioluminescent caverns, merfolk factions, and abyssal leviathans"),

        ("❄️  Frozen Wastes — Ice citadels, frost wraiths, and buried Viking halls",
         "Frozen Wastes — a frozen tundra of ice citadels, howling blizzards, frost wraiths, buried Viking longship halls, and aurora-lit skies"),

        ("🧟 Post-Apocalypse — Overgrown cities, mutant beasts, and scavenger gangs",
         "Post-Apocalypse — a post-apocalyptic wasteland of crumbling skyscrapers, overgrown highways, mutant beasts, scavenger gangs, and pre-war bunkers"),
    ];

    /// <summary>Default theme used as fallback.</summary>
    public const string DefaultDescription = "High Fantasy — a classic world of knights, dragons, wizards, and ancient dungeons";

    /// <summary>Get theme description by 1-based index, or null if out of range.</summary>
    public static string? GetDescription(int index)
        => index >= 1 && index <= Themes.Count ? Themes[index - 1].Description : null;
}
