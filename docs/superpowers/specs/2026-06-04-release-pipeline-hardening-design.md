# Release Pipeline Hardening Design

## Goal

Reduce supply-chain and user-trust risk in the GitHub build and release process without adding code-signing yet.

## Scope

This design covers:

- Pinning GitHub Actions in CI and release workflows to immutable commit SHAs.
- Pinning the Inno Setup package version used during release builds.
- Publishing a SHA-256 checksum alongside the installer artifact.
- Making it explicit in release notes that the installer is currently unsigned.

This design does not cover:

- Authenticode signing or certificate management.
- Splitting the release flow into separate build and publish workflows.
- Functional changes to the plugin or installer payload.

## Current State

The repository currently:

- Uses major-version GitHub Action tags in CI and release workflows.
- Installs Inno Setup during the release workflow through Chocolatey without a pinned package version.
- Publishes the built installer `.exe` directly to GitHub Releases.
- Does not publish a checksum artifact.
- Does not sign the installer.

## Proposed Changes

### 1. Immutable action pins

Replace floating major tags such as `actions/checkout@v4`, `actions/setup-dotnet@v4`, and `actions/github-script@v7` with their corresponding full commit SHAs in both workflow files.

This reduces the chance that a compromised or unexpectedly changed upstream action tag alters the build or release process.

### 2. Pinned Inno Setup install

Keep the current Chocolatey-based installation path for simplicity, but pin the `innosetup` package to a specific version in the release workflow.

This keeps the existing build shape intact while reducing the risk of silently picking up an unexpected package revision at release time.

### 3. Release checksum publication

After building the installer, compute a SHA-256 checksum for the generated `.exe`, write it to a sibling `.sha256` file, and upload both assets to the GitHub release.

This gives users a lightweight integrity check even before signing is introduced.

### 4. Explicit unsigned-artifact messaging

Update the generated release notes to include a short warning that the installer is unsigned and that users should verify the published SHA-256 hash.

This does not prevent antivirus false positives, but it makes the release posture honest and easier to reason about for users.

## File Changes

- Modify `.github/workflows/ci.yml`
  - Pin GitHub Actions to immutable SHAs.

- Modify `.github/workflows/release.yml`
  - Pin GitHub Actions to immutable SHAs.
  - Pin `innosetup` package version in Chocolatey install step.
  - Generate a SHA-256 file for the installer.
  - Upload the checksum file as a second release asset.
  - Add unsigned-artifact guidance to generated release notes.

- Modify `README.md`
  - Add a short note in the release/install guidance that GitHub release installers are currently unsigned and should be verified against the published SHA-256 checksum.

## Error Handling

- If the installer checksum file is not created, the release job should fail.
- If the installer asset already exists, the workflow should continue replacing it as it does today.
- If the checksum asset already exists, the workflow should also replace it to keep the release idempotent.

## Testing

Local validation for this change set should include:

- YAML syntax review of the updated workflows.
- A local `dotnet build .\Affinity\Affinity.csproj /p:SimHubInstallPath=C:\does-not-exist` to confirm repo changes did not break the project build.
- If practical, a dry review of the generated PowerShell logic for checksum creation and asset upload paths.

Full end-to-end validation of release publication will still require a GitHub Actions run because local execution cannot exercise the hosted release environment.

## Risks And Tradeoffs

- Pinning action SHAs adds maintenance overhead when actions need updates.
- Pinning the Chocolatey package version reduces surprise but still leaves Chocolatey in the trust path.
- Publishing hashes improves integrity verification, but it is not a substitute for code signing.

## Success Criteria

This work is successful when:

- CI and release workflows no longer rely on floating action tags.
- The release workflow uses a specific Inno Setup package version.
- GitHub Releases include both the installer `.exe` and a `.sha256` checksum file.
- Release notes clearly state that the installer is unsigned for now and recommend hash verification.
