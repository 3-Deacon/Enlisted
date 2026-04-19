using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;
using Enlisted.Mod.Core.Logging;

namespace Enlisted.Features.Equipment.Behaviors
{
    /// <summary>
    /// Validates troop discovery across all cultures to ensure complete faction coverage.
    ///
    /// This utility class tests our troop selection system against all 8 Bannerlord cultures
    /// (including Nord and Darshi added in 1.3.4) to identify potential gaps in troop availability.
    ///
    /// IMPORTANT: Uses GetBattleTier() for tier checks to match TroopSelectionManager behavior.
    /// The Tier property returns raw XML tier, while GetBattleTier() returns calculated battle tier.
    /// </summary>
    public static class TroopDiscoveryValidator
    {
        /// <summary>
        /// Safe tier accessor matching TroopSelectionManager implementation.
        /// Uses GetBattleTier() which returns calculated battle tier, not raw XML tier.
        /// </summary>
        private static int SafeGetTier(CharacterObject troop)
        {
            try { return troop.GetBattleTier(); } catch { return 1; }
        }
        /// <summary>
        /// Validate troop availability across all cultures and tiers.
        /// Call this during development to ensure complete coverage.
        /// </summary>
        public static void ValidateAllCulturesAndTiers()
        {
            try
            {
                ModLogger.Debug("TroopDiscovery", "Validating troop coverage (all cultures)");

                // All 8 cultures: 6 main + nord and darshi (added in 1.3.4)
                var cultures = new[] { "empire", "aserai", "khuzait", "vlandia", "sturgia", "battania", "nord", "darshi" };
                var allTroops = MBObjectManager.Instance.GetObjectTypeList<CharacterObject>();

                foreach (var cultureId in cultures)
                {
                    ValidateCultureCoverage(cultureId, allTroops);
                }

                ModLogger.Debug("TroopDiscovery", "Troop coverage validation complete");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("TroopDiscovery", "Error during faction validation", ex);
            }
        }

