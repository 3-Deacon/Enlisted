using System;
using System.Collections.Generic;
using System.Linq;
using Enlisted.Features.Retinue.Core;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Enlisted.Features.Combat.Behaviors
{
    /// <summary>
    ///     Mission behavior that automatically assigns enlisted players to their designated formation
    ///     (Infantry, Ranged, Cavalry, Horse Archer) based on their duty when a battle starts.
    ///
    ///     At Commander tier (T7+), players can command their own formation (sergeant mode). Their retinue and
    ///     companions are assigned to the same formation, and the formation is made player-controllable.
    ///     Below T7, players join the formation but cannot issue commands.
    ///
    ///     FIX: Also teleports the player to the correct position within their formation to handle
    ///     cases where the player's map party was slightly behind the lord when battle started,
    ///     causing them to spawn in the wrong position (behind the formation instead of in it).
    /// </summary>
    public class EnlistedFormationAssignmentBehavior : MissionBehavior
    {
        private const int MaxPositionFixAttempts = 120; // Try for about 2 seconds at 60fps (lord may spawn later)
        private const int MaxPartyAssignmentAttempts = 60; // Try for about 1 second at 60fps for party assignment
        private const int CompanionRemovalDelayTicks = 30; // Delay companion removal to avoid spawn corruption

        private Agent _assignedAgent;

        // Cached spawn logic to detect reinforcement phase
        private MissionAgentSpawnLogic _spawnLogic;

        // Track if we've logged the behavior initialization
        private bool _hasLoggedInit;

        // Track whether we need to teleport the player to their formation position
        // This handles the case where the player spawned late or in wrong position
        private bool _needsPositionFix;
        private int _positionFixAttempts;

        // Track player party (companions + retinue) formation assignment
        // This ensures all player party members fight together as a unified squad
        private bool _needsPartyAssignment;
        private bool _partyAssignmentComplete;
        private int _partyAssignmentAttempts;
        private Formation _playerSquadFormation;

        // Retry lord attach when formation is solo and lord agent was not spawned yet
        private bool _needsLordAttachRetry;
        private int _lordAttachRetryAttempts;
        private const int MaxLordAttachRetryAttempts = 180; // ~3 seconds at 60fps

        // Logging flags to avoid spamming user-facing logs
        private bool _loggedSoloAttachOutcome;
        private bool _loggedSoloAttachMissing;

        // Deferred companion removal to avoid corrupting spawn state
        // Removing agents during OnAgentBuild can crash the native spawn loop
        private readonly List<Agent> _companionsToRemove = new List<Agent>();
        private int _companionRemovalDelayCounter;

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        /// <summary>
        ///     Called after the mission starts. We try to assign formation here,
        ///     but the player agent might not exist yet.
        ///     Note: This may not fire if we're added after mission start.
        /// </summary>
        public override void AfterStart()
        {
            try
            {
                base.AfterStart();

                // Cache the spawn logic for reinforcement detection
                _spawnLogic = Mission.Current?.GetMissionBehavior<MissionAgentSpawnLogic>();
                _loggedSoloAttachOutcome = false;
                _loggedSoloAttachMissing = false;

                // Log that the behavior has been initialized (once per mission)
                if (!_hasLoggedInit)
                {
                    _hasLoggedInit = true;
                    var enlistment = EnlistmentBehavior.Instance;
                    ModLogger.Info("FORMATIONASSIGNMENT",
                        $"=== BEHAVIOR ACTIVE === Mission: {Mission.Current?.Mode}, " +
                        $"Enlisted: {enlistment?.IsEnlisted}, OnLeave: {enlistment?.IsOnLeave}, " +
                        $"Agent.Main exists: {Agent.Main != null}");

                    // Log companion states at mission start for debugging Stay Back issues
                    var companionManager = CompanionAssignmentManager.Instance;
                    var companions = companionManager?.GetAssignableCompanions() ?? new List<Hero>();
                    if (companions.Count > 0)
                    {
                        var stayBackCompanions = companions.Where(c => !(companionManager?.ShouldCompanionFight(c) ?? true)).ToList();
                        if (stayBackCompanions.Any())
                        {
                            ModLogger.Debug("FORMATIONASSIGNMENT",
                                $"Companions set to Stay Back: {string.Join(", ", stayBackCompanions.Select(c => c.Name))}");
                        }
                    }
                }

                TryAssignPlayerToFormation("AfterStart");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("FORMATIONASSIGNMENT", "Error in AfterStart", ex);
            }
        }

        /// <summary>
        /// Determines if we're currently in reinforcement spawn phase.
        /// Initial spawn places agents at formation positions; reinforcements spawn at map edges.
        /// For reinforcements, we should always teleport regardless of distance.
        /// </summary>
        private bool IsReinforcementPhase()
        {
            // If we don't have spawn logic, assume initial spawn (conservative approach)
            if (_spawnLogic == null)
            {
                return false;
            }

            // IsInitialSpawnOver is true once the initial deployment phase troops have all spawned
            // After that, any new spawns are reinforcements from the map edge
            return _spawnLogic.IsInitialSpawnOver;
        }

        /// <summary>
        ///     Called when deployment finishes. This is a reliable point where
        ///     the player agent should exist.
        /// </summary>
        public override void OnDeploymentFinished()
        {
            try
            {
                base.OnDeploymentFinished();
                TryAssignPlayerToFormation("OnDeploymentFinished");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("FORMATIONASSIGNMENT", "Error in OnDeploymentFinished", ex);
            }
        }

        /// <summary>
        ///     Called when an agent is built.
        ///     This catches late joins, respawns, and reinforcements immediately.
        ///     Also handles "Stay Back" companions, ensuring they are immediately retreated from battle.
        /// </summary>
        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            try
            {
                base.OnAgentBuild(agent, banner);
                if (agent.IsMainAgent)
                {
                    TryAssignPlayerToFormation("OnAgentBuild", agent);
                }
                else
                {
                    // Check if this is a "stay back" companion and remove them from battle.
                    TryRemoveStayBackCompanion(agent);
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("FORMATIONASSIGNMENT", "Error in OnAgentBuild", ex);
            }
        }

        /// <summary>
        /// Queues "stay back" companions for deferred removal from battle.
        /// We can't remove them during OnAgentBuild because that corrupts the native spawn loop
        /// and causes crashes in Mission.SpawnAgent. Instead, we queue them for removal
        /// after the spawn phase completes.
        /// </summary>
        private void TryRemoveStayBackCompanion(Agent agent)
        {
            // Only process companions from player's party at Commander tier (T7+).
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment?.IsEnlisted != true || enlistment.EnlistmentTier < RetinueManager.CommanderTier1)
            {
                return;
            }

            // Check if agent is from player's party
            if (agent.Origin is not PartyGroupAgentOrigin partyOrigin || partyOrigin.Party != PartyBase.MainParty)
            {
                return;
            }

            // Only handle heroes (companions)
            var characterObject = agent.Character as CharacterObject;
            if (characterObject?.IsHero != true || characterObject.HeroObject == null)
            {
                return;
            }

            var hero = characterObject.HeroObject;
            if (!hero.IsPlayerCompanion)
            {
                return;
            }

            // Check if this companion should stay back
            if (ShouldCompanionFight(hero))
            {
                return;
            }

            // Queue companion for deferred removal - doing it during OnAgentBuild crashes the spawn loop
            // The removal will happen after the spawn phase completes in OnMissionTick
            if (!_companionsToRemove.Contains(agent))
            {
                _companionsToRemove.Add(agent);
                _companionRemovalDelayCounter = 0; // Reset delay counter when we add a new companion

                ModLogger.Debug("FORMATIONASSIGNMENT",
                    $"Stay Back companion {hero.Name} queued for removal (formation: {agent.Formation?.FormationIndex})");
            }
        }

        /// <summary>
        /// Processes the deferred companion removal queue after spawn phase completes.
        /// This avoids corrupting the native spawn loop which caused crashes.
        /// </summary>
        private void ProcessDeferredCompanionRemovals()
        {
            if (_companionsToRemove.Count == 0)
            {
                return;
            }

            // Wait for spawn phase to complete before removing companions
            _companionRemovalDelayCounter++;
            if (_companionRemovalDelayCounter < CompanionRemovalDelayTicks)
            {
                return;
            }

            // Process all queued companions
            foreach (var agent in _companionsToRemove)
            {
                try
                {
                    // Verify agent is still valid and active before removal
                    if (agent == null || !agent.IsActive())
                    {
                        continue;
                    }

                    var heroName = (agent.Character as CharacterObject)?.HeroObject?.Name?.ToString() ?? "Unknown";

                    // Clear captain status if this companion was made captain
                    if (agent.Formation?.Captain == agent)
                    {
                        agent.Formation.Captain = null;
                        ModLogger.Debug("FORMATIONASSIGNMENT", $"Cleared {heroName} as formation captain before removal");
                    }

                    // CRITICAL FIX: Clear captain status from ALL formations, not just own formation
                    // The game's GeneralsAndCaptainsAssignmentLogic can make companion captain of ANY formation
                    if (agent.Team != null)
                    {
                        foreach (var formation in agent.Team.FormationsIncludingEmpty)
                        {
                            if (formation?.Captain == agent)
                            {
                                formation.Captain = null;
                                ModLogger.Debug("FORMATIONASSIGNMENT",
                                    $"Cleared {heroName} as captain of {formation.FormationIndex} (Stay Back)");
                            }
                        }
                    }

                    // FadeOut removes the agent without tracking as casualty
                    agent.FadeOut(hideInstantly: true, hideMount: true);

                    // Verify removal worked
                    var stillActive = agent.IsActive();

                    if (stillActive)
                    {
                        ModLogger.Warn("FORMATIONASSIGNMENT",
                            $"Companion {heroName} FadeOut called but agent still active - trying alternative removal");
                        // Try setting health to force removal
                        agent.Health = 0f;
                    }
                    else
                    {
                        ModLogger.Info("FORMATIONASSIGNMENT",
                            $"Companion {heroName} removed from battle (set to 'stay back')");
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.Caught("FORMATIONASSIGNMENT", "Error removing stay-back companion", ex);
                }
            }

            _companionsToRemove.Clear();
        }

        /// <summary>
        ///     Called each mission tick. We use this as a fallback in case
        ///     the player agent wasn't available in earlier callbacks.
        ///     Also handles delayed position correction for players who spawned late.
        /// </summary>
        public override void OnMissionTick(float dt)
        {
            try
            {
                base.OnMissionTick(dt);
                TryAssignPlayerToFormation("OnMissionTick");

                // Handle position fix for players who may have spawned in wrong location
                // This happens when the player's map party lagged behind the lord
                if (_needsPositionFix && _positionFixAttempts < MaxPositionFixAttempts)
                {
                    _positionFixAttempts++;
                    TryTeleportPlayerToFormationPosition();
                }
                else if (_needsPositionFix && _positionFixAttempts >= MaxPositionFixAttempts)
                {
                    // Log failure when we've exhausted all attempts
                    ModLogger.Warn("FORMATIONASSIGNMENT",
                        $"Position fix FAILED after {MaxPositionFixAttempts} attempts - player may be at wrong position");
                    _needsPositionFix = false; // Stop trying
                }

                // Retry lord attach if we couldn't find lord formation initially
                if (_needsLordAttachRetry && _lordAttachRetryAttempts < MaxLordAttachRetryAttempts)
                {
                    _lordAttachRetryAttempts++;
                    TryAttachToAlliedOrLordFormation("OnMissionTick-Retry");
                }
                else if (_lordAttachRetryAttempts >= MaxLordAttachRetryAttempts)
                {
                    _needsLordAttachRetry = false;
                }

                // Handle player party (companions and retinue) formation assignment.
                // Agents may not all be spawned in the first few ticks, so we keep trying.
                if (_needsPartyAssignment && !_partyAssignmentComplete &&
                    _partyAssignmentAttempts < MaxPartyAssignmentAttempts)
                {
                    _partyAssignmentAttempts++;
                    TryAssignPlayerPartyToFormation();
                }

                // Process deferred companion removals after the spawn phase completes.
                // This prevents crashes from FadeOut during OnAgentBuild corrupting the spawn loop.
                ProcessDeferredCompanionRemovals();
            }
            catch (Exception ex)
            {
                ModLogger.LogOnce("formation_tick_error", "FORMATIONASSIGNMENT",
                    $"Error in OnMissionTick: {ex.Message}");
            }
        }

        /// <summary>
        ///     Attempts to assign the player to their designated formation.
        ///     Safe to call multiple times - will only assign once per agent instance.
        /// </summary>
        private void TryAssignPlayerToFormation(string caller, Agent specificAgent = null)
        {
            // Get the player agent (either specific or Main)
            var playerAgent = specificAgent ?? Agent.Main;

            // If no agent or not main agent, skip
            if (playerAgent == null || !playerAgent.IsMainAgent)
            {
                // Only log once per callback to avoid spam
                if (caller == "AfterStart" || caller == "OnDeploymentFinished")
                {
                    ModLogger.Debug("FORMATIONASSIGNMENT",
                        $"[{caller}] No player agent yet (null: {playerAgent == null})");
                }

                return;
            }

            // Skip if already assigned for this specific agent instance
            if (playerAgent == _assignedAgent)
            {
                return;
            }

            // Skip in naval battles - the Naval DLC has its own ship-based spawn system
            // Our ground-based formation teleporting would interfere with ship positioning
            if (Mission.Current?.IsNavalBattle == true)
            {
                LogNavalBattleRetinueInfo(caller);
                return;
            }

            // Check if player is enlisted
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment?.IsEnlisted != true || enlistment.IsOnLeave)
            {
                // Not enlisted or on leave - don't force formation assignment
                // Log this at Info level since it's an important state decision
                ModLogger.LogOnce("formation_not_enlisted", "FORMATIONASSIGNMENT",
                    $"[{caller}] Skipping formation assignment - not enlisted or on leave " +
                    $"(Instance: {enlistment != null}, Enlisted: {enlistment?.IsEnlisted}, OnLeave: {enlistment?.IsOnLeave})");
                _assignedAgent = playerAgent; // Mark as handled so we don't keep checking
                return;
            }

            // Ensure mission is valid
            if (Mission.Current == null)
            {
                ModLogger.Debug("FORMATIONASSIGNMENT", $"[{caller}] Mission.Current is null");
                return;
            }

            // Check if agent is active (with null safety)
            try
            {
                if (!playerAgent.IsActive())
                {
                    ModLogger.Debug("FORMATIONASSIGNMENT", $"[{caller}] Player agent not active yet");
                    return;
                }
            }
            catch
            {
                // Agent might be in invalid state - skip
                ModLogger.Debug("FORMATIONASSIGNMENT", $"[{caller}] Player agent in invalid state");
                return;
            }

            // Get the player's team
            var playerTeam = playerAgent.Team;
            if (playerTeam == null)
            {
                // Team might not be assigned yet (especially in OnAgentBuild)
                // We'll try again next tick
                ModLogger.Debug("FORMATIONASSIGNMENT", $"[{caller}] Player team not assigned yet");
                return;
            }

            var enlistmentTier = enlistment.EnlistmentTier;
            var isCommanderTier = enlistmentTier >= RetinueManager.CommanderTier1;

            // T7+ (Commander tier): Player has their own party and controls their own formation.
            // Skip auto-assignment and let the native game handle it - they're commanders now.
            if (isCommanderTier)
            {
                ModLogger.Info("FORMATIONASSIGNMENT",
                    $"[{caller}] Commander tier (T{enlistmentTier}) - skipping formation assignment, player controls their own party");
                _assignedAgent = playerAgent;
                return;
            }

            // T1-T6: Soldiers are assigned to formation based on equipped weapons
            // They fight where the chain of command puts them based on their loadout
            // No squad command - they're regular soldiers in the ranks
            const bool isTier4Plus = false;
            var formationClass = DetectFormationFromEquipment();

            // Get the formation from the team
            var targetFormation = playerTeam.GetFormation(formationClass);
            if (targetFormation == null)
            {
                ModLogger.Debug("FORMATIONASSIGNMENT",
                    $"[{caller}] Could not get {formationClass} formation from team");
                return;
            }

            var targetFormationUnitCount = targetFormation.CountOfUnits;
            ModLogger.Debug("FORMATIONASSIGNMENT",
                $"[{caller}] Target formation={formationClass}, Units={targetFormationUnitCount}, PlayerTeamSide={playerTeam.Side}, MissionTeams={Mission.Current?.Teams.Count()}");

            // If already in this formation but it is effectively empty (only player), try to anchor to lord's formation instead.
            // In non-army battles with 0 party troops, native spawns the player in a solo formation at the rear.
            // We reattach to the lord's formation so the player spawns with the main line.
            var isSoloFormation = targetFormationUnitCount <= 1;

            if (playerAgent.Formation == targetFormation && !isSoloFormation)
            {
                ModLogger.Info("FORMATIONASSIGNMENT",
                    $"[{caller}] Player already in {formationClass} formation - will still check position");
                SetupSquadCommand(playerAgent, targetFormation, isTier4Plus);
                _assignedAgent = playerAgent;
                _playerSquadFormation = targetFormation;

                // FIX: Still need to check position even if already in correct formation
                // The game may have auto-assigned the player but spawned them in wrong location
                if (!_needsPositionFix)
                {
                    _needsPositionFix = true;
                    _positionFixAttempts = 0;
                }

                return;
            }

            // If formation is solo, try to attach to a populated allied formation on the same side; fallback to lord formation
            if (isSoloFormation && enlistment?.CurrentLord != null)
            {
                if (TryAttachToAlliedOrLordFormation(caller, playerAgent, playerTeam, isTier4Plus, formationClass))
                {
                    return;
                }
            }

            // Assign the player to the formation
            try
            {
                playerAgent.Formation = targetFormation;
                SetupSquadCommand(playerAgent, targetFormation, isTier4Plus);
                _assignedAgent = playerAgent;

                // Store the player's formation for party assignment
                _playerSquadFormation = targetFormation;

                // Mark that we need to teleport the player to their formation position
                // This handles cases where the player spawned late or in wrong location
                // because their map party was slightly behind the lord when battle started
                _needsPositionFix = true;
                _positionFixAttempts = 0;

                ModLogger.Info("FORMATIONASSIGNMENT",
                    $"[{caller}] Assigned enlisted soldier to {formationClass} formation (index: {targetFormation.Index})");
            }
            catch (Exception ex)
            {
                ModLogger.LogOnce($"assign_error_{playerAgent.Index}", "FORMATIONASSIGNMENT",
                    $"[{caller}] Failed to assign player to formation: {ex.Message}");
                // Mark as assigned to prevent spamming this error for this agent
                _assignedAgent = playerAgent;
            }
        }

        /// <summary>
        /// Attempts to attach the player to the lord's formation. Returns true if attached.
        /// </summary>
        private bool TryAttachToAlliedOrLordFormation(string caller, Agent playerAgent = null, Team playerTeam = null,
            bool isTier4Plus = false, FormationClass desiredClass = FormationClass.Infantry)
        {
            try
            {
                playerAgent ??= Agent.Main;
                playerTeam ??= playerAgent?.Team;

                var enlistment = EnlistmentBehavior.Instance;
                if (playerAgent == null || playerTeam == null || enlistment?.CurrentLord == null)
                {
                    return false;
                }

                var currentLord = enlistment.CurrentLord;
                Formation lordFormation = null;
                Team lordTeam = null;
                Formation bestAlliedFormation = null;
                var missionTeams = Mission.Current?.Teams;

                foreach (var missionTeam in missionTeams ?? Enumerable.Empty<Team>())
                {
                    if (missionTeam.Side == playerTeam.Side)
                    {
                        // Consider allied formations first
                        var preferred = missionTeam.GetFormation(desiredClass);
                        if (preferred != null && preferred.CountOfUnits > 1)
                        {
                            if (bestAlliedFormation == null || preferred.CountOfUnits > bestAlliedFormation.CountOfUnits)
                            {
                                bestAlliedFormation = preferred;
                            }
                        }

                        // If none in desired class, pick the largest non-empty allied formation
                        foreach (var formation in missionTeam.FormationsIncludingSpecialAndEmpty)
                        {
                            if (formation == null || formation.CountOfUnits <= 1)
                            {
                                continue;
                            }

                            if (bestAlliedFormation == null || formation.CountOfUnits > bestAlliedFormation.CountOfUnits)
                            {
                                bestAlliedFormation = formation;
                            }
                        }
                    }

                    foreach (var agent in missionTeam.ActiveAgents)
                    {
                        if (agent?.Character != null && agent.Character == currentLord.CharacterObject)
                        {
                            lordFormation = agent.Formation;
                            lordTeam = missionTeam;
                            break;
                        }
                    }

                    if (lordFormation != null)
                    {
                        break;
                    }
                }

                // Primary fallback: populated allied formation (prefer duty class)
                if (bestAlliedFormation != null)
                {
                    if (!_loggedSoloAttachOutcome)
                    {
                        ModLogger.Info("FORMATIONASSIGNMENT",
                            $"[{caller}] Joined allied formation (index {bestAlliedFormation.FormationIndex}, units {bestAlliedFormation.CountOfUnits})");
                        _loggedSoloAttachOutcome = true;
                    }

                    playerAgent.Formation = bestAlliedFormation;
                    _playerSquadFormation = bestAlliedFormation;
                    _assignedAgent = playerAgent;

                    SetupSquadCommand(playerAgent, bestAlliedFormation, isTier4Plus);

                    _needsPositionFix = true;
                    _positionFixAttempts = 0;

                    if (isTier4Plus)
                    {
                        _needsPartyAssignment = true;
                        _partyAssignmentAttempts = 0;
                        _partyAssignmentComplete = false;
                    }

                    _needsLordAttachRetry = false;
                    return true;
                }

                if (lordFormation != null && lordFormation.CountOfUnits > 0)
                {
                    if (!_loggedSoloAttachOutcome)
                    {
                        ModLogger.Info("FORMATIONASSIGNMENT",
                            $"[{caller}] Joined lord formation (index {lordFormation.FormationIndex}, units {lordFormation.CountOfUnits}, side={lordTeam.Side})");
                        _loggedSoloAttachOutcome = true;
                    }

                    playerAgent.Formation = lordFormation;
                    _playerSquadFormation = lordFormation;
                    _assignedAgent = playerAgent;

                    SetupSquadCommand(playerAgent, lordFormation, isTier4Plus);

                    // Ensure we teleport to the lord's line
                    _needsPositionFix = true;
                    _positionFixAttempts = 0;

                    if (isTier4Plus)
                    {
                        _needsPartyAssignment = true;
                        _partyAssignmentAttempts = 0;
                        _partyAssignmentComplete = false;
                    }

                    _needsLordAttachRetry = false;
                    return true;
                }

                var missionTeamCount = missionTeams?.Count() ?? 0;
                if (!_loggedSoloAttachMissing)
                {
                    ModLogger.Warn("FORMATIONASSIGNMENT",
                        $"[{caller}] Solo formation; no allied/lord formation yet (teams={missionTeamCount}). Will retry briefly.");
                    _loggedSoloAttachMissing = true;
                }

                // Schedule retry on tick (lord may not be spawned yet)
                _needsLordAttachRetry = true;
                _lordAttachRetryAttempts = 0;
                return false;
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("FORMATIONASSIGNMENT", "Error during lord attach", ex,
                    LogCtx.Of("Mission", Mission.Current?.Mode.ToString(), "Formation", desiredClass.ToString()));
                return false;
            }
        }

        /// <summary>
        ///     Attempts to teleport the player to the correct position within their assigned formation.
        ///     This fixes the issue where the player spawns behind the formation when their map party
        ///     was slightly behind the lord when battle started.
        ///
        ///     Only applies to T1-T6 soldiers. T7+ commanders control their own party and spawn position.
        ///
        ///     CRITICAL: We find an ARMY agent (not from player's party) to use as reference position,
        ///     because CachedMedianPosition includes our own party members who spawned at the wrong spot.
        /// </summary>
        private void TryTeleportPlayerToFormationPosition()
        {
            try
            {
                var playerAgent = Agent.Main;
                if (playerAgent == null || !playerAgent.IsMainAgent || !playerAgent.IsActive())
                {
                    return;
                }

                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true || enlistment.IsOnLeave)
                {
                    _needsPositionFix = false;
                    return;
                }

                // T7+ commanders control their own party - don't teleport them
                if (enlistment.EnlistmentTier >= RetinueManager.CommanderTier1)
                {
                    _needsPositionFix = false;
                    return;
                }

                var formation = playerAgent.Formation;
                if (formation == null)
                {
                    return;
                }

                var team = playerAgent.Team;
                if (team?.ActiveAgents == null)
                {
                    return;
                }

                var playerPosition = playerAgent.Position;
                var mainParty = PartyBase.MainParty;

                // Detect formation from player's equipped weapons and mount
                var formationClass = DetectFormationFromEquipment();
                var currentLord = enlistment.CurrentLord;
                Vec3 targetPosition = Vec3.Invalid;
                Vec2 formationDirection = Vec2.Forward;
                string teleportSource = "unknown";

                // PRIORITY 1: Use the formation on the player's team directly
                // All allied troops (lord's army + player's party) are on the SAME team in Bannerlord battles
                // The formation's CachedMedianPosition includes all units, giving us the army's center position
                var targetFormation = team.GetFormation(formationClass);
                if (targetFormation != null && targetFormation.CountOfUnits > 0)
                {
                    var unitCount = targetFormation.CountOfUnits;
                    formationDirection = targetFormation.Direction.IsValid ? targetFormation.Direction : Vec2.Forward;

                    // Check if this is a siege assault - in sieges, we want to spawn WITH the formation
                    // not behind it, since "behind" during a siege assault means far from the walls/action
                    var isSiegeAssault = Mission.Current?.IsSiegeBattle == true;

                    if (isSiegeAssault)
                    {
                        // For siege assaults: spawn at formation center to be with the attacking troops
                        targetPosition = targetFormation.CachedMedianPosition.GetGroundVec3();
                        teleportSource = $"{formationClass} formation ({unitCount} units, siege assault)";
                    }
                    else
                    {
                        // For field battles: position several meters behind the formation
                        // so player spawns at the rear rank, not stuck in the middle of troops
                        var behindOffset = -formationDirection.ToVec3() * 5f;
                        targetPosition = targetFormation.CachedMedianPosition.GetGroundVec3() + behindOffset;
                        teleportSource = $"{formationClass} formation ({unitCount} units)";
                    }
                }

                // FALLBACK 1: If no allied formation found, fall back to LORD position
                if (!targetPosition.IsValid && currentLord != null && Mission.Current?.Teams != null)
                {
                    foreach (var missionTeam in Mission.Current.Teams)
                    {
                        if (missionTeam.Side != team.Side)
                        {
                            continue;
                        }

                        foreach (var agent in missionTeam.ActiveAgents)
                        {
                            if (agent?.Character != null && agent.Character == currentLord.CharacterObject)
                            {
                                targetPosition = agent.Position;
                                formationDirection = formation.Direction.IsValid ? formation.Direction : Vec2.Forward;
                                teleportSource = $"Lord {currentLord.Name} (fallback)";
                                break;
                            }
                        }

                        if (targetPosition.IsValid)
                        {
                            break;
                        }
                    }
                }

                // FALLBACK 2: If still nothing, use ANY non-player-party agent (last resort)
                if (!targetPosition.IsValid && Mission.Current?.Teams != null)
                {
                    foreach (var missionTeam in Mission.Current.Teams)
                    {
                        if (missionTeam.Side != team.Side)
                        {
                            continue;
                        }

                        foreach (var agent in missionTeam.ActiveAgents)
                        {
                            if (agent == null || !agent.IsActive() || agent == playerAgent)
                            {
                                continue;
                            }

                            if (agent.Origin is PartyGroupAgentOrigin partyOrigin && partyOrigin.Party == mainParty)
                            {
                                continue;
                            }

                            targetPosition = agent.Position;
                            formationDirection = formation.Direction.IsValid ? formation.Direction : Vec2.Forward;
                            teleportSource = "allied agent (fallback)";
                            break;
                        }

                        if (targetPosition.IsValid)
                        {
                            break;
                        }
                    }
                }

                // If no valid target found, retry next tick
                if (!targetPosition.IsValid)
                {
                    return;
                }

                // Calculate distance from player to formation
                var distanceToTarget = playerPosition.Distance(targetPosition);
                const float teleportThreshold = 10f;

                if (distanceToTarget > teleportThreshold)
                {
                    var prePos = playerAgent.Position;

                    // Teleport player to formation position (or fallback to lord/agent)
                    playerAgent.TeleportToPosition(targetPosition);
                    playerAgent.SetMovementDirection(formationDirection);
                    playerAgent.LookDirection = formationDirection.ToVec3();
                    playerAgent.ForceUpdateCachedAndFormationValues(true, false);

                    var postPos = playerAgent.Position;
                    var actualMovement = prePos.Distance(postPos);

                    ModLogger.Info("FORMATIONASSIGNMENT",
                        $"Teleported soldier to {teleportSource}: " +
                        $"was {distanceToTarget:F1}m away, moved {actualMovement:F1}m, " +
                        $"from ({prePos.x:F1},{prePos.y:F1}) to ({postPos.x:F1},{postPos.y:F1})");

                    if (actualMovement < 5f && distanceToTarget > 20f)
                    {
                        ModLogger.Warn("FORMATIONASSIGNMENT",
                            $"TELEPORT MAY HAVE FAILED: Expected ~{distanceToTarget:F1}m but only moved {actualMovement:F1}m!");
                    }
                }
                else
                {
                    // Log at INFO level - this is an important diagnostic for understanding spawn positions
                    ModLogger.Info("FORMATIONASSIGNMENT",
                        $"Player already near {teleportSource} ({distanceToTarget:F1}m <= {teleportThreshold}m) - no teleport needed");
                }

                _needsPositionFix = false;
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("FORMATIONASSIGNMENT", "Error teleporting player to formation", ex,
                    LogCtx.Of("Mission", Mission.Current?.Mode.ToString(), "Formation", Agent.Main?.Formation?.FormationIndex.ToString()));
                _needsPositionFix = false;
            }
        }

        /// <summary>
        /// Teleports all squad members (retinue + companions) to positions near the player within the formation.
        /// This ensures the entire squad spawns together with their troop type formation, not behind the line.
        /// </summary>
        private int TeleportSquadToFormation(Agent playerAgent, Formation formation, Vec3 formationCenter)
        {
            var teleportedCount = 0;

            try
            {
                var team = playerAgent.Team;
                if (team?.ActiveAgents == null)
                {
                    return 0;
                }

                var mainParty = PartyBase.MainParty;
                if (mainParty == null)
                {
                    return 0;
                }

                // Get formation direction for proper positioning
                var formationDirection = formation.Direction.IsValid ? formation.Direction : Vec2.Forward;
                var formationRight = formationDirection.RightVec();

                // Position squad members in a small cluster around the formation center
                // Spread them out slightly so they don't stack on top of each other
                var squadIndex = 0;
                const float squadSpacing = 1.5f; // meters between squad members

                foreach (var agent in team.ActiveAgents)
                {
                    // Skip the player
                    if (agent == playerAgent || agent == null || !agent.IsActive())
                    {
                        continue;
                    }

                    // Only teleport agents from player's party
                    if (agent.Origin is not PartyGroupAgentOrigin partyOrigin || partyOrigin.Party != mainParty)
                    {
                        continue;
                    }

                    // Skip companions that were faded out (stay back)
                    if (!agent.IsActive())
                    {
                        continue;
                    }

                    // Calculate offset position in a grid pattern around the player
                    // This keeps the squad together but not stacked
                    var row = squadIndex / 3;
                    var col = (squadIndex % 3) - 1; // -1, 0, 1 for left, center, right

                    var offsetForward = -row * squadSpacing; // Behind the player slightly
                    var offsetRight = col * squadSpacing;

                    var squadPosition = formationCenter
                        + formationDirection.ToVec3() * offsetForward
                        + formationRight.ToVec3() * offsetRight;

                    agent.TeleportToPosition(squadPosition);

                    // Face same direction as formation
                    if (formation.Direction.IsValid)
                    {
                        agent.SetMovementDirection(formation.Direction);
                        agent.LookDirection = formation.Direction.ToVec3();
                    }

                    agent.ForceUpdateCachedAndFormationValues(true, false);

                    teleportedCount++;
                    squadIndex++;
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("FORMATIONASSIGNMENT", "Error teleporting squad to formation", ex);
            }

            return teleportedCount;
        }

        /// <summary>
        ///     Assigns all agents from the player's party (companions and retinue) to the same formation.
        ///     At Commander tier (T7+), the player commands a unified squad of their personal troops.
        ///     This ensures companions and retinue soldiers fight together with the player.
        ///     FIX: Now also teleports reassigned soldiers to the player's position, since they may have
        ///     spawned in a different formation's location (e.g., infantry retinue spawns with Infantry
        ///     formation even when player's duty is Ranged).
        /// </summary>
        private void TryAssignPlayerPartyToFormation()
        {
            try
            {
                var playerAgent = Agent.Main;
                if (playerAgent == null || !playerAgent.IsMainAgent || !playerAgent.IsActive())
                {
                    return;
                }

                // Verify we have a valid formation to assign to
                if (_playerSquadFormation == null)
                {
                    ModLogger.Debug("FORMATIONASSIGNMENT", "Party assignment skipped: no player formation set");
                    return;
                }

                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true || enlistment.IsOnLeave)
                {
                    _needsPartyAssignment = false;
                    return;
                }

                // Check tier requirement (Commander tier / T7+)
                if (enlistment.EnlistmentTier < RetinueManager.CommanderTier1)
                {
                    _needsPartyAssignment = false;
                    return;
                }

                var team = playerAgent.Team;
                if (team == null || team.ActiveAgents == null)
                {
                    return;
                }

                // Get player's main party for comparison
                var mainParty = PartyBase.MainParty;
                if (mainParty == null)
                {
                    ModLogger.Debug("FORMATIONASSIGNMENT", "Party assignment skipped: MainParty is null");
                    return;
                }

                // Track assignment statistics for logging
                var assignedCompanions = 0;
                var assignedRetinue = 0;
                var alreadyAssigned = 0;
                var agentsChecked = 0;
                var partyAgentsFound = 0;
                var teleportedCount = 0;

                // Get player position for teleporting reassigned soldiers
                var playerPosition = playerAgent.Position;
                var formationDirection = _playerSquadFormation.Direction.IsValid
                    ? _playerSquadFormation.Direction
                    : Vec2.Forward;
                var formationRight = formationDirection.RightVec();

                // Track how many soldiers we've positioned for grid layout
                var positionIndex = 0;
                const float squadSpacing = 1.5f;

                // Determine teleport threshold based on spawn type
                // Reinforcements spawn at map edge, so always teleport them to the player
                var isReinforcement = IsReinforcementPhase();
                var minTeleportDistanceSquared = isReinforcement ? 0f : 25f; // 0m for reinforcements, 5m for initial

                // Create a snapshot of agents to iterate (avoids collection modification issues)
                var agentSnapshot = new List<Agent>(team.ActiveAgents);

                foreach (var agent in agentSnapshot)
                {
                    // Skip the player themselves
                    if (agent == playerAgent)
                    {
                        continue;
                    }

                    // Skip dead or invalid agents
                    if (agent == null || !agent.IsActive())
                    {
                        continue;
                    }

                    agentsChecked++;

                    // Check if this agent originated from the player's main party
                    // PartyGroupAgentOrigin is the standard origin type for campaign battle agents
                    if (agent.Origin is PartyGroupAgentOrigin partyOrigin && partyOrigin.Party == mainParty)
                    {
                        partyAgentsFound++;

                        // Skip agents that have been faded out (e.g., "stay back" companions)
                        if (!agent.IsActive())
                        {
                            continue;
                        }

                        // Track if this agent needs teleporting (was in wrong formation)
                        var needsTeleport = agent.Formation != _playerSquadFormation;

                        // Check if already in the correct formation
                        if (agent.Formation == _playerSquadFormation)
                        {
                            alreadyAssigned++;
                        }
                        else
                        {
                            // Assign to player's squad formation
                            agent.Formation = _playerSquadFormation;

                            // Track what type of agent was assigned for logging
                            if (agent.Character?.IsHero == true)
                            {
                                assignedCompanions++;
                                ModLogger.Debug("FORMATIONASSIGNMENT",
                                    $"Assigned companion {agent.Character.Name} to player's squad");
                            }
                            else
                            {
                                assignedRetinue++;
                                var troopName = agent.Character?.Name?.ToString() ?? "unknown";
                                ModLogger.Debug("FORMATIONASSIGNMENT",
                                    $"Assigned retinue soldier {troopName} to player's squad");
                            }
                        }

                        // Teleport soldiers who were in a different formation to be near the player
                        // This fixes the issue where infantry retinue spawns with Infantry formation
                        // even when the player's duty puts them in Ranged formation
                        if (needsTeleport)
                        {
                            var agentPosition = agent.Position;
                            var distanceSquared = (agentPosition.AsVec2 - playerPosition.AsVec2).LengthSquared;

                            if (distanceSquared > minTeleportDistanceSquared)
                            {
                                // Calculate position in a grid behind/around the player
                                var row = positionIndex / 3;
                                var col = (positionIndex % 3) - 1; // -1, 0, 1 for left, center, right

                                var offsetForward = -row * squadSpacing - 2f; // Start 2m behind player
                                var offsetRight = col * squadSpacing;

                                var targetPosition = playerPosition
                                    + formationDirection.ToVec3() * offsetForward
                                    + formationRight.ToVec3() * offsetRight;

                                agent.TeleportToPosition(targetPosition);

                                // Face same direction as formation
                                if (_playerSquadFormation.Direction.IsValid)
                                {
                                    agent.SetMovementDirection(_playerSquadFormation.Direction);
                                    agent.LookDirection = _playerSquadFormation.Direction.ToVec3();
                                }

                                agent.ForceUpdateCachedAndFormationValues(true, false);
                                teleportedCount++;
                            }

                            positionIndex++;
                        }
                    }
                }

                // Ensure formation is player-controlled for squad commands
                // This reinforces the SetupSquadCommand call but handles late-spawning party members
                if (_playerSquadFormation.PlayerOwner != playerAgent)
                {
                    _playerSquadFormation.PlayerOwner = playerAgent;
                    _playerSquadFormation.SetControlledByAI(false);
                    ModLogger.Debug("FORMATIONASSIGNMENT", "Reinforced player control of squad formation");
                }

                // CRITICAL: Clear captain if it's a player party member (companion)
                // The game auto-assigns captains based on hero power, which can make companions captain
                // This breaks the player's command experience - they see their companion giving orders
                var currentCaptain = _playerSquadFormation.Captain;
                if (currentCaptain != null && currentCaptain != playerAgent)
                {
                    // Check if captain is from player's party
                    if (currentCaptain.Origin is PartyGroupAgentOrigin captainOrigin &&
                        captainOrigin.Party == mainParty)
                    {
                        var captainName = currentCaptain.Character?.Name?.ToString() ?? "Unknown";
                        _playerSquadFormation.Captain = null;
                        ModLogger.Info("FORMATIONASSIGNMENT",
                            $"Cleared companion {captainName} as formation captain (player should command)");
                    }
                }

                // Log summary if we assigned anyone
                var totalAssigned = assignedCompanions + assignedRetinue;
                if (totalAssigned > 0 || partyAgentsFound > 0)
                {
                    var teleportInfo = teleportedCount > 0 ? $", teleported={teleportedCount}" : "";
                    var spawnType = isReinforcement ? " [REINFORCEMENT]" : "";
                    ModLogger.Info("FORMATIONASSIGNMENT",
                        $"Player party assignment{spawnType}: checked={agentsChecked}, found={partyAgentsFound}, " +
                        $"companions={assignedCompanions}, retinue={assignedRetinue}, already={alreadyAssigned}{teleportInfo}");
                }

                // Mark complete when we've either assigned agents or done enough attempts
                // We keep trying for a while because agents may spawn in waves (reinforcements)
                if (partyAgentsFound > 0 || _partyAssignmentAttempts >= MaxPartyAssignmentAttempts / 2)
                {
                    _partyAssignmentComplete = true;

                    if (partyAgentsFound > 0)
                    {
                        // Log the formation class (Infantry, Ranged, etc.) not PhysicalClass which can be misleading
                        var formationName = _playerSquadFormation.FormationIndex.ToString();
                        ModLogger.Info("FORMATIONASSIGNMENT",
                            $"=== UNIFIED SQUAD FORMED === {partyAgentsFound} party members in {formationName} formation (index {_playerSquadFormation.Index})");
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("FORMATIONASSIGNMENT", "Error assigning player party to formation", ex);
                _partyAssignmentComplete = true; // Stop trying on error
            }
        }

        /// <summary>
        ///     Sets up command authority for the enlisted player based on their tier.
        ///     At Commander tier (T7+): Player becomes sergeant and can command their own formation.
        ///                             The formation is made player-controllable via SetControlledByAI(false).
        ///     Below T7: Player has no command authority - just a soldier in the ranks.
        /// </summary>
        private void SetupSquadCommand(Agent playerAgent, Formation formation, bool isTier4Plus)
        {
            if (playerAgent == null || formation == null || EnlistmentBehavior.Instance?.IsEnlisted != true)
            {
                return;
            }

            try
            {
                var team = playerAgent.Team;
                if (team == null)
                {
                    return;
                }

                if (isTier4Plus)
                {
                    // COMMANDER TIER (T7+) SQUAD COMMAND SETUP
                    // The player can command their own formation like a sergeant

                    // CRITICAL: Make this specific formation player-controlled
                    // Without this, SetPlayerRole makes ALL formations AI-controlled
                    // This matches what Team.AssignPlayerAsSergeantOfFormation does
                    formation.SetControlledByAI(false);
                    formation.PlayerOwner = playerAgent;

                    // Set player as sergeant (can command own formation only)
                    team.SetPlayerRole(isPlayerGeneral: false, isPlayerSergeant: true);

                    // Strip captaincy if player is captain of a different formation
                    // (shouldn't happen, but safety check)
                    if (formation.Captain == playerAgent)
                    {
                        // Player can remain as captain of their own formation
                    }

                    ModLogger.Info("FORMATIONASSIGNMENT",
                        $"Command Setup: Commander tier (T7+) sergeant mode - formation {formation.FormationIndex} is player-controlled");
                }
                else
                {
                    // BELOW T7: NO COMMAND AUTHORITY
                    // Player is just a soldier in the formation, cannot issue orders

                    // Strip any command roles
                    if (team.IsPlayerGeneral || team.IsPlayerSergeant)
                    {
                        team.SetPlayerRole(isPlayerGeneral: false, isPlayerSergeant: false);
                    }

                    // Strip captaincy if somehow assigned
                    if (formation.Captain == playerAgent)
                    {
                        formation.Captain = null;
                    }

                    // Transfer army command to the lord
                    if (team.PlayerOrderController?.Owner == playerAgent)
                    {
                        TransferArmyCommandToLord(playerAgent, team);
                    }

                    ModLogger.Info("FORMATIONASSIGNMENT",
                        $"Command Setup: Below T7 - no command authority");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("FORMATIONASSIGNMENT", "Error setting up squad command", ex,
                    LogCtx.Of("Mission", Mission.Current?.Mode.ToString(), "Formation", formation?.FormationIndex.ToString()));
            }
        }

        /// <summary>
        ///     Transfers army-wide command from player to the lord or another suitable agent.
        ///     Used for enlisted soldiers below T7 who shouldn't have command authority.
        /// </summary>
        private void TransferArmyCommandToLord(Agent playerAgent, Team team)
        {
            try
            {
                // Try to find the Lord to give command to
                var lord = EnlistmentBehavior.Instance?.CurrentLord;
                Agent lordAgent = null;

                if (lord != null && team.ActiveAgents != null)
                {
                    foreach (var agent in team.ActiveAgents)
                    {
                        if (agent.Character == lord.CharacterObject)
                        {
                            lordAgent = agent;
                            break;
                        }
                    }
                }

                if (lordAgent != null)
                {
                    team.PlayerOrderController.Owner = lordAgent;
                    ModLogger.Debug("FORMATIONASSIGNMENT", $"Army command transferred to Lord {lord.Name}");
                }
                else
                {
                    // Fallback to team general
                    var general = team.GeneralAgent;
                    if (general != null && general != playerAgent)
                    {
                        team.PlayerOrderController.Owner = general;
                        ModLogger.Debug("FORMATIONASSIGNMENT", "Army command transferred to Team General");
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("FORMATIONASSIGNMENT", "Error transferring army command", ex);
            }
        }

        /// <summary>
        /// Detects the appropriate formation for the player based on their equipped weapons and mount.
        /// Uses the CharacterObject's IsRanged and IsMounted properties which check battle equipment.
        /// </summary>
        private FormationClass DetectFormationFromEquipment()
        {
            try
            {
                var hero = Hero.MainHero;
                if (hero?.CharacterObject == null)
                {
                    return FormationClass.Infantry;
                }

                var character = hero.CharacterObject;

                // Horse + ranged weapon = Horse Archer formation
                if (character.IsRanged && character.IsMounted)
                {
                    ModLogger.Debug("FORMATIONASSIGNMENT", "Equipment detection: Horse Archer (mounted + ranged)");
                    return FormationClass.HorseArcher;
                }

                // Horse equipped = Cavalry formation
                if (character.IsMounted)
                {
                    ModLogger.Debug("FORMATIONASSIGNMENT", "Equipment detection: Cavalry (mounted)");
                    return FormationClass.Cavalry;
                }

                // Ranged weapon (bow, crossbow, throwing) = Ranged formation
                if (character.IsRanged)
                {
                    ModLogger.Debug("FORMATIONASSIGNMENT", "Equipment detection: Ranged (bow/crossbow/throwing)");
                    return FormationClass.Ranged;
                }

                // Default to infantry (melee weapons or no weapons)
                ModLogger.Debug("FORMATIONASSIGNMENT", "Equipment detection: Infantry (default)");
                return FormationClass.Infantry;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("FORMATIONASSIGNMENT", "Error detecting formation from equipment", ex);
                return FormationClass.Infantry;
            }
        }

        /// <summary>
        /// Logs retinue state when naval battle detected. Formation assignment deferred to Naval DLC.
        /// </summary>
        private static void LogNavalBattleRetinueInfo(string caller)
        {
            var manager = RetinueManager.Instance;
            var enlistment = EnlistmentBehavior.Instance;

            var retinueCount = manager?.State?.TotalSoldiers ?? 0;
            var retinueType = manager?.State?.SelectedTypeId ?? "none";
            var companionCount = RetinueManager.GetCompanionCount();
            var isMountedType = retinueType is "cavalry" or "horse_archers";
            var partySize = PartyBase.MainParty?.NumberOfAllMembers ?? 0;

            // Single consolidated log entry for naval battle context
            var logMessage = $"[{caller}] NAVAL: Retinue={retinueCount} {retinueType}, " +
                             $"Companions={companionCount}, Party={partySize}";

            if (isMountedType && retinueCount > 0)
            {
                logMessage += " [dismounted]";
            }

            if (enlistment?.IsEnlisted == true)
            {
                logMessage += $", Tier={enlistment.EnlistmentTier}";
            }

            ModLogger.LogOnce("formation_naval_skip", "FORMATIONASSIGNMENT", logMessage);
        }

        /// <summary>
        /// Checks if a companion should spawn and fight in battle.
        /// Delegates to CompanionAssignmentManager for the actual check.
        /// </summary>
        private static bool ShouldCompanionFight(Hero companion)
        {
            if (companion == null)
            {
                return true;
            }

            var manager = CompanionAssignmentManager.Instance;
            return manager?.ShouldCompanionFight(companion) ?? true;
        }

        protected override void OnEndMission()
        {
            try
            {
                base.OnEndMission();

                // Reset player assignment state
                _assignedAgent = null;
                _needsPositionFix = false;
                _positionFixAttempts = 0;

                // Reset player party assignment state
                _needsPartyAssignment = false;
                _partyAssignmentComplete = false;
                _partyAssignmentAttempts = 0;
                _playerSquadFormation = null;

                // Clear deferred companion removal queue
                _companionsToRemove.Clear();
                _companionRemovalDelayCounter = 0;

                // Clear spawn logic reference
                _spawnLogic = null;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("FORMATIONASSIGNMENT", "Error in OnEndMission", ex);
            }
        }
    }
}
