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
    {
        try
        {
            var location = JsonSerializer.Deserialize<Location>(locationJson, AgentHelper.JsonOpts);
            if (location is null || location.Id.IsEmpty)
                return "ERROR: Invalid location JSON or missing id.";

            if (GameStateAccessor.IsLoaded)
                GameStateAccessor.Current.Locations[location.Id] = location;

            return $"OK: Location '{location.Name}' saved with id '{location.Id}'.";
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    [Description("Loads a location by its id. Returns the full JSON of the Location object.")]
    public static string LoadLocation([Description("The location id")] string id)
    {
        try
        {
            if (GameStateAccessor.IsLoaded &&
                GameStateAccessor.Current.Locations.TryGetValue(id, out var loc))
            {
                return JsonSerializer.Serialize(loc, AgentHelper.JsonOpts);
            }

            return $"ERROR: Location '{id}' not found.";
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

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
