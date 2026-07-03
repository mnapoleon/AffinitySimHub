# Affinity UX Refresh Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Improve the Affinity SimHub plugin UX by making live tracking state obvious, reducing overview visual weight, improving game-tab discovery/search, clarifying settings, and ensuring debug logging is off by default.

**Architecture:** Keep the UI inside SimHub's WPF framework using the existing `IWPFSettingsV2`, `styles:SHTabControl`, `styles:SHSection`, `DataGrid`, `ComboBox`, `TextBox`, `CheckBox`, and `Button` patterns. Add small view-model properties to `AffinityPlugin` and `GameDistanceTab` so XAML stays declarative and runtime telemetry paths remain lightweight.

**Tech Stack:** .NET Framework 4.8, WPF XAML, MSTest, SimHub plugin SDK/stubs, Newtonsoft.Json.

---

## File Structure

- Modify `Affinity/AffinitySettings.cs`
  - Owns persisted settings defaults and reset behavior.
- Modify `Affinity/AffinityPlugin.cs`
  - Owns top-level UI view-model state, live status display values, summary-section grouping, settings save feedback, and debug logging option defaults.
- Modify `Affinity/AffinityDatabase.cs`
  - Owns `GameDistanceTab` filtering, active filter descriptions, track/car search, and empty-state flags.
- Modify `Affinity/AffinitySimHub.xaml`
  - Owns the SimHub-hosted layout: live status strip, overview hierarchy, game-tab search/filter presentation, empty states, and settings copy/control state.
- Modify `Affinity/AffinitySimHub.xaml.cs`
  - Owns lightweight UI event handlers only if a settings save status needs to be cleared on focus or debug logging changes need immediate UI refresh.
- Modify `Affinity.Tests/AffinitySettingsTests.cs`
  - Tests debug logging defaults and reset behavior.
- Modify `Affinity.Tests/GameDistanceTabFilterTests.cs`
  - Tests track/car search and active filter descriptions.
- Modify `Affinity.Tests/AffinitySummaryBuilderTests.cs`
  - Add no summary logic unless implementation reveals a regression; otherwise leave untouched.
- Modify `README.md`
  - Update UI overview wording so docs match the refreshed UI.
- Later replace `site/assets/screenshots/affinity_tab.png`, `site/assets/screenshots/game_tab.png`, and `site/assets/screenshots/settings-tab-v2.png` after the plugin is visually verified in SimHub.

---

### Task 1: Make Debug Logging Off By Default

**Files:**
- Modify: `Affinity/AffinityPlugin.cs:1681-1750`
- Modify: `Affinity.Tests/AffinitySettingsTests.cs:1-37`

- [ ] **Step 1: Write failing tests for new debug logging defaults**

Add these tests to `Affinity.Tests/AffinitySettingsTests.cs`:

```csharp
[TestMethod]
public void NewSettings_DisablesDebugLoggingByDefault()
{
    AffinitySettings settings = new AffinitySettings();

    Assert.IsFalse(settings.EnableDebugLogging);
    Assert.IsNotNull(settings.GameDebugLogging);
    Assert.AreEqual(0, settings.GameDebugLogging.Count);
}

[TestMethod]
public void EnsureDefaultGameDebugLoggingSettings_AddsSupportedGamesDisabled()
{
    AffinityPlugin plugin = new AffinityPlugin();
    MethodInfo method = typeof(AffinityPlugin).GetMethod(
        "EnsureDefaultGameDebugLoggingSettings",
        BindingFlags.Instance | BindingFlags.NonPublic);

    method.Invoke(plugin, null);

    Assert.IsFalse(plugin.Settings.EnableDebugLogging);
    Assert.IsTrue(plugin.Settings.GameDebugLogging.ContainsKey("assettocorsa"));
    Assert.IsTrue(plugin.Settings.GameDebugLogging.ContainsKey("iracing"));
    Assert.IsTrue(plugin.Settings.GameDebugLogging.ContainsKey("lmu"));
    Assert.IsFalse(plugin.Settings.GameDebugLogging["assettocorsa"]);
    Assert.IsFalse(plugin.Settings.GameDebugLogging["iracing"]);
    Assert.IsFalse(plugin.Settings.GameDebugLogging["lmu"]);
}
```

