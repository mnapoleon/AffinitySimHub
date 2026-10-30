# Driving Patterns Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Recent Highlights and Overview game totals with session-derived Driving Patterns, and show each game's cumulative total at the top of its own tab.

**Architecture:** Add one bounded session-history read to `AffinitySqliteRepository` and keep all analysis in a pure `AffinityPatternBuilder`. `AffinityPlugin` owns range selection and snapshot refresh, while XAML only renders the resulting observation, weekday, track-share, and comparison models. No schema, settings, or telemetry-hot-path changes are required.

**Tech Stack:** C# 7.3, .NET Framework 4.8, WPF/XAML, System.Data.SQLite, MSTest

**Spec:** `docs/superpowers/specs/2026-10-29-driving-patterns-design.md`

## Global Constraints

- Work on the existing `driving-patterns` branch; do not prefix the branch with `codex/`.
- Keep `DataUpdate()` free of pattern queries and calculations.
- Do not change the SQLite schema or `AffinitySettings` persistence shape.
- Exclude rows whose `session_uid` begins with `legacy-` from pattern history.
- Require five current-period sessions for patterns and three prior-period sessions for comparison deltas.
- Pattern identities remain game-scoped.
- Remove Recent Highlights completely and move Overview game-total cards into their corresponding game tabs.
- Run tests and build serially with `/p:SimHubInstallPath=C:\does-not-exist`.
- Deploy only the six-file runtime manifest; do not copy `ac_track_id_map.json`.

## Review Focus

- A session starting exactly at a UTC boundary belongs to only the correct half-open range; Task 1 pins both endpoints.
- Synthetic legacy rows mixed with genuine rows never affect patterns; Task 1 pins mixed-history filtering.
- Local day/time grouping stays correct across UTC offsets and day rollover; Task 2 uses a custom time zone in tests.
- Zero prior distance never creates an infinite percentage; Task 2 expects an unavailable distance delta.
- Rebuilding tabs after a range change preserves the selected game tab and its filter state; Task 3 extends tab-refresh coverage.

---

## File Structure

- Create `Affinity/AffinityDrivingPatterns.cs`: pattern record and UI snapshot model types only.
- Create `Affinity/AffinityPatternBuilder.cs`: pure aggregation, thresholds, observation selection, and formatting.
- Create `Affinity.Tests/AffinityPatternBuilderTests.cs`: deterministic builder coverage.
- Modify `Affinity/AffinitySqliteRepository.cs`: bounded non-legacy session-history query.
- Modify `Affinity/AffinityPlugin.cs`: range state, window calculation, repository/builder orchestration, and removal of Recent Highlights state.
- Modify `Affinity/AffinityDatabase.cs`: only game-tab notification/model adjustments required by the moved total card.
- Modify `Affinity/AffinitySimHub.xaml` and `.xaml.cs`: render Driving Patterns, move game totals, and replace the old selection handler.
- Modify repository/overview/range/tab-refresh tests and `README.md` to cover the new behavior.

### Task 1: Trustworthy Session-History Read

**Files:**
- Create: `Affinity/AffinityDrivingPatterns.cs`
- Modify: `Affinity/AffinitySqliteRepository.cs`
- Test: `Affinity.Tests/AffinitySqliteRepositoryTests.cs`

**Interfaces:**
- Produces: `AffinitySessionRecord` with `SessionUid`, `GameName`, `CarModel`, `TrackName`, `TrackDisplayName`, `StartedUtc`, `EndedUtc`, `DistanceMeters`, and `TimeDrivenSeconds`.
- Produces: `List<AffinitySessionRecord> AffinitySqliteRepository.GetPatternSessions(DateTime startUtc, DateTime endUtc)`.

- [ ] **Step 1: Add failing repository tests**

Add `GetPatternSessions_ReturnsGameScopedDisplayDataWithinHalfOpenRange` and assert that a row at `startUtc` is included, a row at `endUtc` is excluded, display names are preferred, and equal track labels from different games remain separate records.

Add `GetPatternSessions_ExcludesLegacyRowsFromMixedHistory` and assert that a `legacy-*` import row is excluded while a normal row inside the same range remains.

