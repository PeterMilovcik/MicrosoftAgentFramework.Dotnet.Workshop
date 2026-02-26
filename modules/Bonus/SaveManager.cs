using System.Text.Json;
using RPGGameMaster.Models;
using RPGGameMaster.Workflow;

namespace RPGGameMaster;

/// <summary>
/// Manages all save-game file I/O: saving, loading, listing, deleting,
/// and legacy migration.  Extracted from <c>GameMasterWorkflow</c> (SRP).
/// </summary>
internal static class SaveManager
{
    private static string SavesDir
    {
        get
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "game-data", "saves");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>Saves the game and prints a confirmation message.</summary>
    public static void Save(GameState state)
    {
        state.LastSavedAt = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(state, AgentHelper.JsonOpts);
        File.WriteAllText(Path.Combine(SavesDir, state.GetSaveFileName()), json);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n{UIStrings.Get(state.Language, "save_confirmed")}");
        Console.ResetColor();
    }

    /// <summary>Silent auto-save — no console output to avoid cluttering gameplay.</summary>
    public static void AutoSave(GameState state)
    {
        try
        {
            state.LastSavedAt = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(state, AgentHelper.JsonOpts);
            File.WriteAllText(Path.Combine(SavesDir, state.GetSaveFileName()), json);
        }
        catch { /* best-effort — don't crash the game loop */ }
    }

    /// <summary>
    /// Lists all saved games found on disk.
    /// Returns a list of (filePath, GameState) tuples, sorted by LastSavedAt descending.
    /// </summary>
    public static List<(string Path, GameState State)> ListSaves()
    {
        var results = new List<(string, GameState)>();

        foreach (var file in Directory.GetFiles(SavesDir, "save_*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var state = JsonSerializer.Deserialize<GameState>(json, AgentHelper.JsonOpts);
                if (state is not null)
                    results.Add((file, state));
            }
            catch { /* skip corrupt saves */ }
        }

        // Also load legacy save.json if it exists (migration)
        var legacyPath = Path.Combine(SavesDir, "save.json");
        if (File.Exists(legacyPath) && !results.Any(r => r.Item1 == legacyPath))
        {
            try
            {
                var json = File.ReadAllText(legacyPath);
                var state = JsonSerializer.Deserialize<GameState>(json, AgentHelper.JsonOpts);
                if (state is not null)
                    results.Add((legacyPath, state));
            }
            catch { /* skip */ }
        }

        return results.OrderByDescending(r => r.Item2.LastSavedAt).ToList();
    }

    /// <summary>Loads a specific save file from disk.</summary>
    public static GameState? LoadFromFile(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<GameState>(json, AgentHelper.JsonOpts);
        }
        catch { return null; }
    }

    /// <summary>Deletes a save file from disk.</summary>
    public static bool Delete(string path)
    {
        try { File.Delete(path); return true; }
        catch { return false; }
    }

    /// <summary>
    /// Migrates a legacy save (no SaveId) to the new naming scheme.
    /// Assigns a SaveId, re-saves under the new filename, and deletes the old file.
    /// </summary>
    public static void MigrateLegacy(GameState state, string legacyPath)
    {
        if (!state.SaveId.IsEmpty) return; // already migrated

        state.SaveId = EntityId.New();
        state.LastSavedAt = DateTime.UtcNow;

        var json = JsonSerializer.Serialize(state, AgentHelper.JsonOpts);
        var newPath = Path.Combine(SavesDir, state.GetSaveFileName());
        File.WriteAllText(newPath, json);

        // Remove old legacy file if different
        if (Path.GetFullPath(legacyPath) != Path.GetFullPath(newPath))
        {
            try { File.Delete(legacyPath); } catch { /* best-effort */ }
        }
    }
}
