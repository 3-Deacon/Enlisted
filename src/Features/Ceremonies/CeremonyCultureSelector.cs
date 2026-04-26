using Enlisted.Features.Enlistment.Behaviors;

namespace Enlisted.Features.Ceremonies
{
    /// <summary>
    /// Maps the player's enlisted-faction culture to one of the three authored
    /// ceremony cultural variants (vlandian / sturgian / imperial) plus a base
    /// catch-all. Battania / Aserai / Khuzait fall back to the closest cultural
    /// cousin for v1; Plan 7 polish pass adds full per-culture variants if
    /// playtest demands. Storylet IDs follow the locked underscore convention
    /// (architecture brief §4 rule 6).
    /// </summary>
    public static class CeremonyCultureSelector
    {
        public static string SelectVariantSuffix()
        {
            var lord = EnlistmentBehavior.Instance?.EnlistedLord;
            var cultureId = lord?.Culture?.StringId;
            if (string.IsNullOrEmpty(cultureId))
            {
                return "base";
            }

            switch (cultureId)
            {
                case "vlandia":
                case "battania":
                    return "vlandian";
                case "sturgia":
                case "khuzait":
                    return "sturgian";
                case "empire":
                case "aserai":
                    return "imperial";
                default:
                    return "base";
            }
        }

        public static string ResolveStoryletId(int newTier)
        {
            if (newTier < 2 || newTier > 9)
            {
                return null;
            }

            var suffix = SelectVariantSuffix();
            var prev = newTier - 1;
            return "ceremony_t" + prev + "_to_t" + newTier + "_" + suffix;
        }
    }
}
