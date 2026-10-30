using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Reflection;
using Affinity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Affinity.Tests
{
    [TestClass]
    public class AffinityPluginTabRefreshTests
    {
        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ApplySummarySnapshot_PreservesSelectedGameAndFiltersWhenTabsRefresh(bool structureChanges)
        {
            var plugin = new AffinityPlugin();
            var previous = new GameDistanceTab
            {
                GameName = "iRacing",
                SelectedTimePeriodFilterKey = GameDistanceTab.TimePeriodLast30Days,
                SelectedSortModeKey = GameDistanceTab.SortByTimeDriven,
                SelectedResultLimitKey = GameDistanceTab.ResultLimitTop5
            };
            plugin.GameTabs.Add(previous);
            plugin.TopLevelTabs.Insert(1, previous);
            plugin.SelectedTopLevelTab = previous;
            var refreshed = new GameDistanceTab { GameName = "iRacing" };
            var summary = new AffinitySummarySnapshot { GameTabs = new List<GameDistanceTab> { refreshed } };
            if (structureChanges) summary.GameTabs.Insert(0, new GameDistanceTab { GameName = "Assetto Corsa" });
            var pattern = new AffinityPatternSnapshot { HasSufficientData = true };

            AffinityOverviewSummaryTests.InvokeApplySummarySnapshot(plugin, summary, pattern);

            Assert.AreSame(refreshed, plugin.SelectedTopLevelTab);
            Assert.AreSame(refreshed, plugin.SelectedGameTab);
            Assert.AreEqual(GameDistanceTab.TimePeriodLast30Days, refreshed.SelectedTimePeriodFilterKey);
            Assert.AreEqual(GameDistanceTab.SortByTimeDriven, refreshed.SelectedSortModeKey);
            Assert.AreEqual(GameDistanceTab.ResultLimitTop5, refreshed.SelectedResultLimitKey);
            Assert.AreSame(pattern, AffinityOverviewSummaryTests.GetProperty<AffinityPatternSnapshot>(plugin, "DrivingPatternSnapshot"));
        }

        [TestMethod]
        public void ApplySelectedPatternRange_LeavesGameTabInstancesAndFiltersIntact()
        {
            var plugin = new AffinityPlugin();
            var selected = new GameDistanceTab
            {
                GameName = "iRacing",
                SelectedTimePeriodFilterKey = GameDistanceTab.TimePeriodLast30Days,
                SelectedSortModeKey = GameDistanceTab.SortByTimeDriven,
                SelectedResultLimitKey = GameDistanceTab.ResultLimitTop5
            };
            plugin.GameTabs.Add(selected);
            plugin.TopLevelTabs.Insert(1, selected);
            plugin.SelectedTopLevelTab = selected;
            MethodInfo method = typeof(AffinityPlugin).GetMethod("ApplySelectedPatternRange", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);

            method.Invoke(plugin, null);

            Assert.AreSame(selected, plugin.GameTabs[0]);
            Assert.AreSame(selected, plugin.SelectedTopLevelTab);
            Assert.AreEqual(GameDistanceTab.TimePeriodLast30Days, selected.SelectedTimePeriodFilterKey);
            Assert.AreEqual(GameDistanceTab.SortByTimeDriven, selected.SelectedSortModeKey);
            Assert.AreEqual(GameDistanceTab.ResultLimitTop5, selected.SelectedResultLimitKey);
        }

        [TestMethod]
        public void CanReuseTopLevelTabStructure_ReturnsTrueWhenGameTabsMatchByName()
        {
            GameDistanceTab[] existingTabs =
            {
                new GameDistanceTab { GameName = "Assetto Corsa" },
                new GameDistanceTab { GameName = "iRacing" }
            };
            GameDistanceTab[] refreshedTabs =
            {
                new GameDistanceTab { GameName = "Assetto Corsa", TotalDistanceDisplay = 12.3, GameLogoPath = @"C:\SimHub\Logos\244210.jpg" },
                new GameDistanceTab { GameName = "iRacing", TotalDistanceDisplay = 45.6, GameLogoPath = @"C:\SimHub\Logos\iRacing.jpg" }
            };

            bool canReuse = AffinityPlugin.CanReuseTopLevelTabStructure(existingTabs, refreshedTabs);

            Assert.IsTrue(canReuse);
        }

        [TestMethod]
        public void ReplaceGameTabsInCollections_PreservesOverviewAndSettingsTabs()
        {
            AffinityOverviewTab overviewTab = new AffinityOverviewTab();
            AffinitySettingsTab settingsTab = new AffinitySettingsTab();
            ObservableCollection<GameDistanceTab> gameTabs = new ObservableCollection<GameDistanceTab>
            {
                new GameDistanceTab { GameName = "Assetto Corsa", TotalDistanceDisplay = 10.0 },
                new GameDistanceTab { GameName = "iRacing", TotalDistanceDisplay = 20.0 }
            };
            ObservableCollection<object> topLevelTabs = new ObservableCollection<object>
            {
                overviewTab,
                gameTabs[0],
                gameTabs[1],
                settingsTab
            };
            GameDistanceTab[] refreshedTabs =
            {
                new GameDistanceTab { GameName = "Assetto Corsa", TotalDistanceDisplay = 12.3, GameLogoPath = @"C:\SimHub\Logos\244210.jpg" },
                new GameDistanceTab { GameName = "iRacing", TotalDistanceDisplay = 45.6, GameLogoPath = @"C:\SimHub\Logos\iRacing.jpg" }
            };

            AffinityPlugin.ReplaceGameTabsInCollections(gameTabs, topLevelTabs, refreshedTabs);

            Assert.AreSame(overviewTab, topLevelTabs[0]);
            Assert.AreSame(settingsTab, topLevelTabs[3]);
            Assert.AreSame(refreshedTabs[0], gameTabs[0]);
            Assert.AreSame(refreshedTabs[0], topLevelTabs[1]);
            Assert.AreEqual(@"C:\SimHub\Logos\244210.jpg", gameTabs[0].GameLogoPath);
            Assert.AreSame(refreshedTabs[1], gameTabs[1]);
            Assert.AreSame(refreshedTabs[1], topLevelTabs[2]);
            Assert.AreEqual(@"C:\SimHub\Logos\iRacing.jpg", gameTabs[1].GameLogoPath);
        }

        [TestMethod]
        public void ResolveSelectedTopLevelTab_PreservesSelectedGameTabByGameName()
        {
            AffinityOverviewTab overviewTab = new AffinityOverviewTab();
            AffinitySettingsTab settingsTab = new AffinitySettingsTab();
            GameDistanceTab previouslySelectedTab = new GameDistanceTab { GameName = "iRacing" };
            ObservableCollection<GameDistanceTab> refreshedTabs = new ObservableCollection<GameDistanceTab>
            {
                new GameDistanceTab { GameName = "Assetto Corsa" },
                new GameDistanceTab { GameName = "iRacing" }
            };

            object selectedTab = AffinityPlugin.ResolveSelectedTopLevelTab(previouslySelectedTab, refreshedTabs, overviewTab, settingsTab);

            Assert.AreSame(refreshedTabs[1], selectedTab);
        }

        [TestMethod]
        public void ResolveSelectedTopLevelTab_PreservesOverviewTabSelection()
        {
            AffinityOverviewTab overviewTab = new AffinityOverviewTab();
            AffinitySettingsTab settingsTab = new AffinitySettingsTab();
            ObservableCollection<GameDistanceTab> refreshedTabs = new ObservableCollection<GameDistanceTab>
            {
                new GameDistanceTab { GameName = "Assetto Corsa" }
            };

            object selectedTab = AffinityPlugin.ResolveSelectedTopLevelTab(overviewTab, refreshedTabs, overviewTab, settingsTab);

            Assert.AreSame(overviewTab, selectedTab);
        }
    }
}
