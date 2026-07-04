using System.Linq;
using System.Reflection;
using Affinity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Affinity.Tests
{
    [TestClass]
    public class AffinityOverviewSummaryTests
    {
        [TestMethod]
        public void NewPlugin_InitializesOverviewSummarySourcesSafely()
        {
            AffinityPlugin plugin = new AffinityPlugin();

            Assert.IsNotNull(plugin.OverallTopSummarySection);
            Assert.AreEqual("Top Overall", plugin.OverallTopSummarySection.Header);
            Assert.IsNotNull(plugin.SelectedRecentHighlightsSection);
            Assert.AreEqual(AffinityPlugin.RecentHighlightsPeriodThisMonth, plugin.SelectedRecentHighlightsPeriodKey);
            CollectionAssert.AreEqual(
                new[]
                {
                    AffinityPlugin.RecentHighlightsPeriodThisWeek,
                    AffinityPlugin.RecentHighlightsPeriodLastWeek,
                    AffinityPlugin.RecentHighlightsPeriodThisMonth,
                    AffinityPlugin.RecentHighlightsPeriodLastMonth
                },
                plugin.RecentHighlightsPeriodOptions.Select(option => option.Key).ToArray());
        }

        [TestMethod]
        public void ApplySummarySnapshot_PopulatesOverviewSummarySourcesAndCompatibilitySections()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            AffinitySummarySnapshot allTimeSnapshot = CreateSnapshot("All Game", "All Track", "All Car");
            AffinitySummarySnapshot thisMonthSnapshot = CreateSnapshot("This Game", "This Track", "This Car");
            AffinitySummarySnapshot lastMonthSnapshot = CreateSnapshot("Last Game", "Last Track", "Last Car");

            InvokeApplySummarySnapshot(
                plugin,
                allTimeSnapshot,
                thisMonthSnapshot,
                lastMonthSnapshot,
                thisMonthSnapshot,
                "This month highlights",
                "Jul 1 - Jul 4");

            Assert.AreEqual("Top Overall", plugin.OverallTopSummarySection.Header);
            Assert.AreEqual("All Game", plugin.OverallTopSummarySection.FeaturedGameTab.GameName);
            Assert.AreEqual("This month highlights", plugin.SelectedRecentHighlightsSection.Header);
            Assert.AreEqual("This Game", plugin.SelectedRecentHighlightsSection.FeaturedGameTab.GameName);
            Assert.AreEqual("This month", plugin.SelectedRecentHighlightsPeriodDisplayName);
            Assert.AreEqual("Jul 1 - Jul 4", plugin.SelectedRecentHighlightsDateRangeDisplay);
            Assert.AreEqual(3, plugin.TopSummarySections.Count);
            CollectionAssert.AreEqual(
                new[] { "Top Overall", "Top This Month", "Top Last Month" },
                plugin.TopSummarySections.Select(section => section.Header).ToArray());
        }

        [TestMethod]
        public void SelectedRecentHighlightsPeriodKey_UpdatesDisplayState()
        {
            AffinityPlugin plugin = new AffinityPlugin();

            plugin.SelectedRecentHighlightsPeriodKey = AffinityPlugin.RecentHighlightsPeriodLastWeek;

            Assert.AreEqual(AffinityPlugin.RecentHighlightsPeriodLastWeek, plugin.SelectedRecentHighlightsPeriodKey);
            Assert.AreEqual("Last week", plugin.SelectedRecentHighlightsPeriodDisplayName);
        }

        [TestMethod]
        public void TopSummarySection_HelperPropertiesExposeNullSafeState()
        {
            AffinityTopSummarySection section = new AffinityTopSummarySection();

            Assert.AreEqual("No driving history yet", section.EmptyStateText);
            Assert.IsFalse(section.HasFeaturedGame);
            Assert.IsFalse(section.HasFeaturedTrack);
            Assert.IsFalse(section.HasFeaturedCar);

            section.FeaturedGameTab = new GameDistanceTab { GameName = "Assetto Corsa" };
            section.FeaturedTrackSummary = new TrackDistanceSummary { TrackDisplayName = "Spa" };
            section.FeaturedCarSummary = new CarDistanceSummary { CarModel = "Mazda MX-5" };

            Assert.IsTrue(section.HasFeaturedGame);
            Assert.IsTrue(section.HasFeaturedTrack);
            Assert.IsTrue(section.HasFeaturedCar);
        }

        private static AffinitySummarySnapshot CreateSnapshot(string gameName, string trackName, string carModel)
        {
            return new AffinitySummarySnapshot
            {
                FeaturedGameTab = new GameDistanceTab
                {
                    GameName = gameName,
                    TotalDistanceDisplay = 12.34,
                    TotalUsedTimeDisplay = "00:12:34"
                },
                FeaturedTrackSummary = new TrackDistanceSummary
                {
                    GameName = gameName,
                    TrackDisplayName = trackName,
                    DistanceDisplay = 23.45,
                    UsedTimeDisplay = "00:23:45"
                },
                FeaturedCarSummary = new CarDistanceSummary
                {
                    GameName = gameName,
                    CarModel = carModel,
                    DistanceDisplay = 34.56,
                    UsedTimeDisplay = "00:34:56"
                }
            };
        }

        private static void InvokeApplySummarySnapshot(
            AffinityPlugin plugin,
            AffinitySummarySnapshot snapshot,
            AffinitySummarySnapshot thisMonthSnapshot,
            AffinitySummarySnapshot lastMonthSnapshot,
            AffinitySummarySnapshot selectedRecentHighlightsSnapshot,
            string selectedRecentHighlightsHeader,
            string selectedRecentHighlightsDateRangeDisplay)
        {
            MethodInfo method = typeof(AffinityPlugin).GetMethod(
                "ApplySummarySnapshot",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(method);
            method.Invoke(
                plugin,
                new object[]
                {
                    snapshot,
                    thisMonthSnapshot,
                    lastMonthSnapshot,
                    selectedRecentHighlightsSnapshot,
                    selectedRecentHighlightsHeader,
                    selectedRecentHighlightsDateRangeDisplay
                });
        }
    }
}
