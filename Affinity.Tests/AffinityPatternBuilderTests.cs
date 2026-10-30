using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Affinity;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MsStringAssert = Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert;

namespace Affinity.Tests
{
    [TestClass]
    public class AffinityPatternBuilderTests
    {
        private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");
        private static readonly TimeZoneInfo UtcMinusFour = TimeZoneInfo.CreateCustomTimeZone(
            "Pattern test UTC-4", TimeSpan.FromHours(-4), "Pattern test UTC-4", "Pattern test UTC-4");

        [DataTestMethod]
        [DataRow(0, false)]
        [DataRow(4, false)]
        [DataRow(5, true)]
        public void Build_RequiresFiveCurrentSessionsBeforeShowingPatterns(int count, bool sufficient)
        {
            AffinityPatternSnapshot snapshot = Build(SpreadSessions(count));

            Assert.AreEqual(sufficient, snapshot.HasSufficientData);
            Assert.AreEqual(sufficient ? string.Empty :
                "Complete five sessions in this period to reveal your driving patterns.", snapshot.EmptyStateText);
            if (!sufficient)
            {
                Assert.AreEqual(0, snapshot.Observations.Count);
                Assert.AreEqual(0, snapshot.WeekdayBuckets.Count);
                Assert.AreEqual(0, snapshot.TrackShares.Count);
            }
        }

        [TestMethod]
        public void Build_UsesMiddleDrivenDurationForOddMedian()
        {
            AffinitySessionRecord[] sessions = DurationSessions(20, 30, 90);
            sessions[2].EndedUtc = sessions[2].StartedUtc.AddHours(20);

            AffinityPatternComparison comparison = Build(sessions).Comparison;

            Assert.AreEqual(1800.0, comparison.CurrentMedianDurationSeconds, 0.001);
            Assert.AreEqual("30 min", comparison.CurrentMedianDurationDisplay);
        }

        [TestMethod]
        public void Build_AveragesMiddleDrivenDurationsForEvenMedian()
        {
            AffinityPatternComparison comparison = Build(DurationSessions(90, 20, 40, 30)).Comparison;

            Assert.AreEqual(2100.0, comparison.CurrentMedianDurationSeconds, 0.001);
            Assert.AreEqual("35 min", comparison.CurrentMedianDurationDisplay);
        }

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(2)]
        public void Build_LeavesDeltasUnavailableWithFewerThanThreePreviousSessions(int previousCount)
        {
            AffinityPatternSnapshot snapshot = Build(SpreadSessions(5), SpreadSessions(previousCount));

            Assert.IsTrue(snapshot.HasSufficientData);
            Assert.IsFalse(snapshot.Comparison.HasComparison);
            Assert.IsNull(snapshot.Comparison.DistancePercentChange);
            Assert.IsNull(snapshot.Comparison.SessionCountDifference);
            Assert.IsNull(snapshot.Comparison.MedianDurationDifferenceSeconds);
            Assert.AreEqual("Unavailable", snapshot.Comparison.DistanceChangeDisplay);
            Assert.AreEqual("Unavailable", snapshot.Comparison.SessionCountChangeDisplay);
            Assert.AreEqual("Unavailable", snapshot.Comparison.MedianDurationChangeDisplay);
            Assert.IsFalse(snapshot.Observations.Any(card => card.Category == "Change"));
        }

