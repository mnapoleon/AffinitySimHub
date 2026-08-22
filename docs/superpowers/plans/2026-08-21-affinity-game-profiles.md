# Affinity Game Profiles Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Affinity's scattered game-name branches and duplicated supported-game metadata with a behavior-preserving game-profile registry modeled on StatsPlus.

**Architecture:** `IAffinityGameProfile` and an abstract base class define shared metadata, display, telemetry-classification, and distance-policy behavior. Nine concrete supported profiles override only their existing differences, while `AffinityPlugin` retains mutable session state, persistence, logging I/O, and WPF orchestration. Generic replay detection is extracted and invoked by the base profile implementation so `DataUpdate()` consumes one telemetry disposition.

**Tech Stack:** C# (`LangVersion=latest`), .NET Framework 4.8, SimHub plugin APIs, MSTest 3.4.3, WPF

**Spec:** `docs/superpowers/specs/2026-08-21-affinity-game-profiles-design.md`

## Global Constraints

- Preserve Affinity's nine existing supported games; do not add or remove game support.
- Preserve raw persisted `GameName`, `CarModel`, `TrackName`, and `TrackNameWithConfig` values and all SQLite identities.
- Preserve debug settings keys, game-specific log filenames, logo filenames, status behavior, telemetry thresholds, published SimHub properties, and visible circuit formatting.
- Keep mutable session/distance state, file I/O, storage access, logging I/O, and WPF updates out of profiles.
- Keep `DataUpdate()` lightweight; profile resolution and decisions must be in-memory operations.
- Do not add default interface implementations; Affinity targets `.NET Framework 4.8`.
- Do not introduce a dependency-injection container or a database migration.
- Before Task 1, use `superpowers:using-git-worktrees` to detect existing isolation and prefer an isolated worktree on the short kebab-case branch `affinity-game-profiles`. If worktree creation is unavailable or declined, create that feature branch in the current checkout; do not implement directly on `main`.
- Commit `docs/superpowers/specs/2026-08-21-affinity-game-profiles-design.md` and `docs/superpowers/plans/2026-08-21-affinity-game-profiles.md` with Task 1 so the branch and every review package contain the approved design and execution plan. If later tasks update either document, include those updates in the corresponding task commit.
- Run tests and builds serially to avoid `obj` and DLL locks.
- During normal deployment, do not overwrite the installed `ac_track_id_map.json`.
- Leave the pre-existing untracked files under `site/assets/screenshots/` untouched.

---

## Required Execution Setup

Complete this setup before Task 1. The implementation must run on a feature branch, preferably in an isolated worktree.

- [ ] **Detect whether the current checkout is already an isolated worktree**

Run:

```powershell
git status --short --branch
git rev-parse --absolute-git-dir
git rev-parse --path-format=absolute --git-common-dir
git rev-parse --show-superproject-working-tree
git branch --show-current
```

Interpretation:

- Different absolute git-dir and common-dir paths, with an empty superproject path, means the checkout is already a linked worktree. Continue there and ensure the branch is `affinity-game-profiles` or another user-approved short kebab-case feature branch.
- Matching git-dir and common-dir paths means this is the primary checkout. Prefer the app's native worktree mechanism; if none is available, follow the manual fallback below.
- A non-empty superproject path means this is a submodule, not an isolated worktree; treat it as a normal checkout.
- An empty branch name means detached `HEAD`; create a branch with the app's native **Create branch** control before committing.

- [ ] **Create the isolated worktree or feature branch**

Preferred: invoke the environment's native worktree creation mechanism with branch name `affinity-game-profiles`.

If no native mechanism exists, verify the project-local worktree directory is ignored before using Git's fallback:

```powershell
git check-ignore -q .worktrees
```

Expected: exit code `0`. If `.worktrees` is not ignored, add `/.worktrees/` to `.gitignore` and commit that narrow infrastructure change before creating the worktree.

Then run:

```powershell
git worktree add .worktrees\affinity-game-profiles -b affinity-game-profiles
```

If worktree creation is unavailable, declined, or blocked by the sandbox, create the branch in the current checkout instead:

```powershell
git checkout -b affinity-game-profiles
```

Do not continue implementation while `git branch --show-current` reports `main`.

- [ ] **Ensure the design and plan travel into the isolated workspace**

The design and plan may still be untracked in the primary checkout. Before implementation, confirm both exist in the selected worktree/branch:

```powershell
Test-Path .\docs\superpowers\specs\2026-08-21-affinity-game-profiles-design.md
Test-Path .\docs\superpowers\plans\2026-08-21-affinity-game-profiles.md
```

Expected: both commands print `True`. If a newly created worktree does not contain them, copy only these two planning artifacts from the primary checkout into the same relative paths in the worktree before Task 1; do not copy unrelated untracked files.

- [ ] **Verify a clean baseline in the selected workspace**

Run serially from the selected worktree/branch:

```powershell
git status --short --branch
dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist
dotnet build .\Affinity\Affinity.csproj /p:SimHubInstallPath=C:\does-not-exist
```

Expected: the branch is not `main`, only the approved plan/spec artifacts are initially untracked or changed, and both commands exit `0`. If baseline validation fails before production edits, stop and report the failure rather than attributing it to the profile migration.

---

## File Structure

- Create `Affinity/AffinityGameProfiles.cs`: contracts, context objects, enums, game-name normalization, base profile, registry, and unsupported fallback.
- Create `Affinity/AffinityGameProfileImplementations.cs`: the nine concrete supported profiles and all per-game overrides.
- Create `Affinity/AffinityReplayDetector.cs`: reusable generic replay detection and safe reflection-based telemetry inspection.
- Delete `Affinity/AffinityGameLogic.cs` after every consumer has migrated.
- Create `Affinity.Tests/AffinityGameProfileTests.cs`: registry, aliases, metadata, display, telemetry, and distance-policy unit coverage.
- Create `Affinity.Tests/AffinityGameProfileBoundaryTests.cs`: source-level regression coverage for the architectural boundary.
- Modify `Affinity/AffinityPlugin.cs`: resolve a profile once per update and consume profile metadata/classification/policies.
- Modify `Affinity/AffinitySummaryBuilder.cs`: obtain track and circuit display values from profiles.
- Modify affected tests in `Affinity.Tests/AffinityPluginReplayTests.cs`, `AffinityPluginDistanceSourceTests.cs`, `AffinityPluginAccTrackContextTests.cs`, `AffinityGameLogoTests.cs`, `AffinityGameLogicTests.cs`, `AffinitySummaryBuilderTests.cs`, and `AffinitySettingsTests.cs`.
- Modify `README.md`, `Affinity-distance-counting.md`, and `.codex/project-practices.md`: document the new ownership rule.

---

### Task 1: Add the profile contracts, registry, and display implementations

**Files:**
- Create: `Affinity/AffinityGameProfiles.cs`
- Create: `Affinity/AffinityGameProfileImplementations.cs`
- Create: `Affinity.Tests/AffinityGameProfileTests.cs`
- Modify later/delete later: `Affinity/AffinityGameLogic.cs`

**Interfaces:**
- Produces: `AffinityGameProfileRegistry.CreateDefault()`.
- Produces: `AffinityGameProfileRegistry.SupportedProfiles : IReadOnlyList<IAffinityGameProfile>`.
- Produces: `AffinityGameProfileRegistry.Resolve(string gameName) : IAffinityGameProfile`.
- Produces: `AffinityGameProfileRegistry.ResolveLogo(string gameName) : IAffinityGameProfile` for the existing logo-only `Le Mans Ultimate` variant.
- Produces: the final `IAffinityGameProfile` contract shown below; later tasks route existing plugin behavior through it.
- Consumes: no production types other than `GameData`, `StatusDataBase`, and `IReadOnlyDictionary<string,string>`.

- [ ] **Step 1: Add failing registry and metadata tests**

Create `Affinity.Tests/AffinityGameProfileTests.cs` with a test class in `namespace Affinity.Tests`. Add this matrix test first:

