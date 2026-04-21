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
    }
}
