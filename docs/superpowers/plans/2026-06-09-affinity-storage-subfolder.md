# Affinity Storage Subfolder Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move Affinity-owned runtime data from `PluginsData\Common` into `PluginsData\Affinity`, migrate older files safely, and write a rolling SQLite backup on clean shutdown.

**Architecture:** Keep the change centered in `AffinityPlugin` by introducing explicit path-resolution and migration helpers that compute the new storage root, probe older locations in priority order, and handle the shutdown backup after SQLite disposal. Add focused tests around path resolution, migration precedence, legacy JSON fallback, and backup behavior so the storage move is locked in without changing repository or aggregation logic.

**Tech Stack:** C# (.NET Framework 4.8), MSTest, SimHub plugin SDK stubs, System.IO, SQLite repository lifecycle

---

## File Map

- Modify: `Affinity/AffinityPlugin.cs`
  - Resolve `PluginsData\Affinity` paths instead of `Common`.
  - Add helper methods for path migration and fallback probing.
  - Add a shutdown backup helper invoked from `End()`.
- Add: `Affinity.Tests/AffinityPluginStorageTests.cs`
  - Cover path resolution, migration precedence, and shutdown backup behavior with a fake `PluginManager`.
- Modify: `Affinity.Tests/Affinity.Tests.csproj`
  - Ensure the new test file is included if needed by the project structure.

### Task 1: Add storage-path regression tests

**Files:**
- Add: `Affinity.Tests/AffinityPluginStorageTests.cs`

- [ ] **Step 1: Write the failing test for new path resolution**

```csharp
[TestMethod]
public void Init_ResolvesDatabasePathUnderPluginsDataAffinity()
{
    string root = CreateTempStorageRoot();
    var plugin = new AffinityPlugin();
    var pluginManager = new StoragePluginManager(root);

    plugin.Init(pluginManager);

    StringAssert.EndsWith(
        plugin.DatabasePath,
        Path.Combine("PluginsData", "Affinity", "Affinity.distance.db"));
}
```

- [ ] **Step 2: Run the targeted test to verify it fails**

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj --filter Init_ResolvesDatabasePathUnderPluginsDataAffinity /p:SimHubInstallPath=C:\does-not-exist`
Expected: FAIL because `AffinityPlugin` still resolves the database under `Common`.

- [ ] **Step 3: Add migration and backup expectation tests**

```csharp
[TestMethod]
public void Init_MigratesDatabaseFromLegacyCommonRootWhenNewPathMissing()
{
    string root = CreateTempStorageRoot();
    string legacyPath = Path.Combine(root, "PluginsData", "Common", "Affinity.distance.db");
    string newPath = Path.Combine(root, "PluginsData", "Affinity", "Affinity.distance.db");
    Directory.CreateDirectory(Path.GetDirectoryName(legacyPath));
    File.WriteAllText(legacyPath, "legacy-db");

    var plugin = new AffinityPlugin();
    plugin.Init(new StoragePluginManager(root));

    Assert.IsFalse(File.Exists(legacyPath));
    Assert.IsTrue(File.Exists(newPath));
}

[TestMethod]
public void End_CreatesRollingBackupOfDatabase()
{
    string root = CreateTempStorageRoot();
    string newPath = Path.Combine(root, "PluginsData", "Affinity", "Affinity.distance.db");
    Directory.CreateDirectory(Path.GetDirectoryName(newPath));
    File.WriteAllText(newPath, "db");

    var plugin = new AffinityPlugin();
    plugin.Init(new StoragePluginManager(root));

    File.WriteAllText(newPath, "new-db");
    plugin.End(plugin.PluginManager);

    Assert.AreEqual(
        "new-db",
        File.ReadAllText(Path.Combine(root, "PluginsData", "Affinity", "Affinity.distance.db.bak")));
}
```

- [ ] **Step 4: Run the targeted storage tests to verify they fail for the expected reasons**

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj --filter AffinityPluginStorageTests /p:SimHubInstallPath=C:\does-not-exist`
Expected: FAIL with assertions showing the plugin still uses `Common` and does not create the `.bak` file yet.

- [ ] **Step 5: Commit the failing tests**

```bash
git add Affinity.Tests/AffinityPluginStorageTests.cs Affinity.Tests/Affinity.Tests.csproj
git commit -m "test: cover Affinity storage migration"
```

### Task 2: Implement new storage resolution and migration

