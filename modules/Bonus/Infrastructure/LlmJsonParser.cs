using System.Text.Json;

namespace RPGGameMaster.Infrastructure;

/// <summary>
/// Extracts and deserializes structured JSON from raw LLM text output.
/// LLM responses often wrap JSON in prose, markdown fences, or preamble —
/// this class handles all those cases.
/// </summary>
internal static class LlmJsonParser
{
    /// <summary>Shared serializer options used across the application for JSON round-tripping.</summary>
    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Extract the first top-level JSON object from text that may contain prose or markdown fences.</summary>
    public static string? ExtractJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var cleaned = StripMarkdownFences(text);

        var start = cleaned.IndexOf('{');
        if (start < 0) return null;

        // Find the matching closing brace (handle nested braces)
        var depth = 0;
        for (var i = start; i < cleaned.Length; i++)
        {
            if (cleaned[i] == '{') depth++;
            else if (cleaned[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return cleaned[start..(i + 1)];
            }
        }

        // Fallback: use last closing brace
        var end = cleaned.LastIndexOf('}');
        if (end > start) return cleaned[start..(end + 1)];
        return null;
    }

    /// <summary>Extract the first top-level JSON array from text that may contain prose or markdown fences.</summary>
    public static string? ExtractJsonArray(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var cleaned = StripMarkdownFences(text);

        var start = cleaned.IndexOf('[');
        if (start < 0) return null;

        var depth = 0;
        for (var i = start; i < cleaned.Length; i++)
        {
            if (cleaned[i] == '[') depth++;
            else if (cleaned[i] == ']')
            {
                depth--;
                if (depth == 0)
                    return cleaned[start..(i + 1)];
            }
        }

        return null;
    }

    /// <summary>Parse a JSON string into a typed object, returning null on failure.</summary>
    public static T? ParseJson<T>(string text) where T : class
    {
        var json = ExtractJson(text);
        if (json is null) return null;
        try { return JsonSerializer.Deserialize<T>(json, JsonOpts); }
        catch { return null; }
    }

    /// <summary>Parse a JSON string into a typed object, returning <paramref name="fallback"/> on failure.</summary>
    public static T ParseJson<T>(string text, T fallback) where T : class
    {
        var json = ExtractJson(text);
        if (json is null) return fallback;
        try { return JsonSerializer.Deserialize<T>(json, JsonOpts) ?? fallback; }
        catch { return fallback; }
    }

    /// <summary>
    /// Extract a single named property from an LLM JSON response.
    /// Falls back to the trimmed raw text if JSON parsing fails or the property is missing.
    /// </summary>
    public static string ExtractProperty(string text, string propertyName)
    {
        var json = ExtractJson(text);
        if (json is null) return text.Trim();
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.StrOrNull(propertyName) ?? text.Trim();
        }
        catch { return text.Trim(); }
    }

    /// <summary>Strip markdown code fences from LLM output.</summary>
    private static string StripMarkdownFences(string text)
    {
        if (!text.Contains("```")) return text;

        var fenceStart = text.IndexOf("```", StringComparison.Ordinal);
        var afterFence = text.IndexOf('\n', fenceStart);
        if (afterFence > 0)
        {
            var fenceEnd = text.IndexOf("```", afterFence, StringComparison.Ordinal);
            if (fenceEnd > 0)
                return text[(afterFence + 1)..fenceEnd];
        }

        return text;
    }
}
