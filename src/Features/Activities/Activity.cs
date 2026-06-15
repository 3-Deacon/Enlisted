using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace Enlisted.Features.Activities
{
    /// <summary>
    /// Abstract stateful ticking activity. Banner-Kings Feast-shape: each subclass (CampActivity,
    /// MusterActivity, OrderActivity, QuartermasterActivity) ships its own OnStart/Tick/OnPhaseEnter/
    /// OnPhaseExit/Finish logic and claims a save-definer offset from 45-60 in its surface spec.
    /// </summary>
    [Serializable]
    public abstract class Activity
    {
        [SaveableProperty(1)] public string Id { get; set; } = string.Empty;                 // instance id
        [SaveableProperty(2)] public string TypeId { get; set; } = string.Empty;             // template id
        [SaveableProperty(3)] public string Intent { get; set; } = string.Empty;
        [SaveableProperty(4)] public int CurrentPhaseIndex { get; set; }
        [SaveableProperty(5)] public List<Hero> Attendees { get; set; } = new List<Hero>();
        [SaveableProperty(6)] public CampaignTime StartedAt { get; set; } = CampaignTime.Zero;
        [SaveableProperty(7)] public CampaignTime EndsAt { get; set; } = CampaignTime.Zero;
        [SaveableProperty(8)] public Dictionary<string, int> PhaseQuality { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// The in-game hour at which the last Auto-phase storylet fired, used to honour
        /// <see cref="Phase.FireIntervalHours"/>. -1 means "never fired in this phase".
        /// Generic: any Auto phase on any Activity subclass can use interval pacing.
        /// Serialized as part of Activity state; survives save-reload.
        /// </summary>
        [SaveableProperty(9)] public int LastAutoFireHour { get; set; } = -1;

        // Non-serialized runtime cache — resolved from ActivityTypeDefinition on load.
        [NonSerialized] private List<Phase> _cachedPhases;
        public List<Phase> Phases
        {
            get => _cachedPhases ?? new List<Phase>();
            set => _cachedPhases = value;
        }

        public Phase CurrentPhase =>
            _cachedPhases != null && CurrentPhaseIndex >= 0 && CurrentPhaseIndex < _cachedPhases.Count
                ? _cachedPhases[CurrentPhaseIndex]
                : null;

        public abstract void OnStart(ActivityContext ctx);
        public abstract void Tick(bool hourly);
        public abstract void OnPhaseEnter(Phase phase);
        public abstract void OnPhaseExit(Phase phase);
        public abstract void Finish(ActivityEndReason reason);

        /// <summary>Called by ActivityRuntime.ResolvePlayerChoice after the player selects
        /// an option in a PlayerChoice-delivery phase. Default no-op.</summary>
        public virtual void OnPlayerChoice(string optionId, ActivityContext ctx) { }

        /// <summary>Called by starter behaviors to signal a campaign beat relevant to this
        /// activity (e.g. "departure_imminent"). Default no-op.</summary>
        public virtual void OnBeat(string beatId, ActivityContext ctx) { }

        /// <summary>Called by ActivityRuntime after load; resolves Phases from the TypeDefinition.</summary>
        public void ResolvePhasesFromType(IReadOnlyDictionary<string, ActivityTypeDefinition> types)
        {
            if (string.IsNullOrEmpty(TypeId)) { return; }
            if (types == null || !types.TryGetValue(TypeId, out var def)) { return; }
            _cachedPhases = def.Phases;
        }
    }
}
