using System;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Activities.Orders
{
    /// <summary>Wall-clock pre-filter for the orders subsystem's non-modal StoryDirector emissions — enforces a 20-second floor between emits and suppresses entirely when the campaign speed is above 4x.</summary>
    internal static class OrdersNewsFeedThrottle
    {
        private static readonly TimeSpan FLOOR = TimeSpan.FromSeconds(20);
        private static DateTime _lastEmit = DateTime.MinValue;

        public static bool TryClaim()
        {
            try
            {
                var speed = Campaign.Current?.SpeedUpMultiplier ?? 1f;
                if (speed > 4f)
                {
                    return false;
                }

                var now = DateTime.UtcNow;
                if (now - _lastEmit < FLOOR)
                {
                    return false;
                }
                _lastEmit = now;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
