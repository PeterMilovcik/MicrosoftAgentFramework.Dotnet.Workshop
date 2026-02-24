using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.AI;
using RPGGameMaster.Models;

namespace RPGGameMaster.Tools;

/// <summary>
/// Tools for saving, loading, and listing world locations.
/// Assigned to the World Architect agent.
/// </summary>
internal static class LocationTools
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string LocationsDir
    {
        get
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "game-data", "locations");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    [Description("Saves a location to persistent storage. Input: the full JSON of the Location object.")]
    public static string SaveLocation([Description("Full JSON of the Location object")] string locationJson)
    {
        try
        {
            var location = JsonSerializer.Deserialize<Location>(locationJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (location is null || string.IsNullOrWhiteSpace(location.Id))
                return "ERROR: Invalid location JSON or missing id.";

            var path = Path.Combine(LocationsDir, $"{location.Id}.json");
            File.WriteAllText(path, JsonSerializer.Serialize(location, JsonOptions));
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
            var path = Path.Combine(LocationsDir, $"{id}.json");
            if (!File.Exists(path))
                return $"ERROR: Location '{id}' not found.";
            return File.ReadAllText(path);
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
            var files = Directory.GetFiles(LocationsDir, "*.json");
            var summaries = new List<object>();
            foreach (var file in files)
            {
                var loc = JsonSerializer.Deserialize<Location>(File.ReadAllText(file), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (loc is not null)
                    summaries.Add(new { loc.Id, loc.Name, loc.Theme, ExitCount = loc.Exits.Count });
            }
            return JsonSerializer.Serialize(summaries, JsonOptions);
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
