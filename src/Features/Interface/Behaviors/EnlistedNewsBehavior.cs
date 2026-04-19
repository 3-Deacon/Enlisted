using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Enlisted.Features.Camp;
using Enlisted.Features.Camp.Models;
using Enlisted.Features.Conditions;
using Enlisted.Features.Content;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Escalation;
using Enlisted.Features.Interface.News.Generation;
using Enlisted.Features.Interface.News.Generation.Producers;
using Enlisted.Features.Interface.News.Models;
using Enlisted.Features.Interface.News.State;
using Enlisted.Features.Logistics;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Triggers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Enlisted.Features.Interface.Behaviors
{
    /// <summary>
    /// Manages kingdom-wide and personal news/dispatch feeds.
    /// Read-only observer of campaign events that generates localized headlines.
    /// </summary>
    public sealed class EnlistedNewsBehavior : CampaignBehaviorBase
    {
        private const string LogCategory = "News";
        private const int MaxKingdomFeedItems = 60;
        private const int MaxPersonalFeedItems = 35; // Increased from 20 to support priority system with 48h critical events

        /// <summary>
        /// Singleton instance for external access (menu integration, etc.).
        /// </summary>
        public static EnlistedNewsBehavior Instance { get; private set; }

        /// <summary>
        /// Kingdom-wide news feed (wars, battles, settlements, prisoner fates, politics).
        /// Displayed in the enlisted_status menu.
        /// </summary>
        private List<DispatchItem> _kingdomFeed = new List<DispatchItem>();

        /// <summary>
        /// Personal/army news feed (your lord's army, participation, direct orders).
        /// Displayed in the enlisted_activities (camp) menu.
        /// </summary>
        private List<DispatchItem> _personalFeed = new List<DispatchItem>();

        /// <summary>
        /// Day number when the last bulletin was generated (2-day cadence).
        /// </summary>
        private int _lastBulletinDayNumber = -1;

        /// <summary>
        /// Day number when the current Daily Brief was generated.
        /// The brief is intentionally stable for the whole in-game day (light RP flavor).
        /// </summary>
        private int _lastDailyBriefDayNumber = -1;

        /// <summary>
        /// Daily Brief lines (stable for the day).
        /// Stored as raw values (without "Company:"/"Unit:"/"Kingdom:" prefixes) so UIs can format as needed.
        /// </summary>
        private string _dailyBriefCompany = string.Empty;
        private string _dailyBriefUnit = string.Empty;
        private string _dailyBriefKingdom = string.Empty;

        /// <summary>
        /// Tracks when the player last participated in a battle (for "battle aftermath" flavor).
        /// </summary>
        private CampaignTime _lastPlayerBattleTime = CampaignTime.Zero;

        /// <summary>
        /// Type of the last battle: "bandit", "army", "siege", or empty if none.
        /// </summary>
        private string _lastPlayerBattleType = string.Empty;

        /// <summary>
        /// Whether the player won their last battle.
        /// </summary>
        private bool _lastPlayerBattleWon;

        /// <summary>
        /// Cached battle initial strengths for pyrrhic detection.
        /// Key: MapEvent hash code as string.
        /// </summary>
        private Dictionary<string, BattleSnapshot> _battleSnapshots = new Dictionary<string, BattleSnapshot>();

        /// <summary>
        /// Tracks if the lord had an army yesterday (for army formation detection).
        /// </summary>
        private bool _lordHadArmyYesterday;

        /// <summary>
        /// Last time skill progress was checked (cached for 6 hours to avoid expensive recalculation).
        /// </summary>
        private CampaignTime _lastSkillProgressCheck = CampaignTime.Zero;

        /// <summary>
        /// Cached skill progress line result (empty string if no skills near level-up).
        /// </summary>
        private string _cachedSkillProgressLine = string.Empty;

        // Persisted Daily Report and rolling ledger state.
        private CampNewsState _campNewsState = new CampNewsState();

        // "Since last muster" counters for camp news display. Reset at each muster cycle.
        private int _lostSinceLastMuster;
        private int _sickSinceLastMuster;

        /// <summary>
        /// Track order outcomes for display in daily brief and detailed reports.
        /// </summary>
        private List<OrderOutcomeRecord> _orderOutcomes = new List<OrderOutcomeRecord>();

        /// <summary>
        /// Track reputation changes for display in reports.
        /// </summary>
        private List<ReputationChangeRecord> _reputationChanges = new List<ReputationChangeRecord>();

        /// <summary>
        /// Track company need changes for display in reports.
        /// </summary>
        private List<CompanyNeedChangeRecord> _companyNeedChanges = new List<CompanyNeedChangeRecord>();

        /// <summary>
        /// Track event outcomes for Personal Feed headlines and Daily Brief context.
        /// Shows what events fired, choices made, and effects applied.
        /// </summary>
        private List<EventOutcomeRecord> _eventOutcomes = new List<EventOutcomeRecord>();

        /// <summary>
        /// Track pending chain events for Daily Brief context hints.
        /// Shows reminders like "A comrade owes you money" before the follow-up event fires.
        /// </summary>
        private List<PendingEventRecord> _pendingEvents = new List<PendingEventRecord>();

        /// <summary>
        /// Track muster outcomes for camp news display and daily brief.
        /// One entry per muster cycle, showing pay, rations, and unit status.
        /// </summary>
        private List<MusterOutcomeRecord> _musterOutcomes = new List<MusterOutcomeRecord>();

        // Expose the last generated snapshot to other systems (for example, Decisions) without forcing a
        // recomputation. The snapshot contains primitives only, so it is safe to cache and share as read-only.
        private DailyReportSnapshot _lastDailyReportSnapshot;

        // Producer set used to populate snapshot facts without scattering logic across the behavior.
        private static readonly IDailyReportFactProducer[] DailyReportFactProducers =
        {
            new CompanyMovementObjectiveProducer(),
            new UnitStatusFactProducer(),
            new KingdomHeadlineFactProducer()
        };

        public EnlistedNewsBehavior()
        {
            Instance = this;
        }

        /// <summary>
        /// Current kingdom feed for read-only inspection (debugging, UI).
        /// </summary>
        public IReadOnlyList<DispatchItem> KingdomFeed => _kingdomFeed;

        /// <summary>
        /// Current personal feed for read-only inspection (debugging, UI).
        /// </summary>
        public IReadOnlyList<DispatchItem> PersonalFeed => _personalFeed;

        public override void RegisterEvents()
        {
            // Daily tick for bulletin generation and army formation detection
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);

            // Battle events for victory/defeat/pyrrhic detection
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);

            // Siege events
            CampaignEvents.OnSiegeEventStartedEvent.AddNonSerializedListener(this, OnSiegeStarted);

            // Prisoner events (captures, releases, escapes)
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
            CampaignEvents.HeroPrisonerReleased.AddNonSerializedListener(this, OnHeroPrisonerReleased);

            // Hero death/execution
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);

            // Settlement ownership changes
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);

            // Army dispersal (personal feed)
            CampaignEvents.ArmyDispersed.AddNonSerializedListener(this, OnArmyDispersed);

            // Village raids
            CampaignEvents.VillageBeingRaided.AddNonSerializedListener(this, OnVillageRaided);

            // War/peace declarations
            CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
            CampaignEvents.MakePeace.AddNonSerializedListener(this, OnPeaceMade);

            ModLogger.Info(LogCategory, "News behavior registered");
        }

        public override void SyncData(IDataStore dataStore)
        {
            SaveLoadDiagnostics.SafeSyncData(this, dataStore, () =>
            {
                // Sync kingdom feed as individual primitives (lists of complex types require manual handling)
                _kingdomFeed ??= new List<DispatchItem>();
                var kingdomCount = _kingdomFeed.Count;
                dataStore.SyncData("en_news_kingdomCount", ref kingdomCount);

                if (dataStore.IsLoading)
                {
                    _kingdomFeed.Clear();
                    for (var i = 0; i < kingdomCount; i++)
                    {
                        var item = LoadDispatchItem(dataStore, $"en_news_k_{i}");
                        _kingdomFeed.Add(item);
                    }
                }
                else
                {
                    for (var i = 0; i < kingdomCount; i++)
                    {
                        var item = _kingdomFeed[i];
                        SaveDispatchItem(dataStore, $"en_news_k_{i}", item);
                    }
                }

                // Sync personal feed
                _personalFeed ??= new List<DispatchItem>();
                var personalCount = _personalFeed.Count;
                dataStore.SyncData("en_news_personalCount", ref personalCount);

                if (dataStore.IsLoading)
                {
                    _personalFeed.Clear();
                    for (var i = 0; i < personalCount; i++)
                    {
                        var item = LoadDispatchItem(dataStore, $"en_news_p_{i}");
                        _personalFeed.Add(item);
                    }
                }
                else
                {
                    for (var i = 0; i < personalCount; i++)
                    {
                        var item = _personalFeed[i];
                        SaveDispatchItem(dataStore, $"en_news_p_{i}", item);
                    }
                }

                // Sync bulletin tracking
                dataStore.SyncData("en_news_lastBulletinDay", ref _lastBulletinDayNumber);

                // Sync Daily Brief (once-per-day RP digest)
                // Ensure strings are never null before syncing (Bannerlord SyncData requires non-null refs)
                _dailyBriefCompany ??= string.Empty;
                _dailyBriefUnit ??= string.Empty;
                _dailyBriefKingdom ??= string.Empty;

                dataStore.SyncData("en_news_dailyBriefDay", ref _lastDailyBriefDayNumber);
                dataStore.SyncData("en_news_dailyBriefCompany", ref _dailyBriefCompany);
                dataStore.SyncData("en_news_dailyBriefUnit", ref _dailyBriefUnit);
                dataStore.SyncData("en_news_dailyBriefKingdom", ref _dailyBriefKingdom);

                // Battle aftermath tracking for daily brief flavor
                _lastPlayerBattleType ??= string.Empty;
                dataStore.SyncData("en_news_lastBattleTime", ref _lastPlayerBattleTime);
                dataStore.SyncData("en_news_lastBattleType", ref _lastPlayerBattleType);
                dataStore.SyncData("en_news_lastBattleWon", ref _lastPlayerBattleWon);

                // "Since last muster" counters for camp news display
                dataStore.SyncData("en_news_lostSinceMuster", ref _lostSinceLastMuster);
                dataStore.SyncData("en_news_sickSinceMuster", ref _sickSinceLastMuster);

                // Sync battle snapshots (for pyrrhic detection across save/load)
                _battleSnapshots ??= new Dictionary<string, BattleSnapshot>();
                var snapshotCount = _battleSnapshots.Count;
                dataStore.SyncData("en_news_snapshotCount", ref snapshotCount);

                if (dataStore.IsLoading)
                {
                    _battleSnapshots.Clear();
                    for (var i = 0; i < snapshotCount; i++)
                    {
                        var snapshot = LoadBattleSnapshot(dataStore, $"en_news_bs_{i}");
                        if (!string.IsNullOrEmpty(snapshot.MapEventId))
                        {
                            _battleSnapshots[snapshot.MapEventId] = snapshot;
                        }
                    }
                }
                else
                {
                    var snapshotList = _battleSnapshots.Values.ToList();
                    for (var i = 0; i < snapshotCount; i++)
                    {
                        SaveBattleSnapshot(dataStore, $"en_news_bs_{i}", snapshotList[i]);
                    }
                }

                // Sync army tracking state
                dataStore.SyncData("en_news_lordHadArmy", ref _lordHadArmyYesterday);

                // Persisted camp news state (Daily Report archive + ledger).
                _campNewsState ??= new CampNewsState();
                _campNewsState.SyncData(dataStore);

                // Sync order outcomes tracking
                _orderOutcomes ??= new List<OrderOutcomeRecord>();
                var orderOutcomesCount = _orderOutcomes.Count;
                dataStore.SyncData("en_news_orderOutcomesCount", ref orderOutcomesCount);

                if (dataStore.IsLoading)
                {
                    _orderOutcomes.Clear();
                    for (var i = 0; i < orderOutcomesCount; i++)
                    {
                        var record = LoadOrderOutcomeRecord(dataStore, $"en_news_order_{i}");
                        _orderOutcomes.Add(record);
                    }
                }
                else
                {
                    for (var i = 0; i < orderOutcomesCount; i++)
                    {
                        SaveOrderOutcomeRecord(dataStore, $"en_news_order_{i}", _orderOutcomes[i]);
                    }
                }

                // Sync reputation changes tracking
                _reputationChanges ??= new List<ReputationChangeRecord>();
                var reputationChangesCount = _reputationChanges.Count;
                dataStore.SyncData("en_news_reputationChangesCount", ref reputationChangesCount);

                if (dataStore.IsLoading)
                {
                    _reputationChanges.Clear();
                    for (var i = 0; i < reputationChangesCount; i++)
                    {
                        var record = LoadReputationChangeRecord(dataStore, $"en_news_rep_{i}");
                        _reputationChanges.Add(record);
                    }
                }
                else
                {
                    for (var i = 0; i < reputationChangesCount; i++)
                    {
                        SaveReputationChangeRecord(dataStore, $"en_news_rep_{i}", _reputationChanges[i]);
                    }
                }

                // Sync company need changes tracking
                _companyNeedChanges ??= new List<CompanyNeedChangeRecord>();
                var needChangesCount = _companyNeedChanges.Count;
                dataStore.SyncData("en_news_needChangesCount", ref needChangesCount);

                if (dataStore.IsLoading)
                {
                    _companyNeedChanges.Clear();
                    for (var i = 0; i < needChangesCount; i++)
                    {
                        var record = LoadCompanyNeedChangeRecord(dataStore, $"en_news_need_{i}");
                        _companyNeedChanges.Add(record);
                    }
                }
                else
                {
                    for (var i = 0; i < needChangesCount; i++)
                    {
                        SaveCompanyNeedChangeRecord(dataStore, $"en_news_need_{i}", _companyNeedChanges[i]);
                    }
                }

                // Sync event outcomes tracking
                _eventOutcomes ??= new List<EventOutcomeRecord>();
                var eventOutcomesCount = _eventOutcomes.Count;
                dataStore.SyncData("en_news_eventOutcomesCount", ref eventOutcomesCount);

                if (dataStore.IsLoading)
                {
                    _eventOutcomes.Clear();
                    for (var i = 0; i < eventOutcomesCount; i++)
                    {
                        var record = LoadEventOutcomeRecord(dataStore, $"en_news_evt_{i}");
                        _eventOutcomes.Add(record);
                    }
                }
                else
                {
                    for (var i = 0; i < eventOutcomesCount; i++)
                    {
                        SaveEventOutcomeRecord(dataStore, $"en_news_evt_{i}", _eventOutcomes[i]);
                    }
                }

                // Sync pending events tracking
                _pendingEvents ??= new List<PendingEventRecord>();
                var pendingEventsCount = _pendingEvents.Count;
                dataStore.SyncData("en_news_pendingEventsCount", ref pendingEventsCount);

                if (dataStore.IsLoading)
                {
                    _pendingEvents.Clear();
                    for (var i = 0; i < pendingEventsCount; i++)
                    {
                        var record = LoadPendingEventRecord(dataStore, $"en_news_pend_{i}");
                        _pendingEvents.Add(record);
                    }
                }
                else
                {
                    for (var i = 0; i < pendingEventsCount; i++)
                    {
                        SavePendingEventRecord(dataStore, $"en_news_pend_{i}", _pendingEvents[i]);
                    }
                }

                // Safe initialization for null collections after load
                _kingdomFeed ??= new List<DispatchItem>();
                _personalFeed ??= new List<DispatchItem>();
                _battleSnapshots ??= new Dictionary<string, BattleSnapshot>();
                _orderOutcomes ??= new List<OrderOutcomeRecord>();
                _reputationChanges ??= new List<ReputationChangeRecord>();
                _companyNeedChanges ??= new List<CompanyNeedChangeRecord>();
                _eventOutcomes ??= new List<EventOutcomeRecord>();
                _pendingEvents ??= new List<PendingEventRecord>();

                _dailyBriefCompany ??= string.Empty;
                _dailyBriefUnit ??= string.Empty;
                _dailyBriefKingdom ??= string.Empty;

                // Trim feeds to max size
                TrimFeeds();

                ModLogger.Debug(LogCategory, $"News data synced: kingdom={_kingdomFeed.Count}, personal={_personalFeed.Count}");
            });
        }

        #region Public API (Menu Integration)

        /// <summary>
        /// Builds the player status RP flavor line for use in menus.
        /// Returns tier-appropriate, context-aware text about the player's current condition.
        /// </summary>
        public string BuildPlayerStatusLine(EnlistmentBehavior enlistment)
        {
            return BuildDailyUnitLine(enlistment ?? EnlistmentBehavior.Instance);
        }

        /// <summary>
        /// Returns the last battle the player participated in (if any), for contextual menu flavor.
        /// </summary>
        public bool TryGetLastPlayerBattleSummary(out CampaignTime battleTime, out bool playerWon)
        {
            battleTime = _lastPlayerBattleTime;
            playerWon = _lastPlayerBattleWon;
            return battleTime != CampaignTime.Zero;
        }

        /// <summary>
        /// Builds the once-per-day Daily Brief as a single flowing RP narrative paragraph.
        /// Combines company situation, casualties, player status, and kingdom news into immersive text.
        /// </summary>
        public string BuildDailyBriefSection()
        {
            try
            {
                if (!IsEnlisted())
                {
                    return string.Empty;
                }

                EnsureDailyBriefGenerated();

                // If generation produced no content, hide the section
                if (string.IsNullOrWhiteSpace(_dailyBriefCompany) &&
                    string.IsNullOrWhiteSpace(_dailyBriefUnit) &&
                    string.IsNullOrWhiteSpace(_dailyBriefKingdom))
                {
                    return string.Empty;
                }

                // Build report with clear section headers: Kingdom, Company, Your Status
                var kingdomParts = new List<string>();
                var companyParts = new List<string>();
                var playerParts = new List<string>();

                // Kingdom Reports
                // Add forecast line first if strategic order is imminent
                var kingdomForecast = BuildKingdomForecastLine();
                if (!string.IsNullOrWhiteSpace(kingdomForecast))
                {
                    kingdomParts.Add(kingdomForecast);
                }

                if (!string.IsNullOrWhiteSpace(_dailyBriefKingdom))
                {
                    kingdomParts.Add(_dailyBriefKingdom);
                }

                // Company Reports
                // Add forecast line first if company-level order is imminent
                var companyForecast = BuildCompanyForecastLine();
                if (!string.IsNullOrWhiteSpace(companyForecast))
                {
                    companyParts.Add(companyForecast);
                }

                if (!string.IsNullOrWhiteSpace(_dailyBriefCompany))
                {
                    companyParts.Add(_dailyBriefCompany);
                }

                // Add supply context when relevant (< 70%)
                var supplyContext = BuildSupplyContextLine();
                if (!string.IsNullOrWhiteSpace(supplyContext))
                {
                    companyParts.Add(supplyContext);
                }

                // Add casualty report with RP flavor (losses and wounded since last muster)
                var casualtyLine = BuildCasualtyReportLine();
                if (!string.IsNullOrWhiteSpace(casualtyLine))
                {
                    companyParts.Add(casualtyLine);
                }

                // Add recent event aftermath (events in last 24 hours)
                var recentEventLine = BuildRecentEventLine();
                if (!string.IsNullOrWhiteSpace(recentEventLine))
                {
                    companyParts.Add(recentEventLine);
                }

                // Add pending chain event hints
                var pendingLine = BuildPendingEventsLine();
                if (!string.IsNullOrWhiteSpace(pendingLine))
                {
                    companyParts.Add(pendingLine);
                }

                // Add flag-based personality context
                var flagContext = BuildFlagContextLine();
                if (!string.IsNullOrWhiteSpace(flagContext))
                {
                    companyParts.Add(flagContext);
                }

                // Add skill progress hint if any skill is close to level-up
                var skillProgress = BuildSkillProgressLine();
                if (!string.IsNullOrWhiteSpace(skillProgress))
                {
                    companyParts.Add(skillProgress);
                }

                // Add retinue context for commanders (T7+)
                var retinueContext = BuildRetinueContextLine();
                if (!string.IsNullOrWhiteSpace(retinueContext))
                {
                    companyParts.Add(retinueContext);
                }

                // Add baggage status when notable (locked, delayed, temporary access)
                var baggageStatus = BuildBaggageStatusLine();
                if (!string.IsNullOrWhiteSpace(baggageStatus))
                {
                    companyParts.Add(baggageStatus);
                }

                // Add camp rumors (social activities others are doing that player can join)
                var campRumors = BuildCampRumorsLine();
                if (!string.IsNullOrWhiteSpace(campRumors))
                {
                    companyParts.Add(campRumors);
                }

                // Your Status (player)
                // Add forecast line first if player has imminent duty
                var playerForecast = BuildPlayerForecastLine();
                if (!string.IsNullOrWhiteSpace(playerForecast))
                {
                    playerParts.Add(playerForecast);
                }

                if (!string.IsNullOrWhiteSpace(_dailyBriefUnit))
                {
                    playerParts.Add(_dailyBriefUnit);
                }

                // Add personal hints (player-specific opportunities like medical care, rest)
                var personalHints = BuildPersonalHintsLine();
                if (!string.IsNullOrWhiteSpace(personalHints))
                {
                    playerParts.Add(personalHints);
                }

                // Build final output with three section headers
                var sb = new System.Text.StringBuilder();
                
                if (kingdomParts.Count > 0)
                {
                    sb.AppendLine("Kingdom Reports");
                    sb.AppendLine();
                    sb.AppendLine(string.Join(" ", kingdomParts));
                }

                if (companyParts.Count > 0)
                {
                    if (kingdomParts.Count > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine();
                    }
                    sb.AppendLine("Company Reports");
                    sb.AppendLine();
                    sb.AppendLine(string.Join(" ", companyParts));
                }

                if (playerParts.Count > 0)
                {
                    if (kingdomParts.Count > 0 || companyParts.Count > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine();
                    }
                    sb.AppendLine("Your Status");
                    sb.AppendLine();
                    sb.AppendLine(string.Join(" ", playerParts));
                }

                var paragraph = sb.ToString().TrimEnd();

                return paragraph;
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Error building Daily Brief section", ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// Builds an RP-flavored casualty report line for the daily brief.
        /// Shows losses and wounded since last muster in narrative style.
        /// </summary>
        private string BuildCasualtyReportLine()
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                var lord = enlistment?.CurrentLord;
                var lordParty = lord?.PartyBelongedTo;

                var lostCount = _lostSinceLastMuster;
                var woundedCount = lordParty?.MemberRoster?.TotalWounded ?? 0;

                // No casualties to report
                if (lostCount <= 0 && woundedCount <= 0)
                {
                    return string.Empty;
                }

                // Heavy losses - somber tone
                if (lostCount >= 10)
                {
                    var text = new TextObject("{=brief_casualties_heavy}The company has paid dearly — {COUNT} souls lost since last muster. The men speak in hushed tones and sleep comes hard.");
                    text.SetTextVariable("COUNT", $"<span style=\"Alert\">{lostCount}</span>");
                    return text.ToString();
                }

                // Moderate losses with wounded
                if (lostCount >= 5)
                {
                    if (woundedCount >= 10)
                    {
                        var text = new TextObject("{=brief_casualties_moderate_wounded}Hard fighting has cost us {DEAD} dead and left {WOUNDED} nursing wounds. The surgeons work through the night.");
                        text.SetTextVariable("DEAD", $"<span style=\"Alert\">{lostCount}</span>");
                        text.SetTextVariable("WOUNDED", $"<span style=\"Warning\">{woundedCount}</span>");
                        return text.ToString();
                    }
                    var text2 = new TextObject("{=brief_casualties_moderate}We've lost {COUNT} good soldiers since the last muster. Their names are spoken at the evening fire.");
                    text2.SetTextVariable("COUNT", $"<span style=\"Alert\">{lostCount}</span>");
                    return text2.ToString();
                }

                // Some losses
                if (lostCount >= 2)
                {
                    if (woundedCount >= 15)
                    {
                        var text = new TextObject("{=brief_casualties_few_manywounded}The wounded fill the medical tents, groaning through the night. {COUNT} didn't make it.");
                        text.SetTextVariable("COUNT", $"<span style=\"Alert\">{lostCount}</span>");
                        return text.ToString();
                    }
                    if (woundedCount >= 5)
                    {
                        var text = new TextObject("{=brief_casualties_few_somewounded}{DEAD} fallen, {WOUNDED} wounded — the cost of the march weighs on every man.");
                        text.SetTextVariable("DEAD", $"<span style=\"Alert\">{lostCount}</span>");
                        text.SetTextVariable("WOUNDED", $"<span style=\"Warning\">{woundedCount}</span>");
                        return text.ToString();
                    }
                    var text2 = new TextObject("{=brief_casualties_few}{COUNT} soldiers have fallen since last muster.");
                    text2.SetTextVariable("COUNT", $"<span style=\"Alert\">{lostCount}</span>");
                    return text2.ToString();
                }

                // Single loss
                if (lostCount == 1)
                {
                    if (woundedCount >= 10)
                    {
                        var text = new TextObject("{=brief_casualties_one_manywounded}One of ours didn't make it. {WOUNDED} others nurse their wounds and pray they're luckier.");
                        text.SetTextVariable("WOUNDED", $"<span style=\"Warning\">{woundedCount}</span>");
                        return text.ToString();
                    }
                    return new TextObject("{=brief_casualties_one}One of ours didn't make it through. The lads are quiet today.").ToString();
                }

                // No deaths but significant wounded
                if (woundedCount >= 20)
                {
                    var text = new TextObject("{=brief_wounded_many}The surgeons are overwhelmed — {COUNT} wounded fill the medical tents, and the air reeks of blood and poultices.");
                    text.SetTextVariable("COUNT", $"<span style=\"Warning\">{woundedCount}</span>");
                    return text.ToString();
                }
                if (woundedCount >= 10)
                {
                    var text = new TextObject("{=brief_wounded_moderate}{COUNT} soldiers recovering from their wounds. The company marches slower for it.");
                    text.SetTextVariable("COUNT", $"<span style=\"Warning\">{woundedCount}</span>");
                    return text.ToString();
                }
                if (woundedCount >= 5)
                {
                    var text = new TextObject("{=brief_wounded_few}A handful of wounded among us — {COUNT} in all, patched up and carrying on.");
                    text.SetTextVariable("COUNT", $"<span style=\"Warning\">{woundedCount}</span>");
                    return text.ToString();
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                ModLogger.Debug(LogCategory, $"Error building casualty report: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Builds a forecast line for Kingdom Reports section.
        /// Shows strategic orders that are imminent (4-8 hours).
        /// </summary>
        private string BuildKingdomForecastLine()
        {
            try
            {
                var orderManager = Orders.Behaviors.OrderManager.Instance;
                if (orderManager == null || !orderManager.IsOrderImminent())
                {
                    return string.Empty;
                }

                var order = orderManager.GetCurrentOrder();
                if (order == null || !order.Tags.Contains("strategic"))
                {
                    return string.Empty;
                }

                var hoursUntil = orderManager.GetHoursUntilIssue();
                var text = new TextObject("{=forecast_strategic_orders}Expect strategic orders from command soon. (in {HOURS}h)");
                text.SetTextVariable("HOURS", $"{(int)hoursUntil}");
                return $"<span style=\"Warning\">{text}</span>";
            }
            catch (Exception ex)
            {
                ModLogger.Debug(LogCategory, $"Error building kingdom forecast: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Builds a forecast line for Company Reports section.
        /// Shows company-level orders that are imminent (4-8 hours).
        /// </summary>
        private string BuildCompanyForecastLine()
        {
            try
            {
                var orderManager = Orders.Behaviors.OrderManager.Instance;
                if (orderManager == null || !orderManager.IsOrderImminent())
                {
                    return string.Empty;
                }

                var order = orderManager.GetCurrentOrder();
                if (order == null || order.Tags.Contains("strategic"))
                {
                    // Strategic orders show in Kingdom section, not Company section
                    return string.Empty;
                }

                var forecastText = orderManager.GetImminentWarningText();
                if (string.IsNullOrWhiteSpace(forecastText))
                {
                    return string.Empty;
                }

                var hoursUntil = orderManager.GetHoursUntilIssue();
                return $"<span style=\"Warning\">{forecastText} (in {(int)hoursUntil}h)</span>";
            }
            catch (Exception ex)
            {
                ModLogger.Debug(LogCategory, $"Error building company forecast: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Builds a forecast line for Your Status section.
        /// Shows player-specific duty forecast (imminent orders) and scheduled commitments.
        /// </summary>
        private string BuildPlayerForecastLine()
        {
            try
            {
                var parts = new List<string>();

                // Check for scheduled commitments first (player's plans)
                var commitmentLine = BuildCommitmentLine();
                if (!string.IsNullOrWhiteSpace(commitmentLine))
                {
                    parts.Add(commitmentLine);
                }

                // Then check for imminent orders
                var orderManager = Orders.Behaviors.OrderManager.Instance;
                if (orderManager != null && orderManager.IsOrderImminent())
                {
                    var order = orderManager.GetCurrentOrder();
                    if (order != null)
                    {
                        var hoursUntil = orderManager.GetHoursUntilIssue();

                        // For mandatory orders, phrase as upcoming duty
                        if (order.Mandatory)
                        {
                            var text = new TextObject("{=forecast_duty_imminent}Duty assignment coming in {HOURS}h: {TITLE}");
                            text.SetTextVariable("HOURS", $"{(int)hoursUntil}");
                            text.SetTextVariable("TITLE", Orders.OrderCatalog.GetDisplayTitle(order));
                            parts.Add($"<span style=\"Warning\">{text}</span>");
                        }
                        else
                        {
                            // For optional orders, phrase as opportunity
                            var text2 = new TextObject("{=forecast_order_imminent}Order opportunity coming in {HOURS}h: {TITLE}");
                            text2.SetTextVariable("HOURS", $"{(int)hoursUntil}");
                            text2.SetTextVariable("TITLE", Orders.OrderCatalog.GetDisplayTitle(order));
                            parts.Add($"<span style=\"Warning\">{text2}</span>");
                        }
                    }
                }

                return string.Join(" ", parts);
            }
            catch (Exception ex)
            {
                ModLogger.Debug(LogCategory, $"Error building player forecast: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Builds a commitment display line for the Your Status section.
        /// Shows the player's scheduled camp activities.
        /// </summary>
        private string BuildCommitmentLine()
        {
            try
            {
                var generator = Camp.CampOpportunityGenerator.Instance;
                if (generator == null)
                {
                    return string.Empty;
                }

                var nextCommitment = generator.GetNextCommitment();
                if (nextCommitment == null)
                {
                    return string.Empty;
                }

                var hoursUntil = generator.GetHoursUntilCommitment(nextCommitment);
                var activityName = nextCommitment.Title?.ToLower() ?? "an activity";
                var phase = nextCommitment.ScheduledPhase?.ToLower() ?? "later";

                // Build the commitment line
                TextObject text;
                if (hoursUntil <= 1)
                {
                    text = new TextObject("{=commitment_soon}You've committed to {ACTIVITY}. It's almost time.");
                    text.SetTextVariable("ACTIVITY", activityName);
                }
                else if (hoursUntil <= 6)
                {
                    text = new TextObject("{=commitment_today}You've committed to {ACTIVITY} this {PHASE}.");
                    text.SetTextVariable("ACTIVITY", activityName);
                    text.SetTextVariable("PHASE", phase);
                }
                else
                {
                    text = new TextObject("{=commitment_tomorrow}You've committed to {ACTIVITY} tomorrow at {PHASE}.");
                    text.SetTextVariable("ACTIVITY", activityName);
                    text.SetTextVariable("PHASE", phase);
                }

                return $"<span style=\"Link\">{text}</span>";
            }
            catch (Exception ex)
            {
                ModLogger.Debug(LogCategory, $"Error building commitment line: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Builds a supply context line for the Daily Brief.
        /// Shows warnings when supply levels are concerning.
        /// </summary>
        private string BuildSupplyContextLine()
        {
            try
            {
                var supply = EnlistmentBehavior.Instance?.CompanyNeeds?.Supplies ?? 100;

                if (supply < 30)
                {
                    var text = new TextObject("{=brief_supply_critical}The supply wagons are nearly empty. Men eye what remains with worry, and the quartermaster has locked the stores. Equipment requisitions are on hold until the next resupply.").ToString();
                    return $"<span style=\"Alert\">{text}</span>";
                }
                if (supply < 50)
                {
                    var text = new TextObject("{=brief_supply_low}Supplies are running thin. Rations have been cut and the quartermaster counts every bolt and bandage with a furrowed brow.").ToString();
                    return $"<span style=\"Warning\">{text}</span>";
                }
                if (supply < 70)
                {
                    return new TextObject("{=brief_supply_moderate}Supplies are adequate but dwindling. The men grumble about the portions, though there's enough for now.").ToString();
                }

                // Good supply levels don't need mention
                return string.Empty;
            }
            catch (Exception ex)
            {
                ModLogger.Debug(LogCategory, $"Error building supply context: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Builds a retinue status line for the Daily Brief.
        /// Shows context about the player's personal retinue when they have commander rank.
        /// </summary>
        private static string BuildRetinueContextLine()
        {
            try
            {
                var state = Retinue.Core.RetinueManager.Instance?.State;
                if (state == null || !state.HasTypeSelected)
                {
                    // Not a commander or no retinue type selected
                    return string.Empty;
                }

                var loyalty = state.RetinueLoyalty;
                var soldierCount = state.TotalSoldiers;
                var veteranCount = state.NamedVeterans?.Count ?? 0;

                // Get capacity from tier
                var enlistment = EnlistmentBehavior.Instance;
                var capacity = enlistment != null
                    ? Retinue.Core.RetinueManager.GetTierCapacity(enlistment.EnlistmentTier)
                    : 20;

                // Build context based on loyalty and composition
                if (soldierCount == 0)
                {
                    return new TextObject("{=brief_ret_empty}Your retinue stands empty. You must acquire soldiers to rebuild your personal command.").ToString();
                }

                if (loyalty < 20)
                {
                    return new TextObject("{=brief_ret_critical}Your retinue's loyalty is dangerously low. The men eye you with barely-concealed resentment. Desertion seems imminent.").ToString();
                }

                if (loyalty < 40)
                {
                    var line = new TextObject("{=brief_ret_low}Your {COUNT} retinue soldiers serve grudgingly. Morale is poor, and complaints circulate openly.");
                    line.SetTextVariable("COUNT", soldierCount);
                    return line.ToString();
                }

                if (loyalty >= 80)
                {
                    if (veteranCount > 0)
                    {
                        var line = new TextObject("{=brief_ret_high_vets}Your retinue of {COUNT} stands loyal and eager. Your {VET_COUNT} named veterans speak well of you around the campfire.");
                        line.SetTextVariable("COUNT", soldierCount);
                        line.SetTextVariable("VET_COUNT", veteranCount);
                        return line.ToString();
                    }
                    else
                    {
                        var line = new TextObject("{=brief_ret_high}Your retinue of {COUNT} soldiers stands ready and devoted. They trust your command.");
                        line.SetTextVariable("COUNT", soldierCount);
                        return line.ToString();
                    }
                }

                // Normal range - only show if at capacity or have veterans
                if (veteranCount > 0)
                {
                    var line = new TextObject("{=brief_ret_vets}Among your {COUNT} retinue soldiers, {VET_COUNT} named veterans stand out as battle-hardened warriors.");
                    line.SetTextVariable("COUNT", soldierCount);
                    line.SetTextVariable("VET_COUNT", veteranCount);
                    return line.ToString();
                }

                if (soldierCount >= capacity)
                {
                    var line = new TextObject("{=brief_ret_full}Your retinue has reached its full complement of {COUNT} soldiers.");
                    line.SetTextVariable("COUNT", soldierCount);
                    return line.ToString();
                }

                // Normal state with nothing notable
                return string.Empty;
            }
            catch (Exception ex)
            {
                ModLogger.Debug(LogCategory, $"Error building retinue context: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Builds a baggage status line for the Daily Brief when the state is notable.
        /// Shows status for: locked (supply crisis), delayed (days behind), temporary access (hours remaining),
        /// recent raids, or recent arrivals. Does not show FullAccess in settlements (normal state).
        /// Uses sea-specific text when party is at sea.
        /// </summary>
        public static string BuildBaggageStatusLine()
        {
            try
            {
                var baggageManager = BaggageTrainManager.Instance;
                if (baggageManager == null)
                {
                    return string.Empty;
                }

                var currentAccess = baggageManager.GetCurrentAccess();
                var enlistment = EnlistmentBehavior.Instance;
                var lordParty = enlistment?.CurrentLord?.PartyBelongedTo;
                
                // BUGFIX: IsCurrentlyAtSea can incorrectly return true when docked at coastal settlements
                // Only consider at-sea if not in a settlement and not besieging
                var isAtSea = lordParty != null &&
                              lordParty.CurrentSettlement == null &&
                              lordParty.BesiegedSettlement == null &&
                              lordParty.IsCurrentlyAtSea;

                // Priority 1: Check for recent raid (within last 3 days) - most urgent
                var daysSinceRaid = baggageManager.GetDaysSinceLastRaid();
                if (daysSinceRaid >= 0 && daysSinceRaid <= 3)
                {
                    // Same-day raid gets specific wording, older raids get general atmosphere
                    string raidText;
                    if (isAtSea)
                    {
                        raidText = daysSinceRaid == 0
                            ? new TextObject("{=brief_baggage_raided_sea}Pirates hit our convoy this morning. The crew is still counting what's missing from the hold.").ToString()
                            : new TextObject("{=brief_baggage_raided_recent_sea}The pirate attack weighs on everyone's mind. The crew keeps nervous watch on the horizon.").ToString();
                    }
                    else
                    {
                        raidText = daysSinceRaid == 0
                            ? new TextObject("{=brief_baggage_raided_today}Raiders struck the baggage train this morning. The wagon guards are still counting what's missing.").ToString()
                            : new TextObject("{=brief_baggage_raided_recent}The baggage raid weighs on everyone's mind. The guards double their watches at night.").ToString();
                    }
                    return $"<span style=\"Alert\">{raidText}</span>";
                }

                // Priority 2: Locked state (supply crisis)
                if (currentAccess == BaggageAccessState.Locked)
                {
                    var text = isAtSea
                        ? new TextObject("{=brief_baggage_locked_sea}The quartermaster has locked the ship's hold. Supplies are too scarce for personal requisitions.").ToString()
                        : new TextObject("{=brief_baggage_locked}The quartermaster has locked the baggage wagons. Supplies are too scarce for personal requisitions.").ToString();
                    return $"<span style=\"Alert\">{text}</span>";
                }

                // Priority 3: Active delay
                var delayDays = baggageManager.GetBaggageDelayDaysRemaining();
                if (delayDays > 0)
                {
                    // Sea variant uses different phrasing (cargo shifted, not stuck in mud)
                    string delayText;
                    if (isAtSea)
                    {
                        delayText = new TextObject("{=brief_baggage_delayed_sea}Rough seas have shifted the cargo. Your belongings are buried under crates for now.").ToString();
                    }
                    else if (delayDays == 1)
                    {
                        delayText = new TextObject("{=brief_baggage_delayed_single}The wagons are stuck in the mud, a day behind the column. The drivers curse and strain.").ToString();
                    }
                    else
                    {
                        var line = new TextObject("{=brief_baggage_delayed}The wagons are stuck in the mud, {DAYS} days behind the column. The drivers curse and strain.");
                        line.SetTextVariable("DAYS", delayDays);
                        delayText = line.ToString();
                    }
                    return $"<span style=\"Warning\">{delayText}</span>";
                }

                // Priority 4: Recent arrival (within last 2 days) - good news
                var daysSinceArrival = baggageManager.GetDaysSinceLastArrival();
                if (daysSinceArrival >= 0 && daysSinceArrival <= 2)
                {
                    // Same-day arrival is more urgent/specific than lingering access
                    string arrivalText;
                    if (isAtSea)
                    {
                        arrivalText = new TextObject("{=brief_baggage_arrived_today_sea}The ship's quartermaster has opened the hold. Men crowd around, looking for their kit.").ToString();
                    }
                    else
                    {
                        arrivalText = daysSinceArrival == 0
                            ? new TextObject("{=brief_baggage_arrived_today}The baggage wagons caught up with the column. Men crowd around, looking for their kit.").ToString()
                            : new TextObject("{=brief_baggage_arrived_recent}The wagons are still close. A chance to check your belongings before they fall behind again.").ToString();
                    }
                    return $"<span style=\"Success\">{arrivalText}</span>";
                }

                // Priority 5: Temporary access window
                if (currentAccess == BaggageAccessState.TemporaryAccess)
                {
                    var hoursRemaining = baggageManager.GetTemporaryAccessHoursRemaining();
                    if (hoursRemaining > 0)
                    {
                        // Use singular or plural phrasing
                        if (isAtSea)
                        {
                            if (hoursRemaining == 1)
                            {
                                return new TextObject("{=brief_baggage_temporary_sea}The ship has anchored. A few hours to access your belongings in the hold before we sail.").ToString();
                            }
                            var seaLine = new TextObject("{=brief_baggage_temporary_plural_sea}The ship has anchored. {HOURS} hours to access your belongings in the hold before we sail.");
                            seaLine.SetTextVariable("HOURS", hoursRemaining);
                            return seaLine.ToString();
                        }
                        else
                        {
                            if (hoursRemaining == 1)
                            {
                                return new TextObject("{=brief_baggage_temporary}The wagons have halted nearby. A few hours to rummage through your belongings before they move on.").ToString();
                            }
                            var landLine = new TextObject("{=brief_baggage_temporary_plural}The wagons have halted nearby. {HOURS} hours to rummage through your belongings before they move on.");
                            landLine.SetTextVariable("HOURS", hoursRemaining);
                            return landLine.ToString();
                        }
                    }
                }

                // Priority 6: On march/sail with no access (informational)
                if (currentAccess == BaggageAccessState.NoAccess && lordParty?.IsMoving == true)
                {
                    var marchText = isAtSea
                        ? new TextObject("{=brief_baggage_march_sea}Your belongings are stowed in the ship's hold, inaccessible during the voyage.").ToString()
                        : new TextObject("{=brief_baggage_march}The baggage wagons rumble somewhere behind the column, out of reach for now.").ToString();
                    return marchText;
                }

                // Normal state (FullAccess in settlement or halted) - no need to mention
                return string.Empty;
            }
            catch (Exception ex)
            {
                ModLogger.Debug(LogCategory, $"Error building baggage status: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Builds camp rumor hints for Company Reports.
        /// Social activities others are doing (card games, dice, drinking, etc.) that the player can join.
        /// Uses Rumor styling (muted lavender) to distinguish from alerts/warnings.
        /// </summary>
        private static string BuildCampRumorsLine()
        {
            try
            {
                // Use Orchestrator's pre-scheduled hints for stability (won't disappear mid-session)
                var orchestrator = ContentOrchestrator.Instance;
                IEnumerable<string> hints = null;

                if (orchestrator != null)
                {
                    hints = orchestrator.GetUpcomingHints()?.ToList();
                }

                // Fall back to generator if orchestrator not available
                if (hints == null || !hints.Any())
                {
                    var generator = CampOpportunityGenerator.Instance;
                    if (generator == null)
                    {
                        return string.Empty;
                    }
                    hints = generator.GetUpcomingHints()?.ToList();
                }

                if (hints == null || !hints.Any())
                {
                    return string.Empty;
                }

                // Filter for camp rumors (social activities - hints about others, not "Your...")
                var campRumors = hints
                    .Select(h => ResolveHintTokens(h))
                    .Where(h => !string.IsNullOrWhiteSpace(h) && !IsPersonalHint(h))
                    .Take(2)
                    .ToList();

                if (campRumors.Count == 0)
                {
                    ModLogger.Debug(LogCategory, "BuildCampRumorsLine: No camp rumors to display (all personal hints)");
                    return string.Empty;
                }

                ModLogger.Debug(LogCategory, $"BuildCampRumorsLine: {campRumors.Count} camp rumors displayed");

                // Apply Rumor styling to camp chatter
                return $"<span style=\"Rumor\">{string.Join(" ", campRumors)}</span>";
            }
            catch (Exception ex)
            {
                ModLogger.Debug(LogCategory, $"Error building camp rumors: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Builds personal opportunity hints for Your Status section.
        /// Player-specific hints (medical care, rest, personal activities).
        /// </summary>
        private static string BuildPersonalHintsLine()
        {
            try
            {
                // Use Orchestrator's pre-scheduled hints for stability (won't disappear mid-session)
                var orchestrator = ContentOrchestrator.Instance;
                IEnumerable<string> hints = null;

                if (orchestrator != null)
                {
                    hints = orchestrator.GetUpcomingHints()?.ToList();
                }

                // Fall back to generator if orchestrator not available
                if (hints == null || !hints.Any())
                {
                    var generator = CampOpportunityGenerator.Instance;
                    if (generator == null)
                    {
                        return string.Empty;
                    }
                    hints = generator.GetUpcomingHints()?.ToList();
                }

                if (hints == null || !hints.Any())
                {
                    return string.Empty;
                }

                // Filter for personal hints (things about the player)
                var personalHints = hints
                    .Select(h => ResolveHintTokens(h))
                    .Where(h => !string.IsNullOrWhiteSpace(h) && IsPersonalHint(h))
                    .Take(2)
                    .ToList();

                if (personalHints.Count == 0)
                {
                    ModLogger.Debug(LogCategory, "BuildPersonalHintsLine: No personal hints to display (all camp rumors)");
                    return string.Empty;
                }

                ModLogger.Debug(LogCategory, $"BuildPersonalHintsLine: {personalHints.Count} personal hints displayed");

                return string.Join(" ", personalHints);
            }
            catch (Exception ex)
            {
                ModLogger.Debug(LogCategory, $"Error building personal hints: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Determines if a hint is personal (about the player) vs. a camp rumor (about others).
        /// Personal hints start with "Your" or contain player-specific language.
        /// </summary>
        private static bool IsPersonalHint(string hint)
        {
            if (string.IsNullOrWhiteSpace(hint))
            {
                return false;
            }

            var lower = hint.ToLowerInvariant();
            
            // Personal hints typically start with "Your" or "You"
            if (lower.StartsWith("your ") || lower.StartsWith("you ") || lower.StartsWith("you'"))
            {
                return true;
            }

            // Medical/health-related hints are personal
            if (lower.Contains("condition") || lower.Contains("wound") || lower.Contains("injury") ||
                lower.Contains("pushing yourself") || lower.Contains("worsening"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Resolves placeholder tokens in hint text (e.g., {SOLDIER_NAME}, {COMRADE_NAME}).
        /// Uses the same token resolution system as events via TextObject.
        /// </summary>
        private static string ResolveHintTokens(string hint)
        {
            if (string.IsNullOrWhiteSpace(hint))
            {
                return hint;
            }

            try
            {
                // Create TextObject and set all common variables
                var textObject = new TextObject(hint);
                
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted == true)
                {
                    // NCO names
                    var ncoFullName = enlistment.NcoFullName ?? "the Sergeant";
                    textObject.SetTextVariable("SERGEANT", ncoFullName);
                    textObject.SetTextVariable("SERGEANT_NAME", ncoFullName);
                    textObject.SetTextVariable("NCO_NAME", ncoFullName);
                    
                    // Soldier names (randomized for variety)
                    textObject.SetTextVariable("SOLDIER_NAME", enlistment.GetRandomSoldierName());
                    textObject.SetTextVariable("COMRADE_NAME", enlistment.GetRandomSoldierName());
                    textObject.SetTextVariable("VETERAN_1_NAME", enlistment.GetRandomSoldierName());
                    textObject.SetTextVariable("VETERAN_2_NAME", enlistment.GetRandomSoldierName());
                    textObject.SetTextVariable("RECRUIT_NAME", enlistment.GetRandomSoldierName());
                    
                    // Officer names
                    textObject.SetTextVariable("OFFICER_NAME", ncoFullName);
                    
                    // Lord info
                    if (enlistment.EnlistedLord != null)
                    {
                        textObject.SetTextVariable("LORD_NAME", enlistment.EnlistedLord.Name?.ToString() ?? "the Lord");
                    }
                    
                    // Settlement info
                    var party = enlistment.EnlistedLord?.PartyBelongedTo;
                    if (party?.CurrentSettlement != null)
                    {
                        textObject.SetTextVariable("SETTLEMENT_NAME", party.CurrentSettlement.Name?.ToString() ?? "a settlement");
                    }
                    else
                    {
                        textObject.SetTextVariable("SETTLEMENT_NAME", "home");
                    }
                }
                
                return textObject.ToString();
            }
            catch
            {
                // Fallback: return hint as-is if token resolution fails
                return hint;
            }
        }

        /// <summary>
        /// Builds a recent event aftermath line for the Daily Brief.
        /// Shows flavor text for events that fired in the last 24 hours.
        /// </summary>
        private string BuildRecentEventLine()
        {
            try
            {
                _eventOutcomes ??= new List<EventOutcomeRecord>();

                var currentDay = (int)CampaignTime.Now.ToDays;
                var recentEvents = _eventOutcomes
                    .Where(e => currentDay - e.DayNumber <= 1)
                    .OrderByDescending(e => e.DayNumber)
                    .Take(1)
                    .ToList();

                if (recentEvents.Count == 0)
                {
                    return string.Empty;
                }

                var recent = recentEvents[0];
                var eventId = recent.EventId?.ToLowerInvariant() ?? string.Empty;

                // Dice/gambling aftermath
                if (eventId.Contains("dice") || eventId.Contains("gambling") || eventId.Contains("wager"))
                {
                    return new TextObject("{=brief_event_dice}The men still talk about your dice game — coin changed hands, and some are richer for it.").ToString();
                }

                // Training aftermath
                if (eventId.Contains("training") || eventId.Contains("drill") || eventId.Contains("practice"))
                {
                    return new TextObject("{=brief_event_training}Yesterday's training session left your arms sore and your muscles aching, but your skills are sharper for it.").ToString();
                }

                // Hunt aftermath
                if (eventId.Contains("hunt"))
                {
                    return new TextObject("{=brief_event_hunt}Fresh game from the hunt sizzles over the cookfires. The men eat well tonight.").ToString();
                }

                // Loan aftermath
                if (eventId.Contains("lend") || eventId.Contains("loan"))
                {
                    if (eventId.Contains("repay") || eventId.Contains("return"))
                    {
                        return string.Empty; // Repayment doesn't need aftermath
                    }
                    return new TextObject("{=brief_event_loan}A comrade owes you coin. He catches your eye across the camp and nods, remembering his debt.").ToString();
                }

                // Tavern aftermath
                if (eventId.Contains("tavern") || eventId.Contains("drinking"))
                {
                    return new TextObject("{=brief_event_tavern}Last night's drinking still echoes in your head. The ale was strong, and the songs were louder.").ToString();
                }

                // Battle loot aftermath
                if (eventId.Contains("loot") || eventId.Contains("battlefield"))
                {
                    return new TextObject("{=brief_event_loot}The spoils of battle weigh in your pack. Some men did well, others came away empty-handed.").ToString();
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                ModLogger.Debug(LogCategory, $"Error building recent event line: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Builds a pending chain events line for the Daily Brief.
        /// Shows context hints for upcoming events.
        /// </summary>
        private string BuildPendingEventsLine()
        {
            try
            {
                _pendingEvents ??= new List<PendingEventRecord>();

                if (_pendingEvents.Count == 0)
                {
                    return string.Empty;
                }

                var currentDay = (int)CampaignTime.Now.ToDays;
                var lines = new List<string>();

                foreach (var pending in _pendingEvents)
                {
                    var daysRemaining = pending.ScheduledDay - currentDay;
                    var chainId = pending.ChainEventId?.ToLowerInvariant() ?? string.Empty;

                    // Skip stale pending events (more than 7 days overdue)
                    if (daysRemaining < -7)
                    {
                        continue;
                    }

                    // Loan repayment pending
                    if (chainId.Contains("repay") || chainId.Contains("return") || chainId.Contains("debt"))
                    {
                        if (daysRemaining <= 1)
                        {
                            lines.Add(new TextObject("{=brief_pending_repay_due}A comrade promised to repay you today. You catch him avoiding your gaze across the camp.").ToString());
                        }
                        else
                        {
                            // Clamp to minimum 1 to avoid negative or zero display
                            var daysSince = Math.Max(1, currentDay - pending.CreatedDay);
                            var text = new TextObject("{=brief_pending_repay_waiting}A comrade owes you coin. It's been {DAYS} days, and you're starting to wonder if he'll make good on it.");
                            text.SetTextVariable("DAYS", daysSince);
                            lines.Add(text.ToString());
                        }
                    }
                    // Gratitude pending
                    else if (chainId.Contains("gratitude") || chainId.Contains("thank") || chainId.Contains("favor"))
                    {
                        lines.Add(new TextObject("{=brief_pending_gratitude}Someone in the company remembers your kindness. A favor owed, perhaps.").ToString());
                    }
                    // Revenge/grudge pending
                    else if (chainId.Contains("revenge") || chainId.Contains("grudge"))
                    {
                        lines.Add(new TextObject("{=brief_pending_grudge}You've made an enemy. Someone watches you with hard eyes when they think you're not looking.").ToString());
                    }
                    // Generic hint from the record
                    else if (!string.IsNullOrEmpty(pending.ContextHint))
                    {
                        lines.Add(pending.ContextHint);
                    }
                }

                // Return only the most relevant (first) to avoid spam
                return lines.Count > 0 ? lines[0] : string.Empty;
            }
            catch (Exception ex)
            {
                ModLogger.Debug(LogCategory, $"Error building pending events line: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Builds a flag-based context line for the Daily Brief.
        /// Shows personality hints based on active escalation flags.
        /// </summary>
        private string BuildFlagContextLine()
        {
            try
            {
                var state = EscalationManager.Instance?.State;
                if (state == null)
                {
                    return string.Empty;
                }

                var lines = new List<string>();

                if (state.HasFlag("has_helped_comrade") || state.HasFlag("helped_comrade"))
                {
                    lines.Add(new TextObject("{=brief_flag_helpful}Word has spread that you look out for your comrades. Men nod when you pass.").ToString());
                }
                if (state.HasFlag("dice_winner") || state.HasFlag("gambling_winner"))
                {
                    lines.Add(new TextObject("{=brief_flag_lucky}Your luck at dice is remembered. Some men avoid gambling with you; others can't resist trying their fortune.").ToString());
                }
                if (state.HasFlag("shared_winnings") || state.HasFlag("generous"))
                {
                    lines.Add(new TextObject("{=brief_flag_generous}The men appreciate your generosity. When you have coin, you share it, and that's not forgotten.").ToString());
                }
                if (state.HasFlag("officer_attention") || state.HasFlag("noticed_by_officers"))
                {
                    lines.Add(new TextObject("{=brief_flag_noticed}Officers have taken notice of you lately. Whether that's good or bad remains to be seen.").ToString());
                }
                if (state.HasFlag("training_focused") || state.HasFlag("dedicated_training"))
                {
                    lines.Add(new TextObject("{=brief_flag_training}Your dedication to training has been noted. The drill sergeant speaks well of your discipline.").ToString());
                }
                if (state.HasFlag("good_hunter") || state.HasFlag("skilled_hunter"))
                {
                    lines.Add(new TextObject("{=brief_flag_hunter}Your hunting skills are well regarded. When the company needs fresh meat, they look to you.").ToString());
                }
                if (state.HasFlag("drinks_with_soldiers") || state.HasFlag("tavern_regular"))
                {
                    lines.Add(new TextObject("{=brief_flag_drinker}You're known to drink with the common soldiers. They count you as one of their own.").ToString());
                }

                // Return one random context to avoid spam and add variety
                if (lines.Count > 0)
                {
                    var index = MBRandom.RandomInt(lines.Count);
                    return lines[index];
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                ModLogger.Debug(LogCategory, $"Error building flag context line: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Checks main combat skills for level-up progress and returns a hint if any skill is within 20% of leveling.
        /// Results are cached for 6 hours to avoid expensive recalculation.
        /// </summary>
        private string BuildSkillProgressLine()
        {
            try
            {
                // Use cached result if checked within last 6 hours
                var hoursSinceCheck = CampaignTime.Now.ToHours - _lastSkillProgressCheck.ToHours;
                if (hoursSinceCheck < 6.0f && !string.IsNullOrEmpty(_cachedSkillProgressLine))
                {
                    return _cachedSkillProgressLine;
                }

                var hero = Hero.MainHero;
                if (hero?.HeroDeveloper == null)
                {
                    _lastSkillProgressCheck = CampaignTime.Now;
                    _cachedSkillProgressLine = string.Empty;
                    return string.Empty;
                }

                var model = Campaign.Current?.Models?.CharacterDevelopmentModel;
                if (model == null)
                {
                    _lastSkillProgressCheck = CampaignTime.Now;
                    _cachedSkillProgressLine = string.Empty;
                    return string.Empty;
                }

                // Check main combat skills for level-up progress
                var combatSkills = new[]
                {
                    DefaultSkills.OneHanded,
                    DefaultSkills.TwoHanded,
                    DefaultSkills.Polearm,
                    DefaultSkills.Bow,
                    DefaultSkills.Crossbow
                };

                foreach (var skill in combatSkills)
                {
                    try
                    {
                        var skillValue = hero.GetSkillValue(skill);

                        // Skip skills at max level (330)
                        if (skillValue >= 330)
                        {
                            continue;
                        }

                        var xpProgress = hero.HeroDeveloper.GetSkillXpProgress(skill);
                        var xpNeeded = model.GetXpRequiredForSkillLevel(skillValue + 1) - model.GetXpRequiredForSkillLevel(skillValue);

                        // Skip if XP calculation is invalid (shouldn't happen in vanilla, but guards against mod edge cases)
                        if (xpNeeded <= 0)
                        {
                            continue;
                        }

                        // Check if within 20% of next level
                        if (xpProgress > xpNeeded * 0.8f)
                        {
                            _lastSkillProgressCheck = CampaignTime.Now;
                            var skillName = skill.Name.ToString();
                            _cachedSkillProgressLine = $"Your <span style=\"Success\">{skillName}</span> skill is nearly ready to advance.";
                            return _cachedSkillProgressLine;
                        }
                    }
                    catch (Exception ex)
                    {
                        ModLogger.Debug(LogCategory, $"Error checking skill progress for {(skill != null ? skill.Name.ToString() : "unknown")}: {ex.Message}");
                    }
                }

                // No skills close to level-up
                _lastSkillProgressCheck = CampaignTime.Now;
                _cachedSkillProgressLine = string.Empty;
                return string.Empty;
            }
            catch (Exception ex)
            {
                ModLogger.Debug(LogCategory, $"Error building skill progress line: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Returns the current "visible" kingdom dispatch items using the same rules as the enlisted_status menu.
        /// This is useful for other UIs (e.g. Camp Bulletin) that want the same cadence and dedupe behavior.
        /// </summary>
        public List<DispatchItem> GetVisibleKingdomFeedItems(int maxItems = 3)
        {
            try
            {
                if (_kingdomFeed == null || _kingdomFeed.Count == 0)
                {
                    return new List<DispatchItem>();
                }

                var currentDay = (int)CampaignTime.Now.ToDays;
                var candidates = _kingdomFeed
                    .Where(x =>
                        x.FirstShownDay < 0 ||
                        currentDay - x.FirstShownDay < Math.Max(1, x.MinDisplayDays))
                    .OrderByDescending(x => x.FirstShownDay >= 0 && currentDay - x.FirstShownDay < Math.Max(1, x.MinDisplayDays)) // sticky
                    .ThenByDescending(x => Math.Max(1, x.MinDisplayDays)) // important first
                    .ThenByDescending(x => x.DayCreated)
                    .ToList();

                var recentItems = candidates.Take(maxItems).ToList();

                // Mark newly-shown items so they remain visible for their minimum display window.
                for (var i = 0; i < recentItems.Count; i++)
                {
                    var item = recentItems[i];
                    if (item.FirstShownDay < 0 && !string.IsNullOrEmpty(item.StoryKey))
                    {
                        var idx = _kingdomFeed.FindIndex(x => x.StoryKey == item.StoryKey);
                        if (idx >= 0)
                        {
                            var updated = _kingdomFeed[idx];
                            updated.FirstShownDay = currentDay;
                            _kingdomFeed[idx] = updated;
                        }
                    }
                }

                return recentItems;
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Error selecting visible kingdom feed items", ex);
                return new List<DispatchItem>();
            }
        }

        /// <summary>
        /// Returns the current "visible" personal dispatch items using the same rules as the enlisted_activities menu.
        /// </summary>
        public List<DispatchItem> GetVisiblePersonalFeedItems(int maxItems = 2)
        {
            try
            {
                if (_personalFeed == null || _personalFeed.Count == 0)
                {
                    return new List<DispatchItem>();
                }

                var currentDay = (int)CampaignTime.Now.ToDays;
                var candidates = _personalFeed
                    .Where(x =>
                        x.FirstShownDay < 0 ||
                        currentDay - x.FirstShownDay < Math.Max(1, x.MinDisplayDays))
                    .OrderByDescending(x => x.FirstShownDay >= 0 && currentDay - x.FirstShownDay < Math.Max(1, x.MinDisplayDays)) // sticky
                    .ThenByDescending(x => Math.Max(1, x.MinDisplayDays))
                    .ThenByDescending(x => x.DayCreated)
                    .ToList();

                var recentItems = candidates.Take(maxItems).ToList();

                // Mark newly-shown items so they remain visible for their minimum display window.
                for (var i = 0; i < recentItems.Count; i++)
                {
                    var item = recentItems[i];
                    if (item.FirstShownDay < 0 && !string.IsNullOrEmpty(item.StoryKey))
                    {
                        var idx = _personalFeed.FindIndex(x => x.StoryKey == item.StoryKey);
                        if (idx >= 0)
                        {
                            var updated = _personalFeed[idx];
                            updated.FirstShownDay = currentDay;
                            _personalFeed[idx] = updated;
                        }
                    }
                }

                return recentItems;
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Error selecting visible personal feed items", ex);
                return new List<DispatchItem>();
            }
        }

        /// <summary>
        /// Formats a dispatch item into display text using the same localization/template rules used by the menus.
        /// </summary>
        /// <param name="item">The dispatch item to format.</param>
        /// <param name="includeColor">If true, wraps the text in color style spans based on severity.</param>
        /// <returns>Formatted text, optionally with color styling.</returns>
        public static string FormatDispatchForDisplay(DispatchItem item, bool includeColor = true)
        {
            var text = FormatDispatchItem(item);

            if (includeColor && item.Severity > 0 && !string.IsNullOrWhiteSpace(text))
            {
                var style = GetColorStyleForSeverity((NewsSeverity)item.Severity);
                return $"<span style=\"{style}\">{text}</span>";
            }

            return text;
        }

        /// <summary>
        /// Maps a NewsSeverity value to the corresponding color style name from EnlistedColors.xml.
        /// </summary>
        /// <param name="severity">The severity level.</param>
        /// <returns>The style name for use in span tags.</returns>
        private static string GetColorStyleForSeverity(NewsSeverity severity)
        {
            return severity switch
            {
                NewsSeverity.Critical => "Critical",  // Bright Red with outline (#FF0000FF)
                NewsSeverity.Urgent => "Alert",       // Soft Red (#FF6B6BFF)
                NewsSeverity.Attention => "Warning",  // Gold (#FFD700FF)
                NewsSeverity.Positive => "Success",   // Light Green (#90EE90FF)
                NewsSeverity.Normal => "Default",     // Warm Cream (#FAF4DEFF)
                _ => "Default"
            };
        }

        /// <summary>
        /// Builds the kingdom news section for display in enlisted_status menu.
        /// </summary>
        /// <param name="maxItems">Maximum headlines to display (default 3).</param>
        /// <returns>Formatted news section text, or empty string if no news.</returns>
        public string BuildKingdomNewsSection(int maxItems = 3)
        {
            try
            {
                if (_kingdomFeed == null || _kingdomFeed.Count == 0)
                {
                    return string.Empty;
                }

                // Select which items are "currently visible" based on priority and backlog rules. Every item stays
                // visible for at least one day once shown. Important items stay visible for two days. An item can be
                // replaced by a newer update with the same StoryKey.
                var currentDay = (int)CampaignTime.Now.ToDays;
                var candidates = _kingdomFeed
                    .Where(x =>
                        x.FirstShownDay < 0 ||
                        currentDay - x.FirstShownDay < Math.Max(1, x.MinDisplayDays))
                    .OrderByDescending(x => x.FirstShownDay >= 0 && currentDay - x.FirstShownDay < Math.Max(1, x.MinDisplayDays)) // sticky
                    .ThenByDescending(x => Math.Max(1, x.MinDisplayDays)) // important first
                    .ThenByDescending(x => x.DayCreated)
                    .ToList();

                var recentItems = candidates.Take(maxItems).ToList();

                if (recentItems.Count == 0)
                {
                    return string.Empty;
                }

                // Mark newly-shown items so they remain visible for their minimum display window.
                for (var i = 0; i < recentItems.Count; i++)
                {
                    var item = recentItems[i];
                    if (item.FirstShownDay < 0 && !string.IsNullOrEmpty(item.StoryKey))
                    {
                        var idx = _kingdomFeed.FindIndex(x => x.StoryKey == item.StoryKey);
                        if (idx >= 0)
                        {
                            var updated = _kingdomFeed[idx];
                            updated.FirstShownDay = currentDay;
                            _kingdomFeed[idx] = updated;
                        }
                    }
                }

                var sb = new StringBuilder();
                sb.AppendLine();
                sb.AppendLine(new TextObject("{=News_SectionHeader_Kingdom}--- Kingdom News ---").ToString());

                foreach (var item in recentItems)
                {
                    var headline = FormatDispatchItem(item);
                    if (!string.IsNullOrEmpty(headline))
                    {
                        sb.AppendLine($"- {headline}");
                    }
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Error building kingdom news section", ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// Builds the personal news section for display in enlisted_activities menu.
        /// </summary>
        /// <param name="maxItems">Maximum headlines to display (default 2).</param>
        /// <returns>Formatted news section text, or empty string if no news.</returns>
        public string BuildPersonalNewsSection(int maxItems = 2)
        {
            try
            {
                if (_personalFeed == null || _personalFeed.Count == 0)
                {
                    return string.Empty;
                }

                var currentDay = (int)CampaignTime.Now.ToDays;
                var candidates = _personalFeed
                    .Where(x =>
                        x.FirstShownDay < 0 ||
                        currentDay - x.FirstShownDay < Math.Max(1, x.MinDisplayDays))
                    .OrderByDescending(x => x.FirstShownDay >= 0 && currentDay - x.FirstShownDay < Math.Max(1, x.MinDisplayDays)) // sticky
                    .ThenByDescending(x => Math.Max(1, x.MinDisplayDays))
                    .ThenByDescending(x => x.DayCreated)
                    .ToList();

                var recentItems = candidates.Take(maxItems).ToList();

                if (recentItems.Count == 0)
                {
                    return string.Empty;
                }

                // Mark newly-shown items.
                for (var i = 0; i < recentItems.Count; i++)
                {
                    var item = recentItems[i];
                    if (item.FirstShownDay < 0 && !string.IsNullOrEmpty(item.StoryKey))
                    {
                        var idx = _personalFeed.FindIndex(x => x.StoryKey == item.StoryKey);
                        if (idx >= 0)
                        {
                            var updated = _personalFeed[idx];
                            updated.FirstShownDay = currentDay;
                            _personalFeed[idx] = updated;
                        }
                    }
                }

                var sb = new StringBuilder();
                sb.AppendLine(new TextObject("{=News_SectionHeader_Personal}--- Army Orders ---").ToString());

                foreach (var item in recentItems)
                {
                    var headline = FormatDispatchItem(item);
                    if (!string.IsNullOrEmpty(headline))
                    {
                        sb.AppendLine($"- {headline}");
                    }
                }

                sb.AppendLine(); // Extra line before camp activities

                return sb.ToString();
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Error building personal news section", ex);
                return string.Empty;
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Daily tick: check for bulletin generation and army formation detection.
        /// </summary>
        private void OnDailyTick()
        {
            try
            {
                if (!IsEnlisted())
                {
                    // Reset army tracking when not enlisted
                    _lordHadArmyYesterday = false;
                    return;
                }

                // Generate the daily brief once per day while enlisted so the main menu can display it without jitter.
                EnsureDailyBriefGenerated();

                // Generate the Daily Report once per in-game day (persisted, stable).
                EnsureDailyReportGenerated();

                // Clean up stale pending events that never fired
                CleanupStalePendingEvents();

                // Process event outcome queue (promote next queued outcome if current one expired)
                ProcessEventOutcomeQueue();

                CheckForArmyFormation();
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Error in OnDailyTick", ex);
            }
        }

        /// <summary>
        /// Removes pending events that are more than 7 days overdue.
        /// Handles cases where chain events were scheduled but never fired
        /// (e.g., event definition removed, mod conflict, or game state issue).
        /// </summary>
        private void CleanupStalePendingEvents()
        {
            try
            {
                _pendingEvents ??= new List<PendingEventRecord>();

                if (_pendingEvents.Count == 0)
                {
                    return;
                }

                var currentDay = (int)CampaignTime.Now.ToDays;
                var staleThreshold = 7; // Remove events more than 7 days overdue

                var removed = _pendingEvents.RemoveAll(p => currentDay - p.ScheduledDay > staleThreshold);

                if (removed > 0)
                {
                    ModLogger.Info(LogCategory, $"Cleaned up {removed} stale pending event(s)");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Debug(LogCategory, $"Error cleaning up stale pending events: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes the event outcome queue system.
        /// Checks if the currently shown outcome has been visible for 1 day, and if so,
        /// removes it from display and promotes the next queued outcome.
        /// This ensures only one event outcome is shown in Recent Activities at a time.
        /// </summary>
        private void ProcessEventOutcomeQueue()
        {
            try
            {
                _eventOutcomes ??= new List<EventOutcomeRecord>();

                if (_eventOutcomes.Count == 0)
                {
                    return;
                }

                var currentDay = (int)CampaignTime.Now.ToDays;
                var currentOutcome = _eventOutcomes.FirstOrDefault(e => e.IsCurrentlyShown);

                if (currentOutcome != null)
                {
                    // Check if the current outcome has been shown for 1 full day
                    var daysShown = currentDay - currentOutcome.DayShown;
                    if (daysShown >= 1)
                    {
                        // Current outcome expired, clear it from display
                        currentOutcome.IsCurrentlyShown = false;
                        ModLogger.Debug(LogCategory, $"Event outcome expired: {currentOutcome.EventId}");

                        // Remove the expired outcome's feed entry
                        var storyKey = $"event:{currentOutcome.EventId}:{currentOutcome.DayNumber}";
                        _personalFeed?.RemoveAll(item => item.StoryKey == storyKey);

                        // Promote next queued outcome if any
                        var nextQueuedOutcome = _eventOutcomes.FirstOrDefault(e => !e.IsCurrentlyShown && e.DayShown < 0);
                        if (nextQueuedOutcome != null)
                        {
                            nextQueuedOutcome.IsCurrentlyShown = true;
                            nextQueuedOutcome.DayShown = currentDay;
                            ModLogger.Info(LogCategory, $"Promoted queued event outcome: {nextQueuedOutcome.EventId}");

                            // Add it to the personal feed
                            string displayText;
                            if (!string.IsNullOrWhiteSpace(nextQueuedOutcome.ResultNarrative))
                            {
                                displayText = $"{nextQueuedOutcome.EventTitle}: {nextQueuedOutcome.ResultNarrative}";
                            }
                            else
                            {
                                var headline = BuildEventHeadline(nextQueuedOutcome);
                                var placeholders = new Dictionary<string, string>
                                {
                                    { "EVENT_TITLE", nextQueuedOutcome.EventTitle ?? nextQueuedOutcome.EventId },
                                    { "OPTION", nextQueuedOutcome.OptionChosen ?? string.Empty },
                                    { "EFFECTS", nextQueuedOutcome.OutcomeSummary ?? string.Empty }
                                };

                                foreach (var kvp in placeholders)
                                {
                                    headline = headline.Replace($"{{{kvp.Key}}}", kvp.Value);
                                }
                                displayText = headline;
                            }

                            AddPersonalNews("event", displayText, null, $"event:{nextQueuedOutcome.EventId}:{nextQueuedOutcome.DayNumber}", 1, nextQueuedOutcome.Severity);
                        }
                    }
                }
                else
                {
                    // No outcome currently shown, check if we have queued outcomes to promote
                    var nextQueuedOutcome = _eventOutcomes.FirstOrDefault(e => !e.IsCurrentlyShown && e.DayShown < 0);
                    if (nextQueuedOutcome != null)
                    {
                        nextQueuedOutcome.IsCurrentlyShown = true;
                        nextQueuedOutcome.DayShown = currentDay;
                        ModLogger.Info(LogCategory, $"Promoted queued event outcome (no current): {nextQueuedOutcome.EventId}");

                        // Add it to the personal feed
                        string displayText;
                        if (!string.IsNullOrWhiteSpace(nextQueuedOutcome.ResultNarrative))
                        {
                            displayText = $"{nextQueuedOutcome.EventTitle}: {nextQueuedOutcome.ResultNarrative}";
                        }
                        else
                        {
                            var headline = BuildEventHeadline(nextQueuedOutcome);
                            var placeholders = new Dictionary<string, string>
                            {
                                { "EVENT_TITLE", nextQueuedOutcome.EventTitle ?? nextQueuedOutcome.EventId },
                                { "OPTION", nextQueuedOutcome.OptionChosen ?? string.Empty },
                                { "EFFECTS", nextQueuedOutcome.OutcomeSummary ?? string.Empty }
                            };

                            foreach (var kvp in placeholders)
                            {
                                headline = headline.Replace($"{{{kvp.Key}}}", kvp.Value);
                            }
                            displayText = headline;
                        }

                        AddPersonalNews("event", displayText, null, $"event:{nextQueuedOutcome.EventId}:{nextQueuedOutcome.DayNumber}", 1, nextQueuedOutcome.Severity);
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Error processing event outcome queue", ex);
            }
        }

        /// <summary>
        /// Ensure we have a persisted Daily Report for the current day.
        /// </summary>
        private void EnsureDailyReportGenerated()
        {
            try
            {
                var dayNumber = (int)CampaignTime.Now.ToDays;

                _campNewsState ??= new CampNewsState();
                _campNewsState.EnsureInitialized();

                if (_campNewsState.LastGeneratedDayNumber == dayNumber &&
                    _campNewsState.TryGetReportForDay(dayNumber) != null)
                {
                    return;
                }

                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    _campNewsState.MarkGenerated(dayNumber);
                    return;
                }

                var snapshot = BuildDailyReportSnapshot(enlistment, dayNumber, out var aggregate, out var context);

                // Cache snapshot for other systems that need day facts.
                _lastDailyReportSnapshot = snapshot;

                // Generate final report lines (templated strings) and persist them.
                var lines = DailyReportGenerator.Generate(snapshot, context, maxLines: 8);

                var record = new DailyReportRecord
                {
                    HasValue = true,
                    DayNumber = dayNumber,
                    Lines = lines?.ToArray() ?? Array.Empty<string>()
                };

                _campNewsState.AppendReport(record);
                _campNewsState.MarkGenerated(dayNumber);

                // Update ledger (best-effort; mostly zero until later producers).
                if (aggregate.HasValue)
                {
                    _campNewsState.Ledger.RecordDay(aggregate);
                }

                ModLogger.Debug(LogCategory, $"Daily Report generated for day {dayNumber} (lines={record.Lines.Length})");
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "EnsureDailyReportGenerated failed", ex);
            }
        }

        /// <summary>
        /// Provides the current day's Daily Report snapshot (facts), ensuring it exists first.
        /// Intended for systems that need the same "day facts" without duplicating producer logic.
        /// </summary>
        public DailyReportSnapshot GetTodayDailyReportSnapshot()
        {
            try
            {
                EnsureDailyReportGenerated();
                return _lastDailyReportSnapshot;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Returns rolling totals from the camp life ledger for the specified window.
        /// Useful for displaying recent losses, wounded, sick over time.
        /// </summary>
        public CampLifeRollingTotals GetRollingTotals(int windowDays = 7)
        {
            try
            {
                _campNewsState?.EnsureInitialized();
                return _campNewsState?.Ledger?.GetRollingTotals(windowDays)
                       ?? new CampLifeRollingTotals(0, 0, 0, 0, 0, 0);
            }
            catch
            {
                return new CampLifeRollingTotals(0, 0, 0, 0, 0, 0);
            }
        }

        /// <summary>
        /// Soldiers lost since the last muster cycle. Resets when muster completes.
        /// </summary>
        public int LostSinceLastMuster => _lostSinceLastMuster;

        /// <summary>
        /// Soldiers who fell sick since the last muster cycle. Resets when muster completes.
        /// </summary>
        public int SickSinceLastMuster => _sickSinceLastMuster;

        /// <summary>
        /// Resets the "since last muster" counters. Called by EnlistmentBehavior.OnMusterCycleComplete().
        /// </summary>
        public void ResetMusterCounters()
        {
            _lostSinceLastMuster = 0;
            _sickSinceLastMuster = 0;
            ModLogger.Debug(LogCategory, "Muster counters reset");
        }

        /// <summary>
        /// Records an order outcome for display in daily brief, detailed reports, and Recent Activities.
        /// Uses the same queue system as event outcomes to show one item at a time.
        /// </summary>
        public void AddOrderOutcome(string orderTitle, bool success, string briefSummary,
            string detailedSummary, string issuer, int dayNumber)
        {
            try
            {
                _orderOutcomes ??= new List<OrderOutcomeRecord>();

                _orderOutcomes.Add(new OrderOutcomeRecord
                {
                    OrderTitle = orderTitle ?? string.Empty,
                    Success = success,
                    BriefSummary = briefSummary ?? string.Empty,
                    DetailedSummary = detailedSummary ?? string.Empty,
                    Issuer = issuer ?? string.Empty,
                    DayNumber = dayNumber
                });

                // Keep only last 10 order outcomes
                if (_orderOutcomes.Count > 10)
                {
                    _orderOutcomes.RemoveAt(0);
                }

                // Add order outcome to personal feed for Recent Activities display
                // Use brief title with success/failure indicator and detailed summary as body
                var resultPrefix = success ? "✓" : "✗";
                var displayText = $"{resultPrefix} {orderTitle}: {detailedSummary}";
                var severity = success ? 1 : 2; // Positive for success, Attention for failure

                // Add to personal feed with 1-day display duration
                AddPersonalNews("order", displayText, null, $"order:{orderTitle}:{dayNumber}", 1, severity);

                ModLogger.Debug(LogCategory, $"Order outcome recorded: {orderTitle} (success={success}, day={dayNumber})");
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to add order outcome", ex);
            }
        }

        /// <summary>
        /// Records a reputation change for display in reports.
        /// </summary>
        public void AddReputationChange(string target, int delta, int newValue,
            string message, int dayNumber)
        {
            try
            {
                _reputationChanges ??= new List<ReputationChangeRecord>();

                _reputationChanges.Add(new ReputationChangeRecord
                {
                    Target = target ?? string.Empty,
                    Delta = delta,
                    NewValue = newValue,
                    Message = message ?? string.Empty,
                    DayNumber = dayNumber
                });

                // Keep only last 10 reputation changes
                if (_reputationChanges.Count > 10)
                {
                    _reputationChanges.RemoveAt(0);
                }

                ModLogger.Debug(LogCategory, $"Reputation change recorded: {target} {delta:+#;-#;0} (day={dayNumber})");
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to add reputation change", ex);
            }
        }

        /// <summary>
        /// Records a company need change for display in reports.
        /// </summary>
        public void AddCompanyNeedChange(string need, int delta, int oldValue, int newValue,
            string message, int dayNumber)
        {
            try
            {
                _companyNeedChanges ??= new List<CompanyNeedChangeRecord>();

                _companyNeedChanges.Add(new CompanyNeedChangeRecord
                {
                    Need = need ?? string.Empty,
                    Delta = delta,
                    OldValue = oldValue,
                    NewValue = newValue,
                    Message = message ?? string.Empty,
                    DayNumber = dayNumber
                });

                // Keep only last 10 need changes
                if (_companyNeedChanges.Count > 10)
                {
                    _companyNeedChanges.RemoveAt(0);
                }

                ModLogger.Debug(LogCategory, $"Company need change recorded: {need} {delta:+#;-#;0} (day={dayNumber})");
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to add company need change", ex);
            }
        }

        /// <summary>
        /// Returns recent order outcomes within the specified number of days.
        /// </summary>
        public List<OrderOutcomeRecord> GetRecentOrderOutcomes(int maxDaysOld = 3)
        {
            try
            {
                _orderOutcomes ??= new List<OrderOutcomeRecord>();

                var currentDay = (int)CampaignTime.Now.ToDays;
                return _orderOutcomes
                    .Where(o => currentDay - o.DayNumber <= maxDaysOld)
                    .OrderByDescending(o => o.DayNumber)
                    .ToList();
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to get recent order outcomes", ex);
                return new List<OrderOutcomeRecord>();
            }
        }

        /// <summary>
        /// Returns recent reputation changes within the specified number of days.
        /// </summary>
        public List<ReputationChangeRecord> GetRecentReputationChanges(int maxDaysOld = 3)
        {
            try
            {
                _reputationChanges ??= new List<ReputationChangeRecord>();

                var currentDay = (int)CampaignTime.Now.ToDays;
                return _reputationChanges
                    .Where(r => currentDay - r.DayNumber <= maxDaysOld)
                    .OrderByDescending(r => r.DayNumber)
                    .ToList();
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to get recent reputation changes", ex);
                return new List<ReputationChangeRecord>();
            }
        }

        /// <summary>
        /// Returns recent company need changes within the specified number of days.
        /// </summary>
        public List<CompanyNeedChangeRecord> GetRecentCompanyNeedChanges(int maxDaysOld = 3)
        {
            try
            {
                _companyNeedChanges ??= new List<CompanyNeedChangeRecord>();

                var currentDay = (int)CampaignTime.Now.ToDays;
                return _companyNeedChanges
                    .Where(c => currentDay - c.DayNumber <= maxDaysOld)
                    .OrderByDescending(c => c.DayNumber)
                    .ToList();
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to get recent company need changes", ex);
                return new List<CompanyNeedChangeRecord>();
            }
        }

        /// <summary>
        /// Builds the KINGDOM section for the Main Menu (Phase 5 - UI Integration).
        /// Shows high-level kingdom news including war status, sieges, and major events.
        /// </summary>
        public string BuildKingdomSummary()
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true || enlistment.CurrentLord?.Clan?.Kingdom == null)
                {
                    return string.Empty;
                }

                var kingdom = enlistment.CurrentLord.Clan.Kingdom;
                var sb = new StringBuilder();

                // Check for active wars - simplified
                var isAtWar = FactionManager.IsAtWarAgainstFaction(kingdom, null);
                if (isAtWar)
                {
                    sb.Append(new TextObject("{=menu_kingdom_at_war}{KINGDOM} at war with {ENEMY}. Conflict continues.")
                        .SetTextVariable("KINGDOM", kingdom.Name?.ToString() ?? "Unknown")
                        .SetTextVariable("ENEMY", "enemies")
                        .ToString());
                }
                else
                {
                    sb.Append(new TextObject("{=menu_kingdom_peace}The realm is at peace.").ToString());
                }

                // Check for ongoing sieges
                var lord = enlistment.CurrentLord;
                if (lord?.PartyBelongedTo?.SiegeEvent != null)
                {
                    var settlement = lord.PartyBelongedTo.SiegeEvent.BesiegedSettlement;
                    sb.Append(" ");
                    sb.Append(new TextObject("{=menu_kingdom_siege}{KINGDOM} besieging {SETTLEMENT}.")
                        .SetTextVariable("KINGDOM", kingdom.Name?.ToString() ?? "Unknown")
                        .SetTextVariable("SETTLEMENT", settlement?.Name?.ToString() ?? "Unknown")
                        .ToString());
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to build kingdom summary", ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// Builds the CAMP section for the Main Menu (Phase 5 - UI Integration).
        /// Shows immediate camp activities, atmosphere, and time-of-day flavor.
        /// </summary>
        public string BuildCampSummary()
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    return string.Empty;
                }

                var sb = new StringBuilder();

                // Time-of-day flavor (simplified - use hour of day)
                var currentHour = (int)CampaignTime.Now.CurrentHourInDay;
                string timePhrase;
                
                if (currentHour >= 6 && currentHour < 12)
                {
                    timePhrase = new TextObject("{=menu_camp_morning}Morning bustle. Camp coming alive.").ToString();
                }
                else if (currentHour >= 12 && currentHour < 18)
                {
                    timePhrase = new TextObject("{=menu_camp_midday}Midday heat. Most resting in shade.").ToString();
                }
                else if (currentHour >= 18 && currentHour < 22)
                {
                    timePhrase = new TextObject("{=menu_camp_evening}Evening calm. Good spirits in camp.").ToString();
                }
                else
                {
                    timePhrase = new TextObject("{=menu_camp_night}Night watch. Camp quiet.").ToString();
                }

                sb.Append(timePhrase);

                // Add a second sentence about camp activities (simplified for now)
                // Future: integrate with CampOpportunityGenerator for actual upcoming activities
                var activityFlavor = GetCampActivityFlavor();
                if (!string.IsNullOrEmpty(activityFlavor))
                {
                    sb.Append(" ");
                    sb.Append(activityFlavor);
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to build camp summary", ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// Gets a flavor sentence about camp activities from the ContentOrchestrator's scheduled opportunities.
        /// </summary>
        private string GetCampActivityFlavor()
        {
            try
            {
                // Use Orchestrator's pre-scheduled opportunities (the source of truth)
                var orchestrator = Content.ContentOrchestrator.Instance;
                if (orchestrator == null)
                {
                    return string.Empty;
                }

                // Get current phase opportunities from the Orchestrator
                var scheduledOpps = orchestrator.GetCurrentPhaseOpportunities();
                if (scheduledOpps == null || scheduledOpps.Count == 0)
                {
                    return string.Empty;
                }

                // Return the first opportunity's hint or description as camp flavor
                var firstOpp = scheduledOpps[0];
                
                // Prefer the narrative hint if available
                if (!string.IsNullOrEmpty(firstOpp.NarrativeHint))
                {
                    return firstOpp.NarrativeHint;
                }

                // Fall back to display name
                if (!string.IsNullOrEmpty(firstOpp.DisplayName))
                {
                    return firstOpp.DisplayName;
                }

                // Final fallback: describe by source opportunity type
                var sourceOpp = firstOpp.SourceOpportunity;
                if (sourceOpp != null)
                {
                    return sourceOpp.Type switch
                    {
                        Camp.Models.OpportunityType.Training => new TextObject("{=menu_camp_drilling}Veterans drilling by the wagons.").ToString(),
                        Camp.Models.OpportunityType.Social => new TextObject("{=menu_camp_cards}Card game forming tonight by the fire.").ToString(),
                        Camp.Models.OpportunityType.Economic => new TextObject("{=menu_camp_trading}Some trading happening in camp.").ToString(),
                        Camp.Models.OpportunityType.Recovery => new TextObject("{=menu_camp_rest}A quiet moment in camp.").ToString(),
                        _ => string.Empty
                    };
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                Mod.Core.Logging.ModLogger.Debug("News", $"GetCampActivityFlavor failed: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Builds the CAMP STATUS section for the Camp Hub menu (Phase 5 - UI Integration).
        /// Provides transparency about the orchestrator's current assessment of camp rhythm and activity level.
        /// </summary>
        public string BuildCampStatusSection()
        {
            try
            {
                var worldSituation = Content.WorldStateAnalyzer.AnalyzeSituation();
                var sb = new StringBuilder();

                sb.AppendLine("<span style=\"Header\">_____ CAMP STATUS _____</span>");

                // Rhythm icon and activity level
                var rhythmIcon = worldSituation.ExpectedActivity switch
                {
                    Content.Models.ActivityLevel.Quiet => "⚙️",
                    Content.Models.ActivityLevel.Routine => "🏹",
                    Content.Models.ActivityLevel.Active => "⚔️",
                    Content.Models.ActivityLevel.Intense => "⚠️",
                    _ => "⚙️"
                };

                var activityName = worldSituation.ExpectedActivity.ToString();
                sb.AppendLine($"{rhythmIcon} {activityName}");
                sb.AppendLine();

                // Narrative explaining the situation
                var narrative = BuildOrchestratorNarrative(worldSituation);
                sb.AppendLine(narrative);

                return sb.ToString();
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to build camp status section", ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// Builds a 1-2 sentence narrative explaining the orchestrator's current world state assessment.
        /// </summary>
        private string BuildOrchestratorNarrative(Content.Models.WorldSituation situation)
        {
            try
            {
                // Garrison + Peacetime = Quiet
                if (situation.LordIs == Content.Models.LordSituation.PeacetimeGarrison)
                {
                    return "Your lord holds at a settlement with no threats on the horizon. " +
                           "The camp has settled into routine. Little disturbs the daily rhythm.";
                }

                // Siege operations
                if (situation.LordIs == Content.Models.LordSituation.SiegeAttacking || 
                    situation.LordIs == Content.Models.LordSituation.SiegeDefending)
                {
                    return "Siege operations demand constant vigilance. Every hour brings new demands. " +
                           "The pressure never lets up.";
                }

                // Campaign operations
                if (situation.LordIs == Content.Models.LordSituation.WarActiveCampaign || 
                    situation.LordIs == Content.Models.LordSituation.WarMarching)
                {
                    return "The army marches to war. Your sergeants manage the daily needs of soldiers on campaign.";
                }

                // Default
                return "Camp operations continue.";
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to build orchestrator narrative", ex);
                return "Camp operations continue.";
            }
        }

        /// <summary>
        /// Builds the RECENT ACTIONS section for the Camp Hub menu (Phase 5 - UI Integration).
        /// Merges detailed order outcomes and reputation changes from the old Reports menu.
        /// </summary>
        public string BuildRecentActionsSection()
        {
            try
            {
                var parts = new List<string>();
                var maxDays = 5;

                // Get recent order outcomes
                var orderOutcomes = GetRecentOrderOutcomes(maxDays);
                foreach (var outcome in orderOutcomes)
                {
                    var timeAgo = GetTimeAgoText(outcome.DayNumber);
                    parts.Add($"• {outcome.OrderTitle} ({timeAgo})");

                    if (!string.IsNullOrEmpty(outcome.DetailedSummary))
                    {
                        parts.Add($"  {outcome.DetailedSummary}");
                    }
                }

                // Get recent reputation changes
                var repChanges = GetRecentReputationChanges(maxDays);
                foreach (var change in repChanges)
                {
                    var timeAgo = GetTimeAgoText(change.DayNumber);
                    var sign = change.Delta >= 0 ? "+" : "";
                    parts.Add($"• {change.Target} reputation {sign}{change.Delta} ({timeAgo})");

                    if (!string.IsNullOrEmpty(change.Message))
                    {
                        parts.Add($"  {change.Message}");
                    }
                }

                if (parts.Count == 0)
                {
                    return string.Empty;
                }

                return string.Join("\n", parts);
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to build recent actions section", ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// Gets a human-readable "time ago" string from a day number.
        /// </summary>
        private string GetTimeAgoText(int dayNumber)
        {
            var currentDay = (int)CampaignTime.Now.ToDays;
            var daysAgo = currentDay - dayNumber;

            if (daysAgo == 0) return "today";
            if (daysAgo == 1) return "yesterday";
            return $"{daysAgo} days ago";
        }

        /// <summary>
        /// Records an event outcome after an event popup is resolved.
        /// Adds to Personal Feed with a formatted headline showing effects.
        /// </summary>
        public void AddEventOutcome(EventOutcomeRecord outcome)
        {
            try
            {
                if (outcome == null)
                {
                    ModLogger.Warn(LogCategory, "Attempted to add null event outcome");
                    return;
                }

                _eventOutcomes ??= new List<EventOutcomeRecord>();

                // Check for duplicate (same event on same day)
                var existingIndex = _eventOutcomes.FindIndex(e =>
                    e.EventId == outcome.EventId && e.DayNumber == outcome.DayNumber);
                if (existingIndex >= 0)
                {
                    ModLogger.Debug(LogCategory, $"Event outcome already recorded: {outcome.EventId} day {outcome.DayNumber}");
                    return;
                }

                // Queue-based display: only one event outcome visible at a time
                // Check if there's already an outcome being shown
                var currentlyShownOutcome = _eventOutcomes.FirstOrDefault(e => e.IsCurrentlyShown);

                if (currentlyShownOutcome == null)
                {
                    // No outcome currently shown, so show this one immediately
                    outcome.IsCurrentlyShown = true;
                    outcome.DayShown = (int)CampaignTime.Now.ToDays;
                    ModLogger.Debug(LogCategory, $"Event outcome shown immediately: {outcome.EventId}");
                }
                else
                {
                    // Another outcome is already showing, queue this one for later
                    outcome.IsCurrentlyShown = false;
                    outcome.DayShown = -1; // Not shown yet
                    ModLogger.Debug(LogCategory, $"Event outcome queued: {outcome.EventId}");
                }

                _eventOutcomes.Add(outcome);

                // Keep only last 20 event outcomes
                if (_eventOutcomes.Count > 20)
                {
                    _eventOutcomes.RemoveAt(0);
                }

                // Add to personal feed only if this outcome is currently shown
                // Skip onboarding events (baggage check) as they're one-time setup that shouldn't clutter news
                var isOnboarding = outcome.EventId != null && outcome.EventId.StartsWith("evt_bagcheck_", StringComparison.OrdinalIgnoreCase);
                
                if (outcome.IsCurrentlyShown && !isOnboarding)
                {
                    // Build display text with narrative for immersive RP
                    string displayText;
                    if (!string.IsNullOrWhiteSpace(outcome.ResultNarrative))
                    {
                        displayText = $"{outcome.EventTitle}: {outcome.ResultNarrative}";
                    }
                    else
                    {
                        // Fallback to headline system for backwards compatibility
                        var headline = BuildEventHeadline(outcome);
                        var placeholders = new Dictionary<string, string>
                        {
                            { "EVENT_TITLE", outcome.EventTitle ?? outcome.EventId },
                            { "OPTION", outcome.OptionChosen ?? string.Empty },
                            { "EFFECTS", outcome.OutcomeSummary ?? string.Empty }
                        };

                        foreach (var kvp in placeholders)
                        {
                            headline = headline.Replace($"{{{kvp.Key}}}", kvp.Value);
                        }
                        displayText = headline;
                    }

                    var severity = outcome.Severity;
                    // Display for 1 day, then the queue system will promote the next outcome
                    AddPersonalNews("event", displayText, null, $"event:{outcome.EventId}:{outcome.DayNumber}", 1, severity);
                }

                ModLogger.Info(LogCategory, $"Event outcome recorded: {outcome.EventId} - {outcome.OptionChosen}");
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to add event outcome", ex);
            }
        }

        /// <summary>
        /// Records retinue casualties after battle for display in Personal Feed.
        /// Shows how many retinue soldiers were killed or wounded in the last engagement.
        /// </summary>
        public void AddRetinueCasualtyReport(int killed, int wounded, string battleContext)
        {
            try
            {
                if (killed <= 0 && wounded <= 0)
                {
                    return;
                }

                var dayNumber = (int)CampaignTime.Now.ToDays;
                var headline = killed > 0 && wounded > 0
                    ? new TextObject("{=news_ret_casualties_both}Your retinue suffered {KILLED} killed and {WOUNDED} wounded{CONTEXT}.")
                    : killed > 0
                        ? new TextObject("{=news_ret_casualties_killed}Your retinue lost {KILLED} soldier{KILLED_PLURAL}{CONTEXT}.")
                        : new TextObject("{=news_ret_casualties_wounded}Your retinue had {WOUNDED} soldier{WOUNDED_PLURAL} wounded{CONTEXT}.");

                headline.SetTextVariable("KILLED", killed);
                headline.SetTextVariable("WOUNDED", wounded);
                headline.SetTextVariable("KILLED_PLURAL", killed == 1 ? "" : "s");
                headline.SetTextVariable("WOUNDED_PLURAL", wounded == 1 ? "" : "s");
                headline.SetTextVariable("CONTEXT", string.IsNullOrEmpty(battleContext) ? "" : $" in {battleContext}");

                var placeholders = new Dictionary<string, string>
                {
                    { "KILLED", killed.ToString() },
                    { "WOUNDED", wounded.ToString() }
                };

                AddPersonalNews("retinue", headline.ToString(), placeholders, $"retinue:casualties:{dayNumber}", 2);
                ModLogger.Info(LogCategory, $"Retinue casualty report: {killed} killed, {wounded} wounded");
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to add retinue casualty report", ex);
            }
        }

        /// <summary>
        /// Records a named veteran emerging from the retinue for display in Personal Feed.
        /// These events are memorable milestones in the player's command.
        /// </summary>
        public void AddVeteranEmergence(string veteranName, string trait)
        {
            try
            {
                if (string.IsNullOrEmpty(veteranName))
                {
                    return;
                }

                var headline = new TextObject("{=news_vet_emergence}{NAME} the {TRAIT} has distinguished themselves in your retinue.");
                headline.SetTextVariable("NAME", veteranName);
                headline.SetTextVariable("TRAIT", trait ?? "Steady");

                var placeholders = new Dictionary<string, string>
                {
                    { "NAME", veteranName },
                    { "TRAIT", trait ?? "Steady" }
                };

                AddPersonalNews("retinue", headline.ToString(), placeholders, $"veteran:emergence:{veteranName}", 3);
                ModLogger.Info(LogCategory, $"Veteran emergence reported: {veteranName} the {trait}");
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to add veteran emergence news", ex);
            }
        }

        /// <summary>
        /// Records a named veteran's death for display in Personal Feed.
        /// These deaths are impactful narrative moments.
        /// </summary>
        public void AddVeteranDeath(string veteranName, int battlesSurvived, int kills)
        {
            try
            {
                if (string.IsNullOrEmpty(veteranName))
                {
                    return;
                }

                var headline = battlesSurvived > 5
                    ? new TextObject("{=news_vet_death_legend}{NAME}, a legend of {BATTLES} battles and {KILLS} kills, has fallen.")
                    : new TextObject("{=news_vet_death}{NAME}, who served through {BATTLES} battles, has been slain.");
                headline.SetTextVariable("NAME", veteranName);
                headline.SetTextVariable("BATTLES", battlesSurvived);
                headline.SetTextVariable("KILLS", kills);

                var placeholders = new Dictionary<string, string>
                {
                    { "NAME", veteranName },
                    { "BATTLES", battlesSurvived.ToString() },
                    { "KILLS", kills.ToString() }
                };

                AddPersonalNews("retinue", headline.ToString(), placeholders, $"veteran:death:{veteranName}", 3);
                ModLogger.Info(LogCategory, $"Veteran death reported: {veteranName} ({battlesSurvived} battles, {kills} kills)");
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to add veteran death news", ex);
            }
        }

        /// <summary>
        /// Records a promotion to the personal feed with immersive military flavor.
        /// Called after a player is promoted to a new tier.
        /// </summary>
        /// <param name="newTier">The tier the player was promoted to (2-9).</param>
        /// <param name="rankName">The culture-specific rank title.</param>
        /// <param name="retinueSoldiers">Number of retinue soldiers granted (0 for non-commander tiers).</param>
        public void AddPromotionNews(int newTier, string rankName, int retinueSoldiers = 0)
        {
            try
            {
                if (string.IsNullOrEmpty(rankName))
                {
                    rankName = $"Tier {newTier}";
                }

                // Select tier-appropriate headline with RP flavor
                var headlineKey = newTier switch
                {
                    2 => "News_Promotion_T2",      // First real rank
                    3 => "News_Promotion_T3",      // Proven soldier
                    4 => "News_Promotion_T4",      // Veteran status
                    5 => "News_Promotion_T5",      // Officer track begins
                    6 => "News_Promotion_T6",      // Senior NCO
                    7 => "News_Promotion_T7",      // Commander with retinue
                    8 => "News_Promotion_T8",      // Expanded command
                    9 => "News_Promotion_T9",      // Senior commander
                    _ => "News_Promotion_Generic"
                };

                var placeholders = new Dictionary<string, string>
                {
                    { "RANK", rankName },
                    { "TIER", newTier.ToString() },
                    { "RETINUE_SIZE", retinueSoldiers.ToString() }
                };

                AddPersonalNews("promotion", headlineKey, placeholders, $"promotion:t{newTier}", 3);
                ModLogger.Info(LogCategory, $"Promotion recorded: {rankName} (T{newTier})");
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to add promotion news", ex);
            }
        }

        /// <summary>
        /// Records a pay muster outcome to the personal feed.
        /// Provides historical record of payment for player review.
        /// </summary>
        /// <param name="outcome">Outcome type: "full", "partial", "delayed", "promissory", "corruption", "side_deal".</param>
        /// <param name="amountPaid">Gold actually received (0 for delayed/promissory).</param>
        /// <param name="amountOwed">Backpay still owed after this muster (0 if fully paid).</param>
        public void AddPayMusterNews(string outcome, int amountPaid, int amountOwed = 0)
        {
            try
            {
                // Select outcome-appropriate headline with RP flavor
                var headlineKey = outcome?.ToLowerInvariant() switch
                {
                    "full" or "standard" or "backpay" => "News_PayMuster_Full",
                    "partial" or "partial_backpay" => "News_PayMuster_Partial",
                    "delayed" => "News_PayMuster_Delayed",
                    "promissory" or "iou" => "News_PayMuster_Promissory",
                    "corruption" or "corruption_success" => "News_PayMuster_Corruption",
                    "side_deal" => "News_PayMuster_SideDeal",
                    _ => "News_PayMuster_Generic"
                };

                var placeholders = new Dictionary<string, string>
                {
                    { "AMOUNT", amountPaid.ToString() },
                    { "OWED", amountOwed.ToString() }
                };

                var dayNumber = (int)CampaignTime.Now.ToDays;
                AddPersonalNews("pay", headlineKey, placeholders, $"pay:muster:{dayNumber}", 2);
                ModLogger.Info(LogCategory, $"Pay muster recorded: {outcome} - {amountPaid} paid, {amountOwed} owed");
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to add pay muster news", ex);
            }
        }

        /// <summary>
        /// Schedules a pending chain event for context display in Daily Brief.
        /// Shows hints like "A comrade owes you money" before the follow-up fires.
        /// </summary>
        public void AddPendingEvent(string sourceId, string chainId, string hint, int delayDays)
        {
            try
            {
                if (string.IsNullOrEmpty(chainId))
                {
                    return;
                }

                // Validate delay is at least 1 day
                if (delayDays < 1)
                {
                    ModLogger.Debug(LogCategory, $"Pending event skipped - delay too short: {chainId} ({delayDays} days)");
                    return;
                }

                _pendingEvents ??= new List<PendingEventRecord>();

                // Remove any existing pending event with the same chain ID
                _pendingEvents.RemoveAll(p => p.ChainEventId == chainId);

                var currentDay = (int)CampaignTime.Now.ToDays;
                _pendingEvents.Add(new PendingEventRecord
                {
                    SourceEventId = sourceId ?? string.Empty,
                    ChainEventId = chainId,
                    ContextHint = hint ?? string.Empty,
                    CreatedDay = currentDay,
                    ScheduledDay = currentDay + delayDays
                });

                // Capacity limit: keep only the 10 most recent pending events
                if (_pendingEvents.Count > 10)
                {
                    // Remove oldest (first added) events
                    _pendingEvents.RemoveAt(0);
                    ModLogger.Debug(LogCategory, "Pending events trimmed to capacity limit of 10");
                }

                ModLogger.Debug(LogCategory, $"Pending event added: {chainId} scheduled for day {currentDay + delayDays}");
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to add pending event", ex);
            }
        }

        /// <summary>
        /// Clears a pending chain event when it fires.
        /// Called when the chain event is delivered.
        /// </summary>
        public void ClearPendingEvent(string chainEventId)
        {
            try
            {
                if (string.IsNullOrEmpty(chainEventId))
                {
                    return;
                }

                _pendingEvents ??= new List<PendingEventRecord>();
                var removed = _pendingEvents.RemoveAll(p => p.ChainEventId == chainEventId);

                if (removed > 0)
                {
                    ModLogger.Debug(LogCategory, $"Pending event cleared: {chainEventId}");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to clear pending event", ex);
            }
        }

        /// <summary>
        /// Records a muster outcome for camp news display.
        /// Called at the end of each muster cycle to summarize pay, rations, and unit status.
        /// </summary>
        public void AddMusterOutcome(MusterOutcomeRecord record)
        {
            try
            {
                if (record == null)
                {
                    return;
                }

                _musterOutcomes ??= new List<MusterOutcomeRecord>();
                _musterOutcomes.Add(record);

                // Keep only the last 5 muster records (roughly 2 months of game time)
                while (_musterOutcomes.Count > 5)
                {
                    _musterOutcomes.RemoveAt(0);
                }

                ModLogger.Debug(LogCategory, $"Muster outcome recorded: pay={record.PayOutcome}, ration={record.RationOutcome}, supply={record.SupplyLevel}%");

                // Add to personal feed with formatted summary
                var headline = BuildMusterHeadline(record);
                if (!string.IsNullOrEmpty(headline))
                {
                    AddToPersonalFeed(headline, record.DayNumber);
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to add muster outcome", ex);
            }
        }

        /// <summary>
        /// Returns the most recent muster outcome, or null if none recorded.
        /// </summary>
        public MusterOutcomeRecord GetLastMusterOutcome()
        {
            try
            {
                _musterOutcomes ??= new List<MusterOutcomeRecord>();
                return _musterOutcomes.Count > 0 ? _musterOutcomes[_musterOutcomes.Count - 1] : null;
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to get last muster outcome", ex);
                return null;
            }
        }

        /// <summary>
        /// Returns a formatted muster summary for display in menus.
        /// Uses straightforward military language appropriate for the setting.
        /// </summary>
        public string GetLastMusterSummary()
        {
            try
            {
                var record = GetLastMusterOutcome();
                if (record == null)
                {
                    return "No muster on record.";
                }

                var sb = new StringBuilder();
                var currentDay = (int)CampaignTime.Now.ToDays;
                var daysSince = currentDay - record.DayNumber;
                sb.AppendLine($"Last Muster ({daysSince} days ago)");

                // Pay line
                var payText = record.PayOutcome switch
                {
                    "paid" => $"Pay: {record.PayAmount} denars received",
                    "partial" => $"Pay: {record.PayAmount} denars (partial, backpay still owed)",
                    "delayed" => "Pay: Delayed — lord short on funds",
                    "promissory" => $"Pay: Promissory note for {record.PayAmount} denars",
                    "corruption" => $"Pay: {record.PayAmount} denars (with certain arrangements)",
                    "side_deal" => $"Pay: {record.PayAmount} denars (side deal)",
                    _ => $"Pay: {record.PayOutcome}"
                };
                sb.AppendLine(payText);

                // Ration line
                var rationText = record.RationOutcome switch
                {
                    "issued" => $"Rations: {GetRationDisplayName(record.RationItemId)} issued",
                    "none_low_supply" => "Rations: None issued — supplies too low",
                    "none_critical" => "Rations: None issued — supplies critically low",
                    "officer_exempt" => "Rations: Not applicable (officer provision)",
                    "commander_exempt" => "Rations: Not applicable (commander provision)",
                    _ => $"Rations: {record.RationOutcome}"
                };
                sb.AppendLine(rationText);

                // Supply status
                var supplyStatus = record.SupplyLevel switch
                {
                    >= 80 => "well-stocked",
                    >= 60 => "adequate",
                    >= 40 => "limited",
                    >= 30 => "low",
                    _ => "critical"
                };
                sb.AppendLine($"Supplies: {record.SupplyLevel}% ({supplyStatus})");

                // Unit status (only if there were losses or sick)
                if (record.LostSinceLast > 0 || record.SickSinceLast > 0)
                {
                    var unitParts = new List<string>();
                    if (record.LostSinceLast > 0)
                    {
                        unitParts.Add($"{record.LostSinceLast} lost");
                    }
                    if (record.SickSinceLast > 0)
                    {
                        unitParts.Add($"{record.SickSinceLast} sick");
                    }
                    sb.AppendLine($"Unit: {string.Join(", ", unitParts)} since last muster");
                }

                return sb.ToString().TrimEnd();
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to get muster summary", ex);
                return "Muster records unavailable.";
            }
        }

        /// <summary>
        /// Builds a headline for the personal feed from a muster outcome.
        /// </summary>
        private string BuildMusterHeadline(MusterOutcomeRecord record)
        {
            try
            {
                // Lead with the most notable aspect of the muster
                if (record.PayOutcome == "delayed")
                {
                    return "Muster: Pay delayed this cycle.";
                }
                if (record.RationOutcome == "none_critical")
                {
                    return "Muster: No rations — supplies critically low.";
                }
                if (record.RationOutcome == "none_low_supply")
                {
                    return "Muster: No rations issued due to supply shortage.";
                }
                if (record.LostSinceLast >= 5)
                {
                    return $"Muster: Heavy losses this cycle — {record.LostSinceLast} men gone.";
                }
                if (record.PayOutcome == "corruption" || record.PayOutcome == "side_deal")
                {
                    return $"Muster: Pay collected with certain arrangements.";
                }

                // Standard muster
                if (record.RationOutcome == "issued" && !string.IsNullOrEmpty(record.RationItemId))
                {
                    return $"Muster: {record.PayAmount} denars paid, {GetRationDisplayName(record.RationItemId)} issued.";
                }

                return $"Muster: {record.PayAmount} denars paid.";
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to build muster headline", ex);
                return "Muster completed.";
            }
        }

        /// <summary>
        /// Converts a ration item ID to a display-friendly name.
        /// </summary>
        private static string GetRationDisplayName(string itemId)
        {
            return itemId?.ToLowerInvariant() switch
            {
                "meat" => "meat ration",
                "cheese" => "cheese ration",
                "butter" => "butter ration",
                "grain" => "grain ration",
                _ => itemId ?? "ration"
            };
        }

        /// <summary>
        /// Adds an item to the personal feed with the specified headline and day.
        /// Uses a direct headline key for muster-related messages.
        /// Enforces severity-based replacement: higher or equal severity can replace existing items within 24h window.
        /// </summary>
        private void AddToPersonalFeed(string headline, int dayNumber, int severity = 0)
        {
            try
            {
                _personalFeed ??= new List<DispatchItem>();

                // Check if there's a recent item (same day or last 24h) with lower severity that can be replaced
                var currentTime = CampaignTime.Now.ToDays;
                var recentItems = _personalFeed
                    .Where(x => (currentTime - x.DayCreated) <= 1.0) // Within 24h
                    .ToList();

                // Try to find an item with lower severity to replace
                var replacementTarget = recentItems
                    .Where(x => x.Severity < severity)
                    .OrderBy(x => x.Severity) // Replace lowest severity first
                    .ThenBy(x => x.DayCreated) // Then oldest
                    .FirstOrDefault();

                if (replacementTarget.HeadlineKey != null) // Check if we found a valid target (struct has values)
                {
                    _personalFeed.Remove(replacementTarget);
                    ModLogger.Debug(LogCategory, $"Replaced lower-severity news (sev {replacementTarget.Severity}) with new item (sev {severity})");
                }

                // Check if attempting to add a lower-severity item when high-severity items exist
                var hasHigherSeverity = recentItems.Any(x => x.Severity > severity);
                if (hasHigherSeverity && recentItems.Count >= MaxPersonalFeedItems)
                {
                    ModLogger.Debug(LogCategory, $"Lower-severity item (sev {severity}) blocked from replacing higher-severity news");
                    // Don't add - would be trimmed anyway
                    return;
                }

                _personalFeed.Add(new DispatchItem
                {
                    HeadlineKey = headline, // Direct text for muster headlines
                    DayCreated = dayNumber,
                    Category = "muster",
                    Type = DispatchType.Report,
                    Severity = severity,
                    Confidence = 100,
                    MinDisplayDays = 1,
                    FirstShownDay = -1
                });

                // Trim to capacity
                while (_personalFeed.Count > MaxPersonalFeedItems)
                {
                    _personalFeed.RemoveAt(0);
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to add to personal feed", ex);
            }
        }

        /// <summary>
        /// Adds a camp simulation news item to the personal feed.
        /// Called by CompanySimulationBehavior to report daily roster changes, incidents, and pulse events.
        /// </summary>
        /// <param name="headline">The localized headline text to display.</param>
        /// <param name="severity">Severity level: "flavor", "minor", "notable", "serious", "critical".</param>
        /// <param name="subCategory">Sub-category for grouping: "roster", "incident", "pulse".</param>
        public void AddCampNews(string headline, string severity, string subCategory)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(headline))
                {
                    return;
                }

                // Map string severity to numeric value matching NewsSeverity enum
                int severityInt = severity?.ToLowerInvariant() switch
                {
                    "flavor" => 0,      // Normal
                    "minor" => 1,       // Positive (routine camp news)
                    "notable" => 2,     // Attention
                    "serious" => 3,     // Urgent
                    "critical" => 4,    // Critical
                    _ => 0
                };

                // Calculate minimum display days based on severity
                int minDisplayDays = severityInt switch
                {
                    4 => 2,     // Critical: 2 days
                    3 => 1,     // Serious: 1 day
                    2 => 1,     // Notable: 1 day
                    _ => 1      // Default: 1 day
                };

                var dayNumber = (int)CampaignTime.Now.ToDays;
                var storyKey = $"sim:{subCategory}:{dayNumber}:{headline.GetHashCode():X}";

                _personalFeed ??= new List<DispatchItem>();

                // Check for duplicates with same story key
                if (_personalFeed.Any(x => x.StoryKey == storyKey))
                {
                    return;
                }

                _personalFeed.Add(new DispatchItem
                {
                    HeadlineKey = headline,
                    DayCreated = dayNumber,
                    Category = $"simulation_{subCategory}",
                    Type = DispatchType.Report,
                    Severity = severityInt,
                    Confidence = 100,
                    MinDisplayDays = minDisplayDays,
                    FirstShownDay = -1,
                    StoryKey = storyKey
                });

                // Trim to capacity
                while (_personalFeed.Count > MaxPersonalFeedItems)
                {
                    _personalFeed.RemoveAt(0);
                }

                ModLogger.Debug(LogCategory, $"Added camp news: {headline} (severity={severity}, category={subCategory})");
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to add camp news", ex);
            }
        }

        /// <summary>
        /// Adds a routine activity outcome to the personal news feed.
        /// Called by CampRoutineProcessor after processing phase transitions.
        /// </summary>
        /// <param name="outcome">The routine outcome to add as news.</param>
        public void AddRoutineOutcome(RoutineOutcome outcome)
        {
            try
            {
                if (outcome == null)
                {
                    return;
                }

                // Determine severity based on outcome type
                int severityInt = outcome.Outcome switch
                {
                    OutcomeType.Excellent => 1,  // Positive (green)
                    OutcomeType.Good => 0,       // Normal (default)
                    OutcomeType.Normal => 0,     // Normal (default)
                    OutcomeType.Poor => 2,       // Attention (yellow warning)
                    OutcomeType.Mishap => 2,     // Attention (yellow warning)
                    _ => 0
                };

                // Build headline from outcome
                string headline = outcome.GetNewsSummary();
                
                var dayNumber = (int)CampaignTime.Now.ToDays;
                var storyKey = $"routine:{outcome.Phase}:{outcome.ActivityCategory}:{dayNumber}";

                _personalFeed ??= new List<DispatchItem>();

                // Check for duplicates
                if (_personalFeed.Any(x => x.StoryKey == storyKey))
                {
                    return;
                }

                _personalFeed.Add(new DispatchItem
                {
                    HeadlineKey = headline,
                    DayCreated = dayNumber,
                    Category = "routine_activity",
                    Type = DispatchType.Report,
                    Severity = severityInt,
                    Confidence = 100,
                    MinDisplayDays = 1,
                    FirstShownDay = -1,
                    StoryKey = storyKey
                });

                // Trim to capacity
                while (_personalFeed.Count > MaxPersonalFeedItems)
                {
                    _personalFeed.RemoveAt(0);
                }

                ModLogger.Debug(LogCategory, 
                    $"Added routine outcome: {outcome.ActivityName} ({outcome.Outcome})");
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to add routine outcome", ex);
            }
        }

        /// <summary>
        /// Updates the company status summary displayed in the menu header.
        /// Called by CompanySimulationBehavior after daily tick to reflect current company state.
        /// </summary>
        /// <param name="fitForDuty">Number of soldiers currently fit for duty.</param>
        /// <param name="totalStrength">Total soldiers in the company (including sick/wounded).</param>
        /// <param name="netChange">Net change in strength since yesterday (+/- soldiers).</param>
        /// <param name="pulseText">Brief status text describing company mood (empty if neutral).</param>
        public void UpdateCompanyStatus(int fitForDuty, int totalStrength, int netChange, string pulseText)
        {
            try
            {
                // Store in CampNewsState for display in camp menu
                _campNewsState ??= new CampNewsState();
                _campNewsState.LastFitForDuty = fitForDuty;
                _campNewsState.LastTotalStrength = totalStrength;
                _campNewsState.LastNetChange = netChange;
                _campNewsState.LastPulseText = pulseText ?? string.Empty;

                ModLogger.Debug(LogCategory, $"Company status updated: {fitForDuty}/{totalStrength} fit ({netChange:+#;-#;0}). {pulseText}");
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to update company status", ex);
            }
        }

        /// <summary>
        /// Returns recent event outcomes within the specified number of days.
        /// </summary>
        public List<EventOutcomeRecord> GetRecentEventOutcomes(int maxDaysOld = 3)
        {
            try
            {
                _eventOutcomes ??= new List<EventOutcomeRecord>();

                var currentDay = (int)CampaignTime.Now.ToDays;
                return _eventOutcomes
                    .Where(e => currentDay - e.DayNumber <= maxDaysOld)
                    .OrderByDescending(e => e.DayNumber)
                    .ToList();
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to get recent event outcomes", ex);
                return new List<EventOutcomeRecord>();
            }
        }

        /// <summary>
        /// Returns personal feed items since the specified campaign day.
        /// Used by muster menu to display events that occurred during the muster period.
        /// </summary>
        public List<DispatchItem> GetPersonalFeedSince(int dayNumber)
        {
            try
            {
                _personalFeed ??= new List<DispatchItem>();

                return _personalFeed
                    .Where(item => item.DayCreated >= dayNumber)
                    .OrderBy(item => item.DayCreated)
                    .ToList();
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to get personal feed since day", ex);
                return new List<DispatchItem>();
            }
        }

        /// <summary>
        /// Returns order outcomes since the specified campaign day.
        /// Used by muster menu to display orders completed/failed during the muster period.
        /// </summary>
        public List<OrderOutcomeRecord> GetOrderOutcomesSince(int dayNumber)
        {
            try
            {
                _orderOutcomes ??= new List<OrderOutcomeRecord>();

                return _orderOutcomes
                    .Where(outcome => outcome.DayNumber >= dayNumber)
                    .OrderBy(outcome => outcome.DayNumber)
                    .ToList();
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to get order outcomes since day", ex);
                return new List<OrderOutcomeRecord>();
            }
        }

        /// <summary>
        /// Builds a context-appropriate headline for an event outcome.
        /// Uses event type patterns to generate immersive headlines.
        /// </summary>
        private string BuildEventHeadline(EventOutcomeRecord outcome)
        {
            var eventId = outcome.EventId?.ToLowerInvariant() ?? string.Empty;
            var effects = outcome.OutcomeSummary ?? string.Empty;

            // Dice/gambling events
            if (eventId.Contains("dice") || eventId.Contains("gambling") || eventId.Contains("wager"))
            {
                return string.IsNullOrEmpty(effects)
                    ? "Dice game in camp"
                    : $"Dice game in camp — {effects}";
            }

            // Training events
            if (eventId.Contains("training") || eventId.Contains("drill") || eventId.Contains("practice"))
            {
                return string.IsNullOrEmpty(effects)
                    ? "Training session"
                    : $"Training session — {effects}";
            }

            // Hunt events
            if (eventId.Contains("hunt"))
            {
                return string.IsNullOrEmpty(effects)
                    ? "Hunt with the lord"
                    : $"Hunt with the lord — {effects}";
            }

            // Lending/loan events
            if (eventId.Contains("lend") || eventId.Contains("loan"))
            {
                if (eventId.Contains("repay") || eventId.Contains("return"))
                {
                    return string.IsNullOrEmpty(effects)
                        ? "Debt repaid"
                        : $"Debt repaid — {effects}";
                }
                return "Lent money to a comrade";
            }

            // Scrutiny/attention events
            if (eventId.Contains("scrutiny") || eventId.Contains("attention") || eventId.Contains("noticed"))
            {
                return string.IsNullOrEmpty(effects)
                    ? "Unwanted attention"
                    : $"Unwanted attention — {effects}";
            }

            // Discipline events
            if (eventId.Contains("discipline") || eventId.Contains("punishment") || eventId.Contains("infraction"))
            {
                return string.IsNullOrEmpty(effects)
                    ? "Disciplinary matter"
                    : $"Disciplinary matter — {effects}";
            }

            // Loot/battlefield events
            if (eventId.Contains("loot") || eventId.Contains("battlefield") || eventId.Contains("corpse"))
            {
                return string.IsNullOrEmpty(effects)
                    ? "Battlefield opportunity"
                    : $"Battlefield opportunity — {effects}";
            }

            // Tavern/settlement events
            if (eventId.Contains("tavern") || eventId.Contains("drinking"))
            {
                return string.IsNullOrEmpty(effects)
                    ? "Evening at the tavern"
                    : $"Evening at the tavern — {effects}";
            }

            // Generic fallback with title
            var title = outcome.EventTitle ?? outcome.EventId ?? "Event";
            return string.IsNullOrEmpty(effects)
                ? title
                : $"{title} — {effects}";
        }

        /// <summary>
        /// Posts a short high-signal personal dispatch line (used by decision outcomes).
        /// </summary>
        public void PostPersonalDispatchText(string category, string text, string storyKey, int minDisplayDays = 2)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    return;
                }

                AddPersonalNews(category ?? "personal", text.Trim(), new Dictionary<string, string>(), storyKey, minDisplayDays);
                TrimFeeds();
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to post personal dispatch text", ex);
            }
        }

        private DailyReportSnapshot BuildDailyReportSnapshot(
            EnlistmentBehavior enlistment,
            int dayNumber,
            out DailyAggregate aggregate,
            out DailyReportGenerationContext context)
        {
            aggregate = default;
            context = new DailyReportGenerationContext { DayNumber = dayNumber };

            var snapshot = new DailyReportSnapshot
            {
                DayNumber = dayNumber
            };

            try
            {
                var ctx = new CampNewsContext
                {
                    DayNumber = dayNumber,
                    Enlistment = enlistment,
                    Lord = enlistment?.CurrentLord,
                    LordParty = enlistment?.CurrentLord?.PartyBelongedTo,
                    TriggerTracker = CampaignTriggerTrackerBehavior.Instance,
                    CampLife = CampLifeBehavior.Instance,
                    // The unit schedule is managed through direct orders.
                    NewsState = _campNewsState,
                    NewsBehavior = this,
                    Generation = context
                };

                for (var i = 0; i < DailyReportFactProducers.Length; i++)
                {
                    try
                    {
                        DailyReportFactProducers[i]?.Contribute(snapshot, ctx);
                    }
                    catch (Exception ex)
                    {
                        ModLogger.Error(LogCategory, $"Daily report producer failed: {DailyReportFactProducers[i]?.GetType().Name}", ex);
                    }
                }

                // Ledger aggregate for the day (best-effort).
                aggregate = DailyAggregate.CreateEmpty(dayNumber);
                aggregate.LostToday = snapshot.DeadDelta;
                aggregate.WoundedToday = Math.Max(0, snapshot.WoundedDelta);
                aggregate.SickToday = Math.Max(0, snapshot.SickDelta);
                // TrainingIncidents/DecisionsResolved/Dispatches tracked in later phases.

                // Accumulate "since last muster" counters (reset at each muster).
                if (snapshot.DeadDelta > 0)
                {
                    _lostSinceLastMuster += snapshot.DeadDelta;
                }
                if (snapshot.SickDelta > 0)
                {
                    _sickSinceLastMuster += snapshot.SickDelta;
                }

                snapshot.Normalize();
                return snapshot;
            }
            catch
            {
                snapshot.Normalize();
                return snapshot;
            }
        }

        /// <summary>
        /// Helper: get the latest Daily Report lines (already templated) if available.
        /// </summary>
        public IReadOnlyList<string> GetLatestDailyReportLines()
        {
            try
            {
                // If UI asks before the daily tick fires (e.g., right after load), generate lazily but safely.
                EnsureDailyReportGenerated();

                _campNewsState ??= new CampNewsState();

                var today = (int)CampaignTime.Now.ToDays;
                var record = _campNewsState.TryGetReportForDay(today) ?? _campNewsState.TryGetLatestReport();
                return record?.Lines ?? Array.Empty<string>();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Helper: returns a compact excerpt suitable for a main-menu paragraph.
        /// </summary>
        public string GetLatestDailyReportExcerpt(int maxLines = 3, int maxChars = 260)
        {
            try
            {
                // Ensure we have a record for the current day before formatting an excerpt.
                EnsureDailyReportGenerated();

                maxLines = Math.Max(1, Math.Min(maxLines, 6));
                maxChars = Math.Max(80, Math.Min(maxChars, 600));

                var lines = GetLatestDailyReportLines();
                if (lines == null || lines.Count == 0)
                {
                    return string.Empty;
                }

                var parts = lines
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Take(maxLines)
                    .ToList();

                if (parts.Count == 0)
                {
                    return string.Empty;
                }

                var excerpt = string.Join(" ", parts).Trim();

                // Check for any urgent matters that require the soldier's attention.
                try
                {
                    /*
                    var decisionBehavior = DecisionEventBehavior.Instance;
                    var available = decisionBehavior?.GetAvailablePlayerDecisions();
                    var count = available?.Count ?? 0;

                    if (count > 0 && available != null)
                    {
                        var enlistment = EnlistmentBehavior.Instance;
                        var top = available[0];
                        var topTitle = LanceLifeEventText.Resolve(top?.TitleId, top?.TitleFallback, top?.Id, enlistment).Trim();

                        // Keep this ONE sentence in the paragraph (use an em dash rather than a second sentence).
                        var oppLong = !string.IsNullOrWhiteSpace(topTitle)
                            ? $"Opportunities: {count} matters await your decision — most pressing: {topTitle}."
                            : $"Opportunities: {count} matters await your decision.";

                        var oppShort = $"Opportunities: {count} awaiting.";

                        // Prefer the richer sentence when it fits; otherwise use the short one.
                        var opp = $"{excerpt} {oppLong}".Trim().Length <= maxChars ? oppLong : oppShort;

                        // Ensure the Opportunities sentence is included by trimming the base excerpt if needed.
                        // (Main menu excerpt maxChars is typically generous, but we keep this robust.)
                        var reserved = opp.Length + 1; // space + sentence
                        if (reserved < maxChars)
                        {
                            var allowedBase = Math.Max(0, maxChars - reserved);
                            var baseText = excerpt;

                            if (baseText.Length > allowedBase)
                            {
                                // Trim base text to make room and add ellipsis to avoid an abrupt cut.
                                var trimmed = baseText.Substring(0, allowedBase).TrimEnd();
                                if (!string.IsNullOrWhiteSpace(trimmed))
                                {
                                    // Reserve room for ellipsis if possible.
                                    if (trimmed.Length > 3)
                                    {
                                        trimmed = trimmed.Substring(0, trimmed.Length - 3).TrimEnd() + "...";
                                    }
                                    else
                                    {
                                        trimmed = "...";
                                    }
                                }

                                baseText = trimmed;
                            }

                            excerpt = $"{baseText} {opp}".Trim();
                        }
                        else
                        {
                            // Degenerate case: maxChars too small - fall back to the opportunities sentence only.
                            excerpt = opp.Length <= maxChars
                                ? opp
                                : opp.Substring(0, maxChars).TrimEnd() + "...";
                        }
                    }
                    */
                }
                catch
                {
                    // Best-effort only; never break menu rendering due to decision availability checks.
                }

                if (excerpt.Length <= maxChars)
                {
                    return excerpt;
                }

                // Trim with a hard cap, preserving a clean ending.
                var cut = excerpt.Substring(0, maxChars).TrimEnd();
                return cut.EndsWith(".", StringComparison.Ordinal) ? cut : cut + "...";
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Checks if lord's army has just formed (for personal feed).
        /// </summary>
        private void CheckForArmyFormation()
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment == null || !enlistment.IsEnlisted)
                {
                    return;
                }

                var lord = enlistment.EnlistedLord;
                var hasArmyNow = lord?.PartyBelongedTo?.Army != null;

                // Army just formed (wasn't there yesterday, is there now)
                if (hasArmyNow && !_lordHadArmyYesterday)
                {
                    var placeholders = new Dictionary<string, string>
                    {
                        { "LORD", lord.Name.ToString() }
                    };

                    AddPersonalNews("army", "News_ArmyForming", placeholders, $"army:{lord.StringId}", 2);
                }

                _lordHadArmyYesterday = hasArmyNow;
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Error checking army formation", ex);
            }
        }

        /// <summary>
        /// Ensures the Daily Brief is generated for the current in-game day.
        /// Stable for the day; regenerated at most once per day.
        /// </summary>
        private void EnsureDailyBriefGenerated()
        {
            try
            {
                var currentDay = (int)CampaignTime.Now.ToDays;
                if (_lastDailyBriefDayNumber == currentDay &&
                    (!string.IsNullOrWhiteSpace(_dailyBriefCompany) ||
                     !string.IsNullOrWhiteSpace(_dailyBriefUnit) ||
                     !string.IsNullOrWhiteSpace(_dailyBriefKingdom)))
                {
                    return;
                }

                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    _lastDailyBriefDayNumber = currentDay;
                    _dailyBriefCompany = string.Empty;
                    _dailyBriefUnit = string.Empty;
                    _dailyBriefKingdom = string.Empty;
                    return;
                }

                _lastDailyBriefDayNumber = currentDay;
                _dailyBriefCompany = BuildDailyCompanyLine(enlistment);
                _dailyBriefUnit = BuildDailyUnitLine(enlistment);
                _dailyBriefKingdom = BuildDailyKingdomLine(enlistment);
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Error generating Daily Brief", ex);
            }
        }

        private static string BuildDailyCompanyLine(EnlistmentBehavior enlistment)
        {
            try
            {
                var lord = enlistment?.CurrentLord;
                var party = lord?.PartyBelongedTo;
                if (party == null)
                {
                    return new TextObject("{=brief_company_none}No company details available.").ToString();
                }

                var lordName = lord.Name?.ToString() ?? new TextObject("{=brief_the_lord}the lord").ToString();
                var partySize = party.MemberRoster?.TotalManCount ?? 0;
                var sizeDescId = partySize switch
                {
                    < 30 => "{=brief_size_small}small",
                    < 80 => "{=brief_size_sizeable}sizeable",
                    < 150 => "{=brief_size_large}large",
                    _ => "{=brief_size_formidable}formidable"
                };
                var sizeDesc = new TextObject(sizeDescId).ToString();

                // Active battle
                if (party.Party?.MapEvent != null)
                {
                    var enemyLeader = party.Party.MapEvent.GetLeaderParty(
                        party.Party.Side == BattleSideEnum.Attacker ? BattleSideEnum.Defender : BattleSideEnum.Attacker)?.LeaderHero?.Name?.ToString();
                    if (!string.IsNullOrEmpty(enemyLeader))
                    {
                        var text = new TextObject("{=brief_battle_enemy}The company is locked in battle against the forces of {ENEMY}. Steel clashes and war cries fill the air.");
                        text.SetTextVariable("ENEMY", enemyLeader);
                        return text.ToString();
                    }
                    return new TextObject("{=brief_battle_generic}The company is engaged in battle. The clash of steel and shouts of men echo across the field.").ToString();
                }

                // Active siege
                if (party.Party?.SiegeEvent != null)
                {
                    var siegeTarget = party.Party.SiegeEvent.BesiegedSettlement?.Name?.ToString();
                    if (!string.IsNullOrEmpty(siegeTarget))
                    {
                        var isAttacker = party.Party.Side == BattleSideEnum.Attacker;
                        if (isAttacker)
                        {
                            var text = new TextObject("{=brief_siege_attack}The siege of {SETTLEMENT} continues. Siege engines creak and groan as the engineers work through the night. The walls loom ahead, defiant.");
                            text.SetTextVariable("SETTLEMENT", siegeTarget);
                            return text.ToString();
                        }
                        var textDef = new TextObject("{=brief_siege_defend}The company holds {SETTLEMENT} against the besiegers. The enemy camps spread across the horizon, their fires burning through the night.");
                        textDef.SetTextVariable("SETTLEMENT", siegeTarget);
                        return textDef.ToString();
                    }
                    return new TextObject("{=brief_siege_generic}The company is committed to siege operations. The days blend together in a rhythm of labor and watchfulness.").ToString();
                }

                // In settlement
                if (party.CurrentSettlement != null)
                {
                    var settlement = party.CurrentSettlement;
                    var settlementName = settlement.Name?.ToString() ?? new TextObject("{=brief_the_settlement}the settlement").ToString();

                    if (settlement.IsTown)
                    {
                        var text = new TextObject("{=brief_rest_town}The company rests at {SETTLEMENT}. The sounds of the town drift into camp — merchants hawking wares, the clatter of cart wheels, the murmur of townsfolk going about their business.");
                        text.SetTextVariable("SETTLEMENT", settlementName);
                        return text.ToString();
                    }
                    if (settlement.IsCastle)
                    {
                        var text = new TextObject("{=brief_rest_castle}The company is garrisoned at {SETTLEMENT}. The castle walls provide welcome shelter, and the men take the chance to rest properly for once.");
                        text.SetTextVariable("SETTLEMENT", settlementName);
                        return text.ToString();
                    }
                    if (settlement.IsVillage)
                    {
                        var text = new TextObject("{=brief_rest_village}The company has made camp near {SETTLEMENT}. Smoke rises from village hearths, and the smell of cooking fires reminds you of simpler times.");
                        text.SetTextVariable("SETTLEMENT", settlementName);
                        return text.ToString();
                    }
                    var textGeneric = new TextObject("{=brief_rest_generic}The company is camped at {SETTLEMENT}.");
                    textGeneric.SetTextVariable("SETTLEMENT", settlementName);
                    return textGeneric.ToString();
                }

                // Part of army
                if (party.Army != null)
                {
                    var armyLeader = party.Army.LeaderParty?.LeaderHero?.Name?.ToString() ?? new TextObject("{=brief_the_marshal}the marshal").ToString();
                    var armySize = party.Army.TotalManCount;
                    var armySizeDescId = armySize switch
                    {
                        < 200 => "{=brief_army_gathered}The gathered host",
                        < 500 => "{=brief_army_formidable}A formidable army",
                        < 1000 => "{=brief_army_great}A great host",
                        _ => "{=brief_army_mighty}A mighty army"
                    };
                    var armySizeDesc = new TextObject(armySizeDescId).ToString();

                    var text = new TextObject("{=brief_march_army}{ARMY_DESC} marches under the banner of {LEADER}. {LORD}'s {SIZE} company moves with the column, one warband among many.");
                    text.SetTextVariable("ARMY_DESC", armySizeDesc);
                    text.SetTextVariable("LEADER", armyLeader);
                    text.SetTextVariable("LORD", lordName);
                    text.SetTextVariable("SIZE", sizeDesc);
                    return text.ToString();
                }

                // Moving toward target
                if (party.TargetSettlement != null)
                {
                    var target = party.TargetSettlement.Name?.ToString() ?? new TextObject("{=brief_destination}destination").ToString();
                    var terrain = GetTerrainFlavor(party);
                    var text = new TextObject("{=brief_march_target}The company marches toward {TARGET}. {TERRAIN}");
                    text.SetTextVariable("TARGET", target);
                    text.SetTextVariable("TERRAIN", terrain);
                    return text.ToString();
                }

                // Default march
                var defaultTerrain = GetTerrainFlavor(party);
                var defaultText = new TextObject("{=brief_march_default}{LORD}'s {SIZE} company is on the move. {TERRAIN}");
                defaultText.SetTextVariable("LORD", lordName);
                defaultText.SetTextVariable("SIZE", sizeDesc);
                defaultText.SetTextVariable("TERRAIN", defaultTerrain);
                return defaultText.ToString();
            }
            catch
            {
                return new TextObject("{=brief_company_fallback}The company marches onward.").ToString();
            }
        }

        // Provides flavor text based on terrain and time of day for march descriptions.
        private static string GetTerrainFlavor(MobileParty party)
        {
            try
            {
                var hour = CampaignTime.Now.GetHourOfDay;
                var isNight = hour < 6 || hour >= 20;
                var isMorning = hour >= 6 && hour < 10;
                var isEvening = hour >= 17 && hour < 20;

                // Get terrain type from party position if available
                var terrainType = Campaign.Current?.MapSceneWrapper?.GetFaceTerrainType(party.CurrentNavigationFace);

                var terrainId = terrainType switch
                {
                    TerrainType.Mountain or TerrainType.Canyon => isNight
                        ? "{=brief_terrain_mountain_night}The mountain passes are treacherous in the dark, and the column moves slowly."
                        : "{=brief_terrain_mountain_day}Rocky slopes and narrow passes slow the advance, but the views stretch for leagues.",
                    TerrainType.Forest => isNight
                        ? "{=brief_terrain_forest_night}The forest grows dark and the men keep close, wary of what lurks between the trees."
                        : "{=brief_terrain_forest_day}The shade of the trees offers respite from the sun, though the undergrowth slows the wagons.",
                    TerrainType.Desert or TerrainType.Dune => isNight
                        ? "{=brief_terrain_desert_night}The desert cools at night, and the column makes good time across the sands."
                        : "{=brief_terrain_desert_day}Heat shimmers off the sand, and the men wrap their faces against the biting wind.",
                    TerrainType.Steppe => isNight
                        ? "{=brief_terrain_steppe_night}The endless steppe stretches dark beneath the stars, the grass whispering in the wind."
                        : "{=brief_terrain_steppe_day}The open steppe allows swift travel, the column stretching across the grasslands.",
                    TerrainType.Snow =>
                        "{=brief_terrain_cold}The cold bites at exposed skin, and the men huddle in their cloaks as they march.",
                    TerrainType.Bridge or TerrainType.Fording =>
                        "{=brief_terrain_crossing}The crossing slows the column as men and wagons navigate the water.",
                    TerrainType.Plain or TerrainType.Water or TerrainType.River or TerrainType.Lake => isNight
                        ? "{=brief_terrain_plain_night}The column moves by torchlight, the road stretching dark ahead."
                        : isMorning
                            ? "{=brief_terrain_plain_morning}Morning mist rises from the fields as the column forms up for the day's march."
                            : isEvening
                                ? "{=brief_terrain_plain_evening}The sun sinks low, casting long shadows as the men look for a place to make camp."
                                : "{=brief_terrain_plain_day}Dust rises from the road as boots and hooves churn the packed earth.",
                    _ => isNight
                        ? "{=brief_terrain_default_night}The column marches through the night, torches bobbing in the darkness."
                        : "{=brief_terrain_default_day}The road stretches ahead, and the men settle into the rhythm of the march."
                };

                return new TextObject(terrainId).ToString();
            }
            catch
            {
                return new TextObject("{=brief_terrain_fallback}The road stretches ahead.").ToString();
            }
        }

        private static readonly Random FlavorRng = new Random();

        private string BuildDailyUnitLine(EnlistmentBehavior enlistment)
        {
            try
            {
                var tier = enlistment?.EnlistmentTier ?? 1;
                var hour = CampaignTime.Now.GetHourOfDay;
                var tierKey = GetTierKey(tier);

                // Priority 0: Active order (on duty)
                var orderManager = Orders.Behaviors.OrderManager.Instance;
                if (orderManager != null && orderManager.IsOrderActive())
                {
                    var currentOrder = orderManager.GetCurrentOrder();
                    if (currentOrder != null)
                    {
                        // Show that player is currently on duty
                        return $"On duty: {Orders.OrderCatalog.GetDisplayTitle(currentOrder)}.";
                    }
                }

                // Priority 1: Recent battle aftermath (within 1 day)
                if (_lastPlayerBattleTime != CampaignTime.Zero)
                {
                    var hoursSinceBattle = (CampaignTime.Now - _lastPlayerBattleTime).ToHours;
                    if (hoursSinceBattle < 24 && !string.IsNullOrEmpty(_lastPlayerBattleType))
                    {
                        var outcomeKey = _lastPlayerBattleWon ? "won" : "lost";
                        var prefix = $"brief_{_lastPlayerBattleType}_{outcomeKey}_{tierKey}_";
                        var text = PickRandomLocalizedString(prefix, 3);
                        if (!string.IsNullOrEmpty(text))
                        {
                            return text;
                        }
                    }
                }

                // Priority 2: Injuries/illness (from PlayerConditionBehavior)
                var cond = PlayerConditionBehavior.Instance;
                if (cond?.IsEnabled() == true && cond.State?.HasAnyCondition == true)
                {
                    if (cond.State.HasInjury)
                    {
                        var prefix = $"brief_injury_{tierKey}_";
                        return PickRandomLocalizedString(prefix, 3, "brief_fallback_injury");
                    }
                    if (cond.State.HasIllness)
                    {
                        var prefix = $"brief_illness_{tierKey}_";
                        return PickRandomLocalizedString(prefix, 3, "brief_fallback_illness");
                    }
                }

                // Priority 2b: Medical Risk escalation (brewing sickness, not yet a full condition)
                var escalation = EscalationManager.Instance;
                if (escalation?.State != null && escalation.State.MedicalRisk >= 2)
                {
                    var medRisk = escalation.State.MedicalRisk;
                    if (medRisk >= 4)
                    {
                        // Serious/Critical - urgent
                        return new TextObject("{=brief_medrisk_serious}Fever won't break. See the surgeon before it worsens.").ToString();
                    }
                    if (medRisk >= 3)
                    {
                        // Concerning - getting worse
                        return new TextObject("{=brief_medrisk_concerning}Chills and aches. Illness brewing. Rest or surgeon.").ToString();
                    }
                    // Med Risk 2 - early warning
                    return new TextObject("{=brief_medrisk_mild}Feeling run down. Could be coming down with something.").ToString();
                }

                // Priority 3: Good condition - vary by tier and time of day
                var timeKey = GetTimeKey(hour);
                var prefix2 = $"brief_good_{tierKey}_{timeKey}_";
                return PickRandomLocalizedString(prefix2, 3, "brief_fallback_default");
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Error building daily unit line", ex);
                return new TextObject("{=brief_fallback_default}You're ready for whatever comes.").ToString();
            }
        }

        private static string GetTierKey(int tier)
        {
            if (tier <= 2)
            {
                return "recruit";
            }
            if (tier <= 4)
            {
                return "soldier";
            }
            if (tier <= 6)
            {
                return "nco";
            }
            return "veteran";
        }

        private static string GetTimeKey(int hour)
        {
            if (hour < 6 || hour >= 20)
            {
                return "night";
            }
            if (hour < 10)
            {
                return "morning";
            }
            if (hour >= 17)
            {
                return "evening";
            }
            return "day";
        }

        private static string PickRandomLocalizedString(string prefix, int count, string fallbackId = null)
        {
            var index = FlavorRng.Next(1, count + 1);
            var id = $"{prefix}{index}";
            var text = new TextObject($"{{={id}}}").ToString();

            // If TextObject didn't resolve (returned the ID), try fallback
            if (text.Contains($"{{={id}}}") || text == id)
            {
                if (!string.IsNullOrEmpty(fallbackId))
                {
                    return new TextObject($"{{={fallbackId}}}").ToString();
                }
                return string.Empty;
            }

            return text;
        }

        private string BuildDailyKingdomLine(EnlistmentBehavior enlistment)
        {
            try
            {
                // Prefer a real headline if we have one (keeps the brief grounded in actual events).
                if (_kingdomFeed != null && _kingdomFeed.Count > 0)
                {
                    var latest = _kingdomFeed.OrderByDescending(x => x.DayCreated).FirstOrDefault();
                    var headline = FormatDispatchItem(latest);
                    if (!string.IsNullOrWhiteSpace(headline))
                    {
                        return headline;
                    }
                }

                var kingdom = enlistment?.CurrentLord?.MapFaction as Kingdom;
                if (kingdom == null)
                {
                    return new TextObject("{=brief_realm_quiet}The realm is quiet, for now.").ToString();
                }

                var kingdomName = kingdom.Name?.ToString() ?? new TextObject("{=brief_the_realm}the realm").ToString();

                // Check active wars for strategic context using FactionManager
                var enemyKingdoms = Kingdom.All
                    .Where(k => k != kingdom && FactionManager.IsAtWarAgainstFaction(kingdom, k))
                    .ToList();

                var warCount = enemyKingdoms.Count;

                // Count active sieges in the realm (where kingdom is involved)
                var activeSieges = Campaign.Current?.SiegeEventManager?.SiegeEvents?
                    .Count(s => s.BesiegedSettlement?.MapFaction == kingdom ||
                               s.BesiegerCamp?.LeaderParty?.MapFaction == kingdom) ?? 0;

                // Multi-front war with sieges
                if (warCount >= 2 && activeSieges > 0)
                {
                    var text = new TextObject("{=brief_realm_multifront}{KINGDOM} fights on multiple fronts. Siege engines stand ready, and the roads are thick with soldiers marching to war.");
                    text.SetTextVariable("KINGDOM", kingdomName);
                    return text.ToString();
                }

                // Multiple wars, no active sieges
                if (warCount >= 2)
                {
                    var text = new TextObject("{=brief_realm_manywars}{KINGDOM} is pressed on all sides. Lords ride hard between campaigns, and the levies are called up again and again.");
                    text.SetTextVariable("KINGDOM", kingdomName);
                    return text.ToString();
                }

                // Single war with active siege
                if (warCount == 1 && activeSieges > 0)
                {
                    var enemyKingdom = enemyKingdoms[0];
                    var enemyName = enemyKingdom?.Name?.ToString() ?? new TextObject("{=brief_the_enemy}the enemy").ToString();

                    var text = new TextObject("{=brief_realm_war_siege}The war with {ENEMY} grinds on. Castle walls are contested, and the outcome hangs in the balance.");
                    text.SetTextVariable("ENEMY", enemyName);
                    return text.ToString();
                }

                // Single war, no siege
                if (warCount == 1)
                {
                    var enemyKingdom = enemyKingdoms[0];
                    var enemyName = enemyKingdom?.Name?.ToString() ?? new TextObject("{=brief_the_enemy}the enemy").ToString();

                    var text = new TextObject("{=brief_realm_war}The banners of {KINGDOM} march against {ENEMY}. Scouts bring word of enemy movements, and the lords sharpen their blades.");
                    text.SetTextVariable("KINGDOM", kingdomName);
                    text.SetTextVariable("ENEMY", enemyName);
                    return text.ToString();
                }

                // Peace time
                var text2 = new TextObject("{=brief_realm_peace}The realm is at peace, for now. Lords tend to their estates and the common folk go about their business, though every soldier knows it won't last.");
                return text2.ToString();
            }
            catch
            {
                return new TextObject("{=brief_realm_fallback}The realm is quiet, for now.").ToString();
            }
        }

        /// <summary>
        /// Battle started: cache initial troop strengths for pyrrhic detection.
        /// </summary>
        private void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
        {
            try
            {
                if (mapEvent == null)
                {
                    return;
                }

                var playerKingdom = GetPlayerEnlistedKingdom();
                if (playerKingdom == null)
                {
                    return;
                }

                // Only track battles involving player's kingdom
                var attackerFaction = attackerParty?.MapFaction;
                var defenderFaction = defenderParty?.MapFaction;

                if (attackerFaction != playerKingdom && defenderFaction != playerKingdom)
                {
                    return;
                }

                // Cache initial troop strengths for pyrrhic victory detection
                var snapshot = new BattleSnapshot
                {
                    MapEventId = mapEvent.GetHashCode().ToString(),
                    AttackerInitialStrength = mapEvent.AttackerSide?.TroopCount ?? 0,
                    DefenderInitialStrength = mapEvent.DefenderSide?.TroopCount ?? 0
                };

                _battleSnapshots[snapshot.MapEventId] = snapshot;
                ModLogger.Debug(LogCategory, $"Battle started: A={snapshot.AttackerInitialStrength}, D={snapshot.DefenderInitialStrength}");
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Error in OnMapEventStarted", ex);
            }
        }

        /// <summary>
        /// Battle ended: generate battle news with winner/loser and pyrrhic classification.
        /// </summary>
        private void OnMapEventEnded(MapEvent mapEvent)
        {
            try
            {
                if (mapEvent == null)
                {
                    return;
                }

                var playerKingdom = GetPlayerEnlistedKingdom();
                if (playerKingdom == null)
                {
                    return;
                }

                // Only report battles involving player's kingdom
                var attackerFaction = mapEvent.AttackerSide?.LeaderParty?.MapFaction;
                var defenderFaction = mapEvent.DefenderSide?.LeaderParty?.MapFaction;

                if (attackerFaction != playerKingdom && defenderFaction != playerKingdom)
                {
                    // Clean up any orphaned snapshot
                    _battleSnapshots.Remove(mapEvent.GetHashCode().ToString());
                    return;
                }

                // Skip bandit/looter battles - only report kingdom vs kingdom conflicts
                bool isAttackerBandit = attackerFaction == null || attackerFaction.IsBanditFaction;
                bool isDefenderBandit = defenderFaction == null || defenderFaction.IsBanditFaction;

                if (isAttackerBandit || isDefenderBandit)
                {
                    // Don't report bandit skirmishes - they're too minor for kingdom news
                    _battleSnapshots.Remove(mapEvent.GetHashCode().ToString());
                    return;
                }

                // Determine winner
                BattleSideEnum? winnerSide = null;
                if (mapEvent.BattleState == BattleState.AttackerVictory)
                {
                    winnerSide = BattleSideEnum.Attacker;
                }
                else if (mapEvent.BattleState == BattleState.DefenderVictory)
                {
                    winnerSide = BattleSideEnum.Defender;
                }

                if (winnerSide == null)
                {
                    // Inconclusive battle
                    AddBattleNews(mapEvent, "News_Inconclusive", null);
                    _battleSnapshots.Remove(mapEvent.GetHashCode().ToString());
                    return;
                }

                // Get snapshot for pyrrhic detection
                _battleSnapshots.TryGetValue(mapEvent.GetHashCode().ToString(), out var snapshot);
                var classification = ClassifyBattle(mapEvent, snapshot, winnerSide.Value);

                AddBattleNews(mapEvent, classification, winnerSide.Value);

                // Clean up snapshot
                _battleSnapshots.Remove(mapEvent.GetHashCode().ToString());

                // Check for player participation (Personal feed)
                CheckPlayerBattleParticipation(mapEvent, winnerSide.Value);
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Error in OnMapEventEnded", ex);
            }
        }

        /// <summary>
        /// Checks if player participated in the battle and generates personal news.
        /// Also records battle details for daily brief flavor.
        /// </summary>
        private void CheckPlayerBattleParticipation(MapEvent mapEvent, BattleSideEnum winnerSide)
        {
            try
            {
                if (!IsEnlisted() || mapEvent == null)
                {
                    return;
                }

                var playerParty = MobileParty.MainParty;
                if (playerParty?.Party == null)
                {
                    return;
                }

                // Check if player was involved in the battle
                var playerInvolved = mapEvent.InvolvedParties?.Contains(playerParty.Party) == true;
                if (!playerInvolved)
                {
                    return;
                }

                var place = mapEvent.MapEventSettlement?.Name?.ToString() ?? "the battlefield";
                var placeholders = new Dictionary<string, string>
                {
                    { "PLACE", place }
                };

                // Determine if player won or lost
                var playerOnAttacker = mapEvent.AttackerSide?.Parties?.Any(
                    p => p.Party == playerParty.Party) == true;
                var playerOnDefender = mapEvent.DefenderSide?.Parties?.Any(
                    p => p.Party == playerParty.Party) == true;

                var playerWon = (winnerSide == BattleSideEnum.Attacker && playerOnAttacker)
                             || (winnerSide == BattleSideEnum.Defender && playerOnDefender);

                // Record battle details for daily brief flavor
                _lastPlayerBattleTime = CampaignTime.Now;
                _lastPlayerBattleWon = playerWon;

                // Determine battle type
                var attackerFaction = mapEvent.AttackerSide?.LeaderParty?.MapFaction;
                var defenderFaction = mapEvent.DefenderSide?.LeaderParty?.MapFaction;
                var isBandit = (attackerFaction?.IsBanditFaction == true) || (defenderFaction?.IsBanditFaction == true);
                var isSiege = mapEvent.IsSiegeAssault || mapEvent.MapEventSettlement?.IsFortification == true;

                if (isSiege)
                {
                    _lastPlayerBattleType = "siege";
                }
                else if (isBandit)
                {
                    _lastPlayerBattleType = "bandit";
                }
                else
                {
                    _lastPlayerBattleType = "army";
                }

                var headlineKey = playerWon ? "News_PlayerBattle" : "News_PlayerDefeat";
                AddPersonalNews("participation", headlineKey, placeholders);
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Error checking player participation", ex);
            }
        }

        /// <summary>
        /// Siege started: generate siege news for kingdom settlements.
        /// </summary>
        private void OnSiegeStarted(SiegeEvent siegeEvent)
        {
            try
            {
                var playerKingdom = GetPlayerEnlistedKingdom();
                if (playerKingdom == null || siegeEvent == null)
                {
                    return;
                }

                var settlement = siegeEvent.BesiegedSettlement;
                if (settlement == null)
                {
                    return;
                }

                // Report if it's our kingdom's settlement being besieged OR we're the attackers
                var isOurSettlement = settlement.MapFaction == playerKingdom;
                var isOurSiege = siegeEvent.BesiegerCamp?.LeaderParty?.LeaderHero?.MapFaction == playerKingdom;

                if (!isOurSettlement && !isOurSiege)
                {
                    return;
                }

                var placeholders = new Dictionary<string, string>
                {
                    { "SETTLEMENT", settlement.Name?.ToString() ?? new TextObject("{=enl_ui_unknown}Unknown").ToString() }
                };

                AddKingdomNews("siege", "News_Siege", placeholders, $"siege:{settlement.StringId}", 2);
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Error in OnSiegeStarted", ex);
            }
        }

        /// <summary>
        /// Hero captured: generate prisoner news for kingdom lords.
        /// </summary>
        private void OnHeroPrisonerTaken(PartyBase captorParty, Hero prisoner)
        {
            try
            {
                var playerKingdom = GetPlayerEnlistedKingdom();
                if (playerKingdom == null || prisoner == null)
                {
                    return;
                }

                // Only report kingdom lords being captured
                if (prisoner.MapFaction != playerKingdom)
                {
                    return;
                }

                var placeholders = new Dictionary<string, string>
                {
                    { "LORD", prisoner.Name?.ToString() ?? new TextObject("{=enl_ui_unknown}Unknown").ToString() }
                };

                if (captorParty?.LeaderHero != null)
                {
                    placeholders["CAPTOR"] = captorParty.LeaderHero.Name?.ToString() ?? new TextObject("{=enl_ui_unknown}Unknown").ToString();
                    AddKingdomNews("prisoner", "News_PrisonerCapturedBy", placeholders, $"prisoner:{prisoner.StringId}");
                }
                else
                {
                    AddKingdomNews("prisoner", "News_PrisonerTaken", placeholders, $"prisoner:{prisoner.StringId}");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Error in OnHeroPrisonerTaken", ex);
            }
        }

        /// <summary>
        /// Hero released/escaped: generate prisoner release news.
        /// </summary>
        private void OnHeroPrisonerReleased(Hero prisoner, PartyBase party, IFaction captor, EndCaptivityDetail detail, bool isTransfer)
        {
            try
            {
                var playerKingdom = GetPlayerEnlistedKingdom();
                if (playerKingdom == null || prisoner == null)
                {
                    return;
                }

                // Only report kingdom lords being released
                if (prisoner.MapFaction != playerKingdom)
                {
                    return;
                }

                // Don't report transfers as releases
                if (isTransfer)
                {
                    return;
                }

                var placeholders = new Dictionary<string, string>
                {
                    { "LORD", prisoner.Name?.ToString() ?? new TextObject("{=enl_ui_unknown}Unknown").ToString() }
                };

                // Choose headline based on how they got free
                // Note: EndCaptivityDetail values vary by game version; use Released as default
                var headlineKey = detail == EndCaptivityDetail.ReleasedAfterEscape
                    ? "News_PrisonerEscaped"
                    : "News_PrisonerReleased";
                AddKingdomNews("prisoner", headlineKey, placeholders, $"prisoner:{prisoner.StringId}");
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Error in OnHeroPrisonerReleased", ex);
            }
        }

        /// <summary>
        /// Hero killed: generate execution news for kingdom lords.
        /// </summary>
        private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
        {
            try
            {
                var playerKingdom = GetPlayerEnlistedKingdom();
                if (playerKingdom == null || victim == null)
                {
                    return;
                }

                // Only report kingdom lords being executed
                if (victim.MapFaction != playerKingdom)
                {
                    return;
                }

                // Only report executions, not natural deaths or battle deaths
                if (detail != KillCharacterAction.KillCharacterActionDetail.Executed)
                {
                    return;
                }

                var placeholders = new Dictionary<string, string>
                {
                    { "LORD", victim.Name?.ToString() ?? new TextObject("{=enl_ui_unknown}Unknown").ToString() }
                };

                if (killer != null)
                {
                    placeholders["EXECUTOR"] = killer.Name?.ToString() ?? new TextObject("{=enl_ui_unknown}Unknown").ToString();
                    AddKingdomNews("execution", "News_Executed", placeholders, $"executed:{victim.StringId}", 2);
                }
                else
                {
                    AddKingdomNews("execution", "News_ExecutedNoExecutor", placeholders, $"executed:{victim.StringId}", 2);
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Error in OnHeroKilled", ex);
            }
        }

        /// <summary>
        /// Settlement ownership changed: generate capture/fallen news.
        /// </summary>
        private void OnSettlementOwnerChanged(
            Settlement settlement,
            bool openToClaim,
            Hero newOwner,
            Hero oldOwner,
            Hero capturerHero,
            ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            try
            {
                var playerKingdom = GetPlayerEnlistedKingdom();
                if (playerKingdom == null || settlement == null)
                {
                    return;
                }

                // Report if it was our kingdom's settlement (lost) or we captured it
                var wasOurs = oldOwner?.MapFaction == playerKingdom;
                var isNowOurs = newOwner?.MapFaction == playerKingdom;

                if (!wasOurs && !isNowOurs)
                {
                    return;
                }

                var placeholders = new Dictionary<string, string>
                {
                    { "SETTLEMENT", settlement.Name?.ToString() ?? new TextObject("{=enl_ui_unknown}Unknown").ToString() },
                    { "KINGDOM", newOwner?.MapFaction?.Name?.ToString() ?? new TextObject("{=enl_ui_unknown}Unknown").ToString() }
                };

                if (isNowOurs)
                {
                    AddKingdomNews("settlement", "News_Captured", placeholders, $"settlement_captured:{settlement.StringId}", 2);
                }
                else
                {
                    AddKingdomNews("settlement", "News_Fallen", placeholders, $"settlement_fallen:{settlement.StringId}", 2);
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Error in OnSettlementOwnerChanged", ex);
            }
        }

        /// <summary>
        /// Army dispersed: generate army news for personal feed.
        /// </summary>
        private void OnArmyDispersed(Army army, Army.ArmyDispersionReason reason, bool isLeaderPartyRemoved)
        {
            try
            {
                if (army == null)
                {
                    return;
                }

                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment == null || !enlistment.IsEnlisted)
                {
                    return;
                }

                var lord = enlistment.EnlistedLord;
                var lordParty = lord?.PartyBelongedTo;

                // Only report player's lord's army dispersing
                if (army.LeaderParty != lordParty)
                {
                    return;
                }

                var placeholders = new Dictionary<string, string>
                {
                    { "LORD", lord?.Name?.ToString() ?? new TextObject("{=enl_ui_unknown}Unknown").ToString() }
                };

                AddPersonalNews("army", "News_ArmyDisbanded", placeholders, $"army:{lord?.StringId}", 2);
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Error in OnArmyDispersed", ex);
            }
        }

        /// <summary>
        /// Village being raided: generate raid news for kingdom villages.
        /// </summary>
        private void OnVillageRaided(Village village)
        {
            try
            {
                var playerKingdom = GetPlayerEnlistedKingdom();
                if (playerKingdom == null || village == null)
                {
                    return;
                }

                // Only report our kingdom's villages being raided
                if (village.Settlement?.MapFaction != playerKingdom)
                {
                    return;
                }

                var placeholders = new Dictionary<string, string>
                {
                    { "SETTLEMENT", village.Name?.ToString() ?? new TextObject("{=enl_ui_unknown}Unknown").ToString() }
                };

                // Use village settlement StringId for deduplication
                var villageId = village.Settlement?.StringId ?? village.Name?.ToString() ?? "unknown";
                AddKingdomNews("raid", "News_Raid", placeholders, $"raid:{villageId}:{(int)CampaignTime.Now.ToDays}");
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Error in OnVillageRaided", ex);
            }
        }

        /// <summary>
        /// War declared: generate war news if player's kingdom is involved.
        /// </summary>
        private void OnWarDeclared(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail detail)
        {
            try
            {
                var playerKingdom = GetPlayerEnlistedKingdom();
                if (playerKingdom == null)
                {
                    return;
                }

                // Only report if player's kingdom is involved
                if (faction1 != playerKingdom && faction2 != playerKingdom)
                {
                    return;
                }

                var placeholders = new Dictionary<string, string>
                {
                    { "KINGDOM_A", faction1?.Name?.ToString() ?? new TextObject("{=enl_ui_unknown}Unknown").ToString() },
                    { "KINGDOM_B", faction2?.Name?.ToString() ?? new TextObject("{=enl_ui_unknown}Unknown").ToString() }
                };

                AddKingdomNews("war", "News_War", placeholders, $"war:{faction1?.StringId}:{faction2?.StringId}", 2);
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Error in OnWarDeclared", ex);
            }
        }

        /// <summary>
        /// Peace made: generate peace news if player's kingdom is involved.
        /// </summary>
        private void OnPeaceMade(IFaction faction1, IFaction faction2, MakePeaceAction.MakePeaceDetail detail)
        {
            try
            {
                var playerKingdom = GetPlayerEnlistedKingdom();
                if (playerKingdom == null)
                {
                    return;
                }

                // Only report if player's kingdom is involved
                if (faction1 != playerKingdom && faction2 != playerKingdom)
                {
                    return;
                }

                // Show the OTHER kingdom (the one we made peace with)
                var otherKingdom = faction1 == playerKingdom ? faction2 : faction1;
                var placeholders = new Dictionary<string, string>
                {
                    { "KINGDOM", otherKingdom?.Name?.ToString() ?? new TextObject("{=enl_ui_unknown}Unknown").ToString() }
                };

                AddKingdomNews("peace", "News_Peace", placeholders, $"peace:{faction1?.StringId}:{faction2?.StringId}", 2);
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Error in OnPeaceMade", ex);
            }
        }

        #endregion

        #region News Generation Helpers

        /// <summary>
        /// Adds a news item to the kingdom feed with deduplication.
        /// </summary>
        /// <param name="category">Category for filtering (e.g., "war", "battle", "prisoner").</param>
        /// <param name="headlineKey">Localization string ID.</param>
        /// <param name="placeholders">Placeholder values for the headline.</param>
        /// <param name="storyKey">Optional key for deduplication.</param>
        /// <param name="minDisplayDays">Minimum number of whole days this item must remain visible once shown.</param>
        /// <param name="severity">Severity level for color coding and persistence (0=Normal, 1=Positive, 2=Attention, 3=Urgent, 4=Critical).</param>
        private void AddKingdomNews(
            string category,
            string headlineKey,
            Dictionary<string, string> placeholders,
            string storyKey = null,
            int minDisplayDays = 1,
            int severity = 0)
        {
            if (!IsEnlisted())
            {
                return;
            }

            var effectiveStoryKey = storyKey ?? $"{category}:{Guid.NewGuid()}";

            // Story updates:
            // If we already have an item with this storyKey, update it (this simulates "until something else happens").
            if (!string.IsNullOrEmpty(storyKey))
            {
                var existingIndex = _kingdomFeed.FindIndex(x => x.StoryKey == storyKey);
                if (existingIndex >= 0)
                {
                    var updated = _kingdomFeed[existingIndex];
                    updated.DayCreated = (int)CampaignTime.Now.ToDays;
                    updated.Category = category;
                    updated.HeadlineKey = headlineKey;
                    updated.PlaceholderValues = placeholders ?? new Dictionary<string, string>();
                    updated.StoryKey = effectiveStoryKey;
                    updated.Type = DispatchType.Report;
                    updated.Confidence = 100;
                    updated.MinDisplayDays = Math.Max(1, minDisplayDays);
                    updated.FirstShownDay = -1; // reset visibility window
                    updated.Severity = severity;

                    _kingdomFeed[existingIndex] = updated;
                    ModLogger.Info(LogCategory, $"Kingdom news updated: {headlineKey} ({category})");
                    return;
                }
            }

            var item = new DispatchItem
            {
                DayCreated = (int)CampaignTime.Now.ToDays,
                Category = category,
                HeadlineKey = headlineKey,
                PlaceholderValues = placeholders ?? new Dictionary<string, string>(),
                StoryKey = effectiveStoryKey,
                Type = DispatchType.Report,
                Confidence = 100,
                MinDisplayDays = Math.Max(1, minDisplayDays),
                FirstShownDay = -1,
                Severity = severity
            };

            _kingdomFeed.Add(item);
            ModLogger.Info(LogCategory, $"Kingdom news: {headlineKey} ({category})");
        }

        /// <summary>
        /// Classifies a battle outcome based on casualty rates.
        /// </summary>
        /// <param name="mapEvent">The battle map event.</param>
        /// <param name="snapshot">Cached initial troop strengths.</param>
        /// <param name="winnerSide">Which side won the battle.</param>
        /// <returns>Localization key for the appropriate headline.</returns>
        private static string ClassifyBattle(MapEvent mapEvent, BattleSnapshot snapshot, BattleSideEnum winnerSide)
        {
            try
            {
                var winnerIsAttacker = winnerSide == BattleSideEnum.Attacker;
                var winnerFinal = winnerIsAttacker
                    ? mapEvent.AttackerSide?.TroopCount ?? 0
                    : mapEvent.DefenderSide?.TroopCount ?? 0;
                var loserFinal = winnerIsAttacker
                    ? mapEvent.DefenderSide?.TroopCount ?? 0
                    : mapEvent.AttackerSide?.TroopCount ?? 0;

                var winnerInitial = winnerIsAttacker
                    ? snapshot.AttackerInitialStrength
                    : snapshot.DefenderInitialStrength;
                var loserInitial = winnerIsAttacker
                    ? snapshot.DefenderInitialStrength
                    : snapshot.AttackerInitialStrength;

                // If we don't have initial data, default to clean victory
                if (winnerInitial == 0 || loserInitial == 0)
                {
                    return "News_Victory";
                }

                var winnerLosses = winnerInitial - winnerFinal;
                var loserLosses = loserInitial - loserFinal;

                var winnerLossRate = (float)winnerLosses / Math.Max(1, winnerInitial);
                var loserLossRate = (float)loserLosses / Math.Max(1, loserInitial);

                // Mutual ruin: Both sides lost 60%+ troops
                if (winnerLossRate >= 0.60f && loserLossRate >= 0.60f)
                {
                    return "News_Butchery";
                }

                // Pyrrhic victory: Winner lost 45%+ and loser lost 55%+
                if (winnerLossRate >= 0.45f && loserLossRate >= 0.55f)
                {
                    return "News_Pyrrhic";
                }

                // Costly victory: Winner lost 20-45%
                if (winnerLossRate >= 0.20f && winnerLossRate < 0.45f)
                {
                    return "News_Costly";
                }

                // Clean victory: Winner lost <20%
                return "News_Victory";
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Error classifying battle", ex);
                return "News_Victory";
            }
        }

        /// <summary>
        /// Adds battle news with winner, loser, and place information.
        /// </summary>
        /// <param name="mapEvent">The battle map event.</param>
        /// <param name="headlineKey">The localization key for the headline.</param>
        /// <param name="winnerSide">Which side won (null for inconclusive).</param>
        private void AddBattleNews(MapEvent mapEvent, string headlineKey, BattleSideEnum? winnerSide)
        {
            try
            {
                var placeholders = new Dictionary<string, string>();

                if (winnerSide.HasValue)
                {
                    // Get the winning and losing sides based on battle outcome
                    var winnerSideData = winnerSide.Value == BattleSideEnum.Attacker
                        ? mapEvent.AttackerSide
                        : mapEvent.DefenderSide;
                    var loserSideData = winnerSide.Value == BattleSideEnum.Attacker
                        ? mapEvent.DefenderSide
                        : mapEvent.AttackerSide;

                    // Use hero name if available, otherwise fall back to party name (handles bandits, looters, etc.)
                    placeholders["WINNER"] = winnerSideData?.LeaderParty?.LeaderHero?.Name?.ToString()
                                             ?? winnerSideData?.LeaderParty?.Name?.ToString()
                                             ?? new TextObject("{=enl_news_unknown_forces}unknown forces").ToString();
                    placeholders["LOSER"] = loserSideData?.LeaderParty?.LeaderHero?.Name?.ToString()
                                            ?? loserSideData?.LeaderParty?.Name?.ToString()
                                            ?? new TextObject("{=enl_news_unknown_forces}unknown forces").ToString();
                }

                // Get place name (settlement or region)
                var place = new TextObject("{=enl_news_countryside}the countryside").ToString();
                if (mapEvent.MapEventSettlement != null)
                {
                    place = mapEvent.MapEventSettlement.Name?.ToString() ?? place;
                }

                placeholders["PLACE"] = place;

                // Battle news doesn't dedupe - each battle is unique
                // Battles are high-signal: keep visible for 2 days.
                AddKingdomNews("battle", headlineKey, placeholders, null, 2);
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Error adding battle news", ex);
            }
        }

        /// <summary>
        /// Adds a news item to the personal feed with deduplication.
        /// </summary>
        /// <param name="category">Category for filtering (e.g., "army", "participation").</param>
        /// <param name="headlineKey">Localization string ID.</param>
        /// <param name="placeholders">Placeholder values for the headline.</param>
        /// <param name="storyKey">Optional key for deduplication.</param>
        /// <param name="minDisplayDays">Minimum number of whole days this item must remain visible once shown.</param>
        /// <param name="severity">Severity level for color coding and persistence (0=Normal, 1=Positive, 2=Attention, 3=Urgent, 4=Critical).</param>
        private void AddPersonalNews(
            string category,
            string headlineKey,
            Dictionary<string, string> placeholders,
            string storyKey = null,
            int minDisplayDays = 1,
            int severity = 0)
        {
            if (!IsEnlisted())
            {
                return;
            }

            var effectiveStoryKey = storyKey ?? $"{category}:{Guid.NewGuid()}";

            // Story updates (see AddKingdomNews).
            if (!string.IsNullOrEmpty(storyKey))
            {
                var existingIndex = _personalFeed.FindIndex(x => x.StoryKey == storyKey);
                if (existingIndex >= 0)
                {
                    var updated = _personalFeed[existingIndex];
                    updated.DayCreated = (int)CampaignTime.Now.ToDays;
                    updated.Category = category;
                    updated.HeadlineKey = headlineKey;
                    updated.PlaceholderValues = placeholders ?? new Dictionary<string, string>();
                    updated.StoryKey = effectiveStoryKey;
                    updated.Type = DispatchType.Report;
                    updated.Confidence = 100;
                    updated.MinDisplayDays = Math.Max(1, minDisplayDays);
                    updated.FirstShownDay = -1;
                    updated.Severity = severity;

                    _personalFeed[existingIndex] = updated;
                    ModLogger.Info(LogCategory, $"Personal news updated: {headlineKey} ({category})");
                    return;
                }
            }

            var item = new DispatchItem
            {
                DayCreated = (int)CampaignTime.Now.ToDays,
                Category = category,
                HeadlineKey = headlineKey,
                PlaceholderValues = placeholders ?? new Dictionary<string, string>(),
                StoryKey = effectiveStoryKey,
                Type = DispatchType.Report,
                Confidence = 100,
                MinDisplayDays = Math.Max(1, minDisplayDays),
                FirstShownDay = -1,
                Severity = severity
            };

            _personalFeed.Add(item);
            ModLogger.Info(LogCategory, $"Personal news: {headlineKey} ({category})");
        }

        #endregion

        #region Internal Helpers

        /// <summary>
        /// Checks if the player is currently enlisted.
        /// </summary>
        private static bool IsEnlisted()
        {
            return EnlistmentBehavior.Instance?.IsEnlisted == true;
        }

        /// <summary>
        /// Gets the player's enlisted kingdom (if any).
        /// </summary>
        private static Kingdom GetPlayerEnlistedKingdom()
        {
            var lord = EnlistmentBehavior.Instance?.EnlistedLord;
            return lord?.MapFaction as Kingdom;
        }

        /// <summary>
        /// Formats a dispatch item into display text using localization.
        /// </summary>
        private static string FormatDispatchItem(DispatchItem item)
        {
            try
            {
                if (string.IsNullOrEmpty(item.HeadlineKey))
                {
                    return string.Empty;
                }

                // WORKAROUND: Use hardcoded templates instead of localization IDs
                // Bannerlord's LocalizedTextManager doesn't find our enlisted_strings.xml entries,
                // possibly because the XML isn't being loaded into the translation system correctly.
                // For now, use direct template strings with placeholders.
                string template = GetNewsTemplate(item.HeadlineKey);
                var textObj = new TextObject(template);

                // Apply placeholder values
                if (item.PlaceholderValues != null)
                {
                    foreach (var kvp in item.PlaceholderValues)
                    {
                        textObj.SetTextVariable(kvp.Key, kvp.Value);
                    }
                }

                var resolved = textObj.ToString();

                return string.IsNullOrWhiteSpace(resolved) ? item.HeadlineKey : resolved;
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, $"Error formatting dispatch item {item.HeadlineKey}", ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// Gets the hardcoded news template for a given headline key.
        /// WORKAROUND: Bannerlord's localization system doesn't load our XML strings properly.
        /// </summary>
        private static string GetNewsTemplate(string headlineKey)
        {
            return headlineKey switch
            {
                // Kingdom battles
                "News_Victory" => "{WINNER} defeated {LOSER} near {PLACE}.",
                "News_Costly" => "{WINNER} won a costly battle against {LOSER} at {PLACE}.",
                "News_Pyrrhic" => "Pyrrhic victory at {PLACE}: {WINNER} beat {LOSER}, but at great cost.",
                "News_Butchery" => "Butchery at {PLACE}: {WINNER} and {LOSER} bled each other dry.",
                "News_Inconclusive" => "Inconclusive fighting near {PLACE}.",

                // Kingdom diplomacy
                "News_War" => "War declared between {KINGDOM_A} and {KINGDOM_B}.",
                "News_Peace" => "Peace concluded with {KINGDOM}.",

                // Kingdom sieges / settlements / raids
                "News_Siege" => "Siege underway at {SETTLEMENT}.",
                "News_Captured" => "{SETTLEMENT} captured by {KINGDOM}.",
                "News_Fallen" => "{SETTLEMENT} fell to {KINGDOM}.",
                "News_Raid" => "{SETTLEMENT} was raided.",

                // Kingdom prisoners / executions
                "News_PrisonerTaken" => "{LORD} was taken prisoner.",
                "News_PrisonerCapturedBy" => "{LORD} was captured by {CAPTOR}.",
                "News_PrisonerReleased" => "{LORD} was released from captivity.",
                "News_PrisonerEscaped" => "{LORD} escaped captivity.",
                "News_Executed" => "{LORD} was executed by {EXECUTOR}.",
                "News_ExecutedNoExecutor" => "{LORD} was executed.",

                // Personal feed
                "News_PlayerBattle" => "We helped secure victory at {PLACE}.",
                "News_PlayerDefeat" => "We were driven from {PLACE}.",
                "News_ArmyForming" => "{LORD} is gathering an army.",
                "News_ArmyDisbanded" => "{LORD}'s army dispersed.",

                // Fallback
                _ => headlineKey
            };
        }

        /// <summary>
        /// Trims both feeds to their maximum allowed sizes.
        /// </summary>
        private void TrimFeeds()
        {
            if (_kingdomFeed != null && _kingdomFeed.Count > MaxKingdomFeedItems)
            {
                _kingdomFeed = _kingdomFeed
                    .OrderByDescending(x => x.DayCreated)
                    .Take(MaxKingdomFeedItems)
                    .ToList();
            }

            if (_personalFeed != null && _personalFeed.Count > MaxPersonalFeedItems)
            {
                // Sort by severity first (keep important news), then by date (keep recent news).
                // Critical/Urgent events survive trimming even if older than Normal events.
                _personalFeed = _personalFeed
                    .OrderByDescending(x => x.Severity)
                    .ThenByDescending(x => x.DayCreated)
                    .Take(MaxPersonalFeedItems)
                    .ToList();
            }
        }

        #endregion

        #region Save/Load Helpers

        /// <summary>
        /// Saves a single dispatch item to the data store.
        /// </summary>
        private static void SaveDispatchItem(IDataStore dataStore, string prefix, DispatchItem item)
        {
            var dayCreated = item.DayCreated;
            var category = item.Category ?? string.Empty;
            var headlineKey = item.HeadlineKey ?? string.Empty;
            var storyKey = item.StoryKey ?? string.Empty;
            var type = (int)item.Type;
            var confidence = item.Confidence;
            var minDisplayDays = Math.Max(1, item.MinDisplayDays);
            var firstShownDay = item.FirstShownDay;
            var severity = item.Severity;

            dataStore.SyncData($"{prefix}_day", ref dayCreated);
            dataStore.SyncData($"{prefix}_cat", ref category);
            dataStore.SyncData($"{prefix}_key", ref headlineKey);
            dataStore.SyncData($"{prefix}_story", ref storyKey);
            dataStore.SyncData($"{prefix}_type", ref type);
            dataStore.SyncData($"{prefix}_conf", ref confidence);
            dataStore.SyncData($"{prefix}_minDays", ref minDisplayDays);
            dataStore.SyncData($"{prefix}_shownDay", ref firstShownDay);
            dataStore.SyncData($"{prefix}_severity", ref severity);

            // Save placeholder values as a count + individual key-value pairs
            var placeholderCount = item.PlaceholderValues?.Count ?? 0;
            dataStore.SyncData($"{prefix}_phCount", ref placeholderCount);

            if (item.PlaceholderValues != null)
            {
                var placeholderList = item.PlaceholderValues.ToList();
                for (var i = 0; i < placeholderCount; i++)
                {
                    var phKey = placeholderList[i].Key;
                    var phVal = placeholderList[i].Value ?? string.Empty;
                    dataStore.SyncData($"{prefix}_ph_{i}_k", ref phKey);
                    dataStore.SyncData($"{prefix}_ph_{i}_v", ref phVal);
                }
            }
        }

        /// <summary>
        /// Loads a single dispatch item from the data store.
        /// </summary>
        private static DispatchItem LoadDispatchItem(IDataStore dataStore, string prefix)
        {
            var dayCreated = 0;
            var category = string.Empty;
            var headlineKey = string.Empty;
            var storyKey = string.Empty;
            var type = 0;
            var confidence = 100;
            var minDisplayDays = 1;
            var firstShownDay = -1;
            var severity = 0;

            dataStore.SyncData($"{prefix}_day", ref dayCreated);
            dataStore.SyncData($"{prefix}_cat", ref category);
            dataStore.SyncData($"{prefix}_key", ref headlineKey);
            dataStore.SyncData($"{prefix}_story", ref storyKey);
            dataStore.SyncData($"{prefix}_type", ref type);
            dataStore.SyncData($"{prefix}_conf", ref confidence);
            dataStore.SyncData($"{prefix}_minDays", ref minDisplayDays);
            dataStore.SyncData($"{prefix}_shownDay", ref firstShownDay);
            dataStore.SyncData($"{prefix}_severity", ref severity);

            // Load placeholder values
            var placeholderCount = 0;
            dataStore.SyncData($"{prefix}_phCount", ref placeholderCount);

            var placeholders = new Dictionary<string, string>();
            for (var i = 0; i < placeholderCount; i++)
            {
                var phKey = string.Empty;
                var phVal = string.Empty;
                dataStore.SyncData($"{prefix}_ph_{i}_k", ref phKey);
                dataStore.SyncData($"{prefix}_ph_{i}_v", ref phVal);
                if (!string.IsNullOrEmpty(phKey))
                {
                    placeholders[phKey] = phVal;
                }
            }

            return new DispatchItem
            {
                DayCreated = dayCreated,
                Category = category,
                HeadlineKey = headlineKey,
                PlaceholderValues = placeholders,
                StoryKey = storyKey,
                Type = (DispatchType)type,
                Confidence = confidence,
                MinDisplayDays = Math.Max(1, minDisplayDays),
                FirstShownDay = firstShownDay,
                Severity = severity
            };
        }

        /// <summary>
        /// Saves a battle snapshot to the data store.
        /// </summary>
        private static void SaveBattleSnapshot(IDataStore dataStore, string prefix, BattleSnapshot snapshot)
        {
            var mapEventId = snapshot.MapEventId ?? string.Empty;
            var attackerStrength = snapshot.AttackerInitialStrength;
            var defenderStrength = snapshot.DefenderInitialStrength;

            dataStore.SyncData($"{prefix}_id", ref mapEventId);
            dataStore.SyncData($"{prefix}_atk", ref attackerStrength);
            dataStore.SyncData($"{prefix}_def", ref defenderStrength);
        }

        /// <summary>
        /// Loads a battle snapshot from the data store.
        /// </summary>
        private static BattleSnapshot LoadBattleSnapshot(IDataStore dataStore, string prefix)
        {
            var mapEventId = string.Empty;
            var attackerStrength = 0;
            var defenderStrength = 0;

            dataStore.SyncData($"{prefix}_id", ref mapEventId);
            dataStore.SyncData($"{prefix}_atk", ref attackerStrength);
            dataStore.SyncData($"{prefix}_def", ref defenderStrength);

            return new BattleSnapshot
            {
                MapEventId = mapEventId,
                AttackerInitialStrength = attackerStrength,
                DefenderInitialStrength = defenderStrength
            };
        }

        /// <summary>
        /// Saves an order outcome record to the data store.
        /// </summary>
        private static void SaveOrderOutcomeRecord(IDataStore dataStore, string prefix, OrderOutcomeRecord record)
        {
            var orderTitle = record.OrderTitle ?? string.Empty;
            var success = record.Success;
            var briefSummary = record.BriefSummary ?? string.Empty;
            var detailedSummary = record.DetailedSummary ?? string.Empty;
            var issuer = record.Issuer ?? string.Empty;
            var dayNumber = record.DayNumber;

            dataStore.SyncData($"{prefix}_title", ref orderTitle);
            dataStore.SyncData($"{prefix}_success", ref success);
            dataStore.SyncData($"{prefix}_brief", ref briefSummary);
            dataStore.SyncData($"{prefix}_detail", ref detailedSummary);
            dataStore.SyncData($"{prefix}_issuer", ref issuer);
            dataStore.SyncData($"{prefix}_day", ref dayNumber);
        }

        /// <summary>
        /// Loads an order outcome record from the data store.
        /// </summary>
        private static OrderOutcomeRecord LoadOrderOutcomeRecord(IDataStore dataStore, string prefix)
        {
            var orderTitle = string.Empty;
            var success = false;
            var briefSummary = string.Empty;
            var detailedSummary = string.Empty;
            var issuer = string.Empty;
            var dayNumber = 0;

            dataStore.SyncData($"{prefix}_title", ref orderTitle);
            dataStore.SyncData($"{prefix}_success", ref success);
            dataStore.SyncData($"{prefix}_brief", ref briefSummary);
            dataStore.SyncData($"{prefix}_detail", ref detailedSummary);
            dataStore.SyncData($"{prefix}_issuer", ref issuer);
            dataStore.SyncData($"{prefix}_day", ref dayNumber);

            return new OrderOutcomeRecord
            {
                OrderTitle = orderTitle,
                Success = success,
                BriefSummary = briefSummary,
                DetailedSummary = detailedSummary,
                Issuer = issuer,
                DayNumber = dayNumber
            };
        }

        /// <summary>
        /// Saves a reputation change record to the data store.
        /// </summary>
        private static void SaveReputationChangeRecord(IDataStore dataStore, string prefix, ReputationChangeRecord record)
        {
            var target = record.Target ?? string.Empty;
            var delta = record.Delta;
            var newValue = record.NewValue;
            var message = record.Message ?? string.Empty;
            var dayNumber = record.DayNumber;

            dataStore.SyncData($"{prefix}_target", ref target);
            dataStore.SyncData($"{prefix}_delta", ref delta);
            dataStore.SyncData($"{prefix}_newVal", ref newValue);
            dataStore.SyncData($"{prefix}_msg", ref message);
            dataStore.SyncData($"{prefix}_day", ref dayNumber);
        }

        /// <summary>
        /// Loads a reputation change record from the data store.
        /// </summary>
        private static ReputationChangeRecord LoadReputationChangeRecord(IDataStore dataStore, string prefix)
        {
            var target = string.Empty;
            var delta = 0;
            var newValue = 0;
            var message = string.Empty;
            var dayNumber = 0;

            dataStore.SyncData($"{prefix}_target", ref target);
            dataStore.SyncData($"{prefix}_delta", ref delta);
            dataStore.SyncData($"{prefix}_newVal", ref newValue);
            dataStore.SyncData($"{prefix}_msg", ref message);
            dataStore.SyncData($"{prefix}_day", ref dayNumber);

            return new ReputationChangeRecord
            {
                Target = target,
                Delta = delta,
                NewValue = newValue,
                Message = message,
                DayNumber = dayNumber
            };
        }

        /// <summary>
        /// Saves a company need change record to the data store.
        /// </summary>
        private static void SaveCompanyNeedChangeRecord(IDataStore dataStore, string prefix, CompanyNeedChangeRecord record)
        {
            var need = record.Need ?? string.Empty;
            var delta = record.Delta;
            var oldValue = record.OldValue;
            var newValue = record.NewValue;
            var message = record.Message ?? string.Empty;
            var dayNumber = record.DayNumber;

            dataStore.SyncData($"{prefix}_need", ref need);
            dataStore.SyncData($"{prefix}_delta", ref delta);
            dataStore.SyncData($"{prefix}_oldVal", ref oldValue);
            dataStore.SyncData($"{prefix}_newVal", ref newValue);
            dataStore.SyncData($"{prefix}_msg", ref message);
            dataStore.SyncData($"{prefix}_day", ref dayNumber);
        }

        /// <summary>
        /// Loads a company need change record from the data store.
        /// </summary>
        private static CompanyNeedChangeRecord LoadCompanyNeedChangeRecord(IDataStore dataStore, string prefix)
        {
            var need = string.Empty;
            var delta = 0;
            var oldValue = 0;
            var newValue = 0;
            var message = string.Empty;
            var dayNumber = 0;

            dataStore.SyncData($"{prefix}_need", ref need);
            dataStore.SyncData($"{prefix}_delta", ref delta);
            dataStore.SyncData($"{prefix}_oldVal", ref oldValue);
            dataStore.SyncData($"{prefix}_newVal", ref newValue);
            dataStore.SyncData($"{prefix}_msg", ref message);
            dataStore.SyncData($"{prefix}_day", ref dayNumber);

            return new CompanyNeedChangeRecord
            {
                Need = need,
                Delta = delta,
                OldValue = oldValue,
                NewValue = newValue,
                Message = message,
                DayNumber = dayNumber
            };
        }

        /// <summary>
        /// Saves an event outcome record to the data store.
        /// </summary>
        private static void SaveEventOutcomeRecord(IDataStore dataStore, string prefix, EventOutcomeRecord record)
        {
            var eventId = record.EventId ?? string.Empty;
            var eventTitle = record.EventTitle ?? string.Empty;
            var optionChosen = record.OptionChosen ?? string.Empty;
            var outcomeSummary = record.OutcomeSummary ?? string.Empty;
            var resultNarrative = record.ResultNarrative ?? string.Empty;
            var dayNumber = record.DayNumber;
            var isCurrentlyShown = record.IsCurrentlyShown;
            var dayShown = record.DayShown;

            dataStore.SyncData($"{prefix}_eventId", ref eventId);
            dataStore.SyncData($"{prefix}_title", ref eventTitle);
            dataStore.SyncData($"{prefix}_option", ref optionChosen);
            dataStore.SyncData($"{prefix}_summary", ref outcomeSummary);
            dataStore.SyncData($"{prefix}_narrative", ref resultNarrative);
            dataStore.SyncData($"{prefix}_day", ref dayNumber);
            dataStore.SyncData($"{prefix}_isShown", ref isCurrentlyShown);
            dataStore.SyncData($"{prefix}_dayShown", ref dayShown);

            // Save effects dictionary as key-value pairs
            var effectsCount = record.EffectsApplied?.Count ?? 0;
            dataStore.SyncData($"{prefix}_effectsCount", ref effectsCount);

            if (record.EffectsApplied != null)
            {
                var i = 0;
                foreach (var kvp in record.EffectsApplied)
                {
                    var key = kvp.Key;
                    var value = kvp.Value;
                    dataStore.SyncData($"{prefix}_eff_{i}_k", ref key);
                    dataStore.SyncData($"{prefix}_eff_{i}_v", ref value);
                    i++;
                }
            }
        }

        /// <summary>
        /// Loads an event outcome record from the data store.
        /// </summary>
        private static EventOutcomeRecord LoadEventOutcomeRecord(IDataStore dataStore, string prefix)
        {
            var eventId = string.Empty;
            var eventTitle = string.Empty;
            var optionChosen = string.Empty;
            var outcomeSummary = string.Empty;
            var resultNarrative = string.Empty;
            var dayNumber = 0;
            var isCurrentlyShown = false;
            var dayShown = -1;

            dataStore.SyncData($"{prefix}_eventId", ref eventId);
            dataStore.SyncData($"{prefix}_title", ref eventTitle);
            dataStore.SyncData($"{prefix}_option", ref optionChosen);
            dataStore.SyncData($"{prefix}_summary", ref outcomeSummary);
            dataStore.SyncData($"{prefix}_narrative", ref resultNarrative);
            dataStore.SyncData($"{prefix}_day", ref dayNumber);
            dataStore.SyncData($"{prefix}_isShown", ref isCurrentlyShown);
            dataStore.SyncData($"{prefix}_dayShown", ref dayShown);

            // Load effects dictionary
            var effectsCount = 0;
            dataStore.SyncData($"{prefix}_effectsCount", ref effectsCount);

            var effects = new Dictionary<string, int>();
            for (var i = 0; i < effectsCount; i++)
            {
                var key = string.Empty;
                var value = 0;
                dataStore.SyncData($"{prefix}_eff_{i}_k", ref key);
                dataStore.SyncData($"{prefix}_eff_{i}_v", ref value);
                if (!string.IsNullOrEmpty(key))
                {
                    effects[key] = value;
                }
            }

            return new EventOutcomeRecord
            {
                EventId = eventId,
                EventTitle = eventTitle,
                OptionChosen = optionChosen,
                OutcomeSummary = outcomeSummary,
                ResultNarrative = resultNarrative,
                DayNumber = dayNumber,
                EffectsApplied = effects,
                IsCurrentlyShown = isCurrentlyShown,
                DayShown = dayShown
            };
        }

        /// <summary>
        /// Saves a pending event record to the data store.
        /// </summary>
        private static void SavePendingEventRecord(IDataStore dataStore, string prefix, PendingEventRecord record)
        {
            var sourceEventId = record.SourceEventId ?? string.Empty;
            var chainEventId = record.ChainEventId ?? string.Empty;
            var contextHint = record.ContextHint ?? string.Empty;
            var scheduledDay = record.ScheduledDay;
            var createdDay = record.CreatedDay;

            dataStore.SyncData($"{prefix}_sourceId", ref sourceEventId);
            dataStore.SyncData($"{prefix}_chainId", ref chainEventId);
            dataStore.SyncData($"{prefix}_hint", ref contextHint);
            dataStore.SyncData($"{prefix}_scheduled", ref scheduledDay);
            dataStore.SyncData($"{prefix}_created", ref createdDay);
        }

        /// <summary>
        /// Loads a pending event record from the data store.
        /// </summary>
        private static PendingEventRecord LoadPendingEventRecord(IDataStore dataStore, string prefix)
        {
            var sourceEventId = string.Empty;
            var chainEventId = string.Empty;
            var contextHint = string.Empty;
            var scheduledDay = 0;
            var createdDay = 0;

            dataStore.SyncData($"{prefix}_sourceId", ref sourceEventId);
            dataStore.SyncData($"{prefix}_chainId", ref chainEventId);
            dataStore.SyncData($"{prefix}_hint", ref contextHint);
            dataStore.SyncData($"{prefix}_scheduled", ref scheduledDay);
            dataStore.SyncData($"{prefix}_created", ref createdDay);

            return new PendingEventRecord
            {
                SourceEventId = sourceEventId,
                ChainEventId = chainEventId,
                ContextHint = contextHint,
                ScheduledDay = scheduledDay,
                CreatedDay = createdDay
            };
        }

        #endregion
    }

    /// <summary>
    /// Represents a single news dispatch item.
    /// Uses primitive-friendly structure for save/load compatibility.
    /// </summary>
    public struct DispatchItem : IEquatable<DispatchItem>
    {
        /// <summary>
        /// Campaign day when this item was created.
        /// </summary>
        public int DayCreated { get; set; }

        /// <summary>
        /// Category for filtering/grouping (e.g., "war", "battle", "prisoner", "siege").
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Localization string ID (e.g., "News_Victory", "News_Peace").
        /// </summary>
        public string HeadlineKey { get; set; }

        /// <summary>
        /// Placeholder values for localization (e.g., {"WINNER": "Derthert", "PLACE": "Marunath"}).
        /// </summary>
        public Dictionary<string, string> PlaceholderValues { get; set; }

        /// <summary>
        /// Story key for deduplication (e.g., "siege:town_1", "prisoner:hero_123").
        /// </summary>
        public string StoryKey { get; set; }

        /// <summary>
        /// Type of dispatch (Report, Rumor, Bulletin).
        /// </summary>
        public DispatchType Type { get; set; }

        /// <summary>
        /// Confidence level 0-100 (primarily for rumors).
        /// </summary>
        public int Confidence { get; set; }

        /// <summary>
        /// Minimum number of whole days this item must stay visible once it is shown.
        /// 1 = at least today; 2 = today and tomorrow.
        /// </summary>
        public int MinDisplayDays { get; set; }

        /// <summary>
        /// The first campaign day this item was displayed in a menu.
        /// -1 means it has not been shown yet (backlogged).
        /// </summary>
        public int FirstShownDay { get; set; }

        /// <summary>
        /// Severity level for color coding and persistence duration.
        /// 0=Normal, 1=Positive, 2=Attention, 3=Urgent, 4=Critical.
        /// Higher severity items persist longer and cannot be replaced by lower severity items.
        /// </summary>
        public int Severity { get; set; }

        public bool Equals(DispatchItem other)
        {
            return DayCreated == other.DayCreated &&
                   Category == other.Category &&
                   HeadlineKey == other.HeadlineKey &&
                   StoryKey == other.StoryKey &&
                   Type == other.Type &&
                   Confidence == other.Confidence &&
                   MinDisplayDays == other.MinDisplayDays &&
                   FirstShownDay == other.FirstShownDay &&
                   Severity == other.Severity;
        }

        public override bool Equals(object obj)
        {
            return obj is DispatchItem other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + DayCreated;
                hash = hash * 31 + (Category?.GetHashCode() ?? 0);
                hash = hash * 31 + (HeadlineKey?.GetHashCode() ?? 0);
                hash = hash * 31 + (StoryKey?.GetHashCode() ?? 0);
                hash = hash * 31 + (int)Type;
                hash = hash * 31 + Confidence;
                hash = hash * 31 + MinDisplayDays;
                hash = hash * 31 + FirstShownDay;
                hash = hash * 31 + Severity;
                return hash;
            }
        }
    }

    /// <summary>
    /// Type of news dispatch.
    /// </summary>
    public enum DispatchType
    {
        /// <summary>
        /// Factual report derived from a campaign event.
        /// </summary>
        Report = 0,

        /// <summary>
        /// Unconfirmed rumor that may be confirmed/denied later.
        /// </summary>
        Rumor = 1,

        /// <summary>
        /// Periodic summary bulletin (every 2 days).
        /// </summary>
        Bulletin = 2
    }

    /// <summary>
    /// Severity level for news items, determining color coding and persistence duration.
    /// </summary>
    public enum NewsSeverity
    {
        /// <summary>
        /// Normal/routine information. White text, 6h minimum display.
        /// </summary>
        Normal = 0,

        /// <summary>
        /// Good news, opportunities, rewards. Green text, 6h minimum display.
        /// </summary>
        Positive = 1,

        /// <summary>
        /// Requires attention, minor issues. Yellow text, 12h minimum display.
        /// </summary>
        Attention = 2,

        /// <summary>
        /// Problems, losses, dangers. Red text, 24h minimum display.
        /// </summary>
        Urgent = 3,

        /// <summary>
        /// Immediate threats, critical situations. Bright red text, 48h minimum display.
        /// </summary>
        Critical = 4
    }

    /// <summary>
    /// Cached battle state for pyrrhic victory detection.
    /// Stores initial troop strengths to calculate losses after battle ends.
    /// </summary>
    public struct BattleSnapshot
    {
        /// <summary>
        /// Unique identifier for the map event (hash code as string).
        /// </summary>
        public string MapEventId { get; set; }

        /// <summary>
        /// Attacker side troop count at battle start.
        /// </summary>
        public int AttackerInitialStrength { get; set; }

        /// <summary>
        /// Defender side troop count at battle start.
        /// </summary>
        public int DefenderInitialStrength { get; set; }
    }

    /// <summary>
    /// Records an order outcome for display in reports and daily brief.
    /// </summary>
    public sealed class OrderOutcomeRecord
    {
        public string OrderTitle { get; set; }
        public bool Success { get; set; }
        public string BriefSummary { get; set; }
        public string DetailedSummary { get; set; }
        public string Issuer { get; set; }
        public int DayNumber { get; set; }
    }

    /// <summary>
    /// Records a reputation change for display in reports.
    /// </summary>
    public sealed class ReputationChangeRecord
    {
        public string Target { get; set; } // "Lord", "Officer", "Soldier", "Retinue"
        public int Delta { get; set; }
        public int NewValue { get; set; }
        public string Message { get; set; }
        public int DayNumber { get; set; }
    }

    /// <summary>
    /// Records a company need change for display in reports.
    /// </summary>
    public sealed class CompanyNeedChangeRecord
    {
        public string Need { get; set; }
        public int Delta { get; set; }
        public int OldValue { get; set; }
        public int NewValue { get; set; }
        public string Message { get; set; }
        public int DayNumber { get; set; }
    }

    /// <summary>
    /// Records an event outcome for display in Personal Feed and reports.
    /// Tracks what event fired, which option was chosen, and effects applied.
    /// Uses a queue system to show one outcome at a time in Recent Activities.
    /// </summary>
    public sealed class EventOutcomeRecord
    {
        public string EventId { get; set; }
        public string EventTitle { get; set; }
        public string OptionChosen { get; set; }
        public string OutcomeSummary { get; set; }
        public int DayNumber { get; set; }

        /// <summary>
        /// The narrative result text shown to the player describing what happened.
        /// This is the RP/story outcome, not just the mechanical effects.
        /// </summary>
        public string ResultNarrative { get; set; }

        /// <summary>
        /// Individual effect values applied (for headline formatting).
        /// Keys: "Gold", "SoldierRep", "OfficerRep", "LordRep", "Scrutiny", skill names for XP.
        /// </summary>
        public Dictionary<string, int> EffectsApplied { get; set; }

        /// <summary>
        /// Severity level for color coding and persistence (0=Normal, 1=Positive, 2=Attention, 3=Urgent, 4=Critical).
        /// </summary>
        public int Severity { get; set; }

        /// <summary>
        /// Tracks whether this outcome is currently shown in Recent Activities (true) or queued (false).
        /// Only one outcome is shown at a time; others wait in queue.
        /// </summary>
        public bool IsCurrentlyShown { get; set; }

        /// <summary>
        /// The day this outcome was first displayed in Recent Activities.
        /// Used to track the 1-day visibility window before promoting the next queued outcome.
        /// -1 means it hasn't been shown yet (still in queue).
        /// </summary>
        public int DayShown { get; set; }
    }

    /// <summary>
    /// Tracks a pending chain event for context display in Daily Brief.
    /// Shows hints like "A comrade owes you money" before the event fires.
    /// </summary>
    public sealed class PendingEventRecord
    {
        public string SourceEventId { get; set; }
        public string ChainEventId { get; set; }
        public string ContextHint { get; set; }
        public int ScheduledDay { get; set; }
        public int CreatedDay { get; set; }
    }

    /// <summary>
    /// Records muster outcomes for display in camp news and daily brief.
    /// Each muster cycle generates one record summarizing pay, rations, and unit status.
    /// </summary>
    public sealed class MusterOutcomeRecord
    {
        /// <summary>Day number when this muster occurred.</summary>
        public int DayNumber { get; set; }

        /// <summary>Pay outcome: "paid", "partial", "delayed", "promissory", "corruption", etc.</summary>
        public string PayOutcome { get; set; } = string.Empty;

        /// <summary>Amount of gold received (0 if none).</summary>
        public int PayAmount { get; set; }

        /// <summary>Ration outcome: "issued", "none_low_supply", "none_critical", "officer_exempt".</summary>
        public string RationOutcome { get; set; } = string.Empty;

        /// <summary>Item ID of ration issued (empty if none).</summary>
        public string RationItemId { get; set; } = string.Empty;

        /// <summary>Company supply level at muster (0-100).</summary>
        public int SupplyLevel { get; set; }

        /// <summary>Number of soldiers lost since previous muster.</summary>
        public int LostSinceLast { get; set; }

        /// <summary>Number of soldiers sick since previous muster.</summary>
        public int SickSinceLast { get; set; }

        /// <summary>Strategic context tag (e.g., "siege", "campaign", "garrison").</summary>
        public string StrategicContext { get; set; } = string.Empty;

        /// <summary>Quartermaster reputation at time of muster.</summary>
        public int QmReputation { get; set; }

        /// <summary>Number of orders completed since last muster.</summary>
        public int OrdersCompleted { get; set; }

        /// <summary>Number of orders failed since last muster.</summary>
        public int OrdersFailed { get; set; }

        /// <summary>Baggage check outcome (e.g., "passed", "skipped").</summary>
        public string BaggageOutcome { get; set; } = string.Empty;

        /// <summary>Inspection outcome (e.g., "passed", "failed", "skipped").</summary>
        public string InspectionOutcome { get; set; } = string.Empty;

        /// <summary>Recruit outcome (e.g., "recruited", "none_available", "declined").</summary>
        public string RecruitOutcome { get; set; } = string.Empty;

        /// <summary>Promotion tier if player was promoted during this muster (0 if no promotion).</summary>
        public int PromotionTier { get; set; }

        /// <summary>Retinue strength at time of muster (for officers with retinues).</summary>
        public int RetinueStrength { get; set; }

        /// <summary>Number of retinue casualties since last muster.</summary>
        public int RetinueCasualties { get; set; }

        /// <summary>Names of fallen retinue members (for memorial display).</summary>
        public List<string> FallenRetinueNames { get; set; } = new List<string>();

        /// <summary>Flavor text describing ration quality/condition.</summary>
        public string RationFlavorText { get; set; } = string.Empty;
    }
}
