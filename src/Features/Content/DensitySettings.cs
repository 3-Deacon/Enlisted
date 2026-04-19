﻿using System;
using System.IO;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Util;
using Newtonsoft.Json.Linq;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Static density / pacing knobs read from enlisted_config.json. Read once at
    /// submodule init; the Director reads these at every candidate emit.
    ///
    /// Keys consumed (top-level):
    ///   event_density             : "sparse" | "normal" | "dense" -- maps to ModalFloorInGameDays (7/5/3).
    ///   speed_downshift_on_modal  : bool -- if true, FastForward drops to StoppablePlay before a modal fires.
    ///
    /// ModalFloorWallClockSeconds, QuietStretchDays, and CategoryCooldownDays are
    /// defaults-only today — no JSON binding yet. Phase 2+ tasks may expose them.
    /// </summary>
    public static class DensitySettings
    {
        private const string LogCategory = "CONTENT";

        public static int ModalFloorInGameDays { get; private set; } = 5;
        public static int ModalFloorWallClockSeconds { get; private set; } = 60;
        public static int QuietStretchDays { get; private set; } = 14;
        public static bool SpeedDownshiftOnModal { get; private set; } = true;
        public static int CategoryCooldownDays { get; private set; } = 12;

        public static void Load()
        {
            var path = ModulePaths.GetConfigPath("enlisted_config.json");
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                ModLogger.Expected(LogCategory, "density_config_missing",
                    $"enlisted_config.json not found at {path}; Director pacing uses defaults");
                return;
            }

            try
            {
                var root = JObject.Parse(File.ReadAllText(path));
                var density = root["event_density"]?.ToString() ?? "normal";
                ModalFloorInGameDays = density switch
                {
                    "sparse" => 7,
                    "dense" => 3,
                    _ => 5
                };

                SpeedDownshiftOnModal = root["speed_downshift_on_modal"]?.ToObject<bool>() ?? true;
            }
            catch (Exception ex)
            {
                ModLogger.Caught(LogCategory, "DensitySettings.Load failed; using defaults", ex);
            }
        }
    }
}
