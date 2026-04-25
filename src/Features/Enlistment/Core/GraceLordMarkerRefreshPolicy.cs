using System;

namespace Enlisted.Features.Enlistment.Core
{
    public enum LordMarkerScope
    {
        None = 0,
        Leave = 1,
        Grace = 2
    }

    public readonly struct GraceLordMarkerSnapshot
    {
        public GraceLordMarkerSnapshot(string anchorKey, double x, double y)
        {
            AnchorKey = anchorKey ?? string.Empty;
            X = x;
            Y = y;
        }

        public string AnchorKey { get; }

        public double X { get; }

        public double Y { get; }
    }

    public static class GraceLordMarkerRefreshPolicy
    {
        public static LordMarkerScope GetScope(bool isOnLeave, bool isInDesertionGracePeriod)
        {
            if (isInDesertionGracePeriod)
            {
                return LordMarkerScope.Grace;
            }

            if (isOnLeave)
            {
                return LordMarkerScope.Leave;
            }

            return LordMarkerScope.None;
        }

        public static bool ShouldRefresh(
            GraceLordMarkerSnapshot? previous,
            GraceLordMarkerSnapshot current,
            double distanceThreshold)
        {
            if (!previous.HasValue)
            {
                return true;
            }

            var prior = previous.Value;
            if (!string.Equals(prior.AnchorKey, current.AnchorKey, StringComparison.Ordinal))
            {
                return true;
            }

            var deltaX = current.X - prior.X;
            var deltaY = current.Y - prior.Y;
            return (deltaX * deltaX) + (deltaY * deltaY) >= distanceThreshold * distanceThreshold;
        }
    }
}
