using System;
using System.Collections.Generic;
using System.Linq;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Enlisted.Features.Retinue.Core
{
    /// <summary>
    /// Handles automatic granting of raw recruits when player reaches commander tiers (T7-T9).
    /// Part of Retinue System V2.0.
    ///
    /// Grant amounts:
    /// - T6 to T7: 20 soldiers (initial grant)
    /// - T7 to T8: 10 soldiers (expansion)
    /// - T8 to T9: 10 soldiers (final expansion)
    ///
    /// Recruits match player's formation type and enlisted lord's culture.
    /// </summary>
    public static class RetinueRecruitmentGrant
    {
        private const string LogCategory = "RecruitGrant";

        /// <summary>
        /// Grants recruits when player promotes to commander tier.
        /// Called from PromotionBehavior after promotion completes.
        /// </summary>
        /// <param name="newTier">New tier after promotion</param>
        /// <param name="previousTier">Tier before promotion</param>
        public static void GrantCommanderRetinue(int newTier, int previousTier)
        {
            try
            {
                // Only grant on promotion to T7/T8/T9
                if (newTier < RetinueManager.CommanderTier1 || previousTier >= newTier)
                {
                    ModLogger.Debug(LogCategory,
                        $"No grant: newTier={newTier}, previousTier={previousTier}");
                    return;
                }

                var count = CalculateGrantCount(newTier, previousTier);
                if (count <= 0)
                {
                    ModLogger.Debug(LogCategory, "No soldiers to grant");
                    return;
                }

                // First time reaching T7: show formation selection dialog
                // newTier >= CommanderTier1 is guaranteed true here due to the check at line 40
                if (previousTier < RetinueManager.CommanderTier1)
                {
                    // Check if baggage is delayed - if so, defer formation selection
                    var baggageManager = Logistics.BaggageTrainManager.Instance;
                    if (baggageManager != null && baggageManager.IsDelayed())
                    {
                        var daysRemaining = baggageManager.GetBaggageDelayDaysRemaining();
                        ModLogger.Info(LogCategory,
                            $"First T7 promotion - baggage delayed ({daysRemaining} days), deferring formation selection");

                        // Show notification that formation selection is deferred
                        var message = new TextObject(
                            "{=ret_formation_deferred}You've been promoted to Commander, but the supply wagons are delayed. Formation selection will be available once they arrive.")
                            .ToString();
                        InformationManager.DisplayMessage(new InformationMessage(message));

                        // For now, default to Infantry temporarily
                        // TODO: Add proper queueing system to re-prompt when baggage arrives
                        var defaultFormation = FormationClass.Infantry;
                        var lordCulture = GetEnlistedLordCulture();
                        GrantRawRecruits(count, defaultFormation, lordCulture);
                        return;
                    }

                    ModLogger.Info(LogCategory, "First T7 promotion - showing formation selection dialog");
                    ShowFormationSelectionDialog(count);
                    return;
                }

                // T8/T9 expansions: use existing formation type
                var formation = GetPlayerFormationType();
                var culture = GetEnlistedLordCulture();

                ModLogger.Info(LogCategory,
                    $"Granting {count} recruits (tier={newTier}, formation={formation}, culture={culture?.StringId ?? "null"})");

                GrantRawRecruits(count, formation, culture);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("RecruitGrant",
                    $"Failed to grant commander retinue: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Calculates how many recruits to grant based on tier transition.
        /// T6 to T7: 20 (initial grant)
        /// T7 to T8: 10 (expansion)
        /// T8 to T9: 10 (final expansion)
        /// </summary>
        public static int CalculateGrantCount(int newTier, int previousTier)
        {
            // Initial grant at T7
            if (newTier >= RetinueManager.CommanderTier1 && previousTier < RetinueManager.CommanderTier1)
            {
                return RetinueManager.CommanderCapacity1; // 15
            }

            // Expansion grants
            if (newTier >= RetinueManager.CommanderTier2 && previousTier < RetinueManager.CommanderTier2)
            {
                return RetinueManager.CommanderCapacity2 - RetinueManager.CommanderCapacity1; // 10
            }

            if (newTier >= RetinueManager.CommanderTier3 && previousTier < RetinueManager.CommanderTier3)
            {
                return RetinueManager.CommanderCapacity3 - RetinueManager.CommanderCapacity2; // 10
            }

            return 0;
        }

        /// <summary>
        /// Grants raw recruits to player's party.
        /// Handles party size limits and capacity checks.
        /// </summary>
        private static void GrantRawRecruits(int count, FormationClass formation, CultureObject culture)
        {
            var recruitTroop = FindCultureRecruit(culture, formation);
            if (recruitTroop == null)
            {
                ModLogger.Surfaced("RECRUITGRANT", "Could not find recruit troop for culture and formation", null,
                    ctx: LogCtx.Of("Culture", culture?.StringId, "Formation", formation.ToString()));
                ShowRecruitGrantFailedMessage(count);
                return;
            }

            // Check party size limit
            var mobileParty = MobileParty.MainParty;
            if (mobileParty == null)
            {
                ModLogger.Caught("RecruitGrant", "Player party is null", null);
                return;
            }

            var partyBase = mobileParty.Party;
            var partySpace = partyBase.PartySizeLimit - partyBase.NumberOfAllMembers;
            if (partySpace < count)
            {
                ModLogger.Warn(LogCategory,
                    $"Party size limit: requested {count}, space available {partySpace}");
                count = Math.Max(0, partySpace);
            }

            if (count <= 0)
            {
                ModLogger.Warn(LogCategory, "No party space for recruits");
                ShowPartyFullMessage();
                return;
            }

            // Add recruits to party roster
            _ = mobileParty.MemberRoster.AddToCounts(recruitTroop, count);

            // Update retinue state tracking
            var manager = RetinueManager.Instance;
            if (manager != null)
            {
                _ = manager.State.UpdateTroopCount(recruitTroop.StringId, count);
                manager.State.SelectedTypeId = GetFormationTypeId(formation);
            }

            ModLogger.Info(LogCategory,
                $"Granted {count}x {recruitTroop.Name} (ID: {recruitTroop.StringId})");

            ShowRecruitGrantNotification(count, recruitTroop.Name);
        }

        /// <summary>
        /// Finds the appropriate raw recruit troop for given culture and formation.
        /// Returns lowest-tier troop matching formation class.
        /// </summary>
        public static CharacterObject FindCultureRecruit(CultureObject culture, FormationClass formation)
        {
            if (culture?.BasicTroop == null)
            {
                ModLogger.Warn(LogCategory, "No culture or basic troop - using fallback");
                return FindFallbackRecruit(formation);
            }

            // Get all troops from the culture's basic troop tree
            var allTroops = Helpers.CharacterHelper.GetTroopTree(culture.BasicTroop, 1, 3).ToList();

            // Find lowest-level troop matching formation
            var candidates = allTroops
                .Where(t => t != null && IsValidRecruit(t, culture.StringId, formation))
                .OrderBy(t => t.Level)
                .ThenBy(t => t.Tier)
                .ToList();

            var recruit = candidates.FirstOrDefault();

            if (recruit != null)
            {
                ModLogger.Debug(LogCategory,
                    $"Found recruit: {recruit.Name} (Level {recruit.Level}, Tier {recruit.Tier})");
            }
            else
            {
                ModLogger.Warn(LogCategory,
                    $"No recruit found for culture={culture.StringId}, formation={formation}");

                // Fallback: try to find any basic troop of right formation
                recruit = FindFallbackRecruit(formation);
            }

            return recruit;
        }

        /// <summary>
        /// Validates if a troop is a valid recruit for granting.
        /// </summary>
        private static bool IsValidRecruit(CharacterObject troop, string cultureId, FormationClass formation)
        {
            // Must match culture
            if (!string.Equals(troop.Culture?.StringId, cultureId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Must be basic troop (not hero, not noble)
            if (!troop.IsBasicTroop || troop.IsHero)
            {
                return false;
            }

            // Must match formation
            if (troop.DefaultFormationClass != formation)
            {
                return false;
            }

            // Must be low level (raw recruit - level 10 or below)
            if (troop.Level > 15)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Fallback recruit finder if culture-specific search fails.
        /// Finds any basic low-level troop matching formation.
        /// </summary>
        private static CharacterObject FindFallbackRecruit(FormationClass formation)
        {
            var fallback = CharacterObject.All
                .Where(t => t is { IsBasicTroop: true, IsHero: false })
                .Where(t => t.DefaultFormationClass == formation)
                .Where(t => t.Level <= 10)
                .Where(t => t.UpgradeTargets is { Length: > 0 })
                .OrderBy(t => t.Level)
                .FirstOrDefault();

            if (fallback != null)
            {
                ModLogger.Warn(LogCategory,
                    $"Using fallback recruit: {fallback.Name} (Culture: {fallback.Culture?.StringId})");
            }

            return fallback;
        }

        /// <summary>
        /// Gets player's formation type from retinue state.
        /// Returns Infantry as fallback if no selection has been made.
        /// </summary>
        private static FormationClass GetPlayerFormationType()
        {
            var manager = RetinueManager.Instance;
            if (manager?.State?.SelectedTypeId != null)
            {
                return RetinueManager.GetFormationClass(manager.State.SelectedTypeId);
            }

            // Fallback to Infantry if no selection stored
            ModLogger.Debug(LogCategory, "No formation type selected, defaulting to Infantry");
            return FormationClass.Infantry;
        }

        /// <summary>
        /// Gets enlisted lord's culture.
        /// </summary>
        private static CultureObject GetEnlistedLordCulture()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment?.CurrentLord?.Culture != null)
            {
                return enlistment.CurrentLord.Culture;
            }

            // Fallback to empire if no lord culture
            ModLogger.Warn(LogCategory, "No lord culture found, using player culture fallback");
            return Hero.MainHero?.Culture;
        }

        /// <summary>
        /// Gets the type ID string for a formation class.
        /// </summary>
        private static string GetFormationTypeId(FormationClass formation)
        {
            return formation switch
            {
                FormationClass.Infantry => "infantry",
                FormationClass.Ranged => "archers",
                FormationClass.Cavalry => "cavalry",
                FormationClass.HorseArcher => "horse_archers",
                _ => "infantry"
            };
        }

        /// <summary>
        /// Shows the formation selection dialog when player first reaches T7.
        /// Presents four formation options (Infantry, Archers, Cavalry, Horse Archers).
        /// Horse Archers only available for cultures that support them.
        /// </summary>
        /// <param name="recruitCount">Number of recruits to grant after selection</param>
        private static void ShowFormationSelectionDialog(int recruitCount)
        {
            var culture = GetEnlistedLordCulture();
            if (culture == null)
            {
                ModLogger.Caught("RecruitGrant", "Cannot show formation dialog: no culture", null);
                // Fallback to infantry
                GrantRawRecruits(recruitCount, FormationClass.Infantry, Hero.MainHero?.Culture);
                return;
            }

            var title = new TextObject("{=ret_sel_title}Choose Your Retinue");
            var prompt = new TextObject("{=ret_sel_prompt}What type of soldiers will you command?");

            // Build formation options
            var options = new List<InquiryElement>();

            // Infantry - always available
            options.Add(new InquiryElement(
                "infantry",
                new TextObject("{=ret_sel_infantry}Infantry").ToString(),
                null,
                true,
                new TextObject("{=ret_sel_infantry_desc}Foot soldiers with sword and shield").ToString()
            ));

            // Archers - always available
            options.Add(new InquiryElement(
                "archers",
                new TextObject("{=ret_sel_archers}Archers").ToString(),
                null,
                true,
                new TextObject("{=ret_sel_archers_desc}Skilled bowmen for ranged support").ToString()
            ));

            // Cavalry - always available
            options.Add(new InquiryElement(
                "cavalry",
                new TextObject("{=ret_sel_cavalry}Cavalry").ToString(),
                null,
                true,
                new TextObject("{=ret_sel_cavalry_desc}Mounted lancers, swift and deadly").ToString()
            ));

            // Horse Archers - culture restricted
            if (RetinueManager.IsSoldierTypeAvailable("horse_archers", culture))
            {
                options.Add(new InquiryElement(
                    "horse_archers",
                    new TextObject("{=ret_sel_horse_archers}Horse Archers").ToString(),
                    null,
                    true,
                    new TextObject("{=ret_sel_horse_archers_desc}Mounted bowmen of the steppe").ToString()
                ));
            }

            // Show multi-selection dialog (though we only allow one selection)
            MBInformationManager.ShowMultiSelectionInquiry(
                new MultiSelectionInquiryData(
                    title.ToString(),
                    prompt.ToString(),
                    options,
                    false, // isExitShown: false (player must choose)
                    1, // maxSelectableOptionCount
                    1, // minSelectableOptionCount
                    new TextObject("{=ret_sel_confirm}Confirm").ToString(),
                    string.Empty, // no cancel button
                    selectedElements =>
                    {
                        // Player selected a formation type
                        if (selectedElements == null || selectedElements.Count == 0)
                        {
                            ModLogger.Warn(LogCategory, "No formation selected, defaulting to Infantry");
                            OnFormationSelected("infantry", recruitCount);
                            return;
                        }

                        var selectedTypeId = selectedElements[0].Identifier as string;
                        OnFormationSelected(selectedTypeId, recruitCount);
                    },
                    null // no cancel action since isExitShown is false
                ));

            ModLogger.Debug(LogCategory,
                $"Formation selection dialog shown with {options.Count} options (culture: {culture.StringId})");
        }

        /// <summary>
        /// Called when player selects a formation type from the dialog.
        /// Stores selection and grants recruits of that type.
        /// </summary>
        /// <param name="typeId">Selected formation type ID</param>
        /// <param name="recruitCount">Number of recruits to grant</param>
        private static void OnFormationSelected(string typeId, int recruitCount)
        {
            var formation = RetinueManager.GetFormationClass(typeId);
            var culture = GetEnlistedLordCulture();

            ModLogger.Info(LogCategory,
                $"Formation selected: {typeId} -> {formation}, granting {recruitCount} recruits");

            // Grant recruits
            GrantRawRecruits(recruitCount, formation, culture);

            // Show confirmation message
            var formationName = typeId switch
            {
                "infantry" => new TextObject("{=ret_sel_infantry}Infantry"),
                "archers" => new TextObject("{=ret_sel_archers}Archers"),
                "cavalry" => new TextObject("{=ret_sel_cavalry}Cavalry"),
                "horse_archers" => new TextObject("{=ret_sel_horse_archers}Horse Archers"),
                _ => new TextObject("{=ret_sel_infantry}Infantry")
            };

            var confirmMsg = new TextObject("{=ret_sel_confirmed}You have chosen to command {FORMATION}. Your recruits will reflect this choice.")
                .SetTextVariable("FORMATION", formationName);

            InformationManager.DisplayMessage(new InformationMessage(confirmMsg.ToString(), Colors.Green));
        }

        #region Notifications

        private static void ShowRecruitGrantNotification(int count, TextObject troopName)
        {
            var msg = new TextObject("{=retinue_grant_success}{COUNT} {TROOP_NAME} have been assigned to your command.")
                .SetTextVariable("COUNT", count)
                .SetTextVariable("TROOP_NAME", troopName);

            InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Green));

            // Also show a brief banner notification
            MBInformationManager.AddQuickInformation(msg,
                announcerCharacter: Hero.MainHero?.CharacterObject);
        }

        private static void ShowRecruitGrantFailedMessage(int count)
        {
            var msg = new TextObject("{=retinue_grant_failed}Failed to assign {COUNT} recruits. Please report this issue.")
                .SetTextVariable("COUNT", count);

            InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Red));
        }

        private static void ShowPartyFullMessage()
        {
            var msg = new TextObject("{=retinue_party_full}Your party is full. Dismiss soldiers or increase party size to receive recruits.");

            InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Yellow));
        }

        #endregion

        #region Helper Methods

        #endregion
    }
}
