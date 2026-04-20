using System.Collections.Generic;
using Newtonsoft.Json;

namespace Enlisted.Features.Conditions
{
    public enum InjurySeverity
    {
        None = 0,
        Minor = 1,
        Moderate = 2,
        Severe = 3,
        Critical = 4
    }

    public enum IllnessSeverity
    {
        None = 0,
        Mild = 1,
        Moderate = 2,
        Severe = 3,
        Critical = 4
    }

    public enum ExhaustionLevel
    {
        None = 0,
        Tired = 1,
        Worn = 2,
        Depleted = 3,
        Broken = 4
    }

    internal sealed class ConditionDaysBySeverityJson
    {
        // Injury
        [JsonProperty("minor")] public int Minor { get; set; }
        [JsonProperty("moderate")] public int Moderate { get; set; }
        [JsonProperty("severe")] public int Severe { get; set; }
        [JsonProperty("critical")] public int Critical { get; set; }

        // Illness (mild maps to Minor)
        [JsonProperty("mild")] public int Mild { get; set; }
    }

    internal sealed class InjuryDefinitionJson
    {
        [JsonProperty("displayNameId")] public string DisplayNameId { get; set; }
        [JsonProperty("displayNameFallback")] public string DisplayNameFallback { get; set; }
        [JsonProperty("baseRecoveryDays")] public ConditionDaysBySeverityJson BaseRecoveryDays { get; set; } = new ConditionDaysBySeverityJson();
    }

    internal sealed class IllnessDefinitionJson
    {
        [JsonProperty("displayNameId")] public string DisplayNameId { get; set; }
        [JsonProperty("displayNameFallback")] public string DisplayNameFallback { get; set; }
        [JsonProperty("baseRecoveryDays")] public ConditionDaysBySeverityJson BaseRecoveryDays { get; set; } = new ConditionDaysBySeverityJson();
    }

    internal sealed class PlayerConditionDefinitionsJson
    {
        [JsonProperty("schemaVersion")] public int SchemaVersion { get; set; } = 1;

        [JsonProperty("injuries")]
        public Dictionary<string, InjuryDefinitionJson> Injuries { get; set; } =
            new Dictionary<string, InjuryDefinitionJson>();

        [JsonProperty("illnesses")]
        public Dictionary<string, IllnessDefinitionJson> Illnesses { get; set; } =
            new Dictionary<string, IllnessDefinitionJson>();
    }

    public sealed class PlayerConditionState
    {
        // Injury
        public InjurySeverity CurrentInjury { get; set; }
        public string InjuryType { get; set; } = string.Empty;
        public int InjuryDaysRemaining { get; set; }

        // Illness
        public IllnessSeverity CurrentIllness { get; set; }
        public string IllnessType { get; set; } = string.Empty;
        public int IllnessDaysRemaining { get; set; }

        // Exhaustion tracking is currently disabled by default.
        public ExhaustionLevel Exhaustion { get; set; }

        // Treatment
        public bool UnderMedicalCare { get; set; }
        public float RecoveryRateModifier { get; set; } = 1.0f;

        public bool HasInjury => CurrentInjury != InjurySeverity.None && InjuryDaysRemaining > 0;
        public bool HasIllness => CurrentIllness != IllnessSeverity.None && IllnessDaysRemaining > 0;

        /// <summary>
        /// True if player has an injury or illness requiring medical attention.
        /// Exhaustion is NOT included as it doesn't require surgeon/medical decisions.
        /// </summary>
        public bool HasAnyCondition => HasInjury || HasIllness;

        public void ClearTreatment()
        {
            UnderMedicalCare = false;
            RecoveryRateModifier = 1.0f;
        }

        public void ClearAll()
        {
            CurrentInjury = InjurySeverity.None;
            InjuryType = string.Empty;
            InjuryDaysRemaining = 0;

            CurrentIllness = IllnessSeverity.None;
            IllnessType = string.Empty;
            IllnessDaysRemaining = 0;

            Exhaustion = ExhaustionLevel.None;

            ClearTreatment();
        }
    }
}


