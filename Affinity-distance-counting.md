# Affinity Distance And Time Tracking

This note explains how `Affinity` currently tracks cumulative distance and cumulative time, and where the distance behavior differs by game.

## Scope

Affinity stores cumulative totals by:

- `game`
- `car`
- `track`

Internally, the active bucket key is:

- `gameName | carModel | trackNameWithConfig`

That means the live context total at the top of the plugin is for the current `game / car / track` combination only.

## What Affinity Stores

Each active `game / car / track` bucket stores:

- cumulative distance
- cumulative used time
- created and last-updated timestamps

Affinity does not store lap totals anymore.

## Distance Tracking

Affinity tracks a per-session distance baseline and then adds only positive session-distance deltas into the current bucket.

High-level flow:

1. Choose a session distance source for the session.
2. Establish a session origin.
3. On each update, compute `sessionMeters = absoluteSessionMeters - origin`.
4. Add only positive deltas to the bucket.

## Time Tracking

Affinity also accumulates cumulative used time for the active bucket.

High-level flow:

1. When a valid session starts, Affinity stores the current UTC sample time.
2. On each later telemetry update, it measures wall-clock elapsed time since the previous sample.
3. If the elapsed time is positive and small enough to look like normal live telemetry, Affinity adds it to the active bucket's `UsedTime`.
4. On session changes, context changes, disabled state, or shutdown, pending time is flushed to disk.

Important details:

- Time is cumulative across sessions for each `game / car / track` bucket.
- Time is inferred from telemetry update cadence, not from `CreatedUtc` / `LastUpdatedUtc`.
- Very large telemetry gaps are ignored so sleep, pauses, or missing telemetry do not create huge false time jumps.

## Distance Sources

Affinity can use one of these sources:

- `Derived`
- `SessionOdoMeters`
- `SessionOdoKilometers`

### Derived

Derived distance is computed from lap count and track position:

- `CompletedLaps * TrackLength + TrackPositionWithinLap`

If `TrackPositionMeters` is already larger than one lap length, it is treated as an already-cumulative value.

Even though Affinity no longer stores lap totals, it still uses the sim's lap counter internally when a game's telemetry makes that necessary for distance reconstruction or telemetry guards.

### SessionOdoMeters

Uses the raw `SessionOdo` value directly as meters.

### SessionOdoKilometers

Uses `SessionOdo * 1000`.

## Game-Specific Rules

### Assetto Corsa

Games matched:

- `assettocorsa`

Distance source:

- always `Derived`

Why:

- The SimHub-exposed `SessionOdo` value was not reliable as a clean session odometer.

Distance model:

- uses stateful forward track-position accumulation instead of `CompletedLaps * TrackLength + TrackPosition`

Why:

- This avoids undercounting when AC starts the session near the timing line and wraps to `0` before the lap counter catches up.
- It also keeps the saved JSON total aligned with the live session distance instead of relying on lap-counter timing.

Extra guards:

- ignores obvious startup telemetry snaps when the car is effectively stationary

### Assetto Corsa EVO

Games matched:

- `assettocorsaevo`

Distance source:

- always `Derived`

Distance model:

- uses the same stateful forward track-position accumulation model as classic Assetto Corsa

Behavior is intended to mirror classic Assetto Corsa.

### RaceRoom / R3E / RRRE

Games matched:

- `raceroomracingexperience`
- `r3e`
- `rrre`

Distance source:

- always `Derived`

Why:

- The SimHub `SessionOdo` field was observed to scale in a way that caused wildly inflated totals.

Extra guards:

- derived line-wrap guard at the start/finish line

Why:

- In RaceRoom, `TrackPosition` can wrap to near zero one frame before the sim's lap counter increments.
- Without a guard, that can look like a session reset followed by an extra full-lap jump, which double-counts distance.

### Automobilista 2

Games matched:

- `automobilista2`

Distance source:

- always `Derived`

Why:

- The generic source auto-selection could choose a bad `SessionOdo` interpretation and inflate totals into thousands of kilometers.

Session origin:

- not based on a fixed lap-derived starting point
- distance is integrated from forward track-position movement across line wraps

Why:

- AMS2 can start partway around the circuit in a real pit stall location, and its lap counter can lag relative to line crossings.
- Affinity therefore tracks forward path traveled from successive track-position updates instead of depending on lap-counter timing to build cumulative distance.

Extra guards:

- no AMS2-specific wrap or startup guard yet

Why:

