using Microsoft.Agents.AI;

namespace RPGGameMaster.Future;

/// <summary>
/// Strategy interface for player actions. Each <see cref="ActionType"/> could be
/// handled by a dedicated implementation, replacing the switch in
/// <c>GameMasterWorkflow.HandlePlayerAction</c>.
/// <para>
/// NOT yet wired — the switch remains the active dispatch mechanism.
/// This interface exists to document the migration path once DI is introduced.
/// </para>
/// </summary>
/// <example>
/// Future usage:
/// <code>
/// var handlers = new Dictionary&lt;ActionType, IActionHandler&gt;
/// {
///     [ActionType.Move]  = new MoveHandler(locationService),
///     [ActionType.Trade] = new TradeHandler(config),
///     // …
/// };
/// return await handlers[choice.ActionType].HandleAsync(choice, state, ct);
/// </code>
/// </example>
[Obsolete("Future: not yet wired. The switch in GameMasterWorkflow remains the active dispatch.")]
internal interface IActionHandler
{
    /// <summary>The action type this handler is responsible for.</summary>
    ActionType Action { get; }

    /// <summary>Execute the action and return a summary string for the game log.</summary>
    Task<string> HandleAsync(
        GameOption choice, GameState state, AgentConfig config,
        Dictionary<string, AIAgent> agentMap, CancellationToken ct);
}
