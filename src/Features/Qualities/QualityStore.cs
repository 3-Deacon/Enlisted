using System;
using System.Collections.Generic;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace Enlisted.Features.Qualities
{
    /// <summary>
    /// Single home for typed numeric state. Stored qualities persist in GlobalValues /
    /// HeroValues. ReadThrough qualities proxy through Reader/Writer delegates on their
    /// QualityDefinition and don't persist here. Definitions are supplied by
    /// QualityCatalog at boot (JSON + C# read-through registration in QualityBehavior).
    /// </summary>
    [Serializable]
    public sealed class QualityStore
    {
        public static QualityStore Instance { get; private set; }
        internal static void SetInstance(QualityStore instance) => Instance = instance;

        public Dictionary<string, QualityValue> GlobalValues { get; set; } =
            new Dictionary<string, QualityValue>(StringComparer.OrdinalIgnoreCase);

        public Dictionary<MBGUID, Dictionary<string, QualityValue>> HeroValues { get; set; } =
            new Dictionary<MBGUID, Dictionary<string, QualityValue>>();

        [NonSerialized]
        private Dictionary<string, QualityDefinition> _defsById =
            new Dictionary<string, QualityDefinition>(StringComparer.OrdinalIgnoreCase);

        public IEnumerable<QualityDefinition> AllDefs => _defsById.Values;

        public void RegisterDefinitions(IEnumerable<QualityDefinition> defs)
        {
            _defsById = new Dictionary<string, QualityDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var def in defs)
            {
                if (def == null || string.IsNullOrEmpty(def.Id))
                {
                    continue;
                }
                _defsById[def.Id] = def;
            }
        }

        public QualityDefinition GetDefinition(string id)
        {
            _defsById.TryGetValue(id ?? string.Empty, out var def);
            return def;
        }

        public int Get(string qualityId)
        {
            var def = GetDefinition(qualityId);
            if (def == null)
            {
                ModLogger.Expected(
                    "QUALITY",
                    "missing_def_" + qualityId,
                    "Quality definition not registered",
                    new Dictionary<string, object> { { "id", qualityId } });
                return 0;
            }
            if (def.Kind == QualityKind.ReadThrough)
            {
                return def.Reader == null ? def.Default : Clamp(def.Reader(null), def.Min, def.Max);
            }
            return GlobalValues.TryGetValue(qualityId, out var v) ? v.Current : def.Default;
        }

        public int GetForHero(Hero hero, string qualityId)
        {
            if (hero == null)
            {
                return Get(qualityId);
            }
            var def = GetDefinition(qualityId);
            if (def == null)
            {
                ModLogger.Expected(
                    "QUALITY",
                    "missing_def_" + qualityId,
                    "Quality definition not registered",
                    new Dictionary<string, object> { { "id", qualityId } });
                return 0;
            }
            if (def.Kind == QualityKind.ReadThrough)
            {
                return def.Reader == null ? def.Default : Clamp(def.Reader(hero), def.Min, def.Max);
            }
            if (HeroValues.TryGetValue(hero.Id, out var map) && map.TryGetValue(qualityId, out var v))
            {
                return v.Current;
            }
            return def.Default;
        }

        public void Add(string qualityId, int delta, string reason)
        {
            var def = GetDefinition(qualityId);
            if (def == null || !def.Writable)
            {
                ModLogger.Expected(
                    "QUALITY",
                    "write_blocked_" + qualityId,
                    "Write blocked: unknown or read-only quality",
                    new Dictionary<string, object> { { "id", qualityId } });
                return;
            }
            if (def.Kind == QualityKind.ReadThrough)
            {
                def.Writer?.Invoke(null, delta, reason);
                return;
            }
            if (!GlobalValues.TryGetValue(qualityId, out var v))
            {
                v = new QualityValue { Current = def.Default };
                GlobalValues[qualityId] = v;
            }
            v.Current = Clamp(v.Current + delta, def.Min, def.Max);
            v.LastChanged = CampaignTime.Now;
        }

        public void AddForHero(Hero hero, string qualityId, int delta, string reason)
        {
            if (hero == null)
            {
                Add(qualityId, delta, reason);
                return;
            }
            var def = GetDefinition(qualityId);
            if (def == null || !def.Writable)
            {
                ModLogger.Expected(
                    "QUALITY",
                    "write_blocked_hero_" + qualityId,
                    "Write blocked: unknown or read-only quality",
                    new Dictionary<string, object> { { "id", qualityId } });
                return;
            }
            if (def.Kind == QualityKind.ReadThrough)
            {
                def.Writer?.Invoke(hero, delta, reason);
                return;
            }
            if (!HeroValues.TryGetValue(hero.Id, out var map))
            {
                map = new Dictionary<string, QualityValue>(StringComparer.OrdinalIgnoreCase);
                HeroValues[hero.Id] = map;
            }
            if (!map.TryGetValue(qualityId, out var v))
            {
                v = new QualityValue { Current = def.Default };
                map[qualityId] = v;
            }
            v.Current = Clamp(v.Current + delta, def.Min, def.Max);
            v.LastChanged = CampaignTime.Now;
        }

        public void Set(string qualityId, int value, string reason)
        {
            Add(qualityId, value - Get(qualityId), reason);
        }

        /// <summary>Called by QualityBehavior on daily tick. Applies decay rules to stored qualities.</summary>
        public void ApplyDecayTick(CampaignTime now)
        {
            foreach (var def in _defsById.Values)
            {
                if (def.Kind != QualityKind.Stored || def.Decay == null)
                {
                    continue;
                }
                ApplyDecayToMap(def, GlobalValues, now);
                foreach (var heroMap in HeroValues.Values)
                {
                    ApplyDecayToMap(def, heroMap, now);
                }
            }
        }

        private static void ApplyDecayToMap(
            QualityDefinition def,
            Dictionary<string, QualityValue> map,
            CampaignTime now)
        {
            if (!map.TryGetValue(def.Id, out var v))
            {
                return;
            }
            var daysSinceDecayed = (now - v.LastDecayed).ToDays;
            if (daysSinceDecayed < def.Decay.IntervalDays)
            {
                return;
            }
            if (def.Decay.RequireIdle)
            {
                var daysSinceChanged = (now - v.LastChanged).ToDays;
                if (daysSinceChanged < def.Decay.IntervalDays)
                {
                    return;
                }
            }
            v.Current = Clamp(v.Current + def.Decay.Amount, Math.Max(def.Decay.Floor, def.Min), def.Max);
            v.LastDecayed = now;
        }

        private static int Clamp(int value, int min, int max)
        {
            return value < min ? min : (value > max ? max : value);
        }
    }
}
