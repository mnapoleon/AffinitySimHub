# Driving Pattern Layout Refinement

## Intent

Make the Driving Patterns Overview easier to scan by turning the three observations into compact cards and grouping the two supporting charts into one balanced dashboard row.

## Goals

- Show up to three Pattern observations as taller, narrower cards in one three-column row.
- Place When you drive and Where your distance went beside each other in a single row.
- Use the selected Balanced proportions: about 42% of the row for weekday activity and 58% for the distance donut and legend.
- Preserve readable weekday names, durations, donut labels, and track distances.
- Keep the existing dark visual style, bindings, empty state, range selector, comparison cards, and chart behavior.
- Allow both rows to wrap cleanly when the available width cannot support their normal layout.

## Non-goals

- No changes to pattern calculations, persistence, range semantics, chart data, or comparison cards.
- No new chart controls or external UI dependencies.
- No redesign of game tabs or the Overview totals above Driving Patterns.

## Constraints

- Continue using native WPF and existing Affinity view models.
- Keep layout changes in the Overview XAML unless a small presentation-only helper is required.
- Preserve the live SimHub track map during validation and deployment.
- Verify the intended grouping and bindings with focused structure tests, then run the full test suite and plugin build serially.

## Approaches Considered

1. **Balanced (selected):** three equal observation cards, followed by a roughly 42/58 weekday-to-distance chart row. This retains full weekday labels while giving the donut legend more room.
2. **Distance-led:** a roughly 34/66 split. This favors long track names but compresses weekday labels too aggressively.
3. **Equal split:** a 50/50 chart row. This is visually symmetrical but gives the donut legend less useful space than its content needs.

## Selected Design

Use a wrapping three-card observation row with consistent card height and equal normal widths. Below it, use a wrapping two-panel chart row: a compact weekday panel at approximately 42% and a distance panel at approximately 58%. At narrower widths, cards and chart panels may wrap rather than clip or force horizontal scrolling.
