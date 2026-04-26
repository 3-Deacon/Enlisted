using System;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Officer
{
    /// <summary>
    /// Orchestrates officer-tier gear application on rank changes. Subscribes to
    /// <see cref="EnlistmentBehavior.OnTierChanged"/> and fans out to the gear helpers
    /// (cape progression, banner flag, patron weapon modifier). Modifier registration
    /// happens at SubModule.OnGameStart via OfficerGearRegistry; this behavior does
    /// only lookup-and-apply.
    /// </summary>
    public sealed class OfficerTrajectoryBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            // -= then += guards against duplicate subscription on save load (Plan 1 substrate
            // lifecycle pattern; static event survives the GC root and re-loads append handlers
            // unless explicitly removed first).
            EnlistmentBehavior.OnTierChanged -= OnTierChanged;
            EnlistmentBehavior.OnTierChanged += OnTierChanged;
        }

        public override void SyncData(IDataStore dataStore)
        {
            // No own state. Gear lives in vanilla Hero.BattleEquipment + FlagStore keys.
        }

        private void OnTierChanged(int previousTier, int newTier)
        {
            if (newTier < 1 || newTier > 9)
            {
                return;
            }

            try
            {
                CapeProgression.ApplyAtTierChange(newTier);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("OFFICER", "tier_changed_cape_apply_failed", ex);
            }

            try
            {
                BannerProvision.ApplyAtTierChange(newTier);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("OFFICER", "tier_changed_banner_apply_failed", ex);
            }

            try
            {
                PatronWeaponModifier.ApplyAtTierChange(newTier);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("OFFICER", "tier_changed_weapon_apply_failed", ex);
            }
        }
    }
}
