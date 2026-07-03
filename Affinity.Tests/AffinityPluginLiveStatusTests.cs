using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using Affinity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Affinity.Tests
{
    [TestClass]
    public class AffinityPluginLiveStatusTests
    {
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
