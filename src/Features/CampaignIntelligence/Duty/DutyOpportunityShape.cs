namespace Enlisted.Features.CampaignIntelligence.Duty
{
    /// <summary>
    /// Two opportunity shapes per the design-spec opportunity-scale taxonomy.
    /// Episodic resolves inside a storylet flow; ArcScale proposes a named order
    /// through the existing OrderActivity pipeline.
    /// </summary>
    public enum DutyOpportunityShape : byte
    {
        Episodic,
        ArcScale
    }
}
