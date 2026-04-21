using System;
using System.Collections.Generic;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Equipment
{
    /// <summary>
    /// Walks a faction's troop tree from BasicTroop and EliteBasicTroop, returning
    /// all reachable non-hero CharacterObjects of the given culture.
    /// </summary>
    public static class CultureTroopTreeHelper
    {
        /// <summary>
        /// Returns the culture's full reachable troop tree (BFS over UpgradeTargets).
        /// </summary>
        public static List<CharacterObject> BuildCultureTroopTree(CultureObject culture)
        {
            var results = new List<CharacterObject>();
            try
            {
                if (culture == null)
                {
                    return results;
                }

                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var queue = new Queue<CharacterObject>();

                void EnqueueIfValid(CharacterObject start)
                {
                    if (start == null)
                    {
                        return;
                    }
                    if (start.Culture != culture)
                    {
                        return;
                    }
                    if (start.IsHero)
                    {
                        return;
                    }
                    if (!visited.Add(start.StringId))
                    {
                        return;
                    }
                    queue.Enqueue(start);
                }

                EnqueueIfValid(culture.BasicTroop);
                EnqueueIfValid(culture.EliteBasicTroop);

                while (queue.Count > 0)
                {
                    var node = queue.Dequeue();
                    results.Add(node);

                    try
                    {
                        var upgrades = node.UpgradeTargets; // MBReadOnlyList<CharacterObject>
                        if (upgrades != null)
                        {
                            foreach (var next in upgrades)
                            {
                                if (next != null && next.Culture == culture && !next.IsHero && visited.Add(next.StringId))
                                {
                                    queue.Enqueue(next);
                                }
                            }
                        }
                    }
                    catch
                    {
                        // best-effort; continue on any API differences
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("TROOPTREE", "BuildCultureTroopTree failed", ex);
            }
            return results;
        }
    }
}
