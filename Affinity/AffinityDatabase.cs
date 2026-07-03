using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Affinity
{
    public class AffinityDatabase
    {
        public Dictionary<string, GameBucket> Games { get; set; } = new Dictionary<string, GameBucket>(StringComparer.OrdinalIgnoreCase);
    }

    public class GameBucket
    {
        public Dictionary<string, CarBucket> Cars { get; set; } = new Dictionary<string, CarBucket>(StringComparer.OrdinalIgnoreCase);
    }

    public class CarBucket
    {
        public Dictionary<string, TrackBucket> Tracks { get; set; } = new Dictionary<string, TrackBucket>(StringComparer.OrdinalIgnoreCase);
    }

    public class TrackBucket
    {
        public string GameName { get; set; } = string.Empty;

        public string CarModel { get; set; } = string.Empty;

        public string TrackName { get; set; } = string.Empty;

        public string TrackNameWithConfig { get; set; } = string.Empty;

        public double TotalDistanceMeters { get; set; }

        public double UsedTime { get; set; }

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
    }

    public class DistanceSummary
    {
        public string GameName { get; set; } = string.Empty;

        public string CarModel { get; set; } = string.Empty;

        public string TrackName { get; set; } = string.Empty;

        public string TrackNameWithConfig { get; set; } = string.Empty;

        public string TrackDisplayName { get; set; } = string.Empty;

        public double TotalDistanceKm { get; set; }

        public double TotalDistanceMiles { get; set; }

        public double UsedTime { get; set; }

        public DateTime LastUpdatedUtc { get; set; }
    }

    public class TrackDistanceSummary
    {
        public string GameName { get; set; } = string.Empty;

        public string TrackName { get; set; } = string.Empty;

        public string TrackDisplayName { get; set; } = string.Empty;

        public double DistanceKm { get; set; }

        public double DistanceMiles { get; set; }

        public double DistanceDisplay { get; set; }

        public double UsedTime { get; set; }

        public string UsedTimeDisplay { get; set; } = string.Empty;

        public DateTime LastUpdatedUtc { get; set; }
    }

    public class CarDistanceSummary
    {
        public string GameName { get; set; } = string.Empty;

        public string CarModel { get; set; } = string.Empty;

        public double DistanceKm { get; set; }

        public double DistanceMiles { get; set; }

        public double DistanceDisplay { get; set; }

        public double UsedTime { get; set; }

        public string UsedTimeDisplay { get; set; } = string.Empty;

        public DateTime LastUpdatedUtc { get; set; }
    }

    public class GameTabFilterOption
    {
        public GameTabFilterOption(string key, string displayName)
        {
            Key = key;
            DisplayName = displayName;
        }

        public string Key { get; }

        public string DisplayName { get; }
    }

    public class GameDistanceTab : INotifyPropertyChanged
    {
        public const string TimePeriodAllTime = "AllTime";
        public const string TimePeriodThisMonth = "ThisMonth";
        public const string TimePeriodLastMonth = "LastMonth";
        public const string TimePeriodLast7Days = "Last7Days";
        public const string TimePeriodLast30Days = "Last30Days";
        public const string TimePeriodThisYear = "ThisYear";

        public const string SortByDistance = "Distance";
        public const string SortByTimeDriven = "TimeDriven";
        public const string SortByRecentlyDriven = "RecentlyDriven";

        public const string ResultLimitAll = "All";
        public const string ResultLimitTop5 = "Top5";
        public const string ResultLimitTop10 = "Top10";

        private static readonly IReadOnlyList<GameTabFilterOption> TimePeriodFilterOptionsValue = new List<GameTabFilterOption>
        {
            new GameTabFilterOption(TimePeriodAllTime, "All time"),
            new GameTabFilterOption(TimePeriodThisMonth, "This month"),
            new GameTabFilterOption(TimePeriodLastMonth, "Last month"),
            new GameTabFilterOption(TimePeriodLast7Days, "Last 7 days"),
            new GameTabFilterOption(TimePeriodLast30Days, "Last 30 days"),
            new GameTabFilterOption(TimePeriodThisYear, "This year")
        };

        private static readonly IReadOnlyList<GameTabFilterOption> SortModeOptionsValue = new List<GameTabFilterOption>
        {
            new GameTabFilterOption(SortByDistance, "Distance"),
            new GameTabFilterOption(SortByTimeDriven, "Time driven"),
            new GameTabFilterOption(SortByRecentlyDriven, "Recently driven")
        };

        private static readonly IReadOnlyList<GameTabFilterOption> ResultLimitOptionsValue = new List<GameTabFilterOption>
        {
            new GameTabFilterOption(ResultLimitAll, "All"),
            new GameTabFilterOption(ResultLimitTop5, "Top 5"),
            new GameTabFilterOption(ResultLimitTop10, "Top 10")
        };

        private List<TrackDistanceSummary> _visibleTrackSummaries = new List<TrackDistanceSummary>();
        private List<CarDistanceSummary> _visibleCarSummaries = new List<CarDistanceSummary>();
        private List<DistanceSummary> _timePeriodSummaries = new List<DistanceSummary>();
        private TrackDistanceSummary _topTrackSummary;
        private CarDistanceSummary _topCarSummary;
        private TrackDistanceSummary _selectedTrackSummary;
        private CarDistanceSummary _selectedCarSummary;
        private string _activeFilterDescription = "No filter";
        private string _selectedTimePeriodFilterKey = TimePeriodAllTime;
        private string _selectedSortModeKey = SortByDistance;
        private string _selectedResultLimitKey = ResultLimitAll;
        private bool _isUpdatingFilterState;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Header => GameName;

        public string GameName { get; set; } = string.Empty;

        public double TotalDistanceKm { get; set; }

        public double TotalDistanceMiles { get; set; }

        public double TotalDistanceDisplay { get; set; }

        public double TotalUsedTime { get; set; }

        public string TotalUsedTimeDisplay { get; set; } = string.Empty;

        public bool DisplayInMiles { get; set; }

        public IReadOnlyList<GameTabFilterOption> TimePeriodFilterOptions => TimePeriodFilterOptionsValue;

        public IReadOnlyList<GameTabFilterOption> SortModeOptions => SortModeOptionsValue;

        public IReadOnlyList<GameTabFilterOption> ResultLimitOptions => ResultLimitOptionsValue;

        public string SelectedTimePeriodFilterKey
        {
            get => _selectedTimePeriodFilterKey;
            set
            {
                string normalizedValue = NormalizeFilterKey(value, TimePeriodAllTime);
                if (string.Equals(_selectedTimePeriodFilterKey, normalizedValue, StringComparison.Ordinal))
                {
                    return;
                }

                _selectedTimePeriodFilterKey = normalizedValue;
                OnPropertyChanged();
                UpdateActiveFilterDescription();
                OnPropertyChanged(nameof(HasActiveFilter));
            }
        }

        public string SelectedSortModeKey
        {
            get => _selectedSortModeKey;
            set
            {
                string normalizedValue = NormalizeFilterKey(value, SortByDistance);
                if (string.Equals(_selectedSortModeKey, normalizedValue, StringComparison.Ordinal))
                {
                    return;
                }

                _selectedSortModeKey = normalizedValue;
                OnPropertyChanged();
                ApplyActiveFilters();
            }
        }

        public string SelectedResultLimitKey
        {
            get => _selectedResultLimitKey;
            set
            {
                string normalizedValue = NormalizeFilterKey(value, ResultLimitAll);
                if (string.Equals(_selectedResultLimitKey, normalizedValue, StringComparison.Ordinal))
                {
                    return;
                }

                _selectedResultLimitKey = normalizedValue;
                OnPropertyChanged();
                ApplyActiveFilters();
            }
        }

        public TrackDistanceSummary TopTrackSummary
        {
            get => _topTrackSummary;
            set
            {
                if (ReferenceEquals(_topTrackSummary, value))
                {
                    return;
                }

                _topTrackSummary = value;
                OnPropertyChanged();
            }
        }

        public CarDistanceSummary TopCarSummary
        {
            get => _topCarSummary;
            set
            {
                if (ReferenceEquals(_topCarSummary, value))
                {
                    return;
                }

                _topCarSummary = value;
                OnPropertyChanged();
            }
        }

        public List<TrackDistanceSummary> TrackSummaries { get; set; } = new List<TrackDistanceSummary>();

        public List<CarDistanceSummary> CarSummaries { get; set; } = new List<CarDistanceSummary>();

        public List<DistanceSummary> RawSummaries { get; set; } = new List<DistanceSummary>();

        public List<TrackDistanceSummary> VisibleTrackSummaries
        {
            get => _visibleTrackSummaries;
            private set
            {
                _visibleTrackSummaries = value ?? new List<TrackDistanceSummary>();
                OnPropertyChanged();
            }
        }

        public List<CarDistanceSummary> VisibleCarSummaries
        {
            get => _visibleCarSummaries;
            private set
            {
                _visibleCarSummaries = value ?? new List<CarDistanceSummary>();
                OnPropertyChanged();
            }
        }

        public TrackDistanceSummary SelectedTrackSummary
        {
            get => _selectedTrackSummary;
            set
            {
                if (ReferenceEquals(_selectedTrackSummary, value))
                {
                    return;
                }

                _selectedTrackSummary = value;
                OnPropertyChanged();

                if (_isUpdatingFilterState)
                {
                    return;
                }

                if (value == null)
                {
                    if (!HasActiveFilter)
                    {
                        return;
                    }

                    ClearFilter();
                    return;
                }

                ApplyTrackFilter(value.TrackName);
            }
        }

        public CarDistanceSummary SelectedCarSummary
        {
            get => _selectedCarSummary;
            set
            {
                if (ReferenceEquals(_selectedCarSummary, value))
                {
                    return;
                }

                _selectedCarSummary = value;
                OnPropertyChanged();

                if (_isUpdatingFilterState)
                {
                    return;
                }

                if (value == null)
                {
                    if (!HasActiveFilter)
                    {
                        return;
                    }

                    ClearFilter();
                    return;
                }

                ApplyCarFilter(value.CarModel);
            }
        }

        public string ActiveFilterDescription
        {
            get => _activeFilterDescription;
            private set
            {
                if (string.Equals(_activeFilterDescription, value, StringComparison.Ordinal))
                {
                    return;
                }

                _activeFilterDescription = value;
                OnPropertyChanged();
            }
        }

        public bool HasActiveFilter =>
            SelectedTrackSummary != null ||
            SelectedCarSummary != null ||
            !string.Equals(SelectedTimePeriodFilterKey, TimePeriodAllTime, StringComparison.Ordinal) ||
            !string.Equals(SelectedSortModeKey, SortByDistance, StringComparison.Ordinal) ||
            !string.Equals(SelectedResultLimitKey, ResultLimitAll, StringComparison.Ordinal);

        public void InitializeVisibleSummaries()
        {
            _timePeriodSummaries = RawSummaries.ToList();
            ApplyActiveFilters();
        }

        public void SetTimePeriodSummaries(IEnumerable<DistanceSummary> summaries)
        {
            _timePeriodSummaries = (summaries ?? Enumerable.Empty<DistanceSummary>()).ToList();
            ApplyActiveFilters();
        }

        public void ApplyTrackFilter(string trackNameWithConfig)
        {
            if (string.IsNullOrWhiteSpace(trackNameWithConfig))
            {
                ClearFilter();
                return;
            }

            TrackDistanceSummary selectedTrack = TrackSummaries.FirstOrDefault(summary =>
                string.Equals(summary.TrackName, trackNameWithConfig, StringComparison.OrdinalIgnoreCase));
            if (selectedTrack == null)
            {
                ClearFilter();
                return;
            }

            _isUpdatingFilterState = true;
            try
            {
                _selectedTrackSummary = selectedTrack;
                _selectedCarSummary = null;
                OnPropertyChanged(nameof(SelectedTrackSummary));
                OnPropertyChanged(nameof(SelectedCarSummary));
                ApplyActiveFilters();
            }
            finally
            {
                _isUpdatingFilterState = false;
            }
        }

        public void ApplyCarFilter(string carModel)
        {
            if (string.IsNullOrWhiteSpace(carModel))
            {
                ClearFilter();
                return;
            }

            CarDistanceSummary selectedCar = CarSummaries.FirstOrDefault(summary =>
                string.Equals(summary.CarModel, carModel, StringComparison.OrdinalIgnoreCase));
            if (selectedCar == null)
            {
                ClearFilter();
                return;
            }

            _isUpdatingFilterState = true;
            try
            {
                _selectedCarSummary = selectedCar;
                _selectedTrackSummary = null;
                OnPropertyChanged(nameof(SelectedCarSummary));
                OnPropertyChanged(nameof(SelectedTrackSummary));
                ApplyActiveFilters();
            }
            finally
            {
                _isUpdatingFilterState = false;
            }
        }

        public void ClearFilter()
        {
            _isUpdatingFilterState = true;
            try
            {
                _selectedTrackSummary = null;
                _selectedCarSummary = null;
                _selectedTimePeriodFilterKey = TimePeriodAllTime;
                _selectedSortModeKey = SortByDistance;
                _selectedResultLimitKey = ResultLimitAll;
                _timePeriodSummaries = RawSummaries.ToList();
                OnPropertyChanged(nameof(SelectedTrackSummary));
                OnPropertyChanged(nameof(SelectedCarSummary));
                OnPropertyChanged(nameof(SelectedTimePeriodFilterKey));
                OnPropertyChanged(nameof(SelectedSortModeKey));
                OnPropertyChanged(nameof(SelectedResultLimitKey));
                ApplyActiveFilters();
            }
            finally
            {
                _isUpdatingFilterState = false;
            }
        }

        public override string ToString()
        {
            return GameName;
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void ApplyActiveFilters()
        {
            if (_isUpdatingFilterState)
            {
                RebuildVisibleSummaries();
                return;
            }

            _isUpdatingFilterState = true;
            try
            {
                RebuildVisibleSummaries();
            }
            finally
            {
                _isUpdatingFilterState = false;
            }
        }

        private void RebuildVisibleSummaries()
        {
            List<DistanceSummary> activeRows = GetActiveTimePeriodRows().ToList();
            List<TrackDistanceSummary> trackSummaries = ApplyResultLimit(SortTrackSummaries(
                AffinitySummaryBuilder.BuildTrackSummaries(activeRows, DisplayInMiles))).ToList();
            List<CarDistanceSummary> carSummaries = ApplyResultLimit(SortCarSummaries(
                AffinitySummaryBuilder.BuildCarSummaries(activeRows, DisplayInMiles))).ToList();

            TrackDistanceSummary selectedTrack = _selectedTrackSummary == null
                ? null
                : trackSummaries.FirstOrDefault(summary =>
                    string.Equals(summary.TrackName, _selectedTrackSummary.TrackName, StringComparison.OrdinalIgnoreCase));
            CarDistanceSummary selectedCar = _selectedCarSummary == null
                ? null
                : carSummaries.FirstOrDefault(summary =>
                    string.Equals(summary.CarModel, _selectedCarSummary.CarModel, StringComparison.OrdinalIgnoreCase));

            if (_selectedTrackSummary != null && selectedTrack == null)
            {
                _selectedTrackSummary = null;
                OnPropertyChanged(nameof(SelectedTrackSummary));
            }

            if (_selectedCarSummary != null && selectedCar == null)
            {
                _selectedCarSummary = null;
                OnPropertyChanged(nameof(SelectedCarSummary));
            }

            if (selectedTrack != null)
            {
                List<DistanceSummary> filteredRows = activeRows
                    .Where(summary => string.Equals(summary.TrackNameWithConfig, selectedTrack.TrackName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                VisibleTrackSummaries = trackSummaries;
                VisibleCarSummaries = ApplyResultLimit(SortCarSummaries(
                    AffinitySummaryBuilder.BuildCarSummaries(filteredRows, DisplayInMiles))).ToList();
                TopTrackSummary = selectedTrack;
                TopCarSummary = VisibleCarSummaries.FirstOrDefault();
            }
            else if (selectedCar != null)
            {
                List<DistanceSummary> filteredRows = activeRows
                    .Where(summary => string.Equals(summary.CarModel, selectedCar.CarModel, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                VisibleCarSummaries = carSummaries;
                VisibleTrackSummaries = ApplyResultLimit(SortTrackSummaries(
                    AffinitySummaryBuilder.BuildTrackSummaries(filteredRows, DisplayInMiles))).ToList();
                TopTrackSummary = VisibleTrackSummaries.FirstOrDefault();
                TopCarSummary = selectedCar;
            }
            else
            {
                VisibleTrackSummaries = trackSummaries;
                VisibleCarSummaries = carSummaries;
                TopTrackSummary = VisibleTrackSummaries.FirstOrDefault();
                TopCarSummary = VisibleCarSummaries.FirstOrDefault();
            }

            UpdateActiveFilterDescription();
            OnPropertyChanged(nameof(HasActiveFilter));
        }

        private IEnumerable<DistanceSummary> GetActiveTimePeriodRows()
        {
            return _timePeriodSummaries ?? RawSummaries ?? Enumerable.Empty<DistanceSummary>();
        }

        private IEnumerable<TrackDistanceSummary> SortTrackSummaries(IEnumerable<TrackDistanceSummary> summaries)
        {
            switch (SelectedSortModeKey)
            {
                case SortByTimeDriven:
                    return summaries
                        .OrderByDescending(summary => summary.UsedTime)
                        .ThenByDescending(summary => summary.DistanceDisplay)
                        .ThenBy(summary => summary.TrackDisplayName);
                case SortByRecentlyDriven:
                    return summaries
                        .OrderByDescending(summary => summary.LastUpdatedUtc)
                        .ThenByDescending(summary => summary.DistanceDisplay)
                        .ThenBy(summary => summary.TrackDisplayName);
                default:
                    return summaries
                        .OrderByDescending(summary => summary.DistanceDisplay)
                        .ThenBy(summary => summary.TrackDisplayName);
            }
        }

        private IEnumerable<CarDistanceSummary> SortCarSummaries(IEnumerable<CarDistanceSummary> summaries)
        {
            switch (SelectedSortModeKey)
            {
                case SortByTimeDriven:
                    return summaries
                        .OrderByDescending(summary => summary.UsedTime)
                        .ThenByDescending(summary => summary.DistanceDisplay)
                        .ThenBy(summary => summary.CarModel);
                case SortByRecentlyDriven:
                    return summaries
                        .OrderByDescending(summary => summary.LastUpdatedUtc)
                        .ThenByDescending(summary => summary.DistanceDisplay)
                        .ThenBy(summary => summary.CarModel);
                default:
                    return summaries
                        .OrderByDescending(summary => summary.DistanceDisplay)
                        .ThenBy(summary => summary.CarModel);
            }
        }

        private IEnumerable<T> ApplyResultLimit<T>(IEnumerable<T> summaries)
        {
            switch (SelectedResultLimitKey)
            {
                case ResultLimitTop5:
                    return summaries.Take(5);
                case ResultLimitTop10:
                    return summaries.Take(10);
                default:
                    return summaries;
            }
        }

        private void UpdateActiveFilterDescription()
        {
            var descriptions = new List<string>();

            if (_selectedTrackSummary != null)
            {
                descriptions.Add($"Filtered by track: {_selectedTrackSummary.TrackDisplayName}");
            }
            else if (_selectedCarSummary != null)
            {
                descriptions.Add($"Filtered by car: {_selectedCarSummary.CarModel}");
            }

            AddFilterDescription(descriptions, SelectedTimePeriodFilterKey, TimePeriodAllTime, "Period", TimePeriodFilterOptions);
            AddFilterDescription(descriptions, SelectedSortModeKey, SortByDistance, "Sort", SortModeOptions);
            AddFilterDescription(descriptions, SelectedResultLimitKey, ResultLimitAll, "Limit", ResultLimitOptions);

            ActiveFilterDescription = descriptions.Count == 0 ? "No filter" : string.Join("; ", descriptions);
        }

        private static void AddFilterDescription(
            List<string> descriptions,
            string selectedKey,
            string defaultKey,
            string label,
            IEnumerable<GameTabFilterOption> options)
        {
            if (string.Equals(selectedKey, defaultKey, StringComparison.Ordinal))
            {
                return;
            }

            GameTabFilterOption option = options.FirstOrDefault(item =>
                string.Equals(item.Key, selectedKey, StringComparison.Ordinal));
            descriptions.Add($"{label}: {option?.DisplayName ?? selectedKey}");
        }

        private static string NormalizeFilterKey(string value, string defaultValue)
        {
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
        }
    }

    public class AffinityOverviewTab
    {
        public string Header => "Affinity";
    }

    public class AffinityTopSummarySection
    {
        public string Header { get; set; } = string.Empty;

        public GameDistanceTab FeaturedGameTab { get; set; }

        public TrackDistanceSummary FeaturedTrackSummary { get; set; }

        public CarDistanceSummary FeaturedCarSummary { get; set; }
    }

    public class AffinitySettingsTab
    {
        public string Header => "Settings";
    }
}
