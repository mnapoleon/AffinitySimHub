using System;
using System.Reflection;
using Affinity;
using GameReaderCommon;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Affinity.Tests
{
    [TestClass]
    public class AffinityPluginDistanceSourceTests
    {
        [TestMethod]
        public void ResolveSessionDistanceSource_UsesDerivedDistanceForLmu()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            TestStatusData status = new TestStatusData();
            SetProperty(status, "TrackLength", 5000.0);
            SetProperty(status, "SessionOdo", 12.34);

            object result = typeof(AffinityPlugin)
                .GetMethod("ResolveSessionDistanceSource", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { "LMU", status });

            Assert.AreEqual("Derived", result.ToString());
        }

        [TestMethod]
        public void LooksLikeIgnoredLapIncrement_IgnoresLowSpeedLineIncrementForLmu()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            TestStatusData status = new TestStatusData();

            SetProperty(status, "CompletedLaps", 4);
            SetProperty(status, "TrackPositionMeters", 85.41);
            SetProperty(status, "TrackPositionPercent", 0.01883);
            SetProperty(status, "SpeedKmh", 0.12);
            typeof(AffinityPlugin)
                .GetField("_lastObservedSessionMeters", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(plugin, 13529.19);

            object result = typeof(AffinityPlugin)
                .GetMethod("LooksLikeIgnoredLapIncrement", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { "LMU", status, 4, 1, 4535.80 });

            Assert.AreEqual(true, result);
        }

        [TestMethod]
        public void ShouldIgnoreDistanceJumpForIgnoredLapIncrement_IgnoresLmuExitLapDistance()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            TestStatusData status = new TestStatusData();

            SetProperty(status, "CompletedLaps", 4);
            SetProperty(status, "TrackPositionMeters", 102.70);
            SetProperty(status, "TrackPositionPercent", 0.02403);
            SetProperty(status, "SpeedKmh", 0.04);
            typeof(AffinityPlugin)
                .GetField("_lastObservedSessionMeters", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(plugin, 12764.38);

            object result = typeof(AffinityPlugin)
                .GetMethod("ShouldIgnoreDistanceJumpForIgnoredLapIncrement", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { "LMU", status, 4, 1, 4273.22, 4328.51 });

            Assert.AreEqual(true, result);
        }

        [TestMethod]
        public void ShouldIgnoreRepeatedIgnoredDistanceJump_MatchesSameInflatedSessionDistance()
        {
            AffinityPlugin plugin = new AffinityPlugin();

            typeof(AffinityPlugin)
                .GetField("_lastIgnoredSessionMeters", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(plugin, 17092.89);

            object result = typeof(AffinityPlugin)
                .GetMethod("ShouldIgnoreRepeatedIgnoredDistanceJump", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { 17092.89 });

            Assert.AreEqual(true, result);
        }

        [TestMethod]
        public void ShouldIgnorePlaceholderSessionStart_IgnoresLmuPostExitPlaceholder()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            TestStatusData status = new TestStatusData();

            SetProperty(status, "CompletedLaps", 4);
            SetProperty(status, "TrackLength", 4273.22);
            SetProperty(status, "TrackPositionMeters", 102.69);
            SetProperty(status, "TrackPositionPercent", 0.02403);
            SetProperty(status, "SpeedKmh", 0.01);
            typeof(AffinityPlugin)
                .GetField("_lastIgnoredSessionMeters", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(plugin, 17092.89);

            object result = typeof(AffinityPlugin)
                .GetMethod("ShouldIgnorePlaceholderSessionStart", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { "LMU", status, 4 });

            Assert.AreEqual(true, result);
        }

        [TestMethod]
        public void ShouldIgnorePlaceholderSessionStart_IgnoresLmuNegativeLapBoundarySentinel()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            TestStatusData status = new TestStatusData();

            SetProperty(status, "CompletedLaps", 4);
            SetProperty(status, "TrackLength", 4900.67);
            SetProperty(status, "TrackPositionMeters", -4900.67);
            SetProperty(status, "TrackPositionPercent", -1.0);
            SetProperty(status, "SpeedKmh", 0.0);
            typeof(AffinityPlugin)
                .GetField("_lastIgnoredSessionMeters", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(plugin, 19602.70);

            object result = typeof(AffinityPlugin)
                .GetMethod("ShouldIgnorePlaceholderSessionStart", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { "LMU", status, 4 });

            Assert.AreEqual(true, result);
        }

        [TestMethod]
        public void ShouldIgnorePlaceholderSessionStart_IgnoresLmuResetSessionOdoPlaceholder()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            TestStatusData status = new TestStatusData();

            SetProperty(status, "CompletedLaps", 4);
            SetProperty(status, "TrackLength", 5386.80);
            SetProperty(status, "TrackPositionMeters", 91.63);
            SetProperty(status, "TrackPositionPercent", 0.01701);
            SetProperty(status, "SpeedKmh", 0.01);
            SetProperty(status, "SessionOdo", 0.00006);

            object result = typeof(AffinityPlugin)
                .GetMethod("ShouldIgnorePlaceholderSessionStart", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { "LMU", status, 4 });

            Assert.AreEqual(true, result);
        }

        [TestMethod]
        public void ShouldPersistFinalizedSession_IgnoresEffectivelyEmptySession()
        {
            object result = typeof(AffinityPlugin)
                .GetMethod("ShouldPersistFinalizedSession", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { 0.5, 0.5 });

            Assert.AreEqual(false, result);
        }

        [TestMethod]
        public void ShouldPersistFinalizedSession_PersistsMeaningfulSession()
        {
            object result = typeof(AffinityPlugin)
                .GetMethod("ShouldPersistFinalizedSession", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { 1.5, 0.0 });

            Assert.AreEqual(true, result);
        }

        private static void SetProperty(StatusDataBase status, string propertyName, object value)
        {
            typeof(StatusDataBase)
                .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(status, value);
        }

        private sealed class TestStatusData : StatusDataBase
        {
            public override object GetRawDataObject()
            {
                return null;
            }
        }
    }
}
