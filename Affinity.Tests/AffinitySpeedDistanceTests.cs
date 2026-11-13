using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Affinity.Tests
{
    [TestClass]
    public class AffinitySpeedDistanceTests
    {
        [TestMethod]
        public void CountsSpeedAcrossActiveSamplesAndIgnoresLongGaps()
        {
            var estimate = new AffinitySpeedDistance();
            estimate.Update(36.0, 0.0, false, 100);
            estimate.Update(72.0, 1.0, false, 101);
            Assert.AreEqual(15.0, estimate.Meters, 0.001);
            estimate.Update(72.0, 7.0, false, 102);
            Assert.AreEqual(15.0, estimate.Meters, 0.001);
            estimate.Update(72.0, 8.0, false, 103);
            Assert.AreEqual(35.0, estimate.Meters, 0.001);
        }

        [TestMethod]
        public void RepeatedFrozenSampleSequenceStopsEstimateUntilFreshTelemetryResumes()
        {
            var estimate = new AffinitySpeedDistance();
            estimate.Update(144.0, 0.0, false, 200);
            estimate.Update(144.0, 1.0, false, 201);
            Assert.AreEqual(40.0, estimate.Meters, 0.001);

            estimate.Update(144.0, 2.0, false, 201);
            estimate.Update(144.0, 3.0, false, 201);
            Assert.AreEqual(40.0, estimate.Meters, 0.001);

            estimate.Update(144.0, 4.0, false, 202);
            Assert.AreEqual(40.0, estimate.Meters, 0.001);
            estimate.Update(144.0, 5.0, false, 203);
            Assert.AreEqual(80.0, estimate.Meters, 0.001);
        }

        [TestMethod]
        public void PauseInvalidSpeedAndResetBreakTheSamplingChain()
        {
            var estimate = new AffinitySpeedDistance();
            estimate.Update(36.0, 0.0, false, 1);
            estimate.Update(36.0, 1.0, true, 2);
            estimate.Update(36.0, 2.0, false, 3);
            Assert.AreEqual(0.0, estimate.Meters, 0.001);
            estimate.Update(-36.0, 3.0, false, 4);
            Assert.AreEqual(10.0, estimate.Meters, 0.001);
            estimate.Update(double.NaN, 4.0, false, 5);
            estimate.Update(36.0, 5.0, false, 6);
            Assert.AreEqual(10.0, estimate.Meters, 0.001);
            estimate.Reset();
            Assert.AreEqual(0.0, estimate.Meters, 0.001);
            estimate.Update(36.0, 6.0, false, 7);
            Assert.AreEqual(0.0, estimate.Meters, 0.001);
        }

        [TestMethod]
        public void MissingSampleSequenceDoesNotEstimateDistance()
        {
            var estimate = new AffinitySpeedDistance();
            estimate.Update(100.0, 0.0, false, null);
            estimate.Update(100.0, 1.0, false, null);

            Assert.AreEqual(0.0, estimate.Meters, 0.001);
        }

        [TestMethod]
        public void BackwardSampleSequenceReanchorsBeforeCountingAgain()
        {
            var estimate = new AffinitySpeedDistance();
            estimate.Update(36.0, 0.0, false, 100);
            estimate.Update(36.0, 1.0, false, 101);
            Assert.AreEqual(10.0, estimate.Meters, 0.001);

            estimate.Update(36.0, 2.0, false, 1);
            Assert.AreEqual(10.0, estimate.Meters, 0.001);
            estimate.Update(36.0, 3.0, false, 2);
            Assert.AreEqual(20.0, estimate.Meters, 0.001);
        }
    }
}
