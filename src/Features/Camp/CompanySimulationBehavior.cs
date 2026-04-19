using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Enlisted.Features.Camp.Models;
using Enlisted.Features.Company;
using Enlisted.Features.Content;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Escalation;
using Enlisted.Features.Interface.Behaviors;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.SaveSystem;
using Enlisted.Mod.Core.Util;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Enlisted.Features.Camp
{
    /// <summary>
    /// Runs the background company simulation each day.
    /// Soldiers get sick, desert, recover; equipment degrades; incidents occur.
    /// Feeds the news system and provides context for the orchestrator.
    /// </summary>
    public sealed class CompanySimulationBehavior : CampaignBehaviorBase
    {
        private const string LogCategory = "Simulation";
        private const int MaxNewsItemsPerDay = 5;

        /// <summary>Singleton instance for external access.</summary>
        public static CompanySimulationBehavior Instance { get; private set; }

        // State tracking
        private CompanyRoster _roster;
        private CompanyPressure _pressure = new CompanyPressure();
        private HashSet<string> _activeFlags = new HashSet<string>();
        private Dictionary<string, int> _incidentCooldowns = new Dictionary<string, int>();
        private int _lastTickWounded;
        private int _lastProcessedDay = -1;

        // Overlay tracking (saved/loaded)
        private int _sickCount;
        private int _missingCount;
        private int _deadThisCampaign;

        // Configuration
        private SimulationConfig _config;
        private List<CampIncident> _incidentDefinitions;

        public CompanySimulationBehavior()
        {
            Instance = this;
        }

        /// <summary>Current roster state (exposed for WorldStateAnalyzer/ForecastGenerator).</summary>
        public CompanyRoster Roster => _roster;

        /// <summary>Current pressure tracking (exposed for ForecastGenerator).</summary>
        public CompanyPressure Pressure => _pressure;

        /// <summary>Active simulation flags (for incident conditions).</summary>
        public IReadOnlyCollection<string> ActiveFlags => _activeFlags;

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnBattleEnd);
            ModLogger.Info(LogCategory, "Company Simulation registered for daily ticks");
        }

        public override void SyncData(IDataStore dataStore)
        {
            SaveLoadDiagnostics.SafeSyncData(this, dataStore, () =>
            {
                dataStore.SyncData("sim_sickCount", ref _sickCount);
                dataStore.SyncData("sim_missingCount", ref _missingCount);
                dataStore.SyncData("sim_deadThisCampaign", ref _deadThisCampaign);
                dataStore.SyncData("sim_lastTickWounded", ref _lastTickWounded);
                dataStore.SyncData("sim_lastProcessedDay", ref _lastProcessedDay);

                // Pressure tracking
                int daysLowSupplies = _pressure?.DaysLowSupplies ?? 0;
                int daysLowDiscipline = _pressure?.DaysLowDiscipline ?? 0;
                int recentDesertions = _pressure?.RecentDesertions ?? 0;
                int daysHighSickness = _pressure?.DaysHighSickness ?? 0;
                // Backwards compatibility: Load old values but don't use them
                int daysLowMorale = 0;
                int daysLowRest = 0;

                dataStore.SyncData("sim_daysLowSupplies", ref daysLowSupplies);
                dataStore.SyncData("sim_daysLowMorale", ref daysLowMorale); // Discarded
                dataStore.SyncData("sim_daysLowRest", ref daysLowRest); // Discarded (Rest removed 2026-01-11)
                dataStore.SyncData("sim_daysLowDiscipline", ref daysLowDiscipline);
                dataStore.SyncData("sim_recentDesertions", ref recentDesertions);
                dataStore.SyncData("sim_daysHighSickness", ref daysHighSickness);

                _pressure ??= new CompanyPressure();
                _pressure.DaysLowSupplies = daysLowSupplies;
                // DaysLowRest and DaysLowMorale no longer tracked
                _pressure.DaysLowDiscipline = daysLowDiscipline;
                _pressure.RecentDesertions = recentDesertions;
                _pressure.DaysHighSickness = daysHighSickness;

                // Active flags as comma-separated string
                string flagsStr = _activeFlags != null ? string.Join(",", _activeFlags) : "";
                dataStore.SyncData("sim_activeFlags", ref flagsStr);
                _activeFlags = string.IsNullOrEmpty(flagsStr)
                    ? new HashSet<string>()
                    : new HashSet<string>(flagsStr.Split(','));

                // Incident cooldowns as key:value pairs
                string cooldownsStr = _incidentCooldowns != null
                    ? string.Join(",", _incidentCooldowns.Select(kvp => $"{kvp.Key}:{kvp.Value}"))
                    : "";
                dataStore.SyncData("sim_incidentCooldowns", ref cooldownsStr);
                _incidentCooldowns = new Dictionary<string, int>();
                if (!string.IsNullOrEmpty(cooldownsStr))
                {
                    foreach (var pair in cooldownsStr.Split(','))
                    {
                        var parts = pair.Split(':');
                        if (parts.Length == 2 && int.TryParse(parts[1], out int days))
                        {
                            _incidentCooldowns[parts[0]] = days;
                        }
                    }
                }

                // Validate state after loading
                if (dataStore.IsLoading)
                {
                    ValidateLoadedState();
                }
            });
        }

        private void ValidateLoadedState()
        {
            // Ensure non-negative counts
            _sickCount = Math.Max(0, _sickCount);
            _missingCount = Math.Max(0, _missingCount);
            _deadThisCampaign = Math.Max(0, _deadThisCampaign);
            _lastTickWounded = Math.Max(0, _lastTickWounded);

            // Validate pressure
            _pressure ??= new CompanyPressure();
            _activeFlags ??= new HashSet<string>();
            _incidentCooldowns ??= new Dictionary<string, int>();

            ModLogger.Debug(LogCategory, $"Loaded state: Sick={_sickCount}, Missing={_missingCount}, Dead={_deadThisCampaign}");
        }

        private void OnDailyTick()
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    return;
                }

                // Skip if player is prisoner or in battle
                if (Hero.MainHero.IsPrisoner)
                {
                    ModLogger.Debug(LogCategory, "Skipping simulation: player is prisoner");
                    return;
                }

                var party = enlistment.EnlistedLord?.PartyBelongedTo;
                if (party == null || party.MapEvent != null || party.SiegeEvent != null)
                {
                    ModLogger.Debug(LogCategory, "Skipping simulation: party in combat or null");
                    return;
                }

                // Check for time skips (fast forward)
                int currentDay = (int)CampaignTime.Now.ToDays;
                if (_lastProcessedDay > 0 && currentDay > _lastProcessedDay + 1)
                {
                    int missedDays = currentDay - _lastProcessedDay - 1;
                    if (missedDays > 7)
                    {
                        ModLogger.Debug(LogCategory, $"Time skip of {missedDays} days - applying summary effects");
                        ApplyBulkTimeskipEffects(missedDays);
                    }
                    else
                    {
                        for (int i = 0; i < missedDays; i++)
                        {
                            ProcessDailySimulation(party);
                        }
                    }
                }

                _lastProcessedDay = currentDay;

                // Regular daily simulation
                ProcessDailySimulation(party);
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Daily simulation tick failed", ex);
            }
        }

        private void ProcessDailySimulation(MobileParty party)
        {
            LoadConfiguration();
            InitializeRoster(party);

            var result = new SimulationDayResult();

            // Phase 1: Consumption (handled by existing CompanyNeedsManager)
            ProcessConsumption(result);

            // Phase 2: Roster Updates (recovery, healing)
            ProcessRosterRecovery(result, party);

            // Phase 3: Condition Checks (new sickness, injuries, desertion)
            ProcessNewConditions(result, party);

            // Phase 4: Incident Rolls
            ProcessIncidents(result);

            // Phase 5: Pulse Evaluation (threshold crossings)
            ProcessPulse(result);

            // Phase 5.5: Pressure Arc Events (tier-variant narrative events at thresholds)
            CheckPressureArcEvents();

            // Phase 6: News Generation
            GenerateNews(result);

            // Check crisis triggers
            CheckCrisisTriggers(result);

            // Update cooldowns
            DecrementIncidentCooldowns();

            ModLogger.Info(LogCategory, $"Daily simulation complete: {result.TotalNewsItems} news items generated");
        }

        private void LoadConfiguration()
        {
            if (_config != null)
            {
                return;
            }

            try
            {
                var configPath = ModulePaths.GetConfigPath("simulation_config.json");
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var jObject = JObject.Parse(json);
                    _config = ParseSimulationConfig(jObject);
                    _incidentDefinitions = ParseIncidentDefinitions(jObject);
                    ModLogger.Info(LogCategory, $"Loaded simulation config with {_incidentDefinitions?.Count ?? 0} incident definitions");
                }
                else
                {
                    _config = SimulationConfig.Default;
                    _incidentDefinitions = new List<CampIncident>();
                    ModLogger.Warn(LogCategory, "Simulation config not found, using defaults");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to load simulation config", ex);
                _config = SimulationConfig.Default;
                _incidentDefinitions = new List<CampIncident>();
            }
        }

        private void InitializeRoster(MobileParty party)
        {
            if (_roster == null || _roster.TotalSoldiers == 0)
            {
                _roster = new CompanyRoster(party)
                {
                    SickCount = _sickCount,
                    DeadThisCampaign = _deadThisCampaign
                };
                // Restore missing count by adding placeholder entries
                for (int i = 0; i < _missingCount; i++)
                {
                    _roster.AddMissing();
                }
                _lastTickWounded = party.MemberRoster.TotalWounded;
            }
            else
            {
                // Sync overlay counts back (missing count managed internally by roster)
                _roster.SickCount = _sickCount;
                _roster.DeadThisCampaign = _deadThisCampaign;
            }
        }

        /// <summary>Phase 1: Consumption effects are tracked via existing CompanyNeedsManager.</summary>
        private void ProcessConsumption(SimulationDayResult result)
        {
            // Company needs degradation is handled by CompanyNeedsManager.ProcessDailyDegradation
            // We just read the current values for threshold tracking
        }

        /// <summary>Phase 2: Check for wounded recovery (game-controlled) and sick recovery (our system).</summary>
        private void ProcessRosterRecovery(SimulationDayResult result, MobileParty party)
        {
            // Wounded recovery is game-controlled via native Medicine system
            int currentWounded = party.MemberRoster.TotalWounded;
            if (currentWounded < _lastTickWounded)
            {
                int healed = _lastTickWounded - currentWounded;
                var text = healed == 1
                    ? new TextObject("{=sim_healed_one}A wounded soldier has recovered.").ToString()
                    : new TextObject("{=sim_healed_many}{COUNT} wounded soldiers have recovered.")
                        .SetTextVariable("COUNT", healed).ToString();

                result.RosterChanges.Add(RosterChange.Recovered(healed, text));
            }
            _lastTickWounded = currentWounded;

            // Sick recovery (our system)
            if (_sickCount > 0)
            {
                int recovered = 0;
                int died = 0;

                for (int i = 0; i < _sickCount; i++)
                {
                    float recoveryChance = _config.BaseRecoveryChance;
                    float deathChance = _config.BaseDeathChance;

                    // Modifiers based on company state
                    var needs = EnlistmentBehavior.Instance?.CompanyNeeds;
                    if (needs != null)
                    {
                        if (needs.Supplies > 70) recoveryChance += 0.05f;
                        if (needs.Supplies < 30) recoveryChance -= 0.10f;

                        if (needs.Supplies < 20) deathChance += 0.02f;
                    }

                    float roll = MBRandom.RandomFloat;
                    if (roll < deathChance)
                    {
                        died++;
                    }
                    else if (roll < deathChance + recoveryChance)
                    {
                        recovered++;
                    }
                }

                // Process deaths
                for (int i = 0; i < died; i++)
                {
                    _roster.KillSickSoldier();
                    var text = new TextObject("{=sim_died_fever}A soldier died in the night. Fever took him.").ToString();
                    result.Deaths.Add(RosterChange.Death(text));
                }

                // Process recoveries
                if (recovered > 0)
                {
                    _sickCount = Math.Max(0, _sickCount - recovered);
                    _roster.SickCount = _sickCount;

                    var text = recovered == 1
                        ? new TextObject("{=sim_sick_recovered_one}The fever broke. A soldier is back on his feet.").ToString()
                        : new TextObject("{=sim_sick_recovered_many}{COUNT} soldiers recovered from illness.")
                            .SetTextVariable("COUNT", recovered).ToString();

                    result.RosterChanges.Add(RosterChange.Recovered(recovered, text));
                }

                // Update dead count
                _deadThisCampaign = _roster.DeadThisCampaign;
            }

            // Process missing soldiers
            var deserted = _roster.ProcessMissingDays();
            foreach (var id in deserted)
            {
                _roster.ResolveMissing(id, returned: false);
                _pressure.RecentDesertions++;
                var text = new TextObject("{=sim_deserted_confirmed}He's not coming back. Deserted.").ToString();
                result.RosterChanges.Add(RosterChange.Deserted(1, text));
            }
            _missingCount = _roster.MissingCount;
        }

        /// <summary>Phase 3: Generate new sickness, injuries, and desertion attempts.</summary>
        private void ProcessNewConditions(SimulationDayResult result, MobileParty party)
        {
            int totalTroops = _roster.TotalRegulars;
            if (totalTroops <= 5)
            {
                return; // Skip for tiny parties
            }

            // New sickness
            int maxSickness = Math.Max(1, (int)(_config.BaseSicknessRate * totalTroops / 100));
            int newSick = MBRandom.RandomInt(0, maxSickness + 1);

            if (newSick > 0)
            {
                _sickCount += newSick;
                _roster.SickCount = _sickCount;

                var text = newSick == 1
                    ? new TextObject("{=sim_sick_new_one}A soldier reports fever this morning.").ToString()
                    : new TextObject("{=sim_sick_new_many}{COUNT} soldiers report sick this morning.")
                        .SetTextVariable("COUNT", newSick).ToString();

                result.RosterChanges.Add(RosterChange.Sick(newSick, text));
            }

            // New injuries (real game wounds)
            int maxInjury = Math.Max(1, (int)(_config.BaseInjuryRate * totalTroops / 100));
            int newInjured = MBRandom.RandomInt(0, maxInjury + 1);

            // Apply modifiers
            bool isMarching = party.IsMoving && party.CurrentSettlement == null;
            if (isMarching) newInjured = (int)(newInjured * 1.3f);

            if (newInjured > 0 && _roster.TotalRegulars > _roster.WoundedCount + newInjured)
            {
                _roster.WoundRandomTroop(newInjured);

                var text = newInjured == 1
                    ? new TextObject("{=sim_injured_one}A soldier twisted his ankle during the march.").ToString()
                    : new TextObject("{=sim_injured_many}{COUNT} soldiers injured during drill or march.")
                        .SetTextVariable("COUNT", newInjured).ToString();

                result.RosterChanges.Add(RosterChange.Wounded(newInjured, text));
            }

            // Desertion (two-phase: missing first, then confirmed after 3 days)
            float desertionRate = _config.BaseDesertionRate;
            // Morale no longer affects desertion rate (system removed)

            int maxDesertion = Math.Max(0, (int)(desertionRate * totalTroops / 100));
            int newMissing = MBRandom.RandomInt(0, maxDesertion + 1);

            if (newMissing > 0)
            {
                for (int i = 0; i < newMissing; i++)
                {
                    _roster.AddMissing();
                }
                _missingCount = _roster.MissingCount;

                var text = newMissing == 1
                    ? new TextObject("{=sim_missing_one}A soldier didn't report for roll call.").ToString()
                    : new TextObject("{=sim_missing_many}{COUNT} soldiers are missing from roll call.")
                        .SetTextVariable("COUNT", newMissing).ToString();

                result.RosterChanges.Add(RosterChange.Missing(text));
            }
        }

        /// <summary>Phase 4: Roll for random camp incidents (0-2 per day).</summary>
        private void ProcessIncidents(SimulationDayResult result)
        {
            if (_incidentDefinitions == null || _incidentDefinitions.Count == 0)
            {
                return;
            }

            int incidentCount = MBRandom.RandomInt(_config.MinIncidentsPerDay, _config.MaxIncidentsPerDay + 1);

            for (int i = 0; i < incidentCount; i++)
            {
                var incident = SelectIncident();
                if (incident.Id != null)
                {
                    result.Incidents.Add(incident);

                    // Apply effects
                    ApplyIncidentEffects(incident);

                    // Set flag if specified
                    if (!string.IsNullOrEmpty(incident.SetsFlag))
                    {
                        _activeFlags.Add(incident.SetsFlag);
                    }

                    // Set cooldown
                    _incidentCooldowns[incident.Id] = incident.CooldownDays > 0 ? incident.CooldownDays : _config.DefaultCooldownDays;
                }
            }
        }

        private CampIncident SelectIncident()
        {
            // Filter eligible incidents
            var eligible = _incidentDefinitions
                .Where(inc => !_incidentCooldowns.ContainsKey(inc.Id) || _incidentCooldowns[inc.Id] <= 0)
                .Where(inc => string.IsNullOrEmpty(inc.RequiresFlag) || _activeFlags.Contains(inc.RequiresFlag))
                .ToList();

            if (eligible.Count == 0)
            {
                return default;
            }

            // Weight by category and current conditions
            var needs = EnlistmentBehavior.Instance?.CompanyNeeds;
            var filtered = new List<(CampIncident inc, int weight)>();

            foreach (var inc in eligible)
            {
                int weight = inc.Weight;

                // Adjust weight based on conditions
                if (inc.Category == "problems" && needs?.Supplies < 30)
                {
                    weight = (int)(weight * 0.5f); // Less problem incidents when already struggling
                }
                // Morale weight adjustment removed (morale system no longer exists)

                if (weight > 0)
                {
                    filtered.Add((inc, weight));
                }
            }

            if (filtered.Count == 0)
            {
                return default;
            }

            // Weighted random selection
            int totalWeight = filtered.Sum(f => f.weight);
            int roll = MBRandom.RandomInt(totalWeight);
            int cumulative = 0;

            foreach (var (inc, weight) in filtered)
            {
                cumulative += weight;
                if (roll < cumulative)
                {
                    return inc;
                }
            }

            return filtered[0].inc;
        }

        private void ApplyIncidentEffects(CampIncident incident)
        {
            if (incident.Effects == null)
            {
                return;
            }

            var escalation = EscalationManager.Instance;
            var needs = EnlistmentBehavior.Instance?.CompanyNeeds;

            foreach (var effect in incident.Effects)
            {
                switch (effect.Key.ToLower())
                {
                    case "morale":
                        // Morale system removed - skip morale effects
                        break;
                    case "supplies":
                        if (needs != null)
                        {
                            needs.SetNeed(CompanyNeed.Supplies, needs.Supplies + effect.Value);
                        }
                        break;
                    case "discipline":
                        // Legacy: Discipline merged into Scrutiny in Phase 1 (0-100 scale)
                        // Scale old 0-10 values to 0-100
                        escalation?.ModifyScrutiny(effect.Value * 10, "camp incident - discipline");
                        break;
                    case "scrutiny":
                        // Scrutiny effects (0-100 scale)
                        escalation?.ModifyScrutiny(effect.Value, "camp incident");
                        break;
                }

                ModLogger.Debug(LogCategory, $"Applied incident effect: {effect.Key} += {effect.Value}");
            }
        }

        /// <summary>Phase 5: Detect threshold crossings and generate pulse events.</summary>
        private void ProcessPulse(SimulationDayResult result)
        {
            var needs = EnlistmentBehavior.Instance?.CompanyNeeds;
            if (needs == null)
            {
                return;
            }

            // Track pressure days
            if (needs.Supplies < 40) _pressure.DaysLowSupplies++;
            else _pressure.DaysLowSupplies = 0;

            // Morale tracking removed (system no longer exists)

            // High sickness tracking
            if (_roster != null && _roster.TotalSoldiers > 0)
            {
                float sicknessRate = (float)_sickCount / _roster.TotalSoldiers;
                if (sicknessRate > 0.2f) _pressure.DaysHighSickness++;
                else _pressure.DaysHighSickness = 0;
            }

            // Generate pulse news for threshold crossings
            // (threshold crossing detection would need previous values - simplified here)
            if (needs.Supplies < 20 && _pressure.DaysLowSupplies == 1)
            {
                result.PulseEvents.Add(new PulseEvent
                {
                    Need = "Supplies",
                    Direction = "down",
                    NewLevel = "Critical",
                    NewsText = new TextObject("{=sim_pulse_supplies_critical}Starvation rations. Men eye the horses hungrily.").ToString(),
                    Severity = "critical"
                });
            }

            // Morale pulse event removed (morale system no longer exists)
        }

        /// <summary>
        /// Checks pressure counters and fires narrative arc events at thresholds.
        /// Called from ProcessDailySimulation after ProcessPulse.
        /// </summary>
        private void CheckPressureArcEvents()
        {
            if (_pressure == null)
            {
                return;
            }

            // Supply pressure arc
            CheckSupplyPressureArc(_pressure.DaysLowSupplies);
        }

        private void CheckSupplyPressureArc(int daysLow)
        {
            // Only fire at exact thresholds (avoid duplicates)
            string eventId = null;

            switch (daysLow)
            {
                case 3:
                    eventId = GetTierVariantEventId("supply_pressure_stage_1");
                    break;
                case 5:
                    eventId = GetTierVariantEventId("supply_pressure_stage_2");
                    break;
                case 7:
                    eventId = GetTierVariantEventId("supply_crisis");
                    break;
            }

            if (eventId != null)
            {
                var evt = EventCatalog.GetEvent(eventId);
                if (evt != null)
                {
                    EventDeliveryManager.Instance?.QueueEvent(evt);
                    ModLogger.Info(LogCategory, $"Fired supply pressure event: {eventId}");
                }
                else
                {
                    ModLogger.Warn(LogCategory, $"Supply pressure event not found: {eventId}");
                }
            }
        }

        /// <summary>
        /// Returns tier-appropriate event ID suffix based on player tier.
        /// </summary>
        private string GetTierVariantEventId(string baseId)
        {
            var tier = EnlistmentBehavior.Instance?.EnlistmentTier ?? 1;

            if (tier <= 4)
            {
                return $"{baseId}_grunt";
            }

            if (tier <= 6)
            {
                return $"{baseId}_nco";
            }

            return $"{baseId}_cmd";
        }

        /// <summary>Phase 6: Push simulation results to the news system.</summary>
        private void GenerateNews(SimulationDayResult result)
        {
            var news = EnlistedNewsBehavior.Instance;
            if (news == null)
            {
                return;
            }

            int newsCount = 0;

            // Deaths - always show (critical)
            foreach (var death in result.Deaths)
            {
                if (newsCount >= MaxNewsItemsPerDay) break;
                news.AddCampNews(death.NewsText, "critical", "roster");
                newsCount++;
            }

            // Pulse events - notable/critical
            foreach (var pulse in result.PulseEvents)
            {
                if (newsCount >= MaxNewsItemsPerDay) break;
                news.AddCampNews(pulse.NewsText, pulse.Severity, "pulse");
                newsCount++;
            }

            // Roster changes - notable
            foreach (var change in result.RosterChanges)
            {
                if (newsCount >= MaxNewsItemsPerDay) break;
                news.AddCampNews(change.NewsText, change.Severity, "roster");
                newsCount++;
            }

            // Incidents - minor/flavor
            foreach (var incident in result.Incidents)
            {
                if (newsCount >= MaxNewsItemsPerDay) break;
                news.AddCampNews(incident.GetNewsText(), incident.Severity, "incident");
                newsCount++;
            }

            // Update company status
            int netChange = result.Deaths.Count > 0 ? -result.Deaths.Count :
                            result.RosterChanges.Count(c => c.ChangeType == "recovered") -
                            result.RosterChanges.Count(c => c.ChangeType is "sick" or "deserted" or "wounded");

            news.UpdateCompanyStatus(
                _roster?.FitForDuty ?? 0,
                _roster?.TotalSoldiers ?? 0,
                netChange,
                GetPulseText()
            );
        }

        private string GetPulseText()
        {
            var needs = EnlistmentBehavior.Instance?.CompanyNeeds;
            if (needs == null)
            {
                return "";
            }

            // Morale status text removed (morale system no longer exists)
            if (needs.Supplies < 30)
            {
                return new TextObject("{=sim_status_supplies_low}Rations are short. Belts tightened.").ToString();
            }
            if (needs.Supplies > 60)
            {
                return new TextObject("{=sim_status_good}The company is in good shape.").ToString();
            }

            return "";
        }

        private void CheckCrisisTriggers(SimulationDayResult result)
        {
            var needs = EnlistmentBehavior.Instance?.CompanyNeeds;
            if (needs == null)
            {
                return;
            }

            // Supply crisis: 3+ days at critical levels
            if (_pressure.DaysLowSupplies >= 3 && needs.Supplies < 20)
            {
                result.TriggeredCrises.Add("evt_supply_crisis");
                ContentOrchestrator.Instance?.QueueCrisisEvent("evt_supply_crisis");
                ModLogger.Warn(LogCategory, "CRISIS TRIGGERED: Supply crisis");
            }

            // Morale collapse crisis removed (morale system no longer exists)
            // Exhaustion crisis removed 2026-01-11 (Rest system removed)

            // Epidemic: high sickness rate for extended period
            if (_pressure.DaysHighSickness >= 2 && _roster?.CasualtyRate > 0.2f)
            {
                result.TriggeredCrises.Add("evt_epidemic");
                ContentOrchestrator.Instance?.QueueCrisisEvent("evt_epidemic");
                ModLogger.Warn(LogCategory, "CRISIS TRIGGERED: Epidemic");
            }

            // Desertion wave: 5+ desertions in 3 days
            if (_pressure.RecentDesertions >= 5)
            {
                result.TriggeredCrises.Add("evt_desertion_wave");
                ContentOrchestrator.Instance?.QueueCrisisEvent("evt_desertion_wave");
                ModLogger.Warn(LogCategory, "CRISIS TRIGGERED: Desertion wave");
            }
        }

        private void DecrementIncidentCooldowns()
        {
            var keysToRemove = new List<string>();
            foreach (var key in _incidentCooldowns.Keys.ToList())
            {
                _incidentCooldowns[key]--;
                if (_incidentCooldowns[key] <= 0)
                {
                    keysToRemove.Add(key);
                }
            }
            foreach (var key in keysToRemove)
            {
                _incidentCooldowns.Remove(key);
            }
        }

        private void ApplyBulkTimeskipEffects(int days)
        {
            // Simplified effects for long time skips
            // Apply average outcomes instead of simulating each day
            int sickRecovered = (int)(_sickCount * 0.15f * days);
            int sickDied = (int)(_sickCount * 0.02f * days);

            _sickCount = Math.Max(0, _sickCount - sickRecovered - sickDied);
            _deadThisCampaign += sickDied;

            // Decay pressure
            _pressure.RecentDesertions = Math.Max(0, _pressure.RecentDesertions - days / 3);

            ModLogger.Info(LogCategory, $"Applied bulk timeskip effects for {days} days");
        }

        private void OnBattleEnd(TaleWorlds.CampaignSystem.MapEvents.MapEvent mapEvent)
        {
            if (EnlistmentBehavior.Instance?.IsEnlisted != true)
            {
                return;
            }

            // Reset desertion pressure after victory (morale boost)
            var party = EnlistmentBehavior.Instance.EnlistedLord?.PartyBelongedTo;
            if (party != null && mapEvent.IsPlayerMapEvent)
            {
                bool playerWon = mapEvent.WinningSide == party.Party.Side;
                if (playerWon)
                {
                    _pressure.RecentDesertions = Math.Max(0, _pressure.RecentDesertions - 2);
                    ModLogger.Debug(LogCategory, "Battle victory: reduced desertion pressure");
                }
            }
        }

        /// <summary>
        /// Called when lord changes. Resets simulation state for new company.
        /// </summary>
        public void OnLordChanged()
        {
            _roster = null;
            _pressure = new CompanyPressure();
            _sickCount = 0;
            _missingCount = 0;
            _deadThisCampaign = 0;
            _lastTickWounded = 0;
            _activeFlags.Clear();
            _incidentCooldowns.Clear();
            ModLogger.Info(LogCategory, "Simulation state reset for new lord");
        }

        /// <summary>
        /// Check if a specific incident fired recently (for opportunities/events).
        /// </summary>
        public bool HasRecentIncident(string incidentId)
        {
            return _incidentCooldowns.ContainsKey(incidentId);
        }

        #region Configuration Parsing

        private SimulationConfig ParseSimulationConfig(JObject json)
        {
            var config = new SimulationConfig();
            var roster = json["roster"];
            var incidents = json["incidents"];

            if (roster != null)
            {
                config.BaseRecoveryChance = roster["baseRecoveryChance"]?.Value<float>() ?? 0.15f;
                config.BaseDeathChance = roster["baseDeathChance"]?.Value<float>() ?? 0.02f;
                config.BaseSicknessRate = roster["baseSicknessRate"]?.Value<float>() ?? 2f;
                config.BaseInjuryRate = roster["baseInjuryRate"]?.Value<float>() ?? 1f;
                config.BaseDesertionRate = roster["baseDesertionRate"]?.Value<float>() ?? 0.5f;
            }

            if (incidents != null)
            {
                config.MinIncidentsPerDay = incidents["minPerDay"]?.Value<int>() ?? 0;
                config.MaxIncidentsPerDay = incidents["maxPerDay"]?.Value<int>() ?? 2;
                config.DefaultCooldownDays = incidents["cooldownDays"]?.Value<int>() ?? 3;
            }

            return config;
        }

        private List<CampIncident> ParseIncidentDefinitions(JObject json)
        {
            var list = new List<CampIncident>();
            var incidentsArray = json["incident_definitions"] as JArray;

            if (incidentsArray == null)
            {
                return list;
            }

            foreach (var item in incidentsArray)
            {
                var incident = new CampIncident
                {
                    Id = item["id"]?.Value<string>(),
                    Category = item["category"]?.Value<string>() ?? "camp_life",
                    Severity = item["severity"]?.Value<string>() ?? "minor",
                    NewsTextId = item["textId"]?.Value<string>(),
                    NewsTextFallback = item["text"]?.Value<string>(),
                    Weight = item["weight"]?.Value<int>() ?? 10,
                    CooldownDays = item["cooldown"]?.Value<int>() ?? 3,
                    SetsFlag = item["setsFlag"]?.Value<string>(),
                    RequiresFlag = item["requiresFlag"]?.Value<string>()
                };

                var effects = item["effects"] as JObject;
                if (effects != null)
                {
                    incident.Effects = new Dictionary<string, int>();
                    foreach (var prop in effects.Properties())
                    {
                        incident.Effects[prop.Name] = prop.Value.Value<int>();
                    }
                }

                if (!string.IsNullOrEmpty(incident.Id))
                {
                    list.Add(incident);
                }
            }

            return list;
        }

        #endregion
    }

    /// <summary>
    /// Configuration values for the simulation system.
    /// </summary>
    internal class SimulationConfig
    {
        public float BaseRecoveryChance { get; set; } = 0.15f;
        public float BaseDeathChance { get; set; } = 0.02f;
        public float BaseSicknessRate { get; set; } = 2f;
        public float BaseInjuryRate { get; set; } = 1f;
        public float BaseDesertionRate { get; set; } = 0.5f;

        public int MinIncidentsPerDay { get; set; }
        public int MaxIncidentsPerDay { get; set; } = 2;
        public int DefaultCooldownDays { get; set; } = 3;

        public static SimulationConfig Default => new SimulationConfig();
    }
}
