# Game Logo Design

**Goal:** Add SimHub game logos to the overview `Game` card in `Top overall` and to each game tab with a compact header treatment that preserves the current layout when a logo is missing.

**Context**

- The overview `Top overall` cards and the per-game tab content both render from [Affinity\AffinitySimHub.xaml](C:\Users\micha\dev\AffinitySimHub\Affinity\AffinitySimHub.xaml).
- The featured overview game currently binds to `FeaturedGameTab.GameName`, distance, and time values with no logo support.
- Each game tab is represented by `GameDistanceTab` in [Affinity\AffinityDatabase.cs](C:\Users\micha\dev\AffinitySimHub\Affinity\AffinityDatabase.cs) and rendered through the `GameDistanceTab` data template in XAML.
- The plugin already loads one packaged image through `BitmapImage` in [Affinity\AffinityPlugin.cs](C:\Users\micha\dev\AffinitySimHub\Affinity\AffinityPlugin.cs), so adding runtime-loaded `ImageSource` values fits the current WPF approach.
- Repo guidance says to keep runtime work in `DataUpdate()` lightweight, so logo file resolution should stay out of telemetry hot paths.

**Approved UX**

- Keep the current overview card structure, but add the game logo on the same row as the featured game name in the `Most distance driven` card.
- Add a compact header above each game tab's two top summary cards.
- The game-tab header should contain:
  - the game logo
  - the game name beside it
- Scale the logo as needed within a fixed visual slot rather than forcing a single raw image size.
- If a logo cannot be found or loaded, fall back to the current text-only presentation without showing an error message.

**Logo Source**

- Read logos from the SimHub install `Logos` directory rather than bundling copies into the plugin.
- Use a centralized mapping from Affinity game display names to SimHub logo base names:
  - `RaceRoom Racing Experience` -> `211500`
  - `Assetto Corsa` -> `244210`
  - `rFactor 2` -> `365950`
  - `Automobilista 2` -> `1066890`
  - `Le Mans Ultimate` -> `23994202`
  - `Assetto Corsa EVO` -> `3058630`
  - `iRacing` -> `iRacing`
  - `Assetto Corsa Competizione` -> `805550`
- Normalize known game-name variants before lookup so current code display names still resolve correctly, including case and spacing differences such as:
  - `Rfactor2`
  - `rfactor2`
  - accidental extra spaces in `Assetto Corsa Competizione`
- Because the user supplied image names but not file extensions, the resolver should match by base filename in a case-insensitive way and allow the actual file extension to vary.

**Design**

- Add one reusable logo resolver/service responsible for:
  - converting a game name into the mapped base filename
  - locating the logo file in the SimHub `Logos` directory
  - loading the image into a frozen `BitmapImage`
  - caching resolved `ImageSource` results, including negative results for missing files
- Expose logo state through bound properties rather than resolving files directly inside XAML converters.
- Add logo properties to `GameDistanceTab`, for example:
  - `GameLogo`
  - `HasGameLogo`
- Ensure the featured overview card can bind to the same logo state from `FeaturedGameTab`.
- Keep the resolver on the UI/state-refresh side of the plugin:
  - resolve logos when snapshots or tabs are built/refreshed
  - do not perform file probing from `DataUpdate()`

**Overview Layout**

- In the overview `Most distance driven` card:
  - place the logo immediately left of the featured game name
  - keep the `Most distance driven` label above that row
  - keep distance and `Time driven` values in their current order below
- Constrain the logo with a max height and width so large source images do not distort the card.
- If `FeaturedGameTab` is missing or has no logo, the card should still render the existing text and empty states cleanly.

**Game Tab Layout**

- Insert a compact game identity header at the top of the `GameDistanceTab` template above the two top summary cards.
- The header should show:
  - the scaled logo on the left
  - the game name on the right
- Keep the existing top track and top car summary cards below that header without changing their content hierarchy.
- The header should collapse gracefully to text-only when no logo is available, without leaving an empty placeholder box.

**Implementation Shape**

- In [Affinity\AffinityPlugin.cs](C:\Users\micha\dev\AffinitySimHub\Affinity\AffinityPlugin.cs):
  - add the logo resolver and cache
  - add helpers to resolve the SimHub `Logos` directory from the current install/runtime context
  - keep all file lookup and image loading off the telemetry hot path
- In [Affinity\AffinitySummaryBuilder.cs](C:\Users\micha\dev\AffinitySimHub\Affinity\AffinitySummaryBuilder.cs):
  - populate each `GameDistanceTab` with resolved logo state when tabs are built
- In [Affinity\AffinityDatabase.cs](C:\Users\micha\dev\AffinitySimHub\Affinity\AffinityDatabase.cs):
  - add logo-related properties to `GameDistanceTab`
  - keep them UI-facing and non-persisted
- In [Affinity\AffinitySimHub.xaml](C:\Users\micha\dev\AffinitySimHub\Affinity\AffinitySimHub.xaml):
  - add the overview card logo row
  - add the compact game-tab header above the summary cards
  - use visibility bindings so missing logos degrade to text-only cleanly

**Testing**

- Add tests for the logo mapping/resolution logic to verify:
  - each supported game maps to the expected SimHub logo base name
  - known naming variants normalize correctly
  - unknown games return no logo
- Add tests around any new helper that finds a file by base name so extension handling and case-insensitive matching are explicit.
- Keep existing overview and tab snapshot tests passing to confirm the added properties do not change summary ordering or totals.
- Validation should include:
  - `dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist`
  - `dotnet build .\Affinity\Affinity.csproj /p:SimHubInstallPath=C:\does-not-exist`
  - when practical, a SimHub UI check that confirms:
    - the top-overall game card shows the logo
    - each game tab shows the compact header
    - missing logos fall back to text-only

**Risks**

- SimHub may store logos with unexpected extensions or casing, so matching by base filename instead of a hard-coded full filename is important.
- Game-name drift between telemetry labels and UI display names could break lookup if normalization stays too narrow, so the mapping helper should be explicit and easy to extend.
- Large or inconsistent source image dimensions could crowd the cards if the XAML constraints are loose, so max-size limits need to be part of the first implementation.
