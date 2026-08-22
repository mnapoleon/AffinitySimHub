# AffinitySimHub Project Practices

Use these practices for each AffinitySimHub change unless the user explicitly asks for a different workflow.

## Change Workflow

1. Start by checking repo state: current branch, dirty files, untracked files, and whether the checkout is the main workspace or a worktree.
2. Use short kebab-case branch names, without `codex/`, unless the user asks otherwise.
3. Keep each change narrowly scoped. Stage only the intended files and leave unrelated or pre-existing untracked files alone.
4. For non-trivial feature or behavior changes, sketch the design before coding. For small fixes, state the intended shape briefly before editing.
5. For bugs, work from evidence first: logs, persisted data, repro details, current behavior, then code.
6. Prefer test-first fixes when behavior changes. Add or update focused regression tests before production code where practical.
7. Follow existing Affinity patterns before adding new abstractions:
   - game recognition helpers
   - per-game debug logging options
   - summary builder and test patterns
   - storage and repository boundaries
   - XAML layout conventions already in the plugin
8. Keep `DataUpdate()` and telemetry hot paths lightweight. Avoid file I/O, expensive queries, or broad recomputation there.
9. Preserve persisted data compatibility whenever practical. For storage changes, plan migration, fallback, and rollback behavior explicitly.

## Game And Telemetry Changes

- Put game-specific metadata, aliases, display rules, telemetry classification, and distance decisions behind `IAffinityGameProfile`. Prefer extending a concrete profile over adding direct normalized game-name comparisons or `IsXGame` branches to `AffinityPlugin` or `AffinitySummaryBuilder`.

1. For game-support changes, cover the full surface:
   - supported game detection
   - display name and preservation of raw persistence identity
   - runtime distance rules
   - logo mapping
   - debug logging setting
   - tests for the above
2. For UI/settings changes, verify tab refresh and selection behavior. Prior bugs often came from rebuilding WPF tab collections too aggressively.
3. For distance/session logic, compare expected real-world run distance against:
   - Affinity debug log samples
   - persisted DB/session rows
   - track length
   - lap count and session transitions
4. For replay/live-session filtering, verify against game-specific telemetry fields and add regression coverage for the affected simulator.

## Logs And Runtime Data

Use these paths when investigating runtime behavior:

- SimHub runtime log:
  - `C:\Program Files (x86)\SimHub\Logs\simhub.txt`
- Affinity plugin data folder:
  - `C:\Program Files (x86)\SimHub\PluginsData\Affinity\`
- Affinity SQLite distance database:
  - `C:\Program Files (x86)\SimHub\PluginsData\Affinity\Affinity.distance.db`
- Affinity per-game distance debug logs:
  - `C:\Program Files (x86)\SimHub\PluginsData\Affinity\Affinity.distance.debug.<game>.log`
  - Example: `C:\Program Files (x86)\SimHub\PluginsData\Affinity\Affinity.distance.debug.projectmotorracing.log`
- SQLite native fallback copies, when relevant:
  - `C:\Program Files (x86)\SimHub\PluginsData\Affinity\sqlite-native\x86\SQLite.Interop.dll`
  - `C:\Program Files (x86)\SimHub\PluginsData\Affinity\sqlite-native\x64\SQLite.Interop.dll`

When investigating telemetry issues, compare persisted DB rows, Affinity debug logs, and SimHub log exceptions before changing code.

## Validation

1. Run validation serially, not in parallel, because this repo can get temporary `obj` or DLL locks when build and test overlap.
2. Default validation commands:
   - `dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist`
   - `dotnet build .\Affinity\Affinity.csproj /p:SimHubInstallPath=C:\does-not-exist`
3. If runtime plugin behavior or UI changed, use the no-deploy build above, then copy the validated runtime manifest explicitly from `Affinity\bin\Debug\net48` into SimHub. The manifest is `Affinity.dll`, `System.Data.SQLite.dll`, both top-level `x86`/`x64` `SQLite.Interop.dll` files, and both `PluginsData\Affinity\sqlite-native` recovery copies. Use the map-safe PowerShell workflow documented under "Live deploy into SimHub" in `README.md`.
4. Do not use a normal `dotnet build .\Affinity\Affinity.csproj` for routine deployment. The `CopyPluginToSimHub` post-build target copies every file under `$(TargetDir)`, including `ac_track_id_map.json`. Reserve that broad build/copy path for an explicit user request to refresh the installed map; otherwise leave the installed map intact.
5. If SimHub has files locked, do not force the copy. Report the lock, ask the user to close or restart SimHub, then retry the copy when asked.
6. When packaging or release behavior changes, check both the project build output and GitHub workflow/archive contents so shipping artifacts match runtime expectations.

## Commit And PR

1. Before commit or PR, re-check the diff and staged files.
2. Commit messages should follow the repo style, usually `patch: concise behavior summary`.
3. PRs should include:
   - concise `patch:` title
   - what changed
   - tests and builds run
   - whether SimHub copy succeeded or was blocked
4. Close out with the exact verification and deployment state: what passed, what copied to SimHub, what was skipped or blocked, and any remaining local or untracked files.
