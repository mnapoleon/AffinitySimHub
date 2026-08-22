# Affinity Game Profiles Design

## Goal

Affinity will adopt the game-profile model proven in the sibling StatsPlus plugin. Game-name classification, supported-game metadata, display rules, telemetry eligibility, and game-specific distance policies will move behind `IAffinityGameProfile` implementations while `AffinityPlugin` remains the game-agnostic session, persistence, logging, and UI orchestrator.

This is a behavior-preserving structural migration. It must not change recorded distance, used time, supported games, stored game/car/track identities, debug-setting keys, log filenames, logos, published SimHub properties, or visible circuit formatting.

## Current Structure

Game-specific behavior is currently spread across three production files:

- `AffinityGameLogic.cs` normalizes and classifies game names, recognizes supported games, maps Assetto Corsa track displays, validates LMU context, and detects ACC track-name upgrades.
- `AffinityPlugin.cs` contains supported-game metadata, logo metadata, debug-setting metadata, generic replay detection, AMS2 and RaceRoom inactive-telemetry checks, and game-specific distance branches.
- `AffinitySummaryBuilder.cs` contains game-specific circuit-name/layout formatting.

That distribution makes adding or reviewing a game require finding every `IsXGame` branch. It also permits supported-game lists, aliases, labels, logos, debug keys, and runtime behavior to drift apart.

## Scope

The migration covers Affinity's existing nine supported profiles:

1. Assetto Corsa
2. Assetto Corsa Competizione
3. Assetto Corsa EVO
4. Automobilista 2
5. iRacing
6. Le Mans Ultimate
7. Project Motor Racing
8. rFactor 2
9. RaceRoom Racing Experience

The migration does not add games, change telemetry thresholds, correct suspected telemetry behavior, migrate stored data, rename settings, alter the WPF layout, or refresh the installed `ac_track_id_map.json`.

## Architecture

### Profile registry

`AffinityGameProfileRegistry` owns an ordered list of supported profiles and an unsupported fallback profile. It exposes:

```csharp
internal IReadOnlyList<IAffinityGameProfile> SupportedProfiles { get; }
internal IAffinityGameProfile Resolve(string gameName);
```

`Resolve` uses aliases normalized by the same alphanumeric, case-insensitive algorithm as the current `AffinityGameLogic.NormalizeGameName`. The fallback profile has empty metadata and `IsSupported == false`. It still inherits generic replay classification so replay telemetry from an unknown game continues to be identified before the unsupported-game response, matching current `DataUpdate()` ordering.

### Profile contract

`IAffinityGameProfile` defines the following ownership boundary:

```csharp
internal interface IAffinityGameProfile
{
    string SettingsKey { get; }
    string DisplayName { get; }
    string LogoFileName { get; }
    bool IsSupported { get; }
    bool Matches(string gameName);
    bool MatchesLogoName(string gameName);

    TelemetryDisposition EvaluateTelemetry(AffinityTelemetryContext context);
    string GetTrackDisplayName(string rawTrackNameWithConfig, AffinityTrackDisplayContext context);
    CircuitDisplayParts GetCircuitDisplayParts(string trackDisplayName);
    bool CanPromoteTrackContext(string previousTrackNameWithConfig, string updatedTrackNameWithConfig);

    AffinityDistanceMode DistanceMode { get; }
    bool CapturesSessionStartTrackPosition { get; }
    bool UsesStationaryStartupAnchor { get; }
    bool AcceptsInitialPositionSnap { get; }
    bool UsesLapCounterDistanceFloor { get; }
    bool ShouldIgnoreTransientReset(AffinityDistanceSampleContext context);
    bool ShouldIgnoreLowSpeedLineWrap(AffinityDistanceSampleContext context);
    bool ShouldIgnoreLapIncrement(AffinityDistanceSampleContext context);
    bool ShouldIgnorePlaceholderSessionStart(AffinityDistanceSampleContext context);
}
```

The contract uses narrowly scoped value contexts instead of exposing `AffinityPlugin`. `AffinityTelemetryContext` and `AffinityDistanceSampleContext` are structs so the telemetry hot path does not allocate a new context object for every sample. Profiles make decisions from snapshots while the plugin continues to own and update mutable session-distance fields.

`AffinityGameProfileBase` implements shared defaults:

- alias normalization and matching
- generic replay classification
- active telemetry when no generic replay signal is present
- raw track display passthrough
- generic `-` circuit/layout splitting
- stateful-derived distance mode for supported profiles
- `false` for optional distance anomalies and capabilities

The interface contains no default executable implementation. Affinity targets .NET Framework 4.8, so shared behavior belongs in the abstract base class rather than default interface methods.

### Generic replay detection

The existing replay algorithm moves from `AffinityPlugin` to `AffinityReplayDetector`. It keeps all current reflection-based probes:

- `GameData.IsGameReplay`
- `GameData.GameReplay`
- `GameData.ReplayMode`
- status `IsGameReplay`
- status `ReplayMode`
- raw `IsReplayPlaying`
- nested raw `Telemetry.IsReplayPlaying`

`AffinityGameProfileBase.EvaluateTelemetry` calls the detector and returns `TelemetryDisposition.Replay` when any probe is active. Concrete profiles call the base implementation first, then add only game-specific classifications. AMS2 adds garage, spectator, replay game-state, and viewed-participant checks. RaceRoom adds finished and garage checks. LMU adds `WaitingForContext` when car or track context is unavailable.

The result is a single profile-facing classification call from `DataUpdate()` without duplicating generic replay logic.

### Telemetry runtime state

