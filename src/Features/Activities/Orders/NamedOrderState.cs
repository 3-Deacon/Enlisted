using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;

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
        [SaveableProperty(1)] public string OrderStoryletId { get; set; } = string.Empty;
        [SaveableProperty(2)] public CampaignTime StartedAt { get; set; } = CampaignTime.Zero;

        /// <summary>
        /// Arc-relative phase index within the named-order's Phases list.
        /// Distinct from Activity.CurrentPhaseIndex on the enclosing OrderActivity,
        /// which tracks the duty-profile phase.
        /// </summary>
        [SaveableProperty(3)] public int CurrentPhaseIndex { get; set; }
        [SaveableProperty(4)] public string Intent { get; set; } = string.Empty;
        [SaveableProperty(5)] public FormationClass CombatClassAtAccept { get; set; } = FormationClass.Infantry;
        [SaveableProperty(6)] public Dictionary<string, float> AccumulatedOutcomes { get; set; } = new Dictionary<string, float>();
    }
}
