using System.Text.Json;

namespace RPGGameMaster.Infrastructure;

/// <summary>
/// Convenience extensions for <see cref="JsonElement"/> that eliminate the
/// repeated <c>TryGetProperty → GetString/GetInt32</c> ternary patterns.
/// </summary>
internal static class JsonElementExtensions
{
    /// <summary>Get a string property or <paramref name="fallback"/> (default <c>""</c>).</summary>
    public static string Str(this JsonElement el, string property, string fallback = "")
        => el.TryGetProperty(property, out var p) ? p.GetString() ?? fallback : fallback;

    /// <summary>Get a string property or <c>null</c> if missing.</summary>
    public static string? StrOrNull(this JsonElement el, string property)
        => el.TryGetProperty(property, out var p) ? p.GetString() : null;

    /// <summary>Get an int property or <paramref name="fallback"/> (default <c>0</c>).</summary>
    public static int Int(this JsonElement el, string property, int fallback = 0)
        => el.TryGetProperty(property, out var p) ? p.GetInt32() : fallback;

    /// <summary>Get a boolean property (true only when the JSON value is literal <c>true</c>).</summary>
    public static bool Bool(this JsonElement el, string property)
        => el.TryGetProperty(property, out var p) && p.ValueKind == JsonValueKind.True;
}
