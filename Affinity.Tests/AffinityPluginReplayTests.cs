using System;
using System.Reflection;
using Affinity;
using GameReaderCommon;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
        public void IsRaceRoomFinishedTelemetry_ReturnsTrueWhenFinishStatusIsNonZero()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            StatusDataBase status = new RawFinishStatusStatusData(new RawFinishStatusData { FinishStatus = 1 });

            object result = typeof(AffinityPlugin)
                .GetMethod("IsRaceRoomFinishedTelemetry", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { "RRRE", status });

            Assert.AreEqual(true, result);
        }

        [TestMethod]
        public void IsRaceRoomFinishedTelemetry_ReturnsFalseWhenFinishStatusIsZero()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            StatusDataBase status = new RawFinishStatusStatusData(new RawFinishStatusData { FinishStatus = 0 });

            object result = typeof(AffinityPlugin)
                .GetMethod("IsRaceRoomFinishedTelemetry", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(plugin, new object[] { "RRRE", status });

            Assert.AreEqual(false, result);
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
