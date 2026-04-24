using System;

namespace Enlisted.Features.Interface.Models
{
    /// <summary>
    /// Shared constants and small routing helpers for native Camp activity menus.
    /// </summary>
    public static class CampActivityMenuPolicy
    {
        public const int ActivitySlotCount = 5;
        public const int DecisionOptionSlotCount = 6;

        public static bool IsCancelOptionId(string optionId)
        {
            if (string.IsNullOrWhiteSpace(optionId))
            {
                return false;
            }

            return optionId.IndexOf("cancel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   optionId.IndexOf("nevermind", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   optionId.IndexOf("not_now", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   optionId.IndexOf("skip", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   optionId.IndexOf("back", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   optionId.IndexOf("decline", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
