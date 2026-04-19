namespace Enlisted.Features.Camp.Models
{
    /// <summary>
    /// Current mood of the camp affecting opportunity selection and flavor text.
    /// Derived from recent battles, victories/defeats, and casualties.
    /// </summary>
    public enum CampMood
    {
        /// <summary>Normal operations, no special conditions.</summary>
        Routine,

        /// <summary>After victory, pay day, good fortune.</summary>
        Celebration,

        /// <summary>After defeat, casualties, deaths in company.</summary>
        Mourning,

        /// <summary>Before battle, siege, supply crisis.</summary>
        Tense
    }
}
