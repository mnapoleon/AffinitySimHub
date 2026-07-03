using System;
using System.Collections.Generic;
using System.Linq;

namespace Affinity
{
    internal sealed class AffinitySummarySnapshot
    {
        public List<GameDistanceTab> GameTabs { get; set; } = new List<GameDistanceTab>();

        public double TotalDistanceKm { get; set; }

        public double TotalUsedTime { get; set; }

        public GameDistanceTab FeaturedGameTab { get; set; }

        public TrackDistanceSummary FeaturedTrackSummary { get; set; }

        public CarDistanceSummary FeaturedCarSummary { get; set; }
    }

    internal static class AffinitySummaryBuilder
    {
        private const double MetersPerKilometer = 1000.0;
        private const double MetersPerMile = 1609.344;

        public static AffinitySummarySnapshot BuildSnapshot(
            AffinityDatabase database,
            bool displayInMiles,
            IReadOnlyDictionary<string, string> assettoCorsaTrackMap)
        {
            return BuildSnapshot(BuildDistanceSummaries(database), displayInMiles, assettoCorsaTrackMap);
        }

        public static AffinitySummarySnapshot BuildSnapshot(
            IEnumerable<DistanceSummary> distanceSummaries,
            bool displayInMiles,
            IReadOnlyDictionary<string, string> assettoCorsaTrackMap)
        {
            List<DistanceSummary> summaries = (distanceSummaries ?? Enumerable.Empty<DistanceSummary>())
                .Select(summary => new DistanceSummary
                {
                    GameName = summary.GameName,
                    CarModel = summary.CarModel,
                    TrackName = summary.TrackName,
                    TrackNameWithConfig = summary.TrackNameWithConfig,
                    TrackDisplayName = AffinityGameLogic.GetDisplayTrackNameWithConfig(
                        summary.GameName,
                        string.IsNullOrWhiteSpace(summary.TrackDisplayName) ? summary.TrackNameWithConfig : summary.TrackDisplayName,
                        assettoCorsaTrackMap),
                    TotalDistanceKm = summary.TotalDistanceKm,
                    TotalDistanceMiles = summary.TotalDistanceMiles,
                    UsedTime = summary.UsedTime,
                    LastUpdatedUtc = summary.LastUpdatedUtc
                })
                .OrderBy(summary => summary.GameName)
                .ThenBy(summary => summary.CarModel)
                .ThenBy(summary => summary.TrackNameWithConfig)
                .ToList();

            List<GameDistanceTab> tabs = summaries
                .GroupBy(summary => summary.GameName)
                .Select(group =>
                {
                    List<DistanceSummary> gameRows = group.ToList();
                    List<TrackDistanceSummary> trackSummaries = BuildTrackSummaries(gameRows, displayInMiles);
                    List<CarDistanceSummary> carSummaries = BuildCarSummaries(gameRows, displayInMiles);

                    GameDistanceTab tab = new GameDistanceTab
                    {
                        GameName = group.Key,
                        DisplayInMiles = displayInMiles,
                        TotalDistanceKm = group.Sum(summary => summary.TotalDistanceKm),
                        TotalDistanceMiles = group.Sum(summary => summary.TotalDistanceMiles),
                        TotalDistanceDisplay = displayInMiles
                            ? group.Sum(summary => summary.TotalDistanceMiles)
                            : group.Sum(summary => summary.TotalDistanceKm),
                        TotalUsedTime = group.Sum(summary => summary.UsedTime),
                        TotalUsedTimeDisplay = FormatUsedTime(group.Sum(summary => summary.UsedTime)),
                        TopTrackSummary = trackSummaries.FirstOrDefault(),
                        TopCarSummary = carSummaries.FirstOrDefault(),
                        TrackSummaries = trackSummaries,
                        CarSummaries = carSummaries,
                        RawSummaries = gameRows
                    };

                    tab.InitializeVisibleSummaries();
                    return tab;
                })
                .OrderBy(tab => tab.GameName)
                .ToList();

            GameDistanceTab featuredGameTab = tabs
                .OrderByDescending(tab => tab.TotalDistanceKm)
                .ThenByDescending(tab => tab.TotalUsedTime)
                .ThenBy(tab => tab.GameName)
                .FirstOrDefault();

            TrackDistanceSummary featuredTrackSummary = summaries
                .GroupBy(summary => new { summary.GameName, summary.TrackNameWithConfig })
                .Select(trackGroup => new TrackDistanceSummary
                {
                    GameName = trackGroup.Key.GameName,
                    TrackName = trackGroup.Key.TrackNameWithConfig,
                    TrackDisplayName = trackGroup.First().TrackDisplayName,
                    DistanceKm = trackGroup.Sum(summary => summary.TotalDistanceKm),
                    DistanceMiles = trackGroup.Sum(summary => summary.TotalDistanceMiles),
                    DistanceDisplay = displayInMiles
                        ? trackGroup.Sum(summary => summary.TotalDistanceMiles)
                        : trackGroup.Sum(summary => summary.TotalDistanceKm),
                    UsedTime = trackGroup.Sum(summary => summary.UsedTime),
                    UsedTimeDisplay = FormatUsedTime(trackGroup.Sum(summary => summary.UsedTime)),
                    LastUpdatedUtc = trackGroup.Max(summary => summary.LastUpdatedUtc)
                })
                .OrderByDescending(summary => summary.DistanceKm)
                .ThenByDescending(summary => summary.UsedTime)
                .ThenBy(summary => summary.GameName)
                .ThenBy(summary => summary.TrackDisplayName)
                .FirstOrDefault();

            CarDistanceSummary featuredCarSummary = summaries
                .GroupBy(summary => new { summary.GameName, summary.CarModel })
                .Select(carGroup => new CarDistanceSummary
                {
                    GameName = carGroup.Key.GameName,
                    CarModel = carGroup.Key.CarModel,
                    DistanceKm = carGroup.Sum(summary => summary.TotalDistanceKm),
                    DistanceMiles = carGroup.Sum(summary => summary.TotalDistanceMiles),
                    DistanceDisplay = displayInMiles
                        ? carGroup.Sum(summary => summary.TotalDistanceMiles)
                        : carGroup.Sum(summary => summary.TotalDistanceKm),
                    UsedTime = carGroup.Sum(summary => summary.UsedTime),
                    UsedTimeDisplay = FormatUsedTime(carGroup.Sum(summary => summary.UsedTime)),
                    LastUpdatedUtc = carGroup.Max(summary => summary.LastUpdatedUtc)
                })
                .OrderByDescending(summary => summary.DistanceKm)
                .ThenByDescending(summary => summary.UsedTime)
                .ThenBy(summary => summary.GameName)
                .ThenBy(summary => summary.CarModel)
                .FirstOrDefault();

            return new AffinitySummarySnapshot
            {
                GameTabs = tabs,
                TotalDistanceKm = summaries.Sum(summary => summary.TotalDistanceKm),
                TotalUsedTime = summaries.Sum(summary => summary.UsedTime),
                FeaturedGameTab = featuredGameTab,
                FeaturedTrackSummary = featuredTrackSummary,
                FeaturedCarSummary = featuredCarSummary
            };
        }

