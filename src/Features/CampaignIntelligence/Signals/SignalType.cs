using System;

namespace Enlisted.Features.CampaignIntelligence.Signals
{
    /// <summary>
    /// The ten signal families this layer projects. Each family pairs with a
    /// floor_&lt;family&gt;.json storylet pool. Rendering in the news feed
    /// varies by family (header text, icon, voice).
    /// </summary>
    public enum SignalType : byte
    {
        Rumor,
        CampTalk,
        Dispatch,
        OrderShift,
        QuartermasterWarning,
        ScoutReturn,
        PrisonerReport,
        AftermathNotice,
        TensionIncident,
        SilenceWithImplication
    }
}