- [ ] **Step 2: Run the settings tests and verify they fail**

Run:

```powershell
dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist --filter AffinitySettingsTests
```

Expected: `EnsureDefaultGameDebugLoggingSettings_AddsSupportedGamesDisabled` fails because new per-game debug entries are currently created as `true`.

- [ ] **Step 3: Change debug logging option defaults to false**

In `Affinity/AffinityPlugin.cs`, change `EnsureDefaultGameDebugLoggingSettings()` so missing entries are disabled:

```csharp
private void EnsureDefaultGameDebugLoggingSettings()
{
    if (Settings.GameDebugLogging == null)
    {
        Settings.GameDebugLogging = new Dictionary<string, bool>();
    }

    RemoveUnsupportedGameDebugLoggingSettings();

    foreach (KeyValuePair<string, string> entry in DefaultGameDebugLoggingEntries)
    {
        if (!Settings.GameDebugLogging.ContainsKey(entry.Key))
        {
            Settings.GameDebugLogging[entry.Key] = false;
        }
    }
}
```

Change `EnsureGameDebugLoggingConfigured(string gameName)` so newly discovered supported games are disabled:

```csharp
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
```

Change `RefreshGameDebugLoggingOptions()` so missing entries render unchecked:

```csharp
foreach (KeyValuePair<string, string> entry in options)
{
    bool isEnabled = Settings.GameDebugLogging.TryGetValue(entry.Key, out bool configuredEnabled)
        && configuredEnabled;
    GameDebugLoggingOptions.Add(new GameDebugLoggingOption(entry.Key, entry.Value, isEnabled, UpdateGameDebugLoggingSetting));
}
```

- [ ] **Step 4: Run tests and verify they pass**

Run:

```powershell
dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist --filter AffinitySettingsTests
```

Expected: all `AffinitySettingsTests` pass.

- [ ] **Step 5: Commit debug default change**

```powershell
git add -- Affinity/AffinityPlugin.cs Affinity.Tests/AffinitySettingsTests.cs
git commit -m "patch: disable debug logging by default"
```

---

### Task 2: Add Live Status Strip View-Model Properties

**Files:**
- Modify: `Affinity/AffinityPlugin.cs:183-205`
- Test: existing build and UI binding validation

- [ ] **Step 1: Add lightweight display properties**

In `Affinity/AffinityPlugin.cs`, replace the current unit label property and add live status labels:

```csharp
public string DistanceUnitLabel => Settings.DisplayInMiles ? "mi" : "km";

public string DistanceColumnHeader => Settings.DisplayInMiles ? "Distance (mi)" : "Distance (km)";

public string LiveStatusLabel => IsTelemetryActive ? "Tracking" : "Standby";

public string CurrentSessionDistanceDisplay => $"{SessionDistanceDisplay:F2} {DistanceUnitLabel}";

public string CurrentContextTotalDisplay => $"{CurrentContextDistanceDisplay:F2} {DistanceUnitLabel}";
```

- [ ] **Step 2: Update property notifications**

In `NotifyDistanceDisplayChanged()`, add notifications for the new derived display properties:

```csharp
private void NotifyDistanceDisplayChanged()
{
    OnPropertyChanged(nameof(DistanceUnitLabel));
    OnPropertyChanged(nameof(DistanceColumnHeader));
    OnPropertyChanged(nameof(CurrentContextDistanceDisplay));
    OnPropertyChanged(nameof(SessionDistanceDisplay));
    OnPropertyChanged(nameof(TotalDistanceDisplay));
    OnPropertyChanged(nameof(CurrentContextUsedTimeDisplay));
    OnPropertyChanged(nameof(TotalUsedTimeDisplay));
    OnPropertyChanged(nameof(CurrentSessionDistanceDisplay));
    OnPropertyChanged(nameof(CurrentContextTotalDisplay));
}
```

In the `SessionDistanceKm` setter, add:

```csharp
OnPropertyChanged(nameof(CurrentSessionDistanceDisplay));
```

In the `CurrentContextDistanceKm` setter, add:

```csharp
OnPropertyChanged(nameof(CurrentContextTotalDisplay));
```

In the `IsTelemetryActive` setter, add:

