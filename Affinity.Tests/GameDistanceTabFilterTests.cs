using System.Collections.Generic;
using System.Linq;
using Affinity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Affinity.Tests
{
    [TestClass]
    public class GameDistanceTabFilterTests
    {
        [TestMethod]
        public void SelectedTrackSummary_FiltersVisibleCarsForThatTrack()
        {
            GameDistanceTab tab = BuildSnapshot().GameTabs.Single();
            TrackDistanceSummary monza = tab.TrackSummaries.Single(summary => summary.TrackName == "monza_gp");

            tab.SelectedTrackSummary = monza;

            Assert.AreEqual("Filtered by track: Monza GP", tab.ActiveFilterDescription);
            Assert.AreEqual("Monza GP", tab.TopTrackSummary.TrackDisplayName);
            Assert.AreEqual(2, tab.VisibleCarSummaries.Count);
            Assert.AreEqual("BMW M3 GT2", tab.VisibleCarSummaries[0].CarModel);
            Assert.AreEqual(7.0, tab.VisibleCarSummaries.Sum(summary => summary.DistanceKm), 0.000001);
            Assert.AreEqual("Ferrari 488 GT3", tab.VisibleCarSummaries[1].CarModel);
            Assert.IsNull(tab.SelectedCarSummary);
        }

        [TestMethod]
        public void SelectedCarSummary_FiltersVisibleTracksForThatCarAndClearsTrackFilter()
        {
            GameDistanceTab tab = BuildSnapshot().GameTabs.Single();
            tab.SelectedTrackSummary = tab.TrackSummaries.Single(summary => summary.TrackName == "monza_gp");

            CarDistanceSummary ferrari = tab.CarSummaries.Single(summary => summary.CarModel == "Ferrari 488 GT3");
            tab.SelectedCarSummary = ferrari;

            Assert.AreEqual("Filtered by car: Ferrari 488 GT3", tab.ActiveFilterDescription);
            Assert.IsNull(tab.SelectedTrackSummary);
            Assert.AreEqual("Ferrari 488 GT3", tab.TopCarSummary.CarModel);
            Assert.AreEqual(2, tab.VisibleTrackSummaries.Count);
            Assert.AreEqual("spa", tab.VisibleTrackSummaries[0].TrackDisplayName);
            Assert.AreEqual("Monza GP", tab.VisibleTrackSummaries[1].TrackDisplayName);
            Assert.AreEqual(12.0, tab.VisibleTrackSummaries.Sum(summary => summary.DistanceKm), 0.000001);
        }

        [TestMethod]
        public void ClearFilter_RestoresFullTrackAndCarLists()
        {
            GameDistanceTab tab = BuildSnapshot().GameTabs.Single();
            tab.SelectedCarSummary = tab.CarSummaries.Single(summary => summary.CarModel == "Ferrari 488 GT3");

            tab.ClearFilter();

            Assert.AreEqual("No filter", tab.ActiveFilterDescription);
            Assert.IsFalse(tab.HasActiveFilter);
            Assert.IsNull(tab.SelectedTrackSummary);
            Assert.IsNull(tab.SelectedCarSummary);
            CollectionAssert.AreEqual(tab.TrackSummaries.Select(summary => summary.TrackName).ToList(), tab.VisibleTrackSummaries.Select(summary => summary.TrackName).ToList());
            CollectionAssert.AreEqual(tab.CarSummaries.Select(summary => summary.CarModel).ToList(), tab.VisibleCarSummaries.Select(summary => summary.CarModel).ToList());
            Assert.AreEqual(tab.TrackSummaries[0].TrackDisplayName, tab.TopTrackSummary.TrackDisplayName);
            Assert.AreEqual(tab.CarSummaries[0].CarModel, tab.TopCarSummary.CarModel);
        }

        private static AffinitySummarySnapshot BuildSnapshot()
        {
            List<DistanceSummary> rows = new List<DistanceSummary>
            {
                new DistanceSummary
                {
                    GameName = "Assetto Corsa",
                    CarModel = "BMW M3 GT2",
                    TrackName = "monza",
                    TrackNameWithConfig = "monza_gp",
                    TotalDistanceKm = 5.0,
                    TotalDistanceMiles = 5.0 / 1.609344,
                    UsedTime = 600.0
                },
                new DistanceSummary
                {
                    GameName = "Assetto Corsa",
                    CarModel = "Ferrari 488 GT3",
                    TrackName = "spa",
                    TrackNameWithConfig = "spa",
                    TotalDistanceKm = 10.0,
                    TotalDistanceMiles = 10.0 / 1.609344,
                    UsedTime = 1200.0
                },
                new DistanceSummary
                {
                    GameName = "Assetto Corsa",
                    CarModel = "Ferrari 488 GT3",
                    TrackName = "monza",
                    TrackNameWithConfig = "monza_gp",
                    TotalDistanceKm = 2.0,
                    TotalDistanceMiles = 2.0 / 1.609344,
                    UsedTime = 240.0
                },
                new DistanceSummary
                {
                    GameName = "Assetto Corsa",
                    CarModel = "Porsche 911 GT3 R",
                    TrackName = "nurburgring",
                    TrackNameWithConfig = "nurburgring",
                    TotalDistanceKm = 1.0,
                    TotalDistanceMiles = 1.0 / 1.609344,
                    UsedTime = 120.0
                }
            };

            Dictionary<string, string> trackMap = new Dictionary<string, string>
            {
                ["monza_gp"] = "Monza GP"
            };

            return AffinitySummaryBuilder.BuildSnapshot(rows, displayInMiles: false, trackMap);
        }
    }
}