**Files:**
- Modify: `Affinity/AffinityPlugin.cs`
- Test: `Affinity.Tests/AffinityPluginStorageTests.cs`

- [ ] **Step 1: Add helper methods for new-path resolution and older-path probing**

```csharp
private void InitializeStoragePaths(PluginManager pluginManager)
{
    string commonRoot = pluginManager.GetCommonStoragePath();
    string pluginsDataRoot = Directory.GetParent(commonRoot)?.FullName ?? commonRoot;
    string affinityRoot = Path.Combine(pluginsDataRoot, "Affinity");

    _settingsPath = Path.Combine(affinityRoot, SettingsFileName);
    _databasePath = Path.Combine(affinityRoot, SqliteDataFileName);
    _legacyDatabasePath = ResolveLegacyDataPath(affinityRoot, commonRoot);
    _debugLogPath = Path.Combine(affinityRoot, DebugLogFileName);
}
```

- [ ] **Step 2: Add migration helpers for settings and SQLite database files**

```csharp
private void MigrateFileIfNeeded(string targetPath, params string[] candidatePaths)
{
    if (File.Exists(targetPath))
    {
        return;
    }

    foreach (string candidatePath in candidatePaths)
    {
        if (!File.Exists(candidatePath))
        {
            continue;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
        File.Move(candidatePath, targetPath);
        return;
    }
}
```

- [ ] **Step 3: Update `Init()` to use the new helpers before loading settings and SQLite**

```csharp
public void Init(PluginManager pluginManager)
{
    PluginManager = pluginManager;
    InitializeStoragePaths(pluginManager);
    MigrateStorageFilesIfNeeded();
    _acTrackMapPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ac_track_id_map.json");
    Settings = LoadSettings();
    ...
}
```

- [ ] **Step 4: Run the storage tests to verify the path and migration behavior now passes**

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj --filter AffinityPluginStorageTests /p:SimHubInstallPath=C:\does-not-exist`
Expected: PASS for the path-resolution and migration assertions, with backup-related tests still failing if shutdown backup is not implemented yet.

- [ ] **Step 5: Commit the storage resolution change**

```bash
git add Affinity/AffinityPlugin.cs Affinity.Tests/AffinityPluginStorageTests.cs
git commit -m "patch: move Affinity data under PluginsData"
```

### Task 3: Add rolling shutdown backup and finalize verification

**Files:**
- Modify: `Affinity/AffinityPlugin.cs`
- Test: `Affinity.Tests/AffinityPluginStorageTests.cs`

- [ ] **Step 1: Add the failing test for backup overwrite semantics if still missing**

```csharp
[TestMethod]
public void End_OverwritesExistingDatabaseBackup()
{
    string root = CreateTempStorageRoot();
    string dbPath = Path.Combine(root, "PluginsData", "Affinity", "Affinity.distance.db");
    string backupPath = dbPath + ".bak";
    Directory.CreateDirectory(Path.GetDirectoryName(dbPath));
    File.WriteAllText(dbPath, "fresh-db");
    File.WriteAllText(backupPath, "stale-db");

    var plugin = new AffinityPlugin();
    plugin.Init(new StoragePluginManager(root));
    plugin.End(plugin.PluginManager);

    Assert.AreEqual("fresh-db", File.ReadAllText(backupPath));
}
```

- [ ] **Step 2: Implement the backup helper and call it from `End()` after disposing the repository**

```csharp
public void End(PluginManager pluginManager)
{
    AccumulateActiveSessionTime(DateTime.UtcNow);
    FinalizeActiveSession(refreshSummaries: false);
    _sqliteRepository?.Dispose();
    _sqliteRepository = null;
    BackupDatabaseFile();
    SaveSettings();
    SimHub.Logging.Current.Info("Affinity - Shutting down");
}
```

- [ ] **Step 3: Run the targeted storage tests and then the full test suite**

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj --filter AffinityPluginStorageTests /p:SimHubInstallPath=C:\does-not-exist`
Expected: PASS

Run: `dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist`
Expected: PASS

- [ ] **Step 4: Run the plugin build**

Run: `dotnet build .\Affinity\Affinity.csproj /p:SimHubInstallPath=C:\does-not-exist`
Expected: Build succeeds with no compile errors.

- [ ] **Step 5: Commit the backup implementation**

```bash
git add Affinity/AffinityPlugin.cs Affinity.Tests/AffinityPluginStorageTests.cs
git commit -m "patch: back up Affinity database on shutdown"
```
