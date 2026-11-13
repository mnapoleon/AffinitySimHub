# RaceRoom Distance Tracking Implementation Plan

**Goal:** Track actual RaceRoom driving across normal laps, garage returns, and the Nordschleife-NLS shortcut without crediting undriven circuit distance.

**Approach:** First validate a speed-based estimate against fresh telemetry and RaceRoom's in-game track totals. Then use the validated measurement to constrain RaceRoom distance tracking. Preserve the ability to count legitimate out-laps and lap crossings, which motivated the existing RaceRoom lap-counter floor. Keep game-specific decisions behind the RaceRoom profile and retain current behavior for other games.

**Constraints:** Keep telemetry processing lightweight, preserve existing persisted data, and use the map-safe SimHub deployment workflow. The current raw telemetry and speed-estimate logging changes are uncommitted investigation work and must be preserved.

## Evidence and limits

- At Silverstone Stowe, Affinity saved 2.580 km before a garage return and added 2.625 km immediately on departure. The speed estimate was 2.537 km before the return.
- In the three-lap Stowe run, telemetry froze at about 140 km/h after exit. The speed estimate continued rising, so its final logged value is unreliable.
- At Nordschleife-NLS, a shortcut across the layout advanced the lap counter and reset lap position after roughly 3.28 km. The lap-counter floor added 21.04 km in one update.
- The RaceRoom lap-counter floor was added to recover out-lap distance at a line crossing. The existing regression test expects a full lap from a sparse pair of samples near the Tourist line. Verify actual driving evidence before changing that expectation.
- Converting SimHub's `SessionOdo` by 1.609344 gave plausible results in two runs. Its units, provenance, reset behavior, and reliability remain unverified.

## Tasks

1. **Make the diagnostic safe against frozen telemetry.** Inspect RaceRoom raw fields and SimHub data for a reliable advancing timestamp, frame counter, or explicit game-state signal. Log the candidate alongside speed and position before depending on it. Reproduce the recorded exit sequence in `Affinity.Tests/AffinitySpeedDistanceTests.cs` and through the debug logging path in `Affinity.Tests/AffinityPluginReplayTests.cs`: repeated frozen high-speed data must stop increasing the estimate, while genuinely fresh samples at steady speed must continue counting. Cover pause, resume, replay, garage, invalid speed, and long telemetry gaps. Avoid assuming unchanged speed or position alone proves a freeze. If no reliable freshness signal is available, document the limitation and do not promote the estimate to the persisted distance source until it is resolved.

2. **Validate distance sources with controlled RaceRoom runs.** Compare the diagnostic with RaceRoom's in-game per-track total before and after a normal multi-lap run, a partial lap with a garage return, and a Nordschleife-NLS shortcut followed by a normal lap. Record raw and converted `SessionOdo` changes, speed estimate, elapsed time, lap position, and saved rows. Check whether the in-game statistic includes the same activity and updates promptly before treating it as a reference. Measure tolerances and gaps. Do not assume the odometer conversion or use a guessed speed threshold as proof of correctness.

3. **Protect garage boundaries.** Add a regression through the normal telemetry and persistence path for one and two completed laps followed by a garage return. On the 2,632.93 m Silverstone layout, a departure at 170.30 m must add zero at rest and only subsequently driven distance. Preserve the distance already saved before entering the garage. Test repeated returns, zero completed laps, session and context changes, and the first lap crossing after departure. Establish a new RaceRoom segment baseline without crediting carried-over lap count or pit position.

4. **Handle the NLS shortcut and normal crossings.** Add a regression using the observed 24,321.31 m layout and shortcut from approximately 3,560 m to the start line with the lap counter incrementing. Assert that no extra 21.04 km is credited and the following full lap still counts. Add normal multi-lap, partial out-lap, start-near-line, and delayed position/counter update cases, including the existing Tourist regression. Use validated physical movement to constrain any lap-counter correction, and preserve legitimate previously driven distance. Keep the observed shortcut case specific to Nordschleife-NLS without assuming all unusual position changes on other layouts are shortcuts.

5. **Choose and implement the RaceRoom calculation.** Based on tasks 1 and 2, either use the validated speed-based distance as the RaceRoom source or use it to bound the existing position and lap-counter correction. State the chosen method and observed tolerances in the implementation. Reuse the current session lifecycle and ensure inactive, paused, replay, garage, and stale data cannot add distance. Do not change other games' distance rules. If the diagnostic cannot meet these conditions, retain it as an estimate and resolve the gap before changing saved totals.

6. **Document, validate, and deploy.** Update `Affinity-distance-counting.md` and any affected README behavior descriptions. Run `dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist`, then `dotnet build .\Affinity\Affinity.csproj /p:SimHubInstallPath=C:\does-not-exist` serially. Copy the six-file runtime manifest as documented in the README, preserving the installed track map. Repeat the controlled runs and compare debug logs, saved rows, and in-game totals. Report any locked DLL or validation limits.

7. **Investigate prior totals separately.** Identify affected sessions and derive a defensible correction only where the evidence supports one. Known jumps alone do not establish the exact distance actually driven. Present any proposed database corrections before applying them.
