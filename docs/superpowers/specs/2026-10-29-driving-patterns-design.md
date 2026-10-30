# Driving Patterns Design

**Goal:** Replace the Overview page's Recent Highlights and game-total cards with a useful Driving Patterns section, while moving each game's cumulative total card into that game's own tab.

## Intent

Affinity already answers what the driver has accumulated across games, cars, and tracks. This change should help the driver recognize habits and changes in that history: when they tend to drive, where their distance goes, what a typical session looks like, and how the current period compares with the preceding matching period.

The feature should work as a glanceable story first and an analytical view second. Plain-language observations appear above a small dashboard of supporting charts. Both views use the same selected period and the same underlying finalized session records.

## Goals

- Add Driving Patterns to the existing Overview page.
- Show short, deterministic observations followed by supporting charts.
- Offer `Last 30 days`, `Last 90 days`, and `This year` ranges.
- Compare each selected range with its matching preceding period.
- Move each game's cumulative distance/time card from Overview to the top of that game's tab.
- Remove Recent Highlights and its range selector completely.
- Keep all pattern work out of `DataUpdate()` and other telemetry hot paths.
- Avoid a database migration by deriving patterns from the existing `sessions` table.
- Suppress weak conclusions when the selected range has too little trustworthy session history.

## Non-goals

- Goals, achievements, streak rewards, or notifications.
- Lap-time, race-result, incident, fuel, or setup analysis; Affinity does not store those facts.
- New telemetry sampling or new writes during live driving.
- Editing or deleting historical sessions.
- Persisting the selected pattern range as a user setting.
- Treating legacy imported totals as genuine dated sessions.

## Anti-goals

- Do not turn Overview into a dense general-purpose analytics product.
- Do not make human-sounding claims from one or two sessions.
- Do not add expensive database or file work to the runtime telemetry loop.
- Do not change existing cumulative totals or persisted session identities.

## Agreed UI Shape

The Overview retains its existing all-time featured game, track, and car cards. Below those cards, it gains one `Driving patterns` section with a shared range selector.

The first row is `Pattern observations`, containing up to three concise cards:

- a concentration or mainstay observation, such as a track accounting for a meaningful share of distance;
- a schedule observation, such as a clearly preferred local weekday or time band;
- a change observation, such as more sessions paired with a shorter typical session.

The second row is `Patterns dashboard`, containing:

- `When you drive`: time driven grouped by local day of week;
- `Where your distance went`: the top three tracks by distance plus `Other`;
- `Compared with the previous period`: total distance, finalized-session count, and median session duration, each with its change from the matching prior window.

Pattern identities remain game-scoped. The same apparent track or car name reported by two simulators is not merged into one pattern because Affinity preserves raw per-game persistence identities and different simulators can use similar labels for meaningfully different content. Track and car observations include the game name when needed for clarity.

The layout follows the existing Affinity/SimHub dark panels, cyan accents, spacing, and card conventions. It must reflow without horizontal clipping at the current control's narrower widths.

The existing `Recent highlights` section, its selector, its view state, and its event handler are removed. The Overview `Game totals` list is also removed. Each `AffinityGameTab` instead displays its existing total distance and total used time in a summary card at the top of that game's content, ahead of the top-track and top-car cards.

## Ranges and Comparisons

The selector is view-only and defaults to `Last 30 days` whenever the plugin initializes. Its options are:

- `Last 30 days`: a rolling local-time window ending now; compare with the immediately preceding 30 days.
- `Last 90 days`: a rolling local-time window ending now; compare with the immediately preceding 90 days.
- `This year`: local January 1 through now; compare with January 1 of the previous year through the equivalent local calendar date and time in that year. Leap-day conversion clamps to the last valid day when necessary.

Local range boundaries are converted to UTC before repository queries. Session start time determines window membership, matching the existing summary query convention.

Session start UTC is converted to local time for weekday and time-band grouping. Weekday display follows the current culture. Time bands are fixed, understandable local ranges: overnight (midnight–6 AM), morning (6 AM–noon), afternoon (noon–6 PM), and evening (6 PM–midnight).

## Trust and Empty States

Pattern queries exclude imported legacy rows whose `session_uid` begins with `legacy-`, because those rows were assigned a migration date rather than their original driving date.

At least five non-legacy finalized sessions must fall inside the selected current range before observations or charts are shown. Below that threshold, the section shows one friendly empty state explaining that more completed sessions are needed.

Current-period patterns can still appear when the prior period lacks data. Comparisons require at least three prior-period sessions; otherwise comparison deltas are labeled as unavailable instead of being inferred from an unstable baseline.

Median, rather than mean, defines `typical session` so an unusually long outing does not dominate the result. Only the persisted `time_driven_seconds` value is used for session duration.

Comparison formatting follows the metric rather than forcing one unit everywhere:

