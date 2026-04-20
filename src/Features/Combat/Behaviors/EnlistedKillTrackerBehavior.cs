using System;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using EnlistedConfig = Enlisted.Mod.Core.Config.ConfigurationManager;

namespace Enlisted.Features.Combat.Behaviors
{
    /// <summary>
    /// Mission behavior that tracks player kills during battles.
    /// Kill count is used to award bonus XP for tier progression.
    /// Resets at mission start and reports to EnlistmentBehavior at mission end.
    /// </summary>
    public class EnlistedKillTrackerBehavior : MissionBehavior
    {
        /// <summary>
        /// Singleton instance for access from EnlistmentBehavior.
        /// </summary>
        public static EnlistedKillTrackerBehavior Instance { get; private set; }

        /// <summary>
        /// Number of enemies killed by the player in the current mission.
        /// </summary>
        public int KillCount { get; private set; }

        /// <summary>
        /// Whether the player participated in this battle (was present and active).
        /// </summary>
        public bool DidParticipate { get; private set; }

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        public EnlistedKillTrackerBehavior()
        {
            Instance = this;
        }

        public override void AfterStart()
        {
            try
            {
                base.AfterStart();

                // Reset kill count for new mission
                KillCount = 0;
                DidParticipate = false;

                // Log battle start with mission mode
                var missionMode = Mission?.Mode.ToString() ?? "Unknown";
                ModLogger.Info("BATTLE", $"Battle started (Mode: {missionMode}) - kill tracking active");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("KILLTRACKER", "Error in AfterStart", ex);
            }
        }

        public override void OnMissionTick(float dt)
        {
            try
            {
                base.OnMissionTick(dt);

                // Mark participation if player agent exists and is active
                if (!DidParticipate && Agent.Main != null)
                {
                    try
                    {
                        if (Agent.Main.IsActive())
                        {
                            DidParticipate = true;
                            ModLogger.Debug("KillTracker", "Player participation confirmed");
                        }
                    }
                    catch
                    {
                        // Agent in invalid state
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("KILLTRACKER", "Error in OnMissionTick", ex);
            }
        }

        /// <summary>
        /// Called when any agent is removed from the mission (killed, fled, etc).
        /// We check if it was killed by the player and increment the counter.
        /// </summary>
        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            try
            {
                base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow);

                // Only count kills (not retreats, etc)
                if (agentState != AgentState.Killed && agentState != AgentState.Unconscious)
                {
                    return;
                }

                // Check if the killer was the player
                if (affectorAgent == null || affectorAgent != Agent.Main)
                {
                    return;
                }

                // Check if the victim was an enemy
                if (affectedAgent == null || affectedAgent.Team == null || Agent.Main?.Team == null)
                {
                    return;
                }

                // Don't count friendly fire
                if (affectedAgent.Team == Agent.Main.Team)
                {
                    return;
                }

                // Increment kill counter
                KillCount++;

                // Get XP per kill from config for accurate display
                var xpPerKill = EnlistedConfig.GetXpPerKill();

                // Show in-game notification for each kill
                var victimName = affectedAgent.Character?.Name?.ToString() ?? "Enemy";
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Kill: {victimName} (+{xpPerKill} XP)",
                    Colors.Green));

                // Log kill with XP value
                ModLogger.Debug("COMBAT", $"Kill #{KillCount}: {victimName} (+{xpPerKill} XP pending)");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("KILLTRACKER", "Error in OnAgentRemoved", ex);
            }
        }

        protected override void OnEndMission()
        {
            try
            {
                base.OnEndMission();

                // Log battle summary
                if (DidParticipate)
                {
                    var xpPerKill = EnlistedConfig.GetXpPerKill();
                    var estimatedXp = KillCount * xpPerKill;
                    ModLogger.Info("BATTLE", $"Battle ended - Kills: {KillCount}, Estimated bonus XP: {estimatedXp}");
                }
                else
                {
                    ModLogger.Info("BATTLE", "Battle ended - Player did not participate");
                }

                // Note: Don't reset here - EnlistmentBehavior will read and then reset via GetAndResetKillCount()
            }
            catch (Exception ex)
            {
                ModLogger.Caught("KILLTRACKER", "Error in OnEndMission", ex);
            }
        }

        /// <summary>
        /// Gets the current kill count and resets it for the next battle.
        /// Called by EnlistmentBehavior when awarding battle XP.
        /// </summary>
        public int GetAndResetKillCount()
        {
            var kills = KillCount;
            KillCount = 0;
            return kills;
        }

        /// <summary>
        /// Gets participation status and resets it for the next battle.
        /// </summary>
        public bool GetAndResetParticipation()
        {
            var participated = DidParticipate;
            DidParticipate = false;
            return participated;
        }
    }
}

