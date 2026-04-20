using System;

namespace Enlisted.Mod.Core.Util
{
    /// <summary>
    /// Marks a method, property, or class as being used implicitly via reflection,
    /// data binding, or other mechanisms that static analysis cannot detect.
    ///
    /// Primary use case: TaleWorlds Gauntlet UI command binding (e.g., Command.Click="ExecuteXxx")
    /// where methods are invoked through XML data binding at runtime.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Class |
        AttributeTargets.Constructor | AttributeTargets.Field,
        AllowMultiple = false,
        Inherited = false)]
    public sealed class UsedImplicitlyAttribute : Attribute
    {
        public UsedImplicitlyAttribute()
        {
        }

        public UsedImplicitlyAttribute(string reason)
        {
            _ = reason;
            // Reason parameter accepted for documentation purposes but not stored
        }
    }
}

