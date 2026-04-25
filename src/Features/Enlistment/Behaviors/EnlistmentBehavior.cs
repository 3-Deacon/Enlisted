using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Enlisted.Features.Combat.Behaviors;
using Enlisted.Features.Company;
using Enlisted.Features.Content;
using Enlisted.Features.Equipment.Behaviors;
using Enlisted.Features.Enlistment.Core;
using Enlisted.Features.Escalation;
using Enlisted.Features.Identity;
using Enlisted.Features.Interface.Behaviors;
using Enlisted.Features.Logistics;
using Enlisted.Features.Retinue.Core;
using Enlisted.Features.Retinue.Data;
using Enlisted.Mod.Core;
using Enlisted.Mod.Core.Config;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Entry;
using Enlisted.Mod.GameAdapters.Patches;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using ConfigurationManager = Enlisted.Mod.Core.Config.ConfigurationManager;
using EnlistedConfig = Enlisted.Mod.Core.Config.ConfigurationManager;

namespace Enlisted.Features.Enlistment.Behaviors
{
    /// <summary>
    ///     Detailed breakdown of daily wage components for tooltip display.
    ///     Each field represents a separate line item in the finance tooltip.
    /// </summary>
    public class WageBreakdown
    {
        /// <summary>Base soldier's pay (from config base_wage).</summary>
        public int BasePay { get; set; }

        /// <summary>Bonus from player's character level.</summary>
        public int LevelBonus { get; set; }

        /// <summary>Bonus from military rank/tier.</summary>
        public int TierBonus { get; set; }

        /// <summary>Bonus from accumulated service time (XP-based seniority).</summary>
        public int ServiceBonus { get; set; }

        /// <summary>Bonus for serving in an active army campaign.</summary>
        public int ArmyBonus { get; set; }

        /// <summary>Whether currently in an army.</summary>
        public bool IsInArmy { get; set; }

        /// <summary>Final total wage after all bonuses and caps.</summary>
        public int Total { get; set; }
    }

    /// <summary>
    ///     Record of a ration issued to the player at muster.
    ///     Tracks item details and timestamp for reclamation at the next muster.
    /// </summary>
    public class IssuedRationRecord
    {
        /// <summary>Item string ID (e.g., "grain", "meat", "cheese").</summary>
        public string ItemId { get; set; }

        /// <summary>Quantity issued.</summary>
        public int Amount { get; set; }

        /// <summary>Campaign time when the ration was issued.</summary>
        public CampaignTime IssuedAt { get; set; }
    }

    /// <summary>
    ///     Core behavior managing the player's enlistment in a lord's military service.
    ///     This behavior tracks the enlisted lord, manages party following and battle participation,
    ///     processes XP and promotions, handles baggage storage, and manages party activity state
    ///     to prevent unwanted encounters while allowing battle participation.
    ///     The system works by attaching the player's party to the lord's party, making the player
    ///     invisible and inactive during normal travel (preventing random encounters), but activating
    ///     the player when the lord enters battles so they can participate.
    /// </summary>
    public sealed class EnlistmentBehavior : CampaignBehaviorBase
    {
        private const string SaveKeyPrefix = "Enlisted.EnlistmentBehavior.";
        private const string GraceLordMarkerQuestId = "enlisted_grace_lord_marker";
        private const double GraceLordMarkerDistanceThreshold = 5d;

        private static string Key(string legacyKey) => $"{SaveKeyPrefix}{legacyKey}";

        private static void SyncKey<T>(IDataStore dataStore, string legacyKey, ref T value)
        {
            // On load: try legacy key first (for backwards compatibility), then prefixed key.
            // On save: only write prefixed keys to avoid collisions with other behaviors.
            if (dataStore == null || string.IsNullOrWhiteSpace(legacyKey))
            {
                return;
            }

            if (dataStore.IsLoading)
            {
                _ = dataStore.SyncData(legacyKey, ref value);
            }

            _ = dataStore.SyncData(Key(legacyKey), ref value);
        }
        /// <summary>
        ///     Minimum time interval between real-time tick updates, in seconds.
        ///     Updates are throttled to every 100ms to prevent overwhelming the game's
        ///     rendering system with too-frequent state changes, which can cause assertion failures.
        /// </summary>
        private readonly float _realtimeUpdateIntervalSeconds = 0.1f;

        /// <summary>
        ///     Tracks whether battle XP has been awarded for the current battle.
        ///     Prevents double XP awards when both OnPlayerBattleEnd and OnMapEventEnded fire.
        ///     Reset when a new battle starts.
        /// </summary>
        private bool _battleXPAwardedThisBattle;

        private MapEvent _cachedLordMapEvent;

        /// <summary>
        ///     Tracks whether a PlayerEncounter has been created for the current lord's battle.
        ///     Prevents repeated PlayerEncounter creation during the same battle (which causes loops).
        ///     Reset when the battle ends (OnMapEventEnded) or when the lord's MapEvent changes.
        /// </summary>
        private bool _playerEncounterCreatedForBattle;

        /// <summary>
        ///     Tracks whether an army was created specifically for battle participation.
        ///     If true, the army should be disbanded after the battle completes to prevent
        ///     the player from remaining in an army when not needed.
        /// </summary>
        private bool _disbandArmyAfterBattle;

        /// <summary>
        ///     Campaign time when the desertion grace period ends.
        ///     If current time exceeds this and player hasn't rejoined, desertion penalties apply.
        ///     Set when army is defeated or lord is captured, giving player 14 days to rejoin another lord in the same kingdom.
        /// </summary>
        private CampaignTime _desertionGracePeriodEnd = CampaignTime.Zero;

        /// <summary>
        ///     The lord the player is currently serving under, or null if not enlisted.
        /// </summary>
        private Hero _enlistedLord;

        /// <summary>
        ///     Campaign time when the player first enlisted with the current lord.
        ///     Used for calculating service duration and veteran status.
        /// </summary>
        private CampaignTime _enlistmentDate = CampaignTime.Zero;

        /// <summary>
        ///     Current military rank tier (1-6), where 1 is the lowest rank and 6 is the highest.
        ///     Determined by accumulated enlistment XP and tier thresholds from progression_config.json.
        /// </summary>
        private int _enlistmentTier = 1;

        /// <summary>
        ///     Total experience points accumulated during military service.
        ///     Gained from daily service, battle participation, duty performance, and special events.
        ///     Used to determine tier promotions based on thresholds in progression_config.json.
        /// </summary>
        private int _enlistmentXP;

        /// <summary>
        ///     Date of last promotion (or enlistment if T1). Used to calculate days in rank.
        /// </summary>
        private CampaignTime _lastPromotionDate = CampaignTime.Zero;

        /// <summary>
        ///     Number of battles survived since enlistment. Used for promotion requirements.
        /// </summary>
        private int _battlesSurvived;

        // Promotion can be temporarily blocked at very high discipline risk.
        // This is rate-limited to avoid message spam when earning XP while blocked.
        private CampaignTime _lastPromotionBlockedMessageTime = CampaignTime.Zero;

        /// <summary>
        ///     Tracks the current state of all four company needs for the enlisted lord's party.
        ///     Degrades daily and recovers through orders and camp activities.
        /// </summary>
        private CompanyNeedsState _companyNeeds;

        // UI anti-spam: wage breakdown is used by tooltips and can be queried frequently.
        // If it ever throws, log it once per session and fall back to a safe default breakdown.
        private bool _loggedWageBreakdownFailure;

        private CampaignTime _graceProtectionEnds = CampaignTime.Zero;

        // Deferred bag check scheduling (avoids blocking enlistment flow)
        private bool _bagCheckScheduled;
        private CampaignTime _bagCheckDueTime = CampaignTime.Zero;

        /// <summary>
        ///     Counter to skip initial ticks during loading. The game's save system runs
        ///     before our tick handler is ready, so we wait a few ticks for it to settle.
        /// </summary>
        private int _initializationTicksRemaining = 5;

        /// <summary>
        ///     Whether the player is currently on temporary leave from service.
        ///     When on leave, the player is not actively enlisted but can return without
        ///     going through the full enlistment process again.
        /// </summary>
        private bool _isOnLeave;

        /// <summary>
        ///     Tracks whether the player was a prisoner on the previous tick.
        ///     Used to detect captivity release and apply protection when transitioning
        ///     from prisoner to free during the grace period.
        /// </summary>
        private bool _wasPrisonerLastTick;

        /// <summary>
        ///     Flag to block all IsActive modifications until post-load initialization is complete.
        ///     This prevents the realtime tick from setting IsActive during the loading process.
        ///     Defaults to false (safe) - set true only after proper initialization.
        /// </summary>
        private bool _isPartyStateInitialized;

        /// <summary>
        ///     Guard flag to indicate we're processing an intentional discharge (retirement/honorable discharge).
        ///     Prevents OnClanChangedKingdom from treating the kingdom removal as desertion when the player
        ///     is legitimately ending their service through proper channels like retirement.
        /// </summary>
        private bool _isProcessingDischarge;

        // Captivity settlement log throttle to avoid spamming every hop
        private static readonly HashSet<string> _captivitySettlementsLogged = new HashSet<string>();

        /// <summary>
        ///     Last campaign time when the real-time tick update was processed.
        ///     Used with _realtimeUpdateIntervalSeconds to throttle update frequency.
        /// </summary>
        private CampaignTime _lastRealtimeUpdate = CampaignTime.Zero;

        // NOTE: Siege watchdog + latch removed.
        // We now rely on lightweight stale-state cleanup (clearing BesiegerCamp when no active siege exists)
        // and on battle end cleanup paths to keep siege/encounter menus from re-opening.

        /// <summary>
        ///     Campaign time when the player started their current leave period.
        ///     Used for tracking leave duration.
        /// </summary>
        private CampaignTime _leaveStartDate = CampaignTime.Zero;

        /// <summary>
        ///     Campaign time when the player can next request leave.
        ///     Set when returning from leave to enforce a cooldown.
        /// </summary>
        private CampaignTime _leaveCooldownEnds = CampaignTime.Zero;

        /// <summary>
        ///     Flag to indicate party state needs to be restored after save loading.
        ///     We can't set IsActive during SyncData because the game asserts !IsActive during load.
        ///     This flag triggers restoration on the first campaign tick after loading.
        /// </summary>
        private bool _needsPostLoadStateRestore;

        /// <summary>
        ///     The player's original kingdom before enlisting with the lord.
        ///     Stored so it can be restored when service ends. Null if the player was independent.
        /// </summary>
        private Kingdom _originalKingdom;

        /// <summary>
        ///     Kingdom that the player needs to rejoin during grace period to avoid desertion.
        ///     Set when army is defeated or lord is captured. Player can rejoin any lord in this kingdom
        ///     during the grace period to avoid penalties.
        /// </summary>
        private Kingdom _pendingDesertionKingdom;

        /// <summary>
        ///     Tracks pending capture cleanup when the player is taken prisoner during an encounter.
        ///     We defer enlistment teardown until the surrender/encounter menus fully close.
        /// </summary>
        private Kingdom _pendingPlayerCaptureKingdom;

        private string _pendingPlayerCaptureReason;
        private bool _pendingVisibilityRestore;
        private CampaignTime _pendingVisibilityRestoreStartTime = CampaignTime.Zero;

        // Baggage train stash for enlistment storage
        private ItemRoster _baggageStash = new ItemRoster();
        private string _baggageStashFactionId;
        private bool _bagCheckCompleted;
        private bool _bagCheckInProgress;
        private bool _bagCheckEverCompleted;
        private Hero _pendingBagCheckLord;

        // Cross-faction baggage retrieval tracking
        // When player enlists with new faction while baggage exists from previous faction,
        // they can pay to have it couriered. Items arrive after the delay expires.
        private CampaignTime _baggageCourierArrivalTime = CampaignTime.Zero;
        private ItemRoster _pendingCourierBaggage;

        private bool _playerCaptureCleanupScheduled;
        private CampaignTime _savedGraceEnlistmentDate = CampaignTime.Zero;
        private Hero _savedGraceLord;
        private int _savedGraceTier = -1;
        private string _savedGraceTroopId;
        private int _savedGraceXP;
        private GraceLordMarkerSnapshot? _lastGraceLordMarkerSnapshot;

        // Grace period tracking for lord transfers

        // Ledger-based pay scheduling (custom muster system)
        private int _pendingMusterPay;
        private CampaignTime _nextPayday = CampaignTime.Zero;
        private bool _payMusterPending;
        private string _lastPayOutcome;

        // Ration outcome tracking for muster news reporting
        private string _lastRationOutcome;
        private string _lastRationItemId;
        private int _pensionAmountPerDay;
        private string _pensionFactionId;
        private bool _isPensionPaused;
        private bool _isPendingDischarge;
        private string _lastDischargeBand;
        private bool _isOnProbation;
        private CampaignTime _probationEnds = CampaignTime.Zero;

        // Pay tension and backpay tracking
        // PayTension escalates when pay is delayed; triggers events at thresholds
        private int _payTension;
        private int _owedBackpay;
        private CampaignTime _lastPayDate = CampaignTime.Zero;
        private int _consecutiveDelays;

        // Muster tracking fields for the multi-stage muster menu system
        // These cache state from the previous muster to detect changes and build period summaries
        private int _tierAtLastMuster;
        private int _xpAtLastMuster;
        private Dictionary<string, int> _xpSourcesThisPeriod = new Dictionary<string, int>();
        private int _lastMusterDay;
        private CampaignTime _lastMusterCompletionTime = CampaignTime.Zero;
        private int _dayOfLastPromotion;
        private int _lastInspectionDay;
        private int _lastRecruitDay;

        // Ration tracking for reclamation at next muster
        // Tracks issued rations so they can be reclaimed at the next muster, preventing hoarding
        private List<IssuedRationRecord> _issuedRations = new List<IssuedRationRecord>();


        /// <summary>
        ///     Whether the player's clan was independent (no kingdom) before enlistment.
        ///     Used to determine whether to restore independence or rejoin the original kingdom
        ///     when service ends.
        /// </summary>
        private bool _wasIndependentClan;

        /// <summary>
        ///     Tracks faction StringIds for war relations we created when enlisting with a minor faction lord.
        ///     When the player enlists with a minor faction (not a Kingdom), they don't automatically get
        ///     faction relationships. We mirror the lord's faction war relations to the player clan so that
        ///     nameplates show correct ally/enemy colors. These are restored to neutral when service ends.
        /// </summary>
        private List<string> _minorFactionWarRelations = new List<string>();

        // ========================================================================
        // NCO AND SOLDIER NAME SYSTEM
        // Persistent names generated on enlistment for personalized event text.
        // NCO = Non-commissioned officer (culture-specific T5 rank + generated name)
        // Soldier names = pool of 3 comrade names used randomly in events
        // ========================================================================

        /// <summary>
        ///     First name of the NCO (non-commissioned officer) assigned to the player.
        ///     Generated on enlistment using Bannerlord's NameGenerator for the lord's culture.
        /// </summary>
        private string _ncoFirstName;

        /// <summary>
        ///     Culture-specific rank title for the NCO (e.g., "Sergeant" for Vlandia, "Huskarl" for Sturgia).
        ///     Pulled from progression_config.json T5 rank for the lord's culture.
        /// </summary>
        private string _ncoRank;

        /// <summary>
        ///     Pool of 3 soldier first names for personalized event dialogue.
        ///     Generated on enlistment, used randomly when {SOLDIER_NAME} placeholder appears.
        /// </summary>
        private List<string> _soldierNames = new List<string>();

        // ========================================================================
        // QUARTERMASTER HERO SYSTEM
        // Persistent NPC quartermaster with personality archetype
        // ========================================================================

        /// <summary>
        ///     The quartermaster Hero assigned to the current enlisted lord.
        ///     Created on first quartermaster access, persists for duration of service.
        ///     Each lord has their own unique quartermaster.
        /// </summary>
        private Hero _quartermasterHero;

        /// <summary>
        ///     Personality archetype for the quartermaster.
        ///     Determines dialogue style and special interactions.
        ///     Values: "veteran", "merchant", "bookkeeper", "scoundrel", "believer", "eccentric"
        /// </summary>
        private string _quartermasterArchetype;

        /// <summary>
        ///     Relationship level with quartermaster (0-100).
        ///     Affects discounts, dialogue options, and special favors.
        ///     Separate from lord relationship.
        /// </summary>
        private int _quartermasterRelationship;

        /// <summary>
        ///     Whether this is the first meeting with the current quartermaster.
        ///     Used to trigger introduction dialogue on first visit.
        /// </summary>
        private bool _hasMetQuartermaster;

        /// <summary>
        ///     Player's communication style with the current quartermaster.
        ///     Set during first meeting introduction. Values: "direct", "military", "friendly", "flattering".
        ///     Used for future dialogue flavor and potential special interactions.
        /// </summary>
        private string _qmPlayerStyle;

        // ========================================================================
        // FOOD/RATIONS SYSTEM
        // Allows player to purchase better rations for morale bonuses
        // ========================================================================

        /// <summary>
        ///     Current food quality tier affecting morale.
        ///     Standard (0) = no bonus, Supplemental (1) = +2 morale, Officer (2) = +4 morale,
        ///     Commander (3) = +8 morale.
        /// </summary>
        private int _currentFoodQuality;

        /// <summary>
        ///     When the current food quality bonus expires.
        ///     Rations are typically purchased for 1-3 days at a time.
        /// </summary>
        private CampaignTime _foodQualityExpires = CampaignTime.Zero;

        // ========================================================================
        // RETINUE PROVISIONING SYSTEM
        // T7-T9 commanders must provision their retinue weekly
        // ========================================================================

        /// <summary>
        ///     Current provisioning tier for the retinue.
        ///     Affects retinue morale and combat effectiveness.
        /// </summary>
        private int _retinueProvisioningTier;

        /// <summary>
        ///     When the current retinue provisioning expires.
        ///     Provisioning is purchased for 7 days (1 week) at a time.
        /// </summary>
        private CampaignTime _retinueProvisioningExpires = CampaignTime.Zero;

        /// <summary>
        ///     Whether the player has been warned about low provisions (2 days remaining).
        ///     Resets when new provisions are purchased.
        /// </summary>
        private bool _retinueProvisioningWarningShown;

        /// <summary>
        ///     Tracks desertion cooldowns for minor factions. When a player deserts from a minor faction lord,
        ///     they cannot re-enlist with any lord in that faction for 90 days. Unlike Kingdoms, minor factions
        ///     have no crime rating system, so we use this cooldown-based blocking instead.
        ///     Key: Minor faction StringId, Value: Cooldown end time
        /// </summary>
        private Dictionary<string, CampaignTime> _minorFactionDesertionCooldowns = new Dictionary<string, CampaignTime>();

        /// <summary>
        ///     Tracks when the player aborted enlistment during bag check with each lord.
        ///     Lords remember and require 7 days before accepting the player again.
        ///     Key: Lord StringId, Value: Time when abort cooldown ends
        /// </summary>
        private Dictionary<string, CampaignTime> _bagCheckAbortCooldowns = new Dictionary<string, CampaignTime>();

        /// <summary>
        ///     Initializes the enlistment behavior and sets up singleton access.
        ///     The singleton pattern allows other behaviors (like dialog managers) to access
        ///     enlistment state without needing a direct reference.
        /// </summary>
        public EnlistmentBehavior()
        {
            Instance = this;
        }

        /// <summary>
        ///     Singleton instance for accessing enlistment state from other mods.
        ///     Available after campaign starts. Safe to check in any CampaignBehavior.
        /// </summary>
        public static EnlistmentBehavior Instance { get; private set; }

        /// <summary>
        ///     True if player is actively enlisted and not on leave.
        ///     Use this to check if enlistment mechanics should apply.
        /// </summary>
        public bool IsEnlisted => _enlistedLord != null && !_isOnLeave;

        /// <summary>
        ///     Whether the player is currently in a desertion grace period.
        ///     During this time, they can rejoin any lord in the same kingdom to avoid desertion penalties.
        /// </summary>
        public bool IsInDesertionGracePeriod =>
            _pendingDesertionKingdom != null &&
            CampaignTime.Now < _desertionGracePeriodEnd;

        /// <summary>
        ///     Kingdom that the player needs to rejoin during grace period to avoid desertion.
        ///     Returns null if not in grace period.
        /// </summary>
        public Kingdom PendingDesertionKingdom => _pendingDesertionKingdom;

        /// <summary>
        ///     The lord the player is currently serving under.
        /// </summary>
        public Hero EnlistedLord => _enlistedLord;

        /// <summary>
        ///     True if player is on temporary leave from service.
        ///     While on leave, IsEnlisted returns false but enlistment state is preserved.
        /// </summary>
        public bool IsOnLeave => _isOnLeave;

        /// <summary>
        ///     Campaign time when the current leave started.
        /// </summary>
        public CampaignTime LeaveStartDate => _leaveStartDate;

        /// <summary>
        ///     Full name with rank of the NCO (e.g., "Sergeant Aldric" or "Huskarl Bjorn").
        ///     Returns null if not enlisted or names not generated.
        /// </summary>
        public string NcoFullName => !string.IsNullOrEmpty(_ncoRank) && !string.IsNullOrEmpty(_ncoFirstName)
            ? $"{_ncoRank} {_ncoFirstName}"
            : null;

        /// <summary>
        ///     Just the NCO rank title (e.g., "Sergeant", "Huskarl").
        /// </summary>
        public string NcoRank => _ncoRank;

        /// <summary>
        ///     Just the NCO first name (e.g., "Aldric", "Bjorn").
        /// </summary>
        public string NcoFirstName => _ncoFirstName;

        /// <summary>
        ///     Returns a random soldier name from the pool for use in event text.
        ///     Returns "a soldier" if pool is empty.
        /// </summary>
        public string GetRandomSoldierName()
        {
            if (_soldierNames == null || _soldierNames.Count == 0)
            {
                return "a soldier";
            }
            return _soldierNames[MBRandom.RandomInt(_soldierNames.Count)];
        }

        /// <summary>
        ///     Generates NCO and soldier names based on the lord's culture.
        ///     Called on enlistment; names persist until discharge.
        /// </summary>
        private void GenerateNcoAndSoldierNames()
        {
            if (_enlistedLord?.Culture == null)
            {
                ModLogger.Warn("ENLISTMENT", "Cannot generate NCO names - lord or culture is null");
                return;
            }

            var culture = _enlistedLord.Culture;

            // Generate NCO rank from progression_config.json T5 rank for this culture
            _ncoRank = GetCultureT5Rank(culture.StringId);

            // Generate NCO first name using Bannerlord's NameGenerator
            try
            {
                bool isFemale = MBRandom.RandomFloat < 0.2f; // 20% chance female NCO
                var firstName = NameGenerator.Current.GenerateFirstNameForPlayer(culture, isFemale);
                _ncoFirstName = firstName?.ToString() ?? "the Sergeant";
            }
            catch (Exception ex)
            {
                ModLogger.Caught("ENLISTMENT", "NameGenerator failed for NCO, using fallback", ex);
                _ncoFirstName = "the Sergeant";
            }

            // Generate 3 soldier names for the comrade pool
            _soldierNames = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    bool isFemale = MBRandom.RandomFloat < 0.2f;
                    var firstName = NameGenerator.Current.GenerateFirstNameForPlayer(culture, isFemale);
                    _soldierNames.Add(firstName?.ToString() ?? $"Soldier {i + 1}");
                }
                catch
                {
                    _soldierNames.Add($"Soldier {i + 1}");
                }
            }

            ModLogger.Info("ENLISTMENT", $"Generated NCO: {NcoFullName}, Soldiers: {string.Join(", ", _soldierNames)}");
        }

        /// <summary>
        ///     Clears NCO and soldier names when enlistment ends.
        /// </summary>
        private void ClearNcoAndSoldierNames()
        {
            _ncoFirstName = null;
            _ncoRank = null;
            _soldierNames?.Clear();
        }

        /// <summary>
        ///     Gets the T5 rank title for a given culture from progression_config.json.
        ///     Used for NCO rank titles. Falls back to "Sergeant" if culture not found.
        /// </summary>
        private string GetCultureT5Rank(string cultureId)
        {
            // T5 is the NCO/Sergeant tier. Use ConfigurationManager's public method.
            var normalizedId = cultureId?.ToLowerInvariant() ?? "mercenary";
            var rankTitle = ConfigurationManager.GetCultureRankTitle(5, normalizedId);

            // Strip localization prefix if present (e.g., "{=Enlisted_Rank_Vlandia_T5}Sergeant")
            if (!string.IsNullOrEmpty(rankTitle) && rankTitle.Contains("}"))
            {
                var closeBraceIndex = rankTitle.IndexOf("}", StringComparison.Ordinal);
                if (closeBraceIndex >= 0)
                {
                    rankTitle = rankTitle.Substring(closeBraceIndex + 1);
                }
            }

            return !string.IsNullOrEmpty(rankTitle) ? rankTitle : "Sergeant";
        }

        private bool ShouldActivationBeOn()
        {
            return IsEnlisted || _isOnLeave || IsInDesertionGracePeriod;
        }

        private void SyncActivationState(string reason)
        {
            var shouldBeActive = ShouldActivationBeOn();
            if (shouldBeActive != EnlistedActivation.IsActive)
            {
                EnlistedActivation.SetActive(shouldBeActive, reason);
            }
        }

        /// <summary>
        ///     The lord the player is currently serving under.
        ///     Returns null if not enlisted.
        /// </summary>
        public Hero CurrentLord => _enlistedLord;

        /// <summary>
        ///     Current military rank tier (1-6). Higher tiers unlock better equipment and duties.
        /// </summary>
        public int EnlistmentTier => _enlistmentTier;

        /// <summary>
        ///     Total XP accumulated in current service. Used for promotion calculations.
        /// </summary>
        public int EnlistmentXP => _enlistmentXP;

        /// <summary>
        ///     Gets the current company needs state for the enlisted lord's party.
        ///     Initialized on first access. Returns null if not enlisted.
        /// </summary>
        public CompanyNeedsState CompanyNeeds
        {
            get
            {
                if (_companyNeeds == null && IsEnlisted)
                {
                    _companyNeeds = new CompanyNeedsState();
                }
                return _companyNeeds;
            }
        }

        /// <summary>
        ///     Days since last promotion (or enlistment for T1).
        /// </summary>
        public int DaysInRank
        {
            get
            {
                if (_lastPromotionDate == CampaignTime.Zero)
                {
                    // Fall back to enlistment date if no promotion recorded
                    return _enlistmentDate != CampaignTime.Zero
                        ? (int)(CampaignTime.Now - _enlistmentDate).ToDays
                        : 0;
                }
                return (int)(CampaignTime.Now - _lastPromotionDate).ToDays;
            }
        }

        /// <summary>
        ///     Number of battles survived since enlistment.
        /// </summary>
        public int BattlesSurvived => _battlesSurvived;


        /// <summary>
        ///     Returns the player's current specialization role based on trait levels.
        ///     Uses EnlistedStatusManager to determine role from trait hierarchy.
        ///     Returns "Soldier" if no specialization is unlocked or if not properly initialized.
        /// </summary>
        public string CurrentSpecialization => EnlistedStatusManager.Instance?.GetPrimaryRole() ?? "Soldier";

        public int PendingMusterPay => _pendingMusterPay;
        public CampaignTime NextPaydaySafe => _nextPayday != CampaignTime.Zero ? _nextPayday : (_nextPayday = ComputeNextPayday());
        public string LastPayOutcome => _lastPayOutcome ?? "none";
        public bool IsPayMusterPending => _payMusterPending;

        // Pay tension properties
        /// <summary>Pay tension level (0-100). Escalates when pay is delayed, triggers events at thresholds.</summary>
        public int PayTension => _payTension;
        /// <summary>Accumulated unpaid wages when lord can't afford to pay.</summary>
        public int OwedBackpay => _owedBackpay;
        /// <summary>Days since last successful pay. Used for pay delay detection.</summary>
        public int DaysSincePay => _lastPayDate == CampaignTime.Zero ? 0 : (int)(CampaignTime.Now - _lastPayDate).ToDays;
        /// <summary>True if pay is overdue (more than 7 days since last pay).</summary>
        public bool IsPayOverdue => DaysSincePay > 7 && IsEnlisted;

        public int PensionAmountPerDay => _pensionAmountPerDay;
        public bool IsPensionPaused => _isPensionPaused;
        public bool IsPendingDischarge => _isPendingDischarge;
        public string LastDischargeBand => _lastDischargeBand ?? "none";
        public bool IsOnProbation => _isOnProbation;

        // Muster tracking properties for the multi-stage muster menu system
        /// <summary>True when a pay muster is pending (player should proceed to muster menu).</summary>
        public bool PayMusterPending => _payMusterPending;

        /// <summary>Campaign time when the last muster completed. Used by BaggageTrainManager for 6-hour access window.</summary>
        public CampaignTime LastMusterCompletionTime => _lastMusterCompletionTime;

        /// <summary>Campaign day when the last promotion occurred. Used for muster recap display.</summary>
        public int DayOfLastPromotion => _dayOfLastPromotion;

        /// <summary>Tier at the end of the previous muster. Used to detect promotions during the muster period.</summary>
        public int TierAtLastMuster => _tierAtLastMuster;

        /// <summary>XP at the end of the previous muster. Used to calculate XP gained this period.</summary>
        public int XPAtLastMuster => _xpAtLastMuster;

        /// <summary>Campaign day of the last muster. Used to query events since last muster.</summary>
        public int LastMusterDay => _lastMusterDay;

        /// <summary>Campaign day of the last equipment inspection. Used for 12-day cooldown tracking.</summary>
        public int LastInspectionDay => _lastInspectionDay;

        /// <summary>Campaign day of the last recruit event. Used for 10-day cooldown tracking.</summary>
        public int LastRecruitDay => _lastRecruitDay;

        // ========================================================================
        // QUARTERMASTER HERO PROPERTIES
        // ========================================================================

        /// <summary>
        ///     The current quartermaster Hero for dialog interaction.
        ///     Returns null if not enlisted or hero hasn't been created.
        /// </summary>
        public Hero QuartermasterHero => _quartermasterHero;

        /// <summary>
        ///     Personality archetype of the quartermaster.
        ///     Values: "veteran", "merchant", "bookkeeper", "scoundrel", "believer", "eccentric"
        /// </summary>
        public string QuartermasterArchetype => _quartermasterArchetype ?? "veteran";

        /// <summary>
        ///     Relationship level with quartermaster (0-100).
        /// </summary>
        public int QuartermasterRelationship => _quartermasterRelationship;

        /// <summary>
        ///     Whether this is the first meeting with the quartermaster.
        /// </summary>
        public bool HasMetQuartermaster => _hasMetQuartermaster;

        /// <summary>
        ///     Player's communication style with the quartermaster (set during first meeting).
        ///     Values: "direct", "military", "friendly", "flattering"
        /// </summary>
        public string QMPlayerStyle => _qmPlayerStyle ?? "military";

        /// <summary>
        ///     Returns true if a bag check is scheduled or in progress.
        ///     Used to defer other events until bag check completes.
        /// </summary>
        public bool IsBagCheckPending => (_bagCheckScheduled || _bagCheckInProgress) && !_bagCheckCompleted;

        private bool ShouldRunBagCheck()
        {
            // Only on first enlist or after retirement/desertion reset, and only below Tier 7
            return !_bagCheckCompleted && !_bagCheckInProgress && !_bagCheckEverCompleted && _enlistmentTier < 7;
        }

        /// <summary>
        ///     Open baggage train stash.
        /// </summary>
        public bool TryOpenBaggageTrain()
        {
            try
            {
                ModLogger.Info("Baggage", $"TryOpenBaggageTrain called. Tier={_enlistmentTier}");

                // Check if bag check is still pending - block access until onboarding is complete
                if (IsBagCheckPending)
                {
                    ModLogger.Info("Baggage", "Bag check pending - blocking baggage access until storage fee choice is made");
                    var msg = new TextObject("{=baggage_blocked_onboarding}The quartermaster needs you to sort out your storage arrangements first.");
                    InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Yellow));
                    return false;
                }

                ModLogger.Info("Baggage", $"Opening baggage stash with {_baggageStash?.Count ?? 0} items");
                InventoryScreenHelper.OpenScreenAsStash(_baggageStash);
                return true;
            }
            catch (Exception ex)
            {
                // End-user support: stable code + full exception detail (stack trace written once per unique exception).
                ModLogger.Surfaced("ENLISTMENT", "Error opening baggage train stash", ex);
                return false;
            }
        }

        private void ActivateProbation()
        {
            var config = EnlistedConfig.LoadRetirementConfig();
            var days = Math.Max(1, config?.ProbationDays ?? 12);
            _isOnProbation = true;
            _probationEnds = CampaignTime.Now + CampaignTime.Days(days);

            ModLogger.Info("Probation", $"Probation activated for {days} days");
        }

        private void ClearProbation(string reason)
        {
            if (!_isOnProbation)
            {
                return;
            }

            _isOnProbation = false;
            _probationEnds = CampaignTime.Zero;

            ModLogger.Info("Probation", $"Probation cleared ({reason})");
        }

        private void TryApplyReservistReentryBoost(IFaction faction)
        {
            try
            {
                var svc = ServiceRecordManager.Instance;
                if (svc == null || faction == null)
                {
                    return;
                }

                if (svc.TryConsumeReservistForFaction(faction, out var targetTier, out var bonusXp,
                        out var relationBonus, out var band, out var probation))
                {
                    if (probation)
                    {
                        ActivateProbation();
                    }

                    if (targetTier > 0)
                    {
                        _enlistmentTier = Math.Max(_enlistmentTier, targetTier);
                    }

                    if (bonusXp > 0)
                    {
                        AddEnlistmentXP(bonusXp, "Reservist Re-entry");
                    }

                    if (relationBonus > 0)
                    {
                        if (_enlistedLord != null)
                        {
                            ChangeRelationAction.ApplyPlayerRelation(_enlistedLord, relationBonus);
                        }

                        var factionLeader = faction.Leader;
                        if (factionLeader != null && factionLeader != _enlistedLord)
                        {
                            ChangeRelationAction.ApplyPlayerRelation(factionLeader, relationBonus);
                        }
                    }


                    if (targetTier >= 7)
                    {
                        var fine = Math.Max(0, EnlistedConfig.LoadRetirementConfig().CommanderReentryFineGold);
                        if (fine > 0)
                        {
                            var hero = Hero.MainHero;
                            if (hero != null)
                            {
                                var partyGold = hero.PartyBelongedTo?.PartyTradeGold ?? 0;
                                var paid = Math.Min(partyGold, fine);
                                if (paid > 0)
                                {
                                    GiveGoldAction.ApplyBetweenCharacters(hero, null, paid);
                                }
                                ModLogger.Info("ENLISTMENT",
                                    $"Commander re-entry fine paid: {paid} (configured {fine})");
                            }
                        }
                    }

                    _lastDischargeBand = band;
                }
            }
            catch (Exception ex)
            {
                // This should never fail, but if it does we want the full stack trace for support.
                ModLogger.Surfaced("ENLISTMENT", "Reservist re-entry boost failed", ex);
            }
        }


        public bool HasActiveGraceProtection => CampaignTime.Now < _graceProtectionEnds;

        /// <summary>
        ///     Checks if a party is currently involved in a battle, siege, or besieging a settlement.
        ///     Used to prevent finishing player encounters during critical battle/siege operations,
        ///     which can cause assertion failures in the siege system that expects encounters to
        ///     remain active during these operations.
        /// </summary>
        /// <param name="party">The party to check for battle/siege state.</param>
        /// <returns>True if the party is in battle, siege, or besieging a settlement; false otherwise.</returns>
        private static bool InBattleOrSiege(MobileParty party)
        {
            return party?.Party.MapEvent != null || party?.Party.SiegeEvent != null ||
                   party?.BesiegedSettlement != null;
        }

        private void EnsurePlayerSharesArmy(MobileParty lordParty)
        {
            var main = MobileParty.MainParty;
            if (main == null || lordParty == null)
            {
                ModLogger.Debug("BATTLE",
                    $"EnsurePlayerSharesArmy: Skipped - main={main != null}, lordParty={lordParty != null}");
                return;
            }

            // Log current state for diagnostics
            var lordMapEvent = lordParty.Party?.MapEvent;
            var playerMapEvent = main.Party?.MapEvent;
            float distanceToLord = main.GetPosition2D.Distance(lordParty.GetPosition2D);

            ModLogger.Info("BATTLE", "=== BATTLE PARTICIPATION CHECK ===");
            ModLogger.Info("BATTLE",
                $"Lord: {lordParty.LeaderHero?.Name?.ToString() ?? "unknown"}, HasArmy: {lordParty.Army != null}, InMapEvent: {lordMapEvent != null}");
            ModLogger.Info("BATTLE",
                $"Player: InMapEvent: {playerMapEvent != null}, IsActive: {main.IsActive}, IsVisible: {main.IsVisible}");
            ModLogger.Info("BATTLE",
                $"Distance to lord: {distanceToLord:F2}, PlayerArmy: {main.Army?.LeaderParty?.LeaderHero?.Name?.ToString() ?? "none"}");
            if (lordMapEvent != null)
            {
                ModLogger.Info("BATTLE",
                    $"Lord MapEvent Type: {lordMapEvent.EventType}, Attacker: {lordMapEvent.AttackerSide?.LeaderParty?.LeaderHero?.Name?.ToString() ?? "unknown"}, Defender: {lordMapEvent.DefenderSide?.LeaderParty?.LeaderHero?.Name?.ToString() ?? "unknown"}");
            }

            // Army joining is now handled in OnRealtimeTick when lord merges with army
            // If already in army, the native encounter system handles battle participation
            if (lordParty.Army != null && main.Army == lordParty.Army)
            {
                ModLogger.Debug("BATTLE", "Already in lord's army - native encounter system handles participation");
                return;
            }

            // Lord has no army but in battle - player participates through native encounter system
            // The native game will show encounter menu when player is active and near battle
            if (lordMapEvent != null && playerMapEvent == null)
            {
                ModLogger.Info("BATTLE",
                    "LORD IN INDIVIDUAL BATTLE (no army) - Attempting native encounter collection");
                ModLogger.Info("BATTLE",
                    $"Pre-activation state: IsActive={main.IsActive}, ShouldJoinPlayerBattles={main.ShouldJoinPlayerBattles}");

                // Ensure player is active, visible enough to be collected, and near the lord
                main.IsActive = true;
                main.IgnoreByOtherPartiesTill(CampaignTime.Now); // Clear ignore window so we can be collected
                main.ShouldJoinPlayerBattles = true;

                ModLogger.Info("BATTLE",
                    $"Post-activation state: IsActive={main.IsActive}, ShouldJoinPlayerBattles={main.ShouldJoinPlayerBattles}");
                ModLogger.Info("BATTLE",
                    "Player party ACTIVATED for individual lord battle - waiting for native encounter menu");
            }
            else if (lordMapEvent == null)
            {
                ModLogger.Debug("BATTLE", "Lord not in MapEvent - no battle action needed");
            }
            else
            {
                // Both lordMapEvent and playerMapEvent are non-null here
                ModLogger.Debug("BATTLE", "Player already in MapEvent - no action needed");
            }
        }

        private void PreparePartyForNativeBattle(MobileParty main)
        {
            if (main == null)
            {
                return;
            }

            if (!main.IsActive)
            {
                main.IsActive = true;
            }

            main.IsVisible = false;
            main.IgnoreByOtherPartiesTill(CampaignTime.Now);
            main.ShouldJoinPlayerBattles = true;

            // CRITICAL: Follow the LORD's party with the camera, not the player's invisible party
            // This prevents the game from pausing when the lord enters battle while waiting for
            // the player's party (camera target) to arrive at the battle location
            var lordParty = _enlistedLord?.PartyBelongedTo;
            if (lordParty != null)
            {
                lordParty.Party.SetAsCameraFollowParty();
            }

            TrySetShouldJoinPlayerBattles(main, true);
        }

        /// <summary>
        ///     Immediately joins the lord's battle when MapEvent starts so the fight cannot auto-resolve
        ///     before the player is collected. Used as a fast-path guard for tiny skirmishes that end in
        ///     the same frame they begin.
        /// </summary>
        private void ForceImmediateBattleJoin(MapEvent mapEvent, MobileParty main, MobileParty lordParty)
        {
            try
            {
                if (mapEvent == null || main == null || lordParty == null)
                {
                    return;
                }

                // Respect reserve choice; don't force-join if the player opted to sit out
                if (EnlistedEncounterBehavior.IsWaitingInReserve)
                {
                    ModLogger.Debug("BATTLE", "Skipping immediate join - player is waiting in reserve");
                    return;
                }

                // Already joined
                if (main.Party?.MapEvent == mapEvent)
                {
                    return;
                }

                var attackerSide = mapEvent.AttackerSide;
                var defenderSide = mapEvent.DefenderSide;
                bool lordIsAttacker = attackerSide?.Parties?.Any(p => p?.Party == lordParty.Party) == true;

                // ARMY FIX #1: When lord is in an army, the lord's party might not be in the MapEvent
                // parties list yet - only the army leader is listed initially. Check if the army
                // leader is on the attacker side to determine the correct side for the player.
                if (!lordIsAttacker && lordParty.Army != null)
                {
                    var armyLeaderParty = lordParty.Army.LeaderParty?.Party;
                    if (armyLeaderParty != null)
                    {
                        lordIsAttacker = attackerSide?.Parties?.Any(p => p?.Party == armyLeaderParty) == true;
                        if (lordIsAttacker)
                        {
                            ModLogger.Debug("BATTLE",
                                $"Lord's army leader ({lordParty.Army.LeaderParty?.LeaderHero?.Name}) is attacker - joining attacker side");
                        }
                    }
                }

                // ARMY FIX #2: Player might be auto-collected into an army BEFORE the lord joins.
                // If the player is in an army but the lord isn't yet, use the PLAYER'S army leader
                // to determine the correct side. This handles the race condition where native game
                // collects the player party into an army while the lord is still approaching.
                if (!lordIsAttacker && lordParty.Army == null && main.Army != null)
                {
                    var playerArmyLeaderParty = main.Army.LeaderParty?.Party;
                    if (playerArmyLeaderParty != null)
                    {
                        lordIsAttacker = attackerSide?.Parties?.Any(p => p?.Party == playerArmyLeaderParty) == true;
                        if (lordIsAttacker)
                        {
                            ModLogger.Info("BATTLE",
                                $"Player in army before lord - using player's army leader ({main.Army.LeaderParty?.LeaderHero?.Name}) to determine attacker side");
                        }
                    }
                }

                var targetSide = lordIsAttacker ? attackerSide : defenderSide;

                if (targetSide == null)
                {
                    ModLogger.Warn("BATTLE", "MapEventStarted: could not resolve lord side for immediate join");
                    return;
                }

                // Make sure the player is eligible for collection and hidden to avoid helper menus
                main.IsActive = true;
                main.IsVisible = false;
                main.IgnoreByOtherPartiesTill(CampaignTime.Now);
                TrySetShouldJoinPlayerBattles(main, true);

                // NAVAL BATTLE FIX: Sync player's sea state with lord before joining naval battles
                // The game requires IsCurrentlyAtSea to match the MapEvent type for encounters to work.
                // Without this sync, PlayerEncounter.Init() crashes because the player party state
                // doesn't match the naval battle expectations (ship handling, position validation, etc.)
                if (mapEvent.IsNavalMapEvent && main.IsCurrentlyAtSea != lordParty.IsCurrentlyAtSea)
                {
                    main.IsCurrentlyAtSea = lordParty.IsCurrentlyAtSea;
                    main.Position = lordParty.Position;
                    ModLogger.Debug("BATTLE",
                        $"Synced player sea state to {lordParty.IsCurrentlyAtSea} for naval battle join");
                }

                var playerSideLabel = lordIsAttacker ? "Attacker" : "Defender";

                // Join the MapEvent on the lord's side right away to block auto-sim resolution
                if (main.Party != null)
                {
                    main.Party.MapEventSide = targetSide;
                    ModLogger.Info("BATTLE",
                        $"Immediate battle join on {playerSideLabel} side (MapEventStarted guard, naval={mapEvent.IsNavalMapEvent})");

                    // Log battle integration status for diagnostics
                    LogBattleIntegrationStatus(main, lordParty, mapEvent, "OnMapEventStarted-ImmediateJoin");
                }

                if (PlayerEncounter.Current == null && !_playerEncounterCreatedForBattle)
                {
                    _playerEncounterCreatedForBattle = true;
                    PlayerEncounter.Start();
                    PlayerEncounter.Init();
                    ModLogger.Info("BATTLE",
                        "PlayerEncounter initialized at battle start to prevent instant auto-resolve");

                    // Log encounter status after initialization
                    LogEncounterStatus("OnMapEventStarted-EncounterInit");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("BATTLE",
                    $"Failed immediate battle join on MapEventStarted: {ex.Message}", ex);
            }
        }

        /// <summary>
        ///     Registers event listeners that respond to game events throughout the campaign.
        ///     These events fire at various intervals and trigger specific behaviors like
        ///     party state updates, battle detection, lord status changes, and XP processing.
        /// </summary>
        public override void RegisterEvents()
        {
            // Real-time tick runs every frame, used for continuous party state management.
            // Includes time validation to prevent executing updates during zero-delta-time frames,
            // which can cause assertion failures in the rendering system.
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnRealtimeTick);

            // Hourly tick runs once per in-game hour, used for periodic checks like
            // battle detection, menu state validation, and promotion eligibility.
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);

            // Daily tick runs once per in-game day, used for wage calculation,
            // XP accumulation, and daily duty processing.
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);

            // Battle detection and participation are handled in the real-time tick
            // by checking MapEvent status, rather than relying on battle start/end events,
            // to ensure immediate response and compatibility across game versions.

            // Event-driven handlers for lord status changes that affect enlistment:
            // These events fire when the enlisted lord is killed, defeated, taken prisoner,
            // or when their army is dispersed, allowing us to handle service termination.
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
            CampaignEvents.CharacterDefeated.AddNonSerializedListener(this, OnCharacterDefeated);
            CampaignEvents.ArmyDispersed.AddNonSerializedListener(this, OnArmyDispersed);
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);

            // Battle start/end events provide notification when battles begin and complete.
            // Used for logging and state tracking, though battle participation is primarily
            // managed through real-time MapEvent detection for immediate response.
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);

            // Player battle end event fires when the player's battle completes.
            // Used for tracking battle completion and handling post-battle state transitions.
            CampaignEvents.OnPlayerBattleEndEvent.AddNonSerializedListener(this, OnPlayerBattleEnd);

            // Settlement entry/exit events fire when parties enter or leave settlements.
            // We monitor both to keep the enlisted party hidden except during menus.
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
            CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, OnSettlementLeft);

            // Edge case handlers for grace period:
            // Clear grace period if player changes kingdoms or if kingdom is destroyed
            CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);
            CampaignEvents.KingdomDestroyedEvent.AddNonSerializedListener(this, OnKingdomDestroyed);

            // Mission started event - used to add our formation assignment behavior
            // This ensures enlisted players are sorted into their designated formation (Infantry/Archer/etc)
            CampaignEvents.AfterMissionStarted.AddNonSerializedListener(this, OnAfterMissionStarted);
        }

        /// <summary>
        ///     Serializes and deserializes enlistment state for save/load operations.
        ///     Called automatically by Bannerlord's save system when saving or loading games.
        ///     After loading, validates state and restores proper party activity state.
        /// </summary>
        /// <param name="dataStore">The save/load data store that handles serialization.</param>
        public override void SyncData(IDataStore dataStore)
        {
            SaveLoadDiagnostics.SafeSyncData(this, dataStore, () =>
            {
                // Serialize all enlistment state variables
                // These values are saved to the game's save file and restored when loading
                SyncKey(dataStore, "_enlistedLord", ref _enlistedLord);
                SyncKey(dataStore, "_enlistmentTier", ref _enlistmentTier);
                SyncKey(dataStore, "_enlistmentXP", ref _enlistmentXP);
                SyncKey(dataStore, "_lastPromotionBlockedMessageTime", ref _lastPromotionBlockedMessageTime);
                SyncKey(dataStore, "_baggageStash", ref _baggageStash);
                SyncKey(dataStore, "_baggageStashFactionId", ref _baggageStashFactionId);
                SyncKey(dataStore, "_bagCheckCompleted", ref _bagCheckCompleted);
                SyncKey(dataStore, "_bagCheckEverCompleted", ref _bagCheckEverCompleted);
                SyncKey(dataStore, "_bagCheckScheduled", ref _bagCheckScheduled);
                SyncKey(dataStore, "_bagCheckDueTime", ref _bagCheckDueTime);
                SyncKey(dataStore, "_bagCheckInProgress", ref _bagCheckInProgress);
                SyncKey(dataStore, "_pendingBagCheckLord", ref _pendingBagCheckLord);

                // Cross-faction baggage courier tracking
                SyncKey(dataStore, "_baggageCourierArrivalTime", ref _baggageCourierArrivalTime);
                SyncKey(dataStore, "_pendingCourierBaggage", ref _pendingCourierBaggage);
                SyncKey(dataStore, "_disbandArmyAfterBattle", ref _disbandArmyAfterBattle);
                SyncKey(dataStore, "_enlistmentDate", ref _enlistmentDate);

                // Promotion requirements tracking
                SyncKey(dataStore, "_lastPromotionDate", ref _lastPromotionDate);
                SyncKey(dataStore, "_battlesSurvived", ref _battlesSurvived);

                SyncKey(dataStore, "_isOnLeave", ref _isOnLeave);
                SyncKey(dataStore, "_leaveStartDate", ref _leaveStartDate);
                SyncKey(dataStore, "_leaveCooldownEnds", ref _leaveCooldownEnds);
                SyncKey(dataStore, "_desertionGracePeriodEnd", ref _desertionGracePeriodEnd);
                SyncKey(dataStore, "_pendingDesertionKingdom", ref _pendingDesertionKingdom);
                SyncKey(dataStore, "_savedGraceTier", ref _savedGraceTier);
                SyncKey(dataStore, "_savedGraceLord", ref _savedGraceLord);
                SyncKey(dataStore, "_savedGraceXP", ref _savedGraceXP);
                SyncKey(dataStore, "_savedGraceTroopId", ref _savedGraceTroopId);
                SyncKey(dataStore, "_pendingMusterPay", ref _pendingMusterPay);
                SyncKey(dataStore, "_nextPayday", ref _nextPayday);
                SyncKey(dataStore, "_payMusterPending", ref _payMusterPending);
                SyncKey(dataStore, "_lastPayOutcome", ref _lastPayOutcome);

                // Pay tension state
                SyncKey(dataStore, "_payTension", ref _payTension);
                SyncKey(dataStore, "_owedBackpay", ref _owedBackpay);
                SyncKey(dataStore, "_lastPayDate", ref _lastPayDate);
                SyncKey(dataStore, "_consecutiveDelays", ref _consecutiveDelays);

                // Muster tracking state for multi-stage muster menu system
                SyncKey(dataStore, "_tierAtLastMuster", ref _tierAtLastMuster);
                SyncKey(dataStore, "_xpAtLastMuster", ref _xpAtLastMuster);
                SyncKey(dataStore, "_lastMusterDay", ref _lastMusterDay);
                SyncKey(dataStore, "_lastMusterCompletionTime", ref _lastMusterCompletionTime);
                SyncKey(dataStore, "_dayOfLastPromotion", ref _dayOfLastPromotion);
                SyncKey(dataStore, "_lastInspectionDay", ref _lastInspectionDay);
                SyncKey(dataStore, "_lastRecruitDay", ref _lastRecruitDay);
                SerializeXPSourcesDictionary(dataStore);

                // Ration tracking - manual serialization for List<IssuedRationRecord>
                SerializeIssuedRations(dataStore);

                SyncKey(dataStore, "_pensionAmountPerDay", ref _pensionAmountPerDay);
                SyncKey(dataStore, "_pensionFactionId", ref _pensionFactionId);
                SyncKey(dataStore, "_isPensionPaused", ref _isPensionPaused);
                SyncKey(dataStore, "_isPendingDischarge", ref _isPendingDischarge);
                SyncKey(dataStore, "_lastDischargeBand", ref _lastDischargeBand);
                SyncKey(dataStore, "_isOnProbation", ref _isOnProbation);
                SyncKey(dataStore, "_probationEnds", ref _probationEnds);
                SyncKey(dataStore, "_savedGraceEnlistmentDate", ref _savedGraceEnlistmentDate);
                SyncKey(dataStore, "_graceProtectionEnds", ref _graceProtectionEnds);

                // NCO and soldier names for personalized event text
                SyncKey(dataStore, "_ncoFirstName", ref _ncoFirstName);
                SyncKey(dataStore, "_ncoRank", ref _ncoRank);
                SyncSoldierNames(dataStore);

                // Quartermaster hero system
                SyncKey(dataStore, "_quartermasterHero", ref _quartermasterHero);
                SyncKey(dataStore, "_quartermasterArchetype", ref _quartermasterArchetype);
                SyncKey(dataStore, "_quartermasterRelationship", ref _quartermasterRelationship);
                SyncKey(dataStore, "_hasMetQuartermaster", ref _hasMetQuartermaster);
                SyncKey(dataStore, "_qmPlayerStyle", ref _qmPlayerStyle);

                // Food/rations system
                SyncKey(dataStore, "_currentFoodQuality", ref _currentFoodQuality);
                SyncKey(dataStore, "_foodQualityExpires", ref _foodQualityExpires);

                // Retinue provisioning system
                SyncKey(dataStore, "_retinueProvisioningTier", ref _retinueProvisioningTier);
                SyncKey(dataStore, "_retinueProvisioningExpires", ref _retinueProvisioningExpires);
                SyncKey(dataStore, "_retinueProvisioningWarningShown", ref _retinueProvisioningWarningShown);

                // Serialize kingdom state so we can restore the player's original kingdom/clan status
                SyncKey(dataStore, "_originalKingdom", ref _originalKingdom);
                SyncKey(dataStore, "_wasIndependentClan", ref _wasIndependentClan);

                // Serialize minor faction war relations - these are created when enlisting with non-Kingdom lords
                // and need to be restored to neutral when service ends
                SyncKey(dataStore, "_minorFactionWarRelations", ref _minorFactionWarRelations);

                // Serialize minor faction desertion cooldowns - manual serialization for Dictionary<string, CampaignTime>
                SerializeMinorFactionDesertionCooldowns(dataStore);

                // Serialize bag check abort cooldowns - tracks when player aborted enlistment during bag check
                SerializeBagCheckAbortCooldowns(dataStore);

                // Company needs state - manual serialization since it's a custom class
                SerializeCompanyNeeds(dataStore);

                // Veteran retirement system state - manual serialization for dictionary
                // Bannerlord's save system can't serialize custom class dictionaries directly
                SyncKey(dataStore, "_retirementNotificationShown", ref _retirementNotificationShown);
                SyncKey(dataStore, "_currentTermKills", ref _currentTermKills);

                // NOTE: Veteran records are now stored in ServiceRecordManager.FactionServiceRecord.
                // ServiceRecordManager.SyncData() handles all faction record serialization.

                // CRITICAL: Ensure proper party activity state for both new games and loaded games
                // This is important because the save system doesn't preserve IsActive state,
                // and we need to ensure enlisted players start inactive to prevent random encounters
                // For new campaigns, non-enlisted players must be active to allow normal gameplay

                // Validate tier and XP values after loading
                if (dataStore.IsLoading)
                {
                    ModLogger.Info("SaveLoad",
                        $"Loading enlistment state - Lord: {_enlistedLord?.Name?.ToString() ?? "null"}, Tier: {_enlistmentTier}, XP: {_enlistmentXP}, OnLeave: {_isOnLeave}, GracePeriod: {IsInDesertionGracePeriod}");
                    ValidateLoadedState();

                    // Clean up any duplicate hero entries in rosters (can occur after escape from captivity)
                    DeduplicateRosterHeroes();

                    ModLogger.Info("SaveLoad", "Enlistment state validated and restored");

                    // Sync activation based on loaded state
                    SyncActivationState("sync_load");
                }
                else
                {
                    ModLogger.Debug("SaveLoad",
                        $"Saving enlistment state - Lord: {_enlistedLord?.Name?.ToString() ?? "null"}, Tier: {_enlistmentTier}, XP: {_enlistmentXP}");
                }

                // IMPORTANT: Do NOT set IsActive here during SyncData!
                // The game asserts that IsActive must be false during save load process.
                // Party state will be restored on the first campaign tick via _needsPostLoadStateRestore flag.
                if (dataStore.IsLoading)
                {
                    _needsPostLoadStateRestore = true;
                    _isPartyStateInitialized = false; // Block all IsActive modifications until post-load
                    _initializationTicksRemaining = 10; // Reset countdown to ensure we wait for load completion
                    ModLogger.Debug("SaveLoad", "Deferred party state restoration to first campaign tick");

                    // CRITICAL: Clear BesiegerCamp immediately during load if enlisted and not in active siege.
                    // Enlisted soldiers don't command sieges - only the lord does.
                    // If MainParty.BesiegerCamp is set, GetGenericStateMenu() returns menu_siege_strategies
                    // on the first campaign tick BEFORE RestorePartyStateAfterLoad runs.
                    if (IsEnlisted && !_isOnLeave)
                    {
                        var main = MobileParty.MainParty;
                        if (main?.BesiegerCamp != null && !IsPartyInActiveSiege(main))
                        {
                            ModLogger.Info("SaveLoad", "Clearing BesiegerCamp during save load (no active siege detected)");
                            main.BesiegerCamp = null;
                        }
                    }
                }
            });
        }

        /// <summary>
        ///     Manually serialize/deserialize company needs state.
        ///     Persists two need values (0-100 scale) plus non-food supply state to save file.
        ///     Note: Morale and Rest removed - old saves with these values will load but discard them.
        /// </summary>
        private void SerializeCompanyNeeds(IDataStore dataStore)
        {
            try
            {
                if (!dataStore.IsLoading)
                {
                    // Saving: serialize each need value (morale and rest removed)
                    if (_companyNeeds != null)
                    {
                        var readiness = _companyNeeds.Readiness;
                        var supplies = _companyNeeds.Supplies;

                        SyncKey(dataStore, "_companyNeeds_readiness", ref readiness);
                        SyncKey(dataStore, "_companyNeeds_supplies", ref supplies);
                    }
                    else
                    {
                        // No state to save - write defaults
                        var defaultValue = 60;
                        SyncKey(dataStore, "_companyNeeds_readiness", ref defaultValue);
                        SyncKey(dataStore, "_companyNeeds_supplies", ref defaultValue);
                    }

                    // Serialize supply component from CompanySupplyManager
                    var nonFoodSupply = CompanySupplyManager.Instance?.NonFoodSupply ?? 100f;
                    SyncKey(dataStore, "_companySupply_nonFood", ref nonFoodSupply);
                }
                else
                {
                    // Loading: reconstruct CompanyNeedsState from saved values
                    var readiness = 60;
                    var morale = 60; // Backwards compatibility: load but discard
                    var rest = 60;   // Backwards compatibility: load but discard
                    var supplies = 60;

                    SyncKey(dataStore, "_companyNeeds_readiness", ref readiness);
                    SyncKey(dataStore, "_companyNeeds_morale", ref morale); // Load old value but don't use it
                    SyncKey(dataStore, "_companyNeeds_rest", ref rest);     // Load old value but don't use it
                    SyncKey(dataStore, "_companyNeeds_supplies", ref supplies);

                    // Load supply component
                    var nonFoodSupply = 100f;
                    SyncKey(dataStore, "_companySupply_nonFood", ref nonFoodSupply);

                    _companyNeeds = new CompanyNeedsState
                    {
                        Readiness = readiness,
                        Supplies = supplies
                        // Rest removed 2026-01-11 - old save values loaded but discarded
                    };

                    // Restore CompanySupplyManager if player was enlisted
                    if (_enlistedLord != null)
                    {
                        CompanySupplyManager.RestoreFromSave(_enlistedLord, nonFoodSupply);
                    }
                    else
                    {
                        ModLogger.Warn("SaveLoad",
                            $"CompanySupplyManager NOT restored (no enlisted lord). Loaded supply value: {nonFoodSupply:F1}%");
                    }

                    // Log at INFO level when supply is critically low to help diagnose issues
                    if (nonFoodSupply < 15)
                    {
                        ModLogger.Info("SaveLoad",
                            $"SUPPLY WARNING: Loaded critically low supply ({nonFoodSupply:F1}%) - R:{readiness}");
                    }
                    else
                    {
                        ModLogger.Debug("SaveLoad",
                            $"Loaded company needs - R:{readiness} S:{supplies}, NonFood:{nonFoodSupply:F1}%");
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("SaveLoad", "Failed to serialize company needs", ex);
                _companyNeeds = new CompanyNeedsState();
            }
        }

        // NOTE: Veteran records are now stored in ServiceRecordManager.FactionServiceRecord.
        // The old _veteranRecords dictionary and SerializeVeteranRecords method have been removed.
        // ServiceRecordManager.SyncData() handles all faction record serialization.

        /// <summary>
        ///     Manually serialize/deserialize minor faction desertion cooldowns.
        ///     Minor factions don't have a crime rating system, so we track when players leave their service.
        ///     Uses a simple count + individual field approach for maximum compatibility.
        /// </summary>
        private void SerializeMinorFactionDesertionCooldowns(IDataStore dataStore)
        {
            try
            {
                var cooldownCount = _minorFactionDesertionCooldowns?.Count ?? 0;
                SyncKey(dataStore, "_minorFacDesertion_count", ref cooldownCount);

                if (!dataStore.IsLoading)
                {
                    // Saving: serialize each cooldown entry individually
                    var index = 0;
                    if (_minorFactionDesertionCooldowns != null)
                    {
                        foreach (var kvp in _minorFactionDesertionCooldowns)
                        {
                            var factionId = kvp.Key;
                            var cooldownEnd = kvp.Value;

                            SyncKey(dataStore, $"_minorFacDesertion_{index}_id", ref factionId);
                            SyncKey(dataStore, $"_minorFacDesertion_{index}_end", ref cooldownEnd);
                            index++;
                        }
                    }
                }
                else
                {
                    // Loading: reconstruct dictionary from individual fields
                    _minorFactionDesertionCooldowns = new Dictionary<string, CampaignTime>();

                    for (var i = 0; i < cooldownCount; i++)
                    {
                        var factionId = "";
                        var cooldownEnd = CampaignTime.Zero;

                        SyncKey(dataStore, $"_minorFacDesertion_{i}_id", ref factionId);
                        SyncKey(dataStore, $"_minorFacDesertion_{i}_end", ref cooldownEnd);

                        if (!string.IsNullOrEmpty(factionId))
                        {
                            _minorFactionDesertionCooldowns[factionId] = cooldownEnd;
                        }
                    }

                    ModLogger.Debug("SaveLoad", $"Loaded {_minorFactionDesertionCooldowns.Count} minor faction desertion cooldowns");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("SaveLoad", "Error serializing minor faction desertion cooldowns", ex);
                // Ensure dictionary exists even on error
                if (_minorFactionDesertionCooldowns == null)
                {
                    _minorFactionDesertionCooldowns = new Dictionary<string, CampaignTime>();
                }
            }
        }

        /// <summary>
        ///     Manually serialize/deserialize bag check abort cooldowns.
        ///     Tracks when the player aborted enlistment during bag check with each lord.
        /// </summary>
        private void SerializeBagCheckAbortCooldowns(IDataStore dataStore)
        {
            try
            {
                var cooldownCount = _bagCheckAbortCooldowns?.Count ?? 0;
                SyncKey(dataStore, "_bagCheckAbort_count", ref cooldownCount);

                if (!dataStore.IsLoading)
                {
                    // Saving: serialize each cooldown entry individually
                    var index = 0;
                    if (_bagCheckAbortCooldowns != null)
                    {
                        foreach (var kvp in _bagCheckAbortCooldowns)
                        {
                            var lordId = kvp.Key;
                            var cooldownEnd = kvp.Value;

                            SyncKey(dataStore, $"_bagCheckAbort_{index}_id", ref lordId);
                            SyncKey(dataStore, $"_bagCheckAbort_{index}_end", ref cooldownEnd);
                            index++;
                        }
                    }
                }
                else
                {
                    // Loading: reconstruct dictionary from individual fields
                    _bagCheckAbortCooldowns = new Dictionary<string, CampaignTime>();

                    for (var i = 0; i < cooldownCount; i++)
                    {
                        var lordId = "";
                        var cooldownEnd = CampaignTime.Zero;

                        SyncKey(dataStore, $"_bagCheckAbort_{i}_id", ref lordId);
                        SyncKey(dataStore, $"_bagCheckAbort_{i}_end", ref cooldownEnd);

                        if (!string.IsNullOrEmpty(lordId))
                        {
                            _bagCheckAbortCooldowns[lordId] = cooldownEnd;
                        }
                    }

                    ModLogger.Debug("SaveLoad", $"Loaded {_bagCheckAbortCooldowns.Count} bag check abort cooldowns");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("SaveLoad", "Error serializing bag check abort cooldowns", ex);
                // Ensure dictionary exists even on error
                if (_bagCheckAbortCooldowns == null)
                {
                    _bagCheckAbortCooldowns = new Dictionary<string, CampaignTime>();
                }
            }
        }

        /// <summary>
        /// Serializes the list of soldier names for save/load persistence.
        /// </summary>
        private void SyncSoldierNames(IDataStore dataStore)
        {
            try
            {
                var nameCount = _soldierNames?.Count ?? 0;
                SyncKey(dataStore, "_soldierNames_count", ref nameCount);

                if (!dataStore.IsLoading)
                {
                    // Saving: serialize each name
                    if (_soldierNames != null)
                    {
                        for (int i = 0; i < _soldierNames.Count; i++)
                        {
                            var name = _soldierNames[i];
                            SyncKey(dataStore, $"_soldierName_{i}", ref name);
                        }
                    }
                }
                else
                {
                    // Loading: reconstruct list
                    _soldierNames = new List<string>();
                    for (int i = 0; i < nameCount; i++)
                    {
                        var name = "";
                        SyncKey(dataStore, $"_soldierName_{i}", ref name);
                        if (!string.IsNullOrEmpty(name))
                        {
                            _soldierNames.Add(name);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Save", "Failed to sync soldier names", ex);
                if (dataStore.IsLoading)
                {
                    _soldierNames = new List<string>();
                }
            }
        }

        /// <summary>
        /// Serializes the XP sources dictionary for save/load persistence.
        /// Tracks XP earned by source during the current muster period for the period summary.
        /// </summary>
        private void SerializeXPSourcesDictionary(IDataStore dataStore)
        {
            try
            {
                var sourceCount = _xpSourcesThisPeriod?.Count ?? 0;
                SyncKey(dataStore, "_xpSources_count", ref sourceCount);

                if (!dataStore.IsLoading)
                {
                    // Saving: serialize each source and amount
                    var index = 0;
                    if (_xpSourcesThisPeriod != null)
                    {
                        foreach (var kvp in _xpSourcesThisPeriod)
                        {
                            var source = kvp.Key;
                            var amount = kvp.Value;
                            SyncKey(dataStore, $"_xpSource_{index}_name", ref source);
                            SyncKey(dataStore, $"_xpSource_{index}_amount", ref amount);
                            index++;
                        }
                    }
                }
                else
                {
                    // Loading: reconstruct dictionary from individual fields
                    _xpSourcesThisPeriod = new Dictionary<string, int>();

                    for (var i = 0; i < sourceCount; i++)
                    {
                        var source = "";
                        var amount = 0;
                        SyncKey(dataStore, $"_xpSource_{i}_name", ref source);
                        SyncKey(dataStore, $"_xpSource_{i}_amount", ref amount);

                        if (!string.IsNullOrEmpty(source))
                        {
                            _xpSourcesThisPeriod[source] = amount;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Save", "Failed to sync XP sources dictionary", ex);
                if (dataStore.IsLoading)
                {
                    _xpSourcesThisPeriod = new Dictionary<string, int>();
                }
            }
        }

        /// <summary>
        /// Serializes the list of issued rations for save/load persistence.
        /// Tracks rations issued at muster so they can be reclaimed at the next muster.
        /// </summary>
        private void SerializeIssuedRations(IDataStore dataStore)
        {
            try
            {
                var rationCount = _issuedRations?.Count ?? 0;
                SyncKey(dataStore, "_issuedRations_count", ref rationCount);

                if (!dataStore.IsLoading)
                {
                    // Saving: serialize each ration record individually
                    var index = 0;
                    if (_issuedRations != null)
                    {
                        foreach (var ration in _issuedRations)
                        {
                            var itemId = ration.ItemId;
                            var amount = ration.Amount;
                            var issuedAt = ration.IssuedAt;

                            SyncKey(dataStore, $"_issuedRation_{index}_itemId", ref itemId);
                            SyncKey(dataStore, $"_issuedRation_{index}_amount", ref amount);
                            SyncKey(dataStore, $"_issuedRation_{index}_issuedAt", ref issuedAt);
                            index++;
                        }
                    }
                }
                else
                {
                    // Loading: reconstruct list from individual fields
                    _issuedRations = new List<IssuedRationRecord>();

                    for (var i = 0; i < rationCount; i++)
                    {
                        var itemId = "";
                        var amount = 0;
                        var issuedAt = CampaignTime.Zero;

                        SyncKey(dataStore, $"_issuedRation_{i}_itemId", ref itemId);
                        SyncKey(dataStore, $"_issuedRation_{i}_amount", ref amount);
                        SyncKey(dataStore, $"_issuedRation_{i}_issuedAt", ref issuedAt);

                        if (!string.IsNullOrEmpty(itemId) && amount > 0)
                        {
                            _issuedRations.Add(new IssuedRationRecord
                            {
                                ItemId = itemId,
                                Amount = amount,
                                IssuedAt = issuedAt
                            });
                        }
                    }

                    ModLogger.Debug("SaveLoad", $"Loaded {_issuedRations.Count} issued ration records");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("SaveLoad", "Error serializing issued rations", ex);
                // Ensure list exists even on error
                if (_issuedRations == null)
                {
                    _issuedRations = new List<IssuedRationRecord>();
                }
            }
        }

        public bool CanEnlistWithParty(Hero lord, out TextObject reason)
        {
            reason = TextObject.GetEmpty(); // 1.3.4 API: Empty is now GetEmpty() method
            if (IsEnlisted)
            {
                reason = new TextObject("{=Enlisted_Message_AlreadyInService}You are already in service.");
                return false;
            }

            if (lord == null || !lord.IsLord)
            {
                reason = new TextObject("{=Enlisted_Message_MustSpeakToNoble}We must speak to a noble to enlist.");
                return false;
            }

            var main = MobileParty.MainParty;
            if (main == null)
            {
                reason = new TextObject("{=Enlisted_Message_NoMainParty}No main party found.");
                return false;
            }

            // Block enlistment if player is leading their own army - army must be disbanded first
            // This prevents crashes from army members being left in an undefined state
            if (main.Army != null && main.Army.LeaderParty == main)
            {
                reason = new TextObject("{=Enlisted_Message_MustDisbandArmy}You are leading an army. Disband your army before enlisting in another lord's service.");
                return false;
            }

            // Handle players on leave - allow same-faction transfers but block cross-faction enlistment
            if (_isOnLeave)
            {
                if (_enlistedLord != null && lord != _enlistedLord)
                {
                    // Allow transfer to other lords in the SAME faction/kingdom
                    var currentLordKingdom = _enlistedLord.MapFaction as Kingdom;
                    var targetLordKingdom = lord.MapFaction as Kingdom;

                    if (currentLordKingdom != null && targetLordKingdom == currentLordKingdom)
                    {
                        // Same faction - allow transfer (handled by TransferServiceToLord)
                        ModLogger.Debug("ENLISTMENT",
                            $"Allowing same-faction transfer check from {_enlistedLord.Name} to {lord.Name}");
                        // Continue to other checks (party validity, etc.)
                    }
                    else
                    {
                        // Different faction - block
                        reason = new TextObject(
                            "{=Enlisted_Message_OnLeaveCannotJoin}You are currently on leave from {LORD}. You cannot join a different faction without resigning first.");
                        _ = reason.SetTextVariable("LORD", _enlistedLord.Name ?? TextObject.GetEmpty());
                        return false;
                    }
                }

                if (_enlistedLord == null)
                {
                    reason = new TextObject(
                        "{=Enlisted_Message_OnLeaveReportBack}You are on leave and cannot enlist elsewhere until you report back.");
                    return false;
                }
            }

            // During grace period, enforce kingdom loyalty
            if (IsInDesertionGracePeriod && _pendingDesertionKingdom != null)
            {
                var lordKingdom = lord.MapFaction as Kingdom;
                if (lordKingdom != _pendingDesertionKingdom)
                {
                    reason = new TextObject(
                        "{=Enlisted_Message_BoundByGrace}You are still bound to {KINGDOM} by your grace orders. Other lords cannot enlist you yet.");
                    _ = reason.SetTextVariable("KINGDOM",
                        _pendingDesertionKingdom.Name ?? TextObject.GetEmpty()); // 1.3.4 API
                    return false;
                }
            }

            // Block re-enlistment with minor factions if player deserted from them
            // Minor factions have no crime rating system, so we use a cooldown-based block instead
            if (lord.MapFaction != null && !(lord.MapFaction is Kingdom) && IsBlockedFromMinorFaction(lord.MapFaction, out var remainingDays))
            {
                reason = new TextObject(
                    "{=Enlisted_Message_MinorFactionCooldown}{FACTION} will not accept you back for another {DAYS} days due to your past desertion.");
                _ = reason.SetTextVariable("FACTION", lord.MapFaction.Name);
                _ = reason.SetTextVariable("DAYS", remainingDays);
                return false;
            }

            // Block re-enlistment if player aborted the bag check with this lord recently
            if (IsBlockedFromLordDueToAbort(lord, out var abortDaysRemaining))
            {
                reason = new TextObject(
                    "{=Enlisted_Message_BagCheckAbortCooldown}{LORD} expects you to sort out your affairs before returning. Come back in {DAYS} days.");
                _ = reason.SetTextVariable("LORD", lord.Name ?? TextObject.GetEmpty());
                _ = reason.SetTextVariable("DAYS", abortDaysRemaining);
                return false;
            }

            var counterpartParty = MobileParty.ConversationParty ?? lord.PartyBelongedTo;

            // Fallback: If in a settlement, PartyBelongedTo might be null even if the lord has a party.
            // Attempt to find the lord's party via clan components.
            if (counterpartParty == null && lord.Clan != null)
            {
                foreach (var component in lord.Clan.WarPartyComponents)
                {
                    if (component.MobileParty != null && component.MobileParty.LeaderHero == lord)
                    {
                        counterpartParty = component.MobileParty;
                        break;
                    }
                }
            }

            if (counterpartParty == null)
            {
                reason = new TextObject("{=Enlisted_Message_LordNoParty}The lord has no party at present.");
                ModLogger.Debug("ENLISTMENT", $"CanEnlistWithParty: {lord.Name} has no party");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if the player can enlist with the specified faction.
        /// Returns false with a message if re-enlistment is blocked due to a bad discharge.
        /// </summary>
        public bool CanEnlistWithFaction(IFaction faction, out string blockedMessage)
        {
            blockedMessage = null;

            if (faction == null)
            {
                return true; // No faction check needed
            }

            var record = ServiceRecordManager.Instance?.GetOrCreateRecord(faction);
            if (record == null)
            {
                return true; // No record means no block
            }

            // Check re-enlistment block
            if (record.ReenlistmentBlockedUntil != CampaignTime.Zero &&
                CampaignTime.Now < record.ReenlistmentBlockedUntil)
            {
                var daysRemaining = (int)Math.Ceiling((record.ReenlistmentBlockedUntil - CampaignTime.Now).ToDays);
                var bandDisplay = record.LastDischargeBand switch
                {
                    "dishonorable" => "dishonorable discharge",
                    "deserter" => "desertion",
                    "washout" => "washout",
                    _ => "poor standing"
                };

                blockedMessage = $"Cannot enlist with {faction.Name}. Your {bandDisplay} bars re-enlistment for {daysRemaining} more days.";
                return false;
            }

            return true;
        }

        public void StartEnlist(Hero lord)
        {
            if (lord == null)
            {
                return;
            }

            // Check re-enlistment block before proceeding
            if (!CanEnlistWithFaction(lord.MapFaction, out var blockedMessage))
            {
                InformationManager.DisplayMessage(new InformationMessage(blockedMessage, Colors.Red));
                ModLogger.Info("ENLISTMENT", $"Re-enlistment blocked: {blockedMessage}");
                return;
            }

            // Check for cross-faction baggage transfer before proceeding.
            // If player has baggage stored with a different faction, prompt them to transfer it.
            if (HasCrossFactionBaggage(lord.MapFaction))
            {
                ShowCrossFactionBaggagePrompt(lord);
                return;
            }

            // Run enlistment bag check (stash/sell/smuggle) before proceeding
            // If the incidents behavior is unavailable, fail open so enlistment can continue.
            // For stability, schedule the bag check 1 in-game hour later so it never blocks enlistment.
            if (ShouldRunBagCheck() && !_bagCheckScheduled)
            {
                _pendingBagCheckLord = lord;
                _bagCheckScheduled = true;
                _bagCheckDueTime = CampaignTime.Now + CampaignTime.Hours(1f);
                ModLogger.Info("ENLISTMENT", $"Deferred bag check scheduled for {_bagCheckDueTime}");
            }

            ContinueStartEnlistInternal(lord);
        }

        public void HandleBagCheckChoice(string choice)
        {
            // Process all items currently in the player's inventory (personal gear + any equipped items stowed to inventory).
            EnsureBaggageStash();

            // Calculate storage fee: 200g + 5% of inventory value
            var totalValue = 0f;
            var partyRoster = MobileParty.MainParty?.ItemRoster;
            if (partyRoster != null)
            {
                foreach (var element in partyRoster)
                {
                    if (element.EquipmentElement.Item == null || element.Amount <= 0)
                    {
                        continue;
                    }
                    // Skip quest items in fee calculation
                    if (element.EquipmentElement.IsQuestItem)
                    {
                        continue;
                    }
                    totalValue += element.EquipmentElement.Item.Value * element.Amount;
                }
            }
            var storageFee = 200 + (int)Math.Floor(totalValue * 0.05f);

            switch (choice)
            {
                case "stash":
                    StashAllBelongings(Hero.MainHero, chargeFee: storageFee);
                    break;
                case "sell":
                    LiquidateAllBelongings(Hero.MainHero, 0.60f);
                    break;
                case "smuggle":
                    SmuggleOneItem(Hero.MainHero);
                    break;
                default:
                    // Default to stow to avoid enlistment without cleanup
                    StashAllBelongings(Hero.MainHero, chargeFee: storageFee);
                    break;
            }

            _bagCheckCompleted = true;
            _bagCheckEverCompleted = true;
            _bagCheckScheduled = false;
            _bagCheckDueTime = CampaignTime.Zero;
            _bagCheckInProgress = false;

            // Tag baggage stash with the faction where it was stored.
            // Used on future enlistment to detect cross-faction baggage that needs transfer.
            if (_baggageStash != null && _baggageStash.Count > 0 && _pendingBagCheckLord?.MapFaction != null)
            {
                _baggageStashFactionId = _pendingBagCheckLord.MapFaction.StringId;
                ModLogger.Info("ENLISTMENT", $"Baggage stash tagged with faction: {_baggageStashFactionId}");
            }

            _pendingBagCheckLord = null;

            // NOTE: Do NOT call ContinueStartEnlistInternal here!
            // The player is already enlisted from StartEnlist() → ContinueStartEnlistInternal() (first call).
            // Calling it again here causes:
            // 1. OnEnlisted event to fire twice
            // 2. Onboarding state to be re-initialized
            // 3. Queued onboarding events to be dropped as "no longer eligible"
            // The bag check should ONLY process equipment, not re-run the enlistment flow.
            ModLogger.Info("ENLISTMENT", "Bag check completed successfully");
        }

        /// <summary>
        ///     Processes a deferred enlistment bag check after a cooldown so enlistment flow is never blocked.
        /// </summary>
        private void ProcessDeferredBagCheck(bool force = false)
        {
            if (!_bagCheckScheduled || _bagCheckCompleted || _bagCheckInProgress)
            {
                return;
            }

            if (!force && CampaignTime.Now < _bagCheckDueTime)
            {
                return;
            }

            var main = MobileParty.MainParty;
            if (main == null)
            {
                return;
            }

            // Only fire when safe: not in active battle or captivity
            // Note: We allow firing during non-battle encounters (like dialogue) since the player
            // just enlisted and is in a safe state. The event will queue and fire when dialogue closes.
            bool inBattle = main.Party?.MapEvent != null;
            bool isPrisoner = Hero.MainHero?.IsPrisoner == true;

            if (inBattle || isPrisoner)
            {
                // Retry next hour
                ModLogger.Debug("ENLISTMENT", $"Bag check deferred: inBattle={inBattle}, isPrisoner={isPrisoner}");
                return;
            }

            // Use the narrative event system for immersive bag check experience
            var bagCheckEvent = EventCatalog.GetEvent("evt_baggage_stowage_first_enlistment");
            var eventDelivery = EventDeliveryManager.Instance;

            if (bagCheckEvent != null)
            {
                _bagCheckInProgress = true;
                ModLogger.Info("ENLISTMENT", $"Triggering bag check event (scheduled at {_bagCheckDueTime})");

                var director = StoryDirector.Instance;
                if (director != null)
                {
                    director.EmitCandidate(new StoryCandidate
                    {
                        SourceId = "enlistment.bag_check",
                        CategoryId = "enlistment.bag_check",
                        ProposedTier = StoryTier.Modal,
                        SeverityHint = 0.90f,
                        Beats = { StoryBeat.OrderComplete },
                        Relevance = new RelevanceKey { TouchesEnlistedLord = true },
                        EmittedAt = CampaignTime.Now,
                        InteractiveEvent = bagCheckEvent,
                        RenderedTitle = bagCheckEvent.TitleFallback,
                        RenderedBody = bagCheckEvent.SetupFallback,
                        StoryKey = bagCheckEvent.Id,
                        ChainContinuation = true
                    });
                    return;
                }

                if (eventDelivery != null)
                {
                    eventDelivery.QueueEvent(bagCheckEvent);
                    return;
                }
            }

            // Fail open if event system unavailable
            ModLogger.Warn("ENLISTMENT",
                "Bag check event system unavailable; marking bag check complete to avoid blocking enlistment");
            _bagCheckCompleted = true;
            _bagCheckEverCompleted = true;
            _bagCheckScheduled = false;
            _bagCheckDueTime = CampaignTime.Zero;
            _pendingBagCheckLord = null;
        }

        private void EnsureBaggageStash()
        {
            _baggageStash ??= new ItemRoster();
        }

        /// <summary>
        /// Returns true if the player has at least one item in their baggage stash.
        /// Used by events that require items to exist (theft events).
        /// </summary>
        public bool HasBaggageItems()
        {
            return _baggageStash != null && _baggageStash.Count > 0;
        }

        /// <summary>
        /// Removes a random number of items from the baggage stash.
        /// Returns the actual number of items removed (may be less than requested if stash is nearly empty).
        /// Used by theft, raid, and loss events. Gracefully handles empty stash.
        /// </summary>
        public int RemoveRandomBaggageItems(int count)
        {
            if (_baggageStash == null || _baggageStash.Count == 0 || count <= 0)
            {
                return 0;
            }

            var random = new Random();
            var removed = 0;

            for (var i = 0; i < count && _baggageStash.Count > 0; i++)
            {
                // Get a random item from the stash
                var itemList = _baggageStash.ToList();
                if (itemList.Count == 0)
                {
                    break;
                }

                var randomIndex = random.Next(itemList.Count);
                var selectedItem = itemList[randomIndex];

                if (selectedItem.EquipmentElement.Item != null && selectedItem.Amount > 0)
                {
                    _ = _baggageStash.AddToCounts(selectedItem.EquipmentElement, -1);
                    removed++;

                    ModLogger.Info("Baggage", $"Lost from stash: {selectedItem.EquipmentElement.Item.Name}");
                }
            }

            return removed;
        }

        /// <summary>
        /// Checks if the player has baggage stored with a faction different from the one they're joining.
        /// Used to trigger a cross-faction transfer prompt before enlistment.
        /// </summary>
        private bool HasCrossFactionBaggage(IFaction newFaction)
        {
            // No baggage = no transfer needed
            if (_baggageStash == null || _baggageStash.Count == 0)
            {
                return false;
            }

            // No faction tag = legacy baggage, assume it's with the player (no transfer needed)
            if (string.IsNullOrWhiteSpace(_baggageStashFactionId))
            {
                return false;
            }

            // Compare faction IDs - if different, trigger transfer
            var newFactionId = newFaction?.StringId;
            if (string.IsNullOrWhiteSpace(newFactionId))
            {
                return false;
            }

            // Grace period re-enlistment: if baggage is from the pending grace kingdom, don't prompt
            // This handles the case where lord died and player re-enlists with same kingdom
            if (IsInDesertionGracePeriod && _pendingDesertionKingdom != null)
            {
                if (string.Equals(_baggageStashFactionId, _pendingDesertionKingdom.StringId, StringComparison.OrdinalIgnoreCase))
                {
                    // Baggage is from the same kingdom we're rejoining via grace period
                    // No transfer needed - just continue and tag with new faction on next bag check
                    return false;
                }
            }

            return !string.Equals(_baggageStashFactionId, newFactionId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Shows a prompt for handling baggage stored with a different faction.
        /// Player can send a courier, sell remotely, or abandon the baggage.
        /// </summary>
        private void ShowCrossFactionBaggagePrompt(Hero lord)
        {
            try
            {
                var itemCount = _baggageStash?.Count ?? 0;
                var totalValue = CalculateBaggageValue();
                var courierCost = CalculateCourierCost();
                var sellValue = (int)(totalValue * 0.40f); // 40% of value for remote sale

                // Resolve faction name for display
                var storedFactionName = ResolveFactionName(_baggageStashFactionId) ?? "unknown faction";
                var newFactionName = lord?.MapFaction?.Name?.ToString() ?? "this lord";

                var bodyText = new TextObject(
                    "{=baggage_transfer_body}The quartermaster clears his throat. \"You've got {ITEM_COUNT} items stored with {OLD_FACTION}'s baggage train. We're with {NEW_FACTION} now. What do you want done with your old kit?\"")
                    .SetTextVariable("ITEM_COUNT", itemCount)
                    .SetTextVariable("OLD_FACTION", storedFactionName)
                    .SetTextVariable("NEW_FACTION", newFactionName)
                    .ToString();

                var options = new List<InquiryElement>
                {
                    new InquiryElement("courier",
                        new TextObject("{=baggage_courier}\"Send a courier\" ({COST}g, 3-day delay)")
                            .SetTextVariable("COST", courierCost).ToString(),
                        null,
                        Hero.MainHero?.Gold >= courierCost,
                        new TextObject("{=baggage_courier_hint}Pay a courier to retrieve your belongings. They'll arrive in about 3 days.")
                            .ToString()),
                    new InquiryElement("sell",
                        new TextObject("{=baggage_sell_remote}\"Sell it all remotely\" ({GOLD}g)")
                            .SetTextVariable("GOLD", sellValue).ToString(),
                        null,
                        true,
                        new TextObject("{=baggage_sell_hint}Have a merchant buy everything at a reduced rate. The gold will be sent to you.")
                            .ToString()),
                    new InquiryElement("abandon",
                        new TextObject("{=baggage_abandon}\"Leave it behind\"").ToString(),
                        null,
                        true,
                        new TextObject("{=baggage_abandon_hint}Abandon your stored items. They'll be lost forever.")
                            .ToString())
                };

                var inquiry = new MultiSelectionInquiryData(
                    new TextObject("{=baggage_transfer_title}Cross-Faction Baggage").ToString(),
                    bodyText,
                    options,
                    false,
                    1,
                    1,
                    new TextObject("{=str_continue}Continue").ToString(),
                    new TextObject("{=str_cancel}Cancel").ToString(),
                    selection =>
                    {
                        var choice = selection?.FirstOrDefault()?.Identifier as string;
                        HandleCrossFactionBaggageChoice(choice, lord, courierCost, sellValue);
                    },
                    _ =>
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            new TextObject("{=baggage_transfer_cancelled}Enlistment cancelled.").ToString(),
                            Colors.Yellow));
                        ModLogger.Info("ENLISTMENT", "Cross-faction baggage prompt cancelled; enlistment aborted.");
                    });

                MBInformationManager.ShowMultiSelectionInquiry(inquiry);
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("ENLISTMENT", "Cross-faction baggage prompt failed", ex);
                // Fail open: continue enlistment without transfer
                ContinueEnlistmentAfterBaggageTransfer(lord);
            }
        }

        /// <summary>
        /// Handles the player's choice for cross-faction baggage transfer.
        /// </summary>
        private void HandleCrossFactionBaggageChoice(string choice, Hero lord, int courierCost, int sellValue)
        {
            try
            {
                // Null or empty choice = user closed dialog without selecting
                if (string.IsNullOrWhiteSpace(choice))
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=baggage_transfer_cancelled}Enlistment cancelled.").ToString(),
                        Colors.Yellow));
                    ModLogger.Info("ENLISTMENT", "Cross-faction baggage choice was null/empty; enlistment aborted.");
                    return;
                }

                switch (choice)
                {
                    case "courier":
                        // Pay courier fee and schedule baggage delivery
                        if (Hero.MainHero?.Gold >= courierCost)
                        {
                            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, courierCost);

                            // Move baggage to pending courier delivery
                            // If there's already a pending courier (rare edge case), merge items into it
                            if (_pendingCourierBaggage == null)
                            {
                                _pendingCourierBaggage = new ItemRoster();
                            }

                            foreach (var item in _baggageStash.ToList())
                            {
                                if (item.EquipmentElement.Item != null && item.Amount > 0)
                                {
                                    _ = _pendingCourierBaggage.AddToCounts(item.EquipmentElement, item.Amount);
                                }
                            }
                            _baggageStash.Clear();
                            _baggageStashFactionId = null;

                            // Schedule/reset arrival to 3 days from now
                            _baggageCourierArrivalTime = CampaignTime.Now + CampaignTime.Days(3);

                            InformationManager.DisplayMessage(new InformationMessage(
                                new TextObject("{=baggage_courier_sent}Courier dispatched. Your belongings will arrive in 3 days.")
                                    .ToString()));
                            ModLogger.Info("ENLISTMENT",
                                $"Courier dispatched for {_pendingCourierBaggage.Count} items, arrives at {_baggageCourierArrivalTime}");
                        }
                        else
                        {
                            InformationManager.DisplayMessage(new InformationMessage(
                                new TextObject("{=baggage_no_gold}Not enough gold for the courier.").ToString(),
                                Colors.Red));
                            return;
                        }
                        break;

                    case "sell":
                        // Sell baggage at reduced rate
                        if (sellValue > 0)
                        {
                            GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, sellValue);
                            InformationManager.DisplayMessage(new InformationMessage(
                                new TextObject("{=baggage_sold_remote}Your old belongings were sold for {GOLD} denars.")
                                    .SetTextVariable("GOLD", sellValue).ToString()));
                        }
                        _baggageStash?.Clear();
                        _baggageStashFactionId = null;
                        ModLogger.Info("ENLISTMENT", $"Remote baggage sale completed for {sellValue} denars");
                        break;

                    case "abandon":
                        // Simply discard the baggage
                        var abandonedCount = _baggageStash?.Count ?? 0;
                        _baggageStash?.Clear();
                        _baggageStashFactionId = null;
                        InformationManager.DisplayMessage(new InformationMessage(
                            new TextObject("{=baggage_abandoned}Your old belongings have been abandoned.").ToString(),
                            Colors.Yellow));
                        ModLogger.Info("ENLISTMENT", $"Abandoned {abandonedCount} baggage items");
                        break;

                    default:
                        // No choice = abort enlistment
                        ModLogger.Info("ENLISTMENT", "Cross-faction baggage choice cancelled");
                        return;
                }

                // Continue with enlistment
                ContinueEnlistmentAfterBaggageTransfer(lord);
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("ENLISTMENT", "Error handling baggage transfer choice", ex);
                // Fail open: continue enlistment
                ContinueEnlistmentAfterBaggageTransfer(lord);
            }
        }

        /// <summary>
        /// Continues the enlistment process after baggage transfer is resolved.
        /// </summary>
        private void ContinueEnlistmentAfterBaggageTransfer(Hero lord)
        {
            if (lord == null)
            {
                return;
            }

            // Schedule normal bag check if needed
            if (ShouldRunBagCheck() && !_bagCheckScheduled)
            {
                _pendingBagCheckLord = lord;
                _bagCheckScheduled = true;
                _bagCheckDueTime = CampaignTime.Now + CampaignTime.Hours(1f);
                ModLogger.Info("ENLISTMENT", $"Deferred bag check scheduled for {_bagCheckDueTime}");
            }

            ContinueStartEnlistInternal(lord);
        }

        /// <summary>
        /// Checks if a courier baggage delivery has arrived and delivers items to the player.
        /// Called each hour to check for pending deliveries.
        /// </summary>
        private void ProcessCourierArrival()
        {
            // No pending delivery
            if (_baggageCourierArrivalTime == CampaignTime.Zero || _pendingCourierBaggage == null || _pendingCourierBaggage.Count == 0)
            {
                return;
            }

            // Not yet arrived
            if (CampaignTime.Now < _baggageCourierArrivalTime)
            {
                return;
            }

            try
            {
                // Don't deliver to prisoners - wait until released
                if (Hero.MainHero?.IsPrisoner == true)
                {
                    return;
                }

                var partyRoster = MobileParty.MainParty?.ItemRoster;
                if (partyRoster == null)
                {
                    // Player has no party, defer delivery
                    ModLogger.Info("ENLISTMENT", "Courier arrived but player has no party, delivery deferred");
                    return;
                }

                // Deliver items to player inventory
                var deliveredCount = 0;
                foreach (var item in _pendingCourierBaggage.ToList())
                {
                    if (item.EquipmentElement.Item != null && item.Amount > 0)
                    {
                        _ = partyRoster.AddToCounts(item.EquipmentElement, item.Amount);
                        deliveredCount += item.Amount;
                    }
                }

                // Clear pending delivery
                _pendingCourierBaggage.Clear();
                _pendingCourierBaggage = null;
                _baggageCourierArrivalTime = CampaignTime.Zero;

                // Notify player via immediate message
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=baggage_courier_arrived}A courier has arrived with your belongings ({COUNT} items).")
                        .SetTextVariable("COUNT", deliveredCount).ToString(),
                    Colors.Green));

                // Add to personal news feed for camp activities display
                try
                {
                    var newsText = new TextObject("{=baggage_courier_news}A courier delivered {COUNT} items from your old posting.")
                        .SetTextVariable("COUNT", deliveredCount).ToString();
                    EnlistedNewsBehavior.Instance?.PostPersonalDispatchText(
                        "logistics",
                        newsText,
                        $"courier:{(int)CampaignTime.Now.ToDays}");
                }
                catch (Exception newsEx)
                {
                    ModLogger.Caught("News", "Failed to post courier arrival news", newsEx);
                }

                ModLogger.Info("ENLISTMENT", $"Courier delivered {deliveredCount} items to player inventory");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("ENLISTMENT", "Error processing courier arrival", ex);
            }
        }

        /// <summary>
        /// Calculates the total value of items in the baggage stash.
        /// </summary>
        private int CalculateBaggageValue()
        {
            if (_baggageStash == null || _baggageStash.Count == 0)
            {
                return 0;
            }

            var total = 0;
            foreach (var item in _baggageStash)
            {
                if (item.EquipmentElement.Item != null && item.Amount > 0)
                {
                    total += item.EquipmentElement.Item.Value * item.Amount;
                }
            }
            return total;
        }

        /// <summary>
        /// Calculates courier cost based on baggage value.
        /// Base fee of 50g plus 5% of item value, minimum 50g.
        /// </summary>
        private int CalculateCourierCost()
        {
            var baggageValue = CalculateBaggageValue();
            var cost = 50 + (int)(baggageValue * 0.05f);
            return Math.Max(50, cost);
        }

        /// <summary>
        /// Resolves a faction StringId to its display name.
        /// Returns null if faction not found.
        /// </summary>
        private string ResolveFactionName(string factionId)
        {
            if (string.IsNullOrWhiteSpace(factionId))
            {
                return null;
            }

            // Try kingdoms first
            var kingdom = Campaign.Current?.Kingdoms?.FirstOrDefault(k =>
                string.Equals(k.StringId, factionId, StringComparison.OrdinalIgnoreCase));
            if (kingdom != null)
            {
                return kingdom.Name?.ToString();
            }

            // Try clans
            var clan = Campaign.Current?.Clans?.FirstOrDefault(c =>
                string.Equals(c.StringId, factionId, StringComparison.OrdinalIgnoreCase));
            return clan?.Name?.ToString();
        }

        /// <summary>
        /// Return any items left in the baggage train stash to the player's party inventory.
        /// Also delivers any pending courier baggage immediately on discharge.
        /// This prevents "stored belongings" from becoming inaccessible after discharge.
        /// </summary>
        private void ReturnBaggageStashToPlayerInventory()
        {
            try
            {
                var partyRoster = MobileParty.MainParty?.ItemRoster;
                if (partyRoster == null)
                {
                    return;
                }

                var returnedCount = 0;

                // Return baggage stash items
                if (_baggageStash != null && _baggageStash.Count > 0)
                {
                    var elements = _baggageStash.ToList();
                    foreach (var element in elements)
                    {
                        if (element.EquipmentElement.Item == null || element.Amount <= 0)
                        {
                            continue;
                        }

                        _ = partyRoster.AddToCounts(element.EquipmentElement, element.Amount);
                        _ = _baggageStash.AddToCounts(element.EquipmentElement, -element.Amount);
                        returnedCount++;
                    }
                }

                // Also deliver any pending courier baggage immediately on discharge
                // (don't make player wait for courier if they're leaving service)
                if (_pendingCourierBaggage != null && _pendingCourierBaggage.Count > 0)
                {
                    foreach (var item in _pendingCourierBaggage.ToList())
                    {
                        if (item.EquipmentElement.Item != null && item.Amount > 0)
                        {
                            _ = partyRoster.AddToCounts(item.EquipmentElement, item.Amount);
                            returnedCount++;
                        }
                    }
                    _pendingCourierBaggage.Clear();
                    _pendingCourierBaggage = null;
                    _baggageCourierArrivalTime = CampaignTime.Zero;
                }

                // Clear faction tag since baggage is now with the player
                _baggageStashFactionId = null;

                if (returnedCount > 0)
                {
                    ModLogger.Info("ENLISTMENT", $"Returned baggage to player inventory ({returnedCount} stacks).");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("ENLISTMENT", "Error returning baggage train stash to inventory", ex);
            }
        }

        private void StashAllBelongings(Hero hero, int chargeFee)
        {
            try
            {
                EnsureBaggageStash();

                // Build set of issued ration item IDs to protect from stashing
                var issuedRationIds = new HashSet<string>();
                if (_issuedRations != null)
                {
                    foreach (var ration in _issuedRations)
                    {
                        if (!string.IsNullOrEmpty(ration.ItemId))
                        {
                            _ = issuedRationIds.Add(ration.ItemId);
                        }
                    }
                }

                var partyRoster = MobileParty.MainParty?.ItemRoster;
                if (partyRoster != null)
                {
                    var questItemsKept = 0;
                    var rationsKept = 0;
                    foreach (var element in partyRoster.ToList())
                    {
                        if (element.EquipmentElement.Item != null && element.Amount > 0)
                        {
                            // CRITICAL: Skip quest items - they must stay with the player
                            if (element.EquipmentElement.IsQuestItem)
                            {
                                questItemsKept++;
                                continue;
                            }

                            // CRITICAL: Skip issued rations - military issue stays with soldier
                            if (issuedRationIds.Contains(element.EquipmentElement.Item.StringId))
                            {
                                rationsKept++;
                                continue;
                            }

                            _ = _baggageStash.AddToCounts(element.EquipmentElement.Item, element.Amount);
                        }
                    }

                    // Clear non-quest, non-issued-ration items from roster
                    var itemsToRemove = partyRoster.Where(e =>
                        !e.EquipmentElement.IsQuestItem &&
                        !issuedRationIds.Contains(e.EquipmentElement.Item?.StringId ?? "")
                    ).ToList();
                    foreach (var item in itemsToRemove)
                    {
                        _ = partyRoster.AddToCounts(item.EquipmentElement, -item.Amount);
                    }

                    if (questItemsKept > 0)
                    {
                        ModLogger.Info("EQUIPMENT", $"Protected {questItemsKept} quest item(s) from inventory stashing");
                    }
                    if (rationsKept > 0)
                    {
                        ModLogger.Info("Rations", $"Protected {rationsKept} issued ration(s) from inventory stashing");
                    }
                }

                // DO NOT stash equipped items - they've already been replaced with military equipment at enlistment
                // The bag check should only process inventory items (civilian gear that wasn't equipped)

                if (chargeFee > 0)
                {
                    // Use party gold (what the player sees in UI) not hero personal gold
                    var partyGold = Hero.MainHero?.PartyBelongedTo?.PartyTradeGold ?? 0;
                    var fee = Math.Min(chargeFee, partyGold);
                    if (fee > 0)
                    {
                        // GiveGoldAction properly deducts from party treasury and updates UI
                        GiveGoldAction.ApplyBetweenCharacters(hero, null, fee);
                        InformationManager.DisplayMessage(new InformationMessage(
                            new TextObject("{=qm_fee_paid}You pay {FEE} denars for the wagon fee.")
                                .SetTextVariable("FEE", fee).ToString()));
                        ModLogger.Info("Gold", $"Wagon fee paid: {fee} denars");
                    }
                    else
                    {
                        // Player has no gold - still proceed but log it
                        ModLogger.Info("Gold", "No gold available for wagon fee - proceeding without charge.");
                    }
                }

                ModLogger.Info("ENLISTMENT", $"Stashed belongings; stash now {_baggageStash.Count} entries.");
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("ENLISTMENT", "Error stashing belongings", ex);
            }
        }

        private void LiquidateAllBelongings(Hero hero, float rate)
        {
            try
            {
                // Build set of issued ration item IDs to protect from liquidation
                var issuedRationIds = new HashSet<string>();
                if (_issuedRations != null)
                {
                    foreach (var ration in _issuedRations)
                    {
                        if (!string.IsNullOrEmpty(ration.ItemId))
                        {
                            _ = issuedRationIds.Add(ration.ItemId);
                        }
                    }
                }

                var totalValue = 0f;
                var questItemsKept = 0;
                var rationsKept = 0;
                var partyRoster = MobileParty.MainParty?.ItemRoster;
                if (partyRoster != null)
                {
                    foreach (var element in partyRoster.ToList())
                    {
                        if (element.EquipmentElement.Item != null && element.Amount > 0)
                        {
                            // CRITICAL: Skip quest items - they cannot be sold
                            if (element.EquipmentElement.IsQuestItem)
                            {
                                questItemsKept++;
                                continue;
                            }

                            // CRITICAL: Skip issued rations - military issue stays with soldier
                            if (issuedRationIds.Contains(element.EquipmentElement.Item.StringId))
                            {
                                rationsKept++;
                                continue;
                            }

                            totalValue += element.EquipmentElement.Item.Value * element.Amount * rate;
                        }
                    }

                    // Clear only non-quest, non-issued-ration items from roster
                    var itemsToRemove = partyRoster.Where(e =>
                        !e.EquipmentElement.IsQuestItem &&
                        !issuedRationIds.Contains(e.EquipmentElement.Item?.StringId ?? "")
                    ).ToList();
                    foreach (var item in itemsToRemove)
                    {
                        _ = partyRoster.AddToCounts(item.EquipmentElement, -item.Amount);
                    }

                    if (questItemsKept > 0)
                    {
                        ModLogger.Info("EQUIPMENT", $"Protected {questItemsKept} quest item(s) from liquidation");
                    }
                    if (rationsKept > 0)
                    {
                        ModLogger.Info("Rations", $"Protected {rationsKept} issued ration(s) from liquidation");
                    }
                }

                // DO NOT sell equipped items - they've already been replaced with military equipment at enlistment
                // The bag check should only process inventory items (civilian gear that wasn't equipped)

                var gain = (int)Math.Floor(totalValue);
                if (gain > 0)
                {
                    // GiveGoldAction properly adds to party treasury and updates UI
                    GiveGoldAction.ApplyBetweenCharacters(null, hero, gain);
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=qm_liquidate_gain}You receive {GOLD} denars from liquidating your possessions.")
                            .SetTextVariable("GOLD", gain).ToString()));
                }

                ModLogger.Info("ENLISTMENT", $"Liquidated belongings for {gain} denars at rate {rate}");
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("ENLISTMENT", "Error liquidating belongings", ex);
            }
        }

        private void SmuggleOneItem(Hero hero)
        {
            try
            {
                EnsureBaggageStash();

                var allItems = new List<ItemRosterElement>();

                var partyRoster = MobileParty.MainParty?.ItemRoster;
                if (partyRoster != null)
                {
                    allItems.AddRange(partyRoster.ToList());
                }

                allItems.AddRange(GetEquipmentElements(hero.BattleEquipment));
                allItems.AddRange(GetEquipmentElements(hero.CivilianEquipment));

                // Calculate total inventory value for fee calculation
                var totalValue = 0f;
                foreach (var element in allItems)
                {
                    if (element.EquipmentElement.Item == null || element.Amount <= 0)
                    {
                        continue;
                    }
                    totalValue += element.EquipmentElement.Item.Value * element.Amount;
                }

                // Smuggle check: trying to stow everything without paying the storage fee
                var roguery = hero.GetSkillValue(DefaultSkills.Roguery);
                var difficulty = 80; // High difficulty
                var roll = MBRandom.RandomInt(100);
                var successThreshold = Math.Max(0, roguery - (difficulty - 50));
                var success = roll < successThreshold;

                ModLogger.Info("BagCheck", $"Smuggle attempt (fallback): Roguery {roguery}, Roll {roll} vs threshold {successThreshold} = {(success ? "SUCCESS" : "FAIL")}");

                // Always stash everything (fee only if caught)
                var storageFee = success ? 0 : 200 + (int)Math.Floor(totalValue * 0.05f);
                StashAllBelongings(hero, storageFee);

                if (success)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=qm_smuggle_success}You slip everything past the ledger without paying. The clerk never noticed.")
                            .ToString(),
                        Colors.Green));
                }
                else
                {
                    // Caught - scrutiny penalty for attempted deception
                    var escalation = EscalationManager.Instance?.State;
                    if (escalation != null)
                    {
                        escalation.Scrutiny = Math.Min(100, escalation.Scrutiny + 1);
                    }

                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=qm_smuggle_fail}Caught trying to dodge the fee. You pay anyway, and the clerk makes a note in his ledger.")
                            .ToString(),
                        Colors.Red));
                }

                ModLogger.Info("ENLISTMENT", $"Smuggle attempt {(success ? "succeeded" : "failed")} - fee {(success ? "avoided" : "charged")}");
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("ENLISTMENT", "Error smuggling item", ex);
            }
        }

        private List<ItemRosterElement> GetEquipmentElements(TaleWorlds.Core.Equipment equipment)
        {
            var list = new List<ItemRosterElement>();
            if (equipment == null)
            {
                return list;
            }

            // Use numeric iteration to avoid invalid enum values like NumEquipmentSetSlots
            for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
            {
                var slot = (EquipmentIndex)i;
                var elem = equipment[slot];
                if (elem.Item != null)
                {
                    list.Add(new ItemRosterElement(elem, 1));
                }
            }

            return list;
        }

        private void ContinueStartEnlistInternal(Hero lord)
        {
            if (lord == null)
            {
                return;
            }

            try
            {
                var rejoiningKingdom = lord.MapFaction as Kingdom;
                _graceProtectionEnds = CampaignTime.Zero;
                var resumingGraceService = IsInDesertionGracePeriod && rejoiningKingdom == _pendingDesertionKingdom &&
                                           _savedGraceTier > 0;
                var graceTier = _savedGraceTier;
                var graceXP = _savedGraceXP;
                var graceTroopId = _savedGraceTroopId;

                // Protect player's ships from damage during enlistment
                // Ships remain with the player but cannot take damage while serving under a lord
                SetPlayerShipsInvulnerable();

                // Initialize enlistment state with default values
                _enlistedLord = lord;
                if (_enlistedLord != null)
                {
                    Campaign.Current.VisualTrackerManager.RemoveTrackedObject(_enlistedLord);
                }

                // Generate NCO and soldier names for personalized event text
                GenerateNcoAndSoldierNames();

                // Initialize supply simulation for the company
                // Preserve supply level during grace period re-enlistment (same as tier/XP preservation)
                CompanySupplyManager.Initialize(lord, preserveSupply: resumingGraceService);

                // Initialize quartermaster inventory at enlistment start so items are available immediately
                // Only initialize on new enlistment; grace period re-enlistment preserves existing inventory
                if (!resumingGraceService)
                {
                    try
                    {
                        QuartermasterManager.Instance?.RollStockAvailability();
                        ModLogger.Info("ENLISTMENT", "Initialized quartermaster inventory at enlistment start");
                    }
                    catch (Exception ex)
                    {
                        ModLogger.Caught("QUARTERMASTER", "Failed to initialize inventory at enlistment start", ex);
                    }
                }

                SyncActivationState("start_enlist");

                var resumedFromGrace = resumingGraceService;
                string experienceTrack = null;

                if (resumedFromGrace)
                {
                    _enlistmentTier = Math.Max(1, graceTier);
                    _enlistmentXP = Math.Max(0, graceXP);
                }
                else
                {
                    // Determine starting tier based on player experience and faction history.
                    // Higher-level characters start at higher tiers, and prior faction service
                    // can boost the starting tier (HighestTier - 2, minimum 1, capped at T3).
                    experienceTrack = ExperienceTrackHelper.GetExperienceTrack(Hero.MainHero);
                    var factionRecord = ServiceRecordManager.Instance?.GetOrCreateRecord(lord.MapFaction);
                    var startingTier = ExperienceTrackHelper.GetStartingTierForTrack(experienceTrack, factionRecord);

                    _enlistmentTier = startingTier;
                    _enlistmentXP = 0;

                    ModLogger.Info("ENLISTMENT",
                        $"Experience track: {experienceTrack}, base tier: {startingTier} " +
                        $"(player level: {Hero.MainHero?.Level ?? 0}, " +
                        $"faction highest tier: {factionRecord?.HighestTier ?? 0})");
                }

                _enlistmentDate = resumedFromGrace && _savedGraceEnlistmentDate != CampaignTime.Zero
                    ? _savedGraceEnlistmentDate
                    : CampaignTime.Now; // Record when service started (or resume previous enlistment date)
                _pendingMusterPay = 0;
                _payMusterPending = false;
                _nextPayday = CampaignTime.Zero;
                _lastPayOutcome = null;
                _isPensionPaused = true; // Pension payments pause while enlisted
                _isPendingDischarge = false;
                _lastDischargeBand = null;
                _isOnProbation = false;
                _probationEnds = CampaignTime.Zero;

                // Initialize muster tracking - set last muster to enlistment day so we don't trigger muster immediately
                if (!resumedFromGrace)
                {
                    _lastMusterDay = Campaign.Current != null ? (int)CampaignTime.Now.ToDays : 0;
                    _tierAtLastMuster = _enlistmentTier;
                    _xpAtLastMuster = _enlistmentXP;
                    ModLogger.Info("ENLISTMENT", $"Initialized muster tracking: lastMusterDay={_lastMusterDay}, tier={_tierAtLastMuster}, xp={_xpAtLastMuster}");
                }

                // Clear issued rations tracking on new enlistment
                // Note: Grace period re-enlistment preserves tier/XP but not issued rations
                // Each new service period starts with a fresh ration slate
                if (_issuedRations == null)
                {
                    _issuedRations = new List<IssuedRationRecord>();
                }
                else
                {
                    _issuedRations.Clear();
                }


                // Reset term-based notification state (used only for informational prompts)
                _retirementNotificationShown = false;
                _currentTermKills = 0;

                TryApplyReservistReentryBoost(_enlistedLord?.MapFaction);

                // Show experience track notification AFTER all tier calculations are complete.
                // This ensures the displayed tier includes any reservist bonus adjustments.
                if (!resumedFromGrace && experienceTrack != null)
                {
                    ShowExperienceTrackNotification(experienceTrack, _enlistmentTier);
                }

                // Duty registration is handled by the Orders system

                // Log enlistment start for diagnostics
                SessionDiagnostics.LogStateTransition("Enlistment", "Civilian", "Enlisted",
                    $"Lord: {lord.Name}, Kingdom: {lord.MapFaction?.Name?.ToString() ?? "None"}, Tier: {_enlistmentTier}, ResumedGrace: {resumedFromGrace}");

                // Store the player's original kingdom/clan state before joining the lord's faction
                // This information is needed to restore the player's original kingdom when service ends
                var playerClan = Clan.PlayerClan;
                _originalKingdom = playerClan?.Kingdom;
                _wasIndependentClan = playerClan?.Kingdom == null;

                // Join lord's kingdom if they have one and player isn't already in it
                var lordKingdom = lord.MapFaction as Kingdom;
                if (lordKingdom != null && playerClan != null)
                {
                    if (playerClan.Kingdom != lordKingdom)
                    {
                        try
                        {
                            // Join the lord's kingdom as a mercenary (enlisted soldiers are mercenaries, not vassals)
                            // The awardMultiplier parameter controls the mercenary wage rate (default 50)
                            ChangeKingdomAction.ApplyByJoinFactionAsMercenary(playerClan, lordKingdom,
                                default(CampaignTime), 0, false);
                            ModLogger.Info("ENLISTMENT",
                                $"Joined {lordKingdom.Name} as mercenary while enlisted with {lord.Name}");
                        }
                        catch (Exception ex)
                        {
                            ModLogger.Surfaced("ENLISTMENT", "Error joining lord's kingdom", ex,
                                ctx: LogCtx.Of("Lord", lord?.Name?.ToString(), "Kingdom", lordKingdom?.Name?.ToString()));
                        }
                    }
                    else
                    {
                        ModLogger.Debug("ENLISTMENT", "Player already in lord's kingdom");
                    }
                }
                else if (lordKingdom == null)
                {
                    // MINOR FACTION FIX: Mirror the lord's faction war relations to the player clan
                    // Without this, nameplates don't show green/red colors and battle participation fails
                    // because the player has no faction relationships with enemies/allies.
                    MirrorMinorFactionWarRelations(lord);
                }

                // Transfer any existing companions and troops from the player's party to the lord's party
                // This prevents the player from having their own troops while enlisted, as they're
                // now part of the lord's military force. Troops will be restored when service ends.
                TransferPlayerTroopsToLord();

                // CRITICAL: Issue military equipment BEFORE bag check scheduling
                // This ensures bag check only processes pre-enlistment civilian gear,
                // not the military-issued T1 equipment
                var appliedGraceEquipment = TryApplyGraceEquipment(resumedFromGrace, graceTroopId);
                if (!appliedGraceEquipment)
                {
                    AssignInitialEquipment();
                    SetInitialFormation();
                }
                else
                {
                    ModLogger.Info("ENLISTMENT", "Grace enlistment equipment restored for new service");
                }

                // Issue initial food ration on new enlistment (T1-T6 only)
                // Grace period re-enlistment doesn't get a new ration (they already had one from previous service)
                // Commanders (T7+) don't receive issued rations
                if (!resumedFromGrace && _enlistmentTier < 7)
                {
                    try
                    {
                        IssueNewRation();
                        ModLogger.Info("Rations", "Issued initial food ration at enlistment start");
                    }
                    catch (Exception ex)
                    {
                        ModLogger.Caught("Rations", "Failed to issue initial ration at enlistment", ex);
                    }
                }

                if (resumedFromGrace)
                {
                    var message =
                        new TextObject(
                            "{=Enlisted_Message_RejoinedKingdom}You have rejoined {KINGDOM}. Your grace period has been cleared.");
                    var kingdomName = rejoiningKingdom?.Name ??
                                      new TextObject("{=Enlisted_Term_YourKingdom}your kingdom");
                    _ = message.SetTextVariable("KINGDOM", kingdomName);
                    InformationManager.DisplayMessage(new InformationMessage(message.ToString()));
                    ClearDesertionGracePeriod();
                }

                // CRITICAL: Finish any active PlayerEncounter before starting enlistment
                // This prevents unwanted encounters with the lord's party when we position the player
                // at the lord's location. If there's an active encounter, finishing it ensures clean state.
                if (PlayerEncounter.Current != null)
                {
                    try
                    {
                        // If inside a settlement, leave it first before finishing the encounter
                        // This ensures clean encounter state cleanup
                        if (PlayerEncounter.InsideSettlement)
                        {
                            PlayerEncounter.LeaveSettlement();
                        }

                        PlayerEncounter.Finish();
                        ModLogger.Debug("ENLISTMENT", "Finished active PlayerEncounter before starting enlistment");
                    }
                    catch (Exception ex)
                    {
                        ModLogger.Surfaced("ENLISTMENT", "Error finishing PlayerEncounter before enlistment", ex);
                    }
                }

                // Attach the player's party to the lord's party using natural attachment system
                // This ensures the player follows the lord during travel and integrates with armies
                // IMPORTANT: Do this AFTER finishing any active encounters to avoid creating
                // an encounter with the lord's party itself
                // Escort into lord's party when nearby; clan expenses now native
                EncounterGuard.TryAttachOrEscort(lord);

                // Configure party state to prevent random encounters while allowing battle participation
                var main = MobileParty.MainParty;
                var lordParty = lord.PartyBelongedTo;
                if (main != null)
                {
                    // Make the player party invisible on the map (they're part of the lord's force now)
                    main.IsVisible = false;

                    // Also hide the 3D visual entity (separate from nameplate VM)
                    EncounterGuard.HidePlayerPartyVisual();

                    // CRITICAL: Keep party ACTIVE so escort AI works for following
                    // Use IgnoreByOtherPartiesTill to prevent random encounters instead of IsActive = false
                    // This allows SetMoveEscortParty to function properly
                    main.IsActive = true;
                    main.IgnoreByOtherPartiesTill(CampaignTime.Now +
                                                  CampaignTime.Days(365f)); // Ignore all parties for 1 year

                    // Set escort AI to follow the lord - this only works when party is active
                    if (lordParty != null)
                    {
                        main.SetMoveEscortParty(lordParty, MobileParty.NavigationType.Default, false);
                        lordParty.Party.SetAsCameraFollowParty();
                        ModLogger.Debug("ENLISTMENT", $"Set escort AI to follow {lord.Name}");
                    }

                    // Enable battle participation so the player joins battles when the lord fights
                    TrySetShouldJoinPlayerBattles(main, true);
                }


                ModLogger.Info("ENLISTMENT",
                    $"Successfully enlisted with {lord.Name} - Tier {_enlistmentTier}, XP: {_enlistmentXP}, Kingdom: {lord.MapFaction?.Name?.ToString() ?? "Independent"}, Culture: {lord.Culture?.StringId ?? "unknown"}");
                ModLogger.Info("ENLISTMENT",
                    $"Enlistment date: {_enlistmentDate}, Grace resume: {resumedFromGrace}");

                // Fire event for other mods
                OnEnlisted?.Invoke(lord);

                // Persistent Lance Leaders: connect/create the current leader now that we are enlisted.

                // Trigger the deferred bag-check as soon as enlistment finishes (best-effort).
                // The bag-check processes whatever is currently in the player's inventory at that moment.
                // If the player is in a vulnerable state (battle/encounter/prisoner), this will simply no-op
                // and the hourly tick will retry later.
                try
                {
                    ProcessDeferredBagCheck(force: true);
                }
                catch (Exception ex)
                {
                    ModLogger.Surfaced("ENLISTMENT", "Error triggering post-enlist bag check", ex);
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("ENLISTMENT",
                    $"Failed to start enlistment with {lord?.Name?.ToString() ?? "null"} - {ex.Message}", ex);

                // CRITICAL: Reset enlistment state to prevent partial enlistment
                // Without this, IsEnlisted would return true and hide the enlistment dialog
                _enlistedLord = null;
                _enlistmentTier = 1;
                _enlistmentXP = 0;
                _enlistmentDate = CampaignTime.Zero;
                _isOnLeave = false;

                // Deactivate enlisted mode
                SyncActivationState("enlistment_failed");

                // Restore party visibility
                var main = MobileParty.MainParty;
                if (main != null)
                {
                    main.IsVisible = true;
                    main.IsActive = true;
                }

                ModLogger.Info("ENLISTMENT", "Enlistment state reset after failure - dialog should be available again");
            }
        }

        // Obsolete stub methods kept for save compatibility with older event handlers

        /// <summary>
        ///     Discharge the player from enlistment. By default, returns the player to independent status.
        ///     Exception: If honorably discharged after completing the configured full service period,
        ///     the player is returned to their original kingdom.
        /// </summary>
        /// <param name="reason">Reason for discharge (for logging).</param>
        /// <param name="isHonorableDischarge">Whether this is an honorable discharge (e.g., lord died). Defaults to false.</param>
        /// <param name="retainKingdomDuringGrace">If true, keeps player in kingdom during grace period. Defaults to false.</param>
        public void StopEnlist(string reason, bool isHonorableDischarge = false, bool retainKingdomDuringGrace = false)
        {
            try
            {
                ModLogger.Info("ENLISTMENT", $"Service ended: {reason} (Honorable: {isHonorableDischarge})");

                try
                {
                    OnEnlistmentEnded?.Invoke(reason, isHonorableDischarge);
                }
                catch (Exception ex)
                {
                    ModLogger.Caught("ENLISTMENT", "OnEnlistmentEnded listener threw", ex);
                }

                // Save current reputation to faction service record before clearing state.
                // This allows partial restoration when the player re-enlists with the same faction.
                // Restoration percentages are applied based on discharge band in TryConsumeReservistForFaction.
                if (_enlistedLord?.MapFaction != null && ServiceRecordManager.Instance != null)
                {
                    try
                    {
                        // Officer/Soldier reputation removed - service record no longer stores these
                    }
                    catch (Exception ex)
                    {
                        ModLogger.Caught("ServiceRecord", "Failed to save reputation snapshot", ex);
                    }
                }

                // Reclaim all issued rations on service end (discharge, desertion, lord death)
                try
                {
                    ReclaimIssuedRations();
                }
                catch (Exception ex)
                {
                    ModLogger.Caught("Rations", "Failed to reclaim rations on service end", ex);
                }

                // Persistent Lance Leaders: record discharge memory and clear cached leader reference.
                var retirementConfig = EnlistedConfig.LoadRetirementConfig();
                var firstTermDays = retirementConfig?.FirstTermDays > 0 ? retirementConfig.FirstTermDays : 252;

                // Set guard flag to prevent OnClanChangedKingdom from treating the upcoming
                // kingdom removal as desertion. This is an intentional discharge, not abandonment.
                _isProcessingDischarge = true;
                _bagCheckInProgress = false;
                // Allow re-prompt on next enlistment if this was retirement or desertion
                var reasonLower = reason?.ToLowerInvariant() ?? string.Empty;
                if (isHonorableDischarge || reasonLower.Contains("desert"))
                {
                    _bagCheckCompleted = false;
                    _bagCheckEverCompleted = false;
                }
                // Otherwise keep the completed flags so normal re-enlists skip the prompt

                // Clear reserve state immediately when enlistment ends for ANY reason
                // This prevents player getting stuck invisible if lord's party disbands while in reserve
                if (EnlistedEncounterBehavior.IsWaitingInReserve)
                {
                    ModLogger.Info("BATTLE", "Clearing reserve state during service end");
                    EnlistedEncounterBehavior.ClearReserveState();
                }

                var main = MobileParty.MainParty;
                var playerInBattleState = false;

                // CRITICAL: Clean up all battle-related state BEFORE finishing encounters or making party active
                // This prevents crashes when the player is still in a MapEvent or Army after army defeat
                if (main != null)
                {
                    // Remove from army if still in one (army might be dispersed but party still references it)
                    if (main.Army != null)
                    {
                        try
                        {
                            ModLogger.Info("ENLISTMENT",
                                $"Removing player from army before ending service (Army: {main.Army.LeaderParty?.LeaderHero?.Name})");
                            // Clear army reference - the army may already be dispersed
                            main.Army = null;
                            // Note: We can't call RemovePartyFromMergedParties on a dispersed army, so just clear the reference
                        }
                        catch (Exception ex)
                        {
                            ModLogger.Surfaced("ENLISTMENT", "Error removing player from army", ex);
                            // Force clear army reference even if removal fails
                            main.Army = null;
                        }
                    }

                    // Release attachment to lord's party BEFORE finishing encounters
                    // CRITICAL: Clear attachment (clearAttachment = true) to prevent player from being
                    // considered part of defeated force, avoiding immediate "Attack or Surrender" encounters
                    TryReleaseEscort(main, true);

                    // Disable battle joining BEFORE state changes
                    TrySetShouldJoinPlayerBattles(main, false);

                    // CRITICAL: Check if player is still in a MapEvent, PlayerEncounter, or is a prisoner.
                    // If the player is in any of these states, we must NOT activate to prevent crashes.
                    // This is especially important when the lord is captured/defeated while the player
                    // is in a surrender menu - activating while in an encounter causes assertion failures.
                    // Also check prisoner state because native PlayerCaptivity deactivates the party;
                    // reactivating it here would fight with the native captivity system.
                    bool playerInMapEvent = main.Party.MapEvent != null;
                    bool playerInEncounter = PlayerEncounter.Current != null;
                    bool playerIsPrisoner = Hero.MainHero?.IsPrisoner == true;
                    playerInBattleState = playerInMapEvent || playerInEncounter || playerIsPrisoner;

                    if (playerInBattleState)
                    {
                        ModLogger.Info("ENLISTMENT",
                            $"Player in vulnerable state when ending service (MapEvent: {playerInMapEvent}, Encounter: {playerInEncounter}, Prisoner: {playerIsPrisoner}) - deferring activation");
                        // CRITICAL: Deactivate party to prevent them from becoming "attackable"
                        // while still in battle/encounter state - this prevents crashes when clicking "Surrender"
                        main.IsActive = false;
                        main.IsVisible = false;
                        // Don't finish PlayerEncounter or MapEvent yet - wait for them to end naturally
                        // The OnMapEventEnded handler will handle cleanup when battle ends
                        // This prevents crashes when clicking "Surrender" while in invalid state
                        SchedulePostEncounterVisibilityRestore();
                    }
                    else
                    {
                        // No active MapEvent or PlayerEncounter - safe to activate
                        // Restore normal party state now that service is ending
                        main.IsVisible = true; // Make the party visible on the map again
                        main.IsActive = true; // Re-enable party activity to allow normal encounters

                        // Also show the 3D visual entity (separate from nameplate VM)
                        EncounterGuard.ShowPlayerPartyVisual();

                        // CRITICAL: Reset camera to follow player's party instead of lord's party
                        // Without this, the camera remains stuck following the lord after discharge,
                        // causing movement softlock where player can't control their character
                        try
                        {
                            main.Party.SetAsCameraFollowParty();
                        }
                        catch (Exception cameraEx)
                        {
                            ModLogger.Caught("ENLISTMENT", "Failed to reset camera to player party", cameraEx);
                        }

                        ModLogger.Info("ENLISTMENT", "Party activated and made visible (no active battle state)");
                    }

                    // NAVAL FIX: Handle sea stranding - if player is at sea without naval capability, teleport to nearest port
                    // This can happen when the army disbands at sea and service ends (e.g., lord captured at sea, lord dies at sea)
                    // Without this fix, the player becomes permanently stranded on the water
                    // EXCEPTION: If player is a PRISONER, do NOT teleport - let native's player_raft_state_after_prisoner
                    // menu handle stranding after release. Teleporting while prisoner causes confusing "washed ashore"
                    // notifications while still in captivity.
                    if (main.IsCurrentlyAtSea && !main.HasNavalNavigationCapability && !playerIsPrisoner)
                    {
                        ModLogger.Info("Naval",
                            "Player stranded at sea after service ended - teleporting to nearest port");
                        TryTeleportToNearestPort(main);
                    }
                    else if (main.IsCurrentlyAtSea && !main.HasNavalNavigationCapability && playerIsPrisoner)
                    {
                        ModLogger.Info("Naval",
                            "Player stranded at sea but is prisoner - letting native handle stranding after release");
                    }
                }

                // Return any stowed belongings from the baggage train stash so they are not stranded behind the camp UI.
                ReturnBaggageStashToPlayerInventory();

                // Restore player's ships to normal vulnerability after full discharge
                // During grace period, ships remain protected in case player re-enlists
                if (!retainKingdomDuringGrace)
                {
                    RestorePlayerShipsVulnerability();
                }
                else
                {
                    ModLogger.Debug("Naval", "Keeping ships protected during grace period");
                }

                // Restore companions and troops to the player's party
                // These were transferred to the lord's party when service started
                RestoreCompanionsToPlayer();

                // MINOR FACTION CLEANUP: Restore war relations that were mirrored when enlisting with a minor faction
                // During grace period, keep war relations in case player re-enlists with another lord in same faction
                if (!retainKingdomDuringGrace)
                {
                    RestoreMinorFactionWarRelations();
                }
                else
                {
                    ModLogger.Debug("FactionRelations", "Keeping mirrored war relations during grace period");
                }

                // Determine if player completed full enlistment period (configurable Bannerlord years)
                // This is required for honorable discharge to restore original kingdom
                var completedFullService = false;
                var daysServedInt = 0;
                if (_enlistmentDate != CampaignTime.Zero)
                {
                    var serviceDuration = CampaignTime.Now - _enlistmentDate;
                    var fullServicePeriod = CampaignTime.Days(firstTermDays);
                    completedFullService = serviceDuration >= fullServicePeriod;

                    var daysServed = serviceDuration.ToDays;
                    daysServedInt = (int)Math.Floor(daysServed);
                    ModLogger.Info("ENLISTMENT",
                        $"Service duration: {daysServed:F1} days (Full period: {firstTermDays} days, Completed: {completedFullService})");
                }

                // Pension determination happens on discharge (honorable only)
                ApplyPensionOnDischarge(isHonorableDischarge, daysServedInt);

                // Determine target kingdom based on discharge type
                // Default: Return to independent status
                // Exception: Honorably discharged after full service → restore original kingdom
                var playerClan = Clan.PlayerClan;
                if (playerClan != null)
                {
                    try
                    {
                        Kingdom targetKingdom = null;
                        var keepCurrentKingdom = false;

                        // Check if this qualifies for restoration to original kingdom
                        // Must be: honorable discharge AND completed full service period
                        if (isHonorableDischarge && completedFullService && _originalKingdom != null)
                        {
                            // Honorably discharged after full service - restore to original kingdom
                            if (playerClan.Kingdom != _originalKingdom)
                            {
                                targetKingdom = _originalKingdom;
                                ModLogger.Info("ENLISTMENT",
                                    $"Honorable discharge after full service - restoring to {_originalKingdom.Name} without penalties");
                            }
                            else
                            {
                                ModLogger.Debug("ENLISTMENT", "Honorable discharge - already in original kingdom");
                            }
                        }
                        else
                        {
                            // Default: Return to independent status (leave current kingdom if in one)
                            if (playerClan.Kingdom != null)
                            {
                                if (retainKingdomDuringGrace)
                                {
                                    keepCurrentKingdom = true;
                                    ModLogger.Info("ENLISTMENT",
                                        $"Grace discharge - retaining membership in {playerClan.Kingdom.Name}");
                                }
                                else
                                {
                                    targetKingdom = null; // Independent
                                    if (isHonorableDischarge && !completedFullService)
                                    {
                                        ModLogger.Info("ENLISTMENT",
                                            "Honorable discharge but incomplete service - returning to independent status");
                                    }
                                    else
                                    {
                                        ModLogger.Info("ENLISTMENT", "Discharge - returning to independent status");
                                    }
                                }
                            }
                            else
                            {
                                ModLogger.Debug("ENLISTMENT", "Already independent - no kingdom change needed");
                            }
                        }

                        // Use helper method to restore kingdom without penalties
                        if (keepCurrentKingdom)
                        {
                            ModLogger.Debug("ENLISTMENT", "Skipped kingdom change due to active grace period");
                        }
                        else if (targetKingdom == null && playerClan.Kingdom != null)
                        {
                            // Need to leave kingdom to become independent
                            DischargeHelper.RestoreKingdomWithoutPenalties(playerClan, null);
                        }
                        else if (targetKingdom != null && playerClan.Kingdom != targetKingdom)
                        {
                            // Need to join original kingdom (honorable discharge after full service)
                            DischargeHelper.RestoreKingdomWithoutPenalties(playerClan, targetKingdom);
                        }
                    }
                    catch (Exception ex)
                    {
                        ModLogger.Surfaced("ENLISTMENT", "Error restoring kingdom during discharge", ex, ctx: LogCtx.PlayerState());
                    }
                }

                // Clear kingdom state tracking
                _originalKingdom = null;
                _wasIndependentClan = false;
                if (!retainKingdomDuringGrace)
                {
                    _graceProtectionEnds = CampaignTime.Zero;
                }

                // If retaining kingdom for grace period, save progression state BEFORE clearing it
                // This preserves tier/XP so player can resume at their previous rank when re-enlisting
                // CRITICAL: Must happen before _enlistmentTier and _enlistmentXP are reset below
                // BUG FIX: Removed _enlistedLord != null check - lord may be dead but we still need
                // to save progression state so player can resume at their previous rank
                if (retainKingdomDuringGrace)
                {
                    _savedGraceTier = _enlistmentTier;
                    _savedGraceXP = _enlistmentXP;
                    _savedGraceTroopId = TroopSelectionManager.Instance?.LastSelectedTroopId;
                    _savedGraceEnlistmentDate = _enlistmentDate;
                    ModLogger.Info("ENLISTMENT",
                        $"Saved grace progression state: Tier={_savedGraceTier}, XP={_savedGraceXP}, Lord={((_enlistedLord?.Name?.ToString()) ?? "deceased")}");
                }

                // Clear enlistment state
                if (_enlistedLord != null)
                {
                    if (retainKingdomDuringGrace)
                    {
                        _savedGraceLord = _enlistedLord;
                        // BUG FIX: Only register living lords with the visual tracker
                        // Dead heroes cause crashes when the tracker tries to display them
                        if (_enlistedLord.IsAlive)
                        {
                            Campaign.Current.VisualTrackerManager.RegisterObject(_enlistedLord);
                            var trackerMsg =
                                new TextObject("{=Enlisted_Message_LordTracked}Your lord has been marked on the map.");
                            InformationManager.DisplayMessage(new InformationMessage(trackerMsg.ToString()));
                            ModLogger.Debug("ENLISTMENT", "Added map tracker for grace period");
                        }
                        else
                        {
                            ModLogger.Debug("ENLISTMENT", "Skipped map tracker - lord is deceased");
                        }
                    }
                    else
                    {
                        Campaign.Current.VisualTrackerManager.RemoveTrackedObject(_enlistedLord);
                    }
                }

                // Clean up Quartermaster hero reference
                CleanupQuartermasterOnServiceEnd();

                // Clear food quality bonus
                ClearFoodQuality();

                // Clear retinue provisioning
                ClearRetinueProvisioning();

                // Handle retinue soldiers on service end (EC2)
                HandleRetinueOnServiceEnd(isHonorableDischarge);

                // Shutdown supply simulation
                CompanySupplyManager.Shutdown();


                _enlistedLord = null;
                _enlistmentTier = 1;
                _enlistmentXP = 0;
                _enlistmentDate = CampaignTime.Zero;
                _disbandArmyAfterBattle = false; // Clear any pending army operations
                ClearPayState("service_ended");
                ClearNcoAndSoldierNames();
                if (!playerInBattleState)
                {
                    ForceFinishLingeringEncounter("StopEnlist");
                }
                else
                {
                    ModLogger.Debug("EncounterCleanup",
                        "Skipping encounter force-finish because player is still in battle state");
                }

                // Log discharge for diagnostics
                SessionDiagnostics.LogStateTransition("Enlistment", "Enlisted", "Civilian",
                    $"Reason: {reason}, Honorable: {isHonorableDischarge}");

                // Fire event for other mods
                OnDischarged?.Invoke(reason);

                SyncActivationState("stop_enlist");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("ENLISTMENT", "Error ending service", ex);
                // Ensure critical state is cleared even if restoration fails
                _enlistedLord = null;
                _disbandArmyAfterBattle = false;
            }
            finally
            {
                // Always clear the discharge guard flag, even if an exception occurred.
                // This ensures OnClanChangedKingdom returns to normal behavior after discharge completes.
                _isProcessingDischarge = false;
            }
        }

        /// <summary>
        ///     Start desertion grace period when army is defeated or lord is captured.
        ///     Player has 14 days to rejoin another lord in the same kingdom before being branded a deserter.
        /// </summary>
        /// <param name="kingdom">Kingdom that the player needs to rejoin during grace period.</param>
        private void StartDesertionGracePeriod(Kingdom kingdom)
        {
            try
            {
                if (kingdom == null)
                {
                    ModLogger.Expected("Desertion", "desert_grace_kingdom_null",
                        "Cannot start grace period - kingdom is null");
                    return;
                }

                _pendingDesertionKingdom = kingdom;
                var graceDays = EnlistedConfig.LoadGameplayConfig().DesertionGracePeriodDays;
                _desertionGracePeriodEnd = CampaignTime.Now + CampaignTime.Days(graceDays);

                // Only save progression state if not already saved by StopEnlist()
                // StopEnlist saves these values when retainKingdomDuringGrace=true, before clearing
                // If _savedGraceTier is already > 0, the values were pre-saved and shouldn't be overwritten
                if (_savedGraceTier <= 0)
                {
                    _savedGraceTier = _enlistmentTier;
                    _savedGraceXP = _enlistmentXP;
                    _savedGraceTroopId = TroopSelectionManager.Instance?.LastSelectedTroopId;
                    _savedGraceEnlistmentDate = _enlistmentDate;
                    ModLogger.Debug("Desertion",
                        $"Saved grace state in StartDesertionGracePeriod: Tier={_savedGraceTier}, XP={_savedGraceXP}");
                }
                else
                {
                    ModLogger.Debug("Desertion",
                        $"Using pre-saved grace state: Tier={_savedGraceTier}, XP={_savedGraceXP}");
                }

                // BUG FIX: Also record reservist data when starting grace period
                // This ensures player can resume service later even if grace period expires
                // Uses "grace" band to give better treatment than deserter on re-enlistment
                var daysServed = _enlistmentDate != CampaignTime.Zero
                    ? (int)(CampaignTime.Now - _enlistmentDate).ToDays
                    : 0;
                ServiceRecordManager.Instance?.RecordReservist(
                    "grace", // New band type for grace period exits
                    daysServed,
                    _savedGraceTier > 0 ? _savedGraceTier : _enlistmentTier,
                    _savedGraceXP > 0 ? _savedGraceXP : _enlistmentXP,
                    _savedGraceLord ?? _enlistedLord);

                ModLogger.Info("Desertion", $"Started {graceDays}-day grace period to rejoin {kingdom.Name}");

                var message =
                    new TextObject(
                        "{=Enlisted_Message_ServiceEndedGrace}Your service has ended. You have {DAYS} days to find another lord in {KINGDOM} before you are branded a deserter.");
                _ = message.SetTextVariable("DAYS", graceDays);
                _ = message.SetTextVariable("KINGDOM", kingdom.Name);
                InformationManager.DisplayMessage(new InformationMessage(message.ToString()));
                EnsureGraceLordMarker(forceRefresh: true, "start_grace_period");

                // Fire event for other mods
                OnGracePeriodStarted?.Invoke();

                SyncActivationState("start_grace_period");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Desertion", "Error starting grace period", ex);
            }
        }

        /// <summary>
        ///     Apply desertion penalties when grace period expires and player hasn't rejoined.
        ///     Applies -50 relation with all lords in kingdom, +50 crime rating, and removes player from kingdom.
        /// </summary>
        private void ApplyDesertionPenalties()
        {
            try
            {
                if (_pendingDesertionKingdom == null)
                {
                    ModLogger.Debug("Desertion", "No pending desertion kingdom - clearing grace period");
                    _desertionGracePeriodEnd = CampaignTime.Zero;
                    return;
                }

                ModLogger.Info("Desertion",
                    $"Grace period expired - applying desertion penalties for {_pendingDesertionKingdom.Name}");

                var playerClan = Clan.PlayerClan;
                if (playerClan == null)
                {
                    ModLogger.Expected("Desertion", "desert_penalties_clan_null",
                        "Cannot apply penalties - player clan is null");
                    ClearDesertionGracePeriod();
                    return;
                }

                // Check if kingdom still exists (edge case: kingdom destroyed during grace period)
                if (_pendingDesertionKingdom.IsEliminated)
                {
                    ModLogger.Info("Desertion",
                        $"Kingdom {_pendingDesertionKingdom.Name} was destroyed - clearing grace period without penalties");
                    ClearDesertionGracePeriod();
                    return;
                }

                // Check if player already left kingdom (edge case: manual kingdom leave)
                if (playerClan.Kingdom != _pendingDesertionKingdom)
                {
                    ModLogger.Info("Desertion",
                        "Player already left kingdom - clearing grace period without penalties");
                    ClearDesertionGracePeriod();
                    return;
                }

                // -50 relation with all lords in kingdom
                var lordsPenalized = 0;
                foreach (Clan clan in _pendingDesertionKingdom.Clans)
                {
                    if (clan.Leader != null && clan.Leader != Hero.MainHero && clan.Leader.IsAlive)
                    {
                        ChangeRelationAction.ApplyPlayerRelation(clan.Leader, -50);
                        lordsPenalized++;
                    }
                }

                // Apply moderate crime rating (50 points = moderate)
                ChangeCrimeRatingAction.Apply(_pendingDesertionKingdom, 50f);

                // Remove from kingdom (become independent)
                if (playerClan.Kingdom == _pendingDesertionKingdom)
                {
                    ChangeKingdomAction.ApplyByLeaveKingdom(playerClan);
                }

                // Display notification
                var message =
                    new TextObject(
                        "{=Enlisted_Message_BrandedDeserter}You have been branded a deserter. Your relationship with {KINGDOM} has suffered.");
                _ = message.SetTextVariable("KINGDOM", _pendingDesertionKingdom.Name);
                InformationManager.DisplayMessage(new InformationMessage(message.ToString()));

                ModLogger.Info("Desertion",
                    $"Applied desertion penalties: -50 relation with {lordsPenalized} lords, +50 crime rating, removed from kingdom");

                // Clear grace period state
                ClearDesertionGracePeriod();
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Desertion", "Error applying desertion penalties", ex);
                ClearDesertionGracePeriod(); // Always clear state on error
            }
        }

        private void EnsureGraceLordMarker(bool forceRefresh, string reason)
        {
            try
            {
                var mapMarkerManager = Campaign.Current?.MapMarkerManager;
                if (mapMarkerManager == null)
                {
                    return;
                }

                var markerScope = GraceLordMarkerRefreshPolicy.GetScope(_isOnLeave, IsInDesertionGracePeriod);
                var trackedLord = markerScope == LordMarkerScope.Leave ? _enlistedLord : _savedGraceLord;
                if (markerScope == LordMarkerScope.None || trackedLord == null || !trackedLord.IsAlive)
                {
                    RemoveGraceLordMarker();
                    return;
                }

                if (!TryBuildGraceLordMarkerData(trackedLord, out var snapshot, out var position, out var banner,
                        out var label))
                {
                    RemoveGraceLordMarker();
                    return;
                }

                if (!forceRefresh &&
                    !GraceLordMarkerRefreshPolicy.ShouldRefresh(_lastGraceLordMarkerSnapshot, snapshot,
                        GraceLordMarkerDistanceThreshold))
                {
                    return;
                }

                mapMarkerManager.RemoveAllMapMarkersByQuestId(GraceLordMarkerQuestId);
                _ = mapMarkerManager.CreateMapMarker(banner, label, position, true, GraceLordMarkerQuestId);
                _lastGraceLordMarkerSnapshot = snapshot;
                ModLogger.Info("Tracker", $"Updated grace lord marker ({reason})");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("TRACKER", "Failed to update grace lord marker", ex);
            }
        }

        private void RemoveGraceLordMarker()
        {
            try
            {
                Campaign.Current?.MapMarkerManager?.RemoveAllMapMarkersByQuestId(GraceLordMarkerQuestId);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("TRACKER", "Failed to remove grace lord marker", ex);
            }
            finally
            {
                _lastGraceLordMarkerSnapshot = null;
            }
        }

        private static bool TryBuildGraceLordMarkerData(
            Hero lord,
            out GraceLordMarkerSnapshot snapshot,
            out Vec3 position,
            out Banner banner,
            out TextObject label)
        {
            snapshot = default;
            position = Vec3.Zero;
            banner = null;
            label = null;

            if (lord == null || !lord.IsAlive)
            {
                return false;
            }

            var mapPoint = lord.GetMapPoint();
            if (mapPoint == null)
            {
                return false;
            }

            var campaignPosition = mapPoint.Position;
            if (!campaignPosition.IsValid())
            {
                return false;
            }

            var anchorKey = GetGraceLordMarkerAnchorKey(lord, mapPoint);
            banner = lord.ClanBanner;
            if (string.IsNullOrWhiteSpace(anchorKey) || banner == null)
            {
                return false;
            }

            snapshot = new GraceLordMarkerSnapshot(anchorKey, campaignPosition.X, campaignPosition.Y);
            position = campaignPosition.AsVec3();
            label = new TextObject("{=Enlisted_GraceLordMarker}{LORD_NAME}'s command");
            _ = label.SetTextVariable("LORD_NAME", lord.Name);
            return true;
        }

        private static string GetGraceLordMarkerAnchorKey(Hero lord, IMapPoint mapPoint)
        {
            if (mapPoint is Settlement settlement)
            {
                return $"settlement:{settlement.StringId}";
            }

            if (mapPoint is MobileParty mobileParty)
            {
                return $"party:{mobileParty.StringId}";
            }

            return $"hero:{lord.StringId}";
        }

        /// <summary>
        ///     Clear desertion grace period state.
        ///     Called when grace period expires (after penalties) or when player rejoins.
        /// </summary>
        private void ClearDesertionGracePeriod()
        {
            _pendingDesertionKingdom = null;
            _desertionGracePeriodEnd = CampaignTime.Zero;
            RemoveGraceLordMarker();

            // BUG FIX: Only unregister living lords from the visual tracker
            // Dead heroes may cause crashes when the tracker tries to access them
            if (_savedGraceLord != null && _savedGraceLord.IsAlive)
            {
                try
                {
                    Campaign.Current.VisualTrackerManager.RemoveTrackedObject(_savedGraceLord);
                }
                catch (Exception ex)
                {
                    ModLogger.Caught("ENLISTMENT", "Failed to remove grace lord from tracker", ex);
                }
            }

            _savedGraceLord = null;

            _savedGraceTier = -1;
            _savedGraceXP = 0;
            _savedGraceTroopId = null;
            _savedGraceEnlistmentDate = CampaignTime.Zero;
            _graceProtectionEnds = CampaignTime.Zero;

            SyncActivationState("clear_grace_period");
        }

        /// <summary>
        ///     Checks if the player is blocked from re-enlisting with a minor faction due to desertion.
        /// </summary>
        /// <param name="faction">The minor faction to check.</param>
        /// <param name="remainingDays">Output: days remaining in cooldown (0 if not blocked).</param>
        /// <returns>True if blocked, false if can enlist.</returns>
        public bool IsBlockedFromMinorFaction(IFaction faction, out int remainingDays)
        {
            remainingDays = 0;

            if (faction == null || _minorFactionDesertionCooldowns == null)
            {
                return false;
            }

            if (_minorFactionDesertionCooldowns.TryGetValue(faction.StringId, out var cooldownEnd))
            {
                if (CampaignTime.Now < cooldownEnd)
                {
                    remainingDays = (int)(cooldownEnd - CampaignTime.Now).ToDays;
                    return true;
                }
                else
                {
                    // Cooldown expired, remove it
                    _ = _minorFactionDesertionCooldowns.Remove(faction.StringId);
                }
            }

            return false;
        }

        /// <summary>
        ///     Records that the player aborted enlistment during bag check with this lord.
        ///     Lord will not accept the player for 7 days.
        /// </summary>
        public void RecordBagCheckAbort(Hero lord)
        {
            if (lord == null)
            {
                return;
            }

            if (_bagCheckAbortCooldowns == null)
            {
                _bagCheckAbortCooldowns = new Dictionary<string, CampaignTime>();
            }

            var cooldownEnd = CampaignTime.Now + CampaignTime.Days(7);
            _bagCheckAbortCooldowns[lord.StringId] = cooldownEnd;

            ModLogger.Info("ENLISTMENT", $"Recorded bag check abort with {lord.Name}. Cooldown until {cooldownEnd}");
        }

        /// <summary>
        ///     Checks if the player is blocked from enlisting with a lord due to aborting the bag check recently.
        /// </summary>
        /// <param name="lord">The lord to check.</param>
        /// <param name="remainingDays">Output: days remaining in cooldown (0 if not blocked).</param>
        /// <returns>True if blocked, false if can enlist.</returns>
        public bool IsBlockedFromLordDueToAbort(Hero lord, out int remainingDays)
        {
            remainingDays = 0;

            if (lord == null || _bagCheckAbortCooldowns == null)
            {
                return false;
            }

            if (_bagCheckAbortCooldowns.TryGetValue(lord.StringId, out var cooldownEnd))
            {
                if (CampaignTime.Now < cooldownEnd)
                {
                    remainingDays = (int)Math.Ceiling((cooldownEnd - CampaignTime.Now).ToDays);
                    return true;
                }
                else
                {
                    // Cooldown expired, remove it
                    _ = _bagCheckAbortCooldowns.Remove(lord.StringId);
                }
            }

            return false;
        }

        /// <summary>
        ///     Voluntarily desert from the army. The player keeps their current equipment but
        ///     receives desertion penalties: -50 relation with all lords in the kingdom and
        ///     +50 crime rating. After deserting, the player is free to enlist with other factions.
        /// </summary>
        private bool TryApplyGraceEquipment(bool resumedFromGrace, string preferredTroopId)
        {
            if (!resumedFromGrace || _enlistedLord?.Culture == null)
            {
                return false;
            }

            var manager = TroopSelectionManager.Instance;
            if (manager == null)
            {
                ModLogger.Expected("ENLISTMENT", "enlist_grace_no_troop_manager",
                    "TroopSelectionManager unavailable - cannot restore saved equipment");
                return false;
            }

            CharacterObject selectedTroop = null;
            if (!string.IsNullOrEmpty(preferredTroopId))
            {
                try
                {
                    selectedTroop = MBObjectManager.Instance.GetObject<CharacterObject>(preferredTroopId);
                    if (selectedTroop != null && selectedTroop.Culture != _enlistedLord.Culture)
                    {
                        selectedTroop = null;
                    }
                }
                catch
                {
                    selectedTroop = null;
                }
            }

            if (selectedTroop == null)
            {
                var unlocked = manager.GetUnlockedTroopsForCurrentTier(_enlistedLord.Culture.StringId, _enlistmentTier);
                selectedTroop = unlocked?.FirstOrDefault();
            }

            if (selectedTroop == null)
            {
                ModLogger.Info("ENLISTMENT", "Grace enlistment could not find matching troop; using default kit");
                return false;
            }

            manager.ApplySelectedTroopEquipment(Hero.MainHero, selectedTroop, autoIssueEquipment: true);
            return true;
        }

        /// <summary>
        ///     Hourly tick handler that runs once per in-game hour while the player is enlisted.
        ///     Maintains party following, visibility, and encounter prevention state.
        ///     Called automatically by the game every hour.
        /// </summary>
        private void OnHourlyTick()
        {
            // Check if grace period expired (even if not enlisted)
            // BUG FIX: Previous check used IsInDesertionGracePeriod which requires CampaignTime.Now < _desertionGracePeriodEnd
            // This was contradictory with the >= check, meaning expiration NEVER triggered.
            // Fixed: Check if grace period WAS active (kingdom set, deadline set) but has now expired
            if (_pendingDesertionKingdom != null &&
                _desertionGracePeriodEnd != CampaignTime.Zero &&
                CampaignTime.Now >= _desertionGracePeriodEnd)
            {
                ModLogger.Info("Desertion", $"Grace period expired - {(CampaignTime.Now - _desertionGracePeriodEnd).ToDays:F1} days overdue");
                ApplyDesertionPenalties();
                return;
            }

            ProcessDeferredBagCheck();
            ProcessCourierArrival();

            // Process hourly supply resupply when in settlements.
            // This allows partial-day settlement visits to provide meaningful supply gains.
            CompanySupplyManager.Instance?.HourlyUpdate();

            SyncActivationState("hourly_tick");
            if (!EnlistedActivation.IsActive)
            {
                return;
            }

            var main = MobileParty.MainParty;
            if (main == null)
            {
                return;
            }

            // CRITICAL: Enforce map tracker persistence
            // VisualTrackerManager can sometimes lose tracking state (e.g. after load or party changes)
            // We re-apply it here to ensure the user can always find their lord during leave/grace
            if (_isOnLeave && _enlistedLord != null && _enlistedLord.IsAlive)
            {
                if (!Campaign.Current.VisualTrackerManager.CheckTracked(_enlistedLord))
                {
                    Campaign.Current.VisualTrackerManager.RegisterObject(_enlistedLord);
                    ModLogger.Info("Tracker", "Re-applied map tracker for on-leave lord (was missing)");
                }

                var lordParty = _enlistedLord.PartyBelongedTo;
                if (lordParty != null)
                {
                    // Backup: Ensure the party is physically visible
                    if (!lordParty.IsVisible)
                    {
                        lordParty.IsVisible = true;
                        ModLogger.Info("Tracker", "Forced Lord party to be visible (was hidden) - Hourly Check");
                    }
                }

                EnsureGraceLordMarker(forceRefresh: false, "hourly_tick");
            }
            else if (IsInDesertionGracePeriod && _savedGraceLord != null && _savedGraceLord.IsAlive)
            {
                if (!Campaign.Current.VisualTrackerManager.CheckTracked(_savedGraceLord))
                {
                    Campaign.Current.VisualTrackerManager.RegisterObject(_savedGraceLord);
                    ModLogger.Info("Tracker", "Re-applied map tracker for grace period lord (was missing)");
                }

                EnsureGraceLordMarker(forceRefresh: false, "hourly_tick");
            }
            else if (_lastGraceLordMarkerSnapshot.HasValue)
            {
                RemoveGraceLordMarker();
            }

            // Fallback: ensure non-enlisted players stay visible and active in case the realtime tick misses an edge
            // case. The hourly tick runs once per in-game hour, so it provides periodic enforcement.
            if (!IsEnlisted)
            {
                var partyState = main.Party;
                var inMapEvent = partyState?.MapEvent != null;
                var isPrisoner = Hero.MainHero?.IsPrisoner == true;

                // Only skip visibility enforcement while vanilla systems own the player state. We skip when the
                // player is actively in a MapEvent (battle) or is a prisoner (captivity system owns the player).
                // We do not skip just because PlayerEncounter.Current exists, since it can be stale after a battle.
                if (inMapEvent || isPrisoner)
                {
                    ModLogger.LogOnce(
                        "hourly_skip_vanilla_capture",
                        "Hourly",
                        $"Skipping visibility enforcement - vanilla owns player (MapEvent:{inMapEvent}, Prisoner:{isPrisoner})");
                    return;
                }

                // Enforce visibility and activity for non-enlisted players as a fallback. This catches cases where
                // the realtime tick might have missed something.
                bool needsActivation = !main.IsActive;
                bool needsVisibility = !main.IsVisible;

                if (needsActivation || needsVisibility)
                {
                    // Log when we fix visibility/activity so issues are easier to diagnose.
                    if (needsVisibility)
                    {
                        ModLogger.Info("Hourly",
                            "Enforcing visibility for non-enlisted player (was invisible - hourly fallback check)");
                    }

                    if (needsActivation)
                    {
                        ModLogger.Info("Hourly",
                            "Enforcing activity for non-enlisted player (was inactive - hourly fallback check)");
                    }

                    main.IsActive = true;
                    main.IsVisible = true;
                }

                return; // Skip enlistment logic for non-enlisted players.
            }

            // Check if the lord's party still exists
            var counterpartParty = _enlistedLord?.PartyBelongedTo;
            if (counterpartParty == null)
            {
                // The lord's party no longer exists
                // Check if the lord is still alive and in a faction (e.g., joined a garrison, changed kingdoms)
                // This prevents XP/Rank reset when the lord moves to a garrison or changes realms
                if (_enlistedLord != null && _enlistedLord.IsAlive && _enlistedLord.MapFaction is Kingdom lordKingdom)
                {
                    ModLogger.Info("ENLISTMENT",
                        $"Lord's party disbanded (Lord: {_enlistedLord.Name}, Kingdom: {lordKingdom.Name}) - starting grace period");

                    // If player was in reserve during disbandment, teleport to safety first
                    // This prevents spawning surrounded by enemies at the battle location
                    var wasInReserve = EnlistedEncounterBehavior.IsWaitingInReserve;
                    if (wasInReserve)
                    {
                        ModLogger.Info("BATTLE", "Player was in reserve when party disbanded - teleporting to safety");
                        EnlistedEncounterBehavior.ClearReserveState();

                        // Clear any lingering encounter state
                        if (PlayerEncounter.Current != null)
                        {
                            PlayerEncounter.Current.IsPlayerWaiting = false;
                            PlayerEncounter.LeaveEncounter = true;
                            PlayerEncounter.Finish();
                        }

                        // Apply protection and teleport
                        var mainParty = MobileParty.MainParty;
                        if (mainParty != null)
                        {
                            var protectionDuration = CampaignTime.Hours(12f);
                            mainParty.IgnoreByOtherPartiesTill(CampaignTime.Now + protectionDuration);
                            TryTeleportToSafety(mainParty);

                            // Reactivate and make the party visible after the safety teleport
                            mainParty.IsActive = true;
                            mainParty.IsVisible = true;
                            mainParty.SetMoveModeHold(); // stop any phantom movement after disband
                        }

                        // Normalize time control to a stoppable mode so reserve wait state does not leave unstoppable speed
                        if (Campaign.Current != null)
                        {
                            var normalized = QuartermasterManager.NormalizeToStoppable(
                                Campaign.Current.TimeControlMode);
                            Campaign.Current.TimeControlMode = normalized;
                        }

                        GameMenu.ExitToLast();
                    }

                    // Stop enlistment but retain state for grace period
                    // This saves the current tier/XP to _savedGraceTier/_savedGraceXP
                    StopEnlist("Party disbanded - awaiting transfer", retainKingdomDuringGrace: true);

                    // Start grace period for the lord's CURRENT kingdom
                    StartDesertionGracePeriod(lordKingdom);

                    // Notify player specifically about the disbanding
                    var message =
                        new TextObject(
                            "{=Enlisted_Message_PartyDisbanded}Your commander's party has disbanded. Locate them to resume service.");
                    InformationManager.DisplayMessage(new InformationMessage(message.ToString()));
                }
                else
                {
                    // The lord's party no longer exists - automatically end service
                    StopEnlist("Target party invalid");
                }

                return;
            }

            // Maintain party attachment to the lord's party using natural attachment system
            // This ensures the player continues to follow the lord during travel and integrates with armies
            // Escort into lord's party when nearby; clan expenses now native
            EncounterGuard.TryAttachOrEscort(_enlistedLord);

            // Keep the player party invisible on the map (they're part of the lord's force)
            main.IsVisible = false;

            // Ensure the player can join battles when the lord fights
            TrySetShouldJoinPlayerBattles(main, true);

            // Ignore other parties briefly to suppress stray encounters while enlisted
            main.IgnoreByOtherPartiesTill(CampaignTime.Now + CampaignTime.Hours(0.5f));
        }

        /// <summary>
        ///     Attaches the player's party to the lord's party for following.
        ///     Uses direct position matching to ensure the player follows the lord during travel.
        ///     Delegates to EncounterGuard.TryAttachOrEscort for the actual implementation.
        /// </summary>
        /// <param name="lord">The lord whose party the player should follow.</param>
        internal static void ApplyEscort(Hero lord)
        {
            // Use direct position matching instead of AI escort system
            // This avoids complications with the AI system and ensures reliable following
            ModLogger.Debug("Following", "ApplyEscort called - delegating to EncounterGuard.TryAttachOrEscort");
            EncounterGuard.TryAttachOrEscort(lord);
        }

        internal static void TryReleaseEscort(MobileParty main)
        {
            TryReleaseEscort(main, false);
        }

        /// <summary>
        ///     Releases escort AI behavior by clearing the escort target and setting the party to hold mode.
        /// </summary>
        /// <param name="main">The party to release escort for.</param>
        /// <param name="clearAttachment">
        ///     Legacy parameter - no longer used. We don't set AttachedTo
        ///     because it causes GetGenericStateMenu() to crash when player isn't in an army.
        /// </param>
        internal static void TryReleaseEscort(MobileParty main, bool clearAttachment)
        {
            try
            {
                _ = clearAttachment;
                // Set AI to hold mode to stop following behavior.
                //
                // IMPORTANT: Do NOT call SetMoveEscortParty(null, ...) to "clear" escort.
                // Native SetMoveEscortParty dereferences the target party immediately (mobileParty.Position),
                // so passing null will throw.
                // NOTE: We no longer clear AttachedTo because we never set it (causes crashes)
                // 1.3.4 API: SetMoveModeHold is on MobileParty directly
                main.SetMoveModeHold();
                ModLogger.Debug("Following", "Released escort - cleared escort target and set hold mode");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Following", "Error releasing escort", ex);
            }
        }

        /// <summary>
        ///     Sets the ShouldJoinPlayerBattles property on a party, using direct access first
        ///     and falling back to reflection if direct access fails.
        ///     This property controls whether the party automatically joins battles.
        ///     For enlisted players, this should be true so they can participate in battles.
        ///     The method handles both modern API (direct property) and older versions (reflection).
        /// </summary>
        /// <param name="party">The party to modify.</param>
        /// <param name="value">True to enable automatic battle joining, false to disable.</param>
        private static void TrySetShouldJoinPlayerBattles(MobileParty party, bool value)
        {
            try
            {
                // Try direct property access first (modern API in current Bannerlord versions)
                // Note: No debug logging here as this is called frequently during normal operation
                party.ShouldJoinPlayerBattles = value;
            }
            catch (Exception ex1)
            {
                try
                {
                    // Use reflection for game versions where the property is not directly accessible
                    var prop = party.GetType().GetProperty("ShouldJoinPlayerBattles",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (prop != null)
                    {
                        prop.SetValue(party, value);
                    }
                    else
                    {
                        ModLogger.Expected("BATTLE", "reflect_should_join_battles_missing", "ShouldJoinPlayerBattles property not found via reflection");
                    }
                }
                catch (Exception ex2)
                {
                    // Capture both failure stacks: direct set + reflection fallback.
                    var agg = new AggregateException(ex1, ex2);
                    ModLogger.Caught("BATTLE", "Failed to set ShouldJoinPlayerBattles (direct + reflection)", agg);
                }
            }
        }

        /// <summary>
        ///     Processes daily military service benefits: wages and XP progression.
        ///     Called once per in-game day while the player is enlisted.
        ///     Integrates with the duties system to provide additional wage multipliers.
        ///     Also checks for leave expiration and grace period expiration.
        /// </summary>
        private void OnDailyTick()
        {
            // Always run captivity + leave maintenance even when inactive so grace-period escapes
            // and leave expirations are processed for players who temporarily lost activation.
            CheckPlayerCaptivityDuration();
            CheckProbationExpiration();

            // Check for leave expiration (14 days max)
            // This runs even when "not enlisted" because IsOnLeave makes IsEnlisted return false
            CheckLeaveExpiration();

            // Pensions are paid daily while NOT enlisted (and paused while enlisted).
            // This must run even when EnlistedActivation is inactive.
            ProcessDailyPension();

            SyncActivationState("daily_tick");
            if (!EnlistedActivation.IsActive)
            {
                return;
            }

            if (!IsEnlisted || _enlistedLord?.IsAlive != true)
            {
                return;
            }

            // Pause accrual while on leave or captive
            if (_isOnLeave || Hero.MainHero?.IsPrisoner == true)
            {
                return;
            }

            if (_nextPayday == CampaignTime.Zero)
            {
                _nextPayday = ComputeNextPayday();
            }

            try
            {
                // Update company supply simulation (unified logistics including rations)
                CompanySupplyManager.Instance?.DailyUpdate();

                // Accrue daily wage into muster ledger (no clan finance involvement)
                var wage = CalculateDailyWage();
                if (wage > 0 && !_payMusterPending)
                {
                    _pendingMusterPay += wage;
                }

                // Schedule pay muster when due; gate to one active incident at a time
                if (!_payMusterPending && CampaignTime.Now >= _nextPayday)
                {
                    _payMusterPending = true;
                    ModLogger.Info("Gold", $"Pay muster queued. Pending={_pendingMusterPay}, NextPayday={_nextPayday}");

                    // Use feature flag to switch between new menu system and legacy popup
                    if (ModConfig.Settings?.UseNewMusterMenu == true)
                    {
                        MusterMenuHandler.Instance?.BeginMusterSequence();
                    }
                    else
                    {
                        EnlistedIncidentsBehavior.Instance?.TriggerPayMusterIncident();
                    }
                }

                // XP is now awarded exclusively through the order system (order completion, events)
                // Daily/battle static XP removed - progression happens through gameplay

                ModLogger.Debug("ENLISTMENT", $"Daily service completed: {wage} gold paid");

                // Check for retirement eligibility notification (first term complete)
                CheckRetirementEligibility();

                // Check for NPC soldier desertion when pay tension is high.
                CheckNpcDesertionFromPayTension();

                // Check retinue provisioning status (T7+ only).
                CheckRetinueProvisioningStatus();
            }
            catch (Exception ex)
            {
                ModLogger.Caught("ENLISTMENT", "Daily service processing failed", ex);
            }
        }

        private void CheckProbationExpiration()
        {
            if (!_isOnProbation)
            {
                return;
            }

            if (_probationEnds != CampaignTime.Zero && CampaignTime.Now >= _probationEnds)
            {
                ClearProbation("duration_elapsed");
            }
        }

        private CampaignTime ComputeNextPayday()
        {
            var finance = EnlistedConfig.LoadFinanceConfig();
            var interval = finance?.PaydayIntervalDays > 0 ? finance.PaydayIntervalDays : 12;
            var jitter = finance?.PaydayJitterDays ?? 0f;
            var jitterOffset = jitter > 0f ? MBRandom.RandomFloatRanged(-jitter, jitter) : 0f;
            var days = Math.Max(1f, interval + jitterOffset);
            return CampaignTime.Now + CampaignTime.Days(days);
        }

        internal void ResolvePayMusterStandard()
        {
            if (_isPendingDischarge)
            {
                ModLogger.Info("PAY", "Resolving pay muster: final muster (pending discharge)");
                FinalizePendingDischarge();
                return;
            }

            var payout = _pendingMusterPay;
            var totalWithBackpay = payout + _owedBackpay;
            var canPayFull = CanLordAffordPay(totalWithBackpay);
            var canPayPartial = !canPayFull && CanLordAffordPartialPay(totalWithBackpay);
            var wealthStatus = GetLordWealthStatus();

            ModLogger.Info("PAY", $"Resolving pay muster: PendingPay={payout}, Backpay={_owedBackpay}, " +
                $"CanPayFull={canPayFull}, CanPayPartial={canPayPartial}, LordWealth={wealthStatus}");

            if (canPayFull && totalWithBackpay > 0)
            {
                // Full payment including any backpay
                ProcessFullPayment(totalWithBackpay);
            }
            else if (canPayPartial && totalWithBackpay > 0)
            {
                // Partial payment - lord pays 50% of what's owed
                ProcessPartialPayment(payout);
            }
            else if (payout > 0)
            {
                // Lord can't afford to pay anything - accumulate backpay and tension
                ProcessPayDelay(payout);
            }

            _pendingMusterPay = 0;
            _nextPayday = ComputeNextPayday();
            _payMusterPending = false;
            ClearProbation("pay_muster_resolved");
            ModLogger.Info("PAY", $"Pay muster resolved: {_lastPayOutcome} (NextPayday={_nextPayday}, Tension={_payTension})");

            // Roll quartermaster stock availability for the new muster cycle
            OnMusterCycleComplete();
        }

        /// <summary>
        /// Called after a muster cycle completes to trigger post-muster events.
        /// Handles quartermaster stock availability rolling, ration exchange, and resets news counters.
        /// </summary>
        private void OnMusterCycleComplete()
        {
            try
            {
                QuartermasterManager.Instance?.RollStockAvailability();
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTER", "Failed to roll stock availability at muster", ex);
            }

            try
            {
                // Process ration exchange (reclaim old, issue new)
                ProcessMusterFoodRation();
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Rations", "Failed to process ration exchange at muster", ex);
            }

            // Report muster outcome to news system
            try
            {
                ReportMusterOutcome();
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Muster", "Failed to report muster outcome", ex);
            }

            try
            {
                // Reset "since last muster" counters for camp news display
                EnlistedNewsBehavior.Instance?.ResetMusterCounters();
            }
            catch (Exception ex)
            {
                ModLogger.Caught("News", "Failed to reset muster counters", ex);
            }
        }

        /// <summary>
        /// Builds and reports the muster outcome to the news system.
        /// Summarizes pay, rations, supplies, and unit losses for camp news display.
        /// </summary>
        private void ReportMusterOutcome()
        {
            var newsBehavior = EnlistedNewsBehavior.Instance;
            if (newsBehavior == null)
            {
                return;
            }

            // Parse pay outcome to extract type and amount
            var (payOutcome, payAmount) = ParsePayOutcome(_lastPayOutcome);

            // Get supply level
            int supplyLevel = _companyNeeds?.Supplies ?? 100;

            // Get QM reputation (now using lord relation instead of officer rep)
            int qmRep = EscalationManager.Instance?.State?.LordReputation ?? 50;

            // Get loss/sick counters from news behavior (before reset)
            // These are tracked by EnlistedNewsBehavior from battle events
            int lostCount = 0;
            int sickCount = 0;
            try
            {
                // Access the internal counters through reflection or public accessor if available
                // For now, use the camp news state if accessible
                var lastSnapshot = newsBehavior.GetType()
                    .GetField("_lostSinceLastMuster", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.GetValue(newsBehavior);
                if (lastSnapshot is int lost)
                {
                    lostCount = lost;
                }
                var sickSnapshot = newsBehavior.GetType()
                    .GetField("_sickSinceLastMuster", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.GetValue(newsBehavior);
                if (sickSnapshot is int sick)
                {
                    sickCount = sick;
                }
            }
            catch
            {
                // Ignore reflection errors, leave counts at 0
            }

            // Build and submit muster record
            var record = new MusterOutcomeRecord
            {
                DayNumber = (int)CampaignTime.Now.ToDays,
                PayOutcome = payOutcome,
                PayAmount = payAmount,
                RationOutcome = _lastRationOutcome ?? "none",
                RationItemId = _lastRationItemId ?? string.Empty,
                QmReputation = qmRep,
                SupplyLevel = supplyLevel,
                LostSinceLast = lostCount,
                SickSinceLast = sickCount,
                RationFlavorText = !string.IsNullOrEmpty(_lastRationItemId) ? GetRationFlavorText(qmRep) : string.Empty
            };

            newsBehavior.AddMusterOutcome(record);
            ModLogger.Info("Muster", $"Muster outcome reported: pay={payOutcome}:{payAmount}, ration={_lastRationOutcome}, supply={supplyLevel}%");
        }

        /// <summary>
        /// Parses the _lastPayOutcome string to extract outcome type and amount.
        /// Formats: "standard:1000", "backpay:1500", "partial_backpay:800", "promissory:500", "corruption:1200", etc.
        /// </summary>
        private (string outcome, int amount) ParsePayOutcome(string payOutcome)
        {
            if (string.IsNullOrEmpty(payOutcome))
            {
                return ("unknown", 0);
            }

            // Handle outcomes with amounts (format: "type:amount")
            if (payOutcome.Contains(':'))
            {
                var parts = payOutcome.Split(':');
                if (parts.Length >= 2 && int.TryParse(parts[parts.Length - 1], out int amount))
                {
                    var outcome = parts[0] switch
                    {
                        "standard" => "paid",
                        "backpay" => "paid",
                        "partial_backpay" => "partial",
                        "promissory" => "promissory",
                        "corruption" => "corruption",
                        "side_deal" => "side_deal",
                        _ => parts[0]
                    };
                    return (outcome, amount);
                }
            }

            // Handle simple outcomes without amounts
            return payOutcome switch
            {
                "delayed" => ("delayed", 0),
                "none" => ("none", 0),
                _ => (payOutcome, 0)
            };
        }

        /// <summary>
        /// Processes ration exchange at muster for T1-T6 enlisted personnel.
        /// Reclaims previously issued rations and issues new rations based on QM reputation and supply availability.
        /// T7+ commanders don't receive issued rations; they provision their own retinue.
        /// </summary>
        private void ProcessMusterFoodRation()
        {
            // Reset ration tracking for this muster
            _lastRationOutcome = null;
            _lastRationItemId = null;

            // T1-T6 (Enlisted/NCO/Officer) receive issued rations from the quartermaster
            if (_enlistmentTier >= 7)
            {
                // If player was promoted to T7+ since last muster, reclaim any outstanding rations
                // but don't issue new ones (commanders provision their own retinue)
                if (_issuedRations != null && _issuedRations.Count > 0)
                {
                    ModLogger.Info("Rations", "Player promoted to commander - reclaiming outstanding enlisted rations");
                    ReclaimIssuedRations();
                }
                else
                {
                    ModLogger.Debug("Rations", "Skipping ration exchange for T7+ commander");
                }
                _lastRationOutcome = "commander_exempt";
                return;
            }

            // Check company supply availability
            int companySupply = _companyNeeds?.Supplies ?? 100;
            bool rationAvailable = DetermineRationAvailability(companySupply);

            if (!rationAvailable)
            {
                // No rations this muster - don't reclaim either (supply crisis)
                DisplayNoRationsMessage(companySupply);
                _lastRationOutcome = companySupply < 30 ? "none_critical" : "none_low_supply";
                ModLogger.Info("Rations", $"No rations issued at muster (supply={companySupply}%)");
                return;
            }

            // Reclaim old rations from inventory
            ReclaimIssuedRations();

            // Issue new ration based on QM reputation
            IssueNewRation();
            _lastRationOutcome = "issued";
        }

        /// <summary>
        /// Determines if rations are available based on company supply levels.
        /// Supply 70%+: Always available
        /// Supply 50-69%: 80% chance
        /// Supply 30-49%: 50% chance
        /// Supply &lt;30%: Never available
        /// </summary>
        private bool DetermineRationAvailability(int supply)
        {
            if (supply >= 70)
            {
                return true;
            }
            if (supply >= 50)
            {
                return MBRandom.RandomFloat < 0.80f;
            }
            if (supply >= 30)
            {
                return MBRandom.RandomFloat < 0.50f;
            }
            return false;
        }

        /// <summary>
        /// Displays a message when no rations are available due to low supplies.
        /// </summary>
        private void DisplayNoRationsMessage(int supply)
        {
            string message = supply < 30
                ? "No rations were issued � supplies are critically low."
                : "No rations were issued � supplies are too low.";

            InformationManager.DisplayMessage(new InformationMessage(message, Colors.Yellow));
        }

        /// <summary>
        /// Reclaims all previously issued rations from the player's inventory.
        /// Only reclaims items that were tracked as issued (personal food is never reclaimed).
        /// Edge case handling: If player has multiple of the same food type (e.g., 3 grain from various sources),
        /// we only reclaim the exact amount that was issued. Extra food remains as personal property.
        /// </summary>
        private void ReclaimIssuedRations()
        {
            if (_issuedRations == null || _issuedRations.Count == 0)
            {
                ModLogger.Debug("Rations", "No issued rations to reclaim");
                return;
            }

            // Safety check: Ensure MobileParty exists (could be null during capture/unusual states)
            if (MobileParty.MainParty == null)
            {
                ModLogger.Warn("Rations", "Cannot reclaim rations - MainParty is null (player may be captured)");
                // Don't clear issued rations list - will retry on next opportunity
                return;
            }

            var itemRoster = MobileParty.MainParty.ItemRoster;
            if (itemRoster == null)
            {
                ModLogger.Warn("Rations", "Cannot reclaim rations - ItemRoster is null");
                return;
            }

            int totalReclaimed = 0;

            foreach (var record in _issuedRations)
            {
                var item = MBObjectManager.Instance.GetObject<ItemObject>(record.ItemId);
                if (item == null)
                {
                    ModLogger.Warn("Rations", $"Cannot reclaim ration - item not found: {record.ItemId}");
                    continue;
                }

                int toReclaim = record.Amount;
                int inInventory = itemRoster.GetItemNumber(item);
                int reclaimAmount = Math.Min(toReclaim, inInventory);

                if (reclaimAmount > 0)
                {
                    _ = itemRoster.AddToCounts(item, -reclaimAmount);
                    totalReclaimed += reclaimAmount;
                    ModLogger.Debug("Rations", $"Reclaimed {reclaimAmount}x {record.ItemId} from inventory");
                }
                else
                {
                    ModLogger.Debug("Rations", $"Could not reclaim {record.ItemId} - not in inventory (consumed or sold)");
                }
            }

            _issuedRations.Clear();

            if (totalReclaimed > 0)
            {
                ModLogger.Info("Rations", $"Reclaimed {totalReclaimed} issued rations at muster");
            }
        }

        /// <summary>
        /// Issues a new ration to the player based on QM reputation.
        /// Food quality improves with higher reputation.
        /// Note: Stowage system not yet implemented. When available, issued rations
        /// will be tracked there as well for reclamation.
        /// </summary>
        private void IssueNewRation()
        {
            // Get QM reputation (now using lord relation)
            int qmRep = EscalationManager.Instance?.State?.LordReputation ?? 50;

            // Determine food item based on QM reputation
            string itemId = GetFoodItemForReputation(qmRep);
            var item = MBObjectManager.Instance.GetObject<ItemObject>(itemId);

            if (item == null)
            {
                ModLogger.Warn("Rations", $"Cannot issue ration - item not found: {itemId}");
                return;
            }

            // Safety check: Ensure MobileParty exists
            if (MobileParty.MainParty == null)
            {
                ModLogger.Warn("Rations", "Cannot issue ration - MainParty is null (player may be captured)");
                return;
            }

            // Add to player inventory
            var itemRoster = MobileParty.MainParty.ItemRoster;
            if (itemRoster == null)
            {
                ModLogger.Warn("Rations", "Cannot issue ration - ItemRoster is null");
                return;
            }

            int amount = 1;
            _ = itemRoster.AddToCounts(item, amount);

            // Track issued ration for reclamation at next muster
            _issuedRations.Add(new IssuedRationRecord
            {
                ItemId = itemId,
                Amount = amount,
                IssuedAt = CampaignTime.Now
            });

            // Track for muster news reporting
            _lastRationItemId = itemId;

            // Flavor text is saved for Personal Feed/Muster news only - not displayed during conversation
            // Displaying messages during active conversations creates UI clutter and debug text overlap
            string flavorText = GetRationFlavorText(qmRep);

            ModLogger.Info("Rations", $"Issued {amount}x {itemId} (QM rep={qmRep}), flavor: {flavorText}");
        }

        /// <summary>
        /// Determines which food item to issue based on QM reputation.
        /// QM Rep 80+: Meat (high value)
        /// QM Rep 50-79: Cheese (good value)
        /// QM Rep 20-49: Butter (moderate value)
        /// QM Rep &lt;20: Grain (low value)
        /// </summary>
        private string GetFoodItemForReputation(int qmRep)
        {
            if (qmRep >= 80)
            {
                return "meat";
            }
            if (qmRep >= 50)
            {
                return "cheese";
            }
            if (qmRep >= 20)
            {
                return "butter";
            }
            return "grain";
        }

        /// <summary>
        /// Generates flavor text for ration issuance based on QM reputation and food type.
        /// </summary>
        private string GetRationFlavorText(int qmRep)
        {
            if (qmRep >= 80)
            {
                return "The quartermaster favors you with a fine cut of meat.";
            }
            if (qmRep >= 50)
            {
                return "The quartermaster is being generous � a wheel of cheese for the week.";
            }
            if (qmRep >= 20)
            {
                return "Better than grain, at least. A small portion of butter.";
            }
            if (qmRep >= -10)
            {
                return "Standard rations. Grain, as always.";
            }
            return "The quartermaster sneers as he hands you moldy grain.";
        }

        /// <summary>
        /// Process full payment including any backpay. Reduces tension by 30.
        /// </summary>
        private void ProcessFullPayment(int grossAmount)
        {
            GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, grossAmount);
            OnWagePaid?.Invoke(grossAmount);

            // Update pay tension state - successful full payment reduces tension by 30
            _lastPayDate = CampaignTime.Now;
            var hadBackpay = _owedBackpay > 0;
            _payTension = Math.Max(0, _payTension - 30);
            _owedBackpay = 0;
            _consecutiveDelays = 0;

            _lastPayOutcome = hadBackpay ? $"backpay:{grossAmount}" : $"standard:{grossAmount}";
            ModLogger.Info("Gold", $"Full payment: {grossAmount}");

            // Record in Personal Feed for historical review
            EnlistedNewsBehavior.Instance?.AddPayMusterNews(hadBackpay ? "backpay" : "full", grossAmount);

            if (hadBackpay)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Back pay received! {grossAmount} denars (including owed wages).",
                    Colors.Green));
            }
        }

        /// <summary>
        /// Process partial payment - lord pays current week + 50% of backpay. Reduces tension by 10.
        /// </summary>
        private void ProcessPartialPayment(int currentPay)
        {
            // Pay current week + half of backpay
            var backpayPortion = _owedBackpay / 2;
            var grossAmount = currentPay + backpayPortion;

            GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, grossAmount);
            OnWagePaid?.Invoke(grossAmount);

            // Update state - partial payment reduces tension by 10, halves remaining backpay
            _lastPayDate = CampaignTime.Now;
            _owedBackpay = _owedBackpay - backpayPortion;
            _payTension = Math.Max(0, _payTension - 10);
            // Don't reset consecutive delays for partial payment

            _lastPayOutcome = $"partial_backpay:{grossAmount}";
            ModLogger.Info("Gold", $"Partial payment: {grossAmount} (stillOwed={_owedBackpay})");

            // Record in Personal Feed for historical review
            EnlistedNewsBehavior.Instance?.AddPayMusterNews("partial", grossAmount, _owedBackpay);

            InformationManager.DisplayMessage(new InformationMessage(
                $"Partial pay received: {grossAmount} denars. Still owed: {_owedBackpay}.",
                Colors.Yellow));
        }

        /// <summary>
        /// Process pay delay - accumulate backpay and increase tension.
        /// </summary>
        private void ProcessPayDelay(int unpaidAmount)
        {
            _owedBackpay += unpaidAmount;
            _consecutiveDelays++;

            // Tension escalates: 10 + 5 per week overdue
            var tensionIncrease = GetPayTensionIncrease();
            _payTension = Math.Min(100, _payTension + tensionIncrease);

            _lastPayOutcome = $"delayed:{unpaidAmount}";
            ModLogger.Warn("PAY", $"Pay delayed: Owed={_owedBackpay}, Tension={_payTension}, Consecutive={_consecutiveDelays}");

            // Record in Personal Feed for historical review
            EnlistedNewsBehavior.Instance?.AddPayMusterNews("delayed", 0, _owedBackpay);

            InformationManager.DisplayMessage(new InformationMessage(
                $"Pay delayed! Lord {_enlistedLord?.Name} cannot afford wages. Owed: {_owedBackpay} denars.",
                Colors.Red));
        }

        /// <summary>
        /// Write off backpay debt (used when transferring lords or lord goes completely broke).
        /// Clears owed amount and tension, but damages lord relation.
        /// </summary>
        internal void WriteOffBackpay(string reason)
        {
            if (_owedBackpay <= 0)
            {
                return;
            }

            var writtenOff = _owedBackpay;
            _owedBackpay = 0;
            _payTension = 0;
            _consecutiveDelays = 0;

            // Damage relation with lord for stiffing us
            if (_enlistedLord != null)
            {
                ChangeRelationAction.ApplyPlayerRelation(_enlistedLord, -10, false);
            }

            _lastPayOutcome = $"written_off:{writtenOff}";
            ModLogger.Warn("PAY", $"Backpay written off: {writtenOff} denars ({reason})");

            InformationManager.DisplayMessage(new InformationMessage(
                $"Your back pay of {writtenOff} denars has been written off. {reason}",
                Colors.Red));
        }

        internal void ResolveCorruptionMuster()
        {
            try
            {
                if (_isPendingDischarge)
                {
                    ModLogger.Info("PAY", "Resolving pay muster: final muster (pending discharge)");
                    FinalizePendingDischarge();
                    return;
                }

                ModLogger.Info("PAY", $"Resolving pay muster: corruption attempt (PendingPay={_pendingMusterPay})");

                var roguery = Hero.MainHero.GetSkillValue(DefaultSkills.Roguery);
                var charm = Hero.MainHero.GetSkillValue(DefaultSkills.Charm);
                var best = Math.Max(roguery, charm);

                // Requirement: Roguery > 20 OR Charm > 20
                if (best <= 20)
                {
                    FailCorruption();
                }
                else
                {
                    var baseChance = 0.70f;
                    var bonus = (best - 20) * 0.005f;
                    var chance = Math.Min(0.90f, baseChance + bonus);
                    var roll = MBRandom.RandomFloat;

                    if (roll <= chance)
                    {
                        // Success: 1.20x
                        var payout = (int)(_pendingMusterPay * 1.2f);
                        if (payout > 0)
                        {
                            GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, payout);
                            OnWagePaid?.Invoke(payout);
                        }
                        _lastPayOutcome = $"corruption_success:{payout}";
                        ModLogger.Info("PAY", $"Corruption muster success (Payout={payout}, Chance={chance:0.00}, Roll={roll:0.00})");
                        if (_enlistedLord != null)
                        {
                            ChangeRelationAction.ApplyPlayerRelation(_enlistedLord, 1);
                        }
                    }
                    else
                    {
                        FailCorruption();
                        ModLogger.Info("PAY", $"Corruption muster failed (Outcome={_lastPayOutcome}, Chance={chance:0.00}, Roll={roll:0.00})");
                    }
                }

                _pendingMusterPay = 0;
                _nextPayday = ComputeNextPayday();
                _payMusterPending = false;
                ClearProbation("pay_muster_resolved");
                ModLogger.Info("PAY", $"Pay muster resolved: corruption (Outcome={_lastPayOutcome}, NextPayday={_nextPayday})");
                OnMusterCycleComplete();
            }
            catch (Exception ex)
            {
                ModLogger.Caught("PAY", "Corruption muster failed", ex);
                _payMusterPending = false;
                _nextPayday = ComputeNextPayday();
            }
        }

        private void FailCorruption()
        {
            var payout = (int)(_pendingMusterPay * 0.95f);
            if (payout > 0)
            {
                GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, payout);
                OnWagePaid?.Invoke(payout);
            }

            if (_enlistedLord != null)
            {
                ChangeRelationAction.ApplyPlayerRelation(_enlistedLord, -5);
            }

            _lastPayOutcome = $"corruption_fail:{payout}";
        }

        internal string ResolveSideDealMuster()
        {
            try
            {
                if (_isPendingDischarge)
                {
                    ModLogger.Info("PAY", "Resolving pay muster: final muster (pending discharge)");
                    FinalizePendingDischarge();
                    return "discharge";
                }

                ModLogger.Info("PAY", $"Resolving Quartermaster's Deal (PendingPay={_pendingMusterPay})");

                var payout = Math.Max(0, (int)(_pendingMusterPay * 0.4f));

                // Calculate success chance: 70% base + QM rep modifier
                var qmRep = GetQMReputation();
                var baseChance = 0.70f;
                var repModifier = 0f;

                if (qmRep >= 75)
                {
                    repModifier = 0.15f;
                }
                else if (qmRep >= 50)
                {
                    repModifier = 0.05f;
                }
                else if (qmRep >= 25)
                {
                    repModifier = 0f;
                }
                else
                {
                    repModifier = -0.10f;
                }

                var successChance = baseChance + repModifier;
                var roll = MBRandom.RandomFloat;
                var success = roll <= successChance;

                ModLogger.Info("PAY", $"QM Deal: Roll={roll:0.00}, Chance={successChance:0.00}, QMRep={qmRep}, Success={success}");

                // Always give 40% pay
                if (payout > 0)
                {
                    GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, payout);
                    OnWagePaid?.Invoke(payout);
                }

                string outcome;
                if (success)
                {
                    // Roll for equipment tier (90% current, 10% +1 capped at T9)
                    var currentTier = _enlistmentTier;
                    var equipmentTier = currentTier;
                    var tierRoll = MBRandom.RandomFloat;
                    if (tierRoll <= 0.10f && currentTier < 9)
                    {
                        equipmentTier = currentTier + 1;
                        ModLogger.Info("PAY", $"QM Deal: Tier bonus! Rolling for T{equipmentTier} instead of T{currentTier}");
                    }

                    // Select equipment weighted by player skills
                    var selectedItem = SelectEquipmentBySkills(equipmentTier);

                    if (selectedItem != null)
                    {
                        // Add item to inventory
                        _ = (Hero.MainHero.PartyBelongedTo?.ItemRoster?.AddToCounts(selectedItem, 1));

                        outcome = $"qm_deal_success:{payout}:{selectedItem.StringId}";
                        ModLogger.Info("PAY", $"QM Deal successful - awarded {selectedItem.Name} (T{equipmentTier})");
                    }
                    else
                    {
                        // No suitable equipment found, fall back to failure
                        outcome = $"qm_deal_failure:{payout}";
                        ModLogger.Info("PAY", $"QM Deal: No suitable equipment found for T{equipmentTier}, giving coin only");
                    }
                }
                else
                {
                    outcome = $"qm_deal_failure:{payout}";
                    ModLogger.Info("PAY", $"QM Deal failed - only received {payout} denars");
                }

                _lastPayOutcome = outcome;
                _pendingMusterPay = 0;
                _nextPayday = ComputeNextPayday();
                _payMusterPending = false;
                ClearProbation("pay_muster_resolved");
                ModLogger.Info("PAY", $"Pay muster resolved: {_lastPayOutcome} (NextPayday={_nextPayday})");
                OnMusterCycleComplete();

                return outcome;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("PAY", "Quartermaster's Deal failed", ex);
                _payMusterPending = false;
                _nextPayday = ComputeNextPayday();
                return "error";
            }
        }

        internal void DeferPayMuster()
        {
            _payMusterPending = false;
            // Player cancelled the pay muster prompt; do NOT push the schedule out by a full interval.
            // Instead, retry soon so the player can resolve pay without losing track of it.
            _nextPayday = CampaignTime.Now + CampaignTime.Days(1f);
            ModLogger.Info("PAY", $"Pay muster deferred; retry scheduled for {_nextPayday} (Pending={_pendingMusterPay}).");
        }

        internal void ResolvePromissoryMuster()
        {
            try
            {
                if (_isPendingDischarge)
                {
                    // Promissory notes aren't used for final muster; resolve normally.
                    ResolvePayMusterStandard();
                    return;
                }

                // Accept payment in arrears: wages stay owed, roll into next regular muster cycle
                _lastPayOutcome = $"promissory:{_pendingMusterPay}";
                _payMusterPending = false;
                _nextPayday = ComputeNextPayday();
                ModLogger.Info("PAY",
                    $"Pay muster resolved: payment in arrears accepted (PendingStillOwed={_pendingMusterPay}, NextMuster={_nextPayday})");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("PAY", "Promissory muster failed", ex);
                _payMusterPending = false;
                _nextPayday = ComputeNextPayday();
            }
        }

        public bool RequestDischarge()
        {
            if (!IsEnlisted)
            {
                return false;
            }

            _isPendingDischarge = true;
            _lastPayOutcome = "pending_discharge";
            ModLogger.Info("RETIREMENT", "Pending discharge requested; will resolve at next pay muster");
            return true;
        }

        public bool CancelDischarge()
        {
            if (!_isPendingDischarge)
            {
                return false;
            }

            _isPendingDischarge = false;
            _lastPayOutcome = "pending_discharge_cancelled";
            ModLogger.Info("RETIREMENT", "Pending discharge cancelled");
            return true;
        }

        private void FinalizePendingDischarge()
        {
            try
            {
                var daysServed = (int)(CampaignTime.Now - _enlistmentDate).ToDays;
                var config = EnlistedConfig.LoadRetirementConfig();
                var band = daysServed >= 200
                    ? (_enlistmentTier >= 4 ? "heroic" : "veteran")
                    : daysServed >= 100
                        ? "honorable"
                        : "washout";

                // Honorable/Veteran discharge requires at least neutral relation with the enlisted lord.
                // If relations are negative at exit, treat as washout even if days-served threshold is met.
                if ((band == "honorable" || band == "veteran") &&
                    _enlistedLord != null &&
                    Hero.MainHero.GetRelation(_enlistedLord) < 0)
                {
                    band = "washout";
                }

                int lordRelation;
                int factionRelation;
                int severance;
                bool isHonorable;

                switch (band)
                {
                    case "heroic":
                    case "veteran":
                        lordRelation = 30;
                        factionRelation = 15;
                        severance = Math.Max(0, config?.SeveranceVeteran ?? 3000);
                        isHonorable = true;
                        _lastDischargeBand = band;
                        break;
                    case "honorable":
                        lordRelation = 10;
                        factionRelation = 5;
                        severance = Math.Max(0, config?.SeveranceHonorable ?? 3000);
                        isHonorable = true;
                        _lastDischargeBand = "honorable";
                        break;
                    default:
                        lordRelation = -10;
                        factionRelation = -10;
                        severance = 0;
                        isHonorable = false;
                        _lastDischargeBand = "washout";
                        break;
                }

                // Apply relations
                if (_enlistedLord != null)
                {
                    ChangeRelationAction.ApplyPlayerRelation(_enlistedLord, lordRelation);
                }

                var faction = _enlistedLord?.MapFaction;
                if (faction is Kingdom kingdom && kingdom.Leader != null && kingdom.Leader != _enlistedLord)
                {
                    ChangeRelationAction.ApplyPlayerRelation(kingdom.Leader, factionRelation);
                }

                // Payout: pending muster + severance
                var payout = Math.Max(0, _pendingMusterPay) + Math.Max(0, severance);
                if (payout > 0)
                {
                    GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, payout);
                }

                ModLogger.Info("PAY",
                    $"Final muster resolved (Band={band}, DaysServed={daysServed}, PendingPay={_pendingMusterPay}, Severance={severance}, Payout={payout})");

                _pendingMusterPay = 0;
                _payMusterPending = false;
                _isPendingDischarge = false;
                _nextPayday = CampaignTime.Zero;
                _lastPayOutcome = $"final_muster:{band}:{payout}";
                _lastDischargeBand = band;

                // Snapshot reservist data for re-entry flows
                ServiceRecordManager.Instance?.RecordReservist(
                    band,
                    daysServed,
                    _enlistmentTier,
                    _enlistmentXP,
                    _enlistedLord);

                HandleGearOnDischarge(band);
                HandleBaggageOnDischarge(band);

                // Pension assignment handled inside StopEnlist via ApplyPensionOnDischarge
                StopEnlist($"Final muster ({band})", isHonorable);
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("RETIREMENT", "Error finalizing discharge", ex,
                    ctx: LogCtx.PlayerState());
                _isPendingDischarge = false;
                _payMusterPending = false;
                _pendingMusterPay = 0;
                _nextPayday = CampaignTime.Zero;
            }
        }

        internal void ResolveSmuggleDischarge()
        {
            try
            {
                if (!IsEnlisted || !_isPendingDischarge)
                {
                    return;
                }

                ModLogger.Info("PAY", "Resolving final muster: smuggle discharge (deserter)");

                var faction = _enlistedLord?.MapFaction as Kingdom;
                if (faction != null)
                {
                    ChangeCrimeRatingAction.Apply(faction, 30f);
                    if (faction.Leader != null)
                    {
                        ChangeRelationAction.ApplyPlayerRelation(faction.Leader, -50);
                    }
                }

                if (_enlistedLord != null)
                {
                    ChangeRelationAction.ApplyPlayerRelation(_enlistedLord, -50);
                }

                // Deserter outcome: keep all gear, clear pension
                _pensionAmountPerDay = 0;
                _pensionFactionId = null;
                _isPensionPaused = true;

                _pendingMusterPay = 0;
                _payMusterPending = false;
                _isPendingDischarge = false;
                _nextPayday = CampaignTime.Zero;
                _lastDischargeBand = "deserter";
                _lastPayOutcome = "final_muster:deserter:smuggle";

                ServiceRecordManager.Instance?.RecordReservist(
                    "deserter",
                    (int)(CampaignTime.Now - _enlistmentDate).ToDays,
                    _enlistmentTier,
                    _enlistmentXP,
                    _enlistedLord);

                StopEnlist("Smuggle discharge (deserter)");
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("PAY", "Error resolving smuggle discharge", ex, ctx: LogCtx.PlayerState());
                _isPendingDischarge = false;
                _payMusterPending = false;
                _pendingMusterPay = 0;
                _nextPayday = CampaignTime.Zero;
            }
        }

        private void ProcessDailyPension()
        {
            // Pension is only paid while NOT enlisted (no double-dipping).
            if (IsEnlisted)
            {
                return;
            }

            if (_pensionAmountPerDay <= 0 || _isPensionPaused)
            {
                return;
            }

            try
            {
                var faction = ResolvePensionFaction();
                if (faction != null)
                {
                    // Stop if at war with pension faction
                    if (Hero.MainHero?.MapFaction != null && faction.IsAtWarWith(Hero.MainHero.MapFaction))
                    {
                        _isPensionPaused = true;
                        _lastPayOutcome = "pension_paused_war";
                        return;
                    }

                    // Stop if crime rating positive (best-effort)
                    if (HasCrimeAgainstFaction(faction))
                    {
                        _isPensionPaused = true;
                        _lastPayOutcome = "pension_paused_crime";
                        return;
                    }

                    // Stop if relation below threshold
                    var config = EnlistedConfig.LoadRetirementConfig();
                    var relationStop = config?.PensionRelationStopThreshold ?? 0;
                    if (faction.Leader != null && Hero.MainHero != null && Hero.MainHero.GetRelation(faction.Leader) < relationStop)
                    {
                        _isPensionPaused = true;
                        _lastPayOutcome = "pension_paused_relation";
                        return;
                    }
                }

                if (_pensionAmountPerDay > 0)
                {
                    GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, _pensionAmountPerDay);
                }
                _lastPayOutcome = $"pension:{_pensionAmountPerDay}";
                ModLogger.Info("Gold", $"Pension paid: {_pensionAmountPerDay} denars");
            }
            catch (Exception ex)
            {
                // Pension is processed daily while not enlisted. If this throws, pause pension to avoid log spam
                // and prevent repeating a potentially bad/partial payout.
                _isPensionPaused = true;
                _lastPayOutcome = "pension_paused_error";
                ModLogger.Caught("Gold", "Pension processing failed; pension paused", ex);
            }
        }

        private IFaction ResolvePensionFaction()
        {
            if (string.IsNullOrWhiteSpace(_pensionFactionId))
            {
                return null;
            }

            try
            {
                // Search kingdoms first
                var kingdom = Campaign.Current?.Kingdoms?.FirstOrDefault(k => k.StringId == _pensionFactionId);
                if (kingdom != null)
                {
                    return kingdom;
                }
                // Then search clans
                var clan = Campaign.Current?.Clans?.FirstOrDefault(c => c.StringId == _pensionFactionId);
                return clan;
            }
            catch
            {
                return null;
            }
        }

        private bool HasCrimeAgainstFaction(IFaction faction)
        {
            try
            {
                if (faction == null)
                {
                    return false;
                }

                // Verified via decompile: crime rating is stored per-faction on IFaction.MainHeroCrimeRating
                return faction.MainHeroCrimeRating > 0f;
            }
            catch
            {
                // best-effort
            }

            return false;
        }

        private void HandleGearOnDischarge(string band)
        {
            try
            {
                var hero = Hero.MainHero;
                if (hero == null)
                {
                    return;
                }

                var retireConfig = EnlistedConfig.LoadRetirementConfig();
                if (retireConfig?.DebugSkipGearStripping == true)
                {
                    ModLogger.Debug("EQUIPMENT", "Gear stripping skipped (debug flag)");
                    return;
                }

                switch (band)
                {
                    case "washout":
                        ClearAllEquipment(hero);
                        break;
                    case "honorable":
                    case "veteran":
                    case "heroic":
                        // Keep armor (slots 6–9), clear weapons (0–3) and mounts (10–11) to inventory
                        MoveSlotsToInventory(hero, new[] { 0, 1, 2, 3, 10, 11 });
                        break;
                }
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("EQUIPMENT", "Gear handling on discharge failed", ex, ctx: LogCtx.PlayerState());
            }
        }

        /// <summary>
        /// Handles baggage stash on discharge based on the discharge type.
        /// - Deserter: Forfeit ALL baggage (abandoned post)
        /// - Dishonorable/Washout: QM reclaims issued items from baggage
        /// - Honorable/Veteran/Heroic: Keep all baggage (earned through service)
        /// </summary>
        private void HandleBaggageOnDischarge(string band)
        {
            try
            {
                if (_baggageStash == null)
                {
                    return;
                }

                switch (band)
                {
                    case "deserter":
                        // Deserters forfeit all baggage (abandoned post)
                        if (_baggageStash.Count > 0)
                        {
                            var itemCount = _baggageStash.Count;
                            _baggageStash.Clear();
                            ModLogger.Info("Discharge", $"Baggage forfeited due to desertion ({itemCount} items lost)");
                        }
                        break;

                    case "dishonorable":
                    case "washout":
                        // Dishonorable/washout: Baggage is forfeited (no item tracking needed)
                        ModLogger.Info("Discharge", $"Baggage forfeited due to {band} discharge");
                        break;

                    case "honorable":
                    case "veteran":
                    case "heroic":
                        // Honorable discharge: Keep all baggage (earned through service)
                        ModLogger.Debug("Discharge", $"Baggage preserved - honorable service ({band} discharge)");
                        break;

                    default:
                        // Unknown discharge type - log and preserve baggage by default
                        ModLogger.Warn("Discharge", $"Unknown discharge band '{band}' - preserving baggage");
                        break;
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Discharge", "Error handling baggage on discharge", ex);
            }
        }

        private void MoveSlotsToInventory(Hero hero, int[] slotIndices)
        {
            if (hero?.BattleEquipment == null || slotIndices == null)
            {
                return;
            }

            var roster = hero.PartyBelongedTo?.ItemRoster ?? MobileParty.MainParty?.ItemRoster;
            var updated = hero.BattleEquipment.Clone();
            foreach (var idx in slotIndices)
            {
                if (idx < 0 || idx >= (int)EquipmentIndex.NumEquipmentSetSlots)
                {
                    continue;
                }

                var slot = (EquipmentIndex)idx;
                var elem = updated[slot];
                if (elem.Item != null)
                {
                    // Preserve modifiers/quest flags by moving the full EquipmentElement into inventory.
                    _ = (roster?.AddToCounts(elem, 1));
                    updated[slot] = default;
                }
            }

            // Apply equipment changes safely (ensures visuals refresh).
            EquipmentHelper.AssignHeroEquipmentFromEquipment(hero, updated);
        }

        private void ClearAllEquipment(Hero hero)
        {
            try
            {
                if (hero?.BattleEquipment != null)
                {
                    for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
                    {
                        hero.BattleEquipment[(EquipmentIndex)i] = default;
                    }
                }

                if (hero?.CivilianEquipment != null)
                {
                    for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
                    {
                        hero.CivilianEquipment[(EquipmentIndex)i] = default;
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("EQUIPMENT", "Failed to clear equipment", ex);
            }
        }

        private void ApplyPensionOnDischarge(bool isHonorableDischarge, int daysServed)
        {
            try
            {
                // Default: clear/paused
                _pensionAmountPerDay = 0;
                _pensionFactionId = null;
                _isPensionPaused = true;

                if (!isHonorableDischarge || daysServed <= 0)
                {
                    return;
                }

                var faction = _enlistedLord?.MapFaction;
                if (faction == null)
                {
                    ModLogger.Debug("RETIREMENT", "No faction for pension determination");
                    return;
                }

                var config = EnlistedConfig.LoadRetirementConfig();
                var honorable = config?.PensionHonorableDaily ?? 50;
                var veteran = config?.PensionVeteranDaily ?? 100;
                var relationStop = config?.PensionRelationStopThreshold ?? 0;

                // Determine band
                var amount = daysServed >= 200 ? veteran : (daysServed >= 100 ? honorable : 0);
                if (amount <= 0)
                {
                    ModLogger.Debug("RETIREMENT", $"No pension awarded (days served: {daysServed})");
                    return;
                }

                // Relation gate at award time
                var leader = faction.Leader;
                if (leader != null && Hero.MainHero.GetRelation(leader) < relationStop)
                {
                    ModLogger.Info("RETIREMENT",
                        $"Pension blocked at award time due to relation {Hero.MainHero.GetRelation(leader)} < {relationStop}");
                    _lastPayOutcome = "pension_blocked_relation";
                    return;
                }

                _pensionAmountPerDay = amount;
                _pensionFactionId = faction.StringId;
                _isPensionPaused = false;
                _lastPayOutcome = $"pension_start:{amount}";

                ModLogger.Info("RETIREMENT",
                    $"Pension started: {amount}/day (daysServed={daysServed}, faction={faction.Name})");
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("RETIREMENT", "Error applying pension on discharge", ex, ctx: LogCtx.PlayerState());
            }
        }

        /// <summary>
        ///     Calculate daily wage based on tier, level, and duties system bonuses.
        ///     Uses config from enlisted_config.json finance section.
        /// </summary>
        private int CalculateDailyWage()
        {
            // Load wage formula from config
            var financeConfig = EnlistedConfig.LoadFinanceConfig();
            var formula = financeConfig.WageFormula;

            // Base wage formula from config: base + (level*levelMult) + (tier*tierMult) + (xp/xpDiv)
            var xpDivisor = formula.XpDivisor > 0 ? formula.XpDivisor : 200;
            var baseWage = formula.BaseWage +
                           Hero.MainHero.Level * formula.LevelMultiplier +
                           _enlistmentTier * formula.TierMultiplier +
                           _enlistmentXP / xpDivisor;

            // Army bonus from config (default +20% when in active army)
            var lordParty = _enlistedLord.PartyBelongedTo;
            var armyMultiplier = lordParty?.Army != null ? formula.ArmyBonusMultiplier : 1.0f;

            // Apply multipliers and cap
            var probationMult = _isOnProbation ? Math.Max(0.01f, EnlistedConfig.LoadRetirementConfig().ProbationWageMultiplier) : 1f;
            var finalWage = Math.Min((int)(baseWage * armyMultiplier * probationMult), 150);
            return Math.Max(finalWage, 24); // Minimum 24 gold/day
        }

        #region Pay System

        /// <summary>
        /// Lord's financial status affects pay reliability and amount.
        /// </summary>
        internal enum LordWealthStatus { Wealthy, Comfortable, Struggling, Poor, Broke }

        /// <summary>
        /// Check if the lord has enough gold to pay the player.
        /// Lords keep a buffer so we don't drain them completely.
        /// </summary>
        private bool CanLordAffordPay(int amount)
        {
            var lord = _enlistedLord;
            if (lord == null)
            {
                return false;
            }

            // Lord needs to keep a buffer - don't drain them to zero
            const int minimumBuffer = 500;
            return lord.Gold >= amount + minimumBuffer;
        }

        /// <summary>
        /// Check if lord can afford partial pay (at least 50%).
        /// </summary>
        private bool CanLordAffordPartialPay(int amount)
        {
            var lord = _enlistedLord;
            if (lord == null)
            {
                return false;
            }

            const int minimumBuffer = 200;
            return lord.Gold >= (amount / 2) + minimumBuffer;
        }

        /// <summary>
        /// Get lord's financial status based on gold thresholds.
        /// Used for pay reliability checks.
        /// </summary>
        private LordWealthStatus GetLordWealthStatus()
        {
            var gold = _enlistedLord?.Gold ?? 0;
            if (gold > 50000)
            {
                return LordWealthStatus.Wealthy;
            }
            if (gold > 20000)
            {
                return LordWealthStatus.Comfortable;
            }
            if (gold > 5000)
            {
                return LordWealthStatus.Struggling;
            }
            if (gold > 1000)
            {
                return LordWealthStatus.Poor;
            }
            return LordWealthStatus.Broke;
        }

        /// <summary>
        /// Clear pay tension state when service ends or transfers.
        /// </summary>
        private void ClearPayState(string reason)
        {
            _payTension = 0;
            _owedBackpay = 0;
            _lastPayDate = CampaignTime.Zero;
            _consecutiveDelays = 0;
            ModLogger.Debug("PAY", $"Pay state cleared: {reason}");
        }

        /// <summary>
        /// Calculate pay tension increase based on weeks overdue.
        /// Base 10 + 5 per week overdue, per pay_tension_events.md spec.
        /// </summary>
        private int GetPayTensionIncrease()
        {
            if (_lastPayDate == CampaignTime.Zero)
            {
                return 10;
            }
            var weeksOverdue = Math.Max(0, (DaysSincePay - 7) / 7);
            return 10 + (weeksOverdue * 5);
        }

        /// <summary>
        /// Selects equipment for Quartermaster's Deal weighted by player skills.
        /// Prefers weapon types matching player's highest combat skills.
        /// </summary>
        private ItemObject SelectEquipmentBySkills(int tier)
        {
            try
            {
                var qmManager = QuartermasterManager.Instance;
                if (qmManager == null)
                {
                    ModLogger.Warn("PAY", "QM Deal: QuartermasterManager unavailable");
                    return null;
                }

                // Get player's formation and culture
                var formation = qmManager.GetPlayerFormationString();
                var culture = _enlistedLord?.Culture;

                if (culture == null || string.IsNullOrEmpty(formation))
                {
                    ModLogger.Warn("PAY", "QM Deal: Missing formation or culture");
                    return null;
                }

                // Get available equipment for tier
                var equipment = qmManager.GetAvailableEquipmentByFormation(formation, tier, culture);
                if (equipment == null || equipment.Count == 0)
                {
                    ModLogger.Warn("PAY", $"QM Deal: No equipment found for {formation} T{tier} {culture.Name}");
                    return null;
                }

                // Flatten all items into a single list with weights
                var weightedItems = new List<(ItemObject item, float weight)>();

                foreach (var slot in equipment)
                {
                    foreach (var item in slot.Value)
                    {
                        var weight = CalculateItemWeight(item);
                        if (weight > 0)
                        {
                            weightedItems.Add((item, weight));
                        }
                    }
                }

                if (weightedItems.Count == 0)
                {
                    ModLogger.Warn("PAY", "QM Deal: No weighted items available");
                    return null;
                }

                // Select item based on weighted random
                var totalWeight = weightedItems.Sum(x => x.weight);
                var roll = MBRandom.RandomFloat * totalWeight;
                var cumulative = 0f;

                foreach (var (item, weight) in weightedItems)
                {
                    cumulative += weight;
                    if (roll <= cumulative)
                    {
                        ModLogger.Info("PAY", $"QM Deal selected: {item.Name} (Weight={weight:0.0}/{totalWeight:0.0})");
                        return item;
                    }
                }

                // Fallback to first item
                return weightedItems[0].item;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("PAY", "Failed to select equipment by skills", ex);
                return null;
            }
        }

        /// <summary>
        /// Calculates weight for an item based on player skills.
        /// Higher skills in relevant categories = higher weight.
        /// </summary>
        private float CalculateItemWeight(ItemObject item)
        {
            if (item == null)
            {
                return 0f;
            }

            var hero = Hero.MainHero;
            var baseWeight = 1.0f;

            // Weapons: Weight by relevant combat skill
            if (item.PrimaryWeapon != null)
            {
                var weaponClass = item.PrimaryWeapon.WeaponClass;
                var skillValue = 0;

                switch (weaponClass)
                {
                    case WeaponClass.OneHandedSword:
                    case WeaponClass.OneHandedAxe:
                    case WeaponClass.Mace:
                    case WeaponClass.Dagger:
                        skillValue = hero.GetSkillValue(DefaultSkills.OneHanded);
                        break;

                    case WeaponClass.TwoHandedSword:
                    case WeaponClass.TwoHandedAxe:
                    case WeaponClass.TwoHandedMace:
                        skillValue = hero.GetSkillValue(DefaultSkills.TwoHanded);
                        break;

                    case WeaponClass.OneHandedPolearm:
                    case WeaponClass.TwoHandedPolearm:
                    case WeaponClass.LowGripPolearm:
                        skillValue = hero.GetSkillValue(DefaultSkills.Polearm);
                        break;

                    case WeaponClass.Bow:
                        skillValue = hero.GetSkillValue(DefaultSkills.Bow);
                        break;

                    case WeaponClass.Crossbow:
                        skillValue = hero.GetSkillValue(DefaultSkills.Crossbow);
                        break;

                    case WeaponClass.Javelin:
                    case WeaponClass.ThrowingAxe:
                    case WeaponClass.ThrowingKnife:
                    case WeaponClass.Stone:
                        skillValue = hero.GetSkillValue(DefaultSkills.Throwing);
                        break;
                }

                // Scale: 0 skill = 1.0x weight, 100 skill = 3.0x weight
                return baseWeight + (skillValue / 50f);
            }

            // Armor: Weight by Athletics (light armor) vs Riding (medium/heavy)
            if (item.ArmorComponent != null)
            {
                var armorValue = item.ArmorComponent.BodyArmor +
                                 item.ArmorComponent.ArmArmor +
                                 item.ArmorComponent.LegArmor;

                if (armorValue < 30)
                {
                    // Light armor - prefer if high Athletics
                    var athletics = hero.GetSkillValue(DefaultSkills.Athletics);
                    return baseWeight + (athletics / 50f);
                }
                else
                {
                    // Heavy armor - prefer if high Riding
                    var riding = hero.GetSkillValue(DefaultSkills.Riding);
                    return baseWeight + (riding / 50f);
                }
            }

            // Horses: Weight by Riding skill
            if (item.HorseComponent != null)
            {
                var riding = hero.GetSkillValue(DefaultSkills.Riding);
                return baseWeight + (riding / 50f);
            }

            return baseWeight;
        }

        #endregion

        #region Battle Loot Share

        /// <summary>
        /// Get gold share percentage based on tier per pay_system.md spec.
        /// T1-T6: Percentage of troop pool (after lord's 50% take)
        /// T7-T9: Percentage of total loot (before lord's take)
        /// </summary>
        private float GetLootSharePercent(int tier)
        {
            return tier switch
            {
                1 => 0.05f,  // 5%
                2 => 0.10f,  // 10%
                3 => 0.10f,  // 10%
                4 => 0.15f,  // 15%
                5 => 0.15f,  // 15%
                6 => 0.15f,  // 15%
                7 => 0.10f,  // 10% of total (commander)
                8 => 0.15f,  // 15% of total (commander)
                9 => 0.20f,  // 20% of total (commander)
                _ => 0.05f
            };
        }

        /// <summary>
        /// Calculate and award battle loot share based on battle outcome.
        /// Called from OnMapEventEnded when player participated in a victory.
        /// </summary>
        internal void AwardBattleLootShare(MapEvent mapEvent, bool playerWon)
        {
            try
            {
                if (!IsEnlisted || _enlistedLord == null)
                {
                    return;
                }

                if (!playerWon)
                {
                    ModLogger.Debug("PAY", "No loot share - battle lost");
                    return;
                }

                // Estimate battle loot value based on enemy casualties
                // Native doesn't expose loot value directly, so we estimate based on killed troops
                var enemySide = mapEvent.PlayerSide == BattleSideEnum.Defender
                    ? BattleSideEnum.Attacker
                    : BattleSideEnum.Defender;

                var enemyCasualties = mapEvent.GetMapEventSide(enemySide)?.TroopCasualties ?? 0;

                // Estimate loot value: ~20 denars per enemy killed (rough average)
                var estimatedTotalLoot = enemyCasualties * 20;

                if (estimatedTotalLoot <= 0)
                {
                    ModLogger.Debug("PAY", "No loot share - no enemy casualties");
                    return;
                }

                var tier = _enlistmentTier;
                var sharePercent = GetLootSharePercent(tier);

                // T1-T6 get share of troop pool (50% of total after lord's take)
                // T7-T9 get share of total loot (commander privilege)
                var lootPool = tier >= 7 ? estimatedTotalLoot : (int)(estimatedTotalLoot * 0.5f);
                var goldEarned = (int)(lootPool * sharePercent);

                // Battle bonuses: Victory bonus (5-20 based on battle size)
                var battleSizeBonus = Math.Min(20, 5 + (enemyCasualties / 10));

                var totalReward = goldEarned + battleSizeBonus;

                if (totalReward <= 0)
                {
                    return;
                }

                // Award gold
                GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, totalReward);

                ModLogger.Info("PAY", $"Battle loot share: {goldEarned} (share) + {battleSizeBonus} (victory) = {totalReward} gold (T{tier}, {enemyCasualties} casualties)");

                // Notify player
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Battle spoils: {totalReward} denars ({goldEarned} loot share + {battleSizeBonus} victory bonus)",
                    Colors.Green));
            }
            catch (Exception ex)
            {
                ModLogger.Caught("PAY", "Error awarding battle loot share", ex);
            }
        }

        #endregion

        #region PayTension Effects

        /// <summary>
        /// Get morale penalty based on PayTension level.
        /// Higher tension = lower morale as soldiers get desperate.
        /// </summary>
        public int GetPayTensionMoralePenalty()
        {
            if (_payTension < 20)
            {
                return 0;
            }
            if (_payTension < 40)
            {
                return -3;
            }
            if (_payTension < 60)
            {
                return -6;
            }
            if (_payTension < 80)
            {
                return -10;
            }
            return -15;
        }

        /// <summary>
        /// Get scrutiny incident chance modifier based on PayTension.
        /// Higher tension = more likely scrutiny problems.
        /// </summary>
        public float GetPayTensionDisciplineModifier()
        {
            if (_payTension < 40)
            {
                return 0f;
            }
            if (_payTension < 60)
            {
                return 0.05f;  // +5% chance
            }
            if (_payTension < 80)
            {
                return 0.10f;  // +10% chance
            }
            return 0.20f;  // +20% chance
        }

        /// <summary>
        /// Reduces PayTension by the specified amount.
        /// Used when the player helps the lord.
        /// </summary>
        /// <param name="amount">Amount to reduce (positive value)</param>
        public void ReducePayTension(int amount)
        {
            if (amount <= 0 || !IsEnlisted)
            {
                return;
            }

            var oldTension = _payTension;
            _payTension = Math.Max(0, _payTension - amount);

            ModLogger.Info("PAY", $"PayTension reduced: {oldTension} -> {_payTension} (by {amount})");

            if (_payTension < 40 && oldTension >= 40)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=hlm_tension_stabilized}The lord's financial situation has stabilized.").ToString(),
                    Colors.Green));
            }
        }

        /// <summary>
        /// Process free desertion when PayTension is 60+.
        /// Minimal penalties compared to normal desertion.
        /// </summary>
        /// <summary>
        /// Check for NPC soldier desertion due to high pay tension.
        /// Only applies to commanders (T7+) with retinue.
        /// Called daily when tension is 60+.
        /// </summary>
        internal void CheckNpcDesertionFromPayTension()
        {
            try
            {
                // Only commanders have troops to desert
                if (_enlistmentTier < 7 || _payTension < 60)
                {
                    return;
                }

                var mainParty = MobileParty.MainParty;
                if (mainParty?.MemberRoster == null || mainParty.MemberRoster.TotalManCount <= 1)
                {
                    return;
                }

                // Desertion chance scales with tension
                float desertionChance = _payTension switch
                {
                    >= 90 => 0.05f,  // 5% per soldier
                    >= 80 => 0.03f,  // 3% per soldier
                    >= 60 => 0.01f,  // 1% per soldier
                    _ => 0f
                };

                var roster = mainParty.MemberRoster;
                var desertersTotal = 0;

                // Check each troop type (not heroes)
                for (int i = roster.Count - 1; i >= 0; i--)
                {
                    var element = roster.GetElementCopyAtIndex(i);
                    if (element.Character?.IsHero == true)
                    {
                        continue;
                    }

                    var count = element.Number;
                    var deserters = 0;

                    for (int j = 0; j < count; j++)
                    {
                        if (MBRandom.RandomFloat < desertionChance)
                        {
                            deserters++;
                        }
                    }

                    if (deserters > 0)
                    {
                        _ = roster.AddToCounts(element.Character, -deserters);
                        desertersTotal += deserters;
                    }
                }

                if (desertersTotal > 0)
                {
                    ModLogger.Info("PAY", $"NPC desertion: {desertersTotal} soldiers left due to unpaid wages (tension={_payTension})");
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"{desertersTotal} soldier(s) deserted due to unpaid wages!",
                        Colors.Red));
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("PAY", "Error checking NPC desertion from pay tension", ex);
            }
        }

        #endregion

        internal bool TryGetProjectedDailyWage(out int wage)
        {
            wage = 0;

            if (!IsEnlisted || _enlistedLord?.IsAlive != true)
            {
                return false;
            }

            wage = CalculateDailyWage();
            return wage > 0;
        }

        /// <summary>
        ///     Gets a detailed breakdown of daily wage components for tooltip display.
        ///     Returns individual amounts for base pay, tier bonus, army bonus, duty bonus, etc.
        /// </summary>
        internal WageBreakdown GetWageBreakdown()
        {
            var breakdown = new WageBreakdown();

            if (!IsEnlisted || _enlistedLord?.IsAlive != true)
            {
                return breakdown;
            }

            try
            {
                var financeConfig = EnlistedConfig.LoadFinanceConfig();
                var formula = financeConfig.WageFormula;

                // Base soldier's pay
                breakdown.BasePay = formula.BaseWage;

                // Level bonus (experience as a fighter)
                breakdown.LevelBonus = Hero.MainHero.Level * formula.LevelMultiplier;

                // Rank/tier bonus (military rank pay increase)
                breakdown.TierBonus = _enlistmentTier * formula.TierMultiplier;

                // Service bonus (XP accumulated = seniority)
                var xpDivisor = formula.XpDivisor > 0 ? formula.XpDivisor : 200;
                breakdown.ServiceBonus = _enlistmentXP / xpDivisor;

                // Calculate subtotal before multipliers
                var subtotal = breakdown.BasePay + breakdown.LevelBonus + breakdown.TierBonus + breakdown.ServiceBonus;

                // Army campaign bonus (+20% when in active army)
                var lordParty = _enlistedLord?.PartyBelongedTo;
                bool inArmy = lordParty?.Army != null;
                if (inArmy)
                {
                    breakdown.ArmyBonus = (int)(subtotal * (formula.ArmyBonusMultiplier - 1.0f));
                    breakdown.IsInArmy = true;
                }


                // Calculate total (with cap)
                breakdown.Total = Math.Min(
                    breakdown.BasePay + breakdown.LevelBonus + breakdown.TierBonus +
                    breakdown.ServiceBonus + breakdown.ArmyBonus,
                    150);
                breakdown.Total = Math.Max(breakdown.Total, 24); // Minimum wage
            }
            catch (Exception ex)
            {
                if (!_loggedWageBreakdownFailure)
                {
                    _loggedWageBreakdownFailure = true;
                    ModLogger.Caught("Wage", "Failed to calculate wage breakdown (using fallback values)", ex);
                }
                breakdown.BasePay = 24;
                breakdown.Total = 24;
            }

            return breakdown;
        }


        /// <summary>
        ///     Validate loaded state and handle any corruption.
        /// </summary>
        private void ValidateLoadedState()
        {
            if (_baggageStash == null)
            {
                _baggageStash = new ItemRoster();
            }

            // Validate pending courier delivery state
            // If arrival time is set but no baggage pending, clear the time
            if (_baggageCourierArrivalTime != CampaignTime.Zero &&
                (_pendingCourierBaggage == null || _pendingCourierBaggage.Count == 0))
            {
                _baggageCourierArrivalTime = CampaignTime.Zero;
                _pendingCourierBaggage = null;
            }

            // If bag check was in progress when the game was saved, re-queue the event.
            // The event queue is transient (not saved), so we need to restore it.
            if (_bagCheckInProgress && !_bagCheckCompleted)
            {
                try
                {
                    var bagCheckEvent = EventCatalog.GetEvent("evt_baggage_stowage_first_enlistment");
                    if (bagCheckEvent != null)
                    {
                        var director = StoryDirector.Instance;
                        if (director != null)
                        {
                            ModLogger.Info("SaveLoad", "Restoring bag check event through StoryDirector after load");
                            director.EmitCandidate(new StoryCandidate
                            {
                                SourceId = "enlistment.bag_check",
                                CategoryId = "enlistment.bag_check",
                                ProposedTier = StoryTier.Modal,
                                SeverityHint = 0.90f,
                                Beats = { StoryBeat.OrderComplete },
                                Relevance = new RelevanceKey { TouchesEnlistedLord = true },
                                EmittedAt = CampaignTime.Now,
                                InteractiveEvent = bagCheckEvent,
                                RenderedTitle = bagCheckEvent.TitleFallback,
                                RenderedBody = bagCheckEvent.SetupFallback,
                                StoryKey = bagCheckEvent.Id,
                                ChainContinuation = true
                            });
                        }
                        else
                        {
                            var eventDelivery = EventDeliveryManager.Instance;
                            if (eventDelivery != null)
                            {
                                ModLogger.Info("SaveLoad", "Restoring bag check event (director unavailable) after load");
                                eventDelivery.QueueEvent(bagCheckEvent);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.Caught("SaveLoad", "Failed to restore bag check event", ex);
                    // Reset flags to allow retry via hourly tick
                    _bagCheckInProgress = false;
                }
            }

            // Ensure XP progression is valid
            if (_enlistmentTier < 1)
            {
                _enlistmentTier = 1;
            }

            var maxTier = ConfigurationManager.GetMaxTier();
            if (_enlistmentTier > maxTier)
            {
                _enlistmentTier = maxTier;
            }

            if (_enlistmentXP < 0)
            {
                _enlistmentXP = 0;
            }

            // Migrate tracking fields for existing saves.
            MigratePhase7TrackingFields();

            ModLogger.Info("SaveLoad", $"Validated enlistment state - Tier: {_enlistmentTier}, XP: {_enlistmentXP}");
        }

        /// <summary>
        /// Migrate promotion tracking fields for existing saves.
        /// Initializes days in rank, events completed, and battles survived with reasonable estimates.
        /// </summary>
        private void MigratePhase7TrackingFields()
        {
            if (!IsEnlisted)
            {
                return;
            }

            try
            {
                var migrated = false;

                // If lastPromotionDate is not set but player is T2+, estimate from enlistment date
                if (_lastPromotionDate == CampaignTime.Zero && _enlistmentTier > 1)
                {
                    // Assume player was promoted fairly recently (within tier * 7 days)
                    var estimatedDaysSincePromotion = Math.Max(7, (_enlistmentTier - 1) * 7);
                    _lastPromotionDate = CampaignTime.Now - CampaignTime.Days(estimatedDaysSincePromotion);
                    ModLogger.Info("SaveLoad", $"Migration: Estimated lastPromotionDate to {estimatedDaysSincePromotion} days ago");
                    migrated = true;
                }

                // If T1 and lastPromotionDate not set, use enlistment date
                if (_lastPromotionDate == CampaignTime.Zero && _enlistmentTier == 1)
                {
                    _lastPromotionDate = _enlistmentDate != CampaignTime.Zero ? _enlistmentDate : CampaignTime.Now;
                    ModLogger.Info("SaveLoad", "Migration: Set lastPromotionDate to enlistment date for T1 player");
                    migrated = true;
                }

                if (_battlesSurvived == 0 && _enlistmentTier > 1)
                {
                    _battlesSurvived = (_enlistmentTier - 1); // 1 battle per tier
                    ModLogger.Info("SaveLoad", $"Migration: Estimated battlesSurvived to {_battlesSurvived}");
                    migrated = true;
                }

                if (migrated)
                {
                    // Non-spammy summary marker for support: confirms a migration occurred in this session.
                    ModLogger.LogOnce("enlistment_migrate_phase7_tracking", "SaveLoad",
                        "Applied tracking migration for an existing save.");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("SaveLoad", "Tracking migration failed", ex);
            }
        }

        private void TryPauseForSettlementEntry()
        {
            try
            {
                if (Campaign.Current != null && Campaign.Current.TimeControlMode != CampaignTimeControlMode.Stop)
                {
                    Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Settlement", "Failed to pause time on settlement entry", ex);
            }
        }


        /// <summary>
        ///     Removes duplicate hero entries from player's roster that can occur after escape from captivity.
        ///     Also cleans up stale references to player companions in other parties (e.g., lord's party).
        /// </summary>
        private void DeduplicateRosterHeroes()
        {
            try
            {
                var main = MobileParty.MainParty;
                if (main == null)
                {
                    return;
                }

                var mainRoster = main.MemberRoster;
                var duplicatesRemoved = 0;
                var staleRefsRemoved = 0;

                // Track heroes we've seen in main roster to detect duplicates
                var seenHeroes = new HashSet<CharacterObject>();
                var heroesToRemove = new List<(CharacterObject character, int excess)>();

                foreach (var troop in mainRoster.GetTroopRoster())
                {
                    if (!troop.Character.IsHero)
                    {
                        continue;
                    }

                    // Heroes should only appear once in roster (count = 1)
                    if (troop.Number > 1)
                    {
                        heroesToRemove.Add((troop.Character, troop.Number - 1));
                    }
                    else if (seenHeroes.Contains(troop.Character))
                    {
                        // Duplicate entry - shouldn't happen but clean up
                        heroesToRemove.Add((troop.Character, troop.Number));
                    }
                    else
                    {
                        _ = seenHeroes.Add(troop.Character);
                    }
                }

                // Remove duplicate hero entries
                foreach (var (character, excess) in heroesToRemove)
                {
                    _ = mainRoster.AddToCounts(character, -excess);
                    duplicatesRemoved += excess;
                    ModLogger.Warn("SaveLoad", $"Removed {excess} duplicate entry for hero {character.Name}");
                }

                // Clean up stale companion references in lord's party (if enlisted)
                if (_enlistedLord?.PartyBelongedTo != null)
                {
                    var lordRoster = _enlistedLord.PartyBelongedTo.MemberRoster;
                    var staleCompanions = new List<TroopRosterElement>();

                    foreach (var troop in lordRoster.GetTroopRoster())
                    {
                        if (!troop.Character.IsHero || troop.Character == CharacterObject.PlayerCharacter)
                        {
                            continue;
                        }

                        var hero = troop.Character.HeroObject;
                        if (hero != null && hero.Clan == Clan.PlayerClan)
                        {
                            // Companion is in lord's party - check if also in player's party (stale ref)
                            if (mainRoster.GetTroopCount(troop.Character) > 0)
                            {
                                staleCompanions.Add(troop);
                            }
                        }
                    }

                    foreach (var stale in staleCompanions)
                    {
                        _ = lordRoster.AddToCounts(stale.Character, -stale.Number);
                        staleRefsRemoved++;
                        ModLogger.Warn("SaveLoad", $"Removed stale companion ref {stale.Character.Name} from lord's party");
                    }
                }

                if (duplicatesRemoved > 0 || staleRefsRemoved > 0)
                {
                    ModLogger.Info("SaveLoad",
                        $"Roster cleanup: removed {duplicatesRemoved} duplicates, {staleRefsRemoved} stale refs");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("SaveLoad", "Error during roster deduplication", ex);
            }
        }

        /// <summary>
        ///     Restores proper party activity state after a save is loaded.
        ///     Called from the first campaign tick after loading, NOT from SyncData.
        ///     The game asserts !IsActive during save load, so we must defer this.
        /// </summary>
        private void RestorePartyStateAfterLoad()
        {
            try
            {
                var main = MobileParty.MainParty;
                if (main == null)
                {
                    ModLogger.Debug("SaveLoad", "RestorePartyStateAfterLoad: MainParty is null, skipping");
                    return;
                }

                if (!IsEnlisted && !Hero.MainHero.IsPrisoner)
                {
                    // Non-enlisted players should be active and visible
                    main.IsActive = true;
                    main.IsVisible = true;
                    ModLogger.Info("SaveLoad", "Post-load: Party activated - not enlisted");
                }
                else if (IsEnlisted)
                {
                    // Enlisted players: check if lord is in battle
                    ModLogger.Info("SaveLoad",
                        $"Post-load: Restoring enlisted state (Lord: {_enlistedLord?.Name}, OnLeave: {_isOnLeave})");

                    // Re-apply ship protection after loading a save while enlisted
                    // This ensures ships remain invulnerable even if the flag wasn't persisted
                    SetPlayerShipsInvulnerable();

                    // Ensure the party can join battles when the lord fights
                    TrySetShouldJoinPlayerBattles(main, true);

                    // Check if we're loading into a save where a battle is already in progress
                    if (_enlistedLord?.PartyBelongedTo != null)
                    {
                        var lordParty = _enlistedLord.PartyBelongedTo;
                        var lordArmy = lordParty.Army;

                        bool lordInBattle = lordParty.Party.MapEvent != null;
                        bool armyInBattle = lordArmy?.LeaderParty?.Party.MapEvent != null;

                        // CRITICAL: Don't override reserve mode settings
                        // If player is waiting in reserve, keep them inactive and out of the MapEvent
                        if (EnlistedEncounterBehavior.IsWaitingInReserve)
                        {
                            ModLogger.Info("SaveLoad",
                                "Post-load: Player is in reserve mode - keeping party inactive");
                            main.IsActive = false;
                            main.IsVisible = false;
                            main.MapEventSide = null;
                        }
                        else if (lordInBattle || armyInBattle)
                        {
                            ModLogger.Info("SaveLoad",
                                $"Post-load: Loaded into active battle! Lord battle: {lordInBattle}, Army battle: {armyInBattle}");

                            main.IsActive = true;
                            main.IsVisible = true;
                        }
                        else
                        {
                            // Not in battle - keep hidden but active for escort AI
                            main.IsActive = true;
                            main.IsVisible = false;

                            main.IgnoreByOtherPartiesTill(CampaignTime.Now + CampaignTime.Hours(1f));
                            main.SetMoveEscortParty(lordParty, MobileParty.NavigationType.Default, false);
                            ModLogger.Info("SaveLoad", "Post-load: Escort AI restored for enlisted player");
                        }
                    }
                    else
                    {
                        // No lord party - might be in grace period or error state
                        main.IsActive = true;
                        main.IsVisible = true;
                        ModLogger.Info("SaveLoad", "Post-load: Lord party not found, keeping player visible");
                    }

                    // At Tier 4+, ensure companions are with the player for the retinue system
                    // This handles saves where companions are stuck in lord's party from earlier enlistment
                    if (_enlistmentTier >= 4)
                    {
                        ReclaimCompanionsFromLord();
                    }

                    // Activate enlisted menu after state restoration (deferred to next frame)
                    // This ensures the player sees their status menu when loading a save while enlisted
                    NextFrameDispatcher.RunNextFrame(() =>
                    {
                        if (IsEnlisted && !_isOnLeave && !Hero.MainHero.IsPrisoner)
                        {
                            EnlistedMenuBehavior.SafeActivateEnlistedMenu();
                        }
                    });
                }

                if (_isOnLeave || IsInDesertionGracePeriod)
                {
                    EnsureGraceLordMarker(forceRefresh: true, "post_load");
                }
                else if (_lastGraceLordMarkerSnapshot.HasValue)
                {
                    RemoveGraceLordMarker();
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("SaveLoad", "Error in RestorePartyStateAfterLoad", ex);
            }
        }

        /// <summary>
        ///     Checks if the player has been a prisoner for too long while in enlistment grace period.
        ///     If captured > 3 days, we force an escape/release so the player can actually use their grace period.
        ///     NAVAL SAFETY: Does NOT force escape if the captor party is currently at sea.
        ///     The native EndCaptivityInternal only teleports the player if the captor is on land.
        ///     Forcing escape at sea would strand the player without a ship. We wait until the
        ///     captor returns to land before triggering the escape.
        /// </summary>
        private void CheckPlayerCaptivityDuration()
        {
            if (Hero.MainHero.IsPrisoner && IsInDesertionGracePeriod)
            {
                // Check duration
                var daysInCaptivity = (float)(CampaignTime.Now - Hero.MainHero.CaptivityStartTime).ToDays;

                // Log captivity status every 12 in-game hours (not every tick)
                if ((int)(daysInCaptivity * 2) != (int)((daysInCaptivity - 0.5f) * 2))
                {
                    LogCaptivityStatus($"CaptivityCheck-Day{daysInCaptivity:F1}", wasChange: false);
                }
                if (daysInCaptivity > 3f)
                {
                    // NAVAL SAFETY: Check if captor party is at sea
                    // If at sea, do NOT force escape - player would be stranded without a ship
                    // The native EndCaptivityInternal checks IsCurrentlyAtSea before teleporting,
                    // but if we trigger escape while at sea, player ends up at the captor's sea position
                    // with no way to move. Wait until captor reaches land.
                    var captorParty = Hero.MainHero.PartyBelongedToAsPrisoner?.MobileParty;
                    if (captorParty != null && captorParty.IsCurrentlyAtSea)
                    {
                        // Log only occasionally to avoid spam (check if we haven't logged recently)
                        ModLogger.Debug("EVENTSAFETY",
                            $"Player held prisoner for {daysInCaptivity:F1} days but captor is at sea - waiting for land before forcing escape");
                        return;
                    }

                    ModLogger.Info("EVENTSAFETY",
                        $"Player held prisoner for {daysInCaptivity:F1} days during enlistment grace period - forcing release");

                    // Attempt to force release/escape
                    // Use EndCaptivityAction via reflection since it's internal or we just use the public static ApplyByEscape
                    // Public API: EndCaptivityAction.ApplyByEscape(Hero character, Hero facilitator = null)
                    try
                    {
                        // TaleWorlds.CampaignSystem.Actions.EndCaptivityAction
                        var endCaptivityType = typeof(EndCaptivityAction);
                        var applyMethod = endCaptivityType.GetMethod("ApplyByEscape",
                            BindingFlags.Static | BindingFlags.Public);

                        if (applyMethod != null)
                        {
                            _ = applyMethod.Invoke(null, new object[] { Hero.MainHero, null });
                            InformationManager.DisplayMessage(new InformationMessage(
                                "You have managed to escape captivity! Return to your kingdom quickly!"));
                        }
                        else
                        {
                            // Fallback if method not found (unlikely)
                            ModLogger.Expected("EVENTSAFETY", "reflect_end_captivity_missing", "Could not find EndCaptivityAction.ApplyByEscape");
                        }
                    }
                    catch (Exception ex)
                    {
                        ModLogger.Caught("EVENTSAFETY", "Error forcing player escape", ex);
                    }
                }
            }
        }

        /// <summary>
        ///     Checks if the player's leave has expired and applies desertion penalties if so.
        ///     Also displays a daily reminder of remaining leave days.
        ///     Leave duration is configurable via enlisted_config.json gameplay.leave_max_days.
        /// </summary>
        private void CheckLeaveExpiration()
        {
            // Only check if actually on leave with a valid start date
            if (!_isOnLeave || _leaveStartDate == CampaignTime.Zero || _enlistedLord == null)
            {
                return;
            }

            var daysOnLeave = (CampaignTime.Now - _leaveStartDate).ToDays;
            var maxLeaveDays = EnlistedConfig.LoadGameplayConfig().LeaveMaxDays;
            var remainingDays = maxLeaveDays - (int)daysOnLeave;

            if (daysOnLeave > maxLeaveDays)
            {
                // Get the kingdom for desertion penalties
                var lordKingdom = _enlistedLord.MapFaction as Kingdom;

                // BUG FIX: Check if player has become a VASSAL of the same faction
                // Only vassals get honorable discharge - they elevated their service.
                // Mercenaries who didn't return to the army are still deserters.
                var playerClan = Clan.PlayerClan;
                if (playerClan?.Kingdom != null && lordKingdom != null &&
                    playerClan.Kingdom == lordKingdom && !playerClan.IsUnderMercenaryService)
                {
                    ModLogger.Info("Leave",
                        $"Leave expired but player is now a vassal of {lordKingdom.Name} - clearing leave without penalties");

                    // Clear leave state
                    _isOnLeave = false;
                    _leaveStartDate = CampaignTime.Zero;

                    // Fire event for other mods
                    OnLeaveEnded?.Invoke();

                    // End enlistment cleanly since player is now a vassal
                    StopEnlist("Player became vassal of same faction", isHonorableDischarge: true);

                    var vassalMessage =
                        new TextObject(
                            "{=Enlisted_Message_LeaveExpiredVassal}Your leave has expired, but as a member of {KINGDOM}, your service ends honorably.");
                    _ = vassalMessage.SetTextVariable("KINGDOM", lordKingdom.Name);
                    InformationManager.DisplayMessage(new InformationMessage(vassalMessage.ToString()));
                    return;
                }

                ModLogger.Info("Leave", $"Leave expired after {daysOnLeave:F1} days - applying desertion penalties");

                // Clear leave state first
                _isOnLeave = false;
                _leaveStartDate = CampaignTime.Zero;

                // Apply desertion penalties if player was in a kingdom
                if (lordKingdom != null)
                {
                    // Set pending desertion kingdom so ApplyDesertionPenalties knows which kingdom to penalize
                    _pendingDesertionKingdom = lordKingdom;
                    ApplyDesertionPenalties();
                }

                // Fire event for other mods (before StopEnlist clears state)
                OnLeaveEnded?.Invoke();

                StopEnlist("Leave expired - desertion");

                var message =
                    new TextObject(
                        "{=Enlisted_Message_LeaveExpiredDeserter}Your leave has expired. You have been branded a deserter.");
                InformationManager.DisplayMessage(new InformationMessage(message.ToString()));
            }
            else if (remainingDays <= 7 && remainingDays > 0)
            {
                // Daily warning when leave is running out
                var message =
                    new TextObject("{=Enlisted_Message_LeaveRemaining}Leave: {DAYS} days remaining before desertion.");
                _ = message.SetTextVariable("DAYS", remainingDays);
                InformationManager.DisplayMessage(new InformationMessage(message.ToString()));
            }
        }

        /// <summary>
        ///     Real-time tick handler that runs every game frame while the player is enlisted.
        ///     Manages party state (active/inactive), battle participation, army membership,
        ///     and position following in real-time to ensure smooth gameplay.
        ///     This method is throttled to run every 100ms to prevent overwhelming the game's
        ///     rendering system with too-frequent state changes.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last frame, in seconds. Must be positive.</param>
        private void OnRealtimeTick(float deltaTime)
        {
            // CRITICAL: Skip during character creation when campaign isn't initialized
            // Accessing Hero.MainHero or MobileParty.MainParty during character creation throws exceptions
            if (!CampaignSafetyGuard.IsCampaignReady)
            {
                return;
            }

            SyncActivationState("realtime_tick");

            // When inactive, do nothing at all (mod effectively disabled).
            if (!EnlistedActivation.EnsureActive())
            {
                return;
            }

            // Wait for game initialization to complete before modifying party state
            // This prevents assertion failures during save loading when IsActive must be false
            if (_initializationTicksRemaining > 0)
            {
                _initializationTicksRemaining--;
                return;
            }

            // Handle party state initialization after the startup delay
            // For loaded games: restore after SyncData sets _needsPostLoadStateRestore
            // For new games: just enable modifications once campaign is ready
            if (!_isPartyStateInitialized)
            {
                if (_needsPostLoadStateRestore)
                {
                    // Loaded game - do the full restoration
                    _needsPostLoadStateRestore = false;
                    RestorePartyStateAfterLoad();
                    _isPartyStateInitialized = true;
                    ModLogger.Info("SaveLoad", "Loaded game: Party state initialized after restore");
                }
                else
                {
                    // New game or already restored - just enable modifications
                    _isPartyStateInitialized = true;
                    ModLogger.Info("SaveLoad", "New game: Party state initialized");
                }
            }

            // CRITICAL: Ensure non-enlisted players always stay visible and active
            // Native game systems might change visibility (e.g., night events or scripted encounters)
            // This check runs BEFORE throttling to ensure it's checked every frame
            // We need to enforce visibility for non-enlisted players during gameplay
            if (!IsEnlisted)
            {
                var mainParty = CampaignSafetyGuard.SafeMainParty;
                var mainHero = CampaignSafetyGuard.SafeMainHero;
                if (mainParty != null && mainHero != null)
                {
                    var partyState = mainParty.Party;
                    var inMapEvent = partyState?.MapEvent != null;
                    var isPrisoner = mainHero.IsPrisoner;
                    var inEncounter = Campaign.Current?.PlayerEncounter != null;
                    var isActive = mainParty.IsActive;
                    var isVisible = mainParty.IsVisible;

                    // CAPTIVITY TRACKING: Detect when player is released from captivity
                    // This helps debug issues where players get attacked immediately after release
                    if (_wasPrisonerLastTick && !isPrisoner)
                    {
                        // Player just released from captivity!
                        ModLogger.Info("Captivity",
                            $"Player RELEASED from captivity - IsActive:{isActive}, IsVisible:{isVisible}, InMapEvent:{inMapEvent}, InEncounter:{inEncounter}, InGrace:{IsInDesertionGracePeriod}");

                        // Log full captivity status for diagnostics
                        LogCaptivityStatus("CaptivityReleased", wasChange: true);
                        LogEncounterStatus("CaptivityReleased");

                        // ALWAYS apply protection on captivity release - player shouldn't be attacked immediately
                        // This applies regardless of whether we're in grace period or not
                        // mainParty is guaranteed non-null here due to the outer null check
                        var protectionDuration = CampaignTime.Hours(12f);
                        var protectionUntil = CampaignTime.Now + protectionDuration;

                        // Native protection - prevents parties from targeting us
                        mainParty.IgnoreByOtherPartiesTill(protectionUntil);

                        // Mod-level protection - our EncounterSuppressionPatch will block encounters
                        _graceProtectionEnds = protectionUntil;

                        ModLogger.Info("Captivity",
                            $"Applied 1-day protection after captivity release (until {protectionUntil})");
                    }
                    else if (!_wasPrisonerLastTick && isPrisoner)
                    {
                        // Player just became a prisoner!
                        LogCaptivityStatus("CaptivityStarted", wasChange: true);
                        LogEncounterStatus("CaptivityStarted");
                    }
                    _wasPrisonerLastTick = isPrisoner;

                    // Only skip visibility enforcement while vanilla systems own the player state. We skip when the
                    // player is actively in a MapEvent (battle) or is a prisoner (captivity system owns the player).
                    // We do not skip just because PlayerEncounter.Current exists, since it can be stale after a battle.
                    if (inMapEvent || isPrisoner)
                    {
                        ModLogger.LogOnce(
                            "realtime_skip_vanilla_capture",
                            "Realtime",
                            $"Skipping visibility enforcement - vanilla owns player (MapEvent:{inMapEvent}, Prisoner:{isPrisoner})");
                        return;
                    }

                    // Enforce visibility and activity for non-enlisted players. This keeps the player visible even
                    // if native systems switch it off, and checks both IsActive and IsVisible to catch state changes.
                    bool needsActivation = !mainParty.IsActive;
                    bool needsVisibility = !mainParty.IsVisible;

                    if (needsActivation || needsVisibility)
                    {
                        // Log when we're fixing visibility/activity to help diagnose issues
                        if (needsVisibility)
                        {
                            ModLogger.Info("Realtime",
                                "Enforcing visibility for non-enlisted player (was invisible - possibly native night visibility)");
                        }

                        if (needsActivation)
                        {
                            ModLogger.Info("Realtime", "Enforcing activity for non-enlisted player (was inactive)");
                        }

                        mainParty.IsActive = true;
                        mainParty.IsVisible = true;
                    }
                }

                return; // Skip enlistment logic for non-enlisted players
            }

            // Validate that deltaTime is positive to prevent zero-delta-time updates
            // Zero-delta-time updates can cause assertion failures in the rendering system
            if (deltaTime <= 0)
            {
                return;
            }

            // Throttle updates to prevent overwhelming the game's rendering system
            // Updates are limited to once every 100 milliseconds (0.1 seconds)
            // This prevents too-frequent state changes that can cause assertion failures
            var intervalMilliseconds = (int)(_realtimeUpdateIntervalSeconds * 1000);
            if (CampaignTime.Now - _lastRealtimeUpdate < CampaignTime.Milliseconds(intervalMilliseconds))
            {
                return;
            }

            _lastRealtimeUpdate = CampaignTime.Now;

            var main = MobileParty.MainParty;
            if (main == null)
            {
                return;
            }

            // If the player is inside any settlement, let native menus handle state.
            // Keep the party active/visible temporarily for UI, but skip enlistment logic.
            if (main.CurrentSettlement != null)
            {
                if (!main.IsActive)
                {
                    main.IsActive = true;
                }

                return;
            }

            // Skip all enlistment logic when on leave - let vanilla behavior take over
            if (_isOnLeave)
            {
                // Ensure vanilla state when on leave
                if (!main.IsActive && !Hero.MainHero.IsPrisoner)
                {
                    main.IsActive = true;
                    main.IsVisible = true;
                }

                return;
            }

            if (IsEnlisted && _enlistedLord != null)
            {
                // Get the lord's party to check battle and army status
                var lordParty = _enlistedLord.PartyBelongedTo;
                if (lordParty != null)
                {
                    // Check battle and siege state for both lord and player
                    // The MapEvent property exists on Party, not directly on MobileParty
                    // This is the correct API structure for checking battle state
                    var mainParty = MobileParty.MainParty;
                    bool lordHasMapEvent = lordParty.Party.MapEvent != null;
                    bool playerHasMapEvent = mainParty?.Party.MapEvent != null;
                    bool playerInArmy = mainParty?.Army != null;
                    bool lordInArmy = lordParty.Army != null;
                    var lordInSiege = IsPartyInActiveSiege(lordParty);
                    var playerInSiege = IsPartyInActiveSiege(mainParty);

                    // Clear BesiegerCamp when not in active siege.
                    // Keeping it during a real siege is required so the player can engage with the siege menus.
                    if (!playerInSiege && mainParty != null && mainParty.BesiegerCamp != null)
                    {
                        mainParty.BesiegerCamp = null;
                        ModLogger.Info("Siege",
                            "Cleared stale player besieger camp reference (no active siege detected)");
                    }

                    // Ensure the player is in the lord's army when the lord is in an army
                    // Join seamlessly when the LORD physically merges with the army (not when first assigned)
                    // This prevents teleporting the player across the map - we wait for our lord to arrive first
                    if (lordInArmy && mainParty != null && (mainParty.Army == null || mainParty.Army != lordParty.Army))
                    {
                        var targetArmy = lordParty.Army;
                        var armyLeader = targetArmy?.LeaderParty;

                        if (targetArmy == null || armyLeader == null)
                        {
                            ModLogger.Debug("BATTLE",
                                "Lord army reference invalid (null leader) - skipping automatic join this tick");
                        }
                        else
                        {
                            // Check if LORD has physically merged with the army
                            // Lord is merged when: they ARE the leader, OR they're very close to the leader
                            bool lordIsArmyLeader = lordParty == armyLeader;
                            var lordDistanceToLeader = lordIsArmyLeader
                                ? 0f
                                : lordParty.GetPosition2D.Distance(armyLeader.GetPosition2D);
                            var lordHasMerged = lordIsArmyLeader || lordDistanceToLeader < 3.0f;

                            // Also allow immediate join for urgent battles if lord is reasonably close
                            var urgentBattleNeed = (lordHasMapEvent || lordInSiege) && lordDistanceToLeader < 50.0f;

                            if (lordHasMerged || urgentBattleNeed)
                            {
                                try
                                {
                                    // CRITICAL: Set Army property FIRST, then call AddPartyToMergedParties
                                    // Native code does it in this order - the UI update in AddPartyToMergedParties
                                    // reads MainParty.Army to show the Army HUD, so it must be set first
                                    mainParty.Army = targetArmy;
                                    targetArmy.AddPartyToMergedParties(mainParty);

                                    // If the army is currently besieging, align the player's besieger camp
                                    TrySyncBesiegerCamp(mainParty, lordParty);

                                    if (urgentBattleNeed && !lordHasMerged)
                                    {
                                        ModLogger.Info("BATTLE",
                                            $"URGENT: Joined army for active battle/siege (Army: {armyLeader?.LeaderHero?.Name})");
                                    }
                                    else
                                    {
                                        ModLogger.Info("BATTLE",
                                            $"Lord merged with army - player joining (Army: {armyLeader?.LeaderHero?.Name})");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    ModLogger.Caught("BATTLE", "Error joining lord's army", ex);
                                }
                            }
                        }
                    }

                    // NOTE: Removed spammy battle state logging that was causing infinite loop during sieges
                    // These logs were firing every tick, flooding the log and causing freezes

                    // Siege watchdog removed. Siege/encounter stability is handled by centralized cleanup
                    // in CleanupPostEncounterState() and by clearing stale BesiegerCamp when no siege is active.

                    // Handle battle participation when the lord enters battle but the player hasn't yet
                    // This ensures the player joins the battle even if they weren't initially collected
                    // CRITICAL: Skip if player is deliberately waiting in reserve - don't interrupt that menu
                    string currentMenuId = Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId;
                    var isWaitingInReserve = currentMenuId == "enlisted_battle_wait";

                    if (lordHasMapEvent && !playerHasMapEvent && !isWaitingInReserve)
                    {
                        // Only attempt to add the player to the army if they're not already in it
                        // If they're already in the correct army, the native system should collect them automatically
                        if (mainParty.Army == null || mainParty.Army != lordParty.Army)
                        {
                            ModLogger.Info("BATTLE", "Lord entered battle - ensuring player can participate");
                            ModLogger.Info("BATTLE",
                                $"Pre-state: Player Army={mainParty?.Army?.LeaderParty?.LeaderHero?.Name?.ToString() ?? "null"}");

                            try
                            {
                                // Don't clear menus if the lord is in battle or siege
                                // Clearing menus during these operations can cause assertion failures
                                if (InBattleOrSiege(lordParty))
                                {
                                    ModLogger.Info("BATTLE",
                                        "Lord in battle/siege - skipping menu clearing to prevent assertion failures");
                                }
                                else
                                {
                                    // Clear menus before army management to ensure clean state transitions
                                    // This prevents menu state conflicts during battle setup
                                    ModLogger.Info("BATTLE", "Clearing menus before army management");
                                    while (Campaign.Current.CurrentMenuContext != null)
                                    {
                                        GameMenu.ExitToLast();
                                    }
                                }

                                // Army joining is now handled in OnRealtimeTick when lord merges with army
                                // If already in lord's army, just ensure battle participation flags are set
                                if (lordParty.Army != null && mainParty.Army == lordParty.Army)
                                {
                                    ModLogger.Debug("BATTLE",
                                        "Already in lord's army - setting battle participation flags");
                                    mainParty.IsActive = true;
                                    mainParty.IsVisible = false;
                                    mainParty.IgnoreByOtherPartiesTill(CampaignTime.Now);
                                    lordParty.Party.SetAsCameraFollowParty();
                                    mainParty.ShouldJoinPlayerBattles = true;
                                }
                                else if (lordParty.Army == null)
                                {
                                    // Lord has no army but is in battle - create a PlayerEncounter to join directly
                                    // PlayerEncounter.Init() will automatically join the existing MapEvent
                                    ModLogger.Info("BATTLE",
                                        "Lord has no army - attempting direct PlayerEncounter join");

                                    var lordMapEvent = lordParty.Party.MapEvent;
                                    if (lordMapEvent != null)
                                    {
                                        // Determine which side the lord is on
                                        bool lordIsAttacker =
                                            lordMapEvent.AttackerSide.Parties.Any(p => p.Party == lordParty.Party);
                                        BattleSideEnum lordSide = lordIsAttacker
                                            ? BattleSideEnum.Attacker
                                            : BattleSideEnum.Defender;

                                        // Check if we can join on the lord's side using native faction checks
                                        bool canJoinNatively = lordMapEvent.CanPartyJoinBattle(mainParty.Party, lordSide);

                                        // Check if lord is in a minor/bandit faction (not a Kingdom)
                                        // Minor/bandit faction lords don't trigger the mercenary join logic, leaving the player's
                                        // faction state independent. This causes CanPartyJoinBattle to fail because the
                                        // player isn't formally "at war" with bandits/enemies.
                                        var mapFaction = _enlistedLord?.MapFaction;
                                        bool isNonKingdomFactionLord = mapFaction != null &&
                                                                       !(mapFaction is Kingdom) &&
                                                                       (mapFaction.IsMinorFaction || mapFaction.IsBanditFaction);

                                        // Determine if we should bypass the native faction check
                                        // Only bypass for minor/bandit faction lords where we KNOW the faction logic doesn't work
                                        bool shouldBypassFactionCheck = !canJoinNatively && isNonKingdomFactionLord;

                                        if (canJoinNatively)
                                        {
                                            ModLogger.Debug("BATTLE",
                                                $"Can join battle on lord's side ({lordSide}) - preparing for battle participation");
                                        }
                                        else if (shouldBypassFactionCheck)
                                        {
                                            // NON-KINGDOM FACTION FIX: Bypass the native faction check for minor/bandit faction lords.
                                            // CanPartyJoinBattle requires player.MapFaction.IsAtWarWith(enemy), but enlisted
                                            // players with non-Kingdom lords aren't joined to the faction (no Kingdom to join).
                                            // As an enlisted soldier, we logically belong to the lord's side regardless of
                                            // formal faction state. Skip MapEventSide (which crashes without faction compat)
                                            // but create PlayerEncounter so the hero can participate.
                                            ModLogger.Debug("BATTLE",
                                                $"Non-Kingdom faction lord battle - bypassing native faction check for {lordSide} side");
                                        }
                                        else
                                        {
                                            // Kingdom lord but faction check failed - this is unexpected
                                            // Log warning but don't force bypass - let native system handle it
                                            ModLogger.Warn("BATTLE",
                                                $"Faction check failed for Kingdom lord battle ({lordSide} side) - player may not be able to join. Please report this if battle participation fails.");
                                            // Don't return here - still try to set up PlayerEncounter as fallback
                                        }

                                        // Make the player active for the encounter but keep them INVISIBLE
                                        // CRITICAL: If IsVisible = true, the native system creates a "Help [Party]" encounter menu
                                        // instead of directly joining the lord's battle. Keeping invisible ensures seamless battle entry.
                                        mainParty.IsActive = true;
                                        mainParty.IsVisible = false;
                                        mainParty.ShouldJoinPlayerBattles = true;

                                        // Clear any ignore window so we can be collected
                                        mainParty.IgnoreByOtherPartiesTill(CampaignTime.Now);

                                        // NAVAL BATTLE FIX: Check if this is a naval battle
                                        // Naval battles require ships - enlisted players have no ships (they're passengers on lord's ship)
                                        // Skip direct MapEvent join for naval battles to prevent spawn crashes
                                        // The player will still participate via PlayerEncounter as crew member on lord's ship
                                        bool isNavalBattle = lordMapEvent.IsNavalMapEvent;

                                        // Determine if we should set MapEventSide to join the battle
                                        // NOTE: The old code avoided setting MapEventSide for parties with 0 troops,
                                        // fearing a crash in ApplySimulatedHitRewardToSelectedTroop during auto-sim.
                                        // However, this prevented enlisted players from joining small battles entirely!
                                        // The auto-sim crash only occurs if the player uses "Send Troops" with 0 troops,
                                        // which won't happen in practice - players will use "Attack" for manual combat.
                                        // By setting MapEventSide, the player properly joins the MapEvent and can participate.
                                        var targetSide = lordSide == BattleSideEnum.Attacker
                                            ? lordMapEvent.AttackerSide
                                            : lordMapEvent.DefenderSide;

                                        if (isNavalBattle)
                                        {
                                            // Naval battles - join MapEventSide so Init() works
                                            // NavalBattleShipAssignmentPatch assigns ship from lord's fleet (enlisted players have no ships)
                                            mainParty.Party.MapEventSide = targetSide;
                                            ModLogger.Info("Naval",
                                                $"Naval battle detected - joined MapEventSide on {lordSide} side");
                                        }
                                        else if (canJoinNatively || shouldBypassFactionCheck)
                                        {
                                            // Land battle - join the MapEvent on lord's side
                                            mainParty.Party.MapEventSide = targetSide;
                                            int partyTroopCount = mainParty.Party.NumberOfRegularMembers;
                                            ModLogger.Info("BATTLE",
                                                $"Joined MapEvent on {lordSide} side (troops: {partyTroopCount}, bypass: {shouldBypassFactionCheck})");
                                        }
                                        else
                                        {
                                            // Kingdom lord but faction check failed - try anyway, but warn
                                            mainParty.Party.MapEventSide = targetSide;
                                            ModLogger.Warn("BATTLE",
                                                $"Faction check failed but joining MapEvent anyway on {lordSide} side - report if battle doesn't work");
                                        }

                                        // CRITICAL: Create and initialize a PlayerEncounter to show encounter menu.
                                        // Now that MapEventSide is set, MobileParty.MainParty.MapEvent returns the battle.
                                        // The public static PlayerEncounter.Init() will properly initialize the encounter.
                                        // Guard flag prevents repeated creation during the same battle (causes loops).
                                        if (PlayerEncounter.Current == null && !_playerEncounterCreatedForBattle)
                                        {
                                            _playerEncounterCreatedForBattle = true;

                                            // Start the encounter, then Init() uses MainParty.MapEvent
                                            PlayerEncounter.Start();
                                            PlayerEncounter.Init();

                                            var attackerName = lordMapEvent.AttackerSide?.LeaderParty?.Name?.ToString() ?? "unknown";
                                            var defenderName = lordMapEvent.DefenderSide?.LeaderParty?.Name?.ToString() ?? "unknown";
                                            ModLogger.Info("BATTLE",
                                                $"Initialized PlayerEncounter (attacker: {attackerName}, defender: {defenderName})");
                                        }
                                        else if (_playerEncounterCreatedForBattle)
                                        {
                                            // Already created for this battle, don't spam logs
                                        }
                                    }
                                    else
                                    {
                                        ModLogger.Info("BATTLE",
                                            "Lord has MapEvent flag but MapEvent is null - cannot join directly");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                ModLogger.Caught("BATTLE", "Error in battle participation setup", ex);
                            }
                        }
                        else
                        {
                            // Player is already in the correct army - no action needed
                            // The native system should collect them into the battle automatically
                            ModLogger.Debug("BATTLE", "Player already in lord's army - no action needed");
                        }
                    }

                    // Check battle state for both the lord individually and their army
                    // The MapEvent property exists on Party, not directly on MobileParty
                    // This is the correct API structure for checking battle state
                    bool lordInBattle = lordParty.Party.MapEvent != null;
                    bool armyInBattle = lordParty.Army?.LeaderParty?.Party.MapEvent != null;
                    var anyBattleActive = lordInBattle || armyInBattle;

                    // Check if the player is already in a battle
                    bool playerInBattle = mainParty?.Party.MapEvent != null;

                    // Check for siege state (both BesiegedSettlement and SiegeEvent)
                    // During sieges, the player needs to be active/visible even before the assault starts
                    // to allow siege menus to appear

                    // Keep the player's party position matched to the lord's position
                    // This ensures the player follows the lord during travel
                    // Position is handled by Escort AI (SetMoveEscortParty)

                    // Determine if the player needs to be active for battle participation
                    // The player should be activated when a battle is starting but they haven't joined yet
                    // The native system collects nearby active parties when a battle starts
                    var playerNeedsToBeActiveForBattle = anyBattleActive && !playerInBattle;

                    // During sieges, keep the player active and visible so siege menus can appear
                    if (lordInSiege || playerInSiege)
                    {
                        // IMPORTANT: The native siege flow requires the player party to be active in order
                        // to participate in siege menus (lead assault / continue siege / join assault).
                        // We handle the *post-victory* menu loop by clearing stale BesiegerCamp after the siege ends,
                        // not by deactivating the party during siege preparation.
                        if (!mainParty.IsActive)
                        {
                            mainParty.IsActive = true;
                            ModLogger.Debug("Siege", "Activating player party during siege so native siege menus work");
                        }

                        // Player banner should stay hidden even during siege prep
                        mainParty.IsVisible = false;

                        // Army joining is now handled in OnRealtimeTick when lord merges
                        // If in lord's army, siege menus work automatically

                        // If the player enters a settlement while having an active PlayerEncounter, finish it immediately
                        // This prevents assertion failures that can occur when encounters persist after settlement entry
                        // Check both InsideSettlement and CurrentSettlement to ensure we catch all cases
                        if (PlayerEncounter.Current != null && !PlayerEncounter.InsideSettlement)
                        {
                            if (mainParty?.CurrentSettlement != null)
                            {
                                try
                                {
                                    ModLogger.Info("Siege",
                                        "Player entered settlement with active PlayerEncounter - finishing to prevent assertion");
                                    PlayerEncounter.Finish();
                                }
                                catch (Exception ex)
                                {
                                    ModLogger.Caught("Siege", "Error finishing PlayerEncounter when entering settlement", ex);
                                }
                            }
                        }

                        // CRITICAL: Do NOT create PlayerEncounter in realtime tick - let the native system handle it
                        // Creating encounters in realtime tick causes rapid loops and zero-delta-time assertion failures
                        // The OnMapEventStarted handler will create the encounter when the siege battle starts
                        // For siege menus before assault, the native system creates encounters naturally
                        // We only need to ensure the party is active/visible, which we already did above
                        // NOTE: Removed spammy debug log here that fired every tick
                    }
                    else if (!playerNeedsToBeActiveForBattle)
                    {
                        // No battle active, or player already in battle
                        if (playerInBattle)
                        {
                            // Player already in battle - keep invisible (banner should be hidden during enlisted service)
                            // Visibility only needed for battle UI menus before joining, not during active battle
                            mainParty.IsVisible = false;
                            ModLogger.Debug("BATTLE",
                                "Player already in battle - keeping invisible (enlisted service)");
                        }
                        else
                        {
                            // When the lord is in an army, the player must be active to be collected into battles
                            // The game's battle collection system checks for active parties before or during MapEvent creation
                            // If the player is inactive, they won't be included in the battle even if they're in the army
                            // Army membership prevents random encounters, but the player needs to be active for battle collection
                            // CRITICAL: Keep party INVISIBLE even when active - prevents icon rendering
                            // Making it visible causes party icon to appear with troop count and map marker issues
                            // Battle collection uses IsActive, not IsVisible - visibility only needed for UI menus (already handled separately)
                            if (lordInArmy && mainParty.Army == lordParty.Army)
                            {
                                // Lord is in an army and the player is in the same army
                                // Keep the player active for battle collection and escort AI, but INVISIBLE
                                if (!mainParty.IsActive)
                                {
                                    mainParty.IsActive = true;
                                    TrySetShouldJoinPlayerBattles(mainParty, true);
                                    ModLogger.Debug("BATTLE", "Activated player party for army following");
                                }

                                // Check if lord is at sea - use direct position sync instead of escort AI
                                // Naval War Expansion: Escort AI doesn't work when target is at sea without ships
                                if (!TrySyncNavalPosition(mainParty, lordParty))
                                {
                                    // Lord is on land - sync player's sea state if they're still at sea (disembark)
                                    if (mainParty.IsCurrentlyAtSea && !lordParty.IsCurrentlyAtSea)
                                    {
                                        mainParty.IsCurrentlyAtSea = false;
                                        mainParty.Position = lordParty.Position;
                                        ModLogger.Debug("Naval",
                                            "Synced player sea state to land (disembarked with lord in army)");
                                    }

                                    // Lord is on land - use normal escort AI
                                    mainParty.SetMoveEscortParty(lordParty, MobileParty.NavigationType.Default, false);
                                    lordParty.Party.SetAsCameraFollowParty();

                                    // FIX: Aggressive position sync when player is lagging behind
                                    // This prevents the issue where the player spawns behind their formation in battle
                                    // because their map party was slightly behind the lord when battle started
                                    TryAggressivePositionSync(mainParty, lordParty, "army");
                                }

                                // Keep invisible to prevent banner/icon appearing
                                mainParty.IsVisible = false;
                            }
                            else
                            {
                                // Lord is not in an army - keep party active for escort AI to work
                                // Use IgnoreByOtherPartiesTill to prevent random encounters instead of IsActive = false
                                mainParty.IsVisible = false;

                                // CRITICAL: Keep party ACTIVE so escort AI works for following
                                if (!mainParty.IsActive)
                                {
                                    mainParty.IsActive = true;
                                }

                                // Refresh ignore window to prevent random encounters
                                mainParty.IgnoreByOtherPartiesTill(CampaignTime.Now + CampaignTime.Hours(1f));

                                // Check if lord is at sea - use direct position sync instead of escort AI
                                // Naval War Expansion: Escort AI doesn't work when target is at sea without ships
                                if (!TrySyncNavalPosition(mainParty, lordParty))
                                {
                                    // Lord is on land - sync player's sea state if they're still at sea (disembark)
                                    if (mainParty.IsCurrentlyAtSea && !lordParty.IsCurrentlyAtSea)
                                    {
                                        mainParty.IsCurrentlyAtSea = false;
                                        mainParty.Position = lordParty.Position;
                                        ModLogger.Debug("Naval",
                                            "Synced player sea state to land (disembarked with lord)");
                                    }

                                    // Lord is on land - use normal escort AI
                                    mainParty.SetMoveEscortParty(lordParty, MobileParty.NavigationType.Default, false);
                                    lordParty.Party.SetAsCameraFollowParty();

                                    // FIX: Aggressive position sync when player is lagging behind
                                    // This prevents the issue where the player spawns behind their formation in battle
                                    // because their map party was slightly behind the lord when battle started
                                    TryAggressivePositionSync(mainParty, lordParty, "solo");
                                }
                            }
                        }
                    }
                    else
                    {
                        // Lord is in battle but the player hasn't joined yet
                        // Army joining is now handled when lord merges - just activate for battle collection

                        // Only activate if the party doesn't already have a MapEvent (safe to activate)
                        // The MapEvent property exists on Party, not directly on MobileParty
                        // This is the correct API structure for checking battle state
                        if (mainParty.Party.MapEvent == null && !mainParty.IsActive)
                        {
                            mainParty.IsActive = true;
                            // Ensure battle participation flags are set so the player can join battles
                            // Follow the LORD's party with camera to prevent pausing when lord enters battle
                            lordParty.Party.SetAsCameraFollowParty();
                            TrySetShouldJoinPlayerBattles(mainParty, true);
                            ModLogger.Debug("BATTLE",
                                $"Activated party for battle collection (lord in battle, player in army: {playerInArmy})");
                        }

                        // CRITICAL: After battle UI is shown, reset visibility to false if player joined the battle
                        // This ensures banner doesn't remain visible after battle participation
                        if (playerInBattle)
                        {
                            mainParty.IsVisible = false;
                            ModLogger.Debug("BATTLE", "Reset visibility to false - player joined battle");
                        }
                    }

                    // Configure party ignoring behavior to prevent unwanted encounters
                    // The party should ignore other parties when not in battle, but allow encounters
                    // when a battle involving the lord is active (so the player can be collected)
                    // The MapEvent property exists on Party, not directly on MobileParty
                    var lordHasEvent = lordParty?.Party.MapEvent != null ||
                                       lordParty?.Army?.LeaderParty?.Party.MapEvent != null;
                    if (!lordHasEvent)
                    {
                        // No battle active - ignore other parties for 0.5 hours to prevent random encounters
                        mainParty.IgnoreByOtherPartiesTill(CampaignTime.Now + CampaignTime.Hours(0.5f));
                        // NOTE: Removed spammy debug log here that fired every tick
                    }
                    else
                    {
                        // Battle active - don't ignore parties so the player can be collected into the battle
                        ModLogger.Debug("Realtime",
                            "NOT ignoring parties - lord battle active, allowing encounter collection");
                    }
                }
            }
        }

        private void TrySyncBesiegerCamp(MobileParty mainParty, MobileParty lordParty)
        {
            try
            {
                // CRITICAL FIX: Do NOT clear BesiegerCamp for enlisted players!
                //
                // The previous code cleared BesiegerCamp because it was believed that
                // GetGenericStateMenu() returning "menu_siege_strategies" would show
                // "commander-level siege menus". This is INCORRECT.
                //
                // Native's menu_siege_strategies shows different options depending on
                // whether you're the siege leader or a subordinate. The menu OPTIONS
                // filter themselves - for example, "Leave Army" only shows for non-leaders.
                //
                // By clearing BesiegerCamp, we were:
                // 1. Removing the player from BesiegerCamp._besiegerParties
                // 2. Breaking GetInvolvedPartiesForEventType() enumeration
                // 3. Causing IsMainPartyAmongParties() to return false
                // 4. Breaking native's post-siege victory menu flow (SiegeAftermathCampaignBehavior)
                // 5. Preventing the player from being collected into siege MapEvents
                //
                // The fix: Let BesiegerCamp be set normally via AttachedTo setter, and
                // sync it if needed. Native handles subordinate menus correctly.

                if (mainParty == null)
                {
                    return;
                }

                var siegeEvent = lordParty?.Party?.SiegeEvent
                                 ?? lordParty?.BesiegedSettlement?.SiegeEvent
                                 ?? lordParty?.Army?.LeaderParty?.Party?.SiegeEvent;

                var targetCamp = siegeEvent?.BesiegerCamp ?? lordParty?.BesiegedSettlement?.SiegeEvent?.BesiegerCamp;

                if (targetCamp == null)
                {
                    // No active siege - clear BesiegerCamp if set (prevents stale state)
                    if (mainParty.BesiegerCamp != null)
                    {
                        ModLogger.Debug("BATTLE", "Clearing player besieger camp - no active siege event detected");
                        mainParty.BesiegerCamp = null;
                    }
                    return;
                }

                // Sync BesiegerCamp with lord's siege
                if (mainParty.BesiegerCamp == targetCamp)
                {
                    ModLogger.Debug("BATTLE",
                        $"Player besieger camp already synced ({targetCamp.SiegeEvent?.BesiegedSettlement?.Name?.ToString() ?? "unknown"})");
                    return;
                }

                // Set the BesiegerCamp - this triggers OnPartyJoinedSiegeInternal which adds us to _besiegerParties
                mainParty.BesiegerCamp = targetCamp;

                // Log comprehensive siege integration status
                LogSiegeIntegrationStatus(mainParty, lordParty, targetCamp, "TrySyncBesiegerCamp");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("BATTLE", "Failed to sync besieger camp", ex);
            }
        }

        /// <summary>
        /// Logs the current siege integration status to verify the player is properly integrated.
        /// This is critical for diagnosing post-siege menu issues.
        /// </summary>
        private void LogSiegeIntegrationStatus(MobileParty mainParty, MobileParty lordParty, BesiegerCamp camp, string context)
        {
            try
            {
                _ = lordParty;
                var settlement = camp?.SiegeEvent?.BesiegedSettlement?.Name?.ToString() ?? "unknown";
                var army = mainParty?.Army;
                var armyLeader = army?.LeaderParty?.LeaderHero?.Name?.ToString() ?? "none";

                // Check if player is in Army.Parties (required for _wasPlayerArmyMember check)
                bool inArmyParties = army?.Parties?.Contains(mainParty) == true;

                // Check if player is in BesiegerCamp (required for MapEvent collection)
                bool inBesiegerParties = camp?.HasInvolvedPartyForEventType(mainParty?.Party,
                    MapEvent.BattleTypes.Siege) == true;

                // Check attachment status
                bool attachedToLeader = mainParty?.AttachedTo == army?.LeaderParty;

                // Check MapEvent status
                var mapEvent = mainParty?.Party?.MapEvent;
                bool inMapEvent = mapEvent != null;

                ModLogger.Info("SiegeIntegration",
                    $"[{context}] Siege at {settlement}: " +
                    $"InArmyParties={inArmyParties}, " +
                    $"InBesiegerParties={inBesiegerParties}, " +
                    $"AttachedToLeader={attachedToLeader}, " +
                    $"InMapEvent={inMapEvent}, " +
                    $"ArmyLeader={armyLeader}");

                // Warn if integration looks broken
                if (!inArmyParties && army != null)
                {
                    ModLogger.Warn("SiegeIntegration",
                        $"WARNING: Player not in Army.Parties! Native post-siege flow may fail.");
                }
                if (!inBesiegerParties && camp != null)
                {
                    ModLogger.Warn("SiegeIntegration",
                        $"WARNING: Player not in BesiegerCamp._besiegerParties! MapEvent collection may fail.");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Debug("SiegeIntegration", $"Error logging siege status: {ex.Message}");
            }
        }

        /// <summary>
        /// Logs the current battle integration status to verify the player is properly joined to battles.
        /// </summary>
        private void LogBattleIntegrationStatus(MobileParty mainParty, MobileParty lordParty, MapEvent mapEvent, string context)
        {
            try
            {
                _ = lordParty;
                if (mainParty == null || mapEvent == null)
                {
                    return;
                }

                var army = mainParty.Army;
                var armyLeader = army?.LeaderParty?.LeaderHero?.Name?.ToString() ?? "none";

                // Check if player is in Army.Parties
                bool inArmyParties = army?.Parties?.Contains(mainParty) == true;

                // Check if player is in the MapEvent
                bool inMapEvent = mainParty.Party?.MapEvent == mapEvent;
                var playerSide = mainParty.Party?.MapEventSide;
                string sideLabel = playerSide == mapEvent.AttackerSide ? "Attacker"
                    : playerSide == mapEvent.DefenderSide ? "Defender"
                    : "None";

                // Check if player is in InvolvedParties
                bool inInvolvedParties = mapEvent.InvolvedParties?.Contains(mainParty.Party) == true;

                // Check attachment status
                bool attachedToLeader = mainParty.AttachedTo == army?.LeaderParty;

                // Check PlayerEncounter status
                bool hasEncounter = PlayerEncounter.Current != null;

                // Battle type info
                string battleType = mapEvent.IsSiegeAssault ? "SiegeAssault"
                    : mapEvent.IsSallyOut ? "SallyOut"
                    : mapEvent.IsNavalMapEvent ? "Naval"
                    : mapEvent.IsRaid ? "Raid"
                    : "Field";

                ModLogger.Info("BattleIntegration",
                    $"[{context}] {battleType} battle: " +
                    $"InArmyParties={inArmyParties}, " +
                    $"InMapEvent={inMapEvent}, " +
                    $"Side={sideLabel}, " +
                    $"InInvolvedParties={inInvolvedParties}, " +
                    $"HasEncounter={hasEncounter}, " +
                    $"AttachedToLeader={attachedToLeader}");

                // Warn if integration looks broken
                if (!inMapEvent && mapEvent.State != MapEventState.WaitingRemoval)
                {
                    ModLogger.Warn("BattleIntegration",
                        "WARNING: Player not in MapEvent! Battle participation may fail.");
                }
                if (!inInvolvedParties && mapEvent.State != MapEventState.WaitingRemoval)
                {
                    ModLogger.Warn("BattleIntegration",
                        "WARNING: Player not in InvolvedParties! XP/loot may not be awarded.");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Debug("BattleIntegration", $"Error logging battle status: {ex.Message}");
            }
        }

        /// <summary>
        /// Logs the current encounter state for debugging encounter flow issues.
        /// </summary>
        private void LogEncounterStatus(string context)
        {
            try
            {
                var mainParty = MobileParty.MainParty;
                var mainHero = Hero.MainHero;

                bool hasEncounter = PlayerEncounter.Current != null;
                bool insideSettlement = PlayerEncounter.InsideSettlement;
                bool leaveEncounter = hasEncounter && PlayerEncounter.LeaveEncounter;
                bool isWaiting = hasEncounter && PlayerEncounter.Current.IsPlayerWaiting;

                var mapEvent = mainParty?.Party?.MapEvent;
                bool inMapEvent = mapEvent != null;
                string mapEventState = mapEvent?.State.ToString() ?? "None";
                bool hasWinner = mapEvent?.HasWinner == true;

                bool isPrisoner = mainHero?.IsPrisoner == true;
                bool isActive = mainParty?.IsActive == true;
                bool isVisible = mainParty?.IsVisible == true;

                string currentMenu = Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId ?? "none";

                ModLogger.Info("EncounterStatus",
                    $"[{context}] HasEncounter={hasEncounter}, " +
                    $"InsideSettlement={insideSettlement}, " +
                    $"LeaveEncounter={leaveEncounter}, " +
                    $"IsWaiting={isWaiting}, " +
                    $"InMapEvent={inMapEvent}, " +
                    $"MapEventState={mapEventState}, " +
                    $"HasWinner={hasWinner}, " +
                    $"IsPrisoner={isPrisoner}, " +
                    $"IsActive={isActive}, " +
                    $"IsVisible={isVisible}, " +
                    $"Menu={currentMenu}");
            }
            catch (Exception ex)
            {
                ModLogger.Debug("EncounterStatus", $"Error logging encounter status: {ex.Message}");
            }
        }

        /// <summary>
        /// Logs captivity state transitions for debugging captivity flow issues.
        /// </summary>
        private void LogCaptivityStatus(string context, bool wasChange = false)
        {
            try
            {
                var mainHero = Hero.MainHero;
                var mainParty = MobileParty.MainParty;

                if (mainHero == null)
                {
                    return;
                }

                bool isPrisoner = mainHero.IsPrisoner;
                var captorParty = mainHero.PartyBelongedToAsPrisoner?.MobileParty;
                string captorName = captorParty?.LeaderHero?.Name?.ToString() ?? captorParty?.Name?.ToString() ?? "none";
                bool captorAtSea = captorParty?.IsCurrentlyAtSea == true;

                float daysCaptive = isPrisoner
                    ? (float)(CampaignTime.Now - mainHero.CaptivityStartTime).ToDays
                    : 0f;

                bool inGracePeriod = IsInDesertionGracePeriod;
                bool pendingCleanup = _playerCaptureCleanupScheduled;

                bool isActive = mainParty?.IsActive == true;
                bool isVisible = mainParty?.IsVisible == true;

                string currentMenu = Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId ?? "none";

                var logMethod = wasChange ? (Action<string, string>)ModLogger.Info : ModLogger.Debug;
                logMethod("CaptivityStatus",
                    $"[{context}] IsPrisoner={isPrisoner}, " +
                    $"Captor={captorName}, " +
                    $"CaptorAtSea={captorAtSea}, " +
                    $"DaysCaptive={daysCaptive:F1}, " +
                    $"InGracePeriod={inGracePeriod}, " +
                    $"PendingCleanup={pendingCleanup}, " +
                    $"IsActive={isActive}, " +
                    $"IsVisible={isVisible}, " +
                    $"Menu={currentMenu}");

                // Warn on concerning states
                if (isPrisoner && isActive)
                {
                    ModLogger.Warn("CaptivityStatus",
                        "WARNING: Player is prisoner but party is Active! Native captivity may not own state properly.");
                }
                if (isPrisoner && captorAtSea && inGracePeriod && daysCaptive > 3f)
                {
                    ModLogger.Warn("CaptivityStatus",
                        $"WARNING: Player captive for {daysCaptive:F1} days at sea during grace period - waiting for land.");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Debug("CaptivityStatus", $"Error logging captivity status: {ex.Message}");
            }
        }

        /// <summary>
        ///     Aggressively syncs the player's position with the lord's position to ensure the player
        ///     is always at the battle location when combat starts.
        ///     This fixes the issue where players spawn behind their formation in battle because
        ///     their map party was slightly behind the lord when the battle MapEvent was created.
        ///     The sync happens when:
        ///     - Player is more than 3 units behind the lord (normal lag)
        ///     - Lord is approaching an enemy party (pre-battle sync)
        ///     - Lord's army is about to engage (army battle sync)
        /// </summary>
        /// <param name="mainParty">The player's party.</param>
        /// <param name="lordParty">The lord's party that the player is following.</param>
        /// <param name="context">Context string for logging (e.g., "army", "solo").</param>
        private void TryAggressivePositionSync(MobileParty mainParty, MobileParty lordParty, string context)
        {
            try
            {
                if (mainParty == null || lordParty == null)
                {
                    return;
                }

                // Skip if player is in a settlement (handled separately)
                if (mainParty.CurrentSettlement != null)
                {
                    return;
                }

                var playerPos = mainParty.GetPosition2D;
                var lordPos = lordParty.GetPosition2D;
                var distance = playerPos.Distance(lordPos);

                // Always sync if player is more than 3 units behind
                // This is aggressive to ensure player is ALWAYS at lord's location for battle spawning
                const float MaxAllowedDistance = 3f;

                // Also check if lord is near an enemy (potential combat) - be even more aggressive
                var lordNearEnemy = IsLordNearEnemy(lordParty);
                var syncDistance = lordNearEnemy ? 1f : MaxAllowedDistance; // Even tighter sync near combat

                if (distance > syncDistance)
                {
                    mainParty.Position = lordParty.Position;

                    // Only log when the distance was significant (to avoid log spam)
                    if (distance > 5f || lordNearEnemy)
                    {
                        ModLogger.Debug("PositionSync",
                            $"[{context}] Synced player position to lord (was {distance:F1} units behind" +
                            (lordNearEnemy ? ", lord near enemy)" : ")"));
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("PositionSync", "Error in aggressive position sync", ex);
            }
        }

        /// <summary>
        ///     Checks if the lord's party is near an enemy party that could result in battle.
        ///     Used to trigger more aggressive position syncing before combat.
        /// </summary>
        /// <param name="lordParty">The lord's party to check.</param>
        /// <returns>True if the lord is near an enemy and combat is likely.</returns>
        private bool IsLordNearEnemy(MobileParty lordParty)
        {
            try
            {
                if (lordParty == null)
                {
                    return false;
                }

                // Check if lord is already in battle (most obvious case)
                if (lordParty.Party.MapEvent != null)
                {
                    return true;
                }

                // Check if lord's army is in battle
                if (lordParty.Army?.LeaderParty?.Party.MapEvent != null)
                {
                    return true;
                }

                // Check for nearby hostile parties using the lord's current target
                var targetParty = lordParty.TargetParty;
                if (targetParty != null)
                {
                    // Check if targeting a hostile party
                    bool isHostile = FactionManager.IsAtWarAgainstFaction(lordParty.MapFaction, targetParty.MapFaction);
                    if (isHostile)
                    {
                        float distanceToTarget = lordParty.GetPosition2D.Distance(targetParty.GetPosition2D);
                        // If within 10 units of an enemy, combat is imminent
                        return distanceToTarget < 10f;
                    }
                }

                // Check if lord is in an army that's targeting something
                var armyTarget = lordParty.Army?.AiBehaviorObject;
                if (armyTarget != null && armyTarget is MobileParty armyTargetParty)
                {
                    bool isHostile =
                        FactionManager.IsAtWarAgainstFaction(lordParty.MapFaction, armyTargetParty.MapFaction);
                    if (isHostile)
                    {
                        var armyLeader = lordParty.Army.LeaderParty;
                        if (armyLeader != null)
                        {
                            float distanceToTarget = armyLeader.GetPosition2D.Distance(armyTargetParty.GetPosition2D);
                            return distanceToTarget < 15f; // Slightly larger radius for army engagements
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                ModLogger.Debug("PositionSync", $"Error checking if lord near enemy: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        ///     Syncs the player's position and sea state with the lord when the lord is at sea.
        ///     This is needed because the normal escort AI doesn't work when the target is at sea
        ///     and the follower doesn't have naval navigation capability (ships).
        ///     Instead, we directly teleport the player to the lord's position and sync their sea state.
        /// </summary>
        /// <param name="mainParty">The player's party.</param>
        /// <param name="lordParty">The lord's party that the player is following.</param>
        /// <returns>True if the lord is at sea and position was synced, false otherwise.</returns>
        private bool TrySyncNavalPosition(MobileParty mainParty, MobileParty lordParty)
        {
            try
            {
                if (mainParty == null || lordParty == null)
                {
                    return false;
                }

                // Check if the lord is currently at sea (sailing on a ship)
                if (!lordParty.IsCurrentlyAtSea)
                {
                    return false;
                }

                // Lord is at sea - sync position and sea state directly
                // The escort AI won't work across land/sea boundaries without ships
                var lordPosition = lordParty.Position;
                var playerPosition = mainParty.Position;

                // Only sync if positions differ significantly (avoid unnecessary updates)
                var distance = playerPosition.Distance(lordPosition);
                if (distance > 0.5f || mainParty.IsCurrentlyAtSea != lordParty.IsCurrentlyAtSea)
                {
                    // Sync sea state first (must match for position to be valid)
                    if (mainParty.IsCurrentlyAtSea != lordParty.IsCurrentlyAtSea)
                    {
                        mainParty.IsCurrentlyAtSea = lordParty.IsCurrentlyAtSea;
                        ModLogger.Debug("Naval", $"Synced player sea state to {lordParty.IsCurrentlyAtSea}");
                    }

                    // Teleport player to lord's position
                    mainParty.Position = lordPosition;
                    ModLogger.Debug("Naval", $"Synced player position to lord at sea (distance was {distance:F2})");
                }

                // Set hold mode to prevent AI from trying to pathfind (which would fail on water)
                mainParty.SetMoveModeHold();

                // Camera should follow the lord for visual continuity
                lordParty.Party.SetAsCameraFollowParty();

                return true;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Naval", "Failed to sync naval position", ex);
                return false;
            }
        }


        /// <summary>
        ///     Teleports the player to a safe location just outside engagement range.
        ///     Uses the game's navigation mesh to ensure the destination is valid terrain (not water/mountains).
        ///     For naval scenarios, falls back to finding the nearest friendly/neutral port.
        ///     Called when lord is captured while player is in reserve, so player doesn't spawn
        ///     surrounded by enemies at the battle location.
        /// </summary>
        /// <param name="mainParty">The player's party to teleport.</param>
        /// <param name="threatParty">Optional threat party to escape from.</param>
        private void TryTeleportToSafety(MobileParty mainParty, PartyBase threatParty = null)
        {
            try
            {
                if (mainParty == null)
                {
                    return;
                }

                // Determine reference point (threat or lord position) to escape from
                CampaignVec2 referencePosition;
                var threatName = "battle";

                if (threatParty?.MobileParty != null)
                {
                    referencePosition = threatParty.MobileParty.Position;
                    threatName = threatParty.Name?.ToString() ?? "enemy";
                }
                else if (_enlistedLord?.PartyBelongedTo != null)
                {
                    referencePosition = _enlistedLord.PartyBelongedTo.Position;
                    threatName = _enlistedLord.Name?.ToString() ?? "lord";
                }
                else
                {
                    // No reference - use player's current position as center
                    referencePosition = mainParty.Position;
                }

                // Find a safe, reachable point just outside engagement range (12-20 map units)
                // NavigationHelper validates the position is on valid terrain (not water/mountains)
                // and that there's a navigable path to reach it
                var safePosition = NavigationHelper.FindPointAroundPosition(
                    referencePosition,
                    MobileParty.NavigationType.Default, // Land navigation
                    maxDistance: 20f,
                    minDistance: 12f,
                    requirePath: true,
                    useUniformDistribution: false
                );

                // Verify we got a valid different position
                if (safePosition.IsValid() && safePosition != referencePosition)
                {
                    mainParty.Position = safePosition;
                    mainParty.SetMoveModeHold();

                    ModLogger.Info("BATTLE",
                        $"Teleported player to safe distance from {threatName} (validated terrain)");

                    var message = new TextObject("{=Enlisted_Message_EscapedToSafety}You have escaped to a safe distance.");
                    InformationManager.DisplayMessage(new InformationMessage(message.ToString()));
                }
                else
                {
                    // Fallback for naval scenarios or when no nearby land: find nearest friendly/neutral port
                    var port = FindNearestFriendlyPort(mainParty.Position);
                    if (port != null)
                    {
                        mainParty.Position = port.GatePosition;
                        mainParty.SetMoveModeHold();

                        ModLogger.Info("BATTLE",
                            $"Teleported player to {port.Name} after naval defeat (no nearby land found)");

                        var message = new TextObject("{=Enlisted_Message_EscapedToPort}You have washed ashore at {PORT}.");
                        _ = message.SetTextVariable("PORT", port.Name);
                        InformationManager.DisplayMessage(new InformationMessage(message.ToString()));
                    }
                    else
                    {
                        ModLogger.Warn("BATTLE",
                            $"Could not find valid terrain or port for safety teleport near {threatName} - player stays in place");
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("BATTLE", "Failed to teleport player to safety", ex);
            }
        }

        /// <summary>
        ///     Teleports the player to the nearest settlement with a port when stranded at sea.
        ///     Called when service ends and player has no naval navigation capability.
        ///     This prevents players from being permanently stranded on the water.
        /// </summary>
        /// <param name="mainParty">The player's party to teleport.</param>
        private void TryTeleportToNearestPort(MobileParty mainParty)
        {
            try
            {
                if (mainParty == null)
                {
                    ModLogger.Warn("Naval", "TryTeleportToNearestPort called with null party");
                    return;
                }

                var playerPosition = mainParty.Position;
                Settlement nearestPort = null;
                var nearestDistance = float.MaxValue;

                ModLogger.Debug("Naval", "Searching for nearest port from player position");

                // Find the nearest settlement with a port
                foreach (var settlement in Settlement.All)
                {
                    if (settlement == null || settlement.IsHideout)
                    {
                        continue;
                    }

                    // Check if settlement has a port (towns with ports, coastal castles)
                    if (!settlement.HasPort)
                    {
                        continue;
                    }

                    var portPosition = settlement.PortPosition;
                    var distance = playerPosition.Distance(portPosition);

                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestPort = settlement;
                    }
                }

                if (nearestPort != null)
                {
                    // Clear sea state and teleport to the settlement's gate position (on land)
                    mainParty.IsCurrentlyAtSea = false;
                    mainParty.Position = nearestPort.GatePosition;

                    // Set hold mode so player can decide where to go
                    mainParty.SetMoveModeHold();

                    var message =
                        new TextObject("{=Enlisted_Message_WashedAshore}You have washed ashore near {SETTLEMENT}.");
                    _ = message.SetTextVariable("SETTLEMENT", nearestPort.Name);
                    InformationManager.DisplayMessage(new InformationMessage(message.ToString()));

                    ModLogger.Info("Naval",
                        $"Teleported stranded player to {nearestPort.Name} (distance was {nearestDistance:F2})");
                }
                else
                {
                    // Fallback: just clear sea state and hope for the best
                    // This shouldn't happen as there should always be ports in the game
                    mainParty.IsCurrentlyAtSea = false;
                    mainParty.SetMoveModeHold();

                    ModLogger.Warn("Naval", "No port found - cleared sea state but player may still be in water");

                    var message = new TextObject("{=Enlisted_Message_MadeItToShore}You have made it to shore.");
                    InformationManager.DisplayMessage(new InformationMessage(message.ToString()));
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Naval", "Failed to teleport stranded player to nearest port", ex);

                // Emergency fallback - at least try to clear sea state
                try
                {
                    if (mainParty != null)
                    {
                        mainParty.IsCurrentlyAtSea = false;
                    }
                }
                catch
                {
                    // Ignore - we tried our best
                }
            }
        }

        /// <summary>
        ///     Finds the nearest friendly or neutral settlement with a port.
        ///     Used for naval defeat scenarios where the player needs to "wash ashore" somewhere safe.
        /// </summary>
        /// <param name="fromPosition">The position to search from.</param>
        /// <returns>The nearest friendly/neutral port settlement, or null if none found.</returns>
        private Settlement FindNearestFriendlyPort(CampaignVec2 fromPosition)
        {
            var playerFaction = Hero.MainHero?.MapFaction;
            Settlement bestPort = null;
            var bestDistance = float.MaxValue;

            foreach (var settlement in Settlement.All)
            {
                if (settlement == null || settlement.IsHideout)
                {
                    continue;
                }

                // Skip settlements without ports (for naval scenarios)
                // Also accept any town/castle for land-based fallback
                var isPort = settlement.HasPort;
                var isSafeSettlement = settlement.IsTown || settlement.IsCastle;

                if (!isPort && !isSafeSettlement)
                {
                    continue;
                }

                // Skip enemy settlements
                if (playerFaction != null && settlement.MapFaction != null)
                {
                    if (FactionManager.IsAtWarAgainstFaction(playerFaction, settlement.MapFaction))
                    {
                        continue;
                    }
                }

                var targetPosition = isPort ? settlement.PortPosition : settlement.GatePosition;
                var distance = fromPosition.Distance(targetPosition);

                // Prefer ports for naval scenarios
                if (isPort)
                {
                    distance *= 0.8f; // 20% distance bonus for ports
                }

                // Prefer towns over castles (more services)
                if (settlement.IsTown)
                {
                    distance *= 0.9f; // 10% distance bonus for towns
                }

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestPort = settlement;
                }
            }

            return bestPort;
        }

        #region Veteran Retirement System

        // NOTE: Per-faction service records are now managed by ServiceRecordManager.
        // Use GetFactionVeteranRecord() which delegates to ServiceRecordManager.Instance.GetOrCreateRecord().

        /// <summary>
        ///     Whether the player has been notified about retirement eligibility for this term.
        ///     Resets when starting new term.
        /// </summary>
        private bool _retirementNotificationShown;

        /// <summary>
        ///     Total kills accumulated during current service term.
        ///     Added to faction's TotalKills on retirement/re-enlistment.
        /// </summary>
        private int _currentTermKills;

        #endregion

        #region Public Events (for mod integration)

        /// <summary>
        ///     Fired when player enlists with a lord. Passes the lord Hero.
        /// </summary>
        public static event Action<Hero> OnEnlisted;

        /// <summary>
        ///     Fired when player is discharged. Passes the reason string.
        /// </summary>
        public static event Action<string> OnDischarged;

        /// <summary>
        ///     Fired when a unit assignment is finalized.
        /// </summary>
#pragma warning disable CS0067 // Event is currently inactive
        public static event Action<string, string, string> OnLanceFinalized;
#pragma warning restore CS0067

        /// <summary>
        ///     Fired when player is promoted. Passes the new tier number.
        /// </summary>
        public static event Action<int> OnPromoted;

        /// <summary>
        ///     Current rank title for story systems (culture-specific from progression_config).
        ///     Returns the rank name appropriate for the current lord's culture.
        /// </summary>
        public string CurrentRankTitle
        {
            get
            {
                var cultureId = CurrentLord?.Culture?.StringId ??
                               CurrentLord?.Clan?.Kingdom?.Culture?.StringId ??
                               "mercenary";
                return ConfigurationManager.GetCultureRankTitle(_enlistmentTier, cultureId);
            }
        }

        /// <summary>
        ///     Fired when player starts temporary leave.
        /// </summary>
        public static event Action OnLeaveStarted;

        /// <summary>
        ///     Fired when leave ends (either by returning or desertion).
        /// </summary>
        public static event Action OnLeaveEnded;

        /// <summary>
        ///     Fired when grace period begins (lord death, capture, army defeat).
        /// </summary>
        public static event Action OnGracePeriodStarted;

        /// <summary>
        ///     Fired when XP is gained. Passes amount and source description.
        /// </summary>
        public static event Action<int, string> OnXPGained;

        /// <summary>
        ///     Fired when EnlistmentTier changes. Params: previousTier, newTier.
        /// </summary>
        public static event Action<int, int> OnTierChanged;

        /// <summary>
        ///     Fired by StopEnlist before tear-down so subscribers see live state.
        ///     Params: reason, isHonorableDischarge.
        /// </summary>
        public static event Action<string, bool> OnEnlistmentEnded;

        /// <summary>
        ///     Flag indicating the player chose to keep their retinue troops on retirement.
        ///     Set by dialog, checked by RetinueLifecycleHandler and ServiceRecordManager,
        ///     then reset after processing. Troops become regular party members.
        /// </summary>
        public static bool RetainTroopsOnRetirement { get; set; }

        /// <summary>
        ///     Fired when daily wage is paid. Passes the wage amount.
        /// </summary>
        public static event Action<int> OnWagePaid;

        #endregion

        #region Veteran System Properties

        /// <summary>
        ///     Date when the current enlistment started.
        /// </summary>
        public CampaignTime EnlistmentDate => _enlistmentDate;

        /// <summary>
        ///     Days served in current enlistment term.
        /// </summary>
        public float DaysServed => _enlistmentDate != CampaignTime.Zero
            ? (float)(CampaignTime.Now - _enlistmentDate).ToDays
            : 0f;

        /// <summary>
        ///     Whether the player has served the minimum configured days required for first-term retirement.
        /// </summary>
        public bool IsEligibleForRetirement
        {
            get
            {
                var config = EnlistedConfig.LoadRetirementConfig();
                var requiredDays = config?.FirstTermDays > 0 ? config.FirstTermDays : 252;
                return IsEnlisted && DaysServed >= requiredDays && !IsInRenewalTerm;
            }
        }

        /// <summary>
        ///     Whether the player is currently in a renewal term (post-first-term).
        /// </summary>
        public bool IsInRenewalTerm
        {
            get
            {
                if (_enlistedLord == null)
                {
                    return false;
                }

                var record = GetFactionVeteranRecord(_enlistedLord.MapFaction);
                return record?.IsInRenewalTerm ?? false;
            }
        }

        /// <summary>
        ///     Whether the current renewal term has expired and player can retire/continue.
        /// </summary>
        public bool IsRenewalTermComplete
        {
            get
            {
                if (_enlistedLord == null || !IsInRenewalTerm)
                {
                    return false;
                }

                var record = GetFactionVeteranRecord(_enlistedLord.MapFaction);
                return record != null && record.CurrentTermEnd != CampaignTime.Zero && CampaignTime.Now >= record.CurrentTermEnd;
            }
        }

        /// <summary>
        /// Gets the service record for a faction. Creates new record if none exists.
        /// Uses the unified FactionServiceRecord from ServiceRecordManager.
        /// Supports both Kingdoms and Clans (minor factions).
        /// </summary>
        public FactionServiceRecord GetFactionVeteranRecord(IFaction faction)
        {
            if (faction == null)
            {
                return null;
            }

            return ServiceRecordManager.Instance?.GetOrCreateRecord(faction);
        }

        /// <summary>
        ///     Checks if the player is in cooldown period for a faction.
        /// </summary>
        public bool IsInFactionCooldown(IFaction faction)
        {
            if (faction == null)
            {
                return false;
            }

            var record = GetFactionVeteranRecord(faction);
            return record != null && record.CooldownEnds != CampaignTime.Zero && CampaignTime.Now < record.CooldownEnds;
        }

        /// <summary>
        ///     Checks if the player can re-enlist with a faction after cooldown.
        /// </summary>
        public bool CanReEnlistAfterCooldown(IFaction faction)
        {
            if (faction == null)
            {
                return false;
            }

            var record = GetFactionVeteranRecord(faction);
            // Must have completed first term and be past cooldown
            return record != null &&
                   record.FirstTermCompleted &&
                   record.CooldownEnds != CampaignTime.Zero &&
                   CampaignTime.Now >= record.CooldownEnds;
        }

        #endregion

        #region Veteran Retirement System Methods

        /// <summary>
        ///     Checks if the player has reached first-term retirement eligibility (configurable days).
        ///     Shows a one-time notification when first eligible.
        /// </summary>
        private void CheckRetirementEligibility()
        {
            if (_retirementNotificationShown || !IsEnlisted || _isPendingDischarge)
            {
                return;
            }

            var config = EnlistedConfig.LoadRetirementConfig();
            if (DaysServed >= config.FirstTermDays)
            {
                _retirementNotificationShown = true;

                var message =
                    new TextObject(
                        "{=Enlisted_Message_TermCompleted}You have completed your term of service! Use the Camp menu to request a managed discharge (Final Muster).");
                InformationManager.DisplayMessage(new InformationMessage(message.ToString(), Colors.Green));

                ModLogger.Info("RETIREMENT", $"Player eligible for retirement after {DaysServed:F1} days");
            }
        }

        /// <summary>
        ///     Process first-term retirement: apply full benefits and start cooldown.
        ///     Called when player chooses to retire after first term.
        /// </summary>
        public void ProcessFirstTermRetirement()
        {
            if (!IsEnlisted || !IsEligibleForRetirement)
            {
                ModLogger.Expected("RETIREMENT", "retire_first_term_ineligible",
                    "Cannot process first-term retirement - not eligible");
                return;
            }

            var config = EnlistedConfig.LoadRetirementConfig();
            var faction = _enlistedLord.MapFaction;
            var record = GetFactionVeteranRecord(faction);

            // Mark first term as completed
            record.FirstTermCompleted = true;
            record.PreservedTier = _enlistmentTier;
            record.TotalKills += _currentTermKills;

            // Start 6-month cooldown (42 days)
            record.CooldownEnds = CampaignTime.Now + CampaignTime.Days(config.CooldownDays);

            // Award retirement gold
            GiveGoldAction.ApplyForCharacterToParty(null, MobileParty.MainParty.Party, config.FirstTermGold, true);
            ModLogger.Info("Gold", $"Retirement bonus: {config.FirstTermGold} denars (first term completion)");

            // Apply relation bonuses
            ApplyVeteranRelationBonuses(config);

            // Award 50 renown for first term retirement
            GainRenownAction.Apply(Hero.MainHero, 50f);
            ModLogger.Info("Renown", "First term retirement: +50 renown");

            // End enlistment
            StopEnlist("Honorable retirement - first term", true);

            var message =
                new TextObject(
                    "{=Enlisted_Message_RetiredHonor}You have retired with honor. {GOLD} gold received. You may re-enlist with {KINGDOM} after the cooldown period.");
            _ = message.SetTextVariable("GOLD", config.FirstTermGold);
            _ = message.SetTextVariable("KINGDOM",
                faction?.Name ?? new TextObject("{=Enlisted_Term_ThisFaction}this faction"));
            InformationManager.DisplayMessage(new InformationMessage(message.ToString(), Colors.Green));

            ModLogger.Info("ENLISTMENT",
                $"First term retirement processed: {config.FirstTermGold}g, cooldown ends {record.CooldownEnds}");
        }

        /// <summary>
        ///     Process renewal term retirement: award discharge gold and start cooldown.
        ///     Called when player chooses to retire after a renewal term.
        /// </summary>
        public void ProcessRenewalRetirement()
        {
            if (!IsEnlisted || !IsInRenewalTerm)
            {
                ModLogger.Expected("RETIREMENT", "retire_renewal_not_in_term",
                    "Cannot process renewal retirement - not in renewal term");
                return;
            }

            var config = EnlistedConfig.LoadRetirementConfig();
            var faction = _enlistedLord.MapFaction;
            var record = GetFactionVeteranRecord(faction);

            // Update record
            record.PreservedTier = _enlistmentTier;
            record.TotalKills += _currentTermKills;
            record.IsInRenewalTerm = false;
            record.RenewalTermsCompleted++;

            // Start cooldown
            record.CooldownEnds = CampaignTime.Now + CampaignTime.Days(config.CooldownDays);

            // Award discharge gold
            GiveGoldAction.ApplyForCharacterToParty(null, MobileParty.MainParty.Party, config.RenewalDischargeGold,
                true);
            ModLogger.Info("Gold", $"Discharge bonus: {config.RenewalDischargeGold} denars (renewal term completion)");

            // End enlistment
            StopEnlist("Honorable discharge - renewal term", true);

            var message =
                new TextObject(
                    "{=Enlisted_Message_Discharged}You have been discharged. {GOLD} gold received. You may re-enlist with {KINGDOM} after {DAYS} days.");
            _ = message.SetTextVariable("GOLD", config.RenewalDischargeGold);
            _ = message.SetTextVariable("KINGDOM",
                faction?.Name ?? new TextObject("{=Enlisted_Term_ThisFaction}this faction"));
            _ = message.SetTextVariable("DAYS", config.CooldownDays);
            InformationManager.DisplayMessage(new InformationMessage(message.ToString()));

            ModLogger.Info("ENLISTMENT",
                $"Renewal term discharge: {config.RenewalDischargeGold}g, cooldown {config.CooldownDays} days");
        }

        /// <summary>
        ///     Start a renewal term with bonus payment.
        ///     Called when player chooses to continue service.
        ///     First re-enlistment grants full veteran bonuses (30/30/15 relation, 50 renown).
        ///     Subsequent re-enlistments grant smaller bonuses (10/10/10 relation, 10 renown).
        /// </summary>
        public void StartRenewalTerm(int bonus)
        {
            if (!IsEnlisted)
            {
                ModLogger.Expected("RETIREMENT", "retire_renewal_not_enlisted",
                    "Cannot start renewal term - not enlisted");
                return;
            }

            var config = EnlistedConfig.LoadRetirementConfig();
            var faction = _enlistedLord.MapFaction;
            var record = GetFactionVeteranRecord(faction);

            // Check if this is the first re-enlistment (no renewal terms completed yet)
            var isFirstReenlistment = record.RenewalTermsCompleted == 0;

            // Update record for renewal
            if (!record.FirstTermCompleted)
            {
                record.FirstTermCompleted = true;
            }

            record.IsInRenewalTerm = true;
            record.CurrentTermEnd = CampaignTime.Now + CampaignTime.Days(config.RenewalTermDays);

            // Pay the bonus
            if (bonus > 0)
            {
                GiveGoldAction.ApplyForCharacterToParty(null, MobileParty.MainParty.Party, bonus, true);
            }

            // Apply relation and renown bonuses based on whether this is first or subsequent re-enlistment
            if (isFirstReenlistment)
            {
                // First re-enlistment: same bonuses as first retirement (30/30/15 relation, 50 renown)
                ApplyVeteranRelationBonuses(config);
                GainRenownAction.Apply(Hero.MainHero, 50f);
                ModLogger.Info("Renown", "First re-enlistment: +50 renown, full relation bonuses applied");
            }
            else
            {
                // Subsequent re-enlistments: smaller bonuses (10/10/10 relation, 10 renown)
                ApplySubsequentReenlistmentBonuses();
                GainRenownAction.Apply(Hero.MainHero, 10f);
                ModLogger.Info("Renown", "Subsequent re-enlistment: +10 renown, reduced relation bonuses applied");
            }

            // Reset notification flag for this term
            _retirementNotificationShown = true; // Don't show first-term notification again

            var message =
                new TextObject(
                    "{=Enlisted_Message_ReEnlistedBonus}You have re-enlisted for another term. {GOLD} gold bonus received. Term ends in {DAYS} days.");
            _ = message.SetTextVariable("GOLD", bonus);
            _ = message.SetTextVariable("DAYS", config.RenewalTermDays);
            InformationManager.DisplayMessage(new InformationMessage(message.ToString()));

            ModLogger.Info("RETIREMENT",
                $"Renewal term started: {bonus}g bonus, ends in {config.RenewalTermDays} days, first={isFirstReenlistment}");
        }

        /// <summary>
        ///     Re-enlist with a faction after cooldown period.
        ///     Restores preserved tier and starts new 1-year term.
        /// </summary>
        public void ReEnlistAfterCooldown(Hero lord)
        {
            var faction = lord.MapFaction;
            if (faction == null || !CanReEnlistAfterCooldown(faction))
            {
                ModLogger.Expected("RETIREMENT", "retire_reenlist_ineligible",
                    "Cannot re-enlist - not eligible or wrong faction");
                return;
            }

            var config = EnlistedConfig.LoadRetirementConfig();
            var record = GetFactionVeteranRecord(faction);

            // Restore tier from record
            var preservedTier = record.PreservedTier;

            // Clear cooldown
            record.CooldownEnds = CampaignTime.Zero;

            // Start enlistment normally
            StartEnlist(lord);

            // Restore tier
            _enlistmentTier = preservedTier;
            _enlistmentXP = GetMinXPForTier(preservedTier);

            // Start in renewal mode
            record.IsInRenewalTerm = true;
            record.CurrentTermEnd = CampaignTime.Now + CampaignTime.Days(config.RenewalTermDays);

            _retirementNotificationShown = true;
            _currentTermKills = 0;

            var message =
                new TextObject(
                    "{=Enlisted_Message_ReEnlistedRank}You have re-enlisted with {KINGDOM}. Your rank of Tier {TIER} has been restored. Term: {DAYS} days.");
            _ = message.SetTextVariable("KINGDOM", faction.Name);
            _ = message.SetTextVariable("TIER", preservedTier);
            _ = message.SetTextVariable("DAYS", config.RenewalTermDays);
            InformationManager.DisplayMessage(new InformationMessage(message.ToString()));

            ModLogger.Info("RETIREMENT",
                $"Re-enlisted after cooldown: tier {preservedTier} restored, {config.RenewalTermDays} day term");
        }

        /// <summary>
        ///     Get the minimum XP required for a given tier.
        /// </summary>
        private int GetMinXPForTier(int tier)
        {
            var requirements = EnlistedConfig.GetTierXpRequirements();
            if (tier > 0 && tier <= requirements.Length)
            {
                return requirements[tier - 1];
            }

            return 0;
        }

        /// <summary>
        ///     Apply veteran relation bonuses for first-term retirement.
        ///     +30 with current lord, +30 with faction, +15 with other lords (if rep > 50).
        /// </summary>
        private void ApplyVeteranRelationBonuses(RetirementConfig config)
        {
            try
            {
                var faction = _enlistedLord?.MapFaction;
                if (faction == null)
                {
                    ModLogger.Expected("RETIREMENT", "retire_bonuses_no_faction",
                        "Cannot apply relation bonuses - no faction");
                    return;
                }

                // +30 with current lord
                if (_enlistedLord != null)
                {
                    ChangeRelationAction.ApplyPlayerRelation(_enlistedLord, config.LordRelationBonus);
                    ModLogger.Info("RETIREMENT", $"+{config.LordRelationBonus} relation with {_enlistedLord.Name}");
                }

                // +30 with faction (clan reputation affects kingdom standing)
                // Use leader as proxy for faction reputation
                if (faction.Leader != null && faction.Leader != _enlistedLord)
                {
                    ChangeRelationAction.ApplyPlayerRelation(faction.Leader, config.FactionReputationBonus);
                    ModLogger.Info("RETIREMENT",
                        $"+{config.FactionReputationBonus} relation with {faction.Name} (via leader)");
                }

                // +15 with other lords in faction IF player has > 50 relation with them
                if (faction is Kingdom kingdom)
                {
                    foreach (var clan in kingdom.Clans)
                    {
                        if (clan == Clan.PlayerClan)
                        {
                            continue;
                        }

                        foreach (var hero in clan.Heroes)
                        {
                            // Only apply to lords (not companions)
                            if (!hero.IsLord || hero == _enlistedLord || hero == kingdom.Leader || !hero.IsAlive)
                            {
                                continue;
                            }

                            var currentRelation = Hero.MainHero.GetRelation(hero);
                            if (currentRelation > config.OtherLordsMinRelation)
                            {
                                ChangeRelationAction.ApplyPlayerRelation(hero, config.OtherLordsRelationBonus, true,
                                    false);
                                ModLogger.Debug("RETIREMENT",
                                    $"+{config.OtherLordsRelationBonus} relation with {hero.Name} (had {currentRelation})");
                            }
                        }
                    }
                }
                else if (faction is Clan clan)
                {
                    // Minor faction handling
                    foreach (var hero in clan.Heroes)
                    {
                        if (!hero.IsLord || hero == _enlistedLord || hero == clan.Leader || !hero.IsAlive)
                        {
                            continue;
                        }

                        var currentRelation = Hero.MainHero.GetRelation(hero);
                        if (currentRelation > config.OtherLordsMinRelation)
                        {
                            ChangeRelationAction.ApplyPlayerRelation(hero, config.OtherLordsRelationBonus, true, false);
                            ModLogger.Debug("RETIREMENT",
                                $"+{config.OtherLordsRelationBonus} relation with {hero.Name} (had {currentRelation})");
                        }
                    }
                }

                ModLogger.Info("RETIREMENT", "Veteran relation bonuses applied");
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("RETIREMENT", "Error applying relation bonuses", ex);
            }
        }

        /// <summary>
        ///     Apply smaller relation bonuses for subsequent re-enlistments.
        ///     +10 with current lord, +10 with faction leader, +10 with other lords.
        /// </summary>
        private void ApplySubsequentReenlistmentBonuses()
        {
            const int subsequentBonus = 10;

            try
            {
                var faction = _enlistedLord?.MapFaction;
                if (faction == null)
                {
                    ModLogger.Expected("RETIREMENT", "retire_subsequent_bonuses_no_faction",
                        "Cannot apply subsequent bonuses - no faction");
                    return;
                }

                // +10 with current lord
                if (_enlistedLord != null)
                {
                    ChangeRelationAction.ApplyPlayerRelation(_enlistedLord, subsequentBonus);
                    ModLogger.Info("RETIREMENT", $"+{subsequentBonus} relation with {_enlistedLord.Name} (subsequent re-enlistment)");
                }

                // +10 with faction leader
                if (faction.Leader != null && faction.Leader != _enlistedLord)
                {
                    ChangeRelationAction.ApplyPlayerRelation(faction.Leader, subsequentBonus);
                    ModLogger.Info("RETIREMENT", $"+{subsequentBonus} relation with {faction.Name} leader (subsequent re-enlistment)");
                }

                // +10 with other lords in faction (no minimum relation requirement for subsequent)
                if (faction is Kingdom kingdom)
                {
                    foreach (var clan in kingdom.Clans)
                    {
                        if (clan == Clan.PlayerClan)
                        {
                            continue;
                        }

                        foreach (var hero in clan.Heroes)
                        {
                            if (!hero.IsLord || hero == _enlistedLord || hero == kingdom.Leader || !hero.IsAlive)
                            {
                                continue;
                            }

                            ChangeRelationAction.ApplyPlayerRelation(hero, subsequentBonus, true, false);
                        }
                    }
                }
                else if (faction is Clan clan)
                {
                    foreach (var hero in clan.Heroes)
                    {
                        if (!hero.IsLord || hero == _enlistedLord || hero == clan.Leader || !hero.IsAlive)
                        {
                            continue;
                        }

                        ChangeRelationAction.ApplyPlayerRelation(hero, subsequentBonus, true, false);
                    }
                }

                ModLogger.Info("RETIREMENT", $"Subsequent re-enlistment bonuses applied (+{subsequentBonus} relation)");
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("RETIREMENT", "Error applying subsequent re-enlistment bonuses", ex);
            }
        }

        #endregion

        #region Event Handlers for Lord Status Changes

        /// <summary>
        ///     Handles the case when the enlisted lord is killed in battle or other circumstances.
        ///     Starts a 14-day grace period to re-enlist with another lord in the same faction.
        ///     Called by the game when any hero is killed.
        /// </summary>
        /// <param name="victim">The hero that was killed.</param>
        /// <param name="killer">The hero that killed the victim (may be null).</param>
        /// <param name="detail">Details about how the hero was killed.</param>
        /// <param name="showNotification">Whether to show a notification to the player.</param>
        private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail,
            bool showNotification)
        {
            if (IsEnlisted && victim == _enlistedLord)
            {
                ModLogger.Info("EVENTSAFETY", $"Lord {victim.Name} killed - starting grace period");

                // Start grace period instead of immediate discharge
                // Player has 14 days to re-enlist with another lord in the same faction
                var lordKingdom = _enlistedLord.MapFaction as Kingdom;
                if (lordKingdom != null)
                {
                    var message =
                        new TextObject(
                            "{=Enlisted_Message_LordFallenGrace}Your lord has fallen. You have 14 days to find a new commander in {KINGDOM} before desertion.");
                    _ = message.SetTextVariable("KINGDOM", lordKingdom.Name);
                    InformationManager.DisplayMessage(new InformationMessage(message.ToString()));

                    StopEnlist("Lord killed in battle", false, true);
                    StartDesertionGracePeriod(lordKingdom);
                }
                else
                {
                    // No kingdom - immediate discharge without penalties
                    var message =
                        new TextObject(
                            "{=Enlisted_Message_LordFallenEnded}Your lord has fallen. Your service has ended.");
                    InformationManager.DisplayMessage(new InformationMessage(message.ToString()));
                    StopEnlist("Lord killed - no kingdom", true);
                }
            }
        }

        /// <summary>
        ///     Handles the case when the enlisted lord is defeated in battle.
        ///     Checks if the lord was killed or captured and discharges the player accordingly.
        ///     Called by the game when a character is defeated in battle.
        /// </summary>
        /// <param name="defeatedHero">The hero that was defeated.</param>
        /// <param name="victorHero">The hero that defeated the victim (may be null).</param>
        private void OnCharacterDefeated(Hero defeatedHero, Hero victorHero)
        {
            if (IsEnlisted && defeatedHero == _enlistedLord)
            {
                ModLogger.Info("EVENTSAFETY", $"Lord {defeatedHero.Name} defeated");

                // Check lord status after defeat
                if (!_enlistedLord.IsAlive)
                {
                    var message =
                        new TextObject(
                            "{=Enlisted_Message_LordFallenDischarged}Your lord has fallen in battle. You have been honorably discharged.");
                    InformationManager.DisplayMessage(new InformationMessage(message.ToString()));
                    StopEnlist("Lord died in battle", true);
                }
                else if (_enlistedLord.IsPrisoner)
                {
                    // Start grace period instead of immediate discharge
                    // Player has 14 days to rejoin another lord in the same kingdom
                    var lordKingdom = _enlistedLord.MapFaction as Kingdom;
                    if (lordKingdom != null)
                    {
                        // End enlistment but start grace period
                        StopEnlist("Lord captured in battle", false, true);
                        StartDesertionGracePeriod(lordKingdom);
                    }
                    else
                    {
                        // No kingdom - immediate discharge
                        var message = new TextObject("{=enlist_lord_captured_ended}Your lord has been captured. Your service has ended.");
                        InformationManager.DisplayMessage(new InformationMessage(message.ToString()));
                        StopEnlist("Lord captured in battle");
                    }
                }
            }
        }

        /// <summary>
        ///     Handle army dispersal/defeat scenarios.
        ///     Called when lord's army is disbanded or defeated.
        /// </summary>
        private void OnArmyDispersed(Army army, Army.ArmyDispersionReason reason, bool isLeaderPartyRemoved)
        {
            if (IsEnlisted && army?.LeaderParty?.LeaderHero == _enlistedLord)
            {
                ModLogger.Info("EVENTSAFETY", $"Army dispersed - reason: {reason}");

                // Check if lord still exists after army defeat
                if (_enlistedLord == null || !_enlistedLord.IsAlive || _enlistedLord.PartyBelongedTo == null)
                {
                    // Start grace period instead of immediate discharge
                    // Player has 14 days to rejoin another lord in the same kingdom
                    // Capture kingdom before StopEnlist clears _enlistedLord
                    var lordKingdom = _enlistedLord?.MapFaction as Kingdom ??
                                      army?.LeaderParty?.MapFaction as Kingdom;

                    if (lordKingdom != null)
                    {
                        // End enlistment but start grace period
                        StopEnlist("Army defeated - lord lost", false, true);
                        StartDesertionGracePeriod(lordKingdom);
                    }
                    else
                    {
                        // No kingdom - immediate discharge
                        var message = new TextObject("{=enlist_army_defeated_ended}Your lord's army has been defeated. Your service has ended.");
                        InformationManager.DisplayMessage(new InformationMessage(message.ToString()));
                        StopEnlist("Army defeated - lord lost");
                    }
                }
                else
                {
                    var message = new TextObject("{=enlist_army_dispersed}Your lord's army has been dispersed. Continuing service.");
                    InformationManager.DisplayMessage(new InformationMessage(message.ToString()));

                    // NAVAL FIX: Re-establish following when army disbands at sea
                    // Without this fix, players get stranded because DisperseInternal skips
                    // repositioning for parties without naval navigation capability
                    var mainParty = MobileParty.MainParty;
                    var lordParty = _enlistedLord.PartyBelongedTo;

                    if (mainParty != null && lordParty != null)
                    {
                        // Check if lord is at sea - need to re-sync position
                        if (lordParty.IsCurrentlyAtSea)
                        {
                            ModLogger.Info("Naval",
                                $"Army disbanded at sea - re-syncing player position with lord {_enlistedLord.Name}");

                            // Sync sea state and position
                            bool wasAtSea = mainParty.IsCurrentlyAtSea;
                            mainParty.IsCurrentlyAtSea = lordParty.IsCurrentlyAtSea;
                            mainParty.Position = lordParty.Position;

                            // Set hold mode to prevent AI from trying to pathfind on water
                            mainParty.SetMoveModeHold();

                            // Camera should follow the lord
                            lordParty.Party.SetAsCameraFollowParty();

                            var navalMessage = new TextObject("{=enlist_naval_remain}You remain aboard with {LORD}'s party.");
                            _ = navalMessage.SetTextVariable("LORD", _enlistedLord.Name);
                            InformationManager.DisplayMessage(new InformationMessage(navalMessage.ToString()));

                            ModLogger.Debug("Naval",
                                $"Naval sync complete: wasAtSea={wasAtSea}, nowAtSea={mainParty.IsCurrentlyAtSea}, position synced to lord");
                        }
                        else
                        {
                            // Lord is on land - use normal escort AI
                            ModLogger.Debug("Naval", "Army disbanded on land - setting up normal escort AI");
                            mainParty.SetMoveEscortParty(lordParty, MobileParty.NavigationType.Default, false);
                            lordParty.Party.SetAsCameraFollowParty();
                        }
                    }
                    else
                    {
                        ModLogger.Warn("Naval",
                            $"Cannot re-sync after army disband: mainParty={mainParty != null}, lordParty={lordParty != null}");
                    }

                    // Menu restoration is now handled by ArmyDispersedMenuPatch which intercepts
                    // the native army_dispersed menu's "Continue" button to show enlisted_status
                }
            }
        }

        /// <summary>
        ///     Handle lord or player capture scenarios.
        ///     Called when lord or player is taken prisoner.
        /// </summary>
        private void OnHeroPrisonerTaken(PartyBase capturingParty, Hero prisoner)
        {
            if (!IsEnlisted)
            {
                return;
            }

            // Case 1: Player captured
            if (prisoner == Hero.MainHero)
            {
                var captorName = capturingParty?.LeaderHero?.Name?.ToString() ?? capturingParty?.Name?.ToString() ?? "unknown";
                ModLogger.Info("Captivity", $"Captured by {captorName} - service ended, grace period started");
                ModLogger.Info("EVENTSAFETY", "Player captured - deferring enlistment teardown until encounter closes");

                var lordKingdom = _enlistedLord?.MapFaction as Kingdom;
                if (lordKingdom != null)
                {
                    var message =
                        new TextObject(
                            "You have been taken prisoner. You have 14 days after escape to rejoin your kingdom.");
                    InformationManager.DisplayMessage(new InformationMessage(message.ToString()));
                }
                else
                {
                    var message = new TextObject("{=enlist_player_captured}You have been taken prisoner. Your service has ended.");
                    InformationManager.DisplayMessage(new InformationMessage(message.ToString()));
                }

                SchedulePlayerCaptureCleanup(lordKingdom);
                return;
            }

            // Case 2: Lord captured
            if (prisoner == _enlistedLord)
            {
                ModLogger.Info("EVENTSAFETY", $"Lord {prisoner.Name} captured - starting grace period");

                // Handle player state cleanup (reserve mode, encounters, etc.)
                // This doesn't capture the player - if player was fighting, native surrender flow handles capture
                // If player was in reserve, they escape to safety
                HandlePlayerStateOnLordCapture(capturingParty);

                // Start grace period instead of immediate discharge
                // Player has 14 days to rejoin another lord in the same kingdom
                var lordKingdom = _enlistedLord.MapFaction as Kingdom;
                if (lordKingdom != null)
                {
                    if (_playerCaptureCleanupScheduled)
                    {
                        ModLogger.Info("EVENTSAFETY",
                            "Deferring lord capture discharge because player capture cleanup is pending");
                        SchedulePlayerCaptureCleanup(lordKingdom);
                    }
                    else
                    {
                        StopEnlist("Lord captured", false, true);
                        StartDesertionGracePeriod(lordKingdom);
                    }
                }
                else
                {
                    // No kingdom - immediate discharge
                    var message = new TextObject("{=enlist_lord_captured_ended}Your lord has been captured. Your service has ended.");
                    InformationManager.DisplayMessage(new InformationMessage(message.ToString()));
                    if (_playerCaptureCleanupScheduled)
                    {
                        SchedulePlayerCaptureCleanup(null);
                    }
                    else
                    {
                        StopEnlist("Lord captured");
                    }
                }
            }
        }

        /// <summary>
        ///     Handle clan changing kingdoms - both player clan and lord's clan.
        ///     Edge cases:
        ///     1. If player changes kingdoms during grace period, clear grace period.
        ///     2. If player becomes vassal/mercenary while actively enlisted, end enlistment honorably.
        ///     3. If lord's minor faction clan joins a Kingdom, auto-join player as mercenary.
        ///     Player can no longer rejoin the original kingdom, so grace period is invalid.
        /// </summary>
        private void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom,
            ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification)
        {
            // EDGE CASE: Lord's clan changes kingdom affiliation
            // When enlisted as a soldier, the player's faction status must follow the lord's clan.
            // This mirrors native Bannerlord mercenary behavior where soldiers follow their employer.
            // Without this sync, the player could be counted as a participant in battles for a kingdom
            // they're no longer affiliated with (e.g., mercenary band ends contract mid-campaign).
            //
            // IMPORTANT: This applies whether actively enlisted OR on leave - the player is still
            // committed to the lord's service and their faction must stay in sync.
            bool hasEnlistmentCommitment = (IsEnlisted || _isOnLeave) && _enlistedLord != null;
            if (hasEnlistmentCommitment && clan == _enlistedLord.Clan && clan != Clan.PlayerClan)
            {
                var playerClan = Clan.PlayerClan;
                if (playerClan == null)
                {
                    return;
                }

                // EDGE CASE: Don't change faction if player is currently a prisoner
                // The native captivity system manages prisoner state, and changing kingdoms
                // mid-captivity could cause issues with ransom/release logic.
                if (Hero.MainHero?.IsPrisoner == true)
                {
                    ModLogger.Info("ENLISTMENT",
                        $"Lord's clan changed kingdoms but player is prisoner - deferring faction sync until release");
                    return;
                }

                // EDGE CASE: Don't change faction if player is currently in an active battle
                // Changing factions mid-battle could cause the player to switch sides or be
                // counted incorrectly. The faction sync will happen naturally after battle ends.
                var mainParty = MobileParty.MainParty;
                if (mainParty?.Party?.MapEvent != null)
                {
                    ModLogger.Warn("ENLISTMENT",
                        $"Lord's clan changed kingdoms during active battle - faction may be desynced until battle ends");
                    // Don't return here - try to sync anyway, but log the warning
                    // The battle participation was already determined when battle started
                }

                string leaveNote = _isOnLeave ? " (on leave)" : "";

                // Case 1: Lord's clan joined a Kingdom (was independent, now has contract)
                if (newKingdom != null && oldKingdom == null)
                {
                    if (playerClan.Kingdom != newKingdom)
                    {
                        try
                        {
                            ChangeKingdomAction.ApplyByJoinFactionAsMercenary(playerClan, newKingdom,
                                default(CampaignTime), 0, false);
                            ModLogger.Info("ENLISTMENT",
                                $"Lord's clan joined {newKingdom.Name}{leaveNote} - auto-joined player as mercenary to maintain battle eligibility");
                        }
                        catch (Exception ex)
                        {
                            ModLogger.Caught("ENLISTMENT",
                                $"Failed to auto-join player as mercenary when lord's clan joined {newKingdom.Name}", ex);
                        }
                    }
                }
                // Case 2: Lord's clan left a Kingdom (had contract, now independent)
                // Player follows their lord - this is NOT desertion, just following orders
                else if (oldKingdom != null && newKingdom == null)
                {
                    if (playerClan.Kingdom == oldKingdom)
                    {
                        try
                        {
                            // Set discharge flag to prevent this being treated as desertion
                            // The player is loyally following their lord, not abandoning service
                            _isProcessingDischarge = true;
                            ChangeKingdomAction.ApplyByLeaveKingdomAsMercenary(playerClan, false);
                            ModLogger.Info("ENLISTMENT",
                                $"Lord's clan left {oldKingdom.Name}{leaveNote} - removed player from kingdom (following lord)");

                            // Notify the player of the change
                            var message = new TextObject(
                                "{=Enlisted_Message_LordLeftKingdom}Your company has ended its contract with {KINGDOM}. You march on with your lord.");
                            _ = message.SetTextVariable("KINGDOM", oldKingdom.Name);
                            InformationManager.DisplayMessage(new InformationMessage(message.ToString()));
                        }
                        catch (Exception ex)
                        {
                            ModLogger.Caught("ENLISTMENT",
                                $"Failed to remove player from kingdom when lord's clan left {oldKingdom.Name}", ex);
                        }
                        finally
                        {
                            _isProcessingDischarge = false;
                        }
                    }
                }
                // Case 3: Lord's clan switched kingdoms (ended one contract, started another)
                // Player follows their lord to the new kingdom
                else if (oldKingdom != null && newKingdom != null && oldKingdom != newKingdom)
                {
                    try
                    {
                        // Set discharge flag to prevent intermediate state being treated as desertion
                        _isProcessingDischarge = true;

                        // First leave old kingdom if player is in it
                        if (playerClan.Kingdom == oldKingdom)
                        {
                            ChangeKingdomAction.ApplyByLeaveKingdomAsMercenary(playerClan, false);
                            ModLogger.Info("ENLISTMENT",
                                $"Lord's clan switching kingdoms{leaveNote} - removed player from {oldKingdom.Name}");
                        }

                        // Then join new kingdom
                        if (playerClan.Kingdom != newKingdom)
                        {
                            ChangeKingdomAction.ApplyByJoinFactionAsMercenary(playerClan, newKingdom,
                                default(CampaignTime), 0, false);
                            ModLogger.Info("ENLISTMENT",
                                $"Lord's clan switched to {newKingdom.Name}{leaveNote} - auto-joined player as mercenary");
                        }

                        // Notify the player of the change
                        var message = new TextObject(
                            "{=Enlisted_Message_LordSwitchedKingdom}Your company has signed a new contract with {KINGDOM}. The campaign continues.");
                        _ = message.SetTextVariable("KINGDOM", newKingdom.Name);
                        InformationManager.DisplayMessage(new InformationMessage(message.ToString()));
                    }
                    catch (Exception ex)
                    {
                        ModLogger.Caught("ENLISTMENT",
                            $"Failed to switch player's kingdom from {oldKingdom.Name} to {newKingdom.Name}", ex);
                    }
                    finally
                    {
                        _isProcessingDischarge = false;
                    }
                }

                return; // Don't process further - this was the lord's clan, not player's
            }

            if (clan != Clan.PlayerClan)
            {
                return; // Not the player's clan and not lord's clan
            }

            // Handle player leaving the kingdom entirely (quitting mercenary service) while enlisted or on leave
            // This is desertion - they abandoned their service commitment
            // EXCEPTION: If _isProcessingDischarge is true, this is an intentional retirement/discharge,
            // not a player abandoning their post. Skip desertion logic to allow graceful retirement.
            if ((IsEnlisted || _isOnLeave) && newKingdom == null && oldKingdom != null && !_isProcessingDischarge)
            {
                var lordKingdom = _enlistedLord?.MapFaction as Kingdom;

                // Only process if they were in the lord's kingdom (not some random kingdom change)
                if (lordKingdom != null && oldKingdom == lordKingdom)
                {
                    ModLogger.Info("Desertion",
                        $"Player quit mercenary service with {oldKingdom.Name} while {(IsEnlisted ? "enlisted" : "on leave")} - treating as desertion");

                    // Clear leave state if on leave
                    if (_isOnLeave)
                    {
                        _isOnLeave = false;
                        _leaveStartDate = CampaignTime.Zero;
                    }

                    // Set pending desertion kingdom to the original lord's kingdom
                    _pendingDesertionKingdom = lordKingdom;

                    // Apply desertion penalties
                    ApplyDesertionPenalties();

                    // Fire event for other mods (before StopEnlist clears state)
                    OnLeaveEnded?.Invoke();

                    // End enlistment as desertion
                    StopEnlist($"Player quit mercenary service with {oldKingdom.Name} - desertion", isHonorableDischarge: false);

                    var desertionMessage = new TextObject(
                        "{=Enlisted_Message_QuitMercenaryDesertion}You have abandoned your mercenary service with {KINGDOM}. You have been branded a deserter.");
                    _ = desertionMessage.SetTextVariable("KINGDOM", oldKingdom.Name);
                    InformationManager.DisplayMessage(new InformationMessage(desertionMessage.ToString()));

                    return; // Early return - don't process grace period logic
                }
            }

            // BUG FIX: If player is enlisted (active or on leave) and kingdom status changes, handle appropriately
            // When actively enlisted, player is already a mercenary in the lord's kingdom.
            // This handles:
            // 1. Active enlistment: mercenary -> vassal promotion (same kingdom, oldKingdom == newKingdom)
            // 2. Active enlistment: joins different kingdom (oldKingdom != newKingdom)
            // 3. On leave: joins any kingdom as mercenary/vassal (may have left kingdom during leave)
            // NOTE: We must NOT end enlistment when they first join as mercenary during StartEnlist()
            // NOTE: Skip all of this if _isProcessingDischarge is true - intentional discharge kingdom changes
            // (like restoring to original kingdom after full service) should not trigger any of this logic.
            if ((IsEnlisted || _isOnLeave) && newKingdom != null && !_isProcessingDischarge)
            {
                var playerClan = Clan.PlayerClan;
                if (playerClan != null && playerClan.Kingdom == newKingdom)
                {
                    var lordKingdom = _enlistedLord?.MapFaction as Kingdom;

                    // Determine if this is a meaningful status change that should end enlistment:
                    // 1. Promotion to vassal: same kingdom, became vassal (was mercenary, now vassal) = honorable
                    // 2. Kingdom transfer: joined different kingdom than lord's kingdom = desertion
                    // 3. On leave + same kingdom: honorable discharge
                    // 4. On leave + different kingdom: desertion
                    //
                    // EXCLUDE: Initial enlistment (joining lord's kingdom as mercenary during StartEnlist)
                    // This happens when: oldKingdom != lordKingdom && newKingdom == lordKingdom && is mercenary
                    var isPromotionToVassal = oldKingdom == newKingdom && !playerClan.IsUnderMercenaryService;
                    var isKingdomTransfer = lordKingdom != null && lordKingdom != newKingdom;
                    var wasOnLeave = _isOnLeave;
                    var isInitialEnlistment = lordKingdom != null && lordKingdom == newKingdom &&
                                             oldKingdom != lordKingdom && playerClan.IsUnderMercenaryService;
                    // Only vassals of the same kingdom get honorable discharge
                    // Mercenaries who join same kingdom while on leave are still deserters (they broke their service commitment)
                    var isOnLeaveBecameVassal = wasOnLeave && lordKingdom != null && lordKingdom == newKingdom && !playerClan.IsUnderMercenaryService;
                    var isOnLeaveDifferentKingdom = wasOnLeave && lordKingdom != null && lordKingdom != newKingdom;
                    var isOnLeaveSameKingdomAsMercenary = wasOnLeave && lordKingdom != null && lordKingdom == newKingdom && playerClan.IsUnderMercenaryService;

                    // Skip initial enlistment (joining lord's kingdom as mercenary during StartEnlist)
                    if (isInitialEnlistment)
                    {
                        return; // Don't process - this is normal enlistment flow
                    }

                    // Handle honorable discharge cases:
                    // 1. Promoted to vassal in same kingdom (active enlistment)
                    // 2. On leave and became vassal of same kingdom
                    if (isPromotionToVassal || isOnLeaveBecameVassal)
                    {
                        ModLogger.Info("ENLISTMENT",
                            $"Player became vassal of {newKingdom.Name} while {(IsEnlisted ? "enlisted" : "on leave")} - ending enlistment honorably");

                        // Clear leave state if on leave
                        if (_isOnLeave)
                        {
                            _isOnLeave = false;
                            _leaveStartDate = CampaignTime.Zero;
                        }

                        // End enlistment honorably since player elevated their service
                        StopEnlist($"Player became vassal of {newKingdom.Name}", isHonorableDischarge: true);

                        var message = new TextObject(
                            "{=Enlisted_Message_BecameVassal}You have become a vassal of {KINGDOM}. Your enlistment has ended honorably.");
                        _ = message.SetTextVariable("KINGDOM", newKingdom.Name);
                        InformationManager.DisplayMessage(new InformationMessage(message.ToString()));

                        return; // Early return - don't process grace period logic
                    }

                    // Handle desertion cases:
                    // 1. Active enlistment + joined different kingdom
                    // 2. On leave + joined different kingdom
                    // 3. On leave + joined same kingdom as mercenary (they left and came back, breaking service commitment)
                    if (isKingdomTransfer || isOnLeaveDifferentKingdom || isOnLeaveSameKingdomAsMercenary)
                    {
                        ModLogger.Info("Desertion",
                            $"Player joined different kingdom {newKingdom.Name} while {(IsEnlisted ? "enlisted" : "on leave")} - treating as desertion");

                        // Clear leave state if on leave
                        if (_isOnLeave)
                        {
                            _isOnLeave = false;
                            _leaveStartDate = CampaignTime.Zero;
                        }

                        // Set pending desertion kingdom to the original lord's kingdom
                        // lordKingdom is guaranteed non-null here due to the conditions above
                        _pendingDesertionKingdom = lordKingdom;

                        // Apply desertion penalties
                        ApplyDesertionPenalties();

                        // Fire event for other mods (before StopEnlist clears state)
                        OnLeaveEnded?.Invoke();

                        // End enlistment as desertion
                        StopEnlist($"Player joined different kingdom {newKingdom.Name} while {(IsEnlisted ? "enlisted" : "on leave")} - desertion", isHonorableDischarge: false);

                        var desertionMessage = new TextObject(
                            "{=Enlisted_Message_DesertedToOtherKingdom}You have deserted {ORIGINAL_KINGDOM} by joining {NEW_KINGDOM}. You have been branded a deserter.");
                        _ = desertionMessage.SetTextVariable("ORIGINAL_KINGDOM", lordKingdom.Name);
                        _ = desertionMessage.SetTextVariable("NEW_KINGDOM", newKingdom.Name);
                        InformationManager.DisplayMessage(new InformationMessage(desertionMessage.ToString()));

                        return; // Early return - don't process grace period logic
                    }
                }
            }

            // Edge case: If player changes kingdoms during grace period, clear it
            // Player can only rejoin the pending desertion kingdom during grace period
            // Any kingdom change (join different kingdom, become independent) invalidates grace period
            if (IsInDesertionGracePeriod)
            {
                // If player left the grace period kingdom (became independent or joined different kingdom), clear grace period
                if (oldKingdom == _pendingDesertionKingdom && newKingdom != _pendingDesertionKingdom)
                {
                    if (newKingdom == null)
                    {
                        ModLogger.Info("Desertion", "Player left kingdom during grace period - clearing grace period");
                    }
                    else
                    {
                        ModLogger.Info("Desertion",
                            $"Player changed kingdoms during grace period - clearing grace period (left: {oldKingdom.Name}, joined: {newKingdom.Name})");
                    }

                    ClearDesertionGracePeriod();
                }
                // If player is joining the grace period kingdom from a different kingdom, that's okay (rejoin during grace period)
                // This is handled in StartEnlist() which clears the grace period when rejoining
            }
        }

        /// <summary>
        ///     Handle kingdom destruction.
        ///     Edge case: If pending desertion kingdom is destroyed during grace period, clear grace period.
        ///     Player can no longer rejoin a destroyed kingdom, so grace period is invalid.
        /// </summary>
        private void OnKingdomDestroyed(Kingdom kingdom)
        {
            if (IsInDesertionGracePeriod && kingdom == _pendingDesertionKingdom)
            {
                ModLogger.Info("Desertion",
                    $"Kingdom {kingdom.Name} destroyed during grace period - clearing grace period");
                ClearDesertionGracePeriod();

                // Notify player that grace period is cancelled due to kingdom destruction
                var message =
                    new TextObject("{=enlist_kingdom_fallen}The kingdom you served has fallen. Your grace period has been cancelled.");
                InformationManager.DisplayMessage(new InformationMessage(message.ToString()));
            }
        }

        /// <summary>
        ///     Add XP to player's military progression.
        ///     Called by duties system and other progression sources.
        ///     Triggers promotion notifications when XP thresholds are reached.
        /// </summary>
        public void AddEnlistmentXP(int xp, string source = "General")
        {
            if (!IsEnlisted || xp <= 0)
            {
                return;
            }

            var previousXP = _enlistmentXP;
            _enlistmentXP += xp;

            // Track XP by source for muster period summary display
            if (_xpSourcesThisPeriod == null)
            {
                _xpSourcesThisPeriod = new Dictionary<string, int>();
            }

            if (_xpSourcesThisPeriod.ContainsKey(source))
            {
                _xpSourcesThisPeriod[source] += xp;
            }
            else
            {
                _xpSourcesThisPeriod[source] = xp;
            }

            // Get tier requirements to show progress
            var tierXP = ConfigurationManager.GetTierXpRequirements();
            var nextTierXP = _enlistmentTier < tierXP.Length ? tierXP[_enlistmentTier] : tierXP[tierXP.Length - 1];
            var progressPercent = nextTierXP > 0 ? _enlistmentXP * 100 / nextTierXP : 100;

            ModLogger.Info("XP",
                $"+{xp} XP from {source} | Total: {_enlistmentXP}/{nextTierXP} ({progressPercent}% to Tier {_enlistmentTier + 1})");

            // Track XP for summary
            ModLogger.IncrementSummary("xp_earned", 1, xp);

            // Fire event for other mods
            OnXPGained?.Invoke(xp, source);

            // Check if we crossed a promotion threshold
            CheckPromotionNotification(previousXP, _enlistmentXP);
        }

        /// <summary>
        /// Returns XP earned this muster period, grouped by source.
        /// Used by the muster menu to display an XP breakdown showing where XP came from.
        /// </summary>
        public Dictionary<string, int> GetXPSourcesThisPeriod()
        {
            return _xpSourcesThisPeriod ?? new Dictionary<string, int>();
        }

        /// <summary>
        /// Clears the XP source tracking dictionary after muster completes.
        /// Called when transitioning to the next muster period.
        /// </summary>
        public void ResetXPSources()
        {
            _xpSourcesThisPeriod?.Clear();
            ModLogger.Debug("Muster", "XP sources reset for new muster period");
        }

        /// <summary>
        /// Updates muster tracking state when a muster cycle completes.
        /// Caches current tier and XP for detecting promotions and calculating period gains.
        /// </summary>
        public void OnMusterComplete()
        {
            _tierAtLastMuster = _enlistmentTier;
            _xpAtLastMuster = _enlistmentXP;
            _lastMusterDay = Campaign.Current != null ? (int)CampaignTime.Now.ToDays : 0;
            _lastMusterCompletionTime = CampaignTime.Now;
            ResetXPSources();

            ModLogger.Info("Muster",
                $"Muster complete: cached tier {_tierAtLastMuster}, XP {_xpAtLastMuster}, day {_lastMusterDay}");
        }

        /// <summary>
        /// Increment the battles survived counter. Called after successfully completing a battle.
        /// </summary>
        public void IncrementBattlesSurvived()
        {
            if (!IsEnlisted)
            {
                return;
            }
            _battlesSurvived++;
            ModLogger.Debug("ENLISTMENT", $"Battles survived: {_battlesSurvived}");
        }

        /// <summary>
        ///     Set tier directly (called by PromotionBehavior for immediate promotion).
        ///     V2.0: Companions now managed from T1. Commander retinue granted at T7/T8/T9.
        /// </summary>
        public void SetTier(int tier)
        {
            // V2.0: Support T1-T9 (was T1-T6)
            if (!IsEnlisted || tier < 1 || tier > 9)
            {
                ModLogger.Debug("ENLISTMENT", $"SetTier rejected - Enlisted: {IsEnlisted}, Tier: {tier}");
                return;
            }

            var previousTier = _enlistmentTier;
            if (previousTier == tier)
            {
                return;
            }

            _enlistmentTier = tier;

            // Track promotion date for the days-in-rank requirement and muster recap display.
            if (tier > previousTier)
            {
                _lastPromotionDate = CampaignTime.Now;
                _dayOfLastPromotion = Campaign.Current != null
                    ? (int)CampaignTime.Now.ToDays
                    : 0;
            }

            int campaignDay = Campaign.Current?.Models?.CampaignTimeModel != null
                ? (int)Campaign.Current.Models.CampaignTimeModel.CampaignStartTime.ElapsedDaysUntilNow
                : 0;
            ModLogger.Info("ENLISTMENT", $"Tier changed: {previousTier} -> {tier} (XP: {_enlistmentXP}, campaign day: {campaignDay})");

            // V2.0: Companions are now managed from T1 (enlistment start), not T4
            // Legacy reclaim kept for backward compatibility with older saves
            if (previousTier < 4 && tier >= 4)
            {
                ReclaimCompanionsFromLord();
            }

            // V2.0: Grant commander retinue when crossing into T7/T8/T9
            // Recruits are auto-granted based on player's formation and lord's culture
            if (tier >= 7 && previousTier < tier)
            {
                try
                {
                    RetinueRecruitmentGrant.GrantCommanderRetinue(tier, previousTier);
                }
                catch (Exception ex)
                {
                    ModLogger.Caught("ENLISTMENT", "Failed to grant commander retinue on tier change", ex);
                }
            }

            try
            {
                OnTierChanged?.Invoke(previousTier, tier);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("ENLISTMENT", "OnTierChanged listener threw", ex);
            }
        }

        // ========================================================================
        // QUARTERMASTER HERO METHODS
        // ========================================================================

        /// <summary>
        ///     Get the current quartermaster Hero, creating one if necessary.
        ///     Returns null if not enlisted or if hero creation fails.
        /// </summary>
        public Hero GetOrCreateQuartermaster()
        {
            if (!IsEnlisted || _enlistedLord == null)
            {
                return null;
            }

            // Check if we need to create a new quartermaster
            if (_quartermasterHero == null || _quartermasterHero.IsDead)
            {
                _quartermasterHero = CreateQuartermasterForLord();

                if (_quartermasterHero != null)
                {
                    _hasMetQuartermaster = false; // Reset for first meeting
                }
            }

            return _quartermasterHero;
        }

        /// <summary>
        ///     Creates a new quartermaster Hero for the current enlisted lord.
        ///     The quartermaster has a culture-appropriate name, appearance, equipment,
        ///     and a randomly assigned personality archetype.
        /// </summary>
        private Hero CreateQuartermasterForLord()
        {
            if (_enlistedLord == null)
            {
                ModLogger.Expected("QUARTERMASTER", "qm_create_no_lord",
                    "Cannot create quartermaster: no enlisted lord");
                return null;
            }

            try
            {
                var culture = _enlistedLord.Culture;
                if (culture == null)
                {
                    ModLogger.Expected("QUARTERMASTER", "qm_create_no_culture",
                        "Cannot create quartermaster: lord has no culture");
                    return null;
                }

                // 1. Get culture-appropriate troop template (sergeant/veteran tier)
                var template = GetSergeantTierTroopTemplate(culture);
                if (template == null)
                {
                    ModLogger.Surfaced("QUARTERMASTER", "Cannot find troop template for culture - quartermaster hero creation will fail", null);
                    return null;
                }

                // 2. Get birth settlement (fallback chain for safety)
                var birthSettlement = _enlistedLord.HomeSettlement
                    ?? _enlistedLord.BornSettlement
                    ?? Settlement.All.FirstOrDefault(s => s.Culture == culture && s.IsTown);

                // 3. Create the Hero using native HeroCreator
                var qm = HeroCreator.CreateSpecialHero(
                    template,
                    birthSettlement,
                    _enlistedLord.Clan,
                    null, // No spouse
                    MBRandom.RandomInt(35, 55) // Experienced NCO age
                );

                if (qm == null)
                {
                    ModLogger.Expected("QUARTERMASTER", "qm_create_hero_null", "HeroCreator.CreateSpecialHero returned null");
                    return null;
                }

                // 4. Set Hero properties
                // Use Occupation.Soldier (not Wanderer) to prevent vanilla wanderer
                // dialogue gating from firing against the QM. The engine gates the
                // wanderer-introduction dialogue on Occupation == Wanderer — see
                // LordConversationsCampaignBehavior.cs:1274 (conversation_wanderer_on_condition)
                // and :607 (AddWandererConversations) in the developer-local decompile.
                // See AGENTS.md "Common Pitfalls" item 11.
                qm.SetNewOccupation(Occupation.Soldier);
                qm.HiddenInEncyclopedia = true; // Don't clutter encyclopedia
                qm.IsKnownToPlayer = true;
                // Note: HasMet is read-only, but IsKnownToPlayer triggers the met status

                // 4.5. Visuals: quartermasters should look like a wealthy camp official, not a line soldier.
                // We keep battle equipment intact (safety: avoid spawning a helpless hero in rare edge cases),
                // but overwrite CIVILIAN equipment with the richest culture-appropriate civilian outfit we can find.
                TryApplyQuartermasterWealthyCulturalAttire(qm, culture);

                // 5. Place in lord's party (stays in baggage train)
                if (_enlistedLord.PartyBelongedTo != null)
                {
                    qm.ChangeHeroGold(-qm.Gold); // Remove default gold
                    EnterSettlementAction.ApplyForCharacterOnly(qm, birthSettlement);
                }

                // 6. Assign personality archetype based on culture
                _quartermasterArchetype = SelectArchetypeForCulture(culture.StringId);
                _quartermasterRelationship = 0; // Start neutral
                _hasMetQuartermaster = false;

                ModLogger.Info("QUARTERMASTER",
                    $"Created quartermaster '{qm.Name}' ({_quartermasterArchetype}) for lord {_enlistedLord.Name}");

                return qm;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTER", "Failed to create quartermaster Hero", ex);
                return null;
            }
        }

        private static void TryApplyQuartermasterWealthyCulturalAttire(Hero qm, CultureObject culture)
        {
            try
            {
                if (qm?.CivilianEquipment == null || culture == null)
                {
                    return;
                }

                // Candidate civilian templates: “wealthy” town/camp types.
                // Note: We do not rely on string ids (mod compatibility); we rely on Occupation + culture.
                var candidates = CharacterObject.All
                    .Where(t => t != null
                                && !t.IsHero
                                && t.Culture == culture
                                && (t.Occupation == Occupation.Merchant ||
                                    t.Occupation == Occupation.Artisan ||
                                    t.Occupation == Occupation.Preacher ||
                                    t.Occupation == Occupation.Gangster))
                    .ToList();

                if (candidates.Count == 0)
                {
                    // Fallback: any non-soldier civilian template of the culture.
                    candidates = CharacterObject.All
                        .Where(t => t != null
                                    && !t.IsHero
                                    && t.Culture == culture
                                    && t.Occupation != Occupation.Soldier)
                        .ToList();
                }

                if (candidates.Count == 0)
                {
                    return;
                }

                CharacterObject best = null;
                var bestScore = -1;
                foreach (var candidate in candidates)
                {
                    var score = ScoreCivilianOutfit(candidate?.FirstCivilianEquipment);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = candidate;
                    }
                }

                if (best == null || bestScore <= 0)
                {
                    return;
                }

                // Apply the outfit.
                qm.CivilianEquipment.FillFrom(best.FirstCivilianEquipment, false);

                // Keep the look “official” (no visible civilian weapons unless the template insists).
                qm.CivilianEquipment[EquipmentIndex.Weapon0] = default;
                qm.CivilianEquipment[EquipmentIndex.Weapon1] = default;
                qm.CivilianEquipment[EquipmentIndex.Weapon2] = default;
                qm.CivilianEquipment[EquipmentIndex.Weapon3] = default;

                ModLogger.Info("QUARTERMASTER",
                    $"Quartermaster attire: applied wealthy civilian outfit from '{best.StringId}' (score={bestScore}) for culture '{culture.StringId}'");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTER", "Failed to apply wealthy quartermaster attire", ex);
            }
        }

        private static int ScoreCivilianOutfit(TaleWorlds.Core.Equipment equipment)
        {
            try
            {
                if (equipment == null)
                {
                    return 0;
                }

                // Focus on attire slots; treat item value as a decent “wealth” proxy.
                var slots = new[]
                {
                    EquipmentIndex.Head,
                    EquipmentIndex.Body,
                    EquipmentIndex.Leg,
                    EquipmentIndex.Gloves,
                    EquipmentIndex.Cape
                };

                var score = 0;
                foreach (var slot in slots)
                {
                    var item = equipment[slot].Item;
                    if (item != null)
                    {
                        score += Math.Max(0, item.Value);
                    }
                }

                return score;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        ///     Gets a sergeant/veteran tier troop template from the culture.
        ///     Used as the base for quartermaster appearance and skills.
        /// </summary>
        private CharacterObject GetSergeantTierTroopTemplate(CultureObject culture)
        {
            // Find tier 3-4 infantry troops from the lord's culture
            var eligibleTroops = CharacterObject.All
                .Where(t => t.Culture == culture
                         && !t.IsHero
                         && t.Tier >= 3 && t.Tier <= 4
                         && t.Occupation == Occupation.Soldier
                         && t.DefaultFormationClass == FormationClass.Infantry)
                .ToList();

            if (eligibleTroops.Count == 0)
            {
                // Fallback: any tier 3-4 troop
                eligibleTroops = CharacterObject.All
                    .Where(t => t.Culture == culture
                             && !t.IsHero
                             && t.Tier >= 3 && t.Tier <= 4)
                    .ToList();
            }

            if (eligibleTroops.Count == 0)
            {
                // Last resort: basic troop
                ModLogger.Warn("QUARTERMASTER", $"No sergeant-tier troops found for {culture.StringId}, using basic troop");
                return culture.BasicTroop;
            }

            return eligibleTroops.GetRandomElement();
        }

        /// <summary>
        ///     Selects a personality archetype based on culture with weighted randomization.
        ///     Different cultures favor different personality types.
        /// </summary>
        private string SelectArchetypeForCulture(string cultureId)
        {
            var archetypes = new[] { "veteran", "merchant", "bookkeeper", "scoundrel", "believer", "eccentric" };

            // Culture-specific weights (from quartermaster_hero_update.md)
            // Format: veteran, merchant, bookkeeper, scoundrel, believer, eccentric
            var weights = cultureId?.ToLowerInvariant() switch
            {
                "empire" => new[] { 30, 20, 35, 3, 10, 2 },    // Bureaucratic empire favors bookkeepers
                "vlandia" => new[] { 25, 30, 20, 7, 15, 3 },   // Mercantile culture favors merchants
                "sturgia" => new[] { 40, 10, 5, 5, 25, 15 },   // Warrior culture favors veterans
                "battania" => new[] { 20, 7, 3, 30, 15, 25 },  // Tribal culture favors scoundrels/eccentrics
                "khuzait" => new[] { 20, 30, 3, 30, 7, 10 },   // Trade/raid culture
                "aserai" => new[] { 15, 35, 3, 15, 30, 2 },    // Merchant/faith culture
                _ => new[] { 30, 20, 15, 10, 15, 10 }          // Mercenary/default
            };

            // Weighted random selection
            var totalWeight = weights.Sum();
            var roll = MBRandom.RandomInt(0, totalWeight);
            var cumulative = 0;

            for (var i = 0; i < archetypes.Length; i++)
            {
                cumulative += weights[i];
                if (roll < cumulative)
                {
                    return archetypes[i];
                }
            }

            return "veteran"; // Fallback
        }

        /// <summary>
        ///     Modifies the relationship level with the quartermaster.
        /// </summary>
        /// <param name="change">Amount to change (positive or negative)</param>
        public void ModifyQuartermasterRelationship(int change)
        {
            _quartermasterRelationship = Math.Max(0, Math.Min(100, _quartermasterRelationship + change));
            ModLogger.Debug("QUARTERMASTER", $"Relationship changed by {change}, now {_quartermasterRelationship}");
        }

        /// <summary>
        ///     Marks the quartermaster as met (after first conversation).
        /// </summary>
        public void MarkQuartermasterMet()
        {
            _hasMetQuartermaster = true;
            ModLogger.Debug("QUARTERMASTER", "Player has met the quartermaster");
        }

        /// <summary>
        ///     Sets the player's communication style with the quartermaster.
        ///     Called during first meeting introduction when player chooses their tone.
        /// </summary>
        /// <param name="style">Style value: "direct", "military", "friendly", "flattering"</param>
        /// <param name="repChange">Reputation change from choosing this style</param>
        public void SetQMPlayerStyle(string style, int repChange)
        {
            _qmPlayerStyle = style;
            if (repChange != 0)
            {
                ModifyQuartermasterRelationship(repChange);
            }
            ModLogger.Info("QUARTERMASTER", $"Player style set to '{style}' with rep change {repChange}");
        }

        /// <summary>
        ///     Gets the relationship level name for UI display.
        /// </summary>
        public string GetQuartermasterRelationshipLevel()
        {
            return _quartermasterRelationship switch
            {
                >= 80 => "Battle Brother",
                >= 60 => "Respected",
                >= 40 => "Trusted",
                >= 20 => "Known",
                _ => "Stranger"
            };
        }

        /// <summary>
        ///     Gets the quartermaster discount percentage based on relationship.
        ///     High relationship provides discounts on equipment and rations.
        /// </summary>
        /// <returns>Discount percentage (0-15)</returns>
        public int GetQuartermasterDiscount()
        {
            return _quartermasterRelationship switch
            {
                >= 80 => 15, // 15% discount for Battle Brothers
                >= 60 => 10, // 10% discount for Respected
                >= 40 => 5,  // 5% discount for Trusted
                _ => 0       // No discount below Trusted
            };
        }

        /// <summary>
        ///     Applies the quartermaster discount to a price.
        /// </summary>
        /// <param name="basePrice">Original price</param>
        /// <returns>Discounted price</returns>
        public int ApplyQuartermasterDiscount(int basePrice)
        {
            var discount = GetQuartermasterDiscount();
            if (discount <= 0)
            {
                return basePrice;
            }

            var reduction = (int)(basePrice * discount / 100f);
            return Math.Max(1, basePrice - reduction);
        }

        /// <summary>
        ///     Gets relationship milestone info for display.
        ///     Returns (nextMilestone, pointsNeeded, reward) tuple.
        /// </summary>
        public (int NextMilestone, int PointsNeeded, string Reward) GetRelationshipMilestoneInfo()
        {
            var current = _quartermasterRelationship;

            if (current < 20)
            {
                return (20, 20 - current, "Unlocks: Chat option");
            }
            else if (current < 40)
            {
                return (40, 40 - current, "Unlocks: 5% discount, Black market access");
            }
            else if (current < 60)
            {
                return (60, 60 - current, "Unlocks: 10% discount");
            }
            else if (current < 80)
            {
                return (80, 80 - current, "Unlocks: 15% discount, Special items");
            }
            else
            {
                return (100, 100 - current, "Maximum relationship");
            }
        }

        /// <summary>
        ///     Gets the current QM reputation using the native Hero relationship system.
        ///     Returns a value in the range -100 to +100, clamped to the mod's effective range of -50 to +100.
        /// </summary>
        public int GetQMReputation()
        {
            var qm = GetOrCreateQuartermaster();
            if (qm == null)
            {
                return 0;
            }

            int nativeRep = Hero.MainHero.GetRelation(qm);
            return Math.Max(-50, Math.Min(100, nativeRep));
        }

        /// <summary>
        ///     Returns the price multiplier for equipment purchases based on QM reputation.
        ///     Range: 0.70 (30% discount at high rep) to 1.40 (40% markup at low rep).
        /// </summary>
        public float GetEquipmentPriceMultiplier()
        {
            int rep = GetQMReputation();
            if (rep >= 65)
            {
                return 0.70f;  // 30% discount
            }
            if (rep >= 35)
            {
                return 0.80f;  // 20% discount
            }
            if (rep >= 10)
            {
                return 0.90f;  // 10% discount
            }
            if (rep >= -10)
            {
                return 1.00f; // Standard price
            }
            if (rep >= -25)
            {
                return 1.20f; // 20% markup
            }
            return 1.40f;                  // 40% markup
        }

        /// <summary>
        ///     Returns the buyback multiplier based on QM reputation.
        ///     Range: 0.30 (hostile) to 0.65 (trusted).
        ///     Always less than purchase multiplier to prevent profit exploits.
        /// </summary>
        public float GetBuybackMultiplier()
        {
            int rep = GetQMReputation();
            if (rep >= 65)
            {
                return 0.65f;
            }
            if (rep >= 35)
            {
                return 0.60f;
            }
            if (rep >= 10)
            {
                return 0.55f;
            }
            if (rep >= -10)
            {
                return 0.50f;
            }
            if (rep >= -25)
            {
                return 0.40f;
            }
            return 0.30f;
        }

        /// <summary>
        ///     Returns the price multiplier for officer provisions (T5+ food shop).
        ///     Range: 1.50 (floor at high rep) to 2.00 (hostile rep).
        ///     Officer food is always a premium and never cheaper than 150% of town price.
        /// </summary>
        public float GetOfficerFoodPriceMultiplier()
        {
            int rep = GetQMReputation();
            if (rep >= 61)
            {
                return 1.50f;  // Best price (still 150% of town)
            }
            if (rep >= 31)
            {
                return 1.75f;
            }
            if (rep >= 1)
            {
                return 1.90f;
            }
            return 2.00f;                  // Hostile/neutral: 200% of town
        }

        /// <summary>
        ///     Cleans up quartermaster state when service ends.
        /// </summary>
        private void CleanupQuartermasterOnServiceEnd()
        {
            // Quartermaster stays with the lord's army when player leaves
            // Just null out our reference
            _quartermasterHero = null;
            _quartermasterArchetype = null;
            _quartermasterRelationship = 0;
            _hasMetQuartermaster = false;

            ModLogger.Info("QUARTERMASTER", "Quartermaster reference cleared on service end");
        }

        // ========================================================================
        // FOOD/RATIONS SYSTEM METHODS
        // ========================================================================

        /// <summary>
        ///     Food quality tier enumeration for rations system.
        ///     Values correspond to _currentFoodQuality field.
        /// </summary>
        public enum FoodQualityTier
        {
            /// <summary>Standard army rations - no bonus.</summary>
            Standard = 0,
            /// <summary>Supplemental rations - 1 day duration.</summary>
            Supplemental = 1,
            /// <summary>Officer's fare - 2 days duration.</summary>
            Officer = 2,
            /// <summary>Commander's feast - 3 days duration.</summary>
            Commander = 3
        }

        /// <summary>
        ///     Gets the current food quality tier.
        ///     Automatically returns Standard if the current quality has expired.
        /// </summary>
        public FoodQualityTier CurrentFoodQuality
        {
            get
            {
                // Check if expired
                if (_currentFoodQuality > 0 && CampaignTime.Now >= _foodQualityExpires)
                {
                    _currentFoodQuality = 0;
                    _foodQualityExpires = CampaignTime.Zero;
                    ModLogger.Debug("Food", "Food quality bonus expired");
                }
                return (FoodQualityTier)_currentFoodQuality;
            }
        }

        /// <summary>
        ///     Gets the time remaining on the current food quality bonus.
        ///     Returns zero if no bonus active or expired.
        /// </summary>
        public CampaignTime FoodQualityTimeRemaining
        {
            get
            {
                if (_currentFoodQuality <= 0 || CampaignTime.Now >= _foodQualityExpires)
                {
                    return CampaignTime.Zero;
                }
                return _foodQualityExpires - CampaignTime.Now;
            }
        }



        /// <summary>
        ///     Gets food quality information for display.
        /// </summary>
        public (string Name, float DaysRemaining) GetFoodQualityInfo()
        {
            var quality = CurrentFoodQuality;
            var remaining = FoodQualityTimeRemaining;
            var daysRemaining = (float)remaining.ToDays;

            var name = quality switch
            {
                FoodQualityTier.Supplemental => "Supplemental Rations",
                FoodQualityTier.Officer => "Officer's Fare",
                FoodQualityTier.Commander => "Commander's Feast",
                _ => "Standard Rations"
            };

            return (name, daysRemaining);
        }

        /// <summary>
        ///     Purchases rations of the specified quality tier.
        ///     Deducts gold and sets duration.
        /// </summary>
        /// <param name="tier">The food quality tier to purchase</param>
        /// <param name="cost">Gold cost (validated before calling)</param>
        /// <param name="durationDays">How many days the bonus lasts</param>
        /// <returns>True if purchase successful, false if insufficient funds</returns>
        public bool PurchaseRations(FoodQualityTier tier, int cost, int durationDays)
        {
            if (!IsEnlisted)
            {
                ModLogger.Warn("Food", "Cannot purchase rations: not enlisted");
                return false;
            }

            // Relationship discount is part of the Quartermaster Hero system.
            // Apply it here so *all* rations purchases respect the relationship tier.
            var effectiveCost = ApplyQuartermasterDiscount(cost);

            if (Hero.MainHero.Gold < effectiveCost)
            {
                ModLogger.Debug("Food", $"Cannot purchase rations: insufficient gold ({Hero.MainHero.Gold} < {effectiveCost})");
                return false;
            }

            // Deduct gold
            Hero.MainHero.ChangeHeroGold(-effectiveCost);

            // Set quality and duration
            _currentFoodQuality = (int)tier;
            _foodQualityExpires = CampaignTime.Now + CampaignTime.Days(durationDays);

            // Increase quartermaster relationship
            ModifyQuartermasterRelationship(2);

            var tierName = tier switch
            {
                FoodQualityTier.Supplemental => "Supplemental Rations",
                FoodQualityTier.Officer => "Officer's Fare",
                FoodQualityTier.Commander => "Commander's Feast",
                _ => "Rations"
            };

            ModLogger.Info("Food",
                $"Purchased {tierName} for {effectiveCost}g (base {cost}g), duration {durationDays} days");

            return true;
        }

        /// <summary>
        ///     Clears food quality bonus (used when service ends).
        /// </summary>
        private void ClearFoodQuality()
        {
            _currentFoodQuality = 0;
            _foodQualityExpires = CampaignTime.Zero;
        }

        // ========================================================================
        // RETINUE PROVISIONING SYSTEM METHODS
        // ========================================================================

        /// <summary>
        ///     Retinue provisioning tier enumeration.
        ///     Higher tiers provide better provisioning quality but cost more per soldier.
        /// </summary>
        public enum RetinueProvisioningTier
        {
            /// <summary>No provisions - starvation penalties apply.</summary>
            None = 0,
            /// <summary>Bare minimum rations - poor quality.</summary>
            BareMinimum = 1,
            /// <summary>Standard army rations - adequate quality.</summary>
            Standard = 2,
            /// <summary>Good fare - good quality.</summary>
            GoodFare = 3,
            /// <summary>Officer quality - excellent quality.</summary>
            OfficerQuality = 4
        }

        /// <summary>
        ///     Gets the current retinue provisioning tier.
        ///     Returns None if expired.
        /// </summary>
        public RetinueProvisioningTier CurrentRetinueProvisioning
        {
            get
            {
                // Check if expired
                if (_retinueProvisioningTier > 0 && CampaignTime.Now >= _retinueProvisioningExpires)
                {
                    _retinueProvisioningTier = 0;
                    _retinueProvisioningExpires = CampaignTime.Zero;
                    ModLogger.Debug("RetinueFood", "Retinue provisioning expired");
                }
                return (RetinueProvisioningTier)_retinueProvisioningTier;
            }
        }

        /// <summary>
        ///     Gets the time remaining on retinue provisions.
        /// </summary>
        public CampaignTime RetinueProvisioningTimeRemaining
        {
            get
            {
                if (_retinueProvisioningTier <= 0 || CampaignTime.Now >= _retinueProvisioningExpires)
                {
                    return CampaignTime.Zero;
                }
                return _retinueProvisioningExpires - CampaignTime.Now;
            }
        }

        /// <summary>
        ///     Gets the provisioning quality level from current retinue provisioning.
        /// </summary>
        /// <returns>Provisioning quality modifier (-10 to +10)</returns>
        public int GetRetinueProvisioningQuality()
        {
            var tier = CurrentRetinueProvisioning;
            return tier switch
            {
                RetinueProvisioningTier.None => -10,       // Starvation
                RetinueProvisioningTier.BareMinimum => -5, // Poor quality
                RetinueProvisioningTier.Standard => 0,     // Adequate
                RetinueProvisioningTier.GoodFare => 5,     // Good quality
                RetinueProvisioningTier.OfficerQuality => 10, // Excellent quality
                _ => 0
            };
        }

        /// <summary>
        ///     Gets the cost per soldier for a provisioning tier.
        /// </summary>
        public static int GetProvisioningCostPerSoldier(RetinueProvisioningTier tier)
        {
            return tier switch
            {
                RetinueProvisioningTier.BareMinimum => 2,    // 2g per soldier per week
                RetinueProvisioningTier.Standard => 5,       // 5g per soldier per week
                RetinueProvisioningTier.GoodFare => 10,      // 10g per soldier per week
                RetinueProvisioningTier.OfficerQuality => 20, // 20g per soldier per week
                _ => 0
            };
        }

        /// <summary>
        ///     Gets the total cost to provision the retinue for one week.
        /// </summary>
        /// <param name="tier">Provisioning tier</param>
        /// <param name="soldierCount">Number of soldiers (from RetinueManager)</param>
        public static int GetRetinueProvisioningCost(RetinueProvisioningTier tier, int soldierCount)
        {
            return GetProvisioningCostPerSoldier(tier) * soldierCount;
        }

        /// <summary>
        ///     Gets retinue provisioning information for display.
        /// </summary>
        public (string Name, int QualityLevel, float DaysRemaining) GetRetinueProvisioningInfo()
        {
            var tier = CurrentRetinueProvisioning;
            var remaining = RetinueProvisioningTimeRemaining;
            var daysRemaining = (float)remaining.ToDays;

            var name = tier switch
            {
                RetinueProvisioningTier.BareMinimum => "Bare Minimum",
                RetinueProvisioningTier.Standard => "Standard Rations",
                RetinueProvisioningTier.GoodFare => "Good Fare",
                RetinueProvisioningTier.OfficerQuality => "Officer Quality",
                _ => "Not Provisioned"
            };

            return (name, GetRetinueProvisioningQuality(), daysRemaining);
        }

        /// <summary>
        ///     Purchases retinue provisions for one week.
        /// </summary>
        /// <param name="tier">The provisioning tier to purchase</param>
        /// <param name="soldierCount">Number of soldiers to provision</param>
        /// <returns>True if purchase successful</returns>
        public bool PurchaseRetinueProvisioning(RetinueProvisioningTier tier, int soldierCount)
        {
            if (!IsEnlisted || _enlistmentTier < 7)
            {
                ModLogger.Warn("RetinueFood", "Cannot provision retinue: not a commander (T7+)");
                return false;
            }

            var baseCost = GetRetinueProvisioningCost(tier, soldierCount);
            var cost = ApplyQuartermasterDiscount(baseCost);

            if (Hero.MainHero.Gold < cost)
            {
                ModLogger.Debug("RetinueFood", $"Cannot provision: insufficient gold ({Hero.MainHero.Gold} < {cost})");
                return false;
            }

            // Deduct gold
            Hero.MainHero.ChangeHeroGold(-cost);

            // Set provisioning tier and duration (7 days)
            _retinueProvisioningTier = (int)tier;
            _retinueProvisioningExpires = CampaignTime.Now + CampaignTime.Days(7);
            _retinueProvisioningWarningShown = false;

            // Increase quartermaster relationship
            ModifyQuartermasterRelationship(3);

            var tierName = tier switch
            {
                RetinueProvisioningTier.BareMinimum => "Bare Minimum",
                RetinueProvisioningTier.Standard => "Standard",
                RetinueProvisioningTier.GoodFare => "Good Fare",
                RetinueProvisioningTier.OfficerQuality => "Officer Quality",
                _ => "provisions"
            };

            ModLogger.Info("RetinueFood",
                $"Purchased {tierName} provisioning for {soldierCount} soldiers, cost {cost}g (base {baseCost}g), 7 days");

            return true;
        }

        /// <summary>
        ///     Checks retinue provisioning status and displays warnings.
        ///     Called from DailyTick.
        /// </summary>
        public void CheckRetinueProvisioningStatus()
        {
            // Only applies to T7+ commanders with retinue
            if (!IsEnlisted || _enlistmentTier < 7)
            {
                return;
            }

            var retinueManager = RetinueManager.Instance;
            if (retinueManager?.State == null || retinueManager.State.TotalSoldiers <= 0)
            {
                return;
            }

            var remaining = RetinueProvisioningTimeRemaining;
            var daysRemaining = remaining.ToDays;

            // Check for starvation (no provisions)
            if (_retinueProvisioningTier <= 0 || CampaignTime.Now >= _retinueProvisioningExpires)
            {
                // Starvation - apply penalties
                InformationManager.DisplayMessage(new InformationMessage(
                    "Your retinue is starving! Visit the Quartermaster to provision them.",
                    Colors.Red));
                return;
            }

            // Check for low provisions warning (2 days or less)
            if (daysRemaining <= 2 && !_retinueProvisioningWarningShown)
            {
                _retinueProvisioningWarningShown = true;
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Warning: Retinue provisions will run out in {daysRemaining:F1} days!",
                    Colors.Yellow));
            }
        }

        /// <summary>
        ///     Clears retinue provisioning (used when service ends or retinue disbanded).
        /// </summary>
        public void ClearRetinueProvisioning()
        {
            _retinueProvisioningTier = 0;
            _retinueProvisioningExpires = CampaignTime.Zero;
            _retinueProvisioningWarningShown = false;
        }

        /// <summary>
        ///     Handles retinue soldiers when service ends (EC2).
        ///     Honorable discharge: Player keeps retinue soldiers in their party.
        ///     Dishonorable discharge: Retinue soldiers return to the lord's forces.
        /// </summary>
        /// <param name="isHonorableDischarge">Whether this is an honorable discharge.</param>
        private void HandleRetinueOnServiceEnd(bool isHonorableDischarge)
        {
            var retinueManager = RetinueManager.Instance;
            var state = retinueManager?.State;

            if (state == null || !state.HasTypeSelected)
            {
                return; // No retinue to handle
            }

            var soldierCount = state.TotalSoldiers;
            var veteranCount = state.NamedVeterans?.Count ?? 0;

            if (isHonorableDischarge)
            {
                // Honorable: Player keeps their soldiers (they're already in MainParty roster)
                // Just clear the retinue tracking state
                ModLogger.Info("Retinue",
                    $"Honorable discharge: Player retains {soldierCount} retinue soldiers and {veteranCount} named veterans");

                // Show notification
                if (soldierCount > 0)
                {
                    var msg = new TextObject("{=enl_ret_keep_soldiers}Your {COUNT} retinue soldiers remain under your command.");
                    _ = msg.SetTextVariable("COUNT", soldierCount);
                    InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Green));
                }
            }
            else
            {
                // Dishonorable: Remove retinue soldiers from player's party
                ModLogger.Info("Retinue",
                    $"Dishonorable discharge: Returning {soldierCount} retinue soldiers to lord's forces");

                if (soldierCount > 0)
                {
                    ReturnRetinueSoldiersToLord(state);

                    var msg = new TextObject("{=enl_ret_lose_soldiers}Your {COUNT} retinue soldiers have been reassigned to your former lord's command.");
                    _ = msg.SetTextVariable("COUNT", soldierCount);
                    InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Red));
                }
            }

            // Clear retinue state in both cases (soldiers are no longer "retinue" - just party members or gone)
            state.Clear();
        }

        /// <summary>
        ///     Removes retinue soldiers from player's party and adds them to lord's party.
        /// </summary>
        private void ReturnRetinueSoldiersToLord(RetinueState state)
        {
            var playerRoster = MobileParty.MainParty?.MemberRoster;
            var lordParty = _enlistedLord?.PartyBelongedTo;

            if (playerRoster == null || state?.TroopCounts == null)
            {
                return;
            }

            foreach (var kvp in state.TroopCounts.ToList())
            {
                var characterId = kvp.Key;
                var count = kvp.Value;

                if (count <= 0)
                {
                    continue;
                }

                var character = CharacterObject.Find(characterId);
                if (character == null)
                {
                    continue;
                }

                // Remove from player's party
                var actualCount = playerRoster.GetTroopCount(character);
                var toRemove = Math.Min(count, actualCount);

                if (toRemove > 0)
                {
                    _ = playerRoster.AddToCounts(character, -toRemove, removeDepleted: true);

                    // Add to lord's party if available
                    if (lordParty?.MemberRoster != null)
                    {
                        _ = lordParty.MemberRoster.AddToCounts(character, toRemove);
                        ModLogger.Debug("Retinue", $"Transferred {toRemove}x {character.Name} to {_enlistedLord.Name}'s party");
                    }
                }
            }
        }

        /// <summary>
        ///     Whether the player has an active retinue that needs provisioning.
        /// </summary>
        public bool HasRetinueToProvision()
        {
            if (!IsEnlisted || _enlistmentTier < 7)
            {
                return false;
            }

            var retinueManager = RetinueManager.Instance;
            return retinueManager?.State?.TotalSoldiers > 0;
        }


        /// <summary>
        ///     Check if promotion notification should be triggered after XP gain.
        ///     Integrates with the troop selection system.
        /// </summary>
        private void CheckPromotionNotification(int previousXP, int currentXP)
        {
            try
            {
                // Load tier XP requirements from progression_config.json
                var tierXPRequirements = EnlistedConfig.GetTierXpRequirements();

                // Get actual max tier from config (e.g., 6 for tiers 1-6)
                var maxTier = EnlistedConfig.GetMaxTier();

                // Bounds check: ensure we don't go beyond array limits
                if (_enlistmentTier < 0 || _enlistmentTier > maxTier)
                {
                    ModLogger.Debug("Progression",
                        $"Skipping promotion check - tier {_enlistmentTier} out of bounds (max: {maxTier})");
                    return;
                }

                // Check if we crossed any promotion threshold up to max tier
                for (var tier = _enlistmentTier; tier < maxTier && tier >= 0; tier++)
                {
                    var requiredXP = tierXPRequirements[tier];

                    // If we crossed from below to above a threshold
                    if (previousXP < requiredXP && currentXP >= requiredXP)
                    {
                        // Troop selection menu removed. Promotions are handled via PromotionBehavior
                        // which triggers proving events and culture-specific notifications.
                        // Player visits Quartermaster manually for equipment after promotion.
                        // The PromotionBehavior.CheckForPromotion() handles the actual tier advancement.
                        ModLogger.Info("Progression", $"XP threshold crossed for tier {tier + 1} - PromotionBehavior will handle");

                        break; // Only notify for the first threshold crossed
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Progression", "Error checking promotion notification", ex);
            }
        }

        /// <summary>
        ///     Apply basic promotion without equipment change (fallback method).
        ///     Used when troop selection system is unavailable.
        /// </summary>
        public void ApplyBasicPromotion(int newTier)
        {
            try
            {
                _enlistmentTier = newTier;
                var rankName = GetRankName(newTier);

                var message =
                    new TextObject("{=enlist_promotion_quartermaster}Promoted to {RANK} (Tier {TIER})! Visit the quartermaster for equipment upgrades.");
                _ = message.SetTextVariable("RANK", rankName);
                _ = message.SetTextVariable("TIER", newTier.ToString());

                InformationManager.DisplayMessage(new InformationMessage(message.ToString()));

                ModLogger.Info("Promotion", $"Basic promotion applied to {rankName} (Tier {newTier})");

                // Fire event for other mods
                OnPromoted?.Invoke(newTier);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Promotion", "Error applying basic promotion", ex);
            }
        }

        /// <summary>
        /// Shows a notification to the player about their experience track and starting tier.
        /// Called at the start of a new enlistment (not during grace period re-entry).
        /// </summary>
        /// <param name="track">The experience track identifier (green, seasoned, veteran).</param>
        /// <param name="startingTier">The tier the player will start at.</param>
        private void ShowExperienceTrackNotification(string track, int startingTier)
        {
            try
            {
                var trackName = ExperienceTrackHelper.GetTrackDisplayName(track);
                var rankName = GetRankName(startingTier);

                var message = new TextObject(
                    "{=Enlisted_Track_Start}Your experience marks you as a {TRACK}. You begin service at {RANK} (Tier {TIER}).");
                _ = message.SetTextVariable("TRACK", trackName);
                _ = message.SetTextVariable("RANK", rankName);
                _ = message.SetTextVariable("TIER", startingTier);

                InformationManager.DisplayMessage(new InformationMessage(message.ToString(), Colors.Cyan));
                ModLogger.Info("ENLISTMENT", $"Experience track notification: {track} -> Tier {startingTier}");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("ENLISTMENT", "Failed to show experience track notification", ex);
            }
        }

        /// <summary>
        ///     Get rank name for tier. Uses culture-specific names from progression_config.json.
        /// </summary>
        public string GetRankName(int tier)
        {
            // Get culture-specific rank title from progression_config.json
            var culture = _enlistedLord?.Culture?.StringId ?? "empire";
            return ConfigurationManager.GetCultureRankTitle(tier, culture) ?? $"Tier {tier}";
        }

        #region Minor Faction War Relations

        /// <summary>
        ///     Mirrors the lord's faction war relations to the player clan when enlisting with a minor faction.
        ///     This enables proper nameplate coloring (green for allies, red for enemies) and battle participation.
        ///     Uses FactionManager.DeclareWar directly (not DeclareWarAction) to avoid firing campaign events.
        /// </summary>
        /// <param name="lord">The minor faction lord the player is enlisting with.</param>
        private void MirrorMinorFactionWarRelations(Hero lord)
        {
            try
            {
                var lordFaction = lord.MapFaction;
                var playerClan = Clan.PlayerClan;

                if (lordFaction == null || playerClan == null)
                {
                    ModLogger.Debug("ENLISTMENT", "Cannot mirror war relations - lord faction or player clan is null");
                    return;
                }

                // Clear any previous tracking in case of re-enlistment
                _minorFactionWarRelations.Clear();

                // Get all factions the lord's faction is at war with
                var enemyFactions = lordFaction.FactionsAtWarWith?.ToList() ?? new List<IFaction>();

                if (enemyFactions.Count == 0)
                {
                    ModLogger.Info("FactionRelations",
                        $"Minor faction lord {lord.Name} has no enemies - no war relations to mirror");
                    return;
                }

                ModLogger.Info("FactionRelations",
                    $"Mirroring {enemyFactions.Count} war relations from {lordFaction.Name} to player clan");

                foreach (var enemyFaction in enemyFactions)
                {
                    // Skip if already at war (player might have their own existing wars)
                    if (FactionManager.IsAtWarAgainstFaction(playerClan, enemyFaction))
                    {
                        ModLogger.Debug("FactionRelations",
                            $"Already at war with {enemyFaction.Name} - skipping");
                        continue;
                    }

                    // Skip constant-war factions (bandits, etc.) - diplomacy model handles these automatically
                    if (Campaign.Current.Models.DiplomacyModel.IsAtConstantWar(playerClan, enemyFaction))
                    {
                        ModLogger.Debug("FactionRelations",
                            $"Constant war with {enemyFaction.Name} (bandits/outlaws) - handled by diplomacy model");
                        continue;
                    }

                    // Use low-level FactionManager.DeclareWar - does NOT fire OnWarDeclared events
                    FactionManager.DeclareWar(playerClan, enemyFaction);

                    // Track this relation for cleanup when service ends
                    _minorFactionWarRelations.Add(enemyFaction.StringId);

                    ModLogger.Info("FactionRelations",
                        $"Declared war with {enemyFaction.Name} (mirrored from {lordFaction.Name})");
                }

                // Refresh visuals so nameplate colors update immediately
                RefreshFactionVisuals();

                ModLogger.Info("FactionRelations",
                    $"Mirrored {_minorFactionWarRelations.Count} war relations for minor faction enlistment");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("FactionRelations", "Error mirroring war relations", ex);
            }
        }

        /// <summary>
        ///     Restores faction relations to neutral that were created when enlisting with a minor faction.
        ///     Called when enlistment ends to return the player to their original diplomatic state.
        ///     Uses FactionManager.SetNeutral directly (not MakePeaceAction) to avoid firing campaign events.
        /// </summary>
        private void RestoreMinorFactionWarRelations()
        {
            try
            {
                if (_minorFactionWarRelations == null || _minorFactionWarRelations.Count == 0)
                {
                    return;
                }

                var playerClan = Clan.PlayerClan;
                if (playerClan == null)
                {
                    ModLogger.Debug("FactionRelations", "Cannot restore relations - player clan is null");
                    _minorFactionWarRelations.Clear();
                    return;
                }

                ModLogger.Info("FactionRelations",
                    $"Restoring {_minorFactionWarRelations.Count} war relations to neutral");

                var restoredCount = 0;
                foreach (var factionId in _minorFactionWarRelations.ToList())
                {
                    // Look up the faction by StringId
                    var faction = Campaign.Current?.Factions?.FirstOrDefault(f => f.StringId == factionId);
                    if (faction == null)
                    {
                        ModLogger.Debug("FactionRelations",
                            $"Faction {factionId} not found (may have been destroyed) - skipping");
                        continue;
                    }

                    // Skip constant-war factions - can't make peace with them
                    if (Campaign.Current.Models.DiplomacyModel.IsAtConstantWar(playerClan, faction))
                    {
                        ModLogger.Debug("FactionRelations",
                            $"Cannot make peace with {faction.Name} (constant war) - skipping");
                        continue;
                    }

                    // Use low-level FactionManager.SetNeutral - does NOT fire OnMakePeace events
                    FactionManager.SetNeutral(playerClan, faction);
                    restoredCount++;

                    ModLogger.Info("FactionRelations",
                        $"Restored neutral relations with {faction.Name}");
                }

                // Clear tracking
                _minorFactionWarRelations.Clear();

                // Refresh visuals so nameplate colors update
                RefreshFactionVisuals();

                ModLogger.Info("FactionRelations",
                    $"Restored {restoredCount} faction relations to neutral");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("FactionRelations", "Error restoring war relations", ex);
                // Clear tracking even on error to prevent stale state
                _minorFactionWarRelations?.Clear();
            }
        }

        /// <summary>
        ///     Refreshes party visuals to update nameplate colors after faction relation changes.
        ///     This marks enemy faction parties as "dirty" so their nameplates redraw with correct colors.
        /// </summary>
        private void RefreshFactionVisuals()
        {
            try
            {
                var playerFaction = Hero.MainHero?.MapFaction;
                if (playerFaction == null)
                {
                    return;
                }

                // Mark all visible parties from factions at war with player as dirty
                // This triggers nameplate color refresh (same pattern as DeclareWarAction.ApplyInternal)
                foreach (var party in MobileParty.All)
                {
                    if (!party.IsVisible)
                    {
                        continue;
                    }

                    var partyFaction = party.MapFaction;
                    if (partyFaction == null)
                    {
                        continue;
                    }

                    // Check if this party's faction is at war with player
                    if (FactionManager.IsAtWarAgainstFaction(playerFaction, partyFaction))
                    {
                        party.Party.SetVisualAsDirty();
                    }
                }

                // Also mark settlements
                foreach (var settlement in Settlement.All)
                {
                    if (!settlement.IsVisible)
                    {
                        continue;
                    }

                    var settlementFaction = settlement.MapFaction;
                    if (settlementFaction == null)
                    {
                        continue;
                    }

                    if (FactionManager.IsAtWarAgainstFaction(playerFaction, settlementFaction))
                    {
                        settlement.Party.SetVisualAsDirty();
                    }
                }

                ModLogger.Debug("FactionRelations", "Refreshed faction visuals after relation change");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("FactionRelations", "Error refreshing faction visuals", ex);
            }
        }

        #endregion

        // Equipment backup system removed - we track QM-issued gear via item modifiers.
        // Discharge works by: reclaiming QM equipment + returning baggage stash items.
        // Player can sell/trade equipment freely without backup system interference.

        /// <summary>
        ///     Make player's ships invulnerable to prevent damage while enlisted.
        ///     Ships remain with the player but cannot take damage during military service.
        ///     This protects player investment in ships while serving under a lord.
        /// </summary>
        private void SetPlayerShipsInvulnerable()
        {
            try
            {
                var mainParty = MobileParty.MainParty;
                if (mainParty?.Party?.Ships == null)
                {
                    ModLogger.Debug("Naval", "No ships to protect - player has no ships");
                    return;
                }

                var protectedCount = 0;
                foreach (Ship ship in mainParty.Party.Ships)
                {
                    if (ship != null && !ship.IsInvulnerable)
                    {
                        ship.IsInvulnerable = true;
                        protectedCount++;
                        ModLogger.Debug("Naval",
                            $"Protected ship: {ship.Name} (HP: {ship.HitPoints}/{ship.MaxHitPoints})");
                    }
                }

                if (protectedCount > 0)
                {
                    ModLogger.Info("Naval", $"Protected {protectedCount} player ship(s) from damage during enlistment");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Naval", "Error protecting player ships", ex);
            }
        }

        /// <summary>
        ///     Restore player's ships to normal vulnerability after discharge.
        ///     Called when player is fully discharged (not during grace period).
        /// </summary>
        private void RestorePlayerShipsVulnerability()
        {
            try
            {
                var mainParty = MobileParty.MainParty;
                if (mainParty?.Party?.Ships == null)
                {
                    ModLogger.Debug("Naval", "No ships to restore - player has no ships");
                    return;
                }

                var restoredCount = 0;
                foreach (Ship ship in mainParty.Party.Ships)
                {
                    if (ship != null && ship.IsInvulnerable)
                    {
                        ship.IsInvulnerable = false;
                        restoredCount++;
                        ModLogger.Debug("Naval",
                            $"Restored ship vulnerability: {ship.Name} (HP: {ship.HitPoints}/{ship.MaxHitPoints})");
                    }
                }

                if (restoredCount > 0)
                {
                    ModLogger.Info("Naval",
                        $"Restored vulnerability to {restoredCount} player ship(s) after discharge");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Naval", "Error restoring ship vulnerability", ex);
            }
        }

        /// <summary>
        ///     Assign initial recruit equipment based on lord's culture.
        ///     PROTECTS QUEST ITEMS: Quest items are preserved during equipment assignment.
        /// </summary>
        private void AssignInitialEquipment()
        {
            try
            {
                if (_enlistedLord?.Culture?.BasicTroop?.Equipment == null)
                {
                    return;
                }

                // CRITICAL: Preserve quest items before replacing equipment
                var equipmentManager = EquipmentManager.Instance;
                var questItems = equipmentManager?.PreserveEquippedQuestItems() ?? new Dictionary<EquipmentIndex, EquipmentElement>();

                // Use lord's culture basic troop equipment
                var basicTroopEquipment = _enlistedLord.Culture.BasicTroop.Equipment;
                EquipmentHelper.AssignHeroEquipmentFromEquipment(Hero.MainHero, basicTroopEquipment);

                // CRITICAL: Restore quest items after equipment assignment
                equipmentManager?.RestoreEquippedQuestItems(questItems);

                ModLogger.Info("EQUIPMENT", $"Assigned initial {_enlistedLord.Culture.Name} recruit equipment (quest items protected)");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("EQUIPMENT", "Error assigning initial equipment", ex);
            }
        }

        /// <summary>
        ///     Set initial formation for new recruits based on their assigned equipment.
        /// </summary>
        private void SetInitialFormation()
        {
            // Formation preferences are now tracked via native traits
        }


        #endregion

        #region Temporary Leave System

        /// <summary>
        ///     Start temporary leave - suspend enlistment without ending service permanently.
        /// </summary>
        public void StartTemporaryLeave()
        {
            try
            {
                if (!IsEnlisted)
                {
                    return;
                }

                if (IsLeaveOnCooldown(out var cooldownDays))
                {
                    var cooldownMsg = new TextObject("{=Enlisted_Leave_Cooldown}Leave is on cooldown. {DAYS} days remain before you can request leave again.");
                    _ = cooldownMsg.SetTextVariable("DAYS", cooldownDays);
                    InformationManager.DisplayMessage(new InformationMessage(cooldownMsg.ToString(), Colors.Red));
                    ModLogger.Info("ENLISTMENT", $"Leave request blocked by cooldown ({cooldownDays} days remaining)");
                    return;
                }

                ModLogger.Info("ENLISTMENT", "Starting temporary leave - suspending service");

                // Clean up any active encounters first
                if (PlayerEncounter.Current != null)
                {
                    if (PlayerEncounter.InsideSettlement)
                    {
                        PlayerEncounter.LeaveSettlement();
                    }

                    PlayerEncounter.Finish();
                }

                // Restore vanilla player state
                var main = MobileParty.MainParty;
                if (main != null)
                {
                    main.IsVisible = true;
                    main.IsActive = true;
                    TrySetShouldJoinPlayerBattles(main, false);
                    TryReleaseEscort(main);
                }

                // Set leave state (preserve all service data)
                _isOnLeave = true;
                _leaveStartDate = CampaignTime.Now;

                // Notify player of leave timer
                var maxLeaveDays = EnlistedConfig.LoadGameplayConfig().LeaveMaxDays;
                var message =
                    new TextObject(
                        "Leave granted. You have {DAYS} days to return to your lord or face desertion penalties.");
                _ = message.SetTextVariable("DAYS", maxLeaveDays);
                InformationManager.DisplayMessage(new InformationMessage(message.ToString()));

                ModLogger.Info("ENLISTMENT", $"Temporary leave started - {maxLeaveDays} days before desertion");

                if (_enlistedLord != null)
                {
                    Campaign.Current.VisualTrackerManager.RegisterObject(_enlistedLord);
                    EnsureGraceLordMarker(forceRefresh: true, "start_leave");
                    var trackerMsg =
                        new TextObject("{=Enlisted_Message_LordTracked}Your lord has been marked on the map.");
                    InformationManager.DisplayMessage(new InformationMessage(trackerMsg.ToString()));
                }

                // Fire event for other mods
                OnLeaveStarted?.Invoke();
            }
            catch (Exception ex)
            {
                ModLogger.Caught("ENLISTMENT", "Error starting temporary leave", ex);
            }
        }

        /// <summary>
        ///     Return from temporary leave - resume enlistment with preserved data.
        /// </summary>
        public void ReturnFromLeave()
        {
            try
            {
                if (!_isOnLeave || _enlistedLord == null)
                {
                    return;
                }

                ModLogger.Info("ENLISTMENT", "Returning from temporary leave - resuming service");

                if (_enlistedLord != null)
                {
                    Campaign.Current.VisualTrackerManager.RemoveTrackedObject(_enlistedLord);
                }

                RemoveGraceLordMarker();

                // Clear leave state and start cooldown before next leave
                _isOnLeave = false;
                _leaveStartDate = CampaignTime.Zero;
                // Leave cooldown should be short enough to feel usable, while still preventing spam.
                // Requested behavior: 7-day cooldown (was 30).
                _leaveCooldownEnds = CampaignTime.Now + CampaignTime.Days(7);

                // NOTE: We intentionally do NOT transfer troops here. Unlike initial enlistment,
                // troops recruited during leave are the player's personal force and should stay
                // with them when returning to service. The player earned these troops independently.

                // Finish any active encounter first (prevents assertion crashes during state transitions)
                // Same logic as StartEnlist() - must clean up encounters before setting IsActive = false
                if (PlayerEncounter.Current != null)
                {
                    // Ensure we're not inside a settlement before finishing encounter
                    if (PlayerEncounter.InsideSettlement)
                    {
                        PlayerEncounter.LeaveSettlement();
                    }

                    PlayerEncounter.Finish();
                }

                // Resume enlistment behavior (will be handled by next real-time tick)
                var main = MobileParty.MainParty;
                if (main != null)
                {
                    main.IsVisible = false;
                    main.IsActive = false; // Now safe to disable after encounter cleanup
                    TrySetShouldJoinPlayerBattles(main, true);
                }

                // Activate enlisted status menu immediately to prevent encounter gaps
                EnlistedMenuBehavior.SafeActivateEnlistedMenu();
                ModLogger.Info("ENLISTMENT",
                    "Service resumed - attempted enlisted status menu activation (with global guard)");

                // Fire event for other mods
                OnLeaveEnded?.Invoke();
            }
            catch (Exception ex)
            {
                ModLogger.Caught("ENLISTMENT", "Error returning from leave", ex);
            }
        }

        /// <summary>
        ///     Checks whether the player is currently on leave cooldown.
        /// </summary>
        public bool IsLeaveOnCooldown(out int daysRemaining)
        {
            daysRemaining = 0;

            if (_leaveCooldownEnds == CampaignTime.Zero || _leaveCooldownEnds <= CampaignTime.Now)
            {
                return false;
            }

            daysRemaining = (int)Math.Ceiling((_leaveCooldownEnds - CampaignTime.Now).ToDays);
            return true;
        }

        /// <summary>
        ///     Transfer service to a different lord in the same faction.
        ///     Preserves all progression (tier, XP, kills, service date).
        ///     Used when player is on leave or in grace period and wants to serve under a new commander.
        /// </summary>
        /// <param name="newLord">The new lord to serve under (must be in same faction).</param>
        public void TransferServiceToLord(Hero newLord)
        {
            if (newLord == null)
            {
                ModLogger.Expected("ENLISTMENT", "transfer_lord_null",
                    "TransferServiceToLord called with null lord");
                return;
            }

            try
            {
                var previousLord = _enlistedLord;
                var previousLordName = previousLord?.Name?.ToString() ?? "Unknown";
                var newLordName = newLord.Name?.ToString() ?? "Unknown";

                // If transferring from grace period, restore saved progression values first
                // During grace, StopEnlist() clears _enlistmentTier/_enlistmentXP but saves them to grace state
                if (IsInDesertionGracePeriod && _savedGraceTier > 0)
                {
                    _enlistmentTier = _savedGraceTier;
                    _enlistmentXP = _savedGraceXP;
                    _enlistmentDate = _savedGraceEnlistmentDate != CampaignTime.Zero
                        ? _savedGraceEnlistmentDate
                        : CampaignTime.Now;
                    ModLogger.Info("ENLISTMENT",
                        $"Restored grace progression for transfer: Tier={_enlistmentTier}, XP={_enlistmentXP}");
                }

                ModLogger.Info("ENLISTMENT", $"Transferring service from {previousLordName} to {newLordName}");
                ModLogger.Info("ENLISTMENT",
                    $"Preserving: Tier={_enlistmentTier}, XP={_enlistmentXP}, Kills={_currentTermKills}, Date={_enlistmentDate}");

                // Clear leave state if on leave
                if (_isOnLeave)
                {
                    if (previousLord != null)
                    {
                        Campaign.Current.VisualTrackerManager.RemoveTrackedObject(previousLord);
                    }

                    _isOnLeave = false;
                    _leaveStartDate = CampaignTime.Zero;
                    ModLogger.Debug("ENLISTMENT", "Cleared leave state for transfer");
                }

                // Clear grace period if in grace (this is also a valid transfer path)
                if (IsInDesertionGracePeriod)
                {
                    ClearDesertionGracePeriod();
                    ModLogger.Debug("ENLISTMENT", "Cleared grace period for transfer");
                }

                // Update the enlisted lord (progression stays the same)
                _enlistedLord = newLord;

                // Player keeps their current equipment during service transfer
                ModLogger.Info("ENLISTMENT", "Keeping enlisted equipment during service transfer");

                // Transfer any companions/troops to new lord's party
                TransferPlayerTroopsToLord();

                // Finish any active encounter first
                if (PlayerEncounter.Current != null)
                {
                    if (PlayerEncounter.InsideSettlement)
                    {
                        PlayerEncounter.LeaveSettlement();
                    }

                    PlayerEncounter.Finish();
                }

                // Re-attach to new lord's party
                EncounterGuard.TryAttachOrEscort(newLord);

                // Configure party state for enlistment
                var main = MobileParty.MainParty;
                var newLordParty = newLord.PartyBelongedTo;
                if (main != null)
                {
                    main.IsVisible = false;
                    main.IsActive = true;
                    main.IgnoreByOtherPartiesTill(CampaignTime.Now + CampaignTime.Days(365f));

                    if (newLordParty != null)
                    {
                        main.SetMoveEscortParty(newLordParty, MobileParty.NavigationType.Default, false);
                        newLordParty.Party.SetAsCameraFollowParty();
                        ModLogger.Debug("ENLISTMENT", $"Set escort AI to follow {newLordName}");
                    }

                    TrySetShouldJoinPlayerBattles(main, true);
                }

                // Activate enlisted status menu
                EnlistedMenuBehavior.SafeActivateEnlistedMenu();

                ModLogger.Info("ENLISTMENT", $"Service transfer complete: {previousLordName} -> {newLordName}");

                // Log state for diagnostics
                SessionDiagnostics.LogStateTransition("Enlistment", "OnLeave/Grace", "Enlisted",
                    $"Transfer from {previousLordName} to {newLordName}, Tier: {_enlistmentTier}, XP: {_enlistmentXP}");
            }
            catch (Exception ex)
            {
                var newLordName = newLord?.Name?.ToString() ?? "unknown";
                ModLogger.Surfaced("ENLISTMENT", "Error transferring service to new lord", ex,
                    ctx: LogCtx.Of("NewLord", newLordName));
            }
        }

        /// <summary>
        ///     Transfer player companions and troops to lord's party.
        ///     Called when returning from leave or initial enlistment.
        ///     At Tier 4+, companions stay with the player to form their retinue squad.
        /// </summary>
        private void TransferPlayerTroopsToLord()
        {
            try
            {
                var main = MobileParty.MainParty;
                var lordParty = _enlistedLord?.PartyBelongedTo;

                if (main == null || lordParty == null)
                {
                    return;
                }

                // At Tier 4+, companions stay with player to form the retinue squad
                // This enables the Retinue system's Personal Retinue system
                var keepCompanions = _enlistmentTier >= 4;

                var transferCount = 0;
                var companionCount = 0;
                var companionsKept = 0;

                // Transfer non-player troops to lord's party (with tier-based companion exception)
                var troopsToTransfer = new List<TroopRosterElement>();
                foreach (var troop in main.MemberRoster.GetTroopRoster())
                {
                    // Skip the player character
                    if (troop.Character == CharacterObject.PlayerCharacter)
                    {
                        continue;
                    }

                    // At Tier 4+, keep companions with player for retinue system
                    if (keepCompanions && troop.Character.IsHero &&
                        troop.Character.HeroObject?.IsPlayerCompanion == true)
                    {
                        companionsKept++;
                        ModLogger.Debug("ENLISTMENT",
                            $"Keeping companion {troop.Character.Name} with player (Tier {_enlistmentTier}+)");
                        continue;
                    }

                    if (troop.Number > 0)
                    {
                        troopsToTransfer.Add(troop);
                        transferCount += troop.Number;
                        if (troop.Character.IsHero)
                        {
                            companionCount++;
                        }
                    }
                }

                // Perform the transfer using native party roster methods
                foreach (var troop in troopsToTransfer)
                {
                    // Add to lord's party
                    _ = lordParty.MemberRoster.AddToCounts(troop.Character, troop.Number);
                    // Remove from player party
                    _ = main.MemberRoster.AddToCounts(troop.Character, -1 * troop.Number);
                }

                if (transferCount > 0 || companionsKept > 0)
                {
                    if (transferCount > 0)
                    {
                        var message =
                            new TextObject(
                                "Your {TROOP_COUNT} troops{COMPANION_INFO} have joined your lord's party for the duration of service.");
                        _ = message.SetTextVariable("TROOP_COUNT", transferCount.ToString());
                        _ = message.SetTextVariable("COMPANION_INFO",
                            companionCount > 0 ? $" (including {companionCount} companions)" : "");
                        InformationManager.DisplayMessage(new InformationMessage(message.ToString()));
                    }

                    if (companionsKept > 0)
                    {
                        var keptMessage = new TextObject(
                            "{=ct_companions_retained}Your {COUNT} companion(s) remain under your direct command.");
                        _ = keptMessage.SetTextVariable("COUNT", companionsKept.ToString());
                        InformationManager.DisplayMessage(new InformationMessage(keptMessage.ToString()));
                    }

                    ModLogger.Info("ENLISTMENT",
                        $"Transfer complete: {transferCount} troops ({companionCount} companions) to lord, " +
                        $"{companionsKept} companions kept with player (Tier {_enlistmentTier})");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("ENLISTMENT", "Error transferring troops to lord", ex);
            }
        }

        /// <summary>
        ///     Restore companions to player party on retirement (regular troops stay with lord).
        ///     Companions should be available for the player again after service ends.
        ///     Deduplicates against existing roster and cleans stale refs from lord party.
        /// </summary>
        private void RestoreCompanionsToPlayer()
        {
            try
            {
                var main = MobileParty.MainParty;
                var lordParty = _enlistedLord?.PartyBelongedTo;

                if (main == null)
                {
                    ModLogger.Warn("ENLISTMENT", "RestoreCompanionsToPlayer skipped - main party missing");
                    return;
                }

                if (lordParty == null)
                {
                    ModLogger.Warn("ENLISTMENT", "RestoreCompanionsToPlayer skipped - lord party missing");
                    return;
                }

                var mainRoster = main.MemberRoster;
                var lordRoster = lordParty.MemberRoster;
                var companionsToRestore = new List<TroopRosterElement>();
                var staleRefsToRemove = new List<TroopRosterElement>();

                // Find player's companions in lord's party
                foreach (var troop in lordRoster.GetTroopRoster())
                {
                    if (!troop.Character.IsHero || troop.Character == CharacterObject.PlayerCharacter)
                    {
                        continue;
                    }

                    var hero = troop.Character.HeroObject;
                    if (hero == null || hero.Clan != Clan.PlayerClan)
                    {
                        continue;
                    }

                    // Check if companion already in player's roster
                    if (mainRoster.GetTroopCount(troop.Character) > 0)
                    {
                        // Stale reference - companion escaped and is already with player
                        staleRefsToRemove.Add(troop);
                    }
                    else
                    {
                        // Need to restore this companion to player
                        companionsToRestore.Add(troop);
                    }
                }

                // Remove stale references from lord's party (companion already with player)
                foreach (var stale in staleRefsToRemove)
                {
                    _ = lordRoster.AddToCounts(stale.Character, -stale.Number);
                    ModLogger.Info("ENLISTMENT", $"Cleaned stale ref for {stale.Character.Name} from lord's party");
                }

                // Transfer companions that need restoration
                foreach (var companion in companionsToRestore)
                {
                    _ = lordRoster.AddToCounts(companion.Character, -companion.Number);
                    _ = mainRoster.AddToCounts(companion.Character, companion.Number);
                }

                if (companionsToRestore.Count > 0)
                {
                    var message =
                        new TextObject("{=enlist_companions_rejoined}Your {COMPANION_COUNT} companions have rejoined your party upon retirement.");
                    _ = message.SetTextVariable("COMPANION_COUNT", companionsToRestore.Count.ToString());
                    InformationManager.DisplayMessage(new InformationMessage(message.ToString()));

                    ModLogger.Info("ENLISTMENT",
                        $"Restored {companionsToRestore.Count} companions to player party (cleaned {staleRefsToRemove.Count} stale refs)");
                }
                else if (staleRefsToRemove.Count > 0)
                {
                    ModLogger.Info("ENLISTMENT",
                        $"Cleaned {staleRefsToRemove.Count} stale companion refs from lord's party (companions already with player)");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("ENLISTMENT", "Error restoring companions on retirement", ex);
            }
        }

        /// <summary>
        ///     Reclaims companions from the lord's party back to the player's MainParty.
        ///     Called when the player reaches Tier 4 (via promotion or save load) to enable the
        ///     Companion Assignments feature. At Tier 4+, companions should fight alongside the player.
        /// </summary>
        private void ReclaimCompanionsFromLord()
        {
            try
            {
                // Only reclaim at Tier 4+ (when companion assignments become available)
                if (_enlistmentTier < 4)
                {
                    return;
                }

                var main = MobileParty.MainParty;
                var lordParty = _enlistedLord?.PartyBelongedTo;

                if (main == null || lordParty == null)
                {
                    return;
                }

                var companionsReclaimed = 0;
                var companionsToReclaim = new List<TroopRosterElement>();

                // Find player's companions in lord's party
                foreach (var troop in lordParty.MemberRoster.GetTroopRoster())
                {
                    // Only reclaim hero companions that belong to the player's clan
                    if (troop.Character.IsHero && troop.Character != CharacterObject.PlayerCharacter)
                    {
                        var hero = troop.Character.HeroObject;
                        if (hero != null && hero.IsPlayerCompanion && hero.Clan == Clan.PlayerClan)
                        {
                            companionsToReclaim.Add(troop);
                            companionsReclaimed += troop.Number;
                        }
                    }
                }

                // Transfer companions back to player's MainParty
                foreach (var companion in companionsToReclaim)
                {
                    // Remove from lord's party
                    _ = lordParty.MemberRoster.AddToCounts(companion.Character, -1 * companion.Number);
                    // Add to player's party
                    _ = main.MemberRoster.AddToCounts(companion.Character, companion.Number);
                }

                if (companionsReclaimed > 0)
                {
                    var message = new TextObject(
                        "{=ct_companions_reclaimed}Your {COMPANION_COUNT} companion(s) have joined your personal retinue.");
                    _ = message.SetTextVariable("COMPANION_COUNT", companionsReclaimed.ToString());
                    InformationManager.DisplayMessage(new InformationMessage(message.ToString(), Colors.Cyan));

                    ModLogger.Info("ENLISTMENT",
                        $"Reclaimed {companionsReclaimed} companions from lord's party (Tier {_enlistmentTier} promotion)");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("ENLISTMENT", "Error reclaiming companions from lord", ex);
            }
        }

        /// <summary>
        ///     MINIMAL: Settlement entry detection for menu refresh only.
        ///     Just refreshes the menu when lord enters settlements - no complex logic.
        /// </summary>
        private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
        {
            try
            {
                if (_isOnLeave && hero == _enlistedLord)
                {
                    EnsureGraceLordMarker(forceRefresh: true, "settlement_entered");
                }

                if (IsInDesertionGracePeriod && hero == _savedGraceLord)
                {
                    EnsureGraceLordMarker(forceRefresh: true, "settlement_entered");
                }

                // CRITICAL: Skip all processing when player is a prisoner
                // The native PlayerCaptivity system handles everything during captivity
                // Interfering with party state or menus while captured causes crashes
                // (e.g., when captor enters a settlement with the player as prisoner)
                var playerIsPrisoner = Hero.MainHero.IsPrisoner;
                if (!IsEnlisted || _isOnLeave || playerIsPrisoner)
                {
                    if (playerIsPrisoner)
                    {
                        var settlementName = settlement?.Name?.ToString() ?? "unknown";
                        if (!_captivitySettlementsLogged.Contains(settlementName))
                        {
                            ModLogger.Info("Captivity",
                                $"Captor entered {settlementName} with player as prisoner");
                            _ = _captivitySettlementsLogged.Add(settlementName);
                        }
                    }
                    else
                    {
                        // Clear throttle cache once not prisoner so next captivity run logs fresh
                        _captivitySettlementsLogged.Clear();
                    }

                    return;
                }

                var mainParty = MobileParty.MainParty;
                if (party == mainParty)
                {
                    ModLogger.Info("Settlement",
                        $"Player entered {settlement?.Name?.ToString() ?? "unknown"} ({settlement?.StringId ?? "unknown"})");

                    if (PlayerEncounter.Current != null && !PlayerEncounter.InsideSettlement)
                    {
                        try
                        {
                            ModLogger.Info("Settlement",
                                "Finishing PlayerEncounter before entering settlement to prevent InsideSettlement assertion");
                            PlayerEncounter.Finish();
                        }
                        catch (Exception ex)
                        {
                            ModLogger.Caught("Settlement",
                                "Error finishing PlayerEncounter before settlement entry (player)", ex);
                        }
                    }

                    if (mainParty != null)
                    {
                        if (!mainParty.IsActive)
                        {
                            mainParty.IsActive = true;
                        }

                        mainParty.IgnoreByOtherPartiesTill(CampaignTime.Now);
                    }

                    // For towns and castles, pause and activate enlisted menu
                    // This handles the case where escort AI brings player into settlement with lord
                    if (settlement.IsTown || settlement.IsCastle)
                    {
                        TryPauseForSettlementEntry();

                        // Check for active battles/sieges - but a settlement encounter is fine
                        var hasMapEvent = mainParty?.Party.MapEvent != null;
                        var hasBesiegedSettlement = _enlistedLord?.PartyBelongedTo?.BesiegedSettlement != null;
                        var inBattleOrSiege = hasMapEvent || hasBesiegedSettlement;

                        ModLogger.Info("Settlement",
                            $"Player entering {settlement.Name}: MapEvent={hasMapEvent}, Besieged={hasBesiegedSettlement}, inBattleOrSiege={inBattleOrSiege}");

                        if (!inBattleOrSiege)
                        {
                            EnlistedMenuBehavior.SafeActivateEnlistedMenu();
                            ModLogger.Info("Settlement",
                                $"Activated enlisted menu for player entering {settlement.Name}");
                        }
                        else
                        {
                            ModLogger.Info("Settlement",
                                $"GUARDED: Skipped enlisted menu activation - battle/siege active ({settlement.Name})");
                        }
                    }

                    return;
                }

                // Only react when OUR enlisted lord enters a settlement
                if (hero == _enlistedLord)
                {
                    ModLogger.Info("Settlement", $"Lord {hero.Name} entered {settlement.Name} ({settlement.StringId})");

                    // Track supply level for resupply message on departure
                    CompanySupplyManager.Instance?.OnSettlementEntered(settlement);

                    // NOTE: We used to call EnterSettlementAction.ApplyForParty() here to immediately
                    // pull the player into the settlement, but this causes EncounterMenuOverlayVM assertion
                    // failures ("Encounter overlay is open but MapEvent AND SiegeEvent is null").
                    // The escort AI naturally follows the lord into settlements - just a half-second delay.
                    // A small delay is better than a crash.

                    // CRITICAL: Finish any active PlayerEncounter before entering settlement
                    // This prevents InsideSettlement assertion failures when the army enters a settlement
                    // Party encounters cannot exist while inside settlements
                    // According to API: StartPartyEncounter cannot be called when InsideSettlement is true
                    // We should finish party encounters when entering settlements to prevent assertion at line 2080
                    if (PlayerEncounter.Current != null && !PlayerEncounter.InsideSettlement)
                    {
                        try
                        {
                            ModLogger.Info("Settlement",
                                "Finishing PlayerEncounter before entering settlement to prevent InsideSettlement assertion");
                            PlayerEncounter.Finish();
                        }
                        catch (Exception ex)
                        {
                            ModLogger.Caught("Settlement",
                                "Error finishing PlayerEncounter before settlement entry (lord)", ex);
                        }
                    }

                    // Only pause/activate menu for towns and castles, not villages
                    // Villages should allow continuous time flow while following the lord
                    if (settlement.IsTown || settlement.IsCastle)
                    {
                        TryPauseForSettlementEntry();

                        // CRITICAL GUARD: Don't interfere with battle/siege encounter menus
                        // CORRECT API: Use Party.MapEvent (not direct on MobileParty)
                        // Allow settlement encounters (peaceful town/castle entry) but block battles
                        var isSettlementEncounter = PlayerEncounter.EncounterSettlement != null &&
                                                    mainParty?.Party.MapEvent == null;
                        var inBattleOrSiege = (mainParty?.Party.MapEvent != null) ||
                                              (PlayerEncounter.Current != null && !isSettlementEncounter) ||
                                              (_enlistedLord?.PartyBelongedTo?.BesiegedSettlement != null);

                        if (!inBattleOrSiege)
                        {
                            // Trigger settlement entered event for player (MainParty) to activate perks
                            // This allows perks like "Show Your Scars" to trigger automatically when the lord enters a town
                            if (MobileParty.MainParty != null && Hero.MainHero != null &&
                                CampaignEventDispatcher.Instance != null)
                            {
                                try
                                {
                                    CampaignEventDispatcher.Instance.OnSettlementEntered(MobileParty.MainParty,
                                        settlement, Hero.MainHero);
                                    ModLogger.Info("Settlement",
                                        $"Triggered synthetic SettlementEntered for {settlement.Name} to activate perks");
                                }
                                catch (Exception ex)
                                {
                                    // Safely ignore errors from native listeners (e.g. due to unusual enlisted party state)
                                    ModLogger.Warn("Settlement",
                                        $"Synthetic perk trigger suppressed error: {ex.Message}");
                                }
                            }

                            // Safe to show enlisted menu - no battles/sieges active
                            EnlistedMenuBehavior.SafeActivateEnlistedMenu();
                            ModLogger.Debug("Settlement",
                                $"Activated enlisted menu for {settlement.Name} (town/castle)");
                        }
                        else
                        {
                            ModLogger.Info("Settlement",
                                $"GUARDED: Skipped enlisted menu activation - battle/siege active ({settlement.Name})");
                        }
                    }
                    else
                    {
                        // Villages: just log but don't pause or activate menu
                        ModLogger.Debug("Settlement", $"Lord entered village {settlement.Name} - continuing time flow");
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Settlement", "Error in settlement entry detection", ex);
            }
        }

        private void OnSettlementLeft(MobileParty party, Settlement settlement)
        {
            try
            {
                if (_isOnLeave && party == _enlistedLord?.PartyBelongedTo)
                {
                    EnsureGraceLordMarker(forceRefresh: true, "settlement_left");
                }

                if (IsInDesertionGracePeriod && party == _savedGraceLord?.PartyBelongedTo)
                {
                    EnsureGraceLordMarker(forceRefresh: true, "settlement_left");
                }

                if (!IsEnlisted || _isOnLeave)
                {
                    return;
                }

                var mainParty = MobileParty.MainParty;
                if (party == mainParty)
                {
                    ModLogger.Info("Settlement",
                        $"Player left {settlement?.Name?.ToString() ?? "unknown"} ({settlement?.StringId ?? "unknown"})");

                    // Re-hide the party if we're not immediately entering a battle.
                    // Per encounter-safety doc: "Fixed by suppressing IsActive during siege waiting phase"
                    // Only keep active during actual assault (MapEvent), not siege waiting.
                    bool inBattle = mainParty?.Party.MapEvent != null;
                    if (!inBattle && mainParty != null)
                    {
                        mainParty.IsVisible = false;
                        mainParty.IsActive = false;
                        mainParty.IgnoreByOtherPartiesTill(CampaignTime.Now + CampaignTime.Hours(0.5f));
                    }

                    if (_enlistedLord != null)
                    {
                        EncounterGuard.TryAttachOrEscort(_enlistedLord);
                    }

                    return;
                }

                if (party == _enlistedLord?.PartyBelongedTo)
                {
                    ModLogger.Info("Settlement",
                        $"Lord {_enlistedLord?.Name?.ToString() ?? "unknown"} left {settlement?.Name?.ToString() ?? "unknown"} - pulling player to follow");

                    // Show resupply message if meaningful supply was gained during the visit
                    CompanySupplyManager.Instance?.OnSettlementLeft(settlement);

                    // LORD LEFT - pull the player out to follow!
                    // Check if player is still in this settlement
                    if (mainParty?.CurrentSettlement == settlement)
                    {
                        ModLogger.Info("Settlement", "Player still in settlement - forcing exit to follow lord");

                        // CRITICAL: Enable force hidden mode BEFORE exit to block visibility during transition
                        // This prevents race conditions where native code sets IsVisible=true before our hiding code runs
                        VisibilityEnforcementPatch.BeginForceHidden();

                        // Force leave the settlement
                        NextFrameDispatcher.RunNextFrame(() =>
                        {
                            try
                            {
                                if (PlayerEncounter.Current != null)
                                {
                                    if (PlayerEncounter.InsideSettlement)
                                    {
                                        PlayerEncounter.LeaveSettlement();
                                    }

                                    PlayerEncounter.Finish();
                                }

                                // CRITICAL: Set up escort FIRST so IsEmbeddedWithLord() returns true
                                // This ensures the VisibilityEnforcementPatch blocks visibility correctly
                                var main = MobileParty.MainParty;
                                if (main != null)
                                {
                                    main.IsActive = true; // Keep active for escort AI
                                    main.IgnoreByOtherPartiesTill(CampaignTime.Now + CampaignTime.Hours(1f));

                                    // Resume escort BEFORE setting visibility so TargetParty is set
                                    if (_enlistedLord != null)
                                    {
                                        EncounterGuard.TryAttachOrEscort(_enlistedLord);
                                    }

                                    // NOW hide the party - IsEmbeddedWithLord() will return true
                                    main.IsVisible = false;

                                    // Also hide the 3D visual entity (separate from nameplate VM)
                                    EncounterGuard.HidePlayerPartyVisual();
                                }

                                // Force hidden mode served its purpose - can disable now
                                // (It will auto-expire anyway, but clean up for clarity)
                                VisibilityEnforcementPatch.EndForceHidden();

                                // Activate enlisted menu
                                EnlistedMenuBehavior.SafeActivateEnlistedMenu();
                                ModLogger.Info("Settlement", "Player pulled from settlement to follow lord (hidden)");
                            }
                            catch (Exception ex)
                            {
                                // Make sure to end force hidden even on error
                                VisibilityEnforcementPatch.EndForceHidden();
                                ModLogger.Caught("Settlement",
                                    "Error pulling player from settlement to follow lord", ex);
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Settlement", "Error in settlement exit detection", ex);
            }
        }

        /// <summary>
        ///     REVERT: Handle battle start events to inject player into lord's battles.
        ///     Using MapEventStarted for battle detection (BattleStarted is not available).
        /// </summary>
        private void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
        {
            try
            {
                if (!IsEnlisted || _isOnLeave || _enlistedLord == null)
                {
                    return;
                }

                // SAFETY: Never process enlistment battle flow while the player is a prisoner or
                // while capture cleanup is pending. The native captivity system owns the state during this time,
                // and forcing battle prep (teleport/army join) can crash when captors are attacked.
                if (Hero.MainHero.IsPrisoner || _playerCaptureCleanupScheduled)
                {
                    ModLogger.Info("EVENTSAFETY",
                        $"Skipping MapEventStarted - player prisoner or cleanup pending (IsPrisoner={Hero.MainHero.IsPrisoner}, CaptureCleanupScheduled={_playerCaptureCleanupScheduled})");
                    return;
                }

                // BUGFIX: Skip battle processing if player is already waiting in reserve.
                // When player was wounded and chose "Wait in Reserve", they intend to sit out ALL battles
                // in the current series. Without this check, each new MapEvent triggers menu switching,
                // creating a loop where the encounter menu keeps appearing and awarding duplicate XP.
                if (EnlistedEncounterBehavior.IsWaitingInReserve)
                {
                    ModLogger.Debug("BATTLE",
                        "Skipping MapEventStarted - player is waiting in reserve (already chose to sit out battles)");
                    return;
                }

                var lordParty = _enlistedLord.PartyBelongedTo;
                if (lordParty == null || mapEvent == null)
                {
                    return;
                }

                var main = MobileParty.MainParty;
                if (main == null)
                {
                    return;
                }

                // Determine whether this event matters to our enlisted service
                var lordIsAttacker = attackerParty?.MobileParty == lordParty || attackerParty == lordParty.Party;
                var lordIsDefender = defenderParty?.MobileParty == lordParty || defenderParty == lordParty.Party;
                var inArmy = lordParty.Army != null && main.Army == lordParty.Army;
                var armyLeaderInvolved = lordParty.Army?.LeaderParty?.Party == attackerParty ||
                                         lordParty.Army?.LeaderParty?.Party == defenderParty;

                // Check if lord is in the MapEvent's involved parties (most reliable check)
                var lordInvolvedInMapEvent = mapEvent.InvolvedParties?.Any(p => p?.MobileParty == lordParty) == true;

                // FIX: Only consider battle relevant if our LORD is actually involved, not just army members
                // Being in the same army doesn't mean we should join every random skirmish around us
                // During sieges, many small battles happen (looters vs villagers) that we should ignore
                var isRelevantBattle = lordIsAttacker || lordIsDefender || armyLeaderInvolved || lordInvolvedInMapEvent;

                // Early exit for unrelated battles - don't log to avoid spam from all map battles
                if (!isRelevantBattle)
                {
                    return;
                }

                // Only log detailed info for battles that actually involve our lord
                ModLogger.Info("BATTLE", "=== MapEventStarted - LORD INVOLVED ===");
                ModLogger.Info("BATTLE",
                    $"Lord: {lordParty.LeaderHero?.Name?.ToString() ?? "unknown"}, LordParty ID: {lordParty.StringId}");
                ModLogger.Info("BATTLE",
                    $"Attacker: {attackerParty?.MobileParty?.LeaderHero?.Name?.ToString() ?? attackerParty?.Name?.ToString() ?? "unknown"}");
                ModLogger.Info("BATTLE",
                    $"Defender: {defenderParty?.MobileParty?.LeaderHero?.Name?.ToString() ?? defenderParty?.Name?.ToString() ?? "unknown"}");
                ModLogger.Debug("BATTLE",
                    $"lordIsAttacker={lordIsAttacker}, lordIsDefender={lordIsDefender}, inArmy={inArmy}, armyLeaderInvolved={armyLeaderInvolved}");

                // Reset tracking flags for new battle
                _battleXPAwardedThisBattle = false;
                _playerEncounterCreatedForBattle = false;

                var isSiegeBattle = mapEvent.IsSiegeAssault || mapEvent.EventType == MapEvent.BattleTypes.Siege;

                ModLogger.Info("BATTLE",
                    $"Native battle detected (Siege: {isSiegeBattle}, InArmy: {inArmy}) - preparing player for vanilla flow");

                // Log siege integration status for diagnostic purposes
                if (isSiegeBattle && main.BesiegerCamp != null)
                {
                    LogSiegeIntegrationStatus(main, lordParty, main.BesiegerCamp, "OnMapEventStarted-Siege");
                }

                // Exit custom enlisted menus so the native system can push its own encounter/army menus
                var currentMenu = Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId;

                if (!string.IsNullOrEmpty(currentMenu) && currentMenu.StartsWith("enlisted_") && !isSiegeBattle)
                {
                    var encounterMenuModel = Campaign.Current?.Models?.EncounterGameMenuModel;
                    var desiredMenu = encounterMenuModel?.GetGenericStateMenu();

                    if (!string.IsNullOrEmpty(desiredMenu) && desiredMenu != currentMenu)
                    {
                        ModLogger.Info("BATTLE",
                            $"Switching from enlisted menu to native menu '{desiredMenu}' for battle");
                        NextFrameDispatcher.RunNextFrame(() =>
                        {
                            try
                            {
                                GameMenu.SwitchToMenu(desiredMenu);
                            }
                            catch (Exception ex)
                            {
                                ModLogger.Caught("BATTLE",
                                    $"Failed to switch to native menu '{desiredMenu}'", ex);
                            }
                        });
                    }
                    else
                    {
                        ModLogger.Debug("BATTLE",
                            "Native battle menu not ready yet - keeping enlisted menu until engine pushes one");
                    }
                }

                // CRITICAL: Teleport player party to lord's position IMMEDIATELY so encounter triggers instantly
                // Without this, the player's invisible party has to "travel" to the battle, causing a pause
                // while the game waits for the party to arrive before showing the encounter menu
                if (main.Position != lordParty.Position)
                {
                    var oldPos = main.GetPosition2D;
                    main.Position = lordParty.Position;
                    ModLogger.Info("BATTLE",
                        $"Teleported player party to battle location (from {oldPos} to {lordParty.GetPosition2D})");
                }

                EnsurePlayerSharesArmy(lordParty);
                PreparePartyForNativeBattle(main);
                ForceImmediateBattleJoin(mapEvent, main, lordParty);

                // Keep escort behaviour hooked so we stay attached to the lord while the encounter progresses
                EncounterGuard.TryAttachOrEscort(_enlistedLord);
                TryReleaseEscort(main);

                // Force unpause the game so encounter appears immediately without manual intervention
                if (Campaign.Current?.TimeControlMode == CampaignTimeControlMode.Stop)
                {
                    Campaign.Current.TimeControlMode = CampaignTimeControlMode.StoppablePlay;
                    ModLogger.Debug("BATTLE", "Auto-unpaused game for seamless battle entry");
                }

                if (mapEvent != null)
                {
                    _cachedLordMapEvent = mapEvent;
                }

                LogPartyState("OnMapEventStarted");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("BATTLE", "Error in MapEventStarted battle handler", ex);
            }
        }

        /// <summary>
        ///     Handle battle end events to return player to hidden state.
        /// </summary>
        private void OnMapEventEnded(MapEvent mapEvent)
        {
            try
            {
                var main = MobileParty.MainParty;

                // DEBUG: Log entry to confirm this handler is being called
                ModLogger.Info("BATTLE",
                    $"OnMapEventEnded CALLED - mapEvent={(mapEvent != null ? "present" : "null")}, IsEnlisted={IsEnlisted}");

                // Reset PlayerEncounter guard flag - battle is ending
                _playerEncounterCreatedForBattle = false;

                // CRITICAL: Immediately restore IgnoreByOtherPartiesTill protection for enlisted players.
                // This prevents the native game from creating new encounters during the cleanup window
                // between battle end and party deactivation. Without this, nearby enemies (like the
                // party the lord just defeated) can create duplicate encounters.
                if (IsEnlisted && main != null && !Hero.MainHero.IsPrisoner)
                {
                    main.IgnoreByOtherPartiesTill(CampaignTime.Now + CampaignTime.Hours(1f));
                    ModLogger.Debug("BATTLE", "Restored ignore protection immediately after battle end");
                }

                // SAFETY: If the player is still a prisoner or capture cleanup is queued, avoid any post-battle
                // enlistment cleanup (army/visibility/menu) and let native captivity flow finalize first.
                if (Hero.MainHero.IsPrisoner || _playerCaptureCleanupScheduled)
                {
                    ModLogger.Info("EVENTSAFETY",
                        $"Skipping MapEventEnded - player prisoner or cleanup pending (IsPrisoner={Hero.MainHero.IsPrisoner}, CaptureCleanupScheduled={_playerCaptureCleanupScheduled})");
                    _cachedLordMapEvent = null;
                    return;
                }

                var effectiveMapEvent = mapEvent ?? _cachedLordMapEvent;

                var lordParty = _enlistedLord?.PartyBelongedTo;

                var playerParticipated = main?.Party.MapEvent == mapEvent ||
                                         effectiveMapEvent?.InvolvedParties?.Any(p => p?.MobileParty == main) == true;

                var lordParticipated = false;
                if (lordParty != null && effectiveMapEvent?.InvolvedParties != null)
                {
                    lordParticipated = effectiveMapEvent.InvolvedParties.Any(p => p?.MobileParty == lordParty);

                    if (!lordParticipated && lordParty.Army != null)
                    {
                        var army = lordParty.Army;
                        lordParticipated = effectiveMapEvent.InvolvedParties.Any(p =>
                            p?.MobileParty == army.LeaderParty ||
                            (army.Parties != null && army.Parties.Contains(p?.MobileParty)));
                    }
                }

                // DEBUG: Log participation status for debugging battle cleanup issues
                ModLogger.Info("BATTLE",
                    $"OnMapEventEnded participation check: player={playerParticipated}, lord={lordParticipated}, hasEncounter={PlayerEncounter.Current != null}");

                // Early exit for unrelated battles - don't log to avoid spam from all map battles
                if (!playerParticipated && !lordParticipated)
                {
                    // Still clear stale siege/encounter state defensively (prevents native menu re-opening loops).
                    CleanupPostEncounterState("OnMapEventEnded-Unrelated");
                    ModLogger.Debug("BATTLE", "Early exit - unrelated battle (no participation)");
                    _cachedLordMapEvent = null;
                    return;
                }

                // Only log diagnostics for battles that actually involved us
                LogPartyState("OnMapEventEnded-Start");

                // FIX: Proactive army state cleanup after naval battles
                // Native PlayerArmyWaitBehavior crashes when Army.LeaderParty is null but Army is not
                // This can happen after naval battles when the army state becomes inconsistent
                // We proactively clean up any invalid army references to prevent the crash
                ValidateAndCleanupArmyState(main, effectiveMapEvent?.IsNavalMapEvent == true);

                // CRITICAL: Check if enlistment ended during the battle (lord captured/army defeated)
                // Don't activate immediately - keep party inactive to prevent new encounters
                if (!IsEnlisted || _isOnLeave)
                {
                    // FIX: Only perform cleanup if we were actually enlisted when the battle started (tracked by _cachedLordMapEvent)
                    // If _cachedLordMapEvent is null, it means we were never enlisted for this battle (normal player scenario),
                    // so we must NOT interfere with the native encounter flow.
                    if (_cachedLordMapEvent == null)
                    {
                        return;
                    }

                    ModLogger.Info("BATTLE",
                        $"OnMapEventEnded early exit: IsEnlisted={IsEnlisted}, OnLeave={_isOnLeave}, main.HasMapEvent={main?.Party.MapEvent != null}, lordHasMapEvent={lordParty?.Party.MapEvent != null}, MapEventHasWinner={mapEvent?.HasWinner}");

                    // Enlistment ended during battle - cleanup army reference only
                    // StopEnlist already handled party activation state based on whether player was in battle
                    // Do NOT override IsActive/IsVisible here - that would revert StopEnlist's correct activation
                    // for wounded players who weren't in the battle
                    if (main?.Army != null)
                    {
                        main.Army = null;
                    }

                    if (PlayerEncounter.Current != null)
                    {
                        try
                        {
                            if (PlayerEncounter.InsideSettlement)
                            {
                                PlayerEncounter.LeaveSettlement();
                            }

                            PlayerEncounter.Finish();
                            ModLogger.Info("BATTLE", "Finished PlayerEncounter after enlistment ended");
                        }
                        catch (Exception ex)
                        {
                            ModLogger.Caught("BATTLE",
                                "Error finishing PlayerEncounter after enlistment ended", ex);
                        }
                    }

                    CleanupPostEncounterState("OnMapEventEnded-EnlistmentEnded");
                    _cachedLordMapEvent = null;
                    if (effectiveMapEvent != null)
                    {
                        RestoreCampaignFlowAfterBattle();
                    }

                    LogPartyState("OnMapEventEnded-EarlyExit");
                    return;
                }

                if (_enlistedLord == null)
                {
                    return;
                }

                LogPartyState("OnMapEventEnded-After");

                lordParty = _enlistedLord.PartyBelongedTo;
                if (lordParty == null)
                {
                    return;
                }

                // NULL-SAFE: Check if this was our lord's battle that ended
                if (effectiveMapEvent?.InvolvedParties != null &&
                    effectiveMapEvent.InvolvedParties.Any(p => p?.MobileParty == lordParty ||
                                                               (p?.MobileParty?.Army != null &&
                                                                p.MobileParty.Army == lordParty.Army)))
                {
                    ModLogger.Info("BATTLE", "Lord battle ended, returning to hidden state");
                    _cachedLordMapEvent = null;

                    // If the player chose to wait in reserve, make sure we clear the suspended state
                    // once the battle (or battle series) is actually over. Without this, the party
                    // can stay inactive and flagged as waiting, which blocks health regen until the
                    // player hits another major state change (reload, level, new battle, leave).
                    if (EnlistedEncounterBehavior.IsWaitingInReserve)
                    {
                        var lordStillInBattle = lordParty.Party.MapEvent != null;
                        var armyStillInBattle = lordParty.Army?.Parties?.Any(p => p?.Party.MapEvent != null) == true;

                        // Only clear reserve when no active battles remain for the lord/army
                        if (!lordStillInBattle && !armyStillInBattle)
                        {
                            EnlistedEncounterBehavior.ClearReserveState();

                            // Release any lingering wait flag on the encounter
                            var encounter = PlayerEncounter.Current;
                            if (encounter != null)
                            {
                                encounter.IsPlayerWaiting = false;
                                PlayerEncounter.LeaveEncounter = true;
                            }

                            var mainParty = MobileParty.MainParty;
                            if (mainParty != null && Hero.MainHero?.IsPrisoner != true)
                            {
                                mainParty.IsActive = true;   // ensure ticks resume (health, wages, etc.)
                                // Keep them hidden; visibility is managed elsewhere
                                if (!mainParty.IsVisible)
                                {
                                    mainParty.IsVisible = false;
                                }
                            }

                            ModLogger.Info("BATTLE",
                                "Cleared reserve state after battle end - restoring party activity for regen");
                        }
                    }

                    // Award battle loot share if we won (XP now comes from order system)
                    if (!EnlistedEncounterBehavior.IsWaitingInReserve && playerParticipated)
                    {
                        // Track battles for promotion requirements
                        if (!_battleXPAwardedThisBattle)
                        {
                            _battleXPAwardedThisBattle = true;
                            IncrementBattlesSurvived();
                        }

                        // Award battle loot share if we won.
                        var playerWon = effectiveMapEvent?.WinningSide == effectiveMapEvent?.PlayerSide;
                        if (playerParticipated && playerWon)
                        {
                            AwardBattleLootShare(effectiveMapEvent, true);
                        }
                    }
                    else
                    {
                        ModLogger.Debug("BATTLE", "Skipping battle XP - player waited in reserve");
                    }

                    // Determine if this was a siege battle - affects cleanup strategy
                    // CRITICAL: EventType == Siege covers auto-resolved sieges (where IsSiegeAssault is false).
                    // IsSiegeAssault/IsSallyOut are only true for manually-fought siege battles.
                    bool wasSiege = effectiveMapEvent?.EventType == MapEvent.BattleTypes.Siege ||
                                   effectiveMapEvent?.IsSiegeAssault == true ||
                                   effectiveMapEvent?.IsSallyOut == true ||
                                   effectiveMapEvent?.IsSiegeOutside == true ||
                                   lordParty?.BesiegedSettlement != null ||
                                   main?.BesiegedSettlement != null;

                    if (wasSiege)
                    {
                        // SIEGE BATTLES: With BesiegerCamp properly set (see TrySyncBesiegerCamp fix),
                        // the player is now correctly in _besiegerParties and collected into the MapEvent.
                        // Native's SiegeAftermathCampaignBehavior.OnMapEventEnded will:
                        // 1. Check IsMainPartyAmongParties() - should return TRUE now
                        // 2. Check Army.Parties.Contains(MainParty) - should return TRUE (we're in the army)
                        // 3. Set _wasPlayerArmyMember = true
                        // 4. Apply the aftermath using the lord's decision (player doesn't choose as subordinate)
                        //
                        // For subordinate army members, native applies aftermath immediately without showing
                        // a menu - only siege leaders get the menu_settlement_taken choice screen.
                        // So we just need to clean up and return to enlisted_status.
                        ModLogger.Info("BATTLE", "Siege battle ended - native handles aftermath, returning to enlisted state");

                        // Log siege integration status to verify player was properly integrated
                        if (main?.BesiegerCamp != null)
                        {
                            LogSiegeIntegrationStatus(main, lordParty, main.BesiegerCamp, "OnMapEventEnded-SiegeEnd");
                        }
                        else
                        {
                            ModLogger.Warn("SiegeIntegration",
                                "OnMapEventEnded: BesiegerCamp was null at siege end - integration may have been broken");
                        }

                        // Give native a frame to process aftermath before we clean up
                        // This ensures SiegeAftermathCampaignBehavior.OnMapEventEnded completes first
                        NextFrameDispatcher.RunNextFrame(() =>
                        {
                            try
                            {
                                var mainParty = MobileParty.MainParty;

                                // Clean up any lingering encounter state
                                CleanupPostEncounterState("OnMapEventEnded-SiegeVictory");

                                // Finish any lingering PlayerEncounter
                                if (PlayerEncounter.Current != null)
                                {
                                    PlayerEncounter.LeaveEncounter = true;
                                    if (PlayerEncounter.InsideSettlement)
                                    {
                                        PlayerEncounter.LeaveSettlement();
                                    }
                                    PlayerEncounter.Finish();
                                    ModLogger.Info("BATTLE", "Finished PlayerEncounter after siege victory");
                                }

                                // Return player to hidden enlisted state
                                if (mainParty != null && IsEnlisted)
                                {
                                    mainParty.IsActive = false;
                                    mainParty.IsVisible = false;
                                }

                                // Activate enlisted menu if still enlisted
                                if (IsEnlisted && !_isOnLeave)
                                {
                                    EnlistedMenuBehavior.SafeActivateEnlistedMenu();
                                    ModLogger.Info("BATTLE", "Activated enlisted menu after siege victory");
                                }
                            }
                            catch (Exception ex)
                            {
                                ModLogger.Caught("BATTLE", "Error in post-siege transition", ex);
                            }
                        });

                        return;
                    }

                    // FIELD BATTLES ONLY: Handle cleanup ourselves
                    // Field battles don't have structured native post-victory flow for army members
                    ModLogger.Info("BATTLE", "Field battle ended - handling cleanup for enlisted player");

                    // Log battle integration status at end
                    if (effectiveMapEvent != null)
                    {
                        LogBattleIntegrationStatus(main, lordParty, effectiveMapEvent, "OnMapEventEnded-FieldBattle");
                    }
                    LogEncounterStatus("OnMapEventEnded-FieldBattle");

                    CleanupPostEncounterState("OnMapEventEnded-FieldBattle");

                    if (PlayerEncounter.Current != null)
                    {
                        // FIELD BATTLE: Still set party inactive IMMEDIATELY to prevent new encounters
                        // from being created during the cleanup window. Then defer encounter finish.
                        // Without immediate deactivation, native game sees active party and creates
                        // new encounters for nearby enemies, causing "join_encounter" menu after
                        // auto-resolve when no menu should appear.
                        if (main != null && IsEnlisted)
                        {
                            main.IsActive = false;
                            main.IsVisible = false;
                            ModLogger.Debug("BATTLE",
                                "Player set to hidden immediately after field battle end");
                        }

                        // Defer encounter cleanup to next frame (safe for field battles)
                        // Field battles don't have the same AI race condition because parties
                        // don't change their besieging behavior on battle end.
                        NextFrameDispatcher.RunNextFrame(() =>
                        {
                            try
                            {
                                if (PlayerEncounter.Current != null)
                                {
                                    if (PlayerEncounter.InsideSettlement)
                                    {
                                        PlayerEncounter.LeaveSettlement();
                                    }

                                    PlayerEncounter.Finish();
                                    ModLogger.Info("BATTLE",
                                        "Finished PlayerEncounter after battle ended (deferred)");
                                }

                                // Ensure party stays hidden (may have been reactivated by native)
                                var mainParty = MobileParty.MainParty;
                                if (mainParty != null && IsEnlisted)
                                {
                                    mainParty.IsActive = false;
                                    mainParty.IsVisible = false;
                                }

                                RestoreCampaignFlowAfterBattle();
                            }
                            catch (Exception ex)
                            {
                                ModLogger.Caught("BATTLE",
                                    "Error in deferred encounter cleanup", ex);
                            }
                        });
                    }
                    else
                    {
                        // No encounter to finish - return to hidden enlisted state immediately
                        main.IsActive = false;
                        main.IsVisible = false;
                        ModLogger.Debug("BATTLE", "No encounter to finish - returning to hidden state");
                        RestoreCampaignFlowAfterBattle();
                    }

                    // Disband temporary army if we created one
                    if (_disbandArmyAfterBattle && lordParty.Army != null)
                    {
                        var army = lordParty.Army;
                        DisbandArmyAction.ApplyByUnknownReason(army);
                        _disbandArmyAfterBattle = false;
                        ModLogger.Debug("BATTLE", "Disbanded temporary army");
                    }

                    ModLogger.Info("BATTLE", "Battle cleanup complete");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("BATTLE", "Error in MapEvent end handler", ex);
            }
        }

        /// <summary>
        ///     Handles battle completion - awards battle XP, tracks kills, and updates term kill count.
        ///     This is the primary integration point for the kill tracker system.
        ///     CRITICAL: Only awards XP when battle actually ends with a winner - not when entering reserve mode.
        /// </summary>
        private void OnPlayerBattleEnd(MapEvent mapEvent)
        {
            try
            {
                ModLogger.Info("BATTLE", "Player battle ended");
                ModLogger.Info("BATTLE", $"Battle Type: {mapEvent?.EventType}, Was Siege: {mapEvent?.IsSiegeAssault}");

                // CRITICAL: Don't award XP if battle hasn't actually finished (no winner yet)
                // This prevents XP from being awarded when player clicks "Wait in Reserve"
                // which calls PlayerEncounter.Finish() but the battle is still ongoing
                if (mapEvent != null && !mapEvent.HasWinner)
                {
                    ModLogger.Debug("BATTLE", "Skipping battle rewards - battle still ongoing (no winner yet)");
                    return;
                }

                // CRITICAL: Don't award XP if player is entering "Wait in Reserve" mode
                // PlayerEncounter.Finish() triggers this event, but we're just entering reserve, not ending battle
                if (EnlistedEncounterBehavior.IsWaitingInReserve)
                {
                    ModLogger.Debug("BATTLE",
                        "Skipping battle rewards - player is entering reserve mode, not ending battle");
                    return;
                }

                // Only process if enlisted
                if (!IsEnlisted || _isOnLeave)
                {
                    ModLogger.Debug("BATTLE", "Skipping battle rewards - not enlisted or on leave");
                    return;
                }

                // Defensive cleanup: after a battle is actually over, stale MapEvent/BesiegerCamp/PlayerEncounter
                // can keep the engine reopening native encounter/siege menus.
                //
                // CRITICAL: Never mutate encounter state inside CampaignEvents.OnPlayerBattleEnd.
                // Native behaviors (notably CommentOnEndPlayerBattleBehavior) create PlayerBattleEndedLogEntry here,
                // which reads PlayerEncounter.Current.PlayerSide and MapEvent sides. If we finish PlayerEncounter or
                // clear MapEventSide inside this event, native can crash.
                //
                // Therefore: defer cleanup to next frame, after native handlers finish.
                NextFrameDispatcher.RunNextFrame(() =>
                {
                    CleanupPostEncounterState("OnPlayerBattleEnd-Deferred", includeEncounterFinish: false);
                }, true);

                // Get kill count from tracker (if available)
                var killsThisBattle = 0;
                bool participated;

                var killTracker = EnlistedKillTrackerBehavior.Instance;
                if (killTracker != null)
                {
                    killsThisBattle = killTracker.GetAndResetKillCount();
                    participated = killTracker.GetAndResetParticipation();
                    ModLogger.Info("BATTLE", $"Kill tracker: {killsThisBattle} kills, participated: {participated}");
                }
                else
                {
                    // Fallback: assume participation if we got to this event
                    // BUGFIX: Don't assume participation if morale is too low to fight (prevents XP spam loop)
                    if (MobileParty.MainParty.Morale <= 1f)
                    {
                        participated = false;
                        ModLogger.Debug("BATTLE", "Player morale too low for combat - participation denied");
                    }
                    else
                    {
                        participated = true;
                        ModLogger.Debug("BATTLE", "Kill tracker not available - assuming participation");
                    }
                }

                // Track battle participation for promotion requirements
                if (participated && !EnlistedEncounterBehavior.IsWaitingInReserve && !_battleXPAwardedThisBattle)
                {
                    _battleXPAwardedThisBattle = true;
                    IncrementBattlesSurvived();
                }

                // Flush any accumulated combat XP from the skill XP patch.
                // Native Bannerlord awards skill XP during combat based on enemy tier/power:
                //   XP = 0.4 × (attackerPower + 0.5) × (victimPower + 0.5) × damage × multiplier
                // Higher tier enemies have higher power, so killing T5 troops gives more XP than T1.
                // The patch accumulates this and we flush it here for enlistment rank progression.
                if (participated)
                {
                    SkillSuppressionPatch.FlushAccumulatedCombatXP();
                }

                // Add kills to current term total (persists to faction record on retirement)
                if (killsThisBattle > 0)
                {
                    _currentTermKills += killsThisBattle;
                    ModLogger.Info("BATTLE",
                        $"Term kills updated: +{killsThisBattle} = {_currentTermKills} total this term");

                    // Update Service Records for Retinue display
                    ServiceRecordManager.Instance?.OnKillsRecorded(killsThisBattle);
                }

                // Process supply changes from battle (casualties, loot, siege equipment usage)
                ProcessBattleSupplyChanges(mapEvent, killsThisBattle);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("BATTLE", "Error in OnPlayerBattleEnd", ex);
            }
        }

        /// <summary>
        /// Processes supply changes after a battle based on casualties, outcome, and loot.
        /// Updates CompanySupplyManager with losses from friendly casualties and gains from enemy loot.
        /// </summary>
        private void ProcessBattleSupplyChanges(MapEvent mapEvent, int playerKills)
        {
            try
            {
                if (CompanySupplyManager.Instance == null || mapEvent == null)
                {
                    return;
                }

                // Determine if player's side won
                var playerSide = mapEvent.PlayerSide;
                var playerWon = mapEvent.WinningSide == playerSide;

                // Count casualties ONLY from the enlisted lord's party (the Company)
                // Bug fix: Previously counted all army casualties, causing -117% supply from large battles
                int troopsLost = 0;
                var lordParty = _enlistedLord?.PartyBelongedTo;

                if (lordParty != null)
                {
                    var playerMapEventSide = playerSide == BattleSideEnum.Attacker
                        ? mapEvent.AttackerSide
                        : mapEvent.DefenderSide;

                    if (playerMapEventSide?.Parties != null)
                    {
                        foreach (var partyEvent in playerMapEventSide.Parties)
                        {
                            // Only count casualties from the lord's party, not the entire army
                            if (partyEvent?.Party?.MobileParty == lordParty)
                            {
                                troopsLost += partyEvent.DiedInBattle?.TotalManCount ?? 0;
                                troopsLost += partyEvent.WoundedInBattle?.TotalManCount ?? 0;
                                break; // Found our lord's party, no need to continue
                            }
                        }
                    }
                }

                // Count enemy casualties for loot calculation (player's kills only, not army-wide)
                // This is fair since the player only gets loot from their personal contribution
                int enemiesKilled = playerKills;

                // Determine if this was a siege battle
                bool wasSiege = mapEvent.IsSiegeAssault || mapEvent.IsSallyOut;

                // Process supply changes through the manager
                CompanySupplyManager.Instance.ProcessBattleSupplyChanges(
                    troopsLost,
                    enemiesKilled,
                    playerWon,
                    wasSiege);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Supply", "Error processing battle supply changes", ex);
            }
        }

        /// <summary>
        ///     Centralized post-encounter cleanup for enlisted play.
        ///     This is the shared fix for both:
        ///     - Siege menus re-opening after victory (stale BesiegerCamp makes native return menu_siege_strategies)
        ///     - Encounter menus re-opening after victory (stale MapEvent/PlayerEncounter keeps native on encounter)
        /// </summary>
        private void CleanupPostEncounterState(string context, bool includeEncounterFinish = true)
        {
            try
            {
                var main = MobileParty.MainParty;
                if (main == null)
                {
                    return;
                }

                var isUnrelatedContext = context != null && context.IndexOf("Unrelated", StringComparison.OrdinalIgnoreCase) >= 0;
                if (isUnrelatedContext)
                {
                    ModLogger.Debug("EncounterCleanup", $"PostEncounterState({context}) BEFORE: {BuildPostEncounterStateSnapshot(main)}");
                }
                else
                {
                    ModLogger.Info("EncounterCleanup", $"PostEncounterState({context}) BEFORE: {BuildPostEncounterStateSnapshot(main)}");
                }

                // 1) Siege state: if there's no active siege but BesiegerCamp is still set, native will keep
                // returning menu_siege_strategies from GetGenericStateMenu(). Clear it.
                //
                // IMPORTANT: We must NOT clear BesiegerCamp during an active siege, otherwise the player
                // cannot open the native siege menu to participate (attack/defend/continue).
                // We only clear when the party is not involved in a siege.
                if (!IsPartyInActiveSiege(main) && main.BesiegerCamp != null)
                {
                    main.BesiegerCamp = null;
                    ModLogger.Info("Siege", $"CleanupPostEncounterState({context}): Cleared BesiegerCamp (no active siege)");
                }

                // 2) Battle state: if the MapEvent already has a winner but we're still attached to a side,
                // native can keep returning the encounter menu. Detach from the MapEvent.
                var mapEvent = main.Party?.MapEvent;
                if (mapEvent != null && (mapEvent.HasWinner || mapEvent.IsFinalized) && main.MapEventSide != null)
                {
                    main.MapEventSide = null;
                    ModLogger.Info("BATTLE", $"CleanupPostEncounterState({context}): Cleared MapEventSide (battle over)");
                }

                if (includeEncounterFinish)
                {
                    // 3) Encounter state: if PlayerEncounter is still alive after a battle is effectively over,
                    // it can keep the engine in encounter-related menus. Finish it when safe.
                    var encounter = PlayerEncounter.Current;
                    var encounterBattle = PlayerEncounter.Battle;
                    var encounterHasWinner = encounterBattle?.HasWinner == true;

                    // CRITICAL FIX: Do NOT finish the encounter if the player is inside a settlement
                    // and has explicitly visited it (clicked "Visit Settlement"). Finishing the encounter
                    // while the player is in the town wait menu (or any other settlement submenu) would
                    // kick them out of the settlement unexpectedly.
                    // This fixes the bug where clicking "Wait here for some time" in town would cause
                    // the town menu to disappear when an unrelated battle ends elsewhere on the map.
                    var isPlayerInSettlement = PlayerEncounter.InsideSettlement ||
                                               EnlistedMenuBehavior.HasExplicitlyVisitedSettlement;

                    if (encounter != null && (encounterBattle == null || encounterHasWinner) && !isPlayerInSettlement)
                    {
                        PlayerEncounter.LeaveEncounter = true;
                        if (PlayerEncounter.InsideSettlement)
                        {
                            PlayerEncounter.LeaveSettlement();
                        }

                        try
                        {
                            PlayerEncounter.Finish();
                            ModLogger.Info("EncounterCleanup",
                                $"CleanupPostEncounterState({context}): Finished PlayerEncounter");
                        }
                        catch (Exception ex)
                        {
                            ModLogger.Warn("EncounterCleanup",
                                $"CleanupPostEncounterState({context}): Finish failed (safety patch may handle): {ex.Message}");
                        }
                    }
                    else if (encounter != null && isPlayerInSettlement)
                    {
                        ModLogger.Debug("EncounterCleanup",
                            $"CleanupPostEncounterState({context}): Skipped finishing encounter - player is inside settlement");
                    }
                }

                if (isUnrelatedContext)
                {
                    ModLogger.Debug("EncounterCleanup", $"PostEncounterState({context}) AFTER: {BuildPostEncounterStateSnapshot(main)}");
                }
                else
                {
                    ModLogger.Info("EncounterCleanup", $"PostEncounterState({context}) AFTER: {BuildPostEncounterStateSnapshot(main)}");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("EncounterCleanup",
                    $"CleanupPostEncounterState failed ({context})", ex);
            }
        }

        /// <summary>
        /// Allows Harmony patches to trigger the centralized post-encounter cleanup without exposing
        /// the full internal implementation surface.
        /// </summary>
        internal void CleanupPostEncounterStateFromPatch(string context)
        {
            // Patches can run during sensitive native transitions (e.g. encounter menu init, battle-end).
            // Defer to next frame so we don't break native handlers that still expect PlayerEncounter/MapEventSide.
            NextFrameDispatcher.RunNextFrame(() => CleanupPostEncounterState(context, includeEncounterFinish: false), true);
        }

        private string BuildPostEncounterStateSnapshot(MobileParty main)
        {
            try
            {
                var mapEvent = main?.Party?.MapEvent;
                var hasMapEvent = mapEvent != null;
                var mapEventSummary = hasMapEvent
                    ? $"type={mapEvent.EventType}, hasWinner={mapEvent.HasWinner}, finalized={mapEvent.IsFinalized}, state={mapEvent.State}"
                    : "none";

                var camp = main?.BesiegerCamp;
                var siegeSummary = camp?.SiegeEvent?.BesiegedSettlement != null
                    ? $"settlement={camp.SiegeEvent.BesiegedSettlement.Name}, leader={camp.LeaderParty?.LeaderHero?.Name?.ToString() ?? "unknown"}"
                    : (camp != null ? "present(no SiegeEvent/BesiegedSettlement)" : "none");

                var encounter = PlayerEncounter.Current;
                var encounterBattle = PlayerEncounter.Battle;
                var encounterSummary = encounter == null
                    ? "none"
                    : $"hasBattle={(encounterBattle != null)}, battleHasWinner={(encounterBattle?.HasWinner == true)}, leaveEncounter={PlayerEncounter.LeaveEncounter}, insideSettlement={PlayerEncounter.InsideSettlement}";

                var attachedToName = main?.AttachedTo?.LeaderHero?.Name?.ToString()
                                     ?? main?.AttachedTo?.Name?.ToString()
                                     ?? "none";

                return
                    $"enlisted={IsEnlisted}, onLeave={_isOnLeave}, active={main.IsActive}, visible={main.IsVisible}, " +
                    $"army={(main.Army?.LeaderParty?.LeaderHero?.Name?.ToString() ?? "none")}, attachedTo={attachedToName}, " +
                    $"mapEvent={mapEventSummary}, mapEventSide={(main.MapEventSide != null)}, besiegerCamp={siegeSummary}, playerEncounter={encounterSummary}";
            }
            catch (Exception ex)
            {
                return $"snapshot_failed: {ex.GetType().Name}: {ex.Message}";
            }
        }

        /// <summary>
        /// Handles player state cleanup when lord is captured.
        /// - If player was in reserve: clears reserve state, finishes encounter, teleports to safety
        /// - If player is in encounter: native surrender flow handles capture
        /// - Otherwise: just logs (edge case)
        /// Does NOT directly capture the player - native game handles actual capture via surrender.
        /// </summary>
        private void HandlePlayerStateOnLordCapture(PartyBase capturingParty)
        {
            if (!IsEnlisted || _isOnLeave || Hero.MainHero?.IsPrisoner == true)
            {
                return;
            }

            // Check if player was waiting in reserve - if so, clean up the reserve state
            // so the player can appear on the map after the battle ends
            var wasInReserve = EnlistedEncounterBehavior.IsWaitingInReserve;

            if (wasInReserve)
            {
                // Clean up reserve state and IMMEDIATELY finish the encounter
                // Setting LeaveEncounter = true doesn't work reliably because:
                // 1. It's only processed on future menu ticks
                // 2. GameMenu.ExitToLast() can interrupt normal tick processing
                // 3. If game time is paused (menus), the encounter stays stuck forever
                // 4. This caused players to be stuck invisible with no way to recover
                EnlistedEncounterBehavior.ClearReserveState();

                // CRITICAL: Immediately finish the encounter - don't rely on LeaveEncounter flag
                var encounter = PlayerEncounter.Current;
                if (encounter != null)
                {
                    try
                    {
                        encounter.IsPlayerWaiting = false;
                        PlayerEncounter.LeaveEncounter = true;
                        // Force immediate cleanup - this clears PlayerEncounter.Current
                        PlayerEncounter.Finish();
                        ModLogger.Info("BATTLE", "Force-finished PlayerEncounter to prevent stuck state");
                    }
                    catch (Exception ex)
                    {
                        ModLogger.Caught("BATTLE", "Error finishing encounter - will rely on watchdog", ex);
                    }
                }

                // Apply protection so enemies don't immediately attack when restored
                var mainParty = MobileParty.MainParty;
                if (mainParty != null)
                {
                    var protectionDuration = CampaignTime.Hours(12f);
                    mainParty.IgnoreByOtherPartiesTill(CampaignTime.Now + protectionDuration);

                    // Move player to nearest friendly settlement to escape the battle
                    TryTeleportToSafety(mainParty, capturingParty);

                    // Since we've finished the encounter, StopEnlist won't see it as "in battle state"
                    // and will activate the party directly - this is what we want
                }

                // Exit the menu so player returns to campaign map
                GameMenu.ExitToLast();

                ModLogger.Info("BATTLE", "Player escaped during lord capture (was in reserve) - teleported to safety");
                return;
            }

            if (PlayerEncounter.Current != null)
            {
                // Player is already in an encounter (e.g., choosing Surrender). Native flow will capture them.
                ModLogger.Info("BATTLE",
                    "Encounter active during lord capture - native surrender flow will handle player.");
                return;
            }

            // Player wasn't in reserve and has no active encounter - edge case, just log
            ModLogger.Info("BATTLE", "Lord captured but player not in reserve or encounter - no action needed");
        }

        private void SchedulePostEncounterVisibilityRestore()
        {
            if (_pendingVisibilityRestore)
            {
                return;
            }

            _pendingVisibilityRestore = true;
            _pendingVisibilityRestoreStartTime = CampaignTime.Now;
            NextFrameDispatcher.RunNextFrame(RestoreVisibilityAfterEncounter, true);
        }

        private void RestoreVisibilityAfterEncounter()
        {
            if (!_pendingVisibilityRestore)
            {
                return;
            }

            var mainParty = MobileParty.MainParty;
            if (mainParty == null)
            {
                _pendingVisibilityRestore = false;
                return;
            }

            var mainHero = Hero.MainHero;

            // Wait until encounter/MapEvent clears before restoring visibility
            if (PlayerEncounter.Current != null || mainParty.Party?.MapEvent != null)
            {
                // If enlistment already ended and the encounter persists without a MapEvent, force-finish after a short timeout.
                // This prevents the player from remaining invisible/inactive because PostDischargeProtection blocks reactivation.
                var encounter = PlayerEncounter.Current;
                if ((!IsEnlisted || _isOnLeave) && mainHero != null && !mainHero.IsPrisoner &&
                    encounter != null && mainParty.Party?.MapEvent == null &&
                    _pendingVisibilityRestoreStartTime != CampaignTime.Zero &&
                    CampaignTime.Now - _pendingVisibilityRestoreStartTime > CampaignTime.Seconds(5L))
                {
                    ModLogger.Warn("EncounterCleanup",
                        $"Force finishing lingering PlayerEncounter after discharge (requested at {_pendingVisibilityRestoreStartTime})");
                    ForceFinishLingeringEncounter("VisibilityRestoreTimeout");
                    _pendingVisibilityRestoreStartTime = CampaignTime.Now;
                }

                NextFrameDispatcher.RunNextFrame(RestoreVisibilityAfterEncounter, true);
                return;
            }

            // CRITICAL: Don't restore visibility while the player is a prisoner.
            // Native PlayerCaptivity deactivates the party; fighting it causes state corruption.
            // Keep waiting until captivity ends (native will restore party state on release).
            if (mainHero != null && mainHero.IsPrisoner)
            {
                NextFrameDispatcher.RunNextFrame(RestoreVisibilityAfterEncounter, true);
                return;
            }

            _pendingVisibilityRestore = false;
            _pendingVisibilityRestoreStartTime = CampaignTime.Zero;

            // CRITICAL: If in grace period, apply protection BEFORE making visible
            // This prevents enemies from immediately attacking the player after discharge
            if (IsInDesertionGracePeriod)
            {
                var protectionUntil = CampaignTime.Now + CampaignTime.Hours(12f);
                mainParty.IgnoreByOtherPartiesTill(protectionUntil);
                _graceProtectionEnds = protectionUntil;
                ModLogger.Info("ENLISTMENT",
                    $"Applied grace protection during visibility restore (until {protectionUntil})");
            }

            if (!mainParty.IsActive)
            {
                mainParty.IsActive = true;
            }

            if (!mainParty.IsVisible)
            {
                mainParty.IsVisible = true;
            }

            // CRITICAL: Reset camera to follow player's party instead of lord's party
            // Without this, the camera remains stuck following the lord after discharge,
            // causing movement softlock where player can't control their character
            try
            {
                mainParty.Party.SetAsCameraFollowParty();
            }
            catch (Exception cameraEx)
            {
                ModLogger.Caught("ENLISTMENT", "Failed to reset camera to player party", cameraEx);
            }

            ModLogger.Info("ENLISTMENT", "Party visibility restored after encounter cleanup");
            ForceFinishLingeringEncounter("VisibilityRestore");
        }

        private void SchedulePlayerCaptureCleanup(Kingdom lordKingdom)
        {
            _pendingPlayerCaptureKingdom = lordKingdom;
            _pendingPlayerCaptureReason = lordKingdom != null ? "Player captured" : "Player captured (No Kingdom)";

            if (_playerCaptureCleanupScheduled)
            {
                return;
            }

            _playerCaptureCleanupScheduled = true;
            NextFrameDispatcher.RunNextFrame(FinalizePlayerCaptureCleanup, true);
        }

        private void FinalizePlayerCaptureCleanup()
        {
            if (!_playerCaptureCleanupScheduled)
            {
                return;
            }

            var mainParty = MobileParty.MainParty;
            if (PlayerEncounter.Current != null || mainParty?.Party?.MapEvent != null)
            {
                NextFrameDispatcher.RunNextFrame(FinalizePlayerCaptureCleanup, true);
                return;
            }

            _playerCaptureCleanupScheduled = false;

            var reason = _pendingPlayerCaptureReason ?? "Player captured";
            var kingdom = _pendingPlayerCaptureKingdom;
            _pendingPlayerCaptureReason = null;
            _pendingPlayerCaptureKingdom = null;

            if (!IsEnlisted && !_isOnLeave && !IsInDesertionGracePeriod)
            {
                return;
            }

            ModLogger.Info("EVENTSAFETY", "Finalizing deferred capture cleanup for player");

            // If the lord party vanished (captor destroyed/captured), companion restore will skip safely.
            if (_enlistedLord?.PartyBelongedTo == null)
            {
                ModLogger.Warn("EVENTSAFETY", "Capture cleanup: lord party missing, companion restore may be skipped");
            }

            if (kingdom != null)
            {
                StopEnlist(reason, false, true);
                StartDesertionGracePeriod(kingdom);
                GrantGracePeriodInteractionWindow();
                ForceFinishLingeringEncounter("CaptureCleanup");
            }
            else
            {
                StopEnlist("Player captured (No Kingdom)");
                GrantGracePeriodInteractionWindow();
                ForceFinishLingeringEncounter("CaptureCleanup");
            }
        }

        /// <summary>
        ///     Gives the player a short invulnerability window after discharge/capture so they can interact with NPCs.
        ///     Keeps the party active/visible but ignored by hostile AI for one in-game hour.
        /// </summary>
        private void GrantGracePeriodInteractionWindow()
        {
            try
            {
                var main = MobileParty.MainParty;
                if (main == null)
                {
                    return;
                }

                // CRITICAL: Do NOT activate party while prisoner - captivity system owns player state
                // Making the party active/visible while prisoner allows enemies to attack us in dungeon
                if (Hero.MainHero?.IsPrisoner == true)
                {
                    ModLogger.Info("Captivity", "Skipping grace protection activation - player is prisoner");
                    // Still set the protection timer so it's ready when released
                    _graceProtectionEnds = CampaignTime.Now + CampaignTime.Hours(12f);
                    return;
                }

                // Skip if the game still has an encounter/battle running
                if (main.Party?.MapEvent != null || PlayerEncounter.Current != null)
                {
                    ModLogger.Debug("GraceProtection", "Delaying interaction window - still in encounter/battle state");
                    NextFrameDispatcher.RunNextFrame(GrantGracePeriodInteractionWindow, true);
                    return;
                }

                var protectionUntil = CampaignTime.Now + CampaignTime.Hours(12f);
                main.IgnoreByOtherPartiesTill(protectionUntil);
                _graceProtectionEnds = protectionUntil;

                if (!main.IsActive)
                {
                    main.IsActive = true;
                }

                main.IsVisible = true;
                ModLogger.Info("GraceProtection",
                    $"Granted 1-day protection window after discharge (ignored until {protectionUntil})");
                ForceFinishLingeringEncounter("GraceProtection");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("GraceProtection", "Failed to grant grace period interaction window", ex);
            }
        }

        private void RestoreCampaignFlowAfterBattle()
        {
            try
            {
                var campaign = Campaign.Current;
                if (campaign == null)
                {
                    ModLogger.Warn("BATTLE", "RestoreCampaignFlowAfterBattle: Campaign is null");
                    return;
                }

                var menuContext = campaign.CurrentMenuContext;
                var currentMenuId = menuContext?.GameMenu?.StringId ?? "none";
                var mainParty = MobileParty.MainParty;

                ModLogger.Info("BATTLE", $"RestoreCampaignFlowAfterBattle START: currentMenu={currentMenuId}, timeControl={campaign.TimeControlMode}, encounter={PlayerEncounter.Current != null}, partyActive={mainParty?.IsActive}, partyVisible={mainParty?.IsVisible}");

                // Centralized safety net: clear stale siege/encounter state so native menus don't re-open.
                CleanupPostEncounterState("RestoreCampaignFlowAfterBattle");

                if (campaign.TimeControlMode == CampaignTimeControlMode.Stop)
                {
                    campaign.TimeControlMode = CampaignTimeControlMode.StoppablePlay;
                    ModLogger.Info("BATTLE", "Restored campaign time control to StoppablePlay");
                }

                if (!string.IsNullOrEmpty(currentMenuId) && currentMenuId != "none" &&
                    (currentMenuId == "army_wait" ||
                     currentMenuId.Contains("siege") ||
                     currentMenuId.Contains("encounter")))
                {
                    ModLogger.Info("BATTLE", $"RestoreCampaignFlow: Attempting GameMenu.ExitToLast() for menu '{currentMenuId}' at {DateTime.Now:HH:mm:ss.fff}");
                    GameMenu.ExitToLast();

                    // Check if it actually closed - need to let it process
                    System.Threading.Thread.Sleep(10); // Give it 10ms to process
                    var afterMenuId = Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId ?? "none";
                    ModLogger.Info("BATTLE", $"RestoreCampaignFlow: After ExitToLast menu is now '{afterMenuId}' at {DateTime.Now:HH:mm:ss.fff}");

                    if (afterMenuId != "none" && afterMenuId.Contains("siege"))
                    {
                        ModLogger.Warn("BATTLE", "WARNING: Siege menu still present after ExitToLast! Menu system may be re-opening it.");
                    }
                }
                else
                {
                    ModLogger.Info("BATTLE", $"RestoreCampaignFlow: Not exiting menu (current: {currentMenuId})");
                }

                // Handle menu restoration after battle ends
                // Check if we're at a siege - if so, let native siege menu show
                var lordParty = _enlistedLord?.PartyBelongedTo;
                var lordInSiege = lordParty?.SiegeEvent != null ||
                                  lordParty?.BesiegerCamp != null ||
                                  lordParty?.BesiegedSettlement != null;

                // If lord is in an army, also check if the army leader is besieging
                var lordArmy = lordParty?.Army;
                if (!lordInSiege && lordArmy != null)
                {
                    var armyLeader = lordArmy.LeaderParty;
                    lordInSiege = armyLeader?.BesiegerCamp != null ||
                                  armyLeader?.BesiegedSettlement != null ||
                                  armyLeader?.SiegeEvent != null;
                }

                if (IsEnlisted && !_isOnLeave && !lordInSiege)
                {
                    EnlistedMenuBehavior.SafeActivateEnlistedMenu();
                    ModLogger.Info("BATTLE", "Activated enlisted_status menu after battle");
                }
                else if (lordInSiege)
                {
                    ModLogger.Info("BATTLE", "Skipping menu activation - letting native siege menu show (lord/army at siege)");
                }
                else
                {
                    // Discharged during battle - ensure party is properly activated
                    // StopEnlist handles this, but as a safety net verify state is correct
                    var playerParty = MobileParty.MainParty;
                    if (playerParty != null && !playerParty.IsActive)
                    {
                        playerParty.IsActive = true;
                        playerParty.IsVisible = true;
                        ModLogger.Info("BATTLE", "Safety recovery: activated party after discharge-during-battle");
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("BATTLE", "Error restoring campaign flow after battle", ex);
            }
        }

        private void ForceFinishLingeringEncounter(string context)
        {
            try
            {
                if (PlayerEncounter.Current == null)
                {
                    return;
                }

                PlayerEncounter.LeaveEncounter = true;

                if (PlayerEncounter.InsideSettlement)
                {
                    PlayerEncounter.LeaveSettlement();
                }

                var menuContext = Campaign.Current?.CurrentMenuContext;
                var currentMenuId = menuContext?.GameMenu?.StringId;
                if (!string.IsNullOrEmpty(currentMenuId) && currentMenuId.Contains("encounter"))
                {
                    GameMenu.ExitToLast();
                }

                ModLogger.Info("EncounterCleanup", $"Requested encounter exit after {context}");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("EncounterCleanup",
                    $"Failed to finish lingering PlayerEncounter after {context}", ex);
            }
        }

        #endregion

        #region Diagnostics

        public bool IsEmbeddedWithLord()
        {
            if (!IsEnlisted || _enlistedLord == null)
            {
                return false;
            }

            var main = MobileParty.MainParty;
            var lordParty = _enlistedLord.PartyBelongedTo;
            if (main == null || lordParty == null)
            {
                return false;
            }

            // Check if in same army or actively following via Escort AI
            // NOTE: We use TargetParty instead of AttachedTo because AttachedTo crashes
            // GetGenericStateMenu() when the player isn't in an army
            var sameArmy = main.Army != null && lordParty.Army != null && main.Army == lordParty.Army;
            bool following = main.TargetParty == lordParty;
            return sameArmy || following;
        }

        public bool IsPartyInActiveSiege(MobileParty party)
        {
            if (party == null)
            {
                return false;
            }

            try
            {
                if (party.Party?.SiegeEvent != null)
                {
                    return true;
                }

                if (party.BesiegerCamp != null && party.BesiegerCamp.SiegeEvent != null)
                {
                    return true;
                }

                var siegeEvent = party.BesiegedSettlement?.SiegeEvent;
                if (siegeEvent != null && party.Party != null)
                {
                    return siegeEvent.IsPartyInvolved(party.Party);
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Diagnostics",
                    $"Error evaluating siege state for {party.LeaderHero?.Name.ToString() ?? "unknown"}", ex);
            }

            return false;
        }

        /// <summary>
        /// Validates and cleans up potentially invalid army state to prevent native game crashes.
        /// The native PlayerArmyWaitBehavior crashes when Army is not null but LeaderParty is null,
        /// which can happen after naval battles when the army state becomes inconsistent.
        /// This proactive cleanup prevents the crash by removing invalid army references.
        /// </summary>
        /// <param name="mainParty">The player's main party to check.</param>
        /// <param name="wasNavalBattle">Whether the battle that just ended was a naval battle.</param>
        private void ValidateAndCleanupArmyState(MobileParty mainParty, bool wasNavalBattle)
        {
            if (mainParty == null)
            {
                return;
            }

            try
            {
                var army = mainParty.Army;
                if (army == null)
                {
                    return;
                }

                // Check for invalid army state: Army exists but LeaderParty is null or invalid
                // This is the exact crash condition in PlayerArmyWaitBehavior
                var leaderParty = army.LeaderParty;
                if (leaderParty == null)
                {
                    // Invalid state - remove the army reference to prevent crash
                    mainParty.Army = null;
                    ModLogger.Info("Naval",
                        "Cleaned up invalid army state after battle (LeaderParty was null). " +
                        "This prevents a native game crash in PlayerArmyWaitBehavior.");
                    return;
                }

                // Additional validation for naval battles - check if the army is still coherent
                if (wasNavalBattle)
                {
                    // After naval battles, verify the army leader is still valid and alive
                    var leaderHero = leaderParty.LeaderHero;
                    if (leaderHero == null || !leaderHero.IsAlive)
                    {
                        // Leader is gone - army should have been disbanded but wasn't
                        mainParty.Army = null;
                        ModLogger.Info("Naval",
                            "Cleaned up army reference after naval battle (leader deceased or missing). " +
                            "This prevents potential crashes.");
                        return;
                    }

                    // Check if the army is at sea but has no sea capability
                    // This can cause issues with the army_wait menu trying to access sea state
                    if (leaderParty.IsCurrentlyAtSea && !leaderParty.HasNavalNavigationCapability)
                    {
                        ModLogger.Warn("Naval",
                            "Army leader is at sea without naval capability after battle - " +
                            "native game may have state issues");
                        // Don't remove army here - just log for diagnostics
                    }

                    ModLogger.Debug("Naval",
                        $"Post-naval battle army state validated: Leader={leaderHero.Name}, " +
                        $"AtSea={leaderParty.IsCurrentlyAtSea}, ArmySize={army.Parties.Count}");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Naval", "Error validating army state", ex);

                // On error, try to clean up anyway to prevent cascading crashes
                try
                {
                    mainParty.Army = null;
                    ModLogger.Info("Naval", "Forcibly cleared army reference after validation error");
                }
                catch
                {
                    // Can't even clear - leave it as is
                }
            }
        }

        /// <summary>
        ///     Log party state for diagnostics (Debug level only - not verbose in production).
        ///     Only logs on critical state transitions, not every frame.
        /// </summary>
        private void LogPartyState(string context)
        {
            try
            {
                var main = MobileParty.MainParty;
                var lordParty = _enlistedLord?.PartyBelongedTo;
                if (main == null)
                {
                    ModLogger.Debug("Diagnostics", $"{context}: Main party null");
                    return;
                }

                // Lightweight state summary - only key flags, not full details
                var info =
                    $"{context}: Active={main.IsActive}, Visible={main.IsVisible}, " +
                    $"InBattle={main.Party.MapEvent != null}, InSiege={main.Party.SiegeEvent != null}, " +
                    $"LordInBattle={lordParty?.Party.MapEvent != null}";
                ModLogger.Debug("Diagnostics", info);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Diagnostics", "Error logging party state", ex);
            }
        }

        /// <summary>
        ///     Called after a mission (battle) starts.
        ///     Note: We no longer add mission behaviors dynamically as this can cause crashes.
        ///     Kill tracking is now handled via campaign events instead.
        /// </summary>
        private void OnAfterMissionStarted(IMission mission)
        {
            try
            {
                // Only process if player is enlisted and not on leave
                if (!IsEnlisted || _isOnLeave)
                {
                    return;
                }

                // DISABLED: Adding MissionBehaviors dynamically during AfterMissionStarted
                // causes crashes because the mission lifecycle methods get called in an unstable state.
                // Instead, we track participation via campaign events (OnMapEventEnded, OnPlayerBattleEnd).
                // Kill XP is calculated based on the native kill counter when available.

                ModLogger.Debug("BATTLE", "Mission started while enlisted - using campaign event tracking");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("BATTLE", "Error in OnAfterMissionStarted", ex);
            }
        }

        #endregion
    }

    // NOTE: FactionVeteranRecord has been consolidated into FactionServiceRecord.
    // All per-faction tracking is now done through ServiceRecordManager.Instance.GetOrCreateRecord().
}
