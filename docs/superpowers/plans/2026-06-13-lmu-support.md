# LMU Support Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Le Mans Ultimate (`LMU`) as a supported game that uses Affinity's existing derived-distance flow and existing per-game telemetry debug logging.

**Architecture:** Keep the implementation small by extending the current game-recognition helper layer and the existing derived-distance routing in the plugin. Reuse the current debug logging configuration and log-path derivation so LMU behavior can be validated from real telemetry without creating a separate logging subsystem.

**Tech Stack:** C#, .NET Framework 4.8, MSTest, SimHub plugin APIs

---

### Task 1: Add game-logic coverage first

**Files:**
- Modify: `C:\Users\micha\dev\AffinitySimHub\Affinity.Tests\AffinityGameLogicTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[TestMethod]
public void IsSupportedGame_RecognizesLmuAlias()
{
    Assert.IsTrue(AffinityGameLogic.IsSupportedGame("LMU"));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist --filter IsSupportedGame_RecognizesLmuAlias`
Expected: FAIL because `LMU` is not yet considered a supported game.

- [ ] **Step 3: Write minimal implementation**

```csharp
public static bool IsLmuGame(string gameName)
{
    string normalized = NormalizeGameName(gameName);
    return string.Equals(normalized, "lmu", StringComparison.Ordinal);
}
```

```csharp
public static bool IsSupportedGame(string gameName)
{
    return IsAssettoCorsaGame(gameName) ||
        IsRaceRoomGame(gameName) ||
        IsAutomobilista2Game(gameName) ||
        IsIRacingGame(gameName) ||
        IsRFactor2Game(gameName) ||
        IsLmuGame(gameName);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist --filter IsSupportedGame_RecognizesLmuAlias`
Expected: PASS

### Task 2: Lock LMU to the shared derived-distance path

**Files:**
- Modify: `C:\Users\micha\dev\AffinitySimHub\Affinity.Tests\AffinityPluginTests.cs`
- Modify: `C:\Users\micha\dev\AffinitySimHub\Affinity\AffinityPlugin.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[TestMethod]
public void ResolveSessionDistanceSource_UsesDerivedDistanceForLmu()
{
    AffinityPlugin plugin = new AffinityPlugin();
    StatusDataBase status = new TestStatusData
    {
        TrackLength = 5000.0,
        SessionOdo = 12.34
    };

    object result = InvokeResolveSessionDistanceSource(plugin, "LMU", status);

    Assert.AreEqual("Derived", result.ToString());
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist --filter ResolveSessionDistanceSource_UsesDerivedDistanceForLmu`
Expected: FAIL because LMU currently falls through to odometer/unknown source detection.

- [ ] **Step 3: Write minimal implementation**

```csharp
if (IsAssettoCorsaGame(gameName) || IsRaceRoomGame(gameName) || IsAutomobilista2Game(gameName) || IsIRacingGame(gameName) || IsRFactor2Game(gameName) || IsLmuGame(gameName))
{
    return SessionDistanceSource.Derived;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist --filter ResolveSessionDistanceSource_UsesDerivedDistanceForLmu`
Expected: PASS

### Task 3: Verify the whole targeted set and plugin build

**Files:**
- Verify: `C:\Users\micha\dev\AffinitySimHub\Affinity.Tests\AffinityGameLogicTests.cs`
- Verify: `C:\Users\micha\dev\AffinitySimHub\Affinity.Tests\AffinityPluginTests.cs`
- Verify: `C:\Users\micha\dev\AffinitySimHub\Affinity\AffinityGameLogic.cs`
- Verify: `C:\Users\micha\dev\AffinitySimHub\Affinity\AffinityPlugin.cs`

- [ ] **Step 1: Run the test project**

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist`
Expected: PASS

- [ ] **Step 2: Run the plugin build**

Run: `dotnet build .\Affinity\Affinity.csproj /p:SimHubInstallPath=C:\does-not-exist`
Expected: PASS

- [ ] **Step 3: Build for SimHub deployment**

Run: `dotnet build .\Affinity\Affinity.csproj`
Expected: PASS and copy `Affinity.dll`, `Affinity.pdb`, and `ac_track_id_map.json` into the SimHub install if the files are not locked.

- [ ] **Step 4: Check deployment outcome**

If the copy succeeds, report that the plugin was built and deployed for LMU test laps. If the copy fails because SimHub is locking the DLL, report the lock plainly and ask the user to close or restart SimHub before retrying.