- distance shows percentage change, with the current distance already visible as the primary value;
- session count shows the signed absolute session difference;
- typical duration shows the signed absolute time difference in minutes;
- when the prior metric is zero, percentage change is unavailable rather than displayed as an infinite or misleading percentage.

## Observation Selection

Observations are selected deterministically from calculated pattern facts; no generated text or external service is involved.

- Concentration is eligible when the leading track, car, game, or car/track combination has a meaningful share of current-period distance. Prefer the most specific useful fact without repeating a chart label verbatim.
- Schedule is eligible when a weekday or time band represents a clearly dominant share of current-period driven time.
- Change is eligible only when the comparison window meets its minimum session threshold and the absolute change is large enough to be meaningful.
- Variety may replace an ineligible card and reports distinct cars or tracks compared with the prior period.
- Show at most three observations. If fewer than three facts meet their thresholds, show fewer cards rather than filler.

Exact presentation thresholds and tie-breaking constants are left to the builder, but they must be named, deterministic, and covered by focused tests. Ties should resolve stably by display name after the primary metric.

## Data and Architecture

### Selected approach

Add a repository method that reads lightweight, non-legacy session records for a supplied UTC range. A dedicated pure builder converts those records into a pattern snapshot used by the plugin and UI.

This is preferred over calculating every pattern in SQLite because median, observation selection, comparison rules, and local-time grouping remain cohesive and straightforward to unit test. Pattern rows are loaded only during UI refresh or range changes, not during live telemetry processing. Personal Affinity databases are expected to make these bounded queries modest; the existing `sessions_started` index supports the range filter.

### Alternative considered

An all-SQL implementation would minimize rows materialized in memory and scale better for unusually large histories. It was not selected because it would spread presentation policy across complex aggregate queries, make median calculations awkward on the bundled SQLite surface, and reduce deterministic unit-test coverage of the rules.

### Proposed model boundaries

- `AffinitySqliteRepository` returns a lightweight record containing session UID or trust classification, game, car, displayed track identity, start/end UTC, distance meters, and driven seconds.
- A dedicated `AffinityPatternBuilder` accepts current rows, comparison rows, the local time zone/culture context needed for grouping, and the current unit preference.
- The builder returns an `AffinityPatternSnapshot` containing availability state, observation cards, weekday buckets, track-share slices, and comparison metrics.
- `AffinityPlugin` owns the selected range, computes local/UTC windows, obtains rows, invokes the builder, exposes bindable state, and raises the same UI refresh notifications used by existing summary surfaces.
- `AffinitySimHub.xaml.cs` handles only the range-selection event and delegates refresh to the plugin.
- `AffinitySimHub.xaml` renders the snapshot without performing analytical calculations in bindings or converters.

No persisted schema or settings shape changes.

## Refresh and Performance

- Build the pattern snapshot during normal summary/UI refresh and when the pattern range changes.
- Never query pattern history or rebuild charts per telemetry sample.
- Query one combined UTC span covering both current and comparison windows when practical, then partition rows in memory. Two bounded queries are also acceptable if clearer and verified fast.
- Reuse the last snapshot until a normal summary refresh or selection change invalidates it.
- Keep observable collection replacement and tab refresh behavior consistent with existing tests so game-tab selection is not reset unnecessarily.

## Testing

Add focused tests for:

- repository range filtering and exclusion of `legacy-` session rows;
- Last 30 days, Last 90 days, and This year local boundaries and matching comparison windows, including year/leap-day behavior;
- the five-session current threshold and three-session comparison threshold;
- median session duration for odd and even counts;
- local weekday and time-band grouping;
- top-three-track plus Other distance shares;
- stable observation eligibility, ordering, fallback, and tie-breaking;
- missing comparison data without suppressing valid current patterns;
- unit-aware distance display;
- removal of Recent Highlights state and preservation of all-time featured summaries;
- game total cards appearing in their corresponding game-tab model/bindings;
- game-tab selection and refresh remaining stable after the layout change.

Run the full test project and the plugin build serially. Because runtime/UI behavior changes, copy the validated six-file runtime manifest into SimHub using the map-safe workflow if the files are not locked. Do not copy `ac_track_id_map.json`.

## Risks and Mitigations

- **Misleading imported history:** explicitly exclude legacy synthetic sessions.
- **Time-zone surprises:** derive ranges in local time, convert boundaries to UTC, and group starts in local time.
- **Sparse history:** enforce minimum session thresholds and show a single honest empty state.
- **Overview becoming too long:** remove Recent Highlights and Overview game totals, limiting observations to three and charts to three compact views.
- **Refresh cost:** use indexed, bounded session reads only on summary refreshes and range changes.
- **WPF tab regressions:** preserve existing tab instances/selection patterns and extend the current tab refresh tests.

## Builder Calls

The builder may choose concrete class/property names, chart rendering details that fit existing WPF conventions, and exact named observation thresholds. Those choices must stay within the boundaries above and must not change the agreed content, range semantics, trust rules, or hot-path constraints.
