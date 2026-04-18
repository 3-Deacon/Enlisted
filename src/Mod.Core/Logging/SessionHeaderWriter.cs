using System;
using System.IO;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Enlisted.Mod.Core.Logging
{
    /// <summary>
    /// Writes a one-time header block at the top of the session log. Captures
    /// mod version, game version, enlisted state, and player snapshot so that
    /// a reader of the log has fixed state up front.
    /// </summary>
    internal static class SessionHeaderWriter
    {
        public static string Build()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== ENLISTED SESSION ===");
            sb.AppendLine($"Started:  {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Mod:      {GetModVersion()}");
            sb.AppendLine($"Game:     {GetGameVersion()}");
            sb.AppendLine($"Player:   {GetPlayerSummary()}");
            sb.AppendLine($"Flags:    {GetFlags()}");
            sb.AppendLine("========================");
            return sb.ToString();
        }

        private static string GetModVersion()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var ver = asm.GetName().Version?.ToString() ?? "unknown";
                return $"Enlisted {ver}";
            }
            catch
            {
                return "Enlisted (version unavailable)";
            }
        }

        private static string GetGameVersion()
        {
            // v1.3.13 has no ApplicationVersionHelper.GameVersionString(); use
            // ApplicationVersion.FromParametersFile() which reads Parameters/Version.xml
            // (same approach already used by ModConflictDiagnostics).
            try
            {
                return ApplicationVersion.FromParametersFile().ToString();
            }
            catch
            {
                return "unknown";
            }
        }

        private static string GetPlayerSummary()
        {
            try
            {
                var hero = Hero.MainHero;
                if (hero == null)
                {
                    return "(no main hero)";
                }
                return hero.Name?.ToString() ?? "(unnamed)";
            }
            catch
            {
                return "(unavailable)";
            }
        }

        private static string GetFlags()
        {
            try
            {
                var hero = Hero.MainHero;
                var party = hero?.PartyBelongedTo;
                var settlement = hero?.CurrentSettlement?.Name?.ToString() ?? "none";
                var partyName = party?.Name?.ToString() ?? "none";
                return $"Party={partyName}, Settlement={settlement}";
            }
            catch
            {
                return "(unavailable)";
            }
        }
    }
}
