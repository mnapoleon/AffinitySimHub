# Affinity Storage Subfolder Design

## Goal

Move Affinity-owned runtime files into a dedicated `Affinity` subfolder under SimHub common storage so SimHub updates are less likely to delete the plugin's SQLite database.

## Scope

This design covers:

- Moving the SQLite database path from the shared common root into `PluginsData\Common\Affinity\`.
- Moving the settings JSON path into the same `Affinity` subfolder.
- Moving the debug log path into the same `Affinity` subfolder.
- Preserving existing user data by migrating from the old shared-root file locations on startup.
- Preserving legacy JSON-to-SQLite import behavior during the transition.

This design does not cover:

- Moving Affinity storage outside SimHub's `PluginsData\Common` area.
- Changing the SQLite schema or session aggregation logic.
- Introducing backup/export UI for the database.

## Current State

The plugin currently resolves these files directly from the shared SimHub common storage root:

- `Affinity.settings.json`
- `Affinity.distance.db`
- `Affinity.distance.json`
- `Affinity.distance.debug.log`

That keeps the code simple, but it also means Affinity's files live beside unrelated shared data instead of in a plugin-owned directory. If a SimHub update removes or recreates files in that shared root, the Affinity database is exposed.

## Proposed Changes

### 1. Dedicated Affinity storage root

Resolve a plugin-owned storage directory with `PluginManager.GetCommonStoragePath("Affinity")`, then build the runtime file paths under that directory:

- `Affinity\Affinity.settings.json`
- `Affinity\Affinity.distance.db`
- `Affinity\Affinity.distance.debug.log`
- `Affinity\Affinity.distance.json` for transition-time legacy probing only

This keeps all Affinity-owned files together while staying inside SimHub's supported common storage area.

### 2. One-time file migration from old shared-root paths

On startup, if a new-path file is missing and the corresponding old shared-root file exists, copy or move the old file into the new `Affinity` subfolder before normal use.

Migration rules:

- Settings JSON: move from `Common\Affinity.settings.json` to `Common\Affinity\Affinity.settings.json` only when the new file does not already exist.
- SQLite DB: move from `Common\Affinity.distance.db` to `Common\Affinity\Affinity.distance.db` only when the new file does not already exist.
- Debug log: do not migrate old log content eagerly; just begin writing to the new path.

This keeps user data automatically while avoiding overwriting newer files that may already exist in the subfolder.

### 3. Transition-aware legacy JSON import

The plugin already imports old JSON distance totals into SQLite when the SQLite database has no session data. That behavior should remain, but the JSON lookup should become transition-aware:

- Prefer `Common\Affinity\Affinity.distance.json` if it exists.
- Otherwise fall back to the old shared-root `Common\Affinity.distance.json`.

After a successful import, back up the source JSON file in place by renaming it to `.bak`, matching the current behavior.

### 4. Visible runtime path updates

Any plugin property or UI element that exposes the resolved database or debug-log path should reflect the new subfolder path so runtime diagnostics stay accurate.

## File Changes

- Modify `Affinity/AffinityPlugin.cs`
  - Resolve a dedicated Affinity storage root under SimHub common storage.
  - Build settings, SQLite, debug-log, and transition legacy JSON paths from that root.
  - Add startup migration logic from old shared-root files to the new subfolder.
  - Keep legacy JSON import behavior working across both old and new locations.

- Add or modify tests under `Affinity.Tests/`
  - Verify the plugin resolves the new `Affinity` subfolder path.
  - Verify migration prefers an existing new-path file over an old-path file.
  - Verify legacy JSON lookup still falls back to the old shared-root location during transition.

## Error Handling

- If the `Affinity` storage subfolder cannot be created or the SQLite file cannot be moved, log the failure and continue using the resolved new path so initialization still attempts to start cleanly.
- If both old and new copies of a file exist, keep the new-path file and leave the old one untouched.
- If legacy JSON import fails, keep the current behavior of logging a warning and continuing with an empty in-memory store or empty SQLite database state.

## Testing

Local validation for this change set should include:

- Unit tests covering the path resolution and migration decision logic.
- `dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist`
- `dotnet build .\Affinity\Affinity.csproj /p:SimHubInstallPath=C:\does-not-exist`

If practical after the build, the plugin should also be copied into SimHub so the new runtime storage path can be verified in the Settings UI and in actual generated files.

## Risks And Tradeoffs

- Moving settings and database files together makes the storage layout cleaner, but it adds startup migration logic that must avoid overwriting existing files.
- Keeping the debug log in the new subfolder improves consistency, but historical logs will remain split across old and new locations unless the user cleans them up manually.
- Staying inside `PluginsData\Common` preserves SimHub compatibility, but it assumes the `Affinity` subfolder is treated more safely by updates than loose files in the root.

## Success Criteria

This work is successful when:

- Affinity resolves its runtime settings, SQLite database, and debug log under `PluginsData\Common\Affinity\`.
- Existing users keep their settings and SQLite data automatically after upgrading.
- Legacy JSON import still works for users who have not yet migrated to SQLite or still have only the old shared-root JSON file.
- Runtime diagnostics show the new subfolder paths rather than the old shared-root paths.
