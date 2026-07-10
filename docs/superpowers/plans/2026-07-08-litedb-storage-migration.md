# LiteDB Storage Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move Affinity's active distance/time storage from SQLite to SimHub's bundled LiteDB while preserving existing SQLite session history on first startup after upgrade.

**Architecture:** Add a small repository interface so `AffinityPlugin` can use either SQLite or LiteDB during the migration release. LiteDB becomes the preferred store at `Affinity.distance.litedb`; if the plugin finds `Affinity.distance.db` and no completed LiteDB migration, it imports all SQLite session rows into a temporary LiteDB file, writes a migration metadata record, atomically promotes the LiteDB file, and archives the old SQLite file. If migration fails, the plugin keeps using SQLite for that run and retries on a later startup.

**Tech Stack:** C# .NET Framework 4.8, MSTest, LiteDB 4.1.4 from SimHub, System.Data.SQLite for migration/fallback, Newtonsoft.Json for legacy JSON import.

---

## File Structure

- Create `Affinity/AffinityDistanceRepository.cs`: shared repository contract, session transfer DTO, context totals DTO, and migration metadata DTO.
- Create `Affinity/AffinityLiteDbRepository.cs`: LiteDB-backed implementation of the repository contract.
- Create `Affinity/AffinityStorageMigrator.cs`: startup orchestration for LiteDB preference, SQLite import, temp file promotion, and SQLite archive naming.
- Modify `Affinity/AffinitySqliteRepository.cs`: implement the repository contract and expose complete session export for LiteDB migration.
- Modify `Affinity/AffinityPlugin.cs`: track LiteDB and SQLite paths separately, open the active repository through `AffinityStorageMigrator`, and keep SQLite-native loading available for migration/fallback in this release.
- Modify `Affinity/Affinity.csproj`: reference SimHub's `LiteDB.dll` for compile time with `Private=false`; keep SQLite package and native recovery target for this migration release.
- Modify `Affinity.Tests/Affinity.Tests.csproj`: reference `LiteDB.dll` with `Private=true` for test execution.
- Create `Affinity.Tests/AffinityLiteDbRepositoryTests.cs`: verify LiteDB upsert, grouping, date filtering, legacy JSON import, and migration metadata.
- Create `Affinity.Tests/AffinityStorageMigratorTests.cs`: verify first-run SQLite migration, completed-migration detection, archive behavior, and fallback on failed migration.
- Modify `Affinity.Tests/AffinitySqliteRepositoryTests.cs`: verify full session export preserves raw names, display names, dates, distance, and time.
- Modify `Affinity.Tests/AffinityPluginStorageTests.cs`: update path expectations for `Affinity.distance.litedb` and keep SQLite path checks for migration.
- Modify `Installer/AffinitySetup.iss`: keep SQLite files in the migration release, update user-facing install text to mention LiteDB active storage.
- Modify `README.md`: document LiteDB active storage, SQLite first-run migration, archive file naming, and the temporary SQLite dependency.

## Migration Contract

- Active LiteDB file: `PluginsData\Affinity\Affinity.distance.litedb`.
- Legacy SQLite source file: `PluginsData\Affinity\Affinity.distance.db`.
- Successful SQLite archive file: `PluginsData\Affinity\Affinity.distance.db.migrated.bak`.
- If the archive name already exists, use `Affinity.distance.db.migrated.1.bak`, then `.2.bak`, continuing until an unused path is found.
- Completed migration marker lives inside LiteDB in the `metadata` collection with ID `sqlite-migration`.
- The marker stores `Status = "Complete"`, source path, source length, source last-write UTC, migration UTC, and migrated session count.
- The plugin treats migration as complete when LiteDB exists and either has session data or has the completed marker.
- If the archive rename fails after LiteDB promotion, the plugin still uses LiteDB because the marker proves migration completed; it logs the archive failure and does not re-import the SQLite file.
- If migration fails before LiteDB promotion, the plugin leaves the SQLite file untouched, deletes the temp LiteDB file if present, uses SQLite for the current run, and retries migration at the next startup.

---

### Task 1: Add Shared Repository Types

**Files:**
- Create: `Affinity/AffinityDistanceRepository.cs`
- Modify: `Affinity/AffinitySqliteRepository.cs`
- Test: `Affinity.Tests/AffinitySqliteRepositoryTests.cs`

- [ ] **Step 1: Add shared repository contract and DTOs**

Create `Affinity/AffinityDistanceRepository.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace Affinity
{
    public interface IAffinityDistanceRepository : IDisposable
    {
        void Initialize();

        bool HasSessionData();

        void ImportLegacyDatabase(AffinityDatabase database, DateTime migrationDateUtc);

        void UpsertSession(
            string sessionUid,
            string gameName,
            string carModel,
            string trackName,
            string trackNameWithConfig,
            DateTime startedUtc,
            DateTime endedUtc,
            double distanceMeters,
            double usedTimeSeconds);

        List<DistanceSummary> GetDistanceSummaries(DateTime? sessionDateStartUtc = null, DateTime? sessionDateEndUtc = null);
    }

    public sealed class AffinityContextTotals
    {
        public double TotalDistanceMeters { get; set; }

        public double TotalTimeDrivenSeconds { get; set; }
    }

    public sealed class AffinityDistanceSession
    {
        public string SessionUid { get; set; } = string.Empty;

        public string GameName { get; set; } = string.Empty;

        public string CarModel { get; set; } = string.Empty;

        public string CarDisplayName { get; set; } = string.Empty;

        public string TrackName { get; set; } = string.Empty;

        public string TrackNameWithConfig { get; set; } = string.Empty;

        public string TrackDisplayName { get; set; } = string.Empty;

        public DateTime StartedUtc { get; set; }

        public DateTime EndedUtc { get; set; }

        public DateTime SessionDateUtc { get; set; }

        public double DistanceMeters { get; set; }

        public double TimeDrivenSeconds { get; set; }

        public DateTime CreatedUtc { get; set; }

        public DateTime LastUpdatedUtc { get; set; }
    }

    public sealed class AffinityStorageMigrationMetadata
    {
        public string Id { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public string SourcePath { get; set; } = string.Empty;

        public long SourceLengthBytes { get; set; }

        public DateTime SourceLastWriteUtc { get; set; }

        public DateTime MigratedUtc { get; set; }

        public int MigratedSessionCount { get; set; }
    }
}
```

- [ ] **Step 2: Remove duplicate `AffinityContextTotals` from SQLite repository**

Delete the local `AffinityContextTotals` class from the top of `Affinity/AffinitySqliteRepository.cs` and change the class declaration:

