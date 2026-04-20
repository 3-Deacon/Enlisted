using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Enlisted.Mod.Core.Logging;

namespace Enlisted.Mod.Core.Config
{
    /// <summary>
    /// Strongly-typed mod settings loaded from ModuleData/Enlisted/Config/settings.json.
    /// Contains configuration options for logging, discovery, and encounter management.
    /// Defaults are safe and minimal. Fails closed if file is missing or invalid.
    /// </summary>
    [DataContract]
    public sealed class ModSettings
    {
        /// <summary>
        /// Whether to log menu open/close events for debugging.
        /// </summary>
        [DataMember(Name = "LogMenus")]
        public bool LogMenus { get; set; } = true;

        /// <summary>
        /// Whether to log campaign events (hourly/daily ticks, etc.) for debugging.
        /// </summary>
        [DataMember(Name = "LogCampaignEvents")]
        public bool LogCampaignEvents { get; set; } = true;

        /// <summary>
        /// Whether to run deep troop discovery validation at session launch.
        /// This is a development-only diagnostic and can be expensive/noisy.
        /// Leave disabled for normal play.
        /// </summary>
        [DataMember(Name = "RunTroopDiscoveryValidation")]
        public bool RunTroopDiscoveryValidation { get; set; }

        /// <summary>
        /// Whether to run mod conflict diagnostics at startup and write to Conflicts-A_*.log.
        /// Useful for diagnosing issues caused by other mods patching the same game methods.
        /// When disabled, the conflict log file will not be generated.
        /// </summary>
        [DataMember(Name = "LogModConflicts")]
        public bool LogModConflicts { get; set; } = true;

        /// <summary>
        /// Seconds to suppress repeated identical log messages (0 to disable throttling).
        /// Prevents log spam when the same message would be written many times.
        /// </summary>
        [DataMember(Name = "LogThrottleSeconds")]
        public int LogThrottleSeconds { get; set; } = 5;

        /// <summary>
        /// Per-category log level configuration.
        /// Allows fine-grained control over which categories log at what verbosity.
        /// </summary>
        [DataMember(Name = "LogLevels")]
        public LogLevelSettings LogLevels { get; set; } = new LogLevelSettings();

        /// <summary>
        /// Settings for encounter suppression and party attachment behavior.
        /// Controls when encounters are prevented and how party following works.
        /// </summary>
        [DataMember(Name = "Encounter")]
        public EncounterSettings Encounter { get; set; } = new EncounterSettings();

        /// <summary>
        /// Feature flag to enable the new multi-stage muster menu system.
        /// If true, uses MusterMenuHandler for comprehensive muster experience.
        /// If false, falls back to legacy inquiry popup.
        /// Default is true (new system is production-ready).
        /// </summary>
        [DataMember(Name = "use_new_muster_menu")]
        public bool UseNewMusterMenu { get; set; } = true;

        /// <summary>
        /// Whether to pause game time during muster sequence.
        /// If true, game stops and time controls are locked during muster menus.
        /// If false, respects player's current time speed and allows time advancement.
        /// Default is true to create a focused administrative checkpoint experience.
        /// </summary>
        [DataMember(Name = "PauseGameDuringMuster")]
        public bool PauseGameDuringMuster { get; set; } = true;

        /// <summary>
        /// Whether to enable debug tools in-game (debug menu options, test commands).
        /// When true, adds debug options to Camp Hub menu for testing features.
        /// Default is true for development/testing, set to false for production.
        /// </summary>
        [DataMember(Name = "EnableDebugTools")]
        public bool EnableDebugTools { get; set; } = true;

        /// <summary>
        /// Per-category log level configuration.
        /// Each property maps to a logging category and controls its verbosity.
        /// Valid values: "Off", "Error", "Warn", "Info", "Debug", "Trace"
        /// </summary>
        [DataContract]
        public sealed class LogLevelSettings
        {
            /// <summary>Default log level for categories not explicitly configured.</summary>
            [DataMember(Name = "Default")]
            public string Default { get; set; } = "Info";

            /// <summary>Battle system logging (combat, victory/defeat, damage).</summary>
            [DataMember(Name = "Battle")]
            public string Battle { get; set; } = "Info";

            /// <summary>Siege system logging (assault, defense, sally-outs).</summary>
            [DataMember(Name = "Siege")]
            public string Siege { get; set; } = "Info";

            /// <summary>Combat tracking (kill counts, participation).</summary>
            [DataMember(Name = "Combat")]
            public string Combat { get; set; } = "Info";

            /// <summary>Equipment system logging (backup, restore, changes).</summary>
            [DataMember(Name = "Equipment")]
            public string Equipment { get; set; } = "Info";

