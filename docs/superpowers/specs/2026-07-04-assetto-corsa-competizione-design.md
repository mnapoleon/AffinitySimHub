# Assetto Corsa Competizione Support Design

## Goal

Add support for Assetto Corsa Competizione (ACC) as a distinct game in Affinity while reusing the runtime distance behavior currently used for Assetto Corsa (AC). ACC totals, tabs, logos, and persisted summaries should remain separate from AC, but ACC should not reuse AC-only track ID mapping unless later testing shows ACC reports the same identifiers and we intentionally opt into that behavior.

## Architecture

ACC support will be added as a first-class supported title in the existing game classification and plugin metadata layers. The existing AC-family helper logic will expand from "AC and AC EVO" to "AC, ACC, and AC EVO" only for the runtime rules we explicitly want to share, primarily derived distance handling and related telemetry flow.

This keeps the implementation localized to the existing game-name normalization, supported-game detection, debug logging metadata, and display/logo helpers. No database schema changes are required because Affinity already stores per-game totals by display name.

## Components

### Game classification

`AffinityGameLogic` will recognize normalized `assettocorsacompetizione` as:

- a supported game
- part of the Assetto Corsa family for shared runtime behavior

That shared family behavior includes:

- derived session distance handling
- any existing runtime branches that already use `IsAssettoCorsaGame(...)`

ACC will not use `ac_track_id_map.json`. Track names for ACC should continue to use the raw reported track value unless a future ACC-specific mapping is added.

### Plugin metadata and settings

`AffinityPlugin` will expose ACC anywhere game-specific metadata is declared so the game behaves consistently in the UI and settings:

- logo resolution remains mapped to the existing ACC Steam art file name
- debug logging settings include a dedicated ACC entry and display name
- supported debug settings keys accept ACC like the other supported titles

### Storage and presentation

ACC remains a separate game label, `Assetto Corsa Competizione`, throughout persisted totals and UI presentation. This preserves separate game tabs, recent highlights, overview summaries, and storage buckets rather than merging ACC driving into AC totals.

## Data Flow

When SimHub reports ACC as the active game, Affinity will normalize the reported name, classify it as a supported Assetto Corsa-family game for distance handling, and then continue through the existing telemetry path. Session distance source selection and summary updates will therefore reuse the same logic that AC already uses, while storing results under the ACC game name. Track display for ACC will continue to use the raw reported track name rather than the AC-specific map.

## Error Handling and Risk

The main risk is assuming ACC telemetry distance semantics match AC closely enough for the shared runtime rules. To keep that risk contained:

- the implementation will avoid introducing new ACC-only branches unless tests or manual validation require them
- the implementation will keep AC-only track mapping excluded from ACC
- ACC remains isolated as its own stored game name, so any later telemetry correction only affects ACC data
- no persisted data migration is needed

If manual testing later shows ACC reports distance differently, the follow-up change can narrow behavior inside the existing AC-family helper without changing storage shape. If ACC later needs friendlier track labels, that should be added as an ACC-specific mapping rather than borrowing AC's track map.

## Testing

Tests will cover:

- supported-game recognition for ACC
- Assetto Corsa-family classification including ACC
- debug logging settings key/display behavior for ACC
- existing logo resolution behavior for ACC display-name variants
- ACC track display continuing to bypass the AC-specific map

No new persistence test shape is required unless implementation reveals a previously hidden AC-only assumption in summary building or storage.
