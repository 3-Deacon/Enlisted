using System.Collections.Generic;
using Enlisted.Features.CampaignIntelligence;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.CampaignIntelligence.Signals
{
    /// <summary>
    /// Pure projector — reads a Plan 1 snapshot and returns candidate signals.
    /// No side effects, no campaign reads, no statics. Unit-testable.
    /// The emitter behavior picks one signal per tick from this list.
    /// </summary>
    public static class EnlistedCampaignSignalBuilder
    {
        /// <summary>
        /// Projects the snapshot into candidate signals. Returns an empty list
        /// if the snapshot is null or the enlisted-gate isn't satisfied at the
        /// call site (caller's responsibility). The order of the returned list
        /// is NOT prioritized — emitter applies its own picker.
        /// </summary>
        public static List<EnlistedCampaignSignal> Build(EnlistedLordIntelligenceSnapshot snapshot)
        {
            var signals = new List<EnlistedCampaignSignal>();
            if (snapshot == null)
            {
                return signals;
            }

            // Change-driven signals (RecentChangeFlags). These fire on the
            // specific change event and are higher-confidence by default.
            if (snapshot.RecentChanges.HasFlag(RecentChangeFlags.ObjectiveShift))
            {
                signals.Add(new EnlistedCampaignSignal
                {
                    Type = SignalType.OrderShift,
                    Confidence = SignalConfidence.High,
                    ChangeType = SignalChangeType.ObjectiveShift,
                    PoolPrefix = "floor_order_shift_",
                    AsOf = CampaignTime.Now
                });
            }

            if (snapshot.RecentChanges.HasFlag(RecentChangeFlags.SiegeTransition))
            {
                signals.Add(new EnlistedCampaignSignal
                {
                    Type = SignalType.Dispatch,
                    Confidence = SignalConfidence.High,
                    ChangeType = SignalChangeType.SiegeTransition,
                    PoolPrefix = "floor_dispatch_",
                    AsOf = CampaignTime.Now
                });
            }

            if (snapshot.RecentChanges.HasFlag(RecentChangeFlags.ConfirmedScoutInfo))
            {
                signals.Add(new EnlistedCampaignSignal
                {
                    Type = SignalType.ScoutReturn,
                    Confidence = SignalConfidence.High,
                    ChangeType = SignalChangeType.ConfirmedScoutInfo,
                    PoolPrefix = "floor_scout_return_",
                    AsOf = CampaignTime.Now
                });
            }

            if (snapshot.RecentChanges.HasFlag(RecentChangeFlags.NewThreat))
            {
                signals.Add(new EnlistedCampaignSignal
                {
                    Type = SignalType.AftermathNotice,
                    Confidence = SignalConfidence.Medium,
                    ChangeType = SignalChangeType.NewThreat,
                    PoolPrefix = "floor_aftermath_notice_",
                    AsOf = CampaignTime.Now
                });
            }

            if (snapshot.RecentChanges.HasFlag(RecentChangeFlags.DispersalRisk))
            {
                signals.Add(new EnlistedCampaignSignal
                {
                    Type = SignalType.Dispatch,
                    Confidence = SignalConfidence.Medium,
                    ChangeType = SignalChangeType.DispersalRisk,
                    PoolPrefix = "floor_dispatch_",
                    AsOf = CampaignTime.Now
                });
            }

            if (snapshot.RecentChanges.HasFlag(RecentChangeFlags.SupplyBreak))
            {
                signals.Add(new EnlistedCampaignSignal
                {
                    Type = SignalType.QuartermasterWarning,
                    Confidence = SignalConfidence.High,
                    ChangeType = SignalChangeType.SupplyBreak,
                    PoolPrefix = "floor_quartermaster_warning_",
                    AsOf = CampaignTime.Now
                });
            }

            if (snapshot.RecentChanges.HasFlag(RecentChangeFlags.PrisonerUpdate))
            {
                signals.Add(new EnlistedCampaignSignal
                {
                    Type = SignalType.PrisonerReport,
                    Confidence = SignalConfidence.Medium,
                    ChangeType = SignalChangeType.PrisonerUpdate,
                    PoolPrefix = "floor_prisoner_report_",
                    AsOf = CampaignTime.Now
                });
            }

            // State-driven signals (ambient; no change event). Lower-confidence,
            // contextually gated.
            if (snapshot.SupplyPressure >= SupplyPressure.Strained)
            {
                signals.Add(new EnlistedCampaignSignal
                {
                    Type = SignalType.QuartermasterWarning,
                    Confidence = SignalConfidence.Medium,
                    ChangeType = SignalChangeType.None,
                    PoolPrefix = "floor_quartermaster_warning_",
                    AsOf = CampaignTime.Now
                });
            }

            if (snapshot.ArmyStrain >= ArmyStrainLevel.Elevated)
            {
                signals.Add(new EnlistedCampaignSignal
                {
                    Type = SignalType.TensionIncident,
                    Confidence = SignalConfidence.Medium,
                    ChangeType = SignalChangeType.None,
                    PoolPrefix = "floor_tension_incident_",
                    AsOf = CampaignTime.Now
                });
            }

            if (snapshot.FrontPressure >= FrontPressure.High)
            {
                signals.Add(new EnlistedCampaignSignal
                {
                    Type = SignalType.Rumor,
                    Confidence = SignalConfidence.Low,
                    ChangeType = SignalChangeType.None,
                    PoolPrefix = "floor_rumor_",
                    AsOf = CampaignTime.Now
                });
            }

            if (snapshot.RecoveryNeed >= RecoveryNeed.High)
            {
                signals.Add(new EnlistedCampaignSignal
                {
                    Type = SignalType.CampTalk,
                    Confidence = SignalConfidence.Low,
                    ChangeType = SignalChangeType.None,
                    PoolPrefix = "floor_camp_talk_",
                    AsOf = CampaignTime.Now
                });
            }

            // Quiet-stretch fallback — all strain axes calm AND no recent
            // changes → emit a silence-with-implication signal.
            if (signals.Count == 0
                && snapshot.SupplyPressure == SupplyPressure.None
                && snapshot.ArmyStrain == ArmyStrainLevel.None
                && snapshot.FrontPressure == FrontPressure.None
                && snapshot.RecentChanges == RecentChangeFlags.None)
            {
                signals.Add(new EnlistedCampaignSignal
                {
                    Type = SignalType.SilenceWithImplication,
                    Confidence = SignalConfidence.Low,
                    ChangeType = SignalChangeType.None,
                    PoolPrefix = "floor_silence_with_implication_",
                    AsOf = CampaignTime.Now
                });
            }

            return signals;
        }

        /// <summary>
        /// Builds a drift-summary signal for DailyDriftApplicator's flush path.
        /// Returns family/perspective metadata only; the summary body text is
        /// passed separately through <see cref="EnlistedSignalEmitterBehavior.EmitExternalSignal"/>
        /// so it lands in the candidate's RenderedBody (where the player reads it),
        /// not in InferenceOverride (which is a short ≤80-char authored hint, wrong
        /// semantics for an XP summary).
        /// </summary>
        public static EnlistedCampaignSignal BuildDriftSummarySignal(string currentDutyProfile)
        {
            // Marching / scouting / wandering feel like "scout return" summary.
            // Garrisoned / besieging / raiding / escorting feel like "camp talk."
            var isMovementProfile = currentDutyProfile == "marching"
                || currentDutyProfile == "wandering";

            return new EnlistedCampaignSignal
            {
                Type = isMovementProfile ? SignalType.ScoutReturn : SignalType.CampTalk,
                Confidence = SignalConfidence.Medium,
                ChangeType = SignalChangeType.None,
                PoolPrefix = isMovementProfile ? "floor_scout_return_" : "floor_camp_talk_",
                AsOf = CampaignTime.Now
            };
        }
    }
}
