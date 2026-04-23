using System.Collections.Generic;
using System.Linq;

namespace Enlisted.Features.Agency
{
    public sealed class StanceSummaryAccumulator
    {
        private readonly List<string> _lines = new List<string>();

        public string StanceId { get; private set; } = string.Empty;

        public int DaysCovered { get; private set; }

        public bool CanFlush => DaysCovered >= 2 && _lines.Count > 0;

        public void Add(string stanceId, string line, int daysCovered)
        {
            StanceId = stanceId ?? string.Empty;
            DaysCovered += daysCovered;

            if (!string.IsNullOrWhiteSpace(line))
            {
                _lines.Add(line.Trim());
            }
        }

        public StanceSummary Flush(int currentDay)
        {
            var body = string.Join(" ", _lines.Take(3));
            var summary = new StanceSummary
            {
                StanceId = StanceId,
                Title = "Service routine",
                Body = body,
                Severity = 0,
                DaysCovered = DaysCovered,
                StoryKey = $"stance:{StanceId}:{currentDay}"
            };

            _lines.Clear();
            DaysCovered = 0;
            return summary;
        }
    }
}