        [TestMethod]
        public void Build_GroupsUtcStartsByLocalWeekdayAndNormalizesAgainstBusiestDay()
        {
            AffinitySessionRecord[] sessions = SpreadSessions(5);
            for (int i = 0; i < 5; i++)
            {
                sessions[i].StartedUtc = new DateTime(2026, 1, i < 3 ? 5 : 6, 0, 30, 0, DateTimeKind.Utc);
            }

            AffinityPatternSnapshot snapshot = Build(sessions, zone: UtcMinusFour);
            AffinityWeekdayPattern sunday = snapshot.WeekdayBuckets.Single(day => day.DayOfWeek == DayOfWeek.Sunday);
            AffinityWeekdayPattern monday = snapshot.WeekdayBuckets.Single(day => day.DayOfWeek == DayOfWeek.Monday);

            Assert.AreEqual(1800.0, sunday.TimeDrivenSeconds, 0.001);
            Assert.AreEqual("00:30:00", sunday.TimeDrivenDisplay);
            Assert.AreEqual(1.0, sunday.RelativeHeight, 0.001);
            Assert.AreEqual(1200.0, monday.TimeDrivenSeconds, 0.001);
            Assert.AreEqual(2.0 / 3.0, monday.RelativeHeight, 0.001);
            Assert.IsTrue(snapshot.WeekdayBuckets.All(day => day.RelativeHeight >= 0 && day.RelativeHeight <= 1));
        }

        [DataTestMethod]
        [DataRow(4, "Overnight drives")]
        [DataRow(10, "Morning drives")]
        [DataRow(16, "Afternoon drives")]
        [DataRow(22, "Evening drives")]
        [DataRow(3, "Evening drives")]
        public void Build_UsesLocalStartHourForFixedTimeBands(int utcHour, string title)
        {
            AffinitySessionRecord[] sessions = SpreadSessions(5);
            for (int i = 0; i < sessions.Length; i++)
            {
                sessions[i].StartedUtc = new DateTime(2026, 1, 5 + i, utcHour, 0, 0, DateTimeKind.Utc);
            }

            AffinityPatternObservation schedule = Build(sessions, zone: UtcMinusFour)
                .Observations.Single(card => card.Category == "Schedule");

            Assert.AreEqual(title, schedule.Title);
            MsStringAssert.Contains(schedule.Detail, "100%");
        }

