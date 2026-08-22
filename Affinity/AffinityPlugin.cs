using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GameReaderCommon;
using Newtonsoft.Json;
using SimHub.Plugins;

namespace Affinity
{
    [PluginName("Affinity")]
    [PluginDescription("Tracks cumulative distance and time by game, car, and track across sessions.")]
    [PluginAuthor("Affinity")]
    public class AffinityPlugin : IPlugin, IDataPlugin, IWPFSettings, IWPFSettingsV2, INotifyPropertyChanged
    {
        private enum SessionDistanceSource
        {
            Unknown = 0,
            Derived = 1,
            SessionOdoMeters = 2,
            SessionOdoKilometers = 3
        }

        private const string SettingsFileName = "Affinity.settings.json";
        private const string SqliteDataFileName = "Affinity.distance.db";
        private const string LegacyDataFileName = "Affinity.distance.json";
        private const string DebugLogFileName = "Affinity.distance.debug.log";
        private const string SqliteInteropFileName = "SQLite.Interop.dll";
        private const string SqliteNativeRecoveryFolderName = "sqlite-native";
        private const string SqliteUnavailableStatusMessage = "Affinity install incomplete; reinstall plugin to load data";
        private const double MetersPerKilometer = 1000.0;
        private const double MetersPerMile = 1609.344;
        private const double SaveThresholdMeters = 50.0;
        private const double SaveThresholdUsedTimeSeconds = 30.0;
        private const double MinimumPersistedSessionMeters = 1.0;
        private const double MaxCountedTelemetryGapSeconds = 5.0;
        private const int Automobilista2ObservedReplayGameState = 6;
        public const string RecentHighlightsRangeWeek = "Week";
        public const string RecentHighlightsRangeMonth = "Month";
        private static readonly string Version = ResolvePluginVersion();
        private static readonly AffinityGameProfileRegistry DefaultGameProfiles =
            AffinityGameProfileRegistry.CreateDefault();
        private static readonly IReadOnlyList<GameTabFilterOption> RecentHighlightsRangeOptionsValue = new List<GameTabFilterOption>
        {
            new GameTabFilterOption(RecentHighlightsRangeWeek, "Week"),
            new GameTabFilterOption(RecentHighlightsRangeMonth, "Month")
        };
        private static readonly string[] GameDataDebugMemberNames =
        {
            "IsGameReplay",
            "GameReplay",
            "ReplayMode",
            "SessionId",
            "SessionType",
            "SessionState",
            "SessionNum",
            "SessionTime",
            "SessionTimeRemain",
            "SessionTimeLeft",
            "ReplayPlaySpeed",
            "ReplayFrameNum",
            "ReplayFrameNumEnd",
            "ReplaySessionNum",
            "ReplaySessionTime",
            "IsSpectator",
            "CameraState"
        };
        private static readonly string[] StatusDebugMemberNames =
        {
            "IsGameReplay",
            "ReplayMode",
            "SessionType",
            "SessionState",
            "SessionNum",
            "SessionTick",
            "SessionTickCount",
            "SessionTime",
            "SessionTimeRemain",
            "SessionTimeLeft",
            "RaceTime",
            "CurrentLapTime",
            "LapTimeCurrent",
            "ReplayPlaySpeed",
            "ReplayFrameNum",
            "ReplayFrameNumEnd",
            "ReplaySessionNum",
            "ReplaySessionTime",
            "IsSpectator",
            "IsOnTrack",
            "IsInPit",
            "IsInGarage",
            "PitLimiterOn",
            "PlayerTrackSurface",
            "PlayerCarPosition"
        };
        private static readonly string[] RawDataDebugMemberNames =
        {
            "FinishStatus",
            "GamePlayerInGarage",
            "mViewedParticipantIndex",
            "mGameState",
            "mSessionState",
            "mRaceState",
            "mPitMode"
        };

        private bool _hasLoggedDataError;
        private readonly AffinityGameProfileRegistry _gameProfiles = DefaultGameProfiles;
        private ImageSource _pictureIcon;
        private string _settingsPath = string.Empty;
        private string _databasePath = string.Empty;
        private string _legacyDatabasePath = string.Empty;
        private string _debugLogPath = string.Empty;
        private string _acTrackMapPath = string.Empty;
        private AffinityDatabase _database = new AffinityDatabase();
        private AffinitySqliteRepository _sqliteRepository;
        private Dictionary<string, string> _assettoCorsaTrackMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private AffinityTrackDisplayContext _trackDisplayContext = new AffinityTrackDisplayContext(null);
        private string _currentGameName = "No active game";
        private string _currentCarModel = "Unknown car";
        private string _currentTrackName = "Unknown track";
        private string _currentTrackNameWithConfig = "Unknown track variation";
        private string _dataStatus = "Waiting for telemetry";
        private string _settingsStatus = "Settings not saved in this session";
        private double _currentContextDistanceKm;
        private double _sessionDistanceKm;
        private double _totalDistanceKm;
        private double _currentContextUsedTime;
        private double _totalUsedTime;
        private GameDistanceTab _featuredGameTab;
        private TrackDistanceSummary _featuredTrackSummary;
        private CarDistanceSummary _featuredCarSummary;
        private bool _isTelemetryActive;
        private object _selectedTopLevelTab;
        private GameDistanceTab _selectedGameTab;
        private readonly Dictionary<string, ImageSource> _gameLogoCache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        private string _selectedRecentHighlightsRangeKey = RecentHighlightsRangeMonth;
        private string _currentRecentHighlightsDateRangeDisplay = string.Empty;
        private string _previousRecentHighlightsDateRangeDisplay = string.Empty;
        private AffinityTopSummarySection _currentRecentHighlightsSection =
            new AffinityTopSummarySection { Header = "This month highlights" };
        private AffinityTopSummarySection _previousRecentHighlightsSection =
            new AffinityTopSummarySection { Header = "Last month highlights" };
        private Guid _activeSessionId = Guid.Empty;
        private string _activeContextKey = string.Empty;
        private SessionDistanceSource _sessionDistanceSource = SessionDistanceSource.Unknown;
        private double _sessionStartTrackPositionMeters = -1.0;
        private double _sessionStatefulAbsoluteMeters;
        private double _lastTrackPositionWithinLapMeters = -1.0;
        private double _sessionDistanceOriginMeters;
        private double _lastObservedSessionMeters = -1.0;
        private double _lastIgnoredSessionMeters = -1.0;
        private int _lastObservedCompletedLaps = -1;
        private string _activeStorageSessionUid = string.Empty;
        private DateTime _activeSessionStartedUtc = DateTime.MinValue;
        private double _activeSessionUsedTimeSeconds;
        private double _pendingMetersSinceSave;
        private double _pendingUsedTimeSecondsSinceSave;
        private DateTime _lastTelemetryDebugLogUtc = DateTime.MinValue;
        private DateTime _lastSessionSampleUtc = DateTime.MinValue;
        private int _automobilista2PlayerViewedParticipantIndex = -1;
        private readonly AffinityOverviewTab _overviewTab = new AffinityOverviewTab();
        private readonly AffinitySettingsTab _settingsTab = new AffinitySettingsTab();

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string fileName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string moduleName);

        public event PropertyChangedEventHandler PropertyChanged;

        public AffinityPlugin()
        {
            RebuildTopLevelTabs();
        }

        public PluginManager PluginManager { get; set; }

        public AffinitySettings Settings { get; private set; } = new AffinitySettings();

        public ImageSource PictureIcon => _pictureIcon ?? (_pictureIcon = CreatePictureIcon());

        public string LeftMenuTitle => "Affinity";

        public string DatabasePath => _databasePath;

        public bool IsDebugLoggingEnabled
        {
            get => Settings.EnableDebugLogging;
            set
            {
                if (Settings.EnableDebugLogging == value)
                {
                    return;
                }

                Settings.EnableDebugLogging = value;
                OnPropertyChanged();
            }
        }

        public string SettingsStatus
        {
            get => _settingsStatus;
            private set
            {
                if (_settingsStatus == value)
                {
                    return;
                }

                _settingsStatus = value;
                OnPropertyChanged();
            }
        }

        public string PluginVersionDisplay
        {
            get
            {
                string versionCore = Version.Split('+')[0].Split('-')[0];
                return System.Version.TryParse(versionCore, out System.Version parsedVersion)
                    ? $"{parsedVersion.Major}.{parsedVersion.Minor}.{parsedVersion.Build}"
                    : versionCore;
            }
        }

        public ObservableCollection<GameDistanceTab> GameTabs { get; } = new ObservableCollection<GameDistanceTab>();

        public AffinityTopSummarySection OverallTopSummarySection { get; private set; } =
            new AffinityTopSummarySection { Header = "Top Overall" };

        public ObservableCollection<AffinityTopSummarySection> MonthlyTopSummarySections { get; } = new ObservableCollection<AffinityTopSummarySection>();

        public IReadOnlyList<GameTabFilterOption> RecentHighlightsRangeOptions => RecentHighlightsRangeOptionsValue;

        public ObservableCollection<AffinityTopSummarySection> TopSummarySections { get; } = new ObservableCollection<AffinityTopSummarySection>();

        public ObservableCollection<object> TopLevelTabs { get; } = new ObservableCollection<object>();

        public ObservableCollection<GameDebugLoggingOption> GameDebugLoggingOptions { get; } = new ObservableCollection<GameDebugLoggingOption>();

        public GameDistanceTab FeaturedGameTab
        {
            get => _featuredGameTab;
            private set
            {
                if (ReferenceEquals(_featuredGameTab, value))
                {
                    return;
                }

                _featuredGameTab = value;
                OnPropertyChanged();
            }
        }

        public TrackDistanceSummary FeaturedTrackSummary
        {
            get => _featuredTrackSummary;
            private set
            {
                if (ReferenceEquals(_featuredTrackSummary, value))
                {
                    return;
                }

                _featuredTrackSummary = value;
                OnPropertyChanged();
            }
        }

        public CarDistanceSummary FeaturedCarSummary
        {
            get => _featuredCarSummary;
            private set
            {
                if (ReferenceEquals(_featuredCarSummary, value))
                {
                    return;
                }

                _featuredCarSummary = value;
                OnPropertyChanged();
            }
        }

