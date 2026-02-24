using System.Text.Json.Serialization;

namespace CapstoneTriageAssistant;

/// <summary>
/// Structured Triage Card output - matches the triage-rubric.md schema.
/// </summary>
internal sealed class TriageCard
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";

    [JsonPropertyName("category")]
    public string Category { get; set; } = ""; // infra | product | test

    [JsonPropertyName("suspected_areas")]
    public List<string> SuspectedAreas { get; set; } = [];

    [JsonPropertyName("next_steps")]
    public List<string> NextSteps { get; set; } = [];

    [JsonPropertyName("suggested_owner_role")]
    public string SuggestedOwnerRole { get; set; } = ""; // dev | ops | qa | arch

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }
}