```csharp
public sealed class AffinitySqliteRepository : IAffinityDistanceRepository
```

- [ ] **Step 3: Build to verify the type move is clean**

Run:

```powershell
dotnet build .\Affinity\Affinity.csproj /p:SimHubInstallPath=C:\does-not-exist
```

Expected: build succeeds, with no duplicate `AffinityContextTotals` definition.

- [ ] **Step 4: Commit**

```powershell
git add -- Affinity/AffinityDistanceRepository.cs Affinity/AffinitySqliteRepository.cs
git commit -m "patch: introduce Affinity distance repository contract"
```

---

### Task 2: Decouple Plugin From Concrete SQLite Field

**Files:**
- Modify: `Affinity/AffinityPlugin.cs`
- Test: `Affinity.Tests/AffinityPluginStorageTests.cs`

- [ ] **Step 1: Change the active repository field**

In `Affinity/AffinityPlugin.cs`, replace:

```csharp
private AffinitySqliteRepository _sqliteRepository;
```

with:

```csharp
private IAffinityDistanceRepository _distanceRepository;
private string _sqliteDatabasePath = string.Empty;
private string _liteDbDatabasePath = string.Empty;
```

- [ ] **Step 2: Keep current behavior by assigning SQLite to the interface**

In `InitializeDatabase()`, replace the SQLite field writes with the interface:

```csharp
_distanceRepository = new AffinitySqliteRepository(_databasePath);
_distanceRepository.Initialize();

if (!_distanceRepository.HasSessionData() && File.Exists(_legacyDatabasePath))
{
    AffinityDatabase legacyDatabase = LoadLegacyDatabase();
    _distanceRepository.ImportLegacyDatabase(legacyDatabase, DateTime.UtcNow.Date);
    BackupLegacyDatabaseFile();
    SimHub.Logging.Current.Info($"Affinity - Migrated distance history from {_legacyDatabasePath} to {_databasePath}");
}
```

- [ ] **Step 3: Rename all active repository usages**

In `Affinity/AffinityPlugin.cs`, replace active `_sqliteRepository` calls with `_distanceRepository`.

The important replacements are:

```csharp
_distanceRepository?.Dispose();
_distanceRepository = null;
```

```csharp
if (_distanceRepository == null)
{
    return database;
}

foreach (DistanceSummary summary in _distanceRepository.GetDistanceSummaries())
```

```csharp
_distanceRepository.UpsertSession(
    _activeStorageSessionUid,
    CurrentGameName,
    CurrentCarModel,
    CurrentTrackName,
    CurrentTrackNameWithConfig,
    _activeSessionStartedUtc,
    _lastSessionSampleUtc == DateTime.MinValue ? DateTime.UtcNow : _lastSessionSampleUtc,
    sessionDistanceMeters,
    sessionTimeDrivenSeconds);
```

- [ ] **Step 4: Build to catch missed field names**

Run:

```powershell
dotnet build .\Affinity\Affinity.csproj /p:SimHubInstallPath=C:\does-not-exist
```

Expected: build succeeds.

- [ ] **Step 5: Commit**

```powershell
git add -- Affinity/AffinityPlugin.cs
git commit -m "patch: abstract Affinity distance repository usage"
```

---

### Task 3: Add LiteDB Build References

**Files:**
- Modify: `Affinity/Affinity.csproj`
- Modify: `Affinity.Tests/Affinity.Tests.csproj`
- Add local binary: `lib/SimHub/LiteDB.dll`

- [ ] **Step 1: Add the SimHub LiteDB reference file**

Copy `C:\Program Files (x86)\SimHub\LiteDB.dll` into `lib\SimHub\LiteDB.dll`.

Run:

```powershell
Copy-Item -LiteralPath 'C:\Program Files (x86)\SimHub\LiteDB.dll' -Destination '.\lib\SimHub\LiteDB.dll'
```

Expected: `lib\SimHub\LiteDB.dll` exists and has assembly version `4.1.4.0`.

- [ ] **Step 2: Reference LiteDB in the plugin without copying it into plugin output**

Add this to the existing reference `ItemGroup` in `Affinity/Affinity.csproj`:

```xml
<Reference Include="LiteDB">
  <HintPath>$(SimHubReferencePath)LiteDB.dll</HintPath>
  <Private>False</Private>
</Reference>
```

- [ ] **Step 3: Reference LiteDB in tests and copy it for test execution**

Add this to the reference `ItemGroup` in `Affinity.Tests/Affinity.Tests.csproj`:

```xml
<Reference Include="LiteDB">
  <HintPath>$(SimHubReferencePath)LiteDB.dll</HintPath>
  <Private>true</Private>
</Reference>
```

- [ ] **Step 4: Verify compile references**

Run:

```powershell
dotnet build .\Affinity\Affinity.csproj /p:SimHubInstallPath=C:\does-not-exist
```

Expected: build succeeds.

- [ ] **Step 5: Commit**

```powershell
git add -- lib/SimHub/LiteDB.dll Affinity/Affinity.csproj Affinity.Tests/Affinity.Tests.csproj
git commit -m "patch: reference SimHub LiteDB assembly"
```

---

### Task 4: Implement LiteDB Repository With Tests

**Files:**
- Create: `Affinity/AffinityLiteDbRepository.cs`
- Create: `Affinity.Tests/AffinityLiteDbRepositoryTests.cs`

- [ ] **Step 1: Write failing LiteDB repository tests**

Create `Affinity.Tests/AffinityLiteDbRepositoryTests.cs`:

