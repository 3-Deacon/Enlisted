using Enlisted.Features.Content;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Activities.Orders
{
    /// <summary>
    ///     Resolves display metadata for the player's active named order.
    ///     Returns null when no named order is active; call sites should treat null
    ///     as "show no-order state."
    /// </summary>
    public static class OrderDisplayHelper
    {
        public sealed class Display
        {
            public string Title { get; set; } = string.Empty;
            public int HoursElapsed { get; set; }
            public int HoursTotal { get; set; }
            public string Intent { get; set; } = string.Empty;
        }

        public static Display GetCurrent()
        {
            var activity = OrderActivity.Instance;
            var state = activity?.ActiveNamedOrder;
            if (state == null) { return null; }

            var storylet = StoryletCatalog.GetById(state.OrderStoryletId);
            var title = storylet?.Title;
            if (string.IsNullOrEmpty(title)) { title = storylet?.Id ?? "Order"; }
            var totalHours = storylet?.Arc?.DurationHours ?? 0;
            var elapsedHours = (int)(CampaignTime.Now - state.StartedAt).ToHours;

            return new Display
            {
                Title = title,
                HoursElapsed = elapsedHours,
                HoursTotal = totalHours,
                Intent = state.Intent ?? string.Empty
            };
        }

        public static bool IsOrderActive() => OrderActivity.Instance?.ActiveNamedOrder != null;

        /// <summary>
        ///     Returns hours elapsed since the active named order started. Returns 0 when no order is active.
        ///     Clamps to 24 if the computed delta is negative or exceeds 30 days — guards against
        ///     clock-skew and uninitialized StartedAt on partial deserialization.
        /// </summary>
        public static double GetHoursSinceStarted()
        {
            var state = OrderActivity.Instance?.ActiveNamedOrder;
            if (state == null) { return 0; }
            var hours = (CampaignTime.Now - state.StartedAt).ToHours;
            if (hours < 0 || hours > 720) { return 24; }
            return hours;
        }
    }
}