```csharp
OnPropertyChanged(nameof(LiveStatusLabel));
```

- [ ] **Step 3: Build to verify bindings compile**

Run:

```powershell
dotnet build .\Affinity\Affinity.csproj /p:SimHubInstallPath=C:\does-not-exist
```

Expected: build succeeds with no new warnings from Affinity changes.

- [ ] **Step 4: Commit live status view-model properties**

```powershell
git add -- Affinity/AffinityPlugin.cs
git commit -m "patch: add live status display properties"
```

---

### Task 3: Rework Overview Into Live Status Plus Clearer Summary Hierarchy

**Files:**
- Modify: `Affinity/AffinityPlugin.cs:130-135`
- Modify: `Affinity/AffinityPlugin.cs:1981-2035`
- Modify: `Affinity/AffinityDatabase.cs:689-710`
- Modify: `Affinity/AffinitySimHub.xaml:23-260`
- Test: `dotnet build`

- [ ] **Step 1: Add explicit overview summary properties**

In `Affinity/AffinityPlugin.cs`, add these properties near `TopSummarySections`:

```csharp
private AffinityTopSummarySection _overallTopSummarySection = new AffinityTopSummarySection { Header = "Top Overall" };

public AffinityTopSummarySection OverallTopSummarySection
{
    get => _overallTopSummarySection;
    private set
    {
        if (ReferenceEquals(_overallTopSummarySection, value))
        {
            return;
        }

        _overallTopSummarySection = value;
        OnPropertyChanged();
    }
}

public ObservableCollection<AffinityTopSummarySection> MonthlyTopSummarySections { get; } = new ObservableCollection<AffinityTopSummarySection>();
```

- [ ] **Step 2: Populate overall and monthly sections during refresh**

In `ApplySummarySnapshot(...)`, replace the `TopSummarySections.Clear()` block with:

```csharp
AffinityTopSummarySection overallSection = CreateTopSummarySection("Top Overall", snapshot);
AffinityTopSummarySection thisMonthSection = CreateTopSummarySection("This Month", thisMonthSnapshot);
AffinityTopSummarySection lastMonthSection = CreateTopSummarySection("Last Month", lastMonthSnapshot);

OverallTopSummarySection = overallSection;

MonthlyTopSummarySections.Clear();
MonthlyTopSummarySections.Add(thisMonthSection);
MonthlyTopSummarySections.Add(lastMonthSection);

TopSummarySections.Clear();
TopSummarySections.Add(overallSection);
TopSummarySections.Add(thisMonthSection);
TopSummarySections.Add(lastMonthSection);
```

- [ ] **Step 3: Add empty-state text to summary sections**

In `Affinity/AffinityDatabase.cs`, update `AffinityTopSummarySection`:

```csharp
public class AffinityTopSummarySection
{
    public string Header { get; set; } = string.Empty;

    public GameDistanceTab FeaturedGameTab { get; set; }

    public TrackDistanceSummary FeaturedTrackSummary { get; set; }

    public CarDistanceSummary FeaturedCarSummary { get; set; }

    public string EmptyStateText => "No driving history yet";

    public bool HasFeaturedGame => FeaturedGameTab != null;

    public bool HasFeaturedTrack => FeaturedTrackSummary != null;

    public bool HasFeaturedCar => FeaturedCarSummary != null;
}
```

- [ ] **Step 4: Replace overview XAML content**

In `Affinity/AffinitySimHub.xaml`, replace the `DataTemplate DataType="{x:Type local:AffinityOverviewTab}"` content with a `StackPanel` containing this structure:

