# Dynamic Pattern Observation Card Widths

## Intent

Make every visible Pattern observation card participate in filling the full Overview row instead of reserving empty slots for cards that were not generated.

## Goals

- Keep all visible observation cards in one row.
- Let one card fill the row, two cards split it equally, and three cards split it equally.
- Preserve the three-observation maximum, 10-pixel gaps, full outer-row alignment, 112-pixel minimum height, text wrapping, styling, bindings, and card order.
- Preserve the weekday/distance chart row and all Driving Patterns calculations.

## Non-goals

- No changes to which observations are generated or their content.
- No changes to charts, persistence, ranges, game tabs, or responsive stacking.

## Constraints

- Use native WPF and the existing `ItemsControl` binding.
- Keep the change presentation-only in `AffinitySimHub.xaml` with focused structure-test coverage.
- Preserve the installed SimHub track map during deployment.

## Approaches

1. **Automatic equal columns (selected):** set the `UniformGrid` to one row and let WPF calculate the number of equal columns from the actual item count. This directly produces 100%, 50/50, or thirds for the builder's one-to-three observations.
2. **Count-driven columns:** bind `Columns` to `Observations.Count` through a converter or new view-model property. This produces the same result with unnecessary code and binding surface.

## Selected Design

Keep `PatternObservationCards` as an `ItemsControl`, change its items panel from a fixed three-column `UniformGrid` to `<UniformGrid Rows="1" />`, and retain the existing margin compensation that aligns the final card with the summary row. Update the structural test to require one row and no fixed column count. No data or behavior code changes.
