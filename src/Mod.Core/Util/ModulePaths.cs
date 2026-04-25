using System.IO;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;

namespace Enlisted.Mod.Core.Util
{
    /// <summary>
    /// Centralized path resolution for the Enlisted module.
    /// Uses ModuleHelper.GetModuleFullPath to correctly resolve paths for both
    /// manual/Nexus installs (in Modules folder) and Steam Workshop installs
    /// (in steamapps/workshop/content folder).
    /// </summary>
    public static class ModulePaths
    {
        private const string ModuleId = "Enlisted";
        private static string _cachedModulePath;
        private static string _cachedModuleDataPath;
        private static string _cachedDebuggingPath;

        /// <summary>
        /// Gets the root path of the Enlisted module.
        /// Works correctly for both manual installs and Steam Workshop.
        /// Example: "C:\...\Modules\Enlisted\" or "C:\...\workshop\content\261550\3621116083\"
        /// </summary>
        public static string ModuleRoot
        {
            get
            {
                if (_cachedModulePath == null)
                {
                    try
                    {
                        // This is the correct Bannerlord API that handles Workshop paths
                        var path = ModuleHelper.GetModuleFullPath(ModuleId);

                        // Normalize path separators for Windows
                        _cachedModulePath = path?.Replace('/', Path.DirectorySeparatorChar);

                        if (string.IsNullOrEmpty(_cachedModulePath) || !Directory.Exists(_cachedModulePath))
                        {
                            // Fallback: try to find module via BasePath (manual install only)
                            var fallback = Path.Combine(BasePath.Name, "Modules", ModuleId);
                            if (Directory.Exists(fallback))
                            {
                                _cachedModulePath = fallback + Path.DirectorySeparatorChar;
                            }
                        }
                    }
                    catch
                    {
                        // Last resort fallback
                        _cachedModulePath = Path.Combine(BasePath.Name, "Modules", ModuleId) + Path.DirectorySeparatorChar;
                    }
                }
                return _cachedModulePath ?? string.Empty;
            }
        }

        /// <summary>
        /// Gets the ModuleData/Enlisted path where content JSON files are stored.
        /// Example: "C:\...\Modules\Enlisted\ModuleData\Enlisted\"
        /// </summary>
        public static string ModuleDataPath
        {
            get
            {
                if (_cachedModuleDataPath == null)
                {
                    var root = ModuleRoot;
                    if (!string.IsNullOrEmpty(root))
                    {
                        _cachedModuleDataPath = Path.Combine(root, "ModuleData", "Enlisted");
                    }
                }
                return _cachedModuleDataPath ?? string.Empty;
            }
        }

        /// <summary>
        /// Gets the Debugging folder path for logs.
        /// Example: "C:\...\Modules\Enlisted\Debugging\"
        /// </summary>
        public static string DebuggingPath
        {
            get
            {
                if (_cachedDebuggingPath == null)
                {
                    var root = ModuleRoot;
                    if (!string.IsNullOrEmpty(root))
                    {
                        _cachedDebuggingPath = Path.Combine(root, "Debugging");
                    }
                }
                return _cachedDebuggingPath ?? string.Empty;
            }
        }

        /// <summary>
        /// Gets path to a specific folder within ModuleData/Enlisted.
        /// Example: GetContentPath("Storylets") returns "C:\...\ModuleData\Enlisted\Storylets"
        /// </summary>
        public static string GetContentPath(string subFolder)
        {
            var dataPath = ModuleDataPath;
            return !string.IsNullOrEmpty(dataPath) ? Path.Combine(dataPath, subFolder) : string.Empty;
        }

        /// <summary>
        /// Gets path to a specific config file within ModuleData/Enlisted/Config.
        /// Example: GetConfigPath("enlisted_config.json") returns full path to the config file.
        /// </summary>
        public static string GetConfigPath(string fileName)
        {
            return GetContentPath(Path.Combine("Config", fileName));
        }

        /// <summary>
        /// Clears cached paths. Call this if module paths might have changed.
        /// Normally not needed during gameplay.
        /// </summary>
        public static void ClearCache()
        {
            _cachedModulePath = null;
            _cachedModuleDataPath = null;
            _cachedDebuggingPath = null;
        }
    }
}
