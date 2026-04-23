namespace Enlisted.Features.Agency
{
    public sealed class StanceSummary
    {
        public string StanceId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Body { get; set; } = string.Empty;

        public int Severity { get; set; }

        public int DaysCovered { get; set; }

        public string StoryKey { get; set; } = string.Empty;
    }
}
