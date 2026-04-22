namespace Enlisted.Features.CampaignIntelligence.Models
{
    /// <summary>Throttled ModLogger facade the Plan 2 AI-bias wrappers call whenever a bias branch fires; the body is intentionally empty until Plan 2 Phase G (T19) wires the real throttled emitter.</summary>
    internal static class EnlistedAiBiasHeartbeat
    {
        public static void Record(string category, string detail) { }
    }
}
