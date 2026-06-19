# LMU Support Design

**Goal:** Add Le Mans Ultimate support to Affinity using the same derived session-distance calculation path used by the currently supported games, with per-game debug logging available during validation laps.

**Context**

- SimHub exposes the current game name as `LMU`.
- Affinity currently gates supported titles in `AffinityGameLogic`.
- The runtime distance-source choice happens in `AffinityPlugin.ResolveSessionDistanceSource(...)`.
- Debug logging is already keyed per supported game through normalized game-name settings keys and per-game debug log file paths.

**Design**

- Recognize `LMU` as a supported game by normalizing it to `lmu`.
- Add a dedicated helper in `AffinityGameLogic` for LMU, matching the existing per-title helper pattern.
- Route LMU through `SessionDistanceSource.Derived` in `AffinityPlugin.ResolveSessionDistanceSource(...)`.
- Do not add LMU to any iRacing- or rFactor-2-specific reset/wrap heuristics unless testing later shows a need.
- Keep debug logging on the existing shared mechanism so LMU automatically gets:
  - a persisted `GameDebugLogging` settings key of `lmu`
  - a game-specific debug log filename suffix of `.lmu`
  - telemetry snapshots when debug logging is enabled

**Testing**

- Extend `AffinityGameLogicTests` to verify LMU is recognized as supported.
- Add a plugin-level test to verify LMU resolves to the derived session distance source rather than session odometer fallback.

**Risks**

- If LMU reports telemetry with unusual lap-position behavior, derived distance may need game-specific heuristics later.
- The initial implementation intentionally avoids speculative LMU-specific logic so test-lap debug logs can drive any follow-up fixes.
