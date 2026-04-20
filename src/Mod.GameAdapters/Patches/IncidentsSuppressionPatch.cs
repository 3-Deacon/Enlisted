using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    /// Suppresses random "Incidents" (from NavalDLC or similar mechanics) while enlisted.
    /// These incidents often assume the player is a party leader (e.g. "No Time to Mourn", "Sleeping Sentry"),
    /// which is inappropriate for a soldier in an army.
    /// </summary>
    [HarmonyPatch(typeof(IncidentsCampaignBehaviour), "TryInvokeIncident")]
    public class IncidentsSuppressionPatch
    {
        /// <summary>
        /// Prefix method that runs before IncidentsCampaignBehaviour.TryInvokeIncident.
        /// Called by Harmony via reflection.
        /// </summary>
        [HarmonyPrefix]
        public static bool Prefix()
        {
            var enlistment = EnlistmentBehavior.Instance;

            // Check if enlisted
            // We explicitly allow incidents if the player is in a grace period (e.g. after desertion/discharge)
            // even if IsEnlisted were somehow true (though usually IsEnlisted is false during grace period).
            // We also allow incidents if not enlisted at all.
            if (enlistment is { IsEnlisted: true, IsInDesertionGracePeriod: false })
            {
                ModLogger.Debug("Encounter", "Suppressed Incident (TryInvokeIncident) because player is enlisted and not in grace period.");
                return false; // Prevent the incident from triggering
            }

            return true; // Allow normal execution
        }
    }
}
