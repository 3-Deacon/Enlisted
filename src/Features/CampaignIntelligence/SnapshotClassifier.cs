using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Enlisted.Features.CampaignIntelligence
{
    /// <summary>
    /// Pure static classifier. Takes IntelligenceInputs plus the previous snapshot
    /// (for delta detection) and populates the output snapshot's enum fields. No
    /// side effects; no TaleWorlds writes.
    /// </summary>
    internal static class SnapshotClassifier
    {
        /// <summary>
        /// Classify a fresh snapshot from live inputs. When the lord is dead or has
        /// no party the output is cleared to defaults. RecentChanges is the OR of
        /// the pending event-driven flags and delta-detection against the previous
        /// snapshot.
        /// </summary>
        public static void Classify(
            ref IntelligenceInputs inputs,
            EnlistedLordIntelligenceSnapshot previous,
            EnlistedLordIntelligenceSnapshot output)
        {
            if (!inputs.LordAlive || inputs.LordParty == null)
            {
                output.Clear();
                return;
            }

            output.Objective = ClassifyObjective(inputs);
            output.Posture = ClassifyPosture(inputs, output.Objective);
            output.ObjectiveConfidence = ClassifyObjectiveConfidence(inputs);
            output.FrontPressure = ClassifyFrontPressure(inputs);
            output.ArmyStrain = ClassifyArmyStrain(inputs);
            output.SupplyPressure = ClassifySupplyPressure(inputs);
            output.PursuitViability = ClassifyPursuitViability(inputs);
            output.EnemyContactRisk = ClassifyEnemyContactRisk(inputs);
            output.RecoveryNeed = ClassifyRecoveryNeed(inputs);
            output.PrisonerStakes = ClassifyPrisonerStakes(inputs);
            output.PlayerTrustWindow = ClassifyPlayerTrustWindow(inputs);
            output.InformationConfidence = ClassifyInformationConfidence(inputs);
            output.RecentChanges = inputs.PendingFlagsAtTickStart | DetectDeltas(previous, output);
        }

        private static ObjectiveType ClassifyObjective(IntelligenceInputs inputs)
        {
            var party = inputs.LordParty;
            if (inputs.BesiegedSettlement != null)
            {
                return ObjectiveType.BesiegeSettlement;
            }
            if (inputs.ThreatenedFriendlySettlementCount > 0 && !inputs.InArmy)
            {
                return ObjectiveType.DefendSettlement;
            }
            if (inputs.ThreatenedFriendlySettlementCount > 0 && inputs.InArmy)
            {
                return ObjectiveType.RelieveSettlement;
            }
            if (inputs.ArmyIsGathering && !inputs.IsArmyLeader)
            {
                return ObjectiveType.GatherArmy;
            }
            if (party.DefaultBehavior == AiBehavior.EngageParty && inputs.NearbyHostileCount > 0)
            {
                return ObjectiveType.Pursue;
            }
            if (inputs.WoundedRatio > 0.3f || inputs.LordIsWounded)
            {
                return ObjectiveType.Recover;
            }
            if (party.ShortTermBehavior == AiBehavior.RaidSettlement
                || party.DefaultBehavior == AiBehavior.RaidSettlement)
            {
                return ObjectiveType.RaidVillage;
            }
            return ObjectiveType.Move;
        }

        private static StrategicPosture ClassifyPosture(IntelligenceInputs inputs, ObjectiveType objective)
        {
            switch (objective)
            {
                case ObjectiveType.BesiegeSettlement: return StrategicPosture.OffensiveSiege;
                case ObjectiveType.DefendSettlement:  return StrategicPosture.FrontierDefense;
                case ObjectiveType.RelieveSettlement: return StrategicPosture.Relief;
                case ObjectiveType.GatherArmy:        return StrategicPosture.ArmyGather;
                case ObjectiveType.Recover:           return StrategicPosture.Recovery;
                case ObjectiveType.RaidVillage:       return StrategicPosture.OpportunisticRaid;
                case ObjectiveType.Pursue:            return StrategicPosture.ShadowingEnemy;
            }
            if (inputs.CurrentSettlement != null && inputs.CurrentSettlement.IsFortification)
            {
                return StrategicPosture.GarrisonHold;
            }
            if (inputs.FoodDaysRemaining < 1.5f && inputs.WoundedRatio > 0.15f)
            {
                return StrategicPosture.Regroup;
            }
            return StrategicPosture.March;
        }

        private static ObjectiveConfidence ClassifyObjectiveConfidence(IntelligenceInputs inputs)
        {
            // Confidence is HIGH when the objective is unambiguous (settlement target
            // present) and the lord is not being reactive to new threats. MEDIUM when
            // threats are freshly detected but there's an objective. LOW in all noisy
            // / reactive cases.
            if (inputs.BesiegedSettlement != null || inputs.NearestThreatenedFriendly != null)
            {
                if ((inputs.PendingFlagsAtTickStart & (RecentChangeFlags.WarDeclared | RecentChangeFlags.MapEventJoined)) != 0)
                {
                    return ObjectiveConfidence.Medium;
                }
                return ObjectiveConfidence.High;
            }
            if (inputs.NearbyHostileCount > 2) return ObjectiveConfidence.Low;
            return ObjectiveConfidence.Medium;
        }

        private static RecentChangeFlags DetectDeltas(
            EnlistedLordIntelligenceSnapshot previous,
            EnlistedLordIntelligenceSnapshot current)
        {
            if (previous == null) return RecentChangeFlags.None;
            var flags = RecentChangeFlags.None;
            if (previous.Objective != current.Objective) flags |= RecentChangeFlags.ObjectiveShift;
            if (current.FrontPressure > previous.FrontPressure) flags |= RecentChangeFlags.NewThreat;
            if ((previous.Objective != ObjectiveType.BesiegeSettlement) != (current.Objective != ObjectiveType.BesiegeSettlement))
            {
                flags |= RecentChangeFlags.SiegeTransition;
            }
            if (current.SupplyPressure >= SupplyPressure.Strained && previous.SupplyPressure < SupplyPressure.Strained)
            {
                flags |= RecentChangeFlags.SupplyBreak;
            }
            return flags;
        }

        // Stubs filled in later tasks.
        private static FrontPressure ClassifyFrontPressure(IntelligenceInputs _) => FrontPressure.None;
        private static ArmyStrainLevel ClassifyArmyStrain(IntelligenceInputs _) => ArmyStrainLevel.None;
        private static SupplyPressure ClassifySupplyPressure(IntelligenceInputs _) => SupplyPressure.None;
        private static PursuitViability ClassifyPursuitViability(IntelligenceInputs _) => PursuitViability.NotViable;
        private static EnemyContactRisk ClassifyEnemyContactRisk(IntelligenceInputs _) => EnemyContactRisk.Low;
        private static RecoveryNeed ClassifyRecoveryNeed(IntelligenceInputs _) => RecoveryNeed.None;
        private static PrisonerStakes ClassifyPrisonerStakes(IntelligenceInputs _) => PrisonerStakes.None;
        private static PlayerTrustWindow ClassifyPlayerTrustWindow(IntelligenceInputs _) => PlayerTrustWindow.None;
        private static InformationConfidence ClassifyInformationConfidence(IntelligenceInputs _) => InformationConfidence.Low;
    }
}
