using Enlisted.Mod.Core.Logging;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming
namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    /// Fixes vanilla issue where player isn't removed from captor's prison roster when
    /// captor party is inactive (e.g., captor was defeated/captured).
    /// This prevents hero duplication after escaping captivity.
    /// </summary>
    [HarmonyPatch(typeof(PlayerCaptivity), nameof(PlayerCaptivity.EndCaptivity))]
    public static class EndCaptivityCleanupPatch
    {
        /// <summary>
        /// After vanilla EndCaptivity runs, ensure player is removed from all prison rosters.
        /// Vanilla only removes from captor if captor.IsActive, leaving stale refs when captor is inactive.
        /// </summary>
        private static void Postfix()
        {
            try
            {
                // Skip during campaign initialization (character creation, rare edge cases)
                if (Campaign.Current == null)
                {
                    return;
                }

                var playerChar = Hero.MainHero?.CharacterObject;
                if (playerChar == null)
                {
                    return;
                }

                // Log key captivity transition - this helps track prisoner lifecycle
                ModLogger.Info("Captivity", "Player released from captivity - cleaning up prison rosters");

                var removed = 0;

                // Check all mobile parties for stale prison roster entries
                foreach (var party in MobileParty.All)
                {
                    if (party?.PrisonRoster == null)
                    {
                        continue;
                    }

                    var count = party.PrisonRoster.GetTroopCount(playerChar);
                    if (count > 0)
                    {
                        _ = party.PrisonRoster.AddToCounts(playerChar, -count);
                        removed++;
                        ModLogger.Info("CaptivityFix",
                            $"Removed player from stale prison roster: {party.Name?.ToString() ?? party.StringId}");
                    }
                }

                // Also check settlement prison rosters
                foreach (var settlement in Settlement.All)
                {
                    if (settlement?.Party?.PrisonRoster == null)
                    {
                        continue;
                    }

                    var count = settlement.Party.PrisonRoster.GetTroopCount(playerChar);
                    if (count > 0)
                    {
                        _ = settlement.Party.PrisonRoster.AddToCounts(playerChar, -count);
                        removed++;
                        ModLogger.Info("CaptivityFix",
                            $"Removed player from stale settlement prison: {settlement.Name}");
                    }
                }

                if (removed > 0)
                {
                    ModLogger.Info("CaptivityFix",
                        $"Cleaned {removed} stale prison roster entries for player after escape");
                }
            }
            catch (System.Exception ex)
            {
                ModLogger.Caught("CaptivityFix", "Error cleaning prison rosters", ex);
            }
        }
    }
}