```csharp
using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Affinity.Tests
{
    [TestClass]
    public class AffinityLiteDbRepositoryTests
    {
        private string _tempDirectory;
        private string _databasePath;

        [TestInitialize]
        public void SetUp()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "AffinityLiteDbRepositoryTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
            _databasePath = Path.Combine(_tempDirectory, "Affinity.distance.litedb");
        }

        [TestCleanup]
        public void TearDown()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        [TestMethod]
        public void UpsertSession_UpdatesExistingSessionAndAggregatesByContext()
        {
            using (var repository = CreateRepository())
            {
                repository.UpsertSession("session-1", "Assetto Corsa", "BMW M3 GT2", "monza", "monza_gp", Utc(2026, 5, 30, 10), Utc(2026, 5, 30, 10, 10), 1000.0, 600.0);
                repository.UpsertSession("session-1", "assetto corsa", "bmw m3 gt2", "monza", "MONZA_GP", Utc(2026, 5, 30, 10), Utc(2026, 5, 30, 10, 20), 1500.0, 1200.0);
                repository.UpsertSession("session-2", "Assetto Corsa", "Ferrari 488 GT3", "spa", "spa", Utc(2026, 5, 30, 11), Utc(2026, 5, 30, 11, 30), 5000.0, 1800.0);

                var summaries = repository.GetDistanceSummaries();

                Assert.AreEqual(2, summaries.Count);
                Assert.AreEqual(1.5, summaries.Single(summary => summary.TrackNameWithConfig.Equals("MONZA_GP", StringComparison.OrdinalIgnoreCase)).TotalDistanceKm, 0.000001);
                Assert.AreEqual(1200.0, summaries.Single(summary => summary.TrackNameWithConfig.Equals("MONZA_GP", StringComparison.OrdinalIgnoreCase)).UsedTime, 0.000001);
                Assert.AreEqual(5.0, summaries.Single(summary => summary.TrackNameWithConfig.Equals("spa", StringComparison.OrdinalIgnoreCase)).TotalDistanceKm, 0.000001);
            }
        }

        [TestMethod]
        public void GetDistanceSummaries_FiltersByExactStartedUtcRange()
        {
            using (var repository = CreateRepository())
            {
                repository.UpsertSession("may-evening-local-session", "Assetto Corsa", "BMW M3 GT2", "monza", "monza_gp", Utc(2026, 6, 1, 0, 30), Utc(2026, 6, 1, 0, 45), 2000.0, 900.0);
                repository.UpsertSession("june-local-session", "Assetto Corsa", "Ferrari 488 GT3", "spa", "spa", Utc(2026, 6, 1, 5), Utc(2026, 6, 1, 5, 15), 5000.0, 900.0);

                var previousLocalMonthSummaries = repository.GetDistanceSummaries(Utc(2026, 5, 1, 4), Utc(2026, 6, 1, 4));
                var currentLocalMonthSummaries = repository.GetDistanceSummaries(Utc(2026, 6, 1, 4), Utc(2026, 7, 1, 4));

                Assert.AreEqual("BMW M3 GT2", previousLocalMonthSummaries.Single().CarModel);
                Assert.AreEqual("Ferrari 488 GT3", currentLocalMonthSummaries.Single().CarModel);
            }
        }

        [TestMethod]
        public void ImportLegacyDatabase_ImportsAggregateBucketsAsSyntheticSessions()
        {
            using (var repository = CreateRepository())
            {
                var legacy = new AffinityDatabase();
                legacy.Games["Assetto Corsa"] = new GameBucket
                {
                    Cars =
                    {
                        ["BMW M3 GT2"] = new CarBucket
                        {
                            Tracks =
                            {
                                ["monza_gp"] = new TrackBucket
                                {
                                    GameName = "Assetto Corsa",
                                    CarModel = "BMW M3 GT2",
                                    TrackName = "monza",
                                    TrackNameWithConfig = "monza_gp",
                                    TotalDistanceMeters = 4321.0,
                                    UsedTime = 321.0
                                }
                            }
                        }
                    }
                };

                repository.ImportLegacyDatabase(legacy, Utc(2026, 5, 30));

                var summary = repository.GetDistanceSummaries().Single();
                Assert.AreEqual(4.321, summary.TotalDistanceKm, 0.000001);
                Assert.AreEqual(321.0, summary.UsedTime, 0.000001);
            }
        }

        [TestMethod]
        public void ImportSessions_WritesCompletedMigrationMetadata()
        {
            using (var repository = CreateRepository())
            {
                repository.ImportSessions(
                    new[]
                    {
                        new AffinityDistanceSession
                        {
                            SessionUid = "sqlite-session-1",
                            GameName = "Assetto Corsa",
                            CarModel = "BMW M3 GT2",
                            TrackName = "monza",
                            TrackNameWithConfig = "monza_gp",
                            StartedUtc = Utc(2026, 5, 30, 10),
                            EndedUtc = Utc(2026, 5, 30, 10, 10),
                            SessionDateUtc = Utc(2026, 5, 30),
                            DistanceMeters = 1234.0,
                            TimeDrivenSeconds = 600.0,
                            CreatedUtc = Utc(2026, 5, 30, 10),
                            LastUpdatedUtc = Utc(2026, 5, 30, 10, 10)
                        }
                    },
                    new AffinityStorageMigrationMetadata
                    {
                        Id = AffinityLiteDbRepository.SqliteMigrationMetadataId,
                        Status = AffinityLiteDbRepository.MigrationStatusComplete,
                        SourcePath = "Affinity.distance.db",
                        SourceLengthBytes = 100,
                        SourceLastWriteUtc = Utc(2026, 5, 29),
                        MigratedUtc = Utc(2026, 5, 30),
                        MigratedSessionCount = 1
                    });

                Assert.IsTrue(repository.HasCompletedSqliteMigration());
                Assert.AreEqual(1, repository.GetDistanceSummaries().Count);
            }
        }

        private AffinityLiteDbRepository CreateRepository()
        {
            var repository = new AffinityLiteDbRepository(_databasePath);
            repository.Initialize();
            return repository;
        }

        private static DateTime Utc(int year, int month, int day, int hour = 0, int minute = 0)
        {
            return new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);
        }
    }
}
```

- [ ] **Step 2: Run tests and verify they fail**

Run:

```powershell
dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist --filter AffinityLiteDbRepositoryTests
```

Expected: compile fails because `AffinityLiteDbRepository` does not exist.

- [ ] **Step 3: Implement LiteDB repository**

