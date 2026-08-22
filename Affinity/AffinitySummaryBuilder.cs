using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

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
        private static readonly AffinityGameProfileRegistry DefaultGameProfiles =
            AffinityGameProfileRegistry.CreateDefault();

        public static AffinitySummarySnapshot BuildSnapshot(
            AffinityDatabase database,
            bool displayInMiles,
            IReadOnlyDictionary<string, string> assettoCorsaTrackMap,
            AffinityGameProfileRegistry gameProfiles = null)
        {
            return BuildSnapshot(database, displayInMiles, assettoCorsaTrackMap, null, null, gameProfiles);
        }

        public static AffinitySummarySnapshot BuildSnapshot(
            AffinityDatabase database,
            bool displayInMiles,
            IReadOnlyDictionary<string, string> assettoCorsaTrackMap,
            Func<string, string> tryResolveGameLogoPath,
            Func<string, ImageSource> tryResolveGameLogo,
            AffinityGameProfileRegistry gameProfiles = null)
        {
            return BuildSnapshot(
                BuildDistanceSummaries(database),
                displayInMiles,
                assettoCorsaTrackMap,
                tryResolveGameLogoPath,
                tryResolveGameLogo,
                gameProfiles);
        }

        public static AffinitySummarySnapshot BuildSnapshot(
            IEnumerable<DistanceSummary> distanceSummaries,
            bool displayInMiles,
            IReadOnlyDictionary<string, string> assettoCorsaTrackMap,
            Func<string, string> tryResolveGameLogoPath = null,
            Func<string, ImageSource> tryResolveGameLogo = null,
            AffinityGameProfileRegistry gameProfiles = null)
        {
            gameProfiles = gameProfiles ?? DefaultGameProfiles;
            AffinityTrackDisplayContext trackDisplayContext = new AffinityTrackDisplayContext(assettoCorsaTrackMap);
            List<DistanceSummary> summaries = (distanceSummaries ?? Enumerable.Empty<DistanceSummary>())
                .Select(summary =>
                {
                    IAffinityGameProfile profile = gameProfiles.Resolve(summary.GameName);
                    string rawDisplay = string.IsNullOrWhiteSpace(summary.TrackDisplayName)
                        ? summary.TrackNameWithConfig
                        : summary.TrackDisplayName;
                    return new DistanceSummary
                    {
                        GameName = summary.GameName,
                        CarModel = summary.CarModel,
                        TrackName = summary.TrackName,
                        TrackNameWithConfig = summary.TrackNameWithConfig,
                        TrackDisplayName = profile.GetTrackDisplayName(rawDisplay, trackDisplayContext),
                        TotalDistanceKm = summary.TotalDistanceKm,
                        TotalDistanceMiles = summary.TotalDistanceMiles,
                        UsedTime = summary.UsedTime,
                        LastUpdatedUtc = summary.LastUpdatedUtc
                    };
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
                    List<TrackDistanceSummary> trackSummaries = BuildTrackSummaries(gameRows, displayInMiles, gameProfiles);
                    List<CarDistanceSummary> carSummaries = BuildCarSummaries(gameRows, displayInMiles);

                    GameDistanceTab tab = new GameDistanceTab
                    {
                        GameName = group.Key,
                        GameLogoPath = tryResolveGameLogoPath?.Invoke(group.Key) ?? string.Empty,
                        GameLogo = tryResolveGameLogo?.Invoke(group.Key),
                        DisplayInMiles = displayInMiles,
                        GameProfiles = gameProfiles,
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
                .Select(trackGroup => BuildTrackDistanceSummary(
                    trackGroup.Key.GameName,
                    trackGroup.Key.TrackNameWithConfig,
                    trackGroup.First().TrackDisplayName,
                    trackGroup.Sum(summary => summary.TotalDistanceKm),
                    trackGroup.Sum(summary => summary.TotalDistanceMiles),
                    trackGroup.Sum(summary => summary.UsedTime),
                    trackGroup.Max(summary => summary.LastUpdatedUtc),
                    displayInMiles,
                    gameProfiles.Resolve(trackGroup.Key.GameName)))
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
            bool displayInMiles,
            AffinityGameProfileRegistry gameProfiles = null)
        {
            gameProfiles = gameProfiles ?? DefaultGameProfiles;
            return (summaries ?? Enumerable.Empty<DistanceSummary>())
                .GroupBy(summary => summary.TrackNameWithConfig)
                .Select(trackGroup => BuildTrackDistanceSummary(
                    trackGroup.First().GameName,
                    trackGroup.Key,
                    trackGroup.First().TrackDisplayName,
                    trackGroup.Sum(summary => summary.TotalDistanceKm),
                    trackGroup.Sum(summary => summary.TotalDistanceMiles),
                    trackGroup.Sum(summary => summary.UsedTime),
                    trackGroup.Max(summary => summary.LastUpdatedUtc),
                    displayInMiles,
                    gameProfiles.Resolve(trackGroup.First().GameName)))
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

        private static TrackDistanceSummary BuildTrackDistanceSummary(
            string gameName,
            string trackName,
            string trackDisplayName,
            double distanceKm,
            double distanceMiles,
            double usedTime,
            DateTime lastUpdatedUtc,
            bool displayInMiles,
            IAffinityGameProfile profile)
        {
            CircuitDisplayParts circuitDisplay = profile.GetCircuitDisplayParts(trackDisplayName);

            return new TrackDistanceSummary
            {
                GameName = gameName,
                TrackName = trackName,
                TrackDisplayName = trackDisplayName,
                CircuitNameDisplay = circuitDisplay.CircuitNameDisplay,
                CircuitLayoutDisplay = circuitDisplay.CircuitLayoutDisplay,
                DistanceKm = distanceKm,
                DistanceMiles = distanceMiles,
                DistanceDisplay = displayInMiles ? distanceMiles : distanceKm,
                UsedTime = usedTime,
                UsedTimeDisplay = FormatUsedTime(usedTime),
                LastUpdatedUtc = lastUpdatedUtc
            };
        }

        private static string FormatUsedTime(double usedTimeSeconds)
        {
            TimeSpan duration = TimeSpan.FromSeconds(System.Math.Max(0.0, usedTimeSeconds));
            int totalHours = (int)duration.TotalHours;
            return $"{totalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
        }
    }
}
