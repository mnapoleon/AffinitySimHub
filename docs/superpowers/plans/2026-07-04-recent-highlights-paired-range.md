# Recent Highlights Paired Range Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the four-choice `Recent highlights` selector with a two-choice `Week` / `Month` range selector that always shows both current and previous stacked blue-outlined highlight cards.

**Architecture:** Refactor the overview-only recent-highlights state in `AffinityPlugin` from a single selected period snapshot into a paired current/previous range model. Update the WPF overview bindings to render two stacked cards with shared blue-outline styling, while preserving existing summary-builder reuse and local-calendar range calculations.

**Tech Stack:** C#, WPF/XAML, MSTest, .NET Framework 4.8

---

### Task 1: Replace Four-Choice Tests With Paired-Range Tests

**Files:**
- Modify: `C:\Users\micha\dev\AffinitySimHub\.worktrees\recent-highlights-period-selector\Affinity.Tests\AffinityOverviewSummaryTests.cs`
- Modify: `C:\Users\micha\dev\AffinitySimHub\.worktrees\recent-highlights-period-selector\Affinity.Tests\GameTabTimePeriodRangeTests.cs`

- [x] **Step 1: Rewrite the default overview test for a two-choice range selector**

```csharp
[TestMethod]
public void NewPlugin_InitializesRecentHighlightsRangeSelectorToMonth()
{
    AffinityPlugin plugin = new AffinityPlugin();

    Assert.AreEqual(AffinityPlugin.RecentHighlightsRangeMonth, plugin.SelectedRecentHighlightsRangeKey);
    CollectionAssert.AreEqual(
        new[]
        {
            AffinityPlugin.RecentHighlightsRangeWeek,
            AffinityPlugin.RecentHighlightsRangeMonth
        },
        plugin.RecentHighlightsRangeOptions.Select(option => option.Key).ToArray());
    Assert.IsNotNull(plugin.CurrentRecentHighlightsSection);
    Assert.IsNotNull(plugin.PreviousRecentHighlightsSection);
}
```

- [x] **Step 2: Replace the single-section snapshot test with paired current/previous assertions**

```csharp
[TestMethod]
public void ApplySummarySnapshot_PopulatesCurrentAndPreviousRecentHighlightsSections()
{
    AffinityPlugin plugin = new AffinityPlugin();
    AffinitySummarySnapshot allTimeSnapshot = CreateSnapshot("All Game", "All Track", "All Car");
    AffinitySummarySnapshot currentSnapshot = CreateSnapshot("This Game", "This Track", "This Car");
    AffinitySummarySnapshot previousSnapshot = CreateSnapshot("Last Game", "Last Track", "Last Car");

    InvokeApplySummarySnapshot(
        plugin,
        allTimeSnapshot,
        currentSnapshot,
        previousSnapshot,
        currentSnapshot,
        previousSnapshot,
        "This month highlights",
        "Jul 1 - Jul 4",
        "Last month highlights",
        "Jun 1 - Jun 30");

    Assert.AreEqual("This month highlights", plugin.CurrentRecentHighlightsSection.Header);
    Assert.AreEqual("This Game", plugin.CurrentRecentHighlightsSection.FeaturedGameTab.GameName);
    Assert.AreEqual("Jul 1 - Jul 4", plugin.CurrentRecentHighlightsDateRangeDisplay);
    Assert.AreEqual("Last month highlights", plugin.PreviousRecentHighlightsSection.Header);
    Assert.AreEqual("Last Game", plugin.PreviousRecentHighlightsSection.FeaturedGameTab.GameName);
    Assert.AreEqual("Jun 1 - Jun 30", plugin.PreviousRecentHighlightsDateRangeDisplay);
}
```

- [x] **Step 3: Replace the selector-state test with week/month range behavior**

```csharp
[TestMethod]
public void SelectedRecentHighlightsRangeKey_UpdatesDisplayState()
{
    AffinityPlugin plugin = new AffinityPlugin();

    plugin.SelectedRecentHighlightsRangeKey = AffinityPlugin.RecentHighlightsRangeWeek;

    Assert.AreEqual(AffinityPlugin.RecentHighlightsRangeWeek, plugin.SelectedRecentHighlightsRangeKey);
    Assert.AreEqual("Week", plugin.SelectedRecentHighlightsRangeDisplayName);
}
```

- [x] **Step 4: Replace the single-period range tests with paired-range window tests**

