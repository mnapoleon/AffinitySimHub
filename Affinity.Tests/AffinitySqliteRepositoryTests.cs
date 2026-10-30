using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Affinity.Tests
{
    [TestClass]
    public class AffinitySqliteRepositoryTests
    {
        private string _tempDirectory;
        private string _databasePath;

        [TestInitialize]
        public void SetUp()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "AffinityTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
            _databasePath = Path.Combine(_tempDirectory, "Affinity.distance.db");
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
        public void ImportLegacyDatabase_ImportsAggregateBucketsAsSyntheticSessionsUsingMigrationDate()
        {
            var migrationDateUtc = new DateTime(2026, 5, 30, 0, 0, 0, DateTimeKind.Utc);
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
                                UsedTime = 321.0,
                                CreatedUtc = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                                LastUpdatedUtc = new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc)
                            }
                        }
                    }
                }
            };

            using (var repository = CreateRepository())
            {
                repository.ImportLegacyDatabase(legacy, migrationDateUtc);

                Assert.IsTrue(repository.HasSessionData());

                var summaries = repository.GetDistanceSummaries();
                Assert.AreEqual(1, summaries.Count);
                Assert.AreEqual(4.321, summaries[0].TotalDistanceKm, 0.000001);
                Assert.AreEqual(321.0, summaries[0].UsedTime, 0.000001);
                Assert.AreEqual(migrationDateUtc.AddSeconds(321.0), summaries[0].LastUpdatedUtc);

                AffinityContextTotals totals = repository.GetContextTotals("assetto corsa", "bmw m3 gt2", "MONZA_GP");
                Assert.AreEqual(4321.0, totals.TotalDistanceMeters, 0.000001);
                Assert.AreEqual(321.0, totals.TotalTimeDrivenSeconds, 0.000001);
            }
        }

        [TestMethod]
        public void UpsertSession_UpdatesExistingSessionRowAndAggregatesSeparatelyByContext()
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
                repository.UpsertSession(
                    "session-1",
                    "assetto corsa",
                    "bmw m3 gt2",
                    "monza",
                    "MONZA_GP",
                    new DateTime(2026, 5, 30, 10, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 5, 30, 10, 20, 0, DateTimeKind.Utc),
                    1500.0,
                    1200.0);
                repository.UpsertSession(
                    "session-2",
                    "Assetto Corsa",
                    "Ferrari 488 GT3",
                    "spa",
                    "spa",
                    new DateTime(2026, 5, 30, 11, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 5, 30, 11, 30, 0, DateTimeKind.Utc),
                    5000.0,
                    1800.0);

                var summaries = repository.GetDistanceSummaries();
                Assert.AreEqual(2, summaries.Count);

                var monza = summaries.Single(summary => string.Equals(summary.TrackNameWithConfig, "monza_gp", StringComparison.OrdinalIgnoreCase));
                Assert.AreEqual(1.5, monza.TotalDistanceKm, 0.000001);
                Assert.AreEqual(1200.0, monza.UsedTime, 0.000001);

                var spa = summaries.Single(summary => string.Equals(summary.TrackNameWithConfig, "spa", StringComparison.OrdinalIgnoreCase));
                Assert.AreEqual(5.0, spa.TotalDistanceKm, 0.000001);
                Assert.AreEqual(1800.0, spa.UsedTime, 0.000001);
            }
        }

        [TestMethod]
        public void GetDistanceSummaries_FiltersBySessionDateRange()
        {
            using (var repository = CreateRepository())
            {
                repository.UpsertSession(
                    "april-session",
                    "Assetto Corsa",
                    "BMW M3 GT2",
                    "monza",
                    "monza_gp",
                    new DateTime(2026, 4, 30, 23, 30, 0, DateTimeKind.Utc),
                    new DateTime(2026, 4, 30, 23, 45, 0, DateTimeKind.Utc),
                    2000.0,
                    900.0);
                repository.UpsertSession(
                    "may-session",
                    "Assetto Corsa",
                    "Ferrari 488 GT3",
                    "spa",
                    "spa",
                    new DateTime(2026, 5, 1, 0, 5, 0, DateTimeKind.Utc),
                    new DateTime(2026, 5, 1, 0, 20, 0, DateTimeKind.Utc),
                    5000.0,
                    900.0);

                var aprilSummaries = repository.GetDistanceSummaries(
                    new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));
                var maySummaries = repository.GetDistanceSummaries(
                    new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

                Assert.AreEqual(1, aprilSummaries.Count);
                Assert.AreEqual("BMW M3 GT2", aprilSummaries.Single().CarModel);
                Assert.AreEqual(2.0, aprilSummaries.Single().TotalDistanceKm, 0.000001);

                Assert.AreEqual(1, maySummaries.Count);
                Assert.AreEqual("Ferrari 488 GT3", maySummaries.Single().CarModel);
                Assert.AreEqual(5.0, maySummaries.Single().TotalDistanceKm, 0.000001);
            }
        }

        [TestMethod]
        public void GetDistanceSummaries_FiltersByExactStartedUtcRange()
        {
            using (var repository = CreateRepository())
            {
                repository.UpsertSession(
                    "may-evening-local-session",
                    "Assetto Corsa",
                    "BMW M3 GT2",
                    "monza",
                    "monza_gp",
                    new DateTime(2026, 6, 1, 0, 30, 0, DateTimeKind.Utc),
                    new DateTime(2026, 6, 1, 0, 45, 0, DateTimeKind.Utc),
                    2000.0,
                    900.0);
                repository.UpsertSession(
                    "june-local-session",
                    "Assetto Corsa",
                    "Ferrari 488 GT3",
                    "spa",
                    "spa",
                    new DateTime(2026, 6, 1, 5, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 6, 1, 5, 15, 0, DateTimeKind.Utc),
                    5000.0,
                    900.0);

                var previousLocalMonthSummaries = repository.GetDistanceSummaries(
                    new DateTime(2026, 5, 1, 4, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 6, 1, 4, 0, 0, DateTimeKind.Utc));
                var currentLocalMonthSummaries = repository.GetDistanceSummaries(
                    new DateTime(2026, 6, 1, 4, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 7, 1, 4, 0, 0, DateTimeKind.Utc));

                Assert.AreEqual(1, previousLocalMonthSummaries.Count);
                Assert.AreEqual("BMW M3 GT2", previousLocalMonthSummaries.Single().CarModel);
                Assert.AreEqual(2.0, previousLocalMonthSummaries.Single().TotalDistanceKm, 0.000001);

                Assert.AreEqual(1, currentLocalMonthSummaries.Count);
                Assert.AreEqual("Ferrari 488 GT3", currentLocalMonthSummaries.Single().CarModel);
                Assert.AreEqual(5.0, currentLocalMonthSummaries.Single().TotalDistanceKm, 0.000001);
            }
        }

        [TestMethod]
        public void ExistingDisplayNames_ArePreferredInDistanceSummaries()
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

                var summary = repository.GetDistanceSummaries().Single();
                Assert.AreEqual("BMW M3 GT2 Display", summary.CarModel);
                Assert.AreEqual("Monza GP Display", summary.TrackNameWithConfig);
            }
        }

        [TestMethod]
        public void GetPatternSessions_ReturnsGameScopedDisplayDataWithinHalfOpenRange()
        {
            var startUtc = new DateTime(2026, 5, 1, 4, 0, 0, DateTimeKind.Utc);
            var endUtc = new DateTime(2026, 6, 1, 4, 0, 0, DateTimeKind.Utc);

            using (var repository = CreateRepository())
            {
                repository.UpsertSession("before-start", "Assetto Corsa", "BMW M3 GT2", "monza", "monza_gp",
                    startUtc.AddSeconds(-1), startUtc.AddMinutes(10), 100.0, 60.0);
                repository.UpsertSession("at-start", "Assetto Corsa", "BMW M3 GT2", "monza", "monza_gp",
                    startUtc, startUtc.AddMinutes(10), 1500.0, 600.0);
                repository.UpsertSession("same-context", "Assetto Corsa", "BMW M3 GT2", "monza", "monza_gp",
                    startUtc.AddHours(1), startUtc.AddHours(2), 2500.0, 3600.0);
                repository.UpsertSession("other-game", "iRacing", "BMW M3 GT2", "monza", "Monza GP Display",
                    startUtc.AddDays(1), startUtc.AddDays(1).AddMinutes(20), 5000.0, 1200.0);
                repository.UpsertSession("at-end", "Assetto Corsa", "BMW M3 GT2", "monza", "monza_gp",
                    endUtc, endUtc.AddMinutes(10), 100.0, 60.0);

                using (var connection = new System.Data.SQLite.SQLiteConnection($"Data Source={_databasePath};Version=3;"))
                {
                    connection.Open();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = @"
UPDATE cars SET display_name = 'BMW M3 GT2 Display' WHERE game_id = (SELECT id FROM games WHERE name = 'Assetto Corsa');
UPDATE tracks SET display_name = 'Monza GP Display' WHERE game_id = (SELECT id FROM games WHERE name = 'Assetto Corsa');
UPDATE tracks SET display_name = '' WHERE game_id = (SELECT id FROM games WHERE name = 'iRacing');";
                        command.ExecuteNonQuery();
                    }
                }

                System.Collections.Generic.List<AffinitySessionRecord> sessions = repository.GetPatternSessions(startUtc, endUtc);

                Assert.AreEqual(3, sessions.Count);
                var atStart = sessions.Single(session => session.SessionUid == "at-start");
                Assert.AreEqual("Assetto Corsa", atStart.GameName);
                Assert.AreEqual("BMW M3 GT2 Display", atStart.CarModel);
                Assert.AreEqual("monza", atStart.TrackName);
                Assert.AreEqual("Monza GP Display", atStart.TrackDisplayName);
                Assert.AreEqual(startUtc, atStart.StartedUtc);
                Assert.AreEqual(startUtc.AddMinutes(10), atStart.EndedUtc);
                Assert.AreEqual(DateTimeKind.Utc, atStart.StartedUtc.Kind);
                Assert.AreEqual(DateTimeKind.Utc, atStart.EndedUtc.Kind);
                Assert.AreEqual(1500.0, atStart.DistanceMeters, 0.000001);
                Assert.AreEqual(600.0, atStart.TimeDrivenSeconds, 0.000001);

                var sameContext = sessions.Single(session => session.SessionUid == "same-context");
                Assert.AreEqual(2500.0, sameContext.DistanceMeters, 0.000001);
                Assert.AreEqual(3600.0, sameContext.TimeDrivenSeconds, 0.000001);
                var otherGame = sessions.Single(session => session.SessionUid == "other-game");
                Assert.AreEqual("iRacing", otherGame.GameName);
                Assert.AreEqual("BMW M3 GT2", otherGame.CarModel);
                Assert.AreEqual("monza", otherGame.TrackName);
                Assert.AreEqual("Monza GP Display", otherGame.TrackDisplayName);
            }
        }

        [TestMethod]
        public void GetPatternSessions_ExcludesLegacyRowsFromMixedHistory()
        {
            var startUtc = new DateTime(2026, 5, 30, 0, 0, 0, DateTimeKind.Utc);
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
                                TrackName = "monza",
                                TrackNameWithConfig = "monza_gp",
                                TotalDistanceMeters = 4321.0,
                                UsedTime = 321.0
                            }
                        }
                    }
                }
            };

            using (var repository = CreateRepository())
            {
                repository.ImportLegacyDatabase(legacy, startUtc);
                repository.UpsertSession("normal-session", "Assetto Corsa", "BMW M3 GT2", "monza", "monza_gp",
                    startUtc.AddHours(1), startUtc.AddHours(2), 1000.0, 3600.0);

                var sessions = repository.GetPatternSessions(startUtc, startUtc.AddDays(1));

                Assert.AreEqual(1, sessions.Count);
                Assert.AreEqual("normal-session", sessions.Single().SessionUid);
                Assert.AreEqual(1000.0, sessions.Single().DistanceMeters, 0.000001);
                Assert.AreEqual(3600.0, sessions.Single().TimeDrivenSeconds, 0.000001);
                Assert.AreEqual(5.321, repository.GetDistanceSummaries().Single().TotalDistanceKm, 0.000001);
            }
        }

        private AffinitySqliteRepository CreateRepository()
        {
            var repository = new AffinitySqliteRepository(_databasePath);
            repository.Initialize();
            return repository;
        }
    }
}