            /// <summary>Gold transaction logging (wages, purchases, costs).</summary>
            [DataMember(Name = "Gold")]
            public string Gold { get; set; } = "Info";

            /// <summary>XP system logging (awards, progression).</summary>
            [DataMember(Name = "XP")]
            public string Xp { get; set; } = "Info";

            /// <summary>Menu system logging (state transitions, activation).</summary>
            [DataMember(Name = "Menu")]
            public string Menu { get; set; } = "Warn";

            /// <summary>Encounter system logging (suppression, party following).</summary>
            [DataMember(Name = "Encounter")]
            public string Encounter { get; set; } = "Warn";

            /// <summary>Promotion system logging (tier changes, notifications).</summary>
            [DataMember(Name = "Promotion")]
            public string Promotion { get; set; } = "Info";

            /// <summary>Duties system logging (assignments, training).</summary>
            [DataMember(Name = "Duties")]
            public string Duties { get; set; } = "Info";

            /// <summary>Troop selection logging (equipment choices).</summary>
            [DataMember(Name = "TroopSelection")]
            public string TroopSelection { get; set; } = "Info";

            /// <summary>Lance Life Events logging (catalog load, scheduling, delivery).</summary>
            [DataMember(Name = "LanceLifeEvents")]
            public string LanceLifeEvents { get; set; } = "Info";

            /// <summary>Camp Activities logging (definitions load, execution).</summary>
            [DataMember(Name = "CampActivities")]
            public string CampActivities { get; set; } = "Info";

            /// <summary>Player Conditions logging (injury/illness/exhaustion changes).</summary>
            [DataMember(Name = "PlayerConditions")]
            public string PlayerConditions { get; set; } = "Info";

            /// <summary>Troop discovery validation logging (development-only).</summary>
            [DataMember(Name = "TroopDiscovery")]
            public string TroopDiscovery { get; set; } = "Off";

            /// <summary>Kill tracker logging (per-mission tracking).</summary>
            [DataMember(Name = "KillTracker")]
            public string KillTracker { get; set; } = "Info";

            /// <summary>Enlistment core logging (join, leave, state changes).</summary>
            [DataMember(Name = "Enlistment")]
            public string Enlistment { get; set; } = "Info";

            /// <summary>Patch system logging (Harmony patches).</summary>
            [DataMember(Name = "Patch")]
            public string Patch { get; set; } = "Warn";

            /// <summary>Interface/UI logging.</summary>
            [DataMember(Name = "Interface")]
            public string Interface { get; set; } = "Warn";

            /// <summary>Bootstrap/initialization logging.</summary>
            [DataMember(Name = "Bootstrap")]
            public string Bootstrap { get; set; } = "Info";

            /// <summary>Session diagnostics logging.</summary>
            [DataMember(Name = "Session")]
            public string Session { get; set; } = "Info";

            /// <summary>Configuration loading logging.</summary>
            [DataMember(Name = "Config")]
            public string Config { get; set; } = "Info";

            /// <summary>Naval battle logging (ship assignment, deployment).</summary>
            [DataMember(Name = "Naval")]
            public string Naval { get; set; } = "Info";

            /// <summary>
            /// Convert settings to a dictionary for ModLogger.ConfigureLevels().
            /// </summary>
            public Dictionary<string, LogLevel> ToDictionary()
            {
                var dict = new Dictionary<string, LogLevel>(StringComparer.OrdinalIgnoreCase);

                AddLevel(dict, "Battle", Battle);
                AddLevel(dict, "Siege", Siege);
                AddLevel(dict, "Combat", Combat);
                AddLevel(dict, "Equipment", Equipment);
                AddLevel(dict, "Gold", Gold);
                AddLevel(dict, "XP", Xp);
                AddLevel(dict, "Menu", Menu);
                AddLevel(dict, "Encounter", Encounter);
                AddLevel(dict, "Promotion", Promotion);
                AddLevel(dict, "Duties", Duties);
                AddLevel(dict, "TroopSelection", TroopSelection);
                AddLevel(dict, "LanceLifeEvents", LanceLifeEvents);
                AddLevel(dict, "CampActivities", CampActivities);
                AddLevel(dict, "PlayerConditions", PlayerConditions);
                AddLevel(dict, "TroopDiscovery", TroopDiscovery);
                AddLevel(dict, "KillTracker", KillTracker);
                AddLevel(dict, "Enlistment", Enlistment);
                AddLevel(dict, "Patch", Patch);
                AddLevel(dict, "Interface", Interface);
                AddLevel(dict, "Bootstrap", Bootstrap);
                AddLevel(dict, "Session", Session);
                AddLevel(dict, "Config", Config);
                AddLevel(dict, "Naval", Naval);

                return dict;
            }

