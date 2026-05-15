using System.Collections.Generic;
using System.Linq;

namespace Affinity
{
    internal sealed class AffinitySummarySnapshot
    {
        public List<GameDistanceTab> GameTabs { get; set; } = new List<GameDistanceTab>();

        public double TotalDistanceKm { get; set; }
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
                .Select(group => new GameDistanceTab
                {
                    GameName = group.Key,
                    TotalDistanceKm = group.Sum(summary => summary.TotalDistanceKm),
                    TotalDistanceMiles = group.Sum(summary => summary.TotalDistanceMiles),
                    TotalDistanceDisplay = displayInMiles
                        ? group.Sum(summary => summary.TotalDistanceMiles)
                        : group.Sum(summary => summary.TotalDistanceKm),
                    TotalCompletedLaps = group.Sum(summary => summary.CompletedLaps),
                    TrackSummaries = group
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
                            CompletedLaps = trackGroup.Sum(summary => summary.CompletedLaps)
                        })
                        .OrderByDescending(summary => summary.DistanceDisplay)
                        .ThenBy(summary => summary.TrackName)
                        .ToList(),
                    CarSummaries = group
                        .GroupBy(summary => summary.CarModel)
                        .Select(carGroup => new CarDistanceSummary
                        {
                            CarModel = carGroup.Key,
                            DistanceKm = carGroup.Sum(summary => summary.TotalDistanceKm),
                            DistanceMiles = carGroup.Sum(summary => summary.TotalDistanceMiles),
                            DistanceDisplay = displayInMiles
                                ? carGroup.Sum(summary => summary.TotalDistanceMiles)
                                : carGroup.Sum(summary => summary.TotalDistanceKm),
                            CompletedLaps = carGroup.Sum(summary => summary.CompletedLaps)
                        })
                        .OrderByDescending(summary => summary.DistanceDisplay)
                        .ThenBy(summary => summary.CarModel)
                        .ToList()
                })
                .OrderBy(tab => tab.GameName)
                .ToList();

            return new AffinitySummarySnapshot
            {
                GameTabs = tabs,
                TotalDistanceKm = summaries.Sum(summary => summary.TotalDistanceKm)
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
                            CompletedLaps = track.CompletedLaps,
                            LastUpdatedUtc = track.LastUpdatedUtc
                        };
                    }
                }
            }
        }
    }
}
