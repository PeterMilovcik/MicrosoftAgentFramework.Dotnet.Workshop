using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.AI;
namespace RPGGameMaster.Tools;

/// <summary>
/// Tools for saving, loading, and querying NPCs.
/// Assigned to the NPC Weaver agent.
/// All operations target the in-memory GameState — disk persistence is
/// handled by <see cref="SaveManager"/>.
/// </summary>
internal static class NPCTools
{
    [Description("Saves an NPC to persistent storage. Input: the full JSON of the NPC object including agent_instructions.")]
    public static string SaveNPC([Description("Full JSON of the NPC object")] string npcJson)
        => GameStateRepository.Save<NPC>(npcJson, gs => gs.NPCs, "NPC");

    [Description("Loads an NPC by its id. Returns the full JSON of the NPC object.")]
    public static string LoadNPC([Description("The NPC id")] string id)
        => GameStateRepository.Load<NPC>(id, gs => gs.NPCs, "NPC");

    [Description("Loads all NPCs at a given location. Returns a JSON array of NPC objects.")]
    public static string LoadNPCsAtLocation([Description("The location id to filter by")] string locationId)
    {
        try
        {
            if (!GameStateAccessor.IsLoaded)
                return "[]";

            var npcs = GameStateAccessor.Current.NPCs.Values
                .Where(n => n.LocationId == locationId)
                .ToList();

            return JsonSerializer.Serialize(npcs, LlmJsonParser.JsonOpts);
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
