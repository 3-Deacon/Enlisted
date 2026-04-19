// Harmony patch to prevent crashes in PlayerEncounter.Finish() when called with invalid state.
// This can happen after siege battles when the native AI and our mod both try to clean up the encounter.
// Bug report: https://report.butr.link/03DD07

using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using Enlisted.Mod.Core;
using Enlisted.Mod.Core.Logging;

namespace Enlisted.Mod.GameAdapters.Patches;

/// <summary>
///     Safety patch for PlayerEncounter.Finish() to prevent crashes during siege battle cleanup.
///     
///     The Problem:
///     After a siege battle ends, both our mod's OnMapEventEnded and the native AiPartyThinkBehavior
///     can try to call PlayerEncounter.Finish(). This creates a race condition where:
///     1. Native AI tick decides the besieging party should change behavior
///     2. Native calls PlayerEncounter.Finish() directly
///     3. Internal encounter state (_encounteredParty, etc.) may be null or invalid
///     4. NullReferenceException crashes the game
///     
///     The Fix:
///     - Validate state before Finish() executes
///     - Catch any exceptions and clean up gracefully
///     - Log diagnostic info for future debugging
/// </summary>
[HarmonyPatch(typeof(PlayerEncounter), "Finish")]
public static class PlayerEncounterFinishSafetyPatch
{
    private const string LogCategory = "EncounterSafety";
    
    // Track if we're currently inside a Finish() call to detect re-entrancy
    private static bool _isFinishInProgress;
    
    // Track the last finish attempt for debugging (helps identify patterns)
    private static DateTime _lastFinishAttempt = DateTime.MinValue;
    private static string _lastFinishContext = "none";

    /// <summary>
    ///     Validates state before allowing PlayerEncounter.Finish() to execute.
    ///     Returns false to skip the original method if state is dangerous.
    /// </summary>
    [HarmonyPrefix]
    public static bool Prefix(bool forcePlayerOutFromSettlement)
    {
        var now = DateTime.UtcNow;
        var timeSinceLastFinish = (now - _lastFinishAttempt).TotalMilliseconds;
        _lastFinishAttempt = now;
        
        // Detect rapid successive calls (potential race condition indicator)
        var isRapidCall = timeSinceLastFinish < 100; // Within 100ms of previous call
        
        // Prevent re-entrant calls (shouldn't happen, but be safe)
        if (_isFinishInProgress)
        {
            ModLogger.Debug(LogCategory, 
                $"Blocked re-entrant Finish call (last context: {_lastFinishContext})");
            return false;
        }
        
        // Determine calling context for logging
        var context = DetermineCallingContext();
        _lastFinishContext = context;
        
        // Only apply safety measures when enlisted - don't affect normal gameplay
        if (!EnlistedActivation.IsActive)
        {
            _isFinishInProgress = true;
            return true;
        }
        
        try
        {
            // Validate Campaign exists
            if (Campaign.Current == null)
            {
                ModLogger.Debug(LogCategory, $"Finish skipped - Campaign.Current is null ({context})");
                return false;
            }
            
            // Check if PlayerEncounter.Current exists
            var current = Campaign.Current.PlayerEncounter;
            if (current == null)
            {
                // This is normal if encounter was already finished
                if (isRapidCall)
                {
                    ModLogger.Debug(LogCategory, 
                        $"Finish skipped - already cleaned up ({context}, {timeSinceLastFinish:F0}ms since last)");
                }
                return false;
            }
            
            // Validate MainParty exists (required at end of Finish method)
            var mainParty = MobileParty.MainParty;
            if (mainParty == null)
            {
                ModLogger.Warn(LogCategory, 
                    $"Finish - MainParty null, cleaning up safely ({context})");
                SafeCleanupEncounter("MainParty null");
                return false;
            }
            
            // Log rapid successive calls for debugging (potential race condition)
            if (isRapidCall)
            {
                ModLogger.Debug(LogCategory, 
                    $"Rapid Finish calls detected: {timeSinceLastFinish:F0}ms apart ({context})");
            }
            
            // All checks passed - allow original method to run
            _isFinishInProgress = true;
            
            // Log for debugging (only at Debug level to avoid spam)
            ModLogger.Debug(LogCategory, 
                $"Finish proceeding ({context}, forceOut={forcePlayerOutFromSettlement})");
            
            return true;
        }
        catch (Exception ex)
        {
            ModLogger.Caught(LogCategory, $"Error in Finish prefix ({context})", ex);
            SafeCleanupEncounter("prefix exception");
            return false;
        }
    }