```csharp
[TestMethod]
public void Resolve_RecognizesAllSupportedAliasesAndCanonicalMetadata()
{
    AffinityGameProfileRegistry registry = AffinityGameProfileRegistry.CreateDefault();

    AssertProfile(registry, "Assetto Corsa", "assettocorsa", "Assetto Corsa", "244210.jpg");
    AssertProfile(registry, "AssettoCorsaCompetizione", "assettocorsacompetizione", "Assetto Corsa Competizione", "805550.jpg");
    AssertProfile(registry, "Assetto Corsa EVO", "assettocorsaevo", "Assetto Corsa EVO", "3058630.jpg");
    AssertProfile(registry, "Automobilista2", "automobilista2", "Automobilista 2", "1066890.jpg");
    AssertProfile(registry, "iRacing", "iracing", "iRacing", "iRacing.jpg");
    AssertProfile(registry, "LMU", "lmu", "Le Mans Ultimate", "2399420.jpg");
    AssertProfile(registry, "Project Motor Racing", "projectmotorracing", "Project Motor Racing", "299970.jpg");
    AssertProfile(registry, "RFactor2", "rfactor2", "rFactor 2", "365960.jpg");
    AssertProfile(registry, "R3E", "raceroomracingexperience", "RaceRoom Racing Experience", "211500.jpg");
    AssertProfile(registry, "RRRE", "raceroomracingexperience", "RaceRoom Racing Experience", "211500.jpg");
}

private static void AssertProfile(
    AffinityGameProfileRegistry registry,
    string alias,
    string settingsKey,
    string displayName,
    string logoFileName)
{
    IAffinityGameProfile profile = registry.Resolve(alias);
    Assert.IsTrue(profile.IsSupported, alias);
    Assert.AreEqual(settingsKey, profile.SettingsKey, alias);
    Assert.AreEqual(displayName, profile.DisplayName, alias);
    Assert.AreEqual(logoFileName, profile.LogoFileName, alias);
}
```

Add fallback and catalog-invariant tests:

```csharp
[TestMethod]
public void Resolve_ReturnsUnsupportedFallbackForMissingOrUnknownNames()
{
    AffinityGameProfileRegistry registry = AffinityGameProfileRegistry.CreateDefault();

    Assert.IsFalse(registry.Resolve(null).IsSupported);
    Assert.IsFalse(registry.Resolve(string.Empty).IsSupported);
    Assert.IsFalse(registry.Resolve("Unknown Game").IsSupported);
    Assert.AreEqual(string.Empty, registry.Resolve("Unknown Game").SettingsKey);
    Assert.IsFalse(registry.Resolve("Le Mans Ultimate").IsSupported);
    Assert.AreEqual("lmu", registry.ResolveLogo("Le Mans Ultimate").SettingsKey);
}

[TestMethod]
public void SupportedProfiles_HaveUniqueKeysNamesAndLogos()
{
    IAffinityGameProfile[] profiles = AffinityGameProfileRegistry.CreateDefault()
        .SupportedProfiles
        .ToArray();

    Assert.AreEqual(9, profiles.Length);
    Assert.AreEqual(9, profiles.Select(item => item.SettingsKey).Distinct().Count());
    Assert.AreEqual(9, profiles.Select(item => item.DisplayName).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    Assert.AreEqual(9, profiles.Select(item => item.LogoFileName).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    Assert.IsTrue(profiles.All(item => item.IsSupported));
}
```

- [ ] **Step 2: Run the focused tests and confirm they fail**

Run:

```powershell
dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist --filter AffinityGameProfileTests
```

Expected: compilation fails because `AffinityGameProfileRegistry` and `IAffinityGameProfile` do not exist.

- [ ] **Step 3: Create the final contracts and context objects**

Add these final public-to-the-assembly shapes to `Affinity/AffinityGameProfiles.cs`:

```csharp
internal enum TelemetryDisposition
{
    Active,
    Replay,
    Inactive,
    WaitingForContext
}

internal enum AffinityDistanceMode
{
    Automatic,
    Derived,
    StatefulDerived
}

internal sealed class CircuitDisplayParts
{
    public string CircuitNameDisplay { get; set; } = string.Empty;
    public string CircuitLayoutDisplay { get; set; } = string.Empty;
}

internal sealed class AffinityTrackDisplayContext
{
    public AffinityTrackDisplayContext(IReadOnlyDictionary<string, string> assettoCorsaTrackMap)
    {
        AssettoCorsaTrackMap = assettoCorsaTrackMap;
    }

    public IReadOnlyDictionary<string, string> AssettoCorsaTrackMap { get; }
}

internal sealed class AffinityGameRuntimeState
{
    public int Automobilista2PlayerViewedParticipantIndex { get; set; } = -1;

    public void Reset()
    {
        Automobilista2PlayerViewedParticipantIndex = -1;
    }
}

internal struct AffinityTelemetryContext
{
    public GameData GameData { get; set; }
    public StatusDataBase Status { get; set; }
    public string CarModel { get; set; } = string.Empty;
    public string TrackNameWithConfig { get; set; } = string.Empty;
    public AffinityGameRuntimeState RuntimeState { get; set; }
}

internal struct AffinityDistanceSampleContext
{
    public StatusDataBase Status { get; set; }
    public AffinityDistanceMode DistanceMode { get; set; }
    public int CompletedLaps { get; set; }
    public int LapDelta { get; set; }
    public double TrackLengthMeters { get; set; }
    public double DeltaTrackPositionMeters { get; set; }
    public double SessionMeters { get; set; }
    public double DeltaMeters { get; set; }
    public double SessionStatefulAbsoluteMeters { get; set; }
    public double SessionStartTrackPositionMeters { get; set; }
    public double LastTrackPositionWithinLapMeters { get; set; }
    public double LastObservedSessionMeters { get; set; }
    public double LastIgnoredSessionMeters { get; set; }
    public int LastObservedCompletedLaps { get; set; }
}
```

Define the final interface now so later tasks only add overrides and consumers:

```csharp
internal interface IAffinityGameProfile
{
    string SettingsKey { get; }
    string DisplayName { get; }
    string LogoFileName { get; }
    bool IsSupported { get; }
    bool Matches(string gameName);
    bool MatchesLogoName(string gameName);
    TelemetryDisposition EvaluateTelemetry(AffinityTelemetryContext context);
    string GetTrackDisplayName(string rawTrackNameWithConfig, AffinityTrackDisplayContext context);
    CircuitDisplayParts GetCircuitDisplayParts(string trackDisplayName);
    bool CanPromoteTrackContext(string previousTrackNameWithConfig, string updatedTrackNameWithConfig);
    AffinityDistanceMode DistanceMode { get; }
    bool CapturesSessionStartTrackPosition { get; }
    bool UsesStationaryStartupAnchor { get; }
    bool AcceptsInitialPositionSnap { get; }
    bool UsesLapCounterDistanceFloor { get; }
    bool ShouldIgnoreTransientReset(AffinityDistanceSampleContext context);
    bool ShouldIgnoreLowSpeedLineWrap(AffinityDistanceSampleContext context);
    bool ShouldIgnoreLapIncrement(AffinityDistanceSampleContext context);
    bool ShouldIgnorePlaceholderSessionStart(AffinityDistanceSampleContext context);
}
```

Implement `AffinityGameName.Normalize`, `AffinityGameProfileBase`, and `GenericAffinityGameProfile`. The supported base constructor accepts settings key, display name, logo filename, and runtime aliases. `MatchesLogoName` defaults to `Matches`; `LeMansUltimateProfile` additionally accepts normalized `lemansultimate` for logo lookup only. Base defaults must be `Active`, raw-track passthrough, generic `-` circuit splitting, no track promotion, `StatefulDerived`, and `false` for every optional distance capability/decision. The fallback overrides `IsSupported` to `false` and `DistanceMode` to `Automatic`.

- [ ] **Step 4: Implement the registry and nine profiles**

Use this exact registry order and metadata in `CreateDefault()`:

```csharp
return new AffinityGameProfileRegistry(new IAffinityGameProfile[]
{
    new AssettoCorsaProfile(),
    new AssettoCorsaCompetizioneProfile(),
    new AssettoCorsaEvoProfile(),
    new Automobilista2Profile(),
    new IRacingProfile(),
    new LeMansUltimateProfile(),
    new ProjectMotorRacingProfile(),
    new RFactor2Profile(),
    new RaceRoomProfile()
});
```

