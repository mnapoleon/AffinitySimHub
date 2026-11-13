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

The stored identities remain the telemetry-provided game, car, track, and track-with-configuration strings after the existing trim and missing-value fallback. Resolving a game profile does not replace those values with a profile settings key, display name, or mapped track label.

## Ownership Boundary

`AffinityGameProfileRegistry.SupportedProfiles` is the catalog of the nine shipped simulators. Each immutable `IAffinityGameProfile` owns its simulator's aliases and metadata, display rules, telemetry classification, distance capabilities, and anomaly decisions. `AffinityGameProfileBase` provides shared defaults, including stateful derived distance and the call to shared replay detection.

`AffinityPlugin` owns all mutable work: session and distance state, the lifetime and reset of AMS2's learned participant index, bucket updates, persistence, logging and other file I/O, published properties, WPF/UI refresh, and plugin lifecycle. Profiles inspect value-context snapshots and return decisions. The AMS2 telemetry rule may update the learned index through plugin-owned runtime state passed in its context, but the plugin still owns and resets that state. Profiles do not persist data, write logs, perform I/O, or update the UI.

## Distance Tracking

Every shipped supported profile selects `StatefulDerived` distance. Affinity reconstructs the forward path from successive positions within the lap, adjusts real start/finish wraps by one track length, and adds only positive session-distance deltas to the current bucket. The lap counter remains available for profile decisions and RaceRoom's guarded distance floor, but it is not stored as a cumulative total.

High-level flow:

1. Resolve the profile and classify the sample before integration.
2. Establish plugin-owned session, origin, track-position, and lap-counter state.
3. Update stateful derived distance, using profile capabilities and line-wrap decisions.
4. Ask the profile about lap anomalies at the appropriate pre- and post-distance stages.
5. Add only an accepted positive delta to the bucket.

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
- Finalized sessions are persisted only after at least `1 meter` of distance was recorded, so stationary pit, monitor, or replay time does not create a zero-distance row even if telemetry time accumulated.
- Very large telemetry gaps are ignored so sleep, pauses, or missing telemetry do not create huge false time jumps.

## Inactive And Replay Telemetry Filtering

Before distance and time integration, Affinity filters telemetry samples that should not represent new driving.

Ignored samples include:

- replay telemetry detected by the shared probes listed below
- game-specific inactive telemetry such as garage or spectator state
- game-specific raw-view states where the simulator keeps emitting telemetry for a watched car or replay camera while the player's race session is still open

When a filtered sample arrives, Affinity finalizes and resets any pending active session before normal tracking continues. Debug logs report generic replay detection as `replay-ignored` and game-specific inactive or raw replay detection as `inactive-ignored`.

`AffinityReplayDetector`, called by the base profile, currently checks only these reflection-based signals:

- `GameData.IsGameReplay`
- `GameData.GameReplay`
- `GameData.ReplayMode`
- status `IsGameReplay`
- status `ReplayMode`
- raw status `IsReplayPlaying`
- nested raw `Telemetry.IsReplayPlaying`

These probes are shared by all resolved profiles, including the unsupported fallback. They are the implemented classification signals, not a guarantee that every simulator or every replay mode exposes one of them. AMS2, RaceRoom, and LMU add only the concrete classifications documented below.

## Shipped Profile Rules

Shared rules for all nine supported profiles:

- stateful derived distance
- shared replay classification
- forward-only delta accumulation and reset/wrap protections orchestrated by the plugin
- raw persisted identities; profile display values are transient

The engine still contains internal automatic and raw-`SessionOdo` source machinery for compatibility and focused tests, but no shipped supported profile selects it. Unknown games resolve to an unsupported fallback, are rejected before session distance begins, and are not automatically tracked.

### Ownership matrix

| Profile | Telemetry and display policy | Distance capability or anomaly decision |
| --- | --- | --- |
| Assetto Corsa (`assettocorsa`) | Uses the plugin-loaded classic AC track map; duplicates the full track display into both circuit columns. | Shared stateful behavior; no profile-specific anomaly decision. |
| Assetto Corsa Competizione (`assettocorsacompetizione`) | Duplicates the full track display into both circuit columns; can promote a compact track code to a longer descriptive context in the same session. | Shared stateful behavior; no profile-specific anomaly decision. |
| Assetto Corsa EVO (`assettocorsaevo`) | Uses default circuit/layout splitting and does not use the classic AC track map. | Shared stateful behavior; no profile-specific anomaly decision. |
| Automobilista 2 (`automobilista2`) | After shared replay checks, rejects garage, spectator, raw `mGameState=6`, and a raw `mViewedParticipantIndex` that differs from the plugin-owned learned player index. | Captures the session-start position and accepts the simulator's initial-position movement instead of treating it as a false snap. |
| iRacing (`iracing`) | Uses shared replay checks, including the raw and nested `IsReplayPlaying` probes; splits on `-` and title-cases the circuit name while preserving `GP`. | Ignores a transient stopped-car reset to zero laps and zero position after material progress. |
| Le Mans Ultimate (`lmu`) | Waits for reliable car/track context instead of tracking missing or `Unknown` values; duplicates the full track display into both circuit columns. `Le Mans Ultimate` is logo-only and does not broaden runtime matching beyond `LMU`. | Ignores qualifying low-speed exit-line lap increments and placeholder session starts after exit/reset telemetry. |
| Project Motor Racing (`projectmotorracing`) | Uses shared telemetry and default display behavior. | Captures the session-start position, uses the stationary startup anchor, and accepts initial-position movement. |
| rFactor 2 (`rfactor2`) | Splits circuit/layout display on `--`. | Ignores qualifying low-speed false line wraps and near-stationary false lap increments. |
| RaceRoom Racing Experience (`raceroomracingexperience`, `r3e`, `rrre`) | After shared replay checks, rejects raw finished and in-garage telemetry. Raw `Player.GameSimulationTicks` is used only to validate the debug speed estimate against fresh frames. | Uses a segment-relative lap/position baseline and applies the lap-counter distance floor only for a new counter increment. A garage departure therefore cannot inherit completed laps at departure or at its next line crossing, including when the carried count is zero. On the `Nordschleife-NLS` layout, a counter floor more than 10% of the layout ahead of physically observed position movement disables the floor only for that segment; normal position integration continues, including the following full lap. |