        public static IEnumerable<DistanceSummary> BuildDistanceSummaries(AffinityDatabase database)
        {
            if (database?.Games == null)
            {
                yield break;
            }

            foreach (KeyValuePair<string, GameBucket> gameEntry in database.Games)
            {
                if (gameEntry.Value?.Cars == null)
                {
                    continue;
                }

                foreach (KeyValuePair<string, CarBucket> carEntry in gameEntry.Value.Cars)
                {
                    if (carEntry.Value?.Tracks == null)
                    {
                        continue;
                    }

                    foreach (KeyValuePair<string, TrackBucket> trackEntry in carEntry.Value.Tracks)
                    {
                        TrackBucket track = trackEntry.Value;
                        if (track == null)
                        {
                            continue;
                        }

                        yield return new DistanceSummary
                        {
                            GameName = gameEntry.Key,
                            CarModel = carEntry.Key,
                            TrackName = track.TrackName,
                            TrackNameWithConfig = track.TrackNameWithConfig,
                            TrackDisplayName = track.TrackNameWithConfig,
                            TotalDistanceKm = track.TotalDistanceMeters / MetersPerKilometer,
                            TotalDistanceMiles = track.TotalDistanceMeters / MetersPerMile,
                            UsedTime = track.UsedTime,
                            LastUpdatedUtc = track.LastUpdatedUtc
                        };
                    }
                }
            }
        }

        internal static List<TrackDistanceSummary> BuildTrackSummaries(
            IEnumerable<DistanceSummary> summaries,
            bool displayInMiles)
        {
            return (summaries ?? Enumerable.Empty<DistanceSummary>())
                .GroupBy(summary => summary.TrackNameWithConfig)
                .Select(trackGroup => new TrackDistanceSummary
                {
                    GameName = trackGroup.First().GameName,
                    TrackName = trackGroup.Key,
                    TrackDisplayName = trackGroup.First().TrackDisplayName,
                    DistanceKm = trackGroup.Sum(summary => summary.TotalDistanceKm),
                    DistanceMiles = trackGroup.Sum(summary => summary.TotalDistanceMiles),
                    DistanceDisplay = displayInMiles
                        ? trackGroup.Sum(summary => summary.TotalDistanceMiles)
                        : trackGroup.Sum(summary => summary.TotalDistanceKm),
                    UsedTime = trackGroup.Sum(summary => summary.UsedTime),
                    UsedTimeDisplay = FormatUsedTime(trackGroup.Sum(summary => summary.UsedTime)),
                    LastUpdatedUtc = trackGroup.Max(summary => summary.LastUpdatedUtc)
                })
                .OrderByDescending(summary => summary.DistanceDisplay)
                .ThenBy(summary => summary.TrackDisplayName)
                .ToList();
        }

        internal static List<CarDistanceSummary> BuildCarSummaries(
            IEnumerable<DistanceSummary> summaries,
            bool displayInMiles)
        {
            return (summaries ?? Enumerable.Empty<DistanceSummary>())
                .GroupBy(summary => summary.CarModel)
                .Select(carGroup => new CarDistanceSummary
                {
                    GameName = carGroup.First().GameName,
                    CarModel = carGroup.Key,
                    DistanceKm = carGroup.Sum(summary => summary.TotalDistanceKm),
                    DistanceMiles = carGroup.Sum(summary => summary.TotalDistanceMiles),
                    DistanceDisplay = displayInMiles
                        ? carGroup.Sum(summary => summary.TotalDistanceMiles)
                        : carGroup.Sum(summary => summary.TotalDistanceKm),
                    UsedTime = carGroup.Sum(summary => summary.UsedTime),
                    UsedTimeDisplay = FormatUsedTime(carGroup.Sum(summary => summary.UsedTime)),
                    LastUpdatedUtc = carGroup.Max(summary => summary.LastUpdatedUtc)
                })
                .OrderByDescending(summary => summary.DistanceDisplay)
                .ThenBy(summary => summary.CarModel)
                .ToList();
        }

        private static string FormatUsedTime(double usedTimeSeconds)
        {
            TimeSpan duration = TimeSpan.FromSeconds(System.Math.Max(0.0, usedTimeSeconds));
            int totalHours = (int)duration.TotalHours;
            return $"{totalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
        }
    }
}