Create `Affinity/AffinityLiteDbRepository.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiteDB;

namespace Affinity
{
    public sealed class AffinityLiteDbRepository : IAffinityDistanceRepository
    {
        public const string SqliteMigrationMetadataId = "sqlite-migration";
        public const string MigrationStatusComplete = "Complete";

        private const string SessionsCollectionName = "sessions";
        private const string MetadataCollectionName = "metadata";

        private readonly string _databasePath;
        private LiteDatabase _database;
        private LiteCollection<AffinityDistanceSessionDocument> _sessions;
        private LiteCollection<AffinityStorageMigrationMetadata> _metadata;

        public AffinityLiteDbRepository(string databasePath)
        {
            _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
        }

        public void Initialize()
        {
            string directory = Path.GetDirectoryName(_databasePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _database = new LiteDatabase(_databasePath);
            _sessions = _database.GetCollection<AffinityDistanceSessionDocument>(SessionsCollectionName);
            _metadata = _database.GetCollection<AffinityStorageMigrationMetadata>(MetadataCollectionName);
            _sessions.EnsureIndex(session => session.Id, unique: true);
            _sessions.EnsureIndex(session => session.StartedUtc, unique: false);
            _metadata.EnsureIndex(metadata => metadata.Id, unique: true);
        }

        public bool HasSessionData()
        {
            return _sessions.Count() > 0;
        }

        public bool HasCompletedSqliteMigration()
        {
            AffinityStorageMigrationMetadata metadata = _metadata.FindById(SqliteMigrationMetadataId);
            return string.Equals(metadata?.Status, MigrationStatusComplete, StringComparison.Ordinal);
        }

        public void ImportSessions(IEnumerable<AffinityDistanceSession> sessions, AffinityStorageMigrationMetadata migrationMetadata)
        {
            if (sessions == null)
            {
                return;
            }

            foreach (AffinityDistanceSession session in sessions)
            {
                _sessions.Upsert(ToDocument(session));
            }

            if (migrationMetadata != null)
            {
                _metadata.Upsert(migrationMetadata);
            }
        }

        public void ImportLegacyDatabase(AffinityDatabase database, DateTime migrationDateUtc)
        {
            if (database?.Games == null)
            {
                return;
            }

            DateTime importDateUtc = ToUtc(migrationDateUtc).Date;
            int importedSessionIndex = 0;
            var sessions = new List<AffinityDistanceSession>();

            foreach (KeyValuePair<string, GameBucket> gameEntry in database.Games)
            {
                if (gameEntry.Value?.Cars == null)
                {
                    continue;
                }

                foreach (KeyValuePair<string, CarBucket> carEntry in gameEntry.Value.Cars)
                {
                    if (carEntry.Value?.Tracks == null)
                    {
                        continue;
                    }

                    foreach (KeyValuePair<string, TrackBucket> trackEntry in carEntry.Value.Tracks)
                    {
                        TrackBucket track = trackEntry.Value;
                        if (track == null)
                        {
                            continue;
                        }

                        string trackName = string.IsNullOrWhiteSpace(track.TrackName) ? trackEntry.Key : track.TrackName;
                        string trackNameWithConfig = string.IsNullOrWhiteSpace(track.TrackNameWithConfig) ? trackEntry.Key : track.TrackNameWithConfig;
                        DateTime startedUtc = importDateUtc;
                        DateTime endedUtc = importDateUtc.AddSeconds(Math.Max(0.0, track.UsedTime));

                        sessions.Add(new AffinityDistanceSession
                        {
                            SessionUid = $"legacy-{importDateUtc:yyyyMMdd}-{importedSessionIndex++}",
                            GameName = gameEntry.Key,
                            CarModel = carEntry.Key,
                            TrackName = trackName,
                            TrackNameWithConfig = trackNameWithConfig,
                            StartedUtc = startedUtc,
                            EndedUtc = endedUtc,
                            SessionDateUtc = importDateUtc,
                            DistanceMeters = Math.Max(0.0, track.TotalDistanceMeters),
                            TimeDrivenSeconds = Math.Max(0.0, track.UsedTime),
                            CreatedUtc = track.CreatedUtc == default ? startedUtc : ToUtc(track.CreatedUtc),
                            LastUpdatedUtc = track.LastUpdatedUtc == default ? endedUtc : ToUtc(track.LastUpdatedUtc)
                        });
                    }
                }
            }

            ImportSessions(sessions, null);
        }

        public void UpsertSession(string sessionUid, string gameName, string carModel, string trackName, string trackNameWithConfig, DateTime startedUtc, DateTime endedUtc, double distanceMeters, double usedTimeSeconds)
        {
            DateTime normalizedStartedUtc = ToUtc(startedUtc);
            DateTime normalizedEndedUtc = ToUtc(endedUtc);
            DateTime nowUtc = DateTime.UtcNow;

            _sessions.Upsert(ToDocument(new AffinityDistanceSession
            {
                SessionUid = sessionUid,
                GameName = string.IsNullOrWhiteSpace(gameName) ? "Unknown Game" : gameName.Trim(),
                CarModel = string.IsNullOrWhiteSpace(carModel) ? "Unknown Car" : carModel.Trim(),
                TrackName = string.IsNullOrWhiteSpace(trackName) ? "Unknown Track" : trackName.Trim(),
                TrackNameWithConfig = string.IsNullOrWhiteSpace(trackNameWithConfig) ? "Unknown Track" : trackNameWithConfig.Trim(),
                StartedUtc = normalizedStartedUtc,
                EndedUtc = normalizedEndedUtc,
                SessionDateUtc = normalizedStartedUtc.Date,
                DistanceMeters = Math.Max(0.0, distanceMeters),
                TimeDrivenSeconds = Math.Max(0.0, usedTimeSeconds),
                CreatedUtc = nowUtc,
                LastUpdatedUtc = nowUtc
            }));
        }

        public List<DistanceSummary> GetDistanceSummaries(DateTime? sessionDateStartUtc = null, DateTime? sessionDateEndUtc = null)
        {
            DateTime? startUtc = sessionDateStartUtc.HasValue ? ToUtc(sessionDateStartUtc.Value) : (DateTime?)null;
            DateTime? endUtc = sessionDateEndUtc.HasValue ? ToUtc(sessionDateEndUtc.Value) : (DateTime?)null;
            IEnumerable<AffinityDistanceSessionDocument> rows = _sessions.FindAll();

            if (startUtc.HasValue)
            {
                rows = rows.Where(session => session.StartedUtc >= startUtc.Value);
            }

            if (endUtc.HasValue)
            {
                rows = rows.Where(session => session.StartedUtc < endUtc.Value);
            }

            return rows
                .GroupBy(session => new
                {
                    session.GameName,
                    CarModel = string.IsNullOrWhiteSpace(session.CarDisplayName) ? session.CarModel : session.CarDisplayName,
                    session.TrackName,
                    TrackNameWithConfig = string.IsNullOrWhiteSpace(session.TrackDisplayName) ? session.TrackNameWithConfig : session.TrackDisplayName
                })
                .Select(group =>
                {
                    double totalDistanceMeters = group.Sum(session => session.DistanceMeters);
                    return new DistanceSummary
                    {
                        GameName = group.Key.GameName,
                        CarModel = group.Key.CarModel,
                        TrackName = group.Key.TrackName,
                        TrackNameWithConfig = group.Key.TrackNameWithConfig,
                        TotalDistanceKm = totalDistanceMeters / 1000.0,
                        TotalDistanceMiles = totalDistanceMeters / 1609.344,
                        UsedTime = group.Sum(session => session.TimeDrivenSeconds),
                        LastUpdatedUtc = group.Max(session => session.EndedUtc)
                    };
                })
                .ToList();
        }

        public void Dispose()
        {
            _database?.Dispose();
            _database = null;
            _sessions = null;
            _metadata = null;
        }

        private static AffinityDistanceSessionDocument ToDocument(AffinityDistanceSession session)
        {
            string gameName = string.IsNullOrWhiteSpace(session.GameName) ? "Unknown Game" : session.GameName.Trim();
            string carModel = string.IsNullOrWhiteSpace(session.CarModel) ? "Unknown Car" : session.CarModel.Trim();
            string trackName = string.IsNullOrWhiteSpace(session.TrackName) ? "Unknown Track" : session.TrackName.Trim();
            string trackNameWithConfig = string.IsNullOrWhiteSpace(session.TrackNameWithConfig) ? "Unknown Track" : session.TrackNameWithConfig.Trim();

            return new AffinityDistanceSessionDocument
            {
                Id = string.IsNullOrWhiteSpace(session.SessionUid) ? Guid.NewGuid().ToString("N") : session.SessionUid.Trim(),
                GameName = gameName,
                NormalizedGameName = AffinityGameLogic.NormalizeGameName(gameName),
                CarModel = carModel,
                NormalizedCarModel = NormalizeIdentityValue(carModel),
                CarDisplayName = NormalizeDisplayName(session.CarDisplayName),
                TrackName = trackName,
                TrackNameWithConfig = trackNameWithConfig,
                NormalizedTrackNameWithConfig = NormalizeIdentityValue(trackNameWithConfig),
                TrackDisplayName = NormalizeDisplayName(session.TrackDisplayName),
                StartedUtc = ToUtc(session.StartedUtc),
                EndedUtc = ToUtc(session.EndedUtc),
                SessionDateUtc = ToUtc(session.SessionDateUtc).Date,
                DistanceMeters = Math.Max(0.0, session.DistanceMeters),
                TimeDrivenSeconds = Math.Max(0.0, session.TimeDrivenSeconds),
                CreatedUtc = session.CreatedUtc == default ? ToUtc(session.StartedUtc) : ToUtc(session.CreatedUtc),
                LastUpdatedUtc = session.LastUpdatedUtc == default ? ToUtc(session.EndedUtc) : ToUtc(session.LastUpdatedUtc)
            };
        }

        private static string NormalizeIdentityValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }

        private static string NormalizeDisplayName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static DateTime ToUtc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc)
            {
                return value;
            }

            return value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
                : value.ToUniversalTime();
        }

        private sealed class AffinityDistanceSessionDocument
        {
            public string Id { get; set; } = string.Empty;

            public string GameName { get; set; } = string.Empty;

            public string NormalizedGameName { get; set; } = string.Empty;

            public string CarModel { get; set; } = string.Empty;

            public string NormalizedCarModel { get; set; } = string.Empty;

            public string CarDisplayName { get; set; } = string.Empty;

            public string TrackName { get; set; } = string.Empty;

            public string TrackNameWithConfig { get; set; } = string.Empty;

            public string NormalizedTrackNameWithConfig { get; set; } = string.Empty;

            public string TrackDisplayName { get; set; } = string.Empty;

            public DateTime StartedUtc { get; set; }

            public DateTime EndedUtc { get; set; }

            public DateTime SessionDateUtc { get; set; }

            public double DistanceMeters { get; set; }

            public double TimeDrivenSeconds { get; set; }

            public DateTime CreatedUtc { get; set; }

            public DateTime LastUpdatedUtc { get; set; }
        }
    }
}
```

