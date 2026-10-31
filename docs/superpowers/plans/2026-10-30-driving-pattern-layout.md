# Compact Driving Pattern Layout Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Present observations as three compact cards and place the weekday and distance charts together in a balanced dashboard row.

**Architecture:** This is a presentation-only WPF change. Existing view models, bindings, chart controls, data calculations, and persistence remain unchanged; XAML structure tests pin the selected card sizes, chart grouping, bindings, and wrapping containers.

**Tech Stack:** .NET Framework 4.8, WPF/XAML, MSTest

**Spec:** `docs/superpowers/specs/2026-10-30-driving-pattern-layout-design.md`

## Global Constraints

- Continue using native WPF and existing Affinity view models.
- Keep layout changes in the Overview XAML unless a small presentation-only helper is required.
- Preserve the live SimHub track map during validation and deployment.
- Verify focused structure tests, then run the full test suite and plugin build serially.
- Do not change pattern calculations, persistence, range semantics, chart data, comparison cards, game tabs, or Overview totals.

## Review Focus

- One or two observations: cards remain compact and left-aligned without stretching into full-width strips; pin to Task 1 structure tests.
- More than three observations: the card container wraps additional items to another row without clipping; pin to Task 1 container-type assertions.
- Long observation details: text wraps and the card can grow beyond its minimum height; pin to Task 1 `MinHeight` rather than fixed `Height` assertion.
- SimHub minimum-width window: chart panels wrap instead of introducing horizontal scrolling or clipping; pin to Task 1 `WrapPanel` assertion.
- Existing chart bindings and sufficient-data visibility: the rearrangement does not detach either chart from its source; pin to Task 1 binding assertions and the existing Overview structure suite.

---

### Task 1: Compact Overview Pattern Layout

**Files:**
- Modify: `Affinity.Tests/AffinityUiStructureTests.cs`
- Modify: `Affinity/AffinitySimHub.xaml`

**Interfaces:**
- Consumes: `AffinityPatternSnapshot.Observations`, `WeekdayBuckets`, and `TrackShares`; existing `AffinityDonutChart.ItemsSource`.
- Produces: XAML-only `PatternObservationCards`, `PatternChartRow`, `WeekdayPatternPanel`, and `DistancePatternPanel` layout elements for structural verification.

- [ ] **Step 1: Add failing structure tests**

Add `Overview_UsesThreeCompactObservationCardsAndBalancedChartRow` to `AffinityUiStructureTests`. Assert:

- `PatternObservationCards` is the `ItemsControl` bound to `{Binding Observations}`.
- Its `ItemsPanelTemplate` contains a `WrapPanel`, allowing one-to-three cards on the first row and further items on later rows.
- Its item-template `Border` uses `Width="360"` and `MinHeight="112"`, and does not set a fixed `Height`.
- `PatternChartRow` is a `WrapPanel` containing `WeekdayPatternPanel` at `Width="430"` and `DistancePatternPanel` at `Width="600"`.
- The weekday panel remains bound to `{Binding WeekdayBuckets}` and the distance panel contains `AffinityDonutChart ItemsSource="{Binding TrackShares}"` plus the existing legend binding.

- [ ] **Step 2: Run the focused test and verify RED**

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist --filter "FullyQualifiedName~AffinityUiStructureTests"`

Expected: FAIL because the named wrapping layout and compact card dimensions do not exist.

- [ ] **Step 3: Implement the approved Balanced layout**

In `AffinitySimHub.xaml`:

- Name the observation `ItemsControl` `PatternObservationCards`.
- Give it a `WrapPanel` items panel and render each observation in a `360`-wide border with `MinHeight="112"`, `Margin="0,0,10,10"`, and the existing colors/text bindings. Do not set fixed height.
- Replace the two vertically stacked chart borders with one named `WrapPanel` called `PatternChartRow`.
- Put the existing weekday content in `WeekdayPatternPanel`, `Width="430"`, `Margin="0,0,10,12"`.
- Put the existing distance content in `DistancePatternPanel`, `Width="600"`, `Margin="0,0,0,12"`.
- Preserve the existing weekday bars, donut, matching legend colors, headings, tooltips, and bindings unchanged inside those panels.

- [ ] **Step 4: Run focused tests and verify GREEN**

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist --filter "FullyQualifiedName~AffinityUiStructureTests"`

Expected: all `AffinityUiStructureTests` pass.

- [ ] **Step 5: Run full verification serially**

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist`

Expected: all tests pass.

Then run: `dotnet build .\Affinity\Affinity.csproj /p:SimHubInstallPath=C:\does-not-exist`

Expected: build succeeds with zero errors.

- [ ] **Step 6: Commit the implementation**

```powershell
git add -- Affinity/AffinitySimHub.xaml Affinity.Tests/AffinityUiStructureTests.cs
git commit -m "patch: compact driving pattern layout"
```

- [ ] **Step 7: Deploy and inspect live SimHub**

Copy only the six approved runtime files from `Affinity/bin/Debug/net48` into `C:\Program Files (x86)\SimHub`: `Affinity.dll`, `System.Data.SQLite.dll`, both top-level `x86`/`x64` `SQLite.Interop.dll` files, and both `PluginsData/Affinity/sqlite-native` recovery copies. Do not copy `ac_track_id_map.json` or `Affinity.pdb`; stop if SimHub locks a file.

Open the Affinity Overview and verify three observation cards appear across one row at normal width, the weekday and distance panels share one row, long text remains readable, both rows wrap without clipping at the minimum usable window width, and range switching still redraws the same charts.
