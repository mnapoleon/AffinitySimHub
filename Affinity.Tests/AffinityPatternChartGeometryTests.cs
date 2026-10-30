using System;
using System.Linq;
using Affinity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Affinity.Tests
{
    [TestClass]
    public class AffinityPatternChartGeometryTests
    {
        [TestMethod]
        public void BuildDonutSegments_UsesStableCumulativeAngles()
        {
            AffinityDonutSegment[] segments = AffinityPatternChartGeometry
                .BuildDonutSegments(new[] { 0.31, 0.18, 0.18, 0.33 })
                .ToArray();

            Assert.AreEqual(4, segments.Length);
            Assert.AreEqual(0, segments[0].StartAngleDegrees, 0.0001);
            Assert.AreEqual(111.6, segments[0].SweepAngleDegrees, 0.0001);
            Assert.AreEqual(111.6, segments[1].StartAngleDegrees, 0.0001);
            Assert.AreEqual(176.4, segments[2].StartAngleDegrees, 0.0001);
            Assert.AreEqual(241.2, segments[3].StartAngleDegrees, 0.0001);
            Assert.AreEqual(360, segments.Sum(segment => segment.SweepAngleDegrees), 0.0001);
        }

        [TestMethod]
        public void BuildDonutSegments_ReturnsEmptyForMissingOrZeroShares()
        {
            Assert.AreEqual(0, AffinityPatternChartGeometry.BuildDonutSegments(null).Count);
            Assert.AreEqual(0, AffinityPatternChartGeometry.BuildDonutSegments(new double[0]).Count);
            Assert.AreEqual(0, AffinityPatternChartGeometry.BuildDonutSegments(new[] { 0.0, 0.0 }).Count);
        }

        [TestMethod]
        public void NormalizeSparkline_UsesFullWidthAndPlacesLargerValuesHigher()
        {
            AffinityChartPoint[] points = AffinityPatternChartGeometry
                .NormalizeSparkline(new[] { 2.0, 6.0, 4.0 })
                .ToArray();

            Assert.AreEqual(0, points[0].X, 0.0001);
            Assert.AreEqual(1, points[2].X, 0.0001);
            Assert.IsTrue(points[1].Y < points[0].Y);
            Assert.IsTrue(points[1].Y < points[2].Y);
        }

        [TestMethod]
        public void NormalizeSparkline_CentersConstantAndSingleValues()
        {
            AffinityChartPoint[] constant = AffinityPatternChartGeometry.NormalizeSparkline(new[] { 4.0, 4.0, 4.0 }).ToArray();
            AffinityChartPoint single = AffinityPatternChartGeometry.NormalizeSparkline(new[] { 4.0 }).Single();

            Assert.IsTrue(constant.All(point => Math.Abs(point.Y - 0.5) < 0.0001));
            Assert.AreEqual(0.5, single.Y, 0.0001);
            Assert.AreEqual(0.5, single.X, 0.0001);
        }

        [TestMethod]
        public void NormalizeSparkline_PreservesEightBucketsAndRejectsMissingData()
        {
            Assert.AreEqual(8, AffinityPatternChartGeometry.NormalizeSparkline(Enumerable.Range(0, 8).Select(value => (double)value).ToArray()).Count);
            Assert.AreEqual(0, AffinityPatternChartGeometry.NormalizeSparkline(null).Count);
            Assert.AreEqual(0, AffinityPatternChartGeometry.NormalizeSparkline(new double[0]).Count);
            Assert.AreEqual(0, AffinityPatternChartGeometry.NormalizeSparkline(new[] { double.NaN, double.PositiveInfinity }).Count);
        }
    }
}
