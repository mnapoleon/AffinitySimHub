using System;
using System.Collections.Generic;

namespace Affinity
{
    public class AffinityDatabase
    {
        public Dictionary<string, GameBucket> Games { get; set; } = new Dictionary<string, GameBucket>(StringComparer.OrdinalIgnoreCase);
    }

    public class GameBucket
    {
        public Dictionary<string, CarBucket> Cars { get; set; } = new Dictionary<string, CarBucket>(StringComparer.OrdinalIgnoreCase);
    }

    public class CarBucket
    {
        public Dictionary<string, TrackBucket> Tracks { get; set; } = new Dictionary<string, TrackBucket>(StringComparer.OrdinalIgnoreCase);
    }

    public class TrackBucket
    {
        public string GameName { get; set; } = string.Empty;

        public string CarModel { get; set; } = string.Empty;

        public string TrackName { get; set; } = string.Empty;

        public string TrackNameWithConfig { get; set; } = string.Empty;

        public double TotalDistanceMeters { get; set; }

        public double UsedTime { get; set; }

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
    }

    public class DistanceSummary
    {
        public string GameName { get; set; } = string.Empty;

        public string CarModel { get; set; } = string.Empty;

        public string TrackName { get; set; } = string.Empty;

        public string TrackNameWithConfig { get; set; } = string.Empty;

        public double TotalDistanceKm { get; set; }

        public double TotalDistanceMiles { get; set; }

        public double UsedTime { get; set; }

        public DateTime LastUpdatedUtc { get; set; }
    }

    public class TrackDistanceSummary
    {
        public string GameName { get; set; } = string.Empty;

        public string TrackName { get; set; } = string.Empty;

        public string TrackDisplayName { get; set; } = string.Empty;

        public double DistanceKm { get; set; }

        public double DistanceMiles { get; set; }

        public double DistanceDisplay { get; set; }

        public double UsedTime { get; set; }

        public string UsedTimeDisplay { get; set; } = string.Empty;
    }

    public class CarDistanceSummary
    {
        public string GameName { get; set; } = string.Empty;

        public string CarModel { get; set; } = string.Empty;

        public double DistanceKm { get; set; }

        public double DistanceMiles { get; set; }

        public double DistanceDisplay { get; set; }

        public double UsedTime { get; set; }

        public string UsedTimeDisplay { get; set; } = string.Empty;
    }

    public class GameDistanceTab
    {
        public string Header => GameName;

        public string GameName { get; set; } = string.Empty;

        public double TotalDistanceKm { get; set; }

        public double TotalDistanceMiles { get; set; }

        public double TotalDistanceDisplay { get; set; }

        public double TotalUsedTime { get; set; }

        public string TotalUsedTimeDisplay { get; set; } = string.Empty;

        public TrackDistanceSummary TopTrackSummary { get; set; }

        public CarDistanceSummary TopCarSummary { get; set; }

        public List<TrackDistanceSummary> TrackSummaries { get; set; } = new List<TrackDistanceSummary>();

        public List<CarDistanceSummary> CarSummaries { get; set; } = new List<CarDistanceSummary>();

        public override string ToString()
        {
            return GameName;
        }
    }

    public class AffinityOverviewTab
    {
        public string Header => "Affinity";
    }

    public class AffinityTopSummarySection
    {
        public string Header { get; set; } = string.Empty;

        public GameDistanceTab FeaturedGameTab { get; set; }

        public TrackDistanceSummary FeaturedTrackSummary { get; set; }

        public CarDistanceSummary FeaturedCarSummary { get; set; }
    }

    public class AffinitySettingsTab
    {
        public string Header => "Settings";
    }
}
