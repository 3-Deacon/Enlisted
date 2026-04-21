using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Enlisted.Features.Camp;
using Enlisted.Features.Combat.Behaviors;
using Enlisted.Features.Conditions;
using Enlisted.Features.Content;
using Enlisted.Features.Conversations.Behaviors;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Equipment.Behaviors;
using Enlisted.Features.Equipment.UI;
using Enlisted.Features.Escalation;
using Enlisted.Features.Identity;
using Enlisted.Features.Interface;
using Enlisted.Features.Interface.Behaviors;
using Enlisted.Features.Logistics;
using Enlisted.Features.Orders.Behaviors;
using Enlisted.Features.Ranks.Behaviors;
using Enlisted.Features.Retinue.Core;
using Enlisted.Features.Retinue.Systems;
using Enlisted.Mod.Core;
// Phase 1: Assignments, Lances, Schedule systems deleted
using Enlisted.Mod.Core.Config;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Triggers;
using Enlisted.Mod.GameAdapters.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Enlisted.Mod.Entry
{
    /// <summary>
    ///     Defers action execution to the next game frame to prevent race conditions during game state transitions.
    ///     The RGL (Rendering Graphics Library) skeleton system performs time-based updates during encounter exits.
    ///     Executing menu transitions or state changes during these updates can cause assertion failures because
    ///     the system expects a non-zero time delta, but rapid re-entrant calls can create zero-delta situations.
    ///     By deferring actions, we ensure they execute after the critical update phase completes.
    /// </summary>
    public static class NextFrameDispatcher
    {
        private static readonly Queue<DeferredAction> NextFrameQueue = new Queue<DeferredAction>();

        /// <summary>
        ///     Queues an action to execute on the next game frame tick instead of immediately.
        ///     Used for menu activations, encounter finishing, and other state transitions that must occur
        ///     after the current frame's critical updates complete. This prevents timing conflicts with
        ///     the game's internal systems during state transitions.
        /// </summary>
        /// <param name="action">The action to execute on the next frame. Null actions are ignored.</param>
        /// <param name="requireNoEncounter">
        ///     When true, the action will be deferred until no player encounter is active.
        ///     This allows callers to delay sensitive transitions (like menu activation) until
        ///     the encounter lifecycle has fully completed.
        /// </param>
        public static void RunNextFrame(Action action, bool requireNoEncounter = false)
        {
            if (action != null)
            {
                NextFrameQueue.Enqueue(new DeferredAction(action, requireNoEncounter));
            }
        }

        /// <summary>
        ///     Executes queued deferred actions, processing them in FIFO order.
        ///     Called automatically by the Harmony patch on Campaign.Tick() after the native tick completes.
        ///     Actions that request "no encounter" will be re-queued until PlayerEncounter.Current is null.
        /// </summary>
        public static void ProcessNextFrame()
        {
            if (NextFrameQueue.Count == 0)
            {
                return;
            }

            var itemsToProcess = NextFrameQueue.Count;
            while (itemsToProcess-- > 0 && NextFrameQueue.Count > 0)
            {
                var deferred = NextFrameQueue.Dequeue();
                if (deferred?.Action == null)
                {
                    continue;
                }

                if (deferred.RequireNoEncounter && PlayerEncounter.Current != null)
                {
                    // Re-queue until the encounter fully finishes
                    NextFrameQueue.Enqueue(deferred);
                    continue;
                }

                try
                {
                    deferred.Action();
                }
                catch (Exception ex)
                {
                    ModLogger.Caught("NextFrameDispatcher", "Error processing next frame action", ex);
                }
            }
        }

        private sealed class DeferredAction
        {
            public DeferredAction(Action action, bool requireNoEncounter)
            {
                Action = action;
                RequireNoEncounter = requireNoEncounter;
            }

            public Action Action { get; }
            public bool RequireNoEncounter { get; }
        }
    }

    public class SubModule : MBSubModuleBase
    {
        private Harmony _harmony;
        private ModSettings _settings;

        /// <summary>
        ///     Called when the mod module is first loaded by Bannerlord, before any game starts.
        ///     Initializes logging system and applies Harmony patches to game methods.
        ///     This happens once per game session, not per save game.
        /// </summary>
        protected override void OnSubModuleLoad()
        {
            try
            {
                // Initialize logging system - this clears old logs and starts fresh
                ModLogger.Initialize();
                ModLogger.Info("Bootstrap", "SubModule loading");

                // Create Harmony instance with a unique identifier to avoid patch collisions
                // PatchAll() automatically discovers and applies all [HarmonyPatch] attributes in the assembly
                _harmony = new Harmony("com.enlisted.mod");

                try
                {
                    // Reference patch types to satisfy static analysis (ReSharper)
                    // These are discovered via reflection by Harmony.PatchAll()
                    // Listed alphabetically by patch file for easy maintenance
                    _ = typeof(AbandonArmyBlockPatch);
                    _ = typeof(EncounterAbandonArmyBlockPatch);
                    _ = typeof(EncounterAbandonArmyBlockPatch2);
                    _ = typeof(ArmyCohesionExclusionPatch);
                    _ = typeof(ArmyDispersedMenuPatch);
                    _ = typeof(CheckFortificationAttackablePatch);
                    _ = typeof(CombatLogConversationPatch.MapConversationEndPatch);
                    _ = typeof(CompanionCaptainBlockPatch);
                    _ = typeof(CompanionGeneralBlockPatch);
                    _ = typeof(DischargeRelationPenaltyPatch);
                    _ = typeof(DutiesEffectiveEngineerPatch);
                    _ = typeof(DutiesEffectiveScoutPatch);
                    _ = typeof(DutiesEffectiveQuartermasterPatch);
                    _ = typeof(DutiesEffectiveSurgeonPatch);
                    _ = typeof(EncounterSuppressionPatch);
                    _ = typeof(EncounterLeaveSuppressionPatch);
                    _ = typeof(EndCaptivityCleanupPatch);
                    _ = typeof(EnlistedWaitingPatch);
                    _ = typeof(FormationMessageSuppressionPatch);
                    _ = typeof(GenericStateMenuPatch);
                    _ = typeof(HidePartyNamePlatePatch);
                    _ = typeof(IncidentsSuppressionPatch);
                    _ = typeof(InfluenceMessageSuppressionPatch);
                    _ = typeof(InformationManagerDisplayMessagePatch);
                    _ = typeof(JoinEncounterAutoSelectPatch);
                    _ = typeof(LootBlockPatch);
                    _ = typeof(LootBlockPatch.ItemLootPatch);
                    _ = typeof(LootBlockPatch.MemberLootPatch);
                    _ = typeof(LootBlockPatch.PrisonerLootPatch);
                    _ = typeof(LootBlockPatch.LootScreenPatch);
                    _ = typeof(NavalBattleArmyWaitCrashFix);
                    _ = typeof(NavalBattleShipAssignmentPatch);
                    _ = typeof(NavalNavigationCapabilityPatch);
                    _ = typeof(NavalShipDamageProtectionPatch);
                    _ = typeof(OrderOfBattleSuppressionPatch);
                    _ = typeof(PlayerIsAtSeaTagCrashFix);
                    _ = typeof(PostDischargeProtectionPatch);
                    _ = typeof(RaftStateSuppressionPatch);
                    _ = typeof(ReturnToArmySuppressionPatch);
                    _ = typeof(SettlementOutsideLeaveButtonPatch);
                    _ = typeof(SkillSuppressionPatch);
                    _ = typeof(TownLeaveButtonPatch);
                    _ = typeof(VisibilityEnforcementPatch);

                    // These patches are deferred until first Campaign.Tick() to avoid TypeInitializationException.
                    // Their target classes (DefaultArmyManagementCalculationModel, EncounterGameMenuBehavior)
                    // have static fields that call GameTexts.FindText() before the localization system is ready.
                    // This causes crashes on Proton/Linux and potential issues on Windows under some conditions.
                    var manualPatches = new HashSet<string>
                    {
                        "HidePartyNamePlatePatch",                // Uses manual patching via ApplyManualPatches()
                        "ArmyCohesionExclusionPatch",             // Target: DefaultArmyManagementCalculationModel
                        "CheckFortificationAttackablePatch",      // Target: EncounterGameMenuBehavior (deferred)
                        "SettlementOutsideLeaveButtonPatch",      // Target: EncounterGameMenuBehavior
                        "JoinEncounterAutoSelectPatch",           // Target: EncounterGameMenuBehavior
                        "EncounterAbandonArmyBlockPatch",         // Target: EncounterGameMenuBehavior (deferred)
                        "EncounterAbandonArmyBlockPatch2",        // Target: EncounterGameMenuBehavior (deferred)
                        "EncounterLeaveSuppressionPatch",         // Target: EncounterGameMenuBehavior (deferred)
                        "MapConversationEndPatch"                 // Target: MapScreen explicit interface (deferred)
                    };

                    var assembly = Assembly.GetExecutingAssembly();
                    var patchTypes = assembly.GetTypes()
                        .Where(t => t.GetCustomAttributes(typeof(HarmonyPatch), true).Length > 0 ||
                                    t.GetNestedTypes().Any(n =>
                                        n.GetCustomAttributes(typeof(HarmonyPatch), true).Length > 0));

                    var enabledCount = 0;
                    var skippedCount = 0;

                    foreach (var type in patchTypes)
                    {
                        // Skip patches that use manual patching
                        if (manualPatches.Contains(type.Name))
                        {
                            ModLogger.Info("Bootstrap", $"Skipping {type.Name} (uses manual patching)");
                            skippedCount++;
                            continue;
                        }

                        try
                        {
                            _ = _harmony.CreateClassProcessor(type).Patch();
                            enabledCount++;
                        }
                        catch (Exception patchEx)
                        {
                            ModLogger.Caught("Bootstrap", $"Failed to patch {type.Name}", patchEx);
                            if (patchEx.InnerException != null)
                            {
                                ModLogger.Caught("Bootstrap", "Inner exception during patch", patchEx.InnerException);
                            }
                        }
                    }

                    // Deferred patches will be applied later when campaign starts
                    // Applying during SubModule load causes TypeInitializationException on Proton/Linux
                    ModLogger.Info("Bootstrap", $"{skippedCount} patches deferred until campaign start (2 nested classes)");

                    ModLogger.Info("Bootstrap", $"Harmony patches: {enabledCount} auto, {skippedCount} manual");
                }
                catch (Exception ex)
                {
                    ModLogger.Caught("Bootstrap", "Harmony PatchAll failed", ex);
                }

                // Log all patched methods for debugging
                var patchedMethods = _harmony.GetPatchedMethods();
                foreach (var method in patchedMethods)
                {
                    ModLogger.Debug("Bootstrap", $"Patched method: {method.DeclaringType?.Name}.{method.Name}");
                }

                // Run mod conflict diagnostics and write to Debugging/Conflicts-A_*.log
                // This helps users identify when other mods interfere with Enlisted
                ModConflictDiagnostics.RunStartupDiagnostics(_harmony);

                ModLogger.Info("Bootstrap", "Harmony patched");
            }
            catch (Exception ex)
            {
                // If patching fails, log the error but don't crash the game
                // The mod may still function partially without patches, and crashing would prevent the player from playing
                ModLogger.Caught("Bootstrap", "Exception during OnSubModuleLoad", ex);
            }
        }

        /// <summary>
        ///     Called when a new game starts or when loading a save game.
        ///     Registers all campaign behaviors that manage the military service system throughout the campaign.
        ///     Campaign behaviors receive events from the game (hourly ticks, daily ticks, party encounters, etc.)
        ///     and can modify game state in response. They persist across save/load and are serialized automatically.
        /// </summary>
        /// <param name="game">The game instance being started.</param>
        /// <param name="gameStarterObject">Provides methods to register campaign behaviors and game menus.</param>
        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            try
            {
                ModLogger.Info("Bootstrap", "Game start");
                EnlistedActivation.SetActive(false, "game_start");

                // Log startup diagnostics once per session for troubleshooting
                SessionDiagnostics.LogStartupDiagnostics();

                // Load mod settings from JSON configuration file in ModuleData folder
                // Settings control logging verbosity, encounter suppression, and feature toggles
                _settings = ModSettings.LoadFromModule();
                ModConfig.Settings = _settings;

                // Apply log level configuration from settings
                // This enables per-category verbosity control and message throttling
                _settings.ApplyLogLevels();

                // Log configuration values for verification
                SessionDiagnostics.LogConfigurationValues();

                if (gameStarterObject is CampaignGameStarter campaignStarter)
                {
                    // Initialize event catalog before registering behaviors that might use it
                    EventCatalog.Initialize();

                    // Read Director pacing knobs (event_density, speed_downshift_on_modal) from enlisted_config.json.
                    // Safe to load here: ModulePaths is ready and the Director hasn't emitted anything yet.
                    DensitySettings.Load();

                    // Initialize injury system with varied severity definitions for narrative-driven injuries
                    InjurySystem.Initialize();

                    // Register engine-state predicates used by home-surface storylets. Predicates read
                    // live Bannerlord state at evaluation time; no campaign objects are required at registration.
                    Features.Activities.Home.HomeTriggers.Register();

                    // Save/load diagnostics: two marker behaviors registered first/last so we can log
                    // user-friendly "Saving..." / "Save finished" and "Loading..." / "Load finished" lines.
                    campaignStarter.AddBehavior(new SaveLoadDiagnosticsMarkerBehavior(SaveLoadDiagnosticsMarkerBehavior.Phase.Begin));

                    // Debug hotkeys: polls Ctrl+Shift+H/E/B/T on TickEvent and dispatches to
                    // DebugToolsBehavior smoke helpers for the Home surface. No persisted state.
                    campaignStarter.AddBehavior(new Debugging.Behaviors.DebugHotkeysBehavior());

                    // Flag store: global + hero-scoped named booleans with optional expiry; used by storylet
                    // prereq checks and arc progress markers. Registers before all feature behaviors so flags
                    // are available during their OnSessionLaunched / OnGameLoaded handlers.
                    campaignStarter.AddBehavior(new Features.Flags.FlagBehavior());

                    // Quality store: typed numeric state with read-through proxies over native TaleWorlds APIs.
                    // Registered after FlagBehavior (qualities may read flags during init) and before
                    // StoryDirector (Director will consult qualities when evaluating candidates).
                    campaignStarter.AddBehavior(new Features.Qualities.QualityBehavior());

                    // Activity runtime: singleton host for Banner-Kings-style stateful ticking activities.
                    // Registered after QualityBehavior (activities may read qualities on tick) and before
                    // StoryDirector (ActivityRuntime emits candidates via StoryDirector.EmitCandidate).
                    campaignStarter.AddBehavior(new Features.Activities.ActivityRuntime());

                    // Home surface lifecycle: starts HomeActivity when the enlisted lord's party enters a
                    // friendly fief (town, castle, or village) and emits departure_imminent on leave.
                    // Registered immediately after ActivityRuntime so Start() resolves activity types correctly.
                    campaignStarter.AddBehavior(new Features.Activities.Home.HomeActivityStarter());

                    // Duty-profile sampler: hourly classifies the lord's party and commits profile changes
                    // only after two consecutive matching samples, with a hard-bypass for siege/army/rethink
                    // events. Fires a profile_changed beat on the active OrderActivity on each commit.
                    campaignStarter.AddBehavior(new Features.Activities.Orders.DutyProfileBehavior());

                    // Logs kingdom changes affecting the enlisted lord's clan and, on enlistment end,
                    // captures prior-service flags and tears down OrderActivity via ActivityRuntime.Stop.
                    campaignStarter.AddBehavior(new Features.Activities.Orders.EnlistmentLifecycleListener());

                    // Daily silent skill-XP drift weighted by the active duty profile, with a single
                    // accumulator-summary news-feed emission per hourly tick when the throttle allows.
                    campaignStarter.AddBehavior(new Features.Activities.Orders.DailyDriftApplicator());

                    // Accumulates per-path specialization scores from intent picks and major
                    // skill-XP gains, capped at 100 per path via path_{name}_score qualities.
                    campaignStarter.AddBehavior(new Features.Activities.Orders.PathScorer());

                    // Core enlistment system: tracks which lord the player serves, manages enlistment state,
                    // and handles party following, battle participation, and leave or temporary absence.
                    campaignStarter.AddBehavior(new EnlistmentBehavior());

                    // Incidents: registers enlistment-specific incidents (e.g., deferred bag check)
                    campaignStarter.AddBehavior(new EnlistedIncidentsBehavior());

                    // Muster menu system: multi-stage GameMenu sequence for pay muster, replaces inquiry popup
                    campaignStarter.AddBehavior(new MusterMenuHandler());

                    // Conversation system: adds dialog options to talk with lords about enlistment,
                    // service status, promotions, and requesting leave.
                    campaignStarter.AddBehavior(new EnlistedDialogManager());

                    // Duties system deleted in Phase 1 refactor

                    // Menu system: provides the main enlisted status menu and duty/profession selection interface.
                    // Handles menu state transitions, battle detection, and settlement access.
                    campaignStarter.AddBehavior(new EnlistedMenuBehavior());

                    // Combat log: custom scrollable combat log widget that displays messages on the right side
                    // of the campaign map while enlisted, suppressing the native bottom-left log.
                    campaignStarter.AddBehavior(new EnlistedCombatLogBehavior());

                    // Status manager: determines primary role and specializations based on traits and skills.
                    // Provides formatted status descriptions for UI display.
                    campaignStarter.AddBehavior(new EnlistedStatusManager());

                    // Troop selection: allows players to choose which troop type to represent during service,
                    // determining formation and equipment access.
                    campaignStarter.AddBehavior(new TroopSelectionManager());

                    // Equipment management: handles equipment backups and restoration when leaving service,
                    // ensuring players get their personal gear back when they end their enlistment.
                    campaignStarter.AddBehavior(new EquipmentManager());

                    // Promotion system: checks XP thresholds hourly and promotes players through military ranks.
                    // Triggers formation selection at tier 2 and handles promotion notifications.
                    campaignStarter.AddBehavior(new PromotionBehavior());

                    // Quartermaster system: manages equipment variant selection when players can choose
                    // between different equipment sets at their tier level.
                    campaignStarter.AddBehavior(new QuartermasterManager());

                    // Quartermaster UI: provides the grid-based equipment selection interface where players
                    // can view stats and select variants.
                    campaignStarter.AddBehavior(new QuartermasterEquipmentSelectorBehavior());

                    // Quartermaster Provisions UI: visual grid for food item purchases (Phase 8).
                    campaignStarter.AddBehavior(new QuartermasterProvisionsBehavior());

                    // Foundation for shared trigger vocabulary and minimal recent-history persistence.
                    campaignStarter.AddBehavior(new CampaignTriggerTrackerBehavior());

                    // Lance persona system deleted in Phase 1 refactor

                    // Player condition system, managing injuries, illnesses, and exhaustion. This feature is feature-flagged.
                    campaignStarter.AddBehavior(new PlayerConditionBehavior());

                    // Lance Story system, Lance Life Events, Decision Events, Lance Banner, Lance Menu deleted in Phase 1 refactor

                    // Medical care migrated to decision system (Phase 6G)
                    // Treatment options appear as orchestrated opportunities via dec_medical_* decisions

                    // Battle encounter system: detects when the lord enters battle and handles player participation,
                    // manages menu transitions during battles, and provides battle wait menu options.
                    campaignStarter.AddBehavior(new EnlistedEncounterBehavior());

                    // Service records: tracks faction-specific and lifetime statistics
                    campaignStarter.AddBehavior(new ServiceRecordManager());

                    // Camp UI: provides menus for viewing service records, including current posting,
                    // faction history, and lifetime summaries.
                    campaignStarter.AddBehavior(new CampMenuHandler());

                    // Camp Life Simulation: provides a daily snapshot and Quartermaster/Pay integrations.
                    campaignStarter.AddBehavior(new CampLifeBehavior());

                    // Company Simulation: daily background simulation of soldiers getting sick, deserting,
                    // recovering, equipment degrading, and incidents occurring. Feeds the news system.
                    campaignStarter.AddBehavior(new CompanySimulationBehavior());

                    // Camp Opportunity Generator: generates context-aware camp life opportunities
                    // using 4-layer intelligence (world, camp, player, history) for the living camp experience.
                    campaignStarter.AddBehavior(new CampOpportunityGenerator());

                    // Escalation tracks for scrutiny, discipline, lance reputation, and medical risk. This feature is feature-flagged.
                    campaignStarter.AddBehavior(new EscalationManager());

                    // Event delivery system: queues and delivers narrative events to the player via UI popups.
                    campaignStarter.AddBehavior(new EventDeliveryManager());

                    // Content Orchestrator: central coordinator for all content delivery.
                    // Analyzes world state, calculates appropriate content frequency, and coordinates timing.
                    campaignStarter.AddBehavior(new ContentOrchestrator());

                    // Event pacing system: fires narrative events every 3-5 days based on player role, context, and cooldowns.
                    campaignStarter.AddBehavior(new EventPacingManager());

                    // Map incident system: delivers context-based events during travel (battle end, settlement entry/exit, siege).
                    campaignStarter.AddBehavior(new MapIncidentManager());

                    // Story director: central broker that receives StoryCandidate emissions, filters by relevance,
                    // classifies by severity, and routes to Modal or Observational delivery. Route is a no-op in Phase 1.
                    campaignStarter.AddBehavior(new StoryDirector());

                    // Decision system: loads player-initiated decisions from JSON and provides them to the Decisions menu.
                    campaignStarter.AddBehavior(new DecisionManager());

                    // Orders system: issues orders from chain of command, tracks acceptance/decline, applies consequences.
                    campaignStarter.AddBehavior(new OrderManager());

                    // Order progression: handles multi-day order execution with phase-based event injection.
                    // Events fire during slot phases based on world state and activity level.
                    campaignStarter.AddBehavior(new OrderProgressionBehavior());

                    // Baggage train: manages access to player's personal stowage based on march state and logistics.
                    campaignStarter.AddBehavior(new BaggageTrainManager());

                    // News/Dispatches: generates kingdom-wide and personal news headlines.
                    // Read-only observer of campaign events; updates every 2 in-game days.
                    campaignStarter.AddBehavior(new EnlistedNewsBehavior());

                    // Main Menu cache: caches KINGDOM, CAMP, YOU sections for stable display.
                    // Refreshes based on time intervals and state changes.
                    campaignStarter.AddBehavior(new MainMenuNewsCache());

                    // Retinue trickle system: adds free soldiers over time (every 2-3 days)
                    campaignStarter.AddBehavior(new RetinueTrickleSystem());

                    // Retinue lifecycle handler: clears retinue on capture, discharge, lord death, army defeat
                    campaignStarter.AddBehavior(new RetinueLifecycleHandler());

                    // Retinue casualty tracker: reconciles troop counts after battles
                    campaignStarter.AddBehavior(new RetinueCasualtyTracker());

                    // Companion assignment manager: tracks which companions should fight vs stay back
                    // Companions marked "stay back" don't spawn in battle, keeping them safe
                    campaignStarter.AddBehavior(new CompanionAssignmentManager());

                    // Schedule, Lance Simulation, Persistent Leaders systems deleted in Phase 1 refactor

                    // Save/load diagnostics end marker: registered last so it runs after all other behaviors
                    // during save/load serialization passes.
                    campaignStarter.AddBehavior(new SaveLoadDiagnosticsMarkerBehavior(SaveLoadDiagnosticsMarkerBehavior.Phase.End));

                    // Encounter guard: utility system for managing player party attachment and encounter transitions
                    // Initializes static helper methods used throughout the enlistment system
                    EncounterGuard.Initialize();
                    ModLogger.Info("Bootstrap", "Military service behaviors registered successfully");

                    // Log registered behaviors for conflict diagnostics — enumerate the starter's
                    // collection directly, before the engine transfers it to CampaignBehaviorManager.
                    ModConflictDiagnostics.LogRegisteredBehaviors(campaignStarter.CampaignBehaviors);
                }
            }
            catch (Exception ex)
            {
                // If behavior registration fails, log the error but allow the game to continue
                // Some behaviors may still be registered if the failure occurs partway through
                ModLogger.Caught("Bootstrap", "Exception during OnGameStart", ex);
            }
        }

        /// <summary>
        ///     Called when the mod module is being unloaded, typically on clean game exit.
        ///     Flushes the session summary footer so final Surfaced/Caught/Expected counts
        ///     are persisted at the tail of the session log. Wrapped defensively —
        ///     logging failures must never prevent a clean shutdown.
        /// </summary>
        protected override void OnSubModuleUnloaded()
        {
            try
            {
                SessionSummaryFooter.Flush();
            }
            catch
            {
                // Never let a logging flush failure break game shutdown.
            }
            base.OnSubModuleUnloaded();
        }

        /// <summary>
        ///     Called when a mission (battle, siege, etc.) initializes its behaviors.
        ///     This is the proper place to add mission-specific behaviors like kill tracking.
        ///     Unlike dynamic addition during AfterMissionStarted, this runs before mission lifecycle begins.
        /// </summary>
        /// <param name="mission">The mission being initialized.</param>
        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            try
            {
                base.OnMissionBehaviorInitialize(mission);

                // DIAGNOSTIC: Log that we entered the method
                ModLogger.Info("Mission", $"OnMissionBehaviorInitialize called (Mode: {mission.Mode})");

                var enlistment = EnlistmentBehavior.Instance;

                // DIAGNOSTIC: Log enlistment state before checking
                ModLogger.Info("Mission",
                    $"Enlistment check - Instance: {enlistment != null}, IsEnlisted: {enlistment?.IsEnlisted}, OnLeave: {enlistment?.IsOnLeave}");

                if (enlistment?.IsEnlisted != true)
                {
                    ModLogger.Info("Mission", "Skipping behavior add - not enlisted");
                    return;
                }

                // Check if this is a combat mission (field battle, siege, hideout, etc.)
                // Skip non-combat missions like conversations, tournaments, arenas
                // NOTE: Siege battles start in StartUp mode and transition to Battle mode later,
                // so we must include StartUp to catch them at initialization time.
                if (mission.Mode == MissionMode.Battle ||
                    mission.Mode == MissionMode.StartUp ||
                    mission.Mode == MissionMode.Stealth ||
                    mission.Mode == MissionMode.Deployment)
                {
                    mission.AddMissionBehavior(new EnlistedKillTrackerBehavior());
                    mission.AddMissionBehavior(new EnlistedFormationAssignmentBehavior());
                    ModLogger.Info("Mission", $"Enlisted behaviors added to mission (Mode: {mission.Mode})");
                }
                else
                {
                    ModLogger.Info("Mission", $"Skipped behaviors - mode {mission.Mode} not a combat mission");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Bootstrap", "Error in OnMissionBehaviorInitialize", ex);
            }
        }
    }

    /// <summary>
    ///     Harmony patch that hooks into Campaign.Tick() to process deferred actions on the next frame.
    ///     Campaign.Tick() runs every frame of the campaign map and handles all campaign-level updates.
    ///     By executing in Postfix, we run after the native tick completes, ensuring game state is stable.
    /// </summary>
    [HarmonyPatch(typeof(Campaign), "Tick")]
    public static class NextFrameDispatcherPatch
    {
        private static bool _deferredPatchesApplied;

        /// <summary>
        ///     Called after Campaign.Tick() completes each frame.
        ///     Processes any actions that were deferred to avoid timing conflicts during game state transitions.
        ///     Also applies deferred patches on first tick (after character creation is complete).
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051",
            Justification = "Called by Harmony via reflection")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedMember.Local",
            Justification = "Called by Harmony via [HarmonyPatch] postfix")]
        private static void Postfix()
        {
            // Apply deferred patches only after the campaign is fully ready.
            // On some platforms (notably Proton/Linux), Campaign.Tick can run during character creation,
            // and touching certain menu/localization-heavy types at that time can crash the game.
            if (!_deferredPatchesApplied && CampaignSafetyGuard.IsCampaignReady)
            {
                try
                {
                    _deferredPatchesApplied = true;
                    var harmony = new Harmony("com.enlisted.mod.deferred");

                    // Apply manual patches.
                    HidePartyNamePlatePatch.ApplyManualPatches(harmony);

                    // Apply patches that target types with early static initialization.
                    // These fail on Proton/Linux if applied during OnSubModuleLoad because
                    // their target classes call GameTexts.FindText() in static field initializers.
                    // By deferring until the campaign is ready, the localization system is fully initialized.
                    // Each patch is wrapped individually so one failure doesn't block others.
                    ApplyDeferredPatch(harmony, typeof(ArmyCohesionExclusionPatch));
                    ApplyDeferredPatch(harmony, typeof(CheckFortificationAttackablePatch));
                    ApplyDeferredPatch(harmony, typeof(SettlementOutsideLeaveButtonPatch));
                    ApplyDeferredPatch(harmony, typeof(JoinEncounterAutoSelectPatch));
                    ApplyDeferredPatch(harmony, typeof(EncounterAbandonArmyBlockPatch));
                    ApplyDeferredPatch(harmony, typeof(EncounterAbandonArmyBlockPatch2));
                    ApplyDeferredPatch(harmony, typeof(EncounterLeaveSuppressionPatch));
                    ApplyDeferredPatch(harmony, typeof(EncounterCaptureEnemySuppressionPatch));
                    // EncounterVictoryAutoClosePatch REMOVED - it was intercepting ALL menu activations
                    // including native siege menus. GenericStateMenuPatch already handles battle-over
                    // encounter suppression, and CleanupPostEncounterState clears stale state.
                    ApplyDeferredPatch(harmony, typeof(CombatLogConversationPatch.MapConversationEndPatch));

                    // Apply Naval DLC patches that use reflection to find types.
                    // These must be deferred because Naval DLC types aren't available during OnSubModuleLoad.
                    RaftStateSuppressionPatch.TryApplyPatch(harmony);
                    RaftStateSuppressionPatch.TryApplyOnPartyLeftArmyPatch(harmony);
                    NavalMobilePartyVisualUpdateEntityPositionCrashGuardPatch.TryApplyPatch(harmony);

                    ModLogger.Info("Bootstrap", "Deferred patches applied (campaign ready)");

                    // Update conflict diagnostics with deferred patch info
                    // This appends to the existing conflict log so users can see all patches.
                    ModConflictDiagnostics.RefreshDeferredPatches(harmony);
                }
                catch (Exception ex)
                {
                    // Hard safety: never allow a failure during deferred patch application to crash the game.
                    // If something goes wrong here, we'll run without deferred patches.
                    ModLogger.Caught("Bootstrap", "Unexpected error applying deferred patches", ex);
                }
            }

            NextFrameDispatcher.ProcessNextFrame();
        }

        /// <summary>
        /// Applies a deferred patch with detailed error logging.
        /// Isolates failures so one broken patch doesn't block others.
        /// </summary>
        private static void ApplyDeferredPatch(Harmony harmony, Type patchType)
        {
            try
            {
                _ = harmony.CreateClassProcessor(patchType).Patch();
                ModLogger.Info("Bootstrap", $"Applied deferred patch: {patchType.Name}");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Bootstrap", $"Failed to apply deferred patch {patchType.Name}", ex);
                if (ex.InnerException != null)
                {
                    ModLogger.Caught("Bootstrap", "Inner exception during deferred patch", ex.InnerException);
                }
            }
        }
    }
}
