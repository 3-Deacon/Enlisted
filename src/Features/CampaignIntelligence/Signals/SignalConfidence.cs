namespace Enlisted.Features.CampaignIntelligence.Signals
{
    /// <summary>
    /// How much reliable signal the source has. Low = rumor-heavy
    /// speculation; High = confirmed observation or received order.
    /// </summary>
    public enum SignalConfidence : byte
    {
        Low,
        Medium,
        High
    }
}
