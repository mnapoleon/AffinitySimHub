# Dynamic Observation Card Widths Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make one, two, or three visible Pattern observation cards divide and fill the complete Overview row equally.

**Architecture:** Keep the existing presentation-only WPF layout and observation binding. Configure the `UniformGrid` as one row so WPF derives equal columns from the actual item count, while preserving the margin compensation that aligns the final card with the summary row.

**Tech Stack:** .NET Framework 4.8, WPF/XAML, MSTest

**Spec:** `docs/superpowers/specs/2026-10-31-dynamic-observation-card-widths-design.md`

## Global Constraints

- Use native WPF controls; add no dependencies or converter/view-model code.
- Keep every visible observation in one row and divide the available width equally among the actual item count.
- Preserve the builder's three-observation maximum.
- Preserve 10-pixel gaps, full outer-row alignment, `MinHeight="112"`, text wrapping, styling, bindings, and card order.
- Preserve the weekday/distance chart row and all Driving Patterns calculations.
- Run tests and builds serially with `SimHubInstallPath=C:\does-not-exist`.
- Deploy only the six-file runtime manifest documented in `README.md`; do not copy `ac_track_id_map.json`.

## Review Focus

- One observation should occupy the complete row rather than one-third of it.
- Two observations should split the complete row evenly rather than leaving an empty third slot.
- Three observations should remain equal-width in one row.
- The final card should align with the summary row's right edge while retaining 10-pixel inter-card gaps.
- Long text should still grow vertically beyond 112 pixels, and the weekday/distance panels should remain unchanged.

---

### Task 1: Derive Observation Columns From the Visible Card Count

**Files:**
- Modify: `Affinity.Tests/AffinityUiStructureTests.cs:79`
- Modify: `Affinity/AffinitySimHub.xaml:253`

**Interfaces:**
- Consumes: `PatternObservationCards.ItemsSource="{Binding Observations}"`, whose builder returns at most three items.
- Produces: a one-row `UniformGrid` with no fixed `Columns` value, allowing WPF to calculate one, two, or three equal columns.

- [ ] **Step 1: Update the structural regression test**

Rename `Overview_UsesThreeEqualObservationCardsAndBalancedChartRow` to `Overview_StretchesEveryObservationCardAcrossOneRow`. Require the observation `UniformGrid` to have `Rows="1"` and no `Columns` attribute. Retain the assertions for the `Observations` binding, `Margin="0,0,-10,0"`, no `WrapPanel`, no fixed card width/height, `MinHeight="112"`, and the existing 430/600 chart widths and bindings.

- [ ] **Step 2: Run the focused test and verify it fails**

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist --filter "FullyQualifiedName~AffinityUiStructureTests.Overview_StretchesEveryObservationCardAcrossOneRow"`

Expected: FAIL because the current `UniformGrid` has `Columns="3"` and no `Rows` value.

- [ ] **Step 3: Implement the dynamic equal-width row**

In `Affinity/AffinitySimHub.xaml`, change `<UniformGrid Columns="3" />` to `<UniformGrid Rows="1" />`. Do not alter the `ItemsControl` margin, card template, chart row, bindings, or behavior code.

- [ ] **Step 4: Run the focused test and verify it passes**

Run the Step 2 command again.

Expected: PASS.

- [ ] **Step 5: Run complete validation serially**

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist`

Expected: all tests pass.

Then run: `dotnet build .\Affinity\Affinity.csproj /p:SimHubInstallPath=C:\does-not-exist`

Expected: build succeeds with no errors.

- [ ] **Step 6: Review and commit the implementation**

Confirm the diff contains only the single layout rule and its structural test, then run `git diff --check`.

Commit:

```powershell
git add -- 'Affinity/AffinitySimHub.xaml' 'Affinity.Tests/AffinityUiStructureTests.cs'
git commit -m "patch: stretch all pattern observation cards"
```

- [ ] **Step 7: Deploy and verify**

Copy the six runtime files listed under `README.md`'s “Live deploy into SimHub” section from `Affinity\bin\Debug\net48` into `C:\Program Files (x86)\SimHub`, preserving the installed `ac_track_id_map.json`. If any DLL is locked, stop and ask the user to close SimHub before retrying.

In SimHub, verify that the current set of visible observation cards collectively fills the entire row and the chart row remains unchanged.