`AffinityTelemetryContext` includes a plugin-owned `AffinityGameRuntimeState`. The runtime state retains the learned AMS2 player participant index currently stored in `_automobilista2PlayerViewedParticipantIndex`. The AMS2 profile owns the rule that reads and updates this value; the plugin owns its lifetime and resets it whenever `GameRunning` becomes false, preserving current behavior.

The same state value remains available to centralized debug logging so the existing `ams2PlayerViewedParticipantIndex` field remains in telemetry snapshots.

### Distance policy

The distance engine and its mutable state remain in `AffinityPlugin`. Profiles replace game-name branches with capabilities and decision methods:

- Every supported profile selects `AffinityDistanceMode.StatefulDerived`, preserving the current forced derived-distance source.
- AMS2 and Project Motor Racing capture the session's initial track position and accept the existing initial-position snap.
- Project Motor Racing enables the stationary startup-anchor algorithm.
- iRacing recognizes the transient stopped-car lap/distance zero reset.
- RaceRoom recognizes inactive telemetry and enables the lap-counter distance floor.
- rFactor 2 recognizes low-speed false line wraps and false lap increments.
- LMU recognizes unreliable context, false exit/reset lap increments, and placeholder session starts.
- ACC recognizes a compact-to-descriptive track-name promotion within the same session.
- Assetto Corsa classic, ACC, and EVO otherwise share the default supported distance behavior.

Threshold calculations and state mutations stay in the existing distance engine unless they are themselves game-specific decisions. Profiles return decisions; they do not write files, query storage, update WPF properties, or mutate distance buckets.

### Metadata and display

Each concrete profile is the single source for:

- canonical debug settings key
- friendly display name
- supported aliases
- SimHub logo filename
- track display-name resolution
- circuit name/layout derivation

`SupportedProfiles` replaces `DefaultGameDebugLoggingEntries` and `GameLogoFileNames`. Existing debug keys and filenames remain unchanged.

Runtime aliases and logo lookup aliases remain distinct where current behavior differs. In particular, `LMU` remains the supported runtime name, while `Le Mans Ultimate` remains a logo-only lookup variant; the refactor must not silently broaden runtime support. `AffinityGameProfileRegistry.ResolveLogo(string)` may use `MatchesLogoName`, while runtime, summary, debug, and distance decisions continue to use `Resolve(string)`.

The Assetto Corsa track map remains loaded and owned by the plugin. It is passed through `AffinityTrackDisplayContext`; only the Assetto Corsa classic profile uses it, matching current Affinity behavior. Assetto Corsa EVO is not changed to StatsPlus's mapping behavior in this refactor.

Circuit display rules remain:

- Assetto Corsa classic, ACC, and LMU duplicate the full display value into both circuit columns.
- rFactor 2 splits on `--`.
- iRacing splits on `-` and title-cases the circuit name while preserving `GP`.
- Other profiles split on `-` and replace underscores with spaces in split parts.

Display values remain transient. Raw `GameName`, `CarModel`, `TrackName`, and `TrackNameWithConfig` continue to define persistence identities and context keys.

## Runtime Data Flow

For each active `DataUpdate()` sample:

1. Normalize missing car/track values exactly as today, without canonicalizing a persisted game name.
2. Resolve a profile from `data.GameName`.
3. Call `profile.EvaluateTelemetry(context)`.
4. Handle `Replay`, `Inactive`, and `WaitingForContext` with the existing status text, session finalization, reset, and property publication behavior.
5. Reject `!profile.IsSupported` using the existing unsupported-game flow.
6. Use profile metadata for debug settings and paths.
7. Use `profile.CanPromoteTrackContext` before starting a new context.
8. Use profile distance mode, capabilities, and decision methods from the unchanged session-distance orchestration.
9. Persist the original game/car/track identity and publish the same SimHub properties as today.

When no game is running, the plugin resets `AffinityGameRuntimeState` in addition to its existing active-session state.

## Error Handling and Compatibility

Profile resolution is total: null, blank, and unknown names return the fallback profile rather than throwing. Context objects tolerate null `GameData`, status, track maps, and raw telemetry. Generic reflection helpers continue returning `false` for missing or unconvertible members.

No database migration is required. Existing JSON settings keys remain valid. Initialization removes unsupported debug keys and restores missing supported keys from the profile catalog exactly as it does today.

There is no new dependency-injection framework. `AffinityPlugin` owns one default registry instance and passes it to summary construction. Tests can create an independent default registry.

## Testing

Focused profile tests will verify:

- all aliases resolve to unique canonical settings keys and display names
- fallback behavior for null and unknown games
- all existing logo mappings
- Assetto Corsa track-map and circuit display behavior
- ACC track-name promotion
- generic replay classification through the base implementation
- AMS2, RaceRoom, and LMU telemetry dispositions
- every game-specific distance capability and anomaly decision

Existing plugin tests remain as orchestration/regression coverage. They will be updated to assert behavior through profile routing rather than private `IsXGame` helpers. Summary tests will prove stored raw identities are unchanged while display values come from profiles. A source-boundary regression test will ensure production code outside the profile implementation no longer introduces `IsXGame` methods or direct normalized game-name comparisons.

Validation runs serially:

```powershell
dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist
dotnet build .\Affinity\Affinity.csproj /p:SimHubInstallPath=C:\does-not-exist
dotnet build .\Affinity\Affinity.csproj
```

The normal build may be blocked if SimHub has plugin DLLs locked. Routine deployment must leave the installed `ac_track_id_map.json` intact.

## Documentation

`README.md`, `Affinity-distance-counting.md`, and `.codex/project-practices.md` will describe the profile boundary and require future game-specific differences to be added to the appropriate profile rather than as new `AffinityPlugin` game-name branches.
