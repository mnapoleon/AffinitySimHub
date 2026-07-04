# Recent Highlights Period Selector Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a non-persisted overview `Recent highlights` period selector that switches between `This week`, `Last week`, `This month`, and `Last month`.

**Architecture:** Extend `AffinityPlugin` with an overview-only selected-period state, date-range helpers, and a single active recent-highlights section built from filtered summary rows. Replace the current fixed monthly `ItemsControl` with a dropdown-driven single-row view in the overview XAML, while reusing the existing summary builder and local-calendar range behavior.

**Tech Stack:** C#, WPF/XAML, MSTest, .NET Framework 4.8

---

### Task 1: Add Failing Tests For Overview Selector State

**Files:**
- Modify: `C:\Users\micha\dev\AffinitySimHub\.worktrees\recent-highlights-period-selector\Affinity.Tests\AffinityOverviewSummaryTests.cs`

- [ ] **Step 1: Write the failing test for default overview state**

```csharp
[TestMethod]
public void NewPlugin_InitializesRecentHighlightsSelectorToThisMonth()
{
    AffinityPlugin plugin = new AffinityPlugin();

    Assert.AreEqual(AffinityPlugin.RecentHighlightsPeriodThisMonth, plugin.SelectedRecentHighlightsPeriodKey);
    Assert.IsNotNull(plugin.RecentHighlightsPeriodOptions);
    CollectionAssert.AreEqual(
        new[]
        {
            AffinityPlugin.RecentHighlightsPeriodThisWeek,
            AffinityPlugin.RecentHighlightsPeriodLastWeek,
            AffinityPlugin.RecentHighlightsPeriodThisMonth,
            AffinityPlugin.RecentHighlightsPeriodLastMonth
        },
        plugin.RecentHighlightsPeriodOptions.Select(option => option.Key).ToArray());
    Assert.IsNotNull(plugin.SelectedRecentHighlightsSection);
}
```

- [ ] **Step 2: Write the failing test for active recent-highlights content**

```csharp
[TestMethod]
public void ApplySummarySnapshot_PopulatesSelectedRecentHighlightsSection()
{
    AffinityPlugin plugin = new AffinityPlugin();
    AffinitySummarySnapshot allTimeSnapshot = CreateSnapshot("All Game", "All Track", "All Car");
    AffinitySummarySnapshot selectedSnapshot = CreateSnapshot("This Game", "This Track", "This Car");
    AffinitySummarySnapshot compatibilitySnapshot = CreateSnapshot("Last Game", "Last Track", "Last Car");

    InvokeApplySummarySnapshot(plugin, allTimeSnapshot, selectedSnapshot, compatibilitySnapshot);

    Assert.AreEqual("This month highlights", plugin.SelectedRecentHighlightsSection.Header);
    Assert.AreEqual("This Game", plugin.SelectedRecentHighlightsSection.FeaturedGameTab.GameName);
    Assert.AreEqual("This month", plugin.SelectedRecentHighlightsPeriodDisplayName);
}
```

- [ ] **Step 3: Write the failing test for selector updates**

```csharp
[TestMethod]
public void SelectedRecentHighlightsPeriodKey_UpdatesDisplayState()
{
    AffinityPlugin plugin = new AffinityPlugin();

    plugin.SelectedRecentHighlightsPeriodKey = AffinityPlugin.RecentHighlightsPeriodLastWeek;

    Assert.AreEqual(AffinityPlugin.RecentHighlightsPeriodLastWeek, plugin.SelectedRecentHighlightsPeriodKey);
    Assert.AreEqual("Last week", plugin.SelectedRecentHighlightsPeriodDisplayName);
}
```

