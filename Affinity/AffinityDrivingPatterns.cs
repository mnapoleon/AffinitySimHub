using System;
using System.Collections.Generic;

namespace Affinity
{
    public sealed class AffinitySessionRecord
    {
        public string SessionUid { get; set; }

        public string GameName { get; set; }

        public string CarModel { get; set; }

        public string TrackName { get; set; }

        public string TrackDisplayName { get; set; }

        public DateTime StartedUtc { get; set; }

        public DateTime EndedUtc { get; set; }

        public double DistanceMeters { get; set; }

        public double TimeDrivenSeconds { get; set; }
    }

    public sealed class AffinityPatternSnapshot
    {
        public bool HasSufficientData { get; set; }

        public string EmptyStateText { get; set; } = "Complete five sessions in this period to reveal your driving patterns.";

        public List<AffinityPatternObservation> Observations { get; set; } = new List<AffinityPatternObservation>();

        public List<AffinityWeekdayPattern> WeekdayBuckets { get; set; } = new List<AffinityWeekdayPattern>();

        public List<AffinityTrackShare> TrackShares { get; set; } = new List<AffinityTrackShare>();

        public AffinityPatternComparison Comparison { get; set; } = new AffinityPatternComparison();
    }

    public sealed class AffinityPatternObservation
    {
        public string Category { get; set; }

        public string Title { get; set; }

        public string Detail { get; set; }
    }

    public sealed class AffinityWeekdayPattern
    {
        public DayOfWeek DayOfWeek { get; set; }

        public string Label { get; set; }

        public double TimeDrivenSeconds { get; set; }

        public string TimeDrivenDisplay { get; set; }

        public double RelativeHeight { get; set; }
    }

    public sealed class AffinityTrackShare
    {
        public string GameName { get; set; }

        public string TrackDisplayName { get; set; }

        public string Label { get; set; }

        public double DistanceDisplay { get; set; }

        public string DistanceDisplayText { get; set; }

        public double SharePercent { get; set; }

        public double RelativeWidth { get; set; }

        public bool IsOther { get; set; }
    }

    public sealed class AffinityPatternComparison
    {
        public double CurrentDistanceMeters { get; set; }

        public string CurrentDistanceDisplay { get; set; }

        public int CurrentSessionCount { get; set; }

        public string CurrentSessionCountDisplay { get; set; }

        public double CurrentMedianDurationSeconds { get; set; }

        public string CurrentMedianDurationDisplay { get; set; }

        public double? DistancePercentChange { get; set; }

        public int? SessionCountDifference { get; set; }

        public double? MedianDurationDifferenceSeconds { get; set; }

        public string DistanceChangeDisplay { get; set; } = "Unavailable";

        public string SessionCountChangeDisplay { get; set; } = "Unavailable";

        public string MedianDurationChangeDisplay { get; set; } = "Unavailable";

        public bool HasComparison { get; set; }

        public List<AffinityPatternTrendBucket> TrendBuckets { get; set; } = new List<AffinityPatternTrendBucket>();
    }

    public sealed class AffinityPatternTrendBucket
    {
        public DateTime StartUtc { get; set; }

        public DateTime EndUtc { get; set; }

        public double DistanceMeters { get; set; }

        public int SessionCount { get; set; }

        public double MedianDurationSeconds { get; set; }
    }
}
