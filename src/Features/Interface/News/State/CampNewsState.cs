using System;
using Enlisted.Features.Interface.News.Models;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Interface.News.State
{
    /// <summary>
    /// Persisted, save-safe state for Camp News / Daily Reports.
    /// Stores only primitives + bounded arrays so saves remain stable across mod updates.
    /// </summary>
    public sealed class CampNewsState
    {
        // Generate-once-per-day gate.
        private int _lastGeneratedDayNumber = -1;

        // Report archive (ring buffer).
        private int _archiveCapacity = 7;
        private int _archiveHeadIndex;
        private DailyReportRecord[] _archive = Array.Empty<DailyReportRecord>();

        // 30-day rolling ledger.
        private CampLifeLedger _ledger = new CampLifeLedger();

        // Baselines used to compute deltas in DailyReportSnapshot.
        // These are intentionally coarse and best-effort until Phase 4 producers grow richer facts.
        private int _lastCompanyManCount = -1;
        private int _lastCompanyWoundedCount = -1;

        // Company status fields (updated by CompanySimulationBehavior)
        public int LastFitForDuty { get; set; }
        public int LastTotalStrength { get; set; }
        public int LastNetChange { get; set; }
        public string LastPulseText { get; set; } = string.Empty;

        public int LastGeneratedDayNumber => _lastGeneratedDayNumber;
        public CampLifeLedger Ledger => _ledger;

        public void EnsureInitialized()
        {
            _archiveCapacity = Math.Max(1, Math.Min(_archiveCapacity, 30));

            if (_archive == null || _archive.Length != _archiveCapacity)
            {
                var next = new DailyReportRecord[_archiveCapacity];
                if (_archive != null)
                {
                    var copyCount = Math.Min(_archive.Length, next.Length);
                    if (copyCount > 0)
                    {
                        Array.Copy(_archive, 0, next, 0, copyCount);
                    }
                }

                _archive = next;
                _archiveHeadIndex = ClampIndex(_archiveHeadIndex, _archiveCapacity);
            }

            _ledger ??= new CampLifeLedger();
            _ledger.EnsureInitialized();
        }

        public void MarkGenerated(int dayNumber)
        {
            _lastGeneratedDayNumber = dayNumber;
        }

        public (int LastManCount, int LastWoundedCount) GetBaselineRosterCounts()
        {
            return (_lastCompanyManCount, _lastCompanyWoundedCount);
        }

        public void SetBaselineRosterCounts(int manCount, int woundedCount)
        {
            _lastCompanyManCount = manCount;
            _lastCompanyWoundedCount = woundedCount;
        }

        public void AppendReport(DailyReportRecord record)
        {
            if (record == null)
            {
                return;
            }

            EnsureInitialized();

            record.Normalize();
            _archive[_archiveHeadIndex] = record;
            _archiveHeadIndex = (_archiveHeadIndex + 1) % _archive.Length;
        }

        public DailyReportRecord TryGetReportForDay(int dayNumber)
        {
            EnsureInitialized();

            if (_archive == null || _archive.Length == 0)
            {
                return null;
            }

            for (var i = 0; i < _archive.Length; i++)
            {
                var r = _archive[i];
                if (r != null && r.HasValue && r.DayNumber == dayNumber)
                {
                    return r;
                }
            }

            return null;
        }

        public DailyReportRecord TryGetLatestReport()
        {
            EnsureInitialized();

            if (_archive == null || _archive.Length == 0)
            {
                return null;
            }

            DailyReportRecord best = null;
            for (var i = 0; i < _archive.Length; i++)
            {
                var r = _archive[i];
                if (r == null || !r.HasValue)
                {
                    continue;
                }

                if (best == null || r.DayNumber > best.DayNumber)
                {
                    best = r;
                }
            }

            return best;
        }

        public void SyncData(IDataStore dataStore)
        {
            EnsureInitialized();

            _ = dataStore.SyncData("cn_lastGeneratedDay", ref _lastGeneratedDayNumber);

            _ = dataStore.SyncData("cn_archiveCapacity", ref _archiveCapacity);
            _ = dataStore.SyncData("cn_archiveHeadIndex", ref _archiveHeadIndex);

            // Baselines for deltas.
            _ = dataStore.SyncData("cn_lastCompanyManCount", ref _lastCompanyManCount);
            _ = dataStore.SyncData("cn_lastCompanyWoundedCount", ref _lastCompanyWoundedCount);

            // Company simulation status fields
            int lastFitForDuty = LastFitForDuty;
            int lastTotalStrength = LastTotalStrength;
            int lastNetChange = LastNetChange;
            string lastPulseText = LastPulseText ?? string.Empty;

            _ = dataStore.SyncData("cn_lastFitForDuty", ref lastFitForDuty);
            _ = dataStore.SyncData("cn_lastTotalStrength", ref lastTotalStrength);
            _ = dataStore.SyncData("cn_lastNetChange", ref lastNetChange);
            _ = dataStore.SyncData("cn_lastPulseText", ref lastPulseText);

            LastFitForDuty = lastFitForDuty;
            LastTotalStrength = lastTotalStrength;
            LastNetChange = lastNetChange;
            LastPulseText = lastPulseText ?? string.Empty;

            SyncArchive(dataStore);
            SyncLedger(dataStore);

            // Safe initialization for nulls after load.
            _archive ??= Array.Empty<DailyReportRecord>();
            _ledger ??= new CampLifeLedger();

            EnsureInitialized();
        }

        private void SyncArchive(IDataStore dataStore)
        {
            EnsureInitialized();

            // Always persist exactly capacity slots for stability.
            for (var i = 0; i < _archiveCapacity; i++)
            {
                var r = _archive[i] ?? new DailyReportRecord();
                r.SyncData(dataStore, $"cn_rep_{i}");
                _archive[i] = r;
            }
        }

        private void SyncLedger(IDataStore dataStore)
        {
            _ledger ??= new CampLifeLedger();
            _ledger.EnsureInitialized();

            // Mirror the ledger fields explicitly to avoid relying on array serialization.
            // (IDataStore.SyncData requires a ref to a variable, not a property.)
            var capacity = _ledger.Capacity;
            _ = dataStore.SyncData("cn_ledgerCapacity", ref capacity);
            _ledger.Capacity = capacity;

            var headIndex = _ledger.HeadIndex;
            _ = dataStore.SyncData("cn_ledgerHeadIndex", ref headIndex);
            _ledger.HeadIndex = headIndex;

            _ledger.EnsureInitialized();

            var days = _ledger.Days ?? Array.Empty<DailyAggregate>();
            for (var i = 0; i < _ledger.Capacity; i++)
            {
                var a = i < days.Length ? days[i] : default;

                _ = dataStore.SyncData($"cn_ledger_{i}_has", ref a.HasValue);
                _ = dataStore.SyncData($"cn_ledger_{i}_day", ref a.DayNumber);
                _ = dataStore.SyncData($"cn_ledger_{i}_lost", ref a.LostToday);
                _ = dataStore.SyncData($"cn_ledger_{i}_wounded", ref a.WoundedToday);
                _ = dataStore.SyncData($"cn_ledger_{i}_sick", ref a.SickToday);
                _ = dataStore.SyncData($"cn_ledger_{i}_train", ref a.TrainingIncidentsToday);
                _ = dataStore.SyncData($"cn_ledger_{i}_dec", ref a.DecisionsResolvedToday);
                _ = dataStore.SyncData($"cn_ledger_{i}_dispatch", ref a.HighSignalDispatchesToday);

                if (i < days.Length)
                {
                    days[i] = a;
                }
            }

            // EnsureInitialized guarantees Days.Length == Capacity.
            _ledger.Days = days;
        }

        private static int ClampIndex(int idx, int capacity)
        {
            if (capacity <= 0)
            {
                return 0;
            }

            if (idx < 0)
            {
                return 0;
            }

            return idx >= capacity ? idx % capacity : idx;
        }
    }

    public sealed class DailyReportRecord
    {
        public bool HasValue;
        public int DayNumber = -1;
        public string[] Lines = Array.Empty<string>();

        public void Normalize()
        {
            if (Lines == null)
            {
                Lines = Array.Empty<string>();
            }

            for (var i = 0; i < Lines.Length; i++)
            {
                Lines[i] ??= string.Empty;
            }
        }

        public void SyncData(IDataStore dataStore, string prefix)
        {
            _ = dataStore.SyncData($"{prefix}_has", ref HasValue);
            _ = dataStore.SyncData($"{prefix}_day", ref DayNumber);

            var lineCount = Lines?.Length ?? 0;
            _ = dataStore.SyncData($"{prefix}_lineCount", ref lineCount);

            if (dataStore.IsLoading)
            {
                Lines = new string[Math.Max(0, Math.Min(lineCount, 32))];
                for (var i = 0; i < Lines.Length; i++)
                {
                    var line = string.Empty;
                    _ = dataStore.SyncData($"{prefix}_line_{i}", ref line);
                    Lines[i] = line ?? string.Empty;
                }
            }
            else
            {
                var safeLines = Lines ?? Array.Empty<string>();
                for (var i = 0; i < Math.Min(lineCount, safeLines.Length); i++)
                {
                    var line = safeLines[i] ?? string.Empty;
                    _ = dataStore.SyncData($"{prefix}_line_{i}", ref line);
                }
            }

            Normalize();
        }
    }
}


