using System;
using System.Linq;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Config;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Triggers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Enlisted.Features.Camp
{
    /// <summary>
    /// Manages the daily camp-conditions snapshot and related integrations.
    ///
    /// This internal simulation layer runs only while the player is enlisted. It
    /// stores small numeric meters that are updated once per day to feed other
    /// systems such as Quartermaster pricing and mood, Pay Muster text and options,
    /// and Lance Life triggers. It avoids heavy world scanning by using campaign
    /// events and a few small rolling counters.
    /// </summary>
    public sealed class CampLifeBehavior : CampaignBehaviorBase
    {
        private const string LogCategory = "CampLife";

        public static CampLifeBehavior Instance { get; private set; }

        // Snapshot meters (0–100)
        // These internal values drive Quartermaster mood and other systems.
        // Player-visible crime suspicion uses Scrutiny in EscalationState instead.
        private float _logisticsStrain;
        private float _territoryPressure;
        private float _payTension;

        // Daily update gate
        private int _lastSnapshotDayNumber = -1;

        // Simple "recent history" counters (weekly)
        private int _weekNumber;
        private int _battlesThisWeek;
        private int _villagesLootedThisWeek;

        // Quartermaster mood derived from the snapshot (stable for the day)
        private QuartermasterMoodTier _quartermasterMoodTier = QuartermasterMoodTier.Fine;

        public CampLifeBehavior()
        {
            Instance = this;
        }

        public float LogisticsStrain => _logisticsStrain;
        public float TerritoryPressure => _territoryPressure;
        public float PayTension => _payTension;
        public QuartermasterMoodTier QuartermasterMoodTier => _quartermasterMoodTier;

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
            CampaignEvents.VillageLooted.AddNonSerializedListener(this, OnVillageLooted);
        }

        public override void SyncData(IDataStore dataStore)
        {
            SaveLoadDiagnostics.SafeSyncData(this, dataStore, () =>
            {
                dataStore.SyncData("cl_logisticsStrain", ref _logisticsStrain);
                dataStore.SyncData("cl_territoryPressure", ref _territoryPressure);
                dataStore.SyncData("cl_payTension", ref _payTension);
                // Backwards compatibility: Load old morale shock value but don't use it
                float moraleShock = 0f;
                dataStore.SyncData("cl_moraleShock", ref moraleShock);

                var mood = (int)_quartermasterMoodTier;
                dataStore.SyncData("cl_qmMoodTier", ref mood);
                _quartermasterMoodTier = (QuartermasterMoodTier)mood;

                dataStore.SyncData("cl_lastSnapshotDayNumber", ref _lastSnapshotDayNumber);
                dataStore.SyncData("cl_weekNumber", ref _weekNumber);
                dataStore.SyncData("cl_battlesThisWeek", ref _battlesThisWeek);
                dataStore.SyncData("cl_villagesLootedThisWeek", ref _villagesLootedThisWeek);
            });
        }

        private bool IsEnabled()
        {
            return ConfigurationManager.LoadCampLifeConfig()?.Enabled == true;
        }

        public bool IsActiveWhileEnlisted()
        {
            return IsEnabled() && EnlistmentBehavior.Instance?.IsEnlisted == true;
        }

        public bool IsLogisticsHigh()
        {
            var cfg = ConfigurationManager.LoadCampLifeConfig();
            return IsActiveWhileEnlisted() && _logisticsStrain >= (cfg?.LogisticsHighThreshold ?? 70f);
        }

        // Morale tracking removed (morale system no longer exists)

        public bool IsPayTensionHigh()
        {
            var cfg = ConfigurationManager.LoadCampLifeConfig();
            return IsActiveWhileEnlisted() && _payTension >= (cfg?.PayTensionHighThreshold ?? 70f);
        }


        public float GetQuartermasterPurchaseMultiplier()
        {
            if (!IsActiveWhileEnlisted())
            {
                return 1.0f;
            }

            var cfg = ConfigurationManager.LoadCampLifeConfig() ?? new CampLifeConfig();
            switch (_quartermasterMoodTier)
            {
                case QuartermasterMoodTier.Fine:
                    return cfg.QuartermasterPurchaseFine;
                case QuartermasterMoodTier.Tense:
                    return cfg.QuartermasterPurchaseTense;
                case QuartermasterMoodTier.Sour:
                    return cfg.QuartermasterPurchaseSour;
                case QuartermasterMoodTier.Predatory:
                    return cfg.QuartermasterPurchasePredatory;
                default:
                    return 1.0f;
            }
        }

        public float GetQuartermasterBuybackMultiplier()
        {
            if (!IsActiveWhileEnlisted())
            {
                return 1.0f;
            }

            var cfg = ConfigurationManager.LoadCampLifeConfig() ?? new CampLifeConfig();
            switch (_quartermasterMoodTier)
            {
                case QuartermasterMoodTier.Fine:
                    return cfg.QuartermasterBuybackFine;
                case QuartermasterMoodTier.Tense:
                    return cfg.QuartermasterBuybackTense;
                case QuartermasterMoodTier.Sour:
                    return cfg.QuartermasterBuybackSour;
                case QuartermasterMoodTier.Predatory:
                    return cfg.QuartermasterBuybackPredatory;
                default:
                    return 1.0f;
            }
        }

        private void OnDailyTick()
        {
            try
            {
                if (!IsEnabled())
                {
                    return;
                }

                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    return;
                }

                // Weekly counters reset
                var weekNumber = (int)(CampaignTime.Now.ToDays / 7f);
                if (weekNumber != _weekNumber)
                {
                    _weekNumber = weekNumber;
                    _battlesThisWeek = 0;
                    _villagesLootedThisWeek = 0;
                }

                // Daily snapshot update gate
                var dayNumber = (int)CampaignTime.Now.ToDays;
                if (dayNumber == _lastSnapshotDayNumber)
                {
                    return;
                }

                _lastSnapshotDayNumber = dayNumber;

                // Compute the snapshot in a stable, low-cost way.
                var tracker = CampaignTriggerTrackerBehavior.Instance;
                var daysSinceResupply = 5f;
                if (tracker != null)
                {
                    // Resupply can happen at towns or castles (both have markets/stores)
                    // Use the most recent of either settlement type
                    var lastTown = tracker.LastTownEnteredTime;
                    var lastCastle = tracker.LastCastleEnteredTime;
                    var lastResupply = lastTown > lastCastle ? lastTown : lastCastle;

                    if (lastResupply != CampaignTime.Zero)
                    {
                        daysSinceResupply = (float)Math.Max(0d, CampaignTime.Now.ToDays - lastResupply.ToDays);
                    }
                }

                // Logistics strain: long stretches away from resupply points, plus recent disruption.
                var logistics = (daysSinceResupply * 12f) + (_villagesLootedThisWeek * 10f) + (_battlesThisWeek * 4f);
                _logisticsStrain = Clamp01Hundred(logistics);

                // Morale shock tracking removed (morale system no longer exists)

                // Territory pressure: light placeholder for now.
                _territoryPressure = Clamp01Hundred(_villagesLootedThisWeek * 12f);

                // Pay tension: read from EnlistmentBehavior pay system.
                // This is the authoritative source - tracks actual pay delays, backpay, and tension escalation
                _payTension = enlistment.PayTension;

                _quartermasterMoodTier = ComputeQuartermasterMoodTier(_logisticsStrain, _payTension);

                ModLogger.Info(LogCategory,
                    $"Snapshot updated (logistics={_logisticsStrain:0}, payTension={_payTension:0}, mood={_quartermasterMoodTier})");
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "CampLife daily tick failed", ex);
            }
        }

        private void OnVillageLooted(Village village)
        {
            try
            {
                if (!IsActiveWhileEnlisted())
                {
                    return;
                }

                _villagesLootedThisWeek = Math.Max(0, _villagesLootedThisWeek + 1);
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "VillageLooted handler failed", ex);
            }
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            try
            {
                if (!IsActiveWhileEnlisted())
                {
                    return;
                }

                if (!IsMapEventInvolvingMainParty(mapEvent))
                {
                    return;
                }

                _battlesThisWeek = Math.Max(0, _battlesThisWeek + 1);
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "MapEventEnded handler failed", ex);
            }
        }

        private static bool IsMapEventInvolvingMainParty(MapEvent mapEvent)
        {
            try
            {
                if (mapEvent == null)
                {
                    return false;
                }

                var main = MobileParty.MainParty;
                if (main == null)
                {
                    return false;
                }

                return mapEvent.InvolvedParties?.Any(p => p?.MobileParty == main) == true;
            }
            catch
            {
                return false;
            }
        }

        private static QuartermasterMoodTier ComputeQuartermasterMoodTier(float logisticsStrain, float payTension)
        {
            // A small stable blend (0–100). Logistics strain and pay tension drive the score.
            // Morale shock was removed when morale system was eliminated.
            var score = (logisticsStrain * 0.65f) + (payTension * 0.35f);
            score = Clamp01Hundred(score);

            if (score < 25f)
            {
                return QuartermasterMoodTier.Fine;
            }
            if (score < 50f)
            {
                return QuartermasterMoodTier.Tense;
            }
            if (score < 75f)
            {
                return QuartermasterMoodTier.Sour;
            }
            return QuartermasterMoodTier.Predatory;
        }

        private static float Clamp01Hundred(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }
            if (value > 100f)
            {
                return 100f;
            }
            return value;
        }
    }

    public enum QuartermasterMoodTier
    {
        Fine = 0,
        Tense = 1,
        Sour = 2,
        Predatory = 3
    }
}


