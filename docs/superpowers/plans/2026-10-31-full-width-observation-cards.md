# Full-Width Observation Cards Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the three Pattern observations cards share the full Overview width in equal columns, matching the span of the older summary cards.

**Architecture:** Keep this as a presentation-only WPF change. Replace the observation `ItemsControl`'s wrapping panel with a three-column `UniformGrid`, remove the fixed card width, and preserve every binding, style, minimum height, and chart-row setting.

**Tech Stack:** .NET Framework 4.8, WPF/XAML, MSTest

**Spec:** `docs/superpowers/specs/2026-10-31-full-width-observation-cards-design.md`

## Global Constraints

- Use native WPF controls; add no dependencies.
- Keep three equal observation columns across the available Overview width.
- Retain `MinHeight="112"`, text wrapping, card styling, observation order, and `Observations` binding.
- Do not add a fixed card `Width` or `Height`; long observation text must still grow vertically.
- Preserve the compact weekday/distance chart row and all existing pattern calculations.
- Run tests and builds serially with `SimHubInstallPath=C:\does-not-exist`.
- Deploy only the six-file runtime manifest documented in `README.md`; do not copy `ac_track_id_map.json`.

## Review Focus

- With three observations, all three cards should divide the available row evenly and reach the same overall span as the summary-card row.
- With one or two observations, each card should remain one-third of the row and stay left-aligned rather than stretching into fewer oversized columns.
- Long titles or details should wrap and increase card height beyond 112 pixels without clipping.
- The observation template must have neither `Width` nor `Height`, so the grid owns horizontal sizing and content owns vertical growth.
- The weekday and distance panels must retain their current bindings and widths of 430 and 600 respectively.

---

### Task 1: Stretch Observation Cards Across the Overview

**Files:**
- Modify: `Affinity.Tests/AffinityUiStructureTests.cs:79`
- Modify: `Affinity/AffinitySimHub.xaml:253`

**Interfaces:**
- Consumes: `PatternObservationCards.ItemsSource="{Binding Observations}"` and the existing observation `DataTemplate`.
- Produces: a `UniformGrid` with `Columns="3"` whose child cards have `MinHeight="112"` and no fixed `Width` or `Height`.

- [ ] **Step 1: Update the structural test to describe the full-width layout**

Rename `Overview_UsesThreeCompactObservationCardsAndBalancedChartRow` to `Overview_UsesThreeEqualObservationCardsAndBalancedChartRow`. Assert that the observation `ItemsPanelTemplate` contains exactly one `UniformGrid` with `Columns="3"`, contains no `WrapPanel`, and that the card border has no `Width` or `Height` while retaining `MinHeight="112"`. Keep the assertions that pin `Observations`, the 430/600 chart widths, and the weekday/distance bindings.

- [ ] **Step 2: Run the focused test and verify the new assertions fail**

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist --filter "FullyQualifiedName~AffinityUiStructureTests.Overview_UsesThreeEqualObservationCardsAndBalancedChartRow"`

Expected: FAIL because the current observation panel is a `WrapPanel` and each card still has `Width="360"`.

- [ ] **Step 3: Implement the equal three-column XAML layout**

In `Affinity/AffinitySimHub.xaml`, replace the observation `WrapPanel` with `<UniformGrid Columns="3" />` and remove `Width="360"` from the observation card border. Leave its `MinHeight`, margin, padding, colors, bindings, and wrapping unchanged; do not alter `PatternChartRow`.

- [ ] **Step 4: Run the focused test and verify it passes**

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist --filter "FullyQualifiedName~AffinityUiStructureTests.Overview_UsesThreeEqualObservationCardsAndBalancedChartRow"`

Expected: PASS.

- [ ] **Step 5: Run complete validation serially**

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist`

Expected: all tests pass.

Then run: `dotnet build .\Affinity\Affinity.csproj /p:SimHubInstallPath=C:\does-not-exist`

Expected: build succeeds with no errors.

- [ ] **Step 6: Review and commit the implementation**

Inspect the diff to confirm only the observation layout and its structural test changed, then run `git diff --check`.

Commit:

```powershell
git add -- 'Affinity/AffinitySimHub.xaml' 'Affinity.Tests/AffinityUiStructureTests.cs'
git commit -m "patch: stretch pattern observation cards"
```

- [ ] **Step 7: Deploy the validated runtime files and verify the live layout**

Copy the six runtime files listed under `README.md`'s “Live deploy into SimHub” section from `Affinity\bin\Debug\net48` into `C:\Program Files (x86)\SimHub`, preserving the installed `ac_track_id_map.json`. If the DLL is locked, stop and ask the user to close SimHub before retrying.

Open Affinity's Overview in SimHub and confirm the three observation cards evenly span the row, long text remains readable, and the compact weekday/distance row is unchanged.