Use the aliases asserted in Step 1. Keep profile implementations in `AffinityGameProfileImplementations.cs`. Implement these display overrides immediately:

- Assetto Corsa classic maps `rawTrackNameWithConfig` through `context.AssettoCorsaTrackMap` and duplicates the resolved value into both circuit columns.
- ACC duplicates the display value into both circuit columns.
- LMU duplicates the display value into both circuit columns.
- rFactor 2 splits circuit display on `--`.
- iRacing uses generic `-` splitting, then title-cases the circuit name while preserving the token `GP`.
- All other profiles inherit generic `-` splitting.

Move the compact-track-code helpers from `AffinityGameLogic.IsAccTrackNameUpgrade` into the ACC profile and implement `CanPromoteTrackContext` with the same comparisons and thresholds.

- [ ] **Step 5: Add display and ACC-promotion tests**

Add tests that preserve Affinity's current behavior:

```csharp
[TestMethod]
public void TrackDisplay_MapsOnlyAssettoCorsaClassic()
{
    Dictionary<string, string> map = new Dictionary<string, string>
    {
        ["ks_brands_hatch-indy"] = "Brands Hatch - Indy"
    };
    AffinityTrackDisplayContext context = new AffinityTrackDisplayContext(map);
    AffinityGameProfileRegistry registry = AffinityGameProfileRegistry.CreateDefault();

    Assert.AreEqual("Brands Hatch - Indy", registry.Resolve("AssettoCorsa").GetTrackDisplayName("ks_brands_hatch-indy", context));
    Assert.AreEqual("ks_brands_hatch-indy", registry.Resolve("Assetto Corsa EVO").GetTrackDisplayName("ks_brands_hatch-indy", context));
    Assert.AreEqual("ks_brands_hatch-indy", registry.Resolve("AssettoCorsaCompetizione").GetTrackDisplayName("ks_brands_hatch-indy", context));
}

[TestMethod]
public void CircuitDisplay_PreservesExistingPerGameRules()
{
    AffinityGameProfileRegistry registry = AffinityGameProfileRegistry.CreateDefault();

    AssertParts(registry.Resolve("AssettoCorsa"), "monza_short", "monza_short", "monza_short");
    AssertParts(registry.Resolve("LMU"), "Le Mans - 24h", "Le Mans - 24h", "Le Mans - 24h");
    AssertParts(registry.Resolve("Automobilista2"), "Buenos_Aires-Buenos_Aires_Circuito_15", "Buenos Aires", "Buenos Aires Circuito 15");
    AssertParts(registry.Resolve("RFactor2"), "Lime Rock Park -- No Chicanes", "Lime Rock Park", "No Chicanes");
    AssertParts(registry.Resolve("iRacing"), "spielberg_gp-Grand Prix", "Spielberg GP", "Grand Prix");
}

[TestMethod]
public void AccProfile_PromotesOnlyCompactCodeToLongerDescriptiveTrack()
{
    IAffinityGameProfile profile = AffinityGameProfileRegistry.CreateDefault().Resolve("AssettoCorsaCompetizione");

    Assert.IsTrue(profile.CanPromoteTrackContext("barcelona", "Barcelona Grand Prix"));
    Assert.IsFalse(profile.CanPromoteTrackContext("Barcelona Grand Prix", "barcelona"));
    Assert.IsFalse(profile.CanPromoteTrackContext("barcelona", "barcelona"));
    Assert.IsFalse(AffinityGameProfileRegistry.CreateDefault().Resolve("AssettoCorsa")
        .CanPromoteTrackContext("barcelona", "Barcelona Grand Prix"));
}
```

Use an `AssertParts` helper that compares both `CircuitNameDisplay` and `CircuitLayoutDisplay`.

- [ ] **Step 6: Run focused tests**

Run the command from Step 2. Expected: all `AffinityGameProfileTests` pass while existing production consumers still use `AffinityGameLogic`.

- [ ] **Step 7: Commit the foundation**

```powershell
git add -- Affinity/AffinityGameProfiles.cs Affinity/AffinityGameProfileImplementations.cs Affinity.Tests/AffinityGameProfileTests.cs docs/superpowers/specs/2026-08-21-affinity-game-profiles-design.md docs/superpowers/plans/2026-08-21-affinity-game-profiles.md
git commit -m "patch: add Affinity game profiles"
```

Confirm the commit contains both planning artifacts:

```powershell
git show --name-only --format= HEAD | Select-String '2026-08-21-affinity-game-profiles'
```

Expected: both the design spec and implementation plan paths are listed.

---

### Task 2: Route display, logo, supported-game, and debug metadata through profiles

**Files:**
- Modify: `Affinity/AffinityPlugin.cs`
- Modify: `Affinity/AffinitySummaryBuilder.cs`
- Modify: `Affinity.Tests/AffinityGameLogoTests.cs`
- Modify: `Affinity.Tests/AffinitySummaryBuilderTests.cs`
- Modify: `Affinity.Tests/AffinitySettingsTests.cs`
- Modify: `Affinity.Tests/AffinityGameProfileTests.cs`

**Interfaces:**
- Consumes: `AffinityGameProfileRegistry.Resolve(string)` and `SupportedProfiles` from Task 1.
- Consumes: `IAffinityGameProfile.GetTrackDisplayName`, `GetCircuitDisplayParts`, and metadata properties.
- Produces: one `_gameProfiles` registry field in `AffinityPlugin`; all later runtime routing uses the same registry.

- [ ] **Step 1: Add failing metadata-routing tests**

Update logo tests to compare plugin behavior to the registry catalog:

```csharp
[TestMethod]
public void TryGetGameLogoFileName_MatchesSupportedProfileCatalog()
{
    foreach (IAffinityGameProfile profile in AffinityGameProfileRegistry.CreateDefault().SupportedProfiles)
    {
        Assert.AreEqual(profile.LogoFileName, AffinityPlugin.TryGetGameLogoFileName(profile.SettingsKey));
    }
}
```

Add a settings/debug-options test that invokes `RefreshGameDebugLoggingOptions` and asserts the resulting keys equal `SupportedProfiles.Select(profile => profile.SettingsKey)` in display-name order. Preserve the existing `GameDebugLoggingOption` assertions for labels and enabled values.

Add a summary regression test where raw track identity differs from display text:

```csharp
[TestMethod]
public void BuildSnapshot_UsesProfilesForDisplayWithoutChangingRawTrackIdentity()
{
    DistanceSummary row = new DistanceSummary
    {
        GameName = "AssettoCorsa",
        CarModel = "Mazda MX-5 Cup",
        TrackName = "ks_brands_hatch",
        TrackNameWithConfig = "ks_brands_hatch-indy",
        TotalDistanceKm = 10.0
    };
    Dictionary<string, string> map = new Dictionary<string, string>
    {
        ["ks_brands_hatch-indy"] = "Brands Hatch - Indy"
    };

    AffinitySummarySnapshot snapshot = AffinitySummaryBuilder.BuildSnapshot(new[] { row }, false, map);
    TrackDistanceSummary track = snapshot.GameTabs.Single().TrackSummaries.Single();

    Assert.AreEqual("ks_brands_hatch-indy", track.TrackName);
    Assert.AreEqual("Brands Hatch - Indy", track.TrackDisplayName);
    Assert.AreEqual("Brands Hatch - Indy", track.CircuitNameDisplay);
    Assert.AreEqual("Brands Hatch - Indy", track.CircuitLayoutDisplay);
}
```

- [ ] **Step 2: Run the focused metadata/display tests and confirm failure**

Run:

```powershell
dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist --filter "AffinityGameLogoTests|AffinitySummaryBuilderTests|AffinitySettingsTests"
```

Expected: the new catalog-routing tests fail because plugin and summary code still use local dictionaries and `AffinityGameLogic`.

- [ ] **Step 3: Replace plugin metadata catalogs with the registry**

In `AffinityPlugin`:

```csharp
private static readonly AffinityGameProfileRegistry DefaultGameProfiles =
    AffinityGameProfileRegistry.CreateDefault();

private readonly AffinityGameProfileRegistry _gameProfiles = DefaultGameProfiles;
```