- [ ] **Step 4: Run overview tests to verify they fail**

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj --filter AffinityOverviewSummaryTests /p:SimHubInstallPath=C:\does-not-exist`

Expected: FAIL with missing `SelectedRecentHighlights*` members and/or outdated `ApplySummarySnapshot` expectations.

- [ ] **Step 5: Commit**

```bash
git add Affinity.Tests/AffinityOverviewSummaryTests.cs
git commit -m "test: cover recent highlights selector state"
```

### Task 2: Add Failing Tests For Recent-Highlights Period Ranges

**Files:**
- Modify: `C:\Users\micha\dev\AffinitySimHub\.worktrees\recent-highlights-period-selector\Affinity.Tests\GameTabTimePeriodRangeTests.cs`

- [ ] **Step 1: Write the failing test for this-week boundaries**

```csharp
[TestMethod]
public void TryGetRecentHighlightsPeriodUtcRange_UsesCurrentCultureWeekBoundariesForThisWeek()
{
    DateTime referenceLocal = new DateTime(2026, 7, 4, 12, 0, 0, DateTimeKind.Local);

    bool hasRange = AffinityPlugin.TryGetRecentHighlightsPeriodUtcRange(
        AffinityPlugin.RecentHighlightsPeriodThisWeek,
        referenceLocal,
        out DateTime? startUtc,
        out DateTime? endUtc);

    Assert.IsTrue(hasRange);
    Assert.AreEqual(new DateTime(2026, 6, 28, 0, 0, 0, DateTimeKind.Local).ToUniversalTime(), startUtc.Value);
    Assert.AreEqual(referenceLocal.ToUniversalTime(), endUtc.Value);
}
```

- [ ] **Step 2: Write the failing test for last-week boundaries**

```csharp
[TestMethod]
public void TryGetRecentHighlightsPeriodUtcRange_UsesPreviousCalendarWeekForLastWeek()
{
    DateTime referenceLocal = new DateTime(2026, 7, 4, 12, 0, 0, DateTimeKind.Local);

    bool hasRange = AffinityPlugin.TryGetRecentHighlightsPeriodUtcRange(
        AffinityPlugin.RecentHighlightsPeriodLastWeek,
        referenceLocal,
        out DateTime? startUtc,
        out DateTime? endUtc);

    Assert.IsTrue(hasRange);
    Assert.AreEqual(new DateTime(2026, 6, 21, 0, 0, 0, DateTimeKind.Local).ToUniversalTime(), startUtc.Value);
    Assert.AreEqual(new DateTime(2026, 6, 28, 0, 0, 0, DateTimeKind.Local).ToUniversalTime(), endUtc.Value);
}
```

- [ ] **Step 3: Write the failing test for month reuse**

```csharp
[TestMethod]
public void TryGetRecentHighlightsPeriodUtcRange_UsesLocalCalendarBoundariesForLastMonth()
{
    DateTime referenceLocal = new DateTime(2026, 7, 4, 12, 0, 0, DateTimeKind.Local);

    bool hasRange = AffinityPlugin.TryGetRecentHighlightsPeriodUtcRange(
        AffinityPlugin.RecentHighlightsPeriodLastMonth,
        referenceLocal,
        out DateTime? startUtc,
        out DateTime? endUtc);

    Assert.IsTrue(hasRange);
    Assert.AreEqual(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Local).ToUniversalTime(), startUtc.Value);
    Assert.AreEqual(new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Local).ToUniversalTime(), endUtc.Value);
}
```

- [ ] **Step 4: Run range tests to verify they fail**

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj --filter GameTabTimePeriodRangeTests /p:SimHubInstallPath=C:\does-not-exist`

Expected: FAIL with missing `TryGetRecentHighlightsPeriodUtcRange(...)` and period-key constants.

- [ ] **Step 5: Commit**

```bash
git add Affinity.Tests/GameTabTimePeriodRangeTests.cs
git commit -m "test: add recent highlights period range coverage"
```

### Task 3: Implement Plugin State And Range Logic

**Files:**
- Modify: `C:\Users\micha\dev\AffinitySimHub\.worktrees\recent-highlights-period-selector\Affinity\AffinityPlugin.cs`
- Modify: `C:\Users\micha\dev\AffinitySimHub\.worktrees\recent-highlights-period-selector\Affinity\AffinityDatabase.cs`

- [ ] **Step 1: Add recent-highlights period constants and exposed state**

