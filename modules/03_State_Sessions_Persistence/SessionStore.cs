using System.Text.Json;
using System.Text.Json.Serialization;

namespace StateSessionsPersistence;

/// <summary>
/// Persists workshop sessions as JSON files under .sessions/ in the current directory.
/// </summary>
internal static class SessionStore
{
    private static readonly string SessionsDir =
        Path.Combine(Directory.GetCurrentDirectory(), ".sessions");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static void EnsureDirectory()
        => Directory.CreateDirectory(SessionsDir);

    public static void Save(WorkshopSession session)
    {
        EnsureDirectory();
        var path = Path.Combine(SessionsDir, $"{session.SessionId}.json");
        var json = JsonSerializer.Serialize(session, JsonOptions);
        File.WriteAllText(path, json);
    }

    public static WorkshopSession? Load(Guid sessionId)
    {
        var path = Path.Combine(SessionsDir, $"{sessionId}.json");
        if (!File.Exists(path)) return null;
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<WorkshopSession>(json, JsonOptions);
    }

    public static IReadOnlyList<WorkshopSession> ListAll()
    {
        EnsureDirectory();
        var sessions = new List<WorkshopSession>();
        foreach (var file in Directory.GetFiles(SessionsDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var s = JsonSerializer.Deserialize<WorkshopSession>(json, JsonOptions);
                if (s is not null) sessions.Add(s);
            }
            catch { /* skip malformed files */ }
        }
        return sessions.OrderBy(s => s.CreatedAt).ToList();
    }

    public static bool Delete(Guid sessionId)
    {
        var path = Path.Combine(SessionsDir, $"{sessionId}.json");
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }
}