Remove `GameLogoFileNames` and `DefaultGameDebugLoggingEntries`. Change `IsSupportedGame` to resolve `IsSupported`. Change `TryGetGameLogoFileName` to call `ResolveLogo(gameName)` and return `profile.LogoFileName` only when a logo profile is found. Preserve `Le Mans Ultimate` as a logo-only variant while `Resolve("Le Mans Ultimate")` remains unsupported. Remove `NormalizeGameLogoLookupName`.

Change debug initialization and option refresh to iterate `SupportedProfiles`:

```csharp
foreach (IAffinityGameProfile profile in _gameProfiles.SupportedProfiles)
{
    if (!Settings.GameDebugLogging.ContainsKey(profile.SettingsKey))
    {
        Settings.GameDebugLogging[profile.SettingsKey] = false;
    }
}
```

```csharp
foreach (IAffinityGameProfile profile in _gameProfiles.SupportedProfiles.OrderBy(item => item.DisplayName))
{
    bool enabled = Settings.GameDebugLogging.TryGetValue(profile.SettingsKey, out bool configured) && configured;
    GameDebugLoggingOptions.Add(new GameDebugLoggingOption(
        profile.SettingsKey,
        profile.DisplayName,
        enabled,
        UpdateGameDebugLoggingSetting));
}
```

Route debug settings-key lookup through `Resolve(gameName).SettingsKey`. Remove the display-name switch. Keep unsupported-key removal and existing settings serialization behavior.

- [ ] **Step 4: Route summary display through profiles**

Add an optional registry parameter to the final `BuildSnapshot` overload and resolve a default when null. In the initial summary projection:

```csharp
IAffinityGameProfile profile = gameProfiles.Resolve(summary.GameName);
string rawDisplay = string.IsNullOrWhiteSpace(summary.TrackDisplayName)
    ? summary.TrackNameWithConfig
    : summary.TrackDisplayName;
```

Set `TrackDisplayName` using `profile.GetTrackDisplayName(rawDisplay, trackDisplayContext)`. In `BuildTrackDistanceSummary`, call `profile.GetCircuitDisplayParts(trackDisplayName)` and assign both returned display fields.

Delete `UsesSameCircuitNameAndLayoutDisplay`, `SplitCircuitDisplay`, `NormalizeCircuitDisplayPart`, and `ToCircuitTitleCase` from `AffinitySummaryBuilder` after the profile tests cover them. Ensure all overloads delegate with the same registry so no call site silently reverts to old logic.

- [ ] **Step 5: Run metadata/display tests and the complete suite**

Run serially:

```powershell
dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist --filter "AffinityGameLogoTests|AffinitySummaryBuilderTests|AffinitySettingsTests|AffinityGameProfileTests"
dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist
```

Expected: both commands pass. Inspect failures for raw/display identity confusion before changing expectations; stored identity must remain raw.

- [ ] **Step 6: Commit metadata and display routing**

```powershell
git add -- Affinity/AffinityPlugin.cs Affinity/AffinitySummaryBuilder.cs Affinity.Tests/AffinityGameLogoTests.cs Affinity.Tests/AffinitySummaryBuilderTests.cs Affinity.Tests/AffinitySettingsTests.cs Affinity.Tests/AffinityGameProfileTests.cs
git commit -m "patch: route Affinity metadata through profiles"
```

---

### Task 3: Share generic replay classification through the profile base

**Files:**
- Create: `Affinity/AffinityReplayDetector.cs`
- Modify: `Affinity/AffinityGameProfiles.cs`
- Modify: `Affinity/AffinityGameProfileImplementations.cs`
- Modify: `Affinity/AffinityPlugin.cs`
- Modify: `Affinity.Tests/AffinityGameProfileTests.cs`
- Modify: `Affinity.Tests/AffinityPluginReplayTests.cs`

**Interfaces:**
- Consumes: `IAffinityGameProfile.EvaluateTelemetry(AffinityTelemetryContext)` from Task 1.
- Produces: `AffinityReplayDetector.IsReplay(GameData data) : bool`.
- Produces: one `AffinityGameRuntimeState` instance owned and reset by the plugin.
- Produces: `TelemetryDisposition.Active`, `Replay`, `Inactive`, and `WaitingForContext` as the sole telemetry-eligibility result consumed by `DataUpdate()`.

- [ ] **Step 1: Move generic replay expectations to direct detector/profile tests**

Replace reflection calls to the soon-to-be-removed private `AffinityPlugin.IsReplayTelemetry` with direct assertions against `AffinityReplayDetector.IsReplay`. Keep every current test case: game-level replay flag, game replay flag, active replay mode, live replay mode, status replay flag, status replay mode, raw `IsReplayPlaying`, and nested telemetry `IsReplayPlaying`.

Add this base-profile integration test:

```csharp
[TestMethod]
public void EvaluateTelemetry_UsesGenericReplayDetectionForSupportedAndFallbackProfiles()
{
    GameData replayData = CreateGameDataWithStatus(new ReplayStatusData { IsGameReplay = true });
    AffinityTelemetryContext context = new AffinityTelemetryContext
    {
        GameData = replayData,
        Status = replayData.NewData,
        RuntimeState = new AffinityGameRuntimeState()
    };
    AffinityGameProfileRegistry registry = AffinityGameProfileRegistry.CreateDefault();

    Assert.AreEqual(TelemetryDisposition.Replay, registry.Resolve("iRacing").EvaluateTelemetry(context));
    Assert.AreEqual(TelemetryDisposition.Replay, registry.Resolve("Unknown Game").EvaluateTelemetry(context));
}
```

Add direct profile tests for AMS2 garage/spectator/replay-state/viewed-participant behavior, RaceRoom finish/garage behavior, and LMU missing-context behavior. Verify that `AffinityGameRuntimeState.Reset()` allows a new AMS2 participant index to become the learned player index after game stop.

- [ ] **Step 2: Run replay/profile tests and confirm failure**

```powershell
dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist --filter "AffinityPluginReplayTests|AffinityGameProfileTests"
```

Expected: compilation fails because `AffinityReplayDetector` does not exist and profile telemetry evaluation still returns `Active`.

- [ ] **Step 3: Extract safe telemetry inspection and replay detection**

Create `AffinityReplayDetector.cs` and move the existing behavior without changing probe order. Keep reflection case-insensitive and tolerant of missing members. The detector entry point is:

```csharp
internal static bool IsReplay(GameData data)
```

Keep reusable inspection methods internal so profiles and centralized debug logging can use them:

```csharp
internal static object GetRawStatusDataObject(StatusDataBase status);
internal static bool TryGetMemberValue(object source, string memberName, out object value);
internal static bool TryGetBooleanMemberValue(object source, string memberName, out bool value);
internal static bool TryGetIntegerMemberValue(object source, string memberName, out int value);
internal static bool TryGetBooleanValue(object value, out bool result);
```

Keep `IsReplayModeActive` private to the detector. Preserve the accepted inactive values `None`, `Off`, `Disabled`, and `Live`.

- [ ] **Step 4: Implement base and concrete telemetry classification**

Change the base implementation to:

```csharp
public virtual TelemetryDisposition EvaluateTelemetry(AffinityTelemetryContext context)
{
    return AffinityReplayDetector.IsReplay(context.GameData)
        ? TelemetryDisposition.Replay
        : TelemetryDisposition.Active;
}
```

Each concrete override must call `base.EvaluateTelemetry(context)` and immediately return non-active results.

Implement AMS2's existing checks in this order: `IsInGarage`, `IsSpectator`, raw `mGameState == 6`, then learned `mViewedParticipantIndex`. Read and update `context.RuntimeState.Automobilista2PlayerViewedParticipantIndex` rather than profile fields.

Implement RaceRoom's existing raw `FinishStatus` and `GamePlayerInGarage` checks. Implement LMU's `WaitingForContext` result when car is missing/`Unknown Car` or track is missing/`Unknown Track`.

- [ ] **Step 5: Route `DataUpdate()` through one telemetry disposition**

Resolve the profile immediately after normalizing context values:

```csharp
IAffinityGameProfile profile = _gameProfiles.Resolve(gameName);
TelemetryDisposition disposition = profile.EvaluateTelemetry(new AffinityTelemetryContext
{
    GameData = data,
    Status = data.NewData,
    CarModel = carModel,
    TrackNameWithConfig = trackNameWithConfig,
    RuntimeState = _gameRuntimeState
});
```