```xml
<StackPanel>
    <styles:SHSection>
        <StackPanel Margin="12">
            <Border Padding="12"
                    Background="#252525"
                    BorderBrush="{Binding DataContext.StatusSectionForeground, ElementName=Root}"
                    BorderThickness="1"
                    CornerRadius="4">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="140" />
                        <ColumnDefinition Width="*" />
                        <ColumnDefinition Width="150" />
                        <ColumnDefinition Width="150" />
                    </Grid.ColumnDefinitions>
                    <StackPanel Grid.Column="0">
                        <TextBlock FontSize="11" Foreground="#B8B8B8" Text="Status" />
                        <TextBlock FontSize="18"
                                   FontWeight="Bold"
                                   Text="{Binding DataContext.LiveStatusLabel, ElementName=Root}" />
                    </StackPanel>
                    <StackPanel Grid.Column="1" Margin="12,0,0,0">
                        <TextBlock FontSize="11" Foreground="#B8B8B8" Text="Current context" />
                        <TextBlock FontSize="14"
                                   FontWeight="SemiBold"
                                   Text="{Binding DataContext.CurrentContext, ElementName=Root}" />
                        <TextBlock Margin="0,3,0,0"
                                   Foreground="#B8B8B8"
                                   Text="{Binding DataContext.DataStatus, ElementName=Root}" />
                    </StackPanel>
                    <StackPanel Grid.Column="2" Margin="12,0,0,0">
                        <TextBlock FontSize="11" Foreground="#B8B8B8" Text="Session" />
                        <TextBlock FontSize="18"
                                   FontWeight="Bold"
                                   Text="{Binding DataContext.CurrentSessionDistanceDisplay, ElementName=Root}" />
                    </StackPanel>
                    <StackPanel Grid.Column="3" Margin="12,0,0,0">
                        <TextBlock FontSize="11" Foreground="#B8B8B8" Text="Context total" />
                        <TextBlock FontSize="18"
                                   FontWeight="Bold"
                                   Text="{Binding DataContext.CurrentContextTotalDisplay, ElementName=Root}" />
                    </StackPanel>
                </Grid>
            </Border>

            <TextBlock Margin="0,18,0,8"
                       FontSize="14"
                       FontWeight="SemiBold"
                       Text="Top Overall" />

            <!-- Keep the existing three featured summary card layout here, but bind it to DataContext.OverallTopSummarySection instead of each item in TopSummarySections. -->

            <TextBlock Margin="0,18,0,8"
                       FontSize="14"
                       FontWeight="SemiBold"
                       Text="Recent highlights" />

            <ItemsControl ItemsSource="{Binding DataContext.MonthlyTopSummarySections, ElementName=Root}">
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <Border Margin="0,0,0,8"
                                Padding="10,8"
                                Background="#252525"
                                BorderBrush="#3A3A3A"
                                BorderThickness="1"
                                CornerRadius="4">
                            <Grid>
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="110" />
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="*" />
                                </Grid.ColumnDefinitions>
                                <TextBlock Grid.Column="0"
                                           FontWeight="SemiBold"
                                           Text="{Binding Header}" />
                                <TextBlock Grid.Column="1"
                                           Text="{Binding FeaturedGameTab.GameName, StringFormat=Game: {0}, FallbackValue=Game: none, TargetNullValue=Game: none}" />
                                <TextBlock Grid.Column="2"
                                           Text="{Binding FeaturedTrackSummary.TrackDisplayName, StringFormat=Track: {0}, FallbackValue=Track: none, TargetNullValue=Track: none}" />
                                <TextBlock Grid.Column="3"
                                           Text="{Binding FeaturedCarSummary.CarModel, StringFormat=Car: {0}, FallbackValue=Car: none, TargetNullValue=Car: none}" />
                            </Grid>
                        </Border>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>

            <TextBlock Margin="0,18,0,8"
                       FontSize="14"
                       FontWeight="SemiBold"
                       Text="Game totals" />

            <!-- Keep the existing game totals WrapPanel here. Keep the Refresh button below it. -->
        </StackPanel>
    </styles:SHSection>
</StackPanel>
```

When doing the XAML edit, copy the existing three-card markup from `Affinity/AffinitySimHub.xaml:37-163` and change bindings from `FeaturedGameTab`, `FeaturedTrackSummary`, and `FeaturedCarSummary` to `DataContext.OverallTopSummarySection.FeaturedGameTab`, `DataContext.OverallTopSummarySection.FeaturedTrackSummary`, and `DataContext.OverallTopSummarySection.FeaturedCarSummary`.

- [ ] **Step 5: Build to verify XAML compiles**

Run:

```powershell
dotnet build .\Affinity\Affinity.csproj /p:SimHubInstallPath=C:\does-not-exist
```

Expected: build succeeds.

- [ ] **Step 6: Commit overview hierarchy refresh**

