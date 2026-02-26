namespace RPGGameMaster.Future;

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// Game-event contracts for a future event/mediator pattern
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//
// Current dispatch points live inline (CombatWorkflow, DialogueWorkflow,
// TradeWorkflow, GameMasterWorkflow).  When the codebase grows, these
// events can be published through a lightweight mediator so that cross-
// cutting concerns (logging, achievements, analytics, sound effects)
// react without coupling to the originating workflow.
//
// Migration path:
//   1. Create a GameEventBus (simple in-proc mediator):
//
//        public class GameEventBus
//        {
//            private readonly Dictionary<Type, List<Delegate>> _handlers = new();
//
//            public void Subscribe<T>(Action<T> handler) where T : IGameEvent
//                => (_handlers.TryGetValue(typeof(T), out var list)
//                        ? list : _handlers[typeof(T)] = new()).Add(handler);
//
//            public void Publish<T>(T evt) where T : IGameEvent
//            {
//                if (_handlers.TryGetValue(typeof(T), out var list))
//                    foreach (var h in list) ((Action<T>)h)(evt);
//            }
//        }
//
//   2. Replace inline event handling with Publish() calls:
//
//        // Before (CombatWorkflow.cs):
//        creature.IsDefeated = true;
//        state.Player.AddXP(creature.XPReward);
//        state.LogEvent($"Defeated {creature.Name}!");
//
//        // After:
//        bus.Publish(new CombatVictoryEvent(creature, state));
//
//   3. Register handlers for cross-cutting reactions:
//
//        bus.Subscribe<CombatVictoryEvent>(e =>
//        {
//            e.State.LogEvent($"Defeated {e.Creature.Name}!");
//            e.State.Player.AddXP(e.Creature.XPReward);
//            SaveManager.AutoSave(e.State);
//        });
//

/// <summary>Marker interface for all game events.</summary>
[Obsolete("Future: not yet wired. See migration notes in this file.")]
internal interface IGameEvent;

/// <summary>
/// Contract for an event handler that reacts to a specific <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">Concrete event type.</typeparam>
[Obsolete("Future: not yet wired. See migration notes in this file.")]
#pragma warning disable CS0618 // IGameEvent is itself obsolete
internal interface IGameEventHandler<in T> where T : IGameEvent
#pragma warning restore CS0618
{
    void Handle(T evt);
}

// ── Concrete event records ──

#pragma warning disable CS0618 // Obsolete — these are the future event types themselves

/// <summary>Raised when a creature is defeated in combat.</summary>
/// <param name="Creature">The defeated creature.</param>
/// <param name="State">Snapshot of the game state at the moment of victory.</param>
internal readonly record struct CombatVictoryEvent(Creature Creature, GameState State) : IGameEvent;

/// <summary>Raised when combat results in a player death (HP ≤ 0).</summary>
internal readonly record struct PlayerDeathEvent(GameState State) : IGameEvent;

/// <summary>Raised when a quest is marked complete.</summary>
internal readonly record struct QuestCompletedEvent(Quest Quest, GameState State) : IGameEvent;

/// <summary>Raised when the player acquires an item (loot, trade, quest reward).</summary>
internal readonly record struct ItemAcquiredEvent(Item Item, string Source, GameState State) : IGameEvent;

/// <summary>Raised when the player levels up.</summary>
internal readonly record struct LevelUpEvent(int NewLevel, GameState State) : IGameEvent;

#pragma warning restore CS0618