Preserve current ordering:

1. Handle `Replay` and `Inactive` using the current finalization/reset/property-publication paths and status messages.
2. Reject `!profile.IsSupported` with `Unsupported game: {gameName}`.
3. Handle `WaitingForContext` with `Waiting for {gameName} car/track telemetry`.
4. Continue active processing.

Replace `_automobilista2PlayerViewedParticipantIndex` with `_gameRuntimeState.Automobilista2PlayerViewedParticipantIndex` in debug snapshots. Call `_gameRuntimeState.Reset()` whenever `!data.GameRunning`, matching the current reset point.

Delete `IsReplayTelemetry`, `IsInactiveTelemetry`, `IsAutomobilista2InactiveTelemetry`, `IsRaceRoomInactiveTelemetry`, and the moved reflection/replay helpers from `AffinityPlugin` only after all remaining debug code calls the extracted safe helpers.

- [ ] **Step 6: Run replay tests, full tests, and a no-deploy build**

```powershell
dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist --filter "AffinityPluginReplayTests|AffinityGameProfileTests"
dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist
dotnet build .\Affinity\Affinity.csproj /p:SimHubInstallPath=C:\does-not-exist
```

Expected: all pass. The focused debug-log test must still contain `ams2PlayerViewedParticipantIndex=<value>`.

- [ ] **Step 7: Commit telemetry classification**

```powershell
git add -- Affinity/AffinityReplayDetector.cs Affinity/AffinityGameProfiles.cs Affinity/AffinityGameProfileImplementations.cs Affinity/AffinityPlugin.cs Affinity.Tests/AffinityGameProfileTests.cs Affinity.Tests/AffinityPluginReplayTests.cs
git commit -m "patch: classify Affinity telemetry through profiles"
```

---

### Task 4: Route session setup and structural distance capabilities through profiles

**Files:**
- Modify: `Affinity/AffinityGameProfileImplementations.cs`
- Modify: `Affinity/AffinityPlugin.cs`
- Modify: `Affinity.Tests/AffinityGameProfileTests.cs`
- Modify: `Affinity.Tests/AffinityPluginDistanceSourceTests.cs`
- Modify: `Affinity.Tests/AffinityPluginAccTrackContextTests.cs`

**Interfaces:**
- Consumes: `DistanceMode`, `CapturesSessionStartTrackPosition`, `UsesStationaryStartupAnchor`, `AcceptsInitialPositionSnap`, `UsesLapCounterDistanceFloor`, and `CanPromoteTrackContext`.
- Produces: plugin distance helpers that accept an `IAffinityGameProfile` instead of a game-name string.
- Preserves: all mutable fields `_sessionStatefulAbsoluteMeters`, `_sessionStartTrackPositionMeters`, `_lastTrackPositionWithinLapMeters`, `_lastObservedSessionMeters`, `_lastIgnoredSessionMeters`, and `_lastObservedCompletedLaps` in `AffinityPlugin`.

- [ ] **Step 1: Add failing capability tests**

```csharp
[TestMethod]
public void DistanceCapabilities_MatchExistingGameBranches()
{
    AffinityGameProfileRegistry registry = AffinityGameProfileRegistry.CreateDefault();

    Assert.IsTrue(registry.SupportedProfiles.All(item => item.DistanceMode == AffinityDistanceMode.StatefulDerived));
    Assert.IsTrue(registry.Resolve("Automobilista2").CapturesSessionStartTrackPosition);
    Assert.IsTrue(registry.Resolve("ProjectMotorRacing").CapturesSessionStartTrackPosition);
    Assert.IsTrue(registry.Resolve("ProjectMotorRacing").UsesStationaryStartupAnchor);
    Assert.IsTrue(registry.Resolve("Automobilista2").AcceptsInitialPositionSnap);
    Assert.IsTrue(registry.Resolve("ProjectMotorRacing").AcceptsInitialPositionSnap);
    Assert.IsTrue(registry.Resolve("RaceRoom Racing Experience").UsesLapCounterDistanceFloor);
    Assert.IsFalse(registry.Resolve("iRacing").UsesLapCounterDistanceFloor);
    Assert.AreEqual(AffinityDistanceMode.Automatic, registry.Resolve("Unknown").DistanceMode);
}
```

Update distance-source tests to resolve a profile and pass it to private helpers through reflection. Keep the expected `Derived` source and stateful absolute-meter results for all existing supported game cases. Update ACC context-promotion tests to assert the plugin delegates to `profile.CanPromoteTrackContext` while preserving bucket merge behavior.

- [ ] **Step 2: Run focused distance/context tests and confirm failure**

```powershell
dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist --filter "AffinityGameProfileTests|AffinityPluginDistanceSourceTests|AffinityPluginAccTrackContextTests"
```

Expected: capability assertions fail because all optional base values are still false and plugin helpers still accept game-name strings.

- [ ] **Step 3: Set concrete capability overrides**

Override exactly these properties:

```csharp
// Automobilista2Profile
public override bool CapturesSessionStartTrackPosition => true;
public override bool AcceptsInitialPositionSnap => true;

// ProjectMotorRacingProfile
public override bool CapturesSessionStartTrackPosition => true;
public override bool UsesStationaryStartupAnchor => true;
public override bool AcceptsInitialPositionSnap => true;

// RaceRoomProfile
public override bool UsesLapCounterDistanceFloor => true;
```

Do not add per-game overrides for supported profiles' `DistanceMode`; the supported base supplies `StatefulDerived`, and only the fallback supplies `Automatic`.

- [ ] **Step 4: Pass the resolved profile through session setup**

Change these helpers to accept `IAffinityGameProfile profile` instead of `string gameName`:

- `TryPromoteAccTrackContext`
- `ResolveSessionDistanceSource`
- `GetAbsoluteSessionDistanceMeters`
- `ShouldUseZeroSessionOrigin`
- `UpdateStatefulDerivedAbsoluteSessionDistanceMeters`
- `ShouldIgnoreStatefulStartupPlaceholder`
- `GetSessionStartTrackPositionMeters`

Map distance mode to the existing private `SessionDistanceSource` enum:

```csharp
if (profile.DistanceMode == AffinityDistanceMode.StatefulDerived ||
    profile.DistanceMode == AffinityDistanceMode.Derived)
{
    return SessionDistanceSource.Derived;
}
```

Use the existing automatic SessionOdo unit inference only for `Automatic`. In `GetAbsoluteSessionDistanceMeters`, return `_sessionStatefulAbsoluteMeters` when the source is derived and `profile.DistanceMode == StatefulDerived`. `ShouldUseZeroSessionOrigin` returns true only for `AffinityDistanceMode.Derived`.

Use `CapturesSessionStartTrackPosition` instead of AMS2/PMR checks. Use `UsesStationaryStartupAnchor` to gate the existing PMR startup-anchor algorithm. Use `AcceptsInitialPositionSnap` instead of excluding AMS2 and PMR by name. Use `UsesLapCounterDistanceFloor` instead of the RaceRoom check. Do not alter any numeric condition.

- [ ] **Step 5: Resolve once and reuse the profile throughout each update**

Use the `profile` already resolved for telemetry classification for context promotion, distance-source selection, session-origin calculation, stateful update, and initial-snap handling. Do not call `_gameProfiles.Resolve` repeatedly inside the telemetry hot path.

Keep raw `gameName` in `BuildContextKey`, buckets, repository writes, status text, debug records, and published properties.

- [ ] **Step 6: Run focused and full validation**

```powershell
dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist --filter "AffinityGameProfileTests|AffinityPluginDistanceSourceTests|AffinityPluginAccTrackContextTests"
dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist
```

Expected: all pass with unchanged distance values in the existing detailed regression tests.

- [ ] **Step 7: Commit structural distance routing**

```powershell
git add -- Affinity/AffinityGameProfileImplementations.cs Affinity/AffinityPlugin.cs Affinity.Tests/AffinityGameProfileTests.cs Affinity.Tests/AffinityPluginDistanceSourceTests.cs Affinity.Tests/AffinityPluginAccTrackContextTests.cs
git commit -m "patch: route Affinity distance capabilities through profiles"
```

