# Recent Highlights Period Selector Design

**Goal:** Make the overview `Recent highlights` section configurable with a lightweight dashboard period selector that can switch between weekly and monthly highlight windows without affecting saved settings or other summary surfaces.

**Context**

- The overview currently renders `Recent highlights` from two fixed monthly sections: `This Month` and `Last Month`.
- The current UI lives in [Affinity\AffinitySimHub.xaml](C:\Users\micha\dev\AffinitySimHub\Affinity\AffinitySimHub.xaml) and binds to overview summary state exposed by [Affinity\AffinityPlugin.cs](C:\Users\micha\dev\AffinitySimHub\Affinity\AffinityPlugin.cs).
- Summary snapshots are already built from filtered `DistanceSummary` rows through [Affinity\AffinitySummaryBuilder.cs](C:\Users\micha\dev\AffinitySimHub\Affinity\AffinitySummaryBuilder.cs).
- Game-tab time filters already use local calendar boundaries for monthly periods in `TryGetGameTabTimePeriodUtcRange(...)`, so the overview should stay aligned with that behavior where possible.
- This change is intentionally limited to `Recent highlights`. `Game totals` stays unchanged for now.

**Design**

- Replace the fixed multi-row monthly highlights block with one active `Recent highlights` section driven by a single dropdown in the overview header.
- Add four non-persisted dashboard options:
  - `This week`
  - `Last week`
  - `This month`
  - `Last month`
- Default the selector to `This month` each time the plugin initializes or the overview state is rebuilt.
- Keep the selector view-only:
  - changing it updates only the overview `Recent highlights` section
  - it does not modify `AffinitySettings`
  - it does not affect `Top Overall`
  - it does not affect game-tab filters or any persisted state
- Keep the current three highlight content areas:
  - featured game
  - featured track
  - featured car
- Show only one active highlights row at a time, labeled from the selected period, for example:
  - `This week highlights`
  - `Last month highlights`
- Add a compact secondary date-range label so the active window is explicit, for example:
  - `Jun 29 - Jul 4`
  - `Jun 1 - Jun 30`
- If the selected period has no data, show one section-level empty state such as `No driving history for this period yet` rather than three separate empty cards.

**Period Logic**

- Build the selected highlights snapshot by filtering all `DistanceSummary` rows into the chosen UTC window, then passing the filtered rows into `AffinitySummaryBuilder.BuildSnapshot(...)`.
- Use local calendar semantics, then convert to UTC for filtering:
  - `This week`
    - starts at local midnight on the current culture's first day of the current week
    - ends at the current local timestamp
  - `Last week`
    - starts at local midnight on the first day of the prior week
    - ends at local midnight on the first day of the current week
  - `This month`
    - starts at local midnight on the first day of the current month
    - ends at the current local timestamp
  - `Last month`
    - starts at local midnight on the first day of the prior month
    - ends at local midnight on the first day of the current month
- Weekly periods should use `CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek` rather than hard-coding Sunday or Monday.

**Implementation Shape**

- In [Affinity\AffinityPlugin.cs](C:\Users\micha\dev\AffinitySimHub\Affinity\AffinityPlugin.cs):
  - add a selected recent-highlights period key property
  - expose dropdown options for the overview selector
  - expose one active recent-highlights section instead of the current monthly collection
  - expose a formatted date-range label for the active period
  - add a helper that computes the overview highlight period range and rebuilds the active highlights snapshot
- In [Affinity\AffinityDatabase.cs](C:\Users\micha\dev\AffinitySimHub\Affinity\AffinityDatabase.cs):
  - reuse `GameTabFilterOption` for the selector options unless a tiny dedicated overview-period option reads substantially clearer
  - avoid introducing persistence or behavior unrelated to the overview selector
- In [Affinity\AffinitySimHub.xaml](C:\Users\micha\dev\AffinitySimHub\Affinity\AffinitySimHub.xaml):
  - replace the current `ItemsControl` over monthly summary sections with:
    - a header row containing `Recent highlights` and the period dropdown
    - one active highlight row
    - one section-level empty state
- In [Affinity\AffinitySimHub.xaml.cs](C:\Users\micha\dev\AffinitySimHub\Affinity\AffinitySimHub.xaml.cs):
  - add a selection-changed handler that refreshes only the recent-highlights view state
- Rename overview properties away from `MonthlyTopSummarySections` so the code reflects the new week-or-month behavior more clearly. Prefer a name like `SelectedRecentHighlightsSection`.

**Testing**

- Extend [Affinity.Tests\AffinityOverviewSummaryTests.cs](C:\Users\micha\dev\AffinitySimHub\Affinity.Tests\AffinityOverviewSummaryTests.cs) to verify:
  - the default recent-highlights period is `ThisMonth`
  - changing the selected period updates the active overview section
  - the active period label and date-range label match the selected period
  - empty selected periods surface the section-level empty state cleanly
- Add or extend time-range tests near [Affinity.Tests\GameTabTimePeriodRangeTests.cs](C:\Users\micha\dev\AffinitySimHub\Affinity.Tests\GameTabTimePeriodRangeTests.cs) to verify:
  - `ThisWeek`
  - `LastWeek`
  - `ThisMonth`
  - `LastMonth`
  - local calendar boundaries and UTC conversion behavior
- Keep existing overview and game-tab tests passing to confirm that:
  - all-time featured summaries still behave the same
  - game-tab filters remain independent from the overview selector

**Risks**

- Weekly boundaries can surprise users if the first day of week is implicit, so the design intentionally ties it to current culture and makes the active date range visible.
- Replacing the monthly collection with one active section touches overview bindings and test assumptions, so the refactor should stay narrow and avoid changing unrelated summary logic.
- Empty recent periods are more likely with weekly windows, so the section-level empty state needs explicit coverage.
