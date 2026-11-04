# Equal-Width Overview Card Rows

## Intent

Make every card row on the Affinity Overview consume the complete available width and divide that width equally among the cards present in that row.

## Goals

- Keep Top Overall and Pattern observations at their existing full-row equal widths.
- Stretch the two chart cards to equal halves of the full row.
- Stretch the three comparison cards to equal thirds of the full row.
- Derive each row's equal columns from its actual card count so future additions or removals redistribute automatically.
- Preserve the existing gaps, outer-edge alignment, card heights, wrapping, styling, bindings, charts, and calculations.

## Non-goals

- No changes to observation generation, comparison values, chart data, persistence, ranges, or game tabs.
- No responsive stacking or different narrow-window layout in this change.

## Constraints

- Use native WPF controls and existing bindings; add no converter or view-model code.
- Keep the change presentation-only in `AffinitySimHub.xaml` with focused structure-test coverage.
- Preserve the installed SimHub track map during deployment.

## Approaches

1. **Dynamic equal-width rows (selected):** use a one-row `UniformGrid` for the chart and comparison sections, letting WPF derive equal columns from the number of child cards. This matches the already-correct Pattern observation behavior and adapts automatically.
2. **Explicit star-column grids:** define two star columns for charts and three for comparisons. This would match today's card counts but would require manual changes if cards are added or removed.

## Selected Design

Replace `PatternChartRow` and the comparison-card `WrapPanel` with one-row `UniformGrid` panels. Remove the fixed chart widths of `430` and `600` and comparison widths of `220`. Give every card the same 10-pixel right margin and compensate once at the row level with a negative 10-pixel right margin, so visible card widths remain equal while the final card aligns with the Top Overall row. Keep all content and behavior unchanged.