```powershell
git add -- Affinity/AffinityPlugin.cs Affinity/AffinityDatabase.cs Affinity/AffinitySimHub.xaml
git commit -m "patch: improve Affinity overview hierarchy"
```

---

### Task 4: Add Track And Car Search To Game Tabs

**Files:**
- Modify: `Affinity/AffinityDatabase.cs:158-681`
- Modify: `Affinity/AffinitySimHub.xaml:352-458`
- Modify: `Affinity.Tests/GameDistanceTabFilterTests.cs:1-118`

- [ ] **Step 1: Write failing tests for track and car search**

Add these tests to `Affinity.Tests/GameDistanceTabFilterTests.cs`:

```csharp
[TestMethod]
public void TrackSearchText_FiltersVisibleTracksAndUpdatesDescription()
{
    GameDistanceTab tab = BuildSnapshot().GameTabs.Single();

    tab.TrackSearchText = "monza";

    Assert.AreEqual("Track search: monza", tab.ActiveFilterDescription);
    Assert.IsTrue(tab.HasActiveFilter);
    Assert.AreEqual(1, tab.VisibleTrackSummaries.Count);
    Assert.AreEqual("Monza GP", tab.VisibleTrackSummaries[0].TrackDisplayName);
    Assert.AreEqual(3, tab.VisibleCarSummaries.Count);
}

[TestMethod]
public void CarSearchText_FiltersVisibleCarsAndUpdatesDescription()
{
    GameDistanceTab tab = BuildSnapshot().GameTabs.Single();

    tab.CarSearchText = "ferrari";

    Assert.AreEqual("Car search: ferrari", tab.ActiveFilterDescription);
    Assert.IsTrue(tab.HasActiveFilter);
    Assert.AreEqual(3, tab.VisibleTrackSummaries.Count);
    Assert.AreEqual(1, tab.VisibleCarSummaries.Count);
    Assert.AreEqual("Ferrari 488 GT3", tab.VisibleCarSummaries[0].CarModel);
}

[TestMethod]
public void ClearFilter_ClearsTrackAndCarSearchText()
{
    GameDistanceTab tab = BuildSnapshot().GameTabs.Single();
    tab.TrackSearchText = "monza";
    tab.CarSearchText = "ferrari";

    tab.ClearFilter();

    Assert.AreEqual(string.Empty, tab.TrackSearchText);
    Assert.AreEqual(string.Empty, tab.CarSearchText);
    Assert.AreEqual("No filter", tab.ActiveFilterDescription);
    Assert.IsFalse(tab.HasActiveFilter);
}
```

- [ ] **Step 2: Run tests and verify they fail**

Run:

```powershell
dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist --filter GameDistanceTabFilterTests
```

Expected: build or tests fail because `TrackSearchText` and `CarSearchText` do not exist yet.

- [ ] **Step 3: Add search properties and empty-state flags**

In `Affinity/AffinityDatabase.cs`, add fields near the other `GameDistanceTab` private fields:

```csharp
private string _trackSearchText = string.Empty;
private string _carSearchText = string.Empty;
```

Add properties to `GameDistanceTab`:

```csharp
public string TrackSearchText
{
    get => _trackSearchText;
    set
    {
        string normalizedValue = value ?? string.Empty;
        if (string.Equals(_trackSearchText, normalizedValue, StringComparison.Ordinal))
        {
            return;
        }

        _trackSearchText = normalizedValue;
        OnPropertyChanged();
        ApplyActiveFilters();
    }
}

public string CarSearchText
{
    get => _carSearchText;
    set
    {
        string normalizedValue = value ?? string.Empty;
        if (string.Equals(_carSearchText, normalizedValue, StringComparison.Ordinal))
        {
            return;
        }

        _carSearchText = normalizedValue;
        OnPropertyChanged();
        ApplyActiveFilters();
    }
}

public bool HasVisibleTrackSummaries => VisibleTrackSummaries.Count > 0;

public bool HasVisibleCarSummaries => VisibleCarSummaries.Count > 0;

public string TrackEmptyStateText => HasActiveFilter ? "No tracks match the current filter" : "No track history yet";

public string CarEmptyStateText => HasActiveFilter ? "No cars match the current filter" : "No car history yet";
```

In the `VisibleTrackSummaries` setter, add:

