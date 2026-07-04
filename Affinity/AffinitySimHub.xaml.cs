using System.Windows;
using System.Windows.Controls;

namespace Affinity
{
    public partial class AffinitySimHub : UserControl
    {
        private readonly AffinityPlugin _plugin;

        public AffinitySimHub(AffinityPlugin plugin)
        {
            _plugin = plugin;
            InitializeComponent();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _plugin.SaveSettings();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            _plugin.RefreshDistanceSummaries();
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            _plugin.ResetSettings();
        }

        private void DistanceUnitChanged(object sender, RoutedEventArgs e)
        {
            _plugin.RefreshDisplaySettings();
        }

        private void ClearFilterButton_Click(object sender, RoutedEventArgs e)
        {
            _plugin.ClearSelectedGameTabFilter();
        }

        private void GameTabFilterSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _plugin.ApplySelectedGameTabTimeFilter();
        }

        private void RecentHighlightsRangeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _plugin.ApplySelectedRecentHighlightsRange();
        }
    }
}