- [ ] **Step 4: Run LiteDB tests**

Run:

```powershell
dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist --filter AffinityLiteDbRepositoryTests
```

Expected: all `AffinityLiteDbRepositoryTests` pass.

- [ ] **Step 5: Commit**

```powershell
git add -- Affinity/AffinityLiteDbRepository.cs Affinity.Tests/AffinityLiteDbRepositoryTests.cs
git commit -m "patch: add LiteDB distance repository"
```

---

### Task 5: Export Complete SQLite Sessions

**Files:**
- Modify: `Affinity/AffinitySqliteRepository.cs`
- Modify: `Affinity.Tests/AffinitySqliteRepositoryTests.cs`

- [ ] **Step 1: Add failing SQLite export test**

Add this test to `Affinity.Tests/AffinitySqliteRepositoryTests.cs`:

```csharp
[TestMethod]
public void ExportSessions_ReturnsCompleteSessionRowsWithDisplayNames()
{
    using (var repository = CreateRepository())
    {
        repository.UpsertSession(
            "session-1",
            "Assetto Corsa",
            "BMW M3 GT2",
            "monza",
            "monza_gp",
            new DateTime(2026, 5, 30, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 5, 30, 10, 10, 0, DateTimeKind.Utc),
            1000.0,
            600.0);

        using (var connection = new System.Data.SQLite.SQLiteConnection($"Data Source={_databasePath};Version=3;"))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
UPDATE cars SET display_name = 'BMW M3 GT2 Display';
UPDATE tracks SET display_name = 'Monza GP Display';";
                command.ExecuteNonQuery();
            }
        }

        AffinityDistanceSession session = repository.ExportSessions().Single();

        Assert.AreEqual("session-1", session.SessionUid);
        Assert.AreEqual("Assetto Corsa", session.GameName);
        Assert.AreEqual("BMW M3 GT2", session.CarModel);
        Assert.AreEqual("BMW M3 GT2 Display", session.CarDisplayName);
        Assert.AreEqual("monza", session.TrackName);
        Assert.AreEqual("monza_gp", session.TrackNameWithConfig);
        Assert.AreEqual("Monza GP Display", session.TrackDisplayName);
        Assert.AreEqual(1000.0, session.DistanceMeters, 0.000001);
        Assert.AreEqual(600.0, session.TimeDrivenSeconds, 0.000001);
    }
}
```

- [ ] **Step 2: Run test and verify it fails**

Run:

```powershell
dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist --filter ExportSessions_ReturnsCompleteSessionRowsWithDisplayNames
```

Expected: compile fails because `ExportSessions` is missing.

- [ ] **Step 3: Implement `ExportSessions`**

Add this public method to `Affinity/AffinitySqliteRepository.cs`:

