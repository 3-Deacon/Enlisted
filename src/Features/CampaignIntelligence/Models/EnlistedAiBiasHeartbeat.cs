namespace Enlisted.Features.CampaignIntelligence.Models
{
    /// <summary>Throttled ModLogger facade the Plan 2 AI-bias wrappers call with a reason key and before/after scalar values whenever a bias branch fires; body is intentionally empty until Plan 2 Phase G (T19) wires the real throttled emitter.</summary>
    internal static class EnlistedAiBiasHeartbeat
    {
        public static void Record(string reason, float before, float after) { }

        public static void Record(string reason, int before, int after) { }
    }
}