```csharp
OnPropertyChanged(nameof(HasVisibleTrackSummaries));
OnPropertyChanged(nameof(TrackEmptyStateText));
```

In the `VisibleCarSummaries` setter, add:

```csharp
OnPropertyChanged(nameof(HasVisibleCarSummaries));
OnPropertyChanged(nameof(CarEmptyStateText));
```

- [ ] **Step 4: Apply search in active filtering**

Add helper methods to `GameDistanceTab`:

```csharp
private IEnumerable<TrackDistanceSummary> ApplyTrackSearch(IEnumerable<TrackDistanceSummary> summaries)
{
    string search = TrackSearchText?.Trim();
    if (string.IsNullOrWhiteSpace(search))
    {
        return summaries;
    }

    return summaries.Where(summary =>
        (summary.TrackDisplayName ?? string.Empty).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
}

private IEnumerable<CarDistanceSummary> ApplyCarSearch(IEnumerable<CarDistanceSummary> summaries)
{
    string search = CarSearchText?.Trim();
    if (string.IsNullOrWhiteSpace(search))
    {
        return summaries;
    }

    return summaries.Where(summary =>
        (summary.CarModel ?? string.Empty).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
}
```

In `ApplyActiveFilters()`, wrap the final visible lists:

```csharp
VisibleTrackSummaries = ApplyTrackSearch(trackSummaries).ToList();
VisibleCarSummaries = ApplyCarSearch(carSummaries).ToList();
```

For the selected-track branch, use:

```csharp
VisibleTrackSummaries = ApplyTrackSearch(trackSummaries).ToList();
VisibleCarSummaries = ApplyCarSearch(ApplyResultLimit(SortCarSummaries(
    AffinitySummaryBuilder.BuildCarSummaries(filteredRows, DisplayInMiles)))).ToList();
```

For the selected-car branch, use:

```csharp
VisibleCarSummaries = ApplyCarSearch(carSummaries).ToList();
VisibleTrackSummaries = ApplyTrackSearch(ApplyResultLimit(SortTrackSummaries(
    AffinitySummaryBuilder.BuildTrackSummaries(filteredRows, DisplayInMiles)))).ToList();
```

- [ ] **Step 5: Include search in active filter description and clear behavior**

In `ClearFilter()`, clear search fields while `_isUpdatingFilterState` is true:

```csharp
_trackSearchText = string.Empty;
_carSearchText = string.Empty;
OnPropertyChanged(nameof(TrackSearchText));
OnPropertyChanged(nameof(CarSearchText));
```

In `HasActiveFilter`, include search:

```csharp
public bool HasActiveFilter =>
    _selectedTrackSummary != null
    || _selectedCarSummary != null
    || !string.Equals(SelectedTimePeriodFilterKey, TimePeriodAllTime, StringComparison.Ordinal)
    || !string.Equals(SelectedSortModeKey, SortByDistance, StringComparison.Ordinal)
    || !string.Equals(SelectedResultLimitKey, ResultLimitAll, StringComparison.Ordinal)
    || !string.IsNullOrWhiteSpace(TrackSearchText)
    || !string.IsNullOrWhiteSpace(CarSearchText);
```

In `UpdateActiveFilterDescription()`, add:

```csharp
if (!string.IsNullOrWhiteSpace(TrackSearchText))
{
    descriptions.Add($"Track search: {TrackSearchText.Trim()}");
}

if (!string.IsNullOrWhiteSpace(CarSearchText))
{
    descriptions.Add($"Car search: {CarSearchText.Trim()}");
}
```

- [ ] **Step 6: Add search boxes and empty states to game tab XAML**

In `Affinity/AffinitySimHub.xaml`, add a `TextBox` above each `DataGrid`:

```xml
<TextBox Grid.Row="4"
         Grid.Column="0"
         Margin="70,0,0,8"
         Height="24"
         Text="{Binding TrackSearchText, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />

<TextBox Grid.Row="4"
         Grid.Column="2"
         Margin="50,0,0,8"
         Height="24"
         Text="{Binding CarSearchText, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
```

Add empty-state `TextBlock` controls above each grid:

