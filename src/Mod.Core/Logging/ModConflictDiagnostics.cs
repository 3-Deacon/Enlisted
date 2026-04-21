using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Enlisted.Mod.Core.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Mod.Core.Logging
{
    /// <summary>
    ///     Centralized diagnostics for detecting mod conflicts and logging environment info.
    ///     Writes to a dedicated Conflicts-A_*.log file in the Debugging folder for easy user access.
    ///
    ///     This system is designed to be lightweight - it only runs at startup and when
    ///     deferred patches are applied. No per-frame or per-tick overhead.
    ///
    ///     Usage:
    ///     - Call RunStartupDiagnostics() from OnSubModuleLoad after initial Harmony.PatchAll()
    ///     - Call RefreshDeferredPatches() from NextFrameDispatcherPatch after deferred patches apply
    /// </summary>
    public static class ModConflictDiagnostics
    {
        // Harmony IDs used by Enlisted - must match SubModule.cs
        private const string EnlistedHarmonyId = "com.enlisted.mod";
        private const string EnlistedDeferredHarmonyId = "com.enlisted.mod.deferred";

        private static readonly object Sync = new();
        private static string _conflictLogPath;
        private static bool _hasRunStartup;
        private static bool _hasDeferredPatchInfo;
        private const string ConflictPrefix = "Conflicts-";
        private static readonly string[] ConflictSlots = { "Conflicts-A", "Conflicts-B", "Conflicts-C" };

        /// <summary>
        ///     Runs initial startup diagnostics and writes to Debugging/Conflicts-A_*.log.
        ///     Call this once from OnSubModuleLoad after the main Harmony.PatchAll() completes.
        ///
        ///     NOTE: This runs before deferred patches are applied, so those won't appear yet.
        ///     Call RefreshDeferredPatches() later to append deferred patch info.
        /// </summary>
        /// <param name="harmony">The main Harmony instance used by Enlisted</param>
        public static void RunStartupDiagnostics(Harmony harmony)
        {
            if (_hasRunStartup)
            {
                return;
            }

            try
            {
                _hasRunStartup = true;
                InitializeLogPath();

                WriteHeader();
                WriteEnvironmentInfo();
                WriteLoadedModules();
                WriteModuleHealthCheck();
                WritePatchApplicationHealth(harmony, "MAIN PATCHES");
                WritePatchConflicts(harmony, "MAIN PATCHES", EnlistedHarmonyId);
                WriteEnlistedPatchList(harmony, "Main");
                WriteNoteAboutDeferredPatches();
                WriteFooter(isPartial: true);

                ModLogger.Info("DIAGNOSTICS", $"Initial conflict diagnostics written to: {_conflictLogPath}");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("DIAGNOSTICS", "Failed to run startup diagnostics", ex);
            }
        }

        /// <summary>
        ///     Appends deferred patch information to the existing diagnostics log.
        ///     Call this from NextFrameDispatcherPatch.Postfix after deferred patches are applied.
        ///
        ///     This is separate from RunStartupDiagnostics because deferred patches are applied
        ///     on the first Campaign.Tick() to avoid TypeInitializationException on Proton/Linux.
        /// </summary>
        /// <param name="deferredHarmony">The deferred Harmony instance</param>
        public static void RefreshDeferredPatches(Harmony deferredHarmony)
        {
            if (_hasDeferredPatchInfo || string.IsNullOrWhiteSpace(_conflictLogPath))
            {
                return;
            }

            try
            {
                _hasDeferredPatchInfo = true;

                WriteLine();
                WriteLine(new string('=', 72));
                WriteLine("              DEFERRED PATCHES (Applied on Campaign Start)");
                WriteLine(new string('=', 72));
                WriteLine();
                WriteLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                WriteLine();

                WritePatchApplicationHealth(deferredHarmony, "DEFERRED PATCHES");
                WritePatchConflicts(deferredHarmony, "DEFERRED PATCHES", EnlistedDeferredHarmonyId);
                WriteEnlistedPatchList(deferredHarmony, "Deferred");
                WriteCombinedConflictSummary();
                WriteFooter(isPartial: false);

                ModLogger.Info("DIAGNOSTICS", "Deferred patch diagnostics appended to conflict log");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("DIAGNOSTICS", "Failed to refresh deferred patch diagnostics", ex);
            }
        }

        /// <summary>
        ///     Logs registered campaign behaviors for troubleshooting by querying the live
        ///     CampaignBehaviorManager. Call this from OnGameStart after all behaviors are registered.
        /// </summary>
        public static void LogRegisteredBehaviors()
        {
            if (string.IsNullOrWhiteSpace(_conflictLogPath))
            {
                return;
            }

            try
            {
                var behaviorTypes = Campaign.Current?.GetCampaignBehaviors<CampaignBehaviorBase>()
                    ?.Select(b => b.GetType().Name)
                    .OrderBy(n => n)
                    .ToList() ?? new List<string>();

                WriteLine();
                WriteLine("-- REGISTERED CAMPAIGN BEHAVIORS --");
                WriteLine();
                WriteLine($"  Total: {behaviorTypes.Count} behaviors");
                WriteLine();

                foreach (var name in behaviorTypes)
                {
                    WriteLine($"    {name}");
                }

                // Log runtime catalog status after behaviors are registered
                WriteRuntimeCatalogStatus();
            }
            catch (Exception ex)
            {
                ModLogger.Caught("DIAGNOSTICS", "Failed to log behaviors", ex);
            }
        }

        /// <summary>
        ///     Logs the runtime status of content catalogs (how many items actually loaded).
        ///     This shows whether JSON parsing succeeded, not just if files exist.
        /// </summary>
        private static void WriteRuntimeCatalogStatus()
        {
            try
            {
                WriteLine();
                WriteLine("-- CONTENT CATALOG STATUS (Runtime) --");
                WriteLine();

                // Use reflection to check catalog counts without forcing initialization
                var catalogStatus = new List<(string name, int count, string status)>();

                // QM Dialogue Catalog
                try
                {
                    var qmCatalogType = Type.GetType("Enlisted.Features.Conversations.Data.QMDialogueCatalog, Enlisted");
                    if (qmCatalogType != null)
                    {
                        var instanceProp = qmCatalogType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                        var instance = instanceProp?.GetValue(null);
                        var countProp = qmCatalogType.GetProperty("NodeCount");
                        var count = (int)(countProp?.GetValue(instance) ?? 0);
                        catalogStatus.Add(("QM Dialogue", count, count > 0 ? "OK" : "EMPTY"));
                    }
                }
                catch { catalogStatus.Add(("QM Dialogue", 0, "ERROR")); }

                // Event Catalog
                try
                {
                    var eventCatalogType = Type.GetType("Enlisted.Features.Content.EventCatalog, Enlisted");
                    if (eventCatalogType != null)
                    {
                        var countProp = eventCatalogType.GetProperty("EventCount", BindingFlags.Public | BindingFlags.Static);
                        var count = (int)(countProp?.GetValue(null) ?? 0);
                        catalogStatus.Add(("Events", count, count > 0 ? "OK" : "EMPTY"));
                    }
                }
                catch { catalogStatus.Add(("Events", 0, "ERROR")); }

                // Decision Catalog
                try
                {
                    var decisionCatalogType = Type.GetType("Enlisted.Features.Content.DecisionCatalog, Enlisted");
                    if (decisionCatalogType != null)
                    {
                        var countProp = decisionCatalogType.GetProperty("DecisionCount", BindingFlags.Public | BindingFlags.Static);
                        var count = (int)(countProp?.GetValue(null) ?? 0);
                        catalogStatus.Add(("Decisions", count, count > 0 ? "OK" : "EMPTY"));
                    }
                }
                catch { catalogStatus.Add(("Decisions", 0, "ERROR")); }

                // Order Catalog (doesn't have public count, just check if initialized)
                try
                {
                    var orderCatalogType = Type.GetType("Enlisted.Features.Orders.OrderCatalog, Enlisted");
                    if (orderCatalogType != null)
                    {
                        // Try to call GetAvailableOrders to see if it works
                        catalogStatus.Add(("Orders", -1, "PRESENT"));
                    }
                }
                catch { catalogStatus.Add(("Orders", 0, "ERROR")); }

                // Storylet Catalog (Spec 0 backbone)
                try
                {
                    var storyletType = Type.GetType("Enlisted.Features.Content.StoryletCatalog, Enlisted");
                    if (storyletType != null)
                    {
                        var allIdsProp = storyletType.GetProperty("AllIds", BindingFlags.Public | BindingFlags.Static);
                        var allIds = allIdsProp?.GetValue(null) as IEnumerable;
                        var count = allIds?.Cast<object>().Count() ?? 0;
                        catalogStatus.Add(("Storylets", count, count > 0 ? "OK" : "EMPTY"));
                    }
                }
                catch { catalogStatus.Add(("Storylets", 0, "ERROR")); }

                // Trigger Registry (Spec 0 backbone)
                try
                {
                    var triggerType = Type.GetType("Enlisted.Features.Content.TriggerRegistry, Enlisted");
                    if (triggerType != null)
                    {
                        var namesMethod = triggerType.GetMethod("AllRegisteredNames", BindingFlags.Public | BindingFlags.Static);
                        var names = namesMethod?.Invoke(null, null) as IEnumerable;
                        var count = names?.Cast<object>().Count() ?? 0;
                        catalogStatus.Add(("Triggers", count, count > 0 ? "OK" : "EMPTY"));
                    }
                }
                catch { catalogStatus.Add(("Triggers", 0, "ERROR")); }

                // Scripted Effect Registry (Spec 0 backbone)
                try
                {
                    var effectType = Type.GetType("Enlisted.Features.Content.ScriptedEffectRegistry, Enlisted");
                    if (effectType != null)
                    {
                        var allIdsProp = effectType.GetProperty("AllIds", BindingFlags.Public | BindingFlags.Static);
                        var allIds = allIdsProp?.GetValue(null) as IEnumerable;
                        var count = allIds?.Cast<object>().Count() ?? 0;
                        catalogStatus.Add(("Scripted Effects", count, count > 0 ? "OK" : "EMPTY"));
                    }
                }
                catch { catalogStatus.Add(("Scripted Effects", 0, "ERROR")); }

                // Quality Store (runtime instance — only meaningful after QualityBehavior registers)
                try
                {
                    var qualityType = Type.GetType("Enlisted.Features.Qualities.QualityStore, Enlisted");
                    if (qualityType != null)
                    {
                        var instanceProp = qualityType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                        var instance = instanceProp?.GetValue(null);
                        if (instance != null)
                        {
                            var globalValuesProp = qualityType.GetProperty("GlobalValues");
                            var globalValues = globalValuesProp?.GetValue(instance) as IDictionary;
                            var count = globalValues?.Count ?? 0;
                            catalogStatus.Add(("Quality Values", count, count >= 0 ? "OK" : "EMPTY"));
                        }
                    }
                }
                catch { catalogStatus.Add(("Quality Values", 0, "ERROR")); }

                // Flag Store (runtime instance — only meaningful after FlagBehavior registers)
                try
                {
                    var flagType = Type.GetType("Enlisted.Features.Flags.FlagStore, Enlisted");
                    if (flagType != null)
                    {
                        var instanceProp = flagType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                        var instance = instanceProp?.GetValue(null);
                        if (instance != null)
                        {
                            var globalFlagsProp = flagType.GetProperty("GlobalFlags");
                            var globalFlags = globalFlagsProp?.GetValue(instance) as IDictionary;
                            var count = globalFlags?.Count ?? 0;
                            catalogStatus.Add(("Global Flags", count, "OK"));
                        }
                    }
                }
                catch { catalogStatus.Add(("Global Flags", 0, "ERROR")); }

                // Print catalog status
                WriteLine("  Catalog               Items    Status");
                WriteLine("  -------------------  -------  --------");
                foreach (var (name, count, status) in catalogStatus)
                {
                    var countStr = count >= 0 ? count.ToString() : "N/A";
                    var statusMarker = status == "OK" || status == "PRESENT" ? "✓" :
                                      status == "EMPTY" ? "⚠" : "✗";
                    WriteLine($"  {name,-20} {countStr,7}  {statusMarker} {status}");
                }

                WriteLine();
                var hasErrors = catalogStatus.Any(c => c.status == "ERROR");
                var hasEmpty = catalogStatus.Any(c => c.status == "EMPTY");

                if (hasErrors)
                {
                    WriteLine("  [!] Some catalogs failed to load. Check logs for errors.");
                }
                else if (hasEmpty)
                {
                    WriteLine("  [!] Some catalogs are empty. Check file installation.");
                }
                else
                {
                    WriteLine("  [✓] All catalogs loaded successfully.");
                }
            }
            catch (Exception ex)
            {
                WriteLine($"  ERROR: Failed to check catalog status - {ex.Message}");
            }
        }

        #region Path Management

        /// <summary>
        ///     Resolves the path to the conflict log (Conflicts-A_*.log) in the Debugging folder alongside the module.
        /// </summary>
        private static void InitializeLogPath()
        {
            try
            {
                var assembly = typeof(ModConflictDiagnostics).Assembly;
                var assemblyLocation = assembly.Location;

                if (string.IsNullOrWhiteSpace(assemblyLocation))
                {
                    throw new InvalidOperationException("Assembly.Location is null or empty");
                }

                var dllDir = Path.GetDirectoryName(assemblyLocation);
                if (string.IsNullOrWhiteSpace(dllDir))
                {
                    throw new InvalidOperationException("Could not get directory from assembly location");
                }

                var binDir = Directory.GetParent(dllDir);
                if (binDir == null)
                {
                    throw new InvalidOperationException("Could not get parent directory");
                }

                var enlistedRoot = binDir.Parent;
                var debugDir = enlistedRoot != null
                    ? Path.Combine(enlistedRoot.FullName, "Debugging")
                    : "Debugging";

                if (!Directory.Exists(debugDir))
                {
                    _ = Directory.CreateDirectory(debugDir);
                }

                _conflictLogPath = RotateConflictLogs(debugDir);
            }
            catch (Exception ex)
            {
                // Fallback to Documents if module path resolution fails
                try
                {
                    var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    var fallbackDir = Path.Combine(docs, "Mount and Blade II Bannerlord", "Logs", "Enlisted");
                    _ = Directory.CreateDirectory(fallbackDir);
                    _conflictLogPath = RotateConflictLogs(fallbackDir);
                    System.Diagnostics.Debug.WriteLine(
                        $"[Enlisted] Conflict log using Documents fallback (Error: {ex.Message})");
                }
                catch (Exception fallbackEx)
                {
                    // Last resort: use a temp path
                    _conflictLogPath = RotateConflictLogs(Path.Combine(Path.GetTempPath(), "Enlisted"));
                    var tempDir = Path.GetDirectoryName(_conflictLogPath);
                    if (tempDir != null && !Directory.Exists(tempDir))
                    {
                        _ = Directory.CreateDirectory(tempDir);
                    }

                    System.Diagnostics.Debug.WriteLine(
                        $"[Enlisted] Conflict log using temp fallback (Error: {fallbackEx.Message})");
                }
            }
        }

        private static string RotateConflictLogs(string logDir)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(logDir))
                {
                    logDir = "Debugging";
                }

                if (!Directory.Exists(logDir))
                {
                    _ = Directory.CreateDirectory(logDir);
                }

                var utcNow = DateTime.UtcNow;

                var files = Directory.GetFiles(logDir, $"{ConflictPrefix}*.log", SearchOption.TopDirectoryOnly)
                    .Select(path => new FileInfo(path))
                    .OrderByDescending(f => f.CreationTimeUtc)
                    .ToList();

                var toShift = files.Take(ConflictSlots.Length - 1).ToList();
                var toDelete = files.Skip(ConflictSlots.Length - 1).ToList();
                foreach (var old in toDelete)
                {
                    TryDelete(old.FullName);
                }

                for (int i = 0; i < toShift.Count && i + 1 < ConflictSlots.Length; i++)
                {
                    var stamp = ExtractTimestamp(toShift[i]) ?? toShift[i].CreationTimeUtc;
                    var target = Path.Combine(logDir, $"{ConflictSlots[i + 1]}_{stamp:yyyy-MM-dd_HH-mm-ss}.log");
                    TryMove(toShift[i].FullName, target);
                }

                var newName = $"{ConflictSlots[0]}_{utcNow:yyyy-MM-dd_HH-mm-ss}.log";
                var newPath = Path.Combine(logDir, newName);

                // Touch the file so WriteLine can append later
                File.WriteAllText(newPath, string.Empty, Encoding.UTF8);

                // Update combined pointer with latest conflicts file
                ModLogger.WriteCombinedPointer(logDir, null, newName);

                return newPath;
            }
            catch
            {
                // Fallback: use a placeholder filename
                return Path.Combine(logDir ?? "Debugging", "_.log");
            }
        }

        /// <summary>
        ///     Writes a line to the conflict log file.
        /// </summary>
        private static void WriteLine(string text = "")
        {
            try
            {
                lock (Sync)
                {
                    File.AppendAllText(_conflictLogPath, text + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
                // Silent fail - logging should never crash the game
            }
        }

        #endregion

        #region Helpers for rotation

        private static DateTime? ExtractTimestamp(FileInfo file)
        {
            try
            {
                var name = Path.GetFileNameWithoutExtension(file.Name);
                if (string.IsNullOrWhiteSpace(name))
                {
                    return null;
                }

                var parts = name.Split('_');
                if (parts.Length == 0)
                {
                    return null;
                }

                var tail = parts[parts.Length - 1];
                if (DateTime.TryParseExact(tail, "yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
                {
                    return parsed;
                }
            }
            catch
            {
                // ignore parse errors
            }
            return null;
        }

        private static void TryDelete(string path)
        {
            try { File.Delete(path); } catch { /* best effort */ }
        }

        private static void TryMove(string source, string destination)
        {
            try
            {
                if (File.Exists(destination))
                {
                    File.Delete(destination);
                }
                File.Move(source, destination);
            }
            catch
            {
                // best effort
            }
        }

        #endregion

        #region Header and Footer

        private static void WriteHeader()
        {
            WriteLine("========================================================================");
            WriteLine("              ENLISTED MOD - CONFLICT DIAGNOSTICS");
            WriteLine("========================================================================");
            WriteLine();
            WriteLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            WriteLine();
            WriteLine("This file helps diagnose mod conflicts. Share it when reporting issues.");
            WriteLine("NOTE: This mod uses two Harmony instances for stability:");
            WriteLine($"  - {EnlistedHarmonyId} (patches applied at mod load)");
            WriteLine($"  - {EnlistedDeferredHarmonyId} (patches applied when campaign starts)");
            WriteLine(new string('-', 72));
        }

        private static void WriteNoteAboutDeferredPatches()
        {
            WriteLine();
            WriteLine("-- NOTE --");
            WriteLine();
            WriteLine("  Deferred patches will be added below when the campaign starts.");
            WriteLine("  These patches are delayed to avoid crashes on Linux/Proton.");
            WriteLine("  If you only see this message, load a save to see full diagnostics.");
        }

        private static void WriteFooter(bool isPartial)
        {
            WriteLine();
            WriteLine(new string('-', 72));

            if (isPartial)
            {
                WriteLine("END OF INITIAL DIAGNOSTICS (deferred patches pending)");
            }
            else
            {
                WriteLine("END OF FULL DIAGNOSTICS");
            }

            WriteLine();
            WriteLine("If you're experiencing issues:");
            WriteLine("  1. Check the POTENTIAL CONFLICTS sections above");
            WriteLine("  2. Try disabling mods listed as sharing patches with Enlisted");
            WriteLine("  3. Include this file when reporting bugs on Nexus/GitHub");
            WriteLine();
            WriteLine("For support, visit the Enlisted mod page or GitHub repository.");
        }

        #endregion

        #region Environment Info

        /// <summary>
        ///     Logs game version, mod version, and runtime info for support tickets.
        /// </summary>
        private static void WriteEnvironmentInfo()
        {
            WriteLine();
            WriteLine("-- ENVIRONMENT --");
            WriteLine();

            string gameVersion;
            try
            {
                gameVersion = TaleWorlds.Library.ApplicationVersion.FromParametersFile().ToString();
            }
            catch
            {
                gameVersion = "Unknown";
            }

            // Pull version from SessionDiagnostics to avoid duplication
            var modVersion = SessionDiagnostics.ModVersion;
            var targetVersion = SessionDiagnostics.TargetGameVersion;

            WriteLine($"  Game Version:            {gameVersion}");
            WriteLine($"  Enlisted Version:        {modVersion}");
            WriteLine($"  Minimum Required Game:   {targetVersion}");
            WriteLine($"  CLR Version:        {Environment.Version}");
            WriteLine($"  OS:                 {Environment.OSVersion}");
            WriteLine($"  64-bit Process:     {Environment.Is64BitProcess}");
        }

        /// <summary>
        ///     Logs all loaded submodules so we know what mods were active when an issue occurred.
        /// </summary>
        private static void WriteLoadedModules()
        {
            WriteLine();
            WriteLine("-- LOADED MODULES --");
            WriteLine();

            try
            {
                // 1.3.4+ API: SubModules property replaced with CollectSubModules() method
                var subModules = TaleWorlds.MountAndBlade.Module.CurrentModule?.CollectSubModules();
                if (subModules == null || subModules.Count == 0)
                {
                    WriteLine("  (No modules detected - early load stage)");
                    return;
                }

                WriteLine($"  Total: {subModules.Count} modules");
                WriteLine();

                var index = 1;
                foreach (var subModule in subModules)
                {
                    var typeName = subModule?.GetType().FullName ?? "Unknown";
                    var isEnlisted = typeName.Contains("Enlisted") ? " <-- THIS MOD" : "";
                    WriteLine($"  {index,2}. {typeName}{isEnlisted}");
                    index++;
                }
            }
            catch (Exception ex)
            {
                WriteLine($"  ERROR: Failed to enumerate modules - {ex.Message}");
            }
        }

        #endregion

        #region Module Health Check

        /// <summary>
        ///     Verifies that critical mod files and systems are properly installed.
        ///     Helps diagnose installation problems vs actual bugs when users report issues.
        ///
        ///     2026-01-03 UPDATE: Added checks for orchestrator systems:
        ///       - camp_schedule.json, orchestrator_overrides.json, routine_outcomes.json
        ///       - simulation_config.json, strategic_context_config.json
        ///       - camp_opportunities.json (in Decisions folder)
        /// </summary>
        private static void WriteModuleHealthCheck()
        {
            WriteLine();
            WriteLine("-- MODULE HEALTH CHECK --");
            WriteLine();

            var issues = new List<string>();
            var warnings = new List<string>();

            try
            {
                var enlistedModulePath = ModulePaths.ModuleRoot;

                // Show installation location for user reference
                WriteLine($"  Installation Type: {(IsWorkshopInstall(enlistedModulePath) ? "Steam Workshop" : "Manual")}");
                WriteLine($"  Module Location: {enlistedModulePath}");
                WriteLine();

                // Check Dialogue JSON files
                WriteLine("  [Dialogue System]");
                var dialoguePath = ModulePaths.GetContentPath("Dialogue");
                if (Directory.Exists(dialoguePath))
                {
                    var expectedFiles = new[] { "qm_intro.json", "qm_dialogue.json", "qm_gates.json", "qm_baggage.json" };
                    var foundCount = 0;
                    foreach (var expectedFile in expectedFiles)
                    {
                        if (File.Exists(Path.Combine(dialoguePath, expectedFile)))
                        {
                            foundCount++;
                        }
                        else
                        {
                            warnings.Add($"Missing dialogue: {expectedFile}");
                        }
                    }
                    WriteLine($"    Dialogue Files: {foundCount}/{expectedFiles.Length}");
                }
                else
                {
                    issues.Add("Dialogue directory missing");
                    WriteLine("    Dialogue Files: MISSING");
                }

                // Check Events JSON files  
                WriteLine("  [Event System]");
                var eventsPath = ModulePaths.GetContentPath("Events");
                if (Directory.Exists(eventsPath))
                {
                    var eventFiles = Directory.GetFiles(eventsPath, "*.json").Length;
                    WriteLine($"    Event Files: {eventFiles}");
                    if (eventFiles == 0)
                    {
                        warnings.Add("No event files found");
                    }
                }
                else
                {
                    issues.Add("Events directory missing");
                    WriteLine("    Event Files: MISSING");
                }

                // Check Decisions JSON files
                WriteLine("  [Decision System]");
                var decisionsPath = ModulePaths.GetContentPath("Decisions");
                if (Directory.Exists(decisionsPath))
                {
                    var decisionFiles = Directory.GetFiles(decisionsPath, "*.json").Length;
                    var expectedDecisionFiles = new[] {
                        "decisions.json", "camp_decisions.json", "camp_opportunities.json",
                        "medical_decisions.json"
                    };
                    var foundCount = 0;
                    foreach (var expectedFile in expectedDecisionFiles)
                    {
                        if (File.Exists(Path.Combine(decisionsPath, expectedFile)))
                        {
                            foundCount++;
                        }
                    }
                    WriteLine($"    Decision Files: {decisionFiles} total ({foundCount}/{expectedDecisionFiles.Length} core files)");
                    if (decisionFiles == 0)
                    {
                        warnings.Add("No decision files found");
                    }
                    else if (foundCount < expectedDecisionFiles.Length)
                    {
                        warnings.Add($"Some core decision files missing (found {foundCount}/{expectedDecisionFiles.Length})");
                    }
                }
                else
                {
                    issues.Add("Decisions directory missing");
                    WriteLine("    Decision Files: MISSING");
                }

                // Check Orders JSON files
                WriteLine("  [Order System]");
                var ordersPath = ModulePaths.GetContentPath("Orders");
                if (Directory.Exists(ordersPath))
                {
                    var orderFiles = Directory.GetFiles(ordersPath, "*.json").Length;
                    WriteLine($"    Order Files: {orderFiles}");
                    if (orderFiles == 0)
                    {
                        warnings.Add("No order files found");
                    }
                }
                else
                {
                    issues.Add("Orders directory missing");
                    WriteLine("    Order Files: MISSING");
                }

                // Check Storylets JSON files (Spec 0 backbone)
                WriteLine("  [Storylet System]");
                var storyletsPath = Path.Combine(ModulePaths.ModuleRoot, "ModuleData", "Enlisted", "Storylets");
                if (Directory.Exists(storyletsPath))
                {
                    var storyletFiles = Directory.GetFiles(storyletsPath, "*.json").Length;
                    WriteLine($"    Storylet Files: {storyletFiles}");
                    if (storyletFiles == 0)
                    {
                        warnings.Add("No storylet files found (Phase B not yet authored)");
                    }
                }
                else
                {
                    WriteLine("    Storylet Files: MISSING (Phase B directory not created)");
                }

                // Check Activities JSON files (Spec 1+2 surface configs)
                WriteLine("  [Activity System]");
                var activitiesPath = Path.Combine(ModulePaths.ModuleRoot, "ModuleData", "Enlisted", "Activities");
                if (Directory.Exists(activitiesPath))
                {
                    var activityFiles = Directory.GetFiles(activitiesPath, "*.json").Length;
                    WriteLine($"    Activity Files: {activityFiles}");
                }
                else
                {
                    WriteLine("    Activity Files: none (Spec 2 configs not yet authored)");
                }

                // Check Effects JSON files
                WriteLine("  [Effect System]");
                var effectsPath = Path.Combine(ModulePaths.ModuleRoot, "ModuleData", "Enlisted", "Effects");
                if (Directory.Exists(effectsPath))
                {
                    var effectFiles = Directory.GetFiles(effectsPath, "*.json").Length;
                    WriteLine($"    Effect Files: {effectFiles}");
                }
                else
                {
                    WriteLine("    Effect Files: none");
                }

                // Check Config JSON files
                WriteLine("  [Configuration System]");
                var configPath = ModulePaths.GetContentPath("Config");
                if (Directory.Exists(configPath))
                {
                    var expectedConfigs = new[] {
                        "settings.json", "progression_config.json", "enlisted_config.json",
                        "equipment_pricing.json", "retinue_config.json", "baggage_config.json",
                        "camp_schedule.json", "orchestrator_overrides.json", "routine_outcomes.json",
                        "simulation_config.json", "strategic_context_config.json"
                    };
                    var foundCount = 0;
                    foreach (var expectedFile in expectedConfigs)
                    {
                        if (File.Exists(Path.Combine(configPath, expectedFile)))
                        {
                            foundCount++;
                        }
                        else
                        {
                            warnings.Add($"Missing config: {expectedFile}");
                        }
                    }
                    WriteLine($"    Config Files: {foundCount}/{expectedConfigs.Length}");
                }
                else
                {
                    issues.Add("Config directory missing");
                    WriteLine("    Config Files: MISSING");
                }

                // Check Localization XML files
                WriteLine("  [Localization System]");
                var languagesPath = Path.Combine(ModulePaths.ModuleRoot, "ModuleData", "Languages");
                if (Directory.Exists(languagesPath))
                {
                    var xmlFiles = new[] { "enlisted_strings.xml", "enlisted_qm_dialogue.xml", "language_data.xml" };
                    var foundCount = 0;
                    foreach (var xmlFile in xmlFiles)
                    {
                        if (File.Exists(Path.Combine(languagesPath, xmlFile)))
                        {
                            foundCount++;
                        }
                        else
                        {
                            issues.Add($"Missing localization: {xmlFile}");
                        }
                    }
                    WriteLine($"    Localization Files: {foundCount}/{xmlFiles.Length}");
                }
                else
                {
                    issues.Add("Languages directory missing");
                    WriteLine("    Localization Files: MISSING");
                }

                // Summary
                WriteLine();
                if (issues.Count == 0 && warnings.Count == 0)
                {
                    WriteLine("  [✓] MODULE HEALTH: GOOD");
                    WriteLine("  All systems operational. Installation verified.");
                }
                else
                {
                    if (issues.Count > 0)
                    {
                        WriteLine($"  [!] CRITICAL ISSUES: {issues.Count}");
                        foreach (var issue in issues)
                        {
                            WriteLine($"      - {issue}");
                        }
                    }

                    if (warnings.Count > 0)
                    {
                        WriteLine($"  [!] WARNINGS: {warnings.Count}");
                        foreach (var warning in warnings)
                        {
                            WriteLine($"      - {warning}");
                        }
                    }

                    WriteLine();
                    WriteLine("  RECOMMENDATION: Verify game files or reinstall the mod.");
                    WriteLine("  Fallback systems may activate for missing content.");
                }
            }
            catch (Exception ex)
            {
                WriteLine($"  ERROR: Health check failed - {ex.Message}");
                issues.Add($"Health check error: {ex.Message}");
            }
        }

        /// <summary>
        ///     Detects if the mod was installed via Steam Workshop by checking for WorkshopUpdate.xml.
        /// </summary>
        private static bool IsWorkshopInstall(string modulePath)
        {
            try
            {
                // Workshop installs have WorkshopUpdate.xml in the module root
                var workshopFile = Path.Combine(modulePath, "WorkshopUpdate.xml");
                return File.Exists(workshopFile);
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Conflict Detection

        /// <summary>
        ///     Verifies that Harmony patches were applied successfully.
        ///     Helps diagnose when patches fail due to game updates or conflicts.
        /// </summary>
        private static void WritePatchApplicationHealth(Harmony harmony, string sectionTitle)
        {
            WriteLine();
            WriteLine($"-- PATCH APPLICATION STATUS ({sectionTitle}) --");
            WriteLine();

            try
            {
                var patchedMethods = harmony.GetPatchedMethods().ToList();
                WriteLine($"  Patches Applied: {patchedMethods.Count} methods");

                // Count patch types
                var prefixCount = 0;
                var postfixCount = 0;
                var transpilerCount = 0;
                var finalizerCount = 0;

                foreach (var method in patchedMethods)
                {
                    var patchInfo = Harmony.GetPatchInfo(method);
                    if (patchInfo != null)
                    {
                        prefixCount += patchInfo.Prefixes.Count(p => p.owner == EnlistedHarmonyId || p.owner == EnlistedDeferredHarmonyId);
                        postfixCount += patchInfo.Postfixes.Count(p => p.owner == EnlistedHarmonyId || p.owner == EnlistedDeferredHarmonyId);
                        transpilerCount += patchInfo.Transpilers.Count(p => p.owner == EnlistedHarmonyId || p.owner == EnlistedDeferredHarmonyId);
                        finalizerCount += patchInfo.Finalizers.Count(p => p.owner == EnlistedHarmonyId || p.owner == EnlistedDeferredHarmonyId);
                    }
                }

                WriteLine($"    Prefixes:    {prefixCount}");
                WriteLine($"    Postfixes:   {postfixCount}");
                WriteLine($"    Transpilers: {transpilerCount}");
                WriteLine($"    Finalizers:  {finalizerCount}");
                WriteLine();

                if (patchedMethods.Count == 0)
                {
                    WriteLine("  [!] WARNING: No patches applied!");
                    WriteLine("  This indicates a critical Harmony failure.");
                    WriteLine("  The mod will not function correctly.");
                }
                else
                {
                    WriteLine($"  [✓] {sectionTitle} patches applied successfully.");
                }
            }
            catch (Exception ex)
            {
                WriteLine($"  ERROR: Failed to check patch status - {ex.Message}");
            }
        }

        /// <summary>
        ///     Detects when other mods patch the same methods as Enlisted and logs potential conflicts.
        ///     This is the key diagnostic for mod interference issues.
        /// </summary>
        private static void WritePatchConflicts(Harmony harmony, string sectionTitle, string ourHarmonyId)
        {
            WriteLine();
            WriteLine($"-- HARMONY PATCH CONFLICT ANALYSIS ({sectionTitle}) --");
            WriteLine();

            try
            {
                var ourMethods = harmony.GetPatchedMethods().ToList();
                WriteLine($"  Enlisted patches ({sectionTitle.ToLower()}): {ourMethods.Count} methods");
                WriteLine();

                var conflicts = new List<(string method, string declaringType, List<string> otherMods)>();

                foreach (var method in ourMethods)
                {
                    var patchInfo = Harmony.GetPatchInfo(method);
                    if (patchInfo == null)
                    {
                        continue;
                    }

                    // Gather all mod owners that touch this method
                    var owners = new HashSet<string>();
                    foreach (var p in patchInfo.Prefixes)
                    {
                        _ = owners.Add(p.owner);
                    }

                    foreach (var p in patchInfo.Postfixes)
                    {
                        _ = owners.Add(p.owner);
                    }

                    foreach (var p in patchInfo.Transpilers)
                    {
                        _ = owners.Add(p.owner);
                    }

                    foreach (var p in patchInfo.Finalizers)
                    {
                        _ = owners.Add(p.owner);
                    }

                    // If more than just Enlisted patches this method, we have a potential conflict
                    // Check both our Harmony IDs since we use two instances
                    var ourIds = new HashSet<string> { EnlistedHarmonyId, EnlistedDeferredHarmonyId, ourHarmonyId };
                    var otherMods = owners.Where(o => !ourIds.Contains(o)).ToList();

                    if (otherMods.Count > 0)
                    {
                        var methodName = method.Name;
                        var declaringType = method.DeclaringType?.FullName ?? "Unknown";
                        conflicts.Add((methodName, declaringType, otherMods));
                    }
                }

                if (conflicts.Count == 0)
                {
                    WriteLine("  [OK] NO CONFLICTS DETECTED");
                    WriteLine();
                    WriteLine("  All Enlisted patches are exclusive - no other mods are patching");
                    WriteLine("  the same game methods. If you're having issues, the problem is");
                    WriteLine("  likely not a mod conflict.");
                }
                else
                {
                    WriteLine($"  [!] POTENTIAL CONFLICTS: {conflicts.Count}");
                    WriteLine();
                    WriteLine("  The following methods are patched by both Enlisted and other mods.");
                    WriteLine("  This doesn't always cause problems, but may explain unexpected behavior.");
                    WriteLine();

                    // Group by other mod for easier reading
                    var byMod = conflicts
                        .SelectMany(c => c.otherMods.Select(m => new { Mod = m, c.declaringType, c.method }))
                        .GroupBy(x => x.Mod)
                        .OrderBy(g => g.Key);

                    foreach (var modGroup in byMod)
                    {
                        WriteLine($"  Mod: {modGroup.Key}");
                        WriteLine($"    Shared patches: {modGroup.Count()}");
                        foreach (var patch in modGroup.Take(10)) // Limit to first 10 to avoid spam
                        {
                            WriteLine($"      - {patch.declaringType}.{patch.method}");
                        }

                        if (modGroup.Count() > 10)
                        {
                            WriteLine($"      ... and {modGroup.Count() - 10} more");
                        }

                        WriteLine();
                    }

                    // Write detailed patch order for conflicts (limited to avoid huge logs)
                    if (conflicts.Count <= 20)
                    {
                        WritePatchExecutionOrder(harmony, ourMethods, conflicts, ourHarmonyId);
                    }
                    else
                    {
                        WriteLine("  (Patch execution order skipped - too many conflicts to display)");
                        WriteLine("  Try disabling other mods one at a time to identify the source.");
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLine($"  ERROR: Conflict analysis failed - {ex.Message}");
            }
        }

        /// <summary>
        ///     Logs the execution order of patches on conflicting methods to understand who runs first.
        ///     Patch order matters: prefixes can skip the original method, postfixes can modify results.
        /// </summary>
        private static void WritePatchExecutionOrder(
            Harmony _,
            List<MethodBase> ourMethods,
            List<(string method, string declaringType, List<string> otherMods)> conflicts,
            string ourHarmonyId)
        {
            WriteLine("  -- Patch Execution Order --");
            WriteLine();
            WriteLine("  (Higher priority prefixes run first and can skip original method)");
            WriteLine("  (Lower priority postfixes run first after the original method)");
            WriteLine();

            var ourIds = new HashSet<string> { EnlistedHarmonyId, EnlistedDeferredHarmonyId, ourHarmonyId };

            foreach (var method in ourMethods)
            {
                var patchInfo = Harmony.GetPatchInfo(method);
                if (patchInfo == null)
                {
                    continue;
                }

                var methodName = method.Name;
                var declaringType = method.DeclaringType?.FullName ?? "Unknown";

                if (!conflicts.Any(c => c.method == methodName && c.declaringType == declaringType))
                {
                    continue;
                }

                WriteLine($"  {declaringType}.{methodName}:");

                if (patchInfo.Prefixes.Count > 0)
                {
                    WriteLine("    Prefixes (run before original, highest priority first):");
                    foreach (var p in patchInfo.Prefixes.OrderByDescending(x => x.priority))
                    {
                        var marker = ourIds.Contains(p.owner) ? "<- Enlisted" : "";
                        WriteLine($"      [{p.priority,4}] {p.owner} {marker}");
                    }
                }

                if (patchInfo.Postfixes.Count > 0)
                {
                    WriteLine("    Postfixes (run after original, lowest priority first):");
                    foreach (var p in patchInfo.Postfixes.OrderBy(x => x.priority))
                    {
                        var marker = ourIds.Contains(p.owner) ? "<- Enlisted" : "";
                        WriteLine($"      [{p.priority,4}] {p.owner} {marker}");
                    }
                }

                if (patchInfo.Transpilers.Count > 0)
                {
                    WriteLine("    Transpilers (modify IL code):");
                    foreach (var p in patchInfo.Transpilers)
                    {
                        var marker = ourIds.Contains(p.owner) ? "<- Enlisted" : "";
                        WriteLine($"      {p.owner} {marker}");
                    }
                }

                WriteLine();
            }
        }

        /// <summary>
        ///     Writes a combined summary of all conflicts from both Harmony instances.
        /// </summary>
        private static void WriteCombinedConflictSummary()
        {
            WriteLine();
            WriteLine("-- COMBINED CONFLICT SUMMARY --");
            WriteLine();

            try
            {
                // Gather all patched methods from both Harmony instances
                var mainHarmony = new Harmony(EnlistedHarmonyId);
                var deferredHarmony = new Harmony(EnlistedDeferredHarmonyId);

                var allMethods = mainHarmony.GetPatchedMethods()
                    .Union(deferredHarmony.GetPatchedMethods())
                    .Distinct()
                    .ToList();

                var ourIds = new HashSet<string> { EnlistedHarmonyId, EnlistedDeferredHarmonyId };
                var allOtherMods = new HashSet<string>();

                foreach (var method in allMethods)
                {
                    var patchInfo = Harmony.GetPatchInfo(method);
                    if (patchInfo == null)
                    {
                        continue;
                    }

                    foreach (var p in patchInfo.Prefixes.Where(p => !ourIds.Contains(p.owner)))
                    {
                        _ = allOtherMods.Add(p.owner);
                    }

                    foreach (var p in patchInfo.Postfixes.Where(p => !ourIds.Contains(p.owner)))
                    {
                        _ = allOtherMods.Add(p.owner);
                    }

                    foreach (var p in patchInfo.Transpilers.Where(p => !ourIds.Contains(p.owner)))
                    {
                        _ = allOtherMods.Add(p.owner);
                    }

                    foreach (var p in patchInfo.Finalizers.Where(p => !ourIds.Contains(p.owner)))
                    {
                        _ = allOtherMods.Add(p.owner);
                    }
                }

                WriteLine($"  Total Enlisted patches: {allMethods.Count} methods");
                WriteLine($"  Other mods sharing patches: {allOtherMods.Count}");
                WriteLine();

                if (allOtherMods.Count == 0)
                {
                    WriteLine("  [OK] No conflicts detected across all patches.");
                }
                else
                {
                    WriteLine("  Mods with shared patches (potential conflict sources):");
                    foreach (var mod in allOtherMods.OrderBy(m => m))
                    {
                        WriteLine($"    - {mod}");
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLine($"  ERROR: Could not generate combined summary - {ex.Message}");
            }
        }

        #endregion

        #region Patch Listing

        /// <summary>
        ///     Lists all methods that Enlisted patches, categorized by purpose.
        ///     Useful for users to understand what game behavior the mod touches.
        /// </summary>
        private static void WriteEnlistedPatchList(Harmony harmony, string label)
        {
            WriteLine();
            WriteLine($"-- ENLISTED PATCH LIST ({label}) --");
            WriteLine();

            try
            {
                var patchedMethods = harmony.GetPatchedMethods().ToList();

                // Categorize patches for easier reading
                var categories = new Dictionary<string, List<string>>
                {
                    { "Army/Party", new List<string>() },
                    { "Encounter", new List<string>() },
                    { "Kingdom/Clan", new List<string>() },
                    { "Finance", new List<string>() },
                    { "UI/Menu", new List<string>() },
                    { "Combat", new List<string>() },
                    { "Other", new List<string>() }
                };

                foreach (var method in patchedMethods.OrderBy(m => m.DeclaringType?.Name))
                {
                    var typeName = method.DeclaringType?.Name ?? "Unknown";
                    var fullName = $"{typeName}.{method.Name}";

                    // Simple categorization based on type name
                    if (typeName.Contains("Army") || typeName.Contains("Party") || typeName.Contains("Mobile"))
                    {
                        categories["Army/Party"].Add(fullName);
                    }
                    else if (typeName.Contains("Encounter") || typeName.Contains("Map"))
                    {
                        categories["Encounter"].Add(fullName);
                    }
                    else if (typeName.Contains("Kingdom") || typeName.Contains("Clan") || typeName.Contains("Relation"))
                    {
                        categories["Kingdom/Clan"].Add(fullName);
                    }
                    else if (typeName.Contains("Finance") || typeName.Contains("Gold") || typeName.Contains("Income") ||
                             typeName.Contains("Expense"))
                    {
                        categories["Finance"].Add(fullName);
                    }
                    else if (typeName.Contains("Menu") || typeName.Contains("UI") || typeName.Contains("Screen") ||
                             typeName.Contains("Message"))
                    {
                        categories["UI/Menu"].Add(fullName);
                    }
                    else if (typeName.Contains("Battle") || typeName.Contains("Combat") || typeName.Contains("Mission"))
                    {
                        categories["Combat"].Add(fullName);
                    }
                    else
                    {
                        categories["Other"].Add(fullName);
                    }
                }

                WriteLine($"  Total: {patchedMethods.Count} methods patched");
                WriteLine();

                foreach (var category in categories.Where(c => c.Value.Count > 0))
                {
                    WriteLine($"  [{category.Key}] ({category.Value.Count})");
                    foreach (var method in category.Value)
                    {
                        WriteLine($"    - {method}");
                    }

                    WriteLine();
                }
            }
            catch (Exception ex)
            {
                WriteLine($"  ERROR: Failed to list patches - {ex.Message}");
            }
        }

        #endregion
    }
}
