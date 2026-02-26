namespace RPGGameMaster.Shared;

/// <summary>
/// Extension methods for <see cref="DateTime"/> formatting.
/// </summary>
internal static class DateTimeExtensions
{
    /// <summary>Format a UTC timestamp as a human-readable "time ago" string.</summary>
    public static string ToTimeAgo(this DateTime utcTime)
    {
        var span = DateTime.UtcNow - utcTime;
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
        return utcTime.ToLocalTime().ToString("MMM dd");
    }
}
