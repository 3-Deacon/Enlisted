using System.Collections.Generic;
using Enlisted.Features.Camp.Models;
using Enlisted.Features.Content.Models;
using Enlisted.Features.Equipment.Managers;
using Enlisted.Features.Logistics;
using Enlisted.Features.Retinue.Data;
using Enlisted.Mod.Core.Util;
using TaleWorlds.SaveSystem;

namespace Enlisted.Mod.Core.SaveSystem
{
    /// <summary>
    /// Registers all custom types used by the Enlisted mod for Bannerlord's save system.
    /// This definer handles custom classes, container definitions, and enum types that
    /// need serialization support beyond what Bannerlord provides by default.
    ///
    /// Each type registration requires a unique save ID (base ID + offset). The base ID
    /// must be unique across all mods. Container definitions handle generic types like
    /// Dictionary and List with specific type parameters.
    /// </summary>
    [UsedImplicitly("Discovered via reflection by TaleWorlds.SaveSystem (SaveableTypeDefiner scan).")]
    public sealed class EnlistedSaveDefiner : SaveableTypeDefiner
    {
        // Base ID for all Enlisted mod save type registrations.
        // Must be unique across all mods to avoid conflicts.
        // Using 735000 range (735000-735999) for Enlisted.
        private const int BaseId = 735000;

        public EnlistedSaveDefiner() : base(BaseId)
        {
        }

        /// <summary>
        /// Registers custom class types for serialization.
        /// Classes must be serializable and have a parameterless constructor.
        /// </summary>
        protected override void DefineClassTypes()
        {
            // Quartermaster inventory state
            AddClassDefinition(typeof(QMInventoryState), 1);

            // Order system types (offsets 10-14) — deleted with legacy OrderManager subsystem
            // retirement. Save loads tolerate orphaned SyncData entries per
            // CampaignBehaviorDataStore.LoadBehaviorData permissive lookup
            // (decompile: TaleWorlds.CampaignSystem/CampaignBehaviorDataStore.cs:86-106).
            // Offsets 10-14 remain reserved; do not reuse without audit.

            // Retinue system types
            AddClassDefinition(typeof(NamedVeteran), 20);
            AddClassDefinition(typeof(RetinueState), 21);
            AddClassDefinition(typeof(LifetimeServiceRecord), 22);
            AddClassDefinition(typeof(FactionServiceRecord), 23);
            AddClassDefinition(typeof(ReservistRecord), 24);

            // Pacing subsystem
            AddClassDefinition(typeof(Features.Content.StoryCandidatePersistent), 30);

            // Storylet backbone (Spec 0)
            AddClassDefinition(typeof(Features.Flags.FlagStore), 43);
            AddClassDefinition(typeof(Features.Qualities.QualityStore), 40);
            AddClassDefinition(typeof(Features.Qualities.QualityValue), 41);

            // Activity subsystem (Spec 0) — concrete subclasses claim offsets 45-60 in surface specs
            AddClassDefinition(typeof(Features.Activities.Activity), 44);

            // Spec 1 (Enlisted Home Surface) — offset 45
            AddClassDefinition(typeof(Features.Activities.Home.HomeActivity), 45);

            // Spec 2 (Orders Surface) — offsets 46-47
            AddClassDefinition(typeof(Features.Activities.Orders.OrderActivity), 46);
            AddClassDefinition(typeof(Features.Activities.Orders.NamedOrderState), 47);

            // Campaign Intelligence backbone (Plan 1 of 5) — offset 48
            AddClassDefinition(typeof(Features.CampaignIntelligence.EnlistedLordIntelligenceSnapshot), 48);

            // Signal Projection (Plan 3 of 5) — offset 49
            AddClassDefinition(typeof(Features.CampaignIntelligence.Signals.SignalEmissionRecord), 49);

            // Duty Opportunities (Plan 4 of 5) — offset 50
            AddClassDefinition(typeof(Features.CampaignIntelligence.Duty.DutyCooldownStore), 50);
        }

