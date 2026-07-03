# AffinitySimHub

[![Buy Me a Coffee](https://img.shields.io/badge/Buy%20me%20a%20coffee-support-5F7FFF?logo=buymeacoffee&logoColor=white)](https://www.buymeacoffee.com/mnapoleon)

`Affinity` is a SimHub plugin for tracking cumulative driving distance and driving time across multiple racing sims.

It keeps long-lived totals by:

- game
- car
- track

The plugin is aimed at a simple question: "What have I actually driven over time?" Instead of focusing on single-session lap stats, Affinity persists all-time usage totals and turns them into a browsable summary inside SimHub.

## What It Does

Affinity currently:

- tracks cumulative distance
- tracks cumulative driving time
- groups totals by `game / car / track`
- shows cross-game featured summaries in the Data tab
- shows per-game track and car breakdowns
- supports both kilometers and miles in the UI
- writes optional per-game telemetry debug logs

The current SimHub UI includes:

- a featured card for the most-driven game
- a featured card for the most-driven track, including its game
- a featured card for the most-driven car, including its game
- per-game tabs with top-track and top-car highlights
- per-game track and car tables
- per-game track/car cross-filtering
- per-game period, sort, and top-count filters with a clear action
- an all-games totals summary

## Supported Games

The plugin currently recognizes:

- Assetto Corsa
- Assetto Corsa EVO
- Automobilista 2
- iRacing
- rFactor 2
- RaceRoom Racing Experience

Game recognition is normalized in [AffinityGameLogic.cs](Affinity/AffinityGameLogic.cs), including aliases such as `R3E` and `RRRE`.

## How It Works

### High-level flow

At runtime, the plugin follows this loop:

1. SimHub calls `Init()`.
2. Affinity loads settings, persisted totals, and the Assetto Corsa track-name map.
3. SimHub repeatedly calls `DataUpdate()` with live telemetry.
4. Affinity resolves the active `game / car / track` context.
5. It computes session progress, adds only valid forward distance deltas, and accumulates used time.
6. It periodically upserts SQLite session totals, refreshes UI summaries, and writes a rolling database backup when SimHub shuts down cleanly.

The main entry point is [AffinityPlugin.cs](Affinity/AffinityPlugin.cs).

### Data model

Affinity persists data in a nested structure:

- `AffinityDatabase`
- `GameBucket`
- `CarBucket`
- `TrackBucket`

The effective bucket key is:

- `gameName | carModel | trackNameWithConfig`

Each `TrackBucket` stores:

- canonical game name
- car model
- track name
- track name with config/variation
- cumulative distance in meters
- cumulative used time in seconds
- created timestamp
- last updated timestamp

These models live in [AffinityDatabase.cs](Affinity/AffinityDatabase.cs).

### Distance tracking

Affinity does not blindly trust one telemetry source for every game. For each session it chooses a distance source, establishes an origin, and then only adds positive forward deltas to the active bucket.

Possible session distance sources:

- `Derived`
- `SessionOdoMeters`
- `SessionOdoKilometers`

In practice, the currently supported games all force `Derived`, because their telemetry quirks are better handled by Affinity's reconstruction logic than by relying on raw `SessionOdo`.

Derived distance is based on lap progress and track position, with extra stateful logic for games where lap boundaries or resets are noisy.

Important protections in the runtime logic include:

- ignoring negative resets instead of subtracting from totals
- handling lap-wrap transitions
- ignoring transient zero drops in iRacing
- guarding against false wrap behavior near the line in rFactor 2
- handling startup snaps and session restarts

The detailed distance and time rules are documented in [Affinity-distance-counting.md](Affinity-distance-counting.md).

### Time tracking

Used time is inferred from telemetry cadence, not from file timestamps.

On each valid update:

- the plugin measures elapsed wall-clock time since the previous telemetry sample
- ignores very large gaps that look like pauses or missing telemetry
- adds the elapsed time to the active `TrackBucket`

This keeps time cumulative per `game / car / track` while avoiding huge false jumps after interruptions.

### Summary building

The raw persisted buckets are not bound to the UI directly. Instead, Affinity builds a snapshot view model through [AffinitySummaryBuilder.cs](Affinity/AffinitySummaryBuilder.cs).

That snapshot produces:

- all-games total distance
- all-games total used time
- per-game tabs
- per-game top track and top car summaries
- featured cross-game summaries for game, track, and car

This separation is useful because:

- persistence stays simple and storage-oriented
- the UI binds to presentation-ready summary objects
- unit conversion and display formatting happen in one place

### Track naming

Assetto Corsa track/config identifiers are mapped to friendlier display names using [ac_track_id_map.json](Affinity/ac_track_id_map.json).

That mapping is loaded at startup and applied during summary generation via [AffinityGameLogic.cs](Affinity/AffinityGameLogic.cs).

### Settings and debug logging

Affinity stores user settings separately from distance data.

Settings currently include:

- distance unit preference
- debug logging enabled/disabled
- per-game debug logging toggles

When debug logging is enabled, Affinity writes separate telemetry logs per supported game. This is useful when investigating session resets, lap-wrap behavior, or title-specific telemetry quirks.

## Repository Layout

Key files and folders:

- [Affinity\Affinity.csproj](Affinity/Affinity.csproj): main SimHub plugin project
- [Affinity\AffinityPlugin.cs](Affinity/AffinityPlugin.cs): plugin runtime, persistence, telemetry handling
- [Affinity\AffinitySummaryBuilder.cs](Affinity/AffinitySummaryBuilder.cs): summary aggregation and UI-facing snapshot creation
- [Affinity\AffinityGameLogic.cs](Affinity/AffinityGameLogic.cs): supported-game rules, normalization, telemetry helpers
- [Affinity\AffinityDatabase.cs](Affinity/AffinityDatabase.cs): persisted storage and summary models
- [Affinity\AffinitySimHub.xaml](Affinity/AffinitySimHub.xaml): SimHub settings/data UI
- [Affinity\AffinitySimHub.xaml.cs](Affinity/AffinitySimHub.xaml.cs): save/reset/refresh handlers
- [Affinity.Tests](Affinity.Tests): MSTest coverage for summary building, settings, and game logic
- [lib/SimHub](lib/SimHub): committed SimHub reference assemblies used by local builds and GitHub Actions

## Developer Setup

### Prerequisites

Recommended local setup:

- Windows
- .NET SDK with MSBuild support for `net48`
- SimHub installed at `C:\Program Files (x86)\SimHub\` for live plugin deployment

### Open the solution

Use [Affinity.sln](Affinity.sln).

### Build for local validation

For a clean build that does not try to copy into a live SimHub install:

```powershell
dotnet build .\Affinity\Affinity.csproj /p:SimHubInstallPath=C:\does-not-exist
```

### Run tests

```powershell
dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist
```

### Live deploy into SimHub

The plugin project includes a post-build target that copies:

- `Affinity.dll`
- `Affinity.pdb`
- `ac_track_id_map.json`
- `System.Data.SQLite.dll`
- `x64\SQLite.Interop.dll`
- `x86\SQLite.Interop.dll`

into the configured SimHub install path when that path exists.

The default path is:

- `C:\Program Files (x86)\SimHub\`

So a normal build against the default install can deploy directly into SimHub:

```powershell
dotnet build .\Affinity\Affinity.csproj
```

If SimHub is open and the DLL is locked, close or restart SimHub and rebuild.

### Release ZIP

GitHub releases are built by [.github/workflows/release.yml](.github/workflows/release.yml). The release workflow builds the plugin and uploads a ZIP payload plus a matching `.sha256` file as release assets.

### Verify and install a release

Before extracting the ZIP into your SimHub installation folder, verify its SHA-256 value in PowerShell:

```powershell
Get-FileHash .\Affinity-v0.1.2.zip -Algorithm SHA256
```

Compare that hash with `Affinity-v0.1.2.zip.sha256`, then extract the ZIP into `C:\Program Files (x86)\SimHub\` and restart SimHub.

### Stub-based development and tests

The project builds against committed SimHub reference assemblies under [lib/SimHub](lib/SimHub) so local development and GitHub Actions use the same compile-time SDK surface.

## Runtime Files

During normal SimHub use, the important files are typically:

- settings JSON
- cumulative data JSON
- optional debug logs

Common locations:

- SimHub log: `C:\Program Files (x86)\SimHub\Logs\simhub.txt`
- plugin data: `C:\Program Files (x86)\SimHub\PluginsData\Affinity\`

Affinity resolves its runtime data under `PluginsData\Affinity\` and migrates older Common-based files automatically when they are found. On clean shutdown, it keeps up to five SQLite backups as `Affinity.distance.db.bak.1` through `.bak.5`, where `.bak.1` is the latest backup.

## UI Overview

The plugin uses `IWPFSettingsV2` and a WPF-based settings view.

The Data tab is effectively the reporting surface for the plugin:

- live tracking status with current game, car, and track context
- current-session and current-context distance totals
- featured all-time summary cards across all games
- compact monthly highlights
- per-game tabs for track/car detail
- current-tab track/car cross-filtering
- current-tab track/car search, period, sort order, and result limit controls
- all-games totals footer

The Settings tab controls:

- distance units
- settings save/reset with status feedback
- storage path visibility
- debug logging master switch
- per-game debug logging switches, disabled unless debug logging is enabled

The UI implementation lives in [AffinitySimHub.xaml](Affinity/AffinitySimHub.xaml) and [AffinitySimHub.xaml.cs](Affinity/AffinitySimHub.xaml.cs).

## Testing Strategy

The current tests focus on stable logic that benefits from deterministic coverage:

- summary aggregation
- unit conversion for display
- supported-game normalization
- settings reset behavior

Current test files:

- [AffinitySummaryBuilderTests.cs](Affinity.Tests/AffinitySummaryBuilderTests.cs)
- [AffinityGameLogicTests.cs](Affinity.Tests/AffinityGameLogicTests.cs)
- [AffinitySettingsTests.cs](Affinity.Tests/AffinitySettingsTests.cs)

For runtime telemetry debugging, unit tests are complemented by:

- SimHub runtime logs
- persisted JSON inspection
- optional Affinity telemetry debug logs

## Documentation Map

This README is the top-level guide. The deeper repo notes are useful when you need implementation detail:

- [Affinity-distance-counting.md](Affinity-distance-counting.md): exact distance and used-time rules, game-specific handling, and UI interpretation
- [simhub-plugin-development-guide.md](simhub-plugin-development-guide.md): SimHub plugin lifecycle, telemetry access, and project structure background
- [simhub-plugin-ui-guide.md](simhub-plugin-ui-guide.md): WPF settings UI patterns and SimHub-specific UI conventions
- [simhub-plugin-data-storage-guide.md](simhub-plugin-data-storage-guide.md): persistence patterns and storage tradeoffs for SimHub plugins
- [simhub-plugin-everything-else.md](simhub-plugin-everything-else.md): packaging, debugging, performance, compatibility, and release considerations

## GitHub Pages

The repository includes a dedicated GitHub Pages site under [site](site).

- The deployment workflow is [.github/workflows/pages.yml](.github/workflows/pages.yml).
- It publishes only `site/`, which keeps the existing internal planning notes under `docs/` out of the public Pages output.
- Future screenshots for the landing page should be added under `site/assets/screenshots/`.

## Contributing Notes

A few repo conventions matter when working here:

- prefer short kebab-case branch names
- use PR titles in the form `<type>: <summary>`
- keep `DataUpdate()` lightweight
- avoid unnecessary file I/O in telemetry hot paths
- update tests when summary logic or persisted shapes change

The repo-specific working agreement is in [AGENTS.md](AGENTS.md).

## Quick Start

If you only need the shortest path to productive work:

1. Open [Affinity.sln](Affinity.sln).
2. Run `dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist`.
3. Run `dotnet build .\Affinity\Affinity.csproj /p:SimHubInstallPath=C:\does-not-exist`.
4. Make your change.
5. Re-run tests and build.
6. Build against the real SimHub install when you want to deploy the plugin locally.