```csharp
[TestMethod]
public void TryGetRecentHighlightsRangeUtcRanges_ReturnsCurrentAndPreviousWeekWindows()
{
    DateTime referenceLocal = new DateTime(2026, 7, 4, 12, 0, 0, DateTimeKind.Local);

    bool hasRange = AffinityPlugin.TryGetRecentHighlightsRangeUtcRanges(
        AffinityPlugin.RecentHighlightsRangeWeek,
        referenceLocal,
        out DateTime? currentStartUtc,
        out DateTime? currentEndUtc,
        out DateTime? previousStartUtc,
        out DateTime? previousEndUtc);

    Assert.IsTrue(hasRange);
    Assert.AreEqual(new DateTime(2026, 6, 28, 0, 0, 0, DateTimeKind.Local).ToUniversalTime(), currentStartUtc.Value);
    Assert.AreEqual(referenceLocal.ToUniversalTime(), currentEndUtc.Value);
    Assert.AreEqual(new DateTime(2026, 6, 21, 0, 0, 0, DateTimeKind.Local).ToUniversalTime(), previousStartUtc.Value);
    Assert.AreEqual(new DateTime(2026, 6, 28, 0, 0, 0, DateTimeKind.Local).ToUniversalTime(), previousEndUtc.Value);
}
```

- [x] **Step 5: Run the focused tests to verify they fail for the expected reasons**

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj --filter "AffinityOverviewSummaryTests|GameTabTimePeriodRangeTests" /p:SimHubInstallPath=C:\does-not-exist`

Expected: FAIL with missing `RecentHighlightsRange*` members, outdated `ApplySummarySnapshot(...)` parameters, and missing paired range helper logic.

- [x] **Step 6: Commit**

```bash
git add Affinity.Tests/AffinityOverviewSummaryTests.cs Affinity.Tests/GameTabTimePeriodRangeTests.cs
git commit -m "test: cover paired recent highlights ranges"
```

### Task 2: Refactor Plugin State From Single Section To Paired Range State

**Files:**
- Modify: `C:\Users\micha\dev\AffinitySimHub\.worktrees\recent-highlights-period-selector\Affinity\AffinityPlugin.cs`

- [x] **Step 1: Replace the four period constants with two range constants and options**

```csharp
public const string RecentHighlightsRangeWeek = "Week";
public const string RecentHighlightsRangeMonth = "Month";

private static readonly IReadOnlyList<GameTabFilterOption> RecentHighlightsRangeOptionsValue =
    new List<GameTabFilterOption>
    {
        new GameTabFilterOption(RecentHighlightsRangeWeek, "Week"),
        new GameTabFilterOption(RecentHighlightsRangeMonth, "Month")
    };
```

- [x] **Step 2: Replace the single selected overview state with paired current/previous state**

```csharp
private string _selectedRecentHighlightsRangeKey = RecentHighlightsRangeMonth;
private string _currentRecentHighlightsDateRangeDisplay = string.Empty;
private string _previousRecentHighlightsDateRangeDisplay = string.Empty;
private AffinityTopSummarySection _currentRecentHighlightsSection =
    new AffinityTopSummarySection { Header = "This month highlights" };
private AffinityTopSummarySection _previousRecentHighlightsSection =
    new AffinityTopSummarySection { Header = "Last month highlights" };
```

- [x] **Step 3: Add public properties for the paired range model**

```csharp
public IReadOnlyList<GameTabFilterOption> RecentHighlightsRangeOptions => RecentHighlightsRangeOptionsValue;

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
```

- [x] **Step 4: Add a paired current/previous range helper**

```csharp
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
```

- [x] **Step 5: Replace the single recent-highlights snapshot builder with a paired builder**

```csharp
private void BuildRecentHighlightsSummarySnapshots(
    DateTime referenceLocal,
    out AffinitySummarySnapshot currentSnapshot,
    out AffinitySummarySnapshot previousSnapshot,
    out string currentHeader,
    out string currentDateRangeDisplay,
    out string previousHeader,
    out string previousDateRangeDisplay)
{
    // derive range key, compute current/previous UTC windows, build both snapshots
}
```

- [x] **Step 6: Update `RefreshDistanceSummaries()` and `ApplySummarySnapshot(...)` to use the paired model**

```csharp
BuildRecentHighlightsSummarySnapshots(
    nowLocal,
    out AffinitySummarySnapshot currentRecentHighlightsSnapshot,
    out AffinitySummarySnapshot previousRecentHighlightsSnapshot,
    out string currentRecentHighlightsHeader,
    out string currentRecentHighlightsDateRangeDisplay,
    out string previousRecentHighlightsHeader,
    out string previousRecentHighlightsDateRangeDisplay);
```

- [x] **Step 7: Run the focused tests to verify the plugin-state refactor passes**

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj --filter "AffinityOverviewSummaryTests|GameTabTimePeriodRangeTests" /p:SimHubInstallPath=C:\does-not-exist`

Expected: PASS with the new range selector and paired current/previous range coverage.

- [x] **Step 8: Commit**

```bash
git add Affinity/AffinityPlugin.cs Affinity.Tests/AffinityOverviewSummaryTests.cs Affinity.Tests/GameTabTimePeriodRangeTests.cs
git commit -m "patch: pair recent highlights current and previous ranges"
```

### Task 3: Replace The Single Card With Two Stacked Blue-Outlined Cards

