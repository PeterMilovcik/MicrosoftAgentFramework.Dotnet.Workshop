namespace RPGGameMaster;

/// <summary>
/// Single static accessor for the in-memory <see cref="GameState"/> used by AI tool
/// classes (<c>GameTools</c>, <c>ItemTools</c>).  The framework discovers tools via
/// <c>AIFunctionFactory.Create</c> on static methods, so the state must be static.
/// Centralising it here avoids every tool class carrying its own <c>_gameState</c> field.
/// </summary>
internal static class GameStateAccessor
{
    private static GameState? _current;

    /// <summary>Gets the current game state, or throws if not yet set.</summary>
    public static GameState Current
        => _current ?? throw new InvalidOperationException("Game state not yet initialised. Call Set() first.");

    /// <summary>Whether a game state has been loaded.</summary>
    public static bool IsLoaded => _current is not null;

    /// <summary>Sets (or replaces) the current game state.</summary>
    public static void Set(GameState state)
        => _current = state ?? throw new ArgumentNullException(nameof(state));
}
