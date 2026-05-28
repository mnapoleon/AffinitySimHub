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
            List<DistanceSummary> summaries = BuildDistanceSummaries(database)
                .OrderBy(summary => summary.GameName)
                .ThenBy(summary => summary.CarModel)
                .ThenBy(summary => summary.TrackNameWithConfig)
                .ToList();

            List<GameDistanceTab> tabs = summaries
                .GroupBy(summary => summary.GameName)
                .Select(group =>
                {
                    List<TrackDistanceSummary> trackSummaries = group
                        .GroupBy(summary => summary.TrackNameWithConfig)
                        .Select(trackGroup => new TrackDistanceSummary
                        {
                            TrackName = trackGroup.Key,
                            TrackDisplayName = AffinityGameLogic.GetDisplayTrackNameWithConfig(group.Key, trackGroup.Key, assettoCorsaTrackMap),
                            DistanceKm = trackGroup.Sum(summary => summary.TotalDistanceKm),
                            DistanceMiles = trackGroup.Sum(summary => summary.TotalDistanceMiles),
                            DistanceDisplay = displayInMiles
                                ? trackGroup.Sum(summary => summary.TotalDistanceMiles)
                                : trackGroup.Sum(summary => summary.TotalDistanceKm),
                            UsedTime = trackGroup.Sum(summary => summary.UsedTime),
                            UsedTimeDisplay = FormatUsedTime(trackGroup.Sum(summary => summary.UsedTime))
                        })
                        .OrderByDescending(summary => summary.DistanceDisplay)
                        .ThenBy(summary => summary.TrackName)
                        .ToList();

                    List<CarDistanceSummary> carSummaries = group
                        .GroupBy(summary => summary.CarModel)
                        .Select(carGroup => new CarDistanceSummary
                        {
                            CarModel = carGroup.Key,
                            DistanceKm = carGroup.Sum(summary => summary.TotalDistanceKm),
                            DistanceMiles = carGroup.Sum(summary => summary.TotalDistanceMiles),
                            DistanceDisplay = displayInMiles
                                ? carGroup.Sum(summary => summary.TotalDistanceMiles)
                                : carGroup.Sum(summary => summary.TotalDistanceKm),
                            UsedTime = carGroup.Sum(summary => summary.UsedTime),
                            UsedTimeDisplay = FormatUsedTime(carGroup.Sum(summary => summary.UsedTime))
                        })
                        .OrderByDescending(summary => summary.DistanceDisplay)
                        .ThenBy(summary => summary.CarModel)
                        .ToList();

                    return new GameDistanceTab
                    {
                        GameName = group.Key,
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
                        CarSummaries = carSummaries
                    };
                })
                .OrderBy(tab => tab.GameName)
                .ToList();

            return new AffinitySummarySnapshot
            {
                GameTabs = tabs,
                TotalDistanceKm = summaries.Sum(summary => summary.TotalDistanceKm),
                TotalUsedTime = summaries.Sum(summary => summary.UsedTime)
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
                            TotalDistanceKm = track.TotalDistanceMeters / MetersPerKilometer,
                            TotalDistanceMiles = track.TotalDistanceMeters / MetersPerMile,
                            UsedTime = track.UsedTime,
                            LastUpdatedUtc = track.LastUpdatedUtc
                        };
                    }
                }
            }
        }

        private static string FormatUsedTime(double usedTimeSeconds)
        {
            TimeSpan duration = TimeSpan.FromSeconds(System.Math.Max(0.0, usedTimeSeconds));
            int totalHours = (int)duration.TotalHours;
            return $"{totalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
        }
    }
}
