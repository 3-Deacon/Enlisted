using System;
using System.Text;
using Enlisted.Mod.Core.Config;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Mod.Core.Logging
{
    /// <summary>
    ///     Logs session startup diagnostics once per game session.
    ///     Provides visibility into mod configuration and state without log spam.
    /// </summary>
    public static class SessionDiagnostics
    {
        /// <summary>Mod version read from the assembly at runtime (primary source: Properties/AssemblyInfo.cs).</summary>
        public static string ModVersion => _modVersionCache ?? (_modVersionCache = ResolveModVersion());

        private static string _modVersionCache;

        private static string ResolveModVersion()
        {
            try
            {
                var asm = typeof(SessionDiagnostics).Assembly;
                var version = asm.GetName().Version;
                return version != null ? version.ToString() : "unknown";
            }
            catch
            {
                return "unknown";
            }
        }

        public const string TargetGameVersion = "1.3.13";
        private static bool _hasLoggedStartup;
        private static bool _hasLoggedConfigValues;

        /// <summary>
        ///     Log startup diagnostics once when the game session begins.
        ///     Safe to call multiple times - only logs once per session.
        /// </summary>
        public static void LogStartupDiagnostics()
        {
            if (_hasLoggedStartup)
            {
                return;
            }

            _hasLoggedStartup = true;

            var sb = new StringBuilder();
            _ = sb.AppendLine("=== ENLISTED MOD SESSION START ===");
            _ = sb.AppendLine($"Mod Version: {ModVersion}");
            _ = sb.AppendLine($"Minimum Required Game Version: {TargetGameVersion}");
            _ = sb.AppendLine($"Session Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _ = sb.AppendLine($".NET Runtime: {Environment.Version}");
            _ = sb.AppendLine("===================================");

            ModLogger.Info("SESSION", sb.ToString());
        }

        /// <summary>
        ///     Log configuration values once when configs are first loaded.
        ///     Helps verify that JSON configs are being read correctly.
        /// </summary>
        public static void LogConfigurationValues()
        {
            if (_hasLoggedConfigValues)
            {
                return;
            }

            _hasLoggedConfigValues = true;

            try
            {
                var sb = new StringBuilder();
                _ = sb.AppendLine("--- Configuration Loaded ---");

                // Retirement config
                var retirement = ConfigurationManager.LoadRetirementConfig();
                _ = sb.AppendLine($"[Retirement] first_term_days: {retirement.FirstTermDays}");
                _ = sb.AppendLine($"[Retirement] probation_days: {retirement.ProbationDays}");

                // Escalation config
                var escalation = ConfigurationManager.LoadEscalationConfig();
                _ = sb.AppendLine($"[Escalation] enabled: {escalation?.Enabled == true}");
                _ = sb.AppendLine($"[Escalation] scrutiny_decay_days: {escalation?.ScrutinyDecayIntervalDays}");
                _ = sb.AppendLine($"[Escalation] discipline_decay_days: {escalation?.DisciplineDecayIntervalDays}");

                _ = sb.AppendLine("----------------------------");

                ModLogger.Info("CONFIG", sb.ToString());
            }
            catch (Exception ex)
            {
                ModLogger.Caught("CONFIG", "Failed to log configuration values", ex);
            }
        }

        /// <summary>
        ///     Log a state transition for debugging purposes.
        ///     Use for important one-time events, not per-tick updates.
        /// </summary>
        public static void LogStateTransition(string system, string fromState, string toState, string details = null)
        {
            var message = $"[{fromState}] -> [{toState}]";
            if (!string.IsNullOrEmpty(details))
            {
                message += $" | {details}";
            }

            ModLogger.Debug(system, message);
        }

        /// <summary>
        ///     Log an important one-time event for troubleshooting.
        /// </summary>
        public static void LogEvent(string system, string eventName, string details = null)
        {
            var message = $"EVENT: {eventName}";
            if (!string.IsNullOrEmpty(details))
            {
                message += $" | {details}";
            }

            ModLogger.Debug(system, message);
        }
    }

    /// <summary>
    ///     Lightweight, user-friendly save/load logging.
    ///     Implemented via two marker behaviors registered first/last so we can log both "begin" and "end"
    ///     without relying on fragile internal save-system hooks.
    /// </summary>
    public static class SaveLoadDiagnostics
    {
        private static readonly object Sync = new object();

        private static int _saveSequence;
        private static int _loadSequence;

        private static DateTime _saveStartUtc;
        private static DateTime _loadStartUtc;

        private static bool _saveInProgress;
        private static bool _loadInProgress;

        public static void OnSaveBegin()
        {
            lock (Sync)
            {
                if (_saveInProgress)
                {
                    return;
                }

                _saveInProgress = true;
                _saveSequence++;
                _saveStartUtc = DateTime.UtcNow;
            }

            var heroName = Hero.MainHero?.Name?.ToString() ?? "Unknown";
            var day = CampaignTime.Now.ToDays;
            ModLogger.Info("SAVELOAD", $"Saving game... (#{_saveSequence}, Hero: {heroName}, Day: {day:F1})");
        }

        public static void OnSaveEnd()
        {
            int seq;
            DateTime startUtc;

            lock (Sync)
            {
                if (!_saveInProgress)
                {
                    return;
                }

                _saveInProgress = false;
                seq = _saveSequence;
                startUtc = _saveStartUtc;
            }

            var elapsedMs = (int)Math.Max(0, (DateTime.UtcNow - startUtc).TotalMilliseconds);
            ModLogger.Info("SAVELOAD", $"Save finished. (#{seq}, {elapsedMs} ms)");
        }

        public static void OnLoadBegin()
        {
            lock (Sync)
            {
                if (_loadInProgress)
                {
                    return;
                }

                _loadInProgress = true;
                _loadSequence++;
                _loadStartUtc = DateTime.UtcNow;
            }

            ModLogger.Info("SAVELOAD", $"Loading save... (#{_loadSequence})");
        }

        public static void OnLoadEnd()
        {
            int seq;
            DateTime startUtc;

            lock (Sync)
            {
                if (!_loadInProgress)
                {
                    return;
                }

                _loadInProgress = false;
                seq = _loadSequence;
                startUtc = _loadStartUtc;
            }

            var elapsedMs = (int)Math.Max(0, (DateTime.UtcNow - startUtc).TotalMilliseconds);
            ModLogger.Info("SAVELOAD", $"Load finished. (#{seq}, {elapsedMs} ms)");
        }

        /// <summary>
        /// Wrap a behavior SyncData() body so save/load failures always produce a clear log line
        /// identifying the exact behavior that broke serialization.
        ///
        /// IMPORTANT:
        /// - We rethrow after logging. Swallowing save/load exceptions can silently corrupt state.
        /// - This method only logs when something actually fails (no spam).
        /// </summary>
        public static void SafeSyncData(CampaignBehaviorBase behavior, IDataStore dataStore, Action syncAction)
        {
            if (dataStore == null || syncAction == null)
            {
                return;
            }

            try
            {
                syncAction();
            }
            catch (Exception ex)
            {
                var phase = dataStore.IsSaving
                    ? "Saving"
                    : dataStore.IsLoading
                        ? "Loading"
                        : "SyncData";

                var behaviorName = behavior?.GetType().FullName ?? "UnknownBehavior";

                ModLogger.Caught(
                    "SAVELOAD",
                    $"Save/load failed in {behaviorName} during {phase}. This can break saves. Try: load an older save, disable recently-added mods, then share logs from the Enlisted mod's Debugging folder (Session-A and Conflicts-A logs) and the save file if possible.",
                    ex);

                throw;
            }
        }
    }

    /// <summary>
    ///     Save/load marker behavior. Register one instance at the beginning of the behavior list and one at the end.
    ///     This yields predictable "begin" and "end" callbacks during Save/Load serialization passes.
    /// </summary>
    public sealed class SaveLoadDiagnosticsMarkerBehavior : CampaignBehaviorBase
    {
        public enum Phase
        {
            Begin,
            End
        }

        private readonly Phase _phase;

        public SaveLoadDiagnosticsMarkerBehavior(Phase phase)
        {
            _phase = phase;
        }

        public override void RegisterEvents()
        {
            // No runtime events required.
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore == null)
            {
                return;
            }

            if (dataStore.IsSaving)
            {
                if (_phase == Phase.Begin)
                {
                    SaveLoadDiagnostics.OnSaveBegin();
                }
                else
                {
                    SaveLoadDiagnostics.OnSaveEnd();
                }
            }
            else if (dataStore.IsLoading)
            {
                if (_phase == Phase.Begin)
                {
                    SaveLoadDiagnostics.OnLoadBegin();
                }
                else
                {
                    SaveLoadDiagnostics.OnLoadEnd();
                }
            }
        }
    }
}
