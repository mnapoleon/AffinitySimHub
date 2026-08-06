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

        public void ResolveSessionDistanceSource_UsesDerivedDistanceForAssettoCorsaCompetizione()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            TestStatusData status = new TestStatusData();
            SetProperty(status, "TrackLength", 5793.0);
            SetProperty(status, "SessionOdo", 9.87);

            object result = typeof(AffinityPlugin)
                .GetMethod("ResolveSessionDistanceSource", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { "Assetto Corsa Competizione", status });

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
                .Invoke(plugin, new object[] { "ProjectMotorRacing", status });

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
                .Invoke(plugin, new object[] { "ProjectMotorRacing", status });

            object result = typeof(AffinityPlugin)
                .GetMethod("GetAbsoluteSessionDistanceMeters", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { "ProjectMotorRacing", status, derivedSource });

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
                .Invoke(plugin, new object[] { "RRRE", status });

            object result = typeof(AffinityPlugin)
                .GetMethod("GetAbsoluteSessionDistanceMeters", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { "RRRE", status, derivedSource });

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
                .Invoke(plugin, new object[] { "RRRE", anchorStatus, 1939.54 });

            Assert.AreEqual(0.0, (double)anchorResult, 0.001);

            TestStatusData preWrapStatus = new TestStatusData();
            SetProperty(preWrapStatus, "TrackLength", 1939.54);
            SetProperty(preWrapStatus, "CompletedLaps", 0);
            SetProperty(preWrapStatus, "TrackPositionMeters", 1919.32);
            SetProperty(preWrapStatus, "SpeedKmh", 77.76);

            object preWrapResult = typeof(AffinityPlugin)
                .GetMethod("UpdateStatefulDerivedAbsoluteSessionDistanceMeters", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { "RRRE", preWrapStatus, 1939.54 });

            Assert.AreEqual(498.12, (double)preWrapResult, 0.05);

            TestStatusData postWrapStatus = new TestStatusData();
            SetProperty(postWrapStatus, "TrackLength", 1939.54);
            SetProperty(postWrapStatus, "CompletedLaps", 0);
            SetProperty(postWrapStatus, "TrackPositionMeters", 0.28);
            SetProperty(postWrapStatus, "SpeedKmh", 78.24);

            object postWrapResult = typeof(AffinityPlugin)
                .GetMethod("UpdateStatefulDerivedAbsoluteSessionDistanceMeters", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { "RRRE", postWrapStatus, 1939.54 });

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
                .Invoke(plugin, new object[] { "RRRE", anchorStatus, 20785.39 });

            Assert.AreEqual(0.0, (double)anchorResult, 0.001);

            TestStatusData lineCrossingStatus = new TestStatusData();
            SetProperty(lineCrossingStatus, "TrackLength", 20785.39);
            SetProperty(lineCrossingStatus, "CompletedLaps", 1);
            SetProperty(lineCrossingStatus, "TrackPositionMeters", 0.64);
            SetProperty(lineCrossingStatus, "SpeedKmh", 145.73);

            object lineCrossingResult = typeof(AffinityPlugin)
                .GetMethod("UpdateStatefulDerivedAbsoluteSessionDistanceMeters", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { "RRRE", lineCrossingStatus, 20785.39 });

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
                .Invoke(plugin, new object[] { "LMU", status });

            object result = typeof(AffinityPlugin)
                .GetMethod("GetAbsoluteSessionDistanceMeters", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { "LMU", status, derivedSource });

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
                .Invoke(plugin, new object[] { "LMU", preSkipStatus, 4900.67 });

            Assert.AreEqual(0.0, (double)preSkipResult, 0.001);

            TestStatusData postSkipStatus = new TestStatusData();
            SetProperty(postSkipStatus, "TrackLength", 4900.67);
            SetProperty(postSkipStatus, "CompletedLaps", 0);
            SetProperty(postSkipStatus, "TrackPositionMeters", 977.42);
            SetProperty(postSkipStatus, "TrackPositionPercent", 0.19945);
            SetProperty(postSkipStatus, "SpeedKmh", 152.50);

            object postSkipResult = typeof(AffinityPlugin)
                .GetMethod("UpdateStatefulDerivedAbsoluteSessionDistanceMeters", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { "LMU", postSkipStatus, 4900.67 });

            Assert.AreEqual(1137.37, (double)postSkipResult, 0.05);

            TestStatusData resumedStatus = new TestStatusData();
            SetProperty(resumedStatus, "TrackLength", 4900.67);
            SetProperty(resumedStatus, "CompletedLaps", 0);
            SetProperty(resumedStatus, "TrackPositionMeters", 1836.57);
            SetProperty(resumedStatus, "TrackPositionPercent", 0.37476);
            SetProperty(resumedStatus, "SpeedKmh", 135.62);

            object resumedResult = typeof(AffinityPlugin)
                .GetMethod("UpdateStatefulDerivedAbsoluteSessionDistanceMeters", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { "LMU", resumedStatus, 4900.67 });

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
                .Invoke(plugin, new object[] { "ProjectMotorRacing", placeholderStatus, 2462.0 });

            Assert.AreEqual(0.0, (double)placeholderResult, 0.001);

            TestStatusData flickerToLineStatus = new TestStatusData();
            SetProperty(flickerToLineStatus, "TrackLength", 2462.0);
            SetProperty(flickerToLineStatus, "CompletedLaps", 0);
            SetProperty(flickerToLineStatus, "TrackPositionMeters", 0.0);
            SetProperty(flickerToLineStatus, "SpeedKmh", 0.15);

            object flickerResult = typeof(AffinityPlugin)
                .GetMethod("UpdateStatefulDerivedAbsoluteSessionDistanceMeters", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { "ProjectMotorRacing", flickerToLineStatus, 2462.0 });

            Assert.AreEqual(0.0, (double)flickerResult, 0.001);

            TestStatusData movingStatus = new TestStatusData();
            SetProperty(movingStatus, "TrackLength", 2462.0);
            SetProperty(movingStatus, "CompletedLaps", 0);
            SetProperty(movingStatus, "TrackPositionMeters", 79.19);
            SetProperty(movingStatus, "SpeedKmh", 13.91);

            object movingResult = typeof(AffinityPlugin)
                .GetMethod("UpdateStatefulDerivedAbsoluteSessionDistanceMeters", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { "ProjectMotorRacing", movingStatus, 2462.0 });

            Assert.AreEqual(1.33, (double)movingResult, 0.05);
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
