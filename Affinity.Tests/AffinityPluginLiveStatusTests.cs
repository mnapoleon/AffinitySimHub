using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using Affinity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Affinity.Tests
{
    [TestClass]
    public class AffinityPluginLiveStatusTests
    {
        [DataTestMethod]
        [DataRow(50.0, 0.0, false, false)]
        [DataRow(0.0, 30.0, false, false)]
        [DataRow(0.0, 0.0, true, false)]
        [DataRow(50.0, 0.0, false, true)]
        [DataRow(0.0, 30.0, false, true)]
        [DataRow(0.0, 0.0, true, true)]
        public void RefreshLiveSummariesIfNeeded_PreservesSelectedPeriodAndPatternsWithoutQueryingHistory(
            double pendingMeters, double pendingSeconds, bool force, bool attachClosedRepository)
        {
            string directory = Path.Combine(Path.GetTempPath(), "AffinityTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var plugin = CreatePluginWithSelectedPeriod();
                var pattern = new AffinityPatternSnapshot { HasSufficientData = true };
                SetProperty(plugin, "DrivingPatternSnapshot", pattern);
                SetField(plugin, "_pendingMetersSinceSave", pendingMeters);
                SetField(plugin, "_pendingUsedTimeSecondsSinceSave", pendingSeconds);
                GetField<AffinityDatabase>(plugin, "_database").Games["iRacing"].Cars["MX-5"].Tracks["spa"].TotalDistanceMeters += 1000;
                if (attachClosedRepository)
                {
                    // A real closed repository fails immediately if the telemetry path attempts a history query.
                    var repository = new AffinitySqliteRepository(Path.Combine(directory, "closed.db"));
                    repository.Initialize();
                    repository.Dispose();
                    SetField(plugin, "_sqliteRepository", repository);
                }
                List<string> changedProperties = CaptureChangedProperties(plugin);
                MethodInfo refresh = typeof(AffinityPlugin).GetMethod("RefreshLiveSummariesIfNeeded", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(refresh);

                refresh.Invoke(plugin, new object[] { force });

                Assert.AreSame(pattern, plugin.DrivingPatternSnapshot, "Telemetry refresh must not rebuild driving patterns.");
                Assert.AreSame(plugin.SelectedTopLevelTab, plugin.SelectedGameTab);
                Assert.AreEqual("iRacing", plugin.SelectedGameTab.GameName);
                Assert.AreEqual(GameDistanceTab.TimePeriodLast30Days, plugin.SelectedGameTab.SelectedTimePeriodFilterKey);
                Assert.AreEqual(GameDistanceTab.SortByTimeDriven, plugin.SelectedGameTab.SelectedSortModeKey);
                Assert.AreEqual(GameDistanceTab.ResultLimitTop5, plugin.SelectedGameTab.SelectedResultLimitKey);
                Assert.AreEqual(14.0, plugin.SelectedGameTab.TotalDistanceKm, "Cumulative game totals must still refresh.");
                Assert.AreEqual(1, plugin.SelectedGameTab.VisibleTrackSummaries.Count);
                Assert.AreEqual("spa", plugin.SelectedGameTab.VisibleTrackSummaries[0].TrackName);
                Assert.AreEqual(1.0, plugin.SelectedGameTab.VisibleTrackSummaries[0].DistanceKm,
                    "Telemetry must retain the cached period distance, not clear it or replace it with cumulative totals.");
                Assert.AreEqual(60.0, plugin.SelectedGameTab.VisibleCarSummaries.Single().UsedTime);
                CollectionAssert.DoesNotContain(changedProperties, "DrivingPatternSnapshot");
                CollectionAssert.Contains(changedProperties, "OverallTopSummarySection", "The threshold must still refresh live summaries.");
                Assert.AreEqual(0.0, GetField<double>(plugin, "_pendingMetersSinceSave"));
                Assert.AreEqual(0.0, GetField<double>(plugin, "_pendingUsedTimeSecondsSinceSave"));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [TestMethod]
        public void RefreshDistanceSummaries_ExplicitRefreshStillRebuildsPatterns()
        {
            var plugin = new AffinityPlugin();
            var previous = new AffinityPatternSnapshot { HasSufficientData = true };
            SetProperty(plugin, "DrivingPatternSnapshot", previous);

            plugin.RefreshDistanceSummaries();

            Assert.AreNotSame(previous, plugin.DrivingPatternSnapshot);
            Assert.IsFalse(plugin.DrivingPatternSnapshot.HasSufficientData);
            Assert.AreEqual(0, plugin.DrivingPatternSnapshot.Comparison.CurrentSessionCount);
        }

        [TestMethod]
        public void RefreshDistanceSummaries_ExplicitRefreshRequeriesSelectedPeriod()
        {
            string directory = Path.Combine(Path.GetTempPath(), "AffinityTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                using (var repository = new AffinitySqliteRepository(Path.Combine(directory, "period.db")))
                {
                    repository.Initialize();
                    var plugin = CreatePluginWithSelectedPeriod();
                    SetField(plugin, "_sqliteRepository", repository);
                    DateTime startedUtc = DateTime.UtcNow.AddDays(-1);
                    repository.UpsertSession("recent", "iRacing", "MX-5", "spa", "Spa", startedUtc,
                        startedUtc.AddMinutes(2), 2000, 120);
                    repository.UpsertSession("old", "iRacing", "MX-5", "monza", "Monza", startedUtc.AddDays(-60),
                        startedUtc.AddDays(-60).AddMinutes(4), 4000, 240);
                    repository.UpsertSession("other-game", "Assetto Corsa", "MX-5", "spa", "Spa", startedUtc,
                        startedUtc.AddMinutes(6), 6000, 360);

                    plugin.RefreshDistanceSummaries();

                    Assert.AreEqual(GameDistanceTab.TimePeriodLast30Days, plugin.SelectedGameTab.SelectedTimePeriodFilterKey);
                    Assert.AreEqual(2.0, plugin.SelectedGameTab.VisibleTrackSummaries.Single().DistanceKm);
                    Assert.AreEqual(120.0, plugin.SelectedGameTab.VisibleCarSummaries.Single().UsedTime);
                    repository.UpsertSession("newer", "iRacing", "MX-5", "spa", "Spa", startedUtc.AddHours(1),
                        startedUtc.AddHours(1).AddMinutes(3), 3000, 180);

                    plugin.ApplySelectedGameTabTimeFilter();

                    Assert.AreEqual(5.0, plugin.SelectedGameTab.VisibleTrackSummaries.Single().DistanceKm);
                    Assert.AreEqual(300.0, plugin.SelectedGameTab.VisibleCarSummaries.Single().UsedTime);
                }
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        private static AffinityPlugin CreatePluginWithSelectedPeriod()
        {
            var plugin = new AffinityPlugin();
            var car = new CarBucket();
            car.Tracks.Add("spa", new TrackBucket
            {
                GameName = "iRacing", CarModel = "MX-5", TrackName = "spa", TrackNameWithConfig = "spa",
                TotalDistanceMeters = 9000, UsedTime = 900
            });
            car.Tracks.Add("monza", new TrackBucket
            {
                GameName = "iRacing", CarModel = "MX-5", TrackName = "monza", TrackNameWithConfig = "monza",
                TotalDistanceMeters = 4000, UsedTime = 400
            });
            var game = new GameBucket();
            game.Cars.Add("MX-5", car);
            GetField<AffinityDatabase>(plugin, "_database").Games.Add("iRacing", game);
            plugin.RefreshDistanceSummaries();
            plugin.SelectedTopLevelTab = plugin.GameTabs.Single();
            plugin.SelectedGameTab.SelectedTimePeriodFilterKey = GameDistanceTab.TimePeriodLast30Days;
            plugin.SelectedGameTab.SelectedSortModeKey = GameDistanceTab.SortByTimeDriven;
            plugin.SelectedGameTab.SelectedResultLimitKey = GameDistanceTab.ResultLimitTop5;
            plugin.SelectedGameTab.SetTimePeriodSummaries(new[]
            {
                new DistanceSummary
                {
                    GameName = "iRacing", CarModel = "MX-5", TrackName = "spa", TrackNameWithConfig = "spa",
                    TrackDisplayName = "Spa", TotalDistanceKm = 1, UsedTime = 60
                }
            });
            return plugin;
        }

        private static void SetField(AffinityPlugin plugin, string name, object value)
        {
            FieldInfo field = typeof(AffinityPlugin).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            field.SetValue(plugin, value);
        }

        private static T GetField<T>(AffinityPlugin plugin, string name)
        {
            FieldInfo field = typeof(AffinityPlugin).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            return (T)field.GetValue(plugin);
        }

        [TestMethod]
        public void NewPlugin_ExposesDefaultLiveStatusStripDisplays()
        {
            AffinityPlugin plugin = new AffinityPlugin();

            Assert.AreEqual("Standby", GetStringProperty(plugin, "LiveStatusLabel"));
            Assert.AreEqual("0.00 km", GetStringProperty(plugin, "CurrentSessionDistanceDisplay"));
            Assert.AreEqual("0.00 km", GetStringProperty(plugin, "CurrentContextTotalDisplay"));
            Assert.AreEqual("km", plugin.DistanceUnitLabel);
            Assert.AreEqual("Distance (km)", plugin.DistanceColumnHeader);
        }

        [TestMethod]
        public void RefreshDisplaySettings_UsesShortMileLabelsInLiveStatusStripDisplays()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            SetProperty(plugin, "SessionDistanceKm", 1.609344);
            SetProperty(plugin, "CurrentContextDistanceKm", 3.218688);

            plugin.Settings.DisplayInMiles = true;
            plugin.RefreshDisplaySettings();

            Assert.AreEqual("1.00 mi", GetStringProperty(plugin, "CurrentSessionDistanceDisplay"));
            Assert.AreEqual("2.00 mi", GetStringProperty(plugin, "CurrentContextTotalDisplay"));
            Assert.AreEqual("mi", plugin.DistanceUnitLabel);
            Assert.AreEqual("Distance (mi)", plugin.DistanceColumnHeader);
        }

        [TestMethod]
        public void IsTelemetryActiveChange_RaisesLiveStatusLabel()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            List<string> changedProperties = CaptureChangedProperties(plugin);

            SetProperty(plugin, "IsTelemetryActive", true);

            CollectionAssert.Contains(changedProperties, "LiveStatusLabel");
            Assert.AreEqual("Tracking", GetStringProperty(plugin, "LiveStatusLabel"));
        }

        [TestMethod]
        public void DistanceValueChanges_RaiseLiveStatusStripDistanceDisplays()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            List<string> changedProperties = CaptureChangedProperties(plugin);

            SetProperty(plugin, "SessionDistanceKm", 12.345);
            SetProperty(plugin, "CurrentContextDistanceKm", 67.89);

            CollectionAssert.Contains(changedProperties, "CurrentSessionDistanceDisplay");
            CollectionAssert.Contains(changedProperties, "CurrentContextTotalDisplay");
            Assert.AreEqual("12.35 km", GetStringProperty(plugin, "CurrentSessionDistanceDisplay"));
            Assert.AreEqual("67.89 km", GetStringProperty(plugin, "CurrentContextTotalDisplay"));
        }

        [TestMethod]
        public void RefreshDisplaySettings_RaisesLiveStatusStripDistanceDisplays()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            List<string> changedProperties = CaptureChangedProperties(plugin);

            plugin.Settings.DisplayInMiles = true;
            plugin.RefreshDisplaySettings();

            CollectionAssert.Contains(changedProperties, "CurrentSessionDistanceDisplay");
            CollectionAssert.Contains(changedProperties, "CurrentContextTotalDisplay");
        }

        private static List<string> CaptureChangedProperties(AffinityPlugin plugin)
        {
            List<string> propertyNames = new List<string>();
            plugin.PropertyChanged += (object sender, PropertyChangedEventArgs args) => propertyNames.Add(args.PropertyName);
            return propertyNames;
        }

        private static string GetStringProperty(AffinityPlugin plugin, string propertyName)
        {
            PropertyInfo property = typeof(AffinityPlugin).GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);

            Assert.IsNotNull(property, $"Expected AffinityPlugin to expose {propertyName}.");
            return (string)property.GetValue(plugin);
        }

        private static void SetProperty(AffinityPlugin plugin, string propertyName, object value)
        {
            PropertyInfo property = typeof(AffinityPlugin).GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.IsNotNull(property, $"Expected AffinityPlugin to expose {propertyName}.");
            property.SetValue(plugin, value);
        }
    }
}