    /// <summary>
    ///     Resets the in-progress flag after successful completion.
    /// </summary>
    [HarmonyPostfix]
    public static void Postfix()
    {
        _isFinishInProgress = false;
    }

    /// <summary>
    ///     Catches any exceptions from the original method and handles them gracefully.
    ///     This is the key fix - it prevents the game from crashing while still cleaning up state.
    ///     Note: Harmony requires the parameter to be named __exception (double underscore).
    /// </summary>
    [HarmonyFinalizer]
    public static Exception Finalizer(Exception __exception)
    {
        _isFinishInProgress = false;
        
        if (__exception == null)
        {
            return null;
        }
        
        // Only handle exceptions when enlisted to avoid masking unrelated bugs
        if (!EnlistedActivation.IsActive)
        {
            return __exception;
        }
        
        // Log the crash for debugging
        var exceptionType = __exception.GetType().Name;
        var message = __exception.Message;
        
        ModLogger.Surfaced("PATCH",
            "PlayerEncounter.Finish crashed while enlisted", __exception,
            LogCtx.Of("ExceptionType", exceptionType, "Message", message, "Context", _lastFinishContext));
        ModLogger.Debug(LogCategory, 
            $"Crash context: {_lastFinishContext}");
        
        // If it's a NullReferenceException (the bug we're fixing), clean up and swallow
        if (__exception is NullReferenceException)
        {
            ModLogger.Info(LogCategory, 
                "Recovering from NullReferenceException in Finish - cleaning up encounter state");
            SafeCleanupEncounter("NullRef recovery");
            
            // Swallow the exception to prevent game crash
            return null;
        }
        
        // For other exception types, log but let them propagate
        // We don't want to mask unrelated bugs
        ModLogger.Warn(LogCategory, 
            $"Non-NullRef exception in Finish - allowing to propagate: {exceptionType}");
        return __exception;
    }

    /// <summary>
    ///     Determines the calling context for logging purposes.
    ///     Helps identify whether the call came from native AI, our mod, or elsewhere.
    /// </summary>
    private static string DetermineCallingContext()
    {
        try
        {
            // Check common calling patterns
            var stackTrace = Environment.StackTrace;
            
        if (stackTrace.Contains("AiPartyThinkBehavior"))
        {
            return "NativeAI";
        }
        if (stackTrace.Contains("OnMapEventEnded"))
        {
            return "EnlistmentBattle";
        }
        if (stackTrace.Contains("NextFrameDispatcher"))
        {
            return "DeferredCleanup";
        }
        if (stackTrace.Contains("EnlistedMenuBehavior"))
        {
            return "EnlistedMenu";
        }
        if (stackTrace.Contains("EncounterGameMenuBehavior"))
        {
            return "NativeMenu";
        }
            
            return "Other";
        }
        catch
        {
            return "Unknown";
        }
    }

    /// <summary>
    ///     Safely cleans up encounter state without risking further exceptions.
    ///     Called when we detect invalid state or catch an exception.
    ///     NOTE: PlayerEncounter property is read-only, so we use the LeaveEncounter flag
    ///     to let the native system clean up on the next tick instead of direct assignment.
    /// </summary>
    private static void SafeCleanupEncounter(string reason)
    {
        try
        {
            ModLogger.Debug(LogCategory, $"SafeCleanupEncounter: {reason}");
            
            // Set LeaveEncounter flag - the native system will clean up on next tick
            // We cannot assign directly to Campaign.PlayerEncounter as it's read-only
            if (PlayerEncounter.Current != null)
            {
                PlayerEncounter.LeaveEncounter = true;
            }
            
            // Try to reset party move mode (what Finish does at line 790)
            var mainParty = MobileParty.MainParty;
            if (mainParty != null)
            {
                try
                {
                    mainParty.SetMoveModeHold();
                }
                catch
                {
                    // Ignore - party might be in invalid state
                }
            }
            
            ModLogger.Info(LogCategory, "Encounter cleanup requested via LeaveEncounter flag");
        }
        catch (Exception ex)
        {
            ModLogger.Caught(LogCategory, "SafeCleanupEncounter failed", ex);
            // Last resort - set the flag if we can
            try
            {
                PlayerEncounter.LeaveEncounter = true;
            }
            catch
            {
                // Nothing more we can do
            }
        }
    }
}