```csharp
public List<AffinityDistanceSession> ExportSessions()
{
    var sessions = new List<AffinityDistanceSession>();

    using (var command = _connection.CreateCommand())
    {
        command.CommandText = @"
SELECT
    s.session_uid,
    g.name,
    c.model_name,
    COALESCE(c.display_name, ''),
    t.raw_track_name,
    t.track_name_with_config,
    COALESCE(t.display_name, ''),
    s.started_utc,
    s.ended_utc,
    s.session_date_utc,
    s.distance_meters,
    s.time_driven_seconds,
    s.created_utc,
    s.last_updated_utc
FROM sessions s
INNER JOIN track_contexts tc ON tc.id = s.track_context_id
INNER JOIN games g ON g.id = tc.game_id
INNER JOIN cars c ON c.id = tc.car_id
INNER JOIN tracks t ON t.id = tc.track_id
ORDER BY s.started_utc, s.id;";

        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                sessions.Add(new AffinityDistanceSession
                {
                    SessionUid = reader.GetString(0),
                    GameName = reader.GetString(1),
                    CarModel = reader.GetString(2),
                    CarDisplayName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    TrackName = reader.GetString(4),
                    TrackNameWithConfig = reader.GetString(5),
                    TrackDisplayName = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                    StartedUtc = ReadUtcDateTime(reader, 7),
                    EndedUtc = ReadUtcDateTime(reader, 8),
                    SessionDateUtc = ReadUtcDateTime(reader, 9),
                    DistanceMeters = reader.IsDBNull(10) ? 0.0 : Convert.ToDouble(reader.GetValue(10), CultureInfo.InvariantCulture),
                    TimeDrivenSeconds = reader.IsDBNull(11) ? 0.0 : Convert.ToDouble(reader.GetValue(11), CultureInfo.InvariantCulture),
                    CreatedUtc = ReadUtcDateTime(reader, 12),
                    LastUpdatedUtc = ReadUtcDateTime(reader, 13)
                });
            }
        }
    }

    return sessions;
}
```

- [ ] **Step 4: Run SQLite repository tests**

Run:

```powershell
dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist --filter AffinitySqliteRepositoryTests
```

Expected: all `AffinitySqliteRepositoryTests` pass.

- [ ] **Step 5: Commit**

```powershell
git add -- Affinity/AffinitySqliteRepository.cs Affinity.Tests/AffinitySqliteRepositoryTests.cs
git commit -m "patch: export SQLite distance sessions"
```

---

### Task 6: Add Storage Migrator

**Files:**
- Create: `Affinity/AffinityStorageMigrator.cs`
- Create: `Affinity.Tests/AffinityStorageMigratorTests.cs`

- [ ] **Step 1: Write failing storage migration tests**

Create `Affinity.Tests/AffinityStorageMigratorTests.cs`:

```csharp
using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Affinity.Tests
{
    [TestClass]
    public class AffinityStorageMigratorTests
    {
        private string _tempDirectory;
        private string _sqlitePath;
        private string _liteDbPath;

        [TestInitialize]
        public void SetUp()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "AffinityStorageMigratorTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
            _sqlitePath = Path.Combine(_tempDirectory, "Affinity.distance.db");
            _liteDbPath = Path.Combine(_tempDirectory, "Affinity.distance.litedb");
        }

        [TestCleanup]
        public void TearDown()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        [TestMethod]
        public void OpenRepository_MigratesSqliteIntoLiteDbAndArchivesSqlite()
        {
            using (var sqlite = new AffinitySqliteRepository(_sqlitePath))
            {
                sqlite.Initialize();
                sqlite.UpsertSession("session-1", "Assetto Corsa", "BMW M3 GT2", "monza", "monza_gp", Utc(2026, 5, 30, 10), Utc(2026, 5, 30, 10, 10), 1234.0, 600.0);
            }

            using (AffinityStorageMigrationResult result = AffinityStorageMigrator.OpenRepository(_liteDbPath, _sqlitePath, message => { }, message => { }))
            {
                Assert.IsTrue(result.UsingLiteDb);
                Assert.IsTrue(result.MigratedFromSqlite);
                Assert.IsTrue(File.Exists(_liteDbPath));
                Assert.IsFalse(File.Exists(_sqlitePath));
                Assert.IsTrue(File.Exists(_sqlitePath + ".migrated.bak"));
                Assert.AreEqual(1, result.Repository.GetDistanceSummaries().Count);
            }
        }

        [TestMethod]
        public void OpenRepository_UsesExistingLiteDbWithCompletedMigrationAndDoesNotReimportSqlite()
        {
            using (var liteDb = new AffinityLiteDbRepository(_liteDbPath))
            {
                liteDb.Initialize();
                liteDb.ImportSessions(
                    new AffinityDistanceSession[0],
                    new AffinityStorageMigrationMetadata
                    {
                        Id = AffinityLiteDbRepository.SqliteMigrationMetadataId,
                        Status = AffinityLiteDbRepository.MigrationStatusComplete,
                        SourcePath = _sqlitePath,
                        SourceLengthBytes = 10,
                        SourceLastWriteUtc = Utc(2026, 5, 29),
                        MigratedUtc = Utc(2026, 5, 30),
                        MigratedSessionCount = 0
                    });
            }
            File.WriteAllText(_sqlitePath, "old sqlite remains because archive failed previously");

            using (AffinityStorageMigrationResult result = AffinityStorageMigrator.OpenRepository(_liteDbPath, _sqlitePath, message => { }, message => { }))
            {
                Assert.IsTrue(result.UsingLiteDb);
                Assert.IsFalse(result.MigratedFromSqlite);
                Assert.IsTrue(File.Exists(_sqlitePath));
            }
        }

        [TestMethod]
        public void GetAvailableMigrationArchivePath_AppendsNumberWhenArchiveExists()
        {
            File.WriteAllText(_sqlitePath + ".migrated.bak", "first");

            string archivePath = AffinityStorageMigrator.GetAvailableMigrationArchivePath(_sqlitePath);

            Assert.AreEqual(_sqlitePath + ".migrated.1.bak", archivePath);
        }

        private static DateTime Utc(int year, int month, int day, int hour = 0, int minute = 0)
        {
            return new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);
        }
    }
}
```

- [ ] **Step 2: Run tests and verify they fail**

Run:

```powershell
dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist --filter AffinityStorageMigratorTests
```

Expected: compile fails because `AffinityStorageMigrator` does not exist.

- [ ] **Step 3: Implement migration result and migrator**

