using System;
using Affinity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Affinity.Tests
{
    [TestClass]
    public class GameTabTimePeriodRangeTests
    {
        [TestMethod]
        public void TryGetGameTabTimePeriodUtcRange_ReturnsFalseForAllTime()
        {
            bool hasRange = AffinityPlugin.TryGetGameTabTimePeriodUtcRange(
                GameDistanceTab.TimePeriodAllTime,
                new DateTime(2026, 7, 2, 12, 0, 0, DateTimeKind.Local),
                out DateTime? startUtc,
                out DateTime? endUtc);

            Assert.IsFalse(hasRange);
            Assert.IsFalse(startUtc.HasValue);
            Assert.IsFalse(endUtc.HasValue);
        }

        [TestMethod]
        public void TryGetGameTabTimePeriodUtcRange_UsesLocalCalendarBoundariesForMonthFilters()
        {
            var referenceLocal = new DateTime(2026, 7, 2, 12, 0, 0, DateTimeKind.Local);

            bool hasRange = AffinityPlugin.TryGetGameTabTimePeriodUtcRange(
                GameDistanceTab.TimePeriodLastMonth,
                referenceLocal,
                out DateTime? startUtc,
                out DateTime? endUtc);

            Assert.IsTrue(hasRange);
            Assert.AreEqual(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Local).ToUniversalTime(), startUtc.Value);
            Assert.AreEqual(new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Local).ToUniversalTime(), endUtc.Value);
        }

        [TestMethod]
        public void TryGetGameTabTimePeriodUtcRange_UsesRollingDayWindows()
        {
            var referenceLocal = new DateTime(2026, 7, 2, 12, 0, 0, DateTimeKind.Local);

            bool hasRange = AffinityPlugin.TryGetGameTabTimePeriodUtcRange(
                GameDistanceTab.TimePeriodLast7Days,
                referenceLocal,
                out DateTime? startUtc,
                out DateTime? endUtc);

            Assert.IsTrue(hasRange);
            Assert.AreEqual(referenceLocal.AddDays(-7).ToUniversalTime(), startUtc.Value);
            Assert.AreEqual(referenceLocal.ToUniversalTime(), endUtc.Value);
        }
    }
}
