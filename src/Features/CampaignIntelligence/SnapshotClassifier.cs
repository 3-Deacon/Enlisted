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
            if (inputs.NearbyHostileCount > 2)
            {
                return ObjectiveConfidence.Low;
            }
            return ObjectiveConfidence.Medium;
        }

        private static RecentChangeFlags DetectDeltas(
            EnlistedLordIntelligenceSnapshot previous,
            EnlistedLordIntelligenceSnapshot current)
        {
            if (previous == null)
            {
                return RecentChangeFlags.None;
            }
            var flags = RecentChangeFlags.None;
            if (previous.Objective != current.Objective)
            {
                flags |= RecentChangeFlags.ObjectiveShift;
            }
            if (current.FrontPressure > previous.FrontPressure)
            {
                flags |= RecentChangeFlags.NewThreat;
            }
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

        private static FrontPressure ClassifyFrontPressure(IntelligenceInputs inputs)
        {
            int score = 0;
            if (inputs.ThreatenedFriendlySettlementCount > 0)
            {
                score += 2;
            }
            if (inputs.ThreatenedFriendlySettlementCount > 2)
            {
                score += 2;
            }
            if (inputs.NearbyHostileCount >= 2)
            {
                score += 1;
            }
            if (inputs.NearbyHostileCount >= 5)
            {
                score += 1;
            }
            if (inputs.FrontierHeatingUp)
            {
                score += 1;
            }
            if (inputs.RecentWarTransition)
            {
                score += 2;
            }
            if (inputs.NearestHostileStrengthRatio > 1.5f && inputs.NearestHostileDistance < 15f)
            {
                score += 2;
            }

            if (score >= 7)
            {
                return FrontPressure.Critical;
            }
            if (score >= 5)
            {
                return FrontPressure.High;
            }
            if (score >= 3)
            {
                return FrontPressure.Medium;
            }
            if (score >= 1)
            {
                return FrontPressure.Low;
            }
            return FrontPressure.None;
        }

        private static ArmyStrainLevel ClassifyArmyStrain(IntelligenceInputs inputs)
        {
            if (!inputs.InArmy)
            {
                return ArmyStrainLevel.None;
            }

            int score = 0;
            if (inputs.ArmyCohesion < 50f)
            {
                score += 1;
            }
            if (inputs.ArmyCohesion < 25f)
            {
                score += 2;
            }
            if (inputs.FoodDaysRemaining < 3f)
            {
                score += 1;
            }
            if (inputs.FoodDaysRemaining < 1f)
            {
                score += 2;
            }
            if (inputs.WoundedRatio > 0.2f)
            {
                score += 1;
            }
            if (inputs.WoundedRatio > 0.4f)
            {
                score += 2;
            }
            if (inputs.PartySizeRatio < 0.7f)
            {
                score += 1;
            }
            if (inputs.ImportantPrisonerCount > 0)
            {
                score += 1;
            }

            if (score >= 7)
            {
                return ArmyStrainLevel.Breaking;
            }
            if (score >= 5)
            {
                return ArmyStrainLevel.Severe;
            }
            if (score >= 3)
            {
                return ArmyStrainLevel.Elevated;
            }
            if (score >= 1)
            {
                return ArmyStrainLevel.Mild;
            }
            return ArmyStrainLevel.None;
        }

        private static SupplyPressure ClassifySupplyPressure(IntelligenceInputs inputs)
        {
            if (inputs.FoodDaysRemaining < 0.5f)
            {
                return SupplyPressure.Critical;
            }
            if (inputs.FoodDaysRemaining < 2f)
            {
                return SupplyPressure.Strained;
            }
            if (inputs.FoodDaysRemaining < 4f)
            {
                return SupplyPressure.Tightening;
            }
            return SupplyPressure.None;
        }

        private static PursuitViability ClassifyPursuitViability(IntelligenceInputs inputs)
        {
            if (inputs.NearbyHostileCount == 0)
            {
                return PursuitViability.NotViable;
            }
            if (inputs.NearestHostileDistance > 20f)
            {
                return PursuitViability.Marginal;
            }

            bool strongEnough = inputs.NearestHostileStrengthRatio < 1.3f;
            bool closeEnough = inputs.NearestHostileDistance < 10f;
            bool strained = inputs.FoodDaysRemaining < 2f || inputs.WoundedRatio > 0.35f;

            if (strongEnough && closeEnough && !strained)
            {
                return PursuitViability.Strong;
            }
            if (strongEnough && !strained)
            {
                return PursuitViability.Viable;
            }
            if (closeEnough && !strained)
            {
                return PursuitViability.Viable;
            }
            return PursuitViability.Marginal;
        }

        private static EnemyContactRisk ClassifyEnemyContactRisk(IntelligenceInputs inputs)
        {
            if (inputs.InMapEvent)
            {
                return EnemyContactRisk.High;
            }
            if (inputs.NearbyHostileCount == 0)
            {
                return EnemyContactRisk.Low;
            }

            bool outnumbered = inputs.NearestHostileStrengthRatio > 1.3f;
            bool close = inputs.NearestHostileDistance < 8f;

            if (outnumbered && close)
            {
                return EnemyContactRisk.High;
            }
            if (outnumbered || close)
            {
                return EnemyContactRisk.Medium;
            }
            return EnemyContactRisk.Low;
        }

        private static RecoveryNeed ClassifyRecoveryNeed(IntelligenceInputs inputs)
        {
            int score = 0;
            if (inputs.WoundedRatio > 0.15f)
            {
                score += 1;
            }
            if (inputs.WoundedRatio > 0.3f)
            {
                score += 2;
            }
            if (inputs.PartySizeRatio < 0.75f)
            {
                score += 1;
            }
            if (inputs.FoodDaysRemaining < 2f)
            {
                score += 1;
            }
            if (inputs.LordIsWounded)
            {
                score += 1;
            }

            if (score >= 4)
            {
                return RecoveryNeed.High;
            }
            if (score >= 3)
            {
                return RecoveryNeed.Medium;
            }
            if (score >= 1)
            {
                return RecoveryNeed.Low;
            }
            return RecoveryNeed.None;
        }

        private static PrisonerStakes ClassifyPrisonerStakes(IntelligenceInputs inputs)
        {
            if (inputs.ImportantPrisonerCount >= 3)
            {
                return PrisonerStakes.High;
            }
            if (inputs.ImportantPrisonerCount == 2)
            {
                return PrisonerStakes.Medium;
            }
            if (inputs.ImportantPrisonerCount == 1)
            {
                return PrisonerStakes.Low;
            }
            return PrisonerStakes.None;
        }

        private static PlayerTrustWindow ClassifyPlayerTrustWindow(IntelligenceInputs inputs)
        {
            int tier = inputs.PlayerTier;
            int relation = inputs.PlayerRelationWithLord;

            if (tier >= 7 && relation >= 30)
            {
                return PlayerTrustWindow.Broad;
            }
            if (tier >= 5 && relation >= 10)
            {
                return PlayerTrustWindow.Moderate;
            }
            if (tier >= 3 || relation >= 0)
            {
                return PlayerTrustWindow.Narrow;
            }
            return PlayerTrustWindow.None;
        }

        private static InformationConfidence ClassifyInformationConfidence(IntelligenceInputs inputs)
        {
            int score = 0;
            if (inputs.HasFreshTracks)
            {
                score++;
            }
            if (inputs.HasEnemyBuildupEvidence)
            {
                score++;
            }
            if (inputs.HasGarrisonObservation)
            {
                score++;
            }
            if (inputs.PlayerProximate && inputs.PlayerTier >= 5)
            {
                score++;
            }
            // Recent war/peace transitions reduce confidence — the lord's prior objective may be stale.
            if (inputs.RecentWarTransition || inputs.RecentPeaceTransition)
            {
                score--;
            }

            if (score >= 3)
            {
                return InformationConfidence.High;
            }
            if (score >= 1)
            {
                return InformationConfidence.Medium;
            }
            return InformationConfidence.Low;
        }
    }
}
