namespace RPGGameMaster;

/// <summary>
/// Abstraction for dice rolling — allows deterministic testing.
/// </summary>
internal interface IDiceRoller
{
    int Roll(int count, int sides);
    int[] RollEach(int count, int sides);
}

/// <summary>
/// Shared dice-rolling façade. Delegates to a swappable <see cref="IDiceRoller"/>
/// so tests can inject a deterministic implementation via <see cref="SetRoller"/>.
/// </summary>
internal static class Dice
{
    private static IDiceRoller _roller = new RandomDiceRoller();

    /// <summary>Replace the default random roller (useful in tests).</summary>
    internal static void SetRoller(IDiceRoller roller)
        => _roller = roller ?? throw new ArgumentNullException(nameof(roller));

    /// <summary>Rolls <paramref name="count"/>d<paramref name="sides"/> and returns the total.</summary>
    public static int Roll(int count, int sides)
        => _roller.Roll(count, sides);

    /// <summary>
    /// Rolls <paramref name="count"/>d<paramref name="sides"/> and returns each die result.
    /// Useful when individual results must be displayed (e.g. the <c>RollDice</c> AI tool).
    /// </summary>
    public static int[] RollEach(int count, int sides)
        => _roller.RollEach(count, sides);

    // ── Default implementation ──

    private sealed class RandomDiceRoller : IDiceRoller
    {
        public int Roll(int count, int sides)
        {
            var total = 0;
            for (var i = 0; i < count; i++)
                total += Random.Shared.Next(1, sides + 1);
            return total;
        }

        public int[] RollEach(int count, int sides)
        {
            var results = new int[count];
            for (var i = 0; i < count; i++)
                results[i] = Random.Shared.Next(1, sides + 1);
            return results;
        }
    }
}
