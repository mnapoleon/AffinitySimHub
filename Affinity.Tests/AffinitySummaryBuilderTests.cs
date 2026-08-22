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
            TrackDistanceSummary mappedAssettoTrack = assettoTab.TrackSummaries.Single(summary => summary.TrackName == "monza_gp");
            Assert.AreEqual("Monza GP", mappedAssettoTrack.CircuitNameDisplay);
            Assert.AreEqual("Monza GP", mappedAssettoTrack.CircuitLayoutDisplay);
            TrackDistanceSummary fallbackAssettoTrack = assettoTab.TrackSummaries.Single(summary => summary.TrackName == "monza_short");
            Assert.AreEqual("monza_short", fallbackAssettoTrack.CircuitNameDisplay);
            Assert.AreEqual("monza_short", fallbackAssettoTrack.CircuitLayoutDisplay);
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

        [TestMethod]
        public void BuildSnapshot_SplitsCircuitNameAndLayoutForTrackRows()
        {
            AffinitySummarySnapshot snapshot = AffinitySummaryBuilder.BuildSnapshot(new[]
            {
                new DistanceSummary
                {
                    GameName = "Automobilista 2",
                    CarModel = "Formula Trainer",
                    TrackName = "Buenos_Aires",
                    TrackNameWithConfig = "Buenos_Aires-Buenos_Aires_Circuito_15",
                    TrackDisplayName = "Buenos_Aires-Buenos_Aires_Circuito_15",
                    TotalDistanceKm = 1.0,
                    TotalDistanceMiles = 0.621371,
                    UsedTime = 60.0
                }
            }, displayInMiles: false, assettoCorsaTrackMap: null);

            TrackDistanceSummary trackSummary = snapshot.GameTabs.Single().TrackSummaries.Single();

            Assert.AreEqual("Buenos Aires", trackSummary.CircuitNameDisplay);
            Assert.AreEqual("Buenos Aires Circuito 15", trackSummary.CircuitLayoutDisplay);
        }

        [TestMethod]
        public void BuildSnapshot_TitleCasesIRacingCircuitNamesForTrackRows()
        {
            AffinitySummarySnapshot snapshot = AffinitySummaryBuilder.BuildSnapshot(new[]
            {
                new DistanceSummary
                {
                    GameName = "IRacing",
                    CarModel = "Mazda MX-5",
                    TrackName = "spielberg gp",
                    TrackNameWithConfig = "spielberg gp-Grand Prix",
                    TrackDisplayName = "spielberg gp-Grand Prix",
                    TotalDistanceKm = 1.0,
                    TotalDistanceMiles = 0.621371,
                    UsedTime = 60.0
                }
            }, displayInMiles: false, assettoCorsaTrackMap: null);

            TrackDistanceSummary trackSummary = snapshot.GameTabs.Single().TrackSummaries.Single();

            Assert.AreEqual("Spielberg GP", trackSummary.CircuitNameDisplay);
            Assert.AreEqual("Grand Prix", trackSummary.CircuitLayoutDisplay);
        }

        [TestMethod]
        public void BuildSnapshot_SplitsRFactor2CircuitNamesOnDoubleDash()
        {
            AffinitySummarySnapshot snapshot = AffinitySummaryBuilder.BuildSnapshot(new[]
            {
                new DistanceSummary
                {
                    GameName = "RFactor2",
                    CarModel = "BTCC",
                    TrackName = "Lime Rock Park -- No Chicanes",
                    TrackNameWithConfig = "Lime Rock Park -- No Chicanes",
                    TrackDisplayName = "Lime Rock Park -- No Chicanes",
                    TotalDistanceKm = 1.0,
                    TotalDistanceMiles = 0.621371,
                    UsedTime = 60.0
                }
            }, displayInMiles: false, assettoCorsaTrackMap: null);

            TrackDistanceSummary trackSummary = snapshot.GameTabs.Single().TrackSummaries.Single();

            Assert.AreEqual("Lime Rock Park", trackSummary.CircuitNameDisplay);
            Assert.AreEqual("No Chicanes", trackSummary.CircuitLayoutDisplay);
        }

        [TestMethod]
        public void BuildSnapshot_DuplicatesAccAndLmuCircuitNamesAcrossTrackColumns()
        {
            AffinitySummarySnapshot snapshot = AffinitySummaryBuilder.BuildSnapshot(new[]
            {
                new DistanceSummary
                {
                    GameName = "Assetto Corsa Competizione",
                    CarModel = "Ferrari 488 GT3",
                    TrackName = "Brands Hatch Circuit",
                    TrackNameWithConfig = "Brands Hatch Circuit",
                    TrackDisplayName = "Brands Hatch Circuit",
                    TotalDistanceKm = 1.0,
                    TotalDistanceMiles = 0.621371,
                    UsedTime = 60.0
                },
                new DistanceSummary
                {
                    GameName = "LMU",
                    CarModel = "Akkodis ASP Team 2026",
                    TrackName = "Circuit de la Sarthe",
                    TrackNameWithConfig = "Circuit de la Sarthe",
                    TrackDisplayName = "Circuit de la Sarthe",
                    TotalDistanceKm = 2.0,
                    TotalDistanceMiles = 1.242742,
                    UsedTime = 120.0
                }
            }, displayInMiles: false, assettoCorsaTrackMap: null);

            TrackDistanceSummary accTrackSummary = snapshot.GameTabs
                .Single(tab => tab.GameName == "Assetto Corsa Competizione")
                .TrackSummaries
                .Single();
            TrackDistanceSummary lmuTrackSummary = snapshot.GameTabs
                .Single(tab => tab.GameName == "LMU")
                .TrackSummaries
                .Single();

            Assert.AreEqual("Brands Hatch Circuit", accTrackSummary.CircuitNameDisplay);
            Assert.AreEqual("Brands Hatch Circuit", accTrackSummary.CircuitLayoutDisplay);
            Assert.AreEqual("Circuit de la Sarthe", lmuTrackSummary.CircuitNameDisplay);
            Assert.AreEqual("Circuit de la Sarthe", lmuTrackSummary.CircuitLayoutDisplay);
        }

        [TestMethod]
        public void BuildSnapshot_UsesProfilesForDisplayWithoutChangingRawTrackIdentity()
        {
            DistanceSummary row = new DistanceSummary
            {
                GameName = "AssettoCorsa",
                CarModel = "Mazda MX-5 Cup",
                TrackName = "ks_brands_hatch",
                TrackNameWithConfig = "ks_brands_hatch-indy",
                TotalDistanceKm = 10.0
            };
            Dictionary<string, string> map = new Dictionary<string, string>
            {
                ["ks_brands_hatch-indy"] = "Brands Hatch - Indy"
            };

            AffinitySummarySnapshot snapshot = AffinitySummaryBuilder.BuildSnapshot(new[] { row }, false, map);
            TrackDistanceSummary track = snapshot.GameTabs.Single().TrackSummaries.Single();

            Assert.AreEqual("ks_brands_hatch-indy", track.TrackName);
            Assert.AreEqual("Brands Hatch - Indy", track.TrackDisplayName);
            Assert.AreEqual("Brands Hatch - Indy", track.CircuitNameDisplay);
            Assert.AreEqual("Brands Hatch - Indy", track.CircuitLayoutDisplay);
        }

        [TestMethod]
        public void BuildSnapshot_UsesProvidedProfileRegistryAcrossTabRebuilds()
        {
            DistanceSummary row = new DistanceSummary
            {
                GameName = "CustomGame",
                CarModel = "Test Car",
                TrackName = "raw-track",
                TrackNameWithConfig = "raw-track-layout",
                TotalDistanceKm = 1.0
            };
            AffinityGameProfileRegistry registry = new AffinityGameProfileRegistry(new[]
            {
                new TestDisplayProfile()
            });

            AffinitySummarySnapshot snapshot = AffinitySummaryBuilder.BuildSnapshot(
                new[] { row },
                displayInMiles: false,
                assettoCorsaTrackMap: null,
                gameProfiles: registry);
            GameDistanceTab tab = snapshot.GameTabs.Single();
            TrackDistanceSummary track = snapshot.FeaturedTrackSummary;

            Assert.AreEqual("raw-track-layout", track.TrackName);
            Assert.AreEqual("Profile Track", track.TrackDisplayName);
            Assert.AreEqual("Profile Circuit", track.CircuitNameDisplay);
            Assert.AreEqual("Profile Layout", track.CircuitLayoutDisplay);
            AssertProfileCircuitDisplay(tab.VisibleTrackSummaries.Single());
            AssertProfileCircuitDisplay(tab.TopTrackSummary);

            tab.ApplyCarFilter("Test Car");

            AssertProfileCircuitDisplay(tab.VisibleTrackSummaries.Single());
            AssertProfileCircuitDisplay(tab.TopTrackSummary);
        }

        [TestMethod]
        public void BuildSnapshot_PopulatesResolvedGameLogoState()
        {
            Dictionary<string, string> logoPaths = new Dictionary<string, string>
            {
                ["Assetto Corsa"] = @"C:\SimHub\Logos\244210.jpg",
                ["iRacing"] = @"C:\SimHub\Logos\iRacing.jpg"
            };

            AffinitySummarySnapshot snapshot = AffinitySummaryBuilder.BuildSnapshot(
                AffinitySummaryBuilder.BuildDistanceSummaries(new AffinityDatabase
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
                }),
                displayInMiles: false,
                assettoCorsaTrackMap: null,
                tryResolveGameLogoPath: gameName => logoPaths.TryGetValue(gameName, out string path) ? path : null);

            GameDistanceTab assettoTab = snapshot.GameTabs.Single(tab => tab.GameName == "Assetto Corsa");
            GameDistanceTab iracingTab = snapshot.GameTabs.Single(tab => tab.GameName == "iRacing");

            Assert.AreEqual(@"C:\SimHub\Logos\244210.jpg", assettoTab.GameLogoPath);
            Assert.AreEqual(@"C:\SimHub\Logos\iRacing.jpg", iracingTab.GameLogoPath);
            Assert.AreEqual(assettoTab.GameLogoPath, snapshot.FeaturedGameTab.GameLogoPath);
        }

        private sealed class TestDisplayProfile : AffinityGameProfileBase
        {
            public TestDisplayProfile()
                : base("customgame", "Custom Game", "custom.jpg", "CustomGame")
            {
            }

            public override string GetTrackDisplayName(
                string rawTrackNameWithConfig,
                AffinityTrackDisplayContext context)
            {
                return "Profile Track";
            }

            public override CircuitDisplayParts GetCircuitDisplayParts(string trackDisplayName)
            {
                return new CircuitDisplayParts
                {
                    CircuitNameDisplay = "Profile Circuit",
                    CircuitLayoutDisplay = "Profile Layout"
                };
            }
        }

        private static void AssertProfileCircuitDisplay(TrackDistanceSummary track)
        {
            Assert.AreEqual("raw-track-layout", track.TrackName);
            Assert.AreEqual("Profile Track", track.TrackDisplayName);
            Assert.AreEqual("Profile Circuit", track.CircuitNameDisplay);
            Assert.AreEqual("Profile Layout", track.CircuitLayoutDisplay);
        }
    }
}