- [ ] **Step 2: Run the focused repository tests and verify failure**

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist --filter "FullyQualifiedName~AffinitySqliteRepositoryTests.GetPatternSessions"`

Expected: FAIL because `GetPatternSessions` and `AffinitySessionRecord` do not exist.

- [ ] **Step 3: Add the record model and repository query**

Define `AffinitySessionRecord` in `AffinityDrivingPatterns.cs`. Implement `GetPatternSessions` with `s.started_utc >= @startUtc AND s.started_utc < @endUtc AND s.session_uid NOT LIKE 'legacy-%'`, joining games, cars, tracks, and contexts. Use existing display-name fallback rules and `ReadUtcDateTime`.

- [ ] **Step 4: Run the focused repository tests**

Expected: all `GetPatternSessions` tests PASS.

- [ ] **Step 5: Commit the session-history boundary**

Run: `git add Affinity/AffinityDrivingPatterns.cs Affinity/AffinitySqliteRepository.cs Affinity.Tests/AffinitySqliteRepositoryTests.cs && git commit -m "patch: add trustworthy pattern session history"`

### Task 2: Pure Pattern Aggregation

**Files:**
- Modify: `Affinity/AffinityDrivingPatterns.cs`
- Create: `Affinity/AffinityPatternBuilder.cs`
- Create: `Affinity.Tests/AffinityPatternBuilderTests.cs`

**Interfaces:**
- Consumes: `AffinitySessionRecord` from Task 1.
- Produces: `AffinityPatternSnapshot AffinityPatternBuilder.Build(IReadOnlyCollection<AffinitySessionRecord> currentSessions, IReadOnlyCollection<AffinitySessionRecord> previousSessions, bool displayInMiles, TimeZoneInfo localTimeZone, CultureInfo culture)`.
- Produces: `AffinityPatternSnapshot` with `HasSufficientData`, `EmptyStateText`, `Observations`, `WeekdayBuckets`, `TrackShares`, and `Comparison`; insufficient data uses `Complete five sessions in this period to reveal your driving patterns.`.
- Produces: `AffinityPatternObservation` with `Category`, `Title`, and `Detail`.
- Produces: `AffinityWeekdayPattern` with `DayOfWeek`, `Label`, `TimeDrivenSeconds`, `TimeDrivenDisplay`, and normalized `RelativeHeight` from `0.0` to `1.0`.
- Produces: `AffinityTrackShare` with `GameName`, `TrackDisplayName`, `Label`, `DistanceDisplay`, `SharePercent`, normalized `RelativeWidth`, and `IsOther`.
- Produces: `AffinityPatternComparison` with current distance/session-count/median-duration display values, nullable `DistancePercentChange`, nullable `SessionCountDifference`, nullable `MedianDurationDifferenceSeconds`, and `HasComparison`.

- [ ] **Step 1: Add failing threshold and median tests**

Add tests asserting four current sessions return `HasSufficientData == false`; five return true; odd durations `20, 30, 90` yield a 30-minute median; even durations `20, 30, 40, 90` yield 35 minutes; and fewer than three prior sessions leave comparison deltas unavailable.

- [ ] **Step 2: Add failing time-group and track-share tests**

Using a deterministic custom UTC-minus-four time zone, assert UTC starts that cross local midnight land on the correct weekday/time band. Assert track shares use `(GameName, TrackDisplayName)` keys, sort by distance then display name, return the top three plus `Other`, and sum to 100% within rounding tolerance.

- [ ] **Step 3: Add failing comparison and display tests**

Assert kilometers/miles conversion, distance percentage change, signed session-count change, signed median-minute change, and unavailable distance percentage when prior distance is zero.

- [ ] **Step 4: Add failing observation tests**

Pin named constants and deterministic order: concentration at 30% share, dominant weekday at 35% driven time, dominant time band at 40%, meaningful distance change at 15%, session-count change at two, median-duration change at ten minutes, and variety fallback at two distinct cars or tracks. Assert stable display-name tie breaks and a maximum of three cards.

- [ ] **Step 5: Run builder tests and verify failure**

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist --filter "FullyQualifiedName~AffinityPatternBuilderTests"`

Expected: FAIL because builder and snapshot types are incomplete.

- [ ] **Step 6: Implement models and `AffinityPatternBuilder.Build`**

Use constants `MinimumCurrentSessions = 5` and `MinimumPreviousSessions = 3`. Aggregate only the supplied rows, order weekdays from `culture.DateTimeFormat.FirstDayOfWeek`, use median driven seconds, and create at most three observations in concentration/schedule/change order with variety as fallback.

- [ ] **Step 7: Run builder tests**

Expected: all `AffinityPatternBuilderTests` PASS.

