using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Affinity
{
    internal static class AffinityPatternBuilder
    {
        internal const int MinimumCurrentSessions = 5;
        internal const int MinimumPreviousSessions = 3;
        internal const double ConcentrationShareThreshold = 0.30;
        internal const double DominantWeekdayShareThreshold = 0.35;
        internal const double DominantTimeBandShareThreshold = 0.40;
        internal const double MeaningfulDistanceChangePercent = 15.0;
        internal const int MeaningfulSessionCountChange = 2;
        internal const double MeaningfulMedianDurationChangeSeconds = 600.0;
        internal const int MinimumVarietyCount = 2;
        private const int MaximumObservations = 3;
        private const int MaximumNamedTracks = 3;
        private const int TrendBucketCount = 8;
        private const double MetersPerKilometer = 1000.0;
        private const double MetersPerMile = 1609.344;
        private static readonly string[] TimeBandNames = { "Overnight", "Morning", "Afternoon", "Evening" };

        public static AffinityPatternSnapshot Build(
            IReadOnlyCollection<AffinitySessionRecord> currentSessions,
            IReadOnlyCollection<AffinitySessionRecord> previousSessions,
            bool displayInMiles,
            TimeZoneInfo localTimeZone,
            CultureInfo culture,
            DateTime currentStartUtc,
            DateTime currentEndUtc)
        {
            currentSessions = currentSessions ?? Array.Empty<AffinitySessionRecord>();
            previousSessions = previousSessions ?? Array.Empty<AffinitySessionRecord>();
            localTimeZone = localTimeZone ?? TimeZoneInfo.Local;
            culture = culture ?? CultureInfo.CurrentCulture;

            AffinityPatternSnapshot snapshot = new AffinityPatternSnapshot
            {
                HasSufficientData = currentSessions.Count >= MinimumCurrentSessions,
                Comparison = BuildComparison(currentSessions, previousSessions, displayInMiles, culture)
            };
            snapshot.Comparison.TrendBuckets = BuildTrendBuckets(currentSessions, currentStartUtc, currentEndUtc);
            if (!snapshot.HasSufficientData) return snapshot;

            snapshot.EmptyStateText = string.Empty;
            List<LocalSession> localSessions = currentSessions.Select(session => new LocalSession
            {
                Session = session,
                StartedLocal = TimeZoneInfo.ConvertTimeFromUtc(session.StartedUtc, localTimeZone)
            }).ToList();
            snapshot.WeekdayBuckets = BuildWeekdays(localSessions, culture);
            snapshot.TrackShares = BuildTrackShares(currentSessions, displayInMiles, culture);

            AddObservation(snapshot, BuildConcentration(currentSessions, culture));
            AddObservation(snapshot, BuildSchedule(localSessions, snapshot.WeekdayBuckets, culture));
            AddObservation(snapshot, BuildChange(snapshot.Comparison));
            if (snapshot.Observations.Count < MaximumObservations)
            {
                AddObservation(snapshot, BuildVariety(currentSessions, previousSessions,
                    snapshot.Comparison.HasComparison, culture));
            }
            return snapshot;
        }

        private static List<AffinityPatternTrendBucket> BuildTrendBuckets(
            IReadOnlyCollection<AffinitySessionRecord> sessions,
            DateTime currentStartUtc,
            DateTime currentEndUtc)
        {
            long startTicks = currentStartUtc.Ticks;
            long rangeTicks = currentEndUtc.Ticks - startTicks;
            var buckets = new List<AffinityPatternTrendBucket>(TrendBucketCount);
            for (int index = 0; index < TrendBucketCount; index++)
            {
                buckets.Add(new AffinityPatternTrendBucket
                {
                    StartUtc = new DateTime(startTicks + (rangeTicks > 0 ? rangeTicks * index / TrendBucketCount : 0), DateTimeKind.Utc),
                    EndUtc = new DateTime(startTicks + (rangeTicks > 0 ? rangeTicks * (index + 1) / TrendBucketCount : 0), DateTimeKind.Utc)
                });
            }

            if (rangeTicks <= 0)
            {
                return buckets;
            }

            foreach (IGrouping<int, AffinitySessionRecord> group in sessions
                .Where(session => session.StartedUtc >= currentStartUtc && session.StartedUtc < currentEndUtc)
                .GroupBy(session => (int)Math.Min(
                    TrendBucketCount - 1,
                    (session.StartedUtc.Ticks - startTicks) * TrendBucketCount / rangeTicks)))
            {
                AffinityPatternTrendBucket bucket = buckets[group.Key];
                bucket.DistanceMeters = group.Sum(session => session.DistanceMeters);
                bucket.SessionCount = group.Count();
                bucket.MedianDurationSeconds = MedianDuration(group);
            }

            return buckets;
        }

        private static AffinityPatternComparison BuildComparison(
            IReadOnlyCollection<AffinitySessionRecord> current,
            IReadOnlyCollection<AffinitySessionRecord> previous,
            bool miles,
            CultureInfo culture)
        {
            double distance = current.Sum(session => session.DistanceMeters);
            double median = MedianDuration(current);
            AffinityPatternComparison comparison = new AffinityPatternComparison
            {
                CurrentDistanceMeters = distance,
                CurrentDistanceDisplay = FormatDistance(distance, miles, culture),
                CurrentSessionCount = current.Count,
                CurrentSessionCountDisplay = current.Count.ToString("N0", culture),
                CurrentMedianDurationSeconds = median,
                CurrentMedianDurationDisplay = (median / 60.0).ToString("0.#", culture) + " min",
                HasComparison = previous.Count >= MinimumPreviousSessions
            };
            if (!comparison.HasComparison) return comparison;

            double previousDistance = previous.Sum(session => session.DistanceMeters);
            if (previousDistance > 0)
            {
                comparison.DistancePercentChange = (distance - previousDistance) / previousDistance * 100.0;
                comparison.DistanceChangeDisplay = Signed(comparison.DistancePercentChange.Value, culture) + "%";
            }
            comparison.SessionCountDifference = current.Count - previous.Count;
            comparison.MedianDurationDifferenceSeconds = median - MedianDuration(previous);
            comparison.SessionCountChangeDisplay = Signed(comparison.SessionCountDifference.Value, culture);
            comparison.MedianDurationChangeDisplay = Signed(comparison.MedianDurationDifferenceSeconds.Value / 60.0, culture) + " min";
            return comparison;
        }

        private static double MedianDuration(IEnumerable<AffinitySessionRecord> sessions)
        {
            double[] seconds = sessions.Select(session => session.TimeDrivenSeconds).OrderBy(value => value).ToArray();
            if (seconds.Length == 0) return 0;
            int middle = seconds.Length / 2;
            return seconds.Length % 2 == 0 ? (seconds[middle - 1] + seconds[middle]) / 2.0 : seconds[middle];
        }

        private static List<AffinityWeekdayPattern> BuildWeekdays(List<LocalSession> sessions, CultureInfo culture)
        {
            Dictionary<DayOfWeek, double> totals = sessions.GroupBy(session => session.StartedLocal.DayOfWeek)
                .ToDictionary(group => group.Key, group => group.Sum(session => session.Session.TimeDrivenSeconds));
            double maximum = totals.Values.DefaultIfEmpty(0).Max();
            return Enumerable.Range(0, 7).Select(offset =>
            {
                DayOfWeek day = (DayOfWeek)(((int)culture.DateTimeFormat.FirstDayOfWeek + offset) % 7);
                double seconds = totals.TryGetValue(day, out double total) ? total : 0;
                TimeSpan duration = TimeSpan.FromSeconds(seconds);
                return new AffinityWeekdayPattern
                {
                    DayOfWeek = day,
                    Label = culture.DateTimeFormat.GetAbbreviatedDayName(day),
                    TimeDrivenSeconds = seconds,
                    TimeDrivenDisplay = ((int)duration.TotalHours).ToString("D2", culture) + ":" +
                        duration.Minutes.ToString("D2", culture) + ":" + duration.Seconds.ToString("D2", culture),
                    RelativeHeight = maximum > 0 ? seconds / maximum : 0
                };
            }).ToList();
        }

        private static List<AffinityTrackShare> BuildTrackShares(
            IReadOnlyCollection<AffinitySessionRecord> sessions, bool miles, CultureInfo culture)
        {
            double totalDistance = sessions.Sum(session => session.DistanceMeters);
            var tracks = sessions.GroupBy(session => new { session.GameName, session.TrackDisplayName })
                .Select(group => new
                {
                    group.Key.GameName,
                    group.Key.TrackDisplayName,
                    Distance = group.Sum(session => session.DistanceMeters)
                })
                .OrderByDescending(track => track.Distance)
                .ThenBy(track => track.TrackDisplayName, StringComparer.Ordinal)
                .ThenBy(track => track.GameName, StringComparer.Ordinal).ToList();
            List<AffinityTrackShare> shares = tracks.Take(MaximumNamedTracks)
                .Select(track => TrackShare(track.GameName, track.TrackDisplayName, track.Distance,
                    totalDistance, miles, culture, false)).ToList();
            if (tracks.Count > MaximumNamedTracks)
            {
                double remainingDistance = tracks.Skip(MaximumNamedTracks).Sum(track => track.Distance);
                shares.Add(TrackShare(string.Empty, "Other", remainingDistance, totalDistance, miles, culture, true));
            }
            return shares;
        }

        private static AffinityTrackShare TrackShare(string game, string track, double distance,
            double totalDistance, bool miles, CultureInfo culture, bool other)
        {
            double fraction = totalDistance > 0 ? distance / totalDistance : 0;
            return new AffinityTrackShare
            {
                GameName = game,
                TrackDisplayName = track,
                Label = other ? "Other" : track + " (" + game + ")",
                DistanceDisplay = distance / (miles ? MetersPerMile : MetersPerKilometer),
                DistanceDisplayText = FormatDistance(distance, miles, culture),
                SharePercent = fraction * 100.0,
                RelativeWidth = fraction,
                IsOther = other
            };
        }

        private static AffinityPatternObservation BuildConcentration(
            IReadOnlyCollection<AffinitySessionRecord> sessions, CultureInfo culture)
        {
            double totalDistance = sessions.Sum(session => session.DistanceMeters);
            if (totalDistance <= 0) return null;

            // Prefer a car/track pairing, then track, car, and game, all within the game's identity.
            IEnumerable<DistanceFact> facts = sessions.GroupBy(session => new { session.GameName, session.CarModel, session.TrackDisplayName })
                .Select(group => new DistanceFact
                {
                    Specificity = 0,
                    Title = group.Key.CarModel + " at " + group.Key.TrackDisplayName,
                    GameName = group.Key.GameName,
                    Distance = group.Sum(session => session.DistanceMeters)
                })
                .Concat(sessions.GroupBy(session => new { session.GameName, session.TrackDisplayName }).Select(group => new DistanceFact
                {
                    Specificity = 1,
                    Title = group.Key.TrackDisplayName + " is a mainstay",
                    GameName = group.Key.GameName,
                    Distance = group.Sum(session => session.DistanceMeters)
                }))
                .Concat(sessions.GroupBy(session => new { session.GameName, session.CarModel }).Select(group => new DistanceFact
                {
                    Specificity = 2,
                    Title = group.Key.CarModel + " is a mainstay",
                    GameName = group.Key.GameName,
                    Distance = group.Sum(session => session.DistanceMeters)
                }))
                .Concat(sessions.GroupBy(session => session.GameName).Select(group => new DistanceFact
                {
                    Specificity = 3,
                    Title = group.Key + " is a mainstay",
                    GameName = group.Key,
                    Distance = group.Sum(session => session.DistanceMeters)
                }));
            DistanceFact leading = facts.Where(fact => fact.Distance / totalDistance >= ConcentrationShareThreshold)
                .OrderBy(fact => fact.Specificity)
                .ThenByDescending(fact => fact.Distance)
                .ThenBy(fact => fact.Title, StringComparer.Ordinal)
                .ThenBy(fact => fact.GameName, StringComparer.Ordinal).FirstOrDefault();
            return leading == null ? null : new AffinityPatternObservation
            {
                Category = "Concentration",
                Title = leading.Title,
                Detail = (leading.Distance / totalDistance * 100.0).ToString("0.#", culture) +
                    "% of your distance " + new[] { "paired this car and track", "used this track", "used this car", "was driven" }[leading.Specificity] +
                    " in " + leading.GameName + "."
            };
        }

        private static AffinityPatternObservation BuildSchedule(List<LocalSession> sessions,
            List<AffinityWeekdayPattern> weekdays, CultureInfo culture)
        {
            double totalTime = weekdays.Sum(day => day.TimeDrivenSeconds);
            if (totalTime <= 0) return null;
            AffinityWeekdayPattern leadingDay = weekdays.OrderByDescending(day => day.TimeDrivenSeconds)
                .ThenBy(day => culture.DateTimeFormat.GetDayName(day.DayOfWeek), StringComparer.Ordinal).First();
            if (leadingDay.TimeDrivenSeconds / totalTime >= DominantWeekdayShareThreshold)
            {
                return new AffinityPatternObservation
                {
                    Category = "Schedule",
                    Title = culture.DateTimeFormat.GetDayName(leadingDay.DayOfWeek) + " is your busiest day",
                    Detail = (leadingDay.TimeDrivenSeconds / totalTime * 100.0).ToString("0.#", culture) +
                        "% of your driven time started on this local weekday."
                };
            }
            var band = sessions.GroupBy(session => session.StartedLocal.Hour / 6)
                .Select(group => new { Name = TimeBandNames[group.Key], Seconds = group.Sum(session => session.Session.TimeDrivenSeconds) })
                .OrderByDescending(group => group.Seconds).ThenBy(group => group.Name, StringComparer.Ordinal).First();
            return band.Seconds / totalTime < DominantTimeBandShareThreshold ? null : new AffinityPatternObservation
            {
                Category = "Schedule",
                Title = band.Name + " drives",
                Detail = (band.Seconds / totalTime * 100.0).ToString("0.#", culture) +
                    "% of your driven time came from sessions starting in the local " + band.Name.ToLowerInvariant() + "."
            };
        }

        private static AffinityPatternObservation BuildChange(AffinityPatternComparison comparison)
        {
            if (!comparison.HasComparison) return null;
            List<string> changes = new List<string>();
            if (comparison.DistancePercentChange.HasValue && Math.Abs(comparison.DistancePercentChange.Value) >= MeaningfulDistanceChangePercent)
                changes.Add("Distance " + comparison.DistanceChangeDisplay);
            if (Math.Abs(comparison.SessionCountDifference.Value) >= MeaningfulSessionCountChange)
                changes.Add("Sessions " + comparison.SessionCountChangeDisplay);
            if (Math.Abs(comparison.MedianDurationDifferenceSeconds.Value) >= MeaningfulMedianDurationChangeSeconds)
                changes.Add("Typical session " + comparison.MedianDurationChangeDisplay);
            return changes.Count == 0 ? null : new AffinityPatternObservation
            {
                Category = "Change",
                Title = "Your driving changed",
                Detail = string.Join(". ", changes) + " compared with the previous period."
            };
        }

        private static AffinityPatternObservation BuildVariety(
            IReadOnlyCollection<AffinitySessionRecord> current,
            IReadOnlyCollection<AffinitySessionRecord> previous,
            bool hasComparison,
            CultureInfo culture)
        {
            int cars = current.Select(session => new { session.GameName, session.CarModel }).Distinct().Count();
            int tracks = current.Select(session => new { session.GameName, session.TrackDisplayName }).Distinct().Count();
            if (cars < MinimumVarietyCount && tracks < MinimumVarietyCount) return null;
            string detail = VarietyCounts(cars, tracks, culture) + " this period.";
            if (hasComparison)
            {
                int priorCars = previous.Select(session => new { session.GameName, session.CarModel }).Distinct().Count();
                int priorTracks = previous.Select(session => new { session.GameName, session.TrackDisplayName }).Distinct().Count();
                detail += " Previously: " + VarietyCounts(priorCars, priorTracks, culture) + ".";
            }
            return new AffinityPatternObservation { Category = "Variety", Title = "Your driving variety", Detail = detail };
        }

        private static string VarietyCounts(int cars, int tracks, CultureInfo culture)
        {
            return cars.ToString("N0", culture) + (cars == 1 ? " car" : " cars") + " and " +
                tracks.ToString("N0", culture) + (tracks == 1 ? " track" : " tracks");
        }

        private static void AddObservation(AffinityPatternSnapshot snapshot, AffinityPatternObservation observation)
        {
            if (observation != null && snapshot.Observations.Count < MaximumObservations)
                snapshot.Observations.Add(observation);
        }

        private static string FormatDistance(double meters, bool miles, CultureInfo culture)
        {
            return (meters / (miles ? MetersPerMile : MetersPerKilometer)).ToString("N1", culture) + (miles ? " mi" : " km");
        }

        private static string Signed(double value, CultureInfo culture)
        {
            return value.ToString("+0.#;-0.#;0", culture);
        }

        private sealed class LocalSession
        {
            public AffinitySessionRecord Session { get; set; }
            public DateTime StartedLocal { get; set; }
        }

        private sealed class DistanceFact
        {
            public int Specificity { get; set; }
            public string Title { get; set; }
            public string GameName { get; set; }
            public double Distance { get; set; }
        }
    }
}