        public string SelectedRecentHighlightsRangeKey
        {
            get => _selectedRecentHighlightsRangeKey;
            set
            {
                string normalizedValue = NormalizeRecentHighlightsRangeKey(value);
                if (string.Equals(_selectedRecentHighlightsRangeKey, normalizedValue, StringComparison.Ordinal))
                {
                    return;
                }

                _selectedRecentHighlightsRangeKey = normalizedValue;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedRecentHighlightsRangeDisplayName));
            }
        }

        public string SelectedRecentHighlightsRangeDisplayName =>
            GetRecentHighlightsRangeDisplayName(SelectedRecentHighlightsRangeKey);

        public string CurrentRecentHighlightsDateRangeDisplay
        {
            get => _currentRecentHighlightsDateRangeDisplay;
            private set
            {
                if (string.Equals(_currentRecentHighlightsDateRangeDisplay, value, StringComparison.Ordinal))
                {
                    return;
                }

                _currentRecentHighlightsDateRangeDisplay = value;
                OnPropertyChanged();
            }
        }

        public string PreviousRecentHighlightsDateRangeDisplay
        {
            get => _previousRecentHighlightsDateRangeDisplay;
            private set
            {
                if (string.Equals(_previousRecentHighlightsDateRangeDisplay, value, StringComparison.Ordinal))
                {
                    return;
                }

                _previousRecentHighlightsDateRangeDisplay = value;
                OnPropertyChanged();
            }
        }

        public AffinityTopSummarySection CurrentRecentHighlightsSection
        {
            get => _currentRecentHighlightsSection;
            private set
            {
                if (ReferenceEquals(_currentRecentHighlightsSection, value))
                {
                    return;
                }

                _currentRecentHighlightsSection = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasCurrentRecentHighlights));
            }
        }

        public AffinityTopSummarySection PreviousRecentHighlightsSection
        {
            get => _previousRecentHighlightsSection;
            private set
            {
                if (ReferenceEquals(_previousRecentHighlightsSection, value))
                {
                    return;
                }

                _previousRecentHighlightsSection = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasPreviousRecentHighlights));
            }
        }

        public bool HasCurrentRecentHighlights =>
            CurrentRecentHighlightsSection?.HasFeaturedGame == true ||
            CurrentRecentHighlightsSection?.HasFeaturedTrack == true ||
            CurrentRecentHighlightsSection?.HasFeaturedCar == true;

        public bool HasPreviousRecentHighlights =>
            PreviousRecentHighlightsSection?.HasFeaturedGame == true ||
            PreviousRecentHighlightsSection?.HasFeaturedTrack == true ||
            PreviousRecentHighlightsSection?.HasFeaturedCar == true;

        public string CurrentRecentHighlightsEmptyStateText => "No driving history for this period yet";

        public string PreviousRecentHighlightsEmptyStateText => "No driving history for this period yet";

        public string CurrentContext => $"{CurrentGameName} / {CurrentCarModel} / {GetDisplayTrackNameWithConfig(CurrentGameName, CurrentTrackNameWithConfig)}";

        public string DistanceUnitLabel => Settings.DisplayInMiles ? "mi" : "km";

        public string DistanceColumnHeader => Settings.DisplayInMiles ? "Distance (mi)" : "Distance (km)";

        public string LiveStatusLabel => IsTelemetryActive ? "Tracking" : "Standby";

        public double CurrentContextDistanceDisplay => Settings.DisplayInMiles
            ? CurrentContextDistanceKm * MetersPerKilometer / MetersPerMile
            : CurrentContextDistanceKm;

        public string CurrentContextTotalDisplay => $"{CurrentContextDistanceDisplay:F2} {DistanceUnitLabel}";

        public double SessionDistanceDisplay => Settings.DisplayInMiles
            ? SessionDistanceKm * MetersPerKilometer / MetersPerMile
            : SessionDistanceKm;

        public string CurrentSessionDistanceDisplay => $"{SessionDistanceDisplay:F2} {DistanceUnitLabel}";

        public double TotalDistanceDisplay => Settings.DisplayInMiles
            ? TotalDistanceKm * MetersPerKilometer / MetersPerMile
            : TotalDistanceKm;

        public string CurrentContextUsedTimeDisplay => FormatUsedTime(_currentContextUsedTime);

        public string TotalUsedTimeDisplay => FormatUsedTime(_totalUsedTime);

        public Brush StatusSectionForeground => IsTelemetryActive ? Brushes.LimeGreen : Brushes.Red;

        public string CurrentGameName
        {
            get => _currentGameName;
            private set
            {
                if (_currentGameName == value)
                {
                    return;
                }

                _currentGameName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentContext));
            }
        }

        public string CurrentCarModel
        {
            get => _currentCarModel;
            private set
            {
                if (_currentCarModel == value)
                {
                    return;
                }

                _currentCarModel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentContext));
            }
        }

        public string CurrentTrackName
        {
            get => _currentTrackName;
            private set
            {
                if (_currentTrackName == value)
                {
                    return;
                }

                _currentTrackName = value;
                OnPropertyChanged();
            }
        }

        public string CurrentTrackNameWithConfig
        {
            get => _currentTrackNameWithConfig;
            private set
            {
                if (_currentTrackNameWithConfig == value)
                {
                    return;
                }

                _currentTrackNameWithConfig = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentContext));
            }
        }

        public string DataStatus
        {
            get => _dataStatus;
            private set
            {
                if (_dataStatus == value)
                {
                    return;
                }

                _dataStatus = value;
                OnPropertyChanged();
            }
        }

        public double CurrentContextDistanceKm
        {
            get => _currentContextDistanceKm;
            private set
            {
                if (Math.Abs(_currentContextDistanceKm - value) < 0.0001)
                {
                    return;
                }

                _currentContextDistanceKm = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentContextDistanceDisplay));
                OnPropertyChanged(nameof(CurrentContextTotalDisplay));
            }
        }

        public double SessionDistanceKm
        {
            get => _sessionDistanceKm;
            private set
            {
                if (Math.Abs(_sessionDistanceKm - value) < 0.0001)
                {
                    return;
                }

                _sessionDistanceKm = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SessionDistanceDisplay));
                OnPropertyChanged(nameof(CurrentSessionDistanceDisplay));
            }
        }

        public GameDistanceTab SelectedGameTab
        {
            get => _selectedGameTab;
            set
            {
                if (ReferenceEquals(_selectedGameTab, value))
                {
                    return;
                }

                _selectedGameTab = value;
                OnPropertyChanged();
            }
        }

        public object SelectedTopLevelTab
        {
            get => _selectedTopLevelTab;
            set
            {
                if (ReferenceEquals(_selectedTopLevelTab, value))
                {
                    return;
                }

                _selectedTopLevelTab = value;
                OnPropertyChanged();

                if (value is GameDistanceTab gameTab)
                {
                    SelectedGameTab = gameTab;
                }
                else if (!(value is AffinitySettingsTab) && !(value is AffinityOverviewTab))
                {
                    SelectedGameTab = null;
                }
            }
        }

        public double TotalDistanceKm
        {
            get => _totalDistanceKm;
            private set
            {
                if (Math.Abs(_totalDistanceKm - value) < 0.0001)
                {
                    return;
                }

                _totalDistanceKm = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalDistanceDisplay));
            }
        }

        public bool IsTelemetryActive
        {
            get => _isTelemetryActive;
            private set
            {
                if (_isTelemetryActive == value)
                {
                    return;
                }

                _isTelemetryActive = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LiveStatusLabel));
                OnPropertyChanged(nameof(StatusSectionForeground));
            }
        }

        public double CurrentContextUsedTime
        {
            get => _currentContextUsedTime;
            private set
            {
                if (Math.Abs(_currentContextUsedTime - value) < 0.0001)
                {
                    return;
                }

                _currentContextUsedTime = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentContextUsedTimeDisplay));
            }
        }

        public double TotalUsedTime
        {
            get => _totalUsedTime;
            private set
            {
                if (Math.Abs(_totalUsedTime - value) < 0.0001)
                {
                    return;
                }

                _totalUsedTime = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalUsedTimeDisplay));
            }
        }

        public void Init(PluginManager pluginManager)
        {
            PluginManager = pluginManager;
            InitializeStoragePaths(pluginManager);
            _acTrackMapPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ac_track_id_map.json");
            Settings = LoadSettings();
            EnsureDefaultGameDebugLoggingSettings();
            _assettoCorsaTrackMap = LoadAssettoCorsaTrackMap();
            _trackDisplayContext = new AffinityTrackDisplayContext(_assettoCorsaTrackMap);
            EnsureSqliteNativeLibraryReady();
            InitializeDatabase();
            _database = LoadRuntimeDatabase();

            pluginManager.AddProperty("Affinity.Version", GetType(), Version);
            pluginManager.AddProperty("Affinity.IsGameRunning", GetType(), false);
            pluginManager.AddProperty("Affinity.GameName", GetType(), string.Empty);
            pluginManager.AddProperty("Affinity.TrackName", GetType(), string.Empty);
            pluginManager.AddProperty("Affinity.CarModel", GetType(), string.Empty);
            pluginManager.AddProperty("Affinity.CurrentContextDistanceKm", GetType(), 0.0);
            pluginManager.AddProperty("Affinity.CurrentContextDistanceMiles", GetType(), 0.0);
            pluginManager.AddProperty("Affinity.SessionDistanceKm", GetType(), 0.0);
            pluginManager.AddProperty("Affinity.SessionDistanceMiles", GetType(), 0.0);
            pluginManager.AddProperty("Affinity.DataFilePath", GetType(), _databasePath);
            pluginManager.AddProperty("Affinity.DebugLogPath", GetType(), GetDebugLogPath(string.Empty));

            RefreshDistanceSummaries();
            RefreshGameDebugLoggingOptions();
            SimHub.Logging.Current.Info($"Affinity v{Version} - Initialised");
        }

        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            try
            {
                DateTime now = DateTime.UtcNow;
                if (!data.GameRunning)
                {
                    _automobilista2PlayerViewedParticipantIndex = -1;
                }

                pluginManager.SetPropertyValue("Affinity.IsGameRunning", GetType(), data.GameRunning);
                pluginManager.SetPropertyValue("Affinity.DataFilePath", GetType(), _databasePath);
                pluginManager.SetPropertyValue("Affinity.DebugLogPath", GetType(), GetDebugLogPath(string.Empty));

                if (!data.GameRunning || data.NewData == null)
                {
                    DataStatus = "Waiting for telemetry";
                    IsTelemetryActive = false;
                    bool finalizedTime = AccumulateActiveSessionTime(now);
                    FinalizeActiveSession(refreshSummaries: finalizedTime);
                    ResetActiveSession(clearContext: false);
                    PublishProperties(pluginManager, string.Empty, string.Empty, string.Empty, 0.0, 0.0);
                    _hasLoggedDataError = false;
                    return;
                }

                string gameName = NormalizeContextValue(data.GameName, "Unknown Game");
                string carModel = NormalizeContextValue(data.NewData.CarModel, "Unknown Car");
                string trackName = NormalizeContextValue(data.NewData.TrackName, "Unknown Track");
                string trackNameWithConfig = NormalizeContextValue(data.NewData.TrackNameWithConfig, trackName);
                bool isReplayTelemetry = IsReplayTelemetry(data);
                bool isInactiveTelemetry = IsInactiveTelemetry(gameName, data.NewData);
                if (isReplayTelemetry || isInactiveTelemetry)
                {
                    string ignoreReason = isReplayTelemetry
                        ? "replay-ignored"
                        : "inactive-ignored";
                    LogTelemetryDebugSnapshot(ignoreReason, data, gameName, carModel, trackNameWithConfig, data.SessionId, data.NewData, -1.0, 0.0, 0, false);
                    DataStatus = isReplayTelemetry
                        ? $"Ignoring replay telemetry for {gameName}"
                        : $"Ignoring inactive telemetry for {gameName}";
                    IsTelemetryActive = false;
                    bool finalizedTime = AccumulateActiveSessionTime(now);
                    FinalizeActiveSession(refreshSummaries: finalizedTime);
                    ResetActiveSession(clearContext: false);
                    PublishProperties(pluginManager, gameName, string.Empty, string.Empty, 0.0, 0.0);
                    _hasLoggedDataError = false;
                    return;
                }

                if (!IsSupportedGame(gameName))
                {
                    DataStatus = $"Unsupported game: {gameName}";
                    IsTelemetryActive = false;
                    bool finalizedTime = AccumulateActiveSessionTime(now);
                    FinalizeActiveSession(refreshSummaries: finalizedTime);
                    ResetActiveSession(clearContext: false);
                    PublishProperties(pluginManager, gameName, string.Empty, string.Empty, 0.0, 0.0);
                    _hasLoggedDataError = false;
                    return;
                }

                if (!HasReliableTelemetryContext(gameName, carModel, trackNameWithConfig))
                {
                    DataStatus = $"Waiting for {gameName} car/track telemetry";
                    IsTelemetryActive = false;
                    bool finalizedTime = AccumulateActiveSessionTime(now);
                    FinalizeActiveSession(refreshSummaries: finalizedTime);
                    ResetActiveSession(clearContext: false);
                    PublishProperties(pluginManager, gameName, string.Empty, string.Empty, 0.0, 0.0);
                    _hasLoggedDataError = false;
                    return;
                }

                if (EnsureGameDebugLoggingConfigured(gameName))
                {
                    RefreshGameDebugLoggingOptions();
                }
                pluginManager.SetPropertyValue("Affinity.DebugLogPath", GetType(), GetDebugLogPath(gameName));

                string contextKey = BuildContextKey(gameName, carModel, trackNameWithConfig);
                Guid sessionId = data.SessionId;
                double absoluteSessionMeters = -1.0;
                int completedLaps = Math.Max(0, data.NewData.CompletedLaps);
                bool shouldDebugTelemetry = ShouldDebugTelemetry(gameName);
                bool bucketTimeUpdated = AccumulateActiveSessionTime(now);

                bool activeContextChanged =
                    !string.Equals(_activeContextKey, contextKey, StringComparison.OrdinalIgnoreCase) ||
                    _activeSessionId != sessionId ||
                    data.NewData.IsSessionRestart;
                bool promotedAccTrackContext = false;
                if (activeContextChanged && !data.NewData.IsSessionRestart)
                {
                    promotedAccTrackContext = TryPromoteAccTrackContext(sessionId, gameName, carModel, trackName, trackNameWithConfig);
                }

                if (activeContextChanged && !promotedAccTrackContext)
                {
                    FinalizeActiveSession(refreshSummaries: bucketTimeUpdated);

                    if (ShouldIgnorePlaceholderSessionStart(gameName, data.NewData, completedLaps))
                    {
                        double lastIgnoredSessionMeters = _lastIgnoredSessionMeters;
                        ResetActiveSession(clearContext: false);
                        _lastIgnoredSessionMeters = lastIgnoredSessionMeters;
                        DataStatus = "Waiting for LMU telemetry reset after exit";
                        IsTelemetryActive = false;

                        PublishProperties(
                            pluginManager,
                            CurrentGameName,
                            CurrentTrackName,
                            CurrentCarModel,
                            CurrentContextDistanceKm,
                            SessionDistanceKm);
                        return;
                    }

                    _activeContextKey = contextKey;
                    _activeSessionId = sessionId;
                    _activeStorageSessionUid = Guid.NewGuid().ToString("N");
                    _activeSessionStartedUtc = now;
                    _sessionDistanceSource = ResolveSessionDistanceSource(gameName, data.NewData);
                    _sessionStartTrackPositionMeters = GetSessionStartTrackPositionMeters(gameName, data.NewData);
                    _sessionStatefulAbsoluteMeters = 0.0;
                    _lastTrackPositionWithinLapMeters = GetTrackPositionWithinLapMeters(data.NewData, data.NewData.TrackLength > 0.0 ? data.NewData.TrackLength : data.NewData.ReportedTrackLength);
                    _sessionDistanceOriginMeters = ShouldUseZeroSessionOrigin(gameName, _sessionDistanceSource)
                        ? 0.0
                        : GetAbsoluteSessionDistanceMeters(gameName, data.NewData, _sessionDistanceSource);
                    _lastObservedSessionMeters = 0.0;
                    _lastObservedCompletedLaps = completedLaps;
                    _lastSessionSampleUtc = now;
                    _activeSessionUsedTimeSeconds = 0.0;
                    SessionDistanceKm = 0.0;
                    TrackBucket activeBucket = GetOrCreateTrackBucket(gameName, carModel, trackName, trackNameWithConfig);
                    CurrentContextDistanceKm = activeBucket.TotalDistanceMeters / MetersPerKilometer;
                    CurrentContextUsedTime = activeBucket.UsedTime;
                    DataStatus = "Tracking session distance and time";
                    IsTelemetryActive = true;

                    if (shouldDebugTelemetry)
                    {
                        LogTelemetryDebugSnapshot("session-start", data, gameName, carModel, trackNameWithConfig, sessionId, data.NewData, -1.0, 0.0, 0, false);
                    }
                }
                else
                {
                    double trackLengthMeters = data.NewData.TrackLength > 0.0 ? data.NewData.TrackLength : data.NewData.ReportedTrackLength;
                    bool usesStatefulDerivedDistance = UsesStatefulDerivedDistance(gameName) &&
                        _sessionDistanceSource == SessionDistanceSource.Derived;

                    if (usesStatefulDerivedDistance && LooksLikeTransientIracingZeroDrop(gameName, data.NewData, completedLaps, trackLengthMeters))
                    {
                        SessionDistanceKm = _lastObservedSessionMeters / MetersPerKilometer;
                        DataStatus = "Ignoring transient iRacing telemetry reset";
                        IsTelemetryActive = true;

                        if (shouldDebugTelemetry)
                        {
                            LogTelemetryDebugSnapshot("transient-zero-drop", data, gameName, carModel, trackNameWithConfig, sessionId, data.NewData, 0.0, _lastObservedSessionMeters, completedLaps - _lastObservedCompletedLaps, false);
                        }

                        PublishProperties(
                            pluginManager,
                            CurrentGameName,
                            CurrentTrackNameWithConfig,
                            CurrentCarModel,
                            CurrentContextDistanceKm,
                            SessionDistanceKm);
                        return;
                    }

                    if (usesStatefulDerivedDistance)
                    {
                        absoluteSessionMeters = UpdateStatefulDerivedAbsoluteSessionDistanceMeters(gameName, data.NewData, trackLengthMeters);
                    }
                    else
                    {
                        absoluteSessionMeters = GetAbsoluteSessionDistanceMeters(gameName, data.NewData, _sessionDistanceSource);
                    }

                    if (absoluteSessionMeters < 0.0)
                    {
                        return;
                    }

                    double sessionMeters = Math.Max(0.0, absoluteSessionMeters - _sessionDistanceOriginMeters);
                    double deltaMeters = sessionMeters - _lastObservedSessionMeters;
                    int lapDelta = completedLaps - _lastObservedCompletedLaps;
                    bool shouldIgnoreDistanceJumpForIgnoredLapIncrement = ShouldIgnoreDistanceJumpForIgnoredLapIncrement(
                        gameName,
                        data.NewData,
                        completedLaps,
                        lapDelta,
                        trackLengthMeters,
                        deltaMeters);
                    bool shouldIgnoreRepeatedIgnoredDistanceJump = ShouldIgnoreRepeatedIgnoredDistanceJump(sessionMeters);
                    bool looksLikeDerivedLapBoundaryWrap = _sessionDistanceSource == SessionDistanceSource.Derived &&
                        trackLengthMeters > 0.0 &&
                        lapDelta == 0 &&
                        _lastObservedSessionMeters > 0.0 &&
                        sessionMeters + (trackLengthMeters * 0.75) < _lastObservedSessionMeters &&
                        sessionMeters + (trackLengthMeters * 1.25) > _lastObservedSessionMeters;
                    bool looksLikeInitialPositionSnap = deltaMeters > 0.0 &&
                        lapDelta == 0 &&
                        completedLaps == 0 &&
                        _lastObservedSessionMeters <= 25.0 &&
                        sessionMeters >= Math.Max(200.0, trackLengthMeters * 0.25) &&
                        !IsAutomobilista2Game(gameName) &&
                        !IsProjectMotorRacingGame(gameName) &&
                        data.NewData.SpeedKmh < 5.0;
                    TrackBucket bucket = GetOrCreateTrackBucket(gameName, carModel, trackName, trackNameWithConfig);
                    bool bucketUpdated = false;

                    if (looksLikeDerivedLapBoundaryWrap)
                    {
                        _lastIgnoredSessionMeters = -1.0;
                        SessionDistanceKm = _lastObservedSessionMeters / MetersPerKilometer;
                        DataStatus = "Waiting for telemetry sync at line";
                        IsTelemetryActive = true;

                        if (shouldDebugTelemetry)
                        {
                            LogTelemetryDebugSnapshot("lap-wrap-wait", data, gameName, carModel, trackNameWithConfig, sessionId, data.NewData, deltaMeters, sessionMeters, lapDelta, false);
                        }
                    }
                    else if (looksLikeInitialPositionSnap)
                    {
                        _lastIgnoredSessionMeters = -1.0;
                        _lastObservedSessionMeters = sessionMeters;
                        SessionDistanceKm = sessionMeters / MetersPerKilometer;
                        DataStatus = "Ignoring initial telemetry position snap";
                        IsTelemetryActive = true;

                        if (shouldDebugTelemetry)
                        {
                            LogTelemetryDebugSnapshot("initial-snap", data, gameName, carModel, trackNameWithConfig, sessionId, data.NewData, deltaMeters, sessionMeters, lapDelta, true);
                        }
                    }
                    else if (shouldIgnoreDistanceJumpForIgnoredLapIncrement || shouldIgnoreRepeatedIgnoredDistanceJump)
                    {
                        _lastIgnoredSessionMeters = sessionMeters;
                        SessionDistanceKm = _lastObservedSessionMeters / MetersPerKilometer;
                        DataStatus = "Ignoring low-speed telemetry distance jump at line";
                        IsTelemetryActive = true;

                        if (shouldDebugTelemetry)
                        {
                            LogTelemetryDebugSnapshot("lap-distance-ignored", data, gameName, carModel, trackNameWithConfig, sessionId, data.NewData, deltaMeters, sessionMeters, lapDelta, false);
                        }
                    }
                    else if (deltaMeters > 0.0)
                    {
                        _lastIgnoredSessionMeters = -1.0;
                        bucket.TotalDistanceMeters += deltaMeters;
                        bucket.LastUpdatedUtc = DateTime.UtcNow;
                        _pendingMetersSinceSave += deltaMeters;
                        _lastObservedSessionMeters = sessionMeters;
                        SessionDistanceKm = sessionMeters / MetersPerKilometer;
                        bucketUpdated = true;

                        if (shouldDebugTelemetry && ShouldLogTelemetryProgress(deltaMeters, lapDelta, trackLengthMeters))
                        {
                            LogTelemetryDebugSnapshot("progress", data, gameName, carModel, trackNameWithConfig, sessionId, data.NewData, deltaMeters, sessionMeters, lapDelta, false);
                        }
                    }
                    else if (sessionMeters + 1.0 < _lastObservedSessionMeters)
                    {
                        _lastIgnoredSessionMeters = -1.0;
                        _lastObservedSessionMeters = sessionMeters;
                        SessionDistanceKm = sessionMeters / MetersPerKilometer;
                        DataStatus = "Session distance reset detected";
                        IsTelemetryActive = true;

                        if (shouldDebugTelemetry)
                        {
                            LogTelemetryDebugSnapshot("distance-reset", data, gameName, carModel, trackNameWithConfig, sessionId, data.NewData, deltaMeters, sessionMeters, lapDelta, false);
                        }
                    }

                    bool shouldIgnoreLapIncrement = LooksLikeIgnoredLapIncrement(gameName, data.NewData, completedLaps, lapDelta, trackLengthMeters);

                    if (lapDelta > 0 && shouldIgnoreLapIncrement)
                    {
                        _lastObservedCompletedLaps = completedLaps;
                        DataStatus = "Ignoring low-speed telemetry transition at line";
                        IsTelemetryActive = true;

                        if (shouldDebugTelemetry)
                        {
                            LogTelemetryDebugSnapshot("lap-increment-ignored", data, gameName, carModel, trackNameWithConfig, sessionId, data.NewData, deltaMeters, sessionMeters, lapDelta, false);
                        }
                    }
                    else if (lapDelta > 0)
                    {
                        bucket.LastUpdatedUtc = DateTime.UtcNow;
                        _lastObservedCompletedLaps = completedLaps;
                        bucketUpdated = true;

                        if (shouldDebugTelemetry)
                        {
                            LogTelemetryDebugSnapshot("lap-change", data, gameName, carModel, trackNameWithConfig, sessionId, data.NewData, deltaMeters, sessionMeters, lapDelta, false);
                        }
                    }
                    else if (completedLaps < _lastObservedCompletedLaps)
                    {
                        _lastObservedCompletedLaps = completedLaps;
                        DataStatus = "Session counter reset detected";
                        IsTelemetryActive = true;

                        if (shouldDebugTelemetry)
                        {
                            LogTelemetryDebugSnapshot("lap-reset", data, gameName, carModel, trackNameWithConfig, sessionId, data.NewData, deltaMeters, sessionMeters, lapDelta, false);
                        }
                    }

                    if (bucketUpdated)
                    {
                        CurrentContextDistanceKm = bucket.TotalDistanceMeters / MetersPerKilometer;
                        CurrentContextUsedTime = bucket.UsedTime;
                        DataStatus = $"Recorded {CurrentContextDistanceKm:F2} km for {CurrentContext}";
                        IsTelemetryActive = true;

                        RefreshLiveSummariesIfNeeded(force: false);
                    }
                    else if (shouldDebugTelemetry && ShouldLogTelemetryHeartbeat())
                    {
                        LogTelemetryDebugSnapshot("heartbeat", data, gameName, carModel, trackNameWithConfig, sessionId, data.NewData, deltaMeters, sessionMeters, lapDelta, false);
                    }
                }

                if (bucketTimeUpdated)
                {
                    RefreshLiveSummariesIfNeeded(force: false);
                }

                CurrentGameName = gameName;
                CurrentCarModel = carModel;
                CurrentTrackName = trackName;
                CurrentTrackNameWithConfig = trackNameWithConfig;
                TrackBucket currentBucket = GetOrCreateTrackBucket(gameName, carModel, trackName, trackNameWithConfig);
                CurrentContextDistanceKm = currentBucket.TotalDistanceMeters / MetersPerKilometer;
                CurrentContextUsedTime = currentBucket.UsedTime;
                IsTelemetryActive = true;
                PublishProperties(pluginManager, gameName, GetDisplayTrackNameWithConfig(gameName, trackNameWithConfig), carModel, CurrentContextDistanceKm, SessionDistanceKm);
                _hasLoggedDataError = false;
            }
            catch (Exception ex)
            {
                if (_hasLoggedDataError)
                {
                    return;
                }

                SimHub.Logging.Current.Error($"Affinity - DataUpdate error: {ex}");
                _hasLoggedDataError = true;
            }
        }

        public void End(PluginManager pluginManager)
        {
            AccumulateActiveSessionTime(DateTime.UtcNow);
            FinalizeActiveSession(refreshSummaries: false);
            _sqliteRepository?.Dispose();
            _sqliteRepository = null;
            BackupDatabaseFile();
            SaveSettings();
            SimHub.Logging.Current.Info("Affinity - Shutting down");
        }

        internal static string GetAffinityStorageRoot(string commonStorageRoot)
        {
            if (string.IsNullOrWhiteSpace(commonStorageRoot))
            {
                return Path.Combine("PluginsData", "Affinity");
            }

            string trimmedPath = commonStorageRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string pluginsDataRoot = Directory.GetParent(trimmedPath)?.FullName ?? trimmedPath;
            return Path.Combine(pluginsDataRoot, "Affinity");
        }

        internal static void MigrateFileIfNeeded(string targetPath, params string[] candidatePaths)
        {
            if (string.IsNullOrWhiteSpace(targetPath) || File.Exists(targetPath) || candidatePaths == null)
            {
                return;
            }

            string directory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            foreach (string candidatePath in candidatePaths)
            {
                if (string.IsNullOrWhiteSpace(candidatePath) || !File.Exists(candidatePath))
                {
                    continue;
                }

                File.Move(candidatePath, targetPath);
                return;
            }
        }

        internal static string ResolveLegacyDataPath(string affinityStorageRoot, string commonStorageRoot)
        {
            string affinityPath = Path.Combine(affinityStorageRoot ?? string.Empty, LegacyDataFileName);
            string commonAffinityPath = Path.Combine(commonStorageRoot ?? string.Empty, "Affinity", LegacyDataFileName);
            string commonRootPath = Path.Combine(commonStorageRoot ?? string.Empty, LegacyDataFileName);

            if (File.Exists(affinityPath))
            {
                return affinityPath;
            }

            if (File.Exists(commonAffinityPath))
            {
                return commonAffinityPath;
            }

            if (File.Exists(commonRootPath))
            {
                return commonRootPath;
            }

            return affinityPath;
        }

        internal static void BackupFileIfPresent(string sourcePath, string backupPath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) ||
                string.IsNullOrWhiteSpace(backupPath) ||
                !File.Exists(sourcePath))
            {
                return;
            }

            string directory = Path.GetDirectoryName(backupPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            const int backupCount = 5;
            for (int index = backupCount; index >= 2; index--)
            {
                string olderBackupPath = backupPath + "." + index;
                string newerBackupPath = backupPath + "." + (index - 1);

                if (File.Exists(olderBackupPath))
                {
                    File.Delete(olderBackupPath);
                }

                if (File.Exists(newerBackupPath))
                {
                    File.Move(newerBackupPath, olderBackupPath);
                }
            }

            string previousSingleBackupPath = backupPath;
            string previousLatestBackupPath = backupPath + ".2";
            if (File.Exists(previousSingleBackupPath) && !File.Exists(previousLatestBackupPath))
            {
                File.Move(previousSingleBackupPath, previousLatestBackupPath);
            }
            else if (File.Exists(previousSingleBackupPath))
            {
                File.Delete(previousSingleBackupPath);
            }

            File.Copy(sourcePath, backupPath + ".1", overwrite: true);
        }

        internal static IReadOnlyList<string> GetSqliteInteropProbePaths(string pluginRoot, string affinityStorageRoot, bool is64BitProcess)
        {
            string architectureFolder = is64BitProcess ? "x64" : "x86";
            string pluginInteropPath = string.IsNullOrWhiteSpace(pluginRoot)
                ? Path.Combine(architectureFolder, SqliteInteropFileName)
                : Path.Combine(pluginRoot, architectureFolder, SqliteInteropFileName);
            string recoveryInteropPath = string.IsNullOrWhiteSpace(affinityStorageRoot)
                ? Path.Combine(SqliteNativeRecoveryFolderName, architectureFolder, SqliteInteropFileName)
                : Path.Combine(affinityStorageRoot, SqliteNativeRecoveryFolderName, architectureFolder, SqliteInteropFileName);

            return new[]
            {
                pluginInteropPath,
                recoveryInteropPath
            };
        }

        internal static bool TryCopySqliteInteropToRecoveryPath(string sourcePath, string recoveryPath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) ||
                string.IsNullOrWhiteSpace(recoveryPath) ||
                !File.Exists(sourcePath))
            {
                return false;
            }

            string normalizedSourcePath = Path.GetFullPath(sourcePath);
            string normalizedRecoveryPath = Path.GetFullPath(recoveryPath);
            if (string.Equals(normalizedSourcePath, normalizedRecoveryPath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string directory = Path.GetDirectoryName(normalizedRecoveryPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.Copy(normalizedSourcePath, normalizedRecoveryPath, overwrite: true);
            return true;
        }

        internal static string BuildSqliteInitializationFailureMessage(Exception ex)
        {
            var builder = new StringBuilder();
            builder.Append("Affinity - Failed to initialize SQLite database. ");
            builder.Append("SQLite native files are missing or unloadable. ");
            builder.Append("Reinstall Affinity, especially after a SimHub update.");

            if (ex != null)
            {
                builder.Append(" Exception: ");
                builder.Append(ex);
            }

            return builder.ToString();
        }

        public Control GetWPFSettingsControl(PluginManager pluginManager)
        {
            return new AffinitySimHub(this)
            {
                DataContext = this
            };
        }

        public System.Windows.Forms.Control GetSettingsControl(PluginManager pluginManager)
        {
            return null;
        }

        internal void SaveSettings()
        {
            try
            {
                string directory = Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonConvert.SerializeObject(Settings, Formatting.Indented);
                File.WriteAllText(_settingsPath, json, Encoding.UTF8);
                SettingsStatus = $"Settings saved at {DateTime.Now.ToShortTimeString()}";
            }
            catch (Exception ex)
            {
                SettingsStatus = "Settings save failed; see SimHub log";
                SimHub.Logging.Current.Error($"Affinity - Failed to save settings: {ex.Message}");
            }
        }

        private void InitializeStoragePaths(PluginManager pluginManager)
        {
            string commonStorageRoot = pluginManager.GetCommonStoragePath();
            string affinityStorageRoot = GetAffinityStorageRoot(commonStorageRoot);
            string commonAffinityStorageRoot = Path.Combine(commonStorageRoot, "Affinity");

            _settingsPath = Path.Combine(affinityStorageRoot, SettingsFileName);
            _databasePath = Path.Combine(affinityStorageRoot, SqliteDataFileName);
            _debugLogPath = Path.Combine(affinityStorageRoot, DebugLogFileName);

            TryMigrateStorageFile(
                _settingsPath,
                Path.Combine(commonAffinityStorageRoot, SettingsFileName),
                Path.Combine(commonStorageRoot, SettingsFileName));
            TryMigrateStorageFile(
                _databasePath,
                Path.Combine(commonAffinityStorageRoot, SqliteDataFileName),
                Path.Combine(commonStorageRoot, SqliteDataFileName));

            _legacyDatabasePath = ResolveLegacyDataPath(affinityStorageRoot, commonStorageRoot);
        }

        private void TryMigrateStorageFile(string targetPath, params string[] candidatePaths)
        {
            try
            {
                MigrateFileIfNeeded(targetPath, candidatePaths);
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Warn($"Affinity - Failed to migrate storage file to {targetPath}: {ex.Message}");
            }
        }

        private void BackupDatabaseFile()
        {
            try
            {
                BackupFileIfPresent(_databasePath, _databasePath + ".bak");
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Warn($"Affinity - Failed to back up database: {ex.Message}");
            }
        }

        internal void ResetSettings()
        {
            Settings.Reset();
            EnsureDefaultGameDebugLoggingSettings();
            SaveSettings();
            RefreshGameDebugLoggingOptions();
            OnPropertyChanged(nameof(Settings));
            OnPropertyChanged(nameof(IsDebugLoggingEnabled));
            RefreshDistanceSummaries();
            NotifyDistanceDisplayChanged();
        }

        internal void RefreshDisplaySettings()
        {
            RefreshDistanceSummaries();
            NotifyDistanceDisplayChanged();
        }

        internal void RefreshDistanceSummaries()
        {
            AffinitySummarySnapshot snapshot = AffinitySummaryBuilder.BuildSnapshot(
                database: _database,
                displayInMiles: Settings.DisplayInMiles,
                assettoCorsaTrackMap: _assettoCorsaTrackMap,
                tryResolveGameLogoPath: TryResolveGameLogoPath,
                tryResolveGameLogo: TryLoadGameLogo,
                gameProfiles: _gameProfiles);
            DateTime nowLocal = DateTime.Now;
            AffinitySummarySnapshot thisMonthSnapshot = BuildMonthlySummarySnapshot(nowLocal);
            AffinitySummarySnapshot lastMonthSnapshot = BuildMonthlySummarySnapshot(nowLocal.AddMonths(-1));
            BuildRecentHighlightsSummarySnapshots(
                nowLocal,
                out AffinitySummarySnapshot currentRecentHighlightsSnapshot,
                out AffinitySummarySnapshot previousRecentHighlightsSnapshot,
                out string currentRecentHighlightsHeader,
                out string currentRecentHighlightsDateRangeDisplay,
                out string previousRecentHighlightsHeader,
                out string previousRecentHighlightsDateRangeDisplay);
            ExecuteOnUiThread(() => ApplySummarySnapshot(
                snapshot,
                thisMonthSnapshot,
                lastMonthSnapshot,
                currentRecentHighlightsSnapshot,
                previousRecentHighlightsSnapshot,
                currentRecentHighlightsHeader,
                currentRecentHighlightsDateRangeDisplay,
                previousRecentHighlightsHeader,
                previousRecentHighlightsDateRangeDisplay));
        }

        internal void ClearSelectedGameTabFilter()
        {
            SelectedGameTab?.ClearFilter();
        }

        internal void ApplySelectedGameTabTimeFilter()
        {
            GameDistanceTab selectedTab = SelectedGameTab;
            if (selectedTab == null)
            {
                return;
            }

            if (!TryGetGameTabTimePeriodUtcRange(
                selectedTab.SelectedTimePeriodFilterKey,
                DateTime.Now,
                out DateTime? startUtc,
                out DateTime? endUtc))
            {
                selectedTab.SetTimePeriodSummaries(selectedTab.RawSummaries);
                return;
            }

            if (_sqliteRepository == null)
            {
                selectedTab.SetTimePeriodSummaries(Enumerable.Empty<DistanceSummary>());
                return;
            }

            List<DistanceSummary> periodRows = _sqliteRepository
                .GetDistanceSummaries(startUtc, endUtc)
                .Where(summary => string.Equals(summary.GameName, selectedTab.GameName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            GameDistanceTab periodTab = AffinitySummaryBuilder
                .BuildSnapshot(
                    periodRows,
                    Settings.DisplayInMiles,
                    _assettoCorsaTrackMap,
                    TryResolveGameLogoPath,
                    TryLoadGameLogo,
                    _gameProfiles)
                .GameTabs
                .FirstOrDefault(tab => string.Equals(tab.GameName, selectedTab.GameName, StringComparison.OrdinalIgnoreCase));

            selectedTab.SetTimePeriodSummaries(periodTab?.RawSummaries ?? Enumerable.Empty<DistanceSummary>());
        }

        internal void ApplySelectedRecentHighlightsRange()
        {
            RefreshDistanceSummaries();
        }

        internal static bool TryGetGameTabTimePeriodUtcRange(
            string periodKey,
            DateTime referenceLocal,
            out DateTime? startUtc,
            out DateTime? endUtc)
        {
            DateTime localReference = referenceLocal.Kind == DateTimeKind.Utc
                ? referenceLocal.ToLocalTime()
                : referenceLocal;
            DateTime localStart;
            DateTime localEnd;

            switch (periodKey)
            {
                case GameDistanceTab.TimePeriodThisMonth:
                    localStart = new DateTime(localReference.Year, localReference.Month, 1, 0, 0, 0, DateTimeKind.Local);
                    localEnd = localStart.AddMonths(1);
                    break;
                case GameDistanceTab.TimePeriodLastMonth:
                    localEnd = new DateTime(localReference.Year, localReference.Month, 1, 0, 0, 0, DateTimeKind.Local);
                    localStart = localEnd.AddMonths(-1);
                    break;
                case GameDistanceTab.TimePeriodLast7Days:
                    localStart = localReference.AddDays(-7);
                    localEnd = localReference;
                    break;
                case GameDistanceTab.TimePeriodLast30Days:
                    localStart = localReference.AddDays(-30);
                    localEnd = localReference;
                    break;
                case GameDistanceTab.TimePeriodThisYear:
                    localStart = new DateTime(localReference.Year, 1, 1, 0, 0, 0, DateTimeKind.Local);
                    localEnd = localStart.AddYears(1);
                    break;
                default:
                    startUtc = null;
                    endUtc = null;
                    return false;
            }

            startUtc = localStart.ToUniversalTime();
            endUtc = localEnd.ToUniversalTime();
            return true;
        }

        internal static bool TryGetRecentHighlightsRangeUtcRanges(
            string rangeKey,
            DateTime referenceLocal,
            out DateTime? currentStartUtc,
            out DateTime? currentEndUtc,
            out DateTime? previousStartUtc,
            out DateTime? previousEndUtc)
        {
            DateTime localReference = referenceLocal.Kind == DateTimeKind.Utc
                ? referenceLocal.ToLocalTime()
                : referenceLocal;

            switch (NormalizeRecentHighlightsRangeKey(rangeKey))
            {
                case RecentHighlightsRangeWeek:
                    DateTime currentWeekStart = GetStartOfWeek(localReference);
                    currentStartUtc = currentWeekStart.ToUniversalTime();
                    currentEndUtc = localReference.ToUniversalTime();
                    previousStartUtc = currentWeekStart.AddDays(-7).ToUniversalTime();
                    previousEndUtc = currentWeekStart.ToUniversalTime();
                    return true;
                case RecentHighlightsRangeMonth:
                    DateTime currentMonthStart = new DateTime(localReference.Year, localReference.Month, 1, 0, 0, 0, DateTimeKind.Local);
                    currentStartUtc = currentMonthStart.ToUniversalTime();
                    currentEndUtc = localReference.ToUniversalTime();
                    previousStartUtc = currentMonthStart.AddMonths(-1).ToUniversalTime();
                    previousEndUtc = currentMonthStart.ToUniversalTime();
                    return true;
                default:
                    currentStartUtc = null;
                    currentEndUtc = null;
                    previousStartUtc = null;
                    previousEndUtc = null;
                    return false;
            }
        }

        private AffinitySettings LoadSettings()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    return new AffinitySettings();
                }

                string json = File.ReadAllText(_settingsPath, Encoding.UTF8);
                return JsonConvert.DeserializeObject<AffinitySettings>(json) ?? new AffinitySettings();
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Warn($"Affinity - Failed to load settings, using defaults: {ex.Message}");
                return new AffinitySettings();
            }
        }

        private AffinityDatabase LoadLegacyDatabase()
        {
            try
            {
                if (!File.Exists(_legacyDatabasePath))
                {
                    return new AffinityDatabase();
                }

                string json = File.ReadAllText(_legacyDatabasePath, Encoding.UTF8);
                return JsonConvert.DeserializeObject<AffinityDatabase>(json) ?? new AffinityDatabase();
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Warn($"Affinity - Failed to load legacy database, using empty store: {ex.Message}");
                return new AffinityDatabase();
            }
        }

        private Dictionary<string, string> LoadAssettoCorsaTrackMap()
        {
            try
            {
                if (!File.Exists(_acTrackMapPath))
                {
                    return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                string json = File.ReadAllText(_acTrackMapPath, Encoding.UTF8);
                Dictionary<string, string> map = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                return map ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Warn($"Affinity - Failed to load AC track map: {ex.Message}");
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private void InitializeDatabase()
        {
            try
            {
                _sqliteRepository = new AffinitySqliteRepository(_databasePath);
                _sqliteRepository.Initialize();

                if (!_sqliteRepository.HasSessionData() && File.Exists(_legacyDatabasePath))
                {
                    AffinityDatabase legacyDatabase = LoadLegacyDatabase();
                    _sqliteRepository.ImportLegacyDatabase(legacyDatabase, DateTime.UtcNow.Date);
                    BackupLegacyDatabaseFile();
                    SimHub.Logging.Current.Info($"Affinity - Migrated distance history from {_legacyDatabasePath} to {_databasePath}");
                }
            }
            catch (Exception ex)
            {
                HandleSqliteInitializationFailure(ex);
            }
        }

        private void BackupLegacyDatabaseFile()
        {
            if (!File.Exists(_legacyDatabasePath))
            {
                return;
            }

            string backupPath = _legacyDatabasePath + ".bak";
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }

            File.Move(_legacyDatabasePath, backupPath);
        }

        private AffinityDatabase LoadRuntimeDatabase()
        {
            var database = new AffinityDatabase();
            if (_sqliteRepository == null)
            {
                return database;
            }

            foreach (DistanceSummary summary in _sqliteRepository.GetDistanceSummaries())
            {
                TrackBucket bucket = GetOrCreateTrackBucket(database, summary.GameName, summary.CarModel, summary.TrackName, summary.TrackNameWithConfig);
                bucket.TotalDistanceMeters = summary.TotalDistanceKm * MetersPerKilometer;
                bucket.UsedTime = summary.UsedTime;
                bucket.LastUpdatedUtc = summary.LastUpdatedUtc;
            }

            return database;
        }

        private TrackBucket GetOrCreateTrackBucket(string gameName, string carModel, string trackName, string trackNameWithConfig)
        {
            return GetOrCreateTrackBucket(_database, gameName, carModel, trackName, trackNameWithConfig);
        }

        private static TrackBucket GetOrCreateTrackBucket(AffinityDatabase database, string gameName, string carModel, string trackName, string trackNameWithConfig)
        {
            if (database == null)
            {
                database = new AffinityDatabase();
            }

            if (!database.Games.TryGetValue(gameName, out GameBucket gameBucket))
            {
                gameBucket = new GameBucket();
                database.Games[gameName] = gameBucket;
            }

            if (!gameBucket.Cars.TryGetValue(carModel, out CarBucket carBucket))
            {
                carBucket = new CarBucket();
                gameBucket.Cars[carModel] = carBucket;
            }

            if (!carBucket.Tracks.TryGetValue(trackNameWithConfig, out TrackBucket trackBucket))
            {
                trackBucket = new TrackBucket
                {
                    GameName = gameName,
                    CarModel = carModel,
                    TrackName = trackName,
                    TrackNameWithConfig = trackNameWithConfig,
                    CreatedUtc = DateTime.UtcNow,
                    LastUpdatedUtc = DateTime.UtcNow
                };
                carBucket.Tracks[trackNameWithConfig] = trackBucket;
            }

            return trackBucket;
        }

        private bool TryPromoteAccTrackContext(Guid sessionId, string gameName, string carModel, string trackName, string trackNameWithConfig)
        {
            if (!IsAssettoCorsaCompetizioneGame(gameName) ||
                _activeSessionId == Guid.Empty ||
                _activeSessionId != sessionId ||
                !string.Equals(CurrentGameName, gameName, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(CurrentCarModel, carModel, StringComparison.OrdinalIgnoreCase) ||
                !AffinityGameLogic.IsAccTrackNameUpgrade(CurrentTrackNameWithConfig, trackNameWithConfig))
            {
                return false;
            }

            TrackBucket promotedBucket = PromoteTrackBucket(
                _database,
                gameName,
                carModel,
                CurrentTrackNameWithConfig,
                trackName,
                trackNameWithConfig);
            if (promotedBucket == null)
            {
                return false;
            }

            CurrentTrackName = trackName;
            CurrentTrackNameWithConfig = trackNameWithConfig;
            _activeContextKey = BuildContextKey(gameName, carModel, trackNameWithConfig);
            CurrentContextDistanceKm = promotedBucket.TotalDistanceMeters / MetersPerKilometer;
            CurrentContextUsedTime = promotedBucket.UsedTime;
            return true;
        }

        private static TrackBucket PromoteTrackBucket(
            AffinityDatabase database,
            string gameName,
            string carModel,
            string previousTrackNameWithConfig,
            string trackName,
            string trackNameWithConfig)
        {
            if (database?.Games == null ||
                string.IsNullOrWhiteSpace(previousTrackNameWithConfig) ||
                string.IsNullOrWhiteSpace(trackNameWithConfig) ||
                !database.Games.TryGetValue(gameName, out GameBucket gameBucket) ||
                gameBucket?.Cars == null ||
                !gameBucket.Cars.TryGetValue(carModel, out CarBucket carBucket) ||
                carBucket?.Tracks == null ||
                !carBucket.Tracks.TryGetValue(previousTrackNameWithConfig, out TrackBucket previousBucket))
            {
                return null;
            }

            if (string.Equals(previousTrackNameWithConfig, trackNameWithConfig, StringComparison.OrdinalIgnoreCase))
            {
                previousBucket.TrackName = trackName;
                previousBucket.TrackNameWithConfig = trackNameWithConfig;
                return previousBucket;
            }

            if (carBucket.Tracks.TryGetValue(trackNameWithConfig, out TrackBucket promotedBucket))
            {
                if (!ReferenceEquals(previousBucket, promotedBucket))
                {
                    promotedBucket.TotalDistanceMeters += previousBucket.TotalDistanceMeters;
                    promotedBucket.UsedTime += previousBucket.UsedTime;
                    promotedBucket.CreatedUtc = EarlierUtc(promotedBucket.CreatedUtc, previousBucket.CreatedUtc);
                    promotedBucket.LastUpdatedUtc = LaterUtc(promotedBucket.LastUpdatedUtc, previousBucket.LastUpdatedUtc);
                    carBucket.Tracks.Remove(previousTrackNameWithConfig);
                }

                promotedBucket.GameName = gameName;
                promotedBucket.CarModel = carModel;
                promotedBucket.TrackName = trackName;
                promotedBucket.TrackNameWithConfig = trackNameWithConfig;
                return promotedBucket;
            }

            carBucket.Tracks.Remove(previousTrackNameWithConfig);
            previousBucket.GameName = gameName;
            previousBucket.CarModel = carModel;
            previousBucket.TrackName = trackName;
            previousBucket.TrackNameWithConfig = trackNameWithConfig;
            carBucket.Tracks[trackNameWithConfig] = previousBucket;
            return previousBucket;
        }

        private static DateTime EarlierUtc(DateTime left, DateTime right)
        {
            if (left == default)
            {
                return right;
            }

            if (right == default)
            {
                return left;
            }

            return left <= right ? left : right;
        }

        private static DateTime LaterUtc(DateTime left, DateTime right)
        {
            if (left == default)
            {
                return right;
            }

            if (right == default)
            {
                return left;
            }

            return left >= right ? left : right;
        }

        private SessionDistanceSource ResolveSessionDistanceSource(string gameName, StatusDataBase status)
        {
            if (IsAssettoCorsaGame(gameName) || IsRaceRoomGame(gameName) || IsAutomobilista2Game(gameName) || IsProjectMotorRacingGame(gameName) || IsIRacingGame(gameName) || IsRFactor2Game(gameName) || IsLmuGame(gameName))
            {
                return SessionDistanceSource.Derived;
            }

            double trackLengthMeters = status?.TrackLength > 0.0 ? status.TrackLength : status?.ReportedTrackLength ?? 0.0;
            double derivedSessionMeters = GetDerivedSessionDistanceMeters(status, trackLengthMeters);
            if (status?.SessionOdo > 0.0)
            {
                double sessionOdoMeters = status.SessionOdo;
                double sessionOdoKilometers = status.SessionOdo * MetersPerKilometer;
                if (derivedSessionMeters >= 0.0)
                {
                    return Math.Abs(sessionOdoMeters - derivedSessionMeters) <= Math.Abs(sessionOdoKilometers - derivedSessionMeters)
                        ? SessionDistanceSource.SessionOdoMeters
                        : SessionDistanceSource.SessionOdoKilometers;
                }

                return status.SessionOdo >= 100.0
                    ? SessionDistanceSource.SessionOdoMeters
                    : SessionDistanceSource.SessionOdoKilometers;
            }

            return derivedSessionMeters >= 0.0
                ? SessionDistanceSource.Derived
                : SessionDistanceSource.Unknown;
        }

        private double GetAbsoluteSessionDistanceMeters(string gameName, StatusDataBase status, SessionDistanceSource source)
        {
            if (status == null)
            {
                return -1.0;
            }

            double trackLengthMeters = status.TrackLength > 0.0 ? status.TrackLength : status.ReportedTrackLength;
            switch (source)
            {
                case SessionDistanceSource.Derived:
                    if (UsesStatefulDerivedDistance(gameName))
                    {
                        return _sessionStatefulAbsoluteMeters;
                    }

                    return GetDerivedSessionDistanceMeters(status, trackLengthMeters);
                case SessionDistanceSource.SessionOdoMeters:
                    return status.SessionOdo > 0.0 ? status.SessionOdo : -1.0;
                case SessionDistanceSource.SessionOdoKilometers:
                    return status.SessionOdo > 0.0 ? status.SessionOdo * MetersPerKilometer : -1.0;
                default:
                    return -1.0;
            }
        }

        private bool ShouldUseZeroSessionOrigin(string gameName, SessionDistanceSource source)
        {
            return source == SessionDistanceSource.Derived && IsAssettoCorsaGame(gameName) && !UsesStatefulDerivedDistance(gameName);
        }

        private bool ShouldDebugTelemetry(string gameName)
        {
            if (!Settings.EnableDebugLogging)
            {
                return false;
            }

            string settingsKey = GetDebugLoggingSettingsKey(gameName);
            if (string.IsNullOrWhiteSpace(settingsKey))
            {
                return false;
            }

            if (!Settings.GameDebugLogging.TryGetValue(settingsKey, out bool isEnabled))
            {
                Settings.GameDebugLogging[settingsKey] = true;
                return true;
            }

            return isEnabled;
        }

        private bool ShouldLogTelemetryProgress(double deltaMeters, int lapDelta, double trackLengthMeters)
        {
            if (lapDelta != 0)
            {
                return true;
            }

            if (deltaMeters >= Math.Max(100.0, trackLengthMeters * 0.25))
            {
                return true;
            }

            return ShouldLogTelemetryHeartbeat();
        }

        private bool ShouldLogTelemetryHeartbeat()
        {
            DateTime now = DateTime.UtcNow;
            if ((now - _lastTelemetryDebugLogUtc).TotalSeconds < 1.0)
            {
                return false;
            }

            _lastTelemetryDebugLogUtc = now;
            return true;
        }

        private void LogTelemetryDebugSnapshot(string reason, GameData data, string gameName, string carModel, string trackNameWithConfig, Guid sessionId, StatusDataBase status, double deltaMeters, double sessionMeters, int lapDelta, bool looksLikeInitialPositionSnap)
        {
            try
            {
                string debugLogPath = GetDebugLogPath(gameName);
                if (string.IsNullOrWhiteSpace(debugLogPath) || status == null || !ShouldDebugTelemetry(gameName))
                {
                    return;
                }

                string directory = Path.GetDirectoryName(debugLogPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                double trackLengthMeters = status.TrackLength > 0.0 ? status.TrackLength : status.ReportedTrackLength;
                double derivedSessionMeters = GetDerivedSessionDistanceMeters(status, trackLengthMeters);
                double sessionOdoMeters = status.SessionOdo > 0.0 ? status.SessionOdo : -1.0;
                double sessionOdoKilometers = status.SessionOdo > 0.0 ? status.SessionOdo * MetersPerKilometer : -1.0;
                double absoluteSessionMeters = GetAbsoluteSessionDistanceMeters(gameName, status, _sessionDistanceSource);
                bool replayDetected = IsReplayTelemetry(data);
                string gameDataIsGameReplay = GetDebugMemberValue(data, "IsGameReplay");
                string gameDataGameReplay = GetDebugMemberValue(data, "GameReplay");
                string gameDataReplayMode = GetDebugMemberValue(data, "ReplayMode");
                string statusIsGameReplay = GetDebugMemberValue(status, "IsGameReplay");
                string statusReplayMode = GetDebugMemberValue(status, "ReplayMode");
                string gameDataType = FormatDebugValue(data.GetType().FullName);
                string statusType = FormatDebugValue(status.GetType().FullName);
                string gameDataExtra = GetDebugMemberValues(data, GameDataDebugMemberNames);
                string statusExtra = GetDebugMemberValues(status, StatusDebugMemberNames);
                object rawData = GetRawStatusDataObject(status);
                string rawDataType = FormatDebugValue(rawData?.GetType().FullName);
                string rawDataExtra = GetDebugMemberValues(rawData, RawDataDebugMemberNames);
                string line = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{0:o} reason={1} game=\"{2}\" car=\"{3}\" track=\"{4}\" sessionId={5} source={6} originM={7:F2} absM={8:F2} sessM={9:F2} deltaM={10:F2} completedLaps={11} lapDelta={12} trackLenM={13:F2} reportedTrackLenM={14:F2} posM={15:F2} posPct={16:F5} sessOdoRaw={17:F5} sessOdoAsM={18:F2} sessOdoAsKmM={19:F2} derivedM={20:F2} speedKmh={21:F2} isRestart={22} initialSnap={23} replayDetected={24} gdIsGameReplay=\"{25}\" gdGameReplay=\"{26}\" gdReplayMode=\"{27}\" sdIsGameReplay=\"{28}\" sdReplayMode=\"{29}\" gdType=\"{30}\" sdType=\"{31}\" gdExtra=\"{32}\" sdExtra=\"{33}\" rawType=\"{34}\" rawExtra=\"{35}\" ams2PlayerViewedParticipantIndex={36}",
                    DateTime.UtcNow,
                    reason,
                    gameName,
                    carModel,
                    trackNameWithConfig,
                    sessionId,
                    _sessionDistanceSource,
                    _sessionDistanceOriginMeters,
                    absoluteSessionMeters,
                    sessionMeters,
                    deltaMeters,
                    Math.Max(0, status.CompletedLaps),
                    lapDelta,
                    status.TrackLength,
                    status.ReportedTrackLength,
                    status.TrackPositionMeters,
                    status.TrackPositionPercent,
                    status.SessionOdo,
                    sessionOdoMeters,
                    sessionOdoKilometers,
                    derivedSessionMeters,
                    status.SpeedKmh,
                    status.IsSessionRestart,
                    looksLikeInitialPositionSnap,
                    replayDetected,
                    gameDataIsGameReplay,
                    gameDataGameReplay,
                    gameDataReplayMode,
                    statusIsGameReplay,
                    statusReplayMode,
                    gameDataType,
                    statusType,
                    gameDataExtra,
                    statusExtra,
                    rawDataType,
                    rawDataExtra,
                    _automobilista2PlayerViewedParticipantIndex);

                File.AppendAllText(debugLogPath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Warn($"Affinity - Failed to write debug telemetry log: {ex.Message}");
            }
        }

        private double UpdateStatefulDerivedAbsoluteSessionDistanceMeters(string gameName, StatusDataBase status, double trackLengthMeters)
        {
            if (status == null || trackLengthMeters <= 0.0)
            {
                return -1.0;
            }

            double trackPositionMeters = GetTrackPositionWithinLapMeters(status, trackLengthMeters);
            if (trackPositionMeters < 0.0)
            {
                return _sessionStatefulAbsoluteMeters;
            }

            if (ShouldIgnoreStatefulStartupPlaceholder(gameName, status, trackPositionMeters))
            {
                return _sessionStatefulAbsoluteMeters;
            }

            if (_lastTrackPositionWithinLapMeters < 0.0)
            {
                _lastTrackPositionWithinLapMeters = trackPositionMeters;
                return _sessionStatefulAbsoluteMeters;
            }

            double deltaTrackPositionMeters = trackPositionMeters - _lastTrackPositionWithinLapMeters;
            if (LooksLikeIgnoredLowSpeedLineWrap(gameName, status, deltaTrackPositionMeters, trackLengthMeters))
            {
                _lastTrackPositionWithinLapMeters = trackPositionMeters;
                return _sessionStatefulAbsoluteMeters;
            }

            if (deltaTrackPositionMeters < -(trackLengthMeters * 0.5))
            {
                deltaTrackPositionMeters += trackLengthMeters;
            }
            else if (deltaTrackPositionMeters > trackLengthMeters * 0.5)
            {
                deltaTrackPositionMeters -= trackLengthMeters;
            }

            if (deltaTrackPositionMeters > 0.0)
            {
                _sessionStatefulAbsoluteMeters += deltaTrackPositionMeters;
            }

            if (IsRaceRoomGame(gameName) && Math.Max(0, status.CompletedLaps) > 0)
            {
                double lapCounterSessionMeters = GetDerivedSessionDistanceMeters(status, trackLengthMeters);
                if (lapCounterSessionMeters > _sessionStatefulAbsoluteMeters)
                {
                    _sessionStatefulAbsoluteMeters = lapCounterSessionMeters;
                }
            }

            _lastTrackPositionWithinLapMeters = trackPositionMeters;
            return _sessionStatefulAbsoluteMeters;
        }

        private bool ShouldIgnoreStatefulStartupPlaceholder(string gameName, StatusDataBase status, double trackPositionMeters)
        {
            if (!IsProjectMotorRacingGame(gameName) ||
                status == null ||
                Math.Max(0, status.CompletedLaps) != 0 ||
                status.SpeedKmh > 1.0 ||
                _sessionStatefulAbsoluteMeters > 0.0)
            {
                return false;
            }

            bool hasStartupAnchor = _sessionStartTrackPositionMeters > 25.0;
            if (!hasStartupAnchor && trackPositionMeters > 25.0)
            {
                _sessionStartTrackPositionMeters = trackPositionMeters;
                _lastTrackPositionWithinLapMeters = trackPositionMeters;
                return true;
            }

            if (!hasStartupAnchor)
            {
                return false;
            }

            if (trackPositionMeters <= 5.0 ||
                Math.Abs(trackPositionMeters - _sessionStartTrackPositionMeters) <= 5.0)
            {
                _lastTrackPositionWithinLapMeters = _sessionStartTrackPositionMeters;
                return true;
            }

            return false;
        }

        private static double GetDerivedSessionDistanceMeters(StatusDataBase status, double trackLengthMeters)
        {
            if (status == null || trackLengthMeters <= 0.0)
            {
                return -1.0;
            }

            double trackPositionMeters = GetTrackPositionWithinLapMeters(status, trackLengthMeters);
            if (trackPositionMeters < 0.0)
            {
                return -1.0;
            }

            return Math.Max(0, status.CompletedLaps) * trackLengthMeters + trackPositionMeters;
        }

        private double GetSessionStartTrackPositionMeters(string gameName, StatusDataBase status)
        {
            if ((!IsAutomobilista2Game(gameName) && !IsProjectMotorRacingGame(gameName)) || status == null)
            {
                return -1.0;
            }

            double trackLengthMeters = status.TrackLength > 0.0 ? status.TrackLength : status.ReportedTrackLength;
            return AffinityGameLogic.GetTrackPositionWithinLapMeters(status, trackLengthMeters);
        }

        private static double GetTrackPositionWithinLapMeters(StatusDataBase status, double trackLengthMeters)
        {
            return AffinityGameLogic.GetTrackPositionWithinLapMeters(status, trackLengthMeters);
        }

        private void PublishProperties(PluginManager pluginManager, string gameName, string trackName, string carModel, double totalKm, double sessionKm)
        {
            pluginManager.SetPropertyValue("Affinity.GameName", GetType(), gameName);
            pluginManager.SetPropertyValue("Affinity.TrackName", GetType(), trackName);
            pluginManager.SetPropertyValue("Affinity.CarModel", GetType(), carModel);
            pluginManager.SetPropertyValue("Affinity.CurrentContextDistanceKm", GetType(), totalKm);
            pluginManager.SetPropertyValue("Affinity.CurrentContextDistanceMiles", GetType(), totalKm * MetersPerKilometer / MetersPerMile);
            pluginManager.SetPropertyValue("Affinity.SessionDistanceKm", GetType(), sessionKm);
            pluginManager.SetPropertyValue("Affinity.SessionDistanceMiles", GetType(), sessionKm * MetersPerKilometer / MetersPerMile);
        }

        private void ResetActiveSession(bool clearContext)
        {
            _activeSessionId = Guid.Empty;
            _activeContextKey = string.Empty;
            _activeStorageSessionUid = string.Empty;
            _activeSessionStartedUtc = DateTime.MinValue;
            _activeSessionUsedTimeSeconds = 0.0;
            _sessionDistanceSource = SessionDistanceSource.Unknown;
            _sessionStartTrackPositionMeters = -1.0;
            _sessionStatefulAbsoluteMeters = 0.0;
            _lastTrackPositionWithinLapMeters = -1.0;
            _sessionDistanceOriginMeters = 0.0;
            _lastObservedSessionMeters = -1.0;
            _lastIgnoredSessionMeters = -1.0;
            _lastObservedCompletedLaps = -1;
            _pendingMetersSinceSave = 0.0;
            _pendingUsedTimeSecondsSinceSave = 0.0;
            _lastSessionSampleUtc = DateTime.MinValue;
            SessionDistanceKm = 0.0;
            CurrentContextDistanceKm = clearContext ? 0.0 : CurrentContextDistanceKm;
            CurrentContextUsedTime = clearContext ? 0.0 : CurrentContextUsedTime;
        }

        private static string BuildContextKey(string gameName, string carModel, string trackNameWithConfig)
        {
            return $"{gameName}|{carModel}|{trackNameWithConfig}";
        }

        private bool IsSupportedGame(string gameName)
        {
            return _gameProfiles.Resolve(gameName).IsSupported;
        }

        private bool HasReliableTelemetryContext(string gameName, string carModel, string trackNameWithConfig)
        {
            return AffinityGameLogic.HasReliableTelemetryContext(gameName, carModel, trackNameWithConfig);
        }

        private bool IsReplayTelemetry(GameData data)
        {
            // SimHub exposes replay state at runtime even though the local SDK stubs do not model it.
            if (data == null)
            {
                return false;
            }

            if (TryGetBooleanMemberValue(data, "IsGameReplay", out bool isGameReplay))
            {
                if (isGameReplay)
                {
                    return true;
                }
            }

            if (TryGetBooleanMemberValue(data, "GameReplay", out bool gameReplay))
            {
                if (gameReplay)
                {
                    return true;
                }
            }

            if (TryGetMemberValue(data, "ReplayMode", out object gameReplayModeValue))
            {
                if (IsReplayModeActive(gameReplayModeValue))
                {
                    return true;
                }
            }

            if (data.NewData == null)
            {
                return false;
            }

            if (TryGetBooleanMemberValue(data.NewData, "IsGameReplay", out bool statusReplay))
            {
                if (statusReplay)
                {
                    return true;
                }
            }

            if (TryGetMemberValue(data.NewData, "ReplayMode", out object statusReplayModeValue) &&
                IsReplayModeActive(statusReplayModeValue))
            {
                return true;
            }

            object rawData = GetRawStatusDataObject(data.NewData);
            if (TryGetBooleanMemberValue(rawData, "IsReplayPlaying", out bool rawReplayPlaying) && rawReplayPlaying)
            {
                return true;
            }

            if (TryGetMemberValue(rawData, "Telemetry", out object telemetry) &&
                TryGetBooleanMemberValue(telemetry, "IsReplayPlaying", out bool telemetryReplayPlaying) &&
                telemetryReplayPlaying)
            {
                return true;
            }

            return false;
        }

        private bool IsInactiveTelemetry(string gameName, StatusDataBase status)
        {
            if (status == null)
            {
                return false;
            }

            if (IsRaceRoomGame(gameName) && IsRaceRoomInactiveTelemetry(status))
            {
                return true;
            }

            return IsAutomobilista2Game(gameName) &&
                IsAutomobilista2InactiveTelemetry(status);
        }

        private bool IsAutomobilista2InactiveTelemetry(StatusDataBase status)
        {
            if (TryGetBooleanMemberValue(status, "IsInGarage", out bool isInGarage) && isInGarage ||
                TryGetBooleanMemberValue(status, "IsSpectator", out bool isSpectator) && isSpectator)
            {
                return true;
            }

            object rawData = GetRawStatusDataObject(status);
            if (TryGetIntegerMemberValue(rawData, "mGameState", out int gameState) &&
                gameState == Automobilista2ObservedReplayGameState)
            {
                return true;
            }

            if (!TryGetIntegerMemberValue(rawData, "mViewedParticipantIndex", out int viewedParticipantIndex) ||
                viewedParticipantIndex < 0)
            {
                return false;
            }

            if (_automobilista2PlayerViewedParticipantIndex < 0)
            {
                _automobilista2PlayerViewedParticipantIndex = viewedParticipantIndex;
                return false;
            }

            return viewedParticipantIndex != _automobilista2PlayerViewedParticipantIndex;
        }

        private static bool IsRaceRoomInactiveTelemetry(StatusDataBase status)
        {
            object rawData = GetRawStatusDataObject(status);
            return TryGetMemberValue(rawData, "FinishStatus", out object finishStatusValue) &&
                TryGetBooleanValue(finishStatusValue, out bool isFinishedStatusActive) &&
                isFinishedStatusActive ||
                TryGetMemberValue(rawData, "GamePlayerInGarage", out object playerInGarageValue) &&
                TryGetBooleanValue(playerInGarageValue, out bool isPlayerInGarage) &&
                isPlayerInGarage;
        }

        private static bool IsReplayModeActive(object replayModeValue)
        {
            if (replayModeValue == null)
            {
                return false;
            }

            if (replayModeValue is string || replayModeValue.GetType().IsEnum)
            {
                string replayModeText = replayModeValue.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(replayModeText))
                {
                    return false;
                }

                return !string.Equals(replayModeText, "None", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(replayModeText, "Off", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(replayModeText, "Disabled", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(replayModeText, "Live", StringComparison.OrdinalIgnoreCase);
            }

            return TryGetBooleanValue(replayModeValue, out bool replayModeFlag) &&
                replayModeFlag;
        }

        private static bool TryGetBooleanMemberValue(object source, string memberName, out bool value)
        {
            value = false;
            return TryGetMemberValue(source, memberName, out object rawValue) &&
                TryGetBooleanValue(rawValue, out value);
        }

        private static bool TryGetIntegerMemberValue(object source, string memberName, out int value)
        {
            value = 0;
            if (!TryGetMemberValue(source, memberName, out object rawValue) || rawValue == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToInt32(rawValue, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string GetDebugMemberValue(object source, string memberName)
        {
            if (!TryGetMemberValue(source, memberName, out object value))
            {
                return "<missing>";
            }

            return FormatDebugValue(value);
        }

        private static string GetDebugMemberValues(object source, IEnumerable<string> memberNames)
        {
            if (source == null)
            {
                return "<null-source>";
            }

            if (memberNames == null)
            {
                return "<none>";
            }

            List<string> values = new List<string>();
            HashSet<string> seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string memberName in memberNames)
            {
                if (string.IsNullOrWhiteSpace(memberName) || !seenNames.Add(memberName))
                {
                    continue;
                }

                if (!TryGetMemberValue(source, memberName, out object value))
                {
                    continue;
                }

                values.Add(memberName + "=" + FormatDebugValue(value));
            }

            return values.Count > 0
                ? string.Join(",", values)
                : "<none>";
        }

        private static string FormatDebugValue(object value)
        {
            if (value == null)
            {
                return "<null>";
            }

            return value.ToString()
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }

        private static object GetRawStatusDataObject(StatusDataBase status)
        {
            if (status == null)
            {
                return null;
            }

            try
            {
                return status.GetRawDataObject();
            }
            catch
            {
                return null;
            }
        }

        private static bool TryGetBooleanValue(object value, out bool result)
        {
            result = false;
            if (value == null)
            {
                return false;
            }

            if (value is bool boolValue)
            {
                result = boolValue;
                return true;
            }

            if (value is string stringValue)
            {
                return bool.TryParse(stringValue, out result);
            }

            try
            {
                result = Math.Abs(Convert.ToDouble(value, CultureInfo.InvariantCulture)) > 0.0001;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetMemberValue(object source, string memberName, out object value)
        {
            value = null;
            if (source == null || string.IsNullOrWhiteSpace(memberName))
            {
                return false;
            }

            Type sourceType = source.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;

            PropertyInfo property = sourceType.GetProperty(memberName, flags);
            if (property != null && property.GetIndexParameters().Length == 0)
            {
                value = property.GetValue(source);
                return true;
            }

            FieldInfo field = sourceType.GetField(memberName, flags);
            if (field != null)
            {
                value = field.GetValue(source);
                return true;
            }

            return false;
        }

        private bool IsAssettoCorsaGame(string gameName)
        {
            return AffinityGameLogic.IsAssettoCorsaGame(gameName);
        }

        private bool IsAssettoCorsaCompetizioneGame(string gameName)
        {
            return AffinityGameLogic.IsAssettoCorsaCompetizioneGame(gameName);
        }

        private bool IsRaceRoomGame(string gameName)
        {
            return AffinityGameLogic.IsRaceRoomGame(gameName);
        }

        private bool IsAutomobilista2Game(string gameName)
        {
            return AffinityGameLogic.IsAutomobilista2Game(gameName);
        }

        private bool IsProjectMotorRacingGame(string gameName)
        {
            return AffinityGameLogic.IsProjectMotorRacingGame(gameName);
        }

        private bool IsIRacingGame(string gameName)
        {
            return AffinityGameLogic.IsIRacingGame(gameName);
        }

        private bool IsRFactor2Game(string gameName)
        {
            return AffinityGameLogic.IsRFactor2Game(gameName);
        }

        private bool IsLmuGame(string gameName)
        {
            return AffinityGameLogic.IsLmuGame(gameName);
        }

        private bool UsesStatefulDerivedDistance(string gameName)
        {
            return IsAssettoCorsaGame(gameName) ||
                IsRaceRoomGame(gameName) ||
                IsAutomobilista2Game(gameName) ||
                IsProjectMotorRacingGame(gameName) ||
                IsIRacingGame(gameName) ||
                IsRFactor2Game(gameName) ||
                IsLmuGame(gameName);
        }

        private bool LooksLikeTransientIracingZeroDrop(string gameName, StatusDataBase status, int completedLaps, double trackLengthMeters)
        {
            if (!IsIRacingGame(gameName) ||
                status == null ||
                _sessionDistanceSource != SessionDistanceSource.Derived ||
                _lastObservedCompletedLaps <= 0 ||
                _lastObservedSessionMeters <= Math.Max(100.0, trackLengthMeters * 0.25))
            {
                return false;
            }

            return completedLaps == 0 &&
                status.SpeedKmh < 1.0 &&
                status.TrackPositionMeters <= 1.0 &&
                status.TrackPositionPercent <= 0.001;
        }

        private bool LooksLikeIgnoredLowSpeedLineWrap(string gameName, StatusDataBase status, double deltaTrackPositionMeters, double trackLengthMeters)
        {
            if (!IsRFactor2Game(gameName) ||
                status == null ||
                trackLengthMeters <= 0.0 ||
                Math.Abs(deltaTrackPositionMeters) <= trackLengthMeters * 0.5)
            {
                return false;
            }

            double trackPositionMeters = GetTrackPositionWithinLapMeters(status, trackLengthMeters);
            bool nearLine = trackPositionMeters <= 5.0 || trackPositionMeters >= trackLengthMeters - 5.0;

            return Math.Max(0, status.CompletedLaps) == 0 &&
                status.SpeedKmh <= 80.0 &&
                nearLine;
        }

        private bool LooksLikeIgnoredLapIncrement(string gameName, StatusDataBase status, int completedLaps, int lapDelta, double trackLengthMeters)
        {
            if ((!IsRFactor2Game(gameName) && !IsLmuGame(gameName)) ||
                status == null ||
                lapDelta <= 0 ||
                trackLengthMeters <= 0.0)
            {
                return false;
            }

            double trackPositionMeters = GetTrackPositionWithinLapMeters(status, trackLengthMeters);
            if (IsLmuGame(gameName))
            {
                bool nearLineAtExit = trackPositionMeters <= Math.Max(100.0, trackLengthMeters * 0.025) ||
                    trackPositionMeters >= trackLengthMeters - 5.0;

                return completedLaps > 0 &&
                    status.SpeedKmh < 1.0 &&
                    nearLineAtExit &&
                    _lastObservedSessionMeters >= trackLengthMeters;
            }

            bool nearLine = trackPositionMeters <= 5.0 || trackPositionMeters >= trackLengthMeters - 5.0;

            return completedLaps > 0 &&
                status.SpeedKmh < 5.0 &&
                nearLine &&
                _lastObservedSessionMeters >= trackLengthMeters;
        }

        private bool ShouldIgnoreDistanceJumpForIgnoredLapIncrement(string gameName, StatusDataBase status, int completedLaps, int lapDelta, double trackLengthMeters, double deltaMeters)
        {
            if (deltaMeters <= 0.0 ||
                !LooksLikeIgnoredLapIncrement(gameName, status, completedLaps, lapDelta, trackLengthMeters) ||
                trackLengthMeters <= 0.0)
            {
                return false;
            }

            return deltaMeters >= trackLengthMeters * 0.5;
        }

        private bool ShouldIgnorePlaceholderSessionStart(string gameName, StatusDataBase status, int completedLaps)
        {
            if (!IsLmuGame(gameName) ||
                status == null ||
                completedLaps <= 0)
            {
                return false;
            }

            double trackLengthMeters = status.TrackLength > 0.0 ? status.TrackLength : status.ReportedTrackLength;
            if (trackLengthMeters <= 0.0)
            {
                return false;
            }

            double trackPositionMeters = GetTrackPositionWithinLapMeters(status, trackLengthMeters);
            bool nearLineAtExit = trackPositionMeters <= Math.Max(100.0, trackLengthMeters * 0.025) ||
                trackPositionMeters >= trackLengthMeters - 5.0;
            bool looksLikeNegativeLapBoundarySentinel = trackPositionMeters <= (-trackLengthMeters + 5.0) ||
                status.TrackPositionPercent <= -0.99;
            bool hasIgnoredSessionMarker = _lastIgnoredSessionMeters >= 0.0;
            bool looksLikeResetSessionOdo = status.SessionOdo >= 0.0 && status.SessionOdo <= 0.01;

            return status.SpeedKmh < 1.0 &&
                (nearLineAtExit || looksLikeNegativeLapBoundarySentinel) &&
                (hasIgnoredSessionMarker || looksLikeResetSessionOdo);
        }

        private bool ShouldIgnoreRepeatedIgnoredDistanceJump(double sessionMeters)
        {
            return _lastIgnoredSessionMeters >= 0.0 &&
                Math.Abs(sessionMeters - _lastIgnoredSessionMeters) <= 1.0;
        }

        private void EnsureDefaultGameDebugLoggingSettings()
        {
            if (Settings.GameDebugLogging == null)
            {
                Settings.GameDebugLogging = new Dictionary<string, bool>();
            }

            RemoveUnsupportedGameDebugLoggingSettings();

            foreach (IAffinityGameProfile profile in _gameProfiles.SupportedProfiles)
            {
                if (!Settings.GameDebugLogging.ContainsKey(profile.SettingsKey))
                {
                    Settings.GameDebugLogging[profile.SettingsKey] = false;
                }
            }
        }

        private bool EnsureGameDebugLoggingConfigured(string gameName)
        {
            string settingsKey = GetDebugLoggingSettingsKey(gameName);
            if (string.IsNullOrWhiteSpace(settingsKey))
            {
                return false;
            }

            if (Settings.GameDebugLogging == null)
            {
                Settings.GameDebugLogging = new Dictionary<string, bool>();
            }

            if (!Settings.GameDebugLogging.ContainsKey(settingsKey))
            {
                Settings.GameDebugLogging[settingsKey] = false;
                return true;
            }

            return false;
        }

        private void RefreshGameDebugLoggingOptions()
        {
            GameDebugLoggingOptions.Clear();
            foreach (IAffinityGameProfile profile in _gameProfiles.SupportedProfiles.OrderBy(item => item.DisplayName))
            {
                bool enabled = Settings.GameDebugLogging.TryGetValue(profile.SettingsKey, out bool configured) && configured;
                GameDebugLoggingOptions.Add(new GameDebugLoggingOption(
                    profile.SettingsKey,
                    profile.DisplayName,
                    enabled,
                    UpdateGameDebugLoggingSetting));
            }
        }

        private void UpdateGameDebugLoggingSetting(string settingsKey, bool isEnabled)
        {
            if (string.IsNullOrWhiteSpace(settingsKey))
            {
                return;
            }

            if (Settings.GameDebugLogging == null)
            {
                Settings.GameDebugLogging = new Dictionary<string, bool>();
            }

            Settings.GameDebugLogging[settingsKey] = isEnabled;
        }

        private void RemoveUnsupportedGameDebugLoggingSettings()
        {
            if (Settings.GameDebugLogging == null || Settings.GameDebugLogging.Count == 0)
            {
                return;
            }

            List<string> unsupportedKeys = Settings.GameDebugLogging.Keys
                .Where(settingsKey => !IsSupportedDebugLoggingSettingsKey(settingsKey))
                .ToList();

            foreach (string unsupportedKey in unsupportedKeys)
            {
                Settings.GameDebugLogging.Remove(unsupportedKey);
            }
        }

        private string GetDebugLogPath(string gameName)
        {
            if (string.IsNullOrWhiteSpace(_debugLogPath))
            {
                return string.Empty;
            }

            string directory = Path.GetDirectoryName(_debugLogPath);
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(_debugLogPath);
            string extension = Path.GetExtension(_debugLogPath);
            string settingsKey = GetDebugLoggingSettingsKey(gameName);

            if (string.IsNullOrWhiteSpace(settingsKey))
            {
                return _debugLogPath;
            }

            return Path.Combine(directory ?? string.Empty, $"{fileNameWithoutExtension}.{settingsKey}{extension}");
        }

        private string GetDebugLoggingSettingsKey(string gameName)
        {
            return _gameProfiles.Resolve(gameName).SettingsKey;
        }

        private bool IsSupportedDebugLoggingSettingsKey(string settingsKey)
        {
            return IsSupportedGame(settingsKey);
        }

        private static string NormalizeContextValue(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private string GetDisplayTrackNameWithConfig(string gameName, string rawTrackNameWithConfig)
        {
            IAffinityGameProfile profile = _gameProfiles.Resolve(gameName);
            return profile.GetTrackDisplayName(rawTrackNameWithConfig, _trackDisplayContext);
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void NotifyDistanceDisplayChanged()
        {
            OnPropertyChanged(nameof(DistanceUnitLabel));
            OnPropertyChanged(nameof(DistanceColumnHeader));
            OnPropertyChanged(nameof(CurrentContextDistanceDisplay));
            OnPropertyChanged(nameof(CurrentContextTotalDisplay));
            OnPropertyChanged(nameof(SessionDistanceDisplay));
            OnPropertyChanged(nameof(CurrentSessionDistanceDisplay));
            OnPropertyChanged(nameof(TotalDistanceDisplay));
            OnPropertyChanged(nameof(CurrentContextUsedTimeDisplay));
            OnPropertyChanged(nameof(TotalUsedTimeDisplay));
        }

        private bool AccumulateActiveSessionTime(DateTime now)
        {
            if (_lastSessionSampleUtc == DateTime.MinValue || string.IsNullOrWhiteSpace(_activeContextKey))
            {
                _lastSessionSampleUtc = now;
                return false;
            }

            double elapsedSeconds = (now - _lastSessionSampleUtc).TotalSeconds;
            _lastSessionSampleUtc = now;
            if (elapsedSeconds <= 0.0 || elapsedSeconds > MaxCountedTelemetryGapSeconds)
            {
                return false;
            }

            TrackBucket bucket = GetOrCreateTrackBucket(CurrentGameName, CurrentCarModel, CurrentTrackName, CurrentTrackNameWithConfig);
            bucket.UsedTime += elapsedSeconds;
            bucket.LastUpdatedUtc = now;
            _activeSessionUsedTimeSeconds += elapsedSeconds;
            _pendingUsedTimeSecondsSinceSave += elapsedSeconds;
            CurrentContextUsedTime = bucket.UsedTime;
            return true;
        }

        private void RefreshLiveSummariesIfNeeded(bool force)
        {
            if (!force &&
                _pendingMetersSinceSave < SaveThresholdMeters &&
                _pendingUsedTimeSecondsSinceSave < SaveThresholdUsedTimeSeconds)
            {
                return;
            }

            RefreshDistanceSummaries();
            _pendingMetersSinceSave = 0.0;
            _pendingUsedTimeSecondsSinceSave = 0.0;
        }

        private void FinalizeActiveSession(bool refreshSummaries)
        {
            if (string.IsNullOrWhiteSpace(_activeStorageSessionUid) ||
                _activeSessionStartedUtc == DateTime.MinValue ||
                _sqliteRepository == null)
            {
                return;
            }

            double sessionDistanceMeters = Math.Max(0.0, SessionDistanceKm * MetersPerKilometer);
            double sessionTimeDrivenSeconds = Math.Max(0.0, _activeSessionUsedTimeSeconds);
            if (!ShouldPersistFinalizedSession(sessionDistanceMeters, sessionTimeDrivenSeconds))
            {
                _pendingMetersSinceSave = 0.0;
                _pendingUsedTimeSecondsSinceSave = 0.0;
                return;
            }

            _sqliteRepository.UpsertSession(
                _activeStorageSessionUid,
                CurrentGameName,
                CurrentCarModel,
                CurrentTrackName,
                CurrentTrackNameWithConfig,
                _activeSessionStartedUtc,
                _lastSessionSampleUtc == DateTime.MinValue ? DateTime.UtcNow : _lastSessionSampleUtc,
                sessionDistanceMeters,
                sessionTimeDrivenSeconds);

            if (refreshSummaries)
            {
                RefreshLiveSummariesIfNeeded(force: true);
            }

            _pendingMetersSinceSave = 0.0;
            _pendingUsedTimeSecondsSinceSave = 0.0;
        }

        private static bool ShouldPersistFinalizedSession(double sessionDistanceMeters, double sessionTimeDrivenSeconds)
        {
            return sessionDistanceMeters >= MinimumPersistedSessionMeters;
        }

        private static string FormatUsedTime(double usedTimeSeconds)
        {
            TimeSpan duration = TimeSpan.FromSeconds(Math.Max(0.0, usedTimeSeconds));
            int totalHours = (int)duration.TotalHours;
            return $"{totalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
        }

        private AffinitySummarySnapshot BuildMonthlySummarySnapshot(DateTime monthLocal)
        {
            if (_sqliteRepository == null)
            {
                return new AffinitySummarySnapshot();
            }

            DateTime monthStartLocal = new DateTime(monthLocal.Year, monthLocal.Month, 1, 0, 0, 0, DateTimeKind.Local);
            DateTime monthStartUtc = monthStartLocal.ToUniversalTime();
            DateTime nextMonthStartUtc = monthStartLocal.AddMonths(1).ToUniversalTime();
            return AffinitySummaryBuilder.BuildSnapshot(
                _sqliteRepository.GetDistanceSummaries(monthStartUtc, nextMonthStartUtc),
                Settings.DisplayInMiles,
                _assettoCorsaTrackMap,
                TryResolveGameLogoPath,
                TryLoadGameLogo,
                _gameProfiles);
        }

        private void BuildRecentHighlightsSummarySnapshots(
            DateTime referenceLocal,
            out AffinitySummarySnapshot currentSnapshot,
            out AffinitySummarySnapshot previousSnapshot,
            out string currentHeader,
            out string currentDateRangeDisplay,
            out string previousHeader,
            out string previousDateRangeDisplay)
        {
            string normalizedRangeKey = NormalizeRecentHighlightsRangeKey(SelectedRecentHighlightsRangeKey);
            currentHeader = GetCurrentRecentHighlightsHeader(normalizedRangeKey);
            previousHeader = GetPreviousRecentHighlightsHeader(normalizedRangeKey);

            if (!TryGetRecentHighlightsRangeUtcRanges(
                normalizedRangeKey,
                referenceLocal,
                out DateTime? currentStartUtc,
                out DateTime? currentEndUtc,
                out DateTime? previousStartUtc,
                out DateTime? previousEndUtc))
            {
                currentSnapshot = new AffinitySummarySnapshot();
                previousSnapshot = new AffinitySummarySnapshot();
                currentDateRangeDisplay = string.Empty;
                previousDateRangeDisplay = string.Empty;
                return;
            }

            currentDateRangeDisplay = BuildDateRangeDisplay(currentStartUtc.Value.ToLocalTime(), currentEndUtc.Value.ToLocalTime());
            previousDateRangeDisplay = BuildDateRangeDisplay(previousStartUtc.Value.ToLocalTime(), previousEndUtc.Value.ToLocalTime());
            currentSnapshot = BuildRecentHighlightsSummarySnapshot(currentStartUtc.Value, currentEndUtc.Value);
            previousSnapshot = BuildRecentHighlightsSummarySnapshot(previousStartUtc.Value, previousEndUtc.Value);
        }

        private AffinitySummarySnapshot BuildRecentHighlightsSummarySnapshot(DateTime startUtc, DateTime endUtc)
        {
            IEnumerable<DistanceSummary> rows = _sqliteRepository != null
                ? _sqliteRepository.GetDistanceSummaries(startUtc, endUtc)
                : AffinitySummaryBuilder.BuildDistanceSummaries(_database)
                    .Where(summary => summary.LastUpdatedUtc >= startUtc && summary.LastUpdatedUtc < endUtc)
                    .ToList();

            return AffinitySummaryBuilder.BuildSnapshot(
                rows,
                Settings.DisplayInMiles,
                _assettoCorsaTrackMap,
                gameProfiles: _gameProfiles);
        }

        private void ApplySummarySnapshot(
            AffinitySummarySnapshot snapshot,
            AffinitySummarySnapshot thisMonthSnapshot,
            AffinitySummarySnapshot lastMonthSnapshot,
            AffinitySummarySnapshot currentRecentHighlightsSnapshot,
            AffinitySummarySnapshot previousRecentHighlightsSnapshot,
            string currentRecentHighlightsHeader,
            string currentRecentHighlightsDateRangeDisplay,
            string previousRecentHighlightsHeader,
            string previousRecentHighlightsDateRangeDisplay)
        {
            snapshot = snapshot ?? new AffinitySummarySnapshot();
            thisMonthSnapshot = thisMonthSnapshot ?? new AffinitySummarySnapshot();
            lastMonthSnapshot = lastMonthSnapshot ?? new AffinitySummarySnapshot();
            currentRecentHighlightsSnapshot = currentRecentHighlightsSnapshot ?? new AffinitySummarySnapshot();
            previousRecentHighlightsSnapshot = previousRecentHighlightsSnapshot ?? new AffinitySummarySnapshot();
            TotalDistanceKm = snapshot.TotalDistanceKm;
            TotalUsedTime = snapshot.TotalUsedTime;
            FeaturedGameTab = snapshot.FeaturedGameTab;
            FeaturedTrackSummary = snapshot.FeaturedTrackSummary;
            FeaturedCarSummary = snapshot.FeaturedCarSummary;

            OverallTopSummarySection = CreateTopSummarySection("Top Overall", snapshot);
            OnPropertyChanged(nameof(OverallTopSummarySection));

            CurrentRecentHighlightsSection = CreateTopSummarySection(
                string.IsNullOrWhiteSpace(currentRecentHighlightsHeader)
                    ? GetCurrentRecentHighlightsHeader(SelectedRecentHighlightsRangeKey)
                    : currentRecentHighlightsHeader,
                currentRecentHighlightsSnapshot);
            CurrentRecentHighlightsDateRangeDisplay = currentRecentHighlightsDateRangeDisplay ?? string.Empty;
            PreviousRecentHighlightsSection = CreateTopSummarySection(
                string.IsNullOrWhiteSpace(previousRecentHighlightsHeader)
                    ? GetPreviousRecentHighlightsHeader(SelectedRecentHighlightsRangeKey)
                    : previousRecentHighlightsHeader,
                previousRecentHighlightsSnapshot);
            PreviousRecentHighlightsDateRangeDisplay = previousRecentHighlightsDateRangeDisplay ?? string.Empty;

            MonthlyTopSummarySections.Clear();
            MonthlyTopSummarySections.Add(CreateTopSummarySection("This Month", thisMonthSnapshot));
            MonthlyTopSummarySections.Add(CreateTopSummarySection("Last Month", lastMonthSnapshot));

            TopSummarySections.Clear();
            TopSummarySections.Add(CreateTopSummarySection("Top Overall", snapshot));
            TopSummarySections.Add(CreateTopSummarySection("Top This Month", thisMonthSnapshot));
            TopSummarySections.Add(CreateTopSummarySection("Top Last Month", lastMonthSnapshot));

            object previouslySelectedTopLevelTab = SelectedTopLevelTab;
            bool canReuseTopLevelTabStructure = CanReuseTopLevelTabStructure(GameTabs, snapshot.GameTabs)
                && TopLevelTabs.Count == GameTabs.Count + 2
                && ReferenceEquals(TopLevelTabs[0], _overviewTab)
                && ReferenceEquals(TopLevelTabs[TopLevelTabs.Count - 1], _settingsTab);

            if (canReuseTopLevelTabStructure)
            {
                ReplaceGameTabsInCollections(GameTabs, TopLevelTabs, snapshot.GameTabs);
            }
            else
            {
                GameTabs.Clear();
                foreach (GameDistanceTab tab in snapshot.GameTabs)
                {
                    GameTabs.Add(tab);
                }

                RebuildTopLevelTabs();
            }

            SelectedTopLevelTab = ResolveSelectedTopLevelTab(previouslySelectedTopLevelTab, GameTabs, _overviewTab, _settingsTab);
            SelectedGameTab = SelectedTopLevelTab as GameDistanceTab;

            foreach (GameDistanceTab tab in GameTabs)
            {
                EnsureGameDebugLoggingConfigured(tab.GameName);
            }

            RefreshGameDebugLoggingOptions();
        }

        private static DateTime GetStartOfWeek(DateTime localReference)
        {
            DateTime localDate = localReference.Date;
            DayOfWeek firstDayOfWeek = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
            int offset = (7 + (int)localDate.DayOfWeek - (int)firstDayOfWeek) % 7;
            return DateTime.SpecifyKind(localDate.AddDays(-offset), DateTimeKind.Local);
        }

        private static string NormalizeRecentHighlightsRangeKey(string value)
        {
            switch (value)
            {
                case RecentHighlightsRangeWeek:
                case RecentHighlightsRangeMonth:
                    return value;
                default:
                    return RecentHighlightsRangeMonth;
            }
        }

        private static string GetRecentHighlightsRangeDisplayName(string rangeKey)
        {
            GameTabFilterOption option = RecentHighlightsRangeOptionsValue.FirstOrDefault(item =>
                string.Equals(item.Key, NormalizeRecentHighlightsRangeKey(rangeKey), StringComparison.Ordinal));
            return option?.DisplayName ?? "Month";
        }

        private static string GetCurrentRecentHighlightsHeader(string rangeKey)
        {
            return string.Equals(NormalizeRecentHighlightsRangeKey(rangeKey), RecentHighlightsRangeWeek, StringComparison.Ordinal)
                ? "This week highlights"
                : "This month highlights";
        }

        private static string GetPreviousRecentHighlightsHeader(string rangeKey)
        {
            return string.Equals(NormalizeRecentHighlightsRangeKey(rangeKey), RecentHighlightsRangeWeek, StringComparison.Ordinal)
                ? "Last week highlights"
                : "Last month highlights";
        }

        private static string BuildDateRangeDisplay(DateTime startLocal, DateTime endLocal)
        {
            DateTime displayEndLocal = endLocal.TimeOfDay == TimeSpan.Zero ? endLocal.AddDays(-1) : endLocal;
            bool showYear = startLocal.Year != displayEndLocal.Year;
            string format = showYear ? "MMM d, yyyy" : "MMM d";
            return $"{startLocal.ToString(format, CultureInfo.CurrentCulture)} - {displayEndLocal.ToString(format, CultureInfo.CurrentCulture)}";
        }

        private static AffinityTopSummarySection CreateTopSummarySection(string header, AffinitySummarySnapshot snapshot)
        {
            snapshot = snapshot ?? new AffinitySummarySnapshot();
            return new AffinityTopSummarySection
            {
                Header = header,
                FeaturedGameTab = snapshot.FeaturedGameTab,
                FeaturedTrackSummary = snapshot.FeaturedTrackSummary,
                FeaturedCarSummary = snapshot.FeaturedCarSummary
            };
        }

        internal static string TryGetGameLogoFileName(string gameName)
        {
            IAffinityGameProfile profile = DefaultGameProfiles.ResolveLogo(gameName);
            return string.IsNullOrWhiteSpace(profile.LogoFileName)
                ? null
                : profile.LogoFileName;
        }

        internal static string TryGetGameLogoPath(string logosDirectory, string gameName)
        {
            string fileName = TryGetGameLogoFileName(gameName);
            if (string.IsNullOrWhiteSpace(logosDirectory) || string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            string fullPath = Path.Combine(logosDirectory, fileName);
            return File.Exists(fullPath)
                ? fullPath
                : null;
        }

        internal string ResolveSimHubLogosDirectory()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory ?? string.Empty;
            return string.IsNullOrWhiteSpace(baseDirectory)
                ? string.Empty
                : Path.Combine(baseDirectory, "Logos");
        }

        internal string TryResolveGameLogoPath(string gameName)
        {
            return TryGetGameLogoPath(ResolveSimHubLogosDirectory(), gameName);
        }

        private ImageSource TryLoadGameLogo(string gameName)
        {
            IAffinityGameProfile profile = _gameProfiles.ResolveLogo(gameName);
            string cacheKey = string.IsNullOrWhiteSpace(profile.SettingsKey)
                ? AffinityGameName.Normalize(gameName)
                : profile.SettingsKey;
            if (_gameLogoCache.TryGetValue(cacheKey, out ImageSource cachedLogo))
            {
                return cachedLogo;
            }

            ImageSource logo = LoadBitmapFromPath(TryResolveGameLogoPath(gameName));
            _gameLogoCache[cacheKey] = logo;
            return logo;
        }

        private void RebuildTopLevelTabs()
        {
            TopLevelTabs.Clear();
            TopLevelTabs.Add(_overviewTab);
            foreach (GameDistanceTab tab in GameTabs)
            {
                TopLevelTabs.Add(tab);
            }

            TopLevelTabs.Add(_settingsTab);
        }

        internal static bool CanReuseTopLevelTabStructure(
            IReadOnlyList<GameDistanceTab> existingTabs,
            IReadOnlyList<GameDistanceTab> refreshedTabs)
        {
            if (existingTabs == null || refreshedTabs == null || existingTabs.Count != refreshedTabs.Count)
            {
                return false;
            }

            for (int i = 0; i < existingTabs.Count; i++)
            {
                if (!string.Equals(existingTabs[i]?.GameName, refreshedTabs[i]?.GameName, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        internal static void ReplaceGameTabsInCollections(
            ObservableCollection<GameDistanceTab> gameTabs,
            ObservableCollection<object> topLevelTabs,
            IReadOnlyList<GameDistanceTab> refreshedTabs)
        {
            if (gameTabs == null)
            {
                throw new ArgumentNullException(nameof(gameTabs));
            }

            if (topLevelTabs == null)
            {
                throw new ArgumentNullException(nameof(topLevelTabs));
            }

            if (refreshedTabs == null)
            {
                throw new ArgumentNullException(nameof(refreshedTabs));
            }

            if (gameTabs.Count != refreshedTabs.Count || topLevelTabs.Count != refreshedTabs.Count + 2)
            {
                throw new ArgumentException("Tab collections must already match the refreshed game-tab structure.");
            }

            for (int i = 0; i < refreshedTabs.Count; i++)
            {
                gameTabs[i] = refreshedTabs[i];
                topLevelTabs[i + 1] = refreshedTabs[i];
            }
        }

        internal static object ResolveSelectedTopLevelTab(
            object previousSelectedTopLevelTab,
            IReadOnlyList<GameDistanceTab> refreshedTabs,
            AffinityOverviewTab overviewTab,
            AffinitySettingsTab settingsTab)
        {
            if (previousSelectedTopLevelTab is AffinityOverviewTab)
            {
                return overviewTab;
            }

            if (previousSelectedTopLevelTab is AffinitySettingsTab)
            {
                return settingsTab;
            }

            if (previousSelectedTopLevelTab is GameDistanceTab previousGameTab)
            {
                GameDistanceTab matchingTab = refreshedTabs?.FirstOrDefault(tab =>
                    string.Equals(tab.GameName, previousGameTab.GameName, StringComparison.OrdinalIgnoreCase));
                if (matchingTab != null)
                {
                    return matchingTab;
                }
            }

            return overviewTab;
        }

        private static void ExecuteOnUiThread(Action action)
        {
            if (action == null)
            {
                return;
            }

            if (Application.Current?.Dispatcher == null || Application.Current.Dispatcher.CheckAccess())
            {
                action();
                return;
            }

            Application.Current.Dispatcher.BeginInvoke(action);
        }

        private static ImageSource CreatePictureIcon()
        {
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri("pack://application:,,,/Affinity;component/assets/affinity-icon-24.png", UriKind.Absolute);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }

        private static ImageSource LoadBitmapFromPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            BitmapImage image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }

        private void EnsureSqliteNativeLibraryReady()
        {
            string affinityStorageRoot = Path.GetDirectoryName(_databasePath);
            IReadOnlyList<string> probePaths = GetSqliteInteropProbePaths(
                AppDomain.CurrentDomain.BaseDirectory,
                affinityStorageRoot,
                Environment.Is64BitProcess);

            if (probePaths.Count >= 2)
            {
                try
                {
                    TryCopySqliteInteropToRecoveryPath(probePaths[0], probePaths[1]);
                }
                catch (Exception ex)
                {
                    SimHub.Logging.Current.Warn($"Affinity - Failed to cache SQLite native library for recovery: {ex.Message}");
                }
            }

            foreach (string probePath in probePaths)
            {
                if (TryLoadSqliteInteropLibrary(probePath))
                {
                    return;
                }
            }
        }

        private void HandleSqliteInitializationFailure(Exception ex)
        {
            SimHub.Logging.Current.Error(BuildSqliteInitializationFailureMessage(ex));
            DataStatus = SqliteUnavailableStatusMessage;
            IsTelemetryActive = false;
            _sqliteRepository?.Dispose();
            _sqliteRepository = null;
        }

        private bool TryLoadSqliteInteropLibrary(string interopPath)
        {
            if (GetModuleHandle(SqliteInteropFileName) != IntPtr.Zero)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(interopPath) || !File.Exists(interopPath))
            {
                return false;
            }

            IntPtr moduleHandle = LoadLibrary(interopPath);
            if (moduleHandle != IntPtr.Zero)
            {
                return true;
            }

            int win32Error = Marshal.GetLastWin32Error();
            SimHub.Logging.Current.Warn($"Affinity - Failed to load SQLite native library from {interopPath}: Win32 error {win32Error}");
            return false;
        }

        private static string ResolvePluginVersion()
        {
            Assembly assembly = typeof(AffinityPlugin).Assembly;
            AssemblyInformationalVersionAttribute informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (!string.IsNullOrWhiteSpace(informationalVersion?.InformationalVersion))
            {
                string version = informationalVersion.InformationalVersion.Trim();
                int plusIndex = version.IndexOf('+');
                return plusIndex >= 0
                    ? version.Substring(0, plusIndex)
                    : version;
            }

            return assembly.GetName().Version?.ToString() ?? "0.0.0";
        }
    }
}