---

### Task 5: Move per-game distance anomaly decisions into concrete profiles

**Files:**
- Modify: `Affinity/AffinityGameProfileImplementations.cs`
- Modify: `Affinity/AffinityPlugin.cs`
- Modify: `Affinity.Tests/AffinityGameProfileTests.cs`
- Modify: `Affinity.Tests/AffinityPluginDistanceSourceTests.cs`

**Interfaces:**
- Consumes: `AffinityDistanceSampleContext` snapshots built by the plugin.
- Produces: concrete implementations of `ShouldIgnoreTransientReset`, `ShouldIgnoreLowSpeedLineWrap`, `ShouldIgnoreLapIncrement`, and `ShouldIgnorePlaceholderSessionStart`.
- Preserves: generic repeated-jump and distance-bucket update logic in the plugin.

- [ ] **Step 1: Add direct profile regression tests for every anomaly**

Move the input values and expected decisions from existing private-helper tests into direct profile tests. Cover at least these cases:

- iRacing returns true for the current stopped-car zero-drop fixture and false for the same fixture under another profile.
- rFactor 2 returns true for the current low-speed near-line wrap fixture.
- rFactor 2 returns true for the current low-speed false lap-increment fixture.
- LMU returns true for the current exit-line false lap-increment fixture.
- LMU returns true for each current placeholder fixture: prior ignored marker, negative lap-boundary sentinel, and reset SessionOdo.
- Other profiles return false for those same contexts.

Construct contexts explicitly. For example:

```csharp
AffinityDistanceSampleContext context = new AffinityDistanceSampleContext
{
    Status = status,
    DistanceMode = AffinityDistanceMode.StatefulDerived,
    CompletedLaps = 4,
    LapDelta = 1,
    TrackLengthMeters = 4535.80,
    LastObservedSessionMeters = 13529.19,
    LastIgnoredSessionMeters = -1.0
};

Assert.IsTrue(registry.Resolve("LMU").ShouldIgnoreLapIncrement(context));
Assert.IsFalse(registry.Resolve("iRacing").ShouldIgnoreLapIncrement(context));
```

- [ ] **Step 2: Run focused tests and confirm failure**

```powershell
dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist --filter "AffinityGameProfileTests|AffinityPluginDistanceSourceTests"
```

Expected: direct anomaly tests fail because base implementations return false.

- [ ] **Step 3: Implement iRacing, rFactor 2, and LMU decisions in their profiles**

Copy the existing conditions without changing constants:

- `IRacingProfile.ShouldIgnoreTransientReset` uses derived/stateful mode, previous completed laps greater than zero, previous session distance greater than `max(100, trackLength * 0.25)`, current completed laps zero, speed below `1.0`, position meters at most `1.0`, and position percent at most `0.001`.
- `RFactor2Profile.ShouldIgnoreLowSpeedLineWrap` requires an absolute position delta above half a lap, completed laps zero, speed at most `80.0`, and position within `5.0` meters of either line boundary.
- `RFactor2Profile.ShouldIgnoreLapIncrement` requires a positive lap delta, completed laps above zero, speed below `5.0`, position within `5.0` meters of the line, and previous session distance at least one track length.
- `LeMansUltimateProfile.ShouldIgnoreLapIncrement` requires a positive lap delta, completed laps above zero, speed below `1.0`, position within `max(100, trackLength * 0.025)` after the line or `5.0` before it, and previous distance at least one track length.
- `LeMansUltimateProfile.ShouldIgnorePlaceholderSessionStart` preserves the negative sentinel, near-line, ignored-marker, and reset-SessionOdo conditions exactly.

Use a shared protected profile helper for track position clamping if it removes duplication; it must preserve `AffinityGameLogic.GetTrackPositionWithinLapMeters` behavior, including percent values expressed as either `0..1` or `0..100`.

- [ ] **Step 4: Build distance snapshots at the existing decision stages and delegate decisions**

Before the active-context placeholder-start check, build a context containing status, completed laps, track length, and the current ignored/session state for `profile.ShouldIgnorePlaceholderSessionStart(context)`.

At the start of the continuing-session branch, build a context containing status, distance mode, completed laps, track length, and prior observation state for `profile.ShouldIgnoreTransientReset(context)`.

Inside `UpdateStatefulDerivedAbsoluteSessionDistanceMeters`, build a context after calculating `deltaTrackPositionMeters`; populate the status, profile mode, track length, position delta, and current state fields before calling `profile.ShouldIgnoreLowSpeedLineWrap(context)`.

After calculating session meters, delta meters, and lap delta, build a pre-distance-mutation context for `profile.ShouldIgnoreLapIncrement(context)` and the generic distance-jump decision. After the existing distance branch mutates or preserves observation state, build a fresh post-distance-mutation context and evaluate `profile.ShouldIgnoreLapIncrement(context)` again at the existing lap-transition stage. These snapshots are structs, so correct decision timing does not introduce per-sample heap allocations.

Replace the iRacing zero-drop helper call with `profile.ShouldIgnoreTransientReset(context)`. Replace the low-speed line-wrap branch in the stateful updater with `profile.ShouldIgnoreLowSpeedLineWrap(context)`. Replace the first call to `LooksLikeIgnoredLapIncrement` with the pre-mutation profile result used by `ShouldIgnoreDistanceJumpForIgnoredLapIncrement`; keep that helper as generic plugin logic with the existing half-track delta threshold. Replace the second legacy call with a fresh profile decision over the post-mutation snapshot at the lap-transition stage. Do not reuse either result across these two stages because the intervening distance branch can update the supplied observation state.

Replace the LMU placeholder helper with `profile.ShouldIgnorePlaceholderSessionStart(context)`. Keep `ShouldIgnoreRepeatedIgnoredDistanceJump` generic because it does not depend on a game.

Keep existing status text and debug reasons, including `Ignoring transient iRacing telemetry reset`, `lap-distance-ignored`, and `lap-increment-ignored`.

- [ ] **Step 5: Remove remaining private game wrappers and run a source search**

Remove these wrappers after all call sites are gone:

```text
IsAssettoCorsaGame
IsAssettoCorsaCompetizioneGame
IsRaceRoomGame
IsAutomobilista2Game
IsProjectMotorRacingGame
IsIRacingGame
IsRFactor2Game
IsLmuGame
UsesStatefulDerivedDistance
LooksLikeTransientIracingZeroDrop
LooksLikeIgnoredLowSpeedLineWrap
LooksLikeIgnoredLapIncrement
ShouldIgnorePlaceholderSessionStart
```

Run:

```powershell
rg -n "Is(Assetto|RaceRoom|Automobilista|ProjectMotor|IRacing|RFactor|Lmu).*Game|AffinityGameLogic" .\Affinity -g "*.cs"
```

Expected: only intentional references in files not yet migrated in Task 6; no game-name classifier remains in `AffinityPlugin`.

- [ ] **Step 6: Run the focused and full test suites**

```powershell
dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist --filter "AffinityGameProfileTests|AffinityPluginDistanceSourceTests"
dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist
```

Expected: all pass. Do not weaken detailed numeric assertions to make the refactor pass.

- [ ] **Step 7: Commit anomaly routing**

```powershell
git add -- Affinity/AffinityGameProfileImplementations.cs Affinity/AffinityPlugin.cs Affinity.Tests/AffinityGameProfileTests.cs Affinity.Tests/AffinityPluginDistanceSourceTests.cs
git commit -m "patch: move Affinity distance rules into profiles"
```

---

### Task 6: Remove the legacy logic class and enforce the profile boundary

**Files:**
- Delete: `Affinity/AffinityGameLogic.cs`
- Delete or replace: `Affinity.Tests/AffinityGameLogicTests.cs`
- Create: `Affinity.Tests/AffinityGameProfileBoundaryTests.cs`
- Modify: any remaining production/test consumers reported by `rg`

**Interfaces:**
- Consumes: profile normalization, matching, track display, circuit display, context promotion, telemetry classification, and distance policies from Tasks 1–5.
- Produces: a production tree with no `AffinityGameLogic` dependency and no `IsXGame` classifier in `AffinityPlugin` or `AffinitySummaryBuilder`.

- [ ] **Step 1: Search for every legacy consumer**