- [ ] **Step 8: Commit the pure analytics layer**

Run: `git add Affinity/AffinityDrivingPatterns.cs Affinity/AffinityPatternBuilder.cs Affinity.Tests/AffinityPatternBuilderTests.cs && git commit -m "patch: add driving pattern analysis"`

### Task 3: Plugin Range State and Refresh Integration

**Files:**
- Modify: `Affinity/AffinityPlugin.cs`
- Modify: `Affinity.Tests/GameTabTimePeriodRangeTests.cs`
- Modify: `Affinity.Tests/AffinityOverviewSummaryTests.cs`
- Modify: `Affinity.Tests/AffinityPluginTabRefreshTests.cs`

**Interfaces:**
- Consumes: `GetPatternSessions(...)` and `AffinityPatternBuilder.Build(...)`.
- Produces: range constants `PatternRangeLast30Days`, `PatternRangeLast90Days`, `PatternRangeThisYear`.
- Produces: `SelectedPatternRangeKey`, `PatternRangeOptions`, `DrivingPatternSnapshot`, and `ApplySelectedPatternRange()`.
- Produces: `TryGetPatternRangeUtcRanges(string rangeKey, DateTime referenceLocal, out DateTime currentStartUtc, out DateTime currentEndUtc, out DateTime previousStartUtc, out DateTime previousEndUtc)`.

- [ ] **Step 1: Replace Recent Highlights assertions with failing pattern-state assertions**

Update overview tests to require default `Last30Days`, the three agreed options, a null-safe initial snapshot, and `ApplySummarySnapshot(AffinitySummarySnapshot snapshot, AffinityPatternSnapshot patternSnapshot)` without Monthly/Recent Highlight collections.

- [ ] **Step 2: Replace old range tests with failing pattern-window tests**

Assert rolling 30/90-day half-open windows and their immediately preceding windows. Assert `ThisYear` uses local January 1 and the equivalent previous-year local date/time, including clamping February 29 to February 28.

- [ ] **Step 3: Add a failing refresh-preservation test**

Extend `AffinityPluginTabRefreshTests` so applying a new pattern snapshot preserves the selected game tab by game name and retains its selected time/sort/limit filters.

- [ ] **Step 4: Run the three focused test classes and verify failure**

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist --filter "FullyQualifiedName~AffinityOverviewSummaryTests|FullyQualifiedName~GameTabTimePeriodRangeTests|FullyQualifiedName~AffinityPluginTabRefreshTests"`

Expected: FAIL on missing pattern range/state and obsolete Recent Highlights behavior.

- [ ] **Step 5: Implement range state and pattern refresh**

Remove `RecentHighlights*`, `MonthlyTopSummarySections`, and obsolete monthly/highlight snapshot builders. Add the new range options/properties, compute windows in local time, read one combined repository span, partition current/previous rows, build the snapshot, and update it only from summary refresh or `ApplySelectedPatternRange()`.

- [ ] **Step 6: Simplify `ApplySummarySnapshot` without rebuilding unrelated tab state**

Change its signature to accept the all-time summary and pattern snapshot, preserve existing tab replacement/selection logic, set `OverallTopSummarySection`, `Featured*`, `GameTabs`, and `DrivingPatternSnapshot`, and remove compatibility collections used only by the deleted Overview sections.

- [ ] **Step 7: Run the focused integration tests**

Expected: all three focused test classes PASS.

- [ ] **Step 8: Commit plugin integration**

Run: `git add Affinity/AffinityPlugin.cs Affinity.Tests/GameTabTimePeriodRangeTests.cs Affinity.Tests/AffinityOverviewSummaryTests.cs Affinity.Tests/AffinityPluginTabRefreshTests.cs && git commit -m "patch: integrate driving pattern ranges"`

### Task 4: Overview and Game-Tab UI

**Files:**
- Modify: `Affinity/AffinitySimHub.xaml`
- Modify: `Affinity/AffinitySimHub.xaml.cs`
- Modify: `Affinity/AffinityDatabase.cs`
- Create: `Affinity.Tests/AffinityUiStructureTests.cs`

**Interfaces:**
- Consumes: Task 3 plugin properties and Task 2 snapshot collections.
- Produces: `PatternRangeSelectionChanged(object sender, SelectionChangedEventArgs e)` delegating to `ApplySelectedPatternRange()`.

- [ ] **Step 1: Add failing XAML structure tests**

Read `AffinitySimHub.xaml` and assert it contains `Driving patterns`, `Pattern observations`, `When you drive`, `Where your distance went`, and the pattern selector binding. Assert `Recent highlights`, `Game totals`, and `RecentHighlightsRangeSelectionChanged` are absent. Assert the `GameDistanceTab` template binds its top summary card to `TotalDistanceDisplay` and `TotalUsedTimeDisplay`.

- [ ] **Step 2: Run UI structure tests and verify failure**

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist --filter "FullyQualifiedName~AffinityUiStructureTests"`

