using Affinity;
using GameReaderCommon;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;

namespace Affinity.Tests
{
    [TestClass]
    public class AffinityGameLogicTests
    {
        [TestMethod]
        public void NormalizeGameName_RemovesPunctuationAndLowercases()
        {
            string normalized = AffinityGameLogic.NormalizeGameName(" RaceRoom Racing Experience! ");

            Assert.AreEqual("raceroomracingexperience", normalized);
        }

        [TestMethod]
        public void GetDebugLoggingSettingsKey_MapsRaceRoomAliases()
        {
            Assert.AreEqual("raceroomracingexperience", AffinityGameLogic.GetDebugLoggingSettingsKey("R3E"));
            Assert.AreEqual("raceroomracingexperience", AffinityGameLogic.GetDebugLoggingSettingsKey("rrre"));
        }

        [TestMethod]
        public void GetDebugLoggingSettingsKey_UsesProjectMotorRacingRuntimeKey()
        {
            Assert.AreEqual("projectmotorracing", AffinityGameLogic.GetDebugLoggingSettingsKey("ProjectMotorRacing"));
        }

        [TestMethod]
        public void IsSupportedGame_RecognizesConfiguredTitlesAndAliases()
        {
            Assert.IsTrue(AffinityGameLogic.IsSupportedGame("Assetto Corsa EVO"));
            Assert.IsTrue(AffinityGameLogic.IsSupportedGame("LMU"));
            Assert.IsTrue(AffinityGameLogic.IsSupportedGame("ProjectMotorRacing"));
            Assert.IsTrue(AffinityGameLogic.IsSupportedGame("r3e"));
            Assert.IsFalse(AffinityGameLogic.IsSupportedGame("BeamNG.drive"));
        }

        [TestMethod]
        public void GetTrackPositionWithinLapMeters_UsesPercentFallbackAndClampsToTrackLength()
        {
            TestStatusData percentFallbackStatus = new TestStatusData
            {
                TrackPositionMeters = -1.0
            };
            SetTrackPositionPercent(percentFallbackStatus, 25.0);

            TestStatusData clampStatus = new TestStatusData
            {
                TrackPositionMeters = 4000.5
            };
            SetTrackPositionPercent(clampStatus, 0.0);

            TestStatusData passthroughStatus = new TestStatusData
            {
                TrackPositionMeters = 4505.0
            };
            SetTrackPositionPercent(passthroughStatus, 0.0);

            Assert.AreEqual(1000.0, AffinityGameLogic.GetTrackPositionWithinLapMeters(percentFallbackStatus, 4000.0), 0.001);
            Assert.AreEqual(4000.0, AffinityGameLogic.GetTrackPositionWithinLapMeters(clampStatus, 4000.0), 0.001);
            Assert.AreEqual(4505.0, AffinityGameLogic.GetTrackPositionWithinLapMeters(passthroughStatus, 4000.0), 0.001);
        }

        [TestMethod]
        public void HasReliableTelemetryContext_RequiresKnownCarAndTrackForLmu()
        {
            Assert.IsFalse(AffinityGameLogic.HasReliableTelemetryContext("LMU", "Unknown Car", "Fuji Speedway"));
            Assert.IsFalse(AffinityGameLogic.HasReliableTelemetryContext("LMU", "Akkodis ASP Team 2026", "Unknown Track"));
            Assert.IsTrue(AffinityGameLogic.HasReliableTelemetryContext("LMU", "Akkodis ASP Team 2026", "Fuji Speedway"));
            Assert.IsTrue(AffinityGameLogic.HasReliableTelemetryContext("Assetto Corsa", "Unknown Car", "Unknown Track"));
        }

        private static void SetTrackPositionPercent(StatusDataBase status, double value)
        {
            typeof(StatusDataBase)
                .GetProperty("TrackPositionPercent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
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
