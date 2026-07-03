using System.Collections.ObjectModel;
using Affinity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Affinity.Tests
{
    [TestClass]
    public class AffinityPluginTabRefreshTests
    {
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
                new GameDistanceTab { GameName = "Assetto Corsa", TotalDistanceDisplay = 12.3 },
                new GameDistanceTab { GameName = "iRacing", TotalDistanceDisplay = 45.6 }
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
                new GameDistanceTab { GameName = "Assetto Corsa", TotalDistanceDisplay = 12.3 },
                new GameDistanceTab { GameName = "iRacing", TotalDistanceDisplay = 45.6 }
            };

            AffinityPlugin.ReplaceGameTabsInCollections(gameTabs, topLevelTabs, refreshedTabs);

            Assert.AreSame(overviewTab, topLevelTabs[0]);
            Assert.AreSame(settingsTab, topLevelTabs[3]);
            Assert.AreSame(refreshedTabs[0], gameTabs[0]);
            Assert.AreSame(refreshedTabs[0], topLevelTabs[1]);
            Assert.AreSame(refreshedTabs[1], gameTabs[1]);
            Assert.AreSame(refreshedTabs[1], topLevelTabs[2]);
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
