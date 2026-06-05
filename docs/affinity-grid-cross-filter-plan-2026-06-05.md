# Affinity Grid Cross-Filter Plan

Date: 2026-06-05

## Goal

Improve the per-game Affinity summary tab so track and car grids can cross-filter each other.

Desired interaction:

- Selecting a track filters the car grid to cars driven on that track.
- Selecting a car filters the track grid to tracks driven in that car.
- The filtered grid should show the distance and time for the selected track or car only.
- Only one cross-filter is active at a time.
- Selecting a track after a car selection clears the car filter and applies the track filter.
- Selecting a car after a track selection clears the track filter and applies the car filter.
- A dedicated clear action resets both grids to the full unfiltered lists.

## Current State

The current UI renders independent `TrackSummaries` and `CarSummaries` lists for each `GameDistanceTab`.

Relevant files:

- `Affinity/AffinitySimHub.xaml`
- `Affinity/AffinitySummaryBuilder.cs`
- `Affinity/AffinityDatabase.cs`
- `Affinity/AffinityPlugin.cs`

Today:

- The grids are read-only and do not track selection.
- The summary builder aggregates track and car totals separately from the same game-level data.
- Refresh rebuilds the game tabs and replaces the current tab objects.

## Proposed Behavior

Each game tab should support three display states:

1. No filter
2. Filtered by selected track
3. Filtered by selected car

Rules:

- In the no-filter state, both grids show the full game totals.
- In the track-filtered state, the track grid remains fully visible and the car grid is recalculated from rows matching the selected track.
- In the car-filtered state, the car grid remains fully visible and the track grid is recalculated from rows matching the selected car.
- The top track and top car cards should reflect the currently visible data, not always the overall totals.
- The clear action returns the tab to the no-filter state.

## Recommended Implementation

### 1. Extend `GameDistanceTab` with filter-aware state

Add fields or properties to hold:

- the raw per-context `DistanceSummary` rows for the game
- the active selected track key
- the active selected car key
- the visible track summaries
- the visible car summaries
- the selected track row
- the selected car row

This can live directly on `GameDistanceTab` or in a dedicated view-model type if we want cleaner separation from the persistence models.

### 2. Preserve raw rows in the summary snapshot

Update `AffinitySummaryBuilder.BuildSnapshot(...)` so each `GameDistanceTab` keeps the underlying per-game `DistanceSummary` rows in addition to the aggregated lists.

Add helpers that can rebuild:

- track summaries from all rows
- track summaries filtered by car
- car summaries from all rows
- car summaries filtered by track

This keeps all filtering in-memory and avoids changing persistence or querying the database again.

### 3. Make the grids bind to visible collections

In `AffinitySimHub.xaml`:

- bind the track grid to `VisibleTrackSummaries`
- bind the car grid to `VisibleCarSummaries`
- add `SelectedItem` bindings for each grid
- optionally show a short filter status label above the grids

Recommended UI affordance:

- add an always-visible `Clear filter` button near the grid headings

This is preferable to relying on WPF `DataGrid` deselection behavior alone.

### 4. Centralize filter transitions

Add methods on the tab model or view-model such as:

- `ApplyTrackFilter(string trackNameWithConfig)`
- `ApplyCarFilter(string carModel)`
- `ClearFilter()`

Behavior:

- `ApplyTrackFilter(...)` clears any selected car filter before recalculating visible cars
- `ApplyCarFilter(...)` clears any selected track filter before recalculating visible tracks
- `ClearFilter()` restores both visible lists to the full summaries and clears both selections

### 5. Keep refresh semantics simple

`RefreshDistanceSummaries()` currently rebuilds the tabs.

Recommendation:

- default refreshed tabs back to the no-filter state
- do not persist UI filter state between refreshes

This keeps the implementation predictable and avoids restoring a stale selection that may no longer exist after data changes.

## Acceptance Criteria

- Clicking a track filters the car grid to matching cars only.
- Clicking a car filters the track grid to matching tracks only.
- Distance and time values in the filtered grid reflect only the selected track or car context.
- Switching from a track filter to a car filter clears the previous track filter.
- Switching from a car filter to a track filter clears the previous car filter.
- Clicking `Clear filter` restores both grids to their complete unfiltered lists.
- Top cards update to match the active filter and revert after clearing.
- A data refresh leaves the tab in the no-filter state.

## Test Coverage

Add or extend tests for:

- filtering cars by selected track
- filtering tracks by selected car
- recalculated distance and time totals in filtered results
- clearing a track filter
- clearing a car filter
- switching directly from one filter type to the other
- top-card behavior under filtered and cleared states
- empty-state behavior when a selected item has no matching opposite-side rows

## Notes

- This feature should remain UI-only and should not change the persisted data shape.
- Filtering should operate on already loaded summary rows to keep runtime cost low.
- If SimHub selection behavior proves awkward, a button-driven clear path should still make the interaction reliable.