```xml
<TextBlock Grid.Row="5"
           Grid.Column="0"
           Margin="8"
           Foreground="#B8B8B8"
           Text="{Binding TrackEmptyStateText}">
    <TextBlock.Style>
        <Style TargetType="TextBlock">
            <Setter Property="Visibility" Value="Collapsed" />
            <Style.Triggers>
                <DataTrigger Binding="{Binding HasVisibleTrackSummaries}" Value="False">
                    <Setter Property="Visibility" Value="Visible" />
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </TextBlock.Style>
</TextBlock>

<TextBlock Grid.Row="5"
           Grid.Column="2"
           Margin="8"
           Foreground="#B8B8B8"
           Text="{Binding CarEmptyStateText}">
    <TextBlock.Style>
        <Style TargetType="TextBlock">
            <Setter Property="Visibility" Value="Collapsed" />
            <Style.Triggers>
                <DataTrigger Binding="{Binding HasVisibleCarSummaries}" Value="False">
                    <Setter Property="Visibility" Value="Visible" />
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </TextBlock.Style>
</TextBlock>
```

- [ ] **Step 7: Run search tests**

Run:

```powershell
dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist --filter GameDistanceTabFilterTests
```

Expected: all `GameDistanceTabFilterTests` pass.

- [ ] **Step 8: Commit game-tab search**

```powershell
git add -- Affinity/AffinityDatabase.cs Affinity/AffinitySimHub.xaml Affinity.Tests/GameDistanceTabFilterTests.cs
git commit -m "patch: add game tab search filters"
```

---

### Task 5: Clarify Settings And Debug Logging Controls

**Files:**
- Modify: `Affinity/AffinityPlugin.cs:270-283`
- Modify: `Affinity/AffinityPlugin.cs:906-923`
- Modify: `Affinity/AffinitySimHub.xaml:462-540`
- Test: `dotnet build`

- [ ] **Step 1: Add settings save feedback**

In `Affinity/AffinityPlugin.cs`, add a field near `_dataStatus`:

```csharp
private string _settingsStatus = "Settings not saved in this session";
```

Add a property:

```csharp
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
```

Update `SaveSettings()`:

```csharp
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
        SettingsStatus = $"Settings saved at {DateTime.Now:h:mm tt}";
    }
    catch (Exception ex)
    {
        SettingsStatus = "Settings save failed; see SimHub log";
        SimHub.Logging.Current.Error($"Affinity - Failed to save settings: {ex.Message}");
    }
}
```

- [ ] **Step 2: Rename reset action and show storage path in Settings**

In `Affinity/AffinitySimHub.xaml`, change:

```xml
<Button Width="110" Margin="8,0,0,0" Click="ResetButton_Click" Content="Reset Settings" />
```

to:

```xml
<Button Width="120" Margin="8,0,0,0" Click="ResetButton_Click" Content="Restore Defaults" />
```

Add this below the General buttons:

```xml
<TextBlock Margin="0,8,0,0"
           Foreground="#B8B8B8"
           Text="{Binding DataContext.SettingsStatus, ElementName=Root}" />
<TextBlock Margin="0,4,0,0"
           Foreground="#B8B8B8"
           Text="{Binding DataContext.DatabasePath, ElementName=Root}"
           TextWrapping="Wrap" />
```

- [ ] **Step 3: Disable per-game logging options when master logging is off**

Wrap the debug logging `ItemsControl` in XAML with an `IsEnabled` binding:

```xml
<ItemsControl ItemsSource="{Binding DataContext.GameDebugLoggingOptions, ElementName=Root}"
              IsEnabled="{Binding DataContext.Settings.EnableDebugLogging, ElementName=Root}">
```

Change the debug logging help text to:

```xml
<TextBlock Margin="0,0,0,8" TextWrapping="Wrap">
    Enable telemetry debug logging only while investigating tracking issues. Per-game logging options are available when debug logging is enabled.
</TextBlock>
```

- [ ] **Step 4: Build to verify XAML compiles**

Run:

```powershell
dotnet build .\Affinity\Affinity.csproj /p:SimHubInstallPath=C:\does-not-exist
```

Expected: build succeeds.

- [ ] **Step 5: Commit settings clarity**

```powershell
git add -- Affinity/AffinityPlugin.cs Affinity/AffinitySimHub.xaml
git commit -m "patch: clarify Affinity settings controls"
```

