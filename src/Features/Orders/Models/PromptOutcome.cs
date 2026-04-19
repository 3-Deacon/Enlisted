namespace Enlisted.Features.Orders.Models
{
    /// <summary>
    /// Pre-rolled outcomes for Order Prompt investigations.
    /// Determined BEFORE player sees the prompt (CK3-style hidden setup).
    /// </summary>
    public enum PromptOutcome
    {
        /// <summary>
        /// 50% - False alarm, nothing interesting found. May cost fatigue.
        /// </summary>
        Nothing,

        /// <summary>
        /// 30% - Small reward: gold, supplies, reputation, skill XP.
        /// </summary>
        SmallReward,

        /// <summary>
        /// 15% - Triggers a multi-phase event chain.
        /// </summary>
        EventChain,

        /// <summary>
        /// 5% - Danger: ambush, injury, or combat encounter.
        /// </summary>
        Danger
    }
}
