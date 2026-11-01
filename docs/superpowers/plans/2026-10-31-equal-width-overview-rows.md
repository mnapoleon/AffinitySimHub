# Equal-Width Overview Rows Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make every card row on the Affinity Overview fill the available width and divide it equally among the cards present.

**Architecture:** Keep the existing presentation-only WPF structure and bindings. Replace the chart and comparison `WrapPanel` rows with one-row `UniformGrid` panels, remove fixed card widths, and use uniform right margins plus row-level compensation to preserve equal visible widths and outer-edge alignment.

**Tech Stack:** .NET Framework 4.8, WPF/XAML, MSTest

**Spec:** `docs/superpowers/specs/2026-10-31-equal-width-overview-rows-design.md`

## Global Constraints

- Use native WPF controls; add no dependencies, converters, or view-model code.
- Divide each row's full width equally among the cards actually present.
- Keep the two chart cards in equal halves and the three comparison cards in equal thirds.
- Preserve 10-pixel gaps, full outer-row alignment, card heights, wrapping, styling, bindings, charts, and calculations.
- Do not add responsive stacking or change the narrow-window layout in this task.
- Run tests and builds serially with `SimHubInstallPath=C:\does-not-exist`.
- Deploy only the six-file runtime manifest documented in `README.md`; do not copy `ac_track_id_map.json`.

## Review Focus

- The two chart cards should have equal visible widths and collectively align with the Top Overall row.
- The three comparison cards should have equal visible widths and collectively align with the Top Overall row.
- Adding or removing a direct card child should automatically redistribute that row without a fixed column count.
- The distance donut/legend and weekday bars should retain all existing bindings and content.
- The Pattern observation and Top Overall rows should remain unchanged.

---

### Task 1: Stretch Every Overview Card Row Equally

**Files:**
- Modify: `Affinity.Tests/AffinityUiStructureTests.cs:79`
- Modify: `Affinity/AffinitySimHub.xaml:278-374`

**Interfaces:**
- Consumes: the two direct chart `Border` children and three direct comparison `Border` children with their existing bindings and content.
- Produces: `PatternChartRow` and `PatternComparisonRow` one-row `UniformGrid` panels with no fixed column count or child widths.

- [ ] **Step 1: Update the structural regression test**

Rename `Overview_StretchesEveryObservationCardAcrossOneRow` to `Overview_UsesEqualWidthRowsForEveryCardSection`. Keep the observation assertions, then require:

- `PatternChartRow` is a `UniformGrid` with `Rows="1"`, no `Columns`, and `Margin="0,8,-10,0"`.
- Its two direct `Border` children have no `Width` and both use `Margin="0,0,10,12"`.
- `PatternComparisonRow` is a `UniformGrid` with `Rows="1"`, no `Columns`, and `Margin="0,0,-10,0"`.
- Its three direct `Border` children have no `Width` and all use `Margin="0,0,10,10"`.
- Existing weekday, donut, legend, and comparison trend bindings remain present.

- [ ] **Step 2: Run the focused test and verify it fails**

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist --filter "FullyQualifiedName~AffinityUiStructureTests.Overview_UsesEqualWidthRowsForEveryCardSection"`

Expected: FAIL because the chart/comparison rows are `WrapPanel` elements, the cards still have fixed widths, and `PatternComparisonRow` does not exist.

- [ ] **Step 3: Implement the two dynamic equal-width rows**

In `Affinity/AffinitySimHub.xaml`:

- Replace `PatternChartRow` with `<UniformGrid x:Name="PatternChartRow" Rows="1" Margin="0,8,-10,0">`.
- Remove `Width="430"` and `Width="600"` from its chart borders; set both margins to `0,0,10,12`.
- Replace the comparison `WrapPanel` with `<UniformGrid x:Name="PatternComparisonRow" Rows="1" Margin="0,0,-10,0">`.
- Remove `Width="220"` from all three comparison borders and keep their `0,0,10,10` margins.
- Leave every child control, binding, and style unchanged.

- [ ] **Step 4: Run the focused test and verify it passes**

Run the Step 2 command again.

Expected: PASS.

- [ ] **Step 5: Run complete validation serially**

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist`

Expected: all tests pass.

Then run: `dotnet build .\Affinity\Affinity.csproj /p:SimHubInstallPath=C:\does-not-exist`

Expected: build succeeds with no errors.

- [ ] **Step 6: Review and commit the implementation**

Confirm the diff changes only the chart/comparison layout containers and their structural test, then run `git diff --check`.

Commit:

```powershell
git add -- 'Affinity/AffinitySimHub.xaml' 'Affinity.Tests/AffinityUiStructureTests.cs'
git commit -m "patch: stretch overview card rows"
```

- [ ] **Step 7: Deploy and visually verify**

Copy the six runtime files listed under `README.md`'s “Live deploy into SimHub” section from `Affinity\bin\Debug\net48` into `C:\Program Files (x86)\SimHub`, preserving the installed `ac_track_id_map.json`. If any DLL is locked, stop and ask the user to close SimHub before retrying.

Open the Overview and confirm the two chart cards split the full row equally, the three comparison cards split it equally, and the Pattern observation row remains full-width.