Run:

```powershell
rg -n "AffinityGameLogic|NormalizeGameName|GetDebugLoggingSettingsKey|IsSupportedGame|Is[A-Za-z0-9]+Game" .\Affinity .\Affinity.Tests -g "*.cs"
```

Classify each hit. Production game classification must resolve a profile. Tests for normalization, aliases, track display, track position conversion, context validity, and ACC promotion must live under `AffinityGameProfileTests` or a generic distance-helper test.

- [ ] **Step 2: Preserve the generic track-position test coverage**

If `GetTrackPositionWithinLapMeters` moved into the base profile helper, expose it as an internal static helper owned by the profile/distance support code and migrate the current tests unchanged. Preserve cases for meters, percent-as-fraction, percent-as-whole-number, clamping, invalid status, and raw positions greater than reported track length.

- [ ] **Step 3: Add a failing source-boundary regression test**

Create `AffinityGameProfileBoundaryTests.cs` using the repository-root discovery pattern already present in `AffinityAssemblyReferenceTests`:

```csharp
[TestMethod]
public void PluginAndSummaryBuilder_DoNotClassifyGamesDirectly()
{
    string root = FindRepositoryRoot();
    string plugin = File.ReadAllText(Path.Combine(root, "Affinity", "AffinityPlugin.cs"));
    string summaries = File.ReadAllText(Path.Combine(root, "Affinity", "AffinitySummaryBuilder.cs"));

    string[] forbidden =
    {
        "IsAssettoCorsaGame",
        "IsRaceRoomGame",
        "IsAutomobilista2Game",
        "IsProjectMotorRacingGame",
        "IsIRacingGame",
        "IsRFactor2Game",
        "IsLmuGame",
        "AffinityGameLogic"
    };

    foreach (string value in forbidden)
    {
        StringAssert.DoesNotContain(plugin, value);
        StringAssert.DoesNotContain(summaries, value);
    }
}
```

Implement `FindRepositoryRoot()` with the same `.git` directory walk and explicit assertion used in `AffinityAssemblyReferenceTests`.

- [ ] **Step 4: Delete legacy files and repair remaining consumers**

Delete `Affinity/AffinityGameLogic.cs`. Delete `Affinity.Tests/AffinityGameLogicTests.cs` only after every behavior assertion has moved to profile or generic helper tests. Replace any remaining normalization call with `AffinityGameName.Normalize` only in profile infrastructure or non-classification debug formatting; application decisions must use `Resolve`.

- [ ] **Step 5: Run boundary search and all tests**

```powershell
rg -n "AffinityGameLogic|Is(Assetto|RaceRoom|Automobilista|ProjectMotor|IRacing|RFactor|Lmu).*Game" .\Affinity -g "*.cs"
dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist
```

Expected: the search returns no hits and all tests pass.

- [ ] **Step 6: Commit cleanup and the architecture guard**

```powershell
git add -- Affinity Affinity.Tests
git commit -m "patch: complete Affinity game profile routing"
```

Before committing, inspect `git diff --cached --name-status` and unstage anything outside the intended production/test files.

---

### Task 7: Document the profile ownership rule

**Files:**
- Modify: `README.md`
- Modify: `Affinity-distance-counting.md`
- Modify: `.codex/project-practices.md`
- Keep: `docs/superpowers/specs/2026-08-21-affinity-game-profiles-design.md`
- Keep: `docs/superpowers/plans/2026-08-21-affinity-game-profiles.md`

**Interfaces:**
- Consumes: the completed `IAffinityGameProfile` boundary.
- Produces: contributor guidance that keeps future game additions complete across aliases, metadata, display, telemetry, distance, logging, logos, and tests.

- [ ] **Step 1: Update the README architecture/support guidance**

Add a concise “Game profiles” paragraph explaining that each supported simulator has an `IAffinityGameProfile`, `SupportedProfiles` is the supported-game catalog, and raw stored identities are not canonicalized through profiles.

- [ ] **Step 2: Update distance-counting documentation with the ownership matrix**

Document the existing rules, not new behavior:

```text
Shared: stateful derived distance for all supported profiles; generic replay classification.
iRacing: transient stopped-car zero reset.
LMU: reliable-context gate, false exit-line lap increments, placeholder session starts.
rFactor 2: low-speed false line wraps and lap increments.
RaceRoom: garage/finished telemetry and lap-counter floor.
AMS2: garage/spectator/replay/viewed-participant filtering and initial-position handling.
Project Motor Racing: stationary startup anchor and initial-position handling.
ACC: in-session compact-to-descriptive track context promotion.
```

State explicitly that telemetry thresholds remain evidence-driven and must be changed in separate regression-tested work.

- [ ] **Step 3: Add the recurring project practice**

Add this requirement to `.codex/project-practices.md` under game and telemetry changes:

```markdown
- Put game-specific metadata, aliases, display rules, telemetry classification, and distance decisions behind `IAffinityGameProfile`. Prefer extending a concrete profile over adding direct normalized game-name comparisons or `IsXGame` branches to `AffinityPlugin` or `AffinitySummaryBuilder`.
```

- [ ] **Step 4: Scan documentation for contradictions**

```powershell
rg -n "AffinityGameLogic|IsXGame|supported game|replay|distance" README.md Affinity-distance-counting.md .codex/project-practices.md docs/superpowers/specs/2026-08-21-affinity-game-profiles-design.md
```

Expected: no document instructs contributors to add new branches to `AffinityGameLogic` or the plugin.

- [ ] **Step 5: Commit documentation and planning artifacts**

```powershell
git add -- README.md Affinity-distance-counting.md .codex/project-practices.md docs/superpowers/specs/2026-08-21-affinity-game-profiles-design.md docs/superpowers/plans/2026-08-21-affinity-game-profiles.md
git commit -m "docs: define Affinity game profile ownership"
```

---

### Task 8: Perform final serial verification and deploy to SimHub

> **Historical correction after implementation review:** The original Task 8 text prescribed a normal build against the default live SimHub path and checked the installed map afterward. That ordering was unsafe because `CopyPluginToSimHub` recursively copies `$(TargetDir)`, which includes `ac_track_id_map.json`. Task 8 was actually completed with the map-safe six-file selective-copy workflow below; this correction makes the committed plan match that reviewed execution history.

**Files:**
- Verify only; no expected source changes.

**Interfaces:**
- Consumes: all completed profile routing and documentation.
- Produces: test/build/deployment evidence for the pull request and user-facing closeout.

- [ ] **Step 1: Inspect repository state and diff scope**

```powershell
git status --short --branch
git diff --stat origin/main...HEAD
git diff --check origin/main...HEAD
```

Expected: only the profile migration, tests, docs, spec, and plan are changed. The three pre-existing screenshot files remain untracked and unstaged. `git diff --check` prints no errors.

- [ ] **Step 2: Run the complete test project**

```powershell
dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist
```

Expected: exit code `0`; all tests pass.

- [ ] **Step 3: Run a fresh no-deploy plugin build**

```powershell
dotnet build .\Affinity\Affinity.csproj /p:SimHubInstallPath=C:\does-not-exist
```

Expected: exit code `0`; `Affinity.dll` builds for `net48` and SQLite native recovery copies are staged under build output.

- [ ] **Step 4: Capture the installed map state and deploy only the six runtime files**

Run this as one block from an elevated PowerShell prompt after Step 3. It canonicalizes and bounds every source and destination, requires exactly the same six-file manifest used by release and beta packaging, records source evidence before copying, and excludes `ac_track_id_map.json`. It does not invoke `CopyPluginToSimHub`.