        /// <summary>
        /// Registers enum types that need serialization support.
        /// </summary>
        protected override void DefineEnumTypes()
        {
            // Retinue enums
            AddEnumDefinition(typeof(LoyaltyThreshold), 50);
            AddEnumDefinition(typeof(BattleOutcome), 51);

            // Logistics enums
            AddEnumDefinition(typeof(BaggageAccessState), 52);

            // Content Orchestrator enums
            AddEnumDefinition(typeof(DayPhase), 60);
            AddEnumDefinition(typeof(LordSituation), 61);
            AddEnumDefinition(typeof(LifePhase), 62);
            AddEnumDefinition(typeof(ActivityLevel), 63);
            AddEnumDefinition(typeof(WarStance), 64);

            // Camp Life Simulation enums
            AddEnumDefinition(typeof(OpportunityType), 70);
            AddEnumDefinition(typeof(CampMood), 71);

            // Pacing subsystem enums
            AddEnumDefinition(typeof(Features.Content.StoryTier), 80);
            AddEnumDefinition(typeof(Features.Content.StoryBeat), 81);

            // Storylet backbone enums (Spec 0)
            AddEnumDefinition(typeof(Features.Qualities.QualityScope), 82);

            // Activity subsystem enums (Spec 0)
            AddEnumDefinition(typeof(Features.Activities.ActivityEndReason), 83);

            // Spec 2 (Orders Surface) — offset 85. FormationClass is already registered by
            // TaleWorlds.Core.SaveableCoreTypeDefiner at id 2008; re-registering it crashes
            // Module.Initialize with ArgumentException from the shared definition dictionary.
            AddEnumDefinition(typeof(Features.Activities.Orders.DutyProfileId), 85);

            // Campaign Intelligence backbone enums (Plan 1 of 5) — offsets 86-98
            AddEnumDefinition(typeof(Features.CampaignIntelligence.StrategicPosture), 86);
            AddEnumDefinition(typeof(Features.CampaignIntelligence.ObjectiveType), 87);
            AddEnumDefinition(typeof(Features.CampaignIntelligence.ObjectiveConfidence), 88);
            AddEnumDefinition(typeof(Features.CampaignIntelligence.FrontPressure), 89);
            AddEnumDefinition(typeof(Features.CampaignIntelligence.ArmyStrainLevel), 90);
            AddEnumDefinition(typeof(Features.CampaignIntelligence.SupplyPressure), 91);
            AddEnumDefinition(typeof(Features.CampaignIntelligence.InformationConfidence), 92);
            AddEnumDefinition(typeof(Features.CampaignIntelligence.EnemyContactRisk), 93);
            AddEnumDefinition(typeof(Features.CampaignIntelligence.PursuitViability), 94);
            AddEnumDefinition(typeof(Features.CampaignIntelligence.RecoveryNeed), 95);
            AddEnumDefinition(typeof(Features.CampaignIntelligence.PrisonerStakes), 96);
            AddEnumDefinition(typeof(Features.CampaignIntelligence.PlayerTrustWindow), 97);
            AddEnumDefinition(typeof(Features.CampaignIntelligence.RecentChangeFlags), 98);

            // Signal Projection enums (Plan 3 of 5) — offsets 99-103
            AddEnumDefinition(typeof(Features.CampaignIntelligence.Signals.SignalType), 99);
            AddEnumDefinition(typeof(Features.CampaignIntelligence.Signals.SignalConfidence), 100);
            AddEnumDefinition(typeof(Features.CampaignIntelligence.Signals.SignalChangeType), 101);

            // StoryCandidate signal-metadata enums (Plan 3 of 5) — offsets 102-103
            AddEnumDefinition(typeof(Features.Content.SourcePerspective), 102);
            AddEnumDefinition(typeof(Features.Content.SignalRecency), 103);

            // Dispatch routing enums (Agency News Status integration) — offsets 104-106
            AddEnumDefinition(typeof(Features.Interface.Models.DispatchDomain), 104);
            AddEnumDefinition(typeof(Features.Interface.Models.DispatchSourceKind), 105);
            AddEnumDefinition(typeof(Features.Interface.Models.DispatchSurfaceHint), 106);
        }

        /// <summary>
        /// Registers container definitions for generic types with specific type parameters.
        /// Bannerlord's base save system doesn't include all Dictionary/List combinations,
        /// so we must explicitly register the ones we use.
        /// </summary>
        protected override void DefineContainerDefinitions()
        {
            // Dictionary types used by various systems
            ConstructContainerDefinition(typeof(Dictionary<string, int>));    // QMInventoryState.CurrentStock, RetinueState.TroopCounts, OrderOutcome dictionaries
            ConstructContainerDefinition(typeof(Dictionary<string, bool>));   // CompanionAssignmentManager (already in CompanionAssignmentSaveDefiner but included for completeness)
            ConstructContainerDefinition(typeof(Dictionary<string, float>));  // OpportunityHistory.LastPresentedHours

            // List types used by various systems
            ConstructContainerDefinition(typeof(List<string>));               // LifetimeServiceRecord.FactionsServed, Order.Tags
            ConstructContainerDefinition(typeof(List<NamedVeteran>));         // RetinueState.NamedVeterans
            // List<PhaseRecap> container removed with legacy OrderProgressionBehavior.

            // Pacing subsystem containers (Dictionary<string,int> is already registered above)
            ConstructContainerDefinition(typeof(List<Features.Content.StoryCandidatePersistent>));

            // Storylet backbone containers (Spec 0)
            ConstructContainerDefinition(typeof(Dictionary<string, TaleWorlds.CampaignSystem.CampaignTime>));
            ConstructContainerDefinition(typeof(Dictionary<TaleWorlds.ObjectSystem.MBGUID, Dictionary<string, TaleWorlds.CampaignSystem.CampaignTime>>));
            ConstructContainerDefinition(typeof(Dictionary<string, Features.Qualities.QualityValue>));
            ConstructContainerDefinition(typeof(Dictionary<TaleWorlds.ObjectSystem.MBGUID, Dictionary<string, Features.Qualities.QualityValue>>));

            // Activity subsystem containers (Spec 0)
            ConstructContainerDefinition(typeof(List<Features.Activities.Activity>));
            ConstructContainerDefinition(typeof(Dictionary<string, List<string>>));

            // Campaign Intelligence backbone (Plan 1 of 5) — parallel-list encoding for the
            // RecentChangeFlags decay tracker in EnlistedCampaignIntelligenceBehavior.SyncData.
            ConstructContainerDefinition(typeof(List<int>));
            ConstructContainerDefinition(typeof(List<TaleWorlds.CampaignSystem.CampaignTime>));
        }
    }
}

