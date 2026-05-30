using System.Collections.Generic;
using System.Linq;
using Affinity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Affinity.Tests
{
    [TestClass]
    public class AffinitySummaryBuilderTests
    {
        [TestMethod]
        public void BuildSnapshot_AggregatesTotalsAndAppliesAssettoTrackDisplayNames()
        {
            AffinityDatabase database = new AffinityDatabase
            {
                Games = new Dictionary<string, GameBucket>
                {
                    ["Assetto Corsa"] = new GameBucket
                    {
                        Cars = new Dictionary<string, CarBucket>
                        {
                            ["BMW M3 GT2"] = new CarBucket
                            {
                                Tracks = new Dictionary<string, TrackBucket>
                                {
                                    ["monza_gp"] = new TrackBucket
                                    {
                                        TrackName = "monza",
                                        TrackNameWithConfig = "monza_gp",
                                        TotalDistanceMeters = 5000.0,
                                        UsedTime = 600.0
                                    },
                                    ["monza_short"] = new TrackBucket
                                    {
                                        TrackName = "monza",
                                        TrackNameWithConfig = "monza_short",
                                        TotalDistanceMeters = 2000.0,
                                        UsedTime = 300.0
                                    }
                                }
                            },
                            ["Ferrari 488 GT3"] = new CarBucket
                            {
                                Tracks = new Dictionary<string, TrackBucket>
                                {
                                    ["spa"] = new TrackBucket
                                    {
                                        TrackName = "spa",
                                        TrackNameWithConfig = "spa",
                                        TotalDistanceMeters = 10000.0,
                                        UsedTime = 1200.0
                                    }
                                }
                            }
                        }
                    },
                    ["iRacing"] = new GameBucket
                    {
                        Cars = new Dictionary<string, CarBucket>
                        {
                            ["Mazda MX-5"] = new CarBucket
                            {
                                Tracks = new Dictionary<string, TrackBucket>
                                {
                                    ["lime_rock"] = new TrackBucket
                                    {
                                        TrackName = "lime_rock",
                                        TrackNameWithConfig = "lime_rock",
                                        TotalDistanceMeters = 1609.344,
                                        UsedTime = 180.0
                                    }
                                }
                            }
                        }
                    }
                }
            };

            Dictionary<string, string> trackMap = new Dictionary<string, string>
            {
                ["monza_gp"] = "Monza GP"
            };

            AffinitySummarySnapshot snapshot = AffinitySummaryBuilder.BuildSnapshot(database, displayInMiles: false, trackMap);

            Assert.AreEqual(18.609344, snapshot.TotalDistanceKm, 0.000001);
            Assert.AreEqual(2280.0, snapshot.TotalUsedTime, 0.000001);
            Assert.AreEqual(2, snapshot.GameTabs.Count);
            Assert.AreEqual("Assetto Corsa", snapshot.FeaturedGameTab.GameName);
            Assert.AreEqual("spa", snapshot.FeaturedTrackSummary.TrackDisplayName);
            Assert.AreEqual("Assetto Corsa", snapshot.FeaturedTrackSummary.GameName);
            Assert.AreEqual("Ferrari 488 GT3", snapshot.FeaturedCarSummary.CarModel);
            Assert.AreEqual("Assetto Corsa", snapshot.FeaturedCarSummary.GameName);

            GameDistanceTab assettoTab = snapshot.GameTabs.Single(tab => tab.GameName == "Assetto Corsa");
            Assert.AreEqual(17.0, assettoTab.TotalDistanceKm, 0.000001);
            Assert.AreEqual("00:35:00", assettoTab.TotalUsedTimeDisplay);
            Assert.AreEqual("spa", assettoTab.TrackSummaries[0].TrackDisplayName);
            Assert.AreEqual("Monza GP", assettoTab.TrackSummaries[1].TrackDisplayName);
            Assert.AreEqual("Ferrari 488 GT3", assettoTab.CarSummaries[0].CarModel);
            Assert.AreEqual("00:20:00", assettoTab.TrackSummaries[0].UsedTimeDisplay);
        }

        [TestMethod]
        public void BuildSnapshot_UsesMilesForDisplayWhenConfigured()
        {
            AffinityDatabase database = new AffinityDatabase
            {
                Games = new Dictionary<string, GameBucket>
                {
                    ["iRacing"] = new GameBucket
                    {
                        Cars = new Dictionary<string, CarBucket>
                        {
                            ["Mazda MX-5"] = new CarBucket
                            {
                                Tracks = new Dictionary<string, TrackBucket>
                                {
                                    ["lime_rock"] = new TrackBucket
                                    {
                                        TrackName = "lime_rock",
                                        TrackNameWithConfig = "lime_rock",
                                        TotalDistanceMeters = 1609.344,
                                        UsedTime = 90.0
                                    }
                                }
                            }
                        }
                    }
                }
            };

            AffinitySummarySnapshot snapshot = AffinitySummaryBuilder.BuildSnapshot(database, displayInMiles: true, assettoCorsaTrackMap: null);
            GameDistanceTab onlyTab = snapshot.GameTabs.Single();

            Assert.AreEqual(1.0, onlyTab.TotalDistanceDisplay, 0.000001);
            Assert.AreEqual(1.0, onlyTab.TrackSummaries.Single().DistanceDisplay, 0.000001);
            Assert.AreEqual(1.0, onlyTab.CarSummaries.Single().DistanceDisplay, 0.000001);
            Assert.AreEqual("00:01:30", onlyTab.TotalUsedTimeDisplay);
            Assert.AreEqual("iRacing", snapshot.FeaturedTrackSummary.GameName);
            Assert.AreEqual("iRacing", snapshot.FeaturedCarSummary.GameName);
        }
    }
}
