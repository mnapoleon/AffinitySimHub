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
                .Invoke(plugin, new object[] { ResolveProfile("LMU"), status });

            Assert.AreEqual("Derived", result.ToString());
        }

        [TestMethod]
        public void ResolveSessionDistanceSource_UsesDerivedDistanceForAssettoCorsaCompetizione()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            TestStatusData status = new TestStatusData();
            SetProperty(status, "TrackLength", 5793.0);
            SetProperty(status, "SessionOdo", 9.87);

            object result = typeof(AffinityPlugin)
                .GetMethod("ResolveSessionDistanceSource", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { ResolveProfile("Assetto Corsa Competizione"), status });

            Assert.AreEqual("Derived", result.ToString());
        }

        [TestMethod]
        public void ResolveSessionDistanceSource_UsesDerivedDistanceForProjectMotorRacing()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            TestStatusData status = new TestStatusData();
            SetProperty(status, "TrackLength", 5000.0);
            SetProperty(status, "SessionOdo", 12.34);

            object result = typeof(AffinityPlugin)
                .GetMethod("ResolveSessionDistanceSource", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { ResolveProfile("ProjectMotorRacing"), status });

            Assert.AreEqual("Derived", result.ToString());
        }

        [TestMethod]
        public void GetAbsoluteSessionDistanceMeters_UsesStatefulDerivedDistanceForProjectMotorRacing()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            TestStatusData status = new TestStatusData();
            SetProperty(status, "TrackLength", 5000.0);
            SetProperty(status, "CompletedLaps", 1);
            SetProperty(status, "TrackPositionMeters", 1234.0);
            typeof(AffinityPlugin)
                .GetField("_sessionStatefulAbsoluteMeters", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(plugin, 8765.0);

            object derivedSource = typeof(AffinityPlugin)
                .GetMethod("ResolveSessionDistanceSource", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { ResolveProfile("ProjectMotorRacing"), status });

            object result = typeof(AffinityPlugin)
                .GetMethod("GetAbsoluteSessionDistanceMeters", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { ResolveProfile("ProjectMotorRacing"), status, derivedSource });

            Assert.AreEqual(8765.0, (double)result, 0.001);
        }

        [TestMethod]
        public void GetAbsoluteSessionDistanceMeters_UsesStatefulDerivedDistanceForRaceRoom()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            TestStatusData status = new TestStatusData();
            SetProperty(status, "TrackLength", 1939.54);
            SetProperty(status, "CompletedLaps", 0);
            SetProperty(status, "TrackPositionMeters", 1419.73);
            typeof(AffinityPlugin)
                .GetField("_sessionStatefulAbsoluteMeters", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(plugin, 518.75);

            object derivedSource = typeof(AffinityPlugin)
                .GetMethod("ResolveSessionDistanceSource", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { ResolveProfile("RRRE"), status });

            object result = typeof(AffinityPlugin)
                .GetMethod("GetAbsoluteSessionDistanceMeters", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { ResolveProfile("RRRE"), status, derivedSource });

            Assert.AreEqual(518.75, (double)result, 0.001);
        }

        [TestMethod]
        public void UpdateStatefulDerivedAbsoluteSessionDistanceMeters_KeepsRaceRoomFormationLapDistanceAcrossLineWrap()
        {
            AffinityPlugin plugin = new AffinityPlugin();

            TestStatusData anchorStatus = new TestStatusData();
            SetProperty(anchorStatus, "TrackLength", 1939.54);
            SetProperty(anchorStatus, "CompletedLaps", 0);
            SetProperty(anchorStatus, "TrackPositionMeters", 1421.20);
            SetProperty(anchorStatus, "SpeedKmh", 34.40);

            object anchorResult = typeof(AffinityPlugin)
                .GetMethod("UpdateStatefulDerivedAbsoluteSessionDistanceMeters", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { ResolveProfile("RRRE"), anchorStatus, 1939.54 });

            Assert.AreEqual(0.0, (double)anchorResult, 0.001);

            TestStatusData preWrapStatus = new TestStatusData();
            SetProperty(preWrapStatus, "TrackLength", 1939.54);
            SetProperty(preWrapStatus, "CompletedLaps", 0);
            SetProperty(preWrapStatus, "TrackPositionMeters", 1919.32);
            SetProperty(preWrapStatus, "SpeedKmh", 77.76);

            object preWrapResult = typeof(AffinityPlugin)
                .GetMethod("UpdateStatefulDerivedAbsoluteSessionDistanceMeters", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { ResolveProfile("RRRE"), preWrapStatus, 1939.54 });

            Assert.AreEqual(498.12, (double)preWrapResult, 0.05);

            TestStatusData postWrapStatus = new TestStatusData();
            SetProperty(postWrapStatus, "TrackLength", 1939.54);
            SetProperty(postWrapStatus, "CompletedLaps", 0);
            SetProperty(postWrapStatus, "TrackPositionMeters", 0.28);
            SetProperty(postWrapStatus, "SpeedKmh", 78.24);

            object postWrapResult = typeof(AffinityPlugin)
                .GetMethod("UpdateStatefulDerivedAbsoluteSessionDistanceMeters", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { ResolveProfile("RRRE"), postWrapStatus, 1939.54 });

            Assert.AreEqual(518.62, (double)postWrapResult, 0.05);
        }

        [TestMethod]
        public void UpdateStatefulDerivedAbsoluteSessionDistanceMeters_CountsRaceRoomLapIncrementAsFullLap()
        {
            AffinityPlugin plugin = new AffinityPlugin();

            TestStatusData anchorStatus = new TestStatusData();
            SetProperty(anchorStatus, "TrackLength", 20785.39);
            SetProperty(anchorStatus, "CompletedLaps", 0);
            SetProperty(anchorStatus, "TrackPositionMeters", 20753.44);
            SetProperty(anchorStatus, "SpeedKmh", 0.93);

            object anchorResult = typeof(AffinityPlugin)
                .GetMethod("UpdateStatefulDerivedAbsoluteSessionDistanceMeters", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { ResolveProfile("RRRE"), anchorStatus, 20785.39 });

            Assert.AreEqual(0.0, (double)anchorResult, 0.001);

            TestStatusData lineCrossingStatus = new TestStatusData();
            SetProperty(lineCrossingStatus, "TrackLength", 20785.39);
            SetProperty(lineCrossingStatus, "CompletedLaps", 1);
            SetProperty(lineCrossingStatus, "TrackPositionMeters", 0.64);
            SetProperty(lineCrossingStatus, "SpeedKmh", 145.73);

            object lineCrossingResult = typeof(AffinityPlugin)
                .GetMethod("UpdateStatefulDerivedAbsoluteSessionDistanceMeters", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { ResolveProfile("RRRE"), lineCrossingStatus, 20785.39 });

            Assert.AreEqual(20786.03, (double)lineCrossingResult, 0.05);
        }

        [TestMethod]
        public void GetAbsoluteSessionDistanceMeters_UsesStatefulDerivedDistanceForLmu()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            TestStatusData status = new TestStatusData();
            SetProperty(status, "TrackLength", 4900.67);
            SetProperty(status, "CompletedLaps", 1);
            SetProperty(status, "TrackPositionMeters", 846.36);
            typeof(AffinityPlugin)
                .GetField("_sessionStatefulAbsoluteMeters", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(plugin, 5747.03);

            object derivedSource = typeof(AffinityPlugin)
                .GetMethod("ResolveSessionDistanceSource", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { ResolveProfile("LMU"), status });

            object result = typeof(AffinityPlugin)
                .GetMethod("GetAbsoluteSessionDistanceMeters", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { ResolveProfile("LMU"), status, derivedSource });

            Assert.AreEqual(5747.03, (double)result, 0.001);
        }

        [TestMethod]
        public void UpdateStatefulDerivedAbsoluteSessionDistanceMeters_KeepsLmuDistanceAcrossSkippedRollingStartTeleport()
        {
            AffinityPlugin plugin = new AffinityPlugin();

            TestStatusData preSkipStatus = new TestStatusData();
            SetProperty(preSkipStatus, "TrackLength", 4900.67);
            SetProperty(preSkipStatus, "CompletedLaps", 0);
            SetProperty(preSkipStatus, "TrackPositionMeters", 4740.72);
            SetProperty(preSkipStatus, "TrackPositionPercent", 0.96736);
            SetProperty(preSkipStatus, "SpeedKmh", 213.71);

            object preSkipResult = typeof(AffinityPlugin)
                .GetMethod("UpdateStatefulDerivedAbsoluteSessionDistanceMeters", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { ResolveProfile("LMU"), preSkipStatus, 4900.67 });

            Assert.AreEqual(0.0, (double)preSkipResult, 0.001);

            TestStatusData postSkipStatus = new TestStatusData();
            SetProperty(postSkipStatus, "TrackLength", 4900.67);
            SetProperty(postSkipStatus, "CompletedLaps", 0);
            SetProperty(postSkipStatus, "TrackPositionMeters", 977.42);
            SetProperty(postSkipStatus, "TrackPositionPercent", 0.19945);
            SetProperty(postSkipStatus, "SpeedKmh", 152.50);

            object postSkipResult = typeof(AffinityPlugin)
                .GetMethod("UpdateStatefulDerivedAbsoluteSessionDistanceMeters", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { ResolveProfile("LMU"), postSkipStatus, 4900.67 });

            Assert.AreEqual(1137.37, (double)postSkipResult, 0.05);

            TestStatusData resumedStatus = new TestStatusData();
            SetProperty(resumedStatus, "TrackLength", 4900.67);
            SetProperty(resumedStatus, "CompletedLaps", 0);
            SetProperty(resumedStatus, "TrackPositionMeters", 1836.57);
            SetProperty(resumedStatus, "TrackPositionPercent", 0.37476);
            SetProperty(resumedStatus, "SpeedKmh", 135.62);

            object resumedResult = typeof(AffinityPlugin)
                .GetMethod("UpdateStatefulDerivedAbsoluteSessionDistanceMeters", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { ResolveProfile("LMU"), resumedStatus, 4900.67 });

            Assert.AreEqual(1996.52, (double)resumedResult, 0.05);
        }

        [TestMethod]
        public void UpdateStatefulDerivedAbsoluteSessionDistanceMeters_IgnoresProjectMotorRacingStartupPlaceholderBeforeCarMoves()
        {
            AffinityPlugin plugin = new AffinityPlugin();

            typeof(AffinityPlugin)
                .GetField("_lastTrackPositionWithinLapMeters", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(plugin, 0.0);

            TestStatusData placeholderStatus = new TestStatusData();
            SetProperty(placeholderStatus, "TrackLength", 2462.0);
            SetProperty(placeholderStatus, "CompletedLaps", 0);
            SetProperty(placeholderStatus, "TrackPositionMeters", 77.86);
            SetProperty(placeholderStatus, "SpeedKmh", 0.11);

            object placeholderResult = typeof(AffinityPlugin)
                .GetMethod("UpdateStatefulDerivedAbsoluteSessionDistanceMeters", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { ResolveProfile("ProjectMotorRacing"), placeholderStatus, 2462.0 });

            Assert.AreEqual(0.0, (double)placeholderResult, 0.001);

            TestStatusData flickerToLineStatus = new TestStatusData();
            SetProperty(flickerToLineStatus, "TrackLength", 2462.0);
            SetProperty(flickerToLineStatus, "CompletedLaps", 0);
            SetProperty(flickerToLineStatus, "TrackPositionMeters", 0.0);
            SetProperty(flickerToLineStatus, "SpeedKmh", 0.15);

            object flickerResult = typeof(AffinityPlugin)
                .GetMethod("UpdateStatefulDerivedAbsoluteSessionDistanceMeters", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { ResolveProfile("ProjectMotorRacing"), flickerToLineStatus, 2462.0 });

            Assert.AreEqual(0.0, (double)flickerResult, 0.001);

            TestStatusData movingStatus = new TestStatusData();
            SetProperty(movingStatus, "TrackLength", 2462.0);
            SetProperty(movingStatus, "CompletedLaps", 0);
            SetProperty(movingStatus, "TrackPositionMeters", 79.19);
            SetProperty(movingStatus, "SpeedKmh", 13.91);

            object movingResult = typeof(AffinityPlugin)
                .GetMethod("UpdateStatefulDerivedAbsoluteSessionDistanceMeters", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { ResolveProfile("ProjectMotorRacing"), movingStatus, 2462.0 });

            Assert.AreEqual(1.33, (double)movingResult, 0.05);
        }

        [TestMethod]
        public void UpdateStatefulDerivedAbsoluteSessionDistanceMeters_UsesProfileLineWrapDecision()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            typeof(AffinityPlugin)
                .GetField("_sessionStatefulAbsoluteMeters", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(plugin, 200.0);
            typeof(AffinityPlugin)
                .GetField("_lastTrackPositionWithinLapMeters", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(plugin, 1500.0);

            TestStatusData status = new TestStatusData();
            SetProperty(status, "TrackLength", 2000.0);
            SetProperty(status, "CompletedLaps", 0);
            SetProperty(status, "TrackPositionMeters", 100.0);
            SetProperty(status, "SpeedKmh", 100.0);

            object result = typeof(AffinityPlugin)
                .GetMethod("UpdateStatefulDerivedAbsoluteSessionDistanceMeters", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { new IgnoreLineWrapProfile(), status, 2000.0 });

            Assert.AreEqual(200.0, (double)result, 0.001);
        }

        [TestMethod]
        public void ShouldIgnoreDistanceJumpForIgnoredLapIncrement_IgnoresLargeJumpForCachedDecision()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            AffinityDistanceSampleContext context = new AffinityDistanceSampleContext
            {
                TrackLengthMeters = 4273.22,
                DeltaMeters = 4328.51
            };

            object result = typeof(AffinityPlugin)
                .GetMethod("ShouldIgnoreDistanceJumpForIgnoredLapIncrement", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { true, context });

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
        public void ShouldPersistFinalizedSession_IgnoresEffectivelyEmptySession()
        {
            object result = typeof(AffinityPlugin)
                .GetMethod("ShouldPersistFinalizedSession", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { 0.5, 0.5 });

            Assert.AreEqual(false, result);
        }

        [TestMethod]
        public void ShouldPersistFinalizedSession_IgnoresStationaryTimeOnlySession()
        {
            object result = typeof(AffinityPlugin)
                .GetMethod("ShouldPersistFinalizedSession", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { 0.0, 30.0 });

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

        private static IAffinityGameProfile ResolveProfile(string gameName)
        {
            return AffinityGameProfileRegistry.CreateDefault().Resolve(gameName);
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

        private sealed class IgnoreLineWrapProfile : AffinityGameProfileBase
        {
            public IgnoreLineWrapProfile()
                : base("test", "Test", "test.jpg", "Test")
            {
            }

            public override bool ShouldIgnoreLowSpeedLineWrap(AffinityDistanceSampleContext context)
            {
                return true;
            }
        }
    }
}
