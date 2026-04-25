using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Ceremonies
{
    /// <summary>
    /// Listens to EnlistmentBehavior.OnTierChanged and (in Plan 3) fires a
    /// rank-ceremony storylet via ModalEventBuilder.FireCeremony. Choice memory
    /// rides on FlagStore keys 'ceremony_fired_t{N}' (dedup) and
    /// 'ceremony_choice_t{N}' (selected option) — no dedicated save offset
    /// claimed. Plan 3 layers the storylet pool, cultural variants, and trait
    /// drift; Plan 1 ships the wired hook with log-only body so the seam is
    /// observable in playtests.
    /// </summary>
    public sealed class RankCeremonyBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            EnlistmentBehavior.OnTierChanged += OnTierChanged;
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnTierChanged(int previousTier, int newTier)
        {
            ModLogger.Expected("CEREMONY", "tier_changed",
                "Plan 3 will fire a ceremony storylet for this transition");
        }
    }
}
