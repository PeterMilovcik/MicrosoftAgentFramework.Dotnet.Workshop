using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.AI;
using RPGGameMaster.Models;
using RPGGameMaster.Workflow;

namespace RPGGameMaster.Tools;

/// <summary>
/// Tools for saving, loading, and querying creatures.
/// Assigned to the Creature Forger agent.
/// All operations target the in-memory GameState — disk persistence is
/// handled by <see cref="SaveManager"/>.
/// </summary>
internal static class CreatureTools
{
    [Description("Saves a creature to persistent storage. Input: the full JSON of the Creature object.")]
    public static string SaveCreature([Description("Full JSON of the Creature object")] string creatureJson)
    {
        try
        {
            var creature = JsonSerializer.Deserialize<Creature>(creatureJson, AgentHelper.JsonOpts);
            if (creature is null || creature.Id.IsEmpty)
                return "ERROR: Invalid creature JSON or missing id.";

            if (GameStateAccessor.IsLoaded)
                GameStateAccessor.Current.Creatures[creature.Id] = creature;

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
            if (GameStateAccessor.IsLoaded &&
                GameStateAccessor.Current.Creatures.TryGetValue(id, out var creature))
            {
                return JsonSerializer.Serialize(creature, AgentHelper.JsonOpts);
            }

            return $"ERROR: Creature '{id}' not found.";
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
            if (!GameStateAccessor.IsLoaded)
                return "[]";

            var creatures = GameStateAccessor.Current.Creatures.Values
                .Where(c => c.LocationId == locationId && !c.IsDefeated)
                .ToList();

            return JsonSerializer.Serialize(creatures, AgentHelper.JsonOpts);
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
