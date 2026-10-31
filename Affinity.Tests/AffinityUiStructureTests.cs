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
            foreach (string path in new[] { "DrivingPatternSnapshot", "HasSufficientData", "EmptyStateText", "Observations", "WeekdayBuckets", "TrackShares", "RelativeHeight", "Comparison.CurrentDistanceDisplay", "Comparison.DistanceChangeDisplay", "Comparison.CurrentSessionCountDisplay", "Comparison.SessionCountChangeDisplay", "Comparison.CurrentMedianDurationDisplay", "Comparison.MedianDurationChangeDisplay" })
                Assert.IsTrue(xaml.Contains(path), path);
        }

        [TestMethod]
        public void Overview_UsesNativeDonutLegendAndThreeSparklines()
        {
            XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            XNamespace local = "clr-namespace:Affinity";
            XDocument document = XDocument.Parse(ReadSource("AffinitySimHub.xaml"));
            XElement distanceSection = document.Descendants(wpf + "TextBlock")
                .Single(element => (string)element.Attribute("Text") == "Where your distance went")
                .Parent;

            Assert.AreEqual("{Binding TrackShares}", (string)distanceSection.Descendants(local + "AffinityDonutChart").Single().Attribute("ItemsSource"));
            Assert.AreEqual(1, distanceSection.Descendants(wpf + "ItemsControl").Count(element => (string)element.Attribute("ItemsSource") == "{Binding TrackShares}"), "The track legend should remain visible.");
            Assert.AreEqual(0, distanceSection.Descendants(wpf + "ProgressBar").Count(), "The distance section should use the donut, not progress bars.");

            string[] expectedBindings =
            {
                "{Binding Comparison.DistanceTrendValues}",
                "{Binding Comparison.SessionCountTrendValues}",
                "{Binding Comparison.MedianDurationTrendValues}"
            };
            CollectionAssert.AreEquivalent(
                expectedBindings,
                document.Descendants(local + "AffinitySparkline").Select(element => (string)element.Attribute("Values")).ToArray());
        }

        [TestMethod]
        public void Overview_DonutLegendMatchesSegmentsAndSupportsNarrowLayouts()
        {
            string xaml = ReadSource("AffinitySimHub.xaml");
            string controls = ReadSource("AffinityPatternChartControls.cs");
            XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
            XElement distancePanel = XDocument.Parse(xaml).Descendants(wpf + "Border")
                .Single(element => (string)element.Attribute(x + "Name") == "DistancePatternPanel");

            Assert.IsTrue(xaml.Contains("AlternationCount=\"5\""), "Legend entries should cycle through the donut palette.");
            Assert.IsTrue(xaml.Contains("ItemsControl.AlternationIndex"));
            Assert.IsFalse(distancePanel.ToString().Contains("Width=\"360\""), "The legend should not force a width wider than a narrow settings panel.");
            Assert.IsTrue(controls.Contains("leader.TrackDisplayName"), "The donut center should use the shorter track name.");
            Assert.IsTrue(controls.Contains("TextTrimming.CharacterEllipsis"), "Long center labels should stay on one line.");
        }

        [TestMethod]
        public void Overview_StretchesEveryObservationCardAcrossOneRow()
        {
            XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
            XNamespace local = "clr-namespace:Affinity";
            XDocument document = XDocument.Parse(ReadSource("AffinitySimHub.xaml"));

            XElement observations = document.Descendants(wpf + "ItemsControl")
                .Single(element => (string)element.Attribute(x + "Name") == "PatternObservationCards");
            Assert.AreEqual("{Binding Observations}", (string)observations.Attribute("ItemsSource"));
            Assert.AreEqual("0,0,-10,0", (string)observations.Attribute("Margin"), "The observation row should cancel the last card's right margin and align with the summary row.");
            XElement observationPanel = observations.Descendants(wpf + "ItemsPanelTemplate").Single();
            XElement observationGrid = observationPanel.Descendants(wpf + "UniformGrid").Single();
            Assert.AreEqual("1", (string)observationGrid.Attribute("Rows"));
            Assert.IsNull(observationGrid.Attribute("Columns"), "The grid should derive equal columns from the number of visible observation cards.");
            Assert.AreEqual(0, observationPanel.Descendants(wpf + "WrapPanel").Count());
            XElement observationCard = observations.Descendants(wpf + "DataTemplate").Single().Element(wpf + "Border");
            Assert.IsNull(observationCard.Attribute("Width"), "The one-row grid should size every visible observation card equally across the available width.");
            Assert.AreEqual("112", (string)observationCard.Attribute("MinHeight"));
            Assert.IsNull(observationCard.Attribute("Height"), "Observation text must be able to grow beyond the minimum card height.");

            XElement chartRow = document.Descendants(wpf + "WrapPanel")
                .Single(element => (string)element.Attribute(x + "Name") == "PatternChartRow");
            XElement weekdayPanel = chartRow.Elements(wpf + "Border")
                .Single(element => (string)element.Attribute(x + "Name") == "WeekdayPatternPanel");
            XElement distancePanel = chartRow.Elements(wpf + "Border")
                .Single(element => (string)element.Attribute(x + "Name") == "DistancePatternPanel");
            Assert.AreEqual("430", (string)weekdayPanel.Attribute("Width"));
            Assert.AreEqual("600", (string)distancePanel.Attribute("Width"));
            Assert.AreEqual(1, weekdayPanel.Descendants(wpf + "ItemsControl").Count(element => (string)element.Attribute("ItemsSource") == "{Binding WeekdayBuckets}"));
            Assert.AreEqual("{Binding TrackShares}", (string)distancePanel.Descendants(local + "AffinityDonutChart").Single().Attribute("ItemsSource"));
            Assert.AreEqual(1, distancePanel.Descendants(wpf + "ItemsControl").Count(element => (string)element.Attribute("ItemsSource") == "{Binding TrackShares}"));
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
