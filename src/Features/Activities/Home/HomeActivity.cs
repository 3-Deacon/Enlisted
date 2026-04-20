using System;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.SaveSystem;

namespace Enlisted.Features.Activities.Home
{
    /// <summary>
    /// Concrete Activity for "lord's party parked at a friendly settlement."
    /// Four phases: arrive (auto, 1h), settle (auto, 2h fire interval, 4h window),
    /// evening (player_choice, once per in-game night), break_camp (auto, fires on departure beat).
    /// Registered in EnlistedSaveDefiner at offset 45 (Task 7).
    /// </summary>
    public sealed class HomeActivity : Activity
    {
        public const string TypeIdConst = "home_activity";

        [SaveableProperty(1)] public string StartedAtSettlementId { get; set; } = string.Empty;
        [SaveableProperty(2)] public CampaignTime PhaseStartedAt { get; set; } = CampaignTime.Zero;
        [SaveableProperty(3)] public bool DigestReleased { get; set; }
        [SaveableProperty(4)] public bool EveningFired { get; set; }

        public override void OnStart(ActivityContext ctx)
        {
            TypeId = TypeIdConst;
            StartedAtSettlementId = MobileParty.MainParty?.CurrentSettlement?.StringId ?? string.Empty;
            CurrentPhaseIndex = 0;
            PhaseStartedAt = CampaignTime.Now;
            // Intent was set by HomeActivityStarter before calling Start(...). Do not overwrite.
        }

        public override void OnPhaseEnter(Phase phase)
        {
            if (phase == null) { return; }
            PhaseStartedAt = CampaignTime.Now;
            LastAutoFireHour = -1;
            if (phase.Id == "arrive" && !DigestReleased)
            {
                // Emits a digest-release signal for StoryDirector; the API is pluggable and
                // currently absent, so this logs the intent and continues.
                ModLogger.Expected("HOME", "digest_api_missing",
                    "StoryDirector digest-release API absent; skipping digest release");
                DigestReleased = true;
            }
            if (phase.Id == "evening")
            {
                EveningFired = false;
            }
        }

        public override void OnPhaseExit(Phase phase) { }

        public override void Tick(bool hourly)
        {
            // Auto-phase advancement is handled by ActivityRuntime.TryAdvancePhase.
            // PlayerChoice phases wait for player input; no per-tick work needed here.
        }

        public override void OnPlayerChoice(string optionId, ActivityContext ctx)
        {
            if (string.IsNullOrEmpty(optionId)) { return; }
            Intent = optionId;
            EveningFired = true;
        }

        public override void OnBeat(string beatId, ActivityContext ctx)
        {
            if (string.Equals(beatId, "departure_imminent", StringComparison.OrdinalIgnoreCase))
            {
                // Skip directly to break_camp phase regardless of current phase position.
                var phases = Phases;
                if (phases == null) { return; }
                var breakCampIndex = phases.FindIndex(p => p.Id == "break_camp");
                if (breakCampIndex >= 0 && breakCampIndex != CurrentPhaseIndex)
                {
                    CurrentPhaseIndex = breakCampIndex - 1;  // AdvancePhase increments before entering
                    ActivityRuntime.Instance?.ResolvePlayerChoice(this);
                }
            }
        }

        public override void Finish(ActivityEndReason reason) { }
    }
}