```csharp
public const string RecentHighlightsPeriodThisWeek = "ThisWeek";
public const string RecentHighlightsPeriodLastWeek = "LastWeek";
public const string RecentHighlightsPeriodThisMonth = "ThisMonth";
public const string RecentHighlightsPeriodLastMonth = "LastMonth";

private static readonly IReadOnlyList<GameTabFilterOption> RecentHighlightsPeriodOptionsValue =
    new List<GameTabFilterOption>
    {
        new GameTabFilterOption(RecentHighlightsPeriodThisWeek, "This week"),
        new GameTabFilterOption(RecentHighlightsPeriodLastWeek, "Last week"),
        new GameTabFilterOption(RecentHighlightsPeriodThisMonth, "This month"),
        new GameTabFilterOption(RecentHighlightsPeriodLastMonth, "Last month")
    };
```

- [ ] **Step 2: Add one active recent-highlights section and display labels**

```csharp
public IReadOnlyList<GameTabFilterOption> RecentHighlightsPeriodOptions => RecentHighlightsPeriodOptionsValue;

public AffinityTopSummarySection SelectedRecentHighlightsSection { get; private set; } =
    new AffinityTopSummarySection { Header = "This month highlights" };

public string SelectedRecentHighlightsPeriodDisplayName => GetRecentHighlightsPeriodDisplayName(SelectedRecentHighlightsPeriodKey);

public string SelectedRecentHighlightsDateRangeDisplay { get; private set; } = string.Empty;
```

- [ ] **Step 3: Add the period-range helper and week-start calculation**

```csharp
internal static bool TryGetRecentHighlightsPeriodUtcRange(
    string periodKey,
    DateTime referenceLocal,
    out DateTime? startUtc,
    out DateTime? endUtc)
{
    DateTime localReference = referenceLocal.Kind == DateTimeKind.Utc ? referenceLocal.ToLocalTime() : referenceLocal;
    DateTime localStart;
    DateTime localEnd;

    switch (periodKey)
    {
        case RecentHighlightsPeriodThisWeek:
            localStart = GetStartOfWeek(localReference);
            localEnd = localReference;
            break;
        case RecentHighlightsPeriodLastWeek:
            localEnd = GetStartOfWeek(localReference);
            localStart = localEnd.AddDays(-7);
            break;
        case RecentHighlightsPeriodThisMonth:
            localStart = new DateTime(localReference.Year, localReference.Month, 1, 0, 0, 0, DateTimeKind.Local);
            localEnd = localReference;
            break;
        case RecentHighlightsPeriodLastMonth:
            localEnd = new DateTime(localReference.Year, localReference.Month, 1, 0, 0, 0, DateTimeKind.Local);
            localStart = localEnd.AddMonths(-1);
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
```

- [ ] **Step 4: Update refresh/apply flow to build a selected recent snapshot**

```csharp
DateTime nowLocal = DateTime.Now;
AffinitySummarySnapshot selectedRecentHighlightsSnapshot = BuildRecentHighlightsSnapshot(nowLocal);
AffinitySummarySnapshot lastMonthSnapshot = BuildMonthlySummarySnapshot(nowLocal.AddMonths(-1));
ExecuteOnUiThread(() => ApplySummarySnapshot(snapshot, selectedRecentHighlightsSnapshot, lastMonthSnapshot));
```

- [ ] **Step 5: Implement selector refresh behavior**

```csharp
internal void ApplySelectedRecentHighlightsPeriod()
{
    RefreshDistanceSummaries();
}
```