            /// <summary>
            /// Get the default log level parsed from the Default string.
            /// </summary>
            public LogLevel GetDefaultLevel()
            {
                return ParseLevel(Default);
            }

            private void AddLevel(Dictionary<string, LogLevel> dict, string category, string levelStr)
            {
                dict[category] = ParseLevel(levelStr);
            }

            private LogLevel ParseLevel(string levelStr)
            {
                if (string.IsNullOrEmpty(levelStr))
                {
                    return LogLevel.Info;
                }

                if (Enum.TryParse<LogLevel>(levelStr, true, out var level))
                {
                    return level;
                }

                return LogLevel.Info;
            }
        }

        /// <summary>
        /// Settings for encounter suppression and party attachment behavior.
        /// Controls when encounters are prevented and how party following works
        /// when the player is enlisted with a lord.
        /// </summary>
        [DataContract]
        public sealed class EncounterSettings
        {
            /// <summary>
            /// Whether to attach the player's party to the lord's party when close.
            /// When true, the player party follows the lord more closely.
            /// </summary>
            [DataMember(Name = "AttachWhenClose")]
            public bool AttachWhenClose { get; set; } = true;

            /// <summary>
            /// Distance threshold for attaching to the lord's party, in game units.
            /// When the player is within this distance, party attachment is attempted.
            /// </summary>
            [DataMember(Name = "AttachRange")]
            public double AttachRange { get; set; } = 0.6;

            /// <summary>
            /// Distance for trailing behind the lord's party, in game units.
            /// When following, the player party maintains this distance behind the lord.
            /// </summary>
            [DataMember(Name = "TrailDistance")]
            public double TrailDistance { get; set; } = 1.2;

            /// <summary>
            /// Whether to suppress unwanted player encounters while enlisted.
            /// When true, random encounters are prevented while still allowing battle participation.
            /// </summary>
            [DataMember(Name = "SuppressPlayerEncounter")]
            public bool SuppressPlayerEncounter { get; set; } = true;
        }

        /// <summary>
        /// Attempts to load settings from the installed module's ModuleData folder.
        /// On failure, returns defaults and does not throw.
        /// </summary>
        public static ModSettings LoadFromModule()
        {
            try
            {
                var settingsPath = ResolveSettingsPath();
                if (!File.Exists(settingsPath))
                {
                    return new ModSettings();
                }

                using var stream = File.OpenRead(settingsPath);
                var serializer = new DataContractJsonSerializer(typeof(ModSettings));
                var loaded = serializer.ReadObject(stream) as ModSettings;
                return loaded ?? new ModSettings();
            }
            catch
            {
                // Fail closed; avoid throwing during module load
                return new ModSettings();
            }
        }

        /// <summary>
        /// Apply log level settings to ModLogger.
        /// Call this after loading settings to configure logging verbosity.
        /// </summary>
        public void ApplyLogLevels()
        {
            try
            {
                var levels = LogLevels?.ToDictionary() ?? new Dictionary<string, LogLevel>();
                var defaultLevel = LogLevels?.GetDefaultLevel() ?? LogLevel.Info;
                ModLogger.ConfigureLevels(levels, defaultLevel, LogThrottleSeconds);

                // Log levels that are set to Debug or higher verbosity for diagnostics
                foreach (var kvp in levels)
                {
                    if (kvp.Value >= LogLevel.Debug)
                    {
                        ModLogger.Info("Config", $"Category '{kvp.Key}' set to {kvp.Value}");
                    }
                }
            }
            catch
            {
                // Don't crash if log configuration fails
            }
        }

        /// <summary>
        /// Resolves absolute path to ModuleData/Enlisted/Config/settings.json from the executing assembly location.
        /// </summary>
        private static string ResolveSettingsPath()
        {
            // Executing DLL is at: .../Modules/Enlisted/bin/Win64_*/Enlisted.dll
            var dllDir = Path.GetDirectoryName(typeof(ModSettings).Assembly.Location);
            if (string.IsNullOrWhiteSpace(dllDir))
            {
                return "settings.json"; // harmless fallback; will not exist
            }

            var moduleRoot = Directory.GetParent(dllDir); // Win64_*
            moduleRoot = moduleRoot?.Parent; // bin
            moduleRoot = moduleRoot?.Parent; // Enlisted (module root)

            var path = moduleRoot != null
                ? Path.Combine(moduleRoot.FullName, "ModuleData", "Enlisted", "Config", "settings.json")
                : "settings.json";
            return path;
        }
    }
}