        [TestMethod]
        public void Build_OrdersAndLabelsWeekdaysUsingSuppliedCulture()
        {
            AffinityPatternSnapshot snapshot = Build(SpreadSessions(5), culture: CultureInfo.GetCultureInfo("en-GB"));

            CollectionAssert.AreEqual(new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday },
                snapshot.WeekdayBuckets.Select(day => day.DayOfWeek).ToArray());
            Assert.AreEqual("Mon", snapshot.WeekdayBuckets[0].Label);
        }

        [TestMethod]
        public void Build_TrackSharesKeepGamesSeparateAndCombineRemainderIntoOther()
        {
            AffinitySessionRecord[] sessions = SpreadSessions(6);
            SetTrack(sessions[0], "Game A", "Leader", 4000);
            SetTrack(sessions[1], "Game B", "Shared", 3000);
            SetTrack(sessions[2], "Game A", "Shared", 1000);
            SetTrack(sessions[3], "Game A", "Shared", 1000);
            SetTrack(sessions[4], "Game A", "Zulu", 1000);
            SetTrack(sessions[5], "Game A", "Beta", 1000);

            AffinityPatternSnapshot snapshot = Build(sessions);

            CollectionAssert.AreEqual(new[] { "Leader", "Shared", "Shared", "Other" },
                snapshot.TrackShares.Select(track => track.TrackDisplayName).ToArray());
            Assert.AreEqual("Game B", snapshot.TrackShares[1].GameName);
            Assert.AreEqual("Game A", snapshot.TrackShares[2].GameName);
            Assert.AreEqual("Shared (Game B)", snapshot.TrackShares[1].Label);
            Assert.AreEqual(2.0, snapshot.TrackShares[2].DistanceDisplay, 0.001);
            Assert.IsTrue(snapshot.TrackShares[3].IsOther);
            Assert.AreEqual(2.0, snapshot.TrackShares[3].DistanceDisplay, 0.001);
            Assert.AreEqual(100.0, snapshot.TrackShares.Sum(track => track.SharePercent), 0.01);
            Assert.AreEqual(4.0 / 11.0, snapshot.TrackShares[0].RelativeWidth, 0.001);
            Assert.IsTrue(snapshot.TrackShares.All(track => track.RelativeWidth >= 0 && track.RelativeWidth <= 1));
        }

        [TestMethod]
        public void Build_TrackDistanceTiesSortByDisplayNameThenGameRegardlessOfInputOrder()
        {
            AffinitySessionRecord[] sessions = SpreadSessions(5);
            SetTrack(sessions[0], "Zulu game", "Alpha", 1000);
            SetTrack(sessions[1], "Alpha game", "Alpha", 1000);
            SetTrack(sessions[2], "Game", "Beta", 1000);
            SetTrack(sessions[3], "Game", "Gamma", 1000);
            SetTrack(sessions[4], "Game", "Zulu", 1000);

            AffinityPatternSnapshot snapshot = Build(sessions.Reverse().ToArray());

            CollectionAssert.AreEqual(new[] { "Alpha (Alpha game)", "Alpha (Zulu game)", "Beta (Game)", "Other" },
                snapshot.TrackShares.Select(track => track.Label).ToArray());
        }

        [DataTestMethod]
        [DataRow(false, "8.0 km", 1.609344)]
        [DataRow(true, "5.0 mi", 1.0)]
        public void Build_ConvertsDistanceForSelectedDisplayUnits(bool miles, string display, double trackDistance)
        {
            AffinitySessionRecord[] sessions = SpreadSessions(5);
            foreach (AffinitySessionRecord session in sessions) session.DistanceMeters = 1609.344;

            AffinityPatternSnapshot snapshot = Build(sessions, miles: miles);

            Assert.AreEqual(display, snapshot.Comparison.CurrentDistanceDisplay);
            Assert.AreEqual(8046.72, snapshot.Comparison.CurrentDistanceMeters, 0.001);
            Assert.AreEqual(trackDistance, snapshot.TrackShares[0].DistanceDisplay, 0.000001);
            Assert.AreEqual("5", snapshot.Comparison.CurrentSessionCountDisplay);
        }

        [TestMethod]
        public void Build_FormatsNumericDisplaysWithSuppliedCulture()
        {
            AffinitySessionRecord[] sessions = SpreadSessions(5);
            foreach (AffinitySessionRecord session in sessions) session.DistanceMeters = 1609.344;

            Assert.AreEqual("8,0 km", Build(sessions, culture: CultureInfo.GetCultureInfo("fr-FR"))
                .Comparison.CurrentDistanceDisplay);
        }

        [DataTestMethod]
        [DataRow(5, 3, 40, 30, 66.6666667, 2, 600.0, "+66.7%", "+2", "+10 min")]
        [DataRow(5, 7, 20, 30, -28.5714286, -2, -600.0, "-28.6%", "-2", "-10 min")]
        public void Build_ReportsSignedComparisonMetrics(int currentCount, int previousCount,
            int currentMinutes, int previousMinutes, double percent, int countDifference,
            double secondsDifference, string distanceDisplay, string countDisplay, string durationDisplay)
        {
            AffinitySessionRecord[] current = SpreadSessions(currentCount);
            AffinitySessionRecord[] previous = SpreadSessions(previousCount);
            foreach (AffinitySessionRecord session in current) session.TimeDrivenSeconds = currentMinutes * 60;
            foreach (AffinitySessionRecord session in previous) session.TimeDrivenSeconds = previousMinutes * 60;

            AffinityPatternComparison comparison = Build(current, previous).Comparison;

            Assert.IsTrue(comparison.HasComparison);
            Assert.AreEqual(percent, comparison.DistancePercentChange.Value, 0.00001);
            Assert.AreEqual(countDifference, comparison.SessionCountDifference.Value);
            Assert.AreEqual(secondsDifference, comparison.MedianDurationDifferenceSeconds.Value, 0.001);
            Assert.AreEqual(distanceDisplay, comparison.DistanceChangeDisplay);
            Assert.AreEqual(countDisplay, comparison.SessionCountChangeDisplay);
            Assert.AreEqual(durationDisplay, comparison.MedianDurationChangeDisplay);
        }

        [TestMethod]
        public void Build_ZeroPriorDistanceLeavesOnlyDistancePercentageUnavailable()
        {
            AffinitySessionRecord[] previous = SpreadSessions(3);
            foreach (AffinitySessionRecord session in previous) session.DistanceMeters = 0;

            AffinityPatternComparison comparison = Build(SpreadSessions(5), previous).Comparison;

            Assert.IsTrue(comparison.HasComparison);
            Assert.IsNull(comparison.DistancePercentChange);
            Assert.AreEqual("Unavailable", comparison.DistanceChangeDisplay);
            Assert.AreEqual(2, comparison.SessionCountDifference);
            Assert.AreEqual(0.0, comparison.MedianDurationDifferenceSeconds);
        }

        [TestMethod]
        public void Build_ZeroCurrentMetricsYieldFiniteEmptyChartBars()
        {
            AffinitySessionRecord[] sessions = SpreadSessions(5);
            foreach (AffinitySessionRecord session in sessions)
            {
                session.DistanceMeters = 0;
                session.TimeDrivenSeconds = 0;
            }

            AffinityPatternSnapshot snapshot = Build(sessions);

            Assert.IsTrue(snapshot.WeekdayBuckets.All(day => day.RelativeHeight == 0));
            Assert.IsTrue(snapshot.TrackShares.All(track => track.SharePercent == 0 && track.RelativeWidth == 0));
            Assert.IsFalse(snapshot.Observations.Any(card => card.Category == "Concentration" || card.Category == "Schedule"));
        }

        [DataTestMethod]
        [DataRow(29.9, false)]
        [DataRow(30.0, true)]
        public void Build_ConcentrationRequiresThirtyPercentDistance(double percent, bool eligible)
        {
            AffinitySessionRecord[] sessions = SpreadSessions(10);
            sessions[0].DistanceMeters = percent;
            foreach (AffinitySessionRecord session in sessions.Skip(1)) session.DistanceMeters = (100 - percent) / 9;

            Assert.AreEqual(eligible, Build(sessions).Observations.Any(card => card.Category == "Concentration"));
        }

        [TestMethod]
        public void Build_ConcentrationPrefersCarTrackCombinationAndBreaksTiesByDisplayName()
        {
            AffinitySessionRecord[] sessions = SpreadSessions(10);
            for (int i = 0; i < 6; i++)
            {
                sessions[i].GameName = "Simulator";
                sessions[i].CarModel = i < 3 ? "Zulu car" : "Alpha car";
                sessions[i].TrackDisplayName = i < 3 ? "Zulu track" : "Alpha track";
            }

            AffinityPatternObservation concentration = Build(sessions.Reverse().ToArray())
                .Observations.Single(card => card.Category == "Concentration");

            Assert.AreEqual("Alpha car at Alpha track", concentration.Title);
            MsStringAssert.Contains(concentration.Detail, "30%");
            MsStringAssert.Contains(concentration.Detail, "Simulator");
        }

        [DataTestMethod]
        [DataRow("Track", "Shared track is a mainstay")]
        [DataRow("Car", "Shared car is a mainstay")]
        [DataRow("Game", "Simulator is a mainstay")]
        public void Build_ConcentrationFallsBackToBroaderGameScopedFacts(string kind, string title)
        {
            AffinitySessionRecord[] sessions = SpreadSessions(10);
            for (int i = 0; i < 3; i++)
            {
                sessions[i].GameName = "Simulator";
                if (kind == "Track") sessions[i].TrackDisplayName = "Shared track";
                if (kind == "Car") sessions[i].CarModel = "Shared car";
            }

            Assert.AreEqual(title, Build(sessions).Observations.Single(card => card.Category == "Concentration").Title);
        }

        [DataTestMethod]
        [DataRow(34.9, false)]
        [DataRow(35.0, true)]
        public void Build_DominantWeekdayRequiresThirtyFivePercentDrivenTime(double percent, bool eligible)
        {
            AffinitySessionRecord[] sessions = SpreadSessions(7);
            for (int i = 0; i < sessions.Length; i++)
            {
                sessions[i].TimeDrivenSeconds = i == 0 ? percent : (100 - percent) / 6;
                sessions[i].StartedUtc = new DateTime(2026, 1, 5 + i, i == 0 ? 0 : ((i - 1) % 3 + 1) * 6,
                    0, 0, DateTimeKind.Utc);
            }

            AffinityPatternObservation schedule = Build(sessions).Observations.SingleOrDefault(card => card.Category == "Schedule");
            Assert.AreEqual(eligible, schedule != null);
            if (eligible) Assert.AreEqual("Monday is your busiest day", schedule.Title);
        }

        [DataTestMethod]
        [DataRow(39.9, false)]
        [DataRow(40.0, true)]
        public void Build_DominantTimeBandRequiresFortyPercentDrivenTime(double percent, bool eligible)
        {
            AffinitySessionRecord[] sessions = SpreadSessions(8);
            for (int i = 0; i < sessions.Length; i++)
            {
                int band = i / 2;
                sessions[i].TimeDrivenSeconds = band == 0 ? percent / 2 : (100 - percent) / 6;
                sessions[i].StartedUtc = new DateTime(2026, 1, 5 + i, band * 6, 0, 0, DateTimeKind.Utc);
            }

            AffinityPatternObservation schedule = Build(sessions).Observations.SingleOrDefault(card => card.Category == "Schedule");
            Assert.AreEqual(eligible, schedule != null);
            if (eligible) Assert.AreEqual("Overnight drives", schedule.Title);
        }

        [TestMethod]
        public void Build_WeekdayTiesResolveByDisplayName()
        {
            AffinitySessionRecord[] sessions = SpreadSessions(5);
            for (int i = 0; i < sessions.Length; i++)
            {
                sessions[i].StartedUtc = new DateTime(2026, 1, i < 2 ? 5 : i < 4 ? 9 : 10,
                    i * 4, 0, 0, DateTimeKind.Utc);
            }

            Assert.AreEqual("Friday is your busiest day", Build(sessions).Observations
                .Single(card => card.Category == "Schedule").Title);
        }

        [DataTestMethod]
        [DataRow(14.9, false)]
        [DataRow(15.0, true)]
        [DataRow(-15.0, true)]
        public void Build_DistanceChangeRequiresFifteenPercent(double percent, bool eligible)
        {
            AffinitySessionRecord[] current = SpreadSessions(10);
            foreach (AffinitySessionRecord session in current) session.DistanceMeters = 1000 + percent * 10;

            Assert.AreEqual(eligible, Build(current, SpreadSessions(10)).Observations.Any(card => card.Category == "Change"));
        }

        [DataTestMethod]
        [DataRow(1, false)]
        [DataRow(2, true)]
        [DataRow(-2, true)]
        public void Build_SessionCountChangeRequiresTwoSessions(int difference, bool eligible)
        {
            AffinitySessionRecord[] current = SpreadSessions(10 + difference);
            foreach (AffinitySessionRecord session in current) session.DistanceMeters = 10000.0 / current.Length;

            Assert.AreEqual(eligible, Build(current, SpreadSessions(10)).Observations.Any(card => card.Category == "Change"));
        }

        [DataTestMethod]
        [DataRow(599.0, false)]
        [DataRow(600.0, true)]
        [DataRow(-600.0, true)]
        public void Build_MedianDurationChangeRequiresTenMinutes(double difference, bool eligible)
        {
            AffinitySessionRecord[] current = SpreadSessions(10);
            AffinitySessionRecord[] previous = SpreadSessions(10);
            foreach (AffinitySessionRecord session in previous) session.TimeDrivenSeconds = 1200;
            foreach (AffinitySessionRecord session in current) session.TimeDrivenSeconds = 1200 + difference;

            Assert.AreEqual(eligible, Build(current, previous).Observations.Any(card => card.Category == "Change"));
        }

        [DataTestMethod]
        [DataRow(false, 1, false)]
        [DataRow(false, 2, true)]
        [DataRow(true, 2, true)]
        public void Build_VarietyFillsMissingCardsAtTwoDistinctCarsOrTracks(bool varyTracks, int distinct, bool eligible)
        {
            AffinitySessionRecord[] sessions = SpreadSessions(10);
            for (int i = 0; i < sessions.Length; i++)
            {
                sessions[i].GameName = "Simulator";
                sessions[i].CarModel = !varyTracks && i == 0 && distinct == 2 ? "Second car" : "Car";
                sessions[i].TrackDisplayName = varyTracks && i == 0 && distinct == 2 ? "Second track" : "Track";
            }

            AffinityPatternObservation variety = Build(sessions).Observations.SingleOrDefault(card => card.Category == "Variety");
            Assert.AreEqual(eligible, variety != null);
            if (eligible) MsStringAssert.Contains(variety.Detail, varyTracks ? "2 tracks" : "2 cars");
        }

        [TestMethod]
        public void Build_VarietyCountsGameScopedIdentitiesAndIncludesPriorCountsWhenAvailable()
        {
            AffinitySessionRecord[] current = SpreadSessions(10);
            AffinitySessionRecord[] previous = SpreadSessions(10);
            foreach (AffinitySessionRecord session in current)
            {
                session.CarModel = "Same label";
                session.TrackDisplayName = "Same label";
            }
            foreach (AffinitySessionRecord session in previous)
            {
                session.GameName = "One game";
                session.CarModel = "One car";
                session.TrackDisplayName = "One track";
            }

            string detail = Build(current, previous).Observations.Single(card => card.Category == "Variety").Detail;

            MsStringAssert.Contains(detail, "10 cars and 10 tracks");
            MsStringAssert.Contains(detail, "1 car and 1 track");
        }

        [TestMethod]
        public void Build_OrdersConcentrationScheduleChangeAndCapsCardsAtThree()
        {
            AffinitySessionRecord[] current = SpreadSessions(10);
            foreach (AffinitySessionRecord session in current)
            {
                session.GameName = "Simulator";
                session.StartedUtc = new DateTime(2026, 1, 5, 18, 0, 0, DateTimeKind.Utc);
                session.DistanceMeters = 2000;
            }

            AffinityPatternSnapshot snapshot = Build(current, SpreadSessions(10));

            CollectionAssert.AreEqual(new[] { "Concentration", "Schedule", "Change" },
                snapshot.Observations.Select(card => card.Category).ToArray());
        }

        private static AffinityPatternSnapshot Build(IReadOnlyCollection<AffinitySessionRecord> current,
            IReadOnlyCollection<AffinitySessionRecord> previous = null, bool miles = false,
            TimeZoneInfo zone = null, CultureInfo culture = null)
        {
            return AffinityPatternBuilder.Build(current, previous ?? Array.Empty<AffinitySessionRecord>(),
                miles, zone ?? TimeZoneInfo.Utc, culture ?? English);
        }

        private static AffinitySessionRecord[] SpreadSessions(int count)
        {
            return Enumerable.Range(0, count).Select(i => new AffinitySessionRecord
            {
                SessionUid = "Session " + i,
                GameName = "Game " + i,
                CarModel = "Car " + i,
                TrackName = "raw_track_" + i,
                TrackDisplayName = "Track " + i,
                StartedUtc = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc).AddDays(i).AddHours(i % 4 * 6),
                EndedUtc = new DateTime(2026, 1, 5, 0, 10, 0, DateTimeKind.Utc).AddDays(i).AddHours(i % 4 * 6),
                DistanceMeters = 1000,
                TimeDrivenSeconds = 600
            }).ToArray();
        }

        private static AffinitySessionRecord[] DurationSessions(params int[] minutes)
        {
            AffinitySessionRecord[] sessions = SpreadSessions(minutes.Length);
            for (int i = 0; i < minutes.Length; i++) sessions[i].TimeDrivenSeconds = minutes[i] * 60;
            return sessions;
        }

        private static void SetTrack(AffinitySessionRecord session, string game, string track, double meters)
        {
            session.GameName = game;
            session.TrackDisplayName = track;
            session.DistanceMeters = meters;
        }
    }
}
