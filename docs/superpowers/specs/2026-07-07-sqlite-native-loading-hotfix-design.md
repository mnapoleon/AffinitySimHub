# SQLite Native Loading Hotfix Design

## Goal

Harden the `v0.2.1` Affinity release against SimHub updates that leave the plugin without a loadable `SQLite.Interop.dll`, while keeping the hotfix narrowly scoped for release risk.

## Problem Summary

Affinity `v0.2.1` uses `System.Data.SQLite.Core` for persistent storage. The managed assembly loads, but if the matching native `SQLite.Interop.dll` is missing from the deployed `x86` or `x64` folder, SQLite initialization fails during plugin startup. The plugin then falls back to an empty in-memory database, which makes user data appear to disappear.

Observed user symptom:

- Affinity tabs and stored data appear missing after a SimHub update.

Observed technical failure:

- `DllNotFoundException` for `SQLite.Interop.dll` during `InitializeDatabase()`.

## Constraints

- This is a hotfix on top of `v0.2.1`, so scope should stay tight.
- Existing `System.Data.SQLite` deployment layout must continue working.
- The fix should not require broader storage or schema changes.
- If native loading still fails, the plugin should fail clearly rather than looking empty without explanation.

## Options Considered

### Option 1: Clearer failure messaging only

Catch the SQLite startup failure and surface a better user-facing status plus clearer logs.

Pros:

- Lowest implementation risk.
- Minimal code change.

Cons:

- Does not improve resilience.
- Users still need reinstall/manual recovery every time the layout is disturbed.

### Option 2: Custom native loading plus clearer failure messaging

Before first SQLite use, explicitly load the native interop DLL from a controlled path, while preserving the standard deployment layout and adding a clear status/log fallback if SQLite still cannot initialize.

Pros:

- Improves resilience without replacing the storage stack.
- Keeps a clear diagnostic path if recovery still fails.
- Small enough to plausibly ship as a hotfix.

Cons:

- Adds Windows-specific startup logic.
- Needs careful architecture/path handling.

### Option 3: Replace `System.Data.SQLite` packaging/stack

Move away from the current native SQLite dependency approach entirely.

Pros:

- Could remove this class of deployment issue more thoroughly.

Cons:

- Too large and risky for a `v0.2.1` hotfix.
- Would require broader testing and likely release-process adjustments.

## Recommendation

Use Option 2 for the hotfix.

It gives the release a better chance to recover when the standard SQLite deployment layout is disturbed, while still giving users an explicit explanation and next step if recovery fails.

## Proposed Design

### Startup loading flow

Add a small startup helper that runs before `InitializeDatabase()` performs any SQLite work.

The helper should:

1. Detect the current process architecture.
2. Compute the expected interop path for that architecture.
3. Try to load `SQLite.Interop.dll` explicitly from that path.
4. Treat load failure as non-fatal at this step, because `InitializeDatabase()` remains the final authority on whether SQLite can start.

This helper is a fallback/guard, not a replacement for the current standard deployment layout.

### Path strategy

The hotfix should preserve the current deployed structure:

- `System.Data.SQLite.dll` at the plugin root
- `x86\\SQLite.Interop.dll`
- `x64\\SQLite.Interop.dll`

Custom loading should first target the existing plugin-root-relative `x86`/`x64` folders so the hotfix remains compatible with the current release packaging.

The hotfix may add one secondary probe only if an existing Affinity-owned path is already available in the current release layout and can be used without changing packaging. Otherwise, the hotfix should stay with the current plugin-root-relative `x86`/`x64` folders.

### Failure behavior

If SQLite still fails to initialize:

- log a clearer Affinity error explaining that the SQLite native dependency is missing or unloadable
- include a reinstall-after-SimHub-update recommendation in the log text
- set an explicit user-facing plugin status that indicates the install is incomplete and Affinity data cannot be loaded until the plugin is reinstalled

The important change is that the plugin should no longer fail into an “empty but seemingly healthy” state without explanation.

### User-facing wording

The status/log wording should be plain and actionable. The implementation should use wording equivalent to:

- Affinity install is incomplete; SQLite native files are missing or unloadable.
- Reinstall Affinity, especially after a SimHub update.

Exact wording can be finalized during implementation, but it should stay short and unambiguous.

## Testing Strategy

Add focused tests for the new hotfix behavior:

1. A test that verifies the plugin surfaces the explicit failure status when SQLite initialization fails.
2. A test that verifies the clearer logging/error-path behavior as much as the current test seams allow.
3. A narrow unit test for the architecture/path helper if it can be added without introducing platform-fragile assertions.

Validation for the hotfix should also include:

- `dotnet test .\\Affinity.Tests\\Affinity.Tests.csproj /p:SimHubInstallPath=C:\\does-not-exist`
- `dotnet build .\\Affinity\\Affinity.csproj /p:SimHubInstallPath=C:\\does-not-exist`

## Out Of Scope

- Replacing `System.Data.SQLite`
- Moving runtime data storage again
- Redesigning the Affinity persistence layer
- Changing SQLite package versions as part of this hotfix unless implementation proves that the current package cannot support the loading strategy

## Risks

- Explicit native loading can fail if the wrong architecture is selected.
- Explicit loading can fail if the actual runtime path differs from the assumed plugin layout.
- The plugin may still require reinstall after certain SimHub updates; this hotfix reduces ambiguity and may improve resilience, but does not guarantee immunity to host-side file cleanup.

## Success Criteria

- Affinity attempts explicit native SQLite loading before database initialization.
- A missing/unloadable SQLite native dependency produces a clear status and log message.
- Standard SQLite deployments continue to work unchanged.
- Tests and build pass on the hotfix branch.