Create `Affinity/AffinityStorageMigrator.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;

namespace Affinity
{
    public sealed class AffinityStorageMigrationResult : IDisposable
    {
        public IAffinityDistanceRepository Repository { get; set; }

        public string ActiveDatabasePath { get; set; } = string.Empty;

        public bool UsingLiteDb { get; set; }

        public bool MigratedFromSqlite { get; set; }

        public void Dispose()
        {
            Repository?.Dispose();
            Repository = null;
        }
    }

    public static class AffinityStorageMigrator
    {
        public static AffinityStorageMigrationResult OpenRepository(
            string liteDbPath,
            string sqlitePath,
            Action<string> logInfo,
            Action<string> logWarning)
        {
            logInfo = logInfo ?? (_ => { });
            logWarning = logWarning ?? (_ => { });

            AffinityLiteDbRepository existingLiteDb = TryOpenLiteDb(liteDbPath);
            if (existingLiteDb != null)
            {
                if (existingLiteDb.HasSessionData() || existingLiteDb.HasCompletedSqliteMigration())
                {
                    return new AffinityStorageMigrationResult
                    {
                        Repository = existingLiteDb,
                        ActiveDatabasePath = liteDbPath,
                        UsingLiteDb = true,
                        MigratedFromSqlite = false
                    };
                }

                existingLiteDb.Dispose();
            }

            if (File.Exists(sqlitePath))
            {
                try
                {
                    return MigrateSqliteToLiteDb(liteDbPath, sqlitePath, logInfo, logWarning);
                }
                catch (Exception ex)
                {
                    logWarning($"Affinity - SQLite to LiteDB migration failed; using SQLite for this run: {ex.Message}");
                    return OpenSqliteFallback(sqlitePath);
                }
            }

            AffinityLiteDbRepository liteDb = new AffinityLiteDbRepository(liteDbPath);
            liteDb.Initialize();
            return new AffinityStorageMigrationResult
            {
                Repository = liteDb,
                ActiveDatabasePath = liteDbPath,
                UsingLiteDb = true,
                MigratedFromSqlite = false
            };
        }

        public static string GetAvailableMigrationArchivePath(string sqlitePath)
        {
            string archivePath = sqlitePath + ".migrated.bak";
            if (!File.Exists(archivePath))
            {
                return archivePath;
            }

            int index = 1;
            while (true)
            {
                string indexedPath = sqlitePath + $".migrated.{index}.bak";
                if (!File.Exists(indexedPath))
                {
                    return indexedPath;
                }

                index++;
            }
        }

        private static AffinityStorageMigrationResult MigrateSqliteToLiteDb(
            string liteDbPath,
            string sqlitePath,
            Action<string> logInfo,
            Action<string> logWarning)
        {
            string tempLiteDbPath = liteDbPath + ".migration.tmp";
            DeleteFileIfExists(tempLiteDbPath);

            List<AffinityDistanceSession> sessions;
            FileInfo sourceInfo = new FileInfo(sqlitePath);
            using (var sqlite = new AffinitySqliteRepository(sqlitePath))
            {
                sqlite.Initialize();
                sessions = sqlite.ExportSessions();
            }

            using (var liteDb = new AffinityLiteDbRepository(tempLiteDbPath))
            {
                liteDb.Initialize();
                liteDb.ImportSessions(
                    sessions,
                    new AffinityStorageMigrationMetadata
                    {
                        Id = AffinityLiteDbRepository.SqliteMigrationMetadataId,
                        Status = AffinityLiteDbRepository.MigrationStatusComplete,
                        SourcePath = sqlitePath,
                        SourceLengthBytes = sourceInfo.Length,
                        SourceLastWriteUtc = sourceInfo.LastWriteTimeUtc,
                        MigratedUtc = DateTime.UtcNow,
                        MigratedSessionCount = sessions.Count
                    });
            }

            DeleteFileIfExists(liteDbPath);
            File.Move(tempLiteDbPath, liteDbPath);

            try
            {
                File.Move(sqlitePath, GetAvailableMigrationArchivePath(sqlitePath));
            }
            catch (Exception ex)
            {
                logWarning($"Affinity - LiteDB migration completed, but SQLite archive failed: {ex.Message}");
            }

            var activeLiteDb = new AffinityLiteDbRepository(liteDbPath);
            activeLiteDb.Initialize();
            logInfo($"Affinity - Migrated {sessions.Count} distance sessions from SQLite to LiteDB");
            return new AffinityStorageMigrationResult
            {
                Repository = activeLiteDb,
                ActiveDatabasePath = liteDbPath,
                UsingLiteDb = true,
                MigratedFromSqlite = true
            };
        }

        private static AffinityStorageMigrationResult OpenSqliteFallback(string sqlitePath)
        {
            var sqlite = new AffinitySqliteRepository(sqlitePath);
            sqlite.Initialize();
            return new AffinityStorageMigrationResult
            {
                Repository = sqlite,
                ActiveDatabasePath = sqlitePath,
                UsingLiteDb = false,
                MigratedFromSqlite = false
            };
        }

        private static AffinityLiteDbRepository TryOpenLiteDb(string liteDbPath)
        {
            if (!File.Exists(liteDbPath))
            {
                return null;
            }

            var liteDb = new AffinityLiteDbRepository(liteDbPath);
            liteDb.Initialize();
            return liteDb;
        }

        private static void DeleteFileIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
```

- [ ] **Step 4: Run storage migrator tests**

Run:

```powershell
dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist --filter AffinityStorageMigratorTests
```

Expected: all `AffinityStorageMigratorTests` pass.

- [ ] **Step 5: Commit**

```powershell
git add -- Affinity/AffinityStorageMigrator.cs Affinity.Tests/AffinityStorageMigratorTests.cs
git commit -m "patch: migrate SQLite distance data to LiteDB"
```

---

### Task 7: Wire LiteDB Migration Into Plugin Startup

**Files:**
- Modify: `Affinity/AffinityPlugin.cs`
- Modify: `Affinity.Tests/AffinityPluginStorageTests.cs`

- [ ] **Step 1: Add storage filename constants**

In `Affinity/AffinityPlugin.cs`, replace:

```csharp
private const string SqliteDataFileName = "Affinity.distance.db";
```

with:

```csharp
private const string LiteDbDataFileName = "Affinity.distance.litedb";
private const string SqliteDataFileName = "Affinity.distance.db";
```

- [ ] **Step 2: Resolve both LiteDB and SQLite paths**

In `InitializeStoragePaths`, replace the database path assignments with:

```csharp
_liteDbDatabasePath = Path.Combine(affinityStorageRoot, LiteDbDataFileName);
_sqliteDatabasePath = Path.Combine(affinityStorageRoot, SqliteDataFileName);
_databasePath = _liteDbDatabasePath;
```

Keep SQLite file migration from old storage locations:

```csharp
TryMigrateStorageFile(
    _sqliteDatabasePath,
    Path.Combine(commonAffinityStorageRoot, SqliteDataFileName),
    Path.Combine(commonStorageRoot, SqliteDataFileName));
```

- [ ] **Step 3: Open active repository through migrator**

Replace `InitializeDatabase()` with:

