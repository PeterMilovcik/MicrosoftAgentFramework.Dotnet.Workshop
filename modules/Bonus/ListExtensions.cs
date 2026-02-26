namespace RPGGameMaster;

/// <summary>
/// Convenience extensions for <see cref="List{T}"/>.
/// </summary>
internal static class ListExtensions
{
    /// <summary>
    /// Appends <paramref name="item"/> to the list and trims the oldest entries
    /// so the total count never exceeds <paramref name="maxCount"/>.
    /// </summary>
    public static void AddCapped<T>(this List<T> list, T item, int maxCount)
    {
        list.Add(item);
        while (list.Count > maxCount)
            list.RemoveAt(0);
    }
}
