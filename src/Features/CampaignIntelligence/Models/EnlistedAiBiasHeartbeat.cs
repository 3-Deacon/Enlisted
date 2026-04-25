using System.Globalization;
using Enlisted.Mod.Core.Logging;

namespace Enlisted.Features.CampaignIntelligence.Models
{
    /// <summary>
    /// Throttled bias-record emitter. Every Plan 2 wrapper bias call routes
    /// through Record(reason, before, after) so a single ModLogger.Expected
    /// per reason per 60s wall-clock window lands in the session log. Lets
    /// Phase H confirm wrappers fire without flooding the log during routine
    /// play.
    /// </summary>
    internal static class EnlistedAiBiasHeartbeat
    {
        public static void Record(string reason, int before, int after)
        {
            // ModLogger.Expected is already per-key 60s throttled (see
            // CLAUDE.md project convention). "reason" doubles as the key,
            // so each reason logs at most once per wall-clock minute.
            ModLogger.Expected(
                "INTELAI",
                "bias_" + reason,
                "bias applied reason=" + reason + " before=" + before + " after=" + after);
        }

        public static void Record(string reason, float before, float after)
        {
            // Float overload avoids (int) cast loss for fractional values.
            // Uses the same throttle key path as the int overload.
            ModLogger.Expected(
                "INTELAI",
                "bias_" + reason,
                "bias applied reason=" + reason
                + " before=" + before.ToString("F3", CultureInfo.InvariantCulture)
                + " after=" + after.ToString("F3", CultureInfo.InvariantCulture));
        }
    }
}
