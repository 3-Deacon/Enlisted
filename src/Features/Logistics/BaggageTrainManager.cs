using System;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core;
using Enlisted.Mod.Core.Config;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Enlisted.Features.Logistics
{
    /// <summary>
    /// Manages baggage train access based on march state, rank, location, and supply conditions.
    /// Controls when players can access their personal stowage and handles emergency access requests.
    /// Coordinates with QuartermasterManager for dialogue-based access gating.
    /// The baggage train marches separately from the fighting column, requiring players to plan
    /// ahead and creating natural preparation pressure before leaving settlements.
    /// </summary>
    public class BaggageTrainManager : CampaignBehaviorBase
    {
        private const string LogCategory = "Baggage";

        public static BaggageTrainManager Instance { get; private set; }

        // Core state - persisted in save
        private BaggageAccessState _currentState;
        private CampaignTime _temporaryAccessExpires;
        private CampaignTime _baggageDelayedUntil;
        private CampaignTime _lastEmergencyRequest;
        private int _emergencyRequestsToday;
        private CampaignTime _lastNcoDailyAccess;
        private int _lastRaidDay;
        private int _lastArrivalDay;

        // Configuration cache
        private BaggageConfig _config;

        public BaggageTrainManager()
        {
            Instance = this;
            LoadConfiguration();
        }

        public override void RegisterEvents()
        {
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Persist all state fields for save/load
            _ = dataStore.SyncData("_baggageCurrentState", ref _currentState);
            _ = dataStore.SyncData("_baggageTemporaryAccessExpires", ref _temporaryAccessExpires);
            _ = dataStore.SyncData("_baggageDelayedUntil", ref _baggageDelayedUntil);
            _ = dataStore.SyncData("_baggageLastEmergencyRequest", ref _lastEmergencyRequest);
            _ = dataStore.SyncData("_baggageEmergencyRequestsToday", ref _emergencyRequestsToday);
            _ = dataStore.SyncData("_baggageLastNcoDailyAccess", ref _lastNcoDailyAccess);
            _ = dataStore.SyncData("_baggageLastRaidDay", ref _lastRaidDay);
            _ = dataStore.SyncData("_baggageLastArrivalDay", ref _lastArrivalDay);
        }

        /// <summary>
        /// Loads configuration from baggage_config.json.
        /// </summary>
        private void LoadConfiguration()
        {
            try
            {
                var moduleDataPath = ConfigurationManager.GetModuleDataPathForConsumers();
                var configPath = System.IO.Path.Combine(moduleDataPath, "Config", "baggage_config.json");

                if (!System.IO.File.Exists(configPath))
                {
                    ModLogger.Warn(LogCategory, $"baggage_config.json not found at {configPath}, using defaults");
                    _config = new BaggageConfig();
                    return;
                }

                var json = System.IO.File.ReadAllText(configPath);
                _config = Newtonsoft.Json.JsonConvert.DeserializeObject<BaggageConfig>(json,
                    new Newtonsoft.Json.JsonSerializerSettings
                    {
                        ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver
                        {
                            NamingStrategy = new Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy()
                        }
                    });

                if (_config == null)
                {
                    ModLogger.Warn(LogCategory, "Failed to deserialize baggage_config.json, using defaults");
                    _config = new BaggageConfig();
                }
                else
                {
                    ModLogger.Info(LogCategory, "Baggage configuration loaded successfully");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Baggage", "Error loading baggage configuration", ex);
                _config = new BaggageConfig();
            }
        }

        /// <summary>
        /// Returns the current baggage access state, evaluating all conditions
        /// (location, activity, supply level, delays, temporary windows).
        /// Priority order: Captivity > Combat/Reserve > Locked > Leave > Grace > Delay > Siege > Settlement > Temporary > March.
        /// </summary>
        public BaggageAccessState GetCurrentAccess()
        {
            var hero = CampaignSafetyGuard.SafeMainHero;
            if (hero == null)
            {
                ModLogger.Debug(LogCategory, "GetCurrentAccess: Hero null, returning NoAccess");
                return BaggageAccessState.NoAccess;
            }

            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null || !enlistment.IsEnlisted)
            {
                ModLogger.Debug(LogCategory, "GetCurrentAccess: Not enlisted, returning FullAccess");
                return BaggageAccessState.FullAccess; // Not enlisted = full access to own baggage
            }

            var party = MobileParty.MainParty;
            if (party == null)
            {
                ModLogger.Debug(LogCategory, "GetCurrentAccess: MainParty null, returning NoAccess");
                return BaggageAccessState.NoAccess;
            }

            // Priority 1: Captivity - prisoner state blocks all access
            if (hero.IsPrisoner)
            {
                ModLogger.Debug(LogCategory, "GetCurrentAccess: Prisoner, returning NoAccess");
                return BaggageAccessState.NoAccess;
            }

            // Priority 2: Combat/Battle Reserve - no access during active encounters or reserve mode
            if (party.MapEvent != null)
            {
                ModLogger.Debug(LogCategory, "GetCurrentAccess: In battle, returning NoAccess");
                return BaggageAccessState.NoAccess;
            }

            // Check if waiting in reserve during battle
            if (Combat.Behaviors.EnlistedEncounterBehavior.IsWaitingInReserve)
            {
                ModLogger.Debug(LogCategory, "GetCurrentAccess: Waiting in battle reserve, returning NoAccess");
                return BaggageAccessState.NoAccess;
            }

            // Priority 3: Locked - supply crisis overrides other states
            if (ShouldLockBaggage())
            {
                ModLogger.Debug(LogCategory, "GetCurrentAccess: Locked due to supply crisis");
                return BaggageAccessState.Locked;
            }

            // Priority 4: Leave System - players on leave have full access to baggage
            if (enlistment.IsOnLeave)
            {
                ModLogger.Debug(LogCategory, "GetCurrentAccess: On leave, returning FullAccess");
                return BaggageAccessState.FullAccess;
            }

            // Priority 5: Grace Period - players in desertion grace period have full access
            if (enlistment.IsInDesertionGracePeriod)
            {
                ModLogger.Debug(LogCategory, "GetCurrentAccess: In grace period, returning FullAccess");
                return BaggageAccessState.FullAccess;
            }

            // Priority 6: Active Delay - baggage train is stuck/delayed, blocks access
            // Note: Delay countdown is frozen during captivity (handled in OnHourlyTick)
            if (_baggageDelayedUntil > CampaignTime.Now)
            {
                var daysRemaining = (_baggageDelayedUntil.ToDays - CampaignTime.Now.ToDays);
                ModLogger.Debug(LogCategory, $"GetCurrentAccess: Delayed for {daysRemaining:F1} more days");
                return BaggageAccessState.NoAccess;
            }

            // Priority 7: Siege Context - check if in siege as attacker or defender
            if (party.SiegeEvent != null)
            {
                // If besieging (attacker), baggage access is blocked
                if (party.BesiegerCamp != null)
                {
                    ModLogger.Debug(LogCategory, "GetCurrentAccess: Besieging (attacker), returning NoAccess");
                    return BaggageAccessState.NoAccess;
                }

                // If besieged (defender), full access (inside settlement)
                // This is already handled by settlement check below, but explicit for clarity
                ModLogger.Debug(LogCategory, "GetCurrentAccess: Besieged (defender), returning FullAccess");
                return BaggageAccessState.FullAccess;
            }

            // Priority 7.5: Muster Window - grants full access during muster and for 6 hours after
            // Active muster takes precedence over march state to allow baggage access during pay muster
            if (enlistment.PayMusterPending)
            {
                ModLogger.Debug(LogCategory, "GetCurrentAccess: Muster active, returning FullAccess");
                return BaggageAccessState.FullAccess;
            }

            // Check post-muster window (6 hours after muster completes)
            if (enlistment.LastMusterCompletionTime > CampaignTime.Zero)
            {
                var hoursSinceMuster = CampaignTime.Now.ToHours - enlistment.LastMusterCompletionTime.ToHours;
                if (hoursSinceMuster >= 0 && hoursSinceMuster < 6.0f)
                {
                    ModLogger.Debug(LogCategory, $"GetCurrentAccess: Post-muster window active ({6.0f - hoursSinceMuster:F1}h remaining)");
                    return BaggageAccessState.FullAccess;
                }
            }

            // Priority 8: Settlement - always has access in settlements
            if (party.CurrentSettlement != null)
            {
                ModLogger.Debug(LogCategory, "GetCurrentAccess: In settlement, returning FullAccess");
                return BaggageAccessState.FullAccess;
            }

            // Priority 9: Temporary Access Window - brief window when wagons catch up
            if (_temporaryAccessExpires > CampaignTime.Now)
            {
                ModLogger.Debug(LogCategory, "GetCurrentAccess: Temporary access window active");
                return BaggageAccessState.TemporaryAccess;
            }

            // Priority 10: March State - on the march, baggage is behind the column
            if (party.IsMoving)
            {
                ModLogger.Debug(LogCategory, "GetCurrentAccess: On march, returning NoAccess");
                return BaggageAccessState.NoAccess;
            }

            // Default: Army halted or resting - full access
            ModLogger.Debug(LogCategory, "GetCurrentAccess: Halted/resting, returning FullAccess");
            return BaggageAccessState.FullAccess;
        }

        /// <summary>
        /// Checks if baggage should be locked due to supply crisis.
        /// </summary>
        private bool ShouldLockBaggage()
        {
            // Check for supply crisis
            var supplyManager = CompanySupplyManager.Instance;
            if (supplyManager != null)
            {
                var supplyThreshold = _config?.Lockdown?.SupplyThresholdPercent ?? 20;
                if (supplyManager.TotalSupply < supplyThreshold)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if emergency access is currently on cooldown.
        /// Returns true if cooldown is active (request would fail).
        /// </summary>
        public bool IsEmergencyAccessOnCooldown()
        {
            if (_lastEmergencyRequest == CampaignTime.Zero)
            {
                return false;
            }

            var cooldownHours = _config?.EmergencyAccess?.CooldownHours ?? 12;
            var hoursSinceLastRequest = (CampaignTime.Now.ToHours - _lastEmergencyRequest.ToHours);
            return hoursSinceLastRequest < cooldownHours;
        }

        /// <summary>
        /// Attempts to grant emergency baggage access via quartermaster request.
        /// Checks cooldowns, rank requirements, and applies reputation costs.
        /// Returns false if request denied, with failReason explaining why.
        /// </summary>
        public bool TryRequestEmergencyAccess(out string failReason)
        {
            var hero = CampaignSafetyGuard.SafeMainHero;
            if (hero == null)
            {
                failReason = "Invalid state";
                ModLogger.Caught("Baggage", "TryRequestEmergencyAccess: Hero null", null);
                return false;
            }

            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null || !enlistment.IsEnlisted)
            {
                failReason = "Not enlisted";
                ModLogger.Debug(LogCategory, "TryRequestEmergencyAccess: Not enlisted");
                return false;
            }

            var tier = enlistment.EnlistmentTier;
            var minTier = _config?.RankGates?.EmergencyRequestMinTier ?? 3;

            // Check tier requirement (T1-T2 cannot request)
            if (tier < minTier)
            {
                failReason = "Rank too low";
                ModLogger.Info(LogCategory, $"Emergency access denied: Tier {tier} < required {minTier}");
                return false;
            }

            // Check cooldown
            var cooldownHours = _config?.EmergencyAccess?.CooldownHours ?? 12;
            if (_lastEmergencyRequest != CampaignTime.Zero)
            {
                var hoursSinceLastRequest = (CampaignTime.Now.ToHours - _lastEmergencyRequest.ToHours);
                if (hoursSinceLastRequest < cooldownHours)
                {
                    failReason = "Cooldown active";
                    ModLogger.Info(LogCategory, $"Emergency access denied: Cooldown active ({hoursSinceLastRequest:F1}h / {cooldownHours}h)");
                    return false;
                }
            }

            // Determine reputation cost based on tier
            int repCost = GetEmergencyAccessRepCost(tier);

            // Grant temporary access
            var accessHours = _config?.AccessWindows?.TemporaryAccessHours ?? 4;
            GrantTemporaryAccess(accessHours);

            // Apply reputation cost (handled by dialogue system via action handler)
            // This method just grants the access and tracks cooldowns
            _lastEmergencyRequest = CampaignTime.Now;
            _emergencyRequestsToday++;

            failReason = null;
            ModLogger.Info(LogCategory, $"Emergency access granted: {accessHours}h window (cost: {repCost} QM rep, tier: {tier})");
            return true;
        }

        /// <summary>
        /// Calculates reputation cost for emergency access based on player tier.
        /// </summary>
        private int GetEmergencyAccessRepCost(int tier)
        {
            if (_config?.EmergencyAccess == null)
            {
                // Default costs
                return tier >= 7 ? 0 : tier >= 5 ? 2 : 5;
            }

            if (tier >= 7)
            {
                return _config.EmergencyAccess.OfficerQmRepCost;
            }

            if (tier >= 5)
            {
                return _config.EmergencyAccess.NcoQmRepCost;
            }

            return _config.EmergencyAccess.BaseQmRepCost;
        }

        /// <summary>
        /// Grants temporary baggage access for specified hours.
        /// Typically triggered by events (baggage caught up, night halt, emergency request).
        /// </summary>
        public void GrantTemporaryAccess(int hours)
        {
            if (hours <= 0)
            {
                ModLogger.Warn(LogCategory, $"Invalid hours for temporary access: {hours}");
                return;
            }

            _temporaryAccessExpires = CampaignTime.HoursFromNow(hours);
            _currentState = BaggageAccessState.TemporaryAccess;
            ModLogger.Info(LogCategory, $"Temporary access granted: {hours}h (expires at {_temporaryAccessExpires})");
        }

        /// <summary>
        /// Applies a baggage delay, preventing access until the delay clears.
        /// Used by events (bad weather, rough terrain, raids).
        /// </summary>
        public void ApplyBaggageDelay(int days)
        {
            if (days <= 0)
            {
                ModLogger.Warn(LogCategory, $"Invalid days for baggage delay: {days}");
                return;
            }

            _baggageDelayedUntil = CampaignTime.DaysFromNow(days);
            ModLogger.Info(LogCategory, $"Baggage delayed: {days} days (until {_baggageDelayedUntil})");
        }

        /// <summary>
        /// Clears any active baggage delay, restoring normal access state.
        /// Used when events or actions resolve delays early.
        /// </summary>
        public void ClearBaggageDelay()
        {
            if (_baggageDelayedUntil > CampaignTime.Zero)
            {
                ModLogger.Info(LogCategory, "Baggage delay cleared");
                _baggageDelayedUntil = CampaignTime.Zero;
            }
        }

        /// <summary>
        /// Returns the number of days remaining on the current baggage delay, or 0 if no delay is active.
        /// Used for UI display and event condition checks.
        /// </summary>
        public int GetBaggageDelayDaysRemaining()
        {
            if (_baggageDelayedUntil <= CampaignTime.Now)
            {
                return 0;
            }

            return (int)Math.Ceiling(_baggageDelayedUntil.ToDays - CampaignTime.Now.ToDays);
        }

        /// <summary>
        /// Returns the number of hours remaining on the current temporary access window, or 0 if no window is active.
        /// Used for UI display in Daily Brief.
        /// </summary>
        public int GetTemporaryAccessHoursRemaining()
        {
            if (_temporaryAccessExpires <= CampaignTime.Now)
            {
                return 0;
            }

            return (int)Math.Ceiling(_temporaryAccessExpires.ToHours - CampaignTime.Now.ToHours);
        }

        /// <summary>
        /// Returns the number of days since the baggage train was last raided.
        /// Returns -1 if baggage has never been raided.
        /// </summary>
        public int GetDaysSinceLastRaid()
        {
            if (_lastRaidDay <= 0)
            {
                return -1;
            }

            var currentDay = (int)CampaignTime.Now.ToDays;
            return currentDay - _lastRaidDay;
        }

        /// <summary>
        /// Returns the number of days since the baggage train last caught up to the column.
        /// Returns -1 if baggage has never caught up (or arrival tracking not initialized).
        /// </summary>
        public int GetDaysSinceLastArrival()
        {
            if (_lastArrivalDay <= 0)
            {
                return -1;
            }

            var currentDay = (int)CampaignTime.Now.ToDays;
            return currentDay - _lastArrivalDay;
        }

        /// <summary>
        /// Returns true if baggage is currently delayed (behind schedule).
        /// </summary>
        public bool IsDelayed()
        {
            return _baggageDelayedUntil > CampaignTime.Now;
        }

        /// <summary>
        /// Records that a baggage raid occurred, updating internal tracking for Daily Brief display.
        /// Called by baggage raid events or external systems.
        /// </summary>
        public void RecordBaggageRaid()
        {
            _lastRaidDay = (int)CampaignTime.Now.ToDays;
            ModLogger.Info(LogCategory, $"Baggage raid recorded on day {_lastRaidDay}");
        }

        /// <summary>
        /// Records that baggage caught up to the column, updating internal tracking for Daily Brief display.
        /// Called by baggage arrival events.
        /// </summary>
        public void RecordBaggageArrival()
        {
            _lastArrivalDay = (int)CampaignTime.Now.ToDays;
            ModLogger.Info(LogCategory, $"Baggage arrival recorded on day {_lastArrivalDay}");
        }

        /// <summary>
        /// Hourly tick checks for state transitions (temporary access expiring, delays clearing).
        /// Delay countdown is frozen while player is captured (delay resumes on release).
        /// </summary>
        private void OnHourlyTick()
        {
            if (!CampaignSafetyGuard.IsCampaignReady)
            {
                return;
            }

            var hero = CampaignSafetyGuard.SafeMainHero;
            if (hero == null)
            {
                return;
            }

            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null || !enlistment.IsEnlisted)
            {
                return;
            }

            // Freeze delay countdown if player is captured
            // Baggage delay timer doesn't progress during captivity
            if (hero.IsPrisoner)
            {
                ModLogger.Debug(LogCategory, "OnHourlyTick: Player captured, freezing baggage delay countdown");
                return;
            }

            // Check if temporary access expired
            if (_currentState == BaggageAccessState.TemporaryAccess &&
                _temporaryAccessExpires <= CampaignTime.Now)
            {
                ModLogger.Info(LogCategory, "Temporary access window expired");
                _temporaryAccessExpires = CampaignTime.Zero;
                _currentState = BaggageAccessState.NoAccess;
            }

            // Check if delay cleared (only progresses when not captured)
            if (_baggageDelayedUntil > CampaignTime.Zero && _baggageDelayedUntil <= CampaignTime.Now)
            {
                ModLogger.Info(LogCategory, "Baggage delay cleared");
                _baggageDelayedUntil = CampaignTime.Zero;
            }
        }

        /// <summary>
        /// Daily tick resets cooldowns and triggers baggage-related events when conditions are met.
        /// Events include: baggage caught up (positive), baggage delayed (weather/terrain), theft (low rep).
        /// For T5+ NCOs on march, automatically grants a daily access window (separate from random events).
        /// </summary>
        private void OnDailyTick()
        {
            if (!CampaignSafetyGuard.IsCampaignReady)
            {
                return;
            }

            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null || !enlistment.IsEnlisted)
            {
                return;
            }

            // Reset daily counters
            _emergencyRequestsToday = 0;

            // Only trigger march-related events when on the march (NoAccess state)
            var currentAccess = GetCurrentAccess();
            if (currentAccess == BaggageAccessState.NoAccess)
            {
                // T5+ NCOs get automatic daily access window while on march
                TryGrantNcoDailyAccess(enlistment);

                // Separate from NCO access, random events can still trigger
                TryTriggerBaggageEvent();
            }

            ModLogger.Debug(LogCategory, $"Daily tick: Counters reset, access state: {currentAccess}");
        }

        /// <summary>
        /// Grants automatic daily access window for T5+ NCOs while on march.
        /// Separate from emergency requests and random events. Grants 2-4 hour window once per day.
        /// </summary>
        private void TryGrantNcoDailyAccess(EnlistmentBehavior enlistment)
        {
            if (enlistment == null)
            {
                return;
            }

            var tier = enlistment.EnlistmentTier;
            var minTier = _config?.RankGates?.DailyAccessWindowMinTier ?? 5;

            // Check tier requirement (T5+)
            if (tier < minTier)
            {
                return;
            }

            // Check if already granted today (use day boundary to prevent multiple grants)
            var currentDay = (int)CampaignTime.Now.ToDays;
            var lastGrantDay = (int)_lastNcoDailyAccess.ToDays;

            if (currentDay == lastGrantDay)
            {
                ModLogger.Debug(LogCategory, $"NCO daily access already granted today (day {currentDay})");
                return;
            }

            // Check if player is actually on march (party moving and no access)
            var party = MobileParty.MainParty;
            if (party == null || !party.IsMoving)
            {
                return;
            }

            // Grant 2-4 hour window (slightly less than temporary event access to differentiate)
            var random = new Random();
            var accessHours = random.Next(2, 5); // 2-4 hours inclusive
            GrantTemporaryAccess(accessHours);

            _lastNcoDailyAccess = CampaignTime.Now;

            ModLogger.Info(LogCategory, $"NCO daily access granted: {accessHours}h window (tier {tier}, automatic privilege)");
        }

        /// <summary>
        /// Attempts to update baggage status during march.
        /// Updates state directly - no popup events. Players see status in Daily Brief flavor text.
        /// Now uses world-state-aware probabilities from ContentOrchestrator.
        /// </summary>
        private void TryTriggerBaggageEvent()
        {
            // Get world-state-aware probabilities
            var worldState = Content.ContentOrchestrator.Instance?.GetCurrentWorldSituation();
            var probs = worldState != null
                ? CalculateEventProbabilities(worldState)
                : GetDefaultProbabilities();

            var random = new Random();
            var roll = random.Next(100);

            // Check for positive "baggage caught up" event
            if (roll < probs.CaughtUpChance)
            {
                // Grant temporary access - wagons caught up
                GrantTemporaryAccess(4); // 4 hours of access
                _lastArrivalDay = (int)CampaignTime.Now.ToDays;
                ModLogger.Info(LogCategory, $"Baggage wagons caught up (probability: {probs.CaughtUpChance}%)");
                return;
            }

            // Check for delay event (context-aware)
            if (roll < probs.CaughtUpChance + probs.DelayChance)
            {
                // Apply delay - wagons stuck or delayed
                var delayDays = random.Next(1, 3); // 1-2 days delay
                ApplyBaggageDelay(delayDays);
                ModLogger.Info(LogCategory, $"Baggage delayed {delayDays} days (probability: {probs.DelayChance}%)");
                return;
            }

            // Check for raid event (context-aware)
            if (roll < probs.CaughtUpChance + probs.DelayChance + probs.RaidChance)
            {
                // Apply raid - mark raid day and optionally add delay
                _lastRaidDay = (int)CampaignTime.Now.ToDays;
                ApplyBaggageDelay(1); // Raiders cause a 1-day delay
                ModLogger.Info(LogCategory, $"Baggage train raided (probability: {probs.RaidChance}%)");
            }

            // Theft now happens passively (items may go missing over time)
            // No popup events - the player discovers losses when accessing baggage
        }

        /// <summary>
        /// Calculates contextual baggage event probabilities based on world state.
        /// Makes baggage simulation responsive to campaign situation (siege, retreat, peace, etc.).
        /// </summary>
        public BaggageEventProbabilities CalculateEventProbabilities(Content.Models.WorldSituation worldState)
        {
            // Start with config-based defaults
            var baseCaughtUp = _config?.Timing?.CaughtUpChancePercent ?? 25;
            var baseDelay = _config?.Events?.DelayEventChanceBadWeather ?? 15;
            var baseRaid = _config?.Events?.RaidEventChanceEnemyTerritory ?? 8;

            var probs = new BaggageEventProbabilities
            {
                CaughtUpChance = baseCaughtUp,
                DelayChance = baseDelay,
                RaidChance = baseRaid
            };

            var lordParty = EnlistmentBehavior.Instance?.CurrentLord?.PartyBelongedTo;
            if (lordParty == null)
            {
                return probs;
            }

            // ACTIVITY LEVEL: Affects wagon mobility and security
            switch (worldState.ExpectedActivity)
            {
                case Content.Models.ActivityLevel.Intense:
                    // Desperate retreat/siege - wagons fall behind
                    probs.CaughtUpChance = 10;   // Rarely catch up
                    probs.DelayChance = 35;      // Frequently delayed
                    probs.RaidChance = 20;       // High vulnerability
                    break;

                case Content.Models.ActivityLevel.Active:
                    // Active campaign - moderate pressure
                    probs.CaughtUpChance = 20;
                    probs.DelayChance = 20;
                    probs.RaidChance = 12;
                    break;

                case Content.Models.ActivityLevel.Routine:
                    // Normal march - use config base values (already set above)
                    break;

                case Content.Models.ActivityLevel.Quiet:
                    // Garrison/peacetime - wagons keep up easily
                    probs.CaughtUpChance = 40;   // Frequently catch up
                    probs.DelayChance = 5;       // Rarely delayed
                    probs.RaidChance = 2;        // Safe
                    break;
            }

            // LORD SITUATION: Specific tactical context
            switch (worldState.LordIs)
            {
                case Content.Models.LordSituation.Defeated:
                    // Routed - baggage scattered
                    probs.DelayChance += 20;
                    probs.RaidChance += 15;
                    break;

                case Content.Models.LordSituation.SiegeAttacking:
                case Content.Models.LordSituation.SiegeDefending:
                    // Siege - wagons stationary but vulnerable
                    probs.CaughtUpChance += 10;  // Easier to access
                    probs.RaidChance += 5;       // Target for raids
                    break;

                case Content.Models.LordSituation.PeacetimeGarrison:
                    // Safe - wagons well-managed
                    probs.DelayChance = (int)(probs.DelayChance * 0.5f);
                    probs.RaidChance = (int)(probs.RaidChance * 0.3f);
                    break;
            }

            // WAR STANCE: Strategic pressure affects raid risk
            switch (worldState.KingdomStance)
            {
                case Content.Models.WarStance.Desperate:
                    // Losing badly - enemy raids frequent
                    probs.RaidChance += 12;
                    break;

                case Content.Models.WarStance.Defensive:
                    // Under pressure - increased raids
                    probs.RaidChance += 6;
                    break;

                case Content.Models.WarStance.Offensive:
                    // Attacking - wagons in friendly territory
                    probs.RaidChance -= 3;
                    break;

                case Content.Models.WarStance.Peace:
                    // Peacetime - minimal raids
                    probs.RaidChance = 1;
                    break;
            }

            // TERRAIN: Physical environment affects wagon mobility
            try
            {
                if (lordParty.CurrentNavigationFace.IsValid() && Campaign.Current?.MapSceneWrapper != null)
                {
                    var terrain = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(lordParty.CurrentNavigationFace);
                    switch (terrain)
                    {
                        case TerrainType.Mountain:
                            probs.DelayChance += 10;  // Rough terrain
                            break;
                        case TerrainType.Snow:
                            probs.DelayChance += 15;  // Snow slows wagons
                            break;
                        case TerrainType.Desert:
                            probs.DelayChance += 8;   // Sand is difficult
                            break;
                        case TerrainType.Fording:
                        case TerrainType.Water:
                            probs.DelayChance += 12;  // River crossings
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Debug(LogCategory, $"Error checking terrain for baggage: {ex.Message}");
            }

            // Clamp all values to reasonable ranges
            probs.CaughtUpChance = Math.Max(5, Math.Min(60, probs.CaughtUpChance));
            probs.DelayChance = Math.Max(2, Math.Min(50, probs.DelayChance));
            probs.RaidChance = Math.Max(0, Math.Min(35, probs.RaidChance));

            ModLogger.Debug(LogCategory,
                $"Baggage probabilities: CatchUp={probs.CaughtUpChance}%, Delay={probs.DelayChance}%, " +
                $"Raid={probs.RaidChance}% (Activity={worldState.ExpectedActivity}, " +
                $"Lord={worldState.LordIs}, War={worldState.KingdomStance})");

            return probs;
        }

        /// <summary>
        /// Returns default probabilities when orchestrator is unavailable.
        /// </summary>
        private BaggageEventProbabilities GetDefaultProbabilities()
        {
            return new BaggageEventProbabilities
            {
                CaughtUpChance = _config?.Timing?.CaughtUpChancePercent ?? 25,
                DelayChance = _config?.Events?.DelayEventChanceBadWeather ?? 15,
                RaidChance = _config?.Events?.RaidEventChanceEnemyTerritory ?? 8
            };
        }
    }

    /// <summary>
    /// Configuration structure for baggage train system, loaded from baggage_config.json.
    /// </summary>
    public class BaggageConfig
    {
        public AccessWindowsConfig AccessWindows { get; set; } = new AccessWindowsConfig();
        public TimingConfig Timing { get; set; } = new TimingConfig();
        public EmergencyAccessConfig EmergencyAccess { get; set; } = new EmergencyAccessConfig();
        public RankGatesConfig RankGates { get; set; } = new RankGatesConfig();
        public LockdownConfig Lockdown { get; set; } = new LockdownConfig();
        public EventsConfig Events { get; set; } = new EventsConfig();
    }

    public class AccessWindowsConfig
    {
        public int TemporaryAccessHours { get; set; } = 4;
        public bool NightHaltGrantsAccess { get; set; } = true;
        public bool MusterGrantsAccess { get; set; } = true;
        public bool SettlementAlwaysAccess { get; set; } = true;
    }

    public class TimingConfig
    {
        public int CaughtUpCheckHours { get; set; } = 24;
        public int CaughtUpChancePercent { get; set; } = 25;
        public int MinCooldownHours { get; set; } = 18;
        public int MaxCooldownHours { get; set; } = 30;
    }

    public class EmergencyAccessConfig
    {
        public int BaseQmRepCost { get; set; } = 5;
        public int NcoQmRepCost { get; set; } = 2;
        public int OfficerQmRepCost { get; set; } = 0;
        public int CooldownHours { get; set; } = 12;
        public int HighRepThreshold { get; set; } = 50;
        public int SpamPenaltySoldierRep { get; set; } = 2;
    }

    public class RankGatesConfig
    {
        public int EmergencyRequestMinTier { get; set; } = 3;
        public int ColumnHaltMinTier { get; set; } = 7;
        public int DailyAccessWindowMinTier { get; set; } = 5;
    }

    public class LockdownConfig
    {
        public int SupplyThresholdPercent { get; set; } = 20;
    }

    public class EventsConfig
    {
        public int DelayEventChanceBadWeather { get; set; } = 15;
        public int DelayEventChanceMountains { get; set; } = 10;
        public int RaidEventChanceEnemyTerritory { get; set; } = 8;
        public int TheftEventChanceLowRep { get; set; } = 5;
    }

    /// <summary>
    /// World-state-aware baggage event probabilities.
    /// Calculated dynamically based on activity level, lord situation, war stance, and terrain.
    /// </summary>
    public class BaggageEventProbabilities
    {
        /// <summary>Probability that wagons catch up with the column (grants temporary access).</summary>
        public int CaughtUpChance { get; set; }

        /// <summary>Probability that wagons are delayed (terrain, weather, retreat).</summary>
        public int DelayChance { get; set; }

        /// <summary>Probability that wagons are raided by enemy forces.</summary>
        public int RaidChance { get; set; }
    }
}

