using System;
using System.Reflection;
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

        [DataTestMethod]
        [DataRow("Last30Days", 2026, 5, 3, 2026, 6, 2)]
        [DataRow("Last90Days", 2026, 1, 3, 2026, 4, 3)]
        public void TryGetPatternRangeUtcRanges_UsesAdjacentRollingLocalDayWindows(
            string key, int previousYear, int previousMonth, int previousDay, int currentYear, int currentMonth, int currentDay)
        {
            var reference = new DateTime(2026, 7, 2, 12, 34, 56, DateTimeKind.Local);
            var currentStart = new DateTime(currentYear, currentMonth, currentDay, 12, 34, 56, DateTimeKind.Local);
            var previousStart = new DateTime(previousYear, previousMonth, previousDay, 12, 34, 56, DateTimeKind.Local);
            AssertPatternRanges(key, reference, currentStart, reference, previousStart, currentStart);
        }

        [DataTestMethod]
        [DataRow(2026, 7, 4, 2025, 7, 4)]
        [DataRow(2024, 2, 29, 2023, 2, 28)]
        [DataRow(2026, 1, 1, 2025, 1, 1)]
        public void TryGetPatternRangeUtcRanges_UsesYearToDateAndEquivalentPreviousLocalDate(
            int year, int month, int day, int previousYear, int previousMonth, int previousDay)
        {
            var reference = new DateTime(year, month, day, 12, 34, 56, DateTimeKind.Local);
            AssertPatternRanges("ThisYear", reference,
                new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Local), reference,
                new DateTime(previousYear, 1, 1, 0, 0, 0, DateTimeKind.Local),
                new DateTime(previousYear, previousMonth, previousDay, 12, 34, 56, DateTimeKind.Local));
        }

        [TestMethod]
        public void TryGetPatternRangeUtcRanges_ConvertsUtcReferenceBeforeComputingLocalWindow()
        {
            var reference = new DateTime(2026, 7, 2, 12, 0, 0, DateTimeKind.Local);
            AssertPatternRanges("Last30Days", reference.ToUniversalTime(),
                new DateTime(2026, 6, 2, 12, 0, 0, DateTimeKind.Local), reference,
                new DateTime(2026, 5, 3, 12, 0, 0, DateTimeKind.Local),
                new DateTime(2026, 6, 2, 12, 0, 0, DateTimeKind.Local));
        }

        private static void AssertPatternRanges(string key, DateTime reference, DateTime currentStart, DateTime currentEnd,
            DateTime previousStart, DateTime previousEnd)
        {
            MethodInfo method = typeof(AffinityPlugin).GetMethod("TryGetPatternRangeUtcRanges", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            object[] args = { key, reference, null, null, null, null };
            Assert.IsTrue((bool)method.Invoke(null, args));
            Assert.AreEqual(currentStart.ToUniversalTime(), args[2]);
            Assert.AreEqual(currentEnd.ToUniversalTime(), args[3]);
            Assert.AreEqual(previousStart.ToUniversalTime(), args[4]);
            Assert.AreEqual(previousEnd.ToUniversalTime(), args[5]);
            for (int i = 2; i < args.Length; i++) Assert.AreEqual(DateTimeKind.Utc, ((DateTime)args[i]).Kind);
        }
    }
}
