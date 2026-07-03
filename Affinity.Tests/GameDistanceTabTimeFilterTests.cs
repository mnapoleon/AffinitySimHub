using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Affinity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Affinity.Tests
{
    [TestClass]
    public class GameDistanceTabTimeFilterTests
    {
        [TestMethod]
        public void SetTimePeriodSummaries_UpdatesCurrentTabWithoutChangingAllTimeRows()
        {
            GameDistanceTab tab = BuildTab();

            tab.SelectedTimePeriodFilterKey = GameDistanceTab.TimePeriodLast30Days;
            tab.SetTimePeriodSummaries(new[]
            {
                Row("Assetto Corsa", "Ferrari 488 GT3", "spa", 4.0, 2400.0, new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc))
            });

            Assert.AreEqual(5, tab.RawSummaries.Count);
            Assert.AreEqual(1, tab.VisibleTrackSummaries.Count);
            Assert.AreEqual("spa", tab.VisibleTrackSummaries.Single().TrackName);
            Assert.AreEqual(1, tab.VisibleCarSummaries.Count);
            Assert.AreEqual("Ferrari 488 GT3", tab.VisibleCarSummaries.Single().CarModel);
            Assert.AreEqual("Period: Last 30 days", tab.ActiveFilterDescription);
        }

        [TestMethod]
        public void SortByTimeAndTopLimit_AppliesToTrackAndCarSummaries()
        {
            GameDistanceTab tab = BuildTab();

            tab.SelectedSortModeKey = GameDistanceTab.SortByTimeDriven;
            tab.SelectedResultLimitKey = GameDistanceTab.ResultLimitTop5;

            Assert.AreEqual("spa", tab.VisibleTrackSummaries[0].TrackName);
            Assert.AreEqual("nurburgring", tab.VisibleTrackSummaries[1].TrackName);
            Assert.AreEqual(4, tab.VisibleTrackSummaries.Count);
            Assert.AreEqual("Ferrari 488 GT3", tab.VisibleCarSummaries[0].CarModel);
            Assert.AreEqual("Porsche 911 GT3 R", tab.VisibleCarSummaries[1].CarModel);
            Assert.AreEqual("Sort: Time driven; Limit: Top 5", tab.ActiveFilterDescription);
        }

        [TestMethod]
        public void PublicFilterSurface_DoesNotExposeRecencyFilter()
        {
            List<string> recencyMembers = typeof(GameDistanceTab)
                .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Where(member => member.Name.IndexOf("Recency", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(member => member.Name)
                .Distinct()
                .OrderBy(name => name)
                .ToList();

            CollectionAssert.AreEqual(new List<string>(), recencyMembers);
        }

        [TestMethod]
        public void TrackFilter_ComposesWithTimeFilters()
        {
            GameDistanceTab tab = BuildTab();
            tab.SelectedSortModeKey = GameDistanceTab.SortByTimeDriven;
            tab.SelectedResultLimitKey = GameDistanceTab.ResultLimitTop5;

            tab.SelectedTrackSummary = tab.TrackSummaries.Single(summary => summary.TrackName == "monza_gp");

            Assert.AreEqual("monza_gp", tab.TopTrackSummary.TrackName);
            Assert.AreEqual(2, tab.VisibleCarSummaries.Count);
            Assert.AreEqual("BMW M3 GT2", tab.VisibleCarSummaries[0].CarModel);
            Assert.AreEqual("Ferrari 488 GT3", tab.VisibleCarSummaries[1].CarModel);
            Assert.AreEqual("Filtered by track: Monza GP; Sort: Time driven; Limit: Top 5", tab.ActiveFilterDescription);
        }

        [TestMethod]
        public void ClearFilter_ResetsTimeFiltersAndRestoresAllTimeRows()
        {
            GameDistanceTab tab = BuildTab();
            tab.SelectedTimePeriodFilterKey = GameDistanceTab.TimePeriodLast7Days;
            tab.SetTimePeriodSummaries(new[]
            {
                Row("Assetto Corsa", "Ferrari 488 GT3", "spa", 4.0, 2400.0, new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc))
            });
            tab.SelectedSortModeKey = GameDistanceTab.SortByTimeDriven;
            tab.SelectedResultLimitKey = GameDistanceTab.ResultLimitTop5;

            tab.ClearFilter();

            Assert.AreEqual(GameDistanceTab.TimePeriodAllTime, tab.SelectedTimePeriodFilterKey);
            Assert.AreEqual(GameDistanceTab.SortByDistance, tab.SelectedSortModeKey);
            Assert.AreEqual(GameDistanceTab.ResultLimitAll, tab.SelectedResultLimitKey);
            Assert.AreEqual(4, tab.VisibleTrackSummaries.Count);
            Assert.AreEqual(4, tab.VisibleCarSummaries.Count);
            Assert.AreEqual("No filter", tab.ActiveFilterDescription);
        }

        private static GameDistanceTab BuildTab()
        {
            Dictionary<string, string> trackMap = new Dictionary<string, string>
            {
                ["monza_gp"] = "Monza GP"
            };

            return AffinitySummaryBuilder.BuildSnapshot(new[]
            {
                Row("Assetto Corsa", "BMW M3 GT2", "monza_gp", 5.0, 600.0, new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc)),
                Row("Assetto Corsa", "Ferrari 488 GT3", "spa", 10.0, 1200.0, new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc)),
                Row("Assetto Corsa", "Ferrari 488 GT3", "monza_gp", 2.0, 240.0, new DateTime(2026, 6, 18, 0, 0, 0, DateTimeKind.Utc)),
                Row("Assetto Corsa", "Porsche 911 GT3 R", "nurburgring", 1.0, 900.0, new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)),
                Row("Assetto Corsa", "Lotus 49", "ks_silverstone", 3.0, 300.0, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc))
            }, displayInMiles: false, trackMap).GameTabs.Single();
        }

        private static DistanceSummary Row(
            string gameName,
            string carModel,
            string trackNameWithConfig,
            double distanceKm,
            double usedTime,
            DateTime lastUpdatedUtc)
        {
            return new DistanceSummary
            {
                GameName = gameName,
                CarModel = carModel,
                TrackName = trackNameWithConfig,
                TrackNameWithConfig = trackNameWithConfig,
                TrackDisplayName = trackNameWithConfig,
                TotalDistanceKm = distanceKm,
                TotalDistanceMiles = distanceKm / 1.609344,
                UsedTime = usedTime,
                LastUpdatedUtc = lastUpdatedUtc
            };
        }
    }
}
