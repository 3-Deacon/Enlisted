using System;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.InputSystem;

namespace Enlisted.Debugging.Behaviors
{
    /// <summary>
    ///     Polls modifier+letter hotkeys on the campaign TickEvent and dispatches to the
    ///     static helpers on <see cref="DebugToolsBehavior"/>. This is the input surface for
    ///     Spec 1 (Home Surface) smoke testing so Task 21 manual QA can exercise HomeActivity
    ///     without waiting for a natural settlement-entry trigger.
    ///     <para/>
    ///     Modifiers use <see cref="Input.IsKeyDown"/> (held state); letters use
    ///     <see cref="Input.IsKeyPressed"/> (edge-triggered) so a held key fires once per press.
    /// </summary>
    public sealed class DebugHotkeysBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // No persisted state — hotkey polling is entirely runtime.
        }

        private void OnTick(float dt)
        {
            try
            {
                var ctrl = Input.IsKeyDown(InputKey.LeftControl) || Input.IsKeyDown(InputKey.RightControl);
                var shift = Input.IsKeyDown(InputKey.LeftShift) || Input.IsKeyDown(InputKey.RightShift);
                if (!(ctrl && shift))
                {
                    return;
                }

                if (Input.IsKeyPressed(InputKey.H))
                {
                    DebugToolsBehavior.ForceStartHomeActivity();
                }
                else if (Input.IsKeyPressed(InputKey.E))
                {
                    DebugToolsBehavior.AdvanceHomeToEvening();
                }
                else if (Input.IsKeyPressed(InputKey.B))
                {
                    DebugToolsBehavior.AdvanceHomeToBreakCamp();
                }
                else if (Input.IsKeyPressed(InputKey.T))
                {
                    DebugToolsBehavior.DumpTriggerEvalTable();
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("HOME-DEBUG", "Debug hotkey tick failed", ex);
            }
        }
    }
}
