namespace RPGGameMaster;

/// <summary>
/// Centralised case-insensitive fuzzy-matching for named game entities.
/// Removes the duplicated FirstOrDefault + Contains patterns scattered
/// across tools and workflows.
/// </summary>
internal static class FuzzyFinder
{
    /// <summary>
    /// Finds an element by name: exact match first, then bidirectional
    /// case-insensitive <c>Contains</c> fallback.
    /// </summary>
    public static T? ByName<T>(
        IEnumerable<T> source,
        string query,
        Func<T, string> nameSelector) where T : class
    {
        return source.FirstOrDefault(e =>
                   nameSelector(e).Equals(query, StringComparison.OrdinalIgnoreCase))
               ?? source.FirstOrDefault(e =>
                   nameSelector(e).Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   query.Contains(nameSelector(e), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Finds an element by name <em>or</em> ID: bidirectional case-insensitive
    /// <c>Contains</c> on the name, plus a one-way <c>Contains</c> on the ID.
    /// Typically used after a dictionary-key lookup has already failed.
    /// </summary>
    public static T? ByNameOrId<T>(
        IEnumerable<T> source,
        string query,
        Func<T, string> nameSelector,
        Func<T, string> idSelector) where T : class
    {
        return source.FirstOrDefault(e =>
            nameSelector(e).Contains(query, StringComparison.OrdinalIgnoreCase) ||
            query.Contains(nameSelector(e), StringComparison.OrdinalIgnoreCase) ||
            idSelector(e).Contains(query, StringComparison.OrdinalIgnoreCase));
    }
}
