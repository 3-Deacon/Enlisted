using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Enlisted.Features.Interface.Utils
{
    /// <summary>
    /// Formats combat log messages with RichTextWidget-compatible encyclopedia links.
    /// Uses native Bannerlord link format: &lt;a href="event:{EncyclopediaLink}"&gt;Name&lt;/a&gt;
    /// </summary>
    public static class EncyclopediaLinkFormatter
    {
        /// <summary>
        /// Enhances message text by replacing hero and settlement names with clickable links.
        /// Format matches native: &lt;a href="event:TaleWorlds.CampaignSystem.Hero-id"&gt;Name&lt;/a&gt;
        /// The "event:" prefix is required by RichTextWidget's link parser.
        /// </summary>
        public static string AddEncyclopediaLinks(string messageText)
        {
            if (string.IsNullOrEmpty(messageText))
            {
                return messageText;
            }

            // Skip messages that already contain encyclopedia links (native messages)
            // This prevents nested <a> tags which break the RichText parser
            if (messageText.Contains("<a") || messageText.Contains("href="))
            {
                return messageText;
            }

            var result = messageText;

            // Process settlements first (longer names first to avoid partial matches)
            var settlements = Settlement.All
                .Where(s => !string.IsNullOrEmpty(s.Name?.ToString()) && !string.IsNullOrEmpty(s.EncyclopediaLink))
                .OrderByDescending(s => s.Name.ToString().Length)
                .ToList();

            foreach (var settlement in settlements)
            {
                var settlementName = settlement.Name.ToString();
                // Only replace if name exists and isn't already part of a link
                if (result.Contains(settlementName) && !result.Contains($">{settlementName}<"))
                {
                    // Use native format: <a style="Link.Settlement" href="event:{EncyclopediaLink}"><b>Name</b></a>
                    // The style attribute makes the link visually distinct and clickable
                    // The <b> tag matches native formatting (bold + different color)
                    var linkedName = $"<a style=\"Link.Settlement\" href=\"event:{settlement.EncyclopediaLink}\"><b>{settlementName}</b></a>";
                    result = result.Replace(settlementName, linkedName);
                }
            }

            // Process heroes (longer names first to avoid partial matches)
            var heroes = Hero.AllAliveHeroes
                .Where(h => !string.IsNullOrEmpty(h.Name?.ToString()) && !string.IsNullOrEmpty(h.EncyclopediaLink))
                .OrderByDescending(h => h.Name.ToString().Length)
                .ToList();

            foreach (var hero in heroes)
            {
                var heroName = hero.Name.ToString();
                // Only replace if name exists and isn't already part of a link
                if (result.Contains(heroName) && !result.Contains($">{heroName}<"))
                {
                    // Use native format: <a style="Link.Hero" href="event:{EncyclopediaLink}"><b>Name</b></a>
                    // The style attribute makes the link visually distinct and clickable
                    // The <b> tag matches native formatting (bold + different color)
                    var linkedName = $"<a style=\"Link.Hero\" href=\"event:{hero.EncyclopediaLink}\"><b>{heroName}</b></a>";
                    result = result.Replace(heroName, linkedName);
                }
            }

            return result;
        }
    }
}
