namespace Enlisted.Features.Escalation
{
    /// <summary>
    /// Threshold constants for determining "states" and which threshold events become eligible.
    /// These values mirror the Phase 4 research docs and are intentionally centralized.
    /// </summary>
    public static class EscalationThresholds
    {
        // Scrutiny thresholds (0-100 scale)
        public const int ScrutinyWarning = 20;
        public const int ScrutinyShakedown = 40;
        public const int ScrutinyAudit = 60;
        public const int ScrutinyExposed = 80;
        public const int ScrutinyCritical = 100;

        // Medical risk thresholds
        public const int MedicalWorsening = 3;
        public const int MedicalComplication = 4;
        public const int MedicalEmergency = 5;
    }
}


