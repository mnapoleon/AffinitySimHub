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
    }

    public class GameDistanceTab : INotifyPropertyChanged
    {
        private List<TrackDistanceSummary> _visibleTrackSummaries = new List<TrackDistanceSummary>();
        private List<CarDistanceSummary> _visibleCarSummaries = new List<CarDistanceSummary>();
        private TrackDistanceSummary _topTrackSummary;
        private CarDistanceSummary _topCarSummary;
        private TrackDistanceSummary _selectedTrackSummary;
        private CarDistanceSummary _selectedCarSummary;
        private string _activeFilterDescription = "No filter";
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

        public bool HasActiveFilter => SelectedTrackSummary != null || SelectedCarSummary != null;

        public void InitializeVisibleSummaries()
        {
            VisibleTrackSummaries = TrackSummaries.ToList();
            VisibleCarSummaries = CarSummaries.ToList();
            TopTrackSummary = TrackSummaries.FirstOrDefault();
            TopCarSummary = CarSummaries.FirstOrDefault();
            ActiveFilterDescription = "No filter";
            OnPropertyChanged(nameof(HasActiveFilter));
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

            List<DistanceSummary> filteredRows = RawSummaries
                .Where(summary => string.Equals(summary.TrackNameWithConfig, trackNameWithConfig, StringComparison.OrdinalIgnoreCase))
                .ToList();

            _isUpdatingFilterState = true;
            try
            {
                _selectedTrackSummary = selectedTrack;
                _selectedCarSummary = null;
                OnPropertyChanged(nameof(SelectedTrackSummary));
                OnPropertyChanged(nameof(SelectedCarSummary));
                VisibleTrackSummaries = TrackSummaries.ToList();
                VisibleCarSummaries = AffinitySummaryBuilder.BuildCarSummaries(filteredRows, DisplayInMiles);
                TopTrackSummary = selectedTrack;
                TopCarSummary = VisibleCarSummaries.FirstOrDefault();
                ActiveFilterDescription = $"Filtered by track: {selectedTrack.TrackDisplayName}";
                OnPropertyChanged(nameof(HasActiveFilter));
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

            List<DistanceSummary> filteredRows = RawSummaries
                .Where(summary => string.Equals(summary.CarModel, carModel, StringComparison.OrdinalIgnoreCase))
                .ToList();

            _isUpdatingFilterState = true;
            try
            {
                _selectedCarSummary = selectedCar;
                _selectedTrackSummary = null;
                OnPropertyChanged(nameof(SelectedCarSummary));
                OnPropertyChanged(nameof(SelectedTrackSummary));
                VisibleCarSummaries = CarSummaries.ToList();
                VisibleTrackSummaries = AffinitySummaryBuilder.BuildTrackSummaries(filteredRows, DisplayInMiles);
                TopTrackSummary = VisibleTrackSummaries.FirstOrDefault();
                TopCarSummary = selectedCar;
                ActiveFilterDescription = $"Filtered by car: {selectedCar.CarModel}";
                OnPropertyChanged(nameof(HasActiveFilter));
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
                OnPropertyChanged(nameof(SelectedTrackSummary));
                OnPropertyChanged(nameof(SelectedCarSummary));
                InitializeVisibleSummaries();
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
