using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Compresses runs of same-category StoryCandidates inside a 48-hour rolling window
    /// into a single representative. Prevents "3 battles in the Sturgian war" from showing
    /// as three separate accordion entries — the Director calls this before surfacing
    /// pertinent/log tiers for release.
    /// </summary>
    public static class StoryAggregator
    {
        private const float WindowHours = 48f;

        public static List<StoryCandidate> CompressSameCategoryInWindow(
            List<StoryCandidate> input,
            CampaignTime now)
        {
            if (input == null || input.Count == 0)
            {
                return input;
            }

            var output = new List<StoryCandidate>();
            var grouped = input
                .Where(c => now.ToHours - c.EmittedAt.ToHours <= WindowHours)
                .GroupBy(c => c.CategoryId ?? "_none");

            foreach (var g in grouped)
            {
                var list = g.ToList();
                if (list.Count <= 1 || string.IsNullOrEmpty(g.Key) || g.Key == "_none")
                {
                    output.AddRange(list);
                    continue;
                }

                var compressed = list.OrderByDescending(c => c.SeverityHint).First();
                compressed.RenderedBody = $"{compressed.RenderedBody} ({list.Count} similar in past 48 hours)";
                output.Add(compressed);
            }

            return output;
        }
    }
}
