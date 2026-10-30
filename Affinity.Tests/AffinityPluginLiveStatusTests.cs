using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
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
        public void RefreshLiveSummariesIfNeeded_PreservesPatternsWithoutQueryingSessionHistory(
            double pendingMeters, double pendingSeconds, bool force, bool attachClosedRepository)
        {
            string directory = Path.Combine(Path.GetTempPath(), "AffinityTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var plugin = new AffinityPlugin();
                var pattern = new AffinityPatternSnapshot { HasSufficientData = true };
                SetProperty(plugin, "DrivingPatternSnapshot", pattern);
                SetField(plugin, "_pendingMetersSinceSave", pendingMeters);
                SetField(plugin, "_pendingUsedTimeSecondsSinceSave", pendingSeconds);
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