All alias matching removes non-alphanumeric characters and ignores case. Profile settings keys are also used for per-game debug settings and log filenames, but do not replace stored game names.

### Lap-decision timing

LMU and rFactor 2 implement `ShouldIgnoreLapIncrement`. The plugin deliberately evaluates that decision twice with distinct snapshots:

1. After stateful accumulation and session/delta calculation, but before the distance-acceptance branch updates the plugin's `_lastObservedSessionMeters` or `_lastIgnoredSessionMeters`, the first decision feeds the generic half-lap distance-jump guard for an invalid lap increment.
2. After the distance branch has updated `_lastObservedSessionMeters` or `_lastIgnoredSessionMeters`, the plugin constructs a fresh context and asks again whether to accept the lap-counter change and how to log it.

The post-distance decision must not reuse the pre-distance result because the decision inputs can change during the distance branch.

Telemetry thresholds and anomaly rules are evidence-driven. Change them only in separate work with simulator evidence and focused regression tests; the profile migration does not generalize one game's observed thresholds to another.

### RaceRoom evidence and source choice

RaceRoom remains on stateful track-position distance. The speed integral and raw `SessionOdo` are diagnostics, not persisted distance sources.

The November 8, 2026 controlled logs established these limits:

- On Silverstone Stowe, two normal laps produced `2,580.03 m` of saved position distance. The freshness-unsafe speed estimate was close before telemetry froze, but a repeated `140.36 km/h` frame continued to inflate it. The installed RaceRoom reader exposes `Player.GameSimulationTicks`; repeated ticks now break the estimate chain, the first fresh frame after a freeze only re-anchors it, and a backward/reset sequence starts a new timing anchor instead of bridging timelines.
- Returning to the Stowe garage with two completed laps preserved the prior `2,580.03 m`, but the old unconditional floor created a separate `2,624.86 m` session immediately after departure while the car had not driven that distance.
- On the `24,321.31 m` Nordschleife-NLS layout, the shortcut advanced the counter near `3.28 km` of observed movement. The old floor added `21,041.56 m` in one update. A subsequent normal full lap was visible in continuous position telemetry, so the new layout-specific decision disables the untrusted counter floor while keeping position movement.
- Multiplying raw `SessionOdo` changes by `1.609344` was close to the movement estimates in the observed runs, but its units, reset behavior, provenance, and agreement with RaceRoom's in-game per-track statistic have not been established. It is therefore logged only.

Fresh live validation still needs before/after RaceRoom per-track totals for a normal multi-lap run, a partial lap returned to the garage, and an NLS shortcut followed by a normal lap. Compare those totals with saved rows, the freshness-safe speed estimate, raw and converted `SessionOdo`, and position distance before considering either diagnostic as a persisted source.

## Session Start Behavior

At session start, Affinity records:

- current session id
- active bucket key
- profile-selected distance mode and derived source
- session origin
- last track position and lap-counter state
- current telemetry sample time

AMS2 and Project Motor Racing also declare that the engine should capture the starting track position and accept their observed initial-position behavior. Project Motor Racing alone enables the stationary startup anchor.

ACC may promote an active compact track context to a longer descriptive context without ending the session when its profile confirms that the new value is an upgrade of the old one.

Unsupported fallback profiles never reach session setup.

## Reset And Wrap Handling

Affinity has a few protections for bad telemetry transitions:

- startup position snap guard
  - ignores large initial jumps while stationary
- session distance reset handling
  - if session distance drops materially, Affinity updates its local baseline instead of adding negative distance
- derived line-wrap guard
  - if derived session distance appears to move backward by about one lap before counters settle, Affinity waits for telemetry sync instead of treating the transition as a real reset
- empty-session persistence guard
  - if a finalized session has less than `1 meter` of distance, Affinity does not save it, even if telemetry time accumulated

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

- `C:\Program Files (x86)\SimHub\PluginsData\Affinity\Affinity.distance.debug.<game>.log`

Each supported profile supplies the stable settings key used by its debug toggle and filename. Unsupported games have no per-game logging key.

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
- replay and inactive-sample decisions when detected
- selected raw game-state fields for games with targeted guards
- reset and wrap events
- RaceRoom's diagnostic speed integral, raw `Player.GameSimulationTicks` freshness sequence, freshness availability, and whether the NLS lap-counter floor has been disabled for the active segment

## Extending Game Support

Add game-specific behavior to a concrete `IAffinityGameProfile`, then register that profile in `SupportedProfiles`. A complete addition covers runtime aliases, any distinct logo aliases, settings/debug key, display name, logo filename, telemetry classification, structural distance capabilities, anomaly decisions, track/circuit display, and regression tests.

Keep mutable counters, session state, persistence, logging, file access, WPF/UI, and lifecycle orchestration in `AffinityPlugin`. Do not add direct normalized game-name comparisons or `IsXGame` branches to `AffinityPlugin` or `AffinitySummaryBuilder`. Preserve raw stored identities unless separately planned migration work explicitly changes them.
