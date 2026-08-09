# Affinity iRacing Replay Investigation

Date: 2026-07-22

## User Report

- Affinity records distance/time when iRacing replays are watched.
- The problem does not reproduce when replay is watched immediately after a live run inside the same session.
- The problem does reproduce when a replay is opened and watched as its own session.

## Runtime Fields We Checked

The plugin now logs these replay-related fields from both `GameData` and `StatusDataBase`:

- `IsGameReplay`
- `GameReplay`
- `ReplayMode`

It also logs a wider set of replay-adjacent fields when present, including:

- `SessionId`
- `SessionType`
- `SessionState`
- `SessionNum`
- `SessionTime`
- `SessionTimeRemain`
- `SessionTimeLeft`
- `SessionTick`
- `SessionTickCount`
- `ReplayPlaySpeed`
- `ReplayFrameNum`
- `ReplayFrameNumEnd`
- `ReplaySessionNum`
- `ReplaySessionTime`
- `IsSpectator`
- `IsOnTrack`
- `IsInPit`
- `IsInGarage`
- `PlayerTrackSurface`
- `PlayerCarPosition`

## Key Finding

For the standalone replay capture on Wednesday, July 22, 2026, iRacing continued to emit telemetry during replay, and SimHub surfaced it as if it were live telemetry.

The replay samples kept reporting:

- `gdGameReplay=False`
- `sdIsGameReplay=False`
- `sdReplayMode=Live`

So the direct replay flags are not reliable enough to use as the only replay detector for iRacing.

## Evidence From Logs

Relevant files:

- `C:\Program Files (x86)\SimHub\PluginsData\Affinity\Affinity.distance.debug.iracing.log`
- `C:\Program Files (x86)\SimHub\Logs\SimHub.txt`

### Passive replay still looks live

During the standalone replay capture around `2026-07-22T22:38:04Z` through `2026-07-22T22:39:54Z`, the debug log showed normal-looking moving telemetry while replay flags stayed false/live.

Examples:

- stable `SessionId=846ae0ac-8c43-4d73-bae0-3f6dfd5c0dd1` for a long replay stretch
- non-zero `speedKmh`
- advancing `posM`
- advancing `sessOdoRaw`
- `gdGameReplay=False`
- `sdReplayMode=Live`

This means a user could simply open a replay and watch it straight through, and the plugin would still see telemetry that looks countable.

### Scrubbing and lap skipping create session churn

Later in the same replay capture, the `SessionId` changed repeatedly while staying in the same `game / car / track` context:

- `846ae0ac-8c43-4d73-bae0-3f6dfd5c0dd1`
- `340aa360-ffa7-4376-b0a0-f4ceaa68eefc`
- `dd024f1a-68a3-4e2c-8405-9d68f2a6ae2b`
- `cb268e90-abe8-429a-8209-e8058dd70b10`
- `e675b736-e550-420a-bb23-ec67cc82f319`

The matching SimHub runtime log also showed repeated restarts without a true disconnect first:

- `2026-07-22 18:39:35` `Session restarted`
- `2026-07-22 18:39:40` `Session restarted`
- `2026-07-22 18:39:40` `Session restarted`
- `2026-07-22 18:39:41` `Session restarted`

This churn is a useful secondary signal for replay scrubbing, fast-forwarding, rewinding, and lap skipping.

### Why `SessionId` churn is not enough by itself

`SessionId` churn would help catch replay transitions caused by:

- rewinding
- fast-forwarding
- jumping between laps
- similar replay navigation

But it would not catch a passive replay that keeps one stable `SessionId` and still emits live-looking telemetry.

## Current Conclusion

- iRacing replay mode can emit telemetry that Affinity currently sees as valid live telemetry.
- The known replay-state fields are not trustworthy in this scenario.
- `SessionId` churn is helpful but only as a secondary heuristic.
- A `SessionId`-churn-only fix should not be shipped because it would miss passive replay playback.

## Good Future Directions

If we return to this work later, the best options are:

1. Keep direct replay flags as a first-pass signal, but do not trust them alone.
2. Add an inferred-replay heuristic for iRacing that combines:
   - repeated `SessionId` changes
   - repeated `Session restarted` behavior
   - impossible live-session resets or jumps
3. Look for one more raw iRacing/SimHub field that differs between real live driving and passive replay playback.

## Implemented Fix

On Saturday, August 8, 2026, reflection against the installed SimHub iRacing reader showed that `IRacingReader.DataSampleEx` exposes a nested SDK telemetry object:

- `DataSampleEx.Telemetry`
- `Telemetry.IsReplayPlaying`

Affinity already received the raw iRacing sample through `StatusDataBase.GetRawDataObject()`, but `IsReplayTelemetry()` only checked replay fields directly on `GameData` and `StatusDataBase`.

The fix keeps the existing generic checks first, then inspects the raw iRacing object:

1. call `GetRawStatusDataObject(data.NewData)`
2. check `rawData.IsReplayPlaying` when a reader exposes the flag directly
3. check `rawData.Telemetry.IsReplayPlaying` for iRacing's `DataSampleEx`
4. ignore the telemetry sample before distance or used time can be accumulated

This avoids relying on iRacing's misleading SimHub-facing values, where replay playback can still report `GameReplay=False`, `IsGameReplay=False`, and `ReplayMode=Live`.

Runtime follow-up:

- After copying the build into SimHub on Saturday, August 8, 2026, watching the beginning of the same iRacing replay no longer recorded replay distance/time.

## What Not To Assume

- Do not assume `ReplayMode` will flip away from `Live`.
- Do not assume `GameReplay` or `IsGameReplay` will become `True`.
- Do not assume replay is only detectable when the user scrubs backward.

## Status

- Investigation captured.
- Logging expanded for future replay captures.
- Follow-up on 2026-08-08 found the reliable raw iRacing SDK signal: `DataSampleEx.Telemetry.IsReplayPlaying`.
- Affinity now checks that nested raw telemetry flag in addition to SimHub's generic replay fields.
- User runtime validation on 2026-08-08 confirmed the deployed build stopped recording distance/time during replay playback.
