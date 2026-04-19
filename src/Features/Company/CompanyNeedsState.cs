using System;
using Enlisted.Features.Logistics;
using TaleWorlds.Library;

namespace Enlisted.Features.Company
{
    /// <summary>
    /// Tracks the current state of company needs for the enlisted lord's party.
    /// Degrades daily based on activities and recovers through appropriate assignments.
    /// NOTE: Serialization is handled manually in ScheduleBehavior.SyncData()
    /// Supplies uses CompanySupplyManager for unified logistics calculation including rations.
    /// </summary>
    public class CompanyNeedsState
    {
        /// <summary>Combat readiness (0-100)</summary>
        public int Readiness { get; set; } = 60;
        
        /// <summary>
        /// Supplies level (0-100). Uses CompanySupplyManager for unified logistics tracking
        /// including rations, ammunition, repairs, and camp supplies. Falls back to stored
        /// value when manager is unavailable.
        /// </summary>
        public int Supplies
        {
            get => CompanySupplyManager.Instance?.TotalSupply ?? _suppliesFallback;
            set => _suppliesFallback = (int)MathF.Clamp(value, 0, 100);
        }
        
        /// <summary>
        /// Fallback supplies value used when CompanySupplyManager is not active.
        /// </summary>
        private int _suppliesFallback = 60;

        /// <summary>Thresholds for need levels</summary>
        public const int ExcellentThreshold = 80;
        public const int GoodThreshold = 60;
        public const int FairThreshold = 40;
        public const int PoorThreshold = 30;
        public const int CriticalThreshold = 20;

        /// <summary>
        /// Get the value of a specific need.
        /// </summary>
        public int GetNeed(CompanyNeed need)
        {
            return need switch
            {
                CompanyNeed.Readiness => Readiness,
                CompanyNeed.Supplies => Supplies,
                _ => 0
            };
        }

        /// <summary>
        /// Set the value of a specific need (clamped to 0-100).
        /// </summary>
        public void SetNeed(CompanyNeed need, int value)
        {
            value = (int)MathF.Clamp(value, 0, 100);
            
            switch (need)
            {
                case CompanyNeed.Readiness:
                    Readiness = value;
                    break;
                case CompanyNeed.Supplies:
                    Supplies = value;
                    break;
            }
        }

        /// <summary>
        /// Modify a need by a delta amount (clamped to 0-100).
        /// </summary>
        public void ModifyNeed(CompanyNeed need, int delta)
        {
            var current = GetNeed(need);
            SetNeed(need, current + delta);
        }

        /// <summary>
        /// Get the status level for a need (Excellent, Good, Fair, Poor, Critical).
        /// </summary>
        public string GetNeedStatus(CompanyNeed need)
        {
            var value = GetNeed(need);
            
            if (value >= ExcellentThreshold)
            {
                return "Excellent";
            }
            if (value >= GoodThreshold)
            {
                return "Good";
            }
            if (value >= FairThreshold)
            {
                return "Fair";
            }
            if (value >= PoorThreshold)
            {
                return "Poor";
            }
            return "Critical";
        }

        /// <summary>
        /// Check if any need is at critical level (below 20%).
        /// </summary>
        public bool HasCriticalNeeds()
        {
            return Readiness < CriticalThreshold ||
                   Supplies < CriticalThreshold;
        }

        /// <summary>
        /// Get the most critical need (lowest value).
        /// </summary>
        public CompanyNeed GetMostCriticalNeed()
        {
            var minValue = 100;
            var criticalNeed = CompanyNeed.Readiness;
            
            foreach (CompanyNeed need in Enum.GetValues(typeof(CompanyNeed)))
            {
                var value = GetNeed(need);
                if (value < minValue)
                {
                    minValue = value;
                    criticalNeed = need;
                }
            }
            
            return criticalNeed;
        }

        /// <summary>
        /// Get overall company health score (average of all needs, 0-100).
        /// </summary>
        public int GetOverallHealth()
        {
            return (Readiness + Supplies) / 2;
        }
    }
}