**Files:**
- Modify: `C:\Users\micha\dev\AffinitySimHub\.worktrees\recent-highlights-period-selector\Affinity\AffinitySimHub.xaml`
- Modify: `C:\Users\micha\dev\AffinitySimHub\.worktrees\recent-highlights-period-selector\Affinity\AffinitySimHub.xaml.cs`

- [x] **Step 1: Replace the selector label and bindings from `Period` to `Range`**

```xml
<TextBlock Margin="0,0,6,0"
           VerticalAlignment="Center"
           Foreground="#B8B8B8"
           Text="Range" />
<ComboBox Width="120"
          DisplayMemberPath="DisplayName"
          ItemsSource="{Binding DataContext.RecentHighlightsRangeOptions, ElementName=Root}"
          SelectedValue="{Binding DataContext.SelectedRecentHighlightsRangeKey, ElementName=Root, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
          SelectedValuePath="Key"
          SelectionChanged="RecentHighlightsRangeSelectionChanged" />
```

- [x] **Step 2: Replace the single recent-highlights card with a reusable stacked-card pattern**

```xml
<StackPanel Margin="0,0,0,16">
  <Border Margin="0,0,0,8"
          Padding="12,10"
          Background="#252525"
          BorderBrush="#5BAFD0"
          BorderThickness="1"
          CornerRadius="4">
    <!-- This week / This month content -->
  </Border>

  <Border Padding="12,10"
          Background="#232323"
          BorderBrush="#5BAFD0"
          BorderThickness="1"
          CornerRadius="4">
    <!-- Last week / Last month content -->
  </Border>
</StackPanel>
```

- [x] **Step 3: Bind each card independently to current/previous overview state**

```xml
<TextBlock FontSize="13"
           FontWeight="SemiBold"
           Text="{Binding DataContext.CurrentRecentHighlightsSection.Header, ElementName=Root}" />
<TextBlock Margin="0,4,0,0"
           FontSize="11"
           Foreground="#B8B8B8"
           Text="{Binding DataContext.CurrentRecentHighlightsDateRangeDisplay, ElementName=Root}" />
```

- [x] **Step 4: Add per-card empty state handling instead of collapsing the whole section**

```xml
<TextBlock Foreground="#B8B8B8"
           Text="{Binding DataContext.CurrentRecentHighlightsEmptyStateText, ElementName=Root}">
  <TextBlock.Style>
    <Style TargetType="TextBlock">
      <Setter Property="Visibility" Value="Collapsed" />
      <Style.Triggers>
        <DataTrigger Binding="{Binding DataContext.HasCurrentRecentHighlights, ElementName=Root}" Value="False">
          <Setter Property="Visibility" Value="Visible" />
        </DataTrigger>
      </Style.Triggers>
    </Style>
  </TextBlock.Style>
</TextBlock>
```

- [x] **Step 5: Rename the event handler to match the new range selector**

```csharp
private void RecentHighlightsRangeSelectionChanged(object sender, SelectionChangedEventArgs e)
{
    _plugin.ApplySelectedRecentHighlightsRange();
}
```

- [x] **Step 6: Run the focused overview tests again**

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj --filter AffinityOverviewSummaryTests /p:SimHubInstallPath=C:\does-not-exist`

Expected: PASS with the stacked current/previous card bindings in place.

- [x] **Step 7: Commit**

```bash
git add Affinity/AffinitySimHub.xaml Affinity/AffinitySimHub.xaml.cs
git commit -m "patch: stack recent highlights comparison cards"
```

### Task 4: Verify Full Regression Coverage And Build Outputs

**Files:**
- Modify: `C:\Users\micha\dev\AffinitySimHub\.worktrees\recent-highlights-period-selector\docs/superpowers/plans/2026-07-04-recent-highlights-paired-range.md`

- [x] **Step 1: Run the full test suite**

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist`

Expected: PASS with all tests green.

- [x] **Step 2: Run the clean no-copy plugin build**

Run: `dotnet build .\Affinity\Affinity.csproj /p:SimHubInstallPath=C:\does-not-exist`

Expected: PASS and produce the updated plugin output.

- [x] **Step 3: Run the normal plugin build to copy into SimHub if the install is not locked**

Run: `dotnet build .\Affinity\Affinity.csproj`

Expected: PASS and copy the plugin output into `C:\Program Files (x86)\SimHub\`, unless SimHub is open and locking the files.

- [x] **Step 4: Review final git status**

Run: `git status --short`

Expected: only intended paired-range follow-up files changed.

- [x] **Step 5: Commit the follow-up implementation**

```bash
git add Affinity/AffinityPlugin.cs Affinity/AffinitySimHub.xaml Affinity/AffinitySimHub.xaml.cs Affinity.Tests/AffinityOverviewSummaryTests.cs Affinity.Tests/GameTabTimePeriodRangeTests.cs docs/superpowers/plans/2026-07-04-recent-highlights-paired-range.md
git commit -m "patch: compare recent highlights by week or month"
```
