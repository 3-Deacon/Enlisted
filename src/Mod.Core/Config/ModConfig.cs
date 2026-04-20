namespace Enlisted.Mod.Core.Config
{
    /// <summary>
    /// Holds globally accessible mod configuration for easy access by behaviors/patches.
    /// Set once during module startup.
    /// </summary>
    public static class ModConfig
    {
        public static ModSettings Settings { get; set; } = new ModSettings();
    }
}


