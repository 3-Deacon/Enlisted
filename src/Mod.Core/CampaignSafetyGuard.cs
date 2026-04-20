using System;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Enlisted.Mod.Core
{
    /// <summary>
    /// Provides safe checks for campaign state that won't throw during character creation.
    /// During character creation, accessing Hero.MainHero, MobileParty.MainParty, etc.
    /// can throw exceptions, not just return null. This helper wraps all access in try-catch.
    /// </summary>
    public static class CampaignSafetyGuard
    {
        /// <summary>
        /// Returns true if the campaign is fully initialized and safe for mod operations.
        /// Returns false during character creation, menu screens, or other non-campaign states.
        /// All property accesses are wrapped in try-catch to prevent crashes.
        /// </summary>
        public static bool IsCampaignReady
        {
            get
            {
                try
                {
                    // First check Campaign.Current - this is usually safe
                    if (Campaign.Current == null)
                    {
                        return false;
                    }

                    // Try to access Hero.MainHero - this can throw during character creation
                    try
                    {
                        var hero = Hero.MainHero;
                        if (hero == null)
                        {
                            return false;
                        }
                    }
                    catch
                    {
                        return false;
                    }

                    // Try to access MobileParty.MainParty - can also throw
                    try
                    {
                        var party = MobileParty.MainParty;
                        if (party == null)
                        {
                            return false;
                        }
                    }
                    catch
                    {
                        return false;
                    }

                    // Try to access Clan.PlayerClan
                    try
                    {
                        var clan = Clan.PlayerClan;
                        if (clan == null)
                        {
                            return false;
                        }
                    }
                    catch
                    {
                        return false;
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    // If anything goes wrong, assume campaign is not ready
                    ModLogger.Debug("SafetyGuard", $"Campaign not ready: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// Minimal check - only verifies Campaign.Current exists.
        /// Use when you don't need full hero/party access.
        /// </summary>
        public static bool HasCampaign
        {
            get
            {
                try
                {
                    return Campaign.Current != null;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Safely gets Hero.MainHero, returning null if not available.
        /// </summary>
        public static Hero SafeMainHero
        {
            get
            {
                try
                {
                    if (Campaign.Current == null)
                    {
                        return null;
                    }

                    return Hero.MainHero;
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Safely gets MobileParty.MainParty, returning null if not available.
        /// </summary>
        public static MobileParty SafeMainParty
        {
            get
            {
                try
                {
                    if (Campaign.Current == null)
                    {
                        return null;
                    }

                    return MobileParty.MainParty;
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Safely gets Clan.PlayerClan, returning null if not available.
        /// </summary>
        public static Clan SafePlayerClan
        {
            get
            {
                try
                {
                    if (Campaign.Current == null)
                    {
                        return null;
                    }

                    return Clan.PlayerClan;
                }
                catch
                {
                    return null;
                }
            }
        }
    }
}

