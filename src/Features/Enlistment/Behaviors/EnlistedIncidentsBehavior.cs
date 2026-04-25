using System;
using System.Collections.Generic;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Incidents;
using TaleWorlds.Core;

namespace Enlisted.Features.Enlistment.Behaviors
{
    /// <summary>
    /// Registers and triggers custom incidents used by Enlisted.
    /// Handles pay muster incident. Bag check uses the narrative event system instead.
    /// </summary>
    public sealed class EnlistedIncidentsBehavior : CampaignBehaviorBase
    {
        private Incident _payMusterIncident;

        public static EnlistedIncidentsBehavior Instance { get; private set; }

        public EnlistedIncidentsBehavior()
        {
            Instance = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // No state to sync; incidents are registered per session
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            RegisterPayMusterIncident();
        }

        private void RegisterPayMusterIncident()
        {
            try
            {
                _payMusterIncident =
                    Game.Current.ObjectManager.RegisterPresumedObject(new Incident("incident_enlisted_pay_muster"));
                _payMusterIncident.Initialize(
                    "{=enlisted_pay_muster_title}Pay Muster",
                    "{=enlisted_pay_muster_body}The paymaster calls the muster. Step forward to receive your pay.",
                    IncidentsCampaignBehaviour.IncidentTrigger.EnteringTown,
                    IncidentsCampaignBehaviour.IncidentType.TroopSettlementRelation,
                    CampaignTime.Days(365f),
                    _ => true);

                _payMusterIncident.AddOption("{=enlisted_pay_standard}Accept your pay", new List<IncidentEffect>());

                ModLogger.Info("Incident", "Registered pay muster incident.");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Incident", "Failed to register pay muster incident", ex);
            }
        }

        /// <summary>
        /// Triggers the pay muster sequence using the 6-stage GameMenu system.
        /// Replaced legacy popup inquiry with comprehensive muster flow.
        /// </summary>
        public void TriggerPayMusterIncident()
        {
            try
            {
                var musterHandler = MusterMenuHandler.Instance;
                if (musterHandler == null)
                {
                    ModLogger.Expected("Incident", "muster_handler_not_found", "MusterMenuHandler not found, deferring muster");
                    EnlistmentBehavior.Instance?.DeferPayMuster();
                    return;
                }

                // Trigger the 6-stage GameMenu muster sequence
                musterHandler.BeginMusterSequence();
                ModLogger.Info("Incident", "Pay muster triggered via GameMenu system");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Incident", "Error triggering pay muster, deferring", ex);
                EnlistmentBehavior.Instance?.DeferPayMuster();
            }
        }

    }
}

