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

1. For game-support changes, cover the full surface:
   - supported game detection
   - display and storage name
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
3. If runtime plugin behavior or UI changed, also run the normal plugin build so it copies plugin binaries into SimHub when possible:
   - `dotnet build .\Affinity\Affinity.csproj`
4. Routine SimHub deployment must not overwrite the live `ac_track_id_map.json`; leave the installed map intact unless the user explicitly asks to refresh it.
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
