using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Affinity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Affinity.Tests
{
    [TestClass]
    public class AffinityUiStructureTests
    {
        [TestMethod]
        public void Overview_ExposesDrivingPatternsAndSharedRangeInsteadOfOldSections()
        {
            string xaml = ReadSource("AffinitySimHub.xaml");
            foreach (string heading in new[] { "Driving patterns", "Pattern observations", "When you drive", "Where your distance went" })
                Assert.IsTrue(xaml.Contains("Text=\"" + heading + "\""), heading);
            Assert.IsTrue(xaml.Contains("DataContext.PatternRangeOptions"));
            Assert.IsTrue(xaml.Contains("DataContext.SelectedPatternRangeKey"));
            Assert.IsTrue(xaml.Contains("SelectionChanged=\"PatternRangeSelectionChanged\""));
            foreach (string removed in new[] { "Recent highlights", "Game totals", "RecentHighlightsRangeSelectionChanged" })
                Assert.IsFalse(xaml.Contains(removed), removed + " should no longer be displayed.");
            Assert.IsFalse(ReadSource("AffinitySimHub.xaml.cs").Contains("RecentHighlightsRangeSelectionChanged"));
        }

        [TestMethod]
        public void Overview_BindsPatternCardsChartsAndComparisonToSnapshot()
        {
            string xaml = ReadSource("AffinitySimHub.xaml");
            foreach (string path in new[] { "DrivingPatternSnapshot", "HasSufficientData", "EmptyStateText", "Observations", "WeekdayBuckets", "TrackShares", "RelativeHeight", "RelativeWidth", "Comparison.CurrentDistanceDisplay", "Comparison.DistanceChangeDisplay", "Comparison.CurrentSessionCountDisplay", "Comparison.SessionCountChangeDisplay", "Comparison.CurrentMedianDurationDisplay", "Comparison.MedianDurationChangeDisplay" })
                Assert.IsTrue(xaml.Contains(path), path);
        }

        [TestMethod]
        public void GameTab_ShowsAllTimeTotalsBeforeTopTrackAndCar()
        {
            XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            XElement template = XDocument.Parse(ReadSource("AffinitySimHub.xaml")).Descendants(wpf + "DataTemplate")
                .Single(element => (string)element.Attribute("DataType") == "{x:Type local:GameDistanceTab}");
            string[] bindings = template.Descendants(wpf + "TextBlock").Select(element => (string)element.Attribute("Text") ?? string.Empty).ToArray();
            int totalIndex = Array.FindIndex(bindings, binding => binding.StartsWith("{Binding TotalDistanceDisplay,"));
            int trackIndex = Array.FindIndex(bindings, binding => binding.StartsWith("{Binding TopTrackSummary.TrackDisplayName,"));
            Assert.IsTrue(totalIndex >= 0 && totalIndex < trackIndex, "The game total card should precede track/car summaries.");
            Assert.IsTrue(bindings.Any(binding => binding.StartsWith("{Binding TotalUsedTimeDisplay,")));
        }

        [TestMethod]
        public void GameTab_TotalDisplayChangesNotifyBindings()
        {
            var tab = new GameDistanceTab();
            var changed = new List<string>();
            tab.PropertyChanged += (sender, e) => changed.Add(e.PropertyName);

            tab.TotalDistanceDisplay = 12.5;
            tab.TotalUsedTimeDisplay = "2h";

            CollectionAssert.Contains(changed, nameof(GameDistanceTab.TotalDistanceDisplay));
            CollectionAssert.Contains(changed, nameof(GameDistanceTab.TotalUsedTimeDisplay));
        }

        private static string ReadSource(string name)
        {
            string directory = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(directory) && !Directory.Exists(Path.Combine(directory, "Affinity")))
                directory = Path.GetDirectoryName(directory);
            Assert.IsFalse(string.IsNullOrEmpty(directory), "Could not locate Affinity source.");
            return File.ReadAllText(Path.Combine(directory, "Affinity", name));
        }
    }
}
