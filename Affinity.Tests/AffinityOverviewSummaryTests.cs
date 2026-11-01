using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Affinity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Affinity.Tests
{
    [TestClass]
    public class AffinityOverviewSummaryTests
    {
        [TestMethod]
        public void NewPlugin_InitializesOverviewAndPatternStateSafely()
        {
            var plugin = new AffinityPlugin();
            Assert.AreEqual("Top Overall", plugin.OverallTopSummarySection.Header);
            Assert.AreEqual("Last30Days", GetProperty<string>(plugin, "SelectedPatternRangeKey"));
            var options = GetProperty<IReadOnlyList<GameTabFilterOption>>(plugin, "PatternRangeOptions");
            CollectionAssert.AreEqual(new[] { "Last30Days", "Last90Days", "ThisYear" }, options.Select(option => option.Key).ToArray());
            CollectionAssert.AreEqual(new[] { "Last 30 days", "Last 90 days", "This year" }, options.Select(option => option.DisplayName).ToArray());
            AssertEmptySnapshot(GetProperty<AffinityPatternSnapshot>(plugin, "DrivingPatternSnapshot"));
        }

        [TestMethod]
        public void ApplySummarySnapshot_PopulatesOverviewAndPatternSources()
        {
            var plugin = new AffinityPlugin();
            var summary = new AffinitySummarySnapshot
            {
                FeaturedGameTab = new GameDistanceTab { GameName = "All Game", GameLogoPath = @"C:\SimHub\Logos\All Game.jpg" },
                FeaturedTrackSummary = new TrackDistanceSummary { TrackDisplayName = "All Track" },
                FeaturedCarSummary = new CarDistanceSummary { CarModel = "All Car" },
                TotalDistanceKm = 12.5,
                TotalUsedTime = 600
            };
            var pattern = new AffinityPatternSnapshot { HasSufficientData = true };
            string notifiedProperty = null;
            plugin.PropertyChanged += (sender, args) => { if (args.PropertyName == "DrivingPatternSnapshot") notifiedProperty = args.PropertyName; };

            InvokeApplySummarySnapshot(plugin, summary, pattern);

            Assert.AreEqual("Top Overall", plugin.OverallTopSummarySection.Header);
            Assert.AreSame(summary.FeaturedGameTab, plugin.OverallTopSummarySection.FeaturedGameTab);
            Assert.AreSame(summary.FeaturedGameTab, plugin.FeaturedGameTab);
            Assert.AreSame(summary.FeaturedTrackSummary, plugin.FeaturedTrackSummary);
            Assert.AreSame(summary.FeaturedCarSummary, plugin.FeaturedCarSummary);
            Assert.AreEqual(12.5, plugin.TotalDistanceKm);
            Assert.AreEqual(600.0, plugin.TotalUsedTime);
            Assert.AreSame(pattern, GetProperty<AffinityPatternSnapshot>(plugin, "DrivingPatternSnapshot"));
            Assert.AreEqual("DrivingPatternSnapshot", notifiedProperty);
        }

        [TestMethod]
        public void ApplySummarySnapshot_NullInputsLeaveBindableEmptyState()
        {
            var plugin = new AffinityPlugin();
            InvokeApplySummarySnapshot(plugin, null, null);
            Assert.IsNotNull(plugin.OverallTopSummarySection);
            AssertEmptySnapshot(GetProperty<AffinityPatternSnapshot>(plugin, "DrivingPatternSnapshot"));
            Assert.AreEqual(0, plugin.GameTabs.Count);
        }

        [TestMethod]
        public void SelectedPatternRangeKey_NormalizesUnsupportedValuesWithoutRefreshingSnapshot()
        {
            var plugin = new AffinityPlugin();
            PropertyInfo property = typeof(AffinityPlugin).GetProperty("SelectedPatternRangeKey");
            Assert.IsNotNull(property);
            var initialSnapshot = GetProperty<AffinityPatternSnapshot>(plugin, "DrivingPatternSnapshot");
            property.SetValue(plugin, "Last90Days");
            Assert.AreEqual("Last90Days", property.GetValue(plugin));
            Assert.AreSame(initialSnapshot, GetProperty<AffinityPatternSnapshot>(plugin, "DrivingPatternSnapshot"));
            property.SetValue(plugin, "unsupported");
            Assert.AreEqual("Last30Days", property.GetValue(plugin));
            property.SetValue(plugin, null);
            Assert.AreEqual("Last30Days", property.GetValue(plugin));
        }

        [TestMethod]
        public void BuildPatternSnapshot_PartitionsRollingSessionsAtHalfOpenBoundaries()
        {
            WithRepository((plugin, repository) =>
            {
                AddSession(repository, "previous-start", new DateTime(2026, 5, 3, 12, 0, 0, DateTimeKind.Local));
                AddSession(repository, "previous-middle", new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Local));
                AddSession(repository, "previous-end", new DateTime(2026, 6, 2, 11, 59, 59, DateTimeKind.Local));
                for (int day = 2; day <= 6; day++) AddSession(repository, "current-" + day, new DateTime(2026, 6, day, 12, 0, 0, DateTimeKind.Local));
                AddSession(repository, "before-range", new DateTime(2026, 5, 3, 11, 59, 59, DateTimeKind.Local));
                AddSession(repository, "at-end", new DateTime(2026, 7, 2, 12, 0, 0, DateTimeKind.Local));
                AddSession(repository, "legacy-imported", new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Local));

                AffinityPatternSnapshot snapshot = BuildPatternSnapshot(plugin, new DateTime(2026, 7, 2, 12, 0, 0, DateTimeKind.Local));

                Assert.IsTrue(snapshot.HasSufficientData);
                Assert.AreEqual(5, snapshot.Comparison.CurrentSessionCount);
                Assert.AreEqual(5000.0, snapshot.Comparison.CurrentDistanceMeters);
                Assert.IsTrue(snapshot.Comparison.HasComparison);
                Assert.AreEqual(2, snapshot.Comparison.SessionCountDifference);
            });
        }

        [TestMethod]
        public void BuildPatternSnapshot_ExcludesGapBetweenYearToDateComparisonWindows()
        {
            WithRepository((plugin, repository) =>
            {
                typeof(AffinityPlugin).GetProperty("SelectedPatternRangeKey").SetValue(plugin, "ThisYear");
                for (int day = 1; day <= 3; day++) AddSession(repository, "previous-" + day, new DateTime(2025, 1, day, 12, 0, 0, DateTimeKind.Local));
                for (int day = 1; day <= 5; day++) AddSession(repository, "current-" + day, new DateTime(2026, 1, day, 12, 0, 0, DateTimeKind.Local));
                AddSession(repository, "at-previous-end", new DateTime(2025, 7, 2, 12, 0, 0, DateTimeKind.Local));
                AddSession(repository, "in-gap", new DateTime(2025, 12, 1, 12, 0, 0, DateTimeKind.Local));

                AffinityPatternSnapshot snapshot = BuildPatternSnapshot(plugin, new DateTime(2026, 7, 2, 12, 0, 0, DateTimeKind.Local));

                Assert.AreEqual(5, snapshot.Comparison.CurrentSessionCount);
                Assert.IsTrue(snapshot.Comparison.HasComparison);
                Assert.AreEqual(2, snapshot.Comparison.SessionCountDifference);
            });
        }

        [TestMethod]
        public void BuildPatternSnapshot_AppliesGameProfileTrackDisplayNames()
        {
            WithRepository((plugin, repository) =>
            {
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ks_brands_hatch-indy"] = "Brands Hatch - Indy"
                };
                typeof(AffinityPlugin).GetField("_assettoCorsaTrackMap", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(plugin, map);
                typeof(AffinityPlugin).GetField("_trackDisplayContext", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(plugin, new AffinityTrackDisplayContext(map));
                for (int day = 2; day <= 6; day++)
                {
                    DateTime startedLocal = new DateTime(2026, 6, day, 12, 0, 0, DateTimeKind.Local);
                    repository.UpsertSession("mapped-" + day, "Assetto Corsa", "MX-5",
                        "ks_brands_hatch", "ks_brands_hatch-indy", startedLocal.ToUniversalTime(),
                        startedLocal.AddMinutes(10).ToUniversalTime(), 1000, 600);
                }

                AffinityPatternSnapshot snapshot = BuildPatternSnapshot(
                    plugin, new DateTime(2026, 7, 2, 12, 0, 0, DateTimeKind.Local));

                Assert.AreEqual("Brands Hatch - Indy", snapshot.TrackShares.Single().TrackDisplayName);
            });
        }

        private static void WithRepository(Action<AffinityPlugin, AffinitySqliteRepository> test)
        {
            string directory = Path.Combine(Path.GetTempPath(), "AffinityTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                using (var repository = new AffinitySqliteRepository(Path.Combine(directory, "patterns.db")))
                {
                    repository.Initialize();
                    var plugin = new AffinityPlugin();
                    typeof(AffinityPlugin).GetField("_sqliteRepository", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(plugin, repository);
                    test(plugin, repository);
                }
            }
            finally { Directory.Delete(directory, recursive: true); }
        }

        private static void AddSession(AffinitySqliteRepository repository, string uid, DateTime startedLocal)
        {
            repository.UpsertSession(uid, "iRacing", "MX-5", "spa", "Spa", startedLocal.ToUniversalTime(),
                startedLocal.AddMinutes(10).ToUniversalTime(), 1000, 600);
        }

        private static AffinityPatternSnapshot BuildPatternSnapshot(AffinityPlugin plugin, DateTime reference)
        {
            MethodInfo method = typeof(AffinityPlugin).GetMethod("BuildPatternSnapshot", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            return (AffinityPatternSnapshot)method.Invoke(plugin, new object[] { reference });
        }

        [TestMethod]
        public void TopSummarySection_HelperPropertiesExposeNullSafeState()
        {
            var section = new AffinityTopSummarySection();
            Assert.AreEqual("No driving history yet", section.EmptyStateText);
            Assert.IsFalse(section.HasFeaturedGame);
            Assert.IsFalse(section.HasFeaturedTrack);
            Assert.IsFalse(section.HasFeaturedCar);
            section.FeaturedGameTab = new GameDistanceTab { GameName = "Assetto Corsa" };
            section.FeaturedTrackSummary = new TrackDistanceSummary { TrackDisplayName = "Spa" };
            section.FeaturedCarSummary = new CarDistanceSummary { CarModel = "Mazda MX-5" };
            Assert.IsTrue(section.HasFeaturedGame);
            Assert.IsTrue(section.HasFeaturedTrack);
            Assert.IsTrue(section.HasFeaturedCar);
        }

        private static void AssertEmptySnapshot(AffinityPatternSnapshot snapshot)
        {
            Assert.IsNotNull(snapshot);
            Assert.IsFalse(snapshot.HasSufficientData);
            Assert.IsFalse(string.IsNullOrWhiteSpace(snapshot.EmptyStateText));
            Assert.IsNotNull(snapshot.Observations);
            Assert.IsNotNull(snapshot.WeekdayBuckets);
            Assert.IsNotNull(snapshot.TrackShares);
            Assert.IsNotNull(snapshot.Comparison);
        }

        internal static T GetProperty<T>(AffinityPlugin plugin, string name)
        {
            PropertyInfo property = typeof(AffinityPlugin).GetProperty(name);
            Assert.IsNotNull(property, name + " must be bindable.");
            return (T)property.GetValue(plugin);
        }

        internal static void InvokeApplySummarySnapshot(AffinityPlugin plugin, AffinitySummarySnapshot summary, AffinityPatternSnapshot pattern)
        {
            MethodInfo method = typeof(AffinityPlugin).GetMethod("ApplySummarySnapshot", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(AffinitySummarySnapshot), typeof(AffinityPatternSnapshot) }, null);
            Assert.IsNotNull(method, "Summary refresh must accept the all-time summary and pattern snapshot.");
            method.Invoke(plugin, new object[] { summary, pattern });
        }
    }
}
