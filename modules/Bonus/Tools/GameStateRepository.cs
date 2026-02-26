using System.Text.Json;

namespace RPGGameMaster.Tools;

/// <summary>
/// Generic Save / Load operations for game entities stored in <see cref="GameState"/> dictionaries.
/// Implements a lightweight repository pattern that eliminates the repeated
/// try → deserialise → validate-id → store / retrieve → serialise boilerplate
/// from <see cref="LocationTools"/>, <see cref="NPCTools"/>, and <see cref="CreatureTools"/>.
/// </summary>
internal static class GameStateRepository
{
    /// <summary>
    /// Deserialises <paramref name="json"/> into <typeparamref name="T"/>,
    /// validates the id, and stores it in the dictionary returned by <paramref name="getDict"/>.
    /// </summary>
    public static string Save<T>(
        string json,
        Func<GameState, Dictionary<string, T>> getDict,
        string typeName) where T : class, IEntity
    {
        try
        {
            var entity = JsonSerializer.Deserialize<T>(json, LlmJsonParser.JsonOpts);
            if (entity is null || entity.Id.IsEmpty)
                return $"ERROR: Invalid {typeName} JSON or missing id.";

            if (GameStateAccessor.IsLoaded)
                getDict(GameStateAccessor.Current)[entity.Id] = entity;

            return $"OK: {typeName} '{entity.Name}' saved with id '{entity.Id}'.";
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    /// <summary>
    /// Looks up an entity of <typeparamref name="T"/> by <paramref name="id"/>
    /// and returns its JSON representation.
    /// </summary>
    public static string Load<T>(
        string id,
        Func<GameState, Dictionary<string, T>> getDict,
        string typeName) where T : class
    {
        try
        {
            if (GameStateAccessor.IsLoaded &&
                getDict(GameStateAccessor.Current).TryGetValue(id, out var entity))
            {
                return JsonSerializer.Serialize(entity, LlmJsonParser.JsonOpts);
            }

            return $"ERROR: {typeName} '{id}' not found.";
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }
}
