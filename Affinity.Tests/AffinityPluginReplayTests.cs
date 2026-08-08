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
        public void IsReplayTelemetry_ReturnsTrueWhenIsGameReplayIsTrue()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            GameData data = CreateReplayFlagGameData(isGameReplay: true);

            object result = typeof(AffinityPlugin)
                .GetMethod("IsReplayTelemetry", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { data });

            Assert.AreEqual(true, result);
        }

        [TestMethod]
        public void IsReplayTelemetry_ReturnsTrueWhenReplayModeIsActive()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            GameData data = CreateReplayModeGameData("Playing");

            object result = typeof(AffinityPlugin)
                .GetMethod("IsReplayTelemetry", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { data });

            Assert.AreEqual(true, result);
        }

        [TestMethod]
        public void IsReplayTelemetry_ReturnsFalseWhenReplayModeIsLive()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            GameData data = CreateReplayModeGameData("Live");

            object result = typeof(AffinityPlugin)
                .GetMethod("IsReplayTelemetry", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { data });

            Assert.AreEqual(false, result);
        }

        [TestMethod]
        public void IsReplayTelemetry_ReturnsTrueWhenStatusIsGameReplayIsTrue()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            GameData data = new GameData();
            SetMemberValue(data, "NewData", new ReplayFlagStatusData { IsGameReplay = true });

            object result = typeof(AffinityPlugin)
                .GetMethod("IsReplayTelemetry", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { data });

            Assert.AreEqual(true, result);
        }

        [TestMethod]
        public void IsRaceRoomInactiveTelemetry_ReturnsTrueWhenFinishStatusIsNonZero()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            StatusDataBase status = new RawFinishStatusStatusData(new RawFinishStatusData { FinishStatus = 1 });

            object result = typeof(AffinityPlugin)
                .GetMethod("IsInactiveTelemetry", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { "RRRE", status });

            Assert.AreEqual(true, result);
        }

        [TestMethod]
        public void IsRaceRoomInactiveTelemetry_ReturnsFalseWhenFinishStatusIsZero()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            StatusDataBase status = new RawFinishStatusStatusData(new RawFinishStatusData { FinishStatus = 0 });

            object result = typeof(AffinityPlugin)
                .GetMethod("IsInactiveTelemetry", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { "RRRE", status });

            Assert.AreEqual(false, result);
        }

        [TestMethod]
        public void IsRaceRoomInactiveTelemetry_ReturnsTrueWhenPlayerIsInGarage()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            StatusDataBase status = new RawFinishStatusStatusData(new RawFinishStatusData
            {
                FinishStatus = 0,
                GamePlayerInGarage = 1
            });

            object result = typeof(AffinityPlugin)
                .GetMethod("IsInactiveTelemetry", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { "RRRE", status });

            Assert.AreEqual(true, result);
        }

        [TestMethod]
        public void IsInactiveTelemetry_ReturnsTrueForAutomobilista2GarageTelemetry()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            StatusDataBase status = new InactiveStatusData
            {
                IsInGarage = true
            };

            object result = typeof(AffinityPlugin)
                .GetMethod("IsInactiveTelemetry", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { "Automobilista2", status });

            Assert.AreEqual(true, result);
        }

        [TestMethod]
        public void IsInactiveTelemetry_ReturnsTrueForAutomobilista2SpectatorTelemetry()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            StatusDataBase status = new InactiveStatusData
            {
                IsSpectator = true
            };

            object result = typeof(AffinityPlugin)
                .GetMethod("IsInactiveTelemetry", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { "Automobilista2", status });

            Assert.AreEqual(true, result);
        }

        [TestMethod]
        public void IsInactiveTelemetry_ReturnsTrueForAutomobilista2ViewedParticipantChange()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            StatusDataBase playerStatus = new InactiveStatusData
            {
                RawData = new Automobilista2RawData { mViewedParticipantIndex = 3 }
            };
            StatusDataBase monitoredStatus = new InactiveStatusData
            {
                RawData = new Automobilista2RawData { mViewedParticipantIndex = 7 }
            };

            MethodInfo method = typeof(AffinityPlugin)
                .GetMethod("IsInactiveTelemetry", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.AreEqual(false, method.Invoke(plugin, new object[] { "Automobilista2", playerStatus }));
            Assert.AreEqual(true, method.Invoke(plugin, new object[] { "Automobilista2", monitoredStatus }));
        }

        [TestMethod]
        public void IsInactiveTelemetry_ReturnsTrueForAutomobilista2ReplayGameState()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            StatusDataBase status = new InactiveStatusData
            {
                RawData = new Automobilista2RawData
                {
                    mViewedParticipantIndex = 0,
                    mGameState = 6
                }
            };

            object result = typeof(AffinityPlugin)
                .GetMethod("IsInactiveTelemetry", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { "Automobilista2", status });

            Assert.AreEqual(true, result);
        }

        [TestMethod]
        public void DataUpdate_ClearsAutomobilista2ViewedParticipantIndexWhenGameStops()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            MethodInfo method = typeof(AffinityPlugin)
                .GetMethod("IsInactiveTelemetry", BindingFlags.Instance | BindingFlags.NonPublic);
            StatusDataBase playerStatus = new InactiveStatusData
            {
                RawData = new Automobilista2RawData { mViewedParticipantIndex = 3 }
            };
            StatusDataBase nextRacePlayerStatus = new InactiveStatusData
            {
                RawData = new Automobilista2RawData { mViewedParticipantIndex = 7 }
            };
            GameData stoppedData = new GameData();
            SetMemberValue(stoppedData, "GameRunning", false);

            Assert.AreEqual(false, method.Invoke(plugin, new object[] { "Automobilista2", playerStatus }));

            plugin.DataUpdate(CreatePluginManager(), ref stoppedData);

            Assert.AreEqual(false, method.Invoke(plugin, new object[] { "Automobilista2", nextRacePlayerStatus }));
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

                StatusDataBase status = new InactiveStatusData
                {
                    RawData = new Automobilista2RawData { mViewedParticipantIndex = 3 }
                };
                GameData data = new GameData();
                SetMemberValue(data, "NewData", status);
                typeof(AffinityPlugin)
                    .GetMethod("IsInactiveTelemetry", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(plugin, new object[] { "Automobilista2", status });

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

        private static GameData CreateReplayFlagGameData(bool isGameReplay)
        {
            ReplayFlagGameData data = new ReplayFlagGameData { IsGameReplay = isGameReplay };
            return data;
        }

        private static GameData CreateReplayModeGameData(string replayMode)
        {
            GameData data = new GameData();
            SetMemberValue(data, "NewData", CreateStatusData(replayMode));
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

        private sealed class ReplayFlagStatusData : StatusDataBase
        {
            public new bool IsGameReplay { get; set; }

            public override object GetRawDataObject()
            {
                return null;
            }
        }

        private sealed class RawFinishStatusStatusData : StatusDataBase
        {
            private readonly object _rawData;

            public RawFinishStatusStatusData(object rawData)
            {
                _rawData = rawData;
            }

            public override object GetRawDataObject()
            {
                return _rawData;
            }
        }

        private sealed class RawFinishStatusData
        {
            public int FinishStatus { get; set; }

            public int GamePlayerInGarage { get; set; }
        }

        private sealed class InactiveStatusData : StatusDataBase
        {
            public bool IsInGarage { get; set; }

            public bool IsSpectator { get; set; }

            public object RawData { get; set; }

            public override object GetRawDataObject()
            {
                return RawData;
            }
        }

        private sealed class Automobilista2RawData
        {
            public int mViewedParticipantIndex;

            public int mGameState;
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
