# Driving Pattern Charts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Correct Affinity's live Driving Patterns UI so it matches the approved mockup with a track-share donut and three eight-bucket comparison sparklines.

**Architecture:** Extend the pure pattern builder with explicit current-range boundaries and eight trend buckets. Add dependency-free WPF chart controls backed by pure geometry helpers, then bind them into the existing Overview while retaining visible text and legends for all values.

**Tech Stack:** C# 7.3, .NET Framework 4.8, WPF/DrawingContext, MSTest

**Spec:** `docs/superpowers/specs/2026-10-30-driving-pattern-charts-design.md`

## Global Constraints

- Continue on the existing `driving-patterns` branch.
- Add no third-party chart dependency, database migration, settings change, or telemetry write.
- Keep trend calculation and rendering outside `DataUpdate()` and telemetry-triggered refreshes.
- Use eight equal, half-open buckets across the selected current UTC range.
- Keep the existing range selector, observations, weekday chart, five-session threshold, comparison text, and SimHub dark/cyan styling.
- Preserve essential values as visible text; graphics are supplementary.
- Run tests and build serially with `/p:SimHubInstallPath=C:\does-not-exist`.
- Deploy only the six-file runtime manifest and do not copy `ac_track_id_map.json`.

## Review Focus

- A session exactly on an internal or final bucket boundary belongs to one bucket only; Task 1 pins half-open boundary behavior.
- Empty buckets remain present with zero values so chart spacing does not shift; Task 1 pins all eight results.
- Constant and single-point sparkline input remains visible and finite; Task 2 pins normalized point behavior.
- A null or empty donut source renders safely without arcs; Task 2 pins geometry helper output.
- Live chart controls must not trigger history work from telemetry refreshes; Task 3 reruns the existing hot-path regression suite and inspects the live Overview.

---

## File Structure

- Modify `Affinity/AffinityDrivingPatterns.cs`: add trend-bucket and comparison-series models.
- Modify `Affinity/AffinityPatternBuilder.cs`: produce eight time buckets from explicit range boundaries.
- Modify `Affinity/AffinityPlugin.cs`: pass the selected current UTC boundaries into the builder.
- Modify `Affinity.Tests/AffinityPatternBuilderTests.cs`: trend bucketing and boundary coverage.
- Create `Affinity/AffinityPatternChartControls.cs`: native WPF donut/sparkline controls plus pure geometry helpers.
- Create `Affinity.Tests/AffinityPatternChartGeometryTests.cs`: geometry normalization and empty/constant coverage.
- Modify `Affinity/AffinitySimHub.xaml`: donut, legend, and comparison sparkline layout.
- Modify `Affinity.Tests/AffinityUiStructureTests.cs`: binding-level chart structure coverage.
- Modify `README.md`: describe the corrected chart presentation.

### Task 1: Eight-Bucket Trend Data

**Files:**
- Modify: `Affinity/AffinityDrivingPatterns.cs`
- Modify: `Affinity/AffinityPatternBuilder.cs`
- Modify: `Affinity/AffinityPlugin.cs`
- Modify: `Affinity.Tests/AffinityPatternBuilderTests.cs`

**Interfaces:**
- Produces: `AffinityPatternTrendBucket` with `StartUtc`, `EndUtc`, `DistanceMeters`, `SessionCount`, and `MedianDurationSeconds`.
- Produces: `AffinityPatternComparison.TrendBuckets` as an eight-item `List<AffinityPatternTrendBucket>`.
- Changes: `AffinityPatternBuilder.Build(..., CultureInfo culture, DateTime currentStartUtc, DateTime currentEndUtc)`; every production call supplies explicit half-open UTC boundaries.

- [ ] **Step 1: Add failing builder tests**

Add `Build_CreatesEightEqualTrendBucketsIncludingEmptyBuckets`, `Build_AssignsInternalBoundariesToTheFollowingBucket`, and `Build_ExcludesTheFinalEndpoint`. Use hand-derived UTC timestamps and assert exact distance totals, session counts, and medians for all eight positions.

- [ ] **Step 2: Run focused tests and verify RED**

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist --filter "FullyQualifiedName~AffinityPatternBuilderTests"`

Expected: FAIL because the trend model and range-aware builder signature do not exist.

- [ ] **Step 3: Implement range-aware bucketing**

Add the trend model and builder signature. Partition by `StartedUtc` into eight equal ticks-based intervals; ignore rows before `currentStartUtc` and at/after `currentEndUtc`; emit all eight buckets and use zero median for empty buckets. Populate `Comparison.TrendBuckets` even when prior-period comparison is unavailable.

- [ ] **Step 4: Pass explicit ranges from the plugin**

Update `BuildPatternSnapshot` and its empty/fallback call so the selected current boundaries reach the builder. Preserve the existing repository query, legacy exclusion, explicit timezone/culture, and non-telemetry refresh cadence.

- [ ] **Step 5: Run focused and hot-path tests**

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist --filter "FullyQualifiedName~AffinityPatternBuilderTests|FullyQualifiedName~AffinityPluginLiveStatusTests|FullyQualifiedName~GameTabTimePeriodRangeTests"`

Expected: all selected tests PASS.

- [ ] **Step 6: Commit trend data**

Run: `git add Affinity/AffinityDrivingPatterns.cs Affinity/AffinityPatternBuilder.cs Affinity/AffinityPlugin.cs Affinity.Tests/AffinityPatternBuilderTests.cs && git commit -m "patch: add driving pattern trend buckets"`

### Task 2: Native Donut and Sparkline UI

