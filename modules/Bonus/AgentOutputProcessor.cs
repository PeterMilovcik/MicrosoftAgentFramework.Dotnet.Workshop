using RPGGameMaster.Models;
using RPGGameMaster.Workflow;

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
                    var loc = AgentHelper.ParseJson<Location>(response);
                    if (loc is not null && !loc.Id.IsEmpty)
                    {
                        loc.Visited = true;
                        state.Locations[loc.Id] = loc;
                        if (state.CurrentLocationId.IsEmpty)
                            state.CurrentLocationId = loc.Id;
                        state.AddLog($"Discovered new location: {loc.Name}");
                    }
                    break;

                case AgentNames.NPCWeaver:
                    var npc = AgentHelper.ParseJson<NPC>(response);
                    if (npc is not null && !npc.Id.IsEmpty)
                    {
                        state.NPCs[npc.Id] = npc;
                        var currentLoc = state.CurrentLocation;
                        if (currentLoc is not null && !currentLoc.NPCIds.Contains(npc.Id))
                        {
                            npc.LocationId = currentLoc.Id;
                            currentLoc.NPCIds.Add(npc.Id);
                        }
                        state.AddLog($"Met {npc.Name}.");
                    }
                    break;

                case AgentNames.CreatureForger:
                    var creature = AgentHelper.ParseJson<Creature>(response);
                    if (creature is not null && !creature.Id.IsEmpty)
                    {
                        state.Creatures[creature.Id] = creature;
                        var curLoc = state.CurrentLocation;
                        if (curLoc is not null && !curLoc.CreatureIds.Contains(creature.Id))
                        {
                            creature.LocationId = curLoc.Id;
                            curLoc.CreatureIds.Add(creature.Id);
                        }
                        state.AddLog($"A {creature.Name} lurks nearby.");
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            // Log parse failures so they are diagnosable without disrupting gameplay
            AgentHelper.PrintWarning($"[AgentOutputProcessor] Failed to process {agentName}: {ex.Message}");
        }
    }
}
