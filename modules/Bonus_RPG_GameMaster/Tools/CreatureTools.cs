using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.AI;
using RPGGameMaster.Models;

namespace RPGGameMaster.Tools;

/// <summary>
/// Tools for saving, loading, and querying creatures.
/// Assigned to the Creature Forger agent.
/// </summary>
internal static class CreatureTools
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string CreaturesDir
    {
        get
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "game-data", "creatures");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    [Description("Saves a creature to persistent storage. Input: the full JSON of the Creature object.")]
    public static string SaveCreature([Description("Full JSON of the Creature object")] string creatureJson)
    {
        try
        {
            var creature = JsonSerializer.Deserialize<Creature>(creatureJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (creature is null || string.IsNullOrWhiteSpace(creature.Id))
                return "ERROR: Invalid creature JSON or missing id.";

            var path = Path.Combine(CreaturesDir, $"{creature.Id}.json");
            File.WriteAllText(path, JsonSerializer.Serialize(creature, JsonOptions));
            return $"OK: Creature '{creature.Name}' saved with id '{creature.Id}'.";
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    [Description("Loads a creature by its id. Returns the full JSON of the Creature object.")]
    public static string LoadCreature([Description("The creature id")] string id)
    {
        try
        {
            var path = Path.Combine(CreaturesDir, $"{id}.json");
            if (!File.Exists(path))
                return $"ERROR: Creature '{id}' not found.";
            return File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    [Description("Loads all creatures at a given location. Returns a JSON array of Creature objects.")]
    public static string LoadCreaturesAtLocation([Description("The location id to filter by")] string locationId)
    {
        try
        {
            var files = Directory.GetFiles(CreaturesDir, "*.json");
            var creatures = new List<Creature>();
            foreach (var file in files)
            {
                var creature = JsonSerializer.Deserialize<Creature>(File.ReadAllText(file), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (creature is not null && creature.LocationId == locationId && !creature.IsDefeated)
                    creatures.Add(creature);
            }
            return JsonSerializer.Serialize(creatures, JsonOptions);
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    public static IList<AITool> GetTools() =>
    [
        AIFunctionFactory.Create(SaveCreature),
        AIFunctionFactory.Create(LoadCreature),
        AIFunctionFactory.Create(LoadCreaturesAtLocation),
    ];
}
