# Affinity Storage Subfolder Design

## Goal

Move Affinity-owned runtime data out of `PluginsData\Common` and into a dedicated `PluginsData\Affinity` folder, with a rolling SQLite backup written each time SimHub shuts down cleanly.

## Scope

This design covers:

- Moving the SQLite database path from `PluginsData\Common` into `PluginsData\Affinity`.
- Moving the settings JSON path into `PluginsData\Affinity`.
- Moving the debug log path into `PluginsData\Affinity`.
- Preserving existing user data by migrating from older Affinity file locations on startup.
- Preserving legacy JSON-to-SQLite import behavior during the transition.
- Writing a rolling set of numbered SQLite `.bak` copies during plugin shutdown.

This design does not cover:

- Moving SQLite runtime DLLs out of the SimHub plugin load location.
- Changing the SQLite schema or session aggregation logic.
- Introducing timestamped backup retention or backup UI.

## Current State

The plugin currently resolves these files directly from SimHub common storage:

- `PluginsData\Common\Affinity.settings.json`
- `PluginsData\Common\Affinity.distance.db`
- `PluginsData\Common\Affinity.distance.json`
- `PluginsData\Common\Affinity.distance.debug.log`

That leaves Affinity's mutable data in SimHub's shared common-data area instead of in a plugin-owned folder. If a SimHub update removes or recreates files in `Common`, the Affinity database is exposed.

The SQLite runtime binaries are separate from this problem:

- `System.Data.SQLite.dll`
- `x64\SQLite.Interop.dll`
- `x86\SQLite.Interop.dll`

Those are runtime dependencies and should remain in the normal plugin deployment location beside the plugin binaries rather than moving into a data folder.

## Proposed Changes

### 1. Dedicated Affinity storage root under PluginsData

Resolve a plugin-owned storage directory under `PluginsData\Affinity`, then build the runtime file paths under that directory:

- `PluginsData\Affinity\Affinity.settings.json`
- `PluginsData\Affinity\Affinity.distance.db`
- `PluginsData\Affinity\Affinity.distance.debug.log`
- `PluginsData\Affinity\Affinity.distance.json` for transition-time legacy probing only

This keeps all mutable Affinity data together while avoiding any ongoing reliance on `PluginsData\Common`.

### 2. Transition-aware startup migration

On startup, if a new-path file is missing and an older-path copy exists, move the older file into `PluginsData\Affinity` before normal use.

Migration precedence:

- First preference: existing file already present in `PluginsData\Affinity`
- Second preference: file in `PluginsData\Common\Affinity\...`
- Third preference: file in the old shared-root `PluginsData\Common\...`

Migration rules:

- Settings JSON: move only when the new file does not already exist.
- SQLite DB: move only when the new file does not already exist.
- Debug log: do not migrate old log contents eagerly; just begin writing to the new path.

This keeps user data automatically while avoiding overwriting a newer file that may already exist in the final location.

### 3. Transition-aware legacy JSON import

The plugin already imports old JSON distance totals into SQLite when the SQLite database has no session data. That behavior should remain, but the JSON lookup should become transition-aware:

- Prefer `PluginsData\Affinity\Affinity.distance.json` if it exists.
- Otherwise fall back to `PluginsData\Common\Affinity\Affinity.distance.json` if it exists.
- Otherwise fall back to the old shared-root `PluginsData\Common\Affinity.distance.json`.

After a successful import, back up the source JSON file in place by renaming it to `.bak`, matching the current behavior.

### 4. Rolling SQLite backup on clean shutdown

During `End()`, after the plugin closes and disposes the SQLite connection, copy:

- `PluginsData\Affinity\Affinity.distance.db`

to:

- `PluginsData\Affinity\Affinity.distance.db.bak.1`

Backup rules:

- Keep up to five numbered backups, `Affinity.distance.db.bak.1` through `.bak.5`.
- Treat `.bak.1` as the latest backup and `.bak.5` as the oldest backup.
- Rotate existing numbered backups before writing the new `.bak.1`.
- If an older unnumbered `.bak` exists from a previous version, preserve it as the previous backup in the numbered rotation when possible.
- If the live database file does not exist, skip backup silently.
- If the backup copy fails, log a warning but do not crash plugin shutdown.

This gives the plugin five last-known-good local backups without introducing settings or extra UI.

### 5. Visible runtime path updates

Any plugin property or UI element that exposes the resolved database or debug-log path should reflect the new `PluginsData\Affinity` location so runtime diagnostics stay accurate.

## File Changes

- Modify `Affinity/AffinityPlugin.cs`
  - Resolve a dedicated `PluginsData\Affinity` storage root.
  - Build settings, SQLite, debug-log, backup, and transition legacy JSON paths from that root.
  - Add startup migration logic from older file locations to the new folder.
  - Keep legacy JSON import behavior working across old and new locations.
  - Write rolling numbered SQLite `.bak` files during `End()` after the repository is disposed.

- Add or modify tests under `Affinity.Tests/`
  - Verify the plugin resolves the new `PluginsData\Affinity` paths.
  - Verify migration prefers an existing new-path file over older-path files.
  - Verify startup migration accepts both `Common\Affinity\...` and old `Common\...` file locations.
  - Verify legacy JSON lookup still falls back across older locations during transition.
  - Verify shutdown backup behavior handles missing live DB and five-backup rotation semantics.

## Error Handling

- If the `PluginsData\Affinity` folder cannot be created or a file cannot be moved, log the failure and continue using the resolved new path so initialization still attempts to start cleanly.
- If both old and new copies of a file exist, keep the new-path file and leave the old one untouched.
- If legacy JSON import fails, keep the current behavior of logging a warning and continuing with an empty in-memory store or empty SQLite database state.
- If SQLite backup creation fails during shutdown, log a warning and continue shutdown without surfacing an unhandled exception.

## Testing

Local validation for this change set should include:

- Unit tests covering path resolution, migration precedence, legacy JSON fallback, and shutdown backup behavior.
- `dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist`
- `dotnet build .\Affinity\Affinity.csproj /p:SimHubInstallPath=C:\does-not-exist`

If practical after the build, the plugin should also be copied into SimHub so the new runtime storage path and generated numbered `.bak` files can be verified in a real install.

## Risks And Tradeoffs

- Moving out of `PluginsData\Common` lowers exposure to shared-root cleanup, but it adds more startup migration paths to support older users safely.
- Five numbered rolling backups preserve more recent shutdown states, but still avoid timestamp cleanup and configuration UI.
- Keeping SQLite runtime DLLs in the plugin load directory avoids loader risk, but it means mutable data and runtime dependencies remain intentionally separated.

## Success Criteria

This work is successful when:

- Affinity resolves its runtime settings, SQLite database, and debug log under `PluginsData\Affinity`.
- Existing users keep their settings and SQLite data automatically after upgrading.
- Legacy JSON import still works for users who only have older Common-based files.
- Each clean SimHub shutdown refreshes `Affinity.distance.db.bak.1` and rotates older backups through `.bak.5` when a live SQLite database exists.
- Runtime diagnostics show the new `PluginsData\Affinity` paths rather than the old Common-based paths.