- The AMS2 path model handles legitimate pit starts and line crossings by integrating forward movement across wraps instead of using AC-style zero-origin handling.

### iRacing

Games matched:

- `iracing`

Distance source:

- always `Derived`

Why:

- iRacing's SimHub `SessionOdo` values were not trustworthy as a per-session distance source.
- Its lap semantics can also lag the real line crossing for pit and out-lap scenarios, which makes the generic lap-count-derived formula undercount or reset incorrectly.

Session origin:

- not based on a fixed lap-derived starting point
- distance is integrated from forward track-position movement across line wraps

Why:

- iRacing can start the car partway around the circuit at pit exit and may keep `CompletedLaps = 0` across the out lap.
- Affinity therefore measures actual forward path traveled from successive track-position updates instead of reconstructing session distance from lap count.

Extra guards:

- ignores brief iRacing-only zeroed telemetry drops after progress has already been recorded

Why:

- iRacing can briefly report `0` laps and `0` position without a real session restart.
- Without a guard, that transient reset can double-count distance when telemetry snaps back a frame later.

### rFactor 2

Games matched:

- `rfactor2`

Distance source:

- always `Derived`

Why:

- rFactor 2's `SessionOdo` did not behave like a trustworthy session-distance source.
- Its pit and garage exit flow can also oscillate `TrackPosition` around the timing line before the session counters settle.

Session origin:

- not based on a fixed lap-derived starting point
- distance is integrated from forward track-position movement across line wraps

Why:

- rFactor 2 can start near the timing line and then move through pit and garage areas in a way that makes the generic lap-count-derived formula noisy.
- Affinity therefore measures forward path traveled from successive track-position updates instead of reconstructing cumulative distance from lap count.

Extra guards:

- ignores low-speed track-position wraps near the timing line while leaving the pit and garage area
- ignores near-stationary line transitions at the timing line

Why:

- rFactor 2 can bounce between just-before-line and just-after-line positions at low speed before the real telemetry flow stabilizes.
- It can also report extra counter changes when the car is effectively stopped on the line at the end of a run.

### Other Games

For other games, Affinity currently auto-selects a source at session start:

- compare `Derived`
- compare raw `SessionOdo` as meters
- compare `SessionOdo * 1000`
- lock the closest plausible source for the rest of the session

This is a heuristic and may need game-specific overrides if a title exposes ambiguous telemetry.

## Session Start Behavior

At session start, Affinity records:

- current session id
- active bucket key
- chosen distance source
- session origin
- current telemetry sample time

For derived-source sessions that still use the simple lap-count formula, the origin may be intentionally forced to `0`.

AC/ACE no longer rely on that zero-origin behavior, because they now use the stateful forward track-position model instead.

For other games, the origin is the chosen absolute session distance at the time the session begins.

## Reset And Wrap Handling

Affinity has a few protections for bad telemetry transitions:

- startup position snap guard
  - ignores large initial jumps while stationary
- session distance reset handling
  - if session distance drops materially, Affinity updates its local baseline instead of adding negative distance
- derived line-wrap guard
  - for derived-source sims, if track position wraps by about one lap before the sim's counters settle, Affinity waits for telemetry sync instead of treating that wrap as a real reset

## What The UI Shows

### Top Status Area

Top values are current live context values:

- current total distance for the active `game / car / track`
- current session distance
- cumulative total time for the active `game / car / track`

### Game Tabs

Each game tab is cumulative for that game.

### Track Table

The track table is grouped by track only, across all cars in that game.

That means:

- running the same track in a different car increases the same track row

### Car Table

The car table is grouped by car only, across all tracks in that game.

## Practical Implications

- Affinity is cumulative, not per-run.
- If you want to validate one clean test, clear the stored data first.
- Time totals represent observed active telemetry duration for that `game / car / track` context.
- Raw distance semantics can still differ by sim, especially around pit starts, pit exits, and line-wrap behavior.

## Debug Logging

When enabled in the current build, targeted debug logging writes to:

- `C:\Program Files (x86)\SimHub\PluginsData\Common\Affinity.distance.debug.log`

The debug log is especially useful for:

- `RaceRoom`
- `AssettoCorsaEVO`
- `Automobilista2`

It records:

- selected distance source
- origin
- raw `SessionOdo`
- `TrackPositionMeters`
- `TrackPositionPercent`
- `TrackLength`
- derived session meters
- session deltas
- counter deltas used by telemetry guards
- reset and wrap events
