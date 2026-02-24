using System.Text.Json.Serialization;

namespace MagenticOrchestration;

/// <summary>
/// Magentic manager decision parsed from LLM JSON output.
/// </summary>
internal sealed class ManagerDecision
{
    [JsonPropertyName("progress_summary")]
    public string ProgressSummary { get; set; } = "";

    [JsonPropertyName("next_agent")]
    public string NextAgent { get; set; } = "";

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = "";

    [JsonPropertyName("task")]
    public string Task { get; set; } = "";

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }
}
