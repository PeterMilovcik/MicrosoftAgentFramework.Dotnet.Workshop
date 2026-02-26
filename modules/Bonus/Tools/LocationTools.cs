using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.AI;
using RPGGameMaster.Models;
using RPGGameMaster.Workflow;

namespace RPGGameMaster.Tools;

/// <summary>
/// Tools for saving, loading, and listing world locations.
/// Assigned to the World Architect agent.
/// All operations target the in-memory GameState — disk persistence is
/// handled by <see cref="SaveManager"/>.
/// </summary>
internal static class LocationTools
{
    [Description("Saves a location to persistent storage. Input: the full JSON of the Location object.")]
    public static string SaveLocation([Description("Full JSON of the Location object")] string locationJson)
        => ToolHelper.Save<Location>(locationJson, gs => gs.Locations, "Location");

    [Description("Loads a location by its id. Returns the full JSON of the Location object.")]
    public static string LoadLocation([Description("The location id")] string id)
        => ToolHelper.Load<Location>(id, gs => gs.Locations, "Location");

    [Description("Lists all saved locations. Returns a JSON array of {id, name} objects.")]
    public static string ListLocations()
    {
        try
        {
            if (!GameStateAccessor.IsLoaded)
                return "[]";

            var summaries = GameStateAccessor.Current.Locations.Values
                .Select(loc => new { loc.Id, loc.Name, loc.Theme, ExitCount = loc.Exits.Count })
                .ToList();

            return JsonSerializer.Serialize(summaries, AgentHelper.JsonOpts);
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    public static IList<AITool> GetTools() =>
    [
        AIFunctionFactory.Create(SaveLocation),
        AIFunctionFactory.Create(LoadLocation),
        AIFunctionFactory.Create(ListLocations),
    ];
}