---

### Task 6: Update Docs And Validate The Full Plugin

**Files:**
- Modify: `README.md:264-283`
- Optionally replace after SimHub visual QA: `site/assets/screenshots/affinity_tab.png`
- Optionally replace after SimHub visual QA: `site/assets/screenshots/game_tab.png`
- Optionally replace after SimHub visual QA: `site/assets/screenshots/settings-tab-v2.png`

- [ ] **Step 1: Update README UI overview**

Replace the UI overview bullets in `README.md` with:

```markdown
The Data tab is effectively the reporting surface for the plugin:

- live tracking status with current game/car/track context
- current-session and current-context distance totals
- featured all-time summary cards across all games
- compact monthly highlights
- per-game tabs for track/car detail
- current-tab track/car cross-filtering
- current-tab search, period, sort order, and result limit controls
- all-games totals footer

The Settings tab controls:

- distance units
- settings save/reset behavior
- storage path visibility
- debug logging master switch
- per-game debug logging switches, disabled unless debug logging is enabled
```

- [ ] **Step 2: Run full tests**

Run:

```powershell
dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist
```

Expected: all tests pass.

- [ ] **Step 3: Run clean plugin build**

Run:

```powershell
dotnet build .\Affinity\Affinity.csproj /p:SimHubInstallPath=C:\does-not-exist
```

Expected: build succeeds.

- [ ] **Step 4: Build and copy into SimHub when available**

Run:

```powershell
dotnet build .\Affinity\Affinity.csproj
```

Expected when SimHub is closed and installed at `C:\Program Files (x86)\SimHub\`: `Affinity.dll`, `Affinity.pdb`, and `ac_track_id_map.json` copy into the SimHub install. If the DLL is locked, stop and ask the user to close or restart SimHub before retrying.

- [ ] **Step 5: Perform visual QA in SimHub**

Open SimHub and verify:

- Overview first viewport shows the live status strip before summary cards.
- Tracking/standby state changes color with `StatusSectionForeground`.
- All-time cards remain readable with long car and track names.
- This Month and Last Month render as compact rows.
- Game tabs show Period, Sort, Limit, Track search, Car search, active filter text, and Clear filter.
- Selecting a track filters cars.
- Selecting a car filters tracks.
- Track and car search filter independently.
- Empty states render when search has no matches.
- Settings shows `Restore Defaults`, save status, database path, debug master switch, and disabled per-game logging options when debug logging is off.
- Fresh settings show debug logging disabled and all per-game logging options unchecked.

- [ ] **Step 6: Replace screenshots after visual QA**

Capture and replace:

```text
site/assets/screenshots/affinity_tab.png
site/assets/screenshots/game_tab.png
site/assets/screenshots/settings-tab-v2.png
```

Use screenshots from the refreshed plugin after Step 5 passes so the project site matches the shipped UI.

- [ ] **Step 7: Commit docs and verification artifacts**

```powershell
git add -- README.md site/assets/screenshots/affinity_tab.png site/assets/screenshots/game_tab.png site/assets/screenshots/settings-tab-v2.png
git commit -m "patch: update Affinity UX documentation"
```

---

## Self-Review

**Spec coverage:** The plan covers the live status strip, overview hierarchy reduction, game-tab filter discoverability/search, empty states, settings clarity, docs/screenshots, and debug logging off by default.

**Placeholder scan:** The plan contains no `TBD`, no incomplete code references, and no unspecified test commands. The only optional screenshot replacements are explicitly gated on SimHub visual QA because those files require runtime capture.

**Type consistency:** New members are consistently named `LiveStatusLabel`, `CurrentSessionDistanceDisplay`, `CurrentContextTotalDisplay`, `OverallTopSummarySection`, `MonthlyTopSummarySections`, `TrackSearchText`, `CarSearchText`, `HasVisibleTrackSummaries`, `HasVisibleCarSummaries`, `TrackEmptyStateText`, `CarEmptyStateText`, and `SettingsStatus`.

---

## Follow-Up Notes

- Revisit the Overview after the initial UX refresh: make the `Game totals` section cleaner and make `Recent highlights` configurable, likely with a SimHub-friendly period selector that can switch between monthly and weekly highlight ranges.
