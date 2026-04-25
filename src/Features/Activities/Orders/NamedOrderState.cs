using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace Enlisted.Features.Activities.Orders
{
    /// <summary>
    /// Persisted state for an active named-order arc. OrderStoryletId is the
    /// reconstruction key used by OrderActivity.ReconstructArcOnLoad to
    /// re-resolve the arc's Phase list after save/load. CombatClassAtAccept
    /// is cached so the arc's mid-arc storylet weighting stays stable if the
    /// player swaps loadout mid-arc.
    /// </summary>
    [Serializable]
    public sealed class NamedOrderState
    {
        public string OrderStoryletId { get; set; } = string.Empty;
        public CampaignTime StartedAt { get; set; } = CampaignTime.Zero;

        /// <summary>
        /// Arc-relative phase index within the named-order's Phases list.
        /// Distinct from Activity.CurrentPhaseIndex on the enclosing OrderActivity,
        /// which tracks the duty-profile phase.
        /// </summary>
        public int CurrentPhaseIndex { get; set; }
        public string Intent { get; set; } = string.Empty;
        public FormationClass CombatClassAtAccept { get; set; } = FormationClass.Infantry;
        public Dictionary<string, float> AccumulatedOutcomes { get; set; } = new Dictionary<string, float>();
    }
}
