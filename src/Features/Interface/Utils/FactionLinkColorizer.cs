using System.Text.RegularExpressions;
using Enlisted.Mod.Core.Logging;

namespace Enlisted.Features.Interface.Utils
{
    /// <summary>
    /// Replaces generic Link.Kingdom styles with faction-specific styles
    /// so each kingdom appears in its native banner color.
    /// </summary>
    public static class FactionLinkColorizer
    {
        /// <summary>
        /// Replaces Link.Kingdom references with faction-specific link styles
        /// based on the kingdom name in the message.
        /// </summary>
        public static string ColorizeFactionLinks(string messageText)
        {
            if (string.IsNullOrEmpty(messageText) || !messageText.Contains("Link.Kingdom"))
            {
                return messageText;
            }

            // Pattern to find <a style="Link.Kingdom" href="...">content</a>
            // Content can include <b> tags, so we need to match everything until </a>
            var pattern = @"<a style=""Link\.Kingdom"" href=""([^""]+)"">(.+?)</a>";

            return Regex.Replace(messageText, pattern, match =>
            {
                var href = match.Groups[1].Value; // e.g., "event:Faction:faction_empire_n"
                var content = match.Groups[2].Value; // May include <b>Name</b>

                // Determine faction style based on href (more reliable than display name)
                string factionStyle = GetFactionStyleFromHref(href);

                ModLogger.Info("Interface", $"Colorizing: href={href} -> {factionStyle}");

                // Replace with faction-specific style, preserving original content (including <b> tags)
                return $"<a style=\"{factionStyle}\" href=\"{href}\">{content}</a>";
            });
        }

        /// <summary>
        /// Determines the appropriate faction link style based on the href (encyclopedia link).
        /// More reliable than matching display names since href contains the faction StringId.
        /// </summary>
        private static string GetFactionStyleFromHref(string href)
        {
            // href format: "event:Faction:kingdom_1" or "Faction:kingdom_1"
            // Extract the StringId and map to faction style
            var stringId = href.ToLower();

            // Map faction StringIds to brush styles
            if (stringId.Contains("vlandia"))
            {
                return "Link.FactionVlandia";
            }

            if (stringId.Contains("sturgia"))
            {
                return "Link.FactionSturgia";
            }

            if (stringId.Contains("aserai"))
            {
                return "Link.FactionAserai";
            }

            if (stringId.Contains("khuzait"))
            {
                return "Link.FactionKhuzait";
            }

            if (stringId.Contains("battania"))
            {
                return "Link.FactionBattania";
            }

            if (stringId.Contains("empire"))
            {
                // Differentiate between the three empires
                if (stringId.Contains("empire_s"))
                {
                    return "Link.FactionEmpire_S";
                }

                if (stringId.Contains("empire_w"))
                {
                    return "Link.FactionEmpire_W";
                }

                if (stringId.Contains("north"))
                {
                    return "Link.FactionEmpire_N";
                }

                // Generic empire fallback
                return "Link.FactionEmpire_N";
            }

            // Fallback to generic kingdom style
            return "Link.Kingdom";
        }
    }
}