        /// <summary>
        /// Validate troop coverage for a specific culture.
        /// Note: Nord uses Sturgian troops, Darshi uses Aserai troops (as of 1.3.4)
        /// </summary>
        private static void ValidateCultureCoverage(string cultureId, IEnumerable<CharacterObject> allTroops)
        {
            try
            {
                var troopList = allTroops?.ToList() ?? new List<CharacterObject>();

                var culture = MBObjectManager.Instance.GetObject<CultureObject>(cultureId);
                if (culture == null)
                {
                    // Nord and Darshi are secondary cultures - they may share troops with parent cultures
                    ModLogger.Warn("TroopDiscovery", $"Culture not found: {cultureId} (may use parent culture troops)");
                    return;
                }

                // Log if this is a secondary culture that shares troops
                var parentNote = cultureId == "nord" ? " (uses Sturgian troops)" :
                                 cultureId == "darshi" ? " (uses Aserai troops)" : "";

                ModLogger.Debug("TroopDiscovery", $"Validating {cultureId} culture coverage{parentNote}");

                // Check each tier (1-6) - Bannerlord has 6 troop tiers
                // Uses GetBattleTier() to match TroopSelectionManager behavior
                for (int tier = 1; tier <= 6; tier++)
                {
                    var troopsAtTier = troopList.Where(troop =>
                        troop.Culture == culture &&
                        SafeGetTier(troop) == tier &&
                        troop.IsSoldier &&
                        troop.BattleEquipments.Any()).ToList();

                    if (troopsAtTier.Count > 0)
                    {
                        ModLogger.Debug("TroopDiscovery", $"T{tier}: {troopsAtTier.Count} troops found");

                        // Show sample troop names
                        var sampleTroops = troopsAtTier.Take(3).Select(t => t.Name?.ToString() ?? "Unnamed").ToArray();
                        ModLogger.Debug("TroopDiscovery", $"Samples: {string.Join(", ", sampleTroops)}");

                        // Check formation coverage
                        ValidateFormationCoverage(troopsAtTier, tier);
                    }
                    else
                    {
                        ModLogger.Caught("TroopDiscovery", $"T{tier}: no troops found - troop selection may fail", null);
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("TroopDiscovery", $"Error validating culture {cultureId}", ex);
            }
        }

        /// <summary>
        /// Check formation coverage for troops at a specific tier.
        /// </summary>
        private static void ValidateFormationCoverage(List<CharacterObject> troopsAtTier, int tier)
        {
            try
            {
                var formationCounts = new Dictionary<FormationType, int>
                {
                    { FormationType.Infantry, 0 },
                    { FormationType.Archer, 0 },
                    { FormationType.Cavalry, 0 },
                    { FormationType.HorseArcher, 0 }
                };

                foreach (var troop in troopsAtTier)
                {
                    var formation = DetectTroopFormation(troop);
                    formationCounts[formation]++;
                }

                var formationsWithTroops = formationCounts.Count(kvp => kvp.Value > 0);
                var totalFormations = formationCounts.Count;

                if (formationsWithTroops == totalFormations)
                {
                    ModLogger.Debug("TroopDiscovery", "All 4 formations covered");
                }
                else
                {
                    ModLogger.Warn("TroopDiscovery", $"Only {formationsWithTroops}/{totalFormations} formations available");

                    // Report missing formations
                    foreach (var kvp in formationCounts.Where(x => x.Value == 0))
                    {
                        ModLogger.Warn("TroopDiscovery", $"No {kvp.Key} troops at T{tier}");
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("TroopDiscovery", $"Error validating formations at tier {tier}", ex);
            }
        }

        /// <summary>
        /// Detect formation type from troop properties (same logic as TroopSelectionManager).
        /// </summary>
        private static FormationType DetectTroopFormation(CharacterObject troop)
        {
            try
            {
                if (troop.IsRanged && troop.IsMounted)
                {
                    return FormationType.HorseArcher;
                }
                else if (troop.IsMounted)
                {
                    return FormationType.Cavalry;
                }
                else if (troop.IsRanged)
                {
                    return FormationType.Archer;
                }
                else
                {
                    return FormationType.Infantry;
                }
            }
            catch
            {
                return FormationType.Infantry;
            }
        }

        /// <summary>
        /// Test specific culture and tier to verify troop availability.
        /// Call this during development to debug specific issues.
        /// </summary>
        public static void TestSpecificCultureTier(string cultureId, int tier)
        {
            try
            {
                ModLogger.Debug("TroopDiscovery", $"Testing {cultureId} tier {tier} coverage");

                var culture = MBObjectManager.Instance.GetObject<CultureObject>(cultureId);
                var allTroops = MBObjectManager.Instance.GetObjectTypeList<CharacterObject>();

                var availableTroops = allTroops.Where(troop =>
                    troop.Culture == culture &&
                    SafeGetTier(troop) == tier &&
                    troop.IsSoldier &&
                    troop.BattleEquipments.Any()).ToList();

                if (availableTroops.Count > 0)
                {
                    ModLogger.Debug("TroopDiscovery", $"Success: {availableTroops.Count} troops found");

                    foreach (var troop in availableTroops.Take(5))
                    {
                        var formation = DetectTroopFormation(troop);
                        var equipmentCount = troop.BattleEquipments.Count();
                        ModLogger.Debug("TroopDiscovery", $"{troop.Name} ({formation}) - {equipmentCount} equipment sets");
                    }
                }
                else
                {
                    ModLogger.Caught("TroopDiscovery", $"Failure: no troops found for {cultureId} T{tier}", null);

                    // Debug info - also show raw Tier vs calculated GetBattleTier difference
                    var totalCultureTroops = allTroops.Count(t => t.Culture == culture);
                    var totalTierTroops = allTroops.Count(t => SafeGetTier(t) == tier);
                    var totalRawTierTroops = allTroops.Count(t => t.Tier == tier);
                    var totalSoldiers = allTroops.Count(t => t.IsSoldier);

                    // Log if there's a mismatch between raw Tier and GetBattleTier
                    if (totalRawTierTroops != totalTierTroops)
                    {
                        ModLogger.Debug("TroopDiscovery", $"Note: Raw Tier={totalRawTierTroops} vs GetBattleTier={totalTierTroops} troops at T{tier}");
                    }

                    ModLogger.Debug("TroopDiscovery", $"Debug: {totalCultureTroops} culture troops, {totalTierTroops} tier troops, {totalSoldiers} soldiers total");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("TroopDiscovery", $"Error testing {cultureId} T{tier}", ex);
            }
        }
    }
}
