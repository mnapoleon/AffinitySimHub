# Full-Width Pattern Observation Cards

## Intent

Make the three Pattern observation cards collectively fill the same available row width as the existing Top Overall cards, removing the unused space caused by fixed 360-pixel card widths.

## Goals

- Keep three observation cards across one row.
- Give each card an equal share of the available width, matching the Top Overall three-column layout.
- Preserve the taller `112`-pixel minimum height, text wrapping, card styling, bindings, and order.
- Preserve the existing weekday/distance chart row and all Driving Patterns behavior.

## Non-goals

- No changes to pattern calculations, observation content, charts, persistence, ranges, or game tabs.
- No responsive switch to two or one observation columns.

## Constraints

- Use native WPF and the existing `ItemsControl` data binding.
- Keep the change presentation-only in `AffinitySimHub.xaml` with focused structure-test coverage.
- Preserve the live SimHub track map during deployment.

## Approaches Considered

1. **Equal three-column row (selected):** use a three-column `UniformGrid` and remove the fixed card width. This directly matches the Top Overall row and fills the available width.
2. **Responsive wrapping row:** calculate one-third widths at normal size and switch column counts at narrow sizes. This adds layout logic and does not match the older cards exactly.

## Selected Design

Keep `PatternObservationCards` as an `ItemsControl`, replace its `WrapPanel` items panel with a three-column `UniformGrid`, and remove `Width="360"` from the observation border. Retain `MinHeight="112"` and use item-container spacing so the three equal cards have gaps without leaving a trailing fixed-width layout.
