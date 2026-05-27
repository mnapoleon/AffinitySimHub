# AGENTS.md

This file captures the repo-specific working agreement for agents and contributors in `AffinitySimHub`.

## Scope

- Applies to the whole repository.
- Use these instructions in addition to any higher-priority system or user instructions.

## Project Overview

- `Affinity` is a SimHub plugin targeting `.NET Framework 4.8`.
- The main plugin project is [Affinity\Affinity.csproj](C:\Users\micha\dev\AffinitySimHub\Affinity\Affinity.csproj).
- Tests live in [Affinity.Tests\Affinity.Tests.csproj](C:\Users\micha\dev\AffinitySimHub\Affinity.Tests\Affinity.Tests.csproj).

## Branch Naming

- Prefer short kebab-case branch names describing the work.
- Do not prefix branches with `codex/` unless the user explicitly asks for that convention.
- Example: `affinity-used-time`

## PR Naming

- Use concise PR titles in the form `<type>: <summary>`.
- Keep the summary short, imperative, and specific to the user-visible change.
- Match the repo's existing style, which commonly uses `patch:` for small behavior, workflow, or UI changes.
- Examples:
  - `patch: add cumulative used time tracking`
  - `patch: remove lap totals from Affinity summaries`
  - `patch: fix UI-thread summary refresh crash`

## Build And Test

- Preferred validation commands:
  - `dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist`
  - `dotnet build .\Affinity\Affinity.csproj /p:SimHubInstallPath=C:\does-not-exist`
- Use the `SimHubInstallPath=C:\does-not-exist` override when you want a clean validation run without copying into a live SimHub install.
- If the task touches runtime plugin behavior, run both tests and a plugin build when practical.

## SimHub Copy Workflow

- The plugin project has a post-build copy target that copies `Affinity.dll`, `Affinity.pdb`, and `ac_track_id_map.json` into the SimHub install when `SimHubInstallPath` exists.
- After changing plugin code, always copy the built plugin into SimHub if able.
- The default install path is:
  - `C:\Program Files (x86)\SimHub\`
- If SimHub is open and the DLL is locked, do not force around it.
- Instead:
  - report that the copy was blocked by the lock
  - ask the user to close or restart SimHub
  - retry the copy when appropriate

## Debugging And Runtime Validation

- SimHub runtime logs are usually the first place to check for plugin failures:
  - `C:\Program Files (x86)\SimHub\Logs\simhub.txt`
- Affinity data and debug logs are usually under:
  - `C:\Program Files (x86)\SimHub\PluginsData\Common\`
- When investigating telemetry issues, compare:
  - persisted JSON totals
  - Affinity debug logs
  - SimHub log exceptions

## Editing Guidance

- Keep runtime work in `DataUpdate()` lightweight.
- Avoid introducing file I/O or expensive processing in telemetry hot paths.
- Preserve compatibility with persisted plugin data when practical.
- Update tests when summary logic, persistence shape, or user-visible formatting changes.
- Update docs when behavior changes, especially around distance/time tracking rules.

## User-Facing Closeout

- When plugin changes were built and copied successfully, say so clearly.
- If validation was partial, state what ran and what did not.
- If SimHub deployment could not be completed, say why plainly and include the next needed step.
