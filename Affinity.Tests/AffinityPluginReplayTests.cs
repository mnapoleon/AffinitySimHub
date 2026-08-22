using System;
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
    public class AffinityPluginReplayTests
    {
        [TestMethod]
        public void IsReplay_ReturnsTrueWhenGameDataIsGameReplayIsTrue()
        {
            GameData data = new ReplayFlagGameData { IsGameReplay = true };

            Assert.IsTrue(AffinityReplayDetector.IsReplay(data));
        }

        [TestMethod]
        public void IsReplay_ReturnsTrueWhenGameDataGameReplayIsTrue()
        {
            GameData data = new GameData();
            SetMemberValue(data, "GameReplay", true);

            Assert.IsTrue(AffinityReplayDetector.IsReplay(data));
        }

        [TestMethod]
        public void IsReplay_ReturnsTrueWhenGameDataReplayModeIsActive()
        {
            GameData data = new ReplayModeGameData { ReplayMode = "Playing" };

            Assert.IsTrue(AffinityReplayDetector.IsReplay(data));
        }

        [DataTestMethod]
        [DataRow("None")]
        [DataRow("Off")]
        [DataRow("Disabled")]
        [DataRow("Live")]
        public void IsReplay_ReturnsFalseWhenGameDataReplayModeIsInactive(string replayMode)
        {
            GameData data = new ReplayModeGameData { ReplayMode = replayMode };

            Assert.IsFalse(AffinityReplayDetector.IsReplay(data));
        }

        [TestMethod]
        public void IsReplay_ReturnsTrueWhenStatusIsGameReplayIsTrue()
        {
            GameData data = CreateGameDataWithStatus(new ReplayFlagStatusData { IsGameReplay = true });

            Assert.IsTrue(AffinityReplayDetector.IsReplay(data));
        }

        [TestMethod]
        public void IsReplay_ReturnsTrueWhenStatusReplayModeIsActive()
        {
            GameData data = CreateGameDataWithStatus(CreateStatusData("Playing"));

            Assert.IsTrue(AffinityReplayDetector.IsReplay(data));
        }

        [TestMethod]
        public void IsReplay_ReturnsTrueWhenRawDataIsReplayPlaying()
        {
            GameData data = CreateGameDataWithStatus(
                new RawStatusData
                {
                    RawData = new RawReplayData { IsReplayPlaying = true }
                });

            Assert.IsTrue(AffinityReplayDetector.IsReplay(data));
        }

        [TestMethod]
        public void IsReplay_ReturnsTrueWhenNestedRawTelemetryIsReplayPlaying()
        {
            GameData data = CreateGameDataWithStatus(
                new RawStatusData
                {
                    RawData = new RawReplayData
                    {
                        Telemetry = new RawReplayTelemetry
                        {
                            IsReplayPlaying = true
                        }
                    }
                });

            Assert.IsTrue(AffinityReplayDetector.IsReplay(data));
        }

        [TestMethod]
        public void DataUpdate_ClearsAutomobilista2ViewedParticipantIndexWhenGameStops()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            AffinityGameRuntimeState runtimeState = GetRuntimeState(plugin);
            IAffinityGameProfile profile = AffinityGameProfileRegistry.CreateDefault().Resolve("Automobilista2");
            StatusDataBase playerStatus = new RawStatusData
            {
                RawData = new Automobilista2RawData { mViewedParticipantIndex = 3 }
            };
            StatusDataBase nextRacePlayerStatus = new RawStatusData
            {
                RawData = new Automobilista2RawData { mViewedParticipantIndex = 7 }
            };
            GameData stoppedData = new GameData();
            SetMemberValue(stoppedData, "GameRunning", false);

            Assert.AreEqual(
                TelemetryDisposition.Active,
                profile.EvaluateTelemetry(CreateTelemetryContext(playerStatus, runtimeState)));
            Assert.AreEqual(3, runtimeState.Automobilista2PlayerViewedParticipantIndex);

            plugin.DataUpdate(CreatePluginManager(), ref stoppedData);

            Assert.AreEqual(-1, runtimeState.Automobilista2PlayerViewedParticipantIndex);
            Assert.AreEqual(
                TelemetryDisposition.Active,
                profile.EvaluateTelemetry(CreateTelemetryContext(nextRacePlayerStatus, runtimeState)));
            Assert.AreEqual(7, runtimeState.Automobilista2PlayerViewedParticipantIndex);
        }

        [TestMethod]
        public void LogTelemetryDebugSnapshot_IncludesAutomobilista2LearnedPlayerViewedParticipantIndex()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string debugLogPath = Path.Combine(tempDirectory, "Affinity.distance.debug.log");
            string gameDebugLogPath = Path.Combine(tempDirectory, "Affinity.distance.debug.automobilista2.log");
            try
            {
                AffinityPlugin plugin = new AffinityPlugin();
                plugin.Settings.EnableDebugLogging = true;
                plugin.Settings.GameDebugLogging["automobilista2"] = true;
                SetMemberValue(plugin, "_debugLogPath", debugLogPath);

                AffinityGameRuntimeState runtimeState = GetRuntimeState(plugin);
                StatusDataBase status = new RawStatusData
                {
                    RawData = new Automobilista2RawData { mViewedParticipantIndex = 3 }
                };
                GameData data = CreateGameDataWithStatus(status);
                IAffinityGameProfile profile = AffinityGameProfileRegistry.CreateDefault().Resolve("Automobilista2");

                Assert.AreEqual(
                    TelemetryDisposition.Active,
                    profile.EvaluateTelemetry(CreateTelemetryContext(data, status, runtimeState)));

                typeof(AffinityPlugin)
                    .GetMethod("LogTelemetryDebugSnapshot", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(
                        plugin,
                        new object[]
                        {
                            "test",
                            data,
                            "Automobilista2",
                            "Porsche 996 GT3 RSR",
                            "OultonPark-OultonPark_Fosters",
                            Guid.NewGuid(),
                            status,
                            0.0,
                            0.0,
                            0,
                            false
                        });

                string line = File.ReadAllText(gameDebugLogPath);
                Assert.IsTrue(line.Contains("ams2PlayerViewedParticipantIndex=3"), line);
            }
            finally
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, recursive: true);
                }
            }
        }

        private static AffinityGameRuntimeState GetRuntimeState(AffinityPlugin plugin)
        {
            FieldInfo field = typeof(AffinityPlugin)
                .GetField("_gameRuntimeState", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            return (AffinityGameRuntimeState)field.GetValue(plugin);
        }

        private static AffinityTelemetryContext CreateTelemetryContext(
            StatusDataBase status,
            AffinityGameRuntimeState runtimeState)
        {
            return CreateTelemetryContext(CreateGameDataWithStatus(status), status, runtimeState);
        }

        private static AffinityTelemetryContext CreateTelemetryContext(
            GameData data,
            StatusDataBase status,
            AffinityGameRuntimeState runtimeState)
        {
            return new AffinityTelemetryContext
            {
                GameData = data,
                Status = status,
                CarModel = "Test Car",
                TrackNameWithConfig = "Test Track",
                RuntimeState = runtimeState
            };
        }

        private static GameData CreateGameDataWithStatus(StatusDataBase status)
        {
            GameData data = new GameData();
            SetMemberValue(data, "NewData", status);
            return data;
        }

        private static StatusDataBase CreateStatusData(string replayMode)
        {
            TestStatusData status = new TestStatusData();
            SetMemberValue(status, "ReplayMode", replayMode);
            return status;
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
            if (field != null)
            {
                field.SetValue(instance, value);
                return;
            }

            Assert.Fail($"Expected {instance.GetType().Name} to expose writable member {memberName}.");
        }

        private static PluginManager CreatePluginManager()
        {
            return (PluginManager)FormatterServices.GetUninitializedObject(typeof(PluginManager));
        }

        private sealed class ReplayFlagGameData : GameData
        {
            public bool IsGameReplay { get; set; }
        }

        private sealed class ReplayModeGameData : GameData
        {
            public object ReplayMode { get; set; }
        }

        private sealed class ReplayFlagStatusData : StatusDataBase
        {
            public new bool IsGameReplay { get; set; }

            public override object GetRawDataObject()
            {
                return null;
            }
        }

        private sealed class RawStatusData : StatusDataBase
        {
            public object RawData { get; set; }

            public override object GetRawDataObject()
            {
                return RawData;
            }
        }

        private sealed class Automobilista2RawData
        {
            public int mViewedParticipantIndex;
        }

        private sealed class RawReplayData
        {
            public bool IsReplayPlaying { get; set; }

            public RawReplayTelemetry Telemetry { get; set; }
        }

        private sealed class RawReplayTelemetry
        {
            public bool IsReplayPlaying { get; set; }
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
