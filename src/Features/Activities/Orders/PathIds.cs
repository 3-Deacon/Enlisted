using System;
using System.Collections.Generic;

namespace Enlisted.Features.Activities.Orders
{
    internal static class PathIds
    {
        public static readonly IReadOnlyList<string> All = new string[]
        {
            "ranger",
            "enforcer",
            "support",
            "diplomat",
            "rogue"
        };

        public static readonly HashSet<string> Set = new HashSet<string>(All, StringComparer.OrdinalIgnoreCase);
    }
}
