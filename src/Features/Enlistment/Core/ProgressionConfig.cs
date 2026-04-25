using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Enlisted.Features.Enlistment.Core
{
    /// <summary>
    /// Configuration data structure for tier progression.
    /// Maps directly to progression_config.json for complete JSON-driven configuration.
    /// Supports 9 tiers (T1-6 enlisted, T7-9 officer track) with culture-specific rank names.
    /// </summary>
    [Serializable]
    public class ProgressionConfig
    {
        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; } = 2;

        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonProperty("tier_progression")]
        public TierProgressionConfig TierProgression { get; set; } = new TierProgressionConfig();

        [JsonProperty("culture_ranks")]
        public Dictionary<string, CultureRankConfig> CultureRanks { get; set; } =
            new Dictionary<string, CultureRankConfig>(StringComparer.OrdinalIgnoreCase);

        [JsonProperty("xp_sources")]
        public XpSourcesConfig XpSources { get; set; } = new XpSourcesConfig();
    }

    [Serializable]
    public class XpSourcesConfig
    {
        [JsonProperty("daily_base")]
        public int DailyBase { get; set; } = 25;

        [JsonProperty("battle_participation")]
        public int BattleParticipation { get; set; } = 25;

        [JsonProperty("xp_per_kill")]
        public int XpPerKill { get; set; } = 2;
    }

    [Serializable]
    public class TierProgressionConfig
    {
        [JsonProperty("requirements")]
        public List<TierRequirement> Requirements { get; set; } = new List<TierRequirement>();
    }

    [Serializable]
    public class TierRequirement
    {
        [JsonProperty("tier")]
        public int Tier { get; set; }

        [JsonProperty("xp_required")]
        public int XpRequired { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("duration")]
        public string Duration { get; set; }
    }

    /// <summary>
    /// Culture-specific rank configuration.
    /// Each culture has a style description and a list of rank names for tiers 1-9.
    /// </summary>
    [Serializable]
    public class CultureRankConfig
    {
        [JsonProperty("style")]
        public string Style { get; set; }

        [JsonProperty("ranks")]
        public List<string> Ranks { get; set; } = new List<string>();
    }
}