- [ ] **Step 6: Run focused tests to verify they pass**

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj --filter "AffinityOverviewSummaryTests|GameTabTimePeriodRangeTests" /p:SimHubInstallPath=C:\does-not-exist`

Expected: PASS for the updated overview and range coverage.

- [ ] **Step 7: Commit**

```bash
git add Affinity/AffinityPlugin.cs Affinity/AffinityDatabase.cs Affinity.Tests/AffinityOverviewSummaryTests.cs Affinity.Tests/GameTabTimePeriodRangeTests.cs
git commit -m "patch: add recent highlights period selector state"
```

### Task 4: Implement Overview XAML And Empty-State Rendering

**Files:**
- Modify: `C:\Users\micha\dev\AffinitySimHub\.worktrees\recent-highlights-period-selector\Affinity\AffinitySimHub.xaml`
- Modify: `C:\Users\micha\dev\AffinitySimHub\.worktrees\recent-highlights-period-selector\Affinity\AffinitySimHub.xaml.cs`
- Modify: `C:\Users\micha\dev\AffinitySimHub\.worktrees\recent-highlights-period-selector\Affinity.Tests\AffinityOverviewSummaryTests.cs`

- [ ] **Step 1: Replace the multi-row items control with a single selector-driven section**

```xml
<Grid Margin="0,4,0,8">
  <Grid.ColumnDefinitions>
    <ColumnDefinition Width="*" />
    <ColumnDefinition Width="Auto" />
  </Grid.ColumnDefinitions>
  <TextBlock FontSize="14" FontWeight="SemiBold" Text="Recent highlights" />
  <StackPanel Grid.Column="1" Orientation="Horizontal">
    <TextBlock Margin="0,0,6,0" VerticalAlignment="Center" Foreground="#B8B8B8" Text="Period" />
    <ComboBox Width="130"
              DisplayMemberPath="DisplayName"
              ItemsSource="{Binding DataContext.RecentHighlightsPeriodOptions, ElementName=Root}"
              SelectedValue="{Binding DataContext.SelectedRecentHighlightsPeriodKey, ElementName=Root, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
              SelectedValuePath="Key"
              SelectionChanged="RecentHighlightsPeriodSelectionChanged" />
  </StackPanel>
</Grid>
```

- [ ] **Step 2: Add single-row content and section-level empty state**

```xml
<TextBlock Margin="0,0,0,4"
           Foreground="#B8B8B8"
           Text="{Binding DataContext.SelectedRecentHighlightsDateRangeDisplay, ElementName=Root}" />
```

- [ ] **Step 3: Add the code-behind event handler**

```csharp
private void RecentHighlightsPeriodSelectionChanged(object sender, SelectionChangedEventArgs e)
{
    _plugin.ApplySelectedRecentHighlightsPeriod();
}
```

- [ ] **Step 4: Add/adjust overview tests for empty states and labels**

```csharp
[TestMethod]
public void SelectedRecentHighlightsSection_UsesSectionLevelEmptyStateWhenNoFeaturedSummaries()
{
    AffinityTopSummarySection section = new AffinityTopSummarySection { Header = "This week highlights" };

    Assert.AreEqual("No driving history yet", section.EmptyStateText);
}
```

- [ ] **Step 5: Run overview tests again**

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj --filter AffinityOverviewSummaryTests /p:SimHubInstallPath=C:\does-not-exist`

Expected: PASS with selector/empty-state expectations.

- [ ] **Step 6: Commit**

```bash
git add Affinity/AffinitySimHub.xaml Affinity/AffinitySimHub.xaml.cs Affinity.Tests/AffinityOverviewSummaryTests.cs
git commit -m "patch: update recent highlights overview layout"
```

### Task 5: Full Verification And SimHub Build

**Files:**
- Modify: `C:\Users\micha\dev\AffinitySimHub\.worktrees\recent-highlights-period-selector\docs/superpowers/plans/2026-07-04-recent-highlights-period-selector.md`

- [ ] **Step 1: Run the full test suite**

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist`

Expected: PASS with all tests green.

- [ ] **Step 2: Run the plugin build**

Run: `dotnet build .\Affinity\Affinity.csproj /p:SimHubInstallPath=C:\does-not-exist`

Expected: PASS and produce `Affinity.dll`.

- [ ] **Step 3: Build for SimHub copy if practical**

Run: `dotnet build .\Affinity\Affinity.csproj`

Expected: PASS and copy to `C:\Program Files (x86)\SimHub\` if the install is available and files are not locked.

- [ ] **Step 4: Review git status**

Run: `git status --short`

Expected: only intended feature files changed.

- [ ] **Step 5: Commit the finished feature**

```bash
git add Affinity/AffinityPlugin.cs Affinity/AffinityDatabase.cs Affinity/AffinitySimHub.xaml Affinity/AffinitySimHub.xaml.cs Affinity.Tests/AffinityOverviewSummaryTests.cs Affinity.Tests/GameTabTimePeriodRangeTests.cs docs/superpowers/plans/2026-07-04-recent-highlights-period-selector.md
git commit -m "patch: add recent highlights period selector"
```
