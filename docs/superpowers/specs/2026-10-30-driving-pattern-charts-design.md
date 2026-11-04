# Driving Pattern Charts Visual Correction

## Intent

Bring the live Affinity Overview in line with the mockup the user approved. The current implementation has simplified vertical and horizontal progress bars, but it omitted the mockup's donut visualization and comparison sparklines. This is a visual-correction change, not a change to tracking or pattern eligibility.

## Goals

- Replace the `Where your distance went` progress-bar list with a donut chart showing the existing top-three game-scoped track shares plus `Other`.
- Keep the share legend beside the donut with track/game label, percentage, and distance.
- Add a compact sparkline to each comparison card: distance, sessions, and median session duration.
- Build eight equal time buckets across the selected current period for each sparkline.
- Preserve the existing observation cards, weekday chart, range selector, five-session threshold, comparison text, colors, and SimHub dark theme.
- Keep all chart data calculation in explicit pattern-summary refreshes, never `DataUpdate()` or telemetry-triggered refreshes.

## Non-goals

- No third-party charting package.
- No database or settings migration.
- No new telemetry data or write path.
- No animation, hover-only information, zooming, or chart configuration UI.
- No change to which sessions qualify for patterns.

## Approaches

### Selected: lightweight native WPF controls

Add focused WPF `FrameworkElement` controls that render with `DrawingContext`: one donut control consuming the existing track-share collection and one sparkline control consuming normalized numeric points. This has no runtime dependency, fits the existing plugin package, and keeps drawing behavior directly testable through its geometry helpers.

### Rejected: third-party chart library

A chart library would provide more features than required while expanding the release payload and creating .NET Framework/SimHub compatibility risk.

### Rejected: XAML-only geometry converters

Multi-binding cumulative angles and responsive line geometry would spread chart calculations across converters and markup, making behavior more brittle and harder to test.

## Design

- `AffinityPatternBuilder.Build(...)` receives the selected current UTC range boundaries and partitions current sessions into eight half-open buckets.
- Each bucket contains total distance, session count, and median driven duration.
- Every sparkline series includes all eight buckets, including zero-value buckets, and exposes raw values. The control normalizes points to its actual render size; a constant series is centered vertically rather than disappearing.
- The donut uses the existing `AffinityTrackShare.SharePercent` values in stable top-three-plus-Other order. Segments use a restrained four-color palette compatible with Affinity's cyan theme. A central label shows the leading share percentage and abbreviated track label.
- Both controls render useful empty states without throwing when collections are null or empty.
- Chart controls have accessible labels supplied by their surrounding visible headings and value text; no essential information exists only in graphics.
- At narrow widths, the donut and legend stack vertically. Comparison cards continue wrapping as they do today.

## Testing and Validation

- Add builder tests proving eight bucket boundaries, zero-filled buckets, distance/session/median values, and half-open endpoint behavior.
- Add chart-helper tests for donut angle totals, stable segment order, normalized sparkline points, constant-series centering, and empty input.
- Extend UI structure tests for donut and sparkline bindings and removal of the simplified track-share progress bars.
- Run focused tests, the full test project, and the plugin build serially.
- Copy the validated six-file runtime manifest into SimHub without copying `ac_track_id_map.json`, then inspect the live Affinity Overview.
