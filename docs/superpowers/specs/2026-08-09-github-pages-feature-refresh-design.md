# GitHub Pages Feature Refresh Design

Date: 2026-08-09

## Goal

Refresh the static GitHub Pages site so it matches the current Affinity plugin feature set and the updated screenshots already present in `site/assets/screenshots/`.

## Scope

- Preserve the current one-page static site structure and visual direction.
- Update landing-page copy for the current cumulative distance and used-time features.
- Add a compact section describing telemetry-aware tracking protections, including replay and inactive-sample filtering.
- Refresh supported-game and install/release wording for the current plugin state.
- Keep the existing screenshot assets and captions aligned with the new screenshots.

## Content Direction

The page should position Affinity as a long-term SimHub driving-history plugin, not a lap/session analyzer.

Key points to surface:

- Persistent totals by game, car, and track.
- Cumulative distance and driving time.
- Overview highlights for top game, track, and car.
- Per-game drilldowns with period, sort, result-limit, and track/car cross-filter controls.
- Telemetry safeguards for replay, garage, spectator, session reset, and line-wrap edge cases.
- Current supported games: Assetto Corsa, Assetto Corsa EVO, Automobilista 2, iRacing, Le Mans Ultimate, rFactor 2, and RaceRoom Racing Experience.

## Implementation Shape

- Edit `site/index.html` for copy and one new compact informational section.
- Edit `site/styles.css` only if the new section needs a small reusable layout rule.
- Do not redesign the hero or screenshot layout.
- Do not overwrite the modified screenshot PNG files.

## Verification

- Inspect the page source for broken local image/link references.
- Open the static page locally, or otherwise verify the HTML can be rendered without a build step.
- Check `git diff` so only intended site/spec files and the existing screenshot updates are included.
