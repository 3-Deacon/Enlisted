using System;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Ceremonies
{
    /// <summary>
    /// Listens to EnlistmentBehavior.OnTierChanged and fires a rank-ceremony
    /// modal at the five retained tier transitions (newTier 2/3/5/7/8). Tiers
    /// 4/6/9 are skipped because PathCrossroadsBehavior already fires Modal
    /// storylets there (architecture brief §6, Plan 3 Lock 1). Choice memory
    /// rides on FlagStore keys 'ceremony_fired_t{N}' (dedup) and
    /// 'ceremony_choice_t{N}_&lt;choice_id&gt;' (one bool per option) — no
    /// dedicated save offset claimed. Single hook covers all 3 T6→T7 promotion
    /// paths (auto proving-event, decline-then-dialog, dialog-request) since
    /// each routes through SetTier(7) which always emits OnTierChanged once.
    /// </summary>
    public sealed class RankCeremonyBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            EnlistmentBehavior.OnTierChanged -= OnTierChanged;
            EnlistmentBehavior.OnTierChanged += OnTierChanged;
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnTierChanged(int previousTier, int newTier)
        {
            try
            {
                if (newTier <= previousTier)
                {
                    return;
                }
                if (newTier < 2 || newTier > 9)
                {
                    return;
                }

                CeremonyProvider.FireCeremonyForTier(newTier);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("CEREMONY", "on_tier_changed_failed", ex);
            }
        }
    }
}
