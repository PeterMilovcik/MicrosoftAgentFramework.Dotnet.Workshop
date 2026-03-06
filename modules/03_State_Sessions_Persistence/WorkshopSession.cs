using System.Text.Json;

namespace StateSessionsPersistence;

/// <summary>
/// Represents a persisted workshop session metadata and serialized agent session state.
/// </summary>
internal sealed class WorkshopSession
{
    public Guid SessionId { get; init; } = Guid.NewGuid();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string Label { get; set; } = "";

    /// <summary>Number of conversation turns (user+assistant message pairs).</summary>
    public int TurnCount { get; set; }

    /// <summary>
    /// The serialized <see cref="Microsoft.Agents.AI.AgentSession"/> state produced by
    /// <see cref="Microsoft.Agents.AI.AIAgent.SerializeSessionAsync"/>.
    /// </summary>
    public JsonElement? AgentSessionState { get; set; }
}
