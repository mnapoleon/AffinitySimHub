using System.Collections.ObjectModel;
using Affinity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Affinity.Tests
{
    [TestClass]
    public class AffinityPluginTabRefreshTests
    {
        [TestMethod]
        public void CanReuseTopLevelTabStructure_ReturnsFalseWhenGameTabsMatchByName()
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

            Assert.IsFalse(canReuse);
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
