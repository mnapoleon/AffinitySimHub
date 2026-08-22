using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using Affinity;
using GameReaderCommon;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SimHub.Plugins;

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
        public void DataUpdate_DelegatesTransientResetToResolvedProfile()
        {
            RecordingDistanceProfile profile = new RecordingDistanceProfile
            {
                IgnoreTransientReset = true
            };
            AffinityPlugin plugin = CreatePlugin(profile);
            PluginManager pluginManager = CreatePluginManager();
            Guid sessionId = Guid.NewGuid();

            TestStatusData initialStatus = CreateStatus(
                completedLaps: 1,
                trackLengthMeters: 1000.0,
                trackPositionMeters: 500.0,
                speedKmh: 50.0);
            GameData initialData = CreateGameData(initialStatus, sessionId);
            plugin.DataUpdate(pluginManager, ref initialData);

            SetPrivateField(plugin, "_sessionStatefulAbsoluteMeters", 1234.0);
            SetPrivateField(plugin, "_lastObservedSessionMeters", 1234.0);
            SetPrivateField(plugin, "_lastObservedCompletedLaps", 1);

            TestStatusData resetStatus = CreateStatus(
                completedLaps: 0,
                trackLengthMeters: 1000.0,
                trackPositionMeters: 0.0,
                speedKmh: 0.0);
            GameData resetData = CreateGameData(resetStatus, sessionId);
            plugin.DataUpdate(pluginManager, ref resetData);

            Assert.AreEqual(1, profile.TransientResetContexts.Count);
            Assert.AreSame(resetStatus, profile.TransientResetContexts[0].Status);
            Assert.AreEqual(1234.0, profile.TransientResetContexts[0].LastObservedSessionMeters, 0.001);
            Assert.AreEqual(1, profile.TransientResetContexts[0].LastObservedCompletedLaps);
            Assert.AreEqual("Ignoring transient iRacing telemetry reset", plugin.DataStatus);
            Assert.AreEqual(1.234, plugin.SessionDistanceKm, 0.001);
        }

        [TestMethod]
        public void DataUpdate_DelegatesPlaceholderSessionStartToResolvedProfile()
        {
            RecordingDistanceProfile profile = new RecordingDistanceProfile
            {
                IgnorePlaceholderSessionStart = true
            };
            AffinityPlugin plugin = CreatePlugin(profile);
            PluginManager pluginManager = CreatePluginManager();
            SetPrivateField(plugin, "_sessionStatefulAbsoluteMeters", 500.0);
            SetPrivateField(plugin, "_lastIgnoredSessionMeters", 77.0);

            TestStatusData placeholderStatus = CreateStatus(
                completedLaps: 4,
                trackLengthMeters: 1000.0,
                trackPositionMeters: 2.0,
                speedKmh: 0.0);
            GameData placeholderData = CreateGameData(placeholderStatus, Guid.NewGuid());
            plugin.DataUpdate(pluginManager, ref placeholderData);

            Assert.AreEqual(1, profile.PlaceholderSessionStartContexts.Count);
            Assert.AreSame(placeholderStatus, profile.PlaceholderSessionStartContexts[0].Status);
            Assert.AreEqual(500.0, profile.PlaceholderSessionStartContexts[0].SessionStatefulAbsoluteMeters, 0.001);
            Assert.AreEqual(77.0, profile.PlaceholderSessionStartContexts[0].LastIgnoredSessionMeters, 0.001);
            Assert.AreEqual("Waiting for LMU telemetry reset after exit", plugin.DataStatus);
            Assert.IsFalse(plugin.IsTelemetryActive);
            Assert.AreEqual(77.0, GetPrivateField<double>(plugin, "_lastIgnoredSessionMeters"), 0.001);
        }

        [TestMethod]
        public void DataUpdate_ReevaluatesLapIncrementAfterDistanceMutation()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string debugLogPath = Path.Combine(tempDirectory, "Affinity.distance.debug.log");
            string gameDebugLogPath = Path.Combine(tempDirectory, "Affinity.distance.debug.test.log");
            try
            {
                RecordingDistanceProfile profile = new RecordingDistanceProfile
                {
                    UseThresholdLapIncrementDecision = true
                };
                AffinityPlugin plugin = CreatePlugin(profile);
                plugin.Settings.EnableDebugLogging = true;
                plugin.Settings.GameDebugLogging["test"] = true;
                SetPrivateField(plugin, "_debugLogPath", debugLogPath);
                PluginManager pluginManager = CreatePluginManager();
                Guid sessionId = Guid.NewGuid();

                TestStatusData initialStatus = CreateStatus(
                    completedLaps: 0,
                    trackLengthMeters: 1000.0,
                    trackPositionMeters: 999.0,
                    speedKmh: 50.0);
                GameData initialData = CreateGameData(initialStatus, sessionId);
                plugin.DataUpdate(pluginManager, ref initialData);

                SetPrivateField(plugin, "_sessionStatefulAbsoluteMeters", 999.0);
                SetPrivateField(plugin, "_lastObservedSessionMeters", 999.0);

                TestStatusData lineCrossingStatus = CreateStatus(
                    completedLaps: 1,
                    trackLengthMeters: 1000.0,
                    trackPositionMeters: 1.0,
                    speedKmh: 1.0);
                GameData lineCrossingData = CreateGameData(lineCrossingStatus, sessionId);
                plugin.DataUpdate(pluginManager, ref lineCrossingData);

                Assert.AreEqual(2, profile.LapIncrementContexts.Count);
                Assert.AreEqual(999.0, profile.LapIncrementContexts[0].LastObservedSessionMeters, 0.001);
                Assert.IsFalse(profile.LapIncrementDecisions[0]);
                Assert.AreEqual(1001.0, profile.LapIncrementContexts[1].LastObservedSessionMeters, 0.001);
                Assert.IsTrue(profile.LapIncrementDecisions[1]);
                Assert.AreEqual(1.001, plugin.SessionDistanceKm, 0.001);

                string debugLog = File.ReadAllText(gameDebugLogPath);
                Assert.IsTrue(debugLog.Contains("reason=lap-increment-ignored"), debugLog);
                Assert.IsFalse(debugLog.Contains("reason=lap-change"), debugLog);
            }
            finally
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, recursive: true);
                }
            }
        }

        [TestMethod]
        public void DataUpdate_UsesDistinctLapIncrementDecisionsAtBothStages()
        {
            RecordingDistanceProfile profile = new RecordingDistanceProfile
            {
                UseIgnoredMarkerLapIncrementDecision = true
            };
            AffinityPlugin plugin = CreatePlugin(profile);
            PluginManager pluginManager = CreatePluginManager();
            Guid sessionId = Guid.NewGuid();

            TestStatusData initialStatus = CreateStatus(
                completedLaps: 0,
                trackLengthMeters: 1000.0,
                trackPositionMeters: 100.0,
                speedKmh: 50.0);
            GameData initialData = CreateGameData(initialStatus, sessionId);
            plugin.DataUpdate(pluginManager, ref initialData);

            SetPrivateField(plugin, "_sessionStatefulAbsoluteMeters", 1000.0);
            SetPrivateField(plugin, "_lastObservedSessionMeters", 1000.0);

            TestStatusData jumpStatus = CreateStatus(
                completedLaps: 1,
                trackLengthMeters: 1000.0,
                trackPositionMeters: 600.0,
                speedKmh: 50.0);
            GameData jumpData = CreateGameData(jumpStatus, sessionId);
            plugin.DataUpdate(pluginManager, ref jumpData);

            Assert.AreEqual(2, profile.LapIncrementContexts.Count);
            Assert.AreEqual(-1.0, profile.LapIncrementContexts[0].LastIgnoredSessionMeters, 0.001);
            Assert.IsTrue(profile.LapIncrementDecisions[0]);
            Assert.AreEqual(1500.0, profile.LapIncrementContexts[1].LastIgnoredSessionMeters, 0.001);
            Assert.IsFalse(profile.LapIncrementDecisions[1]);
            Assert.AreEqual(1.0, plugin.SessionDistanceKm, 0.001);
            Assert.AreEqual(1500.0, GetPrivateField<double>(plugin, "_lastIgnoredSessionMeters"), 0.001);
            Assert.IsTrue(plugin.DataStatus.StartsWith("Recorded "), plugin.DataStatus);
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

        private static AffinityPlugin CreatePlugin(IAffinityGameProfile profile)
        {
            AffinityPlugin plugin = new AffinityPlugin();
            SetPrivateField(
                plugin,
                "_gameProfiles",
                new AffinityGameProfileRegistry(new[] { profile }));
            return plugin;
        }

        private static PluginManager CreatePluginManager()
        {
            PluginManager pluginManager =
                (PluginManager)FormatterServices.GetUninitializedObject(typeof(PluginManager));
            typeof(PluginManager)
                .GetField("GeneratedProperties", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(pluginManager, new ConcurrentDictionary<string, PropertyEntry>());

            pluginManager.AddProperty("Affinity.IsGameRunning", typeof(AffinityPlugin), false);
            pluginManager.AddProperty("Affinity.DataFilePath", typeof(AffinityPlugin), string.Empty);
            pluginManager.AddProperty("Affinity.DebugLogPath", typeof(AffinityPlugin), string.Empty);
            pluginManager.AddProperty("Affinity.GameName", typeof(AffinityPlugin), string.Empty);
            pluginManager.AddProperty("Affinity.TrackName", typeof(AffinityPlugin), string.Empty);
            pluginManager.AddProperty("Affinity.CarModel", typeof(AffinityPlugin), string.Empty);
            pluginManager.AddProperty("Affinity.CurrentContextDistanceKm", typeof(AffinityPlugin), 0.0);
            pluginManager.AddProperty("Affinity.CurrentContextDistanceMiles", typeof(AffinityPlugin), 0.0);
            pluginManager.AddProperty("Affinity.SessionDistanceKm", typeof(AffinityPlugin), 0.0);
            pluginManager.AddProperty("Affinity.SessionDistanceMiles", typeof(AffinityPlugin), 0.0);
            return pluginManager;
        }

        private static TestStatusData CreateStatus(
            int completedLaps,
            double trackLengthMeters,
            double trackPositionMeters,
            double speedKmh)
        {
            TestStatusData status = new TestStatusData();
            SetProperty(status, "CarModel", "Test Car");
            SetProperty(status, "TrackName", "Test Track");
            SetProperty(status, "CompletedLaps", completedLaps);
            SetProperty(status, "TrackLength", trackLengthMeters);
            SetProperty(status, "TrackPositionMeters", trackPositionMeters);
            SetProperty(status, "TrackPositionPercent", trackPositionMeters / trackLengthMeters);
            SetProperty(status, "SpeedKmh", speedKmh);
            return status;
        }

        private static GameData CreateGameData(StatusDataBase status, Guid sessionId)
        {
            GameData data = new GameData();
            SetMemberValue(data, "GameRunning", true);
            SetMemberValue(data, "GameName", "Test Game");
            SetMemberValue(data, "SessionId", sessionId);
            SetMemberValue(data, "NewData", status);
            return data;
        }

        private static void SetMemberValue(object instance, string memberName, object value)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            PropertyInfo property = instance.GetType().GetProperty(memberName, flags);
            if (property != null && property.SetMethod != null)
            {
                property.SetValue(instance, value);
                return;
            }

            FieldInfo field = instance.GetType().GetField(memberName, flags);
            Assert.IsNotNull(field, $"Expected {instance.GetType().Name} to expose writable member {memberName}.");
            field.SetValue(instance, value);
        }

        private static void SetPrivateField(object instance, string fieldName, object value)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Expected {instance.GetType().Name} to expose field {fieldName}.");
            field.SetValue(instance, value);
        }

        private static T GetPrivateField<T>(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Expected {instance.GetType().Name} to expose field {fieldName}.");
            return (T)field.GetValue(instance);
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

        private sealed class RecordingDistanceProfile : AffinityGameProfileBase
        {
            public RecordingDistanceProfile()
                : base("test", "Test", "test.jpg", "Test Game")
            {
            }

            public bool IgnoreTransientReset { get; set; }

            public bool IgnorePlaceholderSessionStart { get; set; }

            public bool UseThresholdLapIncrementDecision { get; set; }

            public bool UseIgnoredMarkerLapIncrementDecision { get; set; }

            public List<AffinityDistanceSampleContext> TransientResetContexts { get; } =
                new List<AffinityDistanceSampleContext>();

            public List<AffinityDistanceSampleContext> PlaceholderSessionStartContexts { get; } =
                new List<AffinityDistanceSampleContext>();

            public List<AffinityDistanceSampleContext> LapIncrementContexts { get; } =
                new List<AffinityDistanceSampleContext>();

            public List<bool> LapIncrementDecisions { get; } = new List<bool>();

            public override bool ShouldIgnoreTransientReset(AffinityDistanceSampleContext context)
            {
                TransientResetContexts.Add(context);
                return IgnoreTransientReset;
            }

            public override bool ShouldIgnorePlaceholderSessionStart(AffinityDistanceSampleContext context)
            {
                PlaceholderSessionStartContexts.Add(context);
                return IgnorePlaceholderSessionStart;
            }

            public override bool ShouldIgnoreLapIncrement(AffinityDistanceSampleContext context)
            {
                LapIncrementContexts.Add(context);
                bool isNearLine = context.Status.TrackPositionMeters <= 5.0 ||
                    context.Status.TrackPositionMeters >= context.TrackLengthMeters - 5.0;
                bool decision = (UseThresholdLapIncrementDecision &&
                        context.LapDelta > 0 &&
                        context.CompletedLaps > 0 &&
                        context.Status.SpeedKmh < 5.0 &&
                        isNearLine &&
                        context.LastObservedSessionMeters >= context.TrackLengthMeters) ||
                    (UseIgnoredMarkerLapIncrementDecision && context.LastIgnoredSessionMeters < 0.0);
                LapIncrementDecisions.Add(decision);
                return decision;
            }
        }
    }
}
