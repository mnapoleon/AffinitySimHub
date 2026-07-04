# Recent Highlights Paired Range Design

**Goal:** Update the overview `Recent highlights` section so the user chooses only `Week` or `Month`, and the overview always shows both the current and previous period together in stacked blue-outlined cards.

**Context**

- The current branch implementation added a four-choice overview selector for:
  - `This week`
  - `Last week`
  - `This month`
  - `Last month`
- The user wants comparison built into the section instead:
  - choose `Week` or `Month`
  - always show both `this` and `last`
- The current overview implementation already has:
  - recent-highlights range helper logic in `AffinityPlugin`
  - overview WPF bindings for recent-highlights cards
  - tests covering the initial four-choice behavior
- This is a focused follow-up to the recent-highlights feature. `Game totals` remains out of scope.

**Design**

- Replace the four-value recent-highlights selector with a two-value range selector:
  - `Week`
  - `Month`
- When `Week` is selected, render two stacked cards:
  - `This week highlights`
  - `Last week highlights`
- When `Month` is selected, render two stacked cards:
  - `This month highlights`
  - `Last month highlights`
- Keep the cards vertically stacked with the current period first and the previous period second.
- Keep the three-column summary content inside each card:
  - featured game
  - featured track
  - featured car
- Add a subtle blue outline around both cards so the section reads as a distinct feature block and visually matches the stronger featured-card styling elsewhere in the overview.
- Keep the top card slightly more visually prominent than the lower card if needed, but both cards should clearly share the same blue-outlined visual family.

**Behavior**

- The selector remains dashboard-only and non-persisted.
- Changing the selector updates only the `Recent highlights` section.
- The selector no longer asks the user to choose among four discrete periods.
- The comparison is implicit:
  - `Week` means current week versus previous week
  - `Month` means current month versus previous month
- Each card shows its own label and date range, for example:
  - `This week highlights`
  - `Jun 29 - Jul 4`
  - `Last week highlights`
  - `Jun 22 - Jun 28`
- If one card has data and the other does not:
  - render the populated card normally
  - render the empty card with a compact empty state in the same outlined card shell
- If both cards are empty:
  - render both empty cards rather than collapsing the section
  - this preserves the paired-comparison structure and makes the selected range understandable

**Data Model**

- Replace the existing four-choice period key with a two-choice range key:
  - `Week`
  - `Month`
- Replace the single selected recent-highlights section with paired overview state:
  - `CurrentRecentHighlightsSection`
  - `PreviousRecentHighlightsSection`
  - `CurrentRecentHighlightsDateRangeDisplay`
  - `PreviousRecentHighlightsDateRangeDisplay`
- Derive both snapshots from the selected range key:
  - `Week`
    - current snapshot = `This week`
    - previous snapshot = `Last week`
  - `Month`
    - current snapshot = `This month`
    - previous snapshot = `Last month`

**Period Logic**

- Keep local calendar semantics:
  - current week starts at local midnight on the culture's first day of the current week and ends at the current local timestamp
  - last week is the previous full local calendar week
  - current month starts at local midnight on the first day of the current month and ends at the current local timestamp
  - last month is the previous full local calendar month
- Continue to convert local boundaries to UTC before querying or filtering `DistanceSummary` rows.
- Weekly logic should continue using `CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek`.

**Implementation Shape**

- In `AffinityPlugin.cs`:
  - replace the four recent-highlights period constants/options with two range constants/options
  - replace `SelectedRecentHighlightsSection` with paired current/previous recent-highlights sections
  - replace the single selected recent-highlights date-range label with current/previous labels
  - update recent-highlights snapshot construction so one selected range computes both cards together
- In `AffinitySimHub.xaml`:
  - rename the selector label from `Period` to `Range`
  - replace the single recent-highlights card with two stacked outlined cards
  - ensure both cards use the blue border treatment
  - render per-card empty states when necessary
- In `AffinitySimHub.xaml.cs`:
  - keep the selector event simple and refresh overview recent-highlights state only
- In tests:
  - remove assumptions tied to the four-choice selector
  - add explicit expectations for paired `current` and `previous` cards

**Testing**

- Update overview tests to verify:
  - the default selector is `Month`
  - `Week` shows current and previous week sections
  - `Month` shows current and previous month sections
  - each card gets the correct header and date-range label
  - one empty card does not collapse the other populated card
  - both empty cards still render predictable empty-state text
- Update range tests to verify:
  - the selected range key maps to the correct current and previous period windows
  - week and month boundary logic remains correct in local time
- Run the full test suite and the plugin build again after the follow-up change.

**Risks**

- This change replaces the just-added four-choice selector model, so tests and bindings need to be updated consistently rather than patched in place.
- Showing both cards at all times adds vertical weight, so the stacked cards need disciplined spacing and restrained border styling.
- Per-card empty states need explicit coverage to avoid null-binding regressions when only one side has data.
