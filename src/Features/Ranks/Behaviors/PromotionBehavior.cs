using System;
using System.Collections.Generic;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Equipment.Behaviors;
using Enlisted.Features.Escalation;
using Enlisted.Features.Interface.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Enlisted.Features.Ranks.Behaviors
{
    /// <summary>
    /// Requirements for promotion to each tier.
    /// Based on the standard promotion requirements table.
    /// </summary>
    public sealed class PromotionRequirements
    {
        public int DaysInRank { get; set; }
        public int BattlesRequired { get; set; }
        public int MinLeaderRelation { get; set; }
        public int MaxScrutiny { get; set; }

        /// <summary>
        ///     Get promotion requirements for a specific tier transition.
        ///     Note: XP requirements come from progression_config.json, not this table.
        ///     This table defines other requirements: days, battles, reputation, etc.
        /// </summary>
        public static PromotionRequirements GetForTier(int targetTier)
        {
            // Promotion requirements table (XP values shown for reference only - actual values from progression_config.json):
            // NOTE: Scrutiny is now 0-100 scale (merged discipline into scrutiny). Thresholds scaled x10.
            // | Promotion | XP | Days | Battles | Leader Rel | Max Scrutiny |
            // |-----------|-----|------|---------|------------|-------------|
            // | T1→T2 | 700 | 14 | 2 | ≥0 | <80 |
            // | T2→T3 | 2,200 | 35 | 6 | ≥10 | <70 |
            // | T3→T4 | 4,400 | 56 | 12 | ≥20 | <60 |
            // | T4→T5 | 6,600 | 56 | 20 | ≥30 | <50 |
            // | T5→T6 | 8,800 | 56 | 30 | ≥15 | <40 |
            // | T6→T7 | 11,000 | 70 | 40 | ≥20 | <30 |
            // | T7→T8 | 14,000 | 84 | 50 | ≥25 | <20 |
            // | T8→T9 | 18,000 | 112 | 60 | ≥30 | <10 |

            return targetTier switch
            {
                2 => new PromotionRequirements { DaysInRank = 14, BattlesRequired = 2, MinLeaderRelation = 0, MaxScrutiny = 80 },
                3 => new PromotionRequirements { DaysInRank = 35, BattlesRequired = 6, MinLeaderRelation = 10, MaxScrutiny = 70 },
                4 => new PromotionRequirements { DaysInRank = 56, BattlesRequired = 12, MinLeaderRelation = 20, MaxScrutiny = 60 },
                5 => new PromotionRequirements { DaysInRank = 56, BattlesRequired = 20, MinLeaderRelation = 30, MaxScrutiny = 50 },
                6 => new PromotionRequirements { DaysInRank = 56, BattlesRequired = 30, MinLeaderRelation = 15, MaxScrutiny = 40 },
                7 => new PromotionRequirements { DaysInRank = 70, BattlesRequired = 40, MinLeaderRelation = 20, MaxScrutiny = 30 },
                8 => new PromotionRequirements { DaysInRank = 84, BattlesRequired = 50, MinLeaderRelation = 25, MaxScrutiny = 20 },
                9 => new PromotionRequirements { DaysInRank = 112, BattlesRequired = 60, MinLeaderRelation = 30, MaxScrutiny = 10 },
                _ => new PromotionRequirements { DaysInRank = 999, BattlesRequired = 999, MinLeaderRelation = 999, MaxScrutiny = 0 }
            };
        }
    }

    /// <summary>
    ///     Promotion and advancement system for military service progression.
    ///     This system handles tier advancement, promotion notifications, and integration
    ///     with the troop selection system. Implements 1-year military progression with
    ///     meaningful advancement milestones and realistic military economics.
    /// </summary>
    public sealed class PromotionBehavior : CampaignBehaviorBase
    {
        // Promotion tracking
        private CampaignTime _lastPromotionCheck = CampaignTime.Zero;

        /// <summary>
        /// Track pending promotion tier to prevent event spam.
        /// Reset when promotion completes or is cancelled.
        /// </summary>
        private int _pendingPromotionTier;

        public PromotionBehavior()
        {
            Instance = this;
        }

        public static PromotionBehavior Instance { get; private set; }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);

            // Subscribe to enlistment events to reset promotion state when enlistment changes.
            // This fixes a bug where _pendingPromotionTier persisted across enlistments,
            // blocking promotions if the previous lord was defeated while a proving event was pending.
            EnlistmentBehavior.OnEnlisted += OnNewEnlistmentStarted;
            EnlistmentBehavior.OnDischarged += OnEnlistmentEnded;
        }

        /// <summary>
        /// Resets promotion tracking state when a new enlistment starts.
        /// Clears pending promotion tier and declined promotion flags to give the player
        /// a fresh start with the new lord. This fixes the bug where promotion could be
        /// permanently blocked if the previous lord was defeated while a proving event was pending.
        /// </summary>
        private void OnNewEnlistmentStarted(Hero lord)
        {
            if (_pendingPromotionTier > 0)
            {
                ModLogger.Info("Promotion", $"Clearing pending promotion tier {_pendingPromotionTier} on new enlistment with {lord?.Name}");
            }
            _pendingPromotionTier = 0;

            // Also clear declined promotions so player gets a fresh start with the new lord
            EscalationManager.Instance?.ClearAllDeclinedPromotions();
        }

        /// <summary>
        /// Resets promotion tracking state when enlistment ends.
        /// Clears pending promotion tier since any in-progress promotion event is now cancelled.
        /// </summary>
        private void OnEnlistmentEnded(string reason)
        {
            if (_pendingPromotionTier > 0)
            {
                ModLogger.Info("Promotion", $"Clearing pending promotion tier {_pendingPromotionTier} on enlistment end ({reason})");
            }
            _pendingPromotionTier = 0;
        }

        public override void SyncData(IDataStore dataStore)
        {
            SaveLoadDiagnostics.SafeSyncData(this, dataStore, () =>
            {
                dataStore.SyncData("_lastPromotionCheck", ref _lastPromotionCheck);
                dataStore.SyncData("_pendingPromotionTier", ref _pendingPromotionTier);
            });
        }

        /// <summary>
        /// Get the proving event ID for a tier transition.
        /// </summary>
        private static string GetProvingEventId(int fromTier, int toTier)
        {
            // Event IDs follow the pattern: promotion_t{from}_t{to}_*
            // T1→T2 has no proving event; uses direct promotion (formation is equipment-based now)
            return fromTier switch
            {
                2 => "promotion_t2_t3_sergeants_test",
                3 => "promotion_t3_t4_crisis_of_command",
                4 => "promotion_t4_t5_veterans_vote",
                5 => "promotion_t5_t6_lord_audience",
                6 => "promotion_t6_t7_commanders_commission",
                _ => $"promotion_t{fromTier}_t{toTier}" // Fallback pattern
            };
        }

        /// <summary>
        /// Clear the pending promotion flag when the event completes.
        /// </summary>
        public void ClearPendingPromotion()
        {
            _pendingPromotionTier = 0;
        }

        /// <summary>
        /// Fallback direct promotion when proving events are unavailable.
        /// Ensures promotion still works even if event system is disabled or events missing.
        /// </summary>
        private void FallbackDirectPromotion(int targetTier, EnlistmentBehavior enlistment)
        {
            ModLogger.Info("Promotion", $"Processing direct promotion to T{targetTier}");
            _pendingPromotionTier = 0;

            // SetTier handles retinue grant for T7/T8/T9 promotions
            enlistment.SetTier(targetTier);
            QuartermasterManager.Instance?.UpdateNewlyUnlockedItems();
            TriggerPromotionNotification(targetTier);
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            ModLogger.Info("Promotion", "Promotion system initialized");
        }

        /// <summary>
        /// Check if player meets all requirements for promotion to the next tier.
        /// Returns a tuple of (canPromote, failureReasons).
        /// </summary>
        public (bool CanPromote, List<string> FailureReasons) CanPromote()
        {
            var reasons = new List<string>();
            var enlistment = EnlistmentBehavior.Instance;

            if (enlistment?.IsEnlisted != true)
            {
                reasons.Add("Not enlisted");
                return (false, reasons);
            }

            var currentTier = enlistment.EnlistmentTier;
            var maxTier = Mod.Core.Config.ConfigurationManager.GetMaxTier();

            if (currentTier >= maxTier)
            {
                reasons.Add("Already at maximum tier");
                return (false, reasons);
            }

            var targetTier = currentTier + 1;
            var req = PromotionRequirements.GetForTier(targetTier);
            var escalation = EscalationManager.Instance;

            // XP thresholds are owned by progression_config.json (single source of truth).
            var tierXp = Mod.Core.Config.ConfigurationManager.GetTierXpRequirements();
            var requiredXp = currentTier < tierXp.Length ? tierXp[currentTier] : tierXp[tierXp.Length - 1];

            // Check XP threshold
            if (enlistment.EnlistmentXP < requiredXp)
            {
                reasons.Add($"XP: {enlistment.EnlistmentXP}/{requiredXp}");
            }

            // Check days in rank
            if (enlistment.DaysInRank < req.DaysInRank)
            {
                reasons.Add($"Days in rank: {enlistment.DaysInRank}/{req.DaysInRank}");
            }

            // Check battles survived
            if (enlistment.BattlesSurvived < req.BattlesRequired)
            {
                reasons.Add($"Battles: {enlistment.BattlesSurvived}/{req.BattlesRequired}");
            }

            // Check scrutiny (escalation system - merged discipline into this 0-100 track)
            if (escalation?.IsEnabled() == true)
            {
                var scrutiny = escalation.State?.Scrutiny ?? 0;
                if (scrutiny >= req.MaxScrutiny)
                {
                    reasons.Add($"Scrutiny too high: {scrutiny} (max: {req.MaxScrutiny - 1})");
                }
            }

            // Check leader relation
            if (enlistment.EnlistedLord != null)
            {
                var relation = enlistment.EnlistedLord.GetRelationWithPlayer();
                if (relation < req.MinLeaderRelation)
                {
                    reasons.Add($"Leader relation: {relation}/{req.MinLeaderRelation}");
                }
            }

            var canPromote = reasons.Count == 0;

            if (!canPromote)
            {
                ModLogger.Debug("Promotion", $"Cannot promote to T{targetTier}: {string.Join(", ", reasons)}");
            }

            return (canPromote, reasons);
        }

        /// <summary>
        /// Get promotion progress as a percentage (0-100).
        /// Useful for UI display.
        /// </summary>
        public int GetPromotionProgress()
        {
            var enlistment = EnlistmentBehavior.Instance;
            var maxTier = Mod.Core.Config.ConfigurationManager.GetMaxTier();

            if (enlistment?.IsEnlisted != true || enlistment.EnlistmentTier >= maxTier)
            {
                return 100;
            }

            var req = PromotionRequirements.GetForTier(enlistment.EnlistmentTier + 1);
            var tierXp = Mod.Core.Config.ConfigurationManager.GetTierXpRequirements();
            var requiredXp = enlistment.EnlistmentTier < tierXp.Length ? tierXp[enlistment.EnlistmentTier] : tierXp[tierXp.Length - 1];

            // Calculate progress for each requirement
            var xpProgress = Math.Min(100, enlistment.EnlistmentXP * 100 / Math.Max(1, requiredXp));
            var daysProgress = Math.Min(100, enlistment.DaysInRank * 100 / Math.Max(1, req.DaysInRank));
            var battlesProgress = Math.Min(100, enlistment.BattlesSurvived * 100 / Math.Max(1, req.BattlesRequired));

            // Average progress (can weight differently if desired)
            var average = (xpProgress + daysProgress + battlesProgress) / 3;
            return average;
        }

        /// <summary>
        ///     Hourly tick handler that checks for promotion eligibility once per in-game hour.
        ///     This provides responsive promotion detection without checking every frame,
        ///     allowing players to see promotions shortly after reaching XP thresholds.
        /// </summary>
        private void OnHourlyTick()
        {
            // Check every hour for promotion eligibility
            // This provides immediate response when XP thresholds are reached
            _lastPromotionCheck = CampaignTime.Now;
            CheckForPromotion();
        }

        /// <summary>
        ///     Check if player has reached promotion thresholds.
        ///     Check all promotion requirements, including XP, days, events, battles, and reputation.
        /// </summary>
        private void CheckForPromotion()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment?.IsEnlisted != true)
            {
                return;
            }

            try
            {
                var currentTier = enlistment.EnlistmentTier;
                var maxTier = Mod.Core.Config.ConfigurationManager.GetMaxTier();

                // Already at max tier
                if (currentTier >= maxTier)
                {
                    return;
                }

                // Check all promotion requirements
                var (canPromote, failureReasons) = CanPromote();

                if (!canPromote)
                {
                    // Log once per session when close to promotion but blocked
                    var progress = GetPromotionProgress();
                    if (progress >= 75)
                    {
                        ModLogger.LogOnce($"promo_blocked_t{currentTier + 1}", "Promotion",
                            $"Promotion to T{currentTier + 1} blocked ({progress}% progress): {string.Join(", ", failureReasons)}");
                    }
                    return;
                }

                // Player meets all requirements
                var targetTier = currentTier + 1;

                // Check if player has previously declined this promotion (must request via dialog)
                if (EscalationManager.Instance?.HasDeclinedPromotion(targetTier) == true)
                {
                    ModLogger.Debug("Promotion", $"Promotion to T{targetTier} previously declined - must request via dialog");
                    return;
                }

                // Check if we've already triggered this promotion event recently (prevent spam)
                if (_pendingPromotionTier == targetTier)
                {
                    return; // Already pending
                }

                _pendingPromotionTier = targetTier;

                // Try to queue the proving event
                var eventId = GetProvingEventId(currentTier, targetTier);
                var provingEvent = Content.EventCatalog.GetEventById(eventId);

                if (provingEvent != null && Content.EventDeliveryManager.Instance != null)
                {
                    // Queue the proving event popup
                    ModLogger.Info("Promotion", $"Queuing proving event: {eventId} (T{currentTier} to T{targetTier})");
                    Content.EventDeliveryManager.Instance.QueueEvent(provingEvent);
                }
                else
                {
                    // Fallback to direct promotion if event system unavailable
                    ModLogger.Warn("Promotion", $"Proving event '{eventId}' not found or EventDeliveryManager unavailable - using direct promotion");
                    FallbackDirectPromotion(targetTier, enlistment);
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("Promotion", "Error checking for promotion", ex);
            }
        }

        /// <summary>
        /// Public entry point for triggering promotion notifications from other systems.
        /// Called by EventDeliveryManager after a proving event grants promotion.
        /// </summary>
        public void TriggerPromotionNotificationPublic(int newTier)
        {
            TriggerPromotionNotification(newTier);
        }

        /// <summary>
        ///     Trigger promotion notification with immersive roleplay text.
        ///     Each tier has unique narrative describing what the promotion means.
        /// </summary>
        private void TriggerPromotionNotification(int newTier)
        {
            try
            {
                var rankName = GetRankName(newTier);
                var playerName = Hero.MainHero?.Name?.ToString() ?? "Soldier";

                // Get tier-specific localized title and message
                var titleText = GetPromotionTitle(newTier);
                var popupMessage = GetPromotionMessage(newTier);
                popupMessage.SetTextVariable("PLAYER_NAME", playerName);
                popupMessage.SetTextVariable("RANK", rankName);

                // Show short notification in chat with rank variable
                var chatMessage = GetPromotionChatMessage(newTier);
                chatMessage.SetTextVariable("RANK", rankName);
                InformationManager.DisplayMessage(new InformationMessage(chatMessage.ToString(), Colors.Green));

                // Show quartermaster prompt after promotion.
                var qmPrompt = new TextObject("{=promo_qm_prompt}Report to the Quartermaster for your new kit.");
                InformationManager.DisplayMessage(new InformationMessage(qmPrompt.ToString(), Colors.Cyan));

                // Get appropriate button text based on tier
                var buttonText = newTier >= 4
                    ? new TextObject("{=promo_btn_command}To Camp").ToString()
                    : new TextObject("{=promo_btn_understood}Understood").ToString();

                var data = new InquiryData(
                    titleText.ToString(),
                    popupMessage.ToString(),
                    true,
                    false,
                    buttonText,
                    "",
                    () => { },
                    null
                );

                // pauseGameActiveState = false so notifications don't freeze game time
                InformationManager.ShowInquiry(data);

                // Record promotion in Personal Feed for historical review
                var retinueSoldiers = newTier switch
                {
                    7 => 20,
                    8 => 30,
                    9 => 40,
                    _ => 0
                };
                EnlistedNewsBehavior.Instance?.AddPromotionNews(newTier, rankName, retinueSoldiers);

                ModLogger.Info("Promotion", $"Promotion notification triggered for {rankName} (Tier {newTier})");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Promotion", "Error showing promotion notification", ex);
            }
        }

        /// <summary>
        ///     Get tier-specific promotion popup title with culture-specific rank.
        /// </summary>
        private TextObject GetPromotionTitle(int tier)
        {
            var enlistment = EnlistmentBehavior.Instance;
            var rankName = RankHelper.GetRankTitle(tier, enlistment?.EnlistedLord?.Culture?.StringId ?? "mercenary");

            var title = tier switch
            {
                2 => new TextObject("{=promo_title_2}Recognized as {RANK}"),
                3 => new TextObject("{=promo_title_3}Rise to {RANK}"),
                4 => new TextObject("{=promo_title_4}Sworn as {RANK}"),
                5 => new TextObject("{=promo_title_5}Elevated to {RANK}"),
                6 => new TextObject("{=promo_title_6}Welcomed as {RANK}"),
                7 => new TextObject("{=promo_title_7}Appointed as {RANK}"),
                8 => new TextObject("{=promo_title_8}Commissioned as {RANK}"),
                9 => new TextObject("{=promo_title_9}Honored as {RANK}"),
                _ => new TextObject("{=promo_title_default}Promoted to {RANK}")
            };

            title.SetTextVariable("RANK", rankName);
            return title;
        }

        /// <summary>
        ///     Get tier-specific immersive promotion message from localization.
        /// </summary>
        private TextObject GetPromotionMessage(int tier)
        {
            return tier switch
            {
                2 => new TextObject("{=promo_msg_2}"),
                3 => new TextObject("{=promo_msg_3}"),
                4 => new TextObject("{=promo_msg_4}"),
                5 => new TextObject("{=promo_msg_5}"),
                6 => new TextObject("{=promo_msg_6}"),
                _ => new TextObject("{=promo_msg_default}You have been promoted to {RANK}.")
            };
        }

        /// <summary>
        ///     Get tier-specific short chat notification from localization.
        /// </summary>
        private TextObject GetPromotionChatMessage(int tier)
        {
            return tier switch
            {
                2 => new TextObject("{=promo_chat_2}"),
                3 => new TextObject("{=promo_chat_3}"),
                4 => new TextObject("{=promo_chat_4}"),
                5 => new TextObject("{=promo_chat_5}"),
                6 => new TextObject("{=promo_chat_6}"),
                _ => new TextObject("{=promo_chat_default}You have been promoted!")
            };
        }

        /// <summary>
        ///     Get culture-specific rank name for tier.
        /// </summary>
        private string GetRankName(int tier)
        {
            // Use culture-specific ranks from RankHelper
            var enlistment = EnlistmentBehavior.Instance;
            return RankHelper.GetRankTitle(tier, enlistment?.EnlistedLord?.Culture?.StringId ?? "mercenary");
        }
    }
}
