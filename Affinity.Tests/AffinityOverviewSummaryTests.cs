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
            Assert.IsNotNull(plugin.MonthlyTopSummarySections);
            Assert.AreEqual(0, plugin.MonthlyTopSummarySections.Count);
        }

        [TestMethod]
        public void ApplySummarySnapshot_PopulatesOverviewSummarySourcesAndCompatibilitySections()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            AffinitySummarySnapshot allTimeSnapshot = CreateSnapshot("All Game", "All Track", "All Car");
            AffinitySummarySnapshot thisMonthSnapshot = CreateSnapshot("This Game", "This Track", "This Car");
            AffinitySummarySnapshot lastMonthSnapshot = CreateSnapshot("Last Game", "Last Track", "Last Car");

            InvokeApplySummarySnapshot(plugin, allTimeSnapshot, thisMonthSnapshot, lastMonthSnapshot);

            Assert.AreEqual("Top Overall", plugin.OverallTopSummarySection.Header);
            Assert.AreEqual("All Game", plugin.OverallTopSummarySection.FeaturedGameTab.GameName);
            Assert.AreEqual(2, plugin.MonthlyTopSummarySections.Count);
            CollectionAssert.AreEqual(
                new[] { "This Month", "Last Month" },
                plugin.MonthlyTopSummarySections.Select(section => section.Header).ToArray());
            Assert.AreEqual("This Game", plugin.MonthlyTopSummarySections[0].FeaturedGameTab.GameName);
            Assert.AreEqual("Last Game", plugin.MonthlyTopSummarySections[1].FeaturedGameTab.GameName);
            Assert.AreEqual(3, plugin.TopSummarySections.Count);
            CollectionAssert.AreEqual(
                new[] { "Top Overall", "Top This Month", "Top Last Month" },
                plugin.TopSummarySections.Select(section => section.Header).ToArray());
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
            AffinitySummarySnapshot lastMonthSnapshot)
        {
            MethodInfo method = typeof(AffinityPlugin).GetMethod(
                "ApplySummarySnapshot",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(method);
            method.Invoke(plugin, new object[] { snapshot, thisMonthSnapshot, lastMonthSnapshot });
        }
    }
}