**Files:**
- Modify: `Affinity/AffinityDrivingPatterns.cs`
- Create: `Affinity/AffinityPatternChartControls.cs`
- Create: `Affinity.Tests/AffinityPatternChartGeometryTests.cs`
- Modify: `Affinity/AffinitySimHub.xaml`
- Modify: `Affinity.Tests/AffinityUiStructureTests.cs`

**Interfaces:**
- Consumes: existing `AffinityTrackShare` and Task 1 `TrendBuckets`.
- Produces: `AffinityChartPoint` with normalized `X`/`Y` values.
- Produces: `AffinityDonutSegment` with `StartAngleDegrees` and `SweepAngleDegrees`.
- Produces: `AffinityPatternChartGeometry.NormalizeSparkline(IReadOnlyList<double> values)` and `BuildDonutSegments(IReadOnlyList<double> shares)`.
- Produces: `AffinityDonutChart : FrameworkElement` with `ItemsSource` dependency property (`IEnumerable<AffinityTrackShare>`).
- Produces: `AffinitySparkline : FrameworkElement` with `Values` dependency property (`IEnumerable<double>`).
- Produces: comparison convenience properties `DistanceTrendValues`, `SessionCountTrendValues`, and `MedianDurationTrendValues`, each projecting the eight trend buckets.

- [ ] **Step 1: Add failing geometry tests**

Assert donut shares `0.31, 0.18, 0.18, 0.33` produce four stable cumulative segments totaling 360 degrees; null/empty/zero input produces no segments. Assert sparkline values normalize first/last X to `0/1`, invert Y so larger values render higher, center constant and single values at `0.5`, preserve eight points, and return empty for null/empty/non-finite-only input.

- [ ] **Step 2: Add failing UI structure tests**

Assert the Overview uses `AffinityDonutChart` bound to `TrackShares`, three `AffinitySparkline` controls bound to the three comparison trend properties, a visible track legend, and no `ProgressBar` inside the `Where your distance went` section.

- [ ] **Step 3: Run chart/UI tests and verify RED**

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist --filter "FullyQualifiedName~AffinityPatternChartGeometryTests|FullyQualifiedName~AffinityUiStructureTests"`

Expected: FAIL because the helpers, controls, and bindings do not exist.

- [ ] **Step 4: Implement pure chart geometry and WPF controls**

Implement helpers without WPF state. Render the donut as a background ring plus stable cyan-family segments and center text for the leading percentage/track; render the sparkline as a neutral baseline plus a cyan polyline. Invalidate rendering when dependency-property collections change, tolerate null/empty sources, and honor actual control size.

- [ ] **Step 5: Replace the simplified track-share layout and add sparklines**

Use a wrapping donut/legend layout that stacks when narrow. Keep percentage/distance text visible in the legend. Add one sparkline below the value/delta text in each comparison card. Retain the weekday chart and existing section visibility triggers.

- [ ] **Step 6: Run focused tests and compile XAML**

Run focused chart/UI tests, then `dotnet build .\Affinity\Affinity.csproj /p:SimHubInstallPath=C:\does-not-exist`.

Expected: tests PASS; build succeeds with zero errors.

- [ ] **Step 7: Commit chart UI**

Run: `git add Affinity/AffinityDrivingPatterns.cs Affinity/AffinityPatternChartControls.cs Affinity/AffinitySimHub.xaml Affinity.Tests/AffinityPatternChartGeometryTests.cs Affinity.Tests/AffinityUiStructureTests.cs && git commit -m "patch: match driving patterns chart preview"`

### Task 3: Documentation, Verification, Deployment, and Live Inspection

**Files:**
- Modify: `README.md`
- Commit: `docs/superpowers/specs/2026-10-30-driving-pattern-charts-design.md`
- Commit: `docs/superpowers/plans/2026-10-30-driving-pattern-charts.md`

**Interfaces:**
- Consumes: completed Task 1 and Task 2 implementation.
- Produces: documented and live-verified chart correction.

- [ ] **Step 1: Update README chart wording**

Describe the weekday bars, track-share donut/legend, and three period sparklines without changing unrelated feature documentation.

- [ ] **Step 2: Run full verification serially**

Run `dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist`, then `dotnet build .\Affinity\Affinity.csproj /p:SimHubInstallPath=C:\does-not-exist`.

Expected: all tests pass; build succeeds with zero errors.

- [ ] **Step 3: Review branch scope**

Run `git status --short`, `git diff --check`, and `git diff main...HEAD --stat`. Confirm no mockup files, unrelated changes, or `ac_track_id_map.json` modifications.

- [ ] **Step 4: Commit documentation**

Run: `git add README.md docs/superpowers/specs/2026-10-30-driving-pattern-charts-design.md docs/superpowers/plans/2026-10-30-driving-pattern-charts.md && git commit -m "docs: describe driving pattern charts"`

- [ ] **Step 5: Deploy the six-file runtime manifest**

Copy `Affinity.dll`, `System.Data.SQLite.dll`, both top-level `x86`/`x64` `SQLite.Interop.dll` files, and both `PluginsData/Affinity/sqlite-native` recovery files from `Affinity/bin/Debug/net48` into `C:\Program Files (x86)\SimHub`. Do not copy `ac_track_id_map.json` or `Affinity.pdb`. Stop and report if SimHub locks any file.

- [ ] **Step 6: Inspect the live Affinity Overview**

Restart or refresh SimHub as needed, open Affinity Overview, and verify the donut, legend, three sparklines, weekday bars, observations, range changes, and narrow/wrapped layout are visible without clipping. Record any limitation plainly.