```powershell
$ErrorActionPreference = 'Stop'

$affinityBuildOutput = (Resolve-Path -LiteralPath '.\Affinity\bin\Debug\net48').Path.TrimEnd('\')
$affinitySimHubInstall = (Resolve-Path -LiteralPath 'C:\Program Files (x86)\SimHub').Path.TrimEnd('\')
$affinityBuildRootPrefix = $affinityBuildOutput + [IO.Path]::DirectorySeparatorChar
$affinityInstallRootPrefix = $affinitySimHubInstall + [IO.Path]::DirectorySeparatorChar
$affinityInstalledMap = Join-Path $affinitySimHubInstall 'ac_track_id_map.json'

if (-not (Test-Path -LiteralPath $affinityInstalledMap -PathType Leaf)) {
    throw "Installed track map is missing: $affinityInstalledMap"
}

$affinityMapBeforeItem = Get-Item -LiteralPath $affinityInstalledMap
$affinityMapBefore = [pscustomobject]@{
    Hash = (Get-FileHash -LiteralPath $affinityInstalledMap -Algorithm SHA256).Hash
    Length = $affinityMapBeforeItem.Length
    LastWriteTimeUtc = $affinityMapBeforeItem.LastWriteTimeUtc.ToString('o')
}

$affinityRuntimeFiles = @(
    'Affinity.dll'
    'System.Data.SQLite.dll'
    'x64\SQLite.Interop.dll'
    'x86\SQLite.Interop.dll'
    'PluginsData\Affinity\sqlite-native\x64\SQLite.Interop.dll'
    'PluginsData\Affinity\sqlite-native\x86\SQLite.Interop.dll'
)

$affinityUniqueRuntimeFiles = @($affinityRuntimeFiles | Select-Object -Unique)
if ($affinityRuntimeFiles.Count -ne 6 -or $affinityUniqueRuntimeFiles.Count -ne 6) {
    throw 'The runtime deployment manifest must contain exactly six unique files.'
}

if ($affinityRuntimeFiles | Where-Object { [IO.Path]::GetFileName($_) -ieq 'ac_track_id_map.json' }) {
    throw 'The runtime deployment manifest must not contain ac_track_id_map.json.'
}

$affinityDeploymentManifest = foreach ($affinityRuntimeFile in $affinityRuntimeFiles) {
    $affinitySourceCandidate = Join-Path $affinityBuildOutput $affinityRuntimeFile
    if (-not (Test-Path -LiteralPath $affinitySourceCandidate -PathType Leaf)) {
        throw "Validated build output is missing: $affinitySourceCandidate"
    }

    $affinitySourcePath = (Resolve-Path -LiteralPath $affinitySourceCandidate).Path
    $affinityDestinationPath = [IO.Path]::GetFullPath(
        (Join-Path $affinitySimHubInstall $affinityRuntimeFile))

    if (-not $affinitySourcePath.StartsWith(
        $affinityBuildRootPrefix,
        [StringComparison]::OrdinalIgnoreCase)) {
        throw "Runtime source escapes the build root: $affinitySourcePath"
    }

    if (-not $affinityDestinationPath.StartsWith(
        $affinityInstallRootPrefix,
        [StringComparison]::OrdinalIgnoreCase)) {
        throw "Runtime destination escapes the SimHub root: $affinityDestinationPath"
    }

    $affinitySourceItem = Get-Item -LiteralPath $affinitySourcePath
    [pscustomobject]@{
        RelativePath = $affinityRuntimeFile
        SourcePath = $affinitySourcePath
        DestinationPath = $affinityDestinationPath
        SourceHash = (Get-FileHash -LiteralPath $affinitySourcePath -Algorithm SHA256).Hash
        SourceLength = $affinitySourceItem.Length
        SourceLastWriteTimeUtc = $affinitySourceItem.LastWriteTimeUtc.ToString('o')
    }
}

$affinityDeploymentResults = foreach ($affinityDeploymentFile in $affinityDeploymentManifest) {
    $affinityDestinationDirectory = Split-Path -Path $affinityDeploymentFile.DestinationPath -Parent
    New-Item -ItemType Directory -Path $affinityDestinationDirectory -Force | Out-Null
    Copy-Item -LiteralPath $affinityDeploymentFile.SourcePath `
        -Destination $affinityDeploymentFile.DestinationPath -Force -ErrorAction Stop

    $affinityDestinationItem = Get-Item -LiteralPath $affinityDeploymentFile.DestinationPath
    $affinityDestinationHash = (Get-FileHash `
        -LiteralPath $affinityDeploymentFile.DestinationPath -Algorithm SHA256).Hash
    $affinityDestinationTimestamp = $affinityDestinationItem.LastWriteTimeUtc.ToString('o')

    if ($affinityDestinationHash -ne $affinityDeploymentFile.SourceHash -or
        $affinityDestinationItem.Length -ne $affinityDeploymentFile.SourceLength -or
        $affinityDestinationTimestamp -ne $affinityDeploymentFile.SourceLastWriteTimeUtc) {
        throw "Deployed file does not match its source: $($affinityDeploymentFile.RelativePath)"
    }

    [pscustomobject]@{
        RelativePath = $affinityDeploymentFile.RelativePath
        SourcePath = $affinityDeploymentFile.SourcePath
        DestinationPath = $affinityDeploymentFile.DestinationPath
        Hash = $affinityDestinationHash
        Length = $affinityDestinationItem.Length
        LastWriteTimeUtc = $affinityDestinationTimestamp
    }
}

$affinityMapAfterItem = Get-Item -LiteralPath $affinityInstalledMap
$affinityMapAfter = [pscustomobject]@{
    Hash = (Get-FileHash -LiteralPath $affinityInstalledMap -Algorithm SHA256).Hash
    Length = $affinityMapAfterItem.Length
    LastWriteTimeUtc = $affinityMapAfterItem.LastWriteTimeUtc.ToString('o')
}

if ($affinityMapAfter.Hash -ne $affinityMapBefore.Hash -or
    $affinityMapAfter.Length -ne $affinityMapBefore.Length -or
    $affinityMapAfter.LastWriteTimeUtc -ne $affinityMapBefore.LastWriteTimeUtc) {
    throw 'The installed ac_track_id_map.json changed during runtime deployment.'
}

$affinityDeploymentResults | Format-Table -AutoSize
$affinityMapBefore
$affinityMapAfter
```

Expected: exactly `Affinity.dll`, `System.Data.SQLite.dll`, both top-level `x86`/`x64` `SQLite.Interop.dll` files, and both `PluginsData\Affinity\sqlite-native` recovery copies deploy to `C:\Program Files (x86)\SimHub\`. Every destination matches its source by SHA-256, length, and UTC last-write timestamp; the installed map matches its before-copy SHA-256, length, and UTC last-write timestamp.

- [ ] **Step 5: Record deployment evidence and handle any lock without bypassing it**

Record the six source/destination rows and the before/after map values reported by Step 4. If SimHub locks a DLL, the copy must stop at that error; do not force around the lock. Report the lock, ask the user to close or restart SimHub, and rerun the complete Step 4 block only after that so it captures a new before-copy map baseline and revalidates all six files.

Do not run `dotnet build .\Affinity\Affinity.csproj` against the default/live path during routine deployment. The `CopyPluginToSimHub` wildcard/full-output target is reserved for an explicit user request to refresh the installed `ac_track_id_map.json` along with the full build output.

- [ ] **Step 6: Prepare the final commit or PR metadata**

Suggested branch: `affinity-game-profiles`

Suggested final/PR title:

```text
patch: route game behavior through Affinity profiles
```

The PR body must summarize the profile boundary, state that behavior and stored identities are unchanged, list the exact test/build commands and results, and state whether SimHub copy succeeded or was blocked.

---

## Self-Review Results

- **Spec coverage:** The tasks cover all nine supported games, metadata, aliases, logos, track/circuit display, generic replay sharing, game-specific telemetry classification, ACC context promotion, every current distance branch, debug settings, persisted identity compatibility, docs, tests, build, and SimHub deployment.
- **Workspace isolation:** Execution begins by detecting an existing linked worktree, preferring native worktree creation, falling back to a verified ignored `.worktrees/` directory, or using the `affinity-game-profiles` feature branch in place. Implementation on `main` is explicitly prohibited.
- **Planning artifacts:** Task 1 explicitly commits both the approved design spec and implementation plan; later edits to either artifact are committed with the task that changes them.
- **Placeholder scan:** The plan contains no deferred implementation markers; each task names exact files, interfaces, expected behaviors, commands, and commit boundaries.
- **Type consistency:** All tasks use `IAffinityGameProfile`, `AffinityGameProfileRegistry`, `AffinityTelemetryContext`, `AffinityGameRuntimeState`, `AffinityDistanceSampleContext`, `TelemetryDisposition`, and `AffinityDistanceMode` consistently with the design spec.
