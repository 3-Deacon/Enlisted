namespace Enlisted.Features.Content
{
    /// <summary>
    /// Chain continuation. Either time-delay form (Days set, When empty) or phase-target form
    /// (When set, Days ignored). Empty When + Days==0 fires synchronously on option-select.
    /// </summary>
    public sealed class ChainDecl
    {
        public string Id { get; set; } = string.Empty;
        public int Days { get; set; }
        public string When { get; set; } = string.Empty;   // e.g. "next_muster.promotion_phase"
    }
}
