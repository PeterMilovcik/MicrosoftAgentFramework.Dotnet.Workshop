using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.AI;
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
        => GameStateRepository.Save<Creature>(creatureJson, gs => gs.Creatures, "Creature");

    [Description("Loads a creature by its id. Returns the full JSON of the Creature object.")]
    public static string LoadCreature([Description("The creature id")] string id)
        => GameStateRepository.Load<Creature>(id, gs => gs.Creatures, "Creature");

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

            return JsonSerializer.Serialize(creatures, LlmJsonParser.JsonOpts);
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
