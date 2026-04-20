#if BATTLE_AI
using Enlisted.Mod.Core.Logging;
using TaleWorlds.MountAndBlade;

namespace Enlisted.Features.Combat.BattleAI
{
    /// <summary>
    /// Optional SubModule for Battle AI systems.
    /// Users can disable this SubModule in the Bannerlord launcher to disable all Battle AI features.
    /// When disabled, there is no performance cost as this SubModule never initializes.
    /// </summary>
    public class BattleAISubModule : MBSubModuleBase
    {
        private const string LogCategory = "BattleAI";

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            ModLogger.Info(LogCategory, "Battle AI SubModule loaded - Advanced combat AI enabled");
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();

            // Register Battle AI mission behaviors here when implemented
            // Example:
            // MissionManager.OnMissionBehaviourCreated += RegisterBattleAIBehaviors;

            ModLogger.Info(LogCategory, "Battle AI systems initialized");
        }

        protected override void OnSubModuleUnloaded()
        {
            base.OnSubModuleUnloaded();
            ModLogger.Info(LogCategory, "Battle AI SubModule unloaded");
        }
    }
}
#endif
