using System.Collections.Generic;
using Enlisted.Features.Camp.Models;
using Enlisted.Features.Content.Models;
using Enlisted.Features.Equipment.Managers;
using Enlisted.Features.Logistics;
using Enlisted.Features.Orders.Models;
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

            // Order system types
            AddClassDefinition(typeof(Order), 10);
            AddClassDefinition(typeof(OrderRequirement), 11);
            AddClassDefinition(typeof(OrderConsequence), 12);
            AddClassDefinition(typeof(OrderOutcome), 13);
            AddClassDefinition(typeof(PhaseRecap), 14);

            // Retinue system types
            AddClassDefinition(typeof(NamedVeteran), 20);
            AddClassDefinition(typeof(RetinueState), 21);
            AddClassDefinition(typeof(LifetimeServiceRecord), 22);
            AddClassDefinition(typeof(FactionServiceRecord), 23);
            AddClassDefinition(typeof(ReservistRecord), 24);

            // Pacing subsystem
            AddClassDefinition(typeof(Enlisted.Features.Content.StoryCandidatePersistent), 30);

            // Storylet backbone (Spec 0)
            AddClassDefinition(typeof(Enlisted.Features.Flags.FlagStore), 43);
            AddClassDefinition(typeof(Enlisted.Features.Qualities.QualityStore), 40);
            AddClassDefinition(typeof(Enlisted.Features.Qualities.QualityValue), 41);
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
            AddEnumDefinition(typeof(Enlisted.Features.Content.StoryTier), 80);
            AddEnumDefinition(typeof(Enlisted.Features.Content.StoryBeat), 81);

            // Storylet backbone enums (Spec 0)
            AddEnumDefinition(typeof(Enlisted.Features.Qualities.QualityScope), 82);
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
            ConstructContainerDefinition(typeof(List<PhaseRecap>));           // OrderProgressionBehavior._phaseRecaps

            // Pacing subsystem containers (Dictionary<string,int> is already registered above)
            ConstructContainerDefinition(typeof(System.Collections.Generic.List<Enlisted.Features.Content.StoryCandidatePersistent>));

            // DispatchItem.Beats — typed beat set replacing substring matches on HeadlineKey.
            // See docs/superpowers/specs/2026-04-19-event-meaning-design.md §5.3.
            ConstructContainerDefinition(typeof(HashSet<Enlisted.Features.Content.StoryBeat>));

            // Storylet backbone containers (Spec 0)
            ConstructContainerDefinition(typeof(System.Collections.Generic.Dictionary<string, TaleWorlds.CampaignSystem.CampaignTime>));
            ConstructContainerDefinition(typeof(System.Collections.Generic.Dictionary<TaleWorlds.ObjectSystem.MBGUID, System.Collections.Generic.Dictionary<string, TaleWorlds.CampaignSystem.CampaignTime>>));
            ConstructContainerDefinition(typeof(System.Collections.Generic.Dictionary<string, Enlisted.Features.Qualities.QualityValue>));
            ConstructContainerDefinition(typeof(System.Collections.Generic.Dictionary<TaleWorlds.ObjectSystem.MBGUID, System.Collections.Generic.Dictionary<string, Enlisted.Features.Qualities.QualityValue>>));
        }
    }
}

