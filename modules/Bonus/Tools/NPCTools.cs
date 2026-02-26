using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.AI;
using RPGGameMaster.Models;
using RPGGameMaster.Workflow;

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
    {
        try
        {
            var npc = JsonSerializer.Deserialize<NPC>(npcJson, AgentHelper.JsonOpts);
            if (npc is null || npc.Id.IsEmpty)
                return "ERROR: Invalid NPC JSON or missing id.";

            if (GameStateAccessor.IsLoaded)
                GameStateAccessor.Current.NPCs[npc.Id] = npc;

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
            if (GameStateAccessor.IsLoaded &&
                GameStateAccessor.Current.NPCs.TryGetValue(id, out var npc))
            {
                return JsonSerializer.Serialize(npc, AgentHelper.JsonOpts);
            }

            return $"ERROR: NPC '{id}' not found.";
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
            if (!GameStateAccessor.IsLoaded)
                return "[]";

            var npcs = GameStateAccessor.Current.NPCs.Values
                .Where(n => n.LocationId == locationId)
                .ToList();

            return JsonSerializer.Serialize(npcs, AgentHelper.JsonOpts);
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