Expected: FAIL because the old sections remain and Driving Patterns is absent.

- [ ] **Step 3: Replace the Overview sections**

Delete Recent Highlights and Overview Game Totals XAML. Add the shared range selector, a single insufficient-data empty state, up to three observation cards, weekday bars bound to relative time, game-scoped top-track share bars, and comparison metric cards. Use existing dark `SHSection`, `#252525` surfaces, and cyan accents; stack grids at narrow widths rather than introducing horizontal scrolling.

- [ ] **Step 4: Move the game total card**

Add a total-distance/time summary card at the start of each `GameDistanceTab` template, before top track/car. Ensure `GameDistanceTab` raises any additional property notifications needed when display units refresh; do not change its persistence or filtering responsibilities.

- [ ] **Step 5: Replace the selection handler**

Remove `RecentHighlightsRangeSelectionChanged` and add `PatternRangeSelectionChanged`, delegating directly to `_plugin.ApplySelectedPatternRange()`.

- [ ] **Step 6: Run UI, overview, and tab-refresh tests**

Expected: `AffinityUiStructureTests`, `AffinityOverviewSummaryTests`, and `AffinityPluginTabRefreshTests` PASS.

- [ ] **Step 7: Build the plugin for XAML compile validation**

Run: `dotnet build .\Affinity\Affinity.csproj /p:SimHubInstallPath=C:\does-not-exist`

Expected: build succeeds with zero errors.

- [ ] **Step 8: Commit the UI change**

Run: `git add Affinity/AffinitySimHub.xaml Affinity/AffinitySimHub.xaml.cs Affinity/AffinityDatabase.cs Affinity.Tests/AffinityUiStructureTests.cs && git commit -m "patch: show driving patterns on overview"`

### Task 5: Documentation, Full Verification, and Map-Safe Deployment

**Files:**
- Modify: `README.md`
- Verify: all changed source, tests, design, and plan files

**Interfaces:**
- Consumes: completed Tasks 1–4.
- Produces: documented user behavior and validated/deployed plugin binaries.

- [ ] **Step 1: Update user-facing documentation**

Replace Recent Highlights and Overview game-total descriptions with the three pattern ranges, observations/charts, five-session trust threshold, legacy-row exclusion, and game-tab total-card placement. Keep implementation detail concise and preserve the existing supported-game and deployment documentation.

- [ ] **Step 2: Run the full test project serially**

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist`

Expected: all tests PASS with zero failures.

- [ ] **Step 3: Run the plugin build after tests complete**

Run: `dotnet build .\Affinity\Affinity.csproj /p:SimHubInstallPath=C:\does-not-exist`

Expected: build succeeds with zero errors.

- [ ] **Step 4: Review branch scope and whitespace**

Run: `git status --short`, `git diff --check`, and `git diff main...HEAD --stat`. Confirm only intended files are present and no mockup files or `ac_track_id_map.json` changes entered the branch.

- [ ] **Step 5: Commit documentation and any verification-only fixes**

Run: `git add README.md docs/superpowers/specs/2026-10-29-driving-patterns-design.md docs/superpowers/plans/2026-10-29-driving-patterns.md && git commit -m "docs: describe driving patterns"`

- [ ] **Step 6: Copy the validated runtime manifest into SimHub**

Copy `Affinity.dll`, `System.Data.SQLite.dll`, top-level `x86/SQLite.Interop.dll` and `x64/SQLite.Interop.dll`, plus both `PluginsData/Affinity/sqlite-native` recovery copies from `Affinity/bin/Debug/net48` into `C:\Program Files (x86)\SimHub`. Do not copy `ac_track_id_map.json` or `Affinity.pdb`. If any file is locked, stop and report that SimHub must be closed or restarted.

- [ ] **Step 7: Record final verification and deployment state**

Report the exact test/build results, whether all six runtime files copied, whether SimHub locking blocked deployment, and any remaining untracked or unstaged files.
