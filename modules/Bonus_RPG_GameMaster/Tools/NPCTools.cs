using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.AI;
using RPGGameMaster.Models;

namespace RPGGameMaster.Tools;

/// <summary>
/// Tools for saving, loading, and querying NPCs.
/// Assigned to the NPC Weaver agent.
/// </summary>
internal static class NPCTools
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string NPCsDir
    {
        get
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "game-data", "npcs");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    [Description("Saves an NPC to persistent storage. Input: the full JSON of the NPC object including agent_instructions.")]
    public static string SaveNPC([Description("Full JSON of the NPC object")] string npcJson)
    {
        try
        {
            var npc = JsonSerializer.Deserialize<NPC>(npcJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (npc is null || string.IsNullOrWhiteSpace(npc.Id))
                return "ERROR: Invalid NPC JSON or missing id.";

            var path = Path.Combine(NPCsDir, $"{npc.Id}.json");
            File.WriteAllText(path, JsonSerializer.Serialize(npc, JsonOptions));
            return $"OK: NPC '{npc.Name}' saved with id '{npc.Id}'.";
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    [Description("Loads an NPC by its id. Returns the full JSON of the NPC object.")]
    public static string LoadNPC([Description("The NPC id")] string id)
    {
        try
        {
            var path = Path.Combine(NPCsDir, $"{id}.json");
            if (!File.Exists(path))
                return $"ERROR: NPC '{id}' not found.";
            return File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    [Description("Loads all NPCs at a given location. Returns a JSON array of NPC objects.")]
    public static string LoadNPCsAtLocation([Description("The location id to filter by")] string locationId)
    {
        try
        {
            var files = Directory.GetFiles(NPCsDir, "*.json");
            var npcs = new List<NPC>();
            foreach (var file in files)
            {
                var npc = JsonSerializer.Deserialize<NPC>(File.ReadAllText(file), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (npc is not null && npc.LocationId == locationId)
                    npcs.Add(npc);
            }
            return JsonSerializer.Serialize(npcs, JsonOptions);
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    public static IList<AITool> GetTools() =>
    [
        AIFunctionFactory.Create(SaveNPC),
        AIFunctionFactory.Create(LoadNPC),
        AIFunctionFactory.Create(LoadNPCsAtLocation),
    ];
}
