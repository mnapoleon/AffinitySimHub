# Remove Enable Plugin Setting Design

## Summary

Remove Affinity's internal `Enable Plugin` setting and the exported `Affinity.Enabled` property. SimHub's own plugin enablement is the only supported on/off control. After this change, Affinity will run whenever SimHub loads it and telemetry is available.

## Goals

- Remove the redundant `Enable Plugin` checkbox from the Settings tab.
- Remove `EnablePlugin` from persisted settings and reset behavior.
- Remove the exported `Affinity.Enabled` property from the plugin.
- Simplify runtime status handling so the plugin only distinguishes between active telemetry and waiting for telemetry.

## Non-Goals

- Preserve compatibility for dashboards or formulas that reference `Affinity.Enabled`.
- Add a replacement in-plugin disable mechanism.
- Change SimHub's plugin installation or loading behavior.

## Current Behavior

- The Settings tab exposes an `Enable Plugin` checkbox.
- `AffinitySettings` persists `EnablePlugin` with a default of `true`.
- `AffinityPlugin` publishes `Affinity.Enabled` and checks `Settings.EnablePlugin` before processing telemetry.
- When the setting is off, the plugin reports `Plugin disabled` instead of processing incoming data.

## Proposed Design

### Settings Model

Remove `EnablePlugin` from `AffinitySettings` and from `Reset()`. Existing persisted settings that still contain the field may deserialize with an extra JSON property, but the plugin will no longer read or write it.

### Settings UI

Remove the `Enable Plugin` checkbox from the General section of the Settings tab. Keep the section layout clean and compact after removal, with the remaining rows preserving the current visual style.

### Plugin Runtime

Remove `Affinity.Enabled` property registration and updates from `AffinityPlugin`.

Update the telemetry gate so plugin execution depends only on normal runtime conditions such as `data.GameRunning` and `data.NewData`. When telemetry is unavailable, the status should follow the normal waiting path rather than a plugin-disabled path.

### Tests

Update settings tests to reflect the new defaults and reset behavior without `EnablePlugin`.

Add or adjust focused tests so the suite verifies the setting model no longer exposes `EnablePlugin` and the plugin project no longer relies on the redundant property path where practical.

## Risks And Mitigations

### Breaking `Affinity.Enabled` consumers

Any dashboard, formula, or property binding that references `Affinity.Enabled` will stop resolving after deployment.

Mitigation: keep the change explicit in commit/PR messaging and user-facing closeout so the behavior shift is unsurprising.

### Persisted settings compatibility

Older persisted settings may still include `EnablePlugin`.

Mitigation: .NET JSON deserialization should ignore the removed property when loading into the new settings model, so no migration step is required.

## Validation

- `dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist`
- `dotnet build .\Affinity\Affinity.csproj /p:SimHubInstallPath=C:\does-not-exist`
- Copy the rebuilt plugin into `C:\Program Files (x86)\SimHub\` and verify the Settings tab no longer shows `Enable Plugin`.
- Confirm the plugin still loads and reports normal waiting/active states in SimHub.
