# Settings Version Display Design

## Goal

Show the plugin version number in the Affinity Settings tab without exposing commit SHA or other build metadata.

## Scope

This change is limited to the existing Settings UI and the plugin property surface that backs it. It does not change persistence, release packaging, installer behavior, or any telemetry logic.

## Current Context

- The Settings tab UI is defined in `Affinity/AffinitySimHub.xaml`.
- The view is backed by `Affinity/AffinityPlugin.cs`.
- The plugin already resolves a version string via `ResolvePluginVersion()` and publishes it to SimHub through `Affinity.Version`.

## Chosen Approach

Add a read-only `Version` row to the existing `General` section in the Settings tab.

The displayed value will be the plugin's resolved semantic version string only. The UI will not display commit SHA, branch name, or informational build suffixes.

## UI Behavior

- The new row appears alongside the existing `Enable plugin` and `Distance units` settings.
- The left column label is `Version`.
- The right column shows a plain text version such as `0.1.2`.
- The value is display-only and cannot be edited.

## Implementation Notes

- Expose a public read-only property from `AffinityPlugin` for the version display string.
- Reuse the plugin's existing resolved version source rather than duplicating version parsing in the view.
- Bind the new text element in `AffinitySimHub.xaml` to that property.

## Testing

- Add a focused regression test that verifies the plugin exposes a non-empty version display value.
- Run the existing test suite and plugin build.
- Copy the built plugin into the local SimHub install if file locks allow it.

## Risks

- Very low risk. The change is read-only and limited to the settings view.
- The only meaningful failure mode is binding to the wrong property or surfacing a version string with extra metadata that we do not want to show.