```csharp
private void InitializeDatabase()
{
    try
    {
        AffinityStorageMigrationResult migrationResult = AffinityStorageMigrator.OpenRepository(
            _liteDbDatabasePath,
            _sqliteDatabasePath,
            message => SimHub.Logging.Current.Info(message),
            message => SimHub.Logging.Current.Warn(message));

        _distanceRepository = migrationResult.Repository;
        _databasePath = migrationResult.ActiveDatabasePath;

        if (!_distanceRepository.HasSessionData() && File.Exists(_legacyDatabasePath))
        {
            AffinityDatabase legacyDatabase = LoadLegacyDatabase();
            _distanceRepository.ImportLegacyDatabase(legacyDatabase, DateTime.UtcNow.Date);
            BackupLegacyDatabaseFile();
            SimHub.Logging.Current.Info($"Affinity - Migrated distance history from {_legacyDatabasePath} to {_databasePath}");
        }
    }
    catch (Exception ex)
    {
        HandleStorageInitializationFailure(ex);
    }
}
```

- [ ] **Step 4: Rename SQLite-specific failure handling**

Replace `HandleSqliteInitializationFailure` with:

```csharp
private void HandleStorageInitializationFailure(Exception ex)
{
    SimHub.Logging.Current.Error($"Affinity - Failed to initialize distance storage: {ex}");
    DataStatus = "Affinity storage unavailable; see SimHub log";
    IsTelemetryActive = false;
    _distanceRepository?.Dispose();
    _distanceRepository = null;
}
```

- [ ] **Step 5: Keep SQLite native preload during the migration release**

In `Init`, keep this existing call before `InitializeDatabase()`:

```csharp
EnsureSqliteNativeLibraryReady();
```

Keep `EnsureSqliteNativeLibraryReady`, `TryLoadSqliteInteropLibrary`, and SQLite recovery helpers in this release because the SQLite importer and fallback path still need them. Delete these helpers only in the later release that removes SQLite fallback.

- [ ] **Step 6: Update database backup to active path**

Ensure `BackupDatabaseFile()` uses `_databasePath`, which now points to LiteDB in normal operation and SQLite only during fallback:

```csharp
BackupFileIfPresent(_databasePath, _databasePath + ".bak");
```

- [ ] **Step 7: Build plugin**

Run:

```powershell
dotnet build .\Affinity\Affinity.csproj /p:SimHubInstallPath=C:\does-not-exist
```

Expected: build succeeds.

- [ ] **Step 8: Commit**

```powershell
git add -- Affinity/AffinityPlugin.cs Affinity.Tests/AffinityPluginStorageTests.cs
git commit -m "patch: prefer LiteDB distance storage"
```

---

### Task 8: Update Installer And Docs

**Files:**
- Modify: `Installer/AffinitySetup.iss`
- Modify: `README.md`

- [ ] **Step 1: Update installer completion text**

In `Installer/AffinitySetup.iss`, replace:

```ini
FinishedLabelNoIcons=Affinity and its SQLite dependencies were installed into the selected SimHub folder. Start or restart SimHub to load the plugin.
```

with:

```ini
FinishedLabelNoIcons=Affinity was installed into the selected SimHub folder. This migration release keeps SQLite support for existing data and uses SimHub's bundled LiteDB for new active storage. Start or restart SimHub to load the plugin.
```

- [ ] **Step 2: Update README runtime storage section**

In `README.md`, update the storage wording so it says:

```markdown
Affinity resolves runtime data under `PluginsData\Affinity\`. New distance and time history is stored in `Affinity.distance.litedb` using SimHub's bundled LiteDB assembly.

When Affinity first finds an older `Affinity.distance.db` SQLite file and no completed LiteDB migration marker, it imports every SQLite session into LiteDB, writes a `sqlite-migration` metadata record, and renames the old SQLite file to `Affinity.distance.db.migrated.bak`. If that archive name already exists, Affinity appends a number before `.bak`.

This migration release still includes SQLite support so the plugin can read old databases and fall back safely if migration fails. After the migration has shipped long enough for users to cross over, SQLite packaging can be removed in a follow-up release.
```

- [ ] **Step 3: Verify docs mention both files**

Run:

```powershell
rg -n "Affinity\.distance\.(db|litedb)|SQLite|LiteDB" README.md Installer\AffinitySetup.iss
```

Expected: README mentions LiteDB active storage, SQLite first-run migration, and the migrated SQLite archive.

- [ ] **Step 4: Commit**

```powershell
git add -- README.md Installer/AffinitySetup.iss
git commit -m "patch: document LiteDB storage migration"
```

---

### Task 9: Full Verification And SimHub Deployment

**Files:**
- Verify all modified files
- Deploy built output to SimHub if the DLL is not locked

- [ ] **Step 1: Run full test suite**

Run:

```powershell
dotnet test .\Affinity.Tests\Affinity.Tests.csproj /p:SimHubInstallPath=C:\does-not-exist
```

Expected: all tests pass.

- [ ] **Step 2: Build plugin without live SimHub copy**

Run:

```powershell
dotnet build .\Affinity\Affinity.csproj /p:SimHubInstallPath=C:\does-not-exist
```

Expected: build succeeds.

- [ ] **Step 3: Build plugin with default SimHub deployment**

Run:

```powershell
dotnet build .\Affinity\Affinity.csproj
```

Expected: build succeeds and copies output into `C:\Program Files (x86)\SimHub\` if SimHub is not holding `Affinity.dll` open.

- [ ] **Step 4: Confirm deployed plugin files**

Run:

```powershell
Get-ChildItem -Path 'C:\Program Files (x86)\SimHub' -Filter 'Affinity.dll'
```

Expected: `Affinity.dll` exists in the SimHub folder. If the copy failed because SimHub locked the DLL, close or restart SimHub and rerun Step 3.

- [ ] **Step 5: Final commit**

```powershell
git status --short
git add -- Affinity Affinity.Tests Installer README.md lib/SimHub/LiteDB.dll
git commit -m "patch: switch Affinity storage to LiteDB"
```

Expected: the commit contains the LiteDB repository, migration logic, tests, docs, installer text, and build reference.

---

## Self-Review

- Spec coverage: The plan covers LiteDB active storage, first-run SQLite migration, completed migration detection, archive renaming, fallback to SQLite on migration failure, build references, tests, installer updates, and README updates.
- Placeholder scan: The plan uses concrete file paths, test names, commands, expected outcomes, and code snippets. It contains no unresolved placeholder tokens.
- Type consistency: `IAffinityDistanceRepository`, `AffinityDistanceSession`, `AffinityStorageMigrationMetadata`, `AffinityLiteDbRepository`, `AffinityStorageMigrator`, and `AffinityStorageMigrationResult` are introduced before later tasks use them.
