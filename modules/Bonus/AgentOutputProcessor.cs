namespace RPGGameMaster;

/// <summary>
/// Processes sub-agent JSON output and integrates the generated entities
/// (locations, NPCs, creatures) into the game state.
/// Extracted from GameMasterWorkflow to separate hydration from orchestration.
/// </summary>
internal static class AgentOutputProcessor
{
    /// <summary>
    /// Deserialize the agent's response and merge it into <paramref name="state"/>.
    /// </summary>
    public static void Apply(string agentName, string response, GameState state)
    {
        try
        {
            switch (agentName.ToLowerInvariant())
            {
                case AgentNames.WorldArchitect:
                    var loc = LlmJsonParser.ParseJson<Location>(response);
                    if (loc is not null && !loc.Id.IsEmpty)
                        state.RegisterLocation(loc);
                    break;

                case AgentNames.NPCWeaver:
                    var npc = LlmJsonParser.ParseJson<NPC>(response);
                    if (npc is not null && !npc.Id.IsEmpty)
                        state.RegisterNPC(npc, state.CurrentLocation);
                    break;

                case AgentNames.CreatureForger:
                    var creature = LlmJsonParser.ParseJson<Creature>(response);
                    if (creature is not null && !creature.Id.IsEmpty)
                        state.RegisterCreature(creature, state.CurrentLocation);
                    break;
            }
        }
        catch (Exception ex)
        {
            // Log parse failures so they are diagnosable without disrupting gameplay
            GameConsoleUI.PrintWarning($"[AgentOutputProcessor] Failed to process {agentName}: {ex.Message}");
        }
    }
}
